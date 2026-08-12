using AssetStudio;
using PjskBundle2Parts.Models;
using Object = AssetStudio.Object;

namespace PjskBundle2Parts.Services;

public sealed record OfficialUnityResource(
    string ResourceName,
    string ContainerPath,
    GameObject RootGameObject,
    IReadOnlyList<Object> Objects
);

public sealed class OfficialUnityResourceExtractor
{
    public OfficialUnityResource Extract(
        ResolvedBundleInput input,
        IReadOnlyList<Object> primaryObjects,
        string resourceName
    )
    {
        var expectedContainerSuffix = BuildExpectedContainerSuffix(input, resourceName);
        var candidates = primaryObjects
            .OfType<AssetBundle>()
            .SelectMany(bundle => bundle.m_Container.Select(entry => new
            {
                Bundle = bundle,
                Entry = entry,
                ContainerPath = NormalizePath(entry.Key),
            }))
            .Where(candidate => candidate.Entry.Value.asset.TryGet(out var value) &&
                value is GameObject gameObject &&
                string.Equals(gameObject.m_Name, resourceName, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => new
            {
                candidate.Bundle,
                candidate.Entry,
                candidate.ContainerPath,
                Root = (GameObject)ResolveAsset(candidate.Entry.Value),
                IsExact = candidate.ContainerPath.EndsWith(expectedContainerSuffix, StringComparison.OrdinalIgnoreCase),
                IsGeneratedInput = candidate.ContainerPath.Contains("/fbx/", StringComparison.OrdinalIgnoreCase) ||
                    candidate.ContainerPath.Contains("/sourceimages/", StringComparison.OrdinalIgnoreCase),
            })
            .OrderByDescending(candidate => candidate.IsExact)
            .ThenBy(candidate => candidate.IsGeneratedInput)
            .ThenBy(candidate => candidate.ContainerPath.Count(character => character == '/'))
            .ThenBy(candidate => candidate.ContainerPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"Official Unity resource '{resourceName}' was not found in the AssetBundle container for {input.ResolvedBundlePath}."
            );
        }

        var exactCandidates = candidates
            .Where(candidate => candidate.IsExact && !candidate.IsGeneratedInput)
            .ToList();
        if (exactCandidates.Count != 1)
        {
            throw new InvalidOperationException(
                $"Official Unity resource '{resourceName}' must resolve to exactly one container ending in " +
                $"'{expectedContainerSuffix}', found {exactCandidates.Count}. Candidates: " +
                $"{string.Join(", ", candidates.Take(4).Select(candidate => candidate.ContainerPath))}."
            );
        }
        var selected = exactCandidates[0];

        var objects = ResolvePreloadObjects(selected.Bundle, selected.Entry.Value)
            .Append(selected.Root)
            .Distinct()
            .ToList();
        return new OfficialUnityResource(
            resourceName,
            selected.ContainerPath,
            selected.Root,
            objects
        );
    }

    private static Object ResolveAsset(AssetInfo info)
    {
        if (info.asset.TryGet(out var value))
        {
            return value;
        }
        throw new InvalidOperationException("AssetBundle container resource could not be resolved.");
    }

    private static IEnumerable<Object> ResolvePreloadObjects(AssetBundle bundle, AssetInfo info)
    {
        var start = Math.Max(info.preloadIndex, 0);
        var end = Math.Min(start + Math.Max(info.preloadSize, 0), bundle.m_PreloadTable.Count);
        for (var index = start; index < end; index += 1)
        {
            if (bundle.m_PreloadTable[index].TryGet(out var value))
            {
                yield return value;
            }
        }
    }

    private static string BuildExpectedContainerSuffix(ResolvedBundleInput input, string resourceName)
    {
        var normalized = NormalizePath(input.ResolvedBundlePath);
        const string assetBundlesMarker = "/assetbundles/";
        var markerIndex = normalized.IndexOf(assetBundlesMarker, StringComparison.OrdinalIgnoreCase);
        var relative = markerIndex >= 0
            ? normalized[(markerIndex + assetBundlesMarker.Length)..]
            : Path.GetFileName(normalized);
        relative = relative.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)
            ? relative[..^".bundle".Length]
            : relative;
        return $"/{relative.Trim('/')}/{resourceName}.prefab";
    }

    private static string NormalizePath(string value)
    {
        return value.Replace('\\', '/');
    }
}
