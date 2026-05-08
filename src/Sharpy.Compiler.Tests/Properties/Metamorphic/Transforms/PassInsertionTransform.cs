namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

internal sealed class PassInsertionTransform : IAstTransform
{
    public string Name => "PassInsertion";

    public string Apply(string source)
    {
        var lines = source.Split('\n').ToList();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("return ", StringComparison.Ordinal) ||
                trimmed.StartsWith("print(", StringComparison.Ordinal))
            {
                var indent = lines[i].Length - trimmed.Length;
                lines.Insert(i, new string(' ', indent) + "pass");
            }
        }
        return string.Join('\n', lines);
    }
}
