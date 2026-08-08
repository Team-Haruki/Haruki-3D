using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Core;
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
        public string faceBundleName;
        public string faceAssetName = "face";
        public string headOptionalBundleName;
        public string headOptionalAssetName = "head_optional";
        public string timelineBindingName;
        public string standaloneMotionBundleName;
        public string[] standaloneMotionAssetNames = Array.Empty<string>();
        public float characterHeight;
        public float heelOffset;
    }

    public sealed class MvCharacterInstance
    {
        private readonly List<PlayableGraph> _motionGraphs = new List<PlayableGraph>();

        internal MvCharacterInstance(
            GameObject root,
            Animator animator,
            float heightMeters,
            float heelOffset)
        {
            Root = root;
            Animator = animator;
            HeightMeters = heightMeters;
            HeelOffset = heelOffset;
        }

        public GameObject Root { get; }
        public Animator Animator { get; }
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

        public void Load(MusicVideoData mvData, IReadOnlyList<MvCharacterLoadSpec> specs)
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

            var mainCount = MvOfficialRuntimeData.MainCharacterCount(mvData);
            for (var index = 0; index < infos.Length; index++)
            {
                var info = infos[index] ?? throw new InvalidOperationException(
                    $"MV {mvData.id} character slot {index} is null.");
                var spec = specs != null && index < specs.Count ? specs[index] : null;
                spec = ResolveSpec(info, spec);
                var bindingName = !string.IsNullOrWhiteSpace(spec.timelineBindingName)
                    ? spec.timelineBindingName
                    : CharacterTrackName(index, mainCount, info.isInsertCharacter);

                var body = _bundles.InstantiatePrefab(
                    new MvPrefabLoadRequest
                    {
                        bundleName = spec.bodyBundleName,
                        assetName = DefaultAssetName(spec.bodyAssetName, "body"),
                    },
                    _root,
                    bindingName);
                var face = _bundles.InstantiatePrefab(
                    new MvPrefabLoadRequest
                    {
                        bundleName = spec.faceBundleName,
                        assetName = DefaultAssetName(spec.faceAssetName, "face"),
                    });
                AttachSkinnedPart(body, face);

                if (!string.IsNullOrWhiteSpace(spec.headOptionalBundleName))
                {
                    var headOptional = _bundles.InstantiatePrefab(
                        new MvPrefabLoadRequest
                        {
                            bundleName = spec.headOptionalBundleName,
                            assetName = DefaultAssetName(
                                spec.headOptionalAssetName,
                                "head_optional"),
                        });
                    AttachSkinnedPart(body, headOptional);
                }

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
                MvPlayerRenderSettings.Apply(body);
                body.SetActive(!info.isLoadInActive);
                var character = new MvCharacterInstance(
                    body,
                    animator,
                    heightMeters,
                    spec.heelOffset);
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

            var bodyPosition = FindDirectChild(body.transform, "Position");
            var partPosition = FindDirectChild(part.transform, "Position");
            if (bodyPosition == null || partPosition == null)
            {
                throw new InvalidOperationException(
                    "Character body and attached parts must both contain a Position root.");
            }

            var renderers = part.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var map = new Dictionary<Transform, Transform>();
            MergeHierarchy(partPosition, bodyPosition, map);
            foreach (var renderer in renderers)
            {
                renderer.bones = renderer.bones
                    .Select(bone => bone != null && map.TryGetValue(bone, out var target) ? target : bone)
                    .ToArray();
                if (renderer.rootBone != null && map.TryGetValue(renderer.rootBone, out var rootBone))
                {
                    renderer.rootBone = rootBone;
                }
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(part);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(part);
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
                spec.faceBundleName = MvOfficialRuntimeData.CharacterFaceBundleName(info.face);
            }
            if (string.IsNullOrWhiteSpace(spec.bodyBundleName) && !string.IsNullOrWhiteSpace(info.body))
            {
                spec.bodyBundleName = _bundles.FindSingleBundleByPrefix(
                    MvOfficialRuntimeData.CharacterBodyBundlePrefix(info.body));
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

        private static void MergeHierarchy(
            Transform source,
            Transform target,
            IDictionary<Transform, Transform> map)
        {
            map[source] = target;
            var children = new Transform[source.childCount];
            for (var index = 0; index < source.childCount; index++)
            {
                children[index] = source.GetChild(index);
            }

            foreach (var child in children)
            {
                var targetChild = FindDirectChild(target, child.name);
                if (targetChild != null)
                {
                    MergeHierarchy(child, targetChild, map);
                    continue;
                }

                child.SetParent(target, false);
                MapIdentity(child, map);
            }
        }

        private static void MapIdentity(
            Transform root,
            IDictionary<Transform, Transform> map)
        {
            map[root] = root;
            for (var index = 0; index < root.childCount; index++)
            {
                MapIdentity(root.GetChild(index), map);
            }
        }
    }
}
