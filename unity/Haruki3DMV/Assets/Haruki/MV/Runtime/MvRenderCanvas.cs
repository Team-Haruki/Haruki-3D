using System;
using System.Collections.Generic;
using Sekai.Core;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Haruki.MV
{
    /// <summary>
    /// Owns the official main-camera boundary: all Main/CutIn cameras render
    /// into one linear, single-sample texture, stage monitors consume that
    /// texture, and a full-screen canvas presents it to URP's final target.
    /// </summary>
    public sealed class MvRenderCanvas : IDisposable
    {
        public const string ResourcePath = "Core/Common/Camera/RenderCanvas";

        private readonly HashSet<Camera> _boundCameras = new HashSet<Camera>();

        public MvRenderCanvas(Transform parent = null)
        {
            Root = new GameObject("RenderCanvas", typeof(RectTransform), typeof(Canvas));
            Root.transform.SetParent(parent, false);
            Root.layer = 5;
            Canvas = Root.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            BaseCamera = CreatePresentationCamera(
                "BaseCamera",
                Root.transform,
                true,
                0,
                out var baseData);
            BaseCameraData = baseData;
            UiCamera = CreatePresentationCamera(
                "UICamera",
                Root.transform,
                false,
                1 << 5,
                out var uiData);
            UiCameraData = uiData;
            UiCameraData.renderType = CameraRenderType.Overlay;
            BaseCameraData.cameraStack.Add(UiCamera);

            var imageObject = new GameObject(
                "MainCameraOutput",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            imageObject.transform.SetParent(Root.transform, false);
            imageObject.layer = 5;
            Image = imageObject.GetComponent<RawImage>();
            Image.raycastTarget = false;
            Image.color = Color.white;

            var rect = Image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public GameObject Root { get; }
        public Canvas Canvas { get; }
        public RawImage Image { get; }
        public Camera BaseCamera { get; }
        public Camera UiCamera { get; }
        public UniversalAdditionalCameraData BaseCameraData { get; }
        public UniversalAdditionalCameraData UiCameraData { get; }
        public RenderTexture Target { get; private set; }

        public void Configure(Vector2Int size)
        {
            if (size.x <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Width must be positive.");
            }
            if (size.y <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Height must be positive.");
            }
            if (Target != null && Target.width == size.x && Target.height == size.y)
            {
                return;
            }

            var previous = Target;
            Target = CreateTarget(size);
            Target.name = $"_HarukiMvMainCamera_{size.x}x{size.y}";
            Target.filterMode = FilterMode.Bilinear;
            Target.wrapMode = TextureWrapMode.Clamp;
            Target.Create();

            Image.texture = Target;
            LiveMonitorRuntime.MainCameraTexture = Target;
            foreach (var camera in _boundCameras)
            {
                if (camera != null)
                {
                    camera.targetTexture = Target;
                }
            }

            Release(previous);
        }

        public void Bind(Camera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }
            if (Target == null)
            {
                throw new InvalidOperationException("Configure the MV render canvas before binding cameras.");
            }
            _boundCameras.Add(camera);
            camera.targetTexture = Target;
        }

        public void Unbind(Camera camera)
        {
            if (camera == null)
            {
                return;
            }
            _boundCameras.Remove(camera);
            if (camera.targetTexture == Target)
            {
                camera.targetTexture = null;
            }
        }

        public void Dispose()
        {
            foreach (var camera in _boundCameras)
            {
                if (camera != null && camera.targetTexture == Target)
                {
                    camera.targetTexture = null;
                }
            }
            _boundCameras.Clear();
            if (Image != null && Image.texture == Target)
            {
                Image.texture = null;
            }
            if (LiveMonitorRuntime.MainCameraTexture == Target)
            {
                LiveMonitorRuntime.MainCameraTexture = null;
            }
            Release(Target);
            Target = null;
            Destroy(Root);
        }

        private static RenderTexture CreateTarget(Vector2Int size)
        {
            var descriptor = new RenderTextureDescriptor(
                size.x,
                size.y,
                GraphicsFormat.R8G8B8A8_UNorm,
                24)
            {
                msaaSamples = 1,
                volumeDepth = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false,
                useDynamicScale = false,
            };
            return new RenderTexture(descriptor);
        }

        private static Camera CreatePresentationCamera(
            string name,
            Transform parent,
            bool isBase,
            int cullingMask,
            out UniversalAdditionalCameraData cameraData)
        {
            var cameraObject = new GameObject(name, typeof(Camera));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.layer = 0;

            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = isBase
                ? CameraClearFlags.SolidColor
                : CameraClearFlags.Depth;
            camera.backgroundColor = Color.black;
            camera.cullingMask = cullingMask;
            camera.depth = isBase ? -100f : -99f;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = true;
            camera.allowMSAA = isBase;
            camera.allowDynamicResolution = false;
            camera.targetTexture = null;

            cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.SetRenderer(MvRecoveredCameraResources.PresentationRendererIndex);
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.stopNaN = false;
            cameraData.dithering = false;
            return camera;
        }

        private static void Release(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }
            texture.Release();
            Destroy(texture);
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
