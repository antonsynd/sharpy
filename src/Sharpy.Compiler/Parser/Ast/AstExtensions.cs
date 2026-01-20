using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Extension methods to help with AST immutability migration.
/// </summary>
public static class AstExtensions
{
    /// <summary>
    /// Converts a list to an immutable array.
    /// Use during migration from List to ImmutableArray.
    /// </summary>
    public static ImmutableArray<T> ToImmutableArraySafe<T>(this List<T>? list)
        => list?.ToImmutableArray() ?? ImmutableArray<T>.Empty;

    /// <summary>
    /// Converts an enumerable to an immutable array.
    /// </summary>
    public static ImmutableArray<T> ToImmutableArraySafe<T>(this IEnumerable<T>? items)
        => items?.ToImmutableArray() ?? ImmutableArray<T>.Empty;

    /// <summary>
    /// Creates an ImmutableArray builder for efficient building.
    /// </summary>
    public static ImmutableArray<T>.Builder CreateBuilder<T>(int initialCapacity = 4)
        => ImmutableArray.CreateBuilder<T>(initialCapacity);
}
