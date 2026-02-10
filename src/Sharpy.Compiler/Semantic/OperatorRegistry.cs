using System.Collections.Frozen;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Categorizes operator dunders by their semantic purpose.
/// </summary>
public enum OperatorKind
{
    BinaryArithmetic,  // __add__, __sub__, __mul__, etc.
    BinaryBitwise,     // __and__, __or__, __xor__, etc.
    InPlace,           // __iadd__, __isub__, __imul__, etc.
    Comparison,        // __eq__, __ne__, __lt__, etc.
    Unary              // __pos__, __neg__, __invert__
}

/// <summary>
/// Central registry of operator dunder methods and their classification.
/// Complements <see cref="ProtocolRegistry"/> which handles non-operator dunders.
/// </summary>
public static class OperatorRegistry
{
    private static readonly FrozenSet<string> BinaryArithmeticOps = new[]
    {
        DunderNames.Add, DunderNames.Sub, DunderNames.Mul, DunderNames.Div, DunderNames.Mod
    }.ToFrozenSet();

    private static readonly FrozenSet<string> BinaryBitwiseOps = new[]
    {
        DunderNames.And, DunderNames.Or, DunderNames.Xor, DunderNames.LShift, DunderNames.RShift
    }.ToFrozenSet();

    private static readonly FrozenSet<string> InPlaceOps = new[]
    {
        DunderNames.IAdd, DunderNames.ISub, DunderNames.IMul, DunderNames.IDiv, DunderNames.IMod,
        DunderNames.IAnd, DunderNames.IOr, DunderNames.IXor, DunderNames.ILShift, DunderNames.IRShift
    }.ToFrozenSet();

    private static readonly FrozenSet<string> ComparisonOps = new[]
    {
        DunderNames.Eq, DunderNames.Ne, DunderNames.Lt, DunderNames.Le, DunderNames.Gt, DunderNames.Ge
    }.ToFrozenSet();

    private static readonly FrozenSet<string> UnaryOps = new[]
    {
        DunderNames.Pos, DunderNames.Neg, DunderNames.Invert
    }.ToFrozenSet();

    private static readonly FrozenDictionary<string, OperatorKind> AllOperatorDunders;

    static OperatorRegistry()
    {
        var dict = new Dictionary<string, OperatorKind>();
        foreach (var op in BinaryArithmeticOps)
            dict[op] = OperatorKind.BinaryArithmetic;
        foreach (var op in BinaryBitwiseOps)
            dict[op] = OperatorKind.BinaryBitwise;
        foreach (var op in InPlaceOps)
            dict[op] = OperatorKind.InPlace;
        foreach (var op in ComparisonOps)
            dict[op] = OperatorKind.Comparison;
        foreach (var op in UnaryOps)
            dict[op] = OperatorKind.Unary;
        AllOperatorDunders = dict.ToFrozenDictionary();
    }

    /// <summary>
    /// Checks if a method name is a recognized operator dunder method.
    /// </summary>
    public static bool IsOperatorDunder(string methodName)
        => AllOperatorDunders.ContainsKey(methodName);

    /// <summary>
    /// Gets the operator kind for a dunder method name, or null if not an operator dunder.
    /// </summary>
    public static OperatorKind? GetOperatorKind(string methodName)
        => AllOperatorDunders.TryGetValue(methodName, out var kind) ? kind : null;

    /// <summary>
    /// Checks if the operator is unary (takes only self).
    /// </summary>
    public static bool IsUnaryOperator(string methodName)
        => UnaryOps.Contains(methodName);

    /// <summary>
    /// Checks if the operator is a comparison operator (must return bool).
    /// </summary>
    public static bool IsComparisonOperator(string methodName)
        => ComparisonOps.Contains(methodName);

    /// <summary>
    /// Gets the expected parameter count for an operator dunder.
    /// Returns 1 for unary operators, 2 for binary/comparison/in-place operators, or null if not an operator.
    /// </summary>
    public static int? GetExpectedParamCount(string methodName)
    {
        var kind = GetOperatorKind(methodName);
        return kind switch
        {
            OperatorKind.Unary => 1,
            OperatorKind.BinaryArithmetic or OperatorKind.BinaryBitwise
                or OperatorKind.InPlace or OperatorKind.Comparison => 2,
            _ => null
        };
    }

    /// <summary>
    /// Returns all registered operator dunder names.
    /// </summary>
    public static IEnumerable<string> GetAllOperators()
        => AllOperatorDunders.Keys;

    /// <summary>
    /// Gets the count of registered operator dunders.
    /// </summary>
    public static int Count => AllOperatorDunders.Count;
}
