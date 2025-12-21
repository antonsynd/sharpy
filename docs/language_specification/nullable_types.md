# Nullable Types

All Sharpy standard library types and Sharpy user-defined types are
not nullable by default, meaning they cannot be assigned `None`. This is
unlike C# and Python, where C# allows reference types to be assigned the
C# equivalent (`null`), and Python allows all types to be assigned `None`.

Sharpy standard library types and Sharpy user-defined type can hold
`None` by being marked nullable with the `?` suffix on the type annotation.

```python
# Nullable type annotations (type followed by ?)
result: int? = get_value()
optional_name: str? = None
optional_list_of_non_optional_ints: list[int]? = None
optional_list_of_optional_ints: list[int?]? = None

# All types are non-nullable by default
exists: bool = False           # Cannot be None
count: int = 42                # Cannot be None
numbers: list[int] = [42, 67]  # Cannot be None

# Assigning None requires nullable type
value: int? = None    # OK
other: int = None     # ERROR: Cannot assign None to non-nullable type
maybe_numbers: list[int?] = [42, None, 67]  # OK
numbers: list[int] = [42, None, 67]         # ERROR: Cannot assign None to non-nullable
```

*Implementation*
- *✅ Native - Maps to C# nullable reference types with `#nullable enable`.*
