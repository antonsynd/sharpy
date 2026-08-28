# Variable Declaration and Assignment

Variables in Sharpy must be declared and assigned in a single statement. Variable declarations with type inference (Forms 2 and 3 below) are only allowed inside functions; module-level declarations require explicit type annotations.

There are three syntactic forms:

| Form | Syntax | Type Determination |
|------|--------|-------------------|
| Explicit type | `x: int = 5` | Type annotation specifies type |
| Inferred type | `x = 5` | Type inferred from initializer |
| Explicit inference | `x: auto = 5` | Type inferred from initializer (explicit) |

**Form 1: Explicit Type Annotation**

The type is explicitly specified. This form can be used both at module level (static fields) and inside functions:

```python
# Module level (static fields) - explicit type REQUIRED
counter: int = 0
config: str = "default"

def main():
    # Inside functions - explicit type allowed
    count: int = 0
    name: str = "Alice"
    items: list[int] = [1, 2, 3]
    user: User | None = None
```

**Form 2: Type Inference (Implicit)**

The type is inferred from the initializer expression. This form is **only allowed inside functions**, not at module level:

```python
def main():
    count = 0              # Inferred as int
    name = "Alice"         # Inferred as str
    items = [1, 2, 3]      # Inferred as list[int]
    pi = 3.14159           # Inferred as float

# ❌ NOT allowed at module level:
# count = 0              # ERROR: module-level requires type annotation
```

**Form 3: Type Inference (Explicit with `auto`)**

The `auto` keyword explicitly requests type inference. This is functionally equivalent to Form 2 but makes the inference explicit. Like Form 2, this is **only allowed inside functions**:

```python
def main():
    count: auto = 0        # Inferred as int
    name: auto = "Alice"   # Inferred as str
    items: auto = [1, 2, 3]  # Inferred as list[int]
```

**When to Use `auto`:**

The `auto` keyword is primarily useful for variable shadowing, where you want to redeclare a variable with a different type:

```python
def main():
    x: int = 5
    x = 10                 # Assignment to existing int variable
    x: str = "hello"       # Shadowing: new variable of type str
    x: auto = [1, 2, 3]    # Shadowing: new variable, type inferred as list[int]
```

## Bare Declarations (Declare-Then-Assign)

Sharpy allows variable declarations without initialization. The variable must be assigned on all control-flow paths before it is read — use-before-assign is a compile-time error (SPY0600):

```spy
def main() -> None:
    x: int
    if condition:
        x = 1
    else:
        x = 2
    print(x)  # OK — x is assigned on all paths

    y: int
    print(y)  # ERROR SPY0600 — y is used before being assigned

    z: int
    if condition:
        z = 1
    print(z)  # ERROR SPY0600 — z is not assigned on the else path
```

Definite assignment follows the **emitted** control flow, and Sharpy's analysis is more precise
than C#'s in three places. A bare local it proves assigned is emitted with a definite
initializer (`= default!`) so the C# compiler agrees:

- **`except` handlers** are entered from the state at the `try` statement's *entry*: a local
  assigned before the `try` is definitely assigned in every handler, `else`, `finally`, and
  after the statement; a local assigned only inside the try body is not (the exception may be
  raised before the assignment). Struct fields assigned in `__init__` follow the same rule.
- **Loop `else`** bodies run whenever the loop ends without `break`, so a local assigned in the
  `else` body (or on every path through the body *and* the `else`) is definitely assigned after
  the loop.
- **`defer` bodies** run at scope exit, after every statement of the enclosing scope, so a
  deferred read of a local assigned *later* in that scope is definitely assigned (bodies run in
  LIFO order).

```spy
def risky(flag: bool) -> int:
    if flag:
        raise ValueError("boom")
    return 1

def handlers(flag: bool) -> None:
    x: int
    y: int
    x = 10
    try:
        y = risky(flag)
    except ValueError:
        print(x)          # OK — x was assigned before the try
    else:
        print(x, y)       # OK — y is assigned when the try body completed

def loop_else(items: list[int]) -> None:
    x: int
    for i in items:
        x = i
    else:
        x = -1
    print(x)              # OK — the else body assigned x on the no-break path

def deferred() -> None:
    x: int
    defer:
        print(x)          # OK — runs at scope exit, after `x = 7`
    x = 7
```

```spy
def handler_reads_try_body() -> None:
    y: int
    try:
        y = risky(True)
    except ValueError:
        print(y)          # ERROR SPY0600 — the exception may precede the assignment
```

**Class and struct fields** can also be declared without initialization if they are assigned in `__init__`.

## Module-Level vs Function-Level Variables

See [Program Entry Point](program_entry_point.md) for details on module-level declarations vs executable statements inside `main()`.
