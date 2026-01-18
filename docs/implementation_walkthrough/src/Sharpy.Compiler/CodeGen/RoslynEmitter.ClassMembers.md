# Walkthrough: RoslynEmitter.ClassMembers.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`

---

## 1. Overview

`RoslynEmitter.ClassMembers.cs` is a **partial class file** that handles the generation of C# class and interface members from Sharpy's typed AST. This file is specifically responsible for transforming Sharpy class bodies—constructors, methods, fields, and interface declarations—into their C# equivalents using the Roslyn Syntax API.

### Position in the Pipeline

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C#
                                                               ↓
                                                    RoslynEmitter.ClassMembers.cs
                                                    (Class body transformation)
```

**Upstream Input:**
- `ClassDef.Body` / `StructDef.Body` / `InterfaceDef.Body` (lists of statements from the Parser)
- `SemanticInfo` with resolved types (from Semantic Analysis)
- `_typeMapper` for converting Sharpy types to C# types

**Downstream Output:**
- `List<MemberDeclarationSyntax>` containing:
  - `FieldDeclarationSyntax` (class fields)
  - `ConstructorDeclarationSyntax` (from `__init__` methods)
  - `MethodDeclarationSyntax` (instance and static methods)
  - `OperatorDeclarationSyntax` (from dunder methods, generated in `RoslynEmitter.Operators.cs`)
  - `PropertyDeclarationSyntax` (for interface properties)

### Key Responsibilities

- Transform Python-style `__init__` methods into C# constructors
- Handle `super().__init__()` calls as constructor initializers (`: base(...)`)
- Generate class fields with proper type annotations and PascalCase naming
- Generate class methods with appropriate modifiers (static, abstract, override, etc.)
- Determine method staticness based on presence of `self` parameter (Pythonic style)
- Generate interface method signatures (no body)
- Generate interface properties with get/set accessors

---

## 2. Class/Type Structure

This file is a **partial class** that extends `RoslynEmitter`. It doesn't define new types but adds methods to handle class member generation.

### Region: Class Member Generation

All methods are defined within a `#region Class Member Generation` block, making it easy to navigate.

```csharp
public partial class RoslynEmitter
{
    #region Class Member Generation

    private List<MemberDeclarationSyntax> GenerateClassMembers(...)
    private ConstructorDeclarationSyntax GenerateConstructor(...)
    private MethodDeclarationSyntax GenerateClassMethod(...)
    private SyntaxTokenList GenerateMethodModifiersFromDecorators(...)
    private FieldDeclarationSyntax GenerateField(...)
    private List<MemberDeclarationSyntax> GenerateInterfaceMembers(...)
    private MethodDeclarationSyntax GenerateInterfaceMethod(...)
    private PropertyDeclarationSyntax GenerateInterfaceProperty(...)

    #endregion
}
```

### Fields Used (from main RoslynEmitter.cs)

| Field | Type | Purpose |
|-------|------|---------|
| `_declaredVariables` | `HashSet<string>` | Tracks variables declared in current scope |
| `_variableVersions` | `Dictionary<string, int>` | Supports variable shadowing with versioned names |
| `_constVariables` | `HashSet<string>` | Tracks const variable names |
| `_isInAbstractClass` | `bool` | Context flag for implicit abstract method detection |
| `_typeMapper` | `TypeMapper` | Converts Sharpy types to C# syntax |

---

## 3. Key Functions/Methods

### 3.1 `GenerateClassMembers` — Main Entry Point

```csharp
private List<MemberDeclarationSyntax> GenerateClassMembers(
    List<Statement> body,
    string className)
```

**What it does:** Orchestrates the generation of all members for a class or struct body.

**Two-Pass Architecture:**

The method uses a deliberate two-pass approach:

```csharp
// First pass: Generate fields and build name mapping
foreach (var stmt in body.Where(s => s is VariableDeclaration))
{
    var varDecl = (VariableDeclaration)stmt;
    var fieldDecl = GenerateField(varDecl);
    fieldMembers.Add(fieldDecl);
    fieldMapping[varDecl.Name] = fieldName; // Original → Mangled
}

// Second pass: Generate methods, constructors, operators
foreach (var stmt in body)
{
    switch (stmt)
    {
        case FunctionDef funcDef:
            // Handle __init__, dunder methods, regular methods
            break;
        // ...
    }
}
```

**Why Two Passes?**

The field mapping dictionary built in the first pass ensures that constructor assignments like `self.name = name` correctly reference the generated field name (`Name`), even if the field declaration appears after the constructor in the source.

**Key Behaviors:**

1. **`__init__` Collection:** All `__init__` methods are collected, then generated as constructors (supports overloading)

2. **Dunder Method Handling:** For dunder methods like `__add__`:
   - Generates the method itself (e.g., `__Add__`)
   - Delegates to `TryGenerateOperatorOverload()` to synthesize the C# operator

3. **Complementary Operator Generation:** Tracks which comparison operators are defined and generates missing pairs:
   ```csharp
   // If __eq__ exists but not __ne__:
   if (dunders.Contains("__eq__") && !dunders.Contains("__ne__"))
   {
       members.Add(GenerateComplementaryNotEqualsOperator(className));
   }
   ```

**Statement Filtering:**

| Statement Type | Action |
|---------------|--------|
| `FunctionDef` | Generate method/constructor/operator |
| `VariableDeclaration` | Already processed in first pass |
| `PassStatement` | Ignored (placeholder) |
| `EllipsisLiteral` (in `ExpressionStatement`) | Ignored (abstract method body marker) |

---

### 3.2 `GenerateConstructor` — `__init__` to C# Constructor

```csharp
private ConstructorDeclarationSyntax GenerateConstructor(
    FunctionDef func,
    string className,
    Dictionary<string, string> fieldMapping)
```

**What it does:** Transforms a Sharpy `__init__` method into a C# constructor.

**Example Transformation:**

```python
# Sharpy
class Dog(Animal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed
```

```csharp
// Generated C#
public class Dog : Animal
{
    public string Breed;

    public Dog(string name, string breed) : base(name)
    {
        this.Breed = breed;
    }
}
```

**Key Steps:**

1. **Scope Reset:** Clears `_declaredVariables`, `_variableVersions`, `_constVariables` for fresh scope

2. **Parameter Processing:** Skips `self` parameter, tracks remaining parameters as declared variables

3. **`super().__init__()` Detection:** Checks if the first statement is a `super().__init__()` call:
   ```csharp
   if (exprStmt.Expression is FunctionCall call &&
       call.Function is MemberAccess memberAccess &&
       memberAccess.Object is SuperExpression &&
       memberAccess.Member == "__init__")
   {
       // Generate `: base(...)` initializer
       baseInitializer = ConstructorInitializer(
           SyntaxKind.BaseConstructorInitializer,
           ArgumentList(SeparatedList(baseArgs)));
       bodyStartIndex = 1; // Skip this statement in body
   }
   ```

4. **`self.field = value` Translation:** Converts field assignments:
   ```csharp
   // self.breed = breed → this.Breed = breed
   var fieldName = fieldMapping.TryGetValue(memberAccess.Member, out var mappedFieldName)
       ? mappedFieldName
       : NameMangler.ToPascalCase(memberAccess.Member);
   ```

5. **XML Documentation:** Adds docstring as `/// <summary>` comment if present

**Important Design Note:**

The `fieldMapping` parameter is crucial for maintaining consistency between field declarations and constructor assignments. Without it, you might have `Breed` as a field but `breed` in the assignment.

---

### 3.3 `GenerateClassMethod` — Instance and Static Methods

```csharp
private MethodDeclarationSyntax GenerateClassMethod(FunctionDef func)
```

**What it does:** Transforms a Sharpy method into a C# method declaration.

**Key Features:**

1. **Name Mangling:**
   ```csharp
   var mangledName = NameMangler.Transform(func.Name, NameContext.Method);
   // calculate_sum → CalculateSum
   // __add__ → __Add__ (preserved dunder with capitalized middle)
   ```

2. **Return Type Resolution (Priority Order):**
   - Explicit type annotation: `func.ReturnType`
   - Protocol registry (for dunders like `__str__`): `ProtocolRegistry.GetProtocol(func.Name)`
   - Default: `void`

3. **Automatic `override` Detection:**
   ```csharp
   var shouldAddOverride = protocol?.ClrMethodName is "ToString" or "GetHashCode"
       || func.Name == "__repr__"
       || func.Name == "__eq__";
   ```

4. **Static Method Detection (Pythonic Style):**
   ```csharp
   // Primary mechanism: No 'self' parameter = static method
   bool hasSelfParameter = func.Parameters.Any(p =>
       string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));

   if (!hasSelfParameter && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
   {
       modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
   }
   ```

5. **Abstract Method Detection:**
   ```csharp
   bool hasAbstractDecorator = func.Decorators.Any(d => d.Name == "abstract");
   bool hasEllipsisBody = func.Body.Count == 1
       && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

   // Implicit: ellipsis body in abstract class = abstract method
   bool isAbstract = hasAbstractDecorator || (_isInAbstractClass && hasEllipsisBody);
   ```

6. **Special `__eq__` Handling:**
   ```csharp
   // Equals() must take object parameter for proper override
   if (func.Name == "__eq__" && parameters.Length > 0)
   {
       var objParam = Parameter(Identifier(parameters[0].Identifier.Text))
           .WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)));
       parameters = new[] { objParam };
   }
   ```

**Example Transformation:**

```python
# Sharpy
@virtual
def greet(self) -> str:
    """Say hello."""
    return f"Hello, I'm {self.name}"
```

```csharp
// Generated C#
/// <summary>
/// Say hello.
/// </summary>
public virtual string Greet()
{
    return $"Hello, I'm {this.Name}";
}
```

---

### 3.4 `GenerateMethodModifiersFromDecorators`

```csharp
private SyntaxTokenList GenerateMethodModifiersFromDecorators(List<Decorator> decorators)
```

**What it does:** Maps Sharpy decorators to C# method modifiers.

**Modifier Mapping Table:**

| Sharpy Decorator | C# Modifier | Notes |
|-----------------|-------------|-------|
| `@public` | `public` | Default if none specified |
| `@private` | `private` | — |
| `@protected` | `protected` | — |
| `@internal` | `internal` | — |
| `@staticmethod` / `@static` | `static` | Redundant if no `self` param |
| `@abstract` | `abstract` | — |
| `@virtual` | `virtual` | — |
| `@override` | `override` | — |

**Default Behavior:**
- If no access modifier: defaults to `public`
- Methods are **not** static by default (unlike module-level functions)

---

### 3.5 `GenerateField` — Variable Declarations to Fields

```csharp
private FieldDeclarationSyntax GenerateField(VariableDeclaration varDecl)
```

**What it does:** Transforms Sharpy class variable declarations into C# fields.

**Key Behaviors:**

1. **Name Transformation:**
   ```csharp
   var fieldName = NameMangler.ToPascalCase(varDecl.Name);
   // first_name → FirstName
   ```

2. **Type Resolution (Priority Order):**
   - Explicit type annotation
   - Type inference from initializer (for `const` declarations)
   - Fallback: `object`

3. **Const Handling:**
   ```csharp
   if (varDecl.IsConst)
   {
       modifiers = modifiers.Add(Token(SyntaxKind.ConstKeyword));
   }
   ```

**Example Transformation:**

```python
# Sharpy
class Circle:
    PI: float = 3.14159
    radius: float
```

```csharp
// Generated C#
public class Circle
{
    public float Pi = 3.14159F;
    public float Radius;
}
```

---

### 3.6 Interface Member Generation

```csharp
private List<MemberDeclarationSyntax> GenerateInterfaceMembers(List<Statement> body)
private MethodDeclarationSyntax GenerateInterfaceMethod(FunctionDef func)
private PropertyDeclarationSyntax GenerateInterfaceProperty(VariableDeclaration varDecl)
```

**What these do:** Transform Sharpy interface definitions into C# interface members.

**Key Differences from Classes:**

| Aspect | Class | Interface |
|--------|-------|-----------|
| Methods | Have body | No body (semicolon only) |
| Properties | Fields with initializers | `{ get; set; }` accessors |
| Modifiers | `public static virtual` etc. | None (implicitly public) |
| Type annotations | Optional | Required for properties |

**Example Transformation:**

```python
# Sharpy
interface IShape:
    name: str

    def area(self) -> float:
        ...
```

```csharp
// Generated C#
public interface IShape
{
    string Name { get; set; }

    float Area();
}
```

**Error Handling:**
```csharp
// Interface properties must have type annotations
if (varDecl.Type == null)
{
    throw new InvalidOperationException(
        $"Interface property '{varDecl.Name}' must have a type annotation " +
        $"at {varDecl.LineStart}:{varDecl.ColumnStart}");
}
```

---

## 4. Dependencies

### Internal Dependencies

| Dependency | Location | Purpose |
|------------|----------|---------|
| `NameMangler` | `CodeGen/NameMangler.cs` | Name convention transformation |
| `TypeMapper` | `CodeGen/TypeMapper.cs` | Sharpy → C# type conversion |
| `ProtocolRegistry` | `Semantic/ProtocolRegistry.cs` | Dunder method metadata |
| `CodeGenContext` | `CodeGen/CodeGenContext.cs` | Compilation state |

### Partial Class Dependencies

This file is part of `RoslynEmitter` which is split across:

| File | Responsibility |
|------|---------------|
| `RoslynEmitter.cs` | Core state, variable name mangling, helpers |
| **`RoslynEmitter.ClassMembers.cs`** | **Class/interface member generation (this file)** |
| `RoslynEmitter.CompilationUnit.cs` | Module → CompilationUnit, using directives |
| `RoslynEmitter.Expressions.cs` | Expression generation |
| `RoslynEmitter.ModuleClass.cs` | Exports class generation |
| `RoslynEmitter.Operators.cs` | Operator overload generation |
| `RoslynEmitter.Statements.cs` | Statement generation |
| `RoslynEmitter.TypeDeclarations.cs` | Class/struct/interface/enum declarations |

### AST Node Types (from `Sharpy.Compiler.Parser.Ast`)

| Node | C# Equivalent |
|------|---------------|
| `FunctionDef` | `MethodDeclarationSyntax` or `ConstructorDeclarationSyntax` |
| `VariableDeclaration` | `FieldDeclarationSyntax` or `PropertyDeclarationSyntax` |
| `Decorator` | Modifiers (`public`, `static`, `virtual`, etc.) |
| `SuperExpression` | `base` keyword |
| `MemberAccess` | `MemberAccessExpression` |

---

## 5. Patterns and Design Decisions

### Pattern: Two-Pass Member Generation

**Problem:** Field names are needed when generating constructor assignments, but fields may be declared after the constructor in source order.

**Solution:** First pass collects all fields and builds a name mapping dictionary. Second pass generates methods/constructors using this mapping.

```csharp
// Phase 1: Build mapping
fieldMapping["first_name"] = "FirstName";

// Phase 2: Use mapping in constructor
self.first_name = name  →  this.FirstName = name
                                    ↑
                         Looked up from fieldMapping
```

### Pattern: Dunder Tracking for Complementary Operators

**Problem:** C# requires both `==` and `!=` if either is defined, but Sharpy only requires `__eq__`.

**Solution:** Track which dunder methods are present, then generate missing counterparts:

```csharp
var dunders = new HashSet<string>();
foreach (var stmt in body)
{
    if (stmt is FunctionDef fd && NameMangler.IsDunderMethod(fd.Name))
        dunders.Add(fd.Name);
}

// After generating all methods, add missing operators
if (dunders.Contains("__eq__") && !dunders.Contains("__ne__"))
    members.Add(GenerateComplementaryNotEqualsOperator(className));
```

### Design Decision: Pythonic Static Detection

**Problem:** Python uses absence of `self` to indicate static methods. Should Sharpy require `@staticmethod`?

**Decision:** Follow Python semantics—no `self` parameter automatically means static method. The `@staticmethod` decorator is supported but redundant.

```python
# Both equivalent in Sharpy:
def utility():          # Static (no self)
    pass

@staticmethod
def utility():          # Explicitly static (redundant but valid)
    pass
```

### Design Decision: `super().__init__()` as Constructor Initializer

**Problem:** In Python, `super().__init__()` can appear anywhere in `__init__`. In C#, base constructor calls must be in the initializer (`: base(...)`).

**Decision:** Only the **first statement** is checked. If it's `super().__init__()`, it becomes `: base(...)`. Otherwise, it's left as a method call (which would be a compile error in C# if the base class requires constructor arguments).

```python
def __init__(self, name: str, age: int):
    super().__init__(name)   # FIRST statement → : base(name)
    self.age = age
```

### Design Decision: Implicit Abstract Methods

**Problem:** Marking every abstract method with `@abstract` is verbose.

**Decision:** In an abstract class, methods with an ellipsis body (`...`) are implicitly abstract:

```python
@abstract
class Shape:
    def area(self) -> float:
        ...  # Implicitly abstract (no @abstract needed)
```

Checked via `_isInAbstractClass` flag set by `RoslynEmitter.TypeDeclarations.cs`.

---

## 6. Debugging Tips

### Inspecting Generated Members

Add this after `GenerateClassMembers()`:

```csharp
foreach (var member in members)
{
    Console.WriteLine(member.NormalizeWhitespace().ToFullString());
    Console.WriteLine("---");
}
```

### Key Breakpoint Locations

| Method | What to Inspect |
|--------|-----------------|
| `GenerateClassMembers()` | Entry point, field mapping construction |
| `GenerateConstructor()` | `super()` detection, `self.field` translation |
| `GenerateClassMethod()` | Modifier determination, override detection |
| `GenerateField()` | Name mangling, type inference |

### Common Issues and Solutions

| Issue | Symptom | Cause | Fix |
|-------|---------|-------|-----|
| Wrong field name in constructor | `this.firstName` instead of `this.FirstName` | Field mapping not used | Check `fieldMapping.TryGetValue()` |
| Missing `static` keyword | Instance method error on method with no `self` | `hasSelfParameter` check failed | Verify parameter parsing |
| Missing `: base(...)` | Base constructor not called | `super().__init__()` not first statement | Reorder source or update detection logic |
| Abstract method has body | Compile error | `isAbstract` not detected | Check `_isInAbstractClass` flag |
| Missing `override` on `Equals` | Hiding warning | `shouldAddOverride` not triggering | Verify `func.Name == "__eq__"` check |

### Verifying Operator Generation

Dunder methods should generate **two** members. Check for both:

```csharp
// Expected for __add__:
// 1. public Point __Add__(Point other) { ... }
// 2. public static Point operator +(Point left, Point right) { ... }

var methods = members.OfType<MethodDeclarationSyntax>();
var operators = members.OfType<OperatorDeclarationSyntax>();
Debug.Assert(methods.Any(m => m.Identifier.Text == "__Add__"));
Debug.Assert(operators.Any(o => o.OperatorToken.IsKind(SyntaxKind.PlusToken)));
```

---

## 7. Contribution Guidelines

### Adding Support for a New Decorator

1. **Add case to `GenerateMethodModifiersFromDecorators()`:**
   ```csharp
   case "new_modifier":
       tokens.Add(Token(SyntaxKind.NewModifierKeyword));
       break;
   ```

2. **Document in language spec:** Update `docs/language_specification/class_methods.md`

### Adding a New Dunder-to-Method Mapping

1. **Update `NameMangler._dunderMethodMap`** in `NameMangler.cs`:
   ```csharp
   { "__new_dunder__", "ClrMethodName" },
   ```

2. **Add protocol to `ProtocolRegistry`** if it needs special return type handling

3. **Update `shouldAddOverride`** if it overrides an Object method

### Extending Constructor Chaining

Currently only `super().__init__()` is supported. To add `self.__init__()` (for chaining to another overload):

1. Detect `self.__init__()` pattern in `GenerateConstructor()`
2. Generate `: this(...)` instead of `: base(...)`
3. Update constructor detection logic

### Adding Property Generation (Instead of Fields)

Currently class variables become public fields. To generate properties instead:

1. Replace `GenerateField()` with `GenerateProperty()`
2. Generate `{ get; set; }` accessors
3. Consider backing field for complex cases

---

## 8. Cross-References

### Related Partial Class Files

- **[RoslynEmitter.cs](./RoslynEmitter.md)** — Core class definition, variable name mangling, entry points
- **[RoslynEmitter.TypeDeclarations.cs](./RoslynEmitter.TypeDeclarations.md)** — Calls `GenerateClassMembers()` from `GenerateClassDeclaration()`
- **[RoslynEmitter.Operators.cs](./RoslynEmitter.Operators.md)** — `TryGenerateOperatorOverload()`, complementary operator generation
- **[RoslynEmitter.Expressions.cs](./RoslynEmitter.Expressions.md)** — Expression generation used in method bodies
- **[RoslynEmitter.Statements.cs](./RoslynEmitter.Statements.md)** — Statement generation for method bodies

### Supporting Modules

- **[NameMangler.md](./NameMangler.md)** — Name convention transformation details
- **[TypeMapper.md](./TypeMapper.md)** — Type conversion rules
- **[CodeGenContext.md](./CodeGenContext.md)** — Compilation context and symbol lookup

### Language Specification

- `docs/language_specification/classes.md` — Class syntax and semantics
- `docs/language_specification/class_methods.md` — Method types (instance, static, dunder)
- `docs/language_specification/constructors.md` — `__init__`, overloading, chaining
- `docs/language_specification/inheritance.md` — `super()` usage and constraints
- `docs/language_specification/dotnet_interop.md` — .NET integration patterns

---

## Summary

`RoslynEmitter.ClassMembers.cs` is the **class body transformation engine** of the Sharpy compiler. It bridges the gap between Python's class semantics and C#'s member declarations:

| Sharpy Concept | C# Output |
|----------------|-----------|
| `__init__` method | Constructor |
| `super().__init__()` | `: base(...)` initializer |
| `self.field = value` | `this.Field = value` |
| No `self` parameter | `static` method |
| Ellipsis body in abstract class | Abstract method |
| Dunder methods | Method + operator overload |
| Interface variables | Properties with accessors |

The key insight is the **two-pass design** for consistent field naming and the **Pythonic static detection** based on `self` parameter presence. When debugging, focus on the field mapping construction and the modifier determination logic.

---

**Next Steps for Newcomers:**
1. Read the main `RoslynEmitter.cs` walkthrough for overall context
2. Trace through `GenerateClassMembers()` with a simple class example
3. Understand how `NameMangler` transforms identifiers
4. Explore `RoslynEmitter.Operators.cs` for operator synthesis details
5. Run integration tests with `--emit-csharp` to see actual output
