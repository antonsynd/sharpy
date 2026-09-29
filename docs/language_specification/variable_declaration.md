# Variable Declaration and Assignment

A local variable is declared with `let`; module-scope and class-scope values are declared with
`const` (or, in Stage 1, an annotated declaration). Bare `=` assigns to an existing binding.

| Form | Syntax | Where | Meaning |
|------|--------|-------|---------|
| Inferred `let` | `let x = 5` | Function bodies | New local; type inferred from the initializer |
| Annotated `let` | `let x: int = 5` | Function bodies | New local; type given by the annotation |
| Constant | `const NAME[: T] = e` | Module, class, function | Assign-once binding |
| Assignment | `x = e` | Anywhere a statement is allowed | Writes through to the nearest existing `x` |

**Form 1: `let` with Type Inference**

`let` introduces a fresh, mutable, block-scoped local whose type is inferred from the initializer:

```python
def main() -> None:
    let count = 0              # Inferred as int
    let name = "Alice"         # Inferred as str
    let items = [1, 2, 3]      # Inferred as list[int]
    let pi = 3.14159           # Inferred as float
    print(count, name, items, pi)   # 0 Alice [1, 2, 3] 3.14159
```

**Form 2: `let` with a Type Annotation**

```python
class User:
    name: str = ""

def main() -> None:
    let count: int = 0
    let items: list[int] = [1, 2, 3]
    let user: User | None = None
    print(count, items, user is None)   # 0 [1, 2, 3] True
```

A `let` always takes an initializer and is always a new variable, even when the name is already
bound — it shadows the existing binding for the rest of its block rather than assigning it (see
[Variable Shadowing](variable_scoping.md#variable-shadowing)). `let` is refused at module level and
in class, struct and interface bodies (SPY0340): module-scope bindings are `const`, and fields are
declared `x: T = ...`. In a union or enum body a `let` is that body grammar's parser error.

**Stage 1: keywordless declarations (#1974)**

In Stage 1 — the current language — the forms that predate `let` still declare. An annotated
`x: T = e` declares a new variable (inside a function, or at module level as a static field), and
bare `x = e` on a name that is not yet bound declares it inside a function. Stage 2 (#1974) makes
`let` required for a new local, bare `=` write-through only, and module scope `const`-only:

```python
# Module level (static fields) - explicit type REQUIRED in Stage 1
counter: int = 0
config: str = "default"

def main():
    count: int = 0         # Annotated declaration
    name = "Alice"         # Bare '=' on a new name declares it
    count = count + 1      # Bare '=' on an existing name assigns it
    print(counter, config, count, name)   # 0 default 1 Alice
```

Bare `=` never declares at module level: `count = 0` there is an executable statement (SPY0340).

**Deprecated: `auto`**

`x: auto = e` is the Stage 1 spelling of `let x = e`. It is deprecated — write `let` — and is
retired in Stage 2 (#1974):

```python
def main():
    count: auto = 0          # Deprecated: 'let count = 0'
    items: auto = [1, 2, 3]  # Deprecated: 'let items = [1, 2, 3]'
    print(count, items)      # 0 [1, 2, 3]
```

## Bare Declarations (Declare-Then-Assign)

Sharpy allows variable declarations without initialization, spelled `x: T` (a `let` always takes
an initializer — `let x: int` is SPY0104). The variable must be assigned on all control-flow paths
before it is read — use-before-assign is a compile-time error (SPY0600):

```spy
def choose(condition: bool) -> None:
    x: int
    if condition:
        x = 1
    else:
        x = 2
    print(x)  # OK — x is assigned on all paths
```

<!-- spec-sweep: error SPY0600 -->
```spy
def never_assigned() -> None:
    y: int
    print(y)  # ERROR SPY0600 — y is used before being assigned
```

<!-- spec-sweep: error SPY0600 -->
```spy
def one_path(condition: bool) -> None:
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
- **Lambda and nested `def` bodies** run when the closure is *called*, not where it is written, so
  they are judged by the same deferred-read rule: a read of a local assigned *later* in the
  enclosing scope is definitely assigned, but a local assigned *nowhere* in the enclosing function
  is refused (SPY0600) even though its flow position alone would not catch it — the closure could
  be called after the enclosing function returns.

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

def closures() -> None:
    x: int
    def inner() -> None:
        print(x)          # OK — inner() runs after `x = 3` below, not where it's defined
    x = 3
    inner()
```

`defer` is an experimental feature ([#1023](https://github.com/antonsynd/sharpy/issues/1023)); without `--enable-feature=defer` it is refused (SPY0331):

<!-- spec-sweep: error SPY0331 -->
```spy
def deferred() -> None:
    x: int
    defer:
        print(x)          # OK — runs at scope exit, after `x = 7`
    x = 7
```

<!-- spec-sweep: error SPY0600 -->
```spy
def never_assigned() -> None:
    x: int
    def inner() -> None:
        print(x)          # ERROR SPY0600 — x is never assigned in never_assigned
    inner()
```

<!-- spec-sweep: error SPY0600 -->
```spy
def risky(flag: bool) -> int:
    if flag:
        raise ValueError("boom")
    return 1

def handler_reads_try_body() -> None:
    y: int
    try:
        y = risky(True)
    except ValueError:
        print(y)          # ERROR SPY0600 — the exception may precede the assignment
```

**Class and struct fields** can also be declared without initialization if they are assigned in `__init__`.

## Module-Level vs Function-Level Variables

A function-level variable is declared with `let` (or, in Stage 1, a keywordless declaration) and
lives until the end of its block. A module-level `let` is refused (SPY0340, "module-scope bindings
are `const`: use 'const NAME[: T] = ...'"); module-scope values are `const`, and in Stage 1 an
annotated module declaration (`counter: int = 0`) is still accepted. See
[Program Entry Point](program_entry_point.md) for details on module-level declarations vs executable
statements inside `main()`.
