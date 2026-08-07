using System;
using UnityEngine;

namespace Haruki.MV
{
    public sealed class MvCameraAdjustment : MonoBehaviour
    {
        private float[] _heights = Array.Empty<float>();
        private float[] _heelOffsets = Array.Empty<float>();
        private float[] _defaultHeelOffsets = Array.Empty<float>();

        public int Target { get; set; }
        public int SecondTarget { get; set; }
        public float TargetLerp { get; set; }
        public float SelectedDefaultHeight { get; set; }
        public float SecondSelectedDefaultHeight { get; set; }

        public void SetCharacterHeight(MvCameraHeightData data)
        {
            if (data?.Heights == null || data.HeelOffsets == null || data.DefaultHeelOffsets == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Heights.Length == 0 ||
                data.Heights.Length != data.HeelOffsets.Length ||
                data.Heights.Length != data.DefaultHeelOffsets.Length)
            {
                throw new ArgumentException("Camera height arrays must be non-empty and equal in length.", nameof(data));
            }

            _heights = (float[])data.Heights.Clone();
            _heelOffsets = (float[])data.HeelOffsets.Clone();
            _defaultHeelOffsets = (float[])data.DefaultHeelOffsets.Clone();
        }

        public void Apply()
        {
            if (_heights.Length == 0)
            {
                throw new InvalidOperationException("Camera character heights have not been initialized.");
            }

            var first = HeightOffset(Target, SelectedDefaultHeight);
            var secondHeight = SecondSelectedDefaultHeight != 0
                ? SecondSelectedDefaultHeight
                : SelectedDefaultHeight;
            var second = HeightOffset(SecondTarget, secondHeight);
            var position = transform.localPosition;
            position.y = MvOfficialRuntimeData.BlendedCameraHeightOffset(
                first,
                second,
                TargetLerp);
            transform.localPosition = position;
        }

        private void LateUpdate()
        {
            if (_heights.Length != 0)
            {
                Apply();
            }
        }

        private float HeightOffset(int slot, float selectedDefaultHeight)
        {
            if (slot < 0 || slot >= _heights.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return MvOfficialRuntimeData.CameraHeightOffset(
                _heights[slot],
                _heelOffsets[slot],
                selectedDefaultHeight,
                _defaultHeelOffsets[slot]);
        }
    }
}
