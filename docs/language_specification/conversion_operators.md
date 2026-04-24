# Conversion Operators

> **Implementation status:** ✅ Implemented

User-defined implicit and explicit type conversions via `__implicit__` and `__explicit__` dunder methods, mapping directly to C# conversion operators.

## Syntax

```python
class Celsius:
    value: float

    def __init__(self, value: float):
        self.value = value

    @static
    def __implicit__(val: float) -> Celsius:
        return Celsius(val)

    @static
    def __explicit__(val: Celsius) -> float:
        return val.value
```

## Rules

- Must be `@static` (no `self` parameter)
- Exactly one parameter (the source type)
- Return type must be specified (not inferred)
- At least one of {parameter type, return type} must be the enclosing type
- Cannot define both `__implicit__` and `__explicit__` for the same source→target pair
- Multiple `__implicit__` (or `__explicit__`) methods with different source types are allowed (overloading)

## Implicit Conversions (`__implicit__`)

Implicit conversions are applied automatically by the C# runtime when assigning a value of the source type to a variable of the target type.

```python
@static
def __implicit__(val: float) -> Celsius:
    return Celsius(val)
```

Emits: `public static implicit operator Celsius(double val) { ... }`

## Explicit Conversions (`__explicit__`)

Explicit conversions require the `to` operator:

```python
temp: Celsius = Celsius(100.0)
raw: float = temp to float  # invokes __explicit__
```

Emits: `public static explicit operator double(Celsius val) { ... }`

## Diagnostics

| Code | Message |
|------|---------|
| SPY0436 | Conversion operator must be @static |
| SPY0437 | Conversion operator must have exactly 1 parameter |
| SPY0438 | At least one type must be the enclosing type |
| SPY0439 | Cannot define both implicit and explicit for the same type pair |

## C# Emission

```csharp
// __implicit__(val: float) -> Celsius
public static implicit operator Celsius(double val)
{
    return new Celsius(val);
}

// __explicit__(val: Celsius) -> float
public static explicit operator double(Celsius val)
{
    return val.Value;
}
```

## See Also

- [Dunder Methods](dunder_methods.md) — Overview of special methods
- [Type Casting](type_casting.md) — The `to` operator
- [Operator Overloading](operator_overloading.md) — Custom operators
- [Arithmetic Operators](arithmetic_operators.md) — Numeric type promotion
