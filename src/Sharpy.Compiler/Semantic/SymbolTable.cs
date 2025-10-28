namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Manages all scopes and symbols during semantic analysis
/// </summary>
public class SymbolTable
{
    private Scope _currentScope;
    private readonly Scope _globalScope;

    public SymbolTable()
    {
        _globalScope = new Scope("global");
        _currentScope = _globalScope;
    }

    public void EnterScope(string name)
    {
        _currentScope = new Scope(name, _currentScope);
    }

    public void ExitScope()
    {
        if (_currentScope == _globalScope)
        {
            throw new InvalidOperationException("Cannot exit global scope");
        }

        _currentScope = _currentScope != null && _currentScope != _globalScope
            ? new Scope("parent", null) // Simplified - need proper parent tracking
            : _globalScope;
    }

    public void Define(Symbol symbol)
    {
        _currentScope.Define(symbol);
    }

    public Symbol? Lookup(string name)
    {
        return _currentScope.Lookup(name);
    }

    public Scope CurrentScope => _currentScope;
    public Scope GlobalScope => _globalScope;
}
