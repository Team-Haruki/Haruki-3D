using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Haruki.MV
{
    public sealed class MvTimelineModel
    {
        public MvTimelineModel(IReadOnlyList<PlayableDirector> directors)
        {
            Directors = directors;
        }

        public IReadOnlyList<PlayableDirector> Directors { get; }
    }

    public sealed class MvCameraModel
    {
        public MvCameraModel(
            GameObject mainCamera,
            GameObject subCamera,
            MvCameraAdjustment mainAdjustment,
            MvCameraAdjustment subAdjustment)
        {
            MainCamera = mainCamera;
            SubCamera = subCamera;
            MainAdjustment = mainAdjustment;
            SubAdjustment = subAdjustment;
        }

        public GameObject MainCamera { get; }
        public GameObject SubCamera { get; }
        public MvCameraAdjustment MainAdjustment { get; }
        public MvCameraAdjustment SubAdjustment { get; }
    }

    public sealed class MvLightModel
    {
        public MvLightModel(GameObject directionalLight, GameObject shadowLight)
        {
            DirectionalLight = directionalLight;
            ShadowLight = shadowLight;
        }

        public GameObject DirectionalLight { get; }
        public GameObject ShadowLight { get; }
    }

    public sealed class MvStageModel
    {
        public MvStageModel(
            GameObject baseStage,
            IReadOnlyList<GameObject> decorations)
        {
            BaseStage = baseStage;
            Decorations = decorations;
        }

        public GameObject BaseStage { get; }
        public IReadOnlyList<GameObject> Decorations { get; }
    }

    public sealed class MvCharacterModel
    {
        public MvCharacterModel(IReadOnlyList<MvCharacterInstance> characters)
        {
            Characters = characters;
        }

        public IReadOnlyList<MvCharacterInstance> Characters { get; }
    }

    public sealed class MvPenlightModel
    {
        public MvPenlightModel(GameObject penlight)
        {
            Penlight = penlight;
        }

        public GameObject Penlight { get; }
    }

    public sealed class MvRegisteredPlayerModels
    {
        public MvTimelineModel Timeline { get; internal set; }
        public MvCameraModel Camera { get; internal set; }
        public MvLightModel Light { get; internal set; }
        public MvStageModel Stage { get; internal set; }
        public MvCharacterModel Character { get; internal set; }
        public MvPenlightModel Penlight { get; internal set; }
    }

    public sealed class MvMusicVideoModel
    {
        private readonly MvRegisteredPlayerModels[] _cutIns;

        private MvMusicVideoModel(int cutInCount)
        {
            if (cutInCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cutInCount));
            }

            Main = new MvRegisteredPlayerModels();
            _cutIns = new MvRegisteredPlayerModels[cutInCount];
            for (var index = 0; index < _cutIns.Length; index++)
            {
                _cutIns[index] = new MvRegisteredPlayerModels();
            }
        }

        public MvRegisteredPlayerModels Main { get; }
        public IReadOnlyList<MvRegisteredPlayerModels> CutIns => _cutIns;

        public static MvMusicVideoModel Create(int cutInCount)
        {
            return new MvMusicVideoModel(cutInCount);
        }

        public void RegisterMainTimeline(MvTimelineModel model)
        {
            Main.Timeline = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterCutInTimeline(MvTimelineModel model, int cutInOrder)
        {
            CutIn(cutInOrder).Timeline = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterMainCamera(MvCameraModel model)
        {
            Main.Camera = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterCutInCamera(MvCameraModel model, int cutInOrder)
        {
            CutIn(cutInOrder).Camera = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterMainLight(MvLightModel model)
        {
            Main.Light = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterCutInLight(MvLightModel model, int cutInOrder)
        {
            CutIn(cutInOrder).Light = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterMainStage(MvStageModel model)
        {
            Main.Stage = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterCutInStage(MvStageModel model, int cutInOrder)
        {
            CutIn(cutInOrder).Stage = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterMainCharacter(MvCharacterModel model)
        {
            Main.Character = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterCutInCharacter(MvCharacterModel model, int cutInOrder)
        {
            CutIn(cutInOrder).Character = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterMainPenlight(MvPenlightModel model)
        {
            Main.Penlight = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void RegisterCutInPenlight(MvPenlightModel model, int cutInOrder)
        {
            CutIn(cutInOrder).Penlight = model ?? throw new ArgumentNullException(nameof(model));
        }

        private MvRegisteredPlayerModels CutIn(int cutInOrder)
        {
            if (cutInOrder < 0 || cutInOrder >= _cutIns.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(cutInOrder));
            }
            return _cutIns[cutInOrder];
        }
    }
}
