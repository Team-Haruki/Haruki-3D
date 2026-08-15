using System.Reflection;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV.Tests
{
    public sealed class SekaiDistortionPassTests
    {
        [Test]
        public void DistortedUvPassUsesRecoveredTagQueueAndEvent()
        {
            var pass = new DistortedUvBufferPass("DistortedUv");

            Assert.That(pass.renderPassEvent,
                Is.EqualTo(RenderPassEvent.AfterRenderingTransparents));
            Assert.That(GetField<ShaderTagId>(pass, "_shaderTagId").name,
                Is.EqualTo("DistortedUv"));
            var queue = GetField<RenderQueueRange>(pass, "_renderQueueRange");
            Assert.That(queue.lowerBound, Is.EqualTo(RenderQueueRange.minimumBound));
            Assert.That(queue.upperBound, Is.EqualTo(RenderQueueRange.maximumBound));
        }

        [Test]
        public void ApplyPassUsesRecoveredPropertiesEventAndTargets()
        {
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var pass = new ApplyDistortionPass(shader);
            var uv = RTHandles.Alloc(32, 32, name: "Distorted-UV-Test");
            var dest = RTHandles.Alloc(32, 32, name: "Distortion-Dest-Test");
            try
            {
                pass.Setup(uv, dest);
                Assert.That(pass.renderPassEvent,
                    Is.EqualTo(RenderPassEvent.AfterRenderingPostProcessing));
                Assert.That(
                    GetField<int>(pass, "_distortionBufferPropertyId"),
                    Is.EqualTo(Shader.PropertyToID("_ScreenSpaceUvTexture")));
                Assert.That(
                    GetField<int>(pass, "_applyDistortionTexPropertyId"),
                    Is.EqualTo(Shader.PropertyToID("_ApplyDistortionTex")));
                Assert.That(GetField<RTHandle>(pass, "_distortedUvBufferRTHandle"),
                    Is.SameAs(uv));
                Assert.That(GetField<RTHandle>(pass, "_dest"), Is.SameAs(dest));
            }
            finally
            {
                pass.Dispose();
                uv.Release();
                dest.Release();
            }
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }
    }
}
