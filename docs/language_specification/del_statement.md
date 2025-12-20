# The `del` Statement

The `del` statement removes items from collections:

```python
# Delete from dictionary
d = {"a": 1, "b": 2}
del d["a"]              # Removes key "a"
print(d)                # {"b": 2}

# Delete from list by index
items = [1, 2, 3, 4]
del items[0]            # Removes first element
print(items)            # [2, 3, 4]

# Delete slice
del items[1:3]          # Removes elements at indices 1 and 2
```

## Del with Slices

When `del` is used with a slice, it calls `__delitem__` with a `slice` object:

```python
class slice:
    """Represents a slice range for __delitem__."""
    start: int?
    stop: int?
    step: int?

# When you write:
del items[1:3]

# The compiler generates:
items.__delitem__(slice(1, 3, None))
```

Types that want to support slice deletion should implement an overload of `__delitem__` accepting a `slice` parameter:

```python
class MyList[T]:
    def __delitem__(self, index: int) -> None:
        # Delete single element
        pass

    def __delitem__(self, s: slice) -> None:
        # Delete range of elements
        pass
```

## What `del` Does NOT Do

Unlike Python, Sharpy's `del` cannot delete local variables:

```python
x = 42
del x                   # ERROR: cannot delete local variable
```

Sharpy's `del` also cannot delete attributes on objects because
objects are not dynamic in Sharpy:

```python
class SomeObject:
    name: str

    def __init__(self, name: str):
        self.name = name

x = SomeObject(name="Bob")
del x.name              # ERROR: cannot delete attributes
```

## Dunder Method

`del obj[key]` calls the `__delitem__` dunder method:

```python
class CustomContainer:
    def __delitem__(self, key: str) -> None:
        print(f"Deleting {key}")

c = CustomContainer()
del c["test"]           # Prints: "Deleting test"
```

The dunder method can be overloaded to take one key of any type, including
but not limited to an integer index (negative indexing is possible), a slice,
etc.
