using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;

namespace Haruki.MV.Tests
{
    public sealed class MvMotionSequenceTests
    {
        private GameObject _host;
        private PlayableGraph _graph;
        private AnimationClip _first;
        private AnimationClip _second;
        private MvMotionSequence _sequence;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Character");
            _graph = PlayableGraph.Create("MvMotionSequenceTests");
            _first = CreateClip("First", 1);
            _second = CreateClip("Second", 2);
            _sequence = _host.AddComponent<MvMotionSequence>();
            _sequence.Initialize(_graph, new[] { _first, _second });
        }

        [TearDown]
        public void TearDown()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(_second);
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void SeekSelectsExactlyOneSegmentAtItsLocalTime()
        {
            _sequence.Seek(1.5);

            Assert.That(_sequence.ActiveIndex, Is.EqualTo(1));
            Assert.That(_sequence.CurrentTime, Is.EqualTo(1.5).Within(0.001));
            Assert.That(_sequence.Mixer.GetInputWeight(0), Is.Zero);
            Assert.That(_sequence.Mixer.GetInputWeight(1), Is.EqualTo(1));
            Assert.That(_sequence.Mixer.GetInput(1).GetTime(), Is.EqualTo(0.5).Within(0.001));
        }

        [Test]
        public void SeekClampsAndHoldsTheFinalSegmentEndpoint()
        {
            _sequence.Seek(99);

            Assert.That(_sequence.ActiveIndex, Is.EqualTo(1));
            Assert.That(_sequence.CurrentTime, Is.EqualTo(3).Within(0.001));
            Assert.That(_sequence.Mixer.GetInput(1).GetTime(), Is.EqualTo(2).Within(0.001));
        }

        private static AnimationClip CreateClip(string name, float duration)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve(
                "Position",
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0, 0, duration, duration));
            return clip;
        }
    }
}
