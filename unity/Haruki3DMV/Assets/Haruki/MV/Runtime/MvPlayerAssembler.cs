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
            Transform parent)
        {
            MvData = mvData ?? throw new ArgumentNullException(nameof(mvData));
            IsCutIn = isCutIn;
            CutInOrder = cutInOrder;
            Root = new GameObject($"Background3DPlayer_ID{mvData.id}");
            Root.transform.SetParent(parent, false);

            Timeline = new MvTimelineNode();
            Stage = new MvStageNode(bundles, _bindings, Root.transform);
            Character = new MvCharacterNode(bundles, _bindings, Root.transform);
            try
            {
                Timeline.Initialize(_bindings, Root.transform);
                Timeline.LoadTimelines(bundles, mvData.id, isCutIn, cutInOrder);
                Stage.Load(mvData, parentMvData, isCutIn);
                Character.Load(mvData, characters);
                MvPlayerRenderSettings.Apply(Root);
                Timeline.BindTimeline();
                TimelinePlayback = Root.AddComponent<MvTimelinePlaybackParticipant>();
                TimelinePlayback.Initialize(Timeline);
                if (isCutIn)
                {
                    SetActive(false);
                }
            }
            catch
            {
                Timeline.Dispose();
                Character.Dispose();
                Stage.Dispose();
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
        public MvStageNode Stage { get; }
        public MvCharacterNode Character { get; }
        public MvTimelinePlaybackParticipant TimelinePlayback { get; }
        public double Duration => Timeline.TimelineDuration;

        public void SetActive(bool active)
        {
            if (active)
            {
                Root.SetActive(true);
                TimelinePlayback.ActivateAtCurrentTime();
            }
            else
            {
                TimelinePlayback.DeactivateAtCurrentTime();
                Root.SetActive(false);
            }
        }

        public void Dispose()
        {
            Timeline.Dispose();
            Character.Dispose();
            Stage.Dispose();
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
    }

    [RequireComponent(typeof(MvBundleSetLoader), typeof(MvPlaybackCoordinator))]
    public sealed class MvPlayerAssembler : MonoBehaviour
    {
        private readonly List<MvPlayerInstance> _players = new List<MvPlayerInstance>();
        private MvBundleSetLoader _bundles;
        private MvPlaybackCoordinator _coordinator;

        public IReadOnlyList<MvPlayerInstance> Players => _players;
        public MvPlayerInstance MainPlayer => _players.Count == 0 ? null : _players[0];

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
                var main = new MvPlayerInstance(
                    _bundles,
                    mainData,
                    null,
                    request.characters,
                    false,
                    -1,
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
                var declaredCutInIds = mainData.cutinInfo?.ChildIds ?? Array.Empty<int>();
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
                        _players.Add(new MvPlayerInstance(
                            _bundles,
                            childData,
                            mainData,
                            cutInSpec?.characters ?? Array.Empty<MvCharacterLoadSpec>(),
                            true,
                            order,
                            transform));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            $"Optional CutIn MV {childId} was skipped: {exception.Message}");
                    }
                }

                AudioSource audioSource = null;
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

                var duration = audioSource != null && audioSource.clip != null
                    ? audioSource.clip.length
                    : _players.Max(player => player.Duration);
                _coordinator.BindScene(
                    _players.Select(player => player.Root).ToArray(),
                    audioSource,
                    duration);
            }
            catch
            {
                DisposePlayers();
                throw;
            }
        }

        public void SetCutInActive(int cutInOrder, bool active)
        {
            var player = _players.FirstOrDefault(
                candidate => candidate.IsCutIn && candidate.CutInOrder == cutInOrder);
            if (player == null)
            {
                throw new ArgumentOutOfRangeException(nameof(cutInOrder));
            }
            if (active)
            {
                foreach (var candidate in _players)
                {
                    if (candidate.IsCutIn && candidate != player)
                    {
                        candidate.SetActive(false);
                    }
                }
            }
            player.SetActive(active);
        }

        public void DisposePlayers()
        {
            if (_coordinator != null)
            {
                _coordinator.DisposeScene();
            }
            for (var index = _players.Count - 1; index >= 0; index--)
            {
                _players[index].Dispose();
            }
            _players.Clear();
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
