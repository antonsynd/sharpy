# copy

Shallow and deep copy operations, similar to Python's copy module.

```python
import copy
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `instance` | `IdentityEqualityComparer` |  |

## Functions

### `copy.copy(x: object) -> object`

Return a shallow copy of .
For Sharpy collections (, ,
), a new collection is created with the same element
references. For value types the value is returned as-is. For other
reference types, MemberwiseClone is invoked via reflection.

**Parameters:**

- `x` (object) -- The object to copy.

**Returns:** A shallow copy of .

```python
a = [1, 2, 3]
b = copy.copy(a)    # new list, same element references
```

### `copy.deepcopy(x: object) -> object`

Return a deep copy of .
For Sharpy collections, elements are recursively deep-copied. An identity
dictionary tracks already-copied objects to handle circular references.
For non-collection objects, falls back to a shallow copy.

**Parameters:**

- `x` (object) -- The object to deep-copy.

**Returns:** A deep copy of .

```python
a = [[1, 2], [3, 4]]
b = copy.deepcopy(a)
b[0].append(99)
# a is unchanged: [[1, 2], [3, 4]]
```
