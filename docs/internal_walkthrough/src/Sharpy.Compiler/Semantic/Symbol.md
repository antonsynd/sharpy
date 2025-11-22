# Walkthrough: Symbol.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Symbol.cs`

---

## Overview

`Symbol.cs` defines the **symbol representation system** for the Sharpy compiler's semantic analysis phase. Think of symbols as the compiler's "database entries" for everything that can be named in a Sharpy program: variables, functions, types (classes/structs/interfaces), modules, and more.

**Key Role**: This file provides the data structures that store metadata about program entities after parsing but before code generation. Symbols bridge the gap between the Abstract Syntax Tree (AST) and type-checked, semantically-validated code.

**Why This Matters**: 
- Enables type checking by associating types with variables and functions
- Supports name resolution (finding what a name refers to)
- Facilitates .NET interop by linking to CLR types and methods
- Powers IDE features like autocomplete and "go to definition" (future)

---

## Class/Type Structure

The file uses C# **records** extensively, which provide immutable data structures with value-based equality—perfect for representing compiler metadata that shouldn't change once created.

### Hierarchy Overview

```
Symbol (abstract base)
├── VariableSymbol - Variables, fields, constants
├── FunctionSymbol - Functions and methods
├── TypeSymbol - Classes, structs, interfaces, enums
└── ModuleSymbol - Sharpy modules/files

ParameterSymbol - Function parameters (standalone)
PropertySymbol - Properties (future feature)
```

### Base Class: `Symbol`

```csharp
public abstract record Symbol
{
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Public;
    public int? DeclarationLine { get; init; }
    public int? DeclarationColumn { get; init; }
}
```

**What It Represents**: The base class for all symbols in the compiler.

**Key Fields**:
- **`Name`**: The identifier as written in source code (e.g., `my_variable`, `MyClass`)
- **`Kind`**: Discriminator enum to quickly identify symbol type without casting
- **`AccessLevel`**: Visibility modifiers (`Public`, `Protected`, `Private`)
- **`DeclarationLine` / `DeclarationColumn`**: Source location for error reporting and debugging (nullable because built-in symbols like `int` or `print` aren't declared in user code)

**Design Decision**: Abstract record with `init` properties makes symbols immutable and forces creation through derived types.

---

## Key Symbol Types

### 1. `VariableSymbol`

```csharp
public record VariableSymbol : Symbol
{
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool IsParameter { get; init; }
    public bool IsConstant { get; init; }
    public bool HasDefaultValue { get; init; }
}
```

**Represents**: Variables, fields, and constants.

**Use Cases**:
- Local variables in functions: `x: int = 42`
- Class/struct fields: `self.name: str`
- Constants: `MAX_SIZE: int = 100` (when `IsConstant = true`)
- Function parameters (when `IsParameter = true`, though `ParameterSymbol` is more common for this)

**Key Field - `Type`**: 
- Links to `SemanticType` hierarchy (see `SemanticType.cs`)
- Starts as `SemanticType.Unknown` during parsing, resolved during type checking
- Example: `SemanticType.Int` for `x: int`, or `GenericType { Name = "list", TypeArguments = [SemanticType.Str] }` for `list[str]`

**Example Usage** (from `TypeChecker.cs`):
```csharp
var newSymbol = new VariableSymbol
{
    Name = variableName,
    Kind = SymbolKind.Variable,
    Type = resolvedType,
    AccessLevel = AccessLevel.Public,
    DeclarationLine = assignStmt.Line,
    DeclarationColumn = assignStmt.Column
};
_symbolTable.Define(newSymbol);
```

---

### 2. `FunctionSymbol`

```csharp
public record FunctionSymbol : Symbol
{
    public List<ParameterSymbol> Parameters { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Unknown;
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }
    
    // For .NET interop
    public System.Reflection.MethodInfo? ClrMethod { get; init; }
}
```

**Represents**: Functions, methods, and imported .NET methods.

**Key Fields**:
- **`Parameters`**: Ordered list of parameter symbols with their types and default values
- **`ReturnType`**: What the function returns (or `SemanticType.Void` for `-> None`)
- **Modifiers**: `IsStatic`, `IsAbstract`, `IsVirtual`, `IsOverride` - control method dispatch and inheritance
- **`ClrMethod`**: Critical for .NET interop! When importing from C# libraries (e.g., `System.Console.WriteLine`), this holds the reflection metadata

**Example - Sharpy Function**:
```python
def greet(name: str, excited: bool = False) -> str:
    return f"Hello, {name}!"
```

Maps to:
```csharp
new FunctionSymbol
{
    Name = "greet",
    Kind = SymbolKind.Function,
    Parameters = [
        new ParameterSymbol { Name = "name", Type = SemanticType.Str },
        new ParameterSymbol { Name = "excited", Type = SemanticType.Bool, HasDefault = true }
    ],
    ReturnType = SemanticType.Str,
    IsStatic = false
}
```

**Example - .NET Interop** (from `ImportResolver.cs`):
```csharp
var funcSymbol = new FunctionSymbol
{
    Name = methodInfo.Name,
    Kind = SymbolKind.Function,
    ClrMethod = methodInfo,  // Link to System.Reflection.MethodInfo
    ReturnType = MapClrType(methodInfo.ReturnType),
    // ... parameters mapped from CLR parameter metadata
};
```

---

### 3. `TypeSymbol`

```csharp
public record TypeSymbol : Symbol
{
    public TypeKind TypeKind { get; init; }
    public Type? ClrType { get; init; }
    
    // Generic type parameters
    public List<string> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;
    
    // Members
    public List<VariableSymbol> Fields { get; init; } = new();
    public List<FunctionSymbol> Methods { get; init; } = new();
    public List<PropertySymbol> Properties { get; init; } = new();
    
    // Inheritance
    public TypeSymbol? BaseType { get; set; }
    public List<TypeSymbol> Interfaces { get; init; } = new();
}
```

**Represents**: User-defined types (classes, structs, interfaces, enums) and imported .NET types.

**Key Fields**:
- **`TypeKind`**: Distinguishes between `Class`, `Struct`, `Interface`, `Enum`
- **`ClrType`**: For .NET types (e.g., `System.String`, `System.Collections.Generic.List<T>`)
- **`TypeParameters`**: Generic type parameter names (e.g., `["T"]` for `class Box[T]`)
- **`Fields`, `Methods`, `Properties`**: Member symbols for type checking member access
- **`BaseType`**: Single inheritance support (mutable via `set` for two-pass resolution)
- **`Interfaces`**: Multiple interface implementation

**Why `BaseType` is Mutable**:
This is the **only mutable field** in the symbol system! It's necessary because of circular dependencies in inheritance:

```python
class Base:
    pass

class Derived(Base):  # Need to resolve Base before setting Derived.BaseType
    pass
```

The compiler uses a two-pass approach:
1. **First pass**: Create `TypeSymbol` for each type declaration
2. **Second pass**: Resolve base types and set `BaseType`

**Example - Generic Class**:
```python
class Container[T]:
    value: T
    
    def get(self) -> T:
        return self.value
```

Maps to:
```csharp
new TypeSymbol
{
    Name = "Container",
    Kind = SymbolKind.Type,
    TypeKind = TypeKind.Class,
    TypeParameters = ["T"],
    Fields = [
        new VariableSymbol { Name = "value", Type = /* T as SemanticType */ }
    ],
    Methods = [
        new FunctionSymbol { Name = "get", ReturnType = /* T */ }
    ]
}
```

---

### 4. `ParameterSymbol`

```csharp
public record ParameterSymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasDefault { get; init; }
    public Expression? DefaultValue { get; init; }
}
```

**Represents**: Function/method parameters.

**Why Separate from Symbol?**: Parameters aren't top-level symbols in scopes—they're owned by functions. This keeps the symbol table cleaner.

**Key Field - `DefaultValue`**:
- Stores the AST `Expression` node for default values
- Example: For `def foo(x: int = 42)`, stores `IntLiteral(42)`
- Used during code generation to emit default parameter values

**Example**:
```python
def calculate(base: int, exponent: int = 2) -> int:
    return base ** exponent
```

Maps to:
```csharp
new FunctionSymbol
{
    Name = "calculate",
    Parameters = [
        new ParameterSymbol { Name = "base", Type = SemanticType.Int },
        new ParameterSymbol { 
            Name = "exponent", 
            Type = SemanticType.Int,
            HasDefault = true,
            DefaultValue = new IntLiteral { Value = 2 }
        }
    ]
}
```

---

### 5. `ModuleSymbol`

```csharp
public record ModuleSymbol : Symbol
{
    public string FilePath { get; init; } = string.Empty;
    public List<Symbol> Exports { get; init; } = new();
}
```

**Represents**: Sharpy modules (typically `.spy` files).

**Key Fields**:
- **`FilePath`**: Absolute path to the source file
- **`Exports`**: Symbols made available via `__all__` or public declarations

**Usage**: When you import a module:
```python
from mymodule import calculate
```

The compiler:
1. Finds `mymodule.spy` via `ModuleDiscovery`
2. Parses and analyzes it
3. Creates a `ModuleSymbol` with its exports
4. Adds `calculate` (from `Exports`) to the current scope

---

### 6. `PropertySymbol`

```csharp
public record PropertySymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
    public AccessLevel GetterAccess { get; init; } = AccessLevel.Public;
    public AccessLevel SetterAccess { get; init; } = AccessLevel.Public;
}
```

**Status**: **Future feature** - not currently used in the compiler.

**Intended Use**: Python-style `@property` decorators:
```python
class Person:
    @property
    def age(self) -> int:
        return self._age
    
    @age.setter
    def age(self, value: int) -> None:
        self._age = value
```

**Why Not Part of Symbol Hierarchy?**: Properties aren't standalone symbols—they're owned by types, similar to parameters.

---

## Enumerations

### `SymbolKind`

```csharp
public enum SymbolKind
{
    Variable,
    Parameter,
    Function,
    Type,
    Module,
    Property
}
```

**Purpose**: Fast type discrimination without casting.

**Usage Pattern**:
```csharp
Symbol symbol = _symbolTable.Lookup("x");
if (symbol?.Kind == SymbolKind.Variable)
{
    var variable = (VariableSymbol)symbol;
    // Work with variable-specific fields
}
```

**Alternative**: Could use pattern matching (`symbol is VariableSymbol`), but `Kind` is faster for quick checks.

---

### `TypeKind`

```csharp
public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum
}
```

**Purpose**: Distinguish user-defined type categories.

**Semantic Differences**:
- **Class**: Reference type, heap-allocated, supports inheritance
- **Struct**: Value type, stack-allocated (when possible), no inheritance
- **Interface**: Contract definition, supports multiple implementation
- **Enum**: Named integer constants

**Code Generation Impact**: Emits different C# constructs based on `TypeKind`.

---

### `AccessLevel`

```csharp
public enum AccessLevel
{
    Public,
    Protected,
    Private
}
```

**Purpose**: Visibility modifiers for symbols.

**Mapping**:
- **Public**: Accessible everywhere (default)
- **Protected**: Accessible in derived classes
- **Private**: Accessible only within the declaring type

**Validation**: `AccessValidator.cs` enforces these rules during semantic analysis.

---

## Dependencies

### Direct Dependencies (used by Symbol.cs)

1. **`Sharpy.Compiler.Parser.Ast`**: 
   - `ParameterSymbol.DefaultValue` references `Expression` AST nodes
   - Shows symbols bridge parsing and semantic analysis phases

2. **`SemanticType`** (same namespace):
   - All type-related symbols reference `SemanticType` hierarchy
   - Circular relationship: `TypeSymbol` has a `SemanticType`, and `UserDefinedType : SemanticType` has a `TypeSymbol`

### Dependent Components (use Symbol.cs)

1. **`SymbolTable.cs`**: 
   - Core data structure that stores symbols in scoped collections
   - Methods: `Define(Symbol)`, `Lookup(string) -> Symbol?`

2. **`NameResolver.cs`**:
   - First pass: Creates symbols from AST declarations
   - Populates symbol table with `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`

3. **`TypeChecker.cs`**:
   - Second pass: Resolves types and validates usage
   - Reads symbols from table, validates type compatibility

4. **`ImportResolver.cs`**:
   - Imports .NET assemblies and Sharpy modules
   - Creates symbols with `ClrType` and `ClrMethod` populated

5. **`BuiltinRegistry.cs`**:
   - Registers built-in types (`int`, `str`, `list[T]`) and functions (`print`, `len`)
   - Creates immutable symbols loaded at startup

6. **`CodeGen/RoslynEmitter.cs`**:
   - Reads symbols to generate C# code
   - Uses `ClrType` and `ClrMethod` for .NET interop

---

## Patterns and Design Decisions

### 1. **Immutability via Records**

**Pattern**: All symbols are C# records with `init`-only properties.

**Benefits**:
- **Thread-safety**: Immutable symbols can be shared across compiler passes
- **Value equality**: Records compare by value, useful for caching and deduplication
- **Clear intent**: Once created, a symbol's metadata shouldn't change (except `BaseType`)

**Example - Wrong Approach**:
```csharp
// ❌ Don't modify after creation
var symbol = new VariableSymbol { Name = "x", Type = SemanticType.Int };
symbol.Type = SemanticType.Str;  // Won't compile! init-only
```

**Example - Correct Approach**:
```csharp
// ✅ Create a new symbol with updated information
var updatedSymbol = symbol with { Type = SemanticType.Str };
```

---

### 2. **Separation of Concerns: Symbol vs. SemanticType**

**Why Two Hierarchies?**

- **Symbol**: Represents a *named entity* in the program (variables, functions, types)
- **SemanticType**: Represents a *type* (int, str, list[T], User-defined types)

**Example**:
```python
x: list[int] = [1, 2, 3]
```

- **Symbol**: `VariableSymbol { Name = "x", Type = ... }`
- **SemanticType**: `GenericType { Name = "list", TypeArguments = [SemanticType.Int] }`

**Relationship**:
- Symbols *have* types (`VariableSymbol.Type`, `FunctionSymbol.ReturnType`)
- Types *can reference* symbols (`UserDefinedType.Symbol -> TypeSymbol`)

---

### 3. **.NET Interop Design**

**Key Fields**:
- `TypeSymbol.ClrType`: Links to `System.Type` (e.g., `typeof(System.String)`)
- `FunctionSymbol.ClrMethod`: Links to `System.Reflection.MethodInfo`

**Why Important**: Allows seamless use of .NET libraries:

```python
# Sharpy code
import System
x: System.String = "Hello from .NET"
System.Console.WriteLine(x)
```

The compiler:
1. Reflects over `System.Console` type
2. Creates `FunctionSymbol { Name = "WriteLine", ClrMethod = Console.WriteLine MethodInfo }`
3. Code generator uses `ClrMethod` to emit correct C# invocation

**Trade-off**: Mixing compiler symbols with runtime reflection data, but necessary for interop.

---

### 4. **The `BaseType` Mutability Exception**

**Why Mutable?** Two-phase type resolution:

```python
# Phase 1: Create TypeSymbols (BaseType = null)
class Animal:
    pass

class Dog(Animal):  # "Animal" not resolved yet
    pass

# Phase 2: Resolve inheritance (set BaseType)
# Dog.BaseType = AnimalTypeSymbol
```

**Implementation** (from `NameResolver.cs`):
```csharp
// Phase 1
var dogSymbol = new TypeSymbol { Name = "Dog", BaseType = null };

// Phase 2 (after all types are declared)
var animalSymbol = _symbolTable.LookupType("Animal");
dogSymbol.BaseType = animalSymbol;  // Only mutable field!
```

**Alternative Considered**: Make everything immutable and use maps to track inheritance. Rejected for complexity.

---

### 5. **Default Initialization**

**Pattern**: All collection properties initialize to empty collections:
```csharp
public List<ParameterSymbol> Parameters { get; init; } = new();
```

**Why?**: Prevents null reference exceptions and simplifies usage:
```csharp
// Always safe, even if Parameters not set during creation
foreach (var param in functionSymbol.Parameters)
{
    // ...
}
```

**Alternative**: Nullable lists (`List<T>?`), but adds null-checking burden everywhere.

---

## Debugging Tips

### 1. **Symbol Not Found Errors**

**Symptom**: `NameError: 'x' is not defined`

**Debug Strategy**:
1. Check if symbol was added to symbol table:
   ```csharp
   var symbol = _symbolTable.Lookup("x");
   if (symbol == null)
       // Not added - check NameResolver
   ```

2. Check scope depth:
   ```csharp
   Console.WriteLine($"Looking for 'x' at scope depth {_symbolTable.ScopeDepth}");
   ```

3. Verify declaration line/column:
   ```csharp
   Console.WriteLine($"Symbol 'x' declared at {symbol.DeclarationLine}:{symbol.DeclarationColumn}");
   ```

---

### 2. **Type Mismatch Errors**

**Symptom**: `TypeError: Cannot assign str to int`

**Debug Strategy**:
1. Inspect symbol type:
   ```csharp
   var varSymbol = _symbolTable.LookupVariable("x") as VariableSymbol;
   Console.WriteLine($"Variable 'x' has type: {varSymbol?.Type.GetDisplayName()}");
   ```

2. Check if type was resolved:
   ```csharp
   if (varSymbol?.Type == SemanticType.Unknown)
       // Type resolution failed
   ```

---

### 3. **Method Not Found in .NET Type**

**Symptom**: `AttributeError: 'Console' has no attribute 'PrintLine'` (typo)

**Debug Strategy**:
1. Check `ClrType` reflection:
   ```csharp
   var typeSymbol = _symbolTable.LookupType("Console");
   if (typeSymbol?.ClrType != null)
   {
       var methods = typeSymbol.ClrType.GetMethods();
       Console.WriteLine($"Available methods: {string.Join(", ", methods.Select(m => m.Name))}");
   }
   ```

2. Verify `ClrMethod` is set:
   ```csharp
   var funcSymbol = /* lookup WriteLine */;
   if (funcSymbol?.ClrMethod == null)
       // Import resolution failed
   ```

---

### 4. **Inheritance Chain Issues**

**Symptom**: `TypeError: Cannot assign Dog to Animal` (should work!)

**Debug Strategy**:
1. Walk inheritance chain:
   ```csharp
   var dogSymbol = _symbolTable.LookupType("Dog");
   var current = dogSymbol;
   while (current != null)
   {
       Console.WriteLine($"Inheritance: {current.Name}");
       current = current.BaseType;
   }
   ```

2. Check if `BaseType` was set:
   ```csharp
   if (dogSymbol?.BaseType == null)
       // Phase 2 resolution didn't run or failed
   ```

---

### 5. **Useful Debugging Extensions**

Add these helper methods for debugging:

```csharp
public static class SymbolDebugExtensions
{
    public static string ToDebugString(this Symbol symbol)
    {
        return symbol switch
        {
            VariableSymbol v => $"var {v.Name}: {v.Type.GetDisplayName()}",
            FunctionSymbol f => $"func {f.Name}({string.Join(", ", f.Parameters.Select(p => $"{p.Name}: {p.Type.GetDisplayName()}"))}) -> {f.ReturnType.GetDisplayName()}",
            TypeSymbol t => $"type {t.Name} ({t.TypeKind})",
            ModuleSymbol m => $"module {m.Name} @ {m.FilePath}",
            _ => symbol.Name
        };
    }
}
```

Usage:
```csharp
Console.WriteLine(symbol.ToDebugString());
// Output: "func greet(name: str) -> str"
```

---

## Contribution Guidelines

### What Kinds of Changes Might You Make?

#### 1. **Adding New Symbol Types**

**When**: Introducing new language constructs (e.g., decorators, type aliases, protocol definitions)

**Example - Adding `TypeAliasSymbol`**:
```csharp
public record TypeAliasSymbol : Symbol
{
    public SemanticType AliasedType { get; init; } = SemanticType.Unknown;
}

// Update SymbolKind enum
public enum SymbolKind
{
    // ... existing ...
    TypeAlias
}
```

**Checklist**:
- [ ] Add new record type inheriting from `Symbol`
- [ ] Add corresponding `SymbolKind` enum value
- [ ] Update `NameResolver.cs` to create the symbol
- [ ] Update `TypeChecker.cs` to validate usage
- [ ] Add tests in `Sharpy.Compiler.Tests/Semantic/`

---

#### 2. **Extending Symbol Metadata**

**When**: Need additional information for optimization, error reporting, or IDE features

**Example - Adding Documentation Strings**:
```csharp
public abstract record Symbol
{
    // ... existing fields ...
    public string? DocString { get; init; }  // Add this
}
```

**Use Case**: Store Python-style docstrings:
```python
def greet(name: str) -> str:
    """Greet someone by name."""  # <- Store in DocString
    return f"Hello, {name}!"
```

**Checklist**:
- [ ] Add field to base `Symbol` or specific symbol type
- [ ] Update parser/name resolver to populate the field
- [ ] Update code generator if needed (e.g., emit XML doc comments)
- [ ] Add tests verifying the field is set correctly

---

#### 3. **Improving .NET Interop**

**When**: Need more sophisticated reflection or handling of .NET-specific features

**Example - Support for Extension Methods**:
```csharp
public record FunctionSymbol : Symbol
{
    // ... existing fields ...
    public bool IsExtensionMethod { get; init; }  // Add this
}
```

**Implementation**:
```csharp
// In ImportResolver.cs
var funcSymbol = new FunctionSymbol
{
    Name = methodInfo.Name,
    ClrMethod = methodInfo,
    IsExtensionMethod = methodInfo.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute))
};
```

---

#### 4. **Refactoring for Performance**

**Current Limitation**: Symbol lookup is O(n) per scope in worst case.

**Potential Improvement**: Add indexing to `TypeSymbol` members:
```csharp
public record TypeSymbol : Symbol
{
    // Current
    public List<FunctionSymbol> Methods { get; init; } = new();
    
    // Add for fast lookup
    private Dictionary<string, FunctionSymbol>? _methodIndex;
    public FunctionSymbol? FindMethod(string name)
    {
        _methodIndex ??= Methods.ToDictionary(m => m.Name);
        return _methodIndex.GetValueOrDefault(name);
    }
}
```

**Trade-off**: More memory, faster lookups. Profile before optimizing!

---

#### 5. **Bug Fixes**

**Common Issues**:

**Issue**: Symbols lose location information
```csharp
// ❌ Bad: DeclarationLine not set
var symbol = new VariableSymbol
{
    Name = name,
    Type = type
    // Missing: DeclarationLine, DeclarationColumn
};

// ✅ Good: Always include location
var symbol = new VariableSymbol
{
    Name = name,
    Type = type,
    DeclarationLine = node.Line,
    DeclarationColumn = node.Column
};
```

**Issue**: Forgetting to set `Kind` property
```csharp
// ❌ Bad: Kind defaults to 0 (Variable)
var funcSymbol = new FunctionSymbol { Name = "foo" };

// ✅ Good: Explicitly set Kind
var funcSymbol = new FunctionSymbol 
{ 
    Name = "foo",
    Kind = SymbolKind.Function
};
```

---

### Testing Changes

**Always add tests** when modifying `Symbol.cs`:

```csharp
// In Sharpy.Compiler.Tests/Semantic/SymbolTests.cs

[Fact]
public void VariableSymbol_SetsTypeCorrectly()
{
    var symbol = new VariableSymbol
    {
        Name = "x",
        Kind = SymbolKind.Variable,
        Type = SemanticType.Int
    };
    
    Assert.Equal("x", symbol.Name);
    Assert.Equal(SymbolKind.Variable, symbol.Kind);
    Assert.Equal(SemanticType.Int, symbol.Type);
}

[Fact]
public void TypeSymbol_IsGeneric_ReturnsTrueForGenericTypes()
{
    var symbol = new TypeSymbol
    {
        Name = "Box",
        TypeParameters = ["T"]
    };
    
    Assert.True(symbol.IsGeneric);
}
```

---

### Code Review Checklist

When reviewing changes to `Symbol.cs`:

- [ ] **Immutability preserved**: New fields use `init` (except justified exceptions like `BaseType`)
- [ ] **Default values set**: Collections initialized to `new()`, not left null
- [ ] **SymbolKind updated**: If adding new symbol type, enum includes it
- [ ] **Location tracking**: New symbols set `DeclarationLine`/`DeclarationColumn` when applicable
- [ ] **Documentation added**: XML comments explain purpose of new fields
- [ ] **Tests included**: New functionality has corresponding unit tests
- [ ] **Backward compatibility**: Existing code won't break (check `NameResolver`, `TypeChecker`, `CodeGen`)

---

## Summary

`Symbol.cs` is the **data backbone** of Sharpy's semantic analysis. It defines immutable, type-safe representations of program entities that flow through the compiler pipeline:

1. **Parser** creates AST nodes
2. **NameResolver** creates Symbols from AST and populates SymbolTable
3. **TypeChecker** reads Symbols, validates types, updates symbol metadata
4. **CodeGenerator** reads Symbols to emit C# code
5. **BuiltinRegistry** and **ImportResolver** create Symbols for builtins and .NET types

**Key Takeaways**:
- Symbols are immutable records (except `BaseType` for circular dependencies)
- Separation between Symbol (named entities) and SemanticType (types)
- .NET interop via `ClrType` and `ClrMethod` fields
- Rich metadata (location, access level, modifiers) for error reporting and analysis

**Next Steps for Learning**:
1. Read `SymbolTable.cs` to see how symbols are stored and looked up
2. Read `NameResolver.cs` to see symbols being created from AST
3. Read `TypeChecker.cs` to see symbols being used for type validation
4. Explore `SemanticType.cs` for the type system representation

Happy compiling! 🚀
