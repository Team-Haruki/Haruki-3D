using UnityEngine;
using UnityEngine.Playables;

namespace Sekai.Core.Live
{
    public sealed class WaterEyeClip : PlayableAsset
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private string presetId;

        private WaterEyePreset _preset;

        public WaterEyePreset Preset => _preset ??
            (_preset = WaterEyePresetSettings.GetPreset(presetId));
        public string DisplayName => displayName;
        public string PresetId => presetId;

        public void SetPreset(string valueDisplayName, string valuePresetId)
        {
            displayName = valueDisplayName;
            presetId = valuePresetId;
            _preset = WaterEyePresetSettings.GetPreset(presetId);
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<WaterEyeBehaviour>.Create(graph);
            playable.GetBehaviour().Clip = this;
            return playable;
        }
    }

    public sealed class WaterEyeBehaviour : PlayableBehaviour
    {
        public WaterEyeClip Clip { get; internal set; }
    }
}
