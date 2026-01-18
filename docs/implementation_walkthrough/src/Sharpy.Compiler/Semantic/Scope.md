# Walkthrough: Scope.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Scope.cs`

---

## Overview

The `Scope` class is a **fundamental building block** of the Sharpy compiler's semantic analysis system. It represents a single lexical scope where symbols (variables, functions, types, etc.) are defined and can be looked up. Think of it as a "namespace" or "dictionary" that maps symbol names to their metadata.

While `Scope` is a simple class (only ~90 lines), it's used extensively throughout semantic analysis:
- **SymbolTable** manages a stack of scopes (global, function, class, block scopes)
- **NameResolver** enters/exits scopes while discovering declarations
- **TypeChecker** uses scopes to resolve variable references and check shadowing rules

### Role in the Compiler Pipeline

```
Source (.spy) → Lexer → Parser (AST) → [Semantic Analysis] → CodeGen
                                            ↓
                                       SymbolTable
                                       (Stack of Scopes)
```

The `Scope` class provides the **core data structure** for the symbol table. Each scope forms a node in a **parent-chain hierarchy**, enabling nested scope lookup (local → parent → grandparent → ... → global).

---

## Class Structure

### Main Class: `Scope`

```csharp
public class Scope
{
    private readonly Dictionary<string, Symbol> _symbols = new();
    private readonly Scope? _parent;
    public string Name { get; }
}
```

**Key Fields:**

- **`_symbols`**: Dictionary mapping symbol names (strings) to `Symbol` objects
  - Fast O(1) lookup by name
  - Stores all symbols defined **directly in this scope**
  - Does NOT include parent scope symbols (those are accessed via `_parent`)

- **`_parent`**: Reference to the enclosing (outer) scope
  - `null` for the global scope
  - Non-null for nested scopes (functions, classes, blocks)
  - Enables **scope chain traversal** for symbol lookup

- **`Name`**: Human-readable identifier for debugging
  - Examples: `"global"`, `"function:main"`, `"class:MyClass"`
  - Used in compiler logs to track which scope is active
  - Helpful when debugging scope-related issues

**Design pattern**: This is a classic **scope chain** implementation, similar to JavaScript's prototype chain or Python's LEGB rule (Local, Enclosing, Global, Built-in).

---

## Constructor

```csharp
public Scope(string name, Scope? parent = null)
{
    Name = name;
    _parent = parent;
}
```

**Parameters:**
- `name`: Descriptive name for debugging (e.g., `"global"`, `"class:Point"`)
- `parent`: The outer scope, or `null` for the top-level scope

**Usage examples:**

```csharp
// Creating the global scope (no parent)
var globalScope = new Scope("global");

// Creating a function scope (parent = global)
var funcScope = new Scope("function:main", globalScope);

// Creating a class scope (parent = global)
var classScope = new Scope("class:MyClass", globalScope);

// Creating a method scope (parent = class scope)
var methodScope = new Scope("method:__init__", classScope);
```

**Scope hierarchy example:**

```
global
├── function:main
│   └── (local variables in main)
└── class:MyClass
    └── method:__init__
        └── (local variables in __init__)
```

---

## Key Methods

### `Define(Symbol symbol)`

**Purpose**: Adds a symbol to the current scope, with special handling for redefinition cases.

```csharp
public void Define(Symbol symbol)
```

**What it does:**

This method handles the complex logic for **variable shadowing, redefinition, and builtin overriding** according to Sharpy's scoping semantics. It distinguishes between several cases:

#### Case 1: Symbol doesn't exist → Simple addition

```csharp
if (_symbols.TryGetValue(symbol.Name, out var existingSymbol))
{
    // ... redefinition logic
}

_symbols[symbol.Name] = symbol;  // No conflict, just add it
```

#### Case 2: Variable redefinition (same-scope reassignment with type change)

Sharpy allows **non-const variables** to be redefined in the same scope with a new type:

```python
x: int = 5
x: str = "hello"  # Valid - shadowing with explicit type annotation
```

```csharp
if (existingSymbol is VariableSymbol existingVar && !existingVar.IsConstant &&
    symbol is VariableSymbol newVar && !newVar.IsConstant)
{
    // Replace the existing symbol with the new one (redefinition)
    _symbols[symbol.Name] = symbol;
    return;
}
```

**Important**: This is different from assignment (`x = "hello"`), which is handled by the type checker. The `Define` method only handles explicit redefinitions with type annotations.

**Implementation note**: The actual variable name versioning (e.g., `x`, `x_1_<uuid>`, `x_2_<uuid>`) is handled during code generation, not in the scope. The scope just tracks the "current" definition of the variable.

See `docs/language_specification/variable_scoping.md` for the full semantics.

#### Case 3: Shadowing builtins

Sharpy follows Python's behavior: **user code can shadow builtin names** like `print`, `len`, `int`, etc.

```python
# This is valid Sharpy code:
def print(msg: str) -> None:  # Shadows the builtin print function
    # Custom print implementation
    pass
```

```csharp
// Allow shadowing builtins (which have no source location)
// This matches Python behavior where user code can shadow builtins like print, len, etc.
bool isBuiltin = existingSymbol.DeclarationLine == null;
if (isBuiltin)
{
    _symbols[symbol.Name] = symbol;
    return;
}
```

**Detection mechanism**: Builtin symbols have `DeclarationLine == null` because they're not defined in source code. User-defined symbols always have a source location.

**Why allow this?** It matches Python's philosophy of "we're all consenting adults here" - if you want to replace `print`, you can. The original builtin is still accessible via other means if needed.

#### Case 4: Invalid redefinition → Error

For all other cases (constants, functions, types), redefinition is an error:

```python
PI: float = 3.14159
PI: float = 3.14  # ERROR: Symbol 'PI' is already defined

def foo(): pass
def foo(): pass   # ERROR: Symbol 'foo' is already defined
```

```csharp
// For all other cases (constants, functions, types, etc.), redefinition is an error
throw new SemanticError($"Symbol '{symbol.Name}' is already defined in this scope");
```

**Note**: Function overloading is handled differently - constructor overloads (`__init__`) and operator overloads are stored in separate collections on the `TypeSymbol`, not in the scope.

---

### `Lookup(string name, bool searchParent = true)`

**Purpose**: Finds a symbol by name, optionally searching parent scopes.

```csharp
public Symbol? Lookup(string name, bool searchParent = true)
```

**Parameters:**
- `name`: The symbol name to search for
- `searchParent`: Whether to search parent scopes if not found locally (default: `true`)

**Returns:**
- The `Symbol` if found
- `null` if not found

**Algorithm:**

1. **Search current scope first**:
   ```csharp
   if (_symbols.TryGetValue(name, out var symbol))
   {
       return symbol;
   }
   ```

2. **If not found and `searchParent == true`, recurse to parent**:
   ```csharp
   if (searchParent && _parent != null)
   {
       return _parent.Lookup(name, searchParent);
   }
   ```

3. **If still not found, return `null`**

**Usage examples:**

```csharp
// Look up variable 'x' in current scope and all parent scopes
var symbol = currentScope.Lookup("x");

// Look up variable 'x' ONLY in current scope (no parent lookup)
var localSymbol = currentScope.Lookup("x", searchParent: false);
```

**When to use `searchParent: false`:**
- Checking for redefinition (you only care if it exists locally)
- Validating that a name is available in the current scope
- Implementing local-only symbol resolution

**Scope chain traversal example:**

```python
x = 10  # Global scope

def foo():
    y = 20  # Function scope
    print(x)  # Looks up: function scope → global scope (found!)
    print(y)  # Looks up: function scope (found!)
```

The `Lookup` method implements this **LEGB-like resolution**:
- **L**ocal (current scope)
- **E**nclosing function scopes (via parent chain)
- **G**lobal (module scope)
- **B**uilt-in (populated in global scope by SymbolTable)

---

### `Contains(string name)`

**Purpose**: Checks if a symbol exists in the **current scope only** (no parent lookup).

```csharp
public bool Contains(string name)
{
    return _symbols.ContainsKey(name);
}
```

**Equivalent to**: `Lookup(name, searchParent: false) != null`

**Usage:**
```csharp
if (currentScope.Contains("x"))
{
    // Symbol 'x' is defined directly in this scope
}
```

**When to use this:**
- Checking for local redefinition before calling `Define`
- Implementing scope-specific logic (e.g., "are there any local variables?")

---

### `Update(Symbol symbol)`

**Purpose**: Updates an existing symbol in the scope chain (current scope or parent scopes).

```csharp
public bool Update(Symbol symbol)
```

**Returns:**
- `true` if the symbol was found and updated
- `false` if the symbol doesn't exist in this scope or any parent

**Algorithm:**

1. **Check current scope first**:
   ```csharp
   if (_symbols.ContainsKey(symbol.Name))
   {
       _symbols[symbol.Name] = symbol;
       return true;
   }
   ```

2. **If not found, recurse to parent**:
   ```csharp
   if (_parent != null)
   {
       return _parent.Update(symbol);
   }
   ```

3. **If still not found, return `false`**

**When is this used?**

This method is used during **type checking** to update function symbols with resolved return types:

```python
def foo(x: int):  # At name resolution: return type is Unknown
    return x * 2  # At type checking: infer return type as int
```

During name resolution, the function's `ReturnType` is `SemanticType.Unknown`. Later, during type checking, the type checker:
1. Infers the return type from the function body
2. Creates a new `FunctionSymbol` with the resolved return type
3. Calls `scope.Update(updatedSymbol)` to replace the old symbol

**Code reference**: This is used in `TypeChecker.Definitions.cs` when processing function definitions.

**Why not just use `Define`?** Because `Define` has special redefinition logic that would throw an error for functions. `Update` is specifically for replacing an existing symbol's metadata, not adding a new definition.

---

### `GetAllSymbols()`

**Purpose**: Returns all symbols defined in the **current scope** (not parent scopes).

```csharp
public IEnumerable<Symbol> GetAllSymbols()
{
    return _symbols.Values;
}
```

**Use cases:**
- Debugging: Inspecting what's defined in a scope
- Code generation: Iterating over local variables to emit declarations
- Analysis tools: Finding all symbols of a certain kind in a scope

**Example:**
```csharp
// Find all variables in the current function scope
var localVars = currentScope.GetAllSymbols()
    .OfType<VariableSymbol>()
    .Where(v => !v.IsParameter);
```

---

## Dependencies

### Direct Dependencies

1. **`Symbol` hierarchy** (`Semantic/Symbol.cs`)
   - `Symbol`: Base class for all symbols
   - `VariableSymbol`: Variables and fields
   - `FunctionSymbol`: Functions and methods
   - `TypeSymbol`: Classes, structs, interfaces, enums
   - `ModuleSymbol`: Imported modules
   - `TypeAliasSymbol`: Type aliases
   - `TypeParameterSymbol`: Generic type parameters

2. **`SemanticError`** (`Semantic/SemanticError.cs`)
   - Thrown when invalid redefinition occurs

### Upstream Consumers

These components use `Scope`:

1. **`SymbolTable`** (`Semantic/SymbolTable.cs`)
   - Manages a **stack** of scopes
   - Delegates `Define`, `Lookup`, `Update` calls to the current scope
   - See: [SymbolTable.md](./SymbolTable.md)

2. **`NameResolver`** (`Semantic/NameResolver.cs`)
   - Enters/exits scopes when processing classes, functions, etc.
   - Calls `scope.Define` for each declaration
   - See: [NameResolver.md](./NameResolver.md)

3. **`TypeChecker`** (`Semantic/TypeChecker.*.cs`)
   - Looks up symbols during type checking
   - Updates function symbols with inferred return types
   - See: [TypeChecker.md](./TypeChecker.md)

---

## Patterns and Design Decisions

### 1. Scope Chain (Parent Pointer)

**Pattern**: Each scope has an optional parent pointer, forming a **singly-linked list** of scopes.

**Benefit**:
- Simple recursive lookup algorithm
- O(1) scope creation/destruction
- Memory-efficient (only one pointer per scope)

**Alternative considered**: Flattening all scopes into a single lookup table with hierarchical keys (e.g., `"global.MyClass.method"`). Rejected because it's more complex and less idiomatic.

---

### 2. Builtin Shadowing via Source Location

**Design decision**: Builtins are detected by checking `DeclarationLine == null`.

**Why this works:**
- User-defined symbols always have a source location (from the AST)
- Builtins are registered by `BuiltinRegistry` without source locations
- Simple, efficient check (no need for a `IsBuiltin` flag)

**Code location** (`src/Sharpy.Compiler/Semantic/Scope.cs:34`):
```csharp
bool isBuiltin = existingSymbol.DeclarationLine == null;
```

**Trade-off**: If we ever want builtins to have source locations (e.g., for documentation), we'd need to add an explicit `IsBuiltin` flag. For now, this approach is simple and works well.

---

### 3. Variable Redefinition vs Assignment

**Important distinction:**

- **Redefinition** (handled by `Scope.Define`): Creating a new variable with the same name but a different type
  ```python
  x: int = 5
  x: str = "hello"  # Redefinition with type annotation
  ```

- **Assignment** (handled by type checker): Changing the value of an existing variable
  ```python
  x: int = 5
  x = 10  # Assignment (same type)
  ```

**Why separate?**
- Redefinition requires **type annotation** to signal intent (Sharpy rule)
- Assignment must **type-check** against the existing variable's type
- Different error messages for clarity

See `docs/language_specification/variable_scoping.md` for the full semantics.

---

### 4. Immutable Symbols (Records)

Symbols are **C# records**, which are immutable by default. However, `Update` appears to "mutate" a symbol by replacing it:

```csharp
_symbols[symbol.Name] = symbol;  // Replacing, not mutating
```

**Why this works:**
- We're not modifying the symbol object itself
- We're replacing the dictionary entry with a new symbol
- The old symbol object is discarded (GC will clean it up)

**Benefit**: Immutable symbols are easier to reason about and safer in concurrent scenarios (though the compiler is currently single-threaded).

---

### 5. Lookup Performance: O(d) where d = scope depth

**Worst case**: Looking up a global symbol from a deeply nested scope requires traversing the entire scope chain.

```python
# Scope depth = 5
x = 10  # Global

def a():
    def b():
        def c():
            def d():
                print(x)  # Looks up: d → c → b → a → global (5 hops)
```

**Why this is acceptable:**
- Most lookups are for **local variables** (fast, 1 hop)
- Deep nesting (>3-4 levels) is rare in practice
- The dictionary lookup at each level is O(1), so total cost is O(d)
- Alternative (flattened table) would complicate scope management

**Performance note**: If profiling shows this is a bottleneck, we could add a **symbol cache** that maps names to their resolved symbols. For now, the simple approach is sufficient.

---

## Debugging Tips

### Common Issues and How to Debug Them

#### Issue: "Symbol 'X' is already defined"

**Cause**: Attempting to define a symbol that already exists in the current scope (and is not a valid redefinition case).

**Debug approach:**
1. Set a breakpoint in `Scope.Define` at line 20 (the `TryGetValue` check)
2. Inspect `existingSymbol` to see what's already defined
3. Check if it's a builtin (`DeclarationLine == null`)
4. Check if it's a valid variable redefinition (non-const variable)
5. Verify the source location to see where the duplicate definition is

**Code location** (`src/Sharpy.Compiler/Semantic/Scope.cs:20-46`):
```csharp
if (_symbols.TryGetValue(symbol.Name, out var existingSymbol))
{
    // Set breakpoint here to inspect existingSymbol
}
```

**Common causes:**
- Duplicate class/function definitions
- Defining a constant twice
- Attempting to redefine a function (not supported)

---

#### Issue: Symbol lookup returns `null` unexpectedly

**Cause**: Symbol not found in current scope or parent scopes.

**Debug approach:**
1. Set a breakpoint in `Scope.Lookup` at line 50
2. Inspect `_symbols.Keys` to see what's in the current scope
3. Step through the parent chain recursion to see where lookup stops
4. Verify the symbol was actually defined (check `SymbolTable` state earlier in the pipeline)

**Code location** (`src/Sharpy.Compiler/Semantic/Scope.cs:48-61`):
```csharp
public Symbol? Lookup(string name, bool searchParent = true)
{
    // Set breakpoint here, inspect _symbols and _parent
    if (_symbols.TryGetValue(name, out var symbol))
    {
        return symbol;
    }

    if (searchParent && _parent != null)
    {
        return _parent.Lookup(name, searchParent);  // Step into to trace parent chain
    }

    return null;
}
```

**Common causes:**
- Symbol not defined yet (forward reference without proper ordering)
- Symbol defined in a sibling scope (not in parent chain)
- Typo in symbol name (case-sensitive!)
- Looking up a member of a type (should use `TypeSymbol.Methods`/`Fields` instead)

---

#### Issue: Builtin shadowing not working

**Cause**: Builtin symbol has a source location (should be `null`).

**Debug approach:**
1. Check the builtin registry initialization
2. Verify builtins are created without `DeclarationLine`/`DeclarationColumn`
3. Set breakpoint at line 34 to check `isBuiltin` flag

**Code location** (`src/Sharpy.Compiler/Semantic/Scope.cs:34-39`):
```csharp
bool isBuiltin = existingSymbol.DeclarationLine == null;
if (isBuiltin)
{
    _symbols[symbol.Name] = symbol;
    return;
}
```

**Verification**:
```csharp
// In BuiltinRegistry, ensure builtins are created like this:
new FunctionSymbol
{
    Name = "print",
    // DeclarationLine = null,  // Implicit default
    // DeclarationColumn = null,
    // ...
};
```

---

#### Issue: Variable redefinition not working

**Cause**: Not meeting the conditions for valid redefinition.

**Debug approach:**
1. Set breakpoint at line 24 (variable redefinition check)
2. Verify both symbols are `VariableSymbol`
3. Verify both symbols are non-const (`IsConstant == false`)
4. Check the symbol types being defined

**Code location** (`src/Sharpy.Compiler/Semantic/Scope.cs:24-30`):
```csharp
if (existingSymbol is VariableSymbol existingVar && !existingVar.IsConstant &&
    symbol is VariableSymbol newVar && !newVar.IsConstant)
{
    // Set breakpoint here to verify conditions
    _symbols[symbol.Name] = symbol;
    return;
}
```

**Remember**: Constants cannot be redefined. Functions cannot be redefined. Only non-const variables can be redefined with explicit type annotations.

---

### Useful Debugging Techniques

**1. Inspect scope chain:**
```csharp
// In debugger watch window:
var scopeChain = new List<string>();
var current = currentScope;
while (current != null)
{
    scopeChain.Add(current.Name);
    current = current._parent;
}
// Result: ["method:__init__", "class:Point", "global"]
```

**2. Dump all symbols in scope:**
```csharp
// In debugger immediate window:
currentScope._symbols.Keys.ToList()
// Shows all symbol names in current scope
```

**3. Check if a symbol exists anywhere in the chain:**
```csharp
// In debugger:
var found = currentScope.Lookup("myVar");
// If found != null, inspect found.DeclarationLine to see where it's defined
```

**4. Trace parent chain depth:**
```csharp
int depth = 0;
var current = currentScope;
while (current._parent != null)
{
    depth++;
    current = current._parent;
}
// Result: scope depth (0 = global, 1 = module-level, 2+ = nested)
```

**5. Enable verbose logging in SymbolTable:**

The `SymbolTable` class already logs scope enter/exit operations. Increase the log level to see the full scope management flow:
```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
```

This will show output like:
```
[Debug] Entering scope: class:MyClass
[Debug] Defining symbol: x (VariableSymbol)
[Debug] Exiting scope: class:MyClass
```

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made

#### 1. Adding Symbol Metadata Validation

**Example**: Validate that symbols have required fields populated before adding to scope.

```csharp
public void Define(Symbol symbol)
{
    // New validation
    if (string.IsNullOrWhiteSpace(symbol.Name))
    {
        throw new ArgumentException("Symbol name cannot be empty", nameof(symbol));
    }

    // ... existing logic
}
```

**When to add this**: If you find bugs related to malformed symbols being registered.

---

#### 2. Improving Error Messages

**Current**:
```
SemanticError: Symbol 'foo' is already defined in this scope
```

**Improved**:
```
SemanticError at line 10, column 5: Symbol 'foo' is already defined in this scope
Note: Previous definition at line 5, column 5 in scope 'class:MyClass'
```

**Implementation**:
```csharp
throw new SemanticError(
    $"Symbol '{symbol.Name}' is already defined in scope '{Name}'. " +
    $"Previous definition at line {existingSymbol.DeclarationLine}, column {existingSymbol.DeclarationColumn}.",
    symbol.DeclarationLine,
    symbol.DeclarationColumn);
```

---

#### 3. Adding Scope Statistics

**Use case**: Debugging, profiling, or IDE features.

```csharp
public class Scope
{
    // ... existing fields

    public int SymbolCount => _symbols.Count;
    public int Depth
    {
        get
        {
            int depth = 0;
            var current = _parent;
            while (current != null)
            {
                depth++;
                current = current._parent;
            }
            return depth;
        }
    }
}
```

---

#### 4. Supporting Symbol Removal

**Current**: No way to remove symbols from a scope (not needed yet).

**Future**: If we add support for "deleting" symbols (e.g., for REPL):

```csharp
public bool Remove(string name)
{
    return _symbols.Remove(name);
}
```

**Note**: Only add this if there's a real use case. Currently, scopes are short-lived and don't need removal.

---

#### 5. Optimizing Lookup with Caching

**If profiling shows lookups are slow:**

```csharp
private readonly Dictionary<string, Symbol> _symbols = new();
private readonly Dictionary<string, Symbol> _lookupCache = new();  // New

public Symbol? Lookup(string name, bool searchParent = true)
{
    // Check cache first
    if (_lookupCache.TryGetValue(name, out var cached))
    {
        return cached;
    }

    // ... existing lookup logic

    // Cache the result
    if (symbol != null)
    {
        _lookupCache[name] = symbol;
    }

    return symbol;
}
```

**Trade-off**: Increased memory usage, cache invalidation complexity. Only add if measurements show a problem.

---

### Testing Guidelines

**Test file location**: `src/Sharpy.Compiler.Tests/Semantic/ScopeTests.cs`

**Test patterns**:

```csharp
[Fact]
public void Define_NewSymbol_AddsToScope()
{
    var scope = new Scope("test");
    var symbol = new VariableSymbol { Name = "x", Type = SemanticType.Int };

    scope.Define(symbol);

    var result = scope.Lookup("x");
    Assert.NotNull(result);
    Assert.Equal("x", result.Name);
}

[Fact]
public void Define_DuplicateConstant_ThrowsSemanticError()
{
    var scope = new Scope("test");
    var const1 = new VariableSymbol { Name = "PI", IsConstant = true };
    var const2 = new VariableSymbol { Name = "PI", IsConstant = true };

    scope.Define(const1);

    var exception = Assert.Throws<SemanticError>(() => scope.Define(const2));
    Assert.Contains("already defined", exception.Message);
}

[Fact]
public void Lookup_SymbolInParentScope_ReturnsSymbol()
{
    var parent = new Scope("parent");
    var child = new Scope("child", parent);
    var symbol = new VariableSymbol { Name = "x", Type = SemanticType.Int };

    parent.Define(symbol);

    var result = child.Lookup("x");
    Assert.NotNull(result);
    Assert.Equal("x", result.Name);
}

[Fact]
public void Lookup_WithSearchParentFalse_DoesNotSearchParent()
{
    var parent = new Scope("parent");
    var child = new Scope("child", parent);
    var symbol = new VariableSymbol { Name = "x", Type = SemanticType.Int };

    parent.Define(symbol);

    var result = child.Lookup("x", searchParent: false);
    Assert.Null(result);  // Not found in child scope
}

[Fact]
public void Define_ShadowBuiltin_ReplacesBuiltin()
{
    var scope = new Scope("global");
    var builtin = new FunctionSymbol
    {
        Name = "print",
        DeclarationLine = null  // Builtins have no source location
    };
    var userDefined = new FunctionSymbol
    {
        Name = "print",
        DeclarationLine = 10  // User-defined has source location
    };

    scope.Define(builtin);
    scope.Define(userDefined);  // Should replace, not throw

    var result = scope.Lookup("print");
    Assert.Equal(10, result.DeclarationLine);  // User-defined version
}
```

**Test categories to cover**:
- ✅ Define: new symbols
- ✅ Define: duplicate detection (constants, functions, types)
- ✅ Define: variable redefinition (same type, different type)
- ✅ Define: builtin shadowing
- ✅ Lookup: local symbols
- ✅ Lookup: parent scope traversal
- ✅ Lookup: `searchParent` parameter
- ✅ Contains: local-only check
- ✅ Update: existing symbol replacement
- ✅ Update: parent scope traversal
- ✅ GetAllSymbols: iteration

**New features must include tests for:**
- Basic functionality
- Error cases
- Edge cases (empty scopes, deep nesting)
- Integration with SymbolTable

---

### Code Style

**Follow existing patterns**:
- Private fields use `_camelCase`
- Public properties use `PascalCase`
- Use `var` for local variables where type is obvious
- Early return for error cases
- XML comments (`///`) for public methods

**CRITICAL**: Don't alter test expectations to make tests pass. Fix the implementation instead.

---

## Cross-References

**Related documentation files:**

**Semantic Analysis Pipeline:**
- [SymbolTable.md](./SymbolTable.md) - Manages scope stack and delegates to Scope
- [Symbol.md](./Symbol.md) - Symbol type definitions (VariableSymbol, FunctionSymbol, etc.)
- [NameResolver.md](./NameResolver.md) - Uses scopes to register declarations
- [TypeChecker.md](./TypeChecker.md) - Uses scopes for symbol lookup and type checking

**Language Specifications:**
- `docs/language_specification/variable_scoping.md` - Scoping rules and variable shadowing
- `docs/language_specification/identifiers.md` - Identifier naming rules
- `docs/language_specification/naming_conventions.md` - Python-style naming conventions

---

## Summary

The `Scope` class is a **simple but essential** component of the Sharpy compiler. It:
- ✅ Stores symbols in a dictionary for fast O(1) lookup
- ✅ Forms a parent chain for nested scope resolution
- ✅ Implements Sharpy's scoping semantics (variable redefinition, builtin shadowing)
- ✅ Provides a clean API for SymbolTable and semantic analysis passes

**Key features:**
- **Parent chain**: Enables lexical scoping and LEGB-like resolution
- **Variable redefinition**: Supports Python-like shadowing with explicit type annotations
- **Builtin shadowing**: Allows user code to override builtin names
- **Symbol update**: Supports replacing symbols with refined metadata (e.g., inferred return types)

**Design philosophy:**
- **Simple**: ~90 lines, single responsibility
- **Efficient**: O(1) lookup per scope, O(d) for scope chain traversal
- **Pythonic**: Matches Python scoping behavior while enabling C# code generation

**Remember:**
- Scopes form a **tree structure** (via parent pointers)
- Only one symbol per name per scope (except for overloads, which are handled separately)
- Builtin detection uses `DeclarationLine == null` (no explicit flag)
- Variable redefinition requires explicit type annotation
- `Update` is for metadata refinement, not new definitions

When contributing:
- Tests are mandatory for all changes
- Follow the existing naming conventions
- Keep the class simple - don't add features without clear use cases
- Consider performance implications of scope chain traversal
- Preserve Pythonic scoping semantics
