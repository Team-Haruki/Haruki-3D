using Haruki.MV;
using Sekai.Timeline.Common;
using UnityEngine;

namespace Sekai.Core.Live
{
    public sealed class DirectionalBlurClip : PostEffectClipBase
    {
        public ReferenceFloatBlend directionalBlurStrength = new ReferenceFloatBlend();
        public ReferenceFloatBlend directionalBlurDirection = new ReferenceFloatBlend();
        public ReferenceFloatBlend radialBlurStrength = new ReferenceFloatBlend();
        public ReferenceVector2Blend radialBlurCenterPosition = new ReferenceVector2Blend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            if (paramType == 0)
            {
                target.DirectionalBlurStrength = directionalBlurStrength.CalcBlend(time);
                target.DirectionalBlurDirection = directionalBlurDirection.CalcBlend(time);
            }
            else if (paramType == 1)
            {
                target.RadialBlurStrength = radialBlurStrength.CalcBlend(time);
                target.RadialBlurCenter = radialBlurCenterPosition.CalcBlend(time);
            }
        }
    }
}
