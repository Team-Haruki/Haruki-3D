using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    public enum MeshFlareParaOrder
    {
        Order_1 = 0,
        Order_2 = 1,
        Order_3 = 2,
    }

    [TrackBindingType(typeof(MeshFlareParaController))]
    [TrackClipType(typeof(MeshFlareParaClip))]
    public sealed class MeshFlareParaTrack : TrackAsset
    {
        [SerializeField]
        private MeshFlareParaOrder _meshFlareParaOrder;

        public MeshFlareParaOrder MeshFlareParaTrackOrder => _meshFlareParaOrder;

        public override Playable CreateTrackMixer(
            PlayableGraph graph,
            GameObject go,
            int inputCount)
        {
            var playable = ScriptPlayable<MeshFlareParaMixerBehaviour>.Create(
                graph,
                inputCount);
            playable.GetBehaviour().Order = (int)_meshFlareParaOrder;
            return playable;
        }
    }

    public sealed class MeshFlareParaMixerBehaviour : PlayableBehaviour
    {
        public int Order { get; internal set; }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as MeshFlareParaController;
            if (target == null)
            {
                return;
            }
            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                if (playable.GetInputWeight(index) <= 0f)
                {
                    continue;
                }
                var input = (ScriptPlayable<MeshFlareParaBehaviour>)playable.GetInput(index);
                var clip = input.GetBehaviour().Clip;
                if (clip != null)
                {
                    clip.Apply(target, Order, playable.GetTime());
                    return;
                }
            }
            target.SetActiveObj(Order, false);
        }
    }
}
