using System.Collections.Generic;
using Sekai.Rendering;
using UnityEngine;

namespace Haruki.MV
{
    public sealed class MvStageFeatureState : MonoBehaviour
    {
        public const string PlanarReflectionKeyword = "_USE_PLANAR_REFLECTION";
        private static readonly List<MvStageFeatureState> ActiveStages =
            new List<MvStageFeatureState>();
        private static int _distortionUsers;
        private bool _registered;

        public bool HeightFogEnabled { get; private set; }
        public bool PlanarReflectionEnabled { get; private set; }
        public bool PlanarReflectionSortingEnabled { get; private set; }
        public bool EffectDistortionEnabled { get; private set; }
        public static bool IsEffectDistortionEnabled => _distortionUsers > 0;

        public void Configure(MvResolvedStageInfo info)
        {
            Configure(
                info.EnableHeightFog,
                info.EnablePlanarReflection,
                info.EnablePlanarReflectionSorting,
                info.EnableEffectDistortion);
        }

        public void Configure(
            bool enableHeightFog,
            bool enablePlanarReflection,
            bool enablePlanarReflectionSorting,
            bool enableEffectDistortion)
        {
            Release();
            HeightFogEnabled = enableHeightFog;
            PlanarReflectionEnabled = enablePlanarReflection;
            PlanarReflectionSortingEnabled =
                enablePlanarReflection && enablePlanarReflectionSorting;
            EffectDistortionEnabled = enableEffectDistortion;
            if (EffectDistortionEnabled) _distortionUsers++;
            _registered = true;
            ActiveStages.Remove(this);
            ActiveStages.Add(this);
            ApplyPlanarReflectionState();
            ApplyEffectDistortionState();
        }

        public void Release()
        {
            if (!_registered)
            {
                return;
            }
            if (EffectDistortionEnabled) _distortionUsers--;
            _registered = false;
            ActiveStages.Remove(this);
            ApplyPlanarReflectionState();
            ApplyEffectDistortionState();
        }

        private static void ApplyPlanarReflectionState()
        {
            var pass = PlanarReflectionPass.Instance;
            if (ActiveStages.Count == 0)
            {
                pass.EnablePlanarReflection = false;
                pass.EnableObjectTransparentSorting = false;
                pass.SetShaderEnableKeyword(false);
                return;
            }

            var active = ActiveStages[ActiveStages.Count - 1];
            pass.EnablePlanarReflection = active.PlanarReflectionEnabled;
            if (active.PlanarReflectionEnabled)
            {
                pass.EnableObjectTransparentSorting =
                    active.PlanarReflectionSortingEnabled;
            }
            pass.SetShaderEnableKeyword(active.PlanarReflectionEnabled);
        }

        private static void ApplyEffectDistortionState()
        {
            EffectDistortionManager.Instance.EnableUseEffectDistortion =
                _distortionUsers > 0;
        }

        private void OnDestroy() => Release();
    }
}
