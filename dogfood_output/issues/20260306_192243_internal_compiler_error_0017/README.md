# Issue Report: internal_compiler_error

**Timestamp:** 2026-03-06T19:18:53.238282
**Type:** internal_compiler_error
**Feature Focus:** dunder_len
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test __len__ dunder method with container hierarchy and type-safe operations
# Features: abstract base with __len__, concrete implementations, interface synthesis, integration with len() builtin

@abstract
class Container:
    @abstract
    def __len__(self) -> int: ...

    @virtual
    def is_empty(self) -> bool:
        return len(self) == 0

class Stack(Container):
    items: list[int]

    def __init__(self, initial: list[int]):
        self.items = initial.copy()

    @override
    def __len__(self) -> int:
        return len(self.items)

    def push(self, item: int) -> None:
        self.items.append(item)

    def peek(self) -> int:
        return self.items[len(self) - 1]

struct Queue:
    buffer: list[str]
    head: int

    def __init__(self, items: list[str]):
        self.buffer = items.copy()
        self.head = 0

    def __len__(self) -> int:
        return len(self.buffer) - self.head

    def dequeue(self) -> str:
        idx: int = self.head
        self.head += 1
        return self.buffer[idx]

class Buffer(Container):
    capacity: int
    used: int

    def __init__(self, cap: int):
        self.capacity = cap
        self.used = 0

    @override
    def __len__(self) -> int:
        return self.used

    def write(self, count: int) -> None:
        self.used = min_int(self.used + count, self.capacity)

def min_int(a: int, b: int) -> int:
    if a < b:
        return a
    return b

def analyze_stack(container: Stack, name: str) -> None:
    length: int = len(container)
    empty: bool = container.is_empty()
    print(f"{name}: len={length}, empty={empty}")

def analyze_buffer(container: Buffer, name: str) -> None:
    length: int = len(container)
    empty: bool = container.is_empty()
    print(f"{name}: len={length}, empty={empty}")

def main():
    # Test Stack with abstract base
    stack: Stack = Stack([10, 20, 30])
    analyze_stack(stack, "stack")
    
    stack.push(40)
    print(len(stack))
    
    # Test Buffer with abstract base
    buffer: Buffer = Buffer(100)
    print(len(buffer))
    buffer.write(25)
    print(len(buffer))
    
    # Test Queue (struct with __len__)
    queue: Queue = Queue(["a", "b", "c", "d"])
    print(len(queue))
    queue.dequeue()
    print(len(queue))
    
    # Combined lengths (Stack + Buffer + Queue)
    combined: int = len(stack) + len(buffer) + len(queue)
    print(combined)

```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'min_int()' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpjhhevwl2/dogfood_test.spy:58:21
    |
 58 |         self.used = min_int(self.used + count, self.capacity)
    |                     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 214.86s
