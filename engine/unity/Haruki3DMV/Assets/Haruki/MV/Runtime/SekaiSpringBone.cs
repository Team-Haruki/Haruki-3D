using System;

namespace Sekai
{
    public sealed class SekaiSpringBone : UTJ.SpringBone
    {
        [Flags]
        public enum ColliderFlag
        {
            Hip = 1,
            Chest = 2,
            L_Arm = 4,
            R_Arm = 8,
            L_Elbow = 16,
            R_Elbow = 32,
        }

        public ColliderFlag colliderFlag;
    }
}
