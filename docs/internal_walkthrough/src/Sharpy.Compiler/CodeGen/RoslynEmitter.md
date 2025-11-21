# Walkthrough: RoslynEmitter.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

---

## Overview

`RoslynEmitter.cs` is the **heart of code generation** in the Sharpy compiler. This file is responsible for transforming validated Sharpy Abstract Syntax Trees (ASTs) into executable C# code using Microsoft's Roslyn compiler API.

Think of it as a translator that speaks both languages: it reads Sharpy's Python-like syntax and writes idiomatic C# code that can be compiled to .NET assemblies.

### Role in the Compilation Pipeline

```
Sharpy Source Code
    ↓ (Lexer)
Tokens
    ↓ (Parser)
AST (Sharpy)
    ↓ (Semantic Analyzer)
Validated AST
    ↓ (RoslynEmitter) ← YOU ARE HERE
C# Syntax Tree (Roslyn)
    ↓ (Roslyn C# Compiler)
.NET Assembly
```

The `RoslynEmitter` sits between semantic analysis and the final C# compilation, responsible for:
1. Converting Sharpy AST nodes to C# Roslyn syntax nodes
2. Handling name mangling and collision resolution
3. Generating operator overloads from Python dunder methods
4. Managing variable scoping and redefinitions
5. Translating Python idioms to C# equivalents

---

## Class/Type Structure

### Main Class: `RoslynEmitter`

```csharp
public class RoslynEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;
    private readonly HashSet<string> _declaredVariables;
    private readonly Dictionary<string, int> _variableVersions;
    private int _tempVarCounter;
}
```

**Key Fields:**

- **`_context`**: Holds compilation context (source paths, namespaces, builtin function info)
- **`_typeMapper`**: Helper for converting Sharpy types to C# types
- **`_declaredVariables`**: Tracks which variables have been declared in current scope
- **`_variableVersions`**: Tracks redefinition versions (e.g., `x`, `x_1`, `x_2` for shadowing)
- **`_tempVarCounter`**: Counter for generating unique temporary variable names

### Important Constants

```csharp
private static readonly HashSet<string> UpperCaseAcronyms
```

A whitelist of common .NET namespace acronyms (IO, UI, XML, API, etc.) that should remain uppercase when converting module names to namespaces.

---

## Key Functions/Methods

### 1. **Entry Point: `GenerateCompilationUnit(Module module)`**

**What it does:** Creates the root C# compilation unit (the entire file structure) from a Sharpy module.

**Returns:** A `CompilationUnitSyntax` representing a complete C# file.

**Flow:**
1. Extracts `using` directives from import statements
2. Separates imports from executable code
3. Generates a module class wrapper (`__Module__`)
4. Creates namespace declaration
5. Assembles everything into a compilation unit

```csharp
public CompilationUnitSyntax GenerateCompilationUnit(Module module)
{
    var usingDirectives = GenerateUsingDirectives(module);
    var nonImportStatements = module.Body.Where(s => s is not ImportStatement...);
    var moduleClass = GenerateModuleClass(nonImportStatements);
    var namespaceName = GenerateNamespaceName();
    
    return CompilationUnit()
        .WithUsings(List(usingDirectives))
        .WithMembers(...);
}
```

**Key Design Decision:** All Sharpy code is wrapped in a static class called `__Module__` to provide a namespace for module-level functions and executable statements.

---

### 2. **Namespace Generation: `GenerateNamespaceName()` and `GenerateProjectNamespace()`**

**What it does:** Converts file paths and project structure into C# namespace names.

**Two Modes:**

**Single-file mode** (`GenerateNamespaceName`):
- Extracts directory structure from source file path
- Converts each component to PascalCase
- Example: `src/myapp/utils.spy` → `Myapp.Utils`

**Project mode** (`GenerateProjectNamespace`):
- Uses project root namespace as base
- Adds relative path from project root to file
- Example: Project namespace `MyApp` + `src/helpers/math.spy` → `MyApp.Helpers.Math`

**Name Transformation:**
- Uses `SimpleToPascalCase` for namespace components
- Respects `UpperCaseAcronyms` (e.g., "io" → "IO", not "Io")
- Filters out common directory names like "src", "lib"

---

### 3. **Import Handling: `GenerateUsingDirectives()`, `GenerateImportUsings()`, `GenerateFromImportUsings()`**

**What it does:** Converts Python-style imports to C# `using` directives.

**Mapping:**

| Python | C# |
|--------|-----|
| `import system.io` | `using System.IO;` |
| `import module as alias` | `using alias = Module;` |
| `from module import *` | `using Module;` |
| `from module import Name` | `using Module;` (C# imports entire namespace) |

**Module Name Conversion:**
```csharp
private string ConvertModuleNameToNamespace(string moduleName)
{
    // "system.io" → "System.IO"
    // "my_module.sub_module" → "MyModule.SubModule"
    var parts = moduleName.Split('.');
    return string.Join(".", parts.Select(SimpleToPascalCase));
}
```

**Note:** Unlike Python, C# doesn't support selective imports from a namespace—the entire namespace becomes available.

---

### 4. **Variable Redefinition Tracking: `GetMangledVariableName()`**

**What it does:** Handles Python-style variable shadowing/redefinition by versioning variable names.

**The Problem:** In Python, you can reassign variables with different types:
```python
x = 5       # int
x = "hello" # str (same variable, different type)
```

In C#, this is a type error. Sharpy solves this by creating versioned variables:
```csharp
var x = 5;        // First declaration
var x_1 = "hello"; // Redefinition gets version suffix
```

**Implementation:**
```csharp
private string GetMangledVariableName(string name, bool isNewDeclaration)
{
    var baseName = NameMangler.ToCamelCase(name);
    
    if (isNewDeclaration)
    {
        if (_variableVersions.TryGetValue(baseName, out var currentVersion))
        {
            var newVersion = currentVersion + 1;
            _variableVersions[baseName] = newVersion;
            return $"{baseName}_{newVersion}"; // x_1, x_2, etc.
        }
        else
        {
            _variableVersions[baseName] = 0;
            return baseName; // First version: x
        }
    }
    else
    {
        // Reference: use current version
        return currentVersion == 0 ? baseName : $"{baseName}_{currentVersion}";
    }
}
```

**Critical:** The `isNewDeclaration` parameter determines whether this is a new variable binding or a reference to an existing one.

---

### 5. **Function Declaration: `GenerateFunctionDeclaration()`**

**What it does:** Converts Sharpy function definitions to C# methods.

**Transformation Steps:**

1. **Clear scope state:** Reset `_declaredVariables` and `_variableVersions` for new function scope
2. **Name mangling:** Use `NameMangler.Transform()` to convert function names
3. **Return type mapping:** Map Sharpy type annotations to C# types (default to `void`)
4. **Modifiers from decorators:** Extract access modifiers and other attributes
5. **Parameter generation:** Convert parameters with type annotations
6. **Body generation:** Generate method statements
7. **Documentation:** Convert Python docstrings to XML doc comments

**Example:**
```python
# Sharpy
@public
def calculate_sum(values: list[int]) -> int:
    """Calculate the sum of values."""
    return sum(values)
```

```csharp
// Generated C#
/// <summary>
/// Calculate the sum of values.
/// </summary>
public static int CalculateSum(List<int> values)
{
    return Sharpy.Core.Exports.Sum(values);
}
```

**Note:** Module-level functions automatically get `static` modifier.

---

### 6. **Class Declaration: `GenerateClassDeclaration()`**

**What it does:** Converts Sharpy classes to C# classes, including special handling for constructors and dunder methods.

**Two-Pass Member Generation:**

**First Pass:** Generate fields
- Creates field members
- Builds `fieldMapping` dictionary for constructor use
- Handles type annotations and initializers

**Second Pass:** Generate methods, constructors, and operators
- Tracks dunder methods for complementary operator generation
- Converts `__init__` to constructor
- Generates operator overloads from dunder methods
- Synthesizes complementary operators (e.g., `!=` from `==`)

**Special Cases:**

**Constructor from `__init__`:**
```python
# Sharpy
class Person:
    name: str
    age: int
    
    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age
```

```csharp
// Generated C#
public class Person
{
    public string Name;
    public int Age;
    
    public Person(string name, int age)
    {
        this.Name = name;
        this.Age = age;
    }
}
```

**Note:** `self.name = name` assignments are converted to `this.Name = name` with proper field name mapping.

---

### 7. **Operator Synthesis: `TryGenerateOperatorOverload()` and Related Methods**

**What it does:** Automatically generates C# operator overloads from Python dunder methods.

**Philosophy:** Sharpy generates BOTH:
1. The dunder method itself (e.g., `__Add__()`)
2. An operator overload that delegates to it (e.g., `operator +`)

This allows interop with both Python-style and C#-style usage.

**Mapping Table:**

| Dunder Method | C# Operator | Method |
|---------------|-------------|---------|
| `__add__` | `operator +` | `GenerateBinaryOperator` |
| `__sub__` | `operator -` | `GenerateBinaryOperator` |
| `__mul__` | `operator *` | `GenerateBinaryOperator` |
| `__eq__` | `operator ==` | `GenerateComparisonOperator` |
| `__ne__` | `operator !=` | `GenerateComparisonOperator` |
| `__lt__` | `operator <` | `GenerateComparisonOperator` |
| `__neg__` | `operator -` (unary) | `GenerateUnaryOperator` |
| `__invert__` | `operator ~` | `GenerateUnaryOperator` |

**Example:**
```python
# Sharpy
class Vector:
    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)
```

```csharp
// Generated C#
public class Vector
{
    // The dunder method
    public Vector Add(Vector other)
    {
        return new Vector(this.x + other.x, this.y + other.y);
    }
    
    // The operator overload that calls it
    public static Vector operator +(Vector left, Vector right)
    {
        return left.Add(right);
    }
}
```

**Complementary Operators:**
C# requires `==` and `!=` to be defined together. If only `__eq__` is defined, Sharpy auto-generates `operator !=` as `!(left == right)`.

---

### 8. **Expression Generation: `GenerateExpression()` and Specialized Methods**

**What it does:** Massive switch statement that handles every type of Sharpy expression, delegating to specialized generators.

**Expression Categories:**

**Literals:**
- `GenerateIntegerLiteral()`: Numeric literals
- `GenerateStringLiteral()`: String literals
- `GenerateFString()`: F-string interpolation

**Collections:**
- `GenerateListLiteral()`: `[1, 2, 3]` → `new List<int> { 1, 2, 3 }`
- `GenerateDictLiteral()`: `{"a": 1}` → `new Dict<string, int> { {"a", 1} }`
- `GenerateSetLiteral()`: `{1, 2, 3}` → `new Set<int> { 1, 2, 3 }`
- `GenerateTupleLiteral()`: `(1, 2)` → `(1, 2)` (C# tuple)

**Comprehensions:**
- `GenerateListComprehension()`: Converts to LINQ `.Where().Select().ToList()`
- `GenerateSetComprehension()`: Similar but `.ToHashSet()`
- `GenerateDictComprehension()`: Uses `.ToDictionary()`

**Operators:**
- `GenerateBinaryOp()`: Arithmetic, comparison, logical, bitwise
- `GenerateUnaryOp()`: Negation, not, bitwise not
- `GenerateComparisonChain()`: `a < b < c` → `a < b && b < c`

**Special Cases:**
- `GenerateCall()`: Function calls, with builtin detection
- `GenerateMemberAccess()`: Dot notation, null-conditional (`?.`)
- `GenerateIndexAccess()`: Array/collection indexing
- `GenerateSliceAccess()`: Python slicing → `Sharpy.Core.Slice()` call

---

### 9. **Statement Generation: `GenerateBodyStatement()` and Control Flow**

**What it does:** Converts Sharpy statements to C# statements.

**Control Flow Mapping:**

**If Statements:** `GenerateIf()`
- Handles `if`/`elif`/`else` chains
- Builds nested structure for `elif` (which C# doesn't have)

```python
# Sharpy
if x > 0:
    print("positive")
elif x < 0:
    print("negative")
else:
    print("zero")
```

```csharp
// Generated C#
if (x > 0)
{
    Sharpy.Core.Exports.Print("positive");
}
else if (x < 0)
{
    Sharpy.Core.Exports.Print("negative");
}
else
{
    Sharpy.Core.Exports.Print("zero");
}
```

**For Loops:** `GenerateFor()`
- Converts `for item in items:` to `foreach (var item in items)`
- Supports tuple unpacking: `for x, y in pairs:` → `foreach (var (x, y) in pairs)`

**While Loops:** `GenerateWhile()`
- Direct mapping to C# `while`

**Try/Except:** `GenerateTry()`
- Maps `except` handlers to `catch` clauses
- Converts `finally:` to `finally`

---

### 10. **Assignment Handling: `GenerateAssignment()`**

**What it does:** Handles all forms of assignment in Sharpy, including redefinition logic.

**Cases:**

**Simple Assignment:**
```python
x = 5  # First declaration
x = 10 # Update existing (if in same scope)
```

**Augmented Assignment:**
```python
x += 5  # x = x + 5
```

**Index Assignment:**
```python
arr[0] = 10  # arr[0] = 10 (element access)
```

**Member Assignment:**
```python
obj.field = 10  # obj.Field = 10
```

**Tuple Unpacking:**
```python
x, y = (1, 2)  # var (x, y) = (1, 2)
```

**The Redefinition Decision:**
```csharp
if (_variableVersions.ContainsKey(baseName))
{
    // Variable exists - just update it
    currentName = GetMangledVariableName(name, isNewDeclaration: false);
    return AssignmentExpression(..., currentName, value);
}
else
{
    // First declaration in scope
    varName = GetMangledVariableName(name, isNewDeclaration: true);
    return LocalDeclarationStatement(...);
}
```

This logic determines whether to emit `var x = value;` (declaration) or `x = value;` (assignment).

---

### 11. **List Comprehension to LINQ: `GenerateListComprehension()`**

**What it does:** Transforms Python list comprehensions into LINQ method chains.

**Example Transformation:**
```python
# Sharpy
[x * 2 for x in items if x > 0]
```

```csharp
// Generated C#
items.Where(x => x > 0).Select(x => x * 2).ToList()
```

**Algorithm:**
1. Extract the first `for` clause and its iterator
2. Apply each `if` clause as `.Where(x => condition)`
3. Apply `.Select(x => element_expression)`
4. Terminate with `.ToList()` (or `.ToHashSet()`, `.ToDictionary()`)

**Current Limitation:** Nested comprehensions (multiple `for` clauses) throw `NotImplementedException`. Future versions may generate imperative code with nested loops for complex cases.

---

### 12. **F-String Interpolation: `GenerateFString()`**

**What it does:** Converts Python f-strings to C# interpolated strings.

**Direct Mapping:**
```python
# Sharpy
f"Hello, {name}!"
```

```csharp
// Generated C#
$"Hello, {name}!"
```

**Implementation:**
- Iterates over `FStringLiteral.Parts`
- Text parts become `InterpolatedStringText`
- Expression parts become `Interpolation(...)`

---

### 13. **Special Binary Operators**

Some Python operators don't have direct C# equivalents:

**Power (`**`):**
```python
x ** y  # Power
```
```csharp
Math.Pow(x, y)  // Generated
```

**Floor Division (`//`):**
```python
x // y  # Floor division
```
```csharp
(int)(x / y)  // Generated (casts to int)
```

**Membership (`in`, `not in`):**
```python
x in collection
```
```csharp
collection.__Contains__(x)  // Calls contains method
```

**Identity (`is`, `is not`):**
```python
x is None
x is not None
```
```csharp
x == null    // Optimized for None
x != null
```

For non-None cases:
```csharp
object.ReferenceEquals(x, y)
!object.ReferenceEquals(x, y)
```

---

## Dependencies

### Internal Dependencies

1. **`CodeGenContext`** (`CodeGen/CodeGenContext.cs`)
   - Provides compilation context (source paths, project namespace, builtin function registry)

2. **`TypeMapper`** (`CodeGen/TypeMapper.cs`)
   - Maps Sharpy types to C# types
   - Infers element types for collections
   - Handles generics, nullables, primitives

3. **`NameMangler`** (`CodeGen/NameMangler.cs`)
   - Converts Python naming conventions to C# conventions
   - Handles different contexts (Method, Type, Parameter, Constant)
   - Prevents name collisions
   - Transforms dunder methods (e.g., `__add__` → `Add`)

4. **AST Nodes** (`Parser/Ast/`)
   - All expression and statement types from the parser
   - `Module`, `FunctionDef`, `ClassDef`, etc.

### External Dependencies

1. **Microsoft.CodeAnalysis.CSharp** (Roslyn)
   - Provides `SyntaxFactory` for building C# syntax trees
   - `SyntaxNode`, `ExpressionSyntax`, `StatementSyntax`, etc.

2. **Sharpy.Core** (referenced types)
   - Used for type names in generated code
   - `Sharpy.Core.List<T>`, `Sharpy.Core.Dict<K,V>`, etc.

---

## Patterns and Design Decisions

### 1. **Visitor Pattern (Implicit)**

While not using a formal `Visitor` class, `RoslynEmitter` follows the visitor pattern through switch expressions:

```csharp
return expr switch
{
    IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
    StringLiteral strLit => GenerateStringLiteral(strLit),
    FunctionCall call => GenerateCall(call),
    // ... hundreds of cases
    _ => throw new NotImplementedException(...)
};
```

This pattern makes it easy to add new AST node types—just add a new case.

### 2. **State Management: Scope-Based Reset**

Variable tracking state is **deliberately cleared** at function boundaries:

```csharp
private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
{
    // Clear state for new function scope
    _declaredVariables.Clear();
    _variableVersions.Clear();
    
    // Generate function body...
}
```

This ensures each function/method has its own variable namespace, preventing cross-function pollution.

### 3. **Two-Pass Class Member Generation**

Classes are processed in two passes:

**Pass 1:** Generate all fields and build `fieldMapping`
- Ensures fields exist before constructor references them
- Maps original field names to mangled names

**Pass 2:** Generate methods, constructors, operators
- Uses `fieldMapping` to correctly reference fields
- Synthesizes complementary operators

This avoids forward reference issues.

### 4. **Lazy Operator Synthesis**

Operators are only generated if the corresponding dunder method exists:

```csharp
var operatorMember = TryGenerateOperatorOverload(funcDef, className);
if (operatorMember != null)
{
    members.Add(operatorMember);
}
```

If there's no `__add__`, no `operator +` is generated.

### 5. **XML Documentation Preservation**

Python docstrings are converted to C# XML doc comments:

```python
def greet(name: str) -> None:
    """Greet someone by name."""
    print(f"Hello, {name}!")
```

```csharp
/// <summary>
/// Greet someone by name.
/// </summary>
public static void Greet(string name)
{
    Sharpy.Core.Exports.Print($"Hello, {name}!");
}
```

This preserves documentation for IntelliSense and generated API docs.

### 6. **Progressive Enhancement Pattern**

Many methods throw `NotImplementedException` for features not yet supported:

```csharp
// Multiple for clauses (nested iteration) - requires more complex LINQ
throw new NotImplementedException("Nested comprehensions not yet supported");
```

This allows the compiler to work on 80% of code while acknowledging limitations clearly.

### 7. **Pythonic Idioms to .NET Patterns**

| Python Idiom | C# Pattern | Reasoning |
|--------------|------------|-----------|
| `x = None` | `x = null` | Direct semantic match |
| `[x for x in items]` | `items.Select(x => x).ToList()` | LINQ is idiomatic C# |
| `def func():` | `public static void Func()` | Module functions are static |
| `class Foo:` | `public class Foo` | Classes are public by default |
| `self.field` | `this.Field` | PascalCase fields, `this` qualifier |

---

## Debugging Tips

### 1. **Use `AstDumper` to Inspect Input**

Before debugging `RoslynEmitter`, verify the AST is what you expect:

```csharp
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

Many "code generation bugs" are actually parser or semantic analyzer issues.

### 2. **Check Generated C# with `NormalizeWhitespace()`**

The final compilation unit is normalized for readability:

```csharp
return CompilationUnit()
    .WithUsings(...)
    .WithMembers(...)
    .NormalizeWhitespace();  // ← Makes it readable
```

You can inspect `compilationUnit.ToFullString()` to see exactly what C# was generated.

### 3. **Variable Versioning Issues**

If you see variables like `x_1`, `x_2` unexpectedly:

- Check `_variableVersions` state at that point
- Verify `isNewDeclaration` parameter is correct
- Ensure state was cleared at function boundary

Common bug: Forgetting to clear state, causing version numbers to keep incrementing.

### 4. **Operator Overload Not Generated?**

Check:
1. Is the dunder method actually present in the AST?
2. Does `TryGenerateOperatorOverload()` have a case for it?
3. Is the dunder method's signature correct (parameters, return type)?
4. Check if `dunders.Contains(methodName)` is working for complementary operators

### 5. **Type Mapping Failures**

If you get C# compilation errors about types:

- Check `TypeMapper` mappings
- Verify type annotations in Sharpy code are correct
- Look for `InferElementType()` issues in collections
- Check if nullable types are handled correctly

### 6. **Namespace Issues**

If generated code has namespace conflicts:

- Verify `GenerateNamespaceName()` logic
- Check `ConvertModuleNameToNamespace()` conversion
- Look for `UpperCaseAcronyms` mismatches
- Ensure `SimpleToPascalCase()` handles edge cases

### 7. **Add Tracing for Complex Expressions**

For debugging complex expression generation:

```csharp
Console.WriteLine($"Generating expression: {expr.GetType().Name}");
var result = GenerateExpression(expr);
Console.WriteLine($"Generated: {result.ToFullString()}");
return result;
```

### 8. **Use Roslyn Quoter**

The [Roslyn Quoter](https://roslynquoter.azurewebsites.net/) tool lets you paste C# code and see the exact Roslyn API calls needed to generate it. Invaluable for learning how to generate complex syntax.

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made?

#### 1. **Adding New Expression Support**

If the parser/semantic analyzer adds a new expression type:

1. Add a case to `GenerateExpression()` switch
2. Implement `Generate{ExpressionType}()` method
3. Add tests in `CodeGenTests.cs`
4. Update this documentation

Example: Adding walrus operator (`:=`) support.

#### 2. **Adding New Statement Support**

For new statement types:

1. Add case to `GenerateBodyStatement()` switch
2. Implement `Generate{Statement}()` method
3. Handle scoping implications (does it introduce a new scope?)
4. Test with nested cases

Example: Adding `match`/`case` (pattern matching) support.

#### 3. **Improving LINQ Generation**

Current comprehension generation is simple but can be improved:

- Support multiple `for` clauses (nested iteration)
- Use `SelectMany` for flattening
- Fall back to imperative code for very complex cases
- Add complexity heuristic to choose strategy

#### 4. **Variable Redefinition Improvements**

Current versioning is simple but could be smarter:

- Detect when redefinition has same type (no version needed)
- Use control flow analysis to avoid versioning in different branches
- Better scope tracking for nested blocks

#### 5. **Operator Synthesis Enhancements**

Add support for:

- Reverse operators (`__radd__`, etc.)
- In-place operators (`__iadd__`, etc.)
- More dunder methods (`__getitem__`, `__setitem__` for indexers)
- Custom comparison operators with proper C# semantics

#### 6. **Better Error Messages**

When throwing `NotImplementedException`:

```csharp
throw new NotImplementedException(
    $"Nested comprehensions not yet supported. " +
    $"Consider using a for loop instead. " +
    $"See https://github.com/sharpy/issues/42"
);
```

Include:
- What's not supported
- Workarounds
- Issue tracker links

#### 7. **Performance Optimizations**

- Cache `NameMangler.Transform()` results
- Reduce allocations in hot paths
- Lazy-generate rarely-used members

#### 8. **Code Quality**

- Extract magic numbers to named constants
- Break up large methods (e.g., `GenerateExpression` could delegate more)
- Add regions for better organization
- Improve naming (some variables like `param` could be more descriptive)

### Guidelines for Contributors

**DO:**
- ✅ Match existing code style and patterns
- ✅ Add tests for new functionality
- ✅ Update documentation
- ✅ Use `NormalizeWhitespace()` for readable output
- ✅ Throw `NotImplementedException` for unfinished features with clear messages
- ✅ Clear scope state at function/class boundaries
- ✅ Preserve Python semantics where possible

**DON'T:**
- ❌ Modify state without understanding scoping implications
- ❌ Generate invalid C# (always test compilation)
- ❌ Break existing tests without good reason
- ❌ Silently ignore unsupported features
- ❌ Forget to handle `null` cases
- ❌ Hardcode type names (use `TypeMapper`)

### Testing Your Changes

1. **Unit test the generator:**
   ```csharp
   [Fact]
   public void TestGenerateNewFeature()
   {
       var ast = /* create test AST */;
       var emitter = new RoslynEmitter(context);
       var result = emitter.GenerateExpression(ast);
       Assert.Equal(expectedCSharp, result.ToFullString());
   }
   ```

2. **Integration test end-to-end:**
   ```csharp
   [Fact]
   public void TestCompileNewFeature()
   {
       var source = "# Sharpy code using new feature";
       var assembly = CompileAndLoad(source);
       Assert.NotNull(assembly);
   }
   ```

3. **Test edge cases:**
   - Nested cases
   - Empty inputs
   - Type mismatches
   - Null values
   - Very long inputs

### Common Pitfalls

1. **Forgetting to normalize whitespace:** Generated code will be hard to read
2. **Not clearing state:** Variables leak between functions
3. **Incorrect versioning logic:** Wrong `isNewDeclaration` parameter
4. **Hardcoding type names:** Use `TypeMapper` instead
5. **Missing null checks:** Many AST fields are nullable
6. **Breaking operator pairs:** C# requires `==` and `!=` together
7. **Ignoring semantic analysis results:** Type information should guide generation

---

## Summary

`RoslynEmitter.cs` is a **large, complex, but well-structured** code generator that bridges two very different language paradigms. Understanding it requires familiarity with:

1. **Sharpy's AST structure** (from the parser)
2. **Roslyn's syntax APIs** (C# code generation)
3. **Python semantics** (what behavior to preserve)
4. **C# semantics** (what's possible, what's idiomatic)

The key to working with this file is understanding the **transformation philosophy**: preserve Python's expressiveness and flexibility while generating safe, performant, idiomatic C# code.

When in doubt:
- Check how similar features are implemented
- Use `AstDumper` to inspect input
- Use Roslyn Quoter to learn output
- Write tests for everything
- Ask "What would Python do?" then "How would C# express that?"

**Happy code generation!** 🚀
