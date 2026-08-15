using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV
{
    /// <summary>
    /// Reconstructs only the camera resource state closed by the 6.7.0 runtime
    /// capture. Base-APK Resources still take precedence when they are present.
    /// </summary>
    public static class MvRecoveredCameraResources
    {
        public const int PresentationRendererIndex = 0;
        public const int MainRendererIndex = 5;
        public const int SubRendererIndex = 10;
        public const int MainCharacterLayer = 21;

        public static GameObject Create(string resourcePath)
        {
            switch (resourcePath)
            {
                case MvCameraNode.MainCameraResource:
                    return CreateMainCamera();
                case MvCameraNode.SubCameraResource:
                    return CreateSubCamera();
                default:
                    throw new ArgumentException(
                        $"No recovered camera contract exists for '{resourcePath}'.",
                        nameof(resourcePath));
            }
        }

        private static GameObject CreateMainCamera()
        {
            var root = Node("MainCamera", null);
            var mainCam = Node("mainCam", root.transform);
            mainCam.transform.localPosition = new Vector3(1.8f, 1f, 6f);
            mainCam.transform.localRotation = new Quaternion(
                0.0411452651f,
                -0.972215474f,
                0.038579978f,
                0.227191001f);

            var cameraParameter = Node("CamParam", mainCam.transform);
            cameraParameter.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            cameraParameter.transform.localScale = new Vector3(1f, 1f, 35f);

            var cameraObject = Node("Camera", mainCam.transform);
            ConfigureMainCamera(cameraObject.AddComponent<Camera>());
            return root;
        }

        private static GameObject CreateSubCamera()
        {
            var root = Node("SubCamera", null);
            root.transform.localScale = new Vector3(1f, 1.000000238f, 1.000000238f);
            var subCam = Node("subCam", root.transform);
            Node("target", subCam.transform);

            var cameraObject = Node("Camera", subCam.transform);
            ConfigureSubCamera(cameraObject.AddComponent<Camera>());

            var cameraParameter = Node("CamParam", subCam.transform);
            cameraParameter.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            return root;
        }

        private static void ConfigureMainCamera(Camera camera)
        {
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 50f;
            camera.orthographic = false;
            camera.orthographicSize = 5f;
            camera.depth = 0f;
            camera.cullingMask = 555745280;
            camera.clearFlags = CameraClearFlags.Color;
            ConfigureCapturedOutputFlags(camera);
            ConfigureCapturedRenderer(camera, MainRendererIndex);
        }

        private static void ConfigureSubCamera(Camera camera)
        {
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            camera.fieldOfView = 30f;
            camera.orthographic = false;
            camera.orthographicSize = 5f;
            camera.depth = -2f;
            camera.cullingMask = 20971520;
            camera.clearFlags = CameraClearFlags.Depth;
            ConfigureCapturedOutputFlags(camera);
            ConfigureCapturedRenderer(camera, SubRendererIndex);
        }

        private static void ConfigureCapturedOutputFlags(Camera camera)
        {
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
        }

        private static void ConfigureCapturedRenderer(Camera camera, int rendererIndex)
        {
            var data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.SetRenderer(rendererIndex);
        }

        private static GameObject Node(string name, Transform parent)
        {
            var node = new GameObject(name);
            if (parent != null)
            {
                node.transform.SetParent(parent, false);
            }
            return node;
        }
    }
}
