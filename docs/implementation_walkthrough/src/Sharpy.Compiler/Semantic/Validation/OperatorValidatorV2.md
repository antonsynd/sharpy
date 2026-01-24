# Walkthrough: OperatorValidatorV2.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/OperatorValidatorV2.cs`

---

## Overview

`OperatorValidatorV2` is a semantic validation pass that ensures operators are used correctly in Sharpy code. It validates:

- **Binary operators**: arithmetic (`+`, `-`, `*`, `/`), comparison (`==`, `!=`, `<`, `>`), bitwise (`&`, `|`, `^`), and logical operators
- **Unary operators**: negation (`-`), positive (`+`), logical not (`not`), bitwise not (`~`)
- **Augmented assignment operators**: `+=`, `-=`, `*=`, etc.

This is the **pipeline-compatible** version of the original `OperatorValidator`. The key architectural difference is:

- **Legacy `OperatorValidator`**: Performed type inference *during* type-checking (now deprecated)
- **`OperatorValidatorV2`**: Performs **post-pass validation only**, assuming types have already been inferred by `TypeInferenceService`

The validator runs at **Order 500** in the semantic analysis pipeline, after type checking and access validation have completed. It examines the types attached to expressions in `SemanticInfo` and reports errors when operators are applied to incompatible types.

**Pipeline Position**: Parser → ... → Type Checker → OperatorValidatorV2 → CodeGen

---

## Class Structure

### Inheritance

```csharp
public class OperatorValidatorV2 : SemanticValidatorBase
```

`OperatorValidatorV2` inherits from `SemanticValidatorBase`, which implements the `ISemanticValidator` interface. This base class provides:

- Common error/warning reporting methods (`AddError`, `AddWarning`)
- A standardized interface for the validation pipeline

### Key Properties

```csharp
public override string Name => "OperatorValidator";
public override int Order => 500;

private ICompilerLogger _logger = NullLogger.Instance;
private SemanticContext _context = null!;
```

- **`Name`**: Identifies this validator in logs and diagnostics as "OperatorValidator" (matching the legacy version)
- **`Order`**: Pipeline execution order (500 = same as `ProtocolValidator`, after access validation at 400)
- **`_logger`**: Diagnostic logger for debug messages
- **`_context`**: Shared semantic context containing `SemanticInfo` (type annotations), symbol tables, and diagnostics

---

## Key Methods

### Entry Point: `Validate`

```csharp
public override void Validate(Module module, SemanticContext context)
```

**Purpose**: Main entry point called by the validation pipeline.

**Algorithm**:
1. Stores the context and logger for instance use
2. Logs a debug message: "Starting operator validation"
3. Iterates through all top-level statements in the module, calling `ValidateStatement` on each

**Note**: This validator is **stateless** between invocations—`_context` and `_logger` are reset on each call to enable potential future parallelization.

---

### Statement Tree Traversal: `ValidateStatement`

```csharp
private void ValidateStatement(Statement stmt)
```

**Purpose**: Recursively traverses the AST statement tree, descending into nested scopes and extracting expressions for validation.

**Handles**:
- **Definitions**: `FunctionDef`, `ClassDef`, `StructDef` — recurse into body statements
- **Control flow**: `ForStatement`, `WhileStatement`, `IfStatement`, `TryStatement` — validate test conditions and recurse into bodies
- **Expressions**: `ExpressionStatement`, `ReturnStatement` — extract and validate expressions
- **Assignments**: Special handling for augmented assignments (see below)
- **Variable declarations**: Validate initializer expressions

**Design Pattern**: Classic **Visitor pattern** using C# pattern matching on statement types.

---

### Expression Tree Traversal: `ValidateExpression`

```csharp
private void ValidateExpression(Expression expr)
```

**Purpose**: Recursively traverses the expression tree, validating operators and descending into subexpressions.

**Handles**:
- **Binary operators** (`BinaryOp`): Calls `ValidateBinaryOp` and recurses on left/right operands
- **Unary operators** (`UnaryOp`): Calls `ValidateUnaryOp` and recurses on operand
- **Function calls** (`FunctionCall`): Recurses on function expression and all arguments
- **Member/index access** (`MemberAccess`, `IndexAccess`): Recurses on object and index expressions
- **Literals** (`ListLiteral`, `DictLiteral`, `SetLiteral`, `TupleLiteral`): Recurses on all elements
- **Comprehensions**: Recurses on element expressions and clause conditions
- **Conditional expressions**: Recurses on test, then-value, and else-value
- **Parenthesized expressions**: Recurses on inner expression

**Design Pattern**: Another application of the **Visitor pattern** for expression nodes.

---

### Binary Operator Validation: `ValidateBinaryOp`

```csharp
private void ValidateBinaryOp(BinaryOp binOp)
```

**Purpose**: Validates that a binary operation is supported by the operand types.

**Algorithm**:
1. **Retrieve types**: Gets left and right operand types from `_context.SemanticInfo.GetExpressionType()`
2. **Early exit**: Returns if either type is null or `UnknownType` (indicates prior error, avoid cascading errors)
3. **Special-case operators**:
   - **Null coalesce (`??`)**: Calls `ValidateNullCoalesce` (left operand must be nullable)
   - **Logical/identity operators** (`and`, `or`, `is`, `is not`, `in`, `not in`): Always valid
4. **Default case**: Calls `ValidateArithmeticOrComparisonOp` for arithmetic, comparison, and bitwise operators

**Error Suppression**: Skips validation if types are unknown to prevent cascading errors from upstream type-checking failures.

---

### Arithmetic/Comparison Validation: `ValidateArithmeticOrComparisonOp`

```csharp
private void ValidateArithmeticOrComparisonOp(BinaryOp binOp, SemanticType leftType, SemanticType rightType)
```

**Purpose**: Validates that arithmetic, comparison, or bitwise operators are supported.

**Algorithm**:
1. **Convert operator to dunder method**: Maps the operator to its Python dunder method name (e.g., `+` → `__add__`)
2. **Check left operand**: Calls `SupportsOperator(leftType, dunderName)`
3. **Fallback for comparison operators**: If left type doesn't support the operator, checks if it's a comparison operator on a primitive type (all primitives support comparison)
4. **Reflected operator fallback**: If left type doesn't support the operator, tries the **reflected dunder method** on the right type (e.g., `__radd__`)
5. **Report error**: If neither the forward nor reflected operator is supported, reports an error

**Python Interop**: This implements Python's operator resolution strategy:
- Try `left.__op__(right)`
- If that fails, try `right.__rop__(left)` (reflected operator)

**Example**:
```python
# If 'int' doesn't support `+ MyClass`, check if MyClass supports `__radd__`
result = 5 + my_obj  # Tries: 5.__add__(my_obj), then my_obj.__radd__(5)
```

---

### Null Coalesce Validation: `ValidateNullCoalesce`

```csharp
private void ValidateNullCoalesce(BinaryOp binOp, SemanticType leftType, SemanticType rightType)
```

**Purpose**: Validates the null coalescing operator (`??`).

**Rule**: Left operand **must** be a `NullableType`. If not, reports an error.

**Example**:
```python
x: int? = None
y = x ?? 0  # Valid: x is nullable

z: int = 5
w = z ?? 0  # ERROR: z is not nullable
```

---

### Unary Operator Validation: `ValidateUnaryOp`

```csharp
private void ValidateUnaryOp(UnaryOp unaryOp)
```

**Purpose**: Validates unary operators (`-`, `+`, `not`, `~`).

**Algorithm**:
1. Gets operand type from `SemanticInfo`
2. Early exit if type is unknown
3. **Special case**: `not` operator is always valid (all types are truthy/falsy)
4. Converts operator to dunder method (`-` → `__neg__`, `~` → `__invert__`)
5. Checks if the type supports the dunder method
6. Reports error if unsupported

**Example**:
```python
x = -5      # Valid: int supports __neg__
y = ~"abc"  # ERROR: str does not support __invert__
```

---

### Augmented Assignment Validation: `ValidateAugmentedAssignment`

```csharp
private void ValidateAugmentedAssignment(Assignment assignment)
```

**Purpose**: Validates augmented assignment operators (`+=`, `-=`, etc.).

**Algorithm**:
1. Gets target and value types
2. Converts operator to **in-place dunder** (e.g., `+=` → `__iadd__`)
3. Checks if target type supports the in-place operator
4. **Fallback**: If in-place operator is not supported, checks for the regular binary operator (e.g., `__add__`)
   - This is because `x += y` can desugar to `x = x + y` if `__iadd__` is not defined
5. Reports error if neither is supported

**Python Semantics**: Mirrors Python's behavior where `x += y` tries:
1. `x.__iadd__(y)` (in-place)
2. `x = x.__add__(y)` (fallback to regular binary operator)

**Example**:
```python
my_list += [1, 2]  # Tries __iadd__ (mutates list), falls back to __add__ (creates new list)
```

---

### Type Support Check: `SupportsOperator`

```csharp
private bool SupportsOperator(SemanticType type, string dunderName)
```

**Purpose**: Core logic to determine if a type supports a given operator (dunder method).

**Algorithm**:
1. **String special case**: Checks if `SemanticType.Str` supports the operator
   - Supports: `__add__` (concatenation), `__mul__` (repetition), comparison operators
2. **Builtin numeric types**: Checks if builtin types (int, float, etc.) support the operator
   - Arithmetic: `__add__`, `__sub__`, `__mul__`, `__truediv__`, etc. → numeric types only
   - Bitwise: `__and__`, `__or__`, `__xor__`, etc. → `int` and `long` only
   - Unary: `__neg__`, `__pos__` → numeric types; `__invert__` → `int` and `long`
   - Comparison: All primitives support `__eq__`, `__ne__`, `__lt__`, etc.
3. **Generic types** (list, dict, set, tuple): Hardcoded operator support
   - `list`: `__add__`, `__iadd__`, `__mul__`, `__imul__`, `__eq__`, `__ne__`
   - `tuple`: `__add__`, `__mul__`, `__eq__`, `__ne__`
   - `set`: Set operations (`__or__`, `__and__`, `__sub__`, `__xor__`, in-place variants)
   - `dict`: `__or__`, `__ior__`, `__eq__`, `__ne__` (Python 3.9+ dict merge)
4. **User-defined types**: Checks if the type symbol defines the dunder method
   - Checks protocol methods (dunder methods from protocols/interfaces)
   - Checks regular methods

**Return**: `true` if the type supports the operator, `false` otherwise.

**Design Note**: This method encodes Sharpy's operator semantics. Future extensibility (e.g., user-defined operator overloads for CLR types) would modify this logic.

---

### Helper Methods: Operator Mapping

These methods map AST operator enums to their corresponding Python dunder method names and string representations:

- **`BinaryOperatorToDunder`**: Maps binary operators to dunder methods
  - `BinaryOperator.Add` → `"__add__"`
  - `BinaryOperator.Equal` → `"__eq__"`

- **`GetReflectedDunder`**: Gets the reflected version of a dunder method
  - `"__add__"` → `"__radd__"`
  - `"__mul__"` → `"__rmul__"`

- **`UnaryOperatorToDunder`**: Maps unary operators to dunder methods
  - `UnaryOperator.Minus` → `"__neg__"`
  - `UnaryOperator.BitwiseNot` → `"__invert__"`

- **`AugmentedOperatorToDunder`**: Maps augmented assignment to in-place dunder methods
  - `AssignmentOperator.PlusAssign` → `"__iadd__"`

- **`OperatorToString`** (3 overloads): Converts operators to their string representation for error messages
  - `BinaryOperator.Add` → `"+"`
  - `UnaryOperator.Minus` → `"-"`
  - `AssignmentOperator.PlusAssign` → `"+="`

---

### Type Classification Helpers

- **`IsNumericType`**: Returns `true` for `int`, `long`, `float`, `float32`, `double`
- **`IsPrimitiveType`**: Returns `true` for builtin types and `str`
- **`IsComparisonOperator`**: Returns `true` for `==`, `!=`, `<`, `<=`, `>`, `>=`

These helpers improve readability and centralize type classification logic.

---

## Dependencies

### Internal (Sharpy Codebase)

- **`Sharpy.Compiler.Parser.Ast`**: All AST node types (Expression, Statement, BinaryOp, etc.)
- **`Sharpy.Compiler.Logging`**: `ICompilerLogger`, `NullLogger` for diagnostic output
- **`SemanticContext`**: Shared context providing:
  - `SemanticInfo`: Type annotations for expressions (via `GetExpressionType`)
  - `Diagnostics`: Error/warning collection
  - `CurrentFilePath`: For error reporting
- **`SemanticValidatorBase`**: Base class providing `AddError` and `AddWarning` helpers
- **`SemanticType` hierarchy**: `BuiltinType`, `GenericType`, `UserDefinedType`, `NullableType`, `UnknownType`

### External (.NET)

- **`System`**: Basic types
- No external NuGet dependencies for this file

---

## Patterns and Design Decisions

### 1. **Post-Pass Validation Architecture**

Unlike the legacy `OperatorValidator`, this class does **not** perform type inference. It assumes all expression types are already resolved and stored in `SemanticInfo`. This separation of concerns:

- **Simplifies** the validation logic (no complex type inference algorithms here)
- **Improves** testability (can test operator validation in isolation)
- **Enables** future parallelization (validators are stateless between calls)

**Related Components**:
- Type inference is now handled by `TypeInferenceService` (see `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`)
- Type checking is performed by `TypeChecker` (Order 300, runs before this validator)

### 2. **Visitor Pattern for AST Traversal**

The validator uses a manual visitor pattern with `switch` statements on AST node types. This is a common pattern in compilers because:

- **Explicit control**: Clear traversal order and control flow
- **Performance**: No virtual dispatch overhead
- **Simplicity**: Easy to understand and debug

**Alternative**: Could use the Visitor pattern with double dispatch (`node.Accept(visitor)`), but manual pattern matching is more idiomatic in C#.

### 3. **Python Operator Resolution Semantics**

The validator implements Python's operator resolution:

1. **Forward operator**: Try `left.__op__(right)`
2. **Reflected operator**: Try `right.__rop__(left)` if forward fails
3. **In-place operators**: Try `__iop__` first, fall back to `__op__` for augmented assignment

This ensures Sharpy's operator behavior matches Python, maintaining **Axiom #3: Python Syntax Fidelity**.

### 4. **Error Suppression for Cascading Errors**

The validator skips validation when types are `null` or `UnknownType`. This prevents cascading errors:

**Example**:
```python
x = undefined_variable + 5  # Type checker reports: 'undefined_variable' not found
# OperatorValidator does NOT report: "operator + not supported"
```

**Rationale**: Reporting operator errors when the operand type is unknown creates noise and confuses users.

### 5. **Hardcoded Operator Support for Builtins**

Instead of using reflection or metadata, operator support for builtin types is **hardcoded** in `SupportsOperator`. This is intentional:

- **Performance**: No reflection overhead
- **Predictability**: Explicit, auditable rules
- **Control**: Sharpy can diverge from Python/C# semantics if needed

**Trade-off**: Adding new builtin types requires updating this method.

### 6. **Order 500 (Same as ProtocolValidator)**

This validator runs at Order 500, the same as `ProtocolValidator`. This is intentional:

- **Operators and protocols are independent**: They don't depend on each other
- **Parallel execution potential**: In the future, validators at the same order could run concurrently

**Pipeline Ordering**:
- 100: Name resolution
- 200: Type resolution
- 300: Type checking
- 400: Access validation
- **500: Protocol validation, operator validation** ← We are here
- 600+: Other post-type-checking validations

---

## Debugging Tips

### 1. **Enable Debug Logging**

The validator logs "Starting operator validation" at the debug level. Enable debug logging to see when this validator runs:

```bash
# If the compiler supports a --verbose flag
dotnet run --project src/Sharpy.Cli -- run file.spy --verbose
```

### 2. **Inspect SemanticInfo**

If you're debugging why an operator validation fails or succeeds, inspect `SemanticInfo`:

```csharp
// In ValidateBinaryOp, add a breakpoint and inspect:
var leftType = _context.SemanticInfo.GetExpressionType(binOp.Left);
var rightType = _context.SemanticInfo.GetExpressionType(binOp.Right);
```

**Common issues**:
- Type is `null` → Type checker didn't annotate this expression (possible bug in type checker)
- Type is `UnknownType` → Upstream error, check diagnostics
- Type is unexpected → Type inference bug

### 3. **Check for Upstream Errors**

This validator depends on correct type information from `TypeChecker` and `TypeInferenceService`. If you see unexpected behavior:

1. Check `context.Diagnostics` for prior errors
2. Run `dotnet run --project src/Sharpy.Cli -- emit ast file.spy` to inspect the AST
3. Run `dotnet run --project src/Sharpy.Cli -- emit csharp file.spy` to see if code generation succeeded despite validation

### 4. **Test with Minimal Examples**

Create minimal `.spy` files to isolate operator validation:

```python
# test_operator.spy
x: int = 5
y: str = "hello"
z = x + y  # Should fail validation
```

Run:
```bash
dotnet run --project src/Sharpy.Cli -- run test_operator.spy
```

### 5. **Add Breakpoints in `SupportsOperator`**

The `SupportsOperator` method is the core logic. If an operator is incorrectly accepted/rejected:

- Add a breakpoint in the relevant type case (builtin, generic, user-defined)
- Inspect the `dunderName` parameter
- Verify the type classification (numeric, primitive, etc.)

### 6. **Review Dunder Method Mapping**

If you suspect an operator is mapped to the wrong dunder method:

- Check `BinaryOperatorToDunder`, `UnaryOperatorToDunder`, `AugmentedOperatorToDunder`
- Cross-reference with Python documentation (e.g., Python 3.9 data model)

---

## Contribution Guidelines

### When to Modify This File

You should modify `OperatorValidatorV2.cs` when:

1. **Adding new operators**: If Sharpy adds new operators (e.g., matrix multiplication `@`), add cases to:
   - `BinaryOperatorToDunder` (if binary) or `UnaryOperatorToDunder` (if unary)
   - `SupportsOperator` (to define which types support it)
   - `OperatorToString` (for error messages)

2. **Adding new builtin types**: If Sharpy adds a new builtin type (e.g., `decimal`, `bigint`), update:
   - `IsNumericType` or `IsPrimitiveType` if applicable
   - `SupportsOperator` to define operator support

3. **Changing operator semantics**: If Sharpy's operator rules diverge from Python:
   - Update `SupportsOperator` to reflect the new rules
   - Add tests in `src/Sharpy.Compiler.Tests/Semantic/Validation/`

4. **Fixing bugs**: If an operator is incorrectly validated, fix the logic in:
   - `ValidateArithmeticOrComparisonOp`
   - `SupportsOperator`
   - Add a regression test

### What NOT to Change

- **Do not add type inference logic here**: Type inference belongs in `TypeInferenceService`
- **Do not modify AST nodes**: This is a read-only validator
- **Do not add state between calls**: Keep the validator stateless for future parallelization

### Testing Changes

1. **Unit tests**: Add tests to `src/Sharpy.Compiler.Tests/Semantic/Validation/OperatorValidatorV2Tests.cs`
2. **Integration tests**: Add `.spy` + `.error` test fixtures to `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
3. **Verify Python compatibility**: Test against Python to ensure Sharpy's behavior matches

**Example test fixture**:
```
TestFixtures/
  operator_validation/
    invalid_unary_on_string.spy    # y = ~"abc"
    invalid_unary_on_string.error  # Type 'str' does not support unary operator '~'
```

### Code Style

- Follow existing patterns (switch statements for AST traversal)
- Use `IsNumericType`, `IsPrimitiveType` helpers instead of inline checks
- Add XML doc comments for new public methods
- Use meaningful variable names (`leftType`, `rightType`, not `t1`, `t2`)

### Cross-File Coordination

If you modify operator semantics, you may need to update:

- **`TypeInferenceService`**: If operator result types change
- **`RoslynEmitter`**: If code generation for operators changes (e.g., dunder methods → C# operator overloads)
- **Language spec**: Update `docs/language_specification/arithmetic_operators.md` or `operator_overloading.md`

---

## Cross-References

### Related Files

- **`src/Sharpy.Compiler/Semantic/OperatorValidator.cs`**: Legacy validator (deprecated), still contains some shared utility methods
- **`src/Sharpy.Compiler/Semantic/Validation/ISemanticValidator.cs`**: Base interface and `SemanticValidatorBase` class
- **`src/Sharpy.Compiler/Semantic/Validation/ValidationPipeline.cs`**: Orchestrates all validators
- **`src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`**: Handles type inference for operator expressions
- **`src/Sharpy.Compiler/Semantic/TypeChecker.cs`**: Runs before this validator (Order 300), annotates expression types
- **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs`**: Emits C# code for Sharpy operators

### Related Tests

- **`src/Sharpy.Compiler.Tests/Semantic/Validation/OperatorValidatorV2Tests.cs`**: Unit tests (if exists)
- **`src/Sharpy.Compiler.Tests/Integration/ValidationPipelineIntegrationTests.cs`**: Integration tests for the validation pipeline
- **`src/Sharpy.Compiler.Tests/Integration/TestFixtures/operator_validation/`**: File-based integration tests

### Related Documentation

- **`docs/language_specification/arithmetic_operators.md`**: Operator precedence and semantics
- **`docs/language_specification/operator_overloading.md`**: Dunder method definitions
- **`docs/language_specification/operator_precedence.md`**: Precedence rules (affects parser, not this validator)

### Partial Classes

This file is **not** a partial class. It is a standalone validator.

---

## Summary

`OperatorValidatorV2` is a critical post-type-checking validation pass that ensures operators are used correctly in Sharpy. It:

- **Validates** binary, unary, and augmented assignment operators
- **Implements** Python's operator resolution semantics (forward, reflected, in-place)
- **Integrates** with the validation pipeline at Order 500
- **Depends on** type information from `TypeChecker` and `TypeInferenceService`

For newcomers:
1. Start by reading `Validate` → `ValidateStatement` → `ValidateExpression` to understand the traversal
2. Focus on `ValidateBinaryOp` and `SupportsOperator` for the core validation logic
3. Review the dunder method mapping functions to understand operator semantics
4. Run the CLI emit commands (`dotnet run --project src/Sharpy.Cli -- emit ast file.spy`) to see how operators are represented in the AST

This validator is a great example of **separation of concerns** in the Sharpy compiler: it does one job (validate operators) and does it well, relying on upstream components for type information.
