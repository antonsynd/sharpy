# math

```python
import math
```

## Functions

### `math.lcm(a: long, b: long) -> long`

Return the least common multiple of a and b.

### `math.comb(n: int, k: int) -> long`

Return the number of ways to choose k items from n items without repetition and without order.

### `math.perm(n: int) -> long`

Return the number of permutations of n items, equivalent to n factorial.

### `math.prod(iterable: Iterable[int], start: long = 1) -> long`

Return the product of all the elements in the iterable, starting with the given start value.

### `math.log(x: float, base_value: float) -> float`

Return the logarithm of x to the given base.

### `math.ceil(x: float) -> float`

Return the ceiling of x as a float.

### `math.floor(x: float) -> float`

Return the floor of x as a float.

### `math.fabs(x: float) -> float`

Return the absolute value of the float x.

### `math.copysign(x: float, y: float) -> float`

Return a float with the magnitude of x but the sign of y.

### `math.sqrt(x: float) -> float`

Return the square root of x.

### `math.pow(x: float, y: float) -> float`

Return x raised to the power y.

### `math.exp(x: float) -> float`

Return e raised to the power of x.

### `math.log(x: float) -> float`

Return the natural logarithm of x (base e).

### `math.log10(x: float) -> float`

Return the base 10 logarithm of x.

### `math.log2(x: float) -> float`

Return the base 2 logarithm of x.

### `math.sin(x: float) -> float`

Return the sine of x (measured in radians).

### `math.cos(x: float) -> float`

Return the cosine of x (measured in radians).

### `math.tan(x: float) -> float`

Return the tangent of x (measured in radians).

### `math.asin(x: float) -> float`

Return the arc sine (measured in radians) of x.

### `math.acos(x: float) -> float`

Return the arc cosine (measured in radians) of x.

### `math.atan(x: float) -> float`

Return the arc tangent (measured in radians) of x.

### `math.atan2(y: float, x: float) -> float`

Return the arc tangent (measured in radians) of y/x.

### `math.sinh(x: float) -> float`

Return the hyperbolic sine of x.

### `math.cosh(x: float) -> float`

Return the hyperbolic cosine of x.

### `math.tanh(x: float) -> float`

Return the hyperbolic tangent of x.

### `math.degrees(x: float) -> float`

Convert angle x from radians to degrees.

### `math.radians(x: float) -> float`

Convert angle x from degrees to radians.

### `math.isfinite(x: float) -> bool`

Return True if x is neither an infinity nor a NaN, and False otherwise.

### `math.isinf(x: float) -> bool`

Return True if x is a positive or negative infinity, and False otherwise.

### `math.isnan(x: float) -> bool`

Return True if x is a NaN (not a number), and False otherwise.

### `math.trunc(x: float) -> float`

Truncate x to the nearest integral value toward 0.

### `math.expm1(x: float) -> float`

Return exp(x) - 1, computed in a way that is accurate for small x.

### `math.log1p(x: float) -> float`

Return the natural logarithm of 1+x (base e), computed in a way that is accurate for small x.

### `math.remainder(x: float, y: float) -> float`

Return the IEEE 754-style remainder of x with respect to y.

### `math.gcd(a: long, b: long) -> long`

Return the greatest common divisor of a and b.

### `math.factorial(n: int) -> long`

Return n factorial. Raises ValueError for negative n and OverflowError for n > 20.

### `math.isclose(a: float, b: float, rel_tol: float = 1e-9d, abs_tol: float = 0.0d) -> bool`

Determine whether two floating-point numbers are close in value.

### `math.perm(n: int, k: int) -> long`

Return the number of ways to choose k items from n items without repetition and with order.

### `math.fsum(iterable: list[float]) -> float`

Return an accurate floating-point sum of values in the iterable.

### `math.prod(iterable: list[float], start: float = 1.0d) -> float`

Return the product of all the elements in the iterable.

### `math.hypot(x: float, y: float) -> float`

Return the Euclidean distance, sqrt(x*x + y*y).
