namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Wraps the argument of a single-argument <c>print(...)</c> statement in redundant parentheses:
/// <c>print(a + b)</c> → <c>print((a + b))</c>.
///
/// <para>Applicability is deliberately narrow because parentheses are only redundant around a single
/// positional expression. <c>print(a, b)</c> must NOT become <c>print((a, b))</c> — that prints a
/// tuple instead of two space-separated values, i.e. the "semantics-preserving" transform would
/// change the program's output. Keyword arguments (<c>sep=</c>/<c>end=</c>), unpacking (<c>*xs</c>)
/// and generator expressions are excluded for the same reason.</para>
/// </summary>
internal sealed class ParensWrapTransform : IAstTransform
{
    public string Name => "ParensWrap";

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var lines = masked.Lines.ToArray();

        for (int i = 0; i < lines.Length; i++)
        {
            if (!masked.StartsStatement(i) || masked.DepthAfterLine(i) != 0)
                continue;

            var codeLine = masked.MaskedLines[i];
            if (!codeLine.TrimStart().StartsWith("print(", StringComparison.Ordinal))
                continue;

            var open = codeLine.IndexOf("print(", StringComparison.Ordinal) + "print".Length;
            var close = ParenBalance.MatchingClose(codeLine, open);
            if (close < 0)
                continue;

            // The call must be the whole statement — anything after the closing paren (other than a
            // masked-out comment) means we are looking at a sub-expression, not a print statement.
            if (codeLine[(close + 1)..].Trim().Length != 0)
                continue;

            var argStart = open + 1;
            var argLength = close - argStart;
            if (argLength <= 0)
                continue;

            var argCode = codeLine.Substring(argStart, argLength);
            if (!ParenBalance.IsSinglePositionalArgument(argCode))
                continue;

            var argText = lines[i].Substring(argStart, argLength);
            if (argText.TrimStart().StartsWith('(') && argText.TrimEnd().EndsWith(')'))
                continue;

            lines[i] = lines[i][..argStart] + "(" + argText + ")" + lines[i][(argStart + argLength)..];
        }

        return string.Join('\n', lines);
    }
}
