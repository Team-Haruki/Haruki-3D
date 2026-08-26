using System;
using UnityEngine;

namespace Sekai.Core
{
    [ExecuteAlways]
    public sealed class SekaiCharacterAmbientLight : MonoBehaviour
    {
        public const int FormationCapacity = 12;
        private static readonly float[] IntensityArray = new float[FormationCapacity];
        private static readonly Vector4[] ColorArray = new Vector4[FormationCapacity];
        private static readonly Vector4[] SpecularColorArray = new Vector4[FormationCapacity];
        private static readonly Vector4[] OutlineColorArray = new Vector4[FormationCapacity];
        private static readonly float[] OutlineBlendingArray = new float[FormationCapacity];

        [SerializeField] private Color ambientColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField, Range(0f, 1f)] private float intensity = 1f;
        [SerializeField] private Color specularColor = Color.white;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float outlineBlending = 0.5f;
        [SerializeField, HideInInspector] private int formationId;

        public float Intensity { get => intensity; set => intensity = value; }
        public Color Color { get => ambientColor; set => ambientColor = value; }
        public Color SpecularColor { get => specularColor; set => specularColor = value; }
        public Color OutlineColor { get => outlineColor; set => outlineColor = value; }
        public float OutlineBlending { get => outlineBlending; set => outlineBlending = value; }
        public int FormationId { get => formationId; set => formationId = value; }

        public void ApplyShaderGlobals()
        {
            ValidateFormationId(formationId);
            IntensityArray[formationId] = intensity;
            ColorArray[formationId] = ambientColor;
            SpecularColorArray[formationId] = specularColor;
            OutlineColorArray[formationId] = outlineColor;
            OutlineBlendingArray[formationId] = outlineBlending;
            Shader.SetGlobalFloatArray("_SekaiCharacterAmbientLightIntensityArray", IntensityArray);
            Shader.SetGlobalVectorArray("_SekaiCharacterAmbientLightColorArray", ColorArray);
            Shader.SetGlobalVectorArray("_SekaiCharacterSpecularColorArray", SpecularColorArray);
            Shader.SetGlobalVectorArray("_SekaiCharacterOutlineColorArray", OutlineColorArray);
            Shader.SetGlobalFloatArray("_SekaiCharacterOutlineBlendingArray", OutlineBlendingArray);
        }

        private void Update() => ApplyShaderGlobals();

        internal static void ValidateFormationId(int id)
        {
            if (id < 0 || id >= FormationCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(id), id, $"Formation id must be in [0,{FormationCapacity - 1}].");
            }
        }
    }
}
