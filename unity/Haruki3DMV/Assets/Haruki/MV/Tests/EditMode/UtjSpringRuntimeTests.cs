using NUnit.Framework;
using Sekai;
using UnityEngine;
using UTJ;

namespace Haruki.MV.Tests
{
    public sealed class UtjSpringRuntimeTests
    {
        [Test]
        public void ManagerFindsBonesByHierarchyDepthAndPreservesSpringLength()
        {
            var root = new GameObject("Character");
            try
            {
                var first = new GameObject("First");
                first.transform.SetParent(root.transform, false);
                var firstTip = new GameObject("FirstTip");
                firstTip.transform.SetParent(first.transform, false);
                firstTip.transform.localPosition = Vector3.right;
                var firstBone = first.AddComponent<SekaiSpringBone>();
                firstBone.dragForce = 0.5f;

                var nested = new GameObject("Nested");
                nested.transform.SetParent(first.transform, false);
                var nestedTip = new GameObject("NestedTip");
                nestedTip.transform.SetParent(nested.transform, false);
                nestedTip.transform.localPosition = Vector3.right * 0.5f;
                var nestedBone = nested.AddComponent<SekaiSpringBone>();

                var manager = root.AddComponent<SpringManager>();
                manager.Initialize();
                Assert.That(manager.springBones, Is.EqualTo(new SpringBone[] { firstBone, nestedBone }));

                var expectedLength = firstBone.CurrentTipPosition.magnitude;
                firstBone.UpdateSpring(1f / 60f, Vector3.down * 10f);
                Assert.That(
                    Vector3.Distance(first.transform.position, firstBone.CurrentTipPosition),
                    Is.EqualTo(expectedLength).Within(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SphereColliderPushesTailOutsideCombinedRadius()
        {
            var owner = new GameObject("Sphere");
            try
            {
                var collider = owner.AddComponent<SpringSphereCollider>();
                collider.radius = 0.5f;
                var tail = Vector3.right * 0.1f;
                var normal = Vector3.zero;

                var status = collider.CheckForCollisionAndReact(
                    Vector3.zero,
                    ref tail,
                    0.1f,
                    ref normal);

                Assert.That(status, Is.EqualTo(SpringBone.CollisionStatus.HeadIsEmbedded));
                Assert.That(tail.magnitude, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(normal, Is.EqualTo(Vector3.right));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
