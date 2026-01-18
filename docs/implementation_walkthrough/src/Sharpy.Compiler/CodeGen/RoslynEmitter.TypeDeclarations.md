# Walkthrough: RoslynEmitter.TypeDeclarations.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`

---

## Overview

This file is part of the **RoslynEmitter** partial class and handles the generation of **type declarations** from Sharpy AST nodes into C# Roslyn syntax trees. It's responsible for transforming Python-like type declarations (functions, classes, structs, interfaces, and enums) into their C# equivalents.

**Role in the Compiler Pipeline:**
- **Input**: Typed AST nodes (FunctionDef, ClassDef, StructDef, InterfaceDef, EnumDef) from Semantic Analysis
- **Output**: Roslyn SyntaxNodes representing C# type declarations
- **Responsibility**: Bridge the gap between Python-style declarations and C#'s type system

This is one of several partial class files that together implement the complete RoslynEmitter. TypeDeclarations specifically handles **top-level type constructs**, while other partial files handle expressions, statements, class members, etc.

---

## Class Structure

This file extends the `RoslynEmitter` partial class and contains methods organized into two main categories:

1. **Function Declaration Generation** (lines 16-208)
   - `GenerateFunctionDeclaration()` - Main entry point for function generation
   - `GenerateParameter()` - Parameter conversion (including variadic `*args`)
   - `GenerateModifiersFromDecorators()` - Decorator → C# modifier mapping
   - `GenerateXmlDocComment()` - Docstring → XML documentation

2. **Type Declaration Generation** (lines 210-764, marked with `#region`)
   - `GenerateClassDeclaration()` - Class generation with inheritance
   - `GenerateStructDeclaration()` - Value type generation
   - `GenerateInterfaceDeclaration()` - Interface contracts
   - `GenerateEnumDeclaration()` - Enum handling (both integer and string enums)
   - Supporting methods for constraints, abstract stubs, and modifiers

---

## Key Methods

### 1. GenerateFunctionDeclaration() - Lines 16-78

**Purpose**: Converts a Sharpy function definition into a C# method declaration.

**Key Steps**:

```csharp
private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
```

1. **Scope Management** (lines 18-21): Clears tracking dictionaries for the new function scope:
   - `_declaredVariables` - Track variables declared in this function
   - `_variableVersions` - Track variable redefinition versions (for SSA-style renaming)
   - `_constVariables` - Track const declarations

2. **Name Mangling** (lines 23-27): Transforms Python naming to C# conventions
   - Special case: `main` → `Main` if entry point, `MainFunc` otherwise
   - Uses `NameMangler.Transform()` with `NameContext.Method` context

3. **Return Type Resolution** (lines 29-32):
   - If annotated: Use `TypeMapper` to convert Sharpy type → C# type
   - If not annotated: Default to `void`

4. **Modifier Generation** (lines 34-35): Process decorators like `@static`, `@private`, etc.

5. **Parameter Processing** (lines 38-50):
   - Convert each parameter with type annotations
   - Track parameters as declared variables (important for scoping!)
   - Initialize version tracking for parameters (enables assignments to params)

6. **Body Generation** (line 53): Transform function body statements to C# statements

7. **Generic Support** (lines 61-69): Handle type parameters and constraints if present

8. **Documentation** (lines 72-75): Convert Python docstrings to XML doc comments

**Design Decision**: The function clears scope-tracking state at the start, ensuring each function is independent. This is critical for correctness in variable scoping.

---

### 2. GenerateParameter() - Lines 80-113

**Purpose**: Converts a Sharpy parameter to a C# parameter, handling special cases.

**Key Features**:

- **Type Mapping**: Uses `TypeMapper` to convert type annotations, defaults to `object` if untyped
- **Variadic Parameters** (lines 89-103):
  ```csharp
  // For *args parameters in Sharpy
  if (param.IsVariadic)
  {
      paramType = ArrayType(paramType)  // Wrap in array
      parameter = parameter.WithModifiers(Token(SyntaxKind.ParamsKeyword))  // Add 'params'
  }
  ```
  This allows Python-style `def foo(*args)` to become C# `void Foo(params object[] args)`

- **Default Values** (lines 106-110): Translates Sharpy default values to C# default expressions

---

### 3. GenerateModifiersFromDecorators() - Lines 115-182

**Purpose**: Maps Sharpy decorators to C# access/behavior modifiers.

**Decorator Mapping**:

| Sharpy Decorator | C# Modifier |
|------------------|-------------|
| `@private` | `private` |
| `@protected` | `protected` |
| `@internal` | `internal` |
| `@public` | `public` |
| `@staticmethod`, `@static` | `static` |
| `@abstract` | `abstract` |
| `@virtual` | `virtual` |
| `@override` | `override` |

**Important Behavior** (lines 144-179):
- Defaults to `public` if no access modifier specified (C# class default is private, but Sharpy uses public)
- **Automatically adds `static`** for module-level functions (lines 171-179)
  - Unless the function already has instance modifiers (`abstract`, `virtual`, `override`)

This automatic `static` injection is crucial because Sharpy's module-level functions are placed in a static `Exports` class.

---

### 4. GenerateClassDeclaration() - Lines 212-291

**Purpose**: Converts a Sharpy class into a C# class declaration.

**Workflow**:

1. **Tracking** (line 215): Add class name to `_classNames` for later instantiation detection
2. **Abstract Class Context** (lines 218-219): Set `_isInAbstractClass` flag for method processing
3. **Name Transformation**: Use `NameContext.Type` for PascalCase conversion
4. **Generic Support** (lines 232-240): Handle type parameters with constraints
5. **Inheritance** (lines 243-249): Map base classes/interfaces via `TypeMapper`
6. **Member Generation** (line 252): Delegate to `GenerateClassMembers()` (in ClassMembers.cs)
7. **Abstract Interface Stubs** (lines 255-277):
   ```csharp
   if (_isInAbstractClass && classDef.BaseClasses.Count > 0)
   {
       var interfaceMethods = CollectInterfaceMethodDefs(classDef.BaseClasses);
       var definedMethods = GetDefinedMethodNames(classDef.Body);
       // Generate abstract stubs for missing interface methods
   }
   ```
   **Why?** In C#, abstract classes implementing interfaces can provide abstract stubs instead of concrete implementations. This auto-generates those stubs.

8. **Cleanup** (line 288): Restore previous `_isInAbstractClass` state

---

### 5. CollectInterfaceMethodDefs() - Lines 297-348

**Purpose**: Recursively collect all method signatures from interfaces (including inherited ones).

**Algorithm**:
- Uses **visited tracking** to avoid infinite recursion on circular interface inheritance
- Uses **seenMethods** to avoid duplicate method definitions
- Recursively walks base interfaces via `_interfaceDefinitions` dictionary
- Returns a flattened list of all interface method signatures

**Why needed?** When an abstract class implements an interface but doesn't implement all methods, C# requires abstract stub declarations. This method finds which methods need stubs.

---

### 6. GenerateAbstractMethodStub() - Lines 371-393

**Purpose**: Create an abstract method declaration for unimplemented interface methods.

**Generated Code**:
```csharp
public abstract ReturnType MethodName(params);  // Note the semicolon!
```

**Important Details**:
- Skips `self` parameter (line 382) - C# doesn't have explicit `self`
- Always `public abstract` modifiers
- Ends with semicolon token (no body) - line 392

---

### 7. GenerateEnumDeclaration() - Lines 533-548

**Purpose**: Route enum generation based on whether it's an integer or string enum.

**Key Design**: Sharpy supports **two kinds of enums**:

1. **Integer Enums**: Standard C# enums
   ```python
   enum Status:
       PENDING = 0
       ACTIVE = 1
   ```
   → C# `enum Status { Pending = 0, Active = 1 }`

2. **String Enums**: Generated as sealed classes with static readonly fields
   ```python
   enum LogLevel:
       DEBUG = "debug"
       INFO = "info"
   ```
   → C# sealed class with `public static readonly string` fields

**Detection** (lines 553-564): Enum is "string" if any member has a `StringLiteral` value.

**Tracking**: String enum names are added to `_stringEnumNames` (line 541) for proper member access generation elsewhere in the compiler.

---

### 8. GenerateStringEnumClass() - Lines 606-671

**Purpose**: Generate a sealed class pattern for string enums (since C# enums must be integral types).

**Generated Pattern**:
```csharp
public sealed class LogLevel
{
    public static readonly string DEBUG = "debug";
    public static readonly string INFO = "info";
}
```

**Field Name Transformation** (line 625):
- Uses `NameContext.Constant` for field names (UPPER_CASE → UPPER_CASE)
- This preserves C# constant naming conventions

**Default Values** (lines 628-640):
- If member has explicit string value: use it
- If no value: use the original member name as the string value

---

### 9. GenerateConstraintClauses() - Lines 488-531

**Purpose**: Convert Sharpy generic constraints to C# `where` clauses.

**Constraint Types Supported**:

| Sharpy Constraint | C# Syntax |
|-------------------|-----------|
| `ClassConstraint` | `where T : class` |
| `StructConstraint` | `where T : struct` |
| `TypeConstraint` | `where T : IInterface` |
| `NewConstraint` | `where T : new()` |

**Critical Detail** (lines 501-509): Constraints are **ordered** according to C# rules:
1. `class`/`struct` constraints first (order 0)
2. Type constraints second (order 1)
3. `new()` constraint last (order 2)

This ordering is **required by C#** - violating it causes compilation errors.

---

### 10. TransformEnumMemberName() - Lines 691-707

**Purpose**: Convert Python-style enum member names to C# PascalCase.

**Examples**:
- `RED` → `Red`
- `DARK_BLUE` → `DarkBlue`
- `very_long_name` → `VeryLongName`

**Special Case** (lines 697-698): Backtick-escaped names are preserved literally
- `` `CustomName` `` → `CustomName`

**Algorithm** (lines 701-706):
1. Split by underscores
2. Capitalize first letter of each part
3. Lowercase the rest
4. Join together

**Why not use NameMangler?** The comment on line 676 explains: `NameMangler.ToPascalCase` preserves all-caps words as-is, but enum members need proper capitalization.

---

## Dependencies

### Internal Dependencies (imported from Sharpy)

1. **Sharpy.Compiler.Parser.Ast**: AST node types
   - `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
   - `Parameter`, `Decorator`, `TypeParameterDef`
   - `Statement`, `Expression` base types
   - Constraint types: `ClassConstraint`, `StructConstraint`, `TypeConstraint`, `NewConstraint`

2. **Sharpy.Compiler.Semantic**: Semantic analysis results
   - `TypeSymbol`, `ModuleSymbol` (referenced in main RoslynEmitter.cs)
   - `SemanticInfo` (used for type resolution)

### External Dependencies (Roslyn)

1. **Microsoft.CodeAnalysis.CSharp.Syntax**: All the `*Syntax` types
   - `MethodDeclarationSyntax`, `ClassDeclarationSyntax`, etc.

2. **Microsoft.CodeAnalysis.CSharp.SyntaxFactory**: Factory methods (imported as `static`)
   - `MethodDeclaration()`, `ClassDeclaration()`, `Token()`, etc.

### Cross-Partial Dependencies

This file calls methods defined in other RoslynEmitter partial files:

- `GenerateExpression()` - from **RoslynEmitter.Expressions.cs** (line 108, 631, 684)
- `GenerateBodyStatement()` - from **RoslynEmitter.Statements.cs** (line 53)
- `GenerateClassMembers()` - from **RoslynEmitter.ClassMembers.cs** (lines 252, 431)
- `GenerateInterfaceMembers()` - from **RoslynEmitter.ClassMembers.cs** (line 476)

### Shared State (from RoslynEmitter.cs)

This file uses numerous private fields defined in the main partial class:

**Context and Mapping**:
- `_context` - CodeGenContext with symbol tables and module info
- `_typeMapper` - TypeMapper for converting Sharpy types to C# types

**Tracking Sets** (populated by this file):
- `_classNames` - Track defined classes (line 215)
- `_structNames` - Track defined structs (line 398)
- `_stringEnumNames` - Track string enums (line 541)
- `_interfaceDefinitions` - Interface definitions for stub generation

**Scoping State** (managed by GenerateFunctionDeclaration):
- `_declaredVariables` - Variables in current scope
- `_variableVersions` - SSA-style versioning for redefinitions
- `_constVariables` - Const declarations in current scope

**Context Flags**:
- `_isInAbstractClass` - Whether currently generating an abstract class

---

## Patterns and Design Decisions

### 1. Partial Class Organization

**Decision**: Split RoslynEmitter into multiple files by responsibility.

**Rationale**: The full emitter would be several thousand lines. Splitting by concern (TypeDeclarations, Expressions, Statements, etc.) improves maintainability.

**Pattern**: Each partial file focuses on a specific AST node category:
- TypeDeclarations.cs ← You are here
- Expressions.cs - Expression evaluation
- Statements.cs - Statement generation
- ClassMembers.cs - Class/interface member generation
- etc.

### 2. Visitor Pattern (Implicit)

While not using explicit Visitor interfaces, the methods follow a **visitor-like pattern**:
- Each AST node type has a corresponding `Generate*` method
- Methods dispatch to appropriate generators based on node type
- Example: `GenerateEnumDeclaration()` dispatches to either `GenerateIntegerEnum()` or `GenerateStringEnumClass()`

### 3. Name Mangling Strategy

**Critical Pattern**: Different contexts use different casing:

| Context | Sharpy Name | C# Name | Example |
|---------|-------------|---------|---------|
| Type | `my_class` | `MyClass` | `NameContext.Type` |
| Method | `get_value` | `GetValue` | `NameContext.Method` |
| Parameter | `user_id` | `userId` | `NameContext.Parameter` |
| Constant | `MAX_SIZE` | `MAX_SIZE` | `NameContext.Constant` |
| Interface | `i_logger` | `ILogger` | `NameContext.Interface` |

**Why?** Sharpy uses Python's snake_case conventions, but generated C# should follow .NET conventions for interop.

### 4. Decorator-Based Modifiers

**Pattern**: Use decorators for access control and behavior modification.

**Rationale**: Python uses decorators, C# uses modifiers. The mapping is straightforward:
- `@public def foo()` → `public void Foo()`
- `@staticmethod` → `static`
- `@abstract` → `abstract`

**Default Behavior**: Module-level functions get `static` unless they have instance modifiers.

### 5. String Enum Emulation

**Pattern**: Since C# enums must be integral types, string enums become sealed classes.

**Implementation**:
```csharp
// Sharpy:
enum LogLevel:
    DEBUG = "debug"

// Generated C#:
public sealed class LogLevel
{
    public static readonly string DEBUG = "debug";
}
```

**Rationale**: This provides compile-time checking while supporting string values. The `sealed` modifier prevents inheritance, making it behave more like an enum.

### 6. Abstract Interface Stub Generation

**Pattern**: Auto-generate abstract method stubs when an abstract class implements an interface.

**Why?** C# allows abstract classes to defer interface implementation to derived classes. The compiler automatically generates stubs for unimplemented interface methods, saving boilerplate.

**Example**:
```python
# Sharpy
@abstract
class BaseHandler(IHandler):
    pass  # Doesn't implement IHandler methods

# Generated C#
public abstract class BaseHandler : IHandler
{
    public abstract void Handle();  // Auto-generated stub
}
```

### 7. Scope Isolation

**Pattern**: Each `GenerateFunctionDeclaration()` clears scope-tracking dictionaries.

**Rationale**: Functions are independent scopes. Clearing ensures variables from one function don't leak into another.

**Implementation** (lines 18-21):
```csharp
_declaredVariables.Clear();
_variableVersions.Clear();
_constVariables.Clear();
```

---

## Debugging Tips

### Problem: Generated method has wrong modifiers

**Check**:
1. The decorators on the Sharpy function (e.g., `@static`, `@public`)
2. `GenerateModifiersFromDecorators()` - is the decorator name being recognized?
3. Whether the function is module-level (should auto-add `static`)

**Common Issue**: Forgetting that module-level functions automatically become `static` (lines 171-179).

---

### Problem: Enum member has wrong casing

**For Integer Enums**:
- Check `TransformEnumMemberName()` (lines 691-707)
- Verify the underscore-splitting logic

**For String Enums**:
- Check `GenerateStringEnumClass()` (lines 606-671)
- Field names use `NameContext.Constant` (line 625)
- Remember: String enum fields preserve UPPER_CASE

---

### Problem: Generic constraints in wrong order

**Symptom**: C# compiler error about constraint ordering.

**Check**: `GenerateConstraintClauses()` lines 501-509. The ordering MUST be:
1. `class`/`struct` first
2. Type constraints second
3. `new()` last

**Fix**: Ensure the `OrderBy` clause (lines 501-509) is working correctly.

---

### Problem: Abstract class missing interface implementations

**Check**:
1. Is `_isInAbstractClass` being set correctly? (line 219)
2. Is `_interfaceDefinitions` populated? (Check where interfaces are registered)
3. `CollectInterfaceMethodDefs()` - is it finding the interface methods?
4. `GetDefinedMethodNames()` - is it correctly identifying already-implemented methods?

**Debugging**: Add breakpoints in lines 255-277 to see which stubs are being generated.

---

### Problem: Parameter tracking issues in function body

**Symptom**: Parameters aren't recognized as declared variables, causing scope errors.

**Check**: Lines 43-50 in `GenerateFunctionDeclaration()`. Parameters must be:
1. Added to `_declaredVariables` (line 46)
2. Added to `_variableVersions` with initial version 0 (line 49)

**Why?** The body generation code checks these dictionaries to determine if a name is a parameter vs. a variable.

---

### Problem: Variadic parameters not working

**Check**:
1. Is `param.IsVariadic` being set correctly by the parser?
2. `GenerateParameter()` lines 89-103 - is the array wrapping happening?
3. Is the `params` keyword being added? (line 102)

**Expected Output**: `void Foo(params object[] args)`

---

### Problem: Docstrings not appearing in generated code

**Check**:
1. Is `func.DocString` non-empty?
2. `GenerateXmlDocComment()` lines 184-208 - is it processing the string correctly?
3. Is the trivia being attached? (line 74)

**Note**: XML doc comments are leading trivia, so they must be attached before the declaration is finalized.

---

## Contribution Guidelines

### When to Modify This File

You'll work in this file when:

1. **Adding support for new type declarations**
   - Example: Supporting records, delegates, or other C# type constructs
   - Pattern: Add a new `Generate*Declaration()` method

2. **Changing decorator behavior**
   - Example: Adding new decorators like `@sealed`, `@readonly`
   - Modify: `GenerateModifiersFromDecorators()` or `GenerateTypeModifiersFromDecorators()`

3. **Enhancing generic constraints**
   - Example: Supporting `where T : unmanaged` or `notnull`
   - Modify: `GenerateConstraintClauses()`

4. **Improving name mangling**
   - Example: Better handling of special names or abbreviations
   - Modify: Calls to `NameMangler.Transform()` with appropriate contexts

5. **Extending enum support**
   - Example: Supporting different enum base types (byte, long, etc.)
   - Modify: `GenerateEnumDeclaration()` and related methods

### Code Style Guidelines

1. **Use Roslyn SyntaxFactory**: Always use factory methods like `MethodDeclaration()`, `ClassDeclaration()`, etc.
   - Don't construct syntax nodes manually

2. **Maintain separation of concerns**:
   - Type declarations → This file
   - Type members → ClassMembers.cs
   - Expressions → Expressions.cs
   - Statements → Statements.cs

3. **Track state carefully**:
   - Use appropriate tracking sets (`_classNames`, `_stringEnumNames`, etc.)
   - Clear scope-local state in `GenerateFunctionDeclaration()`
   - Restore context flags (like `_isInAbstractClass`) after nested processing

4. **Handle edge cases**:
   - Check for null/empty (docstrings, type annotations, base classes)
   - Provide sensible defaults (void for untyped returns, object for untyped params)

5. **Preserve docstrings**:
   - Always convert Sharpy docstrings to XML doc comments when present
   - Use `GenerateXmlDocComment()` helper

### Testing Additions

When adding features to this file:

1. **Add integration tests** in `Sharpy.Tests.Integration`:
   - Write a `.spy` file with your feature
   - Verify it compiles to correct C#
   - Verify the C# compiles and runs correctly

2. **Add unit tests** if appropriate:
   - For complex logic like constraint ordering
   - For name transformation edge cases

3. **Test interop scenarios**:
   - Ensure generated C# is idiomatic and usable from other C# code
   - Test with .NET libraries

### Common Pitfalls to Avoid

1. **Don't break name mangling consistency**
   - Always use the appropriate `NameContext` enum value
   - Match naming conventions used elsewhere in the compiler

2. **Don't forget to track new type kinds**
   - If adding a new type declaration, update tracking sets appropriately
   - Update other partial files that need to know about the new type

3. **Don't violate C# syntax rules**
   - Example: Constraint ordering MUST follow C# rules
   - Example: Enum base types must be integral

4. **Don't leak scope state**
   - Always clear/restore state that's function-scoped
   - Use try/finally if needed to guarantee cleanup

5. **Don't break the Roslyn API**
   - Roslyn syntax nodes are immutable - use `.With*()` methods
   - Chain modifications: `node.WithModifiers(...).WithBody(...)`

---

## Cross-References

This file is part of the **RoslynEmitter partial class**. Related files:

- **RoslynEmitter.cs** - Main partial file with shared state and fields
- **[RoslynEmitter.ClassMembers.md](RoslynEmitter.ClassMembers.md)** - Generates class/interface members (fields, properties, methods)
- **[RoslynEmitter.Expressions.md](RoslynEmitter.Expressions.md)** - Expression generation (called from parameter defaults, enum values)
- **[RoslynEmitter.Statements.md](RoslynEmitter.Statements.md)** - Statement generation (called for function bodies)
- **RoslynEmitter.CompilationUnit.md** - Top-level file generation
- **RoslynEmitter.ModuleClass.md** - Module-level code organization

### Related Documentation

- **Language Specs**:
  - `docs/language_specification/type_annotations.md` - Type annotation syntax
  - `docs/language_specification/type_hierarchy.md` - Class/interface hierarchy
  - `docs/language_specification/dotnet_interop.md` - .NET interop rules

- **Architecture**:
  - CodeGenContext.md - Code generation context and symbol resolution
  - TypeMapper.md - Type mapping from Sharpy to C#
  - NameMangler.md - Name transformation logic
