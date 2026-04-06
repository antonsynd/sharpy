# math

Mathematical functions, similar to Python's math module.
This module provides access to mathematical functions defined by the C standard.

```python
import math
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `pi` | `float` | The mathematical constant π = 3.141592..., to available precision. |
| `e` | `float` | The mathematical constant e = 2.718281..., to available precision. |
| `tau` | `float` | The mathematical constant τ = 2π = 6.283185..., to available precision. |
| `inf` | `float` | Positive infinity. |
| `nan` | `float` |  |

## Functions

### `math.gcd(a: long, b: long) -> long`

Return the Greatest Common Divisor of integers a and b.

### `math.factorial(n: int) -> long`

Return the factorial of n. Raises ValueError if n is negative or OverflowException if n is too large (n > 20).

**Raises:**

- `ValueError` -- Thrown if  is negative.
- `OverflowException` -- Thrown if  is greater than 20.

### `math.lcm(a: long, b: long) -> long`

Return the Least Common Multiple of integers a and b.

### `math.isclose(a: float, b: float, rel_tol: float = 1e-9, abs_tol: float = 0.0) -> bool`

Return True if the values a and b are close to each other, and False otherwise.

**Raises:**

- `ValueError` -- Thrown if tolerances are negative.

### `math.comb(n: int, k: int) -> long`

Return the number of ways to choose k items from n items without repetition and without order.

**Raises:**

- `ValueError` -- Thrown if  or  is negative.

### `math.perm(n: int, k: int) -> long`

Return the number of ways to choose k items from n items without repetition and with order.
If k is not specified, then k defaults to n and the function returns n!.

**Raises:**

- `ValueError` -- Thrown if  or  is negative.

### `math.perm(n: int) -> long`

Return the number of ways to arrange n items (n!).

### `math.fsum(iterable: Iterable[float]) -> float`

Return an accurate floating point sum of values in the iterable, using Kahan summation.

### `math.prod(iterable: Iterable[float], start: float = 1.0) -> float`

Return the product of a start value (default: 1) times an iterable of numbers.

### `math.prod(iterable: Iterable[int], start: long = 1) -> long`

Return the product of a start value (default: 1) times an iterable of integers.

### `math.hypot(x: float, y: float) -> float`

Return the Euclidean distance, sqrt(x*x + y*y).

### `math.ceil(x: float) -> float`

Return the ceiling of x, the smallest integer greater than or equal to x.

**Parameters:**

- `x` (float) -- The value to ceil.

**Returns:** The smallest integer greater than or equal to .

```python
math.ceil(3.2)    # 4.0
math.ceil(-0.5)   # 0.0
```

!!! note
    Unlike Python's math.ceil which returns int,
    Sharpy returns double to match .NET's
    (Axiom 1: .NET compatibility). Cast to int if needed: int(math.ceil(x)).

### `math.floor(x: float) -> float`

Return the floor of x, the largest integer less than or equal to x.

**Parameters:**

- `x` (float) -- The value to floor.

**Returns:** The largest integer less than or equal to .

```python
math.floor(3.7)    # 3.0
math.floor(-0.5)   # -1.0
```

!!! note
    Unlike Python's math.floor which returns int,
    Sharpy returns double to match .NET's
    (Axiom 1: .NET compatibility). Cast to int if needed: int(math.floor(x)).

### `math.fabs(x: float) -> float`

Return the absolute value of x as a float.

**Parameters:**

- `x` (float) -- The value

**Returns:** The absolute value of

### `math.copysign(x: float, y: float) -> float`

Return x with the sign of y.

**Parameters:**

- `x` (float) -- The magnitude
- `y` (float) -- The value whose sign is used

**Returns:** with the sign of

### `math.sqrt(x: float) -> float`

Return the square root of x.

**Parameters:**

- `x` (float) -- The value to compute the square root of.

**Returns:** The square root of .

```python
math.sqrt(16.0)    # 4.0
math.sqrt(2.0)     # 1.4142135623730951
```

### `math.pow(x: float, y: float) -> float`

Return x raised to the power y.

**Parameters:**

- `x` (float) -- The base.
- `y` (float) -- The exponent.

**Returns:** raised to the power .

### `math.exp(x: float) -> float`

Return e raised to the power x.

**Parameters:**

- `x` (float) -- The exponent.

**Returns:** e raised to the power .

### `math.log(x: float) -> float`

Return the natural logarithm of x (to base e).

**Parameters:**

- `x` (float) -- The value.

**Returns:** The natural logarithm of .

### `math.log(x: float, base_value: float) -> float`

Return the logarithm of x to the given base.

**Parameters:**

- `x` (float) -- The value.
- `base_value` (float)

**Returns:** The logarithm of  to base .

### `math.log10(x: float) -> float`

Return the base-10 logarithm of x.

**Parameters:**

- `x` (float) -- The value.

**Returns:** The base-10 logarithm of .

### `math.log2(x: float) -> float`

Return the base-2 logarithm of x.

**Parameters:**

- `x` (float) -- The value.

**Returns:** The base-2 logarithm of .

### `math.sin(x: float) -> float`

Return the sine of x radians.

**Parameters:**

- `x` (float) -- The angle in radians.

**Returns:** The sine of .

### `math.cos(x: float) -> float`

Return the cosine of x radians.

**Parameters:**

- `x` (float) -- The angle in radians.

**Returns:** The cosine of .

### `math.tan(x: float) -> float`

Return the tangent of x radians.

**Parameters:**

- `x` (float) -- The angle in radians.

**Returns:** The tangent of .

### `math.asin(x: float) -> float`

Return the arc sine of x, in radians.

**Parameters:**

- `x` (float) -- The value.

**Returns:** The arc sine of  in radians.

### `math.acos(x: float) -> float`

Return the arc cosine of x, in radians.

**Parameters:**

- `x` (float) -- The value.

**Returns:** The arc cosine of  in radians.

### `math.atan(x: float) -> float`

Return the arc tangent of x, in radians.

**Parameters:**

- `x` (float) -- The value.

**Returns:** The arc tangent of  in radians.

### `math.atan2(y: float, x: float) -> float`

Return the arc tangent of y/x, in radians.

**Parameters:**

- `y` (float) -- The y coordinate.
- `x` (float) -- The x coordinate.

**Returns:** The arc tangent of / in radians.

### `math.sinh(x: float) -> float`

Return the hyperbolic sine of x.

**Parameters:**

- `x` (float) -- The value.

**Returns:** The hyperbolic sine of .

### `math.cosh(x: float) -> float`

Return the hyperbolic cosine of x.

**Parameters:**

- `x` (float) -- The value.

**Returns:** The hyperbolic cosine of .

### `math.tanh(x: float) -> float`

Return the hyperbolic tangent of x.

**Parameters:**

- `x` (float) -- The value.

**Returns:** The hyperbolic tangent of .

### `math.degrees(x: float) -> float`

Convert angle x from radians to degrees.

**Parameters:**

- `x` (float) -- The angle in radians.

**Returns:** The angle in degrees.

### `math.radians(x: float) -> float`

Convert angle x from degrees to radians.

**Parameters:**

- `x` (float) -- The angle in degrees.

**Returns:** The angle in radians.

### `math.isfinite(x: float) -> bool`

Return True if x is neither an infinity nor a NaN, and False otherwise.

**Parameters:**

- `x` (float) -- The value to check.

**Returns:** true if  is finite; otherwise false.

### `math.isinf(x: float) -> bool`

Return True if x is a positive or negative infinity, and False otherwise.

**Parameters:**

- `x` (float) -- The value to check.

**Returns:** true if  is infinite; otherwise false.

### `math.isnan(x: float) -> bool`

Return True if x is a NaN (not a number), and False otherwise.

**Parameters:**

- `x` (float) -- The value to check.

**Returns:** true if  is NaN; otherwise false.

### `math.trunc(x: float) -> float`

Return the integer part of x, removing all fractional digits.

**Parameters:**

- `x` (float) -- The value to truncate.

**Returns:** The integer part of .

### `math.expm1(x: float) -> float`

Return e raised to the power x, minus 1. Accurate for small x.

**Parameters:**

- `x` (float) -- The exponent.

**Returns:** e raised to the power , minus 1.

### `math.log1p(x: float) -> float`

Return the natural logarithm of 1+x (base e). Accurate for small x.

**Parameters:**

- `x` (float) -- The value (must be greater than -1).

**Returns:** The natural logarithm of 1 + .

**Raises:**

- `ValueError` -- Thrown if  is less than or equal to -1.

### `math.remainder(x: float, y: float) -> float`

Return the IEEE 754-style remainder of x with respect to y.

**Parameters:**

- `x` (float) -- The dividend.
- `y` (float) -- The divisor.

**Returns:** The IEEE 754-style remainder of  / .

**Raises:**

- `ValueError` -- Thrown if  is zero.
