using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Recovered pass factory and queueing contract shared by the official
    /// opaque, transparent, reflection, music-item, and eyelash features.
    /// </summary>
    public class SekaiDrawObjectsRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public enum SekaiDrawObjectsRenderQueueType
        {
            opaque = 0,
            transparent = 1,
        }

        [Serializable]
        protected class SekaiDrawObjectsSettings
        {
            public RenderPassEvent Event = RenderPassEvent.BeforeRenderingTransparents;
            public SekaiDrawObjectsRenderQueueType RenderQueueType;
            public bool UseCustomRenderQueueRange;
            public int RenderQueueLowerBound;
            public int RenderQueueUpperBound;
            public SekaiShaderTagType[] ShaderTagTypes =
            {
                SekaiShaderTagType.Default,
            };
        }

        [SerializeField]
        private SekaiDrawObjectsSettings m_Settings = new SekaiDrawObjectsSettings();

        private SekaiDrawObjectsPass[] m_Passes;

        protected SekaiDrawObjectsSettings Settings => m_Settings;

        protected SekaiDrawObjectsPass[] Passes => m_Passes;

        public void ConfigureRecovered(
            RenderPassEvent evt,
            SekaiDrawObjectsRenderQueueType renderQueueType,
            bool useCustomRenderQueueRange,
            int renderQueueLowerBound,
            int renderQueueUpperBound,
            params SekaiShaderTagType[] shaderTagTypes)
        {
            m_Settings.Event = evt;
            m_Settings.RenderQueueType = renderQueueType;
            m_Settings.UseCustomRenderQueueRange = useCustomRenderQueueRange;
            m_Settings.RenderQueueLowerBound = renderQueueLowerBound;
            m_Settings.RenderQueueUpperBound = renderQueueUpperBound;
            m_Settings.ShaderTagTypes = shaderTagTypes;
        }

        public override void Create()
        {
            CreatePasses(m_Settings, out m_Passes);
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (!SekaiRendererRuntime.TryGet(renderer, out var context) ||
                m_Passes == null)
            {
                return;
            }

            if (string.Equals(name, "OpaqueForward", StringComparison.Ordinal))
            {
                renderer.EnqueuePass(context.BufferSetupPass);
            }
            EnqueuePasses(m_Passes, renderer);
        }

        public override void SetupRenderPasses(
            ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            if (!SekaiRendererRuntime.TryGet(renderer, out var context))
            {
                return;
            }

            var layerMask = m_Settings.RenderQueueType ==
                SekaiDrawObjectsRenderQueueType.opaque
                ? context.Data.opaqueLayerMask
                : context.Data.transparentLayerMask;
            SetupPasses(
                layerMask,
                context.StencilState,
                context.StencilReference,
                context.Buffer);
        }

        protected void CreatePasses(
            in SekaiDrawObjectsSettings settings,
            out SekaiDrawObjectsPass[] passes)
        {
            if (settings == null ||
                settings.ShaderTagTypes == null ||
                settings.ShaderTagTypes.Length == 0)
            {
                passes = null;
                return;
            }

            var renderQueueRange = settings.UseCustomRenderQueueRange
                ? new RenderQueueRange(
                    settings.RenderQueueLowerBound,
                    settings.RenderQueueUpperBound)
                : settings.RenderQueueType == SekaiDrawObjectsRenderQueueType.transparent
                    ? RenderQueueRange.transparent
                    : RenderQueueRange.opaque;
            var opaque = settings.RenderQueueType == SekaiDrawObjectsRenderQueueType.opaque;
            passes = new SekaiDrawObjectsPass[settings.ShaderTagTypes.Length];
            for (var index = 0; index < settings.ShaderTagTypes.Length; index++)
            {
                passes[index] = new SekaiDrawObjectsPass(
                    name,
                    settings.ShaderTagTypes[index],
                    opaque,
                    settings.Event,
                    renderQueueRange);
            }
        }

        protected void EnqueuePasses(
            in SekaiDrawObjectsPass[] passes,
            ScriptableRenderer renderer)
        {
            if (passes == null ||
                !SekaiRendererRuntime.TryGet(renderer, out _))
            {
                return;
            }

            foreach (var pass in passes)
            {
                if (pass != null)
                {
                    renderer.EnqueuePass(pass);
                }
            }
        }

        /// <summary>
        /// Applies the values the official generic SetupRenderPass body reads
        /// from SekaiRenderer. The concrete renderer lifecycle will call this
        /// once its recovered renderer implementation is installed.
        /// </summary>
        protected void SetupPasses(
            LayerMask layerMask,
            StencilState stencilState,
            int stencilReference,
            SekaiBuffer buffer)
        {
            if (m_Passes == null)
            {
                return;
            }

            foreach (var pass in m_Passes)
            {
                pass?.Setup(layerMask, stencilState, stencilReference, buffer);
            }
        }
    }

}
