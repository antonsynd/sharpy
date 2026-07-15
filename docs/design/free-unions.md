# Free Union Types (`X | Y`) — Design Note

> **Status:** Design note — 2026-07-15
> **Issue:** [#992](https://github.com/antonsynd/sharpy/issues/992) (Evaluate-backlog disposition:
> *gate-candidate, follow-up plan*, per the #1047 triage — the plan must first resolve how free unions
> relate to the existing `UnionType` placeholder and to `T | None` nullable interop).
> **Relates to:** [nullable_types.md](../language_specification/nullable_types.md),
> [typing_equivalences.md](../language_specification/typing_equivalences.md), the nominal-alias note
> ([nominal-aliases.md](nominal-aliases.md)), and the feature lifecycle
> ([feature-lifecycle.md](feature-lifecycle.md)).
> This is a **short design note**, not a full plan — its job is to make the follow-up `/create-plan`
> unblockable by settling the three reconciliation questions the triage named: `union`-decl
> relationship, `T | None` interop, and the SPY0113 retirement path — plus the erased-vs-reified
> representation direction.

## 1. Current state (verified)

- **Free unions are rejected at parse time.** `Parser.Types.cs:22-39` throws `SPY0113`
  (`DiagnosticCodes.Parser.FreeUnionNotSupported`, marked *Active*) on any `T | X` where `X` is not
  `None`: *"Free unions like 'int | str' are not supported. Use 'union' declarations for custom sum
  types."*
- **`T | None` is a nullability modifier, not a union.** Per `nullable_types.md`, `T | None` is "the
  **only** valid inline union syntax" and is "semantically a nullability modifier (like C# `?`), not a
  general union constructor." In the parser it sets `IsCSharpNullable = true`; it maps to C# nullable
  reference types. This is distinct from `T?`, which desugars to `Optional[T]` (a safe tagged union).
- **Named unions already exist and are nominal.** `union Foo:` parses via `ParseUnionDef`
  (`Parser.Definitions.cs:905`) into a `UnionType` (`SemanticType.cs:883`) carrying a `Name`, a
  `TypeSymbol`, and `CaseTypes`. Its `IsAssignableTo` compares **by name** — two unions are compatible
  only if they share a name. So `union` declarations are *nominal* tagged unions. (`UnionType` is more
  than the "v0.2.x placeholder" the issue describes — it has real case-type and assignability logic.)
- **Python semantics (verified, Python 3.12):** `int | str` is a first-class union object (PEP 604) with
  `get_args → (int, str)`; `isinstance(5, int | str)` is `True` at runtime; `int | str | None`
  annotations are accepted.

## 2. The three reconciliations

### 2.1 `X | Y` vs the named `union` declaration — structural vs nominal

**Decision: anonymous `X | Y` is a *structural* union; named `union Foo:` stays *nominal*.** They are
the union analogue of the tuple split Sharpy already makes: `tuple[int, str]` is structural (any two with
the same element types are the same type), while a named record is nominal. Concretely:

- `int | str` and another `int | str` written elsewhere denote the **same** type, canonicalized by their
  **unordered set of case types** (so `int | str` ≡ `str | int`). No declaration required.
- `union Foo:` with cases `{int, str}` remains a **distinct** type from anonymous `int | str` — its
  `IsAssignableTo`-by-name identity is unchanged. A named union is *not* merely sugar-with-a-name over an
  anonymous one; it can carry methods, a discriminant name, and nominal identity that anonymous unions
  cannot.
- This means `X | Y` is **not** "an anonymous `union` declaration." It is a separate, structural
  construct that reuses the `UnionType` *representation* (a set of `CaseTypes`) with an empty/synthetic
  `Name` and structural — not nominal — assignability. The follow-up plan adds a structural-assignability
  path to `UnionType` (or a sibling `StructuralUnionType`) rather than forcing anonymous unions through
  the nominal name check.

Rationale (Axiom-ordered): C# 9.0 has no anonymous unions (Axiom 1), so *some* generated carrier is
unavoidable; making the anonymous form structural keeps it ergonomic (Axiom 2, matches PEP 604) without
weakening the nominal `union`'s type-safety guarantees (Axiom 3).

### 2.2 `T | None` interop must not change meaning

**Decision: `| None` stays special-cased as the nullability modifier; a free union is orthogonal to
nullability.** When free unions land, `T | None` continues to mean *nullable `T`* (C# nullable, the
current behavior) — it does **not** silently reinterpret as "a two-case union with a `None` variant."
For a union that includes `None`:

- `int | str | None` parses as *the structural union `int | str`, made nullable* — i.e. `(int | str)?`
  at the nullability layer, not a three-case union `{int, str, None}`. The `None`-ness is the nullability
  modifier applied to the whole union.
- This preserves the existing `None`-literal disambiguation (see
  [none_literal_semantics](../language_specification/none_literal.md) and the design decision that bare
  `None` = null for a nullable target, while `None()` is a union `None` *variant*). A structural union
  never introduces a `None` *case*; it can only be *made nullable*. If a genuine `None` case is wanted,
  that is a named `union` with an explicit `None` variant — which is exactly the nominal/structural split
  in §2.1.

The upshot: the parser's `| None` handling (`Parser.Types.cs:22-55`, `IsCSharpNullable`) is untouched;
free-union parsing is a *new* branch that fires for `T | X` where `X ≠ None`, and `None` in a longer
pipe chain is peeled off as the nullability layer around the structural union of the rest.

### 2.3 SPY0113 retirement path

**Decision: SPY0113 is not deleted — it is superseded by the feature gate, then retired at graduation.**
The lifecycle-clean path:

1. **Today:** ungated `int | str` → `SPY0113` (hard "not supported").
2. **Experimental:** free unions ship behind a `free_unions` flag (Parser scope — it changes how a type
   annotation parses). The parser **always builds the AST** for `X | Y` (per the lifecycle's
   "parser builds unconditionally" rule); the gate, not the parser, decides. Ungated use becomes
   **`SPY0331`** (feature-not-enabled, naming `free_unions` and how to enable it) instead of `SPY0113`.
   `SPY0113`'s parse-time throw at `Parser.Types.cs:28-39` is replaced by building the free-union
   annotation node and registering the construct in `GatedConstructRegistry.All`.
3. **Graduated (later):** when the feature graduates, the gate is removed and `int | str` is always
   accepted; `SPY0113` is dropped from `DiagnosticCodes` (and its `DiagnosticExplanations` entry with
   it). Dual fixtures (`free_unions_gated` / `free_unions_ungated` asserting `SPY0331`) guard both paths
   while experimental.

## 3. Representation — erased vs reified

The issue lists three candidate representations; the axioms decide the direction:

| Option | Type safety (Axiom 3) | .NET interop (Axiom 1) | Cost |
|--------|:---:|:---:|------|
| `object` boxing + runtime `is` checks | ✗ (erased) | ✓ trivial | loses static typing — **rejected as the type's representation** |
| Reified tagged-union `readonly struct` (one per canonical case-set) | ✓ | ✓ zero-alloc, but codegen-per-union | mirrors `Result`/`Optional` |
| C# discriminated unions (proposed C# feature) | ✓ | ✓ | speculative — not available |

**Direction: reified as a generated `readonly struct` tagged union, canonicalized by case-set, staged.**
This mirrors how `Result`/`Optional` already lower — a zero-allocation struct with a discriminant and a
typed payload per case — so it is a known, .NET-idiomatic pattern (Axiom 1) that keeps each case
statically typed (Axiom 3). The generated type is interned by the *canonical unordered case-set*, so
`int | str` used in ten places generates one struct.

Because full tagged-union value semantics are a large codegen job, stage it (issue scope note #3):

- **Stage 1 — annotation-only, checked structurally.** `X | Y` is accepted in annotations and drives
  **overload resolution**, **`isinstance`/`match` narrowing**, and assignability (`T` is assignable to
  `X | Y` iff `T` matches a case). The value representation at the interop boundary is the least-upper-
  bound / `object`, with `match`/`isinstance` doing the runtime discrimination — the *static* type is the
  structural union even though the *storage* is not yet a bespoke struct. This unblocks the common
  ergonomic wins (union-typed parameters, `match value: case int(n): … case str(s): …`) at modest codegen
  cost.
- **Stage 2 — reified struct.** Generate the interned `readonly struct` carrier for zero-boxing storage
  and `.NET`-facing round-trips, with `.expected.cs` snapshots pinning the generated shape.

## 4. What the follow-up `/create-plan` inherits

- **Parser:** replace the `Parser.Types.cs:22-39` `SPY0113` throw with a free-union annotation node built
  unconditionally; peel trailing `| None` as the nullability layer (§2.2); register `free_unions` in
  `KnownFeatures` (Parser scope) and its gated construct in `GatedConstructRegistry.All`.
- **Semantic:** structural `UnionType` (or sibling) with case-set canonicalization and structural
  `IsAssignableTo`; leave nominal named-union assignability intact; feed unions into overload resolution
  and the existing `match`/`isinstance` narrowing (`NarrowingFlowAnalysis`).
- **Codegen:** Stage 1 discrimination via `match`; Stage 2 interned `readonly struct` carrier.
- **Diagnostics:** `SPY0331` gated twin; retire `SPY0113` only at graduation. Every new code gets a
  `DiagnosticExplanations` entry (the `AllDiagnosticCodes_HaveExplanations` gate).
- **Fixtures:** `free_unions_{gated,ungated}` dual pattern; `match`/overload/narrowing `.expected`
  fixtures; Stage-2 `.expected.cs` snapshots.

**Recommendation:** proceed to a follow-up `/create-plan` — the reconciliations above make free unions
implementable without disturbing `T | None` interop or the nominal `union`. Sequence **Stage 1**
(annotation + narrowing + overloads) as the shippable experimental increment; treat **Stage 2** (reified
struct) as a second increment gated on demand. Priority is a P2 type-system item; it pairs naturally with
the nominal-alias work ([nominal-aliases.md](nominal-aliases.md)) since both extend the type-checker's
notion of assignability. Issue stays **open**.
