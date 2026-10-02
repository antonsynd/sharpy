# builtins

Functions available without any import.

## Functions

### `abs(x: int) -> int`

Return the absolute value of a number.
Python: `abs(x)`

**Parameters:**

- `x` (int) -- The number

**Returns:** The absolute value

```python
abs(-5)      # 5
abs(3)       # 3
abs(-2.5)    # 2.5
```

### `abs(x: int64) -> int64`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: float) -> float`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: float32) -> float32`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: decimal) -> decimal`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: int16) -> int16`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: int8) -> int8`

Return the absolute value of a number.
Python: `abs(x)`

### `all(iterable: IEnumerable[T]) -> bool`

Return True if all elements of the iterable are True (or if the iterable is empty).

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to check

**Returns:** True if all elements are truthy, False otherwise

```python
all([True, True, True])    # True
all([True, False, True])   # False
all([])                    # True
```

### `any(iterable: IEnumerable[T]) -> bool`

Return True if any element of the iterable is True. If the iterable is empty, return False.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to check

**Returns:** True if any element is truthy, False otherwise

```python
any([False, False, True])    # True
any([0, 0, 0])              # False
any([])                      # False
```

### `ascii(obj: object) -> str`

Return a string with non-ASCII characters escaped.
Calls repr() first, then escapes non-ASCII characters with \xNN, \uNNNN, or \UNNNNNNNN.

```python
ascii("hello")      # "'hello'"
ascii("héllo")      # "'h\\xe9llo'"
```

### `bin(x: int) -> str`

Return a binary string prefixed with "0b".

**Parameters:**

- `x` (int) -- The integer to convert

**Returns:** A binary string representation

```python
bin(10)     # "0b1010"
bin(-10)    # "-0b1010"
bin(0)      # "0b0"
```

### `bin(x: int64) -> str`

Return a binary string prefixed with "0b" for long integers.

**Parameters:**

- `x` (int64) -- The long integer to convert

**Returns:** A binary string representation

### `bool(b: bool) -> bool`

Convert a bool to bool (identity).

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** The same bool value

### `bool(d: decimal) -> bool`

Convert a decimal to bool. Returns False if zero, True otherwise.

**Parameters:**

- `d` (decimal) -- The decimal value

**Returns:** False if zero, True otherwise

### `bool(f: float32) -> bool`

Convert a float to bool. Returns False if zero, True otherwise.

**Parameters:**

- `f` (float32) -- The float value

**Returns:** False if zero, True otherwise

### `bool(d: float) -> bool`

Convert a double to bool. Returns False if zero, True otherwise.

**Parameters:**

- `d` (float) -- The double value

**Returns:** False if zero, True otherwise

### `bool(i: int) -> bool`

Convert an int to bool. Returns False if zero, True otherwise.

**Parameters:**

- `i` (int) -- The int value

**Returns:** False if zero, True otherwise

### `bool(u: uint32) -> bool`

Convert a uint to bool. Returns False if zero, True otherwise.

**Parameters:**

- `u` (uint32) -- The uint value

**Returns:** False if zero, True otherwise

### `bool(s: int16) -> bool`

Convert a short to bool. Returns False if zero, True otherwise.

**Parameters:**

- `s` (int16) -- The short value

**Returns:** False if zero, True otherwise

### `bool(u: uint16) -> bool`

Convert a ushort to bool. Returns False if zero, True otherwise.

**Parameters:**

- `u` (uint16) -- The ushort value

**Returns:** False if zero, True otherwise

### `bool(l: int64) -> bool`

Convert a long to bool. Returns False if zero, True otherwise.

**Parameters:**

- `l` (int64) -- The long value

**Returns:** False if zero, True otherwise

### `bool(u: uint64) -> bool`

Convert a ulong to bool. Returns False if zero, True otherwise.

**Parameters:**

- `u` (uint64) -- The ulong value

**Returns:** False if zero, True otherwise

### `bool(b: uint8) -> bool`

Convert a byte to bool. Returns False if zero, True otherwise.

**Parameters:**

- `b` (uint8) -- The byte value

**Returns:** False if zero, True otherwise

### `bool(s: int8) -> bool`

Convert an sbyte to bool. Returns False if zero, True otherwise.

**Parameters:**

- `s` (int8) -- The sbyte value

**Returns:** False if zero, True otherwise

### `bool(s: str) -> bool`

Convert a string to bool. Returns False if the string is None or empty, True otherwise.

**Parameters:**

- `s` (str) -- The string value

**Returns:** False if None or empty, True otherwise

### `bool(tuple: system.runtime.compiler_services.ITuple) -> bool`

Return the truth value of a tuple: False when empty, True otherwise.

**Parameters:**

- `tuple` (system.runtime.compiler_services.ITuple) -- The tuple value

**Returns:** False if the tuple is empty, True otherwise

!!! note
    Tuples are emitted as `System.ValueTuple` instances, which
    implement `System.Runtime.CompilerServices.ITuple` but
    neither `ICollection` nor
    `ISized`. Without this overload a tuple would bind
    `bool` and fall through to the truthy default.
    This mirrors `Len(ITuple)` and answers from the tuple's arity.

### `bool(obj: object | None) -> bool`

Convert an arbitrary object to bool using Python's truth testing protocol.
Checks __bool__ (IBoolConvertible), then __len__ (ISized), then collection emptiness.
Non-None objects without these protocols are truthy.

**Parameters:**

- `obj` (object | None) -- The object to test for truthiness

**Returns:** The truth value of the object

```python
bool(0)        # False
bool(1)        # True
bool("")       # False
bool("hello")  # True
bool([])       # False
bool([1, 2])   # True
bool(None)     # False
```

### `breakpoint()`

Drop into the debugger. No-op when no debugger is attached.

!!! note
    Maps to `System.Diagnostics.Debugger.Break`.
    When no debugger is attached, this method does nothing.

### `bytes() -> bytes`

Construct an empty bytes object.

### `bytes(size: int) -> bytes`

Construct a bytes object of the given size, filled with zero bytes.

### `bytes(source: IEnumerable[int]) -> bytes`

Construct a bytes object from an iterable of ints.

### `chr(i: int) -> str`

Return a string of one character whose Unicode code point is the integer i.
This is the inverse of ord().

**Parameters:**

- `i` (int) -- A Unicode code point (0 to 0x10FFFF)

**Returns:** A string of one character

```python
chr(65)     # "A"
chr(8364)   # "€"
chr(97)     # "a"
```

**Raises:**

- `ValueError` -- Thrown when i is out of range

### `decimal() -> decimal`

Construct zero, matching CPython's `Decimal()`.

**Returns:** 0

### `decimal(m: decimal) -> decimal`

Convert a decimal to decimal (identity).

**Parameters:**

- `m` (decimal) -- The decimal value

**Returns:** The same decimal value

### `decimal(b: bool) -> decimal`

Convert a bool to decimal. True is 1, False is 0.

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** 1 for True, 0 for False

### `decimal(i: int) -> decimal`

Convert an int to decimal.

**Parameters:**

- `i` (int) -- The int value

**Returns:** The value as a decimal

### `decimal(l: int64) -> decimal`

Convert a long to decimal.

**Parameters:**

- `l` (int64) -- The long value

**Returns:** The value as a decimal

### `decimal(f: float32) -> decimal`

Convert a float to decimal.

**Parameters:**

- `f` (float32) -- The float value

**Returns:** The value as a decimal

**Raises:**

- `OverflowError` -- Value is out of range for decimal

### `decimal(d: float) -> decimal`

Convert a double to decimal.

**Parameters:**

- `d` (float) -- The double value

**Returns:** The value as a decimal

**Raises:**

- `OverflowError` -- Value is out of range for decimal

### `decimal(s: str) -> decimal`

Parse a string as a decimal.

**Parameters:**

- `s` (str) -- The string to parse

**Returns:** The parsed decimal

**Raises:**

- `ValueError` -- The string is not a valid decimal literal

### `decimal_floor_div(x: decimal, y: decimal) -> decimal`

Returns the truncated quotient of *x* divided by
*y*, matching CPython's `Decimal.__floordiv__`.

**Parameters:**

- `x` (decimal) -- The dividend
- `y` (decimal) -- The divisor

**Returns:** The quotient truncated toward zero

```python
DecimalFloorDiv(7m, 3m)    // 2
DecimalFloorDiv(-7m, 3m)   // -2  (truncated; int -7 // 3 is -3)
DecimalFloorDiv(7m, -3m)   // -2
DecimalFloorDiv(-7m, -3m)  // 2
```

!!! note
    Decimal `//` deliberately does NOT floor: the quotient truncates toward zero,
    so `Decimal(-7) // Decimal(3)` is `-2` where int `-7 // 3` is
    `-3`. That is both the spec's native-decimal policy and CPython's own decimal
    behavior (#1174), which is why this is not an overload of
    `floor_div` — it computes a different function.
    
    
    The zero guard lives here, not in the emitted C#, so the `//` lowering splices
    each operand expression exactly once and a side-effecting divisor runs once
    (#1216) — the same reason `floor_div` and
    `floor_mod` own their guards.
    
    
    The emitter previously used `Decimal.Divide` rather than `/` because a
    literal zero divisor (`7m // 0m`) through `/` is a compile-time C# error
    (CS0020, "division by constant zero") even in the unreachable arm of a
    guarding ternary. That workaround does not apply inside this helper — *x* and *y* are runtime parameters, never constants — so
    plain `/` is used here and the workaround must not be restored.

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `decimal_mod(x: decimal, y: decimal) -> decimal`

Returns the native truncating remainder of *x* divided by
*y*, matching CPython's `Decimal.__mod__`.

**Parameters:**

- `x` (decimal) -- The dividend
- `y` (decimal) -- The divisor

**Returns:** The truncating remainder (sign of the dividend)

```python
DecimalMod(7m, 3m)    // 1
DecimalMod(-7m, 3m)   // -1  (sign of dividend; int -7 % 3 is 2)
DecimalMod(7m, -3m)   // 1
DecimalMod(-7m, -3m)  // -1
```

!!! note
    The result takes the sign of the DIVIDEND, so `Decimal(-7) % Decimal(3)` is
    `-1` where int `-7 % 3` is `2`. Decimal sits outside the floored-`%`
    allowlist by design (#1153, #1189), which is why this is not an overload of
    `floor_mod` — it computes a different function.
    
    
    A zero divisor raises `InvalidOperation`, NOT
    `ZeroDivisionError`: CPython raises `decimal.InvalidOperation`
    here, a sibling of `ZeroDivisionError` rather than a subclass — unlike decimal
    `//`, whose `decimal.DivisionByZero` IS a `ZeroDivisionError`. The
    asymmetry with `decimal_floor_div` is deliberate; do not unify them.
    
    
    The zero guard lives here, not in the emitted C#, so the `%` lowering splices
    each operand expression exactly once and a side-effecting divisor runs once
    (#1216) — the same reason `floor_mod` owns its guard.
    
    
    The emitter previously used `Decimal.Remainder` rather than `%` because a
    literal zero divisor (`7m % 0m`) through `%` is a compile-time C# error
    (CS0020, "division by constant zero") even in the unreachable arm of a
    guarding ternary. That workaround does not apply inside this helper — *x* and *y* are runtime parameters, never constants — so
    plain `%` is used here and the workaround must not be restored. It is the same
    operation either way: `decimal.op_Modulus` invokes `Decimal.Remainder`.

**Raises:**

- `InvalidOperation` -- Thrown when *y* is zero

### `divmod(x: int, y: int) -> tuple[int, int]`

Return the quotient and remainder of dividing x by y.
Uses Python's floored division semantics where the remainder has the same sign as the divisor.

**Parameters:**

- `x` (int) -- The dividend
- `y` (int) -- The divisor

**Returns:** A tuple of (quotient, remainder)

```python
divmod(7, 2)     # (3, 1)
divmod(-7, 2)    # (-4, 1)
divmod(10, 3)    # (3, 1)
```

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `divmod(x: int64, y: int64) -> tuple[int64, int64]`

Return the quotient and remainder of dividing x by y.
Uses Python's floored division semantics where the remainder has the same sign as the divisor.

**Parameters:**

- `x` (int64) -- The dividend
- `y` (int64) -- The divisor

**Returns:** A tuple of (quotient, remainder)

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `divmod(x: uint64, y: uint64) -> tuple[uint64, uint64]`

Return the quotient and remainder of dividing two `ulong` operands.
Both operands are non-negative, so floored and truncating division coincide; the
overload exists so `divmod(uint64, uint64)` resolves instead of being refused
(SPY0354) or widened to `double` — the same reason
`floor_div` and `floor_mod` exist (#1662).

**Parameters:**

- `x` (uint64) -- The dividend
- `y` (uint64) -- The divisor

**Returns:** A tuple of (quotient, remainder)

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `divmod(x: float, y: float) -> tuple[float, float]`

Return the quotient and remainder of dividing x by y.
Uses Python's floored division semantics where the remainder has the same sign as the divisor.

**Parameters:**

- `x` (float) -- The dividend
- `y` (float) -- The divisor

**Returns:** A tuple of (quotient, remainder)

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `divmod(x: float32, y: float32) -> tuple[float32, float32]`

Return the quotient and remainder of dividing x by y.
Uses Python's floored division semantics where the remainder has the same sign as the divisor.

**Parameters:**

- `x` (float32) -- The dividend
- `y` (float32) -- The divisor

**Returns:** A tuple of (quotient, remainder)

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `divmod(x: decimal, y: decimal) -> tuple[decimal, decimal]`

Return the quotient and remainder of dividing x by y, using
truncating division semantics where the remainder has the same sign as the
dividend.

**Parameters:**

- `x` (decimal) -- The dividend
- `y` (decimal) -- The divisor

**Returns:** A tuple of (quotient, remainder)

```python
divmod(7m, 3m)     # (2, 1)
divmod(-7m, 3m)    # (-2, -1)   -- int divmod(-7, 3) is (-3, 2)
divmod(7m, -3m)    # (-2, 1)
divmod(-7m, -3m)   # (2, -1)
```

!!! note
    This overload deliberately differs from every sibling above, which are floored.
    It is not an inconsistency to tidy up. CPython's `Decimal.__divmod__` truncates —
    `divmod(Decimal(-7), Decimal(3))` is `(-2, -1)`, not the `(-3, 2)` that
    `divmod(-7, 3)` gives — so matching the int/float siblings here would break parity
    rather than restore it. Sharpy's floored `//`/`%` resolution (#1153) is scoped to
    int/long/float operands; decimal `//` (#1174) and `%` (#1189) are already native
    and truncating, and this overload agrees with them.
    Zero divisor raises `InvalidOperation`, not
    `ZeroDivisionError` — also deliberate, also CPython. In CPython
    `divmod(Decimal(7), Decimal(0))` and `Decimal(7) % Decimal(0)` both raise
    `InvalidOperation` while `Decimal(7) // Decimal(0)` raises `DivisionByZero`
    (a `ZeroDivisionError` subclass). `divmod` follows the `%` side even though
    its quotient half alone would follow the other. The two must not be unified.
    The divmod identity `x == q * y + r` holds for all four sign combinations.

**Raises:**

- `InvalidOperation` -- Thrown when *y* is zero

### `double(b: bool) -> float`

Convert bool to double. True becomes 1.0, False becomes 0.0.

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** 1.0 for True, 0.0 for False

### `double(i: int) -> float`

Convert int to double

### `double(l: int64) -> float`

Convert long to double

### `double(f: float32) -> float`

Convert float to double

### `double(d: float) -> float`

Convert double to double (identity)

### `double(m: decimal) -> float`

Convert decimal to double

### `double(s: str) -> float`

Parse string to double

### `double(b: uint8) -> float`

Convert byte to double

### `double(sb: int8) -> float`

Convert sbyte to double

### `double(s: int16) -> float`

Convert short to double

### `double(us: uint16) -> float`

Convert ushort to double

### `double(u: uint32) -> float`

Convert uint to double

### `double(ul: uint64) -> float`

Convert ulong to double

### `enumerate(iterable: IEnumerable[T], start: int = 0) -> EnumerateIterator[T]`

Return an enumerate object. The iterable must be a sequence, an iterator,
or some other object which supports iteration. The elements produced by
enumerate are tuples containing a count (from start which defaults to 0)
and the values obtained from iterating over iterable.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to enumerate
- `start` (int) -- The starting index (default 0)

**Returns:** An enumerate iterator

```python
for i, val in enumerate(["a", "b", "c"]):
    print(i, val)
# 0 a
# 1 b
# 2 c
```

### `filter(predicate: (T) -> bool, iterable: IEnumerable[T]) -> FilterIterator[T]`

Construct an iterator from those elements of iterable for which predicate is True.
If predicate is None, return the elements that are True.

**Parameters:**

- `predicate` ((T) -> bool) -- The predicate function to test each element
- `iterable` (IEnumerable[T]) -- The iterable to filter

**Returns:** A filter iterator

```python
list(filter(lambda x: x > 0, [-1, 0, 1, 2]))    # [1, 2]
list(filter(lambda s: len(s) > 3, ["hi", "hello"]))  # ["hello"]
```

### `float(b: bool) -> float`

Convert a bool to float. True becomes 1.0, False becomes 0.0.

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** 1.0 for True, 0.0 for False

### `float(i: int) -> float`

Convert an int to float.

**Parameters:**

- `i` (int) -- The int value

**Returns:** The value as a double

### `float(l: int64) -> float`

Convert a long to float.

**Parameters:**

- `l` (int64) -- The long value

**Returns:** The value as a double

### `float(f: float32) -> float`

Convert a float to double (widening).

**Parameters:**

- `f` (float32) -- The float value

**Returns:** The value as a double

### `float(d: float) -> float`

Convert a double to float (identity, since Python float maps to .NET double).

**Parameters:**

- `d` (float) -- The double value

**Returns:** The same double value

### `float(m: decimal) -> float`

Convert a decimal to float.

**Parameters:**

- `m` (decimal) -- The decimal value

**Returns:** The value as a double

### `float(s: str) -> float`

Parse a string to float.

**Parameters:**

- `s` (str) -- The string to parse

**Returns:** The parsed double value

```python
float("3.14")    # 3.14
float("42")      # 42.0
float("-1.5")    # -1.5
```

**Raises:**

- `ValueError` -- Thrown when the string cannot be parsed

### `float32(b: bool) -> float32`

Convert bool to float32. True becomes 1.0f, False becomes 0.0f.

### `float32(i: int) -> float32`

Convert int to float32.

### `float32(l: int64) -> float32`

Convert long to float32.

### `float32(f: float32) -> float32`

Convert float to float32 (identity).

### `float32(d: float) -> float32`

Convert double to float32 (narrowing). Overflow produces Infinity.

### `float32(m: decimal) -> float32`

Convert decimal to float32.

### `float32(s: str) -> float32`

Parse string to float32. Overflow produces Infinity, matching Python semantics.

### `float32(b: uint8) -> float32`

Convert byte to float32.

### `float32(sb: int8) -> float32`

Convert sbyte to float32.

### `float32(s: int16) -> float32`

Convert short to float32.

### `float32(us: uint16) -> float32`

Convert ushort to float32.

### `float32(u: uint32) -> float32`

Convert uint to float32.

### `float32(ul: uint64) -> float32`

Convert ulong to float32.

### `floor_div(x: float, y: float) -> float`

Returns the floored quotient of *x* divided by
*y*, matching CPython's `float_floor_div`.

**Parameters:**

- `x` (float) -- The dividend
- `y` (float) -- The divisor

**Returns:** The floored quotient

```python
FloorDiv(1.0, 0.1)   // 9.0  (not 10.0)
FloorDiv(7.5, 0.1)   // 74.0 (not 75.0)
FloorDiv(-1.0, 0.1)  // -10.0
```

!!! note
    `Math.Floor(x / y)` is not equivalent: `x / y` can round up
    across an integer boundary, so `1.0 // 0.1` would give `10.0` where
    CPython gives `9.0`. Deriving the quotient from the raw `fmod`
    remainder instead keeps the division exact.
    
    
    This is the quotient half of `divmod` — CPython
    implements `float_floor_div` by calling `float_divmod` and taking the
    first element — so the two share this one implementation and the divmod identity
    `x == (x // y) * y + (x % y)` established in #1153 holds for floats.

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `floor_div(x: int, y: int) -> int`

Returns the floored quotient of *x* divided by
*y*, computed entirely in integer arithmetic.

**Parameters:**

- `x` (int) -- The dividend
- `y` (int) -- The divisor

**Returns:** The floored quotient

```python
FloorDiv(7, 3)    // 2
FloorDiv(-7, 3)   // -3   (floored, not truncated)
FloorDiv(7, -3)   // -3
FloorDiv(-7, -3)  // 2
```

!!! note
    This is the quotient half of `divmod` and shares its algorithm,
    so the divmod identity `x == (x // y) * y + (x % y)` established in #1153 holds
    for integers against `floor_mod`.
    
    
    Integer arithmetic rather than `(int)Math.Floor((double)x / y)` (#1226): the double
    round-trip loses precision once the operands exceed 2^53, and it saturates instead of
    reporting at the `int.MinValue / -1` boundary. The zero guard lives HERE rather than
    in a caller-side ternary so the emitter splices each operand exactly once (#1216).
    
    
    `int.MinValue / -1` is decided, not inherited. The exact quotient (2147483648) does
    not fit `int`, and .NET raises `OverflowException` for it even in an unchecked
    context — division at MinValue by -1 is a hardware trap, unlike `*` and `+`,
    which wrap. So there is no "match the runtime wrap" option available. This raises
    `OverflowError`, matching `checked_int_pow`'s
    "diagnose, don't saturate" contract; the behavior it replaces returned
    `int.MaxValue`, a silently wrong value. CPython, whose integers are arbitrary
    precision, computes 2147483648 exactly.

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero
- `OverflowError` -- Thrown when the quotient does not fit an `int`, which happens only for
`int.MinValue / -1`.

### `floor_div(x: int64, y: int64) -> int64`

Returns the floored quotient of *x* divided by
*y*, computed entirely in integer arithmetic.
See the `floor_div` overload for the full contract.

**Parameters:**

- `x` (int64) -- The dividend
- `y` (int64) -- The divisor

**Returns:** The floored quotient

!!! note
    Exact across the whole `long` range — the `(long)Math.Floor((double)x / y)`
    form it replaces went through a double and so was wrong above 2^53 (#1226).

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero
- `OverflowError` -- Thrown when the quotient does not fit a `int64`, which happens only for
`long.MinValue / -1`.

### `floor_div(x: float32, y: float32) -> float32`

Returns the floored quotient of *x* divided by
*y*, matching CPython's `float_floor_div`.
See the `floor_div` overload.

**Parameters:**

- `x` (float32) -- The dividend
- `y` (float32) -- The divisor

**Returns:** The floored quotient

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `floor_div(x: uint64, y: uint64) -> uint64`

Returns the floored quotient of two `ulong` operands.
Both operands are non-negative, so floored division is identical to
truncating division. The overload exists so C# overload resolution
selects it instead of widening to `double` (#1662).

### `floor_mod(x: int, y: int) -> int`

Returns the remainder of Python's floored division of *x* by
*y*. The result takes the sign of the divisor (matching Python's
`%`), unlike C#'s native `%` which takes the sign of the dividend.
This keeps the divmod identity `x == (x // y) * y + FloorMod(x, y)` coherent.

**Parameters:**

- `x` (int) -- The dividend
- `y` (int) -- The divisor

**Returns:** The floored-division remainder (sign of the divisor)

```python
FloorMod(-7, 3)   // 2  (not -1)
FloorMod(7, -3)   // -2
FloorMod(-7, -3)  // -1
```

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `floor_mod(x: int64, y: int64) -> int64`

Returns the remainder of Python's floored division of *x* by
*y*. The result takes the sign of the divisor.

**Parameters:**

- `x` (int64) -- The dividend
- `y` (int64) -- The divisor

**Returns:** The floored-division remainder (sign of the divisor)

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `floor_mod(x: float, y: float) -> float`

Returns the remainder of Python's floored division of *x* by
*y*. The result takes the sign of the divisor.

**Parameters:**

- `x` (float) -- The dividend
- `y` (float) -- The divisor

**Returns:** The floored-division remainder (sign of the divisor)

!!! note
    A zero remainder carries the divisor's sign, matching CPython's `float_mod`
    (`-1.0 % 1.0` is `0.0`, `1.0 % -1.0` is `-0.0`). C#'s `%`
    gives zero the dividend's sign instead, which is observable in printed output and
    in downstream `copysign`/`atan2` use.

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `floor_mod(x: float32, y: float32) -> float32`

Returns the remainder of Python's floored division of *x* by
*y*. The result takes the sign of the divisor.

**Parameters:**

- `x` (float32) -- The dividend
- `y` (float32) -- The divisor

**Returns:** The floored-division remainder (sign of the divisor)

!!! note
    A zero remainder carries the divisor's sign, matching CPython's `float_mod`.
    See the `floor_mod` overload.

**Raises:**

- `ZeroDivisionError` -- Thrown when *y* is zero

### `floor_mod(x: uint64, y: uint64) -> uint64`

Returns the floored remainder of two `ulong` operands.
Both operands are non-negative, so the floored remainder is identical
to the truncating remainder. The overload exists so C# overload
resolution selects it instead of widening to `double` (#1662).

### `format(value: object | None, format_spec: str = "") -> str`

Convert a value to a "formatted" representation, as controlled by format_spec.
The interpretation of format_spec will depend on the type of the value argument.

**Parameters:**

- `value` (object | None) -- The value to format
- `format_spec` (str) -- The format specification string (default is empty string)

**Returns:** The formatted string representation

```python
format(42, "d")        # "42"
format(3.14, ".1f")    # "3.1"
format(255, "x")       # "ff"
```

### `hash(obj: object) -> int`

Return the hash value of an object.
Calls `__hash__` on the given object.

**Parameters:**

- `obj` (object) -- The object to hash

**Returns:** The hash value as an integer

```python
hash("hello")    # integer hash value
hash(42)         # 42
```

**Raises:**

- `TypeError` -- Thrown when *obj* is null

### `hex(x: int) -> str`

Return a lowercase hexadecimal string prefixed with "0x".

**Parameters:**

- `x` (int) -- The integer to convert

**Returns:** A hexadecimal string representation

```python
hex(255)    # "0xff"
hex(-42)    # "-0x2a"
hex(0)      # "0x0"
```

### `hex(x: int64) -> str`

Return a lowercase hexadecimal string prefixed with "0x" for long integers.

**Parameters:**

- `x` (int64) -- The long integer to convert

**Returns:** A hexadecimal string representation

### `id(obj: object) -> int`

Return the identity of an object.
This is an integer which is guaranteed to be unique and constant
for this object during its lifetime.
Maps to RuntimeHelpers.GetHashCode() which returns the sync block index.

**Parameters:**

- `obj` (object) -- The object to get the identity of

**Returns:** An integer uniquely identifying the object during its lifetime

```python
x = [1, 2, 3]
id(x)    # unique integer identity
```

**Raises:**

- `TypeError` -- Thrown when *obj* is null

### `input() -> str`

Read a line from standard input.

**Returns:** The input string (without trailing newline)

```python
name = input("Enter your name: ")
print("Hello, " + name)
```

### `input(prompt: str) -> str`

Read a line from standard input after printing a prompt.

**Parameters:**

- `prompt` (str) -- The prompt to display

**Returns:** The input string (without trailing newline)

### `int(b: bool) -> int`

Convert bool to int. True becomes 1, False becomes 0.

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** 1 for True, 0 for False

```python
int(True)      # 1
int(False)     # 0
int(3.9)       # 3 (truncates)
int("42")      # 42
```

### `int(i: int) -> int`

Convert int to int (identity)

### `int(l: int64) -> int`

Convert long to int

### `int(f: float32) -> int`

Convert float to int (truncates)

### `int(d: float) -> int`

Convert double to int (truncates)

### `int(m: decimal) -> int`

Convert decimal to int (truncates)

### `int(s: str) -> int`

Parse string to int

### `int(b: uint8) -> int`

Convert byte to int

### `int(sb: int8) -> int`

Convert sbyte to int

### `int(s: int16) -> int`

Convert short to int

### `int(us: uint16) -> int`

Convert ushort to int

### `int(u: uint32) -> int`

Convert uint to int

### `int(ul: uint64) -> int`

Convert ulong to int

### `int16(b: bool) -> int16`

Convert bool to int16. True becomes 1, False becomes 0.

### `int16(i: int) -> int16`

Convert int to int16.

### `int16(l: int64) -> int16`

Convert long to int16.

### `int16(f: float32) -> int16`

Convert float to int16 (truncates toward zero).

### `int16(d: float) -> int16`

Convert double to int16 (truncates toward zero).

### `int16(m: decimal) -> int16`

Convert decimal to int16 (truncates toward zero).

### `int16(s: str) -> int16`

Parse string to int16.

### `int16(s: str, base: int) -> int16`

Parse string to int16 with explicit base.

### `int16(b: uint8) -> int16`

Convert byte to int16 (widening).

### `int16(sb: int8) -> int16`

Convert sbyte to int16 (widening).

### `int16(s: int16) -> int16`

Convert short to int16 (identity).

### `int16(us: uint16) -> int16`

Convert ushort to int16.

### `int16(u: uint32) -> int16`

Convert uint to int16.

### `int16(ul: uint64) -> int16`

Convert ulong to int16.

### `int8(b: bool) -> int8`

Convert bool to int8. True becomes 1, False becomes 0.

### `int8(i: int) -> int8`

Convert int to int8.

### `int8(l: int64) -> int8`

Convert long to int8.

### `int8(f: float32) -> int8`

Convert float to int8 (truncates toward zero).

### `int8(d: float) -> int8`

Convert double to int8 (truncates toward zero).

### `int8(m: decimal) -> int8`

Convert decimal to int8 (truncates toward zero).

### `int8(s: str) -> int8`

Parse string to int8.

### `int8(s: str, base: int) -> int8`

Parse string to int8 with explicit base (2, 8, 10, or 16).

### `int8(b: uint8) -> int8`

Convert byte to int8.

### `int8(sb: int8) -> int8`

Convert sbyte to int8 (identity).

### `int8(s: int16) -> int8`

Convert short to int8.

### `int8(us: uint16) -> int8`

Convert ushort to int8.

### `int8(u: uint32) -> int8`

Convert uint to int8.

### `int8(ul: uint64) -> int8`

Convert ulong to int8.

### `isinstance(obj: object | None) -> bool`

Return True if the object argument is an instance of the classinfo argument.

**Parameters:**

- `obj` (object | None) -- The object to check

**Returns:** True if obj is an instance of T, False otherwise

```python
isinstance(42, int)           # True
isinstance("hello", str)      # True
isinstance(42, str)           # False
```

### `isinstance(obj: object | None, class_info: Type) -> bool`

Return True if the object argument is an instance of the classinfo argument.
This overload accepts the type as a parameter for runtime type checking.

**Parameters:**

- `obj` (object | None) -- The object to check
- `class_info` (Type) -- The type to check against

**Returns:** True if obj is an instance of classInfo, False otherwise

### `isinstance(obj: object | None, *class_info: Type) -> bool`

Return True if the object argument is an instance of any of the types in classInfo.

**Parameters:**

- `obj` (object | None) -- The object to check
- `class_info` (*Type) -- A tuple of types to check against

**Returns:** True if obj is an instance of any type in classInfo, False otherwise

### `issubclass(cls: Type, class_info: Type) -> bool`

Return True if class is a subclass of classinfo. A class is considered
a subclass of itself.

**Parameters:**

- `cls` (Type) -- The class to check
- `class_info` (Type) -- The base class to check against

**Returns:** True if cls is a subclass of classInfo, False otherwise

```python
issubclass(bool, int)    # True
issubclass(int, str)     # False
```

### `issubclass(cls: Type, *class_info: Type) -> bool`

Return True if class is a subclass of any of the types in classInfo.

**Parameters:**

- `cls` (Type) -- The class to check
- `class_info` (*Type) -- A tuple of types to check against

**Returns:** True if cls is a subclass of any type in classInfo, False otherwise

### `iter(enumerable: IEnumerable[T]) -> Iterator[T]`

Return an iterator object from any C# enumerable.

**Parameters:**

- `enumerable` (IEnumerable[T]) -- The C# enumerable to get an iterator from.

**Returns:** An iterator for the enumerable.

```python
it = iter([1, 2, 3])
next(it)    # 1
next(it)    # 2
```

!!! note
    Wraps the enumerator using EnumeratorIterator.
    This allows any C# IEnumerable to work seamlessly with Sharpy's iterator protocol.

**Raises:**

- `TypeError` -- Thrown when enumerable is null.

### `len(c: system.collections.ICollection) -> int`

Return the length (the number of items) of a collection.

```python
len([1, 2, 3])    # 3
len("hello")      # 5
len({})           # 0
```

!!! note
    Uses the non-generic `ICollection` interface
    which is implemented by arrays, List{T}, Dictionary{K,V}, etc.
    This avoids overload ambiguity when a type implements both
    `ICollection[T]` and `IReadOnlyCollection[T]`.

**Raises:**

- `TypeError` -- Thrown when *c* is null

### `len(sized: ISized) -> int`

Return the length of an ISized type (user-defined types with __len__).

**Parameters:**

- `sized` (ISized) -- An object implementing `ISized`

**Returns:** The number of elements

**Raises:**

- `TypeError` -- Thrown when *sized* is null

### `len(list: list[T]) -> int`

Return the length of a Sharpy list.

!!! note
    This concrete overload disambiguates between the
    `ICollection` and `ISized`
    overloads, both of which `list[T]` now satisfies (it
    implements the non-generic `IList`).
    An identity conversion to the concrete parameter type is preferred
    over the interface conversions, so this overload wins.

### `len(dict: dict[K, V]) -> int`

Return the length of a Sharpy dictionary.

!!! note
    This concrete overload disambiguates between the
    `ICollection` and `ISized`
    overloads, both of which `dict[K, V]` now satisfies (it
    implements the non-generic `IDictionary`).

### `len(s: str) -> int`

Return the length of a string.

### `len(tuple: system.runtime.compiler_services.ITuple) -> int`

Return the number of elements in a tuple.

!!! note
    Tuples are emitted as `System.ValueTuple` instances, which
    implement `System.Runtime.CompilerServices.ITuple` but
    neither `ICollection` nor
    `ISized`. This overload routes `len(tuple)` to
    `System.Runtime.CompilerServices.ITuple.Length`.

### `list(enumerable: IEnumerable[T]) -> list[T]`

Convert IEnumerable to list

### `list() -> list[T]`

Create empty list

### `list(other: list[T]) -> list[T]`

Convert list to list (copy)

### `list_from_str(s: str) -> list[str]`

Builds a list of single-character strings from a string, matching Python's
`list("abc")` -> `['a', 'b', 'c']` and `list("")` -> `[]`.
Iterates by UTF-16 code unit (Axiom 1), consistent with
`iterate`: `list("abc")` selects this overload because
C# would otherwise bind `list(string)` to `List<char>`
(`string` is `IEnumerable<char>`), diverging from Python (#1067).

### `long(b: bool) -> int64`

Convert bool to long. True becomes 1, False becomes 0.

### `long(i: int) -> int64`

Convert int to long (widening)

### `long(l: int64) -> int64`

Convert long to long (identity)

### `long(f: float32) -> int64`

Convert float to long (truncates)

### `long(d: float) -> int64`

Convert double to long (truncates)

### `long(m: decimal) -> int64`

Convert decimal to long (truncates)

### `long(s: str) -> int64`

Parse string to long

### `long(b: uint8) -> int64`

Convert byte to long

### `long(sb: int8) -> int64`

Convert sbyte to long

### `long(s: int16) -> int64`

Convert short to long

### `long(us: uint16) -> int64`

Convert ushort to long

### `long(u: uint32) -> int64`

Convert uint to long

### `long(ul: uint64) -> int64`

Convert ulong to long

### `map(function: (TIn) -> TOut, iterable: IEnumerable[TIn]) -> MapIterator[TIn, TOut]`

Return an iterator that applies function to every item of iterable, yielding the results.

**Parameters:**

- `function` ((TIn) -> TOut) -- The function to apply to each element
- `iterable` (IEnumerable[TIn]) -- The iterable to map over

**Returns:** A map iterator

```python
list(map(lambda x: x * 2, [1, 2, 3]))    # [2, 4, 6]
list(map(str, [1, 2, 3]))                 # ["1", "2", "3"]
```

### `map(function: (T1, T2) -> TOut, iterable1: IEnumerable[T1], iterable2: IEnumerable[T2], strict: bool = False) -> MapIterator[T1, T2, TOut]`

Return an iterator that applies a two-argument function to corresponding items of two
iterables. With *strict* True, raises ValueError if the iterables have
different lengths (Python 3.14 behaviour); otherwise stops at the shortest.

```python
list(map(lambda a, b: a + b, [1, 2], [10, 20]))             # [11, 22]
list(map(lambda a, b: a + b, [1, 2], [10], strict=True))    # ValueError
```

### `map(function: (T1, T2, T3) -> TOut, iterable1: IEnumerable[T1], iterable2: IEnumerable[T2], iterable3: IEnumerable[T3], strict: bool = False) -> MapIterator[T1, T2, T3, TOut]`

Return an iterator that applies a three-argument function to corresponding items of three
iterables. With *strict* True, raises ValueError if the iterables have
different lengths; otherwise stops at the shortest.

### `max(iterable: IEnumerable[T]) -> T`

Return the largest item in an iterable.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to search

**Returns:** The largest item

```python
max([1, 5, 3])       # 5
max("abc")           # "c"
```

**Raises:**

- `ValueError` -- Thrown when the iterable is empty

### `max(iterable: IEnumerable[T], key: (T) -> TKey) -> T`

Return the largest item in an iterable, using a key function for comparison.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to search
- `key` ((T) -> TKey) -- A function to extract a comparison key from each element

**Returns:** The largest item according to the key function

**Raises:**

- `ValueError` -- Thrown when the iterable is empty

### `max(iterable: IEnumerable[T], default: T) -> T`

Return the largest item in an iterable, or default if the iterable is empty.

### `max(iterable: IEnumerable[T], key: (T) -> TKey, default: T) -> T`

Return the largest item in an iterable using a key function,
or default if the iterable is empty.

### `max(first: T, second: T, *rest: T) -> T`

Return the largest of two or more values (the variadic value form).

**Parameters:**

- `first` (T) -- The first value
- `second` (T) -- The second value
- `rest` (*T) -- Any additional values

**Returns:** The largest value (the first encountered on ties, matching Python)

```python
max(2, 3, 1)     # 3
max(5, 2, 8, 1)  # 8
```

!!! note
    The `key=` form of this variadic value call (e.g. `max(a, b, key=f)`) is
    supported: the compiler lowers it to the iterable+key overload
    `Max<T, TKey>(IEnumerable<T>, Func<T, TKey>)` by wrapping the
    positional values in an array, because a C# `params` parameter must come last and
    cannot coexist with a by-keyword `key` (#1012).

### `min(iterable: IEnumerable[T]) -> T`

Return the smallest item in an iterable.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to search

**Returns:** The smallest item

```python
min([1, 5, 3])       # 1
min("abc")           # "a"
```

**Raises:**

- `ValueError` -- Thrown when the iterable is empty

### `min(iterable: IEnumerable[T], key: (T) -> TKey) -> T`

Return the smallest item in an iterable, using a key function for comparison.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to search
- `key` ((T) -> TKey) -- A function to extract a comparison key from each element

**Returns:** The smallest item according to the key function

**Raises:**

- `ValueError` -- Thrown when the iterable is empty

### `min(iterable: IEnumerable[T], default: T) -> T`

Return the smallest item in an iterable, or default if the iterable is empty.

### `min(iterable: IEnumerable[T], key: (T) -> TKey, default: T) -> T`

Return the smallest item in an iterable using a key function,
or default if the iterable is empty.

### `min(first: T, second: T, *rest: T) -> T`

Return the smallest of two or more values (the variadic value form).

**Parameters:**

- `first` (T) -- The first value
- `second` (T) -- The second value
- `rest` (*T) -- Any additional values

**Returns:** The smallest value (the first encountered on ties, matching Python)

```python
min(2, 3)        # 2
min(5, 2, 8, 1)  # 1
```

!!! note
    The `key=` form of this variadic value call (e.g. `min(a, b, key=f)`) is
    supported: the compiler lowers it to the iterable+key overload
    `Min<T, TKey>(IEnumerable<T>, Func<T, TKey>)` by wrapping the
    positional values in an array, because a C# `params` parameter must come last and
    cannot coexist with a by-keyword `key` (#1012).

### `next(iterator: Iterator[T]) -> T`

Retrieve the next item from the iterator by calling its Next() method.
If the iterator is exhausted, a StopIteration exception is raised.

**Parameters:**

- `iterator` (Iterator[T]) -- The iterator to advance

**Returns:** The next item from the iterator

```python
it = iter([1, 2, 3])
next(it)    # 1
next(it)    # 2
next(it)    # 3
```

**Raises:**

- `StopIteration` -- Thrown when the iterator is exhausted

### `next(iterator: Iterator[T], default: T) -> T`

Retrieve the next item from the iterator, or return default if exhausted.

**Parameters:**

- `iterator` (Iterator[T]) -- The iterator to advance
- `default` (T) -- The value to return if the iterator is exhausted

**Returns:** The next item, or default if exhausted

```python
it = iter([1])
next(it)          # 1
next(it, "done")  # "done"
```

### `object() -> object`

Construct a bare object, matching CPython's `object()`, which takes no arguments.

**Returns:** A new object instance

!!! note
    This exists so a reference to the builtin type `object` has an overload set to pin
    against — the constructor-reference Conversion family's precondition (#1272).

### `oct(x: int) -> str`

Return an octal string prefixed with "0o".

**Parameters:**

- `x` (int) -- The integer to convert

**Returns:** An octal string representation

```python
oct(8)      # "0o10"
oct(-8)     # "-0o10"
oct(0)      # "0o0"
```

### `oct(x: int64) -> str`

Return an octal string prefixed with "0o" for long integers.

**Parameters:**

- `x` (int64) -- The long integer to convert

**Returns:** An octal string representation

### `open(path: str) -> TextFile`

Open a file and return a file object.

**Parameters:**

- `path` (str) -- Path to the file

**Returns:** A TextFile in read mode with UTF-8 encoding

```python
f = open("file.txt")
f = open("output.txt", "w")
f = open("data.txt", "r", "utf-8")
```

### `open(path: str, mode: str) -> TextFile`

Open a file and return a file object.

**Parameters:**

- `path` (str) -- Path to the file
- `mode` (str) -- File mode: "r" (read), "w" (write), "a" (append), "x" (exclusive create)

**Returns:** A TextFile with UTF-8 encoding

### `open(path: str, mode: str, encoding: str) -> TextFile`

Open a file and return a file object.

**Parameters:**

- `path` (str) -- Path to the file
- `mode` (str) -- File mode: "r" (read), "w" (write), "a" (append), "x" (exclusive create)
- `encoding` (str) -- Text encoding name (e.g., "utf-8", "ascii")

**Returns:** A TextFile with the specified mode and encoding

### `ord(s: str) -> int`

Return the Unicode code point for a one-character string.
This is the inverse of chr().

**Parameters:**

- `s` (str) -- A one-character string

**Returns:** The Unicode code point of the character

```python
ord("A")    # 65
ord("€")    # 8364
ord("a")    # 97
```

**Raises:**

- `TypeError` -- Thrown when the string is not exactly one character

### `pow(x: float, y: float) -> float`

Return x raised to the power y.

**Parameters:**

- `x` (float) -- The base
- `y` (float) -- The exponent

**Returns:** x raised to the power y

```python
pow(2, 3)      # 8.0
pow(4, 0.5)    # 2.0
pow(10, -1)    # 0.1
```

### `pow(x: int, y: int) -> float`

Return x raised to the power y.

**Parameters:**

- `x` (int) -- The base
- `y` (int) -- The exponent

**Returns:** x raised to the power y

### `pow(x: int64, y: int64) -> float`

Return x raised to the power y.

**Parameters:**

- `x` (int64) -- The base
- `y` (int64) -- The exponent

**Returns:** x raised to the power y

### `pow(x: float32, y: float32) -> float32`

Return x raised to the power y.

**Parameters:**

- `x` (float32) -- The base
- `y` (float32) -- The exponent

**Returns:** x raised to the power y

### `checked_int_pow(x: int, y: int) -> int`

Return x raised to the power y as an exact `int` using
checked exponentiation-by-squaring. Unlike `pow`,
this does not route through floating-point and therefore never silently
loses precision or saturates: an out-of-range result raises
`OverflowError`, matching Python's "diagnose, don't saturate"
semantics for fixed-width integers.

**Parameters:**

- `x` (int) -- The base.
- `y` (int) -- The exponent. A negative exponent is handled here rather than by the
caller (#1228) — see the remarks.

**Returns:** x raised to the power y.

!!! note
    A negative exponent returns the truncating double-path value, which is the spec's rule
    for `int ** int`: `2 ** -1` is `0`, not `0.5`
    (arithmetic_operators.md, "Integer ** negative exponent"). This is a DELIBERATE
    divergence from CPython, where `2 ** -1` is the float `0.5`.
    
    
    Absorbing the case here rather than throwing is what lets the emitter emit ONE
    invocation splicing each operand once (#1228). Previously the emitter had to wrap the
    call in a negative-exponent ternary dispatching between this method and a double path,
    which regenerated both operands — and regeneration is not pure, since it can re-push
    hoisted statements that then run unconditionally.

**Raises:**

- `OverflowError` -- The result does not fit in an `int`.

### `checked_int_pow(x: int64, y: int64) -> int64`

Return x raised to the power y as an exact `int64` using
checked exponentiation-by-squaring. See `checked_int_pow`
for semantics; an out-of-range result raises `OverflowError`.

**Parameters:**

- `x` (int64) -- The base.
- `y` (int64) -- The exponent. A negative exponent returns the truncating double-path
value, as in `checked_int_pow` (#1228).

**Returns:** x raised to the power y.

**Raises:**

- `OverflowError` -- The result does not fit in a `int64`.

### `checked_int_pow(x: uint64, y: uint64) -> uint64`

Return x raised to the power y as an exact `uint64` using
checked exponentiation-by-squaring. See `checked_int_pow`
for semantics; an out-of-range result raises `OverflowError`.

**Parameters:**

- `x` (uint64) -- The base.
- `y` (uint64) -- The exponent.

**Returns:** x raised to the power y.

**Raises:**

- `OverflowError` -- The result does not fit in a `uint64`.

### `checked_int_pow(x: uint64, y: int64) -> uint64`

Return x raised to the power y as an exact `uint64`. When y is negative,
the truncating double-path value is returned, as in
`checked_int_pow` (#1228). When y is non-negative, delegates to
`checked_int_pow`.

**Parameters:**

- `x` (uint64) -- The base.
- `y` (int64) -- The exponent. A negative exponent returns the truncating double-path
value.

**Returns:** x raised to the power y.

**Raises:**

- `OverflowError` -- The result does not fit in a `uint64`.

### `checked_int_pow(x: int64, y: uint64) -> int64`

Return x raised to the power y as an exact `int64` using
checked exponentiation-by-squaring. See `checked_int_pow`
for semantics; an out-of-range result raises `OverflowError`.
Handles negative bases correctly.

**Parameters:**

- `x` (int64) -- The base.
- `y` (uint64) -- The exponent.

**Returns:** x raised to the power y.

**Raises:**

- `OverflowError` -- The result does not fit in a `int64`.

### `contains(value: int) -> bool`

O(1) arithmetic membership test matching CPython's `range.__contains__` for
`int` needles. Returns `True` when *value* is
within the half-open interval and lies on a step boundary.

### `range(stop: int) -> RangeIterator`

Return an iterator that produces integers from 0 up to (but not including) stop.

**Parameters:**

- `stop` (int) -- The stopping value (exclusive)

**Returns:** A range iterator

```python
list(range(5))         # [0, 1, 2, 3, 4]
list(range(2, 5))      # [2, 3, 4]
list(range(0, 10, 2))  # [0, 2, 4, 6, 8]
```

### `range(start: int, stop: int) -> RangeIterator`

Return an iterator that produces integers from start up to (but not including) stop.

**Parameters:**

- `start` (int) -- The starting value
- `stop` (int) -- The stopping value (exclusive)

**Returns:** A range iterator

### `range(start: int, stop: int, step: int) -> RangeIterator`

Return an iterator that produces integers from start up to (but not including) stop,
incrementing by step.

**Parameters:**

- `start` (int) -- The starting value
- `stop` (int) -- The stopping value (exclusive)
- `step` (int) -- The step value

**Returns:** A range iterator

**Raises:**

- `ValueError` -- Thrown when *step* is zero

### `repr(obj: object | None) -> str`

Return a string containing a printable representation of an object.

**Parameters:**

- `obj` (object | None) -- The object to get the representation of

**Returns:** A printable string representation

```python
repr("hello")      # "'hello'"
repr([1, 2, 3])    # "[1, 2, 3]"
repr(None)         # "None"
```

!!! note
    Uses `__str__` to get the representation.
    Sharpy types (List, Set, Dict) override ToString() to produce
    Python-compatible repr output (e.g., "[1, 2, 3]", "{1, 2}", etc.).
    Strings are wrapped in single quotes, matching Python's repr().
    Floats are formatted by `format_float` so whole values
    keep their trailing `.0` (e.g., `-4.0`, not `-4`).

### `reversed(sequence: IEnumerable[T]) -> Iterator[T]`

Return a reverse iterator over the values of the given sequence.

**Parameters:**

- `sequence` (IEnumerable[T]) -- The sequence to reverse

**Returns:** An iterator that yields elements in reverse order

```python
list(reversed([1, 2, 3]))    # [3, 2, 1]
list(reversed("abc"))        # ["c", "b", "a"]
```

!!! note
    For `IList[T]` implementations, iterates backwards efficiently.
    For other sequences, materializes the sequence and reverses using LINQ.

**Raises:**

- `TypeError` -- Thrown when *sequence* is null

### `reversed(reversible: IReverseEnumerable[T]) -> Iterator[T]`

Return a reverse iterator for types that implement `IReverseEnumerable[T]`
but not `IEnumerable[T]` (i.e., types with __reversed__ but no __iter__).

### `round(x: float) -> int`

Round a number to the nearest integer.

**Parameters:**

- `x` (float) -- The number to round

**Returns:** The rounded value

```python
round(3.7)       # 4
round(2.5)       # 2 (banker's rounding)
round(3.14159, 2) # 3.14
```

!!! note
    Uses .NET's banker's rounding (round half to even). For example, Round(2.5) returns 2, not 3.

### `round(x: float, n: int) -> float`

Round a number to n decimal places.

**Parameters:**

- `x` (float) -- The number to round
- `n` (int) -- The number of decimal places

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `round(x: float32) -> int`

Round a float to the nearest integer.

**Parameters:**

- `x` (float32) -- The number to round

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `round(x: float32, n: int) -> float32`

Round a float to n decimal places.

**Parameters:**

- `x` (float32) -- The number to round
- `n` (int) -- The number of decimal places

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `round(x: decimal) -> int`

Round a decimal to the nearest integer.

**Parameters:**

- `x` (decimal) -- The number to round

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `round(x: decimal, n: int) -> decimal`

Round a decimal to n decimal places.

**Parameters:**

- `x` (decimal) -- The number to round
- `n` (int) -- The number of decimal places

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `set(enumerable: IEnumerable[T]) -> set[T]`

Convert IEnumerable to set

### `set() -> set[T]`

Create empty set

### `set(other: set[T]) -> set[T]`

Convert set to set (copy)

### `sorted(iterable: IEnumerable[T]) -> list[T]`

Return a new sorted list from the items in iterable.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to sort

**Returns:** A new sorted list

```python
sorted([3, 1, 2])              # [1, 2, 3]
sorted("cab")                  # ["a", "b", "c"]
sorted([3, 1, 2], reverse=True) # [3, 2, 1]
```

### `sorted(iterable: IEnumerable[T], key: (T) -> TKey) -> list[T]`

Return a new sorted list using a key function for comparison.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to sort
- `key` ((T) -> TKey) -- A function to extract a comparison key from each element

**Returns:** A new sorted list

### `sorted(iterable: IEnumerable[T], reverse: bool) -> list[T]`

Return a new sorted list, optionally in reverse order.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to sort
- `reverse` (bool) -- If True, sort in descending order

**Returns:** A new sorted list

### `sorted(iterable: IEnumerable[T], key: (T) -> TKey, reverse: bool) -> list[T]`

Return a new sorted list using a key function, optionally in reverse order.

**Parameters:**

- `iterable` (IEnumerable[T]) -- The iterable to sort
- `key` ((T) -> TKey) -- A function to extract a comparison key from each element
- `reverse` (bool) -- If True, sort in descending order

**Returns:** A new sorted list

### `str(x: object) -> str`

Convert an arbitrary object to its string representation.
Returns `"None"` for None, Python-style `"True"`/`"False"`
for booleans, and `__str__` for everything else.

**Parameters:**

- `x` (object) -- The object to convert

**Returns:** The string representation

```python
str(42)        # "42"
str(3.14)      # "3.14"
str(True)      # "True"
str(None)      # "None"
```

### `str(s: str) -> str`

Return the string unchanged.

### `str(c: char) -> str`

Convert a `char` to string without boxing.

### `str(i: int) -> str`

Convert an `int` to string without boxing.

### `str(l: int64) -> str`

Convert a `int64` to string without boxing.

### `str(l: uint64) -> str`

Convert a `uint64` to string without boxing. Without this overload C#
widened `uint64` to `double` and `str(uint64(7))` printed `7.0`
(the other unsigned widths widen to `int64` and were already exact).

### `str(d: float) -> str`

Convert a `float` to string without boxing.
Formats with Python-compatible trailing `.0` for whole numbers.

### `str(f: float32) -> str`

Convert a `float32` to string without boxing.
Formats with Python-compatible trailing `.0` for whole numbers.

### `format_float(value: float) -> str`

Format a floating-point value with Python-compatible representation.
NaN, Infinity, and -Infinity use Python's lowercase forms.
Whole-number values get a trailing `.0`.

!!! note
    This is the single authority for Python-style float formatting; every other
    float-rendering site delegates here (guarded by
    `FloatFormattingAuthorityTests`).
    
    The digits come from .NET's shortest-round-trip formatter (`"R"`), which is
    correct, but the positional-vs-exponential layout is Sharpy's own decision,
    ported from CPython's `format_float_short`: writing a value as
    `0.d1…dn × 10^decpt`, render positionally when `-4 < decpt <= 16`
    and exponentially otherwise. .NET stays positional one decade longer
    (`decpt <= 17`), which is the `[1e16, 1e17)` divergence band of #1204.
    
    
    Re-rendering from the digits, rather than patching the one divergent band, keeps
    the layout policy here instead of inheriting whatever a future runtime picks.

### `format_float(value: float32) -> str`

Format a `float32` value with Python-compatible representation.
Overload to avoid float→double widening precision issues.

!!! note
    Shares `render_shortest_round_trip` with `format_float`
    — the two overloads share a renderer, not a threshold.
    
    The single switches to exponential at `decpt > 9`, not the double's 16.
    That is a deliberate Sharpy decision rather than CPython parity, because CPython
    has no `float32` and therefore has no answer to copy. The derivation mirrors
    CPython's: its 16 tracks the ≤17 significant digits a double's shortest
    round-trip form can need, so positional layout never pads with digits the type
    does not carry; a single needs ≤9, so 9 is its analogue. Using the double's 16
    here would spread float32 output positionally across `[1e9, 1e16)` — e.g.
    `1.5e15f` would print as `1500000000000000.0`, sixteen digits for a
    type carrying about seven (Axiom 3).
    
    
    9 is also .NET's own single threshold, so float32 output is byte-identical to
    what it was before this renderer existed.

### `str(b: bool) -> str`

Convert a `bool` to string.
Returns Python-style `"True"` or `"False"`.

### `sum(iterable: IEnumerable[int]) -> int`

Sums a sequence of integers.

**Parameters:**

- `iterable` (IEnumerable[int]) -- The sequence to sum

**Returns:** The total sum

```python
sum([1, 2, 3])       # 6
sum(range(10))       # 45
sum([])              # 0
```

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[int64]) -> int64`

Sums a sequence of longs.

**Parameters:**

- `iterable` (IEnumerable[int64]) -- The sequence to sum

**Returns:** The total sum

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int64`

### `sum(iterable: IEnumerable[float32]) -> float32`

Sums a sequence of floats.

**Parameters:**

- `iterable` (IEnumerable[float32]) -- The sequence to sum

**Returns:** The total sum

**Raises:**

- `TypeError` -- Thrown when *iterable* is null

### `sum(iterable: IEnumerable[float]) -> float`

Sums a sequence of doubles.

**Parameters:**

- `iterable` (IEnumerable[float]) -- The sequence to sum

**Returns:** The total sum

**Raises:**

- `TypeError` -- Thrown when *iterable* is null

### `sum(iterable: IEnumerable[decimal]) -> decimal`

Sums a sequence of decimals.

**Parameters:**

- `iterable` (IEnumerable[decimal]) -- The sequence to sum

**Returns:** The total sum

**Raises:**

- `TypeError` -- Thrown when *iterable* is null

### `sum(iterable: IEnumerable[int], start: int) -> int`

Sums a sequence of integers with a start value.

**Parameters:**

- `iterable` (IEnumerable[int]) -- The sequence to sum
- `start` (int) -- The initial accumulator value

**Returns:** The total sum plus start

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[int64], start: int64) -> int64`

Sums a sequence of longs with a start value.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int64`

### `sum(iterable: IEnumerable[float32], start: float32) -> float32`

Sums a sequence of floats with a start value.

### `sum(iterable: IEnumerable[float], start: float) -> float`

Sums a sequence of doubles with a start value.

### `sum(iterable: IEnumerable[decimal], start: decimal) -> decimal`

Sums a sequence of decimals with a start value.

### `sum(iterable: IEnumerable[int8]) -> int`

Sums a sequence of signed bytes, accumulating into int.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[uint8]) -> int`

Sums a sequence of bytes, accumulating into int.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[int16]) -> int`

Sums a sequence of short integers, accumulating into int.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[uint16]) -> int`

Sums a sequence of unsigned short integers, accumulating into int.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[uint32]) -> uint32`

Sums a sequence of unsigned integers.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit a `uint32`

### `sum(iterable: IEnumerable[uint64]) -> uint64`

Sums a sequence of unsigned long integers.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit a `uint64`

### `sum(iterable: IEnumerable[int8], start: int) -> int`

Sums a sequence of signed bytes with a start value, accumulating into int.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[uint8], start: int) -> int`

Sums a sequence of bytes with a start value, accumulating into int.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[int16], start: int) -> int`

Sums a sequence of short integers with a start value, accumulating into int.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[uint16], start: int) -> int`

Sums a sequence of unsigned short integers with a start value, accumulating into int.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit an `int32`

### `sum(iterable: IEnumerable[uint32], start: uint32) -> uint32`

Sums a sequence of unsigned integers with a start value.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit a `uint32`

### `sum(iterable: IEnumerable[uint64], start: uint64) -> uint64`

Sums a sequence of unsigned long integers with a start value.

**Raises:**

- `TypeError` -- Thrown when *iterable* is null
- `OverflowError` -- Thrown when the sum does not fit a `uint64`

### `sum(iterable: IEnumerable[bool]) -> int`

Sums a sequence of booleans, counting `True` as 1.

### `sum(iterable: IEnumerable[bool], start: int) -> int`

Sums a sequence of booleans with an integer start value.

### `sum(iterable: IEnumerable[bool], start: float) -> float`

Sums a sequence of booleans with a double start value.

### `sum(iterable: IEnumerable[bool], start: decimal) -> decimal`

Sums a sequence of booleans with a decimal start value.

### `sum(iterable: IEnumerable[int8], start: float) -> float`

Sums a sequence of signed bytes with a double start value.

### `sum(iterable: IEnumerable[uint8], start: float) -> float`

Sums a sequence of bytes with a double start value.

### `sum(iterable: IEnumerable[int16], start: float) -> float`

Sums a sequence of short integers with a double start value.

### `sum(iterable: IEnumerable[uint16], start: float) -> float`

Sums a sequence of unsigned short integers with a double start value.

### `sum(iterable: IEnumerable[int], start: float) -> float`

Sums a sequence of integers with a double start value.

### `sum(iterable: IEnumerable[uint32], start: float) -> float`

Sums a sequence of unsigned integers with a double start value.

### `sum(iterable: IEnumerable[int64], start: float) -> float`

Sums a sequence of long integers with a double start value.

### `sum(iterable: IEnumerable[uint64], start: float) -> float`

Sums a sequence of unsigned long integers with a double start value.

### `sum(iterable: IEnumerable[float32], start: float) -> float`

Sums a sequence of floats with a double start value.

### `sum(iterable: IEnumerable[int8], start: decimal) -> decimal`

Sums a sequence of signed bytes with a decimal start value.

### `sum(iterable: IEnumerable[uint8], start: decimal) -> decimal`

Sums a sequence of bytes with a decimal start value.

### `sum(iterable: IEnumerable[int16], start: decimal) -> decimal`

Sums a sequence of short integers with a decimal start value.

### `sum(iterable: IEnumerable[uint16], start: decimal) -> decimal`

Sums a sequence of unsigned short integers with a decimal start value.

### `sum(iterable: IEnumerable[int], start: decimal) -> decimal`

Sums a sequence of integers with a decimal start value.

### `sum(iterable: IEnumerable[uint32], start: decimal) -> decimal`

Sums a sequence of unsigned integers with a decimal start value.

### `sum(iterable: IEnumerable[int64], start: decimal) -> decimal`

Sums a sequence of long integers with a decimal start value.

### `sum(iterable: IEnumerable[uint64], start: decimal) -> decimal`

Sums a sequence of unsigned long integers with a decimal start value.

### `tuple(enumerable: IEnumerable[object]) -> tuple[T1, T2]`

Convert IEnumerable to tuple (ValueTuple)

### `tuple(enumerable: IEnumerable[object]) -> tuple[T1, T2, T3]`

Convert IEnumerable to tuple (ValueTuple with 3 items)

### `type(obj: object | None) -> Type`

Return the type of an object.

**Parameters:**

- `obj` (object | None) -- The object to get the type of

**Returns:** The type of the object

```python
type(42)        # <class 'int'>
type("hello")   # <class 'str'>
type([1, 2])    # <class 'list'>
```

### `uint16(b: bool) -> uint16`

Convert bool to uint16. True becomes 1, False becomes 0.

### `uint16(i: int) -> uint16`

Convert int to uint16.

### `uint16(l: int64) -> uint16`

Convert long to uint16.

### `uint16(f: float32) -> uint16`

Convert float to uint16 (truncates toward zero).

### `uint16(d: float) -> uint16`

Convert double to uint16 (truncates toward zero).

### `uint16(m: decimal) -> uint16`

Convert decimal to uint16 (truncates toward zero).

### `uint16(s: str) -> uint16`

Parse string to uint16.

### `uint16(s: str, base: int) -> uint16`

Parse string to uint16 with explicit base.

### `uint16(b: uint8) -> uint16`

Convert byte to uint16 (widening).

### `uint16(sb: int8) -> uint16`

Convert sbyte to uint16.

### `uint16(s: int16) -> uint16`

Convert short to uint16.

### `uint16(us: uint16) -> uint16`

Convert ushort to uint16 (identity).

### `uint16(u: uint32) -> uint16`

Convert uint to uint16.

### `uint16(ul: uint64) -> uint16`

Convert ulong to uint16.

### `uint32(b: bool) -> uint32`

Convert bool to uint32. True becomes 1, False becomes 0.

### `uint32(i: int) -> uint32`

Convert int to uint32.

### `uint32(l: int64) -> uint32`

Convert long to uint32.

### `uint32(f: float32) -> uint32`

Convert float to uint32 (truncates toward zero).

### `uint32(d: float) -> uint32`

Convert double to uint32 (truncates toward zero).

### `uint32(m: decimal) -> uint32`

Convert decimal to uint32 (truncates toward zero).

### `uint32(s: str) -> uint32`

Parse string to uint32.

### `uint32(s: str, base: int) -> uint32`

Parse string to uint32 with explicit base.

### `uint32(b: uint8) -> uint32`

Convert byte to uint32 (widening).

### `uint32(sb: int8) -> uint32`

Convert sbyte to uint32.

### `uint32(s: int16) -> uint32`

Convert short to uint32.

### `uint32(us: uint16) -> uint32`

Convert ushort to uint32 (widening).

### `uint32(u: uint32) -> uint32`

Convert uint to uint32 (identity).

### `uint32(ul: uint64) -> uint32`

Convert ulong to uint32.

### `uint64(b: bool) -> uint64`

Convert bool to uint64. True becomes 1, False becomes 0.

### `uint64(i: int) -> uint64`

Convert int to uint64.

### `uint64(l: int64) -> uint64`

Convert long to uint64.

### `uint64(f: float32) -> uint64`

Convert float to uint64 (truncates toward zero).

### `uint64(d: float) -> uint64`

Convert double to uint64 (truncates toward zero).

### `uint64(m: decimal) -> uint64`

Convert decimal to uint64 (truncates toward zero).

### `uint64(s: str) -> uint64`

Parse string to uint64.

### `uint64(s: str, base: int) -> uint64`

Parse string to uint64 with explicit base.

### `uint64(b: uint8) -> uint64`

Convert byte to uint64 (widening).

### `uint64(sb: int8) -> uint64`

Convert sbyte to uint64.

### `uint64(s: int16) -> uint64`

Convert short to uint64.

### `uint64(us: uint16) -> uint64`

Convert ushort to uint64 (widening).

### `uint64(u: uint32) -> uint64`

Convert uint to uint64 (widening).

### `uint64(ul: uint64) -> uint64`

Convert ulong to uint64 (identity).

### `uint8(b: bool) -> uint8`

Convert bool to uint8. True becomes 1, False becomes 0.

### `uint8(i: int) -> uint8`

Convert int to uint8.

### `uint8(l: int64) -> uint8`

Convert long to uint8.

### `uint8(f: float32) -> uint8`

Convert float to uint8 (truncates toward zero).

### `uint8(d: float) -> uint8`

Convert double to uint8 (truncates toward zero).

### `uint8(m: decimal) -> uint8`

Convert decimal to uint8 (truncates toward zero).

### `uint8(s: str) -> uint8`

Parse string to uint8.

### `uint8(s: str, base: int) -> uint8`

Parse string to uint8 with explicit base.

### `uint8(b: uint8) -> uint8`

Convert byte to uint8 (identity).

### `uint8(sb: int8) -> uint8`

Convert sbyte to uint8.

### `uint8(s: int16) -> uint8`

Convert short to uint8.

### `uint8(us: uint16) -> uint8`

Convert ushort to uint8.

### `uint8(u: uint32) -> uint8`

Convert uint to uint8.

### `uint8(ul: uint64) -> uint8`

Convert ulong to uint8.

### `zip(iterable1: IEnumerable[T1], iterable2: IEnumerable[T2]) -> ZipIterator[T1, T2]`

Make an iterator that aggregates elements from two iterables.
Returns an iterator of tuples, where the i-th tuple contains the i-th element
from each of the argument sequences. The iterator stops when the shortest
input iterable is exhausted.

**Parameters:**

- `iterable1` (IEnumerable[T1]) -- The first iterable
- `iterable2` (IEnumerable[T2]) -- The second iterable

**Returns:** A zip iterator

```python
list(zip([1, 2, 3], ["a", "b", "c"]))    # [(1, "a"), (2, "b"), (3, "c")]
list(zip([1, 2], [10, 20, 30]))           # [(1, 10), (2, 20)]
```

### `zip(iterable1: IEnumerable[T1], iterable2: IEnumerable[T2], strict: bool) -> ZipIterator[T1, T2]`

Make an iterator that aggregates elements from two iterables.
When strict is True, raises ValueError if iterables have different lengths.

**Parameters:**

- `iterable1` (IEnumerable[T1]) -- The first iterable
- `iterable2` (IEnumerable[T2]) -- The second iterable
- `strict` (bool) -- If True, raises ValueError when iterables have different lengths

**Returns:** A zip iterator

### `zip(iterable1: IEnumerable[T1], iterable2: IEnumerable[T2], iterable3: IEnumerable[T3]) -> ZipIterator[T1, T2, T3]`

Make an iterator that aggregates elements from three iterables.
Returns an iterator of tuples, where the i-th tuple contains the i-th element
from each of the argument sequences. The iterator stops when the shortest
input iterable is exhausted.

**Parameters:**

- `iterable1` (IEnumerable[T1]) -- The first iterable
- `iterable2` (IEnumerable[T2]) -- The second iterable
- `iterable3` (IEnumerable[T3]) -- The third iterable

**Returns:** A zip iterator

### `zip(iterable1: IEnumerable[T1], iterable2: IEnumerable[T2], iterable3: IEnumerable[T3], strict: bool) -> ZipIterator[T1, T2, T3]`

Make an iterator that aggregates elements from three iterables.
When strict is True, raises ValueError if iterables have different lengths.

**Parameters:**

- `iterable1` (IEnumerable[T1]) -- The first iterable
- `iterable2` (IEnumerable[T2]) -- The second iterable
- `iterable3` (IEnumerable[T3]) -- The third iterable
- `strict` (bool) -- If True, raises ValueError when iterables have different lengths

**Returns:** A zip iterator

### `len(obj: object) -> int`

Get the length of a collection or string.
This is the fallback overload for dynamically-typed scenarios.

**Parameters:**

- `obj` (object) -- The object to measure

**Returns:** The number of elements

**Raises:**

- `TypeError` -- Thrown when *obj* is null or has no len()

### `assigned(flag: ref bool, value: T) -> T`

Records that a runtime-checked local has been assigned and forwards the stored value, so
a store can set the local's assigned-flag inline — in statement position
(`n = Builtins.Assigned(ref __n_assigned, 5);`) and in walrus/expression position
alike. Used by the emitter for a local whose assignment a `with` block can suppress
(#1839): every store to the local is wrapped so a later read can tell "assigned" from
"unset" at runtime.

**Parameters:**

- `flag` (ref bool) -- The local's assigned-flag; set to `True`.
- `value` (T) -- The value being stored.

**Returns:** *value* unchanged.

### `checked_local(flag: bool, value: T, name: str) -> T`

Reads a runtime-checked local: returns *value* when
*flag* is set, else raises `UnboundLocalError` naming the
variable — Python's `UnboundLocalError` semantics for a local read before assignment
(#1839).

**Parameters:**

- `flag` (bool) -- The local's assigned-flag.
- `value` (T) -- The local's current value (a `default!` placeholder when unset).
- `name` (str) -- The Python name of the local, for the error message.

**Returns:** *value* when *flag* is set.

**Raises:**

- `UnboundLocalError` -- When *flag* is `false`.

### `print(*values: object | None)`

Print values to standard output, matching Python's print() behavior.
Values are converted to strings using ToString() and separated by the separator.

**Parameters:**

- `values` (*object | None) -- Values to print

```python
print("hello")           # hello
print(1, 2, 3)           # 1 2 3
print("a", "b", sep=",") # a,b
```
