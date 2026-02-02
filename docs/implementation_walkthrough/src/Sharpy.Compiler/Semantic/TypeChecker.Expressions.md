# Walkthrough: TypeChecker.Expressions.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`

---

## Overview

The `TypeChecker.Expressions` partial class is the **heart of Sharpy's type system**, responsible for **type checking all expression nodes** in the Abstract Syntax Tree (AST). This component ensures that every expression—from simple literals to complex function calls, operators, comprehensions, and generic type instantiations—has a valid type and that all operations are semantically correct.

This file is one of several partial class files that compose the complete `TypeChecker` class. It specifically handles the traversal and validation of expression AST nodes, leveraging the `TypeInferenceService` for type inference and delegating to the validation pipeline for specialized type system rules.

### Role in the Compiler Pipeline

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → [TypeChecker.Expressions] → CodeGen
                                              ↓
                                         SymbolTable
                                              ↓
                                         SemanticInfo (type annotations)
```

**Upstream**: Receives a fully-parsed AST from the Parser and a populated SymbolTable from NameResolver.

**Downstream**: Produces a `SemanticInfo` object containing type information for every expression, which is consumed by the CodeGen (RoslynEmitter) to generate type-correct C# code.

**Key Concepts**:
- **Type Inference**: Automatically determining the type of expressions without explicit annotations
- **Type Checking**: Validating that operations are type-safe (e.g., can't add a string to an integer without coercion)
- **Type Narrowing**: Tracking refined types in conditional contexts (e.g., after an `isinstance` check)
- **Generic Type Instantiation**: Resolving generic types with type arguments (e.g., `Box[int]`, `Pair[int, str]`)
- **Protocol Validation**: Ensuring types support required operations (e.g., `__iter__` for iteration, `__getitem__` for indexing)

---

## Cross-References: Partial Class Files

This file is part of a **partial class** spread across multiple files:

- **[TypeChecker.cs](TypeChecker.md)** - Main class definition, fields, constructor, and public API
- **[TypeChecker.Expressions.cs](TypeChecker.Expressions.md)** - Expression type checking (THIS FILE)
- **[TypeChecker.Statements.cs](TypeChecker.Statements.md)** - Statement type checking (if/while/for/return/etc.)
- **[TypeChecker.Definitions.cs](TypeChecker.Definitions.md)** - Type definition checking (classes, interfaces, functions)
- **[TypeChecker.Utilities.cs](TypeChecker.Utilities.md)** - Helper methods (IsAssignable, type resolution, narrowing, super validation)

To understand the complete type checking system, you may need to reference these related files.

---

## Class Structure

This is a **partial class** definition:

```csharp
public partial class TypeChecker
{
    // Core dispatcher
    public SemanticType CheckExpression(Expression expr);

    // Identifiers and literals
    private SemanticType CheckIdentifier(Identifier id);

    // Operators
    private SemanticType CheckBinaryOp(BinaryOp binOp);
    private SemanticType CheckPipeForward(BinaryOp binOp);
    private SemanticType CheckPipeForwardWithFunctionCall(...);
    private SemanticType CheckUnaryOp(UnaryOp unOp);
    private SemanticType CheckComparisonChain(ComparisonChain chain);

    // Member access and indexing
    private SemanticType CheckMemberAccess(MemberAccess memberAccess);
    private SemanticType CheckIndexAccess(IndexAccess indexAccess);
    private (VariableSymbol?, TypeSymbol?) FindFieldInHierarchy(...);
    private (FunctionSymbol?, TypeSymbol?) FindMethodInHierarchy(...);

    // Function calls
    private SemanticType CheckFunctionCall(FunctionCall call);

    // Collections and literals
    private SemanticType CheckListLiteral(ListLiteral list);
    private SemanticType CheckDictLiteral(DictLiteral dict);
    private SemanticType CheckSetLiteral(SetLiteral set);
    private SemanticType CheckTupleLiteral(TupleLiteral tuple);

    // Comprehensions
    private SemanticType CheckListComprehension(ListComprehension listComp);
    private SemanticType CheckSetComprehension(SetComprehension setComp);
    private SemanticType CheckDictComprehension(DictComprehension dictComp);

    // Control flow expressions
    private SemanticType CheckConditionalExpression(ConditionalExpression cond);
    private SemanticType CheckLambda(LambdaExpression lambda);

    // Type operations
    private SemanticType CheckTypeCast(TypeCast cast);
    private SemanticType CheckTypeCoercion(TypeCoercion coercion);
    private SemanticType CheckTypeCheck(TypeCheck typeCheck);

    // Helper methods
    private SemanticType? TryResolveExpressionAsType(Expression expr);
    private List<SemanticType>? TryResolveTypeArguments(Expression indexExpr);
    private void ValidateTypeCoercion(...);
    private bool CanPotentiallyCast(SemanticType source, SemanticType target);
    private bool InheritsFrom(TypeSymbol? derived, TypeSymbol? baseType);
}
```

### Important Fields (from TypeChecker.cs)

While not defined in this file, these fields from the main `TypeChecker.cs` are heavily used:

- **`_symbolTable`**: Symbol lookup for identifiers and types
- **`_semanticInfo`**: Stores computed type information for AST nodes
- **`_typeInference`**: TypeInferenceService for inferring result types of operations
- **`_typeResolver`**: Resolves type annotations to semantic types
- **`_validationPipeline`**: Pipeline of V2 validators for specialized checks
- **`_narrowedTypes`**: Dictionary tracking type narrowing in conditional branches
- **`_currentClass`**: The class being analyzed (for `self` validation)
- **`_currentFunctionReturnType`**: Tracks return type for return statement checking
- **`_superInitCalled`**: Tracks whether `super().__init__()` was called in constructors

---

## Key Methods

### Core Entry Point

#### `CheckExpression(Expression expr)` (lines 11-52)

**Purpose**: The main dispatcher for type checking any expression node. This is the entry point for all expression type checking.

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
        BooleanLiteral => SemanticType.Bool,
        NoneLiteral => SemanticType.Void,
        Identifier id => CheckIdentifier(id),
        BinaryOp binOp => CheckBinaryOp(binOp),
        UnaryOp unOp => CheckUnaryOp(unOp),
        ComparisonChain chain => CheckComparisonChain(chain),
        SuperExpression superExpr => CheckSuperExpression(superExpr),
        MemberAccess memberAccess => CheckMemberAccess(memberAccess),
        IndexAccess indexAccess => CheckIndexAccess(indexAccess),
        FunctionCall call => CheckFunctionCall(call),
        ListLiteral list => CheckListLiteral(list),
        DictLiteral dict => CheckDictLiteral(dict),
        SetLiteral set => CheckSetLiteral(set),
        TupleLiteral tuple => CheckTupleLiteral(tuple),
        ListComprehension listComp => CheckListComprehension(listComp),
        SetComprehension setComp => CheckSetComprehension(setComp),
        DictComprehension dictComp => CheckDictComprehension(dictComp),
        ConditionalExpression cond => CheckConditionalExpression(cond),
        LambdaExpression lambda => CheckLambda(lambda),
        TypeCast cast => CheckTypeCast(cast),
        TypeCoercion coercion => CheckTypeCoercion(coercion),
        TypeCheck typeCheck => CheckTypeCheck(typeCheck),
        Parenthesized paren => CheckExpression(paren.Expression),
        _ => SemanticType.Unknown
    };

    // Cache the result
    _semanticInfo.SetExpressionType(expr, type);
    return type;
}
```

**Design Pattern**: This is a **caching dispatcher** using C# pattern matching:
1. **Check cache first** - Expressions are often checked multiple times (during validation pipeline passes, type narrowing, control flow analysis), so caching avoids redundant work
2. **Pattern match** on the expression type to route to specialized handlers
3. **Cache the result** before returning to `SemanticInfo`

**Performance Note**: The caching is critical for performance. A single expression might be checked multiple times during the semantic analysis phase. Without caching, the type checker would recompute types exponentially in nested expressions.

**Supported Expression Types** (30+ types):
- **Literals**: IntegerLiteral, FloatLiteral, StringLiteral, BooleanLiteral, NoneLiteral
- **Collections**: ListLiteral, DictLiteral, SetLiteral, TupleLiteral
- **Comprehensions**: ListComprehension, SetComprehension, DictComprehension
- **Operators**: BinaryOp, UnaryOp, ComparisonChain
- **Access**: Identifier, MemberAccess, IndexAccess, SuperExpression
- **Calls**: FunctionCall
- **Control**: ConditionalExpression, LambdaExpression
- **Type Operations**: TypeCast, TypeCoercion, TypeCheck
- **Other**: Parenthesized (recursively unwrapped)

---

### Identifiers and Literals

#### `CheckIdentifier(Identifier id)` (lines 54-96)

**Purpose**: Resolves an identifier to its type by looking it up in the symbol table. Handles special cases like `self` validation and type narrowing.

**Special Cases**:

1. **`self` validation** (lines 57-66): The `self` keyword can only be used inside instance methods
   ```python
   class MyClass:
       def method(self):
           x = self.field  # Valid

   x = self  # Error: 'self' can only be used inside instance methods
   ```

2. **Type narrowing** (lines 79-82): If the identifier has been narrowed by a type check (e.g., `isinstance(x, int)` in a conditional), return the narrowed type
   ```python
   x: object = get_value()
   if isinstance(x, int):
       y = x + 1  # x is narrowed to int here
   ```

3. **Module symbols**: Returns a `ModuleType` for imported modules
   ```python
   import math
   math.sqrt(4)  # 'math' is an Identifier with ModuleType
   ```

**Symbol Type Handling**:
```csharp
return symbol switch
{
    VariableSymbol varSymbol => varSymbol.Type,
    FunctionSymbol funcSymbol => new FunctionType
    {
        ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
        ReturnType = funcSymbol.ReturnType
    },
    ModuleSymbol moduleSymbol => new ModuleType { Symbol = moduleSymbol },
    TypeSymbol => SemanticType.Unknown, // Type names as values need special handling
    _ => SemanticType.Unknown
};
```

**Important**: `TypeSymbol` returns `Unknown` because type names like `int` or `MyClass` can't be used as values directly (except in constructor calls or generics, which are handled by `CheckFunctionCall` and `CheckIndexAccess`).

**Connection to SemanticInfo**: This method stores the resolved symbol in `_semanticInfo.SetIdentifierSymbol(id, symbol)` for use by code generation.

---

### Operators

#### `CheckBinaryOp(BinaryOp binOp)` (lines 98-130)

**Purpose**: Type checks binary operators (e.g., `+`, `-`, `*`, `<`, `and`, `|>`).

**Algorithm**:
1. Special handling for pipe forward operator (delegates to `CheckPipeForward`)
2. Check left and right operand types
3. If either is `Unknown`, return `Unknown` to **avoid cascading errors**
4. Delegate to `TypeInferenceService.InferBinaryOpType()` for the actual type inference
5. If type inference fails (returns null), report error directly

**Key Change from Legacy**: Previously delegated to `OperatorValidator`. Now uses `TypeInferenceService` for clean separation of type inference (this class) from validation (pipeline validators).

**Cascading Error Prevention** (lines 109-113):
```csharp
// If either operand is Unknown, return Unknown to avoid cascading errors
if (leftType is UnknownType || rightType is UnknownType)
{
    return SemanticType.Unknown;
}
```

**Why Report Errors Directly?** (lines 120-126): The comment notes that "V2 validators may not catch all type incompatibilities", so this method reports errors when `InferBinaryOpType` returns null.

**Examples**:
```python
x = 1 + 2        # Int + Int → Int
y = "a" + "b"    # Str + Str → Str
z = 1 + "a"      # Error: Type 'int' does not support operator '+' with operand of type 'str'
```

---

#### `CheckPipeForward(BinaryOp binOp)` (lines 133-238)

**Purpose**: Type checks the pipe forward operator: `x |> f` becomes `f(x)`.

**Syntax Variants**:
```python
x |> f          # f(x) - simple pipe
x |> f(y)       # f(x, y) - prepend x to args
x |> f |> g     # g(f(x)) - chaining via left-associativity
```

**Implementation Strategy**:

1. **Case 1** (lines 147-152): Right side is already a function call → delegate to `CheckPipeForwardWithFunctionCall()`
   ```python
   x |> f(y, z)  # Validate as f(x, y, z)
   ```

2. **Case 2** (lines 155-232): Right side is a simple identifier or expression → validate that it's callable and accepts the piped value as first parameter

**Validation for Simple Pipe** (Case 2):

- Check if right side is a `FunctionType` (lines 163-181):
  - Must accept at least 1 parameter
  - Piped value type must be assignable to first parameter type
  - Returns the function's return type

- Check if right side is an `Identifier` (lines 184-232):
  - Lookup the function symbol
  - Validate required parameter count (must be 1 when piping without additional args)
  - Validate piped value type matches first parameter
  - Check remaining parameters have defaults (lines 210-215)

**Current Limitations**:
- Piping to constructors (`x |> SomeClass()`) is not yet supported (lines 220-226)
- Constructor calls would require special handling for `__init__` methods

**Error Messages**: Notice how error messages are specific to pipe forward context for better developer experience.

---

#### `CheckPipeForwardWithFunctionCall(...)` (lines 240-384)

**Purpose**: Handles `x |> f(y, z)` by prepending the piped value to the argument list: `f(x, y, z)`.

**Complex Validation Steps**:

1. **Get the function being called** (line 247)
2. **Collect existing argument types** (lines 250-254) - both positional args
3. **Collect keyword argument types** (lines 257-261)
4. **Build full argument list** (lines 264-265): `[pipedType] + existingArgTypes`
5. **Calculate total argument count** (line 268): positional + keyword

**Function Symbol Validation** (lines 271-338):

When the function is an `Identifier` with a `FunctionSymbol`:
- Validate total argument count (including piped value) against function signature
- Validate each positional argument type (lines 297-309)
  - Special error message for "piped value" vs "argument N" for clarity
- Validate keyword arguments (lines 312-335)
  - Check parameter exists
  - Check not already provided positionally (accounting for piped value position)
  - Validate type compatibility

**Keyword Argument Position Checking** (lines 323-327):
```csharp
var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
if (paramIndex < allArgTypes.Count)
{
    AddError($"Argument '{kwarg.Name}' was already provided positionally", ...);
}
```
This prevents errors like:
```python
def f(x: int, y: int): pass
value |> f(y=10)  # Error: 'y' already provided positionally (via pipe)
```

**Fallback to FunctionType** (lines 357-379): If no symbol found, use `FunctionType` validation (less detailed, no default parameter support).

**Error Reporting**: Note how error positions point to different locations:
- Piped value errors → `binOp.Left` position
- Argument errors → `call.Arguments[i]` position
- Keyword argument errors → `kwarg` position

---

#### `CheckUnaryOp(UnaryOp unOp)` (lines 386-410)

**Purpose**: Type checks unary operators (e.g., `-`, `+`, `not`, `~`).

**Algorithm**:
1. Check operand type
2. If operand is `Unknown`, return `Unknown` (cascade prevention)
3. Use `TypeInferenceService.InferUnaryOpType()` for type inference
4. If inference fails, report error directly

**Examples**:
```python
x = -5       # Unary minus on int → int
y = not True # Logical not on bool → bool
z = ~0b1010  # Bitwise not on int → int
```

**Similar to Binary Operators**: Uses the same pattern of delegating to `TypeInferenceService` and reporting errors when inference fails.

---

#### `CheckComparisonChain(ComparisonChain chain)` (lines 412-465)

**Purpose**: Type checks chained comparisons like `a < b < c`.

**Comparison Chain Structure**:
```python
a < b < c
# Operands: [a, b, c]
# Operators: [LessThan, LessThan]
# Validates: (a < b) AND (b < c)
```

**Algorithm**:
1. Validate chain structure: operators count = operands count - 1 (lines 421-425)
2. Type check all operands (lines 428-432)
3. Validate each adjacent pair using `TypeInferenceService` (lines 435-461)
   - Skip validation if either operand is `Unknown` (cascade prevention)
   - Convert `ComparisonOperator` to `BinaryOperator` for validation
   - Report errors if type inference fails

**TODO Note** (lines 447-450): The conversion from `ComparisonOperator` to `BinaryOperator` uses an obsolete `OperatorValidator` utility method. This should be moved to a utility class.

**Result**: All comparison chains return `SemanticType.Bool`.

---

### Member Access and Indexing

#### `CheckMemberAccess(MemberAccess memberAccess)` (lines 467-562)

**Purpose**: Type checks member access expressions like `obj.field`, `obj.method`, `module.function`.

**Key Features**:

1. **Super Expression Handling** (lines 470-473):
   ```python
   super().method()  # Validated by ValidateSuperMemberAccess
   ```
   Delegates to `ValidateSuperMemberAccess()` (defined in TypeChecker.Utilities.cs).

2. **Null Conditional Operator** (`?.`) (lines 478-491):
   ```python
   x?.field  # Only works on nullable types
   ```
   - Validates the object is nullable
   - Uses underlying type for member lookup
   - Wraps result in nullable type

3. **Module Member Access** (lines 494-516):
   ```python
   config.MAX_SIZE     # Module variable
   utils.helper()      # Module function
   submodule.Type      # Nested module or type
   ```
   - Looks up exported symbols from the module's export dictionary
   - Returns appropriate type based on symbol kind (variable, function, type, nested module)

4. **Class/Struct Member Access** (lines 518-559):
   - Searches for fields using `FindFieldInHierarchy()` (includes inherited fields)
   - Searches for methods using `FindMethodInHierarchy()` (includes inherited methods and interface methods)
   - Access level validation delegated to `AccessValidator` in the validation pipeline

**Method Binding** (lines 542-550):
When accessing a method via `obj.method`, the object is implicitly bound as `self`:
```csharp
// Skip first parameter (self) when creating FunctionType for bound methods
var paramTypes = method.Parameters.Skip(1).Select(p => p.Type).ToList();
```

**Null Conditional with Methods**: For `obj?.method()`, the method `FunctionType` itself is not wrapped in nullable, but the eventual call result will be (handled in `CheckFunctionCall`).

**Connection to Validation Pipeline**: Access level validation (public/private/protected) is handled by `AccessValidator` in the validation pipeline, not here.

---

#### `FindFieldInHierarchy(TypeSymbol type, string fieldName)` (lines 567-585)

**Purpose**: Searches for a field by name in a type's hierarchy (including parent classes).

**Algorithm**:
1. Check the type itself
2. Walk up the base class chain
3. Return the field AND its declaring type (owner)

**Why Return the Owner?** Access level validation needs to know where the field was declared:
- Private fields in a base class are not accessible from derived classes
- Protected fields in a base class are accessible from derived classes
- Public fields are accessible everywhere

**Does Not Check Interfaces**: Fields are not part of interface contracts in Sharpy.

**Usage**: Called by `CheckMemberAccess()` at line 521.

---

#### `FindMethodInHierarchy(TypeSymbol type, string methodName)` (lines 590-616)

**Purpose**: Searches for a method by name in a type, base classes, and interfaces.

**Search Order**:
1. The type itself (lines 593-595)
2. Base class chain (lines 598-604)
3. Implemented interfaces (lines 607-613)

**Interface Methods**: This allows calling interface methods on concrete types that implement them.

**Example**:
```python
interface IDrawable:
    def draw(self) -> None: ...

class Circle(IDrawable):
    def draw(self) -> None: ...

c = Circle()
c.draw()  # Finds method in Circle (not in IDrawable)

d: IDrawable = Circle()
d.draw()  # Finds method in IDrawable contract
```

**Why Return the Owner?** Same reason as `FindFieldInHierarchy` - for access validation and understanding the inheritance chain.

**Usage**: Called by `CheckMemberAccess()` at line 537.

---

#### `CheckIndexAccess(IndexAccess indexAccess)` (lines 618-677)

**Purpose**: Type checks indexing operations like `list[0]`, `dict[key]`, `Box[int]`.

**Special Cases** (in order of checking):

1. **Type Narrowing** (lines 621-625):
   ```python
   if isinstance(x, dict):
       y = x[key]  # x is narrowed to dict
   ```
   Uses `ExtractNarrowingKey()` to check for narrowed types.

2. **Generic Type Instantiation** (lines 632-649):
   ```python
   Box[int]        # Generic type with single type argument
   Pair[int, str]  # Generic type with multiple type arguments
   ```
   - Parsed as `IndexAccess(Object: Identifier("Box"), Index: int or TupleLiteral)`
   - Checks if object is a generic `TypeSymbol`
   - Uses `TryResolveTypeArguments()` to resolve type arguments
   - Returns a `GenericType` with resolved type arguments

3. **Generic Function Instantiation** (lines 652-666):
   ```python
   identity[int]      # Generic function with explicit type argument
   map[int, str]      # Generic function with multiple type arguments
   ```
   - Checks if object is a generic `FunctionSymbol`
   - Stores type arguments in `SemanticInfo` as `GenericFunctionType`
   - Used later by `CheckFunctionCall` for type substitution

4. **Regular Indexing** (lines 669-676):
   - Check object and index types
   - Use `TypeInferenceService.InferIndexAccessType()` for type inference
   - Returns inferred element type or `Unknown` for unsupported operations

**Type Argument Resolution**: The helper `TryResolveTypeArguments()` (lines 1087-1110) handles both single type arguments and tuple-based multiple type arguments.

**Connection to Code Generation**: The distinction between generic type instantiation and regular indexing is critical for code generation - the former generates C# generic syntax, the latter generates indexer access.

---

### Function Calls

#### `CheckFunctionCall(FunctionCall call)` (lines 679-1038)

**Purpose**: Type checks function calls including regular functions, methods, constructors, built-in functions with overloads, and generic functions.

**Complexity**: This is one of the most complex methods in the file (360+ lines) because it handles many different calling conventions and features.

**Pre-Processing Steps**:

1. **Null Conditional Call Detection** (line 682):
   ```python
   obj?.method()  # Returns nullable result
   ```
   Checked by examining if the function expression is a `MemberAccess` with `IsNullConditional`.

2. **Check Called Expression** (line 685): Get the type of what's being called.

3. **Super Init Call Tracking** (lines 688-692):
   ```python
   class Derived(Base):
       def __init__(self):
           super().__init__()  # Tracked for validation
   ```
   Sets `_superInitCalled = true` for constructor validation.

4. **Collect Argument Types** (lines 695-706):
   - Positional arguments → `argTypes` list
   - Keyword arguments → `kwargTypes` dictionary
   - Calculate total argument count

**Special Case Handling** (in processing order):

1. **Generic Type Instantiation** (lines 716-741):
   ```python
   Box[int](42)         # Constructor for generic type
   Pair[int, str](1, "a")  # Multiple type arguments
   ```
   - Detects pattern: `FunctionCall(Function: IndexAccess(...))`
   - Validates type symbol is generic
   - Resolves type arguments
   - Validates abstract classes cannot be instantiated
   - Returns `GenericType` instance

2. **Generic Function Call** (lines 744-754):
   ```python
   identity[int](42)  # Generic function with explicit type argument
   ```
   - Detects `GenericFunctionType` from `CheckIndexAccess`
   - Performs type parameter substitution in return type
   - Returns substituted return type

3. **Built-in `len()` Function** (lines 758-767):
   ```python
   len([1, 2, 3])  # Built-in with protocol validation
   ```
   - Special handling for `len()` to validate `__len__` protocol
   - Uses `TypeInferenceService.InferLenType()`
   - TODO comment suggests using constant instead of hardcoded string

4. **Constructor Calls** (lines 772-796):
   ```python
   MyClass(arg1, arg2)  # Instantiate user-defined type
   int("123")           # Primitive type conversion (routed to builtin overloads)
   ```
   - Detects when callee symbol is a `TypeSymbol`
   - For primitive types (`int`, `float`, etc.), routes to builtin function overloads
   - For other types, validates abstract classes cannot be instantiated
   - Returns `UserDefinedType` instance

5. **Built-in Function Overload Resolution** (lines 809-894):
   ```python
   range(10)           # Single argument overload
   range(0, 10)        # Two argument overload
   range(0, 10, 2)     # Three argument overload
   ```
   - Handles built-in functions with multiple signatures
   - First pass: filter by argument count (considering defaults and variadic params)
   - Second pass: check type compatibility for each candidate
   - Selects first matching overload
   - Updates `SemanticInfo` to point to selected overload
   - Reports error if no matching overload found

6. **Module Function Calls** (lines 898-926):
   ```python
   module.function(args)  # Call exported function from module
   ```
   - Re-evaluates object to get module type
   - Looks up function symbol from module exports
   - Stores as `funcSymbol` for detailed validation below

**Detailed Validation with FunctionSymbol** (lines 929-996):

When a `FunctionSymbol` is available (user-defined or selected builtin overload):
- Count required parameters (excluding defaults)
- Validate total argument count against parameter count (lines 936-948)
- Validate positional argument types (lines 952-959)
- Validate keyword arguments (lines 962-985):
  - Check parameter name exists
  - Check not already provided positionally
  - Validate type compatibility
- Apply null conditional wrapping if needed (lines 991-994)

**Fallback Validation with FunctionType** (lines 998-1035):

When only a `FunctionType` is available (e.g., lambda, function returned from expression):
- Skip validation if `SkipArgumentValidation` flag is set (for .NET constructor overloads)
- Validate argument count exactly matches parameter count
- Validate positional argument types
- Apply null conditional wrapping if needed

**Error Messages**: Distinguishes between "expects N arguments" (exact) and "expects N to M arguments" (with defaults) for clarity.

**Connection to Generics**: Generic function type substitution happens at lines 749-753, using the `SubstituteTypeParameters` helper (defined in TypeChecker.Utilities.cs).

---

### Collections and Literals

#### `CheckListLiteral(ListLiteral list)` (lines 1112-1141)

**Purpose**: Type checks list literals like `[1, 2, 3]` and infers their element type.

**Type Inference**:

1. **Empty list** (lines 1114-1121):
   ```python
   x = []  # list[Unknown]
   ```
   Returns `list[Unknown]` - the element type is unknown until more context is available.

2. **Non-empty list** (lines 1123-1134):
   ```python
   x = [1, 2, 3]    # list[int]
   y = [1, 2, 3.14] # list[Unknown] if types don't match
   ```
   - Check all element types
   - Use first element type as "common type"
   - Validate remaining elements are assignable to common type
   - If any element is not assignable, use `Unknown`

**Limitation**: Uses simple assignability check. A more sophisticated algorithm would find the least common supertype:
```python
# Current behavior:
x = [1, 3.14]  # list[Unknown]

# Better behavior would be:
x = [1, 3.14]  # list[double] (int is assignable to double)
```

**Returns**: `GenericType` with name "list" and single type argument.

---

#### `CheckDictLiteral(DictLiteral dict)` (lines 1143-1165)

**Purpose**: Type checks dictionary literals like `{"key": "value"}`.

**Type Inference**:

1. **Empty dict** (lines 1145-1152):
   ```python
   x = {}  # dict[Unknown, Unknown]
   ```

2. **Non-empty dict** (lines 1154-1164):
   ```python
   x = {"a": 1, "b": 2}  # dict[str, int]
   ```
   - Check all key types and value types
   - Use first entry's types as common types
   - No validation that all entries match (limitation)

**Limitation**: Like lists, this is simplified. A production compiler would:
- Find common key type across all entries
- Find common value type across all entries
- Report errors if types are incompatible

**Returns**: `GenericType` with name "dict" and two type arguments (key type, value type).

---

#### `CheckSetLiteral(SetLiteral set)` (lines 1167-1186)

**Purpose**: Type checks set literals like `{1, 2, 3}`.

**Type Inference**: Similar to `CheckListLiteral()`:
- Empty set → `set[Unknown]`
- Non-empty set → infer element type from elements

**Note**: Empty set literal `{}` is parsed as `DictLiteral`, not `SetLiteral`, to match Python semantics. Explicit `set()` is needed for empty sets.

**Returns**: `GenericType` with name "set" and single type argument.

---

#### `CheckTupleLiteral(TupleLiteral tuple)` (lines 1188-1192)

**Purpose**: Type checks tuple literals like `(1, "a", True)`.

**Type Inference**:
- Check all element types
- Return `TupleType` with element types list

**Contrast with Lists**: Tuples are **heterogeneous** (can contain different types) and **immutable**. The type system tracks the type of each element position:
```python
x = (1, "a", True)  # TupleType with [int, str, bool]
y = [1, "a", True]  # list[Unknown] (incompatible element types)
```

**Returns**: `TupleType` with list of element types (preserves order and individual types).

---

### Comprehensions

All comprehension checkers follow a similar pattern:
1. Enter new scope (variables don't leak)
2. Process clauses (for/if) in order
3. Define loop variables with inferred types
4. Validate filter conditions are boolean
5. Check element/key/value expression(s)
6. Exit scope
7. Return generic collection type

#### `CheckListComprehension(ListComprehension listComp)` (lines 1194-1254)

**Purpose**: Type checks list comprehensions like `[x * 2 for x in range(10) if x > 5]`.

**Scope Management** (lines 1197, 1247):
```csharp
_symbolTable.EnterScope("list-comprehension");
// ... define loop variables, check expressions
_symbolTable.ExitScope();
```
Variables defined in comprehensions don't leak to outer scope.

**Clause Processing** (lines 1200-1242):

1. **For Clause** (lines 1202-1230):
   ```python
   for x in iterable
   ```
   - Check iterator type
   - Infer element type using `TypeInferenceService.InferIterableElementType()`
   - Define loop variable with element type
   - Store symbol and type in `SemanticInfo`
   - **Limitation**: Tuple unpacking not supported yet (lines 1226-1229)

2. **If Clause** (lines 1232-1241):
   ```python
   if condition
   ```
   - Check condition type
   - Validate it's boolean (report error if not)

**Element Expression** (line 1245): Check the expression that produces each element.

**Result**: Returns `GenericType` named "list" with element type.

**TODO Note** (lines 1226-1229): Tuple unpacking in comprehensions is not yet supported:
```python
# Not yet supported:
[(x + y) for x, y in pairs]
```

---

#### `CheckSetComprehension(SetComprehension setComp)` (lines 1256-1315)

**Purpose**: Type checks set comprehensions like `{x * 2 for x in range(10)}`.

**Implementation**: Nearly identical to `CheckListComprehension()` but returns `set[elementType]` instead of `list[elementType]`.

**Code Duplication Note**: This is duplicated code. A refactoring opportunity would be to extract a shared `CheckComprehension()` helper that takes a collection type parameter.

---

#### `CheckDictComprehension(DictComprehension dictComp)` (lines 1317-1377)

**Purpose**: Type checks dict comprehensions like `{x: x*2 for x in range(10)}`.

**Key Difference from List/Set**: Validates both key and value expressions (lines 1367-1368):
```python
{key_expr: value_expr for x in iterable}
```

**Result**: Returns `GenericType` named "dict" with key type and value type.

**Same Limitations**: No tuple unpacking support in for clauses.

---

### Conditional and Lambda Expressions

#### `CheckConditionalExpression(ConditionalExpression cond)` (lines 1379-1392)

**Purpose**: Type checks ternary expressions like `x if condition else y`.

**Type Inference** (lines 1381-1390):
1. Check test condition (type not validated - could be improved)
2. Check then-value type
3. Check else-value type
4. **Find common type**:
   - If `thenType` is assignable to `elseType` → return `elseType`
   - If `elseType` is assignable to `thenType` → return `thenType`
   - Otherwise → return `Unknown`

**Examples**:
```python
result = 42 if flag else 3.14    # Type: double (int assignable to double)
result = "a" if flag else "b"    # Type: str (str assignable to str)
result = 1 if flag else "a"      # Type: Unknown (incompatible)
```

**Improvement Opportunity**: Could use least common supertype algorithm for better type inference.

---

#### `CheckLambda(LambdaExpression lambda)` (lines 1394-1425)

**Purpose**: Type checks lambda expressions like `lambda x: int, y: int -> x + y`.

**Scope Management** (lines 1401, 1418):
```csharp
_symbolTable.EnterScope("lambda");
// ... define parameters, check body
_symbolTable.ExitScope();
```

**Parameter Processing** (lines 1403-1414):
1. Resolve parameter type annotations
2. Create parameter symbols
3. Mark as parameters (not variables)
4. Define in symbol table

**Body Type Inference** (line 1416): Check lambda body expression to infer return type.

**Result**: Returns `FunctionType` with:
- Parameter types from annotations
- Return type inferred from body expression

**Contrast with Named Functions**:
- Lambdas have **expression bodies** (single expression, no statements)
- Return type is **always inferred** (no explicit annotation)
- Parameters **must have type annotations** (no inference)

---

### Type Operations

#### `CheckTypeCast(TypeCast cast)` (lines 1427-1431)

**Purpose**: Type checks cast expressions like `x as SomeType`.

**Implementation**:
1. Check value expression (type is not used for validation)
2. Resolve target type annotation
3. Return target type

**No Validation**: This is an **unchecked cast** - it trusts the programmer. Runtime checks will be inserted by code generation.

**Use Case**: For bypassing type system when you know better than the compiler:
```python
obj: object = get_value()
s: str = obj as str  # Unchecked cast - will throw at runtime if wrong
```

---

#### `CheckTypeCoercion(TypeCoercion coercion)` (lines 1437-1455)

**Purpose**: Type checks type coercion expressions like `value to Type`.

**Validation Steps**:
1. Check source type of value expression
2. Resolve target type annotation
3. If either is `Unknown`, skip validation (cascade prevention)
4. Strip nullable wrapper from target type if present
5. Delegate to `ValidateTypeCoercion()` for detailed validation
6. Return target type

**Difference from TypeCast**: Coercion has **stricter validation** - only allows conversions that make sense semantically.

---

#### `ValidateTypeCoercion(...)` (lines 1461-1497)

**Purpose**: Validates that a type coercion is valid per language specification. Reports errors for invalid coercions.

**Validation Rules** (in order of checking):

1. **Unboxing** (lines 1464-1467):
   ```python
   obj: object = get_value()
   x: int = obj to int  # Valid - runtime check
   ```
   From `object` to any type is always valid (runtime check).

2. **Numeric Conversions** (lines 1470-1473):
   ```python
   x: int = 42
   y: double = x to double  # Valid numeric conversion
   ```
   All numeric-to-numeric conversions are valid (may throw at runtime for narrowing).

3. **Invalid Primitive to String** (lines 1476-1488):
   ```python
   x: int = 42
   s: str = x to str  # Error: Use str(x) instead
   ```
   Rejects primitive-to-string conversions (should use `str()` function).

4. **Reference Type Casts** (lines 1491-1496):
   ```python
   derived: Derived = get_value()
   base: Base = derived to Base  # Valid if inheritance relationship
   ```
   Checks for inheritance relationship or interface implementation using `CanPotentiallyCast()`.

**Philosophy**: Coercion validates at compile-time that the conversion **could** succeed at runtime. It doesn't guarantee success (e.g., unboxing might fail), but it rejects statically impossible conversions.

---

#### `CanPotentiallyCast(SemanticType source, SemanticType target)` (lines 1520-1572)

**Purpose**: Determines if a cast between two types **could potentially succeed at runtime**. Used by coercion validation.

**Returns**:
- `true` if there's an inheritance relationship, interface implementation, or unboxing potential
- `false` if the cast is statically impossible

**Validation Cases**:

1. **Same Type** (lines 1523-1524): Always castable
2. **User-Defined Types** (lines 1527-1547):
   - Check if source inherits from target (downcast)
   - Check if target inherits from source (upcast - needs runtime check)
   - Check if target is an interface (could be implemented)
   - Check if source is an interface (target could implement it)
3. **Interface Casting** (lines 1550-1551): Always potentially valid at runtime
4. **Unboxing from Object** (lines 1554-1555): Valid (object can contain any type)
5. **Boxing to Object** (lines 1558-1559): Valid (all types can be boxed)
6. **Generic Types** (lines 1562-1567): Check if same generic definition
7. **Default** (lines 1570-1571): Conservative - allow (C# compiler does final validation)

**Inheritance Check**: Uses `InheritsFrom()` helper (lines 1577-1598) to walk base class and interface chains.

---

#### `InheritsFrom(TypeSymbol? derived, TypeSymbol? baseType)` (lines 1577-1598)

**Purpose**: Checks if a type symbol inherits from another type symbol (directly or indirectly).

**Algorithm**:
1. Walk up base class chain
2. Check each base class by equality or name match
3. Also check implemented interfaces

**Name Match**: Checks both symbol equality AND name equality to handle cases where symbols might be different instances.

**Returns**: `true` if inheritance relationship found, `false` otherwise.

---

#### `CheckTypeCheck(TypeCheck typeCheck)` (lines 1600-1605)

**Purpose**: Type checks `isinstance()` expressions like `x is SomeType`.

**Implementation**:
1. Check value expression
2. Resolve check type annotation
3. Return `bool`

**Result**: Always returns `SemanticType.Bool`.

**Type Narrowing Connection**: While this method just returns bool, the `TypeChecker.Utilities.cs` file contains logic (`ExtractNarrowedTypes`) to narrow types based on `isinstance` checks in conditional contexts:
```python
x: object = get_value()
if x is int:
    y = x + 1  # x is narrowed to int here
```

---

## Helper Methods

### `TryResolveExpressionAsType(Expression expr)` (lines 1045-1080)

**Purpose**: Attempts to interpret an expression as a type reference (for generic type syntax).

**Use Case**: When parsing `Box[int]`, the `int` is initially parsed as an `Identifier` expression, but we need to resolve it as a type.

**Supported Cases**:

1. **Simple Identifier** (lines 1048-1059):
   ```python
   Box[int]  # "int" is Identifier → resolve as type
   ```
   Creates synthetic `TypeAnnotation` and resolves via `_typeResolver`.

2. **Nested Generic** (lines 1062-1077):
   ```python
   Container[Box[int]]  # Box[int] is IndexAccess → resolve recursively
   ```
   Recursively resolves inner generic type.

**Returns**:
- `SemanticType` if successful
- `null` if the expression isn't a valid type

**Connection to Generic Instantiation**: Used by `CheckIndexAccess` (line 639) and `TryResolveTypeArguments` (line 1096).

---

### `TryResolveTypeArguments(Expression indexExpr)` (lines 1087-1110)

**Purpose**: Resolves type arguments from an index expression (handles both single and multiple type args).

**Supported Syntaxes**:

1. **Multiple Type Arguments** (lines 1092-1101):
   ```python
   Pair[int, str]  # Parsed as TupleLiteral(int, str)
   ```
   Iterates tuple elements, resolving each as a type.

2. **Single Type Argument** (lines 1104-1109):
   ```python
   Box[int]  # Single identifier
   ```
   Resolves single expression as a type.

**Returns**:
- `List<SemanticType>` if all type arguments resolved successfully
- `null` if any expression can't be interpreted as a type

**Error Handling**: Returns `null` on failure rather than reporting errors - the caller decides whether to report an error or treat as regular indexing.

---

### `IsNumericType(SemanticType type)` (lines 1612-1613)

**Purpose**: Check if a type is numeric (int, long, float, double, etc.).

**Implementation**: Delegates to `PrimitiveCatalog.IsNumeric()` for exhaustive primitive type checking.

**Special Case**: Also allows `UnknownType` to avoid cascading errors.

**Usage**: Internal helper for numeric operation validation.

---

### `GetOperatorSymbol(BinaryOperator op)` (lines 1622-1651)

**Purpose**: Gets the human-readable symbol for a binary operator.

**Use Case**: Error messages need to display operator symbols, not enum names.

**Examples**:
```csharp
BinaryOperator.Add → "+"
BinaryOperator.FloorDivide → "//"
BinaryOperator.PipeForward → "|>"
BinaryOperator.NullCoalesce → "??"
```

**Exhaustive Mapping**: Covers all 25+ binary operators with default fallback to `ToString()`.

---

### `GetOperatorSymbol(UnaryOperator op)` (lines 1656-1663)

**Purpose**: Gets the human-readable symbol for a unary operator.

**Examples**:
```csharp
UnaryOperator.Minus → "-"
UnaryOperator.Not → "not"
UnaryOperator.BitwiseNot → "~"
```

---

## Additional Helper Methods (in TypeChecker.Utilities.cs)

Some methods called from this file are defined in `TypeChecker.Utilities.cs`:

- **`SubstituteTypeParameters()`**: Substitutes type parameters with type arguments for generic function calls
- **`ValidateSuperMemberAccess()`**: Validates `super().method()` calls
- **`CheckSuperExpression()`**: Type checks `super()` expression
- **`ExtractNarrowingKey()`**: Extracts a key for type narrowing from expressions
- **`IsAssignable()`**: Checks if one type is assignable to another

See [TypeChecker.Utilities.md](TypeChecker.Utilities.md) for details on these methods.

---

## Dependencies

### Internal Dependencies

- **`SymbolTable`**: Symbol lookup for identifiers and types
- **`SemanticInfo`**: Stores computed type information for AST nodes
- **`TypeInferenceService`**: Infers result types of operations (operators, indexing, iteration)
- **`TypeResolver`**: Resolves type annotations to semantic types
- **`ValidationPipeline`**: Runs specialized validators (access control, protocols, operators, control flow)
- **`PrimitiveCatalog`**: Provides information about primitive types (numeric, string, bool)
- **`BuiltinRegistry`**: Registry of built-in functions with overloads

### AST Node Types

Depends on expression node types from `Sharpy.Compiler.Parser.Ast`:

- **Literals**: `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`, `NoneLiteral`
- **Collections**: `ListLiteral`, `DictLiteral`, `SetLiteral`, `TupleLiteral`
- **Comprehensions**: `ListComprehension`, `SetComprehension`, `DictComprehension`, `ForClause`, `IfClause`
- **Operators**: `BinaryOp`, `UnaryOp`, `ComparisonChain`
- **Access**: `Identifier`, `MemberAccess`, `IndexAccess`, `SuperExpression`
- **Calls**: `FunctionCall`, `KeywordArgument`
- **Control**: `ConditionalExpression`, `LambdaExpression`
- **Type Operations**: `TypeCast`, `TypeCoercion`, `TypeCheck`
- **Other**: `Parenthesized`

---

## Patterns and Design Decisions

### 1. **Caching Pattern**

Every expression type is cached in `SemanticInfo` to avoid redundant type checking:
```csharp
// Check cache first
var cached = _semanticInfo.GetExpressionType(expr);
if (cached != null)
    return cached;

// ... compute type ...

// Cache before returning
_semanticInfo.SetExpressionType(expr, type);
return type;
```

**Why?** Expressions may be checked multiple times:
- During initial type checking
- During validation pipeline passes
- During type narrowing analysis
- When referenced by multiple statements

**Performance Impact**: Without caching, the type checker would recompute types exponentially for nested expressions.

---

### 2. **Error Cascade Prevention**

When an operand has type `Unknown`, methods return `Unknown` immediately without further validation:
```csharp
if (leftType is UnknownType || rightType is UnknownType)
{
    return SemanticType.Unknown;
}
```

**Why?** Prevents cascading error messages that would confuse users. If `x` is undefined, don't also report that `x + 1` is invalid.

---

### 3. **Delegation to TypeInferenceService**

Rather than embedding type inference rules in this file, it delegates to `TypeInferenceService`:
```csharp
var resultType = _typeInference.InferBinaryOpType(binOp.Operator, leftType, rightType);
```

**Benefits**:
- **Separation of concerns**: Type checking (this file) vs. type inference (service)
- **Centralized type rules**: All type compatibility logic in one place
- **Easier testing**: Can test type inference independently
- **Reusability**: Other components can use `TypeInferenceService`

---

### 4. **Validation Pipeline Integration**

The V2 validation pipeline handles specialized checks:
- **OperatorValidator**: Operator overload validation
- **ProtocolValidator**: Protocol support validation (`__iter__`, `__getitem__`, `__len__`)
- **AccessValidator**: Access level validation (public/private/protected)
- **ControlFlowValidator**: Control flow validation (unreachable code, missing returns)

**This File's Role**: Type checking and type inference only. Detailed validation is done by the pipeline.

**Error Reporting**: Some errors are reported directly in this file when type inference fails, since "V2 validators may not catch all type incompatibilities" (line 119 comment).

---

### 5. **Scope Management for Nested Contexts**

Comprehensions and lambdas create new scopes:
```csharp
_symbolTable.EnterScope("list-comprehension");
// ... define loop variables
_symbolTable.ExitScope();
```

**Why?** Variables defined in comprehensions and lambdas don't leak to outer scopes (matches Python semantics).

**Example**:
```python
x = [i for i in range(10)]
print(i)  # Error: 'i' is not defined (scoped to comprehension)
```

---

### 6. **Two-Level Validation Strategy**

Many methods first try to get a `FunctionSymbol` (for detailed validation), then fall back to `FunctionType`:
```csharp
if (funcSymbol != null) {
    // Detailed validation with default parameters, keyword args
}
else if (funcType is FunctionType ft) {
    // Fallback validation (no default parameter support)
}
```

**Why?**
- `FunctionSymbol` has full parameter information (names, defaults, types)
- `FunctionType` only has parameter types (used for anonymous functions)

---

### 7. **Pattern Matching for Type Dispatch**

The main `CheckExpression()` uses C# pattern matching for clean, exhaustive handling:
```csharp
SemanticType type = expr switch
{
    IntegerLiteral => SemanticType.Int,
    Identifier id => CheckIdentifier(id),
    BinaryOp binOp => CheckBinaryOp(binOp),
    // ... 30+ cases
    _ => SemanticType.Unknown
};
```

**Benefits**:
- **Exhaustive**: Compiler warns if new expression types are added but not handled
- **Readable**: Clear mapping from AST node type to handler
- **Maintainable**: Easy to add new expression types

---

### 8. **Generic Type Support**

Generic types and functions are handled through special syntax parsing:
```python
Box[int]           # IndexAccess(Object: "Box", Index: "int")
identity[int](42)  # FunctionCall(Function: IndexAccess(...), Arguments: [42])
```

**Design Decision**: Reuse `IndexAccess` parsing for generic syntax, then distinguish based on:
- Is object a generic type/function symbol?
- Can index be resolved as type argument(s)?

**Benefits**: No parser changes needed for generics. Type checker interprets intent.

---

## Debugging Tips

### 1. **Expression Type Not What You Expected?**

Add a breakpoint in `CheckExpression()` at line 11 and examine:
- The `_semanticInfo.GetExpressionType(expr)` cached value
- The computed type in the switch expression
- Whether caching is preventing a fresh computation

**Tip**: Use `_semanticInfo.DumpExpressionTypes()` to see all cached types.

---

### 2. **Identifier Not Found?**

Check `CheckIdentifier()` at line 54:
- Is the symbol in `_symbolTable.Lookup(id.Name)`?
- Is the scope correct? Use `_symbolTable.DumpScopes()` to inspect
- Was the identifier defined in a narrower scope that's no longer active?

**Common Issues**:
- Variable defined in comprehension or lambda (scoped)
- Import statement not processed
- Name shadowing by local variable

---

### 3. **Type Narrowing Issues?**

Examine `_narrowedTypes` dictionary in `CheckIdentifier()` (line 79):
- Is the narrowed type being set? Check `TypeChecker.Utilities.cs`
- Is the narrowing key correct? See `ExtractNarrowingKey()`
- Was the narrowed type cleared when exiting the conditional branch?

**Tip**: Add logging to track when types are narrowed and un-narrowed.

---

### 4. **Function Call Validation Failing?**

The `CheckFunctionCall()` method is complex (360 lines). Debug systematically:

1. **What is the callee?**
   - FunctionSymbol? (user-defined or builtin)
   - FunctionType? (lambda or function expression)
   - TypeSymbol? (constructor call)
   - GenericFunctionType? (generic function with type args)

2. **Are arguments being collected correctly?**
   - Check `argTypes` list (line 695)
   - Check `kwargTypes` dictionary (line 702)
   - Check `totalArgCount` (line 709)

3. **Is overload resolution working?**
   - For builtins, check candidate filtering (line 820)
   - Check type compatibility checking (line 835)

4. **Are default parameters handled?**
   - Check `requiredParamCount` vs `totalParamCount` (lines 932-933)

---

### 5. **Generic Type Issues?**

Check the generic instantiation flow:

1. **Is the type/function symbol marked generic?**
   - Check `typeSymbol.IsGeneric` or `funcSymbol.IsGeneric`

2. **Are type arguments being resolved?**
   - Add breakpoint in `TryResolveTypeArguments()` (line 1087)
   - Check if expressions are being resolved as types
   - Verify `TryResolveExpressionAsType()` succeeds

3. **Is type substitution working?**
   - For generic functions, check `SubstituteTypeParameters()` (line 749)

---

### 6. **Pipe Forward Not Working?**

Debug pipe forward operators:

1. **Simple pipe** (`x |> f`):
   - Check `CheckPipeForward()` case 2 (line 155)
   - Verify function accepts piped value type

2. **Pipe with call** (`x |> f(y)`):
   - Check `CheckPipeForwardWithFunctionCall()` (line 244)
   - Verify argument list construction (line 264)
   - Check keyword argument validation (line 312)

---

### 7. **Operator Type Errors?**

Check operator validation flow:

1. **Binary operators**:
   - Check `CheckBinaryOp()` (line 98)
   - Verify `TypeInferenceService.InferBinaryOpType()` result
   - Check for `Unknown` operands (cascade prevention)

2. **Unary operators**:
   - Check `CheckUnaryOp()` (line 386)
   - Verify `TypeInferenceService.InferUnaryOpType()` result

3. **Comparison chains**:
   - Check `CheckComparisonChain()` (line 412)
   - Verify each pair is validated separately

---

### 8. **Tracing Type Checking**

Add comprehensive logging:
```csharp
public SemanticType CheckExpression(Expression expr)
{
    _logger.LogDebug($"Checking expression: {expr.GetType().Name} at {expr.LineStart}:{expr.ColumnStart}");

    var cached = _semanticInfo.GetExpressionType(expr);
    if (cached != null)
    {
        _logger.LogDebug($"  → Cached: {cached.GetDisplayName()}");
        return cached;
    }

    // ... compute type ...

    _logger.LogDebug($"  → Computed: {type.GetDisplayName()}");
    _semanticInfo.SetExpressionType(expr, type);
    return type;
}
```

---

## Contribution Guidelines

### Common Changes

#### 1. Adding Support for New Expression Types

**Steps**:
1. Add a new case to the `CheckExpression()` switch (line 18)
2. Create a new `CheckXxx()` method following the existing pattern
3. Ensure caching is handled correctly (check cache, compute, store)
4. Add error reporting for invalid cases
5. Update tests in `Sharpy.Compiler.Tests`

**Example**:
```csharp
// In CheckExpression switch:
SliceExpression slice => CheckSlice(slice),

// New method:
private SemanticType CheckSlice(SliceExpression slice)
{
    var objectType = CheckExpression(slice.Object);
    var startType = slice.Start != null ? CheckExpression(slice.Start) : null;
    var stopType = slice.Stop != null ? CheckExpression(slice.Stop) : null;
    var stepType = slice.Step != null ? CheckExpression(slice.Step) : null;

    // Validate slice indices are integers
    // Use TypeInferenceService for result type
    // Return element type or Unknown
}
```

---

#### 2. Improving Type Inference

**Focus Areas**:
- **List literals**: Implement least common supertype algorithm
- **Dict literals**: Find common key/value types across all entries
- **Conditional expressions**: Better common type finding

**Process**:
1. Modify `CheckListLiteral()`, `CheckDictLiteral()`, etc.
2. Consider creating shared helper for finding common supertypes
3. Add tests for improved inference

**Don't**: Embed type compatibility rules in this file - delegate to `TypeInferenceService`.

---

#### 3. Adding Protocol Support

**Don't add it here** - extend `ProtocolValidator` and `TypeInferenceService` instead:
- This file delegates protocol validation to the validation pipeline
- `TypeInferenceService` handles type inference for protocol operations

**Example**: Adding `__contains__` protocol for `in` operator:
1. Add inference to `TypeInferenceService.InferBinaryOpType()`
2. Add validation to `ProtocolValidator`
3. No changes needed in this file

---

#### 4. Enhancing Error Messages

**Guidelines**:
- Include type information using `GetDisplayName()`
- Include source location (line/column) for precise errors
- Use context-specific terminology (e.g., "piped value" vs "argument")
- Suggest fixes when possible (e.g., "Use str(x) instead")

**Example**:
```csharp
// Good error message:
AddError(
    $"Cannot pass argument of type '{argType.GetDisplayName()}' to parameter '{param.Name}' of type '{param.Type.GetDisplayName()}'",
    argument.LineStart,
    argument.ColumnStart);

// Bad error message:
AddError("Type error", 0, 0);
```

---

#### 5. Adding Builtin Function Overloads

**Steps**:
1. Register overloads in `BuiltinRegistry` (not in this file)
2. Overload resolution in `CheckFunctionCall()` will automatically handle them
3. Add tests for each overload

**Example**: Already handles `range(stop)`, `range(start, stop)`, `range(start, stop, step)`.

---

### Code Conventions

1. **Always check for `UnknownType`** to prevent cascade errors:
   ```csharp
   if (leftType is UnknownType || rightType is UnknownType)
       return SemanticType.Unknown;
   ```

2. **Cache results** in `SemanticInfo` before returning:
   ```csharp
   _semanticInfo.SetExpressionType(expr, type);
   return type;
   ```

3. **Use `AddError()`** with precise line/column information:
   ```csharp
   AddError(message, expr.LineStart, expr.ColumnStart);
   ```

4. **Delegate to services and validators**:
   - Use `TypeInferenceService` for type inference
   - Use `TypeResolver` for type annotation resolution
   - Don't embed type rules directly

5. **Follow the existing pattern**:
   - Return `SemanticType.Unknown` for errors
   - Check cache before computing
   - Store symbols in `SemanticInfo` for code generation

6. **Scope management**:
   - Always pair `EnterScope()` with `ExitScope()`
   - Use descriptive scope names ("lambda", "list-comprehension")

7. **Generic type handling**:
   - Use `TryResolveExpressionAsType()` to interpret expressions as types
   - Check `IsGeneric` flag before treating as generic
   - Store `GenericFunctionType` in `SemanticInfo` for function calls

---

### Testing Considerations

When adding new expression support, ensure tests cover:

1. **Valid cases** with correct type inference:
   ```python
   # test_new_expression_valid.spy
   x = new_expression()
   assert type(x) == ExpectedType
   ```

2. **Error cases** with helpful error messages:
   ```python
   # test_new_expression_error.spy.error
   x = invalid_new_expression()  # Should report specific error
   ```

3. **Edge cases**:
   - Empty collections
   - Null conditionals
   - Generic type instantiation
   - Type narrowing contexts

4. **Interaction with other features**:
   - Used in comprehensions
   - Used in conditionals
   - Used with type narrowing
   - Used in generic contexts

5. **Performance**:
   - Verify caching works correctly
   - Test deeply nested expressions don't cause exponential slowdown

---

## Related Specification Documents

Refer to these language specification documents for semantic rules:

- `docs/language_specification/expressions.md` - Expression syntax and semantics
- `docs/language_specification/operator_precedence.md` - Operator precedence and associativity rules
- `docs/language_specification/type_annotations.md` - Type annotation syntax and resolution
- `docs/language_specification/type_casting.md` - Cast and type check semantics
- `docs/language_specification/type_hierarchy.md` - Type compatibility and inheritance rules
- `docs/language_specification/generics.md` - Generic types and functions

---

## Summary

The `TypeChecker.Expressions` partial class is the **heart of Sharpy's type system**. It:

✅ **Type checks all expression nodes** from the AST using pattern matching dispatch
✅ **Infers types** for literals, collections, comprehensions, and operators
✅ **Validates** operators, function calls, member access, and indexing
✅ **Supports advanced features**: generics, null conditionals, pipe forward, type narrowing
✅ **Delegates** to `TypeInferenceService` for type inference and validation pipeline for specialized checks
✅ **Caches results** in `SemanticInfo` for performance
✅ **Prevents error cascades** by propagating `Unknown` types
✅ **Manages scopes** for comprehensions and lambdas
✅ **Handles builtin overloads** with sophisticated resolution
✅ **Supports type coercion** with compile-time validation

**Key Architectural Points**:
- **Separation of concerns**: Type checking (this file) vs. type inference (`TypeInferenceService`) vs. validation (`ValidationPipeline`)
- **Caching pattern**: Essential for performance with repeated type checking
- **Error cascade prevention**: `Unknown` types stop error propagation
- **Generic support**: Reuses index syntax, interprets based on symbol types
- **Scope management**: Comprehensions and lambdas don't leak variables

Understanding this file is **essential** for:
- Working on Sharpy's type system
- Adding new expression types
- Debugging type inference issues
- Understanding how generics work
- Contributing to operator or protocol support

**Next Steps**: To fully understand the type checking system, also read:
- [TypeChecker.md](TypeChecker.md) - Main class and overall architecture
- [TypeChecker.Utilities.md](TypeChecker.Utilities.md) - Helper methods (IsAssignable, type narrowing, super validation)
- [TypeInferenceService.md](TypeInferenceService.md) - Type inference algorithms
- [Validation/](Validation/) - Specialized validators (operators, protocols, access, control flow)
