using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PjskBundle2Parts.Services;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: ParityEmit <unit-segment-vector.json> <out-dir>");
    return 1;
}

var vectorPath = args[0];
var outDirectory = Path.GetFullPath(args[1]);
var masterDirectory = Path.Combine(outDirectory, "master-fixture");
var packageDirectory = Path.Combine(outDirectory, "package");
var parityPath = Path.Combine(outDirectory, "exporter-parity.json");

// 1. Unit-segment parity: drive the exporter's production <unit> path-segment
// formula with the shared input vector.
using var vectorDocument = JsonDocument.Parse(File.ReadAllBytes(vectorPath));
var unitSegment = new JsonArray();
foreach (var input in vectorDocument.RootElement.GetProperty("inputs").EnumerateArray())
{
    var value = input.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => input.GetString(),
        _ => throw new InvalidDataException("unit-segment vector inputs must be strings or null."),
    };
    unitSegment.Add(new JsonObject
    {
        ["input"] = value is null ? null : JsonValue.Create(value),
        ["output"] = RuntimeJsonWriter.RuntimePathUnitSegment(value),
    });
}

// 2. Role-table parity: read the exporter's hardcoded 31-role identity table
// through its private seam, the same way exporter/Tests/ConfigParserSmoke.cs
// reaches private production seams (reflection instead of widening visibility).
var expectedRole = typeof(RuntimeRoleCatalogExporter).GetMethod(
    "ExpectedRole",
    BindingFlags.NonPublic | BindingFlags.Static
) ?? throw new MissingMethodException(nameof(RuntimeRoleCatalogExporter), "ExpectedRole");

var identities = new List<(int RoleId, int CharacterId, string Unit)>();
var roleIdentity = new JsonArray();
for (var roleId = 1; roleId <= 31; roleId++)
{
    var result = expectedRole.Invoke(null, [roleId])
        ?? throw new InvalidDataException($"ExpectedRole({roleId}) returned null.");
    var resultType = result.GetType();
    var characterId = (int)(resultType.GetField("Item1")?.GetValue(result)
        ?? throw new InvalidDataException("ExpectedRole result is missing Item1."));
    var unit = (string)(resultType.GetField("Item2")?.GetValue(result)
        ?? throw new InvalidDataException("ExpectedRole result is missing Item2."));
    identities.Add((roleId, characterId, unit));
    roleIdentity.Add(new JsonObject
    {
        ["roleId"] = roleId,
        ["characterId"] = characterId,
        ["unit"] = unit,
    });
}

// 3. Path-formula parity: synthesize the minimal master data set from the
// exporter's own role table, then run the PRODUCTION catalog exporter over it.
// WriteFromMaster validates the fixture against the exporter's hardcoded table,
// computes every roleRuntimePath with the production formula, and lays the
// scoped catalogs out on disk with the production by-role path template.
ResetDirectory(masterDirectory);
ResetDirectory(packageDirectory);
WriteMasterFixture(masterDirectory, identities);
var catalog = RuntimeRoleCatalogExporter.WriteFromMaster(masterDirectory, packageDirectory);

var rootCatalogPath = "runtime-role-catalog.msgpack.br";
if (!File.Exists(Path.Combine(packageDirectory, rootCatalogPath)))
{
    throw new InvalidDataException($"Production exporter did not write {rootCatalogPath}.");
}
var scopedCatalogPaths = Directory
    .EnumerateFiles(packageDirectory, "runtime-role-catalog.msgpack.br", SearchOption.AllDirectories)
    .Select(path => Path.GetRelativePath(packageDirectory, path).Replace(Path.DirectorySeparatorChar, '/'))
    .Where(path => path != rootCatalogPath)
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToList();
if (scopedCatalogPaths.Count != identities.Count)
{
    throw new InvalidDataException(
        $"Production exporter wrote {scopedCatalogPaths.Count} scoped role catalogs, expected {identities.Count}."
    );
}

var parity = new JsonObject
{
    ["unitSegment"] = unitSegment,
    ["roleIdentity"] = roleIdentity,
    ["catalog"] = new JsonObject
    {
        ["version"] = catalog.Version,
        ["masterVersion"] = catalog.MasterVersion,
    },
    ["rootCatalogPath"] = rootCatalogPath,
    ["scopedCatalogPaths"] = new JsonArray([.. scopedCatalogPaths.Select(path => (JsonNode?)JsonValue.Create(path))]),
};
File.WriteAllText(parityPath, parity.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"wrote {parityPath} and {scopedCatalogPaths.Count} scoped role catalogs under {packageDirectory}");
return 0;

static void ResetDirectory(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }
    Directory.CreateDirectory(path);
}

// Master fixture values are arbitrary but valid; the role identities come from
// the exporter's own table so the fixture can never drift from it silently.
static void WriteMasterFixture(string masterDirectory, IReadOnlyList<(int RoleId, int CharacterId, string Unit)> identities)
{
    var character3ds = new JsonArray([.. identities.Select(identity => (JsonNode?)new JsonObject
    {
        ["id"] = identity.RoleId,
        ["characterId"] = identity.CharacterId,
        ["unit"] = identity.Unit,
        ["name"] = $"parity-role-{identity.RoleId}",
        ["headCostume3dId"] = 2000 + identity.RoleId,
        ["hairCostume3dId"] = 3000 + identity.RoleId,
        ["bodyCostume3dId"] = 1000 + identity.RoleId,
    })]);
    var characterUnits = new JsonArray([.. identities.Select((identity, index) => (JsonNode?)new JsonObject
    {
        ["id"] = index + 1,
        ["gameCharacterId"] = identity.CharacterId,
        ["unit"] = identity.Unit,
        ["skinColorCode"] = "#ffd1c2",
        ["skinShadowColorCode1"] = "#e0b0a0",
        ["skinShadowColorCode2"] = "#c09080",
    })]);
    var characters = new JsonArray([.. identities
        .Select(identity => identity.CharacterId)
        .Distinct()
        .OrderBy(characterId => characterId)
        .Select(characterId => (JsonNode?)new JsonObject
        {
            ["id"] = characterId,
            ["resourceId"] = characterId,
            ["gender"] = "female",
            ["height"] = 140 + characterId,
            ["figure"] = "normal",
            ["breastSize"] = "m",
            ["modelName"] = $"parity-model-{characterId}",
            ["unit"] = null,
        })]);

    WriteJsonFile(Path.Combine(masterDirectory, "character3ds.json"), character3ds);
    WriteJsonFile(Path.Combine(masterDirectory, "gameCharacterUnits.json"), characterUnits);
    WriteJsonFile(Path.Combine(masterDirectory, "gameCharacters.json"), characters);
    WriteJsonFile(Path.Combine(masterDirectory, "current_version.json"), new JsonObject
    {
        ["dataVersion"] = "0.0.0-parity-fixture",
    });
}

static void WriteJsonFile(string path, JsonNode value)
{
    File.WriteAllText(path, value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}
