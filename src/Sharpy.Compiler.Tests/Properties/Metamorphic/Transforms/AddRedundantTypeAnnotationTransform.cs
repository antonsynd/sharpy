using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Spells out the type Sharpy already infers: <c>count = 42</c> → <c>count: int = 42</c>.
///
/// <para>The annotation is only redundant when it names the type inference would have chosen AND the
/// line is the variable's sole binding. A second <c>x = …</c> in the same scope is a reassignment,
/// and annotating it declares a NEW variable — so the transform requires exactly one unannotated
/// binding of the name and no other binding form (loop target, <c>as</c> alias, parameter, keyword
/// argument, attribute, or a module-level property of the same name — a bare <c>name = v</c> there
/// runs the setter (#1459) rather than declaring a local, so annotating it would redeclare it as a
/// shadowing local and change the program's diagnostics). Literals too large for <c>int</c> are
/// skipped because inference gives them <c>long</c>, and only function bodies qualify (the same line
/// in a class body declares a field).</para>
/// </summary>
internal sealed partial class AddRedundantTypeAnnotationTransform : IAstTransform
{
    public string Name => "AddRedundantTypeAnnotation";

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var lines = masked.Lines.ToArray();
        var changed = false;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!masked.StartsStatement(i) || masked.DepthAfterLine(i) != 0)
                continue;

            var match = UnannotatedIntAssign().Match(masked.MaskedLines[i]);
            if (!match.Success)
                continue;

            var name = match.Groups[2].Value;
            if (!int.TryParse(match.Groups[3].Value, out _))
                continue;
            if (masked.EnclosingBlockKind(i) != "def")
                continue;
            if (!IsSoleBinding(masked, name))
                continue;

            var nameEnd = match.Groups[2].Index + name.Length;
            lines[i] = string.Concat(lines[i].AsSpan(0, nameEnd), ": int", lines[i].AsSpan(nameEnd));
            changed = true;
        }

        return changed ? string.Join('\n', lines) : source;
    }

    /// <summary>
    /// True when <paramref name="name"/> is bound exactly once in the whole file, by a plain
    /// unannotated assignment, and never appears in a position where the rewritten line would
    /// redeclare or shadow something.
    /// </summary>
    private static bool IsSoleBinding(MaskedSource masked, string name)
    {
        var escaped = Regex.Escape(name);

        var bindings = 0;
        foreach (var maskedLine in masked.MaskedLines)
        {
            if (new Regex($@"^[ \t]*{escaped}[ \t]*=[^=]").IsMatch(maskedLine))
                bindings++;
        }
        if (bindings != 1)
            return false;

        foreach (var pattern in new[]
        {
            $@"\b{escaped}\s*:",              // already annotated somewhere (declaration or lambda param)
            $@"\bfor\b[^\n]*\b{escaped}\b",   // loop / comprehension target
            $@"\bas\s+{escaped}\b",           // with/except alias
            $@"\bdef\b[^\n]*\b{escaped}\b",   // function name or parameter
            $@"\.\s*{escaped}\b",             // attribute of the same name
            $@"\b(global|nonlocal)\b[^\n]*\b{escaped}\b",
            // module-level property of this name: a bare `name = v` runs the setter (#1459), so
            // annotating it would redeclare it as a shadowing local (a different program). Anchored
            // to column 0 — a class-level property is indented and stored via `self.name`, never a
            // bare name, so it cannot reach this transform's target line.
            $@"(?m)^property\b[ \t]+(?:(?:get|set|init)[ \t]+)?{escaped}\b",
        })
        {
            if (new Regex(pattern).IsMatch(masked.Masked))
                return false;
        }

        foreach (Match kwarg in new Regex($@"\b{escaped}\s*=[^=]").Matches(masked.Masked))
        {
            if (masked.DepthAt(kwarg.Index) > 0)
                return false;
        }

        return true;
    }

    [GeneratedRegex(@"^([ \t]+)(\w+)[ \t]*=[ \t]*(\d+)[ \t]*$")]
    private static partial Regex UnannotatedIntAssign();
}
