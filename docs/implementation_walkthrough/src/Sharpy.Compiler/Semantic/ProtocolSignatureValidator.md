# Walkthrough: ProtocolSignatureValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ProtocolSignatureValidator.cs`

---

## Overview

The `ProtocolSignatureValidator` is a static utility class responsible for validating that protocol dunder methods (special methods like `__len__`, `__str__`, `__init__`, etc.) have correct signatures in Sharpy source code. It acts as a semantic analysis gate-keeper, ensuring that when developers implement these special Python-style methods in their classes, they follow the expected parameter counts, return types, and naming conventions.

**Key Responsibilities:**
- Validate parameter counts for protocol dunder methods
- Verify return type annotations match protocol expectations
- Ensure the first parameter is named `self`
- Provide clear, actionable error messages with source location information

**Where it fits in the pipeline:**
```
Source Code → Lexer → Parser → AST → NameResolver → [ProtocolSignatureValidator] → TypeResolver → CodeGen
```

The validator is invoked during the **name resolution phase** of semantic analysis, specifically when processing method definitions within class bodies.

---

## Class Structure

### Static Class Design

```csharp
public static class ProtocolSignatureValidator
```

This is a **static utility class** with no state—all methods are pure functions that operate on the data passed to them. This design choice makes sense because:
- Validation logic is stateless
- No configuration or instance data is needed
- Can be easily called from anywhere in the semantic analysis phase
- Thread-safe by design (no mutable state)

### Key Methods

The class exposes two public methods:

1. **`IsProtocolDunder(string methodName)`** - Quick check if a method name is a protocol dunder
2. **`ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)`** - Full validation with error reporting

---

## Method Walkthrough

### 1. IsProtocolDunder

```csharp
public static bool IsProtocolDunder(string methodName)
    => ProtocolRegistry.IsProtocolDunder(methodName);
```

**Purpose:** Determine if a given method name is a recognized protocol dunder method.

**How it works:**
- Delegates to `ProtocolRegistry.IsProtocolDunder()`
- Acts as a façade/convenience method
- Returns `true` for methods like `__len__`, `__str__`, `__init__`, etc.
- Returns `false` for regular methods and operator dunders (which are handled separately)

**When to use this:**
The `NameResolver` uses this as a quick filter before invoking full validation:
```csharp
if (ProtocolSignatureValidator.IsProtocolDunder(method.Name))
{
    var errors = ProtocolSignatureValidator.ValidateDunderSignature(method, owningType);
    // Handle errors...
}
```

**Design note:** This separation between protocol dunders (handled here) and operator dunders (handled by `OperatorSignatureValidator`) reflects Python's distinction between "magic methods for protocols" vs "operator overloading methods."

---

### 2. ValidateDunderSignature

```csharp
public static List<SemanticError> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
```

**Purpose:** The main validation entry point that orchestrates all signature checks for a protocol dunder method.

**Parameters:**
- `funcDef`: The AST node representing the function definition (contains name, parameters, return type, location info)
- `owningType`: The type symbol for the class that contains this method (used in error messages)

**Return Value:**
- Returns a `List<SemanticError>` (empty if valid, populated if issues found)
- Each error contains message, line number, and column number for precise IDE feedback

**Algorithm:**
```csharp
var errors = new List<SemanticError>();
var methodName = funcDef.Name;

var protocol = ProtocolRegistry.GetProtocol(methodName);
if (protocol == null)
{
    // Not a protocol dunder, no validation needed
    return errors;
}

// Three-pronged validation:
ValidateParameterCount(funcDef, protocol, owningType, errors);
ValidateReturnType(funcDef, protocol, owningType, errors);
ValidateSelfParameter(funcDef, protocol, owningType, errors);

return errors;
```

**Key design decisions:**
1. **Early return for non-protocol methods** - If `ProtocolRegistry.GetProtocol()` returns null, skip validation
2. **Accumulative error collection** - All three validators add to the same error list, so users see all issues at once
3. **Protocol-driven validation** - The `ProtocolInfo` record drives what's expected (parameter count, return type, etc.)

---

### 3. ValidateParameterCount (Private Helper)

```csharp
private static void ValidateParameterCount(
    FunctionDef funcDef,
    ProtocolInfo protocol,
    TypeSymbol owningType,
    List<SemanticError> errors)
```

**Purpose:** Ensure the method has the correct number of parameters.

**Key Logic:**

1. **Variable parameter count handling:**
   ```csharp
   if (expectedCount == -1)
       return;
   ```
   Some protocols like `__init__` can have any number of parameters (as long as they have `self`). The special value `-1` signals "skip count validation."

2. **Context-aware error messages:**
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
   
   This pattern-matching switch generates **meaningful parameter names** in error messages. Instead of saying "expected 2 parameters," it says "expected `(self, item)`" for `__contains__`, making errors immediately actionable.

3. **Interface reference in errors:**
   ```csharp
   protocol.SharpyCoreInterface != null
       ? $"See interface '{protocol.SharpyCoreInterface}' for expected signature."
       : ""
   ```
   
   When protocols map to Sharpy.Core interfaces (like `ISized` for `__len__`), the error message directs developers to the interface definition for reference.

**Example error:**
```
Protocol method '__len__' on 'MyClass' must have exactly 1 parameter (self), got 2. 
See interface 'ISized' for expected signature.
```

---

### 4. ValidateReturnType (Private Helper)

```csharp
private static void ValidateReturnType(
    FunctionDef funcDef,
    ProtocolInfo protocol,
    TypeSymbol owningType,
    List<SemanticError> errors)
```

**Purpose:** Verify the return type annotation matches protocol expectations.

**Key Logic:**

1. **Skip if no constraint:**
   ```csharp
   if (protocol.ExpectedReturnType == null)
       return;
   ```
   Some protocols like `__getitem__` return generic types, so there's no fixed expectation to validate.

2. **Skip if no annotation:**
   ```csharp
   if (funcDef.ReturnType == null)
       return;
   ```
   If the developer omitted a return type annotation, type inference will handle it. This validator only checks **explicit** annotations.

3. **Type normalization:**
   ```csharp
   var actualReturnType = TypeAnnotationHelper.GetName(funcDef.ReturnType);
   
   var expectedNormalized = protocol.ExpectedReturnType == "None" ? "void" : protocol.ExpectedReturnType;
   var actualNormalized = actualReturnType == "None" ? "void" : actualReturnType;
   ```
   
   **Why normalize?** In Sharpy, `None` and `void` are semantically equivalent. The `TypeAnnotationHelper.GetName()` method converts the AST's `TypeAnnotation` into a string like `"int"`, `"str"`, or `"list[int]?"`.

4. **Case-insensitive comparison:**
   ```csharp
   if (!string.Equals(actualNormalized, expectedNormalized, StringComparison.OrdinalIgnoreCase))
   ```
   
   This allows flexibility: `Int`, `int`, and `INT` all match (though Sharpy convention is lowercase).

**Example error:**
```
Protocol method '__str__' on 'MyClass' must return 'str', got 'int'. 
See interface 'IStrConvertible' for expected signature.
```

---

### 5. ValidateSelfParameter (Private Helper)

```csharp
private static void ValidateSelfParameter(
    FunctionDef funcDef,
    ProtocolInfo protocol,
    TypeSymbol owningType,
    List<SemanticError> errors)
```

**Purpose:** Ensure the first parameter is named `self` (Python convention).

**Key Logic:**

1. **Handle zero-parameter edge case:**
   ```csharp
   if (funcDef.Parameters.Count == 0)
   {
       if (protocol.ExpectedParamCount == -1)
       {
           errors.Add(new SemanticError(
               $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have 'self' as first parameter",
               funcDef.LineStart,
               funcDef.ColumnStart));
       }
       return;
   }
   ```
   
   **Subtle logic:** For variable-parameter protocols (like `__init__` with count `-1`), if there are zero parameters, that's a `self` error. For fixed-count protocols, the parameter count validator will already have flagged this, so we skip adding a redundant error.

2. **Check first parameter name:**
   ```csharp
   if (funcDef.Parameters[0].Name != "self")
   {
       errors.Add(new SemanticError(...));
   }
   ```
   
   Strict enforcement of Python convention: `self` is not just a convention in Sharpy—it's required for protocol methods.

**Example error:**
```
First parameter of protocol method '__len__' on 'MyClass' must be 'self', got 'this'
```

---

## Dependencies

### ProtocolRegistry

The validator's "brain"—a static registry that maps dunder method names to their expected signatures:

```csharp
var protocol = ProtocolRegistry.GetProtocol("__len__");
// Returns: ProtocolInfo(
//     DunderName: "__len__",
//     Kind: ProtocolKind.Container,
//     SharpyCoreInterface: "ISized",
//     InterfaceMethodName: "__Len__",
//     ClrMethodName: "get_Count",
//     ExpectedParamCount: 1,
//     ExpectedReturnType: "int"
// )
```

**ProtocolInfo Record:**
- Immutable data structure holding all metadata for a protocol dunder
- `ExpectedParamCount = -1` means variable (any count >= 1)
- `ExpectedReturnType = null` means any type (generic)
- Maps Sharpy dunders → Sharpy.Core interfaces → .NET methods

### TypeAnnotationHelper

Converts AST `TypeAnnotation` nodes to human-readable strings:

```csharp
TypeAnnotation node = new TypeAnnotation 
{ 
    Name = "list", 
    TypeArguments = [new TypeAnnotation { Name = "int" }],
    IsNullable = true 
};

TypeAnnotationHelper.GetName(node);  // Returns: "list[int]?"
```

**Why needed?** The AST stores types as structured data (name + type arguments + nullable flag), but validation errors need readable strings.

### Parser.Ast.FunctionDef

The AST node for function definitions:

```csharp
public record FunctionDef : Statement
{
    public string Name { get; init; }                  // e.g., "__len__"
    public List<Parameter> Parameters { get; init; }   // e.g., [Parameter("self", ...)]
    public TypeAnnotation? ReturnType { get; init; }   // e.g., "int" annotation
    public int LineStart { get; init; }                // For error messages
    public int ColumnStart { get; init; }
    // ... other fields
}
```

**Immutable design:** All AST nodes are C# records with `init` accessors—immutability prevents accidental modifications during analysis.

### SemanticError

Custom exception for compilation errors:

```csharp
public class SemanticError : Exception
{
    public int? Line { get; }
    public int? Column { get; }
    
    public SemanticError(string message, int? line = null, int? column = null)
        : base(FormatMessage(message, line, column))
}
```

**Error message format:**
```
Semantic error at line 42, column 5: Protocol method '__len__' on 'MyClass' must have exactly 1 parameter (self), got 2.
```

---

## Patterns and Design Decisions

### 1. Separation of Concerns

**Protocol dunders vs. Operator dunders:**
- `ProtocolSignatureValidator` handles `__len__`, `__str__`, `__init__`, etc.
- `OperatorSignatureValidator` handles `__add__`, `__eq__`, `__mul__`, etc.

**Why separate?** Different validation rules:
- Protocol methods map to .NET interfaces and methods
- Operator methods map to .NET operator overloads
- Cleaner code than one mega-validator

### 2. Registry-Driven Validation

Instead of hardcoding validation logic for each dunder, the validator queries `ProtocolRegistry`:

```csharp
var protocol = ProtocolRegistry.GetProtocol(methodName);
if (protocol == null) return;  // Not a protocol dunder
```

**Benefits:**
- **Single source of truth**: Add a new protocol? Update the registry, validator "just works"
- **Maintainability**: All protocol metadata in one place
- **Extensibility**: Easy to add new protocols without touching validation logic

### 3. Accumulative Error Reporting

All three validation methods add to the same `errors` list:

```csharp
ValidateParameterCount(funcDef, protocol, owningType, errors);
ValidateReturnType(funcDef, protocol, owningType, errors);
ValidateSelfParameter(funcDef, protocol, owningType, errors);
```

**Why?** Better developer experience—see all issues at once rather than fixing one at a time and recompiling.

### 4. Defensive Null Handling

Multiple early returns prevent null dereferences:

```csharp
if (protocol == null) return errors;                    // ValidateDunderSignature
if (expectedCount == -1) return;                        // ValidateParameterCount
if (protocol.ExpectedReturnType == null) return;        // ValidateReturnType
if (funcDef.ReturnType == null) return;                 // ValidateReturnType
```

**Philosophy:** "Validate what exists, ignore what's missing." Missing annotations will be handled by type inference, not signature validation.

### 5. Context-Rich Error Messages

Every error message includes:
1. **What's wrong**: "must have exactly 1 parameter"
2. **Where it's wrong**: Line and column numbers
3. **What's expected**: "(self)" or "return 'str'"
4. **Where to learn more**: "See interface 'ISized'"

**Example:**
```
Semantic error at line 15, column 5: Protocol method '__contains__' on 'MySet' must have 
exactly 2 parameters (self, item), got 3. See interface 'IContainer' for expected signature.
```

IDE integration: The line/column info enables "jump to error" functionality.

---

## Debugging Tips

### Common Issues

**1. "Why isn't my `__len__` being validated?"**

Check if it's actually a protocol dunder:
```csharp
bool isProtocol = ProtocolSignatureValidator.IsProtocolDunder("__len__");  // true
bool isProtocol = ProtocolSignatureValidator.IsProtocolDunder("__add__");  // false (operator dunder)
```

**2. "Validation passed but codegen fails"**

The signature validator only checks:
- Parameter **count**
- Return type **name**
- First parameter is `self`

It does NOT check:
- Parameter types (that's `TypeChecker`'s job)
- Generic type arguments
- Type compatibility

**3. "Error says 'expected 2 parameters' but I have 2"**

Count the `self` parameter—it's included in the count:
```python
def __contains__(item):  # Wrong: 1 parameter (missing self)
    pass

def __contains__(self, item):  # Correct: 2 parameters
    pass
```

### Adding Debug Output

To understand what's being validated, add logging:

```csharp
public static List<SemanticError> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
{
    var errors = new List<SemanticError>();
    var methodName = funcDef.Name;
    
    // DEBUG: What are we validating?
    Console.WriteLine($"Validating {owningType.Name}.{methodName} with {funcDef.Parameters.Count} params");

    var protocol = ProtocolRegistry.GetProtocol(methodName);
    if (protocol == null)
    {
        Console.WriteLine($"  → Not a protocol dunder, skipping");
        return errors;
    }
    
    Console.WriteLine($"  → Expected: {protocol.ExpectedParamCount} params, returns {protocol.ExpectedReturnType}");
    // ... rest of validation
}
```

### Testing Protocol Validation

Create a test Sharpy file:

```python
class MyClass:
    def __len__(self, extra_param) -> int:  # Should error: too many params
        return 0
    
    def __str__(self) -> int:  # Should error: wrong return type
        return 42
    
    def __init__(this, value):  # Should error: 'this' instead of 'self'
        pass
```

Compile and check for expected errors.

---

## Contribution Guidelines

### Adding a New Protocol Dunder

If you need to add support for a new protocol method (e.g., `__bytes__`):

1. **Update `ProtocolRegistry.cs`:**
   ```csharp
   Register(protocols, new ProtocolInfo(
       DunderName: "__bytes__",
       Kind: ProtocolKind.Conversion,
       SharpyCoreInterface: "IBytesConvertible",  // If it maps to an interface
       InterfaceMethodName: "__Bytes__",
       ClrMethodName: null,  // Or the .NET method name
       ExpectedParamCount: 1,  // Just self
       ExpectedReturnType: "bytes"
   ));
   ```

2. **No changes needed in `ProtocolSignatureValidator.cs`!** It's registry-driven.

3. **Add test cases** in `Sharpy.Compiler.Tests/Semantic/`:
   ```csharp
   [Fact]
   public void TestBytesProtocol_InvalidSignature()
   {
       var source = @"
           class MyClass:
               def __bytes__(self, extra) -> bytes:  # Should error
                   pass
       ";
       var errors = CompileAndGetErrors(source);
       Assert.Contains("must have exactly 1 parameter", errors[0].Message);
   }
   ```

### Improving Error Messages

To enhance the `paramDescription` switch:

1. **Identify the protocol** that needs better messaging
2. **Add a case** to the switch in `ValidateParameterCount`:
   ```csharp
   var paramDescription = (expectedCount, protocol.DunderName) switch
   {
       (1, _) => "(self)",
       (2, "__contains__") => "(self, item)",
       (2, "__your_new_method__") => "(self, your_descriptive_name)",  // Add here
       // ... existing cases
   };
   ```

### Adding New Validation Rules

If protocols need additional validation (e.g., checking parameter types):

1. **Create a new private method:**
   ```csharp
   private static void ValidateParameterTypes(
       FunctionDef funcDef,
       ProtocolInfo protocol,
       TypeSymbol owningType,
       List<SemanticError> errors)
   {
       // New validation logic
   }
   ```

2. **Call it from `ValidateDunderSignature`:**
   ```csharp
   ValidateParameterCount(funcDef, protocol, owningType, errors);
   ValidateReturnType(funcDef, protocol, owningType, errors);
   ValidateSelfParameter(funcDef, protocol, owningType, errors);
   ValidateParameterTypes(funcDef, protocol, owningType, errors);  // New
   ```

### Code Style Notes

- **Use expression-bodied methods** for simple delegates: `=> ProtocolRegistry.IsProtocolDunder(methodName)`
- **Use pattern matching** for readable conditional logic: `var x = y switch { ... }`
- **Accumulate errors** rather than throwing on first issue (better UX)
- **Include source location** in all `SemanticError` instances
- **Keep methods focused**: Each validation method does one thing

---

## Integration Points

### Called By: NameResolver

Location: `src/Sharpy.Compiler/Semantic/NameResolver.cs`

```csharp
else if (ProtocolSignatureValidator.IsProtocolDunder(method.Name))
{
    var validationErrors = ProtocolSignatureValidator.ValidateDunderSignature(method, owningType);
    
    if (validationErrors.Count > 0)
    {
        foreach (var error in validationErrors)
        {
            _logger.LogError(error.Message);
        }
        // Continue processing (non-fatal for name resolution)
    }
}
```

**When?** During class member resolution, after the parser has built the AST but before type resolution.

### Complemented By: OperatorSignatureValidator

For operator dunders like `__add__`, `__eq__`, etc., use `OperatorSignatureValidator`:

```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var errors = OperatorSignatureValidator.ValidateDunderSignature(...);
}
else if (ProtocolSignatureValidator.IsProtocolDunder(method.Name))
{
    var errors = ProtocolSignatureValidator.ValidateDunderSignature(...);
}
```

**Division of labor:**
- **Protocol**: Non-operator magic methods that define type behavior
- **Operator**: Magic methods that map to arithmetic/comparison/bitwise operators

### Related: TypeChecker

`TypeChecker` performs deeper type validation after signatures are validated:

```csharp
// ProtocolSignatureValidator: "Do you have 2 parameters named (self, item)?"
// TypeChecker: "Is 'item' the right type for this container's element type?"
```

The signature validator is a **lightweight first pass**, catching obvious signature mistakes early before expensive type analysis.

---

## Key Takeaways

1. **Purpose**: Validates protocol dunder method signatures during semantic analysis
2. **Architecture**: Registry-driven, stateless, pure-function design
3. **Separation**: Handles protocol dunders; operator dunders go to `OperatorSignatureValidator`
4. **Error Handling**: Accumulates all errors with rich context for better developer experience
5. **Extensibility**: Add new protocols by updating `ProtocolRegistry`—validator adapts automatically
6. **Philosophy**: "Validate explicitly annotated signatures; let type inference handle the rest"

When debugging signature validation issues, start by checking:
1. Is the method name a recognized protocol dunder? (`IsProtocolDunder`)
2. What does `ProtocolRegistry.GetProtocol()` return for this method?
3. Are all three validation methods (`Count`, `ReturnType`, `Self`) passing?
4. Do error messages contain the expected source location?

For contributions, the code is designed to be extended at the `ProtocolRegistry` level—most new features won't require changes to the validator itself, showcasing excellent separation of concerns.
