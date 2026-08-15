using Haruki.MV;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvPostEffectState))]
    [TrackClipType(typeof(LutClip))]
    public sealed class LutTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(
            PlayableGraph graph,
            GameObject go,
            int inputCount)
        {
            return ScriptPlayable<LutMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public sealed class LutMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as MvPostEffectState;
            if (target == null)
            {
                return;
            }
            LutBehaviour front = null;
            LutBehaviour back = null;
            var frontWeight = 0f;
            var backWeight = 0f;
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                var weight = playable.GetInputWeight(index);
                if (weight <= 0f)
                {
                    continue;
                }
                var input = (ScriptPlayable<LutBehaviour>)playable.GetInput(index);
                var normalized = input.GetDuration() > 0d
                    ? input.GetTime() / input.GetDuration()
                    : 0d;
                if (normalized < 0d || normalized > 1d)
                {
                    continue;
                }
                if (front == null)
                {
                    front = input.GetBehaviour();
                    frontWeight = weight;
                }
                else if (back == null)
                {
                    back = input.GetBehaviour();
                    backWeight = weight;
                }
            }
            Assign(target, front, frontWeight, true);
            Assign(target, back, backWeight, false);
        }

        private static void Assign(
            MvPostEffectState target,
            LutBehaviour value,
            float weight,
            bool front)
        {
            if (front)
            {
                target.LutFrontTexture = value?.texture3D;
                target.LutFrontBlend = value == null ? 0f : weight;
                target.LutFrontIsWhole = value != null && value.isWholeLut;
                target.LutFrontPosition = value?.lutPosition ?? Vector2.zero;
                target.LutFrontNonLutPosition = value?.nonLutPosition ?? Vector2.zero;
            }
            else
            {
                target.LutBackTexture = value?.texture3D;
                target.LutBackBlend = value == null ? 0f : weight;
                target.LutBackIsWhole = value != null && value.isWholeLut;
                target.LutBackPosition = value?.lutPosition ?? Vector2.zero;
                target.LutBackNonLutPosition = value?.nonLutPosition ?? Vector2.zero;
            }
        }
    }
}
