using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Core;
using Sekai.Core.Live;
using Sekai.Rendering;
using Sekai.Scripts.Live.Character;
using UTJ;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Haruki.MV
{
    [Serializable]
    public sealed class MvCharacterLoadSpec
    {
        public int characterId;
        public string bodyBundleName;
        public string bodyAssetName = "body";
        public string bodyColorBundleName;
        public string faceBundleName;
        public string faceAssetName = "face";
        public string headOptionalBundleName;
        public string headOptionalAssetName = "head_optional";
        public string headOptionalColorBundleName;
        public string headOptionalPart;
        public string timelineBindingName;
        public string standaloneMotionBundleName;
        public string[] standaloneMotionAssetNames = Array.Empty<string>();
        public bool isFigureMan;
        public float characterHeight;
        public float heelOffset;
        public bool overrideSkinColors;
        public Color defaultSkinColor;
        public Color shadow1SkinColor;
        public Color shadow2SkinColor;
    }

    public sealed class MvCharacterInstance
    {
        private readonly List<PlayableGraph> _motionGraphs = new List<PlayableGraph>();

        internal MvCharacterInstance(
            GameObject root,
            Animator animator,
            MvWaterEyeState waterEye,
            float heightMeters,
            float heelOffset)
        {
            Root = root;
            Animator = animator;
            WaterEye = waterEye;
            HeightMeters = heightMeters;
            HeelOffset = heelOffset;
        }

        public GameObject Root { get; }
        public Animator Animator { get; }
        public MvWaterEyeState WaterEye { get; }
        public float HeightMeters { get; }
        public float HeelOffset { get; }

        public MvMotionSequence BindStandaloneMotion(IReadOnlyList<AnimationClip> clips)
        {
            if (Animator == null)
            {
                throw new InvalidOperationException("Character body has no Animator.");
            }
            var timelinePlayback = Root.GetComponentInParent<MvTimelinePlaybackParticipant>(true);
            if (timelinePlayback != null && timelinePlayback.DrivesAnimator(Animator))
            {
                throw new InvalidOperationException(
                    "Standalone motion cannot drive an Animator bound to the Character Timeline.");
            }

            var graph = PlayableGraph.Create($"{Root.name}.StandaloneMotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var sequence = Root.AddComponent<MvMotionSequence>();
            sequence.Initialize(graph, clips);
            var output = AnimationPlayableOutput.Create(graph, "Body", Animator);
            output.SetSourcePlayable(sequence.Mixer);
            graph.Play();
            _motionGraphs.Add(graph);
            return sequence;
        }

        internal void Dispose()
        {
            foreach (var graph in _motionGraphs)
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
            _motionGraphs.Clear();
        }
    }

    public sealed class MvCharacterNode : IDisposable
    {
        private readonly MvBundleSetLoader _bundles;
        private readonly IDictionary<string, UnityEngine.Object> _bindings;
        private readonly Transform _root;
        private readonly List<MvCharacterInstance> _characters =
            new List<MvCharacterInstance>();
        private readonly List<MusicItemModel> _musicItems =
            new List<MusicItemModel>();

        public MvCharacterNode(
            MvBundleSetLoader bundles,
            IDictionary<string, UnityEngine.Object> bindings,
            Transform root)
        {
            _bundles = bundles ?? throw new ArgumentNullException(nameof(bundles));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
        }

        public IReadOnlyList<MvCharacterInstance> Characters => _characters;
        public IReadOnlyList<MusicItemModel> MusicItems => _musicItems;

        public void Load(
            MusicVideoData mvData,
            IReadOnlyList<MvCharacterLoadSpec> specs,
            Transform directionalLight)
        {
            if (mvData == null)
            {
                throw new ArgumentNullException(nameof(mvData));
            }
            var infos = mvData.characterInfos ?? Array.Empty<MusicVideoCharacterInfo>();
            if (infos.Length == 0)
            {
                throw new InvalidOperationException($"MV {mvData.id} has no character slots.");
            }

            if (!_bundles.ContainsBundle(MvOfficialRuntimeData.WaterEyePresetBundleName))
            {
                throw new InvalidOperationException(
                    $"MV {mvData.id} source set omits the official WaterEye preset bundle.");
            }
            var waterEyeTable = _bundles
                .LoadAllAssets<WaterEyePresetTable>(MvOfficialRuntimeData.WaterEyePresetBundleName)
                .FirstOrDefault(table => table != null) ??
                throw new InvalidOperationException(
                    "Official WaterEye preset bundle contains no WaterEyePresetTable.");
            WaterEyePresetSettings.Setup(waterEyeTable);

            var mainCount = MvOfficialRuntimeData.MainCharacterCount(mvData);
            for (var index = 0; index < infos.Length; index++)
            {
                var info = infos[index] ?? throw new InvalidOperationException(
                    $"MV {mvData.id} character slot {index} is null.");
                var spec = specs != null && index < specs.Count ? specs[index] : null;
                spec = ResolveSpec(info, spec);
                var bindingName = ResolveCharacterKey(
                    info,
                    spec,
                    index,
                    mainCount);

                var body = _bundles.CreatePrefabInstance(
                    new MvPrefabLoadRequest
                    {
                        bundleName = spec.bodyBundleName,
                        assetName = DefaultAssetName(spec.bodyAssetName, "body"),
                    },
                    _root,
                    bindingName);
                if (!string.IsNullOrWhiteSpace(spec.bodyColorBundleName))
                {
                    ApplyColorVariation(
                        body,
                        _bundles.LoadAllAssets<Texture2D>(spec.bodyColorBundleName));
                }
                var face = _bundles.CreatePrefabInstance(
                    new MvPrefabLoadRequest
                    {
                        bundleName = spec.faceBundleName,
                        assetName = DefaultAssetName(spec.faceAssetName, "face"),
                    });
                AttachSkinnedPart(body, face);

                if (!string.IsNullOrWhiteSpace(spec.headOptionalBundleName))
                {
                    var headOptional = _bundles.CreatePrefabInstance(
                        new MvPrefabLoadRequest
                        {
                            bundleName = spec.headOptionalBundleName,
                            assetName = DefaultAssetName(
                                spec.headOptionalAssetName,
                                "head_optional"),
                        });
                    if (!string.IsNullOrWhiteSpace(spec.headOptionalColorBundleName))
                    {
                        ApplyColorVariation(
                            headOptional,
                            _bundles.LoadAllAssets<Texture2D>(
                                spec.headOptionalColorBundleName));
                    }
                    AttachHeadOptional(
                        body,
                        headOptional,
                        spec.headOptionalPart,
                        info.face);
                }

                SetLayerRecursively(body, MvRecoveredCameraResources.MainCharacterLayer);

                SetupSpringRuntime(body);

                var heightMeters = MvOfficialRuntimeData.CharacterHeightMeters(
                    spec.characterHeight);

                var animator = body.GetComponent<Animator>() ??
                    body.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        $"Character body bundle '{spec.bodyBundleName}' has no Animator.");
                }
                animator.applyRootMotion = false;
                var head = FindDescendant(body.transform, "Head");
                foreach (var hair in body.GetComponentsInChildren<SekaiCharacterHair>(true))
                {
                    hair.Setup(head, info.useHairShadow);
                }
                foreach (var eye in body.GetComponentsInChildren<SekaiCharacterEye>(true))
                {
                    eye.Setup();
                }
                var waterEye = body.GetComponent<MvWaterEyeState>() ??
                    body.AddComponent<MvWaterEyeState>();
                var waterEyeMaterials = body.GetComponent<CharacterEyeMaterialController>() ??
                    body.AddComponent<CharacterEyeMaterialController>();
                waterEyeMaterials.Setup(body);
                waterEye.Setup(waterEyeMaterials);
                MvPlayerRenderSettings.Apply(body);
                ConfigureCharacterMaterials(body, index, spec);
                var faceLighting = body.GetComponent<SekaiCharacterFaceLighting>() ??
                    body.AddComponent<SekaiCharacterFaceLighting>();
                faceLighting.Setup(head, directionalLight);
                body.SetActive(!info.isLoadInActive);
                var character = new MvCharacterInstance(
                    body,
                    animator,
                    waterEye,
                    heightMeters,
                    spec.heelOffset);
                BindReflectionOffAll(_bindings, body);
                if (spec.standaloneMotionAssetNames != null &&
                    spec.standaloneMotionAssetNames.Length > 0)
                {
                    var motionBundleName = string.IsNullOrWhiteSpace(
                        spec.standaloneMotionBundleName)
                        ? MvOfficialRuntimeData.ResolveTimelineBundleName(
                            mvData.id,
                            "Character",
                            _bundles.ContainsBundle)
                        : spec.standaloneMotionBundleName;
                    var availableClips = _bundles.LoadAllAssets<AnimationClip>(motionBundleName)
                        .Where(clip => clip != null)
                        .ToDictionary(clip => clip.name, StringComparer.Ordinal);
                    var clips = spec.standaloneMotionAssetNames.Select(name =>
                        availableClips.TryGetValue(name, out var clip)
                            ? clip
                            : throw new InvalidOperationException(
                                $"Standalone motion '{name}' was not found in '{motionBundleName}'."))
                        .ToArray();
                    character.BindStandaloneMotion(clips);
                }
                else
                {
                    if (HasCharacterTrack(_bindings, bindingName))
                    {
                        BindCharacterAliases(_bindings, bindingName, body);
                    }
                    else if (!(info.isLoadInActive && info.isInsertCharacter))
                    {
                        Debug.LogWarning(
                            $"Character Timeline has no track for '{bindingName}'.");
                    }
                }
                var trackIndex = info.isInsertCharacter
                    ? index - mainCount
                    : index;
                BindCharacterAuxiliaryTracks(
                    _bindings,
                    body,
                    waterEye,
                    trackIndex,
                    info.isInsertCharacter);
                LoadMusicItems(
                    info,
                    index,
                    trackIndex,
                    heightMeters,
                    spec.heelOffset);
                _characters.Add(character);
            }
        }

        public MvCameraHeightData CreateCameraHeightData(MusicVideoData mvData)
        {
            if (mvData == null)
            {
                throw new ArgumentNullException(nameof(mvData));
            }
            if (_characters.Count != (mvData.characterInfos?.Length ?? 0))
            {
                throw new InvalidOperationException("Character node is not loaded for this MVData.");
            }

            return MvOfficialRuntimeData.CreateCameraHeightData(
                _characters.Select(character => character.HeightMeters).ToArray(),
                _characters.Select(character => character.HeelOffset).ToArray(),
                mvData.characterInfos);
        }

        public void Dispose()
        {
            foreach (var character in _characters)
            {
                character.Dispose();
            }
            _characters.Clear();
            _musicItems.Clear();
        }

        private void LoadMusicItems(
            MusicVideoCharacterInfo info,
            int formationIndex,
            int trackIndex,
            float characterHeight,
            float heelOffset)
        {
            var itemInfos = info.musicItemInfos ?? Array.Empty<MusicVideoItemInfo>();
            for (var partIndex = 0; partIndex < itemInfos.Length; partIndex++)
            {
                var itemInfo = itemInfos[partIndex];
                if (itemInfo == null || itemInfo.id <= 0) continue;
                var bundleName = MvOfficialRuntimeData.MusicItemBundleName(itemInfo.id);
                if (!_bundles.ContainsBundle(bundleName))
                {
                    throw new InvalidOperationException(
                        $"Character {info.id} requires music-item bundle '{bundleName}'.");
                }
                var root = _bundles.CreatePrefabInstance(
                    new MvPrefabLoadRequest
                    {
                        bundleName = bundleName,
                        assetName = "item",
                    },
                    _root,
                    $"MusicItem{trackIndex}_{partIndex}");
                SetLayerRecursively(root, MvRecoveredCameraResources.MainCharacterLayer);
                var model = root.GetComponent<MusicItemModel>() ??
                    root.AddComponent<MusicItemModel>();
                model.FormationId = formationIndex;
                model.SetUseNonDefaultShader(itemInfo.useNonDefaultShader);
                model.Setup(characterHeight, heelOffset);
                root.SetActive(!info.isLoadInActive);
                BindMusicItemTracks(
                    _bindings,
                    root,
                    model,
                    trackIndex,
                    partIndex,
                    info.isInsertCharacter);
                _musicItems.Add(model);
            }
        }

        public static void BindMusicItemTracks(
            IDictionary<string, UnityEngine.Object> bindings,
            GameObject musicItem,
            MusicItemModel model,
            int trackIndex,
            int partIndex,
            bool isInsert)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (musicItem == null) throw new ArgumentNullException(nameof(musicItem));
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (trackIndex < 0) throw new ArgumentOutOfRangeException(nameof(trackIndex));
            if (partIndex < 0) throw new ArgumentOutOfRangeException(nameof(partIndex));

            if (partIndex == 0)
                BindIfDeclared(bindings, $"MusicItem{trackIndex}_0", musicItem);
            var suffix = isInsert ? "_insert" : string.Empty;
            BindIfDeclared(
                bindings,
                $"MusicItem{trackIndex}_{partIndex}_Opacity{suffix}",
                model);
            BindIfDeclared(
                bindings,
                $"MusicItem{trackIndex}_{partIndex}_UvScroll{suffix}",
                model);
        }

        public static void AttachSkinnedPart(GameObject body, GameObject part)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            var bodyRenderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
            var faceRenderers = part.GetComponentsInChildren<SkinnedMeshRenderer>();
            var faceRenderer = faceRenderers.FirstOrDefault(
                renderer => string.Equals(renderer.name, "Face", StringComparison.Ordinal));
            if (bodyRenderer == null || faceRenderer == null ||
                bodyRenderer.rootBone == null || faceRenderer.rootBone == null)
            {
                throw new InvalidOperationException(
                    "Official face graft requires a body renderer and a face renderer named 'Face'.");
            }

            var bodyNeck = FindDescendant(bodyRenderer.rootBone, "Neck");
            var bodyHead = FindDescendant(bodyRenderer.rootBone, "Head");
            var faceNeck = FindDescendant(faceRenderer.rootBone, "Neck");
            var faceHead = FindDescendant(faceRenderer.rootBone, "Head");
            if (bodyNeck == null || bodyHead == null ||
                faceNeck == null || faceHead == null || bodyNeck.parent == null)
            {
                throw new InvalidOperationException(
                    "Official face graft requires Neck and Head in both skeletons.");
            }

            MoveImmediateChildren(bodyHead, faceHead);
            var targetChildren = new List<Transform>();
            for (var index = 0; index < faceNeck.childCount; index++)
            {
                var child = faceNeck.GetChild(index);
                if (child.name.EndsWith("_target", StringComparison.Ordinal))
                {
                    targetChildren.Add(child);
                }
            }
            foreach (var child in targetChildren)
            {
                child.SetParent(bodyNeck.parent, false);
            }

            foreach (var renderer in faceRenderers)
            {
                renderer.transform.SetParent(bodyRenderer.transform.parent, false);
            }
            faceRenderer.rootBone.SetParent(bodyNeck.parent, false);
            CopyLocalTransform(bodyNeck, faceNeck);
            CopyLocalTransform(bodyHead, faceHead);

            var bodyBones = bodyRenderer.bones;
            for (var index = 0; index < bodyBones.Length; index++)
            {
                var bone = bodyBones[index];
                if (bone == null) continue;
                if (string.Equals(bone.name, bodyNeck.name, StringComparison.Ordinal))
                {
                    bodyBones[index] = faceNeck;
                }
                else if (string.Equals(bone.name, bodyHead.name, StringComparison.Ordinal))
                {
                    bodyBones[index] = faceHead;
                }
            }

            bodyHead.SetParent(null, false);
            bodyNeck.SetParent(null, false);
            bodyRenderer.bones = bodyBones;
            foreach (var renderer in faceRenderers)
            {
                renderer.rootBone = bodyRenderer.rootBone;
            }

            RebindConstraints(body, 1f);
            DestroyObject(bodyHead.gameObject, delayedInPlayMode: true);
            DestroyObject(bodyNeck.gameObject, delayedInPlayMode: true);
            DestroyObject(part, delayedInPlayMode: false);
        }

        public static void AttachHeadOptional(
            GameObject body,
            GameObject headOptional,
            string mountPart,
            string faceKey = null)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (headOptional == null) throw new ArgumentNullException(nameof(headOptional));
            if (string.IsNullOrWhiteSpace(mountPart))
            {
                DestroyObject(headOptional, delayedInPlayMode: false);
                throw new InvalidOperationException(
                    "A head_optional bundle requires its official MasterCostume3DModel.part mount.");
            }

            var bodyRenderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
            var mount = bodyRenderer?.rootBone == null
                ? null
                : FindDescendant(bodyRenderer.rootBone, mountPart);
            if (mount == null)
            {
                DestroyObject(headOptional, delayedInPlayMode: false);
                throw new InvalidOperationException(
                    $"Head optional mount '{mountPart}' does not exist in the body skeleton.");
            }

            headOptional.transform.SetParent(mount, false);
            var controllers = headOptional.GetComponentsInChildren<
                CharacterAccessoryTransformController>(true);
            if (controllers.Length > 0 && !string.IsNullOrWhiteSpace(faceKey))
            {
                foreach (var controller in controllers)
                {
                    controller.ApplyCharacterAccessoryTransformData(faceKey);
                }
            }
            else
            {
                // When no CharacterAccessoryTransformController is authored on the
                // mount, the official fallback clears position/rotation and keeps
                // the prefab scale.
                headOptional.transform.localPosition = Vector3.zero;
                headOptional.transform.localEulerAngles = Vector3.zero;
            }
        }

        public static void ApplyColorVariation(
            GameObject part,
            IReadOnlyList<Texture2D> textures)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (textures == null) throw new ArgumentNullException(nameof(textures));

            var byProperty = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (var texture in textures.Where(texture => texture != null))
            {
                var property = ColorVariationProperty(texture.name);
                if (property == null)
                {
                    continue;
                }
                if (byProperty.ContainsKey(property))
                {
                    throw new InvalidOperationException(
                        $"Color variation contains more than one texture for '{property}'.");
                }
                byProperty.Add(property, texture);
            }
            if (byProperty.Count == 0)
            {
                throw new InvalidOperationException(
                    "Color variation bundle has no official C/S/H texture set.");
            }

            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    if (material == null) continue;
                    foreach (var entry in byProperty)
                    {
                        if (material.HasProperty(entry.Key))
                        {
                            material.SetTexture(entry.Key, entry.Value);
                        }
                    }
                }
            }
        }

        public static string ColorVariationProperty(string textureName)
        {
            if (string.IsNullOrWhiteSpace(textureName)) return null;
            if (textureName.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
                return "_MainTex";
            if (textureName.EndsWith("_S", StringComparison.OrdinalIgnoreCase))
                return "_ShadowTex";
            if (textureName.EndsWith("_H", StringComparison.OrdinalIgnoreCase))
                return "_ValueTex";
            return null;
        }

        public static void RebindConstraints(GameObject model, float heightRate)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (heightRate <= 0f || float.IsNaN(heightRate) || float.IsInfinity(heightRate))
            {
                throw new ArgumentOutOfRangeException(nameof(heightRate));
            }

            var transforms = model.GetComponentsInChildren<Transform>(true)
                .GroupBy(transform => transform.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var constraints = model.GetComponentsInChildren<Behaviour>(true)
                .OfType<IConstraint>();
            foreach (var constraint in constraints)
            {
                for (var index = 0; index < constraint.sourceCount; index++)
                {
                    var source = constraint.GetSource(index);
                    if (source.sourceTransform != null &&
                        transforms.TryGetValue(source.sourceTransform.name, out var rebound))
                    {
                        source.sourceTransform = rebound;
                        constraint.SetSource(index, source);
                    }
                    if (constraint is ParentConstraint parent)
                    {
                        parent.SetTranslationOffset(
                            index,
                            parent.GetTranslationOffset(index) * heightRate);
                    }
                }
            }
        }

        internal static void ConfigureCharacterMaterials(
            GameObject characterRoot,
            int formationIndex,
            MvCharacterLoadSpec spec = null)
        {
            if (characterRoot == null)
            {
                throw new ArgumentNullException(nameof(characterRoot));
            }
            if (formationIndex < 0 || formationIndex >= SekaiCharacterAmbientLight.FormationCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(formationIndex));
            }

            var materials = new HashSet<Material>();
            foreach (var renderer in characterRoot.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    if (material != null) materials.Add(material);
                }
            }

            foreach (var material in materials)
            {
                SetFloatIfPresent(material, "_FormationId", formationIndex);
                SetFloatIfPresent(material, "_CharacterId", formationIndex);
                if (spec?.overrideSkinColors == true)
                {
                    SetColorIfPresent(material, "_DefaultSkinColor", spec.defaultSkinColor);
                    SetColorIfPresent(material, "_Shadow1SkinColor", spec.shadow1SkinColor);
                    SetColorIfPresent(material, "_Shadow2SkinColor", spec.shadow2SkinColor);
                }

                var name = material.name ?? string.Empty;
                var isBody = name.StartsWith("mtl_bdy_", StringComparison.OrdinalIgnoreCase);
                var isFace = name.StartsWith("mtl_chr_00", StringComparison.OrdinalIgnoreCase);
                var isEyebrow = name.StartsWith("mtl_chr_Eyebrow_", StringComparison.OrdinalIgnoreCase);
                var isEyelash = name.StartsWith("mtl_chr_Eyelash_", StringComparison.OrdinalIgnoreCase);
                var isToon = material.shader != null &&
                    (material.shader.name == "Sekai/Live/Character/Toon-v3" ||
                     material.shader.name == "Sekai/Live/Character/Toon");
                if (!isToon)
                {
                    continue;
                }

                SetFloatIfPresent(
                    material,
                    "_UseValueTex",
                    material.GetTexture("_ValueTex") != null ? 1f : 0f);
                SetFloatIfPresent(material, "_UseEyelash", isEyelash ? 1f : 0f);
                SetFloatIfPresent(material, "_UseSkinColor", isBody || isFace || isEyebrow ? 1f : 0f);
                SetFloatIfPresent(material, "_SkinMaskMode", isBody ? 1f : 0f);
                SetFloatIfPresent(material, "_FaceSdfMirror", 1f);
                if ((isFace || isEyebrow) && material.IsKeywordEnabled("_UseFaceSDF"))
                {
                    SetFloatIfPresent(material, "_UseFaceShadowLimiter", 1f);
                    if (material.HasProperty("_RangeLimit") && material.GetFloat("_RangeLimit") <= 0f)
                    {
                        // Captured MV character draws use the official limiter at 0.25.
                        material.SetFloat("_RangeLimit", 0.25f);
                    }
                }
            }
        }

        private static void SetFloatIfPresent(Material material, string name, float value)
        {
            if (material.HasProperty(name)) material.SetFloat(name, value);
        }

        private static void SetColorIfPresent(Material material, string name, Color value)
        {
            if (material.HasProperty(name)) material.SetColor(name, value);
        }

        private static void MoveImmediateChildren(Transform source, Transform destination)
        {
            while (source.childCount > 0)
            {
                var child = source.GetChild(0);
                var position = child.localPosition;
                var eulerAngles = child.localEulerAngles;
                child.SetParent(destination, false);
                child.localPosition = position;
                child.localEulerAngles = eulerAngles;
            }
        }

        private static void CopyLocalTransform(Transform source, Transform destination)
        {
            destination.localPosition = source.localPosition;
            destination.localEulerAngles = source.localEulerAngles;
        }

        private static void DestroyObject(UnityEngine.Object value, bool delayedInPlayMode)
        {
            if (value == null) return;

            if (Application.isPlaying && delayedInPlayMode)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }

        public static string CharacterTrackName(
            int formationIndex,
            int mainCharacterCount,
            bool isInsert)
        {
            if (formationIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(formationIndex));
            }
            if (mainCharacterCount < 0 || (isInsert && formationIndex < mainCharacterCount))
            {
                throw new ArgumentOutOfRangeException(nameof(mainCharacterCount));
            }

            var trackIndex = isInsert
                ? formationIndex - mainCharacterCount
                : formationIndex;
            return $"Character{trackIndex}" + (isInsert ? "_insert" : string.Empty);
        }

        public static string ResolveCharacterKey(
            MusicVideoCharacterInfo info,
            MvCharacterLoadSpec spec,
            int formationIndex,
            int mainCharacterCount)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (!string.IsNullOrWhiteSpace(spec.timelineBindingName))
            {
                return spec.timelineBindingName;
            }
            if (info.motionInfo?.motionType == MotionType.Gender)
            {
                return $"Character{formationIndex}_{(spec.isFigureMan ? "Male" : "Female")}";
            }
            return CharacterTrackName(
                formationIndex,
                mainCharacterCount,
                info.isInsertCharacter);
        }

        public static void BindCharacterAliases(
            IDictionary<string, UnityEngine.Object> bindings,
            string characterKey,
            GameObject character)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }
            if (string.IsNullOrWhiteSpace(characterKey))
            {
                throw new ArgumentException("Character binding key is required.", nameof(characterKey));
            }
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            bindings[characterKey] = character;
            bindings[characterKey + "_MV"] = character;
        }

        public static void BindReflectionOffAll(
            IDictionary<string, UnityEngine.Object> bindings,
            GameObject character)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            bindings["ReflectionOff_All"] = character;
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (layer < 0 || layer > 31) throw new ArgumentOutOfRangeException(nameof(layer));
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        public static void BindCharacterAuxiliaryTracks(
            IDictionary<string, UnityEngine.Object> bindings,
            GameObject character,
            MvWaterEyeState waterEye,
            int trackIndex,
            bool isInsert)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (trackIndex < 0) throw new ArgumentOutOfRangeException(nameof(trackIndex));

            var characterPrefix = $"Character{trackIndex}";
            var characterSuffix = isInsert ? "_insert" : string.Empty;
            BindIfDeclared(bindings, characterPrefix + "_MeshOff" + characterSuffix, character);
            BindIfDeclared(bindings, characterPrefix + "_ReflectionOff" + characterSuffix, character);
            BindIfDeclared(bindings, characterPrefix + "_HeelOffsetOff" + characterSuffix, character);
            BindIfDeclared(bindings, characterPrefix + "_DrawCameraSelect" + characterSuffix, character);
            BindIfDeclared(
                bindings,
                $"Water Eye Track {trackIndex}" + characterSuffix,
                waterEye != null ? waterEye : character);
            BindIfDeclared(
                bindings,
                $"Eye Flipbook Track {trackIndex}" + characterSuffix,
                character);
            var springSuffix = isInsert ? "#insert" : string.Empty;
            BindIfDeclared(
                bindings,
                $"Spring Bone Slow Track {trackIndex}" + springSuffix,
                character);
            BindIfDeclared(
                bindings,
                $"Spring Bone Control Track {trackIndex}" + springSuffix,
                character);
        }

        private static void BindIfDeclared(
            IDictionary<string, UnityEngine.Object> bindings,
            string key,
            UnityEngine.Object target)
        {
            if (bindings.ContainsKey(key)) bindings[key] = target;
        }

        public static bool HasCharacterTrack(
            IDictionary<string, UnityEngine.Object> bindings,
            string characterKey)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }
            if (string.IsNullOrWhiteSpace(characterKey))
            {
                throw new ArgumentException("Character binding key is required.", nameof(characterKey));
            }

            return bindings.ContainsKey(characterKey) ||
                bindings.ContainsKey(characterKey + "_MV");
        }

        private MvCharacterLoadSpec ResolveSpec(
            MusicVideoCharacterInfo info,
            MvCharacterLoadSpec input)
        {
            var spec = input ?? new MvCharacterLoadSpec();
            if (string.IsNullOrWhiteSpace(spec.faceBundleName) && !string.IsNullOrWhiteSpace(info.face))
            {
                spec.faceBundleName = MvOfficialRuntimeData.ResolveCharacterFaceBundleName(
                    info.face,
                    _bundles.ContainsBundle);
            }
            if (string.IsNullOrWhiteSpace(spec.bodyBundleName) && !string.IsNullOrWhiteSpace(info.body))
            {
                spec.bodyBundleName = MvOfficialRuntimeData.ResolveCharacterBodyBundleName(
                    info.body,
                    _bundles.FindSingleBundleByPrefix);
            }
            if (string.IsNullOrWhiteSpace(spec.headOptionalBundleName) &&
                !string.IsNullOrWhiteSpace(info.headOptional))
            {
                spec.headOptionalBundleName =
                    MvOfficialRuntimeData.ResolveCharacterHeadOptionalBundleName(
                        info.headOptional,
                        _bundles.ContainsBundle);
            }
            if (string.IsNullOrWhiteSpace(spec.bodyColorBundleName) &&
                !string.IsNullOrWhiteSpace(info.colorVariation))
            {
                spec.bodyColorBundleName =
                    MvOfficialRuntimeData.ResolveCharacterBodyColorBundleName(
                        info.colorVariation,
                        _bundles.ContainsBundle);
            }
            if (string.IsNullOrWhiteSpace(spec.headOptionalColorBundleName) &&
                !string.IsNullOrWhiteSpace(info.headOptionalColorVariation))
            {
                spec.headOptionalColorBundleName =
                    MvOfficialRuntimeData.ResolveCharacterHeadOptionalColorBundleName(
                        info.headOptionalColorVariation,
                        _bundles.ContainsBundle);
            }
            if (string.IsNullOrWhiteSpace(spec.bodyBundleName) ||
                string.IsNullOrWhiteSpace(spec.faceBundleName))
            {
                throw new InvalidOperationException(
                    $"Character {info.id} requires runtime body and face bundles.");
            }
            return spec;
        }

        private static string DefaultAssetName(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            for (var index = 0; index < root.childCount; index++)
            {
                var match = FindDescendant(root.GetChild(index), name);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private static void SetupSpringRuntime(GameObject character)
        {
            var spheres = character.GetComponentsInChildren<SpringSphereCollider>(true);
            var capsules = character.GetComponentsInChildren<SpringCapsuleCollider>(true);
            foreach (var bone in character.GetComponentsInChildren<Sekai.SekaiSpringBone>(true))
            {
                var selectedSpheres = new HashSet<SpringSphereCollider>(
                    bone.sphereColliders ?? Array.Empty<SpringSphereCollider>());
                var selectedCapsules = new HashSet<SpringCapsuleCollider>(
                    bone.capsuleColliders ?? Array.Empty<SpringCapsuleCollider>());
                AddColliderGroup(bone.colliderFlag, spheres, selectedSpheres);
                AddColliderGroup(bone.colliderFlag, capsules, selectedCapsules);
                bone.sphereColliders = selectedSpheres.Where(value => value != null).ToArray();
                bone.capsuleColliders = selectedCapsules.Where(value => value != null).ToArray();
            }
            foreach (var manager in character.GetComponentsInChildren<SpringManager>(true))
            {
                manager.Initialize();
                manager.UpdateBoneIsAnimatedStates(Array.Empty<string>());
            }
        }

        private static void AddColliderGroup<T>(
            Sekai.SekaiSpringBone.ColliderFlag flags,
            IEnumerable<T> candidates,
            ISet<T> destination)
            where T : Component
        {
            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                var name = candidate.name;
                if (((flags & Sekai.SekaiSpringBone.ColliderFlag.Hip) != 0 &&
                        name.StartsWith("CL_Hip", StringComparison.Ordinal)) ||
                    ((flags & Sekai.SekaiSpringBone.ColliderFlag.Chest) != 0 &&
                        name.StartsWith("CL_Chest", StringComparison.Ordinal)) ||
                    ((flags & Sekai.SekaiSpringBone.ColliderFlag.L_Arm) != 0 &&
                        name.StartsWith("CL_Left_Arm", StringComparison.Ordinal)) ||
                    ((flags & Sekai.SekaiSpringBone.ColliderFlag.R_Arm) != 0 &&
                        name.StartsWith("CL_Right_Arm", StringComparison.Ordinal)) ||
                    ((flags & Sekai.SekaiSpringBone.ColliderFlag.L_Elbow) != 0 &&
                        name.StartsWith("CL_Left_Elbow", StringComparison.Ordinal)) ||
                    ((flags & Sekai.SekaiSpringBone.ColliderFlag.R_Elbow) != 0 &&
                        name.StartsWith("CL_Right_Elbow", StringComparison.Ordinal)))
                {
                    destination.Add(candidate);
                }
            }
        }
    }
}
