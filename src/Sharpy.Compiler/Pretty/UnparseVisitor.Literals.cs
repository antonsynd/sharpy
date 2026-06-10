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
                _w.Write("{");
                Visit(part.Expression);
                if (part.FormatSpec != null)
                {
                    _w.Write(":");
                    _w.Write(part.FormatSpec);
                }
                _w.Write("}");
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
                _w.Write("{");
                Visit(part.Expression);
                if (part.FormatSpec != null)
                {
                    _w.Write(":");
                    _w.Write(part.FormatSpec);
                }
                _w.Write("}");
            }
        }
        _w.Write(delimStr);
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
        bool hasStarUnpack = node.Elements.Any(e => e is StarExpression);
        if (!hasStarUnpack)
            _w.Write("(");
        for (int i = 0; i < node.Elements.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            if (i < node.ElementNames.Length && node.ElementNames[i] != null)
            {
                _w.Write(node.ElementNames[i]!);
                _w.Write("=");
            }
            Visit(node.Elements[i]);
        }
        if (!hasStarUnpack && node.Elements.Length == 1 && (node.ElementNames.IsEmpty || node.ElementNames[0] == null))
            _w.Write(",");
        if (!hasStarUnpack)
            _w.Write(")");
    }
}
