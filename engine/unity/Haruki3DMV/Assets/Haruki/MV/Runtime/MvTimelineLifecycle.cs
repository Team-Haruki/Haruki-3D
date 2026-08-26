using UnityEngine;

namespace Haruki.MV
{
    public interface IMvRetryHandleTrack
    {
        void OnRetry();
    }

    public sealed class MvLiveEffectTimelineManager : MonoBehaviour
    {
        public bool IsCutIn { get; private set; }
        public int CutInOrder { get; private set; }
        public bool IsNowPlaying { get; private set; }
        public bool IsSwitchExecutable { get; private set; }
        public bool IsNowPlayingOnRetry { get; private set; }
        public bool IsSwitchExecutableOnRetry { get; private set; }

        public void Setup(bool isCutIn, int cutInOrder)
        {
            IsCutIn = isCutIn;
            CutInOrder = cutInOrder;
            IsNowPlaying = !isCutIn;
            IsSwitchExecutable = false;
            IsNowPlayingOnRetry = !isCutIn;
            IsSwitchExecutableOnRetry = false;
        }

        public void OnRetry()
        {
            IsNowPlaying = IsNowPlayingOnRetry;
            IsSwitchExecutable = IsSwitchExecutableOnRetry;
        }
    }
}
