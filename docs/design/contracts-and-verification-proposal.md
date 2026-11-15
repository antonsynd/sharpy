# Contracts and Tiered Verification — Proposal

**Issue:** none yet (file one when the proposal is ruled on)
**Status:** Proposal — not committed. Nothing here is implemented; every spelling is provisional.
**Related:** [feature-lifecycle.md](feature-lifecycle.md), [unittest.md](../language_specification/unittest.md), [assert_statement.md](../language_specification/assert_statement.md), [decorators.md](../language_specification/decorators.md)

---

Function contracts as a language feature, checked by a ladder of backends that share one set of
annotations: runtime checks, generated property tests, an SMT solver, and (optionally) Lean export.
The aim is to let people and LLMs writing Sharpy state what a function must do and learn quickly,
with a concrete input, when it does not.

## Motivation

When an LLM writes the implementation, the reviewer's scarce resource is attention. A contract is a
much smaller thing to review than a body, and a machine can check the body against it. The feedback
that helps most is a counterexample, not a failed proof:

```python
@pure
@requires(n >= 0)
@ensures(lambda r: r * r <= n and n < (r + 1) * (r + 1))
def isqrt(n: int) -> int:
    lo, hi = 0, n + 1
    while hi - lo > 1:
        invariant lo * lo <= n and n < hi * hi
        mid = (lo + hi) // 2
        if mid * mid <= n:
            lo = mid
        else:
            hi = mid
    return lo
```

This looks right and is wrong. Runtime `+ - *` on `int` wraps
([arithmetic_operators.md](../language_specification/arithmetic_operators.md)), so at
`n = 2147483647` the initializer `n + 1` is negative and the loop never runs. A checker that models
Sharpy's real integer semantics reports that input; a reviewer reading the code usually does not.

## Non-goals

- **Not a proof assistant.** No tactic language, no dependent types in the surface language.
- **Not whole-program verification.** Verification covers an explicit subset; everything else keeps
  runtime checks and tests.
- **Not a second assertion mechanism.** `assert` stays the one way to state a mid-body fact.
- **Not a build dependency.** A normal `sharpyc` build never needs a solver or Lean installed.

## Design

### Annotations

| Form | Host | Meaning |
|---|---|---|
| `@requires(expr)` | function, method | Precondition over the parameters. Repeatable; conjunction. |
| `@ensures(lambda r: expr)` | function, method | Postcondition over the parameters and the return value. |
| `@pure` | function, method | No writes to non-local state, no I/O, calls only `@pure` code. Compiler-checked. |
| `assert expr` | statement | Unchanged at runtime; a proof obligation under verification. |
| `invariant expr` | first statements of a loop body | Loop invariant. Contextual identifier, special only there. |
| `decreases expr` | after the invariants | Termination measure. Contextual identifier. |

These are compiler-recognized decorators in the same family as `@test` and `@override`: metadata,
not function-wrapping decorators. Contract expressions are ordinary Sharpy expressions, type-checked
as `bool` in the function's parameter scope, and must themselves be `@pure`.

The postcondition names the return value with an explicit binder rather than a contextual `result`
keyword, for the reason property observers take an explicit parameter
([property-observers-proposal.md](property-observers-proposal.md), the anti-magic anti-pattern).

Contracts are inherited: an `@override` may weaken `@requires` and strengthen `@ensures`, never the
reverse. Interface members may carry contracts; implementations are held to them.

### The checker ladder

The same annotations mean the same thing on every rung; a higher rung only checks more inputs.

| Rung | Checks | Covers | Cost |
|---|---|---|---|
| 1. Runtime | The inputs that actually occur | The whole language | None beyond the check |
| 2. Property tests | Many generated inputs satisfying `@requires` | The whole language | Test time |
| 3. SMT | All inputs, automatically | The verifiable subset | Seconds; may answer *unknown* |
| 4. Lean | All inputs, with a written proof | The pure, total subset | Proof effort |

**Rung 1: runtime.** `@requires` lowers to a check at function entry, `@ensures` to a check at each
return, `invariant` to a check per iteration. This is a lowering decision recorded in `SemanticInfo`;
`RoslynEmitter` only translates the resulting IR (Critical Rule 2).

**Rung 2: property tests.** `sharpyc test` derives a test per contracted function: generate
arguments from the parameter types, discard those failing `@requires`, run, check `@ensures`, shrink
any failure to a minimal input. No new user-facing syntax is needed for the common case. It lowers
to xUnit like `@test` does.

**Rung 3: SMT.** `sharpyc verify` generates verification conditions for functions in the verifiable
subset and discharges them with an SMT solver. Three outcomes, each a diagnostic:

- *proved*: silent (or an Info-range note under `--verbose`).
- *refuted*: an error carrying the counterexample as concrete argument values, plus the obligation
  that failed and its source span.
- *unknown* (timeout or outside the solver's theories): a warning. The runtime check stays in.

A refutation can be written out as an ordinary `@test` so the failure is pinned by a test that needs
no solver to run.

**Rung 4: Lean.** `sharpyc emit lean` translates verified-subset functions to Lean definitions and
their contracts to theorem statements with the proofs left open. A person or an LLM writes the
proof; Lean's kernel checks it. This is for obligations rung 3 answers *unknown* on, typically ones
needing induction or a lemma. It is the last rung because it is the only one that asks for work
beyond the annotations.

### Verifiable subset

Rungs 3 and 4 accept a function only when it stays inside a subset the checker models exactly:

- `@pure`, non-`async`, non-generator.
- Fixed-width integers (modelled as bit-vectors, so wraparound is visible), `bool`, immutable
  structs, tagged unions, `Optional`/`Result`, tuples, `match`, `if`, `while`, `for` over ranges.
- Calls to other contracted `@pure` functions, reasoned about through their contracts only.

Out at first: floats, strings beyond equality and length, mutable collections, classes with mutable
fields, exceptions as control flow, and .NET interop. An interop call is admitted only through a
declared contract, which is then trusted, not checked.

Leaving the subset is a diagnostic naming the construct, never a silent downgrade to rung 1.

### Erasure and the .NET boundary

A proved `@ensures`, `assert` or `invariant` may be erased from the emitted C#. A `@requires` on a
`@public` function is never erased: C# callers are not verified, and Axiom 1 puts .NET consumers
first. Internally, a call site whose `@requires` is proved may call an unchecked entry point.

### Pipeline placement

`.spy → Lexer → Parser → Semantic → ValidationPipeline → Lowering → RoslynEmitter`

- **Parser:** `invariant` / `decreases` as contextual identifiers; contract decorators parse as
  decorators already do.
- **Semantic:** type-check contract expressions; record them node-keyed in `SemanticInfo` (joining
  `MergeFrom`); compute purity.
- **Validation:** purity violations, contract variance on overrides, subset membership. New
  diagnostics take Validation-range codes, each with a `DiagnosticExplanations` entry.
- **Lowering:** runtime checks, and erasure where a proof result is supplied.
- **Verification:** a separate consumer of the lowered IR that emits SMT-LIB text
  (`sharpyc emit smt`) and runs a solver as a child process. Emitting text rather than linking a
  solver keeps the compiler free of the dependency, makes verification conditions snapshot-testable,
  and leaves the solver replaceable.

The feature ships experimental behind a `contracts` flag per
[feature-lifecycle.md](feature-lifecycle.md), with ungated use reporting SPY0331.

## What is trusted

| Rung | Trusted |
|---|---|
| Runtime, property tests | The compiler, as for any Sharpy program |
| SMT | The verification-condition generator and the solver |
| Lean | The Sharpy-to-Lean translation and Lean's kernel, not the proof author |

Two failure modes apply on every rung and need their own guards:

- **Vacuous contracts.** `@requires(False)` proves anything, and `@ensures(lambda r: True)` constrains
  nothing. The verifier checks each precondition for satisfiability and reports an unsatisfiable one.
- **Weak contracts.** A contract can be true and still not pin the behaviour. Mutation testing
  (below) measures this.

Both matter more when one LLM writes the contract and the body: the contract is what the person
reviews, so it must be shown to constrain something.

## Companion tooling

Independent of the solver, and each useful alone:

1. **`sharpyc mutate`.** Mutate a function body (flip a comparison, off-by-one a constant, drop a
   statement) and report mutants that no contract or test kills. This is Critical Rule 12 offered
   to users.
2. **`sharpyc check --json`.** Semantic analysis and validation without codegen, with structured
   diagnostics (code, span, counterexample values, suggested fix) for agents to consume.
3. **Executable docstring examples.** Examples in a docstring run as tests, so documentation doubles
   as specification.
4. **Typed holes.** `...` in expression position reports the expected type and the in-scope values
   that fit it.

## Alternatives considered

- **Lean as the primary backend.** A Lean proof is about a Lean model of the code, so it needs a
  faithful formal semantics of Sharpy before it is worth anything, and every obligation needs a
  written proof. It gives no counterexamples. Kept as rung 4.
- **Refinement types** (`type Nat = int where self >= 0`). More compositional, but a second way to
  state what `@requires` states, and a much larger type-system change. Revisit once contracts have
  usage data.
- **Compiling to Dafny.** Dafny already targets C#, but Sharpy would inherit its semantics for
  integers and collections rather than modelling its own, which breaks the counterexample story
  above.
- **Contracts as ordinary library decorators.** Cannot reference parameters in the decorator
  argument, cannot be erased, and are invisible to a checker.

## Staging

Each stage ships alone and none changes what users wrote in an earlier one.

1. Contract decorators, `@pure` checking, runtime lowering (experimental flag).
2. Derived property tests with shrinking; refutation-to-`@test`.
3. `sharpyc emit smt` and `sharpyc verify` for the integer/bool/struct/union subset.
4. `sharpyc mutate` and precondition-satisfiability checks.
5. `sharpyc emit lean`.

## Open questions

1. **Postcondition binder.** `@ensures(lambda r: ...)` (explicit, proposed) or a contextual `result`
   (shorter, magic).
2. **Exception type for a failed runtime contract.** `AssertionError` matches `assert`; .NET callers
   expect `ArgumentException` for a violated precondition.
3. **`invariant` spelling.** A contextual statement (proposed) or a recognised leading `assert` in
   the loop body.
4. **Pre-state in postconditions.** `old(expr)` is unnecessary while only `@pure` functions verify;
   needed as soon as methods on mutable types do.
5. **Release builds.** Whether unproved `@ensures`/`invariant` checks stay in by default, tied to
   the unimplemented `-O` analogue in [assert_statement.md](../language_specification/assert_statement.md).
6. **Generators for user types** on rung 2: derived from constructors, or user-supplied.
7. **Which solver is the reference** for CI, and the default timeout.
