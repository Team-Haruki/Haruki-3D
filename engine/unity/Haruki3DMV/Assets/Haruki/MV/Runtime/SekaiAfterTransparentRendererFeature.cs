using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Owns the two recovered screen-distortion passes that run after the
    /// transparent stage geometry. Temporary targets inherit the active camera
    /// descriptor, so a selected 1080p or UHD output is processed at that exact
    /// pixel size rather than at the costume-preview resolution.
    /// </summary>
    public sealed class SekaiAfterTransparentRendererFeature : ScriptableRendererFeature
    {
        private const string DistortionLightMode = "DistortedUvBuffer";
        private const string ApplyDistortionShaderName = "Hidden/Sekai/Live/ApplyDistortion";

        private ApplyDistortionPass _applyDistortionPass;
        private DistortedUvBufferPass _distortedUvBufferPass;
        private Shader _applyDistortionShader;
        private RTHandle _distortedUvBufferRTHandle;
        private RTHandle _dest;

        public override void Create()
        {
            _applyDistortionShader = Shader.Find(ApplyDistortionShaderName);
            if (_applyDistortionShader == null)
            {
                Debug.LogWarning(
                    $"{ApplyDistortionShaderName} shader was not found; " +
                    "the recovered distortion pass remains disabled.");
                return;
            }

            _distortedUvBufferPass = new DistortedUvBufferPass(DistortionLightMode);
            _applyDistortionPass = new ApplyDistortionPass(_applyDistortionShader);
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (renderer == null ||
                _distortedUvBufferPass == null ||
                _applyDistortionPass == null ||
                !EffectDistortionManager.Instance.EnableUseEffectDistortion)
            {
                return;
            }

            renderer.EnqueuePass(_distortedUvBufferPass);
            renderer.EnqueuePass(_applyDistortionPass);
        }

        public override void SetupRenderPasses(
            ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            if (_applyDistortionPass == null ||
                _distortedUvBufferPass == null ||
                !ShouldSetupForCamera(renderingData.cameraData.cameraType))
            {
                return;
            }

            var descriptor = ConfigureDistortionDescriptor(
                renderingData.cameraData.cameraTargetDescriptor);
            RenderingUtils.ReAllocateIfNeeded(
                ref _distortedUvBufferRTHandle,
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Repeat,
                false,
                1,
                0f,
                string.Empty);
            RenderingUtils.ReAllocateIfNeeded(
                ref _dest,
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Repeat,
                false,
                1,
                0f,
                string.Empty);

            _distortedUvBufferPass.Setup(_distortedUvBufferRTHandle);
            _applyDistortionPass.Setup(_distortedUvBufferRTHandle, _dest);
        }

        internal static RenderTextureDescriptor ConfigureDistortionDescriptor(
            RenderTextureDescriptor descriptor)
        {
            descriptor.depthBufferBits = 0;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;
            return descriptor;
        }

        internal static bool ShouldSetupForCamera(CameraType cameraType)
        {
            return cameraType != CameraType.Preview &&
                cameraType != CameraType.Reflection;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _distortedUvBufferRTHandle?.Release();
            _distortedUvBufferRTHandle = null;
            _dest?.Release();
            _dest = null;
            _applyDistortionPass?.Dispose();
            _applyDistortionPass = null;
            _distortedUvBufferPass = null;
        }
    }
}
