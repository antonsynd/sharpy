# Walkthrough: RoslynEmitter.Expressions.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

---

## Overview

This file is a partial class implementation of `RoslynEmitter` that handles the transformation of **all expression types** from Sharpy's Abstract Syntax Tree (AST) into C# Roslyn syntax trees. It's the final phase of the compiler pipeline that converts typed AST nodes (after semantic analysis) into executable C# code.

**What this file does:**
- Converts 30+ expression types (literals, operators, calls, comprehensions, etc.) into C# syntax
- Handles Python-to-.NET semantic mappings (e.g., `None` → `null`, `self` → `this`, `**` → `Math.Pow`)
- Manages name mangling (snake_case → PascalCase, constants → CONSTANT_CASE)
- Implements Python-specific features like comprehensions, comparison chains, and pipe operators
- Resolves cross-module references and generic type instantiation

**Role in the compiler pipeline:**
```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → [RoslynEmitter] → C# → .NET IL
                                          ↑ TypeMapper, CodeGenContext
```

## Class Structure

This is a **partial class** `RoslynEmitter` in the `Sharpy.Compiler.CodeGen` namespace. It works alongside:
- `RoslynEmitter.cs` (main class with fields and context)
- `RoslynEmitter.Statements.cs` (statement generation)
- `RoslynEmitter.TypeDeclarations.cs` (class, enum, struct definitions)
- `RoslynEmitter.ClassMembers.cs` (methods, properties)
- `RoslynEmitter.CompilationUnit.cs` (top-level compilation)
- `RoslynEmitter.ModuleClass.cs` (module structure)
- `RoslynEmitter.Operators.cs` (operator overloads and helper methods)

**Key dependencies from the main class:**
- `_context: CodeGenContext` - provides symbol lookup, semantic info, and module context
- `_typeMapper: TypeMapper` - maps Sharpy type annotations to C# types
- `_targetTypeContext: TypeAnnotation?` - target type for collection literal inference
- `_tempVarCounter: int` - generates unique temporary variable names
- `_variableVersions: Dictionary<string, int>` - tracks local variable redeclarations

## Key Methods

### 1. GenerateExpression (Central Dispatcher)

**Location:** Lines 17-72

**Purpose:** The main entry point that dispatches to specialized generators based on expression type.

**Design Pattern:** C# pattern matching switch expression (exhaustive handling of 30+ AST node types)

```csharp
private ExpressionSyntax GenerateExpression(Expression expr)
{
    return expr switch
    {
        IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
        StringLiteral strLit => GenerateStringLiteral(strLit),
        BooleanLiteral boolLit => LiteralExpression(...),
        NoneLiteral => LiteralExpression(SyntaxKind.NullLiteralExpression),
        // ... 25+ more cases
        _ => throw new NotImplementedException(...)
    };
}
```

**Important transformations:**
- `self` identifier → `this` expression (line 42)
- `super()` → `base` expression (line 44)
- `NoneLiteral` → `null` (line 26)
- Boolean literals → true/false tokens (line 25)

**Why this matters:** This is the most frequently called method during expression code generation. Every expression in the Sharpy source code flows through here.

---

### 2. GenerateCall (Function and Constructor Calls)

**Location:** Lines 89-233

**Purpose:** Transforms function calls, method calls, generic instantiation, and constructor invocations.

**Key challenges this method solves:**

#### a) Generic Type/Function Instantiation (lines 92-134)
```python
# Sharpy code
Box[int](42)
identity[str]("hello")

# Pattern: FunctionCall(Function: IndexAccess(Object: identifier, Index: type_expr))
```

Generated C#:
```csharp
new Box<int>(42)
Identity<string>("hello")
```

**Algorithm:**
1. Detect `IndexAccess` pattern in function position
2. Look up symbol to distinguish generic types vs. generic functions
3. Map type arguments using `TypeMapper.MapTypeArgumentsFromExpression`
4. Generate `ObjectCreationExpression` for types, `InvocationExpression` for functions

#### b) Builtin Function Resolution (lines 138-155)
```python
len(items)
print("hello")
int("42")
```

Builtin functions are **always** qualified with the standard library:
```csharp
global::Sharpy.Core.Exports.Len(items)
global::Sharpy.Core.Exports.Print("hello")
global::Sharpy.Core.Exports.Int("42")
```

**Why:** Prevents name shadowing if user defines a local variable named `len` or `print`.

#### c) Type Instantiation vs. Function Call (lines 143-165)
```python
# Type instantiation
Point(x=1, y=2)  →  new Point(x: 1, y: 2)

# Function call
calculate(1, 2)  →  Calculate(1, 2)
```

**Detection logic:**
1. Check `_context.IsBuiltinFunction()` - builtins are never constructors
2. Look up symbol - if it's a `TypeSymbol` with `TypeKind.Class` or `Struct`, it's a constructor
3. Generate `ObjectCreationExpression` for constructors, `InvocationExpression` for functions

#### d) Keyword Arguments (lines 169-173)
```python
def greet(name, message):
    ...

greet(message="Hi", name="Alice")
```

Generated C#:
```csharp
Greet(message: "Hi", name: "Alice")
```

Uses Roslyn's `Argument.WithNameColon()` for named arguments.

#### e) Null-Conditional Method Calls (lines 209-220)
```python
obj?.method(arg)
```

Generated C#:
```csharp
obj?.Method(arg)
```

**Roslyn structure:** `ConditionalAccessExpression` + `MemberBindingExpression` + `InvocationExpression`

---

### 3. GenerateBinaryOp (Binary Operators)

**Location:** Lines 235-372

**Purpose:** Translates binary operators with special handling for Python-specific semantics.

**Special cases requiring method calls or transformations:**

#### a) Power Operator (lines 243-254)
```python
x ** y  →  System.Math.Pow(x, y)
```
C# has no `**` operator, so we use `Math.Pow`. Uses fully qualified `System.Math` to avoid conflicts with `Sharpy.Math` namespace.

#### b) True Division (lines 256-270)
```python
# Python always returns float
5 / 2  →  2.5  (not 2)
```

Generated C# ensures float result:
```csharp
// If both operands are integers, cast left to double
(double)5 / 2  →  2.5

// If either is already float, division naturally produces float
5.0 / 2  →  2.5
```

**Algorithm:**
1. Check if operands are float using `IsFloatExpression()` helper
2. If both are integers, cast left operand to `double`
3. Otherwise, rely on natural float promotion

#### c) Floor Division (lines 272-277)
```python
# Floors toward negative infinity (not truncation)
7 // 2   →  3
-7 // 2  →  -4  (not -3)
```

Uses `GenerateFloorDivision()` helper (in `RoslynEmitter.Operators.cs`):
```csharp
// Integer operands
(int)Math.Floor((double)x / y)

// Float operands
Math.Floor(x / y)
```

#### d) Membership Operators (lines 279-294)
```python
x in y      →  y.__Contains__(x)
x not in y  →  !y.__Contains__(x)
```

Translates to method calls on the container object.

#### e) Identity Operators (lines 296-329)
```python
x is y      →  object.ReferenceEquals(x, y)
x is not y  →  !object.ReferenceEquals(x, y)
```

**Optimization for `None` checks:**
```python
x is None      →  x == null
x is not None  →  x != null
```

#### f) Pipe Forward Operator (lines 331-334)
```python
x |> f      →  f(x)
x |> f(y)   →  f(x, y)
```

Uses `GeneratePipeForward()` helper to prepend the left operand to the function's argument list.

**Standard operators** (lines 338-371) map directly to C# syntax kinds:
- Arithmetic: `+`, `-`, `*`, `%`
- Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`
- Logical: `and` → `&&`, `or` → `||`
- Bitwise: `&`, `|`, `^`, `<<`, `>>`
- Null coalescing: `??`

---

### 4. GeneratePipeForward (Pipe Operator)

**Location:** Lines 380-407

**Purpose:** Implements functional pipe operator (`|>`) for chaining operations.

**Two cases:**

#### Case 1: Right side is already a function call
```python
x |> f(y, z)  →  f(x, y, z)
```

**Algorithm:**
1. Generate the left expression
2. Extract the function and existing arguments from the right `FunctionCall`
3. Prepend left as the first argument
4. Generate invocation with combined arguments

#### Case 2: Right side is a simple identifier/expression
```python
x |> f  →  f(x)
```

**Algorithm:**
1. Generate the left expression
2. Generate the call target using `GeneratePipeCallTarget()`
3. Create invocation with left as the only argument

**Helper method** `GeneratePipeCallTarget` (lines 409-426):
- Handles builtin function qualification (`global::Sharpy.Core.Exports.`)
- Applies proper name mangling (PascalCase for user functions)
- Supports member access expressions for piping to methods

**Chaining example:**
```python
x |> f |> g  →  g(f(x))
```
This works automatically because the parser makes `|>` left-associative, so `x |> f` evaluates first, then its result pipes to `g`.

---

### 5. GenerateListLiteral / DictLiteral / SetLiteral

**Location:** Lines 454-546

**Purpose:** Create Sharpy's generic collection instances with C# collection initializer syntax.

**Design pattern:** Target-typed collections with fallback inference

#### List Literal (lines 454-482)
```python
nums: list[int] = [1, 2, 3]
```

Generated C#:
```csharp
new global::Sharpy.Core.List<int> { 1, 2, 3 }
```

**Type resolution algorithm:**
1. **Prefer target type context** - if `_targetTypeContext` is set and is `list[T]`, use `T`
2. **Fall back to element inference** - use `_typeMapper.InferElementType(elements)`
3. Generate `ObjectCreationExpression` with `CollectionInitializerExpression`

**Why `_targetTypeContext`?**
When generating code for `nums: list[int] = [1, 2, 3]`, the statement generator sets `_targetTypeContext` to `list[int]` before calling `GenerateExpression()` on the literal. This ensures the literal gets the correct element type even if it's empty or contains `None` values.

#### Dict Literal (lines 484-518)
```python
config: dict[str, int] = {"max": 100, "min": 0}
```

Generated C#:
```csharp
new global::Sharpy.Core.Dict<string, int> { { "max", 100 }, { "min", 0 } }
```

Uses `ComplexElementInitializerExpression` for key-value pairs.

#### Set Literal (lines 520-546)
```python
unique: set[str] = {"a", "b", "c"}
```

Generated C#:
```csharp
new global::Sharpy.Core.Set<string> { "a", "b", "c" }
```

**Important:** All collections use fully qualified names (`global::Sharpy.Core.List`) to avoid ambiguity with `System.Collections.Generic.List`.

---

### 6. Comprehension Generators

**Location:** Lines 561-765

**Purpose:** Transform Python comprehensions into LINQ method chains.

#### List Comprehension (lines 561-629)
```python
[x * 2 for x in items if x > 0]
```

Generated C#:
```csharp
items.Where(x => x > 0).Select(x => x * 2).ToList()
```

**Algorithm:**
1. Extract first `ForClause` to get loop variable and iterator
2. Generate lambda parameter from loop variable (with camelCase mangling)
3. Chain `.Where(lambda)` for each `IfClause`
4. Chain `.Select(lambda)` for the element expression
5. Terminate with `.ToList()`

**Current limitations (lines 600-605):**
- Multiple `for` clauses (nested iteration) throw `NotImplementedException`
- Tuple unpacking in loop variables not supported

**TODO comment (lines 557-559):** For complex comprehensions, consider generating imperative code (foreach loops) instead of LINQ for better readability.

#### Set Comprehension (lines 631-697)
Same pattern as list comprehension, but terminates with `.ToHashSet()` instead of `.ToList()`.

#### Dict Comprehension (lines 699-765)
```python
{k: v for k, v in pairs if v > 0}
```

Generated C# (simplified):
```csharp
pairs.Where(p => p.v > 0).ToDictionary(p => p.k, p => p.v)
```

**Difference:** Uses `.ToDictionary(keySelector, valueSelector)` with two lambda arguments.

**Current limitation:** Tuple unpacking not supported, so `for k, v in pairs` will fail.

---

### 7. GenerateMemberAccess (Property/Field/Method Access)

**Location:** Lines 767-859

**Purpose:** Handle member access with special cases for enums, modules, constants, and null-conditional access.

**Special cases (checked in order):**

#### a) Module Access (lines 771-774)
```python
lib.math.add  →  Lib.Math.Add
```

Delegates to `TryExtractModulePath()` and `BuildModuleAccessExpression()`.

#### b) Enum Member Access (lines 776-819)
```python
# Integer enum
Color.RED  →  Program.Color.Red

# String enum
Status.ACTIVE  →  Program.Status.ACTIVE  (field name in CONSTANT_CASE)
```

**Algorithm:**
1. Check if object is an `Identifier`
2. Look up symbol - if it's a `TypeSymbol` with `TypeKind.Enum`, handle specially
3. Build qualified enum type: `Program.EnumName`
4. For string enums (detected by `IsStringEnumSymbol()`):
   - Use CONSTANT_CASE field names
   - Return field access directly (already returns string)
5. For integer enums:
   - Use PascalCase member names (`TransformEnumMemberName()`)
   - Return enum member directly (use `.value` property to get underlying int)

**Why `Program.` qualification?** Prevents shadowing by local variables with the same name as the enum type.

#### c) Enum `.value` Property (lines 824-835)
```python
color_enum.value  →  (int)color_enum
```

If the member name is `value` and the object is an enum type, cast to `int`.

#### d) Regular Member Access (lines 838-858)
```python
obj.method_name  →  obj.MethodName
obj?.field_name  →  obj?.FieldName
```

**Name mangling logic:**
- ALL_CAPS names (Python constants) → CONSTANT_CASE
- Other names → PascalCase

**Null-conditional:** Uses `ConditionalAccessExpression` + `MemberBindingExpression`.

---

### 8. TryExtractModulePath / BuildModuleAccessExpression

**Location:** Lines 866-1030

**Purpose:** Resolve and generate code for multi-part module access chains.

#### TryExtractModulePath (lines 866-932)
```python
lib.math.add
```

**Algorithm:**
1. Traverse the `MemberAccess` chain recursively
2. Build path array: `["lib", "math", "add"]`
3. Verify base is a `ModuleSymbol` in symbol table
4. Verify each part exists in the module's exports
5. Return true if entire path is valid module access

**Why this exists:** Distinguishes `lib.math.add` (module access) from `obj.field.method` (member access).

#### BuildModuleAccessExpression (lines 941-1030)
```python
import config
config.MAX_SIZE  →  config.MaxSize
```

**Two strategies:**

**Strategy 1: Imported modules with using alias (lines 948-1008)**
```csharp
using config = MyProject.Config.Exports;
// Later:
config.MaxSize
```

**Algorithm:**
1. Check if base is a `ModuleSymbol`
2. Find longest module path prefix matching an import
3. Build alias name: `module_path` → `module_path` (with C# keyword escaping)
4. Build member access: `alias.Member1.Member2...`

**Strategy 2: Local module path (lines 1010-1029)**
```python
lib.math.add  →  Lib.Math.Add
```

Chain `MemberAccessExpression` nodes with PascalCase names.

**Constant handling:** ALL_CAPS names use CONSTANT_CASE instead of PascalCase.

---

### 9. GenerateComparisonChain (Chained Comparisons)

**Location:** Lines 1069-1107

**Purpose:** Convert Python's comparison chains to multiple comparisons with `&&`.

```python
a < b < c  →  a < b && b < c
```

**Algorithm:**
1. Validate operand and operator counts match
2. For each pair of operands, generate comparison
3. Combine all comparisons with `LogicalAndExpression`

**Current limitation (line 1073):** Re-evaluates intermediate values (e.g., `b` evaluated twice).

**TODO:** Store intermediate values in temp variables for efficiency.

---

### 10. GenerateConditionalExpression (Ternary)

**Location:** Lines 1109-1117

**Purpose:** Transform Python's conditional expression to C# ternary operator.

```python
value if test else other  →  test ? value : other
```

**Note:** Python's syntax has inverted order compared to C# (`value` comes before `test`), but the AST node already has them in the correct structure.

---

### 11. GenerateLambdaExpression

**Location:** Lines 1119-1144

**Purpose:** Convert Python lambda to C# lambda with proper parameter handling.

```python
lambda x, y: x + y  →  (x, y) => x + y
```

**Three cases:**
1. **No parameters:** `ParenthesizedLambdaExpression` with empty parameter list
2. **One parameter:** `SimpleLambdaExpression` (no parentheses around parameter)
3. **Multiple parameters:** `ParenthesizedLambdaExpression` with parameter list

**Name mangling:** Parameters use camelCase.

---

### 12. GenerateTypeCast / GenerateTypeCoercion / GenerateTypeCheck

**Location:** Lines 1146-1227

**Purpose:** Handle Sharpy's three type-related operators.

#### TypeCast (`as` operator) - lines 1146-1153
```python
value as Type  →  (Type)value
```

Simple cast expression (throws if invalid).

#### TypeCoercion (`to` operator) - lines 1155-1215
```python
# Throwing form
value to Type  →  (Type)value

# Safe form (reference types)
value to Type?  →  value as Type

# Safe form (value types)
value to int?  →  value is int _temp ? (int?)_temp : (int?)null
```

**Complex logic for nullable coercion:**
1. Detect if target type is nullable
2. Use `PrimitiveCatalog` to determine if it's a value type
3. For value types: Generate pattern matching with temp variable
4. For reference types: Use `as` expression

**Why different strategies?** C#'s `as` operator only works with reference types.

#### TypeCheck (`is` operator) - lines 1217-1227
```python
value is Type  →  value is Type
```

Direct mapping to C# `is` expression.

---

### 13. GenerateFString (Interpolated Strings)

**Location:** Lines 1229-1254

**Purpose:** Convert Python f-strings to C# interpolated strings.

```python
f"Hello {name}"  →  $"Hello {name}"
```

**Algorithm:**
1. Iterate through f-string parts (text and expression segments)
2. For text parts: Create `InterpolatedStringText` with text token
3. For expression parts: Create `Interpolation` with generated expression
4. Combine into `InterpolatedStringExpression`

---

### 14. GetFullyQualifiedTypeName (Cross-File Type References)

**Location:** Lines 1259-1292

**Purpose:** Generate fully qualified names for types defined in other files.

```python
# File: animal.spy
class Dog: ...

# File: main.spy
from animal import Dog
dog = Dog()  →  new MyProject.Animal.Exports.Dog()
```

**Algorithm:**
1. Check if type's `DefiningFilePath` differs from current file
2. If cross-file: Build qualified name `{ProjectNamespace}.{ModuleName}.Exports.{TypeName}`
3. If same file: Use simple PascalCase name

**Why `Exports`?** All Sharpy modules generate a static `Exports` class containing public symbols.

---

## Dependencies

### Internal Dependencies

**From `RoslynEmitter.cs` (main class):**
- `_context: CodeGenContext` - Symbol lookup, semantic binding, source file path
- `_typeMapper: TypeMapper` - Maps Sharpy types to C# types
- `_targetTypeContext: TypeAnnotation?` - For collection literal type inference
- `_tempVarCounter: int` - Unique temp variable generation
- `GetMangledVariableName()` - Variable name resolution with versioning
- `EscapeCSharpKeyword()` - C# keyword escaping (`base` → `@base`)

**From `RoslynEmitter.Operators.cs`:**
- `GenerateFloorDivision()` - Floor division implementation
- `GenerateTryExpression()` - `try` expression wrapper
- `GenerateMaybeExpression()` - `maybe` expression wrapper
- `IsFloatExpression()` - Float type detection
- `IsEnumTypeExpression()` - Enum type detection

**From `Sharpy.Compiler.Semantic`:**
- `CodeGenContext` - Provides semantic context and symbol resolution
- `Symbol`, `TypeSymbol`, `ModuleSymbol` - Symbol table entries
- `TypeKind` - Enum for class/struct/enum/interface

**From `Sharpy.Compiler.Parser.Ast`:**
- All expression AST node types (`Expression`, `BinaryOp`, `FunctionCall`, etc.)
- Type annotations (`TypeAnnotation`)

### External Dependencies

**Microsoft.CodeAnalysis.CSharp (Roslyn):**
- `SyntaxFactory` - Factory methods for building syntax trees (imported statically)
- `ExpressionSyntax` - Base type for all C# expressions
- `SyntaxKind` - Enum of all C# syntax node kinds

**Why Roslyn?** Provides a strongly-typed, immutable API for building C# syntax trees. Much safer than string templating.

---

## Patterns and Design Decisions

### 1. SyntaxFactory Exclusive Usage

**Rule:** All C# code generation uses Roslyn's `SyntaxFactory` methods - **no string templating**.

**Why:**
- **Type safety** - Compile-time checking of syntax tree structure
- **Correctness** - Handles escaping, precedence, formatting automatically
- **Maintainability** - Refactoring is easier with strongly-typed nodes

**Example:**
```csharp
// ✅ Good (SyntaxFactory)
BinaryExpression(SyntaxKind.AddExpression, left, right)

// ❌ Bad (string templating)
$"{left} + {right}"
```

---

### 2. Immutable AST Pattern

**Rule:** AST nodes are never modified - annotations go in `SemanticInfo`, not AST.

**Why:** Allows reusing AST across multiple compilation passes without mutation bugs.

**Example:**
```csharp
// Type information stored separately
var symbol = _context.LookupSymbol(name);
var typeInfo = symbol.Type;  // From SemanticInfo, not AST
```

---

### 3. Name Mangling Strategy

**Contexts (from `NameMangler` class):**
- **Variables:** snake_case → camelCase
- **Functions/Methods:** snake_case → PascalCase
- **Types:** snake_case → PascalCase
- **Constants:** UPPER_SNAKE_CASE → CONSTANT_CASE (preserved)
- **Enum members:** RED → Red (PascalCase for int enums, CONSTANT_CASE for string enums)

**Helper method:** `NameMangler.Transform(name, context)` centralizes all naming logic.

**Why different rules?** Follows .NET conventions while preserving Python idioms.

---

### 4. Symbol Resolution Precedence

**Order of resolution (from `GetMangledVariableName` in main class):**

1. **Local variables** - Check `_variableVersions` dictionary
2. **Local constants** - Check `_constVariables` set
3. **Type symbols** - Check `SymbolTable` for class/struct
4. **Module symbols** - Check `SymbolTable` for imports
5. **CodeGenInfo** - Check symbol's code generation metadata
6. **Fallback** - Create new local variable entry

**Why this order?** Local scope shadows module scope; parameters shadow globals.

---

### 5. Builtin Function Qualification

**Rule:** Builtin functions **always** use fully qualified names with `global::` prefix.

```csharp
global::Sharpy.Core.Exports.Len(items)
global::Sharpy.Core.Exports.Print("hello")
```

**Why:**
- Prevents shadowing if user defines `len = 5` locally
- Ensures calls always resolve to standard library
- `global::` escapes any nested namespace ambiguity

---

### 6. Python Semantic Fidelity

**Examples of preserving Python behavior:**

#### Floor Division
```python
-7 // 2  →  -4  # Floors toward negative infinity, not toward zero
```
C# `int / int` truncates toward zero, so we use `Math.Floor((double)x / y)`.

#### True Division
```python
5 / 2  →  2.5  # Always returns float
```
C# `int / int` returns `int`, so we cast to `double` first.

#### Identity vs. Equality
```python
x is None  →  x == null  # Reference comparison
```
Python's `is` checks object identity, not equality.

#### Enum Values
```python
Color.RED.value  →  (int)Color.Red  # Get underlying integer
```
Python enums have a `.value` property; C# requires casting.

---

### 7. Target-Typed Collections

**Pattern:** Use `_targetTypeContext` for type inference.

**Setup (in statement generator):**
```csharp
_targetTypeContext = typeAnnotation;  // e.g., list[int]
var expr = GenerateExpression(literal);
_targetTypeContext = null;  // Reset after use
```

**Why:** Allows empty collections to know their element type:
```python
nums: list[int] = []  # Without target type, we can't infer element type
```

---

## Debugging Tips

### 1. Use the `emit` Command

**Quick inspection of generated C#:**
```bash
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy
```

**Compare AST and generated code:**
```bash
dotnet run --project src/Sharpy.Cli -- emit ast file.spy
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy
```

---

### 2. Check for Pattern Matching Exhaustiveness

**Common issue:** Adding a new expression type but forgetting to handle it here.

**Symptom:** `NotImplementedException: Expression type not implemented: NewExprType`

**Fix:** Add new case to `GenerateExpression()` switch expression (line 19-71).

---

### 3. Name Resolution Issues

**Symptom:** Variable `foo` generated as `Foo` instead of `foo`, or vice versa.

**Debug approach:**
1. Check if symbol is in `_context.SymbolTable`
2. Check `symbol.CodeGenInfo` for pre-computed name
3. Check `_variableVersions` for local redeclaration
4. Trace through `GetMangledVariableName()` logic

**Common cause:** Mixing module-level and local variable resolution.

---

### 4. Type Inference Failures

**Symptom:** Generic collection created with `object` element type instead of specific type.

**Debug approach:**
1. Check if `_targetTypeContext` is set before calling `GenerateExpression()`
2. Verify `_typeMapper.InferElementType()` has enough information
3. Add explicit type annotations to test case

**Example:**
```python
# Fails to infer (mixed types)
items = [1, "hello"]  →  List<object>

# Successful inference
items: list[int] = [1, 2, 3]  →  List<int>
```

---

### 5. Cross-Module References

**Symptom:** Generated code references `Point` instead of `MyProject.Geometry.Exports.Point`.

**Debug approach:**
1. Check `symbol.DefiningFilePath` - does it match current file?
2. Check `symbol.DefiningModule` - is it from an import?
3. Verify `GetFullyQualifiedTypeName()` logic (lines 1259-1292)
4. Check if module was imported correctly in semantic analysis

**Common cause:** Symbol lookup returning null, falling back to simple name.

---

### 6. Comprehension LINQ Bugs

**Symptom:** Comprehension generates invalid LINQ or wrong results.

**Debug approach:**
1. Check loop variable name mangling (should be camelCase)
2. Verify lambda parameter matches usage in body
3. Check clause order (ForClause must be first)
4. Test with simple comprehension, then add complexity

**Example issue:**
```python
# BUG: Wrong variable name in Select
[X for X in items]  →  items.Select(x => X)  # Should be: x => x

# Caused by inconsistent casing in lambda param vs. body
```

---

## Contribution Guidelines

### What kinds of changes might be made to this file?

#### 1. Adding New Expression Types

**When:** New expression syntax added to parser (e.g., match expressions, walrus operator).

**Steps:**
1. Add new case to `GenerateExpression()` switch (line 19-71)
2. Implement `GenerateXxxExpression()` method
3. Add unit tests in `Sharpy.Compiler.Tests`
4. Add integration test in `TestFixtures/`

**Example:**
```csharp
// In GenerateExpression():
MatchExpression match => GenerateMatchExpression(match),

// New method:
private ExpressionSyntax GenerateMatchExpression(MatchExpression match)
{
    // Implementation using SyntaxFactory
}
```

---

#### 2. Fixing Python Semantic Bugs

**When:** Generated C# doesn't match Python behavior.

**Example:** Floor division currently returns `int`, but spec says it should return `int64`.

**Steps:**
1. Add failing test case demonstrating incorrect behavior
2. Locate relevant generator method (`GenerateFloorDivision` in this case)
3. Fix implementation to match Python semantics
4. Update tests
5. Check `.NET Axiom Guardian` for .NET compatibility

---

#### 3. Improving LINQ Comprehension Generation

**When:** Current LINQ chains are inefficient or don't support all features.

**Current limitations:**
- Multiple `for` clauses not supported (line 600-605)
- Tuple unpacking not supported (line 643-646)
- Intermediate values re-evaluated in comparison chains (line 1073)

**Approach:**
1. For complex comprehensions, switch to imperative code generation (foreach loops)
2. Use TODO at lines 557-559 as guide
3. Consider complexity heuristic to decide LINQ vs. imperative

---

#### 4. Adding Optimization Passes

**When:** Generated C# is correct but verbose or inefficient.

**Examples:**
- Constant folding: `1 + 2` → `3`
- Null check optimization: `x is not None` already optimized to `x != null` (line 316-320)
- Comparison chain optimization: Store intermediate values in temps (line 1073 TODO)

**Pattern:**
1. Detect optimization opportunity in generator
2. Generate simplified C# syntax
3. Ensure correctness is preserved
4. Add tests covering edge cases

---

#### 5. Supporting New .NET Features

**When:** Targeting C# 10+ instead of C# 9.

**Current constraint:** C# 9.0 target (no global usings, file-scoped namespaces, record structs).

**Future enhancement:** Could use C# 10's global usings, C# 11's raw string literals, etc.

**Steps:**
1. Check project constraint in `CLAUDE.md` and spec
2. Update `RoslynEmitter` to use new syntax features
3. Update compiler output validation
4. Ensure backward compatibility if needed

---

### Code Style Guidelines

#### 1. Use Static Import for SyntaxFactory

```csharp
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

// Then use methods directly:
BinaryExpression(...)  // Not SyntaxFactory.BinaryExpression(...)
```

#### 2. Method Naming Convention

```csharp
GenerateXxxExpression(...)  // For expression generators
GenerateXxxStatement(...)   // For statement generators (in other partial file)
TryGenerateXxx(...)         // For optional/conditional generation
```

#### 3. Comment Complex Transformations

**Good:**
```csharp
// x ** y → System.Math.Pow(x, y)
// Note: We use fully qualified System.Math to avoid conflicts
case BinaryOperator.Power:
    return InvocationExpression(...);
```

**Bad:**
```csharp
case BinaryOperator.Power:
    return InvocationExpression(...);  // No explanation
```

#### 4. Keep Pattern Matching Readable

**Prefer:**
```csharp
return expr switch
{
    IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
    FloatLiteral floatLit => GenerateFloatLiteral(floatLit),
    // ...
};
```

**Over:**
```csharp
if (expr is IntegerLiteral intLit) return GenerateIntegerLiteral(intLit);
if (expr is FloatLiteral floatLit) return GenerateFloatLiteral(floatLit);
// ...
```

---

## Cross-References

This file is part of the `RoslynEmitter` partial class. Related files:

- **[RoslynEmitter.cs](./RoslynEmitter.md)** - Main class definition, fields, context management, name resolution
- **[RoslynEmitter.Statements.cs](./RoslynEmitter.Statements.md)** - Statement generation (if, while, for, etc.)
- **[RoslynEmitter.Operators.cs](./RoslynEmitter.Operators.md)** - Operator overloads, try/maybe expressions, helper methods
- **[RoslynEmitter.TypeDeclarations.cs](./RoslynEmitter.TypeDeclarations.md)** - Class, enum, struct, interface definitions
- **[RoslynEmitter.ClassMembers.cs](./RoslynEmitter.ClassMembers.md)** - Methods, properties, fields
- **[RoslynEmitter.CompilationUnit.cs](./RoslynEmitter.CompilationUnit.md)** - Top-level compilation unit generation

**Upstream dependencies:**
- **Semantic Analysis** - Provides `CodeGenContext`, `SymbolTable`, type checking results
- **TypeMapper** - Maps Sharpy type annotations to C# types
- **NameMangler** - Handles naming convention transformations

**Downstream consumers:**
- **C# Compilation** - Roslyn compiles generated syntax trees to .NET IL
- **Unity Integration** - Generated C# classes can be used in Unity projects

**Related specifications:**
- `docs/language_specification/expressions.md` - Expression semantics
- `docs/language_specification/operator_precedence.md` - Operator rules
- `docs/language_specification/dotnet_interop.md` - .NET interoperability
