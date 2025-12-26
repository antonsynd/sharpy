# Smart Ranges

Ranges in Sharpy are first-class values that support membership testing, set operations, and pattern matching, going beyond simple iteration.

## Range Literals

```python
# Exclusive end (Python-style)
r1: range[int] = 0..10      # 0 to 9 (10 excluded)
r2: range[int] = 1..100     # 1 to 99

# Inclusive end (Rust-style)
r3: range[int] = 0..=10     # 0 to 10 (10 included)
r4: range[int] = 13..=19    # Teen years: 13 to 19

# Open-ended ranges
r5: range[int] = 10..       # 10 to infinity
r6: range[int] = ..10       # Negative infinity to 9
r7: range[int] = ..=10      # Negative infinity to 10
```

## Range Operator Precedence

The `..` and `..=` operators bind **looser** than member access (`.`) but **tighter** than comparison operators. This enables intuitive parsing of common patterns:

| Expression | Parsed As | Explanation |
|------------|-----------|-------------|
| `x..y.z` | `x..(y.z)` | Member access binds tighter |
| `a.b..c.d` | `(a.b)..(c.d)` | Both sides get member access |
| `0..n-1` | `0..(n-1)` | Arithmetic binds tighter |
| `x..y < z` | `(x..y) < z` | Range binds tighter than comparison |
| `a + b..c + d` | `(a + b)..(c + d)` | Arithmetic on both sides |

**Recommended:** Use parentheses for clarity in complex expressions:

```python
# Clear
r = (obj.start)..(obj.end)
r = 0..(len(items) - 1)

# Avoid (works, but less clear)
r = obj.start..obj.end      # Same as above, but parentheses help readers
```

**Note:** Range operators are **non-associative**. Chaining is not allowed:

```python
1..5..10      # ERROR: Range operators cannot be chained
(1..5)..10    # ERROR: Cannot create range of ranges
```

## Range Types

```python
# Type annotation
valid_ages: range[int] = 0..120
valid_percentages: range[double] = 0.0..=100.0

# Generic range type
range[T] where T: IComparable[T]
```

## Membership Testing

```python
valid_ages: range[int] = 0..120

if age in valid_ages:
    print("Valid age")

# With inclusive end
teen_years: range[int] = 13..=19
if age in teen_years:
    print("Teenager")

# Open-ended ranges
positives: range[int] = 0..
if value in positives:
    print("Positive or zero")
```

## Iteration

Ranges can be iterated when they have finite bounds:

```python
# Iterate over range
for i in 0..5:
    print(i)  # 0, 1, 2, 3, 4

# Inclusive end
for i in 1..=5:
    print(i)  # 1, 2, 3, 4, 5

# ❌ Cannot iterate infinite ranges
for i in 0..:  # ERROR: cannot iterate unbounded range
    pass
```

## Range Operations

**Intersection (`&`):**
```python
r1: range[int] = 0..10
r2: range[int] = 5..15

overlap = r1 & r2  # 5..10
```

**Union (`|`):**
```python
# Only works for contiguous or overlapping ranges
r1: range[int] = 0..5
r2: range[int] = 3..8

combined = r1 | r2  # 0..8

# Non-contiguous ranges error
r1 = 0..5
r2 = 10..15
combined = r1 | r2  # ERROR: ranges not contiguous
```

**Contains Range (`in`):**
```python
outer: range[int] = 0..100
inner: range[int] = 10..20

if inner in outer:  # True if inner ⊆ outer
    print("inner is contained in outer")
```

**Empty Check:**
```python
r: range[int] = 5..5  # Empty range
if r.is_empty():
    print("Range is empty")
```

## Pattern Matching

Ranges work naturally in pattern matching:

```python
match score:
    case 90..=100:
        grade = "A"
    case 80..90:
        grade = "B"
    case 70..80:
        grade = "C"
    case 60..70:
        grade = "D"
    case _:
        grade = "F"

# With guards
match age:
    case 0..=12:
        category = "Child"
    case 13..=19:
        category = "Teenager"
    case 20..=64:
        category = "Adult"
    case 65..:
        category = "Senior"
```

## Slicing with Ranges

Ranges can be used for collection slicing:

```python
items = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]

# Using range literals (alternative to Python slice notation)
items[2..5]     # [2, 3, 4] - same as items[2:5]
items[..3]      # [0, 1, 2] - same as items[:3]
items[3..]      # [3, 4, 5, 6, 7, 8, 9] - same as items[3:]
items[2..=5]    # [2, 3, 4, 5] - inclusive end

# Python slice notation still works
items[2:5]      # [2, 3, 4]
items[:3]       # [0, 1, 2]
items[3:]       # [3, 4, 5, 6, 7, 8, 9]
```

## Step Ranges

Ranges with step values:

```python
# Step syntax
r: range[int] = 0..10..2    # 0, 2, 4, 6, 8 (step of 2)
r: range[int] = 0..=10..2   # 0, 2, 4, 6, 8, 10

# Negative step
r: range[int] = 10..0..-1   # 10, 9, 8, ..., 1

# With slicing
items[0..10..2]   # Every 2nd item from 0 to 10
```

## Range Properties

```python
r: range[int] = 5..15

# Properties
r.start      # 5
r.end        # 15
r.inclusive  # False
r.is_empty() # False

# Length (for bounded ranges)
r.length()   # 10

# Contains check
r.contains(10)  # True
```

## Type Constraints

Ranges work with any comparable type:

```python
# Numeric ranges
int_range: range[int] = 0..100
double_range: range[double] = 0.0..1.0

# Character ranges
letter_range: range[char] = 'a'..'z'

# Custom types (must implement IComparable)
date_range: range[Date] = start_date..end_date
```

## Floating-Point Ranges

Special considerations for floating-point ranges:

```python
# Floating-point ranges
r: range[double] = 0.0..1.0

# Membership testing uses epsilon comparison
if 0.5 in r:  # True
    pass

# ⚠️ Iteration not supported for floating-point ranges
for x in 0.0..1.0:  # ERROR: cannot iterate floating-point range
    pass

# Use explicit step for iteration
for x in range(0.0, 1.0, 0.1):  # Use builtin range() function
    print(x)
```

## Common Patterns

**Validation:**
```python
valid_port: range[int] = 1..=65535

def set_port(self, port: int) -> None:
    if port not in valid_port:
        raise ValueError("Invalid port number")
    self.port = port
```

**Age Categories:**
```python
child: range[int] = 0..=12
teen: range[int] = 13..=19
adult: range[int] = 20..=64
senior: range[int] = 65..

def categorize_age(age: int) -> str:
    match age:
        case x if x in child: return "Child"
        case x if x in teen: return "Teen"
        case x if x in adult: return "Adult"
        case x if x in senior: return "Senior"
        case _: return "Invalid"
```

**Bounded Buffers:**
```python
class BoundedBuffer[T]:
    capacity: range[int] = 0..=1000
    size: int

    def add(self, item: T) -> None:
        if self.size not in self.capacity:
            raise OverflowError("Buffer full")
        # ... add item
```

## C# Mapping

Ranges map to C# `Range` type (C# 8+) and custom types for extended functionality:

```python
# Sharpy
items[2..5]
items[..3]
items[3..]
```
```csharp
// C# 9.0
items[2..5];
items[..3];
items[3..];
```

**Smart range type:**
```python
# Sharpy
r: range[int] = 5..15
if value in r:
    pass
```
```csharp
// C# 9.0
var r = new SmartRange<int>(5, 15, inclusive: false);
if (r.Contains(value)) {
    // ...
}
```

**Pattern matching:**
```python
# Sharpy
match score:
    case 90..=100: "A"
    case 80..90: "B"
```
```csharp
// C# 9.0
var grade = score switch {
    >= 90 and <= 100 => "A",
    >= 80 and < 90 => "B",
    _ => "F"
};
```

## Operations Summary

| Operation | Syntax | Description |
|-----------|--------|-------------|
| Exclusive end | `a..b` | Range from a to b-1 |
| Inclusive end | `a..=b` | Range from a to b |
| Open start | `..b` | From -∞ to b-1 |
| Open end | `a..` | From a to +∞ |
| Step | `a..b..s` | Range with step s |
| Membership | `x in r` | Check if x in range |
| Intersection | `r1 & r2` | Overlapping range |
| Union | `r1 \| r2` | Combined range |
| Slicing | `list[r]` | Slice with range |

## Limitations

- Open-ended ranges cannot be iterated
- Floating-point ranges cannot be iterated (use `range()` function with step)
- Union only works for contiguous ranges
- Step must be positive for ascending ranges, negative for descending

*Implementation: 🔄 Mixed:*
- *Slicing syntax (`items[2..5]`): ✅ Native - Maps to C# `Range` syntax*
- *Pattern matching: ✅ Native - Maps to C# relational patterns*
- *Range objects: 🔄 Lowered - Custom `SmartRange<T>` type in Sharpy.Core*
- *Range operations: 🔄 Lowered - Methods on `SmartRange<T>`*

---
