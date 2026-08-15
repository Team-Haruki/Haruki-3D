using System;
using UnityEngine;

namespace Sekai.Core
{
    [ExecuteAlways]
    public sealed class SekaiCharacterRimLight : MonoBehaviour
    {
        public enum RimDirectionMode { TransformForward = 0, ScreenVector = 1 }

        private static readonly Vector4[] DirectionArray = new Vector4[SekaiCharacterAmbientLight.FormationCapacity];
        private static readonly Vector4[] ColorArray = new Vector4[SekaiCharacterAmbientLight.FormationCapacity];
        private static readonly Vector4[] ShadowColorArray = new Vector4[SekaiCharacterAmbientLight.FormationCapacity];
        private static readonly Vector4[] FactorArray = new Vector4[SekaiCharacterAmbientLight.FormationCapacity];
        private static readonly float[] ShadowSharpnessArray = new float[SekaiCharacterAmbientLight.FormationCapacity];

        [SerializeField] private Color rimColor = Color.white;
        [SerializeField, Range(0f, 10f)] private float range = 1f;
        [SerializeField, Range(0.001f, 1f), HideInInspector] private float edgeSmoothness = 0.001f;
        [SerializeField, Range(0f, 10f)] private float emission;
        [SerializeField] private RimDirectionMode directionMode;
        [SerializeField] private Vector3 rimDirectionVector = Vector3.forward;
        [SerializeField, Range(0f, 1f)] private float lightInfluence = 1f;
        [SerializeField] private bool isUseShadowColor;
        [SerializeField] private Color shadowRimColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float shadowSharpness = 0.5f;
        [SerializeField, HideInInspector] private int formationId;
        [SerializeField, HideInInspector] private Camera mainCamera;

        public Color Color { get => rimColor; set => rimColor = value; }
        public float Range { get => range; set => range = value; }
        public float EdgeSmoothness { get => edgeSmoothness; set => edgeSmoothness = value; }
        public float Emission { get => emission; set => emission = value; }
        public RimDirectionMode DirectionMode { get => directionMode; set => directionMode = value; }
        public Vector3 RimDirectionVector { get => rimDirectionVector; set => rimDirectionVector = value; }
        public float LightInfluence { get => lightInfluence; set => lightInfluence = value; }
        public bool IsUseShadowColor { get => isUseShadowColor; set => isUseShadowColor = value; }
        public Color ShadowRimColor { get => shadowRimColor; set => shadowRimColor = value; }
        public float ShadowSharpness { get => shadowSharpness; set => shadowSharpness = value; }
        public int FormationId => formationId;

        public void Setup(int id, Camera camera, float setupRange = 9f)
        {
            SekaiCharacterAmbientLight.ValidateFormationId(id);
            formationId = id;
            mainCamera = camera;
            range = setupRange;
        }

        public Vector4 PackFactor() => new Vector4(
            range, emission, edgeSmoothness, Mathf.GammaToLinearSpace(lightInfluence));

        public void ApplyShaderGlobals()
        {
            SekaiCharacterAmbientLight.ValidateFormationId(formationId);
            var direction = GetRimLightDirection(directionMode);
            DirectionArray[formationId] = new Vector4(direction.x, direction.y, direction.z, 0f);
            ColorArray[formationId] = rimColor;
            ShadowColorArray[formationId] = isUseShadowColor ? shadowRimColor : rimColor;
            FactorArray[formationId] = PackFactor();
            ShadowSharpnessArray[formationId] = shadowSharpness;
            SetVectorArrayAliases("_SekaiCharacterRimLightDirectionArray", "_SekaiRimLightDirectionArray", DirectionArray);
            SetVectorArrayAliases("_SekaiCharacterRimLightColorArray", "_SekaiRimLightColorArray", ColorArray);
            SetVectorArrayAliases("_SekaiCharacterRimLightShadowColorArray", "_SekaiRimLightShadowColorArray", ShadowColorArray);
            SetVectorArrayAliases("_SekaiCharacterRimLightFactorArray", "_SekaiRimLightFactorArray", FactorArray);
            Shader.SetGlobalFloatArray("_SekaiCharacterRimLightShadowSharpnessArray", ShadowSharpnessArray);
            Shader.SetGlobalFloatArray("_SekaiRimLightShadowSharpnessArray", ShadowSharpnessArray);
        }

        private Vector3 GetRimLightDirection(RimDirectionMode mode)
        {
            Vector3 direction;
            switch (mode)
            {
                case RimDirectionMode.TransformForward:
                    direction = transform.forward;
                    break;
                case RimDirectionMode.ScreenVector:
                    if (mainCamera == null)
                    {
                        throw new InvalidOperationException("ScreenVector rim lighting requires the MV main camera.");
                    }
                    direction = mainCamera.transform.localToWorldMatrix.MultiplyVector(-rimDirectionVector);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
            return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        }

        private static void SetVectorArrayAliases(string officialName, string legacyName, Vector4[] values)
        {
            Shader.SetGlobalVectorArray(officialName, values);
            Shader.SetGlobalVectorArray(legacyName, values);
        }

        private void Update() => ApplyShaderGlobals();
    }
}
