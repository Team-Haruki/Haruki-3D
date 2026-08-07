using NUnit.Framework;
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

            _coordinator.Seek(25);

            Assert.That(_coordinator.CurrentTimeSeconds, Is.EqualTo(20));
        }

        [Test]
        public void AudioClipDefinesTheMasterClockDuration()
        {
            var source = _scene.AddComponent<AudioSource>();
            var clip = AudioClip.Create("mv", 96000, 1, 48000, false);
            try
            {
                source.clip = clip;
                _coordinator.BindScene(_scene, source, 20);

                Assert.That(_coordinator.DurationSeconds, Is.EqualTo(2).Within(0.001));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void PlaybackCommandsRequireABoundScene()
        {
            Assert.Throws<System.InvalidOperationException>(() => _coordinator.SetPaused(false));
            Assert.Throws<System.InvalidOperationException>(() => _coordinator.Seek(1));
        }

        [Test]
        public void CommandsDriveEveryScenePlaybackParticipant()
        {
            var participant = _scene.AddComponent<RecordingPlaybackParticipant>();
            _coordinator.BindScene(_scene, null, 20);

            _coordinator.SetPaused(false);
            _coordinator.Seek(8);
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
                _coordinator.SetPaused(false);
                _coordinator.Seek(7);

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
        public void PrefabInstantiationRejectsUnknownBundle()
        {
            var loader = _host.AddComponent<MvBundleSetLoader>();

            Assert.Throws<System.InvalidOperationException>(() => loader.InstantiatePrefab(
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
