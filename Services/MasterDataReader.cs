using System.Text.Json;
using PjskBundle2Parts.Models;

namespace PjskBundle2Parts.Services;

internal static class MasterDataReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<Costume3dModelMaster> ReadCostume3dModels(
        string masterDirectory
    )
    {
        var rawPath = Path.Combine(masterDirectory, "costume3dModels.json");
        if (File.Exists(rawPath))
        {
            return Read<IReadOnlyList<Costume3dModelMaster>>(rawPath);
        }

        var compactPath = Path.Combine(masterDirectory, "compactCostume3dModels.json");
        if (!File.Exists(compactPath))
        {
            throw new FileNotFoundException(
                $"Master file was not found: {rawPath} or {compactPath}"
            );
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(compactPath));
        var root = document.RootElement;
        var ids = RequiredColumn(root, "costume3dId");
        var models = new List<Costume3dModelMaster>(ids.GetArrayLength());

        for (var index = 0; index < ids.GetArrayLength(); index++)
        {
            models.Add(new Costume3dModelMaster(
                Costume3dId: ids[index].GetInt32(),
                Unit: ReadCompactString(root, "unit", index),
                AssetbundleName: ReadCompactString(root, "assetbundleName", index),
                HeadCostume3dAssetbundleType: ReadCompactString(
                    root,
                    "headCostume3dAssetbundleType",
                    index
                ),
                ColorAssetbundleName: ReadCompactString(root, "colorAssetbundleName", index),
                Part: ReadCompactString(root, "part", index),
                ThumbnailAssetbundleName: ReadCompactString(
                    root,
                    "thumbnailAssetbundleName",
                    index
                )
            ));
        }

        return models;
    }

    private static T Read<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse master file: {path}");
    }

    private static JsonElement RequiredColumn(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var column) || column.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Compact master column was not found: {name}");
        }

        return column;
    }

    private static string? ReadCompactString(JsonElement root, string name, int index)
    {
        var column = RequiredColumn(root, name);
        var value = column[index];
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number ||
            !root.TryGetProperty("__ENUM__", out var enums) ||
            !enums.TryGetProperty(name, out var enumValues))
        {
            return value.GetString();
        }

        var enumIndex = value.GetInt32();
        return enumIndex >= 0 && enumIndex < enumValues.GetArrayLength()
            ? enumValues[enumIndex].GetString()
            : throw new InvalidOperationException(
                $"Compact master enum index is out of range: {name}[{index}]={enumIndex}"
            );
    }
}
