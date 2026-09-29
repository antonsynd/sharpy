# Statements

## Expression Statement

Any expression can be a statement:

<!-- spec-sweep: fragment -->
```python
print("Hello")
obj.method()
list.append(item)
```

The examples in the rest of this page use these declarations:

<!-- spec-sweep: prelude -->
```python
class User:
    name: str = ""

class Config:
    debug: bool = False

def load_config() -> Config:
    return Config()

def generate_data() -> list[int]:
    return [1, 2, 3]

def get_current_time() -> int:
    return 0

def compute() -> int:
    return 42

def my_function() -> None:
    pass
```

## Variable Declaration and Assignment

A local variable is declared with `let`, a constant with `const`, and bare `=` assigns:

| Form | Syntax | Meaning |
|------|--------|---------|
| Inferred declaration | `let x = 5` | New local; type inferred from the initializer |
| Annotated declaration | `let x: int = 5` | New local; type given by the annotation |
| Constant | `const NAME[: T] = e` | Assign-once binding (see [Constants](#constants)) |
| Assignment | `x = e` | Writes through to the nearest existing binding of `x` |

`let` always introduces a **fresh, mutable, block-scoped** binding. It takes an initializer
(`let x: int` without one is SPY0104) and a name or a tuple of names as its target
(`let a, b = 1, 2`, `let first, *rest = items`). A `let` of a name that is already bound — in the
same block, an enclosing block, the enclosing function, or the module — is a new variable that
shadows the old one for the rest of its block; it never assigns the old one. `let` is only allowed
inside a function body (see [Module-Level Declaration Rules](#module-level-declaration-rules)).

```python
def main() -> None:
    let count = 0                  # Inferred as int
    let name: str = "Alice"        # Annotated
    let a, b = 1, 2                # Tuple target
    let first, *rest = [1, 2, 3]   # Starred target
    count = count + 1              # Bare '=' writes through to 'count'
    print(count, name, a, b, first, rest)   # 1 Alice 1 2 1 [2, 3]
```

**Shadowing.** Because `let` is always fresh, it is how a block introduces its own variable
under a name that is already in use; bare `=` of that name inside the block would instead assign
the outer variable:

```python
def main() -> None:
    let x = 5
    if x > 0:
        let x = 10          # New block-local 'x'; the outer 'x' is untouched
        print(x)            # 10
    print(x)                # 5
    x = 7                   # Bare '=' writes through to the outer 'x'
    print(x)                # 7
```

**Stage 1 transition (#1974).** Sharpy is moving to `let`-only declarations in stages. In Stage 1,
which is the current language, the keywordless forms still declare: bare `x = e` on a name that is
not yet bound declares it, and annotated `x: T = e` declares a new variable (shadowing any existing
one), exactly as before `let` existed. Stage 2 (#1974) makes `let` required for a new local and
makes bare `=` write-through only. The keywordless forms are shown below because Stage 1 code
still uses them:

```python
def main() -> None:
    count: int = 0         # Annotated declaration (Stage 1)
    name = "Alice"         # Bare '=' on a new name declares it (Stage 1)
    items = [1, 2, 3]      # Inferred as list[int]
    count = count + len(items)
    print(count, name)     # 3 Alice
```

**`auto` (deprecated; use `let`).** `x: auto = e` is the Stage 1 spelling of `let x = e`; `auto`
is retired in Stage 2 (#1974):

```python
def main() -> None:
    count: auto = 0          # Deprecated: write 'let count = 0'
    items: auto = [1, 2, 3]  # Deprecated: write 'let items = [1, 2, 3]'
    print(count, items)      # 0 [1, 2, 3]
```

## Module-Level Declaration Rules

At module level (outside any function or class), variable declarations have additional constraints.

### `let` Is a Local Declaration

`let` declares a local binding, so it is refused at module level (SPY0340, with the steer "module-scope
bindings are `const`: use 'const NAME[: T] = ...'"). Module-scope bindings are `const`:

<!-- spec-sweep: fragment -->
```python
let limit = 10          # ERROR (SPY0340): use 'const LIMIT = 10'
const LIMIT = 10        # ✅ Module-scope binding
```

`let` is likewise refused in a class, struct or interface body (SPY0340, with the steer "declare a
field as 'x: T = ...' or a constant as 'const X = ...'"), and in a union or enum body it is the
body grammar's parser error.

### Type Annotation Required

In Stage 1, annotated module-level variables are still accepted; Stage 2 (#1974) makes module scope
`const`-only. Module-level variables MUST have explicit type annotations:

```python
# ✅ Valid module-level declarations
counter: int = 0
name: str = "default"
items: list[int] = []
data: Config | None = None
```

<!-- spec-sweep: fragment -->
```python
# ❌ Invalid - no type annotation at module level
x = 42                  # ERROR: requires type annotation
name = "hello"          # ERROR: requires type annotation
```

**Rationale:** This rule eliminates ambiguity between static field declarations and executable statements, which would otherwise look identical syntactically.

### No Executable Statements

Bare expression statements are not allowed at module level:

<!-- spec-sweep: fragment -->
```python
# ❌ NOT allowed at module level
print("hello")          # ERROR: executable statement not allowed
my_function()           # ERROR: executable statement not allowed
obj.method()            # ERROR: executable statement not allowed
1 + 2                   # ERROR: executable statement not allowed

# ✅ Move into main() or another function
def main():
    print("hello")      # OK inside function
    my_function()       # OK inside function
```

### Function Calls in Initializers

Function calls ARE allowed as part of a variable initializer:

```python
# ✅ Valid - function call is part of initialization
config: Config = load_config()
data: list[int] = generate_data()
timestamp: int = get_current_time()
```

### Inside Functions

Inside functions (including `main()`), `let` declares with type inference (and, in Stage 1, so
does bare `=` on a new name):

```python
def main():
    let x = 42              # ✅ OK - inferred as int
    let result = compute()  # ✅ OK - inferred from return type
    name = "hello"          # ✅ OK in Stage 1 - inferred as str
    print(x, result, name)  # 42 42 hello
```

## Bare Declarations

Sharpy allows variable declarations without initialization (bare declarations). The variable must be assigned on all control-flow paths before it is read — use-before-assign is a compile-time error (SPY0600). See `variable_declaration.md` for details.

```spy
def choose(condition: bool) -> None:
    x: int
    if condition:
        x = 1
    else:
        x = 2
    print(x)  # OK — assigned on all paths
```

<!-- spec-sweep: error SPY0600 -->
```spy
def never_assigned() -> None:
    y: int
    print(y)  # ERROR SPY0600 — used before being assigned
```

**Class and struct fields** can also be declared without initialization if they are assigned in `__init__`:

```python
class Person:
    # Field declarations (no initializer required)
    name: str
    age: int

    # Optional: fields with default values
    active: bool = True

    def __init__(self, name: str, age: int):
        # All fields without defaults must be assigned in __init__
        self.name = name
        self.age = age
```

## Declaration Keywords

Sharpy has two declaration keywords: `let` for a fresh, mutable, block-scoped local and `const` for
an assign-once binding. There is no mutability axis — `var`, `val` and `let mut` are not Sharpy
syntax:

```python
def main() -> None:
    let x = 5              # Fresh binding, type inferred
    let y: int = 10        # Fresh binding, type explicit
    const Z = 15           # Assign-once
    x = x + y + Z          # Bare '=' writes through to 'x'
    print(x)               # 30
```

<!-- spec-sweep: error SPY0103 -->
```python
def main() -> None:
    var y = 10             # ERROR (SPY0103): 'var' is not a keyword
    val z = 15             # ERROR (SPY0103): 'val' is not a keyword
```

## Constants

Constants are declared with `const` and are assign-once. The initializer may be any expression: a
compile-time constant lowers to C# `const`, anything else to `static readonly`:

```python
# Module-level constants
const PI: float = 3.14159
const MAX_SIZE: int = 1000
const APP_NAME = "MyApp"       # Type inferred as str
const DEBUG: bool = True
const ANSWER: int = compute()  # Runtime initializer: emits 'static readonly'

def main() -> None:
    print(PI, MAX_SIZE, APP_NAME, DEBUG, ANSWER)   # 3.14159 1000 MyApp True 42
```

**Class-Level Constants:**

Constants can also be declared within classes. Class-level constants are implicitly `@static` (matching C# semantics where class constants are always static):

```python
class Math:
    const PI: float = 3.14159265358979
    const E: float = 2.71828182845904
    const TAU: float = 6.28318530717958

    @static
    def circle_area(radius: float) -> float:
        return Math.PI * radius ** 2

class HttpStatus:
    const OK: int = 200
    const NOT_FOUND: int = 404
    const INTERNAL_ERROR: int = 500

def main() -> None:
    # Access via class name (constants are implicitly static)
    print(Math.PI)           # 3.14159265358979
    print(HttpStatus.OK)     # 200

    # Cannot access via instance (they're static, not per-instance)
    m = Math()
    print(m.PI)              # Works but discouraged; prefer Math.PI
```

**Note:** There is no such thing as a per-instance constant. Use a read-only property (`property get`) with a backing field or `@final` field (if added in a future version) for per-instance immutability.

Constants cannot be reassigned:

<!-- spec-sweep: error SPY0225 -->
```python
const X: int = 5
X = 10                 # ERROR: cannot assign to constant
```

*Implementation*
- *✅ Native - Direct mapping to C# variable declarations and `const`. `let x = e` emits the same C#
  local declaration as a Stage 1 keywordless declaration (`var x = e`, or the annotated type); a
  `let` that shadows is versioned like an annotated shadow (`x_1`).*
