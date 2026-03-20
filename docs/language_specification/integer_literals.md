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

## Overflow Promotion

When an integer literal exceeds the range of `int` (32-bit), the compiler automatically promotes it to the next wider type:

| Value Range | Inferred Type |
|-------------|---------------|
| -2,147,483,648 to 2,147,483,647 | `int` (System.Int32) |
| Outside `int` range | `int64` (System.Int64) |

```python
small = 42                    # int (fits in 32-bit)
large = 3_000_000_000         # int64 (exceeds int range, auto-promoted)
explicit = 42L                # int64 (explicit suffix)
```

Unsigned suffixes (`u`, `ul`) follow C# promotion rules.

*Implementation*
- *✅ Native - Direct mapping to C# integer literals.*
