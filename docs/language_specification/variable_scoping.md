# Variable Scoping Rules

## Governing Principle

> **A name means the same thing everywhere in its scope. Declaration order never changes *what* a
> name means; it only affects *when* the value is computed, and Sharpy guarantees the value is
> computed before it is read, or refuses the program. Sharpy never shows you a default value, and
> never gives a Python-valid program a different meaning; where it cannot match Python, it refuses.**

> **Ruled, not yet implemented (R-RQ and R-RT, 2026-11-25).** "Sharpy never shows you a default value" is about a value whose initializer has not run. A `var` field or `var` local declared with no initializer, where its type has a default (a value type, `T | None`, `T?`), will read as that default by rule.

Adopted 2026-11-01. The rationale, the measured comparison with Python, C#, Rust and Swift, and the
.NET emission are in [docs/design/name-resolution.md](../design/name-resolution.md). The rules:

| Rule | Statement | Status |
|---|---|---|
| **N1** | Module-level and class-level names are visible throughout their scope, above and below the declaration; the innermost scope wins. Class-body names are not visible by bare name inside methods (Python). | **Pending #2423.** Today a module variable is visible only below its declaration (SPY0200 above it). |
| **N2** | If top-to-bottom reading (Python) and scope (N1) would give a name different meanings — a class-body initializer reading a member at or before its declaration while an outer binding of the name exists — the program is refused. A module-level initializer that reads a builtin or imported name at or before its own same-named declaration is refused. | **Pending #2423.** Today a later member yields to the outer binding, and a later literal `const` outranks it. |
| **N3** | Every module-level and class-level value is initialized before its first read, by any spelling or path; module initializers run top to bottom before `main`; a cycle is a compile-time or run-time error, never a default value. | **Implemented** (plan-775c50, #2422): see [Values Are Ready Before Use](#values-are-ready-before-use-n3), with its residue. A bare forward read of a module variable or a `@static` member is still SPY0200, which N1 removes (#2423). |
| **N4** | A `const` follows N1–N3 like every other binding. | **Pending #2423.** Today a literal `const` is order-free and a computed one is ordered. |
| **N5** | A local is visible from its declaration to the end of its block. `for`, `except`, `with` and comprehension targets are fresh bindings, fresh per iteration; reusing a name that is read after the construct is refused. A local `def` is ordered. | Block scoping is implemented. **The refusal is pending #2425.** |
| **N6** | Writing a binding outside the current function takes `global x` (module) or `nonlocal x` (enclosing function). A bare store that would cross a function boundary is refused. | **Pending #2424.** Today `global`/`nonlocal` are refused (SPY0134) and a bare store writes through. |
| **N7** | Bare, qualified, aliased, lambda, generator or call: the same read gets the same answer and the same diagnostic. | Holds for every spelling that compiles: readiness is kept at the value (N3). **Pending #2423** for the bare forward read, which is SPY0200 where the qualified one runs. |

Sections below describe **today's** behavior; each marks where a pending rule will change it.

## No `global` or `nonlocal` Keywords

> **Pending #2424 (N6):** `global` and `nonlocal` are reinstated with Python's meaning, and a bare
> store across a function boundary becomes a refusal. This section describes today's behavior.

Sharpy does not support Python's `global` or `nonlocal` keywords. This aligns with C# scoping semantics:

<!-- spec-sweep: error SPY0134 -->
```python
# ❌ Invalid - these keywords don't exist in Sharpy
global x       # ERROR: unexpected 'global'
nonlocal y     # ERROR: unexpected 'nonlocal'
```

Assignment to a name bound in an enclosing scope — including an enclosing function's local — writes through to that binding; use `let` (`let x = ...`) or a new name to shadow instead. In Stage 1 an annotated declaration (`x: T = ...`) also shadows (#1974). This applies uniformly to blocks and nested functions (C# closure semantics).

## Block Scoping

Sharpy uses C#-style block scoping: **all compound statement bodies introduce a new scope**. Variables declared inside a block are not visible outside it. This is a deliberate departure from Python, where variables leak out of most blocks.

**Block-Scoped Compound Statements** (variables declared inside don't leak):
- `if` / `elif` / `else` bodies
- `while` body
- `for` body (including the loop variable itself)
- `try` body
- `except` body (including the `as` binding)
- `else` body (in `try`/`except`/`else`)
- `finally` body
- `with` body
- Comprehensions (including walrus assignments inside comprehensions)

**Note on `try`/`except`/`else`/`finally`**: Variables declared in the `try` body are **not** visible in `except`, `else`, or `finally` handlers. If a variable must be accessible across all clauses, declare it before the `try` statement:

```python
def risky_operation() -> int:
    return int("42")

result: int = 0
try:
    result = risky_operation()
except ValueError as e:
    print(f"Failed: {e}")
finally:
    print(f"Result was: {result}")
```

**Containing-Scope Constructs** (variable persists):
- Declarations in a function body or module top-level (outside any compound statement)
- Walrus operator (`x := value`) in non-block contexts - see [Walrus Operator](walrus_operator.md)

**Walrus Operator Scoping:**

The walrus operator (`:=`) is an assignment and has no scope of its own: a new name it introduces belongs to the block of the statement it sits in. Inside a comprehension, a new name cannot be read after the comprehension:

<!-- spec-sweep: output -->
```python
import re

def main() -> None:
    pattern = re.compile("[0-9]+")
    text = "order 66"
    # Walrus in if-statement: variable persists in containing scope
    if (m := pattern.search(text)) is not None:
        print(m)  # OK
    print(m)      # OK - walrus assigned in containing scope
```

```output
<re.Match object; span=(6, 8), match='66'>
<re.Match object; span=(6, 8), match='66'>
```

<!-- spec-sweep: error SPY0200 -->
```python
def transform(v: int) -> int:
    return v - 1

def main() -> None:
    items = [1, 2, 3]
    # Walrus in comprehension: variable is comprehension-local
    results = [y * 2 for x in items if (y := transform(x)) > 0]
    print(y)          # ERROR: 'y' does not exist in this scope
```

**Note:** This differs from Python 3.8+, where a name a walrus introduces in a comprehension leaks to the outer scope. A walrus over a name that already exists is an assignment to it: over a local of the same function it writes through, as Python does; over a `let`, a parameter, or a module or enclosing-function name it is refused. See [walrus_operator.md](walrus_operator.md).

### Example

<!-- spec-sweep: output -->
```python
x = "outer"

for x in range(5):      # New 'x' shadows outer, block-scoped
    print(x)            # Prints 0, 1, 2, 3, 4

print(x)                # Prints "outer", 'x' was shadowed only
                        # in the for-loop, and not modified.
```

```output
0
1
2
3
4
outer
```

> **Pending #2425 (N5):** because `x` is read after the loop, and Python would print `4` there, this
> program becomes a compile-time error with the steer "rename the loop variable". Without the later
> read it stays legal.

### Write-Through Assignment

Assignment to a name that already exists in an enclosing scope writes through to it — no `nonlocal` keyword needed:

<!-- spec-sweep: output -->
```python
x = 0
for i in range(5):      # 'i' is block-scoped
    x += i              # Modifies outer 'x'
print(x)                # 10
```

<!-- spec-sweep: error SPY0200 -->
```python
for i in range(5):
    print(i)
print(i)                # ERROR: 'i' is block-scoped
```

Within one function this stays the rule. Across a function boundary it is pending change:

> **Pending #2424 (N6):** the nested-function write-through below becomes a refusal; write
> `nonlocal n` in `double_it` to write the outer `n` (Python's rule). Reads are unaffected.

This applies to nested functions too (C# closure semantics — captured by reference):

<!-- spec-sweep: output -->
```python
def main():
    n: int = 1
    def double_it():
        n = n * 2        # Writes through to outer 'n'
    double_it()
    print(n)             # 2

    count: int = 0
    adder = lambda: count + 1  # Reads outer 'count'
    count = 10
    print(adder())       # 11 — lambda sees the updated value
```

```output
2
11
```

To create a **new** local that shadows an outer name, use `let`:

<!-- spec-sweep: output -->
```python
def main():
    let x = 1
    def shadow_it():
        let x = 2         # New local — does NOT modify outer 'x'
        print(x)          # 2
    shadow_it()
    print(x)              # 1 — outer 'x' unchanged
```

```output
2
1
```

A bare `x = 2` in `shadow_it` would instead write through and print `2` twice. In Stage 1 an
annotated declaration (`x: int = 2`) also creates the new local (#1974).

## Class-Body Names Are Not Visible by Bare Name Inside Methods

Class and struct bodies define their own scope, but this scope is **not** a closure scope for methods. Methods cannot read or write class-body names by bare name — they must use `self.name`, or `ClassName.name` for a `const` or `@static` member. This matches Python's class-scope semantics. The rule is about **members**, not about the lines of the body in front of you: a field or property the class inherits, and a property the body declares, are members too and get the same treatment (see [Inherited members and properties](#inherited-members-and-properties-are-members-too)).

<!-- spec-sweep: error SPY0606 -->
```python
class Counter:
    count: int = 0

    def increment(self) -> None:
        count = count + 1     # ERROR (SPY0606) — bare store to class attribute
        print(count)          # ERROR (SPY0200) — bare read of class attribute

    def correct(self) -> None:
        self.count += 1       # OK — instance attribute via self
        let count = 99        # OK — 'let' creates a new local
        print(count)          # 99
```

The SPY0606 refusal steers to the member spelling first (`self.count = ...` for an instance
attribute, `ClassName.count = ...` for a `const` or `@static` one), then to `let count = ...` for a
new local, then to the Stage 1 annotated `count: <type> = ...`.

`ClassName.name` is the spelling for a **type-level** member — a `const` or an `@static` field. An
instance field reached through the type name is a different error (SPY0290), so the diagnostic
offers `ClassName.name` only when it compiles:

<!-- spec-sweep: error SPY0606 -->
```python
class Registry:
    @static
    total: int = 0

    @static
    def bump() -> None:
        total += 1            # ERROR (SPY0606) — "Use 'Registry.total = ...'"
        Registry.total += 1   # OK — @static field via the class name
```

### Every store form is refused, not only the plain one

A bare name in a class body is a member in every write position, so each is refused by name rather
than silently declaring a local:

<!-- spec-sweep: error SPY0606 -->
```python
class Counter:
    count: int = 0

    def forms(self) -> None:
        count = 1             # SPY0606 — plain store
        count += 1            # SPY0606 — augmented store
        print(count := 1)     # SPY0606 — walrus
        count, n = 1, 2       # SPY0606 — tuple-unpacking element
        count ??= 1           # SPY0606 — null-coalescing store
```

A bare store to a class `const` says so, and offers only the shadowing local — a constant cannot be
assigned through any spelling:

<!-- spec-sweep: error SPY0606 -->
```python
class C:
    const K: int = 1

    def m(self) -> None:
        K = 2    # SPY0606 — "'C.K' is a constant and cannot be assigned"
```

### Which bodies the rule applies to

Every function-like body: methods, property accessors, event accessors, property observers, nested
`def`s, and lambdas — including a lambda in a class-field initializer, which has no `self` at all.

```python
class Config:
    name: str = "default"

    property get label(self) -> str:
        return self.name      # OK
        # return name         # SPY0200 — bare read of a class attribute

    def make_greeter(self) -> () -> str:
        return lambda: self.name   # OK — the lambda reaches the field through self
```

### Inherited members and properties are members too

A member is a member whether the enclosing body declares it or a base class's body does, and
whether it is a field or a property. The bare read is SPY0200 with the same `self.` steer, and the
bare store is SPY0606 in every store form — the refusal names the inheritance so the reader is not
sent looking for a declaration in the body in front of them. In C# a bare `v = 7` in a method
writes the inherited field; in Python it declares a local; a Sharpy program that spells it is a
mistake either way, and the shadowing local is still one `let` away:

<!-- spec-sweep: error SPY0606 -->
```python
class Base:
    v: int = 5

    property get label(self) -> str:
        return "base"

class Derived(Base):
    def m(self) -> int:
        v = 7                 # SPY0606 — "Cannot assign to inherited class attribute 'v' by bare name"
        label = "x"           # SPY0606 — a property is a member too
        print(v, label)       # SPY0200 — bare reads, steered to 'self.v' and 'self.label'
        return self.v         # OK

    def shadow(self) -> int:
        let v = 7             # OK — 'let' declares a new local
        print(v)              # 7
        return self.v         # 5 — the inherited field is untouched
```

An inherited `const` or `@static` field is steered to the **declaring** class's name, which is the
spelling that compiles: `Base.K`, not `Derived.K`.

### Initializers and parameter defaults see the class body by position

A class-body position outside a method body reads a sibling by bare name exactly when C# resolves
that name in the position the declaration becomes:

- **An initializer** reads a `const` or `@static` sibling (field, property or method) by bare name.
  That covers a field initializer in a plain class, a `@static` field's initializer, every property
  default (instance or `@static`, in every host), and a `const` whose initializer is not a
  compile-time constant (it is emitted `static readonly`, so it runs as a static initializer).
- **A parameter default** belongs to the method's **signature**, and C# requires it to be a
  compile-time constant, so it reads a `const` only. A `@static` sibling resolves but is not
  constant (SPY0401). An instance field's initializer in a `struct` or a `@dataclass` is the
  synthesized constructor's parameter default, so the same rule applies to it.
- **An instance member** (a field, property, event or method that is not `@static`) is refused
  (SPY0200) in every one of these positions. No instance exists when they run. The diagnostic names
  the position and says where the value can be read instead.

<!-- spec-sweep: output -->
```python
class Grid:
    const SIZE: int = 2

    @static
    scale: int = 2

    @static
    def twice(n: int) -> int:
        return n * 2

    area: int = SIZE                              # a field initializer reads a const
    @static
    property cells: int = SIZE * scale            # a @static property default reads a const and a @static field
    property border: int = SIZE + twice(scale)    # a property default reads both, and calls a @static method

def main() -> None:
    g = Grid()
    print(g.area)         # 2
    print(Grid.cells)     # 4
    print(g.border)       # 6
```

A parameter default reads a `const`:

```python
class Grid:
    const SIZE: int = 8

    def resize(self, n: int = SIZE) -> None:   # OK — the default is resolved in class scope
        print(n)
```

An instance sibling is refused in every position outside a method body:

<!-- spec-sweep: error SPY0200 -->
```python
class Grid:
    width: int = 3
    total: int = width    # SPY0200 — "'width' is an instance attribute of 'Grid' — a field initializer runs before any instance exists, so it may read only a 'const' or '@static' member of 'Grid'"

def main() -> None:
    print(Grid().total)
```

By bare name, a position reads a `@static` sibling only if it is **declared earlier** in the body. A
`@static` field, property or method declared later, or the member being initialized itself, is refused
(SPY0200, "declared later"); Python raises `NameError` there. **Pending #2423 (N1):** the bare forward
read runs, as the same read through the type name already does (below). A `const` sibling is read by
bare name in any order: a compile-time-constant `const` (`const SIZE: int = 8`) because a type body
resolves its constants in dependency order, and a `const` whose initializer is not a compile-time
constant (`const K: int = seven()`) because the read runs its initializer first
([N3](#values-are-ready-before-use-n3)). When an outer binding of the name exists, a later `@static`
sibling and the member itself yield to it instead (below). A later `const` does not, because the
outer binding (Python's reading) and the class's `const` (scope's reading) would give the name two
meanings (rule N2):

- a later `const` whose initializer is not a compile-time constant is refused as ambiguous
  (SPY0618), and so is a bare read of it in a comprehension's body, where Python reads the outer
  binding or, with none, raises `NameError`. `Box.LIMIT` reads the member;
- a later compile-time-constant `const` is read as the class's value (`7` below, where python3
  3.15.2 prints the outer `9`). **Pending #2423 (N2):** this read is refused as well, under N2's own
  code, which also replaces SPY0618.

<!-- spec-sweep: error SPY0618 -->
```python
LIMIT: int = 9

class Box:
    @static
    v: int = LIMIT              # SPY0618 — "'LIMIT' is ambiguous in a '@static' initializer: 'Box.LIMIT' is a 'const' declared later than 'v', and a 'LIMIT' outside the class is in scope"
    const LIMIT: int = seven()

def seven() -> int:
    return 7

def main() -> None:
    print(Box.v)
```

<!-- spec-sweep: error SPY0200 -->
```python
class Counter:
    @static
    start: int = step     # SPY0200 — "'step' is declared later than 'start' in 'Counter'"
    @static
    step: int = 2

def main() -> None:
    print(Counter.start)
```

In these positions a member the class body declares outranks a module-level (or other outer) binding
of the same name, for every member kind, as in C# and Python. A member declared later — or the member
being initialized, read in its own initializer (`x: int = 5` at module level, then
`@static x: int = x + 1` gives 6) — does not exist yet where the position runs, so the outer binding is
what it reads. Inside a method body the module binding still wins (the rule above).

An **inherited** member is never read by bare name in these positions: only a member the body in
front of you declares is. With an outer binding of the name, the outer binding is what the position
reads, as in Python; with none, the read is SPY0200, steered to the declaring class (`Base.unit`), the
spelling that compiles.

<!-- spec-sweep: output -->
```python
unit: int = 9

class Base:
    @static
    unit: int = 3

class Child(Base):
    a: int = unit             # the module variable: 'unit' is inherited, not declared here
    b: int = Base.unit        # the inherited member, named through its class

def main() -> None:
    c = Child()
    print(c.a, c.b)           # 9 3
```

<!-- spec-sweep: output -->
```python
size: int = 7

class Box:
    @static
    property size: int = 2

    @static
    property doubled: int = size * 2    # the @static property, not the module variable

def main() -> None:
    print(Box.doubled)    # 4
```

**Static initializers run in source order.** A `@static` field's initializer, a `@static` property's
default and a `const` whose initializer is not a compile-time constant are the type's static
initializers. They run once, top to bottom, in the order the body declares them — C#'s rule for the
generated class, and Python's for a class body. Such an initializer reads an earlier static sibling and
sees its value, by bare name or through the type (`Host.sp`, or an alias of `Host`):

<!-- spec-sweep: output -->
```python
class Host:
    @static
    property sp: int = 4
    @static
    g: int = Host.sp

def main() -> None:
    print(Host.g)    # 4
```

A static sibling declared later — a `@static` field, a `@static` property, a non-compile-time `const`
— read through the type name (`Host.b`, or an alias of `Host`) is initialized first: the read runs
that sibling's initializer and sees its value ([N3](#values-are-ready-before-use-n3)). Calling a
`@static` method declared later through the type name runs too. The bare spelling of a later `@static`
member is still SPY0200 (above, pending #2423). A compile-time `const` is order-free in every spelling.

<!-- spec-sweep: output -->
```python
class Host:
    @static
    a: int = Host.b     # 'b' is initialized first
    @static
    b: int = 2

def main() -> None:
    print(Host.a)    # 2
```

A member read through the type name in its own initializer has no value to read: that is a cycle, and
a cycle is an error (SPY0278, [below](#a-cycle-is-an-error-never-a-default)).

<!-- spec-sweep: error SPY0278 -->
```python
class Host:
    @static
    a: int = Host.a + 1     # SPY0278 — "Circular value reference: 'Host.a' reads a value whose initializer reads it back on every run"

def main() -> None:
    print(Host.a)
```

Calling an EARLIER `@static` method is allowed, and the method's body runs at the call. A static it
reads that is declared below the reader is initialized first, so the call sees its value.

<!-- spec-sweep: output -->
```python
class Host:
    @static
    def read() -> int:
        return Host.sp
    @static
    a: int = Host.read()    # 'read' reads 'sp', whose initializer runs first
    @static
    property sp: int = 4

def main() -> None:
    print(Host.a)    # 4
```

A generator expression that is the whole initializer is stored, not run: its element, conditions and
every iterable after the first run when the iterator is consumed, like an earlier method's body. Its
first iterable is evaluated where the initializer stands, and a generator consumed inside the
initializer (`sum(x + Host.b for x in xs)`) runs there. A later static read in any of these positions
is initialized first, wherever it runs.

<!-- spec-sweep: output -->
```python
class Host:
    @static
    g: Iterator[int] = (x + Host.b for x in [1, 2])    # stored; runs when consumed
    @static
    b: int = 2

def main() -> None:
    print(list(Host.g))    # [3, 4]
```

<!-- spec-sweep: output -->
```python
class Host:
    @static
    firsts: list[int] = list(x for x in Host.later)    # the first iterable runs here
    @static
    total: int = sum(x + Host.b for x in [1, 2])        # consumed here
    @static
    later: list[int] = [2]
    @static
    b: int = 2

def main() -> None:
    print(Host.firsts, Host.total)    # [2] 7
```

Decorator arguments and type annotations are resolved in the class body as well. Names in the
method's **body** are resolved by the rule above. Python resolves class scope in every one of these positions
(`class C: a = 1; b = a` binds `b` to `1`). Sharpy resolves only what C# resolves there (deviation
`class-body-bare-name-read`).

### Pattern heads are not reads

A `case` head naming a class `const` matches that constant; it does not degrade into a capture:

```python
class C:
    const A: str = "x"

    def m(self, v: str) -> None:
        match v:
            case A:           # matches C.A, with SPY0468 noting the constant
                print("hit")
            case _:
                print("miss")
```

**Nested types and module names** remain visible inside methods — the rule applies only to variable
and constant bindings in a class or struct body, not to type declarations or to names from the
enclosing module or global scope. When a module-level variable and a class attribute share a name,
a bare use inside a method binds the **module** variable, exactly as Python does. Today a module
variable is visible only if it is declared above the method's class (SPY0200 otherwise; python3
reads it). **Pending #2423 (N1):** module variables are visible throughout the module.

## Values Are Ready Before Use (N3)

Every module-level value and every class-level static value is initialized before it is read, by
any spelling and on any path: bare or qualified, through an alias, a call, a lambda or a generator,
from another class or from another module. Such a value is a module variable, a `@static` field, a
`@static` property's default, or a module or class `const` whose initializer is not a compile-time
constant. A read never sees a storage default (`0`, `None`, `False`). A compile-time-constant `const`
(`const SIZE: int = 8`) is a C# `const`, so it has nothing to initialize.

### Initializers run top to bottom, before `main`

Each module runs its initializers once, in declaration order, before `main`. A class's statics run
at the class's line in its module, and a nested class's statics run at its line inside its outer
class. An imported module's initializers run at the import, and so do a referenced Sharpy library's.
A generic class is the exception: each closed type (`Box[int]`) runs its statics on its first use.
This is Python's order, and python3 3.15.2 prints the same for the same program with `main()` called
at the end:

<!-- spec-sweep: output -->
```python
def note(name: str, v: int) -> int:
    print(name)
    return v

const A: int = note("A", 1)

class Outer:
    @static
    a: int = note("Outer.a", 2)

    class Inner:
        @static
        v: int = note("Outer.Inner.v", 3)

    @static
    b: int = note("Outer.b", 4)

const B: int = note("B", 5)

def main() -> None:
    print("main", A + B)
```

```output
A
Outer.a
Outer.Inner.v
Outer.b
B
main 6
```

### A forward read initializes its target first

A read of a value declared below the reader runs that value's initializer at the read. When its
module's run reaches the declaration later, the value already has one, and the initializer does not
run again:

<!-- spec-sweep: output -->
```python
const TOTAL: int = BASE * 2      # BASE's initializer runs first
const BASE: int = compute()

def compute() -> int:
    print("compute")
    return 7

def main() -> None:
    print(TOTAL)
```

```output
compute
14
```

python3 3.15.2 raises `NameError: name 'BASE' is not defined` for this program. That error exists only
because of declaration order, and N3 turns it into a working program. C# runs static field
initializers in declaration order, so the same fields in C# give `TOTAL` the storage default of
`BASE` and print `0`. The same holds inside a class, by bare name or through the type:

<!-- spec-sweep: output -->
```python
class Grid:
    const AREA: int = SIDE * SIDE
    const SIDE: int = measure()
    const DOUBLE: int = Grid.SIDE * 2

def measure() -> int:
    return 3

def main() -> None:
    print(Grid.AREA, Grid.DOUBLE)    # 9 6
```

A plain store to a value whose initializer has not run yet is kept, and a read gets the stored value
back, until the module's run reaches the value. There the initializer runs and its result replaces
the store:

<!-- spec-sweep: output -->
```python
class Config:
    @static
    seen: int = Config.poke()
    @static
    level: int = 7

    @static
    def poke() -> int:
        Config.level = 5
        return Config.level

def main() -> None:
    print(Config.seen, Config.level)    # 5 7
```

### A cycle is an error, never a default

A value that its own initializer needs, directly or around a ring of other values, can never be given
a value. The compiler refuses the cycles it can see (SPY0278):

- a ring of values in which each initializer reads the next one every time it runs. A read inside a
  function body, a lambda or a pattern does not count, and neither does a read in a part of an
  expression that runs only on some evaluations (a conditional arm, the right operand of `and`);
- a ring of `const`s, through any read.

A plain store anywhere in the module to a value on the first kind of ring lifts the refusal, since
the store can give that value one before the ring closes. If no store has run when the ring is
reached, it raises `NameError` at run time instead.

<!-- spec-sweep: error SPY0278 -->
```python
class Ring:
    @static
    a: int = Ring.b + 1     # SPY0278 — "Circular value reference: 'Ring.a' reads a value whose initializer reads it back on every run"
    @static
    b: int = Ring.a + 1

def main() -> None:
    print(Ring.a)
```

A cycle the compiler does not see raises `NameError` at run time, when a read reaches a value whose
initializer is still running:

<!-- spec-sweep: output -->
```python
class Ring:
    @static
    a: int = Ring.b + 1
    @static
    b: int = peek()          # the read of Ring.a is inside a function: not seen

def peek() -> int:
    try:
        return Ring.a
    except NameError:
        print("Ring.a is not ready")
        return 0

def main() -> None:
    print(Ring.a, Ring.b)
```

```output
Ring.a is not ready
1 0
```

### An initializer that throws

A throw stops its module's run at that initializer. The value stays without one, and the exception
reaches whatever required the module: for the program's own module that is the start of the program,
so the program stops before `main` with the initializer's exception. After a failure, each value
whose initializer did not finish is initialized by its own first read. This program prints `A` and
then stops with `ZeroDivisionError: division by zero`, as python3 3.15.2 does:

```python
def note(name: str, v: int) -> int:
    print(name)
    return v

def zero() -> int:
    return 0

const A: int = note("A", 1)
const B: int = 1 // zero()
const C: int = note("C", 3)

def main() -> None:
    print("main")
```

### What N3 does not cover

- **Code compiled before the run finished.** Each read checks a flag that the JIT folds away once the
  value's host has run its initializers. Code compiled before that, and never compiled again, keeps the
  check (ReadyToRun code before tier-up; NativeAOT is not measured). The value is right; only the cost
  differs.
- **A failed or lock-losing run.** After an initializer throws, or after a type initializer finds the
  initialization lock held by another thread (it never waits for it), the host's flag stays off for
  the rest of the process, and every read takes the checked path. The values are still right. After a
  failed run, a value that a store gave a value before its initializer ran keeps that store, because a
  throw resets the value's state, not its storage.
- **A host runs all of its initializers when it is first required**, so a read that comes back to the
  first host raises by which host is touched first. Below, `A` runs first: `A.x` needs `B`, `B` runs
  all of its initializers, and `B.q` reads `A.x`, whose initializer is still running, so the program
  stops with `NameError: name 'x' is not defined`. With `B` declared first it prints `1 2`. python3
  3.15.2 raises `NameError` in both orders, since a class body cannot read a class defined below it.

```python
class A:
    @static
    x: int = B.p

class B:
    @static
    p: int = 1
    @static
    q: int = A.x + 1

def main() -> None:
    print(A.x, B.q)
```

- **Inside an `async def` body**, a use of a value that is not a plain read or a plain store (an
  `out` or `ref` operand, a member store, a mutating call) initializes the value at the start of its
  statement, before the statement's other operands run, not at the operand's own place.
- **An initializer that waits for another thread** never finishes if that thread reads a value whose
  host is still running its initializers: the run holds one process-wide lock, and the reading thread
  waits for it. C# code that is not compiled by Sharpy and reads an unswept Sharpy host from a type
  initializer waits for the same lock.
- **A store from another module** does not lift SPY0278. Only a store in the cycle's own module does,
  so a cycle that another module could break by storing first is still refused.

## Assignment Statement

```python
# Simple assignment
x = 10
count = 1

# Multiple assignment (unpacking)
x, y = 10, 20

# Augmented assignment
x += 5
count *= 2
```

### Sibling-Block Redeclaration

Each block scope is independent — the same name can be reused across sibling blocks without collision:

<!-- spec-sweep: output -->
```python
def main():
    try:
        x = 1
        print(x)          # 1
    except Exception:
        x = 2
        print(x)          # 2 (different 'x')
    finally:
        x = 3
        print(x)          # 3 (different 'x')

    # After the try, 'x' from any block is not visible:
    x = 99                 # New outer declaration
    print(x)               # 99
```

```output
1
3
99
```

## Variable Shadowing

`let` always introduces a new variable, so a name can be redeclared in the same scope, with the same
or a different type; the new variable shadows the old one for the rest of the scope:

<!-- spec-sweep: output -->
```python
def main():
    let x = 5               # Initial declaration
    x = 10                  # Assignment (same type)
    let x = "hello"         # Shadowing: a new variable of type str
    print(x)                # hello
```

Stage 1 also accepts the keywordless shadowing forms, which Stage 2 retires (#1974): an annotated
declaration shadows, and `x: auto = e` is the deprecated spelling of `let x = e`:

<!-- spec-sweep: output -->
```python
def main():
    x: int = 5
    x: str = "hello"        # Stage 1: an annotated declaration shadows
    x: auto = [1, 2, 3]     # Deprecated: write 'let x = [1, 2, 3]'
    print(x)                # [1, 2, 3]
```

*Implementation:*
- *🔄 Lowered — `LocalNameAllocator` assigns C# spellings with monotonic integer versioning (`x`, `x_1`, `x_2`) during `CodeGenInfoComputer.ComputeForModule`. Rebinding chains link each redefinition to its predecessor; chain members share the root's spelling. A `let` target never joins a chain: `let` re-points the trigger that starts a new binding (formerly only the annotation), and the allocator is unchanged — a `let` shadow is versioned exactly like an annotated one. The emitter reads `CodeGenInfo` and `TargetBinding` and owns no local-slot state.*
