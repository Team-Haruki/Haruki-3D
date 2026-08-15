using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    [Serializable]
    public struct PlanarReflectionInfo
    {
        public int width;
        public int height;
        public float clipPlaneOffset;
        public float planeOffset;
    }

    /// <summary>
    /// Recovered renderer feature that owns the stage planar-reflection pass.
    /// </summary>
    public sealed class PlanarReflectionFeature : ScriptableRendererFeature
    {
        private PlanarReflectionPass _planarReflectionPass;

        [SerializeField]
        private Shader _drawStencilShader;

        [SerializeField]
        private PlanarReflectionInfo _planarReflectionInfo;

        private Material _drawStencilMaterial;

        public void ConfigureRecovered(Shader drawStencilShader, PlanarReflectionInfo info)
        {
            _drawStencilShader = drawStencilShader;
            _planarReflectionInfo = info;
        }

        public override void Create()
        {
            if (_drawStencilShader != null)
            {
                _drawStencilMaterial = CoreUtils.CreateEngineMaterial(_drawStencilShader);
            }

            _planarReflectionPass?.Dispose();
            _planarReflectionPass = PlanarReflectionPass.Instance;
            _planarReflectionPass.SetPass(
                _drawStencilMaterial,
                _planarReflectionInfo);
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (_planarReflectionPass != null &&
                _planarReflectionPass.EnablePlanarReflection)
            {
                renderer.EnqueuePass(_planarReflectionPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _planarReflectionPass?.Dispose();
            CoreUtils.Destroy(_drawStencilMaterial);
            _drawStencilMaterial = null;
        }
    }

    /// <summary>
    /// Official stage planar-reflection path reconstructed from the 6.7.0
    /// IL2CPP ARM64 body. It first draws the water-surface stencil mesh, then
    /// culls and draws ReflectionCaster passes through the mirrored camera.
    /// </summary>
    public sealed class PlanarReflectionPass : ScriptableRenderPass
    {
        public const string ShaderKeyword = "_USE_PLANAR_REFLECTION";
        private const string ProfilerTag = "PlanarReflectionPass";
        private const string ReflectionTextureName = "_ReflectionTex";
        private const string ReflectionDepthTextureName = "_ReflectionDepthTex";
        private const float CameraClipBias = 0.1f;

        private struct Plane
        {
            public Vector3 normal;
            public float distance;

            public Plane(Vector3 normal, float distance)
            {
                this.normal = normal;
                this.distance = distance;
            }
        }

        private readonly ShaderTagId _shaderTagId;
        private readonly ProfilingSampler _profilingSampler;
        private RTHandle _reflectionRT;
        private RTHandle _reflectionDepthRT;
        private string _reflectionTexName;
        private string _reflectionDepthTexName;
        private PlanarReflectionInfo _planarReflectionInfo;
        private Material _drawStencilMaterial;

        private static PlanarReflectionPass instance;

        private PlanarReflectionPass()
        {
            _shaderTagId = new ShaderTagId("ReflectionCaster");
            _profilingSampler = new ProfilingSampler(ProfilerTag);
            _reflectionTexName = ReflectionTextureName;
            _reflectionDepthTexName = ReflectionDepthTextureName;
        }

        public static PlanarReflectionPass Instance =>
            instance ?? (instance = new PlanarReflectionPass());

        public bool EnablePlanarReflection { get; set; }

        public bool EnableObjectTransparentSorting { get; set; }

        public Transform TargetTransform { get; set; }

        public List<Mesh> Meshes { get; set; }

        public void SetPass(Material drawStencilMaterial, PlanarReflectionInfo info)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            _drawStencilMaterial = drawStencilMaterial;
            _planarReflectionInfo = info;
            _planarReflectionInfo.width = Math.Max(2, info.width);
            _planarReflectionInfo.height = Math.Max(2, info.height);

            if (!EnablePlanarReflection)
            {
                return;
            }

            _reflectionRT = RTHandles.Alloc(
                _planarReflectionInfo.width,
                _planarReflectionInfo.height,
                depthBufferBits: DepthBits.None,
                colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Clamp,
                name: _reflectionTexName);
            _reflectionDepthRT = RTHandles.Alloc(
                _planarReflectionInfo.width,
                _planarReflectionInfo.height,
                depthBufferBits: DepthBits.Depth24,
                colorFormat: GraphicsFormat.R8G8B8A8_SRGB,
                filterMode: FilterMode.Point,
                name: _reflectionDepthTexName);

            Shader.SetGlobalTexture(_reflectionTexName, _reflectionRT);
            Shader.SetGlobalTexture(_reflectionDepthTexName, _reflectionDepthRT);
        }

        public override void Configure(
            CommandBuffer cmd,
            RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (!EnablePlanarReflection)
            {
                return;
            }

            ConfigureTarget(_reflectionRT, _reflectionDepthRT);
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            if (Meshes == null || TargetTransform == null || !EnablePlanarReflection)
            {
                return;
            }

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                foreach (var mesh in Meshes)
                {
                    if (mesh != null && TargetTransform != null)
                    {
                        cmd.DrawMesh(
                            mesh,
                            TargetTransform.localToWorldMatrix,
                            _drawStencilMaterial);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var camera = renderingData.cameraData.camera;
                var planePosition =
                    TargetTransform.position + Vector3.up * _planarReflectionInfo.planeOffset;
                var planeNormal = TargetTransform.up;
                var plane = new Plane(
                    planeNormal,
                    -Vector3.Dot(planeNormal, planePosition) -
                    _planarReflectionInfo.clipPlaneOffset);

                var reflectionMatrix =
                    Matrix4x4.identity * Matrix4x4.Scale(new Vector3(1f, -1f, 1f));
                CalculateReflectionMatrix(ref reflectionMatrix, plane);
                var reflectionView = camera.worldToCameraMatrix * reflectionMatrix;
                var clipPlane = CameraSpacePlane(
                    reflectionView,
                    planePosition - Vector3.up * CameraClipBias,
                    planeNormal,
                    1f);
                var reflectionProjection = camera.CalculateObliqueMatrix(
                    new Vector4(
                        clipPlane.normal.x,
                        clipPlane.normal.y,
                        clipPlane.normal.z,
                        clipPlane.distance));

                camera.cullingMatrix = reflectionProjection * reflectionView;
                camera.TryGetCullingParameters(out var cullingParameters);
                camera.ResetCullingMatrix();
                var cullingResults = context.Cull(ref cullingParameters);

                var sortingCriteria = EnableObjectTransparentSorting
                    ? SortingCriteria.CommonTransparent
                    : renderingData.cameraData.defaultOpaqueSortFlags;
                var drawingSettings = CreateDrawingSettings(
                    _shaderTagId,
                    ref renderingData,
                    sortingCriteria);
                var filteringSettings = new FilteringSettings(RenderQueueRange.all);

                cmd.SetViewProjectionMatrices(reflectionView, reflectionProjection);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(
                    cullingResults,
                    ref drawingSettings,
                    ref filteringSettings);

                cmd.SetViewProjectionMatrices(
                    renderingData.cameraData.GetViewMatrix(),
                    renderingData.cameraData.GetProjectionMatrix());
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _reflectionRT?.Release();
            _reflectionDepthRT?.Release();
            _reflectionRT = null;
            _reflectionDepthRT = null;
        }

        public void SetShaderEnableKeyword(bool enablePlanarReflection)
        {
            if (enablePlanarReflection)
            {
                Shader.EnableKeyword(ShaderKeyword);
            }
            else
            {
                Shader.DisableKeyword(ShaderKeyword);
            }
        }

        private Plane CameraSpacePlane(
            Matrix4x4 worldToCameraMatrix,
            Vector3 pos,
            Vector3 normal,
            float sideSign)
        {
            var offsetPos = pos + normal * _planarReflectionInfo.clipPlaneOffset;
            var cameraSpacePosition = worldToCameraMatrix.MultiplyPoint(offsetPos);
            var cameraSpaceNormal =
                worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
            return new Plane(
                cameraSpaceNormal,
                -Vector3.Dot(cameraSpacePosition, cameraSpaceNormal));
        }

        private static void CalculateReflectionMatrix(
            ref Matrix4x4 reflectionMat,
            Plane plane)
        {
            var x = plane.normal.x;
            var y = plane.normal.y;
            var z = plane.normal.z;
            var distance = plane.distance;

            reflectionMat.m00 = 1f - 2f * x * x;
            reflectionMat.m01 = -2f * x * y;
            reflectionMat.m02 = -2f * x * z;
            reflectionMat.m03 = -2f * distance * x;
            reflectionMat.m10 = -2f * y * x;
            reflectionMat.m11 = 1f - 2f * y * y;
            reflectionMat.m12 = -2f * y * z;
            reflectionMat.m13 = -2f * distance * y;
            reflectionMat.m20 = -2f * z * x;
            reflectionMat.m21 = -2f * z * y;
            reflectionMat.m22 = 1f - 2f * z * z;
            reflectionMat.m23 = -2f * distance * z;
            reflectionMat.m30 = 0f;
            reflectionMat.m31 = 0f;
            reflectionMat.m32 = 0f;
            reflectionMat.m33 = 1f;
        }
    }
}
