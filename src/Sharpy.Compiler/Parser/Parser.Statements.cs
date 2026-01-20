using System.Text;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parser partial class: Statement parsing (control flow, imports)
/// </summary>
public partial class Parser
{
    private IfStatement ParseIfStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.If);
        var test = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var thenBody = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        var elifClauses = new List<ElifClause>();
        var elseBody = new List<Statement>();

        // Elif clauses
        while (Current.Type == TokenType.Elif)
        {
            var elifStartLine = Current.Line;
            var elifStartColumn = Current.Column;
            var elifStartToken = Current;
            Advance();
            var elifTest = ParseExpression();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            var elifBody = ParseBlock();
            Expect(TokenType.Dedent);
            var elifEndLine = Peek(-1).Line;
            var elifEndColumn = Peek(-1).Column + Peek(-1).Value.Length;
            endToken = Previous;

            elifClauses.Add(new ElifClause
            {
                Test = elifTest,
                Body = elifBody,
                LineStart = elifStartLine,
                ColumnStart = elifStartColumn,
                LineEnd = elifEndLine,
                ColumnEnd = elifEndColumn,
                Span = GetSpanFromTokens(elifStartToken, Previous)
            });
        }

        // Else clause
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            elseBody = ParseBlock();
            Expect(TokenType.Dedent);
            endToken = Previous;
        }

        return new IfStatement
        {
            Test = test,
            ThenBody = thenBody,
            ElifClauses = elifClauses,
            ElseBody = elseBody,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private WhileStatement ParseWhileStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.While);
        var test = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        // Optional else clause (runs if loop completes without break)
        var elseBody = new List<Statement>();
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            elseBody = ParseBlock();
            Expect(TokenType.Dedent);
            endToken = Previous;
        }

        return new WhileStatement
        {
            Test = test,
            Body = body,
            ElseBody = elseBody,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private ForStatement ParseForStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.For);

        // Parse target - this should be a simple identifier or tuple, not a full expression
        // We need to stop before consuming 'in' as a comparison operator
        var target = ParseForTarget();

        Expect(TokenType.In);
        var iterator = ParseExpression();
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        // Optional else clause (runs if loop completes without break)
        var elseBody = new List<Statement>();
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            elseBody = ParseBlock();
            Expect(TokenType.Dedent);
            endToken = Previous;
        }

        return new ForStatement
        {
            Target = target,
            Iterator = iterator,
            Body = body,
            ElseBody = elseBody,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private Expression ParseForTarget()
    {
        // For target can be:
        // - Simple identifier: for x in ...
        // - Tuple: for x, y in ...
        // We parse up to but not including the 'in' keyword

        var startLine = Current.Line;
        var startColumn = Current.Column;

        var first = ParsePrimary();

        // Check if it's a tuple (comma-separated)
        if (Current.Type == TokenType.Comma)
        {
            var elements = new List<Expression> { first };

            while (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.In)
                    break;  // Trailing comma before 'in'
                elements.Add(ParsePrimary());
            }

            return new TupleLiteral
            {
                Elements = elements,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }

        return first;
    }

    /// <summary>
    /// Parse comprehension clauses: for x in iterable [if condition] [for y in iterable2] ...
    /// For now, only supporting single variable (no tuple unpacking in comprehensions)
    /// </summary>
    private List<ComprehensionClause> ParseComprehensionClauses()
    {
        var clauses = new List<ComprehensionClause>();

        while (true)
        {
            if (Current.Type == TokenType.For)
            {
                var startLine = Current.Line;
                var startColumn = Current.Column;
                Advance();

                // Parse target (single identifier for now)
                var target = ParseForTarget();

                Expect(TokenType.In);
                var iterator = ParseLogicalOr(); // Use lower precedence to avoid consuming too much

                clauses.Add(new ForClause
                {
                    Target = target,
                    Iterator = iterator,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                });
            }
            else if (Current.Type == TokenType.If)
            {
                var startLine = Current.Line;
                var startColumn = Current.Column;
                Advance();

                var condition = ParseLogicalOr(); // Use lower precedence to avoid consuming too much

                clauses.Add(new IfClause
                {
                    Condition = condition,
                    LineStart = startLine,
                    ColumnStart = startColumn,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                });
            }
            else
            {
                break;
            }
        }

        return clauses;
    }

    private TryStatement ParseTryStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Try);
        Expect(TokenType.Colon);
        ExpectNewline();
        Expect(TokenType.Indent);
        var body = ParseBlock();
        Expect(TokenType.Dedent);
        var endToken = Previous;

        var handlers = new List<ExceptHandler>();

        while (Current.Type == TokenType.Except)
        {
            var handlerStartLine = Current.Line;
            var handlerStartColumn = Current.Column;
            var handlerStartToken = Current;
            Advance();

            TypeAnnotation? exceptionType = null;
            string? name = null;

            // except ExceptionType as name:
            if (Current.Type != TokenType.Colon)
            {
                exceptionType = ParseTypeAnnotation();

                if (Current.Type == TokenType.As)
                {
                    Advance();
                    name = ExpectIdentifier();
                }
            }

            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            var handlerBody = ParseBlock();
            Expect(TokenType.Dedent);
            var handlerEndLine = Peek(-1).Line;
            var handlerEndColumn = Peek(-1).Column + Peek(-1).Value.Length;
            endToken = Previous;

            handlers.Add(new ExceptHandler
            {
                ExceptionType = exceptionType,
                Name = name,
                Body = handlerBody,
                LineStart = handlerStartLine,
                ColumnStart = handlerStartColumn,
                LineEnd = handlerEndLine,
                ColumnEnd = handlerEndColumn,
                Span = GetSpanFromTokens(handlerStartToken, Previous)
            });
        }

        // else clause (runs if no exception raised in try block)
        var elseBody = new List<Statement>();
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            elseBody = ParseBlock();
            Expect(TokenType.Dedent);
            endToken = Previous;
        }

        var finallyBody = new List<Statement>();
        if (Current.Type == TokenType.Finally)
        {
            Advance();
            Expect(TokenType.Colon);
            ExpectNewline();
            Expect(TokenType.Indent);
            finallyBody = ParseBlock();
            Expect(TokenType.Dedent);
            endToken = Previous;
        }

        return new TryStatement
        {
            Body = body,
            Handlers = handlers,
            ElseBody = elseBody,
            FinallyBody = finallyBody,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private ReturnStatement ParseReturnStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var returnToken = Current;

        Expect(TokenType.Return);

        Expression? value = null;
        if (Current.Type != TokenType.Newline && Current.Type != TokenType.Dedent && !IsAtEnd)
            value = ParseExpression();

        ExpectStatementEnd();

        return new ReturnStatement
        {
            Value = value,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = value != null
                ? CombineSpans(GetSpanFromToken(returnToken), value.Span)
                : GetSpanFromToken(returnToken)
        };
    }

    private RaiseStatement ParseRaiseStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var raiseToken = Current;

        Expect(TokenType.Raise);

        Expression? exception = null;
        Expression? cause = null;

        if (Current.Type != TokenType.Newline && !IsAtEnd)
        {
            exception = ParseExpression();

            // raise ... from cause
            if (Current.Type == TokenType.From)
            {
                Advance();
                cause = ParseExpression();
            }
        }

        ExpectNewline();

        // Determine the span end based on what was parsed
        var endSpan = cause?.Span ?? exception?.Span ?? GetSpanFromToken(raiseToken);

        return new RaiseStatement
        {
            Exception = exception,
            Cause = cause,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = CombineSpans(GetSpanFromToken(raiseToken), endSpan)
        };
    }

    private AssertStatement ParseAssertStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var assertToken = Current;

        Expect(TokenType.Assert);
        var test = ParseExpression();

        Expression? message = null;
        if (Current.Type == TokenType.Comma)
        {
            Advance();
            message = ParseExpression();
        }

        ExpectNewline();

        var endSpan = message?.Span ?? test.Span;

        return new AssertStatement
        {
            Test = test,
            Message = message,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = CombineSpans(GetSpanFromToken(assertToken), endSpan)
        };
    }

    private PassStatement ParsePassStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var passToken = Current;

        Expect(TokenType.Pass);
        ExpectStatementEnd();

        return new PassStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromToken(passToken)
        };
    }

    private BreakStatement ParseBreakStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var breakToken = Current;

        Expect(TokenType.Break);
        ExpectStatementEnd();

        return new BreakStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromToken(breakToken)
        };
    }

    private ContinueStatement ParseContinueStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var continueToken = Current;

        Expect(TokenType.Continue);
        ExpectStatementEnd();

        return new ContinueStatement
        {
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = GetSpanFromToken(continueToken)
        };
    }

    private ImportStatement ParseImportStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.Import);

        var names = new List<ImportAlias>();

        do
        {
            var aliasStartLine = Current.Line;
            var aliasStartColumn = Current.Column;
            var aliasStartToken = Current;
            var name = ParseDottedName();
            string? asName = null;

            if (Current.Type == TokenType.As)
            {
                Advance();
                asName = ExpectIdentifier();
            }

            var aliasEndLine = Peek(-1).Line;
            var aliasEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

            names.Add(new ImportAlias
            {
                Name = name,
                AsName = asName,
                LineStart = aliasStartLine,
                ColumnStart = aliasStartColumn,
                LineEnd = aliasEndLine,
                ColumnEnd = aliasEndColumn,
                Span = GetSpanFromTokens(aliasStartToken, Previous)
            });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);

        var endToken = Previous;
        ExpectNewline();

        return new ImportStatement
        {
            Names = names,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private FromImportStatement ParseFromImportStatement()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        Expect(TokenType.From);
        var module = ParseModuleName();
        Expect(TokenType.Import);

        var names = new List<ImportAlias>();
        var importAll = false;

        if (Current.Type == TokenType.Star)
        {
            Advance();
            importAll = true;
        }
        else
        {
            do
            {
                var aliasStartLine = Current.Line;
                var aliasStartColumn = Current.Column;
                var aliasStartToken = Current;
                var name = ExpectIdentifier();
                string? asName = null;

                if (Current.Type == TokenType.As)
                {
                    Advance();
                    asName = ExpectIdentifier();
                }

                var aliasEndLine = Peek(-1).Line;
                var aliasEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

                names.Add(new ImportAlias
                {
                    Name = name,
                    AsName = asName,
                    LineStart = aliasStartLine,
                    ColumnStart = aliasStartColumn,
                    LineEnd = aliasEndLine,
                    ColumnEnd = aliasEndColumn,
                    Span = GetSpanFromTokens(aliasStartToken, Previous)
                });

                if (Current.Type == TokenType.Comma)
                    Advance();
                else
                    break;
            } while (true);
        }

        var endToken = Previous;
        ExpectNewline();

        return new FromImportStatement
        {
            Module = module,
            Names = names,
            ImportAll = importAll,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endToken.Line,
            ColumnEnd = endToken.Column + endToken.Value.Length,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private string ParseDottedName()
    {
        var parts = new List<string> { ExpectIdentifier() };

        while (Current.Type == TokenType.Dot)
        {
            Advance();
            parts.Add(ExpectIdentifier());
        }

        return string.Join(".", parts);
    }

    private string ParseModuleName()
    {
        // Handle relative imports with leading dots (e.g., ".helpers", "..utils")
        var leadingDots = new StringBuilder();
        while (Current.Type == TokenType.Dot)
        {
            leadingDots.Append('.');
            Advance();
        }

        // After the leading dots, there may be an identifier and more dotted parts
        // For example: ".helpers" or "..package.module"
        // But it's also valid to have just dots (e.g., "." for current package)
        if (Current.Type == TokenType.Identifier)
        {
            var dottedName = ParseDottedName();
            return leadingDots.ToString() + dottedName;
        }

        // If we have leading dots, that's a valid relative import (e.g., "from . import something")
        if (leadingDots.Length > 0)
        {
            return leadingDots.ToString();
        }

        // No dots and no identifier means invalid syntax (e.g., "from import x")
        throw new ParserError("Expected module name", Current.Line, Current.Column);
    }

    private List<Statement> ParseBlock()
    {
        var statements = new List<Statement>();

        while (Current.Type != TokenType.Dedent && !IsAtEnd)
        {
            SkipNewlines();
            if (Current.Type == TokenType.Dedent || IsAtEnd)
                break;

            statements.Add(ParseStatement());
            SkipNewlines();
        }

        return statements;
    }

    private List<Parameter> ParseParameters()
    {
        var parameters = new List<Parameter>();
        var hasVariadic = false;

        if (Current.Type == TokenType.RightParen)
            return parameters;

        do
        {
            var startLine = Current.Line;
            var startColumn = Current.Column;
            var startToken = Current;

            // Check for variadic parameter (*args)
            var isVariadic = false;
            if (Current.Type == TokenType.Star)
            {
                if (hasVariadic)
                    throw new ParserError("Only one variadic parameter (*args) is allowed per function", Current.Line, Current.Column);
                isVariadic = true;
                hasVariadic = true;
                Advance();  // Skip *
            }

            var name = ExpectIdentifier();
            TypeAnnotation? type = null;
            Expression? defaultValue = null;

            if (Current.Type == TokenType.Colon)
            {
                Advance();
                type = ParseTypeAnnotation();
            }

            if (Current.Type == TokenType.Assign)
            {
                if (isVariadic)
                    throw new ParserError("Variadic parameter (*args) cannot have a default value", Current.Line, Current.Column);
                Advance();
                defaultValue = ParseExpression();
            }

            var endToken = Previous;
            var endLine = endToken.Line;
            var endColumn = endToken.Column + endToken.Value.Length;

            parameters.Add(new Parameter
            {
                Name = name,
                Type = type,
                DefaultValue = defaultValue,
                IsVariadic = isVariadic,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = endLine,
                ColumnEnd = endColumn,
                Span = GetSpanFromTokens(startToken, endToken)
            });

            if (Current.Type == TokenType.Comma)
            {
                if (isVariadic)
                    throw new ParserError("Variadic parameter (*args) must be the last parameter", Current.Line, Current.Column);
                Advance();
                // Allow trailing comma: def foo(a, b, c,):
                if (Current.Type == TokenType.RightParen)
                    break;
            }
            else
                break;
        } while (true);

        return parameters;
    }
}
