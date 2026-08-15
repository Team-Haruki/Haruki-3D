using System.Reflection;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV.Tests
{
    public sealed class SekaiDrawObjectsPassTests
    {
        [TestCase(SekaiShaderTagType.Default, "SRPDefaultUnlit")]
        [TestCase(SekaiShaderTagType.OpaqueOutline, "SekaiOutline")]
        [TestCase(SekaiShaderTagType.OpaqueReflection, "SekaiReflection")]
        [TestCase(SekaiShaderTagType.TransparentBase, "SekaiTransparentBase")]
        [TestCase(SekaiShaderTagType.TransparentOutline, "SekaiTransparentOutline")]
        [TestCase(SekaiShaderTagType.TransparentReflection, "SekaiTransparentReflection")]
        [TestCase(SekaiShaderTagType.MeshFlarePara, "SekaiMeshFlarePara")]
        [TestCase(SekaiShaderTagType.Monitor, "SekaiMonitor")]
        [TestCase(SekaiShaderTagType.Eyelash, "SekaiEyelash")]
        public void ShaderTagMappingMatchesTheRecoveredSwitch(
            SekaiShaderTagType tagType,
            string expected)
        {
            Assert.That(SekaiShaderTag.GetTag(tagType), Is.EqualTo(expected));
        }

        [Test]
        public void PassUsesRecoveredOpaqueAndFlipGlobals()
        {
            Assert.That(
                InvokeVector("DrawObjectPassData", true),
                Is.EqualTo(new Vector4(0f, 0f, 0f, 1f)));
            Assert.That(
                InvokeVector("DrawObjectPassData", false),
                Is.EqualTo(Vector4.zero));
            Assert.That(
                InvokeVector("ScaleBias", false),
                Is.EqualTo(new Vector4(1f, 0f, 1f, 1f)));
            Assert.That(
                InvokeVector("ScaleBias", true),
                Is.EqualTo(new Vector4(-1f, 1f, -1f, 1f)));
        }

        [Test]
        public void SetupAppliesLayerAndStencilState()
        {
            var pass = new SekaiDrawObjectsPass(
                "OpaqueForward",
                SekaiShaderTagType.Default,
                true,
                RenderPassEvent.BeforeRenderingOpaques,
                RenderQueueRange.opaque);
            var buffer = new SekaiBuffer();
            try
            {
                var stencil = new StencilState(
                    true,
                    0xff,
                    0xff,
                    CompareFunction.Equal,
                    StencilOp.Keep,
                    StencilOp.Keep,
                    StencilOp.Keep);
                InvokeSetup(pass, 1 << 12, stencil, 7, buffer);

                var filtering = GetField<FilteringSettings>(pass, "m_FilteringSettings");
                var state = GetField<RenderStateBlock>(pass, "m_RenderStateBlock");
                Assert.That(filtering.layerMask, Is.EqualTo(1 << 12));
                Assert.That(state.mask, Is.EqualTo(RenderStateMask.Stencil));
                Assert.That(state.stencilReference, Is.EqualTo(7));
                Assert.That(state.stencilState.enabled, Is.True);
                Assert.That(GetField<SekaiBuffer>(pass, "m_Buffer"), Is.SameAs(buffer));
            }
            finally
            {
                buffer.ReleaseMRT();
            }
        }

        private static Vector4 InvokeVector(string methodName, bool value)
        {
            var method = typeof(SekaiDrawObjectsPass).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Vector4)method.Invoke(null, new object[] { value });
        }

        private static void InvokeSetup(
            SekaiDrawObjectsPass pass,
            LayerMask layerMask,
            StencilState stencilState,
            int stencilReference,
            SekaiBuffer buffer)
        {
            var method = typeof(SekaiDrawObjectsPass).GetMethod(
                "Setup",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(pass, new object[] { layerMask, stencilState, stencilReference, buffer });
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
