# Contracts and Tiered Verification — Proposal

**Issue:** [#2701](https://github.com/antonsynd/sharpy/issues/2701) (parked)
**Status:** Proposal — not committed, not scheduled. Nothing here is implemented; every spelling is provisional.
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
    @invariant(lo * lo <= n and n < hi * hi)
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if mid * mid <= n:
            lo = mid
        else:
            hi = mid
    return lo
```

This looks right and is wrong in both the body and the contract. Runtime `+ - *` on `int` wraps
([arithmetic_operators.md](../language_specification/arithmetic_operators.md)), which gives four
separate defects (figures from simulating 32-bit wraparound):

| Input | What goes wrong |
|---|---|
| from `n = 46340` | `hi * hi` wraps, so the invariant is false on entry although the state is fine. |
| from `n = 92681` | `mid * mid` wraps negative and the search goes the wrong way: `isqrt(92681)` returns 65536, not 304. |
| `n = 92681` | The postcondition *accepts* that wrong answer, because `r * r` wraps to 0. |
| `n >= 2147395600` | `(r + 1) * (r + 1)` wraps, so the postcondition rejects the correct answer 46340. |

At `n = 2147483647` the initializer `n + 1` is also negative, the loop never runs, and the function
returns 0. The repair is to do the arithmetic in `long`, in the contract as well as the body.

A checker that models Sharpy's real integer semantics reports the smallest of these inputs. A
reviewer reading the code usually finds none of them, and the third row is the important one: a
contract can be wrong too, and only a checker that evaluates it under the real semantics shows it.

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
| `@pure` | function, method | No writes to non-local state, no I/O, calls only pure code. Compiler-checked. |
| `assert expr` | statement | Unchanged at runtime; a proof obligation under verification. |
| `@invariant(expr)` | `while` statement | Loop invariant: holds every time control reaches the loop condition. |
| `@decreases(expr)` | `while` statement | Termination measure: non-negative, strictly smaller after each iteration. |

These are compiler-recognized decorators in the same family as `@test` and `@override`: metadata,
not function-wrapping decorators. Contract expressions are ordinary Sharpy expressions and must
themselves be pure. Their typing rules:

- `@requires(expr)`: `expr` is `bool`, in the function's parameter scope.
- `@ensures(lambda r: expr)`: a one-parameter lambda. The decorator is the inference context
  ([lambdas.md](../language_specification/lambdas.md)): `r` has the function's declared return type,
  and the body is `bool` in the parameter scope plus `r`. Refused on a function returning `None`,
  which has no value to bind (see open question 4).
- `@invariant(expr)` / `@decreases(expr)`: `bool` and an integer type respectively, in the scope
  enclosing the loop.

**Loop annotations are decorators on the `while` statement, not statements in its body.** A bare
`invariant expr` statement would collide with programs that already have a variable of that name:
`invariant (x)`, `invariant - x` and `invariant[x]` are all valid expression statements today. `@`
cannot begin a statement, so a decorator before `while` is unambiguous, and its position matches its
meaning, which is a fact about the loop head. This makes `while` a new decorator host, accepted only
for these two decorators.

`@invariant` means one thing on every rung: the expression is true each time control reaches the
loop condition, which is before the first iteration, after every iteration, and on the evaluation
that exits. A loop that runs zero times still checks it once. A `break` leaves without re-checking.

**Purity of code the compiler cannot see.** `@pure` may call:

- Sharpy functions marked `@pure` (spy-sourced stdlib modules use the decorator directly).
- Core/Stdlib C# members carrying `System.Diagnostics.Contracts.PureAttribute`, read through
  `ClrTypeBridge`. The attribute is trusted, not checked.
- Other .NET members carrying the same attribute, on the same terms.

Anything unannotated is impure. Annotating Core is therefore a prerequisite of stage 1, starting
with the arithmetic builtins and the read-only members of the collection types.

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
| 2. Property tests | Many generated inputs satisfying `@requires` | `@pure` functions | Test time |
| 3. SMT | All inputs, automatically | The verifiable subset | Seconds; may answer *unknown* |
| 4. Lean | All inputs, with a written proof | The pure, total subset | Proof effort |

**Rung 1: runtime.** `@requires` lowers to a check at function entry, `@ensures` to a check at each
return, `@invariant` to a check before each evaluation of the loop condition. This is a lowering
decision recorded in `SemanticInfo`; `RoslynEmitter` only translates the resulting IR (Critical
Rule 2). An operation that raises (`ZeroDivisionError`, `OverflowError`) propagates as it does today.

**Rung 2: property tests.** For each contracted **`@pure`** function the compiler derives a test:
generate arguments from the parameter types, discard those failing `@requires`, run, check
`@ensures`, shrink any failure to a minimal input. Impure functions get no derived test, since
calling one hundreds of times with generated arguments would repeat its side effects. A shrunk
failure can be written out as an ordinary `@test` to pin it.

Derived tests are emitted as xUnit tests through the existing `@test` lowering and run by
`dotnet test` ([unittest.md](../language_specification/unittest.md)); there is no new command.

Rejection sampling fails on narrow preconditions such as `n == k * k`. A derived test that cannot
find enough inputs satisfying `@requires` within its budget **fails as inconclusive**; it never
passes on zero cases.

**Rung 3: SMT.** `sharpyc verify` generates verification conditions for functions in the verifiable
subset and discharges them with an SMT solver. Three outcomes, each a diagnostic:

- *proved*: silent (or an Info-range note under `--verbose`).
- *refuted*: an error carrying the counterexample as concrete argument values, plus the obligation
  that failed and its source span.
- *unknown* (timeout or outside the solver's theories): a warning. The runtime check stays in.

A refutation can be written out as an ordinary `@test`, extending the rung 2 mechanism, so the
failure is pinned by a test that needs no solver to run.

*Proved* means three things: every `@ensures`, `assert` and `@invariant` holds; **no operation
raises**; and, where `@decreases` is given, the loop terminates. Each operation that can raise
inside the subset carries its own obligation: a non-zero divisor for `//` and `%`, no
`int.MinValue // -1`, an in-range narrowing conversion, a present value when an `Optional` is
unwrapped. An explicit `raise` is outside the subset.

**Rung 4: Lean.** `sharpyc emit lean` translates verified-subset functions to Lean definitions and
their contracts to theorem statements with the proofs left open. A person or an LLM writes the
proof; Lean's kernel checks it. This is for obligations rung 3 answers *unknown* on, typically ones
needing induction or a lemma. It is the last rung because it is the only one that asks for work
beyond the annotations.

### Verifiable subset

Rungs 3 and 4 accept a function only when it stays inside a subset the checker models exactly:

- `@pure`, non-`async`, non-generator.
- Fixed-width integers (modelled as bit-vectors, so wraparound is visible), `bool`, immutable
  structs, tagged unions, `Optional`/`Result`, tuples, `match`, `if`, `while`.
- Calls to other contracted `@pure` Sharpy functions, reasoned about through their contracts only.
- Calls to a short list of Core builtins the verifier models directly (`abs`, `min`, `max` and the
  width conversions to begin with). Any other pure Core member is opaque: the solver knows only that
  equal arguments give equal results.

Out at first: floats, strings beyond equality and length, mutable collections, classes with mutable
fields, explicit `raise` and `try`, `for` loops (open question 3), and .NET interop beyond the
opaque treatment above.

Leaving the subset is a diagnostic naming the construct, never a silent downgrade to rung 1.

### Erasure and the .NET boundary

Erasure is the last stage and is off by default: until then every check stays in the emitted C#
whether or not it was proved.

When enabled, a proved `@ensures`, `assert` or `@invariant` may be erased. A `@requires` on a
`@public` function is never erased: C# callers are not verified, and Axiom 1 puts .NET consumers
first. Internally, a call site whose `@requires` is proved may call an unchecked entry point.

### Pipeline placement

`.spy → Lexer → Parser → Semantic → ValidationPipeline → Lowering → RoslynEmitter`

- **Parser:** `while` accepts a decorator list; contract decorators on functions parse as decorators
  already do. No new keywords or contextual identifiers.
- **Semantic:** type-check contract expressions; record them node-keyed in `SemanticInfo` (joining
  `MergeFrom`); compute purity.
- **Validation:** purity violations, contract variance on overrides, subset membership. New
  diagnostics take Validation-range codes, each with a `DiagnosticExplanations` entry.
- **Lowering:** every contract becomes a tagged check node in the IR. Lowering never consults a
  proof result, so it has no dependency on verification.
- **Verification** (`sharpyc verify`, never part of a build): reads the lowered IR, treats each
  tagged check as an obligation rather than a throw path, emits SMT-LIB text (`sharpyc emit smt`) and
  runs a solver as a child process. It writes a results file keyed by a hash of the function's IR.
  Emitting text rather than linking a solver keeps the compiler free of the dependency, makes
  verification conditions snapshot-testable, and leaves the solver replaceable.
- **Erasure** (opt-in, last stage): a separate IR-to-IR pass after Lowering that drops tagged checks
  named in a results file whose hash still matches. A stale or missing file erases nothing.

The order is Lowering, then (out of band) Verification, then Erasure, then the emitter. Nothing
feeds back into an earlier pass.

The feature ships experimental behind a `contracts` flag per
[feature-lifecycle.md](feature-lifecycle.md), with ungated use reporting SPY0331.

## What is trusted

| Rung | Trusted |
|---|---|
| Runtime, property tests | The compiler, as for any Sharpy program |
| All rungs | `PureAttribute` on Core/Stdlib and other .NET members |
| SMT | The verification-condition generator, the solver, and the built-in models of Core builtins |
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

1. `PureAttribute` on Core/Stdlib; contract decorators, `@pure` checking and runtime lowering
   (experimental flag).
2. Derived property tests for `@pure` functions, with shrinking; a shrunk failure written out as a
   `@test`.
3. `sharpyc emit smt` and `sharpyc verify` for the integer/bool/struct/union subset; solver
   counterexamples written out as `@test` the same way.
4. `sharpyc mutate` and precondition-satisfiability checks.
5. `sharpyc emit lean`.
6. Opt-in erasure of proved checks.

## Open questions

1. **Postcondition binder.** `@ensures(lambda r: ...)` (explicit, proposed) or a contextual `result`
   (shorter, magic).
2. **Exception type for a failed runtime contract.** `AssertionError` matches `assert`; .NET callers
   expect `ArgumentException` for a violated precondition.
3. **`for` loops.** `@invariant` is defined on `while` only. A `for` over a range needs a rule for
   an invariant that mentions the loop variable, which is unbound at the loop head before the first
   iteration.
4. **Pre-state in postconditions.** `old(expr)` is unnecessary while only `@pure` functions verify;
   needed as soon as methods on mutable types do, and it is what would give a `None`-returning
   function something to state in `@ensures`.
5. **Release builds.** Whether unproved `@ensures`/`@invariant` checks stay in by default, tied to
   the unimplemented `-O` analogue in [assert_statement.md](../language_specification/assert_statement.md).
6. **Generators on rung 2**, for user types (derived from constructors, or user-supplied) and for
   narrow preconditions that rejection sampling cannot satisfy.
7. **Which solver is the reference** for CI, and the default timeout.
8. **Decorators on statements.** `while` becomes a decorator host for two decorators only. Whether
   that is acceptable, or a recognised leading `assert` in the loop body is preferred despite reusing
   `assert` for a second meaning.
9. **Purity by attribute.** Whether trusting `PureAttribute` on Core is acceptable, or Core purity
   should be derived (for example by a checked allowlist test over the Core surface).
