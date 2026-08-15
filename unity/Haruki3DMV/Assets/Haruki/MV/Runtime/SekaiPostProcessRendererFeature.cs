using Sekai.Rendering.PostPrcessV2;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Recovered feature scheduling for the main renderer's eighth feature.
    /// The official player enqueues this pass only when Sekai post-processing
    /// is enabled and configures an in-place camera-color target at event 550.
    /// </summary>
    public sealed class SekaiPostProcessRendererFeature : ScriptableRendererFeature
    {
        private SekaiPostProcessPass m_Pass;

        public SekaiPostProcessPass Pass => m_Pass;

        public override void Create()
        {
            m_Pass?.Dispose();
            m_Pass = new SekaiPostProcessPass();
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (m_Pass == null ||
                !SekaiRendererRuntime.TryGet(renderer, out var runtime) ||
                !runtime.Data.useSekaiPostProcess)
            {
                return;
            }
            renderer.EnqueuePass(m_Pass);
        }

        public override void SetupRenderPasses(
            ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            if (m_Pass == null ||
                !SekaiRendererRuntime.TryGet(renderer, out var runtime) ||
                !runtime.Data.useSekaiPostProcess)
            {
                return;
            }

            var cameraColor = renderer.cameraColorTargetHandle;
            m_Pass.Setup(
                RenderPassEvent.BeforeRenderingPostProcessing,
                runtime.Buffer,
                renderingData.cameraData.cameraTargetDescriptor,
                cameraColor,
                cameraColor);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            m_Pass = null;
        }
    }
}
