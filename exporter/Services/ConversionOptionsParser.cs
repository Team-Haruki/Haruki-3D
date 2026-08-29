using PjskBundle2Parts.Models;
using System.Text.Json;

namespace PjskBundle2Parts.Services;

public static class ConversionOptionsParser
{
    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string Usage =>
        "Usage:\n" +
        "  Haruki-3D-Exporter --emit-costume-registries --master <master-directory> --asset-root <AssetBundles-root> --out <directory>\n" +
        "  Haruki-3D-Exporter --emit-runtime-role-catalog --master <master-directory> --out <directory>\n" +
        "  Haruki-3D-Exporter --emit-part-packages --part-costume3d-id <id> --part-type <body|head|hair|head_optional> --master <master-directory> --asset-root <AssetBundles-root> --out <directory> [--part-unit <unit>]\n\n" +
        "  Haruki-3D-Exporter --emit-role-runtimes [--role-character3d-id <id>] --master <master-directory> --asset-root <AssetBundles-root> --out <directory> [--motion <bundle-or-export-folder>]\n" +
        "  Haruki-3D-Exporter --emit-mv-source-set --mv-manifest <manifest.json> --asset-root <raw-bundle-root> --out <directory>\n" +
        "  Haruki-3D-Exporter --export-face-motion --motion <bundle-or-decoded-folder-or-json> --out <face_motion.json-or-directory> [--source-path <bundle-path>]\n\n" +
        "  Add --config <json> to load defaults from haruki-3d-exporter.config.json.\n\n" +
        "Notes:\n" +
        "  --master provides the masterdata used to resolve runtime roles and parts\n" +
        "  --asset-root points at the AssetBundles root containing live_pv/model/characterv2\n" +
        "  --emit-costume-registries writes .msgpack.br character, part, compatibility, and unlock registries\n" +
        "  --emit-part-packages writes core+delta part-runtime.msgpack.br packages for runtime custom assembly\n" +
        "  --emit-role-runtimes writes roles/<characterId>/<unit>/role-runtime.msgpack.br with motion metadata; without --role-character3d-id it exports one representative row per character+unit role\n" +
        "  --manifest records part package input file stamps for incremental --emit-part-packages runs\n" +
        "  --part-package-process-concurrency runs role or full part exports across N workers; 0 = auto CPU count\n" +
        "  --part-package-workers and --part-package-core-count are aliases for --part-package-process-concurrency\n" +
        "  --part-package-shard-count and --part-package-shard-index run one deterministic package shard\n" +
        "  --part-package-claim-directory coordinates cooperating exporter processes through atomic claim files so each package group is exported once\n" +
        "  --part-package-work-list limits a worker to a planner-written work list JSON and writes worker metrics to <work-list>.summary.json\n" +
        "  --assetstudio-log-level controls AssetStudio logs: warning, info, or debug\n" +
        "  --convert-model-textures controls AssetStudio model texture conversion: true or false\n" +
        "  --compact-textures deduplicates package textures by exact SHA-256 and rewrites runtime package paths\n" +
        "  --shared-content-store hard-links exact texture and part-runtime bytes into a shared cross-region CAS\n" +
        "  --bundle-hash-index reuses updater-provided SHA-256 values when fingerprinting source bundles\n" +
        "  --bundle-dependency-index preserves updater-provided logical bundle dependency closure\n" +
        "  --emit-mv-source-set validates and stages a manifest-selected MV bundle closure; output remains source-platform data and must be rebuilt for WebGL\n" +
        "  --png-optimize controls lossless PNG optimization during compaction: oxipng or off\n" +
        "  --texture-format selects final runtime textures: png (default) or ktx2\n" +
        "  --texture-compact-workers limits concurrent PNG optimizers; 0 = min(4, CPU count)\n" +
        "  --export-face-motion writes face_motion.json from a costume_setting bundle or decoded AnimationClip JSON without Python helpers\n" +
        "  --motion accepts a costume_setting bundle or a folder containing unity-motion.json/face_motion.json/light_motion.json\n" +
        "  runtime metadata is always emitted as Brotli-compressed MessagePack";

    public static ParseResult Parse(string[] args)
    {
        var state = new OptionState { ConfigPath = FindConfigPath(args) };
        var configError = ApplyConfig(state);
        if (configError is not null)
        {
            return Failure(configError);
        }
        if (args.Length == 0 && string.IsNullOrWhiteSpace(state.ConfigPath))
        {
            return Failure("Missing arguments.");
        }

        var argumentError = ApplyArguments(args, state);
        if (argumentError is not null)
        {
            return Failure(argumentError);
        }
        var validationError = ValidateOperation(state) ?? ValidateGeneralOptions(state);
        return validationError is null
            ? new ParseResult(true, BuildOptions(state), string.Empty)
            : Failure(validationError);
    }

    private static string? FindConfigPath(string[] args)
    {
        string? configPath = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config")
            {
                configPath = ReadValue(args, ref i, args[i]);
            }
        }
        return string.IsNullOrWhiteSpace(configPath) && File.Exists("haruki-3d-exporter.config.json")
            ? "haruki-3d-exporter.config.json"
            : configPath;
    }

    private static string? ApplyConfig(OptionState state)
    {
        if (string.IsNullOrWhiteSpace(state.ConfigPath))
        {
            return null;
        }
        try
        {
            CopyConfigValues(LoadConfig(state.ConfigPath), state);
            return null;
        }
        catch (Exception ex)
        {
            return $"Failed to read --config {state.ConfigPath}: {ex.Message}";
        }
    }

    private static void CopyConfigValues(ExporterConfig config, OptionState state)
    {
        state.Output = config.Output;
        state.Motion = config.Motion;
        state.MasterDirectory = config.Master;
        state.AssetRoot = config.AssetRoot;
        state.EmitCostumeRegistries = config.EmitCostumeRegistries ?? false;
        state.EmitRuntimeRoleCatalog = config.EmitRuntimeRoleCatalog ?? false;
        state.EmitPartPackages = config.EmitPartPackages ?? false;
        state.EmitRoleRuntimes = config.EmitRoleRuntimes ?? false;
        state.ExportFaceMotion = config.ExportFaceMotion ?? false;
        state.PartCostume3dId = config.PartCostume3dId;
        state.PartType = config.PartType;
        state.PartUnit = config.PartUnit;
        state.RoleCharacter3dIds = config.RoleCharacter3dIds?.Distinct().ToList() ?? new List<int>();
        state.SourcePath = config.SourcePath;
        state.ManifestPath = config.Manifest;
        state.PartPackageProcessConcurrency = config.PartPackageProcessConcurrency ??
            config.PartPackageWorkers ?? config.PartPackageCoreCount ?? 1;
        state.PartPackageShardCount = config.PartPackageShardCount ?? 1;
        state.PartPackageShardIndex = config.PartPackageShardIndex ?? 0;
        state.PartPackageClaimDirectory = config.PartPackageClaimDirectory;
        state.AssetStudioLogLevel = DefaultWhenBlank(config.AssetStudioLogLevel, "warning");
        state.CompactTextures = config.CompactTextures ?? false;
        state.OptimizeTextureStore = config.OptimizeTextureStore ?? false;
        state.SharedContentStore = config.SharedContentStore;
        state.CompiledContentStore = config.CompiledContentStore;
        state.PngOptimize = DefaultWhenBlank(config.PngOptimize, "oxipng");
        state.TextureFormat = DefaultWhenBlank(config.TextureFormat, "png");
        state.TextureCompactWorkers = config.TextureCompactWorkers ?? 0;
        state.ConvertModelTextures = config.ConvertModelTextures ?? false;
        state.PartPackageWorkList = config.PartPackageWorkList;
        state.BundleHashIndex = config.BundleHashIndex;
        state.BundleDependencyIndex = config.BundleDependencyIndex;
        state.EmitMvSourceSet = config.EmitMvSourceSet ?? false;
        state.MvManifestPath = config.MvManifest;
    }

    private static string DefaultWhenBlank(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string? ApplyArguments(string[] args, OptionState state)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var error = ApplyArgument(args, ref i, state);
            if (error is not null)
            {
                return error;
            }
        }
        return null;
    }

    private static string? ApplyArgument(string[] args, ref int index, OptionState state)
    {
        var arg = args[index];
        switch (arg)
        {
            case "--config": _ = ReadValue(args, ref index, arg); break;
            case "--out": case "-o": state.Output = ReadValue(args, ref index, arg); break;
            case "--motion": case "-m": state.Motion = ReadValue(args, ref index, arg); break;
            case "--master": state.MasterDirectory = ReadValue(args, ref index, arg); break;
            case "--asset-root": state.AssetRoot = ReadValue(args, ref index, arg); break;
            case "--emit-costume-registries": state.EmitCostumeRegistries = true; break;
            case "--emit-runtime-role-catalog": state.EmitRuntimeRoleCatalog = true; break;
            case "--emit-part-packages": state.EmitPartPackages = true; break;
            case "--emit-role-runtimes": state.EmitRoleRuntimes = true; break;
            case "--export-face-motion": state.ExportFaceMotion = true; break;
            case "--part-costume3d-id":
                return TryAddInteger(args, ref index, arg, value => state.PartCostume3dId = value);
            case "--part-type": state.PartType = ReadValue(args, ref index, arg); break;
            case "--part-unit": state.PartUnit = ReadValue(args, ref index, arg); break;
            case "--role-character3d-id":
                return TryAddInteger(args, ref index, arg, value => state.RoleCharacter3dIds.Add(value));
            case "--source-path": state.SourcePath = ReadValue(args, ref index, arg); break;
            case "--manifest": state.ManifestPath = ReadValue(args, ref index, arg); break;
            case "--part-package-process-concurrency":
            case "--part-package-workers":
            case "--part-package-core-count":
                state.PartPackageProcessConcurrency = ReadIntValue(args, ref index, arg); break;
            case "--assetstudio-log-level": state.AssetStudioLogLevel = ReadValue(args, ref index, arg); break;
            case "--compact-textures": state.CompactTextures = true; break;
            case "--optimize-texture-store": state.OptimizeTextureStore = true; break;
            case "--shared-content-store": state.SharedContentStore = ReadValue(args, ref index, arg); break;
            case "--compiled-content-store": state.CompiledContentStore = ReadValue(args, ref index, arg); break;
            case "--png-optimize": state.PngOptimize = ReadValue(args, ref index, arg); break;
            case "--texture-format": state.TextureFormat = ReadValue(args, ref index, arg); break;
            case "--texture-compact-workers": state.TextureCompactWorkers = ReadIntValue(args, ref index, arg); break;
            case "--convert-model-textures": state.ConvertModelTextures = ReadBoolValue(args, ref index, arg); break;
            case "--part-package-shard-count": state.PartPackageShardCount = ReadIntValue(args, ref index, arg); break;
            case "--part-package-shard-index": state.PartPackageShardIndex = ReadIntValue(args, ref index, arg); break;
            case "--part-package-claim-directory": state.PartPackageClaimDirectory = ReadValue(args, ref index, arg); break;
            case "--part-package-work-list": state.PartPackageWorkList = ReadValue(args, ref index, arg); break;
            case "--bundle-hash-index": state.BundleHashIndex = ReadValue(args, ref index, arg); break;
            case "--bundle-dependency-index": state.BundleDependencyIndex = ReadValue(args, ref index, arg); break;
            case "--emit-mv-source-set": state.EmitMvSourceSet = true; break;
            case "--mv-manifest": state.MvManifestPath = ReadValue(args, ref index, arg); break;
            case "--help": case "-?": return "Help requested.";
            default: return $"Unknown argument: {arg}";
        }
        return null;
    }

    private static string? TryAddInteger(string[] args, ref int index, string arg, Action<int> assign)
    {
        var value = ReadValue(args, ref index, arg);
        if (!int.TryParse(value, out var parsed))
        {
            return $"Option {arg} must be an integer.";
        }
        assign(parsed);
        return null;
    }

    private static string? ValidateOperation(OptionState state)
    {
        if (state.ExportFaceMotion)
        {
            return string.IsNullOrWhiteSpace(state.Motion)
                ? "Missing --motion for --export-face-motion."
                : null;
        }
        if (state.OptimizeTextureStore)
        {
            return null;
        }
        if (state.EmitRuntimeRoleCatalog)
        {
            return string.IsNullOrWhiteSpace(state.MasterDirectory)
                ? "Missing --master for --emit-runtime-role-catalog."
                : null;
        }
        if (state.EmitMvSourceSet)
        {
            return ValidateMvSourceSet(state);
        }
        return state.EmitCostumeRegistries || state.EmitPartPackages || state.EmitRoleRuntimes
            ? ValidateRegistryOperation(state)
            : "Missing final pipeline operation.";
    }

    private static string? ValidateMvSourceSet(OptionState state)
    {
        if (string.IsNullOrWhiteSpace(state.AssetRoot))
        {
            return "Missing --asset-root for --emit-mv-source-set.";
        }
        return string.IsNullOrWhiteSpace(state.MvManifestPath)
            ? "Missing --mv-manifest for --emit-mv-source-set."
            : null;
    }

    private static string? ValidateRegistryOperation(OptionState state)
    {
        var mode = ResolveRegistryModeName(state.EmitPartPackages, state.EmitRoleRuntimes);
        if (string.IsNullOrWhiteSpace(state.MasterDirectory))
        {
            return $"Missing --master for {mode}.";
        }
        if (string.IsNullOrWhiteSpace(state.AssetRoot))
        {
            return $"Missing --asset-root for {mode}.";
        }
        return state.EmitPartPackages && !state.EmitCostumeRegistries &&
            (state.PartCostume3dId is null) != string.IsNullOrWhiteSpace(state.PartType)
                ? "--part-costume3d-id and --part-type must be used together."
                : null;
    }

    private static string? ValidateGeneralOptions(OptionState state)
    {
        if (string.IsNullOrWhiteSpace(state.Output))
            return "Missing --out.";
        if (state.PartPackageProcessConcurrency < 0)
            return "--part-package-process-concurrency must be 0 or greater.";
        if (state.PartPackageShardCount < 1)
            return "--part-package-shard-count must be at least 1.";
        if (state.PartPackageShardIndex < 0 || state.PartPackageShardIndex >= state.PartPackageShardCount)
            return "--part-package-shard-index must be between 0 and shard-count - 1.";
        if (!IsValidAssetStudioLogLevel(state.AssetStudioLogLevel))
            return "--assetstudio-log-level must be warning, info, or debug.";
        if (!IsValidPngOptimizeMode(state.PngOptimize))
            return "--png-optimize must be oxipng or off.";
        if (!IsValidTextureFormat(state.TextureFormat))
            return "--texture-format must be png or ktx2.";
        if (state.TextureCompactWorkers < 0)
            return "--texture-compact-workers must be 0 or greater.";
        if (state.PartPackageProcessConcurrency != 1 && state.PartPackageShardCount > 1)
            return "--part-package-process-concurrency cannot be combined with manual shard options.";
        if (state.EmitPartPackages && state.PartCostume3dId is not null &&
            (state.PartPackageProcessConcurrency != 1 || state.PartPackageShardCount > 1 || state.PartPackageShardIndex != 0))
            return "Part package process concurrency and shards are only supported for full --emit-part-packages.";
        return null;
    }

    private static ConversionOptions BuildOptions(OptionState state)
    {
        return new ConversionOptions(
            state.Output!, state.Motion, state.MasterDirectory, state.AssetRoot,
            state.EmitCostumeRegistries, state.EmitRuntimeRoleCatalog, state.EmitPartPackages,
            state.EmitRoleRuntimes, state.ExportFaceMotion, state.PartCostume3dId,
            NormalizePartType(state.PartType), BlankToNull(state.PartUnit),
            state.RoleCharacter3dIds.Distinct().ToList(), BlankToNull(state.SourcePath),
            BlankToNull(state.ManifestPath), state.PartPackageProcessConcurrency,
            state.PartPackageShardCount, state.PartPackageShardIndex,
            BlankToNull(state.PartPackageClaimDirectory),
            state.AssetStudioLogLevel.Trim().ToLowerInvariant(), state.CompactTextures,
            state.OptimizeTextureStore, BlankToNull(state.SharedContentStore),
            BlankToNull(state.CompiledContentStore), NormalizePngOptimizeMode(state.PngOptimize),
            NormalizeTextureFormat(state.TextureFormat), state.TextureCompactWorkers,
            state.ConvertModelTextures, BlankToNull(state.PartPackageWorkList),
            BlankToNull(state.BundleHashIndex), BlankToNull(state.BundleDependencyIndex),
            state.EmitMvSourceSet, BlankToNull(state.MvManifestPath)
        );
    }

    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static ParseResult Failure(string error) => new(false, null, error);

    private static string? NormalizePartType(string? partType)
    {
        if (string.IsNullOrWhiteSpace(partType))
        {
            return null;
        }

        return partType.Trim().ToLowerInvariant() switch
        {
            "body" => "body",
            "head" => "head",
            "hair" => "hair",
            "head_optional" or "accessory" => "head_optional",
            var value => value,
        };
    }

    private static string ResolveRegistryModeName(bool emitPartPackages, bool emitRoleRuntimes)
    {
        if (emitRoleRuntimes)
        {
            return "--emit-role-runtimes";
        }
        return emitPartPackages ? "--emit-part-packages" : "--emit-costume-registries";
    }

    private static bool IsValidAssetStudioLogLevel(string value)
    {
        return value.Trim().ToLowerInvariant() is "warning" or "info" or "debug";
    }

    private static bool IsValidPngOptimizeMode(string value)
    {
        return value.Trim().ToLowerInvariant() is "oxipng" or "off";
    }

    private static string NormalizePngOptimizeMode(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static bool IsValidTextureFormat(string value)
    {
        return value.Trim().ToLowerInvariant() is "png" or "ktx2";
    }

    private static string NormalizeTextureFormat(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static ExporterConfig LoadConfig(string configPath)
    {
        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<ExporterConfig>(
            json,
            ReadJsonOptions
        ) ?? throw new InvalidOperationException("config JSON is empty.");
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Option {optionName} requires a value.");
        }
        index += 1;
        return args[index];
    }

    private static int ReadIntValue(string[] args, ref int index, string optionName)
    {
        var value = ReadValue(args, ref index, optionName);
        if (!int.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Option {optionName} must be an integer.");
        }
        return parsed;
    }

    private static bool ReadBoolValue(string[] args, ref int index, string optionName)
    {
        var value = ReadValue(args, ref index, optionName);
        if (!bool.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Option {optionName} must be true or false.");
        }
        return parsed;
    }

    private sealed class OptionState
    {
        public string? Output { get; set; }
        public string? Motion { get; set; }
        public string? MasterDirectory { get; set; }
        public string? AssetRoot { get; set; }
        public bool EmitCostumeRegistries { get; set; }
        public bool EmitRuntimeRoleCatalog { get; set; }
        public bool EmitPartPackages { get; set; }
        public bool EmitRoleRuntimes { get; set; }
        public bool ExportFaceMotion { get; set; }
        public int? PartCostume3dId { get; set; }
        public string? PartType { get; set; }
        public string? PartUnit { get; set; }
        public List<int> RoleCharacter3dIds { get; set; } = new();
        public string? SourcePath { get; set; }
        public string? ManifestPath { get; set; }
        public string? ConfigPath { get; set; }
        public int PartPackageProcessConcurrency { get; set; } = 1;
        public int PartPackageShardCount { get; set; } = 1;
        public int PartPackageShardIndex { get; set; }
        public string? PartPackageClaimDirectory { get; set; }
        public string AssetStudioLogLevel { get; set; } = "warning";
        public bool CompactTextures { get; set; }
        public bool OptimizeTextureStore { get; set; }
        public string? SharedContentStore { get; set; }
        public string? CompiledContentStore { get; set; }
        public string PngOptimize { get; set; } = "oxipng";
        public string TextureFormat { get; set; } = "png";
        public int TextureCompactWorkers { get; set; }
        public bool ConvertModelTextures { get; set; }
        public string? PartPackageWorkList { get; set; }
        public string? BundleHashIndex { get; set; }
        public string? BundleDependencyIndex { get; set; }
        public bool EmitMvSourceSet { get; set; }
        public string? MvManifestPath { get; set; }
    }
}
