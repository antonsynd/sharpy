using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

internal sealed partial class UnparseVisitor
{
    public override void VisitWildcardPattern(WildcardPattern node)
    {
        _w.Write("_");
    }

    public override void VisitBindingPattern(BindingPattern node)
    {
        Visit(node.Name);
        if (node.Type != null)
        {
            _w.Write(": ");
            WriteTypeAnnotation(node.Type);
        }
    }

    public override void VisitLiteralPattern(LiteralPattern node)
    {
        Visit(node.Literal);
    }

    public override void VisitTypePattern(TypePattern node)
    {
        WriteTypeAnnotation(node.Type);
        _w.Write("()");
        if (node.BindingName != null)
        {
            _w.Write(" as ");
            Visit(node.BindingName);
        }
    }

    public override void VisitUnionCasePattern(UnionCasePattern node)
    {
        if (node.UnionType != null)
        {
            WriteTypeAnnotation(node.UnionType);
            _w.Write(".");
        }
        _w.Write(node.CaseName);
        if (!node.FieldPatterns.IsEmpty)
        {
            _w.Write("(");
            for (int i = 0; i < node.FieldPatterns.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                Visit(node.FieldPatterns[i]);
            }
            _w.Write(")");
        }
    }

    public override void VisitTuplePattern(TuplePattern node)
    {
        _w.Write("(");
        for (int i = 0; i < node.Elements.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            Visit(node.Elements[i]);
        }
        if (node.Elements.Length == 1)
            _w.Write(",");
        _w.Write(")");
    }

    public override void VisitListPattern(ListPattern node)
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

    public override void VisitStarPattern(StarPattern node)
    {
        _w.Write("*");
        if (node.Capture != null)
            Visit(node.Capture);
    }

    public override void VisitOrPattern(OrPattern node)
    {
        for (int i = 0; i < node.Alternatives.Length; i++)
        {
            if (i > 0)
                _w.Write(" | ");
            Visit(node.Alternatives[i]);
        }
    }

    public override void VisitAndPattern(AndPattern node)
    {
        Visit(node.Left);
        _w.Write(" and ");
        Visit(node.Right);
    }

    public override void VisitGuardPattern(GuardPattern node)
    {
        Visit(node.Inner);
        _w.Write(" if ");
        Visit(node.Guard);
    }

    public override void VisitMemberAccessPattern(MemberAccessPattern node)
    {
        for (int i = 0; i < node.Parts.Length; i++)
        {
            if (i > 0)
                _w.Write(".");
            _w.Write(node.Parts[i]);
        }
    }

    public override void VisitRelationalPattern(RelationalPattern node)
    {
        _w.Write(RelationalOperatorText(node.Operator));
        _w.Write(" ");
        Visit(node.Value);
    }

    public override void VisitPropertyPatternField(PropertyPatternField node)
    {
        _w.Write(node.Name);
        _w.Write("=");
        Visit(node.Pattern);
    }

    public override void VisitPropertyPattern(PropertyPattern node)
    {
        if (node.Type != null)
        {
            WriteTypeAnnotation(node.Type);
        }
        _w.Write("(");
        for (int i = 0; i < node.Fields.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            Visit(node.Fields[i]);
        }
        _w.Write(")");
    }

    public override void VisitPositionalPattern(PositionalPattern node)
    {
        if (node.Type != null)
        {
            WriteTypeAnnotation(node.Type);
        }
        _w.Write("(");
        for (int i = 0; i < node.Elements.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            Visit(node.Elements[i]);
        }
        _w.Write(")");
    }
}
