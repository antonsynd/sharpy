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
                    if (!IsTruthTestable(guardType))
                    {
                        AddError("Guard condition must be a boolean expression",
                            matchCase.Guard.LineStart, matchCase.Guard.ColumnStart,
                            code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                            span: matchCase.Guard.Span);
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
                                $"Constant pattern type '{constType}' is not compatible with match subject type '{scrutineeType}'",
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
                    foreach (var alt in orPattern.Alternatives)
                    {
                        // GuardPattern wraps an inner pattern — check the inner pattern normally
                        var effectiveAlt = alt is GuardPattern gp ? gp.Inner : alt;
                        if (effectiveAlt is BindingPattern)
                        {
                            AddError(
                                "Binding patterns are not allowed inside or-patterns",
                                effectiveAlt.LineStart, effectiveAlt.ColumnStart,
                                code: DiagnosticCodes.Semantic.BindingInOrPattern,
                                span: effectiveAlt.Span);
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
                case TypePattern tp:
                    if (tp.BindingName != null)
                        names.Add(tp.BindingName.Name);
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
    /// and register any binding variable.
    /// </summary>
    private void CheckTypePattern(TypePattern typePattern, SemanticType scrutineeType)
    {
        // A bare generic name here is fillable from the scrutinee, so it must come back as the bare
        // symbol for the rule below to apply; the arity diagnostic an annotation would draw (#1331)
        // would pre-empt SPY0345's better diagnosis (#1235).
        var resolvedType = _typeResolver.ResolveTypeAnnotation(
            typePattern.Type, bareGenericFillsFromContext: true);

        // A bare `case list()` against an `array[T]` scrutinee (e.g. a CLR `object?[]`
        // surfaced from interop such as sqlite3's default row) binds the captured value
        // as the array itself, so indexing lowers to ArrayHelpers.GetItem. Resolve the
        // pattern to the scrutinee's array type rather than to `list[T]` (which would
        // bind the non-generic Sharpy.IList interface that a raw CLR array does not
        // implement).
        if (typePattern.Type.TypeArguments.Length == 0
            && typePattern.Type.Name == BuiltinNames.List
            && scrutineeType is GenericType { Name: BuiltinNames.Array } arrayScrutinee)
        {
            _semanticInfo.SetPatternType(typePattern, arrayScrutinee);
            if (typePattern.BindingName != null)
            {
                var arrayBinding = new VariableSymbol
                {
                    Name = typePattern.BindingName.Name,
                    Kind = SymbolKind.Variable,
                    Type = arrayScrutinee,
                    IsConstant = false,
                    DeclarationLine = typePattern.BindingName.LineStart,
                    DeclarationColumn = typePattern.BindingName.ColumnStart,
                    NameDeclarationLine = typePattern.BindingName.LineStart,
                    NameDeclarationColumn = typePattern.BindingName.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(arrayBinding);
                SemanticBinding.SetVariableType(arrayBinding, arrayScrutinee);
                _semanticInfo.SetIdentifierSymbol(typePattern.BindingName, arrayBinding);
            }
            return;
        }

        // Unparameterized collection patterns (e.g., `case list()`, `case dict()`)
        // against an `object` scrutinee. Fill in default `object` type arguments so
        // member access (indexing, .items(), etc.) resolves at the semantic level.
        //
        // Two resolution paths reach here:
        //   1. GenericType with zero type args — if TypeResolver happens to produce it.
        //   2. UserDefinedType backed by the BuiltinRegistry TypeSymbol — the common
        //      path for bare `dict`/`list`/`set` (zero-arg names skip ResolveGenericType
        //      and land in the user-defined-type leg of TypeResolver).
        if (typePattern.Type.TypeArguments.Length == 0 && IsObjectType(scrutineeType))
        {
            string? collectionName = null;
            int arity = 0;
            TypeSymbol? genericDef = null;

            if (resolvedType is GenericType { TypeArguments.Count: 0 } genericPattern)
            {
                collectionName = genericPattern.Name;
                genericDef = genericPattern.GenericDefinition;
            }
            else if (resolvedType is UserDefinedType { Symbol: TypeSymbol ts } && ts.IsGeneric)
            {
                collectionName = ts.Name;
                genericDef = ts;
            }

            if (collectionName != null)
            {
                arity = collectionName switch
                {
                    BuiltinNames.List => 1,
                    BuiltinNames.Set => 1,
                    BuiltinNames.Dict => 2,
                    _ => 0
                };
            }

            if (arity > 0)
            {
                var defaultArgs = new List<SemanticType>(arity);
                for (var i = 0; i < arity; i++)
                {
                    defaultArgs.Add(SemanticType.Object);
                }
                resolvedType = new GenericType
                {
                    Name = collectionName!,
                    TypeArguments = defaultArgs,
                    GenericDefinition = genericDef
                };
                _semanticInfo.SetPatternType(typePattern, resolvedType);
            }
        }

        // The bare-generic-name rule (#1235) — fill from the scrutinee or refuse. Shared with the
        // property- and positional-pattern arms; see ApplyBareGenericPatternRule for the rule text.
        if (ApplyBareGenericPatternRule(typePattern, typePattern.Type, resolvedType, scrutineeType)
            is not { } appliedPatternType)
        {
            return;
        }
        resolvedType = appliedPatternType;

        // #1358: a payload TYPE pattern against an UN-NARROWED Optional scrutinee tests the Some
        // case. `case str():` over `x: str?` is the bare spelling of `case Some(str())`, and a None
        // value falls through to the next arm — which is what Python does for `str | None` and what
        // `case None:` already reads as here.
        //
        // The subject is NOT unwrapped: a C# switch has one governing expression, and
        // Optional<T>.Unwrap() throws on an empty Optional, so an unwrapped subject would throw
        // before any arm ran. Instead this records the synthetic Some case through the SAME
        // node-keyed channel `case None:` uses above, and the emitter turns it into the positional
        // subpattern `(true, <payload>)` over the raw Optional<T> via Deconstruct. Because the
        // channel already exists, no new SemanticInfo dictionary joins MergeFrom.
        //
        // Recording the case is also what closes exhaustiveness: ExhaustivenessHelper already
        // reports {Some, None} as an Optional's case space, and its TypePattern arm reads this
        // union case before falling back to the pattern's type NAME — so `case str():` covered
        // "str" (matching neither case) and now covers "Some".
        //
        // Narrowing is deliberately not involved (the issue: "this is the un-narrowed shape"). Under
        // an `is not None` guard the scrutinee's type is already the payload, so this branch does not
        // fire and the #1299 unwrap lowering keeps owning that path.
        //
        // The sibling Result spelling (`case int():` over `int !E`) stays refused — see #1476, which
        // records why the two diverge and asks for a ruling. The divergence is not deliberate design:
        // `str` IS assignable to `str?` so Optional slipped past the compatibility check below and
        // reached codegen as an ICE, while `int` is NOT assignable to `int !E` so Result trips it.
        if (scrutineeType is OptionalType payloadOptional
            && resolvedType is not UnknownType
            && IsAssignable(resolvedType, payloadOptional.UnderlyingType))
        {
            var optionalUnion = GetSyntheticOptionalUnion();
            var someCase = optionalUnion.UnionCases.First(c => c.Name == WellKnownCaseNames.Some);
            _semanticInfo.SetPatternUnionCase(typePattern, someCase);
            // The payload type the emitter tests inside the Some subpattern. Recorded rather than
            // re-derived: the emitter reads this fact and maps it, making no type decision of its own.
            _semanticInfo.SetPatternType(typePattern, resolvedType);
            BindTypePatternCapture(typePattern, resolvedType);
            return;
        }

        if (resolvedType is UnknownType)
        {
            // Try to resolve as a union case (e.g., case Point(): when matching Shape)
            var unionCaseSymbol = TryResolveUnionCaseFromPattern(
                typePattern.Type.Name, scrutineeType);
            if (unionCaseSymbol != null)
            {
                _semanticInfo.SetPatternUnionCase(typePattern, unionCaseSymbol);
                resolvedType = new UserDefinedType { Name = unionCaseSymbol.Name, Symbol = unionCaseSymbol };
            }
            else
            {
                AddError(
                    $"Unknown type '{typePattern.Type.Name}' in type pattern",
                    typePattern.LineStart, typePattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedType,
                    span: typePattern.Span);
            }
        }
        else if (scrutineeType is not UnknownType
            && !IsAssignable(resolvedType, scrutineeType)
            && !IsAssignable(scrutineeType, resolvedType))
        {
            AddError(
                $"Type pattern '{resolvedType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                typePattern.LineStart, typePattern.ColumnStart,
                code: DiagnosticCodes.Semantic.TypePatternIncompatible,
                span: typePattern.Span);
        }
        BindTypePatternCapture(typePattern, resolvedType);
    }

    /// <summary>
    /// Defines the <c>as</c> capture of a type pattern (<c>case str() as s:</c>) at
    /// <paramref name="capturedType"/>. Shared by the ordinary arm and by #1358's Optional-payload
    /// arm, which binds at the PAYLOAD type rather than at the Optional: the emitted subpattern
    /// destructures <c>Optional&lt;T&gt;</c>, so what the name is bound to is the unwrapped value.
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
    /// Refuses a bare generic name in a class pattern whose type arguments the scrutinee does not
    /// determine (#1235). Shares SPY0345's diagnosis with the other type-operand positions but
    /// <b>not</b> their remedy: the parser rejects type arguments in a pattern (SPY0125), so
    /// <c>case Box[int]():</c> is not writable and suggesting it would send the user to a spelling the
    /// compiler refuses. What does work is giving the scrutinee a more specific static type before
    /// matching, or matching a non-generic base.
    /// </summary>
    private void ReportOpenGenericPatternType(TypeAnnotation patternType, TypeSymbol typeSymbol, Text.TextSpan? fallbackSpan)
    {
        // Both suggestions below are executed as fixtures, not assumed. The isinstance guard used to
        // be excluded here — the narrowed type did not reach the scrutinee, so suggesting it would
        // have sent the user in a circle — and #1299 fixed exactly that, which makes it the first
        // remedy to offer: it is the one that keeps the match reading like the code you wrote.
        var name = patternType.Name;
        var placeholders = OpenGenericPlaceholders(typeSymbol);
        ReportOpenGenericTypeOperand(
            patternType, name, siteNoun: "match pattern",
            remedy: "Match on a value whose static type supplies them — guard the match with "
                + $"`if isinstance(x, {name}[{placeholders}]):`, or bind it first with "
                + $"`v: {name}[{placeholders}] = x as! {name}[{placeholders}]` — or match against a "
                + "non-generic base type. A pattern cannot name type arguments itself.",
            fallbackSpan: fallbackSpan);
    }

    /// <summary>
    /// The bare-generic-name rule for class patterns (#1235), one copy for all three pattern shapes
    /// (type, property, positional — the batch originally wired only the type-pattern arm, and the
    /// other two leaked the open <c>Box&lt;T&gt;</c> as CS0305 behind SPY0908). .NET reifies generics,
    /// so a bare <c>Box</c> names no runtime type: fill the argument vector from the scrutinee's own
    /// static type and record the decision as the pattern's type for the emitter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One rule for user generics and builtin collections; erasure is the fill's fallback</b>
    /// (#1299). Collections used to be excluded before the fill, on the reasoning that their
    /// erasure was the #912 rule and routing them here would add a copy. The residue of that
    /// boundary was that <c>case list():</c> was REFUSED against a <c>list[int]</c> scrutinee
    /// (SPY0361) while being accepted against <c>object</c> — a pattern rejected by the very type
    /// it matches, and the opposite of CPython, where <c>case list():</c> matches a plain list.
    /// </para>
    /// <para>
    /// Moving the exclusion after the fill keeps erasure exactly where it belongs: a non-generic
    /// scrutinee (<c>object</c>) fills nothing, and for an erasable collection that null fill means
    /// "erase", not "refuse". Only user generics reach <see cref="ReportOpenGenericPatternType"/>,
    /// because for them an unfillable bare name genuinely names no runtime type.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The filled type (recorded on the pattern), <paramref name="resolvedType"/> unchanged when the
    /// rule does not apply or the collection erases, or <c>null</c> when the pattern was refused and
    /// checking should stop.
    /// </returns>
    private SemanticType? ApplyBareGenericPatternRule(
        Pattern pattern, TypeAnnotation patternType, SemanticType resolvedType, SemanticType scrutineeType)
    {
        if (patternType.TypeArguments.Length != 0
            || _symbolTable.Lookup(patternType.Name) is not TypeSymbol { IsGeneric: true } genericPatternType)
        {
            return resolvedType;
        }

        if (FillTypeArgumentsFromSubject(genericPatternType, scrutineeType) is { } filledPatternType)
        {
            _semanticInfo.SetPatternType(pattern, filledPatternType);
            return filledPatternType;
        }

        if (BuiltinNames.IsErasableCollection(genericPatternType.Name))
        {
            // Nothing to fill from (an object scrutinee): the #912 erasure applies and the
            // type-pattern arm and the emitter express it.
            return resolvedType;
        }

        ReportOpenGenericPatternType(patternType, genericPatternType, pattern.Span);
        return null;
    }

    /// <summary>
    /// Check a property pattern: resolve the type, then validate each field sub-pattern.
    /// </summary>
    private void CheckPropertyPattern(PropertyPattern propertyPattern, SemanticType scrutineeType)
    {
        TypeSymbol? typeSymbol = null;
        if (propertyPattern.Type != null)
        {
            var resolvedType = _typeResolver.ResolveTypeAnnotation(
                propertyPattern.Type, bareGenericFillsFromContext: true);
            if (resolvedType is UnknownType)
            {
                AddError(
                    $"Unknown type '{propertyPattern.Type.Name}' in property pattern",
                    propertyPattern.LineStart, propertyPattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedType,
                    span: propertyPattern.Span);
            }
            else
            {
                // The bare-generic-name rule (#1235): fill from the scrutinee or refuse — same rule
                // as the type-pattern arm, same recorded channel the emitter reads.
                var applied = ApplyBareGenericPatternRule(
                    propertyPattern, propertyPattern.Type, resolvedType, scrutineeType);
                if (applied == null)
                    return;

                typeSymbol = applied switch
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
                var resolvedType = _typeResolver.ResolveTypeAnnotation(
                    positionalPattern.Type, bareGenericFillsFromContext: true);
                if (resolvedType is UnknownType)
                {
                    AddError(
                        $"Unknown type '{positionalPattern.Type.Name}' in positional pattern",
                        positionalPattern.LineStart, positionalPattern.ColumnStart,
                        code: DiagnosticCodes.Semantic.UndefinedType,
                        span: positionalPattern.Span);
                }
                else
                {
                    // The bare-generic-name rule (#1235): fill from the scrutinee or refuse — same
                    // rule as the type-pattern arm, same recorded channel the emitter reads.
                    var applied = ApplyBareGenericPatternRule(
                        positionalPattern, positionalPattern.Type, resolvedType, scrutineeType);
                    if (applied == null)
                        return;

                    typeSymbol = applied switch
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
        // Resolve the dotted path as a member access (e.g., Color.RED).
        // Look up the first part as a type, then resolve subsequent parts as fields/members.
        var typeName = memberAccess.Parts[0];
        var typeSymbol = _symbolTable.Lookup(typeName) as TypeSymbol;
        if (typeSymbol == null)
        {
            AddError(
                $"Undefined type '{typeName}' in pattern",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType,
                span: memberAccess.Span);
            return;
        }

        // Check if this is a union case pattern (e.g., Option.None, Result.Ok)
        if (typeSymbol.TypeKind == TypeKind.Union && memberAccess.Parts.Length == 2)
        {
            var caseName = memberAccess.Parts[1];
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
        if (typeSymbol.TypeKind == TypeKind.Enum && memberAccess.Parts.Length == 2)
        {
            var memberName = memberAccess.Parts[1];
            var enumField = typeSymbol.Fields.FirstOrDefault(f => f.Name == memberName);
            if (enumField != null)
            {
                // Verify the enum type matches the scrutinee type
                if (scrutineeType is UserDefinedType udt && udt.Symbol == typeSymbol)
                {
                    // Valid enum member pattern matching the scrutinee
                    return;
                }
                else
                {
                    AddError(
                        $"Enum member '{typeName}.{memberName}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
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
        for (int i = 1; i < memberAccess.Parts.Length; i++)
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
                        $"Type '{typeName}' has no member '{fieldName}'",
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
                var existingSymbol = _symbolTable.Lookup(tupleTargetId.Name, searchParents: false);

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
                // For more complex targets (like attributes), just check type compatibility
                var targetElemType = CheckExpression(targetElem);
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
                _semanticInfo.SetExpressionType(id, elementType);
            }
        }
    }
}
