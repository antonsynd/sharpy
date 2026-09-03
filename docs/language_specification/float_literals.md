# Float Literals

```python
# Decimal floats (64-bit)
pi = 3.14159
half = 0.5
negative = -2.718

# Leading decimal point is allowed (like Python and C#)
valid1 = 0.5
valid2 = 5.0
valid3 = .5    # Same as 0.5

# ❌ Invalid - trailing decimal point without digit
# invalid = 5.   # ERROR: use 5.0 instead

# Underscores for readability (optional)
precise = 3.141_592_653
```

## Type Inference

- Float literals with decimal point are inferred as `float64` (System.Double)
- Suffix notation for explicit typing (optional):
  - `f` or `F` for `float32` (System.Single): `3.14f`
  - `d` or `D` for `float64` (System.Double): `3.14d` (redundant but allowed)
  - `m` or `M` for `decimal` (System.Decimal): `3.14m`

## Narrowing an Unsuffixed Literal to `float32` or `decimal`

An **unsuffixed** float literal may narrow to `float32` or `decimal` at **every** store
position — the same rule as integer constants (§10.2.11 above). The literal is re-typed
and emitted with the appropriate suffix (`f` for `float32`, `m` for `decimal`):

```python
from System.Numerics import Vector2

class Holder:
    ratio: float32 = 0.5       # field declaration

def ret() -> float32:
    return 0.1                  # return

def takes(x: float32 = 0.1) -> None:   # parameter default
    print(x)

def main() -> None:
    x: float32 = 0.1           # declaration
    x = 0.25                    # plain store
    print(x)                    # 0.25
    takes(0.5)                  # argument
    print(ret())                # 0.1
    xs: list[float32] = [0.5]   # collection-literal element
    v: Vector2 = Vector2(1.0, 2.0)  # CLR argument
    print(v.X)                  # 1
```

The same rule applies to `decimal`:

```python
d: decimal = 1.5
print(d)                        # 1.5
```

Two limits:

- Only a **literal** narrows. A `float64`-typed *expression* or variable still requires an explicit
  cast: `x: float32 = y` is SPY0220 for `y: float`.
- The value must be within `float32`'s range. `x: float32 = 1e40` is SPY0220, not a silent infinity.

This is a deliberate divergence from C#, which rejects `float f = 0.1;` outright. Precision loss
within range is accepted — writing the annotation is taken as asking for it.

*Implementation*
- *✅ Native - Direct mapping to C# float literals with compiler-inserted suffixes.*
