using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Haruki.MV
{
    [Serializable]
    public sealed class MvSceneLoadRequest
    {
        public string requestId;
        public string baseUrl;
        public string manifestBundleName;
        public string sceneBundleName;
        public string sceneName;
        public string audioObjectPath;
        public string[] preloadBundleNames = Array.Empty<string>();
    }

    [RequireComponent(typeof(MvPlaybackCoordinator))]
    public sealed class MvSceneBundleLoader : MonoBehaviour
    {
        private readonly List<AssetBundle> _loadedBundles = new List<AssetBundle>();
        private readonly Dictionary<string, AssetBundle> _bundlesByName =
            new Dictionary<string, AssetBundle>(StringComparer.Ordinal);
        private MvPlaybackCoordinator _coordinator;
        private Scene _loadedScene;

        public bool IsBusy { get; private set; }
        public bool HasLoadedContent => _loadedScene.IsValid() || _loadedBundles.Count > 0;

        private void Awake()
        {
            _coordinator = GetComponent<MvPlaybackCoordinator>();
        }

        public IEnumerator Load(
            MvSceneLoadRequest request,
            Action onReady,
            Action<Exception> onError)
        {
            if (IsBusy)
            {
                onError(new InvalidOperationException("An MV scene operation is already in progress."));
                yield break;
            }

            var validationError = Validate(request);
            if (validationError != null)
            {
                onError(validationError);
                yield break;
            }

            IsBusy = true;
            yield return DisposeLoadedContent();

            Exception error = null;
            AssetBundle manifestBundle = null;
            yield return DownloadBundle(
                request.baseUrl,
                request.manifestBundleName,
                bundle => manifestBundle = bundle,
                exception => error = exception);
            if (error != null)
            {
                yield return Fail(error, onError);
                yield break;
            }

            RegisterBundle(request.manifestBundleName, manifestBundle);
            var manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            if (manifest == null)
            {
                yield return Fail(
                    new InvalidOperationException("The manifest bundle has no AssetBundleManifest."),
                    onError);
                yield break;
            }

            var requestedBundles = new List<string>();
            if (request.preloadBundleNames != null)
            {
                requestedBundles.AddRange(request.preloadBundleNames);
            }
            requestedBundles.Add(request.sceneBundleName);

            var requiredBundles = new List<string>();
            var knownBundles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var requestedBundle in requestedBundles)
            {
                if (string.IsNullOrWhiteSpace(requestedBundle))
                {
                    continue;
                }

                foreach (var dependency in manifest.GetAllDependencies(requestedBundle))
                {
                    if (knownBundles.Add(dependency))
                    {
                        requiredBundles.Add(dependency);
                    }
                }
                if (knownBundles.Add(requestedBundle))
                {
                    requiredBundles.Add(requestedBundle);
                }
            }

            foreach (var bundleName in requiredBundles)
            {
                if (string.IsNullOrWhiteSpace(bundleName) || _bundlesByName.ContainsKey(bundleName))
                {
                    continue;
                }

                AssetBundle bundle = null;
                error = null;
                yield return DownloadBundle(
                    request.baseUrl,
                    bundleName,
                    loaded => bundle = loaded,
                    exception => error = exception);
                if (error != null)
                {
                    yield return Fail(error, onError);
                    yield break;
                }
                RegisterBundle(bundleName, bundle);
            }

            var sceneBundle = _bundlesByName[request.sceneBundleName];
            var scenePath = ResolveScenePath(sceneBundle.GetAllScenePaths(), request.sceneName);
            if (scenePath == null)
            {
                yield return Fail(
                    new InvalidOperationException($"Scene '{request.sceneName}' was not found in bundle '{request.sceneBundleName}'."),
                    onError);
                yield break;
            }

            var loadOperation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                yield return Fail(new InvalidOperationException($"Unity could not load scene '{scenePath}'."), onError);
                yield break;
            }
            yield return loadOperation;

            _loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (!_loadedScene.IsValid() || !_loadedScene.isLoaded)
            {
                yield return Fail(new InvalidOperationException($"Scene '{scenePath}' did not finish loading."), onError);
                yield break;
            }

            Exception bindError = null;
            try
            {
                var roots = _loadedScene.GetRootGameObjects();
                var audioSource = ResolveAudioSource(roots, request.audioObjectPath);
                _coordinator.BindScene(roots, audioSource, ResolveDuration(roots, audioSource));
            }
            catch (Exception exception)
            {
                bindError = exception;
            }
            if (bindError != null)
            {
                yield return Fail(bindError, onError);
                yield break;
            }

            IsBusy = false;
            onReady();
        }

        public IEnumerator Dispose(Action onDisposed, Action<Exception> onError)
        {
            if (IsBusy)
            {
                onError(new InvalidOperationException("An MV scene operation is already in progress."));
                yield break;
            }

            IsBusy = true;
            yield return DisposeLoadedContent();
            IsBusy = false;
            onDisposed();
        }

        private IEnumerator Fail(Exception exception, Action<Exception> onError)
        {
            yield return DisposeLoadedContent();
            IsBusy = false;
            onError(exception);
        }

        private IEnumerator DisposeLoadedContent()
        {
            _coordinator.DisposeScene();
            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                var unloadOperation = SceneManager.UnloadSceneAsync(_loadedScene);
                if (unloadOperation != null)
                {
                    yield return unloadOperation;
                }
            }
            _loadedScene = default;

            for (var index = _loadedBundles.Count - 1; index >= 0; index--)
            {
                if (_loadedBundles[index] != null)
                {
                    _loadedBundles[index].Unload(true);
                }
            }
            _loadedBundles.Clear();
            _bundlesByName.Clear();
        }

        private IEnumerator DownloadBundle(
            string baseUrl,
            string bundleName,
            Action<AssetBundle> onLoaded,
            Action<Exception> onError)
        {
            var url = $"{baseUrl.TrimEnd('/')}/{bundleName.TrimStart('/')}";
            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError(new InvalidOperationException($"Failed to download MV bundle '{bundleName}': {request.error}"));
                    yield break;
                }

                var bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (bundle == null)
                {
                    onError(new InvalidOperationException($"Downloaded MV bundle '{bundleName}' could not be opened."));
                    yield break;
                }
                onLoaded(bundle);
            }
        }

        private void RegisterBundle(string name, AssetBundle bundle)
        {
            _bundlesByName.Add(name, bundle);
            _loadedBundles.Add(bundle);
        }

        private static Exception Validate(MvSceneLoadRequest request)
        {
            if (request == null)
            {
                return new ArgumentNullException(nameof(request));
            }
            if (string.IsNullOrWhiteSpace(request.baseUrl)
                || string.IsNullOrWhiteSpace(request.manifestBundleName)
                || string.IsNullOrWhiteSpace(request.sceneBundleName)
                || string.IsNullOrWhiteSpace(request.sceneName))
            {
                return new ArgumentException("baseUrl, manifestBundleName, sceneBundleName, and sceneName are required.");
            }
            return null;
        }

        private static string ResolveScenePath(string[] scenePaths, string requestedScene)
        {
            foreach (var scenePath in scenePaths)
            {
                if (string.Equals(scenePath, requestedScene, StringComparison.Ordinal)
                    || string.Equals(Path.GetFileNameWithoutExtension(scenePath), requestedScene, StringComparison.Ordinal))
                {
                    return scenePath;
                }
            }
            return null;
        }

        private static AudioSource ResolveAudioSource(GameObject[] roots, string objectPath)
        {
            if (!string.IsNullOrWhiteSpace(objectPath))
            {
                foreach (var root in roots)
                {
                    var transform = root.transform.Find(objectPath);
                    if (transform != null && transform.TryGetComponent<AudioSource>(out var source))
                    {
                        return source;
                    }
                }
                throw new InvalidOperationException($"AudioSource '{objectPath}' was not found in the MV scene.");
            }

            foreach (var root in roots)
            {
                foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                {
                    if (source.clip != null)
                    {
                        return source;
                    }
                }
            }
            return null;
        }

        private static double ResolveDuration(GameObject[] roots, AudioSource audioSource)
        {
            double duration = audioSource != null && audioSource.clip != null
                ? audioSource.clip.length
                : 0;
            foreach (var root in roots)
            {
                foreach (var director in root.GetComponentsInChildren<PlayableDirector>(true))
                {
                    if (!double.IsNaN(director.duration) && !double.IsInfinity(director.duration))
                    {
                        duration = Math.Max(duration, director.duration);
                    }
                }
            }
            return duration;
        }
    }
}
