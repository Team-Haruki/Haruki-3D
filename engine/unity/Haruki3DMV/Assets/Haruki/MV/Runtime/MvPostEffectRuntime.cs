using System;
using System.Collections.Generic;
using Sekai.Timeline.Common;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haruki.MV
{
    public enum MvPostEffectKind
    {
        ChromaticAberration,
        DirectionalBlur,
        FadeOut,
        LegacyBloom,
        LegacyDof,
        IncidentLight,
        LightOverlay,
        Saturation,
        SaturationBlur,
        ScreenDistortion,
        SekaiDof,
        Solarization,
        Vignette,
    }

    public sealed class MvPostEffectState : MonoBehaviour
    {
        private readonly HashSet<(MvPostEffectKind, int)> _enabled =
            new HashSet<(MvPostEffectKind, int)>();

        public float DirectionalBlurStrength { get; internal set; }
        public Vector2 ChromaticOffsetR { get; internal set; }
        public Vector2 ChromaticOffsetG { get; internal set; }
        public Vector2 ChromaticOffsetB { get; internal set; }
        public Vector3 ChromaticScale { get; internal set; } = Vector3.one;
        public float DirectionalBlurDirection { get; internal set; }
        public float RadialBlurStrength { get; internal set; }
        public Vector2 RadialBlurCenter { get; internal set; }
        public float FadeOut { get; internal set; }
        public float FadeOutLerp { get; internal set; }
        public float FadeOutBeforeProp { get; internal set; }
        public float FadeOutBeforePropLerp { get; internal set; }
        public bool BloomUseBlend { get; internal set; }
        public float BloomIntensity { get; internal set; }
        public float BloomScatter { get; internal set; }
        public float LegacyDofTransitionRange { get; internal set; }
        public float LegacyDofFocalRegion { get; internal set; }
        public Color LightOverlayBrightColor { get; internal set; }
        public Color LightOverlayDarkColor { get; internal set; }
        public Vector2 LightOverlayBrightPosition { get; internal set; }
        public Vector2 LightOverlayDarkPosition { get; internal set; }
        public float Saturation { get; internal set; }
        public float SaturationBlurSat { get; internal set; }
        public float SaturationBlurAlpha { get; internal set; }
        public float ScreenDistortionIntensity { get; internal set; }
        public float ScreenDistortionScale { get; internal set; }
        public float ScreenDistortionOffset { get; internal set; }
        public bool ScreenDistortionUseNoise { get; internal set; }
        public Texture2D ScreenDistortionNoiseTexture { get; internal set; }
        public Vector2 ScreenDistortionNoiseScale { get; internal set; }
        public Vector2 ScreenDistortionUvScrollSpeed { get; internal set; }
        public float ScreenDistortionTime { get; internal set; }
        public float Solarization { get; internal set; }
        public bool EnablePostEffectToCameraDecoration { get; internal set; }
        public float DofAperture { get; internal set; }
        public float DofFocalLength { get; internal set; }
        public bool DofDisableForeBokeh { get; internal set; }
        public Color VignetteColor { get; internal set; }
        public Vector2 VignetteCenter { get; internal set; }
        public float VignetteIntensity { get; internal set; }
        public float VignetteSmoothness { get; internal set; }
        public float VignetteRoundness { get; internal set; }
        public Texture3D LutFrontTexture { get; internal set; }
        public Texture3D LutBackTexture { get; internal set; }
        public float LutFrontBlend { get; internal set; }
        public float LutBackBlend { get; internal set; }
        public bool LutFrontIsWhole { get; internal set; }
        public bool LutBackIsWhole { get; internal set; }
        public Vector2 LutFrontPosition { get; internal set; }
        public Vector2 LutBackPosition { get; internal set; }
        public Vector2 LutFrontNonLutPosition { get; internal set; }
        public Vector2 LutBackNonLutPosition { get; internal set; }
        public Dictionary<int, MvIncidentLightState> IncidentLights { get; } =
            new Dictionary<int, MvIncidentLightState>();

        public bool IsEnabled(MvPostEffectKind kind, int paramType)
        {
            return _enabled.Contains((kind, paramType));
        }

        internal void SetEnabled(MvPostEffectKind kind, int paramType, bool enabled)
        {
            if (enabled)
            {
                _enabled.Add((kind, paramType));
            }
            else
            {
                _enabled.Remove((kind, paramType));
            }
        }
    }

    public struct MvIncidentLightState
    {
        public int Type;
        public Color Color;
        public Vector2 Position;
        public float Length;
    }
}

namespace Sekai.Timeline.Common
{
    public enum InClipBlendType
    {
        Const = 0,
        Blend = 1,
    }

    public interface IReferenceBlendRuntime
    {
        void SetupRuntimeTimeStamp(double begin, double end);
    }

    [Serializable]
    public abstract class ReferenceBlendBase<T> : IReferenceBlendRuntime
    {
        public InClipBlendType blendType;
        public AnimationCurve blendCurve;
        public T beginValue;
        public T endValue;

        public double RuntimeBeginTimeStamp { get; private set; }
        public double RuntimeEndTimeStamp { get; private set; }
        public InClipBlendType BlendType => blendType;
        public AnimationCurve BlendCurve => blendCurve;
        public T BeginValue => beginValue;
        public T EndValue => endValue;

        public void SetupRuntimeTimeStamp(double begin, double end)
        {
            RuntimeBeginTimeStamp = begin;
            RuntimeEndTimeStamp = end;
        }

        public T CalcBlend(double timelineTimeStamp)
        {
            if (blendType == InClipBlendType.Const)
            {
                return beginValue;
            }

            var duration = RuntimeEndTimeStamp - RuntimeBeginTimeStamp;
            if (duration <= Mathf.Epsilon)
            {
                return beginValue;
            }

            var time = (float)((timelineTimeStamp - RuntimeBeginTimeStamp) / duration);
            if (blendCurve != null)
            {
                time = blendCurve.Evaluate(time);
            }
            return CalcLerp(beginValue, endValue, time);
        }

        // Compatibility helper for callers that already hold normalized time.
        // Official runtime paths use CalcBlend with the director timestamp.
        public T Evaluate(float normalizedTime)
        {
            var time = blendCurve != null
                ? blendCurve.Evaluate(normalizedTime)
                : normalizedTime;
            return CalcLerp(beginValue, endValue, time);
        }

        protected abstract T CalcLerp(T begin, T end, float time);
    }

    [Serializable]
    public sealed class ReferenceFloatBlend : ReferenceBlendBase<float>
    {
        protected override float CalcLerp(float begin, float end, float time)
        {
            return Mathf.Lerp(begin, end, time);
        }
    }

    [Serializable]
    public sealed class ReferenceVector2Blend : ReferenceBlendBase<Vector2>
    {
        protected override Vector2 CalcLerp(Vector2 begin, Vector2 end, float time)
        {
            return Vector2.Lerp(begin, end, time);
        }
    }

    [Serializable]
    public sealed class ReferenceColorBlend : ReferenceBlendBase<Color>
    {
        protected override Color CalcLerp(Color begin, Color end, float time)
        {
            return Color.Lerp(begin, end, time);
        }
    }

    [Serializable]
    public sealed class ReferenceVector3Blend : ReferenceBlendBase<Vector3>
    {
        protected override Vector3 CalcLerp(Vector3 begin, Vector3 end, float time)
        {
            return Vector3.Lerp(begin, end, time);
        }
    }

    [Serializable]
    public sealed class ReferenceBool
    {
        public bool param;
    }

    [Serializable]
    public sealed class ReferenceBoolParam
    {
        public bool param;

        public ReferenceBoolParam(bool value = false)
        {
            param = value;
        }
    }

    [Serializable]
    public sealed class ReferenceEnumParam<T> where T : struct, Enum
    {
        public T param;

        public ReferenceEnumParam(T value)
        {
            param = value;
        }
    }
}

namespace Sekai.Core.Live
{
    public interface IMvPostEffectClip
    {
        void Apply(Haruki.MV.MvPostEffectState target, int paramType, double timelineTimeStamp);
    }

    public sealed class MvPostEffectClipBehaviour : PlayableBehaviour
    {
        public IMvPostEffectClip Clip { get; internal set; }
    }

    public sealed class MvPostEffectMixerBehaviour : PlayableBehaviour
    {
        public Haruki.MV.MvPostEffectKind Kind { get; internal set; }
        public int ParamType { get; internal set; }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as Haruki.MV.MvPostEffectState;
            if (target == null)
            {
                return;
            }

            for (var index = 0; index < playable.GetInputCount(); index++)
            {
                if (playable.GetInputWeight(index) <= 0f)
                {
                    continue;
                }
                var input = (ScriptPlayable<MvPostEffectClipBehaviour>)playable.GetInput(index);
                var clip = input.GetBehaviour().Clip;
                if (clip == null)
                {
                    continue;
                }
                clip.Apply(target, ParamType, playable.GetTime());
                target.SetEnabled(Kind, ParamType, true);
                return;
            }

            target.SetEnabled(Kind, ParamType, false);
        }
    }

    public abstract class PostEffectTrackBase : TrackAsset
    {
        [SerializeField]
        protected int m_ParamType;

        protected abstract Haruki.MV.MvPostEffectKind Kind { get; }

        public override Playable CreateTrackMixer(
            PlayableGraph graph,
            GameObject go,
            int inputCount)
        {
            var playable = ScriptPlayable<MvPostEffectMixerBehaviour>.Create(
                graph,
                inputCount);
            var behaviour = playable.GetBehaviour();
            behaviour.Kind = Kind;
            behaviour.ParamType = m_ParamType;
            return playable;
        }
    }

    public abstract class PostEffectClipBase : PlayableAsset, IMvPostEffectClip
    {
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<MvPostEffectClipBehaviour>.Create(graph);
            playable.GetBehaviour().Clip = this;
            return playable;
        }

        public abstract void Apply(
            Haruki.MV.MvPostEffectState target,
            int paramType,
            double timelineTimeStamp);
    }
}
