using Haruki.MV;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvCutInController))]
    [TrackClipType(typeof(CutInClip))]
    public sealed class CutInTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(
            PlayableGraph graph,
            GameObject go,
            int inputCount)
        {
            return ScriptPlayable<CutInMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public sealed class CutInMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var controller = playerData as MvCutInController;
            if (controller == null)
            {
                return;
            }
            controller.EvaluateCurrentFrame();
        }
    }
}
