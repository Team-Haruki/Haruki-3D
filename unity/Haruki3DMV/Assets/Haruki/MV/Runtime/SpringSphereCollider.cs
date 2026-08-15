using UnityEngine;

namespace UTJ
{
    public sealed class SpringSphereCollider : MonoBehaviour
    {
        public float radius;
        public Renderer linkedRenderer;

        private Matrix4x4 _worldToLocal;
        private Matrix4x4 _localToWorld;
        private float _worldToLocalRadiusScale;

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
            var status = CheckSphere(
                localHead,
                ref localTail,
                localTailRadius,
                Vector3.zero,
                radius,
                ref hitNormal);
            if (status == SpringBone.CollisionStatus.NoCollision) return status;

            tailPosition = _localToWorld.MultiplyPoint3x4(localTail);
            hitNormal = transform.TransformDirection(hitNormal).normalized;
            return status;
        }

        internal static SpringBone.CollisionStatus CheckSphere(
            Vector3 head,
            ref Vector3 tail,
            float tailRadius,
            Vector3 center,
            float sphereRadius,
            ref Vector3 hitNormal)
        {
            var combinedRadius = tailRadius + sphereRadius;
            var centerToTail = tail - center;
            if (centerToTail.sqrMagnitude >= combinedRadius * combinedRadius)
                return SpringBone.CollisionStatus.NoCollision;

            if ((head - center).sqrMagnitude <= sphereRadius * sphereRadius)
            {
                hitNormal = centerToTail.sqrMagnitude <= 0.0000000001f
                    ? Vector3.up
                    : centerToTail.normalized;
                tail = center + hitNormal * combinedRadius;
                return SpringBone.CollisionStatus.HeadIsEmbedded;
            }

            if (!ComputeIntersection(
                head,
                Vector3.Distance(head, tail),
                center,
                combinedRadius,
                out var intersection))
            {
                hitNormal = centerToTail.sqrMagnitude <= 0.0000000001f
                    ? Vector3.up
                    : centerToTail.normalized;
                return SpringBone.CollisionStatus.TailCollision;
            }

            tail = ComputeNewTailPosition(intersection, tail);
            var normal = tail - center;
            hitNormal = normal.sqrMagnitude <= 0.0000000001f ? Vector3.up : normal.normalized;
            return SpringBone.CollisionStatus.TailCollision;
        }

        internal struct Circle3
        {
            public Vector3 origin;
            public Vector3 upVector;
            public float radius;
        }

        internal static bool ComputeIntersection(
            Vector3 originA,
            float radiusA,
            Vector3 originB,
            float radiusB,
            out Circle3 result)
        {
            result = default;
            var between = originB - originA;
            var distanceSquared = between.sqrMagnitude;
            var distance = Mathf.Sqrt(distanceSquared);
            if (distance <= 0f) return false;
            var numerator = radiusA * radiusA + distanceSquared - radiusB * radiusB;
            var radicand = radiusA * radiusA * distanceSquared * 4f - numerator * numerator;
            if (radicand < 0f) return false;

            result.upVector = between / distance;
            result.origin = originA + result.upVector * (numerator * 0.5f / distance);
            result.radius = 0.5f / distance * Mathf.Sqrt(radicand);
            return true;
        }

        internal static Vector3 ComputeNewTailPosition(Circle3 circle, Vector3 tail)
        {
            var projected = circle.origin + circle.upVector *
                Vector3.Dot(tail - circle.origin, circle.upVector);
            var radial = tail - projected;
            return radial.sqrMagnitude <= 0.0000000001f || circle.radius <= 0.00001f
                ? circle.origin
                : circle.origin + radial.normalized * circle.radius;
        }
    }
}
