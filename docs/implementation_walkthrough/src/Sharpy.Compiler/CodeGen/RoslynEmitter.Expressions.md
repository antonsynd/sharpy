# Walkthrough: RoslynEmitter.Expressions.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

---

## Overview

This file is a **partial class** of `RoslynEmitter` that handles the translation of Sharpy expression AST nodes into C# Roslyn syntax trees. It's the expression-focused portion of the code generation phase—the final stage of the Sharpy compiler pipeline.

**Role in the Compiler Pipeline:**
```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# Source → .NET
                                                              ↑
                                                    You are here (expressions)
```

This file takes typed expression nodes from the parser AST and transforms them into equivalent C# expressions using Microsoft's Roslyn API. It handles everything from simple literals to complex comprehensions, operators, and function calls.

---

## Partial Class Context

`RoslynEmitter.Expressions.cs` is **one of several partial class files** that together make up the complete `RoslynEmitter` class:

- **`RoslynEmitter.cs`** - Core class definition, fields, and helper methods
- **`RoslynEmitter.Expressions.cs`** - Expression generation (this file)
- **`RoslynEmitter.Statements.cs`** - Statement generation (loops, if/else, etc.)
- **`RoslynEmitter.TypeDeclarations.cs`** - Class, struct, enum, interface generation
- **`RoslynEmitter.ClassMembers.cs`** - Methods, properties, fields within classes
- **`RoslynEmitter.Operators.cs`** - Operator overloads and utility methods
- **`RoslynEmitter.CompilationUnit.cs`** - Top-level compilation unit assembly
- **`RoslynEmitter.ModuleClass.cs`** - Module-level code organization

**Key Dependencies:**
- Access to private fields from `RoslynEmitter.cs`: `_context`, `_typeMapper`, `_targetTypeContext`, etc.
- Relies on helper methods from `RoslynEmitter.Operators.cs`: `IsFloatExpression()`, `GenerateFloorDivision()`, `GenerateTryExpression()`, etc.

---

## Key Private Fields (from main class)

Understanding these fields is essential for reading this file:

```csharp
private readonly CodeGenContext _context;           // Symbol table and semantic info
private readonly TypeMapper _typeMapper;            // Maps Sharpy types → C# types
private TypeAnnotation? _targetTypeContext;         // For type inference in collections
private int _tempVarCounter;                        // Unique temp variable naming
private readonly HashSet<string> _classNames;       // Tracks class names for instantiation
private readonly HashSet<string> _structNames;      // Tracks struct names for instantiation
private readonly HashSet<string> _stringEnumNames;  // String-based enums (different codegen)
```

---

## Main Entry Point: GenerateExpression()

**Location:** Lines 16-71

This is the **grand dispatcher** for all expression types. It uses C# pattern matching to route each AST expression type to its specialized handler.

```csharp
private ExpressionSyntax GenerateExpression(Expression expr)
{
    return expr switch
    {
        // Literals
        IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
        BooleanLiteral boolLit => LiteralExpression(...),

        // Collections
        ListLiteral listLit => GenerateListLiteral(listLit),

        // Operators
        BinaryOp binOp => GenerateBinaryOp(binOp),

        // ... 30+ expression types
        _ => throw new NotImplementedException(...)
    };
}
```

**Design Decision:** This exhaustive switch ensures compile-time safety—if a new expression type is added to the AST but not handled here, you'll get a runtime exception immediately.

**Special Cases Handled Inline:**
- `self` → `this` (instance reference transformation, line 41)
- `super` → `base` (parent class reference, line 43)
- `BooleanLiteral` → Direct `true`/`false` syntax kind (line 24)
- `NoneLiteral` → `null` (line 25)

---

## Literal Generation

### Simple Literals (Lines 73-86)

Straightforward conversions using Roslyn's `LiteralExpression` and `Literal` factories:

```csharp
GenerateIntegerLiteral(IntegerLiteral literal)
    → LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(int.Parse(literal.Value)))

GenerateFloatLiteral(FloatLiteral literal)
    → LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(double.Parse(literal.Value)))
```

**Note:** String parsing for numeric literals assumes the parser already validated the syntax.

### Ellipsis Literal (Lines 429-437)

```sharpy
def not_implemented():
    ...
```

Generates:
```csharp
throw new System.NotImplementedException()
```

**Design Note:** Abstract methods handle ellipsis differently—they don't generate a body at all (handled in `RoslynEmitter.ClassMembers.cs`).

---

## Collection Literals

All collection literals follow a similar pattern: **object creation with collection initializers**.

### List Literal (Lines 439-467)

```sharpy
numbers: list[int] = [1, 2, 3]
```

Generates:
```csharp
new global::Sharpy.Core.List<int> { 1, 2, 3 }
```

**Type Inference Strategy:**
1. **Prefer target type context** if available (e.g., from `list[int] = [...]` annotation)
2. **Fall back to element inference** using `_typeMapper.InferElementType()`

**Why `global::Sharpy.Core.List`?** Fully qualified to avoid ambiguity with user-defined types named "List".

### Dict Literal (Lines 469-503)

```sharpy
config: dict[str, int] = {"max": 100, "min": 0}
```

Generates:
```csharp
new global::Sharpy.Core.Dict<string, int>
{
    { "max", 100 },
    { "min", 0 }
}
```

Uses `ComplexElementInitializerExpression` for key-value pairs.

### Set Literal (Lines 505-531)

Similar to list, but uses `global::Sharpy.Core.Set<T>`.

### Tuple Literal (Lines 533-540)

```sharpy
point = (10, 20)
```

Generates:
```csharp
(10, 20)
```

Uses C#'s native tuple syntax via `TupleExpression`.

---

## Comprehensions → LINQ Chains

Python-style comprehensions are transformed into LINQ method chains. This is one of the most elegant transformations in the compiler.

### List Comprehension (Lines 546-614)

```sharpy
evens = [x * 2 for x in items if x > 0]
```

Generates:
```csharp
items.Where(x => x > 0).Select(x => x * 2).ToList()
```

**Algorithm:**
1. Start with the iterator expression (`items`)
2. Apply each `if` clause as `.Where(param => condition)`
3. Apply the element expression as `.Select(param => element)`
4. Finalize with `.ToList()`

**Current Limitations (TODO on line 542):**
- Multiple `for` clauses (nested iteration) not yet supported
- Tuple unpacking in `for` clauses not yet supported

**Debug Tip:** If you see "nested comprehensions not yet supported," check for patterns like:
```sharpy
[(x, y) for x in range(3) for y in range(3)]  # Multiple for clauses
```

### Set Comprehension (Lines 616-682)

Identical to list comprehension, but uses `.ToHashSet()` instead of `.ToList()`.

### Dict Comprehension (Lines 684-750)

```sharpy
squares = {x: x**2 for x in range(5)}
```

Generates:
```csharp
Enumerable.Range(0, 5).ToDictionary(x => x, x => Math.Pow(x, 2))
```

Uses two lambda selectors for key and value.

---

## Function Calls

**Location:** Lines 88-218

This is one of the **most complex methods** in the file due to handling multiple call patterns.

### Generic Type/Function Instantiation (Lines 92-133)

```sharpy
box = Box[int](42)          # Generic type
result = identity[str]("hello")  # Generic function
```

Parsed as: `FunctionCall(Function: IndexAccess(Box, int), Arguments: [42])`

Generates:
```csharp
new Box<int>(42)
identity<string>("hello")
```

**Symbol Table Lookup:** Checks if `Box` is a `TypeSymbol` or `FunctionSymbol` with `IsGeneric` flag.

### Type Instantiation vs Function Call (Lines 135-173)

```sharpy
person = Person("Alice", 30)  # Type instantiation
result = calculate(x, y)      # Function call
```

**Decision Logic:**
1. Check `_classNames` and `_structNames` sets
2. Check symbol table for `TypeSymbol` with `TypeKind.Class` or `TypeKind.Struct`
3. If true → `new TypeName(args)`, else → `TypeName(args)`

**Builtin Functions:** Qualified as `global::Sharpy.Core.Exports.{Name}` (line 149)

### Keyword Arguments (Lines 156-161)

```sharpy
person = Person(name="Alice", age=30)
```

Generates:
```csharp
new Person(name: "Alice", age: 30)
```

Uses `Argument(...).WithNameColon(...)` syntax.

### Method Calls (Lines 176-215)

```sharpy
result = obj.method(arg1, arg2)
```

Generates:
```csharp
obj.Method(arg1, arg2)
```

**Null-Conditional Methods (Lines 194-204):**
```sharpy
result = obj?.method()
```

Generates:
```csharp
obj?.Method()
```

Uses `ConditionalAccessExpression` with `MemberBindingExpression`.

---

## Binary Operators

**Location:** Lines 220-357

### Special Operator Transformations

#### Power Operator (Lines 228-239)
```sharpy
result = x ** y
```

Generates:
```csharp
System.Math.Pow(x, y)
```

**Why fully qualified?** Avoids conflicts with `Sharpy.Math` namespace.

#### Division - Python Semantics (Lines 241-255)
```sharpy
result = 5 / 2  # Always returns float
```

Generates:
```csharp
(double)5 / 2  # Result: 2.5, not 2
```

**Key Insight:** Python's `/` always performs true division (returns float), unlike C#'s integer division.

#### Floor Division (Lines 257-262)
```sharpy
result = 5 // 2  # Floors toward negative infinity
```

Generates:
```csharp
(int)Math.Floor((double)5 / 2)  # Result: 2
```

Delegates to `GenerateFloorDivision()` from `RoslynEmitter.Operators.cs` (lines 416-443).

**Python Semantics:** `-7 // 2` → `-4` (not `-3` as C# truncation would give).

#### Membership Operators (Lines 264-279)
```sharpy
if x in collection:
    ...
```

Generates:
```csharp
collection.__Contains__(x)
```

Calls Sharpy's `__Contains__` protocol method.

#### Identity Operators (Lines 281-314)

```sharpy
if x is None:        # Optimized
if x is not None:    # Optimized
if x is y:           # General case
```

Generates:
```csharp
x == null
x != null
object.ReferenceEquals(x, y)
```

**Optimization:** Special-cases `None` comparisons to use `== null` instead of `ReferenceEquals`.

#### Pipe Forward (Lines 316-319)

```sharpy
result = data |> process |> format
```

Generates:
```csharp
format(process(data))
```

Delegates to `GeneratePipeForward()` (lines 359-411).

### Standard Operators (Lines 323-356)

Direct mappings using `BinaryExpression`:
- Arithmetic: `+`, `-`, `*`, `%`
- Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`
- Logical: `&&` (short-circuit), `||` (short-circuit)
- Bitwise: `&`, `|`, `^`, `<<`, `>>`
- Null coalescing: `??`

---

## Pipe Forward Operator

**Location:** Lines 359-411

The pipe operator enables functional programming patterns:

```sharpy
result = value |> func1 |> func2(arg) |> func3
```

### Case 1: Piping into Function Call (Lines 370-384)
```sharpy
x |> f(y, z)
```

Generates:
```csharp
f(x, y, z)  // x prepended to arguments
```

### Case 2: Piping into Identifier (Lines 387-391)
```sharpy
x |> f
```

Generates:
```csharp
f(x)
```

**Builtin Function Handling:** Uses `GeneratePipeCallTarget()` to apply the same name mangling as regular function calls (lines 398-411).

---

## Member Access

**Location:** Lines 752-844

Handles dot notation with several special cases.

### Module Path Resolution (Lines 755-759)

```sharpy
import math.ops
result = math.ops.add(1, 2)
```

Uses `TryExtractModulePath()` (lines 846-917) to detect qualified module access.

### Enum Member Access (Lines 762-804)

```sharpy
class Color(Enum):
    RED = 1

color = Color.RED
```

Generates:
```csharp
Program.Color.Red  // Integer enums
Program.Color.RED  // String enums (CONSTANT_CASE)
```

**Why "Program."?** Fully qualified to avoid shadowing by local variables (line 770).

### Enum .value Property (Lines 809-820)

```sharpy
color_int = Color.RED.value
```

Generates:
```csharp
(int)Program.Color.Red
```

### Name Mangling (Lines 822-843)

```csharp
IsConstantCaseName(member)
    ? NameMangler.ToConstantCase(member)  // ALL_CAPS → CONSTANT_CASE
    : NameMangler.ToPascalCase(member)    // snake_case → PascalCase
```

### Null-Conditional Access (Lines 830-834)

```sharpy
value = obj?.property
```

Generates:
```csharp
obj?.Property
```

Uses `ConditionalAccessExpression` with `MemberBindingExpression`.

---

## Module Path Handling

### TryExtractModulePath (Lines 846-917)

**Purpose:** Detect when a member access chain represents module navigation.

```sharpy
import lib.math
result = lib.math.add(1, 2)
```

**Algorithm:**
1. Traverse the member access chain: `lib.math.add` → `["lib", "math", "add"]`
2. Verify the base (`lib`) is a `ModuleSymbol`
3. Walk the module hierarchy to verify each segment exists
4. Return `true` if the entire path is valid

**Edge Case:** Distinguishes between:
```sharpy
lib.math.add    # Module path → Lib.Math.Add
person.age      # Member access → person.Age
```

### BuildModuleAccessExpression (Lines 919-1015)

**Purpose:** Transform a validated module path into C# syntax.

**Imported Module Handling (Lines 933-993):**
```sharpy
import parent.child
result = parent.child.func()
```

Generates:
```csharp
parent_child.Func()  // Uses the "using" alias
```

**Algorithm:**
1. Find the longest module prefix that matches an import
2. Build an alias name: `parent.child` → `parent_child`
3. Append the remaining member path with PascalCase

**Keyword Escaping (Line 970):** Uses `EscapeCSharpKeyword()` to handle imports like:
```sharpy
import base  # C# keyword
```
Generates: `@base`

---

## Indexing and Slicing

### Index Access (Lines 1017-1024)

```sharpy
item = arr[0]
```

Generates:
```csharp
arr[0]
```

Uses `ElementAccessExpression`.

### Slice Access (Lines 1026-1052)

```sharpy
subset = arr[1:10:2]  # start:stop:step
```

Generates:
```csharp
Sharpy.Core.Slice(arr, 1, 10, 2)
```

**Null Handling:** Omitted slice components (e.g., `arr[:10]`) generate `null` arguments.

---

## Comparison Chains

**Location:** Lines 1054-1092

Python allows chained comparisons:

```sharpy
if 0 < x < 10:
    ...
```

Generates:
```csharp
0 < x && x < 10
```

**Algorithm:**
1. Iterate over operators and operands pairwise
2. Build binary comparisons: `operands[i] op operands[i+1]`
3. Combine with `&&` (logical AND)

**TODO Comment (Line 1058):** Currently re-evaluates intermediate values (e.g., `x` is evaluated twice above). Future improvement: store in temp variables.

---

## Conditional Expressions

**Location:** Lines 1094-1102

```sharpy
result = value if test else other
```

Generates:
```csharp
test ? value : other
```

**Note:** Sharpy uses `if-else` syntax, but order is reversed from Python's ternary.

---

## Lambda Expressions

**Location:** Lines 1104-1129

```sharpy
add = lambda x, y: x + y
```

Generates:
```csharp
(x, y) => x + y
```

**Special Cases:**
- **0 parameters:** `ParenthesizedLambdaExpression()` (line 1115)
- **1 parameter:** `SimpleLambdaExpression(param)` (line 1120)
- **2+ parameters:** `ParenthesizedLambdaExpression().WithParameterList(...)` (line 1125)

---

## Type Operations

### Type Cast (Lines 1131-1138)

```sharpy
value as int
```

Generates:
```csharp
(int)value
```

### Type Coercion (Lines 1140-1200)

The `to` operator provides safe and unsafe casting:

```sharpy
value to int      # Unsafe: throws on failure
value to int?     # Safe: returns null on failure
```

**Reference Types (Lines 1186-1192):**
```sharpy
obj to Animal?
```
Generates:
```csharp
obj as Animal
```

**Value Types (Lines 1169-1183):**
```sharpy
obj to int?
```
Generates:
```csharp
obj is int _temp ? (int?)_temp : (int?)null
```

Uses pattern matching to avoid casting twice.

### Type Check (Lines 1202-1212)

```sharpy
if value is int:
    ...
```

Generates:
```csharp
value is int
```

---

## F-String Interpolation

**Location:** Lines 1214-1239

```sharpy
message = f"Hello {name}, you are {age} years old"
```

Generates:
```csharp
$"Hello {name}, you are {age} years old"
```

**Implementation:**
1. Parse `fstring.Parts` (alternating text and expression segments)
2. Build `InterpolatedStringText` and `Interpolation` nodes
3. Wrap in `InterpolatedStringExpression`

---

## Architecture and Design Patterns

### Pattern Matching Dispatcher

The `GenerateExpression()` method uses exhaustive pattern matching—a common pattern in compiler code generation:

**Benefits:**
- Type-safe routing
- Clear separation of concerns
- Easy to add new expression types
- Compiler-enforced completeness

### Semantic Fidelity

The code goes to great lengths to preserve **Python/Sharpy semantics** in C#:
- True division always returns float
- Floor division rounds toward negative infinity
- Identity operators use reference equality
- `None` maps to `null`

### Helper Method Organization

Many complex operations delegate to helper methods in `RoslynEmitter.Operators.cs`:
- `IsFloatExpression()` - Type checking for division semantics
- `GenerateFloorDivision()` - Correct floor division
- `GenerateTryExpression()` - Wrapping in `Result<T, E>`
- `CollectReferencedIdentifiers()` - Dependency analysis

This separation keeps the expression file focused on **expression structure**, not utilities.

---

## Debugging Tips

### Common Issues

1. **"Expression type not implemented"**
   - **Where:** Line 69
   - **Cause:** A new AST expression type was added but not handled in the switch
   - **Fix:** Add a case in `GenerateExpression()` and implement the handler

2. **Type Inference Failures in Collections**
   - **Symptom:** Compiler can't determine `List<T>` element type
   - **Where:** Lines 443-455 (list literals)
   - **Debug:** Check `_targetTypeContext` value and `_typeMapper.InferElementType()` logic
   - **Workaround:** Add explicit type annotations in Sharpy source

3. **Name Mangling Mismatches**
   - **Symptom:** "Symbol not found" errors for module members
   - **Where:** Lines 822-827 (member access name mangling)
   - **Debug:** Check `IsConstantCaseName()` logic and `NameMangler` rules
   - **Tip:** Use `_context.LookupSymbol()` to verify symbol table entries

4. **Null-Conditional Operator Crashes**
   - **Symptom:** Roslyn syntax errors with `?.` chains
   - **Where:** Lines 194-204 (method calls), 830-834 (member access)
   - **Debug:** Verify `IsNullConditional` flag is set correctly during parsing

5. **Comprehension Translation Errors**
   - **Symptom:** LINQ chains produce wrong results
   - **Where:** Lines 546-750 (comprehension methods)
   - **Common Mistake:** Order of `Where` and `Select` matters!
   - **Tip:** Check the lambda parameter names match across clauses

### Tracing Expression Generation

Add breakpoints at:
- **Line 16:** Entry to `GenerateExpression()` to see the AST node type
- **Line 88:** Function call handling (complex logic)
- **Line 220:** Binary operator translation
- **Line 752:** Member access (handles modules, enums, properties)

### Inspecting Generated Roslyn

Use Roslyn's `.ToFullString()` on the generated `ExpressionSyntax` to see the C# code:

```csharp
var expr = GenerateExpression(astNode);
Console.WriteLine(expr.ToFullString());
```

---

## Contribution Guidelines

### When to Modify This File

- **Adding new expression types:** Update the switch in `GenerateExpression()`
- **Changing operator semantics:** Modify `GenerateBinaryOp()` or `GenerateUnaryOp()`
- **Improving type inference:** Update collection literal methods
- **Supporting new Python/Sharpy features:** Add specialized handlers

### When NOT to Modify This File

- **Statement-level changes:** Use `RoslynEmitter.Statements.cs`
- **Type declarations:** Use `RoslynEmitter.TypeDeclarations.cs`
- **Helper utilities:** Use `RoslynEmitter.Operators.cs`
- **Name mangling rules:** Use `NameMangler.cs` (separate file)

### Testing New Expression Types

1. Add test cases in `test/Sharpy.Compiler.Tests/CodeGen/`
2. Verify generated C# compiles correctly
3. Verify runtime behavior matches Sharpy semantics
4. Check edge cases (null values, empty collections, type inference)

### Code Style

- Use **explicit variable names** (avoid single-letter vars except in lambdas)
- Add **XML doc comments** for public/internal methods
- Include **inline comments** explaining non-obvious transformations
- Preserve **Sharpy semantics** over C# idioms when they conflict

---

## Cross-References

### Related Partial Class Files

- **[RoslynEmitter.cs](RoslynEmitter.md)** - Core class, fields, and `GetMangledVariableName()` logic
- **[RoslynEmitter.Operators.cs](RoslynEmitter.Operators.md)** - Helper methods used by expression generation
- **[RoslynEmitter.Statements.cs](RoslynEmitter.Statements.md)** - Statement generation that calls back to `GenerateExpression()`
- **[RoslynEmitter.ClassMembers.cs](RoslynEmitter.ClassMembers.md)** - Method/property generation (uses `GenerateExpression()` for bodies)

### Key Dependencies

- **`TypeMapper.cs`** - Type annotation → Roslyn type conversion
- **`NameMangler.cs`** - Naming convention transformations
- **`CodeGenContext.cs`** - Symbol table and semantic information
- **`Parser.Ast` namespace** - AST node definitions
- **`Semantic` namespace** - Type symbols and semantic analysis results

### Specification Documents

- **`docs/language_specification/expressions.md`** - Sharpy expression syntax and semantics
- **`docs/language_specification/operator_precedence.md`** - Operator behavior
- **`docs/language_specification/dotnet_interop.md`** - .NET integration patterns

---

## Summary

`RoslynEmitter.Expressions.cs` is the **expression translation engine** of the Sharpy compiler. It bridges the gap between Python-like syntax and .NET runtime semantics, handling:

✅ **30+ expression types** from literals to comprehensions
✅ **Python semantic fidelity** (true division, floor division, identity ops)
✅ **Type inference** for generic collections
✅ **Name mangling** for Sharpy → C# conventions
✅ **Null safety** with null-conditional operators
✅ **Functional patterns** (pipe forward, comprehensions → LINQ)

Understanding this file is essential for anyone working on the code generator, as nearly all compiler phases eventually call `GenerateExpression()` to transform AST nodes into executable C# code.
