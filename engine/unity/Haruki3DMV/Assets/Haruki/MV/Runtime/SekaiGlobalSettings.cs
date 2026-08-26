using UnityEngine;

namespace Sekai.Core
{
    [ExecuteAlways]
    public sealed class SekaiGlobalSettings : MonoBehaviour
    {
        public enum GraphicsQuality { High = 500, Middle = 300, Low = 100 }

        [SerializeField] private Color fogColor = Color.white;
        [SerializeField] private float fogStart;
        [SerializeField] private float fogEnd = 1f;
        [SerializeField, Range(0f, 1f), HideInInspector] private float intensity = 1f;
        [SerializeField] private GraphicsQuality quality = GraphicsQuality.High;

        public Color FogColor { get => fogColor; set => fogColor = value; }
        public float FogStart { get => fogStart; set => fogStart = value; }
        public float FogEnd { get => fogEnd; set => fogEnd = value; }
        public float Intensity { get => intensity; set => intensity = value; }
        public GraphicsQuality Quality { get => quality; set => quality = value; }

        public void ApplyShaderGlobals()
        {
            var fogRange = Mathf.Max(fogEnd - fogStart, 0.00001f);
            Shader.globalMaximumLOD = (int)quality;
            Shader.SetGlobalColor("rp_FogColor", fogColor);
            Shader.SetGlobalFloat("rp_GlobalIntensity", intensity);
            Shader.SetGlobalFloat("_SekaiAllLightIntensity", intensity);
            Shader.SetGlobalColor("_SekaiFogColor", fogColor);
            Shader.SetGlobalVector(
                "_SekaiFogFactor",
                new Vector4(fogEnd / fogRange, 1f / fogRange, 0f, 0f));
        }

        private void Update() => ApplyShaderGlobals();
    }
}
