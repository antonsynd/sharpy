# Type Narrowing

Sharpy performs type narrowing in conditional branches:

```python
value: str? = get_optional_string()

if value is not None:
    # Inside this block, 'value' is narrowed from 'str?' to 'str'
    print(value.upper())  # OK - value is str, not str?
else:
    print("No value provided")

# isinstance() narrowing
obj: object = get_value()

if isinstance(obj, str):
    # obj is narrowed to str
    print(obj.upper())
```

The **body** of a branch guarded by `or` is not narrowed, because the operands can imply different
types and there is no single narrowed type that holds:

```python
if isinstance(x, int) or isinstance(x, str):
    # x is NOT narrowed here — it could be int or str
```

(This is distinct from *expression-level* `or`-RHS narrowing, described below, which narrows the
**right operand** of an `or` from the left operand's negation.)

## Narrowing Rules

- `is not None` narrows nullable type (`T?`) to non-nullable (`T`)
- `is None` narrows the variable to `T` (non-optional) in the **else** branch
- `isinstance(x, Type)` narrows `x` to `Type` in the `if` branch
- Narrowing forms compose through `not`, `and`, and parentheses; `== None` is rejected — use
  `is None` (see [#1079])
- Narrowing only affects the scope of the conditional block

## Stores Use the Declared Type

Narrowing describes what a **read** sees. A **store** is checked against the target's declared
type — the slot the emitted C# writes — never against the narrowed type of the previous value or
of an enclosing `assert`/`if`. After a store, reads narrow to the stored value's type.

```python
class Box:
    v: str | None = None

def main() -> None:
    x: str | None = None
    x = "a"                 # reads of x now see str
    n: int = len(x)         # no None check needed
    x = None                # the store is checked against the declared str | None
    b: Box = Box()
    b.v = "a"
    assert b.v is not None  # narrows reads of b.v to str
    b.v = None              # the store writes the declared slot
    print(n, x is None, b.v is None)
```

```
1 True True
```

A non-nullable declaration is unaffected: `y: str = "a"; y = None` is SPY0229.

The walrus operator follows the same rule — its target slot is the declared binding type:

```python
def main() -> None:
    x: str | None = None
    x = "a"
    if (x := None) is None:
        print("back to None")
```

## `isinstance` is call syntax, not a value

`isinstance` is a compile-time narrowing construct rather than an ordinary function. It must be
called; referencing it as a value is an error ([SPY0337]).

```python
if isinstance(shape, Circle):     # OK — narrows shape to Circle
    print(shape.radius)

if (isinstance)(shape, Circle):   # OK — parentheses around a callee change nothing,
    print(shape.radius)           #      narrowing included

g = isinstance                    # ERROR SPY0337 — no first-class value
```

Wrap it in a lambda to pass the test around; the lambda pins the type being tested, and its body is
an ordinary narrowing call:

```python
is_circle = lambda v: isinstance(v, Circle)
```

Python allows `g = isinstance`, so this is a deliberate deviation — see
[`docs/deviations.yaml`](../deviations.yaml), entry `call-syntax-only-forms-as-values`. Union variant
constructors ([tagged unions](tagged_unions.md)) are call syntax only for the same reason and report
the same diagnostic. The rejection is a floor, not a ceiling: it can be lifted without breaking
existing code if these forms ever gain first-class values.

## Expression-level narrowing

Narrowing is not limited to statement branches — it also applies within expressions whose evaluation
order makes a narrowing fact hold for a sub-expression (#1080). The same narrowing forms
(`is None` / `is not None` / `isinstance`, and their `not`/`and`/`or` compositions) are used.

### Conditional expressions (ternary)

In `A if cond else B`, the condition's **positive** narrowings apply inside `A` (evaluated only when
`cond` is true) and its **negative** narrowings apply inside `B` (evaluated only when `cond` is false):

```python
def describe(x: int?) -> int:
    # In the true arm, `x` is narrowed from int? to int; in the false arm it stays int?.
    return x + 1 if x is not None else 0

def label(a: object) -> str:
    # `a` is narrowed to Dog inside the true arm.
    return a.bark() if isinstance(a, Dog) else "unknown"
```

Narrowing does not leak past the arm: a use of `x` after the conditional expression sees the
original (un-narrowed) type.

### `and` — right operand

The right operand of `and` sees the left operand's **positive** narrowings, because the right operand
is evaluated only when the left is truthy:

```python
def is_positive(x: int?) -> bool:
    # `x` is narrowed to int on the right of `and`, so `x > 0` type-checks.
    return x is not None and x > 0

def can_bark(a: object) -> bool:
    # `a` is narrowed to Dog on the right of `and`.
    return isinstance(a, Dog) and a.can_bark()
```

### `or` — right operand

The right operand of `or` sees the left operand's **negative** narrowings, because the right operand
is evaluated only when the left is falsy:

```python
def value_or_default(x: int?) -> int:
    # If the left `x is None` is false, `x` is not None on the right, so `x + 1` type-checks.
    return x is None or use(x + 1)
```

As with ternary arms, expression-level narrowing does not leak past the operand it applies to.

*Implementation*
- *✅ Native — C# supports flow analysis for nullable types; expression-level narrowing (ternary
  arms, `and`/`or` right operands) is applied by the Sharpy type checker (#1080).*

[#1079]: https://github.com/antonsynd/sharpy/issues/1079
[SPY0337]: https://github.com/antonsynd/sharpy/issues/1168
