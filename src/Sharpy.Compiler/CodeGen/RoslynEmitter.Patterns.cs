using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Pattern matching code generation
/// </summary>
internal partial class RoslynEmitter
{
    private const string PatternMatchTempPrefix = "__spy_pm_";

    private StatementSyntax GenerateMatch(MatchStatement matchStmt)
    {
        var scrutineeExpr = GenerateExpression(matchStmt.Scrutinee);
        var scrutineeType = _context.SemanticInfo?.GetExpressionType(matchStmt.Scrutinee);
        var sections = new List<SwitchSectionSyntax>();

        foreach (var matchCase in matchStmt.Cases)
        {
            var bodyStatements = matchCase.Body.SelectMany(GenerateBodyStatements).ToList();

            // Only add break if the last statement isn't an unconditional jump
            var lastStatement = bodyStatements.LastOrDefault();
            if (lastStatement is not (ReturnStatementSyntax or ThrowStatementSyntax
                or BreakStatementSyntax or ContinueStatementSyntax)
                && !(lastStatement is YieldStatementSyntax { ReturnOrBreakKeyword.RawKind: (int)SyntaxKind.BreakKeyword }))
            {
                bodyStatements.Add(BreakStatement());
            }

            // Collect all MemberAccessPattern guards (including nested in tuples).
            // matchVarCounter resets per case arm — each switch section is an independent
            // scope in C#, so __spy_pm_0, __spy_pm_1 etc. can safely repeat across arms.
            var memberGuards = new List<ExpressionSyntax>();
            int matchVarCounter = 0;
            var pattern = GenerateMatchPattern(matchCase.Pattern, memberGuards, ref matchVarCounter, scrutineeType);
            SwitchLabelSyntax caseLabel;

            var combinedGuard = CombineGuards(memberGuards, matchCase.Guard);

            // WildcardPattern without guard → idiomatic `default:` label
            if (matchCase.Pattern is WildcardPattern && combinedGuard == null)
            {
                caseLabel = DefaultSwitchLabel();
            }
            else if (combinedGuard != null)
            {
                caseLabel = CasePatternSwitchLabel(pattern, WhenClause(combinedGuard), Token(SyntaxKind.ColonToken));
            }
            else
            {
                caseLabel = CasePatternSwitchLabel(pattern, Token(SyntaxKind.ColonToken));
            }

            sections.Add(SwitchSection(
                SingletonList(caseLabel),
                List<StatementSyntax>(bodyStatements)));
        }

        return SwitchStatement(scrutineeExpr, List(sections));
    }

    private ExpressionSyntax GenerateMemberAccessValue(MemberAccessPattern memberAccess)
    {
        // Build member access expression: Color.RED -> Color.RED
        // Type name is preserved as-is (ToTypeName), field names use ToPascalCase
        ExpressionSyntax expr = IdentifierName(
            NameMangler.Transform(memberAccess.Parts[0], NameContext.Type));

        for (int i = 1; i < memberAccess.Parts.Length; i++)
        {
            expr = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expr,
                IdentifierName(NameMangler.Transform(
                    memberAccess.Parts[i], NameContext.Field)));
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
                    var varName = GetMangledVariableName(binding.Name.Name, isNewDeclaration: true);
                    return VarPattern(SingleVariableDesignation(Identifier(varName)));
                }

            case LiteralPattern literal:
                {
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

            case TypePattern typePattern:
                {
                    // Check if this is a union case pattern (e.g., case Point(): matching Shape)
                    var unionCase = _context.SemanticInfo?.GetPatternUnionCase(typePattern);
                    if (unionCase != null)
                    {
                        var caseTypeSyntax = BuildUnionCaseTypeSyntax(unionCase, scrutineeType);
                        if (typePattern.BindingName != null)
                        {
                            var varName = GetMangledVariableName(typePattern.BindingName.Name, isNewDeclaration: true);
                            return DeclarationPattern(caseTypeSyntax, SingleVariableDesignation(Identifier(varName)));
                        }
                        return DeclarationPattern(caseTypeSyntax, DiscardDesignation());
                    }

                    var typeSyntax = _typeMapper.MapType(typePattern.Type);
                    if (typePattern.BindingName != null)
                    {
                        var varName = GetMangledVariableName(typePattern.BindingName.Name, isNewDeclaration: true);
                        return DeclarationPattern(typeSyntax, SingleVariableDesignation(Identifier(varName)));
                    }
                    return DeclarationPattern(typeSyntax, DiscardDesignation());
                }

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
                                comparison = BinaryExpression(
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
                                comparison = BinaryExpression(
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
                                : BinaryExpression(SyntaxKind.LogicalOrExpression, orGuard, comparison);
                        }
                        if (orGuard != null)
                            memberGuards.Add(orGuard);
                        return VarPattern(SingleVariableDesignation(Identifier(tempVarName)));
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
                    memberGuards.Add(BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        IdentifierName(tempVarName),
                        memberValue));
                    return VarPattern(SingleVariableDesignation(Identifier(tempVarName)));
                }

            case PropertyPattern propertyPattern:
                {
                    var typeSyntax = propertyPattern.Type != null
                        ? _typeMapper.MapType(propertyPattern.Type) : null;
                    var subPatterns = new List<SubpatternSyntax>();
                    foreach (var field in propertyPattern.Fields)
                    {
                        var fieldName = NameMangler.Transform(field.Name, NameContext.Field);
                        var subPattern = GenerateMatchPattern(field.Pattern, memberGuards, ref matchVarCounter);
                        subPatterns.Add(Subpattern(subPattern)
                            .WithNameColon(NameColon(IdentifierName(fieldName))));
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
                    // Check if this is a union case pattern
                    var unionCase = _context.SemanticInfo?.GetPatternUnionCase(positionalPattern);
                    if (unionCase != null)
                    {
                        return GenerateUnionCasePositionalPattern(
                            positionalPattern, unionCase, scrutineeType, memberGuards, ref matchVarCounter);
                    }

                    var typeSyntax = positionalPattern.Type != null
                        ? _typeMapper.MapType(positionalPattern.Type) : null;

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
                                .WithNameColon(NameColon(IdentifierName(fieldName))));
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
                        // Fallback: emit as positional pattern (requires Deconstruct)
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
                : BinaryExpression(SyntaxKind.LogicalAndExpression, combined, guard);
        }

        if (userGuardExpr != null)
        {
            var userGuard = GenerateExpression(userGuardExpr);
            combined = combined == null
                ? userGuard
                : BinaryExpression(SyntaxKind.LogicalAndExpression, combined, userGuard);
        }

        return combined;
    }

    private ExpressionSyntax GenerateMatchExpression(MatchExpression matchExpr)
    {
        var scrutineeExpr = GenerateExpression(matchExpr.Scrutinee);
        var scrutineeType = _context.SemanticInfo?.GetExpressionType(matchExpr.Scrutinee);
        var arms = new List<SwitchExpressionArmSyntax>();

        foreach (var arm in matchExpr.Arms)
        {
            var memberGuards = new List<ExpressionSyntax>();
            int matchVarCounter = 0;
            var pattern = GenerateMatchPattern(arm.Pattern, memberGuards, ref matchVarCounter, scrutineeType);

            var combinedGuard = CombineGuards(memberGuards, arm.Guard);

            var resultExpr = GenerateExpression(arm.Result);

            var switchArm = SwitchExpressionArm(pattern, resultExpr);
            if (combinedGuard != null)
            {
                switchArm = switchArm.WithWhenClause(WhenClause(combinedGuard));
            }
            arms.Add(switchArm);
        }

        return SwitchExpression(scrutineeExpr, SeparatedList(arms));
    }
}
