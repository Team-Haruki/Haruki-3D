using System.Text.RegularExpressions;
using PjskBundle2Parts.Models;

namespace PjskBundle2Parts.Services;

public sealed partial class BundleInputResolver
{
    private const int RegexTimeoutMilliseconds = 1_000;

    [GeneratedRegex(
        @"(?<=/)(\d{2})(?=/)",
        RegexOptions.None,
        RegexTimeoutMilliseconds
    )]
    private static partial Regex CharacterIdRegex();

    public static ResolvedBundleInput ResolveBody(string inputPath)
    {
        var normalized = Normalize(inputPath);
        if (File.Exists(normalized))
        {
            return BuildResolved(BundlePartKind.Body, inputPath, normalized);
        }

        if (!Directory.Exists(normalized))
        {
            throw new FileNotFoundException($"Body input not found: {inputPath}");
        }

        var candidates = EnumerateBundleFiles(normalized)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                $"No .bundle files found in body directory: {inputPath}"
            );
        }
        if (candidates.Length != 1)
        {
            throw new InvalidOperationException(
                $"Body directory is ambiguous ({candidates.Length} bundles): {inputPath}. " +
                "Pass the exact body bundle selected from gameCharacters.figure and breastSize."
            );
        }

        return BuildResolved(BundlePartKind.Body, inputPath, candidates[0]);
    }

    public static ResolvedBundleInput ResolveHead(string inputPath)
    {
        var normalized = Normalize(inputPath);
        if (File.Exists(normalized))
        {
            return BuildResolved(BundlePartKind.Head, inputPath, normalized);
        }

        if (!Directory.Exists(normalized))
        {
            throw new FileNotFoundException($"Head input not found: {inputPath}");
        }

        var candidates = EnumerateBundleFiles(normalized)
            .OrderBy(path => ScoreHeadCandidate(Path.GetFileName(path)))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                $"No .bundle files found in head directory: {inputPath}"
            );
        }

        return BuildResolved(BundlePartKind.Head, inputPath, candidates[0]);
    }

    private static ResolvedBundleInput BuildResolved(
        BundlePartKind partKind,
        string originalInputPath,
        string resolvedBundlePath
    )
    {
        var normalizedResolved = Normalize(resolvedBundlePath);
        var characterId = InferCharacterId(normalizedResolved);
        var bundleStem = GetBundleStem(normalizedResolved);
        return new ResolvedBundleInput(
            partKind,
            originalInputPath,
            normalizedResolved,
            characterId,
            bundleStem
        );
    }

    private static string[] EnumerateBundleFiles(string directory)
    {
        return Directory.GetFiles(directory, "*.bundle", SearchOption.TopDirectoryOnly);
    }

    private static string GetBundleStem(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    private static string InferCharacterId(string path)
    {
        var unixPath = path.Replace('\\', '/');
        var match = CharacterIdRegex().Match(unixPath);
        return match.Success ? match.Groups[1].Value : "unknown";
    }

    private static int ScoreHeadCandidate(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "0001.bundle" => 0,
            _ => 100,
        };
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
    }
}
