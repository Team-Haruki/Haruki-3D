using System;
using System.Reflection;
using Sekai.Timeline.Common;
using UnityEngine.Timeline;

namespace Haruki.MV
{
    public static class MvReferenceBlendRuntime
    {
        private const BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        public static void Setup(TimelineAsset timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            foreach (var rootTrack in timeline.GetRootTracks())
            {
                SetupTrack(rootTrack);
            }
        }

        public static void SetupClip(TimelineClip timelineClip)
        {
            if (timelineClip == null)
            {
                throw new ArgumentNullException(nameof(timelineClip));
            }
            if (timelineClip.asset == null)
            {
                return;
            }

            var asset = timelineClip.asset;
            for (var type = asset.GetType(); type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(InstanceFields))
                {
                    if (field.GetValue(asset) is IReferenceBlendRuntime blend)
                    {
                        blend.SetupRuntimeTimeStamp(timelineClip.start, timelineClip.end);
                    }
                }
            }
        }

        private static void SetupTrack(TrackAsset track)
        {
            foreach (var timelineClip in track.GetClips())
            {
                SetupClip(timelineClip);
            }
            foreach (var childTrack in track.GetChildTracks())
            {
                SetupTrack(childTrack);
            }
        }
    }
}
