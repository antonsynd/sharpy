# Walkthrough: ProtocolValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/ProtocolValidator.cs`

---

## Overview

`ProtocolValidator` is a **semantic validator** responsible for ensuring that Python protocol methods (dunder methods like `__iter__`, `__contains__`, `__getitem__`, etc.) are properly implemented when used in Sharpy code. This validator runs as part of the **validation pipeline** during semantic analysis, after type checking has been completed.

**Key Responsibilities:**
- Validates iteration protocols (`__iter__`) for `for` loops and comprehensions
- Validates membership protocols (`__contains__`) for `in`/`not in` operators
- Validates indexing protocols (`__getitem__`/`__setitem__`) for subscript access (`obj[index]`)
- Validates len protocol (`__len__`) for `len()` function calls

**Pipeline Position**: This is a **post-pass validator** (Order: 500) that runs after type checking and access validation. It doesn't perform type inference—it only validates that required protocols exist.

This validator is designed for the pipeline architecture and focuses solely on validation, with type inference handled separately.

---

## Class/Type Structure

### ProtocolValidator : SemanticValidatorBase

```csharp
public class ProtocolValidator : SemanticValidatorBase
{
    public override string Name => "ProtocolValidator";
    public override int Order => 500;  // After access validation (450)

    private ICompilerLogger _logger;
    private SemanticContext _context;
}
```

**Inheritance**: Extends `SemanticValidatorBase`, making it part of the validation pipeline framework.

**Key Properties:**
- `Name`: Identifies this validator in logs and diagnostics
- `Order`: Determines execution order (500 = after access validation at 450)

**Private State:**
- `_logger`: For debug logging and diagnostics
- `_context`: Provides access to semantic information (types, symbols, error reporting)

---

## Key Functions/Methods

### 1. Validate (Entry Point)

```csharp
public override void Validate(Module module, SemanticContext context)
```

**Purpose**: Main entry point called by the validation pipeline.

**What It Does:**
1. Stores the `SemanticContext` for use throughout validation
2. Initializes the logger
3. Iterates through all top-level statements in the module
4. Delegates to `ValidateStatement()` for each statement

**Pipeline Connection**: Called by the `ValidationPipeline` after type checking is complete, ensuring type information is available via `context.SemanticInfo`.

---

### 2. ValidateStatement (AST Traversal)

```csharp
private void ValidateStatement(Statement stmt)
```

**Purpose**: Recursively traverses the AST to find protocol usage sites.

**Strategy**: Uses pattern matching on statement types to:
- Recurse into function/class/struct bodies
- Recurse into control flow structures (if/while/try)
- Special handling for `ForStatement` → calls `ValidateIteration()`
- Delegate expressions to `ValidateExpression()`

**Key Cases:**
- `ForStatement`: Validates the iterator has `__iter__` protocol
- `Assignment`: Validates both target and value expressions
- `VariableDeclaration`: Validates initializer expression
- Container definitions: Recurses into body statements

**Design Pattern**: **Visitor-like traversal** without formal visitor pattern—uses switch expressions for cleaner syntax.

---

### 3. ValidateIteration (For Loop Validation)

```csharp
private void ValidateIteration(ForStatement forStmt)
```

**Purpose**: Ensures the iterator expression in a `for` loop supports the iteration protocol.

**Algorithm:**
1. Retrieve the type of `forStmt.Iterator` from `SemanticInfo`
2. Skip validation if type is unknown (to avoid cascading errors)
3. Check if type has `__iter__` protocol using `HasProtocol()`
4. If missing, report a helpful error suggesting `IIterable<T>` interface
5. Recursively validate the iterator expression itself

**Error Message Example:**
```
Type 'MyClass' is not iterable (missing '__iter__' method).
Consider implementing IIterable<T> interface.
```

**Why It's Separate**: For loops are special because they're statements that directly require protocol support, unlike expressions where protocol usage is detected via operators.

---

### 4. ValidateExpression (Expression Traversal)

```csharp
private void ValidateExpression(Expression expr)
```

**Purpose**: Recursively traverses expression trees to find protocol usage.

**Key Protocol Validations:**
- `IndexAccess` → `ValidateIndexAccess()` (checks `__getitem__`)
- `BinaryOp` with `In`/`NotIn` → `ValidateMembership()` (checks `__contains__`)
- `FunctionCall` → `ValidateFunctionCall()` (checks `len()` for `__len__`)
- Comprehensions → `ValidateComprehension()` (checks `__iter__`)

**Recursive Nature**: For composite expressions (BinaryOp, FunctionCall, etc.), it validates the specific protocol then recurses into child expressions.

**Example Flow**:
```python
# for x in my_list[0]:
#    ^^     ^^^^^^^^
#    |      ValidateIndexAccess(__getitem__)
#    ValidateIteration(__iter__)
```

---

### 5. ValidateIndexAccess (Subscript Protocol)

```csharp
private void ValidateIndexAccess(IndexAccess indexAccess)
```

**Purpose**: Validates subscript operations (`obj[index]`) require `__getitem__`.

**What It Checks:**
1. Gets the type of the object being indexed
2. Verifies it has the `__getitem__` protocol
3. Suggests implementing `ISequence<T>` if missing

**Note**: This validator only checks `__getitem__`. The `__setitem__` protocol would be validated separately when the index access appears on the left side of an assignment (handled elsewhere in the pipeline).

---

### 6. ValidateMembership (In/Not In Operators)

```csharp
private void ValidateMembership(BinaryOp binOp)
```

**Purpose**: Validates `in` and `not in` operators require `__contains__` on the container.

**Important Detail**: It checks the **right operand** (the container), not the left (the item being searched for).

```python
if x in my_container:  # Validates my_container has __contains__
    #   ^^^^^^^^^^^^
```

**Suggested Fix**: Error message recommends implementing `IContainer<T>` interface.

---

### 7. ValidateFunctionCall (Special Function Protocols)

```csharp
private void ValidateFunctionCall(FunctionCall call)
```

**Purpose**: Validates special built-in functions that require protocols.

**Currently Handles**: Only `len()` function (requires `__len__` protocol)

**Detection Logic**:
1. Check if function is an `Identifier` named "len"
2. Check if it has exactly 1 argument
3. Validate that argument's type has `__len__` protocol

**Extensibility**: This method could be extended to handle other protocol-requiring functions like `iter()`, `next()`, `str()`, etc.

---

### 8. ValidateComprehension (List/Set/Dict Comprehensions)

```csharp
private void ValidateComprehension(Expression element, IReadOnlyList<ComprehensionClause> clauses)
```

**Purpose**: Validates iteration protocols in comprehensions.

**What It Validates:**
- `ForClause`: Calls `ValidateIteratorExpression()` to check `__iter__`
- `IfClause`: Validates the filter condition expression
- Element expression: The computed value in the comprehension

**Example**:
```python
# [x * 2 for x in numbers if x > 0]
#  ^^^^^     ^^^^^^^        ^^^^^
#  element   iterator       condition
```

---

### 9. HasProtocol (Core Protocol Detection)

```csharp
private bool HasProtocol(SemanticType type, string dunderName)
```

**Purpose**: Central method that determines if a type supports a specific protocol.

**Multi-Layered Checking Strategy:**

1. **Built-in Sharpy types** (hardcoded knowledge):
   - `SemanticType.Str` → has `__len__`, `__iter__`, `__contains__`, `__getitem__`
   - `TupleType` → has `__len__`, `__iter__`, `__getitem__`

2. **Generic container types**:
   ```csharp
   "list" => __len__ | __iter__ | __contains__ | __getitem__ | __setitem__
   "dict" => __len__ | __iter__ | __contains__ | __getitem__ | __setitem__
   "set"  => __len__ | __iter__ | __contains__
   ```

3. **User-defined types** (`UserDefinedType`):
   - Check symbol's `ProtocolMethods` dictionary
   - Check symbol's `Methods` list for dunder method names
   - Fall back to CLR type reflection via `HasClrProtocol()`

4. **Builtin types with CLR backing**:
   - Use `HasClrProtocol()` to check .NET interfaces

5. **Universal protocols**: All types have `__str__` and `__hash__`

**Design Decision**: Hardcoded knowledge of Sharpy built-ins is acceptable because these are core language types with well-defined protocols.

---

### 10. HasClrProtocol (CLR Interop)

```csharp
private bool HasClrProtocol(System.Type clrType, string dunderName)
```

**Purpose**: Maps Python protocols to .NET interface checks for CLR interop.

**Protocol → CLR Mapping:**

| Python Protocol | .NET Interface/Type |
|----------------|---------------------|
| `__iter__` | `IEnumerable`, `IEnumerable<T>`, `Sharpy.Core.Iterator<T>`, `IIterable<T>` |
| `__len__` / `__contains__` | `ICollection` |
| `__getitem__` / `__setitem__` | `IList`, `IDictionary` |

**Special Handling for `__iter__`**:
- Checks base class hierarchy for `Sharpy.Core.Iterator<T>` (generic type definition matching)
- Checks interfaces for `Sharpy.Core.Collections.Interfaces.IIterable<T>`

**Why This Matters**: Allows Sharpy code to use .NET collection types seamlessly:
```python
# .NET List<int> works in for loops because it implements IEnumerable
for x in dotnet_list:
    print(x)
```

---

## Dependencies

### Internal Sharpy Dependencies

**AST Types** (`Sharpy.Compiler.Parser.Ast`):
- Statement types: `ForStatement`, `FunctionDef`, `ClassDef`, etc.
- Expression types: `IndexAccess`, `BinaryOp`, `FunctionCall`, etc.
- Comprehension types: `ForClause`, `IfClause`, `ListComprehension`, etc.

**Semantic Types**:
- `SemanticType`, `UserDefinedType`, `GenericType`, `TupleType`, `BuiltinType`, `UnknownType`
- `SemanticContext`, `SemanticInfo`

**Logging** (`Sharpy.Compiler.Logging`):
- `ICompilerLogger`, `NullLogger`

### External .NET Dependencies

**System.Collections**:
- `IEnumerable`, `ICollection`, `IList`, `IDictionary`

**Sharpy.Core Runtime** (via reflection):
- `Sharpy.Core.Iterator<T>`
- `Sharpy.Core.Collections.Interfaces.IIterable<T>`

---

## Patterns and Design Decisions

### 1. **Pipeline Architecture**

This validator follows the **pipeline pattern** used throughout the Sharpy compiler:
- Implements `SemanticValidatorBase` contract
- Declares an `Order` property for sequencing
- Receives `SemanticContext` with all upstream analysis results
- Reports errors via `AddError()` helper (inherited from base class)

**Why This Matters**: The pipeline ensures validators run in the correct order. Protocol validation must happen *after* type checking (which runs earlier) so that type information is available.

---

### 2. **Fail-Fast on Unknown Types**

```csharp
if (iterableType == null || iterableType is UnknownType)
    return;  // Skip validation
```

**Rationale**: If type checking failed earlier, type information might be missing. Rather than reporting cascading errors ("Type 'Unknown' is not iterable"), the validator silently skips validation. The user already got a type error from the type checker.

**Trade-off**: This means some protocol errors might go unreported if there are type errors, but it prevents error spam.

---

### 3. **Separation from Type Inference**

The comment in the class summary explains an important architectural decision:

```csharp
/// This is the pipeline-compatible version of ProtocolValidator.
/// Unlike the legacy version which provides type inference during type-checking,
/// this validator performs post-pass validation only.
```

**Historical Context**: The original protocol validator was entangled with type inference logic. The current design cleanly separates concerns:
- **Type inference** happens in the type checker
- **Protocol validation** happens in this post-pass validator

**Benefit**: Simpler code, easier to maintain, better separation of concerns.

---

### 4. **Hardcoded Protocol Knowledge**

The validator has explicit knowledge of Sharpy's built-in types and their protocols (see `HasProtocol()`). This is **intentional**:
- Sharpy's core types (`str`, `list`, `dict`, etc.) have stable, well-defined protocols
- Hardcoding avoids expensive reflection for common cases
- Provides fast-path checks before falling back to reflection

**Alternative Considered**: Use metadata attributes or a registry. Rejected because it's over-engineered for a small set of well-known types.

---

### 5. **Helpful Error Messages**

Every error message includes:
1. What's wrong ("Type 'X' is not iterable")
2. Why ("missing '__iter__' method")
3. How to fix ("Consider implementing IIterable<T> interface")

**Example**:
```
Type 'MyClass' does not support indexing (missing '__getitem__' method).
Consider implementing ISequence<T> interface.
```

This guides users toward the right solution without requiring deep knowledge of protocols.

---

### 6. **Recursive Traversal**

Both `ValidateStatement()` and `ValidateExpression()` use recursive descent to traverse the entire AST. This is a **standard compiler pattern**:
- Simple to implement
- Naturally matches the tree structure
-易于理解和维护

**Stack Depth**: Not a concern because Sharpy code doesn't create deeply nested ASTs that would overflow the call stack.

---

## Debugging Tips

### 1. **Enable Debug Logging**

The validator logs its start:
```csharp
_logger.LogDebug("Starting protocol validation");
```

To see this output, configure your `ICompilerLogger` implementation to output debug logs. This helps trace when protocol validation runs in the pipeline.

---

### 2. **Check Validation Order**

If protocol errors aren't being reported, verify:
1. Type checking ran successfully (check for type errors first)
2. Protocol validator is registered in the pipeline
3. Protocol validator's `Order` is correct (should be > type checker's order)

Use the `ValidationPipeline` class to inspect registered validators and their order.

---

### 3. **Inspect SemanticInfo**

The validator relies on `_context.SemanticInfo.GetExpressionType()`. If validation is incorrect:
1. Check what type is being returned for the expression
2. Verify the type checker set it correctly
3. Use a debugger to step through `HasProtocol()` logic

**Common Issue**: Type inference might have inferred a too-generic type (e.g., `object` instead of `List<int>`), causing protocol checks to fail.

---

### 4. **Test with emit ast**

Use the CLI to inspect the AST:
```bash
dotnet run --project src/Sharpy.Cli -- emit ast file.spy
```

This shows the exact AST structure the validator will traverse. Look for:
- Statement types (ForStatement, etc.)
- Expression types (IndexAccess, BinaryOp, etc.)
- Comprehension clauses

---

### 5. **Test CLR Interop Separately**

If CLR protocol detection is failing, write a minimal test:
```csharp
[Fact]
public void TestClrProtocol()
{
    var validator = new ProtocolValidator();
    var listType = typeof(List<int>);
    Assert.True(validator.HasClrProtocol(listType, "__iter__"));
}
```

This isolates the CLR reflection logic from the full validation pipeline.

---

### 6. **Check for UnknownType**

If validation is mysteriously skipped, add a conditional breakpoint:
```csharp
if (iterableType is UnknownType)
    return;  // <-- Breakpoint here
```

This catches cases where type checking failed and validation is being skipped.

---

## Contribution Guidelines

### What Changes Might Be Made

**1. Adding New Protocol Support**

To validate a new protocol (e.g., `__call__` for callable objects):

1. Add a new validation method:
   ```csharp
   private void ValidateCallable(FunctionCall call) { ... }
   ```

2. Call it from `ValidateExpression()`:
   ```csharp
   case FunctionCall call when IsDirectCall(call):
       ValidateCallable(call);
       break;
   ```

3. Update `HasProtocol()` to recognize the new dunder method

4. Update `HasClrProtocol()` if there's a corresponding .NET interface

---

**2. Improving Error Messages**

Error messages can always be improved. Consider:
- Adding code examples to error messages
- Suggesting specific fixes based on the type
- Linking to documentation

Example enhancement:
```csharp
AddError(_context,
    $"Type '{type.GetDisplayName()}' is not iterable.\n" +
    $"Hint: Add 'def __iter__(self) -> Iterator[T]:' method or implement IIterable<T>.",
    location);
```

---

**3. Performance Optimization**

For large codebases, consider:
- Caching `HasProtocol()` results (types don't change during validation)
- Early-exit strategies (skip obviously valid types)
- Parallel validation of independent modules (requires thread-safety)

---

**4. Better CLR Interop**

To handle more .NET types:
- Add checks for more .NET interfaces (IReadOnlyCollection, ISet, etc.)
- Support custom protocol mappings via attributes
- Handle generic type constraints better

---

**5. Integration with Legacy ProtocolValidator**

Currently, both `ProtocolValidator` (legacy) and `ProtocolValidator` exist. Future work:
- Migrate all type inference out of legacy validator
- Deprecate and remove legacy validator
- Ensure 100% feature parity

**When doing this**: Carefully review test coverage to ensure no regressions.

---

### Testing Strategy

When modifying this file:

1. **Unit tests**: Test `HasProtocol()` and `HasClrProtocol()` directly
2. **Integration tests**: Write `.spy` + `.error` file pairs in `TestFixtures/`
3. **Regression tests**: Ensure existing protocol errors still fire
4. **Edge cases**: Test with `UnknownType`, generic types, CLR types

Example test structure:
```
TestFixtures/Validation/Protocols/
  missing_iter.spy          # for loop on non-iterable
  missing_iter.error        # Expected error message
  missing_getitem.spy       # indexing non-indexable
  missing_getitem.error
  clr_list_iteration.spy    # Should NOT error (List<T> is iterable)
```

---

### Code Style Guidelines

**Follow existing patterns**:
- Use pattern matching with `switch` expressions
- Recurse into child nodes after specific validation
- Check for `UnknownType` before validating
- Use descriptive error messages with suggestions

**Don't**:
- Perform type inference in this validator (that's the type checker's job)
- Mutate the AST (it's immutable)
- Store mutable state between `Validate()` calls (validators may be reused)

---

## Cross-References

### Related Validation Pipeline Files

- **SemanticValidatorBase**: Base class for all validators (defines `AddError()`, `Validate()` contract)
- **ValidationPipeline**: Orchestrates validator execution in order
- **AccessValidator**: Runs before this validator (Order: 450)

### Related Semantic Analysis Files

- **TypeChecker**: Performs type inference and checking before validation
- **SemanticInfo**: Stores type annotations that this validator reads
- **SemanticContext**: Provides access to symbols, types, and error reporting

### Related AST Files

- **Sharpy.Compiler.Parser.Ast.Statement**: All statement types
- **Sharpy.Compiler.Parser.Ast.Expression**: All expression types
- **Sharpy.Compiler.Parser.Ast.ComprehensionClause**: Comprehension syntax

### Legacy Protocol Validation

- **ProtocolValidator** (legacy): The original protocol validator that combined validation with type inference has been superseded by this pipeline-based version.

### Runtime Protocol Implementation

- **Sharpy.Core.Collections.Interfaces.IIterable\<T\>**: The recommended interface for iteration protocol
- **Sharpy.Core.Iterator\<T\>**: Base class for custom iterators
- **Sharpy.Core runtime exports**: Check `Partial.List/`, `Partial.Dict/`, etc. to see how built-in types implement protocols

---

## Summary

`ProtocolValidator` is a **focused, post-pass validator** that ensures Python protocols are properly implemented wherever they're used in Sharpy code. It's part of the modern validation pipeline architecture, separating protocol validation from type inference.

**Key Takeaways:**
- Runs **after type checking** (Order: 500)
- Validates **four main protocols**: iteration, membership, indexing, len
- Uses **type information** from SemanticInfo (doesn't infer types itself)
- Handles **CLR interop** by mapping Python protocols to .NET interfaces
- Provides **helpful error messages** with suggested fixes
- **Recursively traverses** statements and expressions to find protocol usage sites

When working with this file, remember: it's a validator, not a type inferencer. Its job is to check that required protocols exist, not to figure out what types things are.
