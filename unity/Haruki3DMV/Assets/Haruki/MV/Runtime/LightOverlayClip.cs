using Haruki.MV;
using Sekai.Timeline.Common;

namespace Sekai.Core.Live
{
    public sealed class LightOverlayClip : PostEffectClipBase
    {
        public ReferenceColorBlend brightColor = new ReferenceColorBlend();
        public ReferenceColorBlend darkColor = new ReferenceColorBlend();
        public ReferenceVector2Blend brightPosition = new ReferenceVector2Blend();
        public ReferenceVector2Blend darkPosition = new ReferenceVector2Blend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            switch (paramType)
            {
                case 0: target.LightOverlayBrightColor = brightColor.CalcBlend(time); break;
                case 1: target.LightOverlayDarkColor = darkColor.CalcBlend(time); break;
                case 2: target.LightOverlayBrightPosition = brightPosition.CalcBlend(time); break;
                case 3: target.LightOverlayDarkPosition = darkPosition.CalcBlend(time); break;
            }
        }
    }
}
