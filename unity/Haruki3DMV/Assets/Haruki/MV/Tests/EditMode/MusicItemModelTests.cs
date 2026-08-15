using NUnit.Framework;
using Sekai.Core;
using Sekai.Rendering;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MusicItemModelTests
    {
        [Test]
        public void SetupAppliesRecoveredHeightAndHeelTargets()
        {
            var root = new GameObject("item");
            var scaleTarget = new GameObject("OffsetValue").transform;
            scaleTarget.SetParent(root.transform, false);
            scaleTarget.localScale = new Vector3(2f, 3f, 4f);
            var positionTarget = new GameObject("PositionOffset").transform;
            positionTarget.SetParent(root.transform, false);
            positionTarget.localPosition = new Vector3(1f, 2f, 3f);
            var model = root.AddComponent<MusicItemModel>();
            try
            {
                model.Setup(1.68f, 0.075f);

                Assert.That(scaleTarget.localScale.x, Is.EqualTo(3.36f).Within(0.0001f));
                Assert.That(scaleTarget.localScale.y, Is.EqualTo(5.04f).Within(0.0001f));
                Assert.That(scaleTarget.localScale.z, Is.EqualTo(6.72f).Within(0.0001f));
                Assert.That(positionTarget.localPosition, Is.EqualTo(new Vector3(1f, 0.075f, 3f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OpacityControlsOfficialVisibilityStatesAndRegistry()
        {
            SekaiMusicItemSettings.ClearTransparentMusicItem();
            var root = new GameObject("item");
            var model = root.AddComponent<MusicItemModel>();
            try
            {
                model.SetOpacity(0.5f);
                Assert.That(model.IsHiding, Is.False);
                Assert.That(model.IsOpaque, Is.False);
                Assert.That(SekaiMusicItemSettings.ExistTransparentMusicItem(), Is.True);

                model.SetOpacity(0f);
                Assert.That(model.IsHiding, Is.True);
                Assert.That(SekaiMusicItemSettings.ExistTransparentMusicItem(), Is.False);

                model.SetOpacity(1f);
                Assert.That(model.IsOpaque, Is.True);
                Assert.That(SekaiMusicItemSettings.ExistTransparentMusicItem(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                SekaiMusicItemSettings.ClearTransparentMusicItem();
            }
        }
    }
}
