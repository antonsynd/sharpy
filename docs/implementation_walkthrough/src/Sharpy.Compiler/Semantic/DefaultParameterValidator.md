# Walkthrough: DefaultParameterValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs`

---

## 1. Overview

The `DefaultParameterValidator` is a focused semantic analysis component that validates default parameter values in function definitions. Its role is to catch common programming mistakes that stem from Python's infamous "mutable default argument" footgun and to ensure type safety when using `None` as a default value.

**Primary Responsibilities:**
- **Mutable Default Detection**: Prevents dangerous patterns like `def foo(items: list = [])` where the mutable default is shared across all calls
- **Compile-Time Constant Enforcement**: Ensures default values can be evaluated at compile time, not runtime
- **Nullable Type Checking**: Validates that `None` is only used as a default for parameters with nullable types

**Where It Fits:**
In the compiler pipeline, this validator runs during semantic analysis, typically after type resolution:
```
Lexer → Parser → NameResolver → TypeResolver → DefaultParameterValidator → TypeChecker → RoslynEmitter
```

**Why This Matters:**
In Python, this code creates a subtle bug:
```python
def append_to(element, to=[]):  # BUG: mutable default!
    to.append(element)
    return to

append_to(1)  # Returns [1]
append_to(2)  # Returns [1, 2] - the same list!
```

Sharpy catches this at compile time rather than letting it become a runtime surprise.

---

## 2. Class Structure

### Main Class: `DefaultParameterValidator`

```csharp
public class DefaultParameterValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly TypeResolver _typeResolver;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    public IReadOnlyList<SemanticError> Errors => _errors;
}
```

**Dependencies:**
- `_symbolTable`: Access to symbol information (currently unused but available for future enhancements like const variable lookup)
- `_typeResolver`: Used to resolve type annotations when checking `None` defaults against nullable types
- `_logger`: For debug/error output during validation
- `_errors`: Accumulated validation errors exposed via the `Errors` property

**Design Note:** The validator follows the error-accumulation pattern common in Sharpy's semantic analysis. Errors are collected in a list rather than thrown immediately, allowing the validator to report multiple issues in a single pass.

---

## 3. Key Methods

### 3.1 `ValidateFunctionDefaults` - Entry Point

```csharp
public void ValidateFunctionDefaults(FunctionDef functionDef)
{
    foreach (var param in functionDef.Parameters)
    {
        if (param.DefaultValue != null)
        {
            ValidateDefaultValue(param, functionDef.Name);
        }
    }
}
```

**Purpose:** Main entry point that validates all default parameter values in a function definition.

**Parameters:**
- `functionDef`: The AST node representing the function to validate

**What It Does:**
1. Iterates through all parameters in the function definition
2. For each parameter with a default value, delegates to `ValidateDefaultValue`

**Usage Context:**
This method is called by the semantic analyzer when processing function definitions:
```csharp
// In the semantic analyzer
var validator = new DefaultParameterValidator(symbolTable, typeResolver, logger);
validator.ValidateFunctionDefaults(functionDef);
if (validator.Errors.Any())
{
    // Handle validation errors
}
```

---

### 3.2 `ValidateDefaultValue` - Core Validation Logic

```csharp
private void ValidateDefaultValue(Parameter param, string functionName)
```

**Purpose:** Performs the three-stage validation on a single parameter's default value.

**Parameters:**
- `param`: The parameter with a default value to validate
- `functionName`: Used for error message context

**Validation Stages (in order):**

1. **Mutable Default Check** (highest priority)
   ```csharp
   if (IsMutableDefault(defaultValue))
   {
       AddError("Mutable default value is not allowed...");
       return;  // Early exit - no further checks needed
   }
   ```
   Mutable defaults are rejected immediately because they represent a fundamental design problem, not a type error.

2. **Compile-Time Constant Check**
   ```csharp
   if (!IsCompileTimeConstant(defaultValue))
   {
       AddError("Default value must be a compile-time constant expression");
       return;
   }
   ```
   This ensures defaults can be evaluated during compilation.

3. **Nullable Type Check for None**
   ```csharp
   if (defaultValue is NoneLiteral)
   {
       var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
       if (paramType is not NullableType && paramType is not UnknownType)
       {
           AddError("Cannot use 'None' as default value for non-nullable parameter...");
       }
   }
   ```
   This ensures type safety when using `None` as a default.

**Early Exit Pattern:**
Notice how the method uses early returns after the first two checks. This is intentional:
- A mutable default is fundamentally wrong, regardless of whether it's also a constant
- A non-constant default is wrong, regardless of its nullability

---

### 3.3 `IsMutableDefault` - Mutable Collection Detection

```csharp
private static bool IsMutableDefault(Expression expr)
{
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
}
```

**Purpose:** Identifies expressions that create mutable objects.

**Detected Patterns:**
| Pattern | AST Type | Example |
|---------|----------|---------|
| List literal | `ListLiteral` | `[]`, `[1, 2, 3]` |
| Dict literal | `DictLiteral` | `{}`, `{"a": 1}` |
| Set literal | `SetLiteral` | `{1, 2, 3}` |
| Set constructor | `FunctionCall` | `set()`, `set([1,2])` |
| List constructor | `FunctionCall` | `list()`, `list("abc")` |
| Dict constructor | `FunctionCall` | `dict()`, `dict(a=1)` |
| Parenthesized | `Parenthesized` | `([])` (unwraps) |

**Key Implementation Detail:**
The function call detection uses pattern matching to identify specific built-in constructors:
```csharp
FunctionCall call when call.Function is Identifier id && id.Name == "set" => true
```

This only catches direct calls like `set()`, not aliased calls like:
```python
my_set = set
def foo(s=my_set()):  # Not caught by IsMutableDefault
    pass
```

However, this would be caught by `IsCompileTimeConstant` since function calls are not constants.

**Design Decision:** The method is `static` because it performs pure syntactic analysis on the AST with no dependency on instance state.

---

### 3.4 `IsCompileTimeConstant` - Constant Expression Analysis

```csharp
private static bool IsCompileTimeConstant(Expression expr)
{
    return expr switch
    {
        // Primitive literals
        IntegerLiteral => true,
        FloatLiteral => true,
        StringLiteral => true,
        BooleanLiteral => true,
        NoneLiteral => true,

        // Composite constants
        TupleLiteral tuple => tuple.Elements.All(IsCompileTimeConstant),

        // Operations on constants
        UnaryOp unary => IsCompileTimeConstant(unary.Operand),
        BinaryOp binary => IsCompileTimeConstant(binary.Left) &&
                          IsCompileTimeConstant(binary.Right),
        Parenthesized paren => IsCompileTimeConstant(paren.Expression),
        ConditionalExpression cond => IsCompileTimeConstant(cond.Test) &&
                                      IsCompileTimeConstant(cond.ThenValue) &&
                                      IsCompileTimeConstant(cond.ElseValue),

        // Non-constants
        Identifier => false,          // Variables
        FunctionCall => false,        // Runtime evaluation
        MemberAccess => false,        // Attribute lookup
        IndexAccess => false,         // Subscript operation
        ListLiteral => false,         // Mutable
        DictLiteral => false,         // Mutable
        SetLiteral => false,          // Mutable
        ListComprehension => false,   // Runtime iteration
        SetComprehension => false,    // Runtime iteration
        DictComprehension => false,   // Runtime iteration
        LambdaExpression => false,    // Creates new function object

        _ => false  // Conservative default
    };
}
```

**Purpose:** Determines whether an expression can be evaluated at compile time.

**What Qualifies as Compile-Time Constants:**

| Category | Examples | Why |
|----------|----------|-----|
| Primitive literals | `42`, `3.14`, `"hello"`, `True`, `None` | Immutable, known at compile time |
| Tuple of constants | `(1, 2, 3)`, `("a", "b")` | Tuples are immutable in Python |
| Unary on constant | `-1`, `+5`, `not True` | Operators on constants yield constants |
| Binary on constants | `1 + 2`, `"a" + "b"` | Operators on constants yield constants |
| Ternary on constants | `1 if True else 2` | All parts are constant |

**What Does NOT Qualify:**

| Category | Examples | Why |
|----------|----------|-----|
| Identifiers | `x`, `MY_CONST` | Requires symbol table lookup |
| Function calls | `len([])`, `int("42")` | Runtime evaluation needed |
| Member access | `obj.attr`, `math.pi` | Requires runtime lookup |
| Index access | `arr[0]`, `d["key"]` | Runtime subscript operation |
| Mutable collections | `[]`, `{}`, `{1,2}` | Creates new mutable object each time |
| Comprehensions | `[x for x in y]` | Requires runtime iteration |
| Lambdas | `lambda x: x + 1` | Creates new function object |

**Code Comment on Identifiers:**
```csharp
// Identifiers are NOT compile-time constants (they reference variables)
// Exception could be made for const variables, but that requires symbol table lookup
Identifier => false,
```

This is a conservative choice. A future enhancement could use the `_symbolTable` to look up `const` declarations and allow those as compile-time constants.

---

### 3.5 `AddError` - Error Recording

```csharp
private void AddError(string message, int? line = null, int? column = null)
{
    var error = new SemanticError(message, line, column);
    _errors.Add(error);
    _logger.LogError(error.Message, line ?? 0, column ?? 0);
}
```

**Purpose:** Helper method to record validation errors with source location information.

**Parameters:**
- `message`: Human-readable error description
- `line`: Optional line number from AST node
- `column`: Optional column number from AST node

**Dual Reporting:**
The method both:
1. Adds the error to the `_errors` list for programmatic access
2. Logs it via `_logger` for immediate output/debugging

---

## 4. Dependencies

### Internal Dependencies

**AST Nodes** (`Sharpy.Compiler.Parser.Ast`):
- `FunctionDef`: Function definition containing parameters
- `Parameter`: Individual function parameter with optional default value
- Expression types: `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`, `NoneLiteral`, `TupleLiteral`, `ListLiteral`, `DictLiteral`, `SetLiteral`, `UnaryOp`, `BinaryOp`, `Parenthesized`, `ConditionalExpression`, `Identifier`, `FunctionCall`, `MemberAccess`, `IndexAccess`, `ListComprehension`, `SetComprehension`, `DictComprehension`, `LambdaExpression`

**Semantic Types** (`Sharpy.Compiler.Semantic`):
- `SymbolTable`: Symbol information storage
- `TypeResolver`: Resolves type annotations to `SemanticType`
- `SemanticError`: Error representation
- `NullableType`: Represents optional/nullable types
- `UnknownType`: Represents unresolved types

**Logging** (`Sharpy.Compiler.Logging`):
- `ICompilerLogger`: Logging interface
- `NullLogger`: No-op logger for when logging isn't needed

### External Dependencies
- Standard .NET collections (`List<T>`, `IReadOnlyList<T>`)
- LINQ (`All` extension method)

---

## 5. Design Patterns & Decisions

### 5.1 Error Accumulation Pattern

Like other validators in Sharpy, errors are collected rather than thrown:
```csharp
private readonly List<SemanticError> _errors = new();
public IReadOnlyList<SemanticError> Errors => _errors;
```

**Benefits:**
- Multiple errors can be reported in a single pass
- Caller decides how to handle errors
- Useful for IDE tooling that wants to show all issues at once

### 5.2 Prioritized Validation

The three validation checks run in a specific order with early exits:
```
Mutable Default → Compile-Time Constant → Nullable Type
```

**Why This Order:**
1. **Mutable defaults are always wrong** - No point checking if `[]` is a constant
2. **Non-constants are always wrong** - No point checking nullability if it's not allowed anyway
3. **Nullable checking is conditional** - Only applies to `None` literals

### 5.3 Static Helper Methods

Both `IsMutableDefault` and `IsCompileTimeConstant` are `static`:
```csharp
private static bool IsMutableDefault(Expression expr)
private static bool IsCompileTimeConstant(Expression expr)
```

**Rationale:**
- Pure functions that only examine AST structure
- No instance state needed
- Can be easily tested in isolation
- Clearly communicates that no side effects occur

### 5.4 Switch Expression Pattern Matching

The validators use C# switch expressions with pattern matching for concise AST traversal:
```csharp
return expr switch
{
    IntegerLiteral => true,
    TupleLiteral tuple => tuple.Elements.All(IsCompileTimeConstant),
    FunctionCall call when call.Function is Identifier id && id.Name == "set" => true,
    _ => false
};
```

**Benefits:**
- Exhaustive case handling (compiler warns about missing cases)
- Type narrowing (e.g., `tuple` is already typed as `TupleLiteral`)
- Guard clauses (`when`) for complex conditions
- Concise compared to if-else chains

### 5.5 Conservative Defaults

The validators are conservative in what they accept:
- `Identifier => false` - Even if the identifier refers to a constant
- `_ => false` - Unknown expression types are rejected

**Philosophy:** It's better to reject valid edge cases than to accept invalid ones. Users can always restructure their code to pass validation.

---

## 6. Debugging Tips

### 6.1 Common Issues

**Issue: "Mutable default" error for seemingly valid code**
- Check if the default is a collection literal, even if empty
- Look for parenthesized expressions wrapping mutable values: `([])` is still mutable
- Check for `set()`, `list()`, or `dict()` constructor calls

**Issue: "Not a compile-time constant" error**
- Verify the default doesn't reference any variables
- Check for hidden function calls
- Look for member access (e.g., `enum.Value`)
- Ensure comprehensions aren't being used

**Issue: "Cannot use None for non-nullable parameter" error**
- Add `?` to the type annotation: `str?` instead of `str`
- Or use `Optional[str]` syntax if supported

### 6.2 Testing Strategy

When testing default parameter validation:

1. **Test each mutable type:**
   ```python
   def test_list(items: list = []):  # Should error
   def test_dict(data: dict = {}):   # Should error
   def test_set(values: set = set()): # Should error
   ```

2. **Test valid constants:**
   ```python
   def test_int(x: int = 42):           # Valid
   def test_tuple(t: tuple = (1, 2)):   # Valid
   def test_expr(n: int = 1 + 2):       # Valid
   def test_unary(n: int = -1):         # Valid
   ```

3. **Test None with types:**
   ```python
   def test_none_ok(x: str? = None):    # Valid
   def test_none_bad(x: str = None):    # Should error
   ```

4. **Test edge cases:**
   ```python
   def test_nested(t: tuple = ((1, 2), (3, 4))):  # Valid - nested tuples
   def test_paren(x: int = (42)):                  # Valid - just parenthesized
   def test_paren_mut(x: list = ([])):             # Should error - parenthesized mutable
   ```

### 6.3 Debugging Walkthrough

If you encounter unexpected behavior:

1. **Check the AST:**
   Use the `AstDumper` to see the exact AST structure:
   ```csharp
   Console.WriteLine(AstDumper.Dump(param.DefaultValue));
   ```

2. **Trace method calls:**
   ```csharp
   private static bool IsMutableDefault(Expression expr)
   {
       Console.WriteLine($"IsMutableDefault checking: {expr.GetType().Name}");
       var result = expr switch { /* ... */ };
       Console.WriteLine($"  Result: {result}");
       return result;
   }
   ```

3. **Verify type resolution:**
   ```csharp
   var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
   Console.WriteLine($"Parameter type: {paramType.GetType().Name} - {paramType.GetDisplayName()}");
   Console.WriteLine($"Is nullable: {paramType is NullableType}");
   ```

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add New Mutable Type Detection:**
If Sharpy adds new mutable types (e.g., `deque`, `Counter`), add them to `IsMutableDefault`:
```csharp
FunctionCall call when call.Function is Identifier id && id.Name == "deque" => true,
```

**Add New Constant Expression Types:**
If new literal types are added to the AST, update `IsCompileTimeConstant`:
```csharp
BytesLiteral => true,  // If bytes literals are added
FrozenSetLiteral frozenSet => frozenSet.Elements.All(IsCompileTimeConstant),  // Immutable set
```

**Support Const Variable References:**
To allow references to `const` declarations:
```csharp
Identifier id => _symbolTable.TryLookup(id.Name, out var symbol)
                 && symbol.Kind == SymbolKind.Const,
```
Note: This would require making the method non-static and passing the symbol table.

**Add Warning-Level Diagnostics:**
For patterns that are technically valid but suspicious:
```csharp
// Warn about complex constant expressions
if (expr is BinaryOp binary && IsCompileTimeConstant(expr))
{
    _logger.LogWarning("Consider simplifying complex default value expression", ...);
}
```

### 7.2 Testing Requirements

All changes **must** include:
1. **Positive tests**: Valid defaults that should pass
2. **Negative tests**: Invalid defaults that should error with correct messages
3. **Boundary tests**: Edge cases at the boundary of validity

Test location: `src/Sharpy.Compiler.Tests/Semantic/DefaultParameterValidatorTests.cs`

### 7.3 Error Message Quality

When adding or modifying error messages:

**Good:**
```csharp
AddError(
    $"Mutable default value is not allowed for parameter '{param.Name}' in function '{functionName}'. " +
    "Use None as default and initialize in the function body instead.",
    param.LineStart,
    param.ColumnStart);
```

**Why it's good:**
- Identifies the specific parameter and function
- Explains the restriction
- Provides actionable guidance

**Bad:**
```csharp
AddError("Invalid default value", null, null);
```

### 7.4 Maintaining Python Compatibility

Sharpy's default parameter validation is **stricter** than Python because Sharpy catches these issues at compile time. However, the philosophy should be:
- Reject patterns that would cause bugs in Python
- Don't reject patterns that are valid and safe in Python

Example: Python allows `def foo(x=1+2)` - this is fine in Sharpy because the expression is constant.

### 7.5 Common Contribution Scenarios

**Scenario 1: Add FrozenSet Support**
```csharp
// In IsMutableDefault - frozenset is immutable, so NOT mutable
FunctionCall call when call.Function is Identifier id && id.Name == "frozenset" => false,

// In IsCompileTimeConstant - but it requires runtime evaluation
FunctionCall call when call.Function is Identifier id && id.Name == "frozenset" => false,
// Still false because it's a function call, even if result is immutable
```

**Scenario 2: Support Module-Level Constants**
```csharp
// This would require architecture changes
public class DefaultParameterValidator
{
    // Need to track const declarations
    private readonly Dictionary<string, object> _constants;

    // IsCompileTimeConstant becomes non-static
    private bool IsCompileTimeConstant(Expression expr)
    {
        return expr switch
        {
            Identifier id when _constants.ContainsKey(id.Name) => true,
            // ... rest unchanged
        };
    }
}
```

---

## 8. Related Components

### Upstream (Inputs)
- **Parser**: Produces `FunctionDef` and `Parameter` AST nodes
- **TypeResolver**: Provides `ResolveTypeAnnotation` for nullable type checking

### Downstream (Outputs)
- **TypeChecker**: May use this validator's results; runs after default validation
- **RoslynEmitter**: Generates C# code knowing defaults are safe

### Sibling Validators in Semantic Analysis
- **ControlFlowValidator**: Validates return paths and unreachable code
- **ProtocolValidator**: Validates protocol implementations
- **AccessValidator**: Checks access modifiers and visibility
- **OperatorValidator**: Validates operator usage

---

## 9. Future Enhancements

### 9.1 Const Variable Support
Allow references to `const` declarations as default values:
```python
MAX_SIZE: const int = 100

def create_buffer(size: int = MAX_SIZE):  # Should be valid
    pass
```

### 9.2 Frozen Collection Support
If Sharpy adds immutable collection types:
```python
def process(items: frozenset = frozenset([1, 2, 3])):  # Should be valid
    pass
```

### 9.3 Suggested Fixes
Provide automated fix suggestions:
```
Error: Mutable default value is not allowed for parameter 'items' in function 'process'.
Suggestion: Change to:
    def process(items: list? = None):
        if items is None:
            items = []
```

### 9.4 Type-Based Default Validation
Ensure the default value is assignable to the parameter type:
```python
def foo(x: int = "hello"):  # Currently caught by TypeChecker, could be here
    pass
```

---

## Summary

The `DefaultParameterValidator` is a focused, defensive component that prevents a common class of Python bugs at compile time. Its design prioritizes:

- **Safety**: Catches mutable default arguments before runtime
- **Clarity**: Clear error messages with actionable guidance
- **Simplicity**: Straightforward validation logic using pattern matching
- **Conservatism**: Rejects uncertain cases rather than accepting potentially problematic code

When working with this validator, remember the core principle: **default values should be immutable, compile-time-evaluable constants**. This restriction prevents subtle bugs while allowing the common use cases of default integers, strings, booleans, None, and tuples.
