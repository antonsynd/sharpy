# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T02:30:59.063931
**Type:** compilation_failed
**Feature Focus:** interface_definition
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex interface definition with inheritance, default methods, and multiple interfaces
# NOTE: Uses non-generic classes to avoid SPY0220 type mismatch errors with generics

interface ISizable:
    """Base interface for objects with a size."""
    def get_size(self) -> int: ...

interface IContainer:
    """Interface for container operations."""
    def add(self, item: int) -> None: ...

    def get_count(self) -> int: ...

interface ICountable:
    """Interface for countable objects."""
    def count(self) -> int: ...

@abstract
class AbstractContainer(ICountable):
    """Abstract base class providing default implementations."""
    @abstract
    def count(self) -> int: ...

    def is_empty(self) -> bool:
        """Default implementation using count."""
        return self.count() == 0

    def summary(self) -> str:
        """Another default method."""
        count_val: int = self.count()
        return f"Items: {count_val}"

class Box(ISizable, AbstractContainer):
    """A simple box that can hold one item."""
    _item: int?
    _has_item: bool

    def __init__(self):
        self._item = None()
        self._has_item = False

    def get_size(self) -> int:
        if self._has_item:
            return 1
        return 0

    @override
    def count(self) -> int:
        if self._has_item:
            return 1
        return 0

    def put(self, item: int) -> None:
        self._item = Some(item)
        self._has_item = True

    def take(self) -> int?:
        if self._has_item:
            self._has_item = False
            result: int? = self._item
            self._item = None()
            return result
        return None()

class Bucket(IContainer, AbstractContainer):
    """A bucket that can hold multiple items."""
    _items: list[int]

    def __init__(self):
        self._items = []

    def get_size(self) -> int:
        return len(self._items)

    def add(self, item: int) -> None:
        self._items.append(item)

    def get_count(self) -> int:
        return len(self._items)

    @override
    def count(self) -> int:
        return len(self._items)

    def clear(self) -> None:
        """Clear all items."""
        while len(self._items) > 0:
            self._items.pop()

def process_sizable(obj: ISizable) -> int:
    """Polymorphic function working with ISizable."""
    return obj.get_size()

def process_countable(obj: ICountable) -> int:
    """Polymorphic function working with ICountable."""
    return obj.count()

def process_container(obj: IContainer) -> int:
    """Polymorphic function working with IContainer."""
    return obj.get_count()

def check_empty(obj: AbstractContainer) -> bool:
    """Function using default method from abstract base."""
    return obj.is_empty()

def main():
    # Test Box with ISizable interface
    box = Box()
    box.put(42)
    print(process_sizable(box))
    print(check_empty(box))

    # Test Bucket with IContainer and ICountable interfaces
    bucket = Bucket()
    bucket.add(10)
    bucket.add(20)
    bucket.add(30)
    print(process_countable(bucket))
    print(process_container(bucket))
    print(check_empty(bucket))
    print(bucket.summary())

    # Test clear
    bucket.clear()
    print(bucket.count())
    print(check_empty(bucket))

    # Test empty Box
    empty_box = Box()
    print(empty_box.is_empty())
    print(process_sizable(empty_box))

    # Test Box with item
    box2 = Box()
    box2.put(100)
    print(box2.is_empty())
    print(box2.summary())

# EXPECTED OUTPUT:
# 1
# False
# 3
# 3
# False
# Items: 3
# 0
# True
# True
# 0
# False
# Items: 1
```

## Error

```
Assembly compilation failed:

error[CS1722]: Base class 'DogfoodTest.AbstractContainer' must come before any interfaces
  --> /tmp/tmp_oq6pkco/dogfood_test.spy:38:34
    |
 38 |     def __init__(self):
    |                        ^
    |

error[CS1722]: Base class 'DogfoodTest.AbstractContainer' must come before any interfaces
  --> /tmp/tmp_oq6pkco/dogfood_test.spy:47:39
    |
 47 |     @override
    |              ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp_oq6pkco/dogfood_test.cs

```

## Timing

- Generation: 672.29s
- Execution: 4.14s
