using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sekai.Core.Live
{
    public sealed class WaterEyePresetTable : ScriptableObject
    {
        [Serializable]
        public sealed class TableItem
        {
            public string PresetId;
            public string DisplayName;
            public WaterEyePreset Preset;
        }

        [SerializeField]
        private TableItem[] items = Array.Empty<TableItem>();

        public IReadOnlyList<TableItem> Items => items;

        public TableItem ElementAt(int index) => items[index];
    }

    public static class WaterEyePresetSettings
    {
        private static WaterEyePresetTable _presetTable;
        private static Dictionary<string, WaterEyePreset> _presets =
            new Dictionary<string, WaterEyePreset>(StringComparer.Ordinal);

        public static WaterEyePresetTable PresetTable => _presetTable;

        public static void Setup(WaterEyePresetTable presetTable)
        {
            _presetTable = presetTable != null
                ? presetTable
                : throw new ArgumentNullException(nameof(presetTable));
            _presets = presetTable.Items
                .Where(item => item != null &&
                    !string.IsNullOrWhiteSpace(item.PresetId) &&
                    item.Preset != null)
                .GroupBy(item => item.PresetId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Preset,
                    StringComparer.Ordinal);
        }

        public static WaterEyePreset GetPreset(string presetId)
        {
            return !string.IsNullOrWhiteSpace(presetId) &&
                _presets.TryGetValue(presetId, out var preset)
                ? preset
                : null;
        }
    }
}
