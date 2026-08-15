using Haruki.MV;
using Sekai.Rendering.Components;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvPostEffectState))]
    public sealed class EnablePostEffectToCameraDecorationTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<EnablePostEffectToCameraDecorationMixer>.Create(graph, inputCount);
        }
    }

    public sealed class EnablePostEffectToCameraDecorationMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as MvPostEffectState;
            if (target == null) return;
            var enabled = false;
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                if (playable.GetInputWeight(index) > 0f)
                {
                    enabled = true;
                    break;
                }
            }
            target.EnablePostEffectToCameraDecoration = enabled;
            EnablePostEffectToCameraDecoration.EnablePostEffect = enabled;
        }
    }
}
