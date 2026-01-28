# Walrus Operator

The walrus operator `:=` allows assignment within expressions:

```python
# Capture value in conditional
if (match := pattern.search(text)) is not None:
    print(f"Found match at {match.start()}")

# Reuse computed value
results = [y for x in data if (y := transform(x)) is not None]

# Avoid repeated calls
while (line := file.read_line()) is not None:
    process(line)
```

**Type Inference Only:**

The walrus operator always infers the type from the right-hand side expression. Type annotations are not supported with `:=` (matching Python 3.8+ behavior):

```python
# ✅ Valid - type inferred from get_value()
if (x := get_value()) > 0:
    pass

# ❌ Invalid - cannot annotate with walrus
if (x: int := get_value()) > 0:  # ERROR: type annotation not supported with :=
    pass
```

Since Sharpy has full static type information, the type of `get_value()` is known at compile time, making explicit annotation unnecessary.

**Walrus Operator in Comprehensions:**

Variables assigned with `:=` inside a comprehension are **local to the comprehension** and do not leak to the outer scope:

```python
# Walrus is useful within a comprehension to avoid recomputation
results = [y * 2 for x in data if (y := transform(x)) > 0]
# y is used within the comprehension - valid!

# But y does NOT leak to outer scope
print(y)  # ERROR: 'y' does not exist in this scope

# Same for iteration variables
print(x)  # ERROR: 'x' does not exist in this scope
```

**Departure from Python:** In Python 3.8+, walrus assignments inside comprehensions leak to the containing scope. Sharpy deliberately differs here for cleaner semantics: the syntactic boundary (`[...]`, `{...}`) equals the semantic boundary. Everything inside the comprehension delimiters stays inside.

**If you need a value after the comprehension:**

```python
# Assign before the comprehension
items = get_items()  # Not: [(x := get_items()) ...]
[x for x in items]

# Or use an explicit loop
last_valid: int | None = None
results: list[int] = []
for x in data:
    y = transform(x)
    if y > 0:
        last_valid = y
        results.append(y * 2)
```

*Implementation*
- *🔄 Lowered - Hoisted variable declaration:*

```python
# Sharpy
if (match := pattern.search(text)) is not None:
    print(match.group())
```
```csharp
// C# 9.0
var match = pattern.Match(text);
if (match.Success) {
    Console.WriteLine(match.Value);
}
```
