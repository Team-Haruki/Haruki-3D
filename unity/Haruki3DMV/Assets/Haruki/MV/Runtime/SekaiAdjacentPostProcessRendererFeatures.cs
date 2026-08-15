using System;
using System.Collections.Generic;
using Haruki.MV;
using Sekai.Rendering.Components;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Draws the MeshFlarePara light mode immediately before the main Sekai
    /// post-process pass. The recovered settings constructor stores event 550
    /// and transparent queue type 1.
    /// </summary>
    public sealed class SekaiBeforePostProcessRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class SekaiBeforePostProcessSettings
        {
            public RenderPassEvent Event = RenderPassEvent.BeforeRenderingPostProcessing;
            public SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType
                RenderQueueType =
                    SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.transparent;
            public bool UseCustomRenderQueueRange;
            public int RenderQueueLowerBound;
            public int RenderQueueUpperBound;
        }

        [SerializeField]
        private SekaiBeforePostProcessSettings m_BeforePostProcessSettings =
            new SekaiBeforePostProcessSettings();

        private SekaiMeshFlareParaPass m_MeshFlareParaPass;

        public SekaiMeshFlareParaPass Pass => m_MeshFlareParaPass;

        public override void Create()
        {
            m_MeshFlareParaPass = new SekaiMeshFlareParaPass(
                name,
                m_BeforePostProcessSettings.Event,
                QueueRange(m_BeforePostProcessSettings),
                m_BeforePostProcessSettings.RenderQueueType ==
                    SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.opaque);
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (m_MeshFlareParaPass != null &&
                SekaiRendererRuntime.TryGet(renderer, out _))
            {
                renderer.EnqueuePass(m_MeshFlareParaPass);
            }
        }

        public override void SetupRenderPasses(
            ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            if (m_MeshFlareParaPass == null ||
                !SekaiRendererRuntime.TryGet(renderer, out var runtime))
            {
                return;
            }

            var opaque = m_BeforePostProcessSettings.RenderQueueType ==
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.opaque;
            m_MeshFlareParaPass.SetupRecovered(
                opaque ? runtime.Data.opaqueLayerMask : runtime.Data.transparentLayerMask,
                runtime.StencilState,
                runtime.StencilReference,
                runtime.Buffer);
        }

        private static RenderQueueRange QueueRange(
            SekaiBeforePostProcessSettings settings)
        {
            if (settings.UseCustomRenderQueueRange)
            {
                return new RenderQueueRange(
                    settings.RenderQueueLowerBound,
                    settings.RenderQueueUpperBound);
            }
            return settings.RenderQueueType ==
                    SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.opaque
                ? RenderQueueRange.opaque
                : RenderQueueRange.transparent;
        }
    }

    public sealed class SekaiMeshFlareParaPass : ScriptableRenderPass
    {
        private static readonly int DrawObjectPassDataPropertyId =
            Shader.PropertyToID("_DrawObjectPassData");
        private static readonly int ScaleBiasRtPropertyId =
            Shader.PropertyToID("_ScaleBiasRt");

        private FilteringSettings _filteringSettings;
        private RenderStateBlock _renderStateBlock;
        private readonly List<ShaderTagId> _shaderTagIds;
        private readonly ProfilingSampler _profilingSampler;
        private readonly bool _isOpaque;
        private SekaiBuffer _buffer;

        public SekaiMeshFlareParaPass(
            string profilerTag,
            RenderPassEvent evt,
            RenderQueueRange renderQueueRange,
            bool opaque)
        {
            profilingSampler = new ProfilingSampler(nameof(SekaiMeshFlareParaPass));
            _profilingSampler = new ProfilingSampler(profilerTag);
            _shaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId(SekaiShaderTag.GetTag(SekaiShaderTagType.MeshFlarePara)),
            };
            _filteringSettings = new FilteringSettings(renderQueueRange);
            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _isOpaque = opaque;
            renderPassEvent = evt;
        }

        internal void SetupRecovered(
            LayerMask layerMask,
            StencilState stencilState,
            int stencilReference,
            SekaiBuffer buffer)
        {
            _filteringSettings.layerMask = layerMask;
            _buffer = buffer;
            if (stencilState.enabled)
            {
                _renderStateBlock.stencilReference = stencilReference;
                _renderStateBlock.mask = RenderStateMask.Stencil;
                _renderStateBlock.stencilState = stencilState;
            }

            if (!_isOpaque)
            {
                ConfigureColorStoreAction(RenderBufferStoreAction.Store);
                ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            }
        }

        public override void Configure(
            CommandBuffer cmd,
            RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (_buffer == null)
            {
                return;
            }

            _buffer.AllocSekaiBufferHandles(cameraTextureDescriptor);
            // The official MeshFlarePara pass is deliberately separate from
            // SekaiDrawObjectsPass and targets only the color attachment. Its
            // shader has one SV_Target output; binding the complete Sekai MRT
            // is invalid in WebGL and produces GL_INVALID_OPERATION.
            ConfigureTarget(_buffer.SekaiBufferColorHandle, _buffer.DepthAttachment);
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                cmd.SetGlobalVector(
                    DrawObjectPassDataPropertyId,
                    SekaiDrawObjectsPass.DrawObjectPassData(_isOpaque));
                cmd.SetGlobalVector(
                    ScaleBiasRtPropertyId,
                    SekaiDrawObjectsPass.ScaleBias(
                        renderingData.cameraData.IsCameraProjectionMatrixFlipped()));

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sorting = _isOpaque
                    ? renderingData.cameraData.defaultOpaqueSortFlags
                    : SortingCriteria.CommonTransparent;
                var drawingSettings = RenderingUtils.CreateDrawingSettings(
                    _shaderTagIds,
                    ref renderingData,
                    sorting);
                context.DrawRenderers(
                    renderingData.cullResults,
                    ref drawingSettings,
                    ref _filteringSettings,
                    ref _renderStateBlock);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }

    /// <summary>
    /// Draws the camera-decoration layer after post processing, or moves it to
    /// event 500 when the Timeline gate says that the decoration itself must
    /// participate in the main post-process chain.
    /// </summary>
    public class SekaiAfterPostProcessRendererFeatureBase : ScriptableRendererFeature
    {
        [SerializeField]
        private LayerMask _targetLayer = 1 << MvRecoveredRendererFactory.CameraDecorationLayer;

        [SerializeField]
        private Shader _fadeOutBlendShader;

        private Material _fadeOutBlendMaterial;
        private SekaiAfterPostProcessRenderPass _pass;

        public SekaiAfterPostProcessRenderPass Pass => _pass;

        public override void Create()
        {
            CoreUtils.Destroy(_fadeOutBlendMaterial);
            _fadeOutBlendMaterial = _fadeOutBlendShader == null
                ? null
                : CoreUtils.CreateEngineMaterial(_fadeOutBlendShader);
            _pass = new SekaiAfterPostProcessRenderPass(
                name,
                _targetLayer,
                _fadeOutBlendMaterial);
        }

        public void ConfigureRecovered(LayerMask targetLayer, Shader fadeOutBlendShader)
        {
            _targetLayer = targetLayer;
            _fadeOutBlendShader = fadeOutBlendShader;
        }

        public RenderPassEvent ResolveRenderPassEvent(bool enablePostProcess)
        {
            return enablePostProcess
                ? RenderPassEvent.AfterRenderingTransparents
                : RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (_pass == null || !SekaiRendererRuntime.TryGet(renderer, out _))
            {
                return;
            }
            _pass.ChangeRenderPassEvent(
                EnablePostEffectToCameraDecoration.EnablePostEffect);
            renderer.EnqueuePass(_pass);
        }

        public override void SetupRenderPasses(
            ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            if (_pass == null ||
                !SekaiRendererRuntime.TryGet(renderer, out var runtime))
            {
                return;
            }
            _pass.Setup(
                renderer.cameraColorTargetHandle,
                renderer.cameraDepthTargetHandle,
                runtime.Buffer);
        }

        protected override void Dispose(bool disposing)
        {
            _pass = null;
            CoreUtils.Destroy(_fadeOutBlendMaterial);
            _fadeOutBlendMaterial = null;
        }
    }

    public sealed class SekaiAfterPostProcessRenderPass : ScriptableRenderPass
    {
        private static readonly int DrawObjectPassDataPropertyId =
            Shader.PropertyToID("_DrawObjectPassData");

        private readonly ShaderTagId _shaderTagId = new ShaderTagId("SRPDefaultUnlit");
        private readonly FilteringSettings _opaqueFilteringSettings;
        private readonly FilteringSettings _transparentFilteringSettings;
        private RenderStateBlock _renderStateBlock =
            new RenderStateBlock(RenderStateMask.Nothing);
        private readonly Material _fadeOutBlendMaterial;
        private RTHandle _destColorHandle;
        private RTHandle _destDepthHandle;
        private SekaiBuffer _buffer;
        private bool _isEnablePostProcess;

        public SekaiAfterPostProcessRenderPass(
            string profilerId,
            LayerMask targetLayer,
            Material fadeOutBlendMaterial)
        {
            profilingSampler = new ProfilingSampler(profilerId);
            _opaqueFilteringSettings = new FilteringSettings(
                RenderQueueRange.opaque,
                targetLayer);
            _transparentFilteringSettings = new FilteringSettings(
                RenderQueueRange.transparent,
                targetLayer);
            _fadeOutBlendMaterial = fadeOutBlendMaterial;
            ChangeRenderPassEvent(false);
        }

        public void Setup(
            RTHandle destColorHandle,
            RTHandle destDepthHandle,
            SekaiBuffer buffer)
        {
            _destColorHandle = destColorHandle;
            _destDepthHandle = destDepthHandle;
            _buffer = buffer;
        }

        public void ChangeRenderPassEvent(bool isEnablePostProcess)
        {
            _isEnablePostProcess = isEnablePostProcess;
            renderPassEvent = isEnablePostProcess
                ? RenderPassEvent.AfterRenderingTransparents
                : RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void Configure(
            CommandBuffer cmd,
            RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (_destColorHandle != null && _destDepthHandle != null)
            {
                ConfigureTarget(_destColorHandle, _destDepthHandle);
            }
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            if (_destColorHandle == null || _buffer == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // The official gate changes scheduling. FadeOut blending remains
                // shader-owned; no substitute pixels are generated if the
                // recovered material is unavailable.
                DrawContext(cmd, context, ref renderingData, true);
                DrawContext(cmd, context, ref renderingData, false);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void DrawContext(
            CommandBuffer cmd,
            ScriptableRenderContext context,
            ref RenderingData renderingData,
            bool isOpaque)
        {
            cmd.SetGlobalVector(
                DrawObjectPassDataPropertyId,
                new Vector4(0f, 0f, 0f, isOpaque ? 1f : 0f));
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var drawingSettings = CreateDrawingSettings(
                _shaderTagId,
                ref renderingData,
                isOpaque
                    ? renderingData.cameraData.defaultOpaqueSortFlags
                    : SortingCriteria.CommonTransparent);
            var filtering = isOpaque
                ? _opaqueFilteringSettings
                : _transparentFilteringSettings;
            context.DrawRenderers(
                renderingData.cullResults,
                ref drawingSettings,
                ref filtering,
                ref _renderStateBlock);
        }
    }
}

namespace Sekai.Rendering.Components
{
    [ExecuteAlways]
    public sealed class EnablePostEffectToCameraDecoration : MonoBehaviour
    {
        public static bool EnablePostEffect { get; set; }
    }
}
