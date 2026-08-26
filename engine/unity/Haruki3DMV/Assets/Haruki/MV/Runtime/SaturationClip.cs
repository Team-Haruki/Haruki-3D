using Haruki.MV;
using Sekai.Timeline.Common;

namespace Sekai.Core.Live
{
    public sealed class SaturationClip : PostEffectClipBase
    {
        public ReferenceFloatBlend saturation = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            target.Saturation = saturation.CalcBlend(time);
        }
    }
}
