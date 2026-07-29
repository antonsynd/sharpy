namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Wraps each single-line <c>print(...)</c> statement in an <c>if True:</c> block. The guarded
/// statement always executes, so output is unchanged and the surrounding block structure is
/// preserved (the wrapper takes the statement's own indentation and the body is indented one level
/// deeper).
/// </summary>
internal sealed class IfTrueWrapTransform : IAstTransform
{
    public string Name => "IfTrueWrap";

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var lines = masked.Lines.ToList();

        for (int i = masked.Lines.Length - 1; i >= 0; i--)
        {
            if (!masked.StartsStatement(i) || masked.DepthAfterLine(i) != 0)
                continue;

            var code = masked.MaskedLines[i].TrimStart();
            if (!code.StartsWith("print(", StringComparison.Ordinal))
                continue;

            var line = masked.Lines[i];
            var indent = MaskedSource.Indent(line);
            lines[i] = $"{indent}if True:\n{indent}    {line[indent.Length..]}";
        }

        return string.Join('\n', lines);
    }
}
