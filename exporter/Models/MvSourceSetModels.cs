using System.Text.Json.Serialization;

namespace PjskBundle2Parts.Models;

public sealed record MvSourceManifest(
    [property: JsonPropertyName("music_id")] int MusicId,
    [property: JsonPropertyName("music_title")] string? MusicTitle,
    [property: JsonPropertyName("asset_version")] string? AssetVersion,
    [property: JsonPropertyName("asset_hash")] string? AssetHash,
    [property: JsonPropertyName("bundles")] IReadOnlyList<MvSourceManifestBundle> Bundles
);

public sealed record MvSourceManifestBundle(
    [property: JsonPropertyName("bundle")] string Bundle,
    [property: JsonPropertyName("dependencies")] IReadOnlyList<string>? Dependencies
);

public sealed record MvSourceSetEntry(
    string Name,
    string Kind,
    string File,
    IReadOnlyList<string> Dependencies,
    long Size,
    string Sha256
);

public sealed record MvSourceSet(
    int MusicId,
    string? MusicTitle,
    string? AssetVersion,
    string? AssetHash,
    string Platform,
    int BundleCount,
    long TotalBytes,
    IReadOnlyList<MvSourceSetEntry> Bundles
);

public sealed record MvDependencySet(
    IReadOnlyList<string> Requested,
    IReadOnlyList<MvDependencyEntry> Entries
);

public sealed record MvDependencyEntry(string Name, IReadOnlyList<string> Deps);

public sealed record MvSourceSetExportResult(int MusicId, int BundleCount, long TotalBytes);
