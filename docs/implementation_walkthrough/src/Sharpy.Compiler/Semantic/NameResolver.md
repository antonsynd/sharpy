# Walkthrough: NameResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`

---

## Overview

The `NameResolver` is the **first semantic analysis pass** in the Sharpy compiler pipeline. Its primary responsibility is to build the symbol table by discovering and registering all declarations (classes, structs, interfaces, enums, functions, constants, and type aliases) from the parsed Abstract Syntax Tree (AST).

Think of it as the compiler's "table of contents builder" - it scans through your code and creates entries for everything that can be named and referenced later. This happens **before** any type checking or type resolution occurs.

### Role in the Compiler Pipeline

```
Source (.spy) → Lexer → Parser (AST) → [NameResolver] → TypeResolver → TypeChecker → CodeGen
                                           ↓
                                      SymbolTable
```

The `NameResolver` operates in **two distinct passes**:

1. **Pass 1 (Declarations)**: Discovers all types, functions, and constants, creating symbol table entries
2. **Pass 2 (Inheritance)**: Resolves inheritance relationships between classes, structs, and interfaces

This two-pass approach is necessary because a class might inherit from another class that's declared later in the file.

---

## Class Structure

### Main Class: `NameResolver`

```csharp
public class NameResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    private readonly List<ClassDef> _classDefs = new();
    private readonly List<StructDef> _structDefs = new();
    private readonly List<InterfaceDef> _interfaceDefs = new();
}
```

**Key Fields:**

- **`_symbolTable`**: The central repository where all symbols (types, functions, variables) are stored and looked up
- **`_logger`**: Diagnostic logger for tracking the resolution process
- **`_errors`**: Accumulated list of semantic errors encountered during resolution
- **`_classDefs`, `_structDefs`, `_interfaceDefs`**: Temporary storage for type definitions that need second-pass processing for inheritance

---

## Key Methods

### Public API

#### `ResolveDeclarations(Module module)`

**Purpose**: First pass - discovers all top-level declarations in a module.

```csharp
public void ResolveDeclarations(Module module)
{
    _logger.LogInfo("Name resolution pass 1: Declarations in module");

    foreach (var statement in module.Body)
    {
        ResolveDeclaration(statement);
    }
}
```

**What it does:**
- Iterates through every statement in the module's AST
- Dispatches to specialized handlers based on statement type (class, function, enum, etc.)
- Registers symbols in the symbol table **without resolving inheritance**

**When to call it**: This is always the first method called on a `NameResolver` instance after parsing completes.

---

#### `ResolveInheritance()`

**Purpose**: Second pass - resolves base class and interface relationships.

```csharp
public void ResolveInheritance()
{
    _logger.LogInfo("Name resolution pass 2: Inheritance relationships");

    foreach (var classDef in _classDefs)
        ResolveClassInheritance(classDef);

    foreach (var structDef in _structDefs)
        ResolveStructInheritance(structDef);

    foreach (var interfaceDef in _interfaceDefs)
        ResolveInterfaceInheritance(interfaceDef);
}
```

**What it does:**
- Processes all classes, structs, and interfaces that were collected in pass 1
- Links base types and interfaces to their resolved `TypeSymbol` entries
- Validates inheritance rules (e.g., structs can't inherit from classes)

**When to call it**: Always called **after** `ResolveDeclarations`, once all types are registered.

**Why separate passes?**: Consider this valid Sharpy code:
```python
class Child(Parent):
    pass

class Parent:
    pass
```
The child class references `Parent` before it's declared. Pass 1 registers both, pass 2 connects them.

---

### Declaration Dispatcher

#### `ResolveDeclaration(Statement statement)`

**Purpose**: Routes different statement types to their appropriate handlers.

**Handles the following declaration types:**
- `ClassDef` → `ResolveClassDeclaration()`
- `StructDef` → `ResolveStructDeclaration()`
- `InterfaceDef` → `ResolveInterfaceDeclaration()`
- `EnumDef` → `ResolveEnumDeclaration()`
- `FunctionDef` → `ResolveFunctionDeclaration()`
- `VariableDeclaration` (const only) → `ResolveConstantDeclaration()`
- `ImportStatement` → `ResolveImport()`
- `FromImportStatement` → `ResolveFromImport()`
- `TypeAlias` → `ResolveTypeAliasDeclaration()`

**Note**: Regular variable declarations (non-const) are handled by `TypeChecker`, not here. Only constants at module level are registered during name resolution.

---

### Type Declaration Handlers

#### `ResolveClassDeclaration(ClassDef classDef)`

**Purpose**: Registers a class type and its members in the symbol table.

**Algorithm:**

1. **Check for redefinition**: Ensures no duplicate class names in the current scope
2. **Check for @abstract decorator**: Determines if class is abstract
3. **Create TypeSymbol**: Builds a symbol with metadata (name, type parameters, location, isAbstract flag)
4. **Register in symbol table**: Makes the class discoverable for later lookups
5. **Store for pass 2**: Adds to `_classDefs` for inheritance resolution
6. **Enter class scope**: Creates a nested scope for class members
7. **Register type parameters**: Makes generic type parameters available within the class scope
8. **Process members**: Recursively processes methods and fields
9. **Exit scope**: Returns to parent scope

**Key implementation details:**

```csharp
// Check for @abstract decorator
bool isAbstract = classDef.Decorators.Any(d => d.Name == "abstract");

// Create and register symbol
var typeSymbol = new TypeSymbol
{
    Name = classDef.Name,
    Kind = SymbolKind.Type,
    TypeKind = TypeKind.Class,
    AccessLevel = AccessLevel.Public,
    TypeParameters = classDef.TypeParameters,
    IsAbstract = isAbstract,
    // ... location info
};
_symbolTable.Define(typeSymbol);

// Process members in nested scope
_symbolTable.EnterScope($"class:{classDef.Name}");

// Register type parameters so they can be used in method/field types
foreach (var typeParam in classDef.TypeParameters)
{
    var typeParamSymbol = new TypeParameterSymbol
    {
        Name = typeParam.Name,
        Kind = SymbolKind.TypeParameter,
        DeclaringType = typeSymbol,
        // ...
    };
    _symbolTable.Define(typeParamSymbol);
}

foreach (var statement in classDef.Body)
{
    if (statement is FunctionDef method)
        ResolveMethodDeclaration(method, typeSymbol);
    else if (statement is VariableDeclaration field)
        ResolveFieldDeclaration(field, typeSymbol);
}
_symbolTable.ExitScope();
```

**Type parameter registration**: Generic type parameters (e.g., `T` in `class MyList[T]`) are registered in the class's scope so they can be referenced in field types and method signatures.

---

#### `ResolveStructDeclaration(StructDef structDef)`

**Purpose**: Similar to class resolution, but for value types (structs).

**Differences from classes:**
- Sets `TypeKind = TypeKind.Struct` instead of `Class`
- Structs have different inheritance rules (resolved in pass 2 - can only implement interfaces)
- Structs are value types in the generated C# code

**Implementation**: Nearly identical to `ResolveClassDeclaration`, including type parameter registration.

---

#### `ResolveInterfaceDeclaration(InterfaceDef interfaceDef)`

**Purpose**: Registers interface types (abstract contracts).

**Unique aspects:**
- Interfaces cannot have fields, only method signatures
- All methods in interfaces must have no implementation (only `...` or `pass`)
- Interfaces can extend multiple other interfaces
- Validates method bodies using `ValidateInterfaceMethod()`

**Key difference in member processing:**
```csharp
foreach (var statement in interfaceDef.Body)
{
    if (statement is FunctionDef method)
    {
        // Validate that interface methods have no implementation (only ... or pass)
        ValidateInterfaceMethod(method, interfaceDef.Name);
        ResolveMethodDeclaration(method, typeSymbol);
    }
    // Note: No field processing - interfaces don't have fields
}
```

---

#### `ResolveEnumDeclaration(EnumDef enumDef)`

**Purpose**: Registers enumeration types.

**Simplest handler** - enums don't have scopes or complex members:
```csharp
var typeSymbol = new TypeSymbol
{
    Name = enumDef.Name,
    Kind = SymbolKind.Type,
    TypeKind = TypeKind.Enum,
    AccessLevel = AccessLevel.Public,
    // ...
};
_symbolTable.Define(typeSymbol);
// No scope entry needed - enum values are handled elsewhere
```

---

#### `ResolveTypeAliasDeclaration(TypeAlias typeAlias)`

**Purpose**: Registers type aliases (type synonyms).

**Example usage in Sharpy:**
```python
type MyInt = int
type Callback = (int, str) -> bool
```

**What it does:**
1. Checks for redefinition
2. Validates that exactly one of `Type` or `FunctionType` is set
3. Creates a `TypeAliasSymbol` to represent the alias
4. Registers it in the symbol table

**Implementation:**
```csharp
// Validate that exactly one of Type or FunctionType is set
if (typeAlias.Type == null && typeAlias.FunctionType == null)
{
    AddError($"Type alias '{typeAlias.Name}' must have a type", ...);
    return;
}

var aliasSymbol = new TypeAliasSymbol
{
    Name = typeAlias.Name,
    Kind = SymbolKind.TypeAlias,
    AccessLevel = AccessLevel.Public,
    TypeAnnotation = typeAlias.Type,
    FunctionType = typeAlias.FunctionType,
    // ...
};
_symbolTable.Define(aliasSymbol);
```

**Note**: Type aliases support both regular types (`type MyInt = int`) and function types (`type Callback = (int) -> str`).

---

### Function Declaration Handlers

#### `ResolveFunctionDeclaration(FunctionDef functionDef)`

**Purpose**: Registers module-level functions (not methods).

**What it does:**
1. Checks for redefinition (allows shadowing builtins)
2. Creates `FunctionSymbol` with parameter metadata
3. Registers in symbol table

**Important: Allows shadowing builtins**:
```csharp
var existingSymbol = _symbolTable.Lookup(functionDef.Name, searchParents: false);
if (existingSymbol != null)
{
    // Allow shadowing builtins (which have no source location)
    // This matches Python behavior where user code can shadow builtins
    bool isBuiltin = existingSymbol.DeclarationLine == null;
    if (!isBuiltin)
    {
        AddError($"Function '{functionDef.Name}' is already defined", ...);
        return;
    }
    // For builtins, we'll replace the symbol below
}
```

**Deferred type resolution**: Types are **not resolved** at this stage - parameters have `Type = SemanticType.Unknown`. Type resolution happens in the `TypeResolver` pass.

```csharp
var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
{
    Name = p.Name,
    Type = SemanticType.Unknown,  // Resolved later by TypeResolver
    HasDefault = p.DefaultValue != null,
    DefaultValue = p.DefaultValue
}).ToList();
```

---

#### `ResolveMethodDeclaration(FunctionDef method, TypeSymbol owningType)`

**Purpose**: Registers methods within classes/structs/interfaces.

**More complex than function resolution** because it handles:
- **Access level determination** (public, private, protected based on Python naming conventions)
- **Decorator processing** (`@static`, `@abstract`, `@virtual`, `@override`)
- **Static method detection** (methods without `self` parameter)
- **Implicit abstract method detection** (ellipsis body in abstract classes)
- **Constructor overload registration** (multiple `__init__` methods)
- **Operator overload validation** (dunder methods like `__add__`, `__eq__`)
- **Protocol method validation** (special methods like `__len__`, `__str__`)

**Access level logic:**
```csharp
var accessLevel = DetermineAccessLevel(method.Name);
// Python conventions:
// __init__, __str__ (dunders) → public
// __private (double underscore) → private
// _protected (single underscore) → protected
// normal → public
```

**Static method detection - Pythonic approach:**
```csharp
bool hasStaticDecorator = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");

// Primary mechanism: Method is static if it doesn't have 'self' parameter (Pythonic)
// @static decorator is valid but OPTIONAL/redundant
bool hasSelfParameter = method.Parameters.Any(p =>
    string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));

bool isStatic = hasStaticDecorator || !hasSelfParameter;
```

This Pythonic approach means you don't need `@static` if a method doesn't have `self` - the compiler infers it.

**Abstract method detection - implicit and explicit:**
```csharp
// Determine if method is abstract:
// 1. Has @abstract decorator explicitly, OR
// 2. Is in an @abstract class AND has ellipsis body (implicit abstract)
bool hasAbstractDecorator = method.Decorators.Any(d => d.Name == "abstract");
bool hasEllipsisBody = method.Body.Count == 1
    && method.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

bool isAbstract = hasAbstractDecorator || (owningType.IsAbstract && hasEllipsisBody);
```

This allows clean abstract method definitions in abstract classes:
```python
@abstract
class Animal:
    def make_sound(self) -> str:
        ...  # Implicitly abstract - no @abstract decorator needed!
```

**Virtual and override decorators:**
```csharp
bool isVirtual = method.Decorators.Any(d => d.Name == "virtual");
bool isOverride = method.Decorators.Any(d => d.Name == "override");
```

**Constructor overload handling:**

Sharpy supports constructor overloading (multiple `__init__` methods with different signatures):

```csharp
// Register constructors (__init__ methods)
// For constructors, we allow multiple overloads with the same name
if (method.Name == "__init__")
{
    owningType.Constructors.Add(funcSymbol);
    _logger.LogDebug($"Registered constructor overload: {owningType.Name}.__init__ (params: {method.Parameters.Count})");

    // Only register the first __init__ in the symbol table to avoid duplicate name errors
    // All overloads are tracked in the Constructors list
    if (owningType.Constructors.Count == 1)
    {
        _symbolTable.Define(funcSymbol);
    }
}
```

This enables Python-style constructor overloading:
```python
class Point:
    def __init__(self, x: int, y: int):
        ...

    def __init__(self, x: int):  # Overload
        ...
```

**Operator and protocol method overloading:**

Similar to constructors, operator and protocol dunder methods can be overloaded:

```csharp
else if (OperatorSignatureValidator.IsOperatorDunder(method.Name) ||
         ProtocolSignatureValidator.IsProtocolDunder(method.Name))
{
    // For operator and protocol dunder methods, allow multiple overloads
    // Only register the first overload in the symbol table to avoid duplicate name errors
    // All overloads are tracked in the OperatorMethods/ProtocolMethods dictionaries
    if (!_symbolTable.TryDefine(funcSymbol))
    {
        _logger.LogDebug($"Method overload registered (not in symbol table): {owningType.Name}.{method.Name}");
    }
}
```

**Operator method validation and registration:**
```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);

    if (validationErrors.Count > 0)
    {
        // Add all validation errors to the errors list
        _errors.AddRange(validationErrors);
    }
    else
    {
        // Signature is valid, add to operator methods cache
        if (!owningType.OperatorMethods.TryGetValue(method.Name, out var overloads))
        {
            overloads = new List<FunctionSymbol>();
            owningType.OperatorMethods[method.Name] = overloads;
        }
        overloads.Add(funcSymbol);

        _logger.LogDebug($"Registered operator method: {owningType.Name}.{method.Name}");
    }
}
```

This enables operator overloading: a class with `__add__` can use the `+` operator.

**Protocol method validation and registration:**
```csharp
else if (ProtocolSignatureValidator.IsProtocolDunder(method.Name))
{
    var validationErrors = ProtocolSignatureValidator.ValidateDunderSignature(method, owningType);

    if (validationErrors.Count > 0)
    {
        _errors.AddRange(validationErrors);
    }
    else
    {
        // Signature is valid, add to protocol methods cache
        if (!owningType.ProtocolMethods.TryGetValue(method.Name, out var overloads))
        {
            overloads = new List<FunctionSymbol>();
            owningType.ProtocolMethods[method.Name] = overloads;
        }
        overloads.Add(funcSymbol);

        _logger.LogDebug($"Registered protocol method: {owningType.Name}.{method.Name}");
    }
}
```

---

#### `ResolveFieldDeclaration(VariableDeclaration field, TypeSymbol owningType)`

**Purpose**: Registers class/struct fields (instance or static variables).

**Simpler than methods** - just determines access level and creates a `VariableSymbol`:

```csharp
var accessLevel = DetermineAccessLevel(field.Name);
var varSymbol = new VariableSymbol
{
    Name = field.Name,
    Kind = SymbolKind.Variable,
    AccessLevel = accessLevel,
    IsConstant = field.IsConst,
    // ...
};
owningType.Fields.Add(varSymbol);
_symbolTable.Define(varSymbol);
```

---

#### `ResolveConstantDeclaration(VariableDeclaration constDecl)`

**Purpose**: Registers module-level constants.

**Example:**
```python
PI: float = 3.14159
MAX_SIZE: int = 1000
```

**What it does:**
1. Checks for redefinition
2. Creates a `VariableSymbol` with `IsConstant = true`
3. Registers in symbol table at module level

```csharp
var varSymbol = new VariableSymbol
{
    Name = constDecl.Name,
    Kind = SymbolKind.Variable,
    AccessLevel = AccessLevel.Public,
    IsConstant = true,
    // ...
};
_symbolTable.Define(varSymbol);
```

---

### Import Handlers

#### `ResolveImport(ImportStatement import)` and `ResolveFromImport(FromImportStatement fromImport)`

**Current status**: Placeholder implementations that log imports but don't resolve them yet.

```csharp
private void ResolveImport(ImportStatement import)
{
    _logger.LogDebug($"Resolving import: {string.Join(", ", import.Names.Select(n => n.Name))}");
    // TODO: Implement module loading and resolution
}
```

**Future work**: These will delegate to `ImportResolver` or `ModuleResolver` for full module import support.

---

### Inheritance Resolution (Pass 2)

#### `ResolveClassInheritance(ClassDef classDef)`

**Purpose**: Links a class to its base class and implemented interfaces.

**C# inheritance model:**
- **Single inheritance**: Only one base class allowed
- **Multiple interfaces**: Can implement many interfaces

**Algorithm:**

1. **Resolve first base class** as the parent type:
```csharp
var baseClassAnnot = classDef.BaseClasses[0];
var baseSymbol = _symbolTable.Lookup(baseClassAnnot.Name) as TypeSymbol;

if (baseSymbol.TypeKind != TypeKind.Class && baseSymbol.TypeKind != TypeKind.Interface)
{
    AddError($"'{baseClassAnnot.Name}' is not a class or interface", ...);
    return;
}

// Update the type symbol with the base type
typeSymbol.BaseType = baseSymbol;
```

2. **Remaining base classes** are treated as interfaces:
```csharp
for (int i = 1; i < classDef.BaseClasses.Count; i++)
{
    var interfaceAnnot = classDef.BaseClasses[i];
    var interfaceSymbol = _symbolTable.Lookup(interfaceAnnot.Name) as TypeSymbol;

    if (interfaceSymbol.TypeKind != TypeKind.Interface)
    {
        AddError($"'{interfaceAnnot.Name}' is not an interface", ...);
        continue;
    }

    typeSymbol.Interfaces.Add(interfaceSymbol);
}
```

**Python-style multiple inheritance to C# mapping:**
```python
class MyClass(BaseClass, IInterface1, IInterface2):
    pass
```
Maps to:
- `BaseClass` → C# base class
- `IInterface1`, `IInterface2` → C# interfaces

**Error handling**: Validates that base types exist and are the correct kind (class/interface).

---

#### `ResolveStructInheritance(StructDef structDef)`

**Purpose**: Links structs to implemented interfaces.

**Key constraint**: Structs **cannot** inherit from classes (C# restriction):

```csharp
foreach (var baseAnnot in structDef.BaseClasses)
{
    var interfaceSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;

    if (interfaceSymbol.TypeKind != TypeKind.Interface)
    {
        AddError($"Structs can only implement interfaces, '{baseAnnot.Name}' is not an interface", ...);
        continue;
    }

    typeSymbol.Interfaces.Add(interfaceSymbol);
}
```

---

#### `ResolveInterfaceInheritance(InterfaceDef interfaceDef)`

**Purpose**: Links interfaces to parent interfaces they extend.

**Simpler than classes** - interfaces only extend other interfaces:

```csharp
foreach (var baseAnnot in interfaceDef.BaseInterfaces)
{
    var baseInterfaceSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;

    if (baseInterfaceSymbol.TypeKind != TypeKind.Interface)
    {
        AddError($"'{baseAnnot.Name}' is not an interface", ...);
        continue;
    }

    typeSymbol.Interfaces.Add(baseInterfaceSymbol);
}
```

---

### Utility Methods

#### `DetermineAccessLevel(string name)`

**Purpose**: Infers C# access modifiers from Python naming conventions.

**Python → C# mapping:**

| Python Name | Example | C# Access Level |
|-------------|---------|-----------------|
| `__name__` | `__init__`, `__str__` | `public` (special methods) |
| `__name` | `__private_field` | `private` (name mangling) |
| `_name` | `_protected_member` | `protected` |
| `name` | `my_method` | `public` |

**Implementation:**
```csharp
if (name.StartsWith("__") && name.EndsWith("__"))
    return AccessLevel.Public; // Dunder methods are always public
if (name.StartsWith("__") && !name.EndsWith("__"))
    return AccessLevel.Private;
if (name.StartsWith("_"))
    return AccessLevel.Protected;
return AccessLevel.Public;
```

**Why this matters**: Sharpy preserves Pythonic naming while generating proper C# access modifiers. See `docs/language_specification/naming_conventions.md` for more details.

---

#### `ValidateInterfaceMethod(FunctionDef method, string interfaceName)`

**Purpose**: Ensures interface methods have no implementation (only `...` or `pass`).

**Validation rules:**
- Method body must not be empty
- Method body must contain exactly one statement
- That statement must be either `pass` or `...` (ellipsis)

**Implementation:**
```csharp
if (method.Body.Count == 0)
{
    AddError($"Interface method '{method.Name}' in interface '{interfaceName}' must have a body with '...' or 'pass'", ...);
    return;
}

if (method.Body.Count == 1)
{
    var stmt = method.Body[0];

    // Allow: pass
    if (stmt is PassStatement)
        return;

    // Allow: ... (ExpressionStatement containing EllipsisLiteral)
    if (stmt is ExpressionStatement exprStmt && exprStmt.Expression is EllipsisLiteral)
        return;
}

// If we get here, the method has an invalid body (implementation)
AddError($"Interface method '{method.Name}' in interface '{interfaceName}' cannot have an implementation. Use '...' or 'pass' instead", ...);
```

**Valid interface methods:**
```python
interface IDrawable:
    def draw(self) -> None:
        ...  # Valid

    def get_bounds(self) -> tuple[int, int, int, int]:
        pass  # Also valid
```

**Invalid interface method:**
```python
interface IDrawable:
    def draw(self) -> None:
        print("Drawing")  # ERROR: Implementation not allowed
```

---

#### `AddError(string message, int? line = null, int? column = null)`

**Purpose**: Records semantic errors with source location context.

**Creates a `SemanticError`** and:
1. Adds it to the `_errors` list
2. Logs it via the compiler logger

```csharp
var error = new SemanticError(message, line, column);
_errors.Add(error);
_logger.LogError(error.Message, line ?? 0, column ?? 0);
```

**Error accumulation strategy**: The name resolver **continues** after errors, collecting as many as possible in one pass. This gives users comprehensive feedback rather than stopping at the first error.

---

## Dependencies

### Core Dependencies

1. **`SymbolTable`** (`Semantic/SymbolTable.cs`)
   - Central storage for all symbols
   - Provides scope management (`EnterScope`, `ExitScope`)
   - Symbol lookup functionality
   - See: [SymbolTable.md](./SymbolTable.md)

2. **`Symbol` hierarchy** (`Semantic/Symbol.cs`)
   - `TypeSymbol`: Represents classes, structs, interfaces, enums
   - `FunctionSymbol`: Represents functions and methods
   - `VariableSymbol`: Represents fields and constants
   - `ParameterSymbol`: Represents function parameters
   - `TypeParameterSymbol`: Represents generic type parameters
   - `TypeAliasSymbol`: Represents type aliases
   - See: [Symbol.md](./Symbol.md)

3. **AST types** (`Parser/Ast/`)
   - `Module`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
   - `FunctionDef`, `VariableDeclaration`, `TypeAlias`
   - `ImportStatement`, `FromImportStatement`
   - All AST nodes visited by the name resolver

4. **`ICompilerLogger`** (`Logging/ICompilerLogger.cs`)
   - Diagnostic output during compilation

### External Validators

The name resolver delegates validation to specialized components:

- **`OperatorSignatureValidator`**: Validates operator overload signatures (e.g., `__add__` must return non-void)
  - See: [OperatorSignatureValidator.md](./OperatorSignatureValidator.md)
- **`ProtocolSignatureValidator`**: Validates protocol method signatures (e.g., `__len__` must return int)
  - See: [ProtocolSignatureValidator.md](./ProtocolSignatureValidator.md)

This follows the **Single Responsibility Principle** - the name resolver focuses on symbol registration, not validation logic.

---

## Patterns and Design Decisions

### 1. Two-Pass Resolution

**Why not resolve everything in one pass?**

Consider forward references:
```python
class Child(Parent):  # Parent not yet defined
    pass

class Parent:
    pass
```

**Solution**:
- **Pass 1** registers both `Child` and `Parent` in the symbol table
- **Pass 2** connects `Child.BaseType` to the `Parent` symbol

This pattern is essential for mutual dependencies and forward references.

### 2. Visitor Pattern (Implicit)

The `ResolveDeclaration` method acts as a dispatcher using pattern matching:

```csharp
switch (statement)
{
    case ClassDef classDef:
        ResolveClassDeclaration(classDef);
        break;
    case FunctionDef functionDef:
        ResolveFunctionDeclaration(functionDef);
        break;
    case TypeAlias typeAlias:
        ResolveTypeAliasDeclaration(typeAlias);
        break;
    // ...
}
```

**Alternative considered**: Using the Visitor pattern explicitly with `IStatementVisitor`. Current approach is simpler for this use case.

### 3. Scope Management

The name resolver **enters and exits scopes** when processing type bodies:

```csharp
_symbolTable.EnterScope($"class:{classDef.Name}");
// ... process members
_symbolTable.ExitScope();
```

**Why?** Members are only visible within the class, not in the parent module scope. This prevents name collisions and enables proper name resolution in nested contexts.

**Scope naming convention**: Scopes are named with prefixes like `class:`, `struct:`, `interface:` for debugging clarity.

### 4. Error Accumulation

Errors are **collected, not thrown**:

```csharp
private readonly List<SemanticError> _errors = new();
public IReadOnlyList<SemanticError> Errors => _errors;
```

**Benefit**: Users see all name resolution errors at once, not just the first one. This provides better IDE integration and faster development feedback.

### 5. Deferred Type Resolution

Notice that parameter types and return types are set to `SemanticType.Unknown`:

```csharp
Type = SemanticType.Unknown,  // Will be resolved during type checking
```

**Why defer?** Type annotations might reference types not yet declared. The `TypeResolver` pass handles this after all names are registered.

**Example that requires deferred resolution:**
```python
def create_node() -> TreeNode:  # TreeNode not yet defined
    return TreeNode()

class TreeNode:
    pass
```

### 6. Type Parameter Scoping

Generic type parameters are registered in the type's scope:

```csharp
foreach (var typeParam in classDef.TypeParameters)
{
    var typeParamSymbol = new TypeParameterSymbol
    {
        Name = typeParam.Name,
        Kind = SymbolKind.TypeParameter,
        DeclaringType = typeSymbol,
        // ...
    };
    _symbolTable.Define(typeParamSymbol);
}
```

**Why?** This allows field types and method signatures to reference the type parameters:
```python
class MyList[T]:
    items: list[T]  # T is in scope here

    def get(self, index: int) -> T:  # And here
        ...
```

### 7. Constructor and Operator Overloading

**Special handling for methods that support overloading:**
- `__init__` (constructors)
- Operator dunder methods (`__add__`, `__eq__`, etc.)
- Protocol dunder methods (`__len__`, `__str__`, etc.)

**Implementation strategy:**
1. Only the **first overload** is registered in the symbol table (to avoid duplicate name errors)
2. **All overloads** are stored in specialized collections:
   - `TypeSymbol.Constructors` for `__init__` methods
   - `TypeSymbol.OperatorMethods` for operator dunders
   - `TypeSymbol.ProtocolMethods` for protocol dunders

This enables method overload resolution during type checking and code generation.

---

## Debugging Tips

### Common Issues and How to Debug Them

#### Issue: "Type 'X' is already defined"

**Cause**: Duplicate declarations in the same scope.

**Debug approach:**
1. Search for the name in the source file
2. Check if it's declared at both module and class level
3. Look at the line/column in the error - the symbol table stores declaration locations

**Code location** (`src/Sharpy.Compiler/Semantic/NameResolver.cs:111-116`):
```csharp
if (_symbolTable.Lookup(classDef.Name, searchParents: false) != null)
{
    AddError($"Class '{classDef.Name}' is already defined", ...);
    return;
}
```

Set a breakpoint here to see what's already in the symbol table.

---

#### Issue: "Base class 'X' not found"

**Cause**: Inheritance resolution can't find the base type.

**Debug approach:**
1. Verify the base class is spelled correctly in source
2. Check if it's in a different module (imports not yet resolved)
3. Examine `_classDefs` after pass 1 - is it registered?
4. Check if `ResolveInheritance()` was called after `ResolveDeclarations()`

**Code location** (`src/Sharpy.Compiler/Semantic/NameResolver.cs:650-656`):
```csharp
var baseSymbol = _symbolTable.Lookup(baseClassAnnot.Name) as TypeSymbol;
if (baseSymbol == null)
{
    AddError($"Base class '{baseClassAnnot.Name}' not found", ...);
    return;
}
```

Inspect `_symbolTable` at this point to see what names are available.

---

#### Issue: Methods not being registered in operator/protocol caches

**Cause**: Validation failures or incorrect dunder method signatures.

**Debug approach:**
1. Check the `_errors` list after name resolution
2. Look for validation errors from `OperatorSignatureValidator` or `ProtocolSignatureValidator`
3. Verify the method signature matches the expected protocol
4. Check that the method is being added to the correct cache

**Code location** (`src/Sharpy.Compiler/Semantic/NameResolver.cs:438-458`):
```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);
    if (validationErrors.Count > 0)
    {
        _errors.AddRange(validationErrors);  // Errors here!
    }
    else
    {
        // Check if actually added to cache
        owningType.OperatorMethods[method.Name] = funcSymbol;
    }
}
```

---

#### Issue: Constructor overloads not working

**Cause**: Constructors not being added to the `Constructors` list.

**Debug approach:**
1. Set breakpoint at line 408 where `method.Name == "__init__"` is checked
2. Verify all `__init__` methods are being processed
3. Check `owningType.Constructors.Count` to see how many were registered
4. Verify only the first one is in the symbol table

**Code location** (`src/Sharpy.Compiler/Semantic/NameResolver.cs:408-419`):
```csharp
if (method.Name == "__init__")
{
    owningType.Constructors.Add(funcSymbol);
    _logger.LogDebug($"Registered constructor overload: {owningType.Name}.__init__ (params: {method.Parameters.Count})");

    // Only first overload in symbol table
    if (owningType.Constructors.Count == 1)
    {
        _symbolTable.Define(funcSymbol);
    }
}
```

---

#### Issue: Static methods being treated as instance methods

**Cause**: Method has `self` parameter but should be static.

**Debug approach:**
1. Check if method has `self` parameter
2. Verify `@static` decorator is present if needed
3. Check `isStatic` flag after line 367

**Code location** (`src/Sharpy.Compiler/Semantic/NameResolver.cs:360-367`):
```csharp
bool hasStaticDecorator = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");

// Primary mechanism: Method is static if it doesn't have 'self' parameter
bool hasSelfParameter = method.Parameters.Any(p =>
    string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));

bool isStatic = hasStaticDecorator || !hasSelfParameter;
```

**Remember**: If a method doesn't have `self`, it's automatically static (Pythonic behavior).

---

#### Issue: Abstract methods not being marked as abstract

**Cause**: Method in abstract class with `...` body not being detected.

**Debug approach:**
1. Verify class has `@abstract` decorator
2. Check method body is exactly `...` (ellipsis)
3. Verify `owningType.IsAbstract` is true
4. Check `hasEllipsisBody` flag at line 373

**Code location** (`src/Sharpy.Compiler/Semantic/NameResolver.cs:369-376`):
```csharp
// Determine if method is abstract:
// 1. Has @abstract decorator explicitly, OR
// 2. Is in an @abstract class AND has ellipsis body (implicit abstract)
bool hasAbstractDecorator = method.Decorators.Any(d => d.Name == "abstract");
bool hasEllipsisBody = method.Body.Count == 1
    && method.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

bool isAbstract = hasAbstractDecorator || (owningType.IsAbstract && hasEllipsisBody);
```

---

### Useful Debugging Techniques

**1. Enable verbose logging:**
```csharp
// In compiler driver code:
var logger = new ConsoleLogger(LogLevel.Debug);
var resolver = new NameResolver(symbolTable, logger);
```

The logger already has debug traces for each declaration - increase log level to see full resolution flow.

**2. Inspect symbol table state:**
```csharp
// After ResolveDeclarations in debugger watch window:
_symbolTable.CurrentScope.Symbols
```

**3. Check error accumulation:**
```csharp
// After both passes:
Console.WriteLine($"Name resolution errors: {resolver.Errors.Count}");
foreach (var error in resolver.Errors)
    Console.WriteLine($"  {error}");
```

**4. Verify pass ordering:**
```csharp
// Ensure this order in Compiler.cs:
nameResolver.ResolveDeclarations(module);  // MUST be first
nameResolver.ResolveInheritance();         // MUST be second
```

**5. Inspect type symbol metadata:**
```csharp
// In debugger, after resolution:
var classSymbol = _symbolTable.Lookup("MyClass") as TypeSymbol;
// Check: Methods, Fields, Constructors, OperatorMethods, ProtocolMethods
```

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made

#### 1. Adding New Declaration Types

**Example**: Adding support for property declarations (if not handled elsewhere):
```python
@property
def value(self) -> int:
    ...
```

**Steps:**
1. Add a case to `ResolveDeclaration`:
```csharp
case PropertyDef propertyDef:
    ResolvePropertyDeclaration(propertyDef);
    break;
```

2. Implement the handler:
```csharp
private void ResolvePropertyDeclaration(PropertyDef propertyDef)
{
    // Create property symbol
    // Register in symbol table
    // Add to owning type's Properties list
}
```

3. Add tests in `Sharpy.Compiler.Tests/Semantic/NameResolverTests.cs`

---

#### 2. Enhancing Validation

**Example**: Adding validation for sealed classes that have abstract methods.

**Where to add** (`src/Sharpy.Compiler/Semantic/NameResolver.cs:106-170`):
```csharp
private void ResolveClassDeclaration(ClassDef classDef)
{
    // ... existing code

    // New validation:
    bool isSealed = classDef.Decorators.Any(d => d.Name == "sealed");
    if (isSealed && isAbstract)
    {
        AddError($"Class '{classDef.Name}' cannot be both sealed and abstract",
            classDef.LineStart, classDef.ColumnStart);
    }
}
```

---

#### 3. Refactoring Duplicate Code

**Observation**: `ResolveClassDeclaration`, `ResolveStructDeclaration`, and `ResolveInterfaceDeclaration` have significant overlap (~80% similar code).

**Refactoring opportunity**:
```csharp
private void ResolveTypeDeclaration(
    string name,
    TypeKind typeKind,
    List<TypeParameter> typeParameters,
    List<Statement> body,
    bool isAbstract,
    int lineStart,
    int columnStart,
    Action<FunctionDef, TypeSymbol>? validateMember = null)
{
    // Common logic for all type declarations
    // Use validateMember for interface-specific validation
}
```

**Trade-off**: Improved DRY vs. less explicit code. Current approach favors clarity and ease of understanding for newcomers. Evaluate based on team preference and maintenance burden.

---

#### 4. Improving Error Messages

**Current**:
```
Semantic error: Class 'MyClass' is already defined
```

**Improved**:
```
Semantic error at line 10, column 7: Class 'MyClass' is already defined
Note: Previous definition at line 5, column 7
```

**Implementation**:
```csharp
var existingSymbol = _symbolTable.Lookup(classDef.Name, searchParents: false);
if (existingSymbol != null)
{
    var prevLoc = existingSymbol.DeclarationLine != null
        ? $" (previous definition at line {existingSymbol.DeclarationLine}, column {existingSymbol.DeclarationColumn})"
        : "";
    AddError($"Class '{classDef.Name}' is already defined{prevLoc}",
        classDef.LineStart, classDef.ColumnStart);
}
```

---

#### 5. Implementing Import Resolution

**Current**: Placeholder implementations that only log imports.

**Future work**:
```csharp
private void ResolveImport(ImportStatement import)
{
    _logger.LogDebug($"Resolving import: {string.Join(", ", import.Names.Select(n => n.Name))}");

    // Delegate to ImportResolver or ModuleResolver
    foreach (var name in import.Names)
    {
        var moduleSymbol = _moduleResolver.ResolveModule(name.Name);
        if (moduleSymbol != null)
        {
            // Register imported names in current scope
            _symbolTable.DefineImport(name.AsName ?? name.Name, moduleSymbol);
        }
        else
        {
            AddError($"Module '{name.Name}' not found", import.LineStart, import.ColumnStart);
        }
    }
}
```

See [ImportResolver.md](./ImportResolver.md) and [ModuleResolver.md](./ModuleResolver.md) for more details.

---

#### 6. Performance Optimization

**Current**: Symbol table lookups are reasonably efficient (hash-based).

**If profiling shows this is slow**, consider:
- Caching frequently looked-up symbols
- Pre-allocating collections with known capacity
- Parallel processing of independent declarations

**Note**: Only optimize if measurements show a problem. The current approach is simple and likely fast enough for typical codebases.

---

### Testing Guidelines

**Critical**: Every change to `NameResolver` must have tests.

**Test file location**: `src/Sharpy.Compiler.Tests/Semantic/NameResolverTests.cs`

**Test pattern**:
```csharp
[Fact]
public void ResolveClassDeclaration_DuplicateClass_ReportsError()
{
    var source = @"
class MyClass:
    pass

class MyClass:  # Duplicate
    pass
";
    var module = ParseModule(source);
    var symbolTable = new SymbolTable(new BuiltinRegistry());
    var resolver = new NameResolver(symbolTable);

    resolver.ResolveDeclarations(module);

    Assert.Single(resolver.Errors);
    Assert.Contains("already defined", resolver.Errors[0].Message);
}
```

**Test categories to cover**:
- ✅ Happy path (valid declarations)
- ✅ Error cases (duplicates, invalid inheritance)
- ✅ Edge cases (empty classes, forward references)
- ✅ Integration with symbol table (correct scoping)
- ✅ Decorator handling (`@abstract`, `@static`, etc.)
- ✅ Constructor overloading
- ✅ Operator and protocol method registration
- ✅ Type parameter scoping

**New features must include tests for:**
- Basic functionality
- Error handling
- Integration with existing features
- Edge cases

---

### Code Style

**Follow existing patterns**:
- Private methods start with uppercase letter (`ResolveClassDeclaration`)
- Use `var` for local variables
- Early return for error cases
- Prefer pattern matching for type dispatching
- Use LINQ for collection operations where it improves readability

**Documentation**:
- XML comments (`///`) for public methods
- Inline comments for non-obvious logic
- Comment the "why", not the "what"

**Example of good comments**:
```csharp
// Primary mechanism: Method is static if it doesn't have 'self' parameter (Pythonic)
// @static decorator is valid but OPTIONAL/redundant
bool hasSelfParameter = method.Parameters.Any(p =>
    string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));
```

**CRITICAL from project guidelines**:
> Never make tests pass by altering expected values. Fix the implementation.

If a test fails, the bug is in the code, not the test.

---

## Cross-References

**Related documentation files:**

**Semantic Analysis Pipeline:**
- [SymbolTable.md](./SymbolTable.md) - Symbol storage and scoping
- [Symbol.md](./Symbol.md) - Symbol type definitions
- [TypeResolver.md](./TypeResolver.md) - Resolves type annotations (runs after NameResolver)
- [TypeChecker.md](./TypeChecker.md) - Validates types and performs inference (runs after TypeResolver)
- [SemanticError.md](./SemanticError.md) - Error representation

**Validators:**
- [OperatorSignatureValidator.md](./OperatorSignatureValidator.md) - Validates operator overload signatures
- [ProtocolSignatureValidator.md](./ProtocolSignatureValidator.md) - Validates protocol method signatures

**Import System:**
- [ImportResolver.md](./ImportResolver.md) - Resolves import statements
- [ModuleResolver.md](./ModuleResolver.md) - Module discovery and loading

**Language Specifications:**
- `docs/language_specification/identifiers.md` - Identifier naming rules
- `docs/language_specification/naming_conventions.md` - Python-style naming conventions
- `docs/language_specification/name_mangling.md` - Name mangling for private members

**AST Documentation:**
- `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Ast/Statement.md` - Statement AST nodes
- `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Ast/Expression.md` - Expression AST nodes

---

## Summary

The `NameResolver` is your **first line of defense** against semantic errors. It:
- ✅ Registers all declarations in a two-pass process
- ✅ Builds the symbol table that later passes depend on
- ✅ Handles complex scenarios: constructor overloading, operator methods, type parameters
- ✅ Validates basic constraints (no duplicates, valid inheritance, interface method bodies)
- ✅ Accumulates errors without stopping compilation
- ✅ Supports Python-style naming conventions and semantics

**Key takeaway**: Name resolution is about **discovery and registration**, not validation or type checking. It answers the question: "What names exist and where are they defined?" Later passes answer: "Are these names used correctly?"

**The two-pass strategy:**
1. **Pass 1**: Register all declarations (types, functions, constants)
2. **Pass 2**: Resolve inheritance relationships

This enables forward references and mutual dependencies.

**Important features:**
- **Constructor overloading**: Multiple `__init__` methods per class
- **Operator overloading**: Dunder methods like `__add__`, `__eq__`
- **Pythonic static detection**: No `self` parameter → static method
- **Implicit abstract methods**: `...` body in `@abstract` class → abstract method
- **Type parameter scoping**: Generics work correctly in method/field types
- **Type aliases**: Support for `type MyInt = int` and function type aliases

When contributing, remember:
- Tests are mandatory for all changes
- Fix bugs in code, not tests
- Follow the two-pass pattern for constructs that might have forward references
- Delegate complex validation to specialized validators
- Maintain Pythonic semantics while generating correct C#
