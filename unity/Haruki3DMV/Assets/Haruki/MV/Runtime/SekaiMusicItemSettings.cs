using System.Collections.Generic;

namespace Sekai.Rendering
{
    public interface ISekaiMusicItem
    {
        bool IsHiding { get; }

        bool IsOpaque { get; }
    }

    /// <summary>
    /// Tracks the visible transparent music items that require the dedicated
    /// draw-object pass. This mirrors the recovered static runtime registry.
    /// </summary>
    public static class SekaiMusicItemSettings
    {
        private static readonly List<ISekaiMusicItem> CurrentTransparentMusicItems =
            new List<ISekaiMusicItem>();

        public static void ClearTransparentMusicItem()
        {
            CurrentTransparentMusicItems.Clear();
        }

        public static void RegisterTransparentMusicItem(ISekaiMusicItem musicItem)
        {
            if (musicItem == null || musicItem.IsHiding || musicItem.IsOpaque)
            {
                return;
            }

            if (!CurrentTransparentMusicItems.Contains(musicItem))
            {
                CurrentTransparentMusicItems.Add(musicItem);
            }
        }

        public static void UnregisterTransparentMusicItem(ISekaiMusicItem musicItem)
        {
            if (musicItem != null)
            {
                CurrentTransparentMusicItems.Remove(musicItem);
            }
        }

        public static bool ExistTransparentMusicItem()
        {
            return CurrentTransparentMusicItems.Count > 0;
        }
    }
}
