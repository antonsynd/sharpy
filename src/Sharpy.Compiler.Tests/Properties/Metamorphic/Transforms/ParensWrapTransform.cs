using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

internal sealed partial class ParensWrapTransform : IAstTransform
{
    public string Name => "ParensWrap";

    public string Apply(string source)
    {
        var lines = source.Split('\n');
        var result = new List<string>();
        foreach (var line in lines)
        {
            var match = PrintExprPattern().Match(line);
            if (match.Success)
            {
                var expr = match.Groups[1].Value;
                if (expr.StartsWith('(') && expr.EndsWith(')'))
                {
                    result.Add(line);
                    continue;
                }
                var prefix = line[..match.Groups[1].Index];
                var suffix = line[(match.Groups[1].Index + expr.Length)..];
                result.Add($"{prefix}({expr}){suffix}");
            }
            else
            {
                result.Add(line);
            }
        }
        return string.Join('\n', result);
    }

    [GeneratedRegex(@"print\(([^\)]+)\)(?:\s*$)")]
    private static partial Regex PrintExprPattern();
}
