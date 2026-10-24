# Built-in Primitive Types

| Sharpy Type | .NET Type | Size | Notes |
|-------------|-----------|------|-------|
| `int32` | `System.Int32` | 32-bit | Default integer type |
| `int64` | `System.Int64` | 64-bit | Large integers |
| `int16` | `System.Int16` | 16-bit | Small integers |
| `int8` | `System.SByte` | 8-bit | Signed byte |
| `uint32` | `System.UInt32` | 32-bit | Unsigned 32-bit |
| `uint64` | `System.UInt64` | 64-bit | Unsigned 64-bit |
| `uint16` | `System.UInt16` | 16-bit | Unsigned 16-bit |
| `uint8` | `System.Byte` | 8-bit | Unsigned byte |
| `float32` | `System.Single` | 32-bit | Single-precision |
| `float64` | `System.Double` | 64-bit | Double-precision (default) |
| `decimal` | `System.Decimal` | 128-bit | High-precision decimal |
| `bool` | `System.Boolean` | - | `True` or `False` |
| `str` | `System.String` | - | Immutable Unicode string |
| `char` | `System.Char` | 16-bit | Interop spelling: Sharpy sees it as a one-character `str` (see below) |
| `object` | `System.Object` | - | Base type for all types |

`char` is the interop spelling of a one-character `str`, not a separate type: a `char` annotation
denotes `str`, a CLR `System.Char` value reaches Sharpy as a one-character `str`, and a
one-character `str` literal is the one argument Sharpy converts into a CLR `char` parameter (#1291,
#1402). Python APIs that take a character — `str.center`, `ljust` and `rjust`'s `fillchar` — take a
`str` and raise `TypeError` unless it is exactly one character long.

There are aliases present that help ease both Python and C# developers at the cost of consistency.

| Sharpy Alias | Sharpy Type |
|--------------|-------------|
| `byte` | `uint8` |
| `double` | `float64` |
| `float` | `float64` |
| `int` | `int32` |
| `long` | `int64` |
| `sbyte` | `int8` |
| `short` | `int16` |
| `string` | `str` |
| `uint` | `uint32` |
| `ulong` | `uint64` |
| `ushort` | `uint16` |

## Conversions

Every primitive width is callable as a conversion function. The function accepts any numeric
type, `bool`, or `str`, returning the target width with range checking:

```python
x: int8 = int8("42")       # string parse → sbyte
y: uint16 = uint16(1000)   # int → ushort, checked
z: float32 = float32(3.14) # double → float, overflow → Infinity
```

| Sharpy Call | CLR Method | Returns | Out of range |
|-------------|-----------|---------|--------------|
| `int8(x)` | `Builtins.Int8` | `sbyte` | `OverflowError` |
| `int16(x)` | `Builtins.Int16` | `short` | `OverflowError` |
| `int(x)` / `int32(x)` | `Builtins.Int` | `int` | `OverflowError` |
| `long(x)` / `int64(x)` | `Builtins.Long` | `long` | `OverflowError` |
| `uint8(x)` | `Builtins.UInt8` | `byte` | `OverflowError` |
| `uint16(x)` | `Builtins.UInt16` | `ushort` | `OverflowError` |
| `uint32(x)` | `Builtins.UInt32` | `uint` | `OverflowError` |
| `uint64(x)` | `Builtins.UInt64` | `ulong` | `OverflowError` |
| `float32(x)` | `Builtins.Float32` | `float` | `Infinity` |
| `float(x)` / `float64(x)` | `Builtins.Float` | `double` | `Infinity` |
| `bool(x)` | `Builtins.Bool` | `bool` | — |
| `str(x)` | `Builtins.Str` | `string` | — |
| `decimal(x)` | `Builtins.Decimal` | `decimal` | `OverflowError` |

Integer conversion functions also accept an explicit base for string parsing:

```python
int8("0xff", 16)   # OverflowError — 255 > 127
int8("0x7f", 16)   # 127
uint8("0xff", 16)  # 255
int16("0b1010", 2) # 10
```

Every string conversion reads CPython's numeric-text grammar: surrounding whitespace, an optional
sign, a `0x`/`0o`/`0b` prefix only when it matches the explicit base, PEP 515 single underscores
between digits (`int("1_000")` is 1000, `float("1_000.5")` is 1000.5), and Unicode decimal digits.
Malformed text raises `ValueError`; well-formed text that does not fit the target width raises
`OverflowError` — including `int("2147483648")`, since `int` is 32-bit (deviation
`int-text-out-of-range`, `docs/deviations.yaml`).

Aliases are transparent in call position — `sbyte("42")` compiles identically to `int8("42")`,
resolved by CLR type identity rather than by spelling.

## Array Type

Sharpy exposes raw .NET arrays as `array[T]`, distinct from `list[T]`:

| Sharpy Type | .NET Type | Notes |
|-------------|-----------|-------|
| `array[T]` | `T[]` | Fixed-size, .NET native array |
| `list[T]` | `Sharpy.List<T>` | Dynamic, Pythonic wrapper |

```python
# Array creation
arr: array[int] = array[int](10)    # Fixed size of 10, zero-initialized
arr[0] = 42                          # Index access same as list

# Converting between array and list
from system import Array

lst: list[int] = [1, 2, 3]
arr: array[int] = Array[int](lst)   # Create array from list

lst2: list[int] = list(arr)         # Create list from array

# Arrays are useful for:
# - Interop with .NET APIs expecting T[]
# - Performance-critical fixed-size collections
# - *args implementation (params T[] internally)
```

**Note:** Most Sharpy code should use `list[T]` for its Pythonic API. Use `array[T]` primarily for .NET interop or when a fixed-size array is explicitly needed.

*Implementation*
- *✅ Native - Direct mapping to .NET types.*
