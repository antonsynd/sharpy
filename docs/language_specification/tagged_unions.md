## Tagged Unions (Algebraic Data Types)

Tagged unions allow cases to carry associated data:

```python
# Generic Result type (like Rust's Result)
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Optional type (like Rust's Option)
enum Optional[T]:
    case Some(value: T)
    case Nothing()

# Tree structure
enum BinaryTree[T]:
    case Leaf(value: T)
    case Node(left: BinaryTree[T], right: BinaryTree[T])
```

**Unit Cases (No Data):**

Cases that carry no associated data can be defined with or without parentheses:

```python
enum Option[T]:
    case Some(value: T)
    case Nothing           # No parentheses needed for unit case
    # case Nothing()       # Also valid, but parentheses are optional

enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

enum LoadState:
    case NotStarted        # Unit case
    case Loading           # Unit case
    case Loaded(data: str) # Data case
    case Failed(error: str) # Data case
```

**Pattern Matching Unit Cases:**

When pattern matching, unit cases also don't require parentheses:

```python
match opt:
    case Option.Some(v): print(v)
    case Option.Nothing: print("none")  # No parens in pattern

match state:
    case LoadState.NotStarted: start_loading()
    case LoadState.Loading: show_spinner()
    case LoadState.Loaded(data): display(data)
    case LoadState.Failed(err): show_error(err)
```

### Creating Values

Tagged union cases are created using the enum type name followed by the case name:

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Create values using Type.Case() syntax
success: Result[int, str] = Result.Ok(42)
failure: Result[int, str] = Result.Err("Something went wrong")
```

**Note:** Case names follow the same casing as defined in the enum declaration (typically `PascalCase`). The syntax `Result.Ok(42)` is a constructor call that creates an instance of the `Ok` case. This of course is just a convention and is not enforced by the compiler.

### Pattern Matching

```python
def divide(a: double, b: double) -> Result[double, str]:
    if b == 0:
        return Result.Err("Division by zero")
    return Result.Ok(a / b)

result = divide(10, 2)
match result:
    case Result.Ok(value):
        print(f"Success: {value}")
    case Result.Err(error):
        print(f"Error: {error}")
```

### Methods on Tagged Unions

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

    def is_ok(self) -> bool:
        match self:
            case Result.Ok():
                return True
            case Result.Err():
                return False

    def unwrap(self) -> T:
        match self:
            case Result.Ok(value):
                return value
            case Result.Err(error):
                raise Exception(f"Called unwrap on Err: {error}")

    def unwrap_or(self, default: T) -> T:
        match self:
            case Result.Ok(value):
                return value
            case Result.Err():
                return default
```

*Implementation: 🔄 Lowered - Abstract base class + sealed nested case classes:*

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

---
