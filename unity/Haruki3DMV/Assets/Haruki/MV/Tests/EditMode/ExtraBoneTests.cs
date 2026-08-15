using NUnit.Framework;
using Sekai.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class ExtraBoneTests
    {
        [Test]
        public void LateUpdateDrivesOnlySelectedAxisWithOfficialCoefficientSign()
        {
            var reference = new GameObject("Reference");
            var driven = new GameObject("Driven");
            try
            {
                reference.transform.localRotation = Quaternion.Euler(30f, 20f, 10f);
                var extraBone = driven.AddComponent<ExtraBone>();
                extraBone.referenceBone = reference.transform;
                extraBone.rotationOrder = math.RotationOrder.ZYX;
                extraBone.coefficient = -0.5f;
                extraBone.axisX = true;
                extraBone.axisY = false;
                extraBone.axisZ = false;

                extraBone.SendMessage("Start");
                extraBone.SendMessage("LateUpdate");

                Assert.That(
                    Quaternion.Angle(driven.transform.localRotation, Quaternion.Euler(15f, 0f, 0f)),
                    Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(reference);
                Object.DestroyImmediate(driven);
            }
        }
    }
}
