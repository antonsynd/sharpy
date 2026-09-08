namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Wraps each single-line <c>print(...)</c> statement in an <c>if True:</c> block. The guarded
/// statement always executes, so output is unchanged and the surrounding block structure is
/// preserved (the wrapper takes the statement's own indentation and the body is indented one level
/// deeper). A print that BINDS a name — a walrus in its argument, <c>print((z := 5))</c> — is left
/// alone: Sharpy scopes the binding to the block, so wrapping it would hide <c>z</c> from the
/// statements after it (SPY0200, measured on generic_inferred_wrapper_scoped_payload_1797).
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
            if (!code.StartsWith("print(", StringComparison.Ordinal)
                || code.Contains(":=", StringComparison.Ordinal))
                continue;

            var line = masked.Lines[i];
            var indent = MaskedSource.Indent(line);
            lines[i] = $"{indent}if True:\n{indent}    {line[indent.Length..]}";
        }

        return string.Join('\n', lines);
    }
}
