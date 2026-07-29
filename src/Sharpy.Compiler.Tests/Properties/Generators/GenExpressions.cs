using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenExpressions
{
    public static Gen<Expression> Expression(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.Frequency(
                (5, Leaf(ctx)),
                (3, SimpleComposite(ctx.Burn())),
                (1, ComplexComposite(ctx.Burn(2))))
            : Leaf(ctx);

    private static Gen<Expression> Leaf(GenContext ctx) =>
        Gen.OneOf<Expression>(
            GenLiterals.AnyLiteral,
            IdentifierExpr(ctx),
            BytesLiteralExpr(),
            SuperExpressionExpr());

    private static Gen<Expression> SimpleComposite(GenContext ctx)
    {
        var gens = new List<Gen<Expression>>
        {
            UnaryOpExpr(ctx).Select(x => (Expression)x),
            BinaryOpExpr(ctx).Select(x => (Expression)x),
            ComparisonChainExpr(ctx).Select(x => (Expression)x),
            ParenthesizedExpr(ctx).Select(x => (Expression)x),
            MemberAccessExpr(ctx).Select(x => (Expression)x),
            FunctionCallExpr(ctx).Select(x => (Expression)x),
            ConditionalExpr(ctx).Select(x => (Expression)x),
            TypeCoercionExpr(ctx).Select(x => (Expression)x),
            TypeCheckExpr(ctx).Select(x => (Expression)x),
            TryExpressionExpr(ctx).Select(x => (Expression)x),
            MaybeExpressionExpr(ctx).Select(x => (Expression)x),
            QuestionMarkExpr(ctx).Select(x => (Expression)x),
            StarExpressionExpr(ctx).Select(x => (Expression)x),
            SpreadElementExpr(ctx).Select(x => (Expression)x),
            ModifiedArgumentExpr(ctx).Select(x => (Expression)x),
        };
        if (ctx.AllowAsync)
            gens.Add(AwaitExpressionExpr(ctx).Select(x => (Expression)x));
        return Gen.OneOf(gens.ToArray());
    }

    private static Gen<Expression> ComplexComposite(GenContext ctx) =>
        Gen.OneOf<Expression>(
            ListLiteralExpr(ctx),
            DictLiteralExpr(ctx),
            SetLiteralExpr(ctx),
            TupleLiteralExpr(ctx),
            LambdaExpr(ctx),
            ListComprehensionExpr(ctx),
            IndexAccessExpr(ctx),
            SliceAccessExpr(ctx),
            MultiAxisAccessExpr(ctx),
            WalrusExpr(ctx),
            FStringLiteralExpr(ctx),
            TStringLiteralExpr(ctx),
            SetComprehensionExpr(ctx),
            DictComprehensionExpr(ctx),
            MatchExpressionExpr(ctx));

    public static Gen<Identifier> IdentifierExpr(GenContext ctx) =>
        GenIdentifier.NameFromContext(ctx).Select(n =>
            new Identifier { Name = n });

    public static Gen<UnaryOp> UnaryOpExpr(GenContext ctx) =>
        Gen.Select(
            Gen.OneOfConst(UnaryOperator.Plus, UnaryOperator.Minus, UnaryOperator.Not, UnaryOperator.BitwiseNot),
            Expression(ctx),
            (op, operand) => new UnaryOp { Operator = op, Operand = operand });

    private static readonly BinaryOperator[] SafeBinaryOps =
    {
        BinaryOperator.Add, BinaryOperator.Subtract, BinaryOperator.Multiply,
        BinaryOperator.Divide, BinaryOperator.FloorDivide, BinaryOperator.Modulo,
        BinaryOperator.Power, BinaryOperator.And, BinaryOperator.Or,
        BinaryOperator.BitwiseAnd, BinaryOperator.BitwiseOr, BinaryOperator.BitwiseXor,
        BinaryOperator.LeftShift, BinaryOperator.RightShift, BinaryOperator.NullCoalesce
    };

    public static Gen<BinaryOp> BinaryOpExpr(GenContext ctx) =>
        Gen.Select(
            Gen.OneOfConst(SafeBinaryOps),
            Expression(ctx),
            Expression(ctx),
            (op, left, right) => new BinaryOp { Operator = op, Left = left, Right = right });

    public static Gen<Parenthesized> ParenthesizedExpr(GenContext ctx) =>
        Expression(ctx).Select(e => new Parenthesized { Expression = e });

    public static Gen<MemberAccess> MemberAccessExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            GenIdentifier.Name,
            (obj, member) => new MemberAccess { Object = obj, Member = member });

    public static Gen<FunctionCall> FunctionCallExpr(GenContext ctx) =>
        Gen.Select(
            IdentifierExpr(ctx).Select(x => (Expression)x),
            Expression(ctx).Array[0, Sizing.MaxParameters(ctx.Fuel)],
            (func, args) => new FunctionCall
            {
                Function = func,
                Arguments = args.ToImmutableArray()
            });

    public static Gen<ConditionalExpression> ConditionalExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            Expression(ctx),
            Expression(ctx),
            (test, then, els) => new ConditionalExpression
            {
                Test = test,
                ThenValue = then,
                ElseValue = els
            });

    public static Gen<ListLiteral> ListLiteralExpr(GenContext ctx) =>
        Expression(ctx).Array[0, Sizing.MaxListLength(ctx.Fuel)].Select(elems =>
            new ListLiteral { Elements = elems.ToImmutableArray() });

    public static Gen<DictLiteral> DictLiteralExpr(GenContext ctx) =>
        GenLiterals.Dict(Expression(ctx), Expression(ctx), Sizing.MaxListLength(ctx.Fuel));

    public static Gen<SetLiteral> SetLiteralExpr(GenContext ctx) =>
        Expression(ctx).Array[1, Math.Max(1, Sizing.MaxListLength(ctx.Fuel))].Select(elems =>
            new SetLiteral { Elements = elems.ToImmutableArray() });

    public static Gen<TupleLiteral> TupleLiteralExpr(GenContext ctx) =>
        Expression(ctx).Array[1, Math.Max(1, Sizing.MaxListLength(ctx.Fuel))].Select(elems =>
            new TupleLiteral
            {
                Elements = elems.ToImmutableArray(),
                ElementNames = ImmutableArray<string?>.Empty
            });

    public static Gen<IndexAccess> IndexAccessExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            IndexValue(ctx),
            (obj, index) => new IndexAccess { Object = obj, Index = index });

    // Subscript index: usually an arbitrary expression, occasionally the empty tuple
    // so 'x[()]' is exercised. The unparser must keep the parens here (#1001) to avoid
    // collapsing to 'x[]'; this regression-guards that via the round-trip harness.
    private static Gen<Expression> IndexValue(GenContext ctx) =>
        Gen.Frequency(
            (6, Expression(ctx)),
            (1, Gen.Const<Expression>(new TupleLiteral
            {
                Elements = ImmutableArray<Expression>.Empty,
                ElementNames = ImmutableArray<string?>.Empty
            })));

    public static Gen<SliceAccess> SliceAccessExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            Gen.Null(Expression(ctx)),
            Gen.Null(Expression(ctx)),
            (obj, start, stop) => new SliceAccess
            {
                Object = obj,
                Start = start,
                Stop = stop
            });

    public static Gen<ComparisonChain> ComparisonChainExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx).Array[2, 4],
            Gen.OneOfConst(
                ComparisonOperator.Equal, ComparisonOperator.NotEqual,
                ComparisonOperator.LessThan, ComparisonOperator.LessThanOrEqual,
                ComparisonOperator.GreaterThan, ComparisonOperator.GreaterThanOrEqual
            ).Array[1, 3],
            (operands, ops) =>
            {
                var opCount = Math.Min(operands.Length - 1, ops.Length);
                return new ComparisonChain
                {
                    Operands = operands[..(opCount + 1)].ToImmutableArray(),
                    Operators = ops[..opCount].ToImmutableArray()
                };
            });

    public static Gen<LambdaExpression> LambdaExpr(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name.Array[0, 3],
            Expression(ctx),
            (paramNames, body) => new LambdaExpression
            {
                Parameters = paramNames.Select(n => new Parameter { Name = n }).ToImmutableArray(),
                Body = body
            });

    // For-clause of a comprehension, occasionally async (#998): '[x async for x in xs]'.
    // The parser accepts async-for clauses anywhere a comprehension is parsed, so this
    // round-trips at module level even though semantic analysis would require an async
    // context. Mostly non-async to keep ordinary comprehensions well-represented.
    private static readonly Gen<bool> ComprehensionIsAsync =
        Gen.Frequency((4, Gen.Const(false)), (1, Gen.Const(true)));

    private static Gen<ForClause> ComprehensionForClause(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            Expression(ctx),
            ComprehensionIsAsync,
            (varName, iter, isAsync) => new ForClause
            {
                Target = new Identifier { Name = varName },
                Iterator = iter,
                IsAsync = isAsync
            });

    public static Gen<ListComprehension> ListComprehensionExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            ComprehensionForClause(ctx),
            (elem, clause) => new ListComprehension
            {
                Element = elem,
                Clauses = ImmutableArray.Create<ComprehensionClause>(clause)
            });

    public static Gen<TypeCoercion> TypeCoercionExpr(GenContext ctx) =>
        // The `as!`/`as?` forms are graduated language surface (#1096) and the only cast spelling:
        // the legacy `to`/`to?` forms retired as a parse error (#1127) and were dropped from the
        // pool. The `as` forms carry their failure mode on the operator, so their target is always
        // written non-nullable.
        Gen.Select(
            Expression(ctx),
            GenTypes.SimpleType,
            Gen.Int[0, 1],
            (expr, type, form) => form switch
            {
                0 => new TypeCoercion
                {
                    Value = expr,
                    TargetType = type with { IsOptional = false },
                    Mode = CastFailureMode.Throw,
                },
                _ => new TypeCoercion
                {
                    Value = expr,
                    TargetType = type with { IsOptional = false },
                    Mode = CastFailureMode.Null,
                },
            });

    public static Gen<TypeCheck> TypeCheckExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            GenTypes.SimpleType,
            (expr, type) => new TypeCheck
            {
                Value = expr,
                CheckType = type
            });

    public static Gen<WalrusExpression> WalrusExpr(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            Expression(ctx),
            (name, val) => new WalrusExpression
            {
                Target = name,
                Value = val
            });

    private static readonly string[] FStringTextParts =
    {
        "hello ", "world", " is ", "value: ", "result = ", " - ", ", ", "! ", " "
    };

    // Conversion flags (#987) and format specs that round-trip verbatim through
    // lexer -> unparser. Kept free of '{', '}', ':' and '!' so they never re-lex
    // as a different specifier.
    private static readonly char[] FStringConversionFlags = { 'r', 's', 'a' };

    private static readonly string[] FStringFormatSpecs =
    {
        ".2f", ">10", "<8", "^6", "05d", "+.3e", "x", "b"
    };

    // One f-string replacement field or text run. Covers plain text, a bare
    // expression, the '!r'/'!s'/'!a' conversion flags (#987), a conversion paired
    // with a format spec (the #987 ordering: '{x!r:>10}'), and the '=' self-
    // documenting specifier (#986). Self-documenting parts use an identifier whose
    // SourceText is exactly what the lexer recaptures ("<name>="), so they round-trip.
    private static Gen<FStringPart> FStringPartGen(GenContext ctx) =>
        Gen.Frequency(
            (3, Gen.OneOfConst(FStringTextParts).Select(t =>
                new FStringPart { Text = t })),
            (3, Expression(ctx.Burn()).Select(e =>
                new FStringPart { Expression = e })),
            (1, Gen.Select(IdentifierExpr(ctx), Gen.OneOfConst(FStringConversionFlags),
                (e, conv) => new FStringPart { Expression = e, Conversion = conv })),
            (1, Gen.Select(IdentifierExpr(ctx), Gen.OneOfConst(FStringConversionFlags),
                Gen.OneOfConst(FStringFormatSpecs),
                (e, conv, spec) => new FStringPart
                {
                    Expression = e,
                    Conversion = conv,
                    FormatSpec = spec
                })),
            (1, GenIdentifier.Name.Select(name => new FStringPart
            {
                Expression = new Identifier { Name = name },
                IsSelfDocumenting = true,
                SourceText = name + "=",
            })));

    public static Gen<FStringLiteral> FStringLiteralExpr(GenContext ctx) =>
        Gen.Int[1, 3].SelectMany(count =>
            Enumerable.Range(0, count)
                .Select(_ => FStringPartGen(ctx))
                .Aggregate(Gen.Const(ImmutableArray<FStringPart>.Empty),
                    (acc, partGen) => Gen.Select(acc, partGen, (parts, part) => parts.Add(part)))
                .Select(parts => new FStringLiteral { Parts = parts }));

    public static Gen<TStringLiteral> TStringLiteralExpr(GenContext ctx) =>
        Gen.Int[1, 3].SelectMany(count =>
            Enumerable.Range(0, count)
                .Select(_ => FStringPartGen(ctx))
                .Aggregate(Gen.Const(ImmutableArray<FStringPart>.Empty),
                    (acc, partGen) => Gen.Select(acc, partGen, (parts, part) => parts.Add(part)))
                .Select(parts => new TStringLiteral { Parts = parts }));

    public static Gen<BytesLiteralExpression> BytesLiteralExpr() =>
        Gen.OneOfConst("hello", "test", "data", "bytes", "", "abc")
            .Select(v => new BytesLiteralExpression { Value = v });

    public static Gen<SetComprehension> SetComprehensionExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            ComprehensionForClause(ctx),
            (elem, clause) => new SetComprehension
            {
                Element = elem,
                Clauses = ImmutableArray.Create<ComprehensionClause>(clause)
            });

    public static Gen<DictComprehension> DictComprehensionExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            Expression(ctx),
            ComprehensionForClause(ctx),
            (key, value, clause) => new DictComprehension
            {
                Key = key,
                Value = value,
                Clauses = ImmutableArray.Create<ComprehensionClause>(clause)
            });

    public static Gen<MatchExpression> MatchExpressionExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            GenPatterns.Pattern(ctx.Burn()).Array[1, 3],
            Expression(ctx.Burn()).Array[1, 3],
            Gen.Null(Expression(ctx.Burn())).Array[1, 3],
            (scrutinee, patterns, results, guards) =>
            {
                var armCount = Math.Min(patterns.Length, results.Length);
                armCount = Math.Min(armCount, guards.Length);
                var arms = new MatchArm[armCount];
                for (int i = 0; i < armCount; i++)
                {
                    arms[i] = new MatchArm
                    {
                        Pattern = patterns[i],
                        Guard = guards[i],
                        Result = results[i]
                    };
                }
                return new MatchExpression
                {
                    Scrutinee = scrutinee,
                    Arms = arms.ToImmutableArray()
                };
            });

    public static Gen<TryExpression> TryExpressionExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            Gen.Null(GenTypes.SimpleType),
            (operand, exType) => new TryExpression
            {
                Operand = operand,
                ExceptionTypes = exType is null
                    ? ImmutableArray<TypeAnnotation>.Empty
                    : ImmutableArray.Create(exType)
            });

    public static Gen<MaybeExpression> MaybeExpressionExpr(GenContext ctx) =>
        Expression(ctx).Select(operand => new MaybeExpression { Operand = operand });

    public static Gen<QuestionMarkExpression> QuestionMarkExpr(GenContext ctx) =>
        Expression(ctx).Select(operand => new QuestionMarkExpression { Operand = operand });

    public static Gen<MultiAxisAccess> MultiAxisAccessExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            SubscriptDimensionGen(ctx).Array[2, 4],
            (obj, dims) => new MultiAxisAccess
            {
                Object = obj,
                Dimensions = dims.ToImmutableArray()
            });

    private static Gen<SubscriptDimension> SubscriptDimensionGen(GenContext ctx) =>
        Gen.Bool.SelectMany(isSlice =>
            isSlice
                ? Gen.Select(
                    Gen.Null(Expression(ctx)),
                    Gen.Null(Expression(ctx)),
                    (start, stop) => new SubscriptDimension
                    {
                        IsSlice = true,
                        Start = start,
                        Stop = stop
                    })
                : Expression(ctx).Select(index => new SubscriptDimension
                {
                    IsSlice = false,
                    Index = index
                }));

    public static Gen<StarExpression> StarExpressionExpr(GenContext ctx) =>
        IdentifierExpr(ctx).Select(id => new StarExpression { Operand = id });

    public static Gen<SpreadElement> SpreadElementExpr(GenContext ctx) =>
        Expression(ctx).Select(value => new SpreadElement { Value = value });

    public static Gen<ModifiedArgument> ModifiedArgumentExpr(GenContext ctx) =>
        Gen.Select(
            Gen.OneOfConst(ParameterModifier.Ref, ParameterModifier.Out, ParameterModifier.In),
            IdentifierExpr(ctx),
            (mod, id) => new ModifiedArgument
            {
                Modifier = mod,
                Argument = id
            });

    public static Gen<AwaitExpression> AwaitExpressionExpr(GenContext ctx) =>
        Expression(ctx).Select(operand => new AwaitExpression { Operand = operand });

    public static Gen<SuperExpression> SuperExpressionExpr() =>
        Gen.Const(new SuperExpression());

    // ============================================================
    // Ambiguity-hotspot bias generators (#1037)
    //
    // The three shapes where the Sharpy parser is most likely to
    // mis-place a ':' , ',' or precedence boundary are lambda
    // bodies, subscripts, and tuple literals. These generators
    // deliberately STACK those hotspots (and nest them inside one
    // another) so the round-trip / never-crash harness spends its
    // budget on the pathological cases rather than on ordinary
    // arithmetic. See docs/design/test-harness/ and the historical
    // bug comments on IndexAccessExpr (#1001), LambdaExpr, etc.
    // Consumed by AmbiguityHotspotPropertyTests.
    //
    // Historical parser/unparser ambiguity bugs these generators can
    // reproduce (anchored deterministically in AmbiguityHotspotPropertyTests
    // .HistoricalParserBugs_ParseAndRoundTrip):
    //   #899/#1015 — subscript lambda body (AmbiguousLambda / CallArg lambdas)
    //   #1011      — bare-identifier lambda body in a call arg (CallArg)
    //   #888       — single-element tuple literal (TupleLiteralExpr)
    //   #1001      — parenthesis-free tuple subscript (AmbiguousSubscript)
    //   #872       — soft-keyword ('match=') keyword arg (TupleBearingCall)
    //   #870       — quote-bearing f-string (FStringWithQuotesExpr)
    // #847 (nullable function-type annotation) is a type-level shape the
    // expression-first generators don't model; it is covered by the anchored
    // regression only.
    // ============================================================

    private static readonly TupleLiteral EmptyTuple = new()
    {
        Elements = ImmutableArray<Expression>.Empty,
        ElementNames = ImmutableArray<string?>.Empty,
    };

    /// <summary>
    /// One ambiguity hotspot at this depth: an ambiguous lambda, an
    /// ambiguous subscript, a tuple-bearing call, or a nested stack
    /// of all three. Falls back to a leaf when out of fuel.
    /// </summary>
    public static Gen<Expression> AmbiguousHotspot(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.OneOf(
                AmbiguousLambda(ctx.Burn()).Select(x => (Expression)x),
                AmbiguousSubscript(ctx.Burn()),
                TupleBearingCall(ctx.Burn()).Select(x => (Expression)x),
                FStringWithQuotesExpr(ctx.Burn()).Select(x => (Expression)x),
                NestedHotspot(ctx.Burn()))
            : IdentifierExpr(ctx).Select(x => (Expression)x);

    // F-string whose literal text contains double-quote characters around a
    // replacement field. Regression cover for #870, where the unparser re-emitted a
    // quote-containing f-string with a colliding delimiter that would not re-parse.
    public static Gen<FStringLiteral> FStringWithQuotesExpr(GenContext ctx) =>
        Gen.Select(
            Gen.OneOfConst("\"", "\"x\"", "say \"", "[\"]"),
            IdentifierExpr(ctx),
            (quoted, e) => new FStringLiteral
            {
                Parts = ImmutableArray.Create(
                    new FStringPart { Text = quoted },
                    new FStringPart { Expression = e },
                    new FStringPart { Text = quoted })
            });

    // --- Hotspot 1: lambdas whose bodies are tuples/conditionals/lambdas ---

    /// <summary>
    /// Lambda whose body is itself an ambiguity hotspot. The parser
    /// has to decide how far the body extends past the ':' — a bare
    /// tuple body (lambda: a, b) vs a grouped one (lambda: (a, b)),
    /// a trailing conditional, or another lambda all stress that.
    /// </summary>
    public static Gen<LambdaExpression> AmbiguousLambda(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name.Array[0, 2],
            AmbiguousLambdaBody(ctx),
            (paramNames, body) => new LambdaExpression
            {
                Parameters = paramNames.Select(n => new Parameter { Name = n }).ToImmutableArray(),
                Body = body
            });

    private static Gen<Expression> AmbiguousLambdaBody(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.OneOf(
                TupleLiteralExpr(ctx.Burn()).Select(x => (Expression)x),
                ConditionalExpr(ctx.Burn()).Select(x => (Expression)x),
                LambdaExpr(ctx.Burn()).Select(x => (Expression)x),
                AmbiguousLambda(ctx.Burn()).Select(x => (Expression)x))
            : IdentifierExpr(ctx).Select(x => (Expression)x);

    // --- Hotspot 2: subscripts containing slices/tuples/lambdas/walrus ---

    /// <summary>
    /// Subscript whose index is a slice, a tuple, a lambda, a walrus
    /// (:=), a multi-axis mix, or the empty tuple x[()] (#1001). The
    /// base object is occasionally another subscript so chained
    /// x[..][..] forms are exercised.
    /// </summary>
    public static Gen<Expression> AmbiguousSubscript(GenContext ctx)
    {
        var baseObj = SubscriptBase(ctx);
        return Gen.Frequency(
            // x[lambda: y]
            (2, Gen.Select(baseObj, LambdaExpr(ctx.Burn()),
                (o, l) => (Expression)new IndexAccess { Object = o, Index = l })),
            // x[n := y]
            (2, Gen.Select(baseObj, WalrusExpr(ctx.Burn()),
                (o, w) => (Expression)new IndexAccess { Object = o, Index = w })),
            // x[a, b] tuple subscript
            (2, Gen.Select(baseObj, TupleLiteralExpr(ctx.Burn()),
                (o, t) => (Expression)new IndexAccess { Object = o, Index = t })),
            // x[a:b] slice
            (2, Gen.Select(
                    baseObj,
                    Gen.Null(Expression(ctx.Burn())),
                    Gen.Null(Expression(ctx.Burn())),
                    (o, start, stop) => (Expression)new SliceAccess
                    {
                        Object = o,
                        Start = start,
                        Stop = stop
                    })),
            // x[a, b:c] multi-axis with slices
            (2, Gen.Select(
                    baseObj,
                    SubscriptDimensionGen(ctx.Burn()).Array[2, 3],
                    (o, dims) => (Expression)new MultiAxisAccess
                    {
                        Object = o,
                        Dimensions = dims.ToImmutableArray()
                    })),
            // x[()] empty-tuple subscript (#1001)
            (1, baseObj.Select(o => (Expression)new IndexAccess { Object = o, Index = EmptyTuple })));
    }

    private static Gen<Expression> SubscriptBase(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.Frequency(
                (4, IdentifierExpr(ctx).Select(x => (Expression)x)),
                (1, AmbiguousSubscript(ctx.Burn())))
            : IdentifierExpr(ctx).Select(x => (Expression)x);

    // --- Hotspot 3: tuple literals (parenthesized and bare) in call args ---

    // Keyword-argument names. Includes the soft keyword 'match', which must be
    // usable as a kwarg name (#872) — a hard-keyword lexer classification used to
    // reject 'match=' in call-argument position.
    private static readonly string[] KeywordArgNames =
    {
        "match", "key", "value", "sep", "default",
    };

    private static Gen<KeywordArgument> KeywordArgGen(GenContext ctx) =>
        Gen.Select(
            Gen.OneOfConst(KeywordArgNames),
            CallArg(ctx),
            (name, val) => new KeywordArgument { Name = name, Value = val });

    /// <summary>
    /// Call whose arguments stress the comma ambiguity: parenthesized
    /// tuple arguments f((a, b)) that must not collapse into separate
    /// arguments f(a, b), mixed with plain args, lambdas, conditionals
    /// and nested tuple-bearing calls. Also emits keyword arguments,
    /// including the soft-keyword name 'match=' (#872).
    /// </summary>
    public static Gen<FunctionCall> TupleBearingCall(GenContext ctx) =>
        Gen.Select(
            IdentifierExpr(ctx).Select(x => (Expression)x),
            CallArg(ctx).Array[1, Math.Max(1, Sizing.MaxParameters(ctx.Fuel))],
            KeywordArgGen(ctx.Burn()).Array[0, 2],
            (func, args, kwargs) => new FunctionCall
            {
                Function = func,
                Arguments = args.ToImmutableArray(),
                KeywordArguments = kwargs.ToImmutableArray()
            });

    private static Gen<Expression> CallArg(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.Frequency(
                // f((a, b)) — a parenthesized tuple as a SINGLE argument
                (3, TupleLiteralExpr(ctx.Burn()).Select(x => (Expression)x)),
                // f(lambda: x) — a lambda argument (unparenthesized body runs to ')')
                (2, LambdaExpr(ctx.Burn()).Select(x => (Expression)x)),
                // f(a if b else c)
                (1, ConditionalExpr(ctx.Burn()).Select(x => (Expression)x)),
                // f(g((a, b))) — nested tuple-bearing call
                (1, TupleBearingCall(ctx.Burn()).Select(x => (Expression)x)),
                // f(a) — plain arg, so bare vs grouped commas actually collide
                (2, Expression(ctx.Burn())))
            : Expression(ctx);

    // --- Nested combinations of all three hotspots ---

    /// <summary>
    /// A hotspot whose sub-expressions are themselves hotspots: a
    /// lambda returning a subscript of a tuple-call, a subscript
    /// indexed by a lambda over a tuple, a call passing a subscript,
    /// etc. This is where the three ambiguities compound.
    /// </summary>
    public static Gen<Expression> NestedHotspot(GenContext ctx)
    {
        if (!ctx.HasFuel)
            return IdentifierExpr(ctx).Select(x => (Expression)x);

        var inner = ctx.Burn();
        return Gen.OneOf(
            // lambda: <subscript>
            Gen.Select(
                GenIdentifier.Name.Array[0, 1],
                AmbiguousSubscript(inner),
                (ps, body) => (Expression)new LambdaExpression
                {
                    Parameters = ps.Select(n => new Parameter { Name = n }).ToImmutableArray(),
                    Body = body
                }),
            // (<call>)[<lambda>]
            Gen.Select(
                TupleBearingCall(inner).Select(x => (Expression)x),
                LambdaExpr(inner),
                (o, l) => (Expression)new IndexAccess { Object = o, Index = l }),
            // f(<subscript>, (a, b))
            Gen.Select(
                IdentifierExpr(ctx).Select(x => (Expression)x),
                AmbiguousSubscript(inner),
                TupleLiteralExpr(inner),
                (func, sub, tup) => (Expression)new FunctionCall
                {
                    Function = func,
                    Arguments = ImmutableArray.Create<Expression>(sub, tup)
                }),
            // lambda: (a, b)[i]  — tuple subscripted, wrapped in a lambda body
            Gen.Select(
                AmbiguousLambda(inner).Select(x => (Expression)x),
                IdentifierExpr(inner).Select(x => (Expression)x),
                (l, idx) => (Expression)new IndexAccess { Object = l, Index = idx }));
    }
}
