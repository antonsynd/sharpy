## Context Managers

The `with` statement manages resources:

```python
with open("file.txt", "r") as f:
    content = f.read()
# f.close() called automatically

# Multiple resources
with open("in.txt") as input, open("out.txt", "w") as output:
    output.write(input.read())
```

**Protocol:**
- Object passed to `with` should implement either `IContextManager` or `IDisposable`
  - For `IContextManager`:
    - `__enter__()` called on entry (returns object for `as` binding)
    - `__exit__()` called on exit
      - If the object returned in the `as` binding implements `IDisposable`, then its `Dispose()` method is also invoked (before `__exit__()`)
  - For `IDisposable`:
    - `Dispose()` called on exit
- If an object implements both, then `__exit__()` is called before `Dispose()`

*Implementation:*
- For `IContextManager`, ✅ Lowered - `try { var asBinding = contextManager; } catch(Exception e) { ... } finally { contextManager.__Exit__(...); }`
- For `IDisposable`, ✅ Native - `using (var r = resource) { ... }`

---
