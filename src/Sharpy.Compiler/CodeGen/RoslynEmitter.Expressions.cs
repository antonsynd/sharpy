using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Sharpy.Compiler.CodeGen.EmittedTreePrecedence;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Expression generation dispatch and small utilities.
/// Sub-partials: Operators, Literals, Access
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateExpression(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        // Re-entry tripwire (#1334). Null in production — one null check, no state, no allocation.
        // Installed only by a test-side ICodeEmitterFactory, which is what makes double generation
        // a structural property rather than something each shape has to be tested for.
        _generationRecorder?.OnGenerate(expr);

        var generated = GenerateExpressionCore(expr);

        // Char materialization (#1291): the TypeChecker decided this expression yields a char-based CLR
        // value where its own semantic type says Sharpy `str`, and named the conversion. Applied before
        // the sequence wrap below, so a value needing both is converted first and then wrapped — the
        // order the two facts were decided in.
        var charKind = _context.SemanticInfo?.GetCharMaterialization(expr);
        if (charKind != null)
            generated = MaterializeChar(generated, charKind.Value);

        // Sequence materialization (#1251): the TypeChecker decided this expression yields a CLR
        // sequence where its own semantic type says Sharpy collection, and named the collection to
        // build. Applied here, at the one choke point every expression passes through, so no position
        // can be reached that forgot to apply it. Absent for every expression whose emitted type
        // already matches its semantic type, which is all of them except CLR-sequence values — so the
        // default path is byte-identical.
        var materializationTarget = _context.SemanticInfo?.GetSequenceMaterialization(expr);
        return materializationTarget != null
            ? MaterializeSequence(generated, materializationTarget)
            : generated;
    }

    /// <summary>
    /// Wraps a CLR sequence in the Sharpy collection the TypeChecker recorded for it:
    /// <c>xs</c> -> <c>new Sharpy.List&lt;string&gt;(xs)</c>. The type name comes from
    /// <see cref="TypeSyntaxMapper"/>, the single authority for <c>list[T]</c> -> <c>Sharpy.List&lt;T&gt;</c>,
    /// so this cannot spell a collection type differently from anywhere else in the emitter.
    /// </summary>
    private ExpressionSyntax MaterializeSequence(ExpressionSyntax value, SemanticType targetCollection)
    {
        return ObjectCreationExpression(_typeMapper.MapSemanticType(targetCollection))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value))));
    }

    /// <summary>
    /// Converts a char-based CLR value into the Sharpy <c>str</c> the TypeChecker typed it as: a scalar
    /// becomes <c>.ToString()</c>, a <c>char[]</c> becomes a <c>string[]</c> of one-character strings
    /// (#1291), and an <c>IEnumerable&lt;char&gt;</c> becomes an <c>IEnumerable&lt;string&gt;</c> of
    /// them (#1401). The emitter decides nothing here — which expressions carry a conversion, and
    /// which of the three it is, was decided by the seam that read the reflected signature.
    /// </summary>
    /// <remarks>
    /// The array and sequence forms are <c>Array.ConvertAll&lt;char, string&gt;(xs, char.ToString)</c>
    /// and <c>Enumerable.Select&lt;char, string&gt;(xs, char.ToString)</c> — a method group rather
    /// than a lambda, so the emitted form introduces no parameter name that could collide with a
    /// Sharpy local in scope, and the explicit type arguments leave no inference to a <c>ToString</c>
    /// overload set. The sequence form stays lazy; the copy, when one is wanted, is #1251's wrap
    /// applied on top of it by the caller.
    /// </remarks>
    private static ExpressionSyntax MaterializeChar(ExpressionSyntax value, CharMaterializationKind kind)
    {
        if (kind == CharMaterializationKind.Scalar)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    value,
                    IdentifierName(nameof(object.ToString))))
                .WithArgumentList(ArgumentList());
        }

        if (kind == CharMaterializationKind.Literal)
        {
            // The call seam that read the reflected `char` parameter already proved this argument is
            // a ONE-character string literal (#1402), so the conversion is a re-spelling of a value
            // already in hand: `"a"` -> `'a'`. Nothing is decided here — a literal of any other
            // length, and every non-literal, is refused at the call and never carries this fact, so
            // the guard below is a shape assertion rather than a second decision. Asserting loudly:
            // passing `value` through unconverted would hand Roslyn a string for a char slot, i.e.
            // CS1503 behind SPY0908 — the exact shape this fact exists to prevent.
            if ((value as LiteralExpressionSyntax)?.Token.ValueText is { Length: 1 } text)
                return LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(text[0]));

            throw new InvalidOperationException(
                "CharMaterializationKind.Literal on a node that is not a one-character string "
                + "literal — the #1402 call-seam gate only records this fact for "
                + "StringLiteral { Value.Length: 1 }, so semantic analysis and emission disagree "
                + "about this node's shape");
        }

        if (kind == CharMaterializationKind.Sequence)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    MakeGlobalQualifiedName(nameof(System), nameof(System.Linq), nameof(System.Linq.Enumerable)),
                    GenericName(Identifier(nameof(System.Linq.Enumerable.Select)))
                        .WithTypeArgumentList(TypeArgumentList(SeparatedList<TypeSyntax>(new TypeSyntax[]
                        {
                            PredefinedType(Token(SyntaxKind.CharKeyword)),
                            PredefinedType(Token(SyntaxKind.StringKeyword))
                        })))))
                .WithArgumentList(ArgumentList(SeparatedList(new[]
                {
                    Argument(value),
                    Argument(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        PredefinedType(Token(SyntaxKind.CharKeyword)),
                        IdentifierName(nameof(char.ToString))))
                })));
        }

        var convertAll = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            MakeGlobalQualifiedName(nameof(System), nameof(System.Array)),
            GenericName(Identifier(nameof(System.Array.ConvertAll)))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList<TypeSyntax>(new TypeSyntax[]
                {
                    PredefinedType(Token(SyntaxKind.CharKeyword)),
                    PredefinedType(Token(SyntaxKind.StringKeyword))
                }))));

        return InvocationExpression(convertAll)
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                Argument(value),
                Argument(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    PredefinedType(Token(SyntaxKind.CharKeyword)),
                    IdentifierName(nameof(char.ToString))))
            })));
    }

    private ExpressionSyntax GenerateExpressionCore(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        // E3 const-folding (opt_const_fold, #640): an operation the pass reduced emits as its literal
        // value. FoldedConstants is empty unless the pass ran for this file, so the default
        // (flag-off) path is byte-identical.
        if (_context.Ir.FoldedConstants.TryGetValue(expr, out var foldedConstant))
            return EmitFoldedConstant(foldedConstant);

        return expr switch
        {
            // Literals
            IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
            FloatLiteral floatLit => GenerateFloatLiteral(floatLit),
            StringLiteral strLit => GenerateStringLiteral(strLit),
            BytesLiteralExpression bytesLit => GenerateBytesLiteral(bytesLit),
            BooleanLiteral boolLit => LiteralExpression(boolLit.Value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            NoneLiteral noneLiteral => GenerateNoneLiteral(noneLiteral),
            EllipsisLiteral => GenerateEllipsisLiteral(),

            // Collections
            ListLiteral listLit => GenerateListLiteral(listLit),
            DictLiteral dictLit => GenerateDictLiteral(dictLit),
            SetLiteral setLit => GenerateSetLiteral(setLit),
            TupleLiteral tupleLit => GenerateTupleLiteral(tupleLit),

            // Comprehensions
            ListComprehension listComp => GenerateListComprehension(listComp),
            SetComprehension setComp => GenerateSetComprehension(setComp),
            DictComprehension dictComp => GenerateDictComprehension(dictComp),
            DictSpreadComprehension dictSpreadComp => GenerateDictSpreadComprehension(dictSpreadComp),

            // Primary expressions
            // Handle 'self' -> 'this' conversion for instance methods
            // When _selfReplacementIdentifier is set (inlined operator body), map to that instead
            Identifier name when string.Equals(name.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) =>
                _selfReplacementIdentifier != null
                    ? IdentifierName(_selfReplacementIdentifier)
                    : ThisExpression(),
            Identifier name => GenerateIdentifierExpression(name),
            SuperExpression => BaseExpression(),  // super() -> base
            MemberAccess memberAccess => GenerateMemberAccess(memberAccess),
            IndexAccess indexAccess => GenerateIndexAccess(indexAccess),
            SliceAccess sliceAccess => GenerateSliceAccess(sliceAccess),
            MultiAxisAccess multiAxis => GenerateMultiAxisAccess(multiAxis),
            // Handle None() -> Optional<T>.None
            FunctionCall call when call.Function is NoneLiteral
                && call.Arguments.Length == 0
                && GetExpressionSemanticType(call) is OptionalType optNone
                => GenerateOptionalNone(optNone),
            // Handle Some/Ok/Err -> Optional/Result factory calls (tagged union constructors)
            FunctionCall call when IsTaggedUnionConstructorCall(call) => GenerateTaggedUnionConstructor(call),
            FunctionCall call => GenerateCall(call),

            // Operators
            UnaryOp unaryOp => GenerateUnaryOp(unaryOp),
            BinaryOp binOp => GenerateBinaryOp(binOp),
            ComparisonChain chain => GenerateComparisonChain(chain),

            // Advanced expressions
            ConditionalExpression cond => GenerateConditionalExpression(cond),
            LambdaExpression lambda => GenerateLambdaExpression(lambda),
            TypeCoercion coercion => GenerateTypeCoercion(coercion),
            TypeCheck check => GenerateTypeCheck(check),
            Parenthesized paren => ParenthesizedExpression(GenerateExpression(paren.Expression)),

            // F-strings and T-strings
            FStringLiteral fstring => GenerateFString(fstring),
            TStringLiteral tstring => GenerateTString(tstring),

            // Try/Maybe expressions
            TryExpression tryExpr => GenerateTryExpression(tryExpr),
            MaybeExpression maybeExpr => GenerateMaybeExpression(maybeExpr),

            // Await expression
            Parser.Ast.AwaitExpression awaitExpr => GenerateAwaitExpression(awaitExpr),

            // Walrus operator
            WalrusExpression walrus => GenerateWalrusExpression(walrus),

            // Early-return ? operator
            QuestionMarkExpression qm => GenerateQuestionMarkExpression(qm),

            // Match expression
            MatchExpression matchExpr => GenerateMatchExpression(matchExpr),

            // Spread/star — normally handled by collection literal and assignment codegen.
            // If reached here, emit a diagnostic — this is an unsupported context.
            SpreadElement spread => EmitNotImplementedExpression(
                "Spread expression (*) is not supported in this context",
                DiagnosticCodes.CodeGen.UnsupportedExpressionType,
                spread.LineStart, spread.ColumnStart),
            StarExpression star => EmitNotImplementedExpression(
                "Star expression (*) is not supported in this context",
                DiagnosticCodes.CodeGen.UnsupportedExpressionType,
                star.LineStart, star.ColumnStart),

            // ModifiedArgument — handled at call-site level in GeneratePositionalArguments.
            // If reached here (standalone context), just emit the inner expression.
            ModifiedArgument modArg => GenerateExpression(modArg.Argument),

            _ => EmitNotImplementedExpression(
                $"Unsupported expression type in code generation: '{expr.GetType().Name}'",
                DiagnosticCodes.CodeGen.UnsupportedExpressionType, expr.LineStart, expr.ColumnStart)
        };
    }

    /// <summary>
    /// Generates a bare <c>None</c> literal as a C# <c>null</c> literal. This converts to
    /// object/nullable targets and forms valid <c>case null:</c> patterns. Coercion of a
    /// bare <c>None</c> to <c>Optional&lt;T&gt;.None</c> is handled at the specific
    /// <em>direct</em> value sites (variable/field initializers, returns, assignments) via
    /// <see cref="TryGenerateBareNoneForOptional"/>, rather than here — using the ambient
    /// target-type context would incorrectly fire for <c>None</c> nested inside call
    /// arguments (e.g. <c>convert(None)</c> against a nullable parameter).
    /// </summary>
    private ExpressionSyntax GenerateNoneLiteral(NoneLiteral noneLiteral)
    {
        // The materialized decision, not a re-derivation: the checker recorded which `None` lands in
        // an OPTIONAL destination and which lands in a nullable one (#1478, Critical Rule 2). Absent
        // for every `None` whose destination is nullable, which is the plain-null default this
        // method has always emitted.
        var optionalTarget = _context.SemanticInfo?.GetOptionalNoneMaterialization(noneLiteral);
        return optionalTarget != null
            ? GenerateOptionalNone(optionalTarget)
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
    }

    /// <summary>
    /// When <paramref name="valueAst"/> is a bare <c>None</c> and <paramref name="targetType"/>
    /// is an <see cref="OptionalType"/>, emits <c>Optional&lt;T&gt;.None</c>. Returns
    /// <c>null</c> otherwise so callers fall back to normal expression generation.
    /// </summary>
    private ExpressionSyntax? TryGenerateBareNoneForOptional(Expression valueAst, SemanticType? targetType)
        => valueAst is NoneLiteral && targetType is OptionalType opt
            ? GenerateOptionalNone(opt)
            : null;

    /// <summary>
    /// Generates a direct initializer/default value expression for a declared target whose
    /// type is given by <paramref name="targetAnnotation"/>. A bare <c>None</c> against an
    /// <see cref="OptionalType"/> target produces <c>Optional&lt;T&gt;.None</c>; everything
    /// else falls back to normal expression generation.
    /// </summary>
    private ExpressionSyntax GenerateInitializerValue(Expression valueAst, TypeAnnotation? targetAnnotation)
    {
        var targetType = targetAnnotation != null
            ? _context.SemanticInfo?.GetTypeAnnotation(targetAnnotation)
            : null;
        return TryGenerateBareNoneForOptional(valueAst, targetType)
            ?? GenerateExpression(valueAst);
    }

    /// <summary>
    /// Generates an identifier expression, with Optional narrowing support.
    /// When a variable has been narrowed from Optional&lt;T&gt; to T (via an is-not-None check),
    /// emits identifier.Unwrap() to extract the underlying value.
    /// </summary>
    private ExpressionSyntax GenerateIdentifierExpression(Identifier name)
    {
        // In an accessor body, the accessor's named incoming value is a MAPPING onto the C# name
        // that carries it, not a declaration — an event handler parameter and a property setter's
        // value parameter both become C#'s implicit `value`, and an observer's parameter becomes
        // `value` or the captured old-value local. Nothing declares the Sharpy spelling, so a
        // reference that reaches the slot lookup below emits an undeclared name (CS0103 behind
        // SPY0908 — #1405). One branch for all three shapes; see AccessorParamRewrite.
        // The match is by spelling, and that is now sound because the rewrite is SCOPED: every
        // binder that re-binds the name suspends it (SuspendAccessorParamRewriteIfShadowed), so a
        // spelling that reaches here can only be the accessor's own value (#1500 — before that
        // guard a shadowing lambda parameter was rewritten too: CPython 106, Sharpy 300, silent).
        // Symbol identity cannot serve here: assigning to the accessor's value makes the checker
        // define a FRESH VariableSymbol for the name (TypeChecker.Statements.cs:178-195), so reads
        // after a rebinding assignment resolve to a symbol that is not the accessor's parameter and
        // yet must still map onto the same slot. The binder, not the symbol, is what shadowing is a
        // property of. Write positions consult AccessorParamSlotName for the same mapping.
        if (_accessorParamRewrite is { } rewrite
            && string.Equals(name.Name, rewrite.Source, StringComparison.Ordinal))
        {
            return IdentifierName(rewrite.Target);
        }

        // A builtin type name the TypeChecker pinned to a concrete signature (#1182). The recorded
        // fact decides the shape and supplies the types; nothing is re-derived here.
        if (_context.SemanticInfo?.GetConstructorReferenceLowering(name) is { } constructorReference)
            return GenerateConstructorReference(name.IsNameBacktickEscaped, constructorReference);

        // #1638: a builtin function name used as a value. The recorded fact carries the selected
        // overload's parameter types so we generate a typed lambda instead of a bare method group —
        // method groups break on struct boxing, generic inference, CS0121 ambiguity, and params elision.
        if (_context.SemanticInfo?.GetCallableReferenceLowering(name) is { } callableRef)
            return GenerateCallableReferenceLambda(callableRef);

        // Builtin function references (e.g., key=len, map(int, items)) need full qualification.
        // Shadowing check: if the semantic analysis resolved this identifier to a VariableSymbol,
        // it's a local variable shadowing the builtin — skip the builtin emission path.
        var resolvedSymbol = _context.SemanticInfo?.GetIdentifierSymbol(name);

        // Inline CLR namespace identifier (e.g., `System` resolved by semantic analysis
        // to a synthetic .NET ModuleSymbol). Emit the namespace name verbatim so chained
        // access produces valid C# (e.g., `System`.Console.WriteLine → System.Console.WriteLine).
        // Bypasses GetMangledVariableName which would otherwise camel-case it to "system".
        if (resolvedSymbol is ModuleSymbol { IsNetModule: true, NetNamespaceName: { } netNamespaceName })
        {
            return IdentifierName(netNamespaceName);
        }

        if (resolvedSymbol is not VariableSymbol)
        {
            var symbol = _context.LookupSymbol(name.Name);
            if (symbol is FunctionSymbol fs && GetCodeGenInfo(fs) == null
                && _context.IsBuiltinFunction(name.Name))
            {
                return MakeGlobalQualifiedName("Sharpy", "Builtins",
                    NameCasing.ResolveMethod(name.Name, name.IsNameBacktickEscaped));
            }

            // Type-name builtin function references (e.g., map(int, items) → Builtins.Int)
            // C# method group conversion handles overload selection automatically.
            if (symbol is TypeSymbol && _context.IsBuiltinFunction(name.Name))
            {
                return MakeGlobalQualifiedName("Sharpy", "Builtins",
                    NameCasing.ResolveMethod(name.Name, name.IsNameBacktickEscaped));
            }

            // Discovered TypeSymbols with CLR type info must resolve through the CLR type name.
            // Sharpy namespace: datetime → global::Sharpy.DateTime
            // System namespace (when inside a user namespace): Math → global::System.Math
            {
                var ts = (resolvedSymbol as TypeSymbol) ?? (symbol as TypeSymbol);
                if (ts?.ClrType != null)
                {
                    // Sharpy namespace (or a sub-namespace such as Sharpy.Generators): emit the
                    // full CLR namespace path so a Sharpy.Sub.X type binds rather than being
                    // mis-qualified to global::Sharpy.X (#1090). For types directly in "Sharpy"
                    // this is byte-identical to the previous two-part emission.
                    if (ClrTypeBridge.SpecialCases.IsSharpyNamespace(ts.ClrType.Namespace))
                    {
                        var fullName = ClrNameHelper.StripArity(ts.ClrType.FullName!);
                        return MakeGlobalQualifiedName(fullName.Split('.'));
                    }

                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        var fullName = ClrNameHelper.StripArity(ts.ClrType.FullName!);
                        return MakeGlobalQualifiedName(fullName.Split('.'));
                    }
                }
            }
        }

        // Prefer the node-keyed symbol when it has CodeGenInfo (locals/parameters after #1560,
        // plus module-level symbols). The node-keyed path sees locals that LookupSymbol cannot
        // after scope collapse. Fall back to GetMangledVariableName for symbols whose CodeGenInfo
        // is keyed on a different instance (e.g., module-level vars where the TypeChecker recorded
        // a distinct VariableSymbol for the reference).
        // Use GetCSharpNameForSymbol which handles _forceModuleLevelFields and all resolution
        // overrides. Prefer the node-keyed resolved symbol (survives scope collapse for locals),
        // fall back to name-based GetMangledVariableName for unresolved identifiers.
        var mangledName = resolvedSymbol != null
            ? GetCSharpNameForSymbol(resolvedSymbol)
            : GetMangledVariableName(name.Name, isNewDeclaration: false, name.IsNameBacktickEscaped);
        if (_parameterNameOverrides != null
            && _parameterNameOverrides.TryGetValue(mangledName, out var overrideName))
            mangledName = overrideName;
        ExpressionSyntax expr = EscapedIdentifierName(mangledName);

        // Apply the narrowed-read accessor the TypeChecker recorded for this identifier node, if any
        // (Optional → .Unwrap(), value-nullable → .Value, reference-nullable → !, isinstance → cast).
        return ApplyNarrowedReadLowering(name, expr);
    }

    /// <summary>
    /// Emits a builtin constructor reference that semantic analysis pinned to a concrete signature
    /// (#1182). A pure application of the recorded fact: the family selects the shape, the pinned
    /// signature supplies the types.
    ///
    /// <para>The conversion families emit the <c>Sharpy.Builtins.X</c> method group, so C#'s own
    /// method-group conversion binds the overload against the pinned delegate type. The collection
    /// families have no such overload set, so they emit a constructor lambda:
    /// <c>() =&gt; new Dict&lt;string, int&gt;()</c> for the empty constructor, and
    /// <c>xs =&gt; new List&lt;int&gt;(xs)</c> for the copy constructor.</para>
    /// </summary>
    private ExpressionSyntax GenerateConstructorReference(
        bool isBacktickEscaped, ConstructorReferenceLowering lowering)
    {
        if (lowering.Family == ConstructorReferenceFamily.Conversion)
        {
            return MakeGlobalQualifiedName("Sharpy", "Builtins",
                NameCasing.ResolveMethod(lowering.Name, isBacktickEscaped));
        }

        var constructed = _typeMapper.MapSemanticType(lowering.ConstructedType);

        // One generated parameter per constructor parameter, forwarded positionally. The arity was
        // hardcoded to 0 and 1 before #1211 — and a ParameterCount >= 2 fell into the arity-1 branch
        // and emitted a ONE-parameter lambda, latent wrong output that was unreachable only because
        // CollectionSignatureSatisfies refuses arity >= 2. User classes routinely take two or more,
        // so the arm is general now and the guard is what will be relaxed.
        var sourceNames = new string[lowering.ParameterCount];
        for (int i = 0; i < sourceNames.Length; i++)
            sourceNames[i] = $"__ctor_source_{_tempVarCounter++}";

        var construction = ObjectCreationExpression(constructed).WithArgumentList(
            ArgumentList(SeparatedList(
                sourceNames.Select(n => Argument(EscapedIdentifierName(n))))));

        // Arity 1 keeps the SimpleLambdaExpression form deliberately: a uniform parenthesized lambda
        // would emit `(x) => …` where every existing emission is `x => …` — semantically identical,
        // but it would move the snapshot for no reason.
        if (sourceNames.Length == 1)
        {
            return SimpleLambdaExpression(
                Parameter(EscapedIdentifier(sourceNames[0])), construction);
        }

        return ParenthesizedLambdaExpression(
            ParameterList(SeparatedList(
                sourceNames.Select(n => Parameter(EscapedIdentifier(n))))),
            construction);
    }

    /// <summary>
    /// Generates an eta-expanded lambda for a builtin function reference (#1638). The lambda
    /// forwards positional arguments to the fully qualified method, e.g.
    /// <c>(__callable_p_0) =&gt; global::Sharpy.Builtins.Len(__callable_p_0)</c>.
    /// A lambda is accepted everywhere a method group was, so this is regression-free.
    /// Parameters are untyped — C# infers from the target delegate at the call site.
    /// </summary>
    private ExpressionSyntax GenerateCallableReferenceLambda(CallableReferenceLowering lowering)
    {
        var paramNames = new string[lowering.ParameterTypes.Count];
        for (int i = 0; i < paramNames.Length; i++)
            paramNames[i] = $"__callable_p_{_tempVarCounter++}";

        var callee = MakeGlobalQualifiedName(lowering.QualifiedName.Split('.'));
        var invocation = InvocationExpression(callee)
            .WithArgumentList(ArgumentList(SeparatedList(
                paramNames.Select(n => Argument(EscapedIdentifierName(n))))));

        if (paramNames.Length == 0)
        {
            return ParenthesizedLambdaExpression(ParameterList(), invocation);
        }

        if (paramNames.Length == 1)
        {
            return SimpleLambdaExpression(
                Parameter(EscapedIdentifier(paramNames[0])), invocation);
        }

        return ParenthesizedLambdaExpression(
            ParameterList(SeparatedList(
                paramNames.Select(n => Parameter(EscapedIdentifier(n))))),
            invocation);
    }

    /// <summary>
    /// Applies the narrowed-read accessor the TypeChecker materialized for <paramref name="node"/>
    /// (#1081). The emitter is a pure applier here: it switches on the recorded
    /// <see cref="NarrowedReadKind"/> and never re-derives narrowing flow. Returns
    /// <paramref name="expr"/> unchanged when the node carries no lowering — unnarrowed reads and
    /// match-arm bindings (whose C# pattern binding already carries the narrowed type).
    /// </summary>
    private ExpressionSyntax ApplyNarrowedReadLowering(Expression node, ExpressionSyntax expr)
    {
        var lowering = _context.SemanticInfo?.GetNarrowedReadLowering(node);
        if (lowering == null)
            return expr;

        switch (lowering.Kind)
        {
            case NarrowedReadKind.NullableValue:
                // Value-type nullable (int?, bool?, etc.) → .Value
                return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expr,
                    IdentifierName("Value"));

            case NarrowedReadKind.NullForgiving:
                // Reference-type nullable (string?, MyClass?, etc.) → !
                // C# only auto-narrows locals after null checks, not fields.
                return PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, expr);

            case NarrowedReadKind.UnwrapOptional:
                // Optional<T> → .Unwrap()
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        expr,
                        IdentifierName(ProtocolConstants.Unwrap)))
                    .WithArgumentList(ArgumentList());

            case NarrowedReadKind.Cast:
                {
                    // isinstance narrowing → parenthesized cast ((Dog)animal) so member access works.
                    // Builtin collections narrow to the non-generic Sharpy.IList/IDict/ISet protocol
                    // interface (#912) so the cast against an object receiver succeeds at runtime.
                    // Invariant: CastTarget is non-null whenever Kind == Cast — every TypeChecker
                    // construction site passes the narrowed type alongside the kind.
                    var castType = lowering.CastTarget is GenericType generic
                        && TryMapBuiltinCollectionToNonGenericInterface(generic.Name) is { } nonGenericInterface
                            ? nonGenericInterface
                            : _typeMapper.MapSemanticType(lowering.CastTarget!);
                    // The narrowing proved the value inhabits the cast target on every path reaching
                    // this read — which also proves it is non-null. Assert that to C#'s nullable flow
                    // with `!` so unboxing casts over nullable receivers (e.g. `(long)obj` where obj
                    // is object?) compile warning-clean (CS8605).
                    return ParenthesizedExpression(CastExpression(
                        castType,
                        PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, expr)));
                }

            default:
                return expr;
        }
    }

    private ExpressionSyntax GenerateIntegerLiteral(IntegerLiteral literal)
    {
        var text = literal.Value.Replace("_", "", StringComparison.Ordinal);

        // The semantic phase typed this literal by suffix + magnitude (#1314, #1320);
        // read its decision and emit the matching C# literal form.
        var semanticType = _context.SemanticInfo?.GetExpressionType(literal);
        if (semanticType is BuiltinType bt)
        {
            try
            {
                var ulongVal = ParseIntegerText(text);
                if (bt.ClrType == typeof(int))
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((int)ulongVal));
                if (bt.ClrType == typeof(long))
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((long)ulongVal));
                if (bt.ClrType == typeof(uint))
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((uint)ulongVal));
                if (bt.ClrType == typeof(ulong))
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(ulongVal));
                return GenerateIntegerLiteralFallback(literal, text);
            }
            catch (OverflowException)
            {
                return GenerateIntegerLiteralFallback(literal, text);
            }
        }

        return GenerateIntegerLiteralFallback(literal, text);
    }

    private static ulong ParseIntegerText(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt64(text[2..], 8);
        if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt64(text[2..], 2);
        return ulong.Parse(text, CultureInfo.InvariantCulture);
    }

    private ExpressionSyntax GenerateIntegerLiteralFallback(IntegerLiteral literal, string text)
    {
        long value;
        try
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            else if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
                value = Convert.ToInt64(text[2..], 8);
            else if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                value = Convert.ToInt64(text[2..], 2);
            else
                value = long.Parse(text, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            _context.Diagnostics.AddError(
                $"Integer literal '{literal.Value}' is too large for a 64-bit integer",
                literal.LineStart, literal.ColumnStart,
                code: DiagnosticCodes.CodeGen.EmitError);
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
        }

        if (literal.Suffix != null)
        {
            if (literal.Suffix.Equals("l", StringComparison.OrdinalIgnoreCase))
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));
            if (literal.Suffix.Equals("ul", StringComparison.OrdinalIgnoreCase)
                || literal.Suffix.Equals("lu", StringComparison.OrdinalIgnoreCase))
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((ulong)value));
            if (literal.Suffix.Equals("u", StringComparison.OrdinalIgnoreCase))
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((uint)value));
        }

        if (value >= int.MinValue && value <= int.MaxValue)
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((int)value));
        else
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));
    }

    private ExpressionSyntax GenerateFloatLiteral(FloatLiteral literal)
    {
        var value = double.Parse(literal.Value, CultureInfo.InvariantCulture);

        if (literal.Suffix != null)
        {
            var text = literal.Value + literal.Suffix;
            if (literal.Suffix.Equals("f", StringComparison.OrdinalIgnoreCase))
                return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal(text, (float)value));
            if (literal.Suffix.Equals("d", StringComparison.OrdinalIgnoreCase))
                return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal(text, value));
            if (literal.Suffix.Equals("m", StringComparison.OrdinalIgnoreCase))
            {
                var decimalValue = decimal.Parse(literal.Value, CultureInfo.InvariantCulture);
                return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal(text, decimalValue));
            }
        }

        // An unsuffixed literal is float64 unless the semantic phase narrowed it — which it does for
        // `x: float32 = 0.1` (#1301). Emitting the double form there is CS0664, so the suffix has to
        // follow the recorded decision rather than the written text.
        if (_context.SemanticInfo?.GetExpressionType(literal) is BuiltinType { Name: "float32" })
        {
            var float32Text = literal.Value.Contains('.', StringComparison.Ordinal)
                || literal.Value.Contains('e', StringComparison.Ordinal)
                || literal.Value.Contains('E', StringComparison.Ordinal)
                ? literal.Value + "f"
                : literal.Value + ".0f";
            return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                Literal(float32Text, (float)value));
        }

        // Append 'd' suffix to force Roslyn to preserve double literal semantics.
        // Without it, Roslyn may normalize whole-number doubles (e.g., 5.0 -> 5).
        var literalText = literal.Value.Contains('.', StringComparison.Ordinal) || literal.Value.Contains('e', StringComparison.Ordinal)
            || literal.Value.Contains('E', StringComparison.Ordinal)
            ? literal.Value + "d"
            : literal.Value + ".0d";
        return LiteralExpression(SyntaxKind.NumericLiteralExpression,
            Literal(literalText, value));
    }

    private ExpressionSyntax GenerateStringLiteral(StringLiteral literal)
    {
        return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal.Value));
    }

    private ExpressionSyntax GenerateBytesLiteral(BytesLiteralExpression literal)
    {
        // Convert each character to a byte literal: b"hello" -> new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 })
        var byteValues = literal.Value.Select(c =>
            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((byte)c)));

        var byteArrayInit = ArrayCreationExpression(
            ArrayType(PredefinedType(Token(SyntaxKind.ByteKeyword)))
                .WithRankSpecifiers(SingletonList(
                    ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                        OmittedArraySizeExpression())))))
            .WithInitializer(
                InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SeparatedList<ExpressionSyntax>(byteValues)));

        return ObjectCreationExpression(ParseQualifiedTypeName(CSharpTypeNames.SharpyBytes))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(byteArrayInit))));
    }

    /// <summary>
    /// Gets the semantic type of an expression from SemanticInfo, if available.
    /// </summary>
    private SemanticType? GetExpressionSemanticType(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        return _context.SemanticInfo?.GetExpressionType(expr);
    }

    /// <summary>
    /// Emits a const-folded value (E3 <c>opt_const_fold</c>, #640) as a C# literal. Integers are boxed
    /// as <c>long</c> and narrowed to <c>int</c> unless the folded type is <c>long</c> (mirroring the
    /// <c>**</c> fold in the power case); doubles and booleans emit directly.
    /// </summary>
    private static ExpressionSyntax EmitFoldedConstant(Lowering.IrConstant constant) => constant.Value switch
    {
        bool b => LiteralExpression(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
        double d => LiteralExpression(SyntaxKind.NumericLiteralExpression, DoubleLiteralToken(d)),
        long l when constant.Type == SemanticType.Long =>
            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(l)),
        long l => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((int)l)),
        _ => throw new System.InvalidOperationException(
            $"Unexpected folded constant value kind: {constant.Value?.GetType().Name ?? "null"}"),
    };

    /// <summary>
    /// Builds a numeric-literal token for a folded <c>double</c> whose text is unambiguously a
    /// floating-point literal — appending <c>.0</c> when the round-trip text has no <c>.</c>/exponent —
    /// so a whole-valued fold like <c>3.0</c> emits as <c>3.0</c>, not the <c>int</c>-typed <c>3</c>
    /// (which could bind a different overload). The token value stays the exact <see cref="double"/>.
    /// </summary>
    private static Microsoft.CodeAnalysis.SyntaxToken DoubleLiteralToken(double d)
    {
        var text = d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (text.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
            text += ".0";
        return Literal(text, d);
    }

    /// <summary>
    /// Generates the desugaring for the postfix <c>?</c> early-return operator.
    ///
    /// <para><b>For <c>Result&lt;T, E&gt;</c>:</b></para>
    /// <code>
    /// var __qm_N = expr;
    /// if (__qm_N.IsErr) return Result&lt;RetOk, RetErr&gt;.Err(__qm_N.UnwrapErr());
    /// // expression value → __qm_N.Unwrap()
    /// </code>
    ///
    /// <para><b>For <c>Optional&lt;T&gt;</c>:</b></para>
    /// <code>
    /// var __qm_N = expr;
    /// if (__qm_N.IsNone) return Optional&lt;RetT&gt;.None;
    /// // expression value → __qm_N.Unwrap()
    /// </code>
    ///
    /// The temp declaration and if-check are hoisted via <see cref="_hoistedStatements"/>
    /// so they appear before the containing statement (same mechanism as walrus operator).
    /// </summary>
    private ExpressionSyntax GenerateQuestionMarkExpression(QuestionMarkExpression qm)
    {
        // Generate the operand expression (may itself contain nested ? operators,
        // which will hoist their own statements first — depth-first recursion)
        var operandExpr = GenerateExpression(qm.Operand);

        // Get the operand's semantic type
        var operandType = GetExpressionSemanticType(qm.Operand);

        // Generate unique temp variable name
        var tempName = $"__qm_{_tempVarCounter++}";

        // Hoist: var __qm_N = operandExpr;
        _hoistedStatements.Add(
            LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(tempName))
                            .WithInitializer(EqualsValueClause(operandExpr))))));

        if (operandType is ResultType resultType)
        {
            // Get the enclosing function's return type (must be ResultType — validated by semantic analysis)
            var returnResultType = _currentReturnType as ResultType;

            var retOkTypeSyntax = _typeMapper.MapSemanticType(returnResultType!.OkType);
            var retErrTypeSyntax = _typeMapper.MapSemanticType(returnResultType.ErrorType);

            // Result<RetOk, RetErr>
            var resultGenericName = GenericName("Result")
                .WithTypeArgumentList(TypeArgumentList(
                    SeparatedList<TypeSyntax>(new[] { retOkTypeSyntax, retErrTypeSyntax })));

            // __qm_N.UnwrapErr()
            var unwrapErrCall = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(tempName),
                    IdentifierName("UnwrapErr")));

            // Result<RetOk, RetErr>.Err(__qm_N.UnwrapErr())
            var errFactory = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    resultGenericName,
                    IdentifierName("Err")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(unwrapErrCall))));

            // Hoist: if (__qm_N.IsErr) return Result<RetOk, RetErr>.Err(__qm_N.UnwrapErr());
            _hoistedStatements.Add(
                IfStatement(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(tempName),
                        IdentifierName("IsErr")),
                    ReturnStatement(errFactory)));
        }
        else if (operandType is OptionalType)
        {
            // Get the enclosing function's return type (must be OptionalType — validated by semantic analysis)
            var returnOptionalType = _currentReturnType as OptionalType;

            var retUnderlyingTypeSyntax = _typeMapper.MapSemanticType(returnOptionalType!.UnderlyingType);

            // Optional<RetT>
            var optionalGenericName = GenericName("Optional")
                .WithTypeArgumentList(TypeArgumentList(
                    SingletonSeparatedList<TypeSyntax>(retUnderlyingTypeSyntax)));

            // Optional<RetT>.None
            var noneAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                optionalGenericName,
                IdentifierName("None"));

            // Hoist: if (__qm_N.IsNone) return Optional<RetT>.None;
            _hoistedStatements.Add(
                IfStatement(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(tempName),
                        IdentifierName("IsNone")),
                    ReturnStatement(noneAccess)));
        }

        // Expression value: __qm_N.Unwrap()
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(tempName),
                IdentifierName("Unwrap")));
    }

    /// <summary>
    /// Returns true if the type represents <c>object</c> (System.Object).
    /// Uses name-based comparison because SemanticType.Object is a UserDefinedType
    /// and may not be reference-equal to discovery-loaded instances.
    /// </summary>
    private static bool IsObjectType(SemanticType? type) =>
        type != null && type.GetDisplayName() == "object";

    /// <summary>
    /// Extracts the element type from an iterable semantic type.
    /// E.g., GenericType("list", [int]) → int; GenericType("IEnumerable", [string]) → string.
    /// Returns null if the element type cannot be determined.
    /// </summary>
    private static SemanticType? ExtractElementType(SemanticType? type)
    {
        if (type is GenericType gt && gt.TypeArguments.Count >= 1
            && gt.TypeArguments[0] is not UnknownType)
        {
            return gt.TypeArguments[0];
        }
        return null;
    }

    /// <summary>
    /// Tries to infer the element type for a collection constructor call like <c>list(d.keys())</c>
    /// by examining the argument's AST structure. When the semantic type of the argument is
    /// unknown (e.g., property access on discovery-loaded types), this extracts the element type
    /// from the receiver's generic type arguments (#555).
    /// </summary>
    private SemanticType? TryInferElementTypeFromArg(Parser.Ast.Expression arg)
    {
        // First try the direct semantic type (skip if it resolved to object — too generic)
        var directType = ExtractElementType(GetExpressionSemanticType(arg));
        if (directType != null && !IsObjectType(directType))
            return directType;

        // For list(inner) / set(inner) wrapping, recurse on the inner argument
        if (arg is FunctionCall { Function: Identifier wrapperId } wrapperCall
            && wrapperCall.Arguments.Length == 1
            && wrapperId.Name is BuiltinNames.List or BuiltinNames.Set)
        {
            return TryInferElementTypeFromArg(wrapperCall.Arguments[0]);
        }

        // For d.keys() / d.values() on a dict-like type, extract from the receiver's generic args
        if (arg is FunctionCall { Function: MemberAccess ma })
        {
            var receiverType = GetExpressionSemanticType(ma.Object);
            if (receiverType is GenericType receiverGeneric && receiverGeneric.TypeArguments.Count >= 2)
            {
                return ma.Member switch
                {
                    "keys" => receiverGeneric.TypeArguments[0] is not UnknownType
                        ? receiverGeneric.TypeArguments[0] : null,
                    "values" => receiverGeneric.TypeArguments[1] is not UnknownType
                        ? receiverGeneric.TypeArguments[1] : null,
                    _ => null
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Wraps an expression used in a truthiness context with the conversion the semantic checker
    /// recorded (#1558). Reads the <see cref="TruthinessLowering"/> tag — never re-derives from type.
    /// </summary>
    private ExpressionSyntax WrapTruthinessIfNeeded(ExpressionSyntax expr, Parser.Ast.Expression astExpr)
    {
        var lowering = _context.SemanticInfo?.GetTruthinessLowering(astExpr);
        if (lowering == null)
            return expr;

        return lowering.Value switch
        {
            TruthinessLowering.NativeBool => expr,
            TruthinessLowering.IntNotZero => BinaryExpression(SyntaxKind.NotEqualsExpression,
                Operand(expr, SyntaxKind.NotEqualsExpression, OperandSlot.Left),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))),
            TruthinessLowering.FloatNotZero => BinaryExpression(SyntaxKind.NotEqualsExpression,
                Operand(expr, SyntaxKind.NotEqualsExpression, OperandSlot.Left),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0.0))),
            TruthinessLowering.LongNotZero => BinaryExpression(SyntaxKind.NotEqualsExpression,
                Operand(expr, SyntaxKind.NotEqualsExpression, OperandSlot.Left),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0L))),
            TruthinessLowering.StringNotEmpty => BinaryExpression(SyntaxKind.GreaterThanExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    Operand(expr, SyntaxKind.SimpleMemberAccessExpression, OperandSlot.Receiver),
                    IdentifierName("Length")),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))),
            TruthinessLowering.BytesNotEmpty or TruthinessLowering.CollectionNotEmpty
                or TruthinessLowering.SizedNotEmpty =>
                BinaryExpression(SyntaxKind.GreaterThanExpression,
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        ParenthesizedExpression(CastExpression(
                            MakeGlobalQualifiedName("Sharpy", "ISized"),
                            Operand(expr, SyntaxKind.CastExpression, OperandSlot.CastOperand))),
                        IdentifierName("Count")),
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))),
            TruthinessLowering.OptionalIsSome => MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                Operand(expr, SyntaxKind.SimpleMemberAccessExpression, OperandSlot.Receiver),
                IdentifierName("IsSome")),
            TruthinessLowering.NullableNotNull => BinaryExpression(SyntaxKind.NotEqualsExpression,
                Operand(expr, SyntaxKind.NotEqualsExpression, OperandSlot.Left),
                LiteralExpression(SyntaxKind.NullLiteralExpression)),
            TruthinessLowering.BoolConvertible => MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                Operand(expr, SyntaxKind.SimpleMemberAccessExpression, OperandSlot.Receiver),
                IdentifierName("IsTrue")),
            TruthinessLowering.AlwaysFalse => LiteralExpression(SyntaxKind.FalseLiteralExpression),
            _ => expr
        };
    }

    /// <summary>
    /// Checks if a function call is a tagged union constructor (Some, Ok, Err)
    /// by checking the expression's semantic type from SemanticInfo.
    /// </summary>
    private bool IsTaggedUnionConstructorCall(FunctionCall call)
    {
        if (call.Function is not Identifier id)
            return false;

        if (id.Name is not ("Some" or "Ok" or "Err"))
            return false;

        var exprType = GetExpressionSemanticType(call);
        return exprType is OptionalType or ResultType;
    }

    private ExpressionSyntax GenerateAwaitExpression(Parser.Ast.AwaitExpression awaitExpr)
    {
        var operand = GenerateExpression(awaitExpr.Operand);
        return SyntaxFactory.AwaitExpression(operand);
    }
}
