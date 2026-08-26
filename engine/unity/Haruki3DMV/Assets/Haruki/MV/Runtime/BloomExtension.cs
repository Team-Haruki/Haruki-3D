using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
    /// <summary>
    /// Immutable size portion of the 6.7.0 BloomExtension packed-atlas
    /// contract. The official implementation keeps seven integer-truncated
    /// levels and packs them into a surface wider than level zero by two
    /// margins and taller than levels zero and one by four margins.
    /// </summary>
    public readonly struct BloomAtlasLayout
    {
        internal BloomAtlasLayout(
            Vector2Int atlasSize,
            Vector2Int[] levelSizes,
            Rect[] textureMaps)
        {
            AtlasSize = atlasSize;
            LevelSizes = levelSizes;
            TextureMaps = textureMaps;
        }

        public Vector2Int AtlasSize { get; }

        public IReadOnlyList<Vector2Int> LevelSizes { get; }

        /// <summary>
        /// Official normalized clip-space rectangles used to pack the seven
        /// levels into the bloom sheet. They intentionally use the level-zero
        /// source dimensions for margin normalization, matching
        /// BloomExtension.SetupSizeInfo rather than the atlas dimensions.
        /// </summary>
        public IReadOnlyList<Rect> TextureMaps { get; }
    }

    /// <summary>
    /// Recovered Sekai packed Bloom helper. This type deliberately does not
    /// replace the game's atlas with URP Bloom: its constants and integer
    /// sizing come directly from BloomExtension.SetupSizeInfo at
    /// JP 6.7.0 RVA 0xA809988 and the captured 384x256 -> 398x412 RT state.
    /// </summary>
    public sealed class BloomExtension
        : IDisposable
    {
        public const int DownCount = 7;
        public const int Margin = 7;
        public const float ScatterNormalization = 0.01f;

        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int BloomScatterId = Shader.PropertyToID("_BloomScatter");
        private static readonly int BloomScatterWeightId = Shader.PropertyToID("_BloomScatterWeight");

        private static readonly Vector3[] FullscreenVertices =
        {
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, -1f, 0f),
            new Vector3(-1f, -1f, 0f),
        };

        private static readonly Vector2[] FullscreenUvs =
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
        };

        private static readonly int[] QuadTriangles = { 0, 1, 2, 0, 2, 3 };

        private RTHandle _sourceHandle;
        private RTHandle _destHandle;
        private RTHandle _tempBufferHandle;
        private readonly RTHandle[] _bloomBufferHandles = new RTHandle[2];
        private Material _material;
        private Mesh _textureToSheetMesh;
        private Mesh _sheetToTextureMesh;
        private BloomAtlasLayout _layout;
        private Vector2Int _latestPostSize;

        public BloomExtension()
        {
            Intensity = 0.2f;
            Scatter = 2f;
        }

        public BloomExtension(
            RTHandle sourceHandle,
            RTHandle destHandle,
            int postBlurWidth,
            int postBlurHeight,
            Material material)
            : this()
        {
            SetupBloom(sourceHandle, destHandle);
            _material = material ?? throw new ArgumentNullException(nameof(material));
            EnsureResources(postBlurWidth, postBlurHeight);
        }

        public float Intensity { get; set; }

        public float Scatter { get; set; }

        public int DestPropertyId => _destHandle == null
            ? -1
            : Shader.PropertyToID(_destHandle.name);

        public BloomAtlasLayout Layout => _layout;

        public Mesh TextureToSheetMesh => _textureToSheetMesh;

        public Mesh SheetToTextureMesh => _sheetToTextureMesh;

        public void SetupBloom(RTHandle sourceHandle, RTHandle destHandle)
        {
            _sourceHandle = sourceHandle ?? throw new ArgumentNullException(nameof(sourceHandle));
            _destHandle = destHandle ?? throw new ArgumentNullException(nameof(destHandle));
        }

        public void Execute(
            CommandBuffer commandBuffer,
            Camera camera,
            Material material,
            int postWidth,
            int postHeight)
        {
            if (commandBuffer == null)
            {
                throw new ArgumentNullException(nameof(commandBuffer));
            }
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }
            if (_sourceHandle == null || _destHandle == null)
            {
                throw new InvalidOperationException("SetupBloom must be called before Execute.");
            }

            _material = material;
            EnsureResources(postWidth, postHeight);
            SetupTexture(commandBuffer);
            DrawBloomSheet(camera, commandBuffer);
            DrawBlur(commandBuffer);
            DrawBloomTexture(camera, commandBuffer);
        }

        public static BloomAtlasLayout CalculateLayout(int postWidth, int postHeight)
        {
            if (postWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(postWidth));
            }
            if (postHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(postHeight));
            }

            var levelSizes = new Vector2Int[DownCount];
            for (var level = 0; level < levelSizes.Length; level++)
            {
                // The IL2CPP body evaluates width / 2^level and converts the
                // result with fcvtzs, i.e. truncation toward zero. Inputs are
                // positive, so integer division is exactly equivalent.
                var divisor = 1 << level;
                levelSizes[level] = new Vector2Int(
                    postWidth / divisor,
                    postHeight / divisor);
            }

            var atlasSize = new Vector2Int(
                levelSizes[0].x + Margin * 2,
                levelSizes[0].y + levelSizes[1].y + Margin * 4);
            var gapX = 2f * Margin / levelSizes[0].x;
            var gapY = 2f * Margin / levelSizes[0].y;
            var textureMaps = new Rect[DownCount];

            textureMaps[0] = CreateTextureMap(levelSizes[0], atlasSize, -1f, -1f);
            textureMaps[1] = CreateTextureMap(
                levelSizes[1],
                atlasSize,
                -1f,
                textureMaps[0].yMax + gapY);
            textureMaps[2] = CreateTextureMap(
                levelSizes[2],
                atlasSize,
                textureMaps[1].xMax + gapX,
                textureMaps[1].y);
            textureMaps[3] = CreateTextureMap(
                levelSizes[3],
                atlasSize,
                textureMaps[2].x,
                textureMaps[2].yMax + gapY);
            textureMaps[4] = CreateTextureMap(
                levelSizes[4],
                atlasSize,
                textureMaps[2].xMax + gapX,
                textureMaps[1].y);
            textureMaps[5] = CreateTextureMap(
                levelSizes[5],
                atlasSize,
                textureMaps[4].xMax + gapX,
                textureMaps[1].y);
            textureMaps[6] = CreateTextureMap(
                levelSizes[6],
                atlasSize,
                textureMaps[5].x,
                textureMaps[5].yMax + gapY);

            return new BloomAtlasLayout(atlasSize, levelSizes, textureMaps);
        }

        public static float CalculateScatterWeight(float scatter)
        {
            var total = 0f;
            for (var level = 0; level < DownCount; level++)
            {
                total += Mathf.Pow(scatter, level) * ScatterNormalization;
            }
            return total == 0f ? 0f : 1f / total;
        }

        public static Mesh CreateTextureToSheetMesh(BloomAtlasLayout layout)
        {
            return CreateBloomMesh(layout, false);
        }

        public static Mesh CreateSheetToTextureMesh(BloomAtlasLayout layout)
        {
            return CreateBloomMesh(layout, true);
        }

        private static Rect CreateTextureMap(
            Vector2Int levelSize,
            Vector2Int atlasSize,
            float x,
            float y)
        {
            return new Rect(
                x,
                y,
                2f * levelSize.x / atlasSize.x,
                2f * levelSize.y / atlasSize.y);
        }

        private static Mesh CreateBloomMesh(
            BloomAtlasLayout layout,
            bool sheetToTexture)
        {
            if (layout.LevelSizes == null || layout.TextureMaps == null)
            {
                throw new ArgumentException("Bloom layout is not initialized.", nameof(layout));
            }

            var vertices = new List<Vector3>(4 + DownCount * 4);
            var uvs = new List<Vector2>(4 + DownCount * 4);
            var levelWeights = new List<Vector2>(4 + DownCount * 4);
            var triangles = new List<int>(6 + DownCount * 6);

            vertices.AddRange(FullscreenVertices);
            uvs.AddRange(FullscreenUvs);
            for (var vertex = 0; vertex < 4; vertex++)
            {
                levelWeights.Add(new Vector2(0f, sheetToTexture ? 1f : 0f));
            }
            triangles.AddRange(QuadTriangles);

            for (var level = 0; level < DownCount; level++)
            {
                var info = new BloomTextureInfo(
                    layout.LevelSizes[level].x,
                    layout.LevelSizes[level].y,
                    layout.TextureMaps[level]);
                var positions = sheetToTexture ? info.SheetVertices : info.Vertices;
                var textureUvs = sheetToTexture ? info.SheetUvs : info.Uvs;
                var vertexOffset = vertices.Count;

                foreach (var position in positions)
                {
                    vertices.Add(new Vector3(position.x, position.y, 0f));
                }
                uvs.AddRange(textureUvs);
                for (var vertex = 0; vertex < positions.Length; vertex++)
                {
                    levelWeights.Add(new Vector2(level, sheetToTexture ? 0f : 1f));
                }
                foreach (var triangle in info.Triangles)
                {
                    triangles.Add(vertexOffset + triangle);
                }
            }

            var mesh = new Mesh
            {
                name = sheetToTexture
                    ? "Bloom Sheet To Texture"
                    : "Bloom Texture To Sheet",
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, levelWeights);
            mesh.SetTriangles(triangles, 0, false);
            mesh.UploadMeshData(false);
            return mesh;
        }

        private void EnsureResources(int postWidth, int postHeight)
        {
            var postSize = new Vector2Int(postWidth, postHeight);
            if (_latestPostSize == postSize &&
                _textureToSheetMesh != null &&
                _sheetToTextureMesh != null)
            {
                return;
            }

            _layout = CalculateLayout(postWidth, postHeight);
            _latestPostSize = postSize;
            CoreUtils.Destroy(_textureToSheetMesh);
            CoreUtils.Destroy(_sheetToTextureMesh);
            _textureToSheetMesh = CreateTextureToSheetMesh(_layout);
            _sheetToTextureMesh = CreateSheetToTextureMesh(_layout);
            AllocateTempBuffer(postWidth, postHeight);
            AllocateBloomBuffers(_layout.AtlasSize.x, _layout.AtlasSize.y);
        }

        private void AllocateTempBuffer(int width, int height)
        {
            var descriptor = BloomDescriptor(width, height);
            descriptor.useMipMap = true;
            descriptor.autoGenerateMips = true;
            descriptor.mipCount = 0;
            RenderingUtils.ReAllocateIfNeeded(
                ref _tempBufferHandle,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                false,
                1,
                0f,
                "_TempBuffer");
        }

        private void AllocateBloomBuffers(int width, int height)
        {
            var descriptor = BloomDescriptor(width, height);
            for (var index = 0; index < _bloomBufferHandles.Length; index++)
            {
                RenderingUtils.ReAllocateIfNeeded(
                    ref _bloomBufferHandles[index],
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0f,
                    $"_BloomBuffer{index + 1}");
            }
        }

        private static RenderTextureDescriptor BloomDescriptor(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(
                width,
                height,
                GraphicsFormat.R8G8B8A8_UNorm,
                0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                useMipMap = false,
                autoGenerateMips = false,
            };
            return descriptor;
        }

        private void SetupTexture(CommandBuffer commandBuffer)
        {
            Blitter.BlitCameraTexture(
                commandBuffer,
                _sourceHandle,
                _tempBufferHandle,
                _material,
                5);
        }

        private void DrawBloomSheet(Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.SetRenderTarget(_bloomBufferHandles[0].nameID);
            commandBuffer.SetGlobalTexture(MainTextureId, _tempBufferHandle.nameID);
            DrawWithIdentityMatrices(
                camera,
                commandBuffer,
                _textureToSheetMesh,
                _material,
                0);
        }

        private void DrawBlur(CommandBuffer commandBuffer)
        {
            Blitter.BlitCameraTexture(
                commandBuffer,
                _bloomBufferHandles[0],
                _bloomBufferHandles[1],
                _material,
                1);
            Blitter.BlitCameraTexture(
                commandBuffer,
                _bloomBufferHandles[1],
                _bloomBufferHandles[0],
                _material,
                2);
        }

        private void DrawBloomTexture(Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.SetRenderTarget(_destHandle.nameID);
            commandBuffer.SetGlobalTexture(MainTextureId, _bloomBufferHandles[0].nameID);
            commandBuffer.SetGlobalFloat(BloomIntensityId, Intensity);
            commandBuffer.SetGlobalFloat(BloomScatterId, Scatter);
            commandBuffer.SetGlobalFloat(
                BloomScatterWeightId,
                CalculateScatterWeight(Scatter));
            DrawWithIdentityMatrices(
                camera,
                commandBuffer,
                _sheetToTextureMesh,
                _material,
                4);
        }

        private static void DrawWithIdentityMatrices(
            Camera camera,
            CommandBuffer commandBuffer,
            Mesh mesh,
            Material material,
            int pass)
        {
            commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            commandBuffer.DrawMesh(mesh, Matrix4x4.identity, material, 0, pass);
            commandBuffer.SetViewProjectionMatrices(
                camera.worldToCameraMatrix,
                camera.projectionMatrix);
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_textureToSheetMesh);
            CoreUtils.Destroy(_sheetToTextureMesh);
            _textureToSheetMesh = null;
            _sheetToTextureMesh = null;
            _tempBufferHandle?.Release();
            _tempBufferHandle = null;
            for (var index = 0; index < _bloomBufferHandles.Length; index++)
            {
                _bloomBufferHandles[index]?.Release();
                _bloomBufferHandles[index] = null;
            }
            _sourceHandle = null;
            _destHandle = null;
            _material = null;
        }

        private readonly struct BloomTextureInfo
        {
            public BloomTextureInfo(int width, int height, Rect textureMap)
            {
                Width = width;
                Height = height;
                TextureMap = textureMap;
                Vertices = new[]
                {
                    new Vector2(textureMap.x, textureMap.yMax),
                    new Vector2(textureMap.xMax, textureMap.yMax),
                    new Vector2(textureMap.xMax, textureMap.y),
                    new Vector2(textureMap.x, textureMap.y),
                };
                Uvs = (Vector2[])FullscreenUvs.Clone();
                SheetVertices = new Vector2[Vertices.Length];
                SheetUvs = new Vector2[Vertices.Length];
                for (var index = 0; index < Vertices.Length; index++)
                {
                    SheetVertices[index] = Vertices[index] * 2f - Vector2.one;
                    SheetUvs[index] = Vertices[index] * 0.5f + Vector2.one * 0.5f;
                }
                Triangles = (int[])QuadTriangles.Clone();
            }

            public int Width { get; }

            public int Height { get; }

            public Rect TextureMap { get; }

            public Vector2[] Vertices { get; }

            public Vector2[] Uvs { get; }

            public Vector2[] SheetVertices { get; }

            public Vector2[] SheetUvs { get; }

            public int[] Triangles { get; }
        }
    }
}
