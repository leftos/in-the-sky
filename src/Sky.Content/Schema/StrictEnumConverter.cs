using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sky.Content.Schema;

/// <summary>
/// Reads an enum only from a JSON string equal, ordinal and case-sensitive, to one of its defined member names: no numbers,
/// no other casing, no comma-separated flag lists. Anything else fails with the allowed names. Registered once per closed
/// enum type in <see cref="ContentJsonContext"/>, so the Engine's enums carry no JSON attributes.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
internal sealed class StrictEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private static readonly string[] Names = Enum.GetNames<T>();

    private static readonly T[] Values = Enum.GetValues<T>();

    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            for (int index = 0; index < Names.Length; index++)
            {
                if (reader.ValueTextEquals(Names[index]))
                {
                    return Values[index];
                }
            }
        }

        throw new JsonException($"Expected a JSON string naming one of {string.Join(", ", Names)}.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        string name = Enum.GetName(value) ?? throw new JsonException($"{value} is not a defined {typeof(T).Name}.");
        writer.WriteStringValue(name);
    }
}
