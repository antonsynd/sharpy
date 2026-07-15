# Placement-Based Decorators — Design

> **Status:** Design — 2026-07-15
> **Issue:** [#1027](https://github.com/antonsynd/sharpy/issues/1027) (Evaluate-backlog disposition:
> *gate-candidate, design-doc first*, per the #1047 triage).
> **Depends on:** the lowering IR ([lowering-ir.md](lowering-ir.md), E1/#1055) — a placement decorator
> is specifiable as an IR function transform, and this design assumes that vocabulary.
> **Relates to:** the experimental feature lifecycle ([feature-lifecycle.md](feature-lifecycle.md)),
> the existing decorator surface ([decorators.md](../language_specification/decorators.md)), and the
> known-decorator registry (`Semantic/Validation/DecoratorValidator.cs`).
> This page is a **design**, not policy and not code. Its deliverable is a go/no-go recommendation for
> scheduling a follow-up `/create-plan`, not an implementation.

## 1. The idea and why it is on the backlog

Sharpy already has decorators, but they are almost all **metadata flags** — `@virtual`, `@override`,
`@final`, `@staticmethod`, `@dataclass`, `@lru_cache` — resolved by name against a closed registry and
lowered to C# modifiers, attributes, or generated members. There is deliberately **no general
Python-style "call the decorator, get back a wrapped callable" mechanism**: that would mean a runtime
closure per decorated function, a delegate allocation, and an extra call frame, none of which sit well
with Axiom 1 (.NET-first, zero-overhead where possible) or Axiom 3 (type safety — a `Callable` wrapper
erases the wrapped signature).

**Placement decorators** (prior art: the Cerun language, `§ Decorators`) are a middle path. A decorator
declares *where* its body runs relative to the target — `prolog` (before), `epilog` (after), or
`wrapper` (around) — and the compiler **statically lowers** the decorator body into the target function
instead of wrapping it at runtime. The result is a single function with the decorator logic inlined: no
delegate, no extra frame, and the arg/result "packs" the decorator inspects are typed, so the contract
is checkable at compile time.

```python
@decorator(placement=epilog, result=value)
def clamp_exit(value: int) -> int:
    if value > 5:
        return 5
    return value

@clamp_exit
def compute() -> int:
    return 9        # compute() now returns 5, with no wrapper call frame
```

This is worth designing because it is the *only* form of "transforming decorator" that does not fight
the axioms: it buys the log-before / clamp-after / retry-around patterns without the runtime-wrapper
cost that made Sharpy decline general Python decorators in the first place.

## 2. Surface syntax

A placement decorator is an ordinary function whose **decorator-ness** is declared by a `placement`
argument on a `@decorator(...)` marker, plus named bindings for the packs it wants to see:

| Placement | Marker | Bindings it may name | Runs |
|-----------|--------|----------------------|------|
| `prolog`  | `@decorator(placement=prolog, positional=args, named=kwargs)` | the target's argument packs | before the body |
| `epilog`  | `@decorator(placement=epilog, result=value)` | the target's return value | after the body |
| `wrapper` | `@decorator(placement=wrapper, call=invoke)` | a binding that invokes the body | around the body; decides *whether* to invoke |

Applying one is the existing `@name` syntax on a `def`:

```python
@decorator(placement=prolog, positional=args)
def log_entry(args: tuple) -> None:
    print(f"called with {args}")

@log_entry
def compute(x: int, y: int) -> int:
    return x + y
```

**Naming and gating.** `placement`, `prolog`, `epilog`, and `wrapper` are **contextual identifiers**,
special only inside a `@decorator(...)` marker — they are not new keywords (anti-pattern: "add a keyword
because Python has it"; they cost nothing as reserved words). The whole surface ships **experimental**
behind a `placement_decorators` flag (Parser scope, since it changes how a `@decorator` marker parses),
following the lifecycle doc's entry criteria: a `SPY0331` ungated `.error` twin plus a gated `.features`
fixture.

## 3. Lowering rules — placement decorators are IR function transforms

This is where the lowering IR (E1) makes the feature specifiable rather than hand-waved. In the IR, a
function is a bound, typed, immutable node (lowering-ir.md §2). A placement decorator is a **pure
IR-function-to-IR-function transform** applied during lowering, before the emitter runs — exactly the
"structural transform produced before emission" role the IR was built for (lowering-ir.md §1.4). The
emitter never sees the decorator; it sees one already-transformed function node and does its usual
mechanical node→syntax mapping.

Let `T` be the target function's IR node, with parameter symbols `p₁…pₙ`, return type `R`, and body
`B` (a list of IR statements). For each decorator `D` in application order (bottom-up, matching the
existing decorator ordering in decorators.md):

- **`prolog`** → prepend `D`'s lowered body to `T`'s. The `positional`/`named` bindings are bound to IR
  expressions that materialize the argument packs: `positional` is an `IrTuple` of `p₁…pₙ`; `named` is
  the keyword subset. A prolog that reassigns a binding forwards modified inputs (Cerun's `.with(...)`);
  v1 may restrict prologs to *read-only* packs and defer forwarding (see open questions).
  Result IR: `T' = T with Body = D.Body ++ B`.
- **`epilog`** → rewrite each `return e` in `B` to bind `value := e`, run `D`'s body, and return `D`'s
  chosen value. Because the IR is immutable and `return` sites are explicit nodes, this is a structural
  rewrite of the return terminators, not a textual one. Result IR: every `IrReturn(e)` becomes
  `IrBlock[ value := e ; D.Body ; IrReturn(valueOut) ]`.
- **`wrapper`** → the strongest form. `D`'s body becomes the new body; the `call=` binding lowers to an
  inlined invocation of the *original* `B` (as a local IR sub-block, not a delegate). A `wrapper` that
  never mentions `call=` produces a function that never runs its target body — a **diagnostic warning**
  (see §5), because silently dropping the body is exactly the "magic behavior" anti-pattern.

Composition of multiple decorators is IR-transform composition in application order, so `@A @B def f`
is `A(B(f))` at the IR level — identical ordering to runtime decorators, but resolved at compile time.
Each transform preserves types by construction (Axiom 3): a `prolog` cannot change `R`; an `epilog`'s
`result` binding is typed `R` and its returned value must be assignable to `R`; a `wrapper`'s body must
return `R`. These are ordinary type-checker obligations on the lowered IR, not new inference.

## 4. Interaction with the known-decorator registry and runtime decorators

Today every `@name` is validated against a closed set. `DecoratorValidator` builds
`AllKnownDecorators` (`Semantic/Validation/DecoratorValidator.cs:223-238`) from the modifier, attribute,
and test decorator name sets plus `@dataclass`/`@lru_cache`/etc.; **any `@decorator` not in that set is
rejected with `SPY0444`** (`DiagnosticCodes.Semantic.UnknownDecorator`). Placement decorators change this
contract in a bounded way:

- A **user-defined placement decorator** is a `def` the user wrote, so its name is *not* a compiler
  built-in and must not go through `AllKnownDecorators`. Instead, the resolver must recognize that the
  applied `@name` binds to a `FunctionSymbol` carrying a `placement=` marker, and admit it as a
  *user placement decorator* rather than emitting `SPY0444`. Concretely: `DecoratorValidator`'s
  unknown-name check grows one branch — "resolves to a placement-decorator function symbol" — before it
  falls through to `SPY0444`. This keeps the closed registry closed for *built-ins* while opening a
  single, explicit, symbol-resolved door for placement decorators. No open-world "any name is a
  decorator" regression.
- The `@decorator(placement=…)` **marker itself** is a new recognized built-in on the *definition* side
  (it marks a `def` as a placement decorator), so it does join a registry — but as a declaration
  attribute, checked for well-formed `placement`/binding arguments, not as an application-site name.
- **Coexistence, not replacement.** Metadata decorators (`@virtual`, `@final`, …) are untouched: they
  remain modifier/attribute lowerings and never become placement transforms. A definition may carry both
  a metadata decorator and a placement decorator; ordering follows decorators.md (metadata flags are
  order-insensitive; placement transforms compose in application order). There is deliberately **no**
  interaction with a general runtime-wrapper decorator mechanism, because Sharpy has none — placement
  decorators are the compile-time answer to the same need, not a layer over a runtime one.

## 5. Diagnostics

- **`wrapper` drops the body.** A `wrapper` placement whose lowered body never invokes its `call=`
  binding compiles, but emits a warning: the target body is unreachable by construction. This mirrors
  Cerun's behavior and the control-flow validator's existing unreachable-code family. (Warning, not
  error: intentionally-skip is a legitimate — if rare — use, e.g. a feature-flag gate.)
- **Signature obligations** (`epilog` result assignable to `R`, `wrapper` returns `R`, `prolog` cannot
  change arity) are type-checker errors on the lowered IR, reusing existing assignability diagnostics
  rather than minting a family — with the caveat that each *new* code still needs a
  `DiagnosticExplanations` entry (the `AllDiagnosticCodes_HaveExplanations` gate).
- **Ungated use** is `SPY0331` via the standard feature gate.

## 6. Open questions

1. **Pack representation and forwarding.** How do the `positional`/`named` packs map onto Sharpy's typed
   parameters — a synthesized `tuple`/`dict`, or typed accessors? Does v1 allow a `prolog` to *rewrite*
   inputs (Cerun's `args.with(...)`), or is forwarding deferred? Read-only packs are the conservative v1.
2. **Generic and overloaded targets.** Applying a placement decorator to a generic `def[T]` or an
   overload set: does the transform run per-instantiation / per-overload? The IR-transform framing
   suggests "per lowered function node," which is per-overload — needs confirming against overload
   resolution.
3. **Interaction with `async`/generators.** An `epilog` on an `async def` must run after the awaited
   body; a `wrapper` around a generator must decide whether `call=` yields lazily. Likely restrict v1 to
   plain functions.
4. **Decorator arguments.** The roadmap's "Decorator Arguments (Cross-Cutting Concern)" note overlaps —
   parameterized placement decorators (`@retry(times=3)`) need the argument-passing story resolved first.
5. **Debuggability / `#line`.** Inlined decorator bodies must keep source spans pointing at the decorator
   definition, not the target — the IR is source-spanned (lowering-ir.md §2), so this is achievable, but
   the mapping needs a fixture.

## 7. Recommendation

**Conditional go — schedule the follow-up plan only on demand signal; the design is ready when it
appears.** Placement decorators are the one transforming-decorator model that is *consistent* with the
axioms rather than in tension with them: static lowering means zero delegate/frame overhead (Axiom 1),
typed packs mean a checkable contract (Axiom 3), and explicit `placement=`/`call=` bindings avoid the
magic-behavior and "wrapper types for Pythonic API" anti-patterns. The lowering IR now gives the feature
a precise home — each placement is a small IR function transform — which removes the main reason it was
"design-doc first": the lowering was previously unspecifiable, and it no longer is.

That said, its priority stays **below the type-system items** (free unions #992, nominal aliases #1020,
typing guards #995), exactly as the issue itself concludes. The justification bar is "profiling or user
feedback shows runtime-decorator wrapping — or hand-rolled wrapper functions — is a real cost or clarity
problem." Absent that signal, adding the surface risks the "feature creep / each feature must earn its
complexity" anti-pattern and the "multiple ways to do the same thing" tension with existing helper
functions. Concretely: **land nothing now; when a demand signal arrives, a `/create-plan` starts from
§3's lowering rules and §6's open questions, piloting `prolog`/`epilog` first (mechanical IR transforms)
and treating `wrapper` (which needs the `call=` inlining and the drop-body diagnostic) as a second
increment.**
