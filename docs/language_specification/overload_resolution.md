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

## Argument types are computed before candidates are considered

Each argument's type is computed from the argument alone. A **candidate set** contributes no
contextual (expected) type to any argument: a collection literal, a comprehension, or any other
context-sensitive expression takes its contextual type only from a **resolved** target — a callee
with exactly one candidate, or a parameter of an overload that has already been chosen.

This is what makes resolution independent of declaration order. Were an unresolved candidate
allowed to type the argument, the recorded type would then decide which candidate is applicable
and better, and the answer would depend on which `def` was written first:

```python
def h(v: list[float]) -> str:
    return "float"

def h(v: list[int]) -> str:
    return "int"

def main() -> None:
    print(h([1, 2]))   # int — the literal is list[int], the exact match wins
```

Swapping the two declarations prints `int` as well. The same holds for `set` and `dict` literals,
for comprehensions, and for every candidate-set source: overloaded module functions (imported or
module-qualified), overloaded instance methods, overloaded builtins, and reflected .NET method
groups such as `statistics.mean`, whose `list[int]` overload binds `mean([1, 2, 3])`.

A callee with a **single** candidate is a resolved target, so contextual typing applies there in
full — `def g(v: list[Base])` accepts `g([Derived(), Derived()])`, with the literal recorded as
`list[Base]`.

## Applicability

A candidate is applicable to a call when **all** of the following hold:

### Arity

Let *required* be the count of parameters that have neither a default nor variadic (`*args`) marker,
and *total* be the parameter count excluding an implicit `self`. For a non-variadic candidate, the call
is arity-applicable when `required ≤ argCount ≤ total`. For a variadic candidate, `argCount ≥ required`
(the variadic parameter absorbs any surplus). Defaults widen the applicable range; `self` is excluded
throughout.

### Keyword arguments

Every keyword argument at the call site is bound to the parameter it names on each candidate. A candidate
lacking a matching parameter name, or whose named parameter was already filled by a positional argument,
is eliminated. The keyword argument's **type** participates in the assignability and betterness checks
exactly as a positional argument's type does — there is no "name-only filtering" step that discards
keyword types. This is what lets `g(x=1)` choose `g(x: int)` over `g(x: str)` in the same way that
`g(1)` chooses the `int` overload positionally. Every betterness criterion reads the parameter a
keyword argument RESOLVED to, so the criteria that consult a candidate's *declared* parameter —
criterion 3 (more specific declared shape) and criterion 4 (CLR-level specificity) — see a keyword
argument exactly as they see a positional one. With `def f[V](v: V)` and `def f[K, V](v: dict[K, V])`,
both `f(d)` and `f(v=d)` select the `dict` overload for a `dict[str, int]`; a keyword spelling that
resolved differently from its positional twin would be a defect, not a rule. Parameter names are taken from the **static type** of
the receiver, so an override that renames a parameter is matched against the receiver's declared type,
not its runtime type (see [Named Arguments in Overload Resolution](function_parameters.md#named-arguments-in-overload-resolution)).

#### When no candidate can bind a keyword

A keyword argument that **no** candidate can bind is a failure of that argument, not of the call, and
is reported by the argument's own code at the argument's own span — the same code a single-candidate
callee gives for the same mistake:

| What is wrong with the keyword | Code |
|--------------------------------|------|
| No candidate declares a parameter of that name | `SPY0234` |
| Every candidate's parameter of that name is positional-only | `SPY0370` |
| Every candidate's parameter of that name is already filled positionally | `SPY0235` |

This holds on every route, including `super().__init__(...)` and `self.__init__(...)`. `SPY0354` is
reserved for the failures the candidates disagree about: an arity mismatch, or candidates that reject
*different* arguments. A candidate set where even one candidate binds the keyword is not a binding
failure at all — it is decided by applicability and betterness like any other call.

The **arity** check runs before binding, so a call that passes more arguments than any candidate
declares reports the arity failure (`SPY0224` for a single candidate, `SPY0354` for an overload set)
rather than a keyword code, even when a keyword is also unbindable.

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
- **Constant conversions** (C# §10.2.11) — an integer constant whose value fits in a narrower integer
  type satisfies a parameter of that type, exactly as it would satisfy a variable annotation. Both
  bare literals (`200`) and `const` references (`const LIMIT: int = 200`) participate.
- **`list[T]` → `array[T]`** at the argument-binding boundary only (element types must match exactly;
  this coercion is deliberately *not* available in ordinary assignment).
- **Per-candidate generic inference.** For a generic candidate without written type arguments, the
  candidate's type parameters are inferred from its bound `(formal, actual)` pairs before the
  assignability check. A candidate whose inference fails — a conflict between two arguments binding
  different concrete types to the same type parameter, or a type parameter no argument binds and no
  PEP-696 default supplies — is **not applicable** (C# §12.6.3). On success, the candidate's formals
  are closed (every `T` replaced by its inferred concrete type) and the assignability check runs
  against those closed types. Explicit type arguments or receiver substitutions close the candidate's
  formals before inference, not through it.

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

1. **Identity match (§12.6.4.6).** At a given argument position, a parameter whose type is *identical*
   to the argument's natural type beats one that requires any conversion — including a constant
   conversion. This is what makes `f(200)` with overloads `f(b: uint8)` / `f(i: int)` select `f(int)`:
   the argument's type is `int`, which matches `int` identically.
2. **Better implicit conversion.** When both parameters require a conversion, the one with the lower
   conversion cost wins, per the [conversion-cost ranking](#conversion-cost-ranking) below. This
   realizes C#'s "better conversion target" rule (§12.6.4.4): for two candidate target types to which
   the argument converts, the target that itself implicitly converts to the other is the better one
   (`int → long` is preferred over `int → double` because `long` implicitly converts to `double`).
   Constant conversions participate in this comparison: when both candidates are reachable via a
   constant conversion, the narrower type that implicitly converts to the wider one is preferred
   (`uint8` beats `int16` because `uint8 → int16` exists).
3. **More specific type (§12.6.4.5).** When neither conversion dominates, the structurally more
   specific parameter wins. Two comparisons, in this order:
   - a parameter *assignable* to the other but not vice-versa is better (`list[int]` beats
     `IEnumerable[int]`);
   - failing that, the more specific **declared** shape is better — the shape as *written*, before
     inference closed it. C#'s rule is recursive: a type parameter is less specific than anything that
     is not one, and between two constructed types over the same generic, the one with at least one
     more specific type argument and none less specific wins. So `dict[K, V]` beats a bare `V`, and
     `list[list[T]]` beats `list[T]`.

   The second comparison is what decides a pair whose *closed* formals are identical. With
   `def f[T](v: list[T])` and `def f[T](v: list[list[T]])`, per-candidate inference closes both
   formals to `list[list[int]]` for a `list[list[int]]` argument: only the declared shapes still
   differ, and `list[list[T]]` is the more specific one. Measured against `csc` on `net10.0`
   (2026-09-10): `F<T>(List<T>)` vs `F<T>(List<List<T>>)` called with a `List<List<int>>` selects the
   nested overload, in the positional and the named spelling alike.
4. **CLR-level specificity.** When two parameters have equal Sharpy types but different underlying CLR
   types (e.g. `ClrTypeMapper` maps both `Sharpy.List<T>` and `IEnumerable<T>` to `list[T]`), the more
   derived CLR type wins. This is part of the per-argument comparison, not a late tie-break: it is a
   form of C#'s better-conversion-target rule (§12.6.4.4), which C# also evaluates inside "better
   function member" and therefore *before* the tie-breaks in criteria 6–8.
5. **Signed beats unsigned (§12.6.4.7).** When the conversion lattice is neutral (neither parameter
   type implicitly converts to the other), a signed integer parameter beats an unsigned integer
   parameter. The pairs are: `int8` beats `uint8`; `int16` beats `uint16`; `int32` beats `uint32`;
   `int64` beats `uint64`. More generally, any signed integer beats any unsigned integer when the
   lattice cannot decide.
6. **All arguments correspond (C# §12.6.4.3 bullet 3).** A candidate whose declared parameter count
   matches the argument count (no defaults needed) beats one that needs a default-value substitution.
   This tie-break applies **without** a sequence-equivalence precondition — it decides even when the two
   candidates' parameter types differ at every position.
7. **Fewer type parameters.** Among candidates whose closed parameter sequences are **equivalent** (every
   bound formal is equal by type), a less-generic overload beats a more-generic one
   (`Merge[T](a, b)` beats `Merge[T, TKey](iterables, key)` when both produce the same closed types).
   This tie-break is **gated on sequence equivalence**: when the closed formal types differ, neither
   candidate wins by type-parameter count alone (C# §12.6.4.3; measured: CS0121 for non-equivalent
   sequences with a generic vs. non-generic pair).
8. **Non-variadic over variadic.** Among candidates with equivalent closed parameter sequences, a
   fixed-arity parameter list beats one that binds arguments through a `*args` parameter. This tie-break
   is **gated on sequence equivalence** in the same way as criterion 7 (measured: CS0121 for
   non-equivalent sequences with a normal-form vs. expanded-form pair).

Criteria 1–5 are the per-argument comparison: a candidate is better when it is at least as good at
every argument and strictly better at one. Criteria 6–8 are tie-breaks applied to the candidates that
comparison left tied, and 7 and 8 additionally require the tied candidates' closed parameter sequences
to be equal position by position.

If, after all eight criteria, no single candidate is strictly better than every other, the call is
**ambiguous** and `SPY0353` is reported. Disambiguate with an explicit type annotation or cast at the
call site.

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

h([1, 2, 3])   # h(list[int]) — more specific type (criterion 3, assignability)
```

The declared-shape half of criterion 3 decides the pairs whose *closed* formals are equal, and it
decides them the same way in both spellings:

```python
def f[T](v: list[T]) -> str:
    return "flat"
def f[T](v: list[list[T]]) -> str:
    return "nested"

def g[V](v: V) -> str:
    return "bare"
def g[K, V](v: dict[K, V]) -> str:
    return "structured"

xs: list[list[int]] = [[1], [2]]
f(xs)      # "nested"      — list[list[T]] is the more specific declared shape
f(v=xs)    # "nested"      — the keyword spelling resolves identically
d: dict[str, int] = {"a": 1}
g(d)       # "structured"  — a constructed type beats a bare type parameter
g(v=d)     # "structured"
```

```python
def bad(x: int, y: float) -> None: ...
def bad(x: float, y: int) -> None: ...

bad(1, 2)   # SPY0353 ambiguous — neither candidate is better at both positions
```

#### Constant-conversion tie-break examples

When a constant conversion widens applicability, the three-level tie-break — identity match,
better-conversion-target lattice, signed-beats-unsigned — resolves the resulting ties to match C#:

```python
# Lattice picks the narrower type (uint8→int16 exists):
def f(b: uint8) -> None:
    print("uint8 arm")
def f(s: int16) -> None:
    print("int16 arm")
f(100)   # prints "uint8 arm" — criterion 2, uint8→int16 exists

# The lattice also decides against a WIDER candidate (uint8→int64 exists):
def q(b: uint8) -> None:
    print("uint8 arm")
def q(l: int64) -> None:
    print("int64 arm")
q(200)   # prints "uint8 arm" — criterion 2, uint8→int64 exists

# Signed beats unsigned when the lattice is neutral:
def g(sb: int8) -> None:
    print("int8 arm")
def g(b: uint8) -> None:
    print("uint8 arm")
g(100)   # prints "int8 arm" — criterion 5, int8 beats uint8

# ...across widths too, exactly as C#'s §12.6.4.7 table: the rule is only ever
# reached when the unsigned candidate is the same width or wider (a narrower
# unsigned converts into the signed type, so the lattice decides first):
def r(sb: int8) -> None:
    print("int8 arm")
def r(u: uint64) -> None:
    print("uint64 arm")
r(100)   # prints "int8 arm" — criterion 5, int8 beats uint64

# Identity match trumps constant conversion:
def h(b: uint8) -> None:
    print("uint8 arm")
def h(i: int) -> None:
    print("int arm")
h(200)   # prints "int arm" — criterion 1, argument type int matches int identically

# Const references resolve the same way:
const LIMIT: int = 200
def p(b: uint8) -> None:
    print("uint8 arm")
def p(s: str) -> None:
    print("str arm")
p(LIMIT)  # prints "uint8 arm" — only uint8 is applicable
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

> **Current implementation status.** The shared core (`ResolveOverloadCore`) implements the full
> two-pass pipeline: per-candidate binding with keyword-argument types, per-candidate generic inference,
> assignability-based applicability, and the deterministic betterness chain (conversion betterness first,
> then all-arguments-correspond, then sequence-equivalence-gated tie-breaks). Conversion betterness is
> approximated through assignability-directed specificity (an argument that is assignable to a parameter
> but not vice-versa is treated as more specific). Method resolution walks the base-class chain and
> then implemented interfaces to gather candidates. Builtin resolution runs the same order-independent
> betterness chain ([#1043](https://github.com/antonsynd/sharpy/issues/1043)).

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
class has **more than one** `__init__`, Sharpy runs the same shared overload-resolution core as ordinary
calls — per-candidate binding, generic inference, applicability, and betterness — and reports ambiguity
as `SPY0353` and no-match as `SPY0354`, exactly as it does for function and method calls.

## Refusal shape: the argument, not the overload set

When **every** arity-surviving candidate rejects the **same argument** for a **type** reason, the
call is refused as that argument's type mismatch (`SPY0220`) rather than as an overload failure
(`SPY0354`). There is nothing to choose between the candidates at that point, and the concrete
mismatch is what the caller has to fix:

```python
def main() -> None:
    xs: list[uint64] = [1, 2, 3]
    n: int32 = 2
    print(xs.index(n))
```

```
error[SPY0220]: Cannot pass argument of type 'int32' to parameter of type 'uint64'
```

The refusal is the same one the single-signature member gives for the same needle — `xs.count(n)`
reports that message too — so how many signatures a member happens to have is not observable in the
message.

Candidates that accept **different** types at that index are all named:

```python
class C:
    def __init__(self):
        pass

    def f(self, x: int) -> int:
        return x

    def f(self, x: float) -> int:
        return 1


def main() -> None:
    c: C = C()
    print(c.f("a"))
```

```
error[SPY0220]: Cannot pass argument of type 'str' to parameter of type 'int32' or 'float64'
```

`SPY0354` remains for the cases where the candidates genuinely disagree: an **arity** mismatch, and
candidates that fail at **different** argument indices.

## Diagnostics

| Code | Level | Meaning |
|------|-------|---------|
| `SPY0353` | Error | Ambiguous overload — more than one candidate is applicable and none is strictly better |
| `SPY0354` | Error | No matching overload — the candidates disagree: an arity mismatch, or candidates that fail at different arguments (a call every candidate rejects at the SAME argument reports `SPY0220` — see above) |
| `SPY0355` | Error | Duplicate method signature — two overloads have identical parameter signatures (overloads may not differ only by return type) |

Constructor-overload ambiguity on a class with multiple `__init__` methods is reported as `SPY0353` by
the shared resolver (see [engine 3](#3-constructors)).

## See Also

- [Function and Method Overloading](method_overloading.md) — declaring overloads; restrictions
- [Function Parameters](function_parameters.md) — parameters, defaults, named and variadic arguments
- [Operator Overloading](operator_overloading.md) — dunder methods behind operators
- [Constructors](constructors.md) — constructor definition, chaining, and overloading
- [C# Language Specification §12.6.4 — Overload resolution](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#1264-overload-resolution)
