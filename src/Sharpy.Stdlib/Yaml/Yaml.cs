using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible <c>yaml</c> module backed by YamlDotNet.
    /// Provides safe load/dump for strings, files, and multi-document streams.
    /// </summary>
    public static partial class Yaml
    {
        /// <summary>
        /// Parse the first YAML document in <paramref name="text"/> and return the
        /// corresponding Sharpy value (Dict, List, or scalar).
        /// </summary>
        /// <param name="text">The YAML text to parse.</param>
        /// <returns>The parsed value, or <c>null</c> for an empty document.</returns>
        /// <exception cref="YAMLParseError">Thrown when the input cannot be parsed.</exception>
        public static object? SafeLoad(string text)
        {
            if (text is null)
            {
                throw new TypeError("the YAML input must be str, not NoneType");
            }

            IDeserializer deserializer = CreateDeserializer();
            try
            {
                object? value = deserializer.Deserialize<object>(text);
                return YamlConverter.ToSharpy(value);
            }
            catch (YamlException ex)
            {
                throw ToParseError(ex);
            }
        }

        /// <summary>
        /// Serialize <paramref name="data"/> to a YAML formatted string.
        /// </summary>
        /// <param name="data">The Sharpy value to serialize.</param>
        /// <param name="defaultFlowStyle">When <c>true</c>, emit collections in flow style
        /// (<c>{a: 1}</c>); otherwise use block style. Mirrors Python's <c>default_flow_style</c>.</param>
        /// <param name="indent">Number of spaces per indentation level (1-9).</param>
        /// <param name="sortKeys">Whether to sort mapping keys.</param>
        /// <param name="allowUnicode">Whether to allow non-ASCII characters unescaped.</param>
        /// <param name="width">Preferred maximum line width before wrapping.</param>
        /// <returns>The YAML string representation of <paramref name="data"/>. A document that is a
        /// single plain scalar is terminated with YAML's document-end marker <c>...</c>, matching
        /// PyYAML; a quoted scalar, a mapping and a sequence are not.</returns>
        public static string SafeDump(
            object? data,
            bool defaultFlowStyle = false,
            int indent = 2,
            bool sortKeys = true,
            bool allowUnicode = true,
            int width = 80)
        {
            return DumpSingle(data, defaultFlowStyle, indent, sortKeys, allowUnicode, width);
        }

        /// <summary>
        /// Parse the first YAML document read from a file and return the corresponding Sharpy value.
        /// </summary>
        /// <param name="fp">The file to read from.</param>
        /// <returns>The parsed value, or <c>null</c> for an empty document.</returns>
        /// <exception cref="YAMLParseError">Thrown when the input cannot be parsed.</exception>
        public static object? SafeLoadFile(TextFile fp)
        {
            if (fp is null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            return SafeLoad(fp.Read());
        }

        /// <summary>
        /// Serialize <paramref name="data"/> to a file as a YAML formatted document.
        /// </summary>
        /// <param name="data">The Sharpy value to serialize.</param>
        /// <param name="fp">The file to write to.</param>
        /// <param name="defaultFlowStyle">When <c>true</c>, emit collections in flow style.</param>
        /// <param name="indent">Number of spaces per indentation level (1-9).</param>
        /// <param name="sortKeys">Whether to sort mapping keys.</param>
        /// <param name="allowUnicode">Whether to allow non-ASCII characters unescaped.</param>
        /// <param name="width">Preferred maximum line width before wrapping.</param>
        public static void SafeDumpFile(
            object? data,
            TextFile fp,
            bool defaultFlowStyle = false,
            int indent = 2,
            bool sortKeys = true,
            bool allowUnicode = true,
            int width = 80)
        {
            if (fp is null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            fp.Write(SafeDump(data, defaultFlowStyle, indent, sortKeys, allowUnicode, width));
        }

        /// <summary>
        /// Parse all YAML documents in a multi-document stream (separated by <c>---</c>).
        /// </summary>
        /// <param name="text">The YAML text to parse.</param>
        /// <returns>A list with one entry per parsed document.</returns>
        /// <exception cref="YAMLParseError">Thrown when the input cannot be parsed.</exception>
        public static List<object?> SafeLoadAll(string text)
        {
            if (text is null)
            {
                throw new TypeError("the YAML input must be str, not NoneType");
            }

            var documents = new List<object?>();
            IDeserializer deserializer = CreateDeserializer();
            try
            {
                var parser = new Parser(new StringReader(text));
                parser.Consume<StreamStart>();
                while (parser.Accept<DocumentStart>(out _))
                {
                    object? value = deserializer.Deserialize<object>(parser);
                    documents.Append(YamlConverter.ToSharpy(value));
                }
            }
            catch (YamlException ex)
            {
                throw ToParseError(ex);
            }

            return documents;
        }

        /// <summary>
        /// Serialize a sequence of documents into a single multi-document YAML string,
        /// separating documents with <c>---</c>.
        /// </summary>
        /// <param name="documents">The documents to serialize.</param>
        /// <param name="defaultFlowStyle">When <c>true</c>, emit collections in flow style.</param>
        /// <param name="indent">Number of spaces per indentation level (1-9).</param>
        /// <param name="sortKeys">Whether to sort mapping keys.</param>
        /// <param name="allowUnicode">Whether to allow non-ASCII characters unescaped.</param>
        /// <param name="width">Preferred maximum line width before wrapping.</param>
        /// <returns>The multi-document YAML string.</returns>
        public static string SafeDumpAll(
            List<object?> documents,
            bool defaultFlowStyle = false,
            int indent = 2,
            bool sortKeys = true,
            bool allowUnicode = true,
            int width = 80)
        {
            if (documents is null)
            {
                throw new TypeError("expected list, got NoneType");
            }

            // The stream shape, matched to PyYAML 6.0.3 cell for cell (#1471 — 19 shapes measured
            // and recorded on the issue). #1348 had excluded this path from the end-marker rule
            // entirely, because letting each document append its own marker would have given
            // `1.0\n...\n---\nhello\n...\n` — markers BETWEEN documents, which is neither PyYAML's
            // shape nor the old one. That exclusion was right about the danger and wrong about the
            // remedy, and the finding that resolves it is that there is NO second marker rule:
            //
            //   PyYAML's "one marker at stream end" is #1348's existing per-document rule applied
            //   to the LAST document instead of to every document.
            //
            // So the marker question is still asked in exactly one place. This method only decides
            // (a) which document gets asked, and (b) how the separator folds — both genuinely
            // stream-level concerns.
            //
            // Separator folding: `---` precedes documents 2..n, and the root node follows INLINE
            // unless it is a BLOCK collection, which starts on the next line. The tempting rule
            // "inline when the document is one line" is wrong — `[{'a':1},{'b':2}]` gives
            // `a: 1\n---\nb: 2\n`, where `b: 2` is one line and still gets its own. The axis is
            // block-vs-flow: flow collections (`[]`, `{}`, and anything under defaultFlowStyle)
            // fold inline, block ones do not. Read off the emitted text's first character, since
            // an emitted flow collection always opens with `[` or `{` and a block one never does.
            var builder = new StringBuilder();
            for (int i = 0; i < documents.Length; i++)
            {
                object? document = documents[i];
                bool isLast = i == documents.Length - 1;

                string body = DumpSingle(
                    document, defaultFlowStyle, indent, sortKeys, allowUnicode, width,
                    emitDocumentEndMarker: isLast);

                if (i > 0)
                {
                    bool startsOwnLine = YamlDocumentEnd.IsCollection(document)
                        && !(body.Length > 0 && (body[0] == '[' || body[0] == '{'));
                    builder.Append(startsOwnLine ? "---\n" : "--- ");
                }

                builder.Append(body);
            }

            return builder.ToString();
        }

        /// <summary>
        /// The untyped loader. Plain scalars resolve through <see cref="YamlScalarResolver"/>
        /// rather than YamlDotNet's <c>WithAttemptingUnquotedStringTypeDeserialization()</c>,
        /// which tried <c>float</c> before <c>double</c> and so put every plain float through a
        /// single-precision detour — <c>safe_load("0.1")</c> came back
        /// <c>0.10000000149011612</c> (#1339). Registered <c>OnTop</c> so it sees untagged
        /// scalars before the built-in deserializers do.
        /// </summary>
        private static IDeserializer CreateDeserializer()
        {
            return new DeserializerBuilder()
                .WithNodeDeserializer(
                    new PlainScalarNodeDeserializer(),
                    selection => selection.OnTop())
                .Build();
        }

        private static YAMLParseError ToParseError(YamlException ex)
        {
            return new YAMLParseError(ex.Message, null, ex.Start.Line, ex.Start.Column, ex);
        }

        private static string DumpSingle(
            object? data,
            bool defaultFlowStyle,
            int indent,
            bool sortKeys,
            bool allowUnicode,
            int width,
            bool emitDocumentEndMarker = true)
        {
            SerializerBuilder builder = new SerializerBuilder().DisableAliases();

            // Unconditional: float spelling is not a style choice (#1229).
            builder = builder.WithTypeConverter(new YamlFloatTypeConverter());

            // Unconditional for the same reason: whether a string needs quotes is not a style
            // choice either — it is whether the text would read back as something else (#1417).
            builder = builder.WithTypeConverter(new YamlStringStyleConverter());

            if (defaultFlowStyle)
            {
                builder = builder.WithEventEmitter(next => new FlowStyleEventEmitter(next));
            }

            ISerializer serializer = builder.Build();

            int safeIndent = indent < 1 ? 1 : (indent > 9 ? 9 : indent);
            int safeWidth = width <= 0 ? int.MaxValue : width;
            EmitterSettings settings = EmitterSettings.Default
                .WithBestIndent(safeIndent)
                .WithBestWidth(safeWidth);

            object? converted = YamlConverter.ToYamlDotNet(data);
            if (sortKeys)
            {
                converted = SortKeys(converted);
            }

            string emitted;
            if (converted is null)
            {
                // YamlDotNet writes a null document as an EMPTY scalar, and that is not "null
                // spelled differently" — it is no value at all. `--- \n` is read by a conforming
                // parser as an EMPTY DOCUMENT, so the value was being lost rather than formatted
                // oddly (#1467). PyYAML writes the plain scalar `null`.
                //
                // Emitted directly instead of taught to the serializer because a null root has no
                // indent, width or flow-style question for the serializer to answer — and going
                // through it is what produced the empty scalar in the first place. The `...` is
                // NOT appended here: spelling the value plainly is enough for #1348's existing
                // rule below to supply it, which is the composition this fix is supposed to have.
                emitted = "null\n";
            }
            else
            {
                // Serializer.Serialize(IEmitter, graph) emits a complete stream
                // (StreamStart..StreamEnd), so it owns the emitter for one document.
                using var writer = new StringWriter(CultureInfo.InvariantCulture);
                var emitter = new Emitter(writer, settings);
                serializer.Serialize(emitter, converted);
                emitted = YamlDocumentStart.Suppress(writer.ToString(), converted);
            }

            return emitDocumentEndMarker
                ? YamlDocumentEnd.Append(emitted, converted)
                : emitted;
        }

        private static object? SortKeys(object? value)
        {
            if (value is Dictionary<object, object?> dict)
            {
                var keys = new System.Collections.Generic.List<object>(dict.Keys);
                keys.Sort(static (a, b) => string.CompareOrdinal(
                    System.Convert.ToString(a, CultureInfo.InvariantCulture),
                    System.Convert.ToString(b, CultureInfo.InvariantCulture)));

                var sorted = new Dictionary<object, object?>(dict.Count);
                foreach (object key in keys)
                {
                    sorted[key] = SortKeys(dict[key]);
                }

                return sorted;
            }

            // Sequences produced by YamlConverter.ToYamlDotNet are Sharpy lists.
            if (value is List<object?> list)
            {
                var result = new List<object?>();
                foreach (object? item in list)
                {
                    result.Add(SortKeys(item));
                }

                return result;
            }

            return value;
        }

        /// <summary>
        /// Parse a YAML document preserving comments, key order, and formatting.
        /// Mappings become <see cref="CommentedMap"/>, sequences become
        /// <see cref="CommentedSeq"/>, and scalars are converted to their natural types.
        /// </summary>
        /// <param name="text">The YAML text to parse.</param>
        /// <returns>The parsed value with comments preserved.</returns>
        /// <exception cref="YAMLParseError">Thrown when the input cannot be parsed.</exception>
        public static object? RoundtripLoad(string text)
        {
            if (text is null)
            {
                throw new TypeError("the YAML input must be str, not NoneType");
            }

            return YamlRoundtrip.RoundtripLoad(text);
        }

        /// <summary>
        /// Serialize data to YAML, re-emitting any comments stored in
        /// <see cref="CommentedMap"/>/<see cref="CommentedSeq"/> nodes.
        /// </summary>
        /// <param name="data">The data to serialize (may include commented nodes).</param>
        /// <param name="indent">Number of spaces per indentation level.</param>
        /// <returns>The YAML string with comments preserved. The document-end marker follows the
        /// same rule as <c>safe_dump</c> — the two dump surfaces share one authority for it, so
        /// they cannot disagree about whether a given document carries <c>...</c>.</returns>
        public static string RoundtripDump(object? data, int indent = 2)
        {
            return YamlRoundtrip.RoundtripDump(data, indent);
        }

#if NET10_0_OR_GREATER
        /// <summary>
        /// Deserialize a YAML string into a strongly-typed object.
        /// </summary>
        /// <typeparam name="T">The target type to deserialize into.</typeparam>
        /// <param name="text">The YAML text to parse.</param>
        /// <returns>A <see cref="Result{T,E}"/> containing the deserialized value on success,
        /// or a <see cref="YAMLError"/> on failure.</returns>
        public static Result<T, YAMLError> SafeLoadTyped<T>(string text)
        {
            if (text is null)
            {
                throw new TypeError("the YAML input must be str, not NoneType");
            }

            try
            {
                // Absence is not a value. AllFieldsConstructorObjectFactory constructs with
                // placeholders and lets the property pass populate, so an absent key used to leave
                // the placeholder standing: `safe_load_typed[Config]("port: 8080")` returned Ok
                // with max_connections == 0 for a field the user never made optional (#1505). The
                // rule is TypedLoadContract's, shared with json.loads[T] so the two typed doors
                // cannot drift; the agreement corpus runs the same document through both.
                if (MissingRequiredField<T>(text) is { } missing)
                {
                    return Result<T, YAMLError>.Err(new YAMLError(
                        TypedLoadContract.MissingFieldMessage(typeof(T), missing)));
                }

                // Mirror the json module's snake_case, lenient mapping for typed loads.
                IDeserializer deserializer = new DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                    .WithObjectFactory(new AllFieldsConstructorObjectFactory())
                    .IgnoreUnmatchedProperties()
                    .Build();
                T value = deserializer.Deserialize<T>(text);
                return Result<T, YAMLError>.Ok(value);
            }
            catch (YamlException ex)
            {
                return Result<T, YAMLError>.Err(ToParseError(ex));
            }
            catch (System.Exception ex)
            {
                return Result<T, YAMLError>.Err(new YAMLError(ex.Message, ex));
            }
        }

        /// <summary>
        /// The first required field of <typeparamref name="T"/> that <paramref name="text"/>'s
        /// document tree omits, walking the whole tree (#1513), or <c>null</c> when none is missing.
        /// </summary>
        private static string? MissingRequiredField<T>(string text)
        {
            try
            {
                object? untyped = CreateDeserializer().Deserialize<object>(text);
                return TypedLoadContract.FirstMissingRequiredFieldPath<T>(
                    new YamlObjectNode(untyped));
            }
            catch (YamlException)
            {
                return null;
            }
        }

        /// <summary>
        /// <see cref="TypedLoadContract.IDocumentNode"/> adapter over the untyped YAML object
        /// graph (<c>IDictionary</c> → mapping, <c>IList</c> → sequence).
        /// </summary>
        private readonly struct YamlObjectNode : TypedLoadContract.IDocumentNode
        {
            private readonly object? _value;

            public YamlObjectNode(object? value)
            {
                _value = value;
            }

            public bool IsMapping => _value is System.Collections.IDictionary;
            public bool IsSequence => _value is System.Collections.IList && !(_value is string);

            public IEnumerable<string> Keys => TypedLoadContract.KeysOf(_value);

            public TypedLoadContract.IDocumentNode GetChild(string key)
            {
                if (_value is IDictionary<string, object?> typed)
                    return new YamlObjectNode(typed[key]);

                if (_value is System.Collections.IDictionary untyped)
                    return new YamlObjectNode(untyped[key]);

                throw new InvalidOperationException("not a mapping");
            }

            public IEnumerable<(string indexLabel, TypedLoadContract.IDocumentNode element)> Elements
            {
                get
                {
                    if (_value is System.Collections.IList list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            yield return (
                                i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                new YamlObjectNode(list[i]));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Constructs deserialization targets that have no parameterless constructor, by calling
        /// their single constructor with default arguments and letting the deserializer set the
        /// properties afterwards.
        ///
        /// <para>This is what makes <c>safe_load_typed[T]</c> work for a <c>@dataclass</c>, the
        /// idiomatic Sharpy spelling for the config record this function exists to produce. A
        /// dataclass lowers to settable properties plus an all-fields constructor and nothing else,
        /// so YamlDotNet's default factory — which calls <c>Activator.CreateInstance</c> — failed
        /// with <c>MissingMethodException</c>, surfaced through the Result's error arm as a .NET
        /// reflection message mentioning neither YAML nor dataclasses (#1424).</para>
        ///
        /// <para>THE DECIDING MEASUREMENT: <c>json.loads[T]</c> already deserializes into a
        /// dataclass, because System.Text.Json matches constructor parameters to property names.
        /// Refusing the shape in yaml would have left the two serializers giving opposite answers
        /// for the same user type; emitting a parameterless constructor for every dataclass would
        /// have changed the language, and its immutability rules, for one consumer. Making the
        /// loader construct what the language already emits is the only option that leaves both
        /// alone.</para>
        ///
        /// <para>Only a SINGLE constructor is used. A type with several is ambiguous, and guessing
        /// would be worse than the default factory's own error, so those fall through to it.</para>
        /// </summary>
        private sealed class AllFieldsConstructorObjectFactory : YamlDotNet.Serialization.ObjectFactories.DefaultObjectFactory
        {
            public override object Create(System.Type type)
            {
                if (type.GetConstructor(System.Type.EmptyTypes) != null)
                {
                    return base.Create(type);
                }

                var constructors = type.GetConstructors();
                if (constructors.Length != 1)
                {
                    return base.Create(type);
                }

                var parameters = constructors[0].GetParameters();
                var arguments = new object?[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    arguments[i] = parameters[i].HasDefaultValue
                        ? parameters[i].DefaultValue
                        : parameters[i].ParameterType.IsValueType
                            ? System.Activator.CreateInstance(parameters[i].ParameterType)
                            : null;
                }

                return constructors[0].Invoke(arguments);
            }
        }
#endif
    }

    /// <summary>
    /// Spells floats the way PyYAML does, for the <c>safe_dump</c> family (#1229).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this, YamlDotNet's own scalar rendering emitted <c>1</c> for <c>1.0</c> — so a float
    /// dumped by <c>safe_dump</c> reloaded as an <b>int</b>. That is value corruption, not a spelling
    /// difference, and it was in the API users actually call: <c>YamlRoundtrip</c>'s emitter (used by
    /// <c>roundtrip_dump</c>) had its own, better, formatting that this path never saw. Both surfaces
    /// now read <see cref="YamlFloatFormat"/> — the two had drifted precisely because each formatted
    /// floats itself.
    /// </para>
    /// <para>
    /// A type converter rather than a <c>ChainedEventEmitter</c>: an emitter that sets
    /// <c>RenderedValue</c> is overwritten by the built-in type-assigning emitter further down the
    /// chain, so the scalar came out unchanged. A converter owns the emission outright. Registered on
    /// the SERIALIZER only — <c>CreateDeserializer</c> builds separately, so reading is untouched and
    /// <see cref="ReadYaml"/> is unreachable.
    /// </para>
    /// </remarks>
    internal sealed class YamlFloatTypeConverter : IYamlTypeConverter
    {
        /// <inheritdoc />
        public bool Accepts(Type type) => type == typeof(double) || type == typeof(float);

        /// <inheritdoc />
        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
            => throw new NotSupportedException(
                "YamlFloatTypeConverter is registered on the serializer only; parsing floats stays with the deserializer.");

        /// <inheritdoc />
        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            string text = value switch
            {
                float f => YamlFloatFormat.Format(f),
                double d => YamlFloatFormat.Format(d),
                _ => string.Empty
            };

            // Plain style: every spelling this produces (1.0, 1.0e+20, .inf, .nan) is a valid plain
            // YAML scalar that reloads as a float, so quoting would break the round-trip.
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, text, ScalarStyle.Plain, true, false));
        }
    }

    /// <summary>
    /// Styles every string scalar through <see cref="YamlScalarStyleAuthority"/> — the one
    /// authority that implements PyYAML 6.0.3's scalar-style rules (#1542). Never emits
    /// <c>ScalarStyle.Any</c> — every string gets an explicit decision.
    /// </summary>
    /// <remarks>
    /// A type converter rather than a <c>ChainedEventEmitter</c>, for the reason recorded on
    /// <see cref="YamlFloatTypeConverter"/>: an emitter that sets <c>RenderedValue</c> is
    /// overwritten by the built-in type-assigning emitter further down the chain. Registered on the
    /// SERIALIZER only, so reading is untouched.
    /// </remarks>
    internal sealed class YamlStringStyleConverter : IYamlTypeConverter
    {
        /// <inheritdoc />
        public bool Accepts(Type type) => type == typeof(string);

        /// <inheritdoc />
        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
            => throw new NotSupportedException(
                "YamlStringStyleConverter is registered on the serializer only.");

        /// <inheritdoc />
        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            string text = value as string ?? string.Empty;
            bool wouldResolve = !(YamlScalarResolver.Resolve(text) is string);
            var decided = YamlScalarStyleAuthority.Choose(text, wouldResolve);

            ScalarStyle style = decided switch
            {
                YamlScalarStyleAuthority.Style.SingleQuoted => ScalarStyle.SingleQuoted,
                YamlScalarStyleAuthority.Style.DoubleQuoted => ScalarStyle.DoubleQuoted,
                _ => ScalarStyle.Plain,
            };

            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, text, style, true, false));
        }
    }

    /// <summary>
    /// Resolves untagged PLAIN scalars for the untyped loader, at double precision (#1339).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Style is the whole gate. A quoted scalar is a string by YAML's own rules, so only
    /// <see cref="ScalarStyle.Plain"/> is resolved and everything else falls through to the
    /// built-in deserializers — <c>safe_load("\"0.1\"")</c> must stay the string "0.1". A
    /// scalar carrying an explicit tag also falls through: the tag is the author saying what
    /// they meant.
    /// </para>
    /// <para>
    /// Only for <c>object</c>-typed requests. The typed path (<c>SafeLoadTyped&lt;T&gt;</c>)
    /// builds its own deserializer and never sees this, but a target type of <c>string</c>
    /// inside an untyped document must still get its string.
    /// </para>
    /// </remarks>
    internal sealed class PlainScalarNodeDeserializer : INodeDeserializer
    {
        /// <inheritdoc />
        public bool Deserialize(
            IParser reader,
            Type expectedType,
            Func<IParser, Type, object?> nestedObjectDeserializer,
            out object? value,
            ObjectDeserializer rootDeserializer)
        {
            if (expectedType == typeof(object)
                && reader.Accept<Scalar>(out Scalar? scalar)
                && scalar != null
                && scalar.Style == ScalarStyle.Plain
                && scalar.Tag.IsEmpty)
            {
                reader.MoveNext();
                value = YamlScalarResolver.Resolve(scalar.Value);
                return true;
            }

            value = null;
            return false;
        }
    }

    /// <summary>
    /// Forces mappings and sequences to be emitted in flow style. Used to implement
    /// <c>default_flow_style=True</c>.
    /// </summary>
    internal sealed class FlowStyleEventEmitter : ChainedEventEmitter
    {
        public FlowStyleEventEmitter(IEventEmitter nextEmitter) : base(nextEmitter) { }

        /// <inheritdoc />
        public override void Emit(MappingStartEventInfo eventInfo, IEmitter emitter)
        {
            eventInfo.Style = MappingStyle.Flow;
            base.Emit(eventInfo, emitter);
        }

        /// <inheritdoc />
        public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
        {
            eventInfo.Style = SequenceStyle.Flow;
            base.Emit(eventInfo, emitter);
        }
    }
}
