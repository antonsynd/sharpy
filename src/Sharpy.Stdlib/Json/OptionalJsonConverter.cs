#if NET10_0_OR_GREATER
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sharpy
{
    /// <summary>
    /// <see cref="System.Text.Json"/> converter factory for <see cref="Optional{T}"/> so that
    /// typed json deserialization (<c>json.loads[T]</c>/<c>json.load[T]</c>) populates Sharpy
    /// optional fields correctly: a present JSON value becomes <c>Some(value)</c> while JSON
    /// null (or a missing field, via the struct default) becomes <c>None</c>. Without this,
    /// an <c>Optional&lt;T&gt;</c> field never receives a present value (#843 dogfooding).
    /// </summary>
    internal sealed class OptionalJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType &&
            typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type valueType = typeToConvert.GetGenericArguments()[0];
            Type converterType = typeof(OptionalJsonConverter<>).MakeGenericType(valueType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    /// <summary>Converts an <see cref="Optional{T}"/> to/from JSON.</summary>
    /// <typeparam name="T">The contained value type.</typeparam>
    internal sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    {
        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return Optional<T>.None;
            }

            // Fully qualified: in the Sharpy namespace the bare name 'JsonSerializer'
            // resolves to Sharpy.JsonSerializer, not System.Text.Json's.
            T value = System.Text.Json.JsonSerializer.Deserialize<T>(ref reader, options)!;
            return Optional<T>.Some(value);
        }

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
        {
            if (value.IsNone)
            {
                writer.WriteNullValue();
            }
            else
            {
                System.Text.Json.JsonSerializer.Serialize(writer, value.Unwrap(), options);
            }
        }
    }
}
#endif
