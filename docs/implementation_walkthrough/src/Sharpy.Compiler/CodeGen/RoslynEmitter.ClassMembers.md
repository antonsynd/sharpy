# Walkthrough: RoslynEmitter.ClassMembers.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`

---

## Overview

This file is part of the **RoslynEmitter** partial class, responsible for generating C# class and interface members from Sharpy's typed AST. It handles the transformation of:

- Class fields (instance variables)
- Constructors (`__init__` methods)
- Instance and static methods
- Special Python dunder methods (e.g., `__str__`, `__eq__`, `__add__`)
- Operator overloads synthesized from dunder methods
- Interface methods and properties

**Position in Pipeline**: This is the final compiler phase. It takes validated, typed AST nodes and emits Roslyn `SyntaxNode` objects representing C# code.

**Key Principle**: All code generation uses Roslyn's `SyntaxFactory` API exclusively—no string templates or concatenation. This ensures syntactically correct C# every time.

---

## Class/Type Structure

This file extends the `RoslynEmitter` partial class (defined in `RoslynEmitter.cs`) with member generation capabilities. The partial class pattern allows the emitter to be logically separated across multiple files:

- **RoslynEmitter.cs** - Core emitter with type mapper and scope tracking
- **RoslynEmitter.ClassMembers.cs** (this file) - Class/interface member generation
- **RoslynEmitter.Expressions.cs** - Expression generation
- **RoslynEmitter.Statements.cs** - Statement generation
- **RoslynEmitter.Operators.cs** - Operator overload synthesis
- **RoslynEmitter.TypeDeclarations.cs** - Class/struct/interface declarations
- **RoslynEmitter.CompilationUnit.cs** - Top-level compilation unit generation
- **RoslynEmitter.ModuleClass.cs** - Module wrapper class generation

### Key Dependencies

The file relies on several compiler components:

1. **NameMangler** (`src/Sharpy.Compiler/CodeGen/NameMangler.cs`)
   - Transforms Python naming conventions to C# conventions
   - `snake_case` → `PascalCase` (methods, fields)
   - `snake_case` → `camelCase` (parameters, local variables)
   - Handles dunder method mappings: `__str__` → `ToString`, `__eq__` → `Equals`

2. **TypeMapper** (referenced via `_typeMapper` field)
   - Maps Sharpy type annotations to C# types
   - Infers types from expressions when annotations are missing

3. **ProtocolRegistry** (`src/Sharpy.Compiler/Semantic/ProtocolRegistry.cs`)
   - Defines protocol dunder methods and their C# equivalents
   - Provides expected return types (e.g., `__len__` → `int`, `__str__` → `string`)

4. **AST Nodes** (`Sharpy.Compiler.Parser.Ast`)
   - `Statement`, `FunctionDef`, `VariableDeclaration`, `Assignment`, etc.
   - Immutable AST nodes produced by the parser

5. **Roslyn SyntaxFactory** (`Microsoft.CodeAnalysis.CSharp.SyntaxFactory`)
   - Factory methods for creating C# syntax nodes
   - Used exclusively for code generation (no string templates)

---

## Key Functions/Methods

### 1. `GenerateClassMembers(IReadOnlyList<Statement> body, string className)`

**Purpose**: Orchestrates the generation of all class members from a class body.

**Algorithm**:
```
1. First pass: Generate fields
   - Extract field declarations from class body
   - Build field mapping (Sharpy name → C# name) for use in constructor

2. Second pass: Generate methods and constructors
   - Collect all __init__ methods (supports overloading)
   - Track dunder methods for complementary operator generation
   - Generate class methods and operator overloads

3. Third pass: Generate constructors
   - Use field mapping to emit proper field assignments

4. Fourth pass: Generate complementary operators
   - If __eq__ exists but not __ne__, synthesize operator !=
   - If __ne__ exists but not __eq__, synthesize operator ==
```

**Why the multi-pass approach?**
- Field mapping must be built before constructors (constructors need to know mangled field names)
- Dunder method tracking must complete before complementary operator generation
- C# requires both `==` and `!=` operators if either is defined

**Example Flow**:
```python
# Sharpy source
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
```

Generates:
```csharp
public class Point
{
    public int X;  // Field pass: x → X (PascalCase)
    public int Y;

    public Point(int x, int y)  // Constructor pass: uses field mapping
    {
        this.X = x;  // Maps to mangled field name
        this.Y = y;
    }
}
```

---

### 2. `GenerateConstructor(FunctionDef func, string className, Dictionary<string, string> fieldMapping)`

**Purpose**: Transforms a Sharpy `__init__` method into a C# constructor.

**Key Parameters**:
- `func`: The `__init__` FunctionDef AST node
- `className`: The C# class name (for constructor declaration)
- `fieldMapping`: Maps Sharpy field names to C# field names

**Implementation Details**:

1. **Scope Tracking Reset**: Clears local variable tracking for new method scope
   ```csharp
   _declaredVariables.Clear();
   _variableVersions.Clear();
   _constVariables.Clear();
   ```

2. **Parameter Handling**:
   - Skips `self` parameter (implicit in C#)
   - Mangles parameter names to camelCase
   - Tracks parameters as declared variables

3. **Base Constructor Detection**:
   ```python
   # Sharpy
   def __init__(self, x: int):
       super().__init__()  # First statement
       self.x = x
   ```

   Transforms to:
   ```csharp
   public Derived(int x) : base()  // Constructor initializer
   {
       this.X = x;  // Body starts from second statement
   }
   ```

4. **Field Assignment Transformation**:
   - Detects `self.field = value` patterns
   - Uses field mapping to ensure correct C# field names
   - Handles inherited fields not in mapping (falls back to PascalCase)

5. **Parameter Reference Mapping**:
   - If RHS of assignment is a parameter, uses mangled parameter name
   - Otherwise generates expression normally

**Debugging Insight**: If constructor field assignments aren't working, check:
- Field mapping was built correctly in `GenerateClassMembers` first pass
- Parameter names are being tracked in `_declaredVariables`
- `self.field` pattern is being detected correctly

---

### 3. `GenerateClassMethod(FunctionDef func)`

**Purpose**: Generates instance or static methods from Sharpy function definitions.

**Key Features**:

1. **Automatic Static Detection** (lines 318-325):
   ```csharp
   bool hasSelfParameter = func.Parameters.Any(p =>
       string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));

   if (!hasSelfParameter && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
   {
       modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
   }
   ```
   - Methods without `self` parameter are automatically `static`
   - `@static` decorator is optional/redundant

2. **Dunder Method Handling**:
   ```python
   # Sharpy
   def __str__(self) -> str:
       return "Point"
   ```

   Generates:
   ```csharp
   public override string ToString()  // __str__ → ToString, override keyword added
   {
       return "Point";
   }
   ```

3. **Protocol Return Type Inference** (lines 287-299):
   - Uses `ProtocolRegistry` to determine correct return types
   - Overrides user annotations if protocol specifies a type
   - Example: `__len__` always returns `int`, `__str__` always returns `string`

4. **Override Detection** (lines 305-315):
   - Adds `override` keyword for methods that override `Object` methods
   - Handles: `ToString()`, `GetHashCode()`, `Equals()`

5. **Special Equals() Handling** (lines 335-341):
   - `__eq__` parameter type forced to `object` (C# requirement)
   ```csharp
   public override bool Equals(object other)  // Not Point other
   {
       // ...
   }
   ```

6. **Abstract Method Detection** (lines 356-369):
   - Explicit: `@abstract` decorator
   - Implicit: Ellipsis body in abstract class
   ```python
   @abstract
   class Shape:
       def area(self) -> float: ...  # Abstract (ellipsis + abstract class)
   ```

---

### 4. `GenerateMethodModifiersFromDecorators(IReadOnlyList<Decorator> decorators)`

**Purpose**: Converts Sharpy decorators to C# modifiers.

**Decorator Mappings**:

| Sharpy Decorator | C# Modifier |
|------------------|-------------|
| `@private` | `private` |
| `@protected` | `protected` |
| `@internal` | `internal` |
| `@public` | `public` (default if none specified) |
| `@static` or `@staticmethod` | `static` |
| `@abstract` | `abstract` |
| `@virtual` | `virtual` |
| `@override` | `override` |

**Default Behavior**: Methods without access modifiers default to `public` (line 427-430).

---

### 5. `GenerateField(VariableDeclaration varDecl)`

**Purpose**: Generates C# field declarations from class-level variable declarations.

**Name Mangling**: Uses `PascalCase` for fields (C# property-like convention)
```python
# Sharpy
class Point:
    x: int  # Instance variable
```

Generates:
```csharp
public class Point
{
    public int X;  // x → X (PascalCase)
}
```

**Type Inference**:
- Uses type annotation if present
- For `const` declarations without annotations, infers from initializer
- Falls back to `object` if no type information available

**Const Handling**:
```python
# Sharpy
class Config:
    MAX_SIZE: int = 100  # Const field
```

Generates:
```csharp
public class Config
{
    public const int MAX_SIZE = 100;  // const modifier added
}
```

---

### 6. Interface Member Generation

#### `GenerateInterfaceMembers(IReadOnlyList<Statement> body)`

**Purpose**: Generates interface members (methods and properties).

**Behavior**:
- Methods: No body, no modifiers (line 510-511)
- Properties: Get/set accessors with no body (line 580-589)
- Ignores: `pass` statements, ellipsis, other statements

#### `GenerateInterfaceMethod(FunctionDef func)`

**C# Interface Requirements**:
```python
# Sharpy
interface IDrawable:
    def draw(self) -> None: ...
```

Generates:
```csharp
interface IDrawable
{
    void Draw();  // No modifiers, no body, semicolon terminator
}
```

#### `GenerateInterfaceProperty(VariableDeclaration varDecl)`

**Type Annotation Requirement**: Interface properties MUST have type annotations (throws exception otherwise, line 571-574).

```python
# Sharpy
interface IPoint:
    x: int  # Type annotation required
```

Generates:
```csharp
interface IPoint
{
    int X { get; set; }  // Get/set accessors, no body
}
```

---

## Patterns and Design Decisions

### 1. Multi-Pass Member Generation

**Why?** Dependencies between members require ordering:
- Fields must be generated before constructors (field mapping needed)
- Dunder methods must be scanned before complementary operators generated
- Constructor overloads collected before generation

### 2. Field Mapping for Constructor Correctness

The `fieldMapping` dictionary (line 24) ensures constructors reference the correct C# field names:

```python
# Sharpy
class Point:
    user_x: int

    def __init__(self, user_x: int):
        self.user_x = user_x  # Must map to correct C# field
```

Without mapping, constructor might generate `this.UserX = user_x` when field is actually `this.User_x`.

### 3. Scope Tracking Per Method

Every method generation clears scope tracking (lines 132-134, 273-275):
```csharp
_declaredVariables.Clear();
_variableVersions.Clear();
_constVariables.Clear();
```

**Why?** Local variable redeclarations are method-scoped in Sharpy:
```python
def foo():
    x = 1    # x
    x = "a"  # x_1 (type change requires new variable)

def bar():
    x = 1    # x (fresh scope, no version number)
```

### 4. Parameter Mapping in Constructors

Parameters are mangled and tracked separately (lines 146-162) to handle:
```python
def __init__(self, my_value: int):
    self.my_value = my_value  # RHS is parameter, LHS is field
```

Generates:
```csharp
public MyClass(int myValue)  // Parameter: camelCase
{
    this.MyValue = myValue;  // Field: PascalCase, parameter reference
}
```

### 5. Complementary Operator Generation

C# requires paired operators (lines 114-124):
- If `operator ==` is defined, `operator !=` must also be defined
- If `operator !=` is defined, `operator ==` must also be defined

This is handled by tracking dunder methods and auto-generating the missing operator.

### 6. Abstract Method Implicit Detection

Methods in abstract classes with ellipsis bodies are implicitly abstract (lines 360-363):
```python
@abstract
class Shape:
    def area(self) -> float: ...  # Implicit abstract
```

This matches Python's `abc` module conventions.

### 7. SyntaxFactory Exclusive Usage

**No String Templates**: All C# code generation uses Roslyn's `SyntaxFactory`:
```csharp
// Good
var assignment = AssignmentExpression(
    SyntaxKind.SimpleAssignmentExpression,
    MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        ThisExpression(),
        IdentifierName(fieldName)),
    assignValue);

// Bad (forbidden)
var assignment = $"this.{fieldName} = {value};";
```

**Benefits**:
- Syntactically guaranteed correct
- Proper escaping/quoting
- Refactoring-safe
- IDE support for Roslyn APIs

---

## Dependencies on Other Components

### Upstream: Semantic Analysis

The emitter expects:
- **Validated AST**: No semantic errors remain
- **Type Annotations**: Resolved and validated
- **SemanticInfo**: Populated on AST nodes (used in expression/statement generation)
- **SymbolTable**: Available via `CodeGenContext` for symbol lookups

### Peer Components (Other RoslynEmitter Partials)

- **RoslynEmitter.Operators.cs**:
  - `TryGenerateOperatorOverload()` - Called for dunder methods (line 78)
  - `GenerateComplementaryEqualsOperator()` - Synthesizes `==` (line 123)
  - `GenerateComplementaryNotEqualsOperator()` - Synthesizes `!=` (line 118)

- **RoslynEmitter.Statements.cs**:
  - `GenerateBodyStatement()` - Called for method/constructor bodies (line 234, 384)

- **RoslynEmitter.Expressions.cs**:
  - `GenerateExpression()` - Called for field initializers, assignments (line 223, 482)
  - `GenerateParameter()` - Called for method parameters (line 142, 332)

### External Dependencies

- **NameMangler**: All identifier transformations
- **TypeMapper**: All type conversions
- **ProtocolRegistry**: Dunder method semantics
- **Roslyn**: Syntax tree construction

---

## Debugging Tips

### 1. Constructor Field Assignments Not Working

**Symptom**: Generated constructor assigns to wrong field name or fails to compile.

**Debug Steps**:
1. Check field mapping in `GenerateClassMembers` first pass (line 24-39)
2. Verify field name extraction from `FieldDeclarationSyntax` (line 36-38)
3. Confirm constructor uses field mapping (line 210-212)
4. Look for inherited fields not in mapping (falls back to PascalCase, line 212)

### 2. Method Not Being Generated

**Symptom**: Method appears in Sharpy source but not in C# output.

**Debug Steps**:
1. Check if method name is `__init__` (handled separately as constructor)
2. Verify method isn't filtered in `GenerateClassMembers` switch (line 60-106)
3. Check if abstract method without `@abstract` in non-abstract class (won't have body)

### 3. Wrong Method Modifiers

**Symptom**: Method is `public` when it should be `private`, or `instance` when it should be `static`.

**Debug Steps**:
1. Check decorator processing in `GenerateMethodModifiersFromDecorators` (line 397-454)
2. Verify static detection logic (line 318-325): methods without `self` are static
3. Check override detection for `ToString`, `GetHashCode`, `Equals` (line 305-315)

### 4. Dunder Method Not Mapping Correctly

**Symptom**: `__str__` generates `__Str__` instead of `ToString()`.

**Debug Steps**:
1. Check `NameMangler._dunderMethodMap` for mapping (line 30-46 in NameMangler.cs)
2. Verify `NameMangler.Transform(func.Name, NameContext.Method)` is called (line 279)
3. Confirm protocol registry entry exists for the dunder method

### 5. Operator Overload Not Generated

**Symptom**: `__add__` method exists but no `operator +` in C# output.

**Debug Steps**:
1. Verify `TryGenerateOperatorOverload` is called (line 78)
2. Check `RoslynEmitter.Operators.cs` for operator mapping
3. Confirm dunder method signature matches expected signature

### 6. Interface Property Missing Type Annotation

**Symptom**: Exception thrown during interface property generation.

**Debug Steps**:
1. Check source Sharpy file for type annotation: `x: int` (required)
2. Error message shows line/column of problematic property (line 573-574)
3. Add type annotation to interface property

### 7. Scope Tracking Issues

**Symptom**: Variables declared in wrong scope or duplicate declarations.

**Debug Steps**:
1. Verify scope clearing at method entry (lines 132-134, 273-275)
2. Check parameter tracking (line 153-162, 344-354)
3. Confirm `_declaredVariables` is being updated correctly

---

## Contribution Guidelines

### When to Modify This File

**Add new class member types**:
- New statement types in class bodies
- New decorator types that affect member generation
- New dunder method protocols

**Extend constructor handling**:
- Support for constructor chaining beyond `super().__init__()`
- Field initialization improvements
- Constructor overload resolution

**Improve interface generation**:
- Support for indexed properties
- Event declarations
- Generic constraints

**Fix bugs in**:
- Field/parameter name mapping
- Scope tracking
- Modifier generation
- Dunder method transformation

### What NOT to Change

**DO NOT**:
- Use string templates for code generation (violates SyntaxFactory exclusive usage)
- Modify AST nodes (they are immutable)
- Add semantic validation (belongs in Semantic phase)
- Bypass NameMangler (all naming must go through it)

### Testing Strategy

When modifying this file:

1. **Unit tests** in `Sharpy.Compiler.Tests/CodeGen/`:
   - Test individual method generation
   - Test modifier combinations
   - Test name mangling edge cases

2. **Integration tests** in `Sharpy.Compiler.Tests/Integration/`:
   - Full class generation from source to C#
   - Dunder method roundtrips
   - Constructor variations

3. **File-based tests** in `Sharpy.Compiler.Tests/Integration/TestFixtures/`:
   - Add `.spy` + `.expected` pairs
   - Cover real-world class patterns

4. **Emit verification**:
   ```bash
   dotnet run --project src/Sharpy.Cli -- emit csharp test.spy
   ```
   - Visually inspect generated C#
   - Ensure proper formatting and readability

### Code Style

**Follow existing patterns**:
- Use Roslyn `SyntaxFactory` exclusively
- Clear variables at method scope entry
- Build modifiers as `SyntaxTokenList`, not individual tokens
- Use LINQ for collections when readable
- Comment complex transformations (e.g., base constructor detection)

**Naming conventions**:
- Private fields: `_camelCase`
- Local variables: `camelCase`
- Parameters: `camelCase`
- Methods: `PascalCase`

---

## Cross-References

### Related Partial Class Files

This file is part of the `RoslynEmitter` partial class. Understanding the complete emitter requires reviewing:

1. **[RoslynEmitter.md](RoslynEmitter.md)**
   - Core emitter initialization
   - Scope tracking fields (`_declaredVariables`, `_variableVersions`, etc.)
   - `TypeMapper` initialization

2. **[RoslynEmitter.Operators.md](RoslynEmitter.Operators.md)**
   - `TryGenerateOperatorOverload()` - Called from this file (line 78)
   - Complementary operator generation methods (lines 118, 123)
   - Operator dunder method mappings

3. **[RoslynEmitter.Statements.md](RoslynEmitter.Statements.md)**
   - `GenerateBodyStatement()` - Called for method bodies (line 234, 240, 384)

4. **[RoslynEmitter.Expressions.md](RoslynEmitter.Expressions.md)**
   - `GenerateExpression()` - Called for initializers and assignments (line 223, 482)
   - `GenerateParameter()` - Called for method parameters (line 142, 332)

5. **[RoslynEmitter.TypeDeclarations.md](RoslynEmitter.TypeDeclarations.md)**
   - Class/interface/struct declaration generation
   - Calls back to `GenerateClassMembers()` from this file

### Related Supporting Files

- **[NameMangler.md](NameMangler.md)**
   - Name transformation logic
   - Dunder method mappings

- **[TypeMapper.md](TypeMapper.md)**
   - Type annotation to C# type conversion
   - Type inference from expressions

### Relevant Specifications

See the following language specification documents for details:
- `docs/language_specification/class_methods.md` - Method declaration semantics
- `docs/language_specification/classes.md` - Class structure and members
- `docs/language_specification/constructors.md` - Constructor behavior and initialization
- `docs/language_specification/dotnet_interop.md` - .NET interop including dunder mappings
- `docs/language_specification/inheritance.md` - Base class constructors and method overriding

---

## Summary

This file is the heart of class member code generation in the Sharpy compiler. It orchestrates the transformation of Sharpy's Pythonic class syntax into C#'s class member model, handling:

- Name mangling (Python conventions → C# conventions)
- Dunder method mappings (Python protocols → C# overrides/operators)
- Scope tracking (method-local variable versioning)
- Field/parameter disambiguation (constructor body generation)
- Interface contract generation (abstract methods and properties)

**Key Takeaway**: The multi-pass generation strategy ensures all dependencies between members are satisfied, while exclusive use of Roslyn's SyntaxFactory guarantees syntactically correct C# output every time.
