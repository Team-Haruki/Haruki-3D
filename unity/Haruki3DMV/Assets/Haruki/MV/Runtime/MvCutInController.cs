using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Core.Live;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haruki.MV
{
    public sealed class MvCutInController : MonoBehaviour, IDisposable, IMvPlaybackParticipant
    {
        public const double OffScreenSimulateDuration = 2.0;

        private MvPlayerAssembler _assembler;
        private PlayableDirector _director;
        private CutInClip[] _clips = Array.Empty<CutInClip>();
        private CutInClip _activeClip;
        private int _offscreenCutInOrder = -1;
        private bool _offscreenMain;

        public int ActiveCutInOrder { get; private set; } = -1;
        public int OffscreenCutInOrder => _offscreenCutInOrder;
        public bool OffscreenMain => _offscreenMain;
        public Color TransitionColor { get; private set; } = Color.black;
        public float TransitionWeight { get; private set; }

        public void Initialize(
            MvPlayerAssembler assembler,
            MvBundleSetLoader bundles,
            int musicId)
        {
            _assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
            if (bundles == null)
            {
                throw new ArgumentNullException(nameof(bundles));
            }

            var bundleName = MvOfficialRuntimeData.ResolveTimelineBundleName(
                musicId,
                "cutin",
                bundles.ContainsBundle);
            if (!bundles.ContainsBundle(bundleName))
            {
                throw new InvalidOperationException(
                    $"CutIn timeline bundle '{bundleName}' is not loaded.");
            }
            var timeline = bundles.LoadAsset<TimelineAsset>(bundleName, "timeline") ??
                throw new InvalidOperationException(
                    $"CutIn timeline bundle '{bundleName}' has no TimelineAsset.");

            var directorObject = new GameObject("CutInTimeline");
            directorObject.transform.SetParent(transform, false);
            _director = directorObject.AddComponent<PlayableDirector>();
            _director.playableAsset = timeline;
            _clips = timeline.GetOutputTracks()
                .SelectMany(track => track.GetClips())
                .Where(clip => clip.asset is CutInClip)
                .Select(clip =>
                {
                    var asset = (CutInClip)clip.asset;
                    asset.Setup(clip.start, clip.duration);
                    return asset;
                })
                .OrderBy(clip => clip.Start)
                .ToArray();
            foreach (var output in timeline.outputs)
            {
                _director.SetGenericBinding(output.sourceObject, this);
            }
            _director.timeUpdateMode = DirectorUpdateMode.Manual;
            _director.time = 0;
            _director.Evaluate();
        }

        public void EvaluateCurrentFrame()
        {
            if (_director == null || _assembler == null)
            {
                return;
            }

            var available = _clips.Where(clip =>
                clip != null && _assembler.HasCutIn(clip.cutinIndex));
            ApplyFrame(ResolveFrame(_director.time, available));
        }

        public void Begin(CutInClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }
            if (!_assembler.HasCutIn(clip.cutinIndex))
            {
                return;
            }
            ActiveCutInOrder = clip.cutinIndex;
            _activeClip = clip;
            _assembler.BeginCutIn(clip.cutinIndex);
            _offscreenCutInOrder = -1;
        }

        public void End(CutInClip clip)
        {
            if (clip != null && ActiveCutInOrder == clip.cutinIndex)
            {
                _assembler.EndCutIn(clip.cutinIndex);
            }
            ActiveCutInOrder = -1;
            _activeClip = null;
            _offscreenMain = false;
            TransitionColor = Color.black;
            TransitionWeight = 0f;
        }

        public void UpdateTransition(CutInClip clip, float localTime, float duration)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }
            var time = Mathf.Clamp(localTime, 0f, duration);
            if (time < clip.entryTransitionInDuration)
            {
                SetTransition(
                    clip.entryTransitionColor,
                    Ratio(time, clip.entryTransitionInDuration));
                return;
            }
            time -= clip.entryTransitionInDuration;
            if (time < clip.entryTransitionOutDuration)
            {
                SetTransition(
                    clip.entryTransitionColor,
                    1f - Ratio(time, clip.entryTransitionOutDuration));
                return;
            }

            var exitStart = duration -
                clip.exitTransitionInDuration -
                clip.exitTransitionOutDuration;
            if (localTime >= exitStart &&
                localTime < exitStart + clip.exitTransitionInDuration)
            {
                SetTransition(
                    clip.exitTransitionColor,
                    Ratio(localTime - exitStart, clip.exitTransitionInDuration));
                return;
            }
            if (localTime >= exitStart + clip.exitTransitionInDuration)
            {
                SetTransition(
                    clip.exitTransitionColor,
                    1f - Ratio(
                        localTime - exitStart - clip.exitTransitionInDuration,
                        clip.exitTransitionOutDuration));
                return;
            }
            SetTransition(Color.black, 0f);
        }

        public void Dispose()
        {
            if (_director != null)
            {
                DestroyOwned(_director.gameObject);
            }
            _director = null;
            _assembler = null;
            _clips = Array.Empty<CutInClip>();
            ActiveCutInOrder = -1;
            _activeClip = null;
            _offscreenCutInOrder = -1;
            _offscreenMain = false;
            TransitionWeight = 0f;
        }

        void IMvPlaybackParticipant.SetPaused(bool paused)
        {
            if (_director == null) return;
            if (paused) _director.Pause();
            else _director.Resume();
        }

        void IMvPlaybackParticipant.Seek(double timeSeconds)
        {
            if (_director == null) return;
            _director.time = timeSeconds;
            _director.Evaluate();
        }

        public void DisposePlayback() => Dispose();

        private void OnGUI()
        {
            if (TransitionWeight <= 0f) return;
            var previous = GUI.color;
            GUI.color = new Color(
                TransitionColor.r,
                TransitionColor.g,
                TransitionColor.b,
                TransitionColor.a * TransitionWeight);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true);
            GUI.color = previous;
        }

        private void SetTransition(Color color, float weight)
        {
            TransitionColor = color;
            TransitionWeight = Mathf.Clamp01(weight);
        }

        public static MvCutInFrameState ResolveFrame(
            double time,
            IEnumerable<CutInClip> clips)
        {
            if (double.IsNaN(time) || double.IsInfinity(time))
            {
                throw new ArgumentOutOfRangeException(nameof(time));
            }
            if (clips == null)
            {
                throw new ArgumentNullException(nameof(clips));
            }

            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (time >= clip.Start && time < clip.End)
                {
                    return new MvCutInFrameState(
                        clip,
                        -1,
                        time >= clip.End - OffScreenSimulateDuration);
                }
                if (time >= clip.Start - OffScreenSimulateDuration && time < clip.Start)
                {
                    return new MvCutInFrameState(null, clip.cutinIndex, false);
                }
            }
            return new MvCutInFrameState(null, -1, false);
        }

        private void ApplyFrame(MvCutInFrameState frame)
        {
            if (!ReferenceEquals(_activeClip, frame.ActiveClip))
            {
                if (_activeClip != null)
                {
                    End(_activeClip);
                }
                if (frame.ActiveClip != null)
                {
                    Begin(frame.ActiveClip);
                }
            }

            if (_offscreenCutInOrder != frame.OffscreenCutInOrder)
            {
                if (_offscreenCutInOrder >= 0)
                {
                    _assembler.SetCutInOffScreenSimulation(_offscreenCutInOrder, false);
                }
                if (frame.OffscreenCutInOrder >= 0)
                {
                    _assembler.SetCutInOffScreenSimulation(frame.OffscreenCutInOrder, true);
                }
                _offscreenCutInOrder = frame.OffscreenCutInOrder;
            }
            if (_offscreenMain != frame.OffscreenMain)
            {
                _assembler.SetMainOffScreenSimulation(frame.OffscreenMain);
                _offscreenMain = frame.OffscreenMain;
            }

            if (frame.ActiveClip != null)
            {
                UpdateTransition(
                    frame.ActiveClip,
                    (float)(_director.time - frame.ActiveClip.Start),
                    (float)frame.ActiveClip.Duration);
            }
        }

        private static float Ratio(float value, float duration)
        {
            return duration > 0f ? Mathf.Clamp01(value / duration) : 1f;
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }

    public readonly struct MvCutInFrameState
    {
        public MvCutInFrameState(
            CutInClip activeClip,
            int offscreenCutInOrder,
            bool offscreenMain)
        {
            ActiveClip = activeClip;
            OffscreenCutInOrder = offscreenCutInOrder;
            OffscreenMain = offscreenMain;
        }

        public CutInClip ActiveClip { get; }
        public int ActiveCutInOrder => ActiveClip == null ? -1 : ActiveClip.cutinIndex;
        public int OffscreenCutInOrder { get; }
        public bool OffscreenMain { get; }
    }
}
