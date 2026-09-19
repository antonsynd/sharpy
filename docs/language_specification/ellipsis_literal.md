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

A type position is not a stub: `...` inside a type argument (e.g. `tuple[int, ...]`, the Python
`typing` spelling for a runtime-arity tuple) is `SPY0149`, not this stub form — see
[#1870](https://github.com/antonsynd/sharpy/issues/1870). Only the value/stub-body spellings above
are accepted.

<!-- spec-sweep: error SPY0149 -->
```sharpy
t: tuple[int, ...] = (1, 2, 3)
```

*Implementation*
- *🔄 Lowered - No-op for abstract methods or interface methods without a default implementation, otherwise `throw new NotImplementedException()`.*
