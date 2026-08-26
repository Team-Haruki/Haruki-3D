using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Recovered draw pass used by the Sekai renderer's opaque, transparent,
    /// reflection, music-item, and eyelash feature families.
    /// </summary>
    public class SekaiDrawObjectsPass : ScriptableRenderPass
    {
        private static readonly int DrawObjectPassDataPropertyId =
            Shader.PropertyToID("_DrawObjectPassData");
        private static readonly int ScaleBiasRtPropertyId =
            Shader.PropertyToID("_ScaleBiasRt");

        private FilteringSettings m_FilteringSettings;
        private RenderStateBlock m_RenderStateBlock;
        private readonly List<ShaderTagId> m_ShaderTagIdList;
        private readonly ProfilingSampler m_ProfilingSampler;
        private readonly bool m_IsOpaque;
        private SekaiBuffer m_Buffer;

        public SekaiDrawObjectsPass(
            string profilerTag,
            ShaderTagId[] shaderTagIds,
            bool opaque,
            RenderPassEvent evt,
            RenderQueueRange renderQueueRange)
        {
            profilingSampler = new ProfilingSampler(nameof(SekaiDrawObjectsPass));
            m_ShaderTagIdList = new List<ShaderTagId>();
            m_ProfilingSampler = new ProfilingSampler(profilerTag);
            if (shaderTagIds != null)
            {
                m_ShaderTagIdList.AddRange(shaderTagIds);
            }
            renderPassEvent = evt;
            m_FilteringSettings = new FilteringSettings(renderQueueRange);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_IsOpaque = opaque;
        }

        public SekaiDrawObjectsPass(
            string profilerTag,
            SekaiShaderTagType shaderTagType,
            bool opaque,
            RenderPassEvent evt,
            RenderQueueRange renderQueueRange)
            : this(
                profilerTag,
                new[] { new ShaderTagId(SekaiShaderTag.GetTag(shaderTagType)) },
                opaque,
                evt,
                renderQueueRange)
        {
        }

        internal void Setup(
            LayerMask layerMask,
            StencilState stencilState,
            int stencilReference,
            SekaiBuffer buffer)
        {
            m_FilteringSettings.layerMask = layerMask;
            m_Buffer = buffer;
            if (stencilState.enabled)
            {
                m_RenderStateBlock.stencilReference = stencilReference;
                m_RenderStateBlock.mask = RenderStateMask.Stencil;
                m_RenderStateBlock.stencilState = stencilState;
            }

            if (!m_IsOpaque)
            {
                ConfigureColorStoreAction(RenderBufferStoreAction.Store);
                ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            }
        }

        public override void Configure(
            CommandBuffer cmd,
            RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (m_Buffer == null)
            {
                return;
            }

            m_Buffer.AllocSekaiBufferHandles(cameraTextureDescriptor);
            ConfigureTarget(m_Buffer.SekaiBufferHandles, m_Buffer.DepthAttachment);
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                cmd.SetGlobalVector(
                    DrawObjectPassDataPropertyId,
                    DrawObjectPassData(m_IsOpaque));
                cmd.SetGlobalVector(
                    ScaleBiasRtPropertyId,
                    ScaleBias(renderingData.cameraData.IsCameraProjectionMatrixFlipped()));

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sorting = m_IsOpaque
                    ? renderingData.cameraData.defaultOpaqueSortFlags
                    : SortingCriteria.CommonTransparent;
                var drawingSettings = RenderingUtils.CreateDrawingSettings(
                    m_ShaderTagIdList,
                    ref renderingData,
                    sorting);
                context.DrawRenderers(
                    renderingData.cullResults,
                    ref drawingSettings,
                    ref m_FilteringSettings,
                    ref m_RenderStateBlock);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        internal static Vector4 DrawObjectPassData(bool opaque)
        {
            return new Vector4(0f, 0f, 0f, opaque ? 1f : 0f);
        }

        internal static Vector4 ScaleBias(bool projectionMatrixFlipped)
        {
            return projectionMatrixFlipped
                ? new Vector4(-1f, 1f, -1f, 1f)
                : new Vector4(1f, 0f, 1f, 1f);
        }
    }
}
