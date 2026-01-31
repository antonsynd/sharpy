# Ellipsis Literal

The ellipsis literal `...` is a placeholder for unimplemented code:

```python
# In interfaces and abstract methods
interface IDrawable:
    def draw(self) -> None:
        ...  # Abstract method

# As placeholder for implementation
def todo_function():
    ...  # Placeholder for implementation
```

*Implementation*
- *🔄 Lowered - No-op for abstract methods or interface methods without a default implementation, otherwise `throw new NotImplementedException()`.*
