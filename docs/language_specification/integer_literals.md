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

- Integer literals without a suffix are inferred as `int` (32-bit, `System.Int32`) by default
- If the literal value exceeds `Int32.MaxValue` (2,147,483,647), it is automatically promoted to `long` (64-bit, `System.Int64`)
- If the literal value exceeds `Int64.MaxValue` (9,223,372,036,854,775,807), it is a compile-time error
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

# Explicit casting (with the `as!` operator)
s = 42 as! int16
b = 255 as! uint8
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

## Literals in a mixed-type expression

A suffixed literal carries its own type: `5u` is `uint32` and `5ul` is `uint64`, exactly as a
variable of that type would be. An unsuffixed literal too large for `int` is `int64`.

```python
# a: int32 = 5u             # ERROR (SPY0220): Cannot assign type 'uint32' to variable of type 'int32'
# b: int32 = 4294967296     # ERROR (SPY0220): Cannot assign type 'int64' to variable of type 'int32'
```

When such a literal meets an operand of another type, the pair goes through C#'s binary numeric
promotion (§12.4.7) — the same table `+`, `<` and `&` all use, tabulated in
[Numeric Type Promotion](arithmetic_operators.md#numeric-type-promotion). A `uint64` operand
against any *signed* operand has no common type and is refused (SPY0222).

**A constant operand converts first.** When exactly one operand is an integer constant whose
**value** fits the other operand's type, the constant converts to that type before promotion
(C# §10.2.11), so the operator sees a same-type pair and the result keeps the variable's type:

```python
b: uint32 = 5
c: uint32 = b + 1        # 6 — the constant 1 converts to uint32; the result is uint32

u: uint64 = 5
s: uint64 = u + 4294967296   # 4294967301 — an int64-typed constant that fits uint64 converts too

a: uint64 = 5
# print(a + (-1))        # ERROR (SPY0222): Type 'uint64' does not support operator '+'
#                        #   with operand of type 'int32' — -1 has no uint64 value
```

The constant's own recorded type does not change (hover on `1` still shows `int`); only the
operator's effective operand types do. A constant whose value does *not* fit stays its own type and
the ordinary promotion applies, so `uint32 + 4294967296` is `int64`.

*Implementation*
- *✅ Native - Direct mapping to C# integer literals.*
