using Haruki.MV;
using Sekai.Timeline.Common;
using UnityEngine;

namespace Sekai.Core.Live
{
    public sealed class IncidentLightClip : PostEffectClipBase
    {
        public enum IncidentLight { Flare = 0, Para = 1 }
        [SerializeField] private ReferenceEnumParam<IncidentLight> incidentLightType = new ReferenceEnumParam<IncidentLight>(IncidentLight.Flare);
        [SerializeField] private ReferenceColorBlend incidentLightColor = new ReferenceColorBlend();
        [SerializeField] private ReferenceVector2Blend incidentLightPosition = new ReferenceVector2Blend();
        [SerializeField] private ReferenceFloatBlend incidentLightLength = new ReferenceFloatBlend();

        public override void Apply(MvPostEffectState target, int paramType, double time)
        {
            target.IncidentLights[paramType] = new MvIncidentLightState
            {
                Type = (int)incidentLightType.param,
                Color = incidentLightColor.CalcBlend(time),
                Position = incidentLightPosition.CalcBlend(time),
                Length = incidentLightLength.CalcBlend(time),
            };
        }
    }
}
