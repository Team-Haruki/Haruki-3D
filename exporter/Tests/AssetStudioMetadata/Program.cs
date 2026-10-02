using AssetStudio;
using System.Text.Json;

static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
var options = new JsonSerializerOptions { IncludeFields = true };
options.Converters.Add(new JsonConverterHelper.KVPConverter());
var materialJson = JsonSerializer.Deserialize<Material>("""
{"m_StringTagMap":{"RenderType":"Transparent"},"m_DisabledShaderPasses":["ShadowCaster"],"m_DoubleSidedGI":true}
""", options)!;
Require(materialJson.m_StringTagMap.Count == 1 && materialJson.m_StringTagMap.Single().Value == "Transparent", "Serialized material tags were lost");
Require(materialJson.m_DisabledShaderPasses.SequenceEqual(new[] { "ShadowCaster" }) && materialJson.m_DoubleSidedGI, "Serialized material flags were lost");
var wrap = JsonSerializer.Deserialize<GLTextureSettings>("{\"m_WrapU\":2,\"m_WrapV\":1,\"m_WrapW\":0}", options)!;
Require(wrap.m_WrapMode == 2 && wrap.m_WrapV == 1 && wrap.m_WrapW == 0, "Independent texture axes were lost");
// Exercise the binding result, including duplicate and unused renderer slots.
var bind = typeof(PjskBundle2Parts.Services.UnityRuntimeNativeMeshExporter).GetMethod(
    "TryResolveSkinBinding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
var paths = new[] { "Root/Thigh", "Root/Thigh", "Root/Knee" };
var ids = new long[] { 17, 17, 29 };
var mesh = new ImportedMesh {
    BoneList = paths.Select((path, index) => new ImportedBone {
        Path = path, Matrix = new Matrix4x4(new float[] { index + 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 })
    }).ToList(),
    VertexList = new List<ImportedVertex> {
        new() { BoneIndices = new[] { 0 }, Weights = new[] { 1f } }
    }
};
object?[] bindingArgs = { mesh, paths, ids, null, null };
Require((bool)bind.Invoke(null, bindingArgs)!, "Exact renderer binding was rejected");
var binding = bindingArgs[3]!;
T Read<T>(string name) => (T)binding.GetType().GetProperty(name)!.GetValue(binding)!;
Require(Read<IReadOnlyList<string>>("BonePaths").SequenceEqual(paths), "Repeated or unused bone paths changed");
Require(Read<IReadOnlyList<long>>("BonePathIds").SequenceEqual(ids), "Renderer PathID order changed");
var matrices = Read<IReadOnlyList<float>>("BoneInverseBindMatrices");
Require(matrices.Count == 48 && matrices[0] == 1 && matrices[16] == 2 && matrices[32] == 3,
    "Bind poses were compacted or reordered");
mesh.VertexList[0].BoneIndices[0] = 3;
Require(!(bool)bind.Invoke(null, bindingArgs)!, "Out-of-range skin index was accepted");
var incompleteMesh = new ImportedMesh {
    BoneList = mesh.BoneList.Take(1).ToList(),
    VertexList = new() { new() { BoneIndices = new[] { 0 }, Weights = new[] { 1f } } }
};
object?[] incompleteArgs = { incompleteMesh, paths.Take(2).ToArray(), ids.Take(2).ToArray(), null, null };
Require(!(bool)bind.Invoke(null, incompleteArgs)!, "One bindpose was accepted for two renderer bone slots");
incompleteMesh.BoneList.Clear();
Require(!(bool)bind.Invoke(null, incompleteArgs)!, "Missing bindposes were accepted for nonempty renderer bones");
object?[] mismatchedIdsArgs = { mesh, paths, ids.Take(2).ToArray(), null, null };
Require(!(bool)bind.Invoke(null, mismatchedIdsArgs)!, "Mismatched renderer bone paths and PathIDs were accepted");
var resolvePalette = typeof(PjskBundle2Parts.Services.UnityRuntimeNativeMeshExporter).GetMethod(
    "ResolveSkinBoneReferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
var paletteMesh = new ImportedMesh {
    Path = "Root/Face",
    BoneList = new() { mesh.BoneList[0], mesh.BoneList[2] },
    VertexList = new() { new() { BoneIndices = new[] { 0 }, Weights = new[] { 1f } } }
};
var paletteTransforms = new[] {
    new PjskBundle2Parts.Models.SpringPrefabTransform(17, null, "Thigh", "Root/Thigh", "Root", null,
        Array.Empty<long>(), new(0, 0, 0), new(0, 0, 0, 1), new(1, 1, 1)),
    new PjskBundle2Parts.Models.SpringPrefabTransform(29, null, "Knee", "Root/Knee", "Root", null,
        Array.Empty<long>(), new(0, 0, 0), new(0, 0, 0, 1), new(1, 1, 1)),
};
object?[] paletteArgs = { paletteMesh, paths, ids, paletteTransforms };
var palette = resolvePalette.Invoke(null, paletteArgs)!;
var palettePaths = (IReadOnlyList<string>)palette.GetType().GetProperty("Paths")!.GetValue(palette)!;
var paletteIds = (IReadOnlyList<long>)palette.GetType().GetProperty("PathIds")!.GetValue(palette)!;
object?[] paletteBindingArgs = { paletteMesh, palettePaths, paletteIds, null, null };
Require((bool)bind.Invoke(null, paletteBindingArgs)!, "Exact mesh palette with unused reordered renderer slots was rejected");
Require(paletteIds.SequenceEqual(new long[] { 17, 29 }) && ids.Length == 3,
    "Mesh bindpose palette or complete original renderer slots changed");
paletteMesh.VertexList[0].BoneIndices[0] = 1;
var rejectedWeightedReorder = false;
try { resolvePalette.Invoke(null, paletteArgs); }
catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidDataException)
{ rejectedWeightedReorder = true; }
Require(rejectedWeightedReorder, "A weighted slot was rebound to a different renderer transform");
var buildMorph = typeof(PjskBundle2Parts.Services.UnityRuntimeNativeMeshExporter).GetMethod(
    "BuildMorphTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
mesh.Path = "Root/Face";
mesh.hasNormal = true;
mesh.hasTangent = true;
mesh.VertexList[0].Vertex = new Vector3(1, 2, 3);
var frame = new ImportedMorphKeyframe {
    hasNormals = true, hasTangents = true,
    VertexList = new() { new() { Index = 0, Vertex = new ImportedVertex {
        Vertex = new Vector3(2, 4, 6), Normal = new Vector3(4, 5, 6),
        Tangent = new Vector4(7, 8, 9, 0)
    } } }
};
var channel = new ImportedMorphChannel { Name = "Smile", KeyframeList = new() { frame } };
var morph = (PjskBundle2Parts.Models.PjskUnityRuntimeNativeMorphTarget)buildMorph.Invoke(null, new object[] { mesh, channel })!;
Require(morph.HasPositionDeltas && morph.HasNormalDeltas && morph.HasTangentDeltas,
    "Explicit morph channel presence was lost");
Require(morph.PositionDeltas.SequenceEqual(new float[] { 1, 2, 3 }) &&
    morph.NormalDeltas.SequenceEqual(new float[] { 4, 5, 6 }) &&
    morph.TangentDeltas.SequenceEqual(new float[] { 7, 8, 9 }), "Morph deltas changed");
channel.KeyframeList.Add(frame);
var rejectedInterpolation = false;
try { buildMorph.Invoke(null, new object[] { mesh, channel }); }
catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidDataException)
{ rejectedInterpolation = true; }
Require(rejectedInterpolation, "Unsupported multi-keyframe morph was silently flattened");
// An old delta must be rebuilt before any cached geometry can be reused.
var readCore = typeof(PjskBundle2Parts.Services.PartPackageExporter).GetMethod(
    "TryReadSafeCorePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
using var oldDelta = JsonDocument.Parse("""{"version":"0415-part-delta-3","corePath":"parts/core.msgpack.br"}""");
object?[] cacheArgs = { oldDelta.RootElement, Path.GetTempPath(), null };
Require(!(bool)readCore.Invoke(null, cacheArgs)!, "Old morph schema was eligible for incremental reuse");
var cacheSchema = typeof(PjskBundle2Parts.Services.CompiledPartCache).GetField(
    "Schema", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
Require((string)cacheSchema.GetRawConstantValue()! == "0415-compiled-part-10",
    "Compiled geometry cache must invalidate packages without explicit morph channels");
Console.WriteLine("Synthetic metadata and complete skin-binding checks passed.");
if (args.SequenceEqual(new[] { "--synthetic-only" })) return;
Require(args.Length > 0, "Pass original bundle paths to compare typed readers with the TypeTree reader");
var checkedObjects = 0;
foreach (var path in args)
{
    var manager = new AssetsManager { LoadViaTypeTree = false };
    manager.Options.CustomUnityVersion = new UnityVersion("2022.3.62f2");
    manager.SetAssetFilter(ClassIDType.Material, ClassIDType.Texture2D);
    manager.LoadFilesAndFolders(path);
    var objects = manager.AssetsFileList.SelectMany(file => file.Objects).ToArray();
    Require(objects.OfType<Material>().Any(), "No materials loaded");
    Require(objects.OfType<Texture2D>().Any(), "No textures loaded");
    foreach (var obj in objects)
    {
        using var tree = obj.ToJsonDoc() ?? throw new InvalidDataException("TypeTree inspection returned no document");
        var raw = tree.RootElement;
        if (obj is Texture2D texture)
        {
            Require(texture.m_ColorSpace == raw.GetProperty("m_ColorSpace").GetInt32(), $"Color space: {texture.m_Name}");
            var settings = raw.GetProperty("m_TextureSettings");
            Require(texture.m_TextureSettings.m_WrapU == settings.GetProperty("m_WrapU").GetInt32(), "Wrap U");
            Require(texture.m_TextureSettings.m_WrapV == settings.GetProperty("m_WrapV").GetInt32(), "Wrap V");
            Require(texture.m_TextureSettings.m_WrapW == settings.GetProperty("m_WrapW").GetInt32(), "Wrap W");
        }
        else if (obj is Material material)
        {
            static string[] Strings(JsonElement element) => element.EnumerateArray().Select(value => value.GetString()!).ToArray();
            Require(material.m_ValidKeywords.SequenceEqual(Strings(raw.GetProperty("m_ValidKeywords"))), "Valid keywords");
            Require(material.m_InvalidKeywords.SequenceEqual(Strings(raw.GetProperty("m_InvalidKeywords"))), "Invalid keywords");
            Require(material.m_LightmapFlags == raw.GetProperty("m_LightmapFlags").GetUInt32(), "Lightmap flags");
            Require(material.m_EnableInstancingVariants == raw.GetProperty("m_EnableInstancingVariants").GetBoolean(), "Instancing");
            Require(material.m_DoubleSidedGI == raw.GetProperty("m_DoubleSidedGI").GetBoolean(), "Double-sided GI");
            Require(material.m_CustomRenderQueue == raw.GetProperty("m_CustomRenderQueue").GetInt32(), "Render queue");
            Require(material.m_DisabledShaderPasses.SequenceEqual(Strings(raw.GetProperty("disabledShaderPasses"))), "Disabled passes");
            var tags = raw.GetProperty("stringTagMap").EnumerateArray().Select(row => new KeyValuePair<string,string>(row.GetProperty("Key").GetString()!,row.GetProperty("Value").GetString()!));
            Require(material.m_StringTagMap.SequenceEqual(tags), "String tags");
        }
        checkedObjects++;
    }
    manager.Clear();
}
Console.WriteLine($"Verified {checkedObjects} texture/material reads from binary readers against TypeTree metadata.");
