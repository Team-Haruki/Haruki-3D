using Haruki.MV;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackBindingType(typeof(GameObject))]
[TrackClipType(typeof(LiveEffectClip))]
public sealed class LiveEffectTrack : TrackAsset, IMvRetryHandleTrack
{
    private LiveEffectMixerBehaviour _mixer;

    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject go,
        int inputCount)
    {
        var playable = ScriptPlayable<LiveEffectMixerBehaviour>.Create(graph, inputCount);
        _mixer = playable.GetBehaviour();
        return playable;
    }

    public void OnRetry()
    {
        _mixer?.OnRetry();
    }
}

public sealed class LiveEffectMixerBehaviour : PlayableBehaviour
{
    private Playable _playable;

    public override void OnGraphStart(Playable playable)
    {
        _playable = playable;
    }

    public override void OnGraphStop(Playable playable)
    {
        _playable = Playable.Null;
    }

    public void OnRetry()
    {
        if (!_playable.IsValid())
        {
            return;
        }
        for (var index = 0; index < _playable.GetInputCount(); index++)
        {
            var input = (ScriptPlayable<LiveEffectBehaviour>)_playable.GetInput(index);
            input.GetBehaviour().OnRetry();
        }
    }
}
