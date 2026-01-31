#pragma warning disable CS0618 // ParserError is obsolete
using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parser partial class: Expression parsing (operators, comparisons, pipelines)
/// </summary>
public partial class Parser
{
    private Expression ParseExpression() => ParseWalrusExpression();

    private Expression ParseWalrusExpression()
    {
        // Walrus operator: name := value
        // Check if we have identifier := pattern
        if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.ColonAssign)
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;
            var startToken = Current;
            var name = Current.Value;
            Advance();  // Skip identifier
            Advance();  // Skip :=
            var value = ParseWalrusExpression();  // Right-associative

            return new WalrusExpression
            {
                Target = name,
                Value = value,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = value.LineEnd,
                ColumnEnd = value.ColumnEnd,
                Span = CombineSpans(GetSpanFromToken(startToken), value.Span)
            };
        }

        return ParseTryMaybeExpression();
    }

    private Expression ParseTryMaybeExpression()
    {
        // try expression: try expr or try[ExceptionType] expr
        // Wraps expression in Result[T, E]
        if (Current.Type == TokenType.Try)
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;
            var startToken = Current;
            Advance();  // Skip 'try'

            // Check for optional exception type: try[ValueError] expr
            TypeAnnotation? exceptionType = null;
            if (Current.Type == TokenType.LeftBracket)
            {
                Advance();  // Skip '['
                exceptionType = ParseTypeAnnotation();
                Expect(TokenType.RightBracket);
            }

            // Parse the operand - try captures everything up to conditional expressions
            var operand = ParseConditionalOperand();

            return new TryExpression
            {
                Operand = operand,
                ExceptionType = exceptionType,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd,
                Span = CombineSpans(GetSpanFromToken(startToken), operand.Span)
            };
        }

        // maybe expression: maybe expr
        // Wraps nullable expression in Optional[T]
        if (Current.Type == TokenType.Maybe)
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;
            var startToken = Current;
            Advance();  // Skip 'maybe'

            // Parse the operand - maybe captures everything up to conditional expressions
            var operand = ParseConditionalOperand();

            return new MaybeExpression
            {
                Operand = operand,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd,
                Span = CombineSpans(GetSpanFromToken(startToken), operand.Span)
            };
        }

        return ParseConditionalExpression();
    }

    /// <summary>
    /// Parse the operand for try/maybe expressions.
    /// This captures everything that try/maybe should bind to, which is
    /// everything up to but not including conditional expressions.
    /// </summary>
    private Expression ParseConditionalOperand()
    {
        // try/maybe binds to everything except conditional expressions
        // So we parse NullCoalesce (which includes all higher-precedence operators)
        return ParseNullCoalesce();
    }

    private Expression ParseConditionalExpression()
    {
        // expr if test else expr
        var expr = ParseNullCoalesce();

        if (Current.Type == TokenType.If)
        {
            Advance();
            var test = ParseNullCoalesce();
            Expect(TokenType.Else);
            var elseValue = ParseConditionalExpression();

            return new ConditionalExpression
            {
                Test = test,
                ThenValue = expr,
                ElseValue = elseValue,
                LineStart = expr.LineStart,
                ColumnStart = expr.ColumnStart,
                LineEnd = elseValue.LineEnd,
                ColumnEnd = elseValue.ColumnEnd,
                Span = CombineSpans(expr.Span, elseValue.Span)
            };
        }

        return expr;
    }

    private Expression ParseNullCoalesce()
    {
        var left = ParseLogicalOr();

        while (Current.Type == TokenType.NullCoalesce)
        {
            Advance();
            var right = ParseLogicalOr();

            left = new BinaryOp
            {
                Operator = BinaryOperator.NullCoalesce,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseLogicalOr()
    {
        var left = ParseLogicalAnd();

        while (Current.Type == TokenType.Or)
        {
            Advance();
            var right = ParseLogicalAnd();

            left = new BinaryOp
            {
                Operator = BinaryOperator.Or,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseLogicalAnd()
    {
        var left = ParseLogicalNot();

        while (Current.Type == TokenType.And)
        {
            Advance();
            var right = ParseLogicalNot();

            left = new BinaryOp
            {
                Operator = BinaryOperator.And,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseLogicalNot()
    {
        if (Current.Type == TokenType.Not)
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;
            var notToken = Current;
            Advance();
            var operand = ParseLogicalNot();

            return new UnaryOp
            {
                Operator = UnaryOperator.Not,
                Operand = operand,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd,
                Span = CombineSpans(GetSpanFromToken(notToken), operand.Span)
            };
        }

        return ParseComparison();
    }

    private Expression ParseComparison()
    {
        var left = ParsePipe();

        // Special case: "is" followed by a type name should be parsed as TypeCheck
        if (Current.Type == TokenType.Is && Peek(1).Type == TokenType.Identifier)
        {
            var nextTokenValue = Peek(1).Value;
            // Check if it's a type name (starts with uppercase or is a known primitive type)
            if (IsTypeName(nextTokenValue))
            {
                var startLine = left.LineStart;
                var startColumn = left.ColumnStart;
                Advance(); // skip 'is'
                var typeStartLine = Current.Line;
                var typeStartColumn = Current.Column;
                var typeAnnotation = ParseTypeAnnotation();
                var endLine = Previous.Line;
                var endColumn = Previous.Column;
                return new TypeCheck
                {
                    Value = left,
                    CheckType = typeAnnotation,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = endLine,
                    ColumnEnd = endColumn,
                    // TypeAnnotation doesn't have Span yet (A.12), use left's span for now
                    Span = left.Span
                };
            }
        }

        // Check for comparison chain (a < b < c)
        var operators = new List<ComparisonOperator>();
        var operands = new List<Expression> { left };

        while (IsComparisonOperator(Current.Type))
        {
            var op = Current.Type;
            Advance();

            // Handle multi-token operators: "is not" and "not in"
            if (op == TokenType.Is && Current.Type == TokenType.Not)
            {
                Advance();
                operators.Add(ComparisonOperator.IsNot);
            }
            else if (op == TokenType.Not && Current.Type == TokenType.In)
            {
                Advance();
                operators.Add(ComparisonOperator.NotIn);
            }
            else
            {
                operators.Add(TokenTypeToComparisonOperator(op));
            }

            operands.Add(ParsePipe());
        }

        if (operators.Count == 0)
            return left;

        if (operators.Count == 1)
        {
            // Single comparison - use BinaryOp
            return new BinaryOp
            {
                Operator = ComparisonOperatorToBinary(operators[0]),
                Left = operands[0],
                Right = operands[1],
                LineStart = operands[0].LineStart,
                ColumnStart = operands[0].ColumnStart,
                LineEnd = operands[1].LineEnd,
                ColumnEnd = operands[1].ColumnEnd,
                Span = CombineSpans(operands[0].Span, operands[1].Span)
            };
        }

        // Comparison chain
        return new ComparisonChain
        {
            Operands = operands.ToImmutableArray(),
            Operators = operators.ToImmutableArray(),
            LineStart = operands[0].LineStart,
            ColumnStart = operands[0].ColumnStart,
            LineEnd = operands[^1].LineEnd,
            ColumnEnd = operands[^1].ColumnEnd,
            Span = CombineSpans(operands[0].Span, operands[^1].Span)
        };
    }

    private bool IsComparisonOperator(TokenType type) => type switch
    {
        TokenType.Equal or TokenType.NotEqual or
        TokenType.Less or TokenType.LessEqual or
        TokenType.Greater or TokenType.GreaterEqual or
        TokenType.In or TokenType.Is or TokenType.Not => true,
        _ => false
    };

    private ComparisonOperator TokenTypeToComparisonOperator(TokenType type) => type switch
    {
        TokenType.Equal => ComparisonOperator.Equal,
        TokenType.NotEqual => ComparisonOperator.NotEqual,
        TokenType.Less => ComparisonOperator.LessThan,
        TokenType.LessEqual => ComparisonOperator.LessThanOrEqual,
        TokenType.Greater => ComparisonOperator.GreaterThan,
        TokenType.GreaterEqual => ComparisonOperator.GreaterThanOrEqual,
        TokenType.In => ComparisonOperator.In,
        TokenType.Is => ComparisonOperator.Is,
        _ => throw new ParserError($"Not a comparison operator: {type}", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken)
    };

    private BinaryOperator ComparisonOperatorToBinary(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => BinaryOperator.Equal,
        ComparisonOperator.NotEqual => BinaryOperator.NotEqual,
        ComparisonOperator.LessThan => BinaryOperator.LessThan,
        ComparisonOperator.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
        ComparisonOperator.GreaterThan => BinaryOperator.GreaterThan,
        ComparisonOperator.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
        ComparisonOperator.In => BinaryOperator.In,
        ComparisonOperator.NotIn => BinaryOperator.NotIn,
        ComparisonOperator.Is => BinaryOperator.Is,
        ComparisonOperator.IsNot => BinaryOperator.IsNot,
        _ => throw new ParserError($"Cannot convert comparison operator to binary: {op}", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken)
    };

    private Expression ParsePipe()
    {
        var left = ParseBitwiseOr();

        while (Current.Type == TokenType.PipeForward)
        {
            Advance();
            var right = ParseBitwiseOr();

            left = new BinaryOp
            {
                Operator = BinaryOperator.PipeForward,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseBitwiseOr()
    {
        var left = ParseBitwiseXor();

        while (Current.Type == TokenType.Pipe)
        {
            Advance();
            var right = ParseBitwiseXor();

            left = new BinaryOp
            {
                Operator = BinaryOperator.BitwiseOr,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseBitwiseXor()
    {
        var left = ParseBitwiseAnd();

        while (Current.Type == TokenType.Caret)
        {
            Advance();
            var right = ParseBitwiseAnd();

            left = new BinaryOp
            {
                Operator = BinaryOperator.BitwiseXor,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseBitwiseAnd()
    {
        var left = ParseShift();

        while (Current.Type == TokenType.Ampersand)
        {
            Advance();
            var right = ParseShift();

            left = new BinaryOp
            {
                Operator = BinaryOperator.BitwiseAnd,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseShift()
    {
        var left = ParseAdditive();

        while (Current.Type == TokenType.LeftShift || Current.Type == TokenType.RightShift)
        {
            var op = Current.Type == TokenType.LeftShift ? BinaryOperator.LeftShift : BinaryOperator.RightShift;
            Advance();
            var right = ParseAdditive();

            left = new BinaryOp
            {
                Operator = op,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus)
        {
            var op = Current.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
            Advance();
            var right = ParseMultiplicative();

            left = new BinaryOp
            {
                Operator = op,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseMultiplicative()
    {
        var left = ParseUnary();

        while (Current.Type == TokenType.Star || Current.Type == TokenType.Slash ||
               Current.Type == TokenType.DoubleSlash || Current.Type == TokenType.Percent)
        {
            var op = Current.Type switch
            {
                TokenType.Star => BinaryOperator.Multiply,
                TokenType.Slash => BinaryOperator.Divide,
                TokenType.DoubleSlash => BinaryOperator.FloorDivide,
                TokenType.Percent => BinaryOperator.Modulo,
                _ => throw new ParserError("Unexpected token", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken)
            };
            Advance();
            var right = ParseUnary();

            left = new BinaryOp
            {
                Operator = op,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParseUnary()
    {
        if (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus || Current.Type == TokenType.Tilde)
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;
            var opToken = Current;
            var op = Current.Type switch
            {
                TokenType.Plus => UnaryOperator.Plus,
                TokenType.Minus => UnaryOperator.Minus,
                TokenType.Tilde => UnaryOperator.BitwiseNot,
                _ => throw new ParserError("Unexpected token", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken)
            };
            Advance();
            var operand = ParseUnary();

            return new UnaryOp
            {
                Operator = op,
                Operand = operand,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd,
                Span = CombineSpans(GetSpanFromToken(opToken), operand.Span)
            };
        }

        return ParsePower();
    }

    private Expression ParsePower()
    {
        var left = ParsePostfix();

        if (Current.Type == TokenType.DoubleStar)
        {
            Advance();
            var right = ParseUnary();  // Right-associative

            return new BinaryOp
            {
                Operator = BinaryOperator.Power,
                Left = left,
                Right = right,
                LineStart = left.LineStart,
                ColumnStart = left.ColumnStart,
                LineEnd = right.LineEnd,
                ColumnEnd = right.ColumnEnd,
                Span = CombineSpans(left.Span, right.Span)
            };
        }

        return left;
    }

    private Expression ParsePostfix()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (Current.Type == TokenType.Dot || Current.Type == TokenType.NullConditional)
            {
                var isNullConditional = Current.Type == TokenType.NullConditional;
                Advance();

                var memberToken = Current;
                var member = ExpectIdentifierOrKeyword();

                expr = new MemberAccess
                {
                    Object = expr,
                    Member = member,
                    IsNullConditional = isNullConditional,
                    LineStart = expr.LineStart,
                    ColumnStart = expr.ColumnStart,
                    LineEnd = Previous.Line,
                    ColumnEnd = Previous.Column + Previous.Value.Length,
                    Span = CombineSpans(expr.Span, GetSpanFromToken(Previous))
                };
            }
            else if (Current.Type == TokenType.LeftBracket)
            {
                var bracketToken = Current;
                Advance();
                var index = ParseSliceOrIndex();
                Expect(TokenType.RightBracket);
                var closeBracket = Previous;

                if (index is IndexAccess ia)
                    expr = ia with
                    {
                        Object = expr,
                        LineStart = expr.LineStart,
                        ColumnStart = expr.ColumnStart,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = CombineSpans(expr.Span, GetSpanFromToken(closeBracket))
                    };
                else if (index is SliceAccess sa)
                    expr = sa with
                    {
                        Object = expr,
                        LineStart = expr.LineStart,
                        ColumnStart = expr.ColumnStart,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = CombineSpans(expr.Span, GetSpanFromToken(closeBracket))
                    };
            }
            else if (Current.Type == TokenType.LeftParen)
            {
                Advance();
                var args = new List<Expression>();
                var kwargs = new List<KeywordArgument>();
                var seenKeywordArg = false;

                if (Current.Type != TokenType.RightParen)
                {
                    do
                    {
                        // Check for keyword argument
                        if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Assign)
                        {
                            seenKeywordArg = true;
                            var kwargStartLine = Current.Line;
                            var kwargStartColumn = Current.Column;
                            var name = Current.Value;
                            Advance();  // Skip name
                            Advance();  // Skip =
                            var value = ParseExpression();
                            var kwargEndLine = Peek(-1).Line;
                            var kwargEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

                            kwargs.Add(new KeywordArgument
                            {
                                Name = name,
                                Value = value,
                                LineStart = kwargStartLine,
                                ColumnStart = kwargStartColumn,
                                LineEnd = kwargEndLine,
                                ColumnEnd = kwargEndColumn
                            });
                        }
                        else
                        {
                            if (seenKeywordArg)
                            {
                                throw new ParserError("Positional argument cannot follow keyword argument", Current.Line, Current.Column, DiagnosticCodes.Parser.PositionalAfterKeyword);
                            }
                            args.Add(ParseExpression());
                        }

                        if (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            // Allow trailing comma: foo(1, 2, 3,)
                            if (Current.Type == TokenType.RightParen)
                                break;
                        }
                        else
                            break;
                    } while (true);
                }

                Expect(TokenType.RightParen);
                var closeParen = Previous;

                expr = new FunctionCall
                {
                    Function = expr,
                    Arguments = args.ToImmutableArray(),
                    KeywordArguments = kwargs.ToImmutableArray(),
                    LineStart = expr.LineStart,
                    ColumnStart = expr.ColumnStart,
                    LineEnd = Previous.Line,
                    ColumnEnd = Previous.Column + Previous.Value.Length,
                    Span = CombineSpans(expr.Span, GetSpanFromToken(closeParen))
                };
            }
            else if (Current.Type == TokenType.As)
            {
                // Type cast
                Advance();
                var targetType = ParseTypeAnnotation();

                expr = new TypeCast
                {
                    Value = expr,
                    TargetType = targetType,
                    LineStart = expr.LineStart,
                    ColumnStart = expr.ColumnStart,
                    LineEnd = Previous.Line,
                    ColumnEnd = Previous.Column + Previous.Value.Length,
                    // TypeAnnotation doesn't have Span yet (A.12), use expr's span for now
                    Span = expr.Span
                };
            }
            else if (Current.Type == TokenType.To)
            {
                // Type coercion (value to T or value to T?)
                // Throws InvalidCastException on failure for T, returns None for T?
                Advance();
                var targetType = ParseTypeAnnotation();

                expr = new TypeCoercion
                {
                    Value = expr,
                    TargetType = targetType,
                    LineStart = expr.LineStart,
                    ColumnStart = expr.ColumnStart,
                    LineEnd = Previous.Line,
                    ColumnEnd = Previous.Column + Previous.Value.Length,
                    // TypeAnnotation doesn't have Span yet (A.12), use expr's span for now
                    Span = expr.Span
                };
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expression ParseSliceOrIndex()
    {
        Expression? start = null;
        Expression? stop = null;
        Expression? step = null;

        var isSlice = false;

        // [start:stop:step] or [index] or [type1, type2, ...] for multiple type arguments
        if (Current.Type != TokenType.Colon)
            start = ParseExpression();

        // Handle multiple type arguments: [int, str, bool] for generics
        // If we see a comma after the first expression, continue parsing as a tuple
        if (Current.Type == TokenType.Comma)
        {
            var elements = new List<Expression> { start! };
            while (Current.Type == TokenType.Comma)
            {
                Advance(); // consume ','
                // Allow trailing comma
                if (Current.Type == TokenType.RightBracket)
                    break;
                elements.Add(ParseExpression());
            }

            // Create a TupleLiteral to hold multiple type arguments
            var tuple = new TupleLiteral
            {
                Elements = elements.ToImmutableArray(),
                LineStart = start!.LineStart,
                ColumnStart = start!.ColumnStart,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };

            return new IndexAccess
            {
                Object = null!,  // Will be filled in by caller
                Index = tuple,
                LineStart = Current.Line,
                ColumnStart = Current.Column,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }

        if (Current.Type == TokenType.Colon)
        {
            isSlice = true;
            Advance();

            if (Current.Type != TokenType.Colon && Current.Type != TokenType.RightBracket)
                stop = ParseExpression();

            if (Current.Type == TokenType.Colon)
            {
                Advance();
                if (Current.Type != TokenType.RightBracket)
                    step = ParseExpression();
            }
        }

        if (isSlice)
        {
            return new SliceAccess
            {
                Object = null!,  // Will be filled in by caller
                Start = start,
                Stop = stop,
                Step = step,
                LineStart = Current.Line,
                ColumnStart = Current.Column,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }
        else
        {
            return new IndexAccess
            {
                Object = null!,  // Will be filled in by caller
                Index = start!,
                LineStart = Current.Line,
                ColumnStart = Current.Column,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }
    }

}
