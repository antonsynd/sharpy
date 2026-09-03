# Assignment Operators

| Operator | Description |
|----------|-------------|
| `=` | Simple assignment |
| `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=` | Augmented arithmetic |
| `&=`, `\|=`, `^=`, `<<=`, `>>=` | Augmented bitwise |

In the current version of Sharpy, user definitions of assignment operators like `+=` via dunder methods (e.g. `__iadd__`) are not supported.

## Augmented Narrowing Rule

When the result of `x op= y` is wider than the integer type of `x`, Sharpy
applies **C#'s own compound-assignment rule** (C# spec §12.21.4): the statement
is evaluated as `x = (T)(x op y)`, where `T` is the type of `x`, when

1. the result is *explicitly* convertible to `T` — always true between integer
   types — **and**
2. `y` is *implicitly* convertible to `T`: a **narrow-or-equal width**
   (`int8 += int8`) or an **in-range integer constant** (`int8 += 1`, including
   a folded constant expression and a `const` reference) — **or** the operator
   is a **shift** (`<<=`, `>>=`), whose right operand is a shift *count* and is
   never converted to the target type.

Otherwise the augmented assignment is refused with **SPY0220**, naming the
promoted result type. The rule is scoped to integer targets: `f: float32`
with `f += 1.0` stays refused.

| Target | RHS | Result |
|--------|-----|--------|
| `int8` | `int8` (narrow-or-equal width) | Accepted |
| `int8` | `1` (in-range constant) | Accepted — §10.2.11 constant conversion |
| `int8` | `300` (out-of-range constant) | **Refused** (SPY0220) |
| `int8` | `i`, an `int` variable | **Refused** (SPY0220) |
| `int8` | any `int` value with `<<=` / `>>=` | Accepted — a count, not a value of the target |
| `uint8` | `1` | Accepted — `u -= 1` on `0` wraps to `255` |
| `uint32` | `uint32` | Accepted — `//=`, `%=`, `**=` produce `int64` and narrow back |
| `float32` | `1.0` | **Refused** (SPY0220) — the rule covers integer targets only |

```python
x: int8 = 5
y: int8 = 3
x += y              # OK: narrows (both operands are int8)
print(x)            # 8

x2: int8 = -7
y2: int8 = 2
x2 //= y2           # OK: narrows (both operands are int8)
print(x2)           # -4

x3: int8 = -7
x3 %= 2             # OK: 2 is an in-range int8 constant
print(x3)           # 1

u: uint8 = 200
v: uint8 = 50
u += v              # OK: narrows (both operands are uint8)
print(u)            # 250

z: uint8 = 0
z -= 1              # OK: 1 is in range; the cast wraps
print(z)            # 255

p: int8 = 127
p += 2              # OK; unchecked narrowing wraps
print(p)            # -127

s: int8 = 5
n: int = 3
s <<= n             # OK: a shift COUNT is never converted to int8
print(s)            # 40

# i: int = 3
# x += i            # SPY0220: 'int' is not implicitly convertible to 'int8'
# x += 300          # SPY0220: 300 is not in int8's range
```

### Targets

The rule is **one decision, taken once**, and it does not consult what is being
assigned to. Identifier, attribute (`b.n`, `self.n`), nested attribute
(`o.inner.n`), index (`xs[0]`) and dict-value (`d[k]`) targets narrow
identically:

```python
class Box:
    n: int8 = 7

b: Box = Box()
y: int8 = 2
b.n += y            # attribute
print(b.n)          # 9

seven: int8 = 7
xs: list[int8] = [seven]
xs[0] //= y         # index
print(xs[0])        # 3
```

### Shifts

`<<=` and `>>=` are the one carve-out in the rule above, and it is C#'s: the
right operand of a shift is a *count*, not a value of the target type, so it is
never required to fit the target. `x8 <<= 2`, `x8 <<= i` and `x8 <<= 300` are
all accepted; the count is masked exactly as C# masks it (`& 31` for a 32-bit
promoted left operand), and the result is narrowed back into the target.

A plain store of the arithmetic result into a narrow target is still refused,
because the expression result is promoted to `int32`:

```python
a: int8 = 5
b: int8 = 3
# c: int8 = a + b  # SPY0220: 'int32' is not assignable to 'int8'
c: int = a + b      # OK: store in int
```

## Storing an Integer Constant

An integer constant whose **value** fits the destination converts implicitly at
**every** store position — not only at a declaration (C# spec §10.2.11):

```python
class Box:
    n: int8 = 0
    property p: int8 = 0       # property default

def take(v: int8) -> None:
    print(v)

def make() -> int8:
    return 120                  # return

def gen() -> int8:
    yield 1                     # yield

def main() -> None:
    x: int8 = 7                 # declaration
    x = 120                     # plain store
    print(x)                    # 120
    b: Box = Box()
    b.n = 120                   # attribute store
    xs: list[int8] = [x]
    xs[0] = 120                 # index store
    d: dict[str, int8] = {}
    d["k"] = 120                # dict-value store
    take(120)                   # positional argument
    take(v=120)                 # keyword argument
    print(make())               # 120
    for v in gen():
        print(v)                # 1
    ys: list[int8] = [1, 2]     # collection-literal elements
    f: () -> int8 = lambda: 7   # lambda body under a typed target
    if (x := 7) > 0:            # walrus
        print(x)                # 7
    r: int8 = 7 if True else 8  # conditional-of-constants
    print(r)                    # 7
    # x = 300                   # SPY0220: 300 is not in int8's range
```

The value is checked, not the literal's spelling: folded expressions
(`1 << 6`) and `const` references fold to their value first.

## Augmented Assignment on Collections

When the target of `+=`, `|=`, `&=`, `-=`, `^=`, or `*=` is a mutable collection
(`list`, `set`, `dict`), the operation **mutates the receiver in place** — every
alias sees the change. This matches CPython's `__iadd__`/`__ior__`/etc. semantics.

| Operator | Collection | Method called |
|----------|-----------|---------------|
| `+=` | `list[T]` | `Extend(other)` |
| `*=` | `list[T]` | `InPlaceRepeat(n)` |
| `\|=` | `set[T]` | `Update(other)` |
| `&=` | `set[T]` | `IntersectionUpdate(other)` |
| `-=` | `set[T]` | `DifferenceUpdate(other)` |
| `^=` | `set[T]` | `SymmetricDifferenceUpdate(other)` |
| `\|=` | `dict[K,V]` | `Update(other)` |

```python
xs: list[int] = [1, 2]
ys = xs              # alias
xs += [3, 4]
print(xs)            # [1, 2, 3, 4]
print(ys)            # [1, 2, 3, 4] — same object, mutated in place
print(xs is ys)      # True
```

### Targets

Augmented collection assignment works on identifier, attribute (`self.xs`), and
index (`d[k]`) targets. The index expression is evaluated exactly once.

### Narrowed receivers

If the receiver was narrowed via `isinstance`, augmented assignment is refused
(SPY0276) because the mutation may invalidate the narrowing. The steer suggests
rebinding through a temporary:

```python
# tmp = xs; tmp += [...]; xs = tmp  # workaround for narrowed receivers
```

### Immutable collections

For immutable types like `frozenset`, `+=`/`|=` etc. rebind the target (creating
a new object) rather than mutating — aliases are **not** updated.

*Implementation*
- *✅ Native - Direct mapping (except `**=` and `//=` which are lowered).*
- *🔄 Augmented narrowing: the checker records `NarrowTo` on the assignment when
  §12.21.4 admits the narrowing (narrow-or-equal RHS, in-range constant RHS, or
  a shift), and the emitter casts the desugared value from that record — one
  decision for every operator and every target kind.*
- *🔄 In-place collection mutation: the classifier routes mutable collection
  receivers to the corresponding mutation method instead of rebinding.*
