using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haruki.MV
{
    /// <summary>
    /// The renderer-feature ordering observed in the 6.7.0 runtime capture.
    /// This is a validation contract, not a replacement implementation for
    /// the game's custom renderer features or shaders.
    /// </summary>
    public static class MvRecoveredRendererContract
    {
        public const float OutlineWidthMin = 0.04f;
        public const float OutlineWidthMax = 0.95f;
        public const float OutlineDistanceNear = 0.45f;
        public const float OutlineDistanceFar = 20f;
        public const int PlanarReflectionWidth = 1024;
        public const int PlanarReflectionHeight = 1024;
        public const float PlanarReflectionClipPlaneOffset = 0f;
        public const float PlanarReflectionPlaneOffset = 0f;
        public const string PlanarReflectionStencilShader = "Sekai/Live/DrawStencil";
        public const string ApplyDistortionShader = "Hidden/Sekai/Live/ApplyDistortion";

        public static AnimationCurve CreateOutlineFovCurve()
        {
            var start = new Keyframe(
                -0.013763427734375f,
                27.81246566772461f,
                -0.13214513659477234f,
                -0.13214513659477234f,
                0f,
                0.0478468873f)
            {
                weightedMode = WeightedMode.None,
            };
            var end = new Keyframe(
                100.92341613769531f,
                -0.03620624542236328f,
                -0.5713597536087036f,
                -0.5713597536087036f,
                0.0392344296f,
                0f)
            {
                weightedMode = WeightedMode.None,
            };
            return new AnimationCurve(start, end)
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.ClampForever,
            };
        }

        private static readonly IReadOnlyList<MvRendererFeatureDescriptor> MainFeatures =
            Array.AsReadOnly(new[]
            {
                Feature("OpaqueForward", "SekaiDrawObjectsRendererFeature"),
                Feature("OpaqueToon", "SekaiDrawObjectsRendererFeature"),
                Feature("TransparentForward", "SekaiDrawObjectsRendererFeature"),
                Feature("MusicItem", "SekaiMusicItemRendererFeature"),
                Feature("Opaque Reflection", "SekaiOpaqueReflectionRendererFeature"),
                Feature("TransparentReflection", "SekaiTransparentReflectionRendererFeature"),
                Feature("BeforePostProcess", "SekaiBeforePostProcessRendererFeature"),
                Feature("PostProcess", "SekaiPostProcessRendererFeature"),
                Feature("SekaiCharacterOutlineFeature", "SekaiCharacterOutlineFeature"),
                Feature("Eyelash", "SekaiDrawObjectsRendererFeature"),
                Feature("SekaiAfterTransparentRendererFeature", "SekaiAfterTransparentRendererFeature"),
                Feature("AfterPostProcess", "SekaiAfterPostProcessRendererFeature"),
                Feature("PlanarReflectionFeature", "PlanarReflectionFeature"),
            });

        private static readonly IReadOnlyList<MvRendererFeatureDescriptor> SubFeatures =
            Array.AsReadOnly(new[]
            {
                Feature("OpaqueForward", "SekaiDrawObjectsRendererFeature"),
                Feature("OpaqueToon", "SekaiDrawObjectsRendererFeature"),
                Feature("TransparentForward", "SekaiDrawObjectsRendererFeature"),
                Feature("MusicItem", "SekaiMusicItemRendererFeature"),
                Feature("Opaque Reflection", "SekaiOpaqueReflectionRendererFeature"),
                Feature("TransparentReflection", "SekaiTransparentReflectionRendererFeature"),
                Feature("SekaiCharacterOutlineFeature", "SekaiCharacterOutlineFeature"),
                Feature("Eyelash", "SekaiDrawObjectsRendererFeature"),
            });

        public static IReadOnlyList<MvRendererFeatureDescriptor> ForRenderer(int rendererIndex)
        {
            switch (rendererIndex)
            {
                case MvRecoveredCameraResources.MainRendererIndex:
                    return MainFeatures;
                case MvRecoveredCameraResources.SubRendererIndex:
                    return SubFeatures;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(rendererIndex),
                        rendererIndex,
                        "No recovered 6.7.0 renderer contract exists for this index.");
            }
        }

        public static IReadOnlyList<string> Validate(
            int rendererIndex,
            IReadOnlyList<MvRendererFeatureDescriptor> actual)
        {
            var expected = ForRenderer(rendererIndex);
            var errors = new List<string>();
            if (actual == null)
            {
                errors.Add($"renderer {rendererIndex} requires {expected.Count} features but was null");
                return errors;
            }

            if (actual.Count != expected.Count)
            {
                errors.Add(
                    $"renderer {rendererIndex} requires {expected.Count} features but found {actual.Count}");
            }

            var count = Math.Min(expected.Count, actual.Count);
            for (var index = 0; index < count; index++)
            {
                var expectedFeature = expected[index];
                var actualFeature = actual[index];
                if (!string.Equals(expectedFeature.Name, actualFeature.Name, StringComparison.Ordinal) ||
                    !string.Equals(expectedFeature.TypeName, actualFeature.TypeName, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"renderer {rendererIndex} position {index} requires " +
                        $"'{expectedFeature.Name}' ({expectedFeature.TypeName}) but found " +
                        $"'{actualFeature.Name}' ({actualFeature.TypeName})");
                }
            }

            return errors;
        }

        public static MvOutlineGlobals CalculateOutlineGlobals(
            float fieldOfView,
            AnimationCurve fovCurve)
        {
            return CalculateOutlineGlobals(
                fieldOfView,
                fovCurve,
                OutlineWidthMin,
                OutlineWidthMax,
                OutlineDistanceNear,
                OutlineDistanceFar);
        }

        public static MvOutlineGlobals CalculateOutlineGlobals(
            float fieldOfView,
            AnimationCurve fovCurve,
            float widthMin,
            float widthMax,
            float distanceNear,
            float distanceFar)
        {
            var curveValue = fovCurve == null
                ? fieldOfView
                : fovCurve.Evaluate(fieldOfView);
            var fovFactor = Mathf.Abs(curveValue) > Mathf.Epsilon
                ? fieldOfView / curveValue
                : 1f;
            return new MvOutlineGlobals(
                new Vector4(widthMin * 0.01f, widthMax * 0.01f, 0f, 0f),
                new Vector4(
                    distanceNear,
                    1f / (distanceFar - distanceNear),
                    fovFactor,
                    0f));
        }

        private static MvRendererFeatureDescriptor Feature(string name, string typeName)
        {
            return new MvRendererFeatureDescriptor(name, typeName);
        }
    }

    public readonly struct MvOutlineGlobals
    {
        public MvOutlineGlobals(Vector4 width, Vector4 factor)
        {
            Width = width;
            Factor = factor;
        }

        public Vector4 Width { get; }

        public Vector4 Factor { get; }
    }

    public readonly struct MvRendererFeatureDescriptor
    {
        public MvRendererFeatureDescriptor(string name, string typeName)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        }

        public string Name { get; }

        public string TypeName { get; }
    }
}
