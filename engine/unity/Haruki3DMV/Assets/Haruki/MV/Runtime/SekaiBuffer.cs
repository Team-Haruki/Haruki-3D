using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Recovered three-target MRT contract used by the Sekai renderer.
    /// Color, depth payload, and brightness are independent color attachments;
    /// the camera depth attachment is supplied separately by the renderer.
    /// </summary>
    public sealed class SekaiBuffer
    {
        internal enum BufferHandles
        {
            Color = 0,
            Depth = 1,
            Brightness = 2,
            Count = 3,
        }

        private static class RenderTargetNames
        {
            public const string Color = "_ColorBuffer";
            public const string Depth = "_DepthBuffer";
            public const string Brightness = "_BrightnessBuffer";
        }

        private readonly RTHandle[] sekaiBufferHandles;
        private RTHandle depthAttachment;

        internal RTHandle[] SekaiBufferHandles => sekaiBufferHandles;

        internal RTHandle SekaiBufferColorHandle =>
            sekaiBufferHandles[(int)BufferHandles.Color];

        internal RTHandle SekaiBufferDepthHandle =>
            sekaiBufferHandles[(int)BufferHandles.Depth];

        internal RTHandle SekaiBufferBrightnessHandle =>
            sekaiBufferHandles[(int)BufferHandles.Brightness];

        internal RTHandle DepthAttachment => depthAttachment;

        internal GraphicsFormat[] SekaiBufferHandleFormats { get; }

        public FilterMode[] BufferFilterModes { get; }

        public SekaiBuffer()
        {
            var format = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.R8G8B8A8_UNorm;
            SekaiBufferHandleFormats = new[] { format, format, format };
            BufferFilterModes = new[]
            {
                FilterMode.Point,
                FilterMode.Point,
                FilterMode.Point,
            };
            SetupBufferAttachments(out sekaiBufferHandles);
        }

        public void Setup(CommandBuffer cmd, RTHandle cameraDepthAttachment)
        {
            depthAttachment = cameraDepthAttachment;
            foreach (var handle in sekaiBufferHandles)
            {
                ClearRenderTarget(cmd, handle);
            }
        }

        public void AllocSekaiBufferHandles(RenderTextureDescriptor cameraTextureDescriptor)
        {
            for (var index = 0; index < sekaiBufferHandles.Length; index++)
            {
                var descriptor = ConfigureDescriptor(
                    cameraTextureDescriptor,
                    SekaiBufferHandleFormats[index]);
                RenderingUtils.ReAllocateIfNeeded(
                    ref sekaiBufferHandles[index],
                    descriptor,
                    BufferFilterModes[index],
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0f,
                    sekaiBufferHandles[index].name);
            }
        }

        private static RenderTextureDescriptor ConfigureDescriptor(
            RenderTextureDescriptor cameraTextureDescriptor,
            GraphicsFormat format)
        {
            cameraTextureDescriptor.depthBufferBits = 0;
            cameraTextureDescriptor.graphicsFormat = format;
            return cameraTextureDescriptor;
        }

        public void ReleaseMRT()
        {
            foreach (var handle in sekaiBufferHandles)
            {
                handle?.Release();
            }
        }

        private static void ClearRenderTarget(CommandBuffer cmd, RTHandle handle)
        {
            CoreUtils.SetRenderTarget(cmd, handle);
            cmd.ClearRenderTarget(false, true, Color.black);
        }

        private static void SetupBufferAttachments(out RTHandle[] handles)
        {
            handles = new RTHandle[(int)BufferHandles.Count];
            var names = new[]
            {
                RenderTargetNames.Color,
                RenderTargetNames.Depth,
                RenderTargetNames.Brightness,
            };
            for (var index = 0; index < names.Length; index++)
            {
                var target = new RenderTargetIdentifier(Shader.PropertyToID(names[index]));
                handles[index] = RTHandles.Alloc(target, names[index]);
            }
        }
    }
}
