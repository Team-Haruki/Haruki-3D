using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Core;
using UnityEngine;

namespace Haruki.MV
{
    public sealed class MvLightBindingTarget : MonoBehaviour
    {
        public MvLightCategory Category { get; internal set; }
        public int FormationIndex { get; internal set; } = -1;
    }

    public sealed class MvLightNode : IDisposable
    {
        public static IReadOnlyList<MvLightCategory> OfficialCategories =>
            MvOfficialRuntimeData.MusicVideoLightCategories;

        private readonly IDictionary<string, UnityEngine.Object> _bindings;
        private readonly Transform _root;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<GameObject> _characterAmbientLights =
            new List<GameObject>();
        private readonly List<GameObject> _characterRimLights =
            new List<GameObject>();

        public MvLightNode(
            IDictionary<string, UnityEngine.Object> bindings,
            Transform root)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
        }

        public GameObject GlobalSettings { get; private set; }
        public GameObject AmbientLight { get; private set; }
        public GameObject DirectionalLight { get; private set; }
        public GameObject SpotLight { get; private set; }
        public GameObject ShadowLight { get; private set; }
        public IReadOnlyList<GameObject> CharacterAmbientLights =>
            _characterAmbientLights;
        public IReadOnlyList<GameObject> CharacterRimLights => _characterRimLights;

        public void Load(MusicVideoData mvData, Camera mainCamera = null)
        {
            if (mvData == null)
            {
                throw new ArgumentNullException(nameof(mvData));
            }
            if (_objects.Count != 0)
            {
                throw new InvalidOperationException("LightNode is already loaded.");
            }

            GlobalSettings = Create("GlobalSettings", MvLightCategory.GlobalSettings);
            AmbientLight = Create("AmbientLight", MvLightCategory.AmbientLight);
            DirectionalLight = Create("DirectionalLight", MvLightCategory.DirectionalLight);
            SpotLight = Create("SpotLight", MvLightCategory.SpotLight);
            ShadowLight = Create("ShadowLight", MvLightCategory.ShadowLight);

            GlobalSettings.AddComponent<SekaiGlobalSettings>();
            AmbientLight.AddComponent<Sekai.SekaiAmbientLight>();
            var directional = DirectionalLight.AddComponent<Sekai.SekaiDirectionalLight>();
            directional.Initialize();
            SpotLight.AddComponent<Sekai.SekaiGlobalSpotLight>();
            ShadowLight.AddComponent<SekaiGlobalCharacterShadowLight>();

            var characterCount = mvData.characterInfos?.Length ?? 0;
            for (var index = 0; index < characterCount; index++)
            {
                var rimObject = Create(
                    $"Character{index}RimLight",
                    MvLightCategory.CharacterRimLight,
                    index);
                rimObject.AddComponent<SekaiCharacterRimLight>()
                    .Setup(index, mainCamera);
                _characterRimLights.Add(rimObject);
                var ambientObject = Create(
                    $"Character{index}AmbientLight",
                    MvLightCategory.CharacterAmbientLight,
                    index);
                ambientObject.AddComponent<SekaiCharacterAmbientLight>().FormationId = index;
                _characterAmbientLights.Add(ambientObject);
            }

            // Timeline assets are loaded before this node. Bind any actual light
            // stream names that the sample exposes instead of guessing an insert
            // character suffix that has not been recovered yet.
            foreach (var streamName in _bindings.Keys.ToArray())
            {
                if (TryCharacterIndex(streamName, "AmbientLight", out var ambientIndex) &&
                    ambientIndex < _characterAmbientLights.Count)
                {
                    _bindings[streamName] = _characterAmbientLights[ambientIndex];
                }
                else if (TryCharacterIndex(streamName, "RimLight", out var rimIndex) &&
                    rimIndex < _characterRimLights.Count)
                {
                    _bindings[streamName] = _characterRimLights[rimIndex];
                }
            }
        }

        public void Dispose()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                Destroy(_objects[index]);
            }
            _objects.Clear();
            _characterAmbientLights.Clear();
            _characterRimLights.Clear();
            GlobalSettings = null;
            AmbientLight = null;
            DirectionalLight = null;
            SpotLight = null;
            ShadowLight = null;
        }

        private GameObject Create(
            string bindingName,
            MvLightCategory category,
            int formationIndex = -1)
        {
            var gameObject = new GameObject(bindingName);
            gameObject.transform.SetParent(_root, false);
            gameObject.AddComponent<Animator>();
            var target = gameObject.AddComponent<MvLightBindingTarget>();
            target.Category = category;
            target.FormationIndex = formationIndex;
            _objects.Add(gameObject);
            _bindings[bindingName] = gameObject;
            return gameObject;
        }

        private static bool TryCharacterIndex(
            string streamName,
            string suffix,
            out int index)
        {
            const string prefix = "Character";
            index = -1;
            if (string.IsNullOrEmpty(streamName) ||
                !streamName.StartsWith(prefix, StringComparison.Ordinal) ||
                !streamName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }
            var digits = streamName.Substring(
                prefix.Length,
                streamName.Length - prefix.Length - suffix.Length);
            return int.TryParse(digits, out index) && index >= 0;
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
