# Float Literals

```python
# Decimal floats (64-bit)
pi = 3.14159
half = 0.5
negative = -2.718

# Must have digit before or after decimal point
valid1 = 0.5
valid2 = 5.0
invalid = .5  # ERROR: Must have digit before decimal

# Underscores for readability (optional)
precise = 3.141_592_653
```

## Type Inference

- Float literals with decimal point are inferred as `double` (System.Double)
- Suffix notation for explicit typing (optional):
  - `f` or `F` for `float` (System.Single): `3.14f`
  - `d` or `D` for `double` (System.Double): `3.14d` (redundant but allowed)
  - `m` or `M` for `decimal` (System.Decimal): `3.14m`

*Implementation: ✅ Native - Direct mapping to C# float literals.*
