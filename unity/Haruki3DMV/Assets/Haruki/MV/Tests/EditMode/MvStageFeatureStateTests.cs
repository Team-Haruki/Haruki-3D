using NUnit.Framework;
using Sekai.Core;
using Sekai.Rendering;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvStageFeatureStateTests
    {
        [Test]
        public void CharacterAdjusterUsesFormationMasterHeightInCentimeters()
        {
            var root = new GameObject("Stage");
            var target = new GameObject("Adjust");
            target.transform.SetParent(root.transform, false);
            var adjuster = target.AddComponent<CharacterAdjuster>();
            adjuster.FormationId = 1;
            try
            {
                CharacterAdjuster.AdjustGameObjects(
                    root,
                    new[]
                    {
                        new CharacterAdjuster.CharacterAdjustData(5, 158f),
                        new CharacterAdjuster.CharacterAdjustData(8, 168f),
                    });

                Assert.That(target.transform.localScale, Is.EqualTo(Vector3.one * 1.68f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StageFeatureStateAppliesConfirmedGatesAndSortingDependency()
        {
            var host = new GameObject("StageState");
            var state = host.AddComponent<MvStageFeatureState>();
            try
            {
                state.Configure(true, false, true, true);

                Assert.That(state.HeightFogEnabled, Is.True);
                Assert.That(state.PlanarReflectionEnabled, Is.False);
                Assert.That(state.PlanarReflectionSortingEnabled, Is.False);
                Assert.That(MvStageFeatureState.IsEffectDistortionEnabled, Is.True);
                Assert.That(
                    EffectDistortionManager.Instance.EnableUseEffectDistortion,
                    Is.True);
                Assert.That(
                    PlanarReflectionPass.Instance.EnablePlanarReflection,
                    Is.False);
            }
            finally
            {
                state.Release();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EffectDistortionUsesOfficialOrAggregationAcrossStages()
        {
            EffectDistortionManager.Dispose();
            var mainHost = new GameObject("MainDistortionState");
            var cutInHost = new GameObject("CutInDistortionState");
            var main = mainHost.AddComponent<MvStageFeatureState>();
            var cutIn = cutInHost.AddComponent<MvStageFeatureState>();
            try
            {
                main.Configure(false, false, false, true);
                cutIn.Configure(false, false, false, false);
                Assert.That(
                    EffectDistortionManager.Instance.EnableUseEffectDistortion,
                    Is.True);

                main.Release();
                Assert.That(
                    EffectDistortionManager.Instance.EnableUseEffectDistortion,
                    Is.False);
            }
            finally
            {
                cutIn.Release();
                main.Release();
                Object.DestroyImmediate(cutInHost);
                Object.DestroyImmediate(mainHost);
                EffectDistortionManager.Dispose();
            }
        }

        [Test]
        public void StageFeatureStateDrivesTheRecoveredPlanarReflectionPass()
        {
            var mainHost = new GameObject("MainStageState");
            var cutInHost = new GameObject("CutInStageState");
            var main = mainHost.AddComponent<MvStageFeatureState>();
            var cutIn = cutInHost.AddComponent<MvStageFeatureState>();
            var pass = PlanarReflectionPass.Instance;
            try
            {
                main.Configure(false, true, true, false);
                Assert.That(pass.EnablePlanarReflection, Is.True);
                Assert.That(pass.EnableObjectTransparentSorting, Is.True);

                cutIn.Configure(false, false, true, false);
                Assert.That(pass.EnablePlanarReflection, Is.False);
                Assert.That(pass.EnableObjectTransparentSorting, Is.True);

                cutIn.Release();
                Assert.That(pass.EnablePlanarReflection, Is.True);
                Assert.That(pass.EnableObjectTransparentSorting, Is.True);

                main.Release();
                Assert.That(pass.EnablePlanarReflection, Is.False);
                Assert.That(pass.EnableObjectTransparentSorting, Is.False);
            }
            finally
            {
                cutIn.Release();
                main.Release();
                Object.DestroyImmediate(cutInHost);
                Object.DestroyImmediate(mainHost);
            }
        }
    }
}
