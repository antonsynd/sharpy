# Walkthrough: SymbolLookupAdapter.cs

**Source File**: `src/Sharpy.Compiler/Services/SymbolLookupAdapter.cs`

---

## Overview

`SymbolLookupAdapter` is a lightweight **Adapter Pattern** implementation that wraps the `SymbolTable` class to provide a read-only, interface-based view of symbol lookup operations. It sits in the **Services** layer of the Sharpy compiler architecture, enabling cleaner separation of concerns and facilitating future features like LSP support, parallel compilation, and incremental builds.

**Role in Compiler Pipeline**: This adapter operates during the **Semantic Analysis** phase, providing a simplified API for components that need to look up symbols (variables, functions, types, type aliases) without needing full write access to the symbol table.

**Key Insight**: This is a *facade* that restricts access to symbol table operations—you can look things up, but you can't modify the table through this interface. This prevents accidental mutations and makes code dependencies more explicit.

---

## Class Structure

### Main Class: `SymbolLookupAdapter`

```csharp
public class SymbolLookupAdapter : ISymbolLookup
{
    private readonly SymbolTable _symbolTable;
    // ... methods
}
```

**Implements**: `ISymbolLookup` interface (defined in `Services/ISymbolLookup.cs`)

**Dependencies**:
- `Sharpy.Compiler.Semantic.SymbolTable` - The underlying symbol table being wrapped
- `Sharpy.Compiler.Semantic.Symbol` and its subtypes (`TypeSymbol`, `FunctionSymbol`, `TypeAliasSymbol`)

---

## Key Methods

### Constructor

```csharp
public SymbolLookupAdapter(SymbolTable symbolTable)
{
    _symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
}
```

**Purpose**: Initializes the adapter with a `SymbolTable` instance.

**Key Details**:
- Performs null validation—compilation cannot proceed without a symbol table
- Stores a reference (not a copy), so lookups reflect the current state of the symbol table
- This is an **immutable wrapper**: once constructed, the wrapped table cannot be changed

---

### `Lookup(string name)` → `Symbol?`

```csharp
public Symbol? Lookup(string name)
{
    return _symbolTable.Lookup(name);
}
```

**Purpose**: General-purpose symbol lookup that searches the current scope and all parent scopes.

**How It Works**:
- Delegates to `SymbolTable.Lookup(name)`, which walks the scope stack from innermost to outermost
- Returns the first symbol with a matching name
- Returns `null` if no symbol is found

**Use Cases**:
- Resolving variable references in expressions
- Looking up identifiers when the symbol type is unknown
- General name resolution during semantic analysis

**Related Spec**: See `docs/language_specification/variable_scoping.md` for scoping rules

---

### `LookupType(string name)` → `TypeSymbol?`

```csharp
public TypeSymbol? LookupType(string name)
{
    return _symbolTable.LookupType(name);
}
```

**Purpose**: Specialized lookup for type symbols (classes, structs, protocols, etc.).

**How It Works**:
- Delegates to `SymbolTable.LookupType`, which internally calls `Lookup(name) as TypeSymbol`
- Returns `null` if the symbol exists but isn't a `TypeSymbol`, or if it doesn't exist at all

**Use Cases**:
- Resolving type annotations (e.g., `x: int`, `def foo() -> List[str]`)
- Checking if a name refers to a type during semantic analysis
- Type-checking inheritance hierarchies

**Example**:
```python
class Point:
    x: int  # TypeResolver needs to lookup "int" as a TypeSymbol
```

---

### `LookupTypeAlias(string name)` → `TypeAliasSymbol?`

```csharp
public TypeAliasSymbol? LookupTypeAlias(string name)
{
    return _symbolTable.LookupTypeAlias(name);
}
```

**Purpose**: Lookup for type aliases defined with the `type` keyword.

**How It Works**:
- Delegates to `SymbolTable.LookupTypeAlias`, which casts to `TypeAliasSymbol`
- Returns `null` if the name doesn't refer to a type alias

**Use Cases**:
- Resolving type alias references during type resolution
- Distinguishing between concrete types and aliases

**Example**:
```python
type Vector = List[float]
v: Vector  # Needs to resolve "Vector" as a TypeAliasSymbol
```

---

### `LookupFunction(string name)` → `FunctionSymbol?`

```csharp
public FunctionSymbol? LookupFunction(string name)
{
    return _symbolTable.Lookup(name) as FunctionSymbol;
}
```

**Purpose**: Specialized lookup for function symbols.

**How It Works**:
- Calls the general `Lookup` and attempts a cast to `FunctionSymbol`
- Returns `null` if the symbol exists but isn't a function, or doesn't exist

**Use Cases**:
- Resolving function calls during type checking
- Checking if a name refers to a callable
- Looking up function signatures for overload resolution

**Important Note**: This returns *one* `FunctionSymbol`, but Sharpy supports function overloading. For full overload resolution, TypeChecker uses `BuiltinRegistry.GetFunctionOverloads()` instead. This method is primarily for simple cases or initial lookups.

---

### `ExistsInCurrentScope(string name)` → `bool`

```csharp
public bool ExistsInCurrentScope(string name)
{
    return _symbolTable.Lookup(name, searchParents: false) != null;
}
```

**Purpose**: Check if a symbol exists in the *current* scope only (not parent scopes).

**How It Works**:
- Calls `SymbolTable.Lookup` with `searchParents: false`
- Returns `true` if a symbol with that name exists in the innermost scope
- Returns `false` otherwise

**Use Cases**:
- Detecting duplicate definitions in the same scope
- Checking for variable shadowing
- Validation during symbol definition

**Example**:
```python
def foo():
    x = 1
    x = 2  # Validator checks if "x" ExistsInCurrentScope before reassignment
```

**Related Spec**: See `docs/language_specification/identifiers.md` for identifier rules

---

### `UnderlyingTable` Property

```csharp
public SymbolTable UnderlyingTable => _symbolTable;
```

**Purpose**: Escape hatch providing direct access to the wrapped `SymbolTable`.

**When to Use**:
- During migration from old code that needs `SymbolTable` directly
- When you need write operations (e.g., `Define`, `EnterScope`, `ExitScope`)
- For components that haven't been fully migrated to the services layer

**Important**: The documentation says "Use sparingly - prefer the interface methods." This is because:
1. Breaking the abstraction makes code harder to test
2. It couples components to `SymbolTable` implementation details
3. It bypasses the read-only guarantee of the interface

**Ideal Usage**: Access this property only in transitional code. New code should use the interface methods or request write operations through a different service.

---

## Dependencies and Relationships

### Upstream (What it depends on)
- **`SymbolTable`** (`Semantic/SymbolTable.cs`): The core symbol table implementation
  - Manages scope stack (global scope + nested function/block scopes)
  - Populates builtin types and functions from `BuiltinRegistry`
  - Provides scope management (`EnterScope`, `ExitScope`)

### Downstream (What depends on it)
- **`CompilerServices`** (`Services/CompilerServices.cs`): The central service container
  - Exposes `ISymbolLookup` as the `SymbolLookup` property
  - Provides convenience method `LookupSymbol(name)` that delegates to the adapter
  - Used throughout semantic analysis and validation

- **`TypeChecker`**: Uses symbol lookup for resolving variable references
- **`ValidationPipeline`**: Validators use symbol lookup to check if identifiers are defined
- **Code generation**: May use symbol lookup to resolve references when emitting C#

### Related Files
- **`ISymbolLookup.cs`**: The interface definition (5 methods)
- **`CompilerServicesBuilder.cs`**: Constructs the adapter and wires it into services
- **`SymbolTable.cs`**: The actual implementation being wrapped

---

## Patterns and Design Decisions

### 1. **Adapter Pattern**
The class is a textbook example of the Adapter Pattern:
- **Adaptee**: `SymbolTable` (existing class with a rich API)
- **Target Interface**: `ISymbolLookup` (simplified, read-only API)
- **Adapter**: `SymbolLookupAdapter` (translates interface calls to adaptee calls)

**Why This Pattern?**
- Decouples consumers from `SymbolTable` implementation details
- Allows swapping implementations (e.g., LSP-specific symbol lookup)
- Makes testing easier (can mock `ISymbolLookup`)

### 2. **Read-Only Facade**
The interface deliberately excludes write operations like:
- `Define(Symbol)` - adding symbols
- `EnterScope(string)` / `ExitScope()` - scope management
- `UpdateSymbol(Symbol)` - modifying symbols

**Rationale**: Components that only need to *read* symbols shouldn't have the power to *modify* them. This prevents bugs where a lookup accidentally mutates state.

### 3. **Delegation Over Inheritance**
The adapter uses **composition** (wrapping a `SymbolTable`) rather than **inheritance** (extending `SymbolTable`).

**Benefits**:
- Can't accidentally expose `SymbolTable`'s public methods
- More flexible—can wrap different implementations
- Follows the "favor composition over inheritance" principle

### 4. **Null Safety**
All methods return nullable types (`Symbol?`, `TypeSymbol?`, etc.), making it explicit that lookups can fail.

**Impact on Callers**: Code using this adapter must handle `null` results:
```csharp
var symbol = symbolLookup.Lookup("x");
if (symbol == null)
{
    services.ReportError($"Undefined variable: x", node);
    return SemanticType.Unknown;
}
```

---

## Debugging Tips

### When Symbol Lookups Fail

If a lookup returns `null` unexpectedly:

1. **Check if the symbol was defined**:
   - Set a breakpoint in `SymbolTable.Define` to see if the symbol is being added
   - Inspect `_symbolTable.CurrentScope` in the debugger

2. **Check scope depth**:
   - Verify `_symbolTable.ScopeDepth` is correct
   - A common bug is calling `ExitScope` too many times, losing symbols

3. **Check search behavior**:
   - `Lookup(name)` searches parent scopes (default behavior)
   - `ExistsInCurrentScope(name)` only checks the innermost scope
   - Make sure you're using the right method for your use case

4. **Check for typos in symbol names**:
   - Symbol lookup is case-sensitive
   - Use the debugger to inspect `CurrentScope._symbols.Keys`

### Inspecting the Symbol Table

To see all symbols in the current scope:
```csharp
var adapter = (SymbolLookupAdapter)services.SymbolLookup;
var symbolTable = adapter.UnderlyingTable;
var currentScope = symbolTable.CurrentScope;
// Inspect currentScope._symbols in debugger
```

### Tracing Lookup Calls

Add logging in `SymbolTable.Lookup` to trace all lookups:
```csharp
public Symbol? Lookup(string name, bool searchParents = true)
{
    Console.WriteLine($"[SymbolTable] Looking up '{name}' in scope '{CurrentScope.Name}'");
    var result = CurrentScope.Lookup(name, searchParents);
    Console.WriteLine($"[SymbolTable] Result: {result?.GetType().Name ?? "null"}");
    return result;
}
```

---

## Contribution Guidelines

### When to Modify This File

This file is **stable infrastructure**—changes should be rare. Modify it only when:

1. **Adding new lookup methods** to the `ISymbolLookup` interface
   - Example: Adding `LookupProtocol(string name)` for protocol symbols
   - Update the interface first, then implement here

2. **Changing delegation behavior**
   - If `SymbolTable`'s lookup API changes, update the delegation here
   - Ensure backward compatibility with existing callers

3. **Improving null safety or error handling**
   - Add validation if needed (though most validation should be in `SymbolTable`)

### When NOT to Modify This File

**Don't add write operations** to this adapter. If you need to:
- Define symbols → Use `CompilerServices.SymbolTable.Define()` directly
- Manage scopes → Use `SymbolTable.EnterScope()` / `ExitScope()` directly
- Update symbols → Use `SymbolTable.UpdateSymbol()` directly

The whole point of this adapter is to provide a *read-only* view. Adding write operations defeats the purpose.

### Testing Changes

If you modify this file:

1. **Unit tests**: Add tests in `Sharpy.Compiler.Tests/Services/SymbolLookupAdapterTests.cs` (if it exists)
2. **Integration tests**: Run the full semantic analysis test suite to ensure lookups still work
3. **Null safety**: Verify that lookups correctly return `null` for undefined symbols

### Code Style

- **Minimal logic**: This is an adapter—don't add complex logic here
- **Pure delegation**: Methods should just forward to `_symbolTable`
- **Explicit nullability**: Use `?` annotations for nullable returns
- **XML comments**: Add `<summary>` tags if adding new methods

---

## Cross-References

### Related Documentation
- [ISymbolLookup Interface Documentation](./ISymbolLookup.md) *(if it exists)*
- [CompilerServices Walkthrough](./CompilerServices.md) *(if it exists)*
- [SymbolTable Walkthrough](../Semantic/SymbolTable.md) *(if it exists)*

### Related Source Files
- **Interface**: `src/Sharpy.Compiler/Services/ISymbolLookup.cs` - Defines the contract
- **Wrapped Class**: `src/Sharpy.Compiler/Semantic/SymbolTable.cs` - The adaptee
- **Service Container**: `src/Sharpy.Compiler/Services/CompilerServices.cs` - Uses this adapter
- **Builder**: `src/Sharpy.Compiler/Services/CompilerServicesBuilder.cs` - Constructs instances

### Related Specifications
- `docs/language_specification/identifiers.md` - Identifier naming and scoping rules
- `docs/language_specification/variable_scoping.md` - How scopes work in Sharpy

### Services Layer Overview
See `src/Sharpy.Compiler/Services/README.md` for the full architecture of the services layer and how this adapter fits into the migration strategy.

---

## Summary

`SymbolLookupAdapter` is a simple but critical piece of infrastructure that:
- ✅ Provides a clean, read-only API for symbol lookups
- ✅ Decouples consumers from `SymbolTable` implementation details
- ✅ Supports future features (LSP, parallel compilation)
- ✅ Follows the Adapter Pattern and Single Responsibility Principle

**For newcomers**: Think of this as a "restricted view" of the symbol table—you can ask questions ("Does symbol X exist?") but you can't make changes. If you need to modify the symbol table, use `CompilerServices.SymbolTable` directly.
