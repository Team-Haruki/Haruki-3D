using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvPlaybackCoordinatorTests
    {
        private GameObject _host;
        private GameObject _scene;
        private MvPlaybackCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("coordinator");
            _scene = new GameObject("scene");
            _coordinator = _host.AddComponent<MvPlaybackCoordinator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_scene);
        }

        [Test]
        public void BindSceneDisablesAnimatorRootMotion()
        {
            var animator = _scene.AddComponent<Animator>();
            animator.applyRootMotion = true;

            _coordinator.BindScene(_scene, null, 20);

            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(_coordinator.State, Is.EqualTo(MvPlaybackState.Paused));
        }

        [Test]
        public void SeekUsesOneClampedAbsoluteClock()
        {
            _coordinator.BindScene(_scene, null, 20);

            _coordinator.SeekTo(25);

            Assert.That(_coordinator.CurrentTimeSeconds, Is.EqualTo(20));
        }

        [Test]
        public void DurationRetainsTimelineTailForExitPlanning()
        {
            var source = _scene.AddComponent<AudioSource>();
            var clip = AudioClip.Create("mv", 96000, 1, 48000, false);
            try
            {
                source.clip = clip;
                _coordinator.BindScene(_scene, source, 20);

                Assert.That(_coordinator.DurationSeconds, Is.EqualTo(20).Within(0.001));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void AudioClockUsesTheRecoveredWaitingStateMachine()
        {
            var method = typeof(MvPlaybackCoordinator).GetMethod(
                "ResolveAudioSyncedTimeMilliseconds",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            var initial = InvokeAudioClock(method, 500, 50, 0, false);
            Assert.That(initial.time, Is.EqualTo(50));
            Assert.That(initial.waiting, Is.False);
            Assert.That(initial.resetReference, Is.False);

            var ahead = InvokeAudioClock(method, 1100, 1000, 1000, false);
            Assert.That(ahead.time, Is.EqualTo(1062));
            Assert.That(ahead.waiting, Is.True);
            Assert.That(ahead.resetReference, Is.False);

            var stalled = InvokeAudioClock(method, 1062, 1000, 1062, true);
            Assert.That(stalled.time, Is.EqualTo(1062));
            Assert.That(stalled.waiting, Is.True);
            Assert.That(stalled.resetReference, Is.True);

            var caughtUp = InvokeAudioClock(method, 1062, 1063, 1062, true);
            Assert.That(caughtUp.time, Is.EqualTo(1063));
            Assert.That(caughtUp.waiting, Is.False);
            Assert.That(caughtUp.resetReference, Is.True);
        }

        [Test]
        public void NaturalAudioEndStopsAtTheAudioDuration()
        {
            var source = _scene.AddComponent<AudioSource>();
            var clip = AudioClip.Create("mv", 96000, 1, 48000, false);
            try
            {
                source.clip = clip;
                _coordinator.BindScene(_scene, source, 20);
                _coordinator.SetPlaybackPaused(false);
                var method = typeof(MvPlaybackCoordinator).GetMethod(
                    "CompleteAudioPlayback",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(method, Is.Not.Null);
                method.Invoke(_coordinator, null);

                Assert.That(_coordinator.CurrentTimeSeconds, Is.EqualTo(2).Within(0.001));
                Assert.That(_coordinator.State, Is.EqualTo(MvPlaybackState.Completed));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ManualSeekToAudioEndDoesNotReportNaturalCompletion()
        {
            var source = _scene.AddComponent<AudioSource>();
            var clip = AudioClip.Create("mv", 96000, 1, 48000, false);
            try
            {
                var completed = false;
                source.clip = clip;
                _coordinator.PlaybackCompleted += () => completed = true;
                _coordinator.BindScene(_scene, source, 20);

                _coordinator.SeekTo(2);

                Assert.That(_coordinator.CurrentTimeSeconds, Is.EqualTo(2).Within(0.001));
                Assert.That(_coordinator.State, Is.EqualTo(MvPlaybackState.Paused));
                Assert.That(completed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void PlaybackCommandsRequireABoundScene()
        {
            Assert.Throws<System.InvalidOperationException>(() => _coordinator.SetPlaybackPaused(false));
            Assert.Throws<System.InvalidOperationException>(() => _coordinator.SeekTo(1));
        }

        [Test]
        public void CommandsDriveEveryScenePlaybackParticipant()
        {
            var participant = _scene.AddComponent<RecordingPlaybackParticipant>();
            _coordinator.BindScene(_scene, null, 20);

            _coordinator.SetPlaybackPaused(false);
            _coordinator.SeekTo(8);
            _coordinator.DisposeScene();

            Assert.That(participant.LastPause, Is.False);
            Assert.That(participant.LastSeek, Is.EqualTo(8));
            Assert.That(participant.WasDisposed, Is.True);
        }

        [Test]
        public void BindSceneDrivesEveryIndependentSceneRoot()
        {
            var secondRoot = new GameObject("second-root");
            try
            {
                var first = _scene.AddComponent<RecordingPlaybackParticipant>();
                var second = secondRoot.AddComponent<RecordingPlaybackParticipant>();

                _coordinator.BindScene(new[] { _scene, secondRoot }, null, 20);
                _coordinator.SetPlaybackPaused(false);
                _coordinator.SeekTo(7);

                Assert.That(first.LastSeek, Is.EqualTo(7));
                Assert.That(second.LastSeek, Is.EqualTo(7));
                Assert.That(first.LastPause, Is.False);
                Assert.That(second.LastPause, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void ActiveSceneRootSwitchIsOwnedByCoordinator()
        {
            var cutIn = new GameObject("cut-in");
            cutIn.SetActive(false);
            try
            {
                _coordinator.BindScene(new[] { _scene, cutIn }, null, 20);

                _coordinator.SetActiveSceneRoot(cutIn);

                Assert.That(_scene.activeSelf, Is.False);
                Assert.That(cutIn.activeSelf, Is.True);

                _coordinator.SetActiveSceneRoot(_scene);

                Assert.That(_scene.activeSelf, Is.True);
                Assert.That(cutIn.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cutIn);
            }
        }

        [Test]
        public void CutInToCutInSwitchPassesThroughMainRoot()
        {
            var firstCutIn = new GameObject("first-cut-in");
            var secondCutIn = new GameObject("second-cut-in");
            _scene.SetActive(false);
            secondCutIn.SetActive(false);
            try
            {
                var roots = new[] { _scene, firstCutIn, secondCutIn };

                var path = MvPlaybackCoordinator.SceneRootTransitionPath(
                    roots,
                    secondCutIn);

                Assert.That(path, Is.EqualTo(new[] { _scene, secondCutIn }));
            }
            finally
            {
                Object.DestroyImmediate(firstCutIn);
                Object.DestroyImmediate(secondCutIn);
            }
        }

        [Test]
        public void BundleSetDependenciesLoadBeforeRequestedRoots()
        {
            var manifest = new MvBundleSetManifest
            {
                requested = new[] { "timeline" },
                entries = new[]
                {
                    new MvBundleSetEntry { name = "timeline", deps = new[] { "model", "shader" } },
                    new MvBundleSetEntry { name = "model", deps = new[] { "shader" } },
                    new MvBundleSetEntry { name = "shader" },
                    new MvBundleSetEntry { name = "unused" },
                }
            };

            Assert.That(
                MvBundleSetLoader.ResolveLoadOrder(manifest),
                Is.EqualTo(new[] { "shader", "model", "timeline" })
            );
        }

        [Test]
        public void BundleSetRejectsMissingDependencies()
        {
            var manifest = new MvBundleSetManifest
            {
                requested = new[] { "timeline" },
                entries = new[]
                {
                    new MvBundleSetEntry { name = "timeline", deps = new[] { "missing" } },
                }
            };

            Assert.Throws<System.InvalidOperationException>(
                () => MvBundleSetLoader.ResolveLoadOrder(manifest)
            );
        }

        [Test]
        public void BundleSetOwnsTheSingleMainMusicBundle()
        {
            var manifest = new MvBundleSetManifest
            {
                audioBundleName = "music/long/se_0112_01",
                entries = new[]
                {
                    new MvBundleSetEntry { name = "live_pv/mv_data/0112" },
                    new MvBundleSetEntry { name = "music/long/se_0112_01" },
                }
            };

            Assert.That(
                MvBundleSetLoader.ResolveAudioBundleName(manifest),
                Is.EqualTo("music/long/se_0112_01"));
        }

        [Test]
        public void BundleSetRejectsAmbiguousMainMusicBundles()
        {
            var manifest = new MvBundleSetManifest
            {
                entries = new[]
                {
                    new MvBundleSetEntry { name = "music/long/first" },
                    new MvBundleSetEntry { name = "music/long/second" },
                }
            };

            Assert.Throws<System.InvalidOperationException>(
                () => MvBundleSetLoader.ResolveAudioBundleName(manifest));
        }

        private static (long time, bool waiting, bool resetReference) InvokeAudioClock(
            MethodInfo method,
            long predicted,
            long audioMilliseconds,
            long playbackMilliseconds,
            bool waiting)
        {
            var arguments = new object[]
            {
                predicted,
                audioMilliseconds,
                playbackMilliseconds,
                waiting,
                false,
            };
            var time = (long)method.Invoke(null, arguments);
            return (time, (bool)arguments[3], (bool)arguments[4]);
        }

        [Test]
        public void PrefabInstantiationRejectsUnknownBundle()
        {
            var loader = _host.AddComponent<MvBundleSetLoader>();

            Assert.Throws<System.InvalidOperationException>(() => loader.CreatePrefabInstance(
                new MvPrefabLoadRequest { bundleName = "missing", assetName = "stage" }
            ));
        }
    }

    public sealed class RecordingPlaybackParticipant : MonoBehaviour, IMvPlaybackParticipant
    {
        public bool LastPause { get; private set; }
        public double LastSeek { get; private set; }
        public bool WasDisposed { get; private set; }

        public void SetPaused(bool paused) => LastPause = paused;
        public void Seek(double timeSeconds) => LastSeek = timeSeconds;
        public void DisposePlayback() => WasDisposed = true;
    }
}
