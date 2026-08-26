using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haruki.MV
{
    public static class MvTimelineBinding
    {
        public static void BindTimeline(
            PlayableDirector director,
            TimelineAsset timeline,
            IReadOnlyDictionary<string, UnityEngine.Object> bindingObjects)
        {
            if (director == null)
            {
                throw new ArgumentNullException(nameof(director));
            }
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }
            if (bindingObjects == null)
            {
                throw new ArgumentNullException(nameof(bindingObjects));
            }

            director.playableAsset = timeline;
            foreach (var output in timeline.outputs)
            {
                director.SetGenericBinding(output.sourceObject, bindingObjects[output.streamName]);
            }
            director.time = 0;
            director.Evaluate();
        }
    }
}
