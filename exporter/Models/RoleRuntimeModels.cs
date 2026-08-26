using System.Text.Json.Serialization;

namespace PjskBundle2Parts.Models;

public sealed record RoleRuntimePackage(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("role")] RoleRuntimeIdentity Role,
    [property: JsonPropertyName("sourceCharacter3dId")] int SourceCharacter3dId,
    [property: JsonPropertyName("sourceCharacterName")] string SourceCharacterName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [property: JsonPropertyName("sourceCostumeSettingBundlePath")] string? SourceCostumeSettingBundlePath,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [property: JsonPropertyName("motionPackage")] PjskSekaiRuntimeMotionPackage? MotionPackage,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings
);

public sealed record RoleRuntimeIdentity(
    [property: JsonPropertyName("characterId")] int CharacterId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [property: JsonPropertyName("unit")] string? Unit
);

public sealed record RoleRuntimeExportResult(
    int Character3dId,
    int CharacterId,
    string? Unit,
    string RuntimePath,
    IReadOnlyList<string> Warnings
);
