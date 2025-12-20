# For Loop

```python
# Iterate over collection
for name in names:
    print(name)

# Iterate with range
for i in range(10):
    print(i)

# Enumerate for index and value
for index, name in enumerate(names):
    print(f"{index}: {name}")
```

*Implementation:*
- *Collection: ✅ Native - `foreach (var item in collection)`*
- *`range()`: 🔄 Lowered - `for (int i = 0; i < n; i++)`*
- *`enumerate()`: 🔄 Lowered - `.Select((x, i) => (i, x))`*

`else`-clauses are described in loop_else.md.
