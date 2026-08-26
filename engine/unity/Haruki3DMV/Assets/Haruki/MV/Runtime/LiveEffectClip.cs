using System;
using UnityEngine;
using UnityEngine.Playables;

public sealed class LiveEffectClip : PlayableAsset
{
    public LiveEffectBehaviour template = new LiveEffectBehaviour();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<LiveEffectBehaviour>.Create(graph, template);
    }
}

[Serializable]
public sealed class LiveEffectBehaviour : PlayableBehaviour
{
    public string parentNodeName;
    public GameObject prefab;
    public AnimationClip clip;
    public GameObject trackBinding;

    private GameObject _instance;
    private ParticleSystem[] _particles = Array.Empty<ParticleSystem>();
    private Renderer[] _renderers = Array.Empty<Renderer>();
    private Animator _animator;
    private double _previousTime = -1d;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var binding = playerData as GameObject ?? (playerData as Component)?.gameObject;
        if (binding == null || prefab == null)
        {
            return;
        }
        EnsureInstance(binding);
        var time = Math.Max(0d, playable.GetTime());
        var seek = _previousTime < 0d || time < _previousTime || time - _previousTime > 0.25d;
        foreach (var particle in _particles)
        {
            if (particle == null)
            {
                continue;
            }
            if (seek)
            {
                particle.Simulate((float)time, true, true, true);
            }
            else
            {
                particle.Simulate((float)(time - _previousTime), true, false, true);
            }
        }
        if (_animator != null && clip != null && clip.length > 0f)
        {
            _animator.Play(clip.name, 0, Mathf.Repeat((float)time / clip.length, 1f));
            _animator.Update(0f);
        }
        SetVisible(true);
        _previousTime = time;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        SetVisible(false);
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (_instance != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(_instance);
            else UnityEngine.Object.DestroyImmediate(_instance);
        }
        _instance = null;
        _particles = Array.Empty<ParticleSystem>();
        _renderers = Array.Empty<Renderer>();
        _animator = null;
        _previousTime = -1d;
    }

    public void OnRetry()
    {
        foreach (var particle in _particles)
        {
            particle?.Simulate(0f, true, true, true);
        }
        _previousTime = -1d;
        SetVisible(false);
    }

    private void EnsureInstance(GameObject binding)
    {
        if (_instance != null)
        {
            return;
        }
        trackBinding = binding;
        var parent = FindParent(binding.transform.root, parentNodeName) ?? binding.transform;
        _instance = UnityEngine.Object.Instantiate(prefab, parent, false);
        _particles = _instance.GetComponentsInChildren<ParticleSystem>(true);
        _renderers = _instance.GetComponentsInChildren<Renderer>(true);
        _animator = _instance.GetComponentInChildren<Animator>(true);
    }

    private void SetVisible(bool visible)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private static Transform FindParent(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        if (root.name == name)
        {
            return root;
        }
        for (var index = 0; index < root.childCount; index++)
        {
            var found = FindParent(root.GetChild(index), name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
