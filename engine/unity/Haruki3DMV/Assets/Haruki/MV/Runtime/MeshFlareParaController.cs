using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sekai.Core.Live
{
    public sealed class MeshFlareParaController : MonoBehaviour
    {
        private const int FlareParaCount = 3;
        private const float Distance = 0.1f;
        private const float ParameterPositionWeight = 0.01f;
        private const float ParameterScaleWeight = 0.1f;
        private const string MultiplyBlendKeyword = "_MULTIPLY_BLEND";
        private const string ShaderName = "Sekai/Live/MeshFlarePara";
        private static readonly int MainTextureId =
            Shader.PropertyToID("_MeshFlareParaMainTex");
        private static readonly int BlendSourceId =
            Shader.PropertyToID("_MeshFlareParaBlendSrc");
        private static readonly int BlendDestinationId =
            Shader.PropertyToID("_MeshFlareParaBlendDst");
        private static readonly int ColorId =
            Shader.PropertyToID("_MeshFlareParaColor");
        private static readonly int ZTestId =
            Shader.PropertyToID("_MeshFlareParaZTest");

        [SerializeField]
        private List<GameObject> _meshFlareParaObject = new List<GameObject>();

        private readonly Material[] _materials = new Material[FlareParaCount];
        private readonly bool[] _dirty = new bool[FlareParaCount];
        private readonly Vector3[] _pos = new Vector3[FlareParaCount];
        private readonly Vector2[] _scaleXY = new Vector2[FlareParaCount];
        private readonly bool[] _zTest = new bool[FlareParaCount];
        private Camera _targetCamera;

        public void Setup(Camera targetCamera, Texture2D[] texture2Ds)
        {
            _targetCamera = targetCamera != null
                ? targetCamera
                : throw new ArgumentNullException(nameof(targetCamera));
            if (_meshFlareParaObject == null || _meshFlareParaObject.Count != FlareParaCount)
            {
                throw new InvalidOperationException(
                    "Official MeshFlarePara prefab must contain exactly three flare objects.");
            }
            if (texture2Ds == null || texture2Ds.Length < FlareParaCount)
            {
                throw new ArgumentException(
                    "Official MeshFlarePara texture data must contain three textures.",
                    nameof(texture2Ds));
            }

            transform.SetParent(_targetCamera.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            var shader = Shader.Find(ShaderName) ??
                throw new InvalidOperationException(
                    $"Official MeshFlarePara shader '{ShaderName}' was not found.");

            for (var order = 0; order < FlareParaCount; order++)
            {
                var flareObject = RequireObject(order);
                var renderer = flareObject.GetComponent<MeshRenderer>() ??
                    flareObject.GetComponentInChildren<MeshRenderer>(true) ??
                    throw new InvalidOperationException(
                        $"MeshFlarePara object {order} has no MeshRenderer.");
                var material = new Material(shader)
                {
                    name = $"MeshFlarePara_{order}",
                    renderQueue = 3003 - order,
                };
                material.SetTexture(MainTextureId, texture2Ds[order]);
                renderer.sharedMaterial = material;
                _materials[order] = material;
                SetupMeshTransform(flareObject.transform);
                SetActiveObj(order, false);
            }
        }

        public void SetBlendModePropertyBlock(
            int order,
            MeshFlareParaClip.MeshBlendMode blendMode)
        {
            var material = RequireMaterial(order);
            GetShaderBlendMode(blendMode, out var source, out var destination);
            material.SetInt(BlendSourceId, (int)source);
            material.SetInt(BlendDestinationId, (int)destination);
        }

        public void SetMultiBlendShaderKeyword(int order, bool enable)
        {
            var material = RequireMaterial(order);
            if (enable) material.EnableKeyword(MultiplyBlendKeyword);
            else material.DisableKeyword(MultiplyBlendKeyword);
        }

        public void SetColorPropertyBlock(int order, Color color)
        {
            color.r = Mathf.Max(0f, color.r);
            color.g = Mathf.Max(0f, color.g);
            color.b = Mathf.Max(0f, color.b);
            color.a = Mathf.Max(0f, color.a);
            RequireMaterial(order).SetColor(ColorId, color);
        }

        public void SetPositionAndScaleParams(
            int order,
            Vector3 position,
            Vector2 scaleXY,
            bool zTest)
        {
            ValidateOrder(order);
            _pos[order] = position;
            _scaleXY[order] = scaleXY;
            _zTest[order] = zTest;
            _dirty[order] = true;
        }

        public void SetActiveObj(int order, bool isActive)
        {
            RequireObject(order).SetActive(isActive);
        }

        public void SetTheta(int order, float theta)
        {
            RequireObject(order).transform.localRotation =
                Quaternion.AngleAxis(theta, Vector3.forward);
        }

        public void SetZTest(int order, bool zTest)
        {
            ValidateOrder(order);
            _zTest[order] = zTest;
            RequireMaterial(order).SetFloat(
                ZTestId,
                zTest ? (float)CompareFunction.LessEqual : (float)CompareFunction.Always);
            _dirty[order] = true;
        }

        public void SetTiling(int order, Vector2 tiling)
        {
            RequireMaterial(order).SetTextureScale(MainTextureId, tiling);
        }

        public void ResetPositionAndScale(int order)
        {
            ValidateOrder(order);
            var flareObject = RequireObject(order);
            flareObject.transform.position = Vector3.zero;
            flareObject.transform.localScale = Vector3.one;
        }

        public bool GetIsActiveObj(int order) => RequireObject(order).activeSelf;

        public void Unload()
        {
            for (var index = 0; index < _materials.Length; index++)
            {
                DestroyObject(_materials[index]);
                _materials[index] = null;
            }
            _targetCamera = null;
        }

        private void LateUpdate()
        {
            if (_targetCamera == null) return;
            for (var order = 0; order < FlareParaCount; order++)
            {
                if (!_dirty[order]) continue;
                var flare = RequireObject(order).transform;
                flare.position = _zTest[order]
                    ? _pos[order]
                    : _targetCamera.ScreenToWorldPoint(new Vector3(
                        _pos[order].x * ParameterPositionWeight * _targetCamera.pixelWidth,
                        _pos[order].y * ParameterPositionWeight * _targetCamera.pixelHeight,
                        Distance));
                var verticalExtent = 2f * Mathf.Tan(
                    _targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                flare.localScale = new Vector3(
                    verticalExtent * _scaleXY[order].x * ParameterScaleWeight,
                    verticalExtent * _scaleXY[order].y * ParameterScaleWeight,
                    1f);
                SetZTest(order, _zTest[order]);
                _dirty[order] = false;
            }
        }

        private void SetupMeshTransform(Transform flare)
        {
            flare.SetParent(transform, false);
            flare.localPosition = Vector3.forward * Distance;
            var initialScale = 2f * Mathf.Tan(
                _targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                * ParameterScaleWeight;
            flare.localScale = Vector3.one * initialScale;
            flare.localRotation = Quaternion.identity;
        }

        private static void GetShaderBlendMode(
            MeshFlareParaClip.MeshBlendMode blendMode,
            out BlendMode source,
            out BlendMode destination)
        {
            switch (blendMode)
            {
                case MeshFlareParaClip.MeshBlendMode.Overwrite:
                    source = BlendMode.One;
                    destination = BlendMode.Zero;
                    return;
                case MeshFlareParaClip.MeshBlendMode.Add:
                    source = BlendMode.One;
                    destination = BlendMode.One;
                    return;
                case MeshFlareParaClip.MeshBlendMode.TransparentAdd:
                    source = BlendMode.SrcAlpha;
                    destination = BlendMode.One;
                    return;
                case MeshFlareParaClip.MeshBlendMode.AlphaBlend:
                    source = BlendMode.SrcAlpha;
                    destination = BlendMode.OneMinusSrcAlpha;
                    return;
                case MeshFlareParaClip.MeshBlendMode.Multiply:
                    source = BlendMode.DstColor;
                    destination = BlendMode.Zero;
                    return;
                case MeshFlareParaClip.MeshBlendMode.MultiplyAdd:
                    source = BlendMode.DstColor;
                    destination = BlendMode.One;
                    return;
                default:
                    source = BlendMode.One;
                    destination = BlendMode.SrcAlpha;
                    return;
            }
        }

        private GameObject RequireObject(int order)
        {
            ValidateOrder(order);
            return _meshFlareParaObject[order] != null
                ? _meshFlareParaObject[order]
                : throw new InvalidOperationException(
                    $"MeshFlarePara object {order} is missing.");
        }

        private Material RequireMaterial(int order)
        {
            ValidateOrder(order);
            return _materials[order] != null
                ? _materials[order]
                : throw new InvalidOperationException(
                    "MeshFlareParaController.Setup must run before timeline playback.");
        }

        private static void ValidateOrder(int order)
        {
            if (order < 0 || order >= FlareParaCount)
                throw new ArgumentOutOfRangeException(nameof(order));
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            Unload();
        }
    }
}
