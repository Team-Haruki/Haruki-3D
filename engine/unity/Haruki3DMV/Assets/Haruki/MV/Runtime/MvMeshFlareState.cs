using System;
using UnityEngine;

namespace Haruki.MV
{
    [Serializable]
    public sealed class MvMeshFlareOrderState
    {
        public bool Active { get; internal set; }
        public bool BlendMode { get; internal set; }
        public Color Color { get; internal set; }
        public Vector3 Position { get; internal set; }
        public Vector2 Radius { get; internal set; }
        public float Theta { get; internal set; }
        public Vector2 Tiling { get; internal set; }
        public bool ZTest { get; internal set; }
    }

    public sealed class MvMeshFlareState : MonoBehaviour
    {
        private readonly MvMeshFlareOrderState[] _orders =
        {
            new MvMeshFlareOrderState(),
            new MvMeshFlareOrderState(),
            new MvMeshFlareOrderState(),
        };

        public MvMeshFlareOrderState GetOrder(int order)
        {
            if (order < 0 || order >= _orders.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }
            return _orders[order];
        }

        public void Disable(int order)
        {
            GetOrder(order).Active = false;
        }
    }
}
