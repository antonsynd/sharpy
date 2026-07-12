# Overload Resolution

This page is the **authoritative specification** for how Sharpy selects one target when a name
(function, method, constructor, operator dunder, or builtin) has more than one candidate. It defines
two things: which candidates are **applicable** to a call, and which applicable candidate is **better**
than the others. Where this page and the current implementation disagree, the page governs
(CLAUDE.md Critical Rule 7); the divergences are flagged inline under
*Current implementation status* and tracked by [#1043](https://github.com/antonsynd/sharpy/issues/1043).

Overloading is a **.NET-side** concept (Axiom 1). Python has no static overloads — a later `def`
simply rebinds the name, so `def f(x)` followed by `def f(x, y)` leaves only the two-argument function
and `f(1)` raises `TypeError`. Sharpy instead keeps every same-named declaration as a distinct
overload and resolves the call at compile time, matching C#. See
[Function and Method Overloading](method_overloading.md) for the declaration rules and restrictions.

## Model

Resolution has two phases, mirroring C#'s *§12.6.4 Overload resolution*:

1. **Applicability** — reduce the candidate set to those that could accept the call at all.
2. **Betterness** — among applicable candidates, pick the single best. If no candidate is strictly
   better than every other, the call is **ambiguous** and `SPY0353` is reported.

The same betterness rules apply to all resolution sites. What differs between sites is only the *entry
shape* (how the candidate list and argument types are gathered), documented under
[The three resolution engines](#the-three-resolution-engines).

## Applicability

A candidate is applicable to a call when **all** of the following hold:

### Arity

Let *required* be the count of parameters that have neither a default nor variadic (`*args`) marker,
and *total* be the parameter count excluding an implicit `self`. For a non-variadic candidate, the call
is arity-applicable when `required ≤ argCount ≤ total`. For a variadic candidate, `argCount ≥ required`
(the variadic parameter absorbs any surplus). Defaults widen the applicable range; `self` is excluded
throughout.

### Keyword-argument names

Every keyword argument at the call site must name a parameter that exists on the candidate. A candidate
lacking a matching parameter name is eliminated. Keyword filtering also verifies that the positional
arguments cover exactly the required parameters *not* supplied by keyword. This is what lets
`merge(a, b, reverse=True)` choose the overload that declares a `reverse` parameter over a `*args`
overload that does not. Parameter names are taken from the **static type** of the receiver, so an
override that renames a parameter is matched against the receiver's declared type, not its runtime type
(see [Named Arguments in Overload Resolution](function_parameters.md#named-arguments-in-overload-resolution)).

### Assignability of each argument

Each argument type must be **assignable** to its corresponding parameter type. Assignability, for
overload purposes, is:

- **Identity** — the same type.
- **Reference conversions** — a derived type to a base type or implemented interface, using CLR
  metadata where available (this is what lets `list[int]` bind to a CLR `IEnumerable<int>` parameter).
- **Generic variance** — same-name generics compare each type-argument position under the definition's
  declared `in`/`out`/invariant variance; different-name generics are matched through the source's
  instantiated supertypes.
- **Nullable / optional wrapping** — a non-null `T` is assignable to `T?` (both the interop
  `NullableType` and the safe `OptionalType`).
- **Delegate compatibility** — a function type binds to a delegate parameter with a compatible
  signature.
- **The documented primitive coercions** (below).
- **`list[T]` → `array[T]`** at the argument-binding boundary only (element types must match exactly;
  this coercion is deliberately *not* available in ordinary assignment).
- A **bare type parameter** (`T`) as a parameter type acts as a wildcard — it accepts any argument, and
  the concrete binding is left to C#'s later generic inference. A *structured* generic parameter
  (`list[T]`, `list[list[T]]`) must match the argument's shape recursively, with bare parameters acting
  as wildcards only at their own position.

#### Primitive implicit coercions

The numeric coercions permitted during applicability are exactly those in
`PrimitiveCatalog.CanImplicitlyConvert`, which follows C#'s implicit numeric conversions. For source
type *S* and target type *T*:

| From *S* | Implicitly converts to *T* | Notes |
|----------|----------------------------|-------|
| Any integer | A wider or equal integer of the **same** signedness | `int → long`, `short → int`, `byte → ushort` |
| Unsigned integer | A **signed** integer strictly wider than it | `byte → short`, `uint → long`; but **not** `uint → int` |
| Signed integer | An unsigned integer | **Never** implicit |
| Any integer | `float32`, `float`/`float64`, `double` | Allowed even where precision may be lost (`long → float`), matching C# |
| Any integer | `decimal` | Allowed |
| `float32` | `float`/`float64`/`double` | Widening to 64-bit float |
| `decimal` | Any floating-point | **Never** implicit (and no floating-point → `decimal`) |
| Non-numeric (`bool`, `str`, `char`, …) | Only itself | No implicit primitive coercions |

`float` and `float64` are aliases for the 64-bit `double`. Narrowing conversions (e.g. `long → int`,
`double → int`) are never implicit and never make a candidate applicable — they require an explicit
`to` conversion at the call site.

If applicability leaves **zero** candidates, `SPY0354` (*no matching overload*) is reported. If it
leaves exactly one, that candidate is selected. If it leaves more than one, betterness decides.

## Betterness

Among applicable candidates, one candidate is **better** than another when it is at least as good at
every argument position and strictly better at one. The tie-break criteria are applied in the following
order; the first that yields a unique winner selects the target.

1. **Exact match over conversion.** At a given argument position, a parameter whose type is *identical*
   to the argument beats one that requires any implicit conversion.
2. **Better implicit conversion.** When both parameters require a conversion, the one with the lower
   conversion cost wins, per the [conversion-cost ranking](#conversion-cost-ranking) below. This
   realizes C#'s "better conversion target" rule (§12.6.4.4): for two candidate target types to which
   the argument converts, the target that itself implicitly converts to the other is the better one
   (`int → long` is preferred over `int → double` because `long` implicitly converts to `double`).
3. **More specific type.** When neither conversion dominates, the structurally more specific parameter
   wins: a parameter assignable to the other but not vice-versa (`list[int]` beats `IEnumerable[int]`),
   and a structured type beats a bare type parameter at the same position (`list[list[T]]` beats
   `list[T]` for a nested literal). This is C#'s §12.6.4.4 shape rule.
4. **Fewer type parameters.** A less-generic overload beats a more-generic one
   (`Merge[T](a, b)` beats `Merge[T, TKey](iterables, key)` when both are exact-arity matches).
5. **Non-variadic over variadic.** A fixed-arity parameter list beats one that binds the argument
   through a `*args` parameter.
6. **CLR-level specificity.** When two parameters have equal Sharpy types but different underlying CLR
   types (e.g. `ClrTypeMapper` maps both `Sharpy.List<T>` and `IEnumerable<T>` to `list[T]`), the more
   derived CLR type wins.

If, after all six criteria, no single candidate is strictly better than every other, the call is
**ambiguous** and `SPY0353` is reported. Disambiguate with an explicit `to` conversion at the call site.

### Conversion-cost ranking

Criterion 2 consumes a declarative cost assigned to each argument-to-parameter conversion. Lower cost is
better; the authoritative ordering is C#'s "better conversion target" partial order, and the table below
is a C#-consistent linearization of it for the common conversions:

| Conversion class | Cost |
|------------------|------|
| Identical type (exact match) | 0 |
| Reference conversion (derived → base, type → interface) | 1 |
| Numeric widening, one step along the widening lattice | 2 (plus 1 per additional step) |
| User-defined implicit conversion (`op_Implicit`) | strictly worse than any builtin widening |
| Boxing to `object` | worst |

The widening lattice is the transitive closure of the [primitive coercion table](#primitive-implicit-coercions)
(`sbyte → short → int → long`; integers → `float32` → `double`; integers → `decimal`; and so on). Cost
compares candidates only when the two target types are **ordered** by the lattice — i.e. one implicitly
converts to the other. When the two candidate targets are **incomparable** (e.g. `decimal` and `double`,
between which no implicit conversion exists in either direction), criterion 2 yields no winner and
resolution falls through to the later criteria; if none breaks the tie, the call is ambiguous. This is
the property the [#1043](https://github.com/antonsynd/sharpy/issues/1043) property tests assert:
resolution is **independent of declaration order** and of which equally-costed candidate the algorithm
happened to visit first.

> **Axiom 1 — where Sharpy and C# could disagree, match C#.** The cost lattice, the "better conversion
> target" rule, and the specificity shape rule are all cribbed directly from C#'s better-function-member
> algorithm so that a Sharpy overload set and its emitted C# resolve identically. Sharpy adds no
> betterness axis that C# lacks.

### Worked examples

```python
def f(x: int) -> str: ...
def f(x: float) -> str: ...

f(42)     # f(int) — exact match beats the int→float widening (criterion 1)
f(3.14)   # f(float) — exact match
```

```python
def g(x: int) -> str: ...
def g(x: long) -> str: ...
def g(x: double) -> str: ...

g(42)     # g(int) — exact match
# with only long and double candidates, g(42) picks long:
#   int→long is a better conversion target than int→double (criterion 2)
```

```python
def h(xs: list[int]) -> int: ...
def h(xs: IEnumerable[int]) -> int: ...

h([1, 2, 3])   # h(list[int]) — more specific type (criterion 3)
```

```python
def bad(x: int, y: float) -> None: ...
def bad(x: float, y: int) -> None: ...

bad(1, 2)   # SPY0353 ambiguous — neither candidate is better at both positions
```

## The three resolution engines

Sharpy resolves overloads at three structurally different call shapes. All three are specified to apply
the **same** applicability and betterness rules; they differ only in how the candidate list and argument
types are assembled.

### 1. Ordinary calls (functions, methods, builtins)

Function calls, instance/static method calls, and builtin-function calls run the shared core: the
two-pass applicability filter followed by the deterministic betterness chain above. Method resolution
walks the base-class chain and then implemented interfaces to gather candidates. Builtin functions
(`len`, `min`, `max`, `sorted`, …) resolve through the same core.

> **Current implementation status.** The shared core (`ResolveOverloadCore`) already implements
> applicability plus the exact-arity → fewer-type-parameters → specificity → ambiguous tie-break chain,
> and it approximates conversion betterness through assignability-directed specificity (an argument that
> is assignable to a parameter but not vice-versa is treated as more specific). What is missing, pending
> [#1043](https://github.com/antonsynd/sharpy/issues/1043), is the **explicit, declarative
> conversion-cost table** (criterion 2). Separately, builtin resolution currently short-circuits on the
> *first* applicable candidate in `BuiltinRegistry` registration order (the `ReturnFirstMatch` path),
> which makes a small set of builtin results depend on registration order; #1043 replaces that path with
> the same order-independent betterness chain.

### 2. Operator dunders and `__getitem__`

Binary-operator dunders (`__add__`, `__eq__`, `__matmul__`, …) and `__getitem__` dispatch to the
overload set declared for the operator on the operand's type. These resolve through the **same**
applicability and betterness core as ordinary calls.

> **Current implementation status.** Operator-dunder and `__getitem__` resolution currently runs a
> *separate*, weaker resolver (`TypeInferenceService.FindBestOverload`): four ordered `FirstOrDefault`
> tiers (exact → assignable → generic-shape → bare-type-parameter) in which **declaration order breaks
> ties**, the bare-type-parameter tier ignores generic constraints, and only two-parameter (binary)
> dunders are handled. This is [#975](https://github.com/antonsynd/sharpy/issues/975). Under this spec
> it is being routed through the shared deterministic core so that operator overloads resolve
> order-independently and with the same specificity rules as ordinary calls
> ([#1043](https://github.com/antonsynd/sharpy/issues/1043)).

### 3. Constructors

A class may declare multiple `__init__` overloads. When a class has **exactly one** `__init__`, Sharpy
type-checks the call against it (arity, positional/keyword kinds, spread-into-non-variadic). When a
class has **more than one** `__init__`, Sharpy performs only the shape checks and **defers the overload
selection to Roslyn** — the emitted C# constructor overloads are resolved by the C# compiler using its
own better-function-member algorithm.

This deferral is a **deliberate Axiom-1 decision**, not a gap: C# already resolves constructor overloads
by exactly the rules this page specifies, and delegating to it guarantees the selected constructor
matches what the generated assembly runs, including edge cases (`None` to a nullable parameter, enum
conversions) that the Sharpy-side checker would have to re-derive. The trade-off is that a
constructor-overload ambiguity surfaces as a C# diagnostic rather than a `SPY03xx` code; the
pipeline no-CS-leaks invariant (CLAUDE.md, #1035) constrains where that is acceptable.

## Diagnostics

| Code | Level | Meaning |
|------|-------|---------|
| `SPY0353` | Error | Ambiguous overload — more than one candidate is applicable and none is strictly better |
| `SPY0354` | Error | No matching overload — no candidate is applicable to the call |
| `SPY0355` | Error | Duplicate method signature — two overloads have identical parameter signatures (overloads may not differ only by return type) |

Constructor-overload failures on a class with multiple `__init__` methods are reported by the C#
compiler (see [engine 3](#3-constructors)).

## See Also

- [Function and Method Overloading](method_overloading.md) — declaring overloads; restrictions
- [Function Parameters](function_parameters.md) — parameters, defaults, named and variadic arguments
- [Operator Overloading](operator_overloading.md) — dunder methods behind operators
- [Constructors](constructors.md) — constructor definition, chaining, and overloading
- [C# Language Specification §12.6.4 — Overload resolution](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#1264-overload-resolution)
