using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
