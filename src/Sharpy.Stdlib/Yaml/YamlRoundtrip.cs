using System.Globalization;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

// Closed-generic alias: inside the `Sharpy` namespace an unqualified `List<T>` would bind to
// `Sharpy.List<T>`, so we alias the BCL list type we use for buffering parsing events.
using EventBuffer = System.Collections.Generic.List<YamlDotNet.Core.Events.ParsingEvent>;

namespace Sharpy
{
    /// <summary>
    /// Implements ruamel.yaml-style roundtrip loading and dumping by walking YamlDotNet's
    /// low-level event stream so that comments can be captured into (and re-emitted from)
    /// <see cref="CommentedMap"/>/<see cref="CommentedSeq"/> structures.
    /// </summary>
    internal static class YamlRoundtrip
    {
        // ---------------------------------------------------------------------
        // Loading
        // ---------------------------------------------------------------------

        /// <summary>
        /// Parses <paramref name="text"/> preserving comments. Mappings become
        /// <see cref="CommentedMap"/>, sequences become <see cref="CommentedSeq"/>, and
        /// scalars are converted to bool/long/double/string/null.
        /// </summary>
        internal static object? RoundtripLoad(string text)
        {
            // skipComments: false makes the scanner surface Comment tokens so the parser
            // emits Comment events we can associate with the surrounding nodes.
            var scanner = new Scanner(new StringReader(text ?? string.Empty), skipComments: false);
            IParser parser = new Parser(scanner);

            var events = new EventBuffer();
            try
            {
                while (parser.MoveNext())
                {
                    events.Add(parser.Current!);
                }
            }
            catch (YamlException ex)
            {
                throw new YAMLParseError(ex.Message, null, ex.Start.Line, ex.Start.Column, ex);
            }

            var reader = new EventReader(events);

            // StreamStart
            if (reader.Current is StreamStart)
            {
                reader.Advance();
            }

            // Empty stream → empty document.
            if (reader.Current is null || reader.Current is StreamEnd)
            {
                return null;
            }

            // DocumentStart
            if (reader.Current is DocumentStart)
            {
                reader.Advance();
            }

            // Comments appearing before the root node attach to its first child.
            string? leading = CollectStandaloneComments(reader);
            return ParseNode(reader, leading);
        }

        private static object? ParseNode(EventReader reader, string? pendingBefore)
        {
            switch (reader.Current)
            {
                case MappingStart:
                    reader.Advance();
                    return ParseMapping(reader, pendingBefore);

                case SequenceStart:
                    reader.Advance();
                    return ParseSequence(reader, pendingBefore);

                case Scalar scalar:
                    reader.Advance();
                    return ConvertScalar(scalar);

                default:
                    // Aliases/unknown events are not supported in roundtrip mode; skip.
                    reader.Advance();
                    return null;
            }
        }

        private static CommentedMap ParseMapping(EventReader reader, string? pendingBefore)
        {
            var map = new CommentedMap();
            string? before = pendingBefore;
            string? lastKey = null;

            while (true)
            {
                string? more = CollectStandaloneComments(reader);
                before = JoinComments(before, more);

                if (reader.Current is null || reader.Current is MappingEnd)
                {
                    // Dangling comments become an "after" comment on the final key.
                    if (before != null && lastKey != null)
                    {
                        map.GetOrAddComment(lastKey).AfterComment = before;
                    }
                    reader.Advance();
                    break;
                }

                // Key (always materialized as a string for CommentedMap).
                string key = reader.Current is Scalar keyScalar ? keyScalar.Value : string.Empty;
                reader.Advance();

                // A comment between the key and its value (rare).
                string? keyInline = TakeInlineComment(reader);

                object? value = ParseNode(reader, null);

                // A comment trailing the value on the same line.
                string? inline = TakeInlineComment(reader) ?? keyInline;

                map.Add(key, value);
                lastKey = key;

                if (before != null || inline != null)
                {
                    var info = map.GetOrAddComment(key);
                    info.BeforeComment = before;
                    info.InlineComment = inline;
                }

                before = null;
            }

            return map;
        }

        private static CommentedSeq ParseSequence(EventReader reader, string? pendingBefore)
        {
            var seq = new CommentedSeq();
            string? before = pendingBefore;
            int index = 0;

            while (true)
            {
                string? more = CollectStandaloneComments(reader);
                before = JoinComments(before, more);

                if (reader.Current is null || reader.Current is SequenceEnd)
                {
                    if (before != null && index > 0)
                    {
                        seq.GetOrAddComment(index - 1).AfterComment = before;
                    }
                    reader.Advance();
                    break;
                }

                object? item = ParseNode(reader, null);
                string? inline = TakeInlineComment(reader);

                seq.Add(item);

                if (before != null || inline != null)
                {
                    var info = seq.GetOrAddComment(index);
                    info.BeforeComment = before;
                    info.InlineComment = inline;
                }

                before = null;
                index++;
            }

            return seq;
        }

        /// <summary>Consumes consecutive standalone (non-inline) comments, joined by newlines.</summary>
        private static string? CollectStandaloneComments(EventReader reader)
        {
            string? acc = null;
            while (reader.Current is Comment comment && !comment.IsInline)
            {
                acc = JoinComments(acc, comment.Value);
                reader.Advance();
            }
            return acc;
        }

        /// <summary>Consumes a single inline comment if it is the current event.</summary>
        private static string? TakeInlineComment(EventReader reader)
        {
            if (reader.Current is Comment comment && comment.IsInline)
            {
                reader.Advance();
                return comment.Value;
            }
            return null;
        }

        private static string? JoinComments(string? first, string? second)
        {
            if (first == null)
            {
                return second;
            }
            if (second == null)
            {
                return first;
            }
            return first + "\n" + second;
        }

        /// <summary>
        /// Converts a YAML scalar to a C# value, honoring quoting: quoted/block scalars are
        /// always strings, plain scalars are resolved to null/bool/int/long/double/string
        /// following YAML core schema conventions.
        /// </summary>
        private static object? ConvertScalar(Scalar scalar)
        {
            // Quoted or block scalars are always strings — never reinterpreted.
            if (scalar.Style != ScalarStyle.Plain && scalar.Style != ScalarStyle.Any)
            {
                return scalar.Value;
            }

            return ResolvePlainScalar(scalar.Value);
        }

        private static object? ResolvePlainScalar(string value)
        {
            if (value.Length == 0 || value == "~" ||
                value == "null" || value == "Null" || value == "NULL")
            {
                return null;
            }

            if (value == "true" || value == "True" || value == "TRUE")
            {
                return true;
            }
            if (value == "false" || value == "False" || value == "FALSE")
            {
                return false;
            }

            if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long longValue))
            {
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                {
                    return (int)longValue;
                }
                return longValue;
            }

            if (value == ".inf" || value == ".Inf" || value == ".INF" || value == "+.inf")
            {
                return double.PositiveInfinity;
            }
            if (value == "-.inf" || value == "-.Inf" || value == "-.INF")
            {
                return double.NegativeInfinity;
            }
            if (value == ".nan" || value == ".NaN" || value == ".NAN")
            {
                return double.NaN;
            }

            // Only treat as float when it actually looks like one, so that values such as
            // hexadecimal-looking strings are not silently coerced.
            if (LooksLikeFloat(value) &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                return doubleValue;
            }

            return value;
        }

        private static bool LooksLikeFloat(string value)
        {
            bool hasDigit = false;
            foreach (char c in value)
            {
                if (c >= '0' && c <= '9')
                {
                    hasDigit = true;
                }
                else if (c != '.' && c != 'e' && c != 'E' && c != '+' && c != '-')
                {
                    return false;
                }
            }
            return hasDigit && (value.IndexOf('.') >= 0 || value.IndexOf('e') >= 0 || value.IndexOf('E') >= 0);
        }

        // ---------------------------------------------------------------------
        // Dumping
        // ---------------------------------------------------------------------

        /// <summary>
        /// Serializes <paramref name="data"/> to YAML, re-emitting stored comments for
        /// <see cref="CommentedMap"/>/<see cref="CommentedSeq"/> nodes. Plain
        /// <see cref="Dict{K,V}"/>/<see cref="List{T}"/> data is emitted without comments.
        /// </summary>
        /// <remarks>
        /// Known limitation: YamlDotNet's emitter only renders comments while emitting in
        /// block style. Comments associated with nodes that are emitted in flow style are
        /// silently dropped. Roundtrip output therefore always uses block style.
        /// </remarks>
        internal static string RoundtripDump(object? data, int indent = 2)
        {
            int bestIndent = indent < 1 ? 1 : indent;
            var writer = new StringWriter();
            var settings = new EmitterSettings(
                bestIndent: bestIndent,
                bestWidth: int.MaxValue,
                isCanonical: false,
                maxSimpleKeyLength: 1024,
                skipAnchorName: false,
                indentSequences: true,
                newLine: "\n",
                useUtf16SurrogatePairs: false);
            var emitter = new Emitter(writer, settings);

            emitter.Emit(new StreamStart());
            emitter.Emit(new DocumentStart(null, null, isImplicit: true));
            EmitNode(emitter, data);
            emitter.Emit(new DocumentEnd(isImplicit: true));
            emitter.Emit(new StreamEnd());

            return writer.ToString();
        }

        private static void EmitNode(IEmitter emitter, object? value)
        {
            switch (value)
            {
                case CommentedMap map:
                    EmitCommentedMap(emitter, map);
                    return;

                case CommentedSeq seq:
                    EmitCommentedSeq(emitter, seq);
                    return;
            }

            // Plain (non-commented) data: normalize Sharpy/.NET containers into shapes the
            // generic emitter understands, then emit without comments.
            EmitPlain(emitter, YamlConverter.ToYamlDotNet(value));
        }

        private static void EmitCommentedMap(IEmitter emitter, CommentedMap map)
        {
            emitter.Emit(new MappingStart(AnchorName.Empty, TagName.Empty, true, MappingStyle.Block));
            foreach (string key in map.Keys)
            {
                CommentInfo? info = map.GetComment(key);
                EmitComment(emitter, info?.BeforeComment, isInline: false);
                EmitScalar(emitter, key);
                EmitNode(emitter, map[key]);
                EmitComment(emitter, info?.InlineComment, isInline: true);
                EmitComment(emitter, info?.AfterComment, isInline: false);
            }
            emitter.Emit(new MappingEnd());
        }

        private static void EmitCommentedSeq(IEmitter emitter, CommentedSeq seq)
        {
            emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, true, SequenceStyle.Block));
            for (int i = 0; i < seq.Count; i++)
            {
                CommentInfo? info = seq.GetComment(i);
                EmitComment(emitter, info?.BeforeComment, isInline: false);
                EmitNode(emitter, seq[i]);
                EmitComment(emitter, info?.InlineComment, isInline: true);
                EmitComment(emitter, info?.AfterComment, isInline: false);
            }
            emitter.Emit(new SequenceEnd());
        }

        private static void EmitPlain(IEmitter emitter, object? value)
        {
            if (value is System.Collections.IDictionary dictionary)
            {
                emitter.Emit(new MappingStart(AnchorName.Empty, TagName.Empty, true, MappingStyle.Block));
                foreach (System.Collections.DictionaryEntry entry in dictionary)
                {
                    EmitScalar(emitter, ScalarText(entry.Key));
                    EmitNode(emitter, entry.Value);
                }
                emitter.Emit(new MappingEnd());
                return;
            }

            if (value is string)
            {
                EmitScalarValue(emitter, value);
                return;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, true, SequenceStyle.Block));
                foreach (object? item in enumerable)
                {
                    EmitNode(emitter, item);
                }
                emitter.Emit(new SequenceEnd());
                return;
            }

            EmitScalarValue(emitter, value);
        }

        private static void EmitComment(IEmitter emitter, string? text, bool isInline)
        {
            if (text == null)
            {
                return;
            }

            if (isInline)
            {
                // Inline comments are single-line by construction.
                emitter.Emit(new Comment(text, isInline: true));
                return;
            }

            foreach (string line in text.Split('\n'))
            {
                emitter.Emit(new Comment(line, isInline: false));
            }
        }

        /// <summary>Emits a string scalar as a YAML key, quoting when necessary.</summary>
        private static void EmitScalar(IEmitter emitter, string text) =>
            EmitScalarValue(emitter, text);

        /// <summary>Emits an arbitrary value as a YAML scalar, preserving its type on reload.</summary>
        private static void EmitScalarValue(IEmitter emitter, object? value)
        {
            if (value is null)
            {
                emitter.Emit(PlainScalar("null"));
                return;
            }

            if (value is bool boolean)
            {
                emitter.Emit(PlainScalar(boolean ? "true" : "false"));
                return;
            }

            if (value is string text)
            {
                if (NeedsQuoting(text))
                {
                    emitter.Emit(QuotedScalar(text));
                }
                else
                {
                    emitter.Emit(PlainScalar(text));
                }
                return;
            }

            if (value is double doubleValue)
            {
                emitter.Emit(PlainScalar(FormatDouble(doubleValue)));
                return;
            }

            if (value is float floatValue)
            {
                emitter.Emit(PlainScalar(FormatDouble(floatValue)));
                return;
            }

            // int, long, and any other primitive: invariant string form, emitted plain.
            emitter.Emit(PlainScalar(ScalarText(value)));
        }

        private static string FormatDouble(double value)
        {
            if (double.IsNaN(value))
            {
                return ".nan";
            }
            if (double.IsPositiveInfinity(value))
            {
                return ".inf";
            }
            if (double.IsNegativeInfinity(value))
            {
                return "-.inf";
            }

            string formatted = value.ToString("R", CultureInfo.InvariantCulture);
            // Ensure the value reloads as a float, not an int (e.g. 1 -> 1.0).
            if (formatted.IndexOf('.') < 0 && formatted.IndexOf('e') < 0 &&
                formatted.IndexOf('E') < 0 && formatted.IndexOf("inf", System.StringComparison.Ordinal) < 0 &&
                formatted.IndexOf("nan", System.StringComparison.Ordinal) < 0)
            {
                formatted += ".0";
            }
            return formatted;
        }

        private static string ScalarText(object? value)
        {
            if (value is null)
            {
                return "null";
            }
            if (value is string s)
            {
                return s;
            }
            return System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <summary>
        /// Determines whether a string must be quoted to roundtrip as a string rather than
        /// being reinterpreted as null/bool/number, or to remain a valid plain scalar.
        /// </summary>
        private static bool NeedsQuoting(string value)
        {
            if (value.Length == 0)
            {
                return true;
            }

            // Would a plain reload turn this into a non-string? Then it must be quoted.
            if (!(ResolvePlainScalar(value) is string))
            {
                return true;
            }

            char first = value[0];
            if (first == ' ' || value[value.Length - 1] == ' ')
            {
                return true;
            }

            // Indicator characters that are unsafe at the start of a plain scalar.
            const string leadingIndicators = "-?:,[]{}#&*!|>'\"%@`";
            if (leadingIndicators.IndexOf(first) >= 0)
            {
                return true;
            }

            foreach (char c in value)
            {
                if (c == '\n' || c == '\t' || c == '#' || c == ':')
                {
                    return true;
                }
            }

            return false;
        }

        private static Scalar PlainScalar(string value) =>
            new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.Plain, true, false);

        private static Scalar QuotedScalar(string value) =>
            new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.DoubleQuoted, false, true);

        /// <summary>
        /// Random-access cursor over a buffered list of parsing events, providing the
        /// lookahead needed to associate comments with neighbouring nodes.
        /// </summary>
        private sealed class EventReader
        {
            private readonly EventBuffer _events;
            private int _position;

            internal EventReader(EventBuffer events)
            {
                _events = events;
                _position = 0;
            }

            internal ParsingEvent? Current =>
                _position < _events.Count ? _events[_position] : null;

            internal void Advance()
            {
                if (_position < _events.Count)
                {
                    _position++;
                }
            }
        }
    }
}
