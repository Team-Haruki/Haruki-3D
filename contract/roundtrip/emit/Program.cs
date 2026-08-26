using System.Text.Json;
using System.Text.Json.Serialization;
using PjskBundle2Parts.Services;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: RoundtripEmit <fixture-manifest.json> <output.msgpack.br>");
    return 1;
}

using var manifest = JsonDocument.Parse(File.ReadAllBytes(args[0]));
var schemaText = manifest.RootElement.GetProperty("schema").GetString()
    ?? throw new InvalidOperationException("fixture manifest is missing \"schema\".");
var schema = Enum.Parse<RuntimeBinaryArraySchema>(schemaText);
var document = ToClrValue(manifest.RootElement.GetProperty("document"))
    ?? throw new InvalidOperationException("fixture manifest \"document\" must not be null.");

// Mirrors the WriteJsonOptions used by the production exporters
// (e.g. PartPackageExporter.WriteJsonOptions).
var options = new JsonSerializerOptions
{
    WriteIndented = false,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

// Production entry point: MessagePack with ext-42 binary arrays, Brotli quality 6.
RuntimeJsonWriter.Write(args[1], document, options, binaryArraySchema: schema);
Console.WriteLine($"wrote {RuntimeJsonWriter.PrimaryPath(args[1])}");
return 0;

// Converts the manifest JSON into a plain CLR object graph so the fixture flows
// through the same RuntimeJsonWriter object-graph branches the production
// exporters use (maps via TryWriteDictionary, arrays via the IEnumerable
// TryWriteBinaryArray overload).
static object? ToClrValue(JsonElement element)
{
    return element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(property => property.Name, property => ToClrValue(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToClrValue).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Number => ToClrNumber(element),
        _ => throw new NotSupportedException($"Unsupported JSON value kind: {element.ValueKind}"),
    };
}

static object ToClrNumber(JsonElement element)
{
    var raw = element.GetRawText();
    var isIntegerText = raw.IndexOfAny(['.', 'e', 'E']) < 0;
    // "-0" must stay a double so the float32 sign survives the round trip.
    if (isIntegerText && raw != "-0")
    {
        if (element.TryGetInt64(out var signed))
        {
            return signed;
        }
        if (element.TryGetUInt64(out var unsigned))
        {
            return unsigned;
        }
    }
    return element.GetDouble();
}
