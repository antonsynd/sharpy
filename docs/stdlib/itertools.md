# itertools

Functions creating iterators for efficient looping.

```python
import itertools
```

## Functions

### `itertools.accumulate(iterable: Iterable[T], func: (T, T) -> T | None = None) -> AccumulateIterator[T]`

Make an iterator that returns accumulated sums (or accumulated results of a binary function).

### `itertools.accumulate(iterable: Iterable[T], func: (T, T) -> T | None, initial: T) -> AccumulateIterator[T]`

Make an iterator that returns accumulated results with an initial value.

### `itertools.combinations(iterable: Iterable[T], r: int) -> CombinationsIterator[T]`

Return r-length combinations of elements in the iterable.

### `itertools.permutations(iterable: Iterable[T], r: int | None = None) -> PermutationsIterator[T]`

Return successive r-length permutations of elements in the iterable.

### `itertools.combinations_with_replacement(iterable: Iterable[T], r: int) -> CombinationsWithReplacementIterator[T]`

Return r-length combinations of elements allowing individual elements to be repeated.

### `itertools.product(iterables: list[Iterable[T]]) -> ProductIterator[T]`

Cartesian product of input iterables, equivalent to nested for-loops.

### `itertools.starmap(func: (T1, T2) -> TResult, iterable: Iterable[tuple[T1, T2]]) -> StarmapIterator[T1, T2, TResult]`

Make an iterator that computes the function using arguments obtained from the iterable.

### `itertools.groupby(iterable: Iterable[T], key: (T) -> TKey | None = None) -> GroupbyIterator[T, TKey]`

Make an iterator that returns consecutive keys and groups from the iterable.

### `itertools.repeat(elem: T, n: uint) -> Iterable[T]`

Make an iterator that returns object over and over again, limited by n times.

### `itertools.chain(iterables: list[Iterable[T]]) -> ChainIterator[T]`

Make an iterator that returns elements from the first iterable until it is exhausted,
then proceeds to the next iterable.

**Parameters:**

- `iterables` (list[Iterable[T]]) -- One or more iterables to chain together.

**Returns:** An iterator over the concatenated elements.

```python
list(itertools.chain([1, 2], [3, 4]))    # [1, 2, 3, 4]
```

### `itertools.zip_longest(iterables: list[Iterable[T]], fillvalue: T = default!) -> ZipLongestIterator[T]`

Make an iterator that aggregates elements from each iterable, filling missing values with fillvalue.

### `itertools.count(start: int = 0, step: int = 1) -> Iterable[int]`

Make an iterator that returns evenly spaced values starting with number start.

### `itertools.repeat(elem: T, n: int = -1) -> Iterable[T]`

Make an iterator that returns object over and over again, optionally limited by n times.

### `itertools.cycle(iterable: list[T]) -> Iterable[T]`

Make an iterator returning elements from the iterable and saving a copy of each.

### `itertools.compress(data: list[T], selectors: list[bool]) -> Iterable[T]`

Make an iterator that filters elements from data returning only those that have a corresponding element in selectors that evaluates to True.

### `itertools.dropwhile(predicate: (T) -> bool, iterable: list[T]) -> Iterable[T]`

Make an iterator that drops elements from the iterable as long as the predicate is True; afterwards, returns every element.

### `itertools.takewhile(predicate: (T) -> bool, iterable: list[T]) -> Iterable[T]`

Make an iterator that returns elements from the iterable as long as the predicate is True.

### `itertools.filterfalse(predicate: (T) -> bool, iterable: list[T]) -> Iterable[T]`

Make an iterator that filters elements from iterable returning only those for which the predicate is False.

### `itertools.islice(iterable: list[T], stop: int) -> Iterable[T]`

Make an iterator that returns selected elements from the iterable.

### `itertools.islice_range(iterable: list[T], start: int, stop: int, step: int = 1) -> Iterable[T]`

Make an iterator that returns selected elements from the iterable with start, stop, and step.

### `itertools.accumulate(iterable: list[int]) -> Iterable[int]`

Make an iterator that returns accumulated sums.

### `itertools.accumulate(iterable: list[T], func: (T, T) -> T) -> Iterable[T]`

Make an iterator that returns accumulated results of a binary function.

### `itertools.accumulate(iterable: list[T], func: (T, T) -> T, initial: T) -> Iterable[T]`

Make an iterator that returns accumulated results of a binary function, starting with an initial value.

### `itertools.chain(first: list[T], second: list[T]) -> Iterable[T]`

Make an iterator that returns elements from the first iterable until it is exhausted, then proceeds to the next iterable.

### `itertools.chain(first: list[T], second: list[T], third: list[T]) -> Iterable[T]`

Make an iterator that returns elements from each iterable in turn until all are exhausted.

### `itertools.chain_from_iterable(iterables: list[list[T]]) -> Iterable[T]`

Make an iterator that chains all iterables from a single list of iterables.

### `itertools.starmap(func: (T1, T2) -> R, iterable: list[tuple[T1, T2]]) -> Iterable[R]`

Make an iterator that computes the function using arguments obtained from the iterable.

### `itertools.combinations(iterable: list[T], r: int) -> Iterable[list[T]]`

Return successive r-length combinations of elements in the iterable.

### `itertools.permutations(iterable: list[T], r: int = -1) -> Iterable[list[T]]`

Return successive r-length permutations of elements in the iterable. A negative r means full length.

### `itertools.combinations_with_replacement(iterable: list[T], r: int) -> Iterable[list[T]]`

Return successive r-length combinations of elements in the iterable allowing individual elements to be repeated.
