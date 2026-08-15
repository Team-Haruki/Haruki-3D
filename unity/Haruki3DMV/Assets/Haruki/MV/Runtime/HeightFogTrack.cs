using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(HeightFogController))]
    [TrackClipType(typeof(HeightFogClip))]
    public sealed class HeightFogTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<HeightFogMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public sealed class HeightFogMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as IHeightFogController;
            if (target == null) return;
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                if (playable.GetInputWeight(index) <= 0f) continue;
                var input = (ScriptPlayable<HeightFogBehaviour>)playable.GetInput(index);
                input.GetBehaviour().Clip?.Apply(target, playable.GetTime());
                return;
            }
        }
    }
}
