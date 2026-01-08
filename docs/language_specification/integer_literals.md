# Integer Literals

```python
# Decimal integers
x = 0
y = 42
z = -10
large = 1000000

# Underscores for readability (optional)
million = 1_000_000
billion = 1_000_000_000
```

## Type Inference

- Integer literals are inferred as `int` (32-bit) (maps to `System.Int32`)
- Suffix notation for explicit sizing (optional):
  - `L` or `l` for `int64` (System.Int64): `42L`
  - `u` or `U` for `uint32` (System.UInt32): `42u`
  - `ul` or `UL` for `uint64` (System.UInt64): `42ul`

**Note:** Like C#, there are no literal suffixes for `int16`, `uint16`, `uint8`, or `int8`. Use type annotations or explicit casts:

```python
# Type annotation
s: int16 = 42
b: uint8 = 255
sb: int8 = -128

# Explicit casting (with `to` operator)
s = 42 to int16
b = 255 to uint8
```

*Implementation*
- *✅ Native - Direct mapping to C# integer literals.*
