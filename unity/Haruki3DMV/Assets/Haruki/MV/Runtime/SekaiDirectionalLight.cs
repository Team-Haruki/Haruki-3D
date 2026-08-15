using UnityEngine;

namespace Sekai
{
    [ExecuteAlways]
    public sealed class SekaiDirectionalLight : MonoBehaviour
    {
        [SerializeField] private SekaiCharacterDirectionalLight characterDirectionalLightData;
        [SerializeField] private Color shadowColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float shadowThreshold;

        public SekaiCharacterDirectionalLight CharacterDirectionalLightData =>
            characterDirectionalLightData;
        public Color ShadowColor { get => shadowColor; set => shadowColor = value; }
        public float ShadowThreshold { get => shadowThreshold; set => shadowThreshold = value; }

        public void Initialize()
        {
            if (characterDirectionalLightData == null)
            {
                characterDirectionalLightData = GetComponent<SekaiCharacterDirectionalLight>() ??
                    gameObject.AddComponent<SekaiCharacterDirectionalLight>();
                characterDirectionalLightData.Initialize();
            }
        }

        public void ApplyShaderGlobals()
        {
            var direction = transform.forward;
            var vector = new Vector4(direction.x, direction.y, direction.z, 0f);
            Shader.SetGlobalVector("_DirectionalLightDirection", vector);
            Shader.SetGlobalVector("_SekaiDirectionalLightDirection", vector);
            Shader.SetGlobalVector("_DirectionalLightVector", vector);
            Shader.SetGlobalVector("_SekaiDirectionalLight", vector);
            Shader.SetGlobalColor("_DirectionalLightShadowColor", shadowColor);
            Shader.SetGlobalColor("_SekaiDirectionalLightShadowColor", shadowColor);
            Shader.SetGlobalColor("_SekaiShadowColor", shadowColor);
            Shader.SetGlobalFloat("_DirectionalLightShadowThreshold", shadowThreshold);
            Shader.SetGlobalFloat("_SekaiDirectionalLightShadowThreshold", shadowThreshold);
            Shader.SetGlobalFloat("_SekaiShadowThreshold", shadowThreshold);
            characterDirectionalLightData?.ApplyShaderGlobals();
        }

        private void Awake() => Initialize();
        private void Update() => ApplyShaderGlobals();
    }
}
