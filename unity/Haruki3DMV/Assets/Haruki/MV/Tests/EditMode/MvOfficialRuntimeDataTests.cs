using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sekai.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Haruki.MV.Tests
{
    public sealed class MvOfficialRuntimeDataTests
    {
        [Test]
        public void AssetNamesPreserveOfficialMainAndCutInFormatting()
        {
            Assert.That(
                MvOfficialRuntimeData.MusicVideoDataBundleName(112),
                Is.EqualTo("live_pv/mv_data/112"));
            Assert.That(
                MvOfficialRuntimeData.MusicVideoDataBundleName(112, true),
                Is.EqualTo("live_pv/mv_data/000112"));
            Assert.That(
                MvOfficialRuntimeData.TimelineBundleName(101120, "Character", true),
                Is.EqualTo("live_pv/timeline/101120/character"));
            Assert.That(
                MvOfficialRuntimeData.TimelineBundleName(112, "Character"),
                Is.EqualTo("live_pv/timeline/112/character"));
            Assert.That(
                MvOfficialRuntimeData.CharacterFaceBundleName("05/9001"),
                Is.EqualTo("live_pv/model/characterv2/face/05/9001"));
            Assert.That(
                MvOfficialRuntimeData.CharacterBodyBundlePrefix("05/9001"),
                Is.EqualTo("live_pv/model/characterv2/body/05/9001/"));
            Assert.That(
                MvOfficialRuntimeData.CharacterHeadOptionalBundleName(
                    "0112/a03",
                    MvCharacterModelVersion.V1),
                Is.EqualTo("live_pv/model/character/head_optional/0112/a03"));
            Assert.That(
                MvOfficialRuntimeData.CharacterBodyColorBundleName("05/9001/02"),
                Is.EqualTo(
                    "live_pv/model/characterv2/color_variation/body/05/9001/02"));
            Assert.That(
                MvOfficialRuntimeData.CharacterHeadOptionalColorBundleName("0112/a03/02"),
                Is.EqualTo(
                    "live_pv/model/characterv2/color_variation/head_optional/0112/a03/02"));
        }

        [Test]
        public void CharacterPartsPreferV2AndFallBackToV1Independently()
        {
            var v2Face = "live_pv/model/characterv2/face/05/9001";
            var v1Face = "live_pv/model/character/face/05/9001";
            Assert.That(
                MvOfficialRuntimeData.ResolveCharacterFaceBundleName(
                    "05/9001",
                    name => name == v2Face),
                Is.EqualTo(v2Face));
            Assert.That(
                MvOfficialRuntimeData.ResolveCharacterFaceBundleName(
                    "05/9001",
                    name => name == v1Face),
                Is.EqualTo(v1Face));

            Assert.That(
                MvOfficialRuntimeData.ResolveCharacterBodyBundleName(
                    "05/9001",
                    prefix => prefix.Contains("characterv2")
                        ? prefix + "ladies_m"
                        : null),
                Is.EqualTo(
                    "live_pv/model/characterv2/body/05/9001/ladies_m"));
            Assert.That(
                MvOfficialRuntimeData.ResolveCharacterBodyBundleName(
                    "05/9001",
                    prefix => prefix.Contains("characterv2")
                        ? null
                        : prefix + "ladies_m"),
                Is.EqualTo(
                    "live_pv/model/character/body/05/9001/ladies_m"));

            var v1Head = "live_pv/model/character/head_optional/0112/a03";
            Assert.That(
                MvOfficialRuntimeData.ResolveCharacterHeadOptionalBundleName(
                    "0112/a03",
                    name => name == v1Head),
                Is.EqualTo(v1Head));

            var v1BodyColor =
                "live_pv/model/character/color_variation/body/05/9001/02";
            Assert.That(
                MvOfficialRuntimeData.ResolveCharacterBodyColorBundleName(
                    "05/9001/02",
                    name => name == v1BodyColor),
                Is.EqualTo(v1BodyColor));
            var v2HeadColor =
                "live_pv/model/characterv2/color_variation/head_optional/0112/a03/02";
            Assert.That(
                MvOfficialRuntimeData.ResolveCharacterHeadOptionalColorBundleName(
                    "0112/a03/02",
                    name => name == v2HeadColor),
                Is.EqualTo(v2HeadColor));
        }

        [Test]
        public void CatalogPathsAcceptTheRecoveredFourDigitMainLayout()
        {
            Assert.That(
                MvOfficialRuntimeData.ResolveMusicVideoDataBundleName(
                    112,
                    name => name == "live_pv/mv_data/0112"),
                Is.EqualTo("live_pv/mv_data/0112"));
            Assert.That(
                MvOfficialRuntimeData.StageBundleName(5),
                Is.EqualTo("live_pv/model/stage/0005"));
            Assert.That(
                MvOfficialRuntimeData.StageDecorationBundleName(101120),
                Is.EqualTo("live_pv/model/stage_decoration/101120"));
            Assert.That(
                MvOfficialRuntimeData.PenlightBundleName(112),
                Is.EqualTo("live_pv/model/penlight/0112"));
            Assert.That(
                MvOfficialRuntimeData.CameraDecorationBundleName(112),
                Is.EqualTo("live_pv/model/camera_decoration/0112"));
            Assert.That(
                MvOfficialRuntimeData.MeshFlareTextureBundleName(112),
                Is.EqualTo("live_pv/model/mesh_flare_para/textures/0112"));
            Assert.That(
                MvOfficialRuntimeData.MusicItemBundleName(42),
                Is.EqualTo("live_pv/model/music_item/0042"));
        }

        [Test]
        public void CharacterHeightUsesMasterCentimetersAtTheFixedMvRate()
        {
            Assert.That(
                MvOfficialRuntimeData.CharacterHeightMeters(168),
                Is.EqualTo(1.68f).Within(0.0001));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MvOfficialRuntimeData.CharacterHeightMeters(0));
        }

        [Test]
        public void CutInsAreOptionalAndUnavailableChildrenDoNotBlockMain()
        {
            var data = ScriptableObject.CreateInstance<MusicVideoData>();
            data.cutinInfo = new MusicVideoCutinInfo
            {
                ChildIds = new[] { 101120, 101121 },
            };
            try
            {
                Assert.That(
                    MvOfficialRuntimeData.OptionalCutInIds(data, false, _ => true),
                    Is.Empty);
                Assert.That(
                    MvOfficialRuntimeData.OptionalCutInIds(data, true, id => id == 101120),
                    Is.EqualTo(new[] { 101120 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void NormalCutInReusesTheMatchingFinalMainMember()
        {
            var main = ScriptableObject.CreateInstance<MusicVideoData>();
            var child = ScriptableObject.CreateInstance<MusicVideoData>();
            var first = new MvCharacterLoadSpec { characterId = 1, characterHeight = 152 };
            var fifth = new MvCharacterLoadSpec { characterId = 23, characterHeight = 158 };
            try
            {
                main.characterInfos = new[]
                {
                    new MusicVideoCharacterInfo { id = 1 },
                    new MusicVideoCharacterInfo { id = 5 },
                };
                child.id = 101120;
                child.characterInfos = new[]
                {
                    new MusicVideoCharacterInfo { id = 5 },
                };

                var result = MvOfficialRuntimeData.ResolveNormalCutInCharacters(
                    main,
                    child,
                    new[] { first, fifth });

                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0], Is.SameAs(fifth));

                Assert.Throws<InvalidOperationException>(() =>
                    MvOfficialRuntimeData.ResolveCutInCharacters(
                        main,
                        child,
                        new[] { first, fifth },
                        false,
                        Array.Empty<MvCharacterLoadSpec>()));
                Assert.Throws<InvalidOperationException>(() =>
                    MvOfficialRuntimeData.ResolveCutInCharacters(
                        main,
                        child,
                        new[] { first, fifth },
                        true,
                        new[] { fifth }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(main);
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        [Test]
        public void MainCharacterCountExcludesInsertSlots()
        {
            var data = ScriptableObject.CreateInstance<MusicVideoData>();
            try
            {
                data.characterInfos = new[]
                {
                    new MusicVideoCharacterInfo(),
                    new MusicVideoCharacterInfo { isInsertCharacter = true },
                    new MusicVideoCharacterInfo(),
                };
                Assert.That(MvOfficialRuntimeData.MainCharacterCount(data), Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void InheritedStageUsesTheConfirmedParentAndChildFields()
        {
            var parent = ScriptableObject.CreateInstance<MusicVideoData>();
            var child = ScriptableObject.CreateInstance<MusicVideoData>();
            try
            {
                parent.id = 112;
                parent.stageInfo = new MusicVideoStageInfo
                {
                    id = 5,
                    overrideTexture = true,
                    stageDecorationInfos = new[] { new MusicVideoStageDecorationInfo { id = 112 } },
                    enableLensFlare = true,
                    enableWaterCaustics = true,
                    enableHeightFog = true,
                    enableEffectDistortion = true,
                };
                child.id = 101120;
                child.stageInfo = new MusicVideoStageInfo
                {
                    id = 99,
                    inheritStage = true,
                    penlightInfo = new MusicVideoPenlightInfo { id = 1000 },
                    stageDecorationInfos = new[] { new MusicVideoStageDecorationInfo { id = 101120 } },
                    enablePlanarReflection = true,
                    enablePlanarReflectionSorting = true,
                    skipBaseStageLoad = true,
                };

                var resolved = MvOfficialRuntimeData.ResolveStage(child, parent);

                Assert.That(resolved.Id, Is.EqualTo(5));
                Assert.That(resolved.OverrideTexture, Is.True);
                Assert.That(resolved.PenlightInfo.id, Is.EqualTo(1000));
                Assert.That(resolved.StageDecorationInfos[0].id, Is.EqualTo(101120));
                Assert.That(resolved.EnableLensFlare, Is.True);
                Assert.That(resolved.EnableWaterCaustics, Is.True);
                Assert.That(resolved.EnableHeightFog, Is.True);
                Assert.That(resolved.EnablePlanarReflection, Is.True);
                Assert.That(resolved.EnablePlanarReflectionSorting, Is.True);
                Assert.That(resolved.EnableEffectDistortion, Is.True);
                Assert.That(resolved.SkipBaseStageLoad, Is.True);
                Assert.That(resolved.AdditionalOverrideTextureMusicVideoIds, Is.EqualTo(new[] { 112 }));
                Assert.That(resolved.OverrideAdditionalStageTexture, Is.True);
                Assert.That(resolved.AdditionalStageDecorationInfos[0].id, Is.EqualTo(112));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        [Test]
        public void InheritedStageRequiresItsParent()
        {
            var child = ScriptableObject.CreateInstance<MusicVideoData>();
            try
            {
                child.id = 101120;
                child.stageInfo = new MusicVideoStageInfo { inheritStage = true };
                Assert.Throws<InvalidOperationException>(() =>
                    MvOfficialRuntimeData.ResolveStage(child));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        [Test]
        public void CameraHeightDataKeepsAllThreeOfficialArraysSeparate()
        {
            var data = MvOfficialRuntimeData.CreateCameraHeightData(
                new[] { 1.58f, 1.68f },
                new[] { 0.02f, 0.04f },
                new[]
                {
                    new MusicVideoCharacterInfo { defaultHeelOffset = 0.01f },
                    new MusicVideoCharacterInfo { defaultHeelOffset = 0.03f },
                });

            Assert.That(data.Heights, Is.EqualTo(new[] { 1.58f, 1.68f }));
            Assert.That(data.HeelOffsets, Is.EqualTo(new[] { 0.02f, 0.04f }));
            Assert.That(data.DefaultHeelOffsets, Is.EqualTo(new[] { 0.01f, 0.03f }));
        }

        [Test]
        public void CameraHeightOffsetUsesActualAndMvDefaultHeelOffsets()
        {
            var first = MvOfficialRuntimeData.CameraHeightOffset(1.58f, 0.02f, 1.6f, 0.01f);
            var second = MvOfficialRuntimeData.CameraHeightOffset(1.68f, 0.04f, 1.6f, 0.03f);

            Assert.That(first, Is.EqualTo(1.58f * 0.903f - 1.6f * 0.893f).Within(0.0001));
            Assert.That(second, Is.EqualTo(1.68f * 0.923f - 1.6f * 0.913f).Within(0.0001));
            Assert.That(
                MvOfficialRuntimeData.BlendedCameraHeightOffset(first, second, 0.25f),
                Is.EqualTo(first + (second - first) * 0.25f).Within(0.0001));
            Assert.That(
                MvOfficialRuntimeData.BlendedCameraHeightOffset(first, second, 2f),
                Is.EqualTo(second).Within(0.0001));
        }

        [Test]
        public void MusicVideoLightCategoriesMatchTheConfirmedSevenItemArray()
        {
            Assert.That(MvOfficialRuntimeData.MusicVideoLightCategories, Is.EqualTo(new[]
            {
                MvLightCategory.GlobalSettings,
                MvLightCategory.AmbientLight,
                MvLightCategory.DirectionalLight,
                MvLightCategory.SpotLight,
                MvLightCategory.CharacterRimLight,
                MvLightCategory.CharacterAmbientLight,
                MvLightCategory.ShadowLight,
            }));
        }

        [Test]
        public void MissingTimelineFallsBackToTheOfficialDefaultMv()
        {
            Assert.That(
                MvOfficialRuntimeData.ResolveTimelineBundleName(
                    112,
                    "Camera",
                    name => name == "live_pv/timeline/0001/camera"),
                Is.EqualTo("live_pv/timeline/0001/camera"));
            Assert.That(
                MvOfficialRuntimeData.ResolveTimelineBundleName(
                    112,
                    "Camera",
                    name => name == "live_pv/timeline/0112/camera"),
                Is.EqualTo("live_pv/timeline/0112/camera"));
            Assert.That(
                MvOfficialRuntimeData.ResolveTimelineBundleName(
                    112,
                    "Camera",
                    name => name == "live_pv/timeline/112/camera"),
                Is.EqualTo("live_pv/timeline/112/camera"));
        }

        [Test]
        public void StageOverrideTexturesKeepCurrentMvBeforeAdditionalFallbacks()
        {
            var merged = MvOfficialRuntimeData.MergeStageOverrideTextures(
                new Dictionary<string, string>
                {
                    ["shared"] = "current",
                    ["current-only"] = "current",
                },
                new IReadOnlyDictionary<string, string>[]
                {
                    new Dictionary<string, string>
                    {
                        ["shared"] = "additional-1",
                        ["additional-only"] = "additional-1",
                    },
                    new Dictionary<string, string>
                    {
                        ["additional-only"] = "additional-2",
                        ["last-only"] = "additional-2",
                    },
                });

            Assert.That(merged["shared"], Is.EqualTo("current"));
            Assert.That(merged["additional-only"], Is.EqualTo("additional-1"));
            Assert.That(merged["last-only"], Is.EqualTo("additional-2"));
        }

        [Test]
        public void StageOverrideTextureSlotsMatchTheRecoveredFourProperties()
        {
            Assert.That(MvStageNode.OverrideTextureProperties, Is.EqualTo(new[]
            {
                "_MainTex",
                "_ColorTex",
                "_LightMapTex",
                "_SubTex",
            }));
        }

        [Test]
        public void StageOverrideKeepsTheOriginalTexture()
        {
            var shader = Shader.Find("Unlit/Texture");
            Assert.That(shader, Is.Not.Null);
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(shader);
            var original = new Texture2D(1, 1) { name = "stage-texture" };
            var replacement = new Texture2D(1, 1) { name = "replacement" };
            var originals = new Dictionary<string, Texture2D>();
            try
            {
                material.SetTexture("_MainTex", original);
                root.GetComponent<Renderer>().sharedMaterial = material;

                MvStageNode.ApplyKnownTextureOverrides(
                    root,
                    new Dictionary<string, Texture2D> { [original.name] = replacement },
                    false,
                    originals);

                Assert.That(material.GetTexture("_MainTex"), Is.SameAs(replacement));
                Assert.That(originals[original.name], Is.SameAs(original));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(original);
                UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void PlayerRenderSettingsMatchBackground3DPlayerOnLoad()
        {
            var root = new GameObject("Background3DPlayer");
            var child = new GameObject("Character");
            child.transform.SetParent(root.transform, false);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = new Mesh();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            renderer.skinnedMotionVectors = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

            try
            {
                MvPlayerRenderSettings.Apply(root);

                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(renderer.receiveShadows, Is.False);
                Assert.That(renderer.motionVectorGenerationMode, Is.EqualTo(MotionVectorGenerationMode.ForceNoMotion));
                Assert.That(renderer.skinnedMotionVectors, Is.False);
                Assert.That(renderer.lightProbeUsage, Is.EqualTo(LightProbeUsage.Off));
                Assert.That(renderer.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.Off));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(renderer.sharedMesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
