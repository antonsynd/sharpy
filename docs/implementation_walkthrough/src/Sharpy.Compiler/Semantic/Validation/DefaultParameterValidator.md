# Walkthrough: DefaultParameterValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/DefaultParameterValidator.cs`

---

## Overview

`DefaultParameterValidator` is a **semantic validator** that enforces Python-style rules for function parameter default values. It runs during the **Semantic Analysis** phase of the Sharpy compiler pipeline, specifically during the **Validation Pipeline** (Pass 5).

### What It Does

This validator catches three critical categories of default parameter errors:

1. **Mutable defaults** (e.g., `def foo(x=[]):`) - prevents the classic Python footgun
2. **Non-constant defaults** (e.g., `def foo(x=some_function()):`) - ensures defaults are compile-time constants
3. **`None` on non-nullable types** (e.g., `def foo(x: int = None):`) - enforces Sharpy's null-safety

### Role in the Compiler Pipeline

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → ValidationPipeline → RoslynEmitter → C#
                                              ↓
                                    [Order 250: DefaultParameterValidator]
                                    [Order 300: TypeChecker]
```

**Order**: 250 (runs **before** type checking at order 300)

This early placement is intentional—we catch parameter errors before type checking tries to infer types from potentially invalid defaults.

---

## Class/Type Structure

### Inheritance Hierarchy

```csharp
ISemanticValidator
    ↑
SemanticValidatorBase (provides AddError/AddWarning helpers)
    ↑
DefaultParameterValidator
```

### Key Properties

| Property | Type | Purpose |
|----------|------|---------|
| `Name` | `string` | Returns `"DefaultParameterValidator"` for logging/debugging |
| `Order` | `int` | Returns `250` (runs before type checking) |
| `_logger` | `ICompilerLogger` | Logger instance for debug output |
| `_context` | `SemanticContext` | Shared context with symbol table, type resolver, diagnostics |

### Design Notes

- **Stateless between calls**: Fields `_logger` and `_context` are set fresh on each `Validate()` call
- **Pipeline-compatible**: Implements `ISemanticValidator` for use in `ValidationPipeline`
- **Pipeline-compatible**: This is the pipeline-compatible version using `ValidationPipeline` and `SemanticContext`

---

## Key Functions/Methods

### 1. `Validate(Module, SemanticContext)` - Entry Point

**Signature:**
```csharp
public override void Validate(Module module, SemanticContext context)
```

**Purpose**: Entry point called by `ValidationPipeline`. Initializes context and kicks off AST traversal.

**Flow**:
1. Store `context` and `logger` in instance fields
2. Log validation start
3. Traverse all top-level statements in the module

**Implementation Details**:
```csharp
_context = context;
_logger = context.Logger;
_logger.LogDebug("Starting default parameter validation");

foreach (var stmt in module.Body)
{
    ValidateStatement(stmt);
}
```

**Connection to Pipeline**: Called automatically by `ValidationPipeline.Validate()` which iterates through all registered validators in order.

---

### 2. `ValidateStatement(Statement)` - AST Traversal

**Signature:**
```csharp
private void ValidateStatement(Statement stmt)
```

**Purpose**: Recursively traverses the AST to find all function definitions (including nested functions and class methods).

**Flow**:
```
FunctionDef → ValidateFunctionDefaults() + recurse into body
ClassDef    → recurse into all members
StructDef   → recurse into all members
```

**Why Recursive?** Functions can be nested:
```python
def outer():
    def inner(x=[]):  # ← Must validate this too!
        pass
```

**Key Pattern**: This is a **depth-first traversal** that visits every statement in the AST tree.

---

### 3. `ValidateFunctionDefaults(FunctionDef)` - Function-Level Check

**Signature:**
```csharp
private void ValidateFunctionDefaults(FunctionDef functionDef)
```

**Purpose**: Iterates through all parameters of a function and validates each default value.

**Implementation**:
```csharp
foreach (var param in functionDef.Parameters)
{
    if (param.DefaultValue != null)
    {
        ValidateDefaultValue(param, functionDef.Name);
    }
}
```

**Upstream Connection**: Called from `ValidateStatement()` when a `FunctionDef` node is encountered.

---

### 4. `ValidateDefaultValue(Parameter, string)` - Core Validation Logic

**Signature:**
```csharp
private void ValidateDefaultValue(Parameter param, string functionName)
```

**Purpose**: Performs three checks on a parameter's default value:

#### Check 1: Mutable Defaults (Most Common Error)

```csharp
if (IsMutableDefault(defaultValue))
{
    AddError(_context,
        $"Mutable default value is not allowed for parameter '{param.Name}' in function '{functionName}'. " +
        "Use None as default and initialize in the function body instead.",
        param.LineStart,
        param.ColumnStart);
    return;
}
```

**Why This Matters**: In Python, mutable defaults are shared across all calls:
```python
def append_to(element, lst=[]):  # BAD!
    lst.append(element)
    return lst

append_to(1)  # [1]
append_to(2)  # [1, 2] ← Unexpected!
```

**Sharpy's Solution**: Force users to use `None` as default:
```python
def append_to(element, lst: list[int]? = None):
    if lst is None:
        lst = []
    lst.append(element)
    return lst
```

#### Check 2: Compile-Time Constants

```csharp
if (!IsCompileTimeConstant(defaultValue))
{
    AddError(_context,
        $"Default value for parameter '{param.Name}' in function '{functionName}' must be a compile-time constant expression",
        param.LineStart,
        param.ColumnStart);
    return;
}
```

**What's Allowed**: Literals, tuples of literals, const references, enum members
**What's Not**: Function calls, variable references, comprehensions

#### Check 3: None on Non-Nullable Types

```csharp
if (defaultValue is NoneLiteral)
{
    var paramType = _context.TypeResolver.ResolveTypeAnnotation(param.Type);

    if (paramType is not NullableType && paramType is not UnknownType)
    {
        AddError(_context,
            $"Cannot use 'None' as default value for non-nullable parameter '{param.Name}' of type '{paramType.GetDisplayName()}' in function '{functionName}'. " +
            $"Use '{paramType.GetDisplayName()}?' to make the parameter nullable.",
            param.LineStart,
            param.ColumnStart);
    }
}
```

**Downstream Connection**: Uses `TypeResolver` from `SemanticContext` to resolve the parameter's type annotation.

**Error Example**:
```python
def greet(name: str = None):  # ERROR: str is not nullable
    pass

# Fix:
def greet(name: str? = None):  # OK: str? is nullable
    pass
```

---

### 5. `IsMutableDefault(Expression)` - Mutable Default Detection

**Signature:**
```csharp
private static bool IsMutableDefault(Expression expr)
```

**Purpose**: Identifies expressions that would create mutable default values.

**Detection Rules**:

| Python Code | AST Node | Detected? |
|-------------|----------|-----------|
| `[]` | `ListLiteral` | ✅ Yes |
| `[1, 2, 3]` | `ListLiteral` | ✅ Yes |
| `{}` | `DictLiteral` | ✅ Yes |
| `{1, 2, 3}` | `SetLiteral` | ✅ Yes |
| `list()` | `FunctionCall` with name "list" | ✅ Yes |
| `dict()` | `FunctionCall` with name "dict" | ✅ Yes |
| `set()` | `FunctionCall` with name "set" | ✅ Yes |
| `(1, 2, 3)` | `TupleLiteral` | ❌ No (immutable!) |
| `"hello"` | `StringLiteral` | ❌ No (immutable!) |

**Implementation Pattern**: Uses C# pattern matching with `switch` expressions:

```csharp
return expr switch
{
    ListLiteral => true,
    DictLiteral => true,
    SetLiteral => true,
    FunctionCall call when call.Function is Identifier id && id.Name == "set" => true,
    FunctionCall call when call.Function is Identifier id && id.Name == "list" => true,
    FunctionCall call when call.Function is Identifier id && id.Name == "dict" => true,
    Parenthesized paren => IsMutableDefault(paren.Expression),
    _ => false
};
```

**Design Decision**: Recursively checks `Parenthesized` expressions to handle `def foo(x=([])):`

---

### 6. `IsCompileTimeConstant(Expression)` - Constant Expression Validation

**Signature:**
```csharp
private bool IsCompileTimeConstant(Expression expr)
```

**Purpose**: Determines if an expression can be evaluated at compile-time (required for C# default parameters).

**What's Allowed**:

#### ✅ Primitive Literals
```csharp
IntegerLiteral => true,
FloatLiteral => true,
StringLiteral => true,
BooleanLiteral => true,
NoneLiteral => true,
```

#### ✅ Tuples of Constants
```csharp
TupleLiteral tuple => tuple.Elements.All(IsCompileTimeConstant),
```
Example: `(1, 2, "hello")` is allowed

#### ✅ Operations on Constants
```csharp
UnaryOp unary => IsCompileTimeConstant(unary.Operand),
BinaryOp binary => IsCompileTimeConstant(binary.Left) && IsCompileTimeConstant(binary.Right),
```
Example: `-42`, `1 + 2` (though unusual in practice)

#### ✅ Const References
```csharp
Identifier id => IsConstReference(id),
```
Example:
```python
const MAX_SIZE: int = 100
def process(size: int = MAX_SIZE):  # OK!
    pass
```

#### ✅ Enum Members
```csharp
MemberAccess memberAccess => IsEnumMemberAccess(memberAccess),
```
Example:
```python
enum Color:
    RED = 1
    GREEN = 2

def draw(color: Color = Color.RED):  # OK!
    pass
```

#### ✅ Conditional Expressions (Ternary)
```csharp
ConditionalExpression cond =>
    IsCompileTimeConstant(cond.Test) &&
    IsCompileTimeConstant(cond.ThenValue) &&
    IsCompileTimeConstant(cond.ElseValue),
```
Example: `def foo(x: int = 1 if True else 2):`

#### ❌ What's **Not** Allowed
```csharp
FunctionCall => false,          // def foo(x=bar()): NO
ListLiteral => false,           // def foo(x=[]): NO
DictLiteral => false,           // def foo(x={}): NO
SetLiteral => false,            // def foo(x={1,2}): NO
IndexAccess => false,           // def foo(x=arr[0]): NO
ListComprehension => false,     // def foo(x=[i for i in range(10)]): NO
LambdaExpression => false,      // def foo(x=lambda: 1): NO
```

**Connection to .NET**: This mirrors C# default parameter restrictions—only compile-time constants can be used.

---

### 7. `IsConstReference(Identifier)` - Const Declaration Check

**Signature:**
```csharp
private bool IsConstReference(Identifier id)
```

**Purpose**: Checks if an identifier references a `const` declaration.

**Implementation**:
```csharp
var symbol = _context.SymbolTable.Lookup(id.Name);
return symbol is VariableSymbol { IsConstant: true };
```

**Upstream Dependency**: Relies on `SymbolTable` from `SemanticContext`, which is populated by `NameResolver` (runs at order 100).

**Example**:
```python
const PI: float = 3.14159
def calculate_area(radius: float = 1.0, pi: float = PI):  # PI is const, OK!
    return pi * radius * radius
```

---

### 8. `IsEnumMemberAccess(MemberAccess)` - Enum Member Check

**Signature:**
```csharp
private bool IsEnumMemberAccess(MemberAccess memberAccess)
```

**Purpose**: Determines if a member access is referring to an enum member (e.g., `Color.RED`).

**Implementation**:
```csharp
// The object must be an identifier (the enum type name)
if (memberAccess.Object is not Identifier typeId)
{
    return false;
}

// Look up the type in the symbol table
var symbol = _context.SymbolTable.Lookup(typeId.Name);

// Check if it's an enum type
return symbol is TypeSymbol { TypeKind: TypeKind.Enum };
```

**Example**:
```python
enum HttpMethod:
    GET = 1
    POST = 2

def request(method: HttpMethod = HttpMethod.GET):  # OK: enum member
    pass
```

**Limitation**: Only handles simple enum access (`Enum.MEMBER`). Nested enums or module-qualified enums would need additional logic.

---

## Dependencies

### Internal Dependencies

#### From `Sharpy.Compiler.Parser.Ast`
- **AST Nodes**: `Module`, `Statement`, `Expression`, `FunctionDef`, `ClassDef`, `StructDef`, `Parameter`
- **Expression Types**: All expression types for pattern matching (`ListLiteral`, `DictLiteral`, `IntegerLiteral`, etc.)

#### From `Sharpy.Compiler.Logging`
- **`ICompilerLogger`**: For debug/info logging
- **`NullLogger`**: Fallback when no logger is provided

### External Dependencies (via `SemanticContext`)

#### `SymbolTable`
- **Used by**: `IsConstReference()`, `IsEnumMemberAccess()`
- **Purpose**: Look up symbol definitions (variables, types)
- **Populated by**: `NameResolver` (order 100)

#### `TypeResolver`
- **Used by**: `ValidateDefaultValue()` (for None checks)
- **Purpose**: Resolve type annotations to semantic types
- **Populated by**: `TypeResolver` pass (order 200)

#### `DiagnosticBag` (via `context.Diagnostics`)
- **Used by**: `AddError()` inherited from `SemanticValidatorBase`
- **Purpose**: Collect error messages with source locations

---

## Patterns and Design Decisions

### 1. **Validator Pipeline Pattern**

This validator follows the **Validator Pipeline Pattern** introduced in Sharpy v0.1:

```csharp
ValidationPipeline
    .AddValidator(new DefaultParameterValidator())
    .AddValidator(new TypeChecker())
    .Validate(module, context);
```

**Benefits**:
- **Composable**: Validators can be added/removed independently
- **Ordered**: `Order` property ensures correct execution sequence
- **Testable**: Each validator can be tested in isolation
- **LSP-ready**: Future support for incremental validation

### 2. **Separation of Concerns**

| Concern | Handled By |
|---------|------------|
| **Mutable defaults** | `IsMutableDefault()` |
| **Compile-time constants** | `IsCompileTimeConstant()` |
| **Null safety** | `ValidateDefaultValue()` + `TypeResolver` |

Each concern is isolated in its own method with a clear responsibility.

### 3. **Immutable AST Pattern**

The AST is **immutable** (readonly records). This validator:
- ✅ Reads from the AST
- ❌ Never modifies the AST
- ✅ Reports errors to `DiagnosticBag`

### 4. **Early Failure Pattern**

In `ValidateDefaultValue()`, checks are ordered by severity:
1. **Mutable default** → `return` (most common error)
2. **Non-constant** → `return` (next most common)
3. **None on non-nullable** → final check

Early returns avoid cascading error messages.

### 5. **Static Helper Methods**

`IsMutableDefault()` is `static` because:
- ✅ No state required
- ✅ Could be unit-tested independently
- ✅ Signals "pure function" to readers

`IsCompileTimeConstant()` is **not** static because:
- ❌ Needs `_context.SymbolTable` for const references
- ❌ Needs enum type checking

### 6. **Pattern Matching Over Visitor Pattern**

The validator uses **C# pattern matching** (`switch` expressions) instead of the traditional Visitor pattern:

**Why?**
- ✅ More concise for simple checks
- ✅ Compiler-enforced exhaustiveness (when using `switch` carefully)
- ✅ Better performance (no virtual dispatch)
- ❌ Less flexible than Visitor if traversal logic becomes complex

### 7. **Error Message Design**

Error messages follow a consistent pattern:
1. **What's wrong**: "Mutable default value is not allowed..."
2. **Context**: "...for parameter 'x' in function 'foo'"
3. **How to fix**: "Use None as default and initialize in the function body"

Example:
```
Cannot use 'None' as default value for non-nullable parameter 'x' of type 'int' in function 'foo'.
Use 'int?' to make the parameter nullable.
```

---

## Debugging Tips

### 1. **Enable Debug Logging**

Set the logger to show debug messages:
```csharp
var context = new SemanticContext(..., logger: new ConsoleLogger(LogLevel.Debug));
```

You'll see:
```
[DEBUG] Starting default parameter validation
```

### 2. **Check Validator Order**

If validation seems to fail unexpectedly, verify that `DefaultParameterValidator` runs **before** `TypeChecker`:

```csharp
var pipeline = new ValidationPipeline();
foreach (var validator in pipeline.Validators)
{
    Console.WriteLine($"{validator.Name}: Order {validator.Order}");
}
```

Expected output:
```
DefaultParameterValidator: Order 250
TypeChecker: Order 300
```

### 3. **Isolate Validators in Tests**

Test `DefaultParameterValidator` alone:
```csharp
var pipeline = ValidationPipeline.CreateEmpty()
    .AddValidator(new DefaultParameterValidator());

var diagnostics = pipeline.Validate(module, context);
```

### 4. **Common Pitfalls**

#### Pitfall 1: False Positives on Tuples
**Symptom**: Tuples like `(1, 2)` are incorrectly flagged as mutable.
**Root Cause**: `IsMutableDefault()` might be checking `TupleLiteral`.
**Check**: Verify line 122-130 only checks `ListLiteral`, `DictLiteral`, `SetLiteral`.

#### Pitfall 2: Missing Enum Members
**Symptom**: Enum member defaults like `Color.RED` are rejected.
**Root Cause**: `IsEnumMemberAccess()` might not be working.
**Debug**:
```csharp
var symbol = _context.SymbolTable.Lookup("Color");
Console.WriteLine($"Symbol: {symbol}, TypeKind: {(symbol as TypeSymbol)?.TypeKind}");
```

#### Pitfall 3: Nested Functions Not Validated
**Symptom**: Nested function parameters aren't checked.
**Root Cause**: `ValidateStatement()` might not be recursing into function bodies.
**Check**: Line 40-42 should recurse into `funcDef.Body`.

### 5. **Test with Edge Cases**

```python
# Edge case 1: Parenthesized mutable default
def foo(x=([1, 2])):  # Should be caught
    pass

# Edge case 2: Const reference
const DEFAULT_SIZE = 10
def bar(size=DEFAULT_SIZE):  # Should pass
    pass

# Edge case 3: Binary operation
def baz(x=1+2):  # Should pass (compile-time constant)
    pass

# Edge case 4: Conditional with constants
def qux(x=1 if True else 2):  # Should pass
    pass
```

### 6. **Symbol Table Debugging**

If `IsConstReference()` fails, dump the symbol table:
```csharp
foreach (var (name, symbol) in _context.SymbolTable.GetAllSymbols())
{
    if (symbol is VariableSymbol vs)
        Console.WriteLine($"{name}: IsConstant={vs.IsConstant}");
}
```

### 7. **TypeResolver Debugging**

If None-checking fails, verify type resolution:
```csharp
var resolvedType = _context.TypeResolver.ResolveTypeAnnotation(param.Type);
Console.WriteLine($"Parameter type: {resolvedType.GetType().Name}, IsNullable: {resolvedType is NullableType}");
```

---

## Contribution Guidelines

### When to Modify This File

#### ✅ Add New Mutable Types
If Sharpy adds new mutable collection types (e.g., `deque`, `Counter`), update `IsMutableDefault()`:
```csharp
FunctionCall call when call.Function is Identifier id && id.Name == "deque" => true,
```

#### ✅ Extend Compile-Time Constants
If new constant expression types are added (e.g., string interpolation with constants), update `IsCompileTimeConstant()`:
```csharp
FStringLiteral fstr => fstr.Parts.All(part => part is StringLiteral || IsCompileTimeConstant(part)),
```

#### ✅ Improve Error Messages
Better diagnostics help users:
```csharp
AddError(_context,
    $"Mutable default value is not allowed for parameter '{param.Name}' in function '{functionName}'. " +
    "Use None as default and initialize in the function body instead. " +
    "Example: def {functionName}({param.Name}: {paramType}? = None):",
    param.LineStart,
    param.ColumnStart);
```

#### ✅ Support New AST Nodes
If new statement types are added (e.g., `ProtocolDef`), update `ValidateStatement()`:
```csharp
case ProtocolDef protocolDef:
    foreach (var member in protocolDef.Body)
        ValidateStatement(member);
    break;
```

### When **Not** to Modify This File

#### ❌ Type Checking Logic
Type checking belongs in `TypeChecker.cs`, not here.

**Wrong**:
```csharp
// Don't add type compatibility checks here!
if (!IsAssignableFrom(paramType, defaultValueType))
    AddError(...);
```

**Right**: Let `TypeChecker` handle this.

#### ❌ Name Resolution
Symbol lookups should use `SymbolTable`, not perform resolution.

**Wrong**:
```csharp
// Don't resolve names here!
var symbol = ResolveIdentifier(id.Name);
```

**Right**: Assume `SymbolTable` is already populated by `NameResolver`.

#### ❌ Code Generation
This validator doesn't emit C# code.

**Wrong**:
```csharp
// Don't generate code here!
var defaultCode = GenerateDefaultParameter(param);
```

**Right**: `RoslynEmitter` handles code generation.

### Testing Requirements

When modifying this file, add tests to `src/Sharpy.Compiler.Tests/Semantic/Validation/DefaultParameterValidatorTests.cs`:

#### Test Categories

1. **Mutable Default Tests**
   ```csharp
   [Fact]
   public void RejectListLiteralDefault()
   {
       var source = "def foo(x=[]):\n    pass";
       var errors = ValidateSource(source);
       Assert.Contains("Mutable default value", errors[0].Message);
   }
   ```

2. **Compile-Time Constant Tests**
   ```csharp
   [Fact]
   public void AcceptConstReference()
   {
       var source = "const MAX = 10\ndef foo(x=MAX):\n    pass";
       var errors = ValidateSource(source);
       Assert.Empty(errors);
   }
   ```

3. **Null Safety Tests**
   ```csharp
   [Fact]
   public void RejectNoneOnNonNullableType()
   {
       var source = "def foo(x: int = None):\n    pass";
       var errors = ValidateSource(source);
       Assert.Contains("non-nullable", errors[0].Message);
   }
   ```

4. **Edge Case Tests**
   ```csharp
   [Fact]
   public void AcceptTupleLiteralDefault()  // Tuples are immutable!
   {
       var source = "def foo(x=(1, 2)):\n    pass";
       var errors = ValidateSource(source);
       Assert.Empty(errors);
   }
   ```

### Code Style Guidelines

1. **Follow existing patterns**: Use `switch` expressions for AST node matching
2. **Early returns**: Check most common errors first
3. **Clear error messages**: Include context + how to fix
4. **Static when possible**: Make helpers static if they don't need state
5. **Document complex logic**: Add XML comments for non-obvious methods

### Performance Considerations

- **AST traversal is O(n)**: Acceptable since validation runs once per compilation
- **Symbol lookups are O(1)**: `SymbolTable.Lookup()` uses a dictionary
- **Type resolution is cached**: `TypeResolver` caches resolved types
- **Avoid redundant work**: Don't re-check the same default value multiple times

---

## Cross-References

### Related Validators

- **[AccessValidator.md](./AccessValidator.md)** - Validates access modifiers (private, protected, public)
- **[ControlFlowValidator.md](./ControlFlowValidator.md)** - Validates control flow (break/continue, return paths)
- **[ControlFlowValidatorV3.md](./ControlFlowValidatorV3.md)** - Enhanced control flow validation

### Validation Infrastructure

- **[AstTraversalContext.md](./AstTraversalContext.md)** - Centralized AST traversal state tracking

### Related Semantic Components

- **[NameResolver.md](../NameResolver.md)** - Populates `SymbolTable` used by `IsConstReference()`
- **[TypeResolver.md](../TypeResolver.md)** - Resolves type annotations used in None-checking
- **[TypeChecker.md](../TypeChecker.md)** - Type checks after this validator runs
- **[Symbol.md](../Symbol.md)** - Symbol types (`VariableSymbol`, `TypeSymbol`) used in lookups

### Upstream Dependencies

- **Parser AST Documentation**: See `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/` for AST node details

### Testing

- Test file: `src/Sharpy.Compiler.Tests/Semantic/Validation/DefaultParameterValidatorTests.cs`

### Legacy Comparison

- **Old validator**: `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs`
- **Walkthrough**: [DefaultParameterValidator.md](../DefaultParameterValidator.md)
- **Differences**: Uses `ValidationPipeline` and `SemanticContext`; old version was standalone

---

## Quick Reference

### Common Validation Scenarios

| Scenario | Result | Error Message |
|----------|--------|---------------|
| `def foo(x=[]):` | ❌ Error | "Mutable default value is not allowed" |
| `def foo(x={}):` | ❌ Error | "Mutable default value is not allowed" |
| `def foo(x=set()):` | ❌ Error | "Mutable default value is not allowed" |
| `def foo(x=(1,2)):` | ✅ OK | (Tuples are immutable) |
| `def foo(x=bar()):` | ❌ Error | "Must be a compile-time constant" |
| `def foo(x: int = None):` | ❌ Error | "Cannot use 'None' for non-nullable" |
| `def foo(x: int? = None):` | ✅ OK | (int? is nullable) |
| `const MAX=10\ndef foo(x=MAX):` | ✅ OK | (Const reference) |
| `def foo(color=Color.RED):` | ✅ OK | (Enum member) |

### Validator Order Reference

```
100: NameResolver           - Populates SymbolTable
200: TypeResolver           - Resolves type annotations
250: DefaultParameterValidator  ← This validator
300: TypeChecker            - Type checking
400: OperatorValidator      - Operator protocol validation
500: ProtocolValidator      - Protocol implementation validation
```

---

**Document Version**: 1.0  
**Last Updated**: 2026-01-24  
**Sharpy Version**: 0.1.0
