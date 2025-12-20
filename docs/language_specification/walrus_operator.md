## Walrus Operator **[v0.1.8]**

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

Variables assigned with `:=` inside a comprehension follow special scoping rules:

```python
# Variable assigned in comprehension filter DOES leak to outer scope
results = [y for x in data if (y := transform(x)) is not None]
print(y)  # OK: y is defined (holds the last assigned value)

# This is because := creates in "containing scope", not comprehension scope
# The comprehension iteration variable (x) does NOT leak
print(x)  # ERROR: x is not defined

# Be cautious: y's final value is the last successful transform
# This may not be the value you expect
```

**Contrast with Comprehension Variables:**

| Variable Type | Scope | Leaks? |
|--------------|-------|--------|
| Iteration variable (`for x in`) | Comprehension | ❌ No |
| Walrus assignment (`y :=`) | Containing | ✅ Yes |

*Implementation: 🔄 Lowered - Hoisted variable declaration:*

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

---
