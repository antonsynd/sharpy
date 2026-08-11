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

## Trailing Rule

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

## Referencing an Earlier Type Parameter

A default may name a type parameter declared **before** it in the same list, on its own or nested
inside another generic (as `V = list[K]` above). The reference means whatever that parameter was
bound to at the use site:

```python
class Dup[K, V = K]:
    a: K
    b: V

    def __init__(self, a: K, b: V):
        self.a = a
        self.b = b


d = Dup[str]("a", "b")     # Dup[str, str] — V takes K's value
e = Dup[int, str](1, "b")  # an explicit argument still overrides the default
```

Defaults are resolved strictly left to right, so a chain works and each link sees the value its
predecessor resolved to:

```python
class Extent[StartT, StopT = StartT, StepT = StopT]:
    ...

# Extent[str] is Extent[str, str, str]
```

Three references are refused, all under SPY0347, because a default is read where the declaration is
instantiated and only the preceding parameters have values there:

```python
# ERROR (SPY0347): V is declared after K
class Pair[K = V, V = int]:
    pass

# ERROR (SPY0347): a parameter is not declared yet at the point its own default is read
class Loop[K = K]:
    pass

# ERROR (SPY0347): T belongs to the enclosing class, not to make's parameter list.
# Note that `w: T` in the same signature would be fine — T is in scope for annotations.
class Outer[T]:
    def make[U, W = T](self, u: U, w: W) -> U:
        ...
```

The enclosing-scope restriction is **deliberate and could be lifted**. It is not a consequence of
how defaults are represented: at `Outer[int]().make[str](...)` the enclosing `T` does have a value,
so supplying it would be well-defined. It is refused because a default is read where the declaration
is instantiated, and taking the enclosing binding into account there would make a parameter list's
meaning depend on its receiver. PEP 696 excludes it for the same reason. A future change that wants
it need only thread the receiver's binding into default resolution; nothing in the current design
prevents it.

The same rules apply to generic functions:

```python
def echo_pair[K, V = K](a: K, b: V) -> V:
    ...
```

## Constraint Satisfaction

Default types must satisfy any constraints on the type parameter. If a default type violates a constraint, the compiler emits SPY0396:

```python
interface Ranked:
    def rank(self) -> int:
        ...

# ERROR (SPY0396) if default type doesn't satisfy constraint
class Sorted[T: Ranked = int]:
    pass
```

A default that **is** an earlier type parameter carries no concrete type to test, so what must
satisfy the constraint is that parameter's own constraints:

```python
interface Shape:
    def area(self) -> int:
        ...

# OK: K is itself constrained to Shape
class Pair[K: Shape, V: Shape = K]:
    pass

# ERROR (SPY0396): nothing forces K to be a str
class Loose[K, V: str = K]:
    pass
```

## When Defaults Are Checked

A default is validated at its **declaration**, whether or not the declaration is ever used. An
unresolvable default type, an out-of-scope reference and a constraint violation are each reported
once, at the parameter list, rather than at each site that omits the type argument.

## Where Defaults Fill

Defaults fill **partially-written annotation and reference positions** (`x: Dup[str]` means
`Dup[str, str]`), **call positions** (`echo_pair("a", "b")`), and **base lists**. A base position
means what an annotation position means: the omitted trailing arguments fill from the defaults
(#1404).

```python
interface HolderD[T = str]:
    def get(self) -> T: ...


class Good(HolderD):           # OK — the base is HolderD[str]
    def get(self) -> str:
        return "d"


class Explicit(HolderD[str]):  # OK — the same base, written out
    def get(self) -> str:
        return "d"
```

It is the same fill, so a default written in terms of an earlier parameter closes over the
arguments the base list supplies:

```python
class Dup[K, V = K]:
    key: K
    value: V

    def __init__(self, key: K, value: V) -> None:
        self.key = key
        self.value = value


class ChildStr(Dup[str]):      # Dup[str, str]
    def __init__(self) -> None:
        super().__init__("a", "b")


class ChildOpen[K](Dup[K]):    # Dup[K, K] — K here is ChildOpen's own parameter
    def __init__(self, k: K) -> None:
        super().__init__(k, k)
```

Defaults fill from the **right**, so a base list may omit a defaulted tail but never a leading
undefaulted parameter — the arity refusal survives for that case:

```python
class Pair[T, U = str]:
    first: T
    second: U


class BadPair(Pair):        # ERROR (SPY0224): Type 'Pair' expects 2 type arguments but got 0
    ...


class GoodPair(Pair[int]):  # OK — Pair[int, str]
    ...
```

A base list names the concrete shape a declaration is built on, and that shape is read in more
places than an annotation is: the base/interface reference the supertype walk consults, and the
generated C# base list itself. The fill is materialized into both, because a base accepted but
left unfilled would reach Roslyn as `CS0305` behind SPY0908 — worse than the refusal it replaced.
That is why base lists refused defaults until every one of those channels could carry the fill
(#1286, #1404).

## Diagnostics

| Code | Level | Description |
|------|-------|-------------|
| SPY0347 | Error | Default references a type parameter declared after it, itself, or one from an enclosing declaration |
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
- *Trailing validation: SPY0395*
- *Constraint satisfaction: SPY0396*
- *Earlier-parameter resolution: `TypeResolver.ResolveTypeParameterDefault`; reference validation SPY0347 (#1245)*
- *Base-list fill: `NameResolver.TryCompleteBaseReferenceArguments` / `FillTypeParameterDefaults`, materialized into the base/interface reference and — via `TypeChecker.RecordCompletedBaseAnnotations` — into `SemanticInfo` for the emitted base list (#1404)*
