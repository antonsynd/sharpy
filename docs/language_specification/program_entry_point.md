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

<!-- spec-sweep: fragment -->
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

## Module Initialization

Before `main` runs, every module of the program runs its module-level initializers, once and top to
bottom: a module runs at its first import, at the line of that import, and the entry module runs
its own initializers before `main`. A class's `@static` values run at the class's line. A read of a
value declared further down runs that value's initializer first, so no read sees a storage default
([N3](variable_scoping.md#values-are-ready-before-use-n3)). This is the order Python runs the same
module in, and python3 3.15.2 prints the same for this program with `main()` called at the end:

<!-- spec-sweep: output -->
```python
def note(name: str, v: int) -> int:
    print(name)
    return v

const FIRST: int = note("first", 1)
const SECOND: int = note("second", 2)

def main() -> None:
    print("main")
    print(FIRST + SECOND)
```

```output
first
second
main
3
```

If an initializer raises, the program stops before `main` with that exception.

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
<!-- spec-sweep: fragment -->
```python
x = 42
print(x)
result = compute(x)
print(result)
```

**After:**
<!-- spec-sweep: output -->
```python
def compute(x: int) -> int:
    return x * 2

def main():
    x = 42
    print(x)
    result = compute(x)
    print(result)
```

```output
42
84
```

*Implementation: ✅ Native*
- *`main()` compiles directly to C# `Main()` in the entry module's members class (`main.spy` →
  `namespace [<Root>.]Main { static partial class MainModule { Main() } }`), which the compiler names as
  the program's one entry point — a non-entry module's function emitted as `Main` is never a second one
  (see [module_system.md](module_system.md#package-structure))*
- *Module-level functions, variables and constants become static members of the members class; types
  are declared beside it in the module's namespace*
- *A module-level variable, a `const` whose initializer is not a compile-time constant and a class's
  `@static` value each become a static property over a backing field with its own initialization
  state; `Main()` first runs the module's initializers through its load entry (plan-775c50 D4, D5)*
