using System;

namespace Sharpy
{
    /// <summary>
    /// The single authority on when a dumped YAML document carries the document-end marker
    /// <c>...</c> (#1348).
    ///
    /// <para>
    /// Sharpy emits YAML from two independent places — <c>yaml.safe_dump</c> builds a YamlDotNet
    /// <c>ISerializer</c>, and <c>yaml.roundtrip_dump</c> drives the raw <c>Emitter</c> itself so it
    /// can re-render stored comments. Neither can see the other's output, so the marker rule lives
    /// here rather than at either site: a rule spelled twice is a rule that will disagree with
    /// itself (#1145). The two Python libraries these surfaces are modelled on agree with each other
    /// cell for cell — see <see cref="Append"/> — so two Sharpy dump paths disagreeing would be an
    /// inconsistency this codebase invented, not one it inherited.
    /// </para>
    /// </summary>
    internal static class YamlDocumentEnd
    {
        /// <summary>
        /// Appends the document-end marker to <paramref name="emitted"/> when it is a single PLAIN
        /// scalar document; returns it unchanged otherwise.
        /// </summary>
        /// <param name="emitted">The emitted document text, exactly as written.</param>
        /// <param name="document">
        /// The value that was emitted, needed only to tell a collection from a scalar — an emitted
        /// mapping (<c>a: b</c>) and an emitted plain scalar are indistinguishable as text.
        /// </param>
        /// <remarks>
        /// <para>
        /// The rule is narrower than "scalar document", and stated broadly it manufactures a fresh
        /// divergence. Measured against PyYAML 6.0.3 and ruamel.yaml 0.18.16 (round-trip mode),
        /// which agree on every cell below except <c>'yes'</c> — where ruamel resolves YAML 1.2 and
        /// PyYAML 1.1, a difference about quoting rather than about the marker:
        /// </para>
        /// <code>
        /// 'hello'      -> hello\n...\n          'a: b'  -> 'a: b'\n
        /// 1.0          -> 1.0\n...\n            'true'  -> 'true'\n
        /// 42           -> 42\n...\n             ''      -> ''\n
        /// True         -> true\n...\n           {'a':'b'} -> a: b\n
        /// None         -> null\n...\n           [1, 2]  -> - 1\n- 2\n
        ///                                       {} / [] -> {}\n / []\n
        /// </code>
        /// <para>
        /// So the condition is the emitted scalar's STYLE, not the value's kind: a quoted scalar
        /// gets no marker even though it is a scalar. That is why this reads the emitted text rather
        /// than the value — style is not a function of the value alone.
        /// <c>YamlAmbiguousStringTypeConverter</c> quotes what the resolver would re-type (#1417),
        /// and YamlDotNet independently quotes anything whose spelling would not survive plain: a
        /// colon-space, a leading indicator character, a control character. Only the output knows
        /// which of them fired.
        /// </para>
        /// <para>
        /// This is a per-DOCUMENT rule. The stream-level shape of a multi-document dump — where
        /// PyYAML puts one marker at stream end and folds <c>---</c> inline — is deliberately not
        /// decided here; see <c>Yaml.SafeDumpAll</c> and #1471.
        /// </para>
        /// </remarks>
        internal static string Append(string emitted, object? document)
        {
            if (IsCollection(document))
            {
                return emitted;
            }

            string body = emitted.TrimEnd('\n');

            // An explicit document start used to mean the emitter could not begin the stream
            // plainly — the empty-scalar and null cases. #1467 removed both upstream of here:
            // null is now spelled as the plain scalar `null` at emission, and YamlDocumentStart
            // suppresses the marker YamlDotNet forces around an empty root scalar. So this arm no
            // longer fires for either, and is kept as a guard rather than deleted: a document
            // reaching here with an explicit start is one this rule has never reasoned about, and
            // appending an end marker to it would be a guess.
            if (body.Length == 0 || body.StartsWith("---", StringComparison.Ordinal))
            {
                return emitted;
            }

            // A quoted or block scalar is not plain. These are the characters YAML uses to open a
            // non-plain scalar or a flow collection; a plain scalar cannot begin with any of them.
            char first = body[0];
            if (first == '\'' || first == '"' || first == '|' || first == '>' ||
                first == '{' || first == '[' || first == '&' || first == '*' || first == '!')
            {
                return emitted;
            }

            // A multi-line emission is a block or a wrapped scalar, not a plain one-line document;
            // the marker case is the single-line plain scalar.
            if (body.IndexOf('\n') >= 0)
            {
                return emitted;
            }

            return body + "\n...\n";
        }

        /// <summary>
        /// Whether the document is a collection rather than a scalar. Both dump paths reach here
        /// with different representations of the same thing: <c>safe_dump</c> has already converted
        /// to YamlDotNet's shapes, while <c>roundtrip_dump</c> passes the caller's value, which may
        /// still be a Sharpy container or a comment-carrying node.
        /// </summary>
        private static bool IsCollection(object? document)
        {
            if (document is CommentedMap || document is CommentedSeq)
            {
                return true;
            }

            // A string is enumerable and is not a collection document; nothing else that reaches a
            // dump as a scalar (null, bool, the numeric types, DateTime) enumerates.
            return document is System.Collections.IEnumerable && !(document is string);
        }
    }
}
