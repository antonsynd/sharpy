# heapq

Heap queue (priority queue) algorithm.

```python
import heapq
```

## Functions

### `heapq.merge(a: list[T], b: list[T]) -> Iterable[T]`

Merge two sorted inputs into a single sorted output.

### `heapq.merge(a: list[T], b: list[T], c: list[T]) -> Iterable[T]`

Merge three sorted inputs into a single sorted output.

### `heapq.merge(iterables: list[list[T]]) -> Iterable[T]`

Merge multiple sorted inputs into a single sorted output.

### `heapq.merge(a: list[T], b: list[T], key: (T) -> TKey, reverse: bool = False) -> Iterable[T]`

Merge two sorted inputs into a single sorted output, using a key function.

### `heapq.merge(a: list[T], b: list[T], c: list[T], key: (T) -> TKey, reverse: bool = False) -> Iterable[T]`

Merge three sorted inputs into a single sorted output, using a key function.

### `heapq.merge(iterables: list[list[T]], reverse: bool = False) -> Iterable[T]`

Merge multiple sorted inputs into a single sorted output, with optional reverse ordering.

### `heapq.merge(iterables: list[list[T]], key: (T) -> TKey, reverse: bool = False) -> Iterable[T]`

Merge multiple sorted inputs into a single sorted output, using a key function.

### `heapq.merge(a: list[T], b: list[T], reverse: bool) -> Iterable[T]`

Merge two sorted inputs into a single sorted output, with optional reverse ordering.

### `heapq.merge(a: list[T], b: list[T], c: list[T], reverse: bool) -> Iterable[T]`

Merge three sorted inputs into a single sorted output, with optional reverse ordering.

### `heapq.nlargest(n: int, iterable: IList[T]) -> list[T]`

Find the n largest elements in a dataset, accepting any IList.

### `heapq.nsmallest(n: int, iterable: IList[T]) -> list[T]`

Find the n smallest elements in a dataset, accepting any IList.

### `heapq.heappush(heap: list[T], item: T)`

Push item onto heap, maintaining the heap invariant.

### `heapq.heappop(heap: list[T]) -> T`

Pop the smallest item off the heap, maintaining the heap invariant.

### `heapq.heapify(x: list[T])`

Transform list into a heap, in-place, in O(len(x)) time.

### `heapq.heapreplace(heap: list[T], item: T) -> T`

Pop and return the smallest item, and push the new item.

### `heapq.heappushpop(heap: list[T], item: T) -> T`

Push item on the heap, then pop and return the smallest item.

### `heapq.nlargest(n: int, iterable: list[T]) -> list[T]`

Find the n largest elements in a dataset.

### `heapq.nsmallest(n: int, iterable: list[T]) -> list[T]`

Find the n smallest elements in a dataset.

### `heapq._sift_up(heap: list[T], index: int)`

Bubble element at index up to restore the heap invariant.

### `heapq._sift_down(heap: list[T], index: int)`

Push element at index down to restore the heap invariant.
