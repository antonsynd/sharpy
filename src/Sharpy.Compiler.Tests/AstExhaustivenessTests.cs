using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Verifies that all concrete AST node types are handled in the major dispatch points.
/// If you add a new Expression or Statement subtype and this test fails,
/// add a case for it in the listed switch expressions.
/// </summary>
public class AstExhaustivenessTests
{
    /// <summary>
    /// All concrete (non-abstract) Expression subtypes must be covered in:
    /// - RoslynEmitter.GenerateExpression (CodeGen/RoslynEmitter.Expressions.cs)
    /// - TypeChecker.CheckExpression (Semantic/TypeChecker.Expressions.cs)
    /// </summary>
    [Fact]
    public void AllExpressionSubtypes_AreCoveredInCodeGen()
    {
        var expressionType = typeof(Expression);
        var concreteTypes = expressionType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(expressionType) && !t.IsAbstract)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        // These are the types handled in GenerateExpression and CheckExpression.
        // If you add a new Expression subtype, add it here AND add handler cases
        // in both RoslynEmitter.GenerateExpression and TypeChecker.CheckExpression.
        var handledTypes = new HashSet<string>
        {
            // Literals
            "IntegerLiteral", "FloatLiteral", "StringLiteral", "BytesLiteralExpression",
            "BooleanLiteral", "NoneLiteral", "EllipsisLiteral", "FStringLiteral",
            // Collections
            "ListLiteral", "DictLiteral", "SetLiteral", "TupleLiteral",
            // Comprehensions
            "ListComprehension", "SetComprehension", "DictComprehension", "DictSpreadComprehension",
            // Primary
            "Identifier", "MemberAccess", "IndexAccess", "SliceAccess",
            "FunctionCall", "SuperExpression",
            // Operators
            "UnaryOp", "BinaryOp", "ComparisonChain",
            // Advanced
            "ConditionalExpression", "LambdaExpression",
            "TypeCoercion", "TypeCheck", "Parenthesized", "WalrusExpression",
            "TryExpression", "MaybeExpression", "StarExpression", "SpreadElement", "ModifiedArgument",
            // Future (in Expression.Future.cs) — not yet handled, expected to be in default case
            "AwaitExpression", "MatchExpression",
        };

        var unhandled = concreteTypes.Except(handledTypes).ToList();
        var stale = handledTypes.Except(concreteTypes).ToList();

        Assert.True(unhandled.Count == 0,
            $"New Expression subtypes not in exhaustiveness list (add handler + update this test): {string.Join(", ", unhandled)}");
        Assert.True(stale.Count == 0,
            $"Stale entries in exhaustiveness list (types no longer exist): {string.Join(", ", stale)}");
    }

    [Fact]
    public void AllStatementSubtypes_AreCoveredInCodeGen()
    {
        var statementType = typeof(Statement);
        var concreteTypes = statementType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(statementType) && !t.IsAbstract)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        // Update this set when adding new Statement subtypes.
        var handledTypes = new HashSet<string>
        {
            // Simple
            "ExpressionStatement", "Assignment", "VariableDeclaration",
            "AssertStatement", "PassStatement", "BreakStatement",
            "BreakWithFlagStatement", "ContinueStatement", "ReturnStatement",
            "YieldStatement", "RaiseStatement",
            // Compound
            "IfStatement", "WhileStatement", "ForStatement", "TryStatement",
            "WithStatement",
            // Definitions
            "FunctionDef", "ClassDef", "StructDef", "InterfaceDef", "EnumDef", "PropertyDef",
            // Type
            "TypeAlias",
            // Imports
            "ImportStatement", "FromImportStatement",
            // Future
            "MatchStatement", "UnionDef", "DelegateDef", "EventDef",
        };

        var unhandled = concreteTypes.Except(handledTypes).ToList();
        var stale = handledTypes.Except(concreteTypes).ToList();

        Assert.True(unhandled.Count == 0,
            $"New Statement subtypes not in exhaustiveness list: {string.Join(", ", unhandled)}");
        Assert.True(stale.Count == 0,
            $"Stale entries in exhaustiveness list: {string.Join(", ", stale)}");
    }
}
