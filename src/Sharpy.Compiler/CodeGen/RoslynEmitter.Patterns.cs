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
    private StatementSyntax GenerateMatch(MatchStatement matchStmt)
    {
        var scrutineeExpr = GenerateExpression(matchStmt.Scrutinee);
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
            // scope in C#, so __match0, __match1 etc. can safely repeat across arms.
            var memberGuards = new List<ExpressionSyntax>();
            int matchVarCounter = 0;
            var pattern = GenerateMatchPattern(matchCase.Pattern, memberGuards, ref matchVarCounter);
            SwitchLabelSyntax caseLabel;

            // Combine all member access guards into a single expression
            ExpressionSyntax? combinedGuard = null;
            foreach (var guard in memberGuards)
            {
                combinedGuard = combinedGuard == null
                    ? guard
                    : BinaryExpression(SyntaxKind.LogicalAndExpression, combinedGuard, guard);
            }

            if (matchCase.Guard != null)
            {
                var userGuard = GenerateExpression(matchCase.Guard);
                combinedGuard = combinedGuard == null
                    ? userGuard
                    : BinaryExpression(SyntaxKind.LogicalAndExpression, combinedGuard, userGuard);
            }

            if (combinedGuard != null)
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

    private PatternSyntax GenerateMatchPattern(Pattern pattern, List<ExpressionSyntax> memberGuards, ref int matchVarCounter)
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
                        ">" => Token(SyntaxKind.GreaterThanToken),
                        ">=" => Token(SyntaxKind.GreaterThanEqualsToken),
                        "<" => Token(SyntaxKind.LessThanToken),
                        "<=" => Token(SyntaxKind.LessThanEqualsToken),
                        _ => Token(SyntaxKind.GreaterThanToken)
                    };
                    var valueExpr = GenerateExpression(relational.Value);
                    return RelationalPattern(operatorToken, valueExpr);
                }

            case OrPattern orPattern:
                {
                    // Check if any alternative is a MemberAccessPattern (needs guard-based approach)
                    bool hasMemberAccess = orPattern.Alternatives.Any(a => a is MemberAccessPattern);

                    if (hasMemberAccess)
                    {
                        // Use var binding + combined when guard with ||
                        var tempVarName = $"__match{matchVarCounter++}";
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
                            else
                            {
                                // For literals in mixed or-patterns, generate equality comparison
                                var altExpr = GenerateExpression(((LiteralPattern)alt).Literal);
                                comparison = BinaryExpression(
                                    SyntaxKind.EqualsExpression,
                                    IdentifierName(tempVarName),
                                    altExpr);
                            }
                            orGuard = orGuard == null
                                ? comparison
                                : BinaryExpression(SyntaxKind.LogicalOrExpression, orGuard, comparison);
                        }
                        if (orGuard != null)
                            memberGuards.Add(orGuard);
                        return VarPattern(SingleVariableDesignation(Identifier(tempVarName)));
                    }

                    // Simple or-pattern: use C# `or` pattern syntax
                    PatternSyntax result = GenerateMatchPattern(orPattern.Alternatives[0], memberGuards, ref matchVarCounter);
                    for (int i = 1; i < orPattern.Alternatives.Length; i++)
                    {
                        var right = GenerateMatchPattern(orPattern.Alternatives[i], memberGuards, ref matchVarCounter);
                        result = BinaryPattern(SyntaxKind.OrPattern, result, right);
                    }
                    return result;
                }

            case MemberAccessPattern memberAccess:
                {
                    // Bind to a named variable and add a when-clause guard for equality.
                    // This handles both top-level and nested (e.g., inside TuplePattern) cases.
                    var tempVarName = $"__match{matchVarCounter++}";
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
                // Return a discard pattern as fallback so compilation can continue
                return DiscardPattern();
        }
    }
}
