using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Haruki.MV.Tests
{
    public sealed class MvRenderCanvasTests
    {
        [TestCase(1920, 1080)]
        [TestCase(3840, 2160)]
        public void OwnsCapturedLinearMainTargetAndFullscreenPresenter(int width, int height)
        {
            var surface = new MvRenderCanvas();
            try
            {
                surface.Configure(new Vector2Int(width, height));

                Assert.That(surface.Root.name, Is.EqualTo("RenderCanvas"));
                Assert.That(surface.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(surface.BaseCamera.name, Is.EqualTo("BaseCamera"));
                Assert.That(surface.UiCamera.name, Is.EqualTo("UICamera"));
                Assert.That(surface.BaseCamera.targetTexture, Is.Null);
                Assert.That(surface.UiCamera.targetTexture, Is.Null);
                Assert.That(surface.BaseCamera.allowHDR, Is.True);
                Assert.That(surface.BaseCamera.allowMSAA, Is.True);
                Assert.That(surface.BaseCamera.allowDynamicResolution, Is.False);
                Assert.That(surface.UiCamera.allowHDR, Is.True);
                Assert.That(surface.UiCamera.allowMSAA, Is.False);
                Assert.That(surface.UiCamera.allowDynamicResolution, Is.False);
                Assert.That(surface.BaseCameraData.renderType, Is.EqualTo(CameraRenderType.Base));
                Assert.That(surface.UiCameraData.renderType, Is.EqualTo(CameraRenderType.Overlay));
                Assert.That(surface.BaseCameraData.renderPostProcessing, Is.False);
                Assert.That(surface.UiCameraData.renderPostProcessing, Is.False);
                Assert.That(surface.BaseCameraData.antialiasing, Is.EqualTo(AntialiasingMode.None));
                Assert.That(surface.UiCameraData.antialiasing, Is.EqualTo(AntialiasingMode.None));
                Assert.That(surface.BaseCameraData.cameraStack, Does.Contain(surface.UiCamera));
                Assert.That(surface.Target.width, Is.EqualTo(width));
                Assert.That(surface.Target.height, Is.EqualTo(height));
                Assert.That(surface.Target.depth, Is.EqualTo(24));
                Assert.That(surface.Target.graphicsFormat, Is.EqualTo(GraphicsFormat.R8G8B8A8_UNorm));
                Assert.That(surface.Target.antiAliasing, Is.EqualTo(1));
                Assert.That(surface.Target.sRGB, Is.False);
                Assert.That(surface.Target.useDynamicScale, Is.False);
                Assert.That(surface.Image.texture, Is.SameAs(surface.Target));
                Assert.That(surface.Image.raycastTarget, Is.False);
                Assert.That(surface.Image.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(surface.Image.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(surface.Image.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(surface.Image.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
                Assert.That(surface.Image.gameObject.layer, Is.EqualTo(5));
            }
            finally
            {
                surface.Dispose();
            }
        }

        [Test]
        public void ReconfigurationRebindsEveryRegisteredCameraToOneSharedTarget()
        {
            var firstObject = new GameObject("MainCamera");
            var secondObject = new GameObject("CutInCamera");
            var first = firstObject.AddComponent<Camera>();
            var second = secondObject.AddComponent<Camera>();
            var surface = new MvRenderCanvas();
            try
            {
                surface.Configure(new Vector2Int(1920, 1080));
                surface.Bind(first);
                surface.Bind(second);
                var oldTarget = surface.Target;

                surface.Configure(new Vector2Int(3840, 2160));

                Assert.That(surface.Target, Is.Not.SameAs(oldTarget));
                Assert.That(first.targetTexture, Is.SameAs(surface.Target));
                Assert.That(second.targetTexture, Is.SameAs(surface.Target));
                Assert.That(surface.Image.texture, Is.SameAs(surface.Target));
            }
            finally
            {
                surface.Dispose();
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }
    }
}
