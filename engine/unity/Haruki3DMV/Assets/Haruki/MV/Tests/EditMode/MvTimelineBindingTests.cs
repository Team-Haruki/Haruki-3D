using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haruki.MV.Tests
{
    public sealed class MvTimelineBindingTests
    {
        private GameObject _directorObject;
        private GameObject _bindingObject;
        private PlayableDirector _director;
        private TimelineAsset _timeline;

        [SetUp]
        public void SetUp()
        {
            _directorObject = new GameObject("director");
            _bindingObject = new GameObject("binding");
            _bindingObject.AddComponent<Animator>();
            _director = _directorObject.AddComponent<PlayableDirector>();
            _timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_timeline);
            Object.DestroyImmediate(_bindingObject);
            Object.DestroyImmediate(_directorObject);
        }

        [Test]
        public void BindTimelineUsesEveryOutputStreamNameAndEvaluatesAtZero()
        {
            var first = _timeline.CreateTrack<AnimationTrack>(null, "MainCamera");
            var second = _timeline.CreateTrack<AnimationTrack>(null, "MainCamera");
            // CreateTrack uniquifies editor-created names. Recovered SEKAI timelines
            // serialize several outputs with the exact same MainCamera stream name.
            second.name = "MainCamera";

            MvTimelineBinding.BindTimeline(
                _director,
                _timeline,
                new Dictionary<string, Object> { ["MainCamera"] = _bindingObject });

            Assert.That(_director.playableAsset, Is.SameAs(_timeline));
            Assert.That(_director.GetGenericBinding(first), Is.SameAs(_bindingObject));
            Assert.That(_director.GetGenericBinding(second), Is.SameAs(_bindingObject));
            Assert.That(_director.time, Is.Zero);
        }

        [Test]
        public void BindTimelineRejectsAnUndeclaredOfficialStream()
        {
            _timeline.CreateTrack<AnimationTrack>(null, "MainCamera");

            Assert.Throws<KeyNotFoundException>(() => MvTimelineBinding.BindTimeline(
                _director,
                _timeline,
                new Dictionary<string, Object>()));
        }
    }
}
