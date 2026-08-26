using Haruki.MV;
using Sekai.Timeline.Common;
using UnityEngine;

namespace Sekai.Core.Live
{
    public sealed class ChromaticAberrationClip : PostEffectClipBase
    {
        [SerializeField] private ReferenceVector2Blend _offsetR = new ReferenceVector2Blend();
        [SerializeField] private ReferenceVector2Blend _offsetG = new ReferenceVector2Blend();
        [SerializeField] private ReferenceVector2Blend _offsetB = new ReferenceVector2Blend();
        [SerializeField] private ReferenceFloatBlend _scaleR = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _scaleG = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _scaleB = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            if (paramType == 0)
            {
                target.ChromaticOffsetR = _offsetR.CalcBlend(time) * 0.1f;
                target.ChromaticOffsetG = _offsetG.CalcBlend(time) * 0.1f;
                target.ChromaticOffsetB = _offsetB.CalcBlend(time) * 0.1f;
                target.ChromaticScale = Vector3.one;
            }
            else
            {
                target.ChromaticOffsetR = Vector2.zero;
                target.ChromaticOffsetG = Vector2.zero;
                target.ChromaticOffsetB = Vector2.zero;
                target.ChromaticScale = new Vector3(
                    _scaleR.CalcBlend(time), _scaleG.CalcBlend(time), _scaleB.CalcBlend(time));
            }
        }
    }
}
