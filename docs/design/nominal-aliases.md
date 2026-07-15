# Nominal ("distinct") Type Aliases — Design Note

> **Status:** Design note — 2026-07-15
> **Issue:** [#1020](https://github.com/antonsynd/sharpy/issues/1020) (Evaluate-backlog disposition:
> *gate-candidate, follow-up plan* — needs a short design note on the interaction with today's
> transparent aliases before implementation, per the #1047 triage).
> **Relates to:** [type_aliases.md](../language_specification/type_aliases.md), the free-union note
> ([free-unions.md](free-unions.md)), and the feature lifecycle
> ([feature-lifecycle.md](feature-lifecycle.md)).
> This is a **short design note**, not a full plan — its job is to settle distinct-alias semantics
> against today's transparent `ExpandTypeAlias` behavior, pick a codegen strategy, and fix the
> conversion rules, so a follow-up `/create-plan` is unblockable.

## 1. Current state (verified)

- **Aliases are transparent (inline-expanded).** `type UserId = int` is resolved by
  `TypeResolver.ExpandTypeAlias` (`TypeResolver.cs:517`, called from the alias-resolution path at
  `:149`), which **erases the alias name** and substitutes the aliased type. `type_aliases.md` documents
  the implementation as "🔄 Lowered — Inline expansion at use sites; `using` directive where possible."
- **Consequence:** `UserId`, `Height = int`, and raw `int` all interchange freely today — there is no
  domain distinction. That is the gap #1020 closes.
- **Python precedent (verified, Python 3.12):** `typing.NewType('UserId', int)` is the exact model —
  `UserId(42)` is checker-distinct from `int` but **runtime-erased**: `type(UserId(42)).__name__ == 'int'`.
  NewType is a zero-cost identity function, distinct only to the type checker. This is the semantics #1020
  asks for, spelled `distinct`.

## 2. Semantics — distinct is nominal in the checker, erased in codegen

**Decision: keep transparent aliases as the default; add an opt-in `distinct` form that is nominal to
the type checker and erased in codegen.** Two alias kinds coexist:

| Form | Assignability | Codegen |
|------|---------------|---------|
| `type UserId = int` (today) | transparent — `UserId` ≡ `int`, interchangeable | erased (inline-expanded) |
| `type UserId = distinct int` (new) | **nominal** — `UserId` incompatible with `int`, `Height`, and sibling distinct aliases | **erased** (identical C# to using `int`) |

This is a pure **Axiom 3 (Type Safety)** win delivered at **zero runtime cost**, which is precisely why
it satisfies Axiom 1 for free: the generated C# is byte-identical to using the underlying primitive; the
distinctness exists only during type checking and vanishes before emission.

Distinctness rules (following Cerun, and matching the "explicit over magic" stance):

- **Entering the domain is explicit:** `UserId(x)` constructor-style conversion from the underlying type.
- **Leaving the domain is explicit:** `int(uid)` converts back to the underlying primitive.
- **Sibling domains never mix:** `let h: Height = uid` where `Height = distinct int` and
  `UserId = distinct int` is a **compile error** — same underlying type, different domains.
- **Arithmetic leaves the domain:** `uid + 1` is a plain `int` (the operation is on the underlying type);
  re-wrap with `UserId(...)` to re-enter. This avoids inventing per-domain operator overloads (which would
  be the "each feature must earn its complexity" anti-pattern) and matches Cerun's rule.

## 3. Codegen strategy — erased-with-checks, **not** a wrapper struct

The issue offers two strategies and its non-goals settle the choice:

- **`readonly record struct` wrapper newtype** — gives runtime identity but costs an allocation/boxing
  surface and diverges the generated C# from the primitive. The issue explicitly lists wrapper-struct
  newtypes as a **non-goal** ("rejected — would violate zero-cost"). Also an interop wart: a `.NET` API
  taking `int` could not receive a `UserId` wrapper without unwrapping.
- **Erased-with-checks (chosen).** Codegen emits the **underlying primitive directly** — `UserId`
  disappears exactly as a transparent alias does today. Distinctness is enforced *only* in the type
  checker; by the time the emitter runs, a `UserId` value *is* an `int`. `UserId(x)` and `int(uid)` are
  compile-time identity conversions that emit **no** runtime code (no cast, no wrapper construction). The
  acceptance test is an `.expected.cs` snapshot proving the generated C# for a `distinct`-alias program is
  identical to the same program written with the raw primitive.

This keeps the emitter a pure translator (Critical Rule 2): the distinctness decision is made and consumed
entirely in semantic analysis, and the emitter sees only the underlying type.

## 4. How this lands in the pipeline

- **Parser:** `distinct <type>` as the alias RHS. `distinct` is a **contextual identifier** recognized
  only in alias-RHS position (no new global keyword — it costs nothing reserved). The alias node gains an
  `IsDistinct` flag. Parser-scoped experimental flag `nominal_aliases`; parser builds the node
  unconditionally, the gate rejects ungated use with `SPY0331`.
- **Semantic — the core change: a distinct alias is *not* expanded.** Where `TypeResolver.cs:149` today
  routes a `TypeAliasSymbol` into `ExpandTypeAlias` (erasing the name), a **distinct** alias must resolve
  to a *nominal* type that carries both its name/identity and its underlying type. Two viable shapes:
  1. a new `DistinctAliasType : SemanticType` wrapping `UnderlyingType` + the `TypeAliasSymbol`, with
     `IsAssignableTo` by symbol identity (mirroring how `UnionType` compares by name) and `ClrType` /
     display delegating to the underlying — cleanest, keeps the nominal type first-class through checking;
  2. a `TypeAliasSymbol.IsDistinct` flag that suppresses `ExpandTypeAlias` and makes the alias symbol its
     own nominal type. Option 1 is preferred — it localizes the distinctness to one `SemanticType` node
     and keeps `ExpandTypeAlias` untouched for transparent aliases.
  - Assignment/argument passing across sibling distinct domains is an error; `UserId(x)`/`int(uid)`
    conversions are checker-recognized identity conversions; binary arithmetic on a distinct alias yields
    the underlying type (§2).
- **Codegen:** the `DistinctAliasType` maps to its `UnderlyingType`'s C# type; conversions emit nothing
  (§3). No emitter branch beyond "unwrap to underlying," which is exactly what transparent aliases already
  do.
- **Diagnostics:** a new "sibling distinct domains are not interchangeable" error (`SPY02xx`, semantic
  band — verify the next free code at implementation) with its `DiagnosticExplanations` entry in the same
  commit (the `AllDiagnosticCodes_HaveExplanations` gate). Ungated use → `SPY0331`.
- **Fixtures:** `.spy`/`.error` for cross-domain rejection and for arithmetic re-entry; an `.expected.cs`
  snapshot proving zero-overhead erasure (identical C# to the raw primitive); dual
  `nominal_aliases_{gated,ungated}` pattern.

## 5. Scope boundaries

- **In v1:** distinct aliases over any type (primitive or otherwise), explicit `Domain(x)` / `underlying(x)`
  conversions, sibling-domain rejection, arithmetic-leaves-the-domain.
- **Deferred (stretch, issue's own list):** generic **relation bounds** `T extends Age` / `T super Age`
  that walk the alias chain. These need the distinct-alias type to participate in generic constraint
  solving and should be a second increment once the base nominal semantics prove out.
- **Non-goal (from the issue):** wrapper-struct newtypes with distinct runtime identity — rejected (§3),
  would violate zero-cost.

**Recommendation:** proceed to a follow-up `/create-plan`. The design is small and axiom-clean —
erased-with-checks is a pure Axiom 3 win at zero Axiom 1 cost, the transparent-alias default is
untouched, and Python's `NewType` is a direct precedent for the runtime-erased/checker-distinct model.
Sequence base nominal semantics (parse `distinct`, `DistinctAliasType`, sibling rejection, erased
codegen) as the shippable experimental increment; defer relation bounds. It pairs naturally with the
free-union work ([free-unions.md](free-unions.md)) since both extend the type checker's assignability
rules. Issue stays **open**.
