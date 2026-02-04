namespace Sharpy.Compiler.Semantic.Collections;

/// <summary>
/// A HashSet for Symbol instances that enforces reference equality.
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
/// Use this for any HashSet that stores Symbol instances.
/// </remarks>
public sealed class SymbolSet : HashSet<Symbol>
{
    /// <summary>
    /// Initializes a new empty SymbolSet with reference equality.
    /// </summary>
    public SymbolSet() : base(ReferenceEqualityComparer.Instance) { }

    /// <summary>
    /// Initializes a new SymbolSet containing the specified symbols with reference equality.
    /// </summary>
    /// <param name="collection">The symbols to add to the set.</param>
    public SymbolSet(IEnumerable<Symbol> collection)
        : base(collection, ReferenceEqualityComparer.Instance) { }

    /// <summary>
    /// Initializes a new empty SymbolSet with the specified initial capacity and reference equality.
    /// </summary>
    /// <param name="capacity">The initial capacity of the set.</param>
    public SymbolSet(int capacity)
        : base(capacity, ReferenceEqualityComparer.Instance) { }
}

/// <summary>
/// A HashSet for TypeSymbol instances that enforces reference equality.
/// </summary>
/// <remarks>
/// Specialized version for <see cref="TypeSymbol"/> to avoid casting when working
/// specifically with type symbols. Inherits reference equality behavior from
/// <see cref="Symbol.Equals(Symbol?)"/> and <see cref="Symbol.GetHashCode()"/>.
/// </remarks>
public sealed class TypeSymbolSet : HashSet<TypeSymbol>
{
    /// <summary>
    /// Initializes a new empty TypeSymbolSet with reference equality.
    /// </summary>
    public TypeSymbolSet() : base(ReferenceEqualityComparer.Instance) { }

    /// <summary>
    /// Initializes a new TypeSymbolSet containing the specified type symbols with reference equality.
    /// </summary>
    /// <param name="collection">The type symbols to add to the set.</param>
    public TypeSymbolSet(IEnumerable<TypeSymbol> collection)
        : base(collection, ReferenceEqualityComparer.Instance) { }

    /// <summary>
    /// Initializes a new empty TypeSymbolSet with the specified initial capacity and reference equality.
    /// </summary>
    /// <param name="capacity">The initial capacity of the set.</param>
    public TypeSymbolSet(int capacity)
        : base(capacity, ReferenceEqualityComparer.Instance) { }
}
