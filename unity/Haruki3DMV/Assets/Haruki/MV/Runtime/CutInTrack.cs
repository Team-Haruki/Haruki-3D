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
        private CutInClip _activeClip;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var controller = playerData as MvCutInController;
            if (controller == null)
            {
                return;
            }

            CutInClip active = null;
            ScriptPlayable<CutInBehaviour> activePlayable = default;
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                if (playable.GetInputWeight(index) <= 0f)
                {
                    continue;
                }
                var input = (ScriptPlayable<CutInBehaviour>)playable.GetInput(index);
                if (input.GetBehaviour().Clip != null)
                {
                    active = input.GetBehaviour().Clip;
                    activePlayable = input;
                    break;
                }
            }

            if (!ReferenceEquals(active, _activeClip))
            {
                if (_activeClip != null)
                {
                    controller.End(_activeClip);
                }
                _activeClip = active;
                if (_activeClip != null)
                {
                    controller.Begin(_activeClip);
                }
            }

            if (active != null)
            {
                controller.UpdateTransition(
                    active,
                    (float)activePlayable.GetTime(),
                    (float)activePlayable.GetDuration());
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            _activeClip = null;
        }
    }
}
