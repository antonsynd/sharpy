using System;
#if NET10_0_OR_GREATER
using System.Text.Json;
#endif

namespace Sharpy
{
    /// <summary>
    /// Python-compatible json module.
    /// Provides dumps/loads for string serialization and dump/load for file I/O.
    /// </summary>
    public static partial class Json
    {
        /// <summary>
        /// Serialize obj to a JSON formatted string.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A JSON string representation of <paramref name="obj"/>.</returns>
        /// <example>
        /// <code>
        /// json.dumps({"key": "value"})    # '{"key": "value"}'
        /// json.dumps([1, 2, 3])           # '[1, 2, 3]'
        /// </code>
        /// </example>
        public static string Dumps(object? obj)
        {
            return JsonSerializer.Serialize(obj);
        }

        /// <summary>
        /// Serialize obj to a JSON formatted string with formatting options.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="indent">Number of spaces for indentation. Use -1 for compact output.</param>
        /// <param name="sortKeys">Whether to sort dictionary keys.</param>
        /// <param name="ensureAscii">Whether to escape non-ASCII characters.</param>
        /// <param name="separators">A tuple of <c>(itemSeparator, keySeparator)</c> overriding the
        /// defaults. When <c>null</c>, defaults to <c>(", ", ": ")</c> in compact mode and
        /// <c>(",", ": ")</c>-style behavior in pretty mode (newlines drive item separation).</param>
        /// <param name="default">Optional callback invoked for any value that is not natively
        /// JSON-serializable. The callback should return a JSON-serializable replacement value, or
        /// raise a <c>TypeError</c> for unsupported types. Returning the original object will
        /// raise a <c>TypeError</c> to avoid infinite recursion. Named <c>default</c> for Python
        /// compatibility (kwarg <c>default=</c>).</param>
        /// <param name="cls">Optional <see cref="JSONEncoder"/> instance. When provided,
        /// delegates serialization to <c>cls.Encode(obj)</c>.</param>
        /// <param name="allowNan">When <c>true</c> (CPython's default), <c>Infinity</c>,
        /// <c>-Infinity</c> and <c>NaN</c> are emitted as those literal tokens — CPython's own
        /// extension to JSON, which its parser also accepts. When <c>false</c>, a non-finite
        /// float raises <c>ValueError</c> (#1296).</param>
        /// <returns>A JSON string representation of <paramref name="obj"/>.</returns>
        public static string Dumps(
            object? obj,
            int indent = -1,
            bool sortKeys = false,
            bool ensureAscii = true,
            (string, string)? separators = null,
            Func<object, object?>? @default = null,
            JSONEncoder? cls = null,
            bool allowNan = true)
        {
            if (cls != null)
            {
                return cls.Encode(obj);
            }

            string? itemSep = null;
            string? keySep = null;

            if (separators.HasValue)
            {
                itemSep = separators.Value.Item1;
                keySep = separators.Value.Item2;
            }

            return JsonSerializer.Serialize(obj, indent, sortKeys, ensureAscii, itemSep, keySep, @default, allowNan);
        }

        /// <summary>
        /// Deserialize a JSON string to a Python-like object.
        /// Returns Dict&lt;string, object?&gt; for objects, List&lt;object?&gt; for arrays,
        /// string, int/long/double, bool, or null.
        /// </summary>
        /// <param name="s">The JSON string to deserialize.</param>
        /// <param name="cls">Optional <see cref="JSONDecoder"/> instance for custom decoding.</param>
        /// <param name="objectHook">Optional callback invoked for every decoded JSON object (dict).</param>
        /// <returns>The deserialized object.</returns>
        /// <example>
        /// <code>
        /// json.loads('{"a": 1}')    # {"a": 1}
        /// json.loads('[1, 2]')      # [1, 2]
        /// </code>
        /// </example>
        public static object? Loads(
            string s,
            JSONDecoder? cls = null,
            Func<Dict<string, object?>, object?>? objectHook = null)
        {
            if (cls != null)
            {
                return cls.Decode(s);
            }

            // Both doors refuse unpaired surrogate escapes with the same message (#1487).
            // The rule is JsonSurrogateEscapes — one scanner, two callers.
            if (JsonSurrogateEscapes.FirstUnpaired(s) is { } unpaired)
            {
                throw new JSONDecodeError(
                    SurrogateRefusalMessage(unpaired.escapeText), s, unpaired.position);
            }

            return JsonParser.Parse(s, objectHook);
        }

        /// <summary>
        /// Serialize obj as a JSON formatted stream to a file.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="fp">The file to write to.</param>
        /// <example>
        /// <code>
        /// f = open("data.json", "w")
        /// json.dump({"key": "value"}, f)
        /// f.close()
        /// </code>
        /// </example>
        public static void Dump(object? obj, TextFile fp)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string json = Dumps(obj);
            fp.Write(json);
        }

        /// <summary>
        /// Serialize obj as a JSON formatted stream to a file with formatting options.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="fp">The file to write to.</param>
        /// <param name="indent">Number of spaces for indentation. Use -1 for compact output.</param>
        /// <param name="sortKeys">Whether to sort dictionary keys.</param>
        /// <param name="ensureAscii">Whether to escape non-ASCII characters.</param>
        /// <param name="separators">A tuple of <c>(itemSeparator, keySeparator)</c> overriding the
        /// defaults. See <see cref="Dumps(object?, int, bool, bool, ValueTuple{string, string}?, Func{object, object?}?)"/>.</param>
        /// <param name="default">Optional callback invoked for any value that is not natively
        /// JSON-serializable. See <see cref="Dumps(object?, int, bool, bool, ValueTuple{string, string}?, Func{object, object?}?)"/>.</param>
        /// <param name="cls">Optional <see cref="JSONEncoder"/> instance for custom encoding.</param>
        /// <param name="allowNan">When <c>true</c> (CPython's default), non-finite floats are
        /// emitted as <c>Infinity</c>/<c>-Infinity</c>/<c>NaN</c>; when <c>false</c> they raise
        /// <c>ValueError</c> (#1296).</param>
        public static void Dump(
            object? obj,
            TextFile fp,
            int indent = -1,
            bool sortKeys = false,
            bool ensureAscii = true,
            (string, string)? separators = null,
            Func<object, object?>? @default = null,
            JSONEncoder? cls = null,
            bool allowNan = true)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string json = Dumps(obj, indent, sortKeys, ensureAscii, separators, @default, cls, allowNan);
            fp.Write(json);
        }

        /// <summary>
        /// Deserialize a JSON document read from a file.
        /// </summary>
        /// <param name="fp">The file to read from.</param>
        /// <param name="cls">Optional <see cref="JSONDecoder"/> instance for custom decoding.</param>
        /// <param name="objectHook">Optional callback invoked for every decoded JSON object (dict).</param>
        /// <returns>The deserialized object.</returns>
        public static object? Load(
            TextFile fp,
            JSONDecoder? cls = null,
            Func<Dict<string, object?>, object?>? objectHook = null)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string content = fp.Read();
            return Loads(content, cls, objectHook);
        }

        /// <summary>
        /// The refusal message both doors use for an unpaired surrogate escape, so the agreement
        /// corpus sees identical text from both sides (#1487).
        /// </summary>
        private static string SurrogateRefusalMessage(string escapeText)
            => "Unpaired UTF-16 surrogate escape " + escapeText;

#if NET10_0_OR_GREATER
        private static readonly JsonSerializerOptions _typedOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            // Sharpy classes lower their instance attributes to public C# fields (not
            // properties), so typed deserialization must opt into field binding for any
            // user-defined Sharpy type to populate correctly (#843 dogfooding).
            IncludeFields = true,
            // Sharpy 'T?' fields lower to Optional<T>; without a converter STJ never
            // populates a present value (struct default is None) (#843 dogfooding).
            // Reads the sentinel object the bare-token pre-edit writes, at any float target
            // (#1488). Registered here rather than beside the pre-edit because the two halves are
            // one mechanism: the pre-edit encodes "this token was BARE" in a shape only a numeric
            // target accepts, and this decodes it. Reached for Optional<float> too, since
            // OptionalJsonConverter delegates to JsonSerializer.Deserialize<T>(ref reader, options)
            // rather than reading the number itself.
            Converters =
            {
                new OptionalJsonConverterFactory(),
                new BareNonFiniteJsonConverter<double>(),
                new BareNonFiniteJsonConverter<float>(),
            },
            // `dumps` emits Infinity/-Infinity/NaN by CPython's default (#1296), and the typed
            // reader has to be able to read back what the writer produces. Without this the two
            // halves of the module disagreed: the untyped `loads` (a hand-written parser) accepted
            // the three tokens while `loads[T]` returned JSONDecodeError for the same text, so
            // `dumps ∘ loads[T]` was broken for every non-finite float (#1353).
            //
            // Case sensitivity comes along for free and matches CPython, which accepts only the
            // exact spellings: `[infinity]`, `[nan]` and `[NAN]` all raise there and stay errors
            // here.
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,

            // Stated rather than inherited. This happens to be System.Text.Json's own default, but
            // leaving it implicit is what let the two doors drift: the untyped parser had no limit
            // at all and died of stack exhaustion on documents this one merely refuses (#1425).
            // Naming the shared constant makes a change to either door a change to both.
            MaxDepth = JsonParser.MaxDepth
        };

        /// <summary>
        /// Rewrites CPython's BARE non-finite tokens — <c>Infinity</c>, <c>-Infinity</c>,
        /// <c>NaN</c> — into a sentinel object only the numeric door accepts, leaving everything
        /// else byte-identical (#1353, #1488).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>JsonNumberHandling.AllowNamedFloatingPointLiterals</c> is necessary but NOT
        /// sufficient: it accepts the tokens only as JSON STRINGS (<c>"Infinity"</c>). CPython
        /// writes them bare, which is not valid JSON at all, so <c>Utf8JsonReader</c> fails while
        /// tokenizing — before any converter or option can intervene. Measured: with the option on,
        /// <c>["Infinity"]</c> reads as <c>inf</c> while <c>[Infinity]</c> still returns
        /// <c>'I' is an invalid start of a value</c>. A converter cannot be reached by a bare
        /// token, which is why a pre-edit exists at all.
        /// </para>
        /// <para>
        /// It used to rewrite to the QUOTED spelling, and that erased the very thing the pre-edit
        /// knows: that the token was bare. A rewritten <c>NaN</c> and an author's <c>"NaN"</c>
        /// became the same bytes, so <c>loads[str]("NaN")</c> answered <c>Ok("NaN")</c> where
        /// CPython reads a float and owes a decode error (#1488). Rewriting to a sentinel OBJECT
        /// keeps the distinction: <see cref="BareNonFiniteJsonConverter{T}"/> reads it back as the
        /// non-finite value at any numeric target, and any other target fails deserialization
        /// naturally — a JSON object is not a string — which the door already turns into
        /// <c>Err(JSONDecodeError)</c>. A genuinely quoted <c>"NaN"</c> is untouched throughout
        /// and keeps reading as the string.
        /// </para>
        /// <para>
        /// The scan is string-aware: an <c>Infinity</c> inside a JSON string is DATA and is left
        /// alone, and backslash escapes are tracked so a <c>\"</c> does not end a string early.
        /// A token is rewritten only at a token boundary, so <c>Infinity2</c> or a property named
        /// <c>NaNo</c> are untouched. Payloads without the substrings skip the walk entirely.
        /// </para>
        /// </remarks>
        internal static string RewriteBareNonFiniteTokens(string s)
        {
            if (s.IndexOf("Infinity", StringComparison.Ordinal) < 0
                && s.IndexOf("NaN", StringComparison.Ordinal) < 0)
            {
                return s;
            }

            var sb = new System.Text.StringBuilder(s.Length + 8);
            bool inString = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < s.Length)
                    {
                        sb.Append(s[++i]);
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    sb.Append(c);
                    continue;
                }

                string? token = MatchBareNonFiniteToken(s, i);
                if (token != null)
                {
                    sb.Append(BareNonFiniteSentinel(token));
                    i += token.Length - 1;
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// The bare non-finite token starting at <paramref name="i"/>, or <c>null</c>. A match must
        /// end at a token boundary so an identifier merely starting with one of the spellings is not
        /// rewritten.
        /// </summary>
        private static string? MatchBareNonFiniteToken(string s, int i)
        {
            foreach (var token in BareNonFiniteTokens)
            {
                if (i + token.Length > s.Length
                    || string.CompareOrdinal(s, i, token, 0, token.Length) != 0)
                {
                    continue;
                }

                int end = i + token.Length;
                if (end < s.Length)
                {
                    char next = s[end];
                    if (char.IsLetterOrDigit(next) || next == '_' || next == '.')
                    {
                        continue;
                    }
                }

                return token;
            }

            return null;
        }

        /// <summary>
        /// Longest first, so <c>-Infinity</c> is matched as one token rather than a minus followed
        /// by <c>Infinity</c> — rewriting only the tail would leave a stray minus in front of the sentinel.
        /// </summary>
        private static readonly string[] BareNonFiniteTokens = { "-Infinity", "Infinity", "NaN" };

        /// <summary>
        /// The property name marking a rewritten BARE non-finite token (#1488). Deliberately
        /// unspellable by accident: a <c>$</c> sigil and a dotted namespace, which no JSON schema
        /// in the wild uses as a data key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// COLLISION CAVEAT. This is a sentinel, not a proof. A document that genuinely contains
        /// <c>{"$sharpy.bare-nonfinite":"NaN"}</c> at a numeric target will be read as the
        /// non-finite value rather than refused. The alternative — a rewrite that carries no
        /// marker — is what #1488 was: it cannot be collided with because it cannot be
        /// distinguished at all, which is strictly worse. The collision requires an author to
        /// write this exact key, at a float-typed position, with one of exactly three values.
        /// </para>
        /// <para>
        /// SURFACE CAVEAT. <c>JsonElement</c> cannot represent NaN as a number — JSON has no such
        /// literal — so a <c>JsonConverter&lt;double&gt;</c> is never consulted for it and a
        /// caller doing <c>loads[JsonElement]</c> on a document with a bare token sees this object
        /// verbatim. That surface was already divergent from CPython (it previously saw the string
        /// <c>"NaN"</c>); the sentinel changes the shape of the divergence to one that is at least
        /// distinguishable from quoted data. Measured and recorded on #1488.
        /// </para>
        /// </remarks>
        internal const string BareNonFiniteSentinelKey = "$sharpy.bare-nonfinite";

        private static string BareNonFiniteSentinel(string token) =>
            "{\"" + BareNonFiniteSentinelKey + "\":\"" + token + "\"}";

        /// <summary>
        /// Deserialize a JSON string to a strongly-typed object using <c>System.Text.Json</c>.
        /// </summary>
        /// <typeparam name="T">The target type to deserialize into.</typeparam>
        /// <param name="s">The JSON string to deserialize.</param>
        /// <returns>A <see cref="Result{T,E}"/> containing the deserialized value on success,
        /// or a <see cref="JSONDecodeError"/> on failure.</returns>
        public static Result<T, JSONDecodeError> Loads<T>(string s)
        {
            if (s == null)
            {
                throw new TypeError("the JSON object must be str, not NoneType");
            }

            // Both doors refuse unpaired surrogate escapes with the same message (#1487).
            if (JsonSurrogateEscapes.FirstUnpaired(s) is { } unpaired)
            {
                return Result<T, JSONDecodeError>.Err(new JSONDecodeError(
                    SurrogateRefusalMessage(unpaired.escapeText), s, unpaired.position));
            }

            try
            {
                string prepared = RewriteBareNonFiniteTokens(s);

                // Absence is not a value: a required field the document omits must be an Err
                // naming it, not a fabricated 0/null/false (#1505, owner ruling 2026-08-13 —
                // decided for this door and yaml's together). The rule lives in
                // TypedLoadContract so the two doors cannot drift; the agreement corpus proves it.
                if (MissingRequiredField<T>(prepared) is { } missing)
                {
                    return Result<T, JSONDecodeError>.Err(new JSONDecodeError(
                        TypedLoadContract.MissingFieldMessage(typeof(T), missing), s, 0));
                }

                T? result = System.Text.Json.JsonSerializer.Deserialize<T>(prepared, _typedOptions);
                return Result<T, JSONDecodeError>.Ok(result!);
            }
            catch (JsonException ex)
            {
                int pos = (int)(ex.BytePositionInLine ?? 0);
                return Result<T, JSONDecodeError>.Err(new JSONDecodeError(ex.Message, s, pos));
            }
            catch (System.Exception ex)
            {
                // The door is crash-proof BY CONSTRUCTION, not per-exception-type. Catching only
                // JsonException left every other System.Text.Json fault to escape a function whose
                // signature promises a Result and kill the process: a multi-word snake_case
                // dataclass field made STJ's constructor binding fail with InvalidOperationException
                // for EVERY document, no diagnostic, no catch (#1504). Cataloguing which exceptions
                // STJ can throw would be the same hand-enumerated totality that produced the hole.
                //
                // Reserving exceptions for bugs (the stdlib contract) does not apply here: from the
                // caller's side a deserializer that cannot bind the target type is an expected
                // failure of THIS call, and the whole point of `Result` at this boundary is that
                // such a failure is a value. Position 0 — a binding fault has no offset in the text.
                //
                // The yaml twin (Yaml.cs SafeLoadTyped) has had this arm since #1424; json is the
                // door that did not, and the agreement corpus now proves they match.
                return Result<T, JSONDecodeError>.Err(new JSONDecodeError(ex.Message, s, 0));
            }
        }

        /// <summary>
        /// The first required field of <typeparamref name="T"/> that <paramref name="json"/>'s
        /// document tree omits, walking the whole tree (#1513), or <c>null</c> when none is missing.
        /// </summary>
        private static string? MissingRequiredField<T>(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    MaxDepth = JsonParser.MaxDepth,
                    AllowTrailingCommas = false,
                });

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return null;

                return TypedLoadContract.FirstMissingRequiredFieldPath<T>(
                    new JsonElementNode(document.RootElement));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Deserialize a JSON document read from a file to a strongly-typed object.
        /// </summary>
        /// <typeparam name="T">The target type to deserialize into.</typeparam>
        /// <param name="fp">The file to read from.</param>
        /// <returns>A <see cref="Result{T,E}"/> containing the deserialized value on success,
        /// or a <see cref="JSONDecodeError"/> on failure.</returns>
        public static Result<T, JSONDecodeError> Load<T>(TextFile fp)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string content = fp.Read();
            return Loads<T>(content);
        }

        /// <summary>
        /// <see cref="TypedLoadContract.IDocumentNode"/> adapter over <see cref="JsonElement"/>.
        /// </summary>
        private readonly struct JsonElementNode : TypedLoadContract.IDocumentNode
        {
            private readonly JsonElement _element;

            public JsonElementNode(JsonElement element)
            {
                _element = element;
            }

            public bool IsMapping => _element.ValueKind == JsonValueKind.Object;
            public bool IsSequence => _element.ValueKind == JsonValueKind.Array;

            public System.Collections.Generic.IEnumerable<string> Keys
            {
                get
                {
                    foreach (var property in _element.EnumerateObject())
                        yield return property.Name;
                }
            }

            public TypedLoadContract.IDocumentNode GetChild(string key)
                => new JsonElementNode(_element.GetProperty(key));

            public System.Collections.Generic.IEnumerable<(string indexLabel, TypedLoadContract.IDocumentNode element)> Elements
            {
                get
                {
                    int i = 0;
                    foreach (var item in _element.EnumerateArray())
                    {
                        yield return (i.ToString(System.Globalization.CultureInfo.InvariantCulture), new JsonElementNode(item));
                        i++;
                    }
                }
            }
        }
#endif
    }
}
