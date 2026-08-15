using UnityEngine;

namespace Sekai
{
    [ExecuteAlways]
    public sealed class SekaiCharacterDirectionalLight : MonoBehaviour
    {
        public enum FadeMode { FadeOut = 0, Spread = 1 }

        [SerializeField] private bool useFaceShadowLimiter = true;
        [SerializeField, Range(0f, 1f)] private float faceShadowLimitRange;
        [SerializeField, Range(0f, 1f)] private float shadowTexWeight = 1f;
        [SerializeField] private bool useShadowFade;
        [SerializeField] private FadeMode fade;
        [SerializeField, Range(0f, 1f)] private float shadowWidth;
        [SerializeField] private bool useHsvControl;
        [SerializeField, Range(0f, 1f)] private float hue;
        [SerializeField, Range(0f, 1f)] private float saturation = 0.5f;
        [SerializeField, Range(0f, 1f)] private float value = 0.5f;
        [SerializeField, Range(0f, 1f)] private float contrast = 0.5f;

        public bool UseFaceShadowLimiter => useFaceShadowLimiter;
        public float FaceShadowLimitRange => faceShadowLimitRange;
        public float ShadowTexWeight => shadowTexWeight;
        public bool UseShadowFade => useShadowFade;
        public FadeMode Fade => fade;
        public float ShadowWidth => shadowWidth;
        public bool UseHsvControl => useHsvControl;
        public float Hue => hue;
        public float Saturation => saturation;
        public float Value => value;
        public float Contrast => contrast;

        public void Initialize()
        {
            useFaceShadowLimiter = true;
            faceShadowLimitRange = 0f;
            shadowTexWeight = 1f;
            useShadowFade = false;
            fade = FadeMode.FadeOut;
            shadowWidth = 0f;
            useHsvControl = false;
            hue = 0f;
            saturation = 0.5f;
            value = 0.5f;
            contrast = 0.5f;
        }

        public void ApplyShaderGlobals()
        {
            var angle = useHsvControl ? hue * Mathf.PI * 2f : 0f;
            Shader.SetGlobalFloat("_SekaiCharacterDirectionalOverride", 1f);
            Shader.SetGlobalFloat("_SekaiCharacterUseFaceShadowLimiter",
                useFaceShadowLimiter ? 1f : 0f);
            Shader.SetGlobalFloat("_SekaiCharacterRangeLimit", faceShadowLimitRange);
            Shader.SetGlobalFloat("_SekaiCharacterShadowTexWeight", shadowTexWeight);
            Shader.SetGlobalFloat("_SekaiCharacterShadowWidth",
                useShadowFade ? shadowWidth : 0f);
            Shader.SetGlobalFloat("_SekaiCharacterFadeMode", (float)fade);
            Shader.SetGlobalFloat("_SekaiCharacterHueSinAngle", Mathf.Sin(angle));
            Shader.SetGlobalFloat("_SekaiCharacterHueCosAngle", Mathf.Cos(angle));
            Shader.SetGlobalFloat("_SekaiCharacterSaturation",
                useHsvControl ? saturation : 0.5f);
            Shader.SetGlobalFloat("_SekaiCharacterValue",
                useHsvControl ? value : 0.5f);
            Shader.SetGlobalFloat("_SekaiCharacterContrast",
                useHsvControl ? contrast : 0.5f);
        }

        private void Update() => ApplyShaderGlobals();
    }
}
