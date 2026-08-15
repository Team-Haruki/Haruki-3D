using System.Collections.Generic;
using NUnit.Framework;
using Sekai.Core;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvLightNodeTests
    {
        [Test]
        public void CreatesAllOfficialLightCategoriesAndBindsCharacterTracks()
        {
            var root = new GameObject("MV");
            var bindings = new Dictionary<string, Object>
            {
                ["GlobalSettings"] = root,
                ["AmbientLight"] = root,
                ["DirectionalLight"] = root,
                ["Character0AmbientLight"] = root,
                ["Character0RimLight"] = root,
                ["Character1AmbientLight"] = root,
                ["Character1RimLight"] = root,
            };
            var data = ScriptableObject.CreateInstance<MusicVideoData>();
            data.characterInfos = new[]
            {
                new MusicVideoCharacterInfo(),
                new MusicVideoCharacterInfo(),
            };

            var node = new MvLightNode(bindings, root.transform);
            try
            {
                node.Load(data);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        MvLightCategory.GlobalSettings,
                        MvLightCategory.AmbientLight,
                        MvLightCategory.DirectionalLight,
                        MvLightCategory.SpotLight,
                        MvLightCategory.CharacterRimLight,
                        MvLightCategory.CharacterAmbientLight,
                        MvLightCategory.ShadowLight,
                    },
                    MvLightNode.OfficialCategories);
                Assert.That(node.CharacterAmbientLights, Has.Count.EqualTo(2));
                Assert.That(node.CharacterRimLights, Has.Count.EqualTo(2));
                Assert.That(bindings["GlobalSettings"], Is.SameAs(node.GlobalSettings));
                Assert.That(bindings["AmbientLight"], Is.SameAs(node.AmbientLight));
                Assert.That(bindings["DirectionalLight"], Is.SameAs(node.DirectionalLight));
                Assert.That(
                    bindings["Character1AmbientLight"],
                    Is.SameAs(node.CharacterAmbientLights[1]));
                Assert.That(
                    bindings["Character1RimLight"],
                    Is.SameAs(node.CharacterRimLights[1]));
                Assert.That(node.SpotLight, Is.Not.Null);
                Assert.That(node.ShadowLight, Is.Not.Null);
                Assert.That(
                    node.GlobalSettings.GetComponent<SekaiGlobalSettings>(),
                    Is.Not.Null);
                Assert.That(
                    node.AmbientLight.GetComponent<Sekai.SekaiAmbientLight>(),
                    Is.Not.Null);
                Assert.That(
                    node.DirectionalLight.GetComponent<Sekai.SekaiDirectionalLight>(),
                    Is.Not.Null);
                Assert.That(
                    node.CharacterAmbientLights[1]
                        .GetComponent<SekaiCharacterAmbientLight>().FormationId,
                    Is.EqualTo(1));
                Assert.That(
                    node.CharacterRimLights[1]
                        .GetComponent<SekaiCharacterRimLight>().FormationId,
                    Is.EqualTo(1));
            }
            finally
            {
                node.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void RimFactorUsesRecoveredOfficialPackingOrder()
        {
            var host = new GameObject("Rim");
            try
            {
                var rim = host.AddComponent<SekaiCharacterRimLight>();
                rim.Range = 7f;
                rim.Emission = 2f;
                rim.EdgeSmoothness = 0.125f;
                rim.LightInfluence = 0.5f;

                var factor = rim.PackFactor();

                Assert.That(factor.x, Is.EqualTo(7f));
                Assert.That(factor.y, Is.EqualTo(2f));
                Assert.That(factor.z, Is.EqualTo(0.125f));
                Assert.That(
                    factor.w,
                    Is.EqualTo(Mathf.GammaToLinearSpace(0.5f)).Within(0.000001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
