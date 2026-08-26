using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering.Components
{
    [Serializable]
    public class SekaiVolumeComponent : VolumeComponent
    {
        [SerializeField]
        protected bool isActive;

        public virtual bool IsActive() => isActive;

        public void SetActive(bool value) => isActive = value;
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/Antialiasing")]
    public sealed class Antialiasing : SekaiVolumeComponent
    {
        public AntialiasingModeParameter mode = new AntialiasingModeParameter(
            AntialiasingMode.SubpixelMorphologicalAntiAliasing);
        public AntialiasingQualityParameter antialiasingQuality =
            new AntialiasingQualityParameter(AntialiasingQuality.High);
    }

    [Serializable]
    public sealed class AntialiasingModeParameter : VolumeParameter<AntialiasingMode>
    {
        public AntialiasingModeParameter(
            AntialiasingMode value,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class AntialiasingQualityParameter : VolumeParameter<AntialiasingQuality>
    {
        public AntialiasingQualityParameter(
            AntialiasingQuality value,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/DirectionalBlur")]
    public sealed class DirectionalBlur : SekaiVolumeComponent
    {
        public IntParameter blurType = new IntParameter(0);
        public MinFloatParameter blurStrength = new MinFloatParameter(0f, 0f);
        public ClampedFloatParameter blurDirection =
            new ClampedFloatParameter(0f, -360f, 360f);
        public Vector2Parameter blurCenterPosition = new Vector2Parameter(Vector2.zero);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/FadeOut")]
    public sealed class FadeOut : SekaiVolumeComponent
    {
        // The recovered neutral value is 0.5 for both channels.
        public ClampedFloatParameter fadeOut = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter fadeOutLerp = new ClampedFloatParameter(0.5f, 0f, 1f);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/FadeOutBeforeProp")]
    public sealed class FadeOutBeforeProp : SekaiVolumeComponent
    {
        public ClampedFloatParameter fadeOutBeforeProp =
            new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter fadeOutBeforePropLerp =
            new ClampedFloatParameter(0.5f, 0f, 1f);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/Legacy/IncidentLight")]
    public sealed class IncidentLight : SekaiVolumeComponent
    {
        public BoolParameter incidentLightUse = new BoolParameter(false);
        public IntParameter incidentLightAddBlend = new IntParameter(0);
        public ColorParameter incidentLightColor = new ColorParameter(Color.clear);
        public Vector2Parameter incidentLightPosition = new Vector2Parameter(Vector2.zero);
        public FloatParameter incidentLightLength = new FloatParameter(0f);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/Bloom")]
    public sealed class LegacyBloom : SekaiVolumeComponent
    {
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1.5f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0f, 0f, 3f);
        public BoolParameter useNewBlend = new BoolParameter(false);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/Legacy/LegacyDof")]
    public sealed class LegacyDof : VolumeComponent
    {
        public BoolParameter enable = new BoolParameter(false);

        public void SetActive(bool value) => enable.value = value;
        public bool IsActive() => enable.value;
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/LUT")]
    public sealed class LegacyLut : SekaiVolumeComponent
    {
        public TextureParameter frontTex = new TextureParameter(null);
        public TextureParameter backTex = new TextureParameter(null);
        public FloatParameter frontBlend = new FloatParameter(0f);
        public FloatParameter backBlend = new FloatParameter(0f);
        public Vector2Parameter frontPoint = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter frontNonPoint = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter backPoint = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter backNonPoint = new Vector2Parameter(Vector2.zero);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/LightOverlay")]
    public sealed class LightOverlay : SekaiVolumeComponent
    {
        public ColorParameter brightColor = new ColorParameter(Color.clear);
        public ColorParameter darkColor = new ColorParameter(Color.clear);
        public Vector2Parameter brightPosition = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter darkPosition = new Vector2Parameter(Vector2.zero);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/Saturation")]
    public sealed class Saturation : SekaiVolumeComponent
    {
        public FloatParameter saturation = new FloatParameter(1f);
    }

    public enum SaturationBlurVolumeType
    {
        V1 = 0,
        V2 = 1,
    }

    [Serializable]
    public sealed class SaturationBlurTypeParameter : VolumeParameter<SaturationBlurVolumeType>
    {
        public SaturationBlurTypeParameter(
            SaturationBlurVolumeType value,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/SaturationBlur")]
    public sealed class SaturationBlur : SekaiVolumeComponent
    {
        public SaturationBlurTypeParameter saturationBlurType =
            new SaturationBlurTypeParameter(SaturationBlurVolumeType.V1);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedFloatParameter alpha = new ClampedFloatParameter(0f, 0f, 1f);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/ExtraPostProcess/ScreenDistortion")]
    public sealed class ScreenDistortion : SekaiVolumeComponent
    {
        public FloatParameter IntensityAmount = new FloatParameter(0f);
        public FloatParameter ScaleAmount = new FloatParameter(1f);
        public FloatParameter OffsetAmount = new FloatParameter(0f);
        public BoolParameter UseNoise = new BoolParameter(false);
        public Vector2Parameter NoiseScale = new Vector2Parameter(Vector2.one);
        public Vector2Parameter NoiseScrollSpeed = new Vector2Parameter(Vector2.zero);
        public FloatParameter Time = new FloatParameter(0f);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/SekaiDof")]
    public sealed class SekaiDof : SekaiVolumeComponent
    {
        public MinFloatParameter focusDistance = new MinFloatParameter(10f, 0f);
        public ClampedFloatParameter aperture = new ClampedFloatParameter(5.6f, 0f, 32f);
        public ClampedFloatParameter focalLength =
            new ClampedFloatParameter(50f, 0f, 300f);
        public BoolParameter disableForeBokeh = new BoolParameter(false);
    }

    [Serializable]
    [VolumeComponentMenu("Sekai/Solarisation")]
    public sealed class Solarisation : SekaiVolumeComponent
    {
        public ClampedFloatParameter solarisation = new ClampedFloatParameter(0f, 0f, 1f);
    }
}

namespace Sekai.Rendering.Components.PostProcessV2
{
    [Serializable]
    [VolumeComponentMenu("Sekai/Vignette")]
    public sealed class Vignette : Sekai.Rendering.Components.SekaiVolumeComponent
    {
        public ColorParameter color = new ColorParameter(Color.black);
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.2f, 0.01f, 1f);
        public FloatParameter roundness = new FloatParameter(1f);
    }
}

namespace sekai_rendering.Runtime.Components
{
    [Serializable]
    [VolumeComponentMenu("Sekai/ChromaticAberration")]
    public sealed class ChromaticAberration : Sekai.Rendering.Components.SekaiVolumeComponent
    {
        public IntParameter ChromaticAberrationMode = new IntParameter(0);
        public Vector2Parameter OffsetR = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter OffsetG = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter OffsetB = new Vector2Parameter(Vector2.zero);
        public FloatParameter ScaleR = new FloatParameter(1f);
        public FloatParameter ScaleG = new FloatParameter(1f);
        public FloatParameter ScaleB = new FloatParameter(1f);
    }
}
