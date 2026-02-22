using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Codegen-specific dunder mapping: Roslyn operator syntax kinds and delegation to
/// <see cref="DunderNameMapping"/> for name resolution.
/// </summary>
internal static class DunderMapping
{
    /// <summary>
    /// Check if a name is a dunder method (starts and ends with __ and length > 5).
    /// </summary>
    public static bool IsDunderMethod(string name)
        => DunderNameMapping.IsDunderMethod(name);

    /// <summary>
    /// Resolve the C# name for a dunder method. Returns null if the name is not a dunder
    /// or if the dunder is not in the known mapping.
    /// For known dunders, returns the mapped name (e.g., __str__ → ToString).
    /// Unknown dunders are rejected at compile time by SignatureValidator (SPY0414).
    /// </summary>
    public static string? ResolveCSharpName(string name)
        => DunderNameMapping.ResolveCSharpName(name);

    /// <summary>
    /// Try to get the binary expression syntax kind for an operator dunder.
    /// Used to transform cross-dunder calls (e.g., self.__lt__(other) → this &lt; other).
    /// Returns null if the dunder is not a binary operator or is handled by the method map (e.g., __eq__ → Equals).
    /// </summary>
    public static SyntaxKind? TryGetBinaryExpressionKind(string dunderName)
    {
        return dunderName switch
        {
            // Arithmetic operators
            DunderNames.Add => SyntaxKind.AddExpression,
            DunderNames.Sub => SyntaxKind.SubtractExpression,
            DunderNames.Mul => SyntaxKind.MultiplyExpression,
            DunderNames.Div => SyntaxKind.DivideExpression,
            DunderNames.Mod => SyntaxKind.ModuloExpression,

            // Bitwise operators
            DunderNames.And => SyntaxKind.BitwiseAndExpression,
            DunderNames.Or => SyntaxKind.BitwiseOrExpression,
            DunderNames.Xor => SyntaxKind.ExclusiveOrExpression,
            DunderNames.LShift => SyntaxKind.LeftShiftExpression,
            DunderNames.RShift => SyntaxKind.RightShiftExpression,

            // Comparison operators (excluding __eq__ which maps to Equals via _dunderMethodMap)
            DunderNames.Ne => SyntaxKind.NotEqualsExpression,
            DunderNames.Lt => SyntaxKind.LessThanExpression,
            DunderNames.Le => SyntaxKind.LessThanOrEqualExpression,
            DunderNames.Gt => SyntaxKind.GreaterThanExpression,
            DunderNames.Ge => SyntaxKind.GreaterThanOrEqualExpression,

            _ => null
        };
    }

    /// <summary>
    /// Try to get the unary expression syntax kind for an operator dunder.
    /// Used to transform cross-dunder calls (e.g., self.__neg__() → -this).
    /// Returns null if the dunder is not a unary operator.
    /// </summary>
    public static SyntaxKind? TryGetUnaryExpressionKind(string dunderName)
    {
        return dunderName switch
        {
            DunderNames.Neg => SyntaxKind.UnaryMinusExpression,
            DunderNames.Pos => SyntaxKind.UnaryPlusExpression,
            DunderNames.Invert => SyntaxKind.BitwiseNotExpression,
            _ => null
        };
    }
}
