using System.Text;
using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Swaps the operands of integer-literal addition: <c>1 + 2</c> → <c>2 + 1</c>. Integer addition is
/// commutative, so every swap preserves both the value and its type.
///
/// <para>Matching runs over the masked source, so a literal like <c>"1 + 2"</c> inside a string and
/// arithmetic inside a comment are never rewritten (that would change printed output, not just
/// syntax). The boundary guards keep the match off float literals: without them <c>1.5 + 2</c>
/// matches at <c>5 + 2</c> and produces the garbage <c>1.2 + 5</c>.</para>
///
/// <para>The precedence guards are what make the swap actually commutative (#1190). Because this is
/// a textual rewrite, a matched literal is only an <em>operand</em> of the matched <c>+</c> when no
/// tighter-binding operator claims it first: in <c>2 + 3 * 4</c> the right operand of <c>+</c> is
/// <c>3 * 4</c>, so swapping the literals yields <c>3 + 2 * 4</c> and changes 14 into 11. The same
/// applies to a left literal owned by a preceding <c>*</c>, <c>/</c>, <c>//</c>, <c>%</c>,
/// <c>**</c>, <c>@</c>, unary <c>~</c>, or a <c>-</c> (which binds the literal as its own right
/// operand: <c>a - 2 + 3</c> is <c>(a - 2) + 3</c>). A neighbouring <c>+</c> needs no guard —
/// addition is associative, so <c>x + 2 + 3</c> → <c>x + 3 + 2</c> is value-preserving. Looser
/// operators (shifts, bitwise, comparisons) parenthesize the addition as a whole and are likewise
/// safe.</para>
/// </summary>
internal sealed partial class SwapCommutativeTransform : IAstTransform
{
    public string Name => "SwapCommutative";

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var matches = CommutativeAddPattern().Matches(masked.Masked);
        if (matches.Count == 0)
            return source;

        var builder = new StringBuilder(source.Length);
        var previous = 0;
        foreach (Match match in matches)
        {
            builder.Append(source, previous, match.Index - previous);
            builder.Append(match.Groups[2].Value).Append(" + ").Append(match.Groups[1].Value);
            previous = match.Index + match.Length;
        }
        builder.Append(source, previous, source.Length - previous);
        return builder.ToString();
    }

    [GeneratedRegex(@"(?<![\w.])(?<![-*/%@~][ \t]*)(\d+)[ \t]*\+[ \t]*(\d+)(?![\w.])(?![ \t]*[*/%@])")]
    private static partial Regex CommutativeAddPattern();
}
