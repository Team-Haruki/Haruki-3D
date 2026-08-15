using Haruki.MV;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvPostEffectState))]
    [TrackClipType(typeof(VignetteClip))]
    public sealed class VignetteTrack : PostEffectTrackBase
    {
        protected override MvPostEffectKind Kind => MvPostEffectKind.Vignette;
    }
}
