# Generic Variance (`in` and `out` Type Parameters)

> **Implementation status:** Not yet implemented — planned for Phase 12 (v0.2.6). `TypeParameterDef` does not yet have variance markers.

Generic variance allows substitution of generic types based on their type arguments' inheritance relationships. Sharpy supports covariance (`out`) and contravariance (`in`) annotations on interface and delegate type parameters.

## Overview

| Variance | Keyword | Data Flow | Substitution Rule |
|----------|---------|-----------|-------------------|
| Covariant | `out` | Type flows **out** (returned) | More derived → base allowed |
| Contravariant | `in` | Type flows **in** (consumed) | Less derived → derived allowed |
| Invariant | (none) | Both directions | Exact match required |

## Covariance (`out T`)

A type parameter marked `out` can only appear in output positions (return types). This enables safe substitution with more derived types.

```python
interface IProducer[out T]:
    """Produces values of type T."""
    def get(self) -> T: ...
    def peek(self) -> T?: ...

# Covariance in action
class DogProducer(IProducer[Dog]):
    def get(self) -> Dog:
        return Dog("Buddy")
    
    def peek(self) -> Dog?:
        return self._next_dog

# Dog is a subtype of Animal, so IProducer[Dog] is a subtype of IProducer[Animal]
producer: IProducer[Animal] = DogProducer()  # ✅ OK: covariant
animal = producer.get()  # Returns Dog, but typed as Animal
```

**Why this is safe:** When you ask for an `Animal`, receiving a `Dog` is always valid because `Dog` is-an `Animal`.

### Valid `out` Positions

```python
interface ICovariant[out T]:
    # ✅ Valid: T in return position
    def get(self) -> T: ...
    def get_optional(self) -> T?: ...
    def get_list(self) -> list[T]: ...  # Assuming list is covariant
    
    # ✅ Valid: T in covariant nested position
    def get_producer(self) -> IProducer[T]: ...
    
    # ❌ Invalid: T in parameter position
    # def set(self, value: T): ...  # ERROR: T is covariant
    
    # ❌ Invalid: T in contravariant nested position  
    # def get_consumer(self) -> IConsumer[T]: ...  # ERROR
```

## Contravariance (`in T`)

A type parameter marked `in` can only appear in input positions (parameters). This enables safe substitution with less derived types.

```python
interface IConsumer[in T]:
    """Consumes values of type T."""
    def accept(self, value: T): ...
    def process(self, items: list[T]): ...

# Contravariance in action
class AnimalHandler(IConsumer[Animal]):
    def accept(self, value: Animal):
        print(f"Handling: {value.name}")
    
    def process(self, items: list[Animal]):
        for item in items:
            self.accept(item)

# Animal is a supertype of Dog, so IConsumer[Animal] is a subtype of IConsumer[Dog]
handler: IConsumer[Dog] = AnimalHandler()  # ✅ OK: contravariant
handler.accept(Dog("Rex"))  # AnimalHandler can handle any Animal, including Dog
```

**Why this is safe:** A handler that can process any `Animal` can certainly process a `Dog`, since `Dog` is-an `Animal`.

### Valid `in` Positions

```python
interface IContravariant[in T]:
    # ✅ Valid: T in parameter position
    def accept(self, value: T): ...
    def process(self, items: list[T]): ...
    
    # ✅ Valid: T in contravariant nested position
    def set_producer(self, producer: IProducer[T]): ...  # Flipped!
    
    # ❌ Invalid: T in return position
    # def get(self) -> T: ...  # ERROR: T is contravariant
    
    # ❌ Invalid: T in covariant nested position
    # def get_consumer(self) -> IConsumer[T]: ...  # ERROR: double flip = covariant
```

## Invariance (Default)

Without a variance annotation, a type parameter is invariant and can appear in any position, but generic types are not substitutable.

```python
interface IMutable[T]:
    """Both produces and consumes T — must be invariant."""
    def get(self) -> T: ...
    def set(self, value: T): ...

# Invariant types require exact match
mutable: IMutable[Animal] = SomeMutable[Animal]()  # ✅ OK: exact match
# mutable: IMutable[Animal] = SomeMutable[Dog]()   # ❌ ERROR: Dog ≠ Animal
```

**Why invariance is required:** If `IMutable[Dog]` were assignable to `IMutable[Animal]`, you could call `set(Cat())` on what's actually a `Dog` container — type safety violation.

## Multiple Type Parameters

Each type parameter can have independent variance:

```python
interface IConverter[in TInput, out TOutput]:
    """Converts input to output."""
    def convert(self, input: TInput) -> TOutput: ...

# Converter[Animal, Dog] can substitute for Converter[Dog, Animal]
# - in TInput: Animal → Dog (contravariant: accept more general)
# - out TOutput: Dog → Animal (covariant: return more specific)
converter: IConverter[Dog, Animal] = SomeConverter[Animal, Dog]()  # ✅ OK
```

## Delegates with Variance

Delegates (function types) also support variance annotations:

```python
# Covariant delegate — returns T
delegate Producer[out T]() -> T

# Contravariant delegate — accepts T
delegate Consumer[in T](value: T) -> None

# Mixed variance
delegate Transformer[in TIn, out TOut](input: TIn) -> TOut

# Usage
dog_producer: Producer[Dog] = lambda: Dog("Max")
animal_producer: Producer[Animal] = dog_producer  # ✅ Covariant

animal_consumer: Consumer[Animal] = lambda a: print(a.name)
dog_consumer: Consumer[Dog] = animal_consumer  # ✅ Contravariant
```

## Built-in Variant Types

Common .NET interfaces and delegates have variance annotations:

| Type | Variance | Notes |
|------|----------|-------|
| `IEnumerable[out T]` | Covariant | Read-only iteration |
| `IReadOnlyList[out T]` | Covariant | Read-only indexed access |
| `IReadOnlyCollection[out T]` | Covariant | Read-only collection |
| `IComparer[in T]` | Contravariant | Compares T values |
| `IComparable[in T]` | Contravariant | Compares to T |
| `IEquatable[in T]` | Contravariant | Equality with T |
| `Action[in T]` | Contravariant | Consumes T |
| `Func[out T]` | Covariant | Produces T |
| `Func[in T, out R]` | Mixed | Transforms T to R |
| `Predicate[in T]` | Contravariant | Tests T |

## Restrictions

Variance annotations are only valid on:
- Interface type parameters
- Delegate type parameters

Classes and structs cannot have variant type parameters:

```python
# ✅ Valid: interface with variance
interface IReadable[out T]:
    def read(self) -> T: ...

# ❌ Invalid: class cannot have variance
# class Reader[out T]:  # ERROR: variance not allowed on classes
#     ...

# ❌ Invalid: struct cannot have variance
# struct Wrapper[out T]:  # ERROR: variance not allowed on structs
#     ...
```

## Variance and Constraints

Variance annotations can be combined with type constraints:

```python
interface IAnimalProducer[out T: Animal]:
    """Produces animals of type T."""
    def produce(self) -> T: ...

interface IComparableConsumer[in T: IComparable[T]]:
    """Consumes comparable values."""
    def compare(self, a: T, b: T) -> int: ...
```

## C# Emission

```python
# Sharpy
interface IProducer[out T]:
    def get(self) -> T: ...

interface IConsumer[in T]:
    def accept(self, value: T): ...

interface IConverter[in TIn, out TOut]:
    def convert(self, input: TIn) -> TOut: ...

delegate Factory[out T]() -> T
delegate Handler[in T](value: T) -> None
```

```csharp
// C# 9.0
public interface IProducer<out T>
{
    T Get();
}

public interface IConsumer<in T>
{
    void Accept(T value);
}

public interface IConverter<in TIn, out TOut>
{
    TOut Convert(TIn input);
}

public delegate T Factory<out T>();
public delegate void Handler<in T>(T value);
```

*Implementation: ❌ Not yet implemented — planned for Phase 12 (v0.2.6)*
- *`out T` → `out T` (C# covariance) — not yet parsed*
- *`in T` → `in T` (C# contravariance) — not yet parsed*
- *Variance validation will be performed at compile time*
- *Position checking will be enforced by semantic analyzer*

## Compiler Validation

The compiler validates variance annotations by checking each usage of a variant type parameter:

1. **Covariant (`out T`)** — T must only appear in:
   - Return types
   - `out` parameters
   - Covariant positions of other types

2. **Contravariant (`in T`)** — T must only appear in:
   - Parameter types (not `out`)
   - Contravariant positions of other types

3. **Nested variance flips:**
   - Covariant in contravariant = contravariant
   - Contravariant in contravariant = covariant
   - Covariant in covariant = covariant
   - Contravariant in covariant = contravariant

**Error example:**

```python
interface IBroken[out T]:
    def set(self, value: T): ...  
    # ERROR: Type parameter 'T' is covariant but appears in contravariant position
```

## See Also

- [Generics](generics.md) — Generic types and constraints
- [Interfaces](interfaces.md) — Interface definitions
- [Delegates](delegates.md) — Named delegate types
- [Type Casting](type_casting.md) — Converting between types
