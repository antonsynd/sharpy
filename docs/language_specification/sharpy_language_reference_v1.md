# Sharpy Language Reference

See [introduction.md](introduction.md) for goals, principles, and philosophy.

See [version_guide.md](version_guide.md) for version features and target compatibility.

## Lexical Structure **[v0.1.0]**

See [source_files.md](source_files.md) for file format, line structure, and continuation rules.

See [identifiers.md](identifiers.md) for identifier syntax, naming conventions, and backtick escaping.

See [keywords.md](keywords.md) for reserved keywords.

See [indentation.md](indentation.md) for indentation rules.

See [comments.md](comments.md) for comment syntax.

---

## Literals **[v0.1.0]**

See [integer_literals.md](integer_literals.md) for integer literals and suffixes.

See [float_literals.md](float_literals.md) for float literals and suffixes.

See [extended_numeric_literals.md](extended_numeric_literals.md) for binary, hex, octal, and scientific notation.

See [string_literals.md](string_literals.md) for string syntax, escape sequences, and raw strings.

See [fstrings.md](fstrings.md) for formatted string literals (f-strings).

See [boolean_literals.md](boolean_literals.md) for `True` and `False`.

See [none_literal.md](none_literal.md) for `None` literal and its semantics.

See [ellipsis_literal.md](ellipsis_literal.md) for the `...` placeholder.

See [empty_set_literal.md](empty_set_literal.md) for the `{/}` empty set literal.

---

## Types **[v0.1.0]**

See [primitive_types.md](primitive_types.md) for built-in primitive types and arrays.

See [string_type.md](string_type.md) for string type and UTF-16 semantics.

See [type_annotations.md](type_annotations.md) for type annotation syntax.

See [type_hierarchy.md](type_hierarchy.md) for the type hierarchy and object model.

See [nullable_types.md](nullable_types.md) for nullable type semantics.

---

## Function Types **[v0.1.3]**

See [function_types.md](function_types.md) for function type syntax, compatibility, and usage.

---

## Operators **[v0.1.1]**

See [null_coalescing_operator.md](null_coalescing_operator.md) for the `??` operator.

See [null_conditional_access.md](null_conditional_access.md) for the `?.` operator.

See [type_narrowing.md](type_narrowing.md) for type narrowing rules with `is not None` and `isinstance()`.

---

## Collection Types **[v0.1.1]**

See [collection_types.md](collection_types.md) for collection types, methods, and .NET interop.

### Collection Literals **[v0.1.1]**

See [del_statement.md](del_statement.md) for the `del` statement **[v0.2.0]**.

---

## Operators **[v0.1.0]**

See [arithmetic_operators.md](arithmetic_operators.md) for arithmetic operators and numeric type promotion.

See [comparison_operators.md](comparison_operators.md) for comparison operators.

See [comparison_chaining.md](comparison_chaining.md) for chained comparisons.

See [logical_operators.md](logical_operators.md) for `and`, `or`, `not`.

See [bitwise_operators.md](bitwise_operators.md) for bitwise operations.

See [string_operators.md](string_operators.md) for string concatenation and repetition.

See [membership_operators.md](membership_operators.md) for `in` and `not in`.

See [identity_operators.md](identity_operators.md) for `is` and `is not`.

See [assignment_operators.md](assignment_operators.md) for assignment operators.

See [operator_precedence.md](operator_precedence.md) for operator precedence table.

---

## Expressions **[v0.1.0]**

See [expressions.md](expressions.md) for primary expressions, member access, index access, function calls, conditional expressions, and expression evaluation order.

See [type_casting.md](type_casting.md) for the `to` operator and type casting.

See [lambdas.md](lambdas.md) for lambda expressions.

---

## Statements **[v0.1.0]**

See [statements.md](statements.md) for expression statements, variable declaration and assignment, constants.

## Variable Scoping Rules [v0.1.0]

See [variable_scoping.md](variable_scoping.md) for:
- No `global` or `nonlocal` keywords
- Block-scoped vs containing-scope constructs
- Variable shadowing
- Variable declaration and assignment
- No declaration without assignment

### Pass Statement

See [pass_statement.md](pass_statement.md) for the pass statement.

### Break and Continue

See [break_continue.md](break_continue.md) for break and continue statements.

### Return Statement

See [return_statement.md](return_statement.md) for the return statement.

### Assert Statement

See [assert_statement.md](assert_statement.md) for the assert statement.

---

## Control Flow **[v0.1.0]**

See [if_statement.md](if_statement.md) for if/elif/else statements.

See [while_statement.md](while_statement.md) for while loops.

See [for_statement.md](for_statement.md) for for loops.

See [loop_else.md](loop_else.md) for else clauses on loops.

---

## Exception Handling **[v0.1.0]**

See [exception_handling.md](exception_handling.md) for exception types, try/except/finally, and raise statements.

---

## Functions **[v0.1.0]**

See [function_definition.md](function_definition.md) for function definition syntax, rules, and placeholder bodies.

See [function_parameters.md](function_parameters.md) for:
- Default parameters (compile-time constant requirement)
- Named (keyword) arguments
- Variadic arguments (*args) - homogeneously typed
- Unpacking iterables with *
- C# interop with params arrays
- No **kwargs support
- Positional-only and keyword-only parameters

---

## Classes **[v0.1.0]**

See [classes.md](classes.md) for basic class definition, field declarations, instance methods, and rules.

See [constructors.md](constructors.md) for constructor overloading and constructor chaining **[v0.1.2]**.

---

## Imports **[v0.1.0]**

See [import_statements.md](import_statements.md) for import and from-import syntax variations.

See [module_resolution.md](module_resolution.md) for how module names are resolved.

See [module_system.md](module_system.md) for package structure, `__init__.spy` files, and circular import handling.

---

## Structs **[v0.1.2]**

See [structs.md](structs.md) for struct definition, usage, value semantics, and comparison with classes.

---

## Interfaces **[v0.1.2]**

See [interfaces.md](interfaces.md) for interface definition, implementation, generic interfaces, interface inheritance, default methods, conflict resolution, and dunder methods in interfaces.

---

## Inheritance **[v0.1.2]**

See [inheritance.md](inheritance.md) for single class inheritance, multiple interface implementation, super() usage, and abstract classes.

---

## Decorators **[v0.1.2]**

See [decorators.md](decorators.md) for @static, @virtual, @override, @abstract, @final, and access modifiers (@public, @private, @protected, @internal).

---
## Generics **[v0.1.3]**

See [generics.md](generics.md) for generic classes, generic methods, and type constraints.

---
## Enumerations **[v0.1.4]**

See [enums.md](enums.md) for enum definition, usage, and flags.

---
## Operator Overloading **[v0.1.4]**

See [operator_overloading.md](operator_overloading.md) for dunder methods, arithmetic operators, comparison operators, and container operations.

---
## Pattern Matching **[v0.1.6]**

See [match_statement.md](match_statement.md) for match statement syntax and pattern matching.

---
## Type Aliases **[v0.1.7]**

See [type_aliases.md](type_aliases.md) for type alias syntax.

---
## Tagged Unions (Algebraic Data Types) **[v0.2.0]**

Tagged unions allow cases to carry associated data:

```python
# Generic Result type (like Rust's Result)
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Optional type (like Rust's Option)
enum Optional[T]:
    case Some(value: T)
    case Nothing()

# Tree structure
enum BinaryTree[T]:
    case Leaf(value: T)
    case Node(left: BinaryTree[T], right: BinaryTree[T])
```

**Unit Cases (No Data):**

Cases that carry no associated data can be defined with or without parentheses:

```python
enum Option[T]:
    case Some(value: T)
    case Nothing           # No parentheses needed for unit case
    # case Nothing()       # Also valid, but parentheses are optional

enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

enum LoadState:
    case NotStarted        # Unit case
    case Loading           # Unit case
    case Loaded(data: str) # Data case
    case Failed(error: str) # Data case
```

**Pattern Matching Unit Cases:**

When pattern matching, unit cases also don't require parentheses:

```python
match opt:
    case Option.Some(v): print(v)
    case Option.Nothing: print("none")  # No parens in pattern

match state:
    case LoadState.NotStarted: start_loading()
    case LoadState.Loading: show_spinner()
    case LoadState.Loaded(data): display(data)
    case LoadState.Failed(err): show_error(err)
```

### Creating Values

Tagged union cases are created using the enum type name followed by the case name:

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Create values using Type.Case() syntax
success: Result[int, str] = Result.Ok(42)
failure: Result[int, str] = Result.Err("Something went wrong")
```

**Note:** Case names follow the same casing as defined in the enum declaration (typically `PascalCase`). The syntax `Result.Ok(42)` is a constructor call that creates an instance of the `Ok` case. This of course is just a convention and is not enforced by the compiler.

### Pattern Matching

```python
def divide(a: double, b: double) -> Result[double, str]:
    if b == 0:
        return Result.Err("Division by zero")
    return Result.Ok(a / b)

result = divide(10, 2)
match result:
    case Result.Ok(value):
        print(f"Success: {value}")
    case Result.Err(error):
        print(f"Error: {error}")
```

### Methods on Tagged Unions

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

    def is_ok(self) -> bool:
        match self:
            case Result.Ok():
                return True
            case Result.Err():
                return False

    def unwrap(self) -> T:
        match self:
            case Result.Ok(value):
                return value
            case Result.Err(error):
                raise Exception(f"Called unwrap on Err: {error}")

    def unwrap_or(self, default: T) -> T:
        match self:
            case Result.Ok(value):
                return value
            case Result.Err():
                return default
```

*Implementation: 🔄 Lowered - Abstract base class + sealed nested case classes:*

```csharp
public abstract class Result<T, E> {
    private Result() { }

    public sealed class Ok : Result<T, E> {
        public T Value { get; }
        public Ok(T value) => Value = value;
        public void Deconstruct(out T value) => value = Value;
    }

    public sealed class Err : Result<T, E> {
        public E Error { get; }
        public Err(E error) => Error = error;
        public void Deconstruct(out E error) => error = Error;
    }
}
```

---

## Try expressions **[v0.2.0]**

The `Result[T, E]` type can be implicitly created via
`try` expressions. A `try` expression wraps the value of
the expression in `Result[T, E]` where `E`, if not
specified, is always the base `Exception` type, and `T` is
the type of the expression. If the expression raises an
exception, then the result holds its `Err` case.

```python
x = try int("some string")  # x is of type Result[int, Exception]
```

A `try` expression can be specified for a specific type
where if the expression throws that type, then it is caught
inside `Err` case. Other types become uncaught exceptions
that must be handled by other means, e.g. `try/except/finally`.

```python
x = try[ValueError] int("some string")  # x is of type Result[int, ValueError]
```

It is not an error if the expression would never raise an
exception. In such cases, the result type is always `Result[T, Exception]` where `T` is the expression's type.

**Precedence Rules:**

The `try` expression has low precedence, binding only to the immediately following primary expression and its arguments:

```python
# try binds to the function call only
x = try int("abc") + 5       # Parsed as: (try int("abc")) + 5
                             # If int() succeeds: Result.Ok + 5 = ERROR (can't add)
                             # Typically you'd unwrap first

# Use parentheses for clarity or different grouping
x = try (int("abc") + 5)     # Parsed as: try (int("abc") + 5)
                             # Exception in either int() or + is caught

# With conditional
y = try foo() if cond else bar()   # Parsed as: (try foo()) if cond else bar()
                                   # try only applies to foo(), not bar()

# Parentheses make intent clear
y = try (foo() if cond else bar())  # try applies to entire conditional
```

*Implementation: 🔄 Lowered - `try`/`catch` pattern wrapping the expression.*

---

## Maybe expressions **[v0.2.0]**

Optionals can be implicitly created via `maybe` expressions.
A `maybe` expression wraps the value of the expression in
`Optional[T]` where `T` is the type of the expression.
If the expression is `None`, then the result
holds its `Nothing` case.

```python
d: dict[str, int] = {"y": 5}
x = maybe d.get("x")  # x is of type Optional[int]
```

It is a type-checking error if the expression does not return
a nullable type (`T?`).

```python
# ✅ Valid - dict.get() returns T?
d: dict[str, int] = {}
x = maybe d.get("key")       # OK: get() returns int?

# ✅ Valid - explicitly nullable
value: int? = get_optional_value()
y = maybe value              # OK: value is int?

# ❌ Invalid - expression is not nullable
s: str = "hello"
z = maybe s.upper()          # ERROR: upper() returns str, not str?

n: int = 42
w = maybe n                  # ERROR: n is int, not int?
```

**Precedence Rules:**

Like `try`, the `maybe` expression has low precedence:

```python
x = maybe d.get("key") ?? 0    # Parsed as: (maybe d.get("key")) ?? 0
                               # ERROR: Optional[int] ?? int doesn't work directly

# Use the Optional's methods instead
x = (maybe d.get("key")).unwrap_or(0)
```

---

## Comprehensions **[v0.1.8]**

See [comprehensions.md](comprehensions.md) for list, dict, and set comprehensions.

---
## Walrus Operator **[v0.1.8]**

See [walrus_operator.md](walrus_operator.md) for assignment expressions using :=.

---
## Properties **[v0.1.2]**

See [properties.md](properties.md) for auto-properties and function-style properties.

---
## Context Managers **[v0.2.0]**

See [context_managers.md](context_managers.md) for with statement and context manager protocol.

---
## Events **[v0.2.0]**

See [events.md](events.md) for event declaration and handling.

---
## Async Programming **[v0.2.0+]**

See [async_programming.md](async_programming.md) for async/await and AsyncIterator.

---
## Built-in Functions **[v0.1.0+]**

See [builtin_functions.md](builtin_functions.md) for type conversion, type checking, collection operations, I/O, math, and object builtins.

---
## .NET Interop **[v0.1.0]**

See [dotnet_interop.md](dotnet_interop.md) for importing .NET types, extension methods, and IDisposable.

---
## Naming Conventions Summary **[v0.1.0]**

| Identifier Type | Sharpy Convention | Compiled C# Form |
|-----------------|-------------------|------------------|
| Module | `snake_case` | `PascalCase` namespace |
| Class | `PascalCase` | (unchanged) |
| Struct | `PascalCase` | (unchanged) |
| Interface | `IPascalCase` | (unchanged) |
| Method/Function | `snake_case` | `PascalCase` |
| Parameter | `snake_case` | `camelCase` |
| Local variable | `snake_case` | (unchanged) |
| Constant | `CAPS_SNAKE_CASE` | (unchanged) |
| Enum type | `PascalCase` | (unchanged) |
| Enum value | `CAPS_SNAKE_CASE` | `PascalCase` |

---

## Program Entry Point **[v0.1.0]**

The entry point is either a file with top-level statements or a `main()` function:

```python
# Option 1: Top-level statements
print("Hello, World!")

# Option 2: main() function
def main():
    print("Hello, World!")
```

**Note:** The Python idiom `if __name__ == "__main__":` does not exist in Sharpy.

*Implementation: 🔄 Lowered*
- *Top-level statements wrapped in generated `Main()` method*
- *Module code wrapped in `public static class Exports`*

---

## Features Deferred to v2.0+

The following features require .NET 7+ runtime or C# 11+ and cannot be supported when targeting Unity or .NET 5/6:

| Feature | Required C# | Required .NET | Reason |
|---------|-------------|---------------|--------|
| `@file` access modifier | C# 11 | .NET 6+ | File-scoped types |
| List patterns `case [a, b]:` | C# 11 | Any | Compiler feature |
| Static abstract interface members | C# 11 | .NET 7 | Runtime support |
| Generic math constraints | C# 11 | .NET 7 | BCL interfaces |
| `required` members | C# 11 | .NET 7 | Attribute + compiler |
| Record structs | C# 10 | Any | Compiler feature |
| `field` keyword in properties | C# 13 | Any | Compiler feature |
| Extension properties/operators | C# 14 | Any | Compiler feature |
| User-defined `+=` operators | C# 14 | Any | Compiler feature |

---

## Version Summary

| Version | Key Additions |
|---------|---------------|
| **v0.1.0** | Core syntax, primitives, functions, classes, exceptions, imports, type hierarchy (`object` base), dunder invocation rules |
| **v0.1.1** | Nullable types (`T?`), `?.`, `??`, collections, slicing |
| **v0.1.2** | Structs, interfaces, inheritance, decorators, access modifiers, function overloading, properties |
| **v0.1.3** | Generics, type constraints, lambdas |
| **v0.1.4** | Enums, operator overloading via dunders |
| **v0.1.5** | F-strings, extended literals, comparison chaining, loop else |
| **v0.1.6** | Pattern matching (`match`/`case`), guards, all pattern types |
| **v0.1.7** | Type aliases, variable shadowing |
| **v0.1.8** | Comprehensions, walrus operator |
| **v0.2.0+** | Context managers (`with`), async/await, generators (`yield`), tagged unions (ADTs), `maybe`/`try` expressions, events, `del` statement |
| **v1.0** | Stable release |
| **v2.0+** | Features requiring C# 11+ / .NET 7+ |

---

## See Also

- **Type System** - Detailed type semantics, interfaces, and generics
- **Compiler Design** - Implementation details and code generation
- **C# 9.0 Compatibility Matrix** - Full transpilation reference
