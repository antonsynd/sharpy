using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Codegen-owned mapping of Python dunder method names to their C# equivalents.
/// This is a codegen concern (it decides what C# method names to emit), not a naming convention concern.
/// </summary>
internal static class DunderMapping
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
        { DunderNames.Iter, "GetEnumerator" },    // For IEnumerable
        { DunderNames.Reversed, "GetReverseEnumerator" }, // For reverse iteration
        // __bool__ is handled as special codegen (operator true/false), not a simple name mapping
    };

#if DEBUG
    static DunderMapping()
    {
        // Verify all protocol dunders with CLR mappings are in _dunderMethodMap
        foreach (var protocol in ProtocolRegistry.GetAllProtocols())
        {
            if (protocol.ClrMethodName != null && !_dunderMethodMap.ContainsKey(protocol.DunderName))
            {
                System.Diagnostics.Debug.Assert(false,
                    $"Protocol '{protocol.DunderName}' with CLR mapping '{protocol.ClrMethodName}' " +
                    $"is missing from DunderMapping._dunderMethodMap. Add: {{ \"{protocol.DunderName}\", \"...\" }}");
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
    /// Resolve the C# name for a dunder method. Returns null if the name is not a dunder.
    /// For known dunders, returns the mapped name (e.g., __str__ → ToString).
    /// For unknown dunders, returns the transformed name (e.g., __add__ → __Add__).
    /// </summary>
    public static string? ResolveCSharpName(string name)
    {
        if (!IsDunderMethod(name))
            return null;
        return GetCSharpName(name) ?? TransformUnknownDunder(name);
    }

    /// <summary>
    /// Transform an unknown dunder method (not in the map) by capitalizing inner segments.
    /// Strips leading/trailing <c>__</c>, splits on <c>_</c>, capitalizes each segment, rejoins with <c>__</c> bookends.
    /// </summary>
    /// <example>
    /// <c>__add__</c> → <c>__Add__</c>, <c>__custom_method__</c> → <c>__CustomMethod__</c>
    /// </example>
    public static string TransformUnknownDunder(string name)
    {
        var middle = name[2..^2]; // Remove leading and trailing __
        var capitalizedMiddle = string.Join("", middle.Split('_').Select(Capitalize));
        return $"__{capitalizedMiddle}__";
    }

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

    private static string Capitalize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }
}
