using NUnit.Framework;
using Sekai.Core;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvCameraAdjustmentTests
    {
        [Test]
        public void ApplyUsesBothTargetsAndPreservesHorizontalPosition()
        {
            var host = new GameObject("CameraAdjustment");
            try
            {
                host.transform.localPosition = new Vector3(3, 99, 4);
                var adjustment = host.AddComponent<MvCameraAdjustment>();
                adjustment.SetCharacterHeight(MvOfficialRuntimeData.CreateCameraHeightData(
                    new[] { 1.5f, 1.7f },
                    new[] { 0.01f, 0.02f },
                    new[]
                    {
                        new MusicVideoCharacterInfo { defaultHeelOffset = 0.03f },
                        new MusicVideoCharacterInfo { defaultHeelOffset = 0.04f },
                    }));
                adjustment.Target = 0;
                adjustment.SecondTarget = 1;
                adjustment.TargetLerp = 0.25f;
                adjustment.SelectedDefaultHeight = 1.6f;

                adjustment.Apply();

                var first = MvOfficialRuntimeData.CameraHeightOffset(1.5f, 0.01f, 1.6f, 0.03f);
                var second = MvOfficialRuntimeData.CameraHeightOffset(1.7f, 0.02f, 1.6f, 0.04f);
                Assert.That(host.transform.localPosition.x, Is.EqualTo(3));
                Assert.That(host.transform.localPosition.z, Is.EqualTo(4));
                Assert.That(
                    host.transform.localPosition.y,
                    Is.EqualTo(Mathf.Lerp(first, second, 0.25f)).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
