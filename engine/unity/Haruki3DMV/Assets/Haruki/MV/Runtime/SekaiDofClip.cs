using Haruki.MV;
using Sekai.Timeline.Common;

namespace Sekai.Core.Live
{
    public sealed class SekaiDofClip : PostEffectClipBase
    {
        public ReferenceFloatBlend aperture = new ReferenceFloatBlend();
        public ReferenceFloatBlend focalLength = new ReferenceFloatBlend();
        public ReferenceBool disableForeBokeh = new ReferenceBool();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            switch (paramType)
            {
                case 0: target.DofAperture = aperture.CalcBlend(time); break;
                case 1: target.DofFocalLength = focalLength.CalcBlend(time); break;
                case 2: target.DofDisableForeBokeh = disableForeBokeh.param; break;
            }
        }
    }
}
