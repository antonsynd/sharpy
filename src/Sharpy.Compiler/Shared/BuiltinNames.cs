namespace Sharpy.Compiler.Shared;

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
    public const string Ascii = "ascii";

    // ---- Debugging ----
    public const string Breakpoint = "breakpoint";

    // ---- Introspection ----
    public const string Type = "type";
    public const string Id = "id";
    public const string Isinstance = "isinstance";
    public const string Repr = "repr";

    // ---- Iteration ----
    public const string Range = "range";
    public const string Iterator = "Iterator";
    public const string IEnumerable = "IEnumerable";
    public const string IEnumerator = "IEnumerator";

    // ---- Additional type names ----
    public const string Long = "long";
    public const string Double = "double";
    public const string Float32 = "float32";
    public const string Float64 = "float64";
    public const string Decimal = "decimal";
    public const string List = "list";
    public const string Dict = "dict";
    public const string Set = "set";
    public const string Array = "array";
    public const string Tuple = "tuple";
    public const string None = "None";
    public const string Object = "object";
    public const string Void = "void";

    // ---- Wrapper type names ----
    public const string Optional = "Optional";
    public const string Result = "Result";
    public const string Function = "function";

    // ---- Async types ----
    public const string Task = "Task";

    // ---- Self type ----
    public const string Self = "Self";

    // ---- Dict view types (returned by dict.items(), .keys(), .values()) ----
    public const string DictItemsView = "DictItemsView";
    public const string DictKeyView = "DictKeyView";
    public const string DictValuesView = "DictValuesView";
}
