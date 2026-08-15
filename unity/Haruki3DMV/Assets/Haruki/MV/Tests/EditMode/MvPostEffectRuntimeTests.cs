using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Timeline.Common;
using UnityEngine;
using UnityEngine.Timeline;

namespace Haruki.MV.Tests
{
    public sealed class MvPostEffectRuntimeTests
    {
        [Test]
        public void ReferenceBlendUsesRecoveredCurveBeforeInterpolating()
        {
            var blend = new ReferenceFloatBlend
            {
                beginValue = 10f,
                endValue = 20f,
                blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 0.5f),
            };

            Assert.That(blend.Evaluate(1f), Is.EqualTo(15f).Within(0.0001f));
        }

        [Test]
        public void ReferenceBlendUsesAbsoluteClipTimestampsAndConstMode()
        {
            var blend = new ReferenceFloatBlend
            {
                blendType = InClipBlendType.Blend,
                beginValue = 10f,
                endValue = 20f,
                blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
            };
            blend.SetupRuntimeTimeStamp(5d, 9d);

            Assert.That(blend.CalcBlend(7d), Is.EqualTo(15f).Within(0.0001f));

            blend.blendType = InClipBlendType.Const;
            Assert.That(blend.CalcBlend(100d), Is.EqualTo(10f));
        }

        [Test]
        public void DirectionalBlurAppliesOnlyTheTrackParamFamily()
        {
            var host = new GameObject("PostEffect");
            var target = host.AddComponent<MvPostEffectState>();
            var clip = ScriptableObject.CreateInstance<DirectionalBlurClip>();
            clip.directionalBlurStrength.beginValue = 2f;
            clip.directionalBlurStrength.endValue = 6f;
            clip.directionalBlurStrength.blendType = InClipBlendType.Blend;
            clip.directionalBlurStrength.SetupRuntimeTimeStamp(0d, 1d);
            clip.directionalBlurDirection.beginValue = 90f;
            clip.directionalBlurDirection.endValue = 180f;
            clip.directionalBlurDirection.blendType = InClipBlendType.Blend;
            clip.directionalBlurDirection.SetupRuntimeTimeStamp(0d, 1d);
            clip.radialBlurStrength.beginValue = 99f;

            try
            {
                clip.Apply(target, 0, 0.5f);

                Assert.That(target.DirectionalBlurStrength, Is.EqualTo(4f));
                Assert.That(target.DirectionalBlurDirection, Is.EqualTo(135f));
                Assert.That(target.RadialBlurStrength, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TimelineSetupAssignsClipStartAndEndToEveryReferenceBlend()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var track = timeline.CreateTrack<DirectionalBlurTrack>(null, "DirectionalBlur");
            var timelineClip = track.CreateClip<DirectionalBlurClip>();
            timelineClip.start = 5d;
            timelineClip.duration = 4d;
            var clip = (DirectionalBlurClip)timelineClip.asset;
            clip.directionalBlurStrength.blendType = InClipBlendType.Blend;
            clip.directionalBlurStrength.beginValue = 10f;
            clip.directionalBlurStrength.endValue = 20f;
            clip.directionalBlurStrength.blendCurve =
                AnimationCurve.Linear(0f, 0f, 1f, 1f);

            try
            {
                MvReferenceBlendRuntime.Setup(timeline);

                Assert.That(
                    clip.directionalBlurStrength.RuntimeBeginTimeStamp,
                    Is.EqualTo(5d));
                Assert.That(
                    clip.directionalBlurStrength.RuntimeEndTimeStamp,
                    Is.EqualTo(9d));
                Assert.That(
                    clip.directionalBlurStrength.CalcBlend(7d),
                    Is.EqualTo(15f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(timeline);
            }
        }
    }
}
