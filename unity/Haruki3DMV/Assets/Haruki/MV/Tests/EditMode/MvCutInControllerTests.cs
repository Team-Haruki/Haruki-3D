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
    }
}
