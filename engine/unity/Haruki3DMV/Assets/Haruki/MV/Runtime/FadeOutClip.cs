using Haruki.MV;
using Sekai.Timeline.Common;

namespace Sekai.Core.Live
{
    public sealed class FadeOutClip : PostEffectClipBase
    {
        public ReferenceFloatBlend fadeOut = new ReferenceFloatBlend();
        public ReferenceFloatBlend fadeOutLerp = new ReferenceFloatBlend();
        public ReferenceFloatBlend fadeOutBeforeProp = new ReferenceFloatBlend();
        public ReferenceFloatBlend fadeOutBeforePropLerp = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            switch (paramType)
            {
                case 0: target.FadeOut = fadeOut.CalcBlend(time); break;
                case 1: target.FadeOutLerp = fadeOutLerp.CalcBlend(time); break;
                case 2: target.FadeOutBeforeProp = fadeOutBeforeProp.CalcBlend(time); break;
                case 3: target.FadeOutBeforePropLerp = fadeOutBeforePropLerp.CalcBlend(time); break;
            }
        }
    }
}
