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

## Narrowing an Unsuffixed Literal to `float32`

An **unsuffixed** float literal may initialize a `float32` **declaration** — a variable or a field
— without the `f` suffix. The literal is typed `float32` and emitted as an `f`-suffixed C# literal:

```python
def main() -> None:
    x: float32 = 0.1      # OK — the literal narrows to float32
    print(x)              # 0.1

class Holder:
    ratio: float32 = 0.5  # OK — same rule for a field declaration
```

The allowance is scoped to declarations, where the annotation sits next to the literal. It does
**not** extend to signature positions, which callers read:

```python
def ret() -> float32:
    return 0.1            # ERROR (SPY0260) — write 0.1f

def takes(x: float32 = 0.1) -> None:   # ERROR (SPY0220) — write 0.1f
    pass
```

Two further limits:

- Only a **literal** narrows. A `float64`-typed *expression* or variable still requires an explicit
  cast: `x: float32 = y` is SPY0220 for `y: float`.
- The value must be within `float32`'s range. `x: float32 = 1e40` is SPY0220, not a silent infinity.

This is a deliberate divergence from C#, which rejects `float f = 0.1;` outright. Precision loss
within range is accepted — writing the annotation is taken as asking for it.

*Implementation*
- *✅ Native - Direct mapping to C# float literals.*
