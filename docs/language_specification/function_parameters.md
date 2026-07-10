# Function Parameters

This document provides an overview of function parameter types in Sharpy. For detailed information on specific topics, see the linked pages below.

## Overview

Sharpy supports several parameter types:
- **Required parameters** - Must be provided by the caller
- **Default parameters** - Optional with compile-time constant defaults (see [Function Default Parameters](function_default_parameters.md))
- **Named arguments** - Pass arguments by name for clarity
- **Variadic arguments** - Accept variable number of arguments with `*args` (see [Function Variadic Arguments](function_variadic_arguments.md))
- **Flexible arguments** - Positional-only, keyword-only, and kwargs support (see [Flexible Arguments](flexible_arguments.md))

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

## Flexible Arguments

Sharpy provides **positional-only (`/`)** and **keyword-only (`*`)** parameter markers for zero-cost compile-time validation of how arguments are passed.

For complete details, see [Flexible Arguments](flexible_arguments.md).

### Quick Example

```python
def search(query: str, /, limit: int = 10, *, case_sensitive: bool = False) -> list[str]:
    pass

search("hello", 20, case_sensitive=True)  # ✅ Valid
search(query="hello")                      # ❌ ERROR: 'query' is positional-only
search("hello", 20, True)                  # ❌ ERROR: 'case_sensitive' is keyword-only
```

## Function and Method Overloading

Sharpy supports overloading following C# semantics. Multiple functions or methods can share the same name if they differ in parameter count or types. This applies to both module-level functions and class methods. For detailed examples including module-level and cross-file overloading, see [Function and Method Overloading](method_overloading.md#module-level-function-overloading).

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

> **Authoritative reference:** [Overload Resolution](overload_resolution.md) is the full specification of
> applicability, the betterness tie-break chain, the conversion-cost ranking, and how each resolution
> engine (calls, operator dunders, constructors, builtins) behaves. This section is a summary; where it
> and that page differ, that page governs.

When multiple overloads could match a call, the compiler picks the best one in two phases, modeled on
C#'s better-function-member algorithm (Axiom 1: where Sharpy and C# betterness could disagree, Sharpy
matches C#):

1. **Applicability** — keep the overloads whose arity fits (accounting for defaults and `*args`), whose
   parameter names cover every keyword argument, and to whose parameter types every argument is
   assignable (including the documented primitive widenings such as `int → long → double`).

2. **Betterness** — among applicable overloads, pick the single best using the ordered tie-break chain:
   exact match over conversion → better (lower-cost) implicit conversion → more specific type → fewer
   type parameters → non-variadic over variadic → CLR-level specificity. If no overload is strictly
   better than all others, the call is **ambiguous** (`SPY0353`).

```python
def f(x: int): ...
def f(x: float): ...

f(42)    # Calls f(int) - exact match beats the int→float widening
f(3.14)  # Calls f(float) - exact match
```

### Ambiguous Overloads

If no single overload is better than all others, the call is ambiguous:

```python
def f(x: int, y: float): ...
def f(x: float, y: int): ...

f(1, 2)  # ERROR (SPY0353): Ambiguous - neither candidate is better at both positions
```

**Resolution:** Use explicit type conversions to disambiguate:
```python
f(1, 2.0)         # Calls f(int, float)
f(1.0, 2)         # Calls f(float, int)
f(1 to float, 2)  # Explicitly calls f(float, int)
```

### Default Parameters and Overloads

Default parameters widen the applicable set, which can create ambiguity:

```python
def greet(name: str): ...
def greet(name: str, greeting: str = "Hello"): ...

greet("Alice")  # Ambiguous - both overloads are applicable to a single string argument
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

For the complete resolution algorithm, see [Overload Resolution](overload_resolution.md). For the C#
rules it mirrors, see the [C# Language Specification §12.6.4: Overload Resolution](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#1264-overload-resolution).

## See Also

- [Overload Resolution](overload_resolution.md) - Authoritative applicability and betterness rules
- [Flexible Arguments](flexible_arguments.md) - Positional-only, keyword-only, and kwargs support
- [Function Default Parameters](function_default_parameters.md) - Detailed guide to default parameter values and compile-time constant requirements
- [Function Variadic Arguments](function_variadic_arguments.md) - Comprehensive coverage of `*args` and unpacking
- [Parameter Modifiers](parameter_modifiers.md) - `ref`, `out`, and `in` pass-by-reference parameters
- [Function Definition](function_definition.md) - Basic function syntax and rules
- [Function Types](function_types.md) - Function type syntax and compatibility
