using Haruki.MV;
using Sekai.Timeline.Common;

namespace Sekai.Core.Live
{
    public sealed class LegacyBloomClip : PostEffectClipBase
    {
        public ReferenceBool newBloomBlend = new ReferenceBool();
        public ReferenceFloatBlend intensity = new ReferenceFloatBlend();
        public ReferenceFloatBlend scatter = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            switch (paramType)
            {
                case 0: target.BloomUseBlend = newBloomBlend.param; break;
                case 1: target.BloomIntensity = intensity.CalcBlend(time); break;
                case 2: target.BloomScatter = scatter.CalcBlend(time); break;
            }
        }
    }
}
