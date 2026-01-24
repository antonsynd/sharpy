# Walkthrough: NameResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`

---

## Overview

`NameResolver` is the **first pass** of semantic analysis in the Sharpy compiler pipeline. Its primary responsibility is to build the symbol table by discovering all type definitions, function declarations, and top-level constants in a module. It operates in two distinct phases:

1. **Pass 1 (Declarations)**: Registers all types (classes, structs, interfaces, enums), functions, constants, and type aliases
2. **Pass 2 (Inheritance)**: Resolves inheritance relationships after all types are known

This two-pass approach solves the forward reference problem—types can reference other types that are defined later in the source file, or even across files in the same module.

**Pipeline Position**:
- **Upstream**: Parser (consumes AST nodes)
- **Downstream**: TypeChecker (produces populated SymbolTable)

**Key Outputs**:
- Populated `SymbolTable` with all symbols from the module
- List of semantic errors (redefinition errors, invalid inheritance)
- Type symbols with registered methods, fields, constructors, and operator/protocol methods

---

## Class Structure

### Main Class: `NameResolver`

```csharp
public class NameResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors;
    private readonly List<ClassDef> _classDefs;
    private readonly List<StructDef> _structDefs;
    private readonly List<InterfaceDef> _interfaceDefs;
    private string? _currentFilePath;
}
```

**Key Fields**:
- `_symbolTable`: The central symbol registry shared across all semantic phases
- `_errors`: Accumulated errors during name resolution (e.g., duplicate definitions)
- `_classDefs`, `_structDefs`, `_interfaceDefs`: Temporary storage for deferred inheritance resolution
- `_currentFilePath`: Tracks which source file is being processed (for cross-file type references)

---

## Two-Pass Architecture

### Pass 1: Declaration Resolution (`ResolveDeclarations`)

```csharp
public void ResolveDeclarations(Module module)
{
    foreach (var statement in module.Body)
    {
        ResolveDeclaration(statement);
    }
}
```

This pass processes only **declarations**:
- Classes, structs, interfaces, enums → Create `TypeSymbol`
- Top-level functions → Create `FunctionSymbol`
- Module-level constants (`const` variables) → Create `VariableSymbol`
- Type aliases → Create `TypeAliasSymbol`
- Import statements → Logged (actual resolution deferred to `ImportResolver`)

**Important**: Regular variable declarations (non-const) are **not** processed in this pass—they're handled later by `TypeChecker` during statement analysis.

### Pass 2: Inheritance Resolution (`ResolveInheritance`)

```csharp
public void ResolveInheritance()
{
    foreach (var classDef in _classDefs)
        ResolveClassInheritance(classDef);

    foreach (var structDef in _structDefs)
        ResolveStructInheritance(structDef);

    foreach (var interfaceDef in _interfaceDefs)
        ResolveInterfaceInheritance(interfaceDef);
}
```

After all types are declared, this pass:
- Resolves base class/interface names to actual `TypeSymbol` references
- Validates inheritance rules (e.g., structs can't inherit classes, only one base class allowed)
- Propagates interface methods through the inheritance hierarchy

---

## Key Methods

### Type Declaration Methods

#### `ResolveClassDeclaration(ClassDef classDef)`

**Purpose**: Registers a class type in the symbol table and processes its members.

**Key Steps**:
1. **Duplicate Check**: Ensures the class name isn't already defined in the current scope
2. **Abstract Detection**: Checks for `@abstract` decorator
3. **Create TypeSymbol**:
   ```csharp
   var typeSymbol = new TypeSymbol
   {
       Name = classDef.Name,
       TypeKind = TypeKind.Class,
       IsAbstract = isAbstract,
       TypeParameters = classDef.TypeParameters.ToList(),
       DefiningFilePath = _currentFilePath,
       // ...
   };
   ```
4. **Enter Class Scope**: Creates a nested scope for the class body (`class:MyClass`)
5. **Register Type Parameters**: Makes `T`, `U`, etc. available for use in method signatures
6. **Process Members**:
   - Methods → `ResolveMethodDeclaration`
   - Fields → `ResolveFieldDeclaration`
7. **Exit Scope**: Returns to module scope

**Store for Later**: The `ClassDef` AST node is saved in `_classDefs` for inheritance resolution in pass 2.

#### `ResolveStructDeclaration(StructDef structDef)`

Nearly identical to class resolution, but:
- Sets `TypeKind = TypeKind.Struct`
- No abstract checking (structs can't be abstract in C#)

#### `ResolveInterfaceDeclaration(InterfaceDef interfaceDef)`

Similar structure to class resolution, with these differences:
- All methods are implicitly abstract
- Validates that interface methods have **no implementation** (only `...` or `pass` allowed)
- Uses `ValidateInterfaceMethod` to enforce this rule

**Example Validation**:
```python
# Valid interface method
interface IDrawable:
    def draw(self) -> None:
        ...  # Ellipsis body is required

# Invalid - would trigger error
interface IDrawable:
    def draw(self) -> None:
        print("hello")  # Error: Interface methods can't have implementation
```

#### `ResolveEnumDeclaration(EnumDef enumDef)`

Enums are simple—just register the type symbol. Enum members are processed later during type checking.

---

### Member Declaration Methods

#### `ResolveMethodDeclaration(FunctionDef method, TypeSymbol owningType)`

**Purpose**: Registers a method in a class/struct/interface and determines its characteristics.

**Key Logic**:

1. **Access Level Determination** (see `DetermineAccessLevel` below):
   ```csharp
   var accessLevel = DetermineAccessLevel(method.Name);
   // __init__ → Public (dunder)
   // __private_method → Private (dunder without trailing __)
   // _protected_method → Protected
   // public_method → Public
   ```

2. **Static Detection**:
   ```csharp
   bool hasSelfParameter = method.Parameters.Any(p => p.Name == "self");
   bool isStatic = hasStaticDecorator || !hasSelfParameter;
   ```
   **Pythonic approach**: Methods without `self` are automatically static. The `@static` decorator is optional/redundant.

3. **Abstract Detection**:
   - Explicit: `@abstract` decorator
   - Implicit: Method in an `@abstract` class with ellipsis body (`...`)

4. **Constructor Registration**:
   ```csharp
   if (method.Name == "__init__")
   {
       owningType.Constructors.Add(funcSymbol);
       // Only register first __init__ in symbol table to avoid duplicates
       if (owningType.Constructors.Count == 1)
           _symbolTable.Define(funcSymbol);
   }
   ```
   **Important**: Sharpy supports constructor overloading (unlike Python), so multiple `__init__` methods are allowed.

5. **Operator/Protocol Method Caching**:
   - Operator dunders (`__add__`, `__mul__`, etc.) → `owningType.OperatorMethods`
   - Protocol dunders (`__len__`, `__str__`, `__iter__`, etc.) → `owningType.ProtocolMethods`
   - These caches enable fast lookup during type checking and code generation

**Example**:
```python
class Vector:
    # Constructor overload 1
    def __init__(self):
        ...

    # Constructor overload 2
    def __init__(self, x: int, y: int):
        ...

    # Static method (no self parameter)
    def zero() -> Vector:
        return Vector()

    # Operator dunder
    def __add__(self, other: Vector) -> Vector:
        ...
```

#### `ResolveFieldDeclaration(VariableDeclaration field, TypeSymbol owningType)`

Registers a class/struct field. Simpler than methods:
- Determines access level from name
- Creates `VariableSymbol` and adds to `owningType.Fields`

---

### Access Level Determination

#### `DetermineAccessLevel(string name)`

Implements Python naming conventions for C# interop:

| Pattern | Access Level | Example |
|---------|--------------|---------|
| `__name__` | Public | `__init__`, `__add__` (dunder methods) |
| `__name` | Private | `__internal_state` (name mangling) |
| `_name` | Protected | `_helper_method` |
| `name` | Public | `calculate_area` |

**Implementation**:
```csharp
if (name.StartsWith("__") && name.EndsWith("__"))
    return AccessLevel.Public;  // Dunder methods
if (name.StartsWith("__") && !name.EndsWith("__"))
    return AccessLevel.Private;  // Name mangling
if (name.StartsWith("_"))
    return AccessLevel.Protected;
return AccessLevel.Public;
```

**See**: [`docs/language_specification/naming_conventions.md`](../../../../../language_specification/naming_conventions.md) for the complete naming convention rules.

---

### Inheritance Resolution

#### `ResolveClassInheritance(ClassDef classDef)`

**Purpose**: Links base classes and interfaces to their `TypeSymbol` references.

**C# Inheritance Rules**:
- **One base class maximum** (C# single inheritance constraint)
- **Multiple interfaces** allowed

**Algorithm**:
```csharp
bool hasSetBaseType = false;
foreach (var baseAnnot in classDef.BaseClasses)
{
    var baseSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;

    if (baseSymbol.TypeKind == TypeKind.Class)
    {
        if (hasSetBaseType)
            AddError("Multiple base classes not allowed");
        else
        {
            typeSymbol.BaseType = baseSymbol;
            hasSetBaseType = true;
        }
    }
    else  // Interface
    {
        typeSymbol.Interfaces.Add(baseSymbol);
    }
}
```

**Example**:
```python
# Valid: One class, multiple interfaces
class Dog(Animal, IWalkable, ISpeakable):
    ...

# Invalid: Two classes
class Dog(Animal, Mammal):  # Error!
    ...
```

#### `ResolveStructInheritance(StructDef structDef)`

**Struct Rules**:
- Can only implement **interfaces** (no base class)
- C# structs implicitly inherit from `System.ValueType`

**Validation**:
```csharp
if (interfaceSymbol.TypeKind != TypeKind.Interface)
{
    AddError("Structs can only implement interfaces");
}
```

#### `ResolveInterfaceInheritance(InterfaceDef interfaceDef)`

Interfaces can extend other interfaces. After linking base interfaces, it calls `PropagateInterfaceMethods` to ensure derived interfaces inherit all method signatures from their parents.

---

### Interface Method Propagation

#### `PropagateInterfaceMethods(TypeSymbol interfaceSymbol)`

**Purpose**: Copies method signatures from base interfaces to derived interfaces (multi-level inheritance supported).

**Algorithm**: Breadth-First Search (BFS) to traverse interface hierarchy
```csharp
var visited = new HashSet<string> { interfaceSymbol.Name };
var queue = new Queue<TypeSymbol>(interfaceSymbol.Interfaces);

while (queue.Count > 0)
{
    var baseInterface = queue.Dequeue();
    if (!visited.Add(baseInterface.Name))
        continue;

    foreach (var method in baseInterface.Methods)
    {
        var signature = GetMethodSignature(method);
        if (seenMethods.Add(signature))
            interfaceSymbol.Methods.Add(method);  // Add reference
    }

    foreach (var grandBase in baseInterface.Interfaces)
        queue.Enqueue(grandBase);
}
```

**Why BFS?** Ensures all base interfaces are processed level-by-level, avoiding duplicates in diamond inheritance scenarios.

**Method Signature Deduplication**:
```csharp
private string GetMethodSignature(FunctionSymbol method)
{
    var paramTypes = method.Parameters
        .Where(p => p.Name != "self")
        .Select(p => p.Type?.GetDisplayName() ?? "unknown");
    return $"{method.Name}({string.Join(",", paramTypes)})";
}
```

**Example**:
```python
interface IBase:
    def foo(self) -> None: ...

interface IDerived(IBase):
    def bar(self) -> int: ...

# IDerived.Methods will contain both foo() and bar()
```

---

### Validation Methods

#### `ValidateInterfaceMethod(FunctionDef method, string interfaceName)`

Enforces that interface methods have **no implementation**. Valid bodies:
- `pass` statement
- Ellipsis literal (`...`)

**Invalid**:
```python
interface IFoo:
    def bar(self):
        return 42  # Error: can't have implementation
```

---

## Dependencies

### Internal Sharpy Dependencies

- **`SymbolTable`** ([SymbolTable.md](SymbolTable.md)): Central registry for symbols; provides scoping and lookup
- **`Symbol` Types** ([Symbol.md](Symbol.md)):
  - `TypeSymbol`, `FunctionSymbol`, `VariableSymbol`, `TypeAliasSymbol`, `TypeParameterSymbol`
- **AST Nodes** (from `Sharpy.Compiler.Parser.Ast`):
  - `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`, `FunctionDef`, `VariableDeclaration`, `TypeAlias`
- **Validators**:
  - `OperatorSignatureValidator`: Identifies operator dunders (`__add__`, `__mul__`, etc.)
  - `ProtocolSignatureValidator`: Identifies protocol dunders (`__len__`, `__iter__`, etc.)

### External Dependencies

- **`ICompilerLogger`**: For debug/error logging (see `Sharpy.Compiler.Logging`)
- **`SemanticError`** ([SemanticError.md](SemanticError.md)): Error representation

---

## Design Patterns and Decisions

### 1. Two-Pass Design

**Why?** Solves the forward reference problem common in compilers:
```python
class A:
    def get_b(self) -> B:  # B not yet defined!
        return B()

class B:
    def get_a(self) -> A:
        return A()
```

**Pass 1** registers both `A` and `B` as type symbols. **Pass 2** can then safely resolve `B` in the return type of `get_b`.

### 2. Scope Management

The resolver enters and exits scopes to prevent name collisions:
```csharp
_symbolTable.EnterScope($"class:{classDef.Name}");
// Process members...
_symbolTable.ExitScope();
```

This allows the same method name to exist in different classes without conflict.

### 3. Deferred Type Resolution

Type annotations (e.g., parameter types, return types) are **not** resolved in this pass. They remain as AST nodes (`TypeAnnotation`) and are resolved later by `TypeResolver` during type checking.

**Why?** At this stage, we don't yet know if all referenced types exist or what their generic arguments are.

### 4. Operator/Protocol Method Caching

Methods like `__add__` and `__len__` are stored in dedicated dictionaries (`OperatorMethods`, `ProtocolMethods`) for fast lookup during:
- **Type checking**: Validating operator overloads
- **Code generation**: Emitting C# operator overloads and interface implementations

### 5. Constructor Overloading

Unlike Python (which replaces `__init__` on redefinition), Sharpy tracks **all** `__init__` overloads in `TypeSymbol.Constructors`. This enables C# constructor overloading.

**Symbol Table Trick**: Only the first `__init__` is registered in the symbol table to avoid "duplicate name" errors, but all overloads are preserved in the `Constructors` list.

### 6. Access Level Inference

Sharpy uses **Pythonic naming conventions** to infer C# access modifiers:
- No decorators like `@private` or `@protected`
- Name prefixes (`_`, `__`) determine visibility
- Matches Python developer expectations while targeting .NET

---

## Debugging Tips

### 1. Enable Verbose Logging

Set the logger to `LogLevel.Debug` to see every declaration being processed:
```
Resolving class declaration: MyClass
Resolving method declaration: MyClass.my_method
Registered operator method: MyClass.__add__
```

### 2. Inspect the Symbol Table

After `ResolveDeclarations`, dump the symbol table to see all registered symbols:
```csharp
var myClassSymbol = symbolTable.Lookup("MyClass") as TypeSymbol;
Console.WriteLine($"Methods: {myClassSymbol.Methods.Count}");
Console.WriteLine($"Constructors: {myClassSymbol.Constructors.Count}");
```

### 3. Check for Deferred Processing

Remember that these are **not** processed in NameResolver:
- Import resolution (handled by `ImportResolver`)
- Type annotation resolution (handled by `TypeResolver`)
- Function body validation (handled by `TypeChecker`)

If you don't see expected symbols, verify you're looking at the right pass.

### 4. Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| "Class 'X' is already defined" | Duplicate class name in same scope | Rename one of the classes |
| "Base type 'Y' not found" | Inheritance references undefined type | Ensure base type is imported or defined |
| "Multiple base classes not allowed" | Python-style multiple inheritance | Extract one class to an interface |
| "Structs can only implement interfaces" | Struct inheriting from a class | Use a class instead of struct |

### 5. Tracing Inheritance Resolution

Add breakpoints in `ResolveClassInheritance` to see how base types are linked:
```csharp
typeSymbol.BaseType = baseSymbol;  // Breakpoint here
```

Inspect `typeSymbol.BaseType` and `typeSymbol.Interfaces` after the method completes.

---

## Contribution Guidelines

### When to Modify NameResolver

You should modify this file when adding support for:
1. **New declaration types** (e.g., tagged unions, type variables)
   - Add a new `case` in `ResolveDeclaration`
   - Create a corresponding `Resolve{Type}Declaration` method
2. **New member types** (e.g., properties, events)
   - Add handling in `ResolveClassDeclaration`/`ResolveStructDeclaration`
3. **New inheritance rules** (e.g., sealed classes, covariance)
   - Modify the appropriate `Resolve{Type}Inheritance` method
4. **New symbol attributes** (e.g., `@sealed`, `@partial`)
   - Extract decorator information in the relevant `Resolve*Declaration` method
   - Add fields to the corresponding `Symbol` type

### Testing Changes

After modifying `NameResolver`, test:
1. **Basic declaration**: Can the new construct be parsed and registered?
2. **Redefinition errors**: Does it catch duplicate names?
3. **Inheritance**: If applicable, can it inherit from other types?
4. **Cross-file references**: Does `DefiningFilePath` work correctly?
5. **Scope isolation**: Are symbols properly scoped within classes/modules?

**Test Files**:
- `src/Sharpy.Compiler.Tests/Semantic/NameResolverTests.cs` (if it exists)
- Integration tests in `src/Sharpy.Compiler.Tests/Integration/`

### Code Style Guidelines

1. **Error messages**: Be specific and actionable
   ```csharp
   // Good
   AddError($"Struct '{structDef.Name}' cannot inherit from class '{baseClass.Name}'", ...);

   // Bad
   AddError("Invalid inheritance", ...);
   ```

2. **Logging**: Log at appropriate levels
   - `LogDebug`: Normal declaration processing
   - `LogInfo`: High-level pass information
   - `LogError`: Semantic errors (also added to `_errors`)

3. **Scope names**: Use descriptive scope identifiers
   ```csharp
   _symbolTable.EnterScope($"class:{classDef.Name}");  // Good
   _symbolTable.EnterScope("scope1");                  // Bad
   ```

4. **Separation of concerns**: Keep NameResolver focused on **declaration registration**. Defer:
   - Type annotation resolution → `TypeResolver`
   - Expression type checking → `TypeChecker`
   - Import loading → `ImportResolver`

---

## Cross-References

### Related Walkthrough Documents

- **[SymbolTable.md](SymbolTable.md)**: How symbols are stored and looked up
- **[Symbol.md](Symbol.md)**: Detailed structure of symbol types
- **[TypeResolver.md](TypeResolver.md)**: How type annotations are resolved (pass 2.5)
- **[TypeChecker.md](TypeChecker.md)**: How expressions and statements are type-checked (pass 3)
- **[ImportResolver.md](ImportResolver.md)**: How module imports are resolved

### Specification Documents

- **[`docs/language_specification/identifiers.md`](../../../../../language_specification/identifiers.md)**: Identifier syntax and rules
- **[`docs/language_specification/naming_conventions.md`](../../../../../language_specification/naming_conventions.md)**: Naming conventions for symbols
- **[`docs/language_specification/name_mangling.md`](../../../../../language_specification/name_mangling.md)**: How Sharpy names map to C#

### Related Validators

- **[OperatorSignatureValidator.md](OperatorSignatureValidator.md)**: Validates operator dunder signatures
- **[ProtocolSignatureValidator.md](ProtocolSignatureValidator.md)**: Validates protocol dunder signatures

---

## Summary

`NameResolver` is the foundation of semantic analysis. It:
- Builds the symbol table in two passes (declarations, then inheritance)
- Registers all types, functions, and constants
- Determines method characteristics (static, abstract, virtual, override)
- Validates inheritance rules (C# constraints)
- Caches operator/protocol methods for fast lookup
- Propagates interface methods through inheritance hierarchies

By separating declaration from type resolution, it enables forward references and simplifies the overall semantic analysis pipeline. The next stage, `TypeResolver`, will convert type annotations into resolved semantic types, and `TypeChecker` will validate the actual code logic.
