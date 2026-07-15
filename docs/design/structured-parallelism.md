# Structured Parallelism Surface — Design

> **Status:** Design — 2026-07-15
> **Issue:** [#1026](https://github.com/antonsynd/sharpy/issues/1026) (Evaluate-backlog disposition:
> *gate-candidate, design-doc first*, per the #1047 triage).
> **Depends on:** the await-aware control-flow graph
> (`Analysis/ControlFlow/ControlFlowGraphBuilder.cs`) and `TaskType` (`Semantic/SemanticType.cs`) for
> the safety analysis and the async interaction; the lowering IR ([lowering-ir.md](lowering-ir.md)) as
> the eventual home for the lowered `Parallel.For`/PLINQ shapes.
> **Relates to:** [async_programming.md](../language_specification/async_programming.md) and the
> experimental feature lifecycle ([feature-lifecycle.md](feature-lifecycle.md)).
> This page is a **design**, not policy and not code. Its deliverable is a go/no-go recommendation for
> scheduling a follow-up `/create-plan`.

## 1. The idea and why it is a *bigger bet*

.NET already ships the execution engine: `Parallel.For`/`Parallel.ForEach` with `ParallelOptions`,
PLINQ (`AsParallel().Select().Where()`), `Parallel.Invoke`, and the TPL. So a structured-parallelism
surface for Sharpy is **not** new concurrency machinery — it is a *Pythonic syntax plus a safety layer*
over runtime that exists. Prior art is the Cerun language (`§ Threading and Parallelism`): a `parallel:`
counted-loop / `section:` block form and a lazy parallel pipeline view.

```python
parallel:
    for i in range(0, 4):
        work(i)

parallel(threads=2, schedule="dynamic", chunk=32):
    for i in range(0, 4):
        work(i)

result = parallel_view(xs).map(f).filter(p).with_workers(4).collect()
```

It is a *bigger bet* — hence design-doc-first — for one reason: **the value is entirely in the safety
story, and the safety story is the hard part.** Wrapping `Parallel.For` is trivial; deciding *which loop
bodies are safe to run in parallel* is a real static analysis, and getting it wrong turns a
data race into silently wrong output. Axiom 1's "explicit over magic" stance pushes hard here: Sharpy
should not *claim* to prove safety it cannot, so the central design decision below is how much to promise.

## 2. How each form maps to .NET

| Sharpy surface | .NET lowering |
|----------------|---------------|
| `parallel:` over a counted `for` | `Parallel.For` / `Parallel.ForEach` |
| `parallel(threads=n, schedule=…, chunk=…):` | the same, with a `ParallelOptions { MaxDegreeOfParallelism = n }` and a custom `Partitioner` for `chunk` |
| `parallel: section: … section: …` | `Parallel.Invoke(a, b, …)` / `Task` fan-out |
| `parallel_view(xs).map(f).filter(p).collect()` | `xs.AsParallel().Select(f').Where(p').ToList()` |
| `.with_workers(n)` | `.WithDegreeOfParallelism(n)` |

The mappings are mechanical and, once lowered, are ordinary IR → emitter output (the lowered
`Parallel.For` call or PLINQ chain is just more IR the emitter maps to `SyntaxFactory` — no new emitter
decisions, consistent with Critical Rule 2). The design weight is *not* here.

## 3. The safety model — the actual design decision

The question is: **when may the compiler parallelize a loop body?** Three postures, in increasing order
of promise and cost:

1. **Explicit opt-in, user owns safety (recommended v1).** `parallel:` is a directive: the user asserts
   the body is safe to run concurrently, and the compiler lowers it faithfully. The compiler still runs a
   **conservative refusal analysis** — it *rejects* (does not silently serialize) bodies it can see are
   unsafe — but it does not *prove* safety for the bodies it accepts. This matches Axiom 1's
   explicit-over-magic stance and PLINQ's own contract, where the user is trusted not to introduce races.
2. **Prove-or-serialize (Cerun's model).** The compiler attempts a no-shared-write / purity proof; if it
   fails, it silently falls back to sequential. Rejected for v1: *silent* fallback is exactly the magic
   behavior the anti-pattern table warns against — the user asks for parallelism and sometimes gets none,
   with no signal. A *diagnostic* fallback (warn, then serialize) is a viable middle option.
3. **Prove-or-reject (strongest).** Refuse to compile any `parallel:` body not provably race-free. Sound
   but demands a real effect system Sharpy does not have; deferred.

**Recommended posture: (1) with the refusal analysis of (3)'s cheap fragment.** The compiler accepts
`parallel:` on the user's assertion but *rejects at compile time* the loop shapes it can prove are unsafe
by a syntactic/dataflow check that needs no new type system:

### 3.1 Loop-carried-dependency detection, on the CFG

The refusal analysis leans on the existing control-flow graph. `ControlFlowGraphBuilder` already builds
a per-function CFG and, notably, is **await-aware** — `AddStatement` sets `BasicBlock.ContainsAwait` when
a statement contains an `AwaitExpression` (`ControlFlowGraphBuilder.cs:239-255` via
`ContainsAwaitExpression`). A parallel-loop analysis adds a small dataflow over that CFG for the loop body
`B` with loop variable `i`:

- **Loop-carried write → read.** If `B` writes a variable/collection element read on a *different*
  iteration (an index expression not keyed solely on `i`; a write to an outer-scope variable later read),
  the iterations are ordered — **reject** (`SPY05xx`, a new "loop body is not parallelizable" error).
  Reduction patterns (`total += f(i)`) are the common case and are the first refinement: recognize the
  `+=`/`*=` accumulator shape and either reject with a "use a parallel reduction" hint or (later) lower to
  a thread-local-then-combine reduction.
- **Outer mutable writes mixed with results.** Cerun's exact caveat: a body that both computes a result
  *and* writes outer state forces the sequential path. In posture (1) this is a **warning + reject**
  rather than a silent serialize.
- **`await` inside `parallel:`** → the CFG's `ContainsAwait` flag makes this cheap to detect. Mixing
  structured parallelism with `await` in the same body is rejected in v1 (see §4) — the two concurrency
  models compose badly and the interaction needs its own design.
- **Side-effecting calls.** A call whose target is not provably pure is *not* by itself a rejection under
  posture (1) (that would make the feature nearly unusable without an effect system); it is the user's
  asserted responsibility. The analysis rejects only the *structural* hazards above, which it can see
  without interprocedural purity.

This is deliberately a **refutation**, not a proof: it catches the mistakes it can see cheaply and trusts
the user otherwise. That honesty is the point — the compiler never claims a safety guarantee it cannot
back.

## 4. Interaction with `async`/`await` and `TaskType`

Sharpy's async surface is more mature than the feature's absence suggests: `await` tokens and
`ParseAwaitExpression` exist, the CFG is await-aware, and `TaskType` (`Semantic/SemanticType.cs:918`)
carries real assignability (`Task<T>` → `Task`, `Task<T>` → `Task<U>` when `T`→`U`). Structured
parallelism must define its relationship to this, not ignore it:

- **`parallel:` is not `async`.** `parallel:` is CPU-bound fan-out over `Parallel.For`/PLINQ (threads);
  `async`/`await` is cooperative I/O suspension (`Task`). They are different tools. A `parallel:` block is
  **synchronous** — it blocks until all iterations complete — and its lowering produces no `TaskType`.
- **v1 forbids `await` inside `parallel:`.** Detected via `BasicBlock.ContainsAwait`. A body that awaits
  wants task-based concurrency, not data-parallel fan-out; forcing them together invites deadlocks
  (blocking on `Parallel.For` from an async context) and is rejected with a diagnostic steering the user
  to `Task.WhenAll` / an async pipeline.
- **Cancellation.** `ParallelOptions.CancellationToken` is the natural seam; a later increment threads an
  ambient cancellation token, consistent with how the compiler already carries a `CancellationToken`
  through long-running analysis.
- **`parallel_view` is lazy but synchronous.** Its terminal (`.collect()`/`.sum()`) blocks; it is PLINQ,
  not `IAsyncEnumerable`. An async parallel pipeline is explicitly out of v1 scope.

## 5. Memory-model caveats

Even with the refusal analysis, the user-owns-safety posture inherits .NET's memory model, and the doc
must say so plainly:

- **No atomicity or ordering guarantees** across iterations beyond what the user writes with `Interlocked`
  / `lock`. `total += f(i)` across threads is a race unless lowered to a reduction; the analysis rejects
  the naive shape rather than emitting a racy `+=`.
- **Captured closures share state.** A `parallel:` body capturing an outer `list` and calling `.append()`
  races on `Sharpy.List<T>` (which is not thread-safe). The refusal analysis flags outer-collection
  mutation; anything it cannot see is the user's asserted responsibility, and the docs must state that the
  Sharpy collection types are not concurrency-safe.
- **Exceptions** from parallel bodies surface as `AggregateException` from the TPL. The lowering must
  decide whether to unwrap to Sharpy's exception model or expose the aggregate — a Python program expects
  the first exception, so unwrapping the first inner exception is the Pythonic choice (Axiom 2), at some
  fidelity cost.
- **Determinism.** Parallel execution reorders side effects. The compiler's own determinism contract (A3)
  is about *compilation* being deterministic, not *execution*; a `parallel:` program is intentionally
  non-deterministic in effect order, and that must be documented as expected, not a regression.

## 6. Surface decision: statement vs stdlib

The issue's own recommendation — **start with the no-new-syntax stdlib layer** (`parallel_view` /
`parallel_map` over PLINQ) before committing to a `parallel:` statement — is sound and this design
endorses it:

- A stdlib `parallel_view(xs).map(f).filter(p).collect()` and a `parallel_map(f, xs)` need **zero parser
  or lexer work**: they are ordinary generic functions lowering to `AsParallel()` chains. They validate
  demand and exercise the memory-model caveats in real code before any syntax is minted.
- The `parallel:` **statement** (counted loop, `section:` blocks, `threads=`/`schedule=`/`chunk=`
  overrides) is the bigger commitment — new Parser-scoped syntax, the CFG refusal analysis, new
  diagnostics — and should follow only if the stdlib layer shows the abstraction earns its keep and the
  safety story holds. This is the "each feature must earn its complexity" anti-pattern applied
  deliberately: prove demand at the cheap layer first.

## 7. Recommendation

**Staged go: build the stdlib layer first as the experiment; defer the `parallel:` statement behind
it.** The .NET mapping is free, so the entire risk is the safety model — and the honest posture
(user-owns-safety, compiler *refuses* the structural hazards it can cheaply see on the await-aware CFG,
never silently serializes) is both implementable without a new type system and consistent with Axiom 1's
explicit-over-magic rule. Concretely:

1. **First increment (validate demand):** a stdlib `parallel_view` / `parallel_map` over PLINQ — no new
   syntax, no gate needed beyond normal stdlib review — plus this document's memory-model caveats written
   into its module docs. Measure real use.
2. **Second increment (only on signal):** the `parallel:` / `section:` statement behind a
   `structured_parallelism` flag (Parser scope, experimental lifecycle), with the §3.1 loop-carried
   dependency refusal analysis and the §4 async-interaction rejections. New `SPY05xx` diagnostics each get
   a `DiagnosticExplanations` entry (the `AllDiagnosticCodes_HaveExplanations` gate).

Priority sits with the type-system backlog items ahead of it; a `/create-plan` for increment 1 is
schedulable now if a parallelism demand signal exists, and starts from §2 (mapping) and §5 (caveats).
Increment 2's plan starts from §3.1 and §4. The issue stays **open** (advanced, not closed).
