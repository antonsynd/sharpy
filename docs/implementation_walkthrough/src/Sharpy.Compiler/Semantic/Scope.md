# Walkthrough: Scope.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Scope.cs`

---

## 1. Overview

`Scope.cs` defines the **fundamental building block** for managing symbol visibility during semantic analysis in the Sharpy compiler. Think of a `Scope` as a container that:

- Stores symbols (variables, functions, types) within a specific code region
- Implements **lexical scoping** through parent-child relationships
- Handles **symbol resolution** by searching upward through the scope chain
- Enforces scoping rules (e.g., preventing duplicate constant definitions)

**Role in the Compiler Pipeline:**
```
Parser → AST → NameResolver/TypeChecker (uses Scope) → CodeGen
```

During semantic analysis, scopes are created and nested as the analyzer traverses the AST:
- **Global scope**: module-level definitions
- **Function scope**: function parameters and local variables
- **Class scope**: class members
- **Block scope**: variables in `if`, `while`, etc.

The `SymbolTable` class (see `SymbolTable.cs`) manages a **stack of scopes** and delegates symbol operations to the current scope.

---

## 2. Class Structure

### **Class: `Scope`**

```csharp
public class Scope
{
    private readonly Dictionary<string, Symbol> _symbols;
    private readonly Scope? _parent;
    public string Name { get; }
}
```

**Key Design Decisions:**
- **Immutable parent reference**: Once created, a scope's parent never changes (lexical scoping is static)
- **Dictionary-based storage**: O(1) symbol lookup by name
- **Nullable parent**: The global scope has no parent (`null`)
- **Named scopes**: Each scope has a descriptive name for debugging (e.g., `"function:calculate"`, `"class:MyClass"`)

**Fields:**

| Field | Type | Purpose |
|-------|------|---------|
| `_symbols` | `Dictionary<string, Symbol>` | Stores symbol definitions in this scope |
| `_parent` | `Scope?` | Reference to enclosing scope (null for global) |
| `Name` | `string` | Human-readable scope name for debugging |

---

## 3. Key Methods

### **Constructor: `Scope(string name, Scope? parent = null)`**

```csharp
public Scope(string name, Scope? parent = null)
{
    Name = name;
    _parent = parent;
}
```

**Purpose**: Creates a new scope with an optional parent reference.

**Parameters:**
- `name`: Descriptive identifier (e.g., `"global"`, `"function:main"`, `"if-block"`)
- `parent`: The enclosing scope (null only for global scope)

**Usage Pattern in SymbolTable:**
```csharp
// Entering a function creates a new child scope
var functionScope = new Scope("function:calculate", currentScope);
_scopeStack.Push(functionScope);
```

---

### **Method: `Define(Symbol symbol)`**

```csharp
public void Define(Symbol symbol)
{
    if (_symbols.TryGetValue(symbol.Name, out var existingSymbol))
    {
        // Special case: allow non-const variable redefinition
        if (existingSymbol is VariableSymbol existingVar && !existingVar.IsConstant &&
            symbol is VariableSymbol newVar && !newVar.IsConstant)
        {
            _symbols[symbol.Name] = symbol;  // Replace
            return;
        }
        
        // Otherwise, redefinition is an error
        throw new SemanticError($"Symbol '{symbol.Name}' is already defined in this scope");
    }
    
    _symbols[symbol.Name] = symbol;
}
```

**Purpose**: Adds a symbol to the current scope, enforcing redefinition rules.

**Important Implementation Details:**

1. **Python-like variable reassignment**: Non-constant variables can be "redefined" (replaced) in the same scope:
   ```python
   x = 5      # x: int
   x = "hi"   # x: str (allowed! Python permits type changes)
   ```
   This is critical for Sharpy's Pythonic semantics.

2. **Strict rules for other symbols**:
   - Constants cannot be redefined
   - Functions cannot be redefined (no ad-hoc overloading in the same scope)
   - Types cannot be redefined

**Error Cases:**
```csharp
// ❌ Redefining a constant
const PI = 3.14
const PI = 3.14159  // SemanticError

// ❌ Redefining a function
def foo(): pass
def foo(): pass  // SemanticError

// ✅ Reassigning a variable
x = 5
x = "hello"  // OK - replaces the symbol
```

**Why This Matters for Debugging:**
If you see a `SemanticError` about duplicate definitions, check:
1. Is it a constant (`IsConstant = true`)?
2. Is it a function or type being redeclared?
3. Is the compiler correctly identifying the symbol kind?

---

### **Method: `Lookup(string name, bool searchParent = true)`**

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

**Purpose**: Searches for a symbol by name, recursively checking parent scopes.

**Parameters:**
- `name`: Symbol identifier
- `searchParent`: If `true`, walks up the scope chain; if `false`, searches only the current scope

**Algorithm:**
1. Check current scope's dictionary
2. If not found and `searchParent` is true, recursively search parent
3. Return `null` if symbol not found in any scope

**Use Cases:**

```csharp
// Example 1: Variable lookup in nested scopes
// Global scope: x = 10
// Function scope: y = 20
// Looking up "x" from function scope → finds it in parent (global)

// Example 2: Shadowing detection
var local = currentScope.Lookup("x", searchParent: false);  // Only this scope
var anyScope = currentScope.Lookup("x", searchParent: true); // Any enclosing scope
if (local != null && anyScope != null && local != anyScope)
{
    // "x" shadows an outer variable
}
```

**Performance**: O(d) where d = depth in scope tree. Typically d < 5 in practice.

**Return Value:**
- Returns the **first matching symbol** found (inner scopes shadow outer)
- Returns `null` if not found (caller must handle undefined symbol errors)

---

### **Method: `Contains(string name)`**

```csharp
public bool Contains(string name)
{
    return _symbols.ContainsKey(name);
}
```

**Purpose**: Checks if a symbol exists **only in the current scope** (does not search parents).

**Use Case:**
```csharp
// Checking for local redefinition before allowing variable reassignment
if (currentScope.Contains("x"))
{
    // "x" is defined in THIS scope, safe to redefine if non-const
}
```

**Difference from `Lookup`:**
- `Contains("x")`: Only checks current scope
- `Lookup("x", searchParent: false)`: Same behavior, but returns the symbol
- `Lookup("x", searchParent: true)`: Checks current + all parent scopes

---

### **Method: `GetAllSymbols()`**

```csharp
public IEnumerable<Symbol> GetAllSymbols()
{
    return _symbols.Values;
}
```

**Purpose**: Returns all symbols defined in this scope (not including parent scopes).

**Use Cases:**
1. **Code generation**: Iterating over local variables to emit declarations
2. **Debugging**: Dumping all symbols in a scope for diagnostics
3. **Analysis**: Checking for unused variables

**Example:**
```csharp
// Report all unused local variables
foreach (var symbol in functionScope.GetAllSymbols())
{
    if (symbol is VariableSymbol varSym && !varSym.IsReferenced)
    {
        logger.Warning($"Unused variable: {varSym.Name}");
    }
}
```

---

## 4. Dependencies

### **Internal Dependencies:**

1. **`Symbol.cs`**: Defines the symbol hierarchy
   - `Symbol` (abstract base)
   - `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`, `ModuleSymbol`
   - Used in `_symbols` dictionary and method signatures

2. **`SemanticError.cs`**: Exception thrown for semantic violations
   - Thrown in `Define()` when redefinition rules are violated

3. **`SymbolTable.cs`**: The orchestrator that manages the scope stack
   - Creates `Scope` instances via `EnterScope()`
   - Delegates `Define()` and `Lookup()` calls to the current scope
   - Maintains the global scope

### **How It Fits Together:**

```csharp
// TypeChecker or NameResolver flow:
symbolTable.EnterScope("function:calculate");  // Creates new Scope

var param = new VariableSymbol 
{ 
    Name = "x", 
    Type = SemanticType.Int, 
    IsParameter = true 
};
symbolTable.Define(param);  // → CurrentScope.Define(param)

var result = symbolTable.Lookup("x");  // → CurrentScope.Lookup("x")
symbolTable.ExitScope();  // Pops the scope
```

---

## 5. Patterns and Design Decisions

### **Design Pattern: Chain of Responsibility**

The `Lookup()` method implements the **Chain of Responsibility** pattern:
- Each scope tries to handle the symbol lookup
- If it can't, it delegates to the parent
- The chain ends at the global scope (no parent)

**Benefits:**
- Clean separation of concerns (each scope manages only its symbols)
- Natural implementation of lexical scoping
- Easy to extend (e.g., adding "nonlocal" keyword support)

### **Python-like Semantics**

The special handling in `Define()` for non-const variable redefinition is a **deliberate design choice** to match Python's dynamic typing:

```python
# Python allows this:
x = 5        # x is an int
x = "hello"  # x is now a str

# Sharpy must support the same behavior for Pythonic ergonomics
```

**Trade-off:** This makes type inference more complex (variables can change types across assignments), but maintains Python compatibility.

### **Null Safety**

The `_parent` field is nullable (`Scope?`), reflecting that the global scope has no parent. The code consistently checks `_parent != null` before recursion.

**Good Practice:** When adding new methods that traverse the scope chain, always guard parent access:
```csharp
if (_parent != null)
{
    return _parent.SomeMethod();
}
```

---

## 6. Debugging Tips

### **Common Issues:**

1. **"Symbol already defined" errors**
   - Check if `Define()` is being called twice for the same symbol
   - Verify that `IsConstant` flags are set correctly
   - Look for parser bugs that create duplicate AST nodes

2. **Symbol not found (null from `Lookup()`)**
   - Use a debugger to inspect `_symbols` dictionary in each scope
   - Check the scope stack in `SymbolTable._scopeStack`
   - Verify that `EnterScope()`/`ExitScope()` calls are balanced

3. **Wrong symbol resolution (shadowing issues)**
   - Use `searchParent: false` to isolate current scope lookup
   - Inspect the `_parent` chain to see scope nesting
   - Check if `EnterScope()` was called at the right time

### **Debugging Workflow:**

```csharp
// Add this helper method to Scope for debugging:
public void DumpScope(int indent = 0)
{
    var prefix = new string(' ', indent * 2);
    Console.WriteLine($"{prefix}Scope: {Name}");
    foreach (var (name, symbol) in _symbols)
    {
        Console.WriteLine($"{prefix}  - {name}: {symbol.Kind}");
    }
    if (_parent != null)
    {
        Console.WriteLine($"{prefix}Parent:");
        _parent.DumpScope(indent + 1);
    }
}
```

### **Testing Strategy:**

When writing tests for semantic analysis:
```csharp
[Fact]
public void TestNestedScopeLookup()
{
    var global = new Scope("global");
    global.Define(new VariableSymbol { Name = "x", Type = SemanticType.Int });
    
    var local = new Scope("local", global);
    local.Define(new VariableSymbol { Name = "y", Type = SemanticType.String });
    
    Assert.NotNull(local.Lookup("x"));  // Found in parent
    Assert.NotNull(local.Lookup("y"));  // Found in current
    Assert.Null(local.Lookup("z"));     // Not found anywhere
}
```

---

## 7. Contribution Guidelines

### **Potential Enhancements:**

1. **Add "nonlocal" keyword support**
   - Extend `Define()` to allow modifying parent scope variables
   - Add a `DefineInParent(Symbol)` method
   - See Python's `nonlocal` keyword for semantics

2. **Improve error messages**
   - Include source location in `SemanticError` (use `Symbol.DeclarationLine`)
   - Show the original definition location when reporting redefinition errors

3. **Add symbol shadowing warnings**
   ```csharp
   public void DefineWithShadowWarning(Symbol symbol, ILogger logger)
   {
       var shadowed = _parent?.Lookup(symbol.Name, searchParent: true);
       if (shadowed != null)
       {
           logger.Warning($"Symbol '{symbol.Name}' shadows outer definition");
       }
       Define(symbol);
   }
   ```

4. **Performance optimization**
   - Cache lookup results for frequently accessed symbols
   - Use a `HashSet<string>` to quickly check if any parent defines a symbol

### **Testing Additions:**

If modifying this file, add tests for:
- ✅ Basic define and lookup
- ✅ Nested scope traversal
- ✅ Variable redefinition (const vs non-const)
- ✅ Symbol shadowing
- ✅ Edge cases (empty scope, null parent)

**Test Location:** `src/Sharpy.Compiler.Tests/Semantic/ScopeTests.cs`

### **Style Conventions:**

- Use nullable reference types (`Scope?`, `Symbol?`)
- Prefer `TryGetValue` over `ContainsKey` + indexer (avoids double lookup)
- Keep methods small and focused (single responsibility)
- Add XML doc comments for public APIs

---

## 8. Related Files

| File | Relationship |
|------|-------------|
| `SymbolTable.cs` | Orchestrates scope stack, delegates to `Scope` |
| `Symbol.cs` | Defines symbol types stored in `_symbols` |
| `NameResolver.cs` | Uses `Scope` to bind identifiers to declarations |
| `TypeChecker.cs` | Uses `Scope` to resolve variable types |
| `SemanticError.cs` | Exception type thrown by `Define()` |

---

## 9. Key Takeaways

✅ **`Scope` is a lightweight dictionary with parent-chaining**  
✅ **Variable redefinition is allowed (Python semantics)**  
✅ **Constants, functions, and types cannot be redefined**  
✅ **`Lookup()` implements lexical scoping via parent recursion**  
✅ **The global scope is the root (no parent)**  

**Mental Model:**
```
Global Scope ["x", "print", "int"]
    ↓ parent
Function Scope ["y", "result"]
    ↓ parent
If-Block Scope ["temp"]
```

When looking up `"x"` from the if-block, the search goes:
1. If-Block → not found
2. Function Scope → not found
3. Global Scope → **found!**

---

## 10. Next Steps

After understanding `Scope.cs`, explore:
1. **`SymbolTable.cs`**: See how scopes are managed as a stack
2. **`NameResolver.cs`**: See how scopes are created while traversing the AST
3. **`TypeChecker.cs`**: See how symbol lookups drive type inference
4. **`Symbol.cs`**: Understand the different symbol types in depth

**Hands-On Exercise:**
Try adding a `DumpHierarchy()` method that prints the entire scope tree with indentation. This will deepen your understanding of parent-child relationships!
