# Type Parameter Defaults

Generic type parameters can have default types, allowing callers to omit type arguments when the default is appropriate. Inspired by Python PEP 696.

## Syntax

Use `= Type` after the type parameter name:

```python
class Box[T = int]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def get(self) -> T:
        return self.value
```

When using the class, the type argument can be omitted to use the default:

```python
b = Box[int](42)      # Explicit: T = int
s = Box[str]("hello") # Explicit: T = str
```

## Ordering Rule

Once one type parameter has a default, all subsequent parameters must also have defaults:

```python
# OK: defaults at the end
class Pair[K, V = str]:
    pass

# OK: all have defaults
class Config[K = str, V = int]:
    pass

# ERROR (SPY0395): non-default follows default
class Bad[T = int, U]:   # U has no default but T does
    pass
```

## Partial Defaults

When only some parameters have defaults, parameters without defaults must come first:

```python
class Container[K, V = list[K]]:
    key: K
    value: V

    def __init__(self, key: K, value: V):
        self.key = key
        self.value = value
```

## Constraint Satisfaction

Default types must satisfy any constraints on the type parameter. If a default type violates a constraint, the compiler emits SPY0396:

```python
interface IComparable:
    def compare_to(self, other: Self) -> int:
        ...

# ERROR (SPY0396) if default type doesn't satisfy constraint
class Sorted[T: IComparable = int]:
    pass
```

## Diagnostics

| Code | Level | Description |
|------|-------|-------------|
| SPY0395 | Error | Type parameter without default follows one with a default |
| SPY0396 | Error | Default type violates type parameter constraint |

## Generated C#

Type parameter defaults are resolved at compile time. The generated C# uses concrete type arguments — there is no runtime default mechanism:

```python
class Box[T = int]:
    value: T
```

generates a generic class `Box<T>` in C#. The default is used during type inference when the caller omits the type argument.

*Implementation*
- *✅ Implemented — `TypeParameterDef.DefaultType` property, parsed in `Parser.Definitions.cs`*
- *Ordering validation: SPY0395*
- *Constraint satisfaction: SPY0396*
