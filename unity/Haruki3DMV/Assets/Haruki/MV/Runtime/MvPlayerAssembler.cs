using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Core;
using UnityEngine;

namespace Haruki.MV
{
    [Serializable]
    public sealed class MvCutInLoadSpec
    {
        public int musicId;
        public bool reuseMainMember;
        public MvCharacterLoadSpec[] characters = Array.Empty<MvCharacterLoadSpec>();
    }

    [Serializable]
    public sealed class MvPlayerLoadRequest
    {
        public string requestId;
        public int musicId;
        public bool enableCutIns;
        public MvCharacterLoadSpec[] characters = Array.Empty<MvCharacterLoadSpec>();
        public MvCutInLoadSpec[] cutIns = Array.Empty<MvCutInLoadSpec>();
        public string audioBundleName;
        public string audioAssetName;
    }

    public sealed class MvPlayerInstance : IDisposable
    {
        private readonly Dictionary<string, UnityEngine.Object> _bindings =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

        internal MvPlayerInstance(
            MvBundleSetLoader bundles,
            MusicVideoData mvData,
            MusicVideoData parentMvData,
            IReadOnlyList<MvCharacterLoadSpec> characters,
            bool isCutIn,
            int cutInOrder,
            MvMusicVideoModel musicVideoModel,
            Transform parent)
        {
            MvData = mvData ?? throw new ArgumentNullException(nameof(mvData));
            if (musicVideoModel == null)
            {
                throw new ArgumentNullException(nameof(musicVideoModel));
            }
            IsCutIn = isCutIn;
            CutInOrder = cutInOrder;
            Root = new GameObject($"Background3DPlayer_ID{mvData.id}");
            Root.transform.SetParent(parent, false);

            Timeline = new MvTimelineNode();
            Camera = new MvCameraNode(_bindings, Root.transform, bundles: bundles);
            Light = new MvLightNode(_bindings, Root.transform);
            Stage = new MvStageNode(bundles, _bindings, Root.transform);
            Character = new MvCharacterNode(bundles, _bindings, Root.transform);
            Penlight = new MvPenlightNode(bundles, _bindings, Root.transform);
            try
            {
                Timeline.Initialize(_bindings, Root.transform);
                Timeline.LoadTimelines(bundles, mvData.id, isCutIn, cutInOrder);
                Register(
                    musicVideoModel,
                    new MvTimelineModel(Timeline.Directors),
                    isCutIn,
                    cutInOrder);
                Camera.Load(mvData);
                Register(
                    musicVideoModel,
                    new MvCameraModel(
                        Camera.MainCameraRoot,
                        Camera.SubCameraRoot,
                        Camera.MainAdjustment,
                        Camera.SubAdjustment),
                    isCutIn,
                    cutInOrder);
                Light.Load(mvData, Camera.MainCamera);
                Register(
                    musicVideoModel,
                    new MvLightModel(Light.DirectionalLight, Light.ShadowLight),
                    isCutIn,
                    cutInOrder);
                Stage.Load(
                    mvData,
                    parentMvData,
                    isCutIn,
                    characters,
                    Camera.MainCamera,
                    Light.DirectionalLight.transform);
                Register(
                    musicVideoModel,
                    new MvStageModel(Stage.BaseStage, Stage.Decorations),
                    isCutIn,
                    cutInOrder);
                Character.Load(mvData, characters, Light.DirectionalLight.transform);
                Register(
                    musicVideoModel,
                    new MvCharacterModel(Character.Characters),
                    isCutIn,
                    cutInOrder);
                Camera.SetCharacterHeight(Character.CreateCameraHeightData(mvData));
                Penlight.Load(Stage.StageInfo.PenlightInfo);
                Register(
                    musicVideoModel,
                    new MvPenlightModel(Penlight.Penlight),
                    isCutIn,
                    cutInOrder);
                MvPlayerRenderSettings.Apply(Root);
                Timeline.BindTimeline();
                TimelinePlayback = Root.AddComponent<MvTimelinePlaybackParticipant>();
                TimelinePlayback.Initialize(Timeline);
                if (isCutIn)
                {
                    Root.SetActive(false);
                }
            }
            catch
            {
                Timeline.Dispose();
                Penlight.Dispose();
                Character.Dispose();
                Stage.Dispose();
                Light.Dispose();
                Camera.Dispose();
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
                throw;
            }
        }

        public GameObject Root { get; }
        public MusicVideoData MvData { get; }
        public bool IsCutIn { get; }
        public int CutInOrder { get; }
        public MvTimelineNode Timeline { get; }
        public MvCameraNode Camera { get; }
        public MvLightNode Light { get; }
        public MvStageNode Stage { get; }
        public MvCharacterNode Character { get; }
        public MvPenlightNode Penlight { get; }
        public MvTimelinePlaybackParticipant TimelinePlayback { get; }
        public double Duration => Timeline.PlaybackDuration;

        public void Dispose()
        {
            Timeline.Dispose();
            Penlight.Dispose();
            Character.Dispose();
            Stage.Dispose();
            Light.Dispose();
            Camera.Dispose();
            if (Root != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
            }
        }

        private static void Register(
            MvMusicVideoModel registry,
            MvTimelineModel model,
            bool isCutIn,
            int cutInOrder)
        {
            if (isCutIn) registry.RegisterCutInTimeline(model, cutInOrder);
            else registry.RegisterMainTimeline(model);
        }

        private static void Register(
            MvMusicVideoModel registry,
            MvCameraModel model,
            bool isCutIn,
            int cutInOrder)
        {
            if (isCutIn) registry.RegisterCutInCamera(model, cutInOrder);
            else registry.RegisterMainCamera(model);
        }

        private static void Register(
            MvMusicVideoModel registry,
            MvLightModel model,
            bool isCutIn,
            int cutInOrder)
        {
            if (isCutIn) registry.RegisterCutInLight(model, cutInOrder);
            else registry.RegisterMainLight(model);
        }

        private static void Register(
            MvMusicVideoModel registry,
            MvStageModel model,
            bool isCutIn,
            int cutInOrder)
        {
            if (isCutIn) registry.RegisterCutInStage(model, cutInOrder);
            else registry.RegisterMainStage(model);
        }

        private static void Register(
            MvMusicVideoModel registry,
            MvCharacterModel model,
            bool isCutIn,
            int cutInOrder)
        {
            if (isCutIn) registry.RegisterCutInCharacter(model, cutInOrder);
            else registry.RegisterMainCharacter(model);
        }

        private static void Register(
            MvMusicVideoModel registry,
            MvPenlightModel model,
            bool isCutIn,
            int cutInOrder)
        {
            if (isCutIn) registry.RegisterCutInPenlight(model, cutInOrder);
            else registry.RegisterMainPenlight(model);
        }
    }

    [RequireComponent(typeof(MvBundleSetLoader), typeof(MvPlaybackCoordinator))]
    public sealed class MvPlayerAssembler : MonoBehaviour
    {
        private readonly List<MvPlayerInstance> _players = new List<MvPlayerInstance>();
        private MvBundleSetLoader _bundles;
        private MvPlaybackCoordinator _coordinator;
        private MvCutInController _cutInController;
        private MvRenderCanvas _renderCanvas;

        public IReadOnlyList<MvPlayerInstance> Players => _players;
        public MvPlayerInstance MainPlayer => _players.Count == 0 ? null : _players[0];
        public MvMusicVideoModel MusicVideoModel { get; private set; }
        public MvRenderCanvas RenderCanvas => _renderCanvas;

        private void Awake()
        {
            _bundles = GetComponent<MvBundleSetLoader>();
            _coordinator = GetComponent<MvPlaybackCoordinator>();
        }

        public void Load(MvPlayerLoadRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (request.musicId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.musicId));
            }

            DisposePlayers();
            try
            {
                var mainData = LoadMvData(request.musicId, false);
                var declaredCutInIds = mainData.cutinInfo?.ChildIds ?? Array.Empty<int>();
                MusicVideoModel = MvMusicVideoModel.Create(declaredCutInIds.Length);
                var main = new MvPlayerInstance(
                    _bundles,
                    mainData,
                    null,
                    request.characters,
                    false,
                    -1,
                    MusicVideoModel,
                    transform);
                _players.Add(main);

                var requestedCutIns = new Dictionary<int, MvCutInLoadSpec>();
                foreach (var spec in request.cutIns ?? Array.Empty<MvCutInLoadSpec>())
                {
                    if (spec != null && spec.musicId > 0)
                    {
                        requestedCutIns[spec.musicId] = spec;
                    }
                }
                var availableCutInIds = new HashSet<int>(MvOfficialRuntimeData.OptionalCutInIds(
                    mainData,
                    request.enableCutIns,
                    id => _bundles.ContainsBundle(
                        MvOfficialRuntimeData.ResolveMusicVideoDataBundleName(
                            id,
                            _bundles.ContainsBundle,
                            true))));
                for (var order = 0; order < declaredCutInIds.Length; order++)
                {
                    var childId = declaredCutInIds[order];
                    if (!availableCutInIds.Contains(childId))
                    {
                        continue;
                    }
                    requestedCutIns.TryGetValue(childId, out var cutInSpec);
                    try
                    {
                        var childData = LoadMvData(childId, true);
                        var childCharacters = MvOfficialRuntimeData.ResolveCutInCharacters(
                            mainData,
                            childData,
                            request.characters,
                            cutInSpec?.reuseMainMember ?? false,
                            cutInSpec?.characters);
                        _players.Add(new MvPlayerInstance(
                            _bundles,
                            childData,
                            mainData,
                            childCharacters,
                            true,
                            order,
                            MusicVideoModel,
                            transform));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            $"Optional CutIn MV {childId} was skipped: {exception.Message}");
                    }
                }

                if (_players.Any(player => player.IsCutIn))
                {
                    _cutInController = gameObject.AddComponent<MvCutInController>();
                    _cutInController.Initialize(this, _bundles, mainData.id);
                }

                EnsureRenderCanvas();
                BindOutputCameras();

                AudioSource audioSource = null;
                if (string.IsNullOrWhiteSpace(request.audioBundleName) !=
                    string.IsNullOrWhiteSpace(request.audioAssetName))
                {
                    throw new InvalidOperationException(
                        "MV audioBundleName and audioAssetName must be supplied together.");
                }
                if (!string.IsNullOrWhiteSpace(request.audioBundleName) &&
                    !string.IsNullOrWhiteSpace(request.audioAssetName))
                {
                    var clip = _bundles.LoadAsset<AudioClip>(
                        request.audioBundleName,
                        request.audioAssetName);
                    if (clip == null)
                    {
                        throw new InvalidOperationException(
                            $"MV audio asset '{request.audioAssetName}' was not found.");
                    }
                    audioSource = main.Root.AddComponent<AudioSource>();
                    audioSource.clip = clip;
                    audioSource.playOnAwake = false;
                }

                _coordinator.BindScene(
                    _players.Select(player => player.Root).ToArray(),
                    audioSource,
                    main.Duration,
                    _cutInController == null
                        ? Array.Empty<IMvPlaybackParticipant>()
                        : new IMvPlaybackParticipant[] { _cutInController });
            }
            catch
            {
                DisposePlayers();
                throw;
            }
        }

        public void SetCutInSceneActive(int cutInOrder, bool active)
        {
            var player = _players.FirstOrDefault(
                candidate => candidate.IsCutIn && candidate.CutInOrder == cutInOrder);
            if (player == null)
            {
                throw new ArgumentOutOfRangeException(nameof(cutInOrder));
            }
            if (active)
            {
                _coordinator.SetActiveSceneRoot(player.Root);
                return;
            }
            if (player.Root.activeSelf)
            {
                _coordinator.SetActiveSceneRoot(MainPlayer.Root);
            }
        }

        public void ApplyOutputProfile(MvRenderProfile profile)
        {
            if (_renderCanvas == null)
            {
                _renderCanvas = new MvRenderCanvas(transform);
            }
            _renderCanvas.Configure(profile.RenderSize);
            BindOutputCameras();
        }

        public bool HasCutIn(int cutInOrder)
        {
            return _players.Any(
                candidate => candidate.IsCutIn && candidate.CutInOrder == cutInOrder);
        }

        public void DisposePlayers()
        {
            if (_coordinator != null)
            {
                _coordinator.DisposeScene();
            }
            if (_cutInController != null)
            {
                _cutInController.Dispose();
                if (Application.isPlaying) Destroy(_cutInController);
                else DestroyImmediate(_cutInController);
            }
            _cutInController = null;
            for (var index = _players.Count - 1; index >= 0; index--)
            {
                _renderCanvas?.Unbind(_players[index].Camera.MainCamera);
                _players[index].Dispose();
            }
            _players.Clear();
            MusicVideoModel = null;
        }

        private void OnDestroy()
        {
            DisposePlayers();
            _renderCanvas?.Dispose();
            _renderCanvas = null;
        }

        private void EnsureRenderCanvas()
        {
            if (_renderCanvas != null)
            {
                return;
            }
            _renderCanvas = new MvRenderCanvas(transform);
            _renderCanvas.Configure(new Vector2Int(
                Mathf.Max(Screen.width, 1),
                Mathf.Max(Screen.height, 1)));
        }

        private void BindOutputCameras()
        {
            if (_renderCanvas?.Target == null)
            {
                return;
            }
            foreach (var player in _players)
            {
                _renderCanvas.Bind(player.Camera.MainCamera);
            }
        }

        private MusicVideoData LoadMvData(int musicId, bool isCutIn)
        {
            var bundleName = MvOfficialRuntimeData.ResolveMusicVideoDataBundleName(
                musicId,
                _bundles.ContainsBundle,
                isCutIn);
            if (!_bundles.ContainsBundle(bundleName))
            {
                throw new InvalidOperationException(
                    $"MV data bundle '{bundleName}' is not loaded.");
            }
            return _bundles.LoadAsset<MusicVideoData>(bundleName, "data") ??
                throw new InvalidOperationException(
                    $"MV data bundle '{bundleName}' has no MusicVideoData asset.");
        }
    }
}
