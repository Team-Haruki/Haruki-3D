using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using Sekai.Core;
using Sekai.Rendering;
using Sekai.Rendering.PostPrcessV2;

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
        private sealed class RenderProfileRequest
        {
            public string requestId;
            public int width;
            public int height;
            public float dpi;
            public int refreshRate;
            public MvQualityType quality;
            public MvLivePlayMode playMode;
            public MvOutputResolution outputResolution;
            public bool use120Fps;
        }

        [Serializable]
        private sealed class RenderProfileReadyPayload
        {
            public string requestId;
            public int renderWidth;
            public int renderHeight;
            public int postEffectWidth;
            public int postEffectHeight;
            public int targetFrameRate;
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

        [Serializable]
        private sealed class DiagnosticsRequest
        {
            public string requestId;
        }

        [Serializable]
        private sealed class CameraDiagnostics
        {
            public string name;
            public Vector3 position;
            public Vector3 eulerAngles;
            public float fieldOfView;
            public bool enabled;
        }

        [Serializable]
        private sealed class MaterialDiagnostics
        {
            public string renderer;
            public string material;
            public string shader;
            public bool shaderSupported;
            public int passCount;
            public string mainTexture;
            public int mainTextureWidth;
            public int mainTextureHeight;
        }

        [Serializable]
        private sealed class DiagnosticsPayload
        {
            public string requestId;
            public CameraDiagnostics camera;
            public Color fogColor;
            public Vector4 fogFactor;
            public float allLightIntensity;
            public string audioClipName;
            public string audioLoadState;
            public bool audioStarted;
            public bool audioIsPlaying;
            public double audioDurationSeconds;
            public double audioTimeSeconds;
            public bool postProcessShadersReady;
            public bool postProcessTexturesReady;
            public string[] missingPostProcessShaders = Array.Empty<string>();
            public int timelineCount;
            public int timelineOutputCount;
            public int unboundTimelineOutputCount;
            public string[] unboundTimelineOutputs = Array.Empty<string>();
            public int rendererCount;
            public MaterialDiagnostics[] materials = Array.Empty<MaterialDiagnostics>();
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
            _coordinator.PlaybackCompleted += OnPlaybackCompleted;
        }

        private void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.PlaybackCompleted -= OnPlaybackCompleted;
            }
        }

        private void Start()
        {
            Emit("ready", "{}");
        }

        public void SetPaused(string json)
        {
            InvokeSafely(() => _coordinator.SetPlaybackPaused(
                JsonUtility.FromJson<PauseRequest>(json).paused));
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
            InvokeSafely(() => _coordinator.SeekTo(
                JsonUtility.FromJson<SeekRequest>(json).timeSeconds));
        }

        public void Retry(string unused)
        {
            InvokeSafely(_coordinator.RetryPlayback);
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
                _playerAssembler.SetCutInSceneActive(request.cutInOrder, request.active);
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
                var instance = _bundleSetLoader.CreatePrefabInstance(request);
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

        public void GetRenderProfile(string json)
        {
            RenderProfileRequest request;
            try
            {
                request = ParseRequest<RenderProfileRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }
            InvokeSafely(() =>
            {
                EmitRenderProfile(
                    "render-profile-ready",
                    request.requestId,
                    CalculateRenderProfile(request));
            }, request.requestId);
        }

        public void ApplyRenderProfile(string json)
        {
            RenderProfileRequest request;
            try
            {
                request = ParseRequest<RenderProfileRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }
            InvokeSafely(() =>
            {
                var profile = CalculateRenderProfile(request);
                Application.targetFrameRate = profile.TargetFrameRate;
                ScalableBufferManager.ResizeBuffers(1f, 1f);
                Screen.SetResolution(
                    profile.RenderSize.x,
                    profile.RenderSize.y,
                    Screen.fullScreenMode);
                _playerAssembler.ApplyOutputProfile(profile);
                EmitRenderProfile("render-profile-applied", request.requestId, profile);
            }, request.requestId);
        }

        private static MvRenderProfile CalculateRenderProfile(RenderProfileRequest request)
        {
            return request.outputResolution == MvOutputResolution.Device
                ? MvRenderProfile.Calculate(
                    request.width,
                    request.height,
                    request.dpi,
                    request.quality,
                    request.playMode,
                    request.refreshRate,
                    request.use120Fps)
                : MvRenderProfile.ForVideoOutput(
                    request.outputResolution,
                    request.width,
                    request.height,
                    request.refreshRate,
                    request.use120Fps);
        }

        private static void EmitRenderProfile(
            string eventName,
            string requestId,
            MvRenderProfile profile)
        {
            Emit(eventName, JsonUtility.ToJson(new RenderProfileReadyPayload
            {
                requestId = requestId,
                renderWidth = profile.RenderSize.x,
                renderHeight = profile.RenderSize.y,
                postEffectWidth = profile.PostEffectSize.x,
                postEffectHeight = profile.PostEffectSize.y,
                targetFrameRate = profile.TargetFrameRate,
            }));
        }

        public void GetState(string unused)
        {
            EmitState();
        }

        public void GetDiagnostics(string json)
        {
            DiagnosticsRequest request;
            try
            {
                request = ParseRequest<DiagnosticsRequest>(json);
            }
            catch (Exception exception)
            {
                EmitError(exception);
                return;
            }

            InvokeSafely(() =>
            {
                var player = _playerAssembler.MainPlayer ??
                    throw new InvalidOperationException("Load an MV before requesting diagnostics.");
                var camera = player.Camera.MainCamera;
                var materials = new List<MaterialDiagnostics>();
                var seenMaterials = new HashSet<int>();
                var renderers = player.Root.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || !seenMaterials.Add(material.GetInstanceID()))
                        {
                            continue;
                        }
                        Texture texture = null;
                        foreach (var propertyName in new[]
                        {
                            "_MainTex",
                            "_BaseMap",
                            "_MeshFlareParaMainTex",
                        })
                        {
                            if (!material.HasProperty(propertyName))
                            {
                                continue;
                            }
                            texture = material.GetTexture(propertyName);
                            break;
                        }
                        materials.Add(new MaterialDiagnostics
                        {
                            renderer = renderer.name,
                            material = material.name,
                            shader = material.shader != null ? material.shader.name : null,
                            shaderSupported = material.shader != null && material.shader.isSupported,
                            passCount = material.passCount,
                            mainTexture = texture != null ? texture.name : null,
                            mainTextureWidth = texture != null ? texture.width : 0,
                            mainTextureHeight = texture != null ? texture.height : 0,
                        });
                    }
                }

                var directors = player.Timeline.Directors;
                var outputCount = 0;
                var unboundOutputs = new List<string>();
                foreach (var director in directors)
                {
                    if (director?.playableAsset == null)
                    {
                        continue;
                    }
                    foreach (var output in director.playableAsset.outputs)
                    {
                        outputCount++;
                        if (director.GetGenericBinding(output.sourceObject) == null)
                        {
                            unboundOutputs.Add($"{director.name}/{output.streamName}");
                        }
                    }
                }

                var postProcess = camera == null
                    ? null
                    : camera.GetUniversalAdditionalCameraData()
                        .scriptableRenderer is var runtimeRenderer &&
                        SekaiRendererRuntime.TryGet(runtimeRenderer, out var runtime)
                            ? runtime.Data.rendererFeatures
                                .OfType<SekaiPostProcessRendererFeature>()
                                .Select(feature => feature.Pass)
                                .FirstOrDefault(pass => pass != null)
                            : null;
                var postProcessTexturesReady =
                    Resources.Load<Texture2D>(TextureResources.AreaTexturePath) != null &&
                    Resources.Load<Texture2D>(TextureResources.SearchTexturePath) != null;

                Emit("diagnostics-ready", JsonUtility.ToJson(new DiagnosticsPayload
                {
                    requestId = request.requestId,
                    camera = camera == null ? null : new CameraDiagnostics
                    {
                        name = camera.name,
                        position = camera.transform.position,
                        eulerAngles = camera.transform.eulerAngles,
                        fieldOfView = camera.fieldOfView,
                        enabled = camera.enabled,
                    },
                    fogColor = Shader.GetGlobalColor("_SekaiFogColor"),
                    fogFactor = Shader.GetGlobalVector("_SekaiFogFactor"),
                    allLightIntensity = Shader.GetGlobalFloat("_SekaiAllLightIntensity"),
                    audioClipName = _coordinator.AudioClipName,
                    audioLoadState = _coordinator.AudioLoadState,
                    audioStarted = _coordinator.AudioStarted,
                    audioIsPlaying = _coordinator.AudioIsPlaying,
                    audioDurationSeconds = _coordinator.AudioDurationSeconds,
                    audioTimeSeconds = _coordinator.AudioTimeSeconds,
                    postProcessShadersReady = postProcess?.HasOfficialShaderLibrary == true,
                    postProcessTexturesReady = postProcessTexturesReady,
                    missingPostProcessShaders = postProcess?.MissingShaderNames.ToArray() ??
                        Array.Empty<string>(),
                    timelineCount = directors.Count,
                    timelineOutputCount = outputCount,
                    unboundTimelineOutputCount = unboundOutputs.Count,
                    unboundTimelineOutputs = unboundOutputs.ToArray(),
                    rendererCount = renderers.Length,
                    materials = materials
                        .OrderBy(value => value.renderer, StringComparer.Ordinal)
                        .ThenBy(value => value.material, StringComparer.Ordinal)
                        .ToArray(),
                }));
            }, request.requestId);
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
            StartCoroutine(_loader.DisposeLoadedScene(
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

        private void OnPlaybackCompleted()
        {
            Emit("finished", "{}");
            EmitState();
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
