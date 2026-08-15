using Haruki.MV;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvPostEffectState))]
    [TrackClipType(typeof(SolarizationClip))]
    public sealed class SolarizationTrack : PostEffectTrackBase
    {
        protected override MvPostEffectKind Kind => MvPostEffectKind.Solarization;
    }
}
