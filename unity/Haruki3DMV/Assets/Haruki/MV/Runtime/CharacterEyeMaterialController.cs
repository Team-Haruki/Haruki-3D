using System;
using System.Collections.Generic;
using Sekai.Core.Live.CharacterWaterEye;
using UnityEngine;

namespace Sekai.Scripts.Live.Character
{
    public sealed class CharacterEyeMaterialController : MonoBehaviour
    {
        private sealed class Target
        {
            public Renderer Renderer;
            public int MaterialIndex;
        }

        private static readonly int ApplyDistortionId =
            Shader.PropertyToID("_ApplyDistortionTex");
        private static readonly int DistortionTexId = Shader.PropertyToID("_DistortionTex");
        private static readonly int TilingXId = Shader.PropertyToID("_DistortionTexTilingX");
        private static readonly int TilingYId = Shader.PropertyToID("_DistortionTexTilingY");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_DistortionScrollSpeed");
        private static readonly int ScrollXId = Shader.PropertyToID("_DistortionScrollX");
        private static readonly int ScrollYId = Shader.PropertyToID("_DistortionScrollY");
        private static readonly int FpsId = Shader.PropertyToID("_DistortionFPS");
        private static readonly int IntensityId = Shader.PropertyToID("_DistortionIntensity");
        private static readonly int IntensityXId = Shader.PropertyToID("_DistortionIntensityX");
        private static readonly int IntensityYId = Shader.PropertyToID("_DistortionIntensityY");
        private static readonly int OffsetXId = Shader.PropertyToID("_DistortionOffsetX");
        private static readonly int OffsetYId = Shader.PropertyToID("_DistortionOffsetY");

        private readonly List<Target> _baseTargets = new List<Target>();
        private readonly List<Target> _highlightTargets = new List<Target>();
        private IEyeMaterialPreset _basePreset;
        private IHighlightMaterialPreset _highlightPreset;

        public void Setup(GameObject characterRoot)
        {
            if (characterRoot == null) throw new ArgumentNullException(nameof(characterRoot));
            _baseTargets.Clear();
            _highlightTargets.Clear();
            foreach (var renderer in characterRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    var material = materials[index];
                    if (material == null) continue;
                    var shaderName = material.shader != null ? material.shader.name : string.Empty;
                    if (shaderName == "Sekai/Live/Character/Eye-Base" ||
                        material.name.StartsWith("mtl_chr_eye_", StringComparison.OrdinalIgnoreCase))
                    {
                        _baseTargets.Add(new Target { Renderer = renderer, MaterialIndex = index });
                    }
                    else if (shaderName == "Sekai/Live/Character/Eye-Highlight" ||
                        material.name.StartsWith("mtl_chr_ehl_", StringComparison.OrdinalIgnoreCase))
                    {
                        _highlightTargets.Add(
                            new Target { Renderer = renderer, MaterialIndex = index });
                    }
                }
            }
        }

        public void ApplyBaseEyePreset(IEyeMaterialPreset preset)
        {
            _basePreset = preset;
            Apply(_baseTargets, preset, true);
        }

        public void ApplyHighlightEyePreset(IHighlightMaterialPreset preset)
        {
            _highlightPreset = preset;
            Apply(_highlightTargets, preset, true);
        }

        public void Enable()
        {
            Apply(_baseTargets, _basePreset, true);
            Apply(_highlightTargets, _highlightPreset, true);
        }

        public void Disable()
        {
            SetEnabled(_baseTargets, false);
            SetEnabled(_highlightTargets, false);
        }

        private static void Apply(
            IReadOnlyList<Target> targets,
            IEyeMaterialPreset preset,
            bool enabled)
        {
            if (preset == null) return;
            foreach (var target in targets)
            {
                var block = new MaterialPropertyBlock();
                target.Renderer.GetPropertyBlock(block, target.MaterialIndex);
                block.SetFloat(ApplyDistortionId, enabled ? 1f : 0f);
                block.SetTexture(DistortionTexId, preset.DistortionTex);
                block.SetFloat(TilingXId, preset.DistortionTexTilingX);
                block.SetFloat(TilingYId, preset.DistortionTexTilingY);
                block.SetFloat(ScrollSpeedId, preset.DistortionScrollSpeed);
                block.SetFloat(ScrollXId, preset.DistortionScrollX);
                block.SetFloat(ScrollYId, preset.DistortionScrollY);
                block.SetFloat(FpsId, preset.DistortionFPS);
                block.SetFloat(IntensityId, preset.DistortionIntensity);
                block.SetFloat(IntensityXId, preset.DistortionIntensityX);
                block.SetFloat(IntensityYId, preset.DistortionIntensityY);
                block.SetFloat(OffsetXId, preset.DistortionOffsetX);
                block.SetFloat(OffsetYId, preset.DistortionOffsetY);
                target.Renderer.SetPropertyBlock(block, target.MaterialIndex);
            }
        }

        private static void SetEnabled(IReadOnlyList<Target> targets, bool enabled)
        {
            foreach (var target in targets)
            {
                var block = new MaterialPropertyBlock();
                target.Renderer.GetPropertyBlock(block, target.MaterialIndex);
                block.SetFloat(ApplyDistortionId, enabled ? 1f : 0f);
                target.Renderer.SetPropertyBlock(block, target.MaterialIndex);
            }
        }
    }
}
