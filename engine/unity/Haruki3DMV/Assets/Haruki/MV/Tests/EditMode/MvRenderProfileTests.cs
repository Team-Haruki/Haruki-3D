using NUnit.Framework;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvRenderProfileTests
    {
        [Test]
        public void HighMusicVideoUsesFullDevicePixelsAndAspectDerivedPostSurface()
        {
            var profile = MvRenderProfile.Calculate(
                3200,
                2136,
                440,
                MvQualityType.High,
                MvLivePlayMode.MusicVideo,
                120,
                true);

            Assert.That(profile.RenderSize, Is.EqualTo(new Vector2Int(3200, 2136)));
            Assert.That(profile.PostEffectSize, Is.EqualTo(new Vector2Int(384, 256)));
            Assert.That(profile.TargetFrameRate, Is.EqualTo(120));
        }

        [Test]
        public void LowAndIngameProfilesFollowTheRecoveredDeviceScaleBranches()
        {
            var lowMv = MvRenderProfile.Calculate(
                3200, 2136, 440, MvQualityType.Default,
                MvLivePlayMode.MusicVideo, 120, false);
            var highIngame = MvRenderProfile.Calculate(
                3200, 2136, 440, MvQualityType.High,
                MvLivePlayMode.Ingame3DMV, 120, false);

            Assert.That(lowMv.RenderSize, Is.EqualTo(new Vector2Int(1617, 1080)));
            Assert.That(highIngame.RenderSize, Is.EqualTo(new Vector2Int(2560, 1708)));
        }

        [Test]
        public void TargetFrameRateRespectsTheRecoveredRefreshFallback()
        {
            Assert.That(ProfileAt(120, false).TargetFrameRate, Is.EqualTo(60));
            Assert.That(ProfileAt(144, true).TargetFrameRate, Is.EqualTo(72));
            Assert.That(ProfileAt(90, true).TargetFrameRate, Is.EqualTo(90));
        }

        [TestCase(MvOutputResolution.Hd720p, 1280, 720)]
        [TestCase(MvOutputResolution.FullHd1080p, 1920, 1080)]
        [TestCase(MvOutputResolution.Qhd1440p, 2560, 1440)]
        [TestCase(MvOutputResolution.Uhd4K, 3840, 2160)]
        public void StandardVideoPresetsUseExactLandscapePixelDimensions(
            MvOutputResolution preset,
            int expectedWidth,
            int expectedHeight)
        {
            var profile = MvRenderProfile.ForVideoOutput(
                preset,
                0,
                0,
                120,
                false);

            Assert.That(
                profile.RenderSize,
                Is.EqualTo(new Vector2Int(expectedWidth, expectedHeight)));
            Assert.That(profile.PostEffectSize, Is.EqualTo(new Vector2Int(455, 256)));
            Assert.That(profile.TargetFrameRate, Is.EqualTo(60));
        }

        [Test]
        public void CustomVideoPresetUsesExplicitPixelsRatherThanDpiScaling()
        {
            var profile = MvRenderProfile.ForVideoOutput(
                MvOutputResolution.Custom,
                3200,
                1800,
                120,
                true);

            Assert.That(profile.RenderSize, Is.EqualTo(new Vector2Int(3200, 1800)));
            Assert.That(profile.PostEffectSize, Is.EqualTo(new Vector2Int(455, 256)));
            Assert.That(profile.TargetFrameRate, Is.EqualTo(120));
        }

        private static MvRenderProfile ProfileAt(int refreshRate, bool use120Fps)
        {
            return MvRenderProfile.Calculate(
                1920, 1080, 440, MvQualityType.High,
                MvLivePlayMode.MusicVideo, refreshRate, use120Fps);
        }
    }
}
