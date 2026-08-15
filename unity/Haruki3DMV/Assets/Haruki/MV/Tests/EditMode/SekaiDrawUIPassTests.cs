using System.Reflection;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Haruki.MV.Tests
{
    public sealed class SekaiDrawUIPassTests
    {
        [Test]
        public void SetupUsesDefaultUiTagLayerQueueAndStencil()
        {
            var pass = new SekaiDrawUIPass("RenderUI");
            var color = RTHandles.Alloc(32, 32, name: "UI-Color-Test");
            var depth = RTHandles.Alloc(32, 32, depthBufferBits: DepthBits.Depth24,
                name: "UI-Depth-Test");
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
                InvokeSetup(
                    pass,
                    1 << 9,
                    stencil,
                    3,
                    color,
                    depth,
                    true,
                    new RenderQueueRange(2000, 3500));

                Assert.That(GetField<LayerMask>(pass, "m_LayerMask").value,
                    Is.EqualTo(1 << 9));
                Assert.That(GetField<ShaderTagId>(pass, "m_ShaderTagId").name,
                    Is.EqualTo("SRPDEFAULTUNLIT"));
                var state = GetField<RenderStateBlock>(pass, "m_RenderStateBlock");
                Assert.That(state.mask, Is.EqualTo(RenderStateMask.Stencil));
                Assert.That(state.stencilReference, Is.EqualTo(3));
                var queue = GetField<RenderQueueRange>(pass, "m_RenderQueueRange");
                Assert.That(queue.lowerBound, Is.EqualTo(2000));
                Assert.That(queue.upperBound, Is.EqualTo(3500));
            }
            finally
            {
                pass.Cleanup();
                color.Release();
                depth.Release();
            }
        }

        [Test]
        public void UiGlobalsMatchTheRecoveredFlipAndScreenFormulas()
        {
            Assert.That(
                InvokeVector("DrawObjectPassData", false),
                Is.EqualTo(new Vector4(1f, 0f, 1f, 1f)));
            Assert.That(
                InvokeVector("DrawObjectPassData", true),
                Is.EqualTo(new Vector4(-1f, 1f, -1f, 1f)));

            var screen = InvokeScreenParams(1920f, 1080f);
            Assert.That(screen.x, Is.EqualTo(1920f));
            Assert.That(screen.y, Is.EqualTo(1080f));
            Assert.That(screen.z, Is.EqualTo(1f + 1f / 1920f).Within(1e-7f));
            Assert.That(screen.w, Is.EqualTo(1f + 1f / 1080f).Within(1e-7f));
        }

        private static void InvokeSetup(
            SekaiDrawUIPass pass,
            LayerMask layerMask,
            StencilState stencilState,
            int stencilReference,
            RTHandle color,
            RTHandle depth,
            bool isCameraTarget,
            RenderQueueRange queue)
        {
            var method = typeof(SekaiDrawUIPass).GetMethod(
                "Setup",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(pass, new object[]
            {
                layerMask,
                stencilState,
                stencilReference,
                color,
                depth,
                isCameraTarget,
                queue,
            });
        }

        private static Vector4 InvokeVector(string methodName, bool value)
        {
            var method = typeof(SekaiDrawUIPass).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Vector4)method.Invoke(null, new object[] { value });
        }

        private static Vector4 InvokeScreenParams(float width, float height)
        {
            var method = typeof(SekaiDrawUIPass).GetMethod(
                "ScreenParams",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Vector4)method.Invoke(null, new object[] { width, height });
        }

        private static T GetField<T>(SekaiDrawUIPass pass, string fieldName)
        {
            var field = typeof(SekaiDrawUIPass).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(pass);
        }
    }
}
