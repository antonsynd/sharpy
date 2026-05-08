namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

internal sealed class CommentInsertionTransform : IAstTransform
{
    public string Name => "CommentInsertion";

    public string Apply(string source)
    {
        var lines = source.Split('\n');
        var result = new List<string>();
        foreach (var line in lines)
        {
            result.Add(line);
            if (line.TrimStart().StartsWith("def ", StringComparison.Ordinal) ||
                line.TrimStart().StartsWith("class ", StringComparison.Ordinal))
            {
                var indent = line.Length - line.TrimStart().Length;
                result.Add(new string(' ', indent) + "    # generated comment");
            }
        }
        return string.Join('\n', result);
    }
}
