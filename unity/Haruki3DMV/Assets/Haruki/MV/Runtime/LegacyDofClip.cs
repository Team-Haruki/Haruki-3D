using Haruki.MV;
using Sekai.Timeline.Common;
using UnityEngine;

namespace Sekai.Core.Live
{
    public sealed class LegacyDofClip : PostEffectClipBase
    {
        [SerializeField] private ReferenceFloatBlend transitionRange = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend focalRegion = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            if (paramType == 0) target.LegacyDofTransitionRange = transitionRange.CalcBlend(time);
            else target.LegacyDofFocalRegion = focalRegion.CalcBlend(time);
        }
    }
}
