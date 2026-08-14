using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: List, set, dict comprehensions and supporting helpers
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Runs <paramref name="generate"/> against a fresh <c>_hoistedStatements</c> scope and returns
    /// whatever statements it hoisted (e.g. a nested or async comprehension's <c>await foreach</c> +
    /// temp declaration), restoring the prior hoist accumulator before returning. Mirrors the
    /// save/clear/restore discipline in <c>GenerateBodyStatements</c>. Imperative comprehension
    /// lowering uses this to place a sub-expression's hoisting at the lexical scope where the
    /// sub-expression is evaluated — element/key/value inside the innermost loop body (rebuilt per
    /// outer iteration, with the loop variable in scope), iterator/condition before their loop —
    /// instead of leaking it to the flat statement boundary above the outer loop (#1000).
    /// </summary>
    private List<StatementSyntax> CaptureHoisted(System.Action generate)
    {
        var saved = new List<StatementSyntax>(_hoistedStatements);
        _hoistedStatements.Clear();
        generate();
        var captured = new List<StatementSyntax>(_hoistedStatements);
        _hoistedStatements.Clear();
        _hoistedStatements.AddRange(saved);
        return captured;
    }

    private ExpressionSyntax GenerateListComprehension(ListComprehension listComp)
        => GenerateImperativeComprehension(
            listComp, listComp.Clauses, listComp.Element, null, null, BuiltinNames.List);

    private ExpressionSyntax GenerateSetComprehension(SetComprehension setComp)
        => GenerateImperativeComprehension(
            setComp, setComp.Clauses, setComp.Element, null, null, BuiltinNames.Set);

    private ExpressionSyntax GenerateDictComprehension(DictComprehension dictComp)
        => GenerateImperativeComprehension(
            dictComp, dictComp.Clauses, null, dictComp.Key, dictComp.Value, BuiltinNames.Dict);

    /// <summary>
    /// Reads the lowered comprehension node for <paramref name="comprehension"/> from the lowering IR
    /// (E2 #1056, migrates the comprehension transform). Returns <c>null</c> when the IR was not built
    /// for this codegen path (the REPL and the source-generator sub-pipeline do not build it, and
    /// direct-emitter unit tests construct a context without it); callers then recompute the decisions
    /// via the shared <see cref="LoweringPass"/> helpers so output is unchanged.
    /// </summary>
    private IrLoweredLoop? GetIrLoweredLoop(Expression comprehension)
    {
        // Prefer the E3 comprehension pass's optimized copy (opt_comprehension_fusion, #1057) when it
        // marked this comprehension; the map is empty unless the pass ran, so the default path reads
        // the initial-lowering copy from the index and is byte-identical.
        if (_context.Ir?.OptimizedComprehensions.TryGetValue(comprehension, out var optimized) == true)
            return optimized;

        return _context.Ir?.Index.TryGetValue(comprehension, out var node) == true
            && node is IrLoweredLoop loop
            ? loop
            : null;
    }

    /// <summary>
    /// Builds the constructor capacity argument for a product-of-counts presized comprehension (E3
    /// <c>opt_comprehension_fusion</c>): <c>((global::Sharpy.ISized)src1).Count * … * ((..)srcN).Count</c>
    /// over the <c>for</c>-clause sources. The pass guarantees every source is sized and effect-free, so
    /// re-generating each here (also generated during iteration) is safe. The <c>ISized</c> cast reads
    /// the interface Count uniformly (a plain <c>.Count</c> on <c>List&lt;T&gt;</c> would bind LINQ).
    /// </summary>
    private ArgumentListSyntax BuildProductCapacityArgs(ImmutableArray<ComprehensionClause> clauses)
    {
        ExpressionSyntax? product = null;
        foreach (var clause in clauses)
        {
            if (clause is not ForClause forClause)
                continue;

            var count = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ParenthesizedExpression(CastExpression(
                    MakeGlobalQualifiedName("Sharpy", "ISized"),
                    GenerateExpression(forClause.Iterator))),
                IdentifierName("Count"));

            product = product is null
                ? count
                : BinaryExpression(SyntaxKind.MultiplyExpression, product, count);
        }

        return ArgumentList(SingletonSeparatedList(Argument(product!)));
    }

    /// <summary>
    /// Builds the fully-resolved C# result collection type for a comprehension of
    /// <paramref name="collectionKind"/> from its <paramref name="resultType"/>
    /// (<c>list[T]</c>/<c>set[T]</c> → <c>Sharpy.List/Set&lt;T&gt;</c>, <c>dict[K,V]</c> →
    /// <c>Sharpy.Dict&lt;K,V&gt;</c>). The result-collection type is a syntax mapping; the decision of
    /// <em>which</em> semantic type it comes from is made in the lowering pass (on the IR node) and
    /// only mapped to syntax here.
    /// </summary>
    private TypeSyntax BuildComprehensionCollectionType(string collectionKind, SemanticType? resultType)
        => collectionKind switch
        {
            BuiltinNames.Set => TypeSyntaxMapper.QualifiedGenericName(
                CSharpTypeNames.SharpySet, MapComprehensionTypeArgument(resultType, 0)),
            BuiltinNames.Dict => TypeSyntaxMapper.QualifiedGenericName(
                CSharpTypeNames.SharpyDict,
                MapComprehensionTypeArgument(resultType, 0),
                MapComprehensionTypeArgument(resultType, 1)),
            _ => TypeSyntaxMapper.QualifiedGenericName(
                CSharpTypeNames.SharpyList, MapComprehensionTypeArgument(resultType, 0)),
        };

    /// <summary>
    /// Maps type argument <paramref name="index"/> of a comprehension's own (already type-checked)
    /// result type — <c>list[T]</c>/<c>set[T]</c> → T, <c>dict[K,V]</c> → K/V — to C# syntax.
    /// The result type is supplied by the lowering pass (via <see cref="IrLoweredLoop.Type"/>) or, on
    /// null-IR codegen paths, recomputed from <c>SemanticInfo</c>; reading the comprehension's own
    /// result type (not the element/key/value node) is what makes spread elements correct, since a
    /// spread node caches the *collection* type it flattens while the comprehension result carries the
    /// unwrapped element type. Falls back to <c>object</c> when the type is unresolved (error recovery).
    /// </summary>
    private TypeSyntax MapComprehensionTypeArgument(SemanticType? resultType, int index)
    {
        if (resultType is GenericType generic
            && index < generic.TypeArguments.Count
            && generic.TypeArguments[index] is not UnknownType)
        {
            return _typeMapper.MapSemanticType(generic.TypeArguments[index]);
        }

        return PredefinedType(Token(SyntaxKind.ObjectKeyword));
    }

    /// <summary>
    /// Generates a comprehension for-clause iterator expression, applying the same enum-iteration
    /// lowering used by for-statements: <c>for c in Color</c> iterates the enum's <em>values</em>
    /// via <c>Enum.GetValues&lt;Color&gt;()</c>. All other sources pass through unchanged. Call
    /// inside a <see cref="CaptureHoisted"/> scope so the iterator's own hoisted sub-statements
    /// (e.g. a nested comprehension used as the iterable) land in the enclosing scope.
    /// </summary>
    private ExpressionSyntax GenerateComprehensionIterator(Expression iterator)
    {
        var iterExpr = GenerateExpression(iterator);
        if (GetExpressionSemanticType(iterator) is Semantic.UserDefinedType
            { Symbol.TypeKind: Semantic.TypeKind.Enum } enumUdt)
        {
            iterExpr = GenerateEnumValuesIterator(enumUdt);
        }

        return iterExpr;
    }

    private ExpressionSyntax GenerateDictSpreadComprehension(DictSpreadComprehension dictSpreadComp)
    {
        var tempName = GenerateTempVarName("comp");

        // Determine K, V from the semantic type of the spread value (dict[K,V])
        var spreadSemanticType = GetExpressionSemanticType(dictSpreadComp.Spread);
        TypeSyntax kTypeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        TypeSyntax vTypeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));

        if (spreadSemanticType is GenericType gDictType
            && gDictType.Name == BuiltinNames.Dict
            && gDictType.TypeArguments.Count >= 2)
        {
            kTypeSyntax = _typeMapper.MapSemanticType(gDictType.TypeArguments[0]);
            vTypeSyntax = _typeMapper.MapSemanticType(gDictType.TypeArguments[1]);
        }

        var dictType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpyDict, kTypeSyntax, vTypeSyntax);

        // var __comp_N = new Dict<K,V>();
        var tempDecl = LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(tempName))
                        .WithInitializer(EqualsValueClause(
                            ObjectCreationExpression(dictType)
                                .WithArgumentList(ArgumentList()))))));

        // Inner statement: __comp_N.Update(spread)
        // #1000: capture any hoisting from the spread expression (e.g. a nested async comprehension)
        // so it nests inside the innermost loop body, rebuilt per outer iteration.
        StatementSyntax innerStmt = null!;
        var spreadHoisted = CaptureHoisted(() =>
        {
            var spreadExpr = GenerateExpression(dictSpreadComp.Spread);
            innerStmt = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(tempName),
                        IdentifierName("Update")))
                .AddArgumentListArguments(Argument(spreadExpr)));
        });

        var currentBody = new List<StatementSyntax>(spreadHoisted) { innerStmt };

        // Build nested loop structure from clauses in reverse order
        for (int i = dictSpreadComp.Clauses.Length - 1; i >= 0; i--)
        {
            switch (dictSpreadComp.Clauses[i])
            {
                case IfClause ifClause:
                    // #1000: hoist condition sub-statements before the if, in the enclosing scope.
                    ExpressionSyntax condition = null!;
                    var condHoisted = CaptureHoisted(() => condition = GenerateExpression(ifClause.Condition));
                    currentBody = new List<StatementSyntax>(condHoisted) { IfStatement(condition, Block(currentBody)) };
                    break;

                case ForClause forClause:
                    // #1000: hoist iterator sub-statements before the foreach, in the enclosing scope.
                    ExpressionSyntax iterExpr = null!;
                    var iterHoisted = CaptureHoisted(() => iterExpr = GenerateExpression(forClause.Iterator));

                    if (forClause.Target is Identifier id)
                    {
                        var loopVar = LocalBaseName(id.Name, id.IsNameBacktickEscaped);
                        var tempLoopVar = GenerateTempVarName("loopVar");

                        _declaredVariables.Add(loopVar);
                        // TODO(#1498): this claim is UNSCOPED — it writes the enclosing function's
                        // slot table with no save/restore, unlike GenerateImperativeComprehension.
                        // A loop target reusing an enclosing local's name is CS0136 behind SPY0908,
                        // and the outer slot's version is reset with no restore.
                        RegisterLocalSlot(loopVar, id.Name);

                        var varInit = LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("var"))
                                .WithVariables(SingletonSeparatedList(
                                    VariableDeclarator(EscapedIdentifier(loopVar))
                                        .WithInitializer(EqualsValueClause(IdentifierName(tempLoopVar))))));

                        var foreachBody = new List<StatementSyntax> { varInit };
                        foreachBody.AddRange(currentBody);

                        var foreachStmt = ForEachStatement(
                            IdentifierName("var"),
                            Identifier(tempLoopVar),
                            iterExpr,
                            Block(foreachBody));
                        if (forClause.IsAsync)
                            foreachStmt = foreachStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword));
                        currentBody = new List<StatementSyntax>(iterHoisted) { foreachStmt };
                    }
                    else if (forClause.Target is TupleLiteral tuple && tuple.Elements.All(e => e is Identifier))
                    {
                        var tempLoopVar = GenerateTempVarName("loopVar");
                        var tupleIds = tuple.Elements.Cast<Identifier>().ToList();
                        var tupleVars = tupleIds.Select(e => LocalBaseName(e.Name, e.IsNameBacktickEscaped)).ToList();

                        foreach (var tupleId in tupleIds)
                        {
                            var tv = LocalBaseName(tupleId.Name, tupleId.IsNameBacktickEscaped);
                            _declaredVariables.Add(tv);
                            RegisterLocalSlot(tv, tupleId.Name);
                        }

                        var designations = tupleVars
                            .Select(name => (VariableDesignationSyntax)SingleVariableDesignation(EscapedIdentifier(name)))
                            .ToList();
                        var deconstructStmt = ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                DeclarationExpression(
                                    IdentifierName("var"),
                                    ParenthesizedVariableDesignation(SeparatedList(designations))),
                                IdentifierName(tempLoopVar)));

                        var foreachBody = new List<StatementSyntax> { deconstructStmt };
                        foreachBody.AddRange(currentBody);

                        var foreachStmt = ForEachStatement(
                            IdentifierName("var"),
                            Identifier(tempLoopVar),
                            iterExpr,
                            Block(foreachBody));
                        if (forClause.IsAsync)
                            foreachStmt = foreachStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword));
                        currentBody = new List<StatementSyntax>(iterHoisted) { foreachStmt };
                    }
                    break;
            }
        }

        _hoistedStatements.Add(tempDecl);
        _hoistedStatements.AddRange(currentBody);

        return IdentifierName(tempName);
    }

    /// <summary>
    /// Generates imperative codegen for a list/set/dict comprehension: a temp collection, nested
    /// <c>foreach</c> loops with <c>.Add()</c>/<c>[key] = value</c> calls, hoisted via
    /// <c>_hoistedStatements</c>, returning the temp identifier. Used for every comprehension —
    /// single-<c>for</c> included, replacing the former LINQ <c>Where</c>/<c>Select</c> chain — so a
    /// comprehension allocates no intermediate LINQ iterators or delegates.
    ///
    /// <para>
    /// The emission <b>decisions</b> — the result collection type, the single-<c>for</c> clause, the
    /// D4 sized-source capacity decision, and the spread-vs-add flag — are read from the lowered
    /// <see cref="IrLoweredLoop"/> when the IR was built for this path (E2 #1056), or recomputed via
    /// the shared <see cref="LoweringPass"/> helpers otherwise. The stateful emission below (temp
    /// naming, hoisting, loop-variable scoping) stays here, which is what keeps output byte-identical.
    /// </para>
    ///
    /// <para>
    /// When there is exactly one <c>for</c> clause and its source is a sized collection, the source is
    /// hoisted into a temp and the result collection is constructed with that source's <c>Count</c> as
    /// its capacity, so <c>.Add()</c> never reallocates the backing store. Hoisting the source also
    /// guarantees single evaluation of a side-effecting iterable (its <c>Count</c> and its enumeration
    /// read the same temp).
    /// </para>
    /// </summary>
    /// <param name="comprehension">The comprehension AST node, used to look up its lowered IR node.</param>
    /// <param name="clauses">The comprehension clauses (for/if)</param>
    /// <param name="element">Element expression for list/set comprehensions (null for dict)</param>
    /// <param name="keyExpr">Key expression for dict comprehensions (null for list/set)</param>
    /// <param name="valueExpr">Value expression for dict comprehensions (null for list/set)</param>
    /// <param name="collectionKind">BuiltinNames.List, BuiltinNames.Set, or BuiltinNames.Dict</param>
    private ExpressionSyntax GenerateImperativeComprehension(
        Expression comprehension,
        ImmutableArray<ComprehensionClause> clauses,
        Expression? element,
        Expression? keyExpr,
        Expression? valueExpr,
        string collectionKind)
    {
        // Emission decisions: read from the lowered IR node when present, else recompute (null-IR
        // codegen paths — REPL, source generators, direct-emitter unit tests). Both yield the same
        // values, so emitted C# is byte-identical.
        var loop = GetIrLoweredLoop(comprehension);
        var resultType = loop != null ? loop.Type : GetExpressionSemanticType(comprehension);
        var elementIsSpread = loop != null ? loop.ElementIsSpread : element is SpreadElement;
        var collectionType = BuildComprehensionCollectionType(collectionKind, resultType);

        var tempName = GenerateTempVarName("comp");

        CollectionTypeRegistry.TryGet(collectionKind, out var collInfo);

        // Capacity preallocation: a single-for comprehension over a sized source presizes the result
        // to the source's element count. The source is hoisted into __src_N so its Count (used as the
        // constructor capacity) and its enumeration reference one evaluation.
        var soleForClause = loop != null ? loop.SoleForClause : LoweringPass.SingleForClause(clauses);
        string? sourceTempName = null;
        StatementSyntax? sourceDecl = null;
        List<StatementSyntax>? sourceHoisted = null;
        ArgumentListSyntax capacityArgs = ArgumentList();

        // The sized-source semantic type when D4 preallocation applies, else null. On the IR path the
        // decision is encoded by IrLoweredLoop.Capacity (whose Type is the source type); otherwise the
        // shared lowering helper recomputes it. Non-null implies a single sized for-clause source.
        SemanticType? sizedSourceType = loop != null
            ? loop.Capacity?.Type
            : soleForClause != null && LoweringPass.IsSizedComprehensionSource(GetExpressionSemanticType(soleForClause.Iterator))
                ? GetExpressionSemanticType(soleForClause.Iterator)
                : null;

        // Product-of-counts presize (E3 opt_comprehension_fusion, #1057): the pass marks a multi-for
        // comprehension whose sources are all sized + effect-free with no filter. Presize the result to
        // the product of the sources' Counts, re-evaluating each (effect-free) source; the loop structure
        // below is unchanged (no hoisting — sourceTempName stays null and every clause iterates normally).
        if (loop?.ProductPreallocation == true)
        {
            capacityArgs = BuildProductCapacityArgs(clauses);
        }
        else if (sizedSourceType != null)
        {
            sourceTempName = GenerateTempVarName("src");
            ExpressionSyntax sourceExpr = null!;
            sourceHoisted = CaptureHoisted(() => sourceExpr = GenerateComprehensionIterator(soleForClause!.Iterator));
            // Declare the hoisted source with its mapped Sharpy collection type rather than `var`:
            // a semantic list[T] can be backed by a CLR array at the C# level (e.g. str.split
            // returns string[]), which does not implement ISized — the typed declaration coerces
            // such sources through the collection's implicit array conversion so the ISized
            // capacity cast below always compiles. Sharpy-backed sources bind by identity.
            // range(...) keeps `var` (RangeIterator is never interop-backed).
            var sourceDeclType = sizedSourceType is GenericType
                ? _typeMapper.MapSemanticType(sizedSourceType)
                : IdentifierName("var");
            sourceDecl = LocalDeclarationStatement(
                VariableDeclaration(sourceDeclType)
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(EscapedIdentifier(sourceTempName))
                            .WithInitializer(EqualsValueClause(sourceExpr)))));
            // ((global::Sharpy.ISized)__src_N).Count — List<T> implements Count only explicitly
            // (per interface), so a plain __src_N.Count would bind to LINQ's Count() method group;
            // the ISized cast reads the public interface property directly, uniformly for every
            // sized source, with no boxing or reflection.
            capacityArgs = ArgumentList(SingletonSeparatedList(Argument(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParenthesizedExpression(CastExpression(
                        MakeGlobalQualifiedName("Sharpy", "ISized"),
                        IdentifierName(sourceTempName))),
                    IdentifierName("Count")))));
        }

        // var __comp_N = new CollectionType(<capacity?>);
        var tempDecl = LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(tempName))
                        .WithInitializer(EqualsValueClause(
                            ObjectCreationExpression(collectionType)
                                .WithArgumentList(capacityArgs))))));

        // Comprehension loop variables are scoped to the comprehension: they must neither collide
        // with nor leak into the enclosing scope (Python semantics). Snapshot the variable-version
        // state now and restore it once the loops are built, so a loop var reusing an enclosing name
        // is bound to a fresh C# name (x_1) while the enclosing binding is left untouched.
        var scopeSnapshot = SaveScope();

        // A comprehension target re-binds its name for the comprehension's extent, so an accessor
        // value-parameter rewrite in force outside must not reach inside it (#1500 — measured
        // before the guard: CPython 110, Sharpy 1100, no diagnostic). Opened exactly where the
        // comprehension's own scope opens, so the presized sole source hoisted above — the
        // outermost iterable, which Python evaluates in the ENCLOSING scope — keeps the rewrite.
        using var targetShadowing =
            SuspendAccessorParamRewriteIfShadowed(ComprehensionTargetNames(clauses));

        // Forward pass (outer→inner): register each for-clause's loop variable(s) and pre-generate
        // every clause's sub-expressions (iterator, filter condition). Registering the loop variables
        // BEFORE the element/key/value and the conditions are generated is what makes references
        // resolve to the exact (possibly versioned) names bound here — the previous inside-out order
        // generated the element while the loop variable was still unbound.
        var loopBindings = new List<StatementSyntax>?[clauses.Length];
        var loopTempVars = new string?[clauses.Length];
        var iterExprs = new ExpressionSyntax?[clauses.Length];
        var iterHoists = new List<StatementSyntax>?[clauses.Length];
        var condExprs = new ExpressionSyntax?[clauses.Length];
        var condHoists = new List<StatementSyntax>?[clauses.Length];

        for (int i = 0; i < clauses.Length; i++)
        {
            switch (clauses[i])
            {
                case ForClause forClause:
                    // #1000: capture the iterator's own hoisted sub-statements so they land before the
                    // foreach, in the enclosing scope where the iterator is evaluated.
                    if (sourceTempName != null && ReferenceEquals(forClause, soleForClause))
                    {
                        // Single-for over a sized source: iterate the presized source temp already
                        // generated (with its own hoisting) above the result-collection declaration.
                        iterExprs[i] = IdentifierName(sourceTempName);
                        iterHoists[i] = new List<StatementSyntax>();
                    }
                    else
                    {
                        ExpressionSyntax generatedIter = null!;
                        iterHoists[i] = CaptureHoisted(() => generatedIter = GenerateComprehensionIterator(forClause.Iterator));
                        iterExprs[i] = generatedIter;
                    }

                    var tempLoopVar = GenerateTempVarName("loopVar");
                    loopTempVars[i] = tempLoopVar;
                    var binding = new List<StatementSyntax>();
                    BindComprehensionLoopTarget(forClause.Target, tempLoopVar, binding);
                    loopBindings[i] = binding;
                    break;

                case IfClause ifClause:
                    // #1000: hoist condition sub-statements (e.g. a nested async comprehension in the
                    // filter) before the if, in the enclosing scope where the condition is evaluated.
                    ExpressionSyntax condition = null!;
                    condHoists[i] = CaptureHoisted(() => condition = GenerateExpression(ifClause.Condition));
                    condExprs[i] = condition;
                    break;
            }
        }

        // Build the innermost statement: __comp_N.Add(element), __comp_N.Extend(it), or __comp_N[key] = value.
        // Generated after the forward pass so element/key/value references resolve to the innermost
        // loop-variable bindings. #1000: capture any statements hoisted while generating them (e.g. an
        // inner async or nested comprehension's await-foreach + temp decl) so they nest INSIDE the
        // innermost loop body — rebuilt per outer iteration, with the loop variable in scope.
        StatementSyntax innerStmt = null!;
        var elementHoisted = CaptureHoisted(() =>
        {
            if (collectionKind == BuiltinNames.Dict)
            {
                // __comp_N[key] = value;
                innerStmt = ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        ElementAccessExpression(IdentifierName(tempName))
                            .WithArgumentList(BracketedArgumentList(
                                SingletonSeparatedList(Argument(GenerateExpression(keyExpr!))))),
                        GenerateExpression(valueExpr!)));
            }
            else if (elementIsSpread && element is SpreadElement spreadElem)
            {
                // PEP 798: __comp_N.Extend(it) / __comp_N.UnionWith(it) for spread elements
                innerStmt = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(tempName),
                            IdentifierName(collInfo!.SpreadMethodName)))
                        .AddArgumentListArguments(Argument(GenerateExpression(spreadElem.Value))));
            }
            else
            {
                // __comp_N.Add(element);
                innerStmt = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(tempName),
                            IdentifierName(collInfo!.AddMethodName)))
                        .AddArgumentListArguments(Argument(GenerateExpression(element!))));
            }
        });

        // Reverse assembly (inner→outer): pure structure building from the pre-generated pieces.
        // Clauses are [for x in iter1, if cond1, for y in iter2, if cond2, ...]; wrap innerStmt from
        // the last clause backward.
        var currentBody = new List<StatementSyntax>(elementHoisted) { innerStmt };

        for (int i = clauses.Length - 1; i >= 0; i--)
        {
            switch (clauses[i])
            {
                case IfClause:
                    var ifStmt = IfStatement(condExprs[i]!, Block(currentBody));
                    currentBody = new List<StatementSyntax>(condHoists[i]!) { ifStmt };
                    break;

                case ForClause forClause:
                    var foreachBody = new List<StatementSyntax>(loopBindings[i]!);
                    foreachBody.AddRange(currentBody);

                    var foreachStmt = ForEachStatement(
                        IdentifierName("var"),
                        Identifier(loopTempVars[i]!),
                        iterExprs[i]!,
                        Block(foreachBody));
                    if (forClause.IsAsync)
                        foreachStmt = foreachStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword));
                    currentBody = new List<StatementSyntax>(iterHoists[i]!) { foreachStmt };
                    break;
            }
        }

        // Comprehension variables were scoped to the comprehension; drop them from the enclosing scope.
        RestoreScope(scopeSnapshot);

        // Hoist: presized source temp (single-for) + temp declaration + outermost loop
        if (sourceDecl != null)
        {
            _hoistedStatements.AddRange(sourceHoisted!);
            _hoistedStatements.Add(sourceDecl);
        }

        _hoistedStatements.Add(tempDecl);
        _hoistedStatements.AddRange(currentBody);

        return IdentifierName(tempName);
    }

    /// <summary>
    /// Registers and declares a comprehension for-clause target's loop variable(s), binding them to
    /// <paramref name="sourceVar"/> (the per-iteration element temp): <c>x</c> → <c>var x = src;</c>,
    /// a flat tuple <c>(a, b)</c> → <c>var (a, b) = src;</c>, a nested tuple recursively via
    /// <c>src.ItemN</c>. Names are always fresh declarations resolved through
    /// <see cref="GetMangledVariableName"/>, so a name colliding with an enclosing local is versioned
    /// (<c>x_1</c>) rather than reassigning or shadowing it — comprehension variables do not leak.
    /// </summary>
    /// <summary>
    /// Every name a comprehension's for-clause targets bind, including tuple-destructuring elements.
    /// Used to decide whether an enclosing accessor-parameter rewrite is shadowed (#1500); the shape
    /// mirrors <see cref="BindComprehensionLoopTarget"/>, which is what actually declares them.
    /// </summary>
    private static IEnumerable<string> ComprehensionTargetNames(
        ImmutableArray<ComprehensionClause> clauses)
    {
        foreach (var clause in clauses)
        {
            if (clause is not ForClause forClause)
                continue;

            foreach (var name in TargetBoundNames(forClause.Target))
                yield return name;
        }
    }

    private static IEnumerable<string> TargetBoundNames(Expression target)
    {
        switch (target)
        {
            case Identifier id:
                yield return id.Name;
                break;

            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                {
                    foreach (var name in TargetBoundNames(element))
                        yield return name;
                }

                break;
        }
    }

    private void BindComprehensionLoopTarget(Expression target, string sourceVar, List<StatementSyntax> statements)
    {
        switch (target)
        {
            case Identifier id:
                statements.Add(DeclareComprehensionVar(
                    GetMangledVariableName(id.Name, isNewDeclaration: true, id.IsNameBacktickEscaped),
                    IdentifierName(sourceVar)));
                break;

            case TupleLiteral tuple when tuple.Elements.All(e => e is Identifier):
                // var (a, b, ...) = src;
                var designations = new List<VariableDesignationSyntax>();
                foreach (var elemId in tuple.Elements.Cast<Identifier>())
                {
                    var name = GetMangledVariableName(elemId.Name, isNewDeclaration: true,
                        elemId.IsNameBacktickEscaped);
                    _declaredVariables.Add(name);
                    designations.Add(SingleVariableDesignation(EscapedIdentifier(name)));
                }

                statements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        DeclarationExpression(
                            IdentifierName("var"),
                            ParenthesizedVariableDesignation(SeparatedList(designations))),
                        IdentifierName(sourceVar))));
                break;

            case TupleLiteral tuple:
                // Nested tuple target, e.g. for (x, y), name in items — unpack via src.ItemN.
                for (int i = 0; i < tuple.Elements.Length; i++)
                {
                    var itemAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(sourceVar),
                        IdentifierName($"Item{i + 1}"));

                    if (tuple.Elements[i] is Identifier elemId)
                    {
                        statements.Add(DeclareComprehensionVar(
                            GetMangledVariableName(elemId.Name, isNewDeclaration: true,
                                elemId.IsNameBacktickEscaped),
                            itemAccess));
                    }
                    else if (tuple.Elements[i] is TupleLiteral nested)
                    {
                        var nestedTemp = GenerateTempVarName("loopVar");
                        statements.Add(DeclareComprehensionVar(nestedTemp, itemAccess));
                        BindComprehensionLoopTarget(nested, nestedTemp, statements);
                    }
                }
                break;
        }
    }

    /// <summary>Builds <c>var {name} = {initializer};</c> and marks <paramref name="name"/> declared.</summary>
    private StatementSyntax DeclareComprehensionVar(string name, ExpressionSyntax initializer)
    {
        _declaredVariables.Add(name);
        return LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(name))
                        .WithInitializer(EqualsValueClause(initializer)))));
    }
}
