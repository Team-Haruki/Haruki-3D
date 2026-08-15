using UnityEngine;

namespace Sekai.Core
{
    public sealed class MeshFlareParaTexData : ScriptableObject
    {
        [HideInInspector]
        [SerializeField]
        private int id;

        [SerializeField]
        private Texture2D[] _texture2Ds = new Texture2D[3];

        public int Id => id;
        public Texture2D[] Texture2Ds => _texture2Ds;
    }
}
