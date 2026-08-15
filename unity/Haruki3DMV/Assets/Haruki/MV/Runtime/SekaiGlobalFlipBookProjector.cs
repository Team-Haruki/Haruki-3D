using UnityEngine;

namespace Sekai.Rendering
{
    public sealed class SekaiGlobalFlipBookProjector : MonoBehaviour
    {
        private const string ShaderKeyword = "_USE_OVERLAY_FLIPBOOK";
        private static readonly int GlobalTimeId = Shader.PropertyToID("_GlobalFlipBookTime");
        private static readonly int CenterId = Shader.PropertyToID("_FlipBookCenterPosition");
        private static readonly int OpacityId = Shader.PropertyToID("_FlipBookOpacity");
        private static readonly int FadeRadiusId = Shader.PropertyToID("_FlipBookFadeRadius");
        private static readonly int FadeFallOffId = Shader.PropertyToID("_FlipBookFadeFallOff");
        private static readonly int TextureId = Shader.PropertyToID("_FlipBookTex");
        private static readonly int FrameCountXId = Shader.PropertyToID("_FlipBookFrameCountX");
        private static readonly int FrameCountYId = Shader.PropertyToID("_FlipBookFrameCountY");
        private static readonly int FpsId = Shader.PropertyToID("_FlipBookFPS");

        [SerializeField] private Texture2D texture;
        [SerializeField] private float fps;
        [SerializeField] private int frameCountX;
        [SerializeField] private int frameCountY;
        [SerializeField, Range(0f, 1f)] private float opacity;
        [SerializeField] private float fadeRadius;
        [SerializeField] private float fadeFallOff;
        [SerializeField] private Vector2 uvScroll;
        [SerializeField] private Color flipBookColor_Stage = Color.white;
        [SerializeField] private float scrollSpeed_Stage;
        [SerializeField] private float uvScale_Stage;
        [SerializeField, Range(0f, 1f)] private float dotMaskThreshold_Stage;
        [SerializeField, Range(0f, 1f)] private float dotMaskFallOff_Stage;
        [SerializeField, Range(0f, 1f)] private float flipBookUpperDotMaskOpacity_Stage;
        [SerializeField, Range(0f, 1f)] private float flipBookLowerDotMaskOpacity_Stage;
        [SerializeField] private Color flipBookColor_Character = Color.white;
        [SerializeField] private float scrollSpeed_Character;
        [SerializeField] private float uvScale_Character;
        [SerializeField, Range(0f, 1f)] private float dotMaskThreshold_Character;
        [SerializeField, Range(0f, 1f)] private float dotMaskFallOff_Character;
        [SerializeField, Range(0f, 1f)] private float flipBookUpperDotMaskOpacity_Character;
        [SerializeField, Range(0f, 1f)] private float flipBookLowerDotMaskOpacity_Character;

        public Vector3 CenterPosition => transform.position;
        public float FlipBookOpacity => opacity;
        public float FlipBookFadeRadius => fadeRadius;
        public float FlipBookFadeFallOff => fadeFallOff;
        public Texture FlipBookTex => texture;
        public int FlipBookFrameCountX => frameCountX;
        public int FlipBookFrameCountY => frameCountY;
        public float FlipBookFPS => fps;
        public Color FlipBookColor_Stage => flipBookColor_Stage;
        public Color FlipBookColor_Character => flipBookColor_Character;
        public float FlipBookScale_Stage => uvScale_Stage;
        public float FlipBookScale_Character => uvScale_Character;
        public Vector2 FlipBookUVScroll => uvScroll;
        public float FlipBookMaskThreshold_Stage => dotMaskThreshold_Stage;
        public float FlipBookMaskThreshold_Character => dotMaskThreshold_Character;
        public float FlipBookMaskFallOff_Stage => dotMaskFallOff_Stage;
        public float FlipBookMaskFallOff_Character => dotMaskFallOff_Character;
        public float FlipBookUpperDotMaskOpacity_Stage =>
            flipBookUpperDotMaskOpacity_Stage;
        public float FlipBookUpperDotMaskOpacity_Character =>
            flipBookUpperDotMaskOpacity_Character;
        public float FlipBookLowerDotMaskOpacity_Stage =>
            flipBookLowerDotMaskOpacity_Stage;
        public float FlipBookLowerDotMaskOpacity_Character =>
            flipBookLowerDotMaskOpacity_Character;
        public float ScrollSpeed_Stage => scrollSpeed_Stage;
        public float SrollSpeed_Character => scrollSpeed_Character;

        public void Setup()
        {
            ApplyGlobals();
            SetFlipBookActive(true);
        }

        public void SetOpacity(float value)
        {
            opacity = value;
            Shader.SetGlobalFloat(OpacityId, value);
        }

        public static void SetTime(float time)
        {
            Shader.SetGlobalFloat(GlobalTimeId, time);
        }

        public static void SetFlipBookActive(bool active)
        {
            if (active) Shader.EnableKeyword(ShaderKeyword);
            else Shader.DisableKeyword(ShaderKeyword);
        }

        private void OnEnable() => Setup();

        private void OnValidate()
        {
            if (isActiveAndEnabled) ApplyGlobals();
        }

        private void Update()
        {
            Shader.SetGlobalVector(CenterId, transform.position);
        }

        private void OnDisable() => SetFlipBookActive(false);

        private void ApplyGlobals()
        {
            Shader.SetGlobalVector(CenterId, transform.position);
            Shader.SetGlobalFloat(OpacityId, opacity);
            Shader.SetGlobalFloat(FadeRadiusId, fadeRadius);
            Shader.SetGlobalFloat(FadeFallOffId, fadeFallOff);
            Shader.SetGlobalTexture(TextureId, texture);
            Shader.SetGlobalInt(FrameCountXId, frameCountX);
            Shader.SetGlobalInt(FrameCountYId, frameCountY);
            Shader.SetGlobalFloat(FpsId, fps);
            Shader.SetGlobalColor("_FlipBookColor_Stage", flipBookColor_Stage);
            Shader.SetGlobalColor("_FlipBookColor_Character", flipBookColor_Character);
            Shader.SetGlobalFloat("_FlipBookScale_Stage", uvScale_Stage);
            Shader.SetGlobalFloat("_FlipBookScale_Character", uvScale_Character);
            Shader.SetGlobalVector(
                "_FlipBookUVScroll_Stage",
                uvScroll * scrollSpeed_Stage);
            Shader.SetGlobalVector(
                "_FlipBookUVScroll_Character",
                uvScroll * scrollSpeed_Character);
            Shader.SetGlobalFloat("_FlipBookMaskThreshold_Stage", dotMaskThreshold_Stage);
            Shader.SetGlobalFloat(
                "_FlipBookMaskThreshold_Character",
                dotMaskThreshold_Character);
            Shader.SetGlobalFloat("_FlipBookMaskFallOff_Stage", dotMaskFallOff_Stage);
            Shader.SetGlobalFloat(
                "_FlipBookMaskFallOff_Character",
                dotMaskFallOff_Character);
            Shader.SetGlobalFloat(
                "_FlipBookUpperDotMaskOpacity_Stage",
                flipBookUpperDotMaskOpacity_Stage);
            Shader.SetGlobalFloat(
                "_FlipBookUpperDotMaskOpacity_Character",
                flipBookUpperDotMaskOpacity_Character);
            Shader.SetGlobalFloat(
                "_FlipBookLowerDotMaskOpacity_Stage",
                flipBookLowerDotMaskOpacity_Stage);
            Shader.SetGlobalFloat(
                "_FlipBookLowerDotMaskOpacity_Character",
                flipBookLowerDotMaskOpacity_Character);
        }
    }
}
