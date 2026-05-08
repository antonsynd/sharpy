using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

internal sealed partial class SwapCommutativeTransform : IAstTransform
{
    public string Name => "SwapCommutative";

    public string Apply(string source)
    {
        return CommutativeAddPattern().Replace(source, match =>
        {
            var left = match.Groups[1].Value;
            var right = match.Groups[2].Value;
            return $"{right} + {left}";
        });
    }

    [GeneratedRegex(@"(\d+)\s*\+\s*(\d+)")]
    private static partial Regex CommutativeAddPattern();
}
