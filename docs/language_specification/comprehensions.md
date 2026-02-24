# Comprehensions

Comprehensions provide concise syntax for creating collections by transforming and filtering iterables.

## List Comprehensions

```python
# Basic transformation
squares = [x ** 2 for x in range(10)]
# [0, 1, 4, 9, 16, 25, 36, 49, 64, 81]

# With filter condition
evens = [x for x in range(10) if x % 2 == 0]
# [0, 2, 4, 6, 8]

# Transformation and filter
doubled_evens = [x * 2 for x in range(10) if x % 2 == 0]
# [0, 4, 8, 12, 16]

# Nested comprehension (comprehension inside comprehension)
matrix = [[i * j for j in range(3)] for i in range(3)]
# [[0, 0, 0], [0, 1, 2], [0, 2, 4]]
```

> **Note:** Both multiple `for` clauses in a single comprehension (e.g., `[(x, y) for x in A for y in B]`)
> and nested comprehensions where the element expression is itself a comprehension (e.g., `[[expr for ...] for ...]`)
> are supported — see [Multiple For Clauses](#multiple-for-clauses) below.

*Implementation*
- *🔄 Lowered - LINQ expressions:*
  - `[expr for x in iter]` → `.Select(x => expr).ToList()`
  - `[expr for x in iter if cond]` → `.Where(x => cond).Select(x => expr).ToList()`

**Filter and Transform Order:**

The filter (`if` clause) is applied **before** the transformation, matching Python semantics exactly:

```python
[x * 2 for x in items if x > 0]
# Equivalent to:
# result = []
# for x in items:
#     if x > 0:           # Filter first
#         result.append(x * 2)  # Then transform
```

This maps to LINQ's `.Where(...).Select(...)` ordering:

```csharp
// C# equivalent
items.Where(x => x > 0).Select(x => x * 2).ToList();
```

## Multiple For Clauses

Comprehensions can have multiple `for` clauses, which are evaluated left-to-right like nested loops:

```python
# Multiple for clauses
pairs = [(x, y) for x in range(3) for y in range(3)]
# Equivalent to:
# result = []
# for x in range(3):
#     for y in range(3):
#         result.append((x, y))
# [(0,0), (0,1), (0,2), (1,0), (1,1), (1,2), (2,0), (2,1), (2,2)]

# With filter on inner loop
pairs_filtered = [(x, y) for x in range(3) for y in range(3) if x != y]
# [(0,1), (0,2), (1,0), (1,2), (2,0), (2,1)]

# Later clauses can reference earlier variables
triangular = [(x, y) for x in range(4) for y in range(x)]
# [(1,0), (2,0), (2,1), (3,0), (3,1), (3,2)]
```

*Implementation*
- *🔄 Lowered - LINQ `SelectMany`:*

```csharp
// [(x, y) for x in range(3) for y in range(3)]
Enumerable.Range(0, 3)
    .SelectMany(x => Enumerable.Range(0, 3), (x, y) => (x, y))
    .ToList();
```

## Dict Comprehensions

```python
# Basic dict comprehension
square_dict = {x: x ** 2 for x in range(5)}
# {0: 0, 1: 1, 2: 4, 3: 9, 4: 16}

# From existing collection
names = ["alice", "bob", "charlie"]
name_lengths = {name: len(name) for name in names}
# {"alice": 5, "bob": 3, "charlie": 7}

# With filter
long_names = {name: len(name) for name in names if len(name) > 3}
# {"alice": 5, "charlie": 7}
```

*Implementation*
- *🔄 Lowered - `.ToDictionary(x => key, x => value)`*

## Set Comprehensions

```python
# Basic set comprehension
unique_lengths = {len(word) for word in ["apple", "banana", "cherry"]}
# {5, 6}

# With filter
short_lengths = {len(word) for word in ["apple", "banana", "cherry"] if len(word) < 7}
# {5, 6}
```

*Implementation*
- *🔄 Lowered - `.Select(...).ToHashSet()`*

## Comprehension Variable Scoping

Variables declared in comprehensions are scoped to that comprehension and do not leak into the enclosing scope:

```python
# Variables don't leak
squares = [i ** 2 for i in range(10)]
print(i)  # ERROR: 'i' does not exist in this scope

# Dict comprehension variables don't leak
ages = {name: age for name, age in pairs}
print(name)  # ERROR: 'name' does not exist in this scope
```

**Walrus Operator in Comprehensions:**

Variables assigned using the walrus operator (`:=`) inside a comprehension are also comprehension-local. They do not leak to the containing scope:

```python
# Walrus useful within comprehension to avoid recomputation
results = [y * 2 for x in items if (y := expensive(x)) > 0]
# y is local to the comprehension - used in both filter and transform

print(y)  # ERROR: 'y' does not exist in this scope
```

**Note:** This differs from Python 3.8+, where walrus assignments leak. In Sharpy, the syntactic boundary (`[...]` / `{...}`) is the semantic boundary: nothing leaks. See [walrus_operator.md](walrus_operator.md) for more details.

**Shadowing Outer Variables:**

Comprehension variables may shadow variables from the enclosing scope. The outer variable is not modified:

```python
x = 100
squares = [x ** 2 for x in range(5)]  # This 'x' shadows outer 'x'
print(x)  # 100 - outer 'x' unchanged
print(squares)  # [0, 1, 4, 9, 16]

name = "outer"
lengths = {name: len(name) for name in ["a", "bb", "ccc"]}
print(name)  # "outer" - unchanged
```

**Unique Variable Names Required:**

Within a single comprehension, each `for` clause must use a unique variable name. Reusing a variable name across multiple `for` clauses is a compile-time error:

```python
# ✅ OK - different variable names in each for clause
pairs = [(x, y) for x in range(3) for y in range(3)]

# ✅ OK - shadows outer scope (different from reuse within comprehension)
x = 100
result = [(x, y) for x in range(3) for y in range(3)]

# ❌ ERROR - same variable name in multiple for clauses
bad = [x for x in range(3) for x in range(3)]
# Compile error: Variable 'x' already declared in this comprehension

# ❌ ERROR - even with different structure
also_bad = [(x, x) for x in range(3) for x in range(3)]
# Compile error: Variable 'x' already declared in this comprehension
```

**Rationale:** Allowing the same variable name in multiple `for` clauses creates confusing code where the inner loop shadows the outer loop variable. This is almost always a bug rather than intentional behavior. Sharpy prohibits this pattern at compile time.

**Filter Clause Scope:**

Filter conditions (`if` clauses) can reference any variable declared in preceding `for` clauses:

```python
# Filter can use variables from any preceding for clause
result = [(x, y) for x in range(5) for y in range(5) if x + y < 4]
# [(0,0), (0,1), (0,2), (0,3), (1,0), (1,1), (1,2), (2,0), (2,1), (3,0)]

# Multiple filters
filtered = [x for x in range(20) if x % 2 == 0 if x % 3 == 0]
# [0, 6, 12, 18]
```
