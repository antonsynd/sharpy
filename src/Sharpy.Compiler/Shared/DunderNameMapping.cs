using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Shared mapping of Python dunder method names to their C# equivalents.
/// Used by both Semantic (CodeGenInfoComputer) and CodeGen (RoslynEmitter) layers.
/// </summary>
internal static class DunderNameMapping
{
    // Dunder method name mappings to C# equivalents
    // Only map dunder methods that have C# override equivalents or special constructs
    // Operator-related dunder methods are NOT in this map — they preserve their dunder name
    private static readonly Dictionary<string, string> _dunderMethodMap = new()
    {
        { DunderNames.Init, "Constructor" },      // Special handling needed
        { DunderNames.Str, "ToString" },
        { DunderNames.Eq, "Equals" },
        { DunderNames.Hash, "GetHashCode" },
        { DunderNames.GetItem, "GetItem" },       // For indexer properties
        { DunderNames.SetItem, "SetItem" },       // For indexer properties
        { DunderNames.Len, "Count" },              // For Count property
        { DunderNames.Contains, "Contains" },     // For Contains method
        { DunderNames.Ne, "NotEquals" },          // For operator !=
        { DunderNames.Iter, "GetEnumerator" },    // For IEnumerable
        { DunderNames.Reversed, "GetReverseEnumerator" }, // For reverse iteration
        // __bool__ is handled as special codegen (operator true/false), not a simple name mapping
        { DunderNames.Enter, "Enter" },                // For context manager __enter__
        { DunderNames.Exit, "Exit" },                  // For context manager __exit__
        { DunderNames.Aenter, "AenterAsync" },         // For async context manager __aenter__
        { DunderNames.Aexit, "AexitAsync" },           // For async context manager __aexit__
    };

#if DEBUG
    static DunderNameMapping()
    {
        // Verify all protocol dunders with CLR mappings are in _dunderMethodMap
        foreach (var protocol in ProtocolRegistry.GetAllProtocols())
        {
            if (protocol.ClrMethodName != null && !_dunderMethodMap.ContainsKey(protocol.DunderName))
            {
                System.Diagnostics.Debug.Assert(false,
                    $"Protocol '{protocol.DunderName}' with CLR mapping '{protocol.ClrMethodName}' " +
                    $"is missing from DunderNameMapping._dunderMethodMap. Add: {{ \"{protocol.DunderName}\", \"...\" }}");
            }
        }
    }
#endif

    /// <summary>
    /// Get the C# equivalent name for a dunder method, if it exists in the map.
    /// Returns null if not found.
    /// </summary>
    public static string? GetCSharpName(string dunderName)
    {
        return _dunderMethodMap.TryGetValue(dunderName, out var mapped) ? mapped : null;
    }

    /// <summary>
    /// Check if a dunder method has a mapping in the map.
    /// </summary>
    public static bool HasMapping(string dunderName)
    {
        return _dunderMethodMap.ContainsKey(dunderName);
    }

    /// <summary>
    /// Check if a name is a dunder method (starts and ends with __ and length > 5).
    /// </summary>
    /// <remarks>
    /// Uses length > 5 for backward compatibility — <c>__x__</c> (length 5) is excluded.
    /// This differs from <see cref="NameFormDetector.Detect"/> which uses length > 4 for
    /// syntactic dunder classification. The difference is harmless.
    /// </remarks>
    public static bool IsDunderMethod(string name)
        => DunderDetector.IsDunderMethod(name);

    /// <summary>
    /// Resolve the C# name for a dunder method. Returns null if the name is not a dunder
    /// or if the dunder is not in the known mapping.
    /// For known dunders, returns the mapped name (e.g., __str__ → ToString).
    /// Unknown dunders are now rejected at compile time by SignatureValidator (SPY0414).
    /// </summary>
    public static string? ResolveCSharpName(string name)
    {
        if (!IsDunderMethod(name))
            return null;
        return GetCSharpName(name);
    }
}
