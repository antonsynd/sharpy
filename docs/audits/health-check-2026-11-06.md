# Project Health Check — Architecture, Verification, Process

> **Date:** 2026-11-06
> **Commit:** `ee9a007` (mainline = dev at the time of the audit), .NET SDK 10.0.403, CPython 3.13 as oracle.
> **Scope:** 284 open issues and 1,385 issues closed since 2026-05-06; all 12 nightly runs and 181 PR/push CI runs since 2026-09-07; `src/` (static read plus live probes); `.claude/`, `build_tools/`, `docs/design`, `docs/audits`; 4,826 non-merge commits over six months; the Mojo 1.2 release review (artifact `TcHMHJ4gJTzq57cVqiomK7`).
> **Method:** five parallel survey agents (issues, CI, compiler architecture, Core/Stdlib + test infrastructure, process/tooling), then the lead verified the highest-impact claims by building the solution and running probe programs. Every number below is measured unless marked **[inferred]**. Prior audits: [2026-06-20](audit-2026-06-20.md), [2026-07-09](strategic-review-2026-07-09.md).

---

## 1. Verdict

Sharpy has an unusually strong verification culture: drain-on-fix ratchets, mutation-tested guards, control runs at the base commit, a CI that proves its own shard partition, a single compile path shared by CLI, LSP and REPL, and SyntaxFactory-only emission. Those are real assets and most projects never get them.

The same culture is now the project's main cost. Over the last three months output shifted from building to pinning: October commits were 42% `test` and 3% `feat`, issue closures fell from 483 (August) to 113 (October) while the open backlog grew to 284, and allowlist rows grew from 608 to 2,047 in three weeks. The process documents respond to each of their own failures with more rule text rather than fewer stages.

Underneath, one architectural fact generates the largest defect class. Lowering decisions are made inline inside a 37k-line `TypeChecker` and spread across **88 node-keyed `SemanticInfo` side-tables** (24 when the lowering IR was designed in July), the IR exists but is not what the emitter reads, nothing freezes `SemanticInfo`, and the emitter still inspects types itself. Every new surface therefore ships with a sibling shape whose fact is missing, which Roslyn reports as a CS error and Sharpy surfaces as SPY0908. About **340 issues** carry SPY0908 or a CS code in the title; **76 are open**; inflow was over 100 a month in August and September and still outpaced closures 45 to 20 in October, after two "class fixes" (#1035, #1146) were closed.

Live probes during this audit also found user-visible correctness bugs that no harness owns: `dict` loses insertion order after a remove and reinsert, the incremental cache ignores feature flags in both directions, `round(x, -2)` throws a .NET exception, and `date(2023, 2, 29)` throws a type that `except ValueError` cannot catch.

The recommended program has three parts, in order: (1) a week of cheap, high-value fixes to the tooling and the live bugs; (2) finishing the lowering IR so the emitter has one input and `SemanticInfo` is frozen, which removes the SPY0908 class structurally instead of cell by cell; (3) a process reset that caps plan size, gates per group, and allowlist growth, and measures throughput every round.

### Scoreboard

| Measure | Value | Source |
|---|---|---|
| Open issues / labeled `class-tracker` | 284 / 130 (46%) | issue survey |
| Issue closures Aug → Sep → Oct | 483 → 305 → 113 | issue survey |
| SPY0908 / CS-leak issues (open / closed in 6 mo) | 76 / 264 | title match |
| Node-keyed `SemanticInfo` tables | 88 (24 in lowering-ir.md, July) | `SemanticInfo.cs` |
| `TypeChecker` | 21 partials, 37.3k lines, ~48 mutable context fields, 60 save/restore sites | grep |
| Allowlist rows 10-15 → 11-06 | 608 → 2,047 | git |
| Spec code blocks that do not compile | 466 of ~1,256 (37%); 459 cite one issue (#1952) | `spec_blocks_allowlist.txt` |
| Test LOC ÷ compiler LOC | 327k ÷ 187k = 1.75 | wc |
| Commit mix, 6 months (4,826 non-merge) | fix 34%, test 28%, feat 13%, docs 9% | git log |
| Commit mix, October | fix 30%, test 42%, feat 3% | git log |
| Hooks that actually run | 1 of 4 | read `.claude/hooks` |
| Nightly runs / red / real compiler finds | 12 / 4 / 1 (the find became a skip) | Actions API |
| PR CI wall clock (median) | 19 min (64 min before sharding); shard 5 alone 15–19 min | Actions API |
| `sharpyc run` hello world, warm | 0.75 s | probe |
| 61-file stdlib project, cold / warm no-change | 4.7 s / 2.0 s (1.5 s is re-emitting IL for 0 changed files) | `--metrics-format text` |

---

## 2. Architecture flaws, ranked by bug-generating potential

Each item names the evidence, the defect class it produces, and a structural remedy. Cell fixes are not listed.

### A1. Lowering is decided inside type checking and spread over 88 side-tables; the IR is not the emitter's input

**Evidence.** `docs/design/lowering-ir.md` §1.2 counted 24 node-keyed dictionaries as "the structure an IR replaces"; `SemanticInfo.cs` now has 88, all `ConcurrentDictionary` on a single-threaded path. `ProjectCompiler.CodeGen.cs:205-212` still says "In E2 Phase 1 nothing reads the IR"; the emitter consults the IR at about 20 sites and `SemanticInfo` everywhere else. The checker makes 282 mutating `SemanticInfo` calls through 75 setters, 72 of them `Set*Lowering`. Validators also write lowering facts (`DefiniteAssignmentValidator.cs:49`, `LoopTransferBindingValidator.cs:134`, `ConstantPositionValidator.cs:766`), so validator `Order` is part of codegen correctness. Within a file the last writer wins (71 indexer writes); at the project merge `TryAdd` keeps the first value and drops a conflict silently. `SemanticInfo` has no `Freeze()`; only symbol-keyed `SemanticBinding` throws on a late write.

**Defect class.** Missing fact for a sibling shape. The fact exists for `x.f` under a call but not under augmented assignment; the emitter falls back and Roslyn rejects the C#. This is the SPY0908 class: 275 SPY0908 mentions in compiler source next to 42 issue numbers, 212 fixtures citing a past leak, 22 `.error` fixtures asserting its absence.

**Remedy.** Finish E2 rather than adding tables: make the lowering pass the only consumer of node-keyed lowering facts and the IR the only emitter input, with each IR node carrying its lowering inline so "fact absent" is unrepresentable. Restrict the checker to types, symbols and diagnostics. Add `SemanticInfo.Freeze()` after the validation pipeline (same contract as `SemanticBinding`), make `MergeFrom` assert disjointness instead of first-wins, and ratchet the table count down from 88 in `SemanticInfoMergeConformanceTests`.

### A2. `TypeChecker` is a god object that threads context through ambient mutable fields

**Evidence.** 21 partials, 37,320 lines (47% comments), about 12 responsibilities (binding, inference, overloads, CLR member resolution with direct reflection, narrowing, store conversion, lowering selection, dataclass synthesis, pattern coverage, class-scope rules, error recovery, running the validation pipeline). About 48 non-readonly fields carry context (`_expectedType`, `_currentCallArguments`, `_typeTestOperand`, `_matchSubjectOperand`, `_indexStoreTarget`, …), needing 60 manual save/restore sites and 17 `finally` blocks. Overload resolution probes candidates and rebinds; only 3 `ClearExpressionType` and 1 `ClearCharMaterialization` retract facts, so writes from a losing candidate survive **[inferred: no general rollback found]**. Largest methods: `CheckMemberAccessCore` ~879 lines, `CheckFunctionCallCore` ~621, `CheckAssignment` ~576. The June audit asked for `TypeChecker.Expressions.Access.Calls.cs` (then 2,891 lines) to be split; it is 7,503.

**Defect class.** Context leakage: state set for one site is seen by a nested expression, or not restored on an early return; speculative writes leak from losing overloads. Both are evaluation-order dependent and show up as matrix cells.

**Remedy.** Replace the ambient fields with an immutable `CheckContext` record passed down the walk (`ctx with { ExpectedType = … }`), deleting the save/restore sites. Split out a `Binder`, keep `TypeChecker` for types and diagnostics, and give `OverloadResolver` a scratch overlay that commits only the winner's facts.

### A3. The emitter still makes type decisions, and the Rule-2 guard cannot see them

**Evidence.** `EmitterCarrierOnlyConformanceTests.cs:98` allow-lists the whole `SemanticType` and `Symbol` hierarchies as "carriers", so type pattern-matching in CodeGen passes; the ratchet is empty for that reason. CodeGen has 73 `GetExpression(Semantic)Type` reads, 51 `is GenericType{Name:…}`-style matches, 36 `UnknownType` checks and 130 `.Name ==` comparisons. Concrete: `ApplyArrayBridge` (`RoslynEmitter.Expressions.Access.Calls.cs:492-511`) re-implements `GenericType.IsAssignableTo` by name; `len` on `str` becomes `.Length` by name (`RoslynEmitter.Expressions.Access.cs:278`); 43 symbol lookups by `Name ==` and 4 disambiguated by `DeclarationLine`.

**Defect class.** Checker/emitter disagreement (admitted by one rule, lowered by another) and overload or identity confusion when two members share a name or line.

**Remedy.** Narrow the carrier set: CodeGen may pass a `SemanticType` to `TypeSyntaxMapper` but may not inspect it. Ban `is GenericType`, `{ Name: … }` and `.TypeArguments` outside `TypeSyntaxMapper.cs` and ratchet the 73 + 51 + 36 sites to zero; each removed site becomes an IR node property. Replace name and line lookups with the existing `SetDeclarationSymbol` maps.

### A4. `UnknownType` means both "error" and "not yet inferred", and is assignable to everything

**Evidence.** `SemanticType.cs:252-258`: `UnknownType.IsAssignableTo(_) => true`. It is the error-recovery type and also the empty-literal placeholder (`RoslynEmitter.Expressions.Access.Calls.cs:505-507`). `GetUnexpectedUnknownExpressions` tests only top-level types, so `list[<?>]` passes; the emitter defends itself at 36 sites with `TypeArguments.All(t => t is not UnknownType)`.

**Defect class.** Silent acceptance: an inference gap becomes "accepted", then `object` or missing type arguments in C#, then a CS error or runtime cast failure; and cascades suppressed at the wrong site.

**Remedy.** Split into `ErrorType(reason)`, constructible only together with a reported diagnostic, and an `InferenceVariable` that must be solved before materialization. Walk nested types for Unknown at `MaterializeCodeGenInfo` in a no-error compilation; then delete the 36 emitter checks.

### A5. Type identity is a string, and three codecs must agree by convention

**Evidence.** `GenericType { string Name; List<SemanticType> TypeArguments; … }` (`SemanticType.cs:311`): identity is `"list"`, the record exposes a mutable `List<>`, and the bridge collapses `List<T>`, `IList<T>`, `IReadOnlyList<T>` and `IEnumerable<T>` into one `list[T]`. 170 name-identity patterns in Semantic and 51 in CodeGen; 332 `BuiltinNames.` references. `ClrTypeBridge` and `CachedModuleDiscovery.ConvertTypeSignature` are two CLR→Sharpy mappers whose agreement is "load-bearing (#1294)", joined by a hand-maintained `SharpyToClrNameMap`; `SymbolSerializer` is a third codec.

**Defect class.** #1933's "name-keyed alias diverging from reflection": user types named like builtins collide, rules for `list` miss CLR `List<T>`, and warm and cold builds decode different identities (schema bumps v27, v28, v37, v41 are this class).

**Remedy.** Key identity on a definition handle (the `TypeSymbol`, or the CLR generic definition), keep `Name` for display, make `TypeArguments` an `ImmutableArray`, collapse the two mappers into one `ClrTypeBridge` path whose codec the serializer reuses, and turn `BuiltinNames.X` type checks into `type.Definition == WellKnown.List` (layer-ladder rung 3 instead of rung 4).

### A6. The incremental cache omits compilation options from its key and re-emits IL on every build (confirmed live)

**Evidence.** `IncrementalCompilationCache.ProjectFactsOf` (`:218-223`) hashes only entry point, output type, root namespace and source root; `Features`, `References`, `PackageReferences`, `TargetFramework` and configuration are absent. Reproduced in this audit with a two-line program and `opt_const_fold`:

| Step | Command | Emitted C# |
|---|---|---|
| A | `--clean --incremental` | `var x = 2 * 3 + 4;` |
| B | `--incremental --enable-feature opt_const_fold` (no source change) | `var x = 2 * 3 + 4;` **(stale)** |
| C | `--clean --incremental --enable-feature opt_const_fold` | `var x = 10;` |
| D | `--incremental` (flag off again) | `var x = 10;` **(stale)** |

#2114 (open) is the same class for a changed `-r` assembly. Separately, `SymbolSerializer.cs` (1,861 lines) plus `SymbolCache.cs` (721) hand-encode symbols at schema v41; nearly every bump comment records a warm≠cold divergence. And a warm no-change build of the 61-file stdlib project reports `Files Compiled: 0` yet spends 1.5 s of its 2.0 s in "IL Emission": the whole assembly is re-emitted when nothing changed.

**Defect class.** Warm≠cold divergence and stale output after an options change; every new semantic fact needs a wire-format change that is silent until the cold/warm differential happens to cover it.

**Remedy.** Fingerprint the effective options (features, reference identities, TFM, configuration, stdlib version) into `ProjectFacts` and add a feature-flag axis and a reference axis to `ColdWarmBuildDifferentialTests`. Skip IL emission when no file and no option changed. Longer term, cache a re-derivable artifact (exported declaration surface) and re-run resolution rather than serializing the symbol graph, or generate the serializer from the records so totality holds by construction.

### A7. CLR reflection leaks into `Semantic/` behind bare catches

**Evidence.** 76 reflection calls in Semantic against 71 in Discovery. `TypeChecker.Utilities.cs:1864-1871, 1893, 1997` wrap `MakeGenericType` in `catch { return null; }`; 27 bare `catch` blocks in the compiler; 30 process-wide static caches keyed by `Type`/`Assembly` (mitigated by `AssemblyVisibility.IsProcessStable` and `ReferenceLoadContext`).

**Defect class.** Reflection answers that differ by call site (Semantic's probe vs Discovery's mapped view) and load failures turned into wrong typing verdicts.

**Remedy.** A conformance scan forbidding `System.Reflection` and `typeof` in `Semantic/` outside a named allowlist, ratcheted to zero; route the rest through a Discovery facade that returns Sharpy facts; replace bare catches with typed results that carry the failure into a diagnostic.

### A8. Diagnostics are free-text at 345 sites, and the SPY0908 net disarms under user errors

**Evidence.** 345 `AddError(` calls, about 288 with inline `$"…"` messages; `code` is an optional parameter (`TypeChecker.Utilities.cs:2867`). `DiagnosticExplanations` documents codes but is not the message source. SPY0400–0449 is full and validation codes span four ranges. `AssemblyCompiler.MapGeneratedCodeDiagnostics` disables the SPY0908 net once any user error exists (#1387), so emitter bugs in broken files reach only a debug log, which matters most for the LSP.

**Remedy.** Typed descriptors (`DiagnosticDescriptor SPY0220 = new(code, format, severity, owner)`) and an `AddError(descriptor, args, node)` API with no free-text overload; generate explanation stubs from descriptors; record owner on the descriptor instead of in the numeric range; count suppressed CS errors in a test-visible channel.

---

## 3. Runtime library (Core and Stdlib)

Items marked **live** were reproduced in this audit with `sharpyc run` against CPython 3.13.

### L1. `dict` loses insertion order after remove-then-insert (live, silent-wrong, untracked)

`Dict<K,V>` is backed by `System.Collections.Generic.Dictionary` (`Dict.cs:26`), which reuses freed slots. `d = {"a":1,"b":2,"c":3}; d.pop("a"); d["d"] = 4; list(d.keys())` prints `['d', 'b', 'c']`; Python prints `['b', 'c', 'd']`. `popitem()` uses LINQ `Last()` (O(n) per call, O(n²) to drain) and returns the slot-last entry. `FrozenDict` already keeps an explicit order array (#1392); `Dict` does not. No fixture deletes then iterates; no issue found. Python has guaranteed this order since 3.7, so this is a P0 fidelity bug. Remedy: a compact insertion-ordered dict (entries array plus index, CPython's layout) pinned by a property test of random insert/remove/reinsert/popitem sequences against the Python model.

### L2. CLR exceptions escape where Python raises `ValueError`, and the tests pin the wrong type (live)

`date(2023, 2, 29)` throws `System.ArgumentOutOfRangeException` from `Datetime.cs:14-17` (the module has zero `throw` statements of its own) and `except ValueError` does not catch it. Numpy throws 33 `ArgumentException`, 5 `InvalidOperationException`, 6 `NotSupportedException`; `ValueError : System.Exception` is unrelated to all of them. Nine spy test files `from system import ArgumentException` and assert the CLR type; `numpy_manipulation_tests.spy:249` asserts that `np.clip(a, 5.0, 2.0)` throws, where numpy returns `[2. 2.]`. Earlier cells were fixed one at a time (#2261, #2263, #2228). Remedy: one class issue with a cell table; a source scan banning `throw new (Argument|InvalidOperation|NotSupported|ArgumentOutOfRange)Exception` on public Stdlib paths, with a twin scan banning `from system import *Exception` in spy tests; a boundary wrapper mapping BCL constructor exceptions to `ValueError`/`OverflowError`.

### L3. The exception hierarchy diverges beyond the tracked `LookupError` cell

`FileNotFoundError`, `PermissionError`, `FileExistsError`, `IsADirectoryError` are not under `OSError` (`Exceptions.cs:274-339`), `OSError : IOError` is inverted, `NotImplementedError` and `UnicodeEncodeError` have the wrong bases. #2124 (open) covers only `IndexError`/`KeyError`. Remedy is what #2124's body asks for: one table-driven test comparing every Core exception's base chain to `python3 -c "print(X.__mro__)"`, then one fix.

### L4. `round()` is an unguarded `Math.Round` wrapper (live)

`round(1234.0, -2)` throws `ArgumentOutOfRangeException: Rounding digits must be between 0 and 15`; Python returns `1200.0`. `round(1234, -2)` is refused at compile time (SPY0354: no overload `(int32, int32)`). `Round(double) => (int)Math.Round(x)` is an unchecked cast, so NaN, ±inf and |x| > 2³¹ wrap instead of raising, contradicting the project's own R-IS ruling (`deviations.yaml` OverflowError row). One fixture covers three values. Remedy: implement CPython's `float.__round__` and a digits × special-values differential matrix.

### L5. The stdlib API-surface check exempts 15 modules with no issue cites

Only 18 of 61 `spy/*.spy` are generated into shipped C#; 43 are declaration stubs. Their only correctness tie is `ApiSurfaceComparisonTests`, whose `KnownMismatchModules` HashSet exempts 15 modules (heapq, random, re, hashlib, functools, statistics, itertools, tempfile, socket, …) with no issue cites and no stale check, and an unmapped module only prints a warning. `check_allowlist_issue_state.sh` cannot see it. Remedy: move the set to an `api-surface-allowlist.txt` with per-method rows, fail on unmapped modules, and rule whether the 43 stubs are a product or debt.

### L6. `netstandard2.1` ships but is never executed; the Python oracle version is unpinned

All six test projects target `net10.0`; the netstandard binaries are compiled and released (`release.yml`) but no test runs them, so BCL-dependent behavior (`ToString("R")` for `str(float)`, `char.IsWhiteSpace` tables, `Dictionary` free-list order) is unverified on Mono/Unity, which is where the dogfood issues (#2182–#2203) come from. Python versions: docs say 3.9+, tools require 3.10/3.11, sweeps require ≥3.12 as a floor, CI pins 3.12, the dev box runs 3.13, and #2350 was measured against 3.13 (`math.fma` is 3.13-only). Remedy: pin one oracle minor in one file read by CI, the `RequireOrSkip` gate and the docs; add a netstandard2.1 smoke job.

### L7. Smaller items

`str.isalnum` uses `char.IsLetterOrDigit` so `'²'`, `'½'`, `'Ⅷ'` are False (Python: True); `sorted` calls the key function per comparison, not per element (#2090, open, already contradicted by `deviations.yaml`); the bottom-up merge sort has no run detection; `Dict.Get` and `defaultdict.Get` return `Optional<V>` while `collection_types.md:41` says `V?` (an owner ruling to record); `Maketrans` returns `Dictionary<char,string>` and Difflib/Struct return raw `List<>` (CLAUDE.md anti-pattern); 13 hand-rolled index clamps bypass the `Index` seam that #2404 called "the single seam"; `List<T>` `<` boxes value-type elements through `Operator.Eq(object, object)` **[inferred]**; `math.expm1`/`log1p` naive outside tiny cutoffs (#2349) and `public static double Pi` is a mutable static; `src/test_discovery.cs` is dead and would not compile; nine hand-written plan files live under the "generated, never hand-edit" `docs/stdlib/`.

---

## 4. The verification system: what it catches and what it costs

### What works (keep)

- **Drain-on-fix ratchets.** 18 of 27 allowlists are at zero; carrier, emitter-annotation, in-process-isolation and warm-fidelity allowlists are all empty. STALE and MOVED detection exists, stale rows fail, issue-state checks run in CI.
- **Behavioral `MergeFrom` guard** (`SemanticInfoMergeConformanceTests.cs:66`, reflection over every field): all 88 tables are covered.
- **Execution-based verification**: control run at BASE, mutation-tested guards, `run` not `emit` for SPY0908. The contract's own ledger credits these with 107 of 149 wrong-result catches.
- **Generative harnesses retiring hand matrices** (interop, metamorphic, differential-exec, name-resolution differential).
- **The dotnet-serialized wrapper**: exit 3 on zero tests ran, exit 4 on watchdog kill, holder probing, per-child staleness, with its own Python tests in CI.
- **PR CI as a regression net**: build once, fan out to 23 jobs, `shard-partition` proves the filters by `--list-tests`, `ci-summary` requires every TRX, median wall clock 19 min.

### Where it is eating itself

**V1. Throughput.** Closures 483 → 305 → 113 (Aug → Oct); 284 open, 257 filed in Sep–Oct; 130 labeled `class-tracker` so the label no longer marks an umbrella; 124 titles say "residue", "sibling", "outlier" or "after P__" (25 open). Median time-to-close for bugs is 1.4 days, mostly batch closes by the filing session, so the median hides the long tail of class trackers. Ruling letters reached R-KN, roughly 250–300 owner rulings since 2026-09-20 **[inferred from lettering]**.

**V2. Allowlists do not trend to empty.** Rows 608 → 2,047 in three weeks. `name-resolution-differential-allowlist.txt` has 1,408 well-formed rows but 787 cite one charter issue (#2423), so they drain only in bulk. `declaration-position-allowlist.txt` had 58 commits in nine days because ~8 generators share one ledger seeded red at base and drained in phases, and because any `#N` in reason text counts as a cite. `spec_blocks_allowlist.txt`: 466 blocks (37% of the spec's fenced code) do not compile and 459 cite #1952, so the issue-state gate is satisfied by keeping one tracker open; rows are sha1-keyed so editing an allowlisted example reddens two rows. `gap-discovery-contracts.md` describes this file out of date.

**V3. Test mass.** 327k lines of Compiler tests against 187k of compiler; 10.3k Compiler test methods; 119 `*Matrix*` files totaling 51.7k lines; 46k lines of sweep/conformance/differential harness; 58 inline `Known*`/`Exempt*` sets forming a second allowlist universe outside the gate. No code coverage tooling exists (no coverlet), so redundancy is unmeasurable. 29 groups of exact-duplicate fixtures (63 files). 74 `test:` commits in three weeks only re-pinned fixture counts or rosters after fixtures were added.

**V4. Guards that passed vacuously, repeatedly.** Python-twin tests passed with no Python 3.12 (#2342); 114 `.warning` sidecars asserted nothing (#1277); spec blocks proved compilation but not output (#2286); a substring grep stood in for architectural enforcement (#1475); the nightly property soak never finished and nobody noticed for two runs (#2392); vstest OOM makes a passing run report "Test Run Failed" with 0 failed rows (#2438, open, seen three times). The pattern is that each guard's SKIP path was not itself guarded.

**V5. The nightly finds little.** 12 runs since 2026-10-25: 8 green, 4 red; 3 reds were the soak breaking itself (#2392); the one real find (`raise None.m` → `throw null.M`, CS0023, seed `c4U0RdI-d-Uw`) was posted to #2365 against the wrong commit and then pinned as a known-red skip. It soaked `dev`, which lagged `mainline` by 118 and 137 commits on two measured nights. The fuzz oracle is crash-only and has never been red. The Core, Stdlib and LSP "property" jobs rerun whole suites (~30 runner-min) for about 15 samples.

---

## 5. Process and tooling footguns, ranked by cost to future sessions

**P1. The plan/verify pipeline is heavier than what it protects.** One group requires reading about 130 KB of procedure (contract 29 KB, CLAUDE.md 18.5 KB, create-plan 18, verify-plan 27, implement-plan 23, verify-implementation 37, gap-discovery-contracts 63). Plans grow to 196 KB over ten `--adequacy` passes. By the contract's own ledger `/verify-plan` returned PASS WITH CORRECTIONS on 53 of 53 first passes and "has never stopped a plan"; 112 of its 132 corrections were stale SHAs or wording. Each group needs at least four whole-solution gates (~30 min each) behind one machine-wide lock; the Stryker pilot held that lock for 7 h 23 min and waiters time out at 75 min. Verify rounds did land 14 real fixes in three weeks, so the execution stages pay; the reading and pinning stages are the bulk.

**P2. CLAUDE.md contradicts its own enforcement.** Its Commands and Testing blocks list raw `dotnet build`/`dotnet test`, which the PreToolUse hook blocks. It says the commit gate is the whole solution; the wrapper header says "GATE ON A PROJECT, NOT ON sharpy.sln (#1384)" because a solution-level run produces a cross-project false red. Its pipeline line omits the Lowering IR stage that `architecture.md` includes. It says `gh` fails TLS; in a cloud session `gh issue view/list` returns HTTP 403 while `gh api` works, and every workflow skill uses the failing form. Rules 13 and 14 depend on a tier and a ledger that live in the gitignored `.claude/plans/`. The hook only matches `build|test`, so `/spy-run`, `/spy-emit` and `/quick-check` run `dotnet run --project src/Sharpy.Cli`, an unserialized MSBuild into the same `bin/obj` a gate may be using.

**P3. Three of four hooks are dead (confirmed).** `block-secrets.sh`, the `.expected/.error` warning and `format-on-save.sh` read `$TOOL_INPUT_FILE_PATH`, which Claude Code never sets (hooks receive JSON on stdin, as `enforce-dotnet-serialized.sh` correctly does). So secrets are not blocked, expectation edits are not warned, and nothing formats on save, which is why whitespace-only fix commits keep appearing and why `/push` needs a solution-wide `dotnet format --verify-no-changes`. `/commit` still claims formatting "is handled automatically by the PostToolUse hook".

**P4. No fast inner loop is documented.** The guidance stops at "filtered run". A prebuilt `sharpyc` compiles and runs a program in 0.75 s and needs no lock; the compile server (`src/Sharpy.Cli/Services/CompileServer.cs`) exists and no skill uses it. Measured in this audit: Core tests 4,055 in 6 s wall (2.2 GB peak), Lexer + Parser 4,412 in 36 s (3.0 GB), CLI 1,127 in 109 s (4.0 GB). There is no `Fast` trait.

**P5. Shared fixed log paths and orphaned holders.** `/run-tests` does `rm -f .claude/tmp/last-test-run.log` before queueing on the lock, so two agents clobber each other; the same pattern applies to the gap-analysis, spy-run and quick-check logs. A hung orphaned `dotnet` child is never killed; waiters time out at 75 min. `/gap-analysis` omits the console logger the watchdog depends on.

**P6. The dev/mainline flow adds noise and skips gates.** Full CI runs only on pushes to `mainline`; `dev` gets only `dev-staleness.yml`, so the spec-block, allowlist-state and allocation gates never run where code lands, and 124 direct mainline pushes versus about 10 PRs means regressions are caught after landing. 21 of the last 100 commits are merges. Commit subjects: median 93 characters over six months, 185 over the last three weeks, against the `/commit` skill's 72; release notes built from them run 47–56 KB.

**P7. Machine-local state carries the project's vocabulary.** Issue bodies cite plan IDs (P41 … P77a) and ruling letters (R-DX … R-KN) defined only in gitignored `.claude/plans/` and the owner's memory index. A fresh session cannot interpret most of the tracker. The `git restore` incidents that justify the shared-tree rule appear in no issue.

**P8. Documents drift after decisions.** The 2026-07 roadmap closed all 25 issues within a week but its status block still says "issues filed". Free unions, structured parallelism and placement decorators were closed not-planned yet their design docs still read "Status: Design". `csharp14-user-defined-compound-assignment.md` calls `inplace_augassign` experimental after it graduated. `deviations.yaml` (114 executed rows) renders nowhere in the docs site, which the Mojo review (R7) already flagged. Eight spec files are absent from the mkdocs nav. `gap-discovery-contracts.md` is wrong about the spec-block allowlist.

---

## 6. CI and nightly

- **PR/push CI (`dotnet10.yml`)**, 181 runs since 2026-09-07: 124 mainline pushes (12.1% failed), 57 PRs (22.8%, mostly Dependabot). Median wall clock 19 min since sharding (#2178), 64 before. About 94 runner-minutes per run across 23 jobs. Compiler shard 5 went from 4–8 min to 15–19 min on 2026-10-24 (coincides with the #2324 typed-generator commits **[inferred]**) and sets the wall clock on every run while six shards finish in 3–6 min. No `timeout-minutes` except Windows, no `--blame-hang`, no `concurrency`/`cancel-in-progress`, no NuGet cache (two restore outages caused failures). Failures since sharding are rare and mostly flakes (a timing-ratio test, fixed; a socket timeout, unfixed) or Windows-only.
- **Nightly**: see V5. The workflow header's "soak `dev`, where work lands first" is stale; the #2365 comment cites the mainline `head_sha`, not the soaked commit.
- **Benchmarks**: the allocation gate (10% regression on Allocated) runs only on PRs, so it ran 13 times against 124 direct pushes. The weekly cross-language benchmark records to `dev` with `git pull --rebase || true`.
- **`dev-staleness.yml`**: 44 of 233 runs failed, all real signals (spec blocks, spy staleness, allowlist state).

---

## 7. Performance

Compile-time, measured at `ee9a007` Debug:

| Workload | Wall | Breakdown |
|---|---|---|
| hello world `sharpyc run`, warm | 0.75 s | emit alone 0.78 s; execution is noise |
| 61-file stdlib `.spyproj`, cold | 4.7 s | type checking 1.59 s, codegen 1.18 s, IL emission 1.45 s, lex + parse 0.12 s, module discovery 0.28 s |
| same, warm, one file touched | 2.7 s | TC 0.20 s, CG 0.21 s, **IL emission 1.79 s** |
| same, warm, no change | 2.0 s | `Files Compiled: 0`, **IL emission 1.52 s** |

The front end is cheap; type checking and codegen are 10× lexing and parsing, and Roslyn IL emission is a fixed ~1.5 s floor the incremental path never skips. Compilation is sequential (`ProjectCompiler.cs:32`), `parallel-compilation.md` is still "Design", yet `SemanticInfo` pays for ~90 `ConcurrentDictionary`s. `Sharpy.Compiler.Benchmarks` tops out at a 632-line corpus with no type-checker-isolation or pathological-overload case. `Token` is a reference record and `TokenizeAll` is a 912-line method. On the runtime side, the hot-path element access work (#2404) is real and guarded; the remaining hazards are the O(n²) `popitem`, per-comparison sort keys, and boxing in generic list comparison.

---

## 8. Status of the Mojo 1.2 review (2026-10-22)

Of the twelve bugs it filed, five closed on 2026-10-31 (#2347 center rounding, #2348 whitespace set, #2354 struct self-field, #2355 semicolons, #2357 `int("1_000")`), #2358 was closed not-planned, and six remain open: #2349 and #2350 (math accuracy and 19 missing functions, ruled into a generator-first group), #2351 (`int.to_bytes` family), #2352 (bare tuple subject), #2353 (duplicate literal arm, SPY0908), #2356 (tuple ordering, reproduced here). The R5 union-printing ruling is pending on #2057. R7 (render `deviations.yaml` as a Python-developers guide) has not happened. Its structural suggestions align with this audit: a differential numerics corpus (R3), the leak corpus for the A2 class (R2), and executable spec blocks.

---

## 9. Recommended program

### Tier 0 — this week, cheap, high value

1. **Fix the three dead hooks** to read `tool_input.file_path` from stdin JSON; mutation-test them (write a `.pem`, expect a deny). Make format-on-save single-file or drop it; keep the `/push` format gate.
2. **Repair CLAUDE.md**: wrapper-form commands only; one gate shape written as a script (`.claude/scripts/gate` looping the five test projects with the console logger) and referenced from CLAUDE.md, the wrapper and the skills; add the Lowering stage to the pipeline line; add a six-line "Inner loop" (build once, probe with the prebuilt `sharpyc`, one fixture by `DisplayName~`, the owning project, gate at phase close); add a cloud-session note (`gh api` only, `dotnet` may need installing, `.claude/plans` absent). Cut to ≤8 KB and move measurements into scripts.
3. **Add compilation options to the incremental cache key** (features, references, TFM, configuration) and a feature-flag and reference axis to the cold/warm differential; skip IL emission when nothing changed. Fold into #2114's class.
4. **Point the nightly at `mainline`**, print the soaked SHA in the job summary, add `timeout-minutes` and `--blame-hang-timeout 10m` to every test job, `concurrency` with `cancel-in-progress` for PRs, NuGet caching, and split Compiler shard 5 (merge shards 1 and 6 to stay under the 20-job cap). Run the spec-block, allowlist-state and allocation gates on mainline pushes.
5. **Fix `dict` ordering** with a compact ordered layout and a property test against the Python model; fix `round()` per `float.__round__`; wrap `date`/`time` constructors to raise `ValueError`; file L2 as one class issue with the exception-leak scans as its guard.
6. **Unique log paths** per wrapper invocation printed to stdout; let a waiter kill an orphaned child whose holder is dead and whose log is silent past the stall window.

### Tier 1 — the next one or two rounds, structural

7. **Finish E2.** IR is the emitter's only input; `SemanticInfo.Freeze()` after validation; `MergeFrom` asserts disjointness; a ratchet on the table count (88, trending down). This is the structural cure for the SPY0908 class and should be chartered as the one `full`-tier group.
8. **`ErrorType` / `InferenceVariable` split** with a deep Unknown walk at materialization; delete the 36 emitter guards.
9. **`CheckContext` record** replacing the ambient fields; overload resolution on an overlay. Start with the five most-threaded fields and delete their save/restore sites.
10. **Narrow the Rule-2 guard**: no type inspection in CodeGen outside `TypeSyntaxMapper`; ratchet the 160 sites to zero; replace name and line symbol lookups.
11. **Typed diagnostic descriptors** with generated explanation stubs.
12. **Exception-hierarchy table test** against CPython's MRO (#2124) and one fix.

### Tier 2 — process reset, decided by the owner at the next `/batch-issues`

13. **Per-group budget**: at most two whole-solution gates, plans ≤15 KB, `/verify-plan` dropped for the `light` tier and folded into a ≤40-line checklist in `/implement-plan`. Track `filed / closed` per round as the primary health metric and pause new harness aperture when closed < filed for two rounds.
14. **Allowlist policy**: report total rows in every round recap; cap rows per cited issue (no omnibus cites, so #1952 and #2423 split into bucket issues); one ledger file per generator; structured `cite=#N` fields the gate parses and reason text it does not; one `band` round draining or fragment-marking the 466 spec blocks so the spec is executable again.
15. **Commit the round record**: charters or a one-page summary per round under `docs/process/rounds/` so plan IDs and ruling letters mean something to a fresh session; delete the `~/.claude/plans` fallback.
16. **Branching**: run full CI on `dev` pushes (or on dev→mainline PRs only) and fast-forward `mainline`, or drop `dev`. Enforce a ≤100-character subject with a commit-msg hook.
17. **Measure before adding tests**: add coverlet, dedupe the 29 duplicate fixture groups with a hash guard, replace count-pinning census tests with set membership derived from the fixture directory, and adopt a test-LOC ÷ product-LOC budget as a charter fact.
18. **Type identity by definition handle** and one CLR mapper (A5), the larger refactor, scheduled after E2 lands.
19. **Pin one Python oracle version** in one file; add a netstandard2.1 smoke job; move `KnownMismatchModules` into a cited allowlist; update or move the stale design docs and the roadmap status.

### Stop doing

- Adding a node-keyed `SemanticInfo` table for a new fact; put it on the IR node.
- Writing lowering facts from validators.
- Citing an omnibus issue from an allowlist row.
- Pinning fixture counts in tests.
- Converting a nightly find in a known class into a known-red skip; treat a new seed in a pinned class as a signal that the class fix is incomplete.
- Filing per-cell residue issues before the class harness exists (the contract already says this; the 124 residue titles say it is not yet followed).

### Health metrics to report every round

Open issues and closures; SPY0908 inflow and outflow; total allowlist rows; `SemanticInfo` table count; test LOC ÷ product LOC; PR CI median wall clock and slowest shard; nightly real finds versus self-inflicted reds; incremental warm no-change build time.

---

## 10. What to protect

The ratchets and their drain-on-fix discipline; the behavioral `MergeFrom` guard; symbol-level freezes that throw; the single compile path through `SyntheticProject`; `ReferenceLoadContext`; the SPY0908 policy as a contract with a non-vacuous `ILCompilesPropertyTests`; parser error recovery with unparser round-trip and CPython differential parsing; the single `CheckStore` seam and SyntaxFactory-only emission; `DispatchSiteInventoryTests` and the totality tests; the dotnet-serialized wrapper and its false-green guards; owner rulings framed as one-way or two-way doors; the DEVIATION-by-default fidelity scope; the generated-artifact staleness gates; the feature-flag lifecycle with gated/ungated fixture twins.

---

## Appendix: not verified

- Defect-class counts are title-keyword matches and overlap; comment threads were read only for a handful of issues.
- The ruling count (~250–300) is inferred from the R-KN lettering.
- The cause of the shard 5 slowdown is a timing correlation with #2324, not a per-test measurement.
- Whether the September `BuiltinShadowingPropertyTests` reds were real bugs or seed flakes.
- CI memory use (nothing records it) and queue time separate from run time.
- The overload-probe rollback gap (A2) and the boxing in `List<T>` comparison (L7) were read from code, not executed.
- `netstandard2.1` behavior on Mono/Unity was not executed.
- Tests were not run as a whole in this container (15 GB RAM, below the measured 18.5 GB peak); only Core, Lexer + Parser and CLI subsets were run, all green.
