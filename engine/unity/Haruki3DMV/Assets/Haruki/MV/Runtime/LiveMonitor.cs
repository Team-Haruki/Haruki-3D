using UnityEngine;

namespace Sekai.Core
{
    [ExecuteAlways]
    public sealed class LiveMonitor : MonoBehaviour
    {
        public enum ImageType
        {
            MainCamera = 0,
            SubCamera = 1,
            AnimationSheet = 2,
        }

        private static readonly int BgTexStId = Shader.PropertyToID("_BgTex_ST");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int LocalTimeId = Shader.PropertyToID("_LocalTime");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SheetValueId = Shader.PropertyToID("_SheetValue");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int FadeColorId = Shader.PropertyToID("_FadeColor");
        private static readonly int CharacterIndexId = Shader.PropertyToID("_CharacterIndex");
        private const string ManyCharacterKeyword = "_MANY_CHARACTER";

        public float time;

        [SerializeField]
        private ImageType imageType;

        [Range(1, 10)] [SerializeField] private int tileX = 1;
        [Range(1, 10)] [SerializeField] private int tileY = 1;
        [SerializeField] private bool isOverrideBgTextureTilingOffset;
        [SerializeField] private float bgTexTilingX = 1f;
        [SerializeField] private float bgTexTilingY = 1f;
        [SerializeField] private float bgTexOffsetX;
        [SerializeField] private float bgTexOffsetY;
        [Range(1, 60)] [SerializeField] private int fps = 10;
        [Range(0f, 1f)] [SerializeField] private float brightness = 1f;
        [Range(0f, 1f)] [SerializeField] private float fade;
        [SerializeField] private Color fadeColor = Color.white;
        [SerializeField] private bool overrideIntensity;
        [SerializeField] private float intensity = 0.1f;
        [SerializeField] private Color baseColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private int characterIndex;

        private Texture _defaultAnimationTexture;
        private float _defaultIntensity;
        private Renderer _renderer;
        private Material _material;

        public ImageType Type => imageType;

        private void Start()
        {
            Setup();
        }

        private void OnEnable()
        {
            Setup();
            LiveMonitorRuntime.Register(this);
        }

        private void OnDisable()
        {
            LiveMonitorRuntime.Unregister(this);
        }

        private void OnDestroy()
        {
            LiveMonitorRuntime.Unregister(this);
        }

        public void Execute(float absoluteTime, bool renderManyCharacters)
        {
            time = absoluteTime;
            if (_material == null) Setup();
            if (_material == null) return;

            var texture = imageType == ImageType.MainCamera
                ? LiveMonitorRuntime.MainCameraTexture
                : imageType == ImageType.SubCamera
                    ? LiveMonitorRuntime.SubCameraTexture
                    : _defaultAnimationTexture;
            if (texture != null) _material.mainTexture = texture;

            _material.SetFloat(BrightnessId, brightness);
            _material.SetFloat(LocalTimeId, time);
            _material.SetColor(BaseColorId, baseColor);
            _material.SetFloat(IntensityId, overrideIntensity ? intensity : _defaultIntensity);
            _material.SetFloat(FadeId, fade);
            _material.SetColor(FadeColorId, fadeColor);
            if (_material.HasProperty(CharacterIndexId))
                _material.SetInt(CharacterIndexId, characterIndex);
            if (renderManyCharacters) _material.EnableKeyword(ManyCharacterKeyword);
            else _material.DisableKeyword(ManyCharacterKeyword);

            if (imageType == ImageType.AnimationSheet)
            {
                var columns = Mathf.Max(tileX, 1);
                var rows = Mathf.Max(tileY, 1);
                var frame = Mathf.FloorToInt(time * Mathf.Max(fps, 1)) % (columns * rows);
                _material.SetVector(
                    SheetValueId,
                    new Vector4(columns, rows, frame % columns, frame / columns));
            }
            if (isOverrideBgTextureTilingOffset)
            {
                _material.SetVector(
                    BgTexStId,
                    new Vector4(bgTexTilingX, bgTexTilingY, bgTexOffsetX, bgTexOffsetY));
            }
        }

        private void Setup()
        {
            _renderer = _renderer != null ? _renderer : GetComponent<Renderer>();
            if (_renderer == null) return;
            // The game updates the authored shared monitor material. Creating a
            // renderer.material clone here would detach Timeline updates from
            // sibling monitor meshes and leak one material per instance.
            _material = _renderer.sharedMaterial;
            if (_defaultAnimationTexture == null) _defaultAnimationTexture = _material.mainTexture;
            _defaultIntensity = _material.HasProperty(IntensityId)
                ? _material.GetFloat(IntensityId)
                : intensity;
            Execute(time, LiveMonitorRuntime.RenderManyCharacters);
        }
    }

    public static class LiveMonitorRuntime
    {
        private static readonly System.Collections.Generic.HashSet<LiveMonitor> Monitors =
            new System.Collections.Generic.HashSet<LiveMonitor>();

        public static Texture MainCameraTexture { get; set; }
        public static Texture SubCameraTexture { get; set; }
        public static bool RenderManyCharacters { get; set; }
        public static double CurrentTime { get; private set; }

        public static void Register(LiveMonitor monitor)
        {
            if (monitor != null) Monitors.Add(monitor);
        }

        public static void Unregister(LiveMonitor monitor)
        {
            if (monitor != null) Monitors.Remove(monitor);
        }

        public static void SetTime(double time)
        {
            CurrentTime = time;
            foreach (var monitor in Monitors)
            {
                if (monitor != null && monitor.isActiveAndEnabled)
                    monitor.Execute((float)time, RenderManyCharacters);
            }
        }

        public static void Refresh()
        {
            SetTime(CurrentTime);
        }
    }
}
