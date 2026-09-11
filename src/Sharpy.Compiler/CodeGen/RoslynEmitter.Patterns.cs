using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Sharpy.Compiler.CodeGen.EmittedTreePrecedence;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Pattern matching code generation
/// </summary>
internal partial class RoslynEmitter
{
    private const string PatternMatchTempPrefix = "__spy_pm_";

    /// <summary>A generated match arm: its pattern, its guard and the guard's hoisted statements.</summary>
    private readonly record struct GeneratedMatchArm(
        PatternSyntax Pattern,
        ExpressionSyntax? Guard,
        List<StatementSyntax> GuardEvaluations,
        bool IsWildcardWithoutGuard,
        List<StatementSyntax> Body);

    private StatementSyntax GenerateMatch(MatchStatement matchStmt)
    {
        var scrutineeExpr = GenerateExpression(matchStmt.Scrutinee);
        if (_context.SemanticInfo?.GetMatchScrutineeLowering(matchStmt.Scrutinee) is
            { Kind: MatchScrutineeLoweringKind.CastToNullableObject })
        {
            scrutineeExpr = Cast(
                NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
                scrutineeExpr);
        }

        var scrutineeType = _context.SemanticInfo?.GetExpressionType(matchStmt.Scrutinee);

        var arms = new List<GeneratedMatchArm>(matchStmt.Cases.Length);

        foreach (var matchCase in matchStmt.Cases)
        {
            // Collect all MemberAccessPattern guards (including nested in tuples).
            // matchVarCounter resets per case arm — each switch section is an independent
            // scope in C#, so __spy_pm_0, __spy_pm_1 etc. can safely repeat across arms.
            var memberGuards = new List<ExpressionSyntax>();
            int matchVarCounter = 0;
            var pattern = GenerateMatchPattern(matchCase.Pattern, memberGuards, ref matchVarCounter, scrutineeType);

            // A guard is evaluated only for its own arm, and only when the pattern matched, so it
            // owns an evaluation sink (plan-0667c5 Design Decision 6). A `when` clause hosts an
            // expression and cannot host statements, so an arm whose guard hoists forces the
            // `is`-chain lowering below.
            ExpressionSyntax? combinedGuard = null;
            var guardEvaluations = WithEvaluationSink(
                () => combinedGuard = CombineGuards(memberGuards, matchCase.Guard));

            // Generate body AFTER pattern — pattern registration in _variableVersions
            // must precede body generation so f-strings and other references see the
            // correct mangled variable names.
            var bodyStatements = GenerateSuite(matchCase.Body).ToList();

            arms.Add(new GeneratedMatchArm(
                pattern,
                combinedGuard,
                guardEvaluations,
                matchCase.Pattern is WildcardPattern && combinedGuard == null,
                bodyStatements));
        }

        // If the match is semantically exhaustive (covers all cases of a finite type)
        // but has no wildcard/default case, add a default throw to satisfy the C# compiler's
        // definite return analysis. This is unreachable at runtime.
        bool hasDefault = matchStmt.Cases.Any(c =>
            c.Guard == null && ExhaustivenessHelper.IsIrrefutable(c.Pattern, _context.SemanticInfo));
        bool needsUnreachableDefault = !hasDefault && scrutineeType != null && _context.SemanticInfo != null
            && ExhaustivenessHelper.IsExhaustiveMatch(
                scrutineeType,
                matchStmt.Cases.Select(c => (c.Pattern, c.Guard)),
                _context.SemanticInfo);

        if (arms.Any(a => a.GuardEvaluations.Count > 0))
        {
            return GenerateMatchAsIsChain(scrutineeExpr, arms, needsUnreachableDefault);
        }

        return GenerateMatchAsSwitch(scrutineeExpr, arms, needsUnreachableDefault);
    }

    /// <summary>
    /// The default lowering: one C# <c>switch</c> section per arm, guards in <c>when</c> clauses.
    /// Used whenever no guard hoists — C#'s <c>when</c> already evaluates a guard once, per arm,
    /// only after its pattern matched, and the <c>switch</c> keeps C#'s own exhaustiveness and
    /// definite-assignment reasoning.
    /// </summary>
    private StatementSyntax GenerateMatchAsSwitch(
        ExpressionSyntax scrutineeExpr, List<GeneratedMatchArm> arms, bool needsUnreachableDefault)
    {
        var sections = new List<SwitchSectionSyntax>(arms.Count + 1);

        foreach (var arm in arms)
        {
            var bodyStatements = new List<StatementSyntax>(arm.Body);

            // Only add break if the last statement isn't an unconditional jump
            var lastStatement = bodyStatements.LastOrDefault();
            if (lastStatement is not (ReturnStatementSyntax or ThrowStatementSyntax
                or BreakStatementSyntax or ContinueStatementSyntax)
                && !(lastStatement is YieldStatementSyntax { ReturnOrBreakKeyword.RawKind: (int)SyntaxKind.BreakKeyword }))
            {
                bodyStatements.Add(BreakStatement());
            }

            SwitchLabelSyntax caseLabel = arm.IsWildcardWithoutGuard
                ? DefaultSwitchLabel()
                : arm.Guard != null
                    ? CasePatternSwitchLabel(arm.Pattern, WhenClause(arm.Guard), Token(SyntaxKind.ColonToken))
                    : CasePatternSwitchLabel(arm.Pattern, Token(SyntaxKind.ColonToken));

            sections.Add(SwitchSection(
                SingletonList(caseLabel),
                List<StatementSyntax>(bodyStatements)));
        }

        if (needsUnreachableDefault)
        {
            sections.Add(SwitchSection(
                SingletonList<SwitchLabelSyntax>(DefaultSwitchLabel()),
                SingletonList<StatementSyntax>(BuildUnreachableExhaustiveMatchThrow())));
        }

        return SwitchStatement(scrutineeExpr, List(sections));
    }

    /// <summary>
    /// The <c>is</c>-chain lowering (plan-0667c5 Design Decision 6, #1739 acceptance cells 8-10),
    /// used when at least one guard produces hoisted statements. A <c>when</c> clause is an
    /// expression position, so a comprehension/spread/<c>?</c> in a guard had to be hoisted above
    /// the whole <c>switch</c> — where it ran once for the match rather than once per arm reached,
    /// and ran even when no arm's pattern matched. Emitted shape:
    /// <code>
    /// var __matchSubject_0 = &lt;scrutinee&gt;;
    /// bool __matchTaken_0 = false;
    /// if (!__matchTaken_0 &amp;&amp; __matchSubject_0 is P1 p1)
    /// {
    ///     &lt;guard 1 hoists&gt;
    ///     if (&lt;guard 1&gt;) { __matchTaken_0 = true; &lt;body 1&gt; }
    /// }
    /// if (!__matchTaken_0 &amp;&amp; __matchSubject_0 is P2 p2) { __matchTaken_0 = true; &lt;body 2&gt; }
    /// </code>
    /// <para>The flag is set BEFORE the body so that a body ending in <c>return</c>/<c>break</c>
    /// leaves no unreachable statement (warnings are errors). No loop and no <c>switch</c> is
    /// manufactured, so a <c>break</c> in an arm body targets the enclosing Python loop — which is
    /// what Python means and what the <c>switch</c> form gets wrong (#1816).</para>
    /// </summary>
    private StatementSyntax GenerateMatchAsIsChain(
        ExpressionSyntax scrutineeExpr, List<GeneratedMatchArm> arms, bool needsUnreachableDefault)
    {
        var subjectName = GenerateTempVarName("matchSubject");
        var takenName = GenerateTempVarName("matchTaken");

        var statements = new List<StatementSyntax>(arms.Count + 3);

        statements.Add(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(subjectName))
                        .WithInitializer(EqualsValueClause(scrutineeExpr))))));

        statements.Add(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(takenName))
                        .WithInitializer(EqualsValueClause(
                            LiteralExpression(SyntaxKind.FalseLiteralExpression)))))));

        var notTaken = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
            EscapedIdentifierName(takenName));
        var markTaken = ExpressionStatement(
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                EscapedIdentifierName(takenName),
                LiteralExpression(SyntaxKind.TrueLiteralExpression)));

        foreach (var arm in arms)
        {
            // The arm's own test: `!taken` for a bare wildcard, otherwise `!taken && subject is P`.
            ExpressionSyntax armTest = arm.IsWildcardWithoutGuard
                ? notTaken
                : BinaryExpression(SyntaxKind.LogicalAndExpression,
                    notTaken,
                    ParenthesizedExpression(IsPatternExpression(
                        EscapedIdentifierName(subjectName), arm.Pattern)));

            // The taken body: mark first, then the arm's statements.
            var takenBody = new List<StatementSyntax>(arm.Body.Count + 1) { markTaken };
            takenBody.AddRange(arm.Body);

            if (arm.Guard == null)
            {
                statements.Add(IfStatement(armTest, Block(takenBody)));
                continue;
            }

            // The guard's hoists run inside the pattern-matched block, before the guard itself,
            // so they execute exactly once and only for an arm whose pattern matched.
            var patternMatchedBody = new List<StatementSyntax>(arm.GuardEvaluations.Count + 1);
            patternMatchedBody.AddRange(arm.GuardEvaluations);
            patternMatchedBody.Add(IfStatement(arm.Guard, Block(takenBody)));

            statements.Add(IfStatement(armTest, Block(patternMatchedBody)));
        }

        if (needsUnreachableDefault)
        {
            statements.Add(IfStatement(notTaken, Block(BuildUnreachableExhaustiveMatchThrow())));
        }

        return Block(statements);
    }

    /// <summary>
    /// <c>throw new System.InvalidOperationException("Unreachable: exhaustive match")</c> — the
    /// fall-through arm an exhaustive match needs so C#'s definite-return analysis accepts a
    /// function whose every arm returns. Unreachable at runtime.
    /// </summary>
    private static StatementSyntax BuildUnreachableExhaustiveMatchThrow()
    {
        return ThrowStatement(
            ObjectCreationExpression(
                QualifiedName(
                    IdentifierName("System"),
                    IdentifierName("InvalidOperationException")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    Literal("Unreachable: exhaustive match")))))));
    }

    private ExpressionSyntax GenerateMemberAccessValue(MemberAccessPattern memberAccess)
    {
        // Read the resolution the TypeChecker recorded: the resolved TypeSymbol and the
        // index of the type segment in the Parts array (#1524, Rule 2). No lookup fallback:
        // CheckMemberAccessPattern records unconditionally on success and errors otherwise,
        // so a missing entry here means the pattern was never checked (or a new SemanticInfo
        // dictionary missed MergeFrom) — surface that loudly instead of re-deriving.
        var resolution = _context.SemanticInfo?.GetPatternMemberAccessResolution(memberAccess)
            ?? throw new InvalidOperationException(
                $"No recorded resolution for member-access pattern "
                + $"'{string.Join(".", memberAccess.Parts)}' — semantic analysis must record "
                + "every pattern the emitter is asked to generate (#1524)");
        var typeSymbol = resolution.TypeSymbol;
        int typeIndex = resolution.TypeIndex;

        var enumSymbol = typeSymbol?.TypeKind == TypeKind.Enum ? typeSymbol : null;

        // Build the type name. For module-qualified patterns (typeIndex > 0), use the
        // TypeSyntaxMapper to emit the full declaring chain with namespace prefix.
        ExpressionSyntax expr;
        if (typeSymbol != null && typeIndex > 0)
        {
            var mappedType = _typeMapper.MapSemanticType(
                new Semantic.UserDefinedType { Name = typeSymbol.Name, Symbol = typeSymbol });
            expr = mappedType is NameSyntax nameSyntax
                ? nameSyntax
                : IdentifierName(NameMangler.Transform(typeSymbol.Name, NameContext.Type));
        }
        else
        {
            expr = IdentifierName(
                NameMangler.Transform(memberAccess.Parts[0], NameContext.Type));
        }

        for (int i = typeIndex + 1; i < memberAccess.Parts.Length; i++)
        {
            expr = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expr,
                enumSymbol != null
                    ? EnumMemberIdentifier(enumSymbol, memberAccess.Parts[i])
                    : IdentifierName(NameMangler.Transform(memberAccess.Parts[i], NameContext.Field)));
        }

        return expr;
    }

    private PatternSyntax GenerateMatchPattern(
        Pattern pattern,
        List<ExpressionSyntax> memberGuards,
        ref int matchVarCounter,
        SemanticType? scrutineeType = null)
    {
        switch (pattern)
        {
            case WildcardPattern:
                return VarPattern(DiscardDesignation());

            case BindingPattern binding:
                {
                    // RFC 3535: If this binding was resolved as a constant, emit as constant pattern
                    var constSymbol = _context.SemanticInfo?.GetPatternConstantSymbol(binding);
                    if (constSymbol != null)
                    {
                        var constName = GetCodeGenInfo(constSymbol)?.CSharpName
                            ?? NameMangler.ToConstantCase(constSymbol.Name);
                        return ConstantPattern(IdentifierName(constName));
                    }

                    // #1562: union-resolved binding emits a variant type test
                    var bindingUnionCase = _context.SemanticInfo?.GetPatternUnionCase(binding);
                    if (bindingUnionCase != null)
                    {
                        var caseTypeSyntax = BuildUnionCaseTypeSyntax(bindingUnionCase, scrutineeType);
                        return DeclarationPattern(caseTypeSyntax, DiscardDesignation());
                    }

                    var varName = GetMangledVariableName(binding.Name, isNewDeclaration: true);
                    return VarPattern(SingleVariableDesignation(Identifier(varName)));
                }

            case LiteralPattern literal:
                {
                    // Handle None()/None patterns on Optional scrutinees
                    var litUnionCase = _context.SemanticInfo?.GetPatternUnionCase(literal);
                    if (litUnionCase?.Name == "None" && scrutineeType is OptionalType)
                    {
                        // Optional<T>.Deconstruct(out bool hasValue, out T value)
                        // None → (false, _)
                        return RecursivePattern()
                            .WithPositionalPatternClause(
                                PositionalPatternClause(SeparatedList(new[]
                                {
                                    Subpattern(ConstantPattern(LiteralExpression(SyntaxKind.FalseLiteralExpression))),
                                    Subpattern(VarPattern(DiscardDesignation()))
                                })));
                    }

                    // For Str scrutinees (cast to string), emit raw string literals
                    // so they serve as valid constant patterns in C# switch.
                    if (scrutineeType == SemanticType.Str && literal.Literal is StringLiteral strLit)
                    {
                        return ConstantPattern(
                            LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(strLit.Value)));
                    }

                    // For None against a non-Optional scrutinee (e.g. object), emit
                    // `null` so the C# constant pattern is valid (avoids CS8505 from `default`).
                    if (literal.Literal is NoneLiteral)
                    {
                        return ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression));
                    }

                    var literalExpr = GenerateExpression(literal.Literal);
                    return ConstantPattern(literalExpr);
                }

            case TuplePattern tuplePattern:
                {
                    var subPatterns = new SubpatternSyntax[tuplePattern.Elements.Length];
                    for (int i = 0; i < tuplePattern.Elements.Length; i++)
                    {
                        subPatterns[i] = Subpattern(GenerateMatchPattern(
                            tuplePattern.Elements[i], memberGuards, ref matchVarCounter));
                    }
                    return RecursivePattern()
                        .WithPositionalPatternClause(
                            PositionalPatternClause(SeparatedList(subPatterns)));
                }

            case ListPattern listPattern:
                {
                    var elementPatterns = new List<PatternSyntax>();
                    foreach (var element in listPattern.Elements)
                    {
                        if (element is StarPattern star)
                        {
                            // *rest / *_ → C# slice pattern (.. [var rest]); bare * → ..
                            var slice = SlicePattern();
                            if (star.Capture != null)
                            {
                                slice = slice.WithPattern(GenerateMatchPattern(
                                    star.Capture, memberGuards, ref matchVarCounter));
                            }
                            elementPatterns.Add(slice);
                        }
                        else
                        {
                            elementPatterns.Add(GenerateMatchPattern(
                                element, memberGuards, ref matchVarCounter));
                        }
                    }
                    return SyntaxFactory.ListPattern(SeparatedList(elementPatterns));
                }

            case AndPattern andPattern:
                {
                    var leftPattern = GenerateMatchPattern(
                        andPattern.Left, memberGuards, ref matchVarCounter, scrutineeType);
                    var rightPattern = GenerateMatchPattern(
                        andPattern.Right, memberGuards, ref matchVarCounter, scrutineeType);
                    return BinaryPattern(SyntaxKind.AndPattern, leftPattern, rightPattern);
                }

            case TypePattern typePattern:
                return GenerateTypePattern(typePattern, DiscardDesignation(), scrutineeType);

            case RelationalPattern relational:
                {
                    var operatorToken = relational.Operator switch
                    {
                        RelationalOperator.GreaterThan => Token(SyntaxKind.GreaterThanToken),
                        RelationalOperator.GreaterThanOrEqual => Token(SyntaxKind.GreaterThanEqualsToken),
                        RelationalOperator.LessThan => Token(SyntaxKind.LessThanToken),
                        RelationalOperator.LessThanOrEqual => Token(SyntaxKind.LessThanEqualsToken),
                        _ => throw new System.InvalidOperationException(
                            $"Unexpected relational operator: {relational.Operator}")
                    };
                    var valueExpr = GenerateExpression(relational.Value);
                    return RelationalPattern(operatorToken, valueExpr);
                }

            case OrPattern orPattern:
                {
                    // Check if any alternative is a non-union MemberAccessPattern (needs guard-based approach)
                    bool hasNonUnionMemberAccess = orPattern.Alternatives.Any(a =>
                        a is MemberAccessPattern ma
                        && _context.SemanticInfo?.GetPatternUnionCase(ma) == null);

                    if (hasNonUnionMemberAccess)
                    {
                        // Use var binding + combined when guard with ||
                        var tempVarName = $"{PatternMatchTempPrefix}{matchVarCounter++}";
                        ExpressionSyntax? orGuard = null;
                        foreach (var alt in orPattern.Alternatives)
                        {
                            ExpressionSyntax comparison;
                            if (alt is MemberAccessPattern ma)
                            {
                                comparison = Binary(
                                    SyntaxKind.EqualsExpression,
                                    IdentifierName(tempVarName),
                                    GenerateMemberAccessValue(ma));
                            }
                            else if (alt is WildcardPattern)
                            {
                                // Wildcard in mixed or-pattern makes it match anything — skip guard
                                orGuard = null;
                                break;
                            }
                            else if (alt is LiteralPattern litPat)
                            {
                                // For literals in mixed or-patterns, generate equality comparison
                                var altExpr = GenerateExpression(litPat.Literal);
                                comparison = Binary(
                                    SyntaxKind.EqualsExpression,
                                    IdentifierName(tempVarName),
                                    altExpr);
                            }
                            else
                            {
                                _context.AddError(
                                    $"Unsupported pattern type '{alt.GetType().Name}' in mixed or-pattern",
                                    DiagnosticCodes.CodeGen.UnsupportedFeature,
                                    alt.LineStart, alt.ColumnStart);
                                continue;
                            }
                            orGuard = orGuard == null
                                ? comparison
                                : Binary(SyntaxKind.LogicalOrExpression, orGuard, comparison);
                        }
                        if (orGuard != null)
                            memberGuards.Add(orGuard);
                        return VarPattern(SingleVariableDesignation(Identifier(tempVarName)));
                    }

                    // Or-pattern where every alternative is `as name`: strip the `as` wrappers,
                    // build the or from the inner patterns, and bind once with `and var name`.
                    // C# does not allow variable designations inside `or` patterns.
                    if (orPattern.Alternatives.All(a => a is AsPattern))
                    {
                        var firstAs = (AsPattern)orPattern.Alternatives[0];
                        var asVarName = GetMangledVariableName(firstAs.Name, isNewDeclaration: true);
                        PatternSyntax orResult = GenerateMatchPattern(
                            firstAs.Inner, memberGuards, ref matchVarCounter, scrutineeType);
                        for (int i = 1; i < orPattern.Alternatives.Length; i++)
                        {
                            var innerAlt = ((AsPattern)orPattern.Alternatives[i]).Inner;
                            var rightInner = GenerateMatchPattern(
                                innerAlt, memberGuards, ref matchVarCounter, scrutineeType);
                            orResult = BinaryPattern(SyntaxKind.OrPattern, orResult, rightInner);
                        }
                        return BinaryPattern(SyntaxKind.AndPattern,
                            ParenthesizedPattern(orResult),
                            VarPattern(SingleVariableDesignation(Identifier(asVarName))));
                    }

                    // Simple or-pattern (including union case or-patterns): use C# `or` pattern syntax
                    PatternSyntax result = GenerateMatchPattern(
                        orPattern.Alternatives[0], memberGuards, ref matchVarCounter, scrutineeType);
                    for (int i = 1; i < orPattern.Alternatives.Length; i++)
                    {
                        var right = GenerateMatchPattern(
                            orPattern.Alternatives[i], memberGuards, ref matchVarCounter, scrutineeType);
                        result = BinaryPattern(SyntaxKind.OrPattern, result, right);
                    }
                    return result;
                }

            case AsPattern asPattern:
                {
                    var varName = GetMangledVariableName(asPattern.Name, isNewDeclaration: true);
                    var designation = SingleVariableDesignation(Identifier(varName));
                    if (asPattern.Inner is TypePattern tp)
                    {
                        return GenerateTypePattern(tp, designation, scrutineeType);
                    }
                    var asInnerPattern = GenerateMatchPattern(
                        asPattern.Inner, memberGuards, ref matchVarCounter, scrutineeType);
                    // `as` scopes over the whole or-pattern (PEP 634, #1663), and C#'s `and` binds
                    // tighter than `or`: `A or B and var w` would re-associate as `A or (B and var w)`
                    // and leave `w` unassigned on the A arm (CS0165). Parenthesize the inner.
                    if (asInnerPattern.RawKind == (int)SyntaxKind.OrPattern)
                        asInnerPattern = ParenthesizedPattern(asInnerPattern);
                    return BinaryPattern(SyntaxKind.AndPattern,
                        asInnerPattern,
                        VarPattern(designation));
                }

            case GuardPattern guardPattern:
                {
                    var innerPattern = GenerateMatchPattern(
                        guardPattern.Inner, memberGuards, ref matchVarCounter, scrutineeType);
                    var guardExpr = GenerateExpression(guardPattern.Guard);
                    memberGuards.Add(guardExpr);
                    return innerPattern;
                }

            case MemberAccessPattern memberAccess:
                {
                    // Check if this is a union case pattern (e.g., Option.None)
                    var unionCase = _context.SemanticInfo?.GetPatternUnionCase(memberAccess);
                    if (unionCase != null)
                    {
                        var caseTypeSyntax = BuildUnionCaseTypeSyntax(unionCase, scrutineeType);
                        return DeclarationPattern(caseTypeSyntax, DiscardDesignation());
                    }

                    // Bind to a named variable and add a when-clause guard for equality.
                    // This handles both top-level and nested (e.g., inside TuplePattern) cases.
                    var tempVarName = $"{PatternMatchTempPrefix}{matchVarCounter++}";
                    var memberValue = GenerateMemberAccessValue(memberAccess);
                    memberGuards.Add(Binary(
                        SyntaxKind.EqualsExpression,
                        IdentifierName(tempVarName),
                        memberValue));
                    return VarPattern(SingleVariableDesignation(Identifier(tempVarName)));
                }

            case PropertyPattern propertyPattern:
                {
                    // Same read as every other class-pattern arm (#1670): the union case when the
                    // checker resolved one, otherwise the recorded type-test lowering.
                    var propertyUnionCase = _context.SemanticInfo?.GetPatternUnionCase(propertyPattern);
                    var typeSyntax = propertyUnionCase != null
                        ? BuildUnionCaseTypeSyntax(propertyUnionCase, scrutineeType)
                        : propertyPattern.Type != null
                            ? PatternTestTypeSyntax(propertyPattern, propertyPattern.Type)
                            : null;
                    var subPatterns = new List<SubpatternSyntax>();
                    foreach (var field in propertyPattern.Fields)
                    {
                        var fieldName = NameMangler.Transform(field.Name, NameContext.Field);
                        var subPattern = GenerateMatchPattern(field.Pattern, memberGuards, ref matchVarCounter);
                        subPatterns.Add(Subpattern(subPattern)
                            .WithNameColon(NameColon(EscapedIdentifierName(fieldName))));
                    }
                    var recursivePattern = RecursivePattern()
                        .WithPropertyPatternClause(
                            PropertyPatternClause(SeparatedList(subPatterns)));
                    if (typeSyntax != null)
                        recursivePattern = recursivePattern.WithType(typeSyntax);
                    return recursivePattern;
                }

            case PositionalPattern positionalPattern:
                {
                    if (positionalPattern.Type != null
                        && SelfMatchingBuiltins.IsSelfMatching(positionalPattern.Type.Name)
                        && positionalPattern.Elements.Length == 1)
                    {
                        // All three sub-arms test the SAME type, so all three read the same recorded
                        // lowering (#1670). Reading GetPatternType here instead emitted the erased
                        // CAPTURE type `Sharpy.List<object>` where the test is `Sharpy.IList`, and
                        // `case list(xs):` on an `object` subject silently took the `_` arm.
                        var selfTypeSyntax = PatternTestTypeSyntax(positionalPattern, positionalPattern.Type);
                        var innerElement = positionalPattern.Elements[0];
                        if (innerElement is BindingPattern bp)
                        {
                            var selfVarName = GetMangledVariableName(bp.Name, isNewDeclaration: true);
                            return DeclarationPattern(selfTypeSyntax,
                                SingleVariableDesignation(Identifier(selfVarName)));
                        }
                        if (innerElement is WildcardPattern)
                        {
                            return DeclarationPattern(selfTypeSyntax, DiscardDesignation());
                        }
                        var innerPat = GenerateMatchPattern(
                            innerElement, memberGuards, ref matchVarCounter, scrutineeType);
                        return BinaryPattern(SyntaxKind.AndPattern,
                            DeclarationPattern(selfTypeSyntax, DiscardDesignation()),
                            innerPat);
                    }

                    // Check if this is a union case pattern
                    var unionCase = _context.SemanticInfo?.GetPatternUnionCase(positionalPattern);
                    if (unionCase != null)
                    {
                        // Handle Optional/Result synthetic union cases via Deconstruct
                        var optResultPattern = TryGenerateOptionalResultPattern(
                            positionalPattern, unionCase, scrutineeType, memberGuards, ref matchVarCounter);
                        if (optResultPattern != null)
                            return optResultPattern;

                        return GenerateUnionCasePositionalPattern(
                            positionalPattern, unionCase, scrutineeType, memberGuards, ref matchVarCounter);
                    }

                    // Same read as the type-pattern arm (#1670) — see the property-pattern case above.
                    var typeSyntax = positionalPattern.Type != null
                        ? PatternTestTypeSyntax(positionalPattern, positionalPattern.Type)
                        : null;

                    // Look up the type symbol to get field names for positional-to-property mapping
                    TypeSymbol? typeSymbol = null;
                    if (positionalPattern.Type != null)
                    {
                        var symbol = _context.SymbolTable.Lookup(positionalPattern.Type.Name);
                        if (symbol is TypeSymbol ts)
                            typeSymbol = ts;
                    }

                    if (typeSymbol != null && typeSymbol.Fields.Count == positionalPattern.Elements.Length)
                    {
                        // Emit as property pattern using field names (no Deconstruct needed)
                        var subPatterns = new List<SubpatternSyntax>();
                        for (int i = 0; i < positionalPattern.Elements.Length; i++)
                        {
                            var fieldName = NameMangler.Transform(
                                typeSymbol.Fields[i].Name, NameContext.Field);
                            var subPattern = GenerateMatchPattern(
                                positionalPattern.Elements[i], memberGuards, ref matchVarCounter);
                            subPatterns.Add(Subpattern(subPattern)
                                .WithNameColon(NameColon(EscapedIdentifierName(fieldName))));
                        }
                        var recursivePattern = RecursivePattern()
                            .WithPropertyPatternClause(
                                PropertyPatternClause(SeparatedList(subPatterns)));
                        if (typeSyntax != null)
                            recursivePattern = recursivePattern.WithType(typeSyntax);
                        return recursivePattern;
                    }
                    else
                    {
                        // Fallback: emit as positional pattern (requires Deconstruct).
                        // This path should only be reached if the type has a Deconstruct method.
                        // If not, the semantic layer should have caught it (SPY0369).
                        _context.AddWarning(
                            $"Emitting positional pattern for type '{positionalPattern.Type?.Name ?? "unknown"}' as Deconstruct fallback. If Deconstruct is missing, this will fail at C# compilation.",
                            DiagnosticCodes.CodeGen.PositionalPatternFallback,
                            positionalPattern.LineStart,
                            positionalPattern.ColumnStart);
                        var subPatterns = new SubpatternSyntax[positionalPattern.Elements.Length];
                        for (int i = 0; i < positionalPattern.Elements.Length; i++)
                        {
                            subPatterns[i] = Subpattern(GenerateMatchPattern(
                                positionalPattern.Elements[i], memberGuards, ref matchVarCounter));
                        }
                        var recursivePattern = RecursivePattern()
                            .WithPositionalPatternClause(
                                PositionalPatternClause(SeparatedList(subPatterns)));
                        if (typeSyntax != null)
                            recursivePattern = recursivePattern.WithType(typeSyntax);
                        return recursivePattern;
                    }
                }

            case StarPattern:
                // A '*' capture is emitted inline by the ListPattern case as a C# slice
                // pattern; it should never reach here standalone. Guard defensively.
                _context.AddError(
                    "A '*' capture may only appear inside a list pattern.",
                    DiagnosticCodes.CodeGen.UnsupportedFeature,
                    pattern.LineStart,
                    pattern.ColumnStart);
                return DiscardPattern();

            default:
                _context.AddError(
                    $"Unsupported match pattern type '{pattern.GetType().Name}'. This pattern is not yet implemented in code generation.",
                    DiagnosticCodes.CodeGen.UnsupportedFeature,
                    pattern.LineStart,
                    pattern.ColumnStart);
                // Return a discard pattern (matches everything) as fallback — acceptable
                // since an error was already reported above.
                return DiscardPattern();
        }
    }

    /// <summary>
    /// Generates a C# positional deconstruction pattern for Optional/Result synthetic union cases.
    /// Returns null if the union case is not from a synthetic Optional/Result union.
    ///
    /// Optional[T].Deconstruct(out bool hasValue, out T value):
    ///   Some(v)  → (true, var v)
    ///   None()   → (false, _)
    ///
    /// Result[T, E].Deconstruct(out bool isOk, out T value, out E error):
    ///   Ok(v)    → (true, var v, _)
    ///   Err(e)   → (false, _, var e)
    /// </summary>
    private PatternSyntax? TryGenerateOptionalResultPattern(
        PositionalPattern positionalPattern,
        TypeSymbol unionCaseSymbol,
        SemanticType? scrutineeType,
        List<ExpressionSyntax> memberGuards,
        ref int matchVarCounter)
    {
        if (scrutineeType is OptionalType && unionCaseSymbol.Name == "Some")
        {
            // Some(v) → (true, var v)
            var subPatterns = new List<SubpatternSyntax>
            {
                Subpattern(ConstantPattern(LiteralExpression(SyntaxKind.TrueLiteralExpression)))
            };
            if (positionalPattern.Elements.Length == 1)
            {
                subPatterns.Add(Subpattern(GenerateMatchPattern(
                    positionalPattern.Elements[0], memberGuards, ref matchVarCounter)));
            }
            else
            {
                subPatterns.Add(Subpattern(VarPattern(DiscardDesignation())));
            }
            return RecursivePattern()
                .WithPositionalPatternClause(
                    PositionalPatternClause(SeparatedList(subPatterns)));
        }

        if (scrutineeType is ResultType && unionCaseSymbol.Name == "Ok")
        {
            // Ok(v) → (true, var v, _)
            var subPatterns = new List<SubpatternSyntax>
            {
                Subpattern(ConstantPattern(LiteralExpression(SyntaxKind.TrueLiteralExpression)))
            };
            if (positionalPattern.Elements.Length == 1)
            {
                subPatterns.Add(Subpattern(GenerateMatchPattern(
                    positionalPattern.Elements[0], memberGuards, ref matchVarCounter)));
            }
            else
            {
                subPatterns.Add(Subpattern(VarPattern(DiscardDesignation())));
            }
            subPatterns.Add(Subpattern(VarPattern(DiscardDesignation())));
            return RecursivePattern()
                .WithPositionalPatternClause(
                    PositionalPatternClause(SeparatedList(subPatterns)));
        }

        if (scrutineeType is ResultType && unionCaseSymbol.Name == "Err")
        {
            // Err(e) → (false, _, var e)
            var subPatterns = new List<SubpatternSyntax>
            {
                Subpattern(ConstantPattern(LiteralExpression(SyntaxKind.FalseLiteralExpression))),
                Subpattern(VarPattern(DiscardDesignation()))
            };
            if (positionalPattern.Elements.Length == 1)
            {
                subPatterns.Add(Subpattern(GenerateMatchPattern(
                    positionalPattern.Elements[0], memberGuards, ref matchVarCounter)));
            }
            else
            {
                subPatterns.Add(Subpattern(VarPattern(DiscardDesignation())));
            }
            return RecursivePattern()
                .WithPositionalPatternClause(
                    PositionalPatternClause(SeparatedList(subPatterns)));
        }

        return null;
    }

    /// <summary>
    /// Generates a C# positional pattern for a union case with fields.
    /// Emits: UnionName{TypeArgs}.CaseName(var field1, var field2)
    /// Uses the Deconstruct method generated on the union case class.
    /// </summary>
    private PatternSyntax GenerateUnionCasePositionalPattern(
        PositionalPattern positionalPattern,
        TypeSymbol unionCaseSymbol,
        SemanticType? scrutineeType,
        List<ExpressionSyntax> memberGuards,
        ref int matchVarCounter)
    {
        var caseTypeSyntax = BuildUnionCaseTypeSyntax(unionCaseSymbol, scrutineeType);

        // Generate positional subpatterns using Deconstruct
        var subPatterns = new SubpatternSyntax[positionalPattern.Elements.Length];
        for (int i = 0; i < positionalPattern.Elements.Length; i++)
        {
            subPatterns[i] = Subpattern(GenerateMatchPattern(
                positionalPattern.Elements[i], memberGuards, ref matchVarCounter));
        }

        return RecursivePattern()
            .WithType(caseTypeSyntax)
            .WithPositionalPatternClause(
                PositionalPatternClause(SeparatedList(subPatterns)));
    }

    /// <summary>
    /// Builds the C# type syntax for a union case nested class.
    /// For non-generic unions: UnionName.CaseName
    /// For generic unions: UnionName{T1, T2}.CaseName
    /// Type arguments are substituted from the scrutinee type.
    /// </summary>
    private TypeSyntax BuildUnionCaseTypeSyntax(TypeSymbol unionCaseSymbol, SemanticType? scrutineeType)
    {
        var caseCSharpName = NameMangler.Transform(unionCaseSymbol.Name, NameContext.Type);
        var unionParent = unionCaseSymbol.BaseType;

        if (unionParent == null)
        {
            return IdentifierName(caseCSharpName);
        }

        var unionCSharpName = NameMangler.Transform(unionParent.Name, NameContext.Type);

        // Build the union base type, with type arguments if generic
        NameSyntax unionNameSyntax;
        if (unionParent.IsGeneric && scrutineeType is GenericType gt
            && gt.TypeArguments.Count > 0)
        {
            var typeArgsSyntax = gt.TypeArguments
                .Select(t => _typeMapper.MapSemanticType(t))
                .ToArray();
            unionNameSyntax = GenericName(Identifier(unionCSharpName))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));
        }
        else if (unionParent.IsGeneric)
        {
            // Scrutinee type carries no concrete type arguments (e.g. 'match self'
            // inside a generic union method, where self is typed as the open union).
            // Reference the union with its own type parameter names so the nested
            // case type is correctly qualified (e.g. Option<T>.Some).
            var typeParamSyntax = unionParent.TypeParameters
                .Select(tp => (TypeSyntax)TypeParameterIdentifierName(tp.Name))
                .ToArray();
            unionNameSyntax = GenericName(Identifier(unionCSharpName))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeParamSyntax)));
        }
        else
        {
            unionNameSyntax = IdentifierName(unionCSharpName);
        }

        return QualifiedName(unionNameSyntax, IdentifierName(caseCSharpName));
    }

    private ExpressionSyntax? CombineGuards(List<ExpressionSyntax> memberGuards, Expression? userGuardExpr)
    {
        ExpressionSyntax? combined = null;
        foreach (var guard in memberGuards)
        {
            combined = combined == null
                ? guard
                : Binary(SyntaxKind.LogicalAndExpression, combined, guard);
        }

        if (userGuardExpr != null)
        {
            var userGuard = WrapTruthinessIfNeeded(GenerateExpression(userGuardExpr), userGuardExpr);
            combined = combined == null
                ? userGuard
                : Binary(SyntaxKind.LogicalAndExpression, combined, userGuard);
        }

        return combined;
    }

    private ExpressionSyntax GenerateMatchExpression(MatchExpression matchExpr)
    {
        var scrutineeExpr = GenerateExpression(matchExpr.Scrutinee);
        if (_context.SemanticInfo?.GetMatchScrutineeLowering(matchExpr.Scrutinee) is
            { Kind: MatchScrutineeLoweringKind.CastToNullableObject })
        {
            scrutineeExpr = Cast(
                NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
                scrutineeExpr);
        }

        var scrutineeType = _context.SemanticInfo?.GetExpressionType(matchExpr.Scrutinee);

        var generated = new List<GeneratedMatchArm>(matchExpr.Arms.Length);
        var armResults = new List<ExpressionSyntax>(matchExpr.Arms.Length);

        foreach (var arm in matchExpr.Arms)
        {
            var memberGuards = new List<ExpressionSyntax>();
            int matchVarCounter = 0;
            var pattern = GenerateMatchPattern(arm.Pattern, memberGuards, ref matchVarCounter, scrutineeType);

            // Guard and result are BOTH per-arm evaluations: the guard runs only when its pattern
            // matched, the result only when the arm is selected. Each gets its own sink, and a
            // switch-expression arm is an expression position that cannot host statements — so a
            // hoist in either forces the `is`-chain lowering (Design Decision 6).
            ExpressionSyntax? combinedGuard = null;
            var guardEvaluations = WithEvaluationSink(
                () => combinedGuard = CombineGuards(memberGuards, arm.Guard));

            ExpressionSyntax resultExpr = null!;
            var resultEvaluations = WithEvaluationSink(
                () => resultExpr = GenerateExpression(arm.Result));

            generated.Add(new GeneratedMatchArm(
                pattern,
                combinedGuard,
                guardEvaluations,
                arm.Pattern is WildcardPattern && combinedGuard == null,
                // The result's own hoists plus the assignment of the result are the arm's "body"
                // in the is-chain form; the switch-expression form needs the bare expression, so
                // it is carried alongside in ResultExpressions below.
                resultEvaluations));

            armResults.Add(resultExpr);
        }

        if (generated.Any(a => a.GuardEvaluations.Count > 0 || a.Body.Count > 0))
        {
            return GenerateMatchExpressionAsIsChain(matchExpr, scrutineeExpr, generated, armResults);
        }

        var switchArms = new List<SwitchExpressionArmSyntax>(generated.Count);
        for (int i = 0; i < generated.Count; i++)
        {
            var switchArm = SwitchExpressionArm(generated[i].Pattern, armResults[i]);
            if (generated[i].Guard != null)
            {
                switchArm = switchArm.WithWhenClause(WhenClause(generated[i].Guard!));
            }
            switchArms.Add(switchArm);
        }

        return SwitchExpression(scrutineeExpr, SeparatedList(switchArms));
    }

    /// <summary>
    /// The <c>is</c>-chain lowering of a match EXPRESSION, used when an arm's guard or result
    /// produces hoisted statements (plan-0667c5 Design Decision 6, #1739 acceptance cells 8-9). The
    /// expression yields a temp assigned in the selected arm:
    /// <code>
    /// var __matchSubject_0 = &lt;scrutinee&gt;;
    /// T __matchValue_0;
    /// bool __matchTaken_0 = false;
    /// if (!__matchTaken_0 &amp;&amp; __matchSubject_0 is P1 p1)
    /// {
    ///     &lt;guard 1 hoists&gt;
    ///     if (&lt;guard 1&gt;) { __matchTaken_0 = true; &lt;result 1 hoists&gt; __matchValue_0 = &lt;result 1&gt;; }
    /// }
    /// …
    /// if (!__matchTaken_0) throw new System.InvalidOperationException("Unreachable: exhaustive match");
    /// </code>
    /// The trailing throw is unconditional here (not gated on semantic exhaustiveness): a match
    /// EXPRESSION must produce a value, so a fall-through is a runtime error, which is exactly what
    /// the C# <c>switch</c> expression does on its own (it throws
    /// <c>SwitchExpressionException</c>). It also gives <c>__matchValue_0</c> its definite
    /// assignment.
    /// </summary>
    private ExpressionSyntax GenerateMatchExpressionAsIsChain(
        MatchExpression matchExpr,
        ExpressionSyntax scrutineeExpr,
        List<GeneratedMatchArm> arms,
        List<ExpressionSyntax> armResults)
    {
        var resultType = GetExpressionSemanticType(matchExpr)
            ?? throw new InvalidOperationException(
                "No semantic type recorded for a match expression whose arm hoists; the TypeChecker "
                + "must type every match expression (plan-0667c5 Design Decision 6).");

        var subjectName = GenerateTempVarName("matchSubject");
        var valueName = GenerateTempVarName("matchValue");
        var takenName = GenerateTempVarName("matchTaken");

        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(subjectName))
                        .WithInitializer(EqualsValueClause(scrutineeExpr))))));

        // `= default!` rather than a bare declaration: the trailing `if (!taken) throw` guarantees
        // no arm-less read at runtime, but C#'s definite-assignment analysis cannot correlate the
        // flag with the assignments and reports CS0165 without an initializer.
        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(_typeMapper.MapSemanticType(resultType))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(valueName))
                        .WithInitializer(EqualsValueClause(
                            PostfixUnaryExpression(
                                SyntaxKind.SuppressNullableWarningExpression,
                                LiteralExpression(SyntaxKind.DefaultLiteralExpression))))))));

        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(takenName))
                        .WithInitializer(EqualsValueClause(
                            LiteralExpression(SyntaxKind.FalseLiteralExpression)))))));

        var notTaken = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
            EscapedIdentifierName(takenName));

        for (int i = 0; i < arms.Count; i++)
        {
            var arm = arms[i];

            ExpressionSyntax armTest = arm.IsWildcardWithoutGuard
                ? notTaken
                : BinaryExpression(SyntaxKind.LogicalAndExpression,
                    notTaken,
                    ParenthesizedExpression(IsPatternExpression(
                        EscapedIdentifierName(subjectName), arm.Pattern)));

            // The selected arm marks itself taken, runs the result's hoists, then stores the value.
            var selectedBody = new List<StatementSyntax>(arm.Body.Count + 2)
            {
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    EscapedIdentifierName(takenName),
                    LiteralExpression(SyntaxKind.TrueLiteralExpression)))
            };
            selectedBody.AddRange(arm.Body);
            selectedBody.Add(ExpressionStatement(
                AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    EscapedIdentifierName(valueName), armResults[i])));

            if (arm.Guard == null)
            {
                HoistEvaluation(IfStatement(armTest, Block(selectedBody)));
                continue;
            }

            var patternMatchedBody = new List<StatementSyntax>(arm.GuardEvaluations.Count + 1);
            patternMatchedBody.AddRange(arm.GuardEvaluations);
            patternMatchedBody.Add(IfStatement(arm.Guard, Block(selectedBody)));

            HoistEvaluation(IfStatement(armTest, Block(patternMatchedBody)));
        }

        HoistEvaluation(IfStatement(notTaken, Block(BuildUnreachableExhaustiveMatchThrow())));

        return EscapedIdentifierName(valueName);
    }

    private PatternSyntax GenerateTypePattern(
        TypePattern typePattern, VariableDesignationSyntax designation, SemanticType? scrutineeType)
    {
        var unionCase = _context.SemanticInfo?.GetPatternUnionCase(typePattern);

        if (unionCase?.Name == WellKnownCaseNames.Some && scrutineeType is OptionalType)
        {
            var payloadTypeSyntax = PatternTestTypeSyntax(typePattern, typePattern.Type);

            var payloadPattern = DeclarationPattern(payloadTypeSyntax, designation);

            return RecursivePattern()
                .WithPositionalPatternClause(
                    PositionalPatternClause(SeparatedList(new[]
                    {
                        Subpattern(ConstantPattern(LiteralExpression(SyntaxKind.TrueLiteralExpression))),
                        Subpattern(payloadPattern)
                    })));
        }

        if (unionCase != null)
        {
            var caseTypeSyntax = BuildUnionCaseTypeSyntax(unionCase, scrutineeType);
            return DeclarationPattern(caseTypeSyntax, designation);
        }

        // The array-scrutinee decision (`case list()` against `array[T]`) is no longer taken here:
        // the checker records it as a ClosedType lowering naming the array, which the shared read
        // below maps identically (#1670).
        return DeclarationPattern(PatternTestTypeSyntax(typePattern, typePattern.Type), designation);
    }

    /// <summary>
    /// The C# type a class pattern tests against — READ, never derived (Critical Rule 2, #1670).
    /// <para>
    /// One reader for the type-pattern arm, all three positional self-matching sub-arms
    /// (binding / wildcard / inner pattern) and the property-pattern arm, because they emit one
    /// test. The recorded <see cref="TypeTestLowering"/> is consulted FIRST and the recorded pattern
    /// type only as its narrower sibling: the two differ exactly where the difference matters — an
    /// erased builtin collection tests <c>Sharpy.IList</c> while it CAPTURES <c>list[object]</c>, and
    /// reading the capture type here is what emitted <c>case Sharpy.List&lt;object&gt; xs:</c> for
    /// <c>case list(xs):</c> over an <c>object</c> subject, an arm that never matched.
    /// </para>
    /// <para>
    /// A missing fact throws rather than falling back to the written annotation: semantic analysis
    /// records one on every class pattern it accepts (<c>TypeChecker.ClassifyPatternClassTest</c>),
    /// so its absence is a compiler bug, and the annotation fallback is precisely the Rule 2
    /// violation this reader exists to remove — it is how the unspellable open generic
    /// <c>Sharpy.List</c> (CS0305) reached the C# compiler.
    /// </para>
    /// </summary>
    private TypeSyntax PatternTestTypeSyntax(Pattern pattern, TypeAnnotation annotation)
    {
        if (_context.SemanticInfo?.GetTypeTestLowering(pattern) is { } lowering)
            return MapTypeTestTarget(lowering);

        if (_context.SemanticInfo?.GetPatternType(pattern) is { } decidedPatternType)
            return _typeMapper.MapSemanticType(decidedPatternType);

        throw new InvalidOperationException(
            $"No type-test lowering and no pattern type were recorded for the class pattern "
            + $"'{annotation.Name}' at line {pattern.LineStart}, column {pattern.ColumnStart}. "
            + "Semantic analysis records both on every class pattern it accepts "
            + "(TypeChecker.ClassifyPatternClassTest); a missing fact is a compiler bug, not a case "
            + "for re-deriving the type from the written annotation (#1670).");
    }
}
