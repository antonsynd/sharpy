# Built-in Primitive Types

| Sharpy Type | .NET Type | Size | Notes |
|-------------|-----------|------|-------|
| `int` | `System.Int32` | 32-bit | Default integer type |
| `long` | `System.Int64` | 64-bit | Large integers |
| `short` | `System.Int16` | 16-bit | Small integers |
| `byte` | `System.Byte` | 8-bit | Unsigned byte |
| `uint` | `System.UInt32` | 32-bit | Unsigned 32-bit |
| `ulong` | `System.UInt64` | 64-bit | Unsigned 64-bit |
| `ushort` | `System.UInt16` | 16-bit | Unsigned 16-bit |
| `sbyte` | `System.SByte` | 8-bit | Signed byte |
| `float` | `System.Single` | 32-bit | Single-precision |
| `double` | `System.Double` | 64-bit | Double-precision (default) |
| `decimal` | `System.Decimal` | 128-bit | High-precision decimal |
| `bool` | `System.Boolean` | - | `True` or `False` |
| `str` | `System.String` | - | Immutable Unicode string |
| `char` | `System.Char` | 16-bit | Single Unicode character |
| `object` | `System.Object` | - | Base type for all types |

## Array Type

Sharpy exposes raw .NET arrays as `array[T]`, distinct from `list[T]`:

| Sharpy Type | .NET Type | Notes |
|-------------|-----------|-------|
| `array[T]` | `T[]` | Fixed-size, .NET native array |
| `list[T]` | `Sharpy.Core.List<T>` | Dynamic, Pythonic wrapper |

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
