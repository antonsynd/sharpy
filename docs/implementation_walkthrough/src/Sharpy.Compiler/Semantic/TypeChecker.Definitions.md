# Walkthrough: TypeChecker.Definitions.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs`

---

## Overview

This file contains the **type definition checking** logic for the Sharpy compiler's type checker. It's a partial class file that handles the semantic analysis of top-level type definitions including:

- **Functions** (both standalone and methods)
- **Classes** (including abstract classes and inheritance)
- **Structs** (value types)
- **Interfaces** (contracts)
- **Enums** (enumeration types)

This component is part of the **Semantic Analysis** phase of the compiler pipeline, operating on the Abstract Syntax Tree (AST) produced by the Parser and feeding validated semantic information to the Code Generation phase (RoslynEmitter).

### Key Responsibilities

1. **Type Resolution**: Resolves type annotations for parameters, return types, and fields using the `TypeResolver`
2. **Symbol Registration**: Registers type parameters, parameters, and fields in the symbol table
3. **Validation**: Enforces language rules for decorators (`@override`, `@abstract`, `@static`), parameter ordering, abstract methods, etc.
4. **Scope Management**: Manages entering/exiting scopes for functions, classes, structs, and interfaces
5. **Context Tracking**: Tracks current class, function return type, and method context for validation rules

---

## Class/Type Structure

This is a **partial class** that extends `TypeChecker`. The main class definition is in `TypeChecker.cs` with additional partial files:

- `TypeChecker.cs` - Main class definition, constructor, fields, `CheckStatement()` dispatcher
- **`TypeChecker.Definitions.cs`** - This file (type definitions)
- `TypeChecker.Statements.cs` - Statement checking (if/while/for/try/etc.)
- `TypeChecker.Expressions.cs` - Expression checking
- `TypeChecker.Utilities.cs` - Helper methods and validation utilities

### Important Fields (from TypeChecker.cs)

```csharp
private readonly SymbolTable _symbolTable;              // Symbol lookup and scope management
private readonly SemanticInfo _semanticInfo;            // Stores type info for AST nodes
private readonly TypeResolver _typeResolver;            // Resolves type annotations
private readonly ControlFlowValidator _controlFlowValidator;
private readonly AccessValidator _accessValidator;
private readonly DefaultParameterValidator _defaultParameterValidator;

private SemanticType? _currentFunctionReturnType;      // For return statement validation
private TypeSymbol? _currentClass;                     // Current class being checked
private string? _currentMethodName;                    // For super() validation
private bool _currentMethodIsOverride;                 // @override decorator present
private bool _currentMethodIsDunder;                   // Is dunder method (__init__, __str__, etc.)
```

---

## Key Methods

### 1. `CheckFunction(FunctionDef functionDef)`

**Purpose**: Type checks function definitions, handling both standalone functions and class methods.

**Key Steps**:

1. **Symbol Lookup**: Finds the corresponding `FunctionSymbol` in the symbol table
   - Special case for `__init__`: Matches by line number since multiple overloads may exist

```csharp
if (functionDef.Name == "__init__" && _currentClass != null)
{
    functionSymbol = _currentClass.Constructors
        .FirstOrDefault(c => c.DeclarationLine == functionDef.LineStart);
}
```

2. **Scope Entry & Type Parameter Registration**: Enters function scope **before** resolving parameter/return types
   - This allows generic functions to reference their type parameters

```csharp
_symbolTable.EnterScope($"function:{functionDef.Name}");

foreach (var typeParam in functionDef.TypeParameters)
{
    var typeParamSymbol = new TypeParameterSymbol { ... };
    _symbolTable.Define(typeParamSymbol);
}
```

3. **Return Type Resolution**:
   - `__init__` always returns `Void`
   - Functions without explicit return type annotation default to `Void`
   - Otherwise resolves using `TypeResolver`

4. **Decorator Validation**:
   - **`@override` requirement**: Enforced for dunder methods that override `System.Object` methods
   - **Virtual method shadowing**: Methods that shadow virtual base methods must have `@override`
   - **`@abstract` validation**: Must have ellipsis body (`...`) and be in abstract class

```csharp
bool hasAbstractDecorator = functionDef.Decorators.Any(d => d.Name == "abstract");
bool isInAbstractClass = _currentClass?.IsAbstract == true;
bool hasEllipsisBody = functionDef.Body.Count == 1 && /* ellipsis check */;

bool isAbstractMethod = hasAbstractDecorator || (isInAbstractClass && hasEllipsisBody);
```

5. **Static Method Detection**:
   - Explicitly: `@static` or `@staticmethod` decorator
   - Implicitly: No `self` parameter (valid, no error)

6. **Parameter Validation**:
   - **Ordering**: Non-default parameters cannot follow default parameters
   - **Type annotations**: Required for all parameters except `self`
   - **Default values**: Must be compile-time constants, validated by `DefaultParameterValidator`
   - **Self parameter**: Automatically typed as current class

```csharp
if (i == 0 && param.Name == "self" && _currentClass != null)
{
    paramType = new UserDefinedType { Symbol = _currentClass };
}
```

7. **Body Checking**: Type checks all statements in the function body

8. **Control Flow Validation**: Ensures all code paths return appropriate values

9. **Symbol Update**: Updates function symbol with resolved return type and parameter types

**Important Details**:

- **Context Tracking**: Saves and restores method context (name, override status, dunder status, super init tracking)
- **Line Number Matching**: Uses line numbers to match `__init__` overloads (important for constructor overloading)
- **Idempotent Design**: Safe for potential replays in graph-based execution

---

### 2. `CheckClass(ClassDef classDef)`

**Purpose**: Type checks class definitions including fields, methods, and inheritance validation.

**Key Steps**:

1. **Symbol Lookup**: Retrieves the `TypeSymbol` for the class

2. **Scope Entry & Type Parameter Registration**:
   - Enters class scope
   - Registers generic type parameters (e.g., `class Stack[T]`)

3. **Field Type Resolution**:
   - Resolves field types **before** checking methods
   - This allows methods to reference fields with their proper types

```csharp
for (int i = 0; i < classSymbol.Fields.Count; i++)
{
    var fieldDecl = classDef.Body
        .OfType<VariableDeclaration>()
        .FirstOrDefault(v => v.Name == fieldSymbol.Name);

    if (fieldDecl != null)
    {
        var resolvedType = _typeResolver.ResolveTypeAnnotation(fieldDecl.Type);
        classSymbol.Fields[i] = fieldSymbol with { Type = resolvedType };
    }
}
```

4. **Current Class Context**:
   - Sets `_currentClass` for method type checking
   - Enables `AccessValidator` to validate access modifiers

5. **Member Checking**: Type checks all class members (fields, methods, nested types)

6. **Constructor Validation**: Validates constructor overloads after all members are checked

7. **Cleanup**: Restores previous class context and exits scope

---

### 3. `CheckStruct(StructDef structDef)`

**Purpose**: Type checks struct definitions (value types in C#).

**Key Characteristics**:

- **Nearly identical to `CheckClass`**: Structs behave like classes in Sharpy's type system
- **Same process**: Type parameter registration → field resolution → member checking
- **Additional validation**: Calls `ValidateStructRules()` for struct-specific constraints
- **Value semantics**: Structs are value types in the generated C# code

**Important Note**: The code sets `_currentClass = structSymbol` even for structs. This is intentional - structs and classes share the same validation logic for methods and fields.

---

### 4. `CheckInterface(InterfaceDef interfaceDef)`

**Purpose**: Type checks interface definitions (contracts).

**Key Differences from Classes**:

1. **No Field Resolution**: Interfaces only contain method signatures
2. **Method Type Resolution**: Resolves parameter and return types for interface methods
3. **Symbol Update**: Updates interface's method symbols with resolved types
4. **No Body Checking**: Interface methods are signatures only (body validation happens in implementing classes)

**Method Type Resolution Process**:

```csharp
foreach (var statement in interfaceDef.Body)
{
    if (statement is FunctionDef method)
    {
        // Find method symbol
        var methodIndex = interfaceSymbol.Methods.FindIndex(m => m.Name == method.Name);

        // Resolve return type
        var returnType = _typeResolver.ResolveTypeAnnotation(method.ReturnType);

        // Resolve parameter types
        var updatedParameters = new List<ParameterSymbol>();
        for (int i = 0; i < method.Parameters.Count; i++)
        {
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
            // Special case: 'self' typed as interface type
            if (i == 0 && param.Name == "self")
            {
                paramType = new UserDefinedType { Symbol = interfaceSymbol };
            }
            updatedParameters.Add(new ParameterSymbol { ... });
        }

        // Update method symbol
        interfaceSymbol.Methods[methodIndex] = methodSymbol with
        {
            ReturnType = returnType,
            Parameters = updatedParameters
        };
    }
}
```

**Why No CheckFunction()**: Interfaces don't call `CheckFunction()` because:
- No body to type check
- No control flow validation needed
- Just need signature type resolution

---

### 5. `CheckEnum(EnumDef enumDef)`

**Purpose**: Type checks enum definitions.

**Current Implementation**: Delegates to `ValidateEnumRules()` (defined in `TypeChecker.Utilities.cs`)

**Minimal Processing**: Enums are relatively simple type definitions with specific validation rules for enum members.

---

## Dependencies

### Internal Dependencies

1. **`SymbolTable`** (`Sharpy.Compiler.Semantic`):
   - `EnterScope()` / `ExitScope()`: Manage nested scopes
   - `Define()`: Register symbols (type parameters, parameters, variables)
   - `Lookup()` / `LookupFunction()`: Find symbols
   - `UpdateSymbol()`: Update function symbols with resolved types

2. **`TypeResolver`** (`Sharpy.Compiler.Semantic`):
   - `ResolveTypeAnnotation()`: Convert AST type annotations to `SemanticType`
   - Handles generic types, nullable types, user-defined types, etc.

3. **`DefaultParameterValidator`** (`Sharpy.Compiler.Semantic`):
   - `ValidateFunctionDefaults()`: Ensures default parameters are compile-time constants
   - Prevents mutable defaults (lists, dicts)
   - Validates `None` only used for nullable types

4. **`ControlFlowValidator`** (`Sharpy.Compiler.Semantic`):
   - `ValidateFunction()`: Ensures all code paths return appropriate values
   - Checks for unreachable code

5. **`AccessValidator`** (`Sharpy.Compiler.Semantic`):
   - `EnterClass()` / `ExitClass()`: Track current class for access checking
   - Validates public/private member access

6. **`ProtocolRegistry`** (`Sharpy.Compiler.Semantic`):
   - `IsObjectOverrideDunder()`: Checks if dunder method overrides `System.Object`

### AST Dependencies

From `Sharpy.Compiler.Parser.Ast`:

- **Definition nodes**: `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
- **Type annotations**: Used in parameters, return types, fields
- **Decorators**: `@override`, `@abstract`, `@static`

### Symbols

From `Sharpy.Compiler.Semantic`:

- **`TypeSymbol`**: Represents classes, structs, interfaces
- **`FunctionSymbol`**: Represents functions/methods
- **`VariableSymbol`**: Represents parameters and fields
- **`TypeParameterSymbol`**: Represents generic type parameters (`T`, `U`, etc.)
- **`ParameterSymbol`**: Represents function parameters

---

## Patterns and Design Decisions

### 1. **Multi-Pass Type Resolution**

The type checker follows a specific order:

1. **Enter scope** (critical: before type resolution)
2. **Register type parameters** (allows them to be referenced)
3. **Resolve types** (parameters, return types, fields)
4. **Check body** (validate usage)

This ordering ensures generic type parameters are available when resolving parameter/return types.

### 2. **Symbol Table Updates**

Symbols are created in the `NameResolver` pass with `Unknown` types, then updated in `TypeChecker`:

```csharp
var updatedSymbol = functionSymbol with { ReturnType = returnType };
_symbolTable.UpdateSymbol(updatedSymbol);
```

**Why**: Separation of concerns - name resolution doesn't need type information, type checking adds it later.

### 3. **Context Preservation**

Methods save and restore context variables:

```csharp
var previousClass = _currentClass;
_currentClass = classSymbol;
// ... checking logic ...
_currentClass = previousClass;
```

**Why**: Allows nested type definitions and ensures correct context for validation rules.

### 4. **Line Number Matching for Overloads**

Constructor overloads are matched by line number:

```csharp
functionSymbol = _currentClass.Constructors
    .FirstOrDefault(c => c.DeclarationLine == functionDef.LineStart);
```

**Why**: Multiple `__init__` methods can have the same name, so line number provides unique identification.

### 5. **Implicit Static Methods**

Methods without `self` are treated as static (no error):

```csharp
if (!hasStaticDecorator && !hasSelfParameter)
{
    // This is implicitly a static method - valid, no error
}
```

**Why**: Pythonic convention - methods without `self` are naturally static. Code generator handles this correctly.

### 6. **Abstract Method Detection**

Two ways to define abstract methods:

1. **Explicit**: `@abstract` decorator
2. **Implicit**: Ellipsis body (`...`) in abstract class

```csharp
bool isAbstractMethod = hasAbstractDecorator || (isInAbstractClass && hasEllipsisBody);
```

**Why**: Allows concise abstract class definitions without requiring `@abstract` on every method.

### 7. **Struct/Class Unified Handling**

Structs reuse class validation logic:

```csharp
_currentClass = structSymbol;  // Yes, even for structs!
```

**Why**: Structs and classes share most validation rules. Only storage semantics differ (value vs reference).

---

## Debugging Tips

### 1. **Function Symbol Not Found**

If you see "function symbol not found" errors:

- Check if `NameResolver` ran successfully (it creates the symbols)
- For `__init__`, verify line number matching is working
- Use debugger to inspect `_symbolTable.LookupFunction()`

### 2. **Type Parameters Not Resolving**

If generic type parameters show as `Unknown`:

- Ensure `EnterScope()` is called **before** `ResolveTypeAnnotation()`
- Verify type parameters are registered in the symbol table
- Check that `TypeResolver` can find type parameter symbols

### 3. **Override Decorator Errors**

If seeing unexpected `@override` requirement errors:

- Check `ProtocolRegistry.IsObjectOverrideDunder()` for dunder methods
- Verify `FindMethodInHierarchy()` is correctly searching base classes
- Ensure `_currentMethodIsOverride` is set correctly

### 4. **Default Parameter Validation Failing**

If default parameters are incorrectly rejected:

- Check `DefaultParameterValidator` for detailed validation logic
- Ensure compile-time constant detection is working
- Verify nullable type checks for `None` defaults

### 5. **Scope Issues**

If symbols aren't visible:

- Verify `EnterScope()` and `ExitScope()` are balanced
- Check scope names (e.g., `"function:myFunc"`)
- Ensure cleanup code in `finally` blocks or at method end

### 6. **Missing Type Annotations**

If seeing "requires a type annotation" errors:

- Exception: `self` parameter doesn't need annotation
- All other parameters require explicit types
- Return type can be omitted (defaults to `Void`)

---

## Contribution Guidelines

### What Kinds of Changes Are Made Here

1. **New Language Features**:
   - Adding support for new type definitions (e.g., protocols, type aliases)
   - New decorator types or validation rules

2. **Validation Rules**:
   - Enhanced error messages
   - New semantic rules (e.g., sealed classes, readonly fields)
   - Stricter type safety checks

3. **Performance Optimizations**:
   - Caching type resolutions
   - Reducing symbol table lookups
   - Lazy validation

4. **Bug Fixes**:
   - Incorrect type resolution for edge cases
   - Missing validation rules
   - Scope leaks or incorrect context tracking

### How to Add New Validation

**Example**: Adding validation for a new `@sealed` decorator on classes:

1. Check for decorator in `CheckClass()`:
```csharp
bool isSealed = classDef.Decorators.Any(d => d.Name == "sealed");
```

2. Add validation logic:
```csharp
if (isSealed && classSymbol.IsAbstract)
{
    AddError("Sealed classes cannot be abstract", ...);
}
```

3. Store in symbol:
```csharp
classSymbol.IsSealed = isSealed;
```

4. Update `TypeSymbol` definition to include `IsSealed` property

### Testing Considerations

When modifying this file:

1. **Unit Tests**: Add tests for specific validation rules
2. **Integration Tests**: Test end-to-end compilation scenarios
3. **Error Messages**: Ensure helpful, actionable error messages
4. **Edge Cases**: Test nested types, generic types, inheritance chains

### Code Style

- **Error reporting**: Use `AddError()` with line/column information
- **Logging**: Use `_logger.LogDebug()` for diagnostics
- **Symbol updates**: Use `with` syntax for immutable record updates
- **Context preservation**: Always save/restore context variables

---

## Cross-References

### Related Partial Class Files

This file is part of the `TypeChecker` partial class. See also:

- **[TypeChecker.md](./TypeChecker.md)** - Main class overview and `CheckStatement()` dispatcher
- **[TypeChecker.Statements.md](./TypeChecker.Statements.md)** *(if exists)* - Statement checking logic
- **[TypeChecker.Expressions.md](./TypeChecker.Expressions.md)** *(if exists)* - Expression checking logic
- **[TypeChecker.Utilities.md](./TypeChecker.Utilities.md)** *(if exists)* - Helper methods (`ValidateConstructorOverloads`, `ValidateStructRules`, `ValidateEnumRules`, `FindMethodInHierarchy`, `IsDunderMethod`)

### Related Semantic Analysis Components

- **[NameResolver.md](./NameResolver.md)** *(if exists)* - Creates symbols that this file updates
- **[TypeResolver.md](./TypeResolver.md)** *(if exists)* - Resolves type annotations
- **[SymbolTable.md](./Symbol.md)** *(if exists)* - Symbol storage and lookup
- **[ControlFlowValidator.md](./ControlFlowValidator.md)** *(if exists)* - Validates return paths
- **[DefaultParameterValidator.md](./DefaultParameterValidator.md)** *(if exists)* - Validates default values

### Language Specification References

- **[type_annotations.md](../../../language_specification/type_annotations.md)** - Type annotation syntax
- **[type_hierarchy.md](../../../language_specification/type_hierarchy.md)** - Class/struct/interface relationships
- **[type_narrowing.md](../../../language_specification/type_narrowing.md)** - Type narrowing in conditionals

---

## Summary

`TypeChecker.Definitions.cs` is the **semantic gatekeeper** for type definitions in Sharpy. It ensures:

✅ All type annotations resolve correctly
✅ Decorators are used appropriately
✅ Abstract and override rules are followed
✅ Parameters are properly ordered and typed
✅ Generic type parameters are registered before use
✅ Inheritance and interface contracts are validated

**Key Insight**: This file bridges the gap between the parsed AST (syntactically correct but semantically unknown) and the fully-typed semantic model used by code generation. It's where Python-like syntax meets C#-like type safety.
