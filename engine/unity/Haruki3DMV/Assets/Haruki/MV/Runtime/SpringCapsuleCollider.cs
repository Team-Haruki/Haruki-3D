using UnityEngine;

namespace UTJ
{
    public sealed class SpringCapsuleCollider : MonoBehaviour
    {
        public float radius;
        public float height;
        public Renderer linkedRenderer;

        private Matrix4x4 _worldToLocal;
        private Matrix4x4 _localToWorld;
        private float _worldToLocalRadiusScale;

        public bool IsRendererEnabled => linkedRenderer == null || linkedRenderer.enabled;

        public void PreUpdate()
        {
            _worldToLocal = transform.worldToLocalMatrix;
            _localToWorld = transform.localToWorldMatrix;
            _worldToLocalRadiusScale = _worldToLocal.MultiplyVector(Vector3.right).magnitude;
        }

        public SpringBone.CollisionStatus CheckForCollisionAndReact(
            Vector3 headPosition,
            ref Vector3 tailPosition,
            float tailRadius,
            ref Vector3 hitNormal)
        {
            PreUpdate();
            var localHead = _worldToLocal.MultiplyPoint3x4(headPosition);
            var localTail = _worldToLocal.MultiplyPoint3x4(tailPosition);
            var localTailRadius = tailRadius * _worldToLocalRadiusScale;
            var status = CheckLocalCapsule(
                localHead,
                ref localTail,
                localTailRadius,
                radius,
                height,
                ref hitNormal);
            if (status == SpringBone.CollisionStatus.NoCollision) return status;

            tailPosition = _localToWorld.MultiplyPoint3x4(localTail);
            hitNormal = transform.TransformDirection(hitNormal).normalized;
            return status;
        }

        private static SpringBone.CollisionStatus CheckLocalCapsule(
            Vector3 head,
            ref Vector3 tail,
            float tailRadius,
            float capsuleRadius,
            float capsuleHeight,
            ref Vector3 hitNormal)
        {
            if (capsuleRadius <= 0.0001f) return SpringBone.CollisionStatus.NoCollision;
            if (tail.y <= 0f || tail.y >= capsuleHeight)
            {
                var center = tail.y < capsuleHeight
                    ? Vector3.zero
                    : Vector3.up * capsuleHeight;
                return SpringSphereCollider.CheckSphere(
                    head,
                    ref tail,
                    tailRadius,
                    center,
                    capsuleRadius,
                    ref hitNormal);
            }

            var combinedRadius = capsuleRadius + tailRadius;
            var radialSquared = tail.x * tail.x + tail.z * tail.z;
            if (radialSquared > combinedRadius * combinedRadius)
                return SpringBone.CollisionStatus.NoCollision;

            var radialLength = Mathf.Sqrt(radialSquared);
            var normalX = radialLength > 0.00001f ? tail.x / radialLength : 0f;
            var normalZ = radialLength > 0.00001f ? tail.z / radialLength : 0f;
            tail.x = combinedRadius * normalX;
            tail.z = combinedRadius * normalZ;
            hitNormal = new Vector3(normalX, 0f, normalZ);
            var headRadialSquared = head.x * head.x + head.z * head.z;
            return headRadialSquared <= capsuleRadius * capsuleRadius
                ? SpringBone.CollisionStatus.HeadIsEmbedded
                : SpringBone.CollisionStatus.TailCollision;
        }
    }
}
