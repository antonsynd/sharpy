# statistics

Exception raised for statistics-related errors, similar to Python's
`statistics.StatisticsError`.

```python
import statistics
```

## Functions

### `statistics.mean(data: Iterable[int]) -> float`

Return the sample arithmetic mean of integer data.

### `statistics.mean(data: Iterable[long]) -> float`

Return the sample arithmetic mean of long integer data.

### `statistics.fmean(data: Iterable[int]) -> float`

Convert integer data to floats and compute the arithmetic mean.

### `statistics.fmean(data: Iterable[long]) -> float`

Convert long integer data to floats and compute the arithmetic mean.

### `statistics.median(data: Iterable[int]) -> float`

Return the median (middle value) of integer data.

### `statistics.median(data: Iterable[long]) -> float`

Return the median (middle value) of long integer data.

### `statistics.median_low(data: Iterable[int]) -> float`

Return the low median of integer data.

### `statistics.median_low(data: Iterable[long]) -> float`

Return the low median of long integer data.

### `statistics.median_high(data: Iterable[int]) -> float`

Return the high median of integer data.

### `statistics.median_high(data: Iterable[long]) -> float`

Return the high median of long integer data.

### `statistics.stdev(data: Iterable[int]) -> float`

Return the square root of the sample variance for integer data.

### `statistics.stdev(data: Iterable[long]) -> float`

Return the square root of the sample variance for long integer data.

### `statistics.pstdev(data: Iterable[int]) -> float`

Return the square root of the population variance for integer data.

### `statistics.pstdev(data: Iterable[long]) -> float`

Return the square root of the population variance for long integer data.

### `statistics.variance(data: Iterable[int]) -> float`

Return the sample variance of integer data.

### `statistics.variance(data: Iterable[long]) -> float`

Return the sample variance of long integer data.

### `statistics.pvariance(data: Iterable[int]) -> float`

Return the population variance of integer data.

### `statistics.pvariance(data: Iterable[long]) -> float`

Return the population variance of long integer data.

### `statistics.mode(data: Iterable[T]) -> T`

Return the most common data point from discrete or nominal data.

### `statistics.mean(data: list[float]) -> float`

Return the sample arithmetic mean of data.

### `statistics.fmean(data: list[float]) -> float`

Convert data to floats and compute the arithmetic mean.

### `statistics.median(data: list[float]) -> float`

Return the median (middle value) of numeric data.

### `statistics.median_low(data: list[float]) -> float`

Return the low median of numeric data.

### `statistics.median_high(data: list[float]) -> float`

Return the high median of numeric data.

### `statistics.mode(data: list[T]) -> T`

Return the most common data point from discrete or nominal data.

### `statistics.stdev(data: list[float]) -> float`

Return the square root of the sample variance.

### `statistics.pstdev(data: list[float]) -> float`

Return the square root of the population variance.

### `statistics.variance(data: list[float]) -> float`

Return the sample variance of data.

### `statistics.pvariance(data: list[float]) -> float`

Return the population variance of data.

### `statistics._materialize(data: list[float]) -> list[float]`

### `statistics._materialize_sorted(data: list[float]) -> list[float]`

### `statistics._sum(values: list[float]) -> float`

### `statistics._sum_of_squared_deviations(values: list[float], m: float) -> float`
