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

    TypeAliasSymbol? LookupTypeAlias(string name);

    /// <summary>
    /// Case-insensitive symbol lookup, used for Python-style names that don't match
    /// CLR PascalCase conventions (e.g., "defaultdict" -> "DefaultDict").
    /// </summary>
    Symbol? LookupCaseInsensitive(string name);

    List<FunctionSymbol>? LookupFunctionOverloads(string name);

    Symbol? LookupInModuleScopes(string name);

    IEnumerable<Symbol> GetAllModuleScopeSymbols();

    Scope? GetModuleScope(string moduleName);

    Scope GlobalScope { get; }

    Scope CurrentScope { get; }

    IEnumerable<Symbol> GetVisibleSymbols();

    IEnumerable<string> GetVisibleSymbolNames();

    IEnumerable<T> GetVisibleSymbolsOfKind<T>() where T : Symbol;

    /// <summary>
    /// Looks up a variable that was previously declared inside a now-exited
    /// block scope (if/for/while/try/with/except/match/comprehension) within
    /// the current function body. Used by <c>TypeChecker</c> to enhance the
    /// SPY0200 "Undefined identifier" diagnostic with an explanation of
    /// Sharpy's block-scoping rules for Python developers.
    /// </summary>
    bool TryGetExitedVariable(string name, out string blockType, out int line);
}
