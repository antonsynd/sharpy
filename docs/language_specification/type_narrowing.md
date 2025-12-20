# Type Narrowing **[v0.1.1]**

Sharpy performs type narrowing in conditional branches:

```python
value: str? = get_optional_string()

if value is not None:
    # Inside this block, 'value' is narrowed from 'str?' to 'str'
    print(value.upper())  # OK - value is str, not str?
else:
    print("No value provided")

# isinstance() narrowing
obj: object = get_value()

if isinstance(obj, str):
    # obj is narrowed to str
    print(obj.upper())
```

Type narrowing does not occur with `or` as type union semantics do not exist in Sharpy:

```python
if isinstance(x, int) or isinstance(x, str):
    # x is not narrowed
```

## Narrowing Rules

- `is not None` narrows nullable type (`T?`) to non-nullable (`T`)
- `is None` narrows to never-type in the `if` branch
- `isinstance(x, Type)` narrows `x` to `Type` in the `if` branch
- Narrowing only affects the scope of the conditional block

*Implementation: ✅ Native - C# supports flow analysis for nullable types.*
