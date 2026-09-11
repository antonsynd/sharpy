# Tagged Unions (Algebraic Data Types)

> **Implementation status:** ✅ Implemented. User-defined tagged unions (`union` keyword) are fully supported — parser, semantic analysis, codegen (abstract base class + sealed nested case classes), and pattern matching all work. The built-in `Result[T, E]` and `Optional[T]` types are implemented as compiler primitives (see below).

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

**Bare Unit-Case Patterns:**

When the scrutinee is union-typed, the union name can be omitted entirely — a bare name in a
case pattern resolves as a unit variant of the scrutinee's union:

```python
union LoadState:
    case NotStarted
    case Loading
    case Loaded(data: str)

def describe(state: LoadState) -> str:
    match state:
        case NotStarted:        # bare spelling — resolves as LoadState.NotStarted
            return "not started"
        case Loading:
            return "loading"
        case Loaded(data):
            return "loaded: " + data

def main():
    print(describe(LoadState.NotStarted()))
    print(describe(LoadState.Loading()))
    print(describe(LoadState.Loaded("users.csv")))
# Output:
# not started
# loading
# loaded: users.csv
```

*Resolution rule:* a bare name in a pattern over a union-typed scrutinee resolves as a variant
of that union **before** constant lookup and **before** capture binding. If the name collides
with a reachable constant, the pattern still resolves to the variant, and the compiler warns
(SPY0485, `VariantPatternShadowsConstant`):

```python
union Status:
    case Idle
    case Active

const Idle = 0  # collides with the variant name

def check(s: Status) -> str:
    match s:
        case Idle:      # resolves as Status.Idle, NOT the constant — warns SPY0485
            return "idle"
        case Active:
            return "active"
```

Use the qualified spelling (`case Status.Idle:`) or rename the constant to make the intent
explicit. In practice collisions are rare because constants are conventionally
`SCREAMING_SNAKE_CASE` while variants are `PascalCase` (a `PascalCase` constant additionally
draws the SPY0453 naming-convention warning).

*Synthetic-union carve-out:* the built-in `Optional[T]` and `Result[T, E]` primitives are
excluded from bare unit-case resolution in v1 — bare `case Some:`, `case Ok:`, and `case Err:`
do **not** resolve as variants over an Optional/Result scrutinee (a bare name there is an
ordinary capture binding, which matches any value). The payload spellings (`case Some(x):`,
`case Ok(v):`, `case Err(e):`) are unaffected, and `case None:` works because `None` is a
keyword literal, not a bare name:

```python
def find(x: int) -> int?:
    if x > 0:
        return Some(x * 2)
    return None()

def main():
    match find(5):
        case Some(v):    # payload spelling — unaffected by the carve-out
            print(v)
        case None:       # keyword literal — no parens needed
            print("none")
    match find(-1):
        case Some(v):
            print(v)
        case None:
            print("none")
# Output:
# 10
# none
```

## Creating Values

A USER-DEFINED union's cases are created with the union type name followed by the case name:

```python
union Shape:
    case Circle(radius: float)
    case Square(side: float)

# Create values using Type.Case() syntax
c: Shape = Shape.Circle(1.5)
s: Shape = Shape.Square(2.0)
```

**Note:** Case names follow the same casing as defined in the union declaration (typically
`PascalCase`). The syntax `Shape.Circle(1.5)` is a constructor call that creates an instance of the
`Circle` case. That casing is a convention and is not enforced by the compiler.

The BUILTIN unions — `Optional[T]` and `Result[T, E]` — are the exception: their cases are written
BARE (`Some(v)`, `None()`, `Ok(v)`, `Err(e)`) and only bare. The qualified spellings
`Optional.Some(…)`, `Optional.None()`, `Result.Ok(…)` and `Result.Err(…)` are refused by name with
SPY0608 and a steer to the bare form, and the four case names are reserved: a declaration that would
make the bare name resolve to something else — `def Some`, `class Ok`, a variable `Err` — is refused
with SPY0212 rather than shadowing the constructor, and the steer offers the backtick-escaped
spelling (`` `Some` ``) for a declaration that really wants the name (#1758). A CLASS MEMBER may take
the name (`Box.Some`): it is reached through its receiver and the bare form still resolves to the
builtin. See [Optional Type](tagged_unions_optional.md#creating-optional-values).

**Type Inference in Return Statements:**

When returning from a function whose return type is a tagged union, the type name is omitted and the
case name used directly:

```python
def divide(a: float, b: float) -> Result[float, str]:
    if b == 0:
        return Err("Division by zero")
    return Ok(a / b)
```

`divide(4.0, 2.0)` then matches `case Ok(v)` with `v == 2.0`.

The compiler infers the full type from the function's return type annotation.

**Type Inference in Variable and Argument Assignments:**

The same applies to variables and to arguments with an explicit tagged union type annotation:

```python
def process(status: Result[int, str]) -> None:
    match status:
        case Ok(value):
            print(f"Value: {value}")
        case Err(msg):
            print(f"Error: {msg}")

def handle_result(res: Result[int, str]) -> None:
    pass

def main() -> None:
    result: Result[int, str] = Ok(42)
    error: Result[int, str] = Err("failed")
    process(result)                 # Value: 42
    process(error)                  # Error: failed
    handle_result(Ok(123))
    handle_result(Err("bad"))
```

The compiler infers the full type from the variable's type annotation or the parameter's type
signature.

A DEFAULT parameter value is not one of those positions: a default must be a compile-time constant
and a `Result`/`Optional` value is not, so `def process(status: Result[int, str] = Ok(0))` is refused
with SPY0401 and the steer to make the parameter required (#1857). A case-arm body also goes on its
own line — `case Ok(value): print(value)` on one line is a parse error (SPY0102).

**Bare vs. qualified spelling.** The bare form (`Ok(42)`, `Err("e")`, `Some(v)`, `None()`) is
the builtin unions' only spelling. User-defined unions are always constructed with the qualified
`Union.Case(…)` syntax:

```python
union Box[T]:
    case Full(v: T)
    case Empty()

b: Box[int] = Box.Full(1)    # qualified — type inferred from the annotation
p = Box.Full("hi")           # qualified — type inferred from the argument: Box[str]
e: Box[int] = Box.Empty()    # qualified — type from annotation; no args to infer from
# c = Full(1)                # SPY0200 — bare case name is not in scope
```

When neither a slot annotation nor the arguments supply type information, the case constructor
cannot infer the type parameters, and SPY0227 is reported:

```python
# x = Box.Empty()            # SPY0227 — cannot infer T; add an annotation
```

The qualified builtin forms (`Optional.Some(…)`, `Result.Ok(…)`) are not supported today (#1758,
Batch 3).

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
