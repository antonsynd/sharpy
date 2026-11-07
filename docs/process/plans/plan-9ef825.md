<!-- Verified by /verify-plan on 2026-10-14 @ 486c9a88a -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: built-first -->
# P54 — process-wide state under concurrent in-process use: a compile is a pure function of its inputs, a stdlib module global is a process global CPython makes thread-safe, a per-event artefact is created exclusively

## Context

Before P41 (#2179) every test project ran on one thread, which hid every piece of process-wide
mutable state in the compiler, the stdlib and the tooling. Now `Sharpy.Compiler.Tests` runs 2
threads, `Sharpy.Stdlib.Tests` 4, `Sharpy.Cli.Tests`/`Lsp.Tests`/`Core.Tests` 8
(`xunit.runner.json`, read @ 3f16f261e), the LSP server holds ONE `CompilerApi` for the life of
the process (`src/Sharpy.Lsp/Program.cs:91`), the compile server (`sharpyc server`) serves many
requests from one process, and the REPL (P72) will too. Issue #2250 (class tracker,
`lane:silent-wrong` `reach:probe`) records the cells: two in-process compilations of one source
gave 5 vs 1 diagnostics; a warm `CompilerApi` used SEQUENTIALLY answers differently from a cold
process; one compile server answers request 3 of `{no -r, -r, no -r}` with `SPY0908 CS0400`
instead of `SPY0300`; five stdlib module globals are plain statics read by tests that now run
concurrently; `CrashBundleWriter` is check-then-create; a socket test's "closed port" can be
re-taken between close and connect.

The round charter (`.claude/plans/remediation-round-2026-09-20-batching.md` §3.6 — condensed
2026-10-14; the pre-condensation text is
`.claude/plans/remediation-round-2026-09-20-batching.pre-condense-2026-10-14.md`) places this
as **P54 — process-wide state under concurrent in-process use** (Compiler + Stdlib + tooling),
generator FIRST, directly after P46 (DONE @ 3f16f261e). Owner rulings, authoritative:

- **R-EX** (own group, harness first; rejected: split onto P28 / chores without a harness):
  https://github.com/antonsynd/sharpy/issues/2250#issuecomment-6008446544
- The P47 ORDER cell (warm-sequential ≠ cold; the harness gains {cold, warm-sequential,
  warm-concurrent} × {analysis, handler query, applied-text analysis}):
  https://github.com/antonsynd/sharpy/issues/2250#issuecomment-6120931669 and its placement
  https://github.com/antonsynd/sharpy/issues/2250#issuecomment-6169740391
- The compile-server cell and the **R-HI** dependency (SPY0306 keeps ONE meaning once P54
  removes the "already loaded from another path" leak; P42 widens the doors AFTER P54):
  https://github.com/antonsynd/sharpy/issues/2250#issuecomment-6174716722,
  https://github.com/antonsynd/sharpy/issues/2250#issuecomment-6181244258, and R-HI on #2182
  (charter §3.30 — P42) [CORRECTED: §3.21 is P37; R-HI is comment 6181244726 on #2182, which names §3.30].
- The stdlib-global cells (P41 verifier and class-cure auditor):
  https://github.com/antonsynd/sharpy/issues/2250#issuecomment-6002790404,
  https://github.com/antonsynd/sharpy/issues/2250#issuecomment-6006313855

## Current State

Read @ 3f16f261e (tree: `D data/python-sharpy-feature-comparison.md`, untracked
`data/stream_store/`; neither touches this plan). Every "measured" value below is the issue's or
the charter's, at the sha it names; everything else is code-read at 3f16f261e and is the
candidate list Phase 1 measures — the owner's instruction is "find it, don't guess".

### The cells (symptom report, not the test plan)

| # | pair / site | program | observed | expected | lane · reach | measured by |
|---|---|---|---|---|---|---|
| 1 | compile × compile, 8 threads | `Fuzz.SemanticPropertyTests.Compilation_IsDeterministic(seed: 2025)` iter 18: `from snake_case import __dunder__` · `def count() -> tuple:` · `if "self": for __dunder__ in None: pass` · `z(8)`; two fresh `Compiler` instances in one test, other classes compiling concurrently | 5 vs 1 diagnostics, once, @ af6f30901 at `MaxParallelThreads=8`; green at 1/2/4 | identical diagnostics | silent-wrong · probe | P41 P5.1 |
| 2 | warm-sequential (DOP 1) on ONE `CompilerApi` | Extract method through `SharpyCodeActionHandler` on `fstrings/format_iformattable_class_1988` (cr twin) [12:0-19:24] after the sweep's earlier documents | first parse error of the applied text at (12,49) vs (12,59) cold; same shape on `format_no_dunder_format_refused_1988` and `deferred_overload_annotated_pin_1589`; two DOP-1 runs agree with each other | identical to a cold process | silent-wrong · probe (one LSP process serves many documents) | P47 sweep @ 43aab2658 |
| 3 | warm-concurrent (DOP 4) on ONE `CompilerApi` | same handler path | (12,55) varying between runs; `Analyze` of the IFormattable fixtures gained/lost SPY0220 between two identical FULL runs | identical to cold; `sharpyc emit diagnostics` reports 0 SPY0220 | silent-wrong · probe | P47 sweep @ 43aab2658 |
| 4 | compile server × a reference across requests | ONE `sharpyc server`: `run main.spy` (imports `Greeter`, no `-r`) → `-r Greeter.dll` → no `-r` | request 1 SPY0300 (correct); request 2 runs; request 3 `SPY0908 … CS0400`, exit 2 | SPY0300 on request 3, as three fresh processes answer | ice · program | P46 final verifier @ 739ae5416, same @ c245ece62 |
| 5 | in-process compile × a same-named assembly from another path | `ModuleNamespaceShadowsClrTypeTests` (9 run-route cells) in a test host that had already loaded `AppTypes.dll` from another temp dir | SPY0306 "Assembly with same name is already loaded" recorded, and the compile **succeeds against the first copy**; P46 therefore filters `MissingReferenceDiagnostics()` to SPY0305 only (`ModuleRegistry.cs:52`) | the registry loads the file it was given | silent-wrong · probe | fa7e8aa63 (commit body) |
| 6 | stdlib fn × fn: `random` | `RandomModuleModule._Random` is one `public static System.Random` (`Random/Random.cs:18`, generated from `spy/random_module.spy:5`); `seed()` swaps it | code-read; `System.Random` is documented non-thread-safe and can collapse to 0 under concurrent `Next` | python3 3.12: "The underlying implementation in C is both fast and threadsafe" (`random.__doc__`, verified this session; 8 threads × 20 000 draws, 0 zeros) | silent-wrong · latent (readers in one `PrngState` collection today) | P41 Phase 2 audit |
| 7 | stdlib fn × fn: `socket.setdefaulttimeout` | writes `public static double? _DefaultTimeout` (`Socket/SocketModule.cs:38,86`, spy-sourced) — a 16-byte nullable, not an atomic write | code-read: a torn write can publish `HasValue` with a stale/garbage value | CPython: one pointer write under the GIL (verified: 8 threads × 2 000 writes/reads, no error, in-domain value) | silent-wrong · latent | P41 verifier |
| 8 | stdlib fn × fn: `calendar.setfirstweekday` | `private static int _moduleFirstweekday` (`Calendar/CalendarConstants.cs:37`) | code-read: an `int` write is atomic — a roster row with a positive-control theory, no cure expected | process-wide, thread-safe | silent-wrong · latent | P41 verifier |
| 9 | stdlib fn × fn: `xml.register_namespace` | writes `internal static readonly Dictionary<string,string> _registeredNamespaces` (`Xml/XmlFunctions.cs:176`); **nothing reads it** — `Tostring` (`:100`) never consults it [CORRECTED: grep @ 486c9a88a finds one reference, the write at `:173`; "registered prefixes are ignored by tostring" is an xml-serialization fidelity cell OUTSIDE this contract → findings-ledger row, no open tracker known, the lead files]. The concurrency theory's read operation therefore reads the field itself (`InternalsVisibleTo`), not `tostring` output | code-read: concurrent `[]=` on a `Dictionary` corrupts it (throws or loops) | CPython: dict write under the GIL | silent-wrong · latent | class-cure auditor @ 5b98c8e24 |
| 10 | stdlib fn × fn: `logging.basicConfig` | `LoggingModule._loggers` is a `ConcurrentDictionary` (`Logging/LoggingModule.cs:27`); `Logger._level` is an INSTANCE `int` (`Logging/Logger.cs:14`, written by `SetLevel` at `:33`) — not a module global [CORRECTED: `Logger._level` is per-instance; only `_loggers` is process-wide] | code-read: already safe — roster rows + positive control | process-wide | — | class-cure auditor |
| 11 | stdlib fn × fn: `numpy.random.seed` (sibling found by this plan's survey) | `NumpyRandom._rng` is `[ThreadStatic]` (`Numpy/NumpyRandom.cs:19`) — seeding on one thread does not seed another | code-read; numpy 2.5.1 verified this session: `np.random.seed(7)` on the main thread, the draw on a worker equals the main thread's re-seeded draw — the global `RandomState` is process-wide | process-wide seeded generator | silent-wrong · probe | this plan's static census |
| 12 | crash × crash | `CrashBundleWriter.TryWrite` (`Diagnostics/CrashBundleWriter.cs:102-116`): `while (Directory.Exists(candidate)) suffix++` then `CreateDirectory` | code-read: two writers in one millisecond pick one directory and interleave `report`/`sources` | one directory per crash (create-exclusive) | tooling · benign | P41 Phase 4.1 |
| 13 | test: ephemeral port re-taken | `_closed_loopback_port` (`Stdlib.Tests/Spy/socket/socket_module_tests.spy:693`) binds port 0, closes, expects refused | low-probability flake at 4 threads; on macOS a bound-but-not-listening port TIMES OUT (verified this session: `TimeoutError`), so "hold the port bound" is not the cure | a deterministic refused outcome | tooling · test | P41 verifier |

### Where the state is (code-read @ 3f16f261e — the candidates Phase 1 discriminates)

`StaticStateConformanceTests` (#1036) bans unlisted mutable statics in the compiler assembly but
exempts `ConcurrentDictionary`/`Lazy` by design ("their contents are a deterministic function of
the key"). That exemption is where the compile cells live:

**(a) Process-wide assembly visibility — the parallel-site class.** Fourteen
`AppDomain.CurrentDomain.GetAssemblies()` scans and **seven** `Type.GetType(string)` calls [CORRECTED: re-counted @ 486c9a88a — the eighth token hit, `CodeGen/TypeSyntaxMapper.cs:800`, is `topLevelType.GetType().Name` (instance `object.GetType()` inside a diagnostic string), not a resolution site] resolve
CLR names against *whatever the process has loaded*, and `Assembly.LoadFrom` (four sites) loads
into the one default `AssemblyLoadContext`:

| site | what it answers |
|---|---|
| `Semantic/Registry/ModuleRegistry.cs:329` (`TryResolveNetType`), `:383` (`GetNamespaceTypes`), `:612` (`MapModuleToNamespace`'s dynamic branch, cached in the instance `_dynamicNamespaceCache`), `:746` (`EnsureRuntimeAssembliesLoaded`) | whether `Greeter` is a CLR namespace, which types it has, which assembly a `system.*` type lives in |
| `Semantic/Registry/ModuleRegistry.cs:103` (`LoadReference` → `Assembly.LoadFrom`), `:752` (TPA loads), `Discovery/ClrAttributeResolver.cs:473` | loads into the default ALC: a same-named assembly from another path throws `FileLoadException` (SPY0306 arm, cell 5) and the first copy stays visible to every later compile (cells 4, 5) |
| `Discovery/CachedModuleDiscovery.cs:262`, `:958` | resolves an overload-index `ClrTypeName` to a `Type` — against the first same-named copy loaded |
| `Discovery/ClrTypeHelper.cs:424`, `:452` (behind the **static** name-keyed caches `_originNameDefinitionCache`, `_displayNameDefinitionCache`) | a name → `Type` pinned for the process by whoever asked first |
| `Discovery/ClrAttributeResolver.cs:368`, `:449` (behind **static** `s_typePresentCache`, `_probedNamespaces`) | attribute presence, TPA namespace probing |
| `Semantic/Registry/BuiltinRegistry.cs:1204` (`TryFindClrType`, behind the master registry's `_clrTypeCache` keyed `(name, arity)`) | the bare-name CLR fallback (#2064, #2175 are this seam's name-resolution classes) |
| `Semantic/TypeChecker.Expressions.Access.Calls.cs:1759`, `Semantic/TypeChecker.Utilities.cs:1980` | call-site CLR type lookups |
| `CodeGen/RoslynEmitter.CompilationUnit.cs:590` (`IsClrTypeGenericInNamespace`) — the emitter's ONE reflection site [CORRECTED: `TypeSyntaxMapper.cs:800` is `object.GetType()` in a message, dropped] | the emitter asking reflection a semantic question (a Rule-2 cell, outside this contract — see Trackers) |
| `Discovery/Caching/AssemblyIdentity.cs:28` (`FromAssemblyPath` → `Assembly.LoadFrom`) [ADDED: adequacy] | **dead code** — zero callers src-wide @ 486c9a88a (contract §8's "helper with no callers" tell); a LoadFrom site that would leak a copy into the default context if anyone called it — DELETE it in Phase 2 task 2, do not rewrite it |
| `AssemblyCompiler.cs:405`, `Services/ReplSession.cs:517,518,531` (`Assembly.Load("<framework name>")`), `ReplSession.cs:556` (`Assembly.Load(bytes)`), `Project/ProjectCompiler.Generators.cs:243` (`new AssemblyLoadContext("SharplyGenerators", isCollectible: true)` + `LoadFromStream`) [ADDED: adequacy] | key-pure or self-owned: a framework simple name resolves to the TPA copy; the REPL loads its own submissions (P72's domain, #2310); the generator context is the precedent for Design Decision 2 and loads only sources the project compiled. These are ROSTER rows of the scan guard (justified, permanent), not allowlist rows (drain-on-fix) |

`ModuleRegistry._loadedRuntimeNamespaces` (`:696`) and `ClrAttributeResolver._probedNamespaces`
are **static check-then-act gates with no completion barrier**: thread B's `TryAdd` fails while
thread A is still loading the namespace's TPA assemblies, and B's `GetNamespaceTypes` scans a
half-loaded process — the one code-read candidate whose signature matches cell 1 (green at 1/2/4,
red at 8; a `system.*` import in the generator's vocabulary). Phase 1 tests exactly this with a
barrier-free interleaving.

**(b) The shared master registries on one `CompilerApi`.** `GetOrBuildAnalysisContext`
(`CompilerApi.cs:645-667`) hands EVERY analysis the same `BuiltinRegistry` and `ModuleRegistry`
(the per-compilation clone was retired at #1633). Post-construction writes on the master: plain
`Dictionary` writes in `GetOrCreateInterfaceSymbol` (`BuiltinRegistry.cs:619`), the `(name,
arity)`-keyed `_clrTypeCache` whose value comes from the process scan above, `_pendingInterfacePopulation`
(a `List`), `CachedModuleDiscovery._moduleTypeSymbols`, `ClrTypeBridge._interfaceSymbolCache`
(rostered by `StaticStateConformanceTests.SymbolVendingRoster`). `MasterRegistryImmutabilityTests`
snapshots symbol PROPERTIES across `Analyze`/`Compile`/`AnalyzeProject`, not dictionary
membership, and not the handler-query / applied-text path cells 2–3 run through. The LSP
serialises analyses per workspace (`SharpyWorkspace._analysisSemaphore`) but handler queries
read the shared masters concurrently with the next analysis.

**(c) Process-wide caches that ARE pure functions of their key** (keep, roster): `AssemblyCompiler.s_referenceCache`
(path + mtime), `OverloadIndexCache.s_inMemoryIndices` (content-addressed), the `Type`-keyed
`ConcurrentDictionary` caches in `ClrTypeHelper`/`ClrTypeBridge`/`BuiltinExceptionSurface`
(a `Type` is an identity — but a `Type` from a per-registry load context pins that context; see
Design Decision 3).

**(d) Tooling statics.** `CliHelpers.LastFailureExitCode` and `ShowDiagnosticProvenance`
(`Cli/CliHelpers.cs:71,79`) — the exit code of "the most recent failed compile" read by
`CompileServer.RunCompile` (`Services/CompileServer.cs:395`) after every request; serialised today
by the server's `_compileLock`, so a roster row, not a cell.

**(e) Stdlib.** The five globals plus `NumpyRandom._rng` (cells 6–11). Spy-sourced modules
(`random_module.spy`, `socket_module.spy` per `build_tools/regenerate_spy_stdlib.sh` MODULES)
emit every module-level binding as a `public static` NON-readonly field — the 60-odd spy
"constants" (`Math.Pi`, `SocketModule.AF_INET`, `StringModule.AsciiLowercase`, …) are
init-once and never assigned again; the written ones are the roster.

### Standing harnesses that should have caught this

`StaticStateConformanceTests` (exempts the caches the cells live in), `ProcessGlobalStateConformanceTests`
(#2179 — scans TEST code for known mutator tokens; the product's own statics are not its subject),
`MasterRegistryImmutabilityTests` (properties, `Analyze`/`Compile` paths only),
`DeterminismPropertyTests`/`Fuzz.Compilation_IsDeterministic` (two sequential compiles; a race
shows only when another class happens to overlap), `WarmBuildDiagnosticFidelityTests` (#1553,
warm ≡ cold across an incremental REBUILD — a different warmth). None enumerates
{threads, warmth, operation}; Phase 1 adds the harness.

## Scope

- **Group:** P54 — process-wide state under concurrent in-process use (#2250; Compiler + Stdlib + tooling). Chores riding along: **none**. The three chores the charter places "directly after P54, beside each other" — #2261 part 2 (adds process-wide threading state and must run under THIS plan's stdlib harness), #2311 (parser error cap), #2313 cell 0 (build/project deploy) — land on their own gates after this plan's push; #2261 part 2 touches the seam this plan builds (the Stdlib module-global roster), so it cannot ride.
- **Alone:** no — no printed output or emitted layout changes (the one diagnostic change, cell 4's CS0400 → SPY0300 and cell 5's dropped SPY0306 arm, is a verified-by-direction refusal, contract §3, not a layout change).
- **Gates:** 1 (the whole solution — the blast radius is every project that compiles Sharpy source).
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class (contract §10); trackers named at the end of this plan.
- **Override:** none.
- **Budget (contract §10):** the sibling-cell prober's budget is this matrix plus ≤ 2 axis changes; expected verify cost: the GapDiscovery sweep at R = 8 (one run per phase close, minutes — measured in Phase 1 and recorded), the stdlib theory at 8 × 2 000 (seconds), one whole-solution gate per phase close (~30 min each, four phases), the P47 sweep in FULL mode once (Phase 2 task 5). [ADDED: missing step]

## Defect Class

- **Violated contract:** a compile, and every semantic query on its result, is a function of its inputs alone — the same under 1, 2 or 8 concurrent compilations, on a cold or a warm instance, in a fresh or a long-lived process; a stdlib module function CPython makes safe to call from several threads is safe here (module globals are process-wide, as in CPython); a per-event filesystem artefact is created exclusively.
- **Meta-class:** "Warm ≠ cold divergence" (standard cure: per-analysis state never trusted; a warm/cold differential harness) stacked on "Position/route missing a shared check" — fourteen parallel `AppDomain` scan sites plus eight `Type.GetType` sites answer "which CLR types exist" from process state instead of ONE seam that answers from the compilation's inputs → **standard cure:** ONE assembly-visibility seam owned by the `ModuleRegistry`, a source-scan totality guard over the sites, and the differential harness as the ratchet.
- **Known member cells:** #2250 (cells 1–13 above; cells 2–4 posted 2026-10-12/14), the fa7e8aa63 SPY0306 arm (R-HI), `ModuleNamespaceShadowsClrTypeTests`' nine run-route cells.
- **Sibling cells this plan must also cover (the matrix):** pair {compile × compile, compile × module load, stdlib fn × fn, crash × crash, server request sequence} × threads {1, 2, 8} × warmth {cold, warm-sequential, warm-concurrent} × operation {analysis, handler query, applied-text analysis, compile-to-assembly} × observation {diagnostic multiset `(code, line, col)` + success, `UsedAssemblyPaths`, value/state in domain, one directory per event}; the stdlib axis is global {`random._Random`, `socket._DefaultTimeout`, `calendar._moduleFirstweekday`, `xml._registeredNamespaces`, `logging._loggers`, `numpy.random._rng`} × operation {write, read, write-then-read-on-another-thread} × threads {1, 2, 8}. ≥ 2 axes × ≥ 3 values → generator FIRST (contract §11).
- **Lane / reach:** silent-wrong · probe (cells 4 and the server family are `ice · program`); **fidelity-scope row:** not a Python-parity surface — the only CPython comparisons are the thread-safety MODEL of module globals (COMPARED as values: `numpy.random.seed` cross-thread draw equality, verified above), no message text.
- **Standing harness that should have caught it:** none — Phase 1 adds `InProcessIsolationSweepTests` (Compiler.Tests, GapDiscovery), `ProcessWideResolutionScanTests` (regular suite), `StdlibModuleGlobalRosterTests` + `StdlibModuleGlobalConcurrencyTests` (Stdlib.Tests), the server-sequence row on `CompileServerE2ETests`, `CrashBundleExclusivityTests`, and `WarmInstanceParityTests` (Lsp.Tests).
- **Generator:** REQUIRED FIRST — Phase 1 builds the interleaving generator (seeded schedules over a program corpus × instance sharing × thread count) with the allowlist ratchet `in-process-isolation-allowlist.txt`; Phases 2–3 drain it. The axis no generator can enumerate: WHICH static is the seam — that is the inventory guard (a Roslyn/reflection census, totality-anchored), and the hand matrices are the stdlib globals' theories (six rostered globals × three operations, each a literal row).
- **Owner rulings:** R-EX, R-HI (links in Context). Do not re-ask: (b) split onto P28; (c) chores without a harness; a second SPY code for the already-loaded case.

## Adversarial Review (pre-mortem)

- **Alternative root cause for cell 1 (checked by the verifier, excluded):** a 30 s per-compile cancellation under 8-thread load truncating one diagnostic set — excluded @ 486c9a88a: every phase's `catch (OperationCanceledException)` rethrows (`ValidationPipeline.cs:123`, `ScopedTypeChecker.cs:38`, `ProjectCompiler.Parsing.cs:376`, `ModuleLoader.cs:181`) to `ProjectCompiler.cs:361`, which records SPY09xx `CompilationCancelled`, and `WasCancelled` keys on that code, so a timed-out iteration is skipped, not compared. Phase 1 task 2 still runs iter 18 with the timeout at infinity once, to pin that reading with execution.
- **Alternative root cause for cell 1:** not a race in the compiler at all but the TEST — `Compilation_IsDeterministic` compares two `Compiler` instances built with `new Compiler()` (no references, no stdlib), so the only shared state reachable is (a)'s process-wide TPA loading and the static name-keyed caches; a probe that discriminates: run the iter-18 program on 8 threads with a `Barrier` released at the moment one thread enters `EnsureRuntimeAssembliesLoaded` for `System.IO` (an injected `Action` hook, test-only) — a count difference reproduces deterministically if the gate is the seam; if it does not, the next candidate is `ClrTypeHelper._originNameDefinitionCache` (poison it from thread A with a `Type` from a test assembly, read from B). Phase 1 records which, with iteration counts @ sha.
- **Alternative root cause for cells 2–3:** the applied-text analyses write symbol properties on the master (the #1633 shape) rather than growing a cache — `MasterRegistryImmutabilityTests`' snapshot, extended to dictionary KEY SETS and taken between the P47 request sequence's steps, names the first write; if the snapshot is clean and the fingerprint still drifts, the seam is `_clrTypeCache`'s dependence on (a) (an earlier analysis loaded a TPA assembly that changes what `TryFindClrType("…")` finds). Both are measured before any seam is cut.
- **Alternative layer:** rung 1 (`.spy`) carries the stdlib cures where a lock expresses them — `random_module.spy` and `socket_module.spy` gain a module-level `threading.Lock` and `with _lock:` (spy `with` is used by `hashlib_module.spy:26`; `Threading/Lock.cs` exists); rung 2 (Core/Stdlib C#) for the hand-written `xml` and `numpy.random`; the compiler seams have no lower rung — assembly visibility is compiler infrastructure (rung 3, keyed on CLR identity, never on a builtin name).
- **How the fix could be inert:** (1) a visibility seam that still consults `AppDomain.CurrentDomain` inside its own fallback arm — the scan guard bans the token outside the seam, so the fallback must be written as a filter over the registry's set, with a positive-control cell (a reference loaded by ANOTHER registry in the same process must be INVISIBLE: `from Greeter import Greeter` → SPY0300); (2) the per-registry load context that still answers from the default context because `Load(AssemblyName)` returns `Default.LoadFromAssemblyName` for everything — the same-name/different-path cell is the control (two registries, two `AppTypes.dll` with different members, each compile sees its own); (3) a `with _lock:` whose lock is itself a module-level mutable binding re-created per call — the regenerated C# is read (one `static readonly`-equivalent lock, not a per-call `new Lock()`).
- **Before/after by direction (contract §3):** cell 4 — request 3 goes from `SPY0908 CS0400` (ICE) to `SPY0300` (refusal) — ICE → diagnostic, a fix; cell 5 — `ModuleNamespaceShadowsClrTypeTests` cells go from SPY0306-recorded-then-succeed to clean success against the given file (no new refusal); every program that compiled correctly before must still compile — the `FrontEndParityTests` corpus and the whole gate are the control, and the P47 sweep's pins (`code-action-shape-allowlist.txt`) must be byte-identical when the per-(stem, twin) isolation is retired in Phase 4. No program is newly refused by this plan; if Phase 2 makes one red, it is listed with its prior behaviour (`ICE` / `wrong output` / `worked`) in the commit body — `worked` is a regression and blocks the commit.
- **Blast radius:** the whole gate once (charter §3.6): `Sharpy.Compiler.Tests` (every compile), `Sharpy.Stdlib.Tests` (the regenerated `Random.cs`/`SocketModule.cs` and every in-process spy test touching the six globals), `Sharpy.Cli.Tests` (server, deployment, option sweep — `UsedAssemblyPaths`), `Sharpy.Lsp.Tests` (`FrontEndParityTests`, `CodeActionShapeSweepTests` pins), the FIVE CI GapDiscovery sweeps (`InteropConformance`, `MetamorphicCorpus`, `DifferentialExecution`, `FormatSpecDifferential`, `FormatterEmitInvariance` — `dotnet10.yml` "matrix of 5" @ 486c9a88a) plus the NEW sixth (a `dotnet10.yml` step — Phase 1 task 7) [CORRECTED: was "three"], `Sharpy.Core.Tests` (Core has no cell; its two `Type`-keyed caches are rostered only). Warm-compile speed (D2, #1049) — the per-registry load context and the un-static-ing of name-keyed caches are measured against the existing warm/cold benchmark numbers before Phase 2 lands (Design Decision 3).
- **Generated artifacts touched:** spy-stdlib C# (`Random/Random.cs`, `Socket/SocketModule.cs` — regenerate in Phase 3's FIRST commit, read the diff as findings); stdlib docs (private `_lock` bindings do not render — `build_tools stdlib generate --force` run and `check` green anyway); spy-test C# (no `.spy` test changes except `socket_module_tests.spy` → regen); oracle ledger: none.

## Design Decisions

1. **ONE assembly-visibility seam, owned by the compilation's `ModuleRegistry` (rung 3 — CLR identity, no builtin name).** `ModuleRegistry.Visibility` (a new `AssemblyVisibility` type in `Discovery/`) answers every "which CLR types/namespaces exist" question from `{the shared framework set} ∪ {the assemblies THIS registry loaded}`, where the framework set is the trusted-platform assemblies plus `Sharpy.Core` and the compiler's own dependencies — computed ONCE per process as a `Lazy<ImmutableArray<Assembly>>` keyed by nothing that a compile can change (TPA paths are a process constant). The fourteen `AppDomain` scans and the eight `Type.GetType` calls become calls on the visibility (`ResolveType(fullName)`, `TypesInNamespace(ns)`, `NamespaceExists(ns)`, `EnsureFrameworkAssembliesFor(ns)`), every one of them pure in (registry inputs, name). A compile with NO registry (`new Compiler()`, `CompilerOptionsFactory.ForLibraryAnalysis()` with no references) gets `AssemblyVisibility.FrameworkOnly`. **The framework set must be CONSTANT, not "whatever framework assemblies are loaded so far"** [CORRECTED: a gap the verifier found — three of the sites answer a query with NO namespace to drive `EnsureFrameworkAssembliesFor(ns)`: `ClrTypeHelper.ResolveGenericDefinitionByDisplayName` (`:452`, any type of a given simple name + arity in any loaded assembly), `ModuleRegistry.MapModuleToNamespace`'s dynamic branch (`:612`, "does any loaded assembly have this namespace"), and `TypeChecker.Utilities.cs:1980` (generic twin of a non-generic `ClrType` by simple name across loaded assemblies); `BuiltinRegistry.TryFindClrType` (`:1204`) probes `ClrFallbackNamespaces` so its namespace IS known per probe. A visibility that scans "the framework assemblies loaded so far" is order-dependent by construction for the three namespace-less sites — the seam would be cut and cell 1's shape would survive]. The implementer picks ONE of: (A — recommended) the framework `Lazy` enumerates the whole TPA list ONCE (load each `System.*`/`Microsoft.*`/`netstandard`/`mscorlib` TPA assembly, index `(simpleName, arity) → Type[]` and `namespace → Type[]`), so `EnsureFrameworkAssembliesFor(ns)` is subsumed and every query is a lookup in a process-constant index — cost measured once per process (Phase 2 task 6 records the first-use millis; the compile server and the LSP amortise it, a cold `sharpyc` pays it once); (B) namespace-driven loading is kept and the three namespace-less sites are rewritten to name their candidate namespaces (`System.Collections.Generic`, the `ClrFallbackNamespaces`, the registry's own references) and `EnsureFrameworkAssembliesFor` each before scanning — cheaper, but every future namespace-less query is a new cell. Either way the plan's claim "pure in (registry inputs, name)" needs the choice recorded in the commit body. Why the lower rungs cannot carry it: no `.spy`, dunder, protocol or operator expresses "which assemblies a compilation may see".
2. **An isolated load context per registry (R-HI's "isolated registry per compilation").** `ModuleRegistry` owns a collectible `AssemblyLoadContext` (`ReferenceLoadContext`): `LoadReference` uses `LoadFromAssemblyPath` on it, never `Assembly.LoadFrom`; `Load(AssemblyName)` answers from the registry's own loaded set by simple name, else `null` (→ the default context, so `Sharpy.Core` and the framework keep ONE identity and `typeof(Sharpy.List<>)` comparisons in the compiler stay true); `Resolving` searches the reference's own directory and the registry's module paths — the same directories `ResolveAssemblyPath` already searches, no wider (a transitive reference that resolves nowhere stays #2182's R-DN phase-1 cell: warn and skip). The SPY0306 "already loaded from another path" case cannot occur (two registries hold two copies). `LoadReference` has no `FileLoadException` arm to delete [CORRECTED: read @ 486c9a88a — its catches are `IOException` (`:123`, which `FileLoadException` derives from), `BadImageFormatException` (`:128`) and `UnauthorizedAccessException` (`:133`), each recording SPY0306]; the `IOException` catch STAYS for genuine I/O failures — what disappears is the already-loaded `FileLoadException` ever reaching it. What IS deleted is `MissingReferenceDiagnostics()`' SPY0305-only filter (`:52`) and its `<remarks>` naming the leak — SPY0306 keeps ONE meaning, "a reference that cannot be loaded as given" (R-HI); P42 widens the doors afterwards, not this plan. Precedent for a collectible context inside the compiler: `ProjectCompiler.Generators.cs:243` (`SharplyGenerators`). `Sharpy.Stdlib` is itself a reference, so each registry loads its own copy; the overload-index disk cache is content-addressed and unaffected, and `CachedModuleDiscovery`'s `ClrTypeName → Type` resolution goes through the visibility (Decision 1) so it finds the copy of ITS context.
3. **Static caches are allowed only when their key is process-stable.** `StaticStateConformanceTests` rule 2 is extended to `ConcurrentDictionary<,>`, `Lazy<>` and `ConditionalWeakTable<,>` statics: each needs an allowlist row with a key-purity justification. Rows drain into the registry for the name-keyed ones (`ClrTypeHelper._originNameDefinitionCache`, `_displayNameDefinitionCache`, `ClrAttributeResolver.s_typePresentCache`, `_probedNamespaces`, `ModuleRegistry._loadedRuntimeNamespaces` → per-`AssemblyVisibility` state, the TPA gate becoming a `ConcurrentDictionary<string, Lazy<bool>>` whose `Lazy` IS the completion barrier). The `Type`-keyed caches stay static but `GetOrAdd` only for a `Type` that is WHOLLY in the default context — the gate walks generic arguments, element types and declaring types (`AssemblyLoadContext.GetLoadContext(t.Assembly) == Default` for `t` and every constituent; a `Sharpy.List<Greeter>` has a Core-assembly `t.Assembly` and still pins Greeter's context) [CORRECTED: the assembly test alone misses constructed generics]; a type from a per-registry context is answered uncached — otherwise every static cache pins every per-request context forever (the compile server would retain one stdlib copy per request). Phase 2 task 6 measures warm-compile cost before and after with the existing benchmark fixtures (`CompileServerE2ETests` phase timings and the `Benchmark`-traited suite) and records the numbers @ sha; a regression beyond noise moves those caches to per-registry instance caches instead (the fallback is named, not improvised).
4. **The master `BuiltinRegistry` is frozen after construction and its lazily-built entries are pure in (name, arity).** `_pendingInterfacePopulation` is drained in the constructor; `_interfaceSymbols` and every post-construction `Dictionary` write become `ConcurrentDictionary.GetOrAdd` with a value that is a function of the key alone; `_clrTypeCache`'s factory calls the FRAMEWORK visibility (the master is built once per reference set, so the framework set is the right one — a user reference is resolved by the `ModuleRegistry`, never by the builtin fallback; this also closes the #2064/#2175 door one notch without widening into their classes). `MasterRegistryImmutabilityTests` gains the dictionary-key-set snapshot and the handler-query / applied-text request sequence (the P47 shape).
5. **Stdlib module globals keep CPython's model — process-wide and thread-safe — at the lowest rung.** `random_module.spy` and `socket_module.spy`: a module-level `_lock: Lock = Lock()` imported as `from sharpy import Lock` — the spelling every spy stdlib module uses for a Core/Stdlib CLR type (`from sharpy import HmacObject`, `… StringIO`, …; `Lock` is `[SharpyModuleType("threading", "Lock")]` in `src/Sharpy.Stdlib/Threading/Lock.cs`, namespace `Sharpy`, with `Enter`/`Exit` + `__enter__`/`__exit__` so `with _lock:` lowers) [CORRECTED: was `threading.Lock()`; `stdlib.spyproj` carries no explicit references, so the implementer confirms the name resolves in the regen build and falls back to `from threading import Lock` if not] — and `with _lock:` around every read and write of the global (rung 1; `seed()` swaps the instance under the same lock). `xml._registeredNamespaces` → a lock around write and the `tostring` snapshot (rung 2, hand-written C#). `numpy.random` → ONE process-wide locked `System.Random` (drop `[ThreadStatic]`; rung 2) — numpy's global `RandomState` is process-wide (verified). `calendar` and `logging` are already safe: roster rows with positive-control theories. The roster itself is a Stdlib reflection census (`StdlibModuleGlobalRosterTests`): every static non-readonly field or static readonly mutable collection in `Sharpy.Stdlib` that is ASSIGNED or MUTATED after its initializer (a Roslyn scan over the Stdlib sources decides "written after init" by a SYNTACTIC rule — an assignment, compound assignment or element assignment whose target is a static field identifier outside that field's own initializer, or an invocation on a static-field receiver whose member name is in a literal mutator roster {`Add`, `Remove`, `Clear`, `TryAdd`, `AddOrUpdate`, `Enqueue`, `Push`, …} — with the generated `Random.cs` `Seed` (`_Random = new …`) as the positive control; the spy constants are init-once and excluded by that predicate, not by a 60-row list; the scan needs the Stdlib compilation's references and `GlobalUsings` to bind the receiver's symbol, `reference_roslyn_source_compilation_scan`) must be a literal roster row naming its CPython model and its synchronization; a new written global without a row is red, a stale row is red.
6. **Crash bundles are created exclusively.** The directory name gains the process id (`<timestamp>-<pid>`) and exclusivity is proven by `FileMode.CreateNew` on a marker inside the candidate (`.owner`) — the only create-exclusive primitive .NET offers on every platform; a `CreateNew` failure means another writer owns the directory → next suffix. The clock becomes an injectable `Func<DateTime>` — `CrashBundleWriter` is a `public static class` (`CrashBundleWriter.cs:63`), so there is no ctor: an `internal static string? TryWrite(string?, CrashBundleRequest, Func<DateTime> clock)` overload that the public `TryWrite` calls with `() => DateTime.UtcNow` [CORRECTED: was "internal ctor parameter"] — so the collision is DETERMINISTIC in the test. A settable static hook is NOT used: it would be a mutable static that `StaticStateConformanceTests` rule 1 flags.
7. **The loopback test retries the race it cannot prevent.** A bound-not-listening port times out on macOS (measured again @ 486c9a88a: `TimeoutError`; a closed port is `ConnectionRefusedError` at once), so `_closed_loopback_port` cannot hold the port. There is no `_assert_refused` helper [CORRECTED: grep]; the two refused-port cells are `test_connect_with_timeout_refused_raises_error_not_timeout` (`socket_module_tests.spy:591`, an inline bind-0/close probe) and the `create_connection(timeout=)` arm of `_assert_timeout_rule` (`:738`, via `_closed_loopback_port`). Both route through ONE new helper (`_connect_to_closed_port(timeout) -> str`, say) that treats an unexpected successful connect (the only symptom of the port having been re-taken) as "pick another port", up to five times, before returning the outcome — the assertions stay on the refused outcome; the race becomes a bounded retry. Positive control: the helper against a LISTENING port returns "accepted" (it must not mask a genuine accept beyond its retries); mutation: a test double whose first probe port is pre-listened and second is closed exercises the retry arm once.
8. **Tooling statics:** `CliHelpers.LastFailureExitCode` becomes a return value of `CompileToBinary` (an `out`/result-carried exit kind), removing the last read-after-the-fact global on the server path; `ShowDiagnosticProvenance` is set once per process from the parse result and is rostered as such.

## Implementation

### Phase 0: Baseline and regeneration control

**Goal:** pin the instrument and the generated artifacts before anything moves.

**Acceptance:**
- Regen control: `build_tools/regenerate_spy_stdlib.sh`, spy-test regen and `build_tools stdlib generate --force` run at BASE — "regen: no diff @ <sha>" recorded (contract §7).
- The site census is a measured number: 14 `AppDomain.CurrentDomain.GetAssemblies` + **7** static `System.Type.GetType(string)` + 4 `Assembly.LoadFrom` sites = **25** in `src/Sharpy.Compiler` @ 486c9a88a [CORRECTED: was 8 / 26 — `TypeSyntaxMapper.cs:800` is an instance `GetType()` in a message; the Roslyn scan must match the STATIC `System.Type.GetType` invocation symbol, not the token text, or it re-counts the false positive]; with the guard's remaining tokens (`Assembly.Load(` ×5 — `AssemblyCompiler.cs:405`, `ReplSession.cs:517,518,531,556`; `new AssemblyLoadContext(` ×1 — `ProjectCompiler.Generators.cs:243`; `AssemblyLoadContext.Default` ×0) the literal at the guard's birth is **31**. Re-counted at the implementation sha and written into Phase 1's guard as the literal anchor.

#### Tasks

1. **Regen control + census pin** — `build_tools/`, scratchpad
   - Run the three regenerations; commit nothing if clean; record the sha and the counts in the findings ledger header.
   - Commit (only if a regen diff appears): `chore(stdlib): regenerate spy-sourced artifacts at P54 base (refs #2250)`

### Phase 1: The harness and the inventory — generator FIRST

**Goal:** every cell of the matrix is enumerated, classified and ratcheted BEFORE a seam is cut; the compile seam is MEASURED, not guessed.

**Acceptance (class-level, measurable):**
- `InProcessIsolationSweepTests` (Compiler.Tests/Conformance, `[Trait("Category","GapDiscovery")]` for the 8-thread × repetitions mode; a regular-suite slice at threads {1, 2} × 1 repetition) enumerates: corpus = {the iter-18 program; the three P47 stems' sources; `main.spy` + `Greeter` library pair; a `from system.io import DriveInfo` program (TPA load); a CLR-attribute program; a stride-16 sample of the single-file fixture corpus} × schedule = seeded sequences over the corpus per thread (`CsCheck`-free: a `Random(seed)` roster, seeds literal) × sharing {per-thread fresh `Compiler`, per-thread fresh `CompilerApi`, ONE shared `CompilerApi`} × threads {1, 2, 8} × operation {`Analyze`, `Compile` (in-memory assembly), code-action Extract (Lsp twin, Phase 1 task 6), applied-text `Analyze`} × repetitions R (8 in GapDiscovery, 1 regular); oracle = the same (program, operation) on a fresh instance alone on one thread, fingerprint = sorted `(code, line, col)` + success + `UsedAssemblyPaths`; buckets `drift` (fingerprint differs from the solo oracle), `crash` (exception escaped), `order` (warm-sequential differs from solo). Reports `.claude/tmp/in-process-isolation-report.json`; ratchets `in-process-isolation-allowlist.txt` (`program sharing threads operation bucket # #issue reason`, drain-on-fix; a probabilistic row records the iteration count it reproduced at, `reproduced k/R @ sha`).
- **The seam probe for cell 1 is a named test, not a hope:** `TpaNamespaceGate_ConcurrentFirstUse_IsComplete` — 8 threads import `system.io` on fresh registries behind a `Barrier`; every thread's `GetNamespaceTypes("system.io")` count must equal the solo count. Expected red @ base (the check-then-act gate); if it is green, the Adversarial Review's second probe (`_originNameDefinitionCache` poisoning) runs and its result is recorded — Phase 2 task 2 is written against whichever is red.
- `ProcessWideResolutionScanTests` (regular suite, Compiler.Tests/Conformance): a Roslyn scan over `src/Sharpy.Compiler/**/*.cs` for `AppDomain.CurrentDomain.GetAssemblies`, static `System.Type.GetType(string)` [CORRECTED: symbol match, not token — `TypeSyntaxMapper.cs:800` is an instance `GetType()`], `Assembly.LoadFrom(`, `Assembly.Load(`, `new AssemblyLoadContext(`, `AssemblyLoadContext.Default` — every site is EITHER in the one visibility file (Phase 2), OR a **roster** row (permanent, with a key-purity justification: the five `Assembly.Load` framework-name/REPL sites and the generator context — see table (a)), OR an **allowlist** row citing #2250 (drain-on-fix) [CORRECTED: two lists, as `StaticStateConformanceTests` has — a key-pure site that never drains cannot sit in a drain-on-fix allowlist without reddening `check_allowlist_issue_state.sh` when #2250 closes]; totality anchored to the literal census from Phase 0 (`Assert.Equal(31, sites)` at birth @ 486c9a88a — 25 resolution sites + 6 rostered, re-anchored as rows drain); positive control: a synthetic in-memory source with one scan is found; mutation: add a scan to any other file → red; a second positive control: the synthetic source with an instance `x.GetType()` is NOT found (the symbol match is what makes the census honest). The Roslyn scan compiles the sources with the compiler's own references and `GlobalUsings` (`reference_roslyn_source_compilation_scan`), or the symbol match silently degrades to null symbols.
- `StaticStateConformanceTests` rule 2 extended per Design Decision 3 — the **22** newly flagged statics (20 `ConcurrentDictionary` + 2 `Lazy`: `CompilerIdentity.s_version`, `ClrExtensionMethodResolver._byName`, both key-pure — a fixed `typeof(Enumerable)` surface) [CORRECTED: grep @ 486c9a88a] each get a row (key-purity justification; the five name-keyed ones cite #2250 "drains in Phase 2"); mutation: delete a row → stale red; add an unrostered static `ConcurrentDictionary` in a scratch type → red.
- `StdlibModuleGlobalRosterTests` + `StdlibModuleGlobalConcurrencyTests` (Stdlib.Tests/Conformance): the roster census (Design Decision 5) finds exactly the six written globals @ base (positive control: literal 6); the concurrency theory per rostered global × operation {write, read, write-then-read-on-another-thread} × threads {1, 2, 8} × 2 000 iterations asserts no exception, value in domain, and the cross-thread read sees the write (`random.seed(7)` on A, `random.random()` on B equals A's re-seeded draw — the CPython/numpy model). Expected reds @ base: `random` (collapse/exception — probabilistic, record k/R), `xml` (Dictionary corruption), `numpy.random` (cross-thread seed), `socket` (tearing is probabilistic on x64 — if 0/R, the row stays a KnownRed cell cited to #2250 with the mechanism named, and the cure lands anyway because the write is not atomic by the CLR memory model).
- `CompileServerE2ETests.RequestSequence_NoRef_Ref_NoRef_AnswersAsThreeFreshProcesses` (Cli.Tests): one server, the three requests; each answer's (exit code, diagnostic code set) equals the fresh-process run of the same command — red @ base on request 3 (`SPY0908` vs `SPY0300`); plus the in-process twin `TwoRegistries_SameNamedAssemblyFromTwoPaths_EachSeesItsOwn` (Compiler.Tests): two `AppTypes.dll` built with different members in two temp dirs; registry A compiles `from App import Alpha`, registry B `from App import Beta`, both must succeed and A must NOT see `Beta` — red @ base (B is SPY0306 and compiles against A's copy, or sees `Beta` missing).
- `CrashBundleExclusivityTests` (Compiler.Tests/Diagnostics): 8 writers × 10 bundles with an injected constant clock → 80 distinct directories, each holding a complete `report`; red @ base deterministically (Design Decision 6's clock injection lands in this phase as a test seam only — default behaviour unchanged).
- `WarmInstanceParityTests` (Lsp.Tests): the three P47 stems × warmth {cold child **`Sharpy.Lsp` process via `LspTestClient`** (the P47 sweep's own oracle; `Sharpy.Lsp.Tests` does not reference `Sharpy.Cli`, so a `sharpyc` child needs a project reference the plan does not add) [CORRECTED], warm-sequential, warm-concurrent DOP 4} × operation {`Analyze`, Extract-method apply, applied-text `Analyze`}; fingerprint parity; expected red @ base on the (12,49)/(12,59) cells; the master-registry key-set snapshot (Design Decision 4) is taken between steps and the first diverging key is printed — that print IS the seam measurement for cells 2–3.
- Guard mutations recorded in each commit body (broken → red N, restored → green N).
- Allowlist entries: every red above is a row citing #2250 with the measured `k/R @ sha`.
- Execution evidence: the sweep report JSON @ sha with per-bucket counts; the named probes' verdicts @ sha.

#### Tasks

1. **Interleaving generator + sweep** — `src/Sharpy.Compiler.Tests/Conformance/InProcessIsolationSweepTests.cs`, `InProcessIsolationCorpus.cs`, `in-process-isolation-allowlist.txt`
   - The corpus roster, the seeded schedule generator, the three sharing modes, the fingerprint oracle, the buckets and the ratchet; `SHARPY_ISOLATION_SWEEP_FULL=1` runs the 8-thread × 8-repetition mode in the regular suite for local use.
   - Acceptance: bullet 1 (matrix enumerated, report written, allowlist populated with #2250 rows).
   - Commit: `test(conformance): in-process isolation sweep — compile/analyse/handler/applied-text × sharing × threads × warmth, ratcheting in-process-isolation-allowlist.txt (refs #2250)`

2. **Cell-1 seam probes** — same directory, `TpaNamespaceGateTests.cs`
   - The barrier probe on the TPA gate and the `_originNameDefinitionCache` poisoning probe (test-only internals: an `InternalsVisibleTo` hook `ModuleRegistry.TestHooks.BeforeTpaLoad`, removed in Phase 2 when the gate becomes a `Lazy`; the hook is a mutable static, so it needs a `StaticStateConformanceTests` allowlist row citing #2250 "removed in Phase 2" for the one commit it lives — or it is passed as a parameter on an internal overload instead, which needs no row [ADDED: missing step]).
   - Acceptance: bullet 2 — the verdict is recorded in the commit body and the findings ledger: "cell 1 seam = <gate | cache | neither>".
   - Commit: `test(registry): the TPA namespace gate and the name-keyed type cache are probed for completion and cross-compile poisoning under 8 threads (refs #2250)`

3. **Process-wide resolution scan guard + static-cache rule extension** — `ProcessWideResolutionScanTests.cs`, `Diagnostics/StaticStateConformanceTests.cs`
   - Acceptance: bullets 3–4; the literal site census; the 22 new rows.
   - Commit: `test(conformance): every process-wide type-resolution site and every static cache is rostered — 25 resolution sites (31 with the rostered key-pure loads) and 22 caches anchored to literals, drained by the visibility seam (refs #2250)` [CORRECTED: counts]

4. **Stdlib roster + concurrency theory** — `src/Sharpy.Stdlib.Tests/Conformance/StdlibModuleGlobalRosterTests.cs`, `StdlibModuleGlobalConcurrencyTests.cs`
   - The Roslyn "written after init" census over `src/Sharpy.Stdlib` (generated files included), the six literal rows, the theory with the CPython model per row (literals verified with `python3`: `random`, `socket`, `calendar`, `xml`, `logging`; numpy 2.5.1 for `numpy.random`).
   - Acceptance: bullet 5.
   - Commit: `test(stdlib): module globals are rostered by a written-after-init census and exercised at 1/2/8 threads against CPython's process-wide model (refs #2250)`

5. **Server request-sequence row + two-registry twin** — `src/Sharpy.Cli.Tests/E2E/CompileServerE2ETests.cs`, `src/Sharpy.Compiler.Tests/Project/RegistryIsolationTests.cs`
   - Acceptance: bullet 6 (both red @ base, direction recorded).
   - Commit: `test(server): {no -r, -r, no -r} on one server must answer as three fresh processes; two registries loading same-named assemblies from two paths each see their own (refs #2250)`

6. **Crash exclusivity test + clock seam; LSP warm-parity theory** — `CrashBundleWriter.cs` (internal clock parameter only), `src/Sharpy.Compiler.Tests/Diagnostics/CrashBundleExclusivityTests.cs`, `src/Sharpy.Lsp.Tests/Conformance/WarmInstanceParityTests.cs`, `Project/MasterRegistryImmutabilityTests.cs` (key-set snapshot + request sequence)
   - Acceptance: bullets 7–8.
   - Commit: `test(diagnostics,lsp): crash bundles under 8 concurrent writers at one clock tick; warm-sequential and warm-concurrent CompilerApi parity against a cold process on the P47 stems, with the master-registry key-set snapshot between steps (refs #2250)`

7. **CI step for the new GapDiscovery sweep** — `.github/workflows/dotnet10.yml`
   - A sixth sweep job `Category=GapDiscovery&FullyQualifiedName~InProcessIsolation`, excluded from `COMPILER_MAIN_FILTER` by name like its siblings; `CiFilterCoverageConformanceTests` must still pass (it pins the filter roster — extend it).
   - Acceptance: the CI run at the Phase 1 sha shows the step; the local whole-solution run covers it.
   - Commit: `ci: run the in-process isolation sweep as its own GapDiscovery step (refs #2250)`

### Phase 2: The compiler seams — visibility, isolation, frozen masters

**Goal:** a compile sees exactly `{framework} ∪ {its own references}`; two compiles never share a mutable cache whose value depends on load order; the master registries are frozen and key-pure.

**Acceptance:**
- `in-process-isolation-allowlist.txt` rows for compile × compile, compile × module load, warm-sequential and warm-concurrent drain to empty (GapDiscovery mode, R = 8, 0 drift @ sha); `ProcessWideResolutionScanTests` census drains to the visibility file's own sites (literal re-anchored); the five name-keyed static rows drain from `StaticStateConformanceTests`.
- `RequestSequence_NoRef_Ref_NoRef…` green: request 3 is SPY0300; `TwoRegistries_SameNamed…` green; `ModuleNamespaceShadowsClrTypeTests` records no SPY0306; `MissingReferenceDiagnostics()` returns every registry error and no door filters it (R-HI — P42 then widens the doors that today read only SPY0305).
- `WarmInstanceParityTests` green at every cell; `MasterRegistryImmutabilityTests` key-set snapshot clean through the P47 request sequence.
- Guard mutations: revert the visibility seam's filter to the raw `AppDomain` scan → `TwoRegistries…` and the server row red; restore the `TryAdd` gate → `TpaNamespaceGate…` red within R; make `_clrTypeCache`'s factory read the default context again → `WarmInstanceParity` red (the measured cell).
- Warm-compile cost measured before/after (Design Decision 3): `CompileServerE2ETests` phase millis and the Benchmark-traited compile fixtures @ both shas, in the commit body.
- Direction record: no program newly refused (the gate + `FrontEndParityTests` + the P47 pins are the controls).

#### Tasks

1. **`AssemblyVisibility` — the one seam** — `src/Sharpy.Compiler/Discovery/AssemblyVisibility.cs` (new), `Semantic/Registry/ModuleRegistry.cs`, `Discovery/CachedModuleDiscovery.cs`, `Discovery/ClrTypeHelper.cs`, `Discovery/ClrAttributeResolver.cs`, `Semantic/Registry/BuiltinRegistry.cs`, `Semantic/TypeChecker.Expressions.Access.Calls.cs`, `Semantic/TypeChecker.Utilities.cs`
   - The framework set as a process `Lazy`; the registry's own set; `ResolveType`, `TypesInNamespace`, `NamespaceExists`, `EnsureFrameworkAssembliesFor(ns)` (the TPA gate as `ConcurrentDictionary<string, Lazy<bool>>` — the completion barrier); every scan site in the files listed routes through the compilation's visibility (threaded from `ModuleRegistry` → `CachedModuleDiscovery`/`ClrAttributeResolver` constructors; the `TypeChecker` reads it from its registry; `ClrTypeHelper`'s two name-keyed statics become methods on the visibility). The emitter's two sites (`RoslynEmitter.CompilationUnit.cs:590`, `TypeSyntaxMapper.cs`) route through `AssemblyVisibility.FrameworkOnly` — explicitly process-stable — with a comment naming the Rule-2 cell (the emitter asking a semantic question; ledger row).
   - Acceptance: scan census drains; `TpaNamespaceGate…` green; cell-1 program 0 drift at 8 threads × 8 repetitions.
   - Commit: `fix(discovery): ONE assembly-visibility seam — every CLR name resolves against {framework} ∪ {this registry's references}, the TPA namespace gate completes before a second compile reads it, the name-keyed type caches move off the process (refs #2250)`

2. **Isolated load context per registry; SPY0306 keeps one meaning** — `ModuleRegistry.cs`, `Discovery/ReferenceLoadContext.cs` (new), `Discovery/ClrAttributeResolver.cs:473`, `Discovery/Caching/AssemblyIdentity.cs:28`
   - `LoadFromAssemblyPath` on the registry's collectible context; `Load` → own set by simple name, else null; `Resolving` → reference directory + module paths; `AssemblyIdentity.FromAssemblyPath` is DELETED — it has zero callers src-wide @ 486c9a88a [CORRECTED: was "reads identity from `AssemblyName.GetAssemblyName`"; a dead helper is not rewritten, contract §8]; the SPY0305-only filter at `:52` is deleted (the `IOException`/`BadImageFormatException`/`UnauthorizedAccessException` catches stay — see Design Decision 2); `MissingReferenceDiagnostics()` renamed `ReferenceDiagnostics()` returning every SPY0305/SPY0306; its four production callers (`CompilerApi.cs:136`, `:477`, `:531`, `Compiler.cs:138`) and `RegistryErrorDoorTests` updated with no behaviour change beyond R-HI's [CORRECTED: callers named].
   - Acceptance: `TwoRegistries…`, the server row, `ModuleNamespaceShadowsClrTypeTests` as stated; `Type`-keyed static caches gated on the default context (Design Decision 3) with the retention probe `CompileServer_FiftyRequests_LoadContextsCollect` (Cli.Tests: after 50 requests + GC, `AssemblyLoadContext.All` count is bounded — recorded as a measured number, a ceiling pinned).
   - Commit: `fix(registry): each compilation loads its references into its own AssemblyLoadContext — a same-named assembly from another path is its own copy, SPY0306 means only "cannot be loaded as given", and every registry error reaches every door (R-HI, refs #2250, refs #2182)`

3. **Frozen master registries** — `BuiltinRegistry.cs`, `CompilerApi.cs`, `Project/MasterRegistryImmutabilityTests.cs`
   - `_pendingInterfacePopulation` drained in the ctor; `_interfaceSymbols` → `ConcurrentDictionary.GetOrAdd`; `_clrTypeCache`'s factory on `AssemblyVisibility.FrameworkOnly`; the `SymbolVendingRoster` rows updated; `CompilerApi`'s cache entry documents the frozen contract.
   - Acceptance: `WarmInstanceParityTests` green; key-set snapshot clean; the measured seam from Phase 1 task 6 named in the commit body.
   - Commit: `fix(registry): the master BuiltinRegistry is frozen after construction and every lazily built entry is a function of its key — a warm CompilerApi answers as a cold process (refs #2250)`

4. **Tooling statics** — `src/Sharpy.Cli/CliHelpers.cs`, `Commands/BuildCommand.cs`, `Services/CompileServer.cs`
   - `CompileToBinary` returns its failure exit kind; `LastFailureExitCode` deleted; `ShowDiagnosticProvenance` rostered in a Cli twin of the static-state rule (one `Fact` over the Cli assembly with the one allowed row).
   - Commit: `fix(cli): the failure exit kind is returned by CompileToBinary, not read from a process global after the fact (refs #2250)`

5. **Retire the P47 sweep's isolation workaround** — `src/Sharpy.Lsp.Tests/Conformance/CodeActionShapeSweepTests.cs`
   - `Isolated()` → one shared `CompilerApi` per sweep again; the pins in `code-action-shape-allowlist.txt` must be byte-identical (count + digest) in regular and FULL mode; sweep time recorded before/after.
   - Acceptance: pins unchanged @ sha (the direction control for the whole phase).
   - Commit: `test(lsp): the code-action sweep shares one CompilerApi again — pins byte-identical, 2× faster (refs #2250, refs #2232)`

6. **Warm-compile cost record** — commit body only (no code): the Design-Decision-3 numbers, or the per-registry-cache fallback if the regression exceeds noise (then its own commit `perf(discovery): …`).

7. **Regen after the visibility seam** [ADDED: missing step — contract §7: the round's first codegen-REACHING commit is followed by a regen] — `build_tools/regenerate_spy_stdlib.sh`, `build_tools/regenerate_spy_tests.sh`, `python -m build_tools stdlib generate --force`, `build_tools/check_spy_staleness.sh` + `check_spy_tests_staleness.sh` directly after task 2: a changed CLR-name resolution ORDER (Design Decision 1 option A loads every TPA assembly; option B changes which assembly answers a bare name first) can change which `Type` a stdlib `.spy` name binds to and therefore the generated C#. "regen: no diff @ <sha>" or the diff read as findings (a changed binding is a #2175 cell — ledger row, not silently committed).
   - Commit (only if a diff appears): `chore(stdlib): regenerate spy-sourced artifacts after the visibility seam (refs #2250)`

### Phase 3: Stdlib globals, crash bundles, the loopback test

**Goal:** every rostered stdlib global is thread-safe under CPython's process-wide model; a crash bundle directory is created exclusively; the socket test cannot flake on a re-taken port.

**Acceptance:**
- `StdlibModuleGlobalConcurrencyTests` green for all six rows (0 exceptions, in-domain values, cross-thread seed visible) at 8 threads × 2 000 iterations; the roster still finds exactly six written globals (+ the two locks, which are init-once and excluded by the predicate).
- Regenerated `Random.cs` / `SocketModule.cs` committed in the SAME commit as the `.spy` change; `check_spy_staleness.sh` green; the diff read: one static lock, `with` lowered to a `lock`-equivalent — no other change.
- `CrashBundleExclusivityTests` green: 80 directories, 80 complete reports; mutation (restore `Directory.Exists` check-then-create) → red deterministically under the injected clock.
- `socket_module_tests.spy` regenerated; the retry helper's positive control: a port held LISTENING is "accepted" (the helper does not mask a genuine accept beyond its five retries).
- Guard mutations recorded; direction: `random`/`socket` sequences unchanged single-threaded (the existing `PrngState` tests pin the seeded sequence).

#### Tasks

1. **`random` and `socket` locks (rung 1) + regen** — `src/Sharpy.Stdlib/spy/random_module.spy`, `spy/socket_module.spy`, regenerated `Random/Random.cs`, `Socket/SocketModule.cs`
   - Commit: `fix(stdlib): random and socket module globals are read and written under one module lock, as CPython's are under the GIL — regenerated (refs #2250)`

2. **`xml` and `numpy.random` (rung 2)** — `Xml/XmlFunctions.cs`, `Numpy/NumpyRandom.cs`
   - Direction (contract §3): `NumpyRandom.cs:12-15` DOCUMENTS the per-thread generator as a design choice ("matches the safer .NET model"); grep `src/Sharpy.Stdlib.Tests` and `docs/` for a test or doc sentence pinning per-thread seeding before dropping `[ThreadStatic]` — a pin is updated in the same commit with the numpy 2.5.1 measurement (`np.random.seed(7)` on the main thread, a worker's draw equals the re-seeded main draw — re-verified @ 486c9a88a) as the reason; the stdlib docs regenerate (the doc comment changes). [ADDED: missing step]
   - Commit: `fix(stdlib): xml.register_namespace and numpy.random share one process-wide, locked generator/registry — a seed on one thread is seen by every thread, as numpy's global RandomState is (refs #2250)`

3. **Crash bundle exclusivity** — `Diagnostics/CrashBundleWriter.cs`
   - Commit: `fix(diagnostics): a crash bundle directory is claimed with a create-exclusive marker and named with the process id — two crashes in one millisecond get two bundles (refs #2250)`

4. **Loopback retry** — `src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy` + regen
   - Commit: `test(stdlib): the closed-port socket cells retry a re-taken ephemeral port instead of asserting on it (refs #2250)`

### Phase 4: Drain, document, close

**Goal:** allowlists empty, the harness rostered, the issue closed on evidence.

**Acceptance:**
- `in-process-isolation-allowlist.txt` EMPTY; `StaticStateConformanceTests` rows only for key-pure caches; `ProcessWideResolutionScanTests` census = the visibility file's sites; every Phase 1 red green; `check_allowlist_issue_state.sh` green.
- `docs/design/gap-discovery-contracts.md` gains the class row (contract, harnesses, umbrella #2250); `docs/architecture.md` › "Semantic Analysis Pipeline" › the **Key registries** paragraph (line 39 @ 486c9a88a; there is no such heading) notes the visibility seam and the per-registry load context [CORRECTED]; `CLAUDE.md` Critical Rules unchanged (the static-state rule is already Rule-adjacent via `StaticStateConformanceTests`; no new rule).
- Whole-solution gate @ sha (`passed/failed/skipped @ sha (measured)`), the CI run with the sixth sweep step green.

#### Tasks

1. **Docs + roster row** — `docs/design/gap-discovery-contracts.md`, `docs/architecture.md`
   - Commit: `docs(design): roster the in-process isolation class — visibility seam, isolated load context, stdlib global roster, crash exclusivity (refs #2250)`

2. **Close-out** — #2250 comment: each acceptance bullet with `what ran @ sha`; the direction record; the retention and warm-cost numbers; `/close-issues` after the gate.

## Testing Strategy

- **Generator first (contract §11):** `InProcessIsolationSweepTests` enumerates the compile matrix from rosters and seeded schedules; its allowlist is the ratchet; the axis it cannot enumerate — WHICH static is the seam — is the reflection/Roslyn inventory (`ProcessWideResolutionScanTests`, the extended `StaticStateConformanceTests`, `StdlibModuleGlobalRosterTests`), each totality-anchored to a literal count so a new site or a new global is red, never silently absent. Hand matrices exist only for the stdlib globals (six literal rows × three operations) and the crash writer (one deterministic theory).
- **Probabilistic cells are measured, not asserted once:** every race row records `reproduced k/R @ sha`; a drained row's proof is the mutation (revert the seam → k > 0 within R), never a single green.
- **Oracles that discriminate:** the fingerprint includes positions and `UsedAssemblyPaths`, not just counts (cell 1 was a COUNT difference; cells 2–3 a POSITION difference; cell 4 an EXIT-CODE difference); the stdlib theories assert the cross-thread VALUE (a seed seen from another thread), not "no exception" alone — "no exception" alone passes on a torn `double?` write.
- **Positive controls for every absence assertion:** the scan guard's synthetic source with one site; the roster census's literal counts; the loopback helper's LISTENING port; `TwoRegistries…`'s "A must NOT see `Beta`" is paired with "A sees `Alpha`".
- **Direction (contract §3):** cell 4 and cell 5 are the only diagnostic-set changes; both are ICE/silent-wrong → correct answer; the gate, `FrontEndParityTests`, the P47 pins and `WarmBuildDiagnosticFidelityTests` are the "nothing that worked is refused" controls.
- **Blast radius:** whole-solution gate once per phase close (Compiler/Stdlib/Cli/Lsp/Core tests + the five CI sweeps + the new one) [CORRECTED]; spy-stdlib regen in Phase 3 task 1's commit; stdlib docs regen checked (no visible change expected); `check_spy_staleness.sh`, `check_allowlist_issue_state.sh` at `/push`.
- **No language change:** no spec section; `docs/architecture.md` and the gap-discovery roster are the doc deliverables (executed examples: the harness report @ sha).

## Issues to Close

- #2250 — process-wide mutable state is not safe under concurrent in-process use — closed by Phase 4, Task 2; **close criterion:** `in-process-isolation-allowlist.txt` empty with the GapDiscovery sweep at R = 8 showing 0 drift @ sha; `RequestSequence_NoRef_Ref_NoRef…` green (request 3 = SPY0300); `TwoRegistries_SameNamed…` green; `WarmInstanceParityTests` green on the three P47 stems; `StdlibModuleGlobalConcurrencyTests` green on six rows; `CrashBundleExclusivityTests` green; the P47 pins byte-identical after the workaround is retired; whole-solution gate `passed/0/skipped @ sha (measured)`; the retention and warm-cost numbers posted. The dependency R-HI records (P42's door widening) is satisfied by Phase 2 task 2 and noted on #2182.

## Class trackers this plan may post cells to

- #2250 — this class — cells found inside the matrix are the plan's work; cells of the same contract outside the matrix (e.g. the REPL's `ReplSession` instance, #2310/P72's domain) go here as comments.
- #2182 — assembly loading (unloadable types, transitive references, not-an-assembly) — the per-registry `Resolving` arm's "resolves nowhere" behaviour is R-DN phase 1 and stays there; P42 reads this plan's SPY0306 change.
- #2313 — Cli residue — the CLR-namespace import arms not recording `UsedAssemblyPaths` (its cell 4) is the half of cell 4's CS0400 this plan does NOT cure (P46b's); any deployment cell the server row exposes goes there.
- #2175 / #2064 — CLR name resolution by fallback namespace / bare-name fallback — a resolution ORDER cell the visibility seam exposes (two framework assemblies declaring one full name) goes to #2175.
- #2114 — a changed referenced assembly across warm builds — a mtime/identity cell on the per-registry context goes there.
- #2309 / #2301 — LSP project-mode residue — an editor cell the warm-parity theory exposes that is about the DOCUMENT, not the instance, goes there.
- Rule-2 emitter reflection (`RoslynEmitter.CompilationUnit.cs:590`, `TypeSyntaxMapper.cs`): no open tracker — a ledger row; the lead files ONE class issue (lane:tooling, reach:probe) if the cell is confirmed outside this contract. No implementer runs `gh issue create`.

## Implementation Checklist

Base sha: 486c9a88a. Baseline gate: 80185/0/4 @ 3f16f261e (code-identical to 486c9a88a; measured 2026-10-14 16:58–18:48). Design Decision 1: **option A** (one-time full TPA index) chosen by the lead under autonomous operation, per the plan's recommendation; recorded again in the Phase 2 task 1 commit body. Findings ledger: session scratchpad `findings-ledger.md`.

- [x] P0.1 Regen control + census pin — lead — regen: no diff @ 486c9a88a
- [x] P1.1 Interleaving generator + sweep (+ P1.7 CI step) — test-expert "sweep" — 97d0b5d5a, 385199b0f (0 reds: GapDiscovery 2×9216 obs, regular 4×432; oracle-first warms the process, so first-use races are the probes lane's; allowlist empty)
- [x] P1.2 Cell-1 seam probes (internal-overload hook, no static) — semantic-expert "probes" — e0a2f5fd6 (verdict: cell 1 seam = NEITHER — see note below; TPA gate passes-before-completion 7/7 and origin-cache poisoning 8/8 are real KnownRed members of the class)
- [x] P1.3 Resolution scan guard + static-cache rule extension — test-expert "census" — cherry-picked from cd2d98a5b (31 sites / 22 caches measured, green, no KnownRed)
- [x] P1.4 Stdlib roster + concurrency theory — stdlib-expert "stdlib" — cherry-picked 083029bcb from 4fe96521e (census = 8 written statics: plan's 6 + HTTPStatus._valueMap, TypedLoadContract.FieldCache; numpy.random 7/7 red, random/xml probabilistic, socket 0/7)
- [x] P1.5 Server request-sequence row + two-registry twin — test-expert "server" — 3c1ea0d02 (request 3 ICE CS0400; B/Beta wrong-refusal; B/Alpha ICE CS0234 — KnownRed rows)
- [x] P1.6a Crash exclusivity test + clock seam — test-expert "crash" — cherry-picked from bb3317c5e (2 writers 1000/1000 red, 8 writers 300/300 red @ 486c9a88a; KnownRed)
- [x] P1.6b LSP warm-parity theory + master key-set snapshot — lsp-expert "warm" — e3a987e09 (seam = master builtin FunctionSymbol.Parameters WRITE: analysing a text defining `def len`/`def format` inside `try:` retypes the master `len`/`format`; key sets are a fixed point; _clrTypeCache NOT the seam. P47 sweep green @ 486c9a88a only because e3c5cbdf6 stopped offering Wrap-in-try over a declaration; the two trigger texts reproduce every P47 cell)
- [x] P1 gate — lead — 80313 passed / 0 failed / 4 skipped @ e3a987e09 (measured; Core 3989, Stdlib 4273, Cli 887/1 skip, Lsp 21937, Compiler 49227/3 skip; baseline 80185/0/4) (all seven P1 lanes merged; lead re-ran the stdlib numpy.random/read/8 mutation: row removed → 1 failed, restored → 83 passed)
  - **Cell-1 note (implementation finding, 2026-10-15):** cell 1's "5 vs 1" @ af6f30901 was a CANCELLATION artifact, not process state: at that sha the fuzz test's timeout was 2000 ms and cancelled pairs were not skipped; a pre-cancelled compile of the iter-18 program yields exactly 1 diagnostic (SPY0901). 4562b1d01 (#2278) raised the timeout to 30 s and added the skip. The Adversarial Review's exclusion was read @ 486c9a88a, not @ af6f30901. With no timeout, 8 threads × 50 reps × 2 runs: 0/800 pairs differ @ 486c9a88a. The iter-18 program never enters the TPA gate (it imports `snake_case`). Phase 2 still cures the gate and the name cache as members of the class.
- [x] P2.1 AssemblyVisibility seam (option A) — semantic-expert "seam" — 333de078b (framework index 205 assemblies / 6266 types / 61 ms first use; 25 allowlisted sites drained; TPA gate + 5 name-keyed static caches gone; BuiltinRegistry fallback reads the PAIRED registry visibility, not FrameworkOnly — FrameworkOnly refused StatisticsError/CapturedOutput/… (#2053/#2064), lead ruling); 535f5a257 drained server row 3
- [x] P2.2 Load context keyed by REFERENCE SET (lead ruling: one context per registry OOMed FrontEndParity at 13m51s; keyed share 1m13s vs 1m14s) — 32ea5428f (SPY0305-only filter deleted, ReferenceDiagnostics, FromAssemblyPath deleted; FiftyRequests probe 0 static-cache keys, 0/50 contexts alive)
- [x] P2.7 Regen after the seam — lead — regen: no diff @ 32ea5428f; 4fe1ca67b fixes the Random/Socket module csprojs (CS0234 Lock — ff610198f never built the sln)
- [x] P2.3 Frozen master registries — semantic-expert "frozen" — cbabbf73a (seam: a def nested in a module/class-level block had no own symbol and wrote the master len/format; now every def declares its own symbol, master symbols frozen after ctor (setters throw PhaseViolationException), Parameters writes via ReplaceParameter + source scan; warm-parity rows drained, 0/12 per concurrent cell over 3 runs) — SEAM MEASURED in P1.6b: a master builtin FunctionSymbol.Parameters WRITE when a user text defines `len`/`format` inside `try:` (not dictionary growth); the cure must stop a user definition from writing a master builtin symbol (the #1633 shape), and the key-set freeze alone would be INERT
- [x] P2.3b — semantic-expert "frozen" — 59a51d513 — (in-contract, from the ledger) StaticStateConformanceTests rules 1/2 also flag static auto-property backing fields (today skipped by IsCompilerGenerated) — blocked by nothing
- [x] P2.4 Tooling statics (Cli) — lead — 1a41b05ad
- [x] P2.5 Retire Isolated() in the P47 sweep — lead — 62558b7f8 (pins byte-identical regular + FULL; regular 1m29s → 46s; FULL 8m08s)
- [x] P2.6 Warm-compile cost record — lead — effba7776 (empty commit; within noise on every row; DD3 fallback not taken)
- [x] P3.1 random/socket locks + regen — stdlib-expert "globals" — ff610198f (spelling `from threading import Lock`: `from sharpy import Lock` lowers `with` to `using` = inert lock → filed #2315; one init-once lock field per module, not readonly — spy bindings are public static non-readonly by the emitter)
- [x] P3.2 xml + numpy.random — stdlib-expert "globals" — dae06b7f6 (numpy rows stale 8/8 then drained; [ThreadStatic] dropped with the numpy 2.5.1 measurement as reason; docs/stdlib/numpy.md regenerated)
- [x] P3.3 Crash bundle exclusivity — lead — a5845224e
- [x] P3.4 Loopback retry — lead — 28235b580
- [x] P4.1 Docs + roster row — lead — b7ff7951d
- [x] Final verification (lead gate + standalone verification-expert) + dotnet format whitespace — gate 80324 passed / 0 failed / 4 skipped @ effba7776 (measured; Core 3989, Stdlib 4273, Cli 887/1, Lsp 21937, Compiler 49238/3); format --verify-no-changes clean; check_allowlist_issue_state OK; spy/spy-tests staleness OK; stdlib docs no diff; verifier pending
- [x] V-F3 (verifier, in-contract) framework set = shared-framework directory + Sharpy.Core, never host packages — lane "framework" — f5fb7f8e4. DIRECTION CHANGE — OWNER CONFIRMED 2026-10-15 ("i agree with what you decided"): an UNUSED `from system.reactive.linq import Observable` compiled at base (SPY0452 warning; any USE crashed at run) and is now SPY0301 — its base answer depended on sharpyc shipping System.Reactive (host-dependent, the contract's violation); microsoft.extensions.primitives StringValues back to SPY0300 (base); code_analysis/command_line imports exit 134 → refused
- [x] V-F1/F2 (verifier, in-contract) type definitions in a block declare their own symbol; frozen master collections — lane "frozen2" — 7ae6ed761 (cherry-pick of f78db52cf). A type definition in a function body or a block is REFUSED SPY0202 (C# has no local types; base: SPY0202 / SPY0340 / SPY0510 ICE — no base-worked program refused, lane direction table). Lead re-ran the collection-freeze mutation: broken → red (2 failed, 15 passed), restored → green (17/0/0); a first attempt with `{ }` was a CS0649 BUILD error, not a test red, and was redone with a compiling no-op.
- [x] Final gate re-run + verifier re-check of F1–F3 — gate 80349 passed / 1 failed / 4 skipped @ 7ae6ed761 (measured; Core 3989, Stdlib 4273, Cli 897/1, Lsp 21938, Compiler 49252/1/3); the red = CallTimingCoverageTests wall-clock ratio 0.930 < 0.95 under full-suite load, 3/3 green alone and Compiler.Tests re-run 49253/0/3 @ 7ae6ed761 (measured). Format verify clean, allowlist state OK, spy/spy-tests staleness OK, stdlib docs no diff. Verifier re-check (opus, report-verify2.md): F1 FIXED, F2 FIXED, F3 FIXED, no base-worked program refused; outside-contract cells → #2185/#2042 comments, #2316, #2317 filed
- [x] P4.2 Close-out comment on #2250 — issuecomment-6194337667; #2182 R-HI note issuecomment-6194338276. #2250 NOT closed: its criterion's CI bullet (the sixth sweep step) needs the push of dev

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-14 @ 486c9a88a (one `.gitignore` commit after the plan's 3f16f261e; every code fact re-read at HEAD)
**Plan file:** `.claude/plans/plan-9ef825.md` (pre-verification copy at `$TMPDIR/plan-9ef825.pre-verify.md` for this session)

### Corrections Made
- **Site census** (Phase 0, Phase 1 bullet 3, task 3 commit, table (a)): `Type.GetType(` 8 → **7** — `TypeSyntaxMapper.cs:800` is `topLevelType.GetType().Name` in a diagnostic string, not a resolution site; the three-token total is 25, the guard's full-token literal 31 (five `Assembly.Load(` + one `new AssemblyLoadContext(`); the scan must match the static `System.Type.GetType` SYMBOL, with an instance-`GetType()` negative control.
- **Scan guard shape** (Phase 1 bullet 3): one list → **roster + allowlist** — key-pure sites (framework-name `Assembly.Load`, the REPL's own submissions, the generator context) are permanent justified rows; a drain-on-fix row citing #2250 for them would redden `check_allowlist_issue_state.sh` when #2250 closes.
- **Static caches** 20 → **22** (two `Lazy`: `CompilerIdentity.s_version`, `ClrExtensionMethodResolver._byName`, both key-pure).
- **Design Decision 1** gains the constant-framework-set requirement: three sites answer namespace-less queries (`ClrTypeHelper.cs:452`, `ModuleRegistry.cs:612`, `TypeChecker.Utilities.cs:1980`); a visibility that scans "framework assemblies loaded so far" stays order-dependent for them. Two options recorded (A: one-time full TPA index, recommended; B: candidate-namespace ensure per site); the implementer records the choice.
- **Design Decision 2 / Phase 2 task 2**: there is no `FileLoadException` arm in `LoadReference` (catches are `IOException` `:123`, `BadImageFormatException` `:128`, `UnauthorizedAccessException` `:133`; all stay); only the `:52` SPY0305 filter is deleted. `AssemblyIdentity.FromAssemblyPath` has **zero callers** → deleted, not rewritten. The four `MissingReferenceDiagnostics` callers are named (`CompilerApi.cs:136/477/531`, `Compiler.cs:138`) plus `RegistryErrorDoorTests`.
- **Design Decision 3**: the default-context gate walks generic arguments / element / declaring types (a `List<Greeter>` has a Core `t.Assembly` and pins Greeter's context).
- **Design Decision 5**: `threading.Lock()` → `from sharpy import Lock` (the spelling every spy stdlib module uses; `Lock` is in `Sharpy.Stdlib/Threading/Lock.cs`, namespace `Sharpy`, with `Enter`/`Exit` + dunders); the "written after init" census is spelled as a syntactic rule with its positive control.
- **Design Decision 6**: `CrashBundleWriter` is a `public static class` — the clock is an internal `TryWrite` overload, not a ctor parameter (a settable static hook would be a rule-1 violation).
- **Design Decision 7**: `_assert_refused` does not exist; the two cells are `test_connect_with_timeout_refused_raises_error_not_timeout` (`:591`) and `_assert_timeout_rule`'s `create_connection` arm (`:738`); one new helper, positive control and mutation named.
- **Cell 9 (`xml`)**: `_registeredNamespaces` is **write-only** — `Tostring` never reads it; the theory reads the field; the "tostring ignores registered prefixes" fidelity cell is a ledger row outside this contract.
- **Cell 10 (`logging`)**: `Logger._level` is an instance field; `_loggers` is `LoggingModule.cs:27`.
- **Phase 1 bullet 8**: the cold oracle is a child `Sharpy.Lsp` process via `LspTestClient` (Lsp.Tests has no `Sharpy.Cli` reference).
- **Charter §3.21 → §3.30** (P42 / R-HI); **three CI sweeps → five** (+ the new sixth); `docs/architecture.md` "§Key registries" → the bold paragraph at line 39.

### Adequacy
- **Contract:** named — a compile and every semantic query on its result is a function of its inputs under {1, 2, 8} threads × {cold, warm-sequential, warm-concurrent} × {fresh, long-lived process}; CPython-safe stdlib module functions are safe; per-event artefacts are created exclusively.
- **Matrix:** larger than the repro lists — 13 cells incl. two the plan's own survey added (`numpy.random`, the server sequence). Verifier-found cells now in the table: `AssemblyIdentity.FromAssemblyPath` (dead LoadFrom site), the six key-pure load sites (roster rows), the three namespace-less resolution sites (Decision 1 gap). Checked and NOT cells: test execution is a child process (`Sharpy.TestInfrastructure/Integration/IntegrationTestBase.cs:906`); `ClrExtensionMethodResolver._byName` is a fixed `typeof(Enumerable)` surface; cancellation under load rethrows to `CompilationCancelled` and `WasCancelled` skips it.
- **Cure:** seam + sweep + harness — one `AssemblyVisibility` seam, a totality-anchored scan guard, the isolation sweep as ratchet (meta-class rows: "Warm ≠ cold divergence" + "Position/route missing a shared check").
- **Harness:** "Phase 1 adds one" — `InProcessIsolationSweepTests` (+ allowlist `in-process-isolation-allowlist.txt`), `ProcessWideResolutionScanTests`, the extended `StaticStateConformanceTests`, `StdlibModuleGlobalRosterTests`/`…ConcurrencyTests`, the server row, `CrashBundleExclusivityTests`, `WarmInstanceParityTests`; allowlist delta: rows added in Phase 1 citing #2250, drained in Phases 2–3, empty at Phase 4.
- **Layer:** every decision rung-justified — Decisions 1–4 rung 3 (CLR identity, no builtin name; no lower rung expresses assembly visibility); Decision 5 rung 1 for spy-sourced modules, rung 2 for hand-written C#; no rung-4 rule introduced.

### Scope & convergence
- **Group:** one (P54, #2250) · **Alone:** no — required? no (no printed-output or layout change; the two diagnostic-set changes are ICE/silent-wrong → correct, direction-verified) · **Gates:** 1 per phase close, whole solution.
- **Generator:** built first in Phase 1 (`InProcessIsolationSweepTests`: corpus × seeded schedule × sharing × threads × operation × repetitions, fingerprint oracle, allowlist ratchet); the hand axis is named (WHICH static is the seam → the inventory guards; the six stdlib globals × three operations).
- **Trackers named:** #2250, #2182, #2313, #2175/#2064, #2114, #2309/#2301, + the Rule-2 emitter cell as a ledger row; "No implementer runs `gh issue create`" stated · **Fidelity rows:** not a Python-parity surface (the CPython comparisons are the thread-safety MODEL, compared as values and re-verified: `random.__doc__` "threadsafe", cross-thread seed equality in `random` and numpy 2.5.1, `ConnectionRefusedError` vs `TimeoutError` on macOS).

### Falsifiability
- `InProcessIsolationSweepTests`: mutation [named: revert the visibility filter / restore the `TryAdd` gate → drift rows reappear within R]; exemption [not subject — the allowlist is keyed by (program, sharing, threads, operation, bucket) and drains]; positive control [the Phase 1 reds themselves, `k/R @ sha`]; by-direction [n/a — no refusal].
- `TpaNamespaceGate_ConcurrentFirstUse_IsComplete`: mutation [named: restore the `TryAdd` gate → red within R]; positive control [solo count]; inert-fix check [the second probe (`_originNameDefinitionCache` poisoning) runs if the first is green — recorded].
- `ProcessWideResolutionScanTests`: mutation [named: add a scan to any other file → red]; exemption [not subject — the visibility file is the one allowed home; the roster rows are literal]; positive control [synthetic source with one scan found; ADDED: synthetic instance `GetType()` NOT found].
- `StaticStateConformanceTests` rule-2 extension: mutation [named: delete a row → stale red; add an unrostered static `ConcurrentDictionary` → red].
- `StdlibModuleGlobalRosterTests`: mutation [named: delete a row → stale red; add a written static → red]; positive control [literal 6; `Random.Seed`'s assignment].
- `StdlibModuleGlobalConcurrencyTests`: mutation [named: remove a lock → `random`/`xml` red within R; the `socket` row may be 0/R — recorded as a KnownRed cell with the mechanism]; positive control [the cross-thread VALUE assertion, not "no exception"].
- `RequestSequence_NoRef_Ref_NoRef…` / `TwoRegistries_SameNamed…`: mutation [named: revert the seam → red]; by-direction probe [specified: request 3 ICE → SPY0300; `ModuleNamespaceShadowsClrTypeTests` SPY0306-then-succeed → clean]; positive control ["A sees Alpha" beside "A must NOT see Beta"].
- `CrashBundleExclusivityTests`: mutation [named: restore `Directory.Exists` check-then-create → red deterministically under the injected clock].
- `WarmInstanceParityTests`: mutation [named: `_clrTypeCache` factory reads the default context again → red on the measured cell]; positive control [cold oracle = child `Sharpy.Lsp` process].
- Loopback retry helper: mutation [ADDED: a pre-listened first probe port exercises the retry arm]; positive control [LISTENING port → "accepted"].

### Warnings
- **Design Decision 1 needs the author's option (A/B) before Phase 2 task 1** — the namespace-less sites decide whether "framework visibility" can be a process constant; option A's one-time TPA-index cost is measured in Phase 2 task 6 and the compile server / LSP amortise it; option B leaves every future namespace-less query a new cell.
- **`socket._DefaultTimeout` tearing is probably 0/R on arm64/x64** — the plan already lands the cure regardless; keep the row's mechanism text honest ("not atomic by the CLR memory model"), never "reproduced".
- **The `numpy.random` change reverses a documented design choice** (`NumpyRandom.cs:12-15`); the direction record and any pinning test/doc are now a Phase 3 task 2 bullet.
- **Regen after Phase 2 task 2 is now a task (2.7)** — a resolution-order change can rebind a stdlib `.spy` name; a diff there is a #2175 ledger row, not noise.
- **`WarmInstanceParityTests` under `maxParallelThreads` 8 in Lsp.Tests** — the DOP-4 arm plus seven other concurrent classes is a different load than the P47 sweep's; record the thread count the (12,49)/(12,59) cells reproduce at.

### Missing Steps Added
- Phase 2 task 7: regenerate spy-stdlib C#, spy-test C#, stdlib docs + staleness checks directly after the visibility/load-context commits (contract §7).
- Phase 1 task 2: the `TestHooks.BeforeTpaLoad` static needs a `StaticStateConformanceTests` row for the commit it lives, or an internal-overload parameter instead.
- Phase 3 task 2: direction record for `numpy.random`'s per-thread → process-wide change.
- Scope: the filing budget / expected verify cost line (contract §10).
- Phase 1 bullet 3: the instance-`GetType()` negative control and the roster/allowlist split.

### Unchecked Claims
- "`Sharpy.Core` has no cell; its two `Type`-keyed caches are rostered only" — not verified (no Core grep; a `Core.Tests` red at 8 threads would be a new cell for #2250).
- The `20`/`22` static-cache rows' key-purity justifications are the implementer's to write; the verifier checked only `_byName` (fixed surface) and the five name-keyed ones the plan drains.
- The P47 pins' byte-identity after retiring `Isolated()` (Phase 2 task 5) is an execution claim — verifiable only at that sha.
- Option A's first-use cost (loading the full TPA set) is unmeasured by me; Phase 2 task 6 measures it.

## Implementation Verification (/verify-implementation, 2026-10-15 @ 045ce6b58)

Scope `486c9a88a..045ce6b58` (26 implementation commits). Gate @ 045ce6b58: 80350 passed / 0 failed / 4 skipped (measured; Core 3989, Stdlib 4273, Cli 897/1, Lsp 21938, Compiler 49253/3). Five auditors (completeness, regression, class-cure, sibling-cell prober, fact-check) — full reports in the session scratchpad (`report-*.md`).

### Defect Class table additions (cells found by the round; filed by class, not fixed here — contract §10)
| cell | observed @ 045ce6b58 | base 486c9a88a | class / where filed |
|---|---|---|---|
| reference DEPENDENCY bound from the host's own packages (`-r` lone Microsoft.CodeAnalysis.CSharp.dll → compiles, exit 134 FileNotFoundException at run) — F3's dependency arm: `ReferenceLoadContext.Load`→Default and `AssemblyCompiler.GetMetadataReferences` (whole host TPA) | uncured | same | **#2322** (new class, owner-instructed 2026-10-15; #2182 comment carries the cells) |
| `-r O -r A` in separate dirs → SPY0909 FileNotFoundException + bundle; order-dependent | uncured | same | #2322 cell 2 |
| framework-named user reference silently replaced by the framework copy (code-read) | — | — | #2182 comment |
| compile server: referenced dll rebuilt in place → exit 3 "Invalid assembly public key", sticky; atomic `mv` → stale SPY0203 with the new binary deployed | uncured | same | #2114 comment (refutes its "stayed correct" note) |
| `ReferenceLoadContext` key reads prior references' CURRENT disk identity, not the loaded copy's (code-read) | — | — | #2114 comment |
| nested `def f` ×2, call fits the FIRST only → SPY0908 CS0128 (was SPY0220 at base: refusal → ICE, in kind) | regression in kind | SPY0220 | #2314 comment (its ruling decides) |
| `from math import pi` then `pi = 3.0` in a function → SPY0908 CS0103 | uncured | same | #2151 comment |
| `type int = str` inside a def / module block accepted (SPY0212 fires at module level only) | uncured | same | **#2319** (new class) |
| module-level `_name` emitted public: `random._lock = Lock()` from an importer compiles and runs | uncured | (`_lock` absent) | #2131 comment |
| emitter reflection site `IsClrTypeGenericInNamespace` answers from FrameworkOnly; semantic from the registry — Rule-2 cell | — | — | **#2320** (new class); the code comment now cites it |
| `enum` in a def → SPY0510 ICE; SPY0202 text/spec for "no local types" | uncured | same | #2316 (already filed by the implement round) |
| unused `from system.composition import ExportAttribute`: ran (SPY0452) → SPY0301 | worked → rejected, same mechanism as the owner-ruled `system.reactive.linq` cell | ran | direction record added to `HostAssemblyVisibilityE2ETests` |
| stdlib `random` spells `random()` as `next_double`; no `Random` class, `getstate`/`setstate`, `randbytes`, `triangular`, nine `*variate` (11 of CPython's 26 names) | — | same | **#2321** (new class, owner-instructed 2026-10-15) |

### Guards
- VACUOUS (class-cure auditor): `InProcessIsolationSweepTests` — 0 red at base; GREEN under the visibility-filter revert (M9) and the master-freeze revert (M7), 9216 observations. Remediated in this round (corpus gains builtin-shadowing triggers + typed probes and a referenced-library pair; mutation record in the commit body).
- Latent ratchet conflict (regression auditor): five harnesses required allowlist/KnownRed rows to cite exactly `#2250` (closed) → switched to the repo's `#\d+` cite convention; `check_allowlist_issue_state.sh` owns the open-state question.
- Every other new guard: red when broken, green when restored (table in `report-class-cure.md`); no inert fix, no inverted exemption.
- Latent, no cell (recorded on #2250): master symbols created after the ctor (`_clrTypeCache`, `_interfaceSymbols`) are not frozen (no writer found); `ProcessWideResolutionScanTests` scans `src/Sharpy.Compiler` only and does not name `AssemblyLoadContext.All` / instance ALC loads as candidates; the own-symbol gate keys on `_controlFlowDepth`, which for/while `else:` bodies do not raise.
