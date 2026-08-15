using Haruki.MV;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvPostEffectState))]
    [TrackClipType(typeof(DirectionalBlurClip))]
    public sealed class DirectionalBlurTrack : PostEffectTrackBase
    {
        protected override MvPostEffectKind Kind => MvPostEffectKind.DirectionalBlur;
    }
}
