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
            // `as` is the outermost combinator (PEP 634, #1663): an as-pattern that is an
            // ALTERNATIVE only got there through parentheses, and must leave with them —
            // `float() as f | list() as f` re-parses as a syntax error.
            VisitPatternOperand(node.Alternatives[i], parenthesize: node.Alternatives[i] is AsPattern);
        }
    }

    public override void VisitAndPattern(AndPattern node)
    {
        // 'and' binds tighter than '|' and 'as' (#991, #1663): either as an operand needs parens.
        VisitPatternOperand(node.Left, parenthesize: node.Left is AsPattern or OrPattern);
        _w.Write(" and ");
        VisitPatternOperand(node.Right, parenthesize: node.Right is AsPattern or OrPattern);
    }

    private void VisitPatternOperand(Pattern operand, bool parenthesize)
    {
        if (!parenthesize)
        {
            Visit(operand);
            return;
        }
        _w.Write("(");
        Visit(operand);
        _w.Write(")");
    }

    public override void VisitAsPattern(AsPattern node)
    {
        Visit(node.Inner);
        _w.Write(" as ");
        Visit(node.Name);
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
