using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haruki.MV.Tests
{
    public sealed class MvTimelineNodeTests
    {
        private readonly List<TimelineAsset> _timelines = new List<TimelineAsset>();
        private GameObject _root;
        private MvTimelineNode _node;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Background3DPlayer");
            _node = new MvTimelineNode();
        }

        [TearDown]
        public void TearDown()
        {
            _node.Dispose();
            foreach (var timeline in _timelines)
            {
                Object.DestroyImmediate(timeline);
            }
            _timelines.Clear();
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void InitializeCreatesTheSixOfficialDirectorsInOrder()
        {
            _node.Initialize(new Dictionary<string, Object>(), _root.transform);

            Assert.That(MvTimelineNode.TimelineNames, Is.EqualTo(new[]
            {
                "Stage", "Character", "Camera", "Light", "Effect", "Penlight",
            }));
            Assert.That(_node.Directors.Count, Is.EqualTo(6));
            for (var index = 0; index < MvTimelineNode.TimelineNames.Length; index++)
            {
                Assert.That(_node.Directors[index].transform.parent, Is.SameAs(_root.transform));
                Assert.That(_node.Directors[index].name, Is.EqualTo(MvTimelineNode.TimelineNames[index]));
            }
        }

        [Test]
        public void LoadRegistersDefaultsBeforeSpecificNodesOverwriteThem()
        {
            var bindings = new Dictionary<string, Object>();
            _node.Initialize(bindings, _root.transform);
            _node.LoadTimelines(CreateTimeline);

            Assert.That(_node.TimelineDuration, Is.EqualTo(6));
            Assert.That(bindings["StageBinding"], Is.SameAs(_root));
            Assert.That(bindings["CharacterBinding"], Is.SameAs(_root));
            Assert.That(bindings["EffectBinding"], Is.SameAs(_node.Directors[4].gameObject));

            var character = new GameObject("Character0");
            character.AddComponent<Animator>();
            try
            {
                bindings["CharacterBinding"] = character;
                _node.BindTimeline();

                var characterTrack = ((TimelineAsset)_node.Directors[1].playableAsset).GetOutputTrack(0);
                Assert.That(
                    _node.Directors[1].GetGenericBinding(characterTrack),
                    Is.SameAs(character));
                Assert.That(_node.Directors[1].time, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void OnMusicStartAppliesTheSameStartTimeToEveryDirector()
        {
            _node.Initialize(new Dictionary<string, Object>(), _root.transform);
            _node.LoadTimelines(CreateTimeline);

            _node.OnMusicStart(2.5f);

            foreach (var director in _node.Directors)
            {
                Assert.That(director.time, Is.EqualTo(2.5).Within(0.001));
                Assert.That(director.state, Is.EqualTo(PlayState.Playing));
            }
        }

        [Test]
        public void EffectTimelineManagerUsesTheConfirmedMainAndCutInInitialState()
        {
            _node.Initialize(new Dictionary<string, Object>(), _root.transform);
            _node.LoadTimelines(CreateTimeline, isCutIn: true, cutInOrder: 2);

            var manager = _node.LiveEffectTimelineManager;
            Assert.That(manager, Is.Not.Null);
            Assert.That(manager.IsCutIn, Is.True);
            Assert.That(manager.CutInOrder, Is.EqualTo(2));
            Assert.That(manager.IsNowPlaying, Is.False);
            Assert.That(manager.IsSwitchExecutable, Is.False);
            Assert.That(manager.IsNowPlayingOnRetry, Is.False);
            Assert.That(manager.IsSwitchExecutableOnRetry, Is.False);
        }

        [Test]
        public void PauseAndResumeKeepEveryDirectorOnOneAbsoluteTime()
        {
            _node.Initialize(new Dictionary<string, Object>(), _root.transform);
            _node.LoadTimelines(CreateTimeline);
            _node.OnMusicStart(1f);

            _node.OnPause(3.25f);
            foreach (var director in _node.Directors)
            {
                Assert.That(director.state, Is.EqualTo(PlayState.Paused));
                Assert.That(director.time, Is.EqualTo(3.25).Within(0.001));
            }

            _node.OnResume(4.5f);
            foreach (var director in _node.Directors)
            {
                Assert.That(director.state, Is.EqualTo(PlayState.Playing));
                Assert.That(director.time, Is.EqualTo(4.5).Within(0.001));
            }
        }

        [Test]
        public void SeekEvaluatesEveryDirectorAtOneAbsoluteTime()
        {
            _node.Initialize(new Dictionary<string, Object>(), _root.transform);
            _node.LoadTimelines(CreateTimeline);

            _node.OnSeek(3.75f);

            foreach (var director in _node.Directors)
            {
                Assert.That(director.time, Is.EqualTo(3.75).Within(0.001));
            }
        }

        [Test]
        public void PlaybackParticipantConnectsCoordinatorCommandsToTimelineLifecycle()
        {
            _node.Initialize(new Dictionary<string, Object>(), _root.transform);
            _node.LoadTimelines(CreateTimeline);
            var participant = _root.AddComponent<MvTimelinePlaybackParticipant>();
            participant.Initialize(_node);

            participant.Seek(2.25);
            participant.SetPaused(false);
            foreach (var director in _node.Directors)
            {
                Assert.That(director.time, Is.EqualTo(2.25).Within(0.001));
                Assert.That(director.state, Is.EqualTo(PlayState.Playing));
                Assert.That(participant.OwnsDirector(director), Is.True);
            }

            participant.SetPaused(true);
            foreach (var director in _node.Directors)
            {
                Assert.That(director.state, Is.EqualTo(PlayState.Paused));
            }
        }

        [Test]
        public void InactiveCutInDefersPlayUntilItsRootIsActivated()
        {
            _node.Initialize(new Dictionary<string, Object>(), _root.transform);
            _node.LoadTimelines(CreateTimeline);
            var participant = _root.AddComponent<MvTimelinePlaybackParticipant>();
            participant.Initialize(_node);
            _root.SetActive(false);

            participant.Seek(4);
            participant.SetPaused(false);
            _root.SetActive(true);
            participant.ActivateAtCurrentTime();

            foreach (var director in _node.Directors)
            {
                Assert.That(director.time, Is.EqualTo(4).Within(0.001));
                Assert.That(director.state, Is.EqualTo(PlayState.Playing));
            }
        }

        [Test]
        public void TimelineBindingToBodyRootProtectsDescendantAnimatorFromDoubleDrive()
        {
            var bindings = new Dictionary<string, Object>();
            _node.Initialize(bindings, _root.transform);
            _node.LoadTimelines(CreateTimeline);
            var body = new GameObject("Character0");
            var model = new GameObject("Model");
            model.transform.SetParent(body.transform, false);
            var animator = model.AddComponent<Animator>();
            try
            {
                bindings["CharacterBinding"] = body;
                _node.BindTimeline();

                Assert.That(_node.DrivesAnimator(animator), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        private TimelineAsset CreateTimeline(string timelineName)
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = timelineName;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = _timelines.Count + 1;
            timeline.CreateTrack<AnimationTrack>(null, timelineName + "Binding");
            _timelines.Add(timeline);
            return timeline;
        }
    }
}
