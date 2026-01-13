# Walkthrough: RoslynEmitter.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

---

## 1. Overview

`RoslynEmitter` is the **final stage** of the Sharpy compiler pipeline, responsible for transforming the typed Abstract Syntax Tree (AST) into C# source code. It uses the **Roslyn Syntax API** (Microsoft.CodeAnalysis.CSharp) to generate well-formed C# syntax trees programmatically—no string templating!

### Position in the Pipeline

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# Source
                                              ↓                   ↓
                                     SemanticInfo +          CompilationUnitSyntax
                                      Typed AST
```

**Upstream Input:**
- `Module` AST node (root of the parsed source)
- `SemanticInfo` with resolved types and symbols (from Semantic Analysis)

**Downstream Output:**
- `CompilationUnitSyntax` (Roslyn syntax tree) → serialized to C# source code
- Ready for .NET compilation via Roslyn or `dotnet build`

### Key Responsibilities

- Generate C# compilation units (files) from Sharpy modules
- Transform Sharpy naming conventions to C# conventions (snake_case → PascalCase/camelCase)
- Map Sharpy types to .NET types via `TypeMapper`
- Generate C# class structures, methods, properties, and operators
- Handle Python-style constructs (list comprehensions, tuple unpacking, dunder methods)
- Emit idiomatic C# code that interoperates with .NET

---

## 2. Class/Type Structure

### Main Class: `RoslynEmitter`

```csharp
public class RoslynEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;
    private readonly HashSet<string> _declaredVariables;
    private readonly Dictionary<string, int> _variableVersions;
    private int _tempVarCounter;
    private static readonly HashSet<string> UpperCaseAcronyms;
}
```

### Field Reference

| Field | Type | Purpose |
|-------|------|---------|
| `_context` | `CodeGenContext` | Compilation state: symbol table, builtins, source paths, project namespace |
| `_typeMapper` | `TypeMapper` | Converts Sharpy types to C# type syntax (e.g., `list[int]` → `List<int>`) |
| `_declaredVariables` | `HashSet<string>` | Tracks variables declared in current scope to avoid redeclaration |
| `_variableVersions` | `Dictionary<string, int>` | Supports Python-style variable redefinition (shadowing with different types) |
| `_tempVarCounter` | `int` | Counter for generating unique temporary variable names |

### Static Data

```csharp
private static readonly HashSet<string> UpperCaseAcronyms = new(...)
{
    "io", "ui", "xml", "html", "api", "sql", "db", "http", "ftp",
    "smtp", "tcp", "udp", "ip", "uri", "url", "json", "csv", "guid"
};
```

Used for namespace conversions—ensures `system.io` becomes `System.IO` rather than `System.Io`.

---

## 3. Key Functions/Methods

### 3.1 Entry Point: `GenerateCompilationUnit(Module module)`

**Signature:**
```csharp
public CompilationUnitSyntax GenerateCompilationUnit(Module module)
```

**What it does:** Orchestrates the entire code generation process, producing a complete C# file.

**Generated Structure:**
```csharp
// Generated C# structure:
using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace ProjectNamespace.FileName;

public static class Exports
{
    // Module-level functions become static methods
    // Classes remain classes
    // Module-level executable code goes into Main()
}
```

**Key Steps:**
1. Collects `using` directives from Sharpy import statements
2. Separates imports from executable/declarative statements
3. Generates file-scoped namespace from source file path
4. Wraps module content in `public static class Exports`
5. Creates `Main()` method if entry point and has executable statements

**Design Note:** All module code goes into a static `Exports` class to match Python's module-level execution model while maintaining .NET conventions.

---

### 3.2 Namespace Generation

```csharp
private NameSyntax GenerateNamespaceName()
private NameSyntax GenerateProjectNamespace()
```

**Dual Mode Operation:**

| Mode | When Used | Example Input | Example Output |
|------|-----------|---------------|----------------|
| **Project** | `ProjectNamespace` + `ProjectRootPath` set | `src/myapp/utils.spy` in project "MyProject" | `MyProject.Myapp.Utils` |
| **Single-file** | No project context | `src/myapp/utils.spy` | `Myapp.Utils` |

**Transformation Rules:**
- Filters out common directories (`src`, `lib`, `.`)
- Applies `SimpleToPascalCase` to each path component
- Handles acronyms: `io` → `IO`, `http` → `HTTP`
- Falls back to `SharpyGenerated` if no path available

---

### 3.3 Import Handling: Using Directive Generation

```csharp
private List<UsingDirectiveSyntax> GenerateUsingDirectives(Module module)
private IEnumerable<UsingDirectiveSyntax> GenerateImportUsings(ImportStatement import)
private IEnumerable<UsingDirectiveSyntax> GenerateFromImportUsings(FromImportStatement fromImport)
```

**Default Imports Always Added:**
- `System`
- `System.Collections.Generic`
- `System.Linq`
- `global::Sharpy.Core` (runtime library)

**Sharpy → C# Mapping Examples:**

```python
# Sharpy
import system.io
from system.collections.generic import List
import mymodule as m
```

```csharp
// Generated C#
using System.IO;
using System.Collections.Generic;
using m = Mymodule;
```

**Why `global::Sharpy.Core`?** Prevents namespace conflicts when output namespace contains "Sharpy" (e.g., `namespace Sharpy.MyApp`).

---

### 3.4 Function Generation

```csharp
private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
```

**Transformation Example:**

```python
# Sharpy
@public
def calculate_sum(values: list[int]) -> int:
    """Calculate sum of values"""
    return sum(values)
```

```csharp
// Generated C#
/// <summary>
/// Calculate sum of values
/// </summary>
public static int CalculateSum(List<int> values)
{
    return global::Sharpy.Core.Exports.Sum(values);
}
```

**Key Behaviors:**

1. **Scope Reset:** Clears `_declaredVariables` and `_variableVersions` for fresh scope
2. **Name Mangling:** `calculate_sum` → `CalculateSum` via `NameMangler.Transform()`
3. **Parameter Tracking:** Registers parameters in `_variableVersions` for proper reference resolution
4. **Decorator → Modifier Mapping:**

| Sharpy Decorator | C# Modifier |
|-----------------|-------------|
| `@public` | `public` (default) |
| `@private` | `private` |
| `@protected` | `protected` |
| `@internal` | `internal` |
| `@staticmethod` / `@static` | `static` |
| `@abstractmethod` / `@abstract` | `abstract` |
| `@virtual` | `virtual` |
| `@override` | `override` |

5. **Default Static:** Module-level functions default to `public static`
6. **Docstring → XML Doc:** Python docstrings become C# XML documentation comments

---

### 3.5 Class Generation

```csharp
private ClassDeclarationSyntax GenerateClassDeclaration(ClassDef classDef)
private List<MemberDeclarationSyntax> GenerateClassMembers(List<Statement> body, string className)
```

**Two-Pass Member Generation:**

1. **First Pass:** Generate fields, build name mapping dictionary
2. **Second Pass:** Generate methods, constructors, operators (using field mapping)

**Why Two Passes?** Ensures consistent field naming in constructors that reference fields declared later in source order.

**Example Transformation:**

```python
# Sharpy
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __add__(self, other: Point) -> Point:
        return Point(self.x + other.x, self.y + other.y)
```

```csharp
// Generated C#
public class Point
{
    public int X;
    public int Y;

    public Point(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }

    public Point __Add__(Point other)
    {
        return new Point(this.X + other.X, this.Y + other.Y);
    }

    public static Point operator +(Point left, Point right)
    {
        return left.__Add__(right);
    }
}
```

**Special Handling:**
- `__init__` → Constructor
- Dunder methods → Both method AND operator overload (where applicable)
- Automatic complementary operators (`__eq__` alone generates both `==` and `!=`)

---

### 3.6 Constructor Generation

```csharp
private ConstructorDeclarationSyntax GenerateConstructor(
    FunctionDef func,
    string className,
    Dictionary<string, string> fieldMapping)
```

**Special `self` Assignment Handling:**

```python
# Sharpy
def __init__(self, name: str, age: int):
    self.name = name
    self.age = age
```

```csharp
// Generated C#
public MyClass(string name, int age)
{
    this.Name = name;
    this.Age = age;
}
```

**Key Logic:**
- Skips `self` parameter from parameter list
- Detects `self.field = value` patterns
- Uses `fieldMapping` dictionary to ensure consistent field names (PascalCase)
- Maps parameter references to their mangled forms

---

### 3.7 Assignment Handling

```csharp
private StatementSyntax GenerateAssignment(Assignment assign)
private string GetMangledVariableName(string name, bool isNewDeclaration)
```

**Sharpy's Variable Redefinition Semantics:**

Python allows reassigning variables with different types. C# doesn't, so the emitter creates versioned variables:

```python
# Sharpy
x = 5         # int
x = "hello"   # string - this shadows!
print(x)      # references the string version
```

```csharp
// Generated C#
var x = 5;
var x_1 = "hello";
Console.WriteLine(x_1);
```

**Supported Assignment Targets:**

| Target Type | Sharpy | C# |
|-------------|--------|-----|
| Simple identifier | `x = value` | `var x = value;` or `x = value;` |
| Index access | `arr[0] = value` | `arr[0] = value;` |
| Member access | `obj.field = value` | `obj.Field = value;` |
| Tuple unpacking | `x, y = (1, 2)` | `var (x, y) = (1, 2);` |

**Augmented Assignment Handling:**

```python
x += 10      # Sharpy
x **= 2      # Power assign
x //= 3      # Floor divide assign
```

```csharp
x = x + 10;              // Standard operators
x = Math.Pow(x, 2);      // Power requires method call
x = (int)(x / 3);        // Floor divide requires cast
```

---

### 3.8 Statement Generation

```csharp
private StatementSyntax? GenerateBodyStatement(Statement stmt)
```

**Dispatcher Pattern:** Routes each AST statement type to its specific generator.

**Supported Statement Types:**

| Sharpy Statement | C# Output | Generator Method |
|-----------------|-----------|------------------|
| `return expr` | `return expr;` | `GenerateReturn()` |
| `x = value` | `var x = value;` | `GenerateAssignment()` |
| `x: int = 5` | `int x = 5;` | `GenerateVariableDeclaration()` |
| `if/elif/else` | `if/else if/else` | `GenerateIf()` |
| `while cond:` | `while (cond)` | `GenerateWhile()` |
| `for x in items:` | `foreach (var x in items)` | `GenerateFor()` |
| `try/except/finally` | `try/catch/finally` | `GenerateTry()` |
| `raise Exception()` | `throw new Exception();` | `GenerateRaise()` |
| `assert cond, msg` | `Debug.Assert(cond, msg);` | `GenerateAssert()` |
| `pass` | `;` (empty statement) | — |
| `break` | `break;` | — |
| `continue` | `continue;` | — |

---

### 3.9 Expression Generation

```csharp
private ExpressionSyntax GenerateExpression(Expression expr)
```

**Massive switch expression** handling all expression types:

#### Literals

| Sharpy | C# |
|--------|-----|
| `42` | `42` |
| `3.14` | `3.14` |
| `"hello"` | `"hello"` |
| `True` / `False` | `true` / `false` |
| `None` | `null` |
| `...` | `throw new NotImplementedException()` |

#### Collection Literals

```python
# Sharpy
[1, 2, 3]
{"a": 1, "b": 2}
{1, 2, 3}
(1, 2, 3)
```

```csharp
// C#
new global::Sharpy.Core.List<int> { 1, 2, 3 }
new global::Sharpy.Core.Dict<string, int> { { "a", 1 }, { "b", 2 } }
new global::Sharpy.Core.Set<int> { 1, 2, 3 }
(1, 2, 3)  // C# ValueTuple
```

#### Comprehensions → LINQ

```python
[x * 2 for x in items if x > 0]
```

```csharp
items.Where(x => x > 0).Select(x => x * 2).ToList()
```

**Current Limitation:** Only single `for` clause supported (no nested comprehensions).

#### Special Binary Operators

| Sharpy | C# | Notes |
|--------|-----|-------|
| `x ** y` | `Math.Pow(x, y)` | No power operator in C# |
| `x // y` | `(int)(x / y)` | Floor division |
| `x in y` | `y.__Contains__(x)` | Membership test |
| `x not in y` | `!y.__Contains__(x)` | Negated membership |
| `x is None` | `x == null` | Optimized null check |
| `x is y` | `object.ReferenceEquals(x, y)` | Identity check |
| `x is not y` | `!object.ReferenceEquals(x, y)` | Negated identity |

#### F-Strings

```python
f"Hello {name}, you are {age} years old"
```

```csharp
$"Hello {name}, you are {age} years old"
```

---

### 3.10 Operator Overload Generation

```csharp
private MemberDeclarationSyntax? TryGenerateOperatorOverload(FunctionDef funcDef, string className)
private OperatorDeclarationSyntax GenerateBinaryOperator(...)
private OperatorDeclarationSyntax GenerateComparisonOperator(...)
private OperatorDeclarationSyntax GenerateUnaryOperator(...)
```

**When a dunder method is found, TWO members are generated:**
1. The method itself (e.g., `__Add__` with preserved dunder naming)
2. The C# operator (e.g., `operator +`)

**Supported Operator Mappings:**

| Category | Dunder Methods | C# Operators |
|----------|----------------|--------------|
| **Arithmetic** | `__add__`, `__sub__`, `__mul__`, `__truediv__`, `__mod__` | `+`, `-`, `*`, `/`, `%` |
| **Bitwise** | `__and__`, `__or__`, `__xor__`, `__lshift__`, `__rshift__` | `&`, `|`, `^`, `<<`, `>>` |
| **Comparison** | `__eq__`, `__ne__`, `__lt__`, `__le__`, `__gt__`, `__ge__` | `==`, `!=`, `<`, `<=`, `>`, `>=` |
| **Unary** | `__neg__`, `__pos__`, `__invert__` | `-`, `+`, `~` |

**Unsupported (method only):**
- `__pow__` → No `**` operator in C#, use `Math.Pow()`
- `__getitem__` / `__setitem__` → Requires indexer syntax

**Complementary Operator Generation:**

C# requires both `==` and `!=` if either is defined. The emitter auto-generates:

```csharp
// If only __eq__ is defined:
public static bool operator !=(Point left, Point right)
{
    return !(left == right);
}
```

---

## 4. Dependencies

### Internal Dependencies

| Dependency | File | Purpose |
|------------|------|---------|
| `CodeGenContext` | `CodeGenContext.cs` | Compilation state, symbol table, builtin registry |
| `TypeMapper` | `TypeMapper.cs` | Sharpy type → C# type conversion (generics, nullability) |
| `NameMangler` | `NameMangler.cs` | Naming convention transformation (snake_case → PascalCase) |
| `ProtocolRegistry` | `Semantic/ProtocolRegistry.cs` | Dunder method metadata (return types, CLR mappings) |

### AST Dependencies (from `Sharpy.Compiler.Parser.Ast`)

- Root: `Module`
- Declarations: `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
- Statements: `Assignment`, `ReturnStatement`, `IfStatement`, `WhileStatement`, `ForStatement`, `TryStatement`, etc.
- Expressions: `BinaryOp`, `UnaryOp`, `FunctionCall`, `ListLiteral`, `ComparisonChain`, etc.

### External Dependencies (NuGet)

| Package | Namespace | Usage |
|---------|-----------|-------|
| Microsoft.CodeAnalysis.CSharp | `Microsoft.CodeAnalysis.CSharp` | Roslyn syntax tree API |
| — | `Microsoft.CodeAnalysis.CSharp.Syntax` | Syntax node types |
| — | `static SyntaxFactory` | Fluent API for building syntax |

---

## 5. Patterns and Design Decisions

### Pattern: Roslyn Fluent Builder API

Instead of error-prone string concatenation:

```csharp
// BAD (string templating):
var code = $"public {returnType} {methodName}({parameters}) {{ {body} }}";

// GOOD (Roslyn API):
var method = MethodDeclaration(returnType, methodName)
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithParameterList(ParameterList(SeparatedList(parameters)))
    .WithBody(Block(bodyStatements));
```

**Benefits:**
- Type-safe code generation
- Automatic syntax validation
- Consistent formatting via `.NormalizeWhitespace()`

### Pattern: Switch Expression Dispatching

Instead of visitor pattern, uses exhaustive `switch` expressions:

```csharp
return stmt switch
{
    ReturnStatement ret => GenerateReturn(ret),
    Assignment assign => GenerateAssignment(assign),
    // ... more cases
    _ => null
};
```

**Benefits:** Concise, compiler-enforced exhaustiveness, easy to extend.

### Design Decision: Static Exports Class

All module content goes into `public static class Exports`:
- Simplifies interop (everything callable as `Namespace.Exports.Method()`)
- Mirrors Python's module-as-namespace semantics
- Allows module-level statements to go into `Main()`

### Design Decision: Dunder Dual Generation

Dunder methods generate **both** a method and an operator:

```csharp
// From __add__(self, other):
public Point __Add__(Point other) { ... }           // Method (preserves dunder name)
public static Point operator +(Point left, Point right)
{
    return left.__Add__(right);  // Delegates to method
}
```

**Rationale:**
- Operators for natural syntax: `p1 + p2`
- Methods for explicit calls: `p1.__Add__(p2)`
- Avoids conflicts with user-defined `Add()` methods
- Single source of truth (operator delegates to method)

### Design Decision: Variable Versioning

Python allows variable redefinition; C# doesn't. Solution: generate versioned names.

Tracked via `_variableVersions[baseName] = version`:
- Version 0: `x`
- Version 1: `x_1`
- Version 2: `x_2`

Scope is reset (`Clear()`) at each function boundary.

---

## 6. Debugging Tips

### Viewing Generated C# Code

```bash
# Enable emit-csharp flag:
dotnet run --project src/Sharpy.Cli -- build file.spy --emit-csharp
```

### Inspecting Syntax Trees

Add this to inspect a generated node:
```csharp
var method = GenerateFunctionDeclaration(funcDef);
Console.WriteLine(method.NormalizeWhitespace().ToFullString());
```

### AST Dumping (Before Code Gen)

```csharp
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

### Key Breakpoint Locations

1. `GenerateCompilationUnit()` — Entry point
2. `GenerateBodyStatement()` — Statement dispatch
3. `GenerateExpression()` — Expression dispatch
4. `GetMangledVariableName()` — Variable versioning
5. Specific `Generate*()` methods for the construct you're debugging

### Common Issues and Solutions

| Issue | Cause | Fix |
|-------|-------|-----|
| "Identifier expected" | Invalid C# identifier (Python keyword) | Check `NameMangler.EscapeKeywordIfNeeded()` |
| "Type not found" | Missing using or incorrect namespace | Debug `GenerateUsingDirectives()` |
| Variable redefinition error | Versioning not working | Check `_variableVersions` in `GenerateAssignment()` |
| Operator overload missing | Dunder not recognized | Add case to `TryGenerateOperatorOverload()` |
| Wrong field name in constructor | Field mapping mismatch | Verify `GenerateClassMembers()` first pass |

### Roslyn Syntax Visualizer

For complex syntax issues, use the **Roslyn Syntax Visualizer** VS extension to inspect tree structure.

---

## 7. Contribution Guidelines

### Adding a New Statement Type

1. **Add case to `GenerateBodyStatement()`:**
```csharp
MyNewStatement newStmt => GenerateMyNewStatement(newStmt),
```

2. **Implement generator:**
```csharp
private StatementSyntax GenerateMyNewStatement(MyNewStatement stmt)
{
    // Use Roslyn SyntaxFactory methods
    return /* C# statement syntax */;
}
```

3. **Add tests** in `Sharpy.Compiler.Tests/CodeGen/`

### Adding a New Expression Type

1. **Add case to `GenerateExpression()`:**
```csharp
MyNewExpression expr => GenerateMyNewExpression(expr),
```

2. **Implement generator:**
```csharp
private ExpressionSyntax GenerateMyNewExpression(MyNewExpression expr)
{
    var operands = expr.Operands.Select(GenerateExpression);
    return /* C# expression syntax */;
}
```

### Adding a New Operator Mapping

1. **Add case to `TryGenerateOperatorOverload()`:**
```csharp
"__my_op__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.MyToken),
```

2. **If no C# operator exists**, generate method call instead (like `Math.Pow()` for `__pow__`)

### Extending Comprehension Support

**Current limitation:** Only single `for` clause.

```python
[x for x in items]              # ✅ Supported
[x for x in items if x > 0]     # ✅ Supported
[x for x in items for y in x]   # ❌ Not yet
```

**To add nested comprehension support:**
1. Detect multiple `ForClause` in `GenerateListComprehension()`
2. Generate nested `SelectMany()` LINQ calls

### Best Practices

1. **Always use Roslyn SyntaxFactory** — Never string templates
2. **Test edge cases**: Empty collections, null values, single-element cases
3. **Match Python semantics** — Run equivalent Python to verify behavior
4. **Add XML doc comments** for complex generators
5. **Use `NormalizeWhitespace()`** on final output
6. **Respect naming conventions**:
   - Methods: PascalCase
   - Parameters: camelCase
   - Public fields: PascalCase
   - Constants: UPPER_SNAKE_CASE

### Testing

```bash
# Unit tests for code generation
dotnet test --filter "FullyQualifiedName~CodeGen"

# Integration tests (compile and run generated C#)
dotnet test --filter "FullyQualifiedName~Integration"

# Manual inspection
dotnet run --project src/Sharpy.Cli -- build snippets/test.spy --emit-csharp
```

---

## Summary

`RoslynEmitter` is the bridge between Python-style Sharpy and idiomatic C#. It handles:

- **Syntax transformation**: Pythonic constructs → C# equivalents
- **Naming convention conversion**: snake_case → PascalCase/camelCase
- **Type mapping**: Sharpy types → .NET types via `TypeMapper`
- **Operator synthesis**: Dunder methods → C# operator overloads
- **Semantic preservation**: Python behavior expressed in C# constructs
- **Scope management**: Variable versioning for Python's redefinition semantics

The key to understanding this file is recognizing it's a **giant pattern matcher** that walks the AST and builds Roslyn syntax trees. Each `Generate*()` method handles one piece of the transformation puzzle.

---

## Related Documentation

- **Language Spec**: `docs/language_specification/dotnet_interop.md` — .NET interop semantics
- **Dependencies**:
  - `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/NameMangler.md`
  - `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/TypeMapper.md`
  - `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/CodeGenContext.md`
- **AST Reference**: `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Ast/`

---

**Next Steps for Newcomers:**
1. Read `NameMangler.cs` — understand naming transformations
2. Read `TypeMapper.cs` — understand type mappings
3. Explore `Parser/Ast/*.cs` — see input AST structure
4. Run integration tests — see end-to-end transformations
5. Try adding a simple feature (e.g., support for a new expression type)
