# Walkthrough: DefaultParameterValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs`

---

## Overview

The `DefaultParameterValidator` is a semantic analysis component responsible for validating default parameter values in function definitions. It enforces Python-like rules about default parameters while integrating with C#'s type system, ensuring that:

1. **Default values are compile-time constants** (no runtime expressions)
2. **Mutable defaults are forbidden** (prevents the classic Python pitfall of shared mutable state)
3. **`None` defaults match nullable types** (type safety for null values)

This validator runs during the semantic analysis phase, after the AST has been constructed but before code generation. It's a focused, single-responsibility component that catches common programming errors early.

### ⚠️ Deprecation Notice

**This class is marked as `[Obsolete]`** and has been replaced by `DefaultParameterValidatorV2` which implements the `ISemanticValidator` interface for the new validation pipeline architecture.

```csharp
[Obsolete("Use DefaultParameterValidatorV2 via ValidationPipelineFactory.CreateDefault() instead")]
public class DefaultParameterValidator
```

- **Old pattern** (deprecated): Direct instantiation of `DefaultParameterValidator`
- **New pattern**: Use `ValidationPipelineFactory.CreateDefault()` which includes `DefaultParameterValidatorV2`
- **Migration guide**: See [Validation Pipeline README](./Validation/README.md)
- **Related file**: [`Validation/DefaultParameterValidatorV2.cs`](./Validation/DefaultParameterValidatorV2.md)

**Pipeline Position**: Parser (AST) → **Semantic Analysis** (Name Resolution → Type Resolution → **Default Parameter Validation**) → CodeGen (RoslynEmitter)

**Why This Matters**: In Python, this code creates a subtle bug:
```python
def append_to(element, to=[]):  # BUG: mutable default!
    to.append(element)
    return to

append_to(1)  # Returns [1]
append_to(2)  # Returns [1, 2] - the same list!
```

Sharpy catches this at compile time rather than letting it become a runtime surprise.

---

## Class/Type Structure

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

**Dependencies**:
- `SymbolTable`: Provides symbol lookup for checking const references and enum types
- `TypeResolver`: Resolves type annotations to check nullable types
- `ICompilerLogger`: Optional logging for diagnostic output
- `_errors`: Accumulates validation errors for later reporting

**Design Pattern**: This class follows the **Validator pattern** - it validates AST nodes and accumulates errors without throwing exceptions, allowing the compiler to continue and report multiple errors at once.

---

## Key Functions/Methods

### 1. Constructor

```csharp
public DefaultParameterValidator(
    SymbolTable symbolTable,
    TypeResolver typeResolver,
    ICompilerLogger? logger = null)
{
    _symbolTable = symbolTable;
    _typeResolver = typeResolver;
    _logger = logger ?? NullLogger.Instance;
}
```

**Purpose**: Initializes the validator with required dependencies.

**Parameters**:
- `symbolTable`: Needed to look up const references and enum types (lines 197-220)
- `typeResolver`: Needed to check if parameter types are nullable (line 76)
- `logger`: Optional logger (defaults to `NullLogger.Instance` if null)

**Design Note**: The nullable logger parameter with a default provides flexibility - tests can omit it, while production code can supply one.

---

### 2. `ValidateFunctionDefaults(FunctionDef functionDef)` - Entry Point

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

**Purpose**: Entry point for validating all default parameters in a function.

**How It Works**:
- Iterates through all parameters in the function definition
- Delegates validation of each default value to `ValidateDefaultValue`
- Passes the function name for better error messages

**Usage in Pipeline**: Called by higher-level semantic analyzers (e.g., `SemanticAnalyzer`) when processing function definitions:

```csharp
var validator = new DefaultParameterValidator(symbolTable, typeResolver, logger);
validator.ValidateFunctionDefaults(functionDef);
if (validator.Errors.Any())
{
    // Report errors to user
}
```

---

### 3. `ValidateDefaultValue(Parameter param, string functionName)` - Core Validation Logic

```csharp
private void ValidateDefaultValue(Parameter param, string functionName)
```

**Purpose**: Validates a single parameter's default value through three key checks.

**Validation Steps** (in order of execution):

#### Step 1: Check for Mutable Defaults (Lines 53-61)
```csharp
if (IsMutableDefault(defaultValue))
{
    AddError(
        $"Mutable default value is not allowed for parameter '{param.Name}' in function '{functionName}'. " +
        "Use None as default and initialize in the function body instead.",
        param.LineStart,
        param.ColumnStart);
    return;  // Early exit
}
```

**Why This Matters**: In Python, mutable defaults are shared across function calls:
```python
# Bad - shared mutable default
def add_item(item, items=[]):  # [] is created once
    items.append(item)
    return items

# Every call shares the same list!
add_item(1)  # [1]
add_item(2)  # [1, 2] - Oops!
```

Sharpy prevents this entirely by forbidding `[]`, `{}`, `set()`, etc. as defaults.

**Early Exit Pattern**: If a mutable default is detected, the method returns immediately. No need to check if it's also a non-constant - it's fundamentally wrong.

#### Step 2: Check for Compile-Time Constants (Lines 64-71)
```csharp
if (!IsCompileTimeConstant(defaultValue))
{
    AddError(
        $"Default value for parameter '{param.Name}' in function '{functionName}' must be a compile-time constant expression",
        param.LineStart,
        param.ColumnStart);
    return;  // Early exit
}
```

**Why This Matters**: Default values must be known at compile time for C# code generation. This excludes:
- Function calls (except const references)
- Variable references (except const variables)
- Runtime computations

**Example Valid Constants**:
```python
def foo(x: int = 42):              # ✓ Literal
def bar(y: int = 10 * 1024):       # ✓ Arithmetic on constants
def baz(z: tuple = (1, 2, 3)):     # ✓ Tuple of constants
```

**Example Invalid Expressions**:
```python
x = 42
def foo(y: int = x):               # ✗ Variable reference
def bar(z: int = len("hello")):    # ✗ Function call
```

#### Step 3: Validate `None` for Nullable Types (Lines 74-87)
```csharp
if (defaultValue is NoneLiteral)
{
    var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);

    if (paramType is not NullableType && paramType is not UnknownType)
    {
        AddError(
            $"Cannot use 'None' as default value for non-nullable parameter '{param.Name}' of type '{paramType.GetDisplayName()}' in function '{functionName}'. " +
            $"Use '{paramType.GetDisplayName()}?' to make the parameter nullable.",
            param.LineStart,
            param.ColumnStart);
    }
}
```

**Why This Matters**: Prevents null assignment to non-nullable types:
```python
# Bad - None for non-nullable type
def process(value: int = None):  # ✗ Error!
    pass

# Good - nullable type
def process(value: int? = None):  # ✓ OK
    pass
```

**Design Note**: The check includes `UnknownType` to avoid spurious errors during type inference. If the type hasn't been resolved yet, we don't report an error - the type checker will handle it later.

**Error Message Quality**: Note how the error message:
1. Identifies the specific parameter (`'value'`)
2. Shows the actual type (`'int'`)
3. Provides the exact fix needed (`'int?'`)

---

### 4. `IsMutableDefault(Expression expr)` - Mutable Detection

```csharp
private static bool IsMutableDefault(Expression expr)
{
    return expr switch
    {
        ListLiteral => true,              // [], [1, 2, 3]
        DictLiteral => true,              // {}, {"key": "value"}
        SetLiteral => true,               // {1, 2, 3}
        FunctionCall call when call.Function is Identifier id && id.Name == "set" => true,
        FunctionCall call when call.Function is Identifier id && id.Name == "list" => true,
        FunctionCall call when call.Function is Identifier id && id.Name == "dict" => true,
        Parenthesized paren => IsMutableDefault(paren.Expression),
        _ => false
    };
}
```

**Purpose**: Detects expressions that create mutable collections.

**Detected Patterns**:

| Pattern | AST Type | Example | Why Mutable |
|---------|----------|---------|-------------|
| List literal | `ListLiteral` | `[]`, `[1, 2, 3]` | Lists are mutable |
| Dict literal | `DictLiteral` | `{}`, `{"a": 1}` | Dicts are mutable |
| Set literal | `SetLiteral` | `{1, 2, 3}` | Sets are mutable |
| `set()` call | `FunctionCall` | `set()`, `set([1,2])` | Creates mutable set |
| `list()` call | `FunctionCall` | `list()`, `list("abc")` | Creates mutable list |
| `dict()` call | `FunctionCall` | `dict()`, `dict(a=1)` | Creates mutable dict |
| Parenthesized | `Parenthesized` | `([])` | Recursively unwraps |

**Key Implementation Details**:

1. **Constructor Detection** (Lines 109-115): Uses pattern matching to identify specific built-in constructors:
   ```csharp
   FunctionCall call when call.Function is Identifier id && id.Name == "set" => true
   ```
   This pattern checks:
   - Is it a function call?
   - Is the function an identifier (not `obj.method()`)?
   - Is the identifier name `"set"`?

2. **Parenthesized Unwrapping** (Line 118): Recursively checks the inner expression:
   ```python
   def foo(x = ([])):  # Still caught - parentheses don't change mutability
   ```

3. **Aliasing Limitation**: This detection only catches direct calls:
   ```python
   my_list = list
   def foo(x = my_list()):  # NOT caught by IsMutableDefault
       pass
   ```
   However, this would still be caught by `IsCompileTimeConstant` since function calls aren't constants.

**Why It's Static**: This is a pure function with no dependencies on instance state, so it's marked `static` for clarity and potential performance benefits.

**Pattern Matching**: Uses C# 9+ switch expressions with pattern matching for clean, exhaustive case handling.

---

### 5. `IsCompileTimeConstant(Expression expr)` - Constant Detection

```csharp
private bool IsCompileTimeConstant(Expression expr)
{
    return expr switch
    {
        IntegerLiteral => true,
        FloatLiteral => true,
        StringLiteral => true,
        BooleanLiteral => true,
        NoneLiteral => true,
        TupleLiteral tuple => tuple.Elements.All(IsCompileTimeConstant),
        UnaryOp unary => IsCompileTimeConstant(unary.Operand),
        BinaryOp binary => IsCompileTimeConstant(binary.Left) && IsCompileTimeConstant(binary.Right),
        Parenthesized paren => IsCompileTimeConstant(paren.Expression),
        ConditionalExpression cond => /* all parts constant */,
        Identifier id => IsConstReference(id),
        MemberAccess memberAccess => IsEnumMemberAccess(memberAccess),
        // ... many explicitly forbidden cases
        _ => false
    };
}
```

**Purpose**: Determines if an expression can be evaluated at compile time.

**Allowed Compile-Time Constants**:

1. **Primitive Literals** (Lines 139-143):
   ```python
   def foo(x: int = 42):          # ✓ Integer literal
   def bar(y: float = 3.14):      # ✓ Float literal
   def baz(s: str = "hello"):     # ✓ String literal
   def qux(b: bool = True):       # ✓ Boolean literal
   def quux(n: int? = None):      # ✓ None literal
   ```

2. **Tuples of Constants** (Line 146):
   ```python
   def foo(point: tuple[int, int] = (0, 0)):  # ✓ Tuple of constants
   def bar(nested: tuple = ((1, 2), (3, 4))): # ✓ Nested tuples
   ```
   Uses `.All()` to recursively validate all elements.

3. **Arithmetic on Constants** (Lines 149-152):
   ```python
   def foo(size: int = 10 * 1024):      # ✓ Binary operation
   def bar(value: int = -1):            # ✓ Unary negation
   def baz(positive: int = +42):        # ✓ Unary plus
   def qux(inverted: bool = not True):  # ✓ Logical negation
   ```

4. **Parenthesized Expressions** (Line 155):
   ```python
   def foo(x: int = (42)):              # ✓ Just removes parentheses
   def bar(y: int = ((1 + 2))):         # ✓ Nested parentheses
   ```

5. **Conditional Expressions** (Lines 158-161):
   ```python
   def foo(val: int = 5 if True else 10):  # ✓ All parts are constant
   ```
   All three parts (test, then-value, else-value) must be constants.

6. **Const References** (Line 164):
   ```python
   const MAX_SIZE = 100
   def foo(size: int = MAX_SIZE):  # ✓ References const declaration
   ```
   Delegates to `IsConstReference()` for symbol table lookup.

7. **Enum Members** (Line 171):
   ```python
   enum HttpMethod:
       GET = 1
       POST = 2

   def request(method: HttpMethod = HttpMethod.GET):  # ✓ Enum member
   ```
   Delegates to `IsEnumMemberAccess()` for enum detection.

**Explicitly Forbidden** (with rationale):

| Expression Type | Lines | Why Forbidden | Example |
|----------------|-------|---------------|---------|
| Function calls | 168 | Runtime evaluation | `len([])`, `int("42")` |
| Index access | 174 | Runtime subscript | `arr[0]`, `d["key"]` |
| Mutable collections | 177-179 | Creates new object | `[]`, `{}`, `{1,2}` |
| Comprehensions | 182-184 | Runtime iteration | `[x for x in y]` |
| Lambdas | 187 | Creates function object | `lambda x: x + 1` |

**Default Case** (Line 190): Returns `false` for unknown expression types - conservative approach ensures new AST node types are explicitly considered.

**Recursive Validation**: Several cases use recursion to validate nested structures:
- Tuples: Check all elements
- Unary ops: Check operand
- Binary ops: Check both operands
- Parenthesized: Check inner expression
- Conditionals: Check all three parts

---

### 6. `IsConstReference(Identifier id)` - Symbol Table Lookup

```csharp
private bool IsConstReference(Identifier id)
{
    var symbol = _symbolTable.Lookup(id.Name);
    return symbol is VariableSymbol { IsConstant: true };
}
```

**Purpose**: Checks if an identifier references a const declaration.

**How It Works**:
1. Looks up the identifier in the symbol table
2. Uses C# pattern matching to check if it's a `VariableSymbol` with `IsConstant = true`

**Example**:
```python
const DEFAULT_PORT = 8080
def connect(port: int = DEFAULT_PORT):  # ✓ Valid - const reference
    pass

regular_var = 9000
def connect2(port: int = regular_var):  # ✗ Error - not const
    pass
```

**Symbol Table Integration**: This is where the validator depends on the `SymbolTable` being properly populated during earlier semantic analysis phases. The symbol table must have:
- Registered all const declarations
- Marked them with `IsConstant = true`

**Null Safety**: If the symbol isn't found, `Lookup` returns `null`, and the pattern match fails, returning `false`.

---

### 7. `IsEnumMemberAccess(MemberAccess memberAccess)` - Enum Detection

```csharp
private bool IsEnumMemberAccess(MemberAccess memberAccess)
{
    // The object must be an identifier (the enum type name)
    if (memberAccess.Object is not Identifier typeId)
    {
        return false;
    }

    // Look up the type in the symbol table
    var symbol = _symbolTable.Lookup(typeId.Name);

    // Check if it's an enum type
    return symbol is TypeSymbol { TypeKind: TypeKind.Enum };
}
```

**Purpose**: Validates that a member access like `Color.RED` refers to an enum member.

**How It Works**:

1. **Structural Check** (Lines 210-213): Verifies the object part is an identifier
   ```python
   Color.RED        # ✓ Object is identifier 'Color'
   obj.method.RED   # ✗ Object is MemberAccess, not Identifier
   ```

2. **Symbol Lookup** (Line 216): Looks up the identifier in the symbol table
   ```python
   # Symbol table should contain:
   # "Color" -> TypeSymbol { TypeKind = TypeKind.Enum }
   ```

3. **Type Check** (Line 219): Verifies it's a `TypeSymbol` with `TypeKind.Enum`

**Why This Pattern**: The validator doesn't check if the member name (e.g., `RED`) is valid - that's the type checker's job. This validator only ensures the pattern `EnumType.Member` is structurally correct for compile-time constants.

**Example**:
```python
enum Color:
    RED = 1
    GREEN = 2

def draw(color: Color = Color.RED):     # ✓ Valid enum member
    pass

def bad(color: Color = Color.BLUE):     # TypeChecker will catch this
    pass                                 # (BLUE doesn't exist)
```

**Scope Limitation**: This only handles simple enum member access. Qualified names like `MyModule.Color.RED` would require additional logic to resolve the module path.

---

### 8. `AddError(string message, int? line, int? column)` - Error Reporting

```csharp
private void AddError(string message, int? line = null, int? column = null)
{
    var error = new SemanticError(message, line, column);
    _errors.Add(error);
    _logger.LogError(error.Message, line ?? 0, column ?? 0);
}
```

**Purpose**: Centralizes error creation and logging.

**Dual Reporting**:
1. **Adds to `_errors` list**: Allows batch retrieval via the `Errors` property
2. **Logs immediately via `_logger`**: Provides real-time diagnostics during compilation

**Nullable Parameters**: Line/column are optional because some errors may not have precise location info (though in practice, parameter nodes always have location data).

**Null Coalescing** (Line 226): `line ?? 0` ensures the logger receives a valid line number even if null is passed.

---

## Dependencies

### Internal Sharpy Dependencies

1. **`SymbolTable`** (from `Sharpy.Compiler.Semantic`):
   - Provides `Lookup(string name)` for symbol resolution
   - Used to check if identifiers are const references (line 199)
   - Used to validate enum types (line 216)
   - Must be populated before validation runs

2. **`TypeResolver`** (from `Sharpy.Compiler.Semantic`):
   - Provides `ResolveTypeAnnotation(TypeAnnotation)` for type resolution
   - Used to check if parameter types are nullable (line 76)
   - Returns `SemanticType` instances like `NullableType`, `UnknownType`

3. **AST Types** (from `Sharpy.Compiler.Parser.Ast`):
   - `FunctionDef`: Function definition containing parameters
   - `Parameter`: Individual function parameter with optional default value
   - Expression hierarchy: `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`, `NoneLiteral`, `TupleLiteral`, `ListLiteral`, `DictLiteral`, `SetLiteral`, `UnaryOp`, `BinaryOp`, `Parenthesized`, `ConditionalExpression`, `Identifier`, `FunctionCall`, `MemberAccess`, `IndexAccess`, `ListComprehension`, `SetComprehension`, `DictComprehension`, `LambdaExpression`

4. **`ICompilerLogger`** (from `Sharpy.Compiler.Logging`):
   - Interface for logging diagnostic messages
   - `NullLogger.Instance` provides a no-op implementation for tests

5. **`SemanticError`** (from `Sharpy.Compiler.Semantic`):
   - Represents a semantic validation error with location information
   - Contains message, line, and column

6. **Semantic Types** (from `Sharpy.Compiler.Semantic`):
   - `NullableType`: Represents optional/nullable types (e.g., `int?`)
   - `UnknownType`: Represents types not yet resolved during multi-pass analysis
   - `VariableSymbol`: Symbol table entry for variables (includes `IsConstant` flag)
   - `TypeSymbol`: Symbol table entry for types (includes `TypeKind` enum)

### External Dependencies
- Standard .NET collections: `List<T>`, `IReadOnlyList<T>`
- LINQ: `All()` extension method for validating tuple elements

---

## Patterns and Design Decisions

### 1. **Error Accumulation Pattern**

The validator collects errors rather than throwing exceptions:

```csharp
private readonly List<SemanticError> _errors = new();
public IReadOnlyList<SemanticError> Errors => _errors;
```

**Benefits**:
- Multiple errors can be reported in a single pass
- Compiler can continue analyzing other code
- Better user experience (see all errors, not just the first one)
- Caller decides how to handle errors

**Usage Pattern**:
```csharp
var validator = new DefaultParameterValidator(symbolTable, typeResolver);
validator.ValidateFunctionDefaults(functionDef);

if (validator.Errors.Any())
{
    foreach (var error in validator.Errors)
    {
        Console.WriteLine($"{error.Line}:{error.Column}: {error.Message}");
    }
}
```

---

### 2. **Fail-Fast Validation with Early Returns**

Validation checks run in priority order with early exits:

```csharp
private void ValidateDefaultValue(Parameter param, string functionName)
{
    if (IsMutableDefault(defaultValue))
    {
        AddError(...);
        return;  // Stop checking this parameter
    }

    if (!IsCompileTimeConstant(defaultValue))
    {
        AddError(...);
        return;  // Stop checking this parameter
    }

    // Only check None if we got this far
    if (defaultValue is NoneLiteral) { ... }
}
```

**Order of Checks**:
1. **Mutable defaults** (highest priority) - fundamental design error
2. **Compile-time constants** - codegen requirement
3. **Nullable type checking** (lowest priority) - only for None literals

**Why**: Each validation check returns early if it fails. This prevents cascading errors (e.g., a mutable default would also fail the compile-time constant check, but we only report the more specific mutable default error).

---

### 3. **Recursive Validation with Pattern Matching**

Both `IsMutableDefault` and `IsCompileTimeConstant` use switch expressions for clean, exhaustive case analysis:

```csharp
private bool IsCompileTimeConstant(Expression expr)
{
    return expr switch
    {
        IntegerLiteral => true,
        TupleLiteral tuple => tuple.Elements.All(IsCompileTimeConstant),  // Recursive
        Parenthesized paren => IsCompileTimeConstant(paren.Expression),   // Recursive
        // ...
        _ => false  // Default case
    };
}
```

**Benefits**:
- Readable and maintainable
- Compiler warns if a new expression type is added but not handled
- Natural recursion for nested structures (tuples, parenthesized expressions)
- Type narrowing (e.g., `tuple` is already typed as `TupleLiteral`)

**Recursive Cases**:
- `TupleLiteral`: Validates all elements recursively
- `UnaryOp`: Validates operand
- `BinaryOp`: Validates both left and right operands
- `ConditionalExpression`: Validates test, then-value, and else-value
- `Parenthesized`: Validates inner expression

---

### 4. **Conservative Unknown Handling**

The validator treats unknown types permissively:

```csharp
if (paramType is not NullableType && paramType is not UnknownType)
{
    AddError(...);
}
```

**Why**: During multi-pass semantic analysis, some types may not be resolved yet. By allowing `UnknownType`, the validator avoids false positives. The type checker will catch real errors later.

**Example**:
```python
# During first pass, SomeType might not be resolved yet
def foo(x: SomeType = None):
    pass
```

If `SomeType` resolves to `UnknownType`, we don't error. If it later resolves to a non-nullable type, the type checker will catch it.

---

### 5. **Helpful Error Messages**

Error messages include:
- The parameter name
- The function name
- The specific problem
- Suggested fixes

**Examples**:

```
"Mutable default value is not allowed for parameter 'items' in function 'add_item'.
Use None as default and initialize in the function body instead."
```

```
"Cannot use 'None' as default value for non-nullable parameter 'count' of type 'int' in function 'process'.
Use 'int?' to make the parameter nullable."
```

**Quality Guidelines**:
- ✓ Identify specific parameter and function
- ✓ Explain the restriction
- ✓ Provide actionable guidance
- ✗ Avoid generic messages like "Invalid default value"

---

### 6. **Static Helper Methods**

Both `IsMutableDefault` and `IsCompileTimeConstant` are `static`:

```csharp
private static bool IsMutableDefault(Expression expr)
private static bool IsCompileTimeConstant(Expression expr)
```

**Rationale**:
- Pure functions that only examine AST structure
- No instance state needed (except for `IsConstReference` and `IsEnumMemberAccess` delegations)
- Can be easily tested in isolation
- Clearly communicates that no side effects occur

**Exception**: `IsCompileTimeConstant` is actually non-static because it calls `IsConstReference` and `IsEnumMemberAccess`, which need access to `_symbolTable`.

---

## Debugging Tips

### 1. **Check Symbol Table State**

If `IsConstReference` or `IsEnumMemberAccess` isn't working:
- Verify the symbol table has been populated before validation
- Check that const declarations are marked with `IsConstant = true`
- Ensure enum types are registered with `TypeKind.Enum`

**Debug Point**: Set breakpoint in `IsConstReference` and inspect `symbol`:
```csharp
var symbol = _symbolTable.Lookup(id.Name);  // Breakpoint here
// Inspect: Is symbol null? Is it a VariableSymbol? Is IsConstant true?
return symbol is VariableSymbol { IsConstant: true };
```

**Common Issues**:
- Symbol table not populated yet (validation running too early in pipeline)
- Const declarations not being registered as `IsConstant = true`
- Wrong scope being searched (need to traverse parent scopes)

---

### 2. **Trace Recursive Validation**

For nested expressions, trace the recursion:

```csharp
private bool IsCompileTimeConstant(Expression expr)
{
    _logger.LogDebug($"IsCompileTimeConstant: {expr.GetType().Name}");
    var result = expr switch { /* ... */ };
    _logger.LogDebug($"  -> {result}");
    return result;
}
```

Example trace for `(1 + 2)`:
```
IsCompileTimeConstant: Parenthesized
  IsCompileTimeConstant: BinaryOp
    IsCompileTimeConstant: IntegerLiteral
      -> true
    IsCompileTimeConstant: IntegerLiteral
      -> true
    -> true
  -> true
```

---

### 3. **Verify Validation Order**

The validation checks run in this order:
1. **Mutable defaults** (highest priority) - line 53
2. **Compile-time constants** - line 64
3. **None for nullable types** - line 74

If you're not seeing expected errors, check if an earlier validation is returning early:

```csharp
private void ValidateDefaultValue(Parameter param, string functionName)
{
    _logger.LogDebug($"Validating {param.Name} in {functionName}");

    if (IsMutableDefault(defaultValue))
    {
        _logger.LogDebug("  -> Mutable default detected");
        AddError(...);
        return;  // Stops here!
    }

    // Won't reach if mutable
    if (!IsCompileTimeConstant(defaultValue))
    {
        _logger.LogDebug("  -> Non-constant detected");
        AddError(...);
        return;
    }

    // Won't reach if non-constant
    if (defaultValue is NoneLiteral)
    {
        _logger.LogDebug("  -> Checking None nullable");
        // ...
    }
}
```

---

### 4. **Test Edge Cases**

Common edge cases to test:

```python
# Nested structures
def foo(t: tuple = ((1, 2), (3, 4))):     # Valid - nested tuples
def bar(t: tuple = ([1, 2], [3, 4])):     # Invalid - tuples containing lists

# Parentheses
def baz(x: int = (42)):                   # Valid - just parenthesized
def qux(x: int = ((1 + 2))):              # Valid - nested parentheses
def quux(x: list = ([])):                 # Invalid - parenthesized mutable

# Ternary expressions
def test1(x: int = 5 if True else 10):    # Valid - all constants
def test2(x: int = y if True else 10):    # Invalid - y is variable
def test3(x: int? = None if True else 5): # Valid - None + constant

# Const references
const MAX = 100
def test4(x: int = MAX):                  # Valid - const reference
def test5(x: int = MAX * 2):              # Valid - const in expression

# Enum members
enum Color:
    RED = 1

def test6(c: Color = Color.RED):          # Valid - enum member
def test7(c: Color = Color):              # Invalid - enum type, not member
```

---

### 5. **Location Information**

If errors don't point to the right location:
- Check that `param.LineStart` and `param.ColumnStart` are set correctly by the parser
- The error points to the parameter declaration, not the default value expression
- Consider whether pointing to the default value would be more helpful (enhancement)

**Current**: Error at parameter declaration
```python
def foo(items: list = []):
        ^^^^^ Error here
```

**Potential Enhancement**: Error at default value
```python
def foo(items: list = []):
                      ^^ Error here
```

---

### 6. **Use AST Dumper for Inspection**

When debugging unexpected AST structures:

```csharp
using Sharpy.Compiler.Parser;

// In your debugging code
Console.WriteLine("Parameter default value AST:");
Console.WriteLine(AstDumper.Dump(param.DefaultValue));
```

This shows the exact AST structure, helping identify:
- Unexpected node types
- Nested structures
- Parenthesization levels

---

## Contribution Guidelines

### ⚠️ Important: This File is Deprecated

**Before making changes**, consider whether you should be modifying `DefaultParameterValidatorV2` instead:

- **Bug fix needed in validation logic?** → Fix in `DefaultParameterValidatorV2` (and potentially backport if necessary)
- **Adding new constant expression support?** → Add to `DefaultParameterValidatorV2`
- **Test failing here?** → Consider updating tests to use `ValidationPipelineFactory`

This validator is kept for backward compatibility during the migration to the new validation pipeline.

### When to Modify This File

Only modify this file if:
- Critical bug fix affecting current code still using the legacy validator
- Documentation improvements (like this walkthrough)
- Maintaining backward compatibility during the migration period

### Legacy Modification Guidelines

If you must modify this deprecated validator:

1. **Adding New Mutable Types**:
   - If Sharpy adds new mutable collection types, update `IsMutableDefault`
   - Example: Adding a mutable `OrderedDict` type:
     ```csharp
     FunctionCall call when call.Function is Identifier id && id.Name == "OrderedDict" => true,
     ```

2. **Supporting New Constant Expression Types**:
   - If new literal types are added to the AST, update `IsCompileTimeConstant`
   - Example: Adding bytes literals:
     ```csharp
     BytesLiteral => true,
     ```

3. **Relaxing Constant Rules**:
   - If certain function calls should be allowed as constants
   - Example: Allowing `len()` on constant strings:
     ```csharp
     FunctionCall call when IsConstantLenCall(call) => true,
     ```
   - Consider codegen implications - can the C# backend handle it?

4. **Improving Error Messages**:
   - Add more context or better suggestions
   - Include code examples in error messages
   - Provide IDE quick-fixes (requires architecture changes)

5. **Supporting Qualified Enum Access**:
   - If Sharpy needs to support `MyModule.Color.RED` patterns
   - Update `IsEnumMemberAccess` to resolve module paths

### What NOT to Change

1. **Don't Make This a Multi-Pass Validator**:
   - Keep it as a single-pass, focused validator
   - Complex type checking belongs in `TypeChecker`

2. **Don't Add Type Inference Here**:
   - This validator assumes types are already resolved by `TypeResolver`
   - Type inference happens elsewhere in semantic analysis

3. **Don't Throw Exceptions for Validation Errors**:
   - Maintain the error accumulation pattern
   - Exceptions are for internal errors only (e.g., null dependencies)

4. **Don't Add Complex Type Compatibility Checking**:
   - Example: Don't check if `42` is assignable to `str` parameter
   - That's the `TypeChecker`'s responsibility

### Testing Considerations

When modifying, ensure tests cover:

**Mutable Default Tests**:
```python
def test_list(items: list = []):          # Must error
def test_dict(data: dict = {}):           # Must error
def test_set(values: set = set()):        # Must error
def test_list_ctor(items: list = list()): # Must error
def test_paren_mut(items: list = ([])):   # Must error
```

**Compile-Time Constant Tests**:
```python
# Valid
def test_int(x: int = 42):
def test_tuple(t: tuple = (1, 2)):
def test_expr(n: int = 1 + 2):
def test_unary(n: int = -1):

const MAX = 100
def test_const(x: int = MAX):
def test_enum(c: Color = Color.RED):

# Invalid
x = 42
def test_var(y: int = x):                 # Must error
def test_call(n: int = len("hello")):     # Must error
```

**Nullable Type Tests**:
```python
def test_none_nullable(x: str? = None):   # Valid
def test_none_non_null(x: str = None):    # Must error
def test_none_unknown(x: UnknownType = None): # No error (type unresolved)
```

**Edge Cases**:
```python
def test_nested_tuple(t: tuple = ((1, 2), (3, 4))): # Valid
def test_ternary(x: int = 5 if True else 10):       # Valid
def test_complex_expr(n: int = (1 + 2) * 3):        # Valid
```

Test file location (expected): `tests/Sharpy.Compiler.Tests/Semantic/DefaultParameterValidatorTests.cs`

---

## Cross-References

### Successor and Migration

- **⭐ [Validation/DefaultParameterValidatorV2.md](./Validation/DefaultParameterValidatorV2.md)**: Modern pipeline-compatible replacement (RECOMMENDED)
- **[Validation/ValidationPipeline.md](./Validation/ValidationPipeline.md)**: New validation architecture
- **[Validation/ValidationPipelineFactory.md](./Validation/ValidationPipelineFactory.md)**: Factory for creating validators
- **[Validation/ISemanticValidator.md](./Validation/ISemanticValidator.md)**: Interface implemented by V2

### Related Semantic Analysis Files

- **[SymbolTable.md](SymbolTable.md)**: Provides symbol lookup functionality used by this validator (lines 199, 216)
- **[TypeResolver.md](TypeResolver.md)**: Resolves type annotations for nullable type checking (line 76)
- **[SemanticError.md](SemanticError.md)**: Error type used for reporting validation failures
- **`SemanticAnalyzer.cs`**: Likely orchestrates semantic analysis and calls `ValidateFunctionDefaults`

### Related AST Files

- **[../Parser/Ast/Statement.md](../Parser/Ast/Statement.md)**: Defines `FunctionDef` and `Parameter` classes
- **[../Parser/Ast/Expression.md](../Parser/Ast/Expression.md)**: Defines all expression types checked in this validator

### Downstream Impact

- **[../CodeGen/RoslynEmitter.md](../CodeGen/RoslynEmitter.md)**: Relies on this validator to ensure default values can be emitted as C# default parameters
- C# default parameters have similar restrictions (must be constants), so this validator ensures Sharpy→C# mapping is valid

### Related Validators

- **[ControlFlowValidator.md](ControlFlowValidator.md)**: Validates return paths and unreachable code
- **[ProtocolValidator.md](ProtocolValidator.md)**: Validates protocol implementations
- **[AccessValidator.md](AccessValidator.md)**: Checks access modifiers and visibility
- **[OperatorValidator.md](OperatorValidator.md)**: Validates operator usage

---

## Summary

The `DefaultParameterValidator` is a focused, well-designed component that enforces critical rules about default parameter values:

1. **Prevents mutable defaults** - avoiding Python's shared state pitfall
2. **Enforces compile-time constants** - ensuring C# codegen compatibility
3. **Type-checks None defaults** - maintaining null safety

**Key Design Principles**:
- **Single Responsibility**: Only validates default parameters, nothing else
- **Error Accumulation**: Collects all errors for batch reporting
- **Fail-Fast Validation**: Prioritized checks with early exits
- **Conservative Approach**: Rejects uncertain cases rather than accepting potentially problematic code
- **Helpful Messages**: Clear error messages with actionable guidance

**Integration Points**:
- **Depends on**: `SymbolTable` (for const/enum lookup), `TypeResolver` (for nullable checks)
- **Called by**: `SemanticAnalyzer` during semantic analysis phase
- **Ensures**: Code is safe for `RoslynEmitter` to generate C# defaults

When working with this code, remember its position in the pipeline - it assumes AST is complete and symbols are resolved, but doesn't perform complex type inference itself. Its goal is simple: **default values should be immutable, compile-time-evaluable constants**.
