using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

internal sealed partial class AlphaRenameTransform : IAstTransform
{
    public string Name => "AlphaRename";

    public string Apply(string source)
    {
        var match = LocalVarPattern().Match(source);
        if (!match.Success)
            return source;

        var varName = match.Groups[1].Value;
        var renamed = varName + "_r";

        if (source.Contains(renamed, StringComparison.Ordinal))
            return source;

        foreach (var line in source.Split('\n'))
        {
            if (StringLiteralPattern().IsMatch(line) &&
                line.Contains(varName, StringComparison.Ordinal))
                return source;
        }

        return Regex.Replace(source, @$"\b{Regex.Escape(varName)}\b", renamed);
    }

    [GeneratedRegex(@"^\s+(\w+)\s*:\s*int\s*=", RegexOptions.Multiline)]
    private static partial Regex LocalVarPattern();

    [GeneratedRegex(@"""[^""]*""|'[^']*'")]
    private static partial Regex StringLiteralPattern();
}
