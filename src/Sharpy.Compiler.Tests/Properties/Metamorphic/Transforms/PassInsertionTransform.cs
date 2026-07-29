namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Inserts a no-op <c>pass</c> statement before every <c>return</c>/<c>print(...)</c> statement.
/// Only statement-starting lines qualify, so a continuation line inside an unclosed call and text
/// inside a docstring are left alone; the original indentation string is reused verbatim so a
/// tab-indented fixture keeps its block structure.
/// </summary>
internal sealed class PassInsertionTransform : IAstTransform
{
    public string Name => "PassInsertion";

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var lines = masked.Lines.ToList();
        for (int i = masked.Lines.Length - 1; i >= 0; i--)
        {
            if (!masked.StartsStatement(i))
                continue;

            var code = masked.MaskedLines[i].TrimStart();
            var isReturn = code.StartsWith("return ", StringComparison.Ordinal) ||
                           code.TrimEnd() == "return";
            var isPrint = code.StartsWith("print(", StringComparison.Ordinal);
            if (!isReturn && !isPrint)
                continue;

            lines.Insert(i, MaskedSource.Indent(masked.Lines[i]) + "pass");
        }
        return string.Join('\n', lines);
    }
}
