using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Recovered UI draw pass used by the live RenderCanvas path. UI is drawn
    /// through SRPDefaultUnlit with the official transparent sort and optional
    /// stencil override, rather than through a host-side overlay substitute.
    /// </summary>
    public sealed class SekaiDrawUIPass : ScriptableRenderPass
    {
        private static readonly int DrawObjectPassDataPropertyId =
            Shader.PropertyToID("_DrawObjectPassData");
        private static readonly int ScreenParamsPropertyId =
            Shader.PropertyToID("_ScreenParams");

        private LayerMask m_LayerMask;
        private RenderStateBlock m_RenderStateBlock;
        private ShaderTagId m_ShaderTagId;
        private RTHandle m_ColorTargetHandle;
        private bool m_ClearRenderTarget;
        private RenderQueueRange m_RenderQueueRange;
        private RTHandle m_DepthTargetHandle;
        private bool m_IsCameraRenderTarget;

        public SekaiDrawUIPass(string profilerTag)
        {
            profilingSampler = new ProfilingSampler(profilerTag);
        }

        internal void Setup(
            LayerMask layerMask,
            StencilState stencilState,
            int stencilReference,
            RTHandle colorTargetHandle,
            RTHandle depthTargetHandle,
            bool isCameraRenderTarget,
            RenderQueueRange renderQueueRange)
        {
            m_LayerMask = layerMask;
            m_RenderQueueRange = renderQueueRange;
            m_ShaderTagId = new ShaderTagId(
                SekaiShaderTag.GetTag(SekaiShaderTagType.Default));
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_ColorTargetHandle = colorTargetHandle;
            m_DepthTargetHandle = depthTargetHandle;
            m_IsCameraRenderTarget = isCameraRenderTarget;

            if (stencilState.enabled)
            {
                m_RenderStateBlock.stencilReference = stencilReference;
                m_RenderStateBlock.mask = RenderStateMask.Stencil;
                m_RenderStateBlock.stencilState = stencilState;
            }
        }

        public override void OnCameraSetup(
            CommandBuffer cmd,
            ref RenderingData renderingData)
        {
            base.OnCameraSetup(cmd, ref renderingData);
            ConfigureColorStoreAction(RenderBufferStoreAction.Store);
            ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
        }

        public override void Configure(
            CommandBuffer cmd,
            RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
            ConfigureTarget(m_ColorTargetHandle, m_DepthTargetHandle);
            ConfigureClear(ClearFlag.Color, Color.black);
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                cmd.SetViewport(renderingData.cameraData.camera.pixelRect);
                cmd.SetGlobalVector(
                    DrawObjectPassDataPropertyId,
                    DrawObjectPassData(
                        renderingData.cameraData.IsCameraProjectionMatrixFlipped()));

                var width = (float)renderingData.cameraData.cameraTargetDescriptor.width;
                var height = (float)renderingData.cameraData.cameraTargetDescriptor.height;
                if (!m_IsCameraRenderTarget)
                {
                    width *= ScalableBufferManager.widthScaleFactor;
                    height *= ScalableBufferManager.heightScaleFactor;
                }
                cmd.SetGlobalVector(
                    ScreenParamsPropertyId,
                    ScreenParams(width, height));

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var drawingSettings = CreateDrawingSettings(
                    m_ShaderTagId,
                    ref renderingData,
                    SortingCriteria.CommonTransparent);
                var filteringSettings = new FilteringSettings(
                    m_RenderQueueRange,
                    m_LayerMask);
                ExecuteRenderUI(
                    context,
                    ref renderingData,
                    ref drawingSettings,
                    filteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        private void ExecuteRenderUI(
            ScriptableRenderContext context,
            ref RenderingData renderingData,
            ref DrawingSettings drawingSettings,
            FilteringSettings filteringSettings)
        {
            context.DrawRenderers(
                renderingData.cullResults,
                ref drawingSettings,
                ref filteringSettings,
                ref m_RenderStateBlock);
        }

        public void Cleanup()
        {
            m_ColorTargetHandle = null;
            m_DepthTargetHandle = null;
        }

        internal static Vector4 DrawObjectPassData(bool projectionMatrixFlipped)
        {
            return projectionMatrixFlipped
                ? new Vector4(-1f, 1f, -1f, 1f)
                : new Vector4(1f, 0f, 1f, 1f);
        }

        internal static Vector4 ScreenParams(float width, float height)
        {
            return new Vector4(
                width,
                height,
                1f + 1f / width,
                1f + 1f / height);
        }
    }
}
