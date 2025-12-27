# Walkthrough: NameResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`

---

## Overview

The `NameResolver` is the **first semantic analysis pass** in the Sharpy compiler pipeline. Its primary responsibility is to build the symbol table by discovering and registering all declarations (classes, structs, interfaces, enums, functions, constants) from the parsed Abstract Syntax Tree (AST).

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

### Type Declaration Handlers

#### `ResolveClassDeclaration(ClassDef classDef)`

**Purpose**: Registers a class type and its members in the symbol table.

**Algorithm:**

1. **Check for redefinition**: Ensures no duplicate class names in the current scope
2. **Create TypeSymbol**: Builds a symbol with metadata (name, type parameters, location)
3. **Register in symbol table**: Makes the class discoverable for later lookups
4. **Store for pass 2**: Adds to `_classDefs` for inheritance resolution
5. **Enter class scope**: Creates a nested scope for class members
6. **Process members**: Recursively processes methods and fields
7. **Exit scope**: Returns to parent scope

**Key implementation details:**

```csharp
// Redefinition check
if (_symbolTable.Lookup(classDef.Name, searchParents: false) != null)
{
    AddError($"Class '{classDef.Name}' is already defined", ...);
    return;
}

// Create and register symbol
var typeSymbol = new TypeSymbol
{
    Name = classDef.Name,
    Kind = SymbolKind.Type,
    TypeKind = TypeKind.Class,
    TypeParameters = classDef.TypeParameters,
    // ... location info
};
_symbolTable.Define(typeSymbol);

// Process members in nested scope
_symbolTable.EnterScope($"class:{classDef.Name}");
foreach (var statement in classDef.Body)
{
    if (statement is FunctionDef method)
        ResolveMethodDeclaration(method, typeSymbol);
    else if (statement is VariableDeclaration field)
        ResolveFieldDeclaration(field, typeSymbol);
}
_symbolTable.ExitScope();
```

**Notable behavior**: The `searchParents: false` parameter ensures we only check the current scope, allowing nested classes to shadow outer names (though this is rare in Python-style code).

---

#### `ResolveStructDeclaration(StructDef structDef)`

**Purpose**: Similar to class resolution, but for value types (structs).

**Differences from classes:**
- Sets `TypeKind = TypeKind.Struct` instead of `Class`
- Structs have different inheritance rules (resolved in pass 2)
- Structs are value types in the generated C# code

**Implementation**: Nearly identical to `ResolveClassDeclaration` - this is a good candidate for refactoring to reduce duplication.

---

#### `ResolveInterfaceDeclaration(InterfaceDef interfaceDef)`

**Purpose**: Registers interface types (abstract contracts).

**Unique aspects:**
- Interfaces cannot have fields, only method signatures
- All methods in interfaces are implicitly abstract
- Interfaces can extend multiple other interfaces

**Key difference in member processing:**
```csharp
foreach (var statement in interfaceDef.Body)
{
    if (statement is FunctionDef method)
        ResolveMethodDeclaration(method, typeSymbol);
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
    // ...
};
_symbolTable.Define(typeSymbol);
// No scope entry needed - enum values are constants
```

---

### Function Declaration Handlers

#### `ResolveFunctionDeclaration(FunctionDef functionDef)`

**Purpose**: Registers module-level functions (not methods).

**What it does:**
1. Checks for redefinition
2. Creates `FunctionSymbol` with parameter metadata
3. Registers in symbol table

**Important note**: Types are **not resolved** at this stage - parameters have `Type = SemanticType.Unknown`. Type resolution happens in the `TypeResolver` pass.

```csharp
var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
{
    Name = p.Name,
    Type = SemanticType.Unknown,  // Resolved later
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

**Decorator extraction:**
```csharp
bool isStatic = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");
bool isAbstract = method.Decorators.Any(d => d.Name == "abstract" || d.Name == "abstractmethod");
bool isVirtual = method.Decorators.Any(d => d.Name == "virtual");
bool isOverride = method.Decorators.Any(d => d.Name == "override");
```

**Operator method registration:**
```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);
    if (validationErrors.Count == 0)
    {
        // Add to owningType.OperatorMethods cache
        owningType.OperatorMethods[method.Name] = funcSymbol;
    }
}
```

This enables operator overloading: a class with `__add__` can use the `+` operator.

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
if (baseSymbol.TypeKind == TypeKind.Class || baseSymbol.TypeKind == TypeKind.Interface)
    typeSymbol.BaseType = baseSymbol;
```

2. **Remaining base classes** are treated as interfaces:
```csharp
for (int i = 1; i < classDef.BaseClasses.Count; i++)
{
    var interfaceSymbol = _symbolTable.Lookup(...) as TypeSymbol;
    if (interfaceSymbol.TypeKind == TypeKind.Interface)
        typeSymbol.Interfaces.Add(interfaceSymbol);
}
```

**Error handling**: Validates that base types exist and are the correct kind (class/interface).

---

#### `ResolveStructInheritance(StructDef structDef)`

**Purpose**: Links structs to implemented interfaces.

**Key constraint**: Structs **cannot** inherit from classes (C# restriction):

```csharp
if (interfaceSymbol.TypeKind != TypeKind.Interface)
{
    AddError($"Structs can only implement interfaces, '{baseAnnot.Name}' is not an interface", ...);
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
    if (baseInterfaceSymbol.TypeKind == TypeKind.Interface)
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

**Why this matters**: Sharpy preserves Pythonic naming while generating proper C# access modifiers.

---

#### `AddError(string message, int? line = null, int? column = null)`

**Purpose**: Records semantic errors with source location context.

**Creates a `SemanticError`** and:
1. Adds it to the `_errors` list
2. Logs it via the compiler logger

**Error accumulation strategy**: The name resolver **continues** after errors, collecting as many as possible in one pass. This gives users comprehensive feedback rather than stopping at the first error.

---

## Dependencies

### Core Dependencies

1. **`SymbolTable`** (`Semantic/SymbolTable.cs`)
   - Central storage for all symbols
   - Provides scope management (`EnterScope`, `ExitScope`)
   - Symbol lookup functionality

2. **`Symbol` hierarchy** (`Semantic/Symbol.cs`)
   - `TypeSymbol`: Represents classes, structs, interfaces, enums
   - `FunctionSymbol`: Represents functions and methods
   - `VariableSymbol`: Represents fields and constants
   - `ParameterSymbol`: Represents function parameters

3. **AST types** (`Parser/Ast/`)
   - `Module`, `ClassDef`, `FunctionDef`, `VariableDeclaration`, etc.
   - All AST nodes visited by the name resolver

4. **`ICompilerLogger`** (`Logging/ICompilerLogger.cs`)
   - Diagnostic output during compilation

### External Validators

The name resolver delegates validation to specialized components:

- **`OperatorSignatureValidator`**: Validates operator overload signatures (e.g., `__add__` must return non-void)
- **`ProtocolSignatureValidator`**: Validates protocol method signatures (e.g., `__len__` must return int)

This follows the **Single Responsibility Principle** - the name resolver focuses on symbol registration, not validation logic.

---

## Patterns and Design Decisions

### 1. Two-Pass Resolution

**Why not resolve everything in one pass?**

Consider forward references:
```python
def use_class():
    return MyClass()  # Used before defined

class MyClass:
    pass
```

**Solution**: Pass 1 registers `MyClass`, pass 2 would resolve usage (though expression resolution happens in `TypeChecker`, not here).

### 2. Visitor Pattern (Implicit)

The `ResolveDeclaration` method acts as a dispatcher:

```csharp
switch (statement)
{
    case ClassDef classDef:
        ResolveClassDeclaration(classDef);
        break;
    case FunctionDef functionDef:
        ResolveFunctionDeclaration(functionDef);
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

**Why?** Members are only visible within the class, not in the parent module scope. This prevents name collisions.

### 4. Error Accumulation

Errors are **collected, not thrown**:

```csharp
private readonly List<SemanticError> _errors = new();
public IReadOnlyList<SemanticError> Errors => _errors;
```

**Benefit**: Users see all name resolution errors at once, not just the first one.

### 5. Deferred Type Resolution

Notice that parameter types and return types are set to `SemanticType.Unknown`:

```csharp
Type = SemanticType.Unknown,  // Will be resolved during type checking
```

**Why defer?** Type annotations might reference types not yet declared. The `TypeResolver` pass handles this after all names are registered.

---

## Debugging Tips

### Common Issues and How to Debug Them

#### Issue: "Type 'X' is already defined"

**Cause**: Duplicate declarations in the same scope.

**Debug approach:**
1. Search for the name in the source file
2. Check if it's declared at both module and class level
3. Look at the line/column in the error - the symbol table stores declaration locations

**Code location**:
```csharp
if (_symbolTable.Lookup(classDef.Name, searchParents: false) != null)
{
    AddError($"Class '{classDef.Name}' is already defined", ...);
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

**Code location**:
```csharp
var baseSymbol = _symbolTable.Lookup(baseClassAnnot.Name) as TypeSymbol;
if (baseSymbol == null)
{
    AddError($"Base class '{baseClassAnnot.Name}' not found", ...);
}
```

Inspect `_symbolTable` at this point to see what names are available.

---

#### Issue: Methods not being registered in operator/protocol caches

**Cause**: Validation failures or incorrect dunder method signatures.

**Debug approach:**
1. Check the `_errors` list after name resolution
2. Look for validation errors from `OperatorSignatureValidator` or `ProtocolSignatureValidator`
3. Verify the method signature matches Python semantics

**Code location**:
```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);
    if (validationErrors.Count > 0)
    {
        _errors.AddRange(validationErrors);  // Errors here!
    }
}
```

---

### Useful Debugging Techniques

**1. Log symbol table state:**
```csharp
// After ResolveDeclarations:
foreach (var symbol in _symbolTable.AllSymbols())
    Console.WriteLine($"{symbol.Kind}: {symbol.Name}");
```

**2. Inspect scope stack:**
```csharp
// In SymbolTable:
public int ScopeDepth => _scopeStack.Count;
```

**3. Trace method calls:**
```csharp
_logger.LogDebug($"Resolving class declaration: {classDef.Name}");
```
The logger already has these - increase log level to see full trace.

**4. Check pass ordering:**
```csharp
// Ensure this order in Compiler.cs:
nameResolver.ResolveDeclarations(module);
nameResolver.ResolveInheritance();  // Must be second!
```

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made

#### 1. Adding New Declaration Types

**Example**: Adding support for type aliases:
```python
type MyInt = int
```

**Steps:**
1. Add a case to `ResolveDeclaration`:
```csharp
case TypeAliasDef aliasDef:
    ResolveTypeAlias(aliasDef);
    break;
```

2. Implement the handler:
```csharp
private void ResolveTypeAlias(TypeAliasDef aliasDef)
{
    // Create alias symbol
    // Register in symbol table
}
```

3. Add tests in `Sharpy.Compiler.Tests/Semantic/NameResolverTests.cs`

---

#### 2. Enhancing Validation

**Example**: Adding validation for abstract classes.

**Where to add**:
```csharp
private void ResolveClassDeclaration(ClassDef classDef)
{
    // ... existing code
    
    // New validation:
    if (classDef.HasAbstractMethods && !classDef.IsAbstract)
    {
        AddError($"Class '{classDef.Name}' has abstract methods but is not marked abstract");
    }
}
```

---

#### 3. Refactoring Duplicate Code

**Observation**: `ResolveClassDeclaration`, `ResolveStructDeclaration`, and `ResolveInterfaceDeclaration` have significant overlap.

**Refactoring opportunity**:
```csharp
private void ResolveTypeDeclaration(TypeDef typeDef, TypeKind typeKind)
{
    // Common logic for all type declarations
}
```

**Trade-off**: Improved DRY vs. less explicit code. Evaluate based on team preference.

---

#### 4. Improving Error Messages

**Current**:
```
Semantic error: Class 'MyClass' is already defined
```

**Improved**:
```
Semantic error at line 10: Class 'MyClass' is already defined
Note: Previous definition at line 5, column 7
```

**Implementation**: Store original definition location in error:
```csharp
var existingSymbol = _symbolTable.Lookup(classDef.Name, searchParents: false);
if (existingSymbol != null)
{
    AddError($"Class '{classDef.Name}' is already defined (previous definition at line {existingSymbol.DeclarationLine})", ...);
}
```

---

#### 5. Performance Optimization

**Current**: Linear search through lists for duplicate checks.

**If profiling shows this is slow**, consider:
```csharp
private readonly HashSet<string> _declaredNames = new();

if (_declaredNames.Contains(classDef.Name))
{
    // Duplicate!
}
_declaredNames.Add(classDef.Name);
```

**Note**: Only optimize if measurements show a problem. The current approach is simple and likely fast enough.

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

---

### Code Style

**Follow existing patterns**:
- Private methods start with capital letter (`ResolveClassDeclaration`)
- Use `var` for local variables
- Early return for error cases
- Prefer `switch` expressions for dispatching

**Documentation**:
- XML comments for public methods
- Inline comments only for non-obvious logic

**CRITICAL from project instructions**: 
> Never make tests pass by altering expected values. Fix the implementation.

If a test fails, the bug is in the code, not the test.

---

## Related Files

**Next steps in the pipeline:**
- `TypeResolver.cs` - Resolves type annotations to `SemanticType` instances
- `TypeChecker.cs` - Validates type compatibility and performs type inference

**Validation helpers:**
- `OperatorSignatureValidator.cs` - Validates operator overload signatures
- `ProtocolSignatureValidator.cs` - Validates protocol method signatures

**Symbol storage:**
- `SymbolTable.cs` - Central symbol repository
- `Symbol.cs` - Symbol type definitions

**AST nodes:**
- `Parser/Ast/Statement.cs` - Statement types (ClassDef, FunctionDef, etc.)
- `Parser/Ast/Expression.cs` - Expression types (used in default values)

---

## Summary

The `NameResolver` is your **first line of defense** against semantic errors. It:
- ✅ Registers all declarations in a two-pass process
- ✅ Builds the symbol table that later passes depend on
- ✅ Validates basic constraints (no duplicates, valid inheritance)
- ✅ Accumulates errors without stopping compilation

**Key takeaway**: Name resolution is about **discovery and registration**, not validation or type checking. It answers the question: "What names exist and where are they defined?" Later passes answer: "Are these names used correctly?"

When contributing, remember:
- Tests are mandatory
- Fix bugs in code, not tests
- Follow the two-pass pattern for new constructs that might have forward references
- Delegate complex validation to specialized validators
