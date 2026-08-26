using UnityEngine;

namespace UTJ
{
    public sealed class SpringPanelCollider : MonoBehaviour
    {
        public float width;
        public float height;
        public Renderer linkedRenderer;

        private Matrix4x4 _worldToLocal;
        private Matrix4x4 _localToWorld;
        private float _worldToLocalRadiusScale;
        private float _worldToLocalLengthScale;

        public bool IsRendererEnabled => linkedRenderer == null || linkedRenderer.enabled;

        public void PreUpdate()
        {
            _worldToLocal = transform.worldToLocalMatrix;
            _localToWorld = transform.localToWorldMatrix;
            _worldToLocalRadiusScale = _worldToLocal.MultiplyVector(Vector3.right).magnitude;
            _worldToLocalLengthScale = _worldToLocalRadiusScale;
        }

        public SpringBone.CollisionStatus CheckForCollisionAndReact(
            Vector3 headPosition,
            float boneLength,
            ref Vector3 tailPosition,
            float tailRadius,
            ref Vector3 hitNormal)
        {
            PreUpdate();
            var localTail = _worldToLocal.MultiplyPoint3x4(tailPosition);
            var localTailRadius = tailRadius * _worldToLocalRadiusScale;
            if (localTail.z >= localTailRadius) return SpringBone.CollisionStatus.NoCollision;

            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            if (Mathf.Abs(localTail.x) >= halfWidth + localTailRadius ||
                Mathf.Abs(localTail.y) >= halfHeight + localTailRadius)
                return SpringBone.CollisionStatus.NoCollision;

            var localHead = _worldToLocal.MultiplyPoint3x4(headPosition);
            var localLength = boneLength * _worldToLocalLengthScale;
            var status = SpringBone.CollisionStatus.NoCollision;
            if (localTail.z > 0f || localHead.z > 0f)
            {
                if (Mathf.Abs(localTail.y) <= halfHeight && Mathf.Abs(localTail.x) <= halfWidth)
                {
                    status = CheckForCollisionWithAlignedPlaneAndReact(
                        localHead,
                        localLength,
                        ref localTail,
                        localTailRadius,
                        2);
                    if (status == SpringBone.CollisionStatus.NoCollision) return status;
                }
                else if (Mathf.Abs(localTail.y) > halfHeight)
                {
                    var edgeY = localTail.y >= 0f ? halfHeight : -halfHeight;
                    var normal = new Vector3(0f, localTail.y - edgeY, localTail.z).normalized;
                    localTail = new Vector3(
                        localTail.x + normal.x * localTailRadius,
                        edgeY + normal.y * localTailRadius,
                        normal.z * localTailRadius);
                    status = SpringBone.CollisionStatus.TailCollision;
                }
                else
                {
                    var edgeX = localTail.x >= 0f ? halfWidth : -halfWidth;
                    var normal = new Vector3(localTail.x - edgeX, 0f, localTail.z).normalized;
                    localTail = new Vector3(
                        edgeX + normal.x * localTailRadius,
                        localTail.y + normal.y * localTailRadius,
                        normal.z * localTailRadius);
                    status = SpringBone.CollisionStatus.TailCollision;
                }
            }
            else if (Mathf.Abs(localHead.y) <= halfHeight)
            {
                if (Mathf.Abs(localHead.x) <= halfWidth)
                {
                    localTail = new Vector3(localHead.x, localHead.y, localTailRadius);
                    status = SpringBone.CollisionStatus.HeadIsEmbedded;
                }
                else
                {
                    localTail.x = localTail.x < 0f ? -halfWidth : halfWidth;
                    status = SpringBone.CollisionStatus.TailCollision;
                }
            }
            else
            {
                localTail.y = localTail.y >= 0f ? halfHeight : -halfHeight;
                status = SpringBone.CollisionStatus.TailCollision;
            }

            tailPosition = _localToWorld.MultiplyPoint3x4(localTail);
            hitNormal = transform.forward.normalized;
            return status;
        }

        public static SpringBone.CollisionStatus CheckForCollisionWithAlignedPlaneAndReact(
            Vector3 localHeadPosition,
            float localLength,
            ref Vector3 localTailPosition,
            float localTailRadius,
            int upAxis)
        {
            var up = GetAxis(localTailPosition, upAxis);
            if (up >= localTailRadius) return SpringBone.CollisionStatus.NoCollision;

            var headUp = GetAxis(localHeadPosition, upAxis);
            if (headUp + localLength <= localTailRadius)
            {
                localTailPosition = localHeadPosition;
                SetAxis(ref localTailPosition, upAxis, headUp + localLength);
                return SpringBone.CollisionStatus.HeadIsEmbedded;
            }

            var sideA = (upAxis + 1) % 3;
            var sideB = (upAxis + 2) % 3;
            var a = GetAxis(localTailPosition, sideA) - GetAxis(localHeadPosition, sideA);
            var b = GetAxis(localTailPosition, sideB) - GetAxis(localHeadPosition, sideB);
            var sideLength = Mathf.Sqrt(a * a + b * b);
            if (sideLength > 0.001f)
            {
                var height = headUp - localTailRadius;
                var sideSquared = Mathf.Max(localLength * localLength - height * height, 0f);
                var scale = Mathf.Sqrt(sideSquared) / sideLength;
                SetAxis(
                    ref localTailPosition,
                    sideA,
                    GetAxis(localHeadPosition, sideA) + a * scale);
                SetAxis(
                    ref localTailPosition,
                    sideB,
                    GetAxis(localHeadPosition, sideB) + b * scale);
                SetAxis(ref localTailPosition, upAxis, localTailRadius);
            }
            else
            {
                localTailPosition = localHeadPosition;
            }
            return SpringBone.CollisionStatus.TailCollision;
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static void SetAxis(ref Vector3 value, int axis, float component)
        {
            if (axis == 0) value.x = component;
            else if (axis == 1) value.y = component;
            else value.z = component;
        }
    }
}
