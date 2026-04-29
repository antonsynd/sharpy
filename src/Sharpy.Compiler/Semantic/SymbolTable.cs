using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Manages all scopes and symbols during semantic analysis
/// </summary>
// TODO(#610): SymbolTable uses non-concurrent Dictionary and Stack, making it
// unsuitable for concurrent access. The parallel compilation design
// (docs/design/parallel-compilation.md) creates per-file instances during name
// resolution, then merges into a read-only GlobalSymbolTable for parallel type
// checking.
public class SymbolTable : IGlobalSymbolTable
{
    private readonly Stack<Scope> _scopeStack = new();
    private readonly Scope _globalScope;
    private readonly BuiltinRegistry _builtins;
    private readonly Dictionary<string, Scope> _moduleScopes = new();

    /// <summary>
    /// Tracks variables that were declared inside a now-exited block scope but are
    /// no longer visible in any active outer scope. Populated by <see cref="ExitScope"/>
    /// for block-like scopes (if, for, while, try, with, except, match, comprehension).
    /// Used to enhance SPY0200 "Undefined identifier" diagnostics with a hint that the
    /// variable was block-scoped (unlike Python, where for-loop and except variables
    /// leak into the enclosing function).
    ///
    /// Cleared when entering a new function-like scope so that exited variables from
    /// one function aren't surfaced as suggestions in a sibling/nested function.
    /// </summary>
    private readonly Dictionary<string, (string BlockType, int Line)> _exitedVariables = new();

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
        // Exited-variable tracking is only meaningful within the same function body.
        // When entering a new function-like scope (function/lambda/pre-pass), clear
        // any prior entries so block-scoped variables from one function aren't
        // surfaced as hints inside a sibling or nested function.
        if (IsFunctionLikeScope(name))
        {
            _exitedVariables.Clear();
        }

        var newScope = new Scope(name, CurrentScope);
        _scopeStack.Push(newScope);
    }

    /// <summary>
    /// Enter (or re-enter) a per-module scope that is a direct child of the global scope.
    /// Module scopes isolate per-file declarations so that same-named types in different
    /// modules don't collide. The scope is lazily created on first call and reused on
    /// subsequent calls for the same module name.
    /// </summary>
    public void EnterModuleScope(string moduleName)
    {
        if (CurrentScope != _globalScope)
        {
            throw new InvalidOperationException(
                $"EnterModuleScope must be called from the global scope, but current scope is '{CurrentScope.Name}'");
        }

        if (!_moduleScopes.TryGetValue(moduleName, out var moduleScope))
        {
            moduleScope = new Scope($"module:{moduleName}", _globalScope);
            _moduleScopes[moduleName] = moduleScope;
        }

        // Exited-variable tracking is per-function within a module — clear when
        // crossing a module boundary so hints don't leak between modules.
        _exitedVariables.Clear();

        _scopeStack.Push(moduleScope);
    }

    /// <summary>
    /// Returns the module scope for the given module name, or null if it hasn't been created yet.
    /// </summary>
    public Scope? GetModuleScope(string moduleName)
    {
        return _moduleScopes.GetValueOrDefault(moduleName);
    }

    public void ExitScope()
    {
        if (_scopeStack.Count <= 1)
        {
            throw new InvalidOperationException("Cannot exit global scope");
        }

        // Before popping, record any block-scoped variables that are about to go
        // out of view. This powers enhanced SPY0200 diagnostics that explain to
        // Python developers why a `for` / `if` / `with` / comprehension variable
        // is unreachable outside its block in Sharpy.
        var scope = _scopeStack.Peek();
        var blockType = GetBlockTypeFromScopeName(scope.Name);
        if (blockType != null)
        {
            var parent = scope.Parent;
            foreach (var symbol in scope.GetAllSymbols())
            {
                if (symbol is not VariableSymbol variable)
                    continue;
                if (variable.DeclarationLine is not int declLine)
                    continue;

                // Skip if a same-named symbol is still visible in an outer active
                // scope — in that case the identifier resolves successfully and
                // the hint would be misleading (false positive for shadowing).
                if (parent != null && parent.Lookup(symbol.Name) != null)
                    continue;

                // Last writer wins: the most recently exited block is the most
                // useful suggestion in the common single-block case.
                _exitedVariables[symbol.Name] = (blockType, declLine);
            }
        }

        _scopeStack.Pop();
    }

    /// <summary>
    /// Looks up a variable that was previously declared inside a now-exited
    /// block scope (if/for/while/try/with/except/match/comprehension) within
    /// the current function body. Used by <c>TypeChecker</c> to enhance the
    /// SPY0200 "Undefined identifier" diagnostic with an explanation of
    /// Sharpy's block-scoping rules for Python developers.
    /// </summary>
    public bool TryGetExitedVariable(string name, out string blockType, out int line)
    {
        if (_exitedVariables.TryGetValue(name, out var info))
        {
            blockType = info.BlockType;
            line = info.Line;
            return true;
        }

        blockType = string.Empty;
        line = 0;
        return false;
    }

    /// <summary>
    /// Maps internal scope names (used as <c>EnterScope</c> tags throughout the
    /// semantic pipeline) to the user-facing block-type label that appears in
    /// SPY0200 diagnostics. Returns <c>null</c> for non-block scopes (functions,
    /// classes, modules, etc.) which should not contribute to exited-variable tracking.
    /// </summary>
    private static string? GetBlockTypeFromScopeName(string scopeName)
    {
        return scopeName switch
        {
            "if-then" or "elif" or "if-else" => "if",
            "while-body" => "while",
            "for-body" => "for",
            "try" or "try-else" or "finally" => "try",
            "except" => "except",
            "with" => "with",
            "match-case" or "match-arm" => "match",
            "list-comprehension" or "set-comprehension"
                or "dict-comprehension" or "dict-spread-comprehension" => "comprehension",
            _ => null,
        };
    }

    /// <summary>
    /// Returns true if the scope name represents a function-like boundary
    /// (function body, lambda, or type-checker pre-pass). Crossing such a
    /// boundary invalidates exited-variable tracking from the prior function.
    /// </summary>
    private static bool IsFunctionLikeScope(string scopeName)
    {
        return scopeName == "lambda"
            || scopeName.StartsWith("function:", StringComparison.Ordinal)
            || scopeName.StartsWith("pre-pass:", StringComparison.Ordinal);
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
    /// Case-insensitive symbol lookup, used for Python-style names that don't match
    /// CLR PascalCase conventions (e.g., "defaultdict" → "DefaultDict").
    /// </summary>
    public Symbol? LookupCaseInsensitive(string name)
    {
        foreach (var symbol in _globalScope.GetAllSymbols())
        {
            if (string.Equals(symbol.Name, name, StringComparison.OrdinalIgnoreCase))
                return symbol;
        }
        return null;
    }

    /// <summary>
    /// Updates an existing symbol in the scope chain.
    /// Used to update function symbols with resolved return types during type checking.
    /// </summary>
    public bool UpdateSymbol(Symbol symbol)
    {
        return CurrentScope.Update(symbol);
    }

    /// <summary>
    /// Defines function overloads for a given name in the current scope.
    /// Used when from-importing overloaded functions (e.g., from os.path import join).
    /// </summary>
    public void DefineFunctionOverloads(string name, List<FunctionSymbol> overloads)
    {
        CurrentScope.DefineFunctionOverloads(name, overloads);
    }

    /// <summary>
    /// Looks up function overloads by name, walking the scope chain.
    /// Returns null if no overloads are registered for the given name.
    /// </summary>
    public List<FunctionSymbol>? LookupFunctionOverloads(string name)
    {
        return CurrentScope.LookupFunctionOverloads(name);
    }

    public Scope CurrentScope => _scopeStack.Peek();
    public Scope GlobalScope => _globalScope;
    public int ScopeDepth => _scopeStack.Count;
    internal BuiltinRegistry BuiltinRegistry => _builtins;

    /// <summary>
    /// Looks up a symbol by name, searching all module scopes (and their parents).
    /// Used when the current scope is the global scope but symbols may be in module scopes.
    /// Returns the first match found, or null if not found in any module scope.
    /// </summary>
    public Symbol? LookupInModuleScopes(string name)
    {
        foreach (var (_, scope) in _moduleScopes)
        {
            var symbol = scope.Lookup(name, searchParent: false);
            if (symbol != null)
                return symbol;
        }
        return null;
    }

    /// <summary>
    /// Returns all symbols from all module scopes (excluding the global scope).
    /// Used by LSP handlers and metrics that need to see all project symbols
    /// regardless of which module scope they're in.
    /// </summary>
    public IEnumerable<Symbol> GetAllModuleScopeSymbols()
    {
        foreach (var (_, scope) in _moduleScopes)
        {
            foreach (var symbol in scope.GetAllSymbols())
            {
                yield return symbol;
            }
        }
    }

    /// <summary>
    /// Collects all visible symbol names by walking the scope chain from the current scope
    /// up through all parent scopes. Used for "did you mean?" suggestions.
    /// </summary>
    public IEnumerable<string> GetVisibleSymbolNames()
    {
        var names = new HashSet<string>();
        var scope = CurrentScope;
        while (scope != null)
        {
            foreach (var symbol in scope.GetAllSymbols())
                names.Add(symbol.Name);
            scope = scope.Parent;
        }
        return names;
    }

    /// <summary>
    /// Collects all visible symbols by walking the scope chain from the current scope
    /// up through all parent scopes. Later definitions shadow earlier ones.
    /// Used for LSP completion and workspace symbol queries.
    /// </summary>
    public IEnumerable<Symbol> GetVisibleSymbols()
    {
        var seen = new HashSet<string>();
        var scope = CurrentScope;
        while (scope != null)
        {
            foreach (var symbol in scope.GetAllSymbols())
            {
                if (seen.Add(symbol.Name))
                    yield return symbol;
            }
            scope = scope.Parent;
        }
    }

    /// <summary>
    /// Returns all visible symbols filtered by type. Walks the scope chain
    /// from the current scope up through all parent scopes.
    /// Used for LSP completion with kind filtering.
    /// </summary>
    public IEnumerable<T> GetVisibleSymbolsOfKind<T>() where T : Symbol
    {
        foreach (var symbol in GetVisibleSymbols())
        {
            if (symbol is T typed)
                yield return typed;
        }
    }

    /// <summary>
    /// Removes a symbol from the scope chain.
    /// Used during incremental compilation to invalidate stale cached symbols.
    /// </summary>
    public bool Remove(string name)
    {
        return CurrentScope.Remove(name);
    }

    /// <summary>
    /// Merges per-file symbol tables into a single unified table.
    /// Transfers symbol references (not copies) to preserve reference equality.
    /// </summary>
    /// <param name="perFileTables">Per-file tables with their module paths.</param>
    /// <param name="builtins">Builtin registry for the merged table.</param>
    /// <param name="diagnostics">Bag to report duplicate symbol errors.</param>
    /// <returns>A merged SymbolTable with all per-file symbols in their module scopes.</returns>
    internal static SymbolTable MergeFrom(
        IReadOnlyList<(string ModulePath, SymbolTable FileTable)> perFileTables,
        BuiltinRegistry builtins,
        Diagnostics.DiagnosticBag? diagnostics = null)
    {
        var merged = new SymbolTable(builtins);

        foreach (var (modulePath, fileTable) in perFileTables)
        {
            var fileModuleScope = fileTable.GetModuleScope(modulePath);
            if (fileModuleScope == null)
                continue;

            merged.EnterModuleScope(modulePath);
            try
            {
                foreach (var symbol in fileModuleScope.GetAllSymbols())
                {
                    if (!merged.TryDefine(symbol))
                    {
                        diagnostics?.AddError(
                            $"Duplicate definition '{symbol.Name}' across files",
                            symbol.DeclarationLine, symbol.DeclarationColumn,
                            code: Diagnostics.DiagnosticCodes.Semantic.DuplicateDefinition);
                    }
                }

                foreach (var (name, overloads) in fileModuleScope.GetAllFunctionOverloads())
                {
                    merged.DefineFunctionOverloads(name, overloads);
                }
            }
            finally
            {
                merged.ExitScope();
            }
        }

        return merged;
    }
}
