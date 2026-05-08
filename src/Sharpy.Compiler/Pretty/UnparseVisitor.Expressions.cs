using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

internal sealed partial class UnparseVisitor
{
    #region Comprehensions

    public override void VisitListComprehension(ListComprehension node)
    {
        _w.Write("[");
        Visit(node.Element);
        WriteClauses(node.Clauses);
        _w.Write("]");
    }

    public override void VisitSetComprehension(SetComprehension node)
    {
        _w.Write("{");
        Visit(node.Element);
        WriteClauses(node.Clauses);
        _w.Write("}");
    }

    public override void VisitDictComprehension(DictComprehension node)
    {
        _w.Write("{");
        Visit(node.Key);
        _w.Write(": ");
        Visit(node.Value);
        WriteClauses(node.Clauses);
        _w.Write("}");
    }

    public override void VisitDictSpreadComprehension(DictSpreadComprehension node)
    {
        _w.Write("{**");
        Visit(node.Spread);
        WriteClauses(node.Clauses);
        _w.Write("}");
    }

    public override void VisitForClause(ForClause node)
    {
        _w.Write(" for ");
        Visit(node.Target);
        _w.Write(" in ");
        Visit(node.Iterator);
    }

    public override void VisitIfClause(IfClause node)
    {
        _w.Write(" if ");
        Visit(node.Condition);
    }

    private void WriteClauses(System.Collections.Immutable.ImmutableArray<ComprehensionClause> clauses)
    {
        foreach (var clause in clauses)
            Visit(clause);
    }

    #endregion

    #region Primaries

    public override void VisitIdentifier(Identifier node)
    {
        WriteName(node.Name, node.IsNameBacktickEscaped);
    }

    public override void VisitMemberAccess(MemberAccess node)
    {
        Visit(node.Object);
        _w.Write(node.IsNullConditional ? "?." : ".");
        _w.Write(node.Member);
    }

    public override void VisitIndexAccess(IndexAccess node)
    {
        Visit(node.Object);
        _w.Write("[");
        Visit(node.Index);
        _w.Write("]");
    }

    public override void VisitSliceAccess(SliceAccess node)
    {
        Visit(node.Object);
        _w.Write("[");
        if (node.Start != null)
            Visit(node.Start);
        _w.Write(":");
        if (node.Stop != null)
            Visit(node.Stop);
        if (node.Step != null)
        {
            _w.Write(":");
            Visit(node.Step);
        }
        _w.Write("]");
    }

    public override void VisitFunctionCall(FunctionCall node)
    {
        Visit(node.Function);
        _w.Write("(");
        WriteArgList(node.Arguments, node.KeywordArguments);
        _w.Write(")");
    }

    #endregion

    #region Operators

    public override void VisitUnaryOp(UnaryOp node)
    {
        int prec = node.Operator == UnaryOperator.Not ? PrecNot : PrecUnaryPrefix;
        switch (node.Operator)
        {
            case UnaryOperator.Plus:
                _w.Write("+");
                break;
            case UnaryOperator.Minus:
                _w.Write("-");
                break;
            case UnaryOperator.Not:
                _w.Write("not ");
                break;
            case UnaryOperator.BitwiseNot:
                _w.Write("~");
                break;
        }
        VisitUnaryOperand(node.Operand, prec);
    }

    public override void VisitBinaryOp(BinaryOp node)
    {
        int prec = GetBinaryPrecedence(node.Operator);
        bool isRightAssoc = node.Operator == BinaryOperator.Power;
        string opText = BinaryOperatorText(node.Operator);

        VisitExprInContext(node.Left, prec, isRightChild: false, parentIsRightAssoc: isRightAssoc);

        bool isWord = node.Operator is BinaryOperator.And or BinaryOperator.Or
            or BinaryOperator.In or BinaryOperator.NotIn
            or BinaryOperator.Is or BinaryOperator.IsNot;
        if (isWord)
        {
            _w.Write(" ");
            _w.Write(opText);
            _w.Write(" ");
        }
        else
        {
            _w.Write(" ");
            _w.Write(opText);
            _w.Write(" ");
        }

        VisitExprInContext(node.Right, prec, isRightChild: true, parentIsRightAssoc: isRightAssoc);
    }

    public override void VisitComparisonChain(ComparisonChain node)
    {
        for (int i = 0; i < node.Operands.Length; i++)
        {
            if (i > 0)
            {
                _w.Write(" ");
                _w.Write(ComparisonOperatorText(node.Operators[i - 1]));
                _w.Write(" ");
            }
            VisitExprInContext(node.Operands[i], PrecComparison, isRightChild: i > 0, parentIsRightAssoc: false);
        }
    }

    #endregion

    #region Advanced expressions

    public override void VisitConditionalExpression(ConditionalExpression node)
    {
        VisitExprInContext(node.ThenValue, PrecConditional, isRightChild: false, parentIsRightAssoc: false);
        _w.Write(" if ");
        Visit(node.Test);
        _w.Write(" else ");
        VisitExprInContext(node.ElseValue, PrecConditional, isRightChild: true, parentIsRightAssoc: true);
    }

    public override void VisitLambdaExpression(LambdaExpression node)
    {
        if (node.IsArrowSyntax)
        {
            WriteParameterList(node.Parameters);
            if (node.ReturnType != null)
            {
                _w.Write(" -> ");
                WriteTypeAnnotation(node.ReturnType);
                _w.Write(": ");
            }
            else
            {
                _w.Write(" -> ");
            }
            Visit(node.Body);
        }
        else
        {
            _w.Write("lambda ");
            for (int i = 0; i < node.Parameters.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                WriteParameter(node.Parameters[i]);
            }
            _w.Write(": ");
            Visit(node.Body);
        }
    }

    public override void VisitTypeCoercion(TypeCoercion node)
    {
        VisitExprInContext(node.Value, PrecCast, isRightChild: false, parentIsRightAssoc: false);
        _w.Write(" to ");
        WriteTypeAnnotation(node.TargetType);
    }

    public override void VisitTypeCheck(TypeCheck node)
    {
        VisitExprInContext(node.Value, PrecComparison, isRightChild: false, parentIsRightAssoc: false);
        _w.Write(" is ");
        WriteTypeAnnotation(node.CheckType);
    }

    public override void VisitParenthesized(Parenthesized node)
    {
        _w.Write("(");
        Visit(node.Expression);
        _w.Write(")");
    }

    public override void VisitSuperExpression(SuperExpression node)
    {
        _w.Write("super()");
    }

    public override void VisitWalrusExpression(WalrusExpression node)
    {
        _w.Write(node.Target);
        _w.Write(" := ");
        Visit(node.Value);
    }

    public override void VisitTryExpression(TryExpression node)
    {
        _w.Write("try");
        if (node.ExceptionType != null)
        {
            _w.Write("[");
            WriteTypeAnnotation(node.ExceptionType);
            _w.Write("]");
        }
        _w.Write(" ");
        VisitUnaryOperand(node.Operand, PrecTryMaybe);
    }

    public override void VisitMaybeExpression(MaybeExpression node)
    {
        _w.Write("maybe ");
        VisitUnaryOperand(node.Operand, PrecTryMaybe);
    }

    public override void VisitStarExpression(StarExpression node)
    {
        _w.Write("*");
        Visit(node.Operand);
    }

    public override void VisitSpreadElement(SpreadElement node)
    {
        _w.Write("*");
        Visit(node.Value);
    }

    public override void VisitModifiedArgument(ModifiedArgument node)
    {
        _w.Write(ParameterModifierText(node.Modifier));
        _w.Write(" ");
        if (node.InlineName != null)
        {
            _w.Write(node.InlineName);
            if (node.InlineType != null)
            {
                _w.Write(": ");
                WriteTypeAnnotation(node.InlineType);
            }
        }
        else
        {
            Visit(node.Argument);
        }
    }

    public override void VisitAwaitExpression(AwaitExpression node)
    {
        _w.Write("await ");
        VisitUnaryOperand(node.Operand, PrecAwait);
    }

    public override void VisitMatchExpression(MatchExpression node)
    {
        _w.Write("match ");
        Visit(node.Scrutinee);
        _w.Write(":");
        _w.WriteLine();
        _w.Indent();
        foreach (var arm in node.Arms)
        {
            _w.Write("case ");
            Visit(arm.Pattern);
            if (arm.Guard != null)
            {
                _w.Write(" if ");
                Visit(arm.Guard);
            }
            _w.Write(": ");
            Visit(arm.Result);
            _w.WriteLine();
        }
        _w.Dedent();
    }

    #endregion
}
