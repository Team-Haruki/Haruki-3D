using Sekai.Rendering;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(WaterSurfaceController))]
    [TrackClipType(typeof(WaterSurfaceClip))]
    public sealed class WaterSurfaceTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<WaterSurfaceMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public sealed class WaterSurfaceMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as WaterSurfaceController;
            if (target == null) return;
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                if (playable.GetInputWeight(index) <= 0f) continue;
                var input = (ScriptPlayable<WaterSurfaceBehaviour>)playable.GetInput(index);
                input.GetBehaviour().Clip?.Apply(target, playable.GetTime());
                return;
            }
        }
    }
}
