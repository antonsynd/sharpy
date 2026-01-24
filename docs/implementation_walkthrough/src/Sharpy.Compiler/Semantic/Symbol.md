# Walkthrough: Symbol.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Symbol.cs`

---

## 1. Overview

`Symbol.cs` is the foundational data structure file for Sharpy's semantic analysis phase. It defines the **symbol hierarchy** that represents every named entity in a Sharpy program during compilation.

Think of symbols as the compiler's internal "business cards" for everything that has a name in your code:
- Variables and parameters
- Functions and methods
- Classes, interfaces, and types
- Modules and type aliases
- Generic type parameters

This file is **pure data definitions** - no logic, just immutable record types that hold metadata about program entities. It's the vocabulary that other semantic analysis components (like `NameResolver`, `TypeChecker`, `SymbolTable`) use to communicate.

**Role in the Compiler Pipeline:**
```
Source (.spy) → Lexer → Parser (AST) → [Symbol.cs data structures] → Semantic Analysis → CodeGen
                                              ↑
                                    NameResolver populates these
                                    TypeChecker annotates them
                                    RoslynEmitter reads them
```

---

## 2. Class/Type Structure

### Core Hierarchy

```
Symbol (abstract base)
├── VariableSymbol      - Variables, fields
├── FunctionSymbol      - Functions, methods
├── TypeSymbol          - Classes, structs, interfaces, enums
├── ModuleSymbol        - Sharpy modules (.spy files)
├── TypeAliasSymbol     - Type aliases (compile-time only)
└── TypeParameterSymbol - Generic type parameters (T, K, V, etc.)

Supporting Types:
├── ParameterSymbol     - Function parameters
└── PropertySymbol      - Properties (future C#-style properties)

Enums:
├── SymbolKind          - Discriminator for symbol types
├── TypeKind            - Class vs Struct vs Interface vs Enum
└── AccessLevel         - Public, Protected, Private
```

### Design Choice: Records Over Classes

All symbols use C# **records** instead of classes:
```csharp
public abstract record Symbol { ... }
```

**Why records?**
1. **Immutability by default** - Semantic info shouldn't change after being set
2. **Value semantics** - Two symbols with same data are equal
3. **With-expressions** - Easy to create modified copies: `symbol with { Type = newType }`
4. **Compact syntax** - Less boilerplate than classes

---

## 3. Key Symbol Types

### 3.1 Symbol (Abstract Base)

```csharp
public abstract record Symbol
{
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Public;
    public int? DeclarationLine { get; init; }
    public int? DeclarationColumn { get; init; }

    // Re-export tracking (for module imports)
    public bool IsReExport { get; init; }
    public string? OriginalModule { get; init; }
    
    // Code generation metadata (computed during semantic analysis)
    public CodeGenInfo? CodeGenInfo { get; set; }
}
```

**Common Properties:**
- **`Name`**: The identifier as written in source code (`my_function`, `MyClass`, etc.)
- **`Kind`**: Discriminator enum - quickly tells you what kind of symbol this is
- **`AccessLevel`**: Visibility (currently simplified to Public/Protected/Private)
- **`DeclarationLine/Column`**: Source location for error messages and IDE features
- **`IsReExport`**: True if this symbol is re-exported from another module (e.g., via `from .submodule import func`)
- **`OriginalModule`**: For re-exported symbols, the original module name where the symbol was defined
- **`CodeGenInfo`**: Metadata for C# code generation (C# name, version, etc.). Null until `CodeGenInfoComputer` pass runs

**Important: CodeGenInfo Migration Note:**
The `CodeGenInfo` property uses a mutable `set` accessor instead of `init` because:
1. Symbols are created during the `NameResolver` pass
2. `CodeGenInfo` is computed later during/after the `TypeChecker` pass
3. This allows populating codegen data without recreating symbol instances

**Future Migration:** New code should use `SemanticBinding.SetCodeGenInfo/GetCodeGenInfo` instead of direct mutation. The mutable setter is preserved for backward compatibility during migration

**Design Note:** All symbols have location info for diagnostics. When the compiler says "error at line 42," it's using these fields.

**Re-export Tracking:** The `IsReExport` and `OriginalModule` properties support Sharpy's module system. When you write:
```python
# module_a.spy
from .utils import helper_function

# Now helper_function is re-exported from module_a
```

The symbol for `helper_function` in `module_a` will have:
```csharp
IsReExport = true
OriginalModule = "utils"
```

---

### 3.2 VariableSymbol

```csharp
public record VariableSymbol : Symbol
{
    public SemanticType Type { get; set; } = SemanticType.Unknown;
    public bool IsParameter { get; init; }
    public bool IsConstant { get; init; }
    public bool HasDefaultValue { get; init; }
}
```

**Represents:**
- Local variables: `x = 42`
- Class fields: `class Foo: x: int`
- Module-level variables

**Key Properties:**
- **`Type`**: The resolved type (e.g., `int`, `str`, `List[int]`). Initially `SemanticType.Unknown`, resolved by `TypeResolver`/`TypeChecker`. Uses mutable `set` accessor for backward compatibility during migration (future code should use `SemanticBinding.SetVariableType/GetVariableType`)
- **`IsParameter`**: True if this is a function parameter (could use `ParameterSymbol` instead, but this dual-purpose design exists)
- **`IsConstant`**: True for `const` declarations (immutable values)
- **`HasDefaultValue`**: Whether the variable has an initializer

**Usage Pattern:**
```csharp
// During name resolution:
var symbol = new VariableSymbol
{
    Name = "user_count",
    Kind = SymbolKind.Variable,
    Type = SemanticType.Int,
    DeclarationLine = 15
};
symbolTable.Define(symbol);
```

---

### 3.3 FunctionSymbol

```csharp
public record FunctionSymbol : Symbol
{
    public List<ParameterSymbol> Parameters { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Unknown;
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }

    // Generic type parameters (for generic functions like def identity[T](value: T) -> T)
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;

    // For .NET interop
    public System.Reflection.MethodInfo? ClrMethod { get; init; }
}
```

**Represents:**
- Functions: `def calculate(x: int) -> float:`
- Methods: `def process(self) -> None:`
- Generic functions: `def identity[T](value: T) -> T:`
- .NET interop methods: `List<T>.Add()`

**Key Properties:**
- **`Parameters`**: Ordered list of parameter symbols (see `ParameterSymbol`)
- **`ReturnType`**: What the function returns (or `None` for void)
- **`IsStatic`**: Methods without `self` parameter
- **`IsAbstract/IsVirtual/IsOverride`**: OOP modifiers for inheritance
- **`TypeParameters`**: Generic type parameters (e.g., `[T, U]` for `def func[T, U](...)`)
- **`IsGeneric`**: Computed property - true if function has type parameters
- **`ClrMethod`**: **Critical for .NET interop** - links to actual .NET reflection method

**Design Insight: The .NET Bridge**

The `ClrMethod` property is the magic that makes .NET interop work:
```csharp
// When calling a .NET library method:
var listAddMethod = new FunctionSymbol
{
    Name = "Add",
    Parameters = new() { new ParameterSymbol { Name = "item", Type = typeT } },
    ReturnType = SemanticType.None,
    ClrMethod = typeof(List<>).GetMethod("Add")  // ← Links to real .NET method
};
```

Later, `RoslynEmitter` uses `ClrMethod` to generate the correct C# call.

**Generic Functions:**

Sharpy supports generic functions similar to Python's typing:
```python
def identity[T](value: T) -> T:
    return value

def zip[T, U](list1: List[T], list2: List[U]) -> List[Tuple[T, U]]:
    # ...
```

The `TypeParameters` property stores these as AST `TypeParameterDef` nodes, which include:
- Type parameter name (`T`, `U`, etc.)
- Optional constraints (future feature)

---

### 3.4 TypeSymbol (Most Complex)

```csharp
public record TypeSymbol : Symbol
{
    public TypeKind TypeKind { get; init; }
    public Type? ClrType { get; init; }
    public bool IsAbstract { get; init; }

    // Generic type parameters
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;

    // Members
    public List<VariableSymbol> Fields { get; init; } = new();
    public List<FunctionSymbol> Methods { get; init; } = new();
    public List<PropertySymbol> Properties { get; init; } = new();

    // Operator methods (dunder methods for operators)
    public Dictionary<string, List<FunctionSymbol>> OperatorMethods { get; init; } = new();

    // Protocol methods (non-operator dunders like __len__, __str__, __iter__)
    public Dictionary<string, List<FunctionSymbol>> ProtocolMethods { get; init; } = new();

    // Constructors - tracks all __init__ overloads
    public List<FunctionSymbol> Constructors { get; init; } = new();

    // Inheritance
    public TypeSymbol? BaseType { get; set; }
    public List<TypeSymbol> Interfaces { get; init; } = new();
}
```

**Represents:**
- Classes: `class User:`
- Generic classes: `class List[T]:`
- Structs, Interfaces, Enums
- .NET types: `System.String`

**Property Deep Dive:**

#### Generics
```csharp
public List<TypeParameterDef> TypeParameters { get; init; } = new();
public bool IsGeneric => TypeParameters.Count > 0;
```

For `class Dict[K, V]:`, this would be:
```csharp
TypeParameters = new() {
    new TypeParameterDef { Name = "K" },
    new TypeParameterDef { Name = "V" }
}
IsGeneric = true
```

**Note:** Unlike the older documentation, type parameters are now stored as `TypeParameterDef` AST nodes (not strings), which allows for future constraint support:
```python
# Future syntax (hypothetical)
class Dict[K: Hashable, V]:
    # K must implement Hashable protocol
```

#### Members
Three collections hold the type's members:
- **`Fields`**: Instance/static variables
- **`Methods`**: Functions defined in the class
- **`Properties`**: Getters/setters (future feature)

#### Python Dunder Methods (The Magic)

Sharpy follows Python's protocol: operators and built-in functions are implemented via "dunder" (double underscore) methods:

```python
class Point:
    def __add__(self, other):  # Operator: p1 + p2
        return Point(self.x + other.x, self.y + other.y)

    def __str__(self):         # Protocol: str(p)
        return f"Point({self.x}, {self.y})"
```

**Two dictionaries handle this:**

1. **`OperatorMethods`**: Maps operator dunders to implementations
   ```csharp
   OperatorMethods = new()
   {
       ["__add__"] = new() { addMethodSymbol },
       ["__eq__"] = new() { equalityMethodSymbol }
   }
   ```
   Used for: `+`, `-`, `*`, `/`, `==`, `!=`, `<`, `>`, etc.

2. **`ProtocolMethods`**: Maps protocol dunders to implementations
   ```csharp
   ProtocolMethods = new()
   {
       ["__str__"] = new() { strMethodSymbol },
       ["__len__"] = new() { lenMethodSymbol },
       ["__iter__"] = new() { iterMethodSymbol }
   }
   ```
   Used for: `str()`, `len()`, `iter()`, `print()`, etc.

**Why lists of overloads?** Python allows multiple signatures (though rare):
```python
def __add__(self, other: Point) -> Point: ...
def __add__(self, other: int) -> Point: ...  # Overload
```

This enables better .NET interop where overloading is common.

#### Constructors

The `Constructors` property is a new addition that tracks all `__init__` method overloads:

```csharp
public List<FunctionSymbol> Constructors { get; init; } = new();
```

**Why separate from Methods?** Unlike Python (which allows only one `__init__` that gets replaced), Sharpy supports multiple `__init__` methods that map to C# constructor overloads:

```python
class Point:
    def __init__(self):
        self.x = 0
        self.y = 0

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
```

Both `__init__` methods are stored in `Constructors`, and `RoslynEmitter` generates multiple C# constructors:
```csharp
public class Point
{
    public Point() { ... }
    public Point(int x, int y) { ... }
}
```

#### Inheritance
```csharp
public TypeSymbol? BaseType { get; set; }           // Single inheritance
public List<TypeSymbol> Interfaces { get; init; }   // Multiple interface impl
```

**Note:** `BaseType` is `{ get; set; }` (mutable!) while others are `{ get; init; }` (immutable). This is because inheritance chains are resolved in multiple passes (after all type declarations are processed), and circular references need to be handled carefully. **Migration note:** Future code should use `SemanticBinding.SetBaseType/GetBaseType` instead of direct mutation.

---

### 3.5 ParameterSymbol

```csharp
public record ParameterSymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasDefault { get; init; }
    public Expression? DefaultValue { get; init; }
    public bool IsVariadic { get; init; }
}
```

**Represents:** Function/method parameters

**Key Properties:**
- **`HasDefault`**: Whether parameter is optional
- **`DefaultValue`**: The AST expression for the default (e.g., `None`, `42`, `[]`)
- **`IsVariadic`**: True for variadic parameters (`*args`). When true, `Type` contains the **element type**, not the array type

**Variadic Parameters:**
```python
def print_all(*args: str) -> None:
    for arg in args:
        print(arg)
```

Creates:
```csharp
new ParameterSymbol 
{ 
    Name = "args", 
    Type = SemanticType.Str,  // Element type, not array!
    IsVariadic = true 
}
```

When `IsVariadic = true`, the code generator wraps the type in a `params` array.

**Example:**
```python
def greet(name: str, greeting: str = "Hello") -> str:
    return f"{greeting}, {name}"
```

Parameters:
```csharp
new List<ParameterSymbol>
{
    new() { Name = "name", Type = SemanticType.Str, HasDefault = false },
    new() { Name = "greeting", Type = SemanticType.Str, HasDefault = true, DefaultValue = StringLiteralExpr("Hello") }
}
```

---

### 3.6 PropertySymbol (Future Feature)

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

**Status:** Placeholder for C#-style properties (not yet implemented in Sharpy)

**Future Usage:**
```python
# Future Sharpy syntax (hypothetical)
class User:
    @property
    def full_name(self) -> str:
        get: return f"{self.first} {self.last}"
        set: self._full_name = value
```

---

### 3.7 ModuleSymbol

```csharp
public record ModuleSymbol : Symbol
{
    public string FilePath { get; init; } = string.Empty;
    public Dictionary<string, Symbol> Exports { get; init; } = new();
}
```

**Represents:** A `.spy` source file as a module

**Key Properties:**
- **`FilePath`**: Absolute path to the `.spy` file
- **`Exports`**: Dictionary mapping exported names to their symbols (public functions, classes, etc.)

**Important Change:** `Exports` is now a `Dictionary<string, Symbol>` (not `List<Symbol>`), which provides O(1) lookup by name.

**Usage Pattern:**
```csharp
// When importing: from utils import calculate
var moduleSymbol = new ModuleSymbol
{
    Name = "utils",
    FilePath = "/path/to/utils.spy",
    Exports = new()
    {
        ["calculate"] = calculateFunctionSymbol,
        ["Helper"] = HelperClassSymbol
    }
};
```

---

### 3.8 TypeAliasSymbol

```csharp
public record TypeAliasSymbol : Symbol
{
    public TypeAnnotation? TypeAnnotation { get; init; }
    public Parser.Ast.FunctionType? FunctionType { get; init; }
}
```

**Represents:** Type aliases (compile-time only, no C# output)

**Purpose:** Sharpy supports Python-style type aliases for better code readability:
```python
# Simple type alias
UserId = int

# Complex type alias
JsonDict = Dict[str, Any]

# Function type alias
Callback = Callable[[int, str], bool]
```

**Key Properties:**
- **`TypeAnnotation`**: For simple and generic type aliases (e.g., `UserId = int`, `JsonDict = Dict[str, Any]`)
- **`FunctionType`**: For callable/function type aliases (e.g., `Callback = Callable[[int], bool]`)

**Design Note:** Type aliases are purely compile-time constructs. They don't generate any C# code - they're just replaced with their underlying types during semantic analysis.

**Usage in Compiler:**
```csharp
// When the type checker encounters a type alias:
if (symbol is TypeAliasSymbol alias)
{
    // Resolve to the underlying type
    var actualType = ResolveTypeAnnotation(alias.TypeAnnotation);
    // Use actualType for type checking
}
```

---

### 3.9 TypeParameterSymbol

```csharp
public record TypeParameterSymbol : Symbol
{
    /// <summary>
    /// The type symbol that declares this type parameter
    /// </summary>
    public TypeSymbol? DeclaringType { get; init; }
}
```

**Represents:** Generic type parameter symbols (e.g., `T` in `class Box[T]`)

**Purpose:** When you define a generic type or function, each type parameter gets its own symbol:
```python
class Box[T]:
    def get(self) -> T:
        # T is a TypeParameterSymbol here
        return self.value
```

**Key Properties:**
- **`DeclaringType`**: Back-reference to the type that declares this type parameter

**Design Insight:** Type parameters are symbols too! This allows them to be looked up in the symbol table like any other identifier. When the compiler sees `T` in the body of `Box[T]`, it resolves it to a `TypeParameterSymbol`.

**Example:**
```csharp
// For class Box[T]:
var typeParamSymbol = new TypeParameterSymbol
{
    Name = "T",
    Kind = SymbolKind.TypeParameter,
    DeclaringType = boxTypeSymbol
};

// Add to symbol table so `T` can be resolved in method bodies
symbolTable.Define(typeParamSymbol);
```

---

## 4. Supporting Enums

### 4.1 SymbolKind

```csharp
public enum SymbolKind
{
    Variable,
    Parameter,
    Function,
    Type,
    Module,
    Property,
    TypeAlias,
    TypeParameter
}
```

**Purpose:** Quick type discrimination without reflection

**Usage:**
```csharp
if (symbol.Kind == SymbolKind.Function)
{
    var funcSymbol = (FunctionSymbol)symbol;
    // Now access funcSymbol.Parameters
}
```

**Recent Additions:**
- `TypeAlias`: Added to support type alias symbols
- `TypeParameter`: Added to support generic type parameter symbols

---

### 4.2 TypeKind

```csharp
public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum
}
```

**Purpose:** Distinguishes different type definitions

**Sharpy Mapping:**
- **`Class`**: Reference type with inheritance (`class User:`)
- **`Struct`**: Value type, stack-allocated (future feature)
- **`Interface`**: Protocol/interface definition (future feature)
- **`Enum`**: Enumeration type (future feature)

Currently, Sharpy primarily uses `Class`.

---

### 4.3 AccessLevel

```csharp
public enum AccessLevel
{
    Public,
    Protected,
    Private
}
```

**Purpose:** Visibility control

**Sharpy Convention:**
- **`Public`**: No underscore prefix (`my_method`)
- **`Protected`**: Single underscore (`_helper_method`)
- **`Private`**: Double underscore (`__internal_method`)

This follows Python's convention, though enforcement is not as strict as C#.

---

## 5. Dependencies

### Within Sharpy.Compiler

**Direct Dependencies:**
```
Symbol.cs
├── Parser.Ast (Expression, TypeAnnotation, TypeParameterDef, FunctionType)
└── SemanticType (defined elsewhere in Semantic/)
```

**Consumed By:**
```
Symbol.cs
├── SymbolTable.cs          - Stores and looks up symbols
├── NameResolver.cs         - Creates symbols from AST
├── TypeResolver.cs         - Fills in Type fields
├── TypeChecker.cs          - Validates using symbol info
├── RoslynEmitter.cs        - Reads symbols to generate C#
└── SemanticInfo.cs         - Maps AST nodes to symbols
```

### .NET Framework Dependencies

- **`System.Reflection.MethodInfo`**: For `FunctionSymbol.ClrMethod`
- **`System.Type`**: For `TypeSymbol.ClrType`

These are critical for bridging Sharpy ↔ .NET interop.

---

## 5.1 Symbol Lifecycle in the Compiler Pipeline

Understanding when symbol properties are populated is crucial for debugging:

**Multi-Pass Semantic Analysis:**

```
Pass 1: NameResolver.ResolveDeclarations()
├── Create Symbol instances
├── Set: Name, Kind, AccessLevel, DeclarationLine/Column
└── Add to SymbolTable

Pass 2: NameResolver.ResolveInheritance()
├── Set: TypeSymbol.BaseType
└── Set: TypeSymbol.Interfaces

Pass 3: TypeResolver.ResolveTypes()
├── Resolve type annotations
└── Set preliminary types on symbols

Pass 4: TypeChecker.CheckModule()
├── Infer types (e.g., x = 5 → int)
├── Set: VariableSymbol.Type (via mutation or SemanticBinding)
└── Validate type consistency

Pass 5: CodeGenInfoComputer.Compute()
├── Compute C# names (PascalCase, camelCase, etc.)
├── Set: Symbol.CodeGenInfo
└── Handle variable versioning (x, x_1, x_2 for shadowing)

Code Generation: RoslynEmitter
└── Read symbols with complete information
```

**Why Multiple Passes?**

1. **Forward References:** Class `Dog` can reference class `Cat` defined later
2. **Circular Dependencies:** Class `A` inherits from `B`, `B` contains field of type `A`
3. **Type Inference:** Need all declarations before inferring types
4. **Name Mangling:** Need types resolved before computing C# names

**Example Timeline:**

```python
class Dog:
    def chase(self, target: Cat) -> None:  # Forward reference to Cat
        pass

class Cat:
    pass
```

- **Pass 1:** Create `TypeSymbol` for `Dog` and `Cat` (both exist now)
- **Pass 3:** Resolve `Cat` type annotation in `chase` parameter (now possible)
- **Pass 4:** Validate the method signature
- **Pass 5:** Compute C# names: `Dog` (PascalCase), `Chase` (PascalCase)

---

## 6. Patterns and Design Decisions

### 6.1 Immutable Records Pattern

**Decision:** Use C# records with `init` properties

**Benefits:**
- Thread-safe (semantic analysis could be parallelized in the future)
- Prevents accidental mutation during multi-pass analysis
- Clear data flow: create once, never modify

**Exceptions:**
- `TypeSymbol.BaseType` is mutable (`set`) due to circular inheritance resolution
- `VariableSymbol.Type` is mutable (`set`) to allow multi-pass type inference

---

### 6.2 Separation of Concerns

**Decision:** Symbols are **data-only**, no behavior

**Pattern:**
```csharp
// ❌ DON'T put logic in Symbol.cs
public record FunctionSymbol : Symbol
{
    public bool IsValid() => Parameters.All(p => p.Type != SemanticType.Unknown);  // NO!
}

// ✅ DO put logic in separate semantic components
public class TypeChecker
{
    public bool ValidateFunction(FunctionSymbol func) { ... }  // YES!
}
```

**Rationale:** Keeps Symbol.cs simple and stable. Logic changes constantly; data structures don't.

---

### 6.3 The Dunder Method Design

**Decision:** Separate dictionaries for `OperatorMethods` vs `ProtocolMethods`

**Why not one dictionary?**
1. **Semantic difference**: Operators are binary/unary operations; protocols are "capabilities"
2. **Code generation differs**: Operators → operator overloads; protocols → interface implementations
3. **Validation differs**: `OperatorValidator` vs `ProtocolValidator` check different rules

**Example:**
```csharp
// Operator: Must have specific signatures
OperatorMethods["__add__"]  // Must return compatible type

// Protocol: More flexible
ProtocolMethods["__len__"]  // Just needs to return int
```

---

### 6.4 Generic Type Parameters as AST Nodes

**Decision:** Store type parameters as `List<TypeParameterDef>` not `List<string>`

```csharp
public List<TypeParameterDef> TypeParameters { get; init; } = new();
```

**Why AST nodes instead of strings?**
```python
class Dict[K, V]:  # K and V have metadata beyond just names
    def get(self, key: K) -> V:
        ...
```

Using `TypeParameterDef` (an AST node) allows for:
1. **Source location tracking**: Error messages can point to the type parameter declaration
2. **Future constraints**: Support for `class Dict[K: Hashable, V]` syntax
3. **Metadata**: Additional information like variance (covariant/contravariant) in the future

The `TypeResolver` handles mapping these to actual types during type instantiation:
```python
my_dict: Dict[str, int]  # K=str, V=int
```

---

### 6.5 Lists for Overloads

**Decision:** `Dictionary<string, List<FunctionSymbol>>` for operators/protocols

**Why lists?**
Even though Python doesn't support true overloading, Sharpy does (for .NET interop):
```csharp
// Multiple signatures for same operator
OperatorMethods["__add__"] = new()
{
    addPointSymbol,      // Point + Point
    addScalarSymbol      // Point + int
};
```

This enables better .NET interop where overloading is common.

---

### 6.6 Module Exports as Dictionary

**Decision:** `ModuleSymbol.Exports` uses `Dictionary<string, Symbol>` instead of `List<Symbol>`

**Why dictionary?**
1. **O(1) lookup**: When resolving `from module import foo`, we can directly look up "foo" without iterating
2. **Prevents duplicates**: Dictionary keys are unique, preventing accidental duplicate exports
3. **Matches usage pattern**: Module imports always look up by name

**Before (old design):**
```csharp
// O(n) lookup
var symbol = module.Exports.FirstOrDefault(s => s.Name == "foo");
```

**After (current design):**
```csharp
// O(1) lookup
module.Exports.TryGetValue("foo", out var symbol);
```

---

### 6.7 Separate Constructors Collection

**Decision:** `TypeSymbol.Constructors` is separate from `Methods`

**Why separate?**
1. **C# mapping**: Constructors are distinct from methods in C# (different syntax, no return type)
2. **Overload resolution**: Constructor overloading is common and needs special handling
3. **Python semantics**: While Python only allows one `__init__`, Sharpy extends this to support overloading
4. **Code generation**: `RoslynEmitter` generates constructors differently from methods

**Example:**
```python
class Point:
    def __init__(self):
        self.x = 0
        self.y = 0

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
```

Both are stored in `Constructors`, not `Methods`:
```csharp
typeSymbol.Constructors = new()
{
    constructorNoArgs,
    constructorWithArgs
};
```

---

## 7. Debugging Tips

### 7.1 Inspecting Symbols

When debugging semantic analysis, symbols are your best friend:

```csharp
// In NameResolver, TypeChecker, etc.
Console.WriteLine($"Symbol: {symbol.Name}, Kind: {symbol.Kind}, Line: {symbol.DeclarationLine}");

if (symbol is FunctionSymbol func)
{
    Console.WriteLine($"  Params: {string.Join(", ", func.Parameters.Select(p => $"{p.Name}: {p.Type}"))}");
    Console.WriteLine($"  Return: {func.ReturnType}");
    Console.WriteLine($"  Generic: {func.IsGeneric} ({func.TypeParameters.Count} type params)");
}

if (symbol is TypeSymbol type)
{
    Console.WriteLine($"  Generic: {type.IsGeneric} ({type.TypeParameters.Count} type params)");
    Console.WriteLine($"  Constructors: {type.Constructors.Count}");
}
```

### 7.2 Common Issues

**Problem:** "Symbol not found" errors

**Debug checklist:**
1. Was symbol added to `SymbolTable`? (Check `NameResolver`)
2. Is it in the correct scope? (Check `Scope.cs`)
3. Is the name correct? (Python `snake_case` vs C# `PascalCase`)
4. For re-exported symbols, check `IsReExport` and `OriginalModule`

**Problem:** Wrong type information

**Debug checklist:**
1. Check `TypeResolver` - did it visit this symbol?
2. Look for `SemanticType.Unknown` - means type wasn't resolved
3. For .NET types, verify `ClrType` is set
4. For type parameters, check `TypeParameterSymbol` is in scope

**Problem:** Operator/protocol not working

**Debug checklist:**
1. Check `OperatorMethods`/`ProtocolMethods` dictionaries
2. Verify dunder name is correct (`__add__`, not `__Add__`)
3. Check signature in `OperatorValidator`/`ProtocolValidator`

**Problem:** Constructor not generating correctly

**Debug checklist:**
1. Check if `__init__` is in `Constructors` (not `Methods`)
2. Verify all overloads are tracked
3. Check `RoslynEmitter` is reading from `Constructors`

**Problem:** Type alias not resolving

**Debug checklist:**
1. Check `TypeAliasSymbol.TypeAnnotation` or `FunctionType` is set
2. Verify `TypeResolver` is unwrapping the alias
3. Ensure alias is in symbol table with `SymbolKind.TypeAlias`

---

### 7.3 Useful Breakpoints

Set breakpoints at these locations when investigating symbol issues:

1. **Symbol Creation:**
   ```csharp
   // NameResolver.cs
   var symbol = new VariableSymbol { ... };  // ← BREAKPOINT HERE
   ```

2. **Symbol Lookup:**
   ```csharp
   // SymbolTable.cs
   public Symbol? Resolve(string name) { ... }  // ← BREAKPOINT HERE
   ```

3. **Type Assignment:**
   ```csharp
   // TypeResolver.cs
   symbol.Type = resolvedType;  // ← BREAKPOINT HERE (for VariableSymbol)
   ```

4. **Constructor Registration:**
   ```csharp
   // NameResolver.cs (or wherever constructors are added)
   typeSymbol.Constructors.Add(constructorSymbol);  // ← BREAKPOINT HERE
   ```

---

### 7.4 Symbol Table Dumping

Add this helper to your debugging session:

```csharp
public static void DumpSymbolTable(SymbolTable table)
{
    foreach (var symbol in table.AllSymbols())
    {
        Console.WriteLine($"{symbol.Kind} '{symbol.Name}' at {symbol.DeclarationLine}");

        if (symbol is TypeSymbol type)
        {
            Console.WriteLine($"  Fields: {type.Fields.Count}");
            Console.WriteLine($"  Methods: {type.Methods.Count}");
            Console.WriteLine($"  Constructors: {type.Constructors.Count}");
            Console.WriteLine($"  Operators: {string.Join(", ", type.OperatorMethods.Keys)}");
            Console.WriteLine($"  Protocols: {string.Join(", ", type.ProtocolMethods.Keys)}");
            Console.WriteLine($"  Generic: {type.IsGeneric} ({type.TypeParameters.Count} params)");
        }

        if (symbol is FunctionSymbol func)
        {
            Console.WriteLine($"  Generic: {func.IsGeneric} ({func.TypeParameters.Count} params)");
        }

        if (symbol is ModuleSymbol module)
        {
            Console.WriteLine($"  Exports: {string.Join(", ", module.Exports.Keys)}");
        }

        if (symbol is TypeAliasSymbol alias)
        {
            Console.WriteLine($"  Aliasing: {alias.TypeAnnotation?.ToString() ?? alias.FunctionType?.ToString()}");
        }

        if (symbol.IsReExport)
        {
            Console.WriteLine($"  Re-exported from: {symbol.OriginalModule}");
        }
    }
}
```

---

## 8. Contribution Guidelines

### 8.1 When to Modify Symbol.cs

✅ **DO** modify when:
- Adding a new language feature that introduces new symbol types
- Adding metadata needed by multiple semantic passes
- Extending .NET interop capabilities
- Supporting new type system features (e.g., type aliases, type parameters)

❌ **DON'T** modify for:
- Logic or validation (put in separate semantic components)
- Temporary debugging data (use `SemanticInfo` side table)
- One-off feature-specific flags (consider composition instead)

---

### 8.2 Adding a New Symbol Type

**Template:**
```csharp
/// <summary>
/// Brief description of what this symbol represents
/// </summary>
public record MySymbol : Symbol
{
    // Core properties
    public SemanticType Type { get; init; } = SemanticType.Unknown;

    // Feature-specific properties
    public bool MyFeatureFlag { get; init; }

    // .NET interop (if applicable)
    public SomeClrType? ClrReference { get; init; }
}
```

**Steps:**
1. Add the record type
2. Add enum value to `SymbolKind`
3. Update `NameResolver` to create instances
4. Update `TypeResolver` to populate type info
5. Update `RoslynEmitter` to handle codegen
6. Add tests in `Sharpy.Compiler.Tests/Semantic/`

---

### 8.3 Adding Properties to Existing Symbols

**Checklist:**
- [ ] Use `{ get; init; }` unless you have a very good reason for `{ get; set; }`
- [ ] Provide a sensible default value
- [ ] Add XML doc comment
- [ ] Update dependent components (`NameResolver`, `TypeChecker`, etc.)
- [ ] Add test cases that exercise the new property

**Example:**
```csharp
public record FunctionSymbol : Symbol
{
    // Existing properties...

    /// <summary>
    /// Indicates if this function is async (future feature for async/await)
    /// </summary>
    public bool IsAsync { get; init; }  // ← New property
}
```

---

### 8.4 Best Practices

**Naming:**
- Use clear, descriptive names: `HasDefaultValue`, not `HasDef`
- Follow C# conventions: `PascalCase` for properties
- Boolean properties: `Is`, `Has`, `Can` prefixes

**Documentation:**
- Add XML doc comments (`///`) for all public types and properties
- Explain *why* something exists, not just *what* it is
- Link to related types or concepts

**Types:**
- Prefer `SemanticType` over strings for type information
- Use nullable types (`?`) for optional references
- Use empty collections instead of nulls: `= new()` not `= null`

---

### 8.5 Testing Symbol Changes

**Unit Tests:** `Sharpy.Compiler.Tests/Semantic/SymbolTests.cs` (if exists)

**Integration Tests:** Modify semantic tests:
```csharp
[Fact]
public void TestNewSymbolFeature()
{
    var source = @"
        def my_function(x: int) -> str:
            return str(x)
    ";

    var module = CompileToSemanticModel(source);
    var funcSymbol = module.GetSymbol<FunctionSymbol>("my_function");

    Assert.Equal(SemanticType.Str, funcSymbol.ReturnType);
    // Assert your new property
}
```

---

### 8.6 Common Pitfalls

**Pitfall 1: Adding mutable state**
```csharp
// ❌ BAD
public record FunctionSymbol : Symbol
{
    public List<string> Errors { get; set; } = new();  // Mutable!
}

// ✅ GOOD - Put errors in SemanticInfo or CompilerDiagnostics
```

**Pitfall 2: Circular references**
```csharp
// ❌ PROBLEMATIC
public record TypeSymbol : Symbol
{
    public TypeSymbol? BaseType { get; init; }  // Can't build circular chains with init!
}

// ✅ ACCEPTABLE (already implemented)
public record TypeSymbol : Symbol
{
    public TypeSymbol? BaseType { get; set; }  // Allows multi-pass resolution
}
```

**Pitfall 3: Coupling to AST**
```csharp
// ❌ BAD - Creates tight coupling
public record FunctionSymbol : Symbol
{
    public FunctionDef AstNode { get; init; }  // Don't store entire AST!
}

// ✅ GOOD - Use location info or specific AST fragments
public record FunctionSymbol : Symbol
{
    public int? DeclarationLine { get; init; }  // Just line/col is enough
}

// ✅ ACCEPTABLE - For specific AST fragments needed by semantic analysis
public record ParameterSymbol
{
    public Expression? DefaultValue { get; init; }  // OK: needed for codegen
}
```

**Pitfall 4: Using strings instead of typed references**
```csharp
// ❌ BAD
public record TypeSymbol : Symbol
{
    public List<string> TypeParameters { get; init; }  // Just names, no metadata
}

// ✅ GOOD
public record TypeSymbol : Symbol
{
    public List<TypeParameterDef> TypeParameters { get; init; }  // Rich AST nodes
}
```

---

### 8.7 Deprecation Strategy

If you need to deprecate a symbol property:

1. Mark as obsolete:
   ```csharp
   [Obsolete("Use NewProperty instead. This will be removed in v2.0")]
   public bool OldProperty { get; init; }
   ```

2. Add replacement property

3. Update all usages in codebase

4. Remove in next major version

---

## 9. Related Files

**Essential companions to Symbol.cs:**

| File | Purpose | Relationship |
|------|---------|--------------|
| [`SymbolTable.md`](SymbolTable.md) | Stores symbols, scoped lookup | Uses `Symbol` as data |
| [`Scope.md`](Scope.md) | Manages lexical scopes | Contains `Symbol` instances |
| [`SemanticType.md`](SemanticType.md) | Type representation | Stored in `Symbol.Type` |
| [`CodeGenInfo.md`](CodeGenInfo.md) | Code generation metadata | Stored in `Symbol.CodeGenInfo` |
| [`SemanticBinding.md`](SemanticBinding.md) | Future API for symbol data | Replacement for mutable properties |
| [`NameResolver.md`](NameResolver.md) | AST → Symbol conversion | Creates `Symbol` instances |
| [`TypeResolver.md`](TypeResolver.md) | Resolves types | Populates `Symbol.Type` |
| [`TypeChecker.md`](TypeChecker.md) | Validates semantics | Reads `Symbol` metadata |
| [`CodeGenInfoComputer.md`](CodeGenInfoComputer.md) | Computes C# names | Populates `Symbol.CodeGenInfo` |
| [`SemanticInfo.md`](SemanticInfo.md) | AST ↔ Symbol mapping | Maps `Node` → `Symbol` |

**Code Generation:**
| File | Purpose |
|------|---------|
| [`../CodeGen/RoslynEmitter.md`](../CodeGen/RoslynEmitter.md) | Reads symbols to generate C# |

---

## 10. Quick Reference

### Symbol Type Cheat Sheet

```
Need to represent...          Use this Symbol type
────────────────────────────────────────────────────────
Variable/field                VariableSymbol
Function/method               FunctionSymbol
Class/struct/interface        TypeSymbol
Module (.spy file)            ModuleSymbol
Function parameter            ParameterSymbol
Property (future)             PropertySymbol
Type alias                    TypeAliasSymbol
Generic type parameter        TypeParameterSymbol
```

### Property Quick Lookup

```
Need to know...                    Check this property
──────────────────────────────────────────────────────────
What is this symbol?               symbol.Kind
Where was it declared?             symbol.DeclarationLine/Column
What type is it?                   symbol.Type (Variable/Parameter)
What does it return?               funcSymbol.ReturnType
What are its parameters?           funcSymbol.Parameters
Is it generic?                     typeSymbol.IsGeneric / funcSymbol.IsGeneric
How many type parameters?          typeSymbol.TypeParameters.Count
What constructors does it have?    typeSymbol.Constructors
What operators does it support?    typeSymbol.OperatorMethods
What protocols does it implement?  typeSymbol.ProtocolMethods
What .NET type is it?              typeSymbol.ClrType
Is it re-exported?                 symbol.IsReExport
What does the alias represent?     aliasSymbol.TypeAnnotation
```

---

## 11. Summary

`Symbol.cs` is the **data backbone** of Sharpy's semantic analysis. It defines immutable, strongly-typed records that represent every named entity in a Sharpy program. Key takeaways:

1. **Pure data structures** - No logic, just properties
2. **Immutable by design** - Uses C# records with `init` (with selective exceptions)
3. **.NET interop ready** - `ClrType` and `ClrMethod` bridge to .NET
4. **Python semantics** - Operator/protocol methods via dunders
5. **Multi-pass friendly** - Designed for incremental analysis
6. **Generics support** - Type parameters for both types and functions
7. **Type aliases** - Compile-time type definitions
8. **Re-export tracking** - Supports module import/export system
9. **Constructor overloading** - Separate tracking for `__init__` methods

When working with semantic analysis, you'll constantly create, read, and pass around these symbols. Understanding this file is essential to understanding how Sharpy's compiler thinks about code structure and types.

---

## 12. Cross-References

### Related Documentation Files

This file is closely related to:
- [`SymbolTable.md`](SymbolTable.md) - How symbols are stored and looked up
- [`SemanticType.md`](SemanticType.md) - The type system used by symbols
- [`NameResolver.md`](NameResolver.md) - How symbols are created from AST
- [`TypeResolver.md`](TypeResolver.md) - How symbol types are resolved
- [`Scope.md`](Scope.md) - How symbols are organized in scopes

### AST Dependencies

Symbol.cs depends on these AST types from `Parser.Ast`:
- `Expression` - For default parameter values
- `TypeAnnotation` - For type alias definitions
- `TypeParameterDef` - For generic type parameters
- `FunctionType` - For callable type aliases

See the Parser documentation for details on these AST node types.

---

**Next Steps:**
- Read [`SymbolTable.md`](SymbolTable.md) to see how symbols are stored and looked up
- Read [`NameResolver.md`](NameResolver.md) to see how symbols are created from AST
- Read [`TypeChecker.md`](TypeChecker.md) to see how symbols are validated
- Read [`../CodeGen/RoslynEmitter.md`](../CodeGen/RoslynEmitter.md) to see how symbols drive code generation
