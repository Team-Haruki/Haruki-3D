using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

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

        public MvPlaybackState State { get; private set; } = MvPlaybackState.Empty;
        public double CurrentTimeSeconds => _timeSeconds;
        public double DurationSeconds => _durationSeconds;

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
                ? audioSource.clip.length
                : durationSeconds;
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
            SetPaused(true);
        }

        public void SetPaused(bool paused)
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
            else
            {
                _audioSource.Play();
                _audioStarted = true;
            }
        }

        public void Seek(double timeSeconds)
        {
            EnsureBound();
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            }

            SeekInternal(timeSeconds);
        }

        public void Retry()
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
            SeekInternal(0);
            SetPaused(true);
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
                if (!_audioSource.isPlaying)
                {
                    SeekVisuals(_durationSeconds);
                    _audioStarted = false;
                    SetPaused(true);
                    return;
                }
                SeekVisuals(_audioSource.time);
                return;
            }

            var nextTime = _timeSeconds + Time.unscaledDeltaTime;
            SeekVisuals(nextTime);
            if (_timeSeconds >= _durationSeconds)
            {
                SetPaused(true);
            }
        }

        private void SeekInternal(double timeSeconds)
        {
            SeekVisuals(timeSeconds);

            if (_audioSource == null || _audioSource.clip == null)
            {
                return;
            }

            var audioTime = Math.Min(_timeSeconds, Math.Max(0, _audioSource.clip.length - 0.001));
            _audioSource.time = (float)audioTime;
        }

        private void SeekVisuals(double timeSeconds)
        {
            _timeSeconds = Math.Max(0, Math.Min(timeSeconds, _durationSeconds));

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
