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

### `itertools.chain(iterables: list[Iterable[T]]) -> ChainIterator[T]`

Make an iterator that returns elements from the first iterable until it is exhausted,
then proceeds to the next iterable.

**Parameters:**

- `iterables` (list[Iterable[T]]) -- One or more iterables to chain together.

**Returns:** An iterator over the concatenated elements.

```python
list(itertools.chain([1, 2], [3, 4]))    # [1, 2, 3, 4]
```

### `itertools.islice(iterable: Iterable[T], stop: int) -> IsliceIterator[T]`

Make an iterator that returns selected elements from the iterable.

**Parameters:**

- `iterable` (Iterable[T]) -- The source iterable.
- `stop` (int) -- Stop index (exclusive).

**Returns:** An iterator over the selected elements.

```python
list(itertools.islice([0, 1, 2, 3, 4], 3))    # [0, 1, 2]
```

### `itertools.islice(iterable: Iterable[T], start: int, stop: int, step: int = 1) -> IsliceIterator[T]`

Make an iterator that returns selected elements from the iterable.

**Parameters:**

- `iterable` (Iterable[T]) -- The source iterable.
- `start` (int) -- Start index.
- `stop` (int) -- Stop index (exclusive).
- `step` (int) -- Step value (default 1).

**Returns:** An iterator over the selected elements.

### `itertools.combinations(iterable: Iterable[T], r: int) -> CombinationsIterator[T]`

Return r-length combinations of elements in the iterable.

### `itertools.permutations(iterable: Iterable[T], r: int? = null) -> PermutationsIterator[T]`

Return successive r-length permutations of elements in the iterable.

### `itertools.combinations_with_replacement(iterable: Iterable[T], r: int) -> CombinationsWithReplacementIterator[T]`

Return r-length combinations of elements allowing individual elements to be repeated.

### `itertools.compress(data: Iterable[T], selectors: Iterable[bool]) -> CompressIterator[T]`

Make an iterator that filters elements from data returning only those that have a corresponding element in selectors that evaluates to true.

### `itertools.count(start: int = 0, step: int = 1) -> CountIterator`

Make an iterator that returns evenly spaced values starting with start.

### `itertools.cycle(iterable: Iterable[T]) -> Iterator[T]`

Make an iterator returning elements from the iterable and saving a copy of each.
When the iterable is exhausted, repeat from the saved copy, indefinitely.

**Parameters:**

- `iterable` (Iterable[T]) -- The source iterable to cycle over.

**Returns:** An infinite iterator cycling over the elements.

```python
list(itertools.islice(itertools.cycle([1, 2, 3]), 7))    # [1, 2, 3, 1, 2, 3, 1]
```

### `itertools.move_next() -> bool`

### `itertools.dropwhile(predicate: Func[T, bool], iterable: Iterable[T]) -> DropwhileIterator[T]`

Make an iterator that drops elements from the iterable as long as the predicate is true; afterwards, returns every element.

### `itertools.filterfalse(predicate: Func[T, bool], iterable: Iterable[T]) -> FilterfalseIterator[T]`

Make an iterator that filters elements from iterable returning only those for which the predicate is false.

### `itertools.groupby(iterable: Iterable[T], key: Func[T, TKey]? = null) -> GroupbyIterator[T, TKey]`

Make an iterator that returns consecutive keys and groups from the iterable.

### `itertools.pairwise(iterable: Iterable[T]) -> PairwiseIterator[T]`

Return successive overlapping pairs taken from the input iterable.

### `itertools.product(iterables: list[Iterable[T]]) -> ProductIterator[T]`

Cartesian product of input iterables, equivalent to nested for-loops.

### `itertools.repeat(elem: T) -> Iterator[T]`

Make an iterator that returns the element indefinitely.

### `itertools.repeat(elem: T, n: uint) -> Iterator[T]`

Make an iterator that returns the element n times.

### `itertools.move_next() -> bool`

### `itertools.starmap(func: Func[T1, T2, TResult], t2: IEnumerable<(T1,) -> StarmapIterator[T1, T2, TResult]`

Make an iterator that computes the function using arguments obtained from the iterable.

### `itertools.takewhile(predicate: Func[T, bool], iterable: Iterable[T]) -> TakewhileIterator[T]`

Make an iterator that returns elements from the iterable as long as the predicate is true.

### `itertools.zip_longest(iterables: list[Iterable[T]], fillvalue: T = default!) -> ZipLongestIterator[T]`

Make an iterator that aggregates elements from each iterable, filling missing values with fillvalue.
