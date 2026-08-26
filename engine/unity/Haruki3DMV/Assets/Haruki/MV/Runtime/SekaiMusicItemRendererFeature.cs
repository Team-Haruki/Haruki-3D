using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Enqueues the configured pass only while at least one visible,
    /// transparent music item is registered.
    /// </summary>
    public class SekaiMusicItemRendererFeature : SekaiDrawObjectsRendererFeature
    {
        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (SekaiMusicItemSettings.ExistTransparentMusicItem())
            {
                EnqueuePasses(Passes, renderer);
            }
        }
    }
}
