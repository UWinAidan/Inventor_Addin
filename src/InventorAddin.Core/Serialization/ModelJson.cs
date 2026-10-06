using System.Text.Json;
using System.Text.Json.Serialization;
using InventorAddin.Core.Models;

namespace InventorAddin.Core.Serialization;

/// <summary>JSON round-tripping for extracted data (debug dumps, test fixtures, offline UI work).</summary>
public static class ModelJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>ModelData is written with a "$type" discriminator so it can be read back as the right subclass.</summary>
    public static string Serialize(object data) => data is ModelData model
        ? JsonSerializer.Serialize(model, Options)
        : JsonSerializer.Serialize(data, data.GetType(), Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    public static ModelData? DeserializeModel(string json) => Deserialize<ModelData>(json);
}
