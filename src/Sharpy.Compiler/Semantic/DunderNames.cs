namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Compile-time constants for Python dunder (double-underscore) method names.
/// Centralizes all dunder name strings to prevent typo-induced silent bugs
/// and provides a single source of truth for the compiler.
/// </summary>
/// <remarks>
/// All new dunder name references should use these constants rather than
/// string literals. The names are organized by category for readability.
/// </remarks>
internal static class DunderNames
{
    // ---- Special methods ----
    public const string Init = "__init__";
    public const string Repr = "__repr__";
    public const string Str = "__str__";
    public const string Hash = "__hash__";
    public const string PostInit = "__post_init__";
    public const string Bool = "__bool__";
    public const string Len = "__len__";
    public const string Iter = "__iter__";
    public const string Next = "__next__";

    // ---- Container methods ----
    public const string Contains = "__contains__";
    public const string GetItem = "__getitem__";
    public const string SetItem = "__setitem__";
    public const string Reversed = "__reversed__";

    // ---- Comparison operators ----
    public const string Eq = "__eq__";
    public const string Ne = "__ne__";
    public const string Lt = "__lt__";
    public const string Le = "__le__";
    public const string Gt = "__gt__";
    public const string Ge = "__ge__";

    // ---- Unary operators ----
    public const string Neg = "__neg__";
    public const string Pos = "__pos__";
    public const string Invert = "__invert__";

    // ---- Binary arithmetic operators ----
    public const string Add = "__add__";
    public const string Sub = "__sub__";
    public const string Mul = "__mul__";
    public const string Div = "__div__";
    public const string Mod = "__mod__";

    // ---- Binary bitwise operators ----
    public const string And = "__and__";
    public const string Or = "__or__";
    public const string Xor = "__xor__";
    public const string LShift = "__lshift__";
    public const string RShift = "__rshift__";

    // ---- Context manager methods ----
    public const string Enter = "__enter__";
    public const string Exit = "__exit__";
    public const string Aenter = "__aenter__";
    public const string Aexit = "__aexit__";

    // ---- Conversion operators ----
    public const string Implicit = "__implicit__";
    public const string Explicit = "__explicit__";

}
