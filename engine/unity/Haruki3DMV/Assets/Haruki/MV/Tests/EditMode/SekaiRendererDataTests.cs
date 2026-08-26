using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering.Tests
{
    public sealed class SekaiRendererDataTests
    {
        [Test]
        public void RendererDataPreservesRecoveredTypeAndPostProcessFlag()
        {
            var data = ScriptableObject.CreateInstance<SekaiRendererData>();
            try
            {
                Assert.That(data, Is.InstanceOf<UniversalRendererData>());
                Assert.That(data.useSekaiPostProcess, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void RuntimeStencilStateUsesRendererDataFields()
        {
            var settings = new StencilStateData
            {
                overrideStencilState = true,
                stencilReference = 37,
                stencilCompareFunction = CompareFunction.Equal,
                passOperation = StencilOp.Replace,
                failOperation = StencilOp.Zero,
                zFailOperation = StencilOp.Invert,
            };

            var method = typeof(SekaiRendererData).Assembly
                .GetType("Sekai.Rendering.SekaiRendererRuntime")
                ?.GetMethod("CreateStencilState", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var state = (StencilState)method.Invoke(null, new object[] { settings });

            Assert.That(state.enabled, Is.True);
            Assert.That(state.compareFunctionFront, Is.EqualTo(CompareFunction.Equal));
            Assert.That(state.passOperationFront, Is.EqualTo(StencilOp.Replace));
            Assert.That(state.failOperationFront, Is.EqualTo(StencilOp.Zero));
            Assert.That(state.zFailOperationFront, Is.EqualTo(StencilOp.Invert));
        }
    }
}
