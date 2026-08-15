using UnityEngine;
using UnityEngine.Playables;

namespace Sekai.Core.Live
{
    public sealed class CutInClip : PlayableAsset
    {
        public int cutinIndex;
        public Color entryTransitionColor = Color.white;
        public float entryTransitionInDuration;
        public float entryTransitionOutDuration;
        public Color exitTransitionColor = Color.white;
        public float exitTransitionInDuration;
        public float exitTransitionOutDuration;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CutInBehaviour>.Create(graph);
            playable.GetBehaviour().Clip = this;
            return playable;
        }
    }

    public sealed class CutInBehaviour : PlayableBehaviour
    {
        public CutInClip Clip { get; internal set; }
    }
}
