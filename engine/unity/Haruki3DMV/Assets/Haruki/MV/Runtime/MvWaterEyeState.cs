using Sekai.Core.Live;
using Sekai.Scripts.Live.Character;
using UnityEngine;

namespace Haruki.MV
{
    public sealed class MvWaterEyeState : MonoBehaviour
    {
        public bool IsEnabled { get; private set; }
        public string PresetId { get; private set; }
        public string DisplayName { get; private set; }
        public WaterEyePreset Preset { get; private set; }

        private CharacterEyeMaterialController _materialController;

        public void Setup(CharacterEyeMaterialController materialController)
        {
            _materialController = materialController;
        }

        public void Enable(string presetId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                Disable();
                return;
            }
            IsEnabled = true;
            PresetId = presetId;
            DisplayName = displayName;
            Preset = WaterEyePresetSettings.GetPreset(presetId);
            if (Preset == null)
            {
                Disable();
                return;
            }
            _materialController?.ApplyBaseEyePreset(Preset.BaseEyeMaterial);
            _materialController?.ApplyHighlightEyePreset(Preset.HighlightEyeMaterial);
            _materialController?.Enable();
        }

        public void Disable()
        {
            IsEnabled = false;
            PresetId = null;
            DisplayName = null;
            Preset = null;
            _materialController?.Disable();
        }
    }
}
