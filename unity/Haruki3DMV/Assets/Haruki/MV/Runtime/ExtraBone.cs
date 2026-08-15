using Unity.Mathematics;
using UnityEngine;

namespace Sekai.Rendering
{
    [ExecuteAlways]
    public sealed class ExtraBone : MonoBehaviour
    {
        [SerializeField]
        public Transform referenceBone;

        [SerializeField]
        public math.RotationOrder rotationOrder = math.RotationOrder.ZXY;

        [Range(-1f, 1f)]
        [SerializeField]
        public float coefficient;

        [SerializeField]
        public Vector3 defaultEulerAngles;

        [SerializeField]
        public bool axisX;

        [SerializeField]
        public bool axisY;

        [SerializeField]
        public bool axisZ;

        private Quaternion _defaultRotation;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            _defaultRotation = Quaternion.Euler(defaultEulerAngles);
        }

        private void LateUpdate()
        {
            if (referenceBone == null)
            {
                return;
            }

            var euler = referenceBone.localEulerAngles;
            if (!axisX) euler.x = 0f;
            if (!axisY) euler.y = 0f;
            if (!axisZ) euler.z = 0f;

            if (Mathf.Approximately(coefficient, 0f))
            {
                euler = Vector3.zero;
            }
            else
            {
                euler *= coefficient > 0f ? -1f : 1f;
            }

            var radians = new float3(euler.x, euler.y, euler.z) * Mathf.Deg2Rad;
            var value = quaternion.Euler(radians, rotationOrder).value;
            var driven = new Quaternion(value.x, value.y, value.z, value.w);
            transform.localRotation = Quaternion.Lerp(
                _defaultRotation,
                driven,
                Mathf.Abs(coefficient));
        }
    }
}
