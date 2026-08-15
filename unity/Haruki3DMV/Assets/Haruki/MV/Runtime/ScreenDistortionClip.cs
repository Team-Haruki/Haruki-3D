using Haruki.MV;
using Sekai.Timeline.Common;
using UnityEngine;

namespace Sekai.Core.Live
{
    public sealed class ScreenDistortionClip : PostEffectClipBase
    {
        [SerializeField] private ReferenceFloatBlend intensity = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend scale = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend offset = new ReferenceFloatBlend();
        [SerializeField] private ReferenceBoolParam useNoiseTexture = new ReferenceBoolParam();
        [SerializeField] private Texture2D noiseTexture;
        [SerializeField] private ReferenceVector2Blend noiseTextureScale = new ReferenceVector2Blend();
        [SerializeField] private ReferenceVector2Blend noiseUVScrollSpeed = new ReferenceVector2Blend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            switch (paramType)
            {
                case 0:
                    target.ScreenDistortionIntensity = intensity.CalcBlend(time);
                    break;
                case 1:
                    target.ScreenDistortionScale = scale.CalcBlend(time);
                    break;
                case 2:
                    target.ScreenDistortionOffset = offset.CalcBlend(time);
                    break;
                case 3:
                    target.ScreenDistortionUseNoise = useNoiseTexture.param;
                    target.ScreenDistortionNoiseTexture = noiseTexture;
                    target.ScreenDistortionNoiseScale = noiseTextureScale.CalcBlend(time);
                    target.ScreenDistortionUvScrollSpeed = noiseUVScrollSpeed.CalcBlend(time);
                    target.ScreenDistortionTime = (float)time;
                    break;
            }
        }

    }
}
