# Successful Dogfood Run

**Timestamp:** 2026-03-10T13:51:35.923045
**Feature Focus:** generator_reversed_class
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Generator __reversed__ with class implementing abstract interface
# Tests a Deque class implementing Container interface with custom bidirectional
# iteration and dynamic filtering based on threshold value

@abstract
class Container:
    @abstract
    def count(self) -> int: ...

    @abstract
    def clear(self) -> None: ...

class Deque(Container):
    _front: list[int]
    _back: list[int]
    _threshold: int

    def __init__(self):
        self._front = []
        self._back = []
        self._threshold = 0

    @override
    def count(self) -> int:
        return len(self._front) + len(self._back)

    @override
    def clear(self) -> None:
        self._front = []
        self._back = []

    def set_threshold(self, t: int) -> None:
        self._threshold = t

    def push_front(self, x: int) -> None:
        self._front.append(x)

    def push_back(self, x: int) -> None:
        self._back.append(x)

    def __iter__(self) -> int:
        # Yield front in reverse (newest first), then back in order
        i = len(self._front) - 1
        while i >= 0:
            if self._front[i] >= self._threshold:
                yield self._front[i]
            i -= 1
        for x in self._back:
            if x >= self._threshold:
                yield x

    def __reversed__(self) -> int:
        # Yield back in reverse (newest first), then front in order
        i = len(self._back) - 1
        while i >= 0:
            if self._back[i] >= self._threshold:
                yield self._back[i]
            i -= 1
        for x in self._front:
            if x >= self._threshold:
                yield x

def main():
    dq = Deque()
    dq.push_front(30)
    dq.push_front(20)
    dq.push_front(10)
    dq.push_back(40)
    dq.push_back(50)
    print("count:")
    print(dq.count())
    print("forward:")
    for x in dq:
        print(x)
    print("reverse:")
    for x in reversed(dq):
        print(x)
    dq.set_threshold(25)
    print("filtered:")
    for x in dq:
        print(x)
    print("filtered_rev:")
    for x in reversed(dq):
        print(x)

```

## Output

```
count:
5
forward:
10
20
30
40
50
reverse:
50
40
30
20
10
filtered:
30
40
50
filtered_rev:
50
40
30
```

## Timing

- Generation: 574.02s
- Execution: 5.52s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
