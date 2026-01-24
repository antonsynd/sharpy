# Walkthrough: SignatureValidatorV2.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/SignatureValidatorV2.cs`

---

## Overview

The `SignatureValidatorV2` is a semantic validator that enforces correct method signatures for special "dunder" methods (double-underscore methods like `__add__`, `__len__`, etc.) in Sharpy classes and structs. It consolidates the logic previously split between `OperatorSignatureValidator` and `ProtocolSignatureValidator` into a unified validator.

**Pipeline Position**: Runs early in the semantic analysis phase (Order 150) to catch signature errors **before** type checking (Order 300) attempts to use these methods.

**Key Responsibilities**:
- Validates parameter counts for operator dunders (`__add__`, `__mul__`, etc.)
- Validates return types for operator dunders (e.g., comparison operators must return `bool`)
- Validates parameter counts for protocol dunders (`__len__`, `__init__`, etc.)
- Validates return types for protocol dunders
- Ensures the first parameter is always named `self`

**Why It Matters**: Dunder methods form the foundation of Python-style operator overloading and protocol implementations. Incorrect signatures would cause confusing errors later in code generation or at runtime.

---

## Class/Type Structure

### SignatureValidatorV2 : SemanticValidatorBase

The main validator class inheriting from `SemanticValidatorBase`, which provides the `ISemanticValidator` interface.

**Properties**:
```csharp
public override string Name => "SignatureValidator";
public override int Order => 150;  // Before type checking (300)

private ICompilerLogger _logger = NullLogger.Instance;
private SemanticContext _context = null!;
```

**Static Data**: The validator maintains several `HashSet<string>` collections categorizing different operator types:

- `BinaryArithmeticOps`: `__add__`, `__sub__`, `__mul__`, `__truediv__`, `__floordiv__`, `__mod__`, `__pow__`
- `BinaryBitwiseOps`: `__and__`, `__or__`, `__xor__`, `__lshift__`, `__rshift__`
- `InPlaceOps`: `__iadd__`, `__isub__`, `__imul__`, etc. (in-place variants)
- `ComparisonOps`: `__eq__`, `__ne__`, `__lt__`, `__le__`, `__gt__`, `__ge__`
- `UnaryOps`: `__pos__`, `__neg__`, `__invert__`

These sets enable efficient lookup to determine what kind of validation to apply.

---

## Key Functions/Methods

### 1. Entry Point: `Validate(Module, SemanticContext)`

```csharp
public override void Validate(Module module, SemanticContext context)
```

**Purpose**: Main entry point called by the `ValidationPipeline`.

**Flow**:
1. Stores the `SemanticContext` and logger in instance fields
2. Iterates over all top-level statements in the module
3. Delegates to `ValidateTopLevelStatement()` for each statement

**Why instance fields?**: Instead of passing `context` through every method call, storing it simplifies the internal API. These fields are reset on each `Validate()` call, making the validator effectively stateless between invocations.

---

### 2. Top-Level Dispatch: `ValidateTopLevelStatement(Statement)`

```csharp
private void ValidateTopLevelStatement(Statement stmt)
```

**Purpose**: Routes to the appropriate validation method based on statement type.

**Logic**:
- `ClassDef` → `ValidateClassSignatures()`
- `StructDef` → `ValidateStructSignatures()`
- Other statements → Ignored (no validation needed)

**Design Note**: The validator only cares about type definitions (classes and structs) because dunder methods only make sense as members of types.

---

### 3. Class/Struct Validation: `ValidateClassSignatures()` / `ValidateStructSignatures()`

```csharp
private void ValidateClassSignatures(ClassDef classDef)
private void ValidateStructSignatures(StructDef structDef)
```

**Purpose**: Extracts the type symbol and validates each method member.

**Flow**:
1. Lookup the `TypeSymbol` from the symbol table using the class/struct name
2. If not found, log a debug message and return (likely a compiler bug if this happens)
3. Iterate through all members in the body
4. For each `FunctionDef` member, call `ValidateMethodSignature()`

**Why lookup the symbol?**: The validator needs the `TypeSymbol` for error messages and to pass context to validation methods. The symbol should always exist at this point because name resolution (Order 100) runs before signature validation (Order 150).

---

### 4. Method Dispatch: `ValidateMethodSignature(FunctionDef, TypeSymbol)`

```csharp
private void ValidateMethodSignature(FunctionDef funcDef, TypeSymbol owningType)
```

**Purpose**: Determines whether a method is an operator or protocol dunder and dispatches accordingly.

**Logic**:
1. Check if the method name is an operator dunder (via `OperatorSignatureValidator.IsOperatorDunder()`)
   - If yes → `ValidateOperatorSignature()`
2. Otherwise, check if it's a protocol dunder (via `ProtocolSignatureValidator.IsProtocolDunder()`)
   - If yes → `ValidateProtocolSignature()`
3. Regular methods (non-dunders) are ignored

**Design Choice**: This validator only cares about special methods. Regular methods have different validation requirements handled elsewhere.

---

## Operator Signature Validation

### 5. `ValidateOperatorSignature(FunctionDef, TypeSymbol)`

```csharp
private void ValidateOperatorSignature(FunctionDef funcDef, TypeSymbol owningType)
```

**Purpose**: Ensures operator dunders have the correct parameter count.

**Parameter Count Rules**:
- **Unary operators** (`__pos__`, `__neg__`, `__invert__`): Exactly 1 parameter (`self`)
- **Binary operators** (arithmetic, bitwise, in-place, comparison): Exactly 2 parameters (`self`, `other`)

**Error Reporting**: Provides specific, helpful error messages:
```
"Unary operator method '__neg__' on 'Vector' must have exactly 1 parameter (self), got 2"
"Binary operator method '__add__' on 'Vector' must have exactly 2 parameters (self, other), got 3"
```

**Return Type Validation**: If the method has a return type annotation, delegates to `ValidateOperatorReturnType()`.

---

### 6. `ValidateOperatorReturnType(FunctionDef, string, string)`

```csharp
private void ValidateOperatorReturnType(FunctionDef funcDef, string methodName, string owningTypeName)
```

**Purpose**: Enforces return type constraints for operators.

**Rules**:
1. **Comparison operators** (`__eq__`, `__lt__`, etc.): Must return `bool`
2. **All other operators**: Must return a **non-void** type (arithmetic, bitwise, unary, in-place)

**Why these rules?**:
- Comparison operators map to C# comparison operators (`==`, `<`, etc.) which must return `bool`
- Other operators must return a value (e.g., `a + b` must produce a result)

**Implementation Detail**: Uses helper methods `IsTypeAnnotationBool()` and `IsTypeAnnotationVoid()` to check the type annotation AST node.

---

## Protocol Signature Validation

### 7. `ValidateProtocolSignature(FunctionDef, TypeSymbol)`

```csharp
private void ValidateProtocolSignature(FunctionDef funcDef, TypeSymbol owningType)
```

**Purpose**: Orchestrates validation for protocol dunders using the `ProtocolRegistry`.

**Flow**:
1. Look up the `ProtocolInfo` from the registry
2. If not found, return (not a known protocol)
3. Validate parameter count
4. Validate return type
5. Validate that the first parameter is named `self`

**Why separate validations?**: Each aspect (param count, return type, self parameter) can fail independently, and we want to report all errors, not just the first one.

---

### 8. `ValidateProtocolParameterCount(FunctionDef, ProtocolInfo, TypeSymbol)`

```csharp
private void ValidateProtocolParameterCount(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
```

**Purpose**: Ensures protocol methods have the expected number of parameters.

**Special Case**: `ExpectedParamCount == -1` means "any count" (used for `__init__`, which can have variable parameters).

**Contextual Error Messages**: The validator provides parameter descriptions tailored to each protocol:
```csharp
var paramDescription = (expectedCount, protocol.DunderName) switch
{
    (1, _) => "(self)",
    (2, "__contains__") => "(self, item)",
    (2, "__getitem__" or "__delitem__") => "(self, index)",
    (2, _) => "(self, other)",
    (3, "__setitem__") => "(self, index, value)",
    (3, _) => "(self, key, value)",
    _ => $"({expectedCount} parameters)"
};
```

**Interface Hints**: If the protocol has a corresponding Sharpy.Core interface (e.g., `ISized` for `__len__`), the error message includes a helpful hint:
```
"See interface 'ISized' for expected signature."
```

---

### 9. `ValidateProtocolReturnType(FunctionDef, ProtocolInfo, TypeSymbol)`

```csharp
private void ValidateProtocolReturnType(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
```

**Purpose**: Ensures protocol methods return the expected type.

**Normalization**: The validator normalizes `"None"` and `"void"` to be equivalent, since Python uses `None` but C# uses `void`.

**Skip Conditions**:
- No expected return type in the protocol definition
- No return type annotation on the method (we only validate when annotations are present)

**Case-Insensitive Matching**: Uses `StringComparison.OrdinalIgnoreCase` to handle `int` vs `Int32`, etc.

---

### 10. `ValidateProtocolSelfParameter(FunctionDef, ProtocolInfo, TypeSymbol)`

```csharp
private void ValidateProtocolSelfParameter(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
```

**Purpose**: Ensures the first parameter is named `self`.

**Edge Cases**:
- If there are **no parameters** and the protocol expects variable params (`__init__`), report an error about missing `self`
- If the first parameter exists but isn't named `self`, report an error

**Why enforce `self`?**: Python convention requires `self` as the instance parameter. Sharpy follows this convention to maintain Python familiarity and to enable clear code generation to C# instance methods.

---

## Helper Methods

### 11. Type Annotation Helpers

```csharp
private static bool IsTypeAnnotationBool(TypeAnnotation typeAnnotation)
private static bool IsTypeAnnotationVoid(TypeAnnotation typeAnnotation)
```

**Purpose**: Check if a type annotation represents `bool` or `void`/`None`.

**Implementation**:
- `IsTypeAnnotationBool`: Checks `Name == "bool"`, no type arguments, and not nullable
- `IsTypeAnnotationVoid`: Checks `Name == "None"` and no type arguments

**Why not just string comparison?**: The `TypeAnnotation` AST node has multiple fields (`Name`, `TypeArguments`, `IsNullable`). These helpers ensure we check all relevant fields.

---

## Dependencies

### Internal Sharpy Dependencies

1. **`SemanticValidatorBase`** (`ISemanticValidator.cs`): Base class providing:
   - `AddError()` convenience method
   - `AddWarning()` convenience method
   - Interface implementation structure

2. **`ProtocolRegistry`** (`Sharpy.Compiler.Semantic/ProtocolRegistry.cs`): Provides:
   - `GetProtocol(string methodName)`: Looks up protocol info by dunder name
   - `ProtocolInfo` records with expected signatures

3. **`OperatorSignatureValidator` / `ProtocolSignatureValidator`**: Legacy validators that provide:
   - `IsOperatorDunder(string name)`: Static method to check if a name is an operator
   - `IsProtocolDunder(string name)`: Static method to check if a name is a protocol

4. **`TypeAnnotationHelper`** (`Sharpy.Compiler.Semantic/TypeAnnotationHelper.cs`): Provides:
   - `GetName(TypeAnnotation)`: Extracts the type name from an annotation

5. **AST Types** (`Sharpy.Compiler.Parser.Ast`):
   - `Module`, `Statement`, `ClassDef`, `StructDef`, `FunctionDef`, `TypeAnnotation`

6. **Semantic Infrastructure**:
   - `SemanticContext`: Provides `SymbolTable`, `Diagnostics`, `Logger`, `CurrentFilePath`
   - `TypeSymbol`: Represents a type in the symbol table

### External Dependencies

- `Sharpy.Compiler.Logging.ICompilerLogger`: Logging infrastructure
- Standard .NET collections (`HashSet<T>`)

---

## Patterns and Design Decisions

### 1. Consolidation Pattern

**What**: This validator consolidates operator and protocol signature validation into a single class.

**Why**:
- Reduces code duplication (both validators have similar structure)
- Simplifies the pipeline (one less validator to register)
- Makes it easier to maintain consistent error messages

**Trade-off**: The class is larger and has two distinct responsibilities. However, both relate to "dunder method signatures," so the coupling is acceptable.

---

### 2. Early Validation (Order 150)

**Why run before type checking?**:
- Type checking (Order 300) may attempt to **use** these methods when resolving operators or protocols
- If the signatures are wrong, type checking will produce confusing cascading errors
- Early validation provides **clear, specific** error messages at the source

**Example**: If `__add__` has the wrong signature, type checking might report "cannot resolve operator +" instead of the clearer "operator method '__add__' must have exactly 2 parameters".

---

### 3. Static HashSets for Operator Categories

**Why not a registry pattern like protocols?**:
- Operators are simpler: they only need parameter count and return type validation
- Protocols need richer metadata (interface names, method names, CLR mappings)
- Static HashSets are sufficient and more efficient for simple lookup

**Performance**: HashSet lookups are O(1), making operator categorization very fast.

---

### 4. Contextual Error Messages

Notice the switch expression in `ValidateProtocolParameterCount`:
```csharp
var paramDescription = (expectedCount, protocol.DunderName) switch { ... }
```

**Why**: Generic error messages like "expected 2 parameters" are less helpful than "expected (self, item)" for `__contains__`. The validator provides **domain-specific** guidance.

**User Experience**: This pattern significantly improves the developer experience when fixing signature errors.

---

### 5. Null-Forgiving Operator (`_context = null!`)

```csharp
private SemanticContext _context = null!;
```

**Why**: The field is initialized in `Validate()`, which is always called before any validation methods. The `null!` tells the C# compiler "trust me, this won't be null at runtime."

**Alternative**: Could use nullable reference type (`SemanticContext?`) but would require null checks everywhere.

---

## Debugging Tips

### 1. Enable Debug Logging

Set up the logger to see validation flow:
```csharp
_logger.LogDebug("Starting signature validation");
_logger.LogDebug($"Type symbol not found for class: {classDef.Name}");
```

If a class's methods aren't being validated, check if the type symbol lookup is failing.

---

### 2. Missing Type Symbol

If you see "Type symbol not found for class: X", it means:
- Name resolution (Order 100) didn't register the type
- There's a bug in the symbol table population
- The class name might be misspelled in the AST

**Fix**: Check the `SymbolTable` after name resolution runs.

---

### 3. Validator Not Running

If signature errors aren't being caught:
- Check that `SignatureValidatorV2` is registered in the `ValidationPipeline`
- Verify the `Order` is correct (should be 150)
- Check that `ValidationPipeline` is being invoked by the compiler

**Verification**:
```bash
dotnet run --project src/Sharpy.Cli -- emit ast test.spy
```
Look for validator execution in the logs.

---

### 4. Wrong Error Message

If the error message is generic or unhelpful:
- Check the `switch` expression in `ValidateProtocolParameterCount()`
- Ensure the dunder name is handled correctly
- Verify the `ProtocolInfo` has the expected metadata

---

### 5. False Positives

If valid signatures are being flagged as errors:
- Check the operator/protocol categorization (is the method in the right HashSet?)
- Verify the `ProtocolRegistry` has the correct expected counts
- For return types, check `IsTypeAnnotationBool()` and `IsTypeAnnotationVoid()` logic

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made?

#### 1. Adding New Operators

If Sharpy adds a new operator (e.g., `__matmul__` for matrix multiplication):

1. Add the dunder name to the appropriate `HashSet` (e.g., `BinaryArithmeticOps`)
2. No other changes needed (the validation logic is generic)

**Example**:
```csharp
private static readonly HashSet<string> BinaryArithmeticOps = new()
{
    "__add__", "__sub__", "__mul__", "__truediv__", "__floordiv__",
    "__mod__", "__pow__", "__matmul__"  // NEW
};
```

---

#### 2. Adding New Protocols

If Sharpy adds a new protocol (e.g., `__enter__` and `__exit__` for context managers):

1. Register the protocol in `ProtocolRegistry` (not this file)
2. No changes needed in `SignatureValidatorV2` (it delegates to the registry)

**Why separation?**: The registry is the single source of truth for protocol metadata.

---

#### 3. Enhancing Error Messages

To improve error messages:

1. Update the `switch` expression in `ValidateProtocolParameterCount()`
2. Add more specific parameter descriptions for new protocols
3. Consider adding code examples or links to documentation

**Guidelines**:
- Keep messages concise (one line if possible)
- Use concrete parameter names (`item`, `index`, `value`) instead of generic (`arg1`, `arg2`)
- Include the interface hint if available

---

#### 4. Relaxing Constraints

If Sharpy decides to allow more flexible signatures (e.g., optional parameters on dunders):

1. Modify the parameter count validation logic
2. Update tests to reflect the new rules
3. Document the change in the language specification

**Caution**: Relaxing constraints can break compatibility with Python semantics. Consult the language specification first.

---

#### 5. Adding Return Type Inference

Currently, the validator only checks **explicit** return type annotations. A future enhancement might:

1. Infer return types from the method body
2. Validate the inferred type matches the protocol expectation

**Complexity**: This would require integration with the type checker, which runs later. Consider moving this logic to a separate validator at Order 350 (after type checking).

---

#### 6. Validation for Reflected Operators

Python supports "reflected" operators like `__radd__` for `other + self`. To add validation:

1. Define a new `HashSet<string>` for reflected operators
2. Add validation logic similar to binary operators
3. Ensure the parameter order is checked correctly (`__radd__(self, other)` vs `__add__(self, other)`)

---

### Testing New Changes

When modifying this validator:

1. **Add unit tests** in `Sharpy.Compiler.Tests/Semantic/Validation/SignatureValidatorV2Tests.cs`
2. **Add integration tests** using file-based fixtures in `TestFixtures/`
   - Create `.spy` files with invalid signatures
   - Create `.error` files with expected error messages
3. **Run existing tests** to ensure no regressions:
   ```bash
   dotnet test --filter "FullyQualifiedName~SignatureValidator"
   ```

---

### Code Style

Follow existing patterns:
- Use region directives (`#region Operator Signature Validation`) for logical grouping
- Keep methods focused (single responsibility)
- Use descriptive parameter names
- Add XML doc comments for public/protected methods
- Use static readonly collections for constant data

---

## Cross-References

### Related Validation Components

- **[ISemanticValidator.md](./ISemanticValidator.md)**: Base interface and `SemanticValidatorBase` class
- **[OperatorValidatorV2.md](./OperatorValidatorV2.md)**: Validates operator usage in expressions (downstream)
- **[ProtocolValidatorV2.md](./ProtocolValidatorV2.md)**: Validates protocol implementations (downstream)

### Related Semantic Components

- **[ProtocolRegistry.md](../ProtocolRegistry.md)**: Central registry of protocol metadata
- **[TypeAnnotationHelper.md](../TypeAnnotationHelper.md)**: Helper for extracting type names from annotations

### Pipeline Context

- **Upstream**: Parser produces AST with `ClassDef`, `StructDef`, `FunctionDef` nodes
- **Parallel**: Name resolution (Order 100) populates the `SymbolTable`
- **Downstream**: Type checker (Order 300) uses validated method signatures

### Testing

- **Unit Tests**: `src/Sharpy.Compiler.Tests/Semantic/Validation/SignatureValidatorV2Tests.cs`
- **Integration Tests**: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` (`.spy` + `.error` pairs)

---

## Summary

The `SignatureValidatorV2` is a focused, early-stage validator that ensures dunder methods have correct signatures before they're used by downstream compiler phases. By running at Order 150 (before type checking), it provides clear, actionable error messages and prevents cascading errors.

**Key Takeaways**:
- **Operators**: Must have correct parameter counts (1 for unary, 2 for binary) and return types
- **Protocols**: Must match `ProtocolRegistry` expectations for params, return types, and `self` naming
- **Design**: Consolidates two legacy validators into one cohesive component
- **Error Messages**: Provides contextual, helpful feedback with interface hints
- **Extensibility**: Easy to add new operators (update HashSets) or protocols (update registry)
