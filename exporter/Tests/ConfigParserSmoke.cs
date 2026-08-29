using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.IO.Compression;
using System.Diagnostics;
using PjskBundle2Parts.Tests;
using PjskBundle2Parts.Models;
using PjskBundle2Parts.Services;

if (args is ["--compiled-cache-copy-race-worker", var sourcePath, var targetPath, var workerStartGate])
{
    while (!File.Exists(workerStartGate))
    {
        Thread.Sleep(1);
    }
    try
    {
        for (var index = 0; index < 32; index++)
        {
            ContentAddressedFile.Replace(targetPath, temporaryPath => File.Copy(sourcePath, temporaryPath));
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 2;
    }
    return;
}

var tempDir = Path.Combine(Path.GetTempPath(), $"haruki-exporter-config-test-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempDir);
var compactMasterDir = Path.Combine(tempDir, "compact-master");
Directory.CreateDirectory(compactMasterDir);
WriteJsonFile(Path.Combine(compactMasterDir, "compactCostume3dModels.json"), new Dictionary<string, object?>
{
    ["__ENUM__"] = new Dictionary<string, string[]>
    {
        ["unit"] = ["piapro", "light_sound"],
        ["headCostume3dAssetbundleType"] = ["head_and_hair", "head_only"],
    },
    ["costume3dId"] = new[] { 101, 102 },
    ["unit"] = new object?[] { 1, 0 },
    ["assetbundleName"] = new object?[] { "01/0101", null },
    ["headCostume3dAssetbundleType"] = new object?[] { 0, 1 },
    ["colorAssetbundleName"] = new object?[] { null, "01" },
    ["part"] = new object?[] { null, "a03" },
    ["thumbnailAssetbundleName"] = new object?[] { "thumb-101", null },
});
var compactModels = MasterDataReader.ReadCostume3dModels(compactMasterDir);
Expect(
    compactModels.Count == 2 &&
    compactModels[0].Unit == "light_sound" &&
    compactModels[0].HeadCostume3dAssetbundleType == "head_and_hair" &&
    compactModels[1].Unit == "piapro" &&
    compactModels[1].Part == "a03",
    "compact costume3dModels columns and enums expand to normal model rows"
);
var splitCostumeMasterDir = Path.Combine(tempDir, "split-costume-master");
Directory.CreateDirectory(splitCostumeMasterDir);
WriteJsonFile(Path.Combine(splitCostumeMasterDir, "costume3ds.json"), new object[]
{
    new
    {
        id = 1002,
        costume3dGroupId = 1001,
        partType = "body",
        colorId = 1,
    },
    new
    {
        id = 1,
        costume3dGroupId = 1,
        partType = "head",
        characterId = 1,
        colorId = 1,
        colorName = "original",
        name = "default head",
        costume3dType = "default",
    },
});
WriteJsonFile(Path.Combine(splitCostumeMasterDir, "costume3dGroups.json"), new[]
{
    new
    {
        groupId = 1001,
        characterId = 1,
        name = "school rock",
        rarity = "normal",
        howToObtain = "shop",
    },
});
WriteJsonFile(Path.Combine(splitCostumeMasterDir, "costume3dColors.json"), new[]
{
    new { id = 1, name = "original" },
});
var splitCostumes = MasterDataReader.ReadCostume3ds(splitCostumeMasterDir);
Expect(
    splitCostumes[0].CharacterId == 1 &&
    splitCostumes[0].Name == "school rock" &&
    splitCostumes[0].ColorName == "original" &&
    splitCostumes[0].Costume3dType == "normal" &&
    splitCostumes[0].Costume3dRarity == "normal" &&
    splitCostumes[0].HowToObtain == "shop" &&
    splitCostumes[1].Name == "default head" &&
    splitCostumes[1].Costume3dType == "default",
    "split Nuverse costume tables normalize to the monolithic costume model"
);
var dependencyAssetRoot = Path.Combine(tempDir, "dependency-assets");
var commonDependencyPath = Path.Combine(
    dependencyAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "face",
    "common.bundle"
);
var shaderDependencyPath = Path.Combine(dependencyAssetRoot, "shader", "live.bundle");
Directory.CreateDirectory(Path.GetDirectoryName(commonDependencyPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(shaderDependencyPath)!);
File.WriteAllText(commonDependencyPath, "common");
File.WriteAllText(shaderDependencyPath, "shader");
var dependencyIndexPath = Path.Combine(tempDir, "bundle-dependencies.json");
File.WriteAllText(dependencyIndexPath, JsonSerializer.Serialize(new Dictionary<string, string[]>
{
    ["live_pv/model/characterv2/face/01/0001"] =
    [
        "live_pv/model/characterv2/face/common",
        "shader/live",
        "missing/dependency",
    ],
}));
var dependencyIndex = new BundleDependencyIndex(dependencyIndexPath);
var resolvedDependencyPaths = dependencyIndex.ResolveExistingBundlePaths(
    dependencyAssetRoot,
    "live_pv/model/characterv2/face/01/0001"
);
Expect(
    resolvedDependencyPaths.SequenceEqual(
        new[] { commonDependencyPath, shaderDependencyPath },
        StringComparer.Ordinal
    ),
    "bundle dependency index resolves existing dependency bundles under asset root"
);
var configPath = Path.Combine(tempDir, "exporter.config.json");
File.WriteAllText(configPath, JsonSerializer.Serialize(new
{
    master = "/data/master",
    assetRoot = "/data/assets",
    output = "/data/out-from-config",
    emitCostumeRegistries = true,
    emitPartPackages = true,
    emitRoleRuntimes = true,
    partCostume3dId = 2,
    partType = "Body",
    partUnit = "light_sound",
    roleCharacter3dIds = new[] { 5, 7 },
    manifest = "/data/manifest-from-config.json",
    assetStudioLogLevel = "info",
    compactTextures = true,
    optimizeTextureStore = false,
    sharedContentStore = "/data/shared-cas-from-config",
    compiledContentStore = "/data/compiled-cas-from-config",
    pngOptimize = "off",
    textureFormat = "ktx2",
    textureCompactWorkers = 2,
    convertModelTextures = true
}));

var parsed = ConversionOptionsParser.Parse(new[]
{
    "--config", configPath,
    "--out", "/data/out-from-cli",
    "--part-type", "head_optional",
    "--role-character3d-id", "9",
    "--manifest", "/data/manifest-from-cli.json",
    "--shared-content-store", "/data/shared-cas-from-cli",
    "--compiled-content-store", "/data/compiled-cas-from-cli",
    "--part-package-work-list", "/data/work-list.json",
    "--bundle-hash-index", "/data/bundle-hashes.json"
});

if (!parsed.IsSuccess || parsed.Options is null)
{
    throw new Exception(parsed.ErrorMessage);
}

var options = parsed.Options;
Expect(options.MasterDirectory == "/data/master", "master comes from config");
Expect(options.AssetRoot == "/data/assets", "asset root comes from config");
Expect(options.OutputDirectory == "/data/out-from-cli", "CLI output overrides config");
Expect(options.EmitCostumeRegistries, "emit registries comes from config");
Expect(options.EmitPartPackages, "emit part packages comes from config");
Expect(options.EmitRoleRuntimes, "emit role runtimes comes from config");
Expect(options.PartCostume3dId == 2, "part costume id comes from config");
Expect(options.PartType == "head_optional", "CLI part type overrides and normalizes config");
Expect(options.PartUnit == "light_sound", "part unit comes from config");
Expect(options.RoleCharacter3dIds.SequenceEqual(new[] { 5, 7, 9 }), "role character3d ids merge config and CLI");
Expect(options.ManifestPath == "/data/manifest-from-cli.json", "CLI manifest overrides config");
Expect(options.PartPackageProcessConcurrency == 1, "part package process concurrency defaults to single process");
Expect(options.PartPackageShardCount == 1, "part package shard count defaults to one");
Expect(options.PartPackageShardIndex == 0, "part package shard index defaults to zero");
Expect(options.AssetStudioLogLevel == "info", "assetstudio log level comes from config");
Expect(options.CompactTextures, "texture compaction comes from config");
Expect(!options.OptimizeTextureStore, "standalone texture optimization comes from config");
Expect(options.SharedContentStore == "/data/shared-cas-from-cli", "CLI shared content store overrides config");
Expect(options.CompiledContentStore == "/data/compiled-cas-from-cli", "CLI compiled content store parses");
Expect(options.PngOptimizeMode == "off", "PNG optimization mode comes from config");
Expect(options.TextureFormat == "ktx2", "texture format comes from config");
Expect(options.TextureCompactWorkers == 2, "texture compaction worker count comes from config");
Expect(options.ConvertModelTextures, "model texture conversion comes from config");
Expect(options.PartPackageWorkList == "/data/work-list.json", "part package work list parses");
Expect(!options.OwnsOutputFinalization, "part package work-list workers do not own output finalization");
Expect(options.BundleHashIndex == "/data/bundle-hashes.json", "bundle hash index parses");

var catalogOnly = ConversionOptionsParser.Parse(new[]
{
    "--emit-runtime-role-catalog",
    "--master", "/data/master",
    "--out", "/data/runtime",
});
Expect(catalogOnly.IsSuccess && catalogOnly.Options?.EmitRuntimeRoleCatalog == true,
    "runtime role catalog refresh requires masterdata but no AssetBundles root");

var mvSourceOnly = ConversionOptionsParser.Parse(new[]
{
    "--emit-mv-source-set",
    "--mv-manifest", "/data/mv-0112.json",
    "--asset-root", "/data/raw",
    "--out", "/data/mv-source",
});
Expect(
    mvSourceOnly.IsSuccess &&
    mvSourceOnly.Options?.EmitMvSourceSet == true &&
    mvSourceOnly.Options.MvManifestPath == "/data/mv-0112.json",
    "MV source set mode requires only a manifest, raw asset root, and output"
);

var mvAssetRoot = Path.Combine(tempDir, "mv-assets");
var mvOutput = Path.Combine(tempDir, "mv-output");
var mvManifestPath = Path.Combine(tempDir, "mv-manifest.json");
var mvDataPath = Path.Combine(mvAssetRoot, "live_pv", "mv_data", "0112.bundle");
var shaderPath = Path.Combine(mvAssetRoot, "shader", "live.bundle");
var mvBodyPath = Path.Combine(mvAssetRoot, "live_pv", "model", "characterv2", "body", "05", "9001", "ladies_s.bundle");
var mvFacePath = Path.Combine(mvAssetRoot, "live_pv", "model", "characterv2", "face", "05", "9001.bundle");
var mvHeadOptionalPath = Path.Combine(mvAssetRoot, "live_pv", "model", "character", "head_optional", "0112", "a03.bundle");
var mvBodyColorPath = Path.Combine(mvAssetRoot, "live_pv", "model", "characterv2", "color_variation", "body", "05", "9001", "02.bundle");
var mvHeadColorPath = Path.Combine(mvAssetRoot, "live_pv", "model", "characterv2", "color_variation", "head_optional", "0112", "a03", "02.bundle");
Directory.CreateDirectory(Path.GetDirectoryName(mvDataPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(shaderPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(mvBodyPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(mvFacePath)!);
Directory.CreateDirectory(Path.GetDirectoryName(mvHeadOptionalPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(mvBodyColorPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(mvHeadColorPath)!);
var wrappedMvBundle = new byte[132];
wrappedMvBundle[0] = 0x10;
"UnityFS-mv"u8.CopyTo(wrappedMvBundle.AsSpan(4));
for (var index = 4; index < wrappedMvBundle.Length; index += 8)
{
    for (var offset = 0; offset < 5; offset++)
    {
        wrappedMvBundle[index + offset] = (byte)~wrappedMvBundle[index + offset];
    }
}
File.WriteAllBytes(mvDataPath, wrappedMvBundle);
File.WriteAllBytes(shaderPath, "UnityFS-shader"u8.ToArray());
File.WriteAllBytes(mvBodyPath, "UnityFS-body"u8.ToArray());
File.WriteAllBytes(mvFacePath, "UnityFS-face"u8.ToArray());
File.WriteAllBytes(mvHeadOptionalPath, "UnityFS-head"u8.ToArray());
File.WriteAllBytes(mvBodyColorPath, "UnityFS-body-color"u8.ToArray());
File.WriteAllBytes(mvHeadColorPath, "UnityFS-head-color"u8.ToArray());
File.WriteAllText(mvManifestPath, JsonSerializer.Serialize(new
{
    music_id = 112,
    music_title = "天使のクローバー",
    asset_version = "test",
    bundles = new object[]
    {
        new { bundle = "live_pv/mv_data/0112", dependencies = Array.Empty<string>() },
        new { bundle = "shader/live", dependencies = Array.Empty<string>() },
        new { bundle = "live_pv/model/characterv2/body/05/9001/ladies_s", dependencies = Array.Empty<string>() },
        new { bundle = "live_pv/model/characterv2/face/05/9001", dependencies = Array.Empty<string>() },
        new { bundle = "live_pv/model/character/head_optional/0112/a03", dependencies = Array.Empty<string>() },
        new { bundle = "live_pv/model/characterv2/color_variation/body/05/9001/02", dependencies = Array.Empty<string>() },
        new { bundle = "live_pv/model/characterv2/color_variation/head_optional/0112/a03/02", dependencies = Array.Empty<string>() },
    },
}));
var mvSourceResult = MvSourceSetExporter.Export(mvManifestPath, mvAssetRoot, mvOutput);
var mvSourceSet = JsonNode.Parse(File.ReadAllText(Path.Combine(mvOutput, "mv-source-set.json")))!;
Expect(
    mvSourceResult.MusicId == 112 &&
    mvSourceResult.BundleCount == 7 &&
    File.Exists(Path.Combine(mvOutput, "mv-source-set.json")) &&
    File.Exists(Path.Combine(mvOutput, "deps.json")) &&
    File.Exists(Path.Combine(mvOutput, "source_bundles", "live_pv", "mv_data", "0112.bundle")),
    "MV source exporter validates UnityFS inputs and preserves logical bundle paths"
);
Expect(
    mvSourceSet["bundles"]![0]!["kind"]!.GetValue<string>() == "mv_data" &&
    mvSourceSet["bundles"]![1]!["kind"]!.GetValue<string>() == "shader" &&
    mvSourceSet["bundles"]![2]!["kind"]!.GetValue<string>() == "character_body" &&
    mvSourceSet["bundles"]![3]!["kind"]!.GetValue<string>() == "character_face" &&
    mvSourceSet["bundles"]![4]!["kind"]!.GetValue<string>() == "character_head_optional" &&
    mvSourceSet["bundles"]![5]!["kind"]!.GetValue<string>() == "character_body_color" &&
    mvSourceSet["bundles"]![6]!["kind"]!.GetValue<string>() == "character_head_optional_color",
    "MV source exporter classifies per-part V2 bundles and V1 fallbacks for the WebGL rebuild"
);

var canaryRoot = Path.Combine(tempDir, "bundle-canary");
Directory.CreateDirectory(canaryRoot);
var unknownWrapperPath = Path.Combine(canaryRoot, "unknown-wrapper.bundle");
File.WriteAllBytes(unknownWrapperPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 });
var rejectedUnknownWrapper = false;
try
{
    new SekaiBundleDecryptor().PrepareReadableBundle(unknownWrapperPath).Dispose();
}
catch (InvalidDataException ex) when (
    ex.Message.Contains(unknownWrapperPath) &&
    ex.Message.Contains("DEADBEEF"))
{
    rejectedUnknownWrapper = true;
}
Expect(rejectedUnknownWrapper,
    "unrecognized bundle obfuscation fails loudly naming the file path and first header bytes");
var fakeWrappedPath = Path.Combine(canaryRoot, "fake-wrapped.bundle");
var fakeWrappedBytes = new byte[132];
fakeWrappedBytes[0] = 0x10;
File.WriteAllBytes(fakeWrappedPath, fakeWrappedBytes);
var rejectedFakeWrapped = false;
try
{
    new SekaiBundleDecryptor().PrepareReadableBundle(fakeWrappedPath).Dispose();
}
catch (InvalidDataException ex) when (ex.Message.Contains(fakeWrappedPath))
{
    rejectedFakeWrapped = true;
}
Expect(rejectedFakeWrapped,
    "known PJSK wrapper magic with an unrecognized payload fails loudly after deobfuscation");
Expect(!Directory.EnumerateFiles(canaryRoot, ".pjskbundle2parts.*").Any(),
    "rejected wrapped bundles do not leak decrypted temp files");
var canaryPrimaryPath = Path.Combine(canaryRoot, "0001.bundle");
var canarySparseSibling = Path.Combine(canaryRoot, "0001a.bundle");
File.WriteAllBytes(canaryPrimaryPath, wrappedMvBundle);
File.WriteAllBytes(canarySparseSibling, Array.Empty<byte>());
using (var canaryWorkspace = new SekaiBundleDecryptor().PrepareReadableWorkspace(
    canaryPrimaryPath,
    new[] { canaryPrimaryPath, canarySparseSibling }
))
{
    Expect(
        File.ReadAllBytes(canaryWorkspace.PrimaryPath).AsSpan(0, 7).SequenceEqual("UnityFS"u8),
        "the known PJSK wrapper still deobfuscates to a Unity bundle inside workspaces"
    );
    Expect(
        File.Exists(Path.Combine(canaryWorkspace.DirectoryPath, "0001a.bundle")),
        "zero-byte sparse sibling placeholders keep passing through workspaces"
    );
}
var canaryGarbageSibling = Path.Combine(canaryRoot, "0001b.bundle");
File.WriteAllBytes(canaryGarbageSibling, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x05, 0x06, 0x07, 0x08 });
var siblingWarningWriter = new StringWriter();
var originalErrorWriter = Console.Error;
Console.SetError(siblingWarningWriter);
try
{
    using var garbageSiblingWorkspace = new SekaiBundleDecryptor().PrepareReadableWorkspace(
        canaryPrimaryPath,
        new[] { canaryPrimaryPath, canaryGarbageSibling }
    );
    Expect(
        File.ReadAllBytes(Path.Combine(garbageSiblingWorkspace.DirectoryPath, "0001b.bundle"))
            .AsSpan(0, 4).SequenceEqual(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }),
        "non-empty unrecognized sibling bundles copy through workspaces unchanged"
    );
}
finally
{
    Console.SetError(originalErrorWriter);
}
var siblingWarning = siblingWarningWriter.ToString();
Expect(
    siblingWarning.Contains(canaryGarbageSibling) && siblingWarning.Contains("DEADBEEF05060708"),
    "unrecognized sibling copy-through warns on stderr naming the file and first header bytes");
var rejectedGarbageWorkspacePrimary = false;
try
{
    new SekaiBundleDecryptor().PrepareReadableWorkspace(
        canaryGarbageSibling,
        new[] { canaryGarbageSibling }
    ).Dispose();
}
catch (InvalidDataException ex) when (
    ex.Message.Contains(canaryGarbageSibling) &&
    ex.Message.Contains("DEADBEEF05060708"))
{
    rejectedGarbageWorkspacePrimary = true;
}
Expect(rejectedGarbageWorkspacePrimary,
    "unrecognized primary bundles still fail loudly inside workspaces");
var rejectedWrappedGarbageSibling = false;
try
{
    new SekaiBundleDecryptor().PrepareReadableWorkspace(
        canaryPrimaryPath,
        new[] { canaryPrimaryPath, fakeWrappedPath }
    ).Dispose();
}
catch (InvalidDataException ex) when (ex.Message.Contains(fakeWrappedPath))
{
    rejectedWrappedGarbageSibling = true;
}
Expect(rejectedWrappedGarbageSibling,
    "wrapped siblings with unrecognized payloads still fail loudly after deobfuscation");

var hashAssetRoot = Path.Combine(tempDir, "hash-assets");
var indexedBundle = Path.Combine(hashAssetRoot, "live_pv", "model", "body.bundle");
Directory.CreateDirectory(Path.GetDirectoryName(indexedBundle)!);
File.WriteAllBytes(indexedBundle, new byte[] { 1, 2, 3 });
var expectedBundleHash = new string('a', 64);
var hashIndexPath = Path.Combine(hashAssetRoot, ".haruki-bundle-sha256.json");
File.WriteAllText(hashIndexPath, JsonSerializer.Serialize(new Dictionary<string, string>
{
    ["live_pv/model/body.bundle"] = expectedBundleHash,
    ["invalid.bundle"] = "not-a-hash",
}));
var hashIndex = new BundleHashIndex(hashIndexPath);
Expect(hashIndex.TryGet(hashAssetRoot, indexedBundle, out var indexedHash),
    "bundle hash index resolves exporter-relative path");
Expect(Convert.ToHexString(indexedHash).ToLowerInvariant() == expectedBundleHash,
    "bundle hash index decodes SHA-256 bytes");
Expect(!hashIndex.TryGet(hashAssetRoot, Path.Combine(hashAssetRoot, "invalid.bundle"), out _),
    "bundle hash index ignores invalid digests");
var corruptHashIndexPath = Path.Combine(hashAssetRoot, "corrupt.json");
File.WriteAllText(corruptHashIndexPath, "{");
Expect(!new BundleHashIndex(corruptHashIndexPath).TryGet(hashAssetRoot, indexedBundle, out _),
    "corrupt bundle hash index safely falls back to file hashing");

var plannerRoot = Path.Combine(tempDir, "work-planner");
Directory.CreateDirectory(plannerRoot);
var plannerEntries = new[]
{
    PartEntry(plannerRoot, "heavy-a", 900, "source-a"),
    PartEntry(plannerRoot, "heavy-b", 700, "source-b"),
    PartEntry(plannerRoot, "medium", 400, "source-c"),
    PartEntry(plannerRoot, "small", 100, "source-d"),
    PartEntry(plannerRoot, "small-alias", 100, "source-d", packagePath: "parts/body/small"),
};
var planned = PartPackageWorkPlanner.Plan(plannerEntries, 2);
var plannedAgain = PartPackageWorkPlanner.Plan(plannerEntries, 2);
Expect(planned.SelectMany(worker => worker).Select(entry => entry.PackagePath).Distinct().Count() == 4,
    "work planner emits one representative per package");
Expect(JsonSerializer.Serialize(planned) == JsonSerializer.Serialize(plannedAgain),
    "work planner is deterministic");
var plannedWeights = planned.Select(worker => worker.Sum(entry => new FileInfo(entry.BundlePath!).Length)).ToArray();
Expect(plannedWeights.Max() - plannedWeights.Min() <= 200,
    "work planner balances heavy source groups");
var sparsePlaceholder = PartEntry(plannerRoot, "sparse-placeholder", 0, "sparse-source");
Expect(PartPackageWorkPlanner.Plan(new[] { sparsePlaceholder }, 1).SelectMany(worker => worker).Any(),
    "work planner does not hide empty bundles during a full export");
Expect(!PartPackageWorkPlanner.Plan(new[] { sparsePlaceholder }, 1, sparseInput: true).SelectMany(worker => worker).Any(),
    "work planner excludes sparse zero-byte bundle placeholders");

var sparseManifestRoot = Path.Combine(tempDir, "sparse-manifest");
var sparseManifestOutput = Path.Combine(sparseManifestRoot, "output");
var sparseManifestPath = Path.Combine(sparseManifestRoot, "manifest.json");
Directory.CreateDirectory(sparseManifestRoot);
var sparseManifestEntry = PartEntry(
    sparseManifestRoot,
    "existing-runtime",
    3,
    "existing-runtime-source"
);
var sparseRuntimePath = Path.Combine(
    sparseManifestOutput,
    sparseManifestEntry.PackagePath,
    "part-runtime.msgpack.br"
);
Directory.CreateDirectory(Path.GetDirectoryName(sparseRuntimePath)!);
File.WriteAllBytes(sparseRuntimePath, new byte[] { 1 });
var originalSparseStamp = PartPackageInputStamp.From(sparseManifestEntry);
PartPackageExportManifest.Rebuild(
    sparseManifestPath,
    sparseManifestOutput,
    new[] { sparseManifestEntry }
);
File.WriteAllBytes(sparseManifestEntry.BundlePath!, Array.Empty<byte>());
var staleSparseError = Path.Combine(
    sparseManifestOutput,
    sparseManifestEntry.PackagePath,
    "part-export-error.json"
);
File.WriteAllText(staleSparseError, "{}");
PartPackageExportManifest.Rebuild(
    sparseManifestPath,
    sparseManifestOutput,
    new[] { sparseManifestEntry },
    sparseInput: true
);
Expect(
    PartPackageExportManifest.Load(sparseManifestPath).CanSkip(
        sparseManifestEntry.PackagePath,
        sparseRuntimePath,
        originalSparseStamp
    ),
    "sparse manifest rebuild preserves the real input stamp for an existing runtime"
);
Expect(!File.Exists(staleSparseError),
    "sparse manifest rebuild clears stale errors beside reusable runtimes");

var newSparseManifestPath = Path.Combine(sparseManifestRoot, "new-sparse-manifest.json");
var newSparseEntry = PartEntry(
    sparseManifestRoot,
    "new-sparse-runtime",
    0,
    "new-sparse-runtime-source"
);
var newSparseRuntimePath = Path.Combine(
    sparseManifestOutput,
    newSparseEntry.PackagePath,
    "part-runtime.msgpack.br"
);
Directory.CreateDirectory(Path.GetDirectoryName(newSparseRuntimePath)!);
File.WriteAllBytes(newSparseRuntimePath, new byte[] { 1 });
PartPackageExportManifest.Rebuild(
    newSparseManifestPath,
    sparseManifestOutput,
    new[] { newSparseEntry },
    sparseInput: true
);
Expect(
    PartPackageExportManifest.Load(newSparseManifestPath).CanSkip(
        newSparseEntry.PackagePath,
        newSparseRuntimePath,
        PartPackageInputStamp.From(newSparseEntry)
    ),
    "sparse manifest rebuild creates a placeholder stamp for an existing untracked runtime"
);

var missingSparseRuntime = PartEntry(
    sparseManifestRoot,
    "missing-runtime",
    0,
    "missing-runtime-source"
);
var rejectedMissingSparseRuntime = false;
try
{
    PartPackageExportManifest.Rebuild(
        sparseManifestPath,
        sparseManifestOutput,
        new[] { missingSparseRuntime },
        sparseInput: true
    );
}
catch (InvalidOperationException)
{
    rejectedMissingSparseRuntime = true;
}
Expect(rejectedMissingSparseRuntime,
    "sparse manifest rebuild fails instead of silently publishing a missing runtime");
var rejectedEmptyManifest = false;
try
{
    PartPackageExportManifest.Rebuild(
        newSparseManifestPath,
        sparseManifestOutput,
        Array.Empty<PartRegistryEntry>()
    );
}
catch (InvalidOperationException)
{
    rejectedEmptyManifest = true;
}
Expect(rejectedEmptyManifest,
    "manifest rebuild refuses to replace an existing registry with an empty one");
var corruptManifestPath = Path.Combine(sparseManifestRoot, "corrupt-manifest.json");
var corruptManifestEntry = PartEntry(
    sparseManifestRoot,
    "corrupt-manifest-part",
    5,
    "corrupt-manifest-source"
);
var corruptManifestRuntimePath = Path.Combine(
    sparseManifestOutput,
    corruptManifestEntry.PackagePath,
    "part-runtime.msgpack.br"
);
Directory.CreateDirectory(Path.GetDirectoryName(corruptManifestRuntimePath)!);
File.WriteAllBytes(corruptManifestRuntimePath, new byte[] { 1 });
var corruptManifestStamp = PartPackageInputStamp.From(corruptManifestEntry);
var trackedManifest = PartPackageExportManifest.Load(corruptManifestPath);
trackedManifest.Update(corruptManifestEntry.PackagePath, corruptManifestStamp);
trackedManifest.Save();
Expect(
    PartPackageExportManifest.Load(corruptManifestPath).CanSkip(
        corruptManifestEntry.PackagePath,
        corruptManifestRuntimePath,
        corruptManifestStamp
    ),
    "saved part package manifest round-trips input stamps"
);
Expect(!Directory.EnumerateFiles(sparseManifestRoot, "*.tmp").Any(),
    "atomic manifest saves clean up their temporary files");
File.WriteAllText(corruptManifestPath, "{\"parts/body/corrupt\":");
Expect(
    !PartPackageExportManifest.Load(corruptManifestPath).CanSkip(
        corruptManifestEntry.PackagePath,
        corruptManifestRuntimePath,
        corruptManifestStamp
    ),
    "corrupt part package manifest is treated as absent so packages rebuild"
);
var recoveredManifest = PartPackageExportManifest.Load(corruptManifestPath);
recoveredManifest.Update(corruptManifestEntry.PackagePath, corruptManifestStamp);
recoveredManifest.Save();
Expect(
    PartPackageExportManifest.Load(corruptManifestPath).CanSkip(
        corruptManifestEntry.PackagePath,
        corruptManifestRuntimePath,
        corruptManifestStamp
    ),
    "a corrupt part package manifest is replaced by the next save"
);
File.WriteAllText(corruptManifestPath, "{\"parts/body/corrupt\":");
var rejectedSparseCorruptManifest = false;
try
{
    PartPackageExportManifest.Rebuild(
        corruptManifestPath,
        sparseManifestOutput,
        new[] { corruptManifestEntry },
        sparseInput: true
    );
}
catch (InvalidOperationException ex) when (
    ex.Message.Contains("Sparse incremental input cannot preserve part package stamps from a corrupt manifest") &&
    ex.Message.Contains(corruptManifestPath) &&
    ex.Message.Contains("Restore the manifest or run a full export"))
{
    rejectedSparseCorruptManifest = true;
}
Expect(rejectedSparseCorruptManifest,
    "sparse incremental input fails loudly instead of dropping stamps from a corrupt manifest");
PartPackageExportManifest.Rebuild(
    corruptManifestPath,
    sparseManifestOutput,
    new[] { corruptManifestEntry }
);
Expect(
    PartPackageExportManifest.Load(corruptManifestPath).CanSkip(
        corruptManifestEntry.PackagePath,
        corruptManifestRuntimePath,
        corruptManifestStamp
    ),
    "a full export without the sparse marker still tolerates and replaces a corrupt manifest"
);
var serializedWorkListPath = Path.Combine(plannerRoot, "worker.json");
File.WriteAllText(serializedWorkListPath, JsonSerializer.Serialize(new PartPackageWorkList(
    new Dictionary<string, float> { ["5"] = 1.56f },
    planned[0]
)));
var serializedWorkList = PartPackageWorkPlanner.Load(serializedWorkListPath);
Expect(serializedWorkList.CharacterHeightMetersById["5"] == 1.56f,
    "worker list carries parent-built character heights");
Expect(serializedWorkList.Entries.Count == planned[0].Count,
    "worker list round trips planned entries");

var booleanOverride = ConversionOptionsParser.Parse(new[]
{
    "--config", configPath,
    "--convert-model-textures", "false"
});
Expect(booleanOverride.IsSuccess && booleanOverride.Options is not null, "model texture CLI override parses");
Expect(!booleanOverride.Options!.ConvertModelTextures, "model texture CLI override wins over config");

var dependencyRoot = Path.Combine(tempDir, "dependencies", "face", "11");
Directory.CreateDirectory(dependencyRoot);
foreach (var fileName in new[] { "0403.bundle", "0403a.bundle", "0403b.bundle", "0403c.bundle", "0403n.bundle", "0509.bundle" })
{
    File.WriteAllText(Path.Combine(dependencyRoot, fileName), fileName);
}
var headInput = new ResolvedBundleInput(
    BundlePartKind.Head,
    Path.Combine(dependencyRoot, "0403c.bundle"),
    Path.Combine(dependencyRoot, "0403c.bundle"),
    "11",
    "0403c"
);
var headDependencies = BundleDependencyResolver.ResolveLoadBundlePaths(headInput)
    .Select(Path.GetFileName)
    .ToArray();
Expect(
    headDependencies.SequenceEqual(new[] { "0403c.bundle", "0403.bundle", "0403a.bundle", "0403b.bundle", "0403n.bundle" }),
    "head dependency resolver loads primary bundle and same numeric family only"
);
var fullHeadDependencies = BundleDependencyResolver.ResolveLoadBundlePaths(headInput, BundleLoadDependencyMode.FullDirectory)
    .Select(Path.GetFileName)
    .ToArray();
Expect(fullHeadDependencies.Contains("0509.bundle"), "full-directory dependency resolver includes unrelated sibling bundles");
Expect(
    RuntimeMaterialIdentityResolver.BuildSyntheticMaterialKey("Accessory", "mtl_acc_00") ==
    RuntimeMaterialIdentityResolver.BuildSyntheticMaterialKey("head_optional", "mtl_acc_00"),
    "accessory native meshes and head_optional material slots share synthetic material identity"
);

var headAllRoot = Path.Combine(tempDir, "dependencies", "face", "12");
Directory.CreateDirectory(headAllRoot);
foreach (var fileName in new[] { "0001.bundle", "0001_head_all.bundle", "0001_mc.bundle", "0101.bundle" })
{
    File.WriteAllText(Path.Combine(headAllRoot, fileName), fileName);
}
var headAllInput = new ResolvedBundleInput(
    BundlePartKind.Head,
    Path.Combine(headAllRoot, "0001_head_all.bundle"),
    Path.Combine(headAllRoot, "0001_head_all.bundle"),
    "12",
    "0001_head_all"
);
var headAllDependencies = BundleDependencyResolver.ResolveLoadBundlePaths(headAllInput)
    .Select(Path.GetFileName)
    .ToArray();
Expect(
    headAllDependencies.SequenceEqual(new[] { "0001_head_all.bundle", "0001.bundle", "0001_mc.bundle" }),
    "head dependency resolver loads underscore siblings for head_all bundles"
);

var workerParsed = ConversionOptionsParser.Parse(new[]
{
    "--emit-part-packages",
    "--master", "/data/master",
    "--asset-root", "/data/assets",
    "--out", "/data/out",
    "--part-package-process-concurrency", "8",
    "--assetstudio-log-level", "debug",
    "--compact-textures",
    "--png-optimize", "off",
    "--texture-compact-workers", "3"
});
Expect(workerParsed.IsSuccess && workerParsed.Options is not null, "worker parse succeeds");
Expect(workerParsed.Options!.PartPackageProcessConcurrency == 8, "CLI part package process concurrency parses");
Expect(workerParsed.Options!.AssetStudioLogLevel == "debug", "CLI assetstudio log level parses");
Expect(workerParsed.Options!.CompactTextures, "CLI compact textures parses");
Expect(workerParsed.Options!.PngOptimizeMode == "off", "CLI PNG optimize mode parses");
Expect(workerParsed.Options!.TextureCompactWorkers == 3, "CLI texture compact workers parses");

var optimizeStoreParsed = ConversionOptionsParser.Parse(new[]
{
    "--optimize-texture-store",
    "--out", "/data/out",
    "--png-optimize", "off",
    "--texture-format", "ktx2",
    "--texture-compact-workers", "2",
});
Expect(optimizeStoreParsed.IsSuccess && optimizeStoreParsed.Options!.OptimizeTextureStore, "standalone texture store optimization parses without asset inputs");
Expect(optimizeStoreParsed.Options!.TextureFormat == "ktx2", "standalone texture format parses");

var defaultTextureFormatParsed = ConversionOptionsParser.Parse(new[]
{
    "--optimize-texture-store",
    "--out", "/data/out",
});
Expect(defaultTextureFormatParsed.IsSuccess && defaultTextureFormatParsed.Options!.TextureFormat == "png",
    "PNG remains the default texture format");

var invalidTextureFormatParsed = ConversionOptionsParser.Parse(new[]
{
    "--optimize-texture-store",
    "--out", "/data/out",
    "--texture-format", "webp",
});
Expect(!invalidTextureFormatParsed.IsSuccess, "invalid texture format is rejected");

var invalidPngOptimizeParsed = ConversionOptionsParser.Parse(new[]
{
    "--emit-part-packages",
    "--master", "/data/master",
    "--asset-root", "/data/assets",
    "--out", "/data/out",
    "--png-optimize", "webp"
});
Expect(!invalidPngOptimizeParsed.IsSuccess, "invalid PNG optimize mode is rejected");

var autoWorkerParsed = ConversionOptionsParser.Parse(new[]
{
    "--emit-part-packages",
    "--master", "/data/master",
    "--asset-root", "/data/assets",
    "--out", "/data/out",
    "--part-package-process-concurrency", "0"
});
Expect(autoWorkerParsed.IsSuccess && autoWorkerParsed.Options is not null, "auto process concurrency parse succeeds for full part package export");
Expect(autoWorkerParsed.Options!.PartPackageProcessConcurrency == 0, "CLI process concurrency 0 is preserved as auto");

var invalidAutoShardParsed = ConversionOptionsParser.Parse(new[]
{
    "--emit-part-packages",
    "--master", "/data/master",
    "--asset-root", "/data/assets",
    "--out", "/data/out",
    "--part-package-process-concurrency", "0",
    "--part-package-shard-count", "2",
    "--part-package-shard-index", "0"
});
Expect(!invalidAutoShardParsed.IsSuccess, "auto process concurrency cannot combine with manual shard options");

var shardParsed = ConversionOptionsParser.Parse(new[]
{
    "--emit-part-packages",
    "--master", "/data/master",
    "--asset-root", "/data/assets",
    "--out", "/data/out",
    "--part-package-shard-count", "8",
    "--part-package-shard-index", "3"
});
Expect(shardParsed.IsSuccess && shardParsed.Options is not null, "shard parse succeeds");
Expect(shardParsed.Options!.PartPackageShardCount == 8, "CLI part package shard count parses");
Expect(shardParsed.Options!.PartPackageShardIndex == 3, "CLI part package shard index parses");

var claimDirectory = Path.Combine(tempDir, "claims");
var claimParsed = ConversionOptionsParser.Parse(new[]
{
    "--emit-part-packages",
    "--master", "/data/master",
    "--asset-root", "/data/assets",
    "--out", "/data/out",
    "--part-package-claim-directory", claimDirectory,
});
Expect(claimParsed.IsSuccess && claimParsed.Options?.PartPackageClaimDirectory == claimDirectory, "dynamic package claim directory parses");
var claims = new PartPackageWorkClaims(claimDirectory);
Expect(claims.TryClaim("parts/_sources/body/a"), "first worker claims a package");
Expect(!claims.TryClaim("parts/_sources/body/a"), "a package can only be claimed by one worker");

var invalidSingleAutoParsed = ConversionOptionsParser.Parse(new[]
{
    "--emit-part-packages",
    "--part-costume3d-id", "2",
    "--part-type", "body",
    "--master", "/data/master",
    "--asset-root", "/data/assets",
    "--out", "/data/out",
    "--part-package-process-concurrency", "0"
});
Expect(!invalidSingleAutoParsed.IsSuccess, "auto process concurrency cannot combine with single part package export");

var allRoleParsed = ConversionOptionsParser.Parse(new[]
{
    "--emit-role-runtimes",
    "--master", "/data/master",
    "--asset-root", "/data/assets",
    "--out", "/data/out"
});
Expect(allRoleParsed.IsSuccess && allRoleParsed.Options is not null, "role runtime export can default to all character3ds");
Expect(allRoleParsed.Options!.RoleCharacter3dIds.Count == 0, "empty role id list means all character3ds");

var writerDir = Path.Combine(tempDir, "writer");
var writerPath = Path.Combine(writerDir, "part-runtime.json");
RuntimeJsonWriter.Write(
    writerPath,
    new
    {
        version = "msgpack",
        value = 7,
        nested = new { ok = true },
        items = new object?[] { "a", 2, null }
    },
    new JsonSerializerOptions()
);
var writerMessagePackPath = RuntimeJsonWriter.MessagePackBrotliPath(writerPath);
Expect(!File.Exists(writerPath), "msgpack-br runtime JSON mode does not write plain JSON");
Expect(!File.Exists(writerPath + ".gz"), "msgpack-br runtime JSON mode does not write gzip");
Expect(File.Exists(writerMessagePackPath), "msgpack-br runtime JSON mode writes MessagePack Brotli file");
using (var document = RuntimeJsonWriter.ReadJsonDocument(writerPath))
{
    Expect(document.RootElement.GetProperty("version").GetString() == "msgpack", "msgpack-br runtime JSON can be decoded and parsed");
    Expect(document.RootElement.GetProperty("nested").GetProperty("ok").GetBoolean(), "msgpack-br runtime JSON preserves nested objects");
    Expect(document.RootElement.GetProperty("items").GetArrayLength() == 3, "msgpack-br runtime JSON preserves arrays");
}
Expect(RuntimeJsonWriter.PrimaryPath(writerPath) == writerMessagePackPath, "runtime primary path points at .msgpack.br");
Expect(RuntimeJsonWriter.PrimaryPath(writerMessagePackPath) == writerMessagePackPath, "runtime primary path keeps final .msgpack.br paths unchanged");
Expect(RuntimeJsonWriter.DefaultBrotliQuality == 6, "runtime MessagePack defaults to Brotli quality 6");

var raceRoot = Path.Combine(writerDir, "compiled-cache-race");
var raceStore = Path.Combine(writerDir, "compiled-cache-race-store");
var raceSource = Path.Combine(writerDir, "compiled-cache-source.msgpack.br");
var startGate = Path.Combine(writerDir, "shared-core.start");
var raceBytes = Enumerable.Range(0, 256 * 1024).Select(index => (byte)(index % 251)).ToArray();
File.WriteAllBytes(raceSource, raceBytes);
var raceTargets = Enumerable.Range(0, 48).Select(index =>
{
    var target = Path.Combine(raceRoot, "parts", index.ToString(), "part-runtime-core.msgpack.br");
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllBytes(target, raceBytes);
    return target;
}).ToArray();
ContentAddressedStore.Compact(raceRoot, raceStore);
var raceWorkers = raceTargets.Select(target =>
{
    var startInfo = new ProcessStartInfo(Environment.ProcessPath!);
    startInfo.ArgumentList.Add("--compiled-cache-copy-race-worker");
    startInfo.ArgumentList.Add(raceSource);
    startInfo.ArgumentList.Add(target);
    startInfo.ArgumentList.Add(startGate);
    return Process.Start(startInfo) ?? throw new Exception("failed to start runtime writer race worker");
}).ToArray();
File.WriteAllText(startGate, "start");
foreach (var worker in raceWorkers)
{
    worker.WaitForExit();
    Expect(worker.ExitCode == 0, "parallel processes can publish the same immutable runtime file");
    worker.Dispose();
}
foreach (var target in raceTargets)
{
    Expect(File.ReadAllBytes(target).SequenceEqual(raceBytes), "parallel compiled-cache publication leaves valid content");
}

var directWriterPath = Path.Combine(writerDir, "direct-writer.json");
RuntimeJsonWriter.Write(
    directWriterPath,
    new DirectWriterFixture("direct", DirectWriterState.Ready, null),
    new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    }
);
using (var document = RuntimeJsonWriter.ReadJsonDocument(directWriterPath))
{
    Expect(document.RootElement.GetProperty("displayName").GetString() == "direct", "direct MessagePack writer honors JSON property naming");
    Expect(document.RootElement.GetProperty("state").GetString() == "Ready", "direct MessagePack writer honors string enums");
    Expect(!document.RootElement.TryGetProperty("optional", out _), "direct MessagePack writer honors null ignore conditions");
}

var lightingWriterPath = Path.Combine(writerDir, "material-lighting.json");
var lightingMaterial = new MaterialInventory(
    MaterialFileId: 0,
    MaterialPathId: 1,
    MaterialKey: "material:0:1",
    Name: "lighting",
    ShaderName: "Sekai/Character",
    TextureSlots: Array.Empty<TextureSlotInventory>(),
    ColorProperties: Array.Empty<ColorPropertyInventory>(),
    FloatProperties: new[]
    {
        new FloatPropertyInventory("_SekaiShadowThreshold", 0.40625f),
    }
);
RuntimeJsonWriter.Write(
    lightingWriterPath,
    new
    {
        lighting = SekaiMaterialMetadata.BuildLightingSettings(lightingMaterial),
        unknownLighting = SekaiMaterialMetadata.BuildLightingSettings(null),
    },
    new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    }
);
using (var document = RuntimeJsonWriter.ReadJsonDocument(lightingWriterPath))
{
    var lighting = document.RootElement.GetProperty("lighting");
    var unknownLighting = document.RootElement.GetProperty("unknownLighting");
    Expect(lighting.TryGetProperty("shadowWidth", out _), "nested material lighting uses the engine's camelCase schema");
    Expect(Math.Abs(lighting.GetProperty("sekaiShadowThreshold").GetSingle() - 0.40625f) < 0.0001f, "known official shader metadata uses the engine's camelCase schema");
    Expect(!unknownLighting.TryGetProperty("sekaiShadowThreshold", out _), "unknown official shader metadata stays absent instead of bloating every material");
    Expect(!lighting.TryGetProperty("ShadowWidth", out _), "nested material lighting does not leak PascalCase keys");
}

var binaryWriterPath = Path.Combine(writerDir, "binary-arrays.json");
var binaryPositions = Enumerable.Range(0, 12).Select(index => index / 10f).ToArray();
var binaryIndices = Enumerable.Range(0, 20).Select(index => index == 19 ? 70_000 : index * 2).ToArray();
RuntimeJsonWriter.Write(
    binaryWriterPath,
    new
    {
        nativeMeshes = new
        {
            meshes = new[]
            {
                new
                {
                    positions = binaryPositions,
                    skinIndices = Enumerable.Range(0, 20).ToArray(),
                    submeshes = new[] { new { indices = binaryIndices } }
                }
            }
        },
        gravityDir = new[] { 0f, -1f, 0f }
    },
    new JsonSerializerOptions(),
    binaryArraySchema: RuntimeBinaryArraySchema.PartRuntime
);
var binaryMessagePack = ReadBrotliBytes(RuntimeJsonWriter.MessagePackBrotliPath(binaryWriterPath));
Expect(ContainsRuntimeBinaryExtension(binaryMessagePack), "runtime mesh arrays are emitted as MessagePack binary extensions");
using (var document = RuntimeJsonWriter.ReadJsonDocument(binaryWriterPath))
{
    var mesh = document.RootElement.GetProperty("nativeMeshes").GetProperty("meshes")[0];
    var positions = mesh.GetProperty("positions").EnumerateArray().Select(item => item.GetSingle()).ToArray();
    var indices = mesh.GetProperty("submeshes")[0].GetProperty("indices").EnumerateArray().Select(item => item.GetInt32()).ToArray();
    var skinIndices = mesh.GetProperty("skinIndices").EnumerateArray().Select(item => item.GetInt32()).ToArray();
    Expect(positions.SequenceEqual(binaryPositions), "binary float32 arrays round-trip exact source float values");
    Expect(indices.SequenceEqual(binaryIndices), "binary index arrays round-trip exact integer values");
    Expect(skinIndices.SequenceEqual(Enumerable.Range(0, 20)), "binary uint16 arrays round-trip exact integer values");
    Expect(document.RootElement.GetProperty("gravityDir").GetArrayLength() == 3, "small semantic vectors remain ordinary arrays");
}

var genericValuesPath = Path.Combine(writerDir, "generic-values.json");
RuntimeJsonWriter.Write(
    genericValuesPath,
    new { values = Enumerable.Range(0, 20).Select(index => (double)index).ToArray() },
    new JsonSerializerOptions()
);
Expect(
    !ContainsRuntimeBinaryExtension(ReadBrotliBytes(RuntimeJsonWriter.MessagePackBrotliPath(genericValuesPath))),
    "generic JSON properties are not binary-encoded by name alone"
);

var directTextureRoot = Path.Combine(tempDir, "direct-texture-store");
var directTextureStore = new RuntimeTextureStore(directTextureRoot);
var directTextureBytes = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 };
var directTexturePath = directTextureStore.StorePng(directTextureBytes);
Expect(directTexturePath.StartsWith("/_texture_store/sha256/", StringComparison.Ordinal), "direct texture store returns a root-relative CAS URI");
Expect(directTextureStore.StorePng(directTextureBytes) == directTexturePath, "direct texture store reuses exact texture bytes");
Expect(Directory.EnumerateFiles(directTextureRoot, "*.png", SearchOption.AllDirectories).Count() == 1, "direct texture store writes one file per exact texture hash");

var concurrentPublishRoot = Path.Combine(tempDir, "concurrent-content-publish");
var concurrentPublishSource = Path.Combine(concurrentPublishRoot, "source.png");
var concurrentPublishTarget = Path.Combine(concurrentPublishRoot, "store", "texture.png");
Directory.CreateDirectory(concurrentPublishRoot);
File.WriteAllBytes(concurrentPublishSource, directTextureBytes);
var concurrentPublishHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(directTextureBytes)).ToLowerInvariant();
const int concurrentPublishers = 8;
using (var publishersReady = new CountdownEvent(concurrentPublishers))
{
    var publishTasks = Enumerable.Range(0, concurrentPublishers)
        .Select(_ => Task.Factory.StartNew(
            () => ContentAddressedFile.Ensure(
                concurrentPublishTarget,
                concurrentPublishHash,
                temporaryPath =>
                {
                    File.Copy(concurrentPublishSource, temporaryPath);
                    publishersReady.Signal();
                    publishersReady.Wait();
                }
            ),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        ))
        .ToArray();
    Task.WaitAll(publishTasks);
}
Expect(File.ReadAllBytes(concurrentPublishTarget).SequenceEqual(directTextureBytes),
    "concurrent exact-content publishers converge on one valid file");
Expect(!Directory.EnumerateFiles(Path.GetDirectoryName(concurrentPublishTarget)!, "*.tmp").Any(),
    "concurrent content publishing cleans temporary files");

var storeOptimization = TextureCompactor.OptimizeStore(
    directTextureRoot,
    "off",
    2
);
Expect(storeOptimization.TextureFileCount == 1 && storeOptimization.OptimizedFileCount == 0, "standalone texture optimizer scans the direct store without rewriting in off mode");
var resolveTexturePath = typeof(TextureCompactor).GetMethod(
    "ResolveTexturePath",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
) ?? throw new Exception("missing texture path resolver");
var rootRelativeTexture = "/_texture_store/sha256/ab/abc.png";
var resolvedRootRelativeTexture = (string?)resolveTexturePath.Invoke(
    null,
    new object[] { directTextureRoot, directTextureRoot, rootRelativeTexture }
);
Expect(
    resolvedRootRelativeTexture == Path.Combine(directTextureRoot, rootRelativeTexture.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)),
    "texture optimizer resolves root-relative CAS paths instead of treating them as external URIs"
);

var motionValuesPath = Path.Combine(writerDir, "motion-values.json");
RuntimeJsonWriter.Write(
    motionValuesPath,
    new
    {
        clips = new[]
        {
            new
            {
                tracks = new[]
                {
                    new
                    {
                        times = Enumerable.Range(0, 20).Select(index => index / 60f).ToArray(),
                        values = Enumerable.Range(0, 20).Select(index => index / 10f).ToArray()
                    }
                }
            }
        }
    },
    new JsonSerializerOptions(),
    binaryArraySchema: RuntimeBinaryArraySchema.UnityMotion
);
Expect(
    ContainsRuntimeBinaryExtension(ReadBrotliBytes(RuntimeJsonWriter.MessagePackBrotliPath(motionValuesPath))),
    "Unity motion track arrays are binary-encoded under the explicit motion schema"
);

var compactDir = Path.Combine(tempDir, "compact");
var packageA = Path.Combine(compactDir, "parts", "_sources", "body", "a");
var packageB = Path.Combine(compactDir, "parts", "_sources", "head", "b");
var packageC = Path.Combine(compactDir, "parts", "_sources", "hair", "c");
WriteRuntimePackage(packageA, "textures/body/a.png", new byte[] { 1, 2, 3, 4 });
WriteRuntimePackage(packageB, "textures/head/b.png", new byte[] { 1, 2, 3, 4 });
WriteRuntimePackage(packageC, "textures/hair/c.png", new byte[] { 9, 8, 7 });
var compactReport = TextureCompactor.Compact(compactDir, "off", 3);
Expect(compactReport.TextureFileCount == 3, "texture compactor scans package textures");
Expect(compactReport.UniqueHashCount == 2, "texture compactor groups by exact SHA-256");
Expect(compactReport.DuplicateFileCount == 1, "texture compactor counts duplicate files");
Expect(compactReport.SavedBytes == 4, "texture compactor saves only exact duplicate bytes with optimization off");
Expect(compactReport.WorkerCount == 3, "texture compactor reports parallel cleanup worker count");
Expect(File.Exists(Path.Combine(compactDir, "texture-compaction-report.json")), "texture compactor writes report");
Expect(!File.Exists(Path.Combine(packageA, "textures", "body", "a.png")), "texture compactor removes package-local texture A");
Expect(!File.Exists(Path.Combine(packageB, "textures", "head", "b.png")), "texture compactor removes package-local texture B");
Expect(!Directory.Exists(Path.Combine(packageA, "textures")), "texture compactor removes empty nested texture directory A");
Expect(!Directory.Exists(Path.Combine(packageB, "textures")), "texture compactor removes empty nested texture directory B");
Expect(!Directory.Exists(Path.Combine(packageC, "textures")), "texture compactor removes empty nested texture directory C");
var rewrittenA = ReadRuntimePackage(Path.Combine(packageA, "part-runtime.json"));
var rewrittenB = ReadRuntimePackage(Path.Combine(packageB, "part-runtime.json"));
var rewrittenC = ReadRuntimePackage(Path.Combine(packageC, "part-runtime.json"));
var textureA = rewrittenA["characterTextures"]!["main"]!.GetValue<string>();
var textureB = rewrittenB["characterTextures"]!["main"]!.GetValue<string>();
var textureC = rewrittenC["characterTextures"]!["main"]!.GetValue<string>();
Expect(textureA.StartsWith("/_texture_store/sha256/"), "texture compactor rewrites texture A to root store");
Expect(textureA == textureB, "texture compactor points same-hash textures at same store path");
Expect(textureA != textureC, "texture compactor keeps different hashes separate");
Expect(rewrittenA["materialSlots"]![0]!["mainTex"]!.GetValue<string>() == textureA, "texture compactor rewrites material slot texture");
Expect(rewrittenA["materialSlots"]![0]!["rawMaterial"]!["textureProperties"]![0]!["uri"]!.GetValue<string>() == textureA,
    "texture compactor rewrites raw material texture URI");
Expect(rewrittenA["textureRoles"]![0]!["uri"]!.GetValue<string>() == textureA, "texture compactor rewrites texture role URI");
Expect(File.Exists(Path.Combine(compactDir, textureA.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))), "texture compactor writes store texture");

var compactMessagePackDir = Path.Combine(tempDir, "compact-msgpack");
var messagePackPackage = Path.Combine(compactMessagePackDir, "parts", "_sources", "body", "a");
WriteRuntimePackage(
    messagePackPackage,
    "textures/body/a.png",
    new byte[] { 1, 2, 3, 4 }
);
var compactMessagePackReport = TextureCompactor.Compact(
    compactMessagePackDir,
    "off",
    1
);
Expect(compactMessagePackReport.RewrittenReferenceCount == 4, "texture compactor rewrites MessagePack runtime and raw material references");
Expect(!File.Exists(Path.Combine(messagePackPackage, "textures", "body", "a.png")), "MessagePack compaction removes replaced source texture");
var rewrittenMessagePack = ReadRuntimePackage(
    Path.Combine(messagePackPackage, "part-runtime.json")
);
var messagePackTexture = rewrittenMessagePack["characterTextures"]!["main"]!.GetValue<string>();
Expect(messagePackTexture.StartsWith("/_texture_store/sha256/"), "MessagePack runtime points at compacted texture store");
Expect(
    File.Exists(Path.Combine(compactMessagePackDir, messagePackTexture.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))),
    "MessagePack compacted texture exists"
);

if (!OperatingSystem.IsWindows())
{
    var ktxRoot = Path.Combine(tempDir, "ktx2-store");
    var ktxPackage = Path.Combine(ktxRoot, "parts", "_sources", "body", "a");
    var sourceUri = "/_texture_store/sha256/aa/source.png";
    var ktxSourcePath = Path.Combine(ktxRoot, sourceUri.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(ktxSourcePath)!);
    File.WriteAllBytes(ktxSourcePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4 });
    RuntimeJsonWriter.Write(
        Path.Combine(ktxPackage, "part-runtime.json"),
        new
        {
            characterTextures = new Dictionary<string, string> { ["shared"] = sourceUri },
            materialSlots = new[]
            {
                new
                {
                    mainTex = sourceUri,
                    shadowTex = (string?)null,
                    valueTex = sourceUri,
                    faceShadowTex = (string?)null,
                    rawMaterial = new
                    {
                        textureProperties = new[]
                        {
                            new { name = "_ValueTex", colorSpace = 1, uri = sourceUri }
                        }
                    }
                }
            },
            textureRoles = new[]
            {
                new { role = "main", uri = sourceUri },
                new { role = "value", uri = sourceUri }
            }
        },
        new JsonSerializerOptions()
    );
    var fakeKtx = Path.Combine(tempDir, "fake-ktx");
    File.WriteAllText(fakeKtx,
        "#!/bin/sh\n" +
        "kind=linear\n" +
        "case \" $* \" in *R8G8B8A8_SRGB*) kind=srgb ;; esac\n" +
        "eval output=\\${$#}\n" +
        "printf '%s:' \"$kind\" > \"$output\"\n" +
        "cat \"$(eval echo \\${$(($# - 1))})\" >> \"$output\"\n");
    File.SetUnixFileMode(fakeKtx, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    var previousKtxTool = Environment.GetEnvironmentVariable("HARUKI_KTX_TOOL");
    var ktxSharedCache = Path.Combine(tempDir, "ktx2-shared-cache");
    Environment.SetEnvironmentVariable("HARUKI_KTX_TOOL", fakeKtx);
    Ktx2TranscodeReport ktxReport;
    try
    {
        ktxReport = TextureCompactor.TranscodeStoreToKtx2(ktxRoot, 2, ktxSharedCache);
    }
    finally
    {
        Environment.SetEnvironmentVariable("HARUKI_KTX_TOOL", previousKtxTool);
    }
    Expect(ktxReport.SourceTextureCount == 1, "KTX2 finalizer counts unique source PNGs");
    Expect(ktxReport.ConvertedVariantCount == 2, "a texture used in color and data slots gets separate KTX2 variants");
    var ktxRuntime = ReadRuntimePackage(Path.Combine(ktxPackage, "part-runtime.json"));
    var ktxMain = ktxRuntime["materialSlots"]![0]!["mainTex"]!.GetValue<string>();
    var ktxValue = ktxRuntime["materialSlots"]![0]!["valueTex"]!.GetValue<string>();
    var ktxRawValue = ktxRuntime["materialSlots"]![0]!["rawMaterial"]!["textureProperties"]![0]!["uri"]!.GetValue<string>();
    Expect(ktxMain.EndsWith(".ktx2", StringComparison.Ordinal) && ktxValue.EndsWith(".ktx2", StringComparison.Ordinal),
        "KTX2 finalizer rewrites runtime texture extensions");
    Expect(ktxMain != ktxValue, "sRGB and linear KTX2 variants have distinct content paths");
    Expect(ktxRawValue == ktxValue, "raw material color space selects the matching linear KTX2 variant");
    Expect(ktxRuntime["characterTextures"]!["shared"]!.GetValue<string>() == ktxMain,
        "ambiguous character texture aliases prefer the color variant");
    Expect(File.Exists(Path.Combine(ktxRoot, ktxMain.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))),
        "sRGB KTX2 object exists");
    Expect(File.Exists(Path.Combine(ktxRoot, ktxValue.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))),
        "linear KTX2 object exists");
    Expect(!File.Exists(ktxSourcePath), "source PNG is removed only after successful KTX2 rewrites");

    File.WriteAllBytes(ktxSourcePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4 });
    RuntimeJsonWriter.Write(
        Path.Combine(ktxPackage, "part-runtime.json"),
        new
        {
            characterTextures = new Dictionary<string, string> { ["shared"] = sourceUri },
            materialSlots = new[]
            {
                new
                {
                    mainTex = sourceUri,
                    shadowTex = (string?)null,
                    valueTex = sourceUri,
                    faceShadowTex = (string?)null,
                    rawMaterial = new
                    {
                        textureProperties = new[]
                        {
                            new { name = "_ValueTex", colorSpace = 1, uri = sourceUri }
                        }
                    }
                }
            },
            textureRoles = new[]
            {
                new { role = "main", uri = sourceUri },
                new { role = "value", uri = sourceUri }
            }
        },
        new JsonSerializerOptions()
    );
    Environment.SetEnvironmentVariable("HARUKI_KTX_TOOL", "/bin/false");
    try
    {
        _ = TextureCompactor.TranscodeStoreToKtx2(ktxRoot, 2, ktxSharedCache);
    }
    finally
    {
        Environment.SetEnvironmentVariable("HARUKI_KTX_TOOL", previousKtxTool);
    }
    Expect(!File.Exists(ktxSourcePath), "shared KTX2 cache resumes without invoking the encoder");

    var failedKtxRoot = Path.Combine(tempDir, "ktx2-failure");
    var failedKtxPackage = Path.Combine(failedKtxRoot, "parts", "_sources", "body", "a");
    var failedSourceUri = "/_texture_store/sha256/bb/source.png";
    var failedSourcePath = Path.Combine(failedKtxRoot, failedSourceUri.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(failedSourcePath)!);
    File.WriteAllBytes(failedSourcePath, new byte[] { 137, 80, 78, 71, 5, 6, 7, 8 });
    RuntimeJsonWriter.Write(
        Path.Combine(failedKtxPackage, "part-runtime.json"),
        new
        {
            characterTextures = new Dictionary<string, string> { ["main"] = failedSourceUri },
            materialSlots = new[] { new { mainTex = failedSourceUri, shadowTex = (string?)null, valueTex = (string?)null, faceShadowTex = (string?)null } },
            textureRoles = new[] { new { role = "main", uri = failedSourceUri } }
        },
        new JsonSerializerOptions()
    );
    Environment.SetEnvironmentVariable("HARUKI_KTX_TOOL", "/bin/false");
    try
    {
        _ = TextureCompactor.TranscodeStoreToKtx2(failedKtxRoot, 1);
        throw new Exception("failed KTX2 encoder should abort finalization");
    }
    catch (InvalidOperationException)
    {
    }
    finally
    {
        Environment.SetEnvironmentVariable("HARUKI_KTX_TOOL", previousKtxTool);
    }
    Expect(File.Exists(failedSourcePath), "failed KTX2 conversion preserves the source PNG");
    var failedKtxRuntime = ReadRuntimePackage(Path.Combine(failedKtxPackage, "part-runtime.json"));
    Expect(failedKtxRuntime["materialSlots"]![0]!["mainTex"]!.GetValue<string>() == failedSourceUri,
        "failed KTX2 conversion preserves runtime PNG references");

    var compiledKtxRoot = Path.Combine(tempDir, "compiled-ktx-restore");
    var compiledKtxShared = Path.Combine(compiledKtxRoot, "shared");
    var compiledKtxOutput = Path.Combine(compiledKtxRoot, "output");
    var compiledKtxPackage = Path.Combine(compiledKtxOutput, "parts", "_sources", "body", "source");
    var compiledKtxPng = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 9, 8, 7, 6 };
    var compiledKtxSourceHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(compiledKtxPng)
    ).ToLowerInvariant();
    var compiledKtxSourceUri = $"/_texture_store/sha256/{compiledKtxSourceHash[..2]}/{compiledKtxSourceHash}.png";
    var compiledKtxRuntime = new JsonObject
    {
        ["characterTextures"] = new JsonObject { ["main"] = compiledKtxSourceUri },
        ["materialSlots"] = new JsonArray(new JsonObject
        {
            ["mainTex"] = compiledKtxSourceUri,
            ["shadowTex"] = null,
            ["valueTex"] = null,
            ["faceShadowTex"] = null,
            ["rawMaterial"] = new JsonObject
            {
                ["textureProperties"] = new JsonArray(new JsonObject
                {
                    ["name"] = "_MainTex",
                    ["colorSpace"] = 0,
                    ["uri"] = compiledKtxSourceUri
                })
            }
        }),
        ["textureRoles"] = new JsonArray(new JsonObject { ["role"] = "main", ["uri"] = compiledKtxSourceUri })
    };
    var compiledKtxBytes = System.Text.Encoding.UTF8.GetBytes("cached-uastc-ktx2");
    var compiledKtxCachePath = Path.Combine(
        compiledKtxShared,
        "ktx2",
        TextureCompactor.Ktx2EncoderVersion,
        "srgb",
        compiledKtxSourceHash[..2],
        compiledKtxSourceHash + ".ktx2"
    );
    Directory.CreateDirectory(Path.GetDirectoryName(compiledKtxCachePath)!);
    File.WriteAllBytes(compiledKtxCachePath, compiledKtxBytes);
    Expect(
        new TextureCompactor().TryRestoreCachedKtx2(
            compiledKtxRuntime,
            compiledKtxPackage,
            compiledKtxOutput,
            compiledKtxShared
        ),
        "cached KTX2 restores without a source PNG"
    );
    var compiledKtxRestoredUri = compiledKtxRuntime["materialSlots"]![0]!["mainTex"]!.GetValue<string>();
    Expect(compiledKtxRestoredUri.EndsWith(".ktx2", StringComparison.Ordinal),
        "cached KTX2 restore rewrites runtime texture references directly");
    Expect(
        compiledKtxRuntime["materialSlots"]![0]!["rawMaterial"]!["textureProperties"]![0]!["uri"]!.GetValue<string>() ==
            compiledKtxRestoredUri,
        "cached KTX2 restore rewrites raw material texture references"
    );
    Expect(File.Exists(Path.Combine(
        compiledKtxOutput,
        compiledKtxRestoredUri.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
    )), "cached KTX2 restore publishes the final texture");
}

var sharedCas = Path.Combine(tempDir, "shared-cas");
var casRegionA = Path.Combine(tempDir, "cas-region-a");
var casRegionB = Path.Combine(tempDir, "cas-region-b");
WriteCasFixture(casRegionA);
WriteCasFixture(casRegionB);
var firstCasReport = ContentAddressedStore.Compact(casRegionA, sharedCas);
var secondCasReport = ContentAddressedStore.Compact(casRegionB, sharedCas);
var unchangedCasReport = ContentAddressedStore.Compact(casRegionB, sharedCas);
Expect(firstCasReport.TextureFileCount == 1, "shared CAS scans compacted textures");
Expect(firstCasReport.PartRuntimeFileCount == 1, "shared CAS scans part runtime packages");
Expect(firstCasReport.NewContentCount == 2, "first region seeds exact content in the shared CAS");
Expect(secondCasReport.ReusedContentCount == 2, "second region reuses exact texture and part runtime bytes");
Expect(secondCasReport.ReusedBytes > 0, "shared CAS reports bytes reused across regions");
Expect(unchangedCasReport.UnchangedFileCount == 2, "repeated CAS runs skip unchanged files without hashing or relinking");
if (!OperatingSystem.IsWindows())
{
    var canonicalModes = File.GetUnixFileMode(CasPartRuntimePath(casRegionA));
    Expect(
        (canonicalModes & (UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0,
        "CAS-linked content is read-only to prevent in-place mutation of shared inodes"
    );
}
Expect(File.ReadAllBytes(CasTexturePath(casRegionA)).SequenceEqual(File.ReadAllBytes(CasTexturePath(casRegionB))), "CAS-linked textures preserve exact bytes");
Expect(File.ReadAllBytes(CasPartRuntimePath(casRegionA)).SequenceEqual(File.ReadAllBytes(CasPartRuntimePath(casRegionB))), "CAS-linked part runtimes preserve exact bytes");
var regionAPartBytes = File.ReadAllBytes(CasPartRuntimePath(casRegionA));
RuntimeJsonWriter.Write(
    Path.Combine(casRegionB, "parts", "_sources", "body", "source", "part-runtime.json"),
    new { version = "changed", positions = new[] { 3f, 4f, 5f } },
    new JsonSerializerOptions()
);
Expect(File.ReadAllBytes(CasPartRuntimePath(casRegionA)).SequenceEqual(regionAPartBytes), "atomic runtime writes do not mutate another region's CAS link");
using (var changedRuntime = RuntimeJsonWriter.ReadJsonDocument(
    Path.Combine(casRegionB, "parts", "_sources", "body", "source", "part-runtime.json")
))
{
    Expect(changedRuntime.RootElement.GetProperty("version").GetString() == "changed", "atomic runtime writes replace only the requested region path");
}
var casStatePath = Path.Combine(casRegionB, "content-addressed-store-state.json");
File.WriteAllText(casStatePath, "{\"parts/_sources/body/source/part-runtime.msgpack.br\": {\"length\": 4,");
var corruptStateCasReport = ContentAddressedStore.Compact(casRegionB, sharedCas);
Expect(corruptStateCasReport.UnchangedFileCount == 0,
    "corrupt CAS state is treated as a first run with full re-verification instead of failing");
Expect(corruptStateCasReport.TextureFileCount == 1 && corruptStateCasReport.PartRuntimeFileCount == 1,
    "corrupt-state CAS recovery still compacts every scanned file");
var recoveredCasReport = ContentAddressedStore.Compact(casRegionB, sharedCas);
Expect(recoveredCasReport.UnchangedFileCount == 2,
    "CAS state is rebuilt atomically after corrupt-state recovery");
Expect(!Directory.EnumerateFiles(casRegionB, "*.tmp").Any(),
    "atomic CAS state and report writes clean up their temporary files");

var registryMasterDir = Path.Combine(tempDir, "registry-master");
var registryAssetRoot = Path.Combine(tempDir, "registry-assets");
Directory.CreateDirectory(registryMasterDir);
WriteJsonFile(Path.Combine(registryMasterDir, "character3ds.json"), new[]
{
    new
    {
        id = 9001,
        characterId = 23,
        unit = "light_sound",
        name = "cross-role official preset",
        headCostume3dId = 11001,
        hairCostume3dId = 202,
        bodyCostume3dId = 13000
    }
});
WriteJsonFile(Path.Combine(registryMasterDir, "costume3ds.json"), new[]
{
    new
    {
        id = 13000,
        costume3dGroupId = 13000,
        partType = "body",
        characterId = 21,
        colorId = 1,
        colorName = "test",
        name = "cross-role body",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "unused",
        howToObtain = "test"
    },
    new
    {
        id = 11001,
        costume3dGroupId = 11000,
        partType = "head",
        characterId = 21,
        colorId = 2,
        colorName = "test",
        name = "legacy accessory",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "unused",
        howToObtain = "test"
    },
    new
    {
        id = 11009,
        costume3dGroupId = 11000,
        partType = "head",
        characterId = 21,
        colorId = 1,
        colorName = "test",
        name = "fallback accessory",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "0020/a04",
        howToObtain = "test"
    },
    new
    {
        id = 1,
        costume3dGroupId = 1,
        partType = "head",
        characterId = 1,
        colorId = 1,
        colorName = "default",
        name = "empty accessory slot",
        costume3dType = "normal",
        costume3dRarity = "rarity_1",
        assetbundleName = "head_default_01",
        howToObtain = "default"
    },
    new
    {
        id = 202,
        costume3dGroupId = 202,
        partType = "hair",
        characterId = 2,
        colorId = 1,
        colorName = "default",
        name = "default hair fallback",
        costume3dType = "normal",
        costume3dRarity = "rarity_1",
        assetbundleName = "unused",
        howToObtain = "default"
    },
    new
    {
        id = 203,
        costume3dGroupId = 203,
        partType = "hair",
        characterId = 21,
        colorId = 1,
        colorName = "default",
        name = "same-character target hair",
        costume3dType = "normal",
        costume3dRarity = "rarity_1",
        assetbundleName = "unused",
        howToObtain = "default"
    },
    new
    {
        id = 12000,
        costume3dGroupId = 12000,
        partType = "head",
        characterId = 2,
        colorId = 1,
        colorName = "missing",
        name = "missing complete head",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "unused",
        howToObtain = "test"
    },
    new
    {
        id = 12001,
        costume3dGroupId = 12001,
        partType = "head",
        characterId = 2,
        colorId = 1,
        colorName = "test",
        name = "split accessory",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "unused",
        howToObtain = "test"
    },
    new
    {
        id = 797001,
        costume3dGroupId = 797001,
        partType = "head",
        characterId = 1,
        colorId = 1,
        colorName = "original",
        name = "shared accessory source",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "cos0797_head",
        howToObtain = "card"
    },
    new
    {
        id = 797009,
        costume3dGroupId = 797002,
        partType = "head",
        characterId = 2,
        colorId = 1,
        colorName = "original",
        name = "exclusive accessory",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "cos0797_unique_head",
        howToObtain = "card"
    },
    new
    {
        id = 797011,
        costume3dGroupId = 797002,
        partType = "head",
        characterId = 2,
        colorId = 2,
        colorName = "another 1",
        name = "exclusive accessory",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "cos0797_unique_head_01",
        howToObtain = "card"
    },
    new
    {
        id = 797161,
        costume3dGroupId = 797021,
        partType = "head",
        characterId = 2,
        colorId = 1,
        colorName = "original",
        name = "shared accessory",
        costume3dType = "normal",
        costume3dRarity = "rarity_4",
        assetbundleName = "cos0797_head",
        howToObtain = "card"
    }
});
WriteJsonFile(Path.Combine(registryMasterDir, "costume3dModels.json"), new object[]
{
    new
    {
        costume3dId = 13000,
        unit = "light_sound",
        assetbundleName = "99/0081",
        headCostume3dAssetbundleType = (string?)null,
        colorAssetbundleName = (string?)null,
        part = (string?)null,
        thumbnailAssetbundleName = "unused"
    },
    new
    {
        costume3dId = 11001,
        unit = "light_sound",
        assetbundleName = "0019/a03",
        headCostume3dAssetbundleType = "head_only",
        colorAssetbundleName = "01",
        part = "a03",
        thumbnailAssetbundleName = "unused"
    },
    new
    {
        costume3dId = 11009,
        unit = "light_sound",
        assetbundleName = (string?)null,
        headCostume3dAssetbundleType = "head_only",
        colorAssetbundleName = "02",
        part = "a04",
        thumbnailAssetbundleName = "unused"
    },
    new
    {
        costume3dId = 1,
        unit = "light_sound",
        assetbundleName = (string?)null,
        headCostume3dAssetbundleType = "head_only",
        colorAssetbundleName = (string?)null,
        part = (string?)null,
        thumbnailAssetbundleName = "head_default_01"
    },
    new
    {
        costume3dId = 202,
        unit = (string?)null,
        assetbundleName = "02/0000",
        headCostume3dAssetbundleType = (string?)null,
        colorAssetbundleName = (string?)null,
        part = (string?)null,
        thumbnailAssetbundleName = "unused"
    },
    new
    {
        costume3dId = 12000,
        unit = "light_sound",
        assetbundleName = "0710/a05",
        headCostume3dAssetbundleType = "head",
        colorAssetbundleName = (string?)null,
        part = (string?)null,
        thumbnailAssetbundleName = "unused"
    },
    new
    {
        costume3dId = 12001,
        unit = "light_sound",
        assetbundleName = "0083/a05",
        headCostume3dAssetbundleType = "head_all",
        colorAssetbundleName = (string?)null,
        part = "a05",
        thumbnailAssetbundleName = "unused"
    },
    new
    {
        costume3dId = 797001,
        unit = "light_sound",
        assetbundleName = "0924/a03",
        headCostume3dAssetbundleType = "head_only",
        colorAssetbundleName = (string?)null,
        part = "a03",
        thumbnailAssetbundleName = "cos0797_head"
    },
    new
    {
        costume3dId = 797009,
        unit = "light_sound",
        assetbundleName = "02/0924",
        headCostume3dAssetbundleType = "head_and_hair",
        colorAssetbundleName = (string?)null,
        part = (string?)null,
        thumbnailAssetbundleName = "cos0797_unique_head"
    },
    new
    {
        costume3dId = 797009,
        unit = "idol",
        assetbundleName = "0924/a03",
        headCostume3dAssetbundleType = "head_only",
        colorAssetbundleName = (string?)null,
        part = "a03",
        thumbnailAssetbundleName = "cos0797_head"
    },
    new
    {
        costume3dId = 797011,
        unit = "light_sound",
        assetbundleName = "02/0924a",
        headCostume3dAssetbundleType = "head_and_hair",
        colorAssetbundleName = "01",
        part = (string?)null,
        thumbnailAssetbundleName = "cos0797_unique_head_01"
    },
    new
    {
        costume3dId = 797161,
        unit = "light_sound",
        assetbundleName = "0924/a03",
        headCostume3dAssetbundleType = "head_only",
        colorAssetbundleName = (string?)null,
        part = "a03",
        thumbnailAssetbundleName = "cos0797_head"
    }
});
WriteJsonFile(Path.Combine(registryMasterDir, "gameCharacters.json"), new[]
{
    new
    {
        id = 23,
        resourceId = 23,
        gender = "male",
        height = 1.7,
        figure = "mens",
        breastSize = "none",
        modelName = "test",
        unit = "light_sound",
        supportUnitType = (string?)null,
        faceModelType = "Special",
        prefabType = "Default",
        isHeelOffset = false
    },
    new
    {
        id = 2,
        resourceId = 2,
        gender = "female",
        height = 1.6,
        figure = "ladies",
        breastSize = "m",
        modelName = "test",
        unit = "light_sound",
        supportUnitType = (string?)null,
        faceModelType = "0",
        prefabType = "Default",
        isHeelOffset = false
    }
});
WriteJsonFile(Path.Combine(registryMasterDir, "cards.json"), Array.Empty<object>());
WriteJsonFile(Path.Combine(registryMasterDir, "cardCostume3ds.json"), Array.Empty<object>());
WriteJsonFile(Path.Combine(registryMasterDir, "costume3dModelNotAvailablePatterns.json"), new[]
{
    new
    {
        headCostume3dId = 999001,
        hairCostume3dId = 999002,
        unit = (string?)"light_sound",
        isDefault = false
    },
    new
    {
        headCostume3dId = 999003,
        hairCostume3dId = 999004,
        unit = (string?)null,
        isDefault = false
    }
});
WriteJsonFile(Path.Combine(registryMasterDir, "costume3dModelDefaultHairs.json"), new[]
{
    new
    {
        headCostume3dId = 11001,
        hairCostume3dId = 202,
        unit = "light_sound",
        isDefault = true
    }
});
var legacyAccessory = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "head_optional",
    "0019",
    "a03.bundle"
);
var legacyAccessoryColor = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "color_variation",
    "head_optional",
    "0019",
    "a03",
    "01.bundle"
);
var fallbackAccessory = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "character",
    "head_optional",
    "0020",
    "a04.bundle"
);
var fallbackAccessoryColor = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "character",
    "color_variation",
    "head_optional",
    "0020",
    "a04",
    "02.bundle"
);
var splitAccessory = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "head_optional",
    "0083",
    "a05.bundle"
);
var sharedAccessory = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "head_optional",
    "0924",
    "a03.bundle"
);
var exclusiveAccessory = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "face",
    "02",
    "0924.bundle"
);
var exclusiveAccessoryColor = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "face",
    "02",
    "0924a.bundle"
);
var defaultHairFallback = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "face",
    "02",
    "0001.bundle"
);
var faceModelTypeVariant = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "face",
    "02",
    "0000_special.bundle"
);
var presetBody = Path.Combine(
    registryAssetRoot,
    "live_pv",
    "model",
    "characterv2",
    "body",
    "99",
    "0081",
    "mens.bundle"
);
Directory.CreateDirectory(Path.GetDirectoryName(legacyAccessory)!);
Directory.CreateDirectory(Path.GetDirectoryName(legacyAccessoryColor)!);
Directory.CreateDirectory(Path.GetDirectoryName(fallbackAccessory)!);
Directory.CreateDirectory(Path.GetDirectoryName(fallbackAccessoryColor)!);
Directory.CreateDirectory(Path.GetDirectoryName(splitAccessory)!);
Directory.CreateDirectory(Path.GetDirectoryName(sharedAccessory)!);
Directory.CreateDirectory(Path.GetDirectoryName(exclusiveAccessory)!);
Directory.CreateDirectory(Path.GetDirectoryName(exclusiveAccessoryColor)!);
Directory.CreateDirectory(Path.GetDirectoryName(defaultHairFallback)!);
Directory.CreateDirectory(Path.GetDirectoryName(faceModelTypeVariant)!);
Directory.CreateDirectory(Path.GetDirectoryName(presetBody)!);
File.WriteAllBytes(legacyAccessory, new byte[] { 1 });
File.WriteAllBytes(legacyAccessoryColor, new byte[] { 2 });
File.WriteAllBytes(fallbackAccessory, new byte[] { 3 });
File.WriteAllBytes(fallbackAccessoryColor, new byte[] { 4 });
File.WriteAllBytes(splitAccessory, new byte[] { 8 });
File.WriteAllBytes(sharedAccessory, new byte[] { 9 });
File.WriteAllBytes(exclusiveAccessory, new byte[] { 10 });
File.WriteAllBytes(exclusiveAccessoryColor, new byte[] { 11 });
File.WriteAllBytes(defaultHairFallback, new byte[] { 5 });
File.WriteAllBytes(faceModelTypeVariant, new byte[] { 6 });
File.WriteAllBytes(presetBody, new byte[] { 7 });
var registryExport = CostumeRegistryExporter.ExportInMemory(registryMasterDir, registryAssetRoot);
Expect(
    registryExport.HeadHairCompatibility.Rules.All(rule => rule.State is "not_available" or "default_hint"),
    "compatibility registry contains only blacklist rows and default-hair hints"
);
Expect(
    registryExport.HeadHairCompatibility.Rules.Any(rule =>
        rule.State == "default_hint" &&
        rule.HeadCostume3dId == 11001 &&
        rule.HairCostume3dId == 202 &&
        rule.IsDefault),
    "default-hair master remains a fallback hint"
);
var registryOutput = Path.Combine(tempDir, "registry-output");
CostumeRegistryExporter.Export(
    registryMasterDir,
    registryAssetRoot,
    registryOutput
);
using (var scopedCompatibility = RuntimeJsonWriter.ReadJsonDocument(Path.Combine(
    registryOutput,
    "parts",
    "compat",
    "by-unit",
    "light_sound",
    "head-hair-compatibility.json"
)))
{
    var scopedRules = scopedCompatibility.RootElement.GetProperty("rules").EnumerateArray().ToArray();
    Expect(scopedRules.Length == 1, "scoped head-hair compatibility omits positive and default rules");
    Expect(scopedRules[0].GetProperty("state").GetString() == "not_available", "scoped head-hair compatibility keeps deny rules");
}
Expect(
    RuntimeJsonWriter.OutputsExist(Path.Combine(registryOutput, "parts", "by-role", "2", "default", "part-registry.json")),
    "null-unit registry rows write the default unit segment the engine requests"
);
Expect(
    !RuntimeJsonWriter.OutputsExist(Path.Combine(registryOutput, "parts", "by-role", "2", "part-registry.json")),
    "null-unit registry rows do not collapse the by-role unit segment"
);
using (var defaultScopedRegistry = RuntimeJsonWriter.ReadJsonDocument(Path.Combine(
    registryOutput,
    "parts",
    "by-role",
    "2",
    "default",
    "part-registry.json"
)))
{
    var defaultScopedEntries = defaultScopedRegistry.RootElement.GetProperty("entries").EnumerateArray().ToArray();
    Expect(
        defaultScopedEntries.Any(entry =>
            entry.GetProperty("costume3dId").GetInt32() == 202 &&
            entry.GetProperty("unit").ValueKind == JsonValueKind.Null),
        "default-segment scoped registry keeps its null-unit rows"
    );
}
Expect(
    RuntimeJsonWriter.OutputsExist(Path.Combine(registryOutput, "parts", "compat", "by-unit", "default", "head-hair-compatibility.json")),
    "null-unit deny rules write the default unit segment the engine requests"
);
Expect(
    !RuntimeJsonWriter.OutputsExist(Path.Combine(registryOutput, "parts", "compat", "by-unit", "head-hair-compatibility.json")),
    "null-unit deny rules do not collapse the by-unit unit segment"
);
using (var defaultScopedCompatibility = RuntimeJsonWriter.ReadJsonDocument(Path.Combine(
    registryOutput,
    "parts",
    "compat",
    "by-unit",
    "default",
    "head-hair-compatibility.json"
)))
{
    var defaultScopedRules = defaultScopedCompatibility.RootElement.GetProperty("rules").EnumerateArray().ToArray();
    Expect(
        defaultScopedRules.Length == 1 &&
        defaultScopedRules[0].GetProperty("state").GetString() == "not_available" &&
        defaultScopedRules[0].GetProperty("unit").ValueKind == JsonValueKind.Null,
        "default-segment compatibility keeps only its null-unit deny rules"
    );
}
using (var compactRegistry = RuntimeJsonWriter.ReadJsonDocument(
    Path.Combine(registryOutput, "parts", "part-registry-compact.json")
))
{
    var root = compactRegistry.RootElement;
    Expect(root.ValueKind == JsonValueKind.Array && root.GetArrayLength() == 3, "compact part registry uses a versioned array envelope");
    var rootItems = root.EnumerateArray().ToArray();
    Expect(rootItems[0].GetInt32() == 1, "compact part registry schema version is stable");
    Expect(rootItems[1].GetInt32() == registryExport.PartRegistry.Version, "compact part registry keeps the source registry version");
    var firstRow = rootItems[2].EnumerateArray().First();
    Expect(firstRow.ValueKind == JsonValueKind.Array && firstRow.GetArrayLength() == 15, "compact part registry rows omit repeated field names");
}
using (var compactCompatibility = RuntimeJsonWriter.ReadJsonDocument(
    Path.Combine(registryOutput, "parts", "head-hair-compatibility-compact.json")
))
{
    var rootItems = compactCompatibility.RootElement.EnumerateArray().ToArray();
    Expect(rootItems.Length == 2 && rootItems[0].GetInt32() == 1, "compact compatibility uses a versioned array envelope");
    var firstRow = rootItems[1].EnumerateArray().First();
    Expect(firstRow.ValueKind == JsonValueKind.Array && firstRow.GetArrayLength() == 5, "compact compatibility rows omit unused metadata and repeated field names");
}
var publicRoleEntries = Enumerable.Range(1, 31)
    .Select(roleId =>
    {
        var characterId = roleId <= 20 ? roleId : roleId <= 26 ? 21 : roleId - 5;
        var unit = roleId switch
        {
            <= 4 => "light_sound",
            <= 8 => "idol",
            <= 12 => "street",
            <= 16 => "theme_park",
            <= 20 => "school_refusal",
            21 => "piapro",
            22 => "idol",
            23 => "light_sound",
            24 => "street",
            25 => "theme_park",
            26 => "school_refusal",
            _ => "piapro",
        };
        return new Character3dMaster(
            Id: roleId,
            CharacterId: characterId,
            Unit: unit,
            Name: $"role-{roleId}",
            HeadCostume3dId: 2000 + roleId,
            HairCostume3dId: 3000 + roleId,
            BodyCostume3dId: 1000 + roleId
        );
    })
    .ToList();
var publicMasterRoot = Path.Combine(tempDir, "public-master");
var publicMasterDirectory = Path.Combine(publicMasterRoot, "master");
Directory.CreateDirectory(publicMasterDirectory);
Directory.CreateDirectory(Path.Combine(publicMasterRoot, "versions"));
File.WriteAllText(
    Path.Combine(publicMasterDirectory, "character3ds.json"),
    JsonSerializer.Serialize(publicRoleEntries)
);
File.WriteAllText(
    Path.Combine(publicMasterDirectory, "gameCharacterUnits.json"),
    JsonSerializer.Serialize(publicRoleEntries.Select((entry, index) => new GameCharacterUnitMaster(
        Id: index + 1,
        GameCharacterId: entry.CharacterId,
        Unit: entry.Unit,
        SkinColorCode: "#feefe0",
        SkinShadowColorCode1: "#efafbb",
        SkinShadowColorCode2: "#e07889"
    )))
);
File.WriteAllText(
    Path.Combine(publicMasterDirectory, "gameCharacters.json"),
    JsonSerializer.Serialize(Enumerable.Range(1, 26).Select(characterId => new
    {
        id = characterId,
        resourceId = characterId,
        gender = "female",
        height = characterId == 8 ? 1.68f : 1.60f,
        figure = "ladies",
        breastSize = "m",
        modelName = $"character-{characterId}",
        unit = (string?)null,
        supportUnitType = (string?)null,
        faceModelType = "Default",
        prefabType = "Default",
        isHeelOffset = false,
    }))
);
File.WriteAllText(
    Path.Combine(publicMasterRoot, "versions", "current_version.json"),
    """{"dataVersion":"test-master"}"""
);
var publicRoleCatalogOutput = Path.Combine(tempDir, "runtime-role-catalog-output");
var publicRoleCatalog = RuntimeRoleCatalogExporter.WriteFromMaster(
    publicMasterDirectory,
    publicRoleCatalogOutput
);
Expect(publicRoleCatalog?.Roles.Count == 31, "runtime role catalog contains exactly the 31 public roles");
Expect(publicRoleCatalog?.Roles.Single(role => role.RoleId == 23).CharacterId == 21, "runtime role 23 maps to Miku");
Expect(publicRoleCatalog?.Roles.Single(role => role.RoleId == 23).Unit == "light_sound", "runtime role 23 keeps the SEKAI unit");
Expect(publicRoleCatalog?.Roles.Single(role => role.RoleId == 31).CharacterId == 26, "runtime role 31 maps to KAITO");
Expect(publicRoleCatalog?.Roles.Single(role => role.RoleId == 5).SkinColors.Default == "#feefe0", "runtime role catalog carries master skin colors");
Expect(Math.Abs((publicRoleCatalog?.Roles.Single(role => role.RoleId == 8).CharacterHeightMeters ?? 0f) - 1.68f) < 0.0001f, "runtime role catalog carries master character height");
Expect(publicRoleCatalog?.Roles.All(role => !string.IsNullOrWhiteSpace(role.RoleRuntimePath)) == true, "runtime role catalog exposes role runtime packages");
Expect(
    RuntimeJsonWriter.OutputsExist(Path.Combine(publicRoleCatalogOutput, "runtime-role-catalog.json")),
    "runtime role catalog uses a stable region-root path"
);
Expect(
    RuntimeJsonWriter.OutputsExist(Path.Combine(publicRoleCatalogOutput, "parts", "by-role", "21", "light_sound", "runtime-role-catalog.json")),
    "runtime role catalog writes a scoped browser index"
);
using (var writtenRoleCatalog = RuntimeJsonWriter.ReadJsonDocument(Path.Combine(publicRoleCatalogOutput, "runtime-role-catalog.json")))
{
    Expect(!writtenRoleCatalog.RootElement.TryGetProperty("releaseId", out _), "runtime role catalog has no release id");
    Expect(writtenRoleCatalog.RootElement.GetProperty("version").GetInt32() == 4, "runtime role catalog uses the skin-and-height-aware master-version schema");
    Expect(!string.IsNullOrWhiteSpace(writtenRoleCatalog.RootElement.GetProperty("masterVersion").GetString()), "runtime role catalog records its master version");
    Expect(writtenRoleCatalog.RootElement.GetProperty("roles").GetArrayLength() == 31, "written runtime role catalog keeps all public roles");
}
var missingMasterVersionRejected = false;
try
{
    RuntimeRoleCatalogExporter.ResolveMasterVersion(Path.Combine(tempDir, "master-without-version"));
}
catch (FileNotFoundException)
{
    missingMasterVersionRejected = true;
}
Expect(missingMasterVersionRejected, "runtime role catalog refuses to invent a master version");
Expect(registryExport.PartRegistry.Version == 2, "part registry marks source-based accessory identity schema");
var outfitBodyEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 13000 && entry.CharacterId == 21);
Expect(outfitBodyEntry.OutfitId == 13, "body registry derives stable outfit id from costume group family");
var legacyAccessoryEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 11001 && entry.CharacterId == 21 && entry.Unit == "light_sound");
Expect(legacyAccessoryEntry.OutfitId == 0, "non-body registry rows do not expose an outfit id");
Expect(legacyAccessoryEntry.AccessoryId == 11000, "head_optional color inherits its original-color source accessory id");
Expect(legacyAccessoryEntry.PartType == "head_optional", "head_only registry rows are exported as head_optional");
Expect(legacyAccessoryEntry.BundlePath == legacyAccessory, "head_optional registry resolves characterv2 base bundle");
Expect(legacyAccessoryEntry.ColorVariationBundlePath == legacyAccessoryColor, "head_optional registry resolves characterv2 color variation bundle");
Expect(legacyAccessoryEntry.PackagePath.StartsWith("parts/_sources/head_optional/"), "head_optional registry writes shared source package path");
var fallbackAccessoryEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 11009 && entry.CharacterId == 21 && entry.Unit == "light_sound");
Expect(fallbackAccessoryEntry.Status == "planned", "legacy head_optional accessory is planned alongside characterv2 accessories");
Expect(fallbackAccessoryEntry.BundlePath == fallbackAccessory, "head_optional registry resolves the legacy character base bundle");
Expect(fallbackAccessoryEntry.ColorVariationBundlePath == fallbackAccessoryColor, "head_optional registry resolves the legacy character color variation");
Expect(fallbackAccessoryEntry.AttachNode == "a04", "head_optional fallback accessory keeps attach node");
var emptyAccessoryEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 1);
Expect(emptyAccessoryEntry.PartType == "head_optional", "empty head_default slot is exported as head_optional");
Expect(emptyAccessoryEntry.Status == "empty", "empty head_default slot is a valid empty part");
Expect(emptyAccessoryEntry.BundlePath is null, "empty head_default slot does not point at a bundle");
Expect(emptyAccessoryEntry.SourceKey is null, "empty head_default slot does not create a source package");
Expect(emptyAccessoryEntry.Warnings.Count == 0, "empty head_default slot is not a warning");
var defaultHairEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 202 && entry.CharacterId == 2);
Expect(defaultHairEntry.Status == "planned", "default hair 0000 row falls back to existing 0001 bundle");
Expect(defaultHairEntry.BundlePath == defaultHairFallback, "default hair fallback points at the existing 0001 bundle");
Expect(defaultHairEntry.PackagePath.StartsWith("parts/_sources/hair/"), "default hair fallback keeps a source package");
var missingHeadEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 12000);
Expect(missingHeadEntry.Status == "missing", "missing complete head remains missing after fallback attempts");
Expect(missingHeadEntry.BundlePath is null, "missing complete head does not keep a fabricated bundle path");
Expect(missingHeadEntry.SourceKey is null, "missing complete head does not create a dangling source key");
Expect(missingHeadEntry.Warnings.Any(warning => warning.Contains("face bundle not found")), "missing complete head records a file warning");
var splitAccessoryEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 12001);
Expect(splitAccessoryEntry.PartType == "head_optional", "head_all registry rows are exported as head_optional");
Expect(splitAccessoryEntry.BundlePath == splitAccessory, "head_all registry resolves its optional accessory bundle");
var canonicalSharedAccessoryEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 797001);
var exclusiveAccessoryEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 797009 && entry.Unit == "light_sound");
var sameRawSharedAccessoryEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 797009 && entry.Unit == "idol");
var exclusiveAccessoryColorEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 797011 && entry.Unit == "light_sound");
var sharedAccessoryEntry = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 797161 && entry.CharacterId == 2);
Expect(canonicalSharedAccessoryEntry.AccessoryId == 797001, "shared accessory uses its smallest original-color costume group id");
Expect(sharedAccessoryEntry.AccessoryId == 797001, "shared accessory aliases keep the canonical source accessory id");
Expect(sameRawSharedAccessoryEntry.PartType == "head_optional", "the shared-unit model resolves the same raw costume as head_optional");
Expect(sameRawSharedAccessoryEntry.AccessoryId == 797001, "the same raw costume follows its resolved shared source");
Expect(exclusiveAccessoryEntry.PartType == "head", "the exclusive-unit model resolves the same raw costume as a complete head");
Expect(exclusiveAccessoryEntry.AccessoryId == 797002, "character-exclusive head uses its original-color costume group id");
Expect(exclusiveAccessoryColorEntry.BaseSourceKey != exclusiveAccessoryEntry.BaseSourceKey, "exclusive color fixture uses a distinct resolved base source");
Expect(exclusiveAccessoryColorEntry.AccessoryId == 797002, "character-exclusive head colors inherit the original-color accessory id");
Expect(sameRawSharedAccessoryEntry.BaseSourceKey == canonicalSharedAccessoryEntry.BaseSourceKey, "shared unit entries reuse the shared base source");
Expect(exclusiveAccessoryEntry.BaseSourceKey != sameRawSharedAccessoryEntry.BaseSourceKey, "different units of the same raw costume retain distinct resolved sources");
Expect(exclusiveAccessoryEntry.AccessoryId != sharedAccessoryEntry.AccessoryId, "exclusive and shared heads remain separate accessories");
var roleHeadOptionalAlias = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 11001 && entry.CharacterId == 23);
Expect(roleHeadOptionalAlias.PartType == "head_optional", "official cross-role head_only preset aliases as head_optional");
Expect(roleHeadOptionalAlias.Unit == "light_sound", "official cross-role alias keeps model unit");
Expect(roleHeadOptionalAlias.PackagePath == legacyAccessoryEntry.PackagePath, "official cross-role alias reuses source package path");
Expect(roleHeadOptionalAlias.AccessoryId == 11000, "official alias receives the canonical accessory id");
Expect(
    !registryExport.PartRegistry.Entries.Any(entry =>
        entry.Costume3dId is 11001 or 11009 &&
        entry.CharacterId == 2),
    "head-hair compatibility does not invent cross-character part ownership"
);
Expect(
    !registryExport.PartRegistry.Entries.Any(entry =>
        entry.Costume3dId is 11001 or 11009 &&
        entry.CharacterId == 21 &&
        entry.Unit == "idol"),
    "head-hair compatibility does not invent cross-unit part ownership"
);
var roleHairAlias = registryExport.PartRegistry.Entries.Single(entry => entry.Costume3dId == 202 && entry.CharacterId == 23);
Expect(roleHairAlias.Unit == "light_sound", "official cross-role alias promotes default-unit rows into the preset role unit");
Expect(roleHairAlias.PackagePath == defaultHairEntry.PackagePath, "official cross-role hair alias reuses source package path");

PartMaterialMetadataSmoke.Run();

var repoRoot = FindRepoRoot();
var programSource = File.ReadAllText(Path.Combine(repoRoot, "Program.cs"));
var partPackageExporterSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "PartPackageExporter.cs"));
var partPackageManifestSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "PartPackageExportManifest.cs"));
var compiledPartCacheSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "CompiledPartCache.cs"));
var nativeMeshExporterSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "UnityRuntimeNativeMeshExporter.cs"));
var runtimeModelsSource = File.ReadAllText(Path.Combine(repoRoot, "Models", "PjskSekaiRuntimeModels.cs"));
var runtimeWriterSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "RuntimeJsonWriter.cs"));
var conversionPlannerSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "ConversionPlanner.cs"));
var roleRuntimeExporterSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "RoleRuntimeExporter.cs"));
var assetStudioLoadedBundleSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "AssetStudioLoadedBundle.cs"));
var assetStudioImportedModelFactorySource = File.ReadAllText(Path.Combine(repoRoot, "Services", "AssetStudioImportedModelFactory.cs"));
var bundleDependencyResolverSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "BundleDependencyResolver.cs"));
var materialIdentityLookupSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "MaterialIdentityLookup.cs"));
var bundleInputResolverSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "BundleInputResolver.cs"));
var sekaiBundleDecryptorSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "SekaiBundleDecryptor.cs"));
var character3dCostumeResolverSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "Character3dCostumeResolver.cs"));
Expect(partPackageExporterSource.Contains("part-runtime-core.msgpack.br"), "part package corePath uses the final MessagePack Brotli filename");
Expect(partPackageExporterSource.Contains("CharacterControllers: new PjskSekaiRuntimeCharacterControllers("), "part package core preserves character material controllers");
Expect(partPackageExporterSource.Contains("Hair: core.SpringBone.CharacterHair"), "part package core preserves the serialized SekaiCharacterHair offset");
Expect(
    partPackageExporterSource.Contains("PropertyNamingPolicy = JsonNamingPolicy.CamelCase"),
    "part package runtime metadata uses the camelCase schema consumed by the engine"
);
Expect(
    partPackageExporterSource.Contains("DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull"),
    "part package runtime metadata omits unknown optional shader fields"
);
Expect(partPackageExporterSource.Contains("partType is not (\"head\" or \"hair\")"), "incremental export invalidates only head and hair packages for controller metadata");
Expect(partPackageExporterSource.Contains("coreVersion.GetString() == \"0415-part-core-3\""), "incremental export requires the camelCase material metadata core schema");
Expect(partPackageExporterSource.Contains("version.GetString() != \"0415-part-delta-3\""), "incremental export keeps the stable part delta schema");
Expect(partPackageExporterSource.Contains("HasResolvedEyelashMasks(document.RootElement)"), "incremental export rejects head runtimes with unresolved through-hair masks");
Expect(compiledPartCacheSource.Contains("part-runtime-core.msgpack.br"), "compiled part cache restores the final MessagePack Brotli corePath");
Expect(nativeMeshExporterSource.Contains("AddTangent(tangents") && nativeMeshExporterSource.Contains("vertex.Tangent"), "native mesh export preserves the tangent basis for second normals");
Expect(nativeMeshExporterSource.Contains("values.Add(-tangent.W)"), "native mesh export flips tangent handedness with AssetStudio's mirrored X axis");
Expect(nativeMeshExporterSource.Contains("AddUv(uv2, vertex, 2)"), "native mesh export preserves packed second-normal UV2 data");
Expect(nativeMeshExporterSource.Contains("if (hasUv1)"), "native mesh export leaves UV1 absent when the source channel is absent");
Expect(nativeMeshExporterSource.Contains("if (hasUv2)"), "native mesh export leaves UV2 absent when the source channel is absent");
Expect(
    nativeMeshExporterSource.Contains("hasExactOrderedBinding") &&
    nativeMeshExporterSource.Contains("Enumerable.Range(0, importedBoneCount)") &&
    nativeMeshExporterSource.Contains("rendererBonePathIds[oldIndex]"),
    "native mesh export preserves every exact ordered Unity skin slot, including unused and repeated Transform paths"
);
Expect(runtimeModelsSource.Contains("JsonPropertyName(\"tangents\")"), "runtime native mesh schema publishes tangents");
Expect(runtimeModelsSource.Contains("JsonPropertyName(\"uv2\")"), "runtime native mesh schema publishes UV2");
Expect(runtimeWriterSource.Contains("\"nativeMeshes.meshes.tangents\""), "runtime binary codec stores tangents as float32");
Expect(runtimeWriterSource.Contains("\"nativeMeshes.meshes.uv2\""), "runtime binary codec stores UV2 as float32");
Expect(compiledPartCacheSource.Contains("delta[\"version\"] = \"0415-part-delta-3\""), "compiled part cache keeps the stable part delta schema version");
Expect(compiledPartCacheSource.Contains("0415-compiled-part-9"), "compiled part cache invalidates compacted Unity skin bindings");
Expect(compiledPartCacheSource.Contains("resolved-eyelash-mask-v1"), "compiled part cache invalidates only head and hair entries without dependency masks");
Expect(compiledPartCacheSource.Contains("ResolveExistingBundlePaths"), "compiled part fingerprints include dependency bundles");
Expect(compiledPartCacheSource.Contains("PropertyNamingPolicy = JsonNamingPolicy.CamelCase"), "compiled part cache patches runtime metadata with camelCase keys");
Expect(compiledPartCacheSource.Contains("DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull"), "compiled part cache does not restore unknown optional shader fields");
Expect(compiledPartCacheSource.Contains("JsonSerializer.SerializeToNode(BuildIdentity(entry), RuntimeJsonOptions)"), "compiled part cache keeps patched part identity keys camelCase");
Expect(compiledPartCacheSource.Contains("Append(hash, Path.GetFileName(input.ResolvedBundlePath))"), "compiled part cache fingerprints the primary bundle among sibling dependencies");
Expect(compiledPartCacheSource.Contains("textureCompactor.TryRestoreCachedKtx2"), "compiled part cache restores final KTX2 without the source PNG CAS");
Expect(compiledPartCacheSource.Contains("restoredKtx2 ? Array.Empty<string>() : cached.TextureHashes"), "compiled KTX2 restore does not republish source PNGs");
Expect(!partPackageExporterSource.Contains("part-runtime-core.json"), "part package exporter omits logical JSON core paths");
Expect(!compiledPartCacheSource.Contains("part-runtime-core.json"), "compiled part cache omits logical JSON core paths");
Expect(partPackageExporterSource.Contains("coreRelativePath.EndsWith(\".msgpack.br\""), "incremental export rejects old logical corePath manifests");
Expect(partPackageExporterSource.Contains("name.Contains(\"eyelash\")"), "part package exporter classifies eyelash separately");
Expect(partPackageExporterSource.Contains("return \"eyelash\""), "part package exporter returns eyelash material kind");
Expect(partPackageExporterSource.Contains("name.Contains(\"eyebrow\")"), "part package exporter classifies eyebrow separately");
Expect(partPackageExporterSource.Contains("return \"eyebrow\""), "part package exporter returns eyebrow material kind");
Expect(partPackageExporterSource.Contains("name.Contains(\"_acc_\")"), "part package exporter classifies head acc materials as accessory");
Expect(
    partPackageExporterSource.IndexOf("name.Contains(\"_hair_\")", StringComparison.Ordinal) <
    partPackageExporterSource.IndexOf("if (hasFaceShadowTex)", StringComparison.Ordinal),
    "part package exporter classifies explicit head hair materials before FaceSDF fallback"
);
Expect(
    partPackageExporterSource.IndexOf("name.Contains(\"_acc_\")", StringComparison.Ordinal) <
    partPackageExporterSource.IndexOf("if (hasFaceShadowTex)", StringComparison.Ordinal),
    "part package exporter classifies explicit head accessory materials before FaceSDF fallback"
);
Expect(
    conversionPlannerSource.IndexOf("name.Contains(\"_hair_\")", StringComparison.Ordinal) <
    conversionPlannerSource.IndexOf("if (hasFaceShadowTex)", StringComparison.Ordinal),
    "conversion planner classifies explicit head hair materials before FaceSDF fallback"
);
Expect(
    conversionPlannerSource.IndexOf("name.Contains(\"_acc_\")", StringComparison.Ordinal) <
    conversionPlannerSource.IndexOf("if (hasFaceShadowTex)", StringComparison.Ordinal),
    "conversion planner classifies explicit head accessory materials before FaceSDF fallback"
);
Expect(partPackageExporterSource.Contains("\"eyelash\" or \"eyebrow\""), "part package exporter uses full-runtime render order for face detail layers");
Expect(partPackageExporterSource.Contains("BuildDeferredColliderFlagBindings"), "part package exporter preserves deferred head colliderFlag bindings");
Expect(partPackageExporterSource.Contains("deferred_body_colliderFlag"), "part package exporter labels head colliderFlag bindings as deferred to viewer composer");
Expect(partPackageExporterSource.Contains("ResolveColliderFlagPrefixes"), "part package exporter resolves colliderFlag matched prefixes for viewer rebinding");
Expect(partPackageExporterSource.Contains("prefixes.Add(\"CL_Hip\")"), "part package exporter maps colliderFlag Hip");
Expect(partPackageExporterSource.Contains("prefixes.Add(\"CL_Chest\")"), "part package exporter maps colliderFlag Chest");
Expect(partPackageExporterSource.Contains("prefixes.Add(\"CL_Left_Arm\")"), "part package exporter maps colliderFlag L_Arm");
Expect(partPackageExporterSource.Contains("prefixes.Add(\"CL_Right_Arm\")"), "part package exporter maps colliderFlag R_Arm");
Expect(partPackageExporterSource.Contains("prefixes.Add(\"CL_Left_Elbow\")"), "part package exporter maps colliderFlag L_Elbow");
Expect(partPackageExporterSource.Contains("prefixes.Add(\"CL_Right_Elbow\")"), "part package exporter maps colliderFlag R_Elbow");
Expect(partPackageExporterSource.Contains("MatchedPrefixes: prefixes"), "part package exporter writes colliderFlag matched prefixes for viewer rebinding");
Expect(partPackageExporterSource.Contains("IsSumOfForcesOnBone: ReadBool(manager.Raw, \"isSumOfForcesOnBone\", defaultValue: true)"), "part package exporter defaults SpringManager force summing on like full runtime export");
Expect(partPackageExporterSource.Contains("RawAngleLimits: new VrmSpringBoneAngleLimitsCandidate("), "part package exporter preserves per-bone angle limits");
Expect(partPackageExporterSource.Contains("Y: ReadAxisLimit(bone.Raw, \"yAngleLimits\")"), "part package exporter reads y angle limits from SpringBone raw data");
Expect(partPackageExporterSource.Contains("Z: ReadAxisLimit(bone.Raw, \"zAngleLimits\")"), "part package exporter reads z angle limits from SpringBone raw data");
Expect(partPackageExporterSource.Contains("ReadOptionalBool(axis, \"active\") ??"), "part package exporter reads explicit angle limit active flags");
Expect(partPackageExporterSource.Contains("ReadOptionalBool(axis, \"m_Enabled\") ??"), "part package exporter reads Unity enabled angle limit flags");
Expect(partPackageExporterSource.Contains("                true,"), "part package exporter defaults present angle limits to active like full runtime output");
Expect(partPackageExporterSource.Contains("AccessoryTransformAdjustments: accessoryTransformAdjustments"), "part package exporter writes head_optional accessory transform adjustments");
Expect(partPackageExporterSource.Contains("root.Name, \"optional\""), "part package exporter prefers official head_optional prefab resource name");

var partRuntimeModelsSource = File.ReadAllText(Path.Combine(repoRoot, "Models", "PartRuntimeModels.cs"));
var springBoneExporterSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "SpringBoneExporter.cs"));
var costumeRegistryModelsSource = File.ReadAllText(Path.Combine(repoRoot, "Models", "CostumeRegistryModels.cs"));
var costumeRegistryExporterSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "CostumeRegistryExporter.cs"));
var pjskRuntimeModelsSource = File.ReadAllText(Path.Combine(repoRoot, "Models", "PjskSekaiRuntimeModels.cs"));
var pjskRuntimeBuilderSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "PjskSekaiRuntimeExtensionBuilder.cs"));
var motionPackageExporterSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "MotionPackageExporter.cs"));
Expect(partRuntimeModelsSource.Contains("accessoryTransformAdjustments"), "part runtime mount exposes accessory transform adjustment map");
Expect(pjskRuntimeModelsSource.Contains("JsonPropertyName(\"isAccessory\")"), "runtime material slots expose official IS_ACCESSORY_ID metadata");
Expect(pjskRuntimeBuilderSource.Contains("IsAccessory: true"), "runtime builder marks accessory material slots with IS_ACCESSORY_ID metadata");
Expect(pjskRuntimeBuilderSource.Contains("IsAccessory: false"), "runtime builder keeps body/head material slots non-accessory");
Expect(pjskRuntimeBuilderSource.Contains("Native mesh geometry, bind matrices, morph deltas, and ImportedFrame rest transforms are already converted by AssetStudio"), "runtime coordinate metadata distinguishes imported viewer-space data from raw Unity prefab data");
Expect(motionPackageExporterSource.Contains("PositionConversion: \"exporter_mirror_x\""), "motion coordinate metadata declares exporter-normalized translations");
Expect(motionPackageExporterSource.Contains("RotationConversion: \"exporter_negate_quaternion_yz\""), "motion coordinate metadata declares exporter-normalized rotations");
Expect(!motionPackageExporterSource.Contains("The viewer must convert transform animation values"), "motion metadata does not request a second viewer conversion");
Expect(partPackageExporterSource.Contains("IsAccessory: partType == \"head_optional\""), "part package exporter marks head_optional materials as accessories");
Expect(springBoneExporterSource.Contains("CharacterAccessoryTransformController"), "spring bone exporter keeps accessory transform controller mono behaviours");
Expect(springBoneExporterSource.Contains("CharacterAccessoryTransformData"), "spring bone exporter keeps accessory transform data mono behaviours");
Expect(springBoneExporterSource.Contains("\"ForceVolume\""), "spring bone exporter keeps standard UTJ ForceVolume providers");
Expect(springBoneExporterSource.Contains("\"WindVolume\""), "spring bone exporter keeps standard UTJ WindVolume providers");
Expect(springBoneExporterSource.Contains("\"WindVolumeOneSelf\""), "spring bone exporter keeps PJSK WindVolumeOneSelf providers");
Expect(springBoneExporterSource.Contains("BuildAccessoryTransformAdjustments"), "spring bone exporter extracts accessory transform adjustments");
Expect(springBoneExporterSource.Contains("_faceIdAccessoryTransformDict"), "spring bone exporter reads official face-id accessory transform dictionary");
Expect(springBoneExporterSource.Contains("string.Equals(entry.ScriptName, \"ExtraBone\", StringComparison.OrdinalIgnoreCase)"), "spring bone exporter serializes ExtraBone only from real MonoBehaviour records");
Expect(!springBoneExporterSource.Contains("StartsWith(\"EX_\""), "spring bone exporter does not infer ExtraBone records from EX_* transform names");
Expect(partRuntimeModelsSource.Contains("JsonPropertyName(\"extraBones\")"), "part runtime spring payload exposes ExtraBone records");
Expect(partPackageExporterSource.Contains("ExtraBones: springBone.ExtraBones"), "part package exporter preserves ExtraBone records for custom composition");
Expect(partPackageExporterSource.Contains("partType is \"head\" or \"hair\""), "head and hair color variations target hair materials");
Expect(character3dCostumeResolverSource.Contains("ResolveFaceColorVariationPath"), "character resolver resolves real face color variation bundles");
var failedPartGuard = programSource.IndexOf("if (failed > 0)", StringComparison.Ordinal);
var failedPartExit = failedPartGuard < 0
    ? -1
    : programSource.IndexOf("return 2;", failedPartGuard, StringComparison.Ordinal);
var failedPartCompaction = failedPartGuard < 0
    ? -1
    : programSource.IndexOf("RunTextureCompactionIfEnabled(options);", failedPartGuard, StringComparison.Ordinal);
Expect(
    failedPartGuard >= 0 && failedPartExit > failedPartGuard && failedPartExit < failedPartCompaction,
    "part package failures exit nonzero before texture compaction"
);
Expect(!programSource.Contains("shardManifestPaths"), "part worker orchestration does not create manifest shards");
Expect(programSource.Contains("PartPackageExportManifest.Rebuild("), "parent rebuilds one canonical manifest after worker success");
Expect(
    partPackageExporterSource.Contains("if (claims is null && string.IsNullOrWhiteSpace(workListPath))"),
    "claim and work-list workers treat the shared baseline manifest as read-only"
);
Expect(pjskRuntimeBuilderSource.Contains("SpringManager.FindSpringBones(true) ownership is authoritative"), "runtime builder documents hierarchy-based SpringManager ownership");
Expect(!pjskRuntimeBuilderSource.Contains("SpringManager.springBones references remain authoritative"), "runtime builder does not treat serialized springBones as authoritative");
Expect(
    pjskRuntimeBuilderSource.IndexOf("\"ModelUtility.SpringBoneSetup\"", StringComparison.Ordinal) <
    pjskRuntimeBuilderSource.IndexOf("\"CharacterModel.SetupSpringBone\"", StringComparison.Ordinal),
    "runtime builder setup plan follows official SpringBoneSetup before SetupSpringBone order"
);
Expect(partPackageExporterSource.Contains("rebuild SpringManager ownership from composed hierarchy"), "part package setup plan rebuilds manager ownership after composition");
Expect(partRuntimeModelsSource.Contains("JsonPropertyName(\"funit\")"), "part runtime spring payload exposes FUnit metadata separately");
Expect(pjskRuntimeModelsSource.Contains("JsonPropertyName(\"funit\")"), "runtime unity setup exposes FUnit metadata separately");
Expect(springBoneExporterSource.Contains("BuildFUnitSummary"), "spring bone exporter detects FUnit metadata");
Expect(springBoneExporterSource.Contains("ScriptNamespace"), "spring bone exporter distinguishes FUnit by MonoScript namespace");
Expect(springBoneExporterSource.Contains("metadata_only; do not merge with UTJ/Sekai SpringBone runtime"), "FUnit detection is explicitly metadata-only");
Expect(!springBoneExporterSource.Contains("FUnit.SpringBone runtime"), "spring bone exporter does not route FUnit into the UTJ runtime path");
Expect(pjskRuntimeModelsSource.Contains("faceRendererName"), "runtime body-head assembly exposes official face renderer predicate name");
Expect(pjskRuntimeModelsSource.Contains("combineNodeAName"), "runtime body-head assembly exposes official combine node A");
Expect(pjskRuntimeModelsSource.Contains("combineNodeBName"), "runtime body-head assembly exposes official combine node B");
Expect(pjskRuntimeModelsSource.Contains("childMoveSuffix"), "runtime body-head assembly exposes official child move suffix");
Expect(pjskRuntimeModelsSource.Contains("PjskUnityRuntimeConstraintSetup"), "runtime unity setup exposes constraint setup metadata");
Expect(pjskRuntimeModelsSource.Contains("PjskUnityRuntimeConstraint"), "runtime unity setup exposes constraint records");
Expect(pjskRuntimeModelsSource.Contains("PjskUnityRuntimeConstraintSource"), "runtime unity setup exposes multi-source constraint records");
Expect(springBoneExporterSource.Contains("Enum.TryParse<ClassIDType>"), "spring exporter probes AssetStudio constraint ClassID support dynamically");
Expect(springBoneExporterSource.Contains("SpringPrefabConstraintCapability"), "spring exporter records AssetStudio constraint capability");
Expect(springBoneExporterSource.Contains("ReadConstraintSources"), "spring exporter reads Unity constraint sources");
Expect(springBoneExporterSource.Contains("m_AimVector"), "spring exporter reads Unity AimConstraint axis fields");
Expect(springBoneExporterSource.Contains("m_WorldUpObject"), "spring exporter reads Unity AimConstraint world-up object");
Expect(springBoneExporterSource.Contains("m_RotationOffsets"), "spring exporter reads Unity per-source rotation offsets");
Expect(springBoneExporterSource.Contains("m_ConstraintActive"), "spring exporter reads Unity runtime constraint activation");
Expect(springBoneExporterSource.Contains("m_Weight"), "spring exporter reads Unity component/source weights");
Expect(springBoneExporterSource.Contains("m_TranslationAxis"), "spring exporter reads ParentConstraint translation-axis masks");
Expect(springBoneExporterSource.Contains("m_RotationAxis"), "spring exporter reads rotation-axis masks");
Expect(pjskRuntimeModelsSource.Contains("aimVector"), "runtime constraint records expose aim vector");
Expect(pjskRuntimeModelsSource.Contains("worldUpObjectPath"), "runtime constraint records expose world-up object path");
Expect(pjskRuntimeModelsSource.Contains("rotationOffset"), "runtime constraint records expose rotation offsets");
Expect(pjskRuntimeModelsSource.Contains("translationAxis"), "runtime constraint records expose translation-axis masks");
Expect(pjskRuntimeModelsSource.Contains("rotationAxis"), "runtime constraint records expose rotation-axis masks");
Expect(pjskRuntimeBuilderSource.Contains("BuildConstraintSetup"), "runtime builder emits constraint setup metadata");
Expect(pjskRuntimeBuilderSource.Contains("ModelUtility.ConstraintSetup"), "runtime setup plan includes official constraint setup step");
Expect(partPackageExporterSource.Contains("repair constraints after composition"), "part package setup plan carries constraint repair through viewer composition");
Expect(partRuntimeModelsSource.Contains("JsonPropertyName(\"constraintSetup\")"), "part runtime spring payload preserves constraint setup metadata");
Expect(partPackageExporterSource.Contains("ConstraintSetup: setup.ConstraintSetup"), "part packages retain their normalized constraint setup");
Expect(partPackageExporterSource.Contains("BuildRuntimeForceProviders(part, manager)"), "part package managers retain UTJ force providers");
Expect(pjskRuntimeBuilderSource.Contains("public static IReadOnlyList<VrmSpringBoneForceProviderCandidate> BuildRuntimeForceProviders"), "part exporter can reuse the canonical UTJ force-provider builder");
Expect(pjskRuntimeBuilderSource.Contains("ParentingMode: \"model_combine_setup\""), "full runtime setup declares official ModelCombineSetup parenting mode");
Expect(pjskRuntimeBuilderSource.Contains("FaceRendererName: \"Face\""), "full runtime setup writes official face renderer predicate");
Expect(pjskRuntimeBuilderSource.Contains("ChildMoveSuffix: \"_target\""), "full runtime setup writes official child move suffix");
Expect(costumeRegistryModelsSource.Contains("headCompositionKind"), "head-hair compatibility rules expose composition kind");
Expect(costumeRegistryModelsSource.Contains("activeContributors"), "head-hair compatibility rules expose active contributors");
Expect(costumeRegistryModelsSource.Contains("PartSourceMap"), "costume registry exposes part source map");
Expect(costumeRegistryModelsSource.Contains("baseSourceKey"), "part registry entries expose base source keys");
Expect(costumeRegistryModelsSource.Contains("sourcePackagePath"), "part registry entries expose shared source package paths");
Expect(costumeRegistryExporterSource.Contains("ResolveHeadHairComposition"), "registry exporter resolves head-hair composition metadata");
Expect(costumeRegistryExporterSource.Contains("complete_head"), "registry exporter marks complete head compositions");
Expect(costumeRegistryExporterSource.Contains("part-source-map.json"), "registry exporter writes part source map");
Expect(costumeRegistryExporterSource.Contains("BuildSourceIdentity"), "registry exporter builds source identities");
Expect(costumeRegistryExporterSource.Contains("SHA256.HashData"), "registry exporter uses stable source key hashes");
Expect(costumeRegistryExporterSource.Contains("parts/_sources/"), "registry exporter points duplicate part ids at shared source package paths");
Expect(costumeRegistryExporterSource.Contains("ResolveAssetBaseDirectoryCandidates"), "registry exporter resolves the characterv2 asset root");
Expect(costumeRegistryExporterSource.Contains("string.Equals(part, \"head_optional\""), "legacy character model roots are limited to head_optional");
Expect(costumeRegistryExporterSource.Contains("\"model\", \"character\", part"), "registry exporter includes legacy head_optional model roots");
Expect(!costumeRegistryExporterSource.Contains("Path.Combine(assetRoot, part)"), "registry exporter omits flat asset roots");
Expect(partPackageExporterSource.Contains("SelectRepresentativePartEntries"), "part package exporter exports each shared source package once");
Expect(partPackageExporterSource.Contains("GroupBy(entry => entry.PackagePath"), "part package exporter groups export work by package path");
Expect(roleRuntimeExporterSource.Contains("ResolveDefaultCostumeSettingMotionPath"), "role runtime exporter auto-resolves costume_setting motion bundles");
Expect(roleRuntimeExporterSource.Contains("LoadRepresentativeRoleCharacter3dIds"), "role runtime exporter exports one representative row per character+unit by default");
Expect(roleRuntimeExporterSource.Contains("LoadCanonicalRoleKeys"), "role runtime exporter filters role output to canonical character+unit roles");
Expect(roleRuntimeExporterSource.Contains("MikuUnitRoles"), "role runtime exporter keeps Miku unit variants");
Expect(roleRuntimeExporterSource.Contains(".haruki-sparse-input"), "sparse incremental input reuses existing role runtime packages");
Expect(roleRuntimeExporterSource.Contains("File.Exists(primaryRuntimePath)"), "sparse role reuse requires a completed runtime package");
Expect(roleRuntimeExporterSource.Contains("entry.Id >= 22 && entry.Id <= 26"), "role runtime exporter keeps non-Miku virtual singers on piapro only");
Expect(programSource.Contains("RunRoleRuntimeWorkers"), "program can export representative roles through worker processes");
Expect(programSource.Contains("Started role runtime worker"), "role runtime worker mode reports process shards");
Expect(roleRuntimeExporterSource.Contains("character\", \"motion\", \"costume_setting\""), "role runtime exporter scans character motion costume_setting directory");
Expect(roleRuntimeExporterSource.Contains("\"light_sound\" => 27"), "role runtime exporter maps Leo/need Miku motion to 27_00");
Expect(roleRuntimeExporterSource.Contains("\"idol\" => 28"), "role runtime exporter maps idol Miku motion to 28_00");
Expect(roleRuntimeExporterSource.Contains("\"street\" => 29"), "role runtime exporter maps street Miku motion to 29_00");
Expect(roleRuntimeExporterSource.Contains("\"theme_park\" => 30"), "role runtime exporter maps theme park Miku motion to 30_00");
Expect(roleRuntimeExporterSource.Contains("\"school_refusal\" => 31"), "role runtime exporter maps school refusal Miku motion to 31_00");
Expect(roleRuntimeExporterSource.Contains("_ => 21"), "role runtime exporter keeps piapro/default Miku motion at 21_00");
var inventoryModelsSource = File.ReadAllText(Path.Combine(repoRoot, "Models", "InventoryModels.cs"));
var assetStudioBundleParserSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "AssetStudioBundleParser.cs"));
Expect(inventoryModelsSource.Contains("RenderMaterialSlotInventory"), "inventory records renderer material slots with identity");
Expect(inventoryModelsSource.Contains("MaterialKey"), "inventory exposes material identity keys");
Expect(assetStudioBundleParserSource.Contains("m_FileID"), "bundle parser preserves renderer material file ids");
Expect(assetStudioBundleParserSource.Contains("m_PathID"), "bundle parser preserves renderer material path ids");
Expect(assetStudioBundleParserSource.Contains("ConvertToStream(ImageFormat.Png, false)"), "bundle parser exports correctly oriented PNGs once");
Expect(!assetStudioBundleParserSource.Contains("Image.Load"), "bundle parser does not decode and re-encode exported PNGs");
Expect(assetStudioImportedModelFactorySource.Contains("new ModelConverter(preferredRoot, ImageFormat.Png, null, convertModelTextures)"), "part model conversion uses the final AssetStudio constructor directly");
Expect(!assetStudioImportedModelFactorySource.Contains("GetConstructor"), "part model conversion omits reflective AssetStudio fallbacks");
Expect(!assetStudioImportedModelFactorySource.Contains("NormalizeTextureOrientation"), "part model conversion omits legacy texture repair");
Expect(partPackageExporterSource.Contains("MaterialIdentityLookup"), "part package exporter resolves materials by identity");
Expect(!partPackageExporterSource.Contains("BuildMaterialMap"), "part package exporter no longer indexes materials by display name");
Expect(partPackageExporterSource.Contains("part-export-error.json"), "part package exporter writes per-package errors during full export");
Expect(partPackageExporterSource.Contains("Part package export skipped"), "part package exporter continues after per-package export failures");
Expect(partPackageExporterSource.Contains("DeletePartExportError"), "part package exporter removes stale per-package errors after success");
Expect(partPackageExporterSource.Contains("IsInShard"), "part package exporter can filter deterministic shards");
Expect(partPackageManifestSource.Contains("public static void Rebuild"), "part package exporter rebuilds one canonical worker manifest");
Expect(partPackageManifestSource.Contains("HARUKI_3D_MISSING_BUNDLE="), "sparse export reports exact missing bundle keys for targeted updater recovery");
Expect(partPackageManifestSource.Contains("RecoveredFromCorruption"), "part package manifest records corrupt-manifest recovery without serializing it");
Expect(partPackageExporterSource.Contains("manifest.EnsureUsableForSparseInput()"), "sparse incremental export refuses to reuse stamps from a corrupt manifest");
Expect(partPackageManifestSource.Contains("ContentAddressedFile.Replace"), "part package manifest saves atomically through the shared temp+rename helper");
Expect(!partPackageManifestSource.Contains("File.WriteAllText(manifestPath"), "part package manifest never writes the final manifest path directly");
var contentAddressedStoreSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "ContentAddressedStore.cs"));
Expect(contentAddressedStoreSource.Contains("ContentAddressedFile.Replace"), "CAS state and report files publish atomically through the shared temp+rename helper");
Expect(!contentAddressedStoreSource.Contains("File.WriteAllBytes(\n            Path.Combine"), "CAS state and report files are never written to their final paths directly");
Expect(!partPackageExporterSource.Contains("bundle-open-summary.json"), "part package exporter omits per-package debug summaries from production output");
Expect(partPackageExporterSource.Contains("missing_after_fallback"), "part package exporter marks material failures after full-directory fallback");
Expect(assetStudioLoadedBundleSource.Contains("ResolveLoadBundlePaths"), "loaded bundle uses shared dependency resolver");
Expect(assetStudioLoadedBundleSource.Contains("dependencyBundlePaths"), "loaded bundle includes dependency-index paths");
Expect(partPackageExporterSource.Contains("ResolveDependencyBundlePaths"), "part package exporter loads dependency-index bundles");
Expect(partPackageExporterSource.Contains("Unresolved _EyelashMaskTex dependency"), "part package exporter reports unresolved through-hair masks");
Expect(partPackageExporterSource.Contains("\"eye\" or \"eyelash\" or \"eyebrow\""), "part package exporter requires the real through-hair mask for every stencil overlay source");
Expect(partPackageExporterSource.Contains("if (!resolvedMask)"), "incremental export rejects overlay materials with no real eyelash mask reference");
Expect(bundleDependencyResolverSource.Contains("BundleLoadDependencyMode.FullDirectory"), "bundle dependency resolver supports full-directory fallback");
var nonexistentCompressedBundlePattern = "\"*.bundle" + ".gz\"";
Expect(!bundleDependencyResolverSource.Contains(nonexistentCompressedBundlePattern), "bundle dependency resolver only scans plain bundles");
Expect(!bundleInputResolverSource.Contains(nonexistentCompressedBundlePattern), "bundle input resolver only accepts plain bundle inputs");
Expect(!sekaiBundleDecryptorSource.Contains("IsGzipBundle"), "bundle decryptor does not special-case nonexistent gzip bundles");
Expect(partPackageExporterSource.Contains("MissingMaterialReferenceException"), "part package exporter retries missing material references");
Expect(partPackageExporterSource.Contains("Recovered missing material reference"), "part package exporter records material dependency fallback warnings");
Expect(materialIdentityLookupSource.Contains("MissingMaterialReferenceException"), "material lookup raises a typed missing reference error");

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
}

static string FindRepoRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var current = new DirectoryInfo(start);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Services", "PartPackageExporter.cs")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
    }
    throw new DirectoryNotFoundException("Could not locate Haruki-3D-Exporter repo root.");
}

static void WriteRuntimePackage(
    string packageDirectory,
    string texturePath,
    byte[] textureBytes
)
{
    var textureFile = Path.Combine(packageDirectory, texturePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(textureFile)!);
    File.WriteAllBytes(textureFile, textureBytes);
    RuntimeJsonWriter.Write(
        Path.Combine(packageDirectory, "part-runtime.json"),
        new
        {
            characterTextures = new Dictionary<string, string>
            {
                ["main"] = texturePath
            },
            materialSlots = new[]
            {
                new
                {
                    mainTex = texturePath,
                    shadowTex = (string?)null,
                    valueTex = (string?)null,
                    faceShadowTex = (string?)null,
                    rawMaterial = new
                    {
                        textureProperties = new[]
                        {
                            new { name = "_MainTex", colorSpace = 0, uri = texturePath }
                        }
                    }
                }
            },
            textureRoles = new[]
            {
                new
                {
                    part = "body",
                    materialKey = "0:1",
                    materialFileId = 0,
                    materialPathId = 1,
                    materialName = "mat",
                    materialKind = "body",
                    role = "main",
                    uri = texturePath
                }
            }
        },
        new JsonSerializerOptions()
    );
}

static string CasTexturePath(string outputDirectory) =>
    Path.Combine(outputDirectory, "_texture_store", "sha256", "aa", "texture.png");

static string CasPartRuntimePath(string outputDirectory) =>
    Path.Combine(outputDirectory, "parts", "_sources", "body", "source", "part-runtime.msgpack.br");

static void WriteCasFixture(string outputDirectory)
{
    var texturePath = CasTexturePath(outputDirectory);
    Directory.CreateDirectory(Path.GetDirectoryName(texturePath)!);
    File.WriteAllBytes(texturePath, new byte[] { 1, 3, 3, 7 });
    RuntimeJsonWriter.Write(
        Path.Combine(outputDirectory, "parts", "_sources", "body", "source", "part-runtime.json"),
        new { version = "cas", positions = new[] { 0f, 1f, 2f } },
        new JsonSerializerOptions()
    );
}

static void WriteJsonFile(string path, object payload)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, JsonSerializer.Serialize(payload));
}

static PartRegistryEntry PartEntry(
    string root,
    string name,
    int bytes,
    string sourceKey,
    string? packagePath = null
)
{
    var path = Path.Combine(root, $"haruki-work-{Guid.NewGuid():N}.bundle");
    File.WriteAllBytes(path, new byte[bytes]);
    return new PartRegistryEntry(
        1, "body", 1, null, name, 1, null, 1, 1, 0,
        null, null, null, null, null, path, null, sourceKey, sourceKey, null,
        packagePath ?? $"parts/body/{name}", null, "ready", Array.Empty<string>()
    );
}

static JsonObject ReadRuntimePackage(string runtimeJsonPath)
{
    using var document = RuntimeJsonWriter.ReadJsonDocument(runtimeJsonPath);
    return JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
}

static byte[] ReadBrotliBytes(string path)
{
    using var input = File.OpenRead(path);
    using var brotli = new BrotliStream(input, CompressionMode.Decompress);
    using var output = new MemoryStream();
    brotli.CopyTo(output);
    return output.ToArray();
}

static bool ContainsRuntimeBinaryExtension(byte[] messagePack)
{
    for (var index = 0; index + 2 < messagePack.Length; index += 1)
    {
        if (messagePack[index] == 0xc7 && messagePack[index + 2] == RuntimeJsonWriter.BinaryArrayExtensionType)
        {
            return true;
        }
        if (index + 3 < messagePack.Length && messagePack[index] == 0xc8 && messagePack[index + 3] == RuntimeJsonWriter.BinaryArrayExtensionType)
        {
            return true;
        }
        if (index + 5 < messagePack.Length && messagePack[index] == 0xc9 && messagePack[index + 5] == RuntimeJsonWriter.BinaryArrayExtensionType)
        {
            return true;
        }
    }
    return false;
}

enum DirectWriterState
{
    Ready,
}

sealed record DirectWriterFixture(
    string DisplayName,
    DirectWriterState State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Optional
);
