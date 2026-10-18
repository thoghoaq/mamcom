using System.Text.Json.Serialization;

namespace MamCom.Core.Json;

/// <summary>
/// String enum converter that honours <see cref="JsonStringEnumMemberNameAttribute"/> and, unlike the default
/// converter, rejects numbers, so an undefined value cannot slip into a model unnoticed.
/// </summary>
public sealed class StrictStringEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public StrictStringEnumConverter()
        : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
