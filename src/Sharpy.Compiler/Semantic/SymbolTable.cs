namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Manages all scopes and symbols during semantic analysis
/// </summary>
public class SymbolTable
{
    private readonly Stack<Scope> _scopeStack = new();
    private readonly Scope _globalScope;
    private readonly BuiltinRegistry _builtins;

    internal SymbolTable(BuiltinRegistry builtins)
    {
        _builtins = builtins;
        _globalScope = new Scope("global");
        _scopeStack.Push(_globalScope);

        // Populate global scope with builtins
        PopulateBuiltins();
    }

    private void PopulateBuiltins()
    {
        // Add builtin types
        var typeNames = new HashSet<string>();
        foreach (var (name, typeSymbol) in _builtins.GetAllTypes())
        {
            _globalScope.Define(typeSymbol);
            typeNames.Add(name);
        }

        // Add builtin functions (only add one symbol per function name, overload resolution happens later)
        // For functions with multiple overloads, only the first overload is added to the global scope.
        // The TypeChecker uses BuiltinRegistry.GetFunctionOverloads() for proper overload resolution.
        // Skip functions that have the same name as types (e.g., int(), str(), bool()) - these are
        // handled by TypeChecker which routes primitive type "constructor" calls to builtin function overloads.
        var addedFunctions = new HashSet<string>();
        foreach (var (name, funcSymbol) in _builtins.GetAllFunctions())
        {
            if (!addedFunctions.Contains(name) && !typeNames.Contains(name))
            {
                _globalScope.Define(funcSymbol);
                addedFunctions.Add(name);
            }
        }
    }

    public void EnterScope(string name)
    {
        var newScope = new Scope(name, CurrentScope);
        _scopeStack.Push(newScope);
    }

    public void ExitScope()
    {
        if (_scopeStack.Count <= 1)
        {
            throw new InvalidOperationException("Cannot exit global scope");
        }
        _scopeStack.Pop();
    }

    public void Define(Symbol symbol)
    {
        CurrentScope.Define(symbol);
    }

    /// <summary>
    /// Define a symbol only if it doesn't already exist in the current scope.
    /// Returns true if the symbol was defined, false if it already exists.
    /// </summary>
    public bool TryDefine(Symbol symbol)
    {
        if (CurrentScope.Contains(symbol.Name))
            return false;
        CurrentScope.Define(symbol);
        return true;
    }

    public Symbol? Lookup(string name, bool searchParents = true)
    {
        return CurrentScope.Lookup(name, searchParents);
    }

    public TypeSymbol? LookupType(string name)
    {
        return Lookup(name) as TypeSymbol;
    }

    public FunctionSymbol? LookupFunction(string name)
    {
        return Lookup(name) as FunctionSymbol;
    }

    public VariableSymbol? LookupVariable(string name)
    {
        return Lookup(name) as VariableSymbol;
    }

    public TypeAliasSymbol? LookupTypeAlias(string name)
    {
        return Lookup(name) as TypeAliasSymbol;
    }

    /// <summary>
    /// Updates an existing symbol in the scope chain.
    /// Used to update function symbols with resolved return types during type checking.
    /// </summary>
    public bool UpdateSymbol(Symbol symbol)
    {
        return CurrentScope.Update(symbol);
    }

    public Scope CurrentScope => _scopeStack.Peek();
    public Scope GlobalScope => _globalScope;
    public int ScopeDepth => _scopeStack.Count;
    internal BuiltinRegistry BuiltinRegistry => _builtins;

    /// <summary>
    /// Removes a symbol from the scope chain.
    /// Used during incremental compilation to invalidate stale cached symbols.
    /// </summary>
    public bool Remove(string name)
    {
        return CurrentScope.Remove(name);
    }
}
