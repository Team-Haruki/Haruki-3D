using System.Reflection;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class PlanarReflectionComponentTests
    {
        [Test]
        public void SurfaceRegistersStaticAndSkinnedMeshesAndConfiguresMaterials()
        {
            var root = new GameObject("PlanarReflectionRoot");
            var staticObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var skinnedObject = new GameObject("SkinnedSurface");
            var skinnedRenderer = skinnedObject.AddComponent<SkinnedMeshRenderer>();
            var skinnedMesh = new Mesh { name = "SkinnedReflectionSurface" };
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null);
            var staticMaterial = new Material(shader);
            var skinnedMaterial = new Material(shader);
            try
            {
                staticObject.transform.SetParent(root.transform, false);
                skinnedObject.transform.SetParent(root.transform, false);
                staticObject.GetComponent<Renderer>().sharedMaterial = staticMaterial;
                skinnedRenderer.sharedMesh = skinnedMesh;
                skinnedRenderer.sharedMaterial = skinnedMaterial;
                var component = root.AddComponent<PlanarReflection>();

                Invoke(component, "Start");
                Invoke(component, "Update");

                var pass = PlanarReflectionPass.Instance;
                Assert.That(pass.TargetTransform, Is.SameAs(root.transform));
                Assert.That(pass.Meshes, Has.Count.EqualTo(2));
                Assert.That(
                    pass.Meshes,
                    Does.Contain(staticObject.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(pass.Meshes, Does.Contain(skinnedMesh));
                Assert.That(staticMaterial.GetTag("RenderType", false), Is.EqualTo("Transparent"));
                Assert.That(skinnedMaterial.GetTag("RenderType", false), Is.EqualTo("Transparent"));
                Assert.That(staticMaterial.renderQueue, Is.EqualTo(2999));
                Assert.That(skinnedMaterial.renderQueue, Is.EqualTo(2999));
            }
            finally
            {
                PlanarReflectionPass.Instance.TargetTransform = null;
                PlanarReflectionPass.Instance.Meshes = null;
                Object.DestroyImmediate(staticMaterial);
                Object.DestroyImmediate(skinnedMaterial);
                Object.DestroyImmediate(skinnedMesh);
                Object.DestroyImmediate(root);
            }
        }

        private static void Invoke(PlanarReflection component, string methodName)
        {
            var method = typeof(PlanarReflection).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(component, null);
        }
    }
}
