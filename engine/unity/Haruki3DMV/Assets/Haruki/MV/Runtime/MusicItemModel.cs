using System;
using System.Collections.Generic;
using Sekai.Rendering;
using UnityEngine;

namespace Sekai.Core
{
    [ExecuteAlways]
    public sealed class MusicItemModel : MonoBehaviour, ISekaiMusicItem
    {
        private const string OpaqueShaderName = "Sekai/Live/MusicItem/Toon";
        private const string TransparentShaderName = "Hidden/Sekai/Live/MusicItem/Toon";
        private static readonly int CharacterId = Shader.PropertyToID("_CharacterId");
        private static readonly int FormationIdProperty = Shader.PropertyToID("_FormationId");
        private static readonly int TransparencyId = Shader.PropertyToID("_Transparency");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        [Range(0f, 1f), SerializeField]
        private float opacity = 1f;

        [HideInInspector, SerializeField]
        private int formationId;

        [SerializeField]
        private Vector2 uvScale = Vector2.one;

        [SerializeField]
        private Vector2 uvOffset;

        private Renderer[] _renderers = Array.Empty<Renderer>();
        private readonly List<Material> _materials = new List<Material>();
        private bool _useNonDefaultShader;

        public int FormationId
        {
            get => formationId;
            set
            {
                formationId = value;
                UpdateFormationId();
            }
        }

        public bool IsHiding { get; private set; }
        public bool IsOpaque { get; private set; } = true;
        public bool MeshVisible { get; private set; } = true;

        private void Start()
        {
            EnsureMaterials();
            ApplyMaterialState();
        }

        public void Setup(float height, float offset)
        {
            if (height <= 0f || float.IsNaN(height) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
            SetScaleAdjust(height);
            SetPositionAdjust(offset);
            EnsureMaterials();
            ApplyMaterialState();
        }

        public void SetScaleAdjust(float height)
        {
            foreach (var target in FindDescendants("OffsetValue"))
            {
                target.localScale *= height;
            }
        }

        public void SetPositionAdjust(float offset)
        {
            foreach (var target in FindDescendants("PositionOffset"))
            {
                var position = target.localPosition;
                position.y = offset;
                target.localPosition = position;
            }
        }

        public void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);
            ApplyMaterialState();
        }

        public void UpdateMeshVisible(bool meshVisible)
        {
            MeshVisible = meshVisible;
            ApplyMaterialState();
        }

        public void SetUVScaleAndOffset(Vector2 scale, Vector2 offset)
        {
            uvScale = scale;
            uvOffset = offset;
            ApplyUvTransform();
        }

        public void SetUseNonDefaultShader(bool use)
        {
            _useNonDefaultShader = use;
            ApplyMaterialState();
        }

        private void EnsureMaterials()
        {
            if (_renderers.Length != 0)
            {
                return;
            }
            _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in _renderers)
            {
                var materials = renderer.materials;
                foreach (var material in materials)
                {
                    if (material != null) _materials.Add(material);
                }
            }
        }

        private void ApplyMaterialState()
        {
            EnsureMaterials();
            IsHiding = opacity <= 0f || !MeshVisible;
            IsOpaque = opacity >= 0.9999f;
            var opaqueShader = Shader.Find(OpaqueShaderName);
            var transparentShader = Shader.Find(TransparentShaderName);
            foreach (var material in _materials)
            {
                if (!_useNonDefaultShader && opaqueShader != null && transparentShader != null)
                {
                    material.shader = IsOpaque ? opaqueShader : transparentShader;
                }
                if (material.HasProperty(CharacterId))
                    material.SetFloat(CharacterId, formationId);
                if (material.HasProperty(FormationIdProperty))
                    material.SetFloat(FormationIdProperty, formationId);
                if (material.HasProperty(TransparencyId))
                    material.SetFloat(TransparencyId, 1f - opacity);
            }
            foreach (var renderer in _renderers)
            {
                if (renderer != null) renderer.enabled = !IsHiding;
            }
            ApplyUvTransform();
            SekaiMusicItemSettings.UnregisterTransparentMusicItem(this);
            SekaiMusicItemSettings.RegisterTransparentMusicItem(this);
        }

        private void UpdateFormationId()
        {
            EnsureMaterials();
            foreach (var material in _materials)
            {
                if (material.HasProperty(CharacterId))
                    material.SetFloat(CharacterId, formationId);
                if (material.HasProperty(FormationIdProperty))
                    material.SetFloat(FormationIdProperty, formationId);
            }
        }

        private void ApplyUvTransform()
        {
            var value = new Vector4(uvScale.x, uvScale.y, uvOffset.x, uvOffset.y);
            foreach (var material in _materials)
            {
                if (material.HasProperty(MainTexStId))
                    material.SetVector(MainTexStId, value);
            }
        }

        private IEnumerable<Transform> FindDescendants(string targetName)
        {
            foreach (var target in GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(target.name, targetName, StringComparison.Ordinal))
                    yield return target;
            }
        }

        private void OnDestroy()
        {
            SekaiMusicItemSettings.UnregisterTransparentMusicItem(this);
            foreach (var material in _materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            _materials.Clear();
        }
    }
}
