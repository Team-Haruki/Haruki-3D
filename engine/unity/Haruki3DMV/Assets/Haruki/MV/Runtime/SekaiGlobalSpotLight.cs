using UnityEngine;

namespace Sekai
{
    [ExecuteAlways]
    public sealed class SekaiGlobalSpotLight : MonoBehaviour
    {
        [SerializeField] private Color outerColor = Color.white;
        [SerializeField] private float radiusNear;
        [SerializeField] private float radiusFar;

        public Color OuterColor { get => outerColor; set => outerColor = value; }
        public float RadiusNear { get => radiusNear; set => radiusNear = value; }
        public float RadiusFar { get => radiusFar; set => radiusFar = value; }

        public void SetEnabled(bool isEnable)
        {
            Shader.SetGlobalFloat(
                "_SekaiGlobalSpotLightEnabled",
                isEnable ? 1f : 0f);
        }

        public bool CheckInsideOnHorizontal(Vector3 position)
        {
            var delta = position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= radiusFar;
        }

        public void Reset()
        {
            outerColor = Color.white;
            radiusNear = 0f;
            radiusFar = 0f;
        }

        public void ApplyShaderGlobals()
        {
            Shader.SetGlobalVector("_SekaiGlobalSpotLightPos", transform.position);
            Shader.SetGlobalColor("_SekaiGlobalSpotLightColor", outerColor);
            Shader.SetGlobalFloat("_SekaiGlobalSpotLightRadiusNear", radiusNear);
            Shader.SetGlobalFloat("_SekaiGlobalSpotLightRadiusFar", radiusFar);
        }

        private void OnEnable() => SetEnabled(true);
        private void OnDisable() => SetEnabled(false);
        private void Update() => ApplyShaderGlobals();
    }
}
