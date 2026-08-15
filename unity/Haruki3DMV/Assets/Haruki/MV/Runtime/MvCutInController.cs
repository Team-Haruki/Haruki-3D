using System;
using Sekai.Core.Live;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haruki.MV
{
    public sealed class MvCutInController : MonoBehaviour, IDisposable, IMvPlaybackParticipant
    {
        private MvPlayerAssembler _assembler;
        private PlayableDirector _director;

        public int ActiveCutInOrder { get; private set; } = -1;
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
            foreach (var output in timeline.outputs)
            {
                _director.SetGenericBinding(output.sourceObject, this);
            }
            _director.timeUpdateMode = DirectorUpdateMode.Manual;
            _director.time = 0;
            _director.Evaluate();
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
            _assembler.SetCutInSceneActive(clip.cutinIndex, true);
        }

        public void End(CutInClip clip)
        {
            if (clip != null && ActiveCutInOrder == clip.cutinIndex)
            {
                _assembler.SetCutInSceneActive(clip.cutinIndex, false);
            }
            ActiveCutInOrder = -1;
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
            ActiveCutInOrder = -1;
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
}
