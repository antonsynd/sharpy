# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T02:31:05.804290
**Type:** compilation_failed
**Feature Focus:** bool_variables
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    # Basic boolean variables
    is_active: bool = True
    is_enabled: bool = False
    print(is_active)
    print(is_enabled)

    # Boolean operations
    and_result: bool = is_active and is_enabled
    or_result: bool = is_active or is_enabled
    not_result: bool = not is_active
    print(and_result)
    print(or_result)
    print(not_result)

    # Comparison results as booleans
    x: int = 10
    y: int = 20
    is_less: bool = x < y
    is_equal: bool = x == y
    is_greater_or_equal: bool = x >= y
    print(is_less)
    print(is_equal)
    print(is_greater_or_equal)

    # Complex boolean expressions
    in_range: bool = x > 5 and x < 15
    out_of_range: bool = x < 5 or x > 25
    complex_check: bool = (x < y) and (not is_equal) and is_active
    print(in_range)
    print(out_of_range)
    print(complex_check)

    # Boolean from chained comparison
    chained: bool = 5 < x < 15
    print(chained)

    # Boolean in conditional
    if is_active:
        print(True)
    else:
        print(False)

    # Boolean with Optional - init with None()
    maybe_flag: bool? = None()
    has_value: bool = maybe_flag is not None
    print(has_value)

    # Update with actual value
    maybe_flag = False
    # Use value if present, otherwise True - using conditional instead of ??
    current: bool = True
    if maybe_flag is not None:
        current = maybe_flag
    print(current)

# EXPECTED OUTPUT:
# True
# False
# False
# True
# False
# True
# False
# False
# True
# False
# False
# True
# True
# False
# True
```

## Error

```
Assembly compilation failed:

error[CS0029]: Cannot implicitly convert type 'Sharpy.Optional<bool>' to 'bool'
  --> /tmp/tmptjmecdy_/dogfood_test.spy:54:23
    |
 54 |         current = maybe_flag
    |                       ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmptjmecdy_/dogfood_test.cs

```

## Timing

- Generation: 488.97s
- Execution: 4.51s
