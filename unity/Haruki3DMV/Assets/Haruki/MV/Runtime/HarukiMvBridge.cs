using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Sekai.Core;

namespace Haruki.MV
{
    [RequireComponent(typeof(MvPlaybackCoordinator), typeof(MvSceneBundleLoader), typeof(MvBundleSetLoader))]
    [RequireComponent(typeof(MvPlayerAssembler))]
    public sealed class HarukiMvBridge : MonoBehaviour
    {
        public const string ObjectName = "HarukiMvBridge";

        [Serializable]
        private sealed class PauseRequest
        {
            public bool paused = false;
        }

        [Serializable]
        private sealed class SeekRequest
        {
            public double timeSeconds = 0;
        }

        [Serializable]
        private sealed class StatePayload
        {
            public string state;
            public double timeSeconds;
            public double durationSeconds;
        }

        [Serializable]
        private sealed class ErrorPayload
        {
            public string requestId;
            public string message;
        }

        [Serializable]
        private sealed class PrefabReadyPayload
        {
            public string requestId;
            public string bundleName;
            public string assetName;
            public string instanceName;
        }

        [Serializable]
        private sealed class CutInActiveRequest
        {
            public int cutInOrder;
            public bool active;
        }

        [Serializable]
        private sealed class MvReadyPayload
        {
            public string requestId;
            public int musicId;
            public int cutInCount;
            public double durationSeconds;
        }

        [Serializable]
        private sealed class BundleSetReadyPayload
        {
            public string requestId;
            public int bundleCount;
        }

        [Serializable]
        private sealed class SceneReadyPayload
        {
            public string requestId;
        }

        [Serializable]
        private sealed class MvDataReadyPayload
        {
            public string requestId;
            public string dataJson;
        }

        [Serializable]
        private sealed class DisposeRequest
        {
            public string requestId;
        }

        [Serializable]
        private sealed class DisposedPayload
        {
            public string requestId;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void HarukiMvEmit(string eventName, string payload);
#endif

        private MvPlaybackCoordinator _coordinator;
        private MvSceneBundleLoader _loader;
        private MvBundleSetLoader _bundleSetLoader;
        private MvPlayerAssembler _playerAssembler;

        private void Awake()
        {
            gameObject.name = ObjectName;
            DontDestroyOnLoad(gameObject);
            _coordinator = GetComponent<MvPlaybackCoordinator>();
            _loader = GetComponent<MvSceneBundleLoader>();
            _bundleSetLoader = GetComponent<MvBundleSetLoader>();
            _playerAssembler = GetComponent<MvPlayerAssembler>();
        }

        private void Start()
        {
            Emit("ready", "{}");
        }

        public void SetPaused(string json)
        {
            InvokeSafely(() => _coordinator.SetPaused(JsonUtility.FromJson<PauseRequest>(json).paused));
        }

        public void LoadScene(string json)
        {
            MvSceneLoadRequest request;
            try
            {
                request = ParseRequest<MvSceneLoadRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }

            if (_bundleSetLoader.IsBusy)
            {
                EmitError(new InvalidOperationException(
                    "Cannot load an MV scene while a bundle-set operation is running."),
                    request.requestId);
                return;
            }
            _playerAssembler.DisposePlayers();
            _bundleSetLoader.DisposeLoadedBundles();
            Emit("loading", "{}");
            StartCoroutine(_loader.Load(
                request,
                () =>
                {
                    Emit("scene-ready", JsonUtility.ToJson(new SceneReadyPayload
                    {
                        requestId = request.requestId,
                    }));
                    EmitState();
                },
                exception => EmitError(exception, request.requestId)));
        }

        public void Seek(string json)
        {
            InvokeSafely(() => _coordinator.Seek(JsonUtility.FromJson<SeekRequest>(json).timeSeconds));
        }

        public void Retry(string unused)
        {
            InvokeSafely(_coordinator.Retry);
        }

        public void LoadBundleSet(string json)
        {
            MvBundleSetLoadRequest request;
            try
            {
                request = ParseRequest<MvBundleSetLoadRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }

            if (_loader.IsBusy || _loader.HasLoadedContent)
            {
                EmitError(new InvalidOperationException(
                    "Dispose the loaded MV scene before loading a bundle set."),
                    request.requestId);
                return;
            }
            _playerAssembler.DisposePlayers();
            Emit("bundle-set-loading", "{}");
            StartCoroutine(_bundleSetLoader.Load(
                request,
                count => Emit("bundle-set-ready", JsonUtility.ToJson(new BundleSetReadyPayload
                {
                    requestId = request.requestId,
                    bundleCount = count,
                })),
                exception => EmitError(exception, request.requestId)));
        }

        public void LoadMv(string json)
        {
            MvPlayerLoadRequest request;
            try
            {
                request = ParseRequest<MvPlayerLoadRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }
            InvokeSafely(() =>
            {
                if (_loader.IsBusy || _loader.HasLoadedContent)
                {
                    throw new InvalidOperationException(
                        "Dispose the loaded MV scene before assembling an MV.");
                }
                if (_bundleSetLoader.IsBusy)
                {
                    throw new InvalidOperationException(
                        "Wait for the bundle set to finish loading before assembling an MV.");
                }
                _playerAssembler.Load(request);
                Emit("mv-ready", JsonUtility.ToJson(new MvReadyPayload
                {
                    requestId = request.requestId,
                    musicId = request.musicId,
                    cutInCount = _playerAssembler.Players.Count - 1,
                    durationSeconds = _coordinator.DurationSeconds,
                }));
            }, request.requestId);
        }

        public void SetCutInActive(string json)
        {
            InvokeSafely(() =>
            {
                var request = JsonUtility.FromJson<CutInActiveRequest>(json);
                _playerAssembler.SetCutInActive(request.cutInOrder, request.active);
            });
        }

        public void InstantiatePrefab(string json)
        {
            MvPrefabLoadRequest request;
            try
            {
                request = ParseRequest<MvPrefabLoadRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }
            InvokeSafely(() =>
            {
                var instance = _bundleSetLoader.InstantiatePrefab(request);
                Emit("prefab-ready", JsonUtility.ToJson(new PrefabReadyPayload
                {
                    requestId = request.requestId,
                    bundleName = request.bundleName,
                    assetName = request.assetName,
                    instanceName = instance.name
                }));
            }, request.requestId);
        }

        public void ReadMvData(string json)
        {
            MvAssetLoadRequest request;
            try
            {
                request = ParseRequest<MvAssetLoadRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }
            InvokeSafely(() =>
            {
                var data = _bundleSetLoader.LoadAsset<MusicVideoData>(
                    request.bundleName,
                    request.assetName);
                if (data == null)
                {
                    throw new InvalidOperationException(
                        $"MV bundle '{request.bundleName}' has no MusicVideoData asset '{request.assetName}'.");
                }
                Emit("mv-data-ready", JsonUtility.ToJson(new MvDataReadyPayload
                {
                    requestId = request.requestId,
                    dataJson = JsonUtility.ToJson(data),
                }));
            }, request.requestId);
        }

        public void GetState(string unused)
        {
            EmitState();
        }

        public void Dispose(string json)
        {
            DisposeRequest request;
            try
            {
                request = ParseRequest<DisposeRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }
            if (_bundleSetLoader.IsBusy || _loader.IsBusy)
            {
                EmitError(new InvalidOperationException(
                    "Wait for the current MV operation before disposing."), request?.requestId);
                return;
            }
            _playerAssembler.DisposePlayers();
            _bundleSetLoader.DisposeLoadedBundles();
            StartCoroutine(_loader.Dispose(
                () =>
                {
                    Emit("disposed", JsonUtility.ToJson(new DisposedPayload
                    {
                        requestId = request?.requestId,
                    }));
                    EmitState();
                },
                exception => EmitError(exception, request?.requestId)));
        }

        private void InvokeSafely(Action action, string requestId = null)
        {
            try
            {
                action();
                EmitState();
            }
            catch (Exception exception)
            {
                EmitError(exception, requestId);
            }
        }

        private static T ParseRequest<T>(string json) where T : class
        {
            var request = JsonUtility.FromJson<T>(json);
            return request ?? throw new ArgumentException("MV bridge request JSON is required.");
        }

        private static void EmitError(Exception exception, string requestId = null)
        {
            Emit("error", JsonUtility.ToJson(new ErrorPayload
            {
                requestId = requestId,
                message = exception.Message,
            }));
        }

        private void EmitState()
        {
            Emit("state", JsonUtility.ToJson(new StatePayload
            {
                state = _coordinator.State.ToString().ToLowerInvariant(),
                timeSeconds = _coordinator.CurrentTimeSeconds,
                durationSeconds = _coordinator.DurationSeconds
            }));
        }

        private static void Emit(string eventName, string payload)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            HarukiMvEmit(eventName, payload);
#else
            Debug.Log($"[HarukiMV] {eventName}: {payload}");
#endif
        }
    }
}
