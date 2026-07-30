namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Adds redundant parentheses in the two positions where they are unambiguously a no-op:
/// <list type="bullet">
/// <item>around the argument of a single-argument <c>print(...)</c> statement:
/// <c>print(a + b)</c> → <c>print((a + b))</c>;</item>
/// <item>around a whole expression statement: <c>print(1)</c> → <c>(print(1))</c>.</item>
/// </list>
///
/// <para>Argument applicability is deliberately narrow because parentheses are only redundant around a
/// single positional expression. <c>print(a, b)</c> must NOT become <c>print((a, b))</c> — that prints
/// a tuple instead of two space-separated values, i.e. the "semantics-preserving" transform would
/// change the program's output. Keyword arguments (<c>sep=</c>/<c>end=</c>), unpacking (<c>*xs</c>)
/// and generator expressions are excluded for the same reason.</para>
///
/// <para>The statement position pins the #1197 defect class. The emitter's expression-statement seam
/// pattern-matches the statement's expression to choose between an emitted statement and a
/// <c>_ = expr;</c> discard; before #1197 a <c>Parenthesized</c> wrapper missed every arm, so a
/// parenthesized void call emitted <c>_ = (print(1));</c> — uncompilable C# (CS8209) from an accepted
/// Sharpy program. Wrapping only whole single-line expression statements keeps the rewrite a pure
/// grouping: assignments, annotations, bare tuples, block headers and every keyword-led statement
/// form are excluded, since parenthesizing those is not a no-op (or not even valid syntax).</para>
/// </summary>
internal sealed class ParensWrapTransform : IAstTransform
{
    public string Name => "ParensWrap";

    /// <summary>
    /// Leading words that make a line something other than a bare expression statement. Parenthesizing
    /// after such a keyword either changes the construct (<c>return x</c> → <c>(return x)</c> does not
    /// parse) or targets a marker rather than a value.
    /// </summary>
    private static readonly HashSet<string> NonExpressionStatementHeads = new(StringComparer.Ordinal)
    {
        "def", "class", "interface", "struct", "enum", "union", "delegate", "event", "property",
        "if", "elif", "else", "for", "while", "with", "try", "except", "finally", "match", "case",
        "return", "yield", "raise", "assert", "del", "pass", "break", "continue", "import", "from",
        "global", "nonlocal", "lambda", "async", "await", "defer", "type",
    };

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var lines = masked.Lines.ToArray();

        for (int i = 0; i < lines.Length; i++)
        {
            if (!masked.StartsStatement(i) || masked.DepthAfterLine(i) != 0)
                continue;

            var codeLine = masked.MaskedLines[i];
            var insertions = new List<(int Index, string Text)>();

            AddPrintArgumentWrap(codeLine, lines[i], insertions);
            AddStatementWrap(codeLine, insertions);

            if (insertions.Count == 0)
                continue;

            var line = lines[i];
            var builder = new System.Text.StringBuilder(line.Length + insertions.Count);
            var previous = 0;
            foreach (var (index, text) in insertions.OrderBy(x => x.Index))
            {
                builder.Append(line, previous, index - previous).Append(text);
                previous = index;
            }
            builder.Append(line, previous, line.Length - previous);
            lines[i] = builder.ToString();
        }

        return string.Join('\n', lines);
    }

    private static void AddPrintArgumentWrap(string codeLine, string line, List<(int, string)> insertions)
    {
        if (!codeLine.TrimStart().StartsWith("print(", StringComparison.Ordinal))
            return;

        var open = codeLine.IndexOf("print(", StringComparison.Ordinal) + "print".Length;
        var close = ParenBalance.MatchingClose(codeLine, open);
        if (close < 0)
            return;

        // The call must be the whole statement — anything after the closing paren (other than a
        // masked-out comment) means we are looking at a sub-expression, not a print statement.
        if (codeLine[(close + 1)..].Trim().Length != 0)
            return;

        var argStart = open + 1;
        var argLength = close - argStart;
        if (argLength <= 0)
            return;

        var argCode = codeLine.Substring(argStart, argLength);
        if (!ParenBalance.IsSinglePositionalArgument(argCode))
            return;

        var argText = line.Substring(argStart, argLength);
        if (argText.TrimStart().StartsWith('(') && argText.TrimEnd().EndsWith(')'))
            return;

        insertions.Add((argStart, "("));
        insertions.Add((argStart + argLength, ")"));
    }

    private static void AddStatementWrap(string codeLine, List<(int, string)> insertions)
    {
        var codeStart = FirstCodeIndex(codeLine);
        if (codeStart < 0)
            return;

        var codeEnd = LastCodeIndex(codeLine) + 1;
        var statement = codeLine[codeStart..codeEnd];
        if (!IsWrappableExpressionStatement(statement))
            return;

        insertions.Add((codeStart, "("));
        insertions.Add((codeEnd, ")"));
    }

    /// <summary>
    /// True when <paramref name="statement"/> (the masked code of a whole single-line statement) is a
    /// bare expression whose value is discarded, so surrounding it with parentheses is pure grouping.
    /// </summary>
    private static bool IsWrappableExpressionStatement(string statement)
    {
        if (statement.Length == 0 || statement[0] == '@' || statement.EndsWith('\\'))
            return false;

        // `...` is the body-less/abstract-member marker, matched at the member seam rather than the
        // statement seam — `(...)` is a different question from `(f())` and is not this class.
        if (statement == "...")
            return false;

        if (NonExpressionStatementHeads.Contains(FirstWord(statement)))
            return false;

        // Any top-level `=` (assignment or augmented assignment), `:` (annotation, block header,
        // lambda), `,` (bare tuple) or `;` means the line is not a bare expression. Comparisons
        // (`a == b`) as statements are rare enough that excluding them costs no coverage.
        var depth = 0;
        foreach (var c in statement)
        {
            if (c is '(' or '[' or '{')
                depth++;
            else if (c is ')' or ']' or '}')
                depth--;
            else if (depth == 0 && c is '=' or ':' or ',' or ';')
                return false;
        }

        // An already-parenthesized statement would only gain nesting depth, not coverage.
        return !(statement[0] == '(' && ParenBalance.MatchingClose(statement, 0) == statement.Length - 1);
    }

    private static string FirstWord(string statement)
    {
        var end = 0;
        while (end < statement.Length && ParenBalance.IsIdentifierChar(statement[end]))
            end++;
        return statement[..end];
    }

    private static int FirstCodeIndex(string maskedLine)
    {
        for (int i = 0; i < maskedLine.Length; i++)
        {
            if (!char.IsWhiteSpace(maskedLine[i]))
                return i;
        }
        return -1;
    }

    private static int LastCodeIndex(string maskedLine)
    {
        for (int i = maskedLine.Length - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(maskedLine[i]))
                return i;
        }
        return -1;
    }
}
