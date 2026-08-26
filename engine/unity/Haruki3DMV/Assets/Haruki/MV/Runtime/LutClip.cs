using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Sekai.Core.Live
{
    public sealed class LutClip : PlayableAsset
    {
        public LutBehaviour template = new LutBehaviour();

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<LutBehaviour>.Create(graph, template);
        }
    }

    [Serializable]
    public sealed class LutBehaviour : PlayableBehaviour
    {
        public Texture3D texture3D;
        public bool isWholeLut;
        public Vector2 lutPosition;
        public Vector2 nonLutPosition;
    }
}
