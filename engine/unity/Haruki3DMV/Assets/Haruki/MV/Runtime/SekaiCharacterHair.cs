using System.Collections.Generic;
using UnityEngine;

namespace Sekai.Rendering
{
    [ExecuteAlways]
    public sealed class SekaiCharacterHair : MonoBehaviour
    {
        private static readonly int HeadPositionId = Shader.PropertyToID("_HeadPosition");
        private const string HairShadowKeyword = "_HAIR_SHADOW";
        private const string LambertKeyword = "_LAMBERT";

        [SerializeField]
        private Transform headTransform;

        [SerializeField]
        private Vector3 offset = new Vector3(-0.07f, 0f, 0f);

        private Material[] _materials;

        public void Setup(Transform headTrans, bool useHairShadow)
        {
            headTransform = headTrans;
            PopulateMaterials();
            foreach (var material in _materials)
            {
                if (material == null) continue;
                if (useHairShadow)
                {
                    material.EnableKeyword(HairShadowKeyword);
                    material.DisableKeyword(LambertKeyword);
                }
                else
                {
                    material.DisableKeyword(HairShadowKeyword);
                    material.EnableKeyword(LambertKeyword);
                }
            }
            OnUpdate();
        }

        private void OnEnable()
        {
            PopulateMaterials();
        }

        public void OnUpdate()
        {
            if (headTransform == null)
            {
                return;
            }
            if (_materials == null || _materials.Length == 0)
            {
                PopulateMaterials();
            }

            var headPosition = headTransform.TransformPoint(offset);
            foreach (var material in _materials)
            {
                if (material != null && material.HasProperty(HeadPositionId))
                {
                    material.SetVector(HeadPositionId, headPosition);
                }
            }
        }

        private void LateUpdate()
        {
            OnUpdate();
        }

        private void PopulateMaterials()
        {
            var unique = new HashSet<Material>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    if (material != null) unique.Add(material);
                }
            }
            _materials = new Material[unique.Count];
            unique.CopyTo(_materials);
        }
    }
}
