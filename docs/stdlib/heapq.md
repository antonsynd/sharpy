# heapq

Heap queue algorithm (priority queue), similar to Python's heapq module.
Implements a min-heap using a list as the underlying storage.

```python
import heapq
```

## Functions

### `heapq.heappush(heap: IList[T], item: T)`

Push  onto , maintaining
the min-heap invariant.

**Parameters:**

- `heap` (IList[T]) -- The heap list.
- `item` (T) -- The item to push.

```python
h = []
heapq.heappush(h, 3)
heapq.heappush(h, 1)
heapq.heappush(h, 2)    # h[0] is now 1
```

### `heapq.heappop(heap: IList[T]) -> T`

Pop and return the smallest item from ,
maintaining the min-heap invariant.

**Parameters:**

- `heap` (IList[T]) -- The heap list.

**Returns:** The smallest element.

```python
h = [1, 2, 3]
heapq.heappop(h)    # returns 1
```

**Raises:**

- `IndexError` -- Thrown if the heap is empty.

### `heapq.heapify(x: IList[T])`

Transform  into a min-heap, in-place, in O(n) time
using Floyd's algorithm.

**Parameters:**

- `x` (IList[T]) -- The list to heapify.

```python
x = [5, 3, 1, 4, 2]
heapq.heapify(x)    # x is now a valid min-heap
```

### `heapq.heapreplace(heap: IList[T], item: T) -> T`

Pop and return the smallest item from , then push
. More efficient than separate pop and push.

**Parameters:**

- `heap` (IList[T]) -- The heap list.
- `item` (T) -- The item to push after popping.

**Returns:** The smallest element that was in the heap.

**Raises:**

- `IndexError` -- Thrown if the heap is empty.

### `heapq.heappushpop(heap: IList[T], item: T) -> T`

Push  onto , then pop and
return the smallest item. More efficient than separate push and pop.

**Parameters:**

- `heap` (IList[T]) -- The heap list.
- `item` (T) -- The item to push before popping.

**Returns:** The smallest element after the push.

### `heapq.nlargest(n: int, iterable: IList[T]) -> list[T]`

Return the  largest elements from
, in descending order.

**Parameters:**

- `n` (int) -- The number of largest elements to return.
- `iterable` (IList[T]) -- The collection to search.

**Returns:** A list of the  largest elements.

```python
heapq.nlargest(3, [3, 1, 4, 1, 5, 9, 2, 6])    # [9, 6, 5]
```

### `heapq.nsmallest(n: int, iterable: IList[T]) -> list[T]`

Return the  smallest elements from
, in ascending order.

**Parameters:**

- `n` (int) -- The number of smallest elements to return.
- `iterable` (IList[T]) -- The collection to search.

**Returns:** A list of the  smallest elements.

```python
heapq.nsmallest(3, [3, 1, 4, 1, 5, 9, 2, 6])    # [1, 1, 2]
```

### `heapq.merge(a: list[T], b: list[T]) -> Iterable[T]`

Merge two sorted lists into a single sorted sequence, yielding
elements lazily in ascending order.

**Parameters:**

- `a` (list[T]) -- First sorted list.
- `b` (list[T]) -- Second sorted list.

**Returns:** An  yielding elements in sorted order.

### `heapq.merge(a: list[T], b: list[T], c: list[T]) -> Iterable[T]`

Merge three sorted lists into a single sorted sequence.

### `heapq.merge(iterables: list[list[T]]) -> Iterable[T]`

Merge multiple sorted lists into a single sorted sequence.

### `heapq.merge(a: list[T], b: list[T], key: Func[T, TKey], reverse: bool = false) -> Iterable[T]`

Merge two sorted lists with a key function and/or reverse flag.
Inputs must be pre-sorted according to the key/reverse order.

### `heapq.merge(a: list[T], b: list[T], c: list[T], key: Func[T, TKey], reverse: bool = false) -> Iterable[T]`

Merge three sorted lists with a key function and/or reverse flag.
Inputs must be pre-sorted according to the key/reverse order.

### `heapq.merge(iterables: list[list[T]], reverse: bool = false) -> Iterable[T]`

Merge an array of sorted lists into a single sorted sequence,
with an optional  flag.
Inputs must be pre-sorted in the corresponding order.

**Parameters:**

- `iterables` (list[list[T]]) -- The array of sorted lists to merge.
- `reverse` (bool) -- If true, merge in descending order (inputs must be descending).

**Returns:** An  yielding elements in sorted order.

### `heapq.merge(iterables: list[list[T]], key: Func[T, TKey], reverse: bool = false) -> Iterable[T]`

Merge an array of sorted lists into a single sorted sequence,
comparing elements by a  function with an
optional  flag.
Inputs must be pre-sorted according to the key/reverse order.

**Parameters:**

- `iterables` (list[list[T]]) -- The array of sorted lists to merge.
- `key` (Func[T, TKey]) -- A function that extracts a comparison key from each element.
- `reverse` (bool) -- If true, merge in descending key order (inputs must be descending).

**Returns:** An  yielding elements in sorted order.

### `heapq.merge(a: list[T], b: list[T], reverse: bool) -> Iterable[T]`

Merge two sorted lists in reverse order (descending).
Inputs must be pre-sorted in descending order.

### `heapq.merge(a: list[T], b: list[T], c: list[T], reverse: bool) -> Iterable[T]`

Merge three sorted lists in reverse order (descending).
Inputs must be pre-sorted in descending order.
