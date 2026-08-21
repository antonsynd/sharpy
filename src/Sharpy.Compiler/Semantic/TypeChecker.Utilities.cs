extern alias SharpyRT;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Collections;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Type checking utilities and validation
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Returns the index of the first variadic parameter in the sequence, or null if none.
    /// Used when materializing <see cref="FunctionType"/> from a parameter list so callers
    /// see `params T` semantics.
    /// </summary>
    internal static int? GetVariadicIndex(IEnumerable<ParameterSymbol> parameters)
    {
        int i = 0;
        foreach (var p in parameters)
        {
            if (p.IsVariadic)
                return i;
            i++;
        }
        return null;
    }

    /// <summary>
    /// Returns true if the type can be used in a boolean context (if, while conditions).
    /// A type is truth-testable if it is bool, UnknownType, or a user-defined type with __bool__.
    /// </summary>
    private bool IsTruthTestable(SemanticType type)
    {
        if (type == SemanticType.Bool || type is UnknownType)
            return true;

        // Strings are truth-testable: empty string is falsy, non-empty is truthy
        if (type == SemanticType.Str)
            return true;

        // User-defined types with __bool__ can be used in boolean contexts
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            return udt.Symbol.Methods.Any(m => m.Name == DunderNames.Bool);
        }

        return false;
    }

    /// <summary>
    /// Interprets a condition into the per-key type narrowings it implies for the given branch polarity,
    /// each paired with the accessor codegen must apply at a read site (#1081). This is the expression-
    /// level condition interpreter used by <c>and</c>-RHS (and, in Phase 5, ternary/or) scopes; the
    /// statement-level branch bodies are narrowed by the CFG dataflow facts instead (#1042). Leaf
    /// recognition (<c>is</c>/<c>is not None</c>, <c>isinstance</c>) is shared with the dataflow engine
    /// via <see cref="NarrowingConditionInterpreter.RecognizeLeaf"/>; <c>and</c> (positive) and <c>or</c>
    /// (negative, the De Morgan dual) combine both sides with the right operand winning on key overlap.
    /// </summary>
    private Dictionary<string, NarrowingEntry> ExtractNarrowedTypes(Expression condition, bool isPositiveBranch)
    {
        var entries = new Dictionary<string, NarrowingEntry>();

        // Unwrap parenthesized expressions
        if (condition is Parenthesized paren)
        {
            return ExtractNarrowedTypes(paren.Expression, isPositiveBranch);
        }

        // Handle 'not <expr>' pattern - flip the branch polarity and recurse
        if (condition is UnaryOp { Operator: UnaryOperator.Not } notOp)
        {
            return ExtractNarrowedTypes(notOp.Operand, !isPositiveBranch);
        }

        // Handle 'A and B' pattern - in the positive branch both conditions hold, so combine
        // narrowings from both sides (right operand wins on key overlap — the more-refining check).
        if (condition is BinaryOp { Operator: BinaryOperator.And } andOp && isPositiveBranch)
        {
            foreach (var kvp in ExtractNarrowedTypes(andOp.Left, true))
                entries[kvp.Key] = kvp.Value;
            foreach (var kvp in ExtractNarrowedTypes(andOp.Right, true))
                entries[kvp.Key] = kvp.Value;
            return entries;
        }

        // Handle 'A or B' pattern in the else-branch - De Morgan dual of 'and' narrowing:
        // else of (A or B) is equivalent to then of (not A and not B), so both sides narrow.
        if (condition is BinaryOp { Operator: BinaryOperator.Or } orOp && !isPositiveBranch)
        {
            foreach (var kvp in ExtractNarrowedTypes(orOp.Left, false))
                entries[kvp.Key] = kvp.Value;
            foreach (var kvp in ExtractNarrowedTypes(orOp.Right, false))
                entries[kvp.Key] = kvp.Value;
            return entries;
        }

        // Leaf recognition (`is`/`is not None`, `isinstance`) is shared with the dataflow engine via
        // NarrowingConditionInterpreter.RecognizeLeaf (#1042); here we resolve each recognised symbolic
        // fact to a concrete narrowed type and the accessor codegen applies at the read site. Branch
        // polarity has already been flipped through any enclosing `not` by the recursion above.
        foreach (var fact in NarrowingConditionInterpreter.RecognizeLeaf(condition, isPositiveBranch, DenotesBuiltinsModule))
        {
            if (fact.Kind == NarrowingActionKind.RemoveNone)
            {
                var resolvedType = ResolveNarrowedOperandType(fact.SourceExpression!);
                if (LoweringForRemoveNone(resolvedType) is { } removeNone)
                    entries[fact.Key] = new NarrowingEntry(removeNone.Type, removeNone.Lowering);
            }
            else // IsType (isinstance)
            {
                var narrowedType = ResolveIsTypeFactType(fact.TypeExpression!);
                if (narrowedType != null)
                    entries[fact.Key] = new NarrowingEntry(narrowedType, new NarrowedReadLowering(NarrowedReadKind.Cast, narrowedType));
            }
        }

        return entries;
    }

    /// <summary>
    /// Whether a type-test site can lower a bare <c>list</c>/<c>set</c>/<c>dict</c> to its non-generic
    /// protocol interface (#912). Only sites that produce a <b>boolean</b> can: the erased interface is
    /// what the runtime test must name, since a closed instantiation would match only itself.
    /// </summary>
    private enum CollectionErasure
    {
        /// <summary>
        /// The site binds a value of the tested type (<c>as?</c>/<c>as!</c>), so the erased interface
        /// is not a usable answer — it is not the type the checker gave the expression. A bare
        /// collection name is filled from the subject or refused like any other open generic.
        /// </summary>
        Disallowed,

        /// <summary>
        /// The site yields a boolean (<c>is</c>), so a bare collection name erases exactly as
        /// <c>isinstance</c>'s does — which is what keeps the two operators' answers the same.
        /// </summary>
        Allowed
    }

    /// <summary>
    /// Classifies an annotation-shaped type-test operand — the <see cref="TypeAnnotation"/> of
    /// <c>is</c>/<c>as?</c>/<c>as!</c>, a match class pattern, or an <c>except</c> clause — and records
    /// the decision on <paramref name="lodgeOn"/> for codegen to apply verbatim (#1235).
    /// <para>
    /// This is the <c>isinstance</c> three-outcome rule at the remaining type-operand positions, and
    /// deliberately the same rule: a closed spelling, a primitive or a non-generic name is a closed
    /// test; a bare generic name has its vector filled from the SUBJECT when the subject determines it;
    /// otherwise the operand names no runtime type and is refused (SPY0345) with a message naming a
    /// spelling that works. .NET reifies generics, so an open name denotes nothing to test against —
    /// emitting it produces CS0305 behind SPY0908, which is the leak this closes.
    /// </para>
    /// <para>
    /// Shapes with no single decidable answer — a nullable/optional/result spelling, an unresolvable
    /// name — are left <b>unrecorded</b> on purpose: the emitter then maps the annotation exactly as it
    /// did before classification existed, so this adds decisions without removing fallbacks.
    /// </para>
    /// </summary>
    /// <param name="annotation">The written type operand.</param>
    /// <param name="lodgeOn">The node the lowering is keyed on — normally <paramref name="annotation"/>
    /// itself, or the owning annotation for one element of an <c>except</c> tuple.</param>
    /// <param name="subjectType">The static type of the value being tested, or null when the site has
    /// no subject (an <c>except</c> clause tests whatever was thrown).</param>
    /// <param name="siteNoun">How the refusal message names this position.</param>
    /// <param name="erasure">Whether a bare builtin collection may erase to its protocol interface.</param>
    /// <returns>The type the site tests against, or null when nothing was recorded.</returns>
    private SemanticType? ClassifyTypeTestAnnotation(
        TypeAnnotation annotation,
        Node lodgeOn,
        SemanticType? subjectType,
        string siteNoun,
        CollectionErasure erasure)
    {
        // Only a bare NAME can be an open generic, so it is the only shape needing the vector-filling
        // rule. A spelling carrying type arguments, or any nullable/optional/result modifier, names
        // what it names; resolve it and record the closed answer.
        if (annotation.TypeArguments.Length > 0
            || annotation.IsOptional || annotation.IsCSharpNullable || annotation.IsResult)
        {
            var spelled = _typeResolver.ResolveTypeAnnotation(annotation);
            if (spelled is UnknownType)
                return null;

            // Nullable/optional/result spellings keep the emitter's own mapping: the wrapper decides
            // the emitted shape, and nothing here improves on it.
            if (annotation.IsOptional || annotation.IsCSharpNullable || annotation.IsResult)
                return null;

            _semanticInfo.SetTypeTestLowering(lodgeOn, new TypeTestLowering(TypeTestLoweringKind.ClosedType, spelled));
            return spelled;
        }

        // The escape decides the namespace both ways (#1325), same as the expression-shaped twin
        // (ClassifyBareTypeNameOperand): the primitive claim belongs to the bare spelling only,
        // and symbol acceptance is by identity — escaped never binds the registry's own symbol,
        // bare never binds an escape-declared one, quoting a bare-declared import stands.
        if (!annotation.IsNameBacktickEscaped
            && ResolveBuiltinPrimitiveTypeName(annotation.Name) is { } primitive)
        {
            _semanticInfo.SetTypeTestLowering(lodgeOn, new TypeTestLowering(TypeTestLoweringKind.ClosedType, primitive));
            return primitive;
        }

        var operandSymbol = _symbolTable.Lookup(annotation.Name);
        if (operandSymbol != null)
        {
            if (annotation.IsNameBacktickEscaped && _symbolTable.BuiltinRegistry.IsBuiltinSymbol(operandSymbol))
                operandSymbol = null;
            else if (!annotation.IsNameBacktickEscaped && operandSymbol.IsNameBacktickEscaped)
                operandSymbol = _symbolTable.BuiltinRegistry.GetType(annotation.Name);
        }

        // A MODULE-QUALIFIED spelling is one string containing dots, which the flat scope lookup
        // above can never answer. Left there, `x as? mod.Box` returned here having decided nothing
        // and skipped the guard below, while the bare `x as? Box` drew SPY0345 — the qualifier
        // silently bought an exemption from a rule about the type, not about how it is named
        // (#1411). Escaped spellings keep TypeResolver's gate (#1325): `` `mod.Box` `` names the
        // user's own declaration and must not bind a module-qualified type by accident.
        if (operandSymbol == null && !annotation.IsNameBacktickEscaped)
            operandSymbol = _typeResolver.LookupModuleQualifiedType(annotation.Name);

        if (operandSymbol is not TypeSymbol typeSymbol)
            return null;

        // list/set/dict written without type arguments: the test cannot know the element types, so a
        // boolean site erases to the non-generic protocol interface, which every closed instantiation
        // implements. BuildIsInstanceNarrowedType supplies the same default-argument type narrowing
        // resolves the operand to, so the test and the narrowed type stay the same object.
        if (erasure == CollectionErasure.Allowed
            && typeSymbol.IsGeneric && BuiltinNames.IsErasableCollection(typeSymbol.Name))
        {
            var erased = BuildIsInstanceNarrowedType(typeSymbol);
            _semanticInfo.SetTypeTestLowering(
                lodgeOn, new TypeTestLowering(TypeTestLoweringKind.ErasedBuiltinCollection, erased));
            return erased;
        }

        if (!typeSymbol.IsGeneric)
        {
            var closed = BuildIsInstanceNarrowedType(typeSymbol);
            _semanticInfo.SetTypeTestLowering(lodgeOn, new TypeTestLowering(TypeTestLoweringKind.ClosedType, closed));
            return closed;
        }

        if (FillTypeArgumentsFromSubject(typeSymbol, subjectType) is { } closedGeneric)
        {
            _semanticInfo.SetTypeTestLowering(
                lodgeOn, new TypeTestLowering(TypeTestLoweringKind.ClosedType, closedGeneric));
            return closedGeneric;
        }

        ReportOpenGenericTypeOperand(
            annotation, annotation.Name, siteNoun,
            remedy: ClosedSpellingRemedy($"{annotation.Name}[{OpenGenericPlaceholders(typeSymbol)}]"));
        return null;
    }

    /// <summary>
    /// The <c>...</c> placeholder vector for a generic type's arity, used when a refusal message
    /// suggests a closed spelling.
    /// </summary>
    private static string OpenGenericPlaceholders(TypeSymbol typeSymbol)
        => string.Join(", ", typeSymbol.TypeParameters.Select(_ => "..."));

    /// <summary>
    /// The remedy clause for sites where a closed spelling is actually writable — <c>isinstance</c>,
    /// <c>is</c>, <c>as?</c>/<c>as!</c> and <c>except</c>. <b>Match patterns do not use this</b>: the
    /// parser refuses type arguments in a pattern (SPY0125), so telling a user to write
    /// <c>case Box[int]():</c> would name a spelling the compiler rejects.
    /// </summary>
    private static string ClosedSpellingRemedy(string example)
        => $"Write the closed spelling — for example `{example}` — or test against a non-generic base type.";

    /// <summary>
    /// Refuses an open generic type operand. One diagnosis sentence and one code (SPY0345) for all
    /// five type-operand positions — <c>isinstance</c>, <c>is</c>, <c>as?</c>/<c>as!</c>, match class
    /// patterns and <c>except</c> clauses — so a reader who has met the refusal once does not have to
    /// learn it again (#1207, #1235).
    /// <para>
    /// The <b>remedy</b> is supplied per site rather than templated here, because what a user should
    /// write genuinely differs: most sites can name the closed spelling, but a match pattern cannot
    /// (SPY0125 refuses type arguments in patterns), so a shared "write <c>Box[int]</c>" sentence would
    /// be false advice at that one site.
    /// </para>
    /// </summary>
    /// <param name="at">The node the diagnostic is anchored to.</param>
    /// <param name="typeName">The generic type's name as written.</param>
    /// <param name="siteNoun">How the message names this position ("type test", "except clause", ...).</param>
    /// <param name="remedy">The site's actionable advice, as a complete sentence.</param>
    /// <param name="fallbackSpan">Used when <paramref name="at"/> carries no span of its own.</param>
    private void ReportOpenGenericTypeOperand(
        Node at, string typeName, string siteNoun, string remedy, Text.TextSpan? fallbackSpan = null)
    {
        AddError(
            $"'{typeName}' is a generic type, so it does not name a single type to test against, "
                + $"and nothing at this {siteNoun} determines its type arguments. "
                + remedy
                + " Unlike Python, Sharpy's generics are real "
                + "runtime types, and a successful open test could not narrow to a type you can write.",
            at.LineStart, at.ColumnStart,
            code: DiagnosticCodes.Semantic.OpenGenericTypeTest,
            span: at.Span ?? fallbackSpan);
    }

    /// <summary>
    /// Builds the narrowed type for an <c>isinstance(x, T)</c> check against a user/builtin
    /// TypeSymbol. Generic builtin collections (list, set, dict) narrow to a parameterized
    /// <see cref="GenericType"/> with default <c>object</c> type arguments, so downstream member
    /// access on the narrowed value (indexing, <c>.items()</c>, etc.) resolves at the semantic
    /// level (#912). Without this they would narrow to a bare <see cref="UserDefinedType"/> with
    /// no type arguments, and e.g. <c>d[k]</c> on a narrowed <c>dict</c> would fail to lower.
    /// Mirrors the unparameterized-collection handling in
    /// <see cref="CheckTypePattern"/>.
    /// </summary>
    private SemanticType BuildIsInstanceNarrowedType(TypeSymbol typeSymbol)
    {
        var arity = typeSymbol.Name switch
        {
            BuiltinNames.List => 1,
            BuiltinNames.Set => 1,
            BuiltinNames.Dict => 2,
            _ => 0
        };

        if (arity > 0 && typeSymbol.TypeParameters.Count == arity)
        {
            var defaultArgs = new List<SemanticType>(arity);
            for (var i = 0; i < arity; i++)
            {
                defaultArgs.Add(SemanticType.Object);
            }

            return new GenericType
            {
                Name = typeSymbol.Name,
                TypeArguments = defaultArgs,
                GenericDefinition = typeSymbol
            };
        }

        return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
    }

    /// <summary>
    /// Extract a key to use for type narrowing from an expression.
    /// Delegates to <see cref="AstHelper.ExtractNarrowingKey"/>.
    /// </summary>
    private string? ExtractNarrowingKey(Expression expr) => AstHelper.ExtractNarrowingKey(expr);

    /// <summary>Strips any <see cref="Parenthesized"/> wrappers, returning the inner expression —
    /// the node the read sites actually resolve (used to mark type-test operands) and the canonical
    /// callee every call-shape dispatch keys on. Delegates to the shared
    /// <see cref="AstHelper.UnwrapParenthesized"/>, which documents the normalization contract
    /// (#1170).</summary>
    private static Expression UnwrapParenthesized(Expression expr) => AstHelper.UnwrapParenthesized(expr);

    /// <summary>
    /// Runs the statement-level narrowing dataflow analysis (#1042) over a function body's CFG.
    /// The graph comes from the shared <see cref="ControlFlowGraphCache"/> so the validation-pass
    /// consumers (ControlFlowValidator, StructRulesValidator, PropertyValidator) reuse it instead
    /// of rebuilding.
    /// </summary>
    private NarrowingFlowResult ComputeNarrowingFlow(FunctionDef function) =>
        NarrowingFlowAnalysis.Analyze(_controlFlowGraphs.GetOrBuild(function), DenotesBuiltinsModule);

    /// <summary>
    /// Runs the statement-level narrowing dataflow analysis (#1042) over a raw statement list
    /// (module body or property accessor body), via the shared <see cref="ControlFlowGraphCache"/>.
    /// </summary>
    private NarrowingFlowResult ComputeNarrowingFlow(IReadOnlyList<Statement> body) =>
        NarrowingFlowAnalysis.Analyze(_controlFlowGraphs.GetOrBuild(body), DenotesBuiltinsModule);

    /// <summary>
    /// Whether <paramref name="receiver"/> denotes the <c>builtins</c> module, so
    /// <c>builtins.isinstance(x, T)</c> narrows as bare <c>isinstance</c> does (#1381).
    /// </summary>
    /// <remarks>
    /// Resolves the NAME through the symbol table rather than reading
    /// <see cref="SemanticInfo"/>, and that is an ordering requirement, not a preference:
    /// <c>ComputeNarrowingFlow</c> runs before the body walk that would populate expression types,
    /// so a type-based answer — or a mark written during expression checking — does not exist yet.
    /// A module symbol is available after import resolution (Pass 1.5), which is earlier than both.
    ///
    /// <para>Matching the spelling <c>mod.isinstance</c> for an arbitrary <c>mod</c> would be wrong
    /// for the same reason the recogniser is syntactic everywhere else: a user module named
    /// <c>builtins</c>-something, or a local shadowing the name, must not narrow.</para>
    /// </remarks>
    private bool DenotesBuiltinsModule(Expression receiver) =>
        UnwrapParenthesized(receiver) is Identifier id
        && _symbolTable.Lookup(id.Name) is ModuleSymbol moduleSymbol
        && moduleSymbol.IsNetModule
        && string.Equals(moduleSymbol.CanonicalModuleName, "builtins", StringComparison.Ordinal);

    /// <summary>
    /// Resolves the narrowing facts currently in effect (<see cref="_currentFacts"/>) for a narrowing
    /// key against the value's live type, returning the narrowed type and the accessor codegen must
    /// apply at the read site, or null if nothing narrows it. This is the read-time counterpart to the
    /// resolution <see cref="ExtractNarrowedTypes"/> performs at the condition: a <c>RemoveNone</c> fact
    /// strips <see cref="NullableType"/>/<see cref="OptionalType"/> (with the lowering keyed on the live
    /// type shape), an <c>IsType</c> fact resolves the recorded type expression to a cast. When a key
    /// carries both (e.g. <c>x is not None and isinstance(x, T)</c>), the more specific <c>IsType</c>
    /// wins — matching the dict-overwrite precedence in <see cref="ExtractNarrowedTypes"/>'s <c>and</c>
    /// handling. The lowering is materialized per read node so codegen never re-derives flow (#1081).
    /// </summary>
    private (SemanticType Type, NarrowedReadLowering Lowering)? ResolveNarrowedTypeFromFacts(string key, SemanticType liveType)
    {
        NarrowingFact? isTypeFact = null;
        NarrowingFact? removeNoneFact = null;
        foreach (var fact in _currentFacts)
        {
            if (fact.Key != key)
                continue;
            if (fact.Kind == NarrowingActionKind.IsType)
                isTypeFact = fact;
            else if (fact.Kind == NarrowingActionKind.RemoveNone)
                removeNoneFact = fact;
        }

        if (isTypeFact?.TypeExpression is { } typeExpr && ResolveIsTypeFactType(typeExpr) is { } narrowed)
            return (narrowed, new NarrowedReadLowering(NarrowedReadKind.Cast, narrowed));

        if (removeNoneFact != null)
            return LoweringForRemoveNone(liveType);

        return null;
    }

    /// <summary>
    /// The single gate every narrowed read passes through before its accessor is materialized for
    /// codegen. It records the lowering unchanged everywhere except at a match SUBJECT carrying a
    /// <see cref="NarrowedReadKind.Cast"/>, where it records nothing (#1370).
    /// <para>
    /// A cast is not load-bearing for a cast-kind narrowing: the arms re-perform the runtime test on
    /// the raw subject, so casting the subject read only tells C# the switch is statically total —
    /// <c>switch (((double)r!))</c> against <c>case double f:</c> proves every later arm dead, and a
    /// <c>case _:</c> arm becomes CS0162 in any warnings-as-errors consumer of the generated C# (the
    /// checked-in spy-test C# is the standing one, by design). Suppressing it here rather than
    /// dropping arms in the emitter keeps the reachability decision in the semantic phase, where the
    /// narrowing fact lives, instead of duplicating Roslyn's analysis (Critical Rule 2).
    /// </para>
    /// <para>
    /// The other kinds ARE load-bearing and are recorded normally: a narrowed <c>T?</c> subject must
    /// still read <c>x.Unwrap()</c> to produce a value the arms can test at all — an un-narrowed
    /// Optional subject is #1358's ICE — and the <c>.Value</c>/<c>!</c> accessors likewise change what
    /// value is switched on rather than merely restating its type. The narrowed TYPE is recorded by
    /// the caller either way, so #1299's pattern filling from the subject is untouched.
    /// </para>
    /// </summary>
    private void RecordNarrowedReadLowering(Expression node, NarrowedReadLowering lowering)
    {
        if (lowering.Kind == NarrowedReadKind.Cast && ReferenceEquals(node, _matchSubjectOperand))
            return;

        _semanticInfo.SetNarrowedReadLowering(node, lowering);
    }

    /// <summary>
    /// The single decision point mapping a <c>RemoveNone</c> narrowing onto a value's un-narrowed
    /// type shape: strips <see cref="NullableType"/>/<see cref="OptionalType"/> and pairs the
    /// underlying type with the accessor codegen must apply at the read site. Shared by the
    /// condition-side (<see cref="ExtractNarrowedTypes"/>) and read-side
    /// (<see cref="ResolveNarrowedTypeFromFacts"/>) resolutions so the fact→accessor mapping cannot
    /// drift between them (#1081). Returns null for shapes a <c>RemoveNone</c> fact cannot narrow.
    /// </summary>
    private static (SemanticType Type, NarrowedReadLowering Lowering)? LoweringForRemoveNone(SemanticType? type)
    {
        return type switch
        {
            NullableType { IsValueType: true } nullable =>
                (nullable.UnderlyingType, new NarrowedReadLowering(NarrowedReadKind.NullableValue)),
            NullableType nullable =>
                (nullable.UnderlyingType, new NarrowedReadLowering(NarrowedReadKind.NullForgiving)),
            OptionalType optional =>
                (optional.UnderlyingType, new NarrowedReadLowering(NarrowedReadKind.UnwrapOptional)),
            _ => null
        };
    }

    /// <summary>
    /// Resolves the (non-narrowed) type of a narrowed operand expression at the condition, so the
    /// <c>RemoveNone</c> leaf can pick the read accessor from its <see cref="NullableType"/>/
    /// <see cref="OptionalType"/> shape: identifiers resolve through the symbol table, member/other
    /// expressions read their already-type-checked type from <see cref="SemanticInfo"/>.
    /// </summary>
    private SemanticType? ResolveNarrowedOperandType(Expression operand)
    {
        if (operand is Identifier id)
            return _symbolTable.Lookup(id.Name) is VariableSymbol varSymbol ? GetVariableType(varSymbol) : null;
        return _semanticInfo.GetExpressionType(operand);
    }

    /// <summary>
    /// Resolves the target type of an <c>IsType</c> narrowing fact (from <c>isinstance(x, T)</c>) —
    /// the read-side resolution, and, through the isinstance leaf of
    /// <see cref="ExtractNarrowedTypes"/>, the condition-side one too.
    /// <para>
    /// Both read the type the TypeChecker's type-operand classifier recorded on this very operand
    /// node, which is also the type codegen emits the <c>is</c> test against. Narrowing and the
    /// emitted test therefore agree by construction rather than by two parallel derivations of the
    /// same expression — the divergence that let #1207 produce a narrowing fact for <c>Box</c> (no
    /// type arguments) while the emitter spelled the unspellable <c>Box&lt;T&gt;</c>.
    /// </para>
    /// <para>
    /// Returns null when the operand was not classified as a type test, which is exactly when codegen
    /// emits no type test either: nothing narrows on the strength of a check that was not made. Facts
    /// themselves stay symbolic and keyed textually (see <c>NarrowingFact</c>), so this resolution is
    /// the only place a <c>SemanticType</c> enters the picture, and fact equality — the property that
    /// lets two <c>isinstance(x, T)</c> checks survive an intersection join at a CFG merge — is
    /// untouched.
    /// </para>
    /// </summary>
    private SemanticType? ResolveIsTypeFactType(Expression typeExpression) =>
        _semanticInfo.GetTypeTestLowering(typeExpression)?.TestType;

    /// <summary>
    /// Returns true if the given type contains any <see cref="TypeParameterType"/>
    /// (e.g., Iterator&lt;T&gt; contains T). Used during overload resolution to
    /// skip type-matching for generic parameters that C# will infer later.
    /// </summary>
    private static bool ContainsTypeParameter(SemanticType type)
    {
        return type switch
        {
            TypeParameterType => true,
            GenericType gt => gt.TypeArguments.Any(ContainsTypeParameter),
            NullableType nt => ContainsTypeParameter(nt.UnderlyingType),
            OptionalType ot => ContainsTypeParameter(ot.UnderlyingType),
            TupleType tt => tt.ElementTypes.Any(ContainsTypeParameter),
            FunctionType ft => ft.ParameterTypes.Any(ContainsTypeParameter) || ContainsTypeParameter(ft.ReturnType),
            _ => false
        };
    }

    /// <summary>
    /// Check if a source type can be assigned to a target type.
    /// This extends the basic IsAssignableTo to handle nullable types and generic variance.
    /// </summary>
    /// <summary>
    /// Whether a type is a string-backed enum — a member of which is both its own type and its
    /// backing string (#1284).
    /// </summary>
    internal static bool IsStringBackedEnum(SemanticType? type)
        => type is UserDefinedType { Symbol: { TypeKind: TypeKind.Enum, IsStringEnum: true } };

    /// <summary>
    /// Whether <paramref name="value"/> is an integer constant expression that C# would convert
    /// implicitly to <paramref name="target"/> because its VALUE is in range (#1355).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ECMA-334 §10.2.11 *Implicit constant expression conversions*, which this implements verbatim:
    /// an <c>int</c> constant converts to <c>sbyte</c>/<c>byte</c>/<c>short</c>/<c>ushort</c>/
    /// <c>uint</c>/<c>ulong</c> when the value is in the destination's range, and a <c>long</c>
    /// constant converts to <c>ulong</c> only, and only when non-negative. Without it NO literal can
    /// initialize a sub-32-bit annotation — there are no suffixes for those widths — so
    /// <c>b: uint8 = 200</c> was SPY0220 and the language specification's own examples
    /// (<c>integer_literals.md</c>: <c>s: int16 = 42</c>, <c>b: uint8 = 255</c>, <c>sb: int8 = -128</c>)
    /// were unexecutable. Implementation moves to the spec (Critical Rule 7).
    /// </para>
    /// <para>
    /// This is deliberately NOT folded into <see cref="Registry.PrimitiveCatalog.ImplicitConversionCost"/>,
    /// which is keyed on types alone and is the single ranking that <c>CanImplicitlyConvert</c> and the
    /// CLR operator resolver both consume. A conversion that depends on a literal's value cannot be
    /// expressed there, and widening that function to admit it would silently change overload
    /// resolution at every numeric call site.
    /// </para>
    /// <para>
    /// Unlike <c>IsFloat32LiteralNarrowing</c> — the value-aware allowance this is modelled on — the
    /// literal's recorded type is left alone. That one has to re-type its node because C# has no
    /// implicit <c>double</c>→<c>float</c> literal conversion, so leaving it would turn SPY0220 into
    /// CS0664. Here C# performs exactly this conversion itself, so <c>byte b = 200;</c> emits and
    /// compiles as written, and re-typing would be a lie about what the source said.
    /// </para>
    /// <para>
    /// The constant's VALUE comes from <see cref="IntegerConstantEvaluator"/>, the evaluator the
    /// checker and the lowering pass already share — so parenthesized and folded shapes
    /// (<c>b: uint8 = 1 + 1</c>, <c>(200)</c>, <c>1 &lt;&lt; 7</c>) come for free and agree with the
    /// numbers SPY0348 is reported from. Its contract is exactly the half needed here: it decides
    /// what the value IS and never whether it fits a type, leaving the range decision to callers.
    /// </para>
    /// <para>
    /// A <c>const</c> reference is a constant expression: <c>const L: int = 200</c> then
    /// <c>b: uint8 = L</c> folds via <see cref="VariableSymbol.ConstantValue"/>, which
    /// <see cref="IntegerConstantEvaluator"/> consults through the resolver parameter (#1460).
    /// Compound expressions over const references (<c>LIMIT + 55</c>) fold transitively.
    /// </para>
    /// </remarks>
    private bool IsImplicitConstantConversion(Expression? value, SemanticType source, SemanticType target)
    {
        if (value == null)
            return false;

        System.Numerics.BigInteger? ResolveConstant(string name)
        {
            var sym = _symbolTable.Lookup(name);
            return sym is VariableSymbol { IsConstant: true, ConstantValue: not null } vs
                ? vs.ConstantValue
                : null;
        }

        if (!IntegerConstantEvaluator.TryGetConstantInteger(value, out var constant, ResolveConstant))
            return false;

        var sourceInfo = Registry.PrimitiveCatalog.GetPrimitiveInfo(source);
        var targetInfo = Registry.PrimitiveCatalog.GetPrimitiveInfo(target);
        if (sourceInfo == null || targetInfo == null)
            return false;

        // §10.2.11 splits on the CONSTANT's type, not on width: a long constant has exactly one
        // legal destination. Compared by CLR type because the int singleton is named "int" while
        // the other widths use catalog spellings (#1304/#1356 class).
        if (sourceInfo.ClrType == typeof(long))
            return targetInfo.ClrType == typeof(ulong) && constant.Sign >= 0;

        if (sourceInfo.ClrType != typeof(int))
            return false;

        // int→long is the value-independent standard numeric conversion (§10.2.3), not a constant
        // conversion; it already works and must not be re-derived here.
        return targetInfo.Kind is Registry.PrimitiveCatalog.NumericKind.SignedInteger or Registry.PrimitiveCatalog.NumericKind.UnsignedInteger
            && targetInfo.ClrType != typeof(int)
            && targetInfo.ClrType != typeof(long)
            && FitsInRange(constant, targetInfo);
    }

    /// <summary>
    /// Whether an exact constant lies in <paramref name="target"/>'s range, derived from
    /// <see cref="Registry.PrimitiveCatalog.PrimitiveInfo.SizeInBits"/> and <see cref="Registry.PrimitiveCatalog.PrimitiveInfo.IsSigned"/>.
    /// Those two fields are identical across the catalog's Sharpy-style and C#-style alias
    /// registrations — only <c>SharpyName</c> differs — so this is unaffected by which alias the CLR
    /// reverse map happens to canonicalize to (#1356).
    /// </summary>
    private static bool FitsInRange(System.Numerics.BigInteger constant, Registry.PrimitiveCatalog.PrimitiveInfo target)
    {
        if (target.IsSigned)
        {
            // Two's complement: the negative side reaches one further than the positive side, which
            // is what makes `sb: int8 = -128` legal while `sb: int8 = 128` is not.
            var limit = System.Numerics.BigInteger.One << (target.SizeInBits - 1);
            return constant >= -limit && constant < limit;
        }

        return constant.Sign >= 0 && constant < (System.Numerics.BigInteger.One << target.SizeInBits);
    }

    private bool IsAssignable(SemanticType source, SemanticType target)
    {
        // Allow assignment to UnknownType to avoid cascading errors
        // (e.g., when a parameter has no type annotation)
        if (target is UnknownType)
            return true;

        // First check the standard assignability
        if (source.IsAssignableTo(target))
            return true;

        // A string-backed enum member IS its string, exactly as CPython's StrEnum is
        // (`isinstance(LogLevel.INFO, str)` is True). The emitted class carries
        // `implicit operator string`, so this is stating a conversion .NET already performs —
        // `IsAssignable` does not consult user conversions, so the rule is explicit (#1284).
        if (IsStringBackedEnum(source) && target is BuiltinType { Name: BuiltinNames.Str })
            return true;

        // Non-nullable type can be assigned to nullable version of the same type.
        // Recurse through IsAssignable (not just IsAssignableTo) so the underlying-type check
        // also benefits from the CLR-metadata fallback below — this is what lets a builtin
        // `bytes` argument bind to a `Bytes?` (Nullable<Bytes>) parameter (#890).
        if (target is NullableType nullable)
        {
            return IsAssignable(source, nullable.UnderlyingType);
        }

        // Non-optional type can be assigned to optional version of the same type
        if (target is OptionalType optional)
        {
            return IsAssignable(source, optional.UnderlyingType);
        }

        // FunctionType is assignable to a delegate type if the signatures are compatible
        if (source is FunctionType sourceFt)
        {
            var delegateInvoke = TryGetDelegateInvokeMethod(target);
            if (delegateInvoke != null)
            {
                // Compare parameter counts
                if (sourceFt.ParameterTypes.Count != delegateInvoke.Parameters.Count)
                    return false;

                // Compare parameter types
                for (int i = 0; i < sourceFt.ParameterTypes.Count; i++)
                {
                    var invokeParamType = delegateInvoke.Parameters[i].Type;
                    if (!invokeParamType.IsAssignableTo(sourceFt.ParameterTypes[i])
                        && !sourceFt.ParameterTypes[i].IsAssignableTo(invokeParamType))
                        return false;
                }

                // Compare return types
                if (delegateInvoke.ReturnType is not VoidType && sourceFt.ReturnType is not VoidType)
                {
                    if (!sourceFt.ReturnType.IsAssignableTo(delegateInvoke.ReturnType)
                        && !IsAssignable(sourceFt.ReturnType, delegateInvoke.ReturnType))
                        return false;
                }

                return true;
            }
        }

        // Two function types, re-compared with the knowledge the record does not have. Same shape as
        // FunctionType.IsAssignableTo — parameters either way, return one way — but each position goes
        // through IsAssignable, so a position type that needs the symbol table or CLR provenance gets
        // the same answer here that it gets anywhere else. The delegate branch above already recurses
        // this way for its return type; this is that rule for the FunctionType-to-FunctionType case.
        //
        // Reached only after the record-level check at the top failed, so it can widen but never narrow.
        // What it buys (#1252): a lambda returning a CLR `List[int]` matches a mapped
        // `(int) -> list[int]` formal, because the return position finally asks .NET about the origin
        // rather than comparing `List` to `list` as text.
        if (source is FunctionType structuralSource && target is FunctionType structuralTarget
            && structuralSource.ParameterTypes.Count == structuralTarget.ParameterTypes.Count)
        {
            var compatible = true;
            for (int i = 0; i < structuralSource.ParameterTypes.Count && compatible; i++)
            {
                compatible = IsAssignable(structuralTarget.ParameterTypes[i], structuralSource.ParameterTypes[i])
                             || IsAssignable(structuralSource.ParameterTypes[i], structuralTarget.ParameterTypes[i]);
            }

            if (compatible
                && (structuralTarget.ReturnType is VoidType
                    || structuralSource.ReturnType is VoidType
                    || IsAssignable(structuralSource.ReturnType, structuralTarget.ReturnType)))
            {
                return true;
            }
        }

        // Generic variance (#827): same-name generics check per-type-parameter variance
        // from the definition's TypeParameterDefs; different-name generics check
        // assignability through implemented interfaces and base classes
        // (e.g., list[int] → IEnumerable[int], MyList[int] → list[int]).
        //
        // A NON-generic source reaches the same walk through AsInstantiatedGeneric (#1244): the
        // question `class StrHolder(Holder[str])` → `Holder[str]` asks exactly what the generic
        // source already asks — "does this declaration have the target among its instantiated
        // supertypes?" — and the answer must not depend on whether the implementing class happens
        // to have type parameters of its own. `class Box[T](Holder[T])` → `Holder[int]` worked
        // before this; `class StrHolder(Holder[str])` → `Holder[str]` did not.
        //
        // This lives here rather than on UserDefinedType.IsAssignableTo because the walk needs the
        // symbol table, the SemanticBinding and the TypeResolver, none of which a SemanticType
        // record can reach; a second, weaker hierarchy walk over there would be the parallel-site
        // hazard (#1145), not a fix.
        if (AsInstantiatedGeneric(source) is { } sourceGeneric && target is GenericType targetGeneric)
        {
            var varianceResult = IsGenericAssignableWithVariance(sourceGeneric, targetGeneric);
            if (varianceResult == true)
                return true;
            if (varianceResult == false)
                return false;
            // null → no opinion, continue to CLR fallback
        }

        // CLR fallback: when both types have CLR metadata (e.g., module-discovered types like
        // StringIO and TextWriter that may be different SemanticType subtypes), use reflection
        // to check inheritance. This covers cross-subtype assignability that the standard
        // IsAssignableTo checks miss, including Sharpy collection types (list/dict/set) being
        // passed to CLR parameters expecting IEnumerable, ICollection<T>, etc.
        var sourceClr = TryGetClrType(source);
        var targetClr = TryGetClrType(target);
        if (sourceClr != null && targetClr != null && targetClr.IsAssignableFrom(sourceClr))
            return true;

        // Provenance-scoped widening (#1260): a formal the bridge MAPPED from CLR metadata is spelled in
        // Sharpy vocabulary but means the CLR type it came from, and the check above asks the wrong
        // question about it — `Sharpy.List<int>.IsAssignableFrom(System.List<int>)` is false, so
        // `outer.concat(inner)` with a CLR `List[int]` drew SPY0220 for a call C# binds natively.
        //
        // Asking .NET about the ORIGIN instead accepts exactly the calls .NET binds and nothing more,
        // and it needs no codegen support: the emitted call's real formal IS that CLR type, so the
        // actual goes in unconverted. Scoped by construction — a `list[int]` written in Sharpy source
        // has no provenance, so a native parameter stays strict and no hidden copy is introduced.
        if (sourceClr != null && target is GenericType { ClrOriginTypeName: not null } mappedTarget
            && Discovery.ClrTypeHelper.ClrOriginIsSatisfiedBy(mappedTarget, sourceClr, TryGetClrType))
        {
            return true;
        }

        // A CLR sequence interface the bridge kept as itself lands in a Sharpy `list[E]` slot exactly
        // as a bridge-mapped `list[E]` does — .NET says it IS an IEnumerable<E>, and the same
        // `new Sharpy.List<E>(source)` copy converts it. The two halves are stated together:
        // TryGetClrSequenceInterfaceElement is what RecordSequenceMaterialization keys on, so this
        // accepts exactly the assignments the ring can carry out and never one it cannot (#1390).
        if (target is GenericType { Name: BuiltinNames.List, ClrOriginTypeName: null, TypeArguments.Count: 1 } listSlot
            && TryGetClrSequenceInterfaceElement(source) is { } sequenceElement
            && IsAssignable(sequenceElement, listSlot.TypeArguments[0]))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// The type as an instantiated generic, for the one supertype walk in
    /// <see cref="IsGenericAssignableWithVariance"/>. A <see cref="GenericType"/> is already one; a
    /// user-defined type backed by a resolved symbol becomes the zero-argument instantiation of that
    /// symbol, which is what a non-generic declaration IS. Anything else has no declaration to walk.
    /// <para>The zero-argument view is exact, not a convenience: <c>GenericInstantiationWalker</c>
    /// builds its initial substitution from (type parameters, type arguments), and a non-generic
    /// declaration has none of either, so the empty map is the correct starting point and its
    /// base-list entries (<c>Holder[str]</c>) resolve to their written arguments unchanged. A symbol
    /// that IS generic but was referenced without arguments produces an arity mismatch there and
    /// yields no supertypes — no opinion, which leaves the CLR fallback in charge exactly as
    /// before (#1244).</para>
    /// </summary>
    private static GenericType? AsInstantiatedGeneric(SemanticType type) => type switch
    {
        GenericType generic => generic,
        UserDefinedType { Symbol: { } symbol } => new GenericType
        {
            Name = symbol.Name,
            TypeArguments = new List<SemanticType>(),
            GenericDefinition = symbol
        },
        _ => null
    };

    /// <summary>
    /// Argument-binding assignability: standard <see cref="IsAssignable"/>, the
    /// list[T] → array[T] coercion for CLR T[] parameters (#959), and the
    /// dict[K,V] → iterable[K] acceptance for an argument codegen projects to its keys (#1159).
    /// Both extensions are scoped to the argument-binding boundary on purpose — general
    /// assignment keeps list[T] ↛ array[T] (Decision #2, #944) and dict ↛ list, so e.g.
    /// <c>arr: array[int] = lst</c> stays an error. Each has a matching codegen bridge
    /// (<c>RoslynEmitter.ApplyArrayBridge</c>, <c>RoslynEmitter.ApplyIterableProjection</c>); the
    /// checker and the emitter must agree on which arguments are coercible.
    /// </summary>
    /// <param name="allowConstantConversion">
    /// Whether an in-range integer constant may satisfy a small-width parameter (#1355). True at
    /// the argument-binding boundary AND during overload applicability filtering. §10.2.11 constant
    /// conversions participate in C# overload resolution; the resulting ties are broken by the
    /// identity-match → better-conversion-target → signed-beats-unsigned chain in
    /// <c>IsMoreSpecificOverload</c> (#1464).
    /// </param>
    private bool IsArgumentAssignable(
        SemanticType source,
        SemanticType target,
        Expression? argument = null,
        bool allowConstantConversion = true)
    {
        if (IsAssignable(source, target))
            return true;

        // An in-range integer constant satisfies a small-width parameter, exactly as it does a
        // small-width annotation — §10.2.11 conversions apply wherever an implicit conversion is
        // asked, not only at a declaration (#1355).
        if (allowConstantConversion && IsImplicitConstantConversion(argument, source, target))
            return true;

        // list[T] → array[T]: element types must match exactly (UnknownType acts as a
        // wildcard for empty list literals) so codegen's .ToArray() produces an array of
        // the parameter's element type. Using IsAssignable on the element would wrongly
        // permit list[int] → array[float] (numeric widening), whose int[] cannot bind to
        // a C# double[].
        if (source is GenericType { Name: "list" } listType
            && target is GenericType { Name: "array" } arrayType
            && listType.TypeArguments.Count == 1
            && arrayType.TypeArguments.Count == 1
            && (listType.TypeArguments[0] is UnknownType
                || arrayType.TypeArguments[0] is UnknownType
                || listType.TypeArguments[0].Equals(arrayType.TypeArguments[0])))
        {
            return true;
        }

        // An argument in an iterable position binds through its PROJECTED type — the list[element]
        // codegen will actually pass (#1159, #1198). Re-entering this method (rather than
        // IsAssignable) lets the projection compose with the array coercion above; it terminates
        // because the recursive call passes no argument node, so nothing is re-projected.
        if (ProjectedArgumentType(argument) is { } projected)
            return IsArgumentAssignable(projected, target, argument: null);

        return false;
    }

    /// <summary>
    /// The clause that tells a user holding an <c>Optional[T]</c> what to do about it. Appended to the
    /// argument type-mismatch that <see cref="IsArgumentAssignable"/>'s refusal produces (#1397).
    ///
    /// <para>It is not decoration. <c>OptionalType</c> and <c>NullableType</c> both render as
    /// <c>T?</c>, so the strict-to-loose cell — an <c>Optional[str]</c> handed to a <c>str | None</c>
    /// formal — otherwise reads "cannot pass argument of type 'str?' to parameter of type 'str?'",
    /// a tautology the reader cannot act on. Naming the argument's Optional-ness says which of the
    /// two <c>T?</c>s is which, and states the remedy the type exists to require.</para>
    /// </summary>
    private static string DescribeOptionalArgument(SemanticType argumentType, SemanticType parameterType)
        => argumentType is OptionalType optional && parameterType is not OptionalType
            ? $" — the argument is Optional[{optional.UnderlyingType.GetDisplayName()}]; narrow it"
              + " ('if x is not None:') or unwrap it first"
            : string.Empty;

    /// <summary>
    /// The inward-conversion steer for a CLR value refused at a Sharpy-collection position:
    /// honest borders (#1517, #1531) refuse the implicit crossing, and the steer names the
    /// explicit constructor that performs it. Guarded to CLR-origin sources so a user generic
    /// that happens to share a BCL name never draws BCL advice.
    /// </summary>
    private static string DescribeClrCollectionConversionSteer(SemanticType sourceType, SemanticType targetType)
    {
        if (sourceType is GenericType { Name: BuiltinNames.Array }
            && targetType is GenericType { Name: BuiltinNames.List })
        {
            return " — a CLR array does not convert implicitly; copy explicitly with 'list(...)'";
        }

        if (sourceType is GenericType source
            && (source.ClrOriginTypeName is { Length: > 0 } || source.GenericDefinition?.ClrType is not null)
            && targetType is GenericType target)
        {
            var constructor = (source.Name, target.Name) switch
            {
                ("HashSet", BuiltinNames.Set) => "set",
                ("HashSet", BuiltinNames.FrozenSet) => "frozenset",
                ("Dictionary", BuiltinNames.Dict) => "dict",
                ("Dictionary", BuiltinNames.FrozenDict) => "frozendict",
                ("List", BuiltinNames.List) => "list",
                _ => null,
            };
            if (constructor != null)
            {
                return $" — a CLR {source.Name} does not convert implicitly; convert inward with '{constructor}(...)'";
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// The type an argument expression binds through in an iterable position — <c>list[element]</c>,
    /// with <c>element</c> whatever the recorded mark says the source iterates as — or null when the
    /// argument carries no mark. The single gate every argument-binding consumer shares (#1159, #1198).
    ///
    /// <para>Acceptance and lowering are one decision here on purpose. The mark is recorded by
    /// <see cref="RecordIterableArgumentMarks"/> before any dispatch, exactly for the iterable
    /// positions the ring knows about (<see cref="GetBuiltinIterableKeyPositions"/>,
    /// <see cref="GetMemberIterableKeyPositions"/>) and only for sources the emitter can also lower
    /// (<see cref="ClassifyIterableArgument"/>). Gating acceptance on that mark means a source is
    /// never type-accepted in a position the emitter would pass unusable — a dict passed unprojected
    /// compiles to C# handing <c>IEnumerable&lt;KeyValuePair&lt;K,V&gt;&gt;</c> where
    /// <c>IEnumerable&lt;K&gt;</c> is required (CS1503), and a tuple passed unbridged has no
    /// <c>IEnumerable&lt;T&gt;</c> at all (CS1503/CS0411). Adding a builtin/method to the position
    /// tables therefore grants acceptance and lowering together; neither can drift ahead of the
    /// other.</para>
    /// </summary>
    private SemanticType? ProjectedArgumentType(Expression? argument)
    {
        if (argument == null || _semanticInfo.GetIterableProjection(argument) is not { } projection)
            return null;

        return new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { projection.ElementType }
        };
    }

    /// <summary>
    /// Checks generic-to-generic assignability using per-type-parameter variance (#827).
    /// Same-name generics compare each argument position under the definition's declared
    /// variance; different-name generics walk the source's instantiated supertypes
    /// (interfaces and base classes) to find one matching the target, then apply the
    /// supertype definition's variance.
    /// Returns <c>true</c> when assignable (variance satisfied), <c>false</c> when
    /// authoritatively rejected (a matching definition was found but variance is
    /// violated), and <c>null</c> when no opinion (no matching definition/supertype
    /// found; the CLR reflection fallback is appropriate) (#829).
    /// </summary>
    private bool? IsGenericAssignableWithVariance(GenericType source, GenericType target)
    {
        var sourceDef = GenericInstantiationWalker.ResolveDefinition(source, _symbolTable);
        var targetDef = GenericInstantiationWalker.ResolveDefinition(target, _symbolTable);

        // Same-type fast path: compare definitions by symbol identity (#1330)
        if (sourceDef != null && targetDef != null && TypeHierarchyService.IsSameType(sourceDef, targetDef))
        {
            if (source.TypeArguments.Count != target.TypeArguments.Count)
                return false;

            if (sourceDef.TypeParameters.Count != source.TypeArguments.Count)
                return null;

            return TypeArgumentsSatisfyVariance(
                sourceDef.TypeParameters, source.TypeArguments, target.TypeArguments);
        }

        // Interface or base-class assignment: find an instantiated supertype of the
        // source matching the target's definition by symbol identity (#1330).
        var rejected = false;
        foreach (var supertype in GenericInstantiationWalker.EnumerateSupertypes(
                     source, _symbolTable, SemanticBinding, _typeResolver))
        {
            // The name comparison is a LAST RESORT, reachable only where identity cannot speak at all:
            // a CLR-discovered definition (`IList` off a walked `list[Dog]`, carrying neither module nor
            // file) against a target resolved with module context. IsSameType answers conservative-false
            // there, which means "not enough information", and taking that as "different declaration"
            // costs the two variance refusals and the cross-module interface spellings their SEMANTIC
            // verdict — `list[Dog]` into `IList[Animal]` stops being SPY0220 and becomes CS1503.
            //
            // Gated, not unconditional (#1330). Two modules each declaring `interface Holder[T]` both
            // carry DefiningFilePath, so identity is authoritative for them and the name never gets a
            // vote — which is exactly the acceptance that used to reach codegen as CS0266.
            var definitionsMatch = targetDef != null
                && (TypeHierarchyService.IsSameType(supertype.Definition, targetDef)
                    || (!TypeHierarchyService.HasComparableIdentityContext(supertype.Definition, targetDef)
                        && supertype.Definition.Name == target.Name));
            if (!definitionsMatch || supertype.TypeArguments.Count != target.TypeArguments.Count)
            {
                continue;
            }

            if (TypeArgumentsSatisfyVariance(
                    supertype.Definition.TypeParameters, supertype.TypeArguments, target.TypeArguments))
            {
                return true;
            }

            rejected = true;
        }

        return rejected ? false : (bool?)null;
    }

    /// <summary>
    /// Whether a WRITTEN generic type names the same declaration as <paramref name="target"/>.
    ///
    /// <para>The written name is not evidence of identity on its own: two modules may each declare
    /// <c>class Bag[T]</c>, and treating those as one declaration is how an argument vector from one
    /// gets stamped onto the other and reaches codegen as CS0029/CS0305 (#1330). So the written type
    /// is resolved to its definition and compared by symbol, and the name decides only where identity
    /// genuinely cannot: a synthesized type that carries no definition to resolve, or the mixed
    /// CLR/source context <see cref="TypeHierarchyService.IsSameType"/> answers conservative-false
    /// for. This is the same gate the supertype match in
    /// <see cref="IsGenericAssignableWithVariance"/> uses; the two must not drift.</para>
    /// </summary>
    private bool NamesSameDeclaration(GenericType written, TypeSymbol target)
    {
        var writtenDefinition = GenericInstantiationWalker.ResolveDefinition(written, _symbolTable);
        if (writtenDefinition == null)
            return written.Name == target.Name;

        return TypeHierarchyService.IsSameType(writtenDefinition, target)
            || (!TypeHierarchyService.HasComparableIdentityContext(writtenDefinition, target)
                && written.Name == target.Name);
    }

    /// <summary>
    /// Checks each type-argument position under the corresponding type parameter's
    /// declared variance: covariant (out) positions require source → target
    /// assignability, contravariant (in) positions require target → source, and
    /// invariant positions require equivalent types.
    /// </summary>
    private bool TypeArgumentsSatisfyVariance(
        IReadOnlyList<TypeParameterDef> typeParameters,
        IReadOnlyList<SemanticType> sourceArguments,
        IReadOnlyList<SemanticType> targetArguments)
    {
        for (int i = 0; i < sourceArguments.Count; i++)
        {
            var variance = i < typeParameters.Count
                ? typeParameters[i].Variance
                : TypeParameterVariance.None;
            var sourceArg = sourceArguments[i];
            var targetArg = targetArguments[i];

            // UnknownType acts as a wildcard — allows empty collection literals
            // (list[<?>], dict[<?>,<?>]) to satisfy any argument position.
            if (sourceArg is UnknownType || targetArg is UnknownType)
                continue;

            var satisfied = variance switch
            {
                TypeParameterVariance.Covariant => IsAssignable(sourceArg, targetArg),
                TypeParameterVariance.Contravariant => IsAssignable(targetArg, sourceArg),
                _ => sourceArg.Equals(targetArg)
                     || (sourceArg.IsAssignableTo(targetArg) && targetArg.IsAssignableTo(sourceArg)),
            };

            if (!satisfied)
                return false;
        }

        return true;
    }

    /// <summary>
    /// The Sharpy-native form of a bridge-mapped collection type — the same type with its CLR
    /// provenance dropped. A value of this type is backed by the Sharpy wrapper (<c>Sharpy.List&lt;T&gt;</c>),
    /// which is what a Sharpy slot means by <c>list[T]</c>.
    /// </summary>
    private static SemanticType NativeCollectionForm(SemanticType type)
        => type is GenericType { ClrOriginTypeName: not null } mapped
            ? mapped with { ClrOriginTypeName = null }
            : type;

    /// <summary>
    /// The element of a bridge-mapped CLR sequence INTERFACE — a generic the bridge kept as ITSELF
    /// (<c>IOrderedEnumerable[T]</c>) instead of collapsing onto a Sharpy collection name, and which
    /// .NET says is an <c>IEnumerable&lt;E&gt;</c>. Null for anything else.
    ///
    /// <para>
    /// Such a value is a sequence that no Sharpy collection name describes, so it reaches a
    /// <c>list[E]</c> slot by the same pair a mapped <c>list[E]</c> uses: <see cref="IsAssignable"/>
    /// accepts it and <see cref="RecordSequenceMaterialization"/> emits the copy. The two must agree —
    /// accepting an assignment the materialization ring cannot convert only moves the failure into
    /// codegen (#1390).
    /// </para>
    ///
    /// <para>
    /// Scoped to INTERFACES: a mapped CLR class (<c>NdArray[T]</c>) is a first-class Sharpy type whose
    /// own identity a list slot must not silently consume, and a mapped collection already has an arm
    /// of its own below.
    /// </para>
    /// </summary>
    private static SemanticType? TryGetClrSequenceInterfaceElement(SemanticType type)
    {
        if (type is not GenericType { TypeArguments.Count: 1 } mapped
            || mapped.GenericDefinition?.ClrType is not { IsInterface: true, IsGenericTypeDefinition: true } definition)
        {
            return null;
        }

        var isSequence = definition.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return isSequence ? mapped.TypeArguments[0] : null;
    }

    /// <summary>
    /// Whether <paramref name="type"/> is a value the checker calls a Sharpy collection but codegen
    /// emits as some OTHER CLR type — a bridge-mapped <c>list</c>/<c>set</c>/<c>dict</c> whose origin
    /// is not the Sharpy wrapper. These are the values that need materializing before they can be used
    /// as what their own semantic type says they are (#1251).
    ///
    /// <para>
    /// A mapping whose origin IS the Sharpy wrapper (<c>Sharpy.List&lt;T&gt;</c> mapped to
    /// <c>list[T]</c>) is excluded: the emitted type already matches, so wrapping it would add a copy
    /// that changes aliasing while fixing nothing. So is any mapped generic that is not one of the
    /// three collections — a <c>DictKeyView</c> is not a slot type.
    /// </para>
    /// </summary>
    private static bool IsUnmaterializedClrSequence(SemanticType type)
    {
        // A CLR sequence interface the bridge kept as itself is unmaterialized by construction: no
        // Sharpy collection name describes it, so nothing about it can already BE the wrapper (#1390).
        if (TryGetClrSequenceInterfaceElement(type) is { } interfaceElement)
            return IsMaterializableElement(interfaceElement);

        // #1517 honest borders, decided: a concrete HashSet/Dictionary/List no longer spells a
        // builtin name, so it deliberately exits this predicate (and the materialization it
        // gates). That is the policy, not a leak — implicit materialization at the seam was
        // exactly the double-identity honest borders retired; those values now REFUSE at
        // annotation/argument/operator positions with an inward-conversion steer instead of
        // silently copying. Only bridge-collapsed spellings (interfaces, Sharpy wrappers) remain
        // materialization candidates below.
        if (type is not GenericType { ClrOriginTypeName: { Length: > 0 } origin } mapped)
            return false;

        var sharpyWrapper = mapped.Name switch
        {
            BuiltinNames.List => Discovery.ClrTypeBridge.SpecialCases.SharpyListFullName,
            BuiltinNames.Dict => Discovery.ClrTypeBridge.SpecialCases.SharpyDictFullName,
            BuiltinNames.Set => Discovery.ClrTypeBridge.SpecialCases.SharpySetFullName,
            _ => null
        };

        if (sharpyWrapper == null || origin == sharpyWrapper)
            return false;

        // A collection whose element the bridge could not represent (`object`) is left alone. The
        // element type is a degradation, not a fact, and materializing would MAKE IT BINDING: today
        // the emitter writes `var groups = xs.GroupBy(...)` and C# keeps the precise
        // IEnumerable<IGrouping<K,V>>, so `g.key` works; building a Sharpy.List<object> from it turns
        // that into CS1061. Recording nothing keeps the permissive channel, which is the same call
        // CompleteStagedExtensionCall already makes for an object-collapsed return (#1206 D2) —
        // `object` there is "strictly WORSE than the Unknown it has today", and it is worse here too.
        //
        // So is a collection whose element the bridge RE-REPRESENTS: materializing emits
        // `new Sharpy.List<T'>(source)`, whose constructor takes `IEnumerable<T'>` — and a source
        // yielding the CLR `T` is not that. `IEnumerable<KeyValuePair<K,V>>` (element mapped to a
        // tuple) and `IEnumerable<List<T>>` (element mapped to `Sharpy.List<T>`) are both this
        // shape, and both turn a would-be copy into CS1503 at the constructor call (#1343).
        return mapped.TypeArguments.All(IsMaterializableElement);
    }

    /// <summary>
    /// Whether a mapped collection's element has the same CLR representation on both sides of the
    /// materializing constructor. Excluded: <c>object</c> (a degradation — see
    /// <see cref="IsUnmaterializedClrSequence"/>), a tuple (the bridge's <c>KeyValuePair</c> form),
    /// and a nested CLR-backed collection whose origin is not already the Sharpy wrapper.
    /// </summary>
    private static bool IsMaterializableElement(SemanticType element)
    {
        if (IsObjectType(element) || element is TupleType)
            return false;

        if (element is not GenericType { ClrOriginTypeName: { Length: > 0 } origin } nested)
            return true;

        var sharpyWrapper = nested.Name switch
        {
            BuiltinNames.List => Discovery.ClrTypeBridge.SpecialCases.SharpyListFullName,
            BuiltinNames.Dict => Discovery.ClrTypeBridge.SpecialCases.SharpyDictFullName,
            BuiltinNames.Set => Discovery.ClrTypeBridge.SpecialCases.SharpySetFullName,
            _ => null
        };

        return origin == sharpyWrapper;
    }

    /// <summary>
    /// Records that <paramref name="value"/> must be materialized into a Sharpy collection, when it is
    /// a CLR sequence landing in a slot that means the Sharpy one (#1251).
    ///
    /// <para>
    /// A "slot" is any position that binds the value as a Sharpy value rather than passing it back to
    /// .NET: a variable declaration (annotated OR inferred — an inferred local is just as much a Sharpy
    /// slot, and it is the case the issue's own "the inferred form works" control got wrong), a
    /// reassignment, a <c>return</c> against a declared Sharpy collection, and a call argument bound to
    /// a Sharpy-native parameter. Positions that hand the value to CLR code are deliberately NOT slots:
    /// materializing a bare property read would insert a copy per read and quietly break mutation
    /// through a CLR collection, which is the aliasing hazard this rule exists to avoid.
    /// </para>
    ///
    /// <para>
    /// Copy semantics are deliberate and are exactly Python's <c>list(...)</c> — the explicit spelling
    /// of the same conversion, whose emitted form this reuses.
    /// </para>
    /// </summary>
    private void RecordSequenceMaterialization(Expression? value, SemanticType valueType, SemanticType slotType)
    {
        if (value == null || !IsUnmaterializedClrSequence(valueType))
            return;

        // The slot must mean a Sharpy collection. A slot that is itself CLR-mapped is a .NET position:
        // the emitted formal is the CLR type and the value goes in unconverted (#1260).
        if (slotType is not GenericType { ClrOriginTypeName: null, Name: BuiltinNames.List or BuiltinNames.Dict or BuiltinNames.Set })
            return;

        _semanticInfo.SetSequenceMaterialization(UnwrapParenthesized(value), slotType);
    }

    /// <summary>
    /// Rewrites a reflected CLR SEQUENCE of <c>char</c> into the sequence of one-character <c>str</c>
    /// it is at the Sharpy surface, recording on <paramref name="producer"/> the per-element
    /// conversion codegen must apply (#1401). Returns <paramref name="mapped"/> unchanged, and
    /// records nothing, for every other type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sequence row of the #1291 char family, and the one shape that family deliberately left
    /// alone: converting a CLR sequence's ELEMENT is the case
    /// <see cref="IsUnmaterializedClrSequence"/>'s "a collection whose element the bridge
    /// RE-REPRESENTS" comment names, so this states the conversion that makes such a collection
    /// materializable instead of adding a second mechanism beside #1251's rule. The two compose at
    /// one choke point: the element projection runs first and #1251's wrap second, which is the order
    /// <c>RoslynEmitter.GenerateExpression</c> already applies them in.
    /// </para>
    /// <para>
    /// Recorded on the PRODUCER, exactly as the scalar and array rows are, so every downstream
    /// position — a <c>list()</c> conversion, an annotated slot, an inferred local, an argument — is
    /// ordinary <c>list[str]</c> handling with nothing further to know. Without it,
    /// <c>list(s.take(3))</c> emitted <c>new Sharpy.List&lt;string&gt;(IEnumerable&lt;char&gt;)</c>
    /// and failed as CS1503 behind SPY0908.
    /// </para>
    /// </remarks>
    private SemanticType ProjectClrCharSequence(Expression producer, SemanticType mapped)
    {
        if (mapped is not GenericType { TypeArguments: { Count: 1 } elements } sequence
            || ClrCharProjection(elements[0])?.Kind != CharMaterializationKind.Scalar
            || !IsUnmaterializedClrSequence(mapped))
        {
            return mapped;
        }

        _semanticInfo.SetCharMaterialization(producer, CharMaterializationKind.Sequence);
        return sequence with { TypeArguments = new List<SemanticType> { SemanticType.Str } };
    }

    /// <summary>
    /// Attempts to resolve a CLR <see cref="Type"/> for a <see cref="SemanticType"/>, including
    /// constructing concrete generic types for Sharpy collection generics (list/dict/set). This
    /// enables CLR assignability checks (e.g., passing a <c>list[int]</c> to a method parameter
    /// typed as <c>IEnumerable</c>).
    /// </summary>
    private Type? TryGetClrType(SemanticType type)
    {
        switch (type)
        {
            case BuiltinType bt:
                return bt.ClrType;
            case UnmappedClrType:
                return null;
            case UserDefinedType udt:
                return udt.Symbol?.ClrType;
            case NullableType nt:
                {
                    // A `T | None` over a VALUE payload IS `Nullable<T>` at runtime, and answering the
                    // bare payload here made the CLR fallback in IsAssignable decide a question about a
                    // value that does not exist — `int.IsAssignableFrom(int)` for a source that is an
                    // `int?` — so an un-unwrapped `int | None` bound a plain-`int` formal and failed
                    // downstream as CS1503, reported to the user as a compiler bug (#1399). Same shape
                    // as the OptionalType arm below (#1397), and the same fix.
                    //
                    // A REFERENCE payload stays unwrapped, deliberately: `string?` IS `string` at
                    // runtime, C# binds `shout(v)` natively, and tagged_unions_optional.md documents
                    // that looseness as the point of the interop type ("allowed — throws at runtime on
                    // None"). Telling the truth about the value case costs that cell nothing.
                    var payloadClr = TryGetClrType(nt.UnderlyingType);
                    if (payloadClr == null
                        || !payloadClr.IsValueType
                        || payloadClr == typeof(void)
                        || Nullable.GetUnderlyingType(payloadClr) != null)
                    {
                        return payloadClr;
                    }

                    try
                    {
                        return typeof(Nullable<>).MakeGenericType(payloadClr);
                    }
                    catch
                    {
                        return null;
                    }
                }
            case OptionalType ot:
                {
                    // An Optional IS `Sharpy.Optional<T>`; it is not its payload. Answering the payload
                    // here made the CLR fallback in IsAssignable decide a question about a value that
                    // does not exist — `List<int>.IsAssignableFrom(List<int>)` for a source that is an
                    // `Optional<List<int>>` — so an un-unwrapped Optional bound to a plain-T formal and
                    // failed downstream as CS1503, reported to the user as a compiler bug (#1397).
                    //
                    // Nothing is lost by telling the truth: Sharpy.Core declares exactly one conversion,
                    // `implicit operator Optional<T>(T)` (Optional.cs), which is the WRAP direction. There
                    // is no Optional→T conversion for any check to find, so every acceptance the payload
                    // mapping produced was a CS1503 in waiting. The caller narrows (`if x is not None`) or
                    // unwraps, which is the entire guarantee `T?` exists to provide.
                    var payloadClr = TryGetClrType(ot.UnderlyingType);
                    if (payloadClr == null)
                        return null;
                    try
                    {
                        return typeof(SharpyRT::Sharpy.Optional<>).MakeGenericType(payloadClr);
                    }
                    catch
                    {
                        return null;
                    }
                }
            case TupleType tt:
                {
                    // A tuple IS a System.ValueTuple<...> at runtime — that is what the emitter
                    // writes and what ClrTypeBridge maps back. Without this arm every tuple answered
                    // "no CLR type", and the callers split on how they treat that: the GenericType
                    // arm below substitutes typeof(object) and keeps going, while
                    // ClrTypeHelper.ClrOriginIsSatisfiedBy (correctly) refuses to guess and returns
                    // false. So a CLR-provenanced formal like `iter`'s IEnumerable<T> rejected a
                    // tuple argument it binds natively — `iter[tuple[int, int]](pairs)` was SPY0354
                    // while `iter[int]`/`iter[list[int]]` resolved (#1470). The gap was invisible for
                    // every other element type because only tuples reach the default arm.
                    var elementClrTypes = new Type[tt.ElementTypes.Count];
                    for (var i = 0; i < tt.ElementTypes.Count; i++)
                    {
                        var elementClr = TryGetClrType(tt.ElementTypes[i]);
                        if (elementClr == null)
                            return null;
                        elementClrTypes[i] = elementClr;
                    }

                    // ValueTuple is declared for arities 1-7 plus an 8th TRest form. Arities beyond
                    // 7 return null rather than building the nested TRest encoding: answering
                    // "unknown" is what every caller already handles, and a wrong shape here would
                    // be worse than no answer.
                    Type? openTuple = elementClrTypes.Length switch
                    {
                        1 => typeof(ValueTuple<>),
                        2 => typeof(ValueTuple<,>),
                        3 => typeof(ValueTuple<,,>),
                        4 => typeof(ValueTuple<,,,>),
                        5 => typeof(ValueTuple<,,,,>),
                        6 => typeof(ValueTuple<,,,,,>),
                        7 => typeof(ValueTuple<,,,,,,>),
                        _ => null,
                    };
                    if (openTuple == null)
                        return null;

                    try
                    {
                        return openTuple.MakeGenericType(elementClrTypes);
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                }
            case GenericType gt:
                {
                    if (gt.Name == BuiltinNames.Array && gt.TypeArguments.Count == 1)
                    {
                        var elemClr = TryGetClrType(gt.TypeArguments[0]);
                        return elemClr?.MakeArrayType();
                    }

                    Type? openType = gt.Name switch
                    {
                        "list" => typeof(SharpyRT::Sharpy.List<>),
                        "dict" => typeof(SharpyRT::Sharpy.Dict<,>),
                        "set" => typeof(SharpyRT::Sharpy.Set<>),
                        _ => null,
                    };
                    if (openType == null)
                    {
                        var candidateClr = gt.GenericDefinition?.ClrType
                            ?? _symbolTable.LookupType(gt.Name)?.ClrType;
                        if (candidateClr != null && candidateClr.IsGenericTypeDefinition
                            && candidateClr.GetGenericArguments().Length == gt.TypeArguments.Count)
                        {
                            openType = candidateClr;
                        }
                        else if (candidateClr != null && !candidateClr.IsGenericTypeDefinition)
                        {
                            // ClrType is non-generic (e.g., IEnumerable instead of IEnumerable<T>).
                            // Search loaded assemblies for the generic version with matching arity.
                            var arity = gt.TypeArguments.Count;
                            var genericName = candidateClr.Name.Contains('`', StringComparison.Ordinal)
                                ? candidateClr.Name
                                : candidateClr.Name + "`" + arity;
                            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                            {
                                try
                                {
                                    var match = asm.GetType(candidateClr.Namespace + "." + genericName)
                                        ?? asm.GetTypes().FirstOrDefault(t =>
                                            t.IsGenericTypeDefinition
                                            && Shared.ClrNameHelper.StripArity(t.Name) == gt.Name
                                            && t.GetGenericArguments().Length == arity);
                                    if (match != null)
                                    {
                                        openType = match;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    if (openType == null || !openType.IsGenericTypeDefinition)
                        return null;
                    var clrTypeArgs = gt.TypeArguments.Select(ta => TryGetClrType(ta) ?? typeof(object)).ToArray();
                    if (clrTypeArgs.Length != openType.GetGenericArguments().Length)
                        return null;
                    try
                    {
                        return openType.MakeGenericType(clrTypeArgs);
                    }
                    catch
                    {
                        return null;
                    }
                }
            default:
                return type.ClrType ?? type.DeclaringSymbol?.ClrType;
        }
    }

    /// <summary>
    /// Extracts the Invoke method from a delegate type, substituting type parameters
    /// for generic delegates. Returns null if the type is not a delegate.
    /// </summary>
    private FunctionSymbol? TryGetDelegateInvokeMethod(SemanticType type)
    {
        TypeSymbol? delegateSymbol = null;
        List<SemanticType>? typeArgs = null;

        if (type is UserDefinedType { Symbol: { TypeKind: TypeKind.Delegate } udt })
        {
            delegateSymbol = udt;
        }
        else if (type is GenericType gt && gt.GenericDefinition is { TypeKind: TypeKind.Delegate })
        {
            delegateSymbol = gt.GenericDefinition;
            typeArgs = gt.TypeArguments;
        }

        if (delegateSymbol == null)
            return null;

        var invoke = delegateSymbol.Methods.FirstOrDefault(m => m.Name == "Invoke");
        if (invoke == null)
            return null;

        // For generic delegates, substitute type parameters in the Invoke signature
        if (typeArgs != null && delegateSymbol.TypeParameters.Count == typeArgs.Count)
        {
            var substitutions = new Dictionary<string, SemanticType>();
            for (int i = 0; i < delegateSymbol.TypeParameters.Count; i++)
            {
                substitutions[delegateSymbol.TypeParameters[i].Name] = typeArgs[i];
            }

            var substitutedParams = invoke.Parameters.Select(p => p with
            {
                Type = TypeSubstitution.Apply(p.Type, substitutions)
            }).ToList();
            var substitutedReturn = TypeSubstitution.Apply(invoke.ReturnType, substitutions);

            return invoke with
            {
                Parameters = substitutedParams,
                ReturnType = substitutedReturn
            };
        }

        return invoke;
    }

    /// <summary>
    /// Check if all types in a list are assignable to a target type.
    /// Used by contextual type inference for collection literals.
    /// </summary>
    private bool AllAssignableTo(List<SemanticType> types, SemanticType target)
    {
        return types.All(t => IsAssignable(t, target));
    }

    /// <summary>
    /// Substitutes type parameters with their corresponding type arguments in a type.
    /// For example, given return type T and type argument int, returns int.
    /// </summary>
    private SemanticType SubstituteTypeParameters(
        SemanticType type,
        List<TypeParameterDef> typeParams,
        List<SemanticType> typeArgs,
        bool substituteNamedUserTypes = false)
    {
        if (typeParams.Count != typeArgs.Count)
            return type;

        var substitutions = new Dictionary<string, SemanticType>();
        for (int i = 0; i < typeParams.Count; i++)
        {
            substitutions[typeParams[i].Name] = typeArgs[i];
        }

        return TypeSubstitution.Apply(type, substitutions, substituteNamedUserTypes);
    }

    /// <summary>
    /// Recursively checks whether <paramref name="type"/> references a type parameter with the
    /// given <paramref name="name"/> (used to determine where a specific generic parameter appears).
    /// </summary>
    private static bool ReferencesTypeParameterNamed(SemanticType type, string name)
    {
        return type switch
        {
            TypeParameterType tp => tp.Name == name,
            ResultType rt => ReferencesTypeParameterNamed(rt.OkType, name) || ReferencesTypeParameterNamed(rt.ErrorType, name),
            OptionalType ot => ReferencesTypeParameterNamed(ot.UnderlyingType, name),
            NullableType nt => ReferencesTypeParameterNamed(nt.UnderlyingType, name),
            GenericType gt => gt.TypeArguments.Any(t => ReferencesTypeParameterNamed(t, name)),
            FunctionType ft => ft.ParameterTypes.Any(t => ReferencesTypeParameterNamed(t, name)) || ReferencesTypeParameterNamed(ft.ReturnType, name),
            TupleType tt => tt.ElementTypes.Any(t => ReferencesTypeParameterNamed(t, name)),
            _ => false
        };
    }

    /// <summary>
    /// Checks if a type contains any unresolved TypeParameterType instances.
    /// Used to detect method-level generic type parameters that need inference.
    /// </summary>
    private static bool ContainsTypeParameterType(SemanticType type)
    {
        return type switch
        {
            TypeParameterType => true,
            ResultType rt => ContainsTypeParameterType(rt.OkType) || ContainsTypeParameterType(rt.ErrorType),
            OptionalType ot => ContainsTypeParameterType(ot.UnderlyingType),
            NullableType nt => ContainsTypeParameterType(nt.UnderlyingType),
            GenericType gt => gt.TypeArguments.Any(ContainsTypeParameterType),
            FunctionType ft => ft.ParameterTypes.Any(ContainsTypeParameterType) || ContainsTypeParameterType(ft.ReturnType),
            TupleType tt => tt.ElementTypes.Any(ContainsTypeParameterType),
            _ => false
        };
    }

    /// <summary>
    /// Checks if an expression is a valid assignment target.
    /// Valid targets: Identifier, MemberAccess (attribute), IndexAccess, TupleLiteral (for unpacking)
    /// Invalid targets: FunctionCall, Literal, BinaryExpression, etc.
    /// </summary>
    private bool IsValidAssignmentTarget(Expression target)
    {
        return target switch
        {
            Identifier => true,
            MemberAccess => true,
            IndexAccess => true,
            TupleLiteral tuple => tuple.Elements.All(IsValidAssignmentTarget),
            StarExpression star => IsValidAssignmentTarget(star.Operand),
            _ => false
        };
    }

    /// <summary>
    /// Gets a human-readable description of an invalid assignment target for error messages.
    /// </summary>
    private string GetAssignmentTargetDescription(Expression target)
    {
        return target switch
        {
            FunctionCall call => UnwrapParenthesized(call.Function) is Identifier id ? $"function call '{id.Name}()'" : "function call result",
            IntegerLiteral => "integer literal",
            FloatLiteral => "float literal",
            StringLiteral => "string literal",
            BooleanLiteral => "boolean literal",
            NoneLiteral => "'None'",
            ListLiteral => "list literal",
            DictLiteral => "dictionary literal",
            SetLiteral => "set literal",
            BinaryOp => "expression result",
            UnaryOp => "expression result",
            ConditionalExpression => "conditional expression result",
            ComparisonChain => "comparison result",
            _ => "expression"
        };
    }

    /// <summary>
    /// Extract element type from an iterable type.
    /// Delegates to <see cref="TypeInferenceService.InferIterableElementType"/>.
    /// </summary>
    private SemanticType ExtractElementType(SemanticType iterType)
        => _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;

    /// <summary>
    /// Check if a method name is a dunder method (starts and ends with __ and has content in between)
    /// </summary>
    private static bool IsDunderMethod(string name) =>
        name.StartsWith("__") && name.EndsWith("__") && name.Length > 4;

    /// <summary>
    /// Validate standalone super() expression (which is always invalid - must be followed by method call)
    /// </summary>
    private SemanticType CheckSuperExpression(SuperExpression superExpr)
    {
        // Standalone super() is not valid - must be used as super().method()
        // The parser allows it, but semantically it's invalid
        AddError("super() must be followed by a method call (e.g., super().__init__())",
            superExpr.LineStart, superExpr.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidSuperUsage,
            span: superExpr.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Validate super().method() member access and return the method's type
    /// </summary>
    private SemanticType ValidateSuperMemberAccess(MemberAccess memberAccess, SuperExpression superExpr)
    {
        var memberName = memberAccess.Member;

        // Check 1: Must be inside a class
        if (_currentClass == null)
        {
            AddError("super() cannot be used outside of a class",
                superExpr.LineStart, superExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.SuperOutsideClass,
                span: superExpr.Span);
            return SemanticType.Unknown;
        }

        // Check 2: Class must have a parent
        var classBaseType = GetBaseType(_currentClass);
        if (classBaseType == null)
        {
            AddError($"super() cannot be used in class '{_currentClass.Name}' which has no parent class",
                superExpr.LineStart, superExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.SuperNoParent,
                span: superExpr.Span);
            return SemanticType.Unknown;
        }

        // Check 3: Cannot access fields via super()
        // Check the entire inheritance chain for fields
        var currentType = classBaseType;
        while (currentType != null)
        {
            var field = currentType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field != null)
            {
                AddError("Cannot access parent fields via super(); only methods are allowed",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: memberAccess.Span);
                return SemanticType.Unknown;
            }
            currentType = GetBaseType(currentType);
        }

        // Check 4: Validate based on method context
        ValidateSuperContextRules(memberName, superExpr, memberAccess);

        // Look up the method in the parent class hierarchy and return its type
        // Use FindMethodInHierarchy to traverse the full inheritance chain
        var (parentMethod, methodOwner) = FindMethodInHierarchy(classBaseType, memberName);
        if (parentMethod == null && memberName == DunderNames.Init)
        {
            // __init__ might be in Constructors list - check full hierarchy
            currentType = classBaseType;
            while (currentType != null)
            {
                // For .NET types, we can't do proper overload resolution here
                // (we don't have access to the call arguments). Mark the type to skip validation
                // and let C# do the overload resolution at compile time.
                if (currentType.ClrType != null)
                {
                    return new FunctionType
                    {
                        ParameterTypes = new List<SemanticType>(),
                        ReturnType = SemanticType.Void,
                        SkipArgumentValidation = true
                    };
                }

                // When the parent has multiple constructor overloads, defer argument
                // validation to the C# compiler — mirroring how direct constructor calls
                // (CheckConstructorCall) skip strict validation for overloaded __init__.
                if (currentType.Constructors.Count > 1)
                {
                    return new FunctionType
                    {
                        ParameterTypes = new List<SemanticType>(),
                        ReturnType = SemanticType.Void,
                        SkipArgumentValidation = true
                    };
                }

                var parentCtor = currentType.Constructors.FirstOrDefault();
                if (parentCtor != null)
                {
                    return FunctionType.FromParameters(
                        parentCtor.Parameters, SemanticType.Void, skipLeading: 1);
                }
                currentType = GetBaseType(currentType);
            }
        }

        if (parentMethod != null)
        {
            // __init__ is stored in both Methods and Constructors. When the owning type
            // has multiple constructor overloads, defer argument validation to the C#
            // compiler — mirroring how direct constructor calls (CheckConstructorCall)
            // skip strict validation for overloaded __init__.
            if (memberName == DunderNames.Init && methodOwner != null && methodOwner.Constructors.Count > 1)
            {
                return new FunctionType
                {
                    ParameterTypes = new List<SemanticType>(),
                    ReturnType = SemanticType.Void,
                    SkipArgumentValidation = true
                };
            }

            return FunctionType.FromParameters(
                parentMethod.Parameters, parentMethod.ReturnType, skipLeading: 1);
        }

        // Also check properties in the parent hierarchy (e.g. super().age in
        // an @override property getter). Properties generate C# base.Property
        // so they resolve at runtime; we just need the property type here.
        var (parentProperty, _) = FindPropertyInHierarchy(classBaseType, memberName);
        if (parentProperty != null)
            return parentProperty.Type;

        AddError($"No method '{memberName}' found in parent class hierarchy of '{_currentClass.Name}'",
            memberAccess.LineStart, memberAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.UndefinedMember,
            span: memberAccess.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Resolves the constructor (<c>__init__</c>) overload candidates that an initializer call —
    /// <c>super().__init__(...)</c> or <c>self.__init__(...)</c> — could bind to. Walks
    /// <paramref name="targetType"/> and its base hierarchy (via <see cref="GetBaseType"/>) and
    /// returns <em>all</em> overloads of the first type that declares any constructors, mirroring
    /// the hierarchy walk in <see cref="ValidateSuperMemberAccess"/> (first type yielding
    /// constructors wins). For <c>super</c>, callers pass the base type; for <c>self</c>, callers
    /// pass the current class.
    /// <para>
    /// Unlike <see cref="ValidateSuperMemberAccess"/> — which defers argument validation entirely
    /// when a base has more than one overload — this returns the complete overload set so callers
    /// can validate keyword-argument names against the <em>union</em> of overloads.
    /// </para>
    /// <para>
    /// A CLR-backed type (<see cref="TypeSymbol.ClrType"/> != null) whose constructors are not
    /// enumerated in metadata yields an empty list, signalling callers to skip validation rather
    /// than reject otherwise-valid keyword arguments.
    /// </para>
    /// </summary>
    private IReadOnlyList<FunctionSymbol> ResolveInitializerConstructorCandidates(TypeSymbol targetType)
    {
        var currentType = targetType;
        while (currentType != null)
        {
            // First type yielding constructors wins — return the full overload set.
            if (currentType.Constructors.Count > 0)
                return currentType.Constructors;

            // CLR-backed type with no enumerated constructors: no metadata to validate
            // against, so signal callers to skip rather than reject valid kwargs.
            if (currentType.ClrType != null)
                return Array.Empty<FunctionSymbol>();

            currentType = GetBaseType(currentType);
        }

        return Array.Empty<FunctionSymbol>();
    }

    /// <summary>
    /// Validate super() context rules based on current method type
    /// </summary>
    private void ValidateSuperContextRules(string calledMethodName, SuperExpression superExpr, MemberAccess memberAccess)
    {
        if (_currentMethodName == null)
        {
            AddError("super() cannot be used outside of a method",
                superExpr.LineStart, superExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                span: superExpr.Span);
            return;
        }

        // Case 1: Inside __init__
        if (_currentMethodName == DunderNames.Init)
        {
            if (calledMethodName != DunderNames.Init)
            {
                AddError("super() in __init__ can only call super().__init__(...)",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: memberAccess.Span);
            }
            else if (_controlFlowDepth > 0)
            {
                AddError("super().__init__() must be the first statement in the constructor, not inside control flow",
                    superExpr.LineStart, superExpr.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: superExpr.Span);
            }
            else if (_superInitCalled)
            {
                AddError("super().__init__() can only be called once",
                    superExpr.LineStart, superExpr.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: superExpr.Span);
            }
            return;
        }

        // Case 2: Inside @override method
        if (_currentMethodIsOverride)
        {
            // In @override methods, can call same method name
            // OR if it's a dunder override, can call other dunders (cross-dunder)
            if (calledMethodName != _currentMethodName)
            {
                if (!(_currentMethodIsDunder && IsDunderMethod(calledMethodName)))
                {
                    AddError($"super() in @override method must call super().{_currentMethodName}(...)",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                        span: memberAccess.Span);
                }
            }
            return;
        }

        // Case 3: Inside dunder method (not __init__, not @override)
        if (_currentMethodIsDunder)
        {
            // Dunder methods can call any dunder via super()
            if (!IsDunderMethod(calledMethodName))
            {
                AddError("super() in dunder method must call a dunder method (e.g., super().__eq__(...))",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: memberAccess.Span);
            }
            return;
        }

        // Case 4: Regular method - super() not allowed
        AddError("super() cannot be used in regular methods; only in __init__, @override, or dunder methods",
            superExpr.LineStart, superExpr.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidSuperUsage,
            span: superExpr.Span);
    }

    /// <summary>
    /// Collect all interfaces a type implements, including:
    /// - Directly implemented interfaces
    /// - Base interfaces (interface inheritance)
    /// - Interfaces implemented by base classes
    /// </summary>
    private TypeSymbolSet CollectAllInterfaces(TypeSymbol type)
    {
        var all = TypeHierarchyService.GetAllInterfaces(type, SemanticBinding);
        var result = new TypeSymbolSet();
        foreach (var iface in all)
            result.Add(iface);
        return result;
    }

    /// <summary>
    /// Finds the least common ancestor (most specific common base type) of a list of types.
    /// Returns SemanticType.Object if no more specific common ancestor exists.
    /// Returns SemanticType.Unknown only if types list is empty.
    /// </summary>
    private SemanticType FindLeastCommonAncestor(List<SemanticType> types)
    {
        if (types.Count == 0)
            return SemanticType.Unknown;
        if (types.Count == 1)
            return types[0];

        // Get all ancestors of the first type (including itself)
        var ancestorChain = GetTypeAncestorChain(types[0]);
        if (ancestorChain.Count == 0)
            return SemanticType.Object;

        // For each subsequent type, find common ancestors
        foreach (var type in types.Skip(1))
        {
            var typeAncestors = new HashSet<string>(
                GetTypeAncestorChain(type).Select(t => GetTypeKey(t)));

            // Filter ancestor chain to only include common ancestors
            ancestorChain = ancestorChain
                .Where(a => typeAncestors.Contains(GetTypeKey(a)))
                .ToList();

            if (ancestorChain.Count == 0)
                return SemanticType.Object;
        }

        // Return the most specific common ancestor (first in chain)
        return ancestorChain.First();
    }

    /// <summary>
    /// Gets a unique key for a type to use in LCA comparison.
    /// </summary>
    private static string GetTypeKey(SemanticType type)
    {
        return type switch
        {
            UserDefinedType udt => udt.Name,
            BuiltinType bt => bt.Name,
            GenericType gt => $"{gt.Name}<{string.Join(",", gt.TypeArguments.Select(GetTypeKey))}>",
            NullableType nt => $"{GetTypeKey(nt.UnderlyingType)}|None",
            OptionalType ot => $"{GetTypeKey(ot.UnderlyingType)}?",
            ResultType rt => $"{GetTypeKey(rt.OkType)}!{GetTypeKey(rt.ErrorType)}",
            _ => type.GetDisplayName()
        };
    }

    /// <summary>
    /// Gets the inheritance chain for a type, from most specific to least specific.
    /// For UserDefinedType: [Type, BaseType, BaseType.BaseType, ..., object]
    /// For primitives: [PrimitiveType, object]
    /// </summary>
    private List<SemanticType> GetTypeAncestorChain(SemanticType type)
        => TypeHierarchyService.GetAncestorChain(type, SemanticBinding).ToList();

    /// <summary>
    /// Marks an expression as error recovery in SemanticInfo, on the stated
    /// <paramref name="reason"/>, and increments the recovery counter. The counter enables
    /// transitive propagation: when a sub-expression is marked as error recovery, parent
    /// expressions that return Unknown can detect this and also mark themselves. Use this instead
    /// of calling <c>_semanticInfo.MarkErrorRecovery()</c> directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reason is required, and that requirement is the contract. This mark tells the invariant
    /// checker not to raise SPY0907 for an Unknown-typed node — it is the compiler asserting that
    /// the Unknown is accounted for. A mark with nothing behind it makes that assertion falsely, and
    /// the reference then reaches code generation unchecked and comes back as SPY0908: #1344's
    /// container members and #1389's <c>object</c> members were both exactly that. Naming the ground
    /// (<see cref="ErrorRecoveryReason"/>) makes the unaccounted case unrepresentable rather than
    /// merely discouraged.
    /// </para>
    /// <para>
    /// Only <see cref="ErrorRecoveryKind.DeliberatelyPermissive"/> is logged. It is the one class
    /// where nothing is reported to anyone, so it is the one worth being able to watch; the other two
    /// are already visible as diagnostics.
    /// </para>
    /// </remarks>
    private void MarkExpressionAsErrorRecovery(Expression expr, ErrorRecoveryReason reason)
    {
        _semanticInfo.MarkErrorRecovery(expr);
        _errorRecoveryMarkCount++;

        if (reason.Kind == ErrorRecoveryKind.DeliberatelyPermissive)
        {
            _logger.LogDebug(
                $"Permissive recovery at {expr.LineStart}:{expr.ColumnStart} " +
                $"({expr.GetType().Name}): {reason.Explanation}");
        }
    }

    /// <summary>
    /// Sets an expression's type to UnknownType and marks it as error recovery in SemanticInfo, on
    /// the stated <paramref name="reason"/>. Use this when the Unknown type is expected because a
    /// user-facing diagnostic was emitted. This allows the invariant checker to distinguish
    /// intentional error recovery from silent type inference failures (compiler bugs).
    /// </summary>
    private void SetErrorRecoveryType(Expression expr, ErrorRecoveryReason reason)
    {
        _semanticInfo.SetExpressionType(expr, SemanticType.Unknown);
        MarkExpressionAsErrorRecovery(expr, reason);
    }

    /// <summary>
    /// Reports SPY0204 and returns true when <paramref name="name"/> is already bound to a
    /// non-variable symbol (function, class, module, ...) in the current scope — e.g. an
    /// assignment colliding with a hoisted module-level <c>def</c> of the same name.
    /// <see cref="Scope.Define"/> treats that collision as a compiler-bug invariant and throws,
    /// so every reachable path that defines a <see cref="VariableSymbol"/> must check first.
    /// Variable-over-variable rebinding stays allowed (Python-like re-binding).
    /// </summary>
    private bool TryReportNonVariableRedefinition(string name, int line, int column, Text.TextSpan? span)
    {
        var existing = _symbolTable.Lookup(name, searchParents: false);
        if (existing is null or VariableSymbol)
            return false;

        var kind = existing switch
        {
            FunctionSymbol => "function",
            TypeSymbol => "type",
            ModuleSymbol => "module",
            TypeAliasSymbol => "type alias",
            TypeParameterSymbol => "type parameter",
            _ => "symbol",
        };
        AddError($"Cannot bind '{name}': it is already defined as a {kind} in this scope",
            line, column, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: span);
        return true;
    }

    /// <summary>
    /// Records a type-checking error. When the error relates to a relationship between
    /// two nodes (e.g., "type X is not assignable to type Y"), use the *target* node's
    /// span — that's where the user needs to fix the code.
    /// </summary>
    private void AddError(string message, int? line = null, int? column = null, string? code = null,
        Text.TextSpan? span = null, IReadOnlyDictionary<string, string>? data = null)
    {
        if (_diagnostics.ErrorCount >= MaxErrors)
        {
            if (!_maxErrorsReported)
            {
                _maxErrorsReported = true;
                _diagnostics.AddWarning(
                    $"Too many errors ({MaxErrors}); further errors suppressed. Use '--max-errors' to increase the limit.",
                    line, column, _currentFilePath,
                    code: DiagnosticCodes.Infrastructure.TooManyErrors,
                    phase: CompilerPhase.TypeChecking);
                _logger.LogError("Maximum error count reached, stopping type checking", 0, 0);
            }
            if (!ContinueAfterError)
            {
                throw new SemanticAnalysisException("Type checking failed with too many errors");
            }
            return;
        }

        _diagnostics.AddPhaseError(message, CompilerPhase.TypeChecking,
            span, line, column, _currentFilePath, code, _logger, data);
    }

    /// <summary>
    /// Records an informational note from the type-checking phase.
    /// Used for non-error suggestions (e.g., recommending idiomatic forms).
    /// </summary>
    private void AddInfo(string message, int? line = null, int? column = null, string? code = null)
    {
        _diagnostics.AddInfo(message, line, column, _currentFilePath, code, CompilerPhase.TypeChecking);
    }

    /// <summary>
    /// Finds a "did you mean?" suggestion for an undefined identifier from visible symbols.
    /// </summary>
    private string? FindSuggestion(string name)
    {
        return EditDistance.FindClosestMatch(name, _symbolTable.GetVisibleSymbolNames());
    }

    /// <summary>
    /// Builds the machine-readable diagnostic <c>Data</c> payload carrying a "did you mean?"
    /// rename target under the <c>suggestedName</c> key, mirroring
    /// <see cref="Validation.NamingConventionValidator"/>. Returns null when there is no suggestion
    /// so the diagnostic carries no data (LSP quick-fixes consume this key to offer a rename).
    /// </summary>
    private static IReadOnlyDictionary<string, string>? SuggestionData(string? suggestedName)
    {
        return suggestedName != null
            ? new Dictionary<string, string> { ["suggestedName"] = suggestedName }
            : null;
    }

    /// <summary>
    /// Finds a "did you mean?" suggestion for an undefined member from a type's fields and methods,
    /// including inherited members from base classes and interfaces.
    /// </summary>
    private string? FindMemberSuggestion(string memberName, TypeSymbol typeSymbol)
    {
        var memberNames = new HashSet<string>();

        // Collect from the type itself and its base class chain
        var current = typeSymbol;
        while (current != null)
        {
            foreach (var f in current.Fields)
                memberNames.Add(f.Name);
            foreach (var m in current.Methods)
                memberNames.Add(m.Name);
            current = GetBaseType(current);
        }

        // Collect from interfaces
        foreach (var iface in GetInterfaces(typeSymbol))
        {
            foreach (var m in iface.Methods)
                memberNames.Add(m.Name);
        }

        return EditDistance.FindClosestMatch(memberName, memberNames);
    }

    /// <summary>
    /// Finds a "did you mean?" suggestion for an undefined module member.
    /// </summary>
    private string? FindModuleMemberSuggestion(string memberName, ModuleSymbol moduleSymbol)
    {
        return EditDistance.FindClosestMatch(memberName, moduleSymbol.Exports.Keys);
    }

    /// <summary>
    /// Tries to extract a constant integer value from an expression.
    /// Delegates to <see cref="AstHelper.TryGetConstantIntIndex"/>.
    /// </summary>
    private static bool TryGetConstantIntIndex(Expression expr, out int value)
        => AstHelper.TryGetConstantIntIndex(expr, out value);

    /// <summary>
    /// Walks the type hierarchy to find an event with the given name.
    /// </summary>
    private static EventSymbol? FindEventInHierarchy(TypeSymbol type, string eventName)
        => FindEventInHierarchyWithOwner(type, eventName).Event;

    /// <summary>
    /// Like <see cref="FindEventInHierarchy"/>, but also returns the ANCESTOR that declares the
    /// event (its owner), needed to substitute the base clause's pinned arguments for an event
    /// inherited from a source-declared generic base (#1449).
    /// </summary>
    private static (EventSymbol? Event, TypeSymbol? Owner) FindEventInHierarchyWithOwner(
        TypeSymbol type, string eventName)
    {
        var current = type;
        while (current != null)
        {
            var evt = current.Events.FirstOrDefault(e => e.Name == eventName);
            if (evt != null)
                return (evt, current);
            current = current.BaseType;
        }

        // Also check interfaces
        foreach (var ifaceRef in type.Interfaces)
        {
            var iface = ifaceRef.Definition;
            var evt = iface.Events.FirstOrDefault(e => e.Name == eventName);
            if (evt != null)
                return (evt, iface);
        }

        return (null, null);
    }

    /// <summary>
    /// Returns true if the given type (or its base types) declares an event with the given name.
    /// </summary>
    private static bool TypeHasEvent(TypeSymbol type, string eventName)
    {
        return FindEventInHierarchy(type, eventName) != null;
    }

    /// <summary>
    /// Resolves the owner type of an event member access expression.
    /// </summary>
    private TypeSymbol? ResolveEventOwner(MemberAccess memberAccess)
    {
        if (memberAccess.Object is Identifier objId)
        {
            if (objId.Name == PythonNames.Self && _currentClass != null)
                return _currentClass;

            var symbol = _symbolTable.Lookup(objId.Name);
            if (symbol is VariableSymbol varSym)
            {
                var varType = GetVariableType(varSym);
                if (varType is UserDefinedType udt)
                    return udt.Symbol;
            }
            else if (symbol is TypeSymbol ts)
            {
                return ts;
            }
        }
        return null;
    }

    /// <summary>
    /// Attempts to resolve a member access expression to an event symbol.
    /// Returns the EventSymbol if the member access refers to an event, null otherwise.
    /// Handles both self.event_name and obj.event_name patterns.
    /// </summary>
    private EventSymbol? TryResolveEventAccess(MemberAccess memberAccess)
    {
        // Resolve the object type to find the owning type
        TypeSymbol? owningType = null;

        if (memberAccess.Object is Identifier objId)
        {
            if (objId.Name == PythonNames.Self && _currentClass != null)
            {
                owningType = _currentClass;
            }
            else
            {
                var symbol = _symbolTable.Lookup(objId.Name);
                if (symbol is VariableSymbol varSym)
                {
                    var varType = GetVariableType(varSym);
                    if (varType is UserDefinedType udt)
                        owningType = udt.Symbol;
                }
                else if (symbol is TypeSymbol ts)
                {
                    owningType = ts;
                }
            }
        }

        if (owningType == null)
            return null;

        return FindEventInHierarchy(owningType, memberAccess.Member);
    }

    /// <summary>
    /// Registers a scoped type alias in the current symbol table scope.
    /// Used to re-register class-scoped aliases (which are first registered during Pass 1
    /// in a scope that no longer exists) and to register function-scoped aliases.
    /// </summary>
    private void RegisterScopedTypeAlias(TypeAlias typeAlias)
    {
        // Skip if already defined in current scope. This guard is needed because class/struct
        // bodies pre-register aliases before field type resolution (TypeChecker.Definitions.cs),
        // then CheckStatement processes the same TypeAlias node again during the full body pass.
        if (_symbolTable.Lookup(typeAlias.Name, searchParents: false) is TypeAliasSymbol)
            return;

        // Validate that exactly one of Type or FunctionType is set
        if (typeAlias.Type == null && typeAlias.FunctionType == null)
        {
            AddError($"Type alias '{typeAlias.Name}' must have a type",
                typeAlias.LineStart, typeAlias.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidTypeAlias, span: typeAlias.Span);
            return;
        }

        if (typeAlias.Type != null && typeAlias.FunctionType != null)
        {
            AddError($"Type alias '{typeAlias.Name}' cannot have both Type and FunctionType",
                typeAlias.LineStart, typeAlias.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidTypeAlias, span: typeAlias.Span);
            return;
        }

        // Check for redefinition by a non-alias symbol
        var existing = _symbolTable.Lookup(typeAlias.Name, searchParents: false);
        if (existing != null)
        {
            AddError($"Type alias '{typeAlias.Name}' is already defined",
                typeAlias.LineStart, typeAlias.ColumnStart,
                code: DiagnosticCodes.Semantic.DuplicateDefinition, span: typeAlias.Span);
            return;
        }

        _symbolTable.Define(TypeAliasSymbol.CreateFrom(typeAlias));
    }

    /// <summary>
    /// What a SPELLING denotes, as opposed to what its NAME finds. The symbol table is keyed by
    /// name and the lexer strips backticks, so a plain <c>Lookup</c> answers a bare reference with
    /// an escape-declared symbol; this applies the identity rule on top of it.
    /// </summary>
    /// <returns>
    /// The symbol the spelling denotes (null when it denotes nothing), and whether it denotes
    /// nothing BECAUSE an escape-declared symbol holds the name — which the caller reports as the
    /// missing escape rather than as an unknown name.
    /// </returns>
    /// <remarks>
    /// <para>The rule (#1325, #1328, #1281): a BARE spelling never binds an escape-DECLARED symbol.
    /// Without it the lookup succeeded and emission mangled the name — <c>class `zed`</c> followed
    /// by a bare <c>zed()</c> compiled to <c>Zed()</c> against a type emitted as <c>zed</c>, i.e.
    /// CS0103 behind SPY0908, the compiler reporting its own bug for a user error. The converse — an
    /// escaped spelling binding a bare-declared symbol — is quoting and stands, which is what keeps
    /// #713's escaped-import spellings usable.</para>
    ///
    /// <para>When the name IS a builtin's, the bare spelling has somewhere to land, so it means the
    /// BUILTIN and this hands back the registry's own symbol (#1281). The program already behaved
    /// that way at the call seam, but the RECORDED symbol was the user's, so every reader of that
    /// map — hover, go-to-definition, rename, highlight — named something the program does not
    /// call.</para>
    ///
    /// <para>It lives here because more than one seam re-looks-a-spelling-up by name and each one
    /// that forgot the rule produced its own defect: the value-position constructor-reference
    /// classifier fell through to "not a type reference" when an escaped binding held the name, so
    /// <c>h: (str) -&gt; int = int</c> in that scope lost its overload pinning (#1326).</para>
    /// </remarks>
    private (Symbol? Symbol, bool EscapeDeclaredShadow) LookupBySpelling(Identifier id)
    {
        var symbol = _symbolTable.Lookup(id.Name);

        if (symbol == null || id.IsNameBacktickEscaped || !symbol.IsNameBacktickEscaped)
            return (symbol, false);

        Symbol? builtinSymbol = _symbolTable.BuiltinRegistry.GetType(id.Name)
            ?? (Symbol?)_symbolTable.BuiltinRegistry.GetFunction(id.Name);

        if (builtinSymbol != null)
            return (builtinSymbol, false);

        // No builtin to land on: the bare spelling names nothing. Reported by the caller rather
        // than bound, because binding it made emission mangle the name (#1328).
        return _symbolTable.BuiltinRegistry.IsReservedBuiltinName(id.Name)
            ? (symbol, false)
            : (null, true);
    }
}
