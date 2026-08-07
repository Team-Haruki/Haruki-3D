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
        }

        [Test]
        public void CutInsAreOptionalAndUnavailableChildrenDoNotBlockMain()
        {
            var host = new GameObject("MVData");
            var data = host.AddComponent<MusicVideoData>();
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
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MainCharacterCountExcludesInsertSlots()
        {
            var host = new GameObject("MVData");
            var data = host.AddComponent<MusicVideoData>();
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
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void InheritedStageUsesTheConfirmedParentAndChildFields()
        {
            var parentHost = new GameObject("ParentMVData");
            var childHost = new GameObject("ChildMVData");
            var parent = parentHost.AddComponent<MusicVideoData>();
            var child = childHost.AddComponent<MusicVideoData>();
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
                UnityEngine.Object.DestroyImmediate(parentHost);
                UnityEngine.Object.DestroyImmediate(childHost);
            }
        }

        [Test]
        public void InheritedStageRequiresItsParent()
        {
            var childHost = new GameObject("ChildMVData");
            var child = childHost.AddComponent<MusicVideoData>();
            try
            {
                child.id = 101120;
                child.stageInfo = new MusicVideoStageInfo { inheritStage = true };
                Assert.Throws<InvalidOperationException>(() =>
                    MvOfficialRuntimeData.ResolveStage(child));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(childHost);
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
