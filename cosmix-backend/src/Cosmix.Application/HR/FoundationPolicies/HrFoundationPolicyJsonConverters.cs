using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.FoundationPolicies;

/// <summary>Keep the strict pre-binding contract and ordinary MVC binding identical without changing global API JSON.</summary>
public sealed class HrFoundationEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return text is not null && Enum.TryParse<T>(text, false, out var value) && Enum.IsDefined(value) &&
            Enum.GetName(value) == text ? value : throw new JsonException("An exact supported policy enum name is required.");
    }
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value)) throw new JsonException("Unsupported policy enum value.");
        writer.WriteStringValue(Enum.GetName(value));
    }
}

public sealed class HrFoundationEnumArrayConverter<T> : JsonConverter<T[]> where T : struct, Enum
{
    private static readonly HrFoundationEnumConverter<T> Element = new();
    public override T[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Policy enum values require an array.");
        var values = new List<T>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) return values.ToArray();
            if (values.Count >= 100) throw new JsonException("Policy enum array exceeds its bound.");
            values.Add(Element.Read(ref reader, typeof(T), options));
        }
        throw new JsonException("Incomplete policy enum array.");
    }
    public override void Write(Utf8JsonWriter writer, T[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value) Element.Write(writer, item, options);
        writer.WriteEndArray();
    }
}
