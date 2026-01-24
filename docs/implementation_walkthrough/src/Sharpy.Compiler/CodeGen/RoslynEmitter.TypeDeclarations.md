# Walkthrough: RoslynEmitter.TypeDeclarations.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`

---

## Overview

This file is a **partial class** component of `RoslynEmitter` that transforms Python-style type declarations (functions, classes, structs, interfaces, enums) into C# using Roslyn's `SyntaxFactory` API. It's the final stage of the compiler pipeline that handles **type-level constructs**.

### Role in the Compiler Pipeline

```
Semantic Analysis (Typed AST) → RoslynEmitter.TypeDeclarations → C# Type Declarations → .NET IL
```

**Inputs:**
- Typed AST nodes: `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
- `SemanticInfo`: Type annotations, symbol table lookups
- `CodeGenContext`: Module-level metadata, symbol resolution

**Outputs:**
- Roslyn syntax nodes: `MethodDeclarationSyntax`, `ClassDeclarationSyntax`, etc.
- Valid C# 9.0 code that compiles to .NET IL

**Key Responsibilities:**
- Transform snake_case → PascalCase (via `NameMangler`)
- Map Python type annotations → C# types (via `TypeMapper`)
- Convert Python decorators → C# modifiers (`@staticmethod` → `static`)
- Generate XML documentation from docstrings
- Handle special cases: generic constraints, abstract stubs, string enums

---

## Partial Class Architecture

`RoslynEmitter` is split across multiple files for maintainability:

- **RoslynEmitter.cs** - Core infrastructure, fields, symbol resolution
- **RoslynEmitter.TypeDeclarations.cs** ← This file (functions, classes, interfaces, enums)
- **RoslynEmitter.ClassMembers.cs** - Methods, properties, fields within classes
- **RoslynEmitter.Expressions.cs** - Expression code generation
- **RoslynEmitter.Statements.cs** - Statement code generation
- **RoslynEmitter.CompilationUnit.cs** - Top-level module generation

### Related Documentation
- [RoslynEmitter.md](./RoslynEmitter.md) - Main emitter overview
- [RoslynEmitter.ClassMembers.md](./RoslynEmitter.ClassMembers.md) - Class member generation
- [TypeMapper.md](./TypeMapper.md) - Type mapping logic
- [NameMangler.md](./NameMangler.md) - Naming convention transformations

---

## Important Fields (from RoslynEmitter.cs)

This partial class uses fields defined in the main `RoslynEmitter.cs`:

```csharp
private readonly CodeGenContext _context;           // Module context, symbol lookup
private readonly TypeMapper _typeMapper;             // Type annotation → C# type mapping
private readonly HashSet<string> _declaredVariables; // Track declared variables in scope
private readonly Dictionary<string, int> _variableVersions;  // Version tracking for redeclarations
private readonly HashSet<string> _constVariables;    // Track const variables
private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions; // For abstract stubs
private bool _isInAbstractClass;                     // Context flag for abstract class generation
```

**Design Note:** The compiler previously tracked `_classNames`, `_structNames`, and `_stringEnumNames` in HashSets. These have been **removed** and replaced with:
- **SymbolTable lookups** for class/struct detection (populated during semantic analysis)
- **CodeGenInfo.IsStringEnum** for string enum detection (computed during semantic analysis)

This shift moves complexity upstream to semantic analysis where it belongs.

---

## Class/Type Structure

This file defines no new types—it extends `RoslynEmitter` with methods organized into two main groups:

### 1. Function Generation (Lines 17-209)
| Method | Purpose |
|--------|---------|
| `GenerateFunctionDeclaration()` | Main entry point: FunctionDef → MethodDeclarationSyntax |
| `GenerateParameter()` | Convert function parameters, handle variadic `*args` |
| `GenerateModifiersFromDecorators()` | `@staticmethod` → `static`, etc. |
| `GenerateXmlDocComment()` | Python docstring → C# XML `<summary>` |

### 2. Type Declaration Generation (Lines 211-773, `#region`)
| Method | Purpose |
|--------|---------|
| `GenerateClassDeclaration()` | ClassDef → ClassDeclarationSyntax (with inheritance) |
| `GenerateStructDeclaration()` | StructDef → StructDeclarationSyntax (value types) |
| `GenerateInterfaceDeclaration()` | InterfaceDef → InterfaceDeclarationSyntax |
| `GenerateEnumDeclaration()` | Dispatcher for integer vs. string enums |
| `GenerateIntegerEnum()` | Standard C# enum declarations |
| `GenerateStringEnumClass()` | Sealed class with `public static readonly string` fields |
| `CollectInterfaceMethodDefs()` | Recursively collect interface methods for abstract stubs |
| `GenerateAbstractMethodStub()` | Generate abstract method for unimplemented interface methods |
| `GenerateConstraintClauses()` | Generic type constraints (`where T : class, new()`) |
| `GenerateTypeModifiersFromDecorators()` | Type-level decorators → modifiers |

---

## Key Functions/Methods Deep Dive

### 1. GenerateFunctionDeclaration() (Lines 17-79)

**Purpose:** Transforms a Sharpy function into a C# method, handling the entry point (`main` → `Main`), parameters, generics, and decorators.

**Algorithm:**

```csharp
private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
{
    // 1. Clear local scope tracking (new function = new scope)
    _declaredVariables.Clear();
    _variableVersions.Clear();
    _constVariables.Clear();

    // 2. Name transformation: snake_case → PascalCase
    //    Special case: "main" → "Main" only for entry point files
    var mangledName = func.Name == "main" && !_context.IsEntryPoint
        ? "MainFunc"  // Avoid C# entry point conflict in non-entry files
        : NameMangler.Transform(func.Name, NameContext.Method);

    // 3. Return type mapping
    TypeSyntax returnType = func.ReturnType != null
        ? _typeMapper.MapType(func.ReturnType)
        : PredefinedType(Token(SyntaxKind.VoidKeyword));  // Default to void

    // 4. Generate modifiers from decorators
    var modifiers = GenerateModifiersFromDecorators(func.Decorators);

    // 5. Generate parameters (including variadic *args)
    var parameters = func.Parameters.Select(GenerateParameter).ToArray();

    // 6. Track parameters as declared variables
    foreach (var param in func.Parameters)
    {
        var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
        _declaredVariables.Add(paramName);
        _variableVersions[NameMangler.ToCamelCase(param.Name)] = 0;
    }

    // 7. Generate method body
    var body = Block(func.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

    // 8. Build method declaration
    var method = MethodDeclaration(returnType, mangledName)
        .WithModifiers(modifiers)
        .WithParameterList(ParameterList(SeparatedList(parameters)))
        .WithBody(body);

    // 9. Add generic type parameters if present
    if (func.TypeParameters.Length > 0)
    {
        var typeParams = func.TypeParameters
            .Select(tp => TypeParameter(tp.Name))
            .ToArray();
        method = method
            .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
            .WithConstraintClauses(GenerateConstraintClauses(func.TypeParameters));
    }

    // 10. Add XML documentation from docstring
    if (!string.IsNullOrEmpty(func.DocString))
    {
        method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
    }

    return method;
}
```

**Important Details:**

- **Scope Clearing (Lines 20-22):** Each function gets a fresh scope. Local variables tracked in `_declaredVariables` don't leak between functions.
  
- **Main Function Handling (Lines 26-28):** In Sharpy, every module can have a `main()` function. To avoid conflicts when compiling non-entry modules:
  - Entry point file: `main` → `Main` (C# convention)
  - Other files: `main` → `MainFunc` (avoid multiple entry points)
  
- **Parameter Tracking (Lines 44-51):** Parameters are pre-registered as "declared" to enable correct variable versioning if they're reassigned in the function body.

**Example Transformation:**

```python
# Sharpy input
def calculate_sum(values: list[int]) -> int:
    """Calculate the sum of a list."""
    total = 0
    for v in values:
        total += v
    return total
```

```csharp
// C# output
/// <summary>
/// Calculate the sum of a list.
/// </summary>
public static int CalculateSum(global::Sharpy.Core.List<int> values)
{
    int total = 0;
    foreach (var v in values)
    {
        total += v;
    }
    return total;
}
```

---

### 2. GenerateParameter() (Lines 81-114)

**Purpose:** Converts a Sharpy parameter to a C# parameter, handling variadic arguments (`*args`), default values, and type annotations.

**Key Logic:**

```csharp
private ParameterSyntax GenerateParameter(Parameter param)
{
    // 1. Transform parameter name (snake_case → camelCase)
    var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);

    // 2. Map type or default to object
    TypeSyntax paramType = param.Type != null
        ? _typeMapper.MapType(param.Type)
        : PredefinedType(Token(SyntaxKind.ObjectKeyword));

    // 3. Handle variadic parameters: *args → params T[]
    if (param.IsVariadic)
    {
        paramType = ArrayType(paramType)
            .WithRankSpecifiers(SingletonList(ArrayRankSpecifier()));
    }

    var parameter = Parameter(Identifier(paramName)).WithType(paramType);

    // 4. Add 'params' modifier for variadic parameters
    if (param.IsVariadic)
    {
        parameter = parameter.WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)));
    }

    // 5. Add default value if present
    if (param.DefaultValue != null)
    {
        var defaultExpr = GenerateExpression(param.DefaultValue);
        parameter = parameter.WithDefault(EqualsValueClause(defaultExpr));
    }

    return parameter;
}
```

**Variadic Parameter Handling:**

Python's `*args` becomes C#'s `params T[]`:

```python
def print_all(*values: int):
    for v in values:
        print(v)
```

```csharp
public static void PrintAll(params int[] values)
{
    foreach (var v in values)
    {
        global::Sharpy.Core.Exports.Print(v);
    }
}
```

**Default Values:**

```python
def greet(name: str = "World"):
    print(f"Hello, {name}!")
```

```csharp
public static void Greet(string name = "World")
{
    global::Sharpy.Core.Exports.Print($"Hello, {name}!");
}
```

---

### 3. GenerateModifiersFromDecorators() (Lines 116-183)

**Purpose:** Converts Python decorators to C# method modifiers.

**Decorator Mappings:**

| Python Decorator | C# Modifier |
|-----------------|-------------|
| `@private` | `private` |
| `@protected` | `protected` |
| `@internal` | `internal` |
| `@public` | `public` (default) |
| `@staticmethod` or `@static` | `static` |
| `@abstract` | `abstract` |
| `@virtual` | `virtual` |
| `@override` | `override` |

**Key Design Decision (Lines 174-181):**

Module-level functions automatically get `static` unless they already have a modifier that makes them non-static (`abstract`, `virtual`, `override`). This is because module-level functions in Sharpy are effectively static methods in a generated module class.

```python
# Sharpy module
def helper_function():
    pass

@staticmethod
def explicit_static():
    pass
```

```csharp
// Generated C# (inside a module class)
public static void HelperFunction() { }

public static void ExplicitStatic() { }
```

---

### 4. GenerateXmlDocComment() (Lines 185-209)

**Purpose:** Converts Python docstrings to C# XML documentation comments.

**Implementation:**

```csharp
private SyntaxTriviaList GenerateXmlDocComment(string docString)
{
    // 1. Split docstring into lines
    var lines = docString.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

    // 2. Build trivia list
    var triviaList = new List<SyntaxTrivia>
    {
        Comment("/// <summary>"),
        EndOfLine("\n")
    };

    // 3. Add each non-empty line as a comment
    triviaList.AddRange(lines
        .Select(line => line.Trim())
        .Where(trimmedLine => !string.IsNullOrEmpty(trimmedLine))
        .SelectMany(trimmedLine => new[]
        {
            Comment($"/// {trimmedLine}"),
            EndOfLine("\n")
        }));

    // 4. Close summary tag
    triviaList.Add(Comment("/// </summary>"));
    triviaList.Add(EndOfLine("\n"));

    return TriviaList(triviaList);
}
```

**Example:**

```python
def process_data(items: list[str]) -> int:
    """
    Process a list of items.
    Returns the count of processed items.
    """
    return len(items)
```

```csharp
/// <summary>
/// Process a list of items.
/// Returns the count of processed items.
/// </summary>
public static int ProcessData(global::Sharpy.Core.List<string> items)
{
    return global::Sharpy.Core.Exports.Len(items);
}
```

---

### 5. GenerateClassDeclaration() (Lines 213-294)

**Purpose:** Transforms a Sharpy class definition into a C# class, handling inheritance, generics, and abstract stub generation.

**Key Steps:**

1. **Track Abstract Context (Lines 220-222):** Set `_isInAbstractClass` flag to enable implicit abstract method detection
2. **Name Transformation (Line 224):** `snake_case` → `PascalCase`
3. **Generate Modifiers (Line 227):** Process decorators (`@abstract`, `@sealed`, etc.)
4. **Generic Type Parameters (Lines 234-242):** Handle `class MyClass[T]:` syntax
5. **Base Classes/Interfaces (Lines 245-251):** Map inheritance chain
6. **Generate Members (Line 254):** Delegate to `GenerateClassMembers()`
7. **Abstract Stub Generation (Lines 257-280):** For abstract classes implementing interfaces, generate abstract methods for missing interface methods
8. **Docstring (Lines 285-288):** Add XML documentation

**Abstract Stub Generation (Lines 257-280):**

This is a sophisticated feature. When an abstract class implements an interface but doesn't provide all method implementations, the compiler generates abstract method stubs:

```python
@abstract
class BaseProcessor(IProcessor):
    """Abstract base class"""
    pass

# IProcessor has methods: process(item: str) -> None
```

```csharp
public abstract class BaseProcessor : IProcessor
{
    // Generated abstract stub because process() wasn't implemented
    public abstract void Process(string item);
}
```

**Algorithm:**

```csharp
if (_isInAbstractClass && classDef.BaseClasses.Length > 0)
{
    // Collect all interface methods recursively
    var interfaceMethods = CollectInterfaceMethodDefs(classDef.BaseClasses);
    
    // Get methods already defined in this class
    var definedMethods = GetDefinedMethodNames(classDef.Body);
    
    // Generate stubs for missing methods
    foreach (var interfaceMethod in interfaceMethods)
    {
        if (!definedMethods.Contains(interfaceMethod.Name))
        {
            var stub = GenerateAbstractMethodStub(interfaceMethod);
            members.Add(stub);
        }
    }
}
```

---

### 6. CollectInterfaceMethodDefs() (Lines 296-354)

**Purpose:** Recursively collect all method definitions from interfaces (including inherited interfaces) to generate abstract stubs.

**Why This Is Needed:**

In C#, an abstract class implementing an interface must still declare abstract methods for unimplemented interface members. Sharpy automates this.

**Algorithm:**

```csharp
private List<FunctionDef> CollectInterfaceMethodDefs(IReadOnlyList<TypeAnnotation> baseTypes)
{
    var result = new List<FunctionDef>();
    var visited = new HashSet<string>();  // Prevent infinite recursion
    var seenMethods = new HashSet<string>();  // Deduplicate methods

    void CollectFromInterface(string interfaceName)
    {
        if (visited.Contains(interfaceName))
            return;
        visited.Add(interfaceName);

        // Look up interface definition
        if (!_interfaceDefinitions.TryGetValue(interfaceName, out var interfaceDef))
            return;

        // Collect methods from this interface
        foreach (var stmt in interfaceDef.Body)
        {
            if (stmt is FunctionDef funcDef)
            {
                if (!seenMethods.Contains(funcDef.Name))
                {
                    seenMethods.Add(funcDef.Name);
                    result.Add(funcDef);
                }
            }
        }

        // Recursively collect from base interfaces
        foreach (var baseInterface in interfaceDef.BaseInterfaces)
        {
            CollectFromInterface(baseInterface.Name);
        }
    }

    // Process all base types
    foreach (var baseType in baseTypes)
    {
        if (_interfaceDefinitions.ContainsKey(baseType.Name))
        {
            CollectFromInterface(baseType.Name);
        }
    }

    return result;
}
```

**Example:**

```python
# Sharpy
interface IDrawable:
    def draw() -> None: ...

interface IResizable(IDrawable):
    def resize(factor: float) -> None: ...

@abstract
class Shape(IResizable):
    pass
```

```csharp
// Generated C#
public interface IDrawable
{
    void Draw();
}

public interface IResizable : IDrawable
{
    void Resize(float factor);
}

public abstract class Shape : IResizable
{
    // Auto-generated stubs from both interfaces
    public abstract void Draw();
    public abstract void Resize(float factor);
}
```

---

### 7. GenerateEnumDeclaration() (Lines 540-555)

**Purpose:** Dispatcher that determines whether to generate an integer enum or a string enum class.

**Design Decision:**

Sharpy supports two types of enums:
1. **Integer enums** → Standard C# `enum`
2. **String enums** → Sealed class with `public static readonly string` fields

**Detection Logic:**

```csharp
private bool IsStringEnum(EnumDef enumDef)
{
    // Check if any member has a string literal value
    foreach (var member in enumDef.Members)
    {
        if (member.Value is StringLiteral)
        {
            return true;
        }
    }
    return false;
}
```

**Why String Enums Need Special Handling:**

C# doesn't support string enums natively. Sharpy emulates them using the pattern:

```csharp
public sealed class Status
{
    public static readonly string Pending = "pending";
    public static readonly string Active = "active";
    public static readonly string Complete = "complete";
}
```

This allows code like:

```python
# Sharpy
status: Status = Status.PENDING
```

```csharp
// C#
Status status = Status.Pending;
```

---

### 8. GenerateIntegerEnum() (Lines 583-609)

**Purpose:** Generate a standard C# enum for integer enums.

```csharp
private EnumDeclarationSyntax GenerateIntegerEnum(EnumDef enumDef)
{
    // 1. Transform name
    var enumName = NameMangler.Transform(enumDef.Name, NameContext.Type);

    // 2. Enums are always public
    var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));

    // 3. Generate members
    var members = enumDef.Members
        .Select(GenerateEnumMember)
        .ToArray();

    // 4. Create enum declaration
    var enumDecl = EnumDeclaration(enumName)
        .WithModifiers(modifiers)
        .WithMembers(SeparatedList(members));

    // 5. Add documentation
    if (!string.IsNullOrEmpty(enumDef.DocString))
    {
        enumDecl = enumDecl.WithLeadingTrivia(GenerateXmlDocComment(enumDef.DocString));
    }

    return enumDecl;
}
```

**Example:**

```python
# Sharpy
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3
```

```csharp
// C#
public enum Color
{
    Red = 1,
    Green = 2,
    Blue = 3
}
```

Note the name transformation: `RED` → `Red` (handled by `TransformEnumMemberName()`).

---

### 9. GenerateStringEnumClass() (Lines 611-679)

**Purpose:** Generate a sealed class with static readonly string fields to emulate string enums.

**Implementation:**

```csharp
private ClassDeclarationSyntax GenerateStringEnumClass(EnumDef enumDef)
{
    var className = NameMangler.Transform(enumDef.Name, NameContext.Type);

    // Create public sealed class
    var modifiers = TokenList(
        Token(SyntaxKind.PublicKeyword),
        Token(SyntaxKind.SealedKeyword)
    );

    var classDecl = ClassDeclaration(className).WithModifiers(modifiers);

    // Generate public static readonly string fields
    var members = new List<MemberDeclarationSyntax>();

    foreach (var member in enumDef.Members)
    {
        var fieldName = NameMangler.Transform(member.Name, NameContext.Constant);

        // Determine value: explicit value or member name as default
        ExpressionSyntax valueExpr;
        if (member.Value is StringLiteral strLit)
        {
            valueExpr = GenerateExpression(strLit);
        }
        else
        {
            // Default to original member name
            valueExpr = LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                Literal(member.Name)
            );
        }

        var field = FieldDeclaration(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(fieldName))
                        .WithInitializer(EqualsValueClause(valueExpr))
                ))
        )
        .WithModifiers(TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.StaticKeyword),
            Token(SyntaxKind.ReadOnlyKeyword)
        ));

        members.Add(field);
    }

    return classDecl.WithMembers(List(members));
}
```

**Example:**

```python
# Sharpy
enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"
```

```csharp
// C#
public sealed class HttpMethod
{
    public static readonly string Get = "GET";
    public static readonly string Post = "POST";
    public static readonly string Put = "PUT";
    public static readonly string Delete = "DELETE";
}
```

---

### 10. GenerateConstraintClauses() (Lines 495-538)

**Purpose:** Generate C# generic type constraints from Sharpy type parameters.

**Constraint Types:**

| Sharpy Constraint | C# Constraint |
|-------------------|---------------|
| `ClassConstraint` | `where T : class` |
| `StructConstraint` | `where T : struct` |
| `TypeConstraint` | `where T : BaseType` |
| `NewConstraint` | `where T : new()` |

**Ordering Matters:**

C# requires constraints in a specific order:
1. `class` or `struct` (reference/value type constraint)
2. Base type constraints
3. `new()` constructor constraint

The code enforces this (lines 508-516):

```csharp
var ordered = typeParam.Constraints
    .OrderBy(c => c switch
    {
        ClassConstraint => 0,
        StructConstraint => 0,
        Parser.Ast.TypeConstraint => 1,
        NewConstraint => 2,
        _ => 3
    });
```

**Example:**

```python
# Sharpy
def create_instance[T: class, new()](factory_data: dict[str, object]) -> T:
    return T()
```

```csharp
// C#
public static T CreateInstance<T>(global::Sharpy.Core.Dict<string, object> factoryData)
    where T : class, new()
{
    return new T();
}
```

---

### 11. TransformEnumMemberName() (Lines 699-715)

**Purpose:** Transform enum member names following Python → C# conventions.

**Rules:**

1. **Backtick-escaped names:** Preserve as-is (strip backticks)
2. **SCREAMING_SNAKE_CASE:** Convert to PascalCase
   - `RED` → `Red`
   - `DARK_BLUE` → `DarkBlue`
   - `HTTP_ERROR` → `HttpError`

**Implementation:**

```csharp
private static string TransformEnumMemberName(string name)
{
    if (string.IsNullOrEmpty(name))
        return name;

    // Handle literal names (backtick-escaped)
    if (name.StartsWith("`") && name.EndsWith("`"))
        return name[1..^1];

    // Split by underscores and capitalize each part
    var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
    var capitalizedParts = parts.Select(part =>
        string.IsNullOrEmpty(part) ? part :
        char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant());

    return string.Join("", capitalizedParts);
}
```

**Why Not Use NameMangler?**

`NameMangler.ToPascalCase()` preserves all-caps words (e.g., `HTTP` stays `HTTP`). For enum members, we want friendlier C# naming (`Http` not `HTTP`).

---

## Dependencies

### Internal (Sharpy Codebase)

| Dependency | Purpose |
|------------|---------|
| `TypeMapper` | Maps Sharpy type annotations → C# types (`list[T]` → `List<T>`) |
| `NameMangler` | Transforms names (`snake_case` → `PascalCase`, `__str__` → `ToString()`) |
| `CodeGenContext` | Provides symbol table, module metadata, entry point detection |
| `SemanticInfo` | Type information from semantic analysis |
| `Parser.Ast.*` | AST node definitions (`FunctionDef`, `ClassDef`, etc.) |

### External (Roslyn)

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
```

All C# code generation uses Roslyn's `SyntaxFactory` API. **Never** use string concatenation or interpolation for code generation.

---

## Patterns and Design Decisions

### 1. Immutability: Roslyn Syntax Trees Are Immutable

All Roslyn syntax nodes are immutable. Modifications create new instances:

```csharp
// ❌ Wrong - this doesn't work
var method = MethodDeclaration(returnType, name);
method.WithModifiers(modifiers);  // Returns new instance, doesn't mutate
method.WithBody(body);            // 'method' is unchanged

// ✅ Correct - chain modifications
var method = MethodDeclaration(returnType, name)
    .WithModifiers(modifiers)
    .WithBody(body);
```

### 2. SyntaxFactory Only - No String Templating

**Critical Rule:** Never generate C# code using strings. Always use `SyntaxFactory`:

```csharp
// ❌ NEVER DO THIS
var code = $"public static {returnType} {methodName}() {{ return {value}; }}";

// ✅ ALWAYS DO THIS
return MethodDeclaration(
    PredefinedType(Token(SyntaxKind.IntKeyword)),
    Identifier("GetValue")
)
.WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
.WithBody(Block(
    ReturnStatement(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(42)))
));
```

**Why?** String templating leads to:
- Syntax errors
- Injection vulnerabilities
- Unmaintainable code
- No compiler guarantees

### 3. Context Flags for Nested Semantics

The `_isInAbstractClass` flag (line 76 in main RoslynEmitter.cs) tracks context:

```csharp
// Set flag when generating abstract class
_isInAbstractClass = classDef.Decorators.Any(d => d.Name == "abstract");

// Use flag to change behavior
if (_isInAbstractClass && methodBody is EllipsisExpression)
{
    // In abstract class, ellipsis means abstract method
    return GenerateAbstractMethod(method);
}

// Restore previous context
_isInAbstractClass = wasInAbstractClass;
```

This pattern avoids threading context through every method call.

### 4. Decorator → Modifier Mapping

Sharpy uses Python-style decorators; C# uses keywords. The mapping is intentional:

```python
@staticmethod    # Python decorator
@abstract        # Python decorator
@override        # Python decorator
```

```csharp
static           // C# keyword
abstract         // C# keyword
override         // C# keyword
```

Default behavior:
- **Module-level functions:** Automatically `static` (they're in a generated module class)
- **Class members:** No automatic `static` (instance methods by default)
- **Types:** Automatically `public` if no access modifier specified

### 5. String Enums as Sealed Classes

C# doesn't have native string enums. Sharpy emulates them:

```csharp
public sealed class MyEnum
{
    public static readonly string Value1 = "value1";
    public static readonly string Value2 = "value2";
}
```

This design:
- ✅ Provides type safety (can't pass arbitrary strings)
- ✅ Works with switch statements (pattern matching)
- ✅ Maintains Python-like ergonomics
- ❌ Can't be used in attributes (C# limitation)
- ❌ Not a "real" enum (can't use `Enum.GetValues()`)

### 6. Symbol Resolution: CodeGenInfo vs. Runtime Tracking

**Before (Old Design):**
```csharp
// Track everything in HashSets during emission
_classNames.Add(className);
_structNames.Add(structName);
_stringEnumNames.Add(enumName);
```

**After (Current Design):**
```csharp
// Use CodeGenInfo computed during semantic analysis
var symbol = _context.LookupSymbol(name);
if (symbol?.CodeGenInfo?.IsStringEnum == true)
{
    // Handle string enum
}
```

**Why?** Moves complexity upstream to semantic analysis where it belongs. Emission should be a pure transformation with minimal state.

**Exception:** Local variables still use runtime tracking (`_declaredVariables`, `_variableVersions`) because local redeclarations happen during emission.

---

## Debugging Tips

### 1. Inspect Generated C# Code

Use the CLI to emit C# before compilation:

```bash
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy
```

This shows the exact C# code generated, making issues obvious.

### 2. Verify Type Mappings

Check that type annotations are correctly mapped:

```bash
dotnet run --project src/Sharpy.Cli -- emit ast file.spy
```

Look at the `TypeAnnotation` nodes in the AST. If they're wrong, the issue is in the parser or semantic analysis, not code generation.

### 3. Breakpoint Strategy

When debugging this file, set breakpoints at:
- **Line 17** (`GenerateFunctionDeclaration`): Catch all function generation
- **Line 213** (`GenerateClassDeclaration`): Catch all class generation
- **Line 540** (`GenerateEnumDeclaration`): Catch enum type detection
- **Line 296** (`CollectInterfaceMethodDefs`): Debug abstract stub generation

### 4. Common Issues and Fixes

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| "Duplicate method definition" | Variable versioning not cleared | Ensure `_declaredVariables.Clear()` at function start (line 20) |
| "Cannot convert type" | Type mapping error | Check `TypeMapper.MapType()` for the specific type |
| "Main method conflict" | Multiple entry points | Check `_context.IsEntryPoint` logic (line 26) |
| "Abstract method not generated" | Interface not in `_interfaceDefinitions` | Verify interface was visited earlier in pipeline |
| Missing XML documentation | Docstring not extracted | Check parser extracted `DocString` from AST |

### 5. Test File-Based Integration Tests

The best way to verify correctness:

```bash
# Run file-based tests that cover type declarations
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

Add new `.spy` + `.expected` pairs in `Integration/TestFixtures/` to test new features.

---

## Contribution Guidelines

### What Changes Might Be Made to This File?

1. **New Decorator Support**
   - Add mapping in `GenerateModifiersFromDecorators()` or `GenerateTypeModifiersFromDecorators()`
   - Example: `@readonly` → `readonly` for structs

2. **Enhanced Generic Constraints**
   - Extend `GenerateConstraintClauses()` to support new constraint types
   - Example: `unmanaged` constraint for native interop

3. **New Type Kinds**
   - Add new `Generate*Declaration()` method
   - Example: Records, delegates, etc.

4. **Improved Name Mangling**
   - Modify `NameMangler` calls or add special cases
   - Example: Preserve certain naming patterns

5. **Better Docstring Handling**
   - Enhance `GenerateXmlDocComment()` to parse Python docstring formats
   - Example: Parse parameter descriptions, return types, raises sections

### Rules for Changes

1. **Always Use SyntaxFactory**
   - Never use string interpolation for code generation
   - Build syntax trees using Roslyn APIs

2. **Test with Python First**
   - Verify expected behavior: `python3 -c "..."`
   - Match Python semantics, not what "feels right"

3. **Maintain Immutability**
   - Don't mutate syntax nodes
   - Chain `.With*()` calls to build up nodes

4. **Add Integration Tests**
   - Create `.spy` + `.expected` pairs in `TestFixtures/`
   - Verify the full pipeline (not just code generation)

5. **Update Documentation**
   - If you add new behavior, update this walkthrough
   - Add comments explaining non-obvious design decisions

### Example: Adding a New Decorator

**Task:** Support `@sealed` decorator on methods (prevent overriding).

**Step 1:** Add to `GenerateModifiersFromDecorators()`:

```csharp
case "sealed":
    tokens.Add(Token(SyntaxKind.SealedKeyword));
    break;
```

**Step 2:** Test it:

```python
# test_sealed_method.spy
class Base:
    @sealed
    @virtual
    def process(self) -> None:
        pass
```

Expected C#:

```csharp
public class Base
{
    public virtual sealed void Process() { }
}
```

**Step 3:** Add integration test:

```bash
# Create test files
echo "class Base:\n    @sealed\n    @virtual\n    def process(self) -> None:\n        pass" > \
    test_sealed_method.spy

echo "" > test_sealed_method.expected  # Or expected output if run
```

---

## Cross-References

### Related Partial Class Files

This file is part of the `RoslynEmitter` partial class split across multiple files:

- **[RoslynEmitter.cs](./RoslynEmitter.md)** - Core emitter, field definitions, symbol resolution
- **[RoslynEmitter.ClassMembers.cs](./RoslynEmitter.ClassMembers.md)** - Generating members within classes (methods, properties, fields)
- **[RoslynEmitter.Expressions.cs](./RoslynEmitter.Expressions.md)** - Expression code generation
- **[RoslynEmitter.Statements.cs](./RoslynEmitter.Statements.md)** - Statement code generation (if/while/for/match)
- **[RoslynEmitter.CompilationUnit.cs](./RoslynEmitter.CompilationUnit.md)** - Top-level module generation
- **[RoslynEmitter.Operators.cs](./RoslynEmitter.Operators.md)** - Operator overload generation

### Key Dependencies

- **[TypeMapper.md](./TypeMapper.md)** - Understanding how types are mapped
- **[NameMangler.md](./NameMangler.md)** - Understanding name transformations
- **[CodeGenContext.md](./CodeGenContext.md)** - Understanding the compilation context

### Upstream Components

- **[Semantic/TypeChecker.md](../Semantic/TypeChecker.md)** - Produces typed AST consumed by this file
- **[Semantic/NameResolver.md](../Semantic/NameResolver.md)** - Resolves symbols used during code generation
- **[Parser/Parser.md](../Parser/Parser.md)** - Produces AST nodes transformed here

### Language Specification

- **[docs/language_specification/type_annotations.md](../../../../language_specification/type_annotations.md)** - Type annotation syntax
- **[docs/language_specification/type_hierarchy.md](../../../../language_specification/type_hierarchy.md)** - Inheritance rules
- **[docs/language_specification/dotnet_interop.md](../../../../language_specification/dotnet_interop.md)** - .NET integration

---

## Summary

`RoslynEmitter.TypeDeclarations.cs` is the bridge between Python-style type declarations and C# type system. It demonstrates:

- **Roslyn mastery**: Exclusive use of `SyntaxFactory` for syntactically correct C# generation
- **Semantic awareness**: Leverages `CodeGenInfo` from upstream semantic analysis
- **Python fidelity**: Carefully maps Python conventions to idiomatic C#
- **Compiler craftsmanship**: Handles edge cases (entry points, abstract stubs, string enums)

Understanding this file requires knowledge of:
1. **Roslyn syntax APIs** - How C# syntax trees are constructed
2. **Sharpy semantics** - What Python-like features mean in .NET context
3. **Compiler pipeline** - How this fits between semantic analysis and final compilation

When working on this file, always verify against real Python behavior and ensure generated C# is idiomatic and efficient.
