using NUnit.Framework;
using Sekai.Core.Live;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvCutInControllerTests
    {
        [Test]
        public void ComputesTheFourOfficialTransitionWindows()
        {
            var host = new GameObject("CutIn");
            var controller = host.AddComponent<MvCutInController>();
            var clip = ScriptableObject.CreateInstance<CutInClip>();
            clip.entryTransitionColor = Color.white;
            clip.entryTransitionInDuration = 1f;
            clip.entryTransitionOutDuration = 1f;
            clip.exitTransitionColor = Color.red;
            clip.exitTransitionInDuration = 1f;
            clip.exitTransitionOutDuration = 1f;

            try
            {
                controller.UpdateTransition(clip, 0.5f, 10f);
                Assert.That(controller.TransitionColor, Is.EqualTo(Color.white));
                Assert.That(controller.TransitionWeight, Is.EqualTo(0.5f));

                controller.UpdateTransition(clip, 1.5f, 10f);
                Assert.That(controller.TransitionWeight, Is.EqualTo(0.5f));

                controller.UpdateTransition(clip, 8.5f, 10f);
                Assert.That(controller.TransitionColor, Is.EqualTo(Color.red));
                Assert.That(controller.TransitionWeight, Is.EqualTo(0.5f));

                controller.UpdateTransition(clip, 9.5f, 10f);
                Assert.That(controller.TransitionWeight, Is.EqualTo(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ResolvesOfficialTwoSecondOffscreenSimulationWindows()
        {
            var clip = ScriptableObject.CreateInstance<CutInClip>();
            clip.cutinIndex = 0;
            clip.Setup(91.75, 12.066666666666663);

            try
            {
                var idle = MvCutInController.ResolveFrame(89.74, new[] { clip });
                Assert.That(idle.ActiveCutInOrder, Is.EqualTo(-1));
                Assert.That(idle.OffscreenCutInOrder, Is.EqualTo(-1));
                Assert.That(idle.OffscreenMain, Is.False);

                var childPrewarm = MvCutInController.ResolveFrame(89.75, new[] { clip });
                Assert.That(childPrewarm.ActiveCutInOrder, Is.EqualTo(-1));
                Assert.That(childPrewarm.OffscreenCutInOrder, Is.EqualTo(0));
                Assert.That(childPrewarm.OffscreenMain, Is.False);

                var active = MvCutInController.ResolveFrame(91.75, new[] { clip });
                Assert.That(active.ActiveCutInOrder, Is.EqualTo(0));
                Assert.That(active.OffscreenCutInOrder, Is.EqualTo(-1));
                Assert.That(active.OffscreenMain, Is.False);

                var mainPrewarm = MvCutInController.ResolveFrame(
                    clip.End - MvCutInController.OffScreenSimulateDuration,
                    new[] { clip });
                Assert.That(mainPrewarm.ActiveCutInOrder, Is.EqualTo(0));
                Assert.That(mainPrewarm.OffscreenCutInOrder, Is.EqualTo(-1));
                Assert.That(mainPrewarm.OffscreenMain, Is.True);

                var ended = MvCutInController.ResolveFrame(clip.End, new[] { clip });
                Assert.That(ended.ActiveCutInOrder, Is.EqualTo(-1));
                Assert.That(ended.OffscreenCutInOrder, Is.EqualTo(-1));
                Assert.That(ended.OffscreenMain, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
