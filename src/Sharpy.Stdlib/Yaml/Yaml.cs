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
        /// <returns>The YAML string representation of <paramref name="data"/>.</returns>
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

            var builder = new StringBuilder();
            bool first = true;
            foreach (object? document in documents)
            {
                if (!first)
                {
                    builder.Append("---\n");
                }

                builder.Append(DumpSingle(document, defaultFlowStyle, indent, sortKeys, allowUnicode, width));
                first = false;
            }

            return builder.ToString();
        }

        private static IDeserializer CreateDeserializer()
        {
            return new DeserializerBuilder()
                .WithAttemptingUnquotedStringTypeDeserialization()
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
            int width)
        {
            SerializerBuilder builder = new SerializerBuilder().DisableAliases();
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

            // Serializer.Serialize(IEmitter, graph) emits a complete stream
            // (StreamStart..StreamEnd), so it owns the emitter for one document.
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            var emitter = new Emitter(writer, settings);
            serializer.Serialize(emitter, converted);
            return writer.ToString();
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
        /// <returns>The YAML string with comments preserved.</returns>
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
                // Mirror the json module's snake_case, lenient mapping for typed loads.
                IDeserializer deserializer = new DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
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
#endif
    }

    /// <summary>
    /// Forces mappings and sequences to be emitted in flow style. Used to implement
    /// <c>default_flow_style=True</c>.
    /// </summary>
    internal sealed class FlowStyleEventEmitter : ChainedEventEmitter
    {
        public FlowStyleEventEmitter(IEventEmitter nextEmitter) : base(nextEmitter) { }

        public override void Emit(MappingStartEventInfo eventInfo, IEmitter emitter)
        {
            eventInfo.Style = MappingStyle.Flow;
            base.Emit(eventInfo, emitter);
        }

        public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
        {
            eventInfo.Style = SequenceStyle.Flow;
            base.Emit(eventInfo, emitter);
        }
    }
}
