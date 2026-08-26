using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Haruki.MV
{
    public sealed class MvTimelinePlaybackParticipant : MonoBehaviour, IMvPlaybackParticipant
    {
        private MvTimelineNode _timeline;
        private double _time;
        private bool _started;
        private bool _paused = true;

        public void Initialize(MvTimelineNode timeline)
        {
            if (_timeline != null)
            {
                throw new InvalidOperationException("Timeline playback is already initialized.");
            }
            _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        }

        public bool OwnsDirector(PlayableDirector director)
        {
            return _timeline != null && _timeline.OwnsDirector(director);
        }

        public bool DrivesAnimator(Animator animator)
        {
            return _timeline != null && _timeline.DrivesAnimator(animator);
        }

        public void SetPaused(bool paused)
        {
            EnsureInitialized();
            _paused = paused;
            if (!isActiveAndEnabled)
            {
                return;
            }
            if (paused)
            {
                _timeline.OnPause((float)_time);
                return;
            }

            if (_started)
            {
                _timeline.OnResume((float)_time);
            }
            else
            {
                _timeline.OnMusicStart((float)_time);
                _started = true;
            }
        }

        public void Seek(double timeSeconds)
        {
            EnsureInitialized();
            _time = timeSeconds;
            if (isActiveAndEnabled)
            {
                _timeline.OnSeek((float)timeSeconds);
            }
        }

        public void Retry()
        {
            EnsureInitialized();
            _timeline.OnRetry();
            _time = 0;
            _started = false;
            _paused = true;
        }

        public void DisposePlayback()
        {
            _timeline = null;
            _time = 0;
            _started = false;
            _paused = true;
        }

        public void ActivateAtCurrentTime()
        {
            EnsureInitialized();
            _timeline.OnSeek((float)_time);
            if (!_paused)
            {
                if (_started)
                {
                    _timeline.OnResume((float)_time);
                }
                else
                {
                    _timeline.OnMusicStart((float)_time);
                    _started = true;
                }
            }
        }

        public void DeactivateAtCurrentTime()
        {
            EnsureInitialized();
            if (_timeline != null && _started && !_paused)
            {
                _timeline.OnPause((float)_time);
            }
        }

        private void EnsureInitialized()
        {
            if (_timeline == null)
            {
                throw new InvalidOperationException("Timeline playback is not initialized.");
            }
        }
    }
}
