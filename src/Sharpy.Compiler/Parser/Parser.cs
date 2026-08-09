using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parses Sharpy tokens into an Abstract Syntax Tree (AST).
/// Implements recursive descent parsing with operator precedence.
/// </summary>
public partial class Parser
{
    /// <summary>
    /// Lightweight sentinel exception for unwinding the parser call stack.
    /// The diagnostic is recorded into DiagnosticBag before this is thrown.
    /// </summary>
    private class ParserAbortException : Exception { }

    /// <summary>
    /// Record a parser error into diagnostics and return an exception for stack unwinding.
    /// Usage: <c>throw ReportError("msg", line, col, code);</c>
    /// When a token is available, pass its span for rich error rendering with underlines.
    /// </summary>
    private ParserAbortException ReportError(string message, int line, int column, string code,
        Text.TextSpan? span = null)
    {
        _diagnostics.AddError(message, span, line, column, code: code, phase: CompilerPhase.Parser);
        return new ParserAbortException();
    }

    private readonly List<Token> _tokens;
    private int _position;
    private readonly ICompilerLogger _logger;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Counter for periodic cancellation checks in loops.
    /// Checking every iteration would be expensive, so we check every N iterations.
    /// </summary>
    private int _cancellationCheckCounter;
    private const int CancellationCheckInterval = 1000;

    /// <summary>
    /// Tracks parser position for loop progress detection.
    /// Used by CheckLoopProgress() to detect and break infinite loops.
    /// </summary>
    private int _lastLoopPosition = -1;

    /// <summary>
    /// Tracks expression recursion depth to prevent StackOverflowException
    /// on deeply nested expressions (e.g., 500 nested parentheses).
    /// </summary>
    private int _recursionDepth;
    private const int MaxRecursionDepth = 120;

    /// <summary>
    /// Diagnostics collected during parsing.
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Flag indicating whether we're currently parsing inside an interface definition.
    /// When true, interface methods can omit the colon and body (e.g., `def foo(self) -> str`).
    /// </summary>
    private bool _parsingInterface;

    /// <summary>
    /// True while parsing a direct subscript-element expression (a bound of <c>x[...]</c>),
    /// and only until we descend below the element's <c>test</c> level. Consulted by
    /// <see cref="ParseLambda"/> to suppress the parameter-annotation heuristic: directly
    /// inside a subscript, CPython reads any unparenthesized <c>lambda p: X: Y</c> as the
    /// slice <c>slice(start=lambda p: X, stop=Y)</c>, never as an annotated-parameter lambda,
    /// so the first <c>:</c> is always the body separator (#1130). Set in
    /// <see cref="ParseSliceOrIndex"/> and cleared in <see cref="ParsePostfix"/> (every
    /// grouping/call descends through it); a nested subscript re-establishes it. Saved and
    /// restored at each boundary so sibling dimensions and nested subscripts don't corrupt it.
    /// </summary>
    private bool _inSubscriptElement;

    /// <summary>
    /// Decorators parsed for the current definition being processed.
    /// Set by ParseDecoratedStatement() before dispatching to the definition parser,
    /// so that ParseFunctionDef() can check for @abstract to allow body-less syntax.
    /// Reset to empty in a finally block after the definition is parsed.
    /// </summary>
    private ImmutableArray<Decorator> _pendingDecorators = ImmutableArray<Decorator>.Empty;

    /// <summary>
    /// Trailing trivia captured from the colon token of compound statements (def, class, if, etc.).
    /// Saved/restored per ParseStatement() call to handle recursion correctly.
    /// </summary>
    private IReadOnlyList<Trivia>? _headerTrailingTrivia;
    private bool _headerTriviaCaptured;

    /// <summary>
    /// Enabled experimental feature flags. Carried for parser-scoped feature gates
    /// (roadmap C2); no parser feature is gated yet, but the flags are threaded here so
    /// future gated syntax can consult them without another plumbing change.
    /// </summary>
    private readonly Shared.FeatureFlags _features;

    /// <summary>The feature flags in effect for this parse.</summary>
    internal Shared.FeatureFlags Features => _features;

    public Parser(List<Token> tokens, ICompilerLogger? logger = null, int maxErrors = 25, CancellationToken cancellationToken = default,
        Shared.FeatureFlags? features = null)
    {
        _tokens = tokens;
        _position = 0;
        _logger = logger ?? NullLogger.Instance;
        _maxErrors = maxErrors > 0 ? maxErrors : 25;
        _cancellationToken = cancellationToken;
        _features = features ?? Shared.FeatureFlags.None;
        _logger.LogInfo($"Parser initialized, token count: {tokens.Count}");
    }

    private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];
    private Token Previous => _position > 0 ? _tokens[Math.Min(_position - 1, _tokens.Count - 1)] : _tokens[0];
    private Token Peek(int offset = 1)
    {
        var index = _position + offset;
        return index >= 0 && index < _tokens.Count ? _tokens[index] : _tokens[^1];
    }
    private bool IsAtEnd => Current.Type == TokenType.Eof;

    /// <summary>
    /// Determines whether <c>match</c> at the current position is a soft-keyword
    /// function call (e.g. <c>match(args)</c>) rather than a match statement/expression.
    /// Returns true when <c>match</c> is followed by <c>(</c> and the matching <c>)</c>
    /// is NOT followed by <c>:</c> — i.e., it's a call, not <c>match (scrutinee):</c>.
    /// </summary>
    private bool IsMatchSoftKeywordCall()
    {
        if (Peek().Type != TokenType.LeftParen)
            return false;

        int depth = 1;
        int i = 2;
        for (; ; i++)
        {
            var type = Peek(i).Type;
            if (type == TokenType.Eof)
                return true;
            if (type == TokenType.LeftParen)
                depth++;
            else if (type == TokenType.RightParen && --depth == 0)
                break;
        }

        // A match STATEMENT's header ends in ':' at bracket depth zero; a call statement's does
        // not. Testing the token immediately after the closing ')' was too eager — it made the
        // subject's parentheses exclusive with any trailing syntax, so `match (x) as! int:`,
        // `match (b.get)():` and `match (x)[0]:` all fell through to expression-statement parsing
        // and surfaced as SPY0107 on the colon (#1349). Scanning the rest of the logical line
        // keeps `match(a, b)` a call while letting a parenthesized subject be a subject.
        for (i++; ; i++)
        {
            switch (Peek(i).Type)
            {
                case TokenType.Eof:
                case TokenType.Newline:
                    return true;
                case TokenType.Colon when depth == 0:
                    return false;
                case TokenType.LeftParen:
                case TokenType.LeftBracket:
                case TokenType.LeftBrace:
                    depth++;
                    break;
                case TokenType.RightParen:
                case TokenType.RightBracket:
                case TokenType.RightBrace:
                    depth--;
                    break;
            }
        }
    }

    /// <summary>
    /// Lambda-parameter annotation disambiguation (#899): when a candidate type
    /// name in lambda-parameter position is followed by '[', determine whether the
    /// bracketed section is generic type arguments (`lambda t: list[int]: ...`) or a
    /// subscript in the lambda body (`lambda t: t[0]`).
    /// </summary>
    /// <remarks>
    /// Assumes the type name is at <c>Peek(1)</c> and the opening '[' is at
    /// <c>Peek(2)</c>. Scans forward counting '['/']' depth to the matching ']', then
    /// classifies the token after it. For '=', ':', '?', '!' the decision is unambiguous
    /// (single-token): the bracketed section is a generic type annotation. A ',' is
    /// ambiguous — a generic type of a multi-param lambda (<c>lambda x: list[int], y: int: …</c>)
    /// vs. an enclosing call/tuple separator after a subscript body
    /// (<c>first(lambda t: t[0], xs)</c>) — so it delegates to
    /// <see cref="LambdaColonStartsTypeAnnotation"/> (#1015). If EOF or a
    /// newline is reached before the brackets balance, returns false (not an
    /// annotation) so the construct is parsed as a body expression. Only bracket
    /// depth is tracked — parentheses inside the brackets (no current type syntax
    /// produces them) would not confuse the bracket balance but a ']' inside a
    /// parenthesized expression counts toward it; extend to track paren depth if
    /// such type syntax is ever added.
    /// </remarks>
    private bool IsBracketedTypeAnnotation()
    {
        int depth = 1;
        for (int i = 3; ; i++)
        {
            var type = Peek(i).Type;
            if (type is TokenType.Eof or TokenType.Newline)
                return false;
            if (type == TokenType.LeftBracket)
            {
                depth++;
            }
            else if (type == TokenType.RightBracket)
            {
                depth--;
                if (depth == 0)
                {
                    var after = Peek(i + 1).Type;
                    // A ',' after the matching ']' is ambiguous: it can end a generic type
                    // annotation of a multi-param lambda (lambda x: list[int], y: int: body) OR
                    // be the enclosing call/tuple separator after a subscript body
                    // (first(lambda t: t[0], xs)). Defer to the #1011 forward scan, which tracks
                    // bracket depth and accepts only when a lambda body-separator ':' is reachable
                    // before a terminator. The other terminators ('=', ':', '?', '!') are
                    // unambiguous after a generic type. (#1015)
                    if (after == TokenType.Comma)
                        return LambdaColonStartsTypeAnnotation();
                    return after is TokenType.Assign or TokenType.Colon
                        or TokenType.Question or TokenType.Bang;
                }
            }
        }
    }

    /// <summary>
    /// Lambda-parameter annotation disambiguation (#1011): resolves the ambiguous
    /// <c>: IDENT ,</c> pattern after a lambda parameter name. The ':' may introduce a
    /// type annotation of a multi-parameter lambda
    /// (<c>lambda x: int, y: int: x + y</c>), or it may be the body separator of a lambda
    /// whose body is a bare identifier terminated by the comma inside an enclosing
    /// call/tuple (<c>apply(lambda v: v, 5)</c>).
    /// </summary>
    /// <remarks>
    /// Assumes <see cref="Current"/> is the candidate parameter ':' and <c>Peek(1)</c> is the
    /// type-start token (<c>Identifier</c>/<c>Auto</c>/<c>None</c>). Invoked for the
    /// <c>: IDENT ,</c> case and, via <see cref="IsBracketedTypeAnnotation"/> (#1015), the
    /// <c>: IDENT [ … ] ,</c> case. Returns <see langword="true"/> iff
    /// the ':' is a type annotation, determined by scanning ahead and simulating the
    /// parameter-list grammar <c>TYPE (',' PARAM)* ':'</c> (where
    /// <c>PARAM = name [':' TYPE] ['=' default]</c>) to find a lambda body-separator ':'
    /// before a lambda terminator. A bare <c>name: expr</c> is not a valid standalone call
    /// argument or tuple element, so the only valid parse that reaches a body ':' is the
    /// typed-lambda interpretation. Tracks '()[]{}' bracket depth; only depth-0 tokens are
    /// structural. EOF/Newline or a depth-0 ')'/']'/'}' is a terminator (no body ':' ahead
    /// → not an annotation).
    ///
    /// A nested <c>lambda</c> used as a kwarg value or parameter default is skipped whole by
    /// <c>SkipLambdaHeader</c> so its own body ':' is not mistaken for the outer lambda's body
    /// separator (<c>f(lambda v: v, key=lambda x: x)</c>) (#1076).
    ///
    /// Residual limitation: the nested-lambda skip consumes only the first depth-0 ':', so a
    /// <em>typed</em> nested-lambda parameter used as a kwarg value (<c>k=lambda x: int: y</c>)
    /// still stops at the parameter ':' rather than the body ':'. This is an extreme edge with
    /// no reported occurrence; all untyped nested lambdas resolve correctly.
    /// </remarks>
    private bool LambdaColonStartsTypeAnnotation()
    {
        // Scan a balanced expression starting at `pos` (offset from Current) until the next
        // depth-0 delimiter, advancing `pos`. Returns: 'b' if a body-separator ':' is reached,
        // ',' for a depth-0 comma, '=' for a depth-0 '=' (type mode only), 't' for a terminator
        // or an empty expression.
        // Skip a nested lambda's header — the `lambda` keyword, its parameter list (with any
        // commas / bracketed defaults), and its body-separator ':' — leaving `pos` at the first
        // token of the lambda body. Without this, a nested lambda used as a kwarg value or
        // default (`k=lambda: x`, `default=lambda a, b: c`) leaks its depth-0 ':' and commas
        // into the outer parameter-list simulation, so the inner body ':' is mistaken for the
        // outer lambda's body separator and the parse fails (#1076). Only the first depth-0 ':'
        // is consumed, which is correct for untyped nested params (the overwhelmingly common
        // case and every reported repro); a *typed* nested-lambda parameter in a kwarg value
        // (`k=lambda x: int: y`) would still stop at the parameter ':' — an accepted limitation.
        void SkipLambdaHeader(ref int pos)
        {
            pos++; // consume 'lambda'
            int d = 0;
            while (true)
            {
                var type = Peek(pos).Type;
                if (type == TokenType.Eof)
                    return;
                if (d == 0)
                {
                    if (type == TokenType.Colon)
                    {
                        pos++; // consume the body separator
                        return;
                    }
                    if (type is TokenType.Newline
                        or TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace)
                        return; // malformed lambda header — bail without consuming the delimiter
                }
                if (type is TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace)
                    d++;
                else if (type is TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace)
                    d--;
                pos++;
            }
        }

        char ScanExpr(ref int pos, bool typeMode)
        {
            int depth = 0;
            bool consumed = false;
            while (true)
            {
                var type = Peek(pos).Type;
                // Eof terminates at any depth — Peek() saturates at the final Eof token, so an
                // unbalanced open bracket would otherwise spin here forever at depth > 0.
                if (type == TokenType.Eof)
                    return 't';
                if (depth == 0)
                {
                    // A nested lambda owns its colon(s): skip its whole header so the inner body
                    // ':' is not read as the outer lambda's body separator (#1076).
                    if (type == TokenType.Lambda)
                    {
                        SkipLambdaHeader(ref pos);
                        consumed = true;
                        continue;
                    }
                    if (type is TokenType.Newline
                        or TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace)
                        return 't';
                    if (type == TokenType.Colon)
                        return 'b';
                    if (type == TokenType.Comma)
                        return consumed ? ',' : 't';
                    if (typeMode && type == TokenType.Assign)
                        return consumed ? '=' : 't';
                }
                if (type is TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace)
                    depth++;
                else if (type is TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace)
                    depth--;
                consumed = true;
                pos++;
            }
        }

        int i = 1; // offset from Current (the candidate ':'); start at the candidate TYPE.
        var d = ScanExpr(ref i, typeMode: true);
        while (true)
        {
            switch (d)
            {
                case 'b':
                    return true;  // body separator ahead → the ':' is a type annotation
                case '=':
                    i++;          // consume '='; scan the default value
                    d = ScanExpr(ref i, typeMode: false);
                    continue;
                case ',':
                    i++;          // consume ','; the next parameter must be an identifier
                    if (Peek(i).Type != TokenType.Identifier)
                        return false;
                    i++;          // consume the parameter name
                    var afterName = Peek(i).Type;
                    if (afterName == TokenType.Colon)
                        return true;  // a body separator (or this param's type ':') exists ahead
                    if (afterName == TokenType.Assign)
                    {
                        i++;      // consume '='; scan the default value
                        d = ScanExpr(ref i, typeMode: false);
                        continue;
                    }
                    if (afterName == TokenType.Comma)
                    {
                        d = ','; // bare parameter; handle the next ',' on the following iteration
                        continue;
                    }
                    return false; // name followed by a token that cannot continue a parameter list
                default:          // 't' terminator / empty expression
                    return false;
            }
        }
    }

    /// <summary>
    /// Checks whether a keyword token type can be used as an identifier in
    /// certain contexts (e.g., keyword arguments in function calls).
    /// These correspond to Python's soft keywords: <c>match</c>, <c>case</c>, and <c>type</c>.
    /// </summary>
    private static bool IsKeywordUsableAsIdentifier(TokenType type) => type is
        TokenType.Match or TokenType.Case or TokenType.Type;

    /// <summary>
    /// Gets the TextSpan of the current token, or null if position tracking is unavailable.
    /// Convenience accessor for use in ReportError calls.
    /// </summary>
    private Text.TextSpan? CurrentSpan => Current.GetSpan();

    /// <summary>
    /// Maximum number of parser errors before aborting.
    /// With panic-mode recovery at both module and block level, errors are more
    /// independent and less likely to cascade, so a higher limit is practical.
    /// </summary>
    private readonly int _maxErrors = 25;

    /// <summary>
    /// Parse the entire module
    /// </summary>
    public Module ParseModule()
    {
        _logger.LogInfo("Starting module parsing");
        var startTime = System.Diagnostics.Stopwatch.StartNew();

        var statements = new List<Statement>();

        // Skip leading newlines
        SkipNewlines();

        _lastLoopPosition = -1;
        while (!IsAtEnd)
        {
            if (!CheckLoopProgress())
                break;

            SkipNewlines();
            if (IsAtEnd)
                break;

            try
            {
                var stmt = ParseStatement();
                statements.Add(stmt);
            }
            catch (ParserAbortException)
            {
                // Error already recorded in _diagnostics by ReportError()

                // Stop after _maxErrors to avoid cascading false errors
                if (_diagnostics.ErrorCount >= _maxErrors)
                {
                    _diagnostics.AddWarning(
                        $"Too many errors ({_maxErrors}); further errors suppressed. Use '--max-errors' to increase the limit.",
                        Current.Line, Current.Column,
                        code: DiagnosticCodes.Infrastructure.TooManyErrors,
                        phase: CompilerPhase.Parser);
                    break;
                }

                // Panic-mode recovery: synchronize to next statement boundary
                Synchronize();
            }
            SkipNewlines();
        }

        var docString = TakeDocString(statements);

        _logger.LogInfo($"Module parsing completed in {startTime.ElapsedMilliseconds}ms, {statements.Count} statements");

        return new Module
        {
            Body = statements.ToImmutableArray(),
            DocString = docString,
            LineStart = 1,
            ColumnStart = 1,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column,
            Span = GetSpanFromTokens(_tokens[0], Current)
        };
    }

    /// <summary>
    /// Parses a single statement from the token stream.
    /// Callers must lex source text into tokens first via <see cref="Lexer.Lexer"/>.
    /// Parse errors accumulate in <see cref="Diagnostics"/>.
    /// </summary>
    public Statement ParseSingleStatement()
    {
        SkipNewlines();
        try
        {
            return ParseStatement();
        }
        catch (ParserAbortException)
        {
            return new ExpressionStatement
            {
                Expression = new Identifier
                {
                    Name = "<error>",
                    LineStart = Current.Line,
                    ColumnStart = Current.Column,
                    LineEnd = Current.Line,
                    ColumnEnd = Current.Column
                },
                LineStart = Current.Line,
                ColumnStart = Current.Column,
                LineEnd = Current.Line,
                ColumnEnd = Current.Column
            };
        }
    }

    /// <summary>
    /// Parses all statements until EOF, without wrapping in a <see cref="Module"/> node.
    /// Useful for REPL multi-line input where a full module is not needed.
    /// Callers must lex source text into tokens first via <see cref="Lexer.Lexer"/>.
    /// Parse errors accumulate in <see cref="Diagnostics"/>.
    /// </summary>
    public IReadOnlyList<Statement> ParseStatements()
    {
        var statements = new List<Statement>();
        SkipNewlines();

        _lastLoopPosition = -1;
        while (!IsAtEnd)
        {
            if (!CheckLoopProgress())
                break;

            SkipNewlines();
            if (IsAtEnd)
                break;

            try
            {
                var stmt = ParseStatement();
                statements.Add(stmt);
            }
            catch (ParserAbortException)
            {
                if (_diagnostics.ErrorCount >= _maxErrors)
                    break;
                Synchronize();
            }
            SkipNewlines();
        }

        return statements;
    }

    /// <summary>
    /// Checks if parser position advanced since last call within a loop.
    /// If no progress was made, forces an advance and returns false to signal
    /// the loop should break. This prevents infinite loops on malformed input.
    /// Also periodically checks for cancellation to support timeout-based interruption.
    ///
    /// Usage pattern:
    /// <code>
    /// _lastLoopPosition = -1;
    /// do {
    ///     if (!CheckLoopProgress()) break;
    ///     // ... parsing logic ...
    /// } while (condition);
    /// </code>
    /// </summary>
    private bool CheckLoopProgress([CallerMemberName] string? callerName = null)
    {
        // Periodically check for cancellation (every N iterations to minimize overhead)
        if (++_cancellationCheckCounter >= CancellationCheckInterval)
        {
            _cancellationCheckCounter = 0;
            _cancellationToken.ThrowIfCancellationRequested();
        }

        if (_position == _lastLoopPosition)
        {
            // No progress - force advance to break potential infinite loop
            _diagnostics.AddWarning(
                $"Parser loop stall in {callerName} at position {_position} " +
                $"(token: {Current.Type} '{TruncateValue(Current.Value, 20)}' " +
                $"at line {Current.Line}:{Current.Column})",
                Current.Line, Current.Column,
                code: DiagnosticCodes.Infrastructure.ParserLoopStall,
                phase: CompilerPhase.Parser);

            if (!IsAtEnd)
                Advance();

            return false;
        }
        _lastLoopPosition = _position;
        return true;
    }

    /// <summary>
    /// Checks if _pendingDecorators contains a decorator named "abstract".
    /// Avoids LINQ to prevent ImmutableArray boxing allocation.
    /// </summary>
    private bool HasAbstractDecorator()
    {
        foreach (var decorator in _pendingDecorators)
        {
            if (decorator.Name == "abstract")
                return true;
        }
        return false;
    }

    /// <summary>
    /// Truncates a string value for diagnostic display.
    /// </summary>
    private static string TruncateValue(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= maxLength)
            return value;
        return value.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Panic-mode recovery: skip tokens until a statement boundary is reached.
    /// Also handles skipping over indented blocks that belong to broken definitions
    /// (e.g., a malformed function header followed by its indented body).
    /// </summary>
    private void Synchronize()
    {
        // If we're already at a statement boundary and the current token starts
        // a new statement, return immediately so the parser can try it.
        // This handles the case where e.g. Expect(TokenType.Indent) fails right
        // after a newline -- the next definition's keyword is already Current.
        if ((Previous.Type == TokenType.Newline || Previous.Type == TokenType.Dedent)
            && IsSyncToken(Current.Type))
        {
            return;
        }

        // Special case: if we're at an unexpected INDENT at module level (e.g., "    x = 1"),
        // skip the entire indented block to avoid infinite loops.
        if (Current.Type == TokenType.Indent)
        {
            SkipIndentedBlock();
            return;
        }

        // Advance past the token that caused the error to guarantee progress.
        // Without this, errors where Current is not a sync token and Previous
        // is a boundary would cause Synchronize to return immediately at the
        // same position, leading to repeated identical errors.
        Advance();

        while (!IsAtEnd)
        {
            // If we just passed a newline or dedent, we're at a statement boundary
            if (Previous.Type == TokenType.Newline || Previous.Type == TokenType.Dedent)
            {
                // If we're now at an INDENT, skip the entire indented block.
                // This handles broken definitions (e.g., `def 123():`) where
                // the body should be skipped entirely to reach the next definition.
                if (Current.Type == TokenType.Indent)
                {
                    SkipIndentedBlock();
                    continue;
                }
                return;
            }

            // These tokens begin a new statement
            if (IsSyncToken(Current.Type))
                return;

            Advance();
        }
    }

    /// <summary>
    /// Returns true if the given token type marks the start of a new statement,
    /// making it a valid synchronization point for error recovery.
    /// </summary>
    private static bool IsSyncToken(TokenType type) => type switch
    {
        TokenType.Def => true,
        TokenType.Class => true,
        TokenType.Struct => true,
        TokenType.Interface => true,
        TokenType.Enum => true,
        TokenType.Delegate => true,
        TokenType.If => true,
        TokenType.While => true,
        TokenType.For => true,
        TokenType.Return => true,
        TokenType.Import => true,
        TokenType.From => true,
        TokenType.Raise => true,
        TokenType.Assert => true,
        TokenType.Pass => true,
        TokenType.Break => true,
        TokenType.Continue => true,
        TokenType.Const => true,
        TokenType.Property => true,
        TokenType.Event => true,
        TokenType.Type => true,
        TokenType.Try => true,
        TokenType.With => true,
        TokenType.Defer => true,
        TokenType.Maybe => true,
        TokenType.Match => true,
        TokenType.Async => true,
        TokenType.At => true,
        TokenType.Dedent => true,
        _ => false,
    };

    /// <summary>
    /// Skip past an entire indented block by tracking INDENT/DEDENT nesting depth.
    /// Used during error recovery to skip the body of a broken definition.
    /// </summary>
    private void SkipIndentedBlock()
    {
        int depth = 0;
        while (!IsAtEnd)
        {
            if (Current.Type == TokenType.Indent)
                depth++;
            else if (Current.Type == TokenType.Dedent)
            {
                depth--;
                if (depth <= 0)
                {
                    Advance(); // consume the matching DEDENT
                    return;
                }
            }
            Advance();
        }
    }

    private Statement ParseStatement()
    {
        var leadingTrivia = Current.LeadingTrivia;

        var savedHeaderTrivia = _headerTrailingTrivia;
        var savedHeaderCaptured = _headerTriviaCaptured;
        _headerTrailingTrivia = null;
        _headerTriviaCaptured = false;

        var stmt = ParseStatementCore();

        var trailingTrivia = _headerTrailingTrivia;
        _headerTrailingTrivia = savedHeaderTrivia;
        _headerTriviaCaptured = savedHeaderCaptured;

        if (trailingTrivia == null)
        {
            for (int i = Math.Min(_position - 1, _tokens.Count - 1); i >= 0; i--)
            {
                var t = _tokens[i];
                if (t.Type is TokenType.Newline or TokenType.Indent or TokenType.Dedent)
                    continue;
                trailingTrivia = t.TrailingTrivia;
                break;
            }
        }

        if (leadingTrivia != null || trailingTrivia != null)
        {
            stmt = stmt with
            {
                LeadingTrivia = leadingTrivia ?? stmt.LeadingTrivia,
                TrailingTrivia = trailingTrivia ?? stmt.TrailingTrivia
            };
        }

        return stmt;
    }

    private Statement ParseStatementCore()
    {
        // Decorators (for functions, classes, structs)
        if (Current.Type == TokenType.At)
            return ParseDecoratedStatement();

        // A keyword immediately followed by '.' is an identifier qualifier, not the keyword itself
        // (e.g. bare `struct.pack(...)`, `enum.Enum`, `match.group()`). Route it to expression
        // parsing before the keyword-specific dispatch below claims it (#1091). Placed ahead of the
        // try/async/match disambiguations so soft-keyword member access (`match.group()`) works too.
        if (IsKeywordQualifierStart())
            return ParseSimpleStatement();

        // Special handling for 'try' - it can be either a statement (try:) or an expression (try expr)
        // Disambiguate by looking ahead: try statement has 'try:' while try expression has 'try expr'
        if (Current.Type == TokenType.Try)
        {
            // Check if next token is ':' (try statement) or '[' for try[Type] expression
            // or something else (try expression)
            if (Peek().Type == TokenType.Colon)
                return ParseTryStatement();
            // Otherwise it's a try expression, fall through to ParseSimpleStatement
            return ParseSimpleStatement();
        }

        // Special handling for 'maybe' - it's always an expression at statement level
        if (Current.Type == TokenType.Maybe)
            return ParseSimpleStatement();

        // Handle async keyword: peek ahead to determine if it's async def, async for, or async with
        if (Current.Type == TokenType.Async)
            return ParseAsyncStatement();

        // Rejected Python keywords with helpful error messages
        if (Current.Type == TokenType.Global)
            throw ReportError("Sharpy does not support 'global' — C# scoping rules apply (Axiom 1)", Current.Line, Current.Column, DiagnosticCodes.Parser.RejectedPythonKeyword, span: CurrentSpan);
        if (Current.Type == TokenType.Nonlocal)
            throw ReportError("Sharpy does not support 'nonlocal' — C# scoping rules apply (Axiom 1)", Current.Line, Current.Column, DiagnosticCodes.Parser.RejectedPythonKeyword, span: CurrentSpan);

        if (Current.Type == TokenType.Match && !IsMatchSoftKeywordCall())
            return ParseMatchStatement();

        return Current.Type switch
        {
            TokenType.Def => ParseFunctionDef(),
            TokenType.Class => ParseClassDef(),
            TokenType.Struct => ParseStructDef(),
            TokenType.Interface => ParseInterfaceDef(),
            TokenType.Enum => ParseEnumDef(),
            TokenType.Union => ParseUnionDef(),
            TokenType.Delegate => ParseDelegateDef(),
            TokenType.Property => ParsePropertyDef(),
            TokenType.Event => ParseEventDef(),
            TokenType.Type => ParseTypeAlias(),
            TokenType.If => ParseIfStatement(),
            TokenType.While => ParseWhileStatement(),
            TokenType.For => ParseForStatement(),
            TokenType.With => ParseWithStatement(),
            TokenType.Defer => ParseDeferStatement(),
            TokenType.Return => ParseReturnStatement(),
            TokenType.Yield => ParseYieldStatement(),
            TokenType.Raise => ParseRaiseStatement(),
            TokenType.Assert => ParseAssertStatement(),
            TokenType.Pass => ParsePassStatement(),
            TokenType.Break => ParseBreakStatement(),
            TokenType.Continue => ParseContinueStatement(),
            TokenType.Import => ParseImportStatement(),
            TokenType.From => ParseFromImportStatement(),
            TokenType.Const => ParseConstDeclaration(),
            _ => ParseSimpleStatement()
        };
    }

    private Statement ParseDecoratedStatement()
    {
        var decorators = new List<Decorator>();

        while (Current.Type == TokenType.At)
        {
            var decoratorStartLine = Current.Line;
            var decoratorStartColumn = Current.Column;
            var decoratorStartToken = Current;
            Advance();  // Skip @

            // Branch: @[attribute_name] bracket attribute syntax
            if (Current.Type == TokenType.LeftBracket)
            {
                Advance();  // Skip [
                if (Current.Type != TokenType.Identifier)
                    throw ReportError("Expected attribute name after '@['", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedDecoratorName, span: CurrentSpan);

                var qualifiedParts = new List<string> { Current.Value };
                var backtickParts = new List<bool> { Current.IsBacktickEscaped };
                Advance();

                while (Current.Type == TokenType.Dot)
                {
                    Advance();  // Skip .
                    if (Current.Type != TokenType.Identifier)
                        throw ReportError("Expected identifier after '.' in attribute name", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedDecoratorName, span: CurrentSpan);
                    qualifiedParts.Add(Current.Value);
                    backtickParts.Add(Current.IsBacktickEscaped);
                    Advance();
                }

                var arguments = ImmutableArray<Expression>.Empty;
                var keywordArguments = ImmutableArray<KeywordArgument>.Empty;
                if (Current.Type == TokenType.LeftParen)
                {
                    Advance();  // Skip (
                    var (args, kwargs) = ParseCallArguments();
                    arguments = args.ToImmutableArray();
                    keywordArguments = kwargs.ToImmutableArray();
                }

                if (Current.Type != TokenType.RightBracket)
                    throw ReportError("Expected ']' to close bracket attribute", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedToken, span: CurrentSpan);
                Advance();  // Skip ]

                var decoratorEndToken = Previous;
                decorators.Add(new Decorator
                {
                    Arguments = arguments,
                    KeywordArguments = keywordArguments,
                    QualifiedParts = qualifiedParts.ToImmutableArray(),
                    BacktickEscapedParts = backtickParts.ToImmutableArray(),
                    IsBracketAttribute = true,
                    LineStart = decoratorStartLine,
                    ColumnStart = decoratorStartColumn,
                    LineEnd = decoratorEndToken.Line,
                    ColumnEnd = decoratorEndToken.Column + decoratorEndToken.Value.Length,
                    Span = GetSpanFromTokens(decoratorStartToken, decoratorEndToken)
                });
                // Mid-construct, not statement-final: a decorator is a prefix, so the statement it
                // decorates must still follow. Dedent or EOF here is a decorator with no target.
                ExpectNewline();
                continue;
            }

            if (Current.Type != TokenType.Identifier)
                throw ReportError("Expected decorator name", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedDecoratorName, span: CurrentSpan);

            var regularParts = new List<string> { Current.Value };
            Advance();

            // Parse dotted names: @system.serializable, @system.runtime.interop_services.dll_import
            while (Current.Type == TokenType.Dot)
            {
                Advance();  // Skip .
                if (Current.Type != TokenType.Identifier)
                    throw ReportError("Expected identifier after '.' in decorator name", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedDecoratorName, span: CurrentSpan);
                regularParts.Add(Current.Value);
                Advance();
            }

            {
                // Parse optional argument list: @decorator(args)
                var arguments = ImmutableArray<Expression>.Empty;
                var keywordArguments = ImmutableArray<KeywordArgument>.Empty;
                if (Current.Type == TokenType.LeftParen)
                {
                    Advance();  // Skip (
                    var (args, kwargs) = ParseCallArguments();
                    arguments = args.ToImmutableArray();
                    keywordArguments = kwargs.ToImmutableArray();
                }

                var decoratorEndToken = Previous;
                var decoratorEndLine = decoratorEndToken.Line;
                var decoratorEndColumn = decoratorEndToken.Column + decoratorEndToken.Value.Length;

                decorators.Add(new Decorator
                {
                    Arguments = arguments,
                    KeywordArguments = keywordArguments,
                    QualifiedParts = regularParts.ToImmutableArray(),
                    LineStart = decoratorStartLine,
                    ColumnStart = decoratorStartColumn,
                    LineEnd = decoratorEndLine,
                    ColumnEnd = decoratorEndColumn,
                    Span = GetSpanFromTokens(decoratorStartToken, decoratorEndToken)
                });
            }
            // Mid-construct: see the bracket-attribute arm above — the decorated statement follows.
            ExpectNewline();
        }

        // Make decorators available to definition parsers (e.g., ParseFunctionDef checks for @abstract)
        _pendingDecorators = decorators.ToImmutableArray();

        // When every decorator is @suppress, the decorators may scope-suppress an *arbitrary*
        // statement (not just a decoratable definition) — statement-scoped suppression (#1024). Such
        // a statement is parsed normally and wrapped in a DecoratedStatement below. Any other
        // decorator on a non-definition target keeps today's SPY0105 parse error exactly.
        var allSuppress = decorators.Count > 0;
        foreach (var d in decorators)
        {
            if (d.Name != DecoratorNames.Suppress)
            {
                allSuppress = false;
                break;
            }
        }

        Statement stmt;
        try
        {
            // Parse the decorated definition
            stmt = Current.Type switch
            {
                TokenType.Async => ParseAsyncStatement(),
                TokenType.Def => ParseFunctionDef(),
                TokenType.Class => ParseClassDef(),
                TokenType.Struct => ParseStructDef(),
                TokenType.Interface => ParseInterfaceDef(),
                TokenType.Union => ParseUnionDef(),
                TokenType.Enum => ParseEnumDef(),
                TokenType.Property => ParsePropertyDef(),
                TokenType.Event => ParseEventDef(),
                // Allow decorators on variable declarations (e.g., @static field in class body)
                TokenType.Identifier => ParseSimpleStatement(),
                // Statement-scoped @suppress may prefix an import (#1124): parse it normally and
                // wrap it in a DecoratedStatement below. Every module.Body import scan classifies
                // through Statement.UnwrapDecorated, so the wrapped import still registers, resolves,
                // and generates its using directive — the CompilerInvariants conformance test guards
                // that unwrap contract. Non-suppress decorators on imports keep SPY0105 (imports have
                // no CLR attribute target, so any other decorator is meaningless).
                TokenType.Import when allSuppress => ParseImportStatement(),
                TokenType.From when allSuppress => ParseFromImportStatement(),
                TokenType.Import or TokenType.From => throw ReportError("Decorators can only be applied to functions, classes, structs, interfaces, enums, properties, events, or field declarations", Current.Line, Current.Column, DiagnosticCodes.Parser.InvalidDecoratorTarget, span: CurrentSpan),
                // Statement-scoped @suppress may prefix an expression statement or assignment
                // (call-site suppression, e.g. of the must-use SPY0480 warning); it is wrapped below.
                _ when allSuppress => ParseSimpleStatement(),
                _ => throw ReportError("Decorators can only be applied to functions, classes, structs, interfaces, enums, properties, events, or field declarations", Current.Line, Current.Column, DiagnosticCodes.Parser.InvalidDecoratorTarget, span: CurrentSpan)
            };
        }
        finally
        {
            _pendingDecorators = ImmutableArray<Decorator>.Empty;
        }

        // Statement-scoped @suppress: wrap any non-definition target rather than rejecting it.
        if (allSuppress && stmt is not (FunctionDef or ClassDef or StructDef or InterfaceDef
            or UnionDef or EnumDef or PropertyDef or EventDef or VariableDeclaration))
        {
            var decoratorSpan = decorators[0].Span;
            return new DecoratedStatement
            {
                Decorators = decorators.ToImmutableArray(),
                Statement = stmt,
                LineStart = decorators[0].LineStart,
                ColumnStart = decorators[0].ColumnStart,
                LineEnd = stmt.LineEnd,
                ColumnEnd = stmt.ColumnEnd,
                Span = decoratorSpan.HasValue && stmt.Span.HasValue
                    ? CombineSpans(decoratorSpan.Value, stmt.Span.Value)
                    : stmt.Span
            };
        }

        // Attach decorators
        return stmt switch
        {
            FunctionDef func => func with { Decorators = decorators.ToImmutableArray() },
            ClassDef cls => cls with { Decorators = decorators.ToImmutableArray() },
            StructDef str => str with { Decorators = decorators.ToImmutableArray() },
            InterfaceDef iface => iface with { Decorators = decorators.ToImmutableArray() },
            UnionDef union => union with { Decorators = decorators.ToImmutableArray() },
            EnumDef en => en with { Decorators = decorators.ToImmutableArray() },
            PropertyDef prop => prop with { Decorators = decorators.ToImmutableArray() },
            EventDef ev => ev with { Decorators = decorators.ToImmutableArray() },
            VariableDeclaration varDecl => varDecl with { Decorators = decorators.ToImmutableArray() },
            Assignment => throw ReportError("Decorators cannot be applied to assignments — only functions, classes, structs, interfaces, enums, properties, events, or field declarations", stmt.LineStart, stmt.ColumnStart, DiagnosticCodes.Parser.InvalidDecoratorTarget, span: stmt.Span),
            _ => throw ReportError("Decorators can only be applied to functions, classes, structs, interfaces, enums, properties, events, or field declarations", stmt.LineStart, stmt.ColumnStart, DiagnosticCodes.Parser.InvalidDecoratorTarget, span: stmt.Span)
        };
    }

    /// <summary>
    /// Parses a parenthesized argument list: positional args, keyword args, spread elements.
    /// The opening '(' must already be consumed. Consumes through the closing ')'.
    /// Used by both function call parsing and decorator argument parsing.
    /// </summary>
    private (List<Expression> Args, List<KeywordArgument> Kwargs) ParseCallArguments()
    {
        var args = new List<Expression>();
        var kwargs = new List<KeywordArgument>();
        var seenKeywordArg = false;

        if (Current.Type != TokenType.RightParen)
        {
            _lastLoopPosition = -1;
            do
            {
                if (!CheckLoopProgress())
                    break;

                // Check for dict spread argument: **expr
                if (Current.Type == TokenType.DoubleStar)
                {
                    throw ReportError("Dict spread arguments (**expr) in function calls are not yet supported",
                        Current.Line, Current.Column, DiagnosticCodes.Parser.DictSpreadCallNotSupported, span: CurrentSpan);
                }
                // Check for spread argument: *expr
                else if (Current.Type == TokenType.Star)
                {
                    if (seenKeywordArg)
                    {
                        throw ReportError("Spread argument cannot follow keyword argument", Current.Line, Current.Column, DiagnosticCodes.Parser.PositionalAfterKeyword, span: CurrentSpan);
                    }
                    var starToken = Current;
                    Advance();
                    var operand = ParseExpression();
                    args.Add(new SpreadElement
                    {
                        Value = operand,
                        LineStart = starToken.Line,
                        ColumnStart = starToken.Column,
                        LineEnd = operand.LineEnd,
                        ColumnEnd = operand.ColumnEnd,
                        Span = CombineSpans(GetSpanFromToken(starToken), operand.Span)
                    });
                }
                // Check for ref/out/in argument modifier at call site.
                // The peek guard excludes tokens where ref/out would be a plain identifier
                // (e.g., `ref,` passing ref as value, `ref = ...` keyword arg).
                else if ((Current.Type == TokenType.Identifier
                         && (Current.Value == "ref" || Current.Value == "out")
                         && Peek().Type is not (TokenType.Comma or TokenType.RightParen or TokenType.Assign))
                         || (Current.Type == TokenType.In
                         && Peek().Type is not (TokenType.Comma or TokenType.RightParen or TokenType.Assign)))
                {
                    if (seenKeywordArg)
                    {
                        throw ReportError("Positional argument cannot follow keyword argument", Current.Line, Current.Column, DiagnosticCodes.Parser.PositionalAfterKeyword, span: CurrentSpan);
                    }
                    var modToken = Current;
                    var mod = Current.Type == TokenType.In ? Ast.ParameterModifier.In
                        : Current.Value == "ref" ? Ast.ParameterModifier.Ref
                        : Ast.ParameterModifier.Out;
                    Advance();
                    var argExpr = ParseExpression();

                    // Check for inline out declaration: out name: type
                    if (mod == Ast.ParameterModifier.Out
                        && argExpr is Ast.Identifier inlineId
                        && Current.Type == TokenType.Colon)
                    {
                        Advance(); // consume the colon
                        var inlineType = ParseTypeAnnotation();
                        args.Add(new Ast.ModifiedArgument
                        {
                            Modifier = mod,
                            Argument = argExpr,
                            InlineName = inlineId.Name,
                            InlineType = inlineType,
                            LineStart = modToken.Line,
                            ColumnStart = modToken.Column,
                            LineEnd = inlineType.LineEnd,
                            ColumnEnd = inlineType.ColumnEnd,
                            Span = CombineSpans(GetSpanFromToken(modToken), inlineType.Span)
                        });
                    }
                    else
                    {
                        args.Add(new Ast.ModifiedArgument
                        {
                            Modifier = mod,
                            Argument = argExpr,
                            LineStart = modToken.Line,
                            ColumnStart = modToken.Column,
                            LineEnd = argExpr.LineEnd,
                            ColumnEnd = argExpr.ColumnEnd,
                            Span = CombineSpans(GetSpanFromToken(modToken), argExpr.Span)
                        });
                    }
                }
                // Check for keyword argument (also accept soft keywords match/case/type as kwarg names)
                else if ((Current.Type == TokenType.Identifier || IsKeywordUsableAsIdentifier(Current.Type)) && Peek().Type == TokenType.Assign)
                {
                    seenKeywordArg = true;
                    var nameToken = Current;
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
                        ColumnEnd = kwargEndColumn,
                        Span = CombineSpans(GetSpanFromToken(nameToken), value.Span)
                    });
                }
                else
                {
                    if (seenKeywordArg)
                    {
                        throw ReportError("Positional argument cannot follow keyword argument", Current.Line, Current.Column, DiagnosticCodes.Parser.PositionalAfterKeyword, span: CurrentSpan);
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
        return (args, kwargs);
    }

    /// <summary>
    /// Dispatches async statements: async def, async for, async with.
    /// Peeks at the token after 'async' to determine which form to parse.
    /// </summary>
    private Statement ParseAsyncStatement()
    {
        var nextType = Peek().Type;

        if (nextType == TokenType.Def)
            return ParseAsyncFunctionDef();

        if (nextType == TokenType.For)
        {
            var asyncToken = Current;
            Advance(); // consume 'async'
            var forStmt = ParseForStatement();
            return forStmt with
            {
                IsAsync = true,
                LineStart = asyncToken.Line,
                ColumnStart = asyncToken.Column,
                Span = CombineSpans(GetSpanFromToken(asyncToken), forStmt.Span)
                    ?? GetSpanFromToken(asyncToken)
            };
        }

        if (nextType == TokenType.With)
        {
            var asyncToken = Current;
            Advance(); // consume 'async'
            var withStmt = ParseWithStatement();
            return withStmt with
            {
                IsAsync = true,
                LineStart = asyncToken.Line,
                ColumnStart = asyncToken.Column,
                Span = CombineSpans(GetSpanFromToken(asyncToken), withStmt.Span)
                    ?? GetSpanFromToken(asyncToken)
            };
        }

        throw ReportError("'async' must be followed by 'def', 'for', or 'with'",
            Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken, span: CurrentSpan);
    }

}
