using AssetStudio;
using PjskBundle2Parts.Models;

namespace PjskBundle2Parts.Services;

public sealed class UnityRuntimeNativeMeshExporter
{
    public static PjskUnityRuntimeNativeMeshSet ExportSinglePart(
        string partKind,
        IImported imported,
        SpringPrefabGraph graph,
        IReadOnlyCollection<string> activeRoots
    )
    {
        var warnings = new List<string>();
        var meshes = ExportPart(partKind, imported, graph, activeRoots, warnings);
        return new PjskUnityRuntimeNativeMeshSet(
            Version: "0414",
            CoordinateSpace: "assetstudio-modelconverter-viewer-space",
            Meshes: meshes,
            Warnings: warnings
                .Distinct(StringComparer.Ordinal)
                .OrderBy(warning => warning, StringComparer.Ordinal)
                .ToList()
        );
    }

    public static PjskUnityRuntimeNativeMeshSet Export(
        IImported bodyImported,
        IImported headImported,
        PjskSpringBoneRuntimeUnitySetup runtimeUnitySetup,
        IImported? accessoryImported = null,
        string? accessoryAttachNodeName = null
    )
    {
        var warnings = new List<string>();
        var meshes = new List<PjskUnityRuntimeNativeMesh>();
        var bodyGraph = runtimeUnitySetup.PrefabGraphs
            .FirstOrDefault(graph => string.Equals(graph.PartKind, "Body", StringComparison.OrdinalIgnoreCase));
        var headGraph = runtimeUnitySetup.PrefabGraphs
            .FirstOrDefault(graph => string.Equals(graph.PartKind, "Head", StringComparison.OrdinalIgnoreCase));

        if (bodyGraph is null)
        {
            throw new InvalidDataException("Body prefab graph is missing.");
        }
        meshes.AddRange(ExportPart(
            "Body",
            bodyImported,
            bodyGraph,
            runtimeUnitySetup.ActiveRootProfile.ActiveRoots,
            warnings));

        if (headGraph is null)
        {
            throw new InvalidDataException("Head prefab graph is missing.");
        }
        meshes.AddRange(ExportPart(
            "Head",
            headImported,
            headGraph,
            runtimeUnitySetup.ActiveRootProfile.ActiveRoots,
            warnings));

        if (accessoryImported is not null)
        {
            meshes.AddRange(ExportAccessoryPart(
                accessoryImported,
                runtimeUnitySetup,
                accessoryAttachNodeName,
                warnings
            ));
        }

        return new PjskUnityRuntimeNativeMeshSet(
            Version: "0414",
            CoordinateSpace: "assetstudio-modelconverter-viewer-space",
            Meshes: meshes,
            Warnings: warnings
                .Distinct(StringComparer.Ordinal)
                .OrderBy(warning => warning, StringComparer.Ordinal)
                .ToList()
        );
    }

    private static IReadOnlyList<PjskUnityRuntimeNativeMesh> ExportAccessoryPart(
        IImported imported,
        PjskSpringBoneRuntimeUnitySetup runtimeUnitySetup,
        string? attachNodeName,
        List<string> warnings
    )
    {
        var transforms = runtimeUnitySetup.PrefabGraphs
            .SelectMany(graph => graph.Transforms)
            .Where(transform => !string.IsNullOrWhiteSpace(transform.TransformPath))
            .ToList();
        var transformPaths = transforms
            .Select(transform => transform.TransformPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var attachPath = ResolveAccessoryAttachPath(transformPaths, attachNodeName);
        if (attachPath is null)
        {
            throw new InvalidDataException(
                $"Accessory attach node '{attachNodeName ?? "<none>"}' was not found in runtime prefab transforms.");
        }

        var morphMap = BuildMorphMap(imported.MorphList);
        var consumedMorphPaths = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<PjskUnityRuntimeNativeMesh>();
        foreach (var mesh in imported.MeshList
            .Where(mesh => !string.IsNullOrWhiteSpace(mesh.Path))
            .OrderBy(mesh => mesh.Path, StringComparer.Ordinal))
        {
            var accessoryBones = ResolveExactImportedBones(mesh, transforms);
            if (!TryResolveSkinBinding(
                    mesh,
                    accessoryBones.Paths,
                    accessoryBones.PathIds,
                    out var skinBinding,
                    out var skinFailure))
            {
                throw new InvalidDataException(
                    $"Accessory mesh '{mesh.Path}' has an invalid skin binding: {skinFailure}");
            }

            var renderer = new SpringPrefabRenderer(
                PathId: -100000 - result.Count,
                TypeName: "ImportedAccessoryMesh",
                GameObjectPathId: null,
                TransformPathId: null,
                Name: Path.GetFileName(mesh.Path),
                TransformPath: attachPath,
                PoseRoot: FirstPathSegment(attachPath),
                ActiveSelf: true,
                ActiveInHierarchy: true,
                Enabled: true,
                MeshPathId: null,
                MeshName: Path.GetFileName(mesh.Path),
                SkinnedMeshBones: Array.Empty<long>(),
                RootBonePathId: null,
                MaterialFileIds: Array.Empty<long>(),
                MaterialPathIds: Array.Empty<long>()
            );

            result.Add(BuildNativeMesh(
                "Accessory",
                mesh,
                renderer,
                attachPath,
                rootBonePath: null,
                skinBinding,
                ResolveMorphTargets(mesh.Path, morphMap, consumedMorphPaths)
            ));
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("Accessory imported model has no mesh paths.");
        }
        RequireAllMorphsConsumed(morphMap, consumedMorphPaths);

        return result;
    }

    private static IReadOnlyList<PjskUnityRuntimeNativeMesh> ExportPart(
        string partKind,
        IImported imported,
        SpringPrefabGraph graph,
        IReadOnlyCollection<string> activeRoots,
        List<string> warnings
    )
    {
        var transformPathByPathId = graph.Transforms
            .Where(transform => !string.IsNullOrWhiteSpace(transform.TransformPath))
            .ToDictionary(transform => transform.PathId, transform => transform.TransformPath!, EqualityComparer<long>.Default);
        var transformPaths = transformPathByPathId.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var activeRenderers = graph.Renderers
            .Where(renderer => renderer.Enabled && IsActiveRenderer(renderer, activeRoots))
            .ToList();
        var morphMap = BuildMorphMap(imported.MorphList);
        var consumedMorphPaths = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<PjskUnityRuntimeNativeMesh>();

        var meshLookup = BuildImportedMeshLookupMap(imported.MeshList
            .Where(mesh => !string.IsNullOrWhiteSpace(mesh.Path)));

        foreach (var renderer in activeRenderers)
        {
            if (string.IsNullOrWhiteSpace(renderer.TransformPath))
            {
                throw new InvalidDataException(
                    $"{partKind} renderer {renderer.PathId} has no transform path.");
            }

            if (!TryResolveImportedMesh(renderer, meshLookup, out var mesh, out var failure))
            {
                throw new InvalidDataException(
                    $"{partKind} renderer '{renderer.TransformPath}' cannot resolve its mesh: {failure}");
            }

            var rendererBonePaths = new List<string>();
            var missingBone = false;
            foreach (var pathId in renderer.SkinnedMeshBones)
            {
                if (!transformPathByPathId.TryGetValue(pathId, out var bonePath))
                {
                    missingBone = true;
                    break;
                }
                rendererBonePaths.Add(bonePath);
            }

            if (missingBone)
            {
                throw new InvalidDataException(
                    $"{partKind} mesh '{mesh.Path}' renderer {renderer.PathId} has unresolved skinned bone PathIDs.");
            }

            var skinBones = ResolveSkinBoneReferences(
                mesh, rendererBonePaths, renderer.SkinnedMeshBones, graph.Transforms);
            if (!TryResolveSkinBinding(
                    mesh,
                    skinBones.Paths,
                    skinBones.PathIds,
                    out var skinBinding,
                    out var skinFailure))
            {
                throw new InvalidDataException(
                    $"{partKind} mesh '{mesh.Path}' has an invalid skin binding: {skinFailure}");
            }

            var rootBonePath = renderer.RootBonePathId is long rootBonePathId &&
                transformPathByPathId.TryGetValue(rootBonePathId, out var resolvedRootBonePath)
                ? resolvedRootBonePath
                : null;

            result.Add(BuildNativeMesh(
                partKind,
                mesh,
                renderer,
                renderer.TransformPath,
                rootBonePath,
                skinBinding,
                ResolveMorphTargets(mesh.Path, morphMap, consumedMorphPaths)
            ));
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException(
                $"{partKind} prefab graph has no active renderer to export.");
        }
        RequireAllMorphsConsumed(morphMap, consumedMorphPaths);
        return result;
    }

    private static bool IsActiveRenderer(
        SpringPrefabRenderer renderer,
        IReadOnlyCollection<string> activeRoots
    )
    {
        var poseRoot = renderer.PoseRoot ?? FirstPathSegment(renderer.TransformPath);
        return !string.IsNullOrWhiteSpace(poseRoot) &&
            activeRoots.Contains(poseRoot, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveAccessoryAttachPath(
        IReadOnlyList<string> transformPaths,
        string? attachNodeName
    )
    {
        if (string.IsNullOrWhiteSpace(attachNodeName))
        {
            return null;
        }

        return TryResolveTransformPathByNameOrPath(
            transformPaths,
            attachNodeName,
            out var explicitPath
        )
            ? explicitPath
            : null;
    }

    private static bool TryResolveTransformPathByNameOrPath(
        IReadOnlyList<string> transformPaths,
        string nameOrPath,
        out string path
    )
    {
        var direct = transformPaths.FirstOrDefault(candidate =>
            string.Equals(candidate, nameOrPath, StringComparison.OrdinalIgnoreCase));
        if (direct is not null)
        {
            path = direct;
            return true;
        }

        var suffix = "/" + nameOrPath;
        var bySuffix = transformPaths.FirstOrDefault(candidate =>
            candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (bySuffix is not null)
        {
            path = bySuffix;
            return true;
        }

        var byLeaf = transformPaths.FirstOrDefault(candidate =>
            string.Equals(LastPathSegment(candidate), nameOrPath, StringComparison.OrdinalIgnoreCase));
        if (byLeaf is not null)
        {
            path = byLeaf;
            return true;
        }

        path = string.Empty;
        return false;
    }

    private static bool TryResolveImportedMesh(
        SpringPrefabRenderer renderer,
        IReadOnlyDictionary<string, ImportedMesh> meshLookup,
        out ImportedMesh mesh,
        out string failure
    )
    {
        if (string.IsNullOrWhiteSpace(renderer.TransformPath))
        {
            mesh = null!;
            failure = "renderer has no exact transform path.";
            return false;
        }
        if (!meshLookup.TryGetValue(renderer.TransformPath, out mesh!))
        {
            failure = $"no imported mesh has the exact renderer transform path '{renderer.TransformPath}'.";
            return false;
        }
        failure = string.Empty;
        return true;
    }

    private sealed record NativeSkinBinding(
        IReadOnlyList<string> BonePaths,
        IReadOnlyList<long> BonePathIds,
        IReadOnlyList<float> BoneInverseBindMatrices
    );

    private static readonly NativeSkinBinding EmptySkinBinding = new(
        Array.Empty<string>(),
        Array.Empty<long>(),
        Array.Empty<float>()
    );

    private sealed record ExactBoneReferences(
        IReadOnlyList<string> Paths,
        IReadOnlyList<long> PathIds
    );

    private static bool TryResolveSkinBinding(
        ImportedMesh mesh,
        IReadOnlyList<string> rendererBonePaths,
        IReadOnlyList<long> rendererBonePathIds,
        out NativeSkinBinding binding,
        out string failure
    )
    {
        if (rendererBonePaths.Count != rendererBonePathIds.Count)
        {
            binding = EmptySkinBinding;
            failure = $"renderer has {rendererBonePaths.Count} bone paths but " +
                $"{rendererBonePathIds.Count} bone PathIDs.";
            return false;
        }
        var importedBoneCount = mesh.BoneList?.Count ?? 0;
        if (importedBoneCount != rendererBonePaths.Count)
        {
            binding = EmptySkinBinding;
            failure = $"mesh has {importedBoneCount} bindposes but renderer has " +
                $"{rendererBonePaths.Count} bone slots.";
            return false;
        }
        if (importedBoneCount == 0)
        {
            binding = new NativeSkinBinding(
                rendererBonePaths,
                rendererBonePathIds,
                Array.Empty<float>()
            );
            failure = string.Empty;
            return true;
        }

        var maxSkinBoneIndex = MaxSkinBoneIndex(mesh);
        if (maxSkinBoneIndex >= importedBoneCount ||
            maxSkinBoneIndex >= rendererBonePaths.Count)
        {
            binding = EmptySkinBinding;
            failure = $"vertex skin index {maxSkinBoneIndex} exceeds bindpose count " +
                $"{importedBoneCount} or renderer bone count {rendererBonePaths.Count}.";
            return false;
        }

        var inverseBindMatrices = new List<float>(importedBoneCount * 16);
        for (var index = 0; index < importedBoneCount; index += 1)
        {
            AddMatrix(inverseBindMatrices, mesh.BoneList![index].Matrix);
        }

        binding = new NativeSkinBinding(
            rendererBonePaths,
            rendererBonePathIds,
            inverseBindMatrices);
        failure = string.Empty;
        return true;
    }

    private static ExactBoneReferences ResolveExactImportedBones(
        ImportedMesh mesh,
        IReadOnlyList<SpringPrefabTransform> transforms)
    {
        if (mesh.BoneList is not { Count: > 0 })
        {
            return new ExactBoneReferences(Array.Empty<string>(), Array.Empty<long>());
        }
        var paths = new List<string>(mesh.BoneList.Count);
        var pathIds = new List<long>(mesh.BoneList.Count);
        foreach (var bone in mesh.BoneList)
        {
            var matches = transforms
                .Where(transform => string.Equals(
                    transform.TransformPath,
                    bone.Path,
                    StringComparison.Ordinal))
                .ToArray();
            if (string.IsNullOrWhiteSpace(bone.Path) || matches.Length != 1)
            {
                throw new InvalidDataException(
                    $"Imported mesh '{mesh.Path}' bone '{bone.Path}' resolves to " +
                    $"{matches.Length} exact runtime Transform PathIDs.");
            }
            paths.Add(bone.Path);
            pathIds.Add(matches[0].PathId);
        }
        return new ExactBoneReferences(paths, pathIds);
    }

    private static ExactBoneReferences ResolveSkinBoneReferences(
        ImportedMesh mesh,
        IReadOnlyList<string> rendererBonePaths,
        IReadOnlyList<long> rendererBonePathIds,
        IReadOnlyList<SpringPrefabTransform> transforms)
    {
        var bindPoseCount = mesh.BoneList?.Count ?? 0;
        if (bindPoseCount == 0 || bindPoseCount >= rendererBonePaths.Count ||
            rendererBonePaths.Count != rendererBonePathIds.Count)
        {
            return new ExactBoneReferences(rendererBonePaths, rendererBonePathIds);
        }

        // Some official face meshes retain fewer bindposes than renderer slots.
        // AssetStudio resolves that mesh palette through its bone-name hashes.
        // Keep every mesh bindpose, plus the complete renderer array in the prefab
        // graph, but never accept a different transform for a weighted source slot.
        var palette = ResolveExactImportedBones(mesh, transforms);
        foreach (var index in CollectUsedBoneIndices(mesh))
        {
            if (index < 0 || index >= palette.PathIds.Count ||
                palette.PathIds[index] != rendererBonePathIds[index])
            {
                throw new InvalidDataException(
                    $"Mesh '{mesh.Path}' weighted skin slot {index} does not match " +
                    "the original renderer bone PathID.");
            }
        }
        return palette;
    }

    private static int MaxSkinBoneIndex(ImportedMesh mesh)
    {
        var max = -1;
        foreach (var index in CollectUsedBoneIndices(mesh))
        {
            max = Math.Max(max, index);
        }

        return max;
    }

    private static IReadOnlySet<int> CollectUsedBoneIndices(ImportedMesh mesh)
    {
        var result = new HashSet<int>();
        foreach (var vertex in mesh.VertexList)
        {
            if (vertex.BoneIndices is null || vertex.Weights is null)
            {
                result.Add(0);
                continue;
            }

            for (var index = 0; index < Math.Min(vertex.BoneIndices.Length, vertex.Weights.Length); index += 1)
            {
                if (vertex.Weights[index] <= 0)
                {
                    continue;
                }
                result.Add(vertex.BoneIndices[index]);
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ImportedMesh> BuildImportedMeshLookupMap(
        IEnumerable<ImportedMesh> meshes
    )
    {
        var result = new Dictionary<string, ImportedMesh>(StringComparer.Ordinal);
        foreach (var mesh in meshes)
        {
            if (string.IsNullOrWhiteSpace(mesh.Path) || !result.TryAdd(mesh.Path, mesh))
            {
                throw new InvalidDataException(
                    $"Imported model has an empty or duplicate mesh path '{mesh.Path}'.");
            }
        }
        return result;
    }

    private static PjskUnityRuntimeNativeMesh BuildNativeMesh(
        string partKind,
        ImportedMesh mesh,
        SpringPrefabRenderer renderer,
        string rendererTransformPath,
        string? rootBonePath,
        NativeSkinBinding skinBinding,
        IReadOnlyList<ImportedMorph> morphs
    )
    {
        var buffers = BuildNativeVertexBuffers(mesh);
        var submeshes = BuildNativeSubmeshes(partKind, mesh, renderer);
        return new PjskUnityRuntimeNativeMesh(
            PartKind: partKind,
            MeshPath: mesh.Path,
            MeshName: Path.GetFileName(mesh.Path),
            RendererPathId: renderer.PathId,
            RendererTransformPathId: renderer.TransformPathId,
            RendererTransformPath: rendererTransformPath,
            RootBonePathId: renderer.RootBonePathId,
            RootBonePath: rootBonePath,
            BonePathIds: skinBinding.BonePathIds,
            BonePaths: skinBinding.BonePaths,
            BoneInverseBindMatrices: skinBinding.BoneInverseBindMatrices,
            Submeshes: submeshes,
            Positions: buffers.Positions,
            Normals: buffers.Normals,
            Tangents: buffers.Tangents,
            Uv0: buffers.Uv0,
            Uv1: buffers.Uv1,
            Uv2: buffers.Uv2,
            Colors: buffers.Colors,
            SkinIndices: buffers.SkinIndices,
            SkinWeights: buffers.SkinWeights,
            MorphTargets: BuildMorphTargets(mesh, morphs)
        );
    }

    private sealed record NativeVertexBuffers(
        IReadOnlyList<float> Positions, IReadOnlyList<float> Normals,
        IReadOnlyList<float> Tangents, IReadOnlyList<float> Uv0,
        IReadOnlyList<float> Uv1, IReadOnlyList<float> Uv2,
        IReadOnlyList<float> Colors, IReadOnlyList<ushort> SkinIndices,
        IReadOnlyList<float> SkinWeights);

    private static NativeVertexBuffers BuildNativeVertexBuffers(ImportedMesh mesh)
    {
        var positions = new List<float>(mesh.VertexList.Count * 3);
        var normals = new List<float>(mesh.VertexList.Count * 3);
        var tangents = new List<float>(mesh.VertexList.Count * 4);
        var uv0 = new List<float>(mesh.VertexList.Count * 2);
        var uv1 = new List<float>(mesh.VertexList.Count * 2);
        var uv2 = new List<float>(mesh.VertexList.Count * 2);
        var colors = new List<float>(mesh.VertexList.Count * 4);
        var skinIndices = new List<ushort>(mesh.VertexList.Count * 4);
        var skinWeights = new List<float>(mesh.VertexList.Count * 4);
        var hasUv0 = mesh.VertexList.Count > 0 && mesh.VertexList.All(vertex => HasUv(vertex, 0));
        var hasUv1 = mesh.VertexList.Count > 0 && mesh.VertexList.All(vertex => HasUv(vertex, 1));
        var hasUv2 = mesh.VertexList.Count > 0 && mesh.VertexList.All(vertex => HasUv(vertex, 2));
        var hasAnySkin = mesh.VertexList.Any(vertex =>
            vertex.BoneIndices is not null || vertex.Weights is not null);
        var hasCompleteSkin = mesh.VertexList.All(vertex =>
            vertex.BoneIndices is { Length: 4 } && vertex.Weights is { Length: 4 });
        if (hasAnySkin != hasCompleteSkin)
        {
            throw new InvalidDataException(
                $"Mesh '{mesh.Path}' has mixed or incomplete four-weight skin data.");
        }

        foreach (var vertex in mesh.VertexList)
        {
            if (!IsFinite(vertex.Vertex) ||
                (mesh.hasNormal && !IsFinite(vertex.Normal)) ||
                (mesh.hasTangent && !IsFinite(vertex.Tangent)))
            {
                throw new InvalidDataException(
                    $"Mesh '{mesh.Path}' has non-finite vertex data.");
            }
            AddVector3(positions, vertex.Vertex);
            if (mesh.hasNormal)
            {
                AddVector3(normals, vertex.Normal);
            }
            if (mesh.hasTangent)
            {
                AddTangent(tangents, vertex.Tangent);
            }
            if (hasUv0)
            {
                AddUv(uv0, vertex, 0);
            }
            if (hasUv1)
            {
                AddUv(uv1, vertex, 1);
            }
            if (hasUv2)
            {
                AddUv(uv2, vertex, 2);
            }
            if (mesh.hasColor)
            {
                colors.Add(vertex.Color.R);
                colors.Add(vertex.Color.G);
                colors.Add(vertex.Color.B);
                colors.Add(vertex.Color.A);
            }
            if (hasCompleteSkin)
            {
                AddSkin(vertex, skinIndices, skinWeights);
            }
        }

        return new NativeVertexBuffers(
            positions, normals, tangents, uv0, uv1, uv2, colors, skinIndices, skinWeights);
    }

    private static List<PjskUnityRuntimeNativeSubmesh> BuildNativeSubmeshes(
        string partKind, ImportedMesh mesh, SpringPrefabRenderer renderer)
    {
        var indexCursor = 0;
        var submeshes = new List<PjskUnityRuntimeNativeSubmesh>();
        foreach (var submesh in mesh.SubmeshList.Select((value, slotIndex) => new { value, slotIndex }))
        {
            var indices = new List<int>(submesh.value.FaceList.Count * 3);
            foreach (var face in submesh.value.FaceList)
            {
                indices.Add(submesh.value.BaseVertex + face.VertexIndices[0]);
                indices.Add(submesh.value.BaseVertex + face.VertexIndices[1]);
                indices.Add(submesh.value.BaseVertex + face.VertexIndices[2]);
            }
            var materialPathId = submesh.slotIndex < renderer.MaterialPathIds.Count
                ? renderer.MaterialPathIds[submesh.slotIndex]
                : throw new InvalidOperationException(
                    $"Renderer {renderer.PathId} submesh {submesh.slotIndex} has no material path id."
                );
            var materialFileId = submesh.slotIndex < renderer.MaterialFileIds.Count
                ? renderer.MaterialFileIds[submesh.slotIndex]
                : throw new InvalidOperationException(
                    $"Renderer {renderer.PathId} submesh {submesh.slotIndex} has no material file id."
                );
            var materialIdentity = RuntimeMaterialIdentityResolver.Resolve(
                partKind,
                submesh.slotIndex,
                materialFileId,
                materialPathId,
                submesh.value.Material
            );
            submeshes.Add(new PjskUnityRuntimeNativeSubmesh(
                SlotIndex: submesh.slotIndex,
                MaterialKey: materialIdentity.MaterialKey,
                MaterialFileId: materialIdentity.MaterialFileId,
                MaterialPathId: materialIdentity.MaterialPathId,
                MaterialName: submesh.value.Material,
                Start: indexCursor,
                Count: indices.Count,
                Indices: indices
            ));
            indexCursor += indices.Count;
        }

        return submeshes;
    }

    private static IReadOnlyList<float> BuildBoneInverseBindMatrices(ImportedMesh mesh)
    {
        if (mesh.BoneList is not { Count: > 0 })
        {
            return Array.Empty<float>();
        }

        var result = new List<float>(mesh.BoneList.Count * 16);
        foreach (var bone in mesh.BoneList)
        {
            AddMatrix(result, bone.Matrix);
        }

        return result;
    }

    private static void AddMatrix(List<float> values, Matrix4x4 matrix)
    {
        values.Add(matrix.M00);
        values.Add(matrix.M01);
        values.Add(matrix.M02);
        values.Add(matrix.M03);
        values.Add(matrix.M10);
        values.Add(matrix.M11);
        values.Add(matrix.M12);
        values.Add(matrix.M13);
        values.Add(matrix.M20);
        values.Add(matrix.M21);
        values.Add(matrix.M22);
        values.Add(matrix.M23);
        values.Add(matrix.M30);
        values.Add(matrix.M31);
        values.Add(matrix.M32);
        values.Add(matrix.M33);
    }

    private static IReadOnlyList<PjskUnityRuntimeNativeMorphTarget> BuildMorphTargets(
        ImportedMesh mesh,
        IReadOnlyList<ImportedMorph> morphs
    )
    {
        if (morphs.Count == 0)
        {
            return Array.Empty<PjskUnityRuntimeNativeMorphTarget>();
        }

        var result = new List<PjskUnityRuntimeNativeMorphTarget>();
        var targetNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var morph in morphs)
        {
            foreach (var channel in morph.Channels)
            {
                if (string.IsNullOrWhiteSpace(channel.Name) || !targetNames.Add(channel.Name))
                {
                    throw new InvalidDataException(
                        $"Mesh '{mesh.Path}' has an empty or duplicate morph target name '{channel.Name}'.");
                }
                result.Add(BuildMorphTarget(mesh, channel));
            }
        }

        return result;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsFinite(Vector4 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);

    private static PjskUnityRuntimeNativeMorphTarget BuildMorphTarget(
        ImportedMesh mesh, ImportedMorphChannel channel)
    {
        if (channel.KeyframeList.Count != 1)
        {
            throw new InvalidDataException(
                $"Morph target '{channel.Name}' on mesh '{mesh.Path}' has " +
                $"{channel.KeyframeList.Count} keyframes; multi-keyframe interpolation " +
                "semantics are not implemented without loss.");
        }

        var keyframe = channel.KeyframeList[0];
        if (keyframe.VertexList.Count == 0)
        {
            throw new InvalidDataException(
                $"Morph target '{channel.Name}' on mesh '{mesh.Path}' has no vertices.");
        }
        if (keyframe.hasNormals && !mesh.hasNormal)
        {
            throw new InvalidDataException(
                $"Morph target '{channel.Name}' has normal deltas but mesh '{mesh.Path}' has no base normals.");
        }
        if (keyframe.hasTangents && !mesh.hasTangent)
        {
            throw new InvalidDataException(
                $"Morph target '{channel.Name}' has tangent deltas but mesh '{mesh.Path}' has no base tangents.");
        }

        var indices = new List<int>(keyframe.VertexList.Count);
        var positionDeltas = new List<float>(keyframe.VertexList.Count * 3);
        var normalDeltas = new List<float>(
            keyframe.hasNormals ? keyframe.VertexList.Count * 3 : 0);
        var tangentDeltas = new List<float>(
            keyframe.hasTangents ? keyframe.VertexList.Count * 3 : 0);
        var seenIndices = new HashSet<int>();
        foreach (var morphVertex in keyframe.VertexList)
        {
            if (morphVertex.Index >= (uint)mesh.VertexList.Count)
            {
                throw new InvalidDataException(
                    $"Morph target '{channel.Name}' on mesh '{mesh.Path}' references " +
                    $"invalid vertex index {morphVertex.Index} for {mesh.VertexList.Count} vertices.");
            }
            var index = (int)morphVertex.Index;
            if (!seenIndices.Add(index))
            {
                throw new InvalidDataException(
                    $"Morph target '{channel.Name}' on mesh '{mesh.Path}' repeats vertex index {index}.");
            }

            var positionDelta = morphVertex.Vertex.Vertex - mesh.VertexList[index].Vertex;
            if (!IsFinite(positionDelta) ||
                (keyframe.hasNormals && !IsFinite(morphVertex.Vertex.Normal)) ||
                (keyframe.hasTangents && !IsFinite(morphVertex.Vertex.Tangent)))
            {
                throw new InvalidDataException(
                    $"Morph target '{channel.Name}' on mesh '{mesh.Path}' contains non-finite deltas at vertex index {index}.");
            }

            indices.Add(index);
            AddVector3(positionDeltas, positionDelta);
            if (keyframe.hasNormals)
            {
                AddVector3(normalDeltas, morphVertex.Vertex.Normal);
            }
            if (keyframe.hasTangents)
            {
                AddVector3(tangentDeltas, new Vector3(
                    morphVertex.Vertex.Tangent.X,
                    morphVertex.Vertex.Tangent.Y,
                    morphVertex.Vertex.Tangent.Z));
            }
        }

        return new PjskUnityRuntimeNativeMorphTarget(
            Name: channel.Name,
            Indices: indices,
            HasPositionDeltas: true,
            PositionDeltas: positionDeltas,
            HasNormalDeltas: keyframe.hasNormals,
            NormalDeltas: normalDeltas,
            HasTangentDeltas: keyframe.hasTangents,
            TangentDeltas: tangentDeltas
        );
    }

    private static IReadOnlyDictionary<string, ImportedMorph> BuildMorphMap(
        IReadOnlyList<ImportedMorph> morphList
    )
    {
        var result = new Dictionary<string, ImportedMorph>(StringComparer.Ordinal);
        foreach (var morph in morphList)
        {
            if (string.IsNullOrWhiteSpace(morph.Path) || !result.TryAdd(morph.Path, morph))
            {
                throw new InvalidDataException(
                    $"Imported model has an empty or duplicate morph path '{morph.Path}'.");
            }
        }
        return result;
    }

    private static IReadOnlyList<ImportedMorph> ResolveMorphTargets(
        string meshPath,
        IReadOnlyDictionary<string, ImportedMorph> morphMap,
        ISet<string> consumedPaths
    )
    {
        if (morphMap.TryGetValue(meshPath, out var morph))
        {
            consumedPaths.Add(meshPath);
            return new[] { morph };
        }
        return Array.Empty<ImportedMorph>();
    }

    private static void RequireAllMorphsConsumed(
        IReadOnlyDictionary<string, ImportedMorph> morphMap,
        IReadOnlySet<string> consumedPaths)
    {
        var missing = morphMap.Keys
            .Where(path => !consumedPaths.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidDataException(
                $"Imported model has {missing.Length} morph path(s) without an exact exported mesh: " +
                string.Join(", ", missing.Take(8)));
        }
    }

    private static void AddVector3(List<float> values, Vector3 vector)
    {
        values.Add(vector.X);
        values.Add(vector.Y);
        values.Add(vector.Z);
    }

    private static void AddTangent(List<float> values, Vector4 tangent)
    {
        values.Add(tangent.X);
        values.Add(tangent.Y);
        values.Add(tangent.Z);
        values.Add(-tangent.W);
    }

    private static void AddUv(List<float> values, ImportedVertex vertex, int channel)
    {
        if (vertex.UV is { Length: > 0 } &&
            channel < vertex.UV.Length &&
            vertex.UV[channel] is { Length: >= 2 })
        {
            values.Add(vertex.UV[channel][0]);
            values.Add(vertex.UV[channel][1]);
            return;
        }

        values.Add(0);
        values.Add(0);
    }

    private static bool HasUv(ImportedVertex vertex, int channel)
    {
        return vertex.UV is { Length: > 0 } &&
            channel < vertex.UV.Length &&
            vertex.UV[channel] is { Length: >= 2 };
    }

    private static void AddSkin(
        ImportedVertex vertex,
        List<ushort> skinIndices,
        List<float> skinWeights
    )
    {
        if (vertex.BoneIndices is not { Length: 4 } ||
            vertex.Weights is not { Length: 4 })
        {
            throw new InvalidDataException(
                "Native mesh skin data does not contain exactly four source influences.");
        }
        for (var index = 0; index < 4; index += 1)
        {
            var sourceIndex = vertex.BoneIndices[index];
            var sourceWeight = vertex.Weights[index];
            if (sourceIndex < 0 || sourceIndex > ushort.MaxValue || !float.IsFinite(sourceWeight))
            {
                throw new InvalidDataException(
                    $"Native mesh skin influence {index} is outside its exact runtime representation.");
            }
            skinIndices.Add((ushort)sourceIndex);
            skinWeights.Add(sourceWeight);
        }
    }

    private static string? FirstPathSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        var slash = path.IndexOf('/');
        return slash < 0 ? path : path[..slash];
    }

    private static string? DropFirstPathSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var slash = path.IndexOf('/');
        return slash < 0 || slash + 1 >= path.Length
            ? path
            : path[(slash + 1)..];
    }

    private static string? LastPathSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var slash = path.LastIndexOf('/');
        return slash < 0 || slash + 1 >= path.Length
            ? path
            : path[(slash + 1)..];
    }
}
