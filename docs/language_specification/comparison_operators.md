# Comparison Operators

| Operator | Description |
|----------|-------------|
| `==` | Equality |
| `!=` | Inequality |
| `<` | Less than |
| `>` | Greater than |
| `<=` | Less than or equal |
| `>=` | Greater than or equal |

## Operand types

A comparison's **result** is always `bool`, but its **operands** go through the same binary numeric
promotion as `+`, `-` and `*` — C# §12.4.7, tabulated in
[Numeric Type Promotion](arithmetic_operators.md#numeric-type-promotion). A pair the table refuses
is refused here too: the comparison operators do not have a wider set of admissible pairs than the
arithmetic ones.

| Left | Right | Comparison | Notes |
|------|-------|------------|-------|
| `uint32` | `int16` | `bool` | Both promote to `int64`, then compare |
| `uint64` | `uint32` | `bool` | Both promote to `uint64` |
| `uint64` | `1` | `bool` | Constant operand converts first (§10.2.11) |
| `int` | `float` | `bool` | Integer promotes to `float64` |
| `decimal` | `int` | `bool` | Integer promotes to `decimal` |
| `float32` | `float64` | `bool` | Lower precision promotes |
| `uint64` | any signed | ❌ SPY0222 | No common type; cast one operand |
| `decimal` | `float64` | ❌ SPY0222 | No common type; convert one operand |

```python
a: uint32 = 5
b: int16 = 4
r: bool = a > b          # True  — both promote to int64

c: uint64 = 5
d: uint32 = 4
s: bool = c > d          # True  — both promote to uint64

t: bool = c < 1          # False — the constant 1 converts to uint64 first
```

### Pairs with no common type

`uint64` against any signed type has no common type in C# (§12.4.7 offers `long` and `ulong`, and
neither holds both ranges), so the comparison is refused:

```python
e: uint64 = 5
f: int32 = 4
# g: bool = e < f        # ERROR (SPY0222): Type 'uint64' does not support
#                        #   operator '<' with operand of type 'int32'

h: bool = int64(e) < int64(f)    # False — compare as int64
i: bool = e < uint64(f)          # False — or compare as uint64
```

Pick the cast that matches the range you actually need: `int64(e)` loses values above 2⁶³−1,
`uint64(f)` loses negative values.

`decimal` against `float64` is refused for the same structural reason — C# has no implicit
conversion in either direction, because `decimal` has more precision and `double` more range:

```python
d1: decimal = 1
f1: float64 = 2.0
# j: bool = d1 < f1      # ERROR (SPY0222): Type 'decimal' does not support
#                        #   operator '<' with operand of type 'float64'

k: bool = d1 < decimal(f1)   # True — compare as decimal
l: bool = float(d1) < f1     # True — or compare as float64
```

**This is an Axiom-1 deviation.** CPython compares the two happily (`Decimal(1) < 2.0` is `True`),
because it converts the float to an exact decimal for the comparison. Sharpy follows .NET, where
the pair does not bind. Catalogued in `docs/deviations.yaml` as `decimal-float-comparison-refused`;
the remedy is to name the type you want to compare in.

### Comparing against `None`

`x == None` is refused for a value-typed operand — the supported spelling is `x is None`, and the
diagnostic says so:

```python
x: int = 1
# if x == None:          # ERROR (SPY0222): Type 'int32' does not support operator
#                        #   '==' with operand of type 'None'. Did you mean 'is None'?

y: int | None = None
# if y == None:          # ERROR (SPY0222): Type 'int32 | None' does not support operator
#                        #   '==' with operand of type 'None'. Did you mean 'is None'?
```

A **reference**-typed operand compares against `None` directly — the comparison lowers to a null
check, which is what the author meant:

```python
s1: str = "a"
print(s1 == None)        # False
s2: str | None = None
print(s2 == None)        # True
```

`is None` works for both and is the spelling that narrows (see
[Type Narrowing](type_narrowing.md)), so prefer it everywhere.

*Implementation*
- *✅ Native - Direct mapping.*
