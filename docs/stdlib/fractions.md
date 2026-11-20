# fractions

```python
import fractions
```

## Fraction

### Properties

| Name | Type | Description |
|------|------|-------------|
| `numerator` | `BigInteger` |  |
| `denominator` | `BigInteger` |  |

### `limit_denominator(max_denominator: int64 = 1000000) -> Fraction`

### `limit_denominator(max_denominator: BigInteger) -> Fraction`

The closest Fraction to this one whose denominator is at most `max_denominator`, as CPython's
`fractions.Fraction.limit_denominator`. On a tie the candidate with the smaller denominator is
returned; with `max_denominator` 1 and a value midway between two integers, that is the floor.
Raises `ValueError` when `max_denominator` is less than 1.

### `floor_div(other: Fraction) -> int64`

### `mod(a: Fraction, b: Fraction) -> Fraction`

### `pow(exponent: int) -> Fraction`

### `abs() -> Fraction`

### `to_long() -> int64`

### `to_double() -> float`

### `__str__() -> str`

`repr()` uses the same method.
