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

## Walrus in Comprehension Filter: Uninitialized Variable Handling

When using walrus operator in a comprehension filter, if the comprehension produces no results, the walrus-assigned variable may never be assigned:

```python
# Potential issue: empty comprehension
results = [y for x in items if (y := f(x)) > 0]
# If items is empty, or no f(x) > 0, then y was never assigned!

print(y)  # ❌ ERROR at compile time if y might be uninitialized
```

**Sharpy's static typing rule:** If a walrus-assigned variable is used after a comprehension, the compiler analyzes whether the variable is guaranteed to be assigned:

| Scenario | Compiler Behavior |
|----------|-------------------|
| Variable used after, items guaranteed non-empty | ⚠️ Warning: "y may be uninitialized" |
| Variable used after, items may be empty | ❌ Error: "y may be uninitialized" |
| Variable not used after comprehension | ✅ OK (no issue) |
| Variable pre-declared with default | ✅ OK (has fallback value) |

**Safe patterns:**

```python
# Pattern 1: Pre-declare with default value
y: int? = None
results = [y for x in items if (y := f(x)) > 0]
# y is either last assigned value or None

# Pattern 2: Don't use the leaked variable
results = [y for x in items if (y := f(x)) > 0]
# Just use results, don't reference y afterward

# Pattern 3: Ensure non-empty (if you can guarantee it)
assert len(items) > 0
results = [y for x in items if (y := f(x)) > 0]
# Still a warning, but logic ensures y is assigned

# Pattern 4: Use explicit loop if you need the variable
last_y: int? = None
results: list[int] = []
for x in items:
    y = f(x)
    if y > 0:
        last_y = y
        results.append(y)
```

**Rationale:** Sharpy's static typing requires definite assignment. Unlike Python where accessing an unassigned variable raises `NameError` at runtime, Sharpy catches this at compile time.

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
