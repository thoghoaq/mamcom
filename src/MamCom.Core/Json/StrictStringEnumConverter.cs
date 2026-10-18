using System.Text.Json;
using System.Text.Json.Serialization;

namespace MamCom.Core.Json;

/// <summary>
/// String enum converter that honours <see cref="JsonStringEnumMemberNameAttribute"/> and accepts exactly one
/// member name. The built-in converter also accepts numbers, comma-separated lists ("man,rau" becomes an undefined
/// value) and names padded with spaces; none of those can reach a model through this converter.
/// </summary>
public sealed class StrictStringEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly JsonStringEnumConverter<TEnum> _factory = new(namingPolicy: null, allowIntegerValues: false);
    private JsonConverter<TEnum>? _inner;

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.IsNullOrEmpty(text) || text.Contains(',') || char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]))
            {
                throw new JsonException($"'{text}' is not a valid {typeof(TEnum).Name}.");
            }
        }

        return Inner(options).Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        Inner(options).Write(writer, value, options);

    private JsonConverter<TEnum> Inner(JsonSerializerOptions options) =>
        _inner ??= (JsonConverter<TEnum>)_factory.CreateConverter(typeof(TEnum), options)!;
}
