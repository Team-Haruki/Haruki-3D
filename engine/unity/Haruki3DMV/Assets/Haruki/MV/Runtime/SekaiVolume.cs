using System;
using Sekai.Rendering.Components;
using Sekai.Rendering.Components.PostProcessV2;
using UnityEngine;
using UnityEngine.Rendering;
using ChromaticAberration = sekai_rendering.Runtime.Components.ChromaticAberration;

namespace Sekai.Rendering
{
    /// <summary>
    /// Per-camera volume container recovered from the 6.7.0 PostEffectV2
    /// runtime. Main and CutIn cameras must never share this profile.
    /// </summary>
    public sealed class SekaiVolume : IDisposable
    {
        private Volume _volume;
        private VolumeProfile _volumeProfile;

        public Vignette Vignette { get; private set; }
        public LightOverlay LightOverlay { get; private set; }
        public IncidentLight IncidentLight { get; private set; }
        public Saturation Saturation { get; private set; }
        public Solarisation Solarisation { get; private set; }
        public FadeOutBeforeProp FadeOutBeforeProp { get; private set; }
        public FadeOut FadeOut { get; private set; }
        public SaturationBlur SaturationBlur { get; private set; }
        public LegacyLut Lut { get; private set; }
        public LegacyBloom Bloom { get; private set; }
        public LegacyDof LegacyDof { get; private set; }
        public DirectionalBlur DirectionalBlur { get; private set; }
        public SekaiDof SekaiDof { get; private set; }
        public Antialiasing Antialiasing { get; private set; }
        public ScreenDistortion ScreenDistortion { get; private set; }
        public ChromaticAberration ChromaticAberration { get; private set; }

        public VolumeProfile Profile => _volumeProfile;

        public bool Enabled
        {
            get => _volume != null && _volume.enabled;
            set
            {
                if (_volume != null)
                {
                    _volume.enabled = value;
                }
            }
        }

        public void SetupVolume(GameObject target, string profileName)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (_volumeProfile != null)
            {
                throw new InvalidOperationException("SekaiVolume is already initialized.");
            }

            _volume = target.GetComponent<Volume>() ?? target.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1000f;
            _volume.weight = 1f;

            _volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volumeProfile.name = string.IsNullOrWhiteSpace(profileName)
                ? "SekaiPostEffect"
                : profileName;
            _volume.sharedProfile = _volumeProfile;

            Vignette = Add<Vignette>();
            LightOverlay = Add<LightOverlay>();
            IncidentLight = Add<IncidentLight>();
            Saturation = Add<Saturation>();
            Solarisation = Add<Solarisation>();
            FadeOutBeforeProp = Add<FadeOutBeforeProp>();
            FadeOut = Add<FadeOut>();
            SaturationBlur = Add<SaturationBlur>();
            Lut = Add<LegacyLut>();
            Bloom = Add<LegacyBloom>();
            LegacyDof = Add<LegacyDof>();
            DirectionalBlur = Add<DirectionalBlur>();
            SekaiDof = Add<SekaiDof>();
            Antialiasing = Add<Antialiasing>();
            ScreenDistortion = Add<ScreenDistortion>();
            ChromaticAberration = Add<ChromaticAberration>();
        }

        public void Dispose()
        {
            if (_volume != null && _volume.sharedProfile == _volumeProfile)
            {
                _volume.sharedProfile = null;
            }
            if (_volumeProfile != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_volumeProfile);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_volumeProfile);
                }
            }

            _volume = null;
            _volumeProfile = null;
            Vignette = null;
            LightOverlay = null;
            IncidentLight = null;
            Saturation = null;
            Solarisation = null;
            FadeOutBeforeProp = null;
            FadeOut = null;
            SaturationBlur = null;
            Lut = null;
            Bloom = null;
            LegacyDof = null;
            DirectionalBlur = null;
            SekaiDof = null;
            Antialiasing = null;
            ScreenDistortion = null;
            ChromaticAberration = null;
        }

        private T Add<T>() where T : VolumeComponent
        {
            var component = _volumeProfile.Add<T>(true);
            component.active = true;
            foreach (var parameter in component.parameters)
            {
                parameter.overrideState = true;
            }
            return component;
        }
    }
}
