using System.Reflection;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV.Tests
{
    public sealed class SekaiDrawObjectsRendererFeatureTests
    {
        private sealed class FeatureProbe : SekaiDrawObjectsRendererFeature
        {
            public void SetSettings(
                RenderPassEvent evt,
                bool transparent,
                bool customRange,
                int lowerBound,
                int upperBound,
                params SekaiShaderTagType[] tags)
            {
                Settings.Event = evt;
                Settings.RenderQueueType = transparent
                    ? SekaiDrawObjectsRenderQueueType.transparent
                    : SekaiDrawObjectsRenderQueueType.opaque;
                Settings.UseCustomRenderQueueRange = customRange;
                Settings.RenderQueueLowerBound = lowerBound;
                Settings.RenderQueueUpperBound = upperBound;
                Settings.ShaderTagTypes = tags;
            }

            public SekaiDrawObjectsPass[] CreatedPasses => Passes;

            public void SetupCreatedPasses(
                LayerMask layerMask,
                StencilState stencilState,
                int stencilReference,
                SekaiBuffer buffer)
            {
                SetupPasses(layerMask, stencilState, stencilReference, buffer);
            }
        }

        [Test]
        public void DefaultsMatchRecoveredSettingsConstructor()
        {
            var feature = ScriptableObject.CreateInstance<FeatureProbe>();
            try
            {
                feature.Create();

                Assert.That(feature.CreatedPasses, Has.Length.EqualTo(1));
                Assert.That(
                    feature.CreatedPasses[0].renderPassEvent,
                    Is.EqualTo(RenderPassEvent.BeforeRenderingTransparents));
                Assert.That(GetField<bool>(feature.CreatedPasses[0], "m_IsOpaque"), Is.True);
                Assert.That(
                    GetField<FilteringSettings>(feature.CreatedPasses[0], "m_FilteringSettings")
                        .renderQueueRange,
                    Is.EqualTo(RenderQueueRange.opaque));
            }
            finally
            {
                Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void CreateBuildsOneTransparentPassPerShaderTagWithCustomRange()
        {
            var feature = ScriptableObject.CreateInstance<FeatureProbe>();
            try
            {
                feature.SetSettings(
                    RenderPassEvent.AfterRenderingTransparents,
                    true,
                    true,
                    2600,
                    3100,
                    SekaiShaderTagType.TransparentBase,
                    SekaiShaderTagType.Eyelash);

                feature.Create();

                Assert.That(feature.CreatedPasses, Has.Length.EqualTo(2));
                foreach (var pass in feature.CreatedPasses)
                {
                    var range = GetField<FilteringSettings>(pass, "m_FilteringSettings")
                        .renderQueueRange;
                    Assert.That(pass.renderPassEvent, Is.EqualTo(RenderPassEvent.AfterRenderingTransparents));
                    Assert.That(GetField<bool>(pass, "m_IsOpaque"), Is.False);
                    Assert.That(range.lowerBound, Is.EqualTo(2600));
                    Assert.That(range.upperBound, Is.EqualTo(3100));
                }
            }
            finally
            {
                Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void MissingShaderTagsProducesNoPassArray()
        {
            var feature = ScriptableObject.CreateInstance<FeatureProbe>();
            try
            {
                feature.SetSettings(
                    RenderPassEvent.BeforeRenderingTransparents,
                    false,
                    false,
                    0,
                    0,
                    null);

                feature.Create();

                Assert.That(feature.CreatedPasses, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void SetupAppliesOfficialRendererResourcesToEveryPass()
        {
            var feature = ScriptableObject.CreateInstance<FeatureProbe>();
            var buffer = new SekaiBuffer();
            try
            {
                feature.SetSettings(
                    RenderPassEvent.BeforeRenderingTransparents,
                    false,
                    false,
                    0,
                    0,
                    SekaiShaderTagType.Default,
                    SekaiShaderTagType.OpaqueOutline);
                feature.Create();
                var stencil = new StencilState(
                    true,
                    0xff,
                    0xff,
                    CompareFunction.Equal,
                    StencilOp.Keep,
                    StencilOp.Keep,
                    StencilOp.Keep);

                feature.SetupCreatedPasses(1 << 14, stencil, 6, buffer);

                foreach (var pass in feature.CreatedPasses)
                {
                    var filtering = GetField<FilteringSettings>(pass, "m_FilteringSettings");
                    var state = GetField<RenderStateBlock>(pass, "m_RenderStateBlock");
                    Assert.That(filtering.layerMask, Is.EqualTo(1 << 14));
                    Assert.That(state.stencilReference, Is.EqualTo(6));
                    Assert.That(GetField<SekaiBuffer>(pass, "m_Buffer"), Is.SameAs(buffer));
                }
            }
            finally
            {
                buffer.ReleaseMRT();
                Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void ReflectionFeatureGateMatchesTheRecoveredThreeConditions()
        {
            var pass = PlanarReflectionPass.Instance;
            try
            {
                SekaiCharacterReflectionOffSettings.Clear();
                SekaiCharacterReflectionOffSettings.SetMemberNum(1);
                pass.EnablePlanarReflection = false;

                Assert.That(
                    InvokeReflectionGate(typeof(SekaiOpaqueReflectionRendererFeature)),
                    Is.True);
                Assert.That(
                    InvokeReflectionGate(typeof(SekaiTransparentReflectionRendererFeature)),
                    Is.True);

                pass.EnablePlanarReflection = true;
                Assert.That(
                    InvokeReflectionGate(typeof(SekaiOpaqueReflectionRendererFeature)),
                    Is.False);

                pass.EnablePlanarReflection = false;
                SekaiCharacterReflectionOffSettings.SetIsHidingAll(true);
                Assert.That(
                    InvokeReflectionGate(typeof(SekaiOpaqueReflectionRendererFeature)),
                    Is.False);

                SekaiCharacterReflectionOffSettings.SetIsHidingAll(false);
                SekaiCharacterReflectionOffSettings.RegisterCharacterReflectionOff(
                    new ReflectionOffProbe());
                Assert.That(
                    InvokeReflectionGate(typeof(SekaiOpaqueReflectionRendererFeature)),
                    Is.False);
            }
            finally
            {
                pass.EnablePlanarReflection = false;
                SekaiCharacterReflectionOffSettings.Clear();
            }
        }

        private static bool InvokeReflectionGate(System.Type featureType)
        {
            var method = featureType.GetMethod(
                "ShouldEnqueueReflectionPasses",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, null);
        }

        private sealed class ReflectionOffProbe : ISekaiCharacterReflectionOff
        {
            public bool IsReflectionHiding => true;

            public int FormationId => 0;
        }

        private static T GetField<T>(SekaiDrawObjectsPass pass, string fieldName)
        {
            var field = typeof(SekaiDrawObjectsPass).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(pass);
        }
    }
}
