using System.Security.Cryptography;
using System.Text.Json;
using PjskBundle2Parts.Models;

namespace PjskBundle2Parts.Services;

public sealed class MvSourceSetExporter
{
    private static readonly byte[] UnityFsMagic = "UnityFS"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public MvSourceSetExportResult Export(string manifestPath, string assetRoot, string outputDirectory)
    {
        var manifest = JsonSerializer.Deserialize<MvSourceManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("MV manifest JSON is empty.");

        if (manifest.MusicId <= 0)
        {
            throw new InvalidOperationException("MV manifest music_id must be positive.");
        }
        if (manifest.Bundles is null || manifest.Bundles.Count == 0)
        {
            throw new InvalidOperationException("MV manifest has no bundles.");
        }

        var fullAssetRoot = Path.GetFullPath(assetRoot);
        var entries = new List<MvSourceSetEntry>(manifest.Bundles.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var planned in manifest.Bundles)
        {
            names.Add(NormalizeBundleName(planned.Bundle));
        }
        if (names.Count != manifest.Bundles.Count)
        {
            throw new InvalidOperationException("MV manifest contains duplicate bundle names.");
        }

        var missingDependencies = manifest.Bundles
            .SelectMany(planned => planned.Dependencies ?? Array.Empty<string>())
            .Select(NormalizeBundleName)
            .Where(dependency => !names.Contains(dependency))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missingDependencies.Length > 0)
        {
            throw new InvalidOperationException(
                $"MV manifest dependency closure is incomplete: {string.Join(", ", missingDependencies)}"
            );
        }

        foreach (var planned in manifest.Bundles)
        {
            var name = NormalizeBundleName(planned.Bundle);
            var sourcePath = ResolveBundlePath(fullAssetRoot, name);
            using var readableBundle = new SekaiBundleDecryptor().PrepareReadableBundle(sourcePath);
            ValidateUnityFs(readableBundle.Path, name);
            var relativeFile = $"source_bundles/{name}.bundle";
            var targetPath = Path.Combine(outputDirectory, relativeFile.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(readableBundle.Path, targetPath, overwrite: true);

            using var source = File.OpenRead(readableBundle.Path);
            var sha256 = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
            entries.Add(new MvSourceSetEntry(
                name,
                ClassifyBundle(name),
                relativeFile,
                (planned.Dependencies ?? Array.Empty<string>())
                    .Select(NormalizeBundleName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                source.Length,
                sha256
            ));
        }

        var totalBytes = entries.Sum(entry => entry.Size);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(
            Path.Combine(outputDirectory, "mv-source-set.json"),
            JsonSerializer.Serialize(new MvSourceSet(
                manifest.MusicId,
                manifest.MusicTitle,
                manifest.AssetVersion,
                manifest.AssetHash,
                "source",
                entries.Count,
                totalBytes,
                entries
            ), JsonOptions)
        );
        File.WriteAllText(
            Path.Combine(outputDirectory, "deps.json"),
            JsonSerializer.Serialize(new MvDependencySet(
                entries.Select(entry => entry.Name).ToArray(),
                entries.Select(entry => new MvDependencyEntry(entry.Name, entry.Dependencies)).ToArray()
            ), JsonOptions)
        );

        return new MvSourceSetExportResult(manifest.MusicId, entries.Count, totalBytes);
    }

    private static string ResolveBundlePath(string assetRoot, string name)
    {
        var relative = name.Replace('/', Path.DirectorySeparatorChar) + ".bundle";
        var path = Path.GetFullPath(Path.Combine(assetRoot, relative));
        var rootPrefix = assetRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Bundle path escapes asset root: '{name}'.");
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"MV bundle '{name}' was not found.", path);
        }
        return path;
    }

    private static string NormalizeBundleName(string value)
    {
        var name = value?.Trim().Replace('\\', '/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)
            || name.StartsWith("/", StringComparison.Ordinal)
            || name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)
            || name.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidOperationException($"Invalid logical bundle name '{value}'.");
        }
        return name;
    }

    private static string ClassifyBundle(string name)
    {
        if (name.StartsWith("live_pv/timeline/", StringComparison.Ordinal))
        {
            return $"timeline_{name[(name.LastIndexOf('/') + 1)..]}";
        }

        return name switch
        {
            _ when name.StartsWith("live_pv/mv_data/", StringComparison.Ordinal) => "mv_data",
            _ when name.StartsWith("live_pv/model/stage_decoration/", StringComparison.Ordinal) => "stage_decoration",
            _ when name.StartsWith("live_pv/model/stage_override_texture/", StringComparison.Ordinal) => "stage_override_texture",
            _ when name.StartsWith("live_pv/model/stage/", StringComparison.Ordinal) => "stage",
            _ when name.StartsWith("live_pv/model/penlight/", StringComparison.Ordinal) => "penlight",
            _ when name.StartsWith("live_pv/model/camera_decoration/", StringComparison.Ordinal) => "camera_decoration",
            _ when name.StartsWith("live_pv/model/character/color_variation/body/", StringComparison.Ordinal) => "character_body_color",
            _ when name.StartsWith("live_pv/model/character/color_variation/head_optional/", StringComparison.Ordinal) => "character_head_optional_color",
            _ when name.StartsWith("live_pv/model/character/head_optional/", StringComparison.Ordinal) => "character_head_optional",
            _ when name.StartsWith("live_pv/model/character/body/", StringComparison.Ordinal) => "character_body",
            _ when name.StartsWith("live_pv/model/character/face/", StringComparison.Ordinal) => "character_face",
            _ when name.StartsWith("live_pv/model/characterv2/color_variation/body/", StringComparison.Ordinal) => "character_body_color",
            _ when name.StartsWith("live_pv/model/characterv2/color_variation/head_optional/", StringComparison.Ordinal) => "character_head_optional_color",
            _ when name.StartsWith("live_pv/model/characterv2/head_optional/", StringComparison.Ordinal) => "character_head_optional",
            _ when name.StartsWith("live_pv/model/characterv2/body/", StringComparison.Ordinal) => "character_body",
            _ when name.StartsWith("live_pv/model/characterv2/face/", StringComparison.Ordinal) => "character_face",
            _ when name.StartsWith("shader/", StringComparison.Ordinal) => "shader",
            _ => "other",
        };
    }

    private static void ValidateUnityFs(string path, string name)
    {
        Span<byte> header = stackalloc byte[UnityFsMagic.Length];
        using var stream = File.OpenRead(path);
        if (stream.Read(header) != header.Length || !header.SequenceEqual(UnityFsMagic))
        {
            throw new InvalidDataException($"MV bundle '{name}' is not a deobfuscated UnityFS file.");
        }
    }
}
