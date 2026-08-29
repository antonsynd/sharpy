# The `del` Statement

Sharpy does not support the `del` statement (Axiom 1 — .NET has no name-unbinding or attribute-deletion primitive). The `del` keyword is recognized by the parser and refused with `SPY0144`, which includes a shape-specific steer:

| Target shape | Steer |
|---|---|
| `del x[k]` | Use `x.pop(k)` to remove by key |
| `del name` | Not supported — .NET bindings cannot be unbound |
| `del obj.attr` | Not supported — .NET properties cannot be deleted |

```python
# All of these are refused at compile time:
del x       # SPY0144: not supported
del d["a"]  # SPY0144: use d.pop("a") instead
del obj.x   # SPY0144: not supported
```
