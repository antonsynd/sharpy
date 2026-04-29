namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Read-only interface for cross-file symbol lookups.
/// Separates mutable per-file state from immutable shared state,
/// enabling downstream consumers (CodeGen, LSP) to depend on the
/// read-only contract while name resolution retains mutable access.
/// </summary>
public interface IGlobalSymbolTable
{
    Symbol? Lookup(string name, bool searchParents = true);

    TypeSymbol? LookupType(string name);

    FunctionSymbol? LookupFunction(string name);

    VariableSymbol? LookupVariable(string name);

    List<FunctionSymbol>? LookupFunctionOverloads(string name);

    Symbol? LookupInModuleScopes(string name);

    IEnumerable<Symbol> GetAllModuleScopeSymbols();

    Scope? GetModuleScope(string moduleName);

    Scope GlobalScope { get; }

    Scope CurrentScope { get; }

    IEnumerable<Symbol> GetVisibleSymbols();

    IEnumerable<string> GetVisibleSymbolNames();

    IEnumerable<T> GetVisibleSymbolsOfKind<T>() where T : Symbol;
}
