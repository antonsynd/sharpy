# Function Definition

```python
def greet(name: str) -> str:
    """Greet a person by name."""
    return f"Hello, {name}!"

def print_message(message: str):
    print(message)

# With default parameters
def power(base: float, exponent: float = 2.0) -> float:
    return base ** exponent

# Multiple return values via tuple
def min_max(values: list[int]) -> tuple[int, int]:
    return (min(values), max(values))  # Note that the parentheses are optional
                                       # as the tuple is implied by the comma
```

## Rules

- All parameters must have type annotations
- Return type annotation required if function returns a value
- Return type can be omitted for `-> None` functions (functions without a return type annotation implicitly return nothing, i.e. C# `void`)
- Parameters with defaults must come after required parameters

## Parameters

See [function_parameters.md](function_parameters.md).

## Empty and Placeholder Function Bodies

A function body can be empty or serve as a placeholder using several forms:

**Valid Empty/Placeholder Bodies:**

```python
# Ellipsis (preferred for abstract/interface methods)
# If present on non-abstract methods, it is lowered as
# `throw new NotImplementedException();`
@abstract
def abstract_method(self) -> int:
    ...

# Pass statement (no-op)
def not_yet_implemented(self):
    pass

# Docstring only
def documented_placeholder(self):
    """This method will return a greeting."""

# Comment only
def minimal_placeholder(self):
    # TODO: implement this

# Docstring and pass
def explicit_placeholder(self):
    """Returns the computed value."""
    pass
```

## Semantics of Each Form

| Body Content | Valid | Compiled Behavior |
|--------------|-------|-------------------|
| `...` (ellipsis) | ✅ | `throw new NotImplementedException()` |
| `pass` | ✅ | Empty body |
| Docstring only | ✅ | Empty body (docstring extracted for documentation) |
| Comment only | ✅ | Empty body |
| Docstring + `pass` | ✅ | Empty body |
| Docstring + `...` | ✅ | `throw new NotImplementedException()` |

## Usage Guidelines

- **Abstract methods and interface methods**: Use `...` (ellipsis)
- **Intentionally empty methods**: Use `pass`
- **Placeholder during development**: Use `...` or `pass` with a descriptive docstring
- **Documentation-only**: Docstring alone is valid but consider adding `pass` or `...` for clarity

```python
interface IProcessor:
    def process(self, data: bytes) -> bytes:
        """Process the input data and return the result."""
        ...  # Abstract - must be implemented

class BaseHandler:
    @virtual
    def on_event(self, event: Event):
        """Called when an event occurs. Override to handle events."""
        pass  # Default: do nothing

    @virtual
    def validate(self, input: str) -> bool:
        """Validate the input. Override to customize validation."""
        # Base implementation accepts everything
        return True
```

*Implementation*
- *✅ Native - Empty bodies compile to empty C# method bodies; ellipsis compiles to `throw new NotImplementedException()`.*
