# Tagged Unions (Algebraic Data Types)

> **Implementation status:** User-defined tagged unions (`union` keyword) are not yet implemented — planned for Phase 8 (v0.2.2). The built-in `Result[T, E]` and `Optional[T]` types are implemented as compiler primitives (see below).

Tagged unions (also called algebraic data types or sum types) allow defining types that can be one of several variants, where each variant can carry associated data.

## Overview

Unlike simple enums, tagged unions allow cases to carry associated data:

```python
# Generic Result type (like Rust's Result)
union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Optional type (like Rust's Option)
union Optional[T]:
    case Some(value: T)
    case None()

# Tree structure
union BinaryTree[T]:
    case Leaf(value: T)
    case Node(left: BinaryTree[T], right: BinaryTree[T])
```

## Standard Library Types

Sharpy provides `Result[T, E]` and `Optional[T]` in the standard library with special integration into the language:

- **[Optional Type](tagged_unions_optional.md)** — `T?` is shorthand for `Optional[T]` (safe tagged union for optional values)
- **[Result Type](tagged_unions_result.md)** — `T !E` is shorthand for `Result[T, E]` (in return type annotations)

Both are **structs** (no heap allocation).

> **Note:** `Optional[T]` and `Result[T, E]` are **core primitives** implemented as
> structs for zero-allocation performance. They are distinct from user-defined
> tagged unions (declared with `union`), which use class-based representation
> to support recursive types and more than two cases.

These types have special syntax and operators. See:
- [Try Expressions](try_expressions.md) - Special syntax for Result types
- [Maybe Expressions](maybe_expressions.md) - Special syntax for Optional types
- [Null Coalescing Operator](null_coalescing_operator.md) - The `??` operator
- [Null Coalescing Assignment](null_coalescing_assignment.md) - The `??=` operator
- [Null Conditional Access](null_conditional_access.md) - The `?.` operator

**Unit Cases (No Data):**

Cases that carry no associated data can be defined with or without parentheses:

```python
union Option[T]:
    case Some(value: T)
    case None              # No parentheses needed for unit case
    # case None()          # Also valid, but parentheses are optional

union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

union LoadState:
    case NotStarted         # Unit case
    case Loading            # Unit case
    case Loaded(data: str)  # Data case
    case Failed(error: str) # Data case
```

**Pattern Matching Unit Cases:**

When pattern matching, unit cases also don't require parentheses:

```python
match opt:
    case Option.Some(v): print(v)
    case Option.None: print("none")  # No parens in pattern

match state:
    case LoadState.NotStarted: start_loading()
    case LoadState.Loading: show_spinner()
    case LoadState.Loaded(data): display(data)
    case LoadState.Failed(err): show_error(err)
```

## Creating Values

Tagged union cases are created using the union type name followed by the case name:

```python
union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Create values using Type.Case() syntax
success: Result[int, str] = Result.Ok(42)
failure: Result[int, str] = Result.Err("Something went wrong")
```

**Note:** Case names follow the same casing as defined in the union declaration (typically `PascalCase`). The syntax `Result.Ok(42)` is a constructor call that creates an instance of the `Ok` case. This of course is just a convention and is not enforced by the compiler.

**Type Inference in Return Statements:**

When returning from a function with a tagged union return type, the type name can be omitted and the case name used directly:

```python
def divide(a: float, b: float) -> Result[float, str]:
    if b == 0:
        return Err("Division by zero")  # Short for Result.Err(...)
    return Ok(a / b)                     # Short for Result.Ok(...)
```

The compiler infers the full type from the function's return type annotation, allowing for more concise code.

**Type Inference in Variable and Argument Assignments:**

The type name can also be omitted when assigning to variables, arguments, or default parameters with an explicit tagged union type annotation:

```python
# Variable assignments with type annotations
result: Result[int, str] = Ok(42)           # Short for Result.Ok(42)
error: Result[int, str] = Err("failed")     # Short for Result.Err("failed")

# Function parameters with default values
def process(status: Result[int, str] = Ok(0)) -> None:
    match status:
        case Ok(value): print(f"Value: {value}")
        case Err(msg): print(f"Error: {msg}")

# Argument passing
def handle_result(res: Result[int, str]) -> None:
    pass

handle_result(Ok(123))      # Short for Result.Ok(123)
handle_result(Err("bad"))   # Short for Result.Err("bad")
```

The compiler infers the full type from the variable's type annotation or the parameter's type signature.

## Pattern Matching

```python
def divide(a: float, b: float) -> Result[float, str]:
    if b == 0:
        return Err("Division by zero")  # Type name omitted in return
    return Ok(a / b)                     # Type name omitted in return

result = divide(10, 2)
match result:
    case Ok(value):              # Type name omitted in match patterns
        print(f"Success: {value}")
    case Err(error):             # Type name omitted in match patterns
        print(f"Error: {error}")
```

**Type Inference in Match Statements:**

When matching on a tagged union value, the type name can be omitted from case patterns. The compiler infers the type from the match subject:

```python
# Both forms are equivalent:
match result:
    case Result.Ok(value): ...   # Explicit form
    case Ok(value): ...          # Short form (type inferred)

    case Result.Err(error): ...  # Explicit form
    case Err(error): ...         # Short form (type inferred)
```

This makes pattern matching more concise, especially when the matched type is clear from context.

## Methods on Tagged Unions

```python
union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

    def is_ok(self) -> bool:
        match self:
            case Ok(_):      # Type name omitted
                return True
            case Err(_):     # Type name omitted
                return False

    def unwrap(self) -> T:
        match self:
            case Ok(value):   # Type name omitted
                return value
            case Err(error):  # Type name omitted
                raise Exception(f"Called unwrap on Err: {error}")

    def unwrap_or(self, default: T) -> T:
        match self:
            case Ok(value):   # Type name omitted
                return value
            case Err(_):       # Type name omitted
                return default
```

*Implementation*
- *🔄 Lowered - Abstract base class + sealed nested case classes:*

```csharp
public abstract class Result<T, E> {
    private Result() { }

    public sealed class Ok : Result<T, E> {
        public T Value { get; }
        public Ok(T value) => Value = value;
        public void Deconstruct(out T value) => value = Value;
    }

    public sealed class Err : Result<T, E> {
        public E Error { get; }
        public Err(E error) => Error = error;
        public void Deconstruct(out E error) => error = Error;
    }
}
```

## See Also

- [Result Type](tagged_unions_result.md) - Detailed guide to the Result type for error handling
- [Optional Type](tagged_unions_optional.md) - Detailed guide to the Optional type for optional values
- [Enums](enums.md) - Similar construct, but expressing simple enumerations without associated data
- [Pattern Matching](match_statement.md) - Using match with tagged unions
- [Generics](generics.md) - Generic type parameters
