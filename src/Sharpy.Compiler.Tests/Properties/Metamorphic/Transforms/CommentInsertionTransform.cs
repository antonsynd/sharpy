namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Inserts a comment line at the top of every <c>def</c>/<c>class</c> body. Applies only to real
/// block headers (a statement-starting line whose code ends with <c>:</c>) so a one-line
/// <c>def f(): return 1</c>, a signature continued on the next line, or the word "def" inside a
/// docstring is never touched.
/// </summary>
internal sealed class CommentInsertionTransform : IAstTransform
{
    public string Name => "CommentInsertion";

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var result = new List<string>(masked.Lines.Length);
        for (int i = 0; i < masked.Lines.Length; i++)
        {
            var line = masked.Lines[i];
            result.Add(line);

            if (!masked.StartsStatement(i))
                continue;

            var code = masked.MaskedLines[i].TrimEnd();
            var trimmed = code.TrimStart();
            if (!trimmed.StartsWith("def ", StringComparison.Ordinal) &&
                !trimmed.StartsWith("class ", StringComparison.Ordinal))
                continue;

            // Only a block header owns a body to comment into.
            if (!code.EndsWith(':') || masked.DepthAfterLine(i) != 0)
                continue;

            result.Add(MaskedSource.Indent(line) + "    # generated comment");
        }
        return string.Join('\n', result);
    }
}
