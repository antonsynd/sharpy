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
                    string value = tokenValue;
                    string? suffix = null;

                    if (tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
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
                        ColumnEnd = Previous.Column + Previous.Value.Length,
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
                        ColumnEnd = Previous.Column + Previous.Value.Length,
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
                        LineStart = startLine,
                        ColumnStart = startColumn,
                        LineEnd = Previous.Line,
                        ColumnEnd = Previous.Column + Previous.Value.Length,
                        Span = GetSpanFromToken(identToken)
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

                    // Empty tuple ()
                    if (Current.Type == TokenType.RightParen)
                    {
                        Advance();
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

                    var firstExpr = ParseExpression();

                    // Check for list comprehension: [expr for x in iterable]
                    if (Current.Type == TokenType.For)
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
                        elements.Add(ParseExpression());
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

                    var firstExpr = ParseExpression();

                    // Dict {key: value, ...} or dict comprehension {key: value for x in iterable}
                    if (Current.Type == TokenType.Colon)
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

                        // Regular dict literal
                        var entries = new List<DictEntry> { new DictEntry { Key = firstExpr, Value = firstValue } };

                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightBrace)
                                break;

                            var key = ParseExpression();
                            Expect(TokenType.Colon);
                            var value = ParseExpression();
                            entries.Add(new DictEntry { Key = key, Value = value });
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
                        if (Current.Type == TokenType.For)
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

                        // Regular set literal
                        var elements = new List<Expression> { firstExpr };

                        while (Current.Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current.Type == TokenType.RightBrace)
                                break;
                            elements.Add(ParseExpression());
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
                            parameters.Add(new Parameter
                            {
                                Name = name,
                                LineStart = paramToken.Line,
                                ColumnStart = paramToken.Column,
                                LineEnd = paramToken.Line,
                                ColumnEnd = paramToken.Column + name.Length,
                                Span = GetSpanFromToken(paramToken)
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

            default:
                throw ReportError($"Unexpected token: {Current.Type}", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken);
        }
    }
}
