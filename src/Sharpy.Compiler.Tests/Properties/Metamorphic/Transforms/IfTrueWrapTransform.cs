namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

internal sealed class IfTrueWrapTransform : IAstTransform
{
    public string Name => "IfTrueWrap";

    public string Apply(string source)
    {
        var lines = source.Split('\n').ToList();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("print(", StringComparison.Ordinal))
            {
                var indent = lines[i].Length - trimmed.Length;
                var spaces = new string(' ', indent);
                lines[i] = $"{spaces}if True:\n{spaces}    {trimmed}";
            }
        }
        return string.Join('\n', lines);
    }
}
