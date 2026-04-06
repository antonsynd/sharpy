# random

Pseudo-random number generators for various distributions, similar to Python's random module.

```python
import random
```

## Functions

### `random.seed(seed: int)`

Initialize the random number generator with a seed.

**Parameters:**

- `seed` (int) -- The seed value

### `random.next_double() -> float`

Return a random floating point number in the range [0.0, 1.0).
Renamed from Random() to NextDouble() to avoid CS0542 (member name
matching enclosing type). Matches System.Random.NextDouble() convention.

**Returns:** A random double in [0.0, 1.0).

### `random.randint(a: int, b: int) -> int`

Return a random integer N such that a <= N <= b.

**Parameters:**

- `a` (int) -- The lower bound (inclusive).
- `b` (int) -- The upper bound (inclusive).

**Returns:** A random integer between  and .

```python
random.randint(1, 6)    # 4 (random die roll)
random.randint(0, 1)    # 0 or 1
```

### `random.uniform(a: float, b: float) -> float`

Return a random floating point number N such that a <= N <= b for a <= b
and b <= N <= a for b < a.

**Parameters:**

- `a` (float) -- One end of the range.
- `b` (float) -- The other end of the range.

**Returns:** A random double between  and .

### `random.choice(seq: IList[T]) -> T`

Return a randomly selected element from a non-empty sequence.

**Parameters:**

- `seq` (IList[T]) -- A non-empty sequence to choose from.

**Returns:** A randomly selected element.

```python
random.choice([1, 2, 3])       # 2 (random)
random.choice(["a", "b"])      # "a" or "b"
```

**Raises:**

- `IndexError` -- Thrown if the sequence is null or empty.

### `random.choice(seq: list[T]) -> T`

Return a randomly selected element from a non-empty sequence.

**Raises:**

- `IndexError` -- Thrown if the sequence is null or empty.

### `random.shuffle(x: IList[T])`

Shuffle the sequence x in place.

**Parameters:**

- `x` (IList[T]) -- The sequence to shuffle.

```python
items = [1, 2, 3, 4, 5]
random.shuffle(items)    # items is now shuffled in place
```

**Raises:**

- `TypeError` -- Thrown if  is null.

### `random.randrange(stop: int) -> int`

Return a randomly-selected element from range(stop) or range(start, stop, step).

**Parameters:**

- `stop` (int) -- The exclusive upper bound.

**Returns:** A random integer from range(0, ).

### `random.randrange(start: int, stop: int) -> int`

Return a randomly-selected element from range(start, stop).

**Parameters:**

- `start` (int) -- The inclusive lower bound.
- `stop` (int) -- The exclusive upper bound.

**Returns:** A random integer from range(, ).

### `random.randrange(start: int, stop: int, step: int) -> int`

Return a randomly-selected element from range(start, stop, step).

**Parameters:**

- `start` (int) -- The inclusive lower bound.
- `stop` (int) -- The exclusive upper bound.
- `step` (int) -- The step between elements.

**Returns:** A random integer from range(, , ).

**Raises:**

- `ValueError` -- Thrown if step is zero or the range is empty.

### `random.gauss(mu: float, sigma: float) -> float`

Gaussian distribution. mu is the mean, and sigma is the standard deviation.
Uses the Box-Muller transform.

**Parameters:**

- `mu` (float) -- The mean.
- `sigma` (float) -- The standard deviation.

**Returns:** A random number drawn from the Gaussian distribution.

### `random.getrandbits(k: int) -> int`

Returns a non-negative integer with k random bits.

**Parameters:**

- `k` (int) -- The number of random bits (0 to 30).

**Returns:** A non-negative integer with  random bits.

**Raises:**

- `ValueError` -- Thrown if  is negative or greater than 30.

### `random.choices(population: IList[T], weights: IList[float]? = null, k: int = 1) -> list[T]`

Return a k sized list of elements chosen from the population with replacement,
optionally weighted.

**Parameters:**

- `population` (IList[T]) -- The population to choose from.
- `weights` (IList[float]?) -- Optional weights for each element.
- `k` (int) -- The number of elements to choose.

**Returns:** A list of  randomly chosen elements.

**Raises:**

- `ValueError` -- Thrown if population is empty, k is negative, weights count mismatches, or total weight is non-positive.

### `random.sample(population: IList[T], k: int) -> list[T]`

Return a k length list of unique elements chosen from the population sequence.

**Parameters:**

- `population` (IList[T]) -- The population to sample from.
- `k` (int) -- The number of unique elements to select.

**Returns:** A list of  unique elements from .

**Raises:**

- `TypeError` -- Thrown if  is null.
- `ValueError` -- Thrown if  is negative or larger than the population.
