using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.LivePerformance
{
    [Serializable]
    public sealed class MusicItemOpacityBehaviour : PlayableBehaviour
    {
        [Serializable]
        public sealed class Data
        {
            public float opacity = 1f;
        }

        public Data data = new Data();
    }

    [Serializable]
    public sealed class MusicItemOpacityClip : PlayableAsset, ITimelineClipAsset
    {
        public MusicItemOpacityBehaviour template = new MusicItemOpacityBehaviour();

        public MusicItemOpacityBehaviour Behaviour { get; private set; }
        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<MusicItemOpacityBehaviour>.Create(graph, template);
            Behaviour = playable.GetBehaviour();
            return playable;
        }
    }
}
