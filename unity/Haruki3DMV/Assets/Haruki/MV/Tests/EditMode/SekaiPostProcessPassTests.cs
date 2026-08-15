using NUnit.Framework;
using Sekai.Rendering;
using Sekai.Rendering.PostPrcessV2;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV.Tests
{
    public sealed class SekaiPostProcessPassTests
    {
        [Test]
        public void SetupUsesTheCapturedEventAndAspectDerivedWorkSurface()
        {
            var pass = new SekaiPostProcessPass();
            var descriptor = Descriptor(3200, 2136);

            pass.Setup(
                RenderPassEvent.BeforeRenderingPostProcessing,
                new Sekai.Rendering.SekaiBuffer(),
                descriptor,
                null,
                null);

            Assert.That(
                pass.renderPassEvent,
                Is.EqualTo(RenderPassEvent.BeforeRenderingPostProcessing));
            Assert.That(pass.PostSize, Is.EqualTo(new Vector2Int(384, 256)));
            Assert.That(pass.Descriptor.width, Is.EqualTo(3200));
            Assert.That(pass.Descriptor.height, Is.EqualTo(2136));
        }

        [TestCase(1920, 1080, 455)]
        [TestCase(2560, 1440, 455)]
        [TestCase(3840, 2160, 455)]
        public void StandardVideoOutputsKeepTheOfficial256HighPostSurface(
            int width,
            int height,
            int expectedPostWidth)
        {
            var size = SekaiPostProcessPass.CalculatePostSize(
                Descriptor(width, height));

            Assert.That(size, Is.EqualTo(new Vector2Int(expectedPostWidth, 256)));
        }

        [Test]
        public void ExecutionOrderMatchesTheRecoveredProfileMarkers()
        {
            Assert.That(
                SekaiPostProcessPass.ExecutionOrder,
                Is.EqualTo(new[]
                {
                    ProfileId.Blur,
                    ProfileId.Dof,
                    ProfileId.Bloom,
                    ProfileId.SaturationBlur,
                    ProfileId.ScreenDistortion,
                    ProfileId.Uber,
                    ProfileId.SMAA,
                }));
        }

        [Test]
        public void EffectVectorsMatchTheRecoveredCpuMappings()
        {
            Assert.That(
                SekaiPostProcessPass.CalculateLightVector(
                    new Vector2(3f, 4f),
                    Vector2.zero),
                Is.EqualTo(new Vector4(3f, 4f, 5f, 1f / 25f)));
            Assert.That(
                SekaiPostProcessPass.CalculateIncidentLightVector(
                    new Vector2(0.25f, 0.75f),
                    2f,
                    1),
                Is.EqualTo(new Vector4(0.25f, 0.75f, 0.25f, 1f)));
            Assert.That(
                SekaiPostProcessPass.CalculateLutVector(
                    new Vector2(4f, 6f),
                    new Vector2(1f, 2f)),
                Is.EqualTo(new Vector4(3f, 4f, 5f, 1f / 25f)));
        }

        [Test]
        public void ZeroLengthEffectVectorsDoNotProduceInfinity()
        {
            Assert.That(
                SekaiPostProcessPass.CalculateLightVector(
                    Vector2.one,
                    Vector2.one),
                Is.EqualTo(Vector4.zero));
            Assert.That(
                SekaiPostProcessPass.CalculateIncidentLightVector(
                    Vector2.one,
                    0f,
                    0),
                Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
        }

        [TestCase(16, 0.03125f, 0.9375f)]
        [TestCase(32, 0.015625f, 0.96875f)]
        public void LutSamplingMatchesTheRecoveredTextureWidthFormula(
            int width,
            float expectedHalfColumn,
            float expectedThreshold)
        {
            Assert.That(
                SekaiPostProcessPass.CalculateLutSampling(width),
                Is.EqualTo(new Vector2(expectedHalfColumn, expectedThreshold)));
        }

        [TestCase(0f, -1f, 0f)]
        [TestCase(90f, 0f, 1f)]
        [TestCase(180f, 1f, 0f)]
        [TestCase(270f, 0f, -1f)]
        public void DirectionalBlurUsesTheRecoveredOneEightyDegreeBasis(
            float degrees,
            float expectedX,
            float expectedY)
        {
            var direction =
                SekaiPostProcessPass.CalculateDirectionalBlurVector(degrees);

            Assert.That(direction.x, Is.EqualTo(expectedX).Within(0.000001f));
            Assert.That(direction.y, Is.EqualTo(expectedY).Within(0.000001f));
            Assert.That(direction.z, Is.Zero);
            Assert.That(direction.w, Is.Zero);
        }

        [Test]
        public void ScreenDistortionParametersMatchTheRecoveredGlobalVectors()
        {
            Assert.That(
                SekaiPostProcessPass.CalculateScreenDistortionParameters(
                    0.25f,
                    3f,
                    0.4f,
                    true),
                Is.EqualTo(new Vector4(0.25f, 3f, 0.4f, 1f)));
            Assert.That(
                SekaiPostProcessPass.CalculateScreenDistortionNoiseParameters(
                    new Vector2(2f, 3f),
                    new Vector2(0.5f, -0.25f),
                    4f),
                Is.EqualTo(new Vector4(2f, 3f, 2f, -1f)));
        }

        [Test]
        public void MaterialLibraryUsesOnlyCapturedOfficialShaderNames()
        {
            Assert.That(
                MaterialLibrary.ShaderNames,
                Is.EqualTo(new[]
                {
                    "Hidden/Sekai/V2/UberPost",
                    "Hidden/CP/PostEffect/BoxBlur",
                    "Hidden/SekaiRP/PostEffect/Bloom",
                    "Hidden/Sekai/V2/SekaiDepthOfField",
                    "Hidden/Sekai/SubpixelMorphologicalAntialiasing",
                }));
        }

        [Test]
        public void RecoveredSekaiDofShaderExposesEveryOfficialPassInOrder()
        {
            var shader = Shader.Find("Hidden/Sekai/V2/SekaiDepthOfField");

            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            try
            {
                Assert.That(material.passCount, Is.EqualTo(9));
                Assert.That(material.FindPass("Alpha_CoC"), Is.EqualTo(0));
                Assert.That(material.FindPass("Mid_DownSample_MRT"), Is.EqualTo(1));
                Assert.That(material.FindPass("Blur_Mid_Bg"), Is.EqualTo(2));
                Assert.That(material.FindPass("Blur_Low_Bg"), Is.EqualTo(3));
                Assert.That(material.FindPass("Downsample_With_Coc_Conserve"), Is.EqualTo(4));
                Assert.That(material.FindPass("Blur_Mid_Fg"), Is.EqualTo(5));
                Assert.That(material.FindPass("Blur_Low_Fg"), Is.EqualTo(6));
                Assert.That(material.FindPass("Apply_Source_Bg_Fg"), Is.EqualTo(7));
                Assert.That(material.FindPass("Apply_Source_Bg"), Is.EqualTo(8));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RecoveredBoxBlurShaderHasTheCapturedHorizontalAndVerticalPasses()
        {
            var shader = Shader.Find("Hidden/CP/PostEffect/BoxBlur");

            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            try
            {
                Assert.That(material.passCount, Is.EqualTo(2));
                Assert.That(material.FindPass("Horizontal"), Is.EqualTo(0));
                Assert.That(material.FindPass("Vertical"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RecoveredBloomShaderHasTheCapturedSixPassPipeline()
        {
            var shader = Shader.Find("Hidden/SekaiRP/PostEffect/Bloom");

            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            try
            {
                Assert.That(material.passCount, Is.EqualTo(6));
                Assert.That(material.FindPass("Texture To Sheet"), Is.EqualTo(0));
                Assert.That(material.FindPass("Blur Horizontal"), Is.EqualTo(1));
                Assert.That(material.FindPass("Blur Vertical"), Is.EqualTo(2));
                Assert.That(material.FindPass("Sheet To Texture"), Is.EqualTo(3));
                Assert.That(material.FindPass("Sheet To Texture Double"), Is.EqualTo(4));
                Assert.That(material.FindPass("Source Prefilter"), Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RecoveredUberPostShaderHasTheCapturedFivePassPipeline()
        {
            var shader = Shader.Find("Hidden/Sekai/V2/UberPost");

            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            try
            {
                Assert.That(material.passCount, Is.EqualTo(5));
                Assert.That(material.FindPass("Copy"), Is.EqualTo(0));
                Assert.That(material.FindPass("Final"), Is.EqualTo(1));
                Assert.That(material.FindPass("Saturation Blur"), Is.EqualTo(2));
                Assert.That(
                    material.FindPass("Pre Directional Blur"),
                    Is.EqualTo(3));
                Assert.That(
                    material.FindPass("Directional Blur"),
                    Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void BloomAtlasUsesTheCapturedSevenLevelPackedSurface()
        {
            var layout = BloomExtension.CalculateLayout(384, 256);

            Assert.That(BloomExtension.DownCount, Is.EqualTo(7));
            Assert.That(BloomExtension.Margin, Is.EqualTo(7));
            Assert.That(layout.AtlasSize, Is.EqualTo(new Vector2Int(398, 412)));
            Assert.That(
                layout.LevelSizes,
                Is.EqualTo(new[]
                {
                    new Vector2Int(384, 256),
                    new Vector2Int(192, 128),
                    new Vector2Int(96, 64),
                    new Vector2Int(48, 32),
                    new Vector2Int(24, 16),
                    new Vector2Int(12, 8),
                    new Vector2Int(6, 4),
                }));

            var maps = layout.TextureMaps;
            var gapX = 2f * BloomExtension.Margin / 384f;
            var gapY = 2f * BloomExtension.Margin / 256f;
            Assert.That(maps[0].x, Is.EqualTo(-1f));
            Assert.That(maps[0].y, Is.EqualTo(-1f));
            Assert.That(maps[1].x, Is.EqualTo(-1f));
            Assert.That(maps[1].y, Is.EqualTo(maps[0].yMax + gapY));
            Assert.That(maps[2].x, Is.EqualTo(maps[1].xMax + gapX));
            Assert.That(maps[2].y, Is.EqualTo(maps[1].y));
            Assert.That(maps[3].x, Is.EqualTo(maps[2].x));
            Assert.That(maps[3].y, Is.EqualTo(maps[2].yMax + gapY));
            Assert.That(maps[4].x, Is.EqualTo(maps[2].xMax + gapX));
            Assert.That(maps[4].y, Is.EqualTo(maps[1].y));
            Assert.That(maps[5].x, Is.EqualTo(maps[4].xMax + gapX));
            Assert.That(maps[5].y, Is.EqualTo(maps[1].y));
            Assert.That(maps[6].x, Is.EqualTo(maps[5].x));
            Assert.That(maps[6].y, Is.EqualTo(maps[5].yMax + gapY));
        }

        [TestCase(455, 256, 469, 412)]
        [TestCase(384, 256, 398, 412)]
        public void BloomAtlasDimensionsFollowTheRecoveredMarginFormula(
            int postWidth,
            int postHeight,
            int expectedWidth,
            int expectedHeight)
        {
            var layout = BloomExtension.CalculateLayout(postWidth, postHeight);

            Assert.That(
                layout.AtlasSize,
                Is.EqualTo(new Vector2Int(expectedWidth, expectedHeight)));
        }

        [Test]
        public void BloomMeshesMatchTheRecoveredBaseQuadAndSevenLevelLayout()
        {
            var layout = BloomExtension.CalculateLayout(384, 256);
            var textureToSheet = BloomExtension.CreateTextureToSheetMesh(layout);
            var sheetToTexture = BloomExtension.CreateSheetToTextureMesh(layout);
            try
            {
                Assert.That(textureToSheet.vertexCount, Is.EqualTo(32));
                Assert.That(textureToSheet.triangles.Length, Is.EqualTo(48));
                Assert.That(sheetToTexture.vertexCount, Is.EqualTo(32));
                Assert.That(sheetToTexture.triangles.Length, Is.EqualTo(48));

                var textureWeights = new System.Collections.Generic.List<Vector2>();
                var sheetWeights = new System.Collections.Generic.List<Vector2>();
                textureToSheet.GetUVs(1, textureWeights);
                sheetToTexture.GetUVs(1, sheetWeights);
                Assert.That(textureWeights[0], Is.EqualTo(Vector2.zero));
                Assert.That(textureWeights[4], Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(textureWeights[28], Is.EqualTo(new Vector2(6f, 1f)));
                Assert.That(sheetWeights[0], Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(sheetWeights[4], Is.EqualTo(Vector2.zero));
                Assert.That(sheetWeights[28], Is.EqualTo(new Vector2(6f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(textureToSheet);
                Object.DestroyImmediate(sheetToTexture);
            }
        }

        [Test]
        public void BloomScatterWeightUsesTheRecoveredPointZeroOneNormalization()
        {
            Assert.That(BloomExtension.ScatterNormalization, Is.EqualTo(0.01f));
            Assert.That(
                BloomExtension.CalculateScatterWeight(2f),
                Is.EqualTo(1f / 1.27f).Within(0.000001f));
        }

        [Test]
        public void RecoveredSmaaShaderHasTheCapturedThreePassPipeline()
        {
            var shader = Shader.Find(
                "Hidden/Sekai/SubpixelMorphologicalAntialiasing");

            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            try
            {
                Assert.That(material.passCount, Is.EqualTo(3));
                Assert.That(material.FindPass("Edge Detection"), Is.EqualTo(0));
                Assert.That(
                    material.FindPass("Blend Weights Calculation"),
                    Is.EqualTo(1));
                Assert.That(
                    material.FindPass("Neighborhood Blending"),
                    Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SmaaLookupTexturesExistAtTheCapturedResourcePaths()
        {
            var area = Resources.Load<Texture2D>(TextureResources.AreaTexturePath);
            var search = Resources.Load<Texture2D>(TextureResources.SearchTexturePath);

            Assert.That(area, Is.Not.Null);
            Assert.That(area.width, Is.EqualTo(160));
            Assert.That(area.height, Is.EqualTo(560));
            Assert.That(search, Is.Not.Null);
            Assert.That(search.width, Is.EqualTo(64));
            Assert.That(search.height, Is.EqualTo(16));
        }

        private static RenderTextureDescriptor Descriptor(int width, int height)
        {
            return new RenderTextureDescriptor(
                width,
                height,
                GraphicsFormat.R8G8B8A8_UNorm,
                0);
        }
    }
}
