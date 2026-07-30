# Arithmetic Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `+` | Addition | `+` |
| `-` | Subtraction | `-` |
| `*` | Multiplication | `*` |
| `/` | Division* | `/` (with cast if necessary) |
| `//` | Floor division** | `Math.Floor` (integer/float); `decimal.Truncate(decimal.Divide(x, y))` for decimal |
| `%` | Modulo*** | `Sharpy.Builtins.FloorMod` (integer/float); native `%` for other types |
| `**` | Exponentiation | Integer: constant folding / checked integer power; float: `Math.Pow(x, y)` (see below) |

## Division Operator `/`

The `/` operator always produces a floating-point result, following Python's semantics where division never truncates. The result type is determined by the operands:

| Operand Types | Result Type | Notes |
|---------------|-------------|-------|
| Both `decimal` | `decimal` | High-precision division |
| `decimal` + any integer | `decimal` | Integer promoted to decimal |
| Any `float64` | `float64` | |
| Any `float32` (no `float64`/`decimal`) | `float32` | |
| Integer types only | `float64` | Always promotes to `float64` |

## Floor Division Operator `//`

For integer and float operands, floor division returns the largest integer less
than or equal to the mathematical quotient (rounds toward negative infinity).
`decimal` truncates toward zero instead — see [Decimal floor
division](#decimal-floor-division) below. The return type depends on the
operands:

| Operands | Result Type | Rounding |
|----------|-------------|----------|
| Any integer types | `int32` | Floored (toward negative infinity) |
| Any float type | Same float type | Floored (toward negative infinity) |
| Mixed integer and float | Float type of the float operand | Floored (toward negative infinity) |
| Both `decimal` | `decimal` | **Truncated** (toward zero) — see below |
| `decimal` + any integer | `decimal` | **Truncated** (toward zero) — not yet accepted, see [#1188](https://github.com/antonsynd/sharpy/issues/1188) |

**Examples:**
```python
7 // 3      # 2 (int32)
-7 // 3     # -3 (int32), not -2
7.5 // 2.0  # 3.0 (float64)
7 // 2.0    # 3.0 (float64) - mixed: result is float64
7.0 // 2    # 3.0 (float64) - mixed: result is float64
7.0f // 2   # 3.0f (float32) - mixed: result is float32
```

### Decimal floor division

`decimal` keeps the native CLR division and **truncates toward zero** instead of
flooring, mirroring the same native-decimal policy as `%` (see below). This is
not a divergence from Python: CPython's `Decimal.__floordiv__` truncates as
well, so the native path satisfies Axiom 1 and Python conformance at once. Only
the mixed-sign cases differ from integer `//`:

```python
7m // 3m     # 2   (same as int 7 // 3)
-7m // 3m    # -2  (truncated; int -7 // 3 is -3)
7m // -3m    # -2  (truncated; int 7 // -3 is -3)
-7m // -3m   # 2   (same as int -7 // -3)
-17m // 5m   # -3  (truncated; int -17 // 5 is -4)
7.5m // 2m   # 3   (the quotient truncates, not the operands)
```

The divmod identity `a == (a // b) * b + (a % b)` documented below still holds
for `decimal`, because `//` and `%` are consistently *both* native: a truncated
quotient pairs with a remainder that takes the sign of the dividend, exactly as
in Python's `Decimal`. What changes for decimal is which of the two consistent
conventions applies, not whether the two operators agree:

```python
(-7m // 3m) * 3m + (-7m % 3m)   # -7  (-2 * 3 + -1)
(-7 // 3) * 3 + (-7 % 3)        # -7  (-3 * 3 + 2, floored)
```

**Division by zero** raises `ZeroDivisionError`, as with every other Sharpy
division. CPython raises `decimal.DivisionByZero`, which is a `ZeroDivisionError`
subclass, so `except ZeroDivisionError` catches it in both languages:

```python
7m // 0m    # ZeroDivisionError: decimal floor division by zero
```

### User-defined types

`//` is defined for numeric types only. Unlike `%` (`__mod__` → `operator %`),
there is no `__floordiv__` dunder: C# has no `//` operator to map one onto, so a
user-defined or CLR type used with `//` is rejected at compile time with
**SPY0222** (`Type 'T' does not support operator '//' with operand of type 'U'`).
Types that want integer-quotient semantics expose a named method instead.

## Modulo Operator `%`

For integer and float operands, the `%` operator returns the remainder of
**floored** division. Following Python's semantics, the result takes the **sign
of the divisor** (not the sign of the dividend, as C#'s native `%` would give).
This keeps the language coherent with floored `//` and `divmod`: the identity
`a == (a // b) * b + (a % b)` holds for all operands — including `decimal`, where
`//` and `%` are consistently both native/truncated.

The return type depends on the operands:

| Operands | Result Type | Remainder sign |
|----------|-------------|----------------|
| Any integer types | Same integer type (`int32`/`int64`) | Sign of the divisor (floored) |
| Any float type | Same float type | Sign of the divisor (floored) |
| Mixed integer and float | Float type of the float operand | Sign of the divisor (floored) |
| Both `decimal` | `decimal` | **Sign of the dividend** (native `%`, truncated) |

**Examples:**
```python
7 % 3       # 1
-7 % 3      # 2  (sign of divisor, not -1)
7 % -3      # -2
-7 % -3     # -1
-7.5 % 2    # 0.5  (float64)
7.5 % -2    # -0.5 (float64)
7 % 2.0     # 1.0  (float64) - mixed: result is float64
```

**Divmod identity** — modulo and floor division agree with `divmod`:
```python
divmod(-7, 3)               # (-3, 2)
(-7 // 3) * 3 + (-7 % 3)    # -7  (identity holds; would be -10 under truncation)
```

**Division by zero** raises `ZeroDivisionError` for both integers and floats
(C#'s native `%` throws `DivideByZeroException` for integers and silently yields
`NaN` for floats, so both are lowered through the runtime helper):
```python
7 % 0       # ZeroDivisionError: integer modulo by zero
7.0 % 0.0   # ZeroDivisionError: float modulo
```

Decimal `%` by zero does not yet reach that surface — it escapes as a CLR
`DivideByZeroException`, and a literal `0m` divisor fails to compile
([#1189](https://github.com/antonsynd/sharpy/issues/1189)). Decimal `//` by zero
does raise `ZeroDivisionError`.

## Exponentiation Operator `**`

Sharpy integers are fixed-width (Axiom 1), so integer exponentiation never
silently saturates or loses precision:

| Case | Behavior |
|------|----------|
| Constant `int ** int` (non-negative exponent) | Folded at compile time. Result is typed `int` if it fits, widened to `long` if it fits `long`; otherwise compile error **SPY0328** (`IntegerPowerOverflow`). |
| Non-constant integer `**` (non-negative exponent) | Checked exponentiation-by-squaring (`Sharpy.Builtins.CheckedIntPow`); raises `OverflowError` on overflow. Results are exact across the full `long` range (no `Math.Pow` rounding above 2^53). |
| Integer `**` negative exponent | Truncating `Math.Pow` double path (`int ** int` stays `int`, e.g. `2 ** -1` is `0`). |
| Any float operand | `Math.Pow(x, y)`, result is float. |

## Implementation

- *Standard: ✅ Native*
- *`**`: 🔄 Constant-folded or lowered to `Sharpy.Builtins.CheckedIntPow()` for
integers, `Math.Pow()` for floats. See table above.*
- *`/`: 🔄 Lowered to floating-point division. See table above.*
- *`//`: 🔄 Lowered to `(int)Math.Floor((double)a / b)` for integers,
`Math.Floor(a / b)` for floats, and
`decimal.Truncate(decimal.Divide(a, b))` for decimal (truncated toward zero,
matching Python's `Decimal`); all three guard a zero divisor with
`ZeroDivisionError`. `decimal.Divide` rather than `/` because a literal zero
divisor through `/` is a C# compile error (CS0020).*
- *`%`: 🔄 Lowered to `Sharpy.Builtins.FloorMod(a, b)` for integer/float operands
(Python floored modulo, sign of divisor, `ZeroDivisionError` on zero); native `%`
for decimal and user-defined `operator %` types.*

## Numeric Type Promotion

When binary arithmetic operators (`+`, `-`, `*`) operate on different numeric types, operands are implicitly promoted following .NET's numeric promotion rules. These rules are designed to be intuitive and follow the spirit of Python's simple "promote integers to floats when mixed" philosophy, adapted to .NET's richer type system:

| Left Type | Right Type | Result Type | Notes |
|-----------|------------|-------------|-------|
| `int32` | `int32` | `int32` | |
| `int32` | `int64` | `int64` | Smaller promoted to larger |
| `int32` | `float64` | `float64` | Integer promoted to float |
| `int32` | `decimal` | `decimal` | Integer promoted to decimal |
| `float32` | `float64` | `float64` | Lower precision promoted |
| `float64` | `decimal` | ❌ Error | Cannot mix double and decimal |
| `uint8` | `int32` | `int32` | Small integers promote to int |
| `int16` | `int32` | `int32` | Small integers promote to int |

**Key Rules:**

1. **Integer operations**: Result is the larger integer type (but at least `int32`)
2. **Float operations**: Result is the higher-precision float type
3. **Mixed integer/float**: Integer is promoted to the float type
4. **Decimal is special**: Can mix with integers, but not with `float32`/`float64`

*Note: Python itself has only `int`, `float` (equivalent to Sharpy's `int32` and `float64` which have aliases `int` and `float`), and `complex` as built-in numeric types. Sharpy's rules handle .NET's richer type system (`int8`, `int16`, `int64`, ..., `float32` vs `float64`, `decimal`) while maintaining Python-like simplicity.*

```python
# Numeric promotion examples
1 + 2           # int32 + int32 = int32
1 + 2L          # int32 + int64 = int64
1 + 2.0         # int32 + float64 = float64
1.0f + 2.0      # float + float64 = float64
1 + 2m          # int32 + decimal = decimal
1.0 + 2m        # ERROR: float64 + decimal is not allowed
```

*Implementation*
- *✅ Native - Follows C# numeric promotion rules.*
