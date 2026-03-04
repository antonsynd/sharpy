using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Metadata about a Sharpy collection type for code generation.
/// </summary>
internal sealed record CollectionTypeInfo(
    string SharpyName,
    string CSharpTypeName,
    string AddMethodName,
    string SpreadMethodName,
    int ExpectedTypeArgCount);

/// <summary>
/// Registry of collection types, replacing hard-coded string comparisons
/// in the emitter with structured lookups.
/// </summary>
internal static class CollectionTypeRegistry
{
    private static readonly Dictionary<string, CollectionTypeInfo> _entries = new()
    {
        [BuiltinNames.List] = new CollectionTypeInfo(
            SharpyName: BuiltinNames.List,
            CSharpTypeName: CSharpTypeNames.SharpyList,
            AddMethodName: "Add",
            SpreadMethodName: "Extend",
            ExpectedTypeArgCount: 1),

        [BuiltinNames.Dict] = new CollectionTypeInfo(
            SharpyName: BuiltinNames.Dict,
            CSharpTypeName: CSharpTypeNames.SharpyDict,
            AddMethodName: "Add",
            SpreadMethodName: "Update",
            ExpectedTypeArgCount: 2),

        [BuiltinNames.Set] = new CollectionTypeInfo(
            SharpyName: BuiltinNames.Set,
            CSharpTypeName: CSharpTypeNames.SharpySet,
            AddMethodName: "Add",
            SpreadMethodName: "UnionWith",
            ExpectedTypeArgCount: 1),
    };

    /// <summary>
    /// Returns true if the given Sharpy type name is a known collection type.
    /// </summary>
    public static bool IsCollection(string name) => _entries.ContainsKey(name);

    /// <summary>
    /// Tries to get collection metadata for the given Sharpy type name.
    /// </summary>
    public static bool TryGet(string name, out CollectionTypeInfo info) =>
        _entries.TryGetValue(name, out info!);
}
