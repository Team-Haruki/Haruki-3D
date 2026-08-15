using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sekai.Core.Live;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MeshFlareParaControllerTests
    {
        [Test]
        public void SetupCreatesOfficialMaterialForEveryFlareObject()
        {
            var cameraRoot = new GameObject("camera");
            var camera = cameraRoot.AddComponent<Camera>();
            var root = new GameObject("MeshFlarePara");
            var controller = root.AddComponent<MeshFlareParaController>();
            var flareObjects = new List<GameObject>();
            var textures = new Texture2D[3];
            try
            {
                for (var order = 0; order < 3; order++)
                {
                    var flare = new GameObject($"MeshFlareParaObject{order}");
                    flare.transform.SetParent(root.transform, false);
                    flare.AddComponent<MeshFilter>();
                    flare.AddComponent<MeshRenderer>();
                    flareObjects.Add(flare);
                    textures[order] = new Texture2D(1, 1);
                }
                typeof(MeshFlareParaController)
                    .GetField(
                        "_meshFlareParaObject",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(controller, flareObjects);

                controller.Setup(camera, textures);

                for (var order = 0; order < 3; order++)
                {
                    var material = flareObjects[order]
                        .GetComponent<MeshRenderer>()
                        .sharedMaterial;
                    Assert.That(material, Is.Not.Null);
                    Assert.That(material.shader.name, Is.EqualTo("Sekai/Live/MeshFlarePara"));
                    Assert.That(material.renderQueue, Is.EqualTo(3003 - order));
                    Assert.That(
                        material.GetTexture("_MeshFlareParaMainTex"),
                        Is.SameAs(textures[order]));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraRoot);
                foreach (var texture in textures)
                {
                    if (texture != null) Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
