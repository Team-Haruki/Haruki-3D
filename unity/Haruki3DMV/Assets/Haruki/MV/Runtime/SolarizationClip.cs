using Haruki.MV;
using Sekai.Timeline.Common;
using UnityEngine;

namespace Sekai.Core.Live
{
    public sealed class SolarizationClip : PostEffectClipBase
    {
        [SerializeField] private ReferenceFloatBlend solarization = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            target.Solarization = solarization.CalcBlend(time);
        }
    }
}
