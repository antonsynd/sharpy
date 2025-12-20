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

Properties provide controlled access to object state with support for computed values, validation, and fine-grained access control. Sharpy properties map cleanly to C# properties while maintaining Pythonic readability.

### Property Forms

Sharpy supports two property forms:

| Form | Use Case | Syntax Pattern |
|------|----------|----------------|
| Auto-property | Simple storage with compiler-generated backing field | `property [get\|set\|init]? name: T [= value]` |
| Function-style property | Custom logic, user-provided backing field | `property (get\|set) name(self, ...) -> T:` |

**Key Distinction:**
- **Auto-properties** generate a backing field automatically (opaque to the user)
- **Function-style properties** require the user to provide their own backing field (or compute the value)

### Auto-Properties

Auto-properties generate a backing field and accessors automatically:

```python
class Person:
    # Read-write (default, has both get and set)
    property name: str = "Unknown"
    property age: int              # Zero-initialized (value type)

    # Read-only getter (must have default value OR be set in constructor)
    property get id: int = 0
    property get uuid: str         # Must be set in __init__

    # Init-only (readable, but can only be set at declaration or in constructor)
    property init created_at: datetime   # Must be set in __init__
    property init email: str = "unknown@example.com"

    # Write-only (rare, typically combined with public getter)
    property set password_hash: str

    def __init__(self, name: str, age: int, id: int, uuid: str, email: str, password: str):
        self.name = name
        self.age = age
        self.id = id             # OK: can set read-only in constructor
        self.uuid = uuid         # Required: no default value
        self.created_at = datetime.now()  # Required: init property, no default
        self.email = email       # OK: overrides default
        self.password_hash = hash_password(password)

# After construction:
p = Person("Alice", 30, 1, "abc-123", "alice@example.com", "secret")
p.name = "Bob"           # OK: read-write
p.id = 2                 # ERROR: read-only property (no setter)
p.email = "new@test.com" # ERROR: init-only, cannot set after construction
print(p.password_hash)   # ERROR: write-only property (no getter)
```

**Auto-Property Modifiers:**

The auto-property modifiers (or lack thereof) are mutually exclusive; for a given property
named X, only one of the following are possible within a given class/struct/interface.

| Syntax | Accessors | Readable | Settable in `__init__` | Settable after |
|--------|-----------|----------|------------------------|----------------|
| `property name: T` | get + set | ✅ | ✅ | ✅ |
| `property get name: T` | get only | ✅ | ✅ | ❌ |
| `property set name: T` | set only | ❌ | ✅ | ✅ |
| `property init name: T` | get + init | ✅ | ✅ | ❌ |

**Difference between `property get` and `property init`:**
- `property get name: T` — getter-only; can have a default value or be set in constructor, then immutable
- `property init name: T` — getter + init-only setter; **must** be set at declaration or in every constructor (no zero-initialization); immutable after construction

**Auto-Property Initialization Rules:**

| Modifier | Default Value | Zero-Init (value types) | Must set in `__init__` |
|----------|---------------|-------------------------|------------------------|
| `property` | Optional | ✅ Yes | If no default (ref types) |
| `property get` | Optional | ✅ Yes | If no default (ref types) |
| `property set` | Optional | ✅ Yes | No |
| `property init` | Optional | ❌ No | If no default |

```python
class Example:
    property name: str           # Must be assigned in __init__ (reference type)
    property count: int          # Zero-initialized to 0 (value type)
    property label: str = ""     # Default value provided
    property get id: int = 0     # Read-only with default
    property init token: str     # MUST be set in __init__ (no zero-init allowed)

    def __init__(self, name: str, token: str):
        self.name = name         # Required: no default for reference type
        self.token = token       # Required: init property without default
        # self.count not assigned - will be 0 (value type default)
```

*Implementation: ✅ Native*
```csharp
public string Name { get; set; }
public int Count { get; set; }
public string Label { get; set; } = "";
public int Id { get; } = 0;
public string Token { get; init; }
```

### Function-Style Properties

For properties requiring custom logic (validation, transformation, computation), use function-style syntax. The user must provide their own backing field or compute the value. You cannot combine get/set/init auto-properties with custom logic get/set/init, since the backing field for the auto-property cannot be accessed from the custom logic.

#### Function-Style Getter

```python
class Rectangle:
    width: double
    height: double

    def __init__(self, width: double, height: double):
        self.width = width
        self.height = height

    # Computed property (no backing field needed)
    property get area(self) -> double:
        return self.width * self.height

    property get perimeter(self) -> double:
        return 2 * (self.width + self.height)

    property get is_square(self) -> bool:
        return self.width == self.height

    # Multi-statement bodies work naturally
    property get diagonal(self) -> double:
        w_sq = self.width ** 2
        h_sq = self.height ** 2
        return (w_sq + h_sq) ** 0.5

    # Can reference other properties
    property get description(self) -> str:
        shape = "square" if self.is_square else "rectangle"
        return f"A {shape} with area {self.area}"
```

*Implementation: ✅ Native*
```csharp
public double Area => Width * Height;
public double Perimeter => 2 * (Width + Height);
public bool IsSquare => Width == Height;

public double Diagonal {
    get {
        var wSq = Width * Width;
        var hSq = Height * Height;
        return Math.Sqrt(wSq + hSq);
    }
}
```

#### Function-Style Setter

```python
class Temperature:
    _celsius: double = 0.0

    # Function-style setter with validation
    property set celsius(self, value: double):
        if value < -273.15:
            raise ValueError("Temperature below absolute zero")
        self._celsius = value

    # Cannot combine with auto getter
    property get celsius: double # ERROR: no auto backing field with function-style!
```

It is possible to have both a function-style getter and setter. However, function-style getter/setters cannot coexist with an auto-property for the same property name since there is no way to retrieve the backing field.

**Important:** Function-style accessors do **not** generate a backing field. You must provide your own storage.

**Type Consistency:** The type must be the same across all accessors (get/set/init) for a property.

**No Function-Style `init`:** There is no `property init name(self, value: T):` form because init-only semantics require compiler support for constructor-only assignment, which doesn't compose well with user-defined logic.

### Mixed Access Modifiers

Getters and setters can have different visibility:

```python
class Counter:
    _value: int = 0

    # Public getter
    property get value(self) -> int:
        return self._value

    # Private setter (only accessible within the class)
    @private
    property set value(self, v: int):
        self._value = v

    # Public methods can use the private setter
    def increment(self):
        self.value += 1

    def reset(self):
        self.value = 0

# Usage
c = Counter()
print(c.value)    # OK: public getter
c.increment()     # OK: internal modification via public method
c.value = 10      # ERROR: setter is private
```

**With Auto-Properties:**

```python
class User:
    property get name: str           # Public getter
    @private
    property set name: str           # Private setter

    def __init__(self, name: str):
        self.name = name             # OK: inside class
```

**Common Access Patterns:**

| Pattern | Getter | Setter | Use Case |
|---------|--------|--------|----------|
| Read-write | (default) | (default) | Mutable public state |
| Read-only | (default) | (none) | Computed or immutable |
| Observable | (default) | `@private` | External read, internal write |
| Protected write | (default) | `@protected` | Subclass modification |
| Internal write | (default) | `@internal` | Assembly-internal modification |

*Implementation: ✅ Native*
```csharp
public int Value {
    get => _value;
    private set => _value = value;
}
```

### Static Properties

Use `@static` decorator for class-level properties. Static properties take no `self` parameter:

```python
class AppConfig:
    _debug_mode: bool = False
    _instance_count: int = 0

    # Static auto-properties
    @static
    property version: str = "1.0.0"

    @static
    property get build_number: int = 42

    # Static function-style getter (no self parameter)
    @static
    property get is_debug_enabled() -> bool:
        return AppConfig._debug_mode

    # Static function-style setter (no self parameter)
    @static
    property set debug_mode(value: bool):
        AppConfig._debug_mode = value

    @static
    property get debug_mode() -> bool:
        return AppConfig._debug_mode

# Usage
print(AppConfig.version)           # "1.0.0"
AppConfig.debug_mode = True
print(AppConfig.is_debug_enabled)  # True
```

**Static Property Rules:**
- Auto: Use `@static` decorator with `property [get|set|init] name: T`
- Function-style: Use `@static` decorator with `property get name() -> T:` or `property set name(value: T):` (no `self`)
- Access the class by name within the body

*Implementation: ✅ Native*
```csharp
public static string Version { get; set; } = "1.0.0";
public static int BuildNumber { get; } = 42;
public static bool IsDebugEnabled => _debugMode;
public static bool DebugMode {
    get => _debugMode;
    set => _debugMode = value;
}
```

### Virtual, Abstract, and Override Properties

Properties participate in inheritance using the standard decorators:

```python
class Shape:
    # Abstract property (must be overridden)
    @abstract
    property get area(self) -> double:
        ...

    # Virtual property (can be overridden)
    @virtual
    property get name(self) -> str:
        return "Shape"

class Circle(Shape):
    property get radius: double

    def __init__(self, radius: double):
        self.radius = radius

    # Override abstract property
    @override
    property get area(self) -> double:
        return 3.14159 * self.radius ** 2

    # Override virtual property
    @override
    property get name(self) -> str:
        return "Circle"

@final
class UnitCircle(Circle):
    def __init__(self):
        super().__init__(1.0)

    # Sealed override - cannot be overridden in further subclasses
    @final
    @override
    property get name(self) -> str:
        return "Unit Circle"
```

**Inheritance Rules:**
- `@abstract` properties must use `...` as the body and must be overridden
- `@virtual` properties can optionally be overridden by subclasses
- `@override` is required when overriding a base class property
- `@final` prevents further overriding in subclasses
- A subclass can override any accessor it has visibility to
- The overriding accessor's visibility cannot be more restrictive than the base

**Covariant Return Types:**

C# 9.0 supports covariant return types for method overrides. Since properties are essentially methods, property return types can be covariant on override:

```python
class Animal:
    @virtual
    property get friend(self) -> Animal:
        return self._friend

class Dog(Animal):
    @override
    property get friend(self) -> Dog:  # Valid - Dog is subtype of Animal
        return self._dog_friend

class Cat(Animal):
    @override
    property get friend(self) -> Cat:  # Valid - Cat is subtype of Animal
        return self._cat_friend
```

This allows subclasses to return more specific types without requiring unsafe casts at call sites.

*Implementation: ✅ Native*
```csharp
public abstract double Area { get; }
public virtual string Name => "Shape";

public override double Area => 3.14159 * Radius * Radius;
public override string Name => "Circle";

public sealed override string Name => "Unit Circle";
```

### Interface Properties

Interfaces declare property requirements using the same syntax:

```python
interface IIdentifiable:
    # Read-only property requirement (getter only)
    property get id: int

interface INamed:
    # Read-write property requirement (getter + setter)
    property name: str

interface ITimestamped:
    # Function-style requirement (read-only computed)
    property get created_at(self) -> datetime: ...
    property get updated_at(self) -> datetime: ...

class Entity(IIdentifiable, INamed, ITimestamped):
    property get id: int
    property name: str = "Unnamed"
    _created: datetime
    _updated: datetime

    def __init__(self, id: int):
        self.id = id
        self._created = datetime.now()
        self._updated = self._created

    property get created_at(self) -> datetime:
        return self._created

    property get updated_at(self) -> datetime:
        return self._updated
```

**Interface Property Requirements:**

| Interface Declares | Implementer Must Provide |
|--------------------|--------------------------|
| `property get x: T` | At least a getter |
| `property set x: T` | At least a setter |
| `property x: T` | Both getter and setter |
| `property get x(self) -> T: ...` | A getter (auto or function-style) |
| `property set x(self, value: T): ...` | A setter (auto or function-style) |

**Auto-Properties in Interfaces:**

For interface auto-properties, no body means abstract (must be implemented). A default value makes it optional:

```python
interface IIdentifiable:
    property get id: int       # Abstract - implementer must provide getter

interface IConfigurable:
    property name: str = ""    # Default value - implementer can override or use default
    property enabled: bool = True
```

This matches C# interface property semantics where properties without a body are abstract requirements.

**Explicit Interface Implementation:**

When a class needs to provide different behavior when accessed through an interface versus directly:

```python
interface ISecret:
    property get value: str

class SecretHolder(ISecret):
    _secret: str

    def __init__(self, secret: str):
        self._secret = secret

    # Regular property (always accessible)
    property get hint(self) -> str:
        return self._secret[0] + "***"

    # Explicit interface implementation
    # Only accessible when referenced through the interface type
    property get ISecret.value(self) -> str:
        return self._secret

# Usage
holder = SecretHolder("password123")
print(holder.hint)        # "p***"
print(holder.value)       # ERROR: 'value' not accessible on SecretHolder

secret: ISecret = holder
print(secret.value)       # "password123" - accessible via interface
```

*Implementation: ✅ Native*
```csharp
public string Hint => _secret[0] + "***";
string ISecret.Value => _secret;
```

### Property and Method Name Conflicts

A property and a method cannot share the same name within a class:

```python
class Example:
    _value: int = 0

    # ✅ OK - property
    property get value(self) -> int:
        return self._value

    # ❌ ERROR - method cannot have same name as property
    def value(self) -> int:
        return self._value
```

**Compiler Error:**

```
error: 'value' is already defined as a property in this class
  --> example.spy:10:5
   |
10 |     def value(self) -> int:
   |         ^^^^^ method name conflicts with property on line 6
```

### Property Syntax Summary

**Auto-properties (compiler-generated backing field):**

| Syntax | Accessors | C# Equivalent |
|--------|-----------|---------------|
| `property name: T` | get + set | `T Name { get; set; }` |
| `property name: T = val` | get + set | `T Name { get; set; } = val` |
| `property get name: T` | get | `T Name { get; }` |
| `property get name: T = val` | get | `T Name { get; } = val` |
| `property set name: T` | set | `T Name { set; }` |
| `property init name: T` | get + init | `T Name { get; init; }` |
| `property init name: T = val` | get + init | `T Name { get; init; } = val` |

**Function-style properties (user-provided backing field or computed):**

| Syntax | C# Equivalent |
|--------|---------------|
| `property get name(self) -> T:` | `T Name { get { ... } }` |
| `property set name(self, value: T):` | `T Name { set { ... } }` |
| `@static property get name() -> T:` | `static T Name { get { ... } }` |
| `@static property set name(value: T):` | `static T Name { set { ... } }` |
| `property get IFace.name(self) -> T:` | `T IFace.Name { get { ... } }` |

**Valid accessor combinations:**

| Accessors | Result | Readable | Writable in `__init__` | Writable after |
|-----------|--------|----------|------------------------|----------------|
| get | Read-only | ✅ | ✅ (auto) / ❌ (func) | ❌ |
| set | Write-only | ❌ | ✅ | ✅ |
| get + set | Read-write | ✅ | ✅ | ✅ |
| init | Init-only (auto only) | ✅ | ✅ | ❌ |
| get + init | Read + init (auto only) | ✅ | ✅ | ❌ |

**Decorator placement:**

```python
@static
@virtual
property get name(self) -> str:
    return "value"

@override
property get name(self) -> str:
    return self._name

@private
property set name(self, value: str):
    self._name = value
```

---

## Context Managers **[v0.2.0]**

The `with` statement manages resources:

```python
with open("file.txt", "r") as f:
    content = f.read()
# f.close() called automatically

# Multiple resources
with open("in.txt") as input, open("out.txt", "w") as output:
    output.write(input.read())
```

**Protocol:**
- Object passed to `with` should implement either `IContextManager` or `IDisposable`
  - For `IContextManager`:
    - `__enter__()` called on entry (returns object for `as` binding)
    - `__exit__()` called on exit
      - If the object returned in the `as` binding implements `IDisposable`, then its `Dispose()` method is also invoked (before `__exit__()`)
  - For `IDisposable`:
    - `Dispose()` called on exit
- If an object implements both, then `__exit__()` is called before `Dispose()`

*Implementation:*
- For `IContextManager`, ✅ Lowered - `try { var asBinding = contextManager; } catch(Exception e) { ... } finally { contextManager.__Exit__(...); }`
- For `IDisposable`, ✅ Native - `using (var r = resource) { ... }`

---

## Events **[v0.2.0]**

Events provide a publish-subscribe pattern:

```python
class Button:
    # Event declaration
    event clicked: (object, EventArgs) -> None

    def click(self):
        if self.clicked is not None:
            self.clicked(self, EventArgs())

# Subscription
button = Button()

def on_clicked(sender: object, args: EventArgs):
    print("Button clicked!")

button.clicked += on_clicked  # Subscribe
button.click()                 # Triggers event
button.clicked -= on_clicked  # Unsubscribe
```

**Thread-Safe Event Invocation:**

For thread-safe event invocation that avoids race conditions, use the null-conditional call pattern:

```python
class Button:
    event clicked: (object, EventArgs) -> None

    def click(self):
        # Thread-safe pattern using ?.
        self.clicked?.invoke(self, EventArgs())
```

This maps to C#'s `clicked?.Invoke(...)` pattern, which atomically checks for null and invokes, preventing race conditions where a subscriber unsubscribes between the null check and invocation.

```python
# These are equivalent:

# Explicit null check (not thread-safe)
if self.clicked is not None:
    self.clicked(self, EventArgs())  # Race condition possible here

# Null-conditional invoke (thread-safe)
self.clicked?.invoke(self, EventArgs())  # Atomic check-and-invoke
```

### Custom EventArgs

```python
class ValueChangedArgs(EventArgs):
    old_value: int
    new_value: int

    def __init__(self, old_value: int, new_value: int):
        self.old_value = old_value
        self.new_value = new_value

class Counter:
    event value_changed: (object, ValueChangedArgs) -> None
    _value: int = 0

    property get value(self) -> int:
        return self._value

    property set value(self, new_value: int):
        old = self._value
        self._value = new_value
        if self.value_changed is not None:
            self.value_changed(self, ValueChangedArgs(old, new_value))
```

**Event Rules:**
- Events can only be invoked from the declaring class
- `+=` subscribes, `-=` unsubscribes
- Multiple subscribers are called in subscription order

*Implementation: ✅ Native - `event EventHandler Name;`*

---

## Async Programming **[v0.2.0+]**

### Async Functions

```python
async def fetch_data(url: str) -> str:
    await asyncio.sleep(1.0)
    return f"Data from {url}"

async def main():
    result = await fetch_data("https://example.com")
    print(result)
```

*Implementation: ✅ Native - `async` method returning `Task<T>`.*

### Concurrent Execution

```python
async def fetch_all(urls: list[str]) -> list[str]:
    tasks = [fetch_data(url) for url in urls]
    results = await asyncio.gather(*tasks)
    return results
```

*Implementation: ✅ Native - `Task.WhenAll()`*

### Async Iteration

```python
async def count_up(n: int) -> AsyncIterator[int]:
    for i in range(n):
        await asyncio.sleep(0.1)
        yield i

async def process():
    async for num in count_up(5):
        print(f"Number: {num}")
```

**No Async Comprehensions:**

Sharpy does not support async comprehensions (`async for` inside comprehensions). C# 9.0's LINQ doesn't natively support `IAsyncEnumerable` in query syntax, making this feature complex to implement.

```python
# ❌ Not supported - async comprehension
results = [x async for x in async_iterator()]
results = [x async for x in async_iterator() if await predicate(x)]

# ✅ Use explicit async loop instead
results: list[T] = []
async for x in async_iterator():
    results.append(x)

# ✅ Or with condition
results: list[T] = []
async for x in async_iterator():
    if await predicate(x):
        results.append(x)
```

Async comprehensions may be added in a future version (v2.0+) when better runtime support is available.

**Generator Return Types:**

Functions using `yield` have special return type annotations:

| Pattern | Return Type | Notes |
|---------|-------------|-------|
| `yield` in function | `Iterator[T]` | Synchronous generator |
| `yield` in `async def` | `AsyncIterator[T]` | Asynchronous generator |
| `yield from` | Same as yielded iterator | Delegation |

```python
# Synchronous generator
def fibonacci(n: int) -> Iterator[int]:
    a, b = 0, 1
    for _ in range(n):
        yield a
        a, b = b, a + b

# Async generator
async def stream_data(url: str) -> AsyncIterator[bytes]:
    async with http_client.stream(url) as response:
        async for chunk in response:
            yield chunk
```

*Implementation: ✅ Native - `IAsyncEnumerable<T>` (C# 8+)*

### Async Context Managers

```python
async def use_resource():
    async with AsyncResource() as resource:
        await resource.process()
```

*Implementation: 🔄 Lowered - `await using (var r = resource) { ... }`*

---

## Built-in Functions **[v0.1.0+]**

Built-in functions provide polymorphic access to type behavior. They work uniformly on all types—primitives, .NET types, and Sharpy-defined types—by internally dispatching to the appropriate implementation:

- **For Sharpy types**: If the type defines the corresponding dunder method, the built-in function calls it
- **For primitives and .NET types**: The built-in function uses the native .NET operation
- **Fallback behavior**: Some functions provide sensible defaults when no custom implementation exists

This design allows code like `len(x)`, `str(x)`, and `repr(x)` to work consistently regardless of whether `x` is a list, a string, or a custom class.

### Type Conversion [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `int(x)` | Convert to integer | `(int)x` or `Convert.ToInt32(x)` |
| `double(x)` | Convert to double | `(double)x` |
| `str(x)` | Convert to string | Calls `__str__` if defined, else `.ToString()` |
| `bool(x)` | Convert to boolean | Truthiness check |

**`str(x)`** returns a human-readable string representation:
- For Sharpy types with `__str__`: calls `__str__`
- For all types: falls back to `.ToString()`

### Type Checking [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `isinstance(x, T)` | Check if `x` is an instance of type `T` | `x is T` |
| `type(x)` | Get runtime type of `x` | `x.GetType()` |

**`type(x)` Return Type:**

The `type()` function returns `System.Type`, the .NET reflection type:

```python
from system import Type

x = 42
t: Type = type(x)        # Returns System.Int32 type
print(t.name)            # "Int32"
print(t.full_name)       # "System.Int32"

# Type comparison
if type(x) == type(0):
    print("x is an integer")

# Prefer isinstance() for type checks
if isinstance(x, int):   # More idiomatic
    print("x is an integer")
```

**Note:** Unlike Python where `type(None)` returns `NoneType`, Sharpy's `type(None)` is a compile-time error because `None` is not a value with a type.

**`type()` on Primitive Literals:**

Unlike `type(None)`, calling `type()` on primitive literals is valid and returns the corresponding `System.Type`:

```python
# All of these are valid
t1 = type(42)        # System.Int32
t2 = type(3.14)      # System.Double
t3 = type("hello")   # System.String
t4 = type(True)      # System.Boolean
t5 = type([1, 2, 3]) # Sharpy.Core.List`1[System.Int32]

# Only type(None) is an error
t6 = type(None)      # ERROR: type(None) is not valid
```

This is because primitive literals are values with concrete runtime types, whereas `None` represents the absence of a value.

**`isinstance(x, T)`**

Checks whether `x` is an instance of type `T` at runtime. Returns `True` if `x` is an instance of `T` or any subclass of `T`.

```python
value: object = get_value()

if isinstance(value, str):
    # value is narrowed to str in this block
    print(value.upper())

if isinstance(value, MyClass):
    # value is narrowed to MyClass
    value.my_method()

# Works with interfaces too
if isinstance(value, IDrawable):
    # value is narrowed to IDrawable
    value.draw()
```

**Single Type Only:**

Unlike Python's `isinstance()` which accepts a tuple of types, Sharpy's `isinstance()` only accepts a single type argument. Sharpy does not have union types.

```python
# ✅ Valid - single type
if isinstance(x, int):
    pass

if isinstance(x, IDrawable):
    pass

# ❌ Invalid - multiple types not supported
if isinstance(x, (int, str)):      # ERROR: isinstance() takes exactly one type argument
    pass

if isinstance(x, int | str):       # ERROR: union types not supported
    pass
```

**To check multiple types**, use explicit `or`:

```python
if isinstance(x, int) or isinstance(x, str):
    # x could be int or str here
    # Note: no automatic type narrowing in this case
    pass
```

**Generic Type Limitation:**

Due to .NET type erasure for generics at runtime, `isinstance()` cannot check generic type arguments:

```python
# ✅ Valid - checks if x is any List<T>
if isinstance(x, list):
    pass  # x could be list[int], list[str], etc.

# ❌ Compile error - cannot check generic type arguments at runtime
if isinstance(x, list[int]):       # ERROR: Cannot check generic type arguments at runtime
    pass

if isinstance(x, dict[str, int]):  # ERROR: Cannot check generic type arguments at runtime
    pass
```

This limitation matches C#'s `is` operator behavior. At runtime, `List<int>` and `List<str>` are both just `List<T>`—the generic type argument is erased.

**Type Narrowing:**

When `isinstance()` is used in a conditional, the variable's type is narrowed within that branch:

```python
def process(value: object) -> str:
    if isinstance(value, str):
        return value.upper()      # OK: value is str
    if isinstance(value, int):
        return str(value * 2)     # OK: value is int
    return "unknown"
```

*Implementation: ✅ Native - Maps to C# `is` pattern matching with type narrowing.*

### Collection Functions [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `len(x)` | Get length | Calls `__len__` if defined, else `.Count` or `.Length` |
| `min(iter)` | Minimum value | `.Min()` or `Math.Min()` |
| `max(iter)` | Maximum value | `.Max()` or `Math.Max()` |
| `sum(iter)` | Sum values | `.Sum()` |
| `sorted(iter)` | Sort collection | `.OrderBy()` |
| `reversed(iter)` | Reverse | `.Reverse()` |
| `enumerate(iter)` | Index + value | `.Select((x, i) => (i, x))` |

**`enumerate()` Signature:**

The `enumerate()` function matches Python's signature:

```python
enumerate(iterable, start=0)
```

| Form | Description |
|------|-------------|
| `enumerate(items)` | Indices start at 0 |
| `enumerate(items, start=1)` | Indices start at 1 |
| `enumerate(items, start=n)` | Indices start at n |

```python
names = ["Alice", "Bob", "Charlie"]

# Default: start at 0
for i, name in enumerate(names):
    print(f"{i}: {name}")  # 0: Alice, 1: Bob, 2: Charlie

# Start at 1 (useful for 1-based numbering)
for i, name in enumerate(names, start=1):
    print(f"{i}. {name}")  # 1. Alice, 2. Bob, 3. Charlie
```

*Implementation: 🔄 Lowered - `.Select((x, i) => (i + start, x))`.*

| `zip(a, b)` | Combine iterables | `.Zip()` |
| `range(n)` | Number sequence | `Enumerable.Range()` |

**`range()` Signature:**

The `range()` function matches Python's signature exactly:

| Form | Description | Example |
|------|-------------|---------|
| `range(stop)` | 0 to stop-1 | `range(5)` → 0, 1, 2, 3, 4 |
| `range(start, stop)` | start to stop-1 | `range(2, 5)` → 2, 3, 4 |
| `range(start, stop, step)` | start to stop-1, by step | `range(0, 10, 2)` → 0, 2, 4, 6, 8 |

```python
# Single argument: 0 to n-1
for i in range(5):
    print(i)  # 0, 1, 2, 3, 4

# Two arguments: start to stop-1
for i in range(2, 7):
    print(i)  # 2, 3, 4, 5, 6

# Three arguments: start to stop-1, stepping by step
for i in range(0, 10, 2):
    print(i)  # 0, 2, 4, 6, 8

# Negative step for countdown
for i in range(10, 0, -1):
    print(i)  # 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
```

*Implementation: 🔄 Lowered - Simple forms use `for (int i = start; i < stop; i += step)`, complex forms use `Enumerable.Range()` or generator.*

| `filter(pred, iter)` | Filter | `.Where()` |
| `map(func, iter)` | Transform | `.Select()` |
| `all(iter)` | All truthy | `.All()` |
| `any(iter)` | Any truthy | `.Any()` |

**`len(x)`** returns the number of items in a container:
- For Sharpy types with `__len__`: calls `__len__`
- For collections: uses `.Count` property
- For strings/arrays: uses `.Length` property

### I/O Functions [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `print(x)` | Print to console | `Console.WriteLine()` |
| `input(prompt)` | Read from console | `Console.ReadLine()` |

### Mathematical Functions [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `abs(x)` | Absolute value | `Math.Abs()` |
| `pow(x, y)` | Power | `Math.Pow()` |
| `round(x, n)` | Round | `Math.Round()` |
| `divmod(a, b)` | Quotient + remainder | `(a / b, a % b)` |

**`divmod()` Return Types:**

The `divmod()` function returns a tuple containing the quotient and remainder. The return type depends on the operand types, following the same numeric promotion rules as `/` and `//`:

| Operand Types | Return Type | Notes |
|---------------|-------------|-------|
| Both `int` | `tuple[int, int]` | Integer division and modulo |
| Any `long` | `tuple[long, long]` | Promoted to long |
| Any `float`/`double` | `tuple[double, double]` | Float division |
| Any `decimal` | `tuple[decimal, decimal]` | Decimal division |

```python
divmod(17, 5)       # (3, 2) - tuple[int, int]
divmod(17L, 5)      # (3L, 2L) - tuple[long, long]
divmod(17.0, 5.0)   # (3.0, 2.0) - tuple[double, double]
divmod(17.0m, 5.0m) # (3.0m, 2.0m) - tuple[decimal, decimal]
```

### Object Functions [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `repr(x)` | Debug representation | Calls `__repr__` if defined, else `__str__`, else `.ToString()` |
| `hash(x)` | Hash code | Calls `__hash__` if defined, else `.GetHashCode()` |
| `id(x)` | Object identity | `RuntimeHelpers.GetHashCode()` |

**`repr(x)`** returns a string representation suitable for debugging:
- For Sharpy types with `__repr__`: calls `__repr__`
- Fallback: tries `__str__`, then `.ToString()`
- Typically includes type name and distinguishing attributes

**`hash(x)`** returns the hash code for use in dictionaries and sets:
- For Sharpy types with `__hash__`: calls `__hash__`
- For all types: falls back to `.GetHashCode()`
- If `__eq__` is defined, `__hash__` must also be defined (and vice versa)

**Hashing Tuples:**

Tuples are automatically hashable if all their elements are hashable:

```python
# Tuples of hashable types can be hashed
point = (10, 20)
h = hash(point)          # OK: both int elements are hashable

# Use tuples to create composite hash keys
coord_to_name: dict[tuple[int, int], str] = {}
coord_to_name[(0, 0)] = "origin"
coord_to_name[(10, 20)] = "point A"

# Nested tuples work if all elements hashable
nested = ((1, 2), (3, 4))
h = hash(nested)         # OK

# Tuples containing unhashable types cannot be hashed
bad = ([1, 2], [3, 4])   # Tuple containing lists
h = hash(bad)            # ERROR: list is not hashable
```

*Implementation: 🔄 Lowered - Generated as method calls or type-appropriate dispatch.*

---

## .NET Interop **[v0.1.0]**

### Importing .NET Types

```python
from system.collections.generic import List, Dictionary
from system.io import File, Path

# Use .NET types directly
items = List[int]()
items.add(42)

content = File.read_all_text("data.txt")
```

### .NET Properties

.NET properties accessed like Sharpy properties:

```python
from system.io import FileInfo

file = FileInfo("data.txt")
size = file.length
name = file.name
```

### Extension Methods

.NET extension methods work naturally:

```python
from system.linq import Enumerable

numbers = [1, 2, 3, 4, 5]
evens = numbers.where(lambda x: x % 2 == 0)
doubled = numbers.select(lambda x: x * 2)
```

### IDisposable Pattern

.NET's `IDisposable` integrates with `with`:

```python
from system.io import FileStream, FileMode

with FileStream("output.dat", FileMode.create) as stream:
    stream.write(data, 0, len(data))
```

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
