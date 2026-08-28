namespace Sharpy.Compiler.Shared;

internal static class SelfMatchingBuiltins
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        BuiltinNames.Int,
        BuiltinNames.Float,
        BuiltinNames.Str,
        BuiltinNames.Bytes,
        BuiltinNames.Bool,
        BuiltinNames.List,
        BuiltinNames.Tuple,
        BuiltinNames.Dict,
        BuiltinNames.Set,
        "frozenset",
        "bytearray"
    };

    public static bool IsSelfMatching(string typeName) => Names.Contains(typeName);
}
