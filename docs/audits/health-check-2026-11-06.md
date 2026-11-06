# Project Health Check — Architecture, Verification, Process

> **Date:** 2026-11-06
> **Commit:** `ee9a007` (mainline = dev at the time of the audit), .NET SDK 10.0.403, CPython 3.13 as oracle.
> **Scope:** 284 open issues and 1,385 issues closed since 2026-05-06; all 12 nightly runs and 181 PR/push CI runs since 2026-09-07; `src/` (static read plus live probes); `.claude/`, `build_tools/`, `docs/design`, `docs/audits`; 4,826 non-merge commits over six months; the Mojo 1.2 release review (artifact `TcHMHJ4gJTzq57cVqiomK7`).
> **Method:** five parallel survey agents (issues, CI, compiler architecture, Core/Stdlib + test infrastructure, process/tooling), then the lead verified the highest-impact claims by building the solution and running probe programs. The owner then supplied the remediation-round charter (`remediation-round-2026-09-20-batching.md`, 718 KB, call 38); a second pass of 17 slice readers, one synthesis and one adversarial critic mapped every finding to its charter home and corrected this text where the charter showed a claim stale (§9). Every number below is measured unless marked **[inferred]**. Prior audits: [2026-06-20](audit-2026-06-20.md), [2026-07-09](strategic-review-2026-07-09.md).

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

`FileNotFoundError`, `PermissionError`, `FileExistsError`, `IsADirectoryError` are not under `OSError` (`Exceptions.cs:274-339`), `OSError : IOError` is inverted, `NotImplementedError` and `UnicodeEncodeError` have the wrong bases. This is chartered: P33 (light, execution row 8) takes #2124 as its first issue and its matrix is the whole `BaseException` tree with Python's own tree as the generator, so these are planned cells and the gap is ordering, not ownership. Remedy is what P33 already says: one table-driven test comparing every Core exception's base chain to `python3 -c "print(X.__mro__)"`, then one fix.

### L4. `round()` is an unguarded `Math.Round` wrapper (live)

`round(1234.0, -2)` throws `ArgumentOutOfRangeException: Rounding digits must be between 0 and 15`; Python returns `1200.0`. `round(1234, -2)` is refused at compile time (SPY0354: no overload `(int32, int32)`). `Round(double) => (int)Math.Round(x)` is an unchecked cast, so NaN, ±inf and |x| > 2³¹ wrap instead of raising, contradicting the project's own R-IS ruling (`deviations.yaml` OverflowError row). One fixture covers three values. Remedy: implement CPython's `float.__round__` and a digits × special-values differential matrix.

### L5. The stdlib API-surface check exempts 15 modules with no issue cites

Only 18 of 61 `spy/*.spy` are generated into shipped C#; the charter's own census (R-GA) counts the rest as 7 re-export stubs and 35 logic-bearing duplicates of hand-written C#. Their only correctness tie is `ApiSurfaceComparisonTests`, whose `KnownMismatchModules` HashSet exempts 15 modules (heapq, random, re, hashlib, functools, statistics, itertools, tempfile, socket, …) with no issue cites and no stale check, and an unmapped module only prints a warning. `check_allowlist_issue_state.sh` cannot see it. This is chartered as P61 (full, row 12d, after P60): #2282 cites or deletes the 15 rows and the non-canonical renderings move to an `ideal/` tree with ledger rows. The remaining gap is that the exemption is invisible to the allowlist gate until P61 lands; moving the set to an `api-surface-allowlist.txt` now is a band-sized chore.

### L6. `netstandard2.1` ships but is never executed; the Python oracle version is unpinned

All six test projects target `net10.0`; the netstandard binaries are compiled and released (`release.yml`) but no test runs them, so BCL-dependent behavior (`ToString("R")` for `str(float)`, `char.IsWhiteSpace` tables, `Dictionary` free-list order) is unverified on Mono/Unity, which is where the dogfood issues (#2182–#2203) come from. Python versions: docs say 3.9+, tools require 3.10/3.11, sweeps require ≥3.12 as a floor, CI pins 3.12, the dev box runs 3.13, and #2350 was measured against 3.13 (`math.fma` is 3.13-only). Remedy: pin one oracle minor in one file read by CI, the `RequireOrSkip` gate and the docs; add a netstandard2.1 smoke job.

### L7. Smaller items

`str.isalnum` uses `char.IsLetterOrDigit` so `'²'`, `'½'`, `'Ⅷ'` are False (Python: True; not chartered); `sorted` calls the key function per comparison, not per element (#2090 is ruled COMPARED by R-CK and planned in P34, row 7; the shipped behaviour contradicts the `deviations.yaml` ledger until then); the bottom-up merge sort has no run detection; `Dict.Get` and `defaultdict.Get` return `Optional<V>` while `collection_types.md:41` says `V?` (an owner ruling to record; not chartered); `Maketrans` returns `Dictionary<char,string>` and Difflib/Struct return raw `List<>` (CLAUDE.md anti-pattern; P39, light, row 27, with `IEnumerable<T>` deliberately exempt under R-DI); 13 hand-rolled index clamps in Core (`StringExtensions.Search.cs`, `List.Methods.cs`) bypass the `Index` seam that #2404 called "the single seam" (R-KN separately leaves six Stdlib accessors outside the contract on purpose); `List<T>` `<` boxes value-type elements through `Operator.Eq(object, object)` **[inferred]**; `math.expm1`/`log1p` naive outside tiny cutoffs (#2349 is P73, generator-first, row 9c; the 19 missing functions of #2350 land through P61's ledger under R-HL) and `public static double Pi` is a mutable static; `src/test_discovery.cs` is dead and would not compile; nine hand-written plan files live under the "generated, never hand-edit" `docs/stdlib/`.

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

**V1. Throughput.** Closures 483 → 305 → 113 (Aug → Oct); 284 open, 257 filed in Sep–Oct; 130 labeled `class-tracker` so the label no longer marks an umbrella; 124 titles say "residue", "sibling", "outlier" or "after P__" (25 open). Median time-to-close for bugs is 1.4 days, mostly batch closes by the filing session, so the median hides the long tail of class trackers. Ruling letters reach R-LJ: 274 owner rulings in this round (R-AW to R-LJ) and 322 since 2026-08-30. The charter's own census corroborates the trend: open issues went from 56 at the round's start to 283, and summing its done-ledger rows gives roughly 322 filed against 174 closed across 42 landings **[inferred: per-row sums, the charter states no total]**.

**V2. Allowlists do not trend to empty.** Rows 608 → 2,047 in three weeks. `name-resolution-differential-allowlist.txt` has 1,408 well-formed rows but 787 cite one charter issue (#2423), so they drain only in bulk; #2423 is the first tracker of P78, third in the execution order, so the drain is scheduled, and the next group (P77b) deliberately adds about 430 more red rows first. The `#1952` spec-block rows did fall from 586 to 459. `declaration-position-allowlist.txt` had 58 commits in nine days because ~8 generators share one ledger seeded red at base and drained in phases, and because any `#N` in reason text counts as a cite. `spec_blocks_allowlist.txt`: 466 blocks (37% of the spec's fenced code) do not compile and 459 cite #1952, so the issue-state gate is satisfied by keeping one tracker open; rows are sha1-keyed so editing an allowlisted example reddens two rows. `gap-discovery-contracts.md` describes this file out of date.

**V3. Test mass.** 327k lines of Compiler tests against 187k of compiler; 10.3k Compiler test methods; 119 `*Matrix*` files totaling 51.7k lines; 46k lines of sweep/conformance/differential harness; 58 inline `Known*`/`Exempt*` sets forming a second allowlist universe outside the gate. No line-coverage tooling exists (no coverlet), so redundancy is unmeasurable; mutation testing exists as an owner-triggered Stryker instrument (R-GB, Lexer at 73.27%, ruled NO-GO for nightly). 29 groups of exact-duplicate fixtures (63 files). 74 `test:` commits in three weeks only re-pinned fixture counts or rosters after fixtures were added.

**V4. Guards that passed vacuously, repeatedly.** Python-twin tests passed with no Python 3.12 (#2342); 114 `.warning` sidecars asserted nothing (#1277); spec blocks proved compilation but not output (#2286); a substring grep stood in for architectural enforcement (#1475); the nightly property soak never finished and nobody noticed for two runs (#2392); vstest OOM makes a passing run report "Test Run Failed" with 0 failed rows (#2438, open, seen three times). The pattern is that each guard's SKIP path was not itself guarded.

**V5. The nightly finds little.** 12 runs since 2026-10-25: 8 green, 4 red; 3 reds were the soak breaking itself (#2392); the one real find (`raise None.m` → `throw null.M`, CS0023, seed `c4U0RdI-d-Uw`) was posted to #2365 against the wrong commit and then pinned under owner ruling R-KK as a self-failing known-red filter that reports "n skipped (#2365)", fails the first time such a draw compiles, and drains with P17 (row 35). The residual risk the round's own verify note concedes is that the filter matches on the CS0023 code alone and can over-skip an unrelated draw. It soaked `dev`, which lagged `mainline` by 118 and 137 commits on two measured nights. The fuzz oracle is crash-only and has never been red. The Core, Stdlib and LSP "property" jobs rerun whole suites (~30 runner-min) for about 15 samples.

---

## 5. Process and tooling footguns, ranked by cost to future sessions

**P1. The plan/verify pipeline is heavier than what it protects.** One group requires reading about 130 KB of procedure (contract 29 KB, CLAUDE.md 18.5 KB, create-plan 18, verify-plan 27, implement-plan 23, verify-implementation 37, gap-discovery-contracts 63). Plans grow to 196 KB over ten `--adequacy` passes. By the contract's own ledger `/verify-plan` returned PASS WITH CORRECTIONS on 53 of 53 first passes and "has never stopped a plan"; the one exception is P77a, whose tenth adequacy pass returned NEEDS REVISION and the owner overrode it to implement. 112 of its 132 corrections were stale SHAs or wording. Each full-tier group runs two to four whole-solution gates (~30 min each: the implement gate, the verify re-gate, the base-commit control run, and a final gate after remediation) behind one machine-wide lock **[inferred from the skill procedures; the charter states one commit gate per landing and no per-group count]**; the Stryker pilot held that lock for 7 h 23 min and waiters time out at 75 min. Verify rounds did land 14 real fixes in three weeks, so the execution stages pay; the reading and pinning stages are the bulk.

**P2. CLAUDE.md contradicts its own enforcement.** Its Commands and Testing blocks list raw `dotnet build`/`dotnet test`, which the PreToolUse hook blocks. It says the commit gate is the whole solution; the wrapper header says "GATE ON A PROJECT, NOT ON sharpy.sln (#1384)" because a solution-level run produces a cross-project false red. Its pipeline line omits the Lowering IR stage that `architecture.md` includes. It says `gh` fails TLS; in a cloud session `gh issue view/list` returns HTTP 403 while `gh api` works, and every workflow skill uses the failing form. Its Axioms table still says `global`/`nonlocal` follow C# scoping, a rule the owner reversed in R-KD (writes across a function boundary need `global`/`nonlocal`, principle N6) and that P79 is scheduled to remove. Rules 13 and 14 depend on a tier and a ledger that live in the gitignored `.claude/plans/`. The hook only matches `build|test`, so `/spy-run`, `/spy-emit` and `/quick-check` run `dotnet run --project src/Sharpy.Cli`, an unserialized MSBuild into the same `bin/obj` a gate may be using.

**P3. Three of four hooks are dead (confirmed).** `block-secrets.sh`, the `.expected/.error` warning and `format-on-save.sh` read `$TOOL_INPUT_FILE_PATH`, which Claude Code never sets (hooks receive JSON on stdin, as `enforce-dotnet-serialized.sh` correctly does). So secrets are not blocked, expectation edits are not warned, and nothing formats on save, which is why whitespace-only fix commits keep appearing and why `/push` needs a solution-wide `dotnet format --verify-no-changes`. `/commit` still claims formatting "is handled automatically by the PostToolUse hook".

**P4. No fast inner loop is documented.** The guidance stops at "filtered run". A prebuilt `sharpyc` compiles and runs a program in 0.75 s and needs no lock; the compile server (`src/Sharpy.Cli/Services/CompileServer.cs`) exists and no skill uses it, though the charter records open warm-correctness reds against it (a sticky `0x8013141E` after an in-place rebuild and stale analysis after an atomic replace, P28 #2114 and P42 #2322), so the prebuilt binary is the safe inner loop today and the server is one only after those land. In-process `run` is a parked door (R-GK, #2293). Measured in this audit: Core tests 4,055 in 6 s wall (2.2 GB peak), Lexer + Parser 4,412 in 36 s (3.0 GB), CLI 1,127 in 109 s (4.0 GB). There is no `Fast` trait.

**P5. Shared fixed log paths and orphaned holders.** `/run-tests` does `rm -f .claude/tmp/last-test-run.log` before queueing on the lock, so two agents clobber each other; the same pattern applies to the gap-analysis, spy-run and quick-check logs. A hung orphaned `dotnet` child is never killed; waiters time out at 75 min. `/gap-analysis` omits the console logger the watchdog depends on.

**P6. The dev/mainline flow adds noise and skips gates.** Full CI runs only on pushes to `mainline`; `dev` gets only `dev-staleness.yml`, so the spec-block, allowlist-state and allocation gates never run where code lands, and 124 direct mainline pushes versus about 10 PRs means regressions are caught after landing. 21 of the last 100 commits are merges. Commit subjects: median 93 characters over six months, 185 over the last three weeks, against the `/commit` skill's 72; release notes built from them run 47–56 KB.

**P7. Machine-local state carries the project's vocabulary.** Issue bodies cite plan IDs (P41 … P81), execution rows (6c‴, 10h, 12d) and ruling letters (R-AW … R-LJ). Rulings themselves are mirrored as issue comments, but the index that resolves them (letter → issue, group → issues → tier → position, supersession chains such as R-JJ/R-JT/R-JV → R-KD) exists only in the 718 KB charter under gitignored `.claude/plans/`, which reached this audit as an upload. A fresh session cannot interpret most of the tracker without it. The `git restore` incidents that justify the shared-tree rule appear in no issue.

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

Of the twelve bugs it filed, five landed as the call-29 band and closed (#2347 center rounding, with its fill-character cell 2 kept open in P17 under R-IT; #2348 whitespace set; #2354 struct self-field; #2355 semicolons; #2357 `int("1_000")`), #2358 was folded into #1951 (P17's constructor ring) and closed, and six remain open with charter homes: #2349 (expm1/log1p accuracy) is P73, generator-first, row 9c; #2350 (19 missing functions) lands through P61's ledger under R-HL; #2351 (`int.to_bytes` family) is in P17 under R-KL; #2352 and #2353 are P13b cells under R-IN; #2356 (tuple ordering, reproduced here) is P34 under R-IP. R5 was ruled at call 29 (R-IM, promoted): union values print in constructor spelling, as a planned P11g cell (row 17, lands alone). R7 (render `deviations.yaml` as a Python-developers guide) has no charter home. Its structural suggestions align with this audit: a differential numerics corpus (R3), the leak corpus for the A2 class (R2), and executable spec blocks.

---

## 9. Cross-reference with the remediation charter

The owner's charter (`remediation-round-2026-09-20-batching.md`, 718 KB, 1,397 lines, at call 38) is the input to every `/create-plan`. It was read in 17 slices by parallel agents, joined against this report by one synthesis agent, and the join was adversarially checked by a critic that re-read the charter where the maps disagreed. The critic confirmed 14 corrections to earlier drafts of this text (all applied above) and rejected or downgraded three.

### Coverage: where each finding already lives

| Finding | Status | Charter home |
|---|---|---|
| A1 lowering in 88 tables, IR not the emitter's input, no freeze | **not chartered** | Only P18b deletes a table (`SequenceMaterialization`); P13b (R-IV), P57, P58 (R-FN) and P16b (#2003) add new node-keyed facts, P15 and P17 add arms, P51 and P72 leave the choice to the plan |
| A2 TypeChecker ambient context, overload probing | partially | P17 phase 1 adds the error sentinel (R-AZ, #1946); R-JP deliberately avoids per-candidate trial typing in P74; no `CheckContext`, binder split or overlay |
| A3 emitter type decisions, Rule-2 guard too wide | partially | Individual cells have homes (P49 markers, P17c pipe speller, P17d struct ctors, P25 `LruCache`, P29 R-CW access authority, P30 #2400 backing-field choice, R-HM last emitter reflection site); nothing narrows the carrier set or ratchets the 160 inspection sites |
| A4 `UnknownType` dual meaning | partially | R-AZ sentinel in P17 (row 35, behind ~30 groups); P52 removes declaration-order Unknown; no `InferenceVariable` split or nested walk |
| A5 string type identity, three codecs | partially | Instances placed in P21k, P18, P26, P31 (#2141), P32 (#2136), P34 (#2103); no definition-handle identity or single mapper |
| A6 incremental cache key and serializer | partially | P28 (light, row 28) plans reference MVID/hash in the key and compile-server cells; P21k owns a warm-only SPY0909 (#2402); P18 #1975 plans a serializer bump; features/TFM/configuration in the key, the `opt_const_fold` repro and the no-change IL skip are not chartered; #1099 is parked |
| A7 reflection in Semantic, bare catches | partially | R-DN keeps runtime reflection for P42 phase 1; #2222 metadata-only loading is later; P18 deletes the AppDomain scan; no scan over `Semantic/` |
| A8 free-text diagnostics, SPY0908 net disarmed | partially | Dozens of SPY0908 cells become refusals from P77/P78 onward; P46b and P65 fix two message-text instances; typed descriptors and #1387 have no home |
| L1 dict order after remove | **not chartered** | zero hits for insertion order; `popitem` appears only as a missing member in P38 |
| L2 CLR exceptions where Python raises `ValueError` | partially | P35 owns datetime parse/overflow cells (#2109, #2133), P55 one `re` cell, P34 #2091, R-FH, R-IG; numpy's 44 CLR throws, the `date(y, m, d)` constructor and the spy tests asserting CLR types have no home |
| L3 exception hierarchy | planned | P33, light, row 8, whole `BaseException` tree |
| L4 `round()` | **not chartered** | zero hits |
| L5 uncited API-surface exemption | planned | P61 (#2282 cite-or-delete, R-GA) |
| L6 netstandard untested, oracle version | partially | R-HZ 3.12 floor; R-DO compiles against netstandard2.1 but does not execute; no pinned oracle file |
| L7 small items | partially | P34, P73, P61, P39, P81, P38; `isalnum`, `dict.get` ruling, mutable `Pi`, dead file, misplaced plans have no home |
| V1 throughput | partially | Rulings cap residue families (R-GF, R-GO, R-GU, R-HC); nothing tracks filed/closed |
| V2 allowlist growth | partially | #2423 drains at P78, #1952 at P20 (last docs-only group, row 43); no cap per cite, no total-row metric; P77b adds ~430 rows |
| V3 test mass | partially | Stryker pilot (R-GB); no coverlet, dedupe or count-pin policy |
| V4 vacuous guards | partially | #2342 and #2392 done; #2438 rides P77b (R-KR); P63c, P62 and P60 repair recorded vacuous rows |
| V5 nightly | partially | R-KK pin; nightly on mainline, soaked SHA and a non-crash fuzz oracle not chartered |
| P1 process weight | partially | Tiers exist (R-JR); 47 of 68 open groups are still `full`; no gate or plan-size cap |
| P2 CLAUDE.md | partially | Only the `global`/`nonlocal` axiom row (P79, R-KD) |
| P3 dead hooks | **not chartered** | zero hits |
| P4 inner loop | ruled against in part | #2293 in-process run parked (R-GK); compile server has open reds |
| P5 logs and orphans | **not chartered** | only #2257 (console logger) landed |
| P6 branching | partially | R-EF, R-GH, R-GM cover CI matrix, Windows and smoke; nothing on CI-on-dev or push policy |
| P7 machine-local vocabulary | **not chartered** | rulings mirrored as comments; the index is not |
| P8 stale docs, deviations rendering | partially | spec retirements ride P77–P79; R7 and the doc statuses have no home |
| CI hygiene | partially | R-IX cost cut for shard 5; P77b fixes three CI gaps; timeouts, hang detection, cancel, cache, gates on mainline not chartered |
| Mojo review | planned | every bug placed; R5 ruled (R-IM); R7 not chartered |

### Queue economics

| Measure | Value |
|---|---|
| Open groups in §1 | 68 (47 `full`, 21 `light`, 1 `band`) plus 16 parked |
| Groups that must land alone | 9 certain (P77, P70, P11g, P26, P43, P36, P48, P18b, P45); tier letter (c) leaves up to ~20 more ambiguous |
| Groups needing a generator first | ~26 |
| Landings since 2026-09-20 | 42 (29 plan rounds, 13 chore bands) |
| Gate test count | 32,148 → 83,542 in 46 days (2.6×); the last gate exited 1 with 0 failed on the #2438 OOM |
| Whole-solution gates to drain the queue | ~183 at 3 per `full` and 2 per `light` group, ~90 h of exclusive lock at 30 min each **[inferred: the charter states no per-group count]** |
| Filed vs closed across the 42 landings | ~322 filed, ~174 closed **[inferred per-row sums]**; 13 of 42 rows closed more than they filed |
| Execution order (next) | P77b → P77 → P78 → P79 → P80 → P52 → P22c → P74 → P21k → P69 → P70 → P37 → P16b → P34 → P81 → P33 → P35 → P56 → P73 → P13b → … → P45 |

The Core fidelity groups that cure the silent-wrong classes (P33 exceptions, P34 ordering, P35 culture and datetime) sit at execution positions 15 to 17, behind seven name-resolution and codemod groups, and the two live silent-wrong bugs found here (L1, L4) have no position at all.

### The charter's own form as a footgun

- 718 KB in 1,397 lines, with single lines of several KB. No session can read it whole, so it is read in slices and cross-slice inconsistencies survive: the title says "through call 37" at call 38; row 0 lists #2429 as open after it closed on 2026-11-05; P21k says "after P21j" after P21j was dissolved (R-KF); P66, P72 and P63c say "before P52" after P52 moved (R-KH); the rationale says "P54 sits first" after P54 landed.
- Condensation deletes evidence: §5 class findings for landed groups are removed, and the 26 open §5 findings were last measured at 2026-10-05 SHAs yet are still cited. The backups the text points to could not be located from this session.
- The positional vocabulary (rows 6c‴, 6c⁶⁗, 10h, 12d) and the printed §1 table order (P22c first) differ from the stated execution order (P22c seventh).
- 274 ruling letters this round with supersession chains (R-JJ/R-JT/R-JV → R-KD/R-KF/R-KG; R-DF → R-DG; R-IR → R-KL); a reader has to resolve a chain to learn the current rule, and struck rulings are kept verbatim.
- Tier letter (c) means "seam under Semantic/CodeGen OR lands alone", so lands-alone status cannot be derived; owner overrides to `light` (e) contradict the derived letters for P63c, P69, P70, P62, P77a, P77b; P39 is `light` while its contract says "Generator: REQUIRED"; P23 and P34 decline a generator that Rule 13 would require.
- There is no machine-readable index. Every batching call and every audit, this one included, re-extracts group → issues → tier → position → rulings from prose with many agents.

### What the cross-reference changes in the program

Tier 0 items 1 to 6 are confirmed as having no charter home and are the cheapest unclaimed value: run them as one band riding P77b's gate beside #2438. Tier 1 item 7 (E2) needs an owner ruling before P13b, P51, P57 and P58 land, because they add the tables it would remove. Tier 2 item 15 becomes a generated index committed per batching call. The A6 options half (features, TFM, configuration) belongs as cells on #2114 with P28's matrix widened, or as a band chore now rather than at row 28.

## 10. Recommended program

### Tier 0 — this week, cheap, high value

1. **Fix the three dead hooks** to read `tool_input.file_path` from stdin JSON; mutation-test them (write a `.pem`, expect a deny). Make format-on-save single-file or drop it; keep the `/push` format gate.
2. **Repair CLAUDE.md**: wrapper-form commands only; one gate shape written as a script (`.claude/scripts/gate` looping the five test projects with the console logger) and referenced from CLAUDE.md, the wrapper and the skills; add the Lowering stage to the pipeline line; add a six-line "Inner loop" (build once, probe with the prebuilt `sharpyc`, one fixture by `DisplayName~`, the owning project, gate at phase close); add a cloud-session note (`gh api` only, `dotnet` may need installing, `.claude/plans` absent). Cut to ≤8 KB and move measurements into scripts.
3. **Add compilation options to the incremental cache key** (features, references, TFM, configuration) and a feature-flag and reference axis to the cold/warm differential; skip IL emission when nothing changed. Fold into #2114's class.
4. **Point the nightly at `mainline`**, print the soaked SHA in the job summary, add `timeout-minutes` and `--blame-hang-timeout 10m` to every test job, `concurrency` with `cancel-in-progress` for PRs, NuGet caching, and split Compiler shard 5 (merge shards 1 and 6 to stay under the 20-job cap). Run the spec-block, allowlist-state and allocation gates on mainline pushes.
5. **Fix `dict` ordering** with a compact ordered layout and a property test against the Python model; fix `round()` per `float.__round__`; wrap `date`/`time` constructors to raise `ValueError`; file L2 as one class issue with the exception-leak scans as its guard. None of these has a charter home; L1 and L4 are live silent-wrong bugs and fit as Core band chores riding P77b's gate beside #2438, and the L2 class issue belongs beside P35, which owns only the datetime parse and overflow cells.
6. **Unique log paths** per wrapper invocation printed to stdout; let a waiter kill an orphaned child whose holder is dead and whose log is silent past the stall window.

### Tier 1 — the next one or two rounds, structural

7. **Finish E2.** IR is the emitter's only input; `SemanticInfo.Freeze()` after validation; `MergeFrom` asserts disjointness; a ratchet on the table count (88, trending down). This is the structural cure for the SPY0908 class and should be chartered as the one `full`-tier group. It needs an owner ruling before P13b, P51, P57 and P58 land: four open groups definitely add new node-keyed tables (P13b under R-IV, P57, P58 under R-FN, P16b for #2003) and up to four more extend or may add them, and only P18b deletes one. Either charter E2 or record that table growth is accepted; today the charter's standing Rule-2 cure (a node-keyed fact joined to `MergeFrom`) and this recommendation point in opposite directions **[inferred: the charter does not state a tension]**.
8. **`ErrorType` / `InferenceVariable` split** with a deep Unknown walk at materialization; delete the 36 emitter guards.
9. **`CheckContext` record** replacing the ambient fields; overload resolution on an overlay. Start with the five most-threaded fields and delete their save/restore sites.
10. **Narrow the Rule-2 guard**: no type inspection in CodeGen outside `TypeSyntaxMapper`; ratchet the 160 sites to zero; replace name and line symbol lookups.
11. **Typed diagnostic descriptors** with generated explanation stubs.
12. **Exception-hierarchy table test** against CPython's MRO (#2124) and one fix.

### Tier 2 — process reset, decided by the owner at the next `/batch-issues`

13. **Per-group budget**: at most two whole-solution gates, plans ≤15 KB, and the remaining `/verify-plan --adequacy` stage dropped for the `light` tier (R-JR already cut it to matrix arms, guard mutations and design predicates) in favour of a ≤40-line checklist in `/implement-plan`. Track `filed / closed` per round as the primary health metric and pause new harness aperture when closed < filed for two rounds.
14. **Allowlist policy**: report total rows in every round recap; cap rows per cited issue (no omnibus cites, so #1952 and #2423 split into bucket issues); one ledger file per generator; structured `cite=#N` fields the gate parses and reason text it does not; one `band` round draining or fragment-marking the 466 spec blocks so the spec is executable again.
15. **Commit the round record**: a generated one-page index per batching call under `docs/process/rounds/` (group → issues → tier → execution position → current rulings with supersession chains resolved) so plan IDs, row numbers and ruling letters mean something to a fresh session, and so the stale cross-references that survive condensation (see §9) get caught; delete the `~/.claude/plans` fallback.
16. **Branching**: run full CI on `dev` pushes (or on dev→mainline PRs only) and fast-forward `mainline`, or drop `dev`. Enforce a ≤100-character subject with a commit-msg hook.
17. **Measure before adding tests**: add coverlet, dedupe the 29 duplicate fixture groups with a hash guard, replace count-pinning census tests with set membership derived from the fixture directory, and adopt a test-LOC ÷ product-LOC budget as a charter fact.
18. **Type identity by definition handle** and one CLR mapper (A5), the larger refactor, scheduled after E2 lands.
19. **Pin one Python oracle version** in one file; add a netstandard2.1 smoke job; move `KnownMismatchModules` into a cited allowlist; update or move the stale design docs and the roadmap status.

### Stop doing

- Adding a node-keyed `SemanticInfo` table for a new fact; put it on the IR node.
- Writing lowering facts from validators.
- Citing an omnibus issue from an allowlist row.
- Pinning fixture counts in tests.
- Keying a known-red pin on an error code alone (the R-KK filter matches any CS0023); key such pins on the shape and location, and treat a new seed in a pinned class as a signal that the class fix is incomplete.
- Filing per-cell residue issues before the class harness exists (the contract already says this; the 124 residue titles say it is not yet followed).

### Health metrics to report every round

Open issues and closures; SPY0908 inflow and outflow; total allowlist rows; `SemanticInfo` table count; test LOC ÷ product LOC; PR CI median wall clock and slowest shard; nightly real finds versus self-inflicted reds; incremental warm no-change build time.

---

## 11. What to protect

The ratchets and their drain-on-fix discipline; the behavioral `MergeFrom` guard; symbol-level freezes that throw; the single compile path through `SyntheticProject`; `ReferenceLoadContext`; the SPY0908 policy as a contract with a non-vacuous `ILCompilesPropertyTests`; parser error recovery with unparser round-trip and CPython differential parsing; the single `CheckStore` seam and SyntaxFactory-only emission; `DispatchSiteInventoryTests` and the totality tests; the dotnet-serialized wrapper and its false-green guards; owner rulings framed as one-way or two-way doors; the DEVIATION-by-default fidelity scope; the generated-artifact staleness gates; the feature-flag lifecycle with gated/ungated fixture twins.

---

## Appendix: not verified

- Defect-class counts are title-keyword matches and overlap; comment threads were read only for a handful of issues.
- The per-group gate count (two to four) and the queue's gate and filed/closed totals are inferred from the skill procedures and from summing the charter's done-ledger rows; the charter itself states neither.
- The charter's backup files named in its preamble could not be located from this session, so "the backups hold every census line verbatim" is unchecked.
- The cause of the shard 5 slowdown is a timing correlation with #2324, not a per-test measurement.
- Whether the September `BuiltinShadowingPropertyTests` reds were real bugs or seed flakes.
- CI memory use (nothing records it) and queue time separate from run time.
- The overload-probe rollback gap (A2) and the boxing in `List<T>` comparison (L7) were read from code, not executed.
- `netstandard2.1` behavior on Mono/Unity was not executed.
- Tests were not run as a whole in this container (15 GB RAM, below the measured 18.5 GB peak); only Core, Lexer + Parser and CLI subsets were run, all green.
