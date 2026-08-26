using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Sekai.Rendering.Tests
{
    public sealed class SekaiAfterTransparentRendererFeatureTests
    {
        [TestCase(1920, 1080)]
        [TestCase(3840, 2160)]
        public void DistortionTargetsKeepTheSelectedMvOutputPixels(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 24)
            {
                msaaSamples = 4,
            };

            var configured = (RenderTextureDescriptor)Invoke(
                "ConfigureDistortionDescriptor",
                descriptor);

            Assert.That(configured.width, Is.EqualTo(width));
            Assert.That(configured.height, Is.EqualTo(height));
            Assert.That(configured.depthBufferBits, Is.Zero);
            Assert.That(configured.colorFormat, Is.EqualTo(RenderTextureFormat.ARGB32));
            Assert.That(configured.msaaSamples, Is.EqualTo(4));
        }

        [TestCase(CameraType.Game, true)]
        [TestCase(CameraType.SceneView, true)]
        [TestCase(CameraType.Preview, false)]
        [TestCase(CameraType.Reflection, false)]
        public void SetupCameraGateMatchesRecoveredFeature(CameraType cameraType, bool expected)
        {
            Assert.That((bool)Invoke("ShouldSetupForCamera", cameraType), Is.EqualTo(expected));
        }

        private static object Invoke(string methodName, object argument)
        {
            var method = typeof(SekaiAfterTransparentRendererFeature).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new[] { argument });
        }
    }
}
