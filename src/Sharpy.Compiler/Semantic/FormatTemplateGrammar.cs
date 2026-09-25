extern alias SharpyRT;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// One replacement field of a literal <c>str.format</c> template that the checker can pair with an
/// operand: <see cref="ArgumentIndex"/> is the positional argument the field reads (null when the
/// field is not a bare positional reference — a keyword name, or a <c>.attr</c>/<c>[key]</c> access
/// whose value is not the operand itself); <see cref="KeywordName"/> is the name of a bare keyword
/// field (<c>{name}</c>, no access path), null otherwise; <see cref="Spec"/> is the static spec text
/// (null when the spec holds nested replacement fields, so its text is only known at runtime).
/// </summary>
internal readonly record struct FormatTemplateHole(int? ArgumentIndex, char? Conversion, string? Spec, string? KeywordName = null);

/// <summary>
/// Splits a LITERAL <c>str.format</c> template into its replacement fields at compile time, by name,
/// mirroring <c>Sharpy.StringExtensions.Vformat</c> / <c>ReadReplacementField</c> /
/// <c>ResolveFieldValue</c> (#1956). Unlike the spec rules (which <see cref="FormatSpecGrammar"/>
/// takes from Core's one validator, #1984), the splitter is a mirror by design: parity is pinned by
/// tests, not by a shared splitter. Only the facts the static
/// spec check needs are produced (which operand each field reads, its conversion, its static spec);
/// every template error Core raises (an unmatched brace, a bad conversion, mixed auto/manual
/// numbering) makes <see cref="Split"/> return null, so the template is left wholly to Core's
/// runtime refusal rather than half-checked.
/// </summary>
internal static class FormatTemplateGrammar
{
    // The characters that open a field's access path (Core's ResolveFieldValue: first '.' or '[').
    private static readonly char[] AccessPathStarts = { '.', '[' };

    /// <summary>
    /// The replacement fields of <paramref name="template"/> in order, or null when Core would raise
    /// a template error before or instead of formatting (see the class summary). Auto-numbered
    /// fields (<c>{}</c>) count from 0 in the order Core claims them — an outer field before the
    /// nested fields of its own spec — so a field after a nested-spec field still pairs with the
    /// right operand.
    /// </summary>
    public static IReadOnlyList<FormatTemplateHole>? Split(string template)
    {
        var holes = new List<FormatTemplateHole>();
        var numbering = new Numbering();
        return SplitInto(template, holes, numbering, nested: false) ? holes : null;
    }

    private sealed class Numbering
    {
        public int AutoIndex;
        public bool UsedAuto;
        public bool UsedManual;
    }

    private static bool SplitInto(string template, List<FormatTemplateHole> holes, Numbering numbering, bool nested)
    {
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    i += 2;
                    continue;
                }

                if (ReadReplacementField(template, ref i) is not { } field)
                    return false;

                string fieldExpr;
                string? specText;
                int colonPos = field.IndexOf(':', StringComparison.Ordinal);
                if (colonPos >= 0)
                {
                    fieldExpr = field.Substring(0, colonPos);
                    specText = field.Substring(colonPos + 1);
                }
                else
                {
                    fieldExpr = field;
                    specText = null;
                }

                char? conversion = null;
                int bangPos = fieldExpr.IndexOf('!', StringComparison.Ordinal);
                if (bangPos >= 0)
                {
                    string convStr = fieldExpr.Substring(bangPos + 1);
                    if (convStr.Length != 1 || (convStr[0] != 'r' && convStr[0] != 's' && convStr[0] != 'a'))
                        return false;
                    conversion = convStr[0];
                    fieldExpr = fieldExpr.Substring(0, bangPos);
                }

                // The outer field claims its index before the nested fields of its spec (Core's order).
                if (!ResolveFieldIndex(fieldExpr, numbering, out var argumentIndex, out var keywordName))
                    return false;

                string? spec;
                if (specText == null)
                {
                    spec = "";
                }
                else if (specText.IndexOf('{', StringComparison.Ordinal) < 0)
                {
                    spec = specText;
                }
                else
                {
                    // A nested spec is only known at runtime; its fields still claim indices. Core
                    // allows one nesting level from a template; a deeper one is its runtime error.
                    if (nested || !SplitInto(specText, new List<FormatTemplateHole>(), numbering, nested: true))
                        return false;
                    spec = null;
                }

                if (!nested)
                    holes.Add(new FormatTemplateHole(argumentIndex, conversion, spec, keywordName));
            }
            else if (c == '}')
            {
                if (i + 1 < template.Length && template[i + 1] == '}')
                {
                    i += 2;
                    continue;
                }
                return false;
            }
            else
            {
                i++;
            }
        }
        return true;
    }

    /// <summary>Mirrors <c>ReadReplacementField</c>: the text between the braces, or null when unmatched.</summary>
    private static string? ReadReplacementField(string template, ref int i)
    {
        int start = i + 1;
        int depth = 1;
        int j = start;
        while (j < template.Length)
        {
            char ch = template[j];
            if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    break;
            }
            j++;
        }

        if (depth != 0)
            return null;

        i = j + 1;
        return template.Substring(start, j - start);
    }

    /// <summary>
    /// Mirrors <c>ResolveFieldValue</c>'s numbering: an empty base field is auto-numbered, a decimal
    /// one is manual, and switching between the two is Core's runtime error (false). A keyword name
    /// (Core's runtime refusal) or an access path yields a null index — the field reads something
    /// other than a positional operand itself; a bare keyword name (no access path) is returned in
    /// <paramref name="keywordName"/>.
    /// </summary>
    private static bool ResolveFieldIndex(
        string fieldExpr, Numbering numbering, out int? argumentIndex, out string? keywordName)
    {
        argumentIndex = null;
        keywordName = null;
        int accessStart = fieldExpr.IndexOfAny(AccessPathStarts);
        string baseField = accessStart >= 0 ? fieldExpr.Substring(0, accessStart) : fieldExpr;

        int index;
        int digitsEnd = 0;
        if (baseField.Length == 0)
        {
            if (numbering.UsedManual)
                return false;
            numbering.UsedAuto = true;
            index = numbering.AutoIndex++;
        }
        else if (!SharpyRT::Sharpy.PyFormatSpec.TryReadDecimal(baseField, ref digitsEnd, out int parsed))
        {
            // Too many digits: Core's runtime ValueError, read by the same Nd reader (#2017).
            return false;
        }
        else if (digitsEnd == baseField.Length)
        {
            if (numbering.UsedAuto)
                return false;
            numbering.UsedManual = true;
            index = parsed;
        }
        else
        {
            if (accessStart < 0)
                keywordName = baseField;
            return true;
        }

        if (accessStart < 0)
            argumentIndex = index;
        return true;
    }
}
