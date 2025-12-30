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

**Protocol:**
- Object passed to `with` should implement `IDisposable`:
  - `Dispose()` called on exit

*Implementation:*
- *For `IDisposable`, ✅ Native - `using (var r = resource) { ... }`*
