# Conversion Operators (`@implicit` and `@explicit`)

Sharpy supports user-defined type conversions through decorated dunder methods. Conversions can be marked as implicit (automatic) or explicit (requires the `to` operator).

## Overview

| Decorator | Conversion Type | Invocation | Use Case |
|-----------|-----------------|------------|----------|
| `@implicit` | Automatic | Assignment, argument passing | Safe, lossless conversions |
| `@explicit` | Manual | `value to Type` operator | Potentially lossy or semantic conversions |

## Defining Conversion Operators

### Conversion TO Another Type

Use a dunder method that returns the target type:

```python
class Celsius:
    _degrees: double

    def __init__(self, degrees: double):
        self._degrees = degrees

    # Implicit conversion to double (safe, no data loss)
    @implicit
    def __double__(self) -> double:
        return self._degrees

    # Explicit conversion to int (lossy: truncates decimal)
    @explicit
    def __int__(self) -> int:
        return int(self._degrees)

    # Explicit conversion to Fahrenheit (semantic change)
    @explicit
    def __Fahrenheit__(self) -> Fahrenheit:
        return Fahrenheit(self._degrees * 9.0 / 5.0 + 32.0)
```

### Conversion FROM Another Type

Use a static method with the `@implicit` or `@explicit` decorator:

```python
class Celsius:
    _degrees: double

    def __init__(self, degrees: double):
        self._degrees = degrees

    # Implicit conversion from double (natural construction)
    @implicit
    @staticmethod
    def __from_double__(value: double) -> Celsius:
        return Celsius(value)

    # Explicit conversion from Fahrenheit (semantic change)
    @explicit
    @staticmethod
    def __from_Fahrenheit__(value: Fahrenheit) -> Celsius:
        return Celsius((value.degrees - 32.0) * 5.0 / 9.0)

    # Explicit conversion from string (may fail)
    @explicit
    @staticmethod
    def __from_str__(value: str) -> Celsius:
        return Celsius(double.parse(value))
```

## Implicit Conversions

Implicit conversions happen automatically in these contexts:

```python
class Distance:
    _meters: double

    @implicit
    def __double__(self) -> double:
        return self._meters

    @implicit
    @staticmethod
    def __from_double__(value: double) -> Distance:
        return Distance(value)

# 1. Assignment
d: Distance = Distance(100.0)
meters: double = d              # Implicit: Distance → double

# 2. Argument passing
def print_meters(m: double):
    print(f"{m} meters")

print_meters(d)                 # Implicit: Distance → double

# 3. Return statements
def get_length() -> double:
    return Distance(50.0)       # Implicit: Distance → double

# 4. Arithmetic with compatible types
total: double = d + 10.0        # Implicit: Distance → double, then double + double

# 5. Construction from source type
dist: Distance = 42.0           # Implicit: double → Distance
```

### Implicit Conversion Guidelines

Only define implicit conversions when:
- No data is lost
- No exception can be thrown
- The conversion is semantically natural
- There's no significant performance cost

```python
# ✅ Good implicit conversions
@implicit
def __double__(self) -> double:     # int → double (widening)
    return double(self._value)

@implicit
def __str__(self) -> str:           # Any → str (always works via ToString)
    return f"{self._value}"

# ❌ Bad implicit conversions (should be explicit)
@implicit
def __int__(self) -> int:           # May truncate or overflow
    return int(self._large_value)

@implicit
def __SomeType__(self) -> SomeType: # May throw
    return SomeType.parse(self._data)
```

## Explicit Conversions

Explicit conversions require the `to` operator:

```python
class Temperature:
    _kelvin: double

    @explicit
    def __Celsius__(self) -> Celsius:
        return Celsius(self._kelvin - 273.15)

    @explicit
    def __int__(self) -> int:
        return int(self._kelvin)

temp = Temperature(300.0)

# Must use explicit cast
celsius = temp to Celsius       # ✅ OK: explicit conversion
rounded = temp to int           # ✅ OK: explicit conversion

# celsius = temp                # ❌ ERROR: no implicit conversion
```

### Combining with Safe Casting

Explicit conversions integrate with the nullable `to T?` form:

```python
class UserId:
    _value: int

    @explicit
    @staticmethod
    def __from_str__(value: str) -> UserId:
        return UserId(int.parse(value))  # May throw

# Safe casting with explicit conversion
input_str = "not_a_number"
user_id = input_str to UserId?          # None (conversion failed)

input_str = "42"
user_id = input_str to UserId?          # UserId(42)
```

## Conversion Dunder Method Naming

### TO Conversions (Instance Methods)

| Target Type | Dunder Name | Example |
|-------------|-------------|---------|
| Built-in types | `__typename__` | `__int__`, `__double__`, `__str__`, `__bool__` |
| User types | `__TypeName__` | `__Celsius__`, `__Vector3__` |
| Generic types | `__TypeName_T__` | `__List_int__` (rare) |

### FROM Conversions (Static Methods)

| Source Type | Dunder Name | Example |
|-------------|-------------|---------|
| Built-in types | `__from_typename__` | `__from_int__`, `__from_str__` |
| User types | `__from_TypeName__` | `__from_Celsius__`, `__from_Vector3__` |

## Conversion Chains

The compiler does **not** chain user-defined conversions:

```python
class A:
    @implicit
    def __B__(self) -> B: ...

class B:
    @implicit
    def __C__(self) -> C: ...

a = A()
c: C = a    # ❌ ERROR: No direct A → C conversion
c: C = (a to B) to C  # ✅ OK: explicit chain
```

However, one user-defined conversion can chain with standard conversions:

```python
class Meters:
    @implicit
    def __double__(self) -> double:
        return self._value

m = Meters(10.0)
x: decimal = m    # ✅ OK: Meters → double (user) → decimal (standard)
```

## Conversions and Inheritance

Conversion operators are not inherited but can be defined on base/derived types:

```python
class Animal:
    @implicit
    def __str__(self) -> str:
        return "Animal"

class Dog(Animal):
    # Inherits __str__ from Animal? No — must redefine
    @implicit
    def __str__(self) -> str:
        return f"Dog: {self.name}"
```

**Restriction:** Cannot define a conversion between types in an inheritance relationship:

```python
class Base: ...
class Derived(Base): ...

class Derived(Base):
    @implicit
    def __Base__(self) -> Base:  # ❌ ERROR: implicit upcast already exists
        return self
```

## Operator Overload Interaction

Implicit conversions enable operators to work across types:

```python
class Vector2:
    x: double
    y: double

    @implicit
    @staticmethod
    def __from_tuple__(value: tuple[double, double]) -> Vector2:
        return Vector2(value[0], value[1])

    def __add__(self, other: Vector2) -> Vector2:
        return Vector2(self.x + other.x, self.y + other.y)

v = Vector2(1.0, 2.0)
result = v + (3.0, 4.0)  # Tuple implicitly converts to Vector2
```

## C# Emission

```python
# Sharpy
class Temperature:
    _celsius: double

    def __init__(self, celsius: double):
        self._celsius = celsius

    # Implicit conversion TO double
    @implicit
    def __double__(self) -> double:
        return self._celsius

    # Explicit conversion TO int
    @explicit
    def __int__(self) -> int:
        return int(self._celsius)

    # Implicit conversion FROM double
    @implicit
    @staticmethod
    def __from_double__(value: double) -> Temperature:
        return Temperature(value)

    # Explicit conversion FROM string
    @explicit
    @staticmethod
    def __from_str__(value: str) -> Temperature:
        return Temperature(double.parse(value))
```

```csharp
// C# 9.0
public class Temperature
{
    private double _celsius;

    public Temperature(double celsius)
    {
        _celsius = celsius;
    }

    // Implicit conversion TO double
    public static implicit operator double(Temperature t)
    {
        return t._celsius;
    }

    // Explicit conversion TO int
    public static explicit operator int(Temperature t)
    {
        return (int)t._celsius;
    }

    // Implicit conversion FROM double
    public static implicit operator Temperature(double value)
    {
        return new Temperature(value);
    }

    // Explicit conversion FROM string
    public static explicit operator Temperature(string value)
    {
        return new Temperature(double.Parse(value));
    }
}
```

**Usage emission:**

```python
# Sharpy
temp = Temperature(25.0)
d: double = temp            # Implicit
n: int = temp to int        # Explicit
temp2: Temperature = 30.0   # Implicit from
temp3 = "20.5" to Temperature  # Explicit from
```

```csharp
// C# 9.0
var temp = new Temperature(25.0);
double d = temp;            // Implicit operator called
int n = (int)temp;          // Explicit operator called
Temperature temp2 = 30.0;   // Implicit operator called
var temp3 = (Temperature)"20.5";  // Explicit operator called
```

## Standard Conversion Dunders

The following dunders map to built-in C# types:

| Dunder | Target Type | C# Operator |
|--------|-------------|-------------|
| `__bool__` | `bool` | `operator bool` + `operator true`/`false` |
| `__int__` | `int` | `operator int` |
| `__long__` | `long` | `operator long` |
| `__float__` | `float` | `operator float` |
| `__double__` | `double` | `operator double` |
| `__decimal__` | `decimal` | `operator decimal` |
| `__str__` | `str` | `operator string` (also `ToString()`) |
| `__byte__` | `byte` | `operator byte` |
| `__sbyte__` | `sbyte` | `operator sbyte` |
| `__short__` | `short` | `operator short` |
| `__ushort__` | `ushort` | `operator ushort` |
| `__uint__` | `uint` | `operator uint` |
| `__ulong__` | `ulong` | `operator ulong` |

## Restrictions

1. **Cannot define both implicit and explicit** for the same source→target pair
2. **Source or target must be the containing type** (at least one)
3. **Cannot convert to/from `object`** (implicit conversions exist)
4. **Cannot convert to/from interfaces** (use implementation instead)
5. **Cannot convert between inheritance-related types**
6. **At most one user-defined conversion per operation**

```python
# ❌ Invalid: both implicit and explicit
class Foo:
    @implicit
    def __int__(self) -> int: ...
    @explicit
    def __int__(self) -> int: ...  # ERROR: duplicate conversion

# ❌ Invalid: neither source nor target is containing type
class Foo:
    @implicit
    @staticmethod
    def __convert__(a: str) -> int: ...  # ERROR: must involve Foo

# ❌ Invalid: conversion to interface
class Foo:
    @implicit
    def __IDisposable__(self) -> IDisposable: ...  # ERROR
```

*Implementation: ✅ Native*
- *`@implicit` + dunder → `public static implicit operator`*
- *`@explicit` + dunder → `public static explicit operator`*
- *TO conversions → operator with containing type as parameter*
- *FROM conversions → operator with containing type as return type*
- *`to` operator triggers explicit conversions via C# cast*

## See Also

- [Dunder Methods](dunder_methods.md) — Overview of special methods
- [Type Casting](type_casting.md) — The `to` operator
- [Operator Overloading](operator_overloading.md) — Custom operators
- [Arithmetic Operators](arithmetic_operators.md) — Numeric type promotion
