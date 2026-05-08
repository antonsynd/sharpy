namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

internal sealed class DeadCodeAfterReturnTransform : IAstTransform
{
    public string Name => "DeadCodeAfterReturn";

    public string Apply(string source)
    {
        var lines = source.Split('\n').ToList();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("return ", StringComparison.Ordinal) ||
                trimmed == "return")
            {
                var indent = lines[i].Length - trimmed.Length;
                lines.Insert(i + 1, new string(' ', indent) + "x_dead_: int = 0");
            }
        }
        return string.Join('\n', lines);
    }
}
