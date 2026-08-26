using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    public sealed class MusicItemUvScrollBehaviour : PlayableBehaviour
    {
        public Vector2 UvScale;
        public Vector2 UvOffset;
    }

    public sealed class MusicItemUvScrollClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField]
        private Vector2 uvScale = Vector2.one;

        [SerializeField]
        private Vector2 uvOffset;

        public ClipCaps clipCaps => ClipCaps.Blending;
        public Vector2 UvScale => uvScale;
        public Vector2 UvOffset => uvOffset;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<MusicItemUvScrollBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.UvScale = uvScale;
            behaviour.UvOffset = uvOffset;
            return playable;
        }
    }
}
