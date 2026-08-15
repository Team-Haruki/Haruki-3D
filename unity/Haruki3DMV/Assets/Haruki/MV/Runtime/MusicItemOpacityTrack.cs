using Sekai.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.LivePerformance
{
    [TrackColor(0.8f, 0.8f, 0.2f)]
    [TrackClipType(typeof(MusicItemOpacityClip))]
    [TrackBindingType(typeof(MusicItemModel))]
    public sealed class MusicItemOpacityTrack : TrackAsset
    {
        [SerializeField]
        private bool _isInsertCharacter;

        public override Playable CreateTrackMixer(
            PlayableGraph graph,
            GameObject go,
            int inputCount)
        {
            return ScriptPlayable<MusicItemOpacityMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public sealed class MusicItemOpacityMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var model = playerData as MusicItemModel;
            if (model == null) return;

            var totalWeight = 0f;
            var opacity = 0f;
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                var weight = playable.GetInputWeight(index);
                if (weight <= 0f) continue;
                var input = (ScriptPlayable<MusicItemOpacityBehaviour>)playable.GetInput(index);
                opacity += input.GetBehaviour().data.opacity * weight;
                totalWeight += weight;
            }
            model.SetOpacity(totalWeight > 0f ? opacity / totalWeight : 0f);
        }
    }
}
