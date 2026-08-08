using System;
using System.Collections.Generic;
using Sekai.Core;
using UnityEngine;

namespace Haruki.MV
{
    public sealed class MvStageNode : IDisposable
    {
        public static readonly IReadOnlyList<string> OverrideTextureProperties =
            Array.AsReadOnly(new[]
            {
                "_MainTex",
                "_ColorTex",
                "_LightMapTex",
                "_SubTex",
            });

        private readonly MvBundleSetLoader _bundles;
        private readonly IDictionary<string, UnityEngine.Object> _bindings;
        private readonly Transform _root;
        private readonly List<GameObject> _instances = new List<GameObject>();
        private readonly List<GameObject> _decorations = new List<GameObject>();
        private readonly List<Material> _clonedMaterials = new List<Material>();
        private readonly Dictionary<string, Texture2D> _originalTextures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public MvStageNode(
            MvBundleSetLoader bundles,
            IDictionary<string, UnityEngine.Object> bindings,
            Transform root)
        {
            _bundles = bundles ?? throw new ArgumentNullException(nameof(bundles));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
        }

        public MvResolvedStageInfo StageInfo { get; private set; }
        public GameObject BaseStage { get; private set; }
        public IReadOnlyList<GameObject> Decorations => _decorations;
        public IReadOnlyDictionary<string, Texture2D> OriginalTextures => _originalTextures;

        public void Load(
            MusicVideoData mvData,
            MusicVideoData parentMvData,
            bool isCutIn)
        {
            if (mvData == null)
            {
                throw new ArgumentNullException(nameof(mvData));
            }

            StageInfo = MvOfficialRuntimeData.ResolveStage(mvData, parentMvData);
            var overrideTextures = StageInfo.OverrideTexture
                ? LoadOverrideTextures(
                    mvData.id,
                    StageInfo.OverrideAdditionalStageTexture
                        ? StageInfo.AdditionalOverrideTextureMusicVideoIds
                        : Array.Empty<int>())
                : new Dictionary<string, Texture2D>(StringComparer.Ordinal);

            if (!StageInfo.SkipBaseStageLoad)
            {
                BaseStage = _bundles.InstantiatePrefab(
                    new MvPrefabLoadRequest
                    {
                        bundleName = MvOfficialRuntimeData.StageBundleName(StageInfo.Id),
                        assetName = "stage",
                    },
                    _root,
                    "Stage");
                _instances.Add(BaseStage);
                _bindings[BaseStage.name] = BaseStage;
                ApplyKnownTextureOverrides(
                    BaseStage,
                    overrideTextures,
                    isCutIn,
                    _clonedMaterials,
                    _originalTextures);
                MvOfficialObjectBinding.BindControlGroups(BaseStage, _bindings);
            }

            var decorationIndex = 0;
            decorationIndex = LoadDecorations(
                StageInfo.StageDecorationInfos,
                decorationIndex,
                overrideTextures,
                isCutIn,
                false);
            LoadDecorations(
                StageInfo.AdditionalStageDecorationInfos,
                decorationIndex,
                overrideTextures,
                isCutIn,
                StageInfo.OverrideTexture);

            if (StageInfo.EnablePlanarReflection)
            {
                var waterSurface = BaseStage == null
                    ? null
                    : MvOfficialObjectBinding.FindFirstComponent(
                        BaseStage,
                        "WaterSurfaceController",
                        true);
                for (var index = 0; waterSurface == null && index < _decorations.Count; index++)
                {
                    waterSurface = MvOfficialObjectBinding.FindFirstComponent(
                        _decorations[index],
                        "WaterSurfaceController",
                        true);
                }
                if (waterSurface != null)
                {
                    _bindings["WaterSurface"] = waterSurface;
                }
            }
        }

        public void Dispose()
        {
            for (var index = _instances.Count - 1; index >= 0; index--)
            {
                if (_instances[index] == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_instances[index]);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_instances[index]);
                }
            }
            _instances.Clear();
            _decorations.Clear();
            foreach (var material in _clonedMaterials)
            {
                if (material == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(material);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
            _clonedMaterials.Clear();
            _originalTextures.Clear();
            BaseStage = null;
            StageInfo = null;
        }

        public static void ApplyKnownTextureOverrides(
            GameObject root,
            IReadOnlyDictionary<string, Texture2D> replacements,
            bool cloneMaterials)
        {
            ApplyKnownTextureOverrides(root, replacements, cloneMaterials, null, null);
        }

        public static void ApplyKnownTextureOverrides(
            GameObject root,
            IReadOnlyDictionary<string, Texture2D> replacements,
            bool cloneMaterials,
            IDictionary<string, Texture2D> originalTextures)
        {
            ApplyKnownTextureOverrides(
                root,
                replacements,
                cloneMaterials,
                null,
                originalTextures);
        }

        private static void ApplyKnownTextureOverrides(
            GameObject root,
            IReadOnlyDictionary<string, Texture2D> replacements,
            bool cloneMaterials,
            ICollection<Material> clonedMaterials,
            IDictionary<string, Texture2D> originalTextures)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            if (replacements == null || replacements.Count == 0)
            {
                return;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                if (cloneMaterials)
                {
                    for (var index = 0; index < materials.Length; index++)
                    {
                        if (materials[index] != null)
                        {
                            materials[index] = new Material(materials[index]);
                            clonedMaterials?.Add(materials[index]);
                        }
                    }
                    renderer.sharedMaterials = materials;
                }

                foreach (var material in materials)
                {
                    ApplyKnownTextureOverrides(material, replacements, originalTextures);
                }
            }
        }

        private int LoadDecorations(
            IReadOnlyList<MusicVideoStageDecorationInfo> infos,
            int startIndex,
            IReadOnlyDictionary<string, Texture2D> overrideTextures,
            bool isCutIn,
            bool applyOverrideTextures)
        {
            if (infos == null)
            {
                return startIndex;
            }

            for (var index = 0; index < infos.Count; index++)
            {
                var info = infos[index];
                if (info == null)
                {
                    continue;
                }
                var objectName = $"StageDecoration{startIndex++}";
                var decoration = _bundles.InstantiatePrefab(
                    new MvPrefabLoadRequest
                    {
                        bundleName = MvOfficialRuntimeData.StageDecorationBundleName(info.id),
                        assetName = "decoration",
                    },
                    _root,
                    objectName);
                _instances.Add(decoration);
                _decorations.Add(decoration);
                _bindings[objectName] = decoration;
                if (applyOverrideTextures)
                {
                    ApplyKnownTextureOverrides(
                        decoration,
                        overrideTextures,
                        isCutIn,
                        _clonedMaterials,
                        _originalTextures);
                }
                MvOfficialObjectBinding.BindStageDecorationTargets(decoration, _bindings);
                MvOfficialObjectBinding.BindControlGroups(decoration, _bindings);
            }
            return startIndex;
        }

        private Dictionary<string, Texture2D> LoadOverrideTextures(
            int mvId,
            IReadOnlyList<int> additionalMvIds)
        {
            var current = LoadOverrideTextureBundle(mvId);
            var additional = new List<IReadOnlyDictionary<string, Texture2D>>();
            if (additionalMvIds != null)
            {
                foreach (var id in additionalMvIds)
                {
                    additional.Add(LoadOverrideTextureBundle(id));
                }
            }
            return MvOfficialRuntimeData.MergeStageOverrideTextures(current, additional);
        }

        private Dictionary<string, Texture2D> LoadOverrideTextureBundle(int mvId)
        {
            var bundleName = MvOfficialRuntimeData.StageOverrideTextureBundleName(mvId);
            var result = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            if (!_bundles.ContainsBundle(bundleName))
            {
                return result;
            }
            foreach (var texture in _bundles.LoadAllAssets<Texture2D>(bundleName))
            {
                if (texture != null && !result.ContainsKey(texture.name))
                {
                    result.Add(texture.name, texture);
                }
            }
            return result;
        }

        private static void ApplyKnownTextureOverrides(
            Material material,
            IReadOnlyDictionary<string, Texture2D> replacements,
            IDictionary<string, Texture2D> originalTextures)
        {
            if (material == null || material.shader == null)
            {
                return;
            }

            foreach (var propertyName in OverrideTextureProperties)
            {
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }
                var original = material.GetTexture(propertyName) as Texture2D;
                if (original != null && replacements.TryGetValue(original.name, out var replacement))
                {
                    material.SetTexture(propertyName, replacement);
                    if (originalTextures != null && !originalTextures.ContainsKey(original.name))
                    {
                        originalTextures.Add(original.name, original);
                    }
                }
            }
        }
    }
}
