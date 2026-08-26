using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Applies the stage distortion UV buffer to the active camera color target.
    /// Reconstructed from the 6.7.0 URP pass rather than approximated as a
    /// post-process volume.
    /// </summary>
    public sealed class ApplyDistortionPass : ScriptableRenderPass
    {
        private const string RenderPassName = "ApplyDistortionPass";
        private readonly int _distortionBufferPropertyId;
        private readonly int _applyDistortionTexPropertyId;
        private readonly Material _material;
        private readonly ProfilingSampler _renderPassProfilingSampler;
        private RTHandle _distortedUvBufferRTHandle;
        private RTHandle _dest;

        public ApplyDistortionPass(Shader shader)
        {
            _distortionBufferPropertyId = Shader.PropertyToID("_ScreenSpaceUvTexture");
            _applyDistortionTexPropertyId = Shader.PropertyToID("_ApplyDistortionTex");
            _material = CoreUtils.CreateEngineMaterial(shader);
            _renderPassProfilingSampler = new ProfilingSampler(RenderPassName);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public void Setup(RTHandle distortedUvBufferRTHandle, RTHandle dest)
        {
            _distortedUvBufferRTHandle = distortedUvBufferRTHandle;
            _dest = dest;
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            if (_material == null || renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                return;
            }

            var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _renderPassProfilingSampler))
            {
                cmd.SetGlobalTexture(
                    _applyDistortionTexPropertyId,
                    cameraColorTarget.nameID);
                cmd.SetGlobalTexture(
                    _distortionBufferPropertyId,
                    _distortedUvBufferRTHandle.nameID);
                Blit(cmd, _distortedUvBufferRTHandle, _dest, _material, 0);
                Blit(cmd, _dest.nameID, cameraColorTarget.nameID, null, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }
    }

    /// <summary>
    /// Draws every object carrying the configured distortion LightMode into the
    /// UV buffer, sharing the camera depth target and clearing only color.
    /// </summary>
    public sealed class DistortedUvBufferPass : ScriptableRenderPass
    {
        private const string ProfilerTag = "DistortedUvBufferPass";
        private readonly ProfilingSampler _profilingSampler;
        private readonly RenderQueueRange _renderQueueRange;
        private readonly ShaderTagId _shaderTagId;
        private FilteringSettings _filteringSettings;
        private RTHandle _renderTargetRTHandle;

        public DistortedUvBufferPass(string lightMode)
        {
            _profilingSampler = new ProfilingSampler(ProfilerTag);
            _renderQueueRange = RenderQueueRange.all;
            _filteringSettings = new FilteringSettings(_renderQueueRange);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            _shaderTagId = new ShaderTagId(lightMode);
        }

        public void Setup(RTHandle renderTargetRTHandle)
        {
            _renderTargetRTHandle = renderTargetRTHandle;
        }

        public override void OnCameraSetup(
            CommandBuffer cmd,
            ref RenderingData renderingData)
        {
            ConfigureTarget(
                _renderTargetRTHandle,
                renderingData.cameraData.renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                var drawingSettings = CreateDrawingSettings(
                    _shaderTagId,
                    ref renderingData,
                    SortingCriteria.CommonTransparent);
                var rendererListParams = new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    _filteringSettings);
                var rendererList = context.CreateRendererList(ref rendererListParams);
                cmd.DrawRendererList(rendererList);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
