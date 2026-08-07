using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Haruki.MV
{
    public static class MvPlayerRenderSettings
    {
        public static void Apply(GameObject playerRoot)
        {
            if (playerRoot == null)
            {
                throw new ArgumentNullException(nameof(playerRoot));
            }

            foreach (var renderer in playerRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null)
                {
                    continue;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.skinnedMotionVectors = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }
    }
}
