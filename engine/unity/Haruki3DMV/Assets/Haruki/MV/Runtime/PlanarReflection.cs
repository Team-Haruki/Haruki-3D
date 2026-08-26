using System.Collections.Generic;
using UnityEngine;

namespace Sekai.Rendering
{
    /// <summary>
    /// Recovered stage-surface binding. It registers the surface meshes used
    /// to establish the planar-reflection stencil and updates the singleton
    /// pass target each frame.
    /// </summary>
    [ExecuteInEditMode]
    public sealed class PlanarReflection : MonoBehaviour
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();

        private void Start()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer is MeshRenderer meshRenderer)
                {
                    var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                    _meshes.Add(meshFilter.sharedMesh);
                    SetupMaterial(meshRenderer);
                }

                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    _meshes.Add(skinnedMeshRenderer.sharedMesh);
                    SetupMaterial(skinnedMeshRenderer);
                }
            }
        }

        private void Update()
        {
            if (PlanarReflectionPass.Instance == null)
            {
                return;
            }

            PlanarReflectionPass.Instance.TargetTransform = transform;
            PlanarReflectionPass.Instance.Meshes = _meshes;
        }

        private static void SetupMaterial(Renderer renderer)
        {
            var material = renderer.sharedMaterial;
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 2999;
        }
    }
}
