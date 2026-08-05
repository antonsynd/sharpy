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

Parentheses are grouping and are transparent here as everywhere else: `(...)` — and any
number of nested parentheses — is the same stub body as `...`, in a class body, an interface
body, and an abstract method, property or event stub. Note this is a stub *body* on its own
line; the one-line form is written `def draw(self) -> None: ...`.

*Implementation*
- *🔄 Lowered - No-op for abstract methods or interface methods without a default implementation, otherwise `throw new NotImplementedException()`.*
