using UnityEngine;

namespace Sekai.Live
{
    [ExecuteAlways]
    public sealed class PenlightParameter : MonoBehaviour
    {
        public const int COLOR_COUNT = 8;
        public const int ANIM_COUNT = 8;
        public const float ROTATION_MULTIPLIER = 1.7f;
        public const int WEIGHT_TEX_SIZE = 128;

        [Range(0f, 1f)] public float viewThreshold = 1f;
        [Range(0f, 2f)] public float bloomIntensity = 1f;
        [Range(0f, 5f)] public float BloomSatMultiplier = 1f;
        [Range(0f, 1f)] public float consolidationRateY;
        [SerializeField] private bool _usePatternTexture;
        [SerializeField] private int _patternFadePreset;
        [SerializeField] private float _patternAnimation;
        [SerializeField] private float _patternThreshold;
        [SerializeField] private float _patternFade;
        [SerializeField] private bool _usePolarPatternAnimation;
        [SerializeField] private Vector2 _patternAnimationOffset;
        [SerializeField] private float _patternAnimationUVRotation;
        [SerializeField] private bool _usePolarPatternColor;
        [SerializeField] private Vector2 _patternColorOffset;
        [SerializeField] private float _patternColorUVRotation;
        [SerializeField] private bool _usePatternMask;
        [SerializeField] private float _patternMaskThreshold;
        public PenlightColor[] penlightColors;
        public PenlightAnimationKey[] animationKeys;
        private Material _material;
        private readonly Vector4[] _colorArray = new Vector4[WEIGHT_TEX_SIZE];
        private readonly Matrix4x4[] _animationArray = new Matrix4x4[WEIGHT_TEX_SIZE];

        public Vector4[] ColorSamples => _colorArray;
        public Matrix4x4[] AnimationSamples => _animationArray;

        public void Initialize()
        {
            InitializeChildObjects();
            var renderer = GetComponent<Renderer>();
            _material = renderer != null ? renderer.sharedMaterial : null;
            UpdateParameters();
        }

        public static float NormalizeWeight(float weight, float sum)
        {
            return weight / (sum > 0f ? sum : 1f);
        }

        private void InitializeChildObjects()
        {
            penlightColors = new PenlightColor[COLOR_COUNT];
            animationKeys = new PenlightAnimationKey[ANIM_COUNT];
            for (var index = 0; index < COLOR_COUNT; index++)
            {
                penlightColors[index] = FindOrCreate<PenlightColor>($"Color_{index:D2}");
                animationKeys[index] = FindOrCreate<PenlightAnimationKey>($"Anim_{index:D2}");
            }
        }

        private T FindOrCreate<T>(string objectName) where T : Component
        {
            var child = transform.Find(objectName);
            if (child == null)
            {
                var childObject = new GameObject(objectName);
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }
            return child.GetComponent<T>() ?? child.gameObject.AddComponent<T>();
        }

        private void Update()
        {
            if (penlightColors == null || animationKeys == null) return;
            UpdateParameters();
        }

        private void UpdateParameters()
        {
            var colorSum = SumWeights(penlightColors);
            var animationSum = SumWeights(animationKeys);
            for (var index = 0; index < WEIGHT_TEX_SIZE; index++)
            {
                var colorIndex = index % COLOR_COUNT;
                var animationIndex = index % ANIM_COUNT;
                var color = penlightColors[colorIndex];
                _colorArray[index] = color.color;
                _colorArray[index].w = NormalizeWeight(color.weight, colorSum);
                _animationArray[index] = PackAnimation(
                    animationKeys[animationIndex],
                    NormalizeWeight(animationKeys[animationIndex].weight, animationSum));
            }
            ApplyKnownMaterialParameters();
        }

        private static float SumWeights(PenlightKey[] keys)
        {
            var sum = 0f;
            foreach (var key in keys) if (key != null) sum += key.weight;
            return sum;
        }

        private static Matrix4x4 PackAnimation(PenlightAnimationKey key, float normalizedWeight)
        {
            var matrix = Matrix4x4.zero;
            matrix.SetRow(0, new Vector4(
                key.armPitch * ROTATION_MULTIPLIER,
                key.armRoll * ROTATION_MULTIPLIER,
                key.handPitch * ROTATION_MULTIPLIER,
                key.handRoll * ROTATION_MULTIPLIER));
            matrix.SetRow(1, new Vector4(
                key.yawOffset, key.elbowPosition.y, key.elbowPosition.z, key.armLength));
            matrix.SetRow(2, new Vector4(
                key.centerPosition.x, key.centerPosition.y,
                key.armRandomness.x, key.armRandomness.y));
            matrix.SetRow(3, new Vector4(
                key.handRandomness.x, key.handRandomness.y,
                key.yawRandomness, normalizedWeight));
            return matrix;
        }

        private void ApplyKnownMaterialParameters()
        {
            if (_material == null) return;
            SetFloat("_ViewThreshold", viewThreshold);
            SetFloat("_Intensity", bloomIntensity);
            SetFloat("_BloomSat", BloomSatMultiplier);
            SetFloat("_PatternAnimation", _patternAnimation);
            SetFloat("_PatternThreshold", _patternThreshold);
            SetFloat("_PatternFade", _patternFade);
            SetFloat("_PatternFadePreset", _patternFadePreset);
            SetFloat("_PolarPatternAnimation", _usePolarPatternAnimation ? 1f : 0f);
            SetFloat("_PolarPatternColor", _usePolarPatternColor ? 1f : 0f);
            SetFloat("_UsePatternMask", _usePatternMask ? 1f : 0f);
            SetFloat("_PatternMaskThreshold", _patternMaskThreshold);
            SetVector("_PatternAnimationOffset", _patternAnimationOffset);
            SetVector("_PatternColorOffset", _patternColorOffset);
            SetFloat("_PatternAnimationUVRotation", _patternAnimationUVRotation * Mathf.Deg2Rad);
            SetFloat("_PatternColorUVRotation", _patternColorUVRotation * Mathf.Deg2Rad);
            if (_usePatternTexture) _material.EnableKeyword("_USE_PENLIGHT_PATTERN");
            else _material.DisableKeyword("_USE_PENLIGHT_PATTERN");
        }

        private void SetFloat(string name, float value) { if (_material.HasProperty(name)) _material.SetFloat(name, value); }
        private void SetVector(string name, Vector4 value) { if (_material.HasProperty(name)) _material.SetVector(name, value); }
    }
}
