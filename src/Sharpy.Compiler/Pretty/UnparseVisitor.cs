using System.Collections.Immutable;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

internal sealed partial class UnparseVisitor : AstVisitor
{
    private readonly UnparseWriter _w;
    private readonly UnparseOptions _options;

    public UnparseVisitor(UnparseWriter writer, UnparseOptions options)
    {
        _w = writer;
        _options = options;
    }

    public void UnparseModule(Module module)
    {
        if (module.DocString != null)
        {
            _w.WriteLine($"\"\"\"{EscapeTripleQuoted(module.DocString)}\"\"\"");
        }

        var fmt = _options.Formatting;
        bool hasWrittenNonImport = false;
        bool lastWasImport = false;

        for (int i = 0; i < module.Body.Length; i++)
        {
            var stmt = module.Body[i];
            bool isImport = stmt is ImportStatement or FromImportStatement;
            bool isDef = stmt is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef or UnionDef or DelegateDef;

            if (fmt != null && i > 0)
            {
                if (isDef || (hasWrittenNonImport && !isImport && module.Body[i - 1] is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef or UnionDef or DelegateDef))
                {
                    for (int b = 0; b < fmt.BlankLinesAroundTopLevelDefs; b++)
                        _w.WriteLine();
                }
                else if (lastWasImport && !isImport)
                {
                    _w.WriteLine();
                }
            }

            VisitStatementWithTrivia(stmt);

            if (!isImport)
                hasWrittenNonImport = true;
            lastWasImport = isImport;
        }

        if (fmt is { TrailingNewline: true } && _w.Length > 0)
        {
            var text = _w.ToString();
            if (!text.EndsWith(_options.LineEnding))
                _w.WriteLine();
        }
    }

    private void VisitStatementWithTrivia(Statement stmt)
    {
        if (_options.PreserveTrivia)
            WriteLeadingTrivia(stmt);

        var posBefore = _w.Length;
        Visit(stmt);

        if (_options.PreserveTrivia && stmt.TrailingTrivia != null)
            InsertTrailingTriviaAtFirstNewline(stmt.TrailingTrivia, posBefore);
    }

    private void WriteLeadingTrivia(Node node)
    {
        if (node.LeadingTrivia == null) return;
        foreach (var trivia in node.LeadingTrivia)
        {
            _w.WriteLine(trivia.Text);
        }
    }

    private void InsertTrailingTriviaAtFirstNewline(IReadOnlyList<Trivia> trivia, int startPos)
    {
        var nlIdx = _w.IndexOf(_options.LineEnding, startPos);
        if (nlIdx < 0) return;

        var triviaText = string.Concat(trivia.Select(t => "  " + t.Text));
        _w.InsertAt(nlIdx, triviaText);
    }

    #region Precedence

    private const int PrecWalrus = 0;
    private const int PrecTryMaybe = 1;
    private const int PrecConditional = 2;
    private const int PrecNullCoalesce = 3;
    private const int PrecOr = 4;
    private const int PrecAnd = 5;
    private const int PrecNot = 6;
    private const int PrecComparison = 7;
    private const int PrecCast = 8;
    private const int PrecPipe = 9;
    private const int PrecBitwiseOr = 10;
    private const int PrecBitwiseXor = 11;
    private const int PrecBitwiseAnd = 12;
    private const int PrecShift = 13;
    private const int PrecAdditive = 14;
    private const int PrecMultiplicative = 15;
    private const int PrecUnaryPrefix = 16;
    private const int PrecAwait = 17;
    private const int PrecPower = 18;
    private const int PrecPostfix = 19;
    private const int PrecAtom = 20;

    private static int GetExpressionPrecedence(Expression expr)
    {
        return expr switch
        {
            WalrusExpression => PrecWalrus,
            TryExpression or MaybeExpression => PrecTryMaybe,
            ConditionalExpression => PrecConditional,
            BinaryOp b => GetBinaryPrecedence(b.Operator),
            ComparisonChain => PrecComparison,
            TypeCheck => PrecComparison,
            TypeCoercion => PrecCast,
            UnaryOp u => u.Operator == UnaryOperator.Not ? PrecNot : PrecUnaryPrefix,
            AwaitExpression => PrecAwait,
            StarExpression => PrecAtom,
            SpreadElement => PrecAtom,
            LambdaExpression => PrecConditional,
            _ => PrecAtom
        };
    }

    private static int GetBinaryPrecedence(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Or => PrecOr,
            BinaryOperator.And => PrecAnd,
            BinaryOperator.NullCoalesce => PrecNullCoalesce,
            BinaryOperator.PipeForward => PrecPipe,
            BinaryOperator.BitwiseOr => PrecBitwiseOr,
            BinaryOperator.BitwiseXor => PrecBitwiseXor,
            BinaryOperator.BitwiseAnd => PrecBitwiseAnd,
            BinaryOperator.LeftShift or BinaryOperator.RightShift => PrecShift,
            BinaryOperator.Add or BinaryOperator.Subtract => PrecAdditive,
            BinaryOperator.Multiply or BinaryOperator.Divide
                or BinaryOperator.FloorDivide or BinaryOperator.Modulo => PrecMultiplicative,
            BinaryOperator.Power => PrecPower,
            BinaryOperator.Equal or BinaryOperator.NotEqual
                or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
                or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual
                or BinaryOperator.In or BinaryOperator.NotIn
                or BinaryOperator.Is or BinaryOperator.IsNot => PrecComparison,
            _ => PrecAtom
        };
    }

    private void VisitExprInContext(Expression child, int parentPrec, bool isRightChild, bool parentIsRightAssoc)
    {
        if (child is Parenthesized)
        {
            Visit(child);
            return;
        }

        int childPrec = GetExpressionPrecedence(child);
        bool needsParens;
        if (isRightChild)
            needsParens = childPrec < parentPrec || (childPrec == parentPrec && !parentIsRightAssoc);
        else
            needsParens = childPrec < parentPrec || (childPrec == parentPrec && parentIsRightAssoc);

        if (needsParens)
        {
            _w.Write("(");
            Visit(child);
            _w.Write(")");
        }
        else
        {
            Visit(child);
        }
    }

    private void VisitUnaryOperand(Expression operand, int unaryPrec)
    {
        if (operand is Parenthesized)
        {
            Visit(operand);
            return;
        }

        int childPrec = GetExpressionPrecedence(operand);
        if (childPrec < unaryPrec)
        {
            _w.Write("(");
            Visit(operand);
            _w.Write(")");
        }
        else
        {
            Visit(operand);
        }
    }

    private void VisitPostfixObject(Expression obj)
    {
        if (obj is Parenthesized)
        {
            Visit(obj);
            return;
        }

        int childPrec = GetExpressionPrecedence(obj);
        bool needsParens = childPrec < PrecPostfix
            || obj is IntegerLiteral or FloatLiteral;

        if (needsParens)
        {
            _w.Write("(");
            Visit(obj);
            _w.Write(")");
        }
        else
        {
            Visit(obj);
        }
    }

    #endregion

    #region Helpers

    private void WriteBody(ImmutableArray<Statement> body)
    {
        if (body.IsEmpty)
        {
            _w.Indent();
            _w.WriteLine("pass");
            _w.Dedent();
            return;
        }

        var fmt = _options.Formatting;
        _w.Indent();
        for (int i = 0; i < body.Length; i++)
        {
            if (fmt != null && i > 0)
            {
                bool isMemberDef = body[i] is FunctionDef or PropertyDef or EventDef;
                bool prevWasMemberDef = body[i - 1] is FunctionDef or PropertyDef or EventDef;
                if (isMemberDef || prevWasMemberDef)
                {
                    for (int b = 0; b < fmt.BlankLinesBetweenClassMembers; b++)
                        _w.WriteLine();
                }
            }

            if (_options.PreserveTrivia)
            {
                VisitStatementWithTrivia(body[i]);
            }
            else
            {
                Visit(body[i]);
            }
        }
        _w.Dedent();
    }

    private void WriteDecorators(ImmutableArray<Decorator> decorators)
    {
        foreach (var dec in decorators)
        {
            _w.Write("@");
            _w.Write(dec.Name);
            if (dec.Arguments.Length > 0 || dec.KeywordArguments.Length > 0)
            {
                _w.Write("(");
                WriteArgList(dec.Arguments, dec.KeywordArguments);
                _w.Write(")");
            }
            _w.WriteLine();
        }
    }

    private void WriteArgList(ImmutableArray<Expression> args, ImmutableArray<KeywordArgument> kwargs)
    {
        bool first = true;
        foreach (var arg in args)
        {
            if (!first)
                _w.Write(", ");
            first = false;
            Visit(arg);
        }
        foreach (var kwarg in kwargs)
        {
            if (!first)
                _w.Write(", ");
            first = false;
            _w.Write(kwarg.Name);
            _w.Write("=");
            Visit(kwarg.Value);
        }
    }

    private void WriteParameterList(ImmutableArray<Parameter> parameters)
    {
        _w.Write("(");
        bool emittedSlash = false;
        bool emittedStar = false;
        bool hasVariadic = parameters.Any(p => p.IsVariadic);
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");

            if (!emittedSlash
                && i > 0
                && parameters[i - 1].Kind == ParameterKind.PositionalOnly
                && parameters[i].Kind != ParameterKind.PositionalOnly)
            {
                _w.Write("/, ");
                emittedSlash = true;
            }

            if (!emittedStar && !hasVariadic
                && parameters[i].Kind == ParameterKind.KeywordOnly)
            {
                _w.Write("*, ");
                emittedStar = true;
            }

            WriteParameter(parameters[i]);
        }
        if (!emittedSlash && parameters.Length > 0
            && parameters[parameters.Length - 1].Kind == ParameterKind.PositionalOnly)
        {
            _w.Write(", /");
        }
        _w.Write(")");
    }

    private void WriteParameter(Parameter param)
    {
        if (param.IsVariadic)
            _w.Write("*");
        WriteName(param.Name, param.IsNameBacktickEscaped);
        if (param.Type != null)
        {
            _w.Write(": ");
            if (param.Modifier != ParameterModifier.None)
            {
                _w.Write(ParameterModifierText(param.Modifier));
                _w.Write(" ");
            }
            WriteTypeAnnotation(param.Type);
        }
        if (param.IsLateBound && param.DefaultValue != null)
        {
            _w.Write(" => ");
            Visit(param.DefaultValue);
        }
        else if (param.DefaultValue != null)
        {
            _w.Write(" = ");
            Visit(param.DefaultValue);
        }
    }

    private void WriteTypeParameters(ImmutableArray<TypeParameterDef> typeParams)
    {
        if (typeParams.IsEmpty)
            return;
        _w.Write("[");
        for (int i = 0; i < typeParams.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            var tp = typeParams[i];
            if (tp.Variance == TypeParameterVariance.Covariant)
                _w.Write("out ");
            else if (tp.Variance == TypeParameterVariance.Contravariant)
                _w.Write("in ");
            _w.Write(tp.Name);
            foreach (var constraint in tp.Constraints)
            {
                WriteConstraint(constraint);
            }
            if (tp.DefaultType != null)
            {
                _w.Write(" = ");
                WriteTypeAnnotation(tp.DefaultType);
            }
        }
        _w.Write("]");
    }

    private void WriteConstraint(ConstraintClause constraint)
    {
        switch (constraint)
        {
            case TypeConstraint tc:
                _w.Write(": ");
                WriteTypeAnnotation(tc.Type);
                break;
            case ClassConstraint:
                _w.Write(": class");
                break;
            case StructConstraint:
                _w.Write(": struct");
                break;
            case NewConstraint:
                _w.Write(": new");
                break;
        }
    }

    private void WriteName(string name, bool isBacktickEscaped)
    {
        if (isBacktickEscaped)
        {
            _w.Write("`");
            _w.Write(name);
            _w.Write("`");
        }
        else
        {
            _w.Write(name);
        }
    }

    #endregion

    #region Operator text

    private static string BinaryOperatorText(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.FloorDivide => "//",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Power => "**",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.And => "and",
            BinaryOperator.Or => "or",
            BinaryOperator.BitwiseAnd => "&",
            BinaryOperator.BitwiseOr => "|",
            BinaryOperator.BitwiseXor => "^",
            BinaryOperator.LeftShift => "<<",
            BinaryOperator.RightShift => ">>",
            BinaryOperator.In => "in",
            BinaryOperator.NotIn => "not in",
            BinaryOperator.Is => "is",
            BinaryOperator.IsNot => "is not",
            BinaryOperator.NullCoalesce => "??",
            BinaryOperator.PipeForward => "|>",
            _ => op.ToString()
        };
    }

    private static string ComparisonOperatorText(ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.Equal => "==",
            ComparisonOperator.NotEqual => "!=",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.In => "in",
            ComparisonOperator.NotIn => "not in",
            ComparisonOperator.Is => "is",
            ComparisonOperator.IsNot => "is not",
            _ => op.ToString()
        };
    }

    private static string AssignmentOperatorText(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.Assign => "=",
            AssignmentOperator.PlusAssign => "+=",
            AssignmentOperator.MinusAssign => "-=",
            AssignmentOperator.StarAssign => "*=",
            AssignmentOperator.SlashAssign => "/=",
            AssignmentOperator.DoubleSlashAssign => "//=",
            AssignmentOperator.PercentAssign => "%=",
            AssignmentOperator.PowerAssign => "**=",
            AssignmentOperator.AndAssign => "&=",
            AssignmentOperator.OrAssign => "|=",
            AssignmentOperator.XorAssign => "^=",
            AssignmentOperator.LeftShiftAssign => "<<=",
            AssignmentOperator.RightShiftAssign => ">>=",
            AssignmentOperator.NullCoalesceAssign => "??=",
            _ => op.ToString()
        };
    }

    private static string ParameterModifierText(ParameterModifier mod)
    {
        return mod switch
        {
            ParameterModifier.Ref => "ref",
            ParameterModifier.Out => "out",
            ParameterModifier.In => "in",
            _ => ""
        };
    }

    private static string RelationalOperatorText(RelationalOperator op)
    {
        return op switch
        {
            RelationalOperator.GreaterThan => ">",
            RelationalOperator.GreaterThanOrEqual => ">=",
            RelationalOperator.LessThan => "<",
            RelationalOperator.LessThanOrEqual => "<=",
            _ => op.ToString()
        };
    }

    #endregion

    #region String escaping

    private static string EscapeString(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\0':
                    sb.Append("\\0");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string EscapeTripleQuoted(string value)
    {
        return value.Replace("\\", "\\\\", System.StringComparison.Ordinal)
                    .Replace("\"\"\"", "\\\"\\\"\\\"", System.StringComparison.Ordinal);
    }

    private static string EscapeFStringText(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '{':
                    sb.Append("{{");
                    break;
                case '}':
                    sb.Append("}}");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    #endregion
}
