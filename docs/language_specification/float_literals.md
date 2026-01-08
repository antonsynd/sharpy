# Float Literals

```python
# Decimal floats (64-bit)
pi = 3.14159
half = 0.5
negative = -2.718

# Digit before or after decimal point is optional
valid1 = 0.5
valid2 = 5.0
valid3 = .5  # Like Python, but unlike C#, this is valid
valid3 = 5.  # Also valid

# Underscores for readability (optional)
precise = 3.141_592_653
```

## Type Inference

- Float literals with decimal point are inferred as `float64` (System.Double)
- Suffix notation for explicit typing (optional):
  - `f` or `F` for `float32` (System.Single): `3.14f`
  - `d` or `D` for `float64` (System.Double): `3.14d` (redundant but allowed)
  - `m` or `M` for `decimal` (System.Decimal): `3.14m`

*Implementation*
- *✅ Native - Direct mapping to C# float literals.*
