# numpy

Numerical computing with multi-dimensional arrays and mathematical operations.

```python
import numpy
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `start` | `int \| None` | Inclusive start index. \`null\` means "from the beginning". |
| `stop` | `int \| None` | Exclusive stop index. \`null\` means "to the end". |
| `step` | `int \| None` | Step between successive indices. \`null\` defaults to 1. Cannot be 0. |
| `is_squeeze` | `bool` | True when this spec was created by \`at\` and the axis should be removed from the result shape. |
| `all` | `SliceSpec` | Sentinel slice representing \`:\` — take every element along this axis. |

## Functions

### `numpy.at(index: int) -> SliceSpec`

Create a single-index spec that squeezes (removes) the axis from the result.

### `numpy.range(start: int, stop: int) -> SliceSpec`

Create a slice of the form `start:stop`.

### `numpy.range(start: int, stop: int, step: int) -> SliceSpec`

Create a slice of the form `start:stop:step`.

### `numpy.equal(a: ndarray[T], b: ndarray[T]) -> ndarray[bool]`

Elementwise `a == b` with broadcasting, returning a boolean ndarray.

### `numpy.not_equal(a: ndarray[T], b: ndarray[T]) -> ndarray[bool]`

Elementwise `a != b` with broadcasting.

### `numpy.less(a: ndarray[T], b: ndarray[T]) -> ndarray[bool]`

Elementwise `a < b` with broadcasting.

### `numpy.less_equal(a: ndarray[T], b: ndarray[T]) -> ndarray[bool]`

Elementwise `a <= b` with broadcasting.

### `numpy.greater(a: ndarray[T], b: ndarray[T]) -> ndarray[bool]`

Elementwise `a > b` with broadcasting.

### `numpy.greater_equal(a: ndarray[T], b: ndarray[T]) -> ndarray[bool]`

Elementwise `a >= b` with broadcasting.

### `numpy.concatenate(arrays: list[ndarray[float]], axis: int = 0) -> ndarray[float]`

Join a sequence of arrays along an existing *axis*.
All input arrays must have the same shape except along *axis*.

**Parameters:**

- `arrays` (list[ndarray[float]]) -- Arrays to concatenate. Must not be empty.
- `axis` (int) -- Axis along which to concatenate. Default 0.

**Returns:** A new C-contiguous array.

### `numpy.stack(arrays: list[ndarray[float]], axis: int = 0) -> ndarray[float]`

Join a sequence of arrays along a new axis. All inputs must have the same shape.
The output has rank `ndim + 1`.

**Parameters:**

- `arrays` (list[ndarray[float]]) -- Arrays to stack.
- `axis` (int) -- Index of the new axis in the output. Default 0.

### `numpy.hstack(arrays: list[ndarray[float]]) -> ndarray[float]`

Stack arrays horizontally — along the second axis for 2-D inputs, along axis 0 for 1-D.

### `numpy.vstack(arrays: list[ndarray[float]]) -> ndarray[float]`

Stack arrays vertically — along the first axis. For 1-D inputs they are promoted
to row vectors (shape `(1, n)`) before stacking.

### `numpy.split(a: ndarray[float], indices: list[int], axis: int = 0) -> list[ndarray[float]]`

Split *a* along *axis* at the given index boundaries,
returning a list of sub-arrays. Mirrors NumPy's `numpy.split`.

**Parameters:**

- `a` (ndarray[float]) -- Input array.
- `indices` (list[int]) -- Sorted strictly-increasing list of split points. A raw
`int[]` deliberately: the stdlib rule is about what a public API produces,
and a parameter accepts — a Sharpy `list[int]` converts to an array at the call
boundary, so requiring one here would add a copy without adding reach (#1293).
- `axis` (int) -- Axis along which to split. Default 0.

**Returns:** A `list[T]` of sub-arrays. A Sharpy list, not `NdArray[]`:
a raw .NET array in a public return is the surface #1256 fixed for `sys.argv`, and
it is what the caller has to live with — `parts.append(...)`, `len(parts)` and
slicing all work on the Sharpy collection and none of them work on the array.

### `numpy.split(a: ndarray[float], sections: int, axis: int = 0) -> list[ndarray[float]]`

Split *a* along *axis* into *sections*
equal parts. Mirrors NumPy's `numpy.split(a, N)`.

**Parameters:**

- `a` (ndarray[float]) -- Input array.
- `sections` (int) -- Number of equal sections. The axis length must divide evenly.
- `axis` (int) -- Axis along which to split. Default 0.

**Returns:** A `list[T]` of *sections* sub-arrays.

!!! note
    Measured against numpy 2.5.1 rather than assumed: `np.split(np.arange(6.0), 3)` gives
    three parts of two, and `np.split(a, 4)` raises
    `ValueError: array split does not result in an equal division` — reproduced verbatim.
    `np.array_split` is the lenient variant that tolerates uneven division; it does not
    exist here yet and is deliberately not added with this (#1422 scopes only `split`).
    
    
    ONE DELIBERATE DIVERGENCE, measured: numpy raises `ZeroDivisionError: integer modulo by
    zero` for `sections=0` and `ValueError: number sections must be larger than 0.`
    for negatives. The former is an artifact of numpy computing `N % sections` before it
    validates the argument, not a contract worth reproducing; both non-positive cases raise the
    ValueError here.

**Raises:**

- `ValueError` -- If the axis length is not divisible by *sections*, or if
*sections* is not positive.

### `numpy.where(condition: ndarray[bool], x: ndarray[float], y: ndarray[float]) -> ndarray[float]`

Return an array whose elements are taken from *x* where
*condition* is True, and *y* otherwise.
All three inputs are broadcast to a common shape.

### `numpy.clip(a: ndarray[float], min: float, max: float) -> ndarray[float]`

Clamp every element of *a* to the interval `[min, max]`.

### `numpy.sqrt(a: ndarray[float]) -> ndarray[float]`

Elementwise square root.

### `numpy.sqrt(a: float) -> float`

Scalar square root — convenience overload mirroring NumPy.

### `numpy.exp(a: ndarray[float]) -> ndarray[float]`

Elementwise natural exponential.

### `numpy.exp(a: float) -> float`

Scalar natural exponential.

### `numpy.log(a: ndarray[float]) -> ndarray[float]`

Elementwise natural logarithm.

### `numpy.log(a: float) -> float`

Scalar natural logarithm.

### `numpy.log2(a: ndarray[float]) -> ndarray[float]`

Elementwise base-2 logarithm.

### `numpy.log2(a: float) -> float`

Scalar base-2 logarithm.

### `numpy.log10(a: ndarray[float]) -> ndarray[float]`

Elementwise base-10 logarithm.

### `numpy.log10(a: float) -> float`

Scalar base-10 logarithm.

### `numpy.abs(a: ndarray[float]) -> ndarray[float]`

Elementwise absolute value.

### `numpy.abs(a: float) -> float`

Scalar absolute value.

### `numpy.sin(a: ndarray[float]) -> ndarray[float]`

Elementwise sine (radians).

### `numpy.sin(a: float) -> float`

Scalar sine.

### `numpy.cos(a: ndarray[float]) -> ndarray[float]`

Elementwise cosine (radians).

### `numpy.cos(a: float) -> float`

Scalar cosine.

### `numpy.tan(a: ndarray[float]) -> ndarray[float]`

Elementwise tangent (radians).

### `numpy.tan(a: float) -> float`

Scalar tangent.

### `numpy.arcsin(a: ndarray[float]) -> ndarray[float]`

Elementwise arcsine, returning radians.

### `numpy.arcsin(a: float) -> float`

Scalar arcsine.

### `numpy.arccos(a: ndarray[float]) -> ndarray[float]`

Elementwise arccosine, returning radians.

### `numpy.arccos(a: float) -> float`

Scalar arccosine.

### `numpy.arctan(a: ndarray[float]) -> ndarray[float]`

Elementwise arctangent, returning radians.

### `numpy.arctan(a: float) -> float`

Scalar arctangent.

### `numpy.floor(a: ndarray[float]) -> ndarray[float]`

Elementwise floor.

### `numpy.floor(a: float) -> float`

Scalar floor.

### `numpy.ceil(a: ndarray[float]) -> ndarray[float]`

Elementwise ceiling.

### `numpy.ceil(a: float) -> float`

Scalar ceiling.

### `numpy.round(a: ndarray[float], decimals: int = 0) -> ndarray[float]`

Elementwise round to *decimals* decimal places (banker's rounding).

### `numpy.round(a: float, decimals: int = 0) -> float`

Scalar round to *decimals* decimal places.

### `numpy.power(a: ndarray[float], b: ndarray[float]) -> ndarray[float]`

Elementwise `a ** b` with broadcasting (NumPy equivalent of `numpy.power`).
C# has no `**` operator, so this is exposed as a module function.

**Parameters:**

- `a` (ndarray[float]) -- Base array.
- `b` (ndarray[float]) -- Exponent array. Broadcast against *a*.

### `numpy.power(a: ndarray[float], b: float) -> ndarray[float]`

Raise every element of *a* to the scalar power *b*.

### `numpy.power(a: float, b: ndarray[float]) -> ndarray[float]`

Raise the scalar *a* elementwise to the powers in *b*.

### `numpy.sum(a: ndarray[float]) -> float`

Sum of all elements.

### `numpy.min(a: ndarray[float]) -> float`

Minimum element. Throws when *a* is empty.

### `numpy.max(a: ndarray[float]) -> float`

Maximum element. Throws when *a* is empty.

### `numpy.mean(a: ndarray[float]) -> float`

Arithmetic mean. Throws when *a* is empty.

### `numpy.var(a: ndarray[float]) -> float`

Population variance (ddof = 0). Throws when *a* is empty.

### `numpy.std(a: ndarray[float]) -> float`

Population standard deviation (ddof = 0). Throws when *a* is empty.

### `numpy.median(a: ndarray[float]) -> float`

Median of all elements. Throws when *a* is empty.

### `numpy.sum(a: ndarray[float], axis: int) -> ndarray[float]`

Sum along *axis*, removing that dimension.

### `numpy.min(a: ndarray[float], axis: int) -> ndarray[float]`

Minimum along *axis*, removing that dimension.

### `numpy.max(a: ndarray[float], axis: int) -> ndarray[float]`

Maximum along *axis*, removing that dimension.

### `numpy.mean(a: ndarray[float], axis: int) -> ndarray[float]`

Mean along *axis*, removing that dimension.

### `numpy.var(a: ndarray[float], axis: int) -> ndarray[float]`

Population variance along *axis*, removing that dimension.

### `numpy.std(a: ndarray[float], axis: int) -> ndarray[float]`

Population standard deviation along *axis*.

### `numpy.median(a: ndarray[float], axis: int) -> ndarray[float]`

Median along *axis*, removing that dimension.

### `numpy.sort(a: ndarray[float]) -> ndarray[float]`

Return a sorted copy of the input. For 1-D input this is a plain ascending sort;
for higher-rank inputs the array is flattened first.

### `numpy.argsort(a: ndarray[float]) -> ndarray[int64]`

Return the indices that would sort the input — i.e. `a.Sort()` is equivalent
to `a.Take(Argsort(a))` for 1-D inputs.

### `numpy.unique(a: ndarray[float]) -> ndarray[float]`

Return the sorted unique elements of *a* as a 1-D array.

### `numpy.searchsorted(a: ndarray[float], values: ndarray[float]) -> ndarray[int64]`

Find indices where elements of *values* should be inserted into
the sorted 1-D array *a* to maintain order. Uses NumPy's "left" side
convention (the first valid insertion point).

### `numpy.allclose(a: ndarray[float], b: ndarray[float], rtol: float = 1e-5, atol: float = 1e-8) -> bool`

True if every pair of elements in *a* and *b* is
close, using NumPy's mixed absolute/relative tolerance:
`|a - b| <= atol + rtol * |b|`.

### `numpy.isnan(a: ndarray[float]) -> ndarray[bool]`

Elementwise `double.IsNaN`.

### `numpy.isinf(a: ndarray[float]) -> ndarray[bool]`

Elementwise `double.IsInfinity`.

### `numpy.isfinite(a: ndarray[float]) -> ndarray[bool]`

Elementwise `double.IsFinite` (neither infinite nor NaN).

### `numpy.array(data: IEnumerable[T]) -> ndarray[T]`

Construct a 1-D `ndarray[T]` from a flat data buffer.

**Parameters:**

- `data` (IEnumerable[T]) -- Source data. Length determines the shape.

**Returns:** A new 1-D ndarray owning a copy of *data*.

### `numpy.array(data: IEnumerable[IEnumerable[T]]) -> ndarray[T]`

Construct a 2-D `ndarray[T]` from a nested sequence of rows
(e.g. `[[1, 2], [3, 4]]`). All rows must have the same length.

**Parameters:**

- `data` (IEnumerable[IEnumerable[T]]) -- Sequence of equal-length rows; the row count and the
common row length determine the 2-D shape.

**Returns:** A new 2-D ndarray owning a copy of the flattened data.

**Raises:**

- `ArgumentException` -- Rows have differing lengths.

### `numpy.zeros(*shape: int) -> ndarray[float]`

Return a new ndarray of the given shape, filled with 0.0.

**Parameters:**

- `shape` (*int) -- Shape of the result. Each dimension must be non-negative.

### `numpy.ones(*shape: int) -> ndarray[float]`

Return a new ndarray of the given shape, filled with 1.0.

**Parameters:**

- `shape` (*int) -- Shape of the result. Each dimension must be non-negative.

### `numpy.full(shape: list[int], value: T) -> ndarray[T]`

Return a new ndarray of the given shape, filled with *value*.

**Parameters:**

- `shape` (list[int]) -- Shape of the result.
- `value` (T) -- Fill value.

### `numpy.eye(n: int) -> ndarray[float]`

Return an *n*×*n* identity matrix.

**Parameters:**

- `n` (int) -- Square matrix dimension.

### `numpy.arange(stop: float) -> ndarray[float]`

Return evenly spaced values within the half-open interval `[0, stop)` — numpy's
single-argument `arange` (#1469).

**Parameters:**

- `stop` (float) -- Exclusive end of the interval.

!!! note
    A separate overload rather than a default on `arange`,
    because a default cannot express this: numpy reads ONE argument as `stop`, not as
    `start`, so the arity-1 form fills the FIRST parameter's role with the SECOND
    parameter's meaning. `np.arange(6.0)` was an SPY0224 arity error until this existed.
    
    Verified against numpy 2.5.1: `arange(6.0)` → `[0. 1. 2. 3. 4. 5.]`,
    `arange(2.5)` → `[0. 1. 2.]`, and a non-positive stop is empty rather than an
    error — `arange(0)` and `arange(-3.0)` both give an empty array, which the
    three-argument implementation already produces for `stop <= start`.

### `numpy.arange(start: float, stop: float, step: float = 1.0) -> ndarray[float]`

Return evenly spaced values within a half-open interval `[start, stop)`.

**Parameters:**

- `start` (float) -- Inclusive start of the interval.
- `stop` (float) -- Exclusive end of the interval.
- `step` (float) -- Step size between successive values. Default 1.0. Cannot be zero.

### `numpy.linspace(start: float, stop: float, num: int = 50) -> ndarray[float]`

Return *num* evenly spaced samples over the closed interval `[start, stop]`.

**Parameters:**

- `start` (float) -- Inclusive start of the interval.
- `stop` (float) -- Inclusive end of the interval.
- `num` (int) -- Number of samples to generate. Must be non-negative. Default 50.

### `numpy.empty(*shape: int) -> ndarray[float]`

Return a new uninitialized ndarray of the given shape. Backed by a fresh
zero-initialized buffer (CLR semantics — no truly-uninitialized storage).

**Parameters:**

- `shape` (*int) -- Shape of the result.

### `numpy.dot(a: ndarray[float], b: ndarray[float]) -> ndarray[float]`

Dot product of two arrays — top-level alias for `dot`.

**Parameters:**

- `a` (ndarray[float]) -- Left operand.
- `b` (ndarray[float]) -- Right operand.

### `numpy.matmul(a: ndarray[float], b: ndarray[float]) -> ndarray[float]`

Matrix product — top-level alias for `matmul`.

**Parameters:**

- `a` (ndarray[float]) -- Left operand.
- `b` (ndarray[float]) -- Right operand.

### `numpy.fft(a: ndarray[float]) -> ndarray[system.numerics.Complex]`

Compute the 1-D discrete Fourier transform of a real-valued ndarray.

**Parameters:**

- `a` (ndarray[float]) -- Input 1-D ndarray of real values.

**Returns:** A 1-D ndarray of complex values with the same length as the input.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 1-dimensional.

### `numpy.fft(a: ndarray[system.numerics.Complex]) -> ndarray[system.numerics.Complex]`

Compute the 1-D discrete Fourier transform of a complex-valued ndarray.

### `numpy.ifft(a: ndarray[system.numerics.Complex]) -> ndarray[system.numerics.Complex]`

Compute the 1-D inverse discrete Fourier transform of a complex-valued ndarray.

**Parameters:**

- `a` (ndarray[system.numerics.Complex]) -- Input 1-D ndarray of complex values.

**Returns:** A 1-D ndarray of complex values with the same length as the input.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 1-dimensional.

### `numpy.fftfreq(n: int, d: float = 1.0) -> ndarray[float]`

Return the discrete Fourier transform sample frequencies for a transform of length *n*.

**Parameters:**

- `n` (int) -- Window length. Must be non-negative.
- `d` (float) -- Sample spacing (inverse of the sampling rate). Default 1.0.

**Returns:** A 1-D ndarray of length *n*. Frequency bins are arranged in
NumPy order: `[0, 1, ..., n/2-1, -n/2, ..., -1] / (d*n)` for even n,
or `[0, 1, ..., (n-1)/2, -(n-1)/2, ..., -1] / (d*n)` for odd n.

**Raises:**

- `ValueError` -- Thrown when *n* is negative.

### `numpy.dot(a: ndarray[float], b: ndarray[float]) -> ndarray[float]`

Dot product of two arrays.
  * 1-D × 1-D — inner product (scalar) returned as a 0-D ndarray.
  * 2-D × 2-D — standard matrix multiplication.
  * 2-D × 1-D — matrix-vector product.
  * 1-D × 2-D — vector-matrix product (treats vector as a row).

**Parameters:**

- `a` (ndarray[float]) -- Left operand.
- `b` (ndarray[float]) -- Right operand.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* or *b* is null.
- `ValueError` -- Thrown when shapes are incompatible or rank is unsupported.

### `numpy.matmul(a: ndarray[float], b: ndarray[float]) -> ndarray[float]`

Matrix product. For 1-D and 2-D inputs this is equivalent to `dot`.

**Parameters:**

- `a` (ndarray[float]) -- Left operand.
- `b` (ndarray[float]) -- Right operand.

### `numpy.inv(a: ndarray[float]) -> ndarray[float]`

Compute the (multiplicative) inverse of a square matrix.

**Parameters:**

- `a` (ndarray[float]) -- A square 2-D array.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 2-D, not square, or is singular.

### `numpy.det(a: ndarray[float]) -> float`

Compute the determinant of a square 2-D array.

**Parameters:**

- `a` (ndarray[float]) -- A square 2-D array.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 2-D or not square.

### `numpy.eig(a: ndarray[float]) -> tuple[ndarray[float], ndarray[float]]`

Compute the eigenvalues and (right) eigenvectors of a square 2-D array.

**Parameters:**

- `a` (ndarray[float]) -- A square 2-D array.

**Returns:** A tuple `(eigenvalues, eigenvectors)` where `eigenvalues` is a 1-D ndarray and
`eigenvectors` is a 2-D ndarray whose columns are the eigenvectors. Imaginary parts
of complex eigenvalues are dropped — only the real component is returned.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 2-D or not square.

### `numpy.svd(a: ndarray[float]) -> tuple[ndarray[float], ndarray[float], ndarray[float]]`

Singular value decomposition. Returns `(U, S, Vh)` such that `A = U · diag(S) · Vh`.

**Parameters:**

- `a` (ndarray[float]) -- A 2-D array.

**Returns:** A tuple `(U, S, Vh)` where `U` and `Vh` are 2-D ndarrays and `S`
is a 1-D ndarray of singular values in descending order.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 2-D.

### `numpy.solve(a: ndarray[float], b: ndarray[float]) -> ndarray[float]`

Solve the linear system `A x = b` for `x`.

**Parameters:**

- `a` (ndarray[float]) -- Coefficient matrix (square 2-D array).
- `b` (ndarray[float]) -- Right-hand side. Either a 1-D vector or a 2-D matrix.

**Returns:** Solution with the same rank as *b* (1-D ndarray when *b*
is 1-D, 2-D ndarray otherwise).

**Raises:**

- `ArgumentNullException` -- Thrown when *a* or *b* is null.
- `ValueError` -- Thrown when shapes are incompatible, the matrix is singular, or the rank is unsupported.

### `numpy.norm(a: ndarray[float]) -> float`

Compute the L2 (Frobenius) norm of an array.
  * 1-D — Euclidean (L2) norm.
  * 2-D — Frobenius norm.

**Parameters:**

- `a` (ndarray[float]) -- Input array.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 1-D or 2-D.

### `numpy.seed(seed: int)`

Seed the thread-local random number generator with *seed*.

**Parameters:**

- `seed` (int) -- Seed value for the underlying `System.Random`.

### `numpy.rand(*shape: int) -> ndarray[float]`

Random samples from a uniform distribution over `[0, 1)`.

**Parameters:**

- `shape` (*int) -- Shape of the result. May be empty (returns a 0-D scalar array).

### `numpy.randn(*shape: int) -> ndarray[float]`

Random samples from the standard normal distribution (mean 0, stddev 1).

**Parameters:**

- `shape` (*int) -- Shape of the result.

### `numpy.randint(low: int, high: int, shape: list[int]) -> ndarray[int]`

Random integers from the half-open interval `[low, high)`.

**Parameters:**

- `low` (int) -- Inclusive lower bound.
- `high` (int) -- Exclusive upper bound. Must be greater than *low*.
- `shape` (list[int]) -- Shape of the result.

**Raises:**

- `ValueError` -- Thrown when *high* is not greater than *low*.

### `numpy.normal(loc: float, scale: float, shape: list[int]) -> ndarray[float]`

Random samples from a normal (Gaussian) distribution with the given mean and standard deviation.

**Parameters:**

- `loc` (float) -- Mean (`mu`) of the distribution.
- `scale` (float) -- Standard deviation (`sigma`) of the distribution. Must be non-negative.
- `shape` (list[int]) -- Shape of the result.

**Raises:**

- `ValueError` -- Thrown when *scale* is negative.

### `numpy.uniform(low: float, high: float, shape: list[int]) -> ndarray[float]`

Random samples from a continuous uniform distribution over `[low, high)`.

**Parameters:**

- `low` (float) -- Inclusive lower bound.
- `high` (float) -- Exclusive upper bound. Must be greater than or equal to *low*.
- `shape` (list[int]) -- Shape of the result.

**Raises:**

- `ValueError` -- Thrown when *high* is less than *low*.

### `numpy.choice(a: ndarray[T], size: int, replace: bool = True) -> ndarray[T]`

Draw *size* random samples from a 1-D ndarray *a*.

**Parameters:**

- `a` (ndarray[T]) -- Source 1-D ndarray to sample from.
- `size` (int) -- Number of samples to draw. Must be non-negative.
- `replace` (bool) -- Whether sampling is with replacement. Default `True`.
When `False`, *size* must not exceed `a.Size`.

**Raises:**

- `ValueError` -- Thrown when *a* is not 1-D, when *size* is negative,
when *a* is empty and *size* > 0, or when sampling without replacement and
*size* exceeds the source length.

### `numpy.shuffle(a: ndarray[T])`

Shuffle the contents of *a* in place along its first axis.

**Parameters:**

- `a` (ndarray[T]) -- Array to shuffle. For multi-dimensional arrays, contiguous row blocks
of `a.Shape[1..]` are permuted as units (matches NumPy semantics).

**Raises:**

- `ValueError` -- Thrown when *a* is 0-dimensional.

## ndarray

N-dimensional homogeneous array — Sharpy equivalent of `numpy.ndarray`.

!!! note
    Storage is a flat `T[]` with a row-major (C-order) stride layout. Views share the
    underlying buffer with different shape/strides/offset for zero-copy slicing and reshaping.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `ndim` | `int` | Number of dimensions (rank) of the array. |
| `size` | `int` | Total number of elements (product of shape dimensions). |
| `shape` | `list[int]` | Shape of the array as a defensive copy of the internal shape vector. |
| `strides` | `list[int]` | Strides of the array as a defensive copy of the internal stride vector. |
| `dtype` | `str` | Element type name in NumPy-style notation (e.g., \`float64\`, \`int32\`). |
| `count` | `int` | \`len(arr)\`-equivalent: the length of the first axis for non-scalar arrays. For 0-D scalars this returns 1 (matches the underlying buffer size). |

### `sum() -> float`

Sum of all elements.

### `sum(axis: int) -> ndarray[float]`

Sum along *axis*, removing that dimension.

### `min() -> float`

Minimum element.

### `min(axis: int) -> ndarray[float]`

Minimum along *axis*.

### `max() -> float`

Maximum element.

### `max(axis: int) -> ndarray[float]`

Maximum along *axis*.

### `mean() -> float`

Arithmetic mean of all elements.

### `mean(axis: int) -> ndarray[float]`

Mean along *axis*.

### `std() -> float`

Population standard deviation.

### `std(axis: int) -> ndarray[float]`

Standard deviation along *axis*.

### `var() -> float`

Population variance.

### `var(axis: int) -> ndarray[float]`

Variance along *axis*.

### `median() -> float`

Median of all elements.

### `median(axis: int) -> ndarray[float]`

Median along *axis*.

### `get_masked(mask: ndarray[bool]) -> ndarray[T]`

Return a 1-D copy containing the elements where *mask* is True.

**Parameters:**

- `mask` (ndarray[bool]) -- Boolean mask with the same shape as this array.

**Raises:**

- `ArgumentNullException` -- Thrown when *mask* is null.
- `ArgumentException` -- Thrown when the mask shape does not match this array's shape.

### `set_masked(mask: ndarray[bool], value: T)`

Assign *value* to every position where *mask* is True.

**Parameters:**

- `mask` (ndarray[bool]) -- Boolean mask with the same shape as this array.
- `value` (T) -- Scalar value written to each selected position.

### `set_masked(mask: ndarray[bool], values: ndarray[T])`

Assign values from *values* to positions where *mask* is True.
*values* must be 1-D with length equal to the number of True entries in the mask.

### `__str__() -> str`

`repr()` uses the same method. Format this array as a NumPy-style string: `array([1, 2, 3])` for 1-D arrays,
`array([[1, 2], [3, 4]])` for 2-D arrays, and so on. Arrays with more than
1000 elements are truncated, showing the first and last 3 entries per axis.

### `take(indices: list[int], axis: int = 0) -> ndarray[T]`

Take elements from this array at the positions given by *indices*.
For a 1-D source this returns a 1-D array of the selected values; for higher-rank
sources this selects entire (N-1)-D slices along *axis*.

**Parameters:**

- `indices` (list[int]) -- Integer indices into *axis*. Negative values follow Python semantics.
- `axis` (int) -- Axis along which to select. Default 0.

**Returns:** A new C-contiguous array of the selected elements.

### `put(indices: list[int], values: ndarray[T], axis: int = 0)`

Write *values* into this array at the positions given by
*indices* along *axis*. The shape of
*values* must match the shape of `take`'s result
for the same indices/axis.

### `tolist() -> object`

Convert this array to a nested `List<...>` mirror — the equivalent of NumPy's
`ndarray.tolist()`. The result type depends on rank: 1-D → `List<T>`,
2-D → `List<List<T>>`, etc. Returned as `object` because the static
nesting depth depends on the runtime rank.

### `to_array() -> list[T]`

Returns a flat copy of the array data in row-major order.

### `mat_mul(other: ndarray[T]) -> ndarray[T]`

Matrix multiplication (`@`, PEP 465). Delegates to
`matmul`, which follows
NumPy's dot semantics (inner product for 1-D operands, matrix product for 2-D).

**Parameters:**

- `other` (ndarray[T]) -- The right-hand operand.

**Raises:**

- `ArgumentNullException` -- Thrown when *other* is null.
- `TypeError` -- Thrown when the arrays are not `float64` (`double`). NumPy's linear-algebra
surface here is defined only for floating-point arrays.

### `reshape(*new_shape: int) -> ndarray[T]`

Return an array with the same data and a new shape. Returns a zero-copy view when
this array is C-contiguous; otherwise materializes a copy.

**Parameters:**

- `new_shape` (*int) -- The target shape. Exactly one dimension may be `-1`, in which case its size is
inferred from the total element count and the remaining dimensions.

**Raises:**

- `ArgumentNullException` -- Thrown when *newShape* is null.
- `ArgumentException` -- Thrown when more than one dimension is -1, or the inferred shape does not match `size`.

### `transpose() -> ndarray[T]`

Return a view of this array with axes reversed. For a 2-D array this is the matrix transpose.

### `flatten() -> ndarray[T]`

Return a 1-D copy of this array's elements in row-major order.

### `ravel() -> ndarray[T]`

Return a 1-D view of this array if it is C-contiguous; otherwise return a 1-D copy.

### `copy() -> ndarray[T]`

Return a deep copy of this array. The result owns its buffer and is C-contiguous.

### `slice(*slices: SliceSpec) -> ndarray[T]`

Produce a zero-copy view defined by per-axis slice specs. The number of slices must
equal `ndim`. The view shares the underlying buffer with this array.

**Parameters:**

- `slices` (*SliceSpec) -- Per-axis slice descriptors. Length must equal `ndim`.

**Returns:** A view of this array with the same `ndim` but possibly smaller per-axis lengths.

**Raises:**

- `ArgumentNullException` -- Thrown when *slices* is null.
- `IndexError` -- Thrown when the slice count does not match `ndim`.

### `get_row(i: int) -> ndarray[T]`

Return a 1-D view of row *i* for a 2-D array. Negative indices follow
Python semantics.

**Parameters:**

- `i` (int) -- Row index. Negative values count from the end.

**Raises:**

- `InvalidOperationException` -- Thrown when this array is not 2-dimensional.
- `IndexError` -- Thrown when *i* is out of range.

### `get_column(j: int) -> ndarray[T]`

Return a 1-D view of column *j* for a 2-D array. Negative indices follow
Python semantics.

**Parameters:**

- `j` (int) -- Column index. Negative values count from the end.

**Raises:**

- `InvalidOperationException` -- Thrown when this array is not 2-dimensional.
- `IndexError` -- Thrown when *j* is out of range.
