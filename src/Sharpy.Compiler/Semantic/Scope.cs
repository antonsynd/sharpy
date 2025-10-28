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
        if (_symbols.ContainsKey(symbol.Name))
        {
            throw new SemanticError($"Symbol '{symbol.Name}' is already defined in this scope");
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

    public IEnumerable<Symbol> GetAllSymbols()
    {
        return _symbols.Values;
    }
}
