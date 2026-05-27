# numpy

Interface implementations for `NdArray{T}` — `IEnumerable&lt;T&gt;`,
`ISized`, structural equality, and conversion helpers.

```python
import numpy
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `count` | `int` | \`len(arr)\`-equivalent: the length of the first axis for non-scalar arrays. For 0-D scalars this returns 1 (matches the underlying buffer size). |
| `start` | `int?` | Inclusive start index. \`null\` means "from the beginning". |
| `stop` | `int?` | Exclusive stop index. \`null\` means "to the end". |
| `step` | `int?` | Step between successive indices. \`null\` defaults to 1. Cannot be 0. |

## Functions

### `numpy.get_masked(mask: NdArray[bool]) -> NdArray[T]`

Return a 1-D copy containing the elements where *mask* is true.

**Parameters:**

- `mask` (NdArray[bool]) -- Boolean mask with the same shape as this array.

**Raises:**

- `ArgumentNullException` -- Thrown when *mask* is null.
- `ArgumentException` -- Thrown when the mask shape does not match this array's shape.

### `numpy.set_masked(mask: NdArray[bool], value: T)`

Assign *value* to every position where *mask* is true.

**Parameters:**

- `mask` (NdArray[bool]) -- Boolean mask with the same shape as this array.
- `value` (T) -- Scalar value written to each selected position.

### `numpy.set_masked(mask: NdArray[bool], values: NdArray[T])`

Assign values from *values* to positions where *mask* is true.
*values* must be 1-D with length equal to the number of true entries in the mask.

### `numpy.take(indices: list[int], axis: int = 0) -> NdArray[T]`

Take elements from this array at the positions given by *indices*.
For a 1-D source this returns a 1-D array of the selected values; for higher-rank
sources this selects entire (N-1)-D slices along *axis*.

**Parameters:**

- `indices` (list[int]) -- Integer indices into *axis*. Negative values follow Python semantics.
- `axis` (int) -- Axis along which to select. Default 0.

**Returns:** A new C-contiguous array of the selected elements.

### `numpy.put(indices: list[int], values: NdArray[T], axis: int = 0)`

Write *values* into this array at the positions given by
*indices* along *axis*. The shape of
*values* must match the shape of `Take`'s result
for the same indices/axis.

### `numpy.tolist() -> object`

Convert this array to a nested `List&lt;...&gt;` mirror — the equivalent of NumPy's
`ndarray.tolist()`. The result type depends on rank: 1-D → `List&lt;T&gt;`,
2-D → `List&lt;List&lt;T&gt;&gt;`, etc. Returned as `object` because the static
nesting depth depends on the runtime rank.

### `numpy.to_array() -> list[T]`

Returns a flat copy of the array data in row-major order.

### `numpy.sum(a: this NdArray<double>) -> float`

Sum of all elements.

### `numpy.sum(a: this NdArray<double>, axis: int) -> NdArray[float]`

Sum along *axis*, removing that dimension.

### `numpy.min(a: this NdArray<double>) -> float`

Minimum element.

### `numpy.min(a: this NdArray<double>, axis: int) -> NdArray[float]`

Minimum along *axis*.

### `numpy.max(a: this NdArray<double>) -> float`

Maximum element.

### `numpy.max(a: this NdArray<double>, axis: int) -> NdArray[float]`

Maximum along *axis*.

### `numpy.mean(a: this NdArray<double>) -> float`

Arithmetic mean of all elements.

### `numpy.mean(a: this NdArray<double>, axis: int) -> NdArray[float]`

Mean along *axis*.

### `numpy.std(a: this NdArray<double>) -> float`

Population standard deviation.

### `numpy.std(a: this NdArray<double>, axis: int) -> NdArray[float]`

Standard deviation along *axis*.

### `numpy.var(a: this NdArray<double>) -> float`

Population variance.

### `numpy.var(a: this NdArray<double>, axis: int) -> NdArray[float]`

Variance along *axis*.

### `numpy.median(a: this NdArray<double>) -> float`

Median of all elements.

### `numpy.median(a: this NdArray<double>, axis: int) -> NdArray[float]`

Median along *axis*.

### `numpy.reshape(new_shape: list[int]) -> NdArray[T]`

Return an array with the same data and a new shape. Returns a zero-copy view when
this array is C-contiguous; otherwise materializes a copy.

**Raises:**

- `ArgumentNullException` -- Thrown when *newShape* is null.
- `ArgumentException` -- Thrown when more than one dimension is -1, or the inferred shape does not match `Size`.

### `numpy.transpose() -> NdArray[T]`

Return a view of this array with axes reversed. For a 2-D array this is the matrix transpose.

### `numpy.flatten() -> NdArray[T]`

Return a 1-D copy of this array's elements in row-major order.

### `numpy.ravel() -> NdArray[T]`

Return a 1-D view of this array if it is C-contiguous; otherwise return a 1-D copy.

### `numpy.copy() -> NdArray[T]`

Return a deep copy of this array. The result owns its buffer and is C-contiguous.

### `numpy.range(start: int, stop: int) -> SliceSpec`

Create a slice of the form `start:stop`.

### `numpy.range(start: int, stop: int, step: int) -> SliceSpec`

Create a slice of the form `start:stop:step`.

### `numpy.slice(slices: list[SliceSpec]) -> NdArray[T]`

Produce a zero-copy view defined by per-axis slice specs. The number of slices must
equal `Ndim`. The view shares the underlying buffer with this array.

**Parameters:**

- `slices` (list[SliceSpec]) -- Per-axis slice descriptors. Length must equal `Ndim`.

**Returns:** A view of this array with the same `Ndim` but possibly smaller per-axis lengths.

**Raises:**

- `ArgumentNullException` -- Thrown when *slices* is null.
- `IndexError` -- Thrown when the slice count does not match `Ndim`.

### `numpy.get_row(i: int) -> NdArray[T]`

Return a 1-D view of row *i* for a 2-D array. Negative indices follow
Python semantics.

**Parameters:**

- `i` (int) -- Row index. Negative values count from the end.

**Raises:**

- `InvalidOperationException` -- Thrown when this array is not 2-dimensional.
- `IndexError` -- Thrown when *i* is out of range.

### `numpy.get_column(j: int) -> NdArray[T]`

Return a 1-D view of column *j* for a 2-D array. Negative indices follow
Python semantics.

**Parameters:**

- `j` (int) -- Column index. Negative values count from the end.

**Raises:**

- `InvalidOperationException` -- Thrown when this array is not 2-dimensional.
- `IndexError` -- Thrown when *j* is out of range.

### `numpy.equal(a: NdArray[T], b: NdArray[T]) -> NdArray[bool]`

Elementwise `a == b` with broadcasting, returning a boolean ndarray.

### `numpy.not_equal(a: NdArray[T], b: NdArray[T]) -> NdArray[bool]`

Elementwise `a != b` with broadcasting.

### `numpy.less(a: NdArray[T], b: NdArray[T]) -> NdArray[bool]`

Elementwise `a &lt; b` with broadcasting.

### `numpy.less_equal(a: NdArray[T], b: NdArray[T]) -> NdArray[bool]`

Elementwise `a &lt;= b` with broadcasting.

### `numpy.greater(a: NdArray[T], b: NdArray[T]) -> NdArray[bool]`

Elementwise `a &gt; b` with broadcasting.

### `numpy.greater_equal(a: NdArray[T], b: NdArray[T]) -> NdArray[bool]`

Elementwise `a &gt;= b` with broadcasting.

### `numpy.concatenate(arrays: list[NdArray[float]], axis: int = 0) -> NdArray[float]`

Join a sequence of arrays along an existing *axis*.
All input arrays must have the same shape except along *axis*.

**Parameters:**

- `arrays` (list[NdArray[float]]) -- Arrays to concatenate. Must not be empty.
- `axis` (int) -- Axis along which to concatenate. Default 0.

**Returns:** A new C-contiguous array.

### `numpy.stack(arrays: list[NdArray[float]], axis: int = 0) -> NdArray[float]`

Join a sequence of arrays along a new axis. All inputs must have the same shape.
The output has rank `ndim + 1`.

**Parameters:**

- `arrays` (list[NdArray[float]]) -- Arrays to stack.
- `axis` (int) -- Index of the new axis in the output. Default 0.

### `numpy.hstack(arrays: list[NdArray[float]]) -> NdArray[float]`

Stack arrays horizontally — along the second axis for 2-D inputs, along axis 0 for 1-D.

### `numpy.vstack(arrays: list[NdArray[float]]) -> NdArray[float]`

Stack arrays vertically — along the first axis. For 1-D inputs they are promoted
to row vectors (shape `(1, n)`) before stacking.

### `numpy.split(a: NdArray[float], indices: list[int], axis: int = 0) -> list[NdArray[float]]`

Split *a* along *axis* at the given index boundaries,
returning a list of sub-arrays. Mirrors NumPy's `numpy.split`.

**Parameters:**

- `a` (NdArray[float]) -- Input array.
- `indices` (list[int]) -- Sorted strictly-increasing list of split points.
- `axis` (int) -- Axis along which to split. Default 0.

### `numpy.where(condition: NdArray[bool], x: NdArray[float], y: NdArray[float]) -> NdArray[float]`

Return an array whose elements are taken from *x* where
*condition* is true, and *y* otherwise.
All three inputs are broadcast to a common shape.

### `numpy.clip(a: NdArray[float], min: float, max: float) -> NdArray[float]`

Clamp every element of *a* to the interval `[min, max]`.

### `numpy.sqrt(a: NdArray[float]) -> NdArray[float]`

Elementwise square root.

### `numpy.sqrt(a: float) -> float`

Scalar square root — convenience overload mirroring NumPy.

### `numpy.exp(a: NdArray[float]) -> NdArray[float]`

Elementwise natural exponential.

### `numpy.exp(a: float) -> float`

Scalar natural exponential.

### `numpy.log(a: NdArray[float]) -> NdArray[float]`

Elementwise natural logarithm.

### `numpy.log(a: float) -> float`

Scalar natural logarithm.

### `numpy.log2(a: NdArray[float]) -> NdArray[float]`

Elementwise base-2 logarithm.

### `numpy.log2(a: float) -> float`

Scalar base-2 logarithm.

### `numpy.log10(a: NdArray[float]) -> NdArray[float]`

Elementwise base-10 logarithm.

### `numpy.log10(a: float) -> float`

Scalar base-10 logarithm.

### `numpy.abs(a: NdArray[float]) -> NdArray[float]`

Elementwise absolute value.

### `numpy.abs(a: float) -> float`

Scalar absolute value.

### `numpy.sin(a: NdArray[float]) -> NdArray[float]`

Elementwise sine (radians).

### `numpy.sin(a: float) -> float`

Scalar sine.

### `numpy.cos(a: NdArray[float]) -> NdArray[float]`

Elementwise cosine (radians).

### `numpy.cos(a: float) -> float`

Scalar cosine.

### `numpy.tan(a: NdArray[float]) -> NdArray[float]`

Elementwise tangent (radians).

### `numpy.tan(a: float) -> float`

Scalar tangent.

### `numpy.arcsin(a: NdArray[float]) -> NdArray[float]`

Elementwise arcsine, returning radians.

### `numpy.arcsin(a: float) -> float`

Scalar arcsine.

### `numpy.arccos(a: NdArray[float]) -> NdArray[float]`

Elementwise arccosine, returning radians.

### `numpy.arccos(a: float) -> float`

Scalar arccosine.

### `numpy.arctan(a: NdArray[float]) -> NdArray[float]`

Elementwise arctangent, returning radians.

### `numpy.arctan(a: float) -> float`

Scalar arctangent.

### `numpy.floor(a: NdArray[float]) -> NdArray[float]`

Elementwise floor.

### `numpy.floor(a: float) -> float`

Scalar floor.

### `numpy.ceil(a: NdArray[float]) -> NdArray[float]`

Elementwise ceiling.

### `numpy.ceil(a: float) -> float`

Scalar ceiling.

### `numpy.round(a: NdArray[float], decimals: int = 0) -> NdArray[float]`

Elementwise round to *decimals* decimal places (banker's rounding).

### `numpy.round(a: float, decimals: int = 0) -> float`

Scalar round to *decimals* decimal places.

### `numpy.power(a: NdArray[float], b: NdArray[float]) -> NdArray[float]`

Elementwise `a ** b` with broadcasting (NumPy equivalent of `numpy.power`).
C# has no `**` operator, so this is exposed as a module function.

**Parameters:**

- `a` (NdArray[float]) -- Base array.
- `b` (NdArray[float]) -- Exponent array. Broadcast against *a*.

### `numpy.power(a: NdArray[float], b: float) -> NdArray[float]`

Raise every element of *a* to the scalar power *b*.

### `numpy.power(a: float, b: NdArray[float]) -> NdArray[float]`

Raise the scalar *a* elementwise to the powers in *b*.

### `numpy.sum(a: NdArray[float]) -> float`

Sum of all elements.

### `numpy.min(a: NdArray[float]) -> float`

Minimum element. Throws when *a* is empty.

### `numpy.max(a: NdArray[float]) -> float`

Maximum element. Throws when *a* is empty.

### `numpy.mean(a: NdArray[float]) -> float`

Arithmetic mean. Throws when *a* is empty.

### `numpy.var(a: NdArray[float]) -> float`

Population variance (ddof = 0). Throws when *a* is empty.

### `numpy.std(a: NdArray[float]) -> float`

Population standard deviation (ddof = 0). Throws when *a* is empty.

### `numpy.median(a: NdArray[float]) -> float`

Median of all elements. Throws when *a* is empty.

### `numpy.sum(a: NdArray[float], axis: int) -> NdArray[float]`

Sum along *axis*, removing that dimension.

### `numpy.min(a: NdArray[float], axis: int) -> NdArray[float]`

Minimum along *axis*, removing that dimension.

### `numpy.max(a: NdArray[float], axis: int) -> NdArray[float]`

Maximum along *axis*, removing that dimension.

### `numpy.mean(a: NdArray[float], axis: int) -> NdArray[float]`

Mean along *axis*, removing that dimension.

### `numpy.var(a: NdArray[float], axis: int) -> NdArray[float]`

Population variance along *axis*, removing that dimension.

### `numpy.std(a: NdArray[float], axis: int) -> NdArray[float]`

Population standard deviation along *axis*.

### `numpy.median(a: NdArray[float], axis: int) -> NdArray[float]`

Median along *axis*, removing that dimension.

### `numpy.sort(a: NdArray[float]) -> NdArray[float]`

Return a sorted copy of the input. For 1-D input this is a plain ascending sort;
for higher-rank inputs the array is flattened first.

### `numpy.argsort(a: NdArray[float]) -> NdArray[long]`

Return the indices that would sort the input — i.e. `a.Sort()` is equivalent
to `a.Take(Argsort(a))` for 1-D inputs.

### `numpy.unique(a: NdArray[float]) -> NdArray[float]`

Return the sorted unique elements of *a* as a 1-D array.

### `numpy.searchsorted(a: NdArray[float], values: NdArray[float]) -> NdArray[long]`

Find indices where elements of *values* should be inserted into
the sorted 1-D array *a* to maintain order. Uses NumPy's "left" side
convention (the first valid insertion point).

### `numpy.allclose(a: NdArray[float], b: NdArray[float], rtol: float = 1e-5, atol: float = 1e-8) -> bool`

True if every pair of elements in *a* and *b* is
close, using NumPy's mixed absolute/relative tolerance:
`|a - b| &lt;= atol + rtol * |b|`.

### `numpy.isnan(a: NdArray[float]) -> NdArray[bool]`

Elementwise `double.IsNaN`.

### `numpy.isinf(a: NdArray[float]) -> NdArray[bool]`

Elementwise `double.IsInfinity`.

### `numpy.isfinite(a: NdArray[float]) -> NdArray[bool]`

Elementwise `double.IsFinite` (neither infinite nor NaN).

### `numpy.array(data: System.Collections.Generic.IEnumerable[T]) -> NdArray[T]`

Construct a 1-D `NdArray{T}` from a flat data buffer.

**Parameters:**

- `data` (System.Collections.Generic.IEnumerable[T]) -- Source data. Length determines the shape.

**Returns:** A new 1-D ndarray owning a copy of *data*.

### `numpy.zeros(shape: list[int]) -> NdArray[float]`

Return a new ndarray of the given shape, filled with 0.0.

**Parameters:**

- `shape` (list[int]) -- Shape of the result. Each dimension must be non-negative.

### `numpy.ones(shape: list[int]) -> NdArray[float]`

Return a new ndarray of the given shape, filled with 1.0.

**Parameters:**

- `shape` (list[int]) -- Shape of the result. Each dimension must be non-negative.

### `numpy.full(shape: list[int], value: T) -> NdArray[T]`

Return a new ndarray of the given shape, filled with *value*.

**Parameters:**

- `shape` (list[int]) -- Shape of the result.
- `value` (T) -- Fill value.

### `numpy.eye(n: int) -> NdArray[float]`

Return an *n*×*n* identity matrix.

**Parameters:**

- `n` (int) -- Square matrix dimension.

### `numpy.arange(start: float, stop: float, step: float = 1.0) -> NdArray[float]`

Return evenly spaced values within a half-open interval `[start, stop)`.

**Parameters:**

- `start` (float) -- Inclusive start of the interval.
- `stop` (float) -- Exclusive end of the interval.
- `step` (float) -- Step size between successive values. Default 1.0. Cannot be zero.

### `numpy.linspace(start: float, stop: float, num: int = 50) -> NdArray[float]`

Return *num* evenly spaced samples over the closed interval `[start, stop]`.

**Parameters:**

- `start` (float) -- Inclusive start of the interval.
- `stop` (float) -- Inclusive end of the interval.
- `num` (int) -- Number of samples to generate. Must be non-negative. Default 50.

### `numpy.empty(shape: list[int]) -> NdArray[float]`

Return a new uninitialized ndarray of the given shape. Backed by a fresh
zero-initialized buffer (CLR semantics — no truly-uninitialized storage).

**Parameters:**

- `shape` (list[int]) -- Shape of the result.

### `numpy.dot(a: NdArray[float], b: NdArray[float]) -> NdArray[float]`

Dot product of two arrays — top-level alias for `NumpyLinalg.Dot`.

**Parameters:**

- `a` (NdArray[float]) -- Left operand.
- `b` (NdArray[float]) -- Right operand.

### `numpy.matmul(a: NdArray[float], b: NdArray[float]) -> NdArray[float]`

Matrix product — top-level alias for `NumpyLinalg.Matmul`.

**Parameters:**

- `a` (NdArray[float]) -- Left operand.
- `b` (NdArray[float]) -- Right operand.

### `numpy.fft(a: NdArray[float]) -> NdArray[BclComplex]`

Compute the 1-D discrete Fourier transform of a real-valued ndarray.

**Parameters:**

- `a` (NdArray[float]) -- Input 1-D ndarray of real values.

**Returns:** A 1-D ndarray of complex values with the same length as the input.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 1-dimensional.

### `numpy.fft(a: NdArray[BclComplex]) -> NdArray[BclComplex]`

Compute the 1-D discrete Fourier transform of a complex-valued ndarray.

### `numpy.ifft(a: NdArray[BclComplex]) -> NdArray[BclComplex]`

Compute the 1-D inverse discrete Fourier transform of a complex-valued ndarray.

**Parameters:**

- `a` (NdArray[BclComplex]) -- Input 1-D ndarray of complex values.

**Returns:** A 1-D ndarray of complex values with the same length as the input.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 1-dimensional.

### `numpy.fftfreq(n: int, d: float = 1.0) -> NdArray[float]`

Return the discrete Fourier transform sample frequencies for a transform of length *n*.

**Parameters:**

- `n` (int) -- Window length. Must be non-negative.
- `d` (float) -- Sample spacing (inverse of the sampling rate). Default 1.0.

**Returns:** A 1-D ndarray of length *n*. Frequency bins are arranged in
NumPy order: `[0, 1, ..., n/2-1, -n/2, ..., -1] / (d*n)` for even n,
or `[0, 1, ..., (n-1)/2, -(n-1)/2, ..., -1] / (d*n)` for odd n.

**Raises:**

- `ValueError` -- Thrown when *n* is negative.

### `numpy.dot(a: NdArray[float], b: NdArray[float]) -> NdArray[float]`

Dot product of two arrays.
  * 1-D × 1-D — inner product (scalar) returned as a 0-D ndarray.
  * 2-D × 2-D — standard matrix multiplication.
  * 2-D × 1-D — matrix-vector product.
  * 1-D × 2-D — vector-matrix product (treats vector as a row).

**Parameters:**

- `a` (NdArray[float]) -- Left operand.
- `b` (NdArray[float]) -- Right operand.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* or *b* is null.
- `ValueError` -- Thrown when shapes are incompatible or rank is unsupported.

### `numpy.matmul(a: NdArray[float], b: NdArray[float]) -> NdArray[float]`

Matrix product. For 1-D and 2-D inputs this is equivalent to `Dot`.

**Parameters:**

- `a` (NdArray[float]) -- Left operand.
- `b` (NdArray[float]) -- Right operand.

### `numpy.inv(a: NdArray[float]) -> NdArray[float]`

Compute the (multiplicative) inverse of a square matrix.

**Parameters:**

- `a` (NdArray[float]) -- A square 2-D array.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 2-D, not square, or is singular.

### `numpy.det(a: NdArray[float]) -> float`

Compute the determinant of a square 2-D array.

**Parameters:**

- `a` (NdArray[float]) -- A square 2-D array.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 2-D or not square.

### `numpy.solve(a: NdArray[float], b: NdArray[float]) -> NdArray[float]`

Solve the linear system `A x = b` for `x`.

**Parameters:**

- `a` (NdArray[float]) -- Coefficient matrix (square 2-D array).
- `b` (NdArray[float]) -- Right-hand side. Either a 1-D vector or a 2-D matrix.

**Returns:** Solution with the same rank as *b* (1-D ndarray when *b*
is 1-D, 2-D ndarray otherwise).

**Raises:**

- `ArgumentNullException` -- Thrown when *a* or *b* is null.
- `ValueError` -- Thrown when shapes are incompatible, the matrix is singular, or the rank is unsupported.

### `numpy.norm(a: NdArray[float]) -> float`

Compute the L2 (Frobenius) norm of an array.
  * 1-D — Euclidean (L2) norm.
  * 2-D — Frobenius norm.

**Parameters:**

- `a` (NdArray[float]) -- Input array.

**Raises:**

- `ArgumentNullException` -- Thrown when *a* is null.
- `ValueError` -- Thrown when *a* is not 1-D or 2-D.

### `numpy.seed(seed: int)`

Seed the thread-local random number generator with *seed*.

**Parameters:**

- `seed` (int) -- Seed value for the underlying `System.Random`.

### `numpy.rand(shape: list[int]) -> NdArray[float]`

Random samples from a uniform distribution over `[0, 1)`.

**Parameters:**

- `shape` (list[int]) -- Shape of the result. May be empty (returns a 0-D scalar array).

### `numpy.randn(shape: list[int]) -> NdArray[float]`

Random samples from the standard normal distribution (mean 0, stddev 1).

**Parameters:**

- `shape` (list[int]) -- Shape of the result.

### `numpy.randint(low: int, high: int, shape: list[int]) -> NdArray[int]`

Random integers from the half-open interval `[low, high)`.

**Parameters:**

- `low` (int) -- Inclusive lower bound.
- `high` (int) -- Exclusive upper bound. Must be greater than *low*.
- `shape` (list[int]) -- Shape of the result.

**Raises:**

- `ValueError` -- Thrown when *high* is not greater than *low*.

### `numpy.normal(loc: float, scale: float, shape: list[int]) -> NdArray[float]`

Random samples from a normal (Gaussian) distribution with the given mean and standard deviation.

**Parameters:**

- `loc` (float) -- Mean (`mu`) of the distribution.
- `scale` (float) -- Standard deviation (`sigma`) of the distribution. Must be non-negative.
- `shape` (list[int]) -- Shape of the result.

**Raises:**

- `ValueError` -- Thrown when *scale* is negative.

### `numpy.uniform(low: float, high: float, shape: list[int]) -> NdArray[float]`

Random samples from a continuous uniform distribution over `[low, high)`.

**Parameters:**

- `low` (float) -- Inclusive lower bound.
- `high` (float) -- Exclusive upper bound. Must be greater than or equal to *low*.
- `shape` (list[int]) -- Shape of the result.

**Raises:**

- `ValueError` -- Thrown when *high* is less than *low*.

### `numpy.choice(a: NdArray[T], size: int, replace: bool = true) -> NdArray[T]`

Draw *size* random samples from a 1-D ndarray *a*.

**Parameters:**

- `a` (NdArray[T]) -- Source 1-D ndarray to sample from.
- `size` (int) -- Number of samples to draw. Must be non-negative.
- `replace` (bool) -- Whether sampling is with replacement. Default `true`.
When `false`, *size* must not exceed `a.Size`.

**Raises:**

- `ValueError` -- Thrown when *a* is not 1-D, when *size* is negative,
when *a* is empty and *size* &gt; 0, or when sampling without replacement and
*size* exceeds the source length.

### `numpy.shuffle(a: NdArray[T])`

Shuffle the contents of *a* in place along its first axis.

**Parameters:**

- `a` (NdArray[T]) -- Array to shuffle. For multi-dimensional arrays, contiguous row blocks
of `a.Shape[1..]` are permuted as units (matches NumPy semantics).

**Raises:**

- `ValueError` -- Thrown when *a* is 0-dimensional.

## ndarray

N-dimensional homogeneous array — Sharpy equivalent of `numpy.ndarray`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `ndim` | `int` | Number of dimensions (rank) of the array. |
| `size` | `int` | Total number of elements (product of shape dimensions). |
