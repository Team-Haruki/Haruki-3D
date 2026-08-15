using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UTJ
{
    [Serializable]
    public sealed class AngleLimits
    {
        public bool active;

        [Range(-180f, 0f)]
        public float min;

        [Range(0f, 180f)]
        public float max;

        public bool ConstrainVector(
            Vector3 basisSide,
            Vector3 basisUp,
            Vector3 basisForward,
            float springStrength,
            float deltaTime,
            ref Vector3 vector)
        {
            if (!active) return false;

            var upComponent = basisUp * Vector3.Dot(basisUp, vector);
            var plane = vector - upComponent;
            var radius = plane.magnitude;
            var planeDirection = radius <= 0.00001f ? Vector3.zero : plane / radius;
            var angle = Mathf.Asin(Mathf.Clamp(Vector3.Dot(planeDirection, basisSide), -1f, 1f)) *
                Mathf.Rad2Deg;
            var relaxed = angle - angle * springStrength * deltaTime * deltaTime;
            var clamped = Mathf.Clamp(relaxed, min, max);
            var bound = clamped >= 0f ? max : min;
            var ratio = Mathf.Abs(bound) > 0.0001f
                ? Mathf.Clamp01(clamped / bound)
                : 0f;
            var finalAngle = bound * ratio * Mathf.Deg2Rad;
            vector = upComponent + radius *
                (basisSide * Mathf.Sin(finalAngle) + basisForward * Mathf.Cos(finalAngle));
            return !Mathf.Approximately(bound * ratio, relaxed);
        }
    }

    public sealed class SpringBonePivot : MonoBehaviour
    {
    }

    public class SpringBone : MonoBehaviour
    {
        public enum CollisionStatus
        {
            NoCollision = 0,
            HeadIsEmbedded = 1,
            TailCollision = 2,
        }

        [Range(0f, 5000f)] public float stiffnessForce;
        [Range(0f, 1f)] public float dragForce;
        public Vector3 springForce;
        [Range(0f, 1f)] public float windInfluence;
        [Range(0f, 1500f)] public float SpringConstant;
        public Transform pivotNode;
        public float angularStiffness;
        public AngleLimits yAngleLimits = new AngleLimits();
        public AngleLimits zAngleLimits = new AngleLimits();
        public Transform[] lengthLimitTargets = Array.Empty<Transform>();
        [Range(0f, 0.5f)] public float radius;
        public SpringSphereCollider[] sphereColliders = Array.Empty<SpringSphereCollider>();
        public SpringCapsuleCollider[] capsuleColliders = Array.Empty<SpringCapsuleCollider>();
        public SpringPanelCollider[] panelColliders = Array.Empty<SpringPanelCollider>();

        private SpringManager _manager;
        private Vector3 _boneAxis;
        private float _springLength;
        private Quaternion _skinAnimationLocalRotation;
        private Quaternion _initialLocalRotation;
        private Quaternion _actualLocalRotation;
        private Vector3 _currentTipPosition;
        private Vector3 _previousTipPosition;
        private float[] _lengthsToLimitTargets = Array.Empty<float>();
        private Transform _localTransform;
        private Transform _parentTransform;
        private Vector3 _cachedPosition;
        private bool _initialized;

        public Vector3 CurrentTipPosition => _currentTipPosition;

        public void Initialize(SpringManager owner)
        {
            _manager = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
            _localTransform = transform;
            _parentTransform = transform.parent;
            _initialLocalRotation = transform.localRotation;

            sphereColliders = FilterNulls(sphereColliders);
            capsuleColliders = FilterNulls(capsuleColliders);
            panelColliders = FilterNulls(panelColliders);
            lengthLimitTargets = FilterNulls(lengthLimitTargets);

            var tip = ComputeChildPosition();
            var localTip = transform.InverseTransformPoint(tip);
            _boneAxis = localTip.magnitude <= 0.00001f
                ? Vector3.right
                : localTip.normalized;
            _springLength = Vector3.Distance(transform.position, tip);
            _currentTipPosition = tip;
            _previousTipPosition = tip;
            _lengthsToLimitTargets = lengthLimitTargets
                .Select(target => Vector3.Distance(target.position, tip))
                .ToArray();
            _cachedPosition = transform.position;
            _initialized = true;
        }

        public Vector3 ComputeChildPosition()
        {
            var children = new List<Transform>();
            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index);
                if (child.GetComponent<SpringBonePivot>() == null)
                {
                    children.Add(child);
                }
            }

            if (children.Count == 1) return children[0].position;
            var head = transform.position;
            if (children.Count == 0) return head - transform.right * 0.1f;

            var averagePosition = Vector3.zero;
            var averageDistance = 0f;
            foreach (var child in children)
            {
                averagePosition += child.position;
                averageDistance += Vector3.Distance(child.position, head);
            }
            averagePosition /= children.Count;
            averageDistance /= children.Count;
            var delta = averagePosition - head;
            var direction = delta.sqrMagnitude <= 0.0000000001f
                ? Vector3.right
                : delta.normalized;
            return head + direction * averageDistance;
        }

        public void UpdateSpring(float deltaTime, Vector3 externalForce)
        {
            EnsureInitialized();
            var head = _localTransform.position;
            _skinAnimationLocalRotation = _localTransform.localRotation;
            var parentRotation = _parentTransform != null
                ? _parentTransform.rotation
                : Quaternion.identity;
            var restRotation = parentRotation * _initialLocalRotation;
            var targetTip = head + restRotation * _boneAxis * _springLength;
            var stiffness = (targetTip - _currentTipPosition) * stiffnessForce;
            var acceleration = (springForce + externalForce + stiffness) *
                (0.5f * deltaTime * deltaTime);
            var velocity = (_currentTipPosition - _previousTipPosition) * (1f - dragForce);
            var oldTip = _currentTipPosition;
            _currentTipPosition += acceleration + velocity;
            _previousTipPosition = oldTip;

            var direction = _currentTipPosition - head;
            direction = direction.magnitude <= 0.001f
                ? _localTransform.TransformDirection(_boneAxis).normalized
                : direction.normalized;
            _currentTipPosition = head + direction * _springLength;
        }

        public void SatisfyConstraintsAndComputeRotation(float deltaTime, float dynamicRatio)
        {
            EnsureInitialized();
            _cachedPosition = _localTransform.position;
            if (_manager.enableLengthLimits) ApplyLengthLimits(deltaTime);

            var groundHit = _manager.collideWithGround && CheckForGroundCollision();
            if (!groundHit && _manager.enableCollision) CheckForCollision();
            if (_manager.enableAngleLimits) ApplyAngleLimits(deltaTime);

            if (!IsFinite(_currentTipPosition))
            {
                var parentRotation = _parentTransform != null
                    ? _parentTransform.rotation
                    : Quaternion.identity;
                _currentTipPosition = _cachedPosition +
                    (parentRotation * _initialLocalRotation) * _boneAxis * _springLength;
                _previousTipPosition = _currentTipPosition;
            }

            _actualLocalRotation = ComputeLocalRotation(_currentTipPosition);
            _localTransform.localRotation = Quaternion.Lerp(
                _skinAnimationLocalRotation,
                _actualLocalRotation,
                dynamicRatio);
        }

        public void ComputeRotation(float dynamicRatio)
        {
            EnsureInitialized();
            _skinAnimationLocalRotation = _localTransform.localRotation;
            _actualLocalRotation = ComputeLocalRotation(_currentTipPosition);
            _localTransform.localRotation = Quaternion.Lerp(
                _skinAnimationLocalRotation,
                _actualLocalRotation,
                dynamicRatio);
        }

        private Quaternion ComputeLocalRotation(Vector3 tip)
        {
            var parentRotation = _parentTransform != null
                ? _parentTransform.rotation
                : Quaternion.identity;
            var baseRotation = parentRotation * _initialLocalRotation;
            var target = Quaternion.Inverse(baseRotation) * (tip - _localTransform.position);
            if (target.sqrMagnitude <= 0.0000000001f)
            {
                return _initialLocalRotation;
            }
            var delta = Quaternion.FromToRotation(_boneAxis, target.normalized);
            return _initialLocalRotation * delta;
        }

        private void ApplyLengthLimits(float deltaTime)
        {
            var movement = Vector3.zero;
            var stiffness = SpringConstant * deltaTime * deltaTime;
            for (var index = 0; index < lengthLimitTargets.Length; index++)
            {
                var targetToTip = _currentTipPosition - lengthLimitTargets[index].position;
                var length = targetToTip.magnitude;
                if (length <= 0.00001f) continue;
                movement -= targetToTip / length *
                    (stiffness * (length - _lengthsToLimitTargets[index]));
            }
            _currentTipPosition += movement;
        }

        private bool CheckForGroundCollision()
        {
            var localHead = _cachedPosition - Vector3.up * _manager.groundHeight;
            var localTail = _currentTipPosition - Vector3.up * _manager.groundHeight;
            var moverRadius = _localTransform.TransformVector(radius, 0f, 0f).magnitude;
            var status = SpringPanelCollider.CheckForCollisionWithAlignedPlaneAndReact(
                localHead,
                Vector3.Distance(_cachedPosition, _currentTipPosition),
                ref localTail,
                moverRadius,
                1);
            if (status == CollisionStatus.NoCollision) return false;

            localTail.y += _manager.groundHeight;
            _currentTipPosition = FixBoneLength(
                _cachedPosition,
                localTail,
                _springLength * 0.5f,
                _springLength);
            _previousTipPosition = _currentTipPosition;
            return true;
        }

        private bool CheckForCollision()
        {
            var beforeCollisionTip = _currentTipPosition;
            var beforeCollisionPrevious = _previousTipPosition;
            var moverRadius = _localTransform.TransformVector(radius, 0f, 0f).magnitude;
            var hitNormal = Vector3.forward;
            var hit = false;

            foreach (var collider in capsuleColliders)
            {
                if (collider != null && collider.enabled && collider.IsRendererEnabled)
                    hit |= collider.CheckForCollisionAndReact(
                        _cachedPosition,
                        ref _currentTipPosition,
                        moverRadius,
                        ref hitNormal) != CollisionStatus.NoCollision;
            }
            foreach (var collider in sphereColliders)
            {
                if (collider != null && collider.enabled)
                    hit |= collider.CheckForCollisionAndReact(
                        _cachedPosition,
                        ref _currentTipPosition,
                        moverRadius,
                        ref hitNormal) != CollisionStatus.NoCollision;
            }
            foreach (var collider in panelColliders)
            {
                if (collider != null && collider.enabled && collider.IsRendererEnabled)
                    hit |= collider.CheckForCollisionAndReact(
                        _cachedPosition,
                        _springLength,
                        ref _currentTipPosition,
                        moverRadius,
                        ref hitNormal) != CollisionStatus.NoCollision;
            }
            if (!hit) return false;

            var normal = hitNormal.sqrMagnitude <= 0.0000000001f
                ? Vector3.right
                : hitNormal.normalized;
            var velocity = _currentTipPosition - beforeCollisionPrevious;
            var normalVelocity = normal * Vector3.Dot(velocity, normal);
            var tangentVelocity = velocity - normalVelocity;
            var response = tangentVelocity * (1f - _manager.friction) -
                normalVelocity * _manager.bounce;
            if (response.sqrMagnitude <= 0.0001f)
            {
                _previousTipPosition = _currentTipPosition;
                return true;
            }

            _previousTipPosition = _currentTipPosition - response;
            var oldSpeed = Vector3.Distance(beforeCollisionTip, beforeCollisionPrevious);
            var responseSpeed = response.magnitude;
            var extra = Mathf.Max(responseSpeed - oldSpeed, 0f);
            if (extra > 0f) _currentTipPosition += response / responseSpeed * extra;
            return true;
        }

        private void ApplyAngleLimits(float deltaTime)
        {
            if (!(yAngleLimits?.active ?? false) && !(zAngleLimits?.active ?? false)) return;
            var vector = _currentTipPosition - _cachedPosition;
            var pivot = pivotNode != null ? pivotNode : _localTransform;
            var forward = -pivot.right;
            var back = -pivot.forward;
            var down = -pivot.up;
            yAngleLimits?.ConstrainVector(
                down,
                back,
                forward,
                angularStiffness,
                deltaTime,
                ref vector);
            zAngleLimits?.ConstrainVector(
                back,
                down,
                forward,
                angularStiffness,
                deltaTime,
                ref vector);
            _currentTipPosition = _cachedPosition + vector;
        }

        private static Vector3 FixBoneLength(
            Vector3 head,
            Vector3 tail,
            float minLength,
            float maxLength)
        {
            var vector = tail - head;
            var length = vector.magnitude;
            if (length <= 0.001f) return head + Vector3.right * minLength;
            return head + vector * (Mathf.Clamp(length, minLength, maxLength) / length);
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                if (_manager == null) _manager = GetComponentInParent<SpringManager>();
                if (_manager == null)
                    throw new InvalidOperationException($"SpringBone '{name}' has no SpringManager.");
                Initialize(_manager);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static T[] FilterNulls<T>(T[] source) where T : UnityEngine.Object
        {
            return source == null ? Array.Empty<T>() : source.Where(value => value != null).ToArray();
        }
    }
}
