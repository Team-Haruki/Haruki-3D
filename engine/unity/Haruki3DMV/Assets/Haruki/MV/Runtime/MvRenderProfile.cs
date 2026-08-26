using System;
using UnityEngine;

namespace Haruki.MV
{
    public enum MvQualityType
    {
        Default = 0,
        High = 1,
        VirtualLiveDefault = 2,
    }

    public enum MvLivePlayMode
    {
        Ingame3DMV = 0,
        MusicVideo = 4,
    }

    public enum MvOutputResolution
    {
        Device = 0,
        Hd720p = 1,
        FullHd1080p = 2,
        Qhd1440p = 3,
        Uhd4K = 4,
        Custom = 5,
    }

    public readonly struct MvRenderProfile
    {
        private const int PostEffectHeight = 256;

        private MvRenderProfile(
            Vector2Int renderSize,
            Vector2Int postEffectSize,
            int targetFrameRate)
        {
            RenderSize = renderSize;
            PostEffectSize = postEffectSize;
            TargetFrameRate = targetFrameRate;
        }

        public Vector2Int RenderSize { get; }
        public Vector2Int PostEffectSize { get; }
        public int TargetFrameRate { get; }

        public static MvRenderProfile Calculate(
            int width,
            int height,
            float dpi,
            MvQualityType quality,
            MvLivePlayMode playMode,
            int refreshRate,
            bool use120Fps)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (!(dpi > 0) || float.IsInfinity(dpi))
            {
                throw new ArgumentOutOfRangeException(nameof(dpi));
            }
            if (refreshRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(refreshRate));
            }

            var renderSize = playMode == MvLivePlayMode.MusicVideo
                ? Scale(width, height, quality == MvQualityType.High
                    ? UncappedHighScale(500f / dpi, 640, height)
                    : StandardDpiScale(350f, dpi, 640, 1080, height))
                : Scale(width, height, quality == MvQualityType.High
                    ? UncappedHighScale(0.8f, 560, height)
                    : Clamp(
                        0.6d,
                        Math.Min(560d / height, 1d),
                        Math.Min(1080d / height, 1d)));

            return Create(renderSize, refreshRate, use120Fps);
        }

        public static MvRenderProfile ForVideoOutput(
            MvOutputResolution resolution,
            int customWidth,
            int customHeight,
            int refreshRate,
            bool use120Fps)
        {
            Vector2Int renderSize;
            switch (resolution)
            {
                case MvOutputResolution.Hd720p:
                    renderSize = new Vector2Int(1280, 720);
                    break;
                case MvOutputResolution.FullHd1080p:
                    renderSize = new Vector2Int(1920, 1080);
                    break;
                case MvOutputResolution.Qhd1440p:
                    renderSize = new Vector2Int(2560, 1440);
                    break;
                case MvOutputResolution.Uhd4K:
                    renderSize = new Vector2Int(3840, 2160);
                    break;
                case MvOutputResolution.Custom:
                    if (customWidth <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(customWidth));
                    }
                    if (customHeight <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(customHeight));
                    }
                    renderSize = new Vector2Int(customWidth, customHeight);
                    break;
                case MvOutputResolution.Device:
                    throw new ArgumentException(
                        "Device output requires the recovered DPI-aware calculation.",
                        nameof(resolution));
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution));
            }

            return Create(renderSize, refreshRate, use120Fps);
        }

        private static MvRenderProfile Create(
            Vector2Int renderSize,
            int refreshRate,
            bool use120Fps)
        {
            if (refreshRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(refreshRate));
            }
            return new MvRenderProfile(
                renderSize,
                new Vector2Int(
                    Mathf.RoundToInt((float)renderSize.x / renderSize.y * PostEffectHeight),
                    PostEffectHeight),
                FeasibleFrameRate(refreshRate, use120Fps ? 120 : 60));
        }

        private static double StandardDpiScale(
            double targetDpi,
            double dpi,
            int minHeight,
            int maxHeight,
            int screenHeight)
        {
            return Clamp(
                targetDpi / dpi,
                Math.Min((double)minHeight / screenHeight, 1d),
                Math.Min((double)maxHeight / screenHeight, 1d));
        }

        private static double UncappedHighScale(
            double requestedScale,
            int minHeight,
            int screenHeight)
        {
            return Clamp(
                requestedScale,
                Math.Min((double)minHeight / screenHeight, 1d),
                1d);
        }

        private static Vector2Int Scale(int width, int height, double scale)
        {
            return new Vector2Int((int)(width * scale), (int)(height * scale));
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static int FeasibleFrameRate(int refreshRate, int requestedFrameRate)
        {
            if (refreshRate % requestedFrameRate == 0)
            {
                return requestedFrameRate;
            }

            var feasible = refreshRate;
            while (feasible > 120)
            {
                feasible /= 2;
            }
            return feasible;
        }
    }
}
