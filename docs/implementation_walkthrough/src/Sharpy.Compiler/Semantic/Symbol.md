# Walkthrough: Symbol.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Symbol.cs`

---

## 1. Overview

`Symbol.cs` is the foundational data structure file for Sharpy's semantic analysis phase. It defines the **symbol hierarchy** that represents every named entity in a Sharpy program during compilation.

Think of symbols as the compiler's internal "business cards" for everything that has a name in your code:
- Variables and parameters
- Functions and methods
- Classes, interfaces, and types
- Modules and properties

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
└── ModuleSymbol        - Sharpy modules (.spy files)

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
}
```

**Common Properties:**
- **`Name`**: The identifier as written in source code (`my_function`, `MyClass`, etc.)
- **`Kind`**: Discriminator enum - quickly tells you what kind of symbol this is
- **`AccessLevel`**: Visibility (currently simplified to Public/Protected/Private)
- **`DeclarationLine/Column`**: Source location for error messages and IDE features

**Design Note:** All symbols have location info for diagnostics. When the compiler says "error at line 42," it's using these fields.

---

### 3.2 VariableSymbol

```csharp
public record VariableSymbol : Symbol
{
    public SemanticType Type { get; init; } = SemanticType.Unknown;
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
- **`Type`**: The resolved type (e.g., `int`, `str`, `List[int]`)
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
    
    // For .NET interop
    public System.Reflection.MethodInfo? ClrMethod { get; init; }
}
```

**Represents:**
- Functions: `def calculate(x: int) -> float:`
- Methods: `def process(self) -> None:`
- .NET interop methods: `List<T>.Add()`

**Key Properties:**
- **`Parameters`**: Ordered list of parameter symbols (see `ParameterSymbol`)
- **`ReturnType`**: What the function returns (or `None` for void)
- **`IsStatic`**: Methods without `self` parameter
- **`IsAbstract/IsVirtual/IsOverride`**: OOP modifiers for inheritance
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

---

### 3.4 TypeSymbol (Most Complex)

```csharp
public record TypeSymbol : Symbol
{
    public TypeKind TypeKind { get; init; }
    public Type? ClrType { get; init; }
    
    // Generics
    public List<string> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;
    
    // Members
    public List<VariableSymbol> Fields { get; init; } = new();
    public List<FunctionSymbol> Methods { get; init; } = new();
    public List<PropertySymbol> Properties { get; init; } = new();
    
    // Operator/Protocol methods (Python dunders)
    public Dictionary<string, List<FunctionSymbol>> OperatorMethods { get; init; } = new();
    public Dictionary<string, List<FunctionSymbol>> ProtocolMethods { get; init; } = new();
    
    // Inheritance
    public TypeSymbol? BaseType { get; set; }
    public List<TypeSymbol> Interfaces { get; init; } = new();
}
```

**Represents:**
- Classes: `class User:`
- Structs, Interfaces, Enums
- Generic types: `class List[T]:`
- .NET types: `System.String`

**Property Deep Dive:**

#### Generics
```csharp
public List<string> TypeParameters { get; init; } = new();
public bool IsGeneric => TypeParameters.Count > 0;
```

For `class Dict[K, V]:`, this would be:
```csharp
TypeParameters = new() { "K", "V" }
IsGeneric = true
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

#### Inheritance
```csharp
public TypeSymbol? BaseType { get; set; }           // Single inheritance
public List<TypeSymbol> Interfaces { get; init; }   // Multiple interface impl
```

**Note:** `BaseType` is `{ get; set; }` (mutable!) while others are `{ get; init; }` (immutable). This is because inheritance chains are resolved in multiple passes, and circular references need to be handled carefully.

---

### 3.5 ParameterSymbol

```csharp
public record ParameterSymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasDefault { get; init; }
    public Expression? DefaultValue { get; init; }
}
```

**Represents:** Function/method parameters

**Key Properties:**
- **`HasDefault`**: Whether parameter is optional
- **`DefaultValue`**: The AST expression for the default (e.g., `None`, `42`, `[]`)

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
    public List<Symbol> Exports { get; init; } = new();
}
```

**Represents:** A `.spy` source file as a module

**Key Properties:**
- **`FilePath`**: Absolute path to the `.spy` file
- **`Exports`**: Symbols visible to other modules (public functions, classes, etc.)

**Usage Pattern:**
```csharp
// When importing: from utils import calculate
var moduleSymbol = new ModuleSymbol
{
    Name = "utils",
    FilePath = "/path/to/utils.spy",
    Exports = new() { calculateFunctionSymbol, HelperClassSymbol }
};
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
    Property
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
├── Parser.Ast (Expression, Node types)
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

## 6. Patterns and Design Decisions

### 6.1 Immutable Records Pattern

**Decision:** Use C# records with `init` properties

**Benefits:**
- Thread-safe (semantic analysis could be parallelized in the future)
- Prevents accidental mutation during multi-pass analysis
- Clear data flow: create once, never modify

**Exception:** `TypeSymbol.BaseType` is mutable (`set`) due to circular inheritance resolution.

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

### 6.4 Generic Type Parameters as Strings

**Decision:** Store type parameters as `List<string>` not `List<TypeSymbol>`

```csharp
public List<string> TypeParameters { get; init; } = new();
```

**Why strings?**
```python
class Dict[K, V]:  # K and V are names, not resolved types yet
    def get(self, key: K) -> V:
        ...
```

At symbol creation time, `K` and `V` are just identifiers. They get resolved to actual types during type instantiation:
```python
my_dict: Dict[str, int]  # K=str, V=int
```

The `TypeResolver` handles this mapping later.

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
}
```

### 7.2 Common Issues

**Problem:** "Symbol not found" errors

**Debug checklist:**
1. Was symbol added to `SymbolTable`? (Check `NameResolver`)
2. Is it in the correct scope? (Check `Scope.cs`)
3. Is the name correct? (Python `snake_case` vs C# `PascalCase`)

**Problem:** Wrong type information

**Debug checklist:**
1. Check `TypeResolver` - did it visit this symbol?
2. Look for `SemanticType.Unknown` - means type wasn't resolved
3. For .NET types, verify `ClrType` is set

**Problem:** Operator/protocol not working

**Debug checklist:**
1. Check `OperatorMethods`/`ProtocolMethods` dictionaries
2. Verify dunder name is correct (`__add__`, not `__Add__`)
3. Check signature in `OperatorValidator`/`ProtocolValidator`

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
   symbol = symbol with { Type = resolvedType };  // ← BREAKPOINT HERE
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
            Console.WriteLine($"  Operators: {string.Join(", ", type.OperatorMethods.Keys)}");
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
    public TypeSymbol? BaseType { get; init; }  // Can't build cirular chains with init!
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
    public FunctionDef AstNode { get; init; }  // Don't store AST!
}

// ✅ GOOD - Use location info instead
public record FunctionSymbol : Symbol
{
    public int? DeclarationLine { get; init; }  // Just line/col is enough
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
| `SymbolTable.cs` | Stores symbols, scoped lookup | Uses `Symbol` as data |
| `Scope.cs` | Manages lexical scopes | Contains `Symbol` instances |
| `SemanticType.cs` | Type representation | Stored in `Symbol.Type` |
| `NameResolver.cs` | AST → Symbol conversion | Creates `Symbol` instances |
| `TypeResolver.cs` | Resolves types | Populates `Symbol.Type` |
| `TypeChecker.cs` | Validates semantics | Reads `Symbol` metadata |
| `SemanticInfo.cs` | AST ↔ Symbol mapping | Maps `Node` → `Symbol` |

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
Is it generic?                     typeSymbol.IsGeneric
What operators does it support?    typeSymbol.OperatorMethods
What protocols does it implement?  typeSymbol.ProtocolMethods
What .NET type is it?              typeSymbol.ClrType
```

---

## 11. Summary

`Symbol.cs` is the **data backbone** of Sharpy's semantic analysis. It defines immutable, strongly-typed records that represent every named entity in a Sharpy program. Key takeaways:

1. **Pure data structures** - No logic, just properties
2. **Immutable by design** - Uses C# records with `init`
3. **.NET interop ready** - `ClrType` and `ClrMethod` bridge to .NET
4. **Python semantics** - Operator/protocol methods via dunders
5. **Multi-pass friendly** - Designed for incremental analysis

When working with semantic analysis, you'll constantly create, read, and pass around these symbols. Understanding this file is essential to understanding how Sharpy's compiler thinks about code structure and types.

---

**Next Steps:**
- Read `SymbolTable.cs` to see how symbols are stored and looked up
- Read `NameResolver.cs` to see how symbols are created from AST
- Read `TypeChecker.cs` to see how symbols are validated
- Read `RoslynEmitter.cs` to see how symbols drive code generation
