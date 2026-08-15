using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Haruki.MV
{
    [Serializable]
    public sealed class MvBundleSetLoadRequest
    {
        public string requestId;
        public string baseUrl;
        public string manifestName = "deps.json";
        public string bundleSuffix = "";
    }

    [Serializable]
    public sealed class MvBundleSetManifest
    {
        public int musicId;
        public string assetVersion;
        public string assetHash;
        public string[] requested = Array.Empty<string>();
        public MvBundleSetEntry[] entries = Array.Empty<MvBundleSetEntry>();
    }

    [Serializable]
    public sealed class MvBundleSetEntry
    {
        public string name;
        public string[] deps = Array.Empty<string>();
    }

    [Serializable]
    public sealed class MvPrefabLoadRequest
    {
        public string requestId;
        public string bundleName;
        public string assetName;
    }

    [Serializable]
    public sealed class MvAssetLoadRequest
    {
        public string requestId;
        public string bundleName;
        public string assetName;
    }

    public sealed class MvBundleSetLoader : MonoBehaviour
    {
        private readonly Dictionary<string, AssetBundle> _loadedBundles =
            new Dictionary<string, AssetBundle>(StringComparer.Ordinal);
        private readonly List<string> _loadOrder = new List<string>();
        private readonly List<GameObject> _instances = new List<GameObject>();

        public bool IsBusy { get; private set; }
        public int LoadedBundleCount => _loadedBundles.Count;
        public IReadOnlyCollection<string> LoadedBundleNames => _loadedBundles.Keys;

        public bool ContainsBundle(string bundleName)
        {
            return !string.IsNullOrWhiteSpace(bundleName) &&
                _loadedBundles.TryGetValue(bundleName, out var bundle) &&
                bundle != null;
        }

        public IEnumerator Load(
            MvBundleSetLoadRequest request,
            Action<int> onReady,
            Action<Exception> onError)
        {
            if (IsBusy)
            {
                onError(new InvalidOperationException("An MV bundle-set operation is already in progress."));
                yield break;
            }
            if (request == null || string.IsNullOrWhiteSpace(request.baseUrl))
            {
                onError(new ArgumentException("MV bundle-set baseUrl is required."));
                yield break;
            }

            IsBusy = true;
            DisposeLoadedBundles();

            var manifestName = string.IsNullOrWhiteSpace(request.manifestName)
                ? "deps.json"
                : request.manifestName;
            var manifestUrl = JoinUrl(request.baseUrl, manifestName);
            MvBundleSetManifest manifest = null;
            using (var manifestRequest = UnityWebRequest.Get(manifestUrl))
            {
                yield return manifestRequest.SendWebRequest();
                if (manifestRequest.result != UnityWebRequest.Result.Success)
                {
                    yield return Fail(
                        new InvalidOperationException($"Failed to download MV dependency manifest: {manifestRequest.error}"),
                        onError);
                    yield break;
                }

                Exception manifestError = null;
                try
                {
                    manifest = JsonUtility.FromJson<MvBundleSetManifest>(manifestRequest.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    manifestError = exception;
                }
                if (manifestError != null)
                {
                    yield return Fail(manifestError, onError);
                    yield break;
                }
            }

            string[] loadOrder = null;
            Exception loadOrderError = null;
            try
            {
                loadOrder = ResolveLoadOrder(manifest);
            }
            catch (Exception exception)
            {
                loadOrderError = exception;
            }
            if (loadOrderError != null)
            {
                yield return Fail(loadOrderError, onError);
                yield break;
            }

            foreach (var bundleName in loadOrder)
            {
                var url = JoinUrl(request.baseUrl, bundleName + (request.bundleSuffix ?? string.Empty));
                using (var bundleRequest = UnityWebRequestAssetBundle.GetAssetBundle(url))
                {
                    yield return bundleRequest.SendWebRequest();
                    if (bundleRequest.result != UnityWebRequest.Result.Success)
                    {
                        yield return Fail(
                            new InvalidOperationException($"Failed to download MV bundle '{bundleName}': {bundleRequest.error}"),
                            onError);
                        yield break;
                    }

                    var bundle = DownloadHandlerAssetBundle.GetContent(bundleRequest);
                    if (bundle == null)
                    {
                        yield return Fail(
                            new InvalidOperationException($"MV bundle '{bundleName}' is not compatible with this Unity player."),
                            onError);
                        yield break;
                    }
                    _loadedBundles.Add(bundleName, bundle);
                    _loadOrder.Add(bundleName);
                }
            }

            IsBusy = false;
            onReady(_loadedBundles.Count);
        }

        public GameObject CreatePrefabInstance(MvPrefabLoadRequest request)
        {
            return CreatePrefabInstance(request, null, null);
        }

        public GameObject CreatePrefabInstance(
            MvPrefabLoadRequest request,
            Transform parent,
            string instanceName)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.bundleName))
            {
                throw new ArgumentException("MV prefab bundleName is required.");
            }
            if (string.IsNullOrWhiteSpace(request.assetName))
            {
                throw new ArgumentException("MV prefab assetName is required.");
            }
            if (!_loadedBundles.TryGetValue(request.bundleName, out var bundle) || bundle == null)
            {
                throw new InvalidOperationException($"MV bundle '{request.bundleName}' is not loaded.");
            }

            var prefab = LoadAsset<GameObject>(request.bundleName, request.assetName);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"MV bundle '{request.bundleName}' has no GameObject asset '{request.assetName}'.");
            }

            var instance = Instantiate(prefab, parent, false);
            instance.name = string.IsNullOrWhiteSpace(instanceName)
                ? prefab.name
                : instanceName;
            _instances.Add(instance);
            return instance;
        }

        public GameObject InstantiateSinglePrefab(
            string bundleName,
            Transform parent,
            string instanceName = null)
        {
            if (!_loadedBundles.TryGetValue(bundleName, out var bundle) || bundle == null)
            {
                throw new InvalidOperationException($"MV bundle '{bundleName}' is not loaded.");
            }
            var prefabs = bundle.LoadAllAssets<GameObject>();
            if (prefabs.Length != 1 || prefabs[0] == null)
            {
                throw new InvalidOperationException(
                    $"MV bundle '{bundleName}' must contain exactly one GameObject prefab.");
            }
            var instance = Instantiate(prefabs[0], parent, false);
            instance.name = string.IsNullOrWhiteSpace(instanceName)
                ? prefabs[0].name
                : instanceName;
            _instances.Add(instance);
            return instance;
        }

        public T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(bundleName) || string.IsNullOrWhiteSpace(assetName))
            {
                throw new ArgumentException("MV asset bundleName and assetName are required.");
            }
            if (!_loadedBundles.TryGetValue(bundleName, out var bundle) || bundle == null)
            {
                throw new InvalidOperationException($"MV bundle '{bundleName}' is not loaded.");
            }
            return bundle.LoadAsset<T>(assetName);
        }

        public T[] LoadAllAssets<T>(string bundleName) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(bundleName))
            {
                throw new ArgumentException("MV asset bundleName is required.", nameof(bundleName));
            }
            if (!_loadedBundles.TryGetValue(bundleName, out var bundle) || bundle == null)
            {
                throw new InvalidOperationException($"MV bundle '{bundleName}' is not loaded.");
            }
            return bundle.LoadAllAssets<T>();
        }

        public string FindSingleBundleByPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("MV bundle prefix is required.", nameof(prefix));
            }

            string match = null;
            foreach (var name in _loadedBundles.Keys)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }
                if (match != null)
                {
                    throw new InvalidOperationException(
                        $"MV bundle prefix '{prefix}' matched more than one loaded bundle.");
                }
                match = name;
            }
            return match;
        }

        public void DisposeLoadedBundles()
        {
            for (var index = _instances.Count - 1; index >= 0; index--)
            {
                if (_instances[index] != null)
                {
                    Destroy(_instances[index]);
                }
            }
            _instances.Clear();

            for (var index = _loadOrder.Count - 1; index >= 0; index--)
            {
                if (_loadedBundles.TryGetValue(_loadOrder[index], out var bundle) && bundle != null)
                {
                    bundle.Unload(true);
                }
            }
            _loadOrder.Clear();
            _loadedBundles.Clear();
        }

        public static string[] ResolveLoadOrder(MvBundleSetManifest manifest)
        {
            if (manifest == null || manifest.entries == null || manifest.entries.Length == 0)
            {
                throw new InvalidOperationException("MV dependency manifest has no entries.");
            }

            var entries = new Dictionary<string, MvBundleSetEntry>(StringComparer.Ordinal);
            foreach (var entry in manifest.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.name) || !entries.TryAdd(entry.name, entry))
                {
                    throw new InvalidOperationException("MV dependency manifest contains an invalid or duplicate entry.");
                }
            }

            var roots = manifest.requested != null && manifest.requested.Length > 0
                ? manifest.requested
                : new List<string>(entries.Keys).ToArray();
            var state = new Dictionary<string, byte>(StringComparer.Ordinal);
            var ordered = new List<string>();

            foreach (var root in roots)
            {
                Visit(root, entries, state, ordered);
            }
            return ordered.ToArray();
        }

        private static void Visit(
            string name,
            IReadOnlyDictionary<string, MvBundleSetEntry> entries,
            IDictionary<string, byte> state,
            ICollection<string> ordered)
        {
            if (!entries.TryGetValue(name, out var entry))
            {
                throw new InvalidOperationException($"MV dependency manifest is missing '{name}'.");
            }
            if (state.TryGetValue(name, out var currentState))
            {
                if (currentState == 1)
                {
                    throw new InvalidOperationException($"MV dependency cycle includes '{name}'.");
                }
                if (currentState == 2)
                {
                    return;
                }
            }

            state[name] = 1;
            foreach (var dependency in entry.deps ?? Array.Empty<string>())
            {
                Visit(dependency, entries, state, ordered);
            }
            state[name] = 2;
            ordered.Add(name);
        }

        private IEnumerator Fail(Exception exception, Action<Exception> onError)
        {
            DisposeLoadedBundles();
            IsBusy = false;
            onError(exception);
            yield break;
        }

        private static string JoinUrl(string baseUrl, string relativePath)
        {
            return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
        }

    }
}
