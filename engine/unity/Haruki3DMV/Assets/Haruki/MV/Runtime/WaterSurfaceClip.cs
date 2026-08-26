using UnityEngine;
using UnityEngine.Playables;
using Sekai.Timeline.Common;

namespace Sekai.Core.Live
{
    public sealed class WaterSurfaceClip : PlayableAsset
    {
        [SerializeField] private ReferenceFloatBlend _smoothness = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _distortionPower = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _distortionDistance = new ReferenceFloatBlend();
        [SerializeField] private ReferenceVector2Blend _waveMoveSpeed = new ReferenceVector2Blend();
        [SerializeField] private ReferenceVector2Blend _waveNoiseSize = new ReferenceVector2Blend();
        [SerializeField] private ReferenceVector2Blend _normalNoiseSize = new ReferenceVector2Blend();
        [SerializeField] private ReferenceFloatBlend _normalPower = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _brightness = new ReferenceFloatBlend();
        [SerializeField] private ReferenceVector3Blend _lightDir = new ReferenceVector3Blend();
        [SerializeField] private ReferenceBoolParam _useSecondaryNormal = new ReferenceBoolParam();
        [SerializeField] private ReferenceBoolParam _usePolarUV = new ReferenceBoolParam();
        [SerializeField] private ReferenceVector2Blend _polarCenterPos = new ReferenceVector2Blend();
        [SerializeField] private ReferenceFloatBlend _radialScale = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _radialSpreadSpeed = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _rotationSpeed = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _polarCenterMaskSize = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend _polarCenterMaskFade = new ReferenceFloatBlend();

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<WaterSurfaceBehaviour>.Create(graph);
            playable.GetBehaviour().Clip = this;
            return playable;
        }

        internal void Apply(Sekai.Rendering.WaterSurfaceController target, double time)
        {
            target.UpdateSmoothnessParameters(_smoothness.CalcBlend(time));
            target.UpdateDistortionParameters(
                _distortionPower.CalcBlend(time),
                _distortionDistance.CalcBlend(time),
                _waveMoveSpeed.CalcBlend(time));
            target.UpdateWaveParameters(
                _waveNoiseSize.CalcBlend(time),
                _normalNoiseSize.CalcBlend(time),
                _normalPower.CalcBlend(time),
                _brightness.CalcBlend(time),
                _lightDir.CalcBlend(time));
            target.UpdateSecondaryNormalParameters(_useSecondaryNormal.param);
            target.UpdatePolarWaveParameters(
                _usePolarUV.param,
                _polarCenterPos.CalcBlend(time),
                _radialScale.CalcBlend(time),
                _radialSpreadSpeed.CalcBlend(time),
                _rotationSpeed.CalcBlend(time),
                _polarCenterMaskSize.CalcBlend(time),
                _polarCenterMaskFade.CalcBlend(time));
            target.UpdateTimerParameter(time);
        }
    }

    public sealed class WaterSurfaceBehaviour : PlayableBehaviour
    {
        public WaterSurfaceClip Clip { get; internal set; }
    }
}
