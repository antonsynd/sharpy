#if NET10_0_OR_GREATER
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sharpy
{
    /// <summary>
    /// Reads the sentinel object <see cref="Json.RewriteBareNonFiniteTokens"/> writes for CPython's
    /// bare <c>NaN</c> / <c>Infinity</c> / <c>-Infinity</c>, at any floating-point target (#1488).
    ///
    /// <para>
    /// The pre-edit exists because a bare token fails <c>Utf8JsonReader</c> tokenization before any
    /// converter can run, so the token never reaches a converter as itself. What this type adds is
    /// the other half: the pre-edit knows a token was BARE, and rewriting it to a shape only a
    /// numeric target accepts is how that knowledge survives to the point of use. Rewriting to the
    /// quoted spelling — what this replaced — threw the knowledge away and made
    /// <c>loads[str]("NaN")</c> indistinguishable from <c>loads[str]('"NaN"')</c>.
    /// </para>
    ///
    /// <para>
    /// Registered for <see cref="double"/> and <see cref="float"/>, so it REPLACES System.Text.Json's
    /// built-in numeric readers for those types and must therefore keep doing everything they did:
    /// ordinary numbers, and the quoted spellings that
    /// <c>JsonNumberHandling.AllowNamedFloatingPointLiterals</c> accepts — the option `dumps` output
    /// relies on (#1353). All three arms are covered below and asserted in
    /// <c>JsonBareNonFiniteTypedDoorTests</c>.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The floating-point target, <see cref="double"/> or <see cref="float"/>.</typeparam>
    internal sealed class BareNonFiniteJsonConverter<T> : JsonConverter<T>
        where T : struct
    {
        /// <inheritdoc />
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return FromSpelling(ReadSentinelSpelling(ref reader));
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                // The quoted spellings AllowNamedFloatingPointLiterals accepts. Anything else at a
                // numeric target is a decode error, exactly as the built-in reader would say.
                string? text = reader.GetString();
                return FromSpelling(text);
            }

            double value = reader.GetDouble();
            return Narrow(value);
        }

        /// <summary>
        /// Consumes the sentinel object and returns the spelling it carries.
        /// </summary>
        /// <remarks>
        /// A document object that merely LOOKS like the sentinel — right key, wrong shape — must
        /// still be a decode error rather than a silent zero, so every departure throws.
        /// </remarks>
        private static string? ReadSentinelSpelling(ref Utf8JsonReader reader)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);

            if (!document.RootElement.TryGetProperty(Json.BareNonFiniteSentinelKey, out JsonElement marker)
                || marker.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(
                    $"expected a number, got a JSON object (no {Json.BareNonFiniteSentinelKey} marker)");
            }

            return marker.GetString();
        }

        private static T FromSpelling(string? spelling)
        {
            switch (spelling)
            {
                case "NaN":
                    return Narrow(double.NaN);
                case "Infinity":
                    return Narrow(double.PositiveInfinity);
                case "-Infinity":
                    return Narrow(double.NegativeInfinity);
                default:
                    throw new JsonException($"expected a number, got the string \"{spelling}\"");
            }
        }

        private static T Narrow(double value)
        {
            if (typeof(T) == typeof(float))
            {
                float single = (float)value;
                return (T)(object)single;
            }

            return (T)(object)value;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Writing is untouched by #1488: `dumps` has its own CPython-matching emitter (#1296), and
        /// this converter is only ever reached on the typed READ path. Delegating to the ordinary
        /// number writer keeps any incidental serialize through these options byte-identical to
        /// what it was before the converter existed.
        /// </remarks>
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value is float single)
            {
                writer.WriteNumberValue(single);
                return;
            }

            writer.WriteNumberValue((double)(object)value);
        }
    }
}
#endif
