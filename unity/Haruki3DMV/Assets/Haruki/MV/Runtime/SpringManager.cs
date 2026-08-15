using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UTJ
{
    public class ForceProvider : MonoBehaviour
    {
        public virtual Vector3 GetForceOnBone(SpringBone springBone) => Vector3.zero;
    }

    public sealed class SpringManager : MonoBehaviour
    {
        public bool automaticUpdates = true;
        public bool isPaused;
        public int simulationFrameRate = 60;
        [Range(0f, 1f)] public float dynamicRatio = 0.5f;
        public Vector3 gravity;
        [Range(0f, 1f)] public float bounce;
        [Range(0f, 1f)] public float friction = 1f;
        public bool enableAngleLimits = true;
        public bool enableCollision = true;
        public bool enableLengthLimits;
        public bool collideWithGround;
        public float groundHeight;
        public SpringBone[] springBones = Array.Empty<SpringBone>();

        private float _slowMotionScale = 1f;
        private SpringSphereCollider[] _cachedSpheres = Array.Empty<SpringSphereCollider>();
        private SpringCapsuleCollider[] _cachedCapsules = Array.Empty<SpringCapsuleCollider>();
        private SpringPanelCollider[] _cachedPanels = Array.Empty<SpringPanelCollider>();
        private bool _isSumOfForcesOnBone = true;
        private bool[] _boneIsAnimatedStates = Array.Empty<bool>();
        private ForceProvider[] _forceProviders = Array.Empty<ForceProvider>();

        public bool[] BoneIsAnimatedStates => _boneIsAnimatedStates;
        public float SlowMotionScale => _slowMotionScale;

        public void SetSumOfForcesOnBone(bool active) => _isSumOfForcesOnBone = active;
        public void SetSlowMotionScale(float value) => _slowMotionScale = value;

        public void FindSpringBones(bool includeInactive = false)
        {
            springBones = GetComponentsInChildren<SpringBone>(includeInactive)
                .OrderBy(bone => GetObjectDepth(bone.transform))
                .ToArray();
            SetupCollider();
        }

        public void UpdateBoneIsAnimatedStates(IList<string> animatedBoneNames)
        {
            var names = animatedBoneNames ?? Array.Empty<string>();
            _boneIsAnimatedStates = springBones
                .Select(bone => names.Contains(bone.name))
                .ToArray();
        }

        public void UpdateDynamics()
        {
            if (springBones == null) return;
            EnsureAnimatedStateSize();
            if (isPaused)
            {
                for (var index = 0; index < springBones.Length; index++)
                {
                    var bone = springBones[index];
                    if (bone != null && bone.enabled)
                        bone.ComputeRotation(_boneIsAnimatedStates[index] ? dynamicRatio : 1f);
                }
                return;
            }

            var deltaTime = simulationFrameRate > 0
                ? 1f / simulationFrameRate
                : Time.deltaTime;
            if (!Mathf.Approximately(_slowMotionScale, 1f)) deltaTime *= _slowMotionScale;
            PreUpdateColliders();
            if (!_isSumOfForcesOnBone) return;

            for (var index = 0; index < springBones.Length; index++)
            {
                var bone = springBones[index];
                if (bone == null || !bone.enabled) continue;
                bone.UpdateSpring(deltaTime, GetSumOfForcesOnBone(bone));
                bone.SatisfyConstraintsAndComputeRotation(
                    deltaTime,
                    _boneIsAnimatedStates[index] ? dynamicRatio : 1f);
            }
        }

        public void Initialize()
        {
            FindSpringBones(includeInactive: true);
            foreach (var bone in springBones) bone.Initialize(this);
            EnsureAnimatedStateSize();
            UpdateForceProviders();
        }

        public void CopyConfigurationFrom(SpringManager source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            automaticUpdates = source.automaticUpdates;
            isPaused = source.isPaused;
            simulationFrameRate = source.simulationFrameRate;
            dynamicRatio = source.dynamicRatio;
            gravity = source.gravity;
            bounce = source.bounce;
            friction = source.friction;
            enableAngleLimits = source.enableAngleLimits;
            enableCollision = source.enableCollision;
            enableLengthLimits = source.enableLengthLimits;
            collideWithGround = source.collideWithGround;
            groundHeight = source.groundHeight;
            _slowMotionScale = source._slowMotionScale;
            _isSumOfForcesOnBone = source._isSumOfForcesOnBone;
        }

        public void UpdateForceProviders()
        {
            _forceProviders = FindObjectsOfType<ForceProvider>(true);
        }

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            UpdateForceProviders();
        }

        private void LateUpdate()
        {
            if (automaticUpdates) UpdateDynamics();
        }

        private void SetupCollider()
        {
            var bones = springBones ?? Array.Empty<SpringBone>();
            _cachedSpheres = bones.Where(bone => bone != null)
                .SelectMany(bone => bone.sphereColliders ?? Array.Empty<SpringSphereCollider>())
                .Where(value => value != null).Distinct().ToArray();
            _cachedCapsules = bones.Where(bone => bone != null)
                .SelectMany(bone => bone.capsuleColliders ?? Array.Empty<SpringCapsuleCollider>())
                .Where(value => value != null).Distinct().ToArray();
            _cachedPanels = bones.Where(bone => bone != null)
                .SelectMany(bone => bone.panelColliders ?? Array.Empty<SpringPanelCollider>())
                .Where(value => value != null).Distinct().ToArray();
        }

        private void PreUpdateColliders()
        {
            foreach (var collider in _cachedSpheres) collider?.PreUpdate();
            foreach (var collider in _cachedCapsules) collider?.PreUpdate();
            foreach (var collider in _cachedPanels) collider?.PreUpdate();
        }

        private Vector3 GetSumOfForcesOnBone(SpringBone bone)
        {
            var force = gravity;
            foreach (var provider in _forceProviders)
            {
                if (provider != null && provider.isActiveAndEnabled)
                    force += provider.GetForceOnBone(bone) * bone.windInfluence;
            }
            return force;
        }

        private void EnsureAnimatedStateSize()
        {
            if (_boneIsAnimatedStates == null || _boneIsAnimatedStates.Length != springBones.Length)
                _boneIsAnimatedStates = new bool[springBones.Length];
        }

        private static int GetObjectDepth(Transform value)
        {
            var depth = 0;
            while (value != null)
            {
                depth++;
                value = value.parent;
            }
            return depth;
        }
    }
}
