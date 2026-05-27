# itertools

Itertools module — tools for creating iterators.

```python
import itertools
```

## Functions

### `itertools.accumulate(iterable: Iterable[T], func: Func[T, T, T]? = null) -> AccumulateIterator[T]`

Make an iterator that returns accumulated sums (or accumulated results of a binary function).

### `itertools.accumulate(iterable: Iterable[T], func: Func[T, T, T]?, initial: T) -> AccumulateIterator[T]`

Make an iterator that returns accumulated results with an initial value.

### `itertools.combinations(iterable: Iterable[T], r: int) -> CombinationsIterator[T]`

Return r-length combinations of elements in the iterable.

### `itertools.permutations(iterable: Iterable[T], r: int? = null) -> PermutationsIterator[T]`

Return successive r-length permutations of elements in the iterable.

### `itertools.combinations_with_replacement(iterable: Iterable[T], r: int) -> CombinationsWithReplacementIterator[T]`

Return r-length combinations of elements allowing individual elements to be repeated.

### `itertools.product(iterables: list[Iterable[T]]) -> ProductIterator[T]`

Cartesian product of input iterables, equivalent to nested for-loops.

### `itertools.starmap(func: Func[T1, T2, TResult], t2: IEnumerable<(T1,) -> StarmapIterator[T1, T2, TResult]`

Make an iterator that computes the function using arguments obtained from the iterable.

### `itertools.groupby(iterable: Iterable[T], key: Func[T, TKey]? = null) -> GroupbyIterator[T, TKey]`

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

### `itertools.count(start: int = 0, step: int = 1) -> System.Collections.Generic.IEnumerable[int]`

Make an iterator that returns evenly spaced values starting with number start.

### `itertools.repeat(elem: T, n: int = -1) -> System.Collections.Generic.IEnumerable[T]`

Make an iterator that returns object over and over again, optionally limited by n times.

### `itertools.cycle(iterable: list[T]) -> System.Collections.Generic.IEnumerable[T]`

Make an iterator returning elements from the iterable and saving a copy of each.

### `itertools.compress(data: list[T], selectors: list[bool]) -> System.Collections.Generic.IEnumerable[T]`

Make an iterator that filters elements from data returning only those that have a corresponding element in selectors that evaluates to True.

### `itertools.dropwhile(predicate: global::System.Func<T, bool>, iterable: list[T]) -> System.Collections.Generic.IEnumerable[T]`

Make an iterator that drops elements from the iterable as long as the predicate is true; afterwards, returns every element.

### `itertools.takewhile(predicate: global::System.Func<T, bool>, iterable: list[T]) -> System.Collections.Generic.IEnumerable[T]`

Make an iterator that returns elements from the iterable as long as the predicate is true.

### `itertools.filterfalse(predicate: global::System.Func<T, bool>, iterable: list[T]) -> System.Collections.Generic.IEnumerable[T]`

Make an iterator that filters elements from iterable returning only those for which the predicate is false.

### `itertools.islice(iterable: list[T], stop: int) -> System.Collections.Generic.IEnumerable[T]`

Make an iterator that returns selected elements from the iterable.

### `itertools.islice_range(iterable: list[T], start: int, stop: int, step: int = 1) -> System.Collections.Generic.IEnumerable[T]`

Make an iterator that returns selected elements from the iterable with start, stop, and step.
