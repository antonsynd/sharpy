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
- *🔄 Lowered - LINQ expressions wrapped in Sharpy.Core types:*
  - `[expr for x in iter]` → `new Sharpy.List<T>(iter.Select(x => expr))`
  - `[expr for x in iter if cond]` → `new Sharpy.List<T>(iter.Where(x => cond).Select(x => expr))`

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
- *🔄 Lowered - `.ToDictionary(x => key, x => value)` wrapped in `new Sharpy.Dict<K,V>(...)`*

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
- *🔄 Lowered - `.Select(...)` wrapped in `new Sharpy.Set<T>(...)`*

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

## Spread Unpacking in Comprehensions (PEP 798)

Comprehensions support `*` and `**` unpacking to flatten iterables or merge dicts in a single expression.

### List/Set Spread (`*`)

Use `*` as the element expression to flatten one level of nesting:

```python
its: list[list[int]] = [[1, 2], [3, 4], [5]]
flat: list[int] = [*it for it in its]
# [1, 2, 3, 4, 5]

# With filter
filtered: list[int] = [*it for it in its if len(it) > 1]
# [1, 2, 3, 4]

# Set spread
unique: set[int] = {*it for it in [{1, 2}, {2, 3}]}
# {1, 2, 3}
```

The result type is `list[T]` / `set[T]` where `T` is the element type of the spread value (one level of nesting is removed).

*Implementation*
- *🔄 Lowered - single-for: `iter.SelectMany(it => it)` wrapped in `new List<T>(...)`*
- *🔄 Lowered - multi-for: imperative loop with `.Extend(it)` / `.UnionWith(it)`*

### Dict Spread (`**`)

Use `**` as the spread expression to merge dicts from an iterable:

```python
dicts: list[dict[str, int]] = [{"a": 1}, {"b": 2, "c": 3}]
merged: dict[str, int] = {**d for d in dicts}
# {"a": 1, "b": 2, "c": 3}

# With filter
filtered: dict[str, int] = {**d for d in dicts if len(d) > 1}
# {"b": 2, "c": 3}
```

The result type is `dict[K, V]` inferred from the spread value type.

Later dicts overwrite earlier keys (same as `dict.update()` semantics).

*Implementation*
- *🔄 Lowered - imperative loop: `new Dict<K,V>()` + `.Update(d)` for each iteration*

## Async Comprehensions

Async comprehensions **are supported** inside `async def` functions: list, set, and dict comprehensions may use an `async for` clause and/or `await` in the element/key/value/filter, executing sequentially. See [async_programming.md](async_programming.md#async-comprehensions) for the rules, semantics, and examples.

Async *generator expressions* (`(x async for x in src)`) are not supported, since Sharpy has no generator-expression construct.
