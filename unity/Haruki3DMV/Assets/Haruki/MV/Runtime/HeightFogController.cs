using UnityEngine;

namespace Sekai.Rendering
{
    public static class HeightFogEnums
    {
        public enum FogMode { UseScriptSettings = 0, UseTimeOfDay = 1 }
        public enum FogAxisMode { XAxis = 0, YAxis = 1, ZAxis = 2 }
        public enum FogLayersMode { MultiplyDistanceAndHeight = 0, AdditiveDistanceAndHeight = 1 }
    }

    public interface IHeightFogController
    {
        void UpdateIntensity(float value);
        void UpdateAxis(HeightFogEnums.FogAxisMode value);
        void UpdateMultiplyDistanceAndHeight(HeightFogEnums.FogLayersMode value);
        void UpdateLightParams(float value);
        void UpdateDistanceParams(Color startColor, Color endColor, float duo, float start, float end);
        void UpdateHeightParams(float start, float end, float farHeight, float farOffset);
        void UpdateTime(float value);
        bool UseDirectParams();
    }

    [ExecuteAlways]
    public sealed class HeightFogController : MonoBehaviour, IHeightFogController
    {
        [SerializeField] private float fogIntensity;
        [SerializeField] private HeightFogEnums.FogAxisMode fogAxisMode;
        [SerializeField] private HeightFogEnums.FogLayersMode fogLayersMode;
        [SerializeField] private HeightFogEnums.FogMode fogMode;
        [SerializeField] private Material presetDay;
        [SerializeField] private Material presetNight;
        [SerializeField, Range(0f, 1f)] private float timeOfDay;
        [SerializeField] private Color distanceColorStart;
        [SerializeField] private Color distanceColorEnd;
        [SerializeField, Range(0f, 1f)] private float distanceColorDuo;
        [SerializeField] private float distanceStart;
        [SerializeField] private float distanceEnd;
        [SerializeField] private float heightStart;
        [SerializeField] private float heightEnd;
        [SerializeField] private float farDistanceHeight;
        [SerializeField] private float farDistanceOffset;
        [SerializeField, Range(0f, 5f)] private float directionalIntensity;
        [SerializeField] private bool useNoise;
        [SerializeField] private float noiseIntensity;
        [SerializeField] private float noiseMin;
        [SerializeField] private float noiseMax;
        [SerializeField] private float noiseScale;
        [SerializeField] private Vector3 noiseSpeed;
        [SerializeField] private float noiseDistanceEnd;
        [SerializeField] private bool lockPositionAndScale;
        [SerializeField] private int renderPriority = 3000;
        [SerializeField] private bool useDirectParams;
        [SerializeField, HideInInspector] private int version;
        private Material _material;
        private Camera _mainCamera;
        private Transform _directionalLight;
        private float _time;

        public float FogIntensity => fogIntensity;
        public HeightFogEnums.FogAxisMode FogAxisMode => fogAxisMode;
        public HeightFogEnums.FogLayersMode FogLayersMode => fogLayersMode;
        public float Time => _time;

        public void Setup(Camera camera, Transform directionalLight)
        {
            _mainCamera = camera;
            _directionalLight = directionalLight;
            ApplyMaterial();
        }

        public void UpdateIntensity(float value) { fogIntensity = value; ApplyMaterial(); }
        public void UpdateAxis(HeightFogEnums.FogAxisMode value) { fogAxisMode = value; ApplyMaterial(); }
        public void UpdateMultiplyDistanceAndHeight(HeightFogEnums.FogLayersMode value) { fogLayersMode = value; ApplyMaterial(); }
        public void UpdateLightParams(float value) { directionalIntensity = value; ApplyMaterial(); }

        public void UpdateDistanceParams(Color startColor, Color endColor, float duo, float start, float end)
        {
            distanceColorStart = startColor;
            distanceColorEnd = endColor;
            distanceColorDuo = duo;
            distanceStart = start;
            distanceEnd = end;
            ApplyMaterial();
        }

        public void UpdateHeightParams(float start, float end, float farHeight, float farOffset)
        {
            heightStart = start;
            heightEnd = end;
            farDistanceHeight = farHeight;
            farDistanceOffset = farOffset;
            ApplyMaterial();
        }

        public void UpdateTime(float value) { _time = value; ApplyMaterial(); }
        public bool UseDirectParams() => useDirectParams;

        private void OnEnable()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            _material = new Material(renderer.sharedMaterial);
            renderer.material = _material;
            _material.renderQueue = renderPriority;
            ApplyMaterial();
        }

        private void LateUpdate()
        {
            if (lockPositionAndScale || _mainCamera == null) return;
            transform.position = _mainCamera.transform.position;
            transform.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
        }

        private void ApplyMaterial()
        {
            if (_material == null) return;
            SetFloat("_FogIntensity", fogIntensity);
            SetFloat("_FogAxisOption", (int)fogAxisMode);
            SetFloat("_FogLayersMode", (int)fogLayersMode);
            SetColor("_DistanceColorStart", distanceColorStart);
            SetColor("_DistanceColorEnd", distanceColorEnd);
            SetFloat("_DistanceColorDuo", distanceColorDuo);
            SetFloat("_DistanceStart", distanceStart);
            SetFloat("_DistanceEnd", distanceEnd);
            SetVector("_HeightParams", new Vector4(heightStart, heightEnd, farDistanceHeight, farDistanceOffset));
            SetFloat("_DirectionalIntensity", directionalIntensity);
            SetFloat("_CurrentTime", _time);
            if (_directionalLight != null)
                SetVector("_DirectionalDir", _directionalLight.forward);
        }

        private void SetFloat(string name, float value) { if (_material.HasProperty(name)) _material.SetFloat(name, value); }
        private void SetColor(string name, Color value) { if (_material.HasProperty(name)) _material.SetColor(name, value); }
        private void SetVector(string name, Vector4 value) { if (_material.HasProperty(name)) _material.SetVector(name, value); }
    }
}
