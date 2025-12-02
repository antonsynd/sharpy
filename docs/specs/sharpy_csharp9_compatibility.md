# Sharpy C# 9.0 Compatibility Matrix

This document maps all Sharpy language features to their C# 9.0 transpilation requirements. Features are categorized by their compatibility status and version availability.

## Compatibility Legend

| Status | Meaning |
|--------|---------|
| ✅ **Native** | Feature maps directly to C# 9.0 with no special handling |
| 🔄 **Lowered** | Feature requires compiler transformation but works in C# 9.0 |
| ⚠️ **Polyfill** | Feature requires polyfill types (e.g., PolySharp) to compile |
| ❌ **v2.0** | Feature requires .NET 7+ runtime; deferred to Sharpy v2.0 |

---

## Version Summary

| Sharpy Version | Target Runtime | C# Version | Notes |
|----------------|----------------|------------|-------|
| **v0.5 - v1.0** | .NET 5+ / Unity | C# 9.0 | Maximum compatibility |
| **v2.0+** | .NET 7+ | C# 11+ | Full modern features |

---

## Core Language Features

### Lexical Structure [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| 4-space indentation | ✅ Native | Converted to braces |
| `#` comments | ✅ Native | Converted to `//` |
| Triple-quote strings | ✅ Native | Verbatim strings `@""` or escaped |
| UTF-8 encoding | ✅ Native | Direct support |
| Line continuation `\` | ✅ Native | Removed during parsing |

### Identifiers and Naming [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `snake_case` to `PascalCase` conversion | ✅ Native | Compile-time transformation |
| Backtick literal names `` `name` `` | ✅ Native | Maps to `@name` or exact casing |
| Unicode identifiers | ✅ Native | Direct support |

### Literals [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Integer literals | ✅ Native | Direct mapping |
| Float literals | ✅ Native | Direct mapping |
| String literals (single/double quote) | ✅ Native | All become double-quoted |
| F-strings `f"...{expr}..."` | ✅ Native | `$"...{expr}..."` |
| Raw strings `r"..."` | ✅ Native | `@"..."` verbatim strings |
| Boolean `True`/`False` | ✅ Native | `true`/`false` |
| `None` | ✅ Native | `null` |
| Ellipsis `...` | ✅ Native | `throw new NotImplementedException()` or empty body |
| Empty set `{/}` | ✅ Native | `new HashSet<T>()` |

### Literals [v1.0] - Extended Formats

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Binary literals `0b1010` | ✅ Native | Direct support (C# 7.0+) |
| Hex literals `0xFF` | ✅ Native | Direct support |
| Octal literals `0o777` | 🔄 Lowered | Convert to decimal at compile time |
| Scientific notation `1e10` | ✅ Native | Direct support |
| Underscore separators `1_000_000` | ✅ Native | Direct support (C# 7.0+) |

---

## Type System

### Built-in Types [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `int`, `long`, `short`, `byte` | ✅ Native | Direct mapping to System types |
| `uint`, `ulong`, `ushort`, `sbyte` | ✅ Native | Direct mapping |
| `float`, `double`, `decimal` | ✅ Native | Direct mapping |
| `bool`, `str`, `char` | ✅ Native | `bool`, `string`, `char` |
| `object` | ✅ Native | `object` |

### Collection Types [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `list[T]` | ✅ Native | `List<T>` |
| `dict[K, V]` | ✅ Native | `Dictionary<K, V>` |
| `set[T]` | ✅ Native | `HashSet<T>` |
| `tuple[T1, T2, ...]` | ✅ Native | `ValueTuple<T1, T2, ...>` or `Tuple<...>` |

### Collection Literals [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| List literal `[1, 2, 3]` | 🔄 Lowered | `new List<int> { 1, 2, 3 }` |
| Dict literal `{"a": 1}` | 🔄 Lowered | `new Dictionary<string, int> { ["a"] = 1 }` |
| Set literal `{1, 2, 3}` | 🔄 Lowered | `new HashSet<int> { 1, 2, 3 }` |
| Empty set `{/}` | 🔄 Lowered | `new HashSet<T>()` |
| Tuple literal `(1, 2)` | ✅ Native | Direct support |

### Nullable Types [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Nullable annotation `T?` | ✅ Native | `T?` with `#nullable enable` |
| `None` assignment | ✅ Native | `null` |
| `is None` / `is not None` | ✅ Native | `== null` / `!= null` or pattern |
| Null-conditional `?.` | ✅ Native | Direct support (C# 6.0+) |
| Null-coalescing `??` | ✅ Native | Direct support |
| Type narrowing after null check | ✅ Native | Flow analysis supported |

### Type Aliases [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `type UserId = int` | 🔄 Lowered | Inline expansion (no `using` alias for non-namespaces) |
| Generic aliases `type Callback[T] = (T) -> None` | 🔄 Lowered | Inline expansion at use sites |
| Module-level aliases | 🔄 Lowered | `using` directive where possible, else inline |
| Class-level aliases | 🔄 Lowered | Inline expansion |
| Function-level aliases | 🔄 Lowered | Inline expansion |

---

## Functions and Methods

### Function Definition [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `def` keyword | ✅ Native | Method declaration |
| Type annotations on parameters | ✅ Native | Direct mapping |
| Return type annotation `-> T` | ✅ Native | Return type |
| `-> None` return | ✅ Native | `void` |
| Default parameter values | ✅ Native | Direct support |
| Function overloading | ✅ Native | Direct support |

### Lambda Expressions [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `lambda x: expr` | ✅ Native | `x => expr` |
| Multi-parameter lambda | ✅ Native | `(x, y) => expr` |
| Lambda as argument | ✅ Native | Direct support |
| Closure capture | ✅ Native | Direct support |

### Function Types [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `(T) -> U` function type | 🔄 Lowered | `Func<T, U>` or `Action<T>` |
| `() -> None` | 🔄 Lowered | `Action` |
| `(T1, T2) -> U` | 🔄 Lowered | `Func<T1, T2, U>` |

---

## Classes and Objects

### Class Definition [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `class` keyword | ✅ Native | `class` |
| Field declarations with types | ✅ Native | Field declarations |
| `__init__` constructor | ✅ Native | Constructor |
| `self` parameter | ✅ Native | Implicit `this` |
| Instance methods | ✅ Native | Methods |
| Constructor overloading | ✅ Native | Direct support |

### Inheritance [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Single class inheritance | ✅ Native | `: BaseClass` |
| Multiple interface implementation | ✅ Native | `: IInterface1, IInterface2` |
| `super().__init__()` | ✅ Native | `: base()` or `base.Method()` |
| `@override` decorator | ✅ Native | `override` keyword |
| `@virtual` decorator | ✅ Native | `virtual` keyword |
| `@abstract` decorator | ✅ Native | `abstract` keyword |
| `@final` decorator (method) | ✅ Native | `sealed override` |
| `@final` decorator (class) | ✅ Native | `sealed class` |

### Static Members [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `@static` decorator | ✅ Native | `static` keyword |
| Static methods | ✅ Native | Direct support |
| Static fields | ✅ Native | Direct support |

### Access Modifiers [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Default (public) | ✅ Native | `public` |
| `@protected` / `_name` | ✅ Native | `protected` |
| `@private` / `__name` | ✅ Native | `private` |
| `@internal` | ✅ Native | `internal` |
| `@file` | ❌ **v2.0** | Requires C# 11 `file` keyword |

**Note:** The `@file` access modifier requires C# 11's file-scoped types. For v0.5-v1.0, transpile to `internal` with a unique generated name suffix to prevent collisions.

---

## Structs [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `struct` keyword | ✅ Native | `struct` |
| Field declarations | ✅ Native | Direct support |
| Constructor requirement | ✅ Native | Direct support |
| Interface implementation | ✅ Native | Direct support |
| Value semantics | ✅ Native | Inherent to structs |
| Operator overloading | ✅ Native | Direct support |

### Record Structs [v2.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `record struct` | ❌ **v2.0** | Requires C# 10 |

**Workaround for v0.5-v1.0:** Generate regular `struct` with manually implemented:
- `IEquatable<T>`
- `Equals(object)`
- `GetHashCode()`
- `==` and `!=` operators
- `ToString()`

---

## Interfaces [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `interface` keyword | ✅ Native | `interface` |
| Method signatures with `...` | ✅ Native | Method declarations |
| Generic interfaces | ✅ Native | Direct support |
| Interface inheritance | ✅ Native | Direct support |
| `isinstance()` checks | ✅ Native | `is` pattern |

### Static Abstract Members [v2.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Static abstract methods in interfaces | ❌ **v2.0** | Requires C# 11 + .NET 7 runtime |
| Static abstract properties | ❌ **v2.0** | Requires C# 11 + .NET 7 runtime |
| Static abstract operators | ❌ **v2.0** | Requires C# 11 + .NET 7 runtime |
| Generic math (`INumber<T>`, etc.) | ❌ **v2.0** | Requires .NET 7 BCL |

**Impact:** Sharpy cannot support generic numeric constraints or type-level polymorphism until v2.0. Numeric algorithms must use concrete types or runtime type checks.

---

## Properties [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Auto properties `property name: T` | 🔄 Lowered | Generate backing field + accessors |
| Read-only auto `get property name: T` | 🔄 Lowered | `{ get; }` with backing field |
| Explicit getter | ✅ Native | `get { ... }` |
| Explicit setter | ✅ Native | `set { ... }` |
| Init-only setter | ⚠️ Polyfill | Requires `IsExternalInit` type definition |
| Computed properties | ✅ Native | Expression-bodied members |

### Field Keyword [v2.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `field` contextual keyword in accessors | ❌ **v2.0** | Requires C# 13 |

**Workaround for v0.5-v1.0:** Always generate explicit backing fields:
```csharp
// Sharpy
property name: str:
    get: return self._name
    set: self._name = value ?? throw ValueError()

// Generated C# 9.0
private string _name;
public string Name {
    get => _name;
    set => _name = value ?? throw new ArgumentNullException();
}
```

---

## Enumerations [v0.5]

### Simple Enums [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `enum` with integer values | ✅ Native | `enum` |
| `enum` with string values | 🔄 Lowered | Static class with string constants |
| Enum methods | 🔄 Lowered | Extension methods |
| `.value` property | ✅ Native | Cast to underlying type |
| `.name` property | 🔄 Lowered | `Enum.GetName()` or lookup |

### Tagged Unions (ADTs) [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `enum Result[T, E]` with cases | 🔄 Lowered | Abstract base + sealed case classes |
| `case Ok(value: T)` | 🔄 Lowered | Nested sealed class with properties |
| `case Err(error: E)` | 🔄 Lowered | Nested sealed class with properties |
| Pattern matching on cases | 🔄 Lowered | `switch` with type patterns |
| `Deconstruct` for C# interop | 🔄 Lowered | Generate `Deconstruct` methods |
| Methods on tagged unions | 🔄 Lowered | Methods on abstract base |

**Generated structure for `Result[T, E]`:**
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

    public static Result<T, E> ok(T value) => new Ok(value);
    public static Result<T, E> err(E error) => new Err(error);
}
```

---

## Pattern Matching [v1.0]

### Supported Patterns (C# 9.0 Compatible)

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `match` statement | ✅ Native | `switch` expression/statement |
| Literal patterns `case 0:` | ✅ Native | Direct support |
| Type patterns `case int():` | ✅ Native | `case int` |
| Type patterns with binding `case int() as i:` | ✅ Native | `case int i` |
| Wildcard `case _:` | ✅ Native | `default` or `_` |
| OR patterns `case "a" \| "b":` | ✅ Native | `case "a" or "b"` (C# 9) |
| Guard clauses `case int() as i if i > 0:` | ✅ Native | `case int i when i > 0` |
| Tuple patterns `case (0, 0):` | ✅ Native | Direct support (C# 8+) |
| Property patterns `case Point(x=0):` | ✅ Native | `case Point { X: 0 }` |
| Nested patterns | ✅ Native | Direct support |
| Relational patterns `case > 0:` | ✅ Native | Direct support (C# 9) |
| Logical patterns `and`, `or`, `not` | ✅ Native | Direct support (C# 9) |

### List Patterns [v2.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `case [1, 2, 3]:` | ❌ **v2.0** | Requires C# 11 |
| `case [first, .., last]:` | ❌ **v2.0** | Requires C# 11 |
| `case [_, _, _]:` | ❌ **v2.0** | Requires C# 11 |
| Slice patterns `..` | ❌ **v2.0** | Requires C# 11 |

**Workaround for v0.5-v1.0:** List patterns must be manually lowered to index checks:
```csharp
// Cannot generate: case [var first, .., var last]
// Must generate:
if (list.Count >= 2) {
    var first = list[0];
    var last = list[list.Count - 1];
    // ...
}
```

**Recommendation:** Prohibit list patterns in Sharpy v0.5-v1.0 or implement complex lowering.

### Extended Property Patterns [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `case Person(address.city="NYC"):` | 🔄 Lowered | `case Person { Address: { City: "NYC" } }` |

Extended property patterns (C# 10) can be lowered to nested property patterns (C# 8).

---

## Control Flow [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `if`/`elif`/`else` | ✅ Native | `if`/`else if`/`else` |
| `while` loop | ✅ Native | `while` |
| `for x in collection:` | ✅ Native | `foreach (var x in collection)` |
| `for i in range(n):` | 🔄 Lowered | `for (int i = 0; i < n; i++)` |
| `break` | ✅ Native | `break` |
| `continue` | ✅ Native | `continue` |
| Loop `else` clause | 🔄 Lowered | Boolean flag pattern |
| Ternary `x if cond else y` | ✅ Native | `cond ? x : y` |

---

## Exception Handling [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `try`/`except`/`finally` | ✅ Native | `try`/`catch`/`finally` |
| `except Exception as e:` | ✅ Native | `catch (Exception e)` |
| Multiple `except` clauses | ✅ Native | Multiple `catch` blocks |
| `else` clause | 🔄 Lowered | Boolean flag pattern |
| `raise Exception()` | ✅ Native | `throw new Exception()` |
| Bare `raise` | ✅ Native | `throw` |
| `raise ... from ...` | 🔄 Lowered | Inner exception constructor |

---

## Context Managers [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `with resource as r:` | ✅ Native | `using (var r = resource)` |
| Multiple resources | ✅ Native | Multiple `using` or nested |
| `__enter__`/`__exit__` protocol | 🔄 Lowered | `IDisposable` implementation |
| Async context managers | ❌ **v2.0** | See Async section |

---

## Async Programming [v1.0+]

### Basic Async [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `async def` | ✅ Native | `async` method |
| `await` expression | ✅ Native | `await` |
| `async for` | 🔄 Lowered | `await foreach` (C# 8+) |
| `async with` | 🔄 Lowered | `await using` (C# 8+) |
| Task/ValueTask return | ✅ Native | Direct support |

### Async Iterators

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Async generators (`async yield`) | ✅ Native | `IAsyncEnumerable<T>` (C# 8+) |

---

## Generics [v0.5]

### Basic Generics

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Generic classes `class Box[T]:` | ✅ Native | `class Box<T>` |
| Generic structs | ✅ Native | Direct support |
| Generic interfaces | ✅ Native | Direct support |
| Generic methods `def func[T]():` | ✅ Native | `void Func<T>()` |
| Multiple type parameters | ✅ Native | Direct support |

### Type Constraints [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Interface constraint `T: IComparable` | ✅ Native | `where T : IComparable` |
| Class constraint | ✅ Native | `where T : class` |
| Struct constraint | ✅ Native | `where T : struct` |
| `new()` constraint | ✅ Native | `where T : new()` |
| Base class constraint | ✅ Native | `where T : BaseClass` |
| Multiple constraints | ✅ Native | Direct support |

### Advanced Constraints [v2.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `allows ref struct` | ❌ **v2.0** | Requires C# 13 |
| Static abstract constraint | ❌ **v2.0** | Requires C# 11 + .NET 7 |

---

## Operator Overloading [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `__add__` → `+` | ✅ Native | `operator +` |
| `__sub__` → `-` | ✅ Native | `operator -` |
| `__mul__` → `*` | ✅ Native | `operator *` |
| `__truediv__` → `/` | ✅ Native | `operator /` |
| `__eq__` → `==` | ✅ Native | `operator ==` |
| `__lt__`, `__gt__`, etc. | ✅ Native | Comparison operators |
| `__getitem__` → indexer | ✅ Native | `this[...]` |
| `__setitem__` → indexer | ✅ Native | `this[...]` |
| `__contains__` → `in` | 🔄 Lowered | `Contains()` method call |
| `__len__` | 🔄 Lowered | `Count` property |
| `__str__` | ✅ Native | `ToString()` |
| `__repr__` | 🔄 Lowered | Custom method |
| `__hash__` | ✅ Native | `GetHashCode()` |
| `__iter__` | ✅ Native | `GetEnumerator()` |

### User-Defined Compound Assignment [v2.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| Custom `+=` without `+` | ❌ **v2.0** | Requires C# 14 |

---

## Modules and Imports [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `import module` | ✅ Native | `using Namespace;` |
| `import module as alias` | ✅ Native | `using Alias = Namespace;` |
| `from module import name` | ✅ Native | `using static` or direct reference |
| `from module import *` | 🔄 Lowered | Multiple `using` statements |
| Module → Namespace mapping | ✅ Native | `snake_case` → `PascalCase` |

### File-Scoped Namespaces

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| File-scoped namespace declaration | 🔄 Lowered | Block-scoped `namespace { }` |

---

## Constants [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `const NAME = value` | ✅ Native | `const` or `static readonly` |
| Compile-time expressions | ✅ Native | Direct support |
| Module-level constants | ✅ Native | Static class members |
| Class-level constants | ✅ Native | Direct support |

---

## Decorators [v0.5]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `@static` | ✅ Native | `static` keyword |
| `@override` | ✅ Native | `override` keyword |
| `@virtual` | ✅ Native | `virtual` keyword |
| `@abstract` | ✅ Native | `abstract` keyword |
| `@final` | ✅ Native | `sealed` keyword |
| `@protected` | ✅ Native | `protected` keyword |
| `@private` | ✅ Native | `private` keyword |
| `@internal` | ✅ Native | `internal` keyword |
| `@file` | ❌ **v2.0** | Requires C# 11 |
| Custom attributes | ✅ Native | `[Attribute]` |

---

## Defer Statement [v1.0+]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `defer:` block | 🔄 Lowered | `try`/`finally` pattern |
| Multiple defers (LIFO) | 🔄 Lowered | Nested `try`/`finally` |
| Defer with exceptions | 🔄 Lowered | `finally` ensures execution |

**Lowering example:**
```python
# Sharpy
def process():
    file = open("data.txt")
    defer:
        file.close()
    return file.read()
```
```csharp
// C# 9.0
string Process() {
    var file = File.OpenRead("data.txt");
    try {
        return file.ReadToEnd();
    } finally {
        file.Close();
    }
}
```

---

## Events [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `event name: (sender, args) -> None` | ✅ Native | `event EventHandler Name` |
| `+=` subscribe | ✅ Native | Direct support |
| `-=` unsubscribe | ✅ Native | Direct support |
| Custom EventArgs | ✅ Native | Direct support |

---

## Comprehensions [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| List comprehension `[x for x in ...]` | 🔄 Lowered | LINQ `.Select()` or loop |
| Dict comprehension `{k: v for ...}` | 🔄 Lowered | `.ToDictionary()` or loop |
| Set comprehension `{x for x in ...}` | 🔄 Lowered | `.ToHashSet()` or loop |
| Generator expression `(x for x in ...)` | 🔄 Lowered | LINQ or iterator method |
| Conditional comprehension | 🔄 Lowered | `.Where().Select()` |
| Nested comprehension | 🔄 Lowered | `.SelectMany()` |

---

## Walrus Operator [v1.0]

| Feature | Status | C# 9.0 Transpilation |
|---------|--------|---------------------|
| `:=` assignment expression | 🔄 Lowered | Separate declaration + assignment |

**Lowering example:**
```python
# Sharpy
if (match := pattern.search(text)) is not None:
    print(match.group())
```
```csharp
// C# 9.0
var match = pattern.Match(text);
if (match.Success) {
    Console.WriteLine(match.Value);
}
```

---

## Built-in Functions [v0.5]

| Function | Status | C# 9.0 Transpilation |
|----------|--------|---------------------|
| `len()` | 🔄 Lowered | `.Count` or `.Length` |
| `print()` | 🔄 Lowered | `Console.WriteLine()` |
| `input()` | 🔄 Lowered | `Console.ReadLine()` |
| `range()` | 🔄 Lowered | `Enumerable.Range()` or loop |
| `enumerate()` | 🔄 Lowered | `.Select((x, i) => ...)` |
| `zip()` | 🔄 Lowered | `.Zip()` |
| `map()` | 🔄 Lowered | `.Select()` |
| `filter()` | 🔄 Lowered | `.Where()` |
| `sorted()` | 🔄 Lowered | `.OrderBy()` |
| `reversed()` | 🔄 Lowered | `.Reverse()` |
| `min()`, `max()` | 🔄 Lowered | `.Min()`, `.Max()` or `Math.Min/Max` |
| `sum()` | 🔄 Lowered | `.Sum()` |
| `all()`, `any()` | 🔄 Lowered | `.All()`, `.Any()` |
| `abs()` | 🔄 Lowered | `Math.Abs()` |
| `round()` | 🔄 Lowered | `Math.Round()` |
| `isinstance()` | 🔄 Lowered | `is` pattern or `.GetType()` |
| `type()` | 🔄 Lowered | `.GetType()` |
| `hash()` | 🔄 Lowered | `.GetHashCode()` |
| `str()` | 🔄 Lowered | `.ToString()` |
| `int()`, `float()`, etc. | 🔄 Lowered | Cast or `Convert.ToXxx()` |

---

## Features Deferred to Sharpy v2.0

The following features require .NET 7+ runtime or C# 11+ and cannot be supported when targeting Unity or .NET 5/6:

### Language Features

| Feature | Required C# | Required .NET | Reason |
|---------|-------------|---------------|--------|
| `@file` access modifier | C# 11 | .NET 6+ | File-scoped types |
| List patterns | C# 11 | Any | Compiler feature |
| Static abstract interface members | C# 11 | .NET 7 | Runtime support |
| Generic math constraints | C# 11 | .NET 7 | BCL interfaces |
| `required` members | C# 11 | .NET 7 | Attribute + compiler |
| `field` keyword in properties | C# 13 | Any | Compiler feature |
| `allows ref struct` constraint | C# 13 | .NET 9 | Runtime support |
| Ref fields in structs | C# 11 | .NET 7 | Runtime support |
| Extension properties/operators | C# 14 | Any | Compiler feature |
| User-defined `+=` operators | C# 14 | Any | Compiler feature |
| Inline arrays | C# 12 | .NET 8 | Runtime support |
| Params collections (non-array) | C# 13 | .NET 9 | BCL support |

### Polyfill-Capable Features

These features can work with polyfill types but have limitations:

| Feature | Polyfill Available | Limitation |
|---------|-------------------|------------|
| `init` setters | Yes (IsExternalInit) | Must define type manually |
| `required` modifier | Yes (RequiredMemberAttribute) | No compile-time enforcement in older compilers |
| Record classes | Partial | Init setters need polyfill |

---

## Recommended Minimum Versions

| Target Platform | Recommended Sharpy Version | C# Target |
|-----------------|---------------------------|-----------|
| Unity (all versions) | v0.5 - v1.0 | C# 9.0 |
| .NET 5 | v0.5 - v1.0 | C# 9.0 |
| .NET 6 | v0.5 - v1.0 | C# 10.0 |
| .NET 7+ | v2.0+ | C# 11+ |
| .NET 8+ | v2.0+ | C# 12+ |
| .NET 9+ | v2.0+ | C# 13+ |

---

## Summary Statistics

| Category | Native | Lowered | Polyfill | v2.0 Only |
|----------|--------|---------|----------|-----------|
| Core syntax | 25 | 8 | 0 | 0 |
| Types | 18 | 6 | 0 | 2 |
| Classes/OOP | 22 | 4 | 1 | 1 |
| Pattern matching | 12 | 1 | 0 | 4 |
| Generics | 10 | 0 | 0 | 2 |
| Operators | 14 | 4 | 0 | 1 |
| Control flow | 8 | 3 | 0 | 0 |
| **Total** | **109** | **26** | **1** | **10** |

**Conclusion:** Approximately 93% of Sharpy features can be supported when targeting C# 9.0, with most requiring only straightforward lowering transformations. The 10 features deferred to v2.0 are advanced type system features that most application code does not require.
