using System;
using Haruki.MV;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    public class SekaiCharacterOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private SekaiCharacterOutlinePass.OutlineSettings settings =
            new SekaiCharacterOutlinePass.OutlineSettings();

        private SekaiCharacterOutlinePass pass;

        public override void Create()
        {
            pass = new SekaiCharacterOutlinePass();
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            renderer.EnqueuePass(pass);
        }

        public override void SetupRenderPasses(
            ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            pass.Setup(settings);
            pass.renderPassEvent = RenderPassEvent.BeforeRendering;
        }
    }

    public class SekaiCharacterOutlinePass : ScriptableRenderPass
    {
        private static readonly int OutlineWidthId =
            Shader.PropertyToID("_SekaiOutlineWidth");
        private static readonly int OutlineFactorId =
            Shader.PropertyToID("_SekaiOutlineFactor");

        private OutlineSettings settings;

        [Serializable]
        public class OutlineSettings
        {
            [Header("幅（最小）")]
            [SerializeField]
            public float outlineWidthMin = MvRecoveredRendererContract.OutlineWidthMin;

            [Header("幅（最大）")]
            [SerializeField]
            public float outlineWidthMax = MvRecoveredRendererContract.OutlineWidthMax;

            [Header("変化距離（最小）")]
            [SerializeField]
            public float outlineDistanceNear =
                MvRecoveredRendererContract.OutlineDistanceNear;

            [Header("変化距離（最大）")]
            [SerializeField]
            public float outlineDistanceFar =
                MvRecoveredRendererContract.OutlineDistanceFar;

            [Header("FOV補正カーブ")]
            [SerializeField]
            public AnimationCurve fovCurve =
                MvRecoveredRendererContract.CreateOutlineFovCurve();
        }

        public void Setup(OutlineSettings value)
        {
            settings = value;
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            UpdateOutline(renderingData.cameraData.camera);
        }

        private void UpdateOutline(Camera camera)
        {
            if (camera == null || settings == null)
            {
                return;
            }

            var globals = MvRecoveredRendererContract.CalculateOutlineGlobals(
                camera.fieldOfView,
                settings.fovCurve,
                settings.outlineWidthMin,
                settings.outlineWidthMax,
                settings.outlineDistanceNear,
                settings.outlineDistanceFar);
            Shader.SetGlobalVector(OutlineWidthId, globals.Width);
            Shader.SetGlobalVector(OutlineFactorId, globals.Factor);
        }
    }
}
