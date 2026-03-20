# Delegate Type Declarations

> **Implementation status:** ✅ Implemented in v0.2.6 (Phase 12.1). `DelegateDef` AST node, semantic analysis, and C# delegate emission fully working.

Delegates are named function type declarations that map directly to C# delegate types. They provide reusable, named function signatures that can be used as event handler types, callback parameters, and variance-annotated function types.

## Syntax

```python
delegate Factory[out T]() -> T
delegate Handler[in T](value: T) -> None
delegate Transformer[in TIn, out TOut](input: TIn) -> TOut
delegate Predicate[in T](value: T) -> bool
```

## Basic Usage

```python
# Simple callback delegate
delegate Callback(message: str) -> None

def register(callback: Callback) -> None:
    callback("registered")

# With generic type parameters
delegate Mapper[T, R](value: T) -> R

def apply[T, R](items: list[T], mapper: Mapper[T, R]) -> list[R]:
    return [mapper(item) for item in items]
```

## Variance

Delegates support covariance (`out`) and contravariance (`in`) on type parameters, enabling safe substitution based on type hierarchies. See [Generic Variance](generic_variance.md) for details.

```python
delegate Producer[out T]() -> T
delegate Consumer[in T](value: T) -> None

# Covariance: Producer[Dog] assignable to Producer[Animal]
dog_producer: Producer[Dog] = lambda: Dog("Rex")
animal_producer: Producer[Animal] = dog_producer  # OK

# Contravariance: Consumer[Animal] assignable to Consumer[Dog]
animal_handler: Consumer[Animal] = lambda a: print(a)
dog_handler: Consumer[Dog] = animal_handler  # OK
```

## Comparison with Function Types

| Feature | Function type `(T) -> R` | Delegate `delegate F(x: T) -> R` |
|---------|--------------------------|----------------------------------|
| Named | No (anonymous) | Yes |
| Variance annotations | No | Yes (`in`/`out`) |
| Parameter names | No | Yes (for documentation) |
| Interop with C# delegates | Via `Func<T,R>`/`Action<T>` | Direct mapping |
| Use as event type | Possible but anonymous | Preferred for events |

## C# Emission

```python
# Sharpy
delegate Factory[out T]() -> T
delegate Handler[in T](value: T) -> None
```

```csharp
// C# 9.0
public delegate T Factory<out T>();
public delegate void Handler<in T>(T value);
```

*Implementation: ✅ Native — direct mapping to C# `delegate` declarations.*

## When to Use Delegates

Use a `delegate` declaration when you need one or more of these:

- **Event handler types** — .NET events require a named delegate type. Use `delegate` for any type passed to `event`.
- **Variance annotations** — Only delegates support `in`/`out` on type parameters. If you need covariant returns or contravariant parameters, use `delegate`.
- **Distinct named C# type for interop** — If C# code consumes your API and expects a specific named delegate (not `Func<>`/`Action<>`), use `delegate`.

For everything else — internal callbacks, local function signatures, higher-order function parameters — prefer a `type` alias with a function type:

```python
# Preferred for internal use
type Callback[T] = (T) -> None
type Predicate[T] = (T) -> bool

# Use delegate only when the above reasons apply
delegate EventHandler[in T](sender: object, args: T) -> None
```

See also [Function Types — Delegates vs function types](function_types.md#delegates-vs-function-types) and [Type Aliases](type_aliases.md).

## See Also

- [Function Types](function_types.md) — Anonymous function type syntax
- [Generic Variance](generic_variance.md) — Covariance and contravariance
- [Events](events.md) — Events use delegates as handler types
- [Lambdas](lambdas.md) — Lambda expressions as delegate instances
