using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sekai.Rendering
{
    [ExecuteAlways]
    public sealed class SekaiCharacterFaceLighting : MonoBehaviour
    {
        private static readonly int FaceFrontId = Shader.PropertyToID("_FaceFront");
        private static readonly int HeadDotLightId =
            Shader.PropertyToID("_HeadDotDirectionalLightValues");
        private static readonly int RangeLimitId = Shader.PropertyToID("_RangeLimit");
        private static readonly int UseLimiterId =
            Shader.PropertyToID("_UseFaceShadowLimiter");
        private const string FaceSdfKeyword = "_UseFaceSDF";
        private const string FaceRangeKeyword = "_FACE_SHADOW_RANGE_LIMIT";

        private Transform _head;
        private Transform _directionalLight;
        private Sekai.SekaiCharacterDirectionalLight _lightData;
        private Material[] _faceSdfMaterials = Array.Empty<Material>();
        private Material[] _faceFrontMaterials = Array.Empty<Material>();

        public void Setup(Transform head, Transform directionalLight)
        {
            _head = head != null ? head : throw new ArgumentNullException(nameof(head));
            _directionalLight = directionalLight != null
                ? directionalLight
                : throw new ArgumentNullException(nameof(directionalLight));
            var light = directionalLight.GetComponent<Sekai.SekaiDirectionalLight>();
            _lightData = light != null ? light.CharacterDirectionalLightData : null;
            CacheMaterials();
            Apply();
        }

        private void CacheMaterials()
        {
            var faceSdf = new HashSet<Material>();
            var faceFront = new HashSet<Material>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    if (material == null) continue;
                    if (material.HasProperty(FaceFrontId)) faceFront.Add(material);
                    if (material.HasProperty(HeadDotLightId) &&
                        material.IsKeywordEnabled(FaceSdfKeyword))
                    {
                        faceSdf.Add(material);
                    }
                }
            }
            _faceSdfMaterials = new Material[faceSdf.Count];
            faceSdf.CopyTo(_faceSdfMaterials);
            _faceFrontMaterials = new Material[faceFront.Count];
            faceFront.CopyTo(_faceFrontMaterials);
        }

        private void Apply()
        {
            if (_head == null || _directionalLight == null) return;

            var faceFront = _head.forward;
            foreach (var material in _faceFrontMaterials)
            {
                if (material != null) material.SetVector(FaceFrontId, faceFront);
            }

            var headHorizontal = NormalizeOrFallback(
                new Vector2(-_head.up.x, -_head.up.z));
            var lightForward = _directionalLight.forward;
            var lightHorizontal = NormalizeOrFallback(
                new Vector2(lightForward.x, lightForward.z));
            var headYaw = PositiveEuler(_head.rotation.eulerAngles.y);
            var lightYaw = PositiveEuler(_directionalLight.rotation.eulerAngles.y);
            var values = new Vector4(
                Vector2.Dot(headHorizontal, lightHorizontal),
                1f - Mathf.Abs(Mathf.Abs(lightYaw - headYaw) - 180f) / 180f,
                0f,
                0f);
            var limiter = _lightData == null || _lightData.UseFaceShadowLimiter;
            var rangeLimit = _lightData != null ? _lightData.FaceShadowLimitRange : 0f;
            foreach (var material in _faceSdfMaterials)
            {
                if (material == null) continue;
                material.SetVector(HeadDotLightId, values);
                if (material.HasProperty(RangeLimitId))
                    material.SetFloat(RangeLimitId, rangeLimit);
                if (material.HasProperty(UseLimiterId))
                    material.SetFloat(UseLimiterId, limiter ? 1f : 0f);
                if (limiter) material.EnableKeyword(FaceRangeKeyword);
                else material.DisableKeyword(FaceRangeKeyword);
            }
        }

        private static Vector2 NormalizeOrFallback(Vector2 value)
        {
            return value.sqrMagnitude > 1e-8f ? value.normalized : Vector2.up;
        }

        private static float PositiveEuler(float value)
        {
            value %= 360f;
            return value < 0f ? value + 360f : value;
        }

        private void LateUpdate() => Apply();
    }
}
