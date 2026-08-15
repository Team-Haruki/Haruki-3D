using UnityEngine;

namespace Sekai
{
    [ExecuteAlways]
    public sealed class SekaiAmbientLight : MonoBehaviour
    {
        [SerializeField] private Color ambientColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float intensity = 1f;
        [SerializeField, Range(0f, 1f), HideInInspector] private float glowIntensity;

        public Color AmbientColor { get => ambientColor; set => ambientColor = value; }
        public float Intensity { get => intensity; set => intensity = value; }
        public float GlowIntensity { get => glowIntensity; set => glowIntensity = value; }

        public void ApplyShaderGlobals()
        {
            Shader.SetGlobalColor("rp_AmbientLightColor", ambientColor);
            Shader.SetGlobalVector(
                "rp_AmbientLightIntensity",
                new Vector4(intensity, glowIntensity, 0f, 0f));
            Shader.SetGlobalColor("_SekaiAmbientLightColor", ambientColor);
            Shader.SetGlobalFloat("_SekaiLightIntensity", intensity);
            Shader.SetGlobalFloat("_SekaiGlowLightIntensity", glowIntensity);
        }

        private void Update() => ApplyShaderGlobals();
    }
}
