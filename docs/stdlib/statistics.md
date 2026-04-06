# statistics

Mathematical statistics functions, similar to Python's `statistics` module.

```python
import statistics
```

## Functions

### `statistics.mean(data: Iterable[float]) -> float`

Return the arithmetic mean (average) of *data*.

**Parameters:**

- `data` (Iterable[float]) -- A sequence of numeric values.

**Returns:** The arithmetic mean.

```python
statistics.mean([1, 2, 3, 4, 5])    # 3.0
```

**Raises:**

- `StatisticsError` -- Thrown if *data* is empty.

### `statistics.mean(data: Iterable[int]) -> float`

Return the arithmetic mean of *data* (integer overload).

### `statistics.mean(data: Iterable[long]) -> float`

Return the arithmetic mean of *data* (long overload).

### `statistics.fmean(data: Iterable[float]) -> float`

Return the arithmetic mean of *data* as a float.
For this implementation, equivalent to `Mean(IEnumerable{double})`.

**Parameters:**

- `data` (Iterable[float]) -- A sequence of numeric values.

**Returns:** The arithmetic mean.

**Raises:**

- `StatisticsError` -- Thrown if *data* is empty.

### `statistics.fmean(data: Iterable[int]) -> float`

Return the arithmetic mean of *data* as a float (integer overload).

### `statistics.fmean(data: Iterable[long]) -> float`

Return the arithmetic mean of *data* as a float (long overload).

### `statistics.median(data: Iterable[float]) -> float`

Return the median (middle value) of *data*.
When the number of data points is even, the median is the average of the
two middle values.

**Parameters:**

- `data` (Iterable[float]) -- A sequence of numeric values.

**Returns:** The median value.

```python
statistics.median([1, 2, 3, 4])    # 2.5
statistics.median([1, 2, 3])       # 2.0
```

**Raises:**

- `StatisticsError` -- Thrown if *data* is empty.

### `statistics.median(data: Iterable[int]) -> float`

Return the median of *data* (integer overload).

### `statistics.median(data: Iterable[long]) -> float`

Return the median of *data* (long overload).

### `statistics.median_low(data: Iterable[float]) -> float`

Return the low median of *data*.
When the number of data points is even, the low median is the smaller
of the two middle values.

**Parameters:**

- `data` (Iterable[float]) -- A sequence of numeric values.

**Returns:** The low median value.

**Raises:**

- `StatisticsError` -- Thrown if *data* is empty.

### `statistics.median_low(data: Iterable[int]) -> float`

Return the low median of *data* (integer overload).

### `statistics.median_low(data: Iterable[long]) -> float`

Return the low median of *data* (long overload).

### `statistics.median_high(data: Iterable[float]) -> float`

Return the high median of *data*.
When the number of data points is even, the high median is the larger
of the two middle values.

**Parameters:**

- `data` (Iterable[float]) -- A sequence of numeric values.

**Returns:** The high median value.

**Raises:**

- `StatisticsError` -- Thrown if *data* is empty.

### `statistics.median_high(data: Iterable[int]) -> float`

Return the high median of *data* (integer overload).

### `statistics.median_high(data: Iterable[long]) -> float`

Return the high median of *data* (long overload).

### `statistics.mode(data: Iterable[T]) -> T`

Return the single most common data point from *data*.
If there are multiple modes (tied), the first encountered value wins.

**Parameters:**

- `data` (Iterable[T]) -- A sequence of values.

**Returns:** The most common value.

```python
statistics.mode([1, 1, 2, 3])    # 1
```

**Raises:**

- `StatisticsError` -- Thrown if *data* is empty.

### `statistics.stdev(data: Iterable[float]) -> float`

Return the sample standard deviation (the square root of the sample
variance) of *data*. Uses `n-1` in the denominator.

**Parameters:**

- `data` (Iterable[float]) -- A sequence of at least two numeric values.

**Returns:** The sample standard deviation.

```python
statistics.stdev([2, 4, 4, 4, 5, 5, 7, 9])    # ~2.138
```

**Raises:**

- `StatisticsError` -- Thrown if *data* has fewer than 2 elements.

### `statistics.stdev(data: Iterable[int]) -> float`

Return the sample standard deviation (integer overload).

### `statistics.stdev(data: Iterable[long]) -> float`

Return the sample standard deviation (long overload).

### `statistics.pstdev(data: Iterable[float]) -> float`

Return the population standard deviation (the square root of the
population variance) of *data*. Uses `n` in the
denominator.

**Parameters:**

- `data` (Iterable[float]) -- A sequence of numeric values.

**Returns:** The population standard deviation.

**Raises:**

- `StatisticsError` -- Thrown if *data* is empty.

### `statistics.pstdev(data: Iterable[int]) -> float`

Return the population standard deviation (integer overload).

### `statistics.pstdev(data: Iterable[long]) -> float`

Return the population standard deviation (long overload).

### `statistics.variance(data: Iterable[float]) -> float`

Return the sample variance of *data*. Uses `n-1`
in the denominator (Bessel's correction).

**Parameters:**

- `data` (Iterable[float]) -- A sequence of at least two numeric values.

**Returns:** The sample variance.

**Raises:**

- `StatisticsError` -- Thrown if *data* has fewer than 2 elements.

### `statistics.variance(data: Iterable[int]) -> float`

Return the sample variance (integer overload).

### `statistics.variance(data: Iterable[long]) -> float`

Return the sample variance (long overload).

### `statistics.pvariance(data: Iterable[float]) -> float`

Return the population variance of *data*. Uses `n`
in the denominator.

**Parameters:**

- `data` (Iterable[float]) -- A sequence of numeric values.

**Returns:** The population variance.

**Raises:**

- `StatisticsError` -- Thrown if *data* is empty.

### `statistics.pvariance(data: Iterable[int]) -> float`

Return the population variance (integer overload).

### `statistics.pvariance(data: Iterable[long]) -> float`

Return the population variance (long overload).
