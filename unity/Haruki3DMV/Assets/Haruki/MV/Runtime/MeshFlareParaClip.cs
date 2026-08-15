using Sekai.Timeline.Common;
using UnityEngine;
using UnityEngine.Playables;

namespace Sekai.Core.Live
{
    public sealed class MeshFlareParaClip : PlayableAsset
    {
        public enum MeshBlendMode
        {
            Overwrite = 0,
            Add = 1,
            TransparentAdd = 2,
            AlphaBlend = 3,
            Multiply = 4,
            MultiplyAdd = 5,
        }

        [SerializeField]
        private ReferenceEnumParam<MeshBlendMode> meshFlareParaBlendMode =
            new ReferenceEnumParam<MeshBlendMode>(MeshBlendMode.Overwrite);

        [SerializeField]
        public ReferenceColorBlend meshFlareParaColor = new ReferenceColorBlend();

        [SerializeField]
        public ReferenceVector3Blend meshFlareParaPosition = new ReferenceVector3Blend();

        [SerializeField]
        public ReferenceVector2Blend meshFlareParaRadius = new ReferenceVector2Blend();

        [SerializeField]
        public ReferenceFloatBlend meshFlareParaTheta = new ReferenceFloatBlend();

        [SerializeField]
        public ReferenceVector2Blend meshFlareParaTiling = new ReferenceVector2Blend();

        [SerializeField]
        private ReferenceBoolParam meshFlareParaZTest = new ReferenceBoolParam();

        public ReferenceEnumParam<MeshBlendMode> MeshFlareParaBlendMode =>
            meshFlareParaBlendMode;
        public ReferenceColorBlend MeshFlareParaColor => meshFlareParaColor;
        public ReferenceVector3Blend MeshFlareParaPosition => meshFlareParaPosition;
        public ReferenceVector2Blend MeshFlareParaRadius => meshFlareParaRadius;
        public ReferenceFloatBlend MeshFlareParaTheta => meshFlareParaTheta;
        public ReferenceVector2Blend MeshFlareParaTiling => meshFlareParaTiling;
        public ReferenceBoolParam MeshFlareParaZTest => meshFlareParaZTest;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<MeshFlareParaBehaviour>.Create(graph);
            playable.GetBehaviour().Clip = this;
            return playable;
        }

        public void Apply(MeshFlareParaController controller, int order, double time)
        {
            controller.SetActiveObj(order, true);
            controller.SetBlendModePropertyBlock(order, meshFlareParaBlendMode.param);
            controller.SetMultiBlendShaderKeyword(
                order,
                meshFlareParaBlendMode.param == MeshBlendMode.MultiplyAdd);
            controller.SetColorPropertyBlock(order, meshFlareParaColor.CalcBlend(time));
            controller.SetPositionAndScaleParams(
                order,
                meshFlareParaPosition.CalcBlend(time),
                meshFlareParaRadius.CalcBlend(time),
                meshFlareParaZTest.param);
            controller.SetTheta(order, meshFlareParaTheta.CalcBlend(time));
            controller.SetTiling(order, meshFlareParaTiling.CalcBlend(time));
        }
    }

    public sealed class MeshFlareParaBehaviour : PlayableBehaviour
    {
        public MeshFlareParaClip Clip { get; internal set; }
    }
}
