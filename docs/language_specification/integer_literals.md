# Integer Literals **[v0.1.0]**

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

- Integer literals are inferred as `int` (maps to `System.Int32`)
- Suffix notation for explicit sizing (optional):
  - `L` or `l` for `long` (System.Int64): `42L`
  - `u` or `U` for `uint` (System.UInt32): `42u`
  - `ul` or `UL` for `ulong` (System.UInt64): `42ul`

**Note:** Like C#, there are no literal suffixes for `short`, `ushort`, `byte`, or `sbyte`. Use type annotations or explicit casts:

```python
# Type annotation
s: short = 42
b: byte = 255
sb: sbyte = -128

# Explicit casting (with `to` operator)
s = 42 to short
b = 255 to byte
```

*Implementation: ✅ Native - Direct mapping to C# integer literals.*
