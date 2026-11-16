# Comparison Chaining

<!-- spec-sweep: fragment -->
```python
# Chained comparisons
a < b < c           # Equivalent to: a < b and b < c
x == y == z         # Equivalent to: x == y and y == z
1 <= value <= 100   # Range check
```

## Mixed Operators

Chained comparisons can mix different comparison operators:

<!-- spec-sweep: fragment -->
```python
# Mixed operators are allowed
a < b <= c          # a < b and b <= c
a == b < c          # a == b and b < c
a != b != c         # a != b and b != c (but doesn't mean all different!)

# Complex chains
a < b <= c < d      # a < b and b <= c and c < d
```

## Membership and Identity Links

Every comparison operator can be a link, `in`, `not in`, `is` and `is not` included: `a < b in c`
is `a < b and b in c`, and `a is b is not c` is `a is b and b is not c`.

<!-- spec-sweep: output -->
```python
enum Color:
    RED = 1
    GREEN = 2

def main():
    items: list[int] = [1, 2, 3]
    x: int = 2
    print(0 < x in items)            # True
    print(0 < x not in items)        # False
    same: list[int] = items
    other: list[int] = [1, 2, 3]
    print(same is items is not other)  # True
    print(same is items is other)    # False
    c: Color = Color.RED
    d: Color = Color.RED
    print(c is Color.RED is d)       # True
    print(c is Color.RED == d)       # True
```

A link is checked and lowered as its binary twin, the expression of its two operands alone: it
answers what the twin answers, and where the twin is refused the chain is refused with the twin's
code. Here the twin `2 in n` is refused because an `int` is not a container:

<!-- spec-sweep: error SPY0320 -->
```python
def main():
    n: int = 5
    print(1 < 2 in n)                # SPY0320, as `2 in n` is
```

An operand between two links is read by both, and it is typed as the twin that reads it first, the
link on its left, types it. When the link on its right is a membership test, the operand is that
link's needle: it is converted to the needle slot at that read alone, as a named needle is (owner
ruling 2026-11-15, plan-67a01f R2). So `t == (1, 2) in ts` compares two `tuple[int, int]`, as
`t == (1, 2)` does, and then tests `(1, 2)` for membership in `ts`, as `p in ts` does for a
`p: tuple[int, int]`. A constant there (a literal, or operators over literals) is admitted by its
value where it fits the slot, as the binary `-1 in cs` admits it, and converted at the membership
read (owner ruling 2026-11-15, plan-67a01f R3): `h == -1 in cs` over `cs: list[int8]` compares `h`
with the `int` -1 and then tests the `int8` -1, and a constant out of the slot's range is refused as
`300 in cs` is. A tuple or list literal there is admitted by its type. Only the head of a chain is
read by one link alone, so only a head needle is typed by its needle slot, as `x in c` types it, and
it is checked before the operands after its container, which may read a name it binds
(`(y := 2) in xs == [y]`).

<!-- spec-sweep: output -->
```python
def main():
    t: tuple[int, int] = (1, 2)
    ts: list[tuple[float, float]] = [(1.0, 2.0)]
    print(t == (1, 2) in ts)         # True
    print(t != (1, 2) in ts)         # False
```

```output
True
False
```

Where the twin on the left cannot type the operand, the chain is refused with that twin's code, and
where the twin on the right refuses the operand at its own type, with that one's. A conditional
expression with a `None` arm takes its type from a needle slot (`(f() if c else None) in ys` is
accepted) but has none of its own beside `==`:

<!-- spec-sweep: error SPY0227 -->
```python
def f() -> int:
    return 1

def main():
    c: bool = True
    a: int | None = 1
    ys: list[int | None] = [1, None]
    print(a == (f() if c else None) in ys)   # SPY0227, as `a == (f() if c else None)` is
```

A chain headed by `x is A.B` takes `A.B` as an identity operand when it names an enum member, as
above. Any other dotted name or class name there is the type test `x is T`, which is refused
(SPY0349) in a chain as it is alone:

<!-- spec-sweep: error SPY0349 -->
```python
class Dog:
    pass

def main():
    x: Dog = Dog()
    d: Dog = x
    print(x is Dog is d)             # SPY0349
```

## Evaluation

Each intermediate expression is evaluated only once:

<!-- spec-sweep: fragment -->
```python
# f() is called only once, not twice
a < f() < c         # Equivalent to: _temp = f(); a < _temp and _temp < c
```

The operands are evaluated left to right, each at most once, and a membership link's needle before
its container:

<!-- spec-sweep: output -->
```python
def trace(name: str, v: int) -> int:
    print(name)
    return v

def container() -> list[int]:
    print("container")
    return [2, 3]

def main():
    print(trace("a", 1) < trace("b", 2) in container())
```

```output
a
b
container
True
```

## Short-Circuit Behavior

Chained comparisons short-circuit: if any comparison in the chain evaluates to `false`, subsequent comparisons are **not evaluated**:

<!-- spec-sweep: fragment -->
```python
# If a < b is false, b < c is never evaluated
a < b < c           # Short-circuits: if (a < b) is false, skips (b < c)

# Useful when later comparisons could have side effects or errors
x > 0 and x < len(items) and items[x] > threshold
```

This matches Python's short-circuit semantics for chained comparisons.

*Implementation*
- *🔄 Lowered - Expanded to `a < b && b < c` with single evaluation of middle expression. The `&&` operator provides short-circuit evaluation.*
