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

    [GeneratedRegex(@"(?<![\w.])(\d+)[ \t]*\+[ \t]*(\d+)(?![\w.])")]
    private static partial Regex CommutativeAddPattern();
}
