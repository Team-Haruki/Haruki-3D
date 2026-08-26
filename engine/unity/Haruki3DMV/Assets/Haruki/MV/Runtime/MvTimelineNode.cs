using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haruki.MV
{
    public sealed class MvTimelineNode : IDisposable
    {
        public static readonly string[] TimelineNames =
        {
            "Stage",
            "Character",
            "Camera",
            "Light",
            "Effect",
            "Penlight",
        };

        private readonly PlayableDirector[] _directors =
            new PlayableDirector[TimelineNames.Length];
        private Dictionary<string, UnityEngine.Object> _bindingObjects;
        private Transform _root;

        public IReadOnlyList<PlayableDirector> Directors => _directors;
        public MvLiveEffectTimelineManager LiveEffectTimelineManager { get; private set; }
        public double TimelineDuration { get; private set; }
        public double PlaybackDuration { get; private set; }

        public void Initialize(
            Dictionary<string, UnityEngine.Object> bindingObjects,
            Transform root)
        {
            if (bindingObjects == null)
            {
                throw new ArgumentNullException(nameof(bindingObjects));
            }
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            if (_root != null)
            {
                throw new InvalidOperationException("TimelineNode is already initialized.");
            }

            _bindingObjects = bindingObjects;
            _root = root;

            for (var index = 0; index < TimelineNames.Length; index++)
            {
                // The recovered GetTimelineName() hierarchy label is not yet known.
                // The logical name is diagnostic only and is never used for binding.
                var nodeObject = new GameObject(TimelineNames[index]);
                nodeObject.transform.SetParent(root, false);
                _directors[index] = nodeObject.AddComponent<PlayableDirector>();
            }
        }

        public void LoadTimelines(
            Func<string, TimelineAsset> loadTimeline,
            bool isCutIn = false,
            int cutInOrder = -1)
        {
            EnsureInitialized();
            if (loadTimeline == null)
            {
                throw new ArgumentNullException(nameof(loadTimeline));
            }

            TimelineDuration = 0;
            PlaybackDuration = 0;
            for (var index = 0; index < TimelineNames.Length; index++)
            {
                var timelineName = TimelineNames[index];
                var timeline = loadTimeline(timelineName);
                if (timeline == null)
                {
                    throw new InvalidOperationException(
                        $"MV timeline '{timelineName}' could not be loaded.");
                }

                MvReferenceBlendRuntime.Setup(timeline);
                var director = _directors[index];
                director.playableAsset = timeline;
                TimelineDuration = Math.Max(TimelineDuration, timeline.duration);
                if (timelineName == "Character")
                {
                    // Light, penlight and stage tracks may deliberately carry
                    // long tail clips past the music. The official character
                    // timeline is the authoritative visual playback span when
                    // no audio clip is supplied.
                    PlaybackDuration = timeline.duration;
                }

                if (timelineName == "Effect")
                {
                    LiveEffectTimelineManager =
                        director.gameObject.AddComponent<MvLiveEffectTimelineManager>();
                    LiveEffectTimelineManager.Setup(isCutIn, cutInOrder);
                }

                var defaultBinding = timelineName == "Effect"
                    ? director.gameObject
                    : _root.gameObject;
                foreach (var output in timeline.outputs)
                {
                    _bindingObjects[output.streamName] = defaultBinding;
                }
            }
        }

        public void LoadTimelines(
            MvBundleSetLoader bundleSet,
            int mvId,
            bool isCutIn = false,
            int cutInOrder = -1)
        {
            if (bundleSet == null)
            {
                throw new ArgumentNullException(nameof(bundleSet));
            }
            if (mvId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(mvId));
            }

            LoadTimelines(
                timelineName => bundleSet.LoadAsset<TimelineAsset>(
                    MvOfficialRuntimeData.ResolveTimelineBundleName(
                        mvId,
                        timelineName,
                        bundleSet.ContainsBundle),
                    "timeline"),
                isCutIn,
                cutInOrder);
        }

        public void BindTimeline()
        {
            EnsureInitialized();
            foreach (var director in _directors)
            {
                if (director.playableAsset is TimelineAsset timeline)
                {
                    MvTimelineBinding.BindTimeline(director, timeline, _bindingObjects);
                }
            }
        }

        public bool OwnsDirector(PlayableDirector director)
        {
            return director != null && Array.IndexOf(_directors, director) >= 0;
        }

        public bool DrivesAnimator(Animator animator)
        {
            if (animator == null)
            {
                throw new ArgumentNullException(nameof(animator));
            }

            foreach (var director in _directors)
            {
                if (!(director?.playableAsset is TimelineAsset timeline))
                {
                    continue;
                }
                foreach (var output in timeline.outputs)
                {
                    var binding = director.GetGenericBinding(output.sourceObject);
                    if (binding == animator || binding == animator.gameObject)
                    {
                        return true;
                    }
                    var bindingObject = binding is GameObject gameObject
                        ? gameObject
                        : (binding as Component)?.gameObject;
                    if (bindingObject != null &&
                        animator.transform.IsChildOf(bindingObject.transform))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void OnMusicStart(float time)
        {
            EnsureInitialized();
            foreach (var director in _directors)
            {
                director.time = time;
                director.Play();
            }
        }

        public void OnPause(float time)
        {
            EnsureInitialized();
            foreach (var director in _directors)
            {
                director.Pause();
                director.time = time;
            }
        }

        public void OnResume(float time)
        {
            EnsureInitialized();
            foreach (var director in _directors)
            {
                director.Resume();
                director.time = time;
            }
        }

        public void OnSeek(float time)
        {
            EnsureInitialized();
            foreach (var director in _directors)
            {
                director.time = time;
                director.Evaluate();
            }
        }

        public void OnRetry()
        {
            EnsureInitialized();
            foreach (var director in _directors)
            {
                if (!(director.playableAsset is TimelineAsset timeline))
                {
                    continue;
                }

                foreach (var track in timeline.GetOutputTracks())
                {
                    if (track is IMvRetryHandleTrack retryHandle)
                    {
                        retryHandle.OnRetry();
                    }
                }
            }

            LiveEffectTimelineManager?.OnRetry();
        }

        public void Dispose()
        {
            for (var index = 0; index < _directors.Length; index++)
            {
                var director = _directors[index];
                if (director != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(director.gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(director.gameObject);
                    }
                    _directors[index] = null;
                }
            }

            _bindingObjects = null;
            _root = null;
            LiveEffectTimelineManager = null;
            TimelineDuration = 0;
            PlaybackDuration = 0;
        }

        private void EnsureInitialized()
        {
            if (_root == null || _bindingObjects == null)
            {
                throw new InvalidOperationException("TimelineNode is not initialized.");
            }
        }
    }
}
