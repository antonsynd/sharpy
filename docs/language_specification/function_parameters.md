# Function Parameters

This document provides an overview of function parameter types in Sharpy. For detailed information on specific topics, see the linked pages below.

## Overview

Sharpy supports several parameter types:
- **Required parameters** - Must be provided by the caller
- **Default parameters** - Optional with compile-time constant defaults (see [Function Default Parameters](function_default_parameters.md))
- **Named arguments** - Pass arguments by name for clarity
- **Variadic arguments** - Accept variable number of arguments with `*args` (see [Function Variadic Arguments](function_variadic_arguments.md))

## Default Parameters

Functions can specify default values for parameters. Parameters with defaults must come after required parameters.

```python
def greet(name: str, greeting: str = "Hello") -> str:
    return f"{greeting}, {name}!"
```

**Key Points:**
- Default values must be compile-time constants
- Eliminates Python's "mutable default argument" pitfall
- Supports numeric, string, boolean literals, `None`, enums, and constants

For complete details on default parameters, including the compile-time constant requirement and patterns for optional mutable arguments, see [Function Default Parameters](function_default_parameters.md).

## Named (Keyword) Arguments

Sharpy supports calling functions with named arguments, allowing callers to specify parameter values by name rather than position:

```python
def create_user(name: str, age: int, active: bool = True) -> User:
    pass

# Positional arguments
user1 = create_user("Alice", 30, False)

# Named arguments
user2 = create_user(name="Bob", age=25)
user3 = create_user(age=25, name="Bob")  # Order doesn't matter for named args

# Mixed: positional first, then named
user4 = create_user("Charlie", age=35, active=False)

# ❌ Invalid: named before positional
user5 = create_user(name="Dave", 40)  # ERROR: positional argument follows keyword argument
```

### Named Argument Rules

- Named arguments must follow all positional arguments
- Once a named argument is used, all subsequent arguments must be named
- A parameter cannot be specified both positionally and by name

*Implementation*
- *✅ Native - Direct mapping to C# named arguments.*

## Variadic Arguments (`*args`)

Sharpy supports variadic arguments using the `*args` syntax for accepting a variable number of arguments. Unlike Python's fully dynamic `*args`, Sharpy's variadic arguments are **homogeneously typed**: all arguments must be of the same type `T`.

```python
def sum_all(*numbers: int) -> int:
    result = 0
    for n in numbers:
        result += n
    return result

total = sum_all(1, 2, 3)  # 6
```

**Key Points:**
- All variadic arguments must be the same type
- `*args` must be the last parameter
- Maps directly to C# `params` arrays
- Supports unpacking with `*` operator

For complete details on variadic arguments, including unpacking rules, C# interop, and examples, see [Function Variadic Arguments](function_variadic_arguments.md).

## No `**kwargs` Support

Sharpy does not support `**kwargs` (variadic keyword arguments), as .NET has no direct equivalent.

**Alternatives:**

```python
# Instead of: def configure(**kwargs) -> None

# Option 1: Named parameters with defaults
def configure(host: str = "localhost", port: int = 8080, debug: bool = False) -> None:
    pass

# Option 2: Typed configuration class
class Config:
    host: str = "localhost"
    port: int = 8080
    debug: bool = False

def configure(config: Config) -> None:
    pass

# Option 3: Dictionary parameter (loses type safety on values)
def configure(options: dict[str, object]) -> None:
    host = options.get("host") ?? "localhost"
    port = options.get("port") to int? ?? 8080
    # ...
```

## Positional-Only and Keyword-Only Parameters

Sharpy does not support Python's positional-only (`/`) or keyword-only (`*`) parameter markers. All parameters can be passed either positionally or by name.

```python
def process(value: int) -> str:
    return f"Integer: {value}"

def process(value: str) -> str:
    return f"String: {value}"

def process(value: int, multiplier: int) -> str:
    return f"Result: {value * multiplier}"
```

**Rules:**
- Overloads resolved by parameter count and types
- Named arguments filter candidates to overloads with matching parameter names (see [Named Arguments in Overload Resolution](#named-arguments-in-overload-resolution))

*Implementation*
- *✅ Native - C# supports method overloading.*

## Overload Resolution Rules

Sharpy follows **C# overload resolution rules exactly**. When multiple overloads could match a call, the compiler selects the "best" overload using C#'s algorithm.

### Resolution Algorithm Summary

1. **Identify candidate overloads**: All overloads where argument count matches parameter count (accounting for defaults and `*args`)

2. **Filter applicable overloads**: Remove overloads where any argument cannot be converted to the corresponding parameter type

3. **Find best overload**: Among applicable overloads, select the one that is "better" than all others

### "Better" Overload Rules

An overload A is better than overload B if:

1. **More specific types**: Arguments match A's parameter types more specifically
   ```python
   def f(x: int): ...       # More specific
   def f(x: object): ...    # Less specific

   f(42)  # Calls f(int) - int is more specific than object
   ```

2. **Fewer conversions needed**: A requires fewer implicit conversions
   ```python
   def f(x: int): ...
   def f(x: double): ...

   f(42)    # Calls f(int) - no conversion needed
   f(3.14)  # Calls f(double) - exact match
   ```

3. **Non-variadic preferred**: Non-`*args` overloads are preferred over `*args` overloads
   ```python
   def f(x: int): ...
   def f(*args: int): ...

   f(42)  # Calls f(int) - non-variadic preferred
   ```

### Ambiguous Overloads

If no single overload is "better" than all others, the call is ambiguous:

```python
def f(x: int, y: double): ...
def f(x: double, y: int): ...

f(1, 2)  # ERROR: Ambiguous - both equally good after implicit conversions
```

**Resolution:** Use explicit type conversions to disambiguate:
```python
f(1, 2.0)       # Calls f(int, double)
f(1.0, 2)       # Calls f(double, int)
f(1 to double, 2)  # Explicitly calls f(double, int)
```

### Default Parameters and Overloads

Default parameters expand the applicable overloads:

```python
def greet(name: str): ...
def greet(name: str, greeting: str = "Hello"): ...

greet("Alice")  # ERROR: Ambiguous - both overloads applicable
```

**Recommendation:** Avoid overloads that differ only in having additional defaulted parameters.

### Named Arguments in Overload Resolution

Named arguments participate in overload resolution by filtering which overloads are candidates. An overload is only considered if it has a parameter matching each named argument's name:

```python
def do_work(num: int, message: str = "Hello") -> None:
    print(f"{num}: {message}")

def do_work(count: int) -> None:
    print(f"Count: {count}")

do_work(21)         # Calls do_work(count) - standard resolution prefers no optional params
do_work(num=21)     # Calls do_work(num, message) - only this overload has 'num' parameter
do_work(count=21)   # Calls do_work(count) - only this overload has 'count' parameter
```

This allows named arguments to disambiguate between overloads that have the same parameter types but different parameter names.

**Inheritance and Parameter Names:**

When a method is overridden with different parameter names, the compiler uses the *static type* of the receiver to determine valid parameter names:

```python
class Animal:
    def eat(self, food_type: str = "grub") -> None:
        pass

class Monkey(Animal):
    def eat(self, banana_type: str = "green banana") -> None:
        pass

m: Monkey = Monkey()
a: Animal = m

m.eat(banana_type="ripe banana")  # OK - Monkey has 'banana_type'
a.eat(food_type="yummy grub")     # OK - Animal has 'food_type'
m.eat(food_type="grub")           # ERROR - Monkey doesn't have 'food_type'
```

*Implementation*
- *✅ Native - Direct mapping to C# named argument resolution.*

### Reference

For complete details, see the [C# Language Specification: Overload Resolution](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#overload-resolution).

## See Also

- [Function Default Parameters](function_default_parameters.md) - Detailed guide to default parameter values and compile-time constant requirements
- [Function Variadic Arguments](function_variadic_arguments.md) - Comprehensive coverage of `*args` and unpacking
- [Parameter Modifiers](parameter_modifiers.md) - `ref`, `out`, and `in` pass-by-reference parameters
- [Function Definition](function_definition.md) - Basic function syntax and rules
- [Function Types](function_types.md) - Function type syntax and compatibility
