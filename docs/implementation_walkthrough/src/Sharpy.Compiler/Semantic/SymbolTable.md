# Walkthrough: SymbolTable.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SymbolTable.cs`

---

## 1. Overview

**What is SymbolTable?**

`SymbolTable` is the central hub for managing all symbols (variables, functions, types, etc.) during the semantic analysis phase of the Sharpy compiler. Think of it as the "phone book" that the compiler uses to look up what names mean in your code.

**Role in the Compiler Pipeline:**

```
Lexer → Parser → [NameResolver → TypeResolver → TypeChecker] → CodeGen
                       ↓              ↓              ↓
                  SymbolTable    SymbolTable    SymbolTable
```

The SymbolTable is used throughout the entire semantic analysis phase:
- **NameResolver**: Populates the symbol table with declarations (functions, classes, variables)
- **TypeResolver**: Looks up type names and resolves type references
- **TypeChecker**: Verifies that expressions use correctly-typed symbols

**Key Responsibilities:**
- Maintains a stack of nested scopes (global → module → function → block)
- Provides symbol lookup with automatic parent scope traversal
- Ensures builtin types and functions are always available
- Prevents duplicate symbol definitions (with special handling for Python-like variable reassignment)

---

## 2. Class Structure

### Main Class: `SymbolTable`

```csharp
public class SymbolTable
{
    private readonly Stack<Scope> _scopeStack = new();
    private readonly Scope _globalScope;
    private readonly BuiltinRegistry _builtins;
    
    // ... methods ...
}
```

**Key Data Structures:**

1. **`_scopeStack`**: A stack of `Scope` objects representing nested scopes
   - The top of the stack is always the "current" scope
   - Pushed when entering functions, classes, or blocks
   - Popped when exiting those constructs

2. **`_globalScope`**: The bottom scope that never gets popped
   - Contains builtin types (`int`, `str`, `list`, etc.)
   - Contains builtin functions (`print`, `len`, `range`, etc.)
   - Always the first scope pushed onto `_scopeStack`

3. **`_builtins`**: The `BuiltinRegistry` that provides all Sharpy.Core types and functions
   - Loaded from reflection over the `Sharpy.Core` assembly
   - Used to populate the global scope on initialization

---

## 3. Key Methods Walkthrough

### 3.1 Constructor: `SymbolTable(BuiltinRegistry builtins)`

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
- Creates the global scope and pushes it onto the stack
- Calls `PopulateBuiltins()` to add all Sharpy standard library symbols

**When it's called:**
The compiler creates a new `SymbolTable` for each compilation unit in `Compiler.cs`:

```csharp
var builtinRegistry = new BuiltinRegistry();
var symbolTable = new SymbolTable(builtinRegistry);
```

**Why the global scope starts on the stack:**
This ensures you can never accidentally pop it off - the `ExitScope()` method checks `_scopeStack.Count <= 1` to prevent this.

---

### 3.2 `PopulateBuiltins()`

```csharp
private void PopulateBuiltins()
{
    // Add builtin types
    foreach (var (name, typeSymbol) in _builtins.GetAllTypes())
    {
        _globalScope.Define(typeSymbol);
    }
    
    // Add builtin functions (only add one symbol per function name)
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
1. Adds all builtin types to the global scope: `int`, `str`, `bool`, `list`, `dict`, etc.
2. Adds builtin functions, but with a **critical caveat**...

**The Function Overload Problem:**

Notice the `addedFunctions` HashSet? This prevents duplicate symbols for overloaded functions. For example, `len()` can be called on different types:
- `len(str)` → `int`
- `len(list[T])` → `int`
- `len(dict[K,V])` → `int`

**Why only add the first overload?**
- The symbol table uses a simple `Dictionary<string, Symbol>` that can't store multiple functions with the same name
- Proper overload resolution happens later in the `TypeChecker` using `BuiltinRegistry.GetFunctionOverloads()`
- The single symbol in the global scope just marks that the name exists

**Design Trade-off:**
This is a pragmatic choice - symbol tables traditionally map names to single symbols. Overload resolution is a separate concern handled by type checking.

---

### 3.3 `EnterScope(string name)` and `ExitScope()`

```csharp
public void EnterScope(string name)
{
    var newScope = new Scope(name, CurrentScope);
    _scopeStack.Push(newScope);
}

public void ExitScope()
{
    if (_scopeStack.Count <= 1)
    {
        throw new InvalidOperationException("Cannot exit global scope");
    }
    _scopeStack.Pop();
}
```

**What they do:**
- `EnterScope`: Creates a new child scope and pushes it onto the stack
- `ExitScope`: Pops the current scope (unless it's the global scope)

**When they're called:**

```csharp
// In NameResolver or TypeChecker
symbolTable.EnterScope("function:my_function");
// ... process function body ...
symbolTable.ExitScope();
```

**Typical scope hierarchy:**
```
global
  └─ module:my_module
      └─ function:calculate
          └─ block:if_statement
```

**Scope naming convention:**
Names like `"function:my_function"` are purely for debugging - they help when you need to print the scope stack to understand where you are.

**Error safety:**
The `ExitScope()` guard prevents catastrophic bugs where you accidentally pop the global scope, which would make all builtins disappear!

---

### 3.4 `Define(Symbol symbol)`

```csharp
public void Define(Symbol symbol)
{
    CurrentScope.Define(symbol);
}
```

**What it does:**
Delegates to the current scope's `Define()` method, which:
- Adds the symbol to the current scope's dictionary
- Handles redefinition logic (see `Scope.cs` for details)

**When it's called:**

```csharp
// In NameResolver, when encountering a function definition:
var funcSymbol = new FunctionSymbol 
{ 
    Name = "my_function", 
    Parameters = ...,
    ReturnType = ...
};
symbolTable.Define(funcSymbol);
```

**Important behavior from `Scope.Define()`:**
- ✅ **Allows**: Reassigning non-const variables (Python-like behavior)
- ❌ **Disallows**: Redefining functions, types, or const variables

```python
# This is OK in Sharpy (and Python):
x = 5        # x is int
x = "hello"  # x is now str

# This is NOT OK:
def foo(): pass
def foo(): pass  # Error: Symbol 'foo' already defined
```

---

### 3.5 `Lookup(string name, bool searchParents = true)`

```csharp
public Symbol? Lookup(string name, bool searchParents = true)
{
    return CurrentScope.Lookup(name, searchParents);
}
```

**What it does:**
Searches for a symbol by name, starting at the current scope and (optionally) walking up the parent chain.

**The search process:**

```
Current scope:    [x, y]  ← Look here first
Parent scope:     [foo, bar]  ← Then here
Global scope:     [int, str, print, len]  ← Finally here
```

**When `searchParents=false` is used:**
Rare, but useful when you specifically want to check if a name is defined in the *current* scope only (e.g., to detect local variable shadowing).

**Return value:**
- Returns the `Symbol` if found
- Returns `null` if not found
- The caller must handle the `null` case (usually as an "undefined variable" error)

**Example usage in TypeChecker:**

```csharp
var symbol = symbolTable.Lookup("my_variable");
if (symbol == null)
{
    _logger.LogError($"Undefined variable: my_variable");
    return SemanticType.Error;
}
```

---

### 3.6 Specialized Lookup Methods

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
```

**What they do:**
Convenience methods that combine lookup + type casting.

**Why they exist:**
Type safety and cleaner code. Instead of:

```csharp
var symbol = symbolTable.Lookup("MyClass");
if (symbol is TypeSymbol typeSymbol) { ... }
```

You can write:

```csharp
var typeSymbol = symbolTable.LookupType("MyClass");
if (typeSymbol != null) { ... }
```

**When to use which:**
- Use `LookupType()` when resolving type annotations: `x: MyClass`
- Use `LookupFunction()` when resolving function calls: `my_func()`
- Use `LookupVariable()` when resolving variable references: `my_var + 5`
- Use `Lookup()` when you don't know what kind of symbol you're looking for

---

### 3.7 Properties

```csharp
public Scope CurrentScope => _scopeStack.Peek();
public Scope GlobalScope => _globalScope;
public int ScopeDepth => _scopeStack.Count;
public BuiltinRegistry BuiltinRegistry => _builtins;
```

**Why they're useful:**

- **`CurrentScope`**: Direct access for advanced operations (rarely needed)
- **`GlobalScope`**: Access to module-level symbols or for adding imports
- **`ScopeDepth`**: Debugging - helps track nested scope issues
- **`BuiltinRegistry`**: Allows `TypeChecker` to perform overload resolution

---

## 4. Dependencies

### 4.1 Direct Dependencies

| Dependency | Purpose |
|------------|---------|
| **`Scope`** | Manages individual scope dictionaries |
| **`Symbol` (and subclasses)** | Data structures representing variables, functions, types |
| **`BuiltinRegistry`** | Provides Sharpy.Core builtin types and functions |

### 4.2 Used By (Consumers)

| Consumer | How It Uses SymbolTable |
|----------|------------------------|
| **`NameResolver`** | Populates with function/class/variable declarations |
| **`TypeResolver`** | Looks up type names in type annotations |
| **`TypeChecker`** | Verifies expressions reference valid, well-typed symbols |
| **`ImportResolver`** | Adds imported symbols to current scope |

### 4.3 Key Related Files

```
Semantic/
├── SymbolTable.cs         ← You are here
├── Scope.cs               ← Individual scope implementation
├── Symbol.cs              ← Symbol type definitions
├── BuiltinRegistry.cs     ← Loads Sharpy.Core builtins
├── NameResolver.cs        ← Populates symbol table
├── TypeResolver.cs        ← Uses symbol table
└── TypeChecker.cs         ← Uses symbol table
```

---

## 5. Design Patterns & Decisions

### 5.1 Scope Stack Pattern

**Pattern**: Stack-based scope management
**Why**: Natural fit for nested scopes with push/pop semantics

```python
# Sharpy code
x = 1                 # global scope
def foo():
    y = 2             # function scope
    if True:
        z = 3         # block scope (hypothetically)
```

```
Scope stack:
[global] → [global, function:foo] → [global, function:foo, block:if]
```

### 5.2 Separation of Concerns

**Decision**: Symbol table only stores symbols; type information lives in `SemanticInfo`

**Why**: 
- AST nodes are immutable (created by parser)
- Adding type info to symbols would require mutation during type checking
- `SemanticInfo` maps AST nodes → types separately

**Example**:
```csharp
// SymbolTable stores the symbol
var varSymbol = new VariableSymbol { Name = "x", Type = SemanticType.Int };
symbolTable.Define(varSymbol);

// SemanticInfo stores type of *expressions*
var expr = /* some AST expression node */;
semanticInfo.SetType(expr, SemanticType.Int);
```

### 5.3 Builtin Overload Strategy

**Decision**: Store one symbol per function name; resolve overloads in type checker

**Trade-off**:
- ✅ **Pro**: Simpler symbol table (no multi-value dictionary needed)
- ✅ **Pro**: Overload resolution belongs conceptually in type checking
- ❌ **Con**: Can't tell from symbol table alone how many overloads exist

**Alternative considered**: Store `List<FunctionSymbol>` for each function name
- **Why rejected**: Complicates symbol table API; all other lookups return single symbols

### 5.4 Python-Like Variable Reassignment

**Decision**: Allow variables (non-const) to be redefined

```csharp
// In Scope.Define():
if (existingVar && !existingVar.IsConstant && newVar && !newVar.IsConstant)
{
    _symbols[symbol.Name] = symbol;  // Replace
    return;
}
```

**Why**: Match Python semantics where variables can change type:

```python
x = 5
x = "hello"  # Valid in Python and Sharpy
```

**But**: Functions, types, and const variables cannot be redefined

---

## 6. Debugging Tips

### 6.1 Print the Scope Stack

Add this helper method to debug scope issues:

```csharp
public void DebugPrintScopes()
{
    Console.WriteLine($"Scope depth: {ScopeDepth}");
    var scopes = _scopeStack.ToArray().Reverse();
    foreach (var scope in scopes)
    {
        Console.WriteLine($"  Scope: {scope.Name}");
        foreach (var symbol in scope.GetAllSymbols())
        {
            Console.WriteLine($"    - {symbol.Name} ({symbol.Kind})");
        }
    }
}
```

### 6.2 Scope Mismatch Errors

**Symptom**: `InvalidOperationException: Cannot exit global scope`

**Cause**: Mismatched `EnterScope`/`ExitScope` calls

**Debug strategy**:
1. Add logging to every `EnterScope`/`ExitScope` call
2. Check if error handling paths skip `ExitScope` (use try-finally!)
3. Verify recursive functions properly exit scopes

```csharp
// Good pattern:
symbolTable.EnterScope("function:foo");
try
{
    // Process function body
}
finally
{
    symbolTable.ExitScope();  // Always executes
}
```

### 6.3 Symbol Not Found

**Symptom**: `Lookup()` returns `null` when you expect a symbol to exist

**Common causes**:
1. Symbol not yet defined (order-of-declaration issue)
2. Shadowing by local variable
3. Typo in symbol name
4. Wrong scope depth (forgot to `EnterScope` or premature `ExitScope`)

**Debug strategy**:
```csharp
var symbol = symbolTable.Lookup("problematic_name");
if (symbol == null)
{
    Console.WriteLine($"Current scope: {symbolTable.CurrentScope.Name}");
    Console.WriteLine($"Scope depth: {symbolTable.ScopeDepth}");
    symbolTable.DebugPrintScopes();
}
```

### 6.4 Builtin Not Available

**Symptom**: Lookup fails for `print`, `len`, etc.

**Cause**: `BuiltinRegistry` didn't load properly or isn't reflected in assembly

**Debug strategy**:
1. Check `_globalScope.GetAllSymbols()` after construction
2. Verify `Sharpy.Core.dll` is accessible
3. Check `BuiltinRegistry.GetAllTypes()` and `GetAllFunctions()` outputs

---

## 7. Contribution Guidelines

### 7.1 When to Modify SymbolTable

**You should modify this file when:**
- Adding a new kind of scope (e.g., class scope, lambda scope)
- Changing how builtins are loaded
- Adding new symbol lookup strategies (e.g., lookup in outer modules)

**You should NOT modify this file when:**
- Adding new symbol types → modify `Symbol.cs`
- Changing type resolution logic → modify `TypeChecker.cs`
- Adding new builtin functions → modify `Sharpy.Core` and `BuiltinRegistry.cs`

### 7.2 Common Modifications

#### Adding a New Scope Type

If you need to track additional scope metadata:

```csharp
public void EnterClassScope(string className, TypeSymbol classSymbol)
{
    var newScope = new Scope($"class:{className}", CurrentScope);
    newScope.OwningClass = classSymbol;  // Hypothetical
    _scopeStack.Push(newScope);
}
```

#### Adding Lookup Filters

If you need to look up symbols with additional constraints:

```csharp
public Symbol? LookupPublic(string name)
{
    var symbol = Lookup(name);
    return symbol?.AccessLevel == AccessLevel.Public ? symbol : null;
}
```

### 7.3 Testing Considerations

**When adding/modifying SymbolTable:**

1. **Test scope nesting**:
   ```csharp
   [Fact]
   public void TestNestedScopes()
   {
       var st = new SymbolTable(new BuiltinRegistry());
       st.EnterScope("outer");
       st.Define(new VariableSymbol { Name = "x" });
       st.EnterScope("inner");
       Assert.NotNull(st.Lookup("x"));  // Should find in parent
       st.ExitScope();
       st.ExitScope();
   }
   ```

2. **Test builtin availability**:
   ```csharp
   [Fact]
   public void TestBuiltinsLoaded()
   {
       var st = new SymbolTable(new BuiltinRegistry());
       Assert.NotNull(st.LookupType("int"));
       Assert.NotNull(st.LookupFunction("print"));
   }
   ```

3. **Test redefinition rules**:
   ```csharp
   [Fact]
   public void TestVariableRedefinition()
   {
       var st = new SymbolTable(new BuiltinRegistry());
       st.Define(new VariableSymbol { Name = "x", Type = SemanticType.Int });
       st.Define(new VariableSymbol { Name = "x", Type = SemanticType.Str });
       // Should succeed (non-const variable redefinition allowed)
   }
   ```

### 7.4 Code Style

**Follow these conventions:**
- Private fields: `_camelCase`
- Public properties: `PascalCase`
- Scope names: Use descriptive strings like `"function:my_func"` or `"class:MyClass"`
- Comments: Explain *why*, not *what* (the code shows what)

### 7.5 Performance Notes

**Current implementation is O(n) for scope depth:**
```csharp
// Worst case: lookup traverses all parent scopes
var symbol = Lookup("deeply_nested_variable");
```

**If profiling shows this is a bottleneck:**
- Consider caching flattened scope views
- Use a hash-consed symbol table
- Profile first - premature optimization is evil!

---

## 8. Real-World Examples

### 8.1 How NameResolver Uses SymbolTable

```csharp
// From NameResolver.cs (simplified)
public void VisitFunctionDef(FunctionDef node)
{
    // Create function symbol
    var funcSymbol = new FunctionSymbol
    {
        Name = node.Name,
        Kind = SymbolKind.Function
    };
    
    // Define in current scope (module or class scope)
    _symbolTable.Define(funcSymbol);
    
    // Enter function scope
    _symbolTable.EnterScope($"function:{node.Name}");
    
    // Define parameters in function scope
    foreach (var param in node.Parameters)
    {
        var paramSymbol = new VariableSymbol
        {
            Name = param.Name,
            IsParameter = true
        };
        _symbolTable.Define(paramSymbol);
    }
    
    // Visit function body
    Visit(node.Body);
    
    // Exit function scope
    _symbolTable.ExitScope();
}
```

### 8.2 How TypeChecker Uses SymbolTable

```csharp
// From TypeChecker.cs (simplified)
public SemanticType VisitNameExpr(NameExpr node)
{
    // Look up the symbol
    var symbol = _symbolTable.Lookup(node.Name);
    
    if (symbol == null)
    {
        _logger.LogError($"Undefined variable: {node.Name}");
        return SemanticType.Error;
    }
    
    // Get the type based on symbol kind
    if (symbol is VariableSymbol varSymbol)
    {
        return varSymbol.Type;
    }
    else if (symbol is FunctionSymbol funcSymbol)
    {
        // Return function type (for first-class functions)
        return new SemanticType.Function(funcSymbol);
    }
    
    _logger.LogError($"Cannot use {symbol.Kind} as a value");
    return SemanticType.Error;
}
```

---

## Summary

**SymbolTable.cs is a straightforward but crucial piece of the compiler:**

- **Purpose**: Central registry for all named entities in the program
- **Design**: Simple stack-based scope management with parent chain lookup
- **Integration**: Used by all semantic analysis passes
- **Key insight**: Delegates overload resolution to TypeChecker, keeps symbol table simple

**When working with this file, remember:**
1. ✅ Always balance `EnterScope`/`ExitScope` calls (use try-finally)
2. ✅ Handle `null` returns from `Lookup()` gracefully
3. ✅ Understand the builtin overload limitation
4. ✅ Use specific lookup methods (`LookupType`, etc.) when you know the kind
5. ✅ Test scope nesting thoroughly

**Next steps for learning:**
- Read `Scope.cs` to understand individual scope behavior
- Read `Symbol.cs` to learn about symbol types
- Trace through `NameResolver.cs` to see population in action
- Study `TypeChecker.cs` to see lookup in action

---

*Document generated as part of the Sharpy internal documentation initiative.*
*Last updated: 2025-12-27*
