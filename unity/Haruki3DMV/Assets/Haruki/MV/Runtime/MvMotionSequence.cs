using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Haruki.MV
{
    public sealed class MvMotionSequence : MonoBehaviour, IMvPlaybackParticipant
    {
        private AnimationMixerPlayable _mixer;
        private PlayableGraph _graph;
        private AnimationClipPlayable[] _segments = Array.Empty<AnimationClipPlayable>();
        private double[] _startTimes = Array.Empty<double>();
        private double _time;
        private double _duration;
        private int _activeIndex = -1;
        private bool _paused = true;

        public Playable Mixer => _mixer;
        public double CurrentTime => _time;
        public double Duration => _duration;
        public int ActiveIndex => _activeIndex;
        public bool IsPaused => _paused;

        public void Initialize(PlayableGraph graph, IReadOnlyList<AnimationClip> clips)
        {
            if (!graph.IsValid())
            {
                throw new ArgumentException("A valid PlayableGraph is required.", nameof(graph));
            }
            if (clips == null || clips.Count == 0)
            {
                throw new ArgumentException("At least one motion clip is required.", nameof(clips));
            }
            if (_segments.Length != 0)
            {
                throw new InvalidOperationException("Motion sequence is already initialized.");
            }

            _mixer = AnimationMixerPlayable.Create(graph, clips.Count);
            _graph = graph;
            _segments = new AnimationClipPlayable[clips.Count];
            _startTimes = new double[clips.Count];
            _duration = 0;

            for (var index = 0; index < clips.Count; index++)
            {
                var clip = clips[index];
                if (clip == null)
                {
                    throw new ArgumentException("Motion clips cannot contain null.", nameof(clips));
                }

                _startTimes[index] = _duration;
                _duration += clip.length;
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetSpeed(0);
                playable.SetTime(0);
                graph.Connect(playable, 0, _mixer, index);
                _mixer.SetInputWeight(index, index == 0 ? 1 : 0);
                _segments[index] = playable;
            }

            _activeIndex = 0;
            _time = 0;
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public void Seek(double timeSeconds)
        {
            if (_segments.Length == 0)
            {
                throw new InvalidOperationException("Motion sequence is not initialized.");
            }
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            }

            _time = Math.Max(0, Math.Min(timeSeconds, _duration));
            var active = _segments.Length - 1;
            for (var index = 1; index < _startTimes.Length; index++)
            {
                if (_startTimes[index] > _time)
                {
                    active = index - 1;
                    break;
                }
            }

            if (active != _activeIndex)
            {
                for (var index = 0; index < _segments.Length; index++)
                {
                    _mixer.SetInputWeight(index, index == active ? 1 : 0);
                }
                _activeIndex = active;
            }

            _segments[active].SetTime(Math.Max(0, _time - _startTimes[active]));
            _graph.Evaluate(0);
        }

        public void DisposePlayback()
        {
            _mixer = default;
            _graph = default;
            _segments = Array.Empty<AnimationClipPlayable>();
            _startTimes = Array.Empty<double>();
            _time = 0;
            _duration = 0;
            _activeIndex = -1;
            _paused = true;
        }
    }
}
