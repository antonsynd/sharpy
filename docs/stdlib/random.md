# random

Generate pseudo-random numbers with various distributions.

```python
import random
```

## Functions

### `random.seed(s: int)`

Initialize the random number generator with the given seed.

### `random.next_double() -> float`

Return the next random floating point number in the range [0.0, 1.0).

### `random.randint(a: int, b: int) -> int`

Return a random integer in the range [a, b], including both end points.

### `random.uniform(a: float, b: float) -> float`

Return a random floating point number in the range [a, b].

### `random.choice(seq: list[T]) -> T`

Return a random element from the non-empty sequence seq.

### `random.shuffle(x: list[T])`

Shuffle the sequence x in place.

### `random.randrange(stop: int) -> int`

Return a randomly selected element from range(stop).

### `random.randrange(start: int, stop: int, step: int = 1) -> int`

Return a randomly selected element from range(start, stop, step).

### `random.gauss(mu: float, sigma: float) -> float`

Return a random floating point number with Gaussian distribution.

### `random.getrandbits(k: int) -> int`

Return an int with k random bits.

### `random.choices(population: list[T], k: int = 1) -> list[T]`

Return a list of k elements chosen from population with replacement.

### `random.sample(population: list[T], k: int) -> list[T]`

Return k unique elements from population without replacement.
