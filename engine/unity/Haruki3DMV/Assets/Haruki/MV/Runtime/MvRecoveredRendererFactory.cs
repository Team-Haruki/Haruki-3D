using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV
{
    /// <summary>
    /// Produces the renderer graphs captured from the 6.7.0 Main (index 5) and
    /// Sub (index 10) renderer assets. Persistence as Unity sub-assets is an
    /// editor/build concern; this factory owns only the runtime graph.
    /// </summary>
    public static class MvRecoveredRendererFactory
    {
        public const int CameraDecorationLayer = 31;

        public static SekaiRendererData CreateMainRendererData()
        {
            var data = CreateRendererData("SekaiMainRenderer", true);
            AddCommonFeatures(data);
            Add<SekaiBeforePostProcessRendererFeature>(data, "BeforePostProcess");
            Add<SekaiPostProcessRendererFeature>(data, "PostProcess");
            Add<SekaiCharacterOutlineFeature>(data, "SekaiCharacterOutlineFeature");
            AddDrawFeature<SekaiDrawObjectsRendererFeature>(
                data,
                "Eyelash",
                RenderPassEvent.AfterRenderingTransparents,
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.transparent,
                SekaiShaderTagType.Eyelash);
            Add<SekaiAfterTransparentRendererFeature>(
                data,
                "SekaiAfterTransparentRendererFeature");
            var afterPost = Add<SekaiAfterPostProcessRendererFeature>(
                data,
                "AfterPostProcess");
            afterPost.ConfigureRecovered(1 << CameraDecorationLayer, null);
            var reflection = Add<PlanarReflectionFeature>(
                data,
                "PlanarReflectionFeature");
            reflection.ConfigureRecovered(
                Shader.Find(MvRecoveredRendererContract.PlanarReflectionStencilShader),
                new PlanarReflectionInfo
                {
                    width = MvRecoveredRendererContract.PlanarReflectionWidth,
                    height = MvRecoveredRendererContract.PlanarReflectionHeight,
                    clipPlaneOffset = MvRecoveredRendererContract.PlanarReflectionClipPlaneOffset,
                    planeOffset = MvRecoveredRendererContract.PlanarReflectionPlaneOffset,
                });
            return data;
        }

        public static SekaiRendererData CreateSubRendererData()
        {
            var data = CreateRendererData("SekaiSubRenderer", false);
            AddCommonFeatures(data);
            Add<SekaiCharacterOutlineFeature>(data, "SekaiCharacterOutlineFeature");
            AddDrawFeature<SekaiDrawObjectsRendererFeature>(
                data,
                "Eyelash",
                RenderPassEvent.AfterRenderingTransparents,
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.transparent,
                SekaiShaderTagType.Eyelash);
            return data;
        }

        private static SekaiRendererData CreateRendererData(
            string name,
            bool usePostProcess)
        {
            var data = ScriptableObject.CreateInstance<SekaiRendererData>();
            data.name = name;
            data.useSekaiPostProcess = usePostProcess;
            data.opaqueLayerMask = ~0;
            data.transparentLayerMask = ~0;
            return data;
        }

        private static void AddCommonFeatures(SekaiRendererData data)
        {
            AddDrawFeature<SekaiDrawObjectsRendererFeature>(
                data,
                "OpaqueForward",
                RenderPassEvent.BeforeRenderingOpaques,
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.opaque,
                SekaiShaderTagType.Default);
            AddDrawFeature<SekaiDrawObjectsRendererFeature>(
                data,
                "OpaqueToon",
                RenderPassEvent.AfterRenderingOpaques,
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.opaque,
                SekaiShaderTagType.OpaqueOutline);
            AddDrawFeature<SekaiDrawObjectsRendererFeature>(
                data,
                "TransparentForward",
                RenderPassEvent.BeforeRenderingTransparents,
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.transparent,
                SekaiShaderTagType.TransparentBase,
                SekaiShaderTagType.TransparentOutline);
            AddDrawFeature<SekaiMusicItemRendererFeature>(
                data,
                "MusicItem",
                RenderPassEvent.BeforeRenderingTransparents,
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.transparent,
                SekaiShaderTagType.TransparentBase);
            AddDrawFeature<SekaiOpaqueReflectionRendererFeature>(
                data,
                "Opaque Reflection",
                RenderPassEvent.AfterRenderingOpaques,
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.opaque,
                SekaiShaderTagType.OpaqueReflection);
            AddDrawFeature<SekaiTransparentReflectionRendererFeature>(
                data,
                "TransparentReflection",
                RenderPassEvent.AfterRenderingTransparents,
                SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType.transparent,
                SekaiShaderTagType.TransparentReflection);
        }

        private static T AddDrawFeature<T>(
            SekaiRendererData data,
            string name,
            RenderPassEvent evt,
            SekaiDrawObjectsRendererFeature.SekaiDrawObjectsRenderQueueType queueType,
            params SekaiShaderTagType[] tags)
            where T : SekaiDrawObjectsRendererFeature
        {
            var feature = Add<T>(data, name);
            feature.ConfigureRecovered(evt, queueType, false, 0, 0, tags);
            return feature;
        }

        private static T Add<T>(SekaiRendererData data, string name)
            where T : ScriptableRendererFeature
        {
            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            data.rendererFeatures.Add(feature);
            return feature;
        }
    }
}
