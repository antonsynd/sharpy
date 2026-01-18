# Walkthrough: SymbolTable.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SymbolTable.cs`

---

## Overview

The `SymbolTable` is the **central hub for all symbol storage and lookup** during semantic analysis in the Sharpy compiler. It manages a stack of nested scopes and provides a clean interface for defining, looking up, and updating symbols (variables, functions, types, etc.) as the compiler analyzes your code.

Think of it as the compiler's "phonebook" - it keeps track of what names mean in different parts of your code, handling both global declarations and local scopes (like function bodies or nested blocks).

### Role in the Compiler Pipeline

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → CodeGen
                                           ↓
                                      SymbolTable
                                      (used by NameResolver,
                                       TypeResolver, TypeChecker)
```

The `SymbolTable` is created once at the start of semantic analysis and is used throughout all semantic passes:
- **NameResolver**: Populates the symbol table with declarations
- **TypeResolver**: Looks up type symbols when resolving type annotations
- **TypeChecker**: Looks up variables, functions, and types during type checking

It persists across all these passes, maintaining the complete symbol database.

---

## Class Structure

### Main Class: `SymbolTable`

```csharp
public class SymbolTable
{
    private readonly Stack<Scope> _scopeStack = new();
    private readonly Scope _globalScope;
    private readonly BuiltinRegistry _builtins;
}
```

**Key Fields:**

- **`_scopeStack`**: A stack that tracks the current scope hierarchy. The top of the stack is the "current scope" - where new symbols are defined and lookups start.
- **`_globalScope`**: A permanent reference to the global (module-level) scope. This is always at the bottom of the stack.
- **`_builtins`**: Reference to the builtin type and function registry (contains `int`, `str`, `list`, `print()`, `len()`, etc.)

**Design Pattern**: The `SymbolTable` uses a **scope stack** pattern common in compilers. As the semantic analyzer enters nested blocks (functions, classes, if-statements), it pushes new scopes onto the stack. When exiting, it pops them off. This naturally handles variable shadowing and scoping rules.

---

## Key Methods

### Constructor

#### `SymbolTable(BuiltinRegistry builtins)`

**Purpose**: Initializes the symbol table with a global scope pre-populated with builtin types and functions.

```csharp
public SymbolTable(BuiltinRegistry builtins)
{
    _builtins = builtins;
    _globalScope = new Scope("global");
    _scopeStack.Push(_globalScope);

    // Populate global scope with builtins
    PopulateBuiltins();
}
```

**What it does:**
1. Creates and pushes the global scope onto the stack
2. Calls `PopulateBuiltins()` to register all builtin types (`int`, `str`, `list`, etc.) and functions (`print`, `len`, etc.) into the global scope
3. After construction, the symbol table is ready to use - all Python-like builtins are immediately available

**Important Detail**: The global scope is never popped from the stack. Attempting to exit the global scope throws an exception (see `ExitScope()`).

---

### Scope Management

#### `EnterScope(string name)`

**Purpose**: Pushes a new nested scope onto the stack.

```csharp
public void EnterScope(string name)
{
    var newScope = new Scope(name, CurrentScope);
    _scopeStack.Push(newScope);
}
```

**What it does:**
- Creates a new `Scope` with the current scope as its parent
- Pushes it onto the stack, making it the new current scope
- The `name` parameter is for debugging/diagnostics (e.g., "function:my_func", "if-block")

**Usage Example:**
When the semantic analyzer processes a function definition like:
```python
def greet(name: str):
    message = "Hello " + name
    print(message)
```

The analyzer will:
1. Call `EnterScope("function:greet")` when entering the function body
2. Define `name` and `message` variables in this new scope
3. Call `ExitScope()` after processing the function body

This ensures `message` is only visible inside the function.

---

#### `ExitScope()`

**Purpose**: Pops the current scope from the stack, returning to the parent scope.

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
- Removes the topmost scope from the stack
- **Safety Check**: Prevents popping the global scope (which should always remain on the stack)

**Important**: This is called in symmetric pairs with `EnterScope()`. Missing an `ExitScope()` call will leave "ghost scopes" on the stack, causing subsequent lookups to behave incorrectly.

---

### Symbol Definition

#### `Define(Symbol symbol)`

**Purpose**: Adds a symbol to the current scope.

```csharp
public void Define(Symbol symbol)
{
    CurrentScope.Define(symbol);
}
```

**What it does:**
- Delegates to the current scope's `Define()` method
- The `Scope.Define()` implementation handles:
  - Redefinition rules (variables can be reassigned, constants cannot)
  - Shadowing builtins (allowed)
  - Duplicate definition errors (for functions, types, etc.)

**Usage Example:**
When processing `x: int = 5`, the semantic analyzer creates a `VariableSymbol` and calls `Define()` to register it.

---

#### `TryDefine(Symbol symbol)`

**Purpose**: Conditionally defines a symbol only if it doesn't already exist in the current scope.

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

**When to use**: This is useful for "soft" definitions where you want to avoid errors if the symbol already exists. For example, when importing modules - if a module is already imported, you can skip re-importing it.

---

### Symbol Lookup

#### `Lookup(string name, bool searchParents = true)`

**Purpose**: The core lookup method that searches for a symbol by name.

```csharp
public Symbol? Lookup(string name, bool searchParents = true)
{
    return CurrentScope.Lookup(name, searchParents);
}
```

**What it does:**
- Searches the current scope first
- If not found and `searchParents` is `true`, recursively searches parent scopes up to the global scope
- Returns `null` if the symbol is not found anywhere

**The `searchParents` Parameter:**
- **`true` (default)**: Normal lookup - search up the scope chain (used 99% of the time)
- **`false`**: Only search the current scope - used for checking if a symbol is local

**Lookup Order Example:**
```python
x = 10  # Global scope

def foo():
    x = 20  # Function scope
    print(x)  # Lookup finds function-scope x (= 20), not global x
```

When looking up `x` inside `foo()`, the algorithm:
1. Checks the function scope → finds `x = 20` → returns it immediately
2. Never checks the global scope (because it found a match)

This implements **variable shadowing** - inner scopes can hide outer scopes.

---

#### `LookupType(string name)`, `LookupFunction(string name)`, `LookupVariable(string name)`, `LookupTypeAlias(string name)`

**Purpose**: Typed convenience methods that cast the lookup result to specific symbol types.

```csharp
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
```

**What they do:**
- Call `Lookup()` and cast the result to the expected symbol type
- Return `null` if either:
  - The symbol doesn't exist
  - The symbol exists but is the wrong type (e.g., looking up "int" as a function when it's actually a type)

**Usage Example:**
When processing `x: int`, the type resolver calls `LookupType("int")` to find the `int` type symbol. If someone defined a variable named `int`, this would return `null` (because the variable symbol can't be cast to `TypeSymbol`).

---

### Symbol Updates

#### `UpdateSymbol(Symbol symbol)`

**Purpose**: Replaces an existing symbol in the scope chain with a new version.

```csharp
public bool UpdateSymbol(Symbol symbol)
{
    return CurrentScope.Update(symbol);
}
```

**What it does:**
- Searches for a symbol with the same name in the current scope or any parent scope
- If found, replaces it with the new symbol
- Returns `true` if updated, `false` if not found

**When to use**: This is primarily used during **type checking** to update function symbols with their resolved return types. Here's why:

During the first pass (NameResolver), a function might be registered with an `Unknown` return type:
```csharp
// First pass: return type not yet known
FunctionSymbol { Name = "calculate", ReturnType = SemanticType.Unknown }
```

Later, during type checking, the analyzer infers the return type and updates the symbol:
```csharp
// Type checking pass: update with inferred return type
UpdateSymbol(new FunctionSymbol { Name = "calculate", ReturnType = SemanticType.Int })
```

This allows the symbol table to evolve as more information becomes available.

---

### Properties

```csharp
public Scope CurrentScope => _scopeStack.Peek();
public Scope GlobalScope => _globalScope;
public int ScopeDepth => _scopeStack.Count;
public BuiltinRegistry BuiltinRegistry => _builtins;
```

**`CurrentScope`**: Returns the top of the scope stack (where new symbols are defined and lookups start).

**`GlobalScope`**: Direct access to the global scope (useful for checking if something is globally defined).

**`ScopeDepth`**: The number of nested scopes (1 = global only, 2 = one nested scope, etc.). Useful for diagnostics.

**`BuiltinRegistry`**: Access to the builtin registry (used by type checker for overload resolution).

---

## Private Methods

### `PopulateBuiltins()`

**Purpose**: Pre-populates the global scope with all builtin types and functions from the `BuiltinRegistry`.

```csharp
private void PopulateBuiltins()
{
    // Add builtin types
    foreach (var (name, typeSymbol) in _builtins.GetAllTypes())
    {
        _globalScope.Define(typeSymbol);
    }

    // Add builtin functions (only add one symbol per function name, overload resolution happens later)
    // For functions with multiple overloads, only the first overload is added to the global scope.
    // The TypeChecker uses BuiltinRegistry.GetFunctionOverloads() for proper overload resolution.
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

1. **Types**: Registers all builtin types (`int`, `str`, `list`, `dict`, `bool`, etc.) directly into the global scope
2. **Functions**: Registers only **one function symbol per name**, even if there are multiple overloads

**Important Design Decision - Function Overload Handling:**

The symbol table only stores **one** `FunctionSymbol` per function name (the first overload encountered). This is intentional because:

- The symbol table's job is **name resolution** ("does this name exist?")
- **Overload resolution** ("which overload should I call?") is handled later by the `TypeChecker`
- During type checking, the analyzer calls `BuiltinRegistry.GetFunctionOverloads()` to get all overloads and select the best match

**Example:**
```python
# Builtin print() has multiple overloads:
# print(value: object) -> None
# print(*values: object) -> None
```

The symbol table only stores one of these. When type checking a call to `print()`, the type checker:
1. Uses `SymbolTable.LookupFunction("print")` to verify the name exists
2. Uses `BuiltinRegistry.GetFunctionOverloads("print")` to get all overloads
3. Selects the best overload based on argument types

---

## Dependencies

### Internal Dependencies

The `SymbolTable` depends on these other semantic analysis components:

- **`Scope`** ([Scope.md](Scope.md)): Manages individual scope instances and implements the actual symbol storage/lookup logic. `SymbolTable` orchestrates scopes; `Scope` does the heavy lifting.

- **`Symbol`** ([Symbol.md](Symbol.md)): All symbol types (`VariableSymbol`, `FunctionSymbol`, `TypeSymbol`, etc.). The symbol table is generic over these types.

- **`BuiltinRegistry`** ([BuiltinRegistry.md](BuiltinRegistry.md)): Provides the initial set of builtin types and functions. Injected via constructor.

### External Dependencies

- **None** - `SymbolTable` is a pure data structure with no external dependencies beyond the semantic analysis namespace.

### Used By (Consumers)

The `SymbolTable` is used throughout semantic analysis:

- **[NameResolver.md](NameResolver.md)**: Populates the symbol table during the first semantic pass
- **[ImportResolver.md](ImportResolver.md)**: Uses the symbol table to register imported modules and symbols
- Type checking passes: Look up symbols to verify correctness

---

## Cross-References

### Related Documentation

- **[Scope.md](Scope.md)**: Explains how individual scopes work, including redefinition rules and shadowing behavior
- **[Symbol.md](Symbol.md)**: Documents all symbol types and their properties
- **[BuiltinRegistry.md](BuiltinRegistry.md)**: How builtin types and functions are loaded and managed
- **[NameResolver.md](NameResolver.md)**: Shows how the symbol table is populated during the first semantic pass
- **[ImportResolver.md](ImportResolver.md)**: Uses the symbol table to register imported modules and symbols

### Language Specification

- **`docs/language_specification/identifiers.md`**: Defines identifier naming rules
- **`docs/language_specification/variable_scoping.md`**: Specifies scoping rules that the symbol table implements

---

## Patterns and Design Decisions

### 1. **Scope Stack Pattern**

The symbol table uses a **stack-based scope management** pattern, which is standard in most compilers:

```
[Global Scope] ← bottom of stack
[Function Scope]
[If-Block Scope] ← top of stack (current scope)
```

**Why a stack?**
- Natural representation of nested scopes
- Efficient push/pop operations (O(1))
- Easy to implement shadowing (inner scopes checked first)
- Automatic cleanup when exiting scopes

### 2. **Separation of Concerns**

The `SymbolTable` is the **orchestrator**, while `Scope` is the **worker**:

- **`SymbolTable`**: Manages scope lifecycle (push/pop), provides high-level API
- **`Scope`**: Implements actual symbol storage and lookup logic

This separation makes the code easier to test and reason about. You can test `Scope` logic independently of scope stack management.

### 3. **Builtin Injection via Constructor**

Builtins are **injected** rather than hardcoded:

```csharp
public SymbolTable(BuiltinRegistry builtins)
```

**Benefits:**
- Testability: Unit tests can inject a mock/minimal builtin registry
- Flexibility: Could support different builtin sets for different language versions
- Clear dependency: Explicit that `SymbolTable` depends on `BuiltinRegistry`

### 4. **Overload Simplification**

Only one function symbol per name in the symbol table, even with multiple overloads. This keeps the symbol table simple and focused on **name existence**, delegating **overload resolution** to the type checker.

**Tradeoff**: This means you can't get all overloads directly from the symbol table - you need to go through `BuiltinRegistry` or other mechanisms. But it simplifies the symbol table's API and responsibility.

### 5. **Typed Lookup Helpers**

Instead of forcing callers to cast lookup results:

```csharp
// Without helpers (verbose)
var typeSymbol = Lookup("int") as TypeSymbol;

// With helpers (clean)
var typeSymbol = LookupType("int");
```

These helpers make calling code more readable and reduce casting clutter.

---

## Debugging Tips

### 1. **Inspect Scope Depth**

If symbols aren't being found when they should be:

```csharp
Console.WriteLine($"Current scope depth: {symbolTable.ScopeDepth}");
```

- Depth = 1 means you're at global scope
- Depth > 1 means you're in nested scopes
- Unexpected depth often indicates missing `EnterScope()` or `ExitScope()` calls

### 2. **Trace Scope Names**

Scopes have debug names:

```csharp
Console.WriteLine($"Current scope: {symbolTable.CurrentScope.Name}");
```

This helps identify where you are in the code structure (e.g., "function:calculate", "class:MyClass").

### 3. **Check Symbol Existence vs. Type**

If `LookupType("x")` returns `null`, check if it's defined as a different symbol type:

```csharp
var symbol = Lookup("x");
if (symbol != null)
{
    Console.WriteLine($"Found 'x' but it's a {symbol.GetType().Name}, not a type");
}
```

This catches bugs where you're looking up the right name but expecting the wrong symbol kind.

### 4. **Global vs. Local Lookups**

To debug shadowing issues:

```csharp
// Check if a symbol is defined locally
var local = Lookup("x", searchParents: false);

// Check if a symbol is defined globally
var global = GlobalScope.Lookup("x", searchParents: false);

if (local != null && global != null && local != global)
{
    Console.WriteLine("Variable 'x' is shadowing a global");
}
```

### 5. **Builtin Verification**

After construction, verify builtins were loaded:

```csharp
var intType = symbolTable.LookupType("int");
var printFunc = symbolTable.LookupFunction("print");

if (intType == null || printFunc == null)
{
    Console.WriteLine("ERROR: Builtins not loaded correctly!");
}
```

This catches issues with `BuiltinRegistry` initialization.

### 6. **Scope Stack Balance**

If you suspect scope mismatches:

```csharp
int depthBefore = symbolTable.ScopeDepth;
// ... process some code ...
int depthAfter = symbolTable.ScopeDepth;

if (depthBefore != depthAfter)
{
    Console.WriteLine($"WARNING: Scope stack imbalance! Before={depthBefore}, After={depthAfter}");
}
```

Each `EnterScope()` should be paired with exactly one `ExitScope()`.

---

## Contribution Guidelines

### When to Modify This File

You might need to modify `SymbolTable.cs` if:

1. **Adding New Symbol Types**: If you add a new symbol type (e.g., `NamespaceSymbol`), add a typed lookup helper:
   ```csharp
   public NamespaceSymbol? LookupNamespace(string name)
   {
       return Lookup(name) as NamespaceSymbol;
   }
   ```

2. **Changing Scope Semantics**: If the language adds new scoping constructs (e.g., block-level scoping for `let` variables), you might need additional scope management methods.

3. **Performance Optimizations**: If symbol lookups become a bottleneck, you might add caching or indexing (though premature optimization should be avoided).

4. **Enhanced Diagnostics**: Adding methods to dump the symbol table state or export scope hierarchies for debugging.

### What NOT to Change

**Do not modify**:

1. The core scope stack mechanism - this is a fundamental compiler pattern
2. The builtin population logic - this should remain in `PopulateBuiltins()` for clarity
3. The separation between name existence (symbol table) and overload resolution (type checker)

### Testing Considerations

When modifying `SymbolTable`, ensure:

1. **Scope Balance**: Every code path that calls `EnterScope()` must call `ExitScope()` (even in error cases)
2. **Builtin Integrity**: Verify that all expected builtins are still registered after changes
3. **Shadowing Behavior**: Test that local variables correctly shadow globals
4. **Error Cases**: Verify that attempting to exit the global scope throws an exception

### Code Style

- Keep methods short and focused (single responsibility)
- Delegate actual work to `Scope` - `SymbolTable` should orchestrate, not implement
- Maintain symmetry (e.g., `EnterScope`/`ExitScope`, `Define`/`Lookup`)
- Add XML documentation comments for public methods

---

## Common Usage Patterns

### Pattern 1: Processing a Function Definition

```csharp
// In semantic analyzer when visiting a function definition

// 1. Define the function in the current scope
var funcSymbol = new FunctionSymbol { Name = "greet", ... };
symbolTable.Define(funcSymbol);

// 2. Enter a new scope for the function body
symbolTable.EnterScope("function:greet");

// 3. Define parameters as local variables
foreach (var param in funcSymbol.Parameters)
{
    var paramSymbol = new VariableSymbol { Name = param.Name, Type = param.Type };
    symbolTable.Define(paramSymbol);
}

// 4. Process function body statements
// ... (lookups here will search function scope, then global scope)

// 5. Exit the function scope
symbolTable.ExitScope();
```

### Pattern 2: Checking for Duplicate Definitions

```csharp
// Before defining a new symbol, check if it already exists

var existing = symbolTable.Lookup(symbolName, searchParents: false);
if (existing != null)
{
    throw new SemanticError($"'{symbolName}' is already defined in this scope");
}
else
{
    symbolTable.Define(newSymbol);
}
```

Note: This logic is actually handled inside `Scope.Define()`, so you usually don't need to do this check manually.

### Pattern 3: Type Resolution

```csharp
// When resolving a type annotation like "list[int]"

var listType = symbolTable.LookupType("list");
if (listType == null)
{
    throw new SemanticError("Type 'list' not found");
}

var intType = symbolTable.LookupType("int");
if (intType == null)
{
    throw new SemanticError("Type 'int' not found");
}

// Create a specialized generic type
var listOfInt = new SemanticType.Generic(listType, new[] { intType });
```

### Pattern 4: Conditional Import

```csharp
// When importing a module, avoid re-importing

var existingModule = symbolTable.Lookup(moduleName);
if (existingModule is ModuleSymbol)
{
    // Already imported, skip
    return;
}

// Not imported yet, load it
var moduleSymbol = LoadModule(modulePath);
symbolTable.Define(moduleSymbol);
```

---

## Summary

The `SymbolTable` is a straightforward but critical component of the Sharpy compiler. Its responsibilities are clear:

1. **Manage scope hierarchy** via a stack
2. **Store symbols** as they're defined
3. **Look up symbols** when referenced
4. **Pre-populate builtins** so they're always available

It's designed to be simple, focused, and efficient - doing one thing well. The actual complexity of symbol handling (redefinition rules, shadowing, etc.) is delegated to the `Scope` class, keeping the `SymbolTable` clean and maintainable.

As a newcomer to the Sharpy compiler, understanding the `SymbolTable` is essential because almost every other semantic analysis component depends on it. Master this, and you'll have a solid foundation for understanding name resolution, type checking, and code generation.
