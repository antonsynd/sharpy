namespace Sharpy.Compiler.Semantic.Collections;

/// <summary>
/// A Dictionary with Symbol keys that enforces reference equality.
/// </summary>
/// <remarks>
/// While <see cref="Symbol"/> already implements reference equality via overridden
/// <see cref="Symbol.Equals(Symbol?)"/> and <see cref="Symbol.GetHashCode()"/>,
/// this wrapper class:
/// <list type="bullet">
///   <item>Makes the reference equality intent explicit at the declaration site</item>
///   <item>Provides a safety net if Symbol's equality implementation ever changes</item>
///   <item>Matches the pattern used elsewhere in the codebase with ReferenceEqualityComparer</item>
/// </list>
/// Use this for any Dictionary that uses Symbol as a key type.
/// </remarks>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public sealed class SymbolDictionary<TValue> : Dictionary<Symbol, TValue>
{
    /// <summary>
    /// Initializes a new empty SymbolDictionary with reference equality.
    /// </summary>
    public SymbolDictionary() : base(ReferenceEqualityComparer.Instance) { }

    /// <summary>
    /// Initializes a new empty SymbolDictionary with the specified initial capacity and reference equality.
    /// </summary>
    /// <param name="capacity">The initial capacity of the dictionary.</param>
    public SymbolDictionary(int capacity)
        : base(capacity, ReferenceEqualityComparer.Instance) { }
}

/// <summary>
/// A Dictionary with TypeSymbol keys that enforces reference equality.
/// </summary>
/// <remarks>
/// Specialized version for <see cref="TypeSymbol"/> to avoid casting when working
/// specifically with type symbols. Inherits reference equality behavior from
/// <see cref="Symbol.Equals(Symbol?)"/> and <see cref="Symbol.GetHashCode()"/>.
/// </remarks>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public sealed class TypeSymbolDictionary<TValue> : Dictionary<TypeSymbol, TValue>
{
    /// <summary>
    /// Initializes a new empty TypeSymbolDictionary with reference equality.
    /// </summary>
    public TypeSymbolDictionary() : base(ReferenceEqualityComparer.Instance) { }

    /// <summary>
    /// Initializes a new empty TypeSymbolDictionary with the specified initial capacity and reference equality.
    /// </summary>
    /// <param name="capacity">The initial capacity of the dictionary.</param>
    public TypeSymbolDictionary(int capacity)
        : base(capacity, ReferenceEqualityComparer.Instance) { }
}
