# Issue Report: execution_failed

**Timestamp:** 2026-01-17T00:45:35.787200
**Type:** execution_failed
**Feature Focus:** generic_class
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Simple generic class test
class Box[T]:
    value: T

    def __init__(self, v: T):
        self.value = v

    def get(self) -> T:
        return self.value

int_box = Box[int](42)
print(int_box.get())

str_box = Box[str]("hello")
print(str_box.get())

# EXPECTED OUTPUT:
# 42
# hello
```

## Error

```
Compilation failed:
  Semantic error at line 3, column 5: Variable 'value' declared with 'auto' must have an initializer
  Semantic error at line 11, column 11: Type '<?>' does not support indexing (missing '__getitem__' method). Consider implementing ISequence<T> interface.
  Semantic error at line 14, column 11: Type '<?>' does not support indexing (missing '__getitem__' method). Consider implementing ISequence<T> interface.

```

## Timing

- Generation: 3.86s
- Execution: 0.87s
