using System;
using UnityEngine;

namespace Sekai.Core.Live.CharacterWaterEye
{
    public interface IEyeMaterialPreset
    {
        Texture DistortionTex { get; }
        float DistortionTexTilingX { get; }
        float DistortionTexTilingY { get; }
        float DistortionScrollSpeed { get; }
        float DistortionScrollX { get; }
        float DistortionScrollY { get; }
        float DistortionFPS { get; }
        float DistortionIntensity { get; }
        float DistortionIntensityX { get; }
        float DistortionIntensityY { get; }
        float DistortionOffsetX { get; }
        float DistortionOffsetY { get; }
    }

    public interface IHighlightMaterialPreset : IEyeMaterialPreset
    {
    }
}

namespace Sekai.Core.Live
{
    using CharacterWaterEye;

    public sealed class WaterEyePreset : ScriptableObject
    {
        [Serializable]
        public sealed class BaseEyeMaterialData : IEyeMaterialPreset
        {
            [field: SerializeField] public Texture DistortionTex { get; set; }
            [field: SerializeField] public float DistortionTexTilingX { get; set; }
            [field: SerializeField] public float DistortionTexTilingY { get; set; }
            [field: SerializeField] public float DistortionScrollSpeed { get; set; }
            [field: SerializeField] public float DistortionScrollX { get; set; }
            [field: SerializeField] public float DistortionScrollY { get; set; }
            [field: SerializeField] public float DistortionFPS { get; set; }
            [field: SerializeField] public float DistortionIntensity { get; set; }
            [field: SerializeField] public float DistortionIntensityX { get; set; }
            [field: SerializeField] public float DistortionIntensityY { get; set; }
            [field: SerializeField] public float DistortionOffsetX { get; set; }
            [field: SerializeField] public float DistortionOffsetY { get; set; }
        }

        [Serializable]
        public sealed class HighlightEyeMaterialData : IHighlightMaterialPreset
        {
            [field: SerializeField] public Texture DistortionTex { get; set; }
            [field: SerializeField] public float DistortionTexTilingX { get; set; }
            [field: SerializeField] public float DistortionTexTilingY { get; set; }
            [field: SerializeField] public float DistortionScrollSpeed { get; set; }
            [field: SerializeField] public float DistortionScrollX { get; set; }
            [field: SerializeField] public float DistortionScrollY { get; set; }
            [field: SerializeField] public float DistortionFPS { get; set; }
            [field: SerializeField] public float DistortionIntensity { get; set; }
            [field: SerializeField] public float DistortionIntensityX { get; set; }
            [field: SerializeField] public float DistortionIntensityY { get; set; }
            [field: SerializeField] public float DistortionOffsetX { get; set; }
            [field: SerializeField] public float DistortionOffsetY { get; set; }
        }

        [SerializeField, HideInInspector]
        private bool isReadOnly;

        [SerializeField]
        private BaseEyeMaterialData baseEyeMaterial = new BaseEyeMaterialData();

        [SerializeField]
        private HighlightEyeMaterialData highlightEyeMaterial =
            new HighlightEyeMaterialData();

        public BaseEyeMaterialData BaseEyeMaterial => baseEyeMaterial;
        public HighlightEyeMaterialData HighlightEyeMaterial => highlightEyeMaterial;
        public bool IsReadOnly => isReadOnly;
    }
}
