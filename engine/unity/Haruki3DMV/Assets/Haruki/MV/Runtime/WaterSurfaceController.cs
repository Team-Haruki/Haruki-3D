using UnityEngine;

namespace Sekai.Rendering
{
    [ExecuteAlways]
    public sealed class WaterSurfaceController : MonoBehaviour
    {
        private Material _material;

        public float Smoothness { get; private set; }
        public float DistortionPower { get; private set; }
        public float DistortionDistance { get; private set; }
        public Vector2 WaveMoveSpeed { get; private set; }
        public Vector2 WaveNoiseSize { get; private set; }
        public Vector2 NormalNoiseSize { get; private set; }
        public float NormalPower { get; private set; }
        public float Brightness { get; private set; }
        public Vector3 LightDirection { get; private set; }
        public bool UseSecondaryNormal { get; private set; }
        public bool UsePolarUv { get; private set; }
        public Vector2 PolarCenterPosition { get; private set; }
        public float RadialScale { get; private set; }
        public float RadialSpreadSpeed { get; private set; }
        public float RotationSpeed { get; private set; }
        public float PolarCenterMaskSize { get; private set; }
        public float PolarCenterMaskFade { get; private set; }
        public float Time { get; private set; }

        public void UpdateSmoothnessParameters(float value)
        {
            Smoothness = value;
            SetFloat("_Smoothness", value);
        }

        public void UpdateDistortionParameters(float power, float distance, Vector2 moveSpeed)
        {
            DistortionPower = power;
            DistortionDistance = distance;
            WaveMoveSpeed = moveSpeed;
            SetFloat("_DistortionPower", power);
            SetFloat("_DistortionDistance", distance);
            SetFloat("_MoveSpeedX", moveSpeed.x);
            SetFloat("_MoveSpeedY", moveSpeed.y);
        }

        public void UpdateWaveParameters(
            Vector2 noiseSize,
            Vector2 normalNoiseSize,
            float normalPower,
            float brightness,
            Vector3 lightDirection)
        {
            WaveNoiseSize = noiseSize;
            NormalNoiseSize = normalNoiseSize;
            NormalPower = normalPower;
            Brightness = brightness;
            LightDirection = lightDirection;
            SetVector("_NoiseSize", noiseSize);
            SetVector("_NormalNoiseSize", normalNoiseSize);
            SetFloat("_NormalPower", normalPower);
            SetFloat("_Brightness", brightness);
            SetVector("_LightDir", lightDirection);
        }

        public void UpdateSecondaryNormalParameters(bool value)
        {
            UseSecondaryNormal = value;
            SetFloat("_UseSecondaryNormal", value ? 1f : 0f);
        }

        public void UpdatePolarWaveParameters(
            bool usePolarUv,
            Vector2 centerPosition,
            float radialScale,
            float radialSpreadSpeed,
            float rotationSpeed,
            float maskSize,
            float maskFade)
        {
            UsePolarUv = usePolarUv;
            PolarCenterPosition = centerPosition;
            RadialScale = radialScale;
            RadialSpreadSpeed = radialSpreadSpeed;
            RotationSpeed = rotationSpeed;
            PolarCenterMaskSize = maskSize;
            PolarCenterMaskFade = maskFade;
            SetFloat("_UsePolarUV", usePolarUv ? 1f : 0f);
            SetVector("_CenterPos", centerPosition);
            SetFloat("_RadialScale", radialScale);
            SetFloat("_RadialSpreadSpeed", radialSpreadSpeed);
            SetFloat("_RotationSpeed", rotationSpeed);
            SetFloat("_PolarCenterMaskSize", maskSize);
            SetFloat("_PolarCenterMaskFade", maskFade);
        }

        public void UpdateTimerParameter(double value)
        {
            Time = (float)value;
            SetFloat("_WaterSurfaceTime", Time);
        }

        private void OnEnable()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            _material = new Material(renderer.sharedMaterial);
            renderer.material = _material;
        }

        private void OnDestroy()
        {
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
        }

        private void SetFloat(string property, float value)
        {
            if (_material != null && _material.HasProperty(property))
                _material.SetFloat(property, value);
        }

        private void SetVector(string property, Vector4 value)
        {
            if (_material != null && _material.HasProperty(property))
                _material.SetVector(property, value);
        }
    }
}
