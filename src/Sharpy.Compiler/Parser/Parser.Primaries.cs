using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parser partial class: Primary expression parsing (literals, identifiers, collections)
/// </summary>
public partial class Parser
{
    private Expression ParsePrimary()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;

        switch (Current.Type)
        {
            case TokenType.Integer:
                {
                    var token = Current;
                    var tokenValue = token.Value;
                    Advance();

                    // Extract suffix if present (L, U, UL, etc.)
                    // Skip suffix extraction for hex/octal/binary literals where
                    // trailing letters are digits, not type suffixes
                    string value = tokenValue;
                    string? suffix = null;
                    bool isPrefixedLiteral = tokenValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        || tokenValue.StartsWith("0o", StringComparison.OrdinalIgnoreCase)
                        || tokenValue.StartsWith("0b", StringComparison.OrdinalIgnoreCase);

                    if (!isPrefixedLiteral && tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
                    {
                        // Check for two-letter suffix
                        if (tokenValue.Length > 1 && char.IsLetter(tokenValue[tokenValue.Length - 2]))
                        {
                            suffix = tokenValue.Substring(tokenValue.Length - 2);
                            value = tokenValue.Substring(0, tokenValue.Length - 2);
                        }
                        else
                        {
                            suffix = tokenValue.Substring(tokenValue.Length - 1);
                            value = tokenValue.Substring(0, tokenValue.Length - 1);
                        }
                    }

                    return new IntegerLiteral
                    {
                        Value = value,
                        Suffix = suffix,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(token)
                    };
                }

            case TokenType.Float:
                {
                    var token = Current;
                    var tokenValue = token.Value;
                    Advance();

                    // Extract suffix if present (f, F, d, D, m, M)
                    string value = tokenValue;
                    string? suffix = null;

                    if (tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
                    {
                        suffix = tokenValue.Substring(tokenValue.Length - 1);
                        value = tokenValue.Substring(0, tokenValue.Length - 1);
                    }

                    return new FloatLiteral
                    {
                        Value = value,
                        Suffix = suffix,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(token)
                    };
                }

            case TokenType.String:
                {
                    var token = Current;
                    var value = token.Value;
                    Advance();
                    return new StringLiteral
                    {
                        Value = value,
                        IsRaw = false,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Length,
                        Span = GetSpanFromToken(token)
                    };
                }

            case TokenType.RawString:
                {
                    var token = Current;
                    var value = token.Value;
                    Advance();
                    return new StringLiteral
                    {
                        Value = value,
                        IsRaw = true,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Length,
                        Span = GetSpanFromToken(token)
                    };
                }


            case TokenType.ByteString:
                {
                    var token = Current;
                    var value = token.Value;
                    Advance();
                    return new BytesLiteralExpression
                    {
                        Value = value,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Length,
                        Span = GetSpanFromToken(token)
                    };
                }

            case TokenType.FStringStart:
                {
                    var startToken = Current;
                    // Segmented f-string lexing
                    return ParseSegmentedFString(startLine, startColumn, startToken);
                }

            case TokenType.True:
                {
                    var token = Current;
                    Advance();
                    return new BooleanLiteral
                    {
                        Value = true,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(token)
                    };
                }

            case TokenType.False:
                {
                    var token = Current;
                    Advance();
                    return new BooleanLiteral
                    {
                        Value = false,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(token)
                    };
                }

            case TokenType.None:
                {
                    var token = Current;
                    Advance();
                    return new NoneLiteral
                    {
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(token)
                    };
                }

            case TokenType.Ellipsis:
                {
                    var token = Current;
                    Advance();
                    return new EllipsisLiteral
                    {
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(token)
                    };
                }

            case TokenType.Identifier:
                {
                    var identToken = Current;
                    var name = identToken.Value;
                    Advance();
                    return new Identifier
                    {
                        Name = name,
                        IsNameBacktickEscaped = identToken.IsBacktickEscaped,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(identToken)
                    };
                }

            // 'type' is a keyword (TokenType.Type) for type alias declarations,
            // but in expression context it acts as a callable: type(X) → typeof(X)
            case TokenType.Type:
                {
                    var typeToken = Current;
                    Advance();
                    return new Identifier
                    {
                        Name = "type",
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(typeToken)
                    };
                }

            case TokenType.Super:
                {
                    var startToken = Current;
                    Advance();
                    // Expect super() - must be followed by ()
                    Expect(TokenType.LeftParen);
                    Expect(TokenType.RightParen);
                    return new SuperExpression
                    {
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromTokens(startToken, Previous)
                    };
                }

            case TokenType.LeftParen:
                {
                    var startToken = Current;
                    Advance();

                    // Empty parens: () -> expr is arrow lambda, () is empty tuple
                    if (Current.Type == TokenType.RightParen)
                    {
                        Advance();

                        if (Current.Type == TokenType.Arrow)
                        {
                            return ParseArrowLambdaBody(
                                ImmutableArray<Parameter>.Empty,
                                startLine, startColumn, startToken);
                        }

                        return new TupleLiteral
                        {
                            Elements = ImmutableArray<Expression>.Empty,
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }

                    // Check for named tuple literal: (x=1.0, y=2.0)
                    // identifier '=' (not '==') indicates a named element
                    if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Assign)
                    {
                        return ParseNamedTupleLiteral(startLine, startColumn, startToken);
                    }

                    // Check for arrow lambda: (name: type, ...) -> expr
                    // identifier ':' uniquely identifies arrow lambda params inside ()
                    if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Colon)
                    {
                        var arrowParams = ParseArrowLambdaParams();
                        Expect(TokenType.RightParen);
                        return ParseArrowLambdaBody(
                            arrowParams.ToImmutableArray(),
                            startLine, startColumn, startToken);
                    }

                    var expr = ParseExpression();

                    // Tuple (expr,) or (expr, expr2, ...)
                    if (Current.Type == TokenType.Comma)
                    {
                        var elements = new List<Expression> { expr };

                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightParen)
                                break;
                            elements.Add(ParseExpression());
                        }

                        Expect(TokenType.RightParen);
                        return new TupleLiteral
                        {
                            Elements = elements.ToImmutableArray(),
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }

                    Expect(TokenType.RightParen);

                    // Check for operator section: (_ * 2) or (_ > 0)
                    if (ContainsPlaceholderIdentifier(expr))
                    {
                        return LowerOperatorSectionToLambda(expr, startLine, startColumn, startToken);
                    }

                    return new Parenthesized
                    {
                        Expression = expr,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromTokens(startToken, Previous)
                    };
                }

            case TokenType.LeftBracket:
                {
                    var startToken = Current;
                    Advance();

                    // Empty list []
                    if (Current.Type == TokenType.RightBracket)
                    {
                        Advance();
                        return new ListLiteral
                        {
                            Elements = ImmutableArray<Expression>.Empty,
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }

                    var firstExpr = ParseListElement();

                    // Check for list comprehension: [expr for x in iterable]
                    // (only if first element is not a spread)
                    if (firstExpr is not SpreadElement && Current.Type == TokenType.For)
                    {
                        var clauses = ParseComprehensionClauses();
                        Expect(TokenType.RightBracket);
                        return new ListComprehension
                        {
                            Element = firstExpr,
                            Clauses = clauses.ToImmutableArray(),
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }

                    // Regular list literal [elem1, elem2, ...]
                    var elements = new List<Expression> { firstExpr };

                    while (Current.Type == TokenType.Comma)
                    {
                        Advance();
                        // Allow trailing comma: [1, 2, 3,]
                        if (Current.Type == TokenType.RightBracket)
                            break;
                        elements.Add(ParseListElement());
                    }

                    Expect(TokenType.RightBracket);
                    return new ListLiteral
                    {
                        Elements = elements.ToImmutableArray(),
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromTokens(startToken, Previous)
                    };
                }

            case TokenType.LeftBrace:
                {
                    var startToken = Current;
                    Advance();

                    // Empty set {/} - special v0.2 syntax
                    if (Current.Type == TokenType.Slash)
                    {
                        Advance();
                        Expect(TokenType.RightBrace);
                        return new SetLiteral
                        {
                            Elements = ImmutableArray<Expression>.Empty,
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }

                    // Empty dict {}
                    if (Current.Type == TokenType.RightBrace)
                    {
                        Advance();
                        return new DictLiteral
                        {
                            Entries = ImmutableArray<DictEntry>.Empty,
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }

                    // Dict spread: {**d1, ...} — first token is **
                    if (Current.Type == TokenType.DoubleStar)
                    {
                        var entries = new List<DictEntry> { ParseDictEntry() };
                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightBrace)
                                break;
                            entries.Add(ParseDictEntry());
                        }

                        Expect(TokenType.RightBrace);
                        return new DictLiteral
                        {
                            Entries = entries.ToImmutableArray(),
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }

                    // Set spread: {*a, ...} — first token is *
                    // Parse first element (may be spread)
                    var firstExpr = ParseSetElement();

                    // Dict {key: value, ...} or dict comprehension {key: value for x in iterable}
                    if (firstExpr is not SpreadElement && Current.Type == TokenType.Colon)
                    {
                        Advance();
                        var firstValue = ParseExpression();

                        // Check for dict comprehension: {key: value for x in iterable}
                        if (Current.Type == TokenType.For)
                        {
                            var clauses = ParseComprehensionClauses();
                            Expect(TokenType.RightBrace);
                            return new DictComprehension
                            {
                                Key = firstExpr,
                                Value = firstValue,
                                Clauses = clauses.ToImmutableArray(),
                                LineStart = startLine,
                                ColumnStart = startColumn,
                                LineEnd = Previous.Line,
                                ColumnEnd = Previous.Column + Previous.Value.Length,
                                Span = GetSpanFromTokens(startToken, Previous)
                            };
                        }

                        // Regular dict literal (may contain ** spread entries)
                        var entries = new List<DictEntry> { new DictEntry { Key = firstExpr, Value = firstValue } };

                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightBrace)
                                break;
                            entries.Add(ParseDictEntry());
                        }

                        Expect(TokenType.RightBrace);
                        return new DictLiteral
                        {
                            Entries = entries.ToImmutableArray(),
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }
                    // Set {elem1, elem2, ...} or set comprehension {expr for x in iterable}
                    else
                    {
                        // Check for set comprehension: {expr for x in iterable}
                        if (firstExpr is not SpreadElement && Current.Type == TokenType.For)
                        {
                            var clauses = ParseComprehensionClauses();
                            Expect(TokenType.RightBrace);
                            return new SetComprehension
                            {
                                Element = firstExpr,
                                Clauses = clauses.ToImmutableArray(),
                                LineStart = startLine,
                                ColumnStart = startColumn,
                                LineEnd = Previous.Line,
                                ColumnEnd = Previous.Column + Previous.Value.Length,
                                Span = GetSpanFromTokens(startToken, Previous)
                            };
                        }

                        // Regular set literal (may contain * spread elements)
                        var elements = new List<Expression> { firstExpr };

                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightBrace)
                                break;
                            elements.Add(ParseSetElement());
                        }

                        Expect(TokenType.RightBrace);
                        return new SetLiteral
                        {
                            Elements = elements.ToImmutableArray(),
                            LineStart = startLine,
                            ColumnStart = startColumn,
                            LineEnd = Previous.Line,
                            ColumnEnd = Previous.Column + Previous.Value.Length,
                            Span = GetSpanFromTokens(startToken, Previous)
                        };
                    }
                }

            case TokenType.Lambda:
                {
                    var lambdaToken = Current;
                    Advance();
                    var parameters = new List<Parameter>();

                    // Parse lambda parameters
                    if (Current.Type != TokenType.Colon)
                    {
                        _lastLoopPosition = -1;
                        do
                        {
                            if (!CheckLoopProgress())
                                break;

                            var paramToken = Current;
                            var name = ExpectIdentifier();

                            TypeAnnotation? paramType = null;
                            if (Current.Type == TokenType.Colon)
                            {
                                // Lambda parameter type annotation disambiguation (#275, #289)
                                //
                                // Problem: In `lambda x: body`, the ':' separates params from body.
                                // But in `lambda x: int: body`, the first ':' is a type annotation.
                                // We need a lookahead heuristic to disambiguate.
                                //
                                // Strategy: After seeing ':', check if the NEXT token starts a type
                                // (Identifier, Auto, or None), then check if the token AFTER THAT
                                // is consistent with "still in parameter list" rather than "start of
                                // lambda body expression".
                                //
                                // Tokens that confirm a type annotation (they follow a type name):
                                //   , → another parameter follows      (lambda x: int, y: ...)
                                //   = → default value follows           (lambda x: int = 0: ...)
                                //   : → lambda body separator           (lambda x: int: body)
                                //   [ → generic type arguments          (lambda x: list[int]: ...)
                                //   ? → optional type                   (lambda x: int?: ...)
                                //   ! → result type                     (lambda x: int!str: ...)
                                //   | → nullable type only if followed by None (lambda x: int | None: ...)
                                //       Otherwise '|' starts a bitwise-OR body (lambda x: a | b).
                                //       None is not a valid bitwise-OR operand, so Peek(3)==None
                                //       unambiguously identifies the type annotation case.
                                //
                                // Note: True/False are TokenType.True/TokenType.False (not Identifier),
                                // so `lambda x: True | False` skips the type path entirely and parses
                                // as a body expression, which is correct.
                                //
                                // This heuristic works for all currently supported type syntax
                                // (simple, generic, optional, nullable, result types).
                                var nextType = Peek().Type;
                                var isTypeAnnotation = false;

                                if (nextType == TokenType.Identifier
                                    || nextType == TokenType.Auto
                                    || nextType == TokenType.None)
                                {
                                    var afterType = Peek(2).Type;
                                    if (afterType == TokenType.Pipe)
                                    {
                                        // Disambiguate: `int | None` is a nullable type annotation,
                                        // but `a | b` (where b is not None) is a bitwise-OR body.
                                        isTypeAnnotation = Peek(3).Type == TokenType.None;
                                    }
                                    else
                                    {
                                        isTypeAnnotation = afterType is TokenType.Comma
                                            or TokenType.Assign or TokenType.Colon
                                            or TokenType.LeftBracket or TokenType.Question
                                            or TokenType.Bang;
                                    }
                                }

                                if (isTypeAnnotation)
                                {
                                    Advance();
                                    paramType = ParseTypeAnnotation();
                                }
                            }

                            Expression? defaultValue = null;
                            if (Current.Type == TokenType.Assign)
                            {
                                Advance();
                                defaultValue = ParseExpression();
                            }

                            var paramEndToken = Previous;
                            var paramEndLine = defaultValue != null || paramType != null ? paramEndToken.Line : paramToken.Line;
                            var paramEndColumn = defaultValue != null || paramType != null ? paramEndToken.Column + paramEndToken.Value.Length : paramToken.Column + name.Length;

                            parameters.Add(new Parameter
                            {
                                Name = name,
                                IsNameBacktickEscaped = paramToken.IsBacktickEscaped,
                                Type = paramType,
                                DefaultValue = defaultValue,
                                LineStart = paramToken.Line,
                                ColumnStart = paramToken.Column,
                                LineEnd = paramEndLine,
                                ColumnEnd = paramEndColumn,
                                Span = defaultValue != null || paramType != null ? GetSpanFromTokens(paramToken, paramEndToken) : GetSpanFromToken(paramToken)
                            });

                            if (Current.Type == TokenType.Comma)
                                Advance();
                            else
                                break;
                        } while (true);
                    }

                    Expect(TokenType.Colon);
                    var body = ParseExpression();

                    // Combine lambda token span with body span
                    var lambdaSpan = GetSpanFromToken(lambdaToken);
                    var combinedSpan = lambdaSpan != null && body.Span != null
                        ? lambdaSpan.Value.Union(body.Span.Value)
                        : (Text.TextSpan?)null;

                    return new LambdaExpression
                    {
                        Parameters = parameters.ToImmutableArray(),
                        Body = body,
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = body.LineEnd,
                        ColumnEnd = body.ColumnEnd,
                        Span = combinedSpan
                    };
                }

            case TokenType.Match:
                return ParseMatchExpression();

            default:
                throw ReportError($"Unexpected token: {Current.Type}", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken, span: CurrentSpan);
        }
    }

    /// <summary>
    /// Parses a named tuple literal: (x=1.0, y=2.0)
    /// Called when the parser has already consumed '(' and sees 'identifier =' pattern.
    /// </summary>
    private TupleLiteral ParseNamedTupleLiteral(int startLine, int startColumn, Token startToken)
    {
        var elements = new List<Expression>();
        var names = new List<string?>();

        // Parse first named element
        var firstName = Current.Value;
        Advance(); // consume identifier
        Advance(); // consume '='
        names.Add(firstName);
        elements.Add(ParseExpression());

        // Parse remaining elements (must all be named)
        while (Current.Type == TokenType.Comma)
        {
            Advance(); // consume ','
            if (Current.Type == TokenType.RightParen)
                break;

            if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Assign)
            {
                names.Add(Current.Value);
                Advance(); // consume identifier
                Advance(); // consume '='
                elements.Add(ParseExpression());
            }
            else
            {
                throw ReportError(
                    "Named tuple elements must be either all named or all unnamed",
                    Current.Line, Current.Column,
                    DiagnosticCodes.Parser.MixedNamedUnnamedTupleElements,
                    span: CurrentSpan);
            }
        }

        Expect(TokenType.RightParen);
        return new TupleLiteral
        {
            Elements = elements.ToImmutableArray(),
            ElementNames = names.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromTokens(startToken, Previous)
        };
    }

    /// <summary>
    /// Parses a list element, handling *spread syntax.
    /// </summary>
    private Expression ParseListElement()
    {
        if (Current.Type == TokenType.Star)
        {
            var starToken = Current;
            Advance();
            var operand = ParseExpression();
            return new SpreadElement
            {
                Value = operand,
                LineStart = starToken.Line,
                ColumnStart = starToken.Column,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd,
                Span = operand.Span
            };
        }

        return ParseExpression();
    }

    /// <summary>
    /// Parses a set element, handling *spread syntax.
    /// </summary>
    private Expression ParseSetElement()
    {
        if (Current.Type == TokenType.Star)
        {
            var starToken = Current;
            Advance();
            var operand = ParseExpression();
            return new SpreadElement
            {
                Value = operand,
                LineStart = starToken.Line,
                ColumnStart = starToken.Column,
                LineEnd = operand.LineEnd,
                ColumnEnd = operand.ColumnEnd,
                Span = operand.Span
            };
        }

        return ParseExpression();
    }

    /// <summary>
    /// Parses a dict entry: either key: value or **spread.
    /// </summary>
    private DictEntry ParseDictEntry()
    {
        if (Current.Type == TokenType.DoubleStar)
        {
            Advance();
            var spreadValue = ParseExpression();
            return new DictEntry { Key = null, Value = spreadValue };
        }

        var key = ParseExpression();
        Expect(TokenType.Colon);
        var value = ParseExpression();
        return new DictEntry { Key = key, Value = value };
    }

    /// <summary>
    /// Checks if an expression tree contains any Identifier("_") placeholder.
    /// Only checks expression types that can appear in operator sections.
    /// </summary>
    private static bool ContainsPlaceholderIdentifier(Expression expr) => expr switch
    {
        Identifier { Name: "_" } => true,
        BinaryOp bin => ContainsPlaceholderIdentifier(bin.Left) || ContainsPlaceholderIdentifier(bin.Right),
        UnaryOp un => ContainsPlaceholderIdentifier(un.Operand),
        ComparisonChain chain => chain.Operands.Any(ContainsPlaceholderIdentifier),
        _ => false
    };

    /// <summary>
    /// Lowers an expression containing Identifier("_") placeholders into a LambdaExpression.
    /// Each placeholder gets a unique __placeholder_N parameter name.
    /// </summary>
    private Expression LowerOperatorSectionToLambda(Expression expr, int startLine, int startColumn, Token startToken)
    {
        var placeholderIndex = 0;
        var parameters = new List<Parameter>();

        var modifiedExpr = ReplacePlaceholders(expr, parameters, ref placeholderIndex);

        return new LambdaExpression
        {
            Parameters = parameters.ToImmutableArray(),
            Body = modifiedExpr,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromTokens(startToken, Previous)
        };
    }

    /// <summary>
    /// Recursively replaces Identifier("_") nodes with unique __placeholder_N identifiers
    /// and collects the corresponding Parameter records.
    /// </summary>
    private static Expression ReplacePlaceholders(Expression expr, List<Parameter> parameters, ref int index)
    {
        switch (expr)
        {
            case Identifier { Name: "_" } placeholder:
                var paramName = $"__placeholder_{index}";
                parameters.Add(new Parameter
                {
                    Name = paramName,
                    Type = null,
                    DefaultValue = null,
                    Kind = ParameterKind.Normal,
                    LineStart = placeholder.LineStart,
                    ColumnStart = placeholder.ColumnStart,
                    LineEnd = placeholder.LineEnd,
                    ColumnEnd = placeholder.ColumnEnd,
                    Span = placeholder.Span
                });
                index++;
                return new Identifier
                {
                    Name = paramName,
                    LineStart = placeholder.LineStart,
                    ColumnStart = placeholder.ColumnStart,
                    LineEnd = placeholder.LineEnd,
                    ColumnEnd = placeholder.ColumnEnd,
                    Span = placeholder.Span
                };

            case BinaryOp bin:
                return bin with
                {
                    Left = ReplacePlaceholders(bin.Left, parameters, ref index),
                    Right = ReplacePlaceholders(bin.Right, parameters, ref index)
                };

            case UnaryOp un:
                return un with
                {
                    Operand = ReplacePlaceholders(un.Operand, parameters, ref index)
                };

            case ComparisonChain chain:
                var builder = ImmutableArray.CreateBuilder<Expression>(chain.Operands.Length);
                foreach (var operand in chain.Operands)
                    builder.Add(ReplacePlaceholders(operand, parameters, ref index));
                return chain with { Operands = builder.MoveToImmutable() };

            default:
                return expr;
        }
    }

    /// <summary>
    /// Parses arrow lambda parameter list: name: type, name: type, ...
    /// Called after '(' when 'identifier :' pattern is detected.
    /// Caller is responsible for consuming ')' afterward.
    /// </summary>
    private List<Parameter> ParseArrowLambdaParams()
    {
        var parameters = new List<Parameter>();

        _lastLoopPosition = -1;
        do
        {
            if (!CheckLoopProgress())
                break;

            var paramToken = Current;
            var name = ExpectIdentifier();

            Expect(TokenType.Colon);
            var paramType = ParseTypeAnnotation();

            Expression? defaultValue = null;
            if (Current.Type == TokenType.Assign)
            {
                Advance();
                defaultValue = ParseExpression();
            }

            var paramEndToken = Previous;
            parameters.Add(new Parameter
            {
                Name = name,
                IsNameBacktickEscaped = paramToken.IsBacktickEscaped,
                Type = paramType,
                DefaultValue = defaultValue,
                LineStart = paramToken.Line,
                ColumnStart = paramToken.Column,
                LineEnd = paramEndToken.Line,
                ColumnEnd = paramEndToken.Column + paramEndToken.Value.Length,
                Span = GetSpanFromTokens(paramToken, paramEndToken)
            });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);

        return parameters;
    }

    /// <summary>
    /// Parses arrow lambda body after '->' token.
    /// Handles optional return type: (x: int) -> int: x + 1
    /// Without return type: (x: int) -> x + 1
    /// </summary>
    private LambdaExpression ParseArrowLambdaBody(
        ImmutableArray<Parameter> parameters,
        int startLine, int startColumn, Token startToken)
    {
        Expect(TokenType.Arrow);

        TypeAnnotation? returnType = null;

        // Check for return type: -> type : body
        // Lookahead: if current is an identifier/keyword that forms a type, AND the token
        // after the type is ':', then we have a return type annotation.
        if (IsTypeStart() && LookaheadReturnTypeColon())
        {
            returnType = ParseTypeAnnotation();
            Expect(TokenType.Colon);
        }

        var body = ParseExpression();

        var combinedSpan = startToken != null && body.Span != null
            ? GetSpanFromToken(startToken)?.Union(body.Span.Value)
            : (Text.TextSpan?)null;

        return new LambdaExpression
        {
            Parameters = parameters,
            Body = body,
            ReturnType = returnType,
            IsArrowSyntax = true,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = body.LineEnd,
            ColumnEnd = body.ColumnEnd,
            Span = combinedSpan
        };
    }

    /// <summary>
    /// Returns true if the current token can start a type annotation
    /// (identifier, None, Auto, or tuple/function type via LeftParen).
    /// </summary>
    private bool IsTypeStart()
    {
        return Current.Type is TokenType.Identifier
            or TokenType.None
            or TokenType.Auto
            or TokenType.LeftParen;
    }

    /// <summary>
    /// Lookahead to determine if, after parsing a type annotation from the current
    /// position, the next token is ':' (indicating a return type in arrow lambda).
    /// Uses a saved position to avoid consuming tokens.
    /// </summary>
    private bool LookaheadReturnTypeColon()
    {
        var savedPos = _position;
        try
        {
            // Speculatively parse a type annotation to find where it ends
            SkipTypeAnnotation();
            return Current.Type == TokenType.Colon;
        }
        finally
        {
            _position = savedPos;
        }
    }

    /// <summary>
    /// Skips over a type annotation without constructing AST nodes.
    /// Used for lookahead in arrow lambda return type detection.
    /// Handles: simple types, generic types (T[U, V]), optional (T?),
    /// nullable (T | None), result types (T!E), tuple/function types ((T, U) -> V).
    /// </summary>
    private void SkipTypeAnnotation()
    {
        if (Current.Type == TokenType.LeftParen)
        {
            // Tuple or function type: (T, U) or (T) -> U
            Advance(); // consume '('
            if (Current.Type != TokenType.RightParen)
            {
                SkipTypeAnnotation();
                while (Current.Type == TokenType.Comma)
                {
                    Advance();
                    SkipTypeAnnotation();
                }
            }
            if (Current.Type == TokenType.RightParen)
                Advance();
            if (Current.Type == TokenType.Arrow)
            {
                Advance();
                SkipTypeAnnotation();
            }
        }
        else if (Current.Type is TokenType.Identifier or TokenType.None or TokenType.Auto)
        {
            Advance();
            // Generic type arguments: T[U, V]
            if (Current.Type == TokenType.LeftBracket)
            {
                Advance();
                if (Current.Type != TokenType.RightBracket)
                {
                    SkipTypeAnnotation();
                    while (Current.Type == TokenType.Comma)
                    {
                        Advance();
                        SkipTypeAnnotation();
                    }
                }
                if (Current.Type == TokenType.RightBracket)
                    Advance();
            }
        }

        // Optional: T?
        if (Current.Type == TokenType.Question)
            Advance();

        // Nullable: T | None
        if (Current.Type == TokenType.Pipe && Peek().Type == TokenType.None)
        {
            Advance(); // consume '|'
            Advance(); // consume 'None'
        }

        // Result type: T!E
        if (Current.Type == TokenType.Bang)
        {
            Advance();
            SkipTypeAnnotation();
        }
    }
}
