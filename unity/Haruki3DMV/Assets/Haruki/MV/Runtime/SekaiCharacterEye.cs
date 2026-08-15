using System;
using UnityEngine;

namespace Sekai.Rendering
{
    [Serializable]
    public struct SekaiEyeTiling
    {
        public int TileX;
        public int TileY;
        public int Sample;

        public Vector2 GetTileOffset()
        {
            if (TileX <= 0 || TileY <= 0) return Vector2.zero;
            return new Vector2(
                (Sample % TileX) / (float)TileX,
                (Sample / TileX) / (float)TileY);
        }
    }

    [ExecuteAlways]
    public sealed class SekaiCharacterEye : MonoBehaviour
    {
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int LightInfluenceId = Shader.PropertyToID("_LightInfluence");
        private static readonly int HighlightInfluenceId =
            Shader.PropertyToID("_LightInfluenceForEyeHighlight");
        private static readonly int LeftEyeCloseId = Shader.PropertyToID("_IsLeftEyeClose");
        private static readonly int RightEyeCloseId = Shader.PropertyToID("_IsRightEyeClose");

        [Range(0f, 1f)]
        [SerializeField]
        private float lightInfluence = 1f;

        [Range(0f, 1f)]
        [SerializeField]
        private float lightInfluenceForEyeHighlight = 1f;

        [SerializeField]
        private Color tintColor = Color.white;

        [SerializeField]
        private Color emissionColor = new Color(0f, 0f, 0f, 1f);

        [SerializeField]
        private SekaiEyeTiling baseTiling;

        [SerializeField]
        private SekaiEyeTiling highlightTiling;

        [SerializeField]
        private float leftEyeCloseBlendShapeValue;

        [SerializeField]
        private float rightEyeCloseBlendShapeValue;

        private Material _baseMaterial;
        private Material _highlightMaterial;
        private Material _eyelashMaterial;

        public void Setup()
        {
            ResolveMaterials();
            Apply();
        }

        private void OnEnable()
        {
            Setup();
        }

        private void Update()
        {
            Apply();
        }

        private void ResolveMaterials()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    if (material == null) continue;
                    var shaderName = material.shader != null ? material.shader.name : string.Empty;
                    if (shaderName == "Sekai/Live/Character/Eye-Base" ||
                        material.name.StartsWith("mtl_chr_eye_", StringComparison.Ordinal))
                        _baseMaterial = material;
                    else if (shaderName == "Sekai/Live/Character/Eye-Highlight" ||
                        material.name.StartsWith("mtl_chr_ehl_", StringComparison.Ordinal))
                        _highlightMaterial = material;
                    else if (material.name.StartsWith("mtl_chr_Eyelash_", StringComparison.Ordinal))
                        _eyelashMaterial = material;
                }
            }
        }

        private void Apply()
        {
            if (_baseMaterial == null && _highlightMaterial == null) ResolveMaterials();
            ApplyTiling(baseTiling, _baseMaterial);
            ApplyTiling(highlightTiling, _highlightMaterial);
            SetColor(_baseMaterial, TintColorId, tintColor);
            SetColor(_highlightMaterial, TintColorId, tintColor);
            SetColor(_baseMaterial, EmissionColorId, emissionColor);
            SetColor(_highlightMaterial, EmissionColorId, emissionColor);
            SetFloat(_baseMaterial, LightInfluenceId, lightInfluence);
            SetFloat(_highlightMaterial, HighlightInfluenceId, lightInfluenceForEyeHighlight);
            SetFloat(_eyelashMaterial, LeftEyeCloseId, leftEyeCloseBlendShapeValue);
            SetFloat(_eyelashMaterial, RightEyeCloseId, rightEyeCloseBlendShapeValue);
        }

        private static void ApplyTiling(SekaiEyeTiling tiling, Material material)
        {
            if (material == null || tiling.TileX <= 0 || tiling.TileY <= 0) return;
            material.mainTextureScale = new Vector2(1f / tiling.TileX, 1f / tiling.TileY);
            material.mainTextureOffset = tiling.GetTileOffset();
        }

        private static void SetColor(Material material, int property, Color value)
        {
            if (material != null && material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, int property, float value)
        {
            if (material != null && material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
