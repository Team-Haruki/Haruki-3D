using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    public class SekaiTransparentReflectionRendererFeature : SekaiDrawObjectsRendererFeature
    {
        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (ShouldEnqueueReflectionPasses())
            {
                EnqueuePasses(Passes, renderer);
            }
        }

        internal static bool ShouldEnqueueReflectionPasses()
        {
            return !SekaiCharacterReflectionOffSettings.ExistCharacterReflection() &&
                !SekaiCharacterReflectionOffSettings.IsHidingAll &&
                !PlanarReflectionPass.Instance.EnablePlanarReflection;
        }
    }
}
