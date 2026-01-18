# Walkthrough: TypeChecker.Expressions.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`

---

## Overview

The `TypeChecker.Expressions` partial class is responsible for **type checking all expression nodes** in the Sharpy Abstract Syntax Tree (AST). This is the heart of the type system - it ensures that every expression (operators, function calls, member accesses, literals, comprehensions, etc.) has a valid type and that operations are semantically correct.

This file is one of several partial class files that make up the complete `TypeChecker` class. It specifically handles the traversal and validation of expression AST nodes, delegating to specialized validators (like `OperatorValidator` and `ProtocolValidator`) for specific type system rules.

### Role in the Compiler Pipeline

```
Source (.spy) → Lexer → Parser (AST) → NameResolver → [TypeChecker.Expressions] → CodeGen
                                           ↓
                                      SymbolTable
                                           ↓
                                      SemanticInfo (type annotations)
```

**Upstream**: Receives a fully-parsed AST from the Parser and a populated SymbolTable from NameResolver.

**Downstream**: Produces a `SemanticInfo` object containing type information for every expression, which is consumed by the CodeGen (RoslynEmitter) to generate type-correct C# code.

**Key Concepts**:
- **Type Inference**: Automatically determining the type of expressions without explicit annotations
- **Type Checking**: Validating that operations are type-safe (e.g., can't add a string to an integer)
- **Type Narrowing**: Tracking refined types in conditional contexts (e.g., after an `isinstance` check)
- **Protocol Validation**: Ensuring types support required operations (e.g., `__iter__` for iteration, `__getitem__` for indexing)

---

## Cross-References: Partial Class Files

This file is part of a **partial class** spread across multiple files:

- **[TypeChecker.cs](TypeChecker.md)** - Main class definition, fields, constructor, and public API
- **[TypeChecker.Expressions.cs](TypeChecker.Expressions.md)** - Expression type checking (THIS FILE)
- **[TypeChecker.Statements.cs](TypeChecker.Statements.md)** - Statement type checking (if/while/for/return/etc.)
- **[TypeChecker.Definitions.cs](TypeChecker.Definitions.md)** - Type definition checking (classes, interfaces, functions)
- **[TypeChecker.Utilities.cs](TypeChecker.Utilities.md)** - Helper methods (IsAssignable, type resolution, narrowing)

To understand the complete type checking system, you may need to reference these related files.

---

## Class Structure

This is a **partial class** definition:

```csharp
public partial class TypeChecker
{
    // Expression checking methods
    public SemanticType CheckExpression(Expression expr);
    private SemanticType CheckIdentifier(Identifier id);
    private SemanticType CheckBinaryOp(BinaryOp binOp);
    // ... many more specialized checkers
}
```

### Important Fields (from TypeChecker.cs)

While not defined in this file, these fields from the main `TypeChecker.cs` are heavily used:

- **`_symbolTable`**: Symbol lookup for identifiers and types
- **`_semanticInfo`**: Stores computed type information for AST nodes
- **`_operatorValidator`**: Validates operator overloads and type compatibility
- **`_protocolValidator`**: Validates protocol support (iteration, indexing, etc.)
- **`_accessValidator`**: Validates field/method access levels (public/private/protected)
- **`_typeResolver`**: Resolves type annotations to semantic types
- **`_narrowedTypes`**: Dictionary tracking type narrowing in conditional branches
- **`_currentClass`**: The class being analyzed (for `self` validation)

---

## Key Methods

### Core Entry Point

#### `CheckExpression(Expression expr)` (lines 11-51)

**Purpose**: The main dispatcher for type checking any expression node.

```csharp
public SemanticType CheckExpression(Expression expr)
{
    // Check cache
    var cached = _semanticInfo.GetExpressionType(expr);
    if (cached != null)
        return cached;

    SemanticType type = expr switch
    {
        IntegerLiteral => SemanticType.Int,
        FloatLiteral => SemanticType.Double,
        StringLiteral => SemanticType.Str,
        // ... 20+ expression types
        _ => SemanticType.Unknown
    };

    // Cache the result
    _semanticInfo.SetExpressionType(expr, type);
    return type;
}
```

**Design Pattern**: This is a **caching dispatcher** using C# pattern matching:
1. **Check cache first** - Expressions are often checked multiple times, so caching avoids redundant work
2. **Pattern match** on the expression type to route to specialized handlers
3. **Cache the result** before returning

**Performance Note**: The caching is critical for performance. A single expression might be checked multiple times during type narrowing, control flow analysis, or nested function calls.

---

### Literals and Identifiers

#### `CheckIdentifier(Identifier id)` (lines 53-95)

**Purpose**: Resolves an identifier to its type by looking it up in the symbol table.

**Special Cases**:
- **`self` validation** (lines 56-65): The `self` keyword can only be used inside instance methods
- **Type narrowing** (lines 78-81): If the identifier has been narrowed by a type check (e.g., `isinstance(x, int)`), return the narrowed type
- **Module symbols**: Returns a `ModuleType` for imported modules (e.g., `import math` makes `math` a module identifier)

**Symbol Type Handling**:
```csharp
return symbol switch
{
    VariableSymbol varSymbol => varSymbol.Type,
    FunctionSymbol funcSymbol => new FunctionType { ... },
    ModuleSymbol moduleSymbol => new ModuleType { ... },
    TypeSymbol => SemanticType.Unknown, // Type names as values need special handling
    _ => SemanticType.Unknown
};
```

**Important**: `TypeSymbol` returns `Unknown` because type names like `int` or `MyClass` can't be used as values directly (except in constructor calls or generics, which are handled elsewhere).

---

### Operators

#### `CheckBinaryOp(BinaryOp binOp)` (lines 97-121)

**Purpose**: Type checks binary operators (e.g., `+`, `-`, `*`, `<`, `and`, `|>`).

**Algorithm**:
1. Check left and right operand types
2. If either is `Unknown`, return `Unknown` to **avoid cascading errors**
3. Delegate to `OperatorValidator.ValidateBinaryOp()` for the actual type checking

**Why delegate?** The `OperatorValidator` encapsulates all the complex rules about which types support which operators, including operator overloading and type compatibility.

**Special Case**: The **pipe forward operator** (`|>`) is handled separately by `CheckPipeForward()` because it's a syntactic transformation, not a regular binary operator.

---

#### `CheckPipeForward(BinaryOp binOp)` (lines 129-229)

**Purpose**: Type checks the pipe forward operator: `x |> f` becomes `f(x)`.

**Syntax Variants**:
```python
x |> f          # f(x)
x |> f(y)       # f(x, y) - prepend x to args
x |> f |> g     # g(f(x)) - chaining via left-associativity
```

**Implementation Strategy**:
1. **Case 1** (line 140): Right side is already a function call → delegate to `CheckPipeForwardWithFunctionCall()`
2. **Case 2** (line 146): Right side is a simple identifier or expression → validate that it's callable and accepts the piped value as first parameter

**Validation**:
- The function must accept at least 1 parameter
- The piped value type must be assignable to the first parameter type
- Remaining required parameters must have defaults (since they're not provided)

**Current Limitation** (lines 211-217): Piping to constructors (`x |> SomeClass()`) is not yet supported.

---

#### `CheckPipeForwardWithFunctionCall(...)` (lines 235-375)

**Purpose**: Handles `x |> f(y, z)` by prepending the piped value to the argument list: `f(x, y, z)`.

**Complex Validation**:
1. Check the function being called
2. Collect existing argument types (both positional and keyword)
3. Build full argument list: `[pipedType] + existingArgTypes`
4. Validate argument count and types against function signature

**Keyword Argument Handling** (lines 303-326):
- Validates that keyword argument names exist in the function signature
- Checks for duplicate arguments (positional + keyword for same parameter)
- Validates keyword argument types

**Error Reporting Details**: Notice how error messages distinguish between "piped value" and regular arguments for better debugging (lines 295-298).

---

#### `CheckComparisonChain(ComparisonChain chain)` (lines 395-445)

**Purpose**: Type checks chained comparisons like `a < b < c`.

**Algorithm**:
1. Validate chain structure: operators count = operands count - 1
2. Type check all operands
3. Validate each adjacent pair using `OperatorValidator`

**Example**: For `a < b < c`:
- Operands: `[a, b, c]`
- Operators: `[<, <]`
- Validates: `(a < b)` AND `(b < c)`

**Result**: All comparison chains return `SemanticType.Bool`.

---

### Member Access and Indexing

#### `CheckMemberAccess(MemberAccess memberAccess)` (lines 447-544)

**Purpose**: Type checks member access expressions like `obj.field`, `obj.method`, `module.function`.

**Key Features**:

1. **Null Conditional Operator** (`?.`) (lines 459-471):
   ```python
   x?.field  # Only works on nullable types
   ```
   - Validates the object is nullable
   - Uses underlying type for member lookup
   - Wraps result in nullable type

2. **Module Member Access** (lines 474-496):
   ```python
   config.MAX_SIZE
   utils.helper()
   ```
   - Looks up exported symbols from the module

3. **Class/Struct Member Access** (lines 498-543):
   - Searches for fields and methods in the type hierarchy
   - Uses `FindFieldInHierarchy()` and `FindMethodInHierarchy()` to support inheritance
   - Validates access levels (public/private/protected) via `_accessValidator`

4. **Method Binding** (lines 524-532):
   - When accessing a method via `obj.method`, the object is implicitly bound as `self`
   - The returned `FunctionType` **skips the first parameter** (self) since it's already bound

**Super Expression Handling** (lines 450-453): Calls to `super().method()` are validated by `ValidateSuperMemberAccess()` (defined in TypeChecker.Utilities.cs).

---

#### `FindFieldInHierarchy(TypeSymbol type, string fieldName)` (lines 549-567)

**Purpose**: Searches for a field in a type and its base classes.

**Algorithm**:
1. Check the type itself
2. Walk up the base class chain
3. Return the field and its declaring type (needed for access validation)

**Why return the owner?** Access level validation needs to know where the field was declared (e.g., private fields in a base class are not accessible).

---

#### `FindMethodInHierarchy(TypeSymbol type, string methodName)` (lines 572-598)

**Purpose**: Searches for a method in a type, base classes, and interfaces.

**Search Order**:
1. The type itself
2. Base class chain
3. Implemented interfaces

**Interface Methods**: This allows calling interface methods on concrete types that implement them.

---

#### `CheckIndexAccess(IndexAccess indexAccess)` (lines 600-661)

**Purpose**: Type checks indexing operations like `list[0]`, `dict[key]`, `Box[int]`.

**Special Cases**:

1. **Type Narrowing** (lines 603-607):
   ```python
   if isinstance(x, dict):
       y = x[key]  # x is narrowed to dict
   ```

2. **Generic Type Instantiation** (lines 614-631):
   ```python
   Box[int]        # Generic type with type argument
   Pair[int, str]  # Multiple type arguments
   ```
   - Parsed as `IndexAccess(Object: Box, Index: int)`
   - Returns a `GenericType` with resolved type arguments

3. **Generic Function Instantiation** (lines 635-648):
   ```python
   identity[int](42)  # Generic function with explicit type argument
   ```

4. **Regular Indexing** (lines 651-660):
   - Delegates to `ProtocolValidator.ValidateIndexAccess()`
   - Checks for `__getitem__` protocol support

**Type Argument Resolution**: The helper `TryResolveTypeArguments()` (lines 1019-1042) handles both single type arguments and tuple-based multiple type arguments.

---

### Function Calls

#### `CheckFunctionCall(FunctionCall call)` (lines 663-970)

**Purpose**: Type checks function calls including regular functions, methods, constructors, and generic functions.

**Complexity**: This is one of the most complex methods in the file (300+ lines) because it handles many different calling conventions:

1. **Null Conditional Method Calls** (line 666):
   ```python
   obj?.method()  # Returns nullable result
   ```

2. **Super Init Tracking** (lines 673-676):
   - Marks `super().__init__()` as called for constructor validation

3. **Generic Type Instantiation** (lines 700-725):
   ```python
   Box[int](42)  # Constructor for generic type
   ```

4. **Generic Function Calls** (lines 728-738):
   ```python
   identity[int](42)  # Generic function with explicit type argument
   ```

5. **Built-in Function Overloading** (lines 781-831):
   - Handles built-in functions with multiple overloads (e.g., `range()`)
   - Performs argument count filtering, then type compatibility checking
   - Selects the best matching overload

6. **Constructor Calls** (lines 755-767):
   - Validates that abstract classes cannot be instantiated
   - Returns a `UserDefinedType` instance

**Argument Validation**:
- **Positional arguments** (lines 889-896): Validates type compatibility
- **Keyword arguments** (lines 899-922):
  - Validates keyword names exist
  - Checks for duplicate arguments (positional + keyword)
  - Validates types

**Default Parameters**: The validation correctly handles functions with default parameters by distinguishing between required and total parameter counts (lines 869-884).

---

### Collections and Comprehensions

#### `CheckListLiteral(ListLiteral list)` (lines 1044-1073)

**Purpose**: Type checks list literals like `[1, 2, 3]`.

**Type Inference**:
1. Empty list `[]` → `list[Unknown]`
2. Non-empty list → Find common element type

**Limitation**: Currently uses simple assignability check. All elements must be assignable to the first element's type. A more sophisticated algorithm would find the least common supertype.

---

#### `CheckDictLiteral(DictLiteral dict)` (lines 1075-1097)

**Purpose**: Type checks dictionary literals like `{"key": "value"}`.

**Type Inference**:
1. Empty dict `{}` → `dict[Unknown, Unknown]`
2. Non-empty dict → Infer key and value types from first entry

**Limitation**: Like lists, this is simplified. Production compilers would find common key/value types across all entries.

---

#### `CheckListComprehension(ListComprehension listComp)` (lines 1126-1189)

**Purpose**: Type checks list comprehensions like `[x * 2 for x in range(10) if x > 5]`.

**Scope Management** (lines 1129, 1182):
- Creates a new scope for the comprehension
- Variables defined in the comprehension don't leak to outer scope

**Clause Processing** (lines 1132-1177):
1. **For Clause**:
   - Validates iterator supports `__iter__` protocol via `ProtocolValidator`
   - Defines loop variable with inferred element type
   - Stores symbol and type information
2. **If Clause**:
   - Validates condition is boolean

**Result**: Returns `list[elementType]` where `elementType` is inferred from the element expression.

**Limitation** (lines 1159-1165): Tuple unpacking in comprehensions (e.g., `[x + y for x, y in pairs]`) is not yet supported.

---

#### `CheckSetComprehension(SetComprehension setComp)` (lines 1191-1253)

**Purpose**: Type checks set comprehensions like `{x * 2 for x in range(10)}`.

**Implementation**: Nearly identical to `CheckListComprehension()` but returns `set[elementType]` instead of `list[elementType]`.

**Code Duplication Note**: This is duplicated code. A refactoring opportunity would be to extract a shared `CheckComprehension()` helper.

---

#### `CheckDictComprehension(DictComprehension dictComp)` (lines 1255-1318)

**Purpose**: Type checks dict comprehensions like `{x: x*2 for x in range(10)}`.

**Key Difference**: Validates both key and value expressions (lines 1308-1309) instead of just element expression.

**Result**: Returns `dict[keyType, valueType]`.

---

### Conditional and Lambda Expressions

#### `CheckConditionalExpression(ConditionalExpression cond)` (lines 1320-1333)

**Purpose**: Type checks ternary expressions like `x if condition else y`.

**Type Inference**:
1. If `thenType` is assignable to `elseType` → return `elseType`
2. If `elseType` is assignable to `thenType` → return `thenType`
3. Otherwise → return `Unknown`

**Example**:
```python
result = 42 if flag else 3.14  # Type: double (int assignable to double)
```

---

#### `CheckLambda(LambdaExpression lambda)` (lines 1335-1366)

**Purpose**: Type checks lambda expressions like `lambda x: int, y: int -> x + y`.

**Scope Management**:
1. Enters a new scope for the lambda
2. Defines parameter symbols
3. Type checks the body
4. Exits scope

**Result**: Returns a `FunctionType` with parameter types and inferred return type from the body.

**Contrast with Named Functions**: Unlike named functions, lambdas don't have explicit return type annotations - the return type is always inferred from the body.

---

### Type Casts and Checks

#### `CheckTypeCast(TypeCast cast)` (lines 1368-1372)

**Purpose**: Type checks cast expressions like `x as SomeType`.

**Implementation**:
1. Type checks the value expression
2. Resolves the target type annotation
3. Returns the target type

**Important**: This doesn't validate that the cast is valid - it trusts the programmer. Runtime checks will be inserted by CodeGen.

---

#### `CheckTypeCheck(TypeCheck typeCheck)` (lines 1374-1379)

**Purpose**: Type checks `isinstance()` expressions like `x is SomeType`.

**Result**: Always returns `SemanticType.Bool`.

**Type Narrowing**: While this method just returns bool, the `TypeChecker.Utilities.cs` file contains logic to narrow types based on `isinstance` checks in conditional contexts.

---

## Helper Methods

### `TryResolveExpressionAsType(Expression expr)` (lines 977-1012)

**Purpose**: Attempts to interpret an expression as a type reference (for generic type syntax).

**Use Case**: When parsing `Box[int]`, the `int` is initially parsed as an `Identifier` expression, but we need to resolve it as a type.

**Supported Cases**:
1. Simple identifier → resolve as type name
2. Nested generic → handle `Container[Box[int]]`

**Returns**: `SemanticType` if successful, `null` if the expression isn't a valid type.

---

### `TryResolveTypeArguments(Expression indexExpr)` (lines 1019-1042)

**Purpose**: Resolves type arguments from an index expression (handles both single and multiple type args).

**Examples**:
```python
Box[int]         # Single type arg → [int]
Pair[int, str]   # Multiple type args → [int, str] (parsed as TupleLiteral)
```

**Implementation**:
1. If tuple literal → resolve each element as a type
2. Otherwise → resolve single type argument

---

## Dependencies

### Internal Dependencies

- **`SymbolTable`**: Symbol lookup for identifiers
- **`SemanticInfo`**: Stores computed type information
- **`OperatorValidator`**: Validates operator overloads and type rules
- **`ProtocolValidator`**: Validates protocol support (iteration, indexing, len, etc.)
- **`AccessValidator`**: Validates public/private/protected access
- **`TypeResolver`**: Resolves type annotations to semantic types

### AST Node Types

Depends on expression node types from `Sharpy.Compiler.Parser.Ast`:
- Literals: `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`, `NoneLiteral`
- Collections: `ListLiteral`, `DictLiteral`, `SetLiteral`, `TupleLiteral`
- Comprehensions: `ListComprehension`, `SetComprehension`, `DictComprehension`
- Operators: `BinaryOp`, `UnaryOp`, `ComparisonChain`
- Access: `Identifier`, `MemberAccess`, `IndexAccess`
- Calls: `FunctionCall`
- Control: `ConditionalExpression`, `LambdaExpression`
- Type operations: `TypeCast`, `TypeCheck`

---

## Patterns and Design Decisions

### 1. **Caching Pattern**

Every expression type is cached in `SemanticInfo` to avoid redundant type checking. This is critical for performance since expressions may be checked multiple times.

### 2. **Error Cascade Prevention**

When an operand has type `Unknown`, methods return `Unknown` immediately without further validation. This prevents cascading error messages that would confuse users.

```csharp
if (leftType is UnknownType || rightType is UnknownType)
{
    return SemanticType.Unknown;
}
```

### 3. **Delegation to Specialized Validators**

Rather than embedding all type rules in this file, it delegates to:
- `OperatorValidator` for operator type checking
- `ProtocolValidator` for protocol support (iteration, indexing)
- `AccessValidator` for access level validation

**Benefit**: Separation of concerns, easier testing, and centralized type rules.

### 4. **Scope Management for Nested Contexts**

Comprehensions and lambdas create new scopes:
```csharp
_symbolTable.EnterScope("list-comprehension");
// ... define loop variables
_symbolTable.ExitScope();
```

This ensures variables don't leak to outer scopes.

### 5. **Two-Level Validation Strategy**

Many methods first try to get a `FunctionSymbol` (for detailed validation with default parameters), then fall back to `FunctionType` validation:

```csharp
if (funcSymbol != null) {
    // Detailed validation with default parameters
}
else if (funcType is FunctionType ft) {
    // Fallback validation
}
```

### 6. **Pattern Matching for Type Dispatch**

The main `CheckExpression()` uses C# pattern matching for clean, exhaustive handling of expression types.

---

## Debugging Tips

### 1. **Expression Type Not What You Expected?**

Add a breakpoint in `CheckExpression()` and examine:
- The `_semanticInfo.GetExpressionType(expr)` cached value
- The computed type before caching

### 2. **Identifier Not Found?**

Check `CheckIdentifier()`:
- Is the symbol in `_symbolTable.Lookup(id.Name)`?
- Is the scope correct? (Use `_symbolTable.DumpScopes()` to inspect)

### 3. **Type Narrowing Issues?**

Examine `_narrowedTypes` dictionary in `CheckIdentifier()`:
- Is the narrowed type being set? (Check `TypeChecker.Utilities.cs`)
- Is the narrowing key correct? (See `ExtractNarrowingKey()`)

### 4. **Function Call Validation Failing?**

The `CheckFunctionCall()` method is complex. Check:
- Is the callee a `FunctionSymbol`, `FunctionType`, or `TypeSymbol`?
- Are default parameters being handled correctly?
- Are keyword arguments validated?

### 5. **Generic Type Issues?**

Check `TryResolveTypeArguments()` and `TryResolveExpressionAsType()`:
- Are type arguments being resolved correctly?
- Is the generic type symbol marked as `IsGeneric`?

### 6. **Tracing Type Checking**

Add logging at the start of `CheckExpression()`:
```csharp
_logger.LogDebug($"Checking expression: {expr.GetType().Name} at {expr.LineStart}:{expr.ColumnStart}");
```

---

## Contribution Guidelines

### Common Changes

1. **Adding Support for New Expression Types**:
   - Add a new case to the `CheckExpression()` switch
   - Create a new `CheckXxx()` method following the existing pattern
   - Ensure caching is handled correctly

2. **Improving Type Inference**:
   - Modify `CheckListLiteral()`, `CheckDictLiteral()`, etc.
   - Consider implementing least common supertype algorithm

3. **Adding Protocol Support**:
   - Don't add it here - extend `ProtocolValidator` instead
   - This file delegates protocol validation

4. **Enhancing Error Messages**:
   - Include type information in error messages
   - Use `GetDisplayName()` for human-readable type names
   - Include source location (line/column) for precise errors

### Code Conventions

1. **Always check for `UnknownType`** to prevent cascade errors
2. **Cache results** in `SemanticInfo` before returning
3. **Use `AddError()`** with precise line/column information
4. **Delegate to validators** rather than embedding type rules
5. **Follow the existing pattern** of returning `SemanticType.Unknown` for errors

### Testing Considerations

When adding new expression support, ensure tests cover:
- Valid cases with correct type inference
- Error cases with helpful error messages
- Edge cases (empty collections, null conditionals, etc.)
- Interaction with type narrowing and generics

---

## Related Specification Documents

Refer to these language specification documents for semantic rules:

- `docs/language_specification/expressions.md` - Expression syntax and semantics
- `docs/language_specification/operator_precedence.md` - Operator precedence rules
- `docs/language_specification/type_annotations.md` - Type annotation syntax
- `docs/language_specification/type_casting.md` - Cast and type check semantics
- `docs/language_specification/type_hierarchy.md` - Type compatibility rules

---

## Summary

The `TypeChecker.Expressions` partial class is the **core of Sharpy's type system**. It:

✅ Type checks all expression nodes from the AST
✅ Infers types for literals, collections, and comprehensions
✅ Validates operators, function calls, and member access
✅ Supports advanced features like generics, null conditionals, and pipe forward
✅ Delegates to specialized validators for clean separation of concerns
✅ Caches results for performance
✅ Prevents error cascades by propagating `Unknown` types

Understanding this file is essential for working on Sharpy's type system, adding new expression types, or debugging type inference issues.
