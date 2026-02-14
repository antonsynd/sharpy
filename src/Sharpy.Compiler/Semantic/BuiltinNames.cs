namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Compile-time constants for Python builtin function names.
/// Centralizes all builtin name strings to prevent typo-induced silent bugs
/// and provides a single source of truth for the compiler.
/// </summary>
/// <remarks>
/// All new builtin name references should use these constants rather than
/// string literals. The names are organized by category for readability.
/// </remarks>
internal static class BuiltinNames
{
    // ---- Type conversion ----
    public const string Bool = "bool";
    public const string Int = "int";
    public const string Float = "float";
    public const string Str = "str";

    // ---- Collection operations ----
    public const string Len = "len";
    public const string Sorted = "sorted";
    public const string Reversed = "reversed";
    public const string Enumerate = "enumerate";
    public const string Zip = "zip";
    public const string Map = "map";
    public const string Filter = "filter";
    public const string Sum = "sum";
    public const string Min = "min";
    public const string Max = "max";

    // ---- I/O ----
    public const string Print = "print";
    public const string Input = "input";

    // ---- Numeric ----
    public const string Abs = "abs";
    public const string Hash = "hash";
    public const string Hex = "hex";
    public const string Oct = "oct";
    public const string Bin = "bin";
    public const string Ord = "ord";
    public const string Chr = "chr";

    // ---- Introspection ----
    public const string Type = "type";
    public const string Id = "id";
    public const string Isinstance = "isinstance";
    public const string Repr = "repr";

    // ---- Iteration ----
    public const string Range = "range";
}
