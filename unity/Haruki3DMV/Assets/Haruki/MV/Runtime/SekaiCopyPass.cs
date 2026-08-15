using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Recovered direct RTHandle copy used by the Sekai renderer's internal
    /// composition path. The original pass performs no filtering or material
    /// processing: mip zero is copied with bilinear sampling disabled.
    /// </summary>
    public sealed class SekaiCopyPass : ScriptableRenderPass
    {
        private RTHandle m_Source;
        private RTHandle m_Dest;

        public SekaiCopyPass(string profilerTag)
        {
            profilingSampler = new ProfilingSampler(profilerTag);
        }

        public void Setup(RTHandle source, RTHandle dest)
        {
            m_Source = source;
            m_Dest = dest;
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                Blitter.BlitCameraTexture(
                    cmd,
                    m_Source,
                    m_Dest,
                    0f,
                    false);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
