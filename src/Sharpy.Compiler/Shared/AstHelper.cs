using System.Globalization;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Shared AST utility methods used by both semantic analysis and code generation.
/// </summary>
internal static class AstHelper
{
    /// <summary>
    /// Tries to extract a constant integer value from an expression.
    /// Handles IntegerLiteral and UnaryOp(Minus, IntegerLiteral) for negative indices.
    /// </summary>
    public static bool TryGetConstantIntIndex(Expression expr, out int value)
    {
        if (expr is IntegerLiteral intLit && int.TryParse(intLit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (expr is UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral negIntLit }
            && int.TryParse(negIntLit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var posValue))
        {
            value = -posValue;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Extracts a key to use for type narrowing from an expression.
    /// For simple identifiers, returns the name. For subscript expressions like arr[i], returns "arr[i]".
    /// For member access like self.value, returns "self.value".
    /// Returns null if the expression contains unsupported node types.
    /// </summary>
    public static string? ExtractNarrowingKey(Expression expr)
    {
        return expr switch
        {
            Identifier id => id.Name,
            IndexAccess indexAccess => BuildIndexAccessNarrowingKey(indexAccess),
            MemberAccess ma => ExtractMemberAccessNarrowingKey(ma),
            _ => null
        };
    }

    /// <summary>
    /// Builds a narrowing key for a subscript expression. The index must be distinguishable so
    /// sibling accesses like <c>t[0]</c> and <c>t[1]</c> get distinct keys — otherwise narrowing
    /// one tuple element would incorrectly shadow the others (and, via the index-access early
    /// return in CheckIndexAccess, suppress .ItemN lowering of the siblings). Returns null when
    /// the object or index cannot be keyed, so unsupported indices never collide on a shared key.
    /// </summary>
    private static string? BuildIndexAccessNarrowingKey(IndexAccess indexAccess)
    {
        var objectKey = ExtractNarrowingKey(indexAccess.Object);
        if (objectKey == null)
            return null;

        var indexKey = ExtractIndexComponentKey(indexAccess.Index);
        if (indexKey == null)
            return null;

        return $"{objectKey}[{indexKey}]";
    }

    /// <summary>
    /// Extracts a stable key component for a subscript index. Handles integer literals (including
    /// negated ones) and string literals as constants, and identifiers / nested subscripts /
    /// member accesses via <see cref="ExtractNarrowingKey"/>. Returns null for anything else.
    /// </summary>
    private static string? ExtractIndexComponentKey(Expression index)
    {
        return index switch
        {
            IntegerLiteral intLit => intLit.Value,
            UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral negLit } => "-" + negLit.Value,
            StringLiteral strLit => $"\"{strLit.Value}\"",
            _ => ExtractNarrowingKey(index)
        };
    }

    /// <summary>
    /// Checks whether an expression tree contains a walrus (assignment) expression.
    /// </summary>
    public static bool ContainsWalrusExpression(Expression expr)
    {
        return expr switch
        {
            WalrusExpression => true,
            BinaryOp binOp => ContainsWalrusExpression(binOp.Left) || ContainsWalrusExpression(binOp.Right),
            UnaryOp unaryOp => ContainsWalrusExpression(unaryOp.Operand),
            FunctionCall call => ContainsWalrusExpression(call.Function) || call.Arguments.Any(ContainsWalrusExpression),
            ComparisonChain cmp => cmp.Operands.Any(ContainsWalrusExpression),
            Parenthesized paren => ContainsWalrusExpression(paren.Expression),
            _ => false
        };
    }

    private static string? ExtractMemberAccessNarrowingKey(MemberAccess ma)
    {
        var objectKey = ExtractNarrowingKey(ma.Object);
        if (objectKey == null)
            return null;
        return $"{objectKey}.{ma.Member}";
    }
}
