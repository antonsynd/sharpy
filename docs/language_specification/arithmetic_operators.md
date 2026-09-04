# Arithmetic Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `+` | Addition | `+` |
| `-` | Subtraction | `-` |
| `*` | Multiplication | `*` |
| `/` | Division* | `/` (with cast if necessary) |
| `//` | Floor division** | `Sharpy.Builtins.FloorDiv` (integer and float); `decimal.Truncate(decimal.Divide(x, y))` for decimal |
| `%` | Modulo*** | `Sharpy.Builtins.FloorMod` (integer/float); `decimal.Remainder(x, y)` for decimal; native `%` for other types |
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
| Both `int` | `int32` | Floored (toward negative infinity) |
| Any `long` operand (with `int` or `long`) | `int64` | Floored (toward negative infinity) |
| Narrow signed integers (`int8`, `int16`) | `int32` | Floored (toward negative infinity) |
| Narrow unsigned integers (`uint8`, `uint16`) | `int32` | Floored (toward negative infinity) |
| Both `uint32` | `int64` | Floored — see [Unsigned result widths](#unsigned-result-widths) |
| Both `uint64` | `uint64` | Floored (= truncated, since both operands are non-negative) |
| Any float type | Same float type | Floored (toward negative infinity) |
| Mixed integer and float | Float type of the float operand | Floored (toward negative infinity) |
| Both `decimal` | `decimal` | **Truncated** (toward zero) — see below |
| `decimal` + any integer | `decimal` | **Truncated** (toward zero) — the integer is promoted to `decimal` |

**Examples:**
```python
7 // 3      # 2 (int32)
-7 // 3     # -3 (int32), not -2
7.5 // 2.0  # 3.0 (float64)
7 // 2.0    # 3.0 (float64) - mixed: result is float64
7.0 // 2    # 3.0 (float64) - mixed: result is float64
7.0f // 2   # 3.0f (float32) - mixed: result is float32
```

Integer `//` is computed **in integer arithmetic** (`Sharpy.Builtins.FloorDiv`),
so quotients are exact across the full `int64` range — there is no
double-precision round trip to lose the low bits of a large `long`:

```python
big: long = 4611686018427387905L   # 2**62 + 1
print(big // 3L)                   # 1537228672809129301 — exact, matches CPython
```

One boundary is deliberately a runtime error: dividing an **`int`-typed**
`int.MinValue` by an `int`-typed `-1` (and the `long` equivalent at
`long.MinValue`) raises `OverflowError`. The mathematically correct quotient
(`2147483648`) does not fit the result type, and .NET traps this division even
in unchecked code, so the helper surfaces it as the ordinary Sharpy overflow
error rather than an unexplained crash. CPython, with arbitrary-precision
integers, computes `2147483648` — a documented divergence. Note the bare
literal spelling `(-2147483648) // -1` does NOT hit this boundary: the literal
`2147483648` is long-width by magnitude (#1320), so that spelling computes
`2147483648` in `long` arithmetic.

### Float floor division is not `Math.Floor(a / b)`

Float `//` lowers to `Sharpy.Builtins.FloorDiv`, which mirrors CPython's
`float_floor_div`: it derives the quotient from the raw `fmod` remainder
(`div = (a - fmod(a, b)) / b`, adjusted by one when the remainder's sign differs
from the divisor's) rather than flooring the quotient directly. The two are not
equivalent, because `a / b` can round up across an integer boundary:

```python
1.0 // 0.1   # 9.0  -- Math.Floor(1.0 / 0.1) would give 10.0
7.5 // 0.1   # 74.0 -- Math.Floor(7.5 / 0.1) would give 75.0
```

This is also what makes the divmod identity below hold for floats, not just
integers: `divmod` and `//` share the one implementation, exactly as CPython
implements `float_floor_div` by taking `float_divmod`'s first element.

A zero quotient carries the sign of the true quotient (`-0.0 // 1.0` is `-0.0`,
`-0.5 // -1.0` is `0.0`), matching the same CPython routine.

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
| `int32`, `int64` and the narrow widths (`int8`, `uint8`, `int16`, `uint16`) | `int32`, or `int64` if either operand is `int64` | Sign of the divisor (floored) |
| Both `uint32` | `int64` | Sign of the divisor — see [Unsigned result widths](#unsigned-result-widths) |
| Both `uint64` | `uint64` | Sign of the divisor (both operands are non-negative) |
| Any float type | Same float type | Sign of the divisor (floored) |
| Mixed integer and float | Float type of the float operand | Sign of the divisor (floored) |
| Both `decimal` | `decimal` | **Sign of the dividend** (native `%`, truncated) |
| `decimal` + any integer | `decimal` | **Sign of the dividend** (native `%`, truncated) |

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
(1.0 // 0.1) * 0.1 + (1.0 % 0.1)   # 1.0 (holds for floats too -- see above)
```

A zero float remainder carries the **divisor's** sign, matching CPython's
`float_mod` (C#'s native `%` gives it the dividend's sign):
```python
-1.0 % 1.0  # 0.0
1.0 % -1.0  # -0.0
```

**Division by zero** raises `ZeroDivisionError` for both integers and floats
(C#'s native `%` throws `DivideByZeroException` for integers and silently yields
`NaN` for floats, so both are lowered through the runtime helper):
```python
7 % 0       # ZeroDivisionError: integer modulo by zero
7.0 % 0.0   # ZeroDivisionError: float modulo
```

Decimal `%` by zero raises **`InvalidOperation`**, not `ZeroDivisionError`. This
mirrors CPython, where `Decimal(7) % Decimal(0)` raises
`decimal.InvalidOperation` — a *sibling* of `ZeroDivisionError`, not a subclass,
so `except ZeroDivisionError` does not catch it in either language:

```python
7m % 0m     # InvalidOperation: decimal modulo by zero
7m // 0m    # ZeroDivisionError: decimal floor division by zero
```

Decimal `//` by zero keeps `ZeroDivisionError` because CPython raises
`decimal.DivisionByZero` there, which *is* a `ZeroDivisionError` subclass (see
[Decimal floor division](#decimal-floor-division) above).

`InvalidOperation` derives from `ArithmeticError`, so `except ArithmeticError`
catches it alongside `ZeroDivisionError` and `OverflowError` — see the [exception
hierarchy](exception_handling.md#exception-type-hierarchy). CPython interposes a
`DecimalException` layer (`InvalidOperation` → `DecimalException` →
`ArithmeticError`) that Sharpy deliberately omits: there is exactly one decimal
exception and no `decimal` module namespace to anchor the layer, and both
observable contracts (`except InvalidOperation`, `except ArithmeticError`) hold
without it.

## Exponentiation Operator `**`

Sharpy integers are fixed-width (Axiom 1), so integer exponentiation never
silently saturates or loses precision:

| Case | Behavior |
|------|----------|
| Constant `int ** int` (non-negative exponent) | Folded at compile time. Result is typed `int` if it fits, widened to `long` if it fits `long`; otherwise compile error **SPY0328** (`IntegerPowerOverflow`). |
| Non-constant integer `**` (non-negative exponent) | Checked exponentiation-by-squaring (`Sharpy.Builtins.CheckedIntPow`); raises `OverflowError` on overflow. Results are exact across the full `long` range (no `Math.Pow` rounding above 2^53). |
| Integer `**` negative exponent | Truncating `Math.Pow` double path (`int ** int` stays `int`, e.g. `2 ** -1` is `0`). |
| Unsigned 32-/64-bit operands (`uint32`, `uint64`) | Evaluated — and typed — as `int64`: `Sharpy.Builtins.CheckedIntPow` has `(int, int)` and `(long, long)` overloads only, so there is no unsigned arm to bind. |
| Any float operand | `Math.Pow(x, y)`, result is float. |

## Constant Integer Arithmetic

Sharpy folds constant integer `+`, `-` (binary and unary), `*` and `<<` at
compile time, exactly. A constant result that does not fit the width the
compiler emits is a **compile error, SPY0348** (`ConstantIntegerOverflow`) —
never a silently wrong value, and never a leaked C# error (Roslyn folds
constants in a checked context, so an unfolded overflowing tree would surface
as CS0220):

```python
print(3794 * 1973 * 948)    # ERROR (SPY0348): 7096312776 does not fit int
print(3794L * 1973 * 948)   # OK: 7096312776 — a long operand makes the expression long
print(4294967296 + 1)       # OK: 4294967297 — the literal itself is long-width by magnitude
print(1 << 40)              # ERROR (SPY0348): 1099511627776 does not fit int
print(1L << 40)             # OK: 1099511627776 — the left operand carries the width
```

`<<` is in this list for a specific reason: without the check, .NET's count
masking made the wrong answer *quiet*. `1 << 40` on an `int` is `1 << 8`, so
the program printed `256` where Python prints `1099511627776`. See
[Bitwise Operators](bitwise_operators.md) for shift typing and the runtime
masking that remains.

### Why constant `**` widens and constant `+ - * <<` do not

Constant `**` widens `int` → `long` when the result needs it, and only errors
past 64 bits (SPY0328). Constant `+ - * <<` do not widen: they are checked at
the expression's own width and error there (SPY0348). The contrast is sharpest
between two values of similar size:

```python
print(2 ** 40)              # OK: 1099511627776 — widened to long
print(1099511 * 1000000)    # ERROR (SPY0348): 1099511000000 does not fit int
```

The rule behind the split is that **a constant must not type differently from
the same expression written with variables.** `int * int` is `int` at runtime,
so a constant `int * int` is `int` too — widening it would mean `a * b` and
`1099511 * 1000000` had different types, and a refactor that introduced a
variable would silently change a program's arithmetic. `**` is exempt because
it has no native C# operator: its result type is not inherited from anything,
it is Sharpy's to define, and defining it as "the smallest of int/long that
fits" costs nothing at runtime (the non-constant path already routes through
`CheckedIntPow`, which returns `long`).

Both halves are Axiom-1 consistent — C# applies exactly this reasoning to its
own constant expressions, and neither half lets a wrong value through.

Two deliberate asymmetries, both Axiom-1 (they are C#'s own rules for
constants):

- **Constant `+ - * <<` does not widen, unlike constant `**`** — the rule
  above.
- **Constant overflow errors; runtime overflow wraps.** Non-constant integer
  `+ - *` runs unchecked in the generated C# and wraps silently
  (`n + 1` at `int.MaxValue` is `int.MinValue`). Loud-at-compile-time,
  wrapping-at-runtime is exactly C#'s own split. CPython, with
  arbitrary-precision integers, computes every case exactly — a documented
  divergence (see `docs/deviations.yaml`, `int-overflow-checked`).

## Implementation

- *Standard: ✅ Native*
- *`**`: 🔄 Constant-folded or lowered to `Sharpy.Builtins.CheckedIntPow()` for
integers, `Math.Pow()` for floats. See table above.*
- *`/`: 🔄 Lowered to floating-point division. See table above.*
- *`//`: 🔄 Lowered to `Sharpy.Builtins.FloorDiv(a, b)` for integers (exact
integer arithmetic, int/long overloads) and floats, and
`decimal.Truncate(decimal.Divide(a, b))` for decimal (truncated toward zero,
matching Python's `Decimal`); all guard a zero divisor with
`ZeroDivisionError` inside the helper, which is what lets the emitter splice
each operand exactly once (#1216, #1226). `decimal.Divide` rather than `/`
because a literal zero divisor through `/` is a C# compile error (CS0020).*
- *`%`: 🔄 Lowered to `Sharpy.Builtins.FloorMod(a, b)` for integer/float operands
(Python floored modulo, sign of divisor, `ZeroDivisionError` on zero); to
`decimal.Remainder(a, b)` for decimal (native truncated remainder, sign of the
dividend, `InvalidOperation` on zero — `decimal.Remainder` rather than `%` because
a literal zero divisor through `%` is a C# compile error, CS0020); native `%` for
user-defined `operator %` types.*

## Numeric Type Promotion

Binary arithmetic (`+`, `-`, `*`), comparison (`==`, `!=`, `<`, `>`, `<=`, `>=`),
and bitwise (`&`, `|`, `^`) operators follow C#'s binary numeric promotion
(C# spec §12.4.7). Shifts (`<<`, `>>`) promote the left operand alone and are
exempt from the mixed-signedness rules below.

### Same-signedness pairs

| Left Type | Right Type | Result Type | Notes |
|-----------|------------|-------------|-------|
| `int32` | `int32` | `int32` | |
| `int32` | `int64` | `int64` | Smaller promoted to larger |
| `int32` | `float64` | `float64` | Integer promoted to float |
| `int32` | `decimal` | `decimal` | Integer promoted to decimal |
| `float32` | `float64` | `float64` | Lower precision promoted |
| `float64` | `decimal` | ❌ SPY0222 | Cannot mix double and decimal |
| `uint8` | `int32` | `int32` | Small integers promote to int |
| `int16` | `int32` | `int32` | Small integers promote to int |

### Mixed-signedness pairs (#1699)

When one operand is unsigned and the other is signed, C# §12.4.7 applies:

| Unsigned | Signed | Result | Rule |
|----------|--------|--------|------|
| `uint32` | `int8`, `int16`, `int32` | `int64` | Both convert to `long` |
| `uint32` | `int64` | `int64` | `uint` converts to `long` |
| `uint32` | `uint8`, `uint16` | `uint32` | Both convert to `uint` |
| `uint64` | any unsigned | `uint64` | Both convert to `ulong` |
| `uint64` | any signed | ❌ SPY0222 | No implicit conversion; cast one operand |

The table is order-symmetric: `int32 + uint64` follows the same rule as
`uint64 + int32`.

```python
a: uint32 = 5
b: int16 = 4
c: int64 = a + b     # OK: result is int64
# d: uint32 = a + b  # SPY0220: 'int64' is not assignable to 'uint32'

e: uint64 = 5
f: int32 = 4
# print(e + f)       # SPY0222: uint64 does not support '+' with int32
#                     #   — cast one operand: 'int64(e)' or 'uint64(f)'
```

### Constant-operand conversion (§10.2.11)

When exactly one operand is an integer constant whose **value** fits the other
operand's type, the constant converts to that type BEFORE promotion — the
operator sees two operands of the same type:

```python
b: uint32 = 5
c: uint32 = b + 1     # OK: 1 converts to uint32, result is uint32
print(c)               # 6

a: uint64 = 5
d: uint64 = a + 1     # OK: 1 converts to uint64, result is uint64

# print(a + (-1))     # SPY0222: -1 does not convert to uint64
```

This covers literal integers, `const` references, and folded constant
expressions (`1 << 2`). The constant's own recorded type is unchanged (hover
still shows `int`); only the operator's effective operand types change.

**Key Rules:**

1. **Integer operations**: Result is the larger integer type (but at least `int32`)
2. **Float operations**: Result is the higher-precision float type
3. **Mixed integer/float**: Integer is promoted to the float type
4. **Decimal is special**: Can mix with integers, but not with `float32`/`float64`
5. **Mixed signedness**: `uint32` with signed → `int64`; `uint64` with signed → refused
6. **Constant operand**: An in-range constant converts to the other operand's type first

### Narrow-width integer promotion floor

Narrow integer types (`int8`, `uint8`, `int16`, `uint16`) are promoted to `int32`
in every arithmetic expression — the result of `int8 + int8` is `int32`, not
`int8`. This follows .NET's own promotion rules (C# spec §12.4.7) and prevents
silent overflow: `int8` can hold 127, so `100 + 100` would wrap.

A plain store back into a narrow target is refused:

```python
a: int8 = 5
b: int8 = 3
# c: int8 = a + b  # SPY0220: 'int' is not assignable to 'int8'
c: int = a + b      # OK: int32 result stored in int
```

Augmented assignment (`+=`, `-=`, etc.) **narrows** when the right-hand side is
implicitly convertible to the target's type — a narrow-or-equal width or an
in-range integer constant — or when the operator is a shift. This is C#'s own
compound-assignment rule (C# spec §12.21.4); see
[Augmented Narrowing Rule](assignment_operators.md#augmented-narrowing-rule) for
the full table.

```python
x: int8 = 5
y: int8 = 3
x += y              # OK: narrows (both operands are int8)
x += 1              # OK: narrows (1 is in int8's range)

i: int = 3
# x += i            # SPY0220: 'int' is not assignable to 'int8'
# x += 300          # SPY0220: 300 is not in int8's range
```

The rule applies uniformly to all narrow widths and all arithmetic operators:
unary `-` and `~` also promote to `int32`, and `**` follows the same floor.

### Unsigned result widths

`uint32` and `uint64` are not narrow — they do not promote to `int32` — but four
operators still produce a **different** type than their operands, because the
predefined operator (or the lowering's overload set) has no unsigned arm at that
width. The result type is always the type the generated C# actually produces:

| Expression | Result type | Why |
|------------|-------------|-----|
| `uint32 + - * & \| ^ << >>` , `~uint32` | `uint32` | C# has predefined `uint` operators |
| `uint32 // uint32`, `uint32 % uint32` | `int64` | Lowered to `Builtins.FloorDiv`/`FloorMod`, whose overloads are `(int, int)`, `(long, long)`, `(ulong, ulong)`, float and double — a `uint` pair binds the `long` one |
| `uint32 ** uint32` | `int64` | `Builtins.CheckedIntPow` has `(int, int)` and `(long, long)` — a `uint` pair binds `long` |
| `-uint32` | `int64` | C# §12.9.3: unary `-` is predefined for `int`, `long`, `float`, `double`, `decimal`; a `uint` operand widens to `long` |
| `uint64 // uint64`, `uint64 % uint64`, `~uint64` | `uint64` | These lowerings *do* have a `ulong` overload |
| `uint64 ** uint64` | `uint64` | `Builtins.CheckedIntPow(ulong, ulong)` overload (#1700) |
| `-uint64` | ❌ **SPY0223** | C# has no unary `-` for `ulong` at all (CS0023); cast to `int64` first |

```python
a: uint32 = 7
b: uint32 = 2
print(a + b)        # 9   (uint32)
print(~a)           # 4294967288 (uint32)
q: int64 = a // b   # int64 destination, not uint32
print(q)            # 3
print(-a)           # -7  (int64)
# c: uint32 = a // b  # SPY0220: 'int64' is not assignable to 'uint32'

g: uint64 = 7
h: uint64 = 2
print(g // h)       # 3   (uint64 — the ulong overload exists)
# print(-g)         # SPY0223: type 'uint64' does not support unary operator '-'
```

The augmented forms narrow back into the target by the same §12.21.4 rule, so
`a //= b` and `a **= b` on `uint32` are accepted and store a `uint32`.

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
