using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

internal sealed partial class UnparseVisitor
{
    public override void VisitIntegerLiteral(IntegerLiteral node)
    {
        _w.Write(node.Value);
        if (node.Suffix != null)
            _w.Write(node.Suffix);
    }

    public override void VisitFloatLiteral(FloatLiteral node)
    {
        _w.Write(node.Value);
        if (node.Suffix != null)
            _w.Write(node.Suffix);
    }

    public override void VisitStringLiteral(StringLiteral node)
    {
        bool needsTripleQuote = node.Value.Contains('\n', System.StringComparison.Ordinal) || node.Value.Contains("\"\"\"", System.StringComparison.Ordinal);
        if (node.IsRaw)
        {
            if (needsTripleQuote || node.Value.Contains('"', System.StringComparison.Ordinal))
            {
                _w.Write("r\"\"\"");
                _w.Write(node.Value);
                _w.Write("\"\"\"");
            }
            else
            {
                _w.Write("r\"");
                _w.Write(node.Value);
                _w.Write("\"");
            }
        }
        else if (needsTripleQuote)
        {
            _w.Write("\"\"\"");
            _w.Write(EscapeTripleQuoted(node.Value));
            _w.Write("\"\"\"");
        }
        else
        {
            _w.Write("\"");
            _w.Write(EscapeString(node.Value));
            _w.Write("\"");
        }
    }

    public override void VisitBytesLiteral(BytesLiteralExpression node)
    {
        _w.Write("b\"");
        _w.Write(EscapeString(node.Value));
        _w.Write("\"");
    }

    public override void VisitFStringLiteral(FStringLiteral node)
    {
        char delim = ChooseFStringDelimiter(node.Parts);
        string delimStr = delim.ToString();
        _w.Write("f" + delimStr);
        foreach (var part in node.Parts)
        {
            if (part.Text != null)
            {
                _w.Write(EscapeFStringText(part.Text, delim));
            }
            else if (part.Expression != null)
            {
                WriteFStringReplacementField(part);
            }
        }
        _w.Write(delimStr);
    }

    public override void VisitTStringLiteral(TStringLiteral node)
    {
        char delim = ChooseFStringDelimiter(node.Parts);
        string delimStr = delim.ToString();
        _w.Write("t" + delimStr);
        foreach (var part in node.Parts)
        {
            if (part.Text != null)
            {
                _w.Write(EscapeFStringText(part.Text, delim));
            }
            else if (part.Expression != null)
            {
                WriteFStringReplacementField(part);
            }
        }
        _w.Write(delimStr);
    }

    /// <summary>
    /// Writes a single replacement field: {expr[=][!conv][:spec]}. A hole parsed from source is
    /// written from its raw text verbatim (#2024) — re-spelling it from the AST changes a t-string's
    /// <c>Interpolation.expression</c> and collapses <c>{ {x, 2} }</c> into the <c>{{</c> escape. An
    /// AST built without source falls back to the captured '=' text, then to re-visiting the
    /// expression.
    /// </summary>
    private void WriteFStringReplacementField(FStringPart part)
    {
        _w.Write("{");
        if (part.RawText != null)
        {
            _w.Write(part.RawText);
        }
        else if (part.IsSelfDocumenting && part.SourceText != null)
        {
            _w.Write(part.SourceText);
        }
        else
        {
            // python's ast.unparse rule: a hole whose expression starts with '{' is padded so the
            // opening pair is not read as the '{{' escape (f"{ {x, 2} }", not f"{{x, 2}}").
            var start = _w.Length;
            Visit(part.Expression!);
            if (_w.Length > start && _w.CharAt(start) == '{')
                _w.InsertAt(start, " ");
        }
        if (part.Conversion != null)
        {
            _w.Write("!");
            _w.Write(part.Conversion.Value.ToString());
        }
        if (part.Spec is { } spec)
        {
            _w.Write(":");
            WriteFStringSpec(spec);
        }
        _w.Write("}");
    }

    /// <summary>
    /// Writes a replacement field's format spec: literal text verbatim (braces re-escaped) and each
    /// nested replacement field via <see cref="WriteFStringReplacementField"/> (recursively).
    /// </summary>
    private void WriteFStringSpec(ImmutableArray<FStringPart> spec)
    {
        foreach (var part in spec)
        {
            if (part.Expression != null)
            {
                WriteFStringReplacementField(part);
            }
            else if (part.Text != null)
            {
                _w.Write(part.Text
                    .Replace("{", "{{", StringComparison.Ordinal)
                    .Replace("}", "}}", StringComparison.Ordinal));
            }
        }
    }

    public override void VisitBooleanLiteral(BooleanLiteral node)
    {
        _w.Write(node.Value ? "True" : "False");
    }

    public override void VisitNoneLiteral(NoneLiteral node)
    {
        _w.Write("None");
    }

    public override void VisitEllipsisLiteral(EllipsisLiteral node)
    {
        _w.Write("...");
    }

    public override void VisitListLiteral(ListLiteral node)
    {
        _w.Write("[");
        for (int i = 0; i < node.Elements.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            Visit(node.Elements[i]);
        }
        _w.Write("]");
    }

    public override void VisitDictLiteral(DictLiteral node)
    {
        _w.Write("{");
        for (int i = 0; i < node.Entries.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            var entry = node.Entries[i];
            if (entry.Key != null)
            {
                Visit(entry.Key);
                _w.Write(": ");
                Visit(entry.Value);
            }
            else
            {
                _w.Write("**");
                Visit(entry.Value);
            }
        }
        _w.Write("}");
    }

    public override void VisitSetLiteral(SetLiteral node)
    {
        _w.Write("{");
        for (int i = 0; i < node.Elements.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            Visit(node.Elements[i]);
        }
        _w.Write("}");
    }

    public override void VisitTupleLiteral(TupleLiteral node)
    {
        // A multi-element tuple that unpacks is written bare — `first, *rest = items` is the only
        // spelling the parser accepts for that target. A SOLE starred element cannot be written
        // bare (`*a` alone is not a target), so it keeps its delimiters: `(*a,)` (paren + comma) or
        // `[*a]` (list display). In operand position the precedence table ranks a bare tuple below
        // every operator so the operand helpers parenthesize it instead (#1172).
        bool hasStarUnpack = RendersAsBareTuple(node);
        string open = node.IsListDisplay ? "[" : "(";
        string close = node.IsListDisplay ? "]" : ")";
        if (!hasStarUnpack)
            _w.Write(open);
        for (int i = 0; i < node.Elements.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            if (i < node.ElementNames.Length && node.ElementNames[i] != null)
            {
                _w.Write(node.ElementNames[i]!);
                _w.Write("=");
            }
            // A nested target that itself renders bare (a multi-element star tuple `c, *d`) must be
            // parenthesized as an element, or `(b, (c, *d))` would flatten to `(b, c, *d)` and lose a
            // level of nesting on reparse (#1846).
            if (node.Elements[i] is TupleLiteral nestedBare && RendersAsBareTuple(nestedBare))
            {
                _w.Write("(");
                Visit(nestedBare);
                _w.Write(")");
            }
            else
            {
                Visit(node.Elements[i]);
            }
        }
        // A single non-star, non-list-display tuple always needs its comma (`(x,)` vs `(x)`); every
        // tuple round-trips a source trailing comma (`(*a,)`, `(1, 2,)`). A list display never does
        // (`[x]`, `[*a]`), and a named tuple is written without one.
        bool unnamed = node.ElementNames.IsEmpty || node.ElementNames.All(n => n == null);
        // A sole StarExpression is the canonical unpacking form and only round-trips with its comma
        // (`(*b,)` — bare `(*b)` reparses to a SpreadElement, not a StarExpression); a sole
        // SpreadElement is the bare `(*a)` and keeps no comma unless the source carried one.
        bool soleAutoComma = node.Elements.Length == 1
            && !node.IsListDisplay
            && node.Elements[0] is not SpreadElement;
        if (!hasStarUnpack && unnamed && (soleAutoComma || node.HasTrailingComma))
            _w.Write(",");
        if (!hasStarUnpack)
            _w.Write(close);
    }
}
