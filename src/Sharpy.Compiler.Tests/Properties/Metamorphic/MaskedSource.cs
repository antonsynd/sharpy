namespace Sharpy.Compiler.Tests.Properties.Metamorphic;

/// <summary>
/// A Sharpy source text paired with a same-length "mask" in which every string-literal and comment
/// character has been replaced by a space (newlines preserved, so indices and line numbers are
/// identical in both).
///
/// <para>The metamorphic transforms are source-text rewrites (<see cref="IAstTransform.Apply"/>), so
/// without a mask a regex happily rewrites <c>"1 + 2"</c> inside a string literal or corrupts a
/// docstring that contains the word <c>def</c> — turning a semantics-preserving transform into a
/// semantics-<em>changing</em> one and manufacturing false violations at corpus scale. Every
/// transform consults the mask before touching a position: rewrite only where the mask has code.</para>
/// </summary>
internal sealed class MaskedSource
{
    private readonly int[] _depthAtLineStart;
    private readonly int[] _depthAtIndex;

    private MaskedSource(string source, string masked, string[] lines, string[] maskedLines,
        int[] depthAtLineStart, int[] depthAtIndex)
    {
        Source = source;
        Masked = masked;
        Lines = lines;
        MaskedLines = maskedLines;
        _depthAtLineStart = depthAtLineStart;
        _depthAtIndex = depthAtIndex;
    }

    public string Source { get; }

    /// <summary>Same length as <see cref="Source"/>; literal/comment characters blanked to spaces.</summary>
    public string Masked { get; }

    public string[] Lines { get; }

    public string[] MaskedLines { get; }

    public static MaskedSource Of(string source)
    {
        var masked = Mask(source);
        var lines = source.Split('\n');
        var maskedLines = masked.Split('\n');

        var depthAtIndex = new int[masked.Length + 1];
        var depth = 0;
        for (int i = 0; i < masked.Length; i++)
        {
            depthAtIndex[i] = depth;
            var c = masked[i];
            if (c is '(' or '[' or '{')
                depth++;
            else if (c is ')' or ']' or '}')
                depth--;
        }
        depthAtIndex[masked.Length] = depth;

        var depths = new int[lines.Length];
        depth = 0;
        for (int i = 0; i < maskedLines.Length; i++)
        {
            depths[i] = depth;
            depth += Delta(maskedLines[i]);
        }
        return new MaskedSource(source, masked, lines, maskedLines, depths, depthAtIndex);
    }

    /// <summary>
    /// Bracket nesting depth at the START of the given line, computed over code positions only.
    /// A line whose depth is non-zero is a continuation of an unclosed call/collection above it —
    /// never a statement boundary, so no transform may insert a statement before it or wrap it.
    /// </summary>
    public int DepthAtLineStart(int lineIndex) => _depthAtLineStart[lineIndex];

    /// <summary>Bracket nesting depth after the given line — 0 means the line closed everything it opened.</summary>
    public int DepthAfterLine(int lineIndex) => _depthAtLineStart[lineIndex] + Delta(MaskedLines[lineIndex]);

    /// <summary>Bracket nesting depth at a character index of <see cref="Source"/>/<see cref="Masked"/>.</summary>
    public int DepthAt(int index) => _depthAtIndex[index];

    private static int Delta(string maskedLine)
    {
        var delta = 0;
        foreach (var c in maskedLine)
        {
            if (c is '(' or '[' or '{')
                delta++;
            else if (c is ')' or ']' or '}')
                delta--;
        }
        return delta;
    }

    /// <summary>
    /// The kind of block a line sits directly inside, found by walking outwards through enclosing
    /// headers: <c>"def"</c> (somewhere in a function body), <c>"class"</c> (directly in a class
    /// body), or <c>"module"</c>. Transforms that introduce a local variable are only safe inside a
    /// function body — the same text in a class body declares a field instead.
    /// </summary>
    public string EnclosingBlockKind(int lineIndex)
    {
        var indent = Indent(Lines[lineIndex]).Length;
        for (int i = lineIndex - 1; i >= 0; i--)
        {
            if (!StartsStatement(i))
                continue;
            var candidateIndent = Indent(Lines[i]).Length;
            if (candidateIndent >= indent)
                continue;
            var code = MaskedLines[i].TrimStart();
            if (code.StartsWith("def ", StringComparison.Ordinal) ||
                code.StartsWith("async def ", StringComparison.Ordinal))
                return "def";
            if (code.StartsWith("class ", StringComparison.Ordinal))
                return "class";
            indent = candidateIndent;
        }
        return "module";
    }

    /// <summary>
    /// True when the line begins a top-level (depth-0) statement whose first token is code — i.e. it
    /// is neither a continuation line, nor a line that starts inside a triple-quoted string, nor a
    /// pure comment/blank line. This is the precondition for inserting a statement before/after it or
    /// for rewriting its leading construct.
    /// </summary>
    public bool StartsStatement(int lineIndex)
    {
        if (DepthAtLineStart(lineIndex) != 0)
            return false;
        var line = Lines[lineIndex];
        var maskedLine = MaskedLines[lineIndex];
        var codeStart = FirstNonSpace(maskedLine);
        // No code on this line at all (blank, comment-only, or inside a docstring).
        return codeStart >= 0 && codeStart == FirstNonSpace(line);
    }

    /// <summary>The line's leading whitespace, preserved verbatim (never re-synthesized as spaces —
    /// a tab-indented fixture must keep its tabs or the reinserted line changes block structure).</summary>
    public static string Indent(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            i++;
        return line.Substring(0, i);
    }

    private static int FirstNonSpace(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != ' ' && s[i] != '\t' && s[i] != '\r')
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Blanks every string-literal body (single, double, and triple quoted, with or without an
    /// f/r/b/t prefix) and every <c>#</c> comment, preserving length and newlines. Quote delimiters
    /// are blanked too, so a masked position is code if and only if it is outside any literal.
    /// Backslash escapes are honoured while scanning for a literal's end (raw strings included: in
    /// Python-family syntax a backslash still prevents a raw string from terminating).
    /// </summary>
    private static string Mask(string source)
    {
        var buffer = source.ToCharArray();
        int i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '#')
            {
                while (i < source.Length && source[i] != '\n')
                    buffer[i++] = ' ';
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var quote = c;

                // The prefix letters of r"x" / b"x" / f"x" / t"x" / rb"x" belong to the literal
                // token, so they must be masked too — the scan reaches the quote with them already
                // copied through, and leaving them visible makes a bare prefixed-string statement
                // look like a bare identifier followed by a string. ParensWrapTransform then wraps
                // what it takes to be the whole statement and emits `(r)"x"`: two adjacent
                // expressions, SPY0103, a transform that no longer preserves semantics.
                for (int p = i - PrefixLength(source, i); p < i; p++)
                    buffer[p] = ' ';

                var isTriple = i + 2 < source.Length && source[i + 1] == quote && source[i + 2] == quote;
                var delimiterLength = isTriple ? 3 : 1;
                for (int k = 0; k < delimiterLength; k++)
                    buffer[i + k] = ' ';
                i += delimiterLength;

                while (i < source.Length)
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        if (source[i] != '\n')
                            buffer[i] = ' ';
                        i++;
                        if (source[i] != '\n')
                            buffer[i] = ' ';
                        i++;
                        continue;
                    }
                    if (source[i] == quote &&
                        (!isTriple || (i + 2 < source.Length && source[i + 1] == quote && source[i + 2] == quote)))
                    {
                        for (int k = 0; k < delimiterLength && i < source.Length; k++)
                            buffer[i++] = ' ';
                        break;
                    }
                    // A single-quoted literal never spans a newline; an unterminated one (only
                    // possible in a fixture that does not parse) stops at the line end.
                    if (source[i] == '\n' && !isTriple)
                        break;
                    if (source[i] != '\n')
                        buffer[i] = ' ';
                    i++;
                }
                continue;
            }

            i++;
        }
        return new string(buffer);
    }

    /// <summary>
    /// Length of the string-prefix run immediately before the quote at <paramref name="quoteIndex"/>
    /// (0, 1 or 2 characters). A run counts only when it is a whole word — the <c>r</c> of
    /// <c>expr"x"</c> would be part of an identifier, not a prefix — and only for the letters the
    /// language actually uses as prefixes, in either case: <c>r b f u t</c>. Any two-letter run of
    /// those letters is accepted (the scan does not validate which pairings are legal spellings —
    /// masking a byte too many on an illegal prefix is harmless, unmasking one is not).
    /// </summary>
    private static int PrefixLength(string source, int quoteIndex)
    {
        var length = 0;
        while (length < 2 && quoteIndex - length - 1 >= 0 && IsPrefixLetter(source[quoteIndex - length - 1]))
            length++;

        var before = quoteIndex - length - 1;
        if (before >= 0 && (char.IsLetterOrDigit(source[before]) || source[before] == '_'))
            return 0;

        return length;
    }

    private static bool IsPrefixLetter(char c) => char.ToLowerInvariant(c) is 'r' or 'b' or 'f' or 'u' or 't';
}
