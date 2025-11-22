# Walkthrough: Scope.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Scope.cs`

---

## Overview

The `Scope` class is a fundamental building block of the Sharpy compiler's semantic analysis system. It manages symbol definitions within a single lexical scope and provides hierarchical symbol lookup through parent scope references. Think of it as a single layer in the scope chain - whether that's the global scope, a function scope, a class scope, or a block scope.

### Role in the Project

- **Symbol Management**: Stores symbol definitions (variables, functions, types) within a specific scope
- **Hierarchical Lookup**: Supports nested scopes by linking to parent scopes
- **Redefinition Control**: Enforces Sharpy's scoping rules, including Python-like variable redefinition semantics
- **Foundation for SymbolTable**: `Scope` is used by `SymbolTable` to build a stack of scopes during semantic analysis

The `Scope` class is intentionally simple and focused - it manages a single scope level. The more complex `SymbolTable` class (see `SymbolTable.cs`) uses a stack of `Scope` instances to manage the entire scope hierarchy during compilation.

---

## Class Structure

### Class Definition

```csharp
public class Scope
{
    private readonly Dictionary<string, Symbol> _symbols = new();
    private readonly Scope? _parent;
    public string Name { get; }
}
```

### Core Components

**Private Fields:**
- `_symbols`: A dictionary mapping symbol names to their `Symbol` objects (variables, functions, types, etc.)
- `_parent`: A nullable reference to the parent scope (null for global scope)

**Public Properties:**
- `Name`: A descriptive name for debugging (e.g., "global", "function:greet", "class:Calculator")

---

## Key Methods

### 1. Constructor

```csharp
public Scope(string name, Scope? parent = null)
{
    Name = name;
    _parent = parent;
}
```

**Purpose**: Creates a new scope with an optional parent scope.

**Parameters:**
- `name`: Descriptive name for the scope (used in debugging and error messages)
- `parent`: Optional parent scope for hierarchical lookup (null for global scope)

**Usage Example:**
```csharp
// Creating the global scope
var globalScope = new Scope("global");

// Creating a function scope nested in global
var functionScope = new Scope("function:main", parent: globalScope);

// Creating a class scope
var classScope = new Scope("class:MyClass", parent: globalScope);
```

**Design Note**: The parent is stored as `readonly`, meaning scope hierarchies are immutable once created. This prevents accidental scope chain modifications during analysis.

---

### 2. Define - Symbol Registration

```csharp
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

        // For all other cases (constants, functions, types, etc.), redefinition is an error
        throw new SemanticError($"Symbol '{symbol.Name}' is already defined in this scope");
    }

    _symbols[symbol.Name] = symbol;
}
```

**Purpose**: Adds a symbol to the current scope, with intelligent redefinition handling.

**Parameters:**
- `symbol`: The symbol to define (could be `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`, etc.)

**Key Behavior - Python-like Variable Redefinition:**

This method implements a critical language design decision: **non-const variables can be redefined within the same scope**, matching Python's behavior:

```python
# Valid in Python and Sharpy
x = 5        # x is int
x = "hello"  # x is now str (redefinition allowed)

# Invalid - constants cannot be redefined
PI = 3.14
PI = 3.14159  # Error!
```

**Implementation Details:**

1. **Check for existing symbol** using `TryGetValue`
2. **If symbol exists:**
   - **Both old and new are non-const variables?** → Allow redefinition (replace old with new)
   - **Otherwise (constants, functions, types)?** → Throw `SemanticError`
3. **If symbol doesn't exist:** Add it to the dictionary

**Examples of What's Allowed vs. Forbidden:**

```csharp
// ✅ ALLOWED: Redefining non-const variable
scope.Define(new VariableSymbol { Name = "x", Type = intType, IsConstant = false });
scope.Define(new VariableSymbol { Name = "x", Type = strType, IsConstant = false }); // OK!

// ❌ FORBIDDEN: Redefining constant
scope.Define(new VariableSymbol { Name = "PI", Type = floatType, IsConstant = true });
scope.Define(new VariableSymbol { Name = "PI", Type = floatType, IsConstant = true }); // Error!

// ❌ FORBIDDEN: Redefining function
scope.Define(new FunctionSymbol { Name = "greet", ... });
scope.Define(new FunctionSymbol { Name = "greet", ... }); // Error!

// ❌ FORBIDDEN: Redefining type
scope.Define(new TypeSymbol { Name = "MyClass", ... });
scope.Define(new TypeSymbol { Name = "MyClass", ... }); // Error!
```

**Why This Matters:**

This behavior enables Sharpy's gradual typing and type inference while maintaining type safety. A variable can be assigned different types throughout its lifetime in a scope, and the semantic analyzer tracks these type changes for proper type checking.

---

### 3. Lookup - Symbol Resolution

```csharp
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
```

**Purpose**: Searches for a symbol by name, optionally traversing parent scopes.

**Parameters:**
- `name`: The symbol name to search for
- `searchParent`: Whether to recursively search parent scopes (default: true)

**Return Value:**
- The `Symbol` if found, or `null` if not found

**Lookup Algorithm:**

1. **Search current scope** - Check `_symbols` dictionary
2. **If not found and `searchParent` is true** - Recursively call `Lookup` on parent scope
3. **If still not found or no parent** - Return `null`

**Examples:**

```csharp
// Setup nested scopes
var globalScope = new Scope("global");
globalScope.Define(new VariableSymbol { Name = "x", Type = intType });

var funcScope = new Scope("function", parent: globalScope);
funcScope.Define(new VariableSymbol { Name = "y", Type = strType });

// Lookup in function scope
var y = funcScope.Lookup("y");        // ✅ Found in current scope
var x = funcScope.Lookup("x");        // ✅ Found in parent (global)
var z = funcScope.Lookup("z");        // ❌ Returns null

// Lookup without parent search
var x2 = funcScope.Lookup("x", searchParent: false);  // ❌ Returns null (only checks funcScope)
```

**When to Use `searchParent: false`:**

Setting `searchParent: false` is useful when you need to check if a symbol is defined **only in the current scope**, such as:

- Checking for shadowing/hiding of parent symbols
- Preventing redefinition within the same scope
- Implementing block-scoped semantics

**Example from TypeChecker:**
```csharp
// Check if loop variable is already defined in this specific scope
if (_symbolTable.Lookup(id.Name, searchParents: false) == null)
{
    _symbolTable.Define(loopVarSymbol);  // Only define if not in current scope
}
```

---

### 4. Contains - Local Scope Check

```csharp
public bool Contains(string name)
{
    return _symbols.ContainsKey(name);
}
```

**Purpose**: Checks if a symbol exists in **this scope only** (no parent search).

**Parameters:**
- `name`: Symbol name to check

**Return Value:**
- `true` if symbol exists in current scope, `false` otherwise

**Difference from Lookup:**
- `Contains(name)` is equivalent to `Lookup(name, searchParent: false) != null`
- More semantic and readable when you want to check local scope only

---

### 5. GetAllSymbols - Scope Inspection

```csharp
public IEnumerable<Symbol> GetAllSymbols()
{
    return _symbols.Values;
}
```

**Purpose**: Returns all symbols defined in this scope (used for debugging, analysis, or iteration).

**Return Value:**
- Collection of all `Symbol` objects in the current scope (no parent symbols)

**Use Cases:**
- Debugging: Printing all symbols in a scope
- Code generation: Iterating over all local variables
- Analysis: Finding all symbols of a specific type in a scope

---

## Dependencies

### Direct Dependencies

**Symbol Types (from `Symbol.cs`):**
- `Symbol` - Base class for all symbols
- `VariableSymbol` - Variables and fields
- `FunctionSymbol` - Functions and methods
- `TypeSymbol` - Classes, structs, interfaces, enums
- `ModuleSymbol` - Imported modules
- `ParameterSymbol` - Function parameters

**Exception Types (from `SemanticError.cs`):**
- `SemanticError` - Thrown when symbol redefinition rules are violated

### Used By

**`SymbolTable.cs`:**
- Manages a stack of `Scope` instances
- Creates new scopes via `EnterScope()`
- Delegates `Define()` and `Lookup()` to current scope

**Semantic Analysis Components:**
- `TypeChecker.cs` - Uses SymbolTable (which uses Scope) for type checking
- `NameResolver.cs` - Populates scopes with symbols during first pass
- `AccessValidator.cs` - Validates symbol access across scopes

---

## Patterns and Design Decisions

### 1. Immutable Parent Chain

The `_parent` field is `readonly`, making the scope hierarchy immutable after construction. This prevents accidental scope chain corruption during analysis.

**Why This Matters:**
- **Thread Safety**: Immutable structures are inherently thread-safe
- **Predictability**: Scope relationships never change after creation
- **Debugging**: Easier to reason about scope structure during debugging

### 2. Dictionary-Based Symbol Storage

Using `Dictionary<string, Symbol>` provides O(1) lookup performance, critical for compilation speed.

**Alternative Considered**: List-based storage would require O(n) lookup, unacceptable for large scopes.

### 3. Recursive Parent Lookup

The `Lookup()` method uses recursion to traverse parent scopes:

```csharp
return _parent.Lookup(name, searchParent);
```

**Pros:**
- Clean, readable implementation
- Natural fit for hierarchical structure
- Easy to understand and maintain

**Cons:**
- Stack depth limited by scope nesting (rarely a problem in practice)
- Could theoretically cause stack overflow with deeply nested scopes (hundreds of levels)

**Design Decision**: Recursion chosen for clarity. Real-world code rarely nests scopes deeply enough to cause issues.

### 4. Python-like Variable Semantics

The `Define()` method allows variable redefinition within the same scope, matching Python behavior:

```python
x = 5        # int
x = "hello"  # str (redefinition allowed)
```

**Why This Design:**
- **Dynamic Feel**: Makes Sharpy feel more Python-like while maintaining static typing
- **Type Inference**: Enables sophisticated type narrowing and flow-sensitive typing
- **Developer Experience**: Matches expectations of Python developers

**Trade-off**: More complex symbol management (must track type changes), but aligns with language goals.

### 5. Separation of Concerns

`Scope` handles **single scope management**, while `SymbolTable` handles **scope hierarchy**. This separation keeps each class focused and testable.

**Scope Responsibilities:**
- Symbol storage
- Local lookup
- Redefinition rules

**SymbolTable Responsibilities:**
- Scope stack management
- Scope creation/destruction
- Delegating to current scope

---

## Debugging Tips

### 1. Inspecting Scope Contents

When debugging symbol resolution issues, inspect the scope's contents:

```csharp
// In debugger or with logging
var allSymbols = scope.GetAllSymbols();
foreach (var symbol in allSymbols)
{
    Console.WriteLine($"{symbol.Name}: {symbol.GetType().Name}");
}
```

### 2. Tracing Lookup Failures

When `Lookup()` returns null, trace the lookup path:

```csharp
public Symbol? Lookup(string name, bool searchParent = true)
{
    Console.WriteLine($"Looking up '{name}' in scope '{Name}'");
    
    if (_symbols.TryGetValue(name, out var symbol))
    {
        Console.WriteLine($"Found '{name}' in scope '{Name}'");
        return symbol;
    }

    if (searchParent && _parent != null)
    {
        Console.WriteLine($"Not found in '{Name}', checking parent...");
        return _parent.Lookup(name, searchParent);
    }

    Console.WriteLine($"'{name}' not found");
    return null;
}
```

### 3. Debugging Redefinition Errors

When encountering "already defined" errors, check:

1. **Symbol types**: Are both symbols variables? Constants?
2. **Scope level**: Is the redefinition in the same scope or a child scope?
3. **IsConstant flag**: Are the variables marked as constant?

```csharp
// Add logging to Define()
if (_symbols.TryGetValue(symbol.Name, out var existingSymbol))
{
    Console.WriteLine($"Symbol '{symbol.Name}' already exists:");
    Console.WriteLine($"  Existing: {existingSymbol.GetType().Name}, IsConstant={existingVar?.IsConstant}");
    Console.WriteLine($"  New: {symbol.GetType().Name}, IsConstant={newVar?.IsConstant}");
    // ... rest of logic
}
```

### 4. Visualizing Scope Hierarchy

Create a helper method to visualize the scope chain:

```csharp
public string GetScopeChain()
{
    var chain = new List<string>();
    var current = this;
    while (current != null)
    {
        chain.Add(current.Name);
        current = current._parent;
    }
    return string.Join(" -> ", chain);
}

// Usage:
// Output: "function:greet -> class:MyClass -> global"
```

### 5. Common Issues and Solutions

**Issue**: Symbol not found even though it should exist
- **Check**: Is `searchParent: false` being used incorrectly?
- **Check**: Was the symbol defined in a sibling scope instead of parent?
- **Check**: Is the scope stack being managed correctly in SymbolTable?

**Issue**: Unexpected redefinition allowed/forbidden
- **Check**: Are both symbols `VariableSymbol` instances?
- **Check**: Is the `IsConstant` flag set correctly?
- **Check**: Are you defining in the same scope or a nested scope?

---

## Contribution Guidelines

### Adding New Features

**1. Extending Symbol Redefinition Rules**

If you need to add special redefinition behavior:

```csharp
public void Define(Symbol symbol)
{
    if (_symbols.TryGetValue(symbol.Name, out var existingSymbol))
    {
        // Add new rule here (e.g., for properties)
        if (existingSymbol is PropertySymbol && symbol is PropertySymbol)
        {
            // Custom property redefinition logic
            HandlePropertyRedefinition(existingSymbol, symbol);
            return;
        }
        
        // ... existing variable redefinition logic ...
    }
    
    _symbols[symbol.Name] = symbol;
}
```

**2. Adding Scope Metadata**

If you need to track additional scope information:

```csharp
public class Scope
{
    // ... existing fields ...
    
    // New metadata
    public ScopeKind Kind { get; }  // e.g., Global, Function, Class, Block
    public bool IsLoopScope { get; }  // For break/continue validation
    
    public Scope(string name, Scope? parent = null, ScopeKind kind = ScopeKind.Block)
    {
        Name = name;
        _parent = parent;
        Kind = kind;
    }
}
```

**3. Adding Specialized Lookup**

For advanced lookup scenarios:

```csharp
public Symbol? LookupInClassHierarchy(string name)
{
    // Custom lookup that follows class inheritance instead of lexical scoping
    // Useful for method resolution
}

public IEnumerable<Symbol> LookupAll(string name)
{
    // Returns all symbols with the given name in scope chain (for shadowing analysis)
}
```

### Testing Guidelines

When modifying `Scope`, ensure you test:

**1. Basic Operations:**
- Defining symbols
- Looking up symbols
- Contains checks
- GetAllSymbols

**2. Redefinition Scenarios:**
- Variable redefinition (allowed)
- Constant redefinition (forbidden)
- Function redefinition (forbidden)
- Type redefinition (forbidden)

**3. Hierarchical Lookup:**
- Finding symbols in current scope
- Finding symbols in parent scope
- Finding symbols in grandparent scope
- Not finding symbols (returns null)
- searchParent parameter behavior

**4. Edge Cases:**
- Empty scope
- Scope with no parent (global)
- Deeply nested scopes
- Symbol name collisions across scopes (shadowing)

### Example Test Structure

```csharp
[Fact]
public void Define_AllowsVariableRedefinition()
{
    var scope = new Scope("test");
    var var1 = new VariableSymbol { Name = "x", Type = intType, IsConstant = false };
    var var2 = new VariableSymbol { Name = "x", Type = strType, IsConstant = false };
    
    scope.Define(var1);
    scope.Define(var2);  // Should not throw
    
    var result = scope.Lookup("x");
    Assert.Equal(strType, ((VariableSymbol)result).Type);  // Should be str, not int
}

[Fact]
public void Define_ForbidsConstantRedefinition()
{
    var scope = new Scope("test");
    var const1 = new VariableSymbol { Name = "PI", Type = floatType, IsConstant = true };
    var const2 = new VariableSymbol { Name = "PI", Type = floatType, IsConstant = true };
    
    scope.Define(const1);
    Assert.Throws<SemanticError>(() => scope.Define(const2));
}

[Fact]
public void Lookup_FindsSymbolInParentScope()
{
    var parent = new Scope("parent");
    var child = new Scope("child", parent);
    
    var symbol = new VariableSymbol { Name = "x", Type = intType };
    parent.Define(symbol);
    
    var result = child.Lookup("x");
    Assert.Equal(symbol, result);
}
```

### Performance Considerations

**Current Performance:**
- `Define()`: O(1) average (dictionary insert)
- `Lookup()`: O(d) where d is scope depth (typically very small)
- `Contains()`: O(1) (dictionary lookup)
- `GetAllSymbols()`: O(n) where n is number of symbols in scope

**If you need to optimize:**
- **Caching**: Add lookup cache for frequently accessed symbols
- **Iterative Lookup**: Replace recursive lookup with iterative for very deep scopes
- **Scope Flattening**: Pre-flatten scope chain for hot paths

### Code Style

When contributing to `Scope.cs`:

1. **Keep it simple**: This class should remain focused and lightweight
2. **Maintain immutability**: Don't make `_parent` or `_symbols` mutable
3. **Clear error messages**: Use descriptive error messages with symbol names
4. **Document special cases**: Comment any non-obvious behavior (like variable redefinition)
5. **Follow existing patterns**: Match the coding style of existing methods

---

## Summary

The `Scope` class is a fundamental, well-designed component that:

- **Manages symbols** within a single lexical scope
- **Enables hierarchical lookup** through parent references
- **Implements Python-like semantics** for variable redefinition
- **Remains simple and focused** while serving as the foundation for `SymbolTable`

Understanding `Scope` is essential for working on semantic analysis, as it's the basic building block used throughout name resolution, type checking, and symbol management in the Sharpy compiler.

**Next Steps:**
- Read `SymbolTable.cs` to see how `Scope` is used in a stack-based scope manager
- Read `Symbol.cs` to understand the different symbol types
- Read `NameResolver.cs` and `TypeChecker.cs` to see `Scope` in action during semantic analysis
