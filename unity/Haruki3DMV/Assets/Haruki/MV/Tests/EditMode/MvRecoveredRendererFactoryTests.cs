using System.Linq;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Haruki.MV.Tests
{
    public sealed class MvRecoveredRendererFactoryTests
    {
        [Test]
        public void MainRendererFactoryCreatesTheCapturedFeatureGraph()
        {
            var data = MvRecoveredRendererFactory.CreateMainRendererData();
            try
            {
                Assert.That(data.useSekaiPostProcess, Is.True);
                Assert.That(
                    data.rendererFeatures.Select(feature => feature.name),
                    Is.EqualTo(
                        MvRecoveredRendererContract.ForRenderer(
                                MvRecoveredCameraResources.MainRendererIndex)
                            .Select(feature => feature.Name)));
                Assert.That(
                    data.rendererFeatures.Select(feature => feature.GetType().Name),
                    Is.EqualTo(
                        MvRecoveredRendererContract.ForRenderer(
                                MvRecoveredCameraResources.MainRendererIndex)
                            .Select(feature => feature.TypeName)));
            }
            finally
            {
                DestroyRendererData(data);
            }
        }

        [Test]
        public void SubRendererFactoryCreatesOnlyTheCapturedSubGraph()
        {
            var data = MvRecoveredRendererFactory.CreateSubRendererData();
            try
            {
                Assert.That(data.useSekaiPostProcess, Is.False);
                Assert.That(data.rendererFeatures, Has.Count.EqualTo(8));
                Assert.That(
                    data.rendererFeatures.Select(feature => feature.name),
                    Is.EqualTo(
                        MvRecoveredRendererContract.ForRenderer(
                                MvRecoveredCameraResources.SubRendererIndex)
                            .Select(feature => feature.Name)));
            }
            finally
            {
                DestroyRendererData(data);
            }
        }

        [Test]
        public void RecoveredAdjacentPostProcessPassesUseTheCapturedEvents()
        {
            var before = ScriptableObject.CreateInstance<
                SekaiBeforePostProcessRendererFeature>();
            var after = ScriptableObject.CreateInstance<
                SekaiAfterPostProcessRendererFeature>();
            try
            {
                before.Create();
                after.Create();

                Assert.That(
                    before.Pass.renderPassEvent,
                    Is.EqualTo(RenderPassEvent.BeforeRenderingPostProcessing));
                Assert.That(
                    after.ResolveRenderPassEvent(false),
                    Is.EqualTo(RenderPassEvent.AfterRenderingPostProcessing));
                Assert.That(
                    after.ResolveRenderPassEvent(true),
                    Is.EqualTo(RenderPassEvent.AfterRenderingTransparents));
            }
            finally
            {
                Object.DestroyImmediate(before);
                Object.DestroyImmediate(after);
            }
        }

        private static void DestroyRendererData(SekaiRendererData data)
        {
            if (data == null)
            {
                return;
            }
            foreach (var feature in data.rendererFeatures)
            {
                Object.DestroyImmediate(feature);
            }
            Object.DestroyImmediate(data);
        }
    }
}
