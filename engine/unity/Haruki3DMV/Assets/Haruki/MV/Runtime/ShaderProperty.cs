using UnityEngine;

namespace Sekai.Core
{
    [RequireComponent(typeof(Renderer))]
    [ExecuteAlways]
    public sealed class ShaderProperty : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int YAxisId = Shader.PropertyToID("_YAxis");

        [SerializeField]
        private Color color = Color.white;

        [Range(0f, 1f)]
        [SerializeField]
        private float intensity = 1f;

        [SerializeField]
        private bool autoDisableRenderer;

        private Renderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private Color _previousColor;
        private float _previousIntensity;
        private Vector3 _previousYAxis;
        private bool _hasApplied;

        public Color Color
        {
            get => color;
            set => color = value;
        }

        public float Intensity
        {
            get => intensity;
            set => intensity = value;
        }

        private void Awake()
        {
            CacheRenderer();
        }

        private void OnEnable()
        {
            CacheRenderer();
            Apply(force: true);
        }

        private void LateUpdate()
        {
            Apply(force: false);
        }

        private void CacheRenderer()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
        }

        private void Apply(bool force)
        {
            CacheRenderer();
            if (_renderer == null)
            {
                return;
            }

            var yAxis = transform.up;
            if (!force && _hasApplied &&
                _previousColor == color &&
                Mathf.Approximately(_previousIntensity, intensity) &&
                _previousYAxis == yAxis)
            {
                return;
            }

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetFloat(IntensityId, intensity);
            _propertyBlock.SetVector(YAxisId, yAxis);
            _renderer.SetPropertyBlock(_propertyBlock);
            if (autoDisableRenderer)
            {
                _renderer.enabled = intensity > 0f && color.maxColorComponent > 0f;
            }

            _previousColor = color;
            _previousIntensity = intensity;
            _previousYAxis = yAxis;
            _hasApplied = true;
        }
    }
}
