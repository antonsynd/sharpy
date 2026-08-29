using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Pattern matching and related helpers
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Extracts the TypeSymbol from a SemanticType, handling UserDefinedType,
    /// NullableType, OptionalType, and GenericType wrappers.
    /// </summary>
    private TypeSymbol? GetTypeSymbolFromSemanticType(SemanticType type)
    {
        return type switch
        {
            UserDefinedType udt => udt.Symbol,
            NullableType nullable => GetTypeSymbolFromSemanticType(nullable.UnderlyingType),
            OptionalType optional => GetTypeSymbolFromSemanticType(optional.UnderlyingType),
            GenericType gt => _symbolTable.Lookup(gt.Name) as TypeSymbol,
            _ => null
        };
    }

    /// <summary>
    /// The #1526 void-scrutinee policy, shared by the statement and expression forms of match:
    /// a bare <c>None</c> scrutinee lowers over a typed null (<c>(object?)None</c>) and the arms
    /// check against <c>object?</c>; any other None-typed scrutinee (a void call) has no value to
    /// match on and is refused with SPY0275. Returns the type the arms should check against —
    /// <c>Unknown</c> after the refusal so non-wildcard arms don't cascade a second TypeMismatch
    /// (the SPY0329 precedent). The lowered type is also written back to the expression-type table
    /// so code generation reads the same scrutinee type the arms were checked against.
    /// </summary>
    private SemanticType ApplyVoidScrutineePolicy(Expression scrutinee, SemanticType scrutineeType)
    {
        if (scrutineeType is not VoidType)
            return scrutineeType;

        if (UnwrapParenthesized(scrutinee) is NoneLiteral)
        {
            _semanticInfo.SetMatchScrutineeLowering(scrutinee,
                new MatchScrutineeLowering(MatchScrutineeLoweringKind.CastToNullableObject));
            var lowered = new NullableType { UnderlyingType = SemanticType.Object };
            _semanticInfo.SetExpressionType(scrutinee, lowered);
            return lowered;
        }

        AddError(
            "Expression of type 'None' has no value and cannot be used as a match scrutinee; " +
            "call it as a statement, then match on None explicitly",
            scrutinee.LineStart, scrutinee.ColumnStart,
            code: DiagnosticCodes.Semantic.VoidMatchScrutinee,
            span: scrutinee.Span);
        return SemanticType.Unknown;
    }

    private void CheckMatch(MatchStatement matchStmt)
    {
        // Resolve the subject against the facts in effect at the dispatch point, exactly as CheckIf
        // does for a condition (#1299). The CFG never tracked the match statement, so the subject
        // read the pre-branch fact set and `if isinstance(o, Box[int]): match o:` saw a bare object.
        // The `??` is load-bearing: with no flow analysis (module body) the current facts stand.
        // CheckStatement's finally restores _currentFacts.
        _currentFacts = _narrowingFlow?.FactsBeforeBranch(matchStmt.Scrutinee) ?? _currentFacts;

        // Mark the subject so its own read suppresses a Cast lowering (#1370) — the narrowed type is
        // still recorded, so the arms keep filling from it. Unwrapped because `match (x):` puts the
        // read, and therefore the lowering, on the inner node (#1349).
        SemanticType scrutineeType;
        using (ScopedValue.Push(ref _matchSubjectOperand, UnwrapParenthesized(matchStmt.Scrutinee)))
            scrutineeType = CheckExpression(matchStmt.Scrutinee);

        scrutineeType = ApplyVoidScrutineePolicy(matchStmt.Scrutinee, scrutineeType);

        foreach (var matchCase in matchStmt.Cases)
        {
            // Match-case narrowing intentionally stays on the _narrowingContext scope stack rather than
            // the CFG dataflow facts (#1042): the CFG builder connects match cases with plain edges and
            // carries no pattern/subject on them, so pattern-derived narrowings (bound via CheckPattern)
            // cannot be modelled as facts without extending the builder. This is the one narrowing form
            // that did not migrate to NarrowingFlowAnalysis; the fact-based path and this path coexist.
            using (_narrowingContext.EnterScope())
            {
                _symbolTable.EnterScope("match-case");
                _controlFlowDepth++;

                CheckPattern(matchCase.Pattern, scrutineeType);

                if (matchCase.Guard != null)
                {
                    var guardType = CheckExpression(matchCase.Guard);
                    var (mcGuardTestable, mcGuardLowering) = ClassifyTruthiness(guardType);
                    if (!mcGuardTestable)
                    {
                        AddError("Guard condition must be a boolean expression",
                            matchCase.Guard.LineStart, matchCase.Guard.ColumnStart,
                            code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                            span: matchCase.Guard.Span);
                    }
                    else
                    {
                        _semanticInfo.SetTruthinessLowering(matchCase.Guard, mcGuardLowering);
                    }
                }

                foreach (var stmt in matchCase.Body)
                    CheckStatement(stmt);

                _controlFlowDepth--;
                _symbolTable.ExitScope();
            }
        }
    }

    private void CheckPattern(Pattern pattern, SemanticType scrutineeType)
    {
        switch (pattern)
        {
            case WildcardPattern:
                break;

            case BindingPattern binding:
                {
                    // #1562: bare name matches a union variant of the scrutinee before
                    // it captures. Design Decision 5: synthetic unions (Optional/Result)
                    // are excluded in v1.
                    if (scrutineeType is not OptionalType and not ResultType)
                    {
                        var unionCaseSymbol = TryResolveUnionCaseFromPattern(
                            binding.Name.Name, scrutineeType);
                        if (unionCaseSymbol != null)
                        {
                            _semanticInfo.SetPatternUnionCase(binding, unionCaseSymbol);

                            // Design Decision 4: variant wins, but warn if a constant is shadowed
                            var shadowed = _symbolTable.Lookup(binding.Name.Name, searchParents: true) as VariableSymbol;
                            if (shadowed is { IsConstant: true })
                            {
                                _diagnostics.AddWarning(
                                    $"Pattern '{binding.Name.Name}' resolves as union variant of the scrutinee type, shadowing constant '{shadowed.Name}'",
                                    binding,
                                    code: DiagnosticCodes.Validation.VariantPatternShadowsConstant);
                            }
                            break;
                        }
                    }

                    // RFC 3535: Check if the identifier resolves to a module-level
                    // constant (Final-annotated or IsConstant) before treating as capture.
                    var existingSymbol = _symbolTable.Lookup(binding.Name.Name, searchParents: true) as VariableSymbol;
                    if (existingSymbol is { IsConstant: true })
                    {
                        _diagnostics.AddWarning(
                            $"Pattern '{binding.Name.Name}' matches constant value, not a capture binding; use a different name to capture",
                            binding,
                            code: DiagnosticCodes.Validation.ConstantPatternShadow);

                        _semanticInfo.SetPatternConstantSymbol(binding, existingSymbol);
                        _semanticInfo.SetIdentifierSymbol(binding.Name, existingSymbol);

                        var constType = existingSymbol.Type;
                        if (constType != SemanticType.Unknown && !IsAssignable(scrutineeType, constType))
                        {
                            _diagnostics.AddError(
                                $"Constant pattern type '{constType.GetDisplayName()}' is not compatible with match subject type '{scrutineeType.GetDisplayName()}'",
                                binding,
                                code: DiagnosticCodes.Semantic.TypeMismatch);
                        }
                        break;
                    }

                    var newSymbol = new VariableSymbol
                    {
                        Name = binding.Name.Name,
                        Kind = SymbolKind.Variable,
                        Type = scrutineeType,
                        IsConstant = false,
                        DeclarationLine = binding.LineStart,
                        DeclarationColumn = binding.ColumnStart,
                        NameDeclarationLine = binding.Name.LineStart,
                        NameDeclarationColumn = binding.Name.ColumnStart,
                        AccessLevel = AccessLevel.Public
                    };

                    _symbolTable.Define(newSymbol);
                    SemanticBinding.SetVariableType(newSymbol, scrutineeType);
                    _semanticInfo.SetIdentifierSymbol(binding.Name, newSymbol);
                    _semanticInfo.SetTargetBinding(binding, new TargetBinding(TargetBindingKind.Declares));
                    break;
                }

            case LiteralPattern literal:
                {
                    // Handle None() pattern when matching against Optional[T]
                    if (literal.Literal is FunctionCall { Function: NoneLiteral } noneCall
                        && noneCall.Arguments.Length == 0
                        && scrutineeType is OptionalType)
                    {
                        // Record synthetic None union case for exhaustiveness checking
                        var synth = GetSyntheticOptionalUnion();
                        var noneCase = synth.UnionCases.First(c => c.Name == "None");
                        _semanticInfo.SetPatternUnionCase(literal, noneCase);
                        break;
                    }

                    // Handle bare None literal when matching against Optional[T]
                    if (literal.Literal is NoneLiteral && scrutineeType is OptionalType)
                    {
                        var synth = GetSyntheticOptionalUnion();
                        var noneCase = synth.UnionCases.First(c => c.Name == "None");
                        _semanticInfo.SetPatternUnionCase(literal, noneCase);
                        break;
                    }

                    var litType = CheckExpression(literal.Literal);
                    if (!IsAssignable(litType, scrutineeType) && !IsAssignable(scrutineeType, litType))
                    {
                        AddError(
                            $"Pattern type '{litType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                            literal.LineStart, literal.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: literal.Span);
                    }
                    if (litType is not UnknownType)
                        _semanticInfo.SetPatternType(literal, litType);
                    break;
                }

            case TuplePattern tuplePattern:
                {
                    if (scrutineeType is TupleType tupleType)
                    {
                        if (tuplePattern.Elements.Length != tupleType.ElementTypes.Count)
                        {
                            AddError(
                                $"Tuple pattern has {tuplePattern.Elements.Length} elements but scrutinee has {tupleType.ElementTypes.Count}",
                                tuplePattern.LineStart, tuplePattern.ColumnStart,
                                code: DiagnosticCodes.Semantic.TuplePatternLengthMismatch,
                                span: tuplePattern.Span);
                        }
                        else
                        {
                            for (int i = 0; i < tuplePattern.Elements.Length; i++)
                                CheckPattern(tuplePattern.Elements[i], tupleType.ElementTypes[i]);
                        }
                    }
                    else
                    {
                        AddError(
                            $"Cannot destructure non-tuple type '{scrutineeType.GetDisplayName()}' with tuple pattern",
                            tuplePattern.LineStart, tuplePattern.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: tuplePattern.Span);
                    }
                    break;
                }

            case TypePattern typePattern:
                CheckTypePattern(typePattern, scrutineeType);
                break;

            case RelationalPattern relational:
                {
                    var valueType = CheckExpression(relational.Value);
                    if (!TypeUtils.IsNumericOrUnknown(scrutineeType))
                    {
                        AddError(
                            $"Relational patterns require a numeric scrutinee type, got '{scrutineeType.GetDisplayName()}'",
                            relational.LineStart, relational.ColumnStart,
                            code: DiagnosticCodes.Semantic.RelationalPatternTypeMismatch,
                            span: relational.Span);
                    }
                    if (!IsAssignable(valueType, scrutineeType) && !IsAssignable(scrutineeType, valueType)
                        && valueType is not UnknownType)
                    {
                        AddError(
                            $"Pattern value type '{valueType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                            relational.LineStart, relational.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: relational.Span);
                    }
                    break;
                }

            case OrPattern orPattern:
                {
                    bool hasMemberAccess = orPattern.Alternatives.Any(a => a is MemberAccessPattern);
                    bool allHaveAs = orPattern.Alternatives.All(a =>
                        a is AsPattern || (a is GuardPattern gp2 && gp2.Inner is AsPattern));

                    if (allHaveAs)
                    {
                        // `(A() as v) | (B() as v)`: one name bound under every alternative. Its
                        // type is what EVERY alternative guarantees — the alternatives' common
                        // ancestor — not the first alternative's own type: binding `v` as `float`
                        // for `(float() as v) | (list() as v)` while the emitted `var v` is
                        // `object` gave CS1503 behind SPY0908 (#1663).
                        var alternativeTypes = new List<SemanticType>();
                        foreach (var alt in orPattern.Alternatives)
                        {
                            var effectiveAlt = alt is GuardPattern gp ? gp.Inner : alt;
                            var asAlt = (AsPattern)effectiveAlt;
                            CheckPattern(asAlt.Inner, scrutineeType);
                            alternativeTypes.Add(
                                _semanticInfo.GetPatternType(asAlt.Inner) ?? scrutineeType);
                        }
                        var firstAs = (AsPattern)(orPattern.Alternatives[0] is GuardPattern gp3
                            ? gp3.Inner : orPattern.Alternatives[0]);
                        var joinedType = alternativeTypes.All(t => t.Equals(alternativeTypes[0]))
                            ? alternativeTypes[0]
                            : FindLeastCommonAncestor(alternativeTypes);
                        BindAsPatternCapture(firstAs, scrutineeType, capturedTypeOverride: joinedType);
                        break;
                    }

                    foreach (var alt in orPattern.Alternatives)
                    {
                        var effectiveAlt = alt is GuardPattern gp ? gp.Inner : alt;
                        if (effectiveAlt is BindingPattern bindingInOr)
                        {
                            bool isUnionVariant = scrutineeType is not OptionalType and not ResultType
                                && TryResolveUnionCaseFromPattern(bindingInOr.Name.Name, scrutineeType) != null;
                            if (!isUnionVariant)
                            {
                                AddError(
                                    "Binding patterns are not allowed inside or-patterns",
                                    effectiveAlt.LineStart, effectiveAlt.ColumnStart,
                                    code: DiagnosticCodes.Semantic.BindingInOrPattern,
                                    span: effectiveAlt.Span);
                            }
                            else
                            {
                                CheckPattern(alt, scrutineeType);
                            }
                        }
                        else if (effectiveAlt is AsPattern asInOr)
                        {
                            AddError(
                                "Binding patterns are not allowed inside or-patterns",
                                asInOr.LineStart, asInOr.ColumnStart,
                                code: DiagnosticCodes.Semantic.BindingInOrPattern,
                                span: asInOr.Span);
                        }
                        else if (hasMemberAccess && effectiveAlt is not MemberAccessPattern && effectiveAlt is not LiteralPattern && effectiveAlt is not WildcardPattern)
                        {
                            AddError(
                                "Only literal, member access, and wildcard patterns can be combined with member access patterns in or-patterns",
                                effectiveAlt.LineStart, effectiveAlt.ColumnStart,
                                code: DiagnosticCodes.Semantic.UnsupportedPatternInMemberAccessOr,
                                span: effectiveAlt.Span);
                        }
                        else
                        {
                            CheckPattern(alt, scrutineeType);
                        }
                    }
                    break;
                }

            case GuardPattern guardPattern:
                {
                    CheckPattern(guardPattern.Inner, scrutineeType);
                    var guardType = CheckExpression(guardPattern.Guard);
                    if (guardType != SemanticType.Bool && guardType != SemanticType.Unknown)
                    {
                        AddError(
                            $"Guard expression must be bool, got '{guardType.GetDisplayName()}'",
                            guardPattern.Guard.LineStart, guardPattern.Guard.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: guardPattern.Guard.Span);
                    }
                    break;
                }

            case PropertyPattern propertyPattern:
                CheckPropertyPattern(propertyPattern, scrutineeType);
                break;

            case PositionalPattern positionalPattern:
                CheckPositionalPattern(positionalPattern, scrutineeType);
                break;

            case MemberAccessPattern memberAccess:
                CheckMemberAccessPattern(memberAccess, scrutineeType);
                break;

            case ListPattern listPattern:
                CheckListPattern(listPattern, scrutineeType);
                break;

            case AsPattern asPattern:
                CheckAsPattern(asPattern, scrutineeType);
                break;

            case AndPattern andPattern:
                CheckAndPattern(andPattern, scrutineeType);
                break;

            case StarPattern:
                // A star capture is only meaningful inside a list pattern (handled by
                // CheckListPattern). Reaching it standalone means a malformed pattern.
                AddError(
                    "A '*' capture may only appear inside a list pattern",
                    pattern.LineStart, pattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnsupportedFeature);
                break;

            default:
                AddError(
                    $"Unsupported pattern type '{pattern.GetType().Name}'. This pattern is not yet implemented.",
                    pattern.LineStart, pattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnsupportedFeature);
                break;
        }
    }

    /// <summary>
    /// Type-checks a list (sequence) pattern: requires the scrutinee to be a sequence
    /// (<c>list[T]</c> / <c>array[T]</c>), checks each element pattern against the element type,
    /// and binds a <c>*rest</c> capture as <c>list[T]</c> (#991).
    /// </summary>
    private void CheckListPattern(ListPattern listPattern, SemanticType scrutineeType)
    {
        SemanticType? elementType = scrutineeType switch
        {
            GenericType { Name: BuiltinNames.List } g when g.TypeArguments.Count > 0 => g.TypeArguments[0],
            GenericType { Name: BuiltinNames.Array } g when g.TypeArguments.Count > 0 => g.TypeArguments[0],
            _ => null
        };

        if (elementType == null)
        {
            // Allow Unknown/Object scrutinees through (error recovery), but reject concrete non-sequences.
            if (scrutineeType != SemanticType.Unknown && scrutineeType != BuiltinType.Object)
            {
                AddError(
                    $"Cannot match non-sequence type '{scrutineeType.GetDisplayName()}' with a list pattern",
                    listPattern.LineStart, listPattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: listPattern.Span);
            }
            elementType = SemanticType.Unknown;
        }

        var restListType = new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { elementType }
        };

        foreach (var element in listPattern.Elements)
        {
            if (element is StarPattern star)
            {
                // *rest captures the remaining elements as list[T]; *_ discards.
                if (star.Capture != null)
                    CheckPattern(star.Capture, restListType);
            }
            else
            {
                CheckPattern(element, elementType);
            }
        }
    }

    /// <summary>
    /// Type-checks an and-pattern: both sub-patterns must match the same scrutinee type, and a
    /// capture name may not be bound on both sides (#991).
    /// </summary>
    private void CheckAndPattern(AndPattern andPattern, SemanticType scrutineeType)
    {
        var leftNames = CollectPatternBindingNames(andPattern.Left);
        var rightNames = CollectPatternBindingNames(andPattern.Right);
        foreach (var name in leftNames)
        {
            if (rightNames.Contains(name))
            {
                AddError(
                    $"Capture name '{name}' is bound on both sides of an and-pattern",
                    andPattern.LineStart, andPattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.DuplicateCaptureInPattern,
                    span: andPattern.Span);
            }
        }

        CheckPattern(andPattern.Left, scrutineeType);
        CheckPattern(andPattern.Right, scrutineeType);
    }

    private void CheckAsPattern(AsPattern asPattern, SemanticType scrutineeType)
    {
        CheckPattern(asPattern.Inner, scrutineeType);
        BindAsPatternCapture(asPattern, scrutineeType);
        var innerType = _semanticInfo.GetPatternType(asPattern.Inner);
        if (innerType != null)
            _semanticInfo.SetPatternType(asPattern, innerType);
    }

    private void BindAsPatternCapture(
        AsPattern asPattern, SemanticType scrutineeType, SemanticType? capturedTypeOverride = null)
    {
        var capturedType = capturedTypeOverride ?? scrutineeType;

        if (capturedTypeOverride == null && asPattern.Inner is TypePattern typeInner)
        {
            var patternType = _semanticInfo.GetPatternType(typeInner);
            if (patternType != null)
                capturedType = patternType;
            else
            {
                var classified = ClassifyTypeTestAnnotation(
                    typeInner.Type, typeInner, scrutineeType, "match pattern",
                    CollectionErasure.Allowed,
                    openGenericRemedyOverride: BuildPatternOpenGenericRemedy(typeInner.Type));
                if (classified != null)
                    capturedType = classified;
            }
        }

        var newSymbol = new VariableSymbol
        {
            Name = asPattern.Name.Name,
            Kind = SymbolKind.Variable,
            Type = capturedType,
            IsConstant = false,
            DeclarationLine = asPattern.Name.LineStart,
            DeclarationColumn = asPattern.Name.ColumnStart,
            NameDeclarationLine = asPattern.Name.LineStart,
            NameDeclarationColumn = asPattern.Name.ColumnStart,
            AccessLevel = AccessLevel.Public
        };

        _symbolTable.Define(newSymbol);
        SemanticBinding.SetVariableType(newSymbol, capturedType);
        _semanticInfo.SetIdentifierSymbol(asPattern.Name, newSymbol);
        _semanticInfo.SetTargetBinding(asPattern, new TargetBinding(TargetBindingKind.Declares));
    }

    /// <summary>
    /// Collects the capture (binding) names introduced by a pattern, recursing into composite
    /// patterns. Used to detect duplicate captures across the two sides of an and-pattern.
    /// </summary>
    private static HashSet<string> CollectPatternBindingNames(Pattern pattern)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        void Walk(Pattern? p)
        {
            switch (p)
            {
                case null:
                    return;
                case BindingPattern b:
                    names.Add(b.Name.Name);
                    break;
                case TypePattern:
                    break;
                case AsPattern asp:
                    names.Add(asp.Name.Name);
                    Walk(asp.Inner);
                    break;
                case TuplePattern t:
                    foreach (var e in t.Elements)
                        Walk(e);
                    break;
                case PositionalPattern pp:
                    foreach (var e in pp.Elements)
                        Walk(e);
                    break;
                case PropertyPattern prop:
                    foreach (var f in prop.Fields)
                        Walk(f.Pattern);
                    break;
                case UnionCasePattern uc:
                    foreach (var f in uc.FieldPatterns)
                        Walk(f);
                    break;
                case ListPattern l:
                    foreach (var e in l.Elements)
                        Walk(e);
                    break;
                case StarPattern s:
                    Walk(s.Capture);
                    break;
                case AndPattern a:
                    Walk(a.Left);
                    Walk(a.Right);
                    break;
                case OrPattern o:
                    foreach (var alt in o.Alternatives)
                        Walk(alt);
                    break;
                case GuardPattern g:
                    Walk(g.Inner);
                    break;
            }
        }
        Walk(pattern);
        return names;
    }

    /// <summary>
    /// Check a type pattern: resolve the type, handle union cases, validate compatibility,
    /// and register any binding variable. Routes through the isinstance classifier so
    /// <see cref="Semantic.SemanticInfo.SetTypeTestLowering"/> is recorded for every path
    /// and the emitter reads that fact instead of re-resolving (#1670).
    /// </summary>
    private void CheckTypePattern(TypePattern typePattern, SemanticType scrutineeType)
    {
        // #1562: try union case probe FIRST, mirroring CheckPositionalPattern.
        var earlyUnionCase = TryResolveUnionCaseFromPattern(
            typePattern.Type.Name, scrutineeType);
        if (earlyUnionCase != null)
        {
            _semanticInfo.SetPatternUnionCase(typePattern, earlyUnionCase);
            var earlyResolved = new UserDefinedType { Name = earlyUnionCase.Name, Symbol = earlyUnionCase };
            _semanticInfo.SetPatternType(typePattern, earlyResolved);
            _semanticInfo.SetTypeTestLowering(typePattern,
                new TypeTestLowering(TypeTestLoweringKind.ClosedType, earlyResolved));
            BindTypePatternCapture(typePattern, earlyResolved);
            return;
        }

        // Array scrutinee interop: bare `case list()` against `array[T]` resolves to the
        // array itself so indexing lowers to ArrayHelpers.GetItem.
        if (typePattern.Type.TypeArguments.Length == 0
            && typePattern.Type.Name == BuiltinNames.List
            && scrutineeType is GenericType { Name: BuiltinNames.Array } arrayScrutinee)
        {
            _semanticInfo.SetPatternType(typePattern, arrayScrutinee);
            _semanticInfo.SetTypeTestLowering(typePattern,
                new TypeTestLowering(TypeTestLoweringKind.ClosedType, arrayScrutinee));
            BindTypePatternCapture(typePattern, arrayScrutinee);
            return;
        }

        // Erasable collections (list/dict/set): try fill-from-subject FIRST — patterns need
        // the closed type for capture typing, not the erased interface. The isinstance classifier
        // erases unconditionally, which is correct for isinstance but wrong for patterns when
        // the scrutinee provides type arguments (#1299 defect 1).
        if (typePattern.Type.TypeArguments.Length == 0)
        {
            var fillSymbol = _symbolTable.Lookup(typePattern.Type.Name) as TypeSymbol;
            if (fillSymbol is { IsGeneric: true }
                && BuiltinNames.IsErasableCollection(fillSymbol.Name)
                && FillTypeArgumentsFromSubject(fillSymbol, scrutineeType) is { } filledCollection)
            {
                _semanticInfo.SetPatternType(typePattern, filledCollection);
                _semanticInfo.SetTypeTestLowering(typePattern,
                    new TypeTestLowering(TypeTestLoweringKind.ClosedType, filledCollection));
                if (scrutineeType is not UnknownType && IsAssignable(scrutineeType, filledCollection))
                    _semanticInfo.SetPatternTotality(typePattern, true);
                BindTypePatternCapture(typePattern, filledCollection);
                return;
            }
        }

        // The isinstance classifier handles the remaining cases: non-generic types, erasable
        // collections on an object subject, user generics with fill-from-subject, and refusals.
        var resolvedType = ClassifyTypeTestAnnotation(
            typePattern.Type, typePattern, scrutineeType, "match pattern",
            CollectionErasure.Allowed,
            openGenericRemedyOverride: BuildPatternOpenGenericRemedy(typePattern.Type));

        if (resolvedType == null)
        {
            // Classifier returns null for unknown types (no diagnostic) and open-generic
            // refusals (SPY0345 reported). Distinguish by symbol lookup for SPY0202.
            var knownSymbol = _symbolTable.Lookup(typePattern.Type.Name) as TypeSymbol;
            if (knownSymbol == null && !typePattern.Type.IsNameBacktickEscaped)
                knownSymbol = _typeResolver.LookupModuleQualifiedType(typePattern.Type.Name) as TypeSymbol;
            if (knownSymbol == null)
            {
                AddError(
                    $"Unknown type '{typePattern.Type.Name}' in type pattern",
                    typePattern.LineStart, typePattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedType,
                    span: typePattern.Span);
            }
            return;
        }

        // #1510: refuse bare payload TYPE pattern over tagged-union scrutinee
        if (scrutineeType is OptionalType payloadOptional
            && IsAssignable(resolvedType, payloadOptional.UnderlyingType))
        {
            AddError(
                $"An Optional scrutinee cannot be matched with the payload type pattern " +
                $"'{resolvedType.GetDisplayName()}'. Match through the constructor cases instead: " +
                "'case Some(v):' for a present value and 'case None():' for absence " +
                "(or narrow first with 'if x is not None:').",
                typePattern.LineStart, typePattern.ColumnStart,
                code: DiagnosticCodes.Validation.PayloadTypePatternOverUnion,
                span: typePattern.Span);
            return;
        }

        if (scrutineeType is ResultType payloadResult
            && (IsAssignable(resolvedType, payloadResult.OkType)
                || IsAssignable(resolvedType, payloadResult.ErrorType)))
        {
            AddError(
                $"A Result scrutinee cannot be matched with the payload type pattern " +
                $"'{resolvedType.GetDisplayName()}'. Match through the constructor cases instead: " +
                "'case Ok(v):' for success and 'case Err(e):' for failure.",
                typePattern.LineStart, typePattern.ColumnStart,
                code: DiagnosticCodes.Validation.PayloadTypePatternOverUnion,
                span: typePattern.Span);
            return;
        }

        if (scrutineeType is not UnknownType
            && !IsAssignable(resolvedType, scrutineeType)
            && !IsAssignable(scrutineeType, resolvedType))
        {
            AddError(
                $"Type pattern '{typePattern.Type.Name}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                typePattern.LineStart, typePattern.ColumnStart,
                code: DiagnosticCodes.Semantic.TypePatternIncompatible,
                span: typePattern.Span);
        }
        if (_semanticInfo.GetPatternType(typePattern) == null)
            _semanticInfo.SetPatternType(typePattern, resolvedType);
        if (scrutineeType is not UnknownType
            && IsAssignable(scrutineeType, resolvedType))
        {
            _semanticInfo.SetPatternTotality(typePattern, true);
        }
        BindTypePatternCapture(typePattern, resolvedType);
    }

    /// <summary>
    /// Defines the <c>as</c> capture of a type pattern (<c>case str() as s:</c>) at
    /// <paramref name="capturedType"/>, the pattern's own resolved type.
    /// </summary>
    private void BindTypePatternCapture(TypePattern typePattern, SemanticType capturedType)
    {
        if (typePattern.BindingName == null)
            return;

        var newSymbol = new VariableSymbol
        {
            Name = typePattern.BindingName.Name,
            Kind = SymbolKind.Variable,
            Type = capturedType,
            IsConstant = false,
            DeclarationLine = typePattern.BindingName.LineStart,
            DeclarationColumn = typePattern.BindingName.ColumnStart,
            NameDeclarationLine = typePattern.BindingName.LineStart,
            NameDeclarationColumn = typePattern.BindingName.ColumnStart,
            AccessLevel = AccessLevel.Public
        };
        _symbolTable.Define(newSymbol);
        SemanticBinding.SetVariableType(newSymbol, capturedType);
        _semanticInfo.SetIdentifierSymbol(typePattern.BindingName, newSymbol);
    }

    /// <summary>
    /// Builds the open-generic remedy text for match patterns. Patterns cannot name type
    /// arguments (SPY0125), so the remedy steers to an isinstance guard or a non-generic base.
    /// </summary>
    private static string BuildPatternOpenGenericRemedy(TypeAnnotation annotation)
    {
        var name = annotation.Name;
        return "Match on a value whose static type supplies them — guard the match with "
            + $"`if isinstance(x, {name}[...]):`, or bind it first with "
            + $"`v: {name}[...] = x as! {name}[...]` — or match against a "
            + "non-generic base type. A pattern cannot name type arguments itself.";
    }

    /// <summary>
    /// Check a property pattern: resolve the type, then validate each field sub-pattern.
    /// </summary>
    private void CheckPropertyPattern(PropertyPattern propertyPattern, SemanticType scrutineeType)
    {
        TypeSymbol? typeSymbol = null;
        if (propertyPattern.Type != null)
        {
            // #1562: try union case probe FIRST, mirroring CheckPositionalPattern.
            var earlyUnionCase = TryResolveUnionCaseFromPattern(
                propertyPattern.Type.Name, scrutineeType);
            if (earlyUnionCase != null)
            {
                typeSymbol = earlyUnionCase;
                _semanticInfo.SetPatternUnionCase(propertyPattern, earlyUnionCase);
            }
            else
            {
                var classifiedType = ClassifyTypeTestAnnotation(
                    propertyPattern.Type, propertyPattern, scrutineeType, "match pattern",
                    CollectionErasure.Allowed,
                    openGenericRemedyOverride: BuildPatternOpenGenericRemedy(propertyPattern.Type));
                if (classifiedType == null)
                {
                    var knownSymbol = _symbolTable.Lookup(propertyPattern.Type.Name) as TypeSymbol;
                    if (knownSymbol == null && !propertyPattern.Type.IsNameBacktickEscaped)
                        knownSymbol = _typeResolver.LookupModuleQualifiedType(propertyPattern.Type.Name) as TypeSymbol;
                    if (knownSymbol == null)
                    {
                        AddError(
                            $"Unknown type '{propertyPattern.Type.Name}' in property pattern",
                            propertyPattern.LineStart, propertyPattern.ColumnStart,
                            code: DiagnosticCodes.Semantic.UndefinedType,
                            span: propertyPattern.Span);
                    }
                    return;
                }

                _semanticInfo.SetPatternType(propertyPattern, classifiedType);

                typeSymbol = classifiedType switch
                {
                    UserDefinedType udt => udt.Symbol,
                    GenericType { GenericDefinition: { } filledDefinition } => filledDefinition,
                    _ => null
                };
            }
        }

        foreach (var field in propertyPattern.Fields)
        {
            if (typeSymbol != null)
            {
                var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == field.Name);
                if (fieldSymbol == null)
                {
                    AddError(
                        $"Type '{typeSymbol.Name}' has no field '{field.Name}'",
                        field.LineStart, field.ColumnStart,
                        code: DiagnosticCodes.Semantic.PropertyPatternUnknownField,
                        span: field.Span);
                }
                else
                {
                    CheckPattern(field.Pattern, fieldSymbol.Type);
                }
            }
            else
            {
                CheckPattern(field.Pattern, scrutineeType);
            }
        }
    }

    /// <summary>
    /// Check a positional pattern: resolve the type (including union cases),
    /// validate deconstruction support, and check element sub-patterns.
    /// </summary>
    private void CheckPositionalPattern(PositionalPattern positionalPattern, SemanticType scrutineeType)
    {
        if (positionalPattern.Type != null
            && SelfMatchingBuiltins.IsSelfMatching(positionalPattern.Type.Name))
        {
            if (positionalPattern.Elements.Length != 1)
            {
                AddError(
                    $"Builtin type '{positionalPattern.Type.Name}' accepts exactly 1 positional sub-pattern ({positionalPattern.Elements.Length} given)",
                    positionalPattern.LineStart, positionalPattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.PositionalPatternCountMismatch,
                    span: positionalPattern.Span);
                return;
            }
            // PEP 634: the single sub-pattern matches the WHOLE subject, which at that point
            // is known to be an instance of the builtin — so it binds with the builtin's type,
            // exactly as `case int() as n:` does (#1653).
            var selfMatchedType = ClassifyTypeTestAnnotation(
                positionalPattern.Type, positionalPattern, scrutineeType, "match pattern",
                CollectionErasure.Allowed,
                openGenericRemedyOverride: BuildPatternOpenGenericRemedy(positionalPattern.Type))
                ?? scrutineeType;
            CheckPattern(positionalPattern.Elements[0], selfMatchedType);
            _semanticInfo.SetPatternType(positionalPattern, selfMatchedType);
            if (selfMatchedType is not UnknownType && scrutineeType is not UnknownType
                && IsAssignable(scrutineeType, selfMatchedType))
            {
                _semanticInfo.SetPatternTotality(positionalPattern, true);
            }
            return;
        }

        TypeSymbol? typeSymbol = null;
        if (positionalPattern.Type != null)
        {
            // Try to resolve as a union case first when scrutinee is a union type
            var unionCaseSymbol = TryResolveUnionCaseFromPattern(
                positionalPattern.Type.Name, scrutineeType);

            if (unionCaseSymbol != null)
            {
                typeSymbol = unionCaseSymbol;
                _semanticInfo.SetPatternUnionCase(positionalPattern, unionCaseSymbol);
            }
            else
            {
                var classifiedType = ClassifyTypeTestAnnotation(
                    positionalPattern.Type, positionalPattern, scrutineeType, "match pattern",
                    CollectionErasure.Allowed,
                    openGenericRemedyOverride: BuildPatternOpenGenericRemedy(positionalPattern.Type));
                if (classifiedType == null)
                {
                    var knownSymbol = _symbolTable.Lookup(positionalPattern.Type.Name) as TypeSymbol;
                    if (knownSymbol == null && !positionalPattern.Type.IsNameBacktickEscaped)
                        knownSymbol = _typeResolver.LookupModuleQualifiedType(positionalPattern.Type.Name) as TypeSymbol;
                    if (knownSymbol == null)
                    {
                        AddError(
                            $"Unknown type '{positionalPattern.Type.Name}' in positional pattern",
                            positionalPattern.LineStart, positionalPattern.ColumnStart,
                            code: DiagnosticCodes.Semantic.UndefinedType,
                            span: positionalPattern.Span);
                    }
                    return;
                }

                _semanticInfo.SetPatternType(positionalPattern, classifiedType);

                typeSymbol = classifiedType switch
                {
                    UserDefinedType udt => udt.Symbol,
                    GenericType { GenericDefinition: { } filledDefinition } => filledDefinition,
                    _ => null
                };

                // For non-union types, check if positional deconstruction is supported
                if (typeSymbol != null
                    && typeSymbol.BaseType?.TypeKind != TypeKind.Union
                    && typeSymbol.TypeKind != TypeKind.Union)
                {
                    bool hasDeconstruct = typeSymbol.Methods.Any(m => m.Name == "Deconstruct");
                    bool hasMatchingFields = typeSymbol.Fields.Count == positionalPattern.Elements.Length;
                    if (!hasDeconstruct && !hasMatchingFields)
                    {
                        AddError(
                            $"Type '{typeSymbol.Name}' does not support positional deconstruction (no Deconstruct method and field count {typeSymbol.Fields.Count} does not match pattern element count {positionalPattern.Elements.Length})",
                            positionalPattern.LineStart, positionalPattern.ColumnStart,
                            code: DiagnosticCodes.Semantic.PositionalPatternNoDeconstruct,
                            span: positionalPattern.Span);
                    }
                }
            }
        }

        if (typeSymbol != null)
        {
            // Get field types, substituting type parameters for generic unions
            var fieldTypes = GetUnionCaseFieldTypes(typeSymbol, scrutineeType);

            if (positionalPattern.Elements.Length != fieldTypes.Count)
            {
                AddError(
                    $"Positional pattern has {positionalPattern.Elements.Length} elements but type '{typeSymbol.Name}' has {fieldTypes.Count} fields",
                    positionalPattern.LineStart, positionalPattern.ColumnStart,
                    code: typeSymbol.BaseType is { TypeKind: TypeKind.Union }
                        ? DiagnosticCodes.Semantic.UnionCaseFieldMismatch
                        : DiagnosticCodes.Semantic.PositionalPatternCountMismatch,
                    span: positionalPattern.Span);
            }
            else
            {
                for (int i = 0; i < positionalPattern.Elements.Length; i++)
                {
                    CheckPattern(positionalPattern.Elements[i], fieldTypes[i]);
                }
            }
        }
        else
        {
            foreach (var element in positionalPattern.Elements)
            {
                CheckPattern(element, scrutineeType);
            }
        }
    }

    /// <summary>
    /// Check a member access pattern: resolve dotted paths for enum members,
    /// union cases, and field/property access chains.
    /// </summary>
    private void CheckMemberAccessPattern(MemberAccessPattern memberAccess, SemanticType scrutineeType)
    {
        // Resolve the dotted path. Consume leading MODULE segments first (so
        // `lib.Color.RED` works like `Color.RED` after `from lib import Color`), then
        // read the remainder as <Type>.<Member> (#1524).
        TypeSymbol? typeSymbol = null;
        int typeIndex = 0;

        var firstSymbol = _symbolTable.Lookup(memberAccess.Parts[0]);
        if (firstSymbol is TypeSymbol ts)
        {
            typeSymbol = ts;
            typeIndex = 0;
        }
        else if (firstSymbol is ModuleSymbol moduleSymbol)
        {
            // Walk module segments until we find a type.
            var current = moduleSymbol;
            for (int i = 1; i < memberAccess.Parts.Length; i++)
            {
                if (current.Exports.TryGetValue(memberAccess.Parts[i], out var exported))
                {
                    if (exported is TypeSymbol exportedType)
                    {
                        typeSymbol = exportedType;
                        typeIndex = i;
                        break;
                    }
                    if (exported is ModuleSymbol nestedModule)
                    {
                        current = nestedModule;
                        continue;
                    }
                }
                break;
            }
        }

        if (typeSymbol == null)
        {
            AddError(
                $"Undefined type '{memberAccess.Parts[0]}' in pattern",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType,
                span: memberAccess.Span);
            return;
        }

        _semanticInfo.SetPatternMemberAccessResolution(memberAccess, typeSymbol, typeIndex);

        // The member name is the part AFTER the type.
        int memberStartIndex = typeIndex + 1;

        // Check if this is a union case pattern (e.g., Option.None, Result.Ok)
        if (typeSymbol.TypeKind == TypeKind.Union && memberAccess.Parts.Length == memberStartIndex + 1)
        {
            var caseName = memberAccess.Parts[memberStartIndex];
            var caseSymbol = typeSymbol.UnionCases.FirstOrDefault(c => c.Name == caseName);
            if (caseSymbol != null)
            {
                _semanticInfo.SetPatternUnionCase(memberAccess, caseSymbol);
                return;
            }
            else
            {
                AddError(
                    $"Union '{typeSymbol.Name}' has no case '{caseName}'",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnionCaseNotFound,
                    span: memberAccess.Span);
                return;
            }
        }

        // Check if this is an enum member pattern (e.g., Color.RED)
        if (typeSymbol.TypeKind == TypeKind.Enum && memberAccess.Parts.Length == memberStartIndex + 1)
        {
            var memberName = memberAccess.Parts[memberStartIndex];
            var enumField = typeSymbol.Fields.FirstOrDefault(f => f.Name == memberName);
            if (enumField != null)
            {
                if (scrutineeType is UserDefinedType udt && udt.Symbol == typeSymbol)
                {
                    return;
                }
                else
                {
                    AddError(
                        $"Enum member '{typeSymbol.Name}.{memberName}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: memberAccess.Span);
                    return;
                }
            }
            else
            {
                AddError(
                    $"Enum '{typeSymbol.Name}' has no member '{memberName}'",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedMember,
                    span: memberAccess.Span);
                return;
            }
        }

        // Resolve remaining parts as field or property access
        SemanticType? resolvedType = null;
        for (int i = memberStartIndex; i < memberAccess.Parts.Length; i++)
        {
            var fieldName = memberAccess.Parts[i];
            var field = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field != null)
            {
                resolvedType = field.Type;
            }
            else
            {
                var prop = typeSymbol.Properties.FirstOrDefault(p => p.Name == fieldName);
                if (prop != null)
                {
                    resolvedType = prop.Type;
                }
                else
                {
                    AddError(
                        $"Type '{typeSymbol.Name}' has no member '{fieldName}'",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.UndefinedMember,
                        span: memberAccess.Span);
                    return;
                }
            }
        }

        if (resolvedType != null && !IsAssignable(resolvedType, scrutineeType) && !IsAssignable(scrutineeType, resolvedType))
        {
            AddError(
                $"Pattern type '{resolvedType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: memberAccess.Span);
        }
    }

    /// <summary>
    /// Tries to resolve a pattern type name as a union case of the scrutinee type.
    /// Supports both short form (e.g., "Ok" when scrutinee is Result) and
    /// long form (e.g., "Result.Ok" via dotted name in TypeAnnotation).
    /// Returns the union case TypeSymbol if found, or null otherwise.
    /// </summary>
    private TypeSymbol? TryResolveUnionCaseFromPattern(string typeName, SemanticType scrutineeType)
    {
        var (unionSymbol, _) = GetUnionSymbolAndTypeArgs(scrutineeType);
        if (unionSymbol == null)
            return null;

        // Short form: name matches a union case directly (e.g., "Ok" for Result union)
        var caseSymbol = unionSymbol.UnionCases.FirstOrDefault(c => c.Name == typeName);
        if (caseSymbol != null)
            return caseSymbol;

        // Long form: "UnionName.CaseName" — the TypeAnnotation name includes the dot
        if (typeName.Contains('.', StringComparison.Ordinal))
        {
            var parts = typeName.Split('.');
            if (parts.Length == 2 && parts[0] == unionSymbol.Name)
            {
                return unionSymbol.UnionCases.FirstOrDefault(c => c.Name == parts[1]);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the field types for a type symbol, applying generic type substitution
    /// when the type is a union case with a generic parent union.
    /// </summary>
    private List<SemanticType> GetUnionCaseFieldTypes(TypeSymbol typeSymbol, SemanticType scrutineeType)
    {
        var fieldTypes = typeSymbol.Fields.Select(f => f.Type).ToList();

        // If this is a union case, substitute type parameters from the scrutinee
        if (typeSymbol.BaseType is { TypeKind: TypeKind.Union } unionParent
            && unionParent.TypeParameters.Count > 0)
        {
            var (_, typeArgs) = GetUnionSymbolAndTypeArgs(scrutineeType);
            if (typeArgs != null && typeArgs.Count == unionParent.TypeParameters.Count)
            {
                for (int i = 0; i < fieldTypes.Count; i++)
                {
                    fieldTypes[i] = SubstituteTypeParameters(
                        fieldTypes[i], unionParent.TypeParameters, typeArgs);
                }
            }
        }

        return fieldTypes;
    }

    /// <summary>
    /// Extracts the union TypeSymbol and type arguments from a scrutinee type.
    /// Handles both UserDefinedType (non-generic unions) and GenericType (generic unions).
    /// </summary>
    private (TypeSymbol? UnionSymbol, List<SemanticType>? TypeArgs) GetUnionSymbolAndTypeArgs(
        SemanticType scrutineeType)
    {
        if (scrutineeType is UserDefinedType udt
            && udt.Symbol?.TypeKind == TypeKind.Union)
        {
            return (udt.Symbol, null);
        }

        if (scrutineeType is GenericType gt
            && gt.GenericDefinition?.TypeKind == TypeKind.Union)
        {
            return (gt.GenericDefinition, gt.TypeArguments);
        }

        // OptionalType -> synthetic union with Some(T) and None() cases
        if (scrutineeType is OptionalType optionalType)
        {
            var synth = GetSyntheticOptionalUnion();
            return (synth, new List<SemanticType> { optionalType.UnderlyingType });
        }

        // ResultType -> synthetic union with Ok(T) and Err(E) cases
        if (scrutineeType is ResultType resultType)
        {
            var synth = GetSyntheticResultUnion();
            return (synth, new List<SemanticType> { resultType.OkType, resultType.ErrorType });
        }

        return (null, null);
    }

    private TypeSymbol? _syntheticOptionalUnion;
    private TypeSymbol? _syntheticResultUnion;

    /// <summary>
    /// Returns a synthetic union TypeSymbol for Optional[T] with cases Some(T) and None().
    /// The type parameter T is substituted at pattern-check time via GetUnionCaseFieldTypes.
    /// </summary>
    private TypeSymbol GetSyntheticOptionalUnion()
    {
        if (_syntheticOptionalUnion != null)
            return _syntheticOptionalUnion;

        var tParam = new TypeParameterType { Name = "T" };

        var someCase = new TypeSymbol
        {
            Name = "Some",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "value", Kind = SymbolKind.Variable, Type = tParam, AccessLevel = AccessLevel.Public }
            }
        };

        var noneCase = new TypeSymbol
        {
            Name = "None",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>()
        };

        var optionalUnion = new TypeSymbol
        {
            Name = "Optional",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Union,
            AccessLevel = AccessLevel.Public,
            TypeParameters = new List<TypeParameterDef>
            {
                new() { Name = "T" }
            },
            UnionCases = new List<TypeSymbol> { someCase, noneCase }
        };

        someCase.BaseType = optionalUnion;
        noneCase.BaseType = optionalUnion;

        _syntheticOptionalUnion = optionalUnion;
        return optionalUnion;
    }

    /// <summary>
    /// Returns a synthetic union TypeSymbol for Result[T, E] with cases Ok(T) and Err(E).
    /// The type parameters T and E are substituted at pattern-check time via GetUnionCaseFieldTypes.
    /// </summary>
    private TypeSymbol GetSyntheticResultUnion()
    {
        if (_syntheticResultUnion != null)
            return _syntheticResultUnion;

        var tParam = new TypeParameterType { Name = "T" };
        var eParam = new TypeParameterType { Name = "E" };

        var okCase = new TypeSymbol
        {
            Name = "Ok",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "value", Kind = SymbolKind.Variable, Type = tParam, AccessLevel = AccessLevel.Public }
            }
        };

        var errCase = new TypeSymbol
        {
            Name = "Err",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "error", Kind = SymbolKind.Variable, Type = eParam, AccessLevel = AccessLevel.Public }
            }
        };

        var resultUnion = new TypeSymbol
        {
            Name = "Result",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Union,
            AccessLevel = AccessLevel.Public,
            TypeParameters = new List<TypeParameterDef>
            {
                new() { Name = "T" },
                new() { Name = "E" }
            },
            UnionCases = new List<TypeSymbol> { okCase, errCase }
        };

        okCase.BaseType = resultUnion;
        errCase.BaseType = resultUnion;

        _syntheticResultUnion = resultUnion;
        return resultUnion;
    }

    /// <summary>
    /// Recursively type-checks tuple unpacking target elements against their value types.
    /// Handles nested tuple targets like (a, b), c and (a, (b, c)), d.
    /// </summary>
    private void CheckTupleUnpackingElements(ImmutableArray<Expression> targets, IReadOnlyList<SemanticType> valueTypes)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var targetElem = targets[i];
            var valueElemType = valueTypes[i];

            if (targetElem is Identifier tupleTargetId)
            {
                var existingSymbol = _symbolTable.Lookup(tupleTargetId.Name, searchParents: false)
                    ?? _symbolTable.Lookup(tupleTargetId.Name, searchParents: true);

                // Check if trying to reassign a constant
                if (existingSymbol is VariableSymbol varSymbol && varSymbol.IsConstant)
                {
                    AddError($"Cannot reassign constant variable '{tupleTargetId.Name}' in tuple unpacking",
                        tupleTargetId.LineStart, tupleTargetId.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                        span: tupleTargetId.Span);
                    continue;
                }

                // In Sharpy, tuple unpacking creates new variable versions
                // Create/redefine with inferred type from tuple element
                var newSymbol = new VariableSymbol
                {
                    Name = tupleTargetId.Name,
                    Kind = SymbolKind.Variable,
                    Type = valueElemType,
                    IsConstant = false,
                    DeclarationLine = tupleTargetId.LineStart,
                    DeclarationColumn = tupleTargetId.ColumnStart,
                    NameDeclarationLine = tupleTargetId.LineStart,
                    NameDeclarationColumn = tupleTargetId.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(newSymbol);
                SemanticBinding.SetVariableType(newSymbol, valueElemType);
                _semanticInfo.SetIdentifierSymbol(tupleTargetId, newSymbol);

                if (existingSymbol is VariableSymbol predecessor)
                {
                    _semanticInfo.SetRebindingPredecessor(newSymbol, predecessor);
                    _semanticInfo.SetTargetBinding(tupleTargetId, new TargetBinding(TargetBindingKind.Rebinds));
                }
                else
                {
                    _semanticInfo.SetTargetBinding(tupleTargetId, new TargetBinding(TargetBindingKind.Declares));
                }

                _semanticInfo.SetExpressionType(tupleTargetId, valueElemType);
                if (valueElemType is UnknownType)
                {
                    MarkExpressionAsErrorRecovery(tupleTargetId,
                        ErrorRecoveryReason.Propagated("the matched tuple element's type"));
                }
            }
            else if (targetElem is TupleLiteral nestedTuple)
            {
                // Nested tuple unpacking: (a, b), c = expr
                if (valueElemType is not TupleType nestedTupleType)
                {
                    AddError($"Cannot unpack non-tuple type '{valueElemType.GetDisplayName()}' into nested tuple",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: targetElem.Span);
                    continue;
                }

                if (nestedTuple.Elements.Length != nestedTupleType.ElementTypes.Count)
                {
                    AddError($"Cannot unpack {nestedTupleType.ElementTypes.Count} values into {nestedTuple.Elements.Length} variables",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: targetElem.Span);
                    continue;
                }

                // Recurse into nested tuple
                CheckTupleUnpackingElements(nestedTuple.Elements, nestedTupleType.ElementTypes);
            }
            else
            {
                // For more complex targets (like attributes), just check type compatibility.
                // An index-access element (`b[k], y = …`) is checked in STORE position (#1620).
                SemanticType targetElemType;
                using (ScopedValue.Push(ref _indexStoreTarget, IndexStoreTarget.Of(targetElem)))
                    targetElemType = CheckExpression(targetElem);
                if (!IsAssignable(valueElemType, targetElemType))
                {
                    AddError($"Cannot assign type '{valueElemType.GetDisplayName()}' to '{targetElemType.GetDisplayName()}' in tuple unpacking",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: targetElem.Span);
                }
            }
        }
    }

    /// <summary>
    /// Type-checks star unpacking patterns: first, *rest = items
    /// The RHS can be a list[T] or tuple[...].
    /// </summary>
    private void CheckStarUnpacking(TupleLiteral targetTuple, SemanticType valueType, Assignment assignment)
    {
        // Validate only one star expression
        int starCount = targetTuple.Elements.Count(e => e is StarExpression);
        if (starCount > 1)
        {
            AddError("Only one starred expression is allowed in an unpacking assignment",
                assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.MultipleStarExpressions,
                span: assignment.Span);
            return;
        }

        // Determine element type from the source
        SemanticType elementType;
        if (valueType is GenericType { Name: BuiltinNames.List } listType && listType.TypeArguments.Count > 0)
        {
            elementType = listType.TypeArguments[0];
        }
        else if (valueType is TupleType tupleType)
        {
            // For tuples, compute the starred variable's element type from the rest elements
            int starIdx = targetTuple.Elements.ToList().FindIndex(e => e is StarExpression);
            int nBefore = starIdx;
            int nAfter = targetTuple.Elements.Length - starIdx - 1;
            int tupleArity = tupleType.ElementTypes.Count;

            // Collect the types of elements that go into the rest variable
            var restTypes = new List<SemanticType>();
            for (int ri = nBefore; ri < tupleArity - nAfter; ri++)
            {
                if (ri >= 0 && ri < tupleArity)
                    restTypes.Add(tupleType.ElementTypes[ri]);
            }

            if (restTypes.Count == 0)
            {
                elementType = tupleType.ElementTypes.Count > 0 ? tupleType.ElementTypes[0] : SemanticType.Unknown;
            }
            else if (restTypes.All(t => t.Equals(restTypes[0])))
            {
                elementType = restTypes[0];
            }
            else
            {
                elementType = BuiltinType.Object;
            }
        }
        else
        {
            AddError($"Cannot use starred unpacking with type '{valueType.GetDisplayName()}'",
                assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                span: assignment.Span);
            return;
        }

        // Define variables for each target
        foreach (var targetElem in targetTuple.Elements)
        {
            if (targetElem is StarExpression starExpr && starExpr.Operand is Identifier starId)
            {
                // Starred variable gets list[T] type
                var listTypeForStar = new GenericType
                {
                    Name = BuiltinNames.List,
                    TypeArguments = new List<SemanticType> { elementType }
                };
                var starSymbol = new VariableSymbol
                {
                    Name = starId.Name,
                    Kind = SymbolKind.Variable,
                    Type = listTypeForStar,
                    IsConstant = false,
                    DeclarationLine = starId.LineStart,
                    DeclarationColumn = starId.ColumnStart,
                    NameDeclarationLine = starId.LineStart,
                    NameDeclarationColumn = starId.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(starSymbol);
                SemanticBinding.SetVariableType(starSymbol, listTypeForStar);
                _semanticInfo.SetIdentifierSymbol(starId, starSymbol);
                _semanticInfo.SetTargetBinding(starId, new TargetBinding(TargetBindingKind.Declares));
                _semanticInfo.SetExpressionType(starId, listTypeForStar);
                _semanticInfo.SetExpressionType(starExpr, listTypeForStar);
            }
            else if (targetElem is Identifier id)
            {
                var symbol = new VariableSymbol
                {
                    Name = id.Name,
                    Kind = SymbolKind.Variable,
                    Type = elementType,
                    IsConstant = false,
                    DeclarationLine = id.LineStart,
                    DeclarationColumn = id.ColumnStart,
                    NameDeclarationLine = id.LineStart,
                    NameDeclarationColumn = id.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(symbol);
                SemanticBinding.SetVariableType(symbol, elementType);
                _semanticInfo.SetIdentifierSymbol(id, symbol);
                _semanticInfo.SetTargetBinding(id, new TargetBinding(TargetBindingKind.Declares));
                _semanticInfo.SetExpressionType(id, elementType);
            }
        }
    }
}
