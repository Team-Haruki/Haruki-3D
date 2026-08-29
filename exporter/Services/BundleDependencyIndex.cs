using System.Text.Json;

namespace PjskBundle2Parts.Services;

public sealed class BundleDependencyIndex
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> dependencies;

    public BundleDependencyIndex(string? path)
    {
        dependencies = Load(path);
    }

    public IReadOnlyList<string> GetClosure(string? logicalBundleName)
    {
        return logicalBundleName is not null &&
            dependencies.TryGetValue(logicalBundleName, out var closure)
                ? closure
                : Array.Empty<string>();
    }

    public IReadOnlyList<string> ResolveExistingBundlePaths(
        string assetRoot,
        string? logicalBundleName
    )
    {
        var root = Path.GetFullPath(assetRoot);
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return GetClosure(logicalBundleName)
            .Select(value => Path.GetFullPath(Path.Combine(
                root,
                value.Replace('/', Path.DirectorySeparatorChar) + ".bundle"
            )))
            .Where(path =>
                path.StartsWith(rootPrefix, StringComparison.Ordinal) &&
                File.Exists(path)
            )
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, IReadOnlyList<string>> Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path))
                ?? new Dictionary<string, string[]>();
            return values.ToDictionary(
                pair => Normalize(pair.Key),
                pair => (IReadOnlyList<string>)pair.Value
                    .Select(Normalize)
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal
            );
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            Console.Error.WriteLine($"Bundle dependency index ignored ({path}): {ex.Message}");
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }
    }

    public static string? LogicalName(string assetRoot, string bundlePath)
    {
        var relative = Path.GetRelativePath(assetRoot, bundlePath).Replace('\\', '/');
        return relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? null
            : Normalize(relative);
    }

    private static string Normalize(string value)
    {
        var normalized = value.Replace('\\', '/').TrimStart('/');
        return normalized.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^".bundle".Length]
            : normalized;
    }
}
