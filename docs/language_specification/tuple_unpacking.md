# Tuple Unpacking

Sharpy supports destructuring tuples and lists into individual variables across multiple contexts: assignments, for loops, and comprehensions.

## Simple Tuple Unpacking

Unpack a tuple into variables by matching the number of elements:

```python
point: tuple[int, int] = (3, 7)
x, y = point
print(x)  # 3
print(y)  # 7
```

The number of targets must match the number of tuple elements:

```python
# ❌ ERROR: Cannot unpack 3 values into 2 variables
a, b = (1, 2, 3)
```

*Implementation*
- *✅ Native - C# tuple deconstruction: `var (x, y) = point;`*

## Nested Tuple Unpacking

Targets can themselves be tuple patterns, enabling nested destructuring:

```python
t: tuple[tuple[int, int], int] = ((1, 2), 3)
(a, b), c = t
print(a)  # 1
print(b)  # 2
print(c)  # 3
```

Nesting can be arbitrarily deep:

```python
t: tuple[tuple[int, tuple[int, int]], int] = ((1, (2, 3)), 4)
(a, (b, c)), d = t
print(a)  # 1
print(b)  # 2
print(c)  # 3
print(d)  # 4
```

Each nested target must match the structure and element count of the corresponding tuple element.

*Implementation*
- *🔄 Lowered - Temporary variables with `.Item1`, `.Item2`, etc. access:*
  ```csharp
  var __t0 = t;
  var a = __t0.Item1.Item1;
  var b = __t0.Item1.Item2;
  var c = __t0.Item2;
  ```

## Rest Patterns (`*rest`)

A starred expression collects remaining elements into a list:

```python
items: list[int] = [1, 2, 3, 4, 5]

# Collect tail
first, *rest = items
print(first)  # 1
print(rest)   # [2, 3, 4, 5]

# Collect head
*rest, last = items
print(rest)   # [1, 2, 3, 4]
print(last)   # 5

# Collect middle
first, *mid, last = items
print(first)  # 1
print(mid)    # [2, 3, 4]
print(last)   # 5
```

**Rules:**
- Only one starred expression is allowed per unpacking
- The starred variable is always typed as `list[T]` where `T` is the element type of the source
- Works with both lists and tuples as the source

```python
# ❌ ERROR: Only one starred expression allowed
*a, *b = items
```

*Implementation*
- *🔄 Lowered - Index access and slicing:*
  ```csharp
  var __t0 = items;
  var first = __t0[0];
  var mid = __t0.GetSlice(new global::Sharpy.Slice((int?)1, (int?)-1));
  var last = __t0[-1];
  ```

## Tuple Unpacking in For Loops

Iterate over collections of tuples with destructuring:

```python
pairs: list[tuple[str, int]] = [("alice", 1), ("bob", 2)]
for name, score in pairs:
    print(f"{name}: {score}")
# alice: 1
# bob: 2
```

Nested unpacking is also supported in for loops:

```python
items: list[tuple[tuple[int, int], str]] = [((1, 2), "a"), ((3, 4), "b")]
for (x, y), label in items:
    print(f"{label}: {x + y}")
# a: 3
# b: 7
```

*Implementation*
- *✅ Native (simple case) - C# `foreach (var (name, score) in pairs)`*
- *🔄 Lowered (nested case) - Temporary loop variable with `.Item1`, `.Item2` access*

## Tuple Unpacking in Comprehensions

List, set, and dict comprehensions support tuple unpacking in their `for` clauses:

```python
pairs: list[tuple[int, int]] = [(1, 2), (3, 4), (5, 6)]
sums = [a + b for a, b in pairs]
print(sums)  # [3, 7, 11]
```

Nested unpacking works in comprehensions as well:

```python
items: list[tuple[tuple[int, int], str]] = [((1, 2), "a"), ((3, 4), "b")]
result: list[str] = [name + ":" + str(x + y) for (x, y), name in items]
print(result)  # ["a:3", "b:7"]
```

*Implementation*
- *🔄 Lowered - Lambda with temporary variable destructuring:*
  ```csharp
  pairs.Select((__param) => {
      var a = __param.Item1;
      var b = __param.Item2;
      return a + b;
  }).ToList()
  ```

## Type Inference

Unpacking targets are automatically inferred from the source type:

| Source Type | Target Inference |
|-------------|-----------------|
| `tuple[int, str]` | First target: `int`, second: `str` |
| `list[tuple[int, str]]` (in for loop) | Loop targets: `int`, `str` |
| `*rest` from `list[T]` | Starred target: `list[T]` |
| `*rest` from `tuple[T, ...]` | Starred target: `list[T]` (uses first element type) |

Nested tuple targets recurse into the corresponding element type and validate structure at each level.

## Error Cases

| Scenario | Diagnostic |
|----------|-----------|
| Element count mismatch | SPY0239: Cannot unpack N values into M variables |
| Unpacking a non-tuple type | SPY0239: Cannot unpack non-tuple type |
| Multiple starred expressions | SPY0356: Only one starred expression allowed |

## See Also

- [Spread Operator](spread_operator.md) — Spreading collections with `*` and `**`
- [Comprehensions](comprehensions.md) — List, dict, and set comprehensions
- [For Statement](for_statement.md) — For loop syntax
