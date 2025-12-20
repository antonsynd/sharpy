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

*The `/` operator always produces a floating-point result, following Python's semantics where division never truncates. The result type is determined by the operands:

| Operand Types | Result Type | Notes |
|---------------|-------------|-------|
| Both `decimal` | `decimal` | High-precision division |
| `decimal` + any integer | `decimal` | Integer promoted to decimal |
| Any `double` | `double` | |
| Any `float` (no `double`/`decimal`) | `float` | |
| Integer types only | `double` | Always promotes to double |

## Floor Division Operator `//`

**The return type depends on the operands:

Floor division returns the largest integer less than or equal to the
mathematical quotient (rounds toward negative infinity).

| Operands | Result Type |
|----------|-------------|
| Any integer types | `long` |
| Any float type | Same float type |
| Mixed integer and float | Float type of the float operand |

**Examples:**
```python
7 // 3      # 2 (long)
-7 // 3     # -3 (long), not -2
7.5 // 2.0  # 3.0 (double)
7 // 2.0    # 3.0 (double) - mixed: result is double
7.0 // 2    # 3.0 (double) - mixed: result is double
7.0f // 2   # 3.0f (float) - mixed: result is float
```

## Implementation

*Implementation:*
- *Standard: ✅ Native*
- *`**`: 🔄 Lowered to `Math.Pow()`*
- *`/`: 🔄 Lowered to floating-point division. See table above.*
- *`//`: 🔄 Lowered to `(long)Math.Floor((double)a / b)` for integers,
`Math.Floor(a / b)` for floats.*

## Numeric Type Promotion

When binary arithmetic operators (`+`, `-`, `*`) operate on different numeric types, operands are implicitly promoted following .NET rules with some Python-inspired adjustments:

| Left Type | Right Type | Result Type | Notes |
|-----------|------------|-------------|-------|
| `int` | `int` | `int` | |
| `int` | `long` | `long` | Smaller promoted to larger |
| `int` | `double` | `double` | Integer promoted to float |
| `int` | `decimal` | `decimal` | Integer promoted to decimal |
| `float` | `double` | `double` | Lower precision promoted |
| `double` | `decimal` | ❌ Error | Cannot mix double and decimal |
| `byte` | `int` | `int` | Small integers promote to int |
| `short` | `int` | `int` | Small integers promote to int |

**Key Rules:**

1. **Integer operations**: Result is the larger integer type (but at least `int`)
2. **Float operations**: Result is the higher-precision float type
3. **Mixed integer/float**: Integer is promoted to the float type
4. **Decimal is special**: Can mix with integers, but not with `float`/`double`

```python
# Numeric promotion examples
1 + 2           # int + int = int
1 + 2L          # int + long = long
1 + 2.0         # int + double = double
1.0f + 2.0      # float + double = double
1 + 2m          # int + decimal = decimal
1.0 + 2m        # ERROR: double + decimal is not allowed
```

*Implementation: ✅ Native - Follows C# numeric promotion rules.*
