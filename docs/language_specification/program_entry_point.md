# Program Entry Point

## Entry Point Function

Every executable Sharpy program requires a `main()` function as its entry point:

```python
def main():
    print("Hello, World!")
```

The `main()` function:
- Must be defined in the entry point file
- Takes no parameters (command-line args accessed via `system.environment`)
- Has an implicit `None` return type (can also be explicit: `def main() -> None:`)
- Is automatically invoked by the runtime

## Module-Level Declarations

Outside of `main()`, only declarations are allowed at module level:

```python
# ✅ Static fields (type annotation required)
counter: int = 0
config: str = "default"
items: list[int] = []

# ✅ Constants
const MAX_SIZE: int = 1000
const APP_NAME: str = "MyApp"

# ✅ Functions (become static methods)
def helper() -> int:
    return 42

# ✅ Classes, structs, enums, interfaces
class Point:
    x: int
    y: int

# ✅ Function calls in static initializers are allowed
data: str = load_config()  # OK: function call as initializer

# ❌ NOT allowed: bare executable statements
print("loading...")     # ERROR: not allowed at module level
helper()                # ERROR: not allowed at module level
x = 5                   # ERROR: requires type annotation
```

## Complete Example

```python
# app.spy - Entry point file

# Static members (type annotation required)
counter: int = 0
const VERSION: str = "1.0.0"

def increment() -> None:
    counter = counter + 1

class Config:
    debug: bool = False

def main():
    # Inside main(), type inference works normally
    message = f"Version {VERSION}"
    print(message)

    increment()
    print(counter)
```

## Non-Entry-Point Modules

Library modules (files that are imported, not executed directly) follow the same rules but do not require a `main()` function:

```python
# utils.spy - Library module

# Static field
call_count: int = 0

def utility() -> int:
    call_count = call_count + 1
    return call_count

# No main() needed - this module is imported, not executed
```

## Migration from Bare Statements

If you have existing code with top-level executable statements, wrap them in a `main()` function:

**Before (no longer valid):**
```python
x = 42
print(x)
result = compute(x)
print(result)
```

**After:**
```python
def main():
    x = 42
    print(x)
    result = compute(x)
    print(result)
```

*Implementation: ✅ Native*
- *`main()` compiles directly to C# `Main()` method*
- *Module-level declarations become static members of the module class*
