using Haruki.MV;
using Sekai.Timeline.Common;
using UnityEngine;

namespace Sekai.Core.Live
{
    public sealed class VignetteClip : PostEffectClipBase
    {
        public ReferenceColorBlend color = new ReferenceColorBlend();
        public ReferenceFloatBlend centerX = new ReferenceFloatBlend();
        public ReferenceFloatBlend centerY = new ReferenceFloatBlend();
        public ReferenceFloatBlend intensity = new ReferenceFloatBlend();
        public ReferenceFloatBlend smoothness = new ReferenceFloatBlend();
        public ReferenceFloatBlend roundness = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            target.VignetteColor = color.CalcBlend(time);
            target.VignetteCenter = new Vector2(
                centerX.CalcBlend(time),
                centerY.CalcBlend(time));
            target.VignetteIntensity = intensity.CalcBlend(time);
            target.VignetteSmoothness = smoothness.CalcBlend(time);
            target.VignetteRoundness = roundness.CalcBlend(time);
        }
    }
}
