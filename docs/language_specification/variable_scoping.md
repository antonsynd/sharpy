# Variable Scoping Rules

## No `global` or `nonlocal` Keywords

Sharpy does not support Python's `global` or `nonlocal` keywords. This aligns with C# scoping semantics:

```python
# ❌ Invalid - these keywords don't exist in Sharpy
global x       # ERROR: unexpected 'global'
nonlocal y     # ERROR: unexpected 'nonlocal'
```

Assignment to a name bound in an enclosing scope — including an enclosing function's local — writes through to that binding; use a new name or an annotated declaration (`x: T = ...`) to shadow instead. This applies uniformly to blocks and nested functions (C# closure semantics).

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

The walrus operator (`:=`) assigns to the *containing scope*. In most cases this is the enclosing function or module. However, inside block-scoped constructs like comprehensions, the walrus variable is scoped to that block:

```python
# Walrus in if-statement: variable persists in containing scope
if (match := pattern.search(text)) is not None:
    print(match)  # OK
print(match)      # OK - walrus assigned in containing scope

# Walrus in comprehension: variable is comprehension-local
results = [y * 2 for x in items if (y := transform(x)) > 0]
print(y)          # ERROR: 'y' does not exist in this scope
```

**Note:** This differs from Python 3.8+, where walrus in comprehensions leaks to the outer scope. In Sharpy, the syntactic boundary equals the semantic boundary—comprehension delimiters (`[...]`, `{...}`) which fully contain all variables declared within.

### Example

```python
x = "outer"

for x in range(5):      # New 'x' shadows outer, block-scoped
    print(x)            # Prints 0, 1, 2, 3, 4

print(x)                # Prints "outer", 'x' was shadowed only
                        # in the for-loop, and not modified.
```

### Write-Through Assignment

Assignment to a name that already exists in an enclosing scope writes through to it — no `nonlocal` keyword needed:

```python
x = 0
for i in range(5):      # 'i' is block-scoped
    x += i              # Modifies outer 'x'
print(x)                # 10
print(i)                # ERROR: 'i' is block-scoped
```

This applies to nested functions too (C# closure semantics — captured by reference):

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

To create a **new** local that shadows an outer name, use an annotated declaration:

```python
def main():
    x: int = 1
    def shadow_it():
        x: str = "hello"  # New local — does NOT modify outer 'x'
        print(x)          # "hello"
    shadow_it()
    print(x)              # 1 — outer 'x' unchanged
```

## Class-Body Names Are Not Visible by Bare Name Inside Methods

Class and struct bodies define their own scope, but this scope is **not** a closure scope for methods. Methods cannot read or write class-body names by bare name — they must use `self.name`, or `ClassName.name` for a `const` or `@static` member. This matches Python's class-scope semantics.

```python
class Counter:
    count: int = 0

    def increment(self) -> None:
        count = count + 1     # ERROR (SPY0606) — bare store to class attribute
        print(count)          # ERROR (SPY0200) — bare read of class attribute

    def correct(self) -> None:
        self.count += 1       # OK — instance attribute via self
        count: int = 99       # OK — annotated declaration creates a new local
        print(count)          # 99
```

`ClassName.name` is the spelling for a **type-level** member — a `const` or an `@static` field. An
instance field reached through the type name is a different error (SPY0290), so the diagnostic
offers `ClassName.name` only when it compiles:

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

Inherited fields obey the same rule and get the same steer: `self.name` reaches a base class's
instance field, and a bare name does not.

### Parameter defaults still see the class body

A parameter default belongs to the **signature**, which is resolved in the scope that declares the
method — the class body — not in the method body:

```python
class Grid:
    const SIZE: int = 8

    def resize(self, n: int = SIZE) -> None:   # OK — the default is resolved in class scope
        print(n)
```

The same holds for decorator arguments and type annotations. Names in the method's **body** are
resolved by the rule above.


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
a bare use inside a method binds the **module** variable, exactly as Python does.

## Assignment Statement

```python
# Simple assignment
x = 10

# Multiple assignment (unpacking)
x, y = 10, 20

# Augmented assignment
x += 5
count *= 2
```

### Sibling-Block Redeclaration

Each block scope is independent — the same name can be reused across sibling blocks without collision:

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

## Variable Shadowing

Variables can be redeclared in the same scope with a different type using explicit type annotation:

```python
x: int = 5              # Initial declaration
x = 10                  # Assignment (same type)
x: str = "hello"        # Shadowing (new type, requires annotation)

# With auto keyword for type inference
x: int = 5
x: auto = "hello"       # Shadowing with inferred type
```

*Implementation:*
- *🔄 Lowered — `LocalNameAllocator` assigns C# spellings with monotonic integer versioning (`x`, `x_1`, `x_2`) during `CodeGenInfoComputer.ComputeForModule`. Rebinding chains link each redefinition to its predecessor; chain members share the root's spelling. The emitter reads `CodeGenInfo` and `TargetBinding` and owns no local-slot state.*
