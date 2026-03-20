# Comparison Chaining

```python
# Chained comparisons
a < b < c           # Equivalent to: a < b and b < c
x == y == z         # Equivalent to: x == y and y == z
1 <= value <= 100   # Range check
```

## Mixed Operators

Chained comparisons can mix different comparison operators:

```python
# Mixed operators are allowed
a < b <= c          # a < b and b <= c
a == b < c          # a == b and b < c
a != b != c         # a != b and b != c (but doesn't mean all different!)

# Complex chains
a < b <= c < d      # a < b and b <= c and c < d
```

## Evaluation

Each intermediate expression is evaluated only once:

```python
# f() is called only once, not twice
a < f() < c         # Equivalent to: _temp = f(); a < _temp and _temp < c
```

## Short-Circuit Behavior

Chained comparisons short-circuit: if any comparison in the chain evaluates to `false`, subsequent comparisons are **not evaluated**:

```python
# If a < b is false, b < c is never evaluated
a < b < c           # Short-circuits: if (a < b) is false, skips (b < c)

# Useful when later comparisons could have side effects or errors
x > 0 and x < len(items) and items[x] > threshold
```

This matches Python's short-circuit semantics for chained comparisons.

*Implementation*
- *🔄 Lowered - Expanded to `a < b && b < c` with single evaluation of middle expression. The `&&` operator provides short-circuit evaluation.*
