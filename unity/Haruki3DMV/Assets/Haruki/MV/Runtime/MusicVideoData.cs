using System;
using UnityEngine;

namespace Sekai.Core
{
    public sealed class MusicVideoData : MonoBehaviour
    {
        public int id;
        public string name;
        public MusicVideoCharacterInfo[] characterInfos = Array.Empty<MusicVideoCharacterInfo>();
        public MusicVideoStageInfo stageInfo;
        public MusicVideoCameraInfo cameraInfo;
        public MusicVideoPostEffectInfo postEffectInfo;
        public MusicVideoCutinInfo cutinInfo;
    }

    [Serializable]
    public sealed class MusicVideoCharacterInfo
    {
        public int id;
        public string headOptional;
        public string face;
        public string body;
        public string colorVariation;
        public string headOptionalColorVariation;
        public int prefabType;
        public bool isHeelOffset;
        public float defaultHeelOffset;
        public MusicVideoMotionInfo motionInfo;
        public MusicVideoItemInfo[] musicItemInfos = Array.Empty<MusicVideoItemInfo>();
        public bool useHairShadow;
        public bool disinheritCharacterInfo;
        public bool isLoadInActive;
        public bool isInsertCharacter;
        public bool isUseSpringBoneController;
    }

    [Serializable]
    public sealed class MusicVideoMotionInfo
    {
        public int motionType;
        public int[] uniqueCharacterIds = Array.Empty<int>();
    }

    [Serializable]
    public sealed class MusicVideoItemInfo
    {
    }

    [Serializable]
    public sealed class MusicVideoStageInfo
    {
        public int id;
        public bool overrideTexture;
        public MusicVideoPenlightInfo penlightInfo;
        public MusicVideoStageDecorationInfo[] stageDecorationInfos =
            Array.Empty<MusicVideoStageDecorationInfo>();
        public bool enableLensFlare;
        public bool enableWaterCaustics;
        public bool enableHeightFog;
        public bool enablePlanarReflection;
        public bool enablePlanarReflectionSorting;
        public bool enableEffectDistortion;
        public bool inheritStage;
        public bool skipBaseStageLoad;
    }

    [Serializable]
    public sealed class MusicVideoPenlightInfo
    {
        public int id;
    }

    [Serializable]
    public sealed class MusicVideoStageDecorationInfo
    {
        public int id;
    }

    [Serializable]
    public sealed class MusicVideoCameraInfo
    {
        public bool hasCameraDecoration;
        public bool useSubCamera;
        public int subCameraResolution;
        public bool isRenderManyCharacter;
        public int subCameraCustomWidth;
        public int subCameraCustomHeight;
        public int propCameraFovType;
        public float propCameraFixedFov;
    }

    [Serializable]
    public sealed class MusicVideoPostEffectInfo
    {
        public bool enableMeshFlarePara;
        public int SaturationBlurType;
    }

    [Serializable]
    public sealed class MusicVideoCutinInfo
    {
        public int[] ChildIds = Array.Empty<int>();
        public int ParentId;
    }
}
