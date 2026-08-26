using Sekai.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackColor(0.8f, 0.8f, 0.2f)]
    [TrackClipType(typeof(MusicItemUvScrollClip))]
    [TrackBindingType(typeof(MusicItemModel))]
    public sealed class MusicItemUvScrollTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(
            PlayableGraph graph,
            GameObject go,
            int inputCount)
        {
            return ScriptPlayable<MusicItemUvScrollMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public sealed class MusicItemUvScrollMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var model = playerData as MusicItemModel;
            if (model == null) return;

            var totalWeight = 0f;
            var scale = Vector2.zero;
            var offset = Vector2.zero;
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                var weight = playable.GetInputWeight(index);
                if (weight <= 0f) continue;
                var input = (ScriptPlayable<MusicItemUvScrollBehaviour>)playable.GetInput(index);
                var behaviour = input.GetBehaviour();
                scale += behaviour.UvScale * weight;
                offset += behaviour.UvOffset * weight;
                totalWeight += weight;
            }
            if (totalWeight > 0f)
                model.SetUVScaleAndOffset(scale / totalWeight, offset / totalWeight);
        }
    }
}
