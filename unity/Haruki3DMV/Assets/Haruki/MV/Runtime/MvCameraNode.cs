using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Core;
using Sekai.Core.Live;
using Sekai.Core.Graphics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Haruki.MV
{
    public sealed class MvCameraNode : IDisposable
    {
        public const string MainCameraResource = "Core/Common/Camera/MainCamera_MV";
        public const string SubCameraResource = "Core/Common/Camera/SubCamera";
        private static readonly string[] PostEffectTrackNames =
        {
            "Chromatic Aberration Track",
            "Directional Blur Track",
            "Enable Post Effect To Camera Decoration Track",
            "Fade Out Track",
            "Legacy Bloom Track",
            "Legacy Dof Track",
            "Incident Light Track",
            "Light Overlay Track",
            "Lut Track",
            "Saturation Track",
            "Saturation Blur Track",
            "Screen Distortion Track",
            "Sekai Dof Track",
            "Solarization Track",
            "Vignette Track",
        };

        private readonly IDictionary<string, UnityEngine.Object> _bindings;
        private readonly Transform _root;
        private readonly Func<string, GameObject> _loadResource;
        private readonly MvBundleSetLoader _bundles;
        private RenderTexture _subCameraTarget;

        public MvCameraNode(
            IDictionary<string, UnityEngine.Object> bindings,
            Transform root,
            Func<string, GameObject> loadResource = null,
            MvBundleSetLoader bundles = null)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _loadResource = loadResource ?? Resources.Load<GameObject>;
            _bundles = bundles;
        }

        public GameObject MainCameraRoot { get; private set; }
        public GameObject SubCameraRoot { get; private set; }
        public Camera MainCamera { get; private set; }
        public Camera SubCamera { get; private set; }
        public GameObject CameraDecoration { get; private set; }
        public MvCameraAdjustment MainAdjustment { get; private set; }
        public MvCameraAdjustment SubAdjustment { get; private set; }
        public MvPostEffectState PostEffectState { get; private set; }
        public PostEffectV2 PostEffect { get; private set; }
        public GameObject MeshFlareRoot { get; private set; }
        public MeshFlareParaController MeshFlareController { get; private set; }
        public bool UsedRecoveredResourceFallback { get; private set; }

        public void Load(MusicVideoData mvData)
        {
            if (mvData?.cameraInfo == null)
            {
                throw new ArgumentException(
                    "MusicVideoData must contain CameraInfo.",
                    nameof(mvData));
            }

            UsedRecoveredResourceFallback = false;

            MainCameraRoot = InstantiateResource(MainCameraResource, "MainCamera");
            MainCamera = RequireCamera(MainCameraRoot, MainCameraResource);
            MainAdjustment = GetOrAddComponent<MvCameraAdjustment>(MainCameraRoot);
            PostEffectState = GetOrAddComponent<MvPostEffectState>(MainCameraRoot);
            PostEffect = GetOrAddComponent<PostEffectV2>(MainCamera.gameObject);
            PostEffect.Initialize(
                PostEffectState,
                $"MV{mvData.id}_{(MainCameraRoot.name ?? "MainCamera")}_PostEffect",
                MainCamera.transform.parent?.Find("CamParam"));
            _bindings["MainCamera"] = MainCameraRoot;
            foreach (var trackName in PostEffectTrackNames)
            {
                if (_bindings.ContainsKey(trackName))
                {
                    _bindings[trackName] = PostEffectState;
                }
            }
            if (mvData.postEffectInfo?.enableMeshFlarePara == true)
            {
                LoadMeshFlare(mvData.id);
                if (_bindings.ContainsKey("MeshFlareParaTrack"))
                {
                    _bindings["MeshFlareParaTrack"] = MeshFlareController;
                }
            }

            if (mvData.cameraInfo.hasCameraDecoration)
            {
                if (_bundles == null)
                {
                    throw new InvalidOperationException(
                        "Camera decoration loading requires the MV bundle catalog.");
                }
                CameraDecoration = _bundles.CreatePrefabInstance(
                    new MvPrefabLoadRequest
                    {
                        bundleName = MvOfficialRuntimeData.CameraDecorationBundleName(mvData.id),
                        assetName = "decoration",
                    },
                    MainCameraRoot.transform,
                    "CameraDecoration");
                SetLayerRecursively(
                    CameraDecoration,
                    MvRecoveredRendererFactory.CameraDecorationLayer);
                MvOfficialObjectBinding.BindCameraDecorationTargets(
                    CameraDecoration,
                    _bindings);
            }

            if (!mvData.cameraInfo.useSubCamera)
            {
                return;
            }

            SubCameraRoot = InstantiateResource(SubCameraResource, "SubCamera");
            SubCamera = RequireCamera(SubCameraRoot, SubCameraResource);
            SubAdjustment = GetOrAddComponent<MvCameraAdjustment>(SubCameraRoot);
            _bindings["SubCamera"] = SubCameraRoot;

            var size = SubCameraSize(mvData.cameraInfo);
            if (size.HasValue)
            {
                _subCameraTarget = CreateCapturedCameraTarget(size.Value);
                _subCameraTarget.Create();
                SubCamera.targetTexture = _subCameraTarget;
                LiveMonitorRuntime.SubCameraTexture = _subCameraTarget;
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
            {
                return;
            }
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        public void SetCharacterHeight(MvCameraHeightData data)
        {
            MainAdjustment?.SetCharacterHeight(data);
            SubAdjustment?.SetCharacterHeight(data);
        }

        public void Dispose()
        {
            if (SubCamera != null && SubCamera.targetTexture == _subCameraTarget)
            {
                SubCamera.targetTexture = null;
            }
            if (_subCameraTarget != null)
            {
                _subCameraTarget.Release();
                Destroy(_subCameraTarget);
                _subCameraTarget = null;
            }
            LiveMonitorRuntime.SubCameraTexture = null;
            Destroy(MainCameraRoot);
            Destroy(SubCameraRoot);
            MainCameraRoot = null;
            SubCameraRoot = null;
            MainCamera = null;
            SubCamera = null;
            CameraDecoration = null;
            MainAdjustment = null;
            SubAdjustment = null;
            PostEffectState = null;
            PostEffect = null;
            MeshFlareRoot = null;
            MeshFlareController = null;
            UsedRecoveredResourceFallback = false;
        }

        public static Vector2Int? SubCameraSize(MusicVideoCameraInfo cameraInfo)
        {
            if (cameraInfo == null)
            {
                throw new ArgumentNullException(nameof(cameraInfo));
            }
            switch (cameraInfo.subCameraResolution)
            {
                case 1:
                    if (cameraInfo.subCameraCustomWidth <= 0 ||
                        cameraInfo.subCameraCustomHeight <= 0)
                    {
                        throw new InvalidOperationException(
                            "Custom sub-camera resolution must be positive.");
                    }
                    return new Vector2Int(
                        cameraInfo.subCameraCustomWidth,
                        cameraInfo.subCameraCustomHeight);
                case 2:
                    return null;
                default:
                    return new Vector2Int(256, 128);
            }
        }

        private static RenderTexture CreateCapturedCameraTarget(Vector2Int size)
        {
            var descriptor = new RenderTextureDescriptor(
                size.x,
                size.y,
                GraphicsFormat.R8G8B8A8_UNorm,
                24)
            {
                msaaSamples = 1,
                volumeDepth = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false,
                useDynamicScale = false,
            };
            return new RenderTexture(descriptor);
        }

        private GameObject InstantiateResource(string path, string instanceName)
        {
            var prefab = _loadResource(path);
            GameObject instance;
            if (prefab != null)
            {
                instance = UnityEngine.Object.Instantiate(prefab, _root, false);
            }
            else
            {
                instance = MvRecoveredCameraResources.Create(path);
                instance.transform.SetParent(_root, false);
                UsedRecoveredResourceFallback = true;
            }
            instance.name = instanceName;
            return instance;
        }

        private static Camera RequireCamera(GameObject root, string path)
        {
            var camera = root.GetComponent<Camera>();
            if (camera == null)
            {
                camera = root.GetComponentInChildren<Camera>(true);
            }
            if (camera == null)
            {
                throw new InvalidOperationException(
                    $"Official MV resource '{path}' has no Camera.");
            }
            return camera;
        }

        private void LoadMeshFlare(int mvId)
        {
            if (_bundles == null)
            {
                throw new InvalidOperationException(
                    "MeshFlarePara loading requires the MV bundle catalog.");
            }
            var controllerBundle = MvOfficialRuntimeData.MeshFlareControllerBundleName;
            var textureBundle = MvOfficialRuntimeData.MeshFlareTextureBundleName(mvId);
            if (!_bundles.ContainsBundle(controllerBundle))
            {
                throw new InvalidOperationException(
                    $"MV {mvId} enables MeshFlarePara but '{controllerBundle}' is unavailable.");
            }
            if (!_bundles.ContainsBundle(textureBundle))
            {
                throw new InvalidOperationException(
                    $"MV {mvId} enables MeshFlarePara but '{textureBundle}' is unavailable.");
            }

            MeshFlareRoot = _bundles.CreatePrefabInstance(
                new MvPrefabLoadRequest
                {
                    bundleName = controllerBundle,
                    assetName = "mesh_flare_para",
                },
                _root,
                "MeshFlarePara");
            MeshFlareController =
                MeshFlareRoot.GetComponent<MeshFlareParaController>() ??
                MeshFlareRoot.GetComponentInChildren<MeshFlareParaController>(true) ??
                throw new InvalidOperationException(
                    "Official MeshFlarePara prefab has no MeshFlareParaController.");
            var textureData = _bundles.LoadAllAssets<MeshFlareParaTexData>(textureBundle)
                .FirstOrDefault(data => data != null && data.Id == mvId) ??
                _bundles.LoadAllAssets<MeshFlareParaTexData>(textureBundle)
                    .FirstOrDefault(data => data != null) ??
                throw new InvalidOperationException(
                    $"MeshFlarePara texture bundle '{textureBundle}' has no texture data.");
            MeshFlareController.Setup(MainCamera, textureData.Texture2Ds);
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
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
