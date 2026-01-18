# Walkthrough: RoslynEmitter.Operators.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs`

---

## Overview

This file is a **partial class** of `RoslynEmitter` responsible for:

1. **Operator Overload Generation**: Converting Python dunder methods (`__add__`, `__eq__`, etc.) into C# operator overloads
2. **Special Expression Handling**: Generating code for `try` expressions, `maybe` expressions, and floor division
3. **Loop Else Support**: Transforming break statements for Python's `for/else` and `while/else` constructs
4. **Utility Functions**: Helper methods for expression analysis, temporary variable naming, and identifier collection

This partial class handles the "glue" between Python's operator semantics and C#'s operator overloading system, ensuring that Sharpy classes can use Python-style operators naturally while generating idiomatic C# code.

**Compiler Pipeline Position**: Code Generation (final phase)
- **Input**: Typed AST with `FunctionDef` nodes representing dunder methods
- **Output**: Roslyn `OperatorDeclarationSyntax` nodes for C# operator overloads

---

## Class Structure

This file extends the partial class `RoslynEmitter` with the following categories of methods:

### Operator Generation Methods
- `TryGenerateOperatorOverload()` - Dispatcher for dunder-to-operator conversion
- `GenerateBinaryOperator()` - Creates `operator +`, `operator *`, etc.
- `GenerateComparisonOperator()` - Creates `operator ==`, `operator <`, etc.
- `GenerateUnaryOperator()` - Creates `operator -`, `operator ~`, etc.
- `GenerateComplementaryEqualsOperator()` - Auto-generates `==` when `!=` exists
- `GenerateComplementaryNotEqualsOperator()` - Auto-generates `!=` when `==` exists

### Special Expression Methods
- `GenerateTryExpression()` - Wraps expressions in `Result[T, E]` for exception handling
- `GenerateMaybeExpression()` - Wraps nullable expressions in `Optional[T]`
- `GenerateFloorDivision()` - Implements Python floor division semantics

### Analysis and Utility Methods
- `ShouldGenerateDunderMethod()` - Determines if a dunder should become a C# method
- `IsFloatExpression()` - Detects if an expression evaluates to float (for division semantics)
- `IsEnumTypeExpression()` - Checks if an expression is an enum (for `.value` translation)
- `CollectReferencedIdentifiers()` - Recursively finds all identifiers in an expression

### Loop Transformation Methods
- `TransformLoopBodyForElse()` - Wraps break statements with flag assignment
- `TransformStatementForLoopElse()` - Recursively transforms individual statements
- `GenerateTempVarName()` - Creates unique temporary variable names

---

## Key Functions and Methods

### 1. Operator Overload Dispatcher

#### `TryGenerateOperatorOverload(FunctionDef funcDef, string className)`

**Purpose**: Maps Python dunder methods to C# operator overloads.

**How It Works**:
```csharp
return funcDef.Name switch
{
    "__add__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.PlusToken),
    "__eq__" => GenerateComparisonOperator(funcDef, className, SyntaxKind.EqualsEqualsToken),
    "__neg__" => GenerateUnaryOperator(funcDef, className, SyntaxKind.MinusToken),
    "__getitem__" => null,  // Requires indexer syntax, not operator
    _ => null
};
```

**Supported Operators**:
- **Arithmetic**: `__add__`, `__sub__`, `__mul__`, `__truediv__`, `__mod__`
- **Bitwise**: `__and__`, `__or__`, `__xor__`, `__lshift__`, `__rshift__`
- **Comparison**: `__eq__`, `__ne__`, `__lt__`, `__le__`, `__gt__`, `__ge__`
- **Unary**: `__neg__`, `__pos__`, `__invert__`

**Not Supported as Operators**:
- `__pow__`: No `**` operator in C# (use `Math.Pow()` method)
- `__getitem__`/`__setitem__`: Require indexer syntax, handled elsewhere

**Connection to Upstream**: Called from `RoslynEmitter.ClassMembers.cs` when processing class methods.

---

### 2. Binary Operator Generation

#### `GenerateBinaryOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)`

**Purpose**: Creates a C# operator overload that delegates to the dunder method.

**Generated Pattern**:
```csharp
// Python:
class Vector:
    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

// Generated C#:
public static Vector operator +(Vector left, Vector right)
{
    return left.Add(right);  // Calls the Add() method (mangled __add__)
}
```

**Key Implementation Details**:
- **Parameter Filtering**: Skips the `self` parameter (case-insensitive)
- **Type Mapping**: Uses `_typeMapper.MapType()` to convert Python types to C# types
- **Return Type**: Defaults to the class type if no return type annotation exists
- **Name Mangling**: Converts `__add__` → `Add` using `NameMangler.Transform()`
- **Delegation Pattern**: Operator calls the instance method, allowing user override

**Why Delegation?**
The operator doesn't duplicate the implementation—it calls the transformed dunder method. This means:
1. Users can override the method in derived classes
2. The implementation exists in one place (the method)
3. Operators provide syntactic sugar for the method call

**Example with Custom Type**:
```python
# Python
def __mul__(self, scalar: int) -> Vector:
    ...

# Generated C#
public static Vector operator *(Vector left, int right)
{
    return left.Mul(right);
}
```

---

### 3. Comparison Operator Generation

#### `GenerateComparisonOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)`

**Purpose**: Creates comparison operators that always return `bool`.

**Key Difference from Binary Operators**:
```csharp
// Binary operators can return any type
var returnType = funcDef.ReturnType != null
    ? _typeMapper.MapType(funcDef.ReturnType)
    : IdentifierName(className);

// Comparison operators ALWAYS return bool
var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));
```

**Generated Pattern**:
```csharp
public static bool operator ==(Vector left, Vector right)
{
    return left.Eq(right);  // Calls Eq() method
}
```

**C# Requirement**: If you define `==`, you must define `!=` (and vice versa). See complementary operators below.

---

### 4. Unary Operator Generation

#### `GenerateUnaryOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)`

**Purpose**: Creates single-operand operators like negation or bitwise NOT.

**Generated Pattern**:
```csharp
// Python:
def __neg__(self) -> Vector:
    return Vector(-self.x, -self.y)

// Generated C#:
public static Vector operator -(Vector value)
{
    return value.Neg();  // Calls Neg() method
}
```

**Supported Unary Operators**:
- `__neg__` → `operator -` (negation)
- `__pos__` → `operator +` (unary plus)
- `__invert__` → `operator ~` (bitwise NOT)

---

### 5. Complementary Operators

#### `GenerateComplementaryEqualsOperator(string className)`
#### `GenerateComplementaryNotEqualsOperator(string className)`

**Purpose**: C# requires paired definition of `==` and `!=`. These methods auto-generate the missing operator.

**Why This Exists**: C# compiler error CS0216 requires both operators to be defined together.

**Implementation**:
```csharp
// If user defines __eq__, generate __ne__ automatically:
public static bool operator !=(ClassName left, ClassName right)
{
    return !(left == right);  // Delegate to ==
}

// If user defines __ne__, generate __eq__ automatically:
public static bool operator ==(ClassName left, ClassName right)
{
    return !(left != right);  // Delegate to !=
}
```

**Note**: These are likely called from `RoslynEmitter.ClassMembers.cs` when it detects only one of the pair is defined.

---

### 6. Try Expression Support

#### `GenerateTryExpression(TryExpression tryExpr)`

**Purpose**: Implements Sharpy's `try` operator for safe exception handling.

**Python-to-C# Translation**:
```python
# Sharpy code:
result = try read_file("data.txt")
result_specific = try[FileNotFoundError] read_file("data.txt")

# Generated C#:
var result = global::Sharpy.Core.Result.Try(() => ReadFile("data.txt"));
var resultSpecific = global::Sharpy.Core.Result.Try<FileNotFoundError>(() => ReadFile("data.txt"));
```

**Return Type**: `Result[T, Exception]` where `T` is the expression's type.

**Key Features**:
- Wraps expression in a lambda: `() => operand`
- Uses fully-qualified name: `global::Sharpy.Core.Result` to avoid namespace collisions
- Supports generic exception types: `try[ExceptionType]` → `Result.Try<ExceptionType>()`
- Defaults to catching all exceptions: `try expr` → catches `Exception`

**Connection to Standard Library**: Relies on `Sharpy.Core.Result` type (likely a discriminated union).

---

### 7. Maybe Expression Support

#### `GenerateMaybeExpression(MaybeExpression maybeExpr)`

**Purpose**: Converts nullable C# values to Sharpy's `Optional[T]` type.

**Translation**:
```python
# Sharpy code:
opt = maybe nullable_value

# Generated C#:
var opt = global::Sharpy.Core.Optional.From(nullableValue);
```

**Use Case**: Bridges C# nullable references (`string?`) with Sharpy's type-safe option type.

**Connection to Standard Library**: Uses `Sharpy.Core.Optional.From()` static method.

---

### 8. Floor Division Implementation

#### `GenerateFloorDivision(ExpressionSyntax left, ExpressionSyntax right, bool hasFloatOperand)`

**Purpose**: Implements Python's `//` operator with correct floor-toward-negative-infinity semantics.

**Critical Difference from C# `/` Operator**:
```python
# Python floor division
-7 // 2  # → -4 (floors toward -∞)

# C# integer division
-7 / 2   # → -3 (truncates toward 0)
```

**Implementation Strategy**:
```csharp
// For integer operands: (int)Math.Floor((double)a / b)
// For float operands: Math.Floor((double)(a / b))
```

**Generated Code Example**:
```csharp
// x // y where both are int
(int)System.Math.Floor((double)((double)x / y))

// x // y where either is float
System.Math.Floor((double)(x / y))
```

**Design Decisions**:
1. **Always cast to `double`**: Avoids CS0121 ambiguity between `Math.Floor(double)` and `Math.Floor(decimal)`
2. **Fully-qualified `System.Math`**: Prevents conflicts with `Sharpy.Math` namespace
3. **Pragmatic int32 return**: Spec says return `int64`, but uses `int32` for .NET compatibility
4. **Float detection**: Uses `IsFloatExpression()` to determine if result should be `double`

**See Also**: `docs/language_specification/arithmetic_operators.md` for floor division semantics.

---

### 9. Loop Else Support

#### `TransformLoopBodyForElse(List<Statement> body, string flagName)`
#### `TransformStatementForLoopElse(Statement stmt, string flagName)`

**Purpose**: Implements Python's `for/else` and `while/else` constructs where the `else` block runs only if the loop completes without `break`.

**Python Semantics**:
```python
for item in items:
    if item == target:
        break
else:
    print("Not found")  # Runs only if break was NOT hit
```

**Transformation Strategy**:
1. Create a boolean flag (initialized to `true`)
2. Transform `break` statements to set flag to `false` before breaking
3. Check flag after loop to determine if `else` block should run

**AST Transformation**:
```csharp
// Original AST:
BreakStatement

// Transformed AST:
BreakWithFlagStatement { FlagName = "loopCompleted_1" }
```

**Recursive Handling**:
- **Transforms**: `if` statements (recursively processes branches)
- **Preserves**: Nested loops (their breaks apply to their own loop, not the outer one)
- **Passes through**: All other statements unchanged

**Generated C# Pattern** (conceptual):
```csharp
bool loopCompleted_1 = true;
foreach (var item in items)
{
    if (item == target)
    {
        loopCompleted_1 = false;
        break;
    }
}
if (loopCompleted_1)
{
    Console.WriteLine("Not found");
}
```

**Note**: The actual C# generation likely happens in `RoslynEmitter.Statements.cs`.

---

### 10. Expression Analysis Utilities

#### `IsFloatExpression(Expression expr)`

**Purpose**: Determines if an expression will evaluate to a floating-point type.

**Why This Matters**: Python's division semantics differ for int vs. float:
- `10 / 3` → `3.333...` (always float in Python 3)
- `10 // 3` → `3` (floor division, result type depends on operands)

**Detection Strategy**:
```csharp
return expr switch
{
    FloatLiteral => true,
    UnaryOp unary => IsFloatExpression(unary.Operand),
    BinaryOp binOp => binOp.Operator switch
    {
        BinaryOperator.Divide => true,        // Always produces float
        BinaryOperator.Power => true,         // Math.Pow returns double
        BinaryOperator.FloorDivide => IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right),
        _ => IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right)
    },
    Parenthesized paren => IsFloatExpression(paren.Expression),
    _ => false  // Conservative: assume int for unknowns
};
```

**Limitations**:
- Assumes variables and function calls are integers (conservative)
- A full implementation would query the semantic analyzer's type information

**Use Case**: Called by `GenerateFloorDivision()` and likely `GenerateDivision()` in `RoslynEmitter.Expressions.cs`.

---

#### `IsEnumTypeExpression(Expression expr)`

**Purpose**: Checks if an expression evaluates to an enum type.

**Why This Matters**: Sharpy allows `.value` access on enums:
```python
color = Color.RED
numeric_value = color.value  # Access underlying int value
```

**Implementation**:
```csharp
if (expr is Identifier id)
{
    var symbol = _context.LookupSymbol(id.Name);
    if (symbol is VariableSymbol varSymbol &&
        varSymbol.Type is UserDefinedType udt &&
        udt.Symbol?.TypeKind == TypeKind.Enum)
    {
        return true;
    }
}
return false;
```

**Connection to Semantic Analysis**: Queries `_context` (the `CodeGenContext`) which wraps `SemanticInfo` from the semantic analyzer.

**Use Case**: Likely used in member access generation to translate `.value` → `(int)enumVar`.

---

#### `CollectReferencedIdentifiers(Expression? expr, HashSet<string> identifiers)`

**Purpose**: Recursively finds all identifiers referenced in an expression.

**Why This Matters**: Determines whether a module-level variable declaration should be:
- A **field** (if referenced by other fields)
- A **local in Main** (if only used in imperative code)

**Recursive Traversal**:
```csharp
switch (expr)
{
    case Identifier id:
        identifiers.Add(id.Name);  // Base case
        break;
    case BinaryOp binOp:
        CollectReferencedIdentifiers(binOp.Left, identifiers);
        CollectReferencedIdentifiers(binOp.Right, identifiers);
        break;
    case FunctionCall call:
        CollectReferencedIdentifiers(call.Function, identifiers);
        foreach (var arg in call.Arguments)
            CollectReferencedIdentifiers(arg, identifiers);
        // ... keyword args
        break;
    // ... 20+ more cases
}
```

**Handles**:
- Binary/unary operators
- Function calls (function expression + arguments)
- Member access (recurse on object)
- Collection literals (lists, dicts, sets, tuples)
- Comprehensions (element expression + iterator/condition clauses)
- Lambda expressions (body may reference outer scope)
- F-strings (interpolated expressions)
- Conditional expressions

**Use Case**: Called during module initialization to build dependency graph for variable ordering.

---

### 11. Utility Methods

#### `GenerateTempVarName(string prefix)`

**Purpose**: Creates unique temporary variable names.

**Implementation**:
```csharp
private string GenerateTempVarName(string prefix)
{
    return $"__{prefix}_{_tempVarCounter++}";
}
```

**Generated Names**: `__loopCompleted_1`, `__temp_2`, `__result_3`, etc.

**Design**: Double underscore prefix avoids collision with user variables (Python convention).

---

#### `ShouldGenerateDunderMethod(string dunderName)`

**Purpose**: Determines if a dunder method should generate a C# method (vs. just an operator).

**Decision Logic**:
```csharp
if (dunderName == "__init__")
    return true;  // Special constructor handling

return ProtocolRegistry.IsProtocolDunder(dunderName);
```

**Protocol Dunders**: Methods that map to .NET protocols (e.g., `__str__` → `ToString()`, `__iter__` → `GetEnumerator()`).

**Why This Matters**:
- Some dunders become **only operators** (`__add__` → `operator +`)
- Some dunders become **only methods** (`__str__` → `ToString()`)
- Some dunders become **both** (e.g., `__eq__` → both `operator ==` and `Equals()` method)

**Avoids Conflicts**: Prevents generating redundant methods that clash with user-defined methods.

**Connection**: Likely used in `RoslynEmitter.ClassMembers.cs` when deciding which members to emit.

---

## Dependencies

### Internal Dependencies (Sharpy Codebase)

1. **`Sharpy.Compiler.Parser.Ast`**
   - All expression types: `BinaryOp`, `UnaryOp`, `FunctionCall`, etc.
   - Statement types: `IfStatement`, `WhileStatement`, `ForStatement`, `BreakStatement`
   - Literals: `IntegerLiteral`, `FloatLiteral`, etc.
   - Special expressions: `TryExpression`, `MaybeExpression`

2. **`Sharpy.Compiler.Semantic`**
   - `SemanticInfo`: Type information and symbol tables
   - `VariableSymbol`: Variable type information
   - `UserDefinedType`: Custom class/enum types
   - `TypeKind`: Enum distinguishing class vs. enum vs. interface

3. **`CodeGenContext`** (from `RoslynEmitter.cs`)
   - `_context`: Wraps `SemanticInfo` for symbol lookup
   - `LookupSymbol()`: Resolves identifier names to symbols

4. **`TypeMapper`** (from `RoslynEmitter.cs`)
   - `_typeMapper.MapType()`: Converts Python type annotations to C# `TypeSyntax`
   - Handles primitives, generics, and user-defined types

5. **`NameMangler`** (likely in `CodeGen` namespace)
   - `NameMangler.Transform(name, NameContext.Method)`: Converts `__add__` → `Add`, `__init__` → constructor, etc.

6. **`ProtocolRegistry`** (likely in `CodeGen` or `Semantic`)
   - `IsProtocolDunder()`: Checks if a dunder implements a .NET protocol

### External Dependencies (Roslyn)

All from **`Microsoft.CodeAnalysis.CSharp`** and **`Microsoft.CodeAnalysis.CSharp.Syntax`**:

- **Syntax Factory** (imported as `using static SyntaxFactory`):
  - `OperatorDeclaration()`: Creates operator overload syntax
  - `Parameter()`, `ParameterList()`: Creates parameter lists
  - `IdentifierName()`, `GenericName()`: Creates type references
  - `BinaryExpression()`, `PrefixUnaryExpression()`: Creates expressions
  - `InvocationExpression()`, `MemberAccessExpression()`: Creates method calls
  - `Block()`, `ReturnStatement()`: Creates statement blocks

- **Syntax Kinds**:
  - Operator tokens: `SyntaxKind.PlusToken`, `SyntaxKind.EqualsEqualsToken`, etc.
  - Expression kinds: `SyntaxKind.SimpleMemberAccessExpression`, etc.
  - Keyword tokens: `SyntaxKind.PublicKeyword`, `SyntaxKind.StaticKeyword`, etc.

---

## Patterns and Design Decisions

### 1. Delegation Pattern for Operators

**Decision**: Operators delegate to instance methods rather than duplicating implementation.

```csharp
// Operator delegates to method:
public static Vector operator +(Vector left, Vector right)
{
    return left.Add(right);  // Calls the Add() method
}

// The method contains the actual implementation:
public Vector Add(Vector other)
{
    return new Vector(this.x + other.x, this.y + other.y);
}
```

**Benefits**:
- Single source of truth for implementation
- Enables polymorphism (derived classes can override the method)
- Separation of concerns (syntax sugar vs. logic)

**Tradeoff**: Slight performance overhead (extra method call), but JIT inlining likely eliminates this.

---

### 2. Conservative Type Inference

**Decision**: When type information is unavailable, assume safe defaults.

**Examples**:
- `IsFloatExpression()`: Returns `false` for unknown expressions (assumes int)
- `IsEnumTypeExpression()`: Returns `false` unless proven to be an enum
- Return types: Defaults to class type if no annotation exists

**Rationale**: Better to generate slightly less optimal code than to crash or produce incorrect semantics.

---

### 3. Fully-Qualified Names for Standard Library

**Decision**: Use `global::Sharpy.Core.Result` instead of `Result`.

**Why**:
- Avoids namespace collision (user might have a `Result` class)
- Makes code generation deterministic (doesn't depend on `using` statements)
- Clearer dependency on standard library

**Applied To**:
- `global::Sharpy.Core.Result.Try()`
- `global::Sharpy.Core.Optional.From()`
- `System.Math.Floor()` (avoids conflict with `Sharpy.Math`)

---

### 4. AST Transformation for Loop Else

**Decision**: Transform AST nodes (`BreakStatement` → `BreakWithFlagStatement`) rather than generating C# directly.

**Benefits**:
- Separation of concerns: This file transforms AST, `RoslynEmitter.Statements.cs` generates C#
- Easier testing: Can verify transformation without generating full C# code
- Reusability: Transformed AST can be analyzed by other passes

**Note**: `BreakWithFlagStatement` is likely a custom AST node type.

---

### 5. Pattern Matching for Operator Dispatch

**Decision**: Use C# switch expressions for clean, extensible operator mapping.

```csharp
return funcDef.Name switch
{
    "__add__" => GenerateBinaryOperator(...),
    "__eq__" => GenerateComparisonOperator(...),
    "__neg__" => GenerateUnaryOperator(...),
    _ => null
};
```

**Benefits**:
- Exhaustiveness checking (compiler warns on missing cases)
- Easy to add new operators
- Self-documenting (all mappings in one place)

---

### 6. Pragmatic Floor Division Semantics

**Decision**: Return `int32` for integer floor division instead of spec-mandated `int64`.

**Rationale** (from code comments):
> Spec says integer floor division should return int64, but we return int32 for .NET compatibility with most use cases (augmented assignment, common variables).

**Tradeoff**: Technically non-compliant with spec, but more ergonomic for .NET developers.

**See Also**: `docs/language_specification/arithmetic_operators.md` for official specification.

---

## Debugging Tips

### 1. Operator Not Generated

**Symptom**: Python class with `__add__` but C# code doesn't have `operator +`.

**Debug Steps**:
1. Check if `TryGenerateOperatorOverload()` is called (likely in `RoslynEmitter.ClassMembers.cs`)
2. Verify dunder name matches exactly (case-sensitive)
3. Check parameter count (binary operators need 2 params including `self`)
4. Ensure return type is mappable by `TypeMapper`

**Common Gotcha**: If the method has wrong parameter count, it throws `InvalidOperationException` in `GenerateBinaryOperator()`:
```csharp
if (otherParam == null)
{
    throw new InvalidOperationException($"Binary operator {funcDef.Name} must have at least 2 parameters");
}
```

---

### 2. Floor Division Generates Wrong Result

**Symptom**: `x // y` produces wrong value (e.g., truncates instead of floors).

**Debug Steps**:
1. Check if `hasFloatOperand` is computed correctly
2. Verify `IsFloatExpression()` handles your expression type
3. Add debug output to see the generated C# expression

**Example**:
```python
x = -7
y = 2
result = x // y  # Should be -4, not -3
```

**Check Generated C#**:
```csharp
// Should generate:
(int)System.Math.Floor((double)((double)x / y))

// NOT just:
x / y  // This would give -3 (truncation)
```

---

### 3. Wrong Method Called from Operator

**Symptom**: `a + b` calls wrong method or method doesn't exist.

**Debug Steps**:
1. Check name mangling: `__add__` should become `Add`
2. Verify `NameMangler.Transform()` is using `NameContext.Method`
3. Ensure the method was actually generated in the class (check `RoslynEmitter.ClassMembers.cs`)

**Debugging Trick**: Add a breakpoint in `GenerateBinaryOperator()` at line 108:
```csharp
var methodName = NameMangler.Transform(funcDef.Name, NameContext.Method);
// Check methodName value here
```

---

### 4. Loop Else Block Doesn't Execute

**Symptom**: Python `for/else` generates C# code but `else` block always/never runs.

**Debug Steps**:
1. Check if `TransformLoopBodyForElse()` was called
2. Verify break statements were transformed to `BreakWithFlagStatement`
3. Ensure nested `if` statements had their bodies transformed recursively
4. Check that nested loops were NOT transformed (they should be skipped)

**Common Issue**: Forgetting to transform `elif` clauses:
```csharp
ElifClauses = ifStmt.ElifClauses.Select(e => e with
{
    Body = TransformLoopBodyForElse(e.Body, flagName)  // Must transform!
}).ToList(),
```

---

### 5. Try/Maybe Expression Type Errors

**Symptom**: Generated C# has type mismatch when using `try` or `maybe`.

**Debug Steps**:
1. Verify `Sharpy.Core.Result` and `Sharpy.Core.Optional` types exist
2. Check if fully-qualified names resolve correctly
3. Ensure lambda expression body is generated correctly

**Check Standard Library**:
```csharp
// These must exist in Sharpy.Core:
Result.Try<TException>(Func<T> fn)
Optional.From<T>(T? value)
```

---

### 6. Missing Identifier in Collected References

**Symptom**: `CollectReferencedIdentifiers()` doesn't find an identifier.

**Debug Steps**:
1. Check if the expression type has a case in the switch statement
2. Verify recursive calls for composite expressions
3. Add your expression type if it's missing

**Example**: If lambda expressions weren't handled:
```csharp
case LambdaExpression lambda:
    CollectReferencedIdentifiers(lambda.Body, identifiers);  // Must recurse!
    break;
```

---

## Contribution Guidelines

### When to Modify This File

You should edit `RoslynEmitter.Operators.cs` when:

1. **Adding New Operator Support**
   - Example: Adding `__matmul__` for matrix multiplication (`@` operator)
   - Add case to `TryGenerateOperatorOverload()` switch
   - Create generator method if needed (likely reuse `GenerateBinaryOperator()`)

2. **Implementing New Special Expressions**
   - Example: Adding `await` expressions or `with` expressions
   - Create new `Generate*Expression()` method
   - Call it from `GenerateExpression()` in `RoslynEmitter.Expressions.cs`

3. **Fixing Operator Semantics**
   - Example: Correcting floor division behavior
   - Modify `GenerateFloorDivision()` or `IsFloatExpression()`

4. **Adding Expression Analysis**
   - Example: Detecting string expressions for concatenation optimization
   - Add new `Is*Expression()` helper method
   - Call it from relevant code generation methods

5. **Improving Loop Else Support**
   - Example: Handling `try/except` blocks within loops
   - Modify `TransformStatementForLoopElse()` to add new statement type

### Code Style Guidelines

**Follow Existing Patterns**:
```csharp
// Use switch expressions for dispatching
return funcDef.Name switch
{
    "__new_op__" => GenerateNewOperator(...),
    _ => null
};

// Use Roslyn SyntaxFactory fluent API
return OperatorDeclaration(returnType, Token(operatorToken))
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
    .WithParameterList(...)
    .WithBody(...);

// Document complex logic with XML comments
/// <summary>
/// Brief description
/// </summary>
private SomeReturnType SomeMethod(...)
```

**Naming Conventions**:
- Private methods: `GenerateXyz()`, `TransformXyz()`, `IsXyz()`, `CollectXyz()`
- Temp variables: `__prefix_N` with counter
- Method parameters: `left`/`right` for operators, `value` for unary

**Error Handling**:
- Throw `InvalidOperationException` for malformed AST (indicates compiler bug)
- Use conservative defaults when type info is missing (don't crash)

### Testing Your Changes

**Manual Testing**:
1. Write Sharpy code using your new feature
2. Compile with `sharpy compile test.spy`
3. Inspect generated C# code
4. Run compiled program to verify semantics

**Unit Testing**:
- Add tests to `tests/Sharpy.Compiler.Tests/CodeGen/`
- Test both operator generation and semantics
- Include edge cases (missing types, nested structures, etc.)

**Integration Testing**:
- Add to `tests/integration/` with `.spy` source and expected output
- Verify end-to-end compilation and execution

**Example Test Cases**:
```python
# Test floor division with mixed types
assert 10 // 3 == 3
assert -7 // 2 == -4
assert 10.0 // 3 == 3.0

# Test operator overloading
v1 = Vector(1, 2)
v2 = Vector(3, 4)
v3 = v1 + v2  # Calls operator +
assert v3.x == 4

# Test try expression
result = try risky_operation()
assert result.is_ok or result.is_err
```

---

## Cross-References

### Related Partial Class Files

This file is part of the `RoslynEmitter` partial class. Other related files:

- **[RoslynEmitter.cs](RoslynEmitter.md)** - Main class definition, fields, constructor, entry point
- **[RoslynEmitter.Expressions.cs](RoslynEmitter.Expressions.md)** - Expression generation (calls methods from this file)
- **[RoslynEmitter.Statements.cs]** *(not yet documented)* - Statement generation (uses loop transformation from this file)
- **[RoslynEmitter.ClassMembers.cs](RoslynEmitter.ClassMembers.md)** - Method/field generation (calls `TryGenerateOperatorOverload()`)
- **[RoslynEmitter.TypeDeclarations.cs]** *(not yet documented)* - Class/enum/interface generation
- **[RoslynEmitter.CompilationUnit.cs](RoslynEmitter.CompilationUnit.md)** - Top-level file structure
- **[RoslynEmitter.ModuleClass.cs](RoslynEmitter.ModuleClass.md)** - Module initialization (uses `CollectReferencedIdentifiers()`)

### Dependencies on Other Modules

- **`NameMangler`** - Name transformation (dunder → PascalCase)
- **`TypeMapper`** - Type annotation mapping (Python → C#)
- **`CodeGenContext`** - Symbol lookup and semantic info access
- **`ProtocolRegistry`** - Dunder-to-protocol mapping

### Language Specification References

- **[docs/language_specification/arithmetic_operators.md]** - Floor division semantics, operator precedence
- **[docs/language_specification/operator_overloading.md]** - Dunder method conventions
- **[docs/language_specification/operator_precedence.md]** - Operator associativity and precedence
- **[docs/language_specification/dotnet_interop.md]** - How Sharpy operators map to .NET

### Standard Library Dependencies

- **`Sharpy.Core.Result`** - Used by `try` expressions
- **`Sharpy.Core.Optional`** - Used by `maybe` expressions
- **`System.Math`** - Used for floor division

---

## Summary

This file is the **operator translation layer** of the Sharpy compiler, bridging Python's operator semantics with C#'s operator overloading system. It handles:

1. **26 operator overloads** (binary, unary, comparison)
2. **Special Python features** (`try`, `maybe`, floor division, loop else)
3. **Type-aware code generation** (float detection, enum detection)
4. **Dependency analysis** (identifier collection)

**Key Insight**: Operators delegate to methods, keeping implementation in one place while providing syntactic sugar. This design enables polymorphism and follows C# best practices.

**When debugging**: Check parameter counts, verify name mangling, inspect type mapping, and ensure semantic info is available.

**When contributing**: Follow the switch-expression dispatch pattern, use Roslyn fluent API, document complex logic, and add comprehensive tests.
