using Haruki.MV;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvPostEffectState))]
    [TrackClipType(typeof(IncidentLightClip))]
    public sealed class IncidentLightTrack : PostEffectTrackBase
    {
        protected override MvPostEffectKind Kind => MvPostEffectKind.IncidentLight;
    }
}
