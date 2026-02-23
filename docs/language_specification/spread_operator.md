# Spread Operator

> **Implementation status:** Spreading in collection literals (`[*a, *b]`, `{*s1, *s2}`, `{**d1, **d2}`) and
> positional call-site spreading (`f(*args)`) are implemented. Tuple spreading, object copy with overrides,
> and `**kwargs` call-site spreading are not yet supported.

The spread operator enables concise syntax for unpacking collections and objects in various contexts, extending Python's `*` and `**` operators.

## List Spreading

Spread elements from one list into another:

```python
# Basic spreading
first = [1, 2]
second = [3, 4]
combined = [*first, *second]  # [1, 2, 3, 4]

# Mixed with literals
numbers = [1, 2, *middle, 99, 100]

# Multiple spreads
all_items = [*group_a, *group_b, *group_c]

# Flatten nested lists
nested = [[1, 2], [3, 4], [5, 6]]
flat = [*nested[0], *nested[1], *nested[2]]  # [1, 2, 3, 4, 5, 6]
```

## Dict Spreading

Merge dictionaries with spread syntax:

```python
# Basic merging
defaults = {"timeout": 30, "retry": True}
user_config = {"timeout": 60, "debug": True}
merged = {**defaults, **user_config}
# {"timeout": 60, "retry": True, "debug": True}

# Later values override earlier ones
config = {**defaults, **user_config, "override": True}

# Conditional spreading
config = {
    **base_config,
    **(debug_settings if debug else {}),
    "mode": mode
}
```

## Function Call Spreading

Unpack positional arguments in function calls:

```python
# Positional arguments
args = [1, 2, 3]
result = some_function(*args)
# Equivalent to: some_function(1, 2, 3)

# Tuples are expanded to .Item1, .Item2, etc.
pair = (10, 20)
add(*pair)  # Equivalent to: add(pair.Item1, pair.Item2)

# ❌ NOT SUPPORTED: **kwargs spreading in function calls
# kwargs = {"prefix": ">>", "suffix": "<<"}
# format_text(**kwargs)  # ERROR: dict spreading in calls not supported
# Use named arguments instead: format_text(prefix=">>", suffix="<<")
```

## Tuple Spreading

> **Not yet implemented.**

Spread into tuple literals:

```python
# Spread into tuples
first = (1, 2)
second = (3, 4)
combined = (*first, *second)  # (1, 2, 3, 4)

# Mixed with literals
coords = (*point_2d, z_value)  # Convert 2D to 3D
```

## Set Spreading

Spread into set literals:

```python
# Combine sets
set1 = {1, 2, 3}
set2 = {3, 4, 5}
combined = {*set1, *set2}  # {1, 2, 3, 4, 5}

# Add individual elements
expanded = {*original_set, new_element}
```

## Object Copy with Overrides

> **Not yet implemented.**

Create modified copies of objects using spread:

```python
# With dataclasses/structs
@dataclass
class User:
    name: str
    email: str
    age: int

original = User(name="Alice", email="alice@example.com", age=30)

# Create copy with changes
updated = original.copy(**{"age": 31, "email": "newalice@example.com"})

# Or with explicit fields
updated = original.copy(age=31, email="newalice@example.com")
```

## Spreading Iterables

Spread any iterable, not just lists:

```python
# Spread range
numbers = [*range(5)]  # [0, 1, 2, 3, 4]

# Spread string (creates list of characters)
chars = [*"hello"]  # ['h', 'e', 'l', 'l', 'o']

# Spread generator
values = [*generate_values()]

# Spread custom iterable
items = [*my_custom_collection]
```

## Rest Patterns (Destructuring)

Collect remaining elements in unpacking:

```python
# In list unpacking
first, *rest = [1, 2, 3, 4, 5]
# first = 1, rest = [2, 3, 4, 5]

# Middle collection
first, *middle, last = [1, 2, 3, 4, 5]
# first = 1, middle = [2, 3, 4], last = 5

# In function parameters (already supported)
def sum_all(first: int, *rest: int) -> int:
    return first + sum(rest)
```

## Type Safety

Spreading is type-checked at compile time:

```python
# ✅ Compatible types
int_list: list[int] = [1, 2, 3]
more_ints: list[int] = [*int_list, 4, 5]

# ❌ Type mismatch
str_list: list[str] = ["a", "b"]
mixed = [*int_list, *str_list]  # ERROR: cannot mix int and str

# ✅ Use a common base type (e.g., object)
mixed: list[object] = [*int_list, *str_list]
```

## Nested Spreading

Spreading can be nested:

```python
# Nested list spreading
groups = [[1, 2], [3, 4], [5, 6]]
flat = [*[*group for group in groups]]  # Flatten using comprehension

# Nested dict spreading
base = {"a": 1}
overrides = {"b": 2}
config = {**base, **{**overrides, "c": 3}}
```

## Common Patterns

**Combining Collections:**
```python
# Concatenate lists
all_items = [*list1, *list2, *list3]

# Merge dictionaries with precedence
final_config = {**defaults, **env_config, **user_config}

# Combine sets
all_tags = {*required_tags, *optional_tags}
```

**Function Argument Forwarding:**
```python
# Positional argument forwarding (supported)
def wrapper(*args: int):
    return underlying_function(*args)

# ❌ NOT YET SUPPORTED: **kwargs forwarding requires @dynamic_kwargs (Phase 11)
# def wrapper(*args, **kwargs):
#     return underlying_function(*args, **kwargs)
```

**Immutable Updates:**
```python
# Add to immutable list
original = [1, 2, 3]
with_new = [*original, 4]  # Don't modify original

# Update immutable dict
original_dict = {"a": 1, "b": 2}
updated_dict = {**original_dict, "b": 3}  # Override "b"
```

**Building Complex Structures:**
```python
# Build API request
request_data = {
    **base_headers,
    **auth_headers,
    "method": "POST",
    "body": {**payload, "timestamp": now()}
}
```

## Operator Positions

Where spreading is allowed:

| Context | Syntax | Example | Status |
|---------|--------|---------|--------|
| List literal | `[*expr]` | `[1, *others, 5]` | ✅ |
| Dict literal | `{**expr}` | `{**base, "key": val}` | ✅ |
| Set literal | `{*expr}` | `{1, *others, 5}` | ✅ |
| Function call (positional) | `f(*args)` | `func(*items)` | ✅ |
| Unpacking | `a, *rest = ...` | `first, *middle, last = items` | ✅ |
| Tuple literal | `(*expr)` | `(1, *others, 5)` | ❌ Not yet |
| Function call (kwargs) | `f(**kwargs)` | `func(**opts)` | ❌ Not yet |

## Limitations

- Cannot spread in type annotations
- Cannot spread in class definition
- Rest patterns must appear once in unpacking
- Dict spreading requires mapping type

```python
# ❌ Cannot spread in type annotations
type MyList = list[*SomeType]  # ERROR

# ❌ Cannot spread in class bases
class MyClass(*bases):  # ERROR
    pass

# ❌ Multiple rest patterns
first, *middle1, *middle2, last = items  # ERROR

# ❌ Rest pattern must be named
first, *, last = items  # ERROR: rest pattern needs name
```

## C# Mapping

Spreading is lowered to appropriate C# constructs:

```python
# Sharpy - list spreading
combined = [1, 2, *middle, 99, 100]
```
```csharp
// C# 9.0
var combined = new List<int> { 1, 2 };
combined.AddRange(middle);
combined.Add(99);
combined.Add(100);
```

**Dict spreading:**
```python
# Sharpy
merged = {**defaults, **user_config, "override": True}
```
```csharp
// C# 9.0
var merged = new Dictionary<string, object>(defaults);
foreach (var kvp in user_config) {
    merged[kvp.Key] = kvp.Value;
}
merged["override"] = true;
```

**Function call spreading (positional):**
```python
# Sharpy
result = function(*args)
```
```csharp
// C# 9.0 — tuple spread expands to .Item1, .Item2, etc.
// Iterable spread → .ToArray() for params T[]
var result = function(args[0], args[1], args[2]);
```

## Performance Considerations

- List spreading creates new collections (allocation cost)
- Dict spreading involves dictionary copies
- Multiple spreads compound allocation costs
- Consider preallocating for large collections

```python
# Less efficient: multiple allocations
result = [*list1, *list2, *list3, *list4]

# More efficient: preallocate
result = []
result.reserve(len(list1) + len(list2) + len(list3) + len(list4))
result.extend(list1)
result.extend(list2)
result.extend(list3)
result.extend(list4)
```

## Comparison with Collection Methods

| Spread Syntax | Method Equivalent |
|---------------|-------------------|
| `[*a, *b]` | `a + b` or `a.extend(b)` |
| `{**a, **b}` | `{**a, **b}` or `a.update(b)` |
| `func(*args)` | Manual unpacking |

Spread syntax is preferred for readability and composability.

*Implementation: 🔄 Lowered - Expanded to collection operations:*
- *List spreading: `List.AddRange()` calls*
- *Dict spreading: Dictionary copy + merge operations*
- *Function spreading: Array access + named parameters*
- *Tuple spreading: Tuple concatenation helpers*
