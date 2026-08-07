namespace Haruki.MV
{
    public interface IMvPlaybackParticipant
    {
        void SetPaused(bool paused);
        void Seek(double timeSeconds);
        void DisposePlayback();
    }
}
