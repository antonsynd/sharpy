# Arithmetic Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `+` | Addition | `+` |
| `-` | Subtraction | `-` |
| `*` | Multiplication | `*` |
| `/` | Division* | `/` (with cast if necessary) |
| `//` | Floor division** | `/` (with cast if necessary) |
| `%` | Modulo | `%` |
| `**` | Exponentiation | `Math.Pow(x, y)` |

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

The return type depends on the operands:

Floor division returns the largest integer less than or equal to the
mathematical quotient (rounds toward negative infinity).

| Operands | Result Type |
|----------|-------------|
| Any integer types | `int64` |
| Any float type | Same float type |
| Mixed integer and float | Float type of the float operand |

**Examples:**
```python
7 // 3      # 2 (int64)
-7 // 3     # -3 (int64), not -2
7.5 // 2.0  # 3.0 (float64)
7 // 2.0    # 3.0 (float64) - mixed: result is float64
7.0 // 2    # 3.0 (float64) - mixed: result is float64
7.0f // 2   # 3.0f (float32) - mixed: result is float32
```

## Implementation

- *Standard: ✅ Native*
- *`**`: 🔄 Lowered to `Math.Pow()`*
- *`/`: 🔄 Lowered to floating-point division. See table above.*
- *`//`: 🔄 Lowered to `(long)Math.Floor((double)a / b)` for integers,
`Math.Floor(a / b)` for floats.*

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
