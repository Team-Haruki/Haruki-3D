using System.Reflection;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Haruki.MV.Tests
{
    public sealed class SekaiBufferTests
    {
        [Test]
        public void ConstructorCreatesTheRecoveredThreePointFilteredAttachments()
        {
            var buffer = new SekaiBuffer();
            try
            {
                Assert.That(buffer.BufferFilterModes, Has.Length.EqualTo(3));
                Assert.That(buffer.BufferFilterModes, Has.All.EqualTo(FilterMode.Point));

                var handles = Get<RTHandle[]>(buffer, "SekaiBufferHandles");
                Assert.That(handles, Has.Length.EqualTo(3));
                Assert.That(handles[0].name, Is.EqualTo("_ColorBuffer"));
                Assert.That(handles[1].name, Is.EqualTo("_DepthBuffer"));
                Assert.That(handles[2].name, Is.EqualTo("_BrightnessBuffer"));

                var expectedFormat = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? GraphicsFormat.R8G8B8A8_SRGB
                    : GraphicsFormat.R8G8B8A8_UNorm;
                Assert.That(
                    Get<GraphicsFormat[]>(buffer, "SekaiBufferHandleFormats"),
                    Has.All.EqualTo(expectedFormat));
            }
            finally
            {
                buffer.ReleaseMRT();
            }
        }

        [Test]
        public void AllocationDescriptorKeepsCameraSizeButRemovesPerAttachmentDepth()
        {
            var buffer = new SekaiBuffer();
            try
            {
                var descriptor = new RenderTextureDescriptor(1920, 1080)
                {
                    depthBufferBits = 24,
                    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    msaaSamples = 1,
                };

                var method = typeof(SekaiBuffer).GetMethod(
                    "ConfigureDescriptor",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var configured = (RenderTextureDescriptor)method.Invoke(
                    null,
                    new object[] { descriptor, GraphicsFormat.R8G8B8A8_UNorm });

                Assert.That(configured.width, Is.EqualTo(1920));
                Assert.That(configured.height, Is.EqualTo(1080));
                Assert.That(configured.depthBufferBits, Is.EqualTo(0));
                Assert.That(configured.graphicsFormat, Is.EqualTo(GraphicsFormat.R8G8B8A8_UNorm));
                Assert.That(configured.msaaSamples, Is.EqualTo(1));
            }
            finally
            {
                buffer.ReleaseMRT();
            }
        }

        private static T Get<T>(SekaiBuffer buffer, string propertyName)
        {
            var property = typeof(SekaiBuffer).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(buffer);
        }
    }
}
