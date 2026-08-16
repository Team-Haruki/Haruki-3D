using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Sekai.Core;
using Sekai.Rendering;

namespace Haruki.MV
{
    public enum MvPlaybackState
    {
        Empty,
        Paused,
        Playing
    }

    public sealed class MvPlaybackCoordinator : MonoBehaviour
    {
        private GameObject[] _sceneRoots = Array.Empty<GameObject>();
        private PlayableDirector[] _directors = Array.Empty<PlayableDirector>();
        private IMvPlaybackParticipant[] _participants = Array.Empty<IMvPlaybackParticipant>();
        private MvTimelinePlaybackParticipant[] _timelineParticipants =
            Array.Empty<MvTimelinePlaybackParticipant>();
        private AudioSource _audioSource;
        private double _durationSeconds;
        private double _timeSeconds;
        private bool _audioStarted;
        private bool _audioWasPlaying;
        private bool _audioCompleted;
        private bool _audioClockInitialized;
        private long _referenceAudioMilliseconds;
        private double _referenceUnityTime;

        public MvPlaybackState State { get; private set; } = MvPlaybackState.Empty;
        public double CurrentTimeSeconds => _timeSeconds;
        public double DurationSeconds => _durationSeconds;
        public string AudioClipName => _audioSource?.clip != null
            ? _audioSource.clip.name
            : null;
        public bool AudioStarted => _audioStarted;
        public bool AudioIsPlaying => _audioSource != null && _audioSource.isPlaying;
        public string AudioLoadState => _audioSource?.clip != null
            ? _audioSource.clip.loadState.ToString()
            : AudioDataLoadState.Unloaded.ToString();
        public double AudioDurationSeconds => _audioSource?.clip != null
            ? ResolveAudioClipDuration(_audioSource.clip)
            : 0;
        public double AudioTimeSeconds => _audioSource?.clip != null &&
            _audioSource.clip.loadState == AudioDataLoadState.Loaded
                ? _audioSource.time
                : 0;

        public void BindScene(GameObject sceneRoot, AudioSource audioSource, double durationSeconds)
        {
            if (sceneRoot == null)
            {
                throw new ArgumentNullException(nameof(sceneRoot));
            }

            BindScene(new[] { sceneRoot }, audioSource, durationSeconds);
        }

        public void BindScene(GameObject[] sceneRoots, AudioSource audioSource, double durationSeconds)
        {
            BindScene(sceneRoots, audioSource, durationSeconds, null);
        }

        public void BindScene(
            GameObject[] sceneRoots,
            AudioSource audioSource,
            double durationSeconds,
            IReadOnlyList<IMvPlaybackParticipant> externalParticipants)
        {
            if (sceneRoots == null || sceneRoots.Length == 0)
            {
                throw new ArgumentException("An MV scene must contain at least one root.", nameof(sceneRoots));
            }

            foreach (var sceneRoot in sceneRoots)
            {
                if (sceneRoot == null)
                {
                    throw new ArgumentException("MV scene roots cannot contain null.", nameof(sceneRoots));
                }
            }

            if (durationSeconds < 0 || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            }

            DisposeScene();
            _sceneRoots = (GameObject[])sceneRoots.Clone();
            _audioSource = audioSource;
            _durationSeconds = audioSource != null && audioSource.clip != null
                ? Math.Max(ResolveAudioClipDuration(audioSource.clip), durationSeconds)
                : durationSeconds;
            _audioWasPlaying = false;
            _audioCompleted = false;
            _directors = CollectComponents<PlayableDirector>(_sceneRoots);
            var behaviours = CollectComponents<MonoBehaviour>(_sceneRoots);
            var participants = new List<IMvPlaybackParticipant>();
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IMvPlaybackParticipant participant)
                {
                    participants.Add(participant);
                }
            }
            if (externalParticipants != null)
            {
                foreach (var participant in externalParticipants)
                {
                    if (participant != null && !participants.Contains(participant))
                    {
                        participants.Add(participant);
                    }
                }
            }
            _participants = participants.ToArray();
            _timelineParticipants = CollectComponents<MvTimelinePlaybackParticipant>(_sceneRoots);

            foreach (var animator in CollectComponents<Animator>(_sceneRoots))
            {
                animator.applyRootMotion = false;
            }

            foreach (var director in _directors)
            {
                director.timeUpdateMode = DirectorUpdateMode.Manual;
            }

            SeekInternal(0);
            SetPlaybackPaused(true);
        }

        public void SetPlaybackPaused(bool paused)
        {
            EnsureBound();
            State = paused ? MvPlaybackState.Paused : MvPlaybackState.Playing;

            foreach (var participant in _participants)
            {
                participant.SetPaused(paused);
            }

            if (_audioSource == null || _audioSource.clip == null)
            {
                return;
            }

            if (paused)
            {
                if (_audioStarted)
                {
                    _audioSource.Pause();
                }
            }
            else if (_audioStarted)
            {
                _audioSource.UnPause();
            }
            else if (!_audioCompleted)
            {
                _audioSource.Play();
                _audioStarted = true;
                _audioWasPlaying = false;
            }
            ResetAudioClock();
        }

        public void SeekTo(double timeSeconds)
        {
            EnsureBound();
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            }

            SeekInternal(timeSeconds);
        }

        public void SetActiveSceneRoot(GameObject activeRoot)
        {
            EnsureBound();
            if (activeRoot == null || Array.IndexOf(_sceneRoots, activeRoot) < 0)
            {
                throw new ArgumentException(
                    "The active MV root must belong to the bound scene.",
                    nameof(activeRoot));
            }

            foreach (var transitionRoot in SceneRootTransitionPath(_sceneRoots, activeRoot))
            {
                ApplyActiveSceneRoot(transitionRoot);
            }
        }

        public static GameObject[] SceneRootTransitionPath(
            IReadOnlyList<GameObject> sceneRoots,
            GameObject activeRoot)
        {
            if (sceneRoots == null || sceneRoots.Count == 0)
            {
                throw new ArgumentException("At least one MV scene root is required.", nameof(sceneRoots));
            }

            var mainRoot = sceneRoots[0];
            if (activeRoot != mainRoot)
            {
                for (var index = 1; index < sceneRoots.Count; index++)
                {
                    var otherCutIn = sceneRoots[index];
                    if (otherCutIn != activeRoot && otherCutIn.activeSelf)
                    {
                        return new[] { mainRoot, activeRoot };
                    }
                }
            }
            return new[] { activeRoot };
        }

        private void ApplyActiveSceneRoot(GameObject activeRoot)
        {
            foreach (var root in _sceneRoots)
            {
                var active = root == activeRoot;
                if (root.activeSelf == active)
                {
                    continue;
                }

                foreach (var participant in TimelineParticipantsUnder(root))
                {
                    if (!active)
                    {
                        participant.DeactivateAtCurrentTime();
                    }
                }
                root.SetActive(active);
                if (active)
                {
                    foreach (var participant in TimelineParticipantsUnder(root))
                    {
                        participant.ActivateAtCurrentTime();
                    }
                }
            }

            // These official systems are shader-global. Re-assert the final
            // active player's owner after all roots have toggled so an inactive
            // CutIn cannot win through its OnDisable ordering.
            var spotLight = activeRoot.GetComponentInChildren<Sekai.SekaiGlobalSpotLight>(true);
            if (spotLight != null)
            {
                spotLight.ApplyShaderGlobals();
                spotLight.SetEnabled(true);
            }
            var waterCaustics =
                activeRoot.GetComponentInChildren<SekaiGlobalFlipBookProjector>(true);
            if (waterCaustics != null) waterCaustics.Setup();
            else SekaiGlobalFlipBookProjector.SetFlipBookActive(false);
        }

        public void RetryPlayback()
        {
            EnsureBound();
            foreach (var participant in _timelineParticipants)
            {
                participant.Retry();
            }
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
            _audioStarted = false;
            _audioWasPlaying = false;
            _audioCompleted = false;
            _audioClockInitialized = false;
            SeekInternal(0);
            SetPlaybackPaused(true);
        }

        public void DisposeScene()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }

            foreach (var director in _directors)
            {
                if (director != null)
                {
                    director.Stop();
                }
            }

            foreach (var participant in _participants)
            {
                participant.DisposePlayback();
            }

            _sceneRoots = Array.Empty<GameObject>();
            _directors = Array.Empty<PlayableDirector>();
            _participants = Array.Empty<IMvPlaybackParticipant>();
            _timelineParticipants = Array.Empty<MvTimelinePlaybackParticipant>();
            _audioSource = null;
            _durationSeconds = 0;
            _timeSeconds = 0;
            _audioStarted = false;
            _audioWasPlaying = false;
            _audioCompleted = false;
            _audioClockInitialized = false;
            State = MvPlaybackState.Empty;
        }

        private void Update()
        {
            if (State != MvPlaybackState.Playing)
            {
                return;
            }

            if (_audioSource != null && _audioSource.clip != null && _audioStarted)
            {
                var audioLoadState = _audioSource.clip.loadState;
                if (audioLoadState == AudioDataLoadState.Unloaded ||
                    audioLoadState == AudioDataLoadState.Loading)
                {
                    return;
                }
                if (audioLoadState == AudioDataLoadState.Failed)
                {
                    _audioStarted = false;
                    _audioCompleted = true;
                    _audioClockInitialized = false;
                    Debug.LogError("MV audio data failed to load; visual playback will continue.");
                }
                else if (!_audioSource.isPlaying && !_audioWasPlaying)
                {
                    _audioSource.Play();
                    ResetAudioClock();
                    return;
                }
                else if (!_audioSource.isPlaying)
                {
                    _audioStarted = false;
                    _audioWasPlaying = false;
                    _audioCompleted = true;
                    _audioClockInitialized = false;
                    SeekVisuals(Math.Max(
                        _timeSeconds,
                        ResolveAudioClipDuration(_audioSource.clip)));
                }
                else
                {
                    _audioWasPlaying = true;
                    var unityNow = Time.unscaledTimeAsDouble;
                    var audioMilliseconds = (long)(_audioSource.time * 1000.0f);
                    if (!_audioClockInitialized)
                    {
                        ResetAudioClock();
                    }
                    var predicted = _referenceAudioMilliseconds
                        + (long)((unityNow - _referenceUnityTime)
                            * _audioSource.pitch * 1000.0);
                    var synced = ResolveAudioSyncedTimeMilliseconds(
                        predicted,
                        audioMilliseconds);
                    if (audioMilliseconds - predicted >= 63)
                    {
                        _referenceAudioMilliseconds = audioMilliseconds;
                        _referenceUnityTime = unityNow;
                    }
                    SeekVisuals(synced / 1000.0);
                    return;
                }
            }

            var nextTime = _timeSeconds + Time.unscaledDeltaTime;
            SeekVisuals(nextTime);
            if (_timeSeconds >= _durationSeconds)
            {
                SetPlaybackPaused(true);
            }
        }

        private void SeekInternal(double timeSeconds)
        {
            SeekVisuals(timeSeconds);

            if (_audioSource == null || _audioSource.clip == null)
            {
                return;
            }

            var clipLength = ResolveAudioClipDuration(_audioSource.clip);
            var withinAudio = clipLength <= 0 || _timeSeconds < clipLength;
            var audioTime = clipLength > 0
                ? Math.Min(_timeSeconds, Math.Max(0, clipLength - 0.001))
                : _timeSeconds;
            if (!withinAudio)
            {
                if (_audioStarted)
                {
                    _audioSource.Stop();
                }
                _audioStarted = false;
                _audioCompleted = true;
            }
            else if (_audioCompleted)
            {
                _audioCompleted = false;
                if (State == MvPlaybackState.Playing)
                {
                    _audioSource.Play();
                    _audioStarted = true;
                    _audioWasPlaying = false;
                }
            }
            _audioSource.time = (float)audioTime;
            ResetAudioClock();
        }

        private static double ResolveAudioClipDuration(AudioClip clip)
        {
            return clip != null && clip.frequency > 0
                ? (double)clip.samples / clip.frequency
                : 0;
        }

        private static long ResolveAudioSyncedTimeMilliseconds(
            long predicted,
            long audioMilliseconds)
        {
            if (predicted - audioMilliseconds >= 63)
            {
                return audioMilliseconds + 62;
            }
            return audioMilliseconds - predicted >= 63
                ? audioMilliseconds
                : predicted;
        }

        private void ResetAudioClock()
        {
            if (_audioSource == null || _audioSource.clip == null)
            {
                _audioClockInitialized = false;
                return;
            }
            _referenceAudioMilliseconds = (long)(_audioSource.time * 1000.0f);
            _referenceUnityTime = Time.unscaledTimeAsDouble;
            _audioClockInitialized = true;
        }

        private void SeekVisuals(double timeSeconds)
        {
            _timeSeconds = Math.Max(0, Math.Min(timeSeconds, _durationSeconds));
            LiveMonitorRuntime.SetTime(_timeSeconds);
            Shader.SetGlobalFloat("_SekaiGlobalEyeTime", (float)_timeSeconds);
            SekaiGlobalFlipBookProjector.SetTime((float)_timeSeconds);

            foreach (var director in _directors)
            {
                if (director == null || director.playableAsset == null)
                {
                    continue;
                }
                if (Array.Exists(
                    _timelineParticipants,
                    participant => participant.OwnsDirector(director)))
                {
                    continue;
                }

                director.time = _timeSeconds;
                director.Evaluate();
            }

            foreach (var participant in _participants)
            {
                participant.Seek(_timeSeconds);
            }
        }

        private void EnsureBound()
        {
            if (_sceneRoots.Length == 0)
            {
                throw new InvalidOperationException("No MV scene is bound.");
            }
        }

        private IEnumerable<MvTimelinePlaybackParticipant> TimelineParticipantsUnder(
            GameObject root)
        {
            foreach (var participant in _timelineParticipants)
            {
                if (participant != null &&
                    (participant.gameObject == root || participant.transform.IsChildOf(root.transform)))
                {
                    yield return participant;
                }
            }
        }

        private static T[] CollectComponents<T>(IEnumerable<GameObject> roots) where T : Component
        {
            var components = new List<T>();
            foreach (var root in roots)
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return components.ToArray();
        }
    }
}
