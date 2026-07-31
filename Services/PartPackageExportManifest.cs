using System.Text.Json;
using PjskBundle2Parts.Models;

namespace PjskBundle2Parts.Services;

public sealed class PartPackageExportManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string? manifestPath;
    private readonly Dictionary<string, PartPackageInputStamp> packages;

    private PartPackageExportManifest(
        string? manifestPath,
        Dictionary<string, PartPackageInputStamp> packages
    )
    {
        this.manifestPath = manifestPath;
        this.packages = packages;
    }

    public static PartPackageExportManifest Load(string? manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return new PartPackageExportManifest(
                manifestPath,
                new Dictionary<string, PartPackageInputStamp>(StringComparer.Ordinal)
            );
        }

        var packages = JsonSerializer.Deserialize<Dictionary<string, PartPackageInputStamp>>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? new Dictionary<string, PartPackageInputStamp>(StringComparer.Ordinal);
        return new PartPackageExportManifest(
            manifestPath,
            new Dictionary<string, PartPackageInputStamp>(packages, StringComparer.Ordinal)
        );
    }

    public bool CanSkip(string packagePath, string runtimePath, PartPackageInputStamp stamp)
    {
        return !string.IsNullOrWhiteSpace(manifestPath) &&
            File.Exists(runtimePath) &&
            packages.TryGetValue(packagePath, out var existing) &&
            existing == stamp;
    }

    public void Update(string packagePath, PartPackageInputStamp stamp)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return;
        }

        packages[packagePath] = stamp;
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(packages, JsonOptions));
    }

    public static void Rebuild(
        string manifestPath,
        string outputDirectory,
        IEnumerable<PartRegistryEntry> entries,
        bool sparseInput = false
    )
    {
        var previous = Load(manifestPath);
        var registryEntries = entries.ToList();
        var rebuilt = new Dictionary<string, PartPackageInputStamp>(StringComparer.Ordinal);
        var missingSparseRuntimes = new List<string>();

        foreach (var group in registryEntries
            .Where(entry => entry.BundlePath is not null && entry.Status != "missing")
            .GroupBy(entry => entry.PackagePath, StringComparer.Ordinal))
        {
            var packagePath = group.Key;
            var runtimeJsonPath = Path.Combine(
                outputDirectory,
                packagePath.Replace('/', Path.DirectorySeparatorChar),
                "part-runtime.json"
            );
            var hasRuntime = RuntimeJsonWriter.OutputsExist(runtimeJsonPath);
            var hasSparsePlaceholder = sparseInput &&
                group.Any(PartPackageWorkPlanner.HasSparsePlaceholder);

            if (!hasRuntime)
            {
                if (hasSparsePlaceholder)
                {
                    missingSparseRuntimes.Add(packagePath);
                }
                continue;
            }

            File.Delete(Path.Combine(
                outputDirectory,
                packagePath.Replace('/', Path.DirectorySeparatorChar),
                "part-export-error.json"
            ));

            var current = group
                .Where(entry => PartPackageWorkPlanner.HasRequiredBundleFiles(entry, sparseInput))
                .OrderBy(entry => entry.Costume3dId)
                .ThenBy(entry => entry.Unit ?? string.Empty, StringComparer.Ordinal)
                .FirstOrDefault();
            if (current is not null)
            {
                rebuilt[packagePath] = PartPackageInputStamp.From(current);
            }
            else if (
                hasSparsePlaceholder &&
                previous.packages.TryGetValue(packagePath, out var previousStamp)
            )
            {
                rebuilt[packagePath] = previousStamp;
            }
            else if (hasSparsePlaceholder)
            {
                var placeholder = group
                    .Where(entry => PartPackageWorkPlanner.HasRequiredBundleFiles(
                        entry,
                        sparseInput: false
                    ))
                    .OrderBy(entry => entry.Costume3dId)
                    .ThenBy(entry => entry.Unit ?? string.Empty, StringComparer.Ordinal)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Sparse incremental input has no complete placeholder paths for {packagePath}."
                    );
                rebuilt[packagePath] = PartPackageInputStamp.From(placeholder);
            }
        }

        if (missingSparseRuntimes.Count > 0)
        {
            var examples = string.Join(", ", missingSparseRuntimes.Take(10));
            throw new InvalidOperationException(
                "Sparse incremental input cannot reuse missing part runtime package(s): " +
                $"{examples}" +
                (missingSparseRuntimes.Count > 10
                    ? $" (+{missingSparseRuntimes.Count - 10} more)"
                    : string.Empty)
            );
        }
        if (rebuilt.Count == 0 && (registryEntries.Count > 0 || previous.packages.Count > 0))
        {
            throw new InvalidOperationException(
                "Refusing to replace a part package manifest with an empty registry."
            );
        }

        var parent = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
        var temporaryPath = manifestPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(rebuilt, JsonOptions));
            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}

public sealed record PartPackageInputStamp(
    string BundlePath,
    long BundleLength,
    long BundleLastWriteUtcTicks,
    string? ColorVariationBundlePath,
    long? ColorVariationLength,
    long? ColorVariationLastWriteUtcTicks
)
{
    public static PartPackageInputStamp From(PartRegistryEntry entry)
    {
        if (entry.BundlePath is null)
        {
            throw new InvalidOperationException(
                $"Part entry {entry.PackagePath} has no bundle path."
            );
        }

        var bundle = new FileInfo(entry.BundlePath);
        FileInfo? colorVariation = entry.ColorVariationBundlePath is null
            ? null
            : new FileInfo(entry.ColorVariationBundlePath);
        return new PartPackageInputStamp(
            BundlePath: entry.BundlePath,
            BundleLength: bundle.Length,
            BundleLastWriteUtcTicks: bundle.LastWriteTimeUtc.Ticks,
            ColorVariationBundlePath: entry.ColorVariationBundlePath,
            ColorVariationLength: colorVariation?.Length,
            ColorVariationLastWriteUtcTicks: colorVariation?.LastWriteTimeUtc.Ticks
        );
    }
}
