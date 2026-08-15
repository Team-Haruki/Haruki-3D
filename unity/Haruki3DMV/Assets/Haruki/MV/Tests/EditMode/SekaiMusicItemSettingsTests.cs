using NUnit.Framework;
using Sekai.Rendering;

namespace Haruki.MV.Tests
{
    public sealed class SekaiMusicItemSettingsTests
    {
        private sealed class MusicItem : ISekaiMusicItem
        {
            public bool IsHiding { get; set; }

            public bool IsOpaque { get; set; }
        }

        [SetUp]
        public void SetUp()
        {
            SekaiMusicItemSettings.ClearTransparentMusicItem();
        }

        [TearDown]
        public void TearDown()
        {
            SekaiMusicItemSettings.ClearTransparentMusicItem();
        }

        [Test]
        public void RegistryOnlyKeepsVisibleTransparentItemsAndDeduplicatesThem()
        {
            var visibleTransparent = new MusicItem();
            SekaiMusicItemSettings.RegisterTransparentMusicItem(
                new MusicItem { IsHiding = true });
            SekaiMusicItemSettings.RegisterTransparentMusicItem(
                new MusicItem { IsOpaque = true });
            Assert.That(SekaiMusicItemSettings.ExistTransparentMusicItem(), Is.False);

            SekaiMusicItemSettings.RegisterTransparentMusicItem(visibleTransparent);
            SekaiMusicItemSettings.RegisterTransparentMusicItem(visibleTransparent);
            Assert.That(SekaiMusicItemSettings.ExistTransparentMusicItem(), Is.True);

            SekaiMusicItemSettings.UnregisterTransparentMusicItem(visibleTransparent);
            Assert.That(SekaiMusicItemSettings.ExistTransparentMusicItem(), Is.False);
        }

    }
}
