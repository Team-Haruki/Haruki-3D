using Haruki.MV;
using Sekai.Timeline.Common;

namespace Sekai.Core.Live
{
    public sealed class SaturationBlurClip : PostEffectClipBase
    {
        public ReferenceFloatBlend sat = new ReferenceFloatBlend();
        public ReferenceFloatBlend satAlpha = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            if (paramType == 0) target.SaturationBlurSat = sat.CalcBlend(time);
            else if (paramType == 1) target.SaturationBlurAlpha = satAlpha.CalcBlend(time);
        }
    }
}
