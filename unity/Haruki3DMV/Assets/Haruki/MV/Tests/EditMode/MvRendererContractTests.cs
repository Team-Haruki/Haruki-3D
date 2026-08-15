using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvRendererContractTests
    {
        [Test]
        public void MainRendererFiveUsesTheCapturedFullFeatureOrder()
        {
            var contract = MvRecoveredRendererContract.ForRenderer(5);

            Assert.That(contract.Count, Is.EqualTo(13));
            Assert.That(contract[0].Name, Is.EqualTo("OpaqueForward"));
            Assert.That(contract[0].TypeName, Is.EqualTo("SekaiDrawObjectsRendererFeature"));
            Assert.That(contract[6].Name, Is.EqualTo("BeforePostProcess"));
            Assert.That(contract[7].TypeName, Is.EqualTo("SekaiPostProcessRendererFeature"));
            Assert.That(contract[8].TypeName, Is.EqualTo("SekaiCharacterOutlineFeature"));
            Assert.That(contract[12].TypeName, Is.EqualTo("PlanarReflectionFeature"));
        }

        [Test]
        public void SubRendererTenOmitsMainOnlyCompositionPasses()
        {
            var contract = MvRecoveredRendererContract.ForRenderer(10);

            Assert.That(contract.Count, Is.EqualTo(8));
            Assert.That(
                contract.Select(feature => feature.Name),
                Is.EqualTo(new[]
                {
                    "OpaqueForward",
                    "OpaqueToon",
                    "TransparentForward",
                    "MusicItem",
                    "Opaque Reflection",
                    "TransparentReflection",
                    "SekaiCharacterOutlineFeature",
                    "Eyelash",
                }));
        }

        [Test]
        public void ValidationRejectsMissingOrReorderedRendererFeatures()
        {
            var expected = MvRecoveredRendererContract.ForRenderer(5);
            var reordered = expected.ToArray();
            (reordered[0], reordered[1]) = (reordered[1], reordered[0]);

            var errors = MvRecoveredRendererContract.Validate(5, reordered);

            Assert.That(errors, Has.Some.Contains("position 0"));
            Assert.That(
                MvRecoveredRendererContract.Validate(5, expected.Take(12).ToArray()),
                Has.Some.Contains("13 features"));
        }

        [Test]
        public void CapturedFeatureSettingsRemainPartOfTheRendererContract()
        {
            Assert.That(MvRecoveredRendererContract.OutlineWidthMin, Is.EqualTo(0.04f));
            Assert.That(MvRecoveredRendererContract.OutlineWidthMax, Is.EqualTo(0.95f));
            Assert.That(MvRecoveredRendererContract.OutlineDistanceNear, Is.EqualTo(0.45f));
            Assert.That(MvRecoveredRendererContract.OutlineDistanceFar, Is.EqualTo(20f));
            Assert.That(MvRecoveredRendererContract.PlanarReflectionWidth, Is.EqualTo(1024));
            Assert.That(MvRecoveredRendererContract.PlanarReflectionHeight, Is.EqualTo(1024));
            Assert.That(MvRecoveredRendererContract.PlanarReflectionClipPlaneOffset, Is.Zero);
            Assert.That(MvRecoveredRendererContract.PlanarReflectionPlaneOffset, Is.Zero);
            Assert.That(
                MvRecoveredRendererContract.PlanarReflectionStencilShader,
                Is.EqualTo("Sekai/Live/DrawStencil"));
            Assert.That(
                MvRecoveredRendererContract.ApplyDistortionShader,
                Is.EqualTo("Hidden/Sekai/Live/ApplyDistortion"));
        }

        [Test]
        public void OutlineGlobalsFollowTheRecoveredFeatureFormula()
        {
            var curve = AnimationCurve.Linear(0f, 10f, 100f, 10f);

            var globals = MvRecoveredRendererContract.CalculateOutlineGlobals(50f, curve);

            Assert.That(
                globals.Width,
                Is.EqualTo(new Vector4(0.0004f, 0.0095f, 0f, 0f)));
            Assert.That(globals.Factor.x, Is.EqualTo(0.45f));
            Assert.That(globals.Factor.y, Is.EqualTo(1f / (20f - 0.45f)));
            Assert.That(globals.Factor.z, Is.EqualTo(5f));
            Assert.That(globals.Factor.w, Is.Zero);
        }

        [Test]
        public void MissingOutlineCurveUsesTheRecoveredUnityFallbackFactor()
        {
            var globals = MvRecoveredRendererContract.CalculateOutlineGlobals(30f, null);

            Assert.That(globals.Factor.z, Is.EqualTo(1f));
        }

        [Test]
        public void CapturedOutlineCurveReproducesTheMvVulkanSampleAtFovFifteen()
        {
            var curve = MvRecoveredRendererContract.CreateOutlineFovCurve();

            var globals = MvRecoveredRendererContract.CalculateOutlineGlobals(15f, curve);

            Assert.That(curve.length, Is.EqualTo(2));
            Assert.That(curve.preWrapMode, Is.EqualTo(WrapMode.ClampForever));
            Assert.That(curve.postWrapMode, Is.EqualTo(WrapMode.ClampForever));
            Assert.That(globals.Factor.z, Is.EqualTo(0.581489444f).Within(1e-7f));
        }
    }
}
