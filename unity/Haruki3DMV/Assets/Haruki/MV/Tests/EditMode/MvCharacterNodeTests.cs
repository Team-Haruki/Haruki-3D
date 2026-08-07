using NUnit.Framework;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvCharacterNodeTests
    {
        [Test]
        public void AttachSkinnedPartMergesMissingBonesAndRemapsRenderer()
        {
            var body = CreateSkeleton("body", out _, out var bodyHead);
            var part = CreateSkeleton("face", out _, out var partHead);
            var hair = new GameObject("Hair");
            hair.transform.SetParent(partHead, false);
            var renderer = hair.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = new Mesh();
            renderer.rootBone = partHead;
            renderer.bones = new[] { partHead, hair.transform };

            try
            {
                MvCharacterNode.AttachSkinnedPart(body, part);

                var mergedHair = bodyHead.Find("Hair");
                Assert.That(mergedHair, Is.SameAs(hair.transform));
                Assert.That(renderer.rootBone, Is.SameAs(bodyHead));
                Assert.That(renderer.bones, Is.EqualTo(new[] { bodyHead, hair.transform }));
            }
            finally
            {
                Object.DestroyImmediate(renderer.sharedMesh);
                Object.DestroyImmediate(body);
                if (part != null)
                {
                    Object.DestroyImmediate(part);
                }
            }
        }

        private static GameObject CreateSkeleton(
            string name,
            out Transform position,
            out Transform head)
        {
            var root = new GameObject(name);
            position = new GameObject("Position").transform;
            position.SetParent(root.transform, false);
            var hip = new GameObject("Hip").transform;
            hip.SetParent(position, false);
            head = new GameObject("Head").transform;
            head.SetParent(hip, false);
            return root;
        }
    }
}
