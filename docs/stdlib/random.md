# random

```python
import random
```

## Functions

### `random.choice(seq: list[T]) -> T`

Return a randomly selected element from a non-empty array.

**Raises:**

- `IndexError` -- Thrown if the sequence is null or empty.

### `random.choice(seq: IList[T]) -> T`

Return a randomly selected element from a non-empty sequence.

**Raises:**

- `IndexError` -- Thrown if the sequence is null or empty.

### `random.shuffle(x: IList[T])`

Shuffle the sequence x in place.

**Raises:**

- `TypeError` -- Thrown if *x* is null.

### `random.randrange(stop: int) -> int`

Return a randomly-selected element from range(stop).

### `random.randrange(start: int, stop: int) -> int`

Return a randomly-selected element from range(start, stop).

### `random.choices(population: IList[T], weights: IList[float]? = null, k: int = 1) -> list[T]`

Return a k sized list of elements chosen from the population with replacement,
optionally weighted.

**Raises:**

- `ValueError` -- Thrown if population is empty, k is negative, weights count mismatches, or total weight is non-positive.

### `random.sample(population: IList[T], k: int) -> list[T]`

Return a k length list of unique elements chosen from the population sequence.

**Raises:**

- `TypeError` -- Thrown if *population* is null.
- `ValueError` -- Thrown if *k* is negative or larger than the population.

### `random.seed(s: int)`

### `random.next_double() -> float`

### `random.randint(a: int, b: int) -> int`

### `random.uniform(a: float, b: float) -> float`

### `random.choice(seq: list[T]) -> T`

### `random.shuffle(x: list[T])`

### `random.randrange(start: int, stop: int, step: int = 1) -> int`

### `random.gauss(mu: float, sigma: float) -> float`

### `random.getrandbits(k: int) -> int`
