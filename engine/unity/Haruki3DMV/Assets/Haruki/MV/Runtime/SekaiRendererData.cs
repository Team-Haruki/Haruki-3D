using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Sekai.Rendering
{
    /// <summary>
    /// Serialized renderer-data type used by the official main and sub camera
    /// assets. URP's maintained UniversalRenderer supplies the stock camera
    /// lifecycle while the recovered Sekai renderer features install the
    /// game's MRT and composition passes.
    /// </summary>
    [ExcludeFromPreset]
    [Serializable]
    public sealed class SekaiRendererData : UniversalRendererData
    {
        [FormerlySerializedAs("useSekaiPostProcess")]
        public bool useSekaiPostProcess;

        [NonSerialized]
        private ScriptableRenderer _runtimeRenderer;

        protected override ScriptableRenderer Create()
        {
            if (_runtimeRenderer != null)
            {
                SekaiRendererRuntime.Unregister(_runtimeRenderer);
            }

            _runtimeRenderer = new UniversalRenderer(this);
            SekaiRendererRuntime.Register(_runtimeRenderer, this);
            return _runtimeRenderer;
        }

        private void OnDisable()
        {
            if (_runtimeRenderer == null)
            {
                return;
            }
            SekaiRendererRuntime.Unregister(_runtimeRenderer);
            _runtimeRenderer = null;
        }
    }

    internal static class SekaiRendererRuntime
    {
        private static readonly ConditionalWeakTable<ScriptableRenderer, Context> Contexts =
            new ConditionalWeakTable<ScriptableRenderer, Context>();

        internal sealed class Context : IDisposable
        {
            public Context(SekaiRendererData data)
            {
                Data = data;
                Buffer = new SekaiBuffer();
                BufferSetupPass = new SekaiBufferSetupPass(Buffer);
                StencilState = CreateStencilState(data.defaultStencilState);
            }

            public SekaiRendererData Data { get; }
            public SekaiBuffer Buffer { get; }
            public SekaiBufferSetupPass BufferSetupPass { get; }
            public StencilState StencilState { get; }
            public int StencilReference => Data.defaultStencilState.stencilReference;

            public void Dispose()
            {
                Buffer.ReleaseMRT();
            }
        }

        internal static void Register(ScriptableRenderer renderer, SekaiRendererData data)
        {
            if (renderer == null || data == null)
            {
                return;
            }
            Unregister(renderer);
            Contexts.Add(renderer, new Context(data));
        }

        internal static bool TryGet(ScriptableRenderer renderer, out Context context)
        {
            context = null;
            return renderer != null && Contexts.TryGetValue(renderer, out context);
        }

        internal static void Unregister(ScriptableRenderer renderer)
        {
            if (renderer == null || !Contexts.TryGetValue(renderer, out var context))
            {
                return;
            }
            Contexts.Remove(renderer);
            context.Dispose();
        }

        internal static StencilState CreateStencilState(StencilStateData data)
        {
            var state = StencilState.defaultValue;
            if (data == null)
            {
                return state;
            }
            state.enabled = data.overrideStencilState;
            state.SetCompareFunction(data.stencilCompareFunction);
            state.SetPassOperation(data.passOperation);
            state.SetFailOperation(data.failOperation);
            state.SetZFailOperation(data.zFailOperation);
            return state;
        }
    }

    internal sealed class SekaiBufferSetupPass : ScriptableRenderPass
    {
        private readonly SekaiBuffer _buffer;

        public SekaiBufferSetupPass(SekaiBuffer buffer)
        {
            _buffer = buffer;
            renderPassEvent = RenderPassEvent.BeforeRendering;
        }

        public override void OnCameraSetup(
            CommandBuffer cmd,
            ref RenderingData renderingData)
        {
            _buffer.AllocSekaiBufferHandles(renderingData.cameraData.cameraTargetDescriptor);
            _buffer.Setup(
                cmd,
                renderingData.cameraData.renderer.cameraDepthTargetHandle);
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
        }
    }
}
