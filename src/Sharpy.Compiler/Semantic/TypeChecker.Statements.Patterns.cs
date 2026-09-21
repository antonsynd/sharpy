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
                    var (mcGuardTestable, mcGuardType) = CheckTruthinessTest(matchCase.Guard);
                    if (!mcGuardTestable)
                    {
                        ReportNotTruthTestable(matchCase.Guard, mcGuardType, "Guard condition must be a boolean expression",
                            code: DiagnosticCodes.Semantic.ConditionNotBoolean);
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
                            _semanticInfo.SetPatternUnionCase(
                                binding, unionCaseSymbol, GetUnionSymbolAndTypeArgs(scrutineeType).TypeArgs);

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
                    //
                    // A class-body const the scope walk crossed (#1786, R-Y) counts here too. R-Y
                    // makes a class member invisible to a bare READ and refuses a bare STORE; a
                    // pattern head is neither — degrading it to a capture would silently change
                    // what the program MATCHES, and `case A:` naming a class const printed `hit`
                    // before the rule existed. The constant is bound; the capture arm below never
                    // sees it.
                    var patternResolution = _symbolTable.Resolve(binding.Name.Name);
                    var existingSymbol = (patternResolution.Bound
                        ?? (patternResolution.CrossedMember is { IsConstant: true } crossedConst
                            ? crossedConst
                            : null)) as VariableSymbol;
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
                        _semanticInfo.SetPatternUnionCase(
                            literal, noneCase, GetUnionSymbolAndTypeArgs(scrutineeType).TypeArgs);
                        break;
                    }

                    // Handle bare None literal when matching against Optional[T]
                    if (literal.Literal is NoneLiteral && scrutineeType is OptionalType)
                    {
                        var synth = GetSyntheticOptionalUnion();
                        var noneCase = synth.UnionCases.First(c => c.Name == "None");
                        _semanticInfo.SetPatternUnionCase(
                            literal, noneCase, GetUnionSymbolAndTypeArgs(scrutineeType).TypeArgs);
                        break;
                    }

                    // `case None:` over a `T | None` (NullableType) is the None case of the finite
                    // family (P13 D3) — record NoneArm so exhaustiveness counts it and GetFiniteTypeCases
                    // sees the whole family covered. A NullableType is NOT a tagged union, so there is no
                    // synthetic union case to record as there is for OptionalType above.
                    if (literal.Literal is NoneLiteral && scrutineeType is NullableType)
                    {
                        _semanticInfo.SetPatternCoverage(literal, PatternCoverage.NoneArm);
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

                    // #1920: no alternative may bind a name. Every capture route below funnels
                    // through CheckAlternativeRefusingCaptures — see its docs for the rule.
                    bool captureRefused = false;

                    if (allHaveAs)
                    {
                        // `(A() as v) | (B() as v)`: one name bound under every alternative. Its
                        // type is what EVERY alternative guarantees — the alternatives' common
                        // ancestor — not the first alternative's own type: binding `v` as `float`
                        // for `(float() as v) | (list() as v)` while the emitted `var v` is
                        // `object` gave CS1503 behind SPY0908 (#1663).
                        var altOperands = new List<(Expression? Node, SemanticType Type)>();
                        var asAlts = new List<AsPattern>();
                        foreach (var alt in orPattern.Alternatives)
                        {
                            var effectiveAlt = alt is GuardPattern gp ? gp.Inner : alt;
                            var asAlt = (AsPattern)effectiveAlt;
                            asAlts.Add(asAlt);
                            // The `as` name is bound OUTSIDE the C# `or` (`(A or B) and var v`), so
                            // it lowers; a capture in the alternative's INNER pattern does not.
                            CheckAlternativeRefusingCaptures(asAlt.Inner, scrutineeType, ref captureRefused);
                            altOperands.Add((null,
                                _semanticInfo.GetPatternType(asAlt.Inner) ?? scrutineeType));
                        }
                        var firstAs = asAlts[0];
                        var joinedType = BestCommonType(altOperands, scrutineeType,
                            StorePosition.Declaration, orPattern, "or-pattern capture");

                        // One name, bound under every alternative, is what the lowering emits.
                        // Alternatives that bind DIFFERENT names have no single `var v` to emit:
                        // only the first name was ever defined, so the body read the others as
                        // SPY0200 "undefined identifier" with nothing naming the rule (#1920).
                        // CPython refuses the same shape ("alternative patterns bind different
                        // names"). Every name is still bound below so the body does not cascade.
                        var divergent = asAlts.FirstOrDefault(
                            a => !string.Equals(a.Name.Name, firstAs.Name.Name, StringComparison.Ordinal));
                        if (divergent != null && !captureRefused)
                        {
                            captureRefused = true;
                            AddError(
                                "Binding patterns are not allowed inside or-patterns: alternatives bind "
                                + $"different names ('{firstAs.Name.Name}' and '{divergent.Name.Name}'). "
                                + "Every alternative must bind the same name, or split the alternatives "
                                + "into separate cases",
                                divergent.Name.LineStart, divergent.Name.ColumnStart,
                                code: DiagnosticCodes.Semantic.BindingInOrPattern,
                                span: divergent.Span);
                        }

                        BindAsPatternCapture(firstAs, scrutineeType, capturedTypeOverride: joinedType);
                        if (divergent != null)
                        {
                            foreach (var asAlt in asAlts)
                                if (!string.Equals(asAlt.Name.Name, firstAs.Name.Name, StringComparison.Ordinal))
                                    BindAsPatternCapture(asAlt, scrutineeType, capturedTypeOverride: joinedType);
                        }
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
                                BindRefusedCapture(bindingInOr, scrutineeType);
                            }
                            else
                            {
                                CheckAlternativeRefusingCaptures(alt, scrutineeType, ref captureRefused);
                            }
                        }
                        else if (effectiveAlt is AsPattern asInOr)
                        {
                            AddError(
                                "Binding patterns are not allowed inside or-patterns",
                                asInOr.LineStart, asInOr.ColumnStart,
                                code: DiagnosticCodes.Semantic.BindingInOrPattern,
                                span: asInOr.Span);
                            BindAsPatternCapture(asInOr, scrutineeType);
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
                            CheckAlternativeRefusingCaptures(alt, scrutineeType, ref captureRefused);
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
    /// Type-checks a list (sequence) pattern: decides the element type through
    /// <see cref="ResolveSequenceSubject"/> (the class-pattern subject rule, Decision 6 / #1702),
    /// checks each element pattern against the element type, and binds a <c>*rest</c> capture as
    /// <c>list[T]</c> (#991). A refused subject leaves the element type <see cref="SemanticType.Unknown"/>
    /// for element-binding recovery, while the reported diagnostic aborts emission.
    /// </summary>
    private void CheckListPattern(ListPattern listPattern, SemanticType scrutineeType)
    {
        var elementType = ResolveSequenceSubject(scrutineeType, listPattern) ?? SemanticType.Unknown;

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
    /// Decides the element type a sequence (list) pattern matches against, by the SAME rule a class
    /// pattern's head uses (Design Decision 6, #1702): the sequence subject is on the type-test
    /// classifier, not a private switch.
    /// <list type="bullet">
    /// <item>A closed <c>list[T]</c> / <c>array[T]</c> — or a <c>T | None</c>
    /// (<see cref="NullableType"/>) wrapping one — gives its element type (the class-pattern arm-1
    /// fill; d04/d05b/d10).</item>
    /// <item>A <c>T?</c> (<see cref="OptionalType"/>) list is a tagged union: a bare sequence pattern
    /// cannot see through it, so it is matched through its <c>Some</c> case (SPY0498; a09's twin).</item>
    /// <item>An open subject — <c>object</c> or a type parameter — determines no element type, so a C#
    /// list pattern against it is CS8985; refused with the <c>case list[int]([1, 2])</c> steer
    /// (SPY0345; the class-pattern arm-3 rule; d01/d12/d13/d14/a16). Before this, the open subject
    /// reached the emitter and left the ICE.</item>
    /// <item>Any other concrete type — <c>str</c>, <c>tuple</c>, <c>int</c> — is not a sequence
    /// (SPY0220; d06/d07/d08).</item>
    /// </list>
    /// Returns the element type, or null when the pattern was refused (a diagnostic was reported) or
    /// the subject is <see cref="UnknownType"/> — a recovery shape whose real error is already
    /// reported, so it stays silent. Every refusal here aborts emission like every pattern refusal.
    /// </summary>
    private SemanticType? ResolveSequenceSubject(SemanticType scrutineeType, ListPattern listPattern)
    {
        // Arm 1: closed list[T] / array[T], including a `T | None` (NullableType) wrapping one. A
        // nullable list still tests as a CLR sequence; the null case falls to a later match arm.
        // An OptionalType is intentionally NOT unwrapped here — it is a tagged union matched through
        // Some (below), not a nullable reference (this is why a08 fills but a09 refuses).
        var sequenceType = scrutineeType is NullableType nullable ? nullable.UnderlyingType : scrutineeType;
        if (sequenceType is GenericType { Name: BuiltinNames.List or BuiltinNames.Array } g
            && g.TypeArguments.Count > 0)
        {
            return g.TypeArguments[0];
        }

        // A `T?` (OptionalType) list must be matched through its Some case — the sequence-pattern twin
        // of the class-pattern SPY0498 refusal (a09).
        if (scrutineeType is OptionalType
            { UnderlyingType: GenericType { Name: BuiltinNames.List or BuiltinNames.Array } })
        {
            AddError(
                "An Optional scrutinee cannot be matched with a sequence pattern directly. " +
                "Match through the constructor cases instead: 'case Some([a, b]):' for a present " +
                "value and 'case None():' for absence (or narrow first with 'if xs is not None:').",
                listPattern.LineStart, listPattern.ColumnStart,
                code: DiagnosticCodes.Validation.PayloadTypePatternOverUnion,
                span: listPattern.Span);
            return null;
        }

        // An Unknown subject's real error is already reported — stay silent, never a second diagnostic.
        if (scrutineeType is UnknownType)
            return null;

        // An open subject — `object` or a type parameter — determines no element type. Refused with the
        // closed-spelling steer (SPY0345), the class-pattern arm-3 rule, so the C# list pattern is never
        // emitted against `object` (CS8985).
        if (scrutineeType.IsObjectLike || scrutineeType is TypeParameterType)
        {
            ReportOpenGenericTypeOperand(listPattern, BuiltinNames.List, TypeTestSite.SequencePattern, arity: 1);
            return null;
        }

        // Any other concrete type is not a sequence.
        AddError(
            $"Cannot match non-sequence type '{scrutineeType.GetDisplayName()}' with a list pattern",
            listPattern.LineStart, listPattern.ColumnStart,
            code: DiagnosticCodes.Semantic.TypeMismatch,
            span: listPattern.Span);
        return null;
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
    /// The or-pattern capture seam (#1920). C# cannot declare a designation inside an <c>or</c>
    /// pattern (CS8780), so a name bound by an ALTERNATIVE has no lowering — whatever the
    /// alternative's kind. A reified head (<c>list[int](xs)</c>), a class pattern
    /// (<c>Point(x)</c>), a property pattern (<c>Point(x=a)</c>), a sequence element
    /// (<c>[x] | [x, _]</c>), a <c>*rest</c> capture, a tuple element, a union-case payload and a
    /// nested <c>as</c> all produce the same CS8780 (plus CS0165 on the body's read), which
    /// surfaced as SPY0908 before this seam existed. Only ONE capture shape lowers: <c>as</c> on
    /// EVERY alternative under the same name, which the emitter hoists outside the C# <c>or</c> as
    /// <c>(A or B) and var v</c> (#1663) — its caller handles that shape and passes only the
    /// alternative's INNER pattern here.
    /// <para>
    /// Detection is by OBSERVATION, not by a structural walk: the pattern is checked normally and
    /// anything it defines in the case scope is a capture. A bare name that resolved to a union
    /// variant (<c>case Red | Yellow</c>, #1562) or to a constant (RFC 3535) defines nothing and is
    /// therefore not a capture — no structural walker could tell those apart from a capture without
    /// re-deriving each nested sub-pattern's own subject type.
    /// </para>
    /// <para>
    /// The captured names stay bound so the case body reads them normally: the refusal is the one
    /// diagnostic the program gets, with no "undefined identifier" cascade behind it.
    /// <paramref name="alreadyRefused"/> keeps it to one report per or-pattern.
    /// </para>
    /// </summary>
    private void CheckAlternativeRefusingCaptures(
        Pattern alternative, SemanticType scrutineeType, ref bool alreadyRefused)
    {
        var scope = _symbolTable.CurrentScope;
        var before = new HashSet<string>(
            scope.GetAllSymbols().Select(s => s.Name), StringComparer.Ordinal);

        CheckPattern(alternative, scrutineeType);

        if (alreadyRefused)
            return;

        var captured = scope.GetAllSymbols()
            .Where(s => !before.Contains(s.Name))
            .OrderBy(s => s.DeclarationLine ?? alternative.LineStart)
            .ThenBy(s => s.DeclarationColumn ?? alternative.ColumnStart)
            .FirstOrDefault();
        if (captured == null)
            return;

        alreadyRefused = true;
        AddError(
            $"Binding patterns are not allowed inside or-patterns: alternative binds '{captured.Name}'. "
            + "C# cannot declare a variable inside an 'or' pattern, so split the alternatives into "
            + "separate cases, or bind the whole subject with 'as' on every alternative: "
            + "case (A() as v) | (B() as v):",
            captured.DeclarationLine ?? alternative.LineStart,
            captured.DeclarationColumn ?? alternative.ColumnStart,
            code: DiagnosticCodes.Semantic.BindingInOrPattern,
            span: alternative.Span);
    }

    /// <summary>
    /// Binds a capture the or-pattern seam just REFUSED, so the refusal is the only diagnostic the
    /// program gets. Without it the case body read an unbound name and SPY0359 arrived with a
    /// SPY0200 "undefined identifier" behind it, pointing the reader at the body instead of the
    /// pattern (#1920). Emission never happens — the refusal is an error — so the binding is
    /// error-recovery state only.
    /// </summary>
    private void BindRefusedCapture(BindingPattern binding, SemanticType scrutineeType)
    {
        if (_symbolTable.CurrentScope.Lookup(binding.Name.Name, searchParent: false) != null)
            return;

        var recovery = new VariableSymbol
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

        _symbolTable.Define(recovery);
        SemanticBinding.SetVariableType(recovery, scrutineeType);
        _semanticInfo.SetIdentifierSymbol(binding.Name, recovery);
        _semanticInfo.SetTargetBinding(binding, new TargetBinding(TargetBindingKind.Declares));
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

        if (capturedTypeOverride == null && asPattern.Inner is TypePattern typeInner
            && _semanticInfo.GetPatternType(typeInner) is { } patternType)
        {
            // The inner pattern has already been classified by the CheckPattern call above —
            // ClassifyPatternClassTest records a type on every class pattern it accepts — so this
            // READS that fact instead of classifying a second time. Classifying twice reported the
            // same refusal twice and lodged the lowering twice (#1670).
            capturedType = patternType;
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
                default:
                    // walker-default-contract: any kind not listed above is deliberately ignored by
                    // this walker (rostered in DispatchSiteInventoryTests).
                    break;
            }
        }
        Walk(pattern);
        return names;
    }

    /// <summary>
    /// Classifies the type a class pattern names — <c>case T():</c>, <c>case T(sub):</c> and
    /// <c>case T{…}:</c> alike — and records every fact the validators and the emitter later read
    /// off the pattern node: the type-test lowering, the pattern's type, and the test's totality
    /// (#1670).
    /// <para>
    /// <b>One helper because the three arms are one rule</b> (owner ruling Q1, #1670): a class
    /// pattern is STATIC, exactly like <c>isinstance</c> — the scrutinee's static type decides, and
    /// nothing is reflected at run time. A bare erasable collection name is FILLED from the
    /// scrutinee when the scrutinee determines the vector (<c>list[int]</c> × <c>case list(xs):</c>
    /// tests <c>Sharpy.List&lt;int&gt;</c> and captures <c>list[int]</c>), ERASED to the non-generic
    /// protocol interface when it does not (<c>object</c> × <c>case list(xs):</c> tests
    /// <c>Sharpy.IList</c> and captures <c>list[object]</c>), and REFUSED when the two types are
    /// provably disjoint (<c>str</c> × <c>case list(xs):</c> → SPY0361). Whichever arm the pattern
    /// was written in cannot change that answer, which is exactly what went wrong: the positional
    /// arm reached codegen with the annotation and emitted <c>Sharpy.List&lt;object&gt;</c> against
    /// an <c>object</c> subject, so <c>case list(xs):</c> silently took the <c>_</c> arm.
    /// </para>
    /// <para>
    /// The fill is tried BEFORE the erasure the <c>isinstance</c> classifier applies: a boolean site
    /// only answers yes/no, but a pattern BINDS the subject, so a scrutinee that determines the
    /// vector must give the capture its element type rather than <c>object</c>.
    /// </para>
    /// <para>
    /// Null means stop — either the classifier reported the refusal (SPY0345 for an open generic
    /// nothing fills), or the name denotes no type and SPY0202 is reported here. There is
    /// deliberately no <c>?? scrutineeType</c> fallback: that fallback turned <c>case bytearray(v):</c>
    /// into an irrefutable <c>case object v:</c> that matched an <c>int</c> (#1670).
    /// </para>
    /// </summary>
    /// <param name="annotation">The type as written in the pattern.</param>
    /// <param name="lodgeOn">The PATTERN node the facts are keyed on — walkers never visit
    /// <see cref="TypePattern.Type"/>, so a fact lodged on the annotation would strand.</param>
    /// <param name="scrutineeType">The static type of the value being matched.</param>
    /// <param name="patternNoun">How SPY0202 names this position ("type pattern", …).</param>
    /// <returns>The type the pattern tests against, or null when the pattern was refused.</returns>
    private SemanticType? ClassifyPatternClassTest(
        TypeAnnotation annotation,
        Pattern lodgeOn,
        SemanticType scrutineeType,
        string patternNoun)
    {
        // One shared decider (#1708): the array-interop and subject-fill arms it used to duplicate here
        // now live in DecideBoundTypeTest, so `isinstance` gets the same array interop and a bare
        // collection on an open subject is refused (SPY0345) rather than erased. A type-parameter
        // scrutinee therefore returns null (refused) before the compatibility check below, so it never
        // draws SPY0361 — it agrees with isinstance on SPY0345 (#1619).
        var testType = ClassifyTypeTestAnnotation(
            annotation, lodgeOn, scrutineeType, TypeTestSite.Pattern, out _);

        if (testType == null)
        {
            // The classifier records nothing for a name it cannot resolve and reports nothing for it
            // either (it also returns null AFTER reporting SPY0345), so SPY0202 is this site's to
            // raise — and only when the name really resolves to no type.
            var knownSymbol = _symbolTable.Lookup(annotation.Name) as TypeSymbol
                ?? _typeResolver.ResolveDottedTypeName(annotation.Name, annotation.IsNameBacktickEscaped);
            if (knownSymbol == null)
            {
                AddError(
                    $"Unknown type '{annotation.Name}' in {patternNoun}",
                    lodgeOn.LineStart, lodgeOn.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedType,
                    span: lodgeOn.Span);
            }
            return null;
        }

        // #1510: a tagged-union scrutinee is matched through its cases, never through the payload's
        // type — `case Some(v):`/`case None():`, not `case int():`.
        if (scrutineeType is OptionalType payloadOptional
            && IsAssignable(testType, payloadOptional.UnderlyingType))
        {
            AddError(
                $"An Optional scrutinee cannot be matched with the payload type pattern " +
                $"'{testType.GetDisplayName()}'. Match through the constructor cases instead: " +
                "'case Some(v):' for a present value and 'case None():' for absence " +
                "(or narrow first with 'if x is not None:').",
                lodgeOn.LineStart, lodgeOn.ColumnStart,
                code: DiagnosticCodes.Validation.PayloadTypePatternOverUnion,
                span: lodgeOn.Span);
            return null;
        }

        if (scrutineeType is ResultType payloadResult
            && (IsAssignable(testType, payloadResult.OkType)
                || IsAssignable(testType, payloadResult.ErrorType)))
        {
            AddError(
                $"A Result scrutinee cannot be matched with the payload type pattern " +
                $"'{testType.GetDisplayName()}'. Match through the constructor cases instead: " +
                "'case Ok(v):' for success and 'case Err(e):' for failure.",
                lodgeOn.LineStart, lodgeOn.ColumnStart,
                code: DiagnosticCodes.Validation.PayloadTypePatternOverUnion,
                span: lodgeOn.Span);
            return null;
        }

        // Provably disjoint: no value of the scrutinee's type can be an instance of the tested type,
        // so the arm is dead. Refused statically here rather than left to CS8121 behind SPY0908.
        if (scrutineeType is not UnknownType
            && !IsAssignable(testType, scrutineeType)
            && !IsAssignable(scrutineeType, testType))
        {
            AddError(
                $"Type pattern '{annotation.Name}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                lodgeOn.LineStart, lodgeOn.ColumnStart,
                code: DiagnosticCodes.Semantic.TypePatternIncompatible,
                span: lodgeOn.Span);
        }

        _semanticInfo.SetPatternType(lodgeOn, testType);
        if (scrutineeType is not UnknownType && testType is not UnknownType)
        {
            // Null-aware coverage (P13 D2): a `T | None` scrutinee is NEVER total for a non-nullable
            // test type — the None value escapes the type test — so a payload head is PayloadTotal,
            // covering only the payload case of the finite family. NullableType.IsAssignableTo is
            // null-blind (SemanticType.cs:736), which is why the un-split scrutinee wrongly recorded
            // Total and drew a spurious SPY0700 on `case list():` + `case None:`.
            if (scrutineeType is NullableType nullableScrutinee)
            {
                if (IsAssignable(nullableScrutinee.UnderlyingType, testType))
                    _semanticInfo.SetPatternCoverage(lodgeOn, PatternCoverage.PayloadTotal);
            }
            else if (IsAssignable(scrutineeType, testType))
            {
                _semanticInfo.SetPatternCoverage(lodgeOn, PatternCoverage.Total);
            }
        }

        return testType;
    }

    /// <summary>
    /// Records a union-case pattern head as a REFERENCE to its case symbol on the head's type
    /// annotation, through the same <see cref="Semantic.SemanticInfo.SetTypeAnnotation"/> seam #1737
    /// uses for class-pattern heads — so hover (#1735) answers for union-case heads and the
    /// annotation-reference matrix's pattern_head rows drain. Guarded to record ONCE (Decision 8),
    /// matching <c>ClassifyTypeTestAnnotation</c>'s guard for class-pattern heads.
    /// </summary>
    private void RecordUnionCaseHeadReference(
        TypeAnnotation annotation, SemanticType caseType, TypeSymbol caseSymbol)
    {
        if (_semanticInfo.GetTypeAnnotation(annotation) == null)
            _semanticInfo.SetTypeAnnotation(annotation, caseType, caseSymbol);
    }

    /// <summary>
    /// Check a type pattern: resolve the type, handle union cases, validate compatibility,
    /// and register any binding variable. Routes through the shared class-pattern classifier so
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
            var earlyResolved = new UserDefinedType { Name = earlyUnionCase.Name, Symbol = earlyUnionCase };
            _semanticInfo.SetPatternUnionCase(
                typePattern, earlyUnionCase, GetUnionSymbolAndTypeArgs(scrutineeType).TypeArgs);
            _semanticInfo.SetPatternType(typePattern, earlyResolved);
            RecordUnionCaseHeadReference(typePattern.Type, earlyResolved, earlyUnionCase);

            // `case Some():` over an Optional scrutinee lowers to the (has-value, payload)
            // deconstruction, so the type the emitted pattern TESTS is the payload, not the case
            // symbol — recording the case symbol here is what emitted `case (true, Some _)` and
            // CS0246 behind SPY0908 (#1670). Every other union case tests the case type itself.
            var earlyTestType = earlyUnionCase.Name == WellKnownCaseNames.Some
                && scrutineeType is OptionalType earlyOptional
                    ? earlyOptional.UnderlyingType
                    : earlyResolved;
            _semanticInfo.SetTypeTestLowering(typePattern,
                new TypeTestLowering(TypeTestLoweringKind.ClosedType, earlyTestType));

            return;
        }

        ClassifyPatternClassTest(typePattern.Type, typePattern, scrutineeType, "type pattern");
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
                _semanticInfo.SetPatternUnionCase(
                    propertyPattern, earlyUnionCase, GetUnionSymbolAndTypeArgs(scrutineeType).TypeArgs);
                RecordUnionCaseHeadReference(
                    propertyPattern.Type,
                    new UserDefinedType { Name = earlyUnionCase.Name, Symbol = earlyUnionCase },
                    earlyUnionCase);
            }
            else
            {
                var classifiedType = ClassifyPatternClassTest(
                    propertyPattern.Type, propertyPattern, scrutineeType, "property pattern");
                if (classifiedType == null)
                    return;

                typeSymbol = classifiedType switch
                {
                    UserDefinedType udt => udt.Symbol,
                    GenericType { GenericDefinition: { } filledDefinition } => filledDefinition,
                    _ => null
                };
            }
        }

        // Substitute type parameters through the SAME path positional patterns use (P13 D7): a
        // generic union case's field is declared as `T`, so `case Node(value=7):` on `Tree[int]` must
        // type the field sub-pattern against `int32`, not the unsubstituted `T` (which reported a
        // spurious SPY0220 "'int32' incompatible with 'T'"). fieldTypes[i] corresponds to
        // typeSymbol.Fields[i]; index by the field's position so the lookup is by name AND substituted.
        var fieldTypes = typeSymbol != null
            ? GetUnionCaseFieldTypes(typeSymbol, scrutineeType)
            : null;

        foreach (var field in propertyPattern.Fields)
        {
            if (typeSymbol != null)
            {
                var fieldIndex = typeSymbol.Fields.FindIndex(f => f.Name == field.Name);
                if (fieldIndex < 0)
                {
                    AddError(
                        $"Type '{typeSymbol.Name}' has no field '{field.Name}'",
                        field.LineStart, field.ColumnStart,
                        code: DiagnosticCodes.Semantic.PropertyPatternUnknownField,
                        span: field.Span);
                }
                else
                {
                    CheckPattern(field.Pattern, fieldTypes![fieldIndex]);
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
            // exactly as `case int() as n:` does (#1653). The type it binds with is the CLASSIFIED
            // one, identical to what `case list() as xs:` binds: erased on an `object` subject,
            // filled on a closed one, refused when disjoint (#1670).
            var selfMatchedType = ClassifyPatternClassTest(
                positionalPattern.Type, positionalPattern, scrutineeType, "positional pattern");
            if (selfMatchedType == null)
                return;
            CheckPattern(positionalPattern.Elements[0], selfMatchedType);
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
                _semanticInfo.SetPatternUnionCase(
                    positionalPattern, unionCaseSymbol, GetUnionSymbolAndTypeArgs(scrutineeType).TypeArgs);
                RecordUnionCaseHeadReference(
                    positionalPattern.Type,
                    new UserDefinedType { Name = unionCaseSymbol.Name, Symbol = unionCaseSymbol },
                    unionCaseSymbol);
            }
            else
            {
                var classifiedType = ClassifyPatternClassTest(
                    positionalPattern.Type, positionalPattern, scrutineeType, "positional pattern");
                if (classifiedType == null)
                    return;

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
        // Resolve the dotted path as the LONGEST type CHAIN followed by exactly one member (#1799).
        // Consume leading MODULE segments first (so `lib.Color.RED` works like `Color.RED` after
        // `from lib import Color`), reach a first type, then walk NESTED types (`Outer.Holder`,
        // `Outer.Mid.Holder`) as far as they go while always leaving one segment for the member —
        // depth-2+ chains reported SPY0203 before this walk existed (probes e01/e03/e05/e09) (#1524).
        var parts = memberAccess.Parts;
        TypeSymbol? typeSymbol = null;
        int typeIndex = 0;

        var firstSymbol = _symbolTable.Lookup(parts[0]);
        if (firstSymbol is TypeSymbol ts)
        {
            typeSymbol = ts;
            typeIndex = 0;
        }
        else if (firstSymbol is ModuleSymbol moduleSymbol)
        {
            // Walk module segments until we find a type.
            var current = moduleSymbol;
            for (int i = 1; i < parts.Length; i++)
            {
                if (current.Exports.TryGetValue(parts[i], out var exported))
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
                $"Undefined type '{parts[0]}' in pattern",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType,
                span: memberAccess.Span);
            return;
        }

        // Walk nested types (`Outer.Holder`, `Holder.Color`, `Outer.Mid.Holder`) as far as the next
        // segment names one, but always leave the FINAL segment for the member (const / enum member /
        // union case). Only advances on a genuine nested type, so a plain field-access chain
        // (`Type.field.subfield`) whose next part is not a nested type stops here unchanged.
        while (typeIndex + 1 < parts.Length - 1)
        {
            var nested = typeSymbol.NestedTypes.FirstOrDefault(n => n.Name == parts[typeIndex + 1]);
            if (nested == null)
                break;
            typeSymbol = nested;
            typeIndex++;
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
                _semanticInfo.SetPatternUnionCase(
                    memberAccess, caseSymbol, GetUnionSymbolAndTypeArgs(scrutineeType).TypeArgs);
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

        // Long form: "UnionName.CaseName" — the TypeAnnotation name includes the dot. Resolve the
        // type prefix (everything before the final segment) through the one dotted-name resolver so a
        // nested or module-qualified union is reached the same way every other type chain is (#1799);
        // fall back to the union's own simple name for the SYNTHETIC unions (Result/Optional), which
        // have no symbol-table entry the resolver could answer.
        if (typeName.Contains('.', StringComparison.Ordinal))
        {
            var lastDot = typeName.LastIndexOf('.');
            var typePart = typeName[..lastDot];
            var caseNamePart = typeName[(lastDot + 1)..];
            var resolvedPrefix = _typeResolver.ResolveDottedTypeName(typePart, escaped: false);
            if (resolvedPrefix == unionSymbol || typePart == unionSymbol.Name)
                return unionSymbol.UnionCases.FirstOrDefault(c => c.Name == caseNamePart);
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
        // A `T | None` (NullableType) union scrutinee is matched through the payload union's cases —
        // the None value is a separate arm (P13 D6). Strip the nullable once, exactly as the sequence
        // helper ResolveSequenceSubject does; without this a union-case head on `Tree[int] | None`
        // failed to resolve and reported SPY0202 "Unknown type 'Node'".
        if (scrutineeType is NullableType nullable)
        {
            return GetUnionSymbolAndTypeArgs(nullable.UnderlyingType);
        }

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
    /// <summary>
    /// The written element expressions of a tuple LITERAL value, or null for anything else —
    /// the per-element nodes the store seam needs to see a value's shape. Parentheses are
    /// transparent, as they are everywhere else the seam looks at a value.
    /// </summary>
    /// <summary>
    /// Re-records a tuple LITERAL's own type from the types its elements were just checked with.
    /// </summary>
    /// <remarks>
    /// A tuple literal is typed bottom-up before anything pushes a slot, so a bare <c>None</c>
    /// element types as <c>void</c> and the literal records <c>tuple[None, …]</c>. The unpacking
    /// path then re-checks each element UNDER its target's declared slot, which fixes the element
    /// nodes but leaves the literal's own recorded type stale — and that recorded type is what the
    /// emitter reads to declare its deconstruction temp, so a stale one prints
    /// <c>var __t = (null, 1)</c>: CS0815, "cannot assign null to an implicitly-typed variable"
    /// (#1707). ONE recomposition point, called by every unpacking shape — flat, nested and
    /// starred — so a fourth shape cannot arrive with its own copy or with none.
    /// </remarks>
    private void RecomposeTupleLiteralType(
        Expression? valueNode, IReadOnlyList<Expression>? elementNodes)
    {
        if (valueNode == null || elementNodes == null)
            return;
        if (AstHelper.UnwrapParenthesized(valueNode) is not TupleLiteral literal)
            return;

        var checkedTypes = new List<SemanticType>(elementNodes.Count);
        foreach (var node in elementNodes)
            checkedTypes.Add(_semanticInfo.GetExpressionType(node) ?? SemanticType.Unknown);

        _semanticInfo.SetExpressionType(literal, new TupleType { ElementTypes = checkedTypes });
    }

    private static IReadOnlyList<Expression>? TupleLiteralElements(Expression? value)
        => value != null && AstHelper.UnwrapParenthesized(value) is TupleLiteral literal
            && !literal.Elements.Any(e => e is SpreadElement)
            ? literal.Elements
            : null;

    /// <param name="valueNodes">
    /// The RHS element EXPRESSIONS, when the value is a tuple literal — the store seam's value-shape
    /// arms are properties of the written expression, not of its type, so without the node
    /// <c>a: int8; b: float32; a, b = 1, 2.5</c> was admitted type-wise and the emitter printed an
    /// unsuffixed <c>2.5</c> into a <c>float</c> slot (CS0029 behind SPY0908). Null when the value is
    /// a tuple-TYPED expression rather than a literal: there is no per-element node then, and the
    /// classification is type-only.
    /// </param>
    private void CheckTupleUnpackingElements(
        ImmutableArray<Expression> targets, IReadOnlyList<SemanticType> valueTypes,
        IReadOnlyList<Expression>? valueNodes = null)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var targetElem = targets[i];
            var valueElemType = valueTypes[i];
            var valueElemNode = valueNodes != null && i < valueNodes.Count ? valueNodes[i] : null;

            if (targetElem is Identifier tupleTargetId)
            {
                // Python class-scope rule (#1786, R-Y): an unpacking element is a store, refused
                // by name like the plain form. Without this the element declared a fresh local and
                // the program compiled silently.
                if (TryRefuseBareClassAttributeStore(
                        tupleTargetId.Name, tupleTargetId, BareStoreForm.TupleElement,
                        tupleTargetId.LineStart, tupleTargetId.ColumnStart, tupleTargetId.Span))
                {
                    continue;
                }

                RecordModuleAccessCrossingClassMember(tupleTargetId.Name, tupleTargetId);

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

                // A target that already has a declared binding is a STORE into it: the emitted C#
                // local keeps its declared type and the deconstruction assigns INTO that local, so
                // the slot is the declared type (#1706) and the element's value shape decides
                // (#1698, #1688, #1785). Without this, `a: int8; b: float32; a, b = 1, 2.5` was
                // admitted type-wise and the emitter printed an unsuffixed 2.5 (CS0029 behind
                // SPY0908), and a genuinely mistyped element (`a: int; a, b = "x", 3`) was the
                // same ICE.
                var storePredecessor = existingSymbol as VariableSymbol;
                var declaredSlot = storePredecessor != null
                    ? DeclaredBindingType(storePredecessor)
                    : (SemanticType?)null;

                // Check the element from its source node when available — the caller may have
                // skipped the whole-tuple check for a tuple-literal RHS (#1785, #1707). When a
                // declared slot exists, push it via EnterStore so Some(5)/None() self-type and
                // value-shape arms (constant narrowing, float32) classify against it.
                SemanticType elementType;
                if (valueElemNode != null && declaredSlot is not null and not UnknownType)
                {
                    using (EnterStore(StorePosition.TupleElement, declaredSlot, valueElemNode))
                        elementType = CheckExpression(valueElemNode);
                }
                else if (valueElemNode != null && valueElemType is UnknownType)
                {
                    // New variable (no declared slot) and the caller provided placeholder types:
                    // check the source node without a slot push.
                    elementType = CheckExpression(valueElemNode);
                }
                else
                {
                    elementType = valueElemType;
                }

                if (storePredecessor != null && declaredSlot is not null and not UnknownType
                    && elementType is not UnknownType
                    && !IsAssignable(elementType, declaredSlot))
                {
                    // R-T: when the target is narrowed and the declared type is a wrapper,
                    // classify against the payload first — a payload-accepted value re-wraps
                    // (mirroring CheckAssignment's R-T arm at Statements.cs:170-201).
                    var payloadType = declaredSlot is OptionalType opt ? opt.UnderlyingType
                        : declaredSlot is NullableType { IsValueType: true } nt ? nt.UnderlyingType
                        : (SemanticType?)null;

                    if (payloadType != null
                        && HasRemoveNoneFact(tupleTargetId.Name)
                        && (IsAssignable(elementType, payloadType)
                            || IsAcceptedVerdict(ClassifyStore(
                                StorePosition.TupleElement, valueElemNode, elementType, payloadType))))
                    {
                        if (declaredSlot is OptionalType wrapOpt)
                            _semanticInfo.SetOptionalStoreWrap(valueElemNode!, wrapOpt);
                        elementType = ClassifyStore(
                            StorePosition.TupleElement, valueElemNode, elementType, payloadType) switch
                        {
                            StoreVerdict.AcceptedFloat32Narrowing => SemanticType.Float32,
                            StoreVerdict.AcceptedDecimalNarrowing => SemanticType.Decimal,
                            _ => payloadType,
                        };
                    }
                    else
                    {
                        // Seam-coded refusal: the seam's CheckStore produces the correct
                        // diagnostic (SPY0604 for strict Optional, SPY0229 for None into
                        // non-nullable) instead of a generic SPY0220 (#1785).
                        if (!CheckStore(StorePosition.TupleElement, valueElemNode, elementType,
                                declaredSlot, targetElem, targetElem.Span))
                        {
                            continue;
                        }
                        elementType = ClassifyStore(
                            StorePosition.TupleElement, valueElemNode, elementType, declaredSlot) switch
                        {
                            StoreVerdict.AcceptedFloat32Narrowing => SemanticType.Float32,
                            StoreVerdict.AcceptedDecimalNarrowing => SemanticType.Decimal,
                            _ => declaredSlot,
                        };
                    }
                }

                // When None stores into a nullable slot, the TUPLE's recorded type must carry the
                // slot type (not Void) so the emitter's temp uses an explicit type instead of var —
                // `var __t = (null, 1)` is CS0815 (#1707).
                if (valueElemNode != null && elementType is VoidType
                    && declaredSlot is NullableType)
                {
                    _semanticInfo.SetExpressionType(valueElemNode, declaredSlot);
                }

                // A fresh element with no declared slot and a void value goes through
                // BestCommonType: `a, b = None, 1` refuses at `a` (R-AB, #1812).
                if (existingSymbol == null && declaredSlot is null or UnknownType
                    && elementType is VoidType)
                {
                    elementType = BestCommonType(
                        new[] { (valueElemNode, elementType) },
                        null, StorePosition.TupleElement, tupleTargetId,
                        $"'{tupleTargetId.Name}'",
                        new BestCommonTypeOptions(
                            AnnotateSteer: $"'{tupleTargetId.Name}: T = ...'",
                            NoneAnnotateSteer: BindingNoneSteer(tupleTargetId.Name)));
                }

                // In Sharpy, tuple unpacking creates new variable versions
                // Create/redefine with inferred type from tuple element
                var newSymbol = new VariableSymbol
                {
                    Name = tupleTargetId.Name,
                    Kind = SymbolKind.Variable,
                    Type = elementType,
                    IsConstant = false,
                    DeclarationLine = tupleTargetId.LineStart,
                    DeclarationColumn = tupleTargetId.ColumnStart,
                    NameDeclarationLine = tupleTargetId.LineStart,
                    NameDeclarationColumn = tupleTargetId.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(newSymbol);
                SemanticBinding.SetVariableType(newSymbol, elementType);
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

                _semanticInfo.SetExpressionType(tupleTargetId, elementType);
                if (elementType is UnknownType)
                {
                    MarkExpressionAsErrorRecovery(tupleTargetId,
                        ErrorRecoveryReason.Propagated("the matched tuple element's type"));
                }
            }
            else if (targetElem is TupleLiteral nestedTuple)
            {
                // Nested tuple unpacking: (a, b), c = expr — routed through the ONE unpacking rule so
                // a nested star `(a, (b, *c)) = …` and a nested arity/non-tuple refusal are the same
                // check as the top level, at every depth (#1846). When the RHS element is a tuple
                // literal, the routine checks each of its rows under the nested targets' declared
                // slots (so `(x, y), n = (None, "b"), 1` types the None under `x: str | None`, #1707)
                // and recomposes the inner literal's own type; otherwise it binds against the source
                // type the caller derived.
                CheckUnpackingTargets(nestedTuple.Elements, valueElemType,
                    UnpackingPosition.Assignment,
                    targetElem.LineStart, targetElem.ColumnStart, targetElem.Span,
                    valueNode: valueElemNode, nested: true);
            }
            else
            {
                // For more complex targets (like attributes), just check type compatibility.
                // An index-access element (`b[k], y = …`) is checked in STORE position (#1620).
                // An attribute element is a plain store: declared member type, no read narrowing (#1706).
                SemanticType targetElemType;
                using (ScopedValue.Push(ref _indexStoreTarget, IndexStoreTarget.Of(targetElem)))
                using (ScopedValue.Push(ref _plainStoreTarget, targetElem))
                    targetElemType = CheckExpression(targetElem);

                // Push the target's type and re-check so value-shape arms classify correctly
                // (#1785). Same slot push the identifier arm does for its declared type.
                SemanticType sourceElemType;
                if (valueElemNode != null && targetElemType is not UnknownType)
                {
                    using (EnterStore(StorePosition.TupleElement, targetElemType, valueElemNode))
                        sourceElemType = CheckExpression(valueElemNode);
                }
                else if (valueElemNode != null && valueElemType is UnknownType)
                {
                    sourceElemType = CheckExpression(valueElemNode);
                }
                else
                {
                    sourceElemType = valueElemType;
                }

                // Seam-coded refusal: CheckStore produces the correct diagnostic (SPY0604,
                // SPY0229, etc.) instead of a generic SPY0220 (#1785).
                if (sourceElemType is not UnknownType && targetElemType is not UnknownType
                    && !IsAssignable(sourceElemType, targetElemType))
                {
                    CheckStore(StorePosition.TupleElement, valueElemNode, sourceElemType,
                        targetElemType, targetElem, targetElem.Span);
                }
                else if (sourceElemType is not UnknownType && targetElemType is not UnknownType)
                {
                    // Assignable — still apply verdict side effects (constant narrowing etc.)
                    CheckStoreQuietly(
                        StorePosition.TupleElement, valueElemNode, sourceElemType, targetElemType);
                }

                // When None stores into a nullable slot, update for the tuple's recorded type
                // (#1707, same as identifier arm).
                if (valueElemNode != null && sourceElemType is VoidType
                    && targetElemType is NullableType)
                {
                    _semanticInfo.SetExpressionType(valueElemNode, targetElemType);
                }
            }
        }
    }

    /// <summary>
    /// Type-checks star unpacking patterns: first, *rest = items
    /// The RHS can be a list[T] or tuple[...].
    /// </summary>
    /// <summary>
    /// Checks each RHS element of a starred unpacking under the DECLARED slot of the target it
    /// lands on, and returns the checked element types in RHS order.
    /// </summary>
    /// <remarks>
    /// The mapping is positional and the star absorbs the middle: the first
    /// <paramref name="targetsBefore"/> values belong to the targets before the star, the last
    /// <paramref name="targetsAfter"/> to the targets after it, and everything between goes into
    /// the star's list and is checked with no slot.
    /// </remarks>
    private IReadOnlyList<SemanticType> CheckStarValueElements(
        ImmutableArray<Expression> targets, IReadOnlyList<Expression> valueNodes,
        int targetsBefore, int targetsAfter)
    {
        var types = new List<SemanticType>(valueNodes.Count);

        for (int i = 0; i < valueNodes.Count; i++)
        {
            var target = StarTargetForValueIndex(targets, valueNodes.Count, i, targetsBefore, targetsAfter);
            var slot = target is Identifier id
                && (_symbolTable.Lookup(id.Name, searchParents: false) as VariableSymbol
                    ?? _symbolTable.Lookup(id.Name, searchParents: true) as VariableSymbol)
                    is { } predecessor
                ? DeclaredBindingType(predecessor)
                : null;

            SemanticType elementType;
            if (slot is not null and not UnknownType)
            {
                using (EnterStore(StorePosition.TupleElement, slot, valueNodes[i]))
                    elementType = CheckExpression(valueNodes[i]);

                // A bare None into a nullable slot carries the SLOT as its recorded type, so the
                // emitter's temp is typed rather than `var __t = (null, …)` — the same arm the flat
                // path applies (#1707).
                if (elementType is VoidType && slot is NullableType)
                {
                    _semanticInfo.SetExpressionType(valueNodes[i], slot);
                    elementType = slot;
                }
            }
            else
            {
                elementType = CheckExpression(valueNodes[i]);

                // No declared slot: this value decides a FRESH name, so it goes through the same
                // single-operand seam the flat unpacking path uses. An untyped operand (bare
                // `None`, a void call) is refused by name here instead of being recorded as
                // `void` and reaching the emitter — `a, *rest = None, 1, 2` was SPY0599
                // "Keyword 'void' cannot be used in this context" (#1812, #1796).
                var boundName = target is Identifier targetId ? targetId.Name : "rest";
                var siteNoun = target is Identifier named
                    ? $"'{named.Name}'"
                    : $"starred element {i + 1}";
                elementType = BestCommonType(
                    new[] { ((Expression?)valueNodes[i], elementType) },
                    null, StorePosition.TupleElement, valueNodes[i], siteNoun,
                    new BestCommonTypeOptions(
                        AnnotateSteer: $"'{boundName}: T = ...'",
                        NoneAnnotateSteer: BindingNoneSteer(boundName)));
            }

            types.Add(elementType);
        }

        return types;
    }


    /// <summary>
    /// The non-star target a starred unpacking's value at <paramref name="valueIndex"/> lands on,
    /// or null when it falls inside the star's span.
    /// </summary>
    private static Expression? StarTargetForValueIndex(
        ImmutableArray<Expression> targets, int valueCount, int valueIndex, int targetsBefore, int targetsAfter)
    {
        if (valueIndex < targetsBefore)
            return targets[valueIndex];

        int fromEnd = valueCount - valueIndex;
        if (fromEnd <= targetsAfter)
            return targets[targets.Length - fromEnd];

        return null;
    }

    /// <summary>
    /// The type a non-star target takes when it has no declared binding: its OWN element's checked
    /// type when the RHS was a literal, else the one type derived for the star's list.
    /// </summary>
    private static SemanticType NonStarTargetType(
        ImmutableArray<Expression> targets, Identifier target,
        IReadOnlyList<SemanticType>? perElement, SemanticType fallback)
    {
        if (perElement == null)
            return fallback;

        int starPosition = -1;
        int targetIndex = -1;
        for (int i = 0; i < targets.Length; i++)
        {
            if (starPosition < 0 && targets[i] is StarExpression)
                starPosition = i;
            if (targetIndex < 0 && ReferenceEquals(targets[i], target))
                targetIndex = i;
        }
        if (targetIndex < 0)
            return fallback;

        if (starPosition < 0 || targetIndex < starPosition)
            return targetIndex < perElement.Count ? perElement[targetIndex] : fallback;

        int fromEnd = targets.Length - targetIndex;
        int valueIndex = perElement.Count - fromEnd;
        return valueIndex >= 0 && valueIndex < perElement.Count ? perElement[valueIndex] : fallback;
    }

}
