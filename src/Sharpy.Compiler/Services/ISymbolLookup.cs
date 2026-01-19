using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for looking up symbols in the symbol table.
/// Provides a simplified, read-only view for consumers that don't need
/// to modify the symbol table.
/// </summary>
public interface ISymbolLookup
{
    /// <summary>
    /// Look up a symbol by name in the current scope and parent scopes.
    /// </summary>
    Symbol? Lookup(string name);

    /// <summary>
    /// Look up a type symbol by name.
    /// </summary>
    TypeSymbol? LookupType(string name);

    /// <summary>
    /// Look up a type alias by name.
    /// </summary>
    TypeAliasSymbol? LookupTypeAlias(string name);

    /// <summary>
    /// Look up a function symbol by name.
    /// </summary>
    FunctionSymbol? LookupFunction(string name);

    /// <summary>
    /// Check if a symbol exists in the current scope (not parent scopes).
    /// </summary>
    bool ExistsInCurrentScope(string name);
}
