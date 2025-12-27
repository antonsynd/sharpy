# Walkthrough: RoslynEmitter.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

---

## Overview

`RoslynEmitter` is the **final stage** of the Sharpy compiler pipeline, responsible for transforming the Sharpy Abstract Syntax Tree (AST) into C# source code. It uses the **Roslyn Syntax API** (Microsoft.CodeAnalysis.CSharp) to generate well-formed C# syntax trees programmatically—no string templating!

**Key responsibilities:**
- Generate C# compilation units (files) from Sharpy modules
- Transform Sharpy naming conventions to C# conventions (snake_case → PascalCase/camelCase)
- Map Sharpy types to .NET types
- Generate C# class structures, methods, properties, and operators
- Handle Python-specific constructs (list comprehensions, tuple unpacking, dunder methods)
- Emit idiomatic C# code that interoperates with .NET

**Position in the pipeline:**
```
Lexer → Parser (AST) → Semantic Analysis → **RoslynEmitter** → C# Syntax Tree → Roslyn Compiler → IL
```

---

## Class Structure

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

**Fields:**
- **`_context`**: Contains compilation context (file paths, namespaces, builtin functions)
- **`_typeMapper`**: Handles Sharpy type → C# type mapping (e.g., `list[int]` → `Sharpy.Core.List<int>`)
- **`_declaredVariables`**: Tracks variables declared in current scope (for scope management)
- **`_variableVersions`**: Supports Python-style variable redefinition with type changes (e.g., `x = 5; x = "hello"` becomes `x`, `x_1`)
- **`_tempVarCounter`**: Counter for generating unique temporary variables
- **`UpperCaseAcronyms`**: Common acronyms that should remain uppercase (IO, HTTP, XML, etc.)

---

## Key Methods

### 1. Entry Point: `GenerateCompilationUnit(Module module)`

**What it does:** Generates the root C# file structure from a Sharpy module.

**Outputs:**
```csharp
// Generated C# structure:
using System;
using System.Collections.Generic;
using global::Sharpy.Core;

namespace GeneratedNamespace.FileName;

public static class Exports
{
    // Module-level functions and classes go here
}
```

**Key steps:**
1. Collects `using` directives from import statements
2. Generates a file-scoped namespace from the source file path
3. Wraps all module content in a `public static class Exports`
4. Handles module-level executable statements (creates `Main` if needed)

**Design note:** All module code goes into a static `Exports` class to match Python's module-level execution model while maintaining .NET conventions.

---

### 2. Namespace Generation: `GenerateNamespaceName()`

**What it does:** Converts Sharpy file paths to C# namespaces.

**Examples:**
```
File: src/myapp/utils.spy
Namespace: Myapp.Utils

File: calculator_app/core/operations.spy (in project)
Namespace: CalculatorApp.Core.Operations
```

**Logic:**
- Uses `ProjectNamespace` from context if available (for multi-file projects)
- Falls back to file-based namespace generation for single files
- Filters out common directories (`src`, `lib`, `.`)
- Applies `SimpleToPascalCase` to each path component
- Handles acronyms (e.g., `io` → `IO`)

---

### 3. Import Handling: `GenerateUsingDirectives(Module module)`

**What it does:** Transforms Sharpy import statements into C# using directives.

**Sharpy → C# mapping:**
```python
# Sharpy
import system.io
from system.collections.generic import List

# Generated C#
using System.IO;
using System.Collections.Generic;
```

**Key features:**
- Converts Python module naming to .NET namespaces
- Handles aliases: `import foo as bar` → `using bar = Foo;`
- Always includes default namespaces (System, System.Linq, global::Sharpy.Core)
- Deduplicates using statements

**Why `global::Sharpy.Core`?** Prevents conflicts when output namespace contains "Sharpy".

---

### 4. Function Generation: `GenerateFunctionDeclaration(FunctionDef func)`

**What it does:** Converts Sharpy functions to C# methods.

**Sharpy → C# transformation:**
```python
# Sharpy
@public
def calculate_sum(values: list[int]) -> int:
    """Calculate sum of values"""
    return sum(values)

# Generated C#
/// <summary>
/// Calculate sum of values
/// </summary>
public static int CalculateSum(List<int> values)
{
    return Exports.Sum(values);
}
```

**Key steps:**
1. **Name mangling**: `calculate_sum` → `CalculateSum` (PascalCase for methods)
2. **Parameter transformation**: Maps types, applies camelCase to parameter names
3. **Decorator processing**: `@public`, `@static`, `@abstractmethod` → C# modifiers
4. **Docstring conversion**: Python docstring → XML documentation comments
5. **Body generation**: Recursively generates statement syntax

**Scope management:** Clears `_declaredVariables` and `_variableVersions` for each function to start fresh.

---

### 5. Class Generation: `GenerateClassDeclaration(ClassDef classDef)`

**What it does:** Transforms Sharpy classes into C# classes.

**Example:**
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

# Generated C#
public class Point
{
    public int X;
    public int Y;
    
    public Point(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    
    public Point Add(Point other)
    {
        return new Point(this.X + other.X, this.Y + other.Y);
    }
    
    public static Point operator +(Point left, Point right)
    {
        return left.Add(right);
    }
}
```

**Key features:**
- **Two-pass member generation**: First collects fields, then methods (for field mapping in constructors)
- **Constructor handling**: `__init__` becomes a C# constructor
- **Dunder method mapping**: `__add__` → both `Add()` method and `operator +`
- **Operator synthesis**: Automatically generates complementary operators (if `__eq__` exists, generates `operator !=`)

---

### 6. Constructor Generation: `GenerateConstructor(FunctionDef func, string className, Dictionary<string, string> fieldMapping)`

**What it does:** Converts Python `__init__` to C# constructors.

**Special handling for `self` assignments:**
```python
# Sharpy
def __init__(self, name: str, age: int):
    self.name = name
    self.age = age

# Generated C#
public MyClass(string name, int age)
{
    this.Name = name;
    this.Age = age;
}
```

**Key logic:**
- Skips `self` parameter
- Detects `self.field = value` patterns
- Uses `fieldMapping` to ensure consistent field names (PascalCase)
- Maps parameter names to their mangled forms

---

### 7. Operator Overload Generation: `TryGenerateOperatorOverload(FunctionDef funcDef, string className)`

**What it does:** Generates C# operator overloads from Python dunder methods.

**Supported mappings:**
| Python Dunder | C# Operator | Method Generated |
|--------------|-------------|------------------|
| `__add__` | `operator +` | Binary |
| `__sub__` | `operator -` | Binary |
| `__mul__` | `operator *` | Binary |
| `__eq__` | `operator ==` | Comparison |
| `__ne__` | `operator !=` | Comparison |
| `__lt__` | `operator <` | Comparison |
| `__neg__` | `operator -` | Unary |
| `__invert__` | `operator ~` | Unary |

**Design pattern:**
```csharp
// Generated operator delegates to the dunder method:
public static Point operator +(Point left, Point right)
{
    return left.Add(right);  // Calls the __add__ → Add() method
}
```

**Why both method and operator?**
- Method (`Add()`) provides explicit access for non-operator contexts
- Operator enables natural syntax: `p1 + p2`
- Follows C# best practices

---

### 8. Statement Generation: `GenerateBodyStatement(Statement stmt)`

**What it does:** Dispatcher method that routes each AST statement type to its specific generator.

**Supported statement types:**
- `ReturnStatement` → `return expr;`
- `Assignment` → Variable declarations, assignments, tuple unpacking
- `IfStatement` → `if`/`else if`/`else` with proper nesting
- `WhileStatement` → `while` loops
- `ForStatement` → `foreach` loops (including tuple unpacking)
- `TryStatement` → `try`/`catch`/`finally`
- `RaiseStatement` → `throw`
- `AssertStatement` → `Debug.Assert()`

---

### 9. Assignment Handling: `GenerateAssignment(Assignment assign)`

**What it does:** Handles Python's flexible assignment semantics in C#.

**Key scenarios:**

#### A. Simple assignment with redefinition
```python
# Sharpy (Python allows type changes)
x = 5
x = "hello"  # Redefinition with different type

# Generated C#
var x = 5;
var x_1 = "hello";  // New variable to handle type change
```

#### B. Augmented assignment
```python
x += 10  # Sharpy

x = x + 10;  // C# (references current version)
```

#### C. Tuple unpacking
```python
x, y = (1, 2)  # Sharpy

var (x, y) = (1, 2);  // C# 7.0 tuple deconstruction
```

#### D. Index/member assignment
```python
arr[0] = 10
obj.field = 20
```

**Variable versioning:** The `_variableVersions` dictionary tracks the current "version" of each variable to support Python's redefinition semantics while maintaining C# type safety.

---

### 10. Expression Generation: `GenerateExpression(Expression expr)`

**What it does:** Massive dispatcher for all expression types. Converts Sharpy expressions to C# expression syntax.

**Notable transformations:**

#### Collection Literals
```python
# Sharpy
[1, 2, 3]
{"a": 1, "b": 2}
{1, 2, 3}
(1, 2, 3)

# C#
new global::Sharpy.Core.List<int> { 1, 2, 3 }
new global::Sharpy.Core.Dict<string, int> { { "a", 1 }, { "b", 2 } }
new global::Sharpy.Core.Set<int> { 1, 2, 3 }
(1, 2, 3)  // C# tuples
```

#### List Comprehensions → LINQ
```python
# Sharpy
[x * 2 for x in items if x > 0]

# C#
items.Where(x => x > 0).Select(x => x * 2).ToList()
```

#### Special Binary Operators
```python
# Python-specific operators mapped to C#:
x ** y        → Math.Pow(x, y)
x // y        → (int)(x / y)
x in y        → y.__Contains__(x)
x is None     → x == null
x is not None → x != null
```

#### F-Strings
```python
f"Hello {name}, you are {age} years old"

$"Hello {name}, you are {age} years old"  // C# interpolated string
```

---

### 11. Name Mangling: `GetMangledVariableName(string name, bool isNewDeclaration)`

**What it does:** Manages variable versioning to support Python's variable redefinition semantics.

**Example:**
```python
# Sharpy
x = 5
print(x)      # Uses "x"
x = "hello"   # Creates "x_1"
print(x)      # Uses "x_1"
```

**How it works:**
- `isNewDeclaration = true`: Increments version counter, returns `name` or `name_N`
- `isNewDeclaration = false`: Returns current version (`name` or `name_N`)
- Tracks versions per-scope (cleared on function entry)

---

### 12. Builtin Function Calls: `GenerateCall(FunctionCall call)`

**What it does:** Routes function calls to either Sharpy builtins or user functions.

```python
# Sharpy
print("hello")
len(my_list)
my_function(10)

# C#
global::Sharpy.Core.Exports.Print("hello");
global::Sharpy.Core.Exports.Len(myList);
MyFunction(10);
```

**Logic:**
- Checks `_context.IsBuiltinFunction(name)` to determine routing
- Builtin functions are fully qualified to avoid ambiguity
- User functions use `NameMangler.ToPascalCase()` for method naming

---

## Dependencies

### Internal (Same Project)
- **`CodeGenContext`**: Provides compilation context (paths, namespaces, builtin registry)
- **`TypeMapper`**: Maps Sharpy types to C# types (handles generics, nullability)
- **`NameMangler`**: Converts between naming conventions (snake_case, PascalCase, camelCase)
- **`Parser.Ast.*`**: All AST node types (Statement, Expression, Module, etc.)
- **`Semantic.SemanticInfo`**: Type information from semantic analysis
- **`ProtocolRegistry`**: Maps Python dunder methods to .NET protocols

### External (NuGet)
- **`Microsoft.CodeAnalysis.CSharp`**: Roslyn syntax tree API
- **`Microsoft.CodeAnalysis.CSharp.Syntax`**: Syntax node types
- **`System.Linq`**: LINQ for comprehension generation

---

## Patterns and Design Decisions

### 1. **Roslyn Syntax Factory Pattern**
Instead of string concatenation, uses Roslyn's fluent API:
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

---

### 2. **Two-Pass Class Member Generation**
Classes are generated in two passes:
1. **First pass**: Collect all fields and build field mapping
2. **Second pass**: Generate methods/constructors using the field mapping

**Why?** Ensures consistent field naming in constructors that reference fields declared later in source order.

---

### 3. **Dunder Method Dual Generation**
Python dunder methods generate **both** a C# method and an operator:
```csharp
// From: __add__(self, other)
public Point Add(Point other) { ... }           // Explicit method
public static operator +(Point left, Point right) // Operator overload
{
    return left.Add(right);  // Delegates to method
}
```

**Rationale:**
- Operators for natural syntax: `p1 + p2`
- Methods for explicit calls: `p1.Add(p2)`
- Operator delegates to method (single source of truth)

---

### 4. **Variable Versioning for Redefinition**
Python allows variable redefinition with type changes:
```python
x = 5      # int
x = "hi"   # str (same variable, new type)
```

C# doesn't allow this, so RoslynEmitter generates:
```csharp
var x = 5;
var x_1 = "hi";
```

Tracked via `_variableVersions` dictionary.

---

### 5. **LINQ for Comprehensions**
List/set/dict comprehensions map naturally to LINQ:
```python
[f(x) for x in items if condition(x)]
↓
items.Where(x => condition(x)).Select(x => f(x)).ToList()
```

**Limitation:** Nested comprehensions (multiple `for` clauses) not yet supported.

---

### 6. **Namespace Collision Prevention**
Uses `global::Sharpy.Core` prefix to avoid namespace conflicts:
```csharp
// If output namespace is "Sharpy.MyApp", prevent ambiguity:
using global::Sharpy.Core;  // Explicitly reference root namespace
```

---

## Debugging Tips

### 1. **Viewing Generated C# Code**
Enable emit-csharp flag to see output:
```bash
dotnet run --project src/Sharpy.Cli -- build file.spy --emit-csharp
```

### 2. **AST Dumping**
Add this to inspect AST before code gen:
```csharp
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

### 3. **Breakpoint Locations**
Set breakpoints in:
- `GenerateCompilationUnit()` - Entry point
- `GenerateBodyStatement()` - Statement dispatch
- `GenerateExpression()` - Expression dispatch
- Specific generators for the construct you're debugging

### 4. **Common Issues**

**Issue:** "Identifier expected" compilation error in generated C#
**Cause:** Invalid C# identifier (e.g., Python keyword like `class`)
**Fix:** Check `NameMangler.Transform()` escapes reserved words

**Issue:** "Type or namespace not found"
**Cause:** Missing using directive or incorrect namespace
**Fix:** Debug `GenerateUsingDirectives()` and `GenerateNamespaceName()`

**Issue:** Variable redefinition error in C#
**Cause:** Variable versioning not working
**Fix:** Check `_variableVersions` dictionary state in `GenerateAssignment()`

**Issue:** Operator overload not generated
**Cause:** Dunder method not recognized
**Fix:** Ensure dunder method is in `TryGenerateOperatorOverload()` switch

---

## Contribution Guidelines

### Adding Support for a New Sharpy Statement

1. **Add case to `GenerateBodyStatement()`:**
```csharp
MyNewStatement newStmt => GenerateMyNewStatement(newStmt),
```

2. **Implement generator method:**
```csharp
private StatementSyntax GenerateMyNewStatement(MyNewStatement stmt)
{
    // Use Roslyn SyntaxFactory methods
    return /* C# statement syntax */;
}
```

3. **Add tests in `Sharpy.Compiler.Tests/CodeGen/`**

---

### Adding Support for a New Expression Type

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

---

### Adding a New Operator Mapping

1. **Add case to `TryGenerateOperatorOverload()`:**
```csharp
"__my_op__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.MyToken),
```

2. **Ensure operator is in C# (not all Python operators exist in C#)**
   - If no direct mapping: Generate method call instead
   - Example: `__pow__` has no `**` operator in C#, use `Math.Pow()`

---

### Extending Comprehension Support

**Current limitation:** Only single `for` clause supported
```python
[x for x in items]  # ✅ Supported
[x for x in items if x > 0]  # ✅ Supported
[x for x in items for y in x]  # ❌ Not yet
```

**To add nested comprehension support:**
1. Detect multiple `ForClause` in `GenerateListComprehension()`
2. Generate nested `SelectMany()` LINQ calls:
```csharp
items.SelectMany(x => x.Select(y => /* expr */))
```

---

### Handling New Python Constructs

When adding support for Python features without C# equivalents:

**Option A: Approximate with C# features**
- Example: `with` statement → `using` statement (for IDisposable)

**Option B: Generate helper method calls**
- Example: `x ** y` → `Math.Pow(x, y)`

**Option C: Emit to Sharpy.Core runtime**
- Example: Slicing → `Sharpy.Core.Slice(obj, start, stop, step)`

---

### Best Practices

1. **Always use Roslyn SyntaxFactory** - Never string templates
2. **Test with edge cases**:
   - Empty collections
   - Null values
   - Single-element collections
   - Deeply nested structures
3. **Match Python semantics** - Run equivalent Python code to verify behavior
4. **Add XML doc comments** for complex generators
5. **Use `NormalizeWhitespace()`** on final output for consistent formatting
6. **Respect naming conventions**:
   - Methods: PascalCase
   - Parameters: camelCase
   - Fields: PascalCase (C# public field convention)
   - Constants: UPPER_SNAKE_CASE

---

## Testing Your Changes

### Unit Tests
```bash
# Test code generation
dotnet test --filter "FullyQualifiedName~CodeGen"
```

### Integration Tests
```bash
# Compile and execute generated C#
dotnet test --filter "FullyQualifiedName~Integration"
```

### Manual Testing
```bash
# Compile a sample file and inspect output
dotnet run --project src/Sharpy.Cli -- build snippets/test.spy --emit-csharp
```

---

## Summary

`RoslynEmitter` is the bridge between Python-style Sharpy and idiomatic C#. It handles:
- **Syntax transformation** (Pythonic → C#-style)
- **Naming convention conversion** (snake_case → PascalCase/camelCase)
- **Type mapping** (Sharpy types → .NET types)
- **Operator synthesis** (dunder methods → C# operators)
- **Semantic preservation** (Python behavior in C# constructs)

The key to understanding this file is recognizing that it's a **giant pattern matcher** that walks the AST and builds Roslyn syntax trees. Each `Generate*()` method handles one piece of the transformation puzzle, using Roslyn's fluent API to ensure type-safe, syntactically correct C# code generation.

**Next steps for newcomers:**
1. Read `NameMangler.cs` to understand naming transformations
2. Read `TypeMapper.cs` to understand type mappings
3. Explore `Parser/Ast/*.cs` to see input AST structure
4. Run integration tests to see end-to-end transformations
5. Try adding a simple statement type (e.g., `print` statement)
