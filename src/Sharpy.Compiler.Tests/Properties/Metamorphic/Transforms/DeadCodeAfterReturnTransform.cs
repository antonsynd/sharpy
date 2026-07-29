namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Appends an unreachable declaration after every <c>return</c> statement. Unreachable code cannot
/// change what a program prints, but it does legitimately raise the unreachable-code and
/// unused-variable warnings — the two codes this transform declares in the allowed-delta table.
///
/// <para>Each inserted name is unique per line so two returns in one function cannot collide into a
/// redeclaration, and insertion is limited to function bodies (the same text in a class body would
/// declare a field).</para>
/// </summary>
internal sealed class DeadCodeAfterReturnTransform : IAstTransform
{
    public string Name => "DeadCodeAfterReturn";

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var lines = masked.Lines.ToList();

        for (int i = masked.Lines.Length - 1; i >= 0; i--)
        {
            if (!masked.StartsStatement(i) || masked.DepthAfterLine(i) != 0)
                continue;

            var code = masked.MaskedLines[i].TrimStart().TrimEnd();
            if (!code.StartsWith("return ", StringComparison.Ordinal) && code != "return")
                continue;

            // `return match n:` opens a block of case arms — the statement continues on the
            // following lines, so there is no "after the return" to insert at yet.
            if (code.EndsWith(':'))
                continue;

            if (masked.EnclosingBlockKind(i) != "def")
                continue;

            lines.Insert(i + 1, $"{MaskedSource.Indent(masked.Lines[i])}dead_after_return_{i}: int = 0");
        }

        return string.Join('\n', lines);
    }
}
