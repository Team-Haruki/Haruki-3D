using Sekai.Rendering;
using Sekai.Timeline.Common;
using UnityEngine;
using UnityEngine.Playables;

namespace Sekai.Core.Live
{
    public sealed class HeightFogClip : PlayableAsset
    {
        [SerializeField] private ReferenceFloatBlend fogIntensity = new ReferenceFloatBlend();
        [SerializeField] private ReferenceEnumParam<HeightFogEnums.FogAxisMode> axisMode = new ReferenceEnumParam<HeightFogEnums.FogAxisMode>(HeightFogEnums.FogAxisMode.YAxis);
        [SerializeField] private ReferenceEnumParam<HeightFogEnums.FogLayersMode> fogMode = new ReferenceEnumParam<HeightFogEnums.FogLayersMode>(HeightFogEnums.FogLayersMode.MultiplyDistanceAndHeight);
        [SerializeField] private ReferenceColorBlend distanceColorStart = new ReferenceColorBlend();
        [SerializeField] private ReferenceColorBlend distanceColorEnd = new ReferenceColorBlend();
        [SerializeField] private ReferenceFloatBlend distanceColorDuo = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend distanceStart = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend distanceEnd = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend lightIntensity = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend heightStart = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend heightEnd = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend farDistanceHeight = new ReferenceFloatBlend();
        [SerializeField] private ReferenceFloatBlend farDistanceOffset = new ReferenceFloatBlend();

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<HeightFogBehaviour>.Create(graph);
            playable.GetBehaviour().Clip = this;
            return playable;
        }

        internal void Apply(IHeightFogController target, double time)
        {
            target.UpdateIntensity(fogIntensity.CalcBlend(time));
            target.UpdateAxis(axisMode.param);
            target.UpdateMultiplyDistanceAndHeight(fogMode.param);
            target.UpdateDistanceParams(
                distanceColorStart.CalcBlend(time),
                distanceColorEnd.CalcBlend(time),
                distanceColorDuo.CalcBlend(time),
                distanceStart.CalcBlend(time),
                distanceEnd.CalcBlend(time));
            target.UpdateLightParams(lightIntensity.CalcBlend(time));
            target.UpdateHeightParams(
                heightStart.CalcBlend(time),
                heightEnd.CalcBlend(time),
                farDistanceHeight.CalcBlend(time),
                farDistanceOffset.CalcBlend(time));
            target.UpdateTime((float)time);
        }
    }

    public sealed class HeightFogBehaviour : PlayableBehaviour
    {
        public HeightFogClip Clip { get; internal set; }
    }
}
