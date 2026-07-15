# Experimental Feature Lifecycle

> **Status:** Policy — 2026-07-10
> **Issue:** [#1047](https://github.com/antonsynd/sharpy/issues/1047) (C4).
> **Implements:** the `FeatureFlags` infrastructure (#1044, C1) + gated-syntax diagnostics (#1045, C2).
> **Source of truth for the mechanism:** `src/Sharpy.Compiler/Shared/FeatureFlags.cs`.
> This page is the *policy*; that file is the *code*. Where they disagree, the code wins and this page is stale — fix it.

An **experimental feature** is a language or codegen change that ships in a release **disabled by
default**, behind a named flag, so that it can be exercised, revised, and either promoted or removed
without a compatibility promise while it is being evaluated. This document defines how such a feature
is proposed, gated, tested, and eventually retired.

The goal is asymmetric by design: **adding** an experimental feature is deliberately structured, but
**deleting** one is cheap by construction — the gate is the only thing standing between the syntax and
users, so removing the gate and the syntax together leaves nothing behind.

## States

A feature moves through exactly one of these lifecycles. There is no "on by default but still called
experimental" state — once a flag is a no-op, the feature is graduated.

```
                          ┌── graduated ── (flag becomes a no-op, then the flag is removed)
proposed ── experimental ─┤
                          └── deleted ──── (syntax + gate removed; nothing to clean up)
```

| State | Default | Gate | Stability promise | Registry presence |
|-------|---------|------|-------------------|-------------------|
| **experimental** | off | required | none — may change or vanish without notice | in `KnownFeatures` |
| **graduated** | on | none (flag is a no-op, then removed) | normal back-compat | removed after a deprecation window |
| **deleted** | n/a | n/a | n/a | removed |

### experimental

The feature is registered in `FeatureFlags.KnownFeatures` and its gated constructs are rejected unless
the flag is enabled. Because it is off by default, **its syntax and semantics carry no stability
promise**: they may change, or the feature may be removed entirely, in any release. Users who opt in
accept that. This is what makes the evaluation cheap — no code depending on the default behaviour can
break, because the default behaviour is "not available."

### graduated

When a feature has earned its place (see [exit criteria](#exit-criteria)), it graduates in two steps so
that no user's build breaks at the transition:

1. **Flag becomes a no-op.** The gate is removed; the construct is always accepted. The feature name
   stays in `KnownFeatures` so that existing `--enable-feature`, `<Features>`, and
   `from __future__ import` sites keep compiling instead of erroring on an now-unknown name. The
   `FeatureInfo` is marked so tooling can note it is a no-op.
2. **Flag is removed.** After at least one release as a no-op, the name is dropped from
   `KnownFeatures`. Enabling it is once again an error — but now it is a *stale* reference to a feature
   that is simply the language, and the error text tells the user to delete the flag.

At graduation the feature is documented as normal language surface, not as experimental, and its
`.spy`/`.expected` fixtures drop their `.features` sidecars (the feature is now unconditional).

### deleted

If evaluation concludes against the feature, both the gated syntax **and** the gate are removed in one
change, and the name is dropped from `KnownFeatures`. Enabling a deleted feature is an error listing the
currently-known features. Because everything lived behind the gate, deletion touches only the feature's
own code — there is no default-path behaviour to unwind. Record the disposition on the feature's issue.

## Entry criteria (proposed → experimental)

A feature may land as experimental when all of the following hold:

- It has a GitHub issue describing the syntax, semantics, and motivation.
- It is registered in `FeatureFlags.KnownFeatures` with a `FeatureInfo(Name, Description, Scope, Hidden)`
  whose `Scope` is chosen per [Scope asymmetry](#scope-asymmetry) below.
- Its gated constructs are registered in the feature-gate registry (see
  [How a feature is gated](#how-a-feature-is-gated)) so that ungated use produces `SPY0331`, not a
  generic parse or type error.
- It has at least one ungated `.error` fixture asserting the `SPY0331` diagnostic and at least one gated
  fixture (via a `.features` sidecar, C3/#1046) exercising the enabled behaviour.

## Exit criteria (experimental → graduated)

A feature may graduate only when all of the following hold:

- **Full fixture coverage** of the enabled behaviour via `.features` sidecars (C3, #1046) — the gated
  path is tested to the same standard as unconditional language surface, so that flipping the flag to a
  no-op is not a leap of faith.
- **A specification page is drafted** under `docs/language_specification/` *before* graduation, so the
  feature becomes authoritative spec (Critical Rule 7) the moment it becomes default.
- **At least one release spent in experimental**, giving real usage a chance to surface problems while
  changes are still free.

A feature that cannot meet these does not graduate; it either stays experimental or is deleted.

## Scope asymmetry

How a feature *can be enabled* is determined by **when it takes effect**, encoded as its
`FeatureScope`. This asymmetry is not a preference — it falls out of the pass order (import resolution
is Pass 1.5, *after* parsing), and it is enforced in `FeatureFlags.cs` and `ImportResolver`.

| Scope | Takes effect during | Enable via `--enable-feature` / `.spyproj <Features>` | Enable via `from __future__ import` |
|-------|---------------------|:---:|:---:|
| `Parser` | lexing / parsing | ✅ | ❌ |
| `Semantic` | semantic analysis | ✅ | ✅ |
| `CodeGen` | code generation | ✅ | ✅ |

A **parser-scoped** feature changes how source text is tokenized or parsed. A `from __future__ import`
statement can never enable it: by the time Pass 1.5 sees the import, the syntax it would have unlocked
has already been rejected by the parser. Parser-scoped features are therefore enabled only
**compilation-wide** — the CLI `--enable-feature=<name>` flag or a `<Features>` entry in the `.spyproj`.

**Semantic-** and **codegen-scoped** features affect only later phases, so they may additionally be
enabled **per file** with `from __future__ import <name>`, in addition to the two compilation-wide
mechanisms. Per-file future flags are stored separately from the compilation-wide set (they must not
leak across files) and unioned in for that file only — see `ImportResolver.GetFileFutureFeatures`.

All pilots below are `Parser`-scoped, so all are enabled compilation-wide only.

## Unknown names are errors, never silent no-ops

At every boundary — CLI, `.spyproj`, and `from __future__ import` — an unknown feature name is a hard
error, never a silently-ignored no-op. This is a deliberate safety property: a typo in a flag name must
not quietly disable the feature the author thought they had turned on.

- `--enable-feature` / `.spyproj <Features>` validate against `KnownFeatures` at the boundary
  (`FeatureFlags.TryValidate`) and reject unknown names with the list of known features.
- `from __future__ import <name>` of an unknown **or mis-scoped** (parser-scoped) name is
  **`SPY0330`** (`UnknownFutureFeature`), which points parser-scoped names at the compilation-wide flags.

## How a feature is gated

Gating is a single post-import-resolution concern, not per-parser or per-checker special cases. The
parser **always** builds the AST for gated syntax — it never guards syntax behind a flag itself — so a
disabled feature yields a helpful "requires experimental feature" diagnostic instead of a generic parse
error.

1. **Register the name.** Add a `FeatureInfo` to `FeatureFlags.KnownFeatures` with the right `Scope`.
2. **Register the gated constructs.** Map the feature's AST constructs (a node kind, an operator, a
   keyword) to the feature name in `GatedConstructRegistry.All` (`Semantic/GatedConstruct.cs`), the
   central registry consumed by the `FeatureGateChecker` (both landed under C2/#1045; the matmul and
   defer pilot entries there are the reference examples).
3. **Enforcement is one AST walk.** After Pass 1.5 (so per-file `from __future__` flags are known) and
   before type resolution, the `FeatureGateChecker` walks each module, unions the compilation-wide flags
   with that file's future flags, and emits **`SPY0331`** (`FeatureNotEnabled`) for every ungated use of
   a gated construct. Downstream passes therefore never see an ungated construct without the error
   already reported. Parser-scoped features are validated here too — by design they cannot arrive via a
   future import.

The `SPY0331` message names the feature and how to enable it, e.g. *"requires experimental feature 'X' —
enable with `--enable-feature=X` or `<Features>X</Features>` in .spyproj"*, appending *"or
`from __future__ import X`"* for semantic/codegen-scoped features.

## Behavioral (non-syntax) flags

Everything above assumes an experimental feature adds **syntax** — a construct the parser builds and the
gate checker rejects when disabled. A second species of flag gates **behavior, not syntax**: an
optimization or codegen pass that changes *how* a valid program is compiled, never *whether* it parses.
The E3 IR optimization passes **planned** under [#1057](https://github.com/antonsynd/sharpy/issues/1057)
will be the first of these — `opt_const_fold`, `opt_comprehension_fusion`, `opt_devirt`,
`opt_stack_collections` — and the first consumers of `CodeGenContext.Features`. (None are registered yet;
they are named here as the motivating cohort, not as existing flags.)

A behavioral flag differs from a syntax feature in three ways:

- **No gated construct, no `SPY0331`.** There is nothing to reject when the flag is off. "Off" simply
  means *the pass does not run*; the un-optimized program is valid on its own. So a behavioral flag gets
  **no `GatedConstructRegistry` entry**, and the `FeatureGateChecker` has nothing to do for it — the
  "requires experimental feature" diagnostic does not apply.
- **`FeatureScope.CodeGen`.** It takes effect during code generation, so (per [Scope
  asymmetry](#scope-asymmetry)) it is enable-able all three ways, including per file via
  `from __future__ import`. It is still registered in `FeatureFlags.KnownFeatures` — so unknown-name
  validation and the enable mechanisms work — only the *gate* is absent, not the registration.
- **Snapshots, not `.error` fixtures, are the evidence.** Because there is no diagnostic to assert, the
  entry criteria below replace the syntax-feature ones.

### Entry criteria (behavioral)

- **Default-off**, registered in `KnownFeatures` with `FeatureScope.CodeGen`.
- **A `.features`-gated snapshot fixture pinning the optimized output** — the enabled behaviour is proven
  by the generated C# it produces, held to the same standard as any other codegen.
- **The default-path snapshots are unchanged** — flag-off must be byte-identical to before the pass
  existed. This is the behavioral analogue of the ungated `.error` fixture: instead of asserting a
  rejection, it asserts *no change*, which is exactly what "off means don't run the pass" must guarantee.

### Graduation (behavioral)

- **Benchmark evidence, then flip the default.** A behavioral flag graduates only after at least one
  release in experimental *and* benchmark numbers that justify turning it on by default (an optimization
  with no measured win does not graduate — it is deleted). Flip the default on, then follow the standard
  two-step retirement: make the flag a no-op — so existing `--enable-feature` / `<Features>` /
  `from __future__ import` sites keep compiling — then remove the name after a release.
- **Deletion** is unchanged from the syntax case: remove the pass and the flag together. Nothing on the
  default path unwinds, because the default path never depended on the pass.

## Pilot features

Four features pilot this lifecycle. All are `Parser`-scoped and all ship experimental.

| Feature | Flag | Issue | Scope | Lowering |
|---------|------|-------|-------|----------|
| `@` matrix-multiplication operator | `matmul` | [#989](https://github.com/antonsynd/sharpy/issues/989) | `Parser` | dispatches to `__matmul__` / `__imatmul__` |
| `defer` statement | `defer` | [#1023](https://github.com/antonsynd/sharpy/issues/1023) | `Parser` | scope-exit registration lowered to try/finally |
| `as?` / `as!` failable casts | `failable_cast` | [#1029](https://github.com/antonsynd/sharpy/issues/1029) | `Parser` | identical to `to` / `to?` — `(T)value` or `is T … ? Optional<T>.Some(…) : default` |
| `before_set` / `after_set` property observers | `property_observers` | [#416](https://github.com/antonsynd/sharpy/issues/416) | `Parser` | auto-property lowered to a backing field + expanded setter running the observers around every store |

Each pilot registers its name in `KnownFeatures`, registers its gated construct (`@`/`@=`, the
`defer` statement, the `as?`/`as!`-form `TypeCoercion`, and a `PropertyDef` carrying non-empty
`before_set`/`after_set` observers respectively) in the feature-gate registry, and ships with the
dual-fixture pattern: an ungated `.error` fixture asserting `SPY0331` and a gated `.expected` fixture
(via `.features`) exercising the enabled behaviour.

`failable_cast` additionally pilots a **feature-conditional transition hint**: while the flag is
enabled, a legacy `to`/`to?` cast emits `SPY0479` suggesting the `as!`/`as?` spelling (the hint is
suppressed when the flag is off, so it never advises syntax the build would reject). Graduation of
`failable_cast` — making `as?`/`as!` primary and retiring `to` — is deferred graduation work tracked in
[#1096](https://github.com/antonsynd/sharpy/issues/1096).

## Proposing a new feature — checklist

1. Open an issue with syntax, semantics, and motivation.
2. Pick a `FeatureScope` (`Parser` if it changes tokenizing/parsing; otherwise `Semantic`/`CodeGen`).
3. Register the `FeatureInfo` in `FeatureFlags.KnownFeatures`.
4. Build the AST unconditionally in the parser; register the gated constructs in the gate registry.
5. Add the ungated `.error` (SPY0331) fixture and a gated `.features` fixture.
6. Land it disabled-by-default. Iterate freely — experimental carries no stability promise.
7. To **graduate**: meet the [exit criteria](#exit-criteria), draft the spec page, make the flag a
   no-op, then remove the flag after one release. To **delete**: remove the syntax, the gate, and the
   name together, and record the disposition on the issue.
