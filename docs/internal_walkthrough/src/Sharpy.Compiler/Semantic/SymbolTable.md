# Walkthrough: SymbolTable.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SymbolTable.cs`

---

## Overview

The `SymbolTable` class is the central registry for all symbols (variables, functions, types, etc.) during semantic analysis. Think of it as the "memory" of the compiler during the analysis phase—it tracks what names have been declared, what they refer to, and in which scope they exist.

**Key Responsibilities:**
- Manage a stack of scopes (global, function, class, block scopes)
- Track all symbols defined in each scope
- Provide lookup operations to resolve names
- Integrate builtin types and functions from Sharpy.Core
- Enforce scoping rules (lexical scoping with shadowing)

**Where it fits in the pipeline:**
```
Parser → AST → [NameResolver] → SymbolTable → [TypeChecker] → Validated AST → CodeGen
                     ↑                ↓              ↑
                     └────────────────┴──────────────┘
                        Both use SymbolTable
```

The `SymbolTable` is shared between the `NameResolver` (first pass) and `TypeChecker` (second pass). The `NameResolver` populates it with declarations, and the `TypeChecker` uses it to verify that names are used correctly.

---

## Class/Type Structure

### Main Class: `SymbolTable`

```csharp
public class SymbolTable
{
    private readonly Stack<Scope> _scopeStack = new();
    private readonly Scope _globalScope;
    private readonly BuiltinRegistry _builtins;
    
    // Properties
    public Scope CurrentScope => _scopeStack.Peek();
    public Scope GlobalScope => _globalScope;
    public int ScopeDepth => _scopeStack.Count;
    public BuiltinRegistry BuiltinRegistry => _builtins;
}
```

**Design:** The symbol table uses a **stack-based scope management** system. Each scope is a dictionary mapping names to symbols, and scopes are nested in a parent-child relationship. When you look up a name, the symbol table searches from the current scope up through parent scopes until it finds the symbol or reaches the global scope.

**Core Data Structures:**
- `_scopeStack`: A stack of `Scope` objects, with the current scope on top
- `_globalScope`: Reference to the bottom-most scope (always present)
- `_builtins`: Registry of all builtin types and functions from Sharpy.Core

---

## Key Functions/Methods

### Constructor

```csharp
public SymbolTable(BuiltinRegistry builtins)
{
    _builtins = builtins;
    _globalScope = new Scope("global");
    _scopeStack.Push(_globalScope);
    PopulateBuiltins();
}
```

**What it does:**
- Initializes the symbol table with a global scope
- Takes a `BuiltinRegistry` dependency that contains all Sharpy.Core builtins
- Immediately populates the global scope with builtin types and functions

**Why it matters:** Every Sharpy program starts with the global scope pre-populated with builtins like `int`, `str`, `list`, `print()`, `len()`, etc. This makes them available everywhere without explicit imports.

---

### `PopulateBuiltins()`

```csharp
private void PopulateBuiltins()
{
    // Add builtin types
    foreach (var (name, typeSymbol) in _builtins.GetAllTypes())
    {
        _globalScope.Define(typeSymbol);
    }

    // Add builtin functions (only one symbol per function name)
    var addedFunctions = new HashSet<string>();
    foreach (var (name, funcSymbol) in _builtins.GetAllFunctions())
    {
        if (!addedFunctions.Contains(name))
        {
            _globalScope.Define(funcSymbol);
            addedFunctions.Add(name);
        }
    }
}
```

**What it does:**
- Adds all builtin types (`int`, `str`, `list`, `dict`, `bool`, etc.) to global scope
- Adds all builtin functions (`print`, `len`, `range`, `enumerate`, etc.) to global scope
- **Important:** Only adds ONE symbol per function name to the symbol table

**Implementation detail:** Many Sharpy builtin functions have multiple overloads (e.g., `print()` can take different argument types). However, the symbol table only stores one `FunctionSymbol` per name. The actual overload resolution happens later in the `TypeChecker` using `BuiltinRegistry.GetFunctionOverloads()`.

**Why this design?** The symbol table is focused on **name resolution** ("does this name exist?"), not **type resolution** ("which overload should I call?"). This separation of concerns keeps the symbol table simple.

---

### `EnterScope(string name)`

```csharp
public void EnterScope(string name)
{
    var newScope = new Scope(name, CurrentScope);
    _scopeStack.Push(newScope);
}
```

**What it does:**
- Creates a new scope with the given name
- Links it to the current scope as its parent
- Pushes it onto the stack, making it the new current scope

**When it's called:**
- When entering a function body
- When entering a class body
- When entering a control flow block (if, while, for, etc.)
- When entering any other construct that creates a new scope

**Example flow:**
```python
# Global scope
x = 10

def foo():           # EnterScope("foo")
    y = 20          # Define "y" in foo's scope
    if True:         # EnterScope("if")
        z = 30      # Define "z" in if's scope
                    # ExitScope() - back to foo's scope
                    # ExitScope() - back to global scope
```

---

### `ExitScope()`

```csharp
public void ExitScope()
{
    if (_scopeStack.Count <= 1)
    {
        throw new InvalidOperationException("Cannot exit global scope");
    }
    _scopeStack.Pop();
}
```

**What it does:**
- Pops the current scope off the stack
- Returns control to the parent scope
- **Safety check:** Prevents exiting the global scope (there must always be at least one scope)

**Why the safety check?** If you could exit the global scope, the `CurrentScope` property would throw an exception when trying to `Peek()` an empty stack. This guards against implementation bugs in the semantic analyzer.

---

### `Define(Symbol symbol)`

```csharp
public void Define(Symbol symbol)
{
    CurrentScope.Define(symbol);
}
```

**What it does:**
- Adds a symbol to the **current** scope
- Delegates to `Scope.Define()`, which handles duplicate detection

**Important behavior (from `Scope.Define()`):**
- Variables can be redefined in the same scope (Python-like behavior: `x = 1; x = "hello"`)
- **But** constants, functions, and types cannot be redefined
- Attempting to redefine a constant/function/type throws a `SemanticError`

**Example:**
```python
x = 10       # Define x as int
x = "hello"  # OK: Redefine x as str (Python allows this)

def foo(): pass
def foo(): pass  # ERROR: Cannot redefine function
```

---

### `Lookup(string name, bool searchParents = true)`

```csharp
public Symbol? Lookup(string name, bool searchParents = true)
{
    return CurrentScope.Lookup(name, searchParents);
}
```

**What it does:**
- Searches for a symbol by name
- Starts from the current scope
- If `searchParents` is true (default), walks up through parent scopes
- Returns `null` if not found

**How scope traversal works:**
1. Check current scope's dictionary
2. If not found and `searchParents` is true, recursively check parent scope
3. Continue until found or reach global scope
4. Return null if never found

**Example:**
```python
x = 10              # Global scope

def foo():
    y = 20          # foo's scope
    print(x)        # Lookup "x": Not in foo's scope → Check global → Found!
    print(y)        # Lookup "y": Found in foo's scope
    print(z)        # Lookup "z": Not in foo's scope → Not in global → null → ERROR
```

---

### Type-Specific Lookup Methods

```csharp
public TypeSymbol? LookupType(string name)
public FunctionSymbol? LookupFunction(string name)
public VariableSymbol? LookupVariable(string name)
```

**What they do:**
- Convenience methods that call `Lookup()` and cast to the specific symbol type
- Return `null` if the symbol doesn't exist OR if it's the wrong type

**Example:**
```python
x = 10                  # VariableSymbol
def foo(): pass         # FunctionSymbol

LookupVariable("x")     # Returns VariableSymbol
LookupVariable("foo")   # Returns null (foo is a function, not a variable)
LookupFunction("foo")   # Returns FunctionSymbol
```

**Why this is useful:** Type checking often needs to verify that a name refers to the correct kind of symbol (e.g., you can't call a variable like a function).

---

## Properties

### `CurrentScope`

```csharp
public Scope CurrentScope => _scopeStack.Peek();
```

**What it is:** The scope at the top of the stack—the most recently entered scope that hasn't been exited yet.

**Usage:** Used extensively throughout semantic analysis to define and lookup symbols in the current context.

---

### `GlobalScope`

```csharp
public Scope GlobalScope => _globalScope;
```

**What it is:** Reference to the bottom-most scope, always available.

**Usage:** Useful when you need to access global symbols directly without traversing the scope stack.

---

### `ScopeDepth`

```csharp
public int ScopeDepth => _scopeStack.Count;
```

**What it is:** The current nesting level of scopes.

**Values:**
- `1` = Global scope only
- `2` = Inside one nested scope (e.g., a function)
- `3` = Inside two nested scopes (e.g., a function with an if block)
- And so on...

**Usage:** Primarily for debugging and logging. You can track how deep you are in the scope hierarchy.

---

### `BuiltinRegistry`

```csharp
public BuiltinRegistry BuiltinRegistry => _builtins;
```

**What it is:** Direct access to the builtin registry for function overload resolution.

**Usage:** The `TypeChecker` uses this to get all overloads of a function for type-based dispatch.

---

## Dependencies

### Direct Dependencies

1. **`Scope`** (`Scope.cs`)
   - Manages individual scopes (name → symbol dictionaries)
   - Handles parent-child relationships
   - Implements lookup with parent traversal

2. **`Symbol` hierarchy** (`Symbol.cs`)
   - `Symbol` (base)
   - `VariableSymbol`
   - `FunctionSymbol`
   - `TypeSymbol`
   - `ModuleSymbol`
   - `ParameterSymbol`
   - `PropertySymbol`

3. **`BuiltinRegistry`** (`BuiltinRegistry.cs`)
   - Provides all builtin types and functions
   - Uses reflection-based discovery to load Sharpy.Core

### Indirect Dependencies

4. **`SemanticError`** (`SemanticError.cs`)
   - Thrown by `Scope.Define()` when symbols are redefined illegally

5. **`CachedModuleDiscovery`** (via `BuiltinRegistry`)
   - Used to discover functions from Sharpy.Core assembly

### Usage by Other Components

The `SymbolTable` is used by:
- **`NameResolver`** - First pass: populates symbol table with declarations
- **`TypeChecker`** - Second pass: looks up symbols to verify types
- **`TypeResolver`** - Resolves type annotations to `SemanticType`
- **`AccessValidator`** - Checks access levels of symbols

---

## Patterns and Design Decisions

### 1. **Stack-Based Scope Management**

**Pattern:** Use a stack to track nested scopes, always operating on the top of the stack.

**Why?** This naturally mirrors the lexical scoping of the language. When you enter a new scope, push; when you exit, pop. The stack order exactly matches the source code nesting.

**Alternative considered:** A tree structure where each scope explicitly tracks its children. Rejected because it's more complex and doesn't match the linear flow of semantic analysis.

---

### 2. **Separation of Name Resolution and Overload Resolution**

**Pattern:** Symbol table stores ONE symbol per function name, even if the function has multiple overloads.

**Why?** 
- **Name resolution** ("Does `print` exist?") is different from **overload resolution** ("Which `print` overload matches these arguments?")
- Symbol table focuses on name resolution
- `TypeChecker` handles overload resolution using `BuiltinRegistry.GetFunctionOverloads()`

**Code evidence:**
```csharp
// Only add one symbol per function name
var addedFunctions = new HashSet<string>();
foreach (var (name, funcSymbol) in _builtins.GetAllFunctions())
{
    if (!addedFunctions.Contains(name))
    {
        _globalScope.Define(funcSymbol);
        addedFunctions.Add(name);
    }
}
```

---

### 3. **Two-Pass Symbol Resolution**

**Pattern:** Symbol table is populated in multiple passes:
1. **NameResolver** first pass: Declare all types, functions, and top-level variables
2. **NameResolver** second pass: Resolve inheritance relationships
3. **TypeChecker** pass: Use the fully-populated symbol table to type-check expressions

**Why?** Forward references. In Sharpy (like Python), you can reference a class or function before it's defined in the source file:

```python
def foo():
    return Bar()  # Bar is used before it's defined

class Bar:
    pass
```

The symbol table enables this by separating declaration (adding the name) from validation (checking it's used correctly).

---

### 4. **Immutable Symbol Records**

**Pattern:** All `Symbol` types are C# `record` types (immutable).

**Why?**
- Thread-safe (if we parallelize semantic analysis in the future)
- Prevents accidental mutation during analysis
- Makes reasoning about state easier (symbols don't change after creation)

**Code evidence:**
```csharp
public abstract record Symbol { ... }
public record VariableSymbol : Symbol { ... }
public record FunctionSymbol : Symbol { ... }
```

---

### 5. **Global Scope Always Present**

**Pattern:** The global scope is never removed from the stack. `ExitScope()` prevents popping it.

**Why?**
- Simplifies code: `CurrentScope` always works
- Prevents bugs: Can't accidentally operate with no scope
- Matches language semantics: Global scope is always accessible

---

## Debugging Tips

### 1. **Tracking Scope Depth**

When debugging scope-related issues, add logging to track scope operations:

```csharp
public void EnterScope(string name)
{
    Console.WriteLine($"[DEBUG] EnterScope: {name}, depth will be {ScopeDepth + 1}");
    var newScope = new Scope(name, CurrentScope);
    _scopeStack.Push(newScope);
}

public void ExitScope()
{
    Console.WriteLine($"[DEBUG] ExitScope: {CurrentScope.Name}, depth was {ScopeDepth}");
    if (_scopeStack.Count <= 1)
        throw new InvalidOperationException("Cannot exit global scope");
    _scopeStack.Pop();
}
```

This helps identify mismatched `EnterScope`/`ExitScope` calls.

---

### 2. **Inspecting Symbol Table State**

Add a helper method to dump the current symbol table state:

```csharp
public void DumpScopes()
{
    Console.WriteLine("=== Symbol Table Dump ===");
    var scopes = _scopeStack.Reverse().ToList();
    for (int i = 0; i < scopes.Count; i++)
    {
        var scope = scopes[i];
        Console.WriteLine($"Scope {i}: {scope.Name}");
        foreach (var symbol in scope.GetAllSymbols())
        {
            Console.WriteLine($"  - {symbol.Name} ({symbol.Kind})");
        }
    }
}
```

Call this when you want to see what symbols are defined in each scope.

---

### 3. **Common Issues and Solutions**

**Issue:** "Symbol not found" error for a builtin function
- **Check:** Is `PopulateBuiltins()` being called?
- **Check:** Does the `BuiltinRegistry` contain the function?
- **Fix:** Ensure `BuiltinRegistry.LoadBuiltins()` includes the function

**Issue:** "Cannot exit global scope" exception
- **Cause:** Mismatched `EnterScope`/`ExitScope` calls
- **Debug:** Add logging to track scope operations (see tip #1)
- **Fix:** Ensure every `EnterScope` has a corresponding `ExitScope`

**Issue:** Variables are shadowing when they shouldn't (or vice versa)
- **Check:** Is `Lookup()` being called with `searchParents = true`?
- **Check:** Is the symbol being defined in the correct scope?
- **Debug:** Use `DumpScopes()` to see what's defined where

**Issue:** Function overloads not resolving correctly
- **Reminder:** Symbol table only stores ONE symbol per function name
- **Solution:** Use `BuiltinRegistry.GetFunctionOverloads()` for overload resolution
- **Location:** This happens in `TypeChecker`, not `SymbolTable`

---

### 4. **Visualizing Scope Hierarchy**

When debugging complex nesting, visualize the scope stack:

```
Global Scope
  ├─ x: int
  ├─ print: function
  └─ [Current] foo: function scope
       ├─ y: str
       └─ [Current] if: block scope
            └─ z: bool
```

The stack looks like: `[Global] → [foo] → [if]`

---

## Contribution Guidelines

### Adding New Symbol Types

If you need a new kind of symbol (e.g., `NamespaceSymbol`, `AliasSymbol`):

1. **Define the symbol type** in `Symbol.cs`:
   ```csharp
   public record NamespaceSymbol : Symbol
   {
       public List<Symbol> Members { get; init; } = new();
   }
   ```

2. **Add the symbol kind** to `SymbolKind` enum:
   ```csharp
   public enum SymbolKind
   {
       Variable, Function, Type, Module, Property,
       Namespace  // Add this
   }
   ```

3. **Add a lookup helper** (optional but recommended):
   ```csharp
   public NamespaceSymbol? LookupNamespace(string name)
   {
       return Lookup(name) as NamespaceSymbol;
   }
   ```

4. **Update tests** in `Sharpy.Compiler.Tests/Semantic/SymbolTableTests.cs`

---

### Modifying Scope Behavior

If you need to change how scopes work (e.g., add block-level scoping rules):

1. **Modify `Scope.cs`** first (not `SymbolTable.cs`)
   - Example: Add support for `const` bindings that can't be shadowed

2. **Keep `SymbolTable.cs` focused on scope management**
   - It should delegate actual scope logic to `Scope`

3. **Add tests** for the new behavior

---

### Performance Considerations

**Current performance:**
- Lookup: O(d) where d is scope depth (typically 1-4)
- Define: O(1) (dictionary insertion)
- EnterScope/ExitScope: O(1)

**If you need to optimize:**
- Consider caching frequently-looked-up symbols
- Profile before optimizing (scope operations are usually not the bottleneck)
- Don't optimize prematurely—symbol table operations are already fast

---

### Testing New Features

When adding features to `SymbolTable`:

1. **Write unit tests** for the symbol table itself
2. **Write integration tests** that use the symbol table through `NameResolver` and `TypeChecker`
3. **Test error cases** (e.g., duplicate definitions, missing symbols)
4. **Test scope nesting** edge cases

Example test structure:
```csharp
[Fact]
public void TestNestedScopeDefinitions()
{
    var symbolTable = new SymbolTable(new BuiltinRegistry());
    
    symbolTable.Define(new VariableSymbol { Name = "x", Type = SemanticType.Int });
    symbolTable.EnterScope("inner");
    symbolTable.Define(new VariableSymbol { Name = "y", Type = SemanticType.String });
    
    Assert.NotNull(symbolTable.Lookup("x")); // Should find x from parent
    Assert.NotNull(symbolTable.Lookup("y")); // Should find y in current
    
    symbolTable.ExitScope();
    Assert.NotNull(symbolTable.Lookup("x")); // Should still find x
    Assert.Null(symbolTable.Lookup("y"));    // y is out of scope
}
```

---

## Summary

The `SymbolTable` is a foundational component that enables name resolution throughout semantic analysis. It's simple by design—just a stack of scopes with lookup operations—but this simplicity is intentional. Complex logic like overload resolution, type checking, and access validation are handled by other components that *use* the symbol table.

**Key takeaways:**
- Symbol table = stack of scopes
- Scopes = dictionaries mapping names to symbols
- Lookup walks up the scope chain
- Builtins are pre-populated in global scope
- One symbol per name (overload resolution happens elsewhere)
- Always at least one scope (global)

When working with this code, remember: **name resolution is separate from type resolution**. The symbol table answers "does this name exist?" The type checker answers "is this name used correctly?"
