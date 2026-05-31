# statistics

Mathematical statistics functions.

```python
import statistics
```

## Functions

### `statistics.mean(data): Iterable[int] = > Mean(ToDoubles(data)) -> float`

Return the sample arithmetic mean of integer data.

### `statistics.mean(data): Iterable[long] = > Mean(ToDoubles(data)) -> float`

Return the sample arithmetic mean of long integer data.

### `statistics.fmean(data): Iterable[int] = > Mean(ToDoubles(data)) -> float`

Convert integer data to floats and compute the arithmetic mean.

### `statistics.fmean(data): Iterable[long] = > Mean(ToDoubles(data)) -> float`

Convert long integer data to floats and compute the arithmetic mean.

### `statistics.median(data): Iterable[int] = > Median(ToDoubles(data)) -> float`

Return the median (middle value) of integer data.

### `statistics.median(data): Iterable[long] = > Median(ToDoubles(data)) -> float`

Return the median (middle value) of long integer data.

### `statistics.median_low(data): Iterable[int] = > MedianLow(ToDoubles(data)) -> float`

Return the low median of integer data.

### `statistics.median_low(data): Iterable[long] = > MedianLow(ToDoubles(data)) -> float`

Return the low median of long integer data.

### `statistics.median_high(data): Iterable[int] = > MedianHigh(ToDoubles(data)) -> float`

Return the high median of integer data.

### `statistics.median_high(data): Iterable[long] = > MedianHigh(ToDoubles(data)) -> float`

Return the high median of long integer data.

### `statistics.stdev(data): Iterable[int] = > Stdev(ToDoubles(data)) -> float`

Return the square root of the sample variance for integer data.

### `statistics.stdev(data): Iterable[long] = > Stdev(ToDoubles(data)) -> float`

Return the square root of the sample variance for long integer data.

### `statistics.pstdev(data): Iterable[int] = > Pstdev(ToDoubles(data)) -> float`

Return the square root of the population variance for integer data.

### `statistics.pstdev(data): Iterable[long] = > Pstdev(ToDoubles(data)) -> float`

Return the square root of the population variance for long integer data.

### `statistics.variance(data): Iterable[int] = > Variance(ToDoubles(data)) -> float`

Return the sample variance of integer data.

### `statistics.variance(data): Iterable[long] = > Variance(ToDoubles(data)) -> float`

Return the sample variance of long integer data.

### `statistics.pvariance(data): Iterable[int] = > Pvariance(ToDoubles(data)) -> float`

Return the population variance of integer data.

### `statistics.pvariance(data): Iterable[long] = > Pvariance(ToDoubles(data)) -> float`

Return the population variance of long integer data.

### `statistics.mode(notnull: IEnumerable<T> data) where T : = > Mode(new Sharpy.List<T>(data)) -> T`

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
