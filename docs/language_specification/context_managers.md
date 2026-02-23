# Context Managers

The `with` statement manages resources:

```python
with open("file.txt", "r") as f:
    content = f.read()
# f.close() called automatically

# Multiple resources
with open("in.txt") as input, open("out.txt", "w") as output:
    output.write(input.read())
```

Sharpy's `with` statement requires the object to implement `IDisposable`. Unlike Python's `__enter__`/`__exit__` protocol, Sharpy uses the .NET `IDisposable` pattern directly.

**Protocol:**
- Object passed to `with` must implement `IDisposable`
- `Dispose()` is called automatically on scope exit (even if an exception is thrown)

> **Note:** Multiple resources in a single `with` statement (e.g., `with a as x, b as y:`) are not yet supported. Use nested `with` statements as a workaround.

*Implementation: ✅ Native — `with expr as name:` → C# `using (var name = expr) { ... }`*
