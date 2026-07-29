using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Consistently renames one local <c>name: int = …</c> declaration (and every reference to it) to
/// <c>name_r</c>. Alpha-renaming a local cannot change behaviour — but only if every occurrence is
/// renamed together and no occurrence means something other than that local, so the transform bails
/// out (returning the source unchanged) whenever the name also appears:
/// <list type="bullet">
///   <item>inside a string literal or comment — renaming code-only would desynchronise an f-string
///         interpolation, and renaming the literal would change printed output;</item>
///   <item>after a dot (<c>obj.name</c>) — that is a member, not this local;</item>
///   <item>as a keyword argument (<c>f(name=1)</c>) — that is a parameter name, not this local.</item>
/// </list>
/// </summary>
internal sealed partial class AlphaRenameTransform : IAstTransform
{
    public string Name => "AlphaRename";

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);

        string? varName = null;
        for (int i = 0; i < masked.Lines.Length && varName == null; i++)
        {
            if (!masked.StartsStatement(i) || masked.DepthAfterLine(i) != 0)
                continue;
            var match = LocalVarPattern().Match(masked.MaskedLines[i]);
            if (match.Success)
                varName = match.Groups[1].Value;
        }

        if (varName == null)
            return source;

        var renamed = varName + "_r";
        if (source.Contains(renamed, StringComparison.Ordinal))
            return source;

        var word = new Regex($@"\b{Regex.Escape(varName)}\b");
        foreach (Match occurrence in word.Matches(source))
        {
            // Masked positions are string-literal or comment text.
            if (masked.Masked[occurrence.Index] == ' ')
                return source;
        }

        if (new Regex($@"\.\s*{Regex.Escape(varName)}\b").IsMatch(masked.Masked))
            return source;

        foreach (Match kwarg in new Regex($@"\b{Regex.Escape(varName)}\s*=[^=]").Matches(masked.Masked))
        {
            if (masked.DepthAt(kwarg.Index) > 0)
                return source;
        }

        return word.Replace(source, renamed);
    }

    [GeneratedRegex(@"^\s+(\w+)\s*:\s*int\s*=")]
    private static partial Regex LocalVarPattern();
}
