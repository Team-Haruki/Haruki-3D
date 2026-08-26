using System.Text.Json;
using PjskBundle2Parts.Models;

namespace PjskBundle2Parts.Services;

internal static class MasterDataReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<Costume3dMaster> ReadCostume3ds(
        string masterDirectory
    )
    {
        var costumes = Read<IReadOnlyList<Costume3dMaster>>(
            Path.Combine(masterDirectory, "costume3ds.json")
        );
        if (costumes.All(costume => costume.CharacterId > 0))
        {
            return costumes;
        }

        var groupPath = Path.Combine(masterDirectory, "costume3dGroups.json");
        if (!File.Exists(groupPath))
        {
            throw new InvalidDataException(
                $"costume3ds.json contains rows without characterId, but {groupPath} was not found"
            );
        }

        var groupsById = Read<IReadOnlyList<Costume3dGroupMaster>>(groupPath)
            .ToDictionary(group => group.GroupId);
        var colorPath = Path.Combine(masterDirectory, "costume3dColors.json");
        var colorsById = File.Exists(colorPath)
            ? Read<IReadOnlyList<Costume3dColorMaster>>(colorPath)
                .ToDictionary(color => color.Id)
            : new Dictionary<int, Costume3dColorMaster>();

        return costumes.Select(costume =>
        {
            if (costume.CharacterId > 0)
            {
                return costume;
            }
            if (!groupsById.TryGetValue(costume.Costume3dGroupId, out var group) ||
                group.CharacterId <= 0)
            {
                throw new InvalidDataException(
                    $"costume3ds row {costume.Id} has no characterId and group " +
                    $"{costume.Costume3dGroupId} cannot supply one"
                );
            }

            colorsById.TryGetValue(costume.ColorId, out var color);
            return costume with
            {
                CharacterId = group.CharacterId,
                Name = !string.IsNullOrWhiteSpace(costume.Name)
                    ? costume.Name
                    : group.Name ?? throw new InvalidDataException(
                        $"costume3ds row {costume.Id} has no name and group " +
                        $"{costume.Costume3dGroupId} cannot supply one"
                    ),
                ColorName = costume.ColorName ?? color?.Name,
                Costume3dType = costume.Costume3dType ?? "normal",
                Costume3dRarity = costume.Costume3dRarity ?? group.Rarity,
                HowToObtain = costume.HowToObtain ?? group.HowToObtain,
            };
        }).ToList();
    }

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
