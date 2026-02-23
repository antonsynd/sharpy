# Generators

A generator function is any function whose body contains a `yield` statement.
Instead of computing a single return value, generators produce a sequence of
values lazily — each `yield` suspends the function and emits one element to the
caller.

```python
def count_up(n: int) -> int:
    i = 0
    while i < n:
        yield i
        i += 1

def main():
    for x in count_up(3):
        print(x)  # 0, 1, 2
```

## Syntax

```
yield_stmt ::= 'yield' expression
             | 'yield' 'from' expression
```

### `yield expression`

Produces a single value to the caller and suspends the generator.

```python
def squares(n: int) -> int:
    for i in range(n):
        yield i * i
```

### `yield from expression`

Delegates to another iterable, yielding all of its values in order before
continuing with the current generator.

```python
def inner() -> int:
    yield 0
    yield 1

def outer() -> int:
    yield from inner()
    yield 2
    yield 3

# Produces: 0, 1, 2, 3
```

## Return Type Annotation

The return type annotation on a generator specifies the **element type**, not
the collection type. The compiler wraps it automatically:

```python
# Annotate with element type T — compiler produces IEnumerable<T>
def fibonacci(n: int) -> int:
    a = 0
    b = 1
    for i in range(n):
        yield a
        a, b = b, a + b
```

| Context | Annotation | Emitted C# return type |
|---------|-----------|------------------------|
| Standalone function | `-> T` | `IEnumerable<T>` |
| `__iter__` method | `-> T` | `IEnumerator<T>` |
| `__reversed__` method | `-> T` | `IEnumerator<T>` |

## Early Termination

A bare `return` (without a value) terminates the generator early:

```python
def take(n: int) -> int:
    i = 0
    while True:
        if i >= n:
            return  # stops iteration
        yield i
        i += 1
```

Returning a value from a generator is forbidden (see [Restrictions](#restrictions)).

## Generator `__iter__` and `__reversed__`

Using `yield` inside `__iter__` makes a class iterable without writing a
separate iterator class:

```python
class Countdown:
    values: list[int]

    def __init__(self, values: list[int]):
        self.values = values

    def __iter__(self) -> int:
        for v in self.values:
            yield v
```

Similarly, `yield` inside `__reversed__` provides reverse iteration:

```python
class Range:
    start: int
    end: int

    def __init__(self, start: int, end: int):
        self.start = start
        self.end = end

    def __iter__(self) -> int:
        i = self.start
        while i < self.end:
            yield i
            i += 1

    def __reversed__(self) -> int:
        i = self.end - 1
        while i >= self.start:
            yield i
            i -= 1
```

When `__iter__` contains `yield`, the compiler synthesizes `IEnumerable<T>` on
the class (reported via SPY1001 info diagnostic). When `__reversed__` contains
`yield`, `IReverseEnumerable<T>` is synthesized.

## Restrictions

### No `yield` inside `__next__`

The `__next__` method is part of the *explicit* iterator protocol (manual state
management). Combining it with `yield` (which generates a state machine
automatically) is contradictory.

```python
class Bad:
    def __next__(self) -> int:
        yield 1  # ERROR: SPY0268
```

### No `return` with a value

Generators produce values via `yield`. A `return` with a value has no
well-defined meaning and is rejected:

```python
def bad_gen() -> int:
    yield 1
    return 42  # ERROR: SPY0267
```

Use a bare `return` for early termination instead.

### No mixing generator `__iter__` with `__next__`

A class must choose one approach: either a generator-based `__iter__` (with
`yield`) or an explicit iterator (with `__next__`). Defining both is an error:

```python
class Bad:
    def __iter__(self) -> int:
        yield 1  # generator-based
    def __next__(self) -> int:
        return 1  # explicit — ERROR: SPY0269
```

### Nested functions do not propagate

A `yield` inside a nested function definition does not make the enclosing
function a generator:

```python
def outer() -> int:
    def inner() -> int:
        yield 1  # inner is the generator, not outer
    return 42    # outer is a normal function
```

## Diagnostics

| Code | Level | Description |
|------|-------|-------------|
| SPY0265 | Error | `yield` outside a function |
| SPY0267 | Error | `return` with a value in a generator function |
| SPY0268 | Error | `yield` inside `__next__` |
| SPY0269 | Error | Class has both generator `__iter__` and `__next__` |

## Implementation

*`yield expr` → `yield return expr;` (C# iterator method)*

*`yield from expr` → `foreach (var item in expr) { yield return item; }` (delegation via loop)*

*Bare `return` in generator → `yield break;` (early termination)*

*Generator detection is automatic: the compiler scans for `YieldStatement` nodes
in the function body (excluding nested function/class definitions). Functions
containing yield have `IsGenerator = true` on their symbol.*
