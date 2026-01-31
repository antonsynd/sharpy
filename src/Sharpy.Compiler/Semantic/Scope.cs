namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Manages symbol scopes during semantic analysis
/// </summary>
public class Scope
{
    private readonly Dictionary<string, Symbol> _symbols = new();
    private readonly Scope? _parent;
    public string Name { get; }

    public Scope(string name, Scope? parent = null)
    {
        Name = name;
        _parent = parent;
    }

    public void Define(Symbol symbol)
    {
        if (_symbols.TryGetValue(symbol.Name, out var existingSymbol))
        {
            // Allow redefinition for non-const variables
            // This enables Python-like behavior where variables can be reassigned to different types
            if (existingSymbol is VariableSymbol existingVar && !existingVar.IsConstant &&
                symbol is VariableSymbol newVar && !newVar.IsConstant)
            {
                // Replace the existing symbol with the new one (redefinition)
                _symbols[symbol.Name] = symbol;
                return;
            }

            // Allow shadowing builtins (which have no source location)
            // This matches Python behavior where user code can shadow builtins like print, len, etc.
            bool isBuiltin = existingSymbol.DeclarationLine == null;
            if (isBuiltin)
            {
                _symbols[symbol.Name] = symbol;
                return;
            }

            // For all other cases (constants, functions, types, etc.), redefinition is an error
            throw new InvalidOperationException($"Symbol '{symbol.Name}' is already defined in this scope");
        }

        _symbols[symbol.Name] = symbol;
    }

    public Symbol? Lookup(string name, bool searchParent = true)
    {
        if (_symbols.TryGetValue(name, out var symbol))
        {
            return symbol;
        }

        if (searchParent && _parent != null)
        {
            return _parent.Lookup(name, searchParent);
        }

        return null;
    }

    public bool Contains(string name)
    {
        return _symbols.ContainsKey(name);
    }

    /// <summary>
    /// Updates an existing symbol in this scope or a parent scope.
    /// Used to update function symbols with resolved return types during type checking.
    /// </summary>
    public bool Update(Symbol symbol)
    {
        if (_symbols.ContainsKey(symbol.Name))
        {
            _symbols[symbol.Name] = symbol;
            return true;
        }

        if (_parent != null)
        {
            return _parent.Update(symbol);
        }

        return false;
    }

    public IEnumerable<Symbol> GetAllSymbols()
    {
        return _symbols.Values;
    }
}
