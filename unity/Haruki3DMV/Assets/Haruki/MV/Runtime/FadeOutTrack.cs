using Haruki.MV;
using UnityEngine.Timeline;

namespace Sekai.Core.Live
{
    [TrackBindingType(typeof(MvPostEffectState))]
    [TrackClipType(typeof(FadeOutClip))]
    public sealed class FadeOutTrack : PostEffectTrackBase
    {
        protected override MvPostEffectKind Kind => MvPostEffectKind.FadeOut;
    }
}
