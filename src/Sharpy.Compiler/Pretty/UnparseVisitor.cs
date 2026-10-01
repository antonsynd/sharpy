using System.Collections.Immutable;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

internal sealed partial class UnparseVisitor : AstVisitor
{
    private readonly UnparseWriter _w;
    private readonly UnparseOptions _options;

    /// <summary>The one comment mechanism (null: no comments are written).</summary>
    private readonly TriviaCursor? _cursor;

    /// <summary>
    /// The bodies being written, innermost on top: each body's source column (0 for a body with no
    /// indented block of its own — an inline <c>def f(): ...</c>) and the writer level it is
    /// written at. A body's end hands its trailing comments to the CLOSING bodies by column.
    /// </summary>
    private readonly Stack<(int Column, int Level)> _openBodies = new();

    /// <summary>
    /// The decorators <see cref="VisitStatementWithTrivia"/> already wrote, each at its own anchor,
    /// before the statement's header anchor; the statement's visitor skips them.
    /// </summary>
    private ImmutableArray<Decorator> _decoratorsWritten;

    public UnparseVisitor(UnparseWriter writer, UnparseOptions options, TriviaCursor? cursor = null)
    {
        _w = writer;
        _options = options;
        _cursor = cursor;
    }

    public void UnparseModule(Module module)
    {
        if (module.DocString != null)
        {
            // A docstring line is an anchor like a statement's: the comments above it stay above it
            // and its inline comment stays on its closing line.
            WriteDocStringAnchored(0, () => _w.WriteLine($"\"\"\"{EscapeTripleQuoted(module.DocString)}\"\"\""));
        }

        var fmt = _options.Formatting;
        bool hasWrittenNonImport = false;
        bool lastWasImport = false;

        for (int i = 0; i < module.Body.Length; i++)
        {
            var stmt = module.Body[i];
            // Unwrap for classification only (#1124): a suppress-decorated import groups with other
            // imports for blank-line formatting, but VisitStatementWithTrivia below still unparses the
            // original DecoratedStatement so the decorator is preserved in the output.
            bool isImport = stmt.UnwrapDecorated() is ImportStatement or FromImportStatement;
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

        WriteEndOfModule(module);

        if (fmt is { TrailingNewline: true } && _w.Length > 0)
        {
            var text = _w.ToString();
            if (!text.EndsWith(_options.LineEnding))
                _w.WriteLine();
        }
    }

    private static bool IsTopLevelDef(Statement stmt) =>
        stmt is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef or UnionDef or DelegateDef;

    #region Trivia anchors

    /// <summary>
    /// End of module: every comment no anchor claimed. A comment left after a top-level definition
    /// is module-level content and is separated from it like any module-level item (a comment at the
    /// definition's BODY indent was already taken as that body's end).
    /// </summary>
    private void WriteEndOfModule(Module module)
    {
        if (_cursor == null)
            return;
        var rest = _cursor.TakeAll();
        if (_options.Formatting is { } fmt
            && rest.Any(t => t.Kind == TriviaKind.Comment)
            && module.Body.Length > 0
            && IsTopLevelDef(module.Body[module.Body.Length - 1]))
        {
            for (int b = 0; b < fmt.BlankLinesAroundTopLevelDefs; b++)
                _w.WriteLine();
        }
        WriteOwnLine(rest);
    }

    /// <summary>
    /// A statement at its anchor: its decorators each at their own anchor, then its header — the
    /// comments above the header line, the header (verbatim when a comment sits inside it), and
    /// the inline comment ending the header line, appended to the written header. A decorated
    /// definition's header comment therefore stays on the <c>def</c> line, not the decorator's.
    /// </summary>
    private void VisitStatementWithTrivia(Statement stmt)
    {
        if (_cursor == null)
        {
            Visit(stmt);
            return;
        }

        var decorators = DecoratorsOf(stmt);
        if (!decorators.IsDefaultOrEmpty)
        {
            WriteDecorators(decorators);
            _decoratorsWritten = decorators;
        }

        var header = stmt is DecoratedStatement decorated ? decorated.Statement : stmt;
        // A body that starts on the header's own line (`def f(self) -> str: (...)`) has no block of
        // its own: the statement is ONE anchor — header and inline body together — so a comment in
        // the body is an inner comment of the statement and the slice covers the whole statement.
        var sliceEnd = stmt.HeaderEndOffset;
        var wholeStatement = sliceEnd > 0 && stmt.Span is { } span && span.End > sliceEnd
            && _cursor.CodeFollowsOnLine(sliceEnd);
        if (wholeStatement)
            sliceEnd = stmt.Span!.Value.End;
        WriteAnchored(header.LineStart, stmt.HeaderLineEnd, header.Span?.Start ?? -1, sliceEnd, () => Visit(stmt), wholeStatement);
        _decoratorsWritten = default;
    }

    /// <summary>The decorators a statement's visitor writes before its header (<see cref="WriteDecorators"/>).</summary>
    private static ImmutableArray<Decorator> DecoratorsOf(Statement stmt) => stmt switch
    {
        DecoratedStatement s => s.Decorators,
        FunctionDef s => s.Decorators,
        ClassDef s => s.Decorators,
        StructDef s => s.Decorators,
        InterfaceDef s => s.Decorators,
        EnumDef s => s.Decorators,
        UnionDef s => s.Decorators,
        PropertyDef s => s.Decorators,
        EventDef s => s.Decorators,
        VariableDeclaration s => s.Decorators,
        _ => default
    };

    /// <summary>
    /// THE anchor helper: every comment the unparser writes goes through here (or a body's end, or
    /// the module's end). Before the anchor's first line <paramref name="lineStart"/>, the pending
    /// own-line comments (and blank lines, when not formatting) are written as full lines at the
    /// current indent; <paramref name="write"/> writes the anchor; the inline comment ending its
    /// header line <paramref name="headerLineEnd"/> is appended to the written header line. A
    /// comment inside the header's range [<paramref name="lineStart"/>, <paramref name="headerLineEnd"/>]
    /// that is neither (an inner comment) makes the header verbatim: the source slice
    /// [<paramref name="sliceStart"/>, <paramref name="sliceEnd"/>) replaces the written header.
    /// </summary>
    private void WriteAnchored(int lineStart, int headerLineEnd, int sliceStart, int sliceEnd, Action write, bool sliceCoversWrite = false)
    {
        if (_cursor == null || lineStart <= 0)
        {
            write();
            return;
        }

        WriteOwnLine(_cursor.TakeBefore(lineStart));
        // The header's last line: from its end offset when the source is at hand (exact for every
        // statement kind), else the parser's line.
        var endFromSource = sliceEnd > 0 ? _cursor.EndLineOf(sliceEnd) : 0;
        var end = Math.Max(lineStart, endFromSource > 0 ? endFromSource : headerLineEnd);
        var inner = _cursor.TakeInner(lineStart, end);
        var trailing = _cursor.TakeInline(end);

        // Inner comments are consumed by the verbatim slice; only an anchor without a source slice
        // (no source text, or a header the parser did not position) writes them as full lines
        // before it — moved, never dropped, and the net refuses the move.
        var slice = inner.Count > 0 ? VerbatimSlice(lineStart, sliceStart, sliceEnd) : null;
        if (inner.Count > 0 && slice == null)
            WriteOwnLine(inner);

        var position = _w.Length;
        var headerStart = position + (_w.AtLineStart ? _w.IndentWidth : 0);
        write();
        if (slice != null)
            ReplaceHeaderWithSlice(headerStart, slice, sliceCoversWrite);
        if (trailing.Count > 0)
            AppendInline(position, trailing);
    }

    /// <summary>
    /// The header's source slice [<paramref name="sliceStart"/>, <paramref name="sliceEnd"/>) with
    /// its continuation lines re-indented for the header's new indent: each moves by (new indent −
    /// the header's original first column), clamped at column 0, except a line that starts inside a
    /// string literal, whose leading whitespace is the literal's content. Null when the anchor has
    /// no slice. The slice's end is re-derived from the source (<see cref="TriviaCursor.SourceEnd"/>):
    /// the parser's end offset reads a numeric literal's normalised length.
    /// </summary>
    private string? VerbatimSlice(int lineStart, int sliceStart, int sliceEnd)
    {
        if (_cursor is not { HasSource: true } || sliceStart < 0 || sliceEnd <= sliceStart)
            return null;
        var source = _cursor.Source!;
        var end = Math.Min(source.Length, _cursor.SourceEnd(sliceEnd));
        if (end <= sliceStart)
            return null;

        var shift = _w.IndentWidth - _cursor.ColumnOf(sliceStart);
        // One line-ending convention in the output: the source's \r\n / \r breaks are written as
        // the writer's line ending, as everywhere else the formatter writes (P22 CRLF cells).
        var lines = source.Substring(sliceStart, end - sliceStart).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (int k = 1; k < lines.Length; k++)
        {
            if (_cursor.LineStartsInsideLiteral(lineStart + k) || lines[k].Trim().Length == 0)
                continue;
            if (shift > 0)
            {
                lines[k] = new string(' ', shift) + lines[k];
            }
            else if (shift < 0)
            {
                var leading = 0;
                while (leading < lines[k].Length && leading < -shift && (lines[k][leading] == ' ' || lines[k][leading] == '\t'))
                    leading++;
                lines[k] = lines[k].Substring(leading);
            }
        }
        return string.Join(_options.LineEnding, lines);
    }

    /// <summary>
    /// Replaces the header just written at <paramref name="headerStart"/> — up to its first line
    /// break outside verbatim text — with its source slice, written opaque so the header's inline
    /// comment is appended after the slice, never inside it.
    /// </summary>
    private void ReplaceHeaderWithSlice(int headerStart, string slice, bool wholeWrite)
    {
        var headerEnd = wholeWrite
            ? _w.Length - (_w.ToString().EndsWith(_options.LineEnding, StringComparison.Ordinal) ? _options.LineEnding.Length : 0)
            : _w.IndexOfLineEndingOutsideOpaque(_options.LineEnding, headerStart);
        if (headerEnd < 0)
            headerEnd = _w.Length;
        // The written header's terminator stays the writer's: a bodyless definition
        // (`def f(self)`) is written with a stub body (`def f(self):` + `...`), so its slice — which
        // has no colon — takes the colon the stub body needs.
        if (!wholeWrite && headerEnd > headerStart && _w.CharAt(headerEnd - 1) == ':' && !slice.EndsWith(':'))
            slice += ":";
        _w.ReplaceRangeOpaque(headerStart, headerEnd, slice);
    }

    /// <summary>A docstring's anchor: its line range is read from the token stream (the AST records only its text).</summary>
    private void WriteDocStringAnchored(int afterOffset, Action write)
    {
        if (_cursor is { HasSource: true } && _cursor.TryFindDocString(afterOffset, out var startLine, out var startOffset, out var endOffset))
            WriteAnchored(startLine, 0, startOffset, endOffset, write);
        else
            write();
    }

    /// <summary>
    /// Opens a body whose first line starts at source column <paramref name="column"/> (written at
    /// the current writer level). Pair with <see cref="CloseBody"/>.
    /// </summary>
    private void OpenBody(int column) => _openBodies.Push((column, _w.IndentLevel));

    /// <summary>
    /// A body's end (its last source line <paramref name="lastLine"/>): the bodies that CLOSE here are
    /// the open ones whose column is right of the next code's column (all of them at the end of the
    /// file). The own-line comments before that code at or right of the outermost closing body's
    /// column are written here, in source order, each at the level of the deepest closing body whose
    /// column is at or left of the comment — so <c># end of the method</c> at the method body's
    /// indent stays in the method even when it follows a comment at the class body's indent. A
    /// comment left of every closing body leads the next statement and is written by its anchor.
    /// </summary>
    private void CloseBody(int lastLine)
    {
        if (_cursor is { HasSource: true } && _openBodies.Peek().Column > 0)
        {
            var nextColumn = _cursor.NextCodeColumn(lastLine);
            var closing = _openBodies.Where(b => b.Column > 0).TakeWhile(b => b.Column > nextColumn).ToList();
            if (closing.Count > 0)
            {
                foreach (var trivia in _cursor.TakeBodyEnd(closing[closing.Count - 1].Column, lastLine))
                {
                    if (trivia.Kind == TriviaKind.BlankLines)
                    {
                        if (_options.Formatting == null)
                            for (int i = 0; i < trivia.BlankLineCount; i++)
                                _w.WriteLine();
                        continue;
                    }
                    _w.WriteLineAtLevel(closing.First(b => b.Column <= trivia.Column).Level, trivia.Text);
                }
            }
        }
        _openBodies.Pop();
    }

    /// <summary>
    /// The source column a body opens at: its first element's column when that element starts its
    /// source line, else 0 — an inline body (<c>def f(): ...</c>) has no block of its own, so no
    /// comment can end it.
    /// </summary>
    private int BodyColumn(Text.TextSpan? span, int columnStart)
    {
        if (_cursor is not { HasSource: true })
            return columnStart;
        if (span is not { } s)
            return 0;
        return _cursor.StartsLine(s.Start) ? columnStart : 0;
    }

    private int BodyColumn(Statement first) => BodyColumn(first.Span, first.ColumnStart);

    /// <summary>
    /// The last source line of a node ending at <paramref name="span"/>: from its end offset when the
    /// source is at hand (several statement kinds record <c>LineEnd</c> as the NEXT statement's line,
    /// which would let a body's end claim that statement's leading comments), else
    /// <paramref name="lineEnd"/>.
    /// </summary>
    private int LastLineOf(Text.TextSpan? span, int lineEnd)
    {
        var fromSource = span is { } s && _cursor != null ? _cursor.EndLineOf(s.End) : 0;
        return fromSource > 0 ? fromSource : lineEnd;
    }

    private int LastLineOf(Statement stmt) => LastLineOf(stmt.Span, stmt.LineEnd);

    private void WriteOwnLine(List<Trivia> items)
    {
        foreach (var trivia in items)
        {
            if (trivia.Kind == TriviaKind.BlankLines)
            {
                if (_options.Formatting != null)
                    continue;
                for (int i = 0; i < trivia.BlankLineCount; i++)
                    _w.WriteLine();
            }
            else
            {
                _w.WriteLine(trivia.Text);
            }
        }
    }

    /// <summary>
    /// Appends inline comments to the anchor's header line: the first line break written at or after
    /// <paramref name="position"/> that is not inside verbatim text (a triple-quoted string, a
    /// multi-line replacement field — #2068).
    /// </summary>
    private void AppendInline(int position, List<Trivia> trivia)
    {
        var nlIdx = _w.IndexOfLineEndingOutsideOpaque(_options.LineEnding, position);
        if (nlIdx < 0)
            return;
        _w.InsertAt(nlIdx, string.Concat(trivia.Select(t => "  " + t.Text)));
    }

    #endregion

    #region Precedence

    /// <summary>
    /// A bare starred form is not an operand at any precedence: the parser accepts <c>*x</c> and
    /// the bare unpacking tuple <c>a, *b</c> only in a call argument, a collection element, an
    /// unpacking target or a standalone expression — never after an operator, where a leading
    /// <c>*</c> is read as multiplication (or, after <c>??</c>, silently turns the coalesce into
    /// two postfix <c>?</c> and a multiply). Ranking these below every operator makes the operand
    /// helpers parenthesize them into the spelling that survives a reparse (#1172).
    /// </summary>
    private const int PrecStarOperand = -1;

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
            QuestionMarkExpression => PrecPostfix,
            StarExpression => PrecStarOperand,
            SpreadElement => PrecStarOperand,
            TupleLiteral t when RendersAsBareTuple(t) => PrecStarOperand,
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
                or BinaryOperator.FloorDivide or BinaryOperator.Modulo
                or BinaryOperator.MatMul => PrecMultiplicative,
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
            WriteParenthesized(child);
        else
            Visit(child);
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
            WriteParenthesized(operand);
        else
            Visit(operand);
    }

    private void VisitPostfixObject(Expression obj)
    {
        if (obj is Parenthesized)
        {
            Visit(obj);
            return;
        }

        // A literal receiver needs grouping only where its spelling would re-lex differently with a
        // `.` after it: an integer (`1.real` lexes `1.` as the start of a number). A float is written
        // with its `.` or exponent (`1.5.real`, `1e3.real` re-lex as float, dot, name), and a string,
        // bytes, f- or t-string literal ends at its closing quote.
        int childPrec = GetExpressionPrecedence(obj);
        bool needsParens = childPrec < PrecPostfix
            || obj is IntegerLiteral
            || obj is FloatLiteral { Suffix: null } f && f.Value.All(c => char.IsDigit(c) || c == '_');

        if (needsParens)
            WriteParenthesized(obj);
        else
            Visit(obj);
    }

    /// <summary>
    /// Writes <paramref name="inner"/> wrapped in parentheses, in the spelling that survives a
    /// reparse. <c>(*x)</c> re-parses as a one-element tuple, whose own rendering carries a
    /// trailing comma — so a single starred element must be written <c>(*x,)</c> for the output
    /// to be a fixed point. Tuples that already supply their own commas need nothing (#1172).
    /// </summary>
    private void WriteParenthesized(Expression inner)
    {
        _w.Write("(");
        Visit(inner);
        if (NeedsTrailingCommaInParens(inner))
            _w.Write(",");
        _w.Write(")");
    }

    private static bool NeedsTrailingCommaInParens(Expression inner) => inner switch
    {
        StarExpression or SpreadElement => true,
        TupleLiteral t => t.Elements.Length == 1 && RendersAsBareTuple(t),
        _ => false
    };

    /// <summary>
    /// True for a tuple that <see cref="VisitTupleLiteral"/> writes without its own parentheses —
    /// the multi-element unpacking-target form <c>a, *b</c>. Such a tuple supplies no delimiters of
    /// its own, so it is not self-contained in operand position. A SOLE starred element is excluded:
    /// <c>*a</c> alone is not a legal target, so the tuple keeps its delimiters (<c>(*a,)</c> /
    /// <c>[*a]</c>) and is self-contained (#1845).
    /// </summary>
    // A list-display target keeps its brackets (`[b, *rest] = t`, accepted since #1841): writing it
    // bare would re-parse as a tuple target, a different IsListDisplay the comparer sees (P22b).
    private static bool RendersAsBareTuple(TupleLiteral tuple) =>
        !tuple.IsListDisplay && tuple.Elements.Length > 1 && tuple.Elements.Any(e => e is StarExpression);

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
        OpenBody(BodyColumn(body[0]));
        for (int i = 0; i < body.Length; i++)
        {
            if (fmt != null && i > 0)
            {
                bool isMemberDef = body[i] is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef or PropertyDef or EventDef;
                bool prevWasMemberDef = body[i - 1] is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef or PropertyDef or EventDef;
                if (isMemberDef || prevWasMemberDef)
                {
                    for (int b = 0; b < fmt.BlankLinesBetweenClassMembers; b++)
                        _w.WriteLine();
                }
            }

            VisitStatementWithTrivia(body[i]);
        }
        CloseBody(LastLineOf(body[body.Length - 1]));
        _w.Dedent();
    }

    private void WriteDecorators(ImmutableArray<Decorator> decorators)
    {
        if (!_decoratorsWritten.IsDefault && _decoratorsWritten == decorators)
        {
            // Already written at their own anchors by VisitStatementWithTrivia.
            _decoratorsWritten = default;
            return;
        }

        foreach (var dec in decorators)
            WriteAnchored(dec.LineStart, dec.LineEnd, dec.Span?.Start ?? -1, dec.Span?.End ?? -1, () => WriteDecorator(dec));
    }

    private void WriteDecorator(Decorator dec)
    {
        // `@[name(args)]` is a .NET attribute and `@name(args)` a Sharpy decorator: the bracket
        // is a user-written fact the parser records, and dropping it changes what compiles.
        _w.Write(dec.IsBracketAttribute ? "@[" : "@");
        WriteDottedName(dec.QualifiedParts, dec.BacktickEscapedParts);
        if (dec.Arguments.Length > 0 || dec.KeywordArguments.Length > 0)
        {
            _w.Write("(");
            WriteArgList(dec.Arguments, dec.KeywordArguments);
            _w.Write(")");
        }
        if (dec.IsBracketAttribute)
            _w.Write("]");
        _w.WriteLine();
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
            WriteName(kwarg.Name, kwarg.IsNameBacktickEscaped);
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
            // The DECLARATION half of #1274: references to `` `class` `` already round-tripped, so
            // dropping the backticks here produced `def identity[class](value: `class`)` — source
            // that cannot re-parse, and a use with no declaration.
            WriteName(tp.Name, tp.IsNameBacktickEscaped);
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
            case NotnullConstraint:
                _w.Write(": notnull");
                break;
        }
    }

    /// <summary>
    /// The one writer for a user-written name: every name position goes through here so its
    /// backtick escape is written back exactly as the parser recorded it (#2157).
    /// </summary>
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

    /// <summary>
    /// Writes a dotted name segment by segment through <see cref="WriteName"/>, joining with
    /// <c>.</c>. <paramref name="escaped"/> is parallel to <paramref name="parts"/>; a missing entry
    /// (an AST not built by the parser, or a parallel array left empty) reads as not escaped.
    /// </summary>
    private void WriteDottedName(ImmutableArray<string> parts, ImmutableArray<bool> escaped)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                _w.Write(".");
            WriteName(parts[i], IsPartEscaped(escaped, i));
        }
    }

    private static bool IsPartEscaped(ImmutableArray<bool> escaped, int i) =>
        !escaped.IsDefault && i < escaped.Length && escaped[i];

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
            BinaryOperator.MatMul => "@",
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
            AssignmentOperator.MatMulAssign => "@=",
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

    private static string EscapeFStringText(string value, char quoteChar = '"')
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (c == quoteChar)
            {
                sb.Append('\\');
                sb.Append(c);
            }
            else
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
        }
        return sb.ToString();
    }

    private static char ChooseFStringDelimiter(System.Collections.Immutable.ImmutableArray<FStringPart> parts)
    {
        bool hasDouble = false;
        bool hasSingle = false;
        foreach (var part in parts)
        {
            if (part.Text == null)
                continue;
            if (part.Text.Contains('"', System.StringComparison.Ordinal))
                hasDouble = true;
            if (part.Text.Contains('\'', System.StringComparison.Ordinal))
                hasSingle = true;
        }

        if (hasDouble && !hasSingle)
            return '\'';
        return '"';
    }

    #endregion
}
