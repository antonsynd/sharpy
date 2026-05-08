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
        return source.Replace(varName, renamed);
    }

    [GeneratedRegex(@"^\s+(\w+)\s*:\s*int\s*=", RegexOptions.Multiline)]
    private static partial Regex LocalVarPattern();
}
