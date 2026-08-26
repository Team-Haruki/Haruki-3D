using Haruki.MV;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvWaterEyeState))]
    [TrackClipType(typeof(WaterEyeClip))]
    public sealed class WaterEyeTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(
            PlayableGraph graph,
            GameObject go,
            int inputCount)
        {
            return ScriptPlayable<WaterEyeMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public sealed class WaterEyeMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var state = playerData as MvWaterEyeState;
            if (state == null)
            {
                return;
            }
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                if (playable.GetInputWeight(index) <= 0f)
                {
                    continue;
                }
                var input = (ScriptPlayable<WaterEyeBehaviour>)playable.GetInput(index);
                var clip = input.GetBehaviour().Clip;
                if (clip != null)
                {
                    state.Enable(clip.PresetId, clip.DisplayName);
                    return;
                }
            }
            state.Disable();
        }
    }
}
