using Sekai.Core.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sekai.Core
{
    [RequireComponent(typeof(Renderer))]
    [ExecuteInEditMode]
    public sealed class LookAtAxis : MonoBehaviour
    {
        public enum Axis
        {
            X = 0,
            Y = 1,
            Z = 2,
        }

        [SerializeField]
        public Axis axis;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += UpdateCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= UpdateCamera;
        }

        private void UpdateCamera(ScriptableRenderContext context, Camera camera)
        {
            if (RenderConfig.lookAtTargetCamera != null
                && RenderConfig.lookAtTargetCamera != camera)
            {
                return;
            }

            var lookRotation = Quaternion.LookRotation(
                transform.position - camera.transform.position,
                Vector3.up);
            if (transform.parent != null)
            {
                lookRotation = Quaternion.Inverse(transform.parent.rotation) * lookRotation;
            }

            var euler = lookRotation.eulerAngles;
            switch (axis)
            {
                case Axis.X:
                    euler.y = 0f;
                    euler.z = 0f;
                    break;
                case Axis.Y:
                    euler.x = 0f;
                    euler.z = 0f;
                    break;
                case Axis.Z:
                    euler.x = 0f;
                    euler.y = 0f;
                    break;
            }
            transform.localEulerAngles = euler;
        }
    }
}
