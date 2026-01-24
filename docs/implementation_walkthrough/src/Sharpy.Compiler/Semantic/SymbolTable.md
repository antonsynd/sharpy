# Walkthrough: SymbolTable.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SymbolTable.cs`

---

## Overview

`SymbolTable` is the central registry for managing all symbols (variables, functions, types, etc.) and their scopes during semantic analysis. It acts as the compiler's "phone book"—tracking what names are defined and where they're visible throughout the code.

**Role in the Compiler Pipeline:**
```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C#
                                           ↓
                                      SymbolTable
                                      (used by NameResolver,
                                       TypeResolver, TypeChecker)
```

The `SymbolTable` is created once at the start of semantic analysis and used throughout all semantic passes:
- **NameResolver**: Populates the symbol table with declarations
- **TypeResolver**: Looks up type symbols when resolving type annotations
- **TypeChecker**: Looks up variables, functions, and types during type checking

Think of `SymbolTable` as managing a stack of dictionaries—each dictionary is a scope (global, function, block), and the stack represents the nesting structure of your code. When you reference a variable, the SymbolTable searches from the top of the stack (current scope) down to the bottom (global scope).

---

## Class/Type Structure

### Main Class: `SymbolTable`

```csharp
public class SymbolTable
{
    private readonly Stack<Scope> _scopeStack = new();      // Stack of active scopes
    private readonly Scope _globalScope;                     // Root global scope
    private readonly BuiltinRegistry _builtins;              // Builtin types/functions

    public Scope CurrentScope => _scopeStack.Peek();
    public Scope GlobalScope => _globalScope;
    public int ScopeDepth => _scopeStack.Count;
    public BuiltinRegistry BuiltinRegistry => _builtins;
}
```

**Key Design Decisions:**
- **Stack-based scope management**: Mirrors the natural nesting structure of code blocks
- **Immutable global scope**: Always at the bottom of the stack, never popped
- **Separation of concerns**: Builtins come from `BuiltinRegistry`, user symbols tracked separately

**Properties:**

| Property | Type | Purpose |
|----------|------|---------|
| `CurrentScope` | `Scope` | The top of the scope stack (where new symbols are defined and lookups start) |
| `GlobalScope` | `Scope` | Direct access to the global scope (always at bottom of stack) |
| `ScopeDepth` | `int` | Number of nested scopes (1 = global only, 2+ = nested) |
| `BuiltinRegistry` | `BuiltinRegistry` | Access to builtin types/functions for overload resolution |

---

## Key Functions/Methods

### 1. Constructor: Initialization and Builtin Population

```csharp
public SymbolTable(BuiltinRegistry builtins)
{
    _builtins = builtins;
    _globalScope = new Scope("global");
    _scopeStack.Push(_globalScope);
    PopulateBuiltins();
}
```

**What happens during construction:**
1. Stores reference to `BuiltinRegistry`
2. Creates the global scope and pushes it onto the stack
3. Populates global scope with built-in types (int, str, list, etc.) and functions (print, len, range, etc.)

**Parameters:**
- `builtins`: The `BuiltinRegistry` containing all Sharpy.Core types and functions

**Connection to Pipeline:** This is called early in semantic analysis (typically in the constructor of `NameResolver` or `TypeChecker`) to initialize the symbol environment before processing any user code.

---

### 2. `PopulateBuiltins()` - Critical Initialization Logic

```csharp
private void PopulateBuiltins()
{
    // Add builtin types
    var typeNames = new HashSet<string>();
    foreach (var (name, typeSymbol) in _builtins.GetAllTypes())
    {
        _globalScope.Define(typeSymbol);
        typeNames.Add(name);  // Track type names
    }

    // Add builtin functions (only one symbol per function name)
    var addedFunctions = new HashSet<string>();
    foreach (var (name, funcSymbol) in _builtins.GetAllFunctions())
    {
        // Skip if already added OR same name as type
        if (!addedFunctions.Contains(name) && !typeNames.Contains(name))
        {
            _globalScope.Define(funcSymbol);
            addedFunctions.Add(name);
        }
    }
}
```

**Implementation Details:**

1. **Type registration first**: All builtin types (int, str, bool, list, dict, etc.) are registered
2. **Track type names**: Stored in `typeNames` HashSet to avoid function collision
3. **Function registration with deduplication**: Only one function symbol per name, even with overloads
4. **Skip type-named functions**: Functions like `int()`, `str()`, `bool()` are NOT added to prevent collision

**Why skip functions with type names?**

In Python (and Sharpy), `int()`, `str()`, `bool()` look like function calls but are actually type constructors:

```python
x: int = 42        # 'int' resolved as TypeSymbol
y = int("123")     # TypeChecker routes to builtin int() function
```

The SymbolTable only adds the *type* symbol to avoid collision. The TypeChecker handles routing these "constructor calls" to the appropriate builtin function overloads via `BuiltinRegistry.GetFunctionOverloads()`.

**Overload Handling Philosophy:**

Only one `FunctionSymbol` per name is stored in the symbol table (the first overload encountered). This is intentional:
- Symbol table's job: **Name resolution** ("does this name exist?")
- TypeChecker's job: **Overload resolution** ("which overload matches these arguments?")

During type checking, the analyzer calls `BuiltinRegistry.GetFunctionOverloads()` to get all overloads and select the best match.

**Example:**
```python
# Builtin print() has multiple overloads:
# print(value: object) -> None
# print(*values: object) -> None
```

Symbol table stores one; TypeChecker gets all overloads from BuiltinRegistry for proper resolution.

---

### 3. Scope Management Methods

#### `EnterScope(string name)`

```csharp
public void EnterScope(string name)
{
    var newScope = new Scope(name, CurrentScope);  // Parent = current scope
    _scopeStack.Push(newScope);
}
```

**What it does:**
- Creates a new `Scope` with the current scope as its parent (for lookup chain)
- Pushes it onto the stack, making it the new current scope
- The `name` parameter is for debugging/diagnostics (e.g., "function foo", "class Bar")

**When used:**
- Function definitions: `EnterScope("function foo")`
- Class bodies: `EnterScope("class Bar")`
- Block scopes (if/while/for) when block-level scoping is needed

**Example call sequence during analysis:**
```csharp
// Analyzing: def foo(name: str):
//                message = "Hello " + name

NameResolver.VisitFunctionDef(node):
    1. Define function symbol in current scope
    2. EnterScope("function foo")
    3. Define parameter 'name' in new scope
    4. Analyze function body (defines 'message')
    5. ExitScope()
```

**Connection to Upstream/Downstream:**
- **Upstream**: Parser provides AST with function/class definitions
- **Downstream**: Ensures variables defined in function scope don't leak to outer scopes

---

#### `ExitScope()`

```csharp
public void ExitScope()
{
    if (_scopeStack.Count <= 1)
        throw new InvalidOperationException("Cannot exit global scope");
    _scopeStack.Pop();
}
```

**What it does:**
- Removes the topmost scope from the stack, returning to parent scope
- **Safety check**: Prevents popping the global scope (which must always remain)

**Important:** Must be called in symmetric pairs with `EnterScope()`. Missing an `ExitScope()` call will leave "ghost scopes" on the stack, causing subsequent lookups to behave incorrectly.

**Error Handling Pattern:**
```csharp
// Good pattern - ensures ExitScope is always called
_symbolTable.EnterScope("function foo");
try
{
    // Analyze function body
}
finally
{
    _symbolTable.ExitScope();
}
```

---

### 4. Symbol Definition Methods

#### `Define(Symbol symbol)`

```csharp
public void Define(Symbol symbol)
{
    CurrentScope.Define(symbol);
}
```

**What it does:**
- Adds a symbol to the current scope
- Delegates to `Scope.Define()` which handles:
  - Redefinition rules (variables can be redefined, constants cannot)
  - Builtin shadowing (allowed—user code can shadow builtins like `print`)
  - Duplicate definition errors (for functions, types, constants)

**Usage Example:**
```csharp
// Processing: x: int = 5
var varSymbol = new VariableSymbol
{
    Name = "x",
    Type = SemanticType.Int,
    Kind = SymbolKind.Variable
};
symbolTable.Define(varSymbol);
```

**Redefinition Behavior (implemented in Scope.cs:19-45):**
```python
# Variables can be redefined (Python-like)
x = 10
x = "hello"  # OK - redefines x

# But constants cannot
MAX: int = 100
MAX = 200  # ERROR - cannot redefine constant

# User code can shadow builtins
print = 42  # OK - shadows builtin print function
```

**Connection to Downstream:** Symbol information flows to CodeGen via `SemanticInfo` annotations on AST nodes.

---

#### `TryDefine(Symbol symbol)`

```csharp
public bool TryDefine(Symbol symbol)
{
    if (CurrentScope.Contains(symbol.Name))
        return false;
    CurrentScope.Define(symbol);
    return true;
}
```

**What it does:**
- Checks if the name already exists in the **current scope only** (not parent scopes)
- Returns `false` if it exists, `true` if successfully defined
- Does NOT throw error on duplicate (unlike `Define()`)

**When to use:**
- "Soft" definitions where duplicates should be silently ignored
- Module imports (avoid re-importing already loaded modules)
- Conditional symbol registration

**Example:**
```csharp
// Import module only if not already imported
if (symbolTable.TryDefine(moduleSymbol))
{
    // Successfully imported - load module contents
}
else
{
    // Already imported - skip
}
```

---

### 5. Symbol Lookup Methods

#### `Lookup(string name, bool searchParents = true)`

```csharp
public Symbol? Lookup(string name, bool searchParents = true)
{
    return CurrentScope.Lookup(name, searchParents);
}
```

**Core lookup algorithm** (implemented in Scope.cs:48-61):
1. Check current scope's symbol dictionary
2. If not found and `searchParents == true`, recursively check parent scopes
3. Return `null` if not found anywhere

**Parameters:**
- `name`: Symbol name to look up
- `searchParents`:
  - `true` (default): Search up the scope chain (normal lexical scoping)
  - `false`: Only search current scope (used for checking local-only existence)

**Return Value:** `Symbol?` (null if not found)

**Lexical Scoping Example:**
```python
x = 10  # Global scope

def foo():
    x = 20  # Function scope (shadows global)
    print(x)  # Lookup finds function-scope x (= 20)
```

**Lookup sequence:**
1. Check function scope → finds `x = 20` → returns immediately (shadowing)
2. Never checks global scope (already found a match)

**When `searchParents = false`:**
```csharp
// Check if symbol is defined locally (not inherited from parent)
var localSymbol = Lookup("x", searchParents: false);
```

**Connection to Upstream:** Called extensively during TypeChecker to resolve variable references, function calls, type annotations.

---

#### Typed Lookup Helpers

Convenience methods that cast lookup results to specific symbol types:

```csharp
public TypeSymbol? LookupType(string name)
    => Lookup(name) as TypeSymbol;

public FunctionSymbol? LookupFunction(string name)
    => Lookup(name) as FunctionSymbol;

public VariableSymbol? LookupVariable(string name)
    => Lookup(name) as VariableSymbol;

public TypeAliasSymbol? LookupTypeAlias(string name)
    => Lookup(name) as TypeAliasSymbol;
```

**What they do:**
- Call `Lookup()` and cast to expected symbol type
- Return `null` if either:
  - Symbol doesn't exist, OR
  - Symbol exists but is wrong type (e.g., `LookupType("print")` returns null because `print` is a FunctionSymbol)

**Usage Examples:**

```csharp
// Type resolution: x: int
var intType = symbolTable.LookupType("int");
if (intType == null)
    throw new SemanticError("Type 'int' not found");

// Function call: print("hello")
var printFunc = symbolTable.LookupFunction("print");
if (printFunc == null)
    throw new SemanticError("Function 'print' not found");

// Variable reference: y = x + 1
var varX = symbolTable.LookupVariable("x");
if (varX == null)
    throw new SemanticError("Variable 'x' not defined");
```

**When to use each:**
- `LookupType`: Parsing type annotations (`def foo() -> int:`)
- `LookupFunction`: Analyzing function calls, method resolution
- `LookupVariable`: Variable reference expressions
- `LookupTypeAlias`: Type alias resolution (e.g., `type MyList = list[int]`)

---

### 6. Symbol Updating

#### `UpdateSymbol(Symbol symbol)`

```csharp
public bool UpdateSymbol(Symbol symbol)
{
    return CurrentScope.Update(symbol);
}
```

**What it does:**
- Searches for a symbol with the same name in current scope or parent scopes
- If found, replaces it with the new symbol (using C# record immutability patterns)
- Returns `true` if updated, `false` if not found

**Primary use case:** Updating function return types during type checking

**Example scenario:**
```python
def calculate():    # Initially: ReturnType = Unknown
    return 42       # TypeChecker infers: ReturnType = int
```

```csharp
// NameResolver pass - initial registration
var funcSymbol = new FunctionSymbol
{
    Name = "calculate",
    ReturnType = SemanticType.Unknown
};
symbolTable.Define(funcSymbol);

// TypeChecker pass - update with inferred return type
var updatedSymbol = funcSymbol with { ReturnType = SemanticType.Int };
symbolTable.UpdateSymbol(updatedSymbol);
```

**Implementation Detail:** The `Scope.Update()` method (Scope.cs:72-86) searches current and parent scopes recursively, updating the first match found.

**Connection to Multi-Pass Analysis:** Allows semantic analysis to progressively refine symbol information across multiple passes without re-creating the entire symbol table.

---

## Dependencies

### Internal Dependencies

| File | Relationship |
|------|--------------|
| **Scope.cs** | Individual scope management—implements symbol storage, lookup, and redefinition logic |
| **Symbol.cs** | All symbol type definitions (VariableSymbol, FunctionSymbol, TypeSymbol, etc.) |
| **BuiltinRegistry.cs** | Provides builtin types/functions to populate global scope |
| **SemanticType.cs** | Type system used in symbol type annotations |

### External Consumers

SymbolTable is used throughout semantic analysis:

| Component | Usage |
|-----------|-------|
| **NameResolver** | Populates symbol table during first semantic pass |
| **TypeResolver** | Looks up type symbols when resolving type annotations |
| **TypeChecker** | Resolves variable/function references, performs type checking |
| **ImportResolver** | Registers imported modules and symbols |
| **CodeGenInfoComputer** | Queries symbols for code generation metadata |

---

## Patterns and Design Decisions

### 1. **Scope Stack Pattern**

Standard compiler pattern for managing nested scopes:

```
Stack representation:
┌─────────────────────┐ ← Top (CurrentScope)
│  If-Block Scope     │
├─────────────────────┤
│  Function Scope     │
├─────────────────────┤
│  Global Scope       │ ← Bottom (never popped)
└─────────────────────┘
```

**Why a stack?**
- Natural representation of nested code structures
- Efficient O(1) push/pop operations
- Automatic shadowing (inner scopes checked first during lookup)
- Automatic cleanup when exiting scopes

### 2. **Delegation Pattern**

SymbolTable is a thin wrapper around `Scope`:
```csharp
public void Define(Symbol symbol) => CurrentScope.Define(symbol);
public Symbol? Lookup(string name) => CurrentScope.Lookup(name);
```

**Rationale:**
- **SymbolTable**: Manages the *stack* of scopes (orchestration)
- **Scope**: Manages the *contents* of a single scope (implementation)
- Clean separation of concerns, easier testing

### 3. **Lexical Scoping (Static Scoping)**

Scope lookup follows syntactic structure, not runtime call stack:

```python
x = "global"

def outer():
    x = "outer"
    def inner():
        print(x)  # Resolves to "outer" (lexical parent), not globals
    return inner
```

The `_scopeStack` mirrors syntactic nesting, ensuring correct lexical resolution.

### 4. **Builtin Injection via Constructor**

```csharp
public SymbolTable(BuiltinRegistry builtins)
```

**Benefits:**
- **Testability**: Unit tests can inject mock/minimal builtin registry
- **Flexibility**: Could support different builtin sets for language versions
- **Explicit dependency**: Clear that SymbolTable depends on BuiltinRegistry

### 5. **Separation of Name Resolution vs. Overload Resolution**

- **Symbol Table**: Answers "Does this name exist?" (stores one FunctionSymbol per name)
- **TypeChecker + BuiltinRegistry**: Answers "Which overload matches these arguments?"

This keeps the symbol table simple and focused on name existence, delegating the complex logic of overload resolution to the type checker.

---

## Debugging Tips

### 1. **"Symbol not found" errors**

**Check:**
- Was `EnterScope()` called without matching `ExitScope()`? Use `ScopeDepth` property
- Is the symbol defined in a sibling scope instead of parent? (Common with nested functions)
- Did you call `Lookup` with `searchParents: false` by accident?

**Debug technique:**
```csharp
var sym = _symbolTable.Lookup("x");
if (sym == null)
{
    Console.WriteLine($"Current scope: {_symbolTable.CurrentScope.Name}");
    Console.WriteLine($"Scope depth: {_symbolTable.ScopeDepth}");
    // Add breakpoint here and inspect _scopeStack
}
```

### 2. **"Symbol already defined" errors**

**Check `Scope.Define()` logic** (delegates from SymbolTable.cs to Scope.cs:19-45):
- Are you trying to redefine a constant? (`IsConstant = true`)
- Are you redefining a function or type? (Not allowed, unlike variables)
- Is the symbol a builtin? (Shadowing is allowed; check `DeclarationLine == null`)

### 3. **Wrong symbol type returned**

```csharp
var typeSym = _symbolTable.LookupType("foo");
if (typeSym == null)
{
    // Either not found, OR found but not a TypeSymbol
    var anySymbol = _symbolTable.Lookup("foo");
    if (anySymbol != null)
    {
        // Found but wrong type!
        Console.WriteLine($"Expected TypeSymbol, got {anySymbol.GetType().Name}");
    }
}
```

### 4. **Scope stack corruption**

If you see `InvalidOperationException: Cannot exit global scope`:
- Mismatched `EnterScope`/`ExitScope` calls
- Exception thrown during analysis before `ExitScope` (use try-finally!)

```csharp
// Good pattern
_symbolTable.EnterScope("function foo");
try
{
    // Analyze function body
}
finally
{
    _symbolTable.ExitScope();  // Always called, even on exception
}
```

### 5. **Inspect Scope Depth**

```csharp
Console.WriteLine($"Current scope depth: {symbolTable.ScopeDepth}");
// Depth = 1: Global scope
// Depth > 1: Nested scopes
```

Unexpected depth indicates missing `EnterScope()`/`ExitScope()` calls.

### 6. **Verify Builtin Population**

```csharp
// After SymbolTable construction
var intType = symbolTable.LookupType("int");
var printFunc = symbolTable.LookupFunction("print");

if (intType == null || printFunc == null)
{
    Console.WriteLine("ERROR: Builtins not loaded correctly!");
}
```

### 7. **Debug Shadowing Issues**

```csharp
// Check if symbol is defined locally vs. globally
var local = Lookup("x", searchParents: false);
var global = GlobalScope.Lookup("x", searchParents: false);

if (local != null && global != null && local != global)
{
    Console.WriteLine("Variable 'x' is shadowing a global");
}
```

---

## Contribution Guidelines

### When to Modify SymbolTable

**DO modify when:**
- Adding new symbol lookup strategies (e.g., namespace-qualified lookup)
- Changing scope management behavior (e.g., adding block-level scoping for `let`)
- Adding new typed lookup helpers (e.g., `LookupNamespace`, `LookupProperty`)
- Enhancing diagnostics (e.g., exporting scope hierarchy for debugging)

**DON'T modify when:**
- Adding new symbol types → Modify `Symbol.cs` instead
- Changing builtin registration → Modify `BuiltinRegistry.cs`
- Changing scope lookup/redefinition rules → Modify `Scope.cs`

### Testing Considerations

When adding features to SymbolTable:

1. **Test scope nesting**: Verify symbols resolve correctly through multiple levels
2. **Test shadowing**: Ensure builtins can be shadowed, but constants cannot
3. **Test scope isolation**: Symbols in sibling scopes should not be visible to each other
4. **Test edge cases**: Empty scopes, global-only lookups, etc.

**Example test structure:**
```csharp
[Fact]
public void Lookup_NestedScopes_ResolvesFromParent()
{
    var builtins = new BuiltinRegistry();
    var table = new SymbolTable(builtins);

    // Define in global scope
    var globalVar = new VariableSymbol { Name = "x", Type = SemanticType.Int };
    table.Define(globalVar);

    // Enter nested scope
    table.EnterScope("function foo");

    // Should find in parent
    var found = table.LookupVariable("x");
    Assert.NotNull(found);
    Assert.Equal("x", found.Name);
}
```

### API Stability

**Public API (stable):**
- `Define`, `TryDefine`, `Lookup`, typed lookup methods
- `EnterScope`, `ExitScope`, `UpdateSymbol`
- `CurrentScope`, `GlobalScope`, `ScopeDepth`, `BuiltinRegistry` properties

**Internal implementation (may change):**
- `PopulateBuiltins` logic (driven by BuiltinRegistry evolution)
- Scope stack data structure (could switch to List or custom structure)

---

## Cross-References

### Related Documentation

- **[Scope.md](Scope.md)** - Individual scope implementation: symbol storage, lookup logic, redefinition rules
- **[Symbol.md](Symbol.md)** - Symbol type hierarchy: VariableSymbol, FunctionSymbol, TypeSymbol, etc.
- **[BuiltinRegistry.md](BuiltinRegistry.md)** - How builtin types/functions are loaded and managed
- **[NameResolver.md](NameResolver.md)** - First semantic pass: populates SymbolTable with declarations
- **[TypeChecker.md](TypeChecker.md)** - Uses SymbolTable for type resolution and validation
- **[ImportResolver.md](ImportResolver.md)** - Registers imported modules and symbols

### Language Specification

- **`docs/language_specification/identifiers.md`** - Identifier naming rules
- **`docs/language_specification/variable_scoping.md`** - Scoping rules implemented by SymbolTable

---

## Summary

`SymbolTable` is the foundational data structure for semantic analysis, providing:

✓ **Scope management**: Stack-based tracking of nested scopes
✓ **Name resolution**: Lexical scoping with parent chain lookup
✓ **Builtin integration**: Seamless inclusion of Sharpy.Core types/functions
✓ **Type-safe lookups**: Convenience methods for common symbol types
✓ **Multi-pass support**: UpdateSymbol allows progressive refinement

**Design Philosophy:** Simple, predictable, and Python-compatible—forming the backbone of Sharpy's multi-pass semantic analysis pipeline.

**Key Takeaway:** SymbolTable manages the *what* and *where* of symbols (scope orchestration), while delegating the *how* (lookup/redefinition logic) to `Scope`. This separation keeps the codebase clean, testable, and maintainable.
