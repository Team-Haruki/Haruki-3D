using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Core;

namespace Haruki.MV
{
    public enum MvLightCategory
    {
        GlobalSettings = 0,
        AmbientLight = 1,
        DirectionalLight = 2,
        SpotLight = 3,
        CharacterRimLight = 4,
        CharacterAmbientLight = 5,
        ShadowLight = 6,
        ShadowLightVirtualLive = 7,
        FlareLight = 8,
        PointLight = 9,
    }

    public sealed class MvResolvedStageInfo
    {
        public int Id { get; internal set; }
        public bool OverrideTexture { get; internal set; }
        public MusicVideoPenlightInfo PenlightInfo { get; internal set; }
        public MusicVideoStageDecorationInfo[] StageDecorationInfos { get; internal set; }
        public bool EnableLensFlare { get; internal set; }
        public bool EnableWaterCaustics { get; internal set; }
        public bool EnableHeightFog { get; internal set; }
        public bool EnablePlanarReflection { get; internal set; }
        public bool EnablePlanarReflectionSorting { get; internal set; }
        public bool EnableEffectDistortion { get; internal set; }
        public bool InheritStage { get; internal set; }
        public bool SkipBaseStageLoad { get; internal set; }
        public int[] AdditionalOverrideTextureMusicVideoIds { get; internal set; }
        public bool OverrideAdditionalStageTexture { get; internal set; }
        public MusicVideoStageDecorationInfo[] AdditionalStageDecorationInfos { get; internal set; }
    }

    public sealed class MvCameraHeightData
    {
        public float[] Heights { get; internal set; }
        public float[] HeelOffsets { get; internal set; }
        public float[] DefaultHeelOffsets { get; internal set; }
    }

    public static class MvOfficialRuntimeData
    {
        private const float CameraHeightBase = 0.883f;
        private static readonly IReadOnlyList<MvLightCategory> LightCategories =
            Array.AsReadOnly(new[]
            {
                MvLightCategory.GlobalSettings,
                MvLightCategory.AmbientLight,
                MvLightCategory.DirectionalLight,
                MvLightCategory.SpotLight,
                MvLightCategory.CharacterRimLight,
                MvLightCategory.CharacterAmbientLight,
                MvLightCategory.ShadowLight,
            });

        public static IReadOnlyList<MvLightCategory> MusicVideoLightCategories =>
            LightCategories;

        public static string MusicVideoDataBundleName(int mvId, bool isCutIn = false)
        {
            ValidateMvId(mvId);
            return $"live_pv/mv_data/{mvId.ToString(isCutIn ? "D6" : "D")}";
        }

        public static string ResolveMusicVideoDataBundleName(
            int mvId,
            Func<string, bool> bundleExists,
            bool isCutIn = false)
        {
            if (bundleExists == null)
            {
                throw new ArgumentNullException(nameof(bundleExists));
            }

            var requested = MusicVideoDataBundleName(mvId, isCutIn);
            if (bundleExists(requested))
            {
                return requested;
            }

            var catalogCompatible = $"live_pv/mv_data/{mvId:D4}";
            return catalogCompatible != requested && bundleExists(catalogCompatible)
                ? catalogCompatible
                : requested;
        }

        public static int[] OptionalCutInIds(
            MusicVideoData mainMvData,
            bool enabled,
            Func<int, bool> isAvailable)
        {
            if (mainMvData == null)
            {
                throw new ArgumentNullException(nameof(mainMvData));
            }
            if (isAvailable == null)
            {
                throw new ArgumentNullException(nameof(isAvailable));
            }
            if (!enabled || mainMvData.cutinInfo?.ChildIds == null)
            {
                return Array.Empty<int>();
            }

            return mainMvData.cutinInfo.ChildIds
                .Where(id => id > 0 && isAvailable(id))
                .ToArray();
        }

        public static string StageBundleName(int stageId)
        {
            ValidateMvId(stageId);
            return $"live_pv/model/stage/{stageId:D4}";
        }

        public static string StageDecorationBundleName(int decorationId)
        {
            ValidateMvId(decorationId);
            return "live_pv/model/stage_decoration/" + CatalogId(decorationId);
        }

        public static string StageOverrideTextureBundleName(int mvId)
        {
            ValidateMvId(mvId);
            return "live_pv/model/stage_override_texture/" + CatalogId(mvId);
        }

        public static string CharacterFaceBundleName(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Character face model ID is required.", nameof(modelId));
            }
            return "live_pv/model/characterv2/face/" + modelId.Trim('/');
        }

        public static string CharacterBodyBundlePrefix(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Character body model ID is required.", nameof(modelId));
            }
            return "live_pv/model/characterv2/body/" + modelId.Trim('/') + "/";
        }

        public static string TimelineBundleName(
            int mvId,
            string timelineName,
            bool sixDigitId = false)
        {
            ValidateMvId(mvId);
            if (string.IsNullOrWhiteSpace(timelineName))
            {
                throw new ArgumentException("Timeline name is required.", nameof(timelineName));
            }

            return $"live_pv/timeline/{mvId.ToString(sixDigitId ? "D6" : "D")}/" +
                timelineName.ToLowerInvariant();
        }

        public static string ResolveTimelineBundleName(
            int mvId,
            string timelineName,
            Func<string, bool> bundleExists,
            bool sixDigitId = false)
        {
            if (bundleExists == null)
            {
                throw new ArgumentNullException(nameof(bundleExists));
            }

            var requested = TimelineBundleName(mvId, timelineName, sixDigitId);
            if (bundleExists(requested))
            {
                return requested;
            }

            // Some exported catalogs preserve four-digit main-MV directories
            // even though the recovered runtime helper formats the int without
            // padding. Accept that physical name before applying the official
            // resource-level fallback.
            var catalogCompatible = $"live_pv/timeline/{mvId:D4}/" +
                timelineName.ToLowerInvariant();
            if (catalogCompatible != requested && bundleExists(catalogCompatible))
            {
                return catalogCompatible;
            }

            return "live_pv/timeline/0001/" + timelineName.ToLowerInvariant();
        }

        public static int MainCharacterCount(MusicVideoData mvData)
        {
            if (mvData == null)
            {
                throw new ArgumentNullException(nameof(mvData));
            }

            return (mvData.characterInfos ?? Array.Empty<MusicVideoCharacterInfo>())
                .Count(info => info != null && !info.isInsertCharacter);
        }

        public static MvResolvedStageInfo ResolveStage(
            MusicVideoData mvData,
            MusicVideoData parentMvData = null)
        {
            if (mvData?.stageInfo == null)
            {
                throw new ArgumentException("MusicVideoData must contain StageInfo.", nameof(mvData));
            }

            var child = mvData.stageInfo;
            if (!child.inheritStage)
            {
                return CopyStage(child);
            }
            if (parentMvData?.stageInfo == null)
            {
                throw new InvalidOperationException(
                    $"MV {mvData.id} inherits its stage but has no parent MusicVideoData.");
            }

            var parent = parentMvData.stageInfo;
            return new MvResolvedStageInfo
            {
                Id = parent.id,
                OverrideTexture = true,
                PenlightInfo = child.penlightInfo,
                StageDecorationInfos = Copy(child.stageDecorationInfos),
                EnableLensFlare = parent.enableLensFlare,
                EnableWaterCaustics = parent.enableWaterCaustics,
                EnableHeightFog = parent.enableHeightFog,
                EnablePlanarReflection = child.enablePlanarReflection,
                EnablePlanarReflectionSorting = child.enablePlanarReflectionSorting,
                EnableEffectDistortion = parent.enableEffectDistortion,
                InheritStage = true,
                SkipBaseStageLoad = child.skipBaseStageLoad,
                AdditionalOverrideTextureMusicVideoIds = new[] { parentMvData.id },
                OverrideAdditionalStageTexture = parent.overrideTexture,
                AdditionalStageDecorationInfos = Copy(parent.stageDecorationInfos),
            };
        }

        public static MvCameraHeightData CreateCameraHeightData(
            IReadOnlyList<float> heights,
            IReadOnlyList<float> heelOffsets,
            IReadOnlyList<MusicVideoCharacterInfo> characterInfos)
        {
            if (heights == null)
            {
                throw new ArgumentNullException(nameof(heights));
            }
            if (heelOffsets == null)
            {
                throw new ArgumentNullException(nameof(heelOffsets));
            }
            if (characterInfos == null)
            {
                throw new ArgumentNullException(nameof(characterInfos));
            }
            if (heights.Count != heelOffsets.Count || heights.Count != characterInfos.Count)
            {
                throw new ArgumentException(
                    "Character heights, heel offsets, and MV character slots must have equal lengths.");
            }

            return new MvCameraHeightData
            {
                Heights = heights.ToArray(),
                HeelOffsets = heelOffsets.ToArray(),
                DefaultHeelOffsets = characterInfos
                    .Select(info => info?.defaultHeelOffset ?? 0)
                    .ToArray(),
            };
        }

        public static float CameraHeightOffset(
            float actualHeight,
            float actualHeelOffset,
            float selectedDefaultHeight,
            float mvDefaultHeelOffset)
        {
            return actualHeight * (actualHeelOffset + CameraHeightBase) -
                selectedDefaultHeight * (mvDefaultHeelOffset + CameraHeightBase);
        }

        public static float BlendedCameraHeightOffset(
            float firstOffset,
            float secondOffset,
            float targetLerp)
        {
            return firstOffset +
                (secondOffset - firstOffset) * Math.Max(0, Math.Min(targetLerp, 1));
        }

        public static Dictionary<string, T> MergeStageOverrideTextures<T>(
            IReadOnlyDictionary<string, T> current,
            IEnumerable<IReadOnlyDictionary<string, T>> additional)
        {
            var merged = current == null
                ? new Dictionary<string, T>()
                : new Dictionary<string, T>(current);
            if (additional == null)
            {
                return merged;
            }

            foreach (var fallback in additional)
            {
                if (fallback == null)
                {
                    continue;
                }

                foreach (var pair in fallback)
                {
                    if (!merged.ContainsKey(pair.Key))
                    {
                        merged.Add(pair.Key, pair.Value);
                    }
                }
            }

            return merged;
        }

        private static MvResolvedStageInfo CopyStage(MusicVideoStageInfo info)
        {
            return new MvResolvedStageInfo
            {
                Id = info.id,
                OverrideTexture = info.overrideTexture,
                PenlightInfo = info.penlightInfo,
                StageDecorationInfos = Copy(info.stageDecorationInfos),
                EnableLensFlare = info.enableLensFlare,
                EnableWaterCaustics = info.enableWaterCaustics,
                EnableHeightFog = info.enableHeightFog,
                EnablePlanarReflection = info.enablePlanarReflection,
                EnablePlanarReflectionSorting = info.enablePlanarReflectionSorting,
                EnableEffectDistortion = info.enableEffectDistortion,
                InheritStage = false,
                SkipBaseStageLoad = info.skipBaseStageLoad,
                AdditionalOverrideTextureMusicVideoIds = Array.Empty<int>(),
                OverrideAdditionalStageTexture = false,
                AdditionalStageDecorationInfos = Array.Empty<MusicVideoStageDecorationInfo>(),
            };
        }

        private static MusicVideoStageDecorationInfo[] Copy(
            MusicVideoStageDecorationInfo[] infos)
        {
            return infos == null
                ? Array.Empty<MusicVideoStageDecorationInfo>()
                : (MusicVideoStageDecorationInfo[])infos.Clone();
        }

        private static void ValidateMvId(int mvId)
        {
            if (mvId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(mvId));
            }
        }

        private static string CatalogId(int id)
        {
            return id < 10000 ? id.ToString("D4") : id.ToString();
        }
    }
}
