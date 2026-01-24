# Walkthrough: ISymbolLookup.cs

**Source File**: `src/Sharpy.Compiler/Services/ISymbolLookup.cs`

---

## Overview

`ISymbolLookup` is a **read-only service interface** that provides symbol lookup operations during semantic analysis and code generation. It's part of the centralized compiler services layer introduced to improve testability, thread-safety, and architectural separation.

**Role in the compiler pipeline:**
```
Parser (AST) → Semantic Analysis → ValidationPipeline → RoslynEmitter
                      ↓                      ↓              ↓
                 ISymbolLookup          ISymbolLookup   ISymbolLookup
                      ↓
                  SymbolTable (underlying storage)
```

The interface acts as a **facade** over the more complex `SymbolTable` class, exposing only lookup operations (no mutations like `Define`, `EnterScope`, `ExitScope`). This provides:
- **Separation of concerns**: Consumers only get read access unless they explicitly need write access
- **Testability**: Easier to mock for unit tests
- **Thread-safety**: Read-only operations are inherently safer for future parallel compilation

---

## Architecture Context

### The Services Layer Pattern

`ISymbolLookup` is one of several service interfaces in the new compiler services architecture:

```
CompilerServices (container)
├── ISymbolLookup        → Symbol lookups (this file)
├── ITypeResolver        → Type annotation resolution
├── IClrTypeMapper       → .NET type mapping
└── IDiagnosticReporter  → Error/warning reporting
```

All services are accessed through the `CompilerServices` container, which is built using `CompilerServicesBuilder`. This pattern supports:
- **LSP (Language Server Protocol)**: Different implementations for different contexts
- **Incremental compilation**: Cacheable service implementations
- **Parallel compilation**: Thread-safe service design

---

## Interface Definition

### Method Signatures

```csharp
public interface ISymbolLookup
{
    Symbol? Lookup(string name);
    TypeSymbol? LookupType(string name);
    TypeAliasSymbol? LookupTypeAlias(string name);
    FunctionSymbol? LookupFunction(string name);
    bool ExistsInCurrentScope(string name);
}
```

---

## Key Methods

### 1. `Lookup(string name)` - General Symbol Lookup

**Purpose**: Look up any symbol by name, searching the current scope and all parent scopes.

**Parameters:**
- `name`: The identifier to search for (e.g., `"x"`, `"MyClass"`, `"print"`)

**Returns:**
- `Symbol?`: The found symbol, or `null` if not found

**Scope Chain Traversal:**
```
Current Scope (e.g., function body)
    ↓ not found, search parent
Enclosing Scope (e.g., class body)
    ↓ not found, search parent
Module Scope
    ↓ not found, search parent
Global Scope (contains builtins like int, str, print, len)
    ↓ not found
Returns null
```

**Implementation Detail** (from `SymbolLookupAdapter`):
```csharp
public Symbol? Lookup(string name)
{
    return _symbolTable.Lookup(name);  // Delegates to SymbolTable
}
```

The underlying `SymbolTable.Lookup` walks the scope stack from innermost to outermost, returning the first match (shadowing inner definitions).

**Use Cases:**
- Checking if a variable is defined before use
- Resolving identifiers in expressions
- Import resolution

---

### 2. `LookupType(string name)` - Type-Specific Lookup

**Purpose**: Look up a type symbol (class, struct, interface, enum) by name.

**Parameters:**
- `name`: The type name (e.g., `"int"`, `"MyClass"`, `"List"`)

**Returns:**
- `TypeSymbol?`: The type symbol if found and is a type, otherwise `null`

**Implementation Detail:**
```csharp
public TypeSymbol? LookupType(string name)
{
    return _symbolTable.LookupType(name);  // Returns Lookup(name) as TypeSymbol
}
```

This is a **typed convenience method** that:
1. Calls `Lookup(name)`
2. Attempts to cast the result to `TypeSymbol`
3. Returns `null` if the symbol exists but isn't a type

**Example:**
```csharp
// In Sharpy code: class Dog: ...
var dogType = symbolLookup.LookupType("Dog");
if (dogType != null)
{
    // Access type-specific properties
    var baseType = dogType.BaseType;      // Inheritance chain
    var methods = dogType.Methods;        // All methods
    var fields = dogType.Fields;          // All fields
}
```

**Important Symbol Properties:**

From `Symbol.cs:82`, `TypeSymbol` contains:
- `TypeKind`: Class, Struct, Interface, or Enum
- `ClrType`: The .NET `Type` for interop (if applicable)
- `BaseType`: Parent class (for inheritance)
- `Interfaces`: Implemented interfaces
- `Fields`, `Methods`, `Properties`: Type members
- `Constructors`: All `__init__` overloads
- `OperatorMethods`: Dunder methods like `__add__`, `__eq__`
- `ProtocolMethods`: Protocol dunders like `__len__`, `__str__`
- `TypeParameters`: Generic type parameters (e.g., `T` in `class Box[T]`)

---

### 3. `LookupTypeAlias(string name)` - Type Alias Lookup

**Purpose**: Look up a type alias symbol (compile-time type aliases).

**Parameters:**
- `name`: The alias name (e.g., `"Vec3"` for `type Vec3 = tuple[float, float, float]`)

**Returns:**
- `TypeAliasSymbol?`: The alias symbol if found, otherwise `null`

**Context:**
Type aliases in Sharpy are **compile-time only** - they don't generate any C# code. They exist purely for developer convenience and documentation.

```python
# Sharpy code
type UserId = int
type Callback = (int, str) -> bool

def process_user(uid: UserId) -> None:
    pass
```

During code generation, `UserId` is replaced with `int` at the type annotation level.

**Implementation:**
```csharp
public TypeAliasSymbol? LookupTypeAlias(string name)
{
    return _symbolTable.LookupTypeAlias(name);
}
```

---

### 4. `LookupFunction(string name)` - Function-Specific Lookup

**Purpose**: Look up a function symbol by name.

**Parameters:**
- `name`: The function name (e.g., `"calculate_total"`, `"print"`, `"len"`)

**Returns:**
- `FunctionSymbol?`: The function symbol if found, otherwise `null`

**Implementation:**
```csharp
public FunctionSymbol? LookupFunction(string name)
{
    return _symbolTable.Lookup(name) as FunctionSymbol;
}
```

**Important:** This returns **one function symbol**, but that function may have overloads. For full overload resolution, use `BuiltinRegistry.GetFunctionOverloads()` for builtin functions.

**Function Symbol Properties** (from `Symbol.cs:62`):
- `Parameters`: List of `ParameterSymbol` with types
- `ReturnType`: The resolved return type (`SemanticType`)
- `IsStatic`, `IsAbstract`, `IsVirtual`, `IsOverride`: Method modifiers
- `TypeParameters`: Generic type parameters
- `ClrMethod`: .NET `MethodInfo` for interop

**Example Usage:**
```csharp
var printFunc = symbolLookup.LookupFunction("print");
if (printFunc != null)
{
    // Check signature
    var paramCount = printFunc.Parameters.Count;
    var returnType = printFunc.ReturnType;  // SemanticType.Void for print
}
```

---

### 5. `ExistsInCurrentScope(string name)` - Scope-Local Check

**Purpose**: Check if a symbol exists **only in the current scope**, ignoring parent scopes.

**Parameters:**
- `name`: The identifier to check

**Returns:**
- `bool`: `true` if the symbol is defined in the current scope, `false` otherwise

**Implementation Detail:**
```csharp
public bool ExistsInCurrentScope(string name)
{
    return _symbolTable.Lookup(name, searchParents: false) != null;
}
```

**Use Cases:**
- **Redefinition checks**: Prevent duplicate definitions in the same scope
- **Shadowing detection**: Warn when a local variable shadows an outer variable
- **Import conflict detection**: Detect when an import would collide with an existing symbol

**Example:**
```python
# Sharpy code
x = 10           # Defines x in module scope

def foo():
    if symbolLookup.ExistsInCurrentScope("x"):  # false (not in function scope)
        # ...
    x = 20       # Defines x in function scope
    if symbolLookup.ExistsInCurrentScope("x"):  # true (now in function scope)
        # ...
```

**Contrast with `Lookup`:**
- `Lookup("x")` would find the module-level `x` from inside `foo()`
- `ExistsInCurrentScope("x")` returns `false` until `x` is defined in the function scope

---

## Implementation: SymbolLookupAdapter

The interface is implemented by `SymbolLookupAdapter.cs:9`, which wraps the existing `SymbolTable`:

```csharp
public class SymbolLookupAdapter : ISymbolLookup
{
    private readonly SymbolTable _symbolTable;

    public SymbolLookupAdapter(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
    }

    public Symbol? Lookup(string name)
        => _symbolTable.Lookup(name);

    public TypeSymbol? LookupType(string name)
        => _symbolTable.LookupType(name);

    public TypeAliasSymbol? LookupTypeAlias(string name)
        => _symbolTable.LookupTypeAlias(name);

    public FunctionSymbol? LookupFunction(string name)
        => _symbolTable.Lookup(name) as FunctionSymbol;

    public bool ExistsInCurrentScope(string name)
        => _symbolTable.Lookup(name, searchParents: false) != null;

    // Escape hatch for legacy code during migration
    public SymbolTable UnderlyingTable => _symbolTable;
}
```

### The Adapter Pattern

This is a **textbook adapter pattern**:
- **Target Interface**: `ISymbolLookup` (what new code expects)
- **Adaptee**: `SymbolTable` (existing implementation)
- **Adapter**: `SymbolLookupAdapter` (bridges the two)

**Benefits:**
- New code uses clean, read-only interface
- Old code continues using `SymbolTable` directly
- Gradual migration path via `UnderlyingTable` escape hatch

---

## Dependencies

### Internal Dependencies

**Direct dependencies:**
- `Sharpy.Compiler.Semantic.Symbol` and all subclasses
  - `Symbol` (base class)
  - `TypeSymbol`, `FunctionSymbol`, `VariableSymbol`, `TypeAliasSymbol`, `ModuleSymbol`, etc.

**Upstream (what creates/populates symbols):**
- `SymbolTable.cs:6` - The underlying symbol storage with scope management
- `Scope.cs:6` - Individual scope implementation (dictionary-based)
- `NameResolver.cs` - Populates symbol table during first semantic pass
- `TypeChecker.cs` - Updates symbol types during type checking
- `BuiltinRegistry.cs` - Provides builtin types and functions

**Downstream (what consumes this interface):**
- `TypeChecker` - Looks up identifiers during type checking
- `ValidationPipeline` - Validates symbol usage
- `RoslynEmitter` - Resolves symbols during C# code generation
- `AccessValidator` - Checks access modifiers
- `ImportResolver` - Resolves cross-module references

---

## Patterns and Design Decisions

### 1. **Read-Only Interface Pattern**

**Decision**: Separate read operations into an interface, keep write operations in `SymbolTable`.

**Rationale:**
- Most code only needs to **look up** symbols, not modify the symbol table
- Read-only access prevents accidental mutations
- Easier to reason about code that can't modify state

**Trade-off:** Code that needs both read and write access must use `SymbolTable` directly or access `UnderlyingTable`.

---

### 2. **Nullable Return Types**

**Decision**: All lookup methods return nullable types (`Symbol?`, `TypeSymbol?`, etc.).

**Rationale:**
- C# 9.0 nullable reference types provide compile-time safety
- Forces consumers to handle "not found" cases explicitly
- Avoids exceptions for normal "symbol not found" scenarios

**Pattern:**
```csharp
var symbol = symbolLookup.Lookup("x");
if (symbol == null)
{
    // Handle not found
    services.ReportError($"Undefined identifier 'x'", node);
    return SemanticType.Unknown;
}
// Use symbol safely
```

---

### 3. **Typed Convenience Methods**

**Decision**: Provide `LookupType`, `LookupFunction`, `LookupTypeAlias` instead of just `Lookup`.

**Rationale:**
- **Type safety**: Return type is already correct, no casting needed
- **Intent clarity**: Code that calls `LookupType` clearly expects a type
- **Null vs wrong type**: Both return `null`, but semantics differ

**Example of intent clarity:**
```csharp
// Clear intent: looking for a type
var classSymbol = symbolLookup.LookupType("Dog");

// Less clear: what kind of symbol?
var symbol = symbolLookup.Lookup("Dog");
if (symbol is TypeSymbol classSymbol) { ... }
```

---

### 4. **Scope Chain Abstraction**

**Decision**: Hide scope stack details behind simple method calls.

**Rationale:**
- Consumers don't need to know about `Scope` objects or the scope stack
- Simplifies mental model: "lookup a name" vs "traverse scope chain"
- Allows future optimization (caching, parallel lookup) without API changes

**What's hidden:**
- The `Stack<Scope>` in `SymbolTable`
- The `Scope` class with its `Dictionary<string, Symbol>`
- Parent scope traversal logic

---

## Debugging Tips

### 1. **Symbol Not Found Issues**

If a symbol lookup returns `null` unexpectedly:

1. **Check scope depth**: Is the lookup happening in the right scope?
   ```csharp
   // Add temporary logging
   Logger.LogDebug($"Scope depth: {symbolTable.ScopeDepth}");
   Logger.LogDebug($"Current scope: {symbolTable.CurrentScope.Name}");
   ```

2. **Check symbol definition timing**: Was the symbol defined before the lookup?
   - `NameResolver` runs before `TypeChecker`
   - Variables are defined when their assignment is processed
   - Imports are defined when `ImportResolver` processes them

3. **Check spelling and case sensitivity**: Symbol names are case-sensitive
   ```python
   myVar = 10
   x = myvar  # Error: 'myvar' not found (should be 'myVar')
   ```

4. **Check for builtin shadowing**: Did user code shadow a builtin?
   ```python
   print = 42  # Shadows builtin print function
   print("hello")  # Error: 'print' is an int, not callable
   ```

---

### 2. **Wrong Symbol Type Issues**

If `LookupType` returns `null` but `Lookup` finds the symbol:

1. **Verify the symbol is actually a type:**
   ```csharp
   var symbol = symbolLookup.Lookup("MyClass");
   Logger.LogDebug($"Symbol kind: {symbol?.Kind}");  // Should be SymbolKind.Type
   ```

2. **Check for name conflicts:**
   ```python
   # Sharpy code
   class Dog: pass
   Dog = "hello"  # Variable shadows type!

   # Now LookupType("Dog") returns null (it's a VariableSymbol, not TypeSymbol)
   ```

---

### 3. **Scope-Related Issues**

If `ExistsInCurrentScope` behaves unexpectedly:

1. **Verify scope entry/exit balance:**
   ```csharp
   // Every EnterScope must have a matching ExitScope
   symbolTable.EnterScope("function");
   try
   {
       // ... processing ...
   }
   finally
   {
       symbolTable.ExitScope();  // Always exit, even on errors
   }
   ```

2. **Check for premature scope exits:**
   - Exiting a scope removes all its symbols
   - Lookups after exit won't find symbols defined in that scope

---

### 4. **Debugging with Underlying SymbolTable**

For deep debugging, access the underlying table:

```csharp
var adapter = (SymbolLookupAdapter)services.SymbolLookup;
var symbolTable = adapter.UnderlyingTable;

// Inspect all symbols in current scope
foreach (var symbol in symbolTable.CurrentScope.GetAllSymbols())
{
    Logger.LogDebug($"Symbol: {symbol.Name} ({symbol.Kind})");
}

// Check global scope for builtins
foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols())
{
    Logger.LogDebug($"Global: {symbol.Name}");
}
```

---

### 5. **Null Reference Warnings**

If you see nullable warnings when using lookup results:

```csharp
// Warning: Dereference of a possibly null reference
var symbol = symbolLookup.Lookup("x");
Console.WriteLine(symbol.Name);  // ⚠️ symbol might be null

// Fix: Check for null first
if (symbol != null)
{
    Console.WriteLine(symbol.Name);  // ✓ Safe
}

// Or use null-conditional operator
Console.WriteLine(symbol?.Name ?? "not found");
```

---

## Contribution Guidelines

### When to Modify This File

**Rarely.** This interface is intentionally minimal and stable. Modify it only for:

1. **New lookup operations** that are universally needed:
   ```csharp
   // Example: Adding module lookup
   ModuleSymbol? LookupModule(string name);
   ```

2. **Scope-related queries** that abstract implementation details:
   ```csharp
   // Example: Check if in global scope
   bool IsGlobalScope();
   ```

**Do NOT add:**
- Write operations (those belong in `SymbolTable`)
- Specific use-case methods (create extension methods instead)
- Methods that leak implementation details

---

### When to Create Extension Methods Instead

For specialized lookup operations, use extension methods:

```csharp
// In a separate static class
public static class SymbolLookupExtensions
{
    public static bool IsBuiltin(this ISymbolLookup lookup, string name)
    {
        var symbol = lookup.Lookup(name);
        return symbol?.DeclarationLine == null;  // Builtins have no source location
    }

    public static IEnumerable<FunctionSymbol> GetAllFunctionsInScope(
        this ISymbolLookup lookup,
        SymbolTable table)
    {
        return table.CurrentScope.GetAllSymbols()
            .OfType<FunctionSymbol>();
    }
}
```

---

### Testing Changes

If you modify the interface:

1. **Update `SymbolLookupAdapter`** to implement new methods
2. **Add unit tests** in `Sharpy.Compiler.Tests/Services/`
3. **Test with mock implementations** to ensure testability
4. **Run full test suite**: `dotnet test`

Example test structure:
```csharp
[Fact]
public void LookupType_ReturnsTypeSymbol_WhenTypeExists()
{
    // Arrange
    var symbolTable = new SymbolTable(new BuiltinRegistry());
    var adapter = new SymbolLookupAdapter(symbolTable);
    symbolTable.Define(new TypeSymbol { Name = "Dog", Kind = SymbolKind.Type });

    // Act
    var result = adapter.LookupType("Dog");

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Dog", result.Name);
}
```

---

### Documentation Updates

If you add methods, update:
1. **This walkthrough**: Add the new method to "Key Methods" section
2. **Services README**: Update `src/Sharpy.Compiler/Services/README.md`
3. **XML documentation**: Add `<summary>`, `<param>`, `<returns>` tags
4. **Architecture docs**: Update `.github/copilot-instructions.md` if architecture changes

---

## Cross-References

### Related Service Interfaces
- `ITypeResolver.cs` - Type annotation resolution service
- `IClrTypeMapper.cs` - .NET type mapping service
- `IDiagnosticReporter.cs` - Error/warning reporting service

### Related Documentation
- [CompilerServices.md](./CompilerServices.md) - Service container overview
- [CompilerServicesBuilder.md](./CompilerServicesBuilder.md) - Builder pattern for services
- [SymbolLookupAdapter.md](./SymbolLookupAdapter.md) - Adapter implementation details

### Related Source Files
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs` - Underlying symbol storage
- `src/Sharpy.Compiler/Semantic/Symbol.cs` - Symbol type definitions
- `src/Sharpy.Compiler/Semantic/Scope.cs` - Scope implementation
- `src/Sharpy.Compiler/Services/SymbolLookupAdapter.cs` - Interface implementation

### Related Specifications
- `docs/language_specification/identifiers.md` - Identifier rules and scoping
- `docs/language_specification/variable_scoping.md` - Variable scoping rules

---

## Summary

`ISymbolLookup` is a **read-only service interface** that provides clean, safe access to symbol table lookups. It:

- **Abstracts** the complexity of scope chain traversal
- **Separates** read operations from write operations
- **Supports** testability through interface-based design
- **Enables** future enhancements (caching, parallelism, LSP)

For most compilation tasks, use this interface instead of accessing `SymbolTable` directly. Only reach for `SymbolTable` when you need write operations like `Define`, `EnterScope`, or `ExitScope`.
