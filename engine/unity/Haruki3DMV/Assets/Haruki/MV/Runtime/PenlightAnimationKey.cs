using UnityEngine;

namespace Sekai.Live
{
    public sealed class PenlightAnimationKey : PenlightKey
    {
        [Range(-1f, 1f)] public float armPitch;
        [Range(-1f, 1f)] public float armRoll;
        [Range(-1f, 1f)] public float handPitch;
        [Range(-1f, 1f)] public float handRoll;
        [Range(-1f, 1f)] public float yawOffset;
        public Vector3 elbowPosition;
        public Vector2 centerPosition;
        public Vector2 armRandomness;
        public Vector2 handRandomness;
        public float yawRandomness;
        [Range(0f, 1.5f)] public float armLength;
        [Range(0f, 0.5f)] public float handLength;
    }
}
