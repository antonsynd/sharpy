# Assignment Operators

| Operator | Description |
|----------|-------------|
| `=` | Simple assignment |
| `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=` | Augmented arithmetic |
| `&=`, `\|=`, `^=`, `<<=`, `>>=` | Augmented bitwise |

In the current version of Sharpy, user definitions of assignment operators like `+=` via dunder methods (e.g. `__iadd__`) are not supported.

## Augmented Narrowing Rule

When the target of an augmented assignment is a narrow integer type (`int8`,
`uint8`, `int16`, `uint16`), the compound operation is allowed **if and only if**
the right-hand side is implicitly convertible to the target type. This mirrors
C#'s own compound-assignment rule (C# spec §12.21.4): `x += y` is equivalent to
`x = (T)(x + y)` where `T` is the type of `x`, and the narrowing cast is
inserted only when the conversion is lossless at the type level.

| Target | RHS type | Result |
|--------|----------|--------|
| `int8` | `int8` | Accepted — both operands are the same narrow type |
| `int8` | `int` | Refused (SPY0220) — `int` is not implicitly convertible to `int8` |
| `uint8` | `uint8` | Accepted |
| `int16` | `int16` | Accepted |
| `uint16` | `uint16` | Accepted |

```python
x: int8 = 5
y: int8 = 3
x += y              # OK: narrows (both operands are int8)
print(x)            # 8

x2: int8 = -7
y2: int8 = 2
x2 //= y2           # OK: narrows (both operands are int8)
print(x2)           # -4

u: uint8 = 200
v: uint8 = 50
u += v              # OK: narrows (both operands are uint8)
print(u)            # 250
```

A plain store of the arithmetic result into a narrow target is always refused,
because the expression result is promoted to `int32`:

```python
a: int8 = 5
b: int8 = 3
# c: int8 = a + b  # SPY0220: 'int' is not assignable to 'int8'
c: int = a + b      # OK: store in int
```

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
- *🔄 Augmented narrowing: the `NarrowTo` pass inserts an implicit cast when the
  RHS is assignable to the target's narrow type.*
- *🔄 In-place collection mutation: the classifier routes mutable collection
  receivers to the corresponding mutation method instead of rebinding.*
