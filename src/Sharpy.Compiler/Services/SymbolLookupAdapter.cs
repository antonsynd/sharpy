using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Adapter that wraps the existing SymbolTable to implement ISymbolLookup.
/// Provides a read-only view of the symbol table.
/// </summary>
public class SymbolLookupAdapter : ISymbolLookup
{
    private readonly SymbolTable _symbolTable;

    public SymbolLookupAdapter(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
    }

    public Symbol? Lookup(string name)
    {
        return _symbolTable.Lookup(name);
    }

    public TypeSymbol? LookupType(string name)
    {
        return _symbolTable.LookupType(name);
    }

    public TypeAliasSymbol? LookupTypeAlias(string name)
    {
        return _symbolTable.LookupTypeAlias(name);
    }

    public FunctionSymbol? LookupFunction(string name)
    {
        return _symbolTable.Lookup(name) as FunctionSymbol;
    }

    public bool ExistsInCurrentScope(string name)
    {
        return _symbolTable.Lookup(name, searchParents: false) != null;
    }

    /// <summary>
    /// Get the underlying SymbolTable for cases that need direct access.
    /// Use sparingly - prefer the interface methods.
    /// </summary>
    public SymbolTable UnderlyingTable => _symbolTable;
}
