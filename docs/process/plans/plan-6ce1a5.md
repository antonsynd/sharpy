<!-- Verified by /verify-plan on 2026-10-13 @ 8be4576fd -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: built-first (Phase 1; hand matrix only on the server-route axis) -->

# P53 — LSP project-mode parity: every build diagnostic lands on the file it belongs to, and every spelling of an imported name navigates

## Context

In project mode (a `.spyproj` governs the workspace) the editor is supposed to show the same
diagnostics `sharpyc project` reports, each on the file it belongs to, and go-to-definition is
supposed to resolve every spelling of an imported name. Issue #2242 (class tracker, `lane:tooling`
`reach:program`) recorded two cells found by the #2234 chore @ f47b45aa5: a `.spyproj` whose
`main.spy` imports a missing (SPY0300) or unlisted (SPY0313) module shows no squiggle because the
project bag holds the error while the per-file result the publisher sends holds none; and
go-to-definition on `lib.build` of an uncompiled module returns nothing while `from lib import
build` navigates.

The round charter (`.claude/plans/remediation-round-2026-09-20-batching.md` §3.4) places this as
**P53 — LSP project-mode parity**, directly after P50, with the generator FIRST
(`FrontEndParityTests` extended to project mode with an allowlist ratchet) and "no seam
hypothesis — the plan measures the seam". This plan measured it (§Current State). The measured
class is wider than the issue's two cells: the editor's published diagnostics in project mode come
from **two competing routes** (whole-project per-file results and single-file analysis of the open
buffer), the per-file view omits every project-bag-only phase (not only import resolution), the
project route publishes **nothing at all** once the project has any error, and two of the six
editor routes never publish [CORRECTED: the configuration route publishes — see cause 4]. Navigation on `m.name` fails for the compiled unit too, not only for
the extracted module.

Batching comment (authoritative placement): https://github.com/antonsynd/sharpy/issues/2242#issuecomment (2026-10-06, "P53 — LSP project-mode parity … Generator: FrontEndParityTests extended to project mode … Class tracker: this issue").

## Current State

Measured 2026-10-13 @ 7f06491a0 (tree clean; `src/Sharpy.Lsp/bin/Debug/net10.0/Sharpy.Lsp` and
`src/Sharpy.Cli/bin/Debug/net10.0/sharpyc` built from that tree). Probe scripts:
`<scratchpad>/p53probe.py`, `p53probe2.py`, `p53e.py` (stdio driver after
`.claude/tmp/p22e-probes/lspfmt.py`; one server per cell; every
`textDocument/publishDiagnostics` recorded in arrival order). [CORRECTED: the scratchpad is
per-session and `p53e.py` was not found in it; `p53probe.py` and `p53probe2.py` were copied to
`.claude/tmp/p53-probes/` (gitignored) by `/verify-plan` so the close-out can re-run them —
`python3 -I .claude/tmp/p53-probes/p53probe.py`, sandbox off, after building `Sharpy.Lsp`.
The verifier's re-run @ 8be4576fd reproduced every cell of the table below (Verification Summary).]

### Where diagnostics live in `ProjectCompiler.AnalyzeProject` (read)

| Phase | Written to `unit.Diagnostics` (per-file bag) | Written to `_diagnostics` (project bag, = `ProjectAnalysisResult.Diagnostics`, = what `sharpyc project` prints) | `FilePath` stamped? |
|---|---|---|---|
| Lexer / Parser | yes (`MergeWithPhase`, Parsing.cs) | yes | yes (file scope) |
| Package/module-name collisions (`ReportPackageModuleNameCollisions`) | no | yes | yes |
| Phase 3 name resolution / declarations (`ForwardDiagnostics(fileResolver.Diagnostics)`) | **no** | yes (+ `GlobalDiagnostics`) | yes (`NameResolver.SetCurrentFilePath`; nullified only by `SyntheticProject`'s `NullifyEntryFilePath`, never for a `.spyproj`) |
| Phase 4 import resolution (SPY0300, SPY0301, SPY0304–SPY0309, SPY0313, SPY0483, duplicate from-import) | **no** | yes (carried whole since #2032) | yes (importing file) |
| Circular import SPY0302 | **no** | yes | yes (first file of the cycle) |
| Phase 4b inheritance (`ForwardDiagnostics(_sharedNameResolver…)`) | **no** | yes | yes |
| Phase 5 type checking + feature gates | yes | yes | yes |
| Phase 5 `Aborted` arm (`FileCompilationPipeline.cs:190` on `SemanticAnalysisException`; `ProjectCompiler.Phases.cs:962`) | **no** (unit.Phase = Failed) | yes | yes — [ADDED: adequacy] unreachable at HEAD: the one throw site (`TypeChecker.Utilities.cs:2872`) is behind `!ContinueAfterError`, and the pipeline sets `ContinueAfterError = true` (`FileCompilationPipeline.cs:367`); D1's projection covers it by construction, no matrix row |
| ICE (`ReportInternalCompilerError`), cancellation SPY09xx, generator-assembly errors (#2127) | no | yes | partly / no |

`ProjectAnalysisResult.GetFileResult(path)` returns `unit.Diagnostics.GetAll()` only
(`ProjectAnalysisResult.cs:48-66`); `LanguageService.ExtractFileResult` copies that into the
`SemanticResult` every handler and the publisher read. The single-file door
(`CompilerApi.AnalyzeDocument`/`Analyze`, `CompilerApi.cs:481-485`) does the opposite: its
`SemanticResult.Diagnostics` is the **whole** project bag of the synthetic project, siblings
included. Two doors, two answers — the Class F cross-entry-point drift shape (#1144).

### What the editor receives, by route (measured)

Project = `test.spyproj` listing `main.spy` (and `lib.spy` where "listed"); `main.spy` =
`from lib import build` / `import lib` + `build("x")` / `lib.build("x")` on line 5.

| Cell | index-complete route | `didOpen main.spy` | `didChange` (body edit) | definition on `build` | hover on `build` |
|---|---|---|---|---|---|
| from / lib missing | **nothing published** | `main.spy=SPY0300` | `main.spy=SPY0300,SPY0451` | none | `<?>` |
| from / lib unlisted (SPY0313 in the build) | **nothing published** | `main.spy=∅` ← **silent wrong** | `SPY0300,SPY0451` ← spurious | lib.spy:7:4 ✓ | ✓ |
| from / lib listed | `lib.spy=∅; main.spy=∅` | `main.spy=∅` | `SPY0300,SPY0451` ← **spurious** | lib.spy:7:4 ✓ | ✓ |
| plain / lib missing | nothing | `SPY0300` | `SPY0300,SPY0451` | none | `<?>` |
| plain / lib unlisted | nothing | `∅` ← silent wrong | spurious | **none** | ✓ |
| plain / lib listed (compiled unit) | `∅; ∅` | `∅` | spurious | **none** ← the issue said "uncompiled module"; the compiled unit fails too | ✓ |
| from / listed + duplicate `class Widget` in lib.spy | **nothing** (project failed) | **nothing** (publish aborted) | — | ✓ | ✓ |
| same, open `lib.spy` instead | nothing | `lib.spy=SPY0204` (single-file route) | — | | |
| listed, identical-text or comment-only edit | `∅; ∅` | `∅` | `∅` (NoChange fast path) | | |
| listed, edit breaks the import (`from nolib`) | `∅; ∅` | `∅` | `SPY0300` (correct) | | |

Causes, each read in source after the measurement:

1. **Index route is gated on success** — `LanguageService.StartBackgroundIndexing` runs
   `onComplete` only `if (success …)` (`LanguageService.cs:215` [CORRECTED: was :206]), and `Program.cs:141-150` is the
   only project-wide publish. A project with ANY error publishes nothing for any file.
2. **The per-file view omits five phases** — table above; `GetFileResult` is unit-bag-only.
3. **Two routes race on open files** — `TextDocumentSyncHandler.Handle(DidOpen/DidChange)` calls
   `_workspace.OpenDocument`/`ApplyChanges` (which `ScheduleAnalysis` → single-file
   `AnalyzeDocument` → `DocumentAnalyzed` → `PublishDiagnostics`) AND
   `TriggerProjectReanalysisAsync`, which **discards** `OnDocumentChangedAsync`'s `updatedUris`
   (`TextDocumentSyncHandler.cs:118-124`). The single-file analysis discovers the unlisted
   `lib.spy` through the import closure, so SPY0313 never reaches the editor; the E2E tests already
   note "background project indexing and single-file analysis can both publish"
   (`E2E/MultiFileTests.cs:229`).
4. **Watcher and reload routes publish nothing** — `FileWatcherHandler` awaits
   `ReloadProjectAsync` / `OnExternalFileChangedAsync` and drops the result
   (`FileWatcherHandler.cs:22-37`). [CORRECTED: `ApplyWorkspaceFeaturesAsync` is NOT a silent
   route — `DidChangeConfigurationHandler.cs:58-77` awaits it and then publishes
   `GetAnalysisAsync(uri)` for every `GetProjectFileUris()` entry with `sourceText: null`. It is a
   SECOND project-wide publish site beside `Program.cs:141-150`, reading the same unit-bag-only
   view (cause 2), so it shares causes 2 and 5 but not cause 1. Task 7 must delete or reroute
   this loop too, or "one publish site" is false by one.]
4b. **`didClose` clears a project file's squiggles** — `TextDocumentSyncHandler.cs:87-93` calls
   `_diagnosticPublisher.ClearDiagnostics(uri)` for every closed document, project file or not,
   so a project file's build diagnostics vanish from the editor on close while `sharpyc project`
   still reports them. `didSave` (`:95-99`) is a no-op (no cell). [ADDED: adequacy — a route
   the contract's "ANY route" ranges over that the plan did not list.]
5. **A sibling file's diagnostic aborts the whole publication** — `DiagnosticPublisher.
   ConvertDiagnostic` positions every diagnostic of the result against the published document's
   `SourceText`; lib.spy's SPY0204 (span at lib.spy:12) threw
   `ArgumentOutOfRangeException: Position is outside the bounds of the source text` inside
   `SharpyWorkspace.FireAndForgetAnalysis` (server log, `p53e.py`), so `main.spy` got no
   notification at all. The single-file door's whole-bag result makes this reachable with or
   without a project.
6. **Spurious SPY0300 + SPY0451 after any function-body edit** — `AstFingerprint` classifies the
   edit `BodyOnly`, `SharpyWorkspace.cs:315-320` calls `ScopedTypeChecker.RecheckFunction(api,
   text, ct)`, whose body is `api.Analyze(newText, ct)`
   (`src/Sharpy.Compiler/Semantic/ScopedTypeChecker.cs:36` [CORRECTED: the file is in the
   compiler's `Semantic/`, not the LSP — the ledger-row-1 fix reaches the whole gate]): no path,
   no options, no import closure. Reproduces with no project (`p53probe2.py` cells C/C′). **Outside
   this plan's contract** (single-file route, not project mode) — ledger row below; #1099 records
   that the re-check is a full re-run but not that it analyzes a different program.
7. **`lib.build` definition** — `SharpyDefinitionHandler.ResolveFromMemberAccess`
   (`DefinitionHandler.cs:65-86`) handles `UserDefinedType` receivers only; a module receiver
   (`ModuleType`) falls through to null. The checker's module-member arm
   (`TypeChecker.Expressions.Access.cs:404-432`) returns the export's type but records no
   node-keyed fact (`SetMemberAccessResolution` takes a `TypeSymbol` owner), so no handler can
   read "this `MemberAccess` resolved to export X". Hover answers only because its arm falls back
   to the enclosing `FunctionCall`'s `GetCallTarget` (`HoverService.cs:227-237`) — the value
   spelling `f = lib.build` has no such fallback anywhere. `ImplementationHandler.
   ResolveFromMemberAccess` (`:118-138`) is a copy of the definition arm (mirrored site).
   `DefinitionCrossModuleTests` covers `lib.Child().describe()` (a UDT member reached through a
   module), not `lib.Child` / `lib.build` themselves.

Control: `sharpyc project test.spyproj` prints `error[SPY0313]: Module 'lib' resolves to …
(in main.spy)` for the unlisted cell and `Build FAILED` for the missing one (measured @
7f06491a0). The build reports both; the editor shows neither.

## Scope

- **Group:** P53 — LSP project-mode parity (§3.4). Chores riding along: none.
- **Alone:** no — no printed output or emitted layout changes (no snapshot, no generated artifact).
- **Gates:** 1.
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`); the lead files by class (contract §10); trackers named at the end of this plan.
- **Budget [ADDED: missing step]:** verify cost = two corpus sweeps (177 fixtures each, one
  `AnalyzeProject` + one `InitializeProjectAsync` per fixture — roughly 2× the current multi-file
  arm), one E2E route × state matrix (8 routes × 4 states = 32 real-server sessions with
  `DrainHelpers`, minutes not seconds — say the measured time in the close-out), one projection
  matrix (8 × 2), one navigation matrix (3 × 5 × 2), one whole-solution gate; the sibling-cell
  prober's budget is this matrix plus at most two axis changes (§10).
- **Override:** none.

## Defect Class

- **Violated contract:** in project mode, the set of diagnostics the editor holds for file F after
  ANY route (index-complete, didOpen, didChange, didClose [ADDED: adequacy], watched-file change,
  `.spyproj` reload, configuration change) equals the project build's diagnostics whose file is F — for every phase
  that writes to the project bag — and never contains a diagnostic that belongs to another file;
  and a definition request on any spelling of an imported name (`from m import x` → `x`,
  `import m` → `m.x`, called or as a value) lands on `x`'s declaration in `m`'s file whether `m`
  is a compilation unit or an extracted module.
- **Meta-class:** two from the §1 table — **Position/route missing a shared check** (six publish
  routes [CORRECTED: was "five … three of which publish nothing"]: two publish the unit-bag-only
  per-file project results (index-complete, configuration change), one publishes single-file
  results (didOpen/didChange), one clears (didClose), two publish nothing (watched `.spy` change,
  `.spyproj` reload)) → standard cure: move publication to ONE seam that
  every route calls, with a route × state matrix; and **Qualified/bare divergence** (`from`
  spelling navigates, `m.name` spelling does not) → ONE shared member-access resolution helper
  reading ONE recorded fact, re-entering the bare path. The per-file omission is the Class F
  cross-entry-point drift (#1144): two doors (project `GetFileResult` vs single-file
  `AnalyzeDocument`) project the same bag differently.
- **Known member cells:** #2242 (SPY0300 / SPY0313 not published; `lib.build` of an extracted module not navigable).
- **Sibling cells this plan must also cover (the issue's list is a symptom report):**
  - diagnostic phase × file: {name-resolution/declaration (SPY0204 in a sibling), inheritance,
    package-name collision, circular import SPY0302, import resolution (SPY0300, SPY0301, SPY0313,
    SPY0483 warning, duplicate from-import), feature gate, type check (control), parse (control)}
    × {entry file, non-entry listed file, file not open in the editor};
  - route × project state: {index-complete, didOpen, didChange structural, didChange NoChange,
    didClose of a project file with an error [ADDED: adequacy — `ClearDiagnostics` on close,
    cause 4b], watched-file change of a non-open file, `.spyproj` reload, `sharpy.features`
    change} × {clean, import error, sibling declaration error, type error} — the LAST notification per file equals
    the build's set for that file; a project that fails analysis still publishes (incl. `∅` to
    clear stale squiggles);
  - foreign-file diagnostic inside a result (cause 5): published on its own URI with its own
    `SourceText`, never positioned against another file, never aborting the publication;
  - navigation: request {definition, implementation (mirrored arm), hover (control)} × spelling
    {`from`-imported name, `m.name`, `m.name` as a value (no call), `m.Type` (type reference),
    aliased `import m as n` → `n.name`, nested `import a.b` → `a.b.name`} × target {compiled
    unit, extracted module} × receiver kind {module, nested module};
  - an unstamped project-bag entry (cancellation, generator-assembly errors #2127) is published
    on a defined file (D5), not dropped.
  - [ADDED: adequacy] a project-bag entry stamped with a path that is NOT a project file (an
    extracted module outside the source set, #2234's shape): measured ABSENT @ 8be4576fd for a
    syntax error and a duplicate `class` inside an unlisted `lib.spy` — the build prints only
    main.spy-stamped `SPY0313` + `SPY0301` + `SPY0200`; the extracted module's own errors are
    swallowed by extraction (`ImportResolver.cs:197-204`). Phase 1's union check (i) is the
    detector if one ever appears (a bag entry in no project file's projection is a missing
    entry); the rule when it does: publish on that file's own URI through D4's foreign partition,
    and the generator's oracle iterates `GetProjectFileUris() ∪ distinct bag FilePaths`, never
    silently dropped.
- **Lane / reach:** tooling · program; **fidelity-scope row:** n/a (no Python surface).
- **Standing harness that should have caught it:** `FrontEndParityTests` — its LSP arm is
  single-file only (`SweepSingleFileAsync`), and its multi-file arm compares
  `ProjectCompiler.Compile` and `emit diagnostics` against `AnalyzeProject` but never the LSP's
  per-file view or what the publisher sends; no harness drives the publish routes
  (`E2E/MultiFileTests` asserts `Count > 0`, never the set). Phase 1 extends it.
- **Generator:** REQUIRED FIRST (contract §11: diagnostic phase × file × route is ≥2 axes × ≥3
  values; the charter orders it). Phase 1 builds (a) the `LSP-project` pair in
  `FrontEndParityTests` over all 177 multi-file fixtures (25 carry `main.error`, incl.
  `errors/selective_import_error`, three circular-import fixtures, `imports/duplicate_wildcard_
  from_import`) with the per-file attribution oracle, and (b) the navigation sweep over every
  import binding and module-qualified `MemberAccess` in those fixtures — both on the existing
  `frontend-parity-allowlist.txt` ratchet (new pair prefixes). The axis a generator cannot
  enumerate is the **server route** (the in-process harness has no `didOpen`/watcher/reload); that
  axis is the hand matrix in Phase 3, driven through `E2E/LspTestClient`.
- **Owner rulings:** placement comment on #2242 (2026-10-06); R-EZ order (P53 after P50). No
  ruling on the publish-route design — D2 below proposes one; the implementer proceeds with the
  recommended option and records it in the close-out.

## Adversarial Review (pre-mortem)

- **Alternative root cause:** "the publisher drops import diagnostics" — refuted: route 2
  (`didOpen`) publishes SPY0300 when the module is missing; the omission is upstream in
  `GetFileResult`, and SPY0313 is masked by route 3's single-file closure. "The index route is
  slow, not absent" — refuted by the 4 s drain and `LanguageService.cs:215` (`if (success …)`).
  Discriminating probe for the implementer: `p53probe.py` cell `from/unlisted` must go from
  `open: main.spy=∅` to `main.spy=SPY0313`, and `from/listed+dupclass` from `(none)` to
  `lib.spy=SPY0204; main.spy=∅`.
- **Alternative layer:** rung 1 (`.spy`) and rung 2 (Core C#) cannot carry an editor publication
  fact. For navigation, the lower rung is the fact the checker already computes in its
  module-member arm: recording it node-keyed (`SemanticInfo`, Rule 2b) is the lowest rung that
  makes `m.name` resolution ONE decision; an LSP-side `symbolTable.Lookup(ma.Object) as
  ModuleSymbol → Exports[ma.Member]` would re-implement the checker's escape-identity rule
  (#1328) and the `.NET` PascalCase fallback — the name-keyed-alias-diverging-from-the-authority
  meta-class (#1933). Rejected.
- **How the fix could be inert:** (i) `GetFileResult` widened but `ExtractFileResult` still
  reads `unit.Diagnostics` — the generator's attribution oracle reads through
  `LanguageService.GetAnalysisAsync`, the seam the publisher reads, so an inert widening stays
  red. (ii) Publication moved to one seam but `TextDocumentSyncHandler.OnDocumentAnalyzedAsync`
  still publishes single-file results for project files one call later — the E2E route cells
  assert the LAST notification, and a mutation that re-enables the single-file publish for a
  project file must turn `from/unlisted` red. (iii) The module-member fact recorded but the
  definition handler reads `GetEffectiveType` first — the navigation sweep's `m.name`-as-value
  cell has no `FunctionCall` to fall back to, so a handler that never reads the fact stays red.
- **Before/after by direction:** every cell in §Current State is "worked (build) / silent (editor)"
  → "worked (editor)"; the only newly published diagnostics are ones the build already prints.
  A program that compiled clean must publish `∅` on every route (positive control in every
  matrix: `from/listed` stays `∅`). The checker change records a fact and emits nothing — the
  whole-solution gate is the direction record (no new refusal anywhere).
- **Blast radius:** Lsp.Tests entire (handlers, E2E, `FrontEndParityTests` in the CI LSP job —
  `Category!=Benchmark` includes GapDiscovery there); Compiler.Tests
  `SemanticInfoMergeConformanceTests` (`MinimumSideTableFieldCount` 78 → 79; it also calls
  `GetFileResult`), `ProjectAnalysisResultTests` (per-file view) [CORRECTED: the plan named
  `SymbolFactMirrorConformanceTests`, `QualifiedBareSpellingConformanceTests` and
  `ImportedStubClassificationReachabilityTests` as `GetFileResult` consumers; `grep -rln
  GetFileResult src/` @ 8be4576fd lists only `CompilerApi.cs`, `LanguageService.cs`,
  `ProjectAnalysisResultTests.cs`, `SemanticInfoMergeConformanceTests.cs`]; `CompilerApi.Analyze*`
  (`CompilerApi.cs:475`) reads `GetFileResult` for AST/SemanticInfo only — its `Diagnostics` stays the whole bag (the CLI prints siblings' errors
  with their paths; unchanged). `Semantic/` is touched (one recording call + one dictionary), so
  the gate runs whole: Stdlib.Tests, Cli.Tests, the three sweeps. No `CodeGen/` change.
- **Generated artifacts touched:** none (no codegen, no stdlib, no docs generator). Regen check
  at the start of the round still runs (`regen: no diff @ sha`).

## Design Decisions

- **D1 — ONE per-file projection of the project bag (rung: compiler service, `Project/`).**
  `ProjectAnalysisResult.GetFileResult(path)` returns `unit.Diagnostics ∪ { d ∈ Diagnostics :
  PathNormalizer.Normalize(d.FilePath) == Normalize(path) }`, deduplicated by the bag's own key
  [CORRECTED: was "`CompilerDiagnostic` record equality" — `CompilerDiagnostic` carries `Data`
  (`IReadOnlyDictionary`) and `RelatedLocations` (`IReadOnlyList`), which record equality compares
  by reference; `DiagnosticBag.GetDeduplicationKey` = (Code, Line, Column, Message-when-codeless,
  Span.Start, Span.Length, FilePath) is the rule every bag already applies — build the projection
  through a `DiagnosticBag` (`Add` dedupes) or expose the key `internal`] (the type-check phase
  already writes both bags), in the project bag's order.
  `FileAnalysisResult.Success` becomes `Phase == TypeChecked && no error in the projected set`
  (the single-file door derives success from the whole bag; a file whose import failed is not
  "successful"). The projection lives in one helper (`ProjectModel.DiagnosticsForFile` or a
  `FileDiagnosticProjection` static) so `GetFileResult`, the publisher's partitioner (D4) and
  the generator's oracle share the rule.
- **D2 — publication is ONE seam that every route calls; for a project file the project per-file
  result is the sole publisher (recommended: option a).**
  - (a) *recommended*: `LanguageService` owns a `PublishProjectDiagnostics(IEnumerable<uri>)`
    path (publisher injected or an event `ProjectFilesAnalyzed(uris)` the sync handler
    subscribes to, mirroring `DocumentAnalyzed`); `InitializeProjectAsync` raises it for every
    project file **whether or not analysis succeeded** (the `success &&` gate in
    `StartBackgroundIndexing` keeps gating `onComplete` for other callers but publication no
    longer rides on it); `OnDocumentChangedAsync`, `OnExternalFileChangedAsync`,
    `ReloadProjectAsync`, `ApplyWorkspaceFeaturesAsync` raise it for their updated URIs;
    `TextDocumentSyncHandler.OnDocumentAnalyzedAsync` returns without publishing when
    `_languageService.IsProjectFile(uri)` (which already means "the project result cache holds
    this file" — `LanguageService.cs:354-361` reads `_fileResults.ContainsKey`, so the
    pre-indexing fallback in Task 7 falls out of the same predicate) (the single-file analysis still runs — completion's
    last-good analysis (#1360) and the pre-indexing fallback depend on it — it just no longer
    writes squiggles for a file the project owns). Cost: the squiggle after an edit arrives when
    the project reanalysis finishes instead of the single-file one; both already run on every
    edit today (`AnalysisLatencyLog` records both paths — Phase 3 reports `project` vs
    `single-file` ms @ sha so the owner sees the delta). [ADDED: adequacy] `Handle(DidClose)`
    clears only when `!IsProjectFile(uri)`; for a project file the project's publication stands
    (the build still owns the file) — the contract's "ANY route" has no close exemption, and an
    editor's Problems panel is where a closed file's build errors live. The E2E `didClose` cells
    pin it (last notification per URI after close == the build's set, not `∅`).
  - (b) keep the single-file publish as a fast preview and let the project publish overwrite it,
    with a per-URI monotonic generation so an older single-file notification cannot land after
    the project one. Rejected for now: a transient squiggle the build disagrees with (SPY0300 on
    an unlisted sibling that the closure then finds, or `∅` where the build says SPY0313) is
    exactly the drift this group removes; (a) → (b) is additive if the latency delta measured in
    Phase 3 is judged too high.
  - (c) status quo with the per-file view widened only (D1 alone). Rejected: cells
    `from/unlisted` (route 3 masks SPY0313) and every "nothing published" row stay red.
- **D3 — the index-complete route publishes on failure too, and publishes `∅` for clean files**
  (clears stale squiggles after a fix made outside the editor). A project whose analysis threw
  (ICE) publishes the ICE on the entry file (D5).
- **D4 — the publisher partitions by file (the E′ fix), on both routes.**
  `DiagnosticPublisher.PublishDiagnostics(uri, result, sourceText)` becomes: partition
  `result.Diagnostics` by normalized `FilePath` (null/empty → this document); publish this
  document's partition against `sourceText`; for every other partition publish on that file's
  URI with that file's `SourceText` from the workspace or disk (`LanguageService.GetSourceText`)
  or, when unavailable, line/column only (`PositionConverter.DiagnosticToRange(diag, null)` is
  already the shape the index route uses). A diagnostic is never positioned against a text it
  does not belong to. Generated-source reroutes (`<generated:…>`) keep their existing
  `TryRerouteGeneratedDiagnostic` path (that reroute sets `FilePath = documentUri`, so it lands in
  this document's partition — the ordering is: reroute first, then partition).
- **D5 — an unstamped project-bag entry is published on the project's entry file** (`config.
  EntryPoint`), or on the first `SourceFiles` entry for a library; the rule is a named constant
  and a test, and the generator reports every unstamped entry it meets (`FilePath` null on a
  phase-stamped diagnostic) as a cell on #2127 (path-less diagnostics), never silently routed.
- **D6 — the module-member navigation fact is node-keyed (Rule 2b, rung 2b in the
  materialization sense).** `SemanticInfo` gains `_moduleMemberResolutions:
  ConcurrentDictionary<MemberAccess, (ModuleSymbol Module, Symbol Member)>` with
  `SetModuleMemberResolution` / `GetModuleMemberResolution` (on `ISemanticQuery`), written at
  the ONE place the checker binds `module.member` (`TypeChecker.Expressions.Access.cs:404`,
  inside the `Exports.TryGetValue` branch, after the escape-identity check — so the fact exists
  exactly when the checker accepted the spelling) and for the `.NET` PascalCase and
  `TryResolveClrSubNamespaceOrType` arms when they bind a symbol; merged in `MergeFrom`
  (`MinimumSideTableFieldCount` 78 → 79, the behavioural round-trip test covers it). Why not widen
  `GetMemberAccessResolution`: its owner is a `TypeSymbol` and `ConstEligibility` reads it as one.
- **D7 — ONE member-access symbol resolver for navigation handlers.** A new
  `Handlers/MemberAccessSymbolResolver.Resolve(ISemanticQuery, SemanticResult, MemberAccess,
  Node? enclosingCall)` tries, in order: `GetMemberAccessResolution` (static/const/enum, the
  fact hover reads first), `GetModuleMemberResolution` (D6), the enclosing `FunctionCall`'s
  `GetCallTarget` when the access is its `Function` (hover's existing fallback, now shared),
  then the UDT method/field search the two handlers duplicate today. `SharpyDefinitionHandler`
  and `SharpyImplementationHandler` call it (their private copies are deleted); `HoverService`'s
  `MemberAccess` arm calls it before its builtin-type and type-info fallbacks. Mirrored sites
  enumerated by `grep -n "ResolveFromMemberAccess\|GetEffectiveType(ma.Object)"
  src/Sharpy.Lsp` — two today (Definition, Implementation); `TypeDefinitionHandler` reads the
  effective type by design (a type, not a symbol) and is not a site.
- **Axioms:** none in conflict (editor plumbing). Spec: no language-spec section; the tooling
  doc `docs/tooling/lsp-server.md` ("Diagnostics are published after each document change") gains
  the project-mode paragraph (Phase 5) — a deliverable of equal weight, verified by the E2E
  route cells it describes.

## Implementation

### Phase 1: Generator FIRST — `FrontEndParityTests` learns project mode, and a navigation sweep

**Goal:** a red harness that enumerates the class before any seam moves: per-file published
diagnostics in project mode vs the build's bag, with attribution; and definition on every imported
spelling in the corpus.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green (after Phases 2–4): `FrontEndParityTests` pair `LSP-project` over
  all 177 multi-file fixtures — for each fixture `LanguageService.InitializeProjectAsync(dir)`
  (temp copy with a generated `project.spyproj` listing every `.spy`, as `SweepMultiFile`'s
  `MakeConfig` does), then for every project file `GetAnalysisAsync(uri)`: (i) the union of
  per-file in-scope multisets (N1 filter) equals the `AnalyzeProject` baseline multiset; (ii) every
  diagnostic in file F's result has `FilePath` ∈ {F, null-under-D5-rule} — a diagnostic published on
  the wrong file is a violation even when the union matches. Pair `LSP-project-nav` over the same
  fixtures: for every `FromImportStatement` name and every `MemberAccess` whose `Object` resolves to
  a `ModuleSymbol` (via `GetIdentifierSymbol`) in every file, `SharpyDefinitionHandler.Handle` at
  the member's position returns a location whose file is the module's `FilePath` (or, for a
  `.NET`/stdlib module with no file, is record-and-skipped with the reason) — the fixture's own
  module files are the oracle, no hand list.
- Guard mutation: before Phase 2, the sweep's first run IS the cell table (expected red:
  `errors/selective_import_error`, the circular-import fixtures, `imports/duplicate_wildcard_
  from_import`, every fixture with a declaration error in a sibling, and every `m.name` cell);
  the entries go on `frontend-parity-allowlist.txt` citing #2242 in a block headed `LSP-project`,
  and Phases 2–4 drain them. After Phase 4: revert the `GetFileResult` hunk → `LSP-project` red
  (N fixtures named); revert the D6 recording → `LSP-project-nav` red; restore → green; both
  counts in the commit body. [ADDED: missing step] The attribution check (ii) is true by
  construction once D1 filters on `FilePath`, so its own mutation is named separately: drop the
  `FilePath` filter in the projection (copy every project-bag entry into every file's result) →
  (ii) reports `misattributed:<code>@<file>` on every fixture with an error in a non-entry file
  AND (i)'s union over-counts; record that red beside the other two. Positive control for the attribution oracle: a fixture with a
  type error in a non-entry file (`multi_file/…`) must already attribute correctly at HEAD (type
  check writes both bags) — if the oracle reports zero attributed diagnostics across the corpus
  it is vacuous (assert `attributed > 0`).
- Allowlist entries that drain: `frontend-parity-allowlist.txt` — every `LSP-project::*` and
  `LSP-project-nav::*` line added here is deleted by the phase that fixes it; stale-entry
  detection (#1605) already fails the ratchet otherwise.
- Execution evidence the close-out will cite: `FrontEndParityTests` report
  `.claude/tmp/frontend-parity-report.json` @ sha — comparisons / violations / allowlist 0 for
  the two new pairs; the sweep's `Fixtures discovered: 177 multi-file` line.

#### Tasks

1. **Add the `LSP-project` pair to the multi-file sweep** — `src/Sharpy.Lsp.Tests/Conformance/FrontEndParityTests.cs`
   - In `SweepMultiFile`, after the `compile-project` comparison: copy the fixture directory to a
     temp root (the fixture tree is read-only shared state; `InitializeProjectAsync` reads
     `*.spyproj` from the root via `ProjectFileParser.FindProjectFile`), write `project.spyproj`
     from `MakeConfig()`'s file list, build a `SharpyWorkspace` + `LanguageService` (as
     `ImportOutsideSourceSetEditorTests` does) and thread the fixture's `main.features` the way
     the single-file LSP arm does (`workspace.SetConfiguredFeatures(fixture.Features)`,
     `FrontEndParityTests.cs:296`) or as `<Features>` in the generated `.spyproj` — otherwise
     every gated fixture is an SPY0331 drift red that is not this class [ADDED: missing step],
     await `InitializeProjectAsync`, then for each source file collect `GetAnalysisAsync(uri)!.
     Diagnostics`. Build (i) the union signature and compare with `Signature(baselineDiags)`
     under key `LSP-project::<fixture>`; (ii) the attribution check: each diagnostic's
     normalized `FilePath` equals the file's, or is null and the file is the D5 target — a
     mismatch is recorded as the same comparison key with `Added = ["misattributed:<code>@<file>"]`
     so one allowlist key covers the fixture.
   - The harness states its coverage: the report gains `multiFileFixtures`, `lspProjectCompared`,
     `attributedDiagnostics` (assert > 0 — the vacuity control).
   - Acceptance: "Matrix/sweep", "Guard mutation" (first-run red), "Allowlist entries that drain".
   - Commit: `test(conformance): the front-end parity sweep compares the LSP's project-mode per-file view against the build's bag, with per-file attribution (P53, refs #2242)`

2. **Add the `LSP-project-nav` navigation sweep** — same file (or `Conformance/ProjectNavigationSweepTests.cs` sharing `SweepCorpus`/the allowlist loader)
   - For every file of every multi-file fixture (project loaded as in Task 1): walk the AST
     (`AstVisitor`) for `FromImportStatement` aliases and for `MemberAccess` nodes whose
     `Object` is an `Identifier` with `GetIdentifierSymbol` → `ModuleSymbol` (nested
     `a.b.name`: `Object` is itself a `MemberAccess` resolving to a nested module — include it).
     Ask `SharpyDefinitionHandler.Handle` at the member token's position (`Symbol.
     EffectiveNameLine/Column` conventions; for a `MemberAccess` use `ma.MemberNameLineStart` /
     `ma.MemberNameColumnStart` (`Expression.cs` ~545, #1503 — recorded from the member token;
     `MemberNameColumnEnd` is the exclusive end) [CORRECTED: the plan guessed `MemberLineStart/
     MemberColumnStart`; the properties exist under the `MemberName*` spelling, so no fallback
     arithmetic is needed]). Expect a `Location` whose file path equals the module's `FilePath` (skip-and-record
     when `IsNetModule` or `FilePath == ""`).
   - Key `LSP-project-nav::<fixture>::<file>:<line>:<col>` so a single cell is one allowlist line.
   - Acceptance: "Matrix/sweep", "Guard mutation" (first run red on every `m.name` cell; `from`
     cells green — the positive control that the oracle can pass).
   - Commit: `test(conformance): a navigation sweep asks go-to-definition on every imported spelling in the multi-file corpus and ratchets its misses (P53, refs #2242)`

3. **Record the first run** — `frontend-parity-allowlist.txt`
   - Run both pairs (`.claude/scripts/dotnet-serialized test src/Sharpy.Lsp.Tests --filter
     "FullyQualifiedName~FrontEndParity" --logger "console;verbosity=normal"`, sandbox off); add
     every offender under `# --- P53 LSP project mode (#2242) — drains in plan-6ce1a5 Phases 2–4 ---`;
     the commit body carries the cell table (code × file × fixture) — this IS the measured matrix.
   - Commit: `test(conformance): allowlist the P53 cell table the project-mode parity and navigation sweeps found @ <sha> (refs #2242)`

### Phase 2: ONE per-file projection of the project bag (D1, D5)

**Goal:** `GetFileResult(F)` returns every build diagnostic that belongs to F.

**Acceptance:**
- Matrix: `ProjectAnalysisResultTests` gains a phase × file matrix built with
  `ProjectCompilationHelper` (in Compiler.Tests): {import SPY0300, import SPY0313, declaration
  SPY0204 in a sibling, inheritance error, circular SPY0302, package collision, type error
  (control), parse error (control)} × {entry, non-entry} — each diagnostic appears in exactly the
  file it was stamped with, once (no duplicate for the double-written type-check phase), and
  `Success` is false for a file whose projected set has an error. The `LSP-project` allowlist
  lines for the diagnostic cells drain.
- Guard mutation: comment out the project-bag union in the projection → the matrix's import and
  declaration rows red, type-check row green (it must — that is the positive control that the
  union, not the unit bag, carries the fix); dedupe disabled → type-check row red with count 2.
- Allowlist entries that drain: every `LSP-project::*` line whose cause is attribution/omission
  (not the route race — those drain in Phase 3).
- Execution evidence: `p53probe.py` cell `from/listed+dupclass` index route after Phase 3; for
  this phase, `LanguageService.GetAnalysisAsync(main.spy)` in a unit test returns SPY0313.

#### Tasks

4. **Project the bag per file** — `src/Sharpy.Compiler/Project/ProjectAnalysisResult.cs`, `src/Sharpy.Compiler/Model/ProjectModel.cs`
   - Add `ProjectModel.DiagnosticsForFile(string filePath, DiagnosticBag projectBag)` (or a
     static `FileDiagnosticProjection.Project(...)`): unit bag entries first, then project-bag
     entries whose normalized `FilePath` matches, then — only for the D5 target file — entries
     with no `FilePath`; dedupe by `DiagnosticBag.GetDeduplicationKey` preserving first
     occurrence [CORRECTED: not record equality — see D1]. `GetFileResult`
     uses it and derives `Success` from the projected set. D5 target: `config.EntryPoint` when
     set, else the first `SourceFiles` entry; expose `ProjectAnalysisResult.UnattributedTarget`
     so the publisher and the generator read the same rule.
   - Doc comment names #2242 and the table in §Current State (which phases were unit-bag-only).
   - Acceptance: matrix rows; "Guard mutation".
   - Commit: `fix(project): a file's analysis result carries every project-bag diagnostic stamped with that file — import, declaration, inheritance and cycle errors were unit-bag-invisible (P53, refs #2242)`

5. **Matrix test + drain** — `src/Sharpy.Compiler.Tests/Project/ProjectAnalysisResultTests.cs`, `frontend-parity-allowlist.txt`
   - The phase × file theory above (`ProjectCompilationHelper.WithRootNamespace(...).
     AddSourceFile(...)`), plus the D5 test (an `AnalyzeProject` cancelled via a pre-cancelled
     token yields SPY09xx on the entry file's result and on no other file).
   - Delete the drained allowlist lines in the same commit.
   - Commit: `test(project): per-file results are a phase × file matrix over the project bag; the P53 attribution allowlist drains (refs #2242)`

### Phase 3: ONE publication seam for every route (D2 a, D3, D4)

**Goal:** what the editor holds for a project file is the project's per-file result, on every
route, including failure; a foreign-file diagnostic never aborts a publication.

**Acceptance:**
- Matrix: E2E route × state hand matrix (the axis the generator cannot enumerate) in
  `E2E/MultiFileTests.cs` (or a new `E2E/ProjectDiagnosticRoutesTests.cs`) driven through
  `LspTestClient` against the real server: routes {initialize (index), didOpen, didChange
  structural, didChange NoChange, didClose [ADDED: adequacy], `workspace/didChangeWatchedFiles`
  on a non-open sibling, `.spyproj` change (reload), `workspace/didChangeConfiguration`
  features} × states {clean,
  missing module, unlisted module, sibling duplicate class}; the oracle is the LAST
  `publishDiagnostics` per URI after a drain (`DrainHelpers`), compared as a code multiset to
  `CompilerApi.AnalyzeProject` projected per file (Phase 2's helper) — the same program the
  build sees. `from/unlisted` → `main.spy=[SPY0313]`; `listed+dupclass` → `lib.spy=[SPY0204]`,
  `main.spy=[]`; clean → `[]` on every file; missing → `[SPY0300]`.
- Guard mutation: re-enable `OnDocumentAnalyzedAsync` publishing for project files → the
  `didOpen/unlisted` cell red (single-file `∅` wins); restore the `success &&` gate on the index
  publish → every error-state index cell red; disable the partitioner (publish the whole result
  on the requesting URI) → `dupclass/didOpen` red (publication aborted) — the unit test for D4
  asserts the exception path is gone with a positive control (a same-file span still converts
  to a real range).
- Allowlist entries that drain: the remaining `LSP-project::*` lines (route race).
- Execution evidence: `p53probe.py` and `p53probe2.py` tables re-run @ sha pasted into the
  close-out beside the HEAD tables above; `AnalysisLatencyLog` `project` vs `single-file` ms on
  the same edit @ sha (D2's cost, measured, not derived).

#### Tasks

6. **Publisher partitions by file** — `src/Sharpy.Lsp/DiagnosticPublisher.cs`, `src/Sharpy.Lsp.Tests/DiagnosticPublisherTests.cs`
   - `PublishDiagnostics(uri, result, sourceText, Func<string, SourceText?>? sourceTextFor = null)`
     partitions after the generated-source reroute; publishes each partition on its own URI with
     its own text (resolver callback supplied by the sync handler / `LanguageService.
     GetSourceText`), null text → line/column ranges. Unit test: a result holding a diagnostic
     with `FilePath = lib.spy` and a span past `main.spy`'s end publishes two notifications and
     throws nothing (positive control: the same diagnostic with `FilePath = main.spy` and an
     in-range span yields the span's range).
   - Commit: `fix(lsp): the publisher publishes each diagnostic on the file it belongs to — a sibling file's span no longer throws against the open buffer's text and drops the whole publication (P53, refs #2242)`

7. **Project publication seam in `LanguageService`** — `src/Sharpy.Lsp/LanguageService.cs`, `src/Sharpy.Lsp/Program.cs`, `src/Sharpy.Lsp/Handlers/TextDocumentSyncHandler.cs`, `src/Sharpy.Lsp/Handlers/FileWatcherHandler.cs`, `src/Sharpy.Lsp/Handlers/DidChangeConfigurationHandler.cs`
   - `LanguageService` raises `ProjectFilesAnalyzed(IReadOnlyList<string> uris)` from
     `InitializeProjectAsync` (all project files, success or not — including after the
     `catch (Exception)` arm: publish the ICE/cancellation on the D5 file), `OnDocumentChangedAsync`
     (its `updatedUris`), `OnExternalFileChangedAsync`, `ReloadProjectAsync`,
     `ApplyWorkspaceFeaturesAsync`. `TextDocumentSyncHandler` subscribes and publishes each URI's
     `GetAnalysisAsync` result through Task 6's publisher (replacing the `Program.cs` callback
     AND the republish loop in `DidChangeConfigurationHandler.cs:65-77` [CORRECTED: a second
     existing project-wide publish site] — both deleted; one publish site); `Handle(DidClose)`
     clears only non-project files (D2 a, [ADDED: adequacy]); `OnDocumentAnalyzedAsync` returns early for
     `IsProjectFile(uri)` once the project cache holds the file (the pre-indexing fallback keeps
     publishing single-file results until then — state that in a comment and pin it: a file
     opened during indexing gets a single-file publish, then the project publish).
   - `StartBackgroundIndexing`'s `onComplete` gate is unchanged for its other callers; publication
     no longer depends on it.
   - Log the measured latency of both paths for the same edit (already in `AnalysisLatencyLog`).
   - Commit: `fix(lsp): project-mode diagnostics are published through one seam on every route — index completion (even on failure), open, change, watched-file change, project reload and feature change — and the single-file route no longer overwrites a project file's squiggles (P53, refs #2242)`

8. **Route × state E2E matrix + drain** — `src/Sharpy.Lsp.Tests/E2E/MultiFileTests.cs` (or `ProjectDiagnosticRoutesTests.cs`), `frontend-parity-allowlist.txt`
   - The matrix above; each cell drains to the last notification and compares to the projected
     build set; `DrainHelpers` already exists. Delete the remaining `LSP-project::*` lines.
   - Commit: `test(lsp): a route × state matrix pins that the editor's last publication per file equals the build's per-file diagnostics in project mode; the P53 route allowlist drains (refs #2242)`

### Phase 4: `m.name` navigates (D6, D7)

**Goal:** one recorded fact, one resolver, every spelling.

**Acceptance:**
- Matrix: `Lsp.Tests` theory (new `DefinitionModuleQualifiedTests.cs`, modelled on
  `DefinitionOutsideSourceSetTests`) — request {definition, implementation, hover} × spelling
  {`lib.build(...)`, `f = lib.build` (value), `lib.Widget` (type), `import lib as l` → `l.build`,
  nested `import pkg.lib` → `pkg.lib.build`} × target {listed unit, unlisted extraction}: definition
  and implementation land on `build`/`Widget` in `lib.spy` at `(8,5)`/`(1,7)`; hover shows the
  signature. `ImportOutsideSourceSetEditorTests.Definition_…` gains the `plain` row it excluded
  ("not a row: … a gap of its own") — delete that comment. The `LSP-project-nav` allowlist drains.
- Guard mutation: remove the `SetModuleMemberResolution` call → every `m.name` cell red, the
  `from` cells green (control); delete `MergeFrom`'s copy → `SemanticInfoMergeConformanceTests`
  red AND the project-mode cells red (the single-file cells stay green — that asymmetry is the
  record that the merge is load-bearing).
- Allowlist entries that drain: every `LSP-project-nav::*` line.
- Execution evidence: `p53probe.py` definition column @ sha: `plain/listed` and `plain/unlisted`
  → `lib.spy:7:4`.

#### Tasks

9. **Record the module-member fact** — `src/Sharpy.Compiler/Semantic/SemanticInfo.cs`, `src/Sharpy.Compiler/Services/ISemanticQuery.cs`, `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.cs`, `src/Sharpy.Compiler.Tests/Semantic/SemanticInfoMergeConformanceTests.cs`
   - Dictionary + setter/getter + `MergeFrom` + interface member; `MinimumSideTableFieldCount`
     78 → 79. Record in the `Exports.TryGetValue` branch (after the #1328 escape check, before
     `return exportedType`), in the PascalCase-renamed branch (same site — `memberName` is
     already rewritten) and in `TryResolveClrSubNamespaceOrType` when it binds a sub-module or
     CLR type symbol. The getter's doc names the consumer (navigation handlers) and the reason
     `GetMemberAccessResolution` cannot carry it (TypeSymbol owner).
   - Commit: `feat(semantic): the checker records which export a module-qualified member access bound, node-keyed and merged, so the editor can read one resolution instead of re-deriving it (P53, refs #2242)`

10. **One resolver for the navigation handlers** — `src/Sharpy.Lsp/Handlers/MemberAccessSymbolResolver.cs` (new), `DefinitionHandler.cs`, `ImplementationHandler.cs`, `HoverService.cs`
    - D7 order; both private `ResolveFromMemberAccess` copies deleted; `DispatchSiteInventoryTests`
      / `WrapperNodeUnwrapConformanceTests` scan `src/Sharpy.Lsp` — run them in the edit loop
      (memory: LSP-only work has reddened them twice).
    - Commit: `fix(lsp): go-to-definition and implementation resolve a module-qualified member through one resolver that reads the checker's recorded export binding — lib.build navigates on a compiled unit and an extracted module alike (P53, refs #2242)`

11. **Matrix test + drain** — `src/Sharpy.Lsp.Tests/DefinitionModuleQualifiedTests.cs` (new), `ImportOutsideSourceSetEditorTests.cs`, `frontend-parity-allowlist.txt`
    - Commit: `test(lsp): module-qualified navigation is a request × spelling × target matrix; the P53 navigation allowlist drains (refs #2242)`

### Phase 5: Docs and close-out

**Goal:** the tooling doc states the contract the E2E matrix pins; the close-out carries evidence.

**Acceptance:**
- `docs/tooling/lsp-server.md` §Diagnostics gains "In project mode": the per-file rule (every
  build diagnostic on its file; unattributed on the entry file), the routes that publish, and that
  a failed project still publishes. Each sentence is a cell in Task 8's matrix (execution evidence
  = that test @ sha).
- Close-out comment on #2242: the §Current State tables re-measured @ sha beside the HEAD ones;
  the two sweeps' report lines; allowlist 0 for both pairs; the gate `passed/failed/skipped @ sha
  (measured)`; the latency delta (D2); the ledger rows handed to the lead.

#### Tasks

12. **Doc paragraph** — `docs/tooling/lsp-server.md`
    - Commit: `docs(tooling): project-mode diagnostics — every build diagnostic on its file, on every route, even when the project fails (P53, refs #2242)`

13. **Close-out** — issue comment; `/close-issues` after the gate.

## Testing Strategy

- **Generator first (§11):** Phase 1's two sweeps over the 177 multi-file fixtures on the existing
  `frontend-parity-allowlist.txt` ratchet; the axis the generator cannot enumerate is the server
  **route** (didOpen/didChange/watcher/reload/configuration are server-side), covered by the
  Phase 3 hand matrix through `E2E/LspTestClient` — and only that axis.
- **New fixtures:** none in the compiler corpus (the class is editor plumbing); temp projects
  are built in-test (`ProjectCompilationHelper` in Compiler.Tests, temp dirs in Lsp.Tests as the
  existing cross-module tests do). Every negative cell has its positive control named in the
  phase: `from/listed` stays `∅`; the type-check row of the projection matrix is green with the
  union disabled; `from` navigation cells are green with the recording removed.
- **Edge cells outside the issue's list:** sibling declaration error (SPY0204) with the sibling
  not open; cancellation/ICE on the D5 file; a file opened DURING indexing (single-file publish
  then project publish — pinned as the documented order); nested and aliased module spellings;
  `m.name` as a value; a `.NET` module member (`system.console.write_line`) is record-and-skipped
  by the navigation sweep (no file) — stated in the report, not silently passed.
- **Discriminating outputs:** every assertion is a code multiset per URI or a `(file, line,
  col)` location; `Count > 0` assertions are not added anywhere (the existing E2E ones stay as
  they are).
- **Blast radius:** Lsp.Tests whole (incl. `FrontEndParityTests`, `DispatchSiteInventoryTests`,
  `WrapperNodeUnwrapConformanceTests` which scan `src/Sharpy.Lsp`); Compiler.Tests
  `SemanticInfoMergeConformanceTests`, `ProjectAnalysisResultTests` (the only test consumers of
  `GetFileResult` [CORRECTED]); the whole gate once (a `Semantic/` recording call). Regen: none expected —
  `regen: no diff @ sha` recorded at the start. Gate command: `.claude/scripts/dotnet-serialized
  test --filter "Category!=Benchmark" --logger "console;verbosity=normal"` in the background;
  counts `passed/failed/skipped @ sha (measured)`.
- **Findings ledger rows for the lead (outside this contract; §10):**
  1. `BodyOnly re-check analyzes a different program` · any file with a sibling import, edit
     inside a function body · `SPY0300 + SPY0451` published, with and without a project ·
     expected `∅` @ 7f06491a0 · lane false-refusal · reach program · candidate class: the
     single-file re-analysis after an edit must be the same door as the open analysis
     (`ScopedTypeChecker.RecheckFunction` calls `api.Analyze(newText)` with no path and no
     options — the #1433/#1377 shape; #1099 records the full re-run, not the drift) · found by
     plan-6ce1a5 research. No open tracker names it (#1099 is the latency design issue): the lead
     files ONE issue with this cell and the `FrontEndParityTests` extension that would catch it
     (an LSP arm after a `BodyOnly` edit).
  2. Phase-stamped project-bag diagnostics with no `FilePath` the Phase 1 sweep reports →
     comments on #2127.

## Issues to Close

- #2242 — lsp: project mode does not publish import-resolution errors (SPY0300, SPY0313), and
  go-to-definition on `m.name` of an uncompiled module returns nothing — closed by Phase 4,
  Task 11 (last drain); **close criterion:** both new parity pairs at allowlist 0 with the
  report's `multiFileFixtures = 177` and `attributedDiagnostics > 0` @ sha; the route × state E2E
  matrix green @ sha; `p53probe.py` re-run @ sha shows `from/unlisted → main.spy=SPY0313`,
  `listed+dupclass → lib.spy=SPY0204; main.spy=∅`, `plain/listed` and `plain/unlisted` definition
  → `lib.spy:7:4`; mutation records for the three guards in their commit bodies; whole-solution
  gate `passed/failed/skipped @ sha (measured)`.

## Class trackers this plan may post cells to

- #2242 — LSP project-mode parity (this class) — cells of the publish/navigation contract found
  outside the plan's matrix (e.g. a further route) go here as comments.
- #2127 — path-less / third-derivation project diagnostics — every phase-stamped diagnostic
  with no `FilePath` the sweep meets.
- #1099 — incremental frontend — the `BodyOnly` nameless re-check is NOT a cell of #1099's
  design scope; the lead files a new class issue (ledger row 1) and cross-links it there.
- #2296 — code-action meaning preservation — if widening a file's diagnostics changes which
  quick fixes `DiagnosticQuickFixProvider` offers on project files (it reads
  `request.Context.Diagnostics`), the cell goes there.
- The lead files a NEW tracker only for a class none of these covers. No implementer runs
  `gh issue create`.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-13 @ 8be4576fd (one test-only commit after the plan's 7f06491a0; the probed `Sharpy.Lsp` / `sharpyc` binaries are the 15:11 / 15:23 builds of that tree)
**Plan file:** `.claude/plans/plan-6ce1a5.md`

Stance: refute. Mechanical check `python3 build_tools/check_plan_refs.py <plan> --issues`: 20 paths ok, 2 missing (both files the plan creates), 15 `file:line` ok, 9 SPY codes ok, 9 symbols not found (all proposed), 12 issues resolved (8 CLOSED, all cited as history; #2242 OPEN `class-tracker lane:tooling reach:program`; #2127, #1099, #2296 OPEN). Every reference a decision rests on was then read by hand (listed under each dimension below).

### Corrections Made
- **Cause 4 / Context / meta-class — the configuration route publishes.** Before: "three of the five editor routes never publish … `ApplyWorkspaceFeaturesAsync` likewise". After: `DidChangeConfigurationHandler.cs:58-77` awaits `ApplyWorkspaceFeaturesAsync` and then publishes every project file's `GetAnalysisAsync` result (unit-bag-only view, `sourceText: null`) — a SECOND project-wide publish site. Two of six routes publish nothing (watched `.spy`, `.spyproj` reload). Task 7 now deletes that loop too (otherwise "one publish site" is false by one).
- **`didClose` route added (cause 4b, contract, matrix, D2 a, Phase 3 matrix, Task 7).** `TextDocumentSyncHandler.cs:87-93` calls `ClearDiagnostics(uri)` for every closed document — a project file's build errors vanish on close. Rule derived from the plan's own contract ("ANY route"): clear only `!IsProjectFile(uri)`; E2E `didClose` cells pin it. `didSave` (`:95-99`) is a no-op — checked, no cell.
- **D1 / Task 4 dedupe rule.** Before: "`CompilerDiagnostic` record equality". After: `DiagnosticBag.GetDeduplicationKey` (Code, Line, Column, Message-when-codeless, Span.Start, Span.Length, FilePath). `CompilerDiagnostic` carries `Data` and `RelatedLocations` reference members, so record equality would leave the double-written type-check phase duplicated whenever a stamp copies differ by reference.
- **Blast radius — `GetFileResult` consumers.** Before: `SymbolFactMirrorConformanceTests`, `QualifiedBareSpellingConformanceTests`, `ImportedStubClassificationReachabilityTests`. After: `grep -rln GetFileResult src/` @ 8be4576fd → `CompilerApi.cs`, `LanguageService.cs`, `ProjectAnalysisResultTests.cs`, `SemanticInfoMergeConformanceTests.cs` only (two places in the plan).
- **Task 2 — `MemberAccess` position names.** Before: `ma.MemberLineStart/MemberColumnStart` "if present, else span arithmetic". After: `MemberNameLineStart` / `MemberNameColumnStart` / `MemberNameColumnEnd` exist (`Expression.cs` ~545, #1503); no fallback arithmetic.
- **Phase table — Phase 5 `Aborted` arm** (`ProjectCompiler.Phases.cs:962` merges the checker's bag into the project bag ONLY, `unit.Phase = Failed`): added as a row, marked unreachable at HEAD (`ContinueAfterError = true`, `FileCompilationPipeline.cs:367`; the one throw site `TypeChecker.Utilities.cs:2872` is behind `!ContinueAfterError`); D1 covers it by construction, no matrix row.
- **Line / path drift:** `LanguageService.cs:206` → `:215` (the `success &&` gate; two sites); `ScopedTypeChecker.cs:35` → `src/Sharpy.Compiler/Semantic/ScopedTypeChecker.cs:36` (the file is in the COMPILER's `Semantic/`, which changes the reach of ledger row 1's fix); D2 a's `IsProjectFile` already reads `_fileResults.ContainsKey` (`LanguageService.cs:354-361`), so the "pre-indexing fallback" in Task 7 is the same predicate, not a second one.
- **Probe scripts parked.** The plan cited `<scratchpad>/p53probe.py`, `p53probe2.py`, `p53e.py`; the scratchpad is per-session and `p53e.py` is not in it. `p53probe.py` and `p53probe2.py` copied to `.claude/tmp/p53-probes/` (gitignored). Close-out command recorded in §Current State.

### Adequacy
- **Contract:** named — per-file editor diagnostics after ANY route equal the build's per-file set, and every spelling of an imported name navigates. Now ranges over `didClose` too.
- **Matrix:** larger than the repro lists — the verifier added `didClose` (route axis), the configuration route's existing publish (route × publisher), the dead `Aborted` arm (phase axis), and the non-project-path cell (file axis; measured ABSENT @ 8be4576fd: a syntax error and a duplicate `class` inside an unlisted `lib.spy` both surface as main.spy-stamped `SPY0313` + `SPY0301` + `SPY0200`, nothing stamped `lib.spy`; the union oracle is its detector; rule recorded). Every dispatch site the plan's fixes touch was enumerated: `TextDocumentSyncHandler` has five `Handle` arms (open/change/close/save + the analyzed callback) — the plan named two; `Program.cs` and `DidChangeConfigurationHandler` are the two project-wide publish loops — the plan named one; the two `ResolveFromMemberAccess` copies (Definition `:65-86`, Implementation `:118-138`) are the mirrored pair the plan names, and `grep -rn "ResolveFromMemberAccess\|GetEffectiveType(ma.Object)" src/Sharpy.Lsp` finds no third.
- **Cure:** seams — D1 one projection, D2 one publication seam, D4 one partitioner, D7 one resolver reading D6's one recorded fact; plus a generator (Phase 1) and the route hand-matrix for the axis the generator cannot reach. Meta-class rows: position/route missing a shared check; qualified/bare divergence; Class F cross-entry-point drift (#1144).
- **Harness:** named — `FrontEndParityTests` (stride default 1 → all 177 multi-file fixtures; `FRONTEND_PARITY_STRIDE` env), new pairs `LSP-project` / `LSP-project-nav` on `frontend-parity-allowlist.txt` (42 live rows today, stale-entry detection at `FrontEndParityTests.cs:185-224`, report `.claude/tmp/frontend-parity-report.json`). Allowlist delta: + the first-run cell table citing #2242, − all of it by Phase 4.
- **Layer:** every decision rung-justified. D6 is node-keyed `SemanticInfo` (Rule 2b) with the LSP-side re-derivation rejected by name (#1328 escape identity, PascalCase fallback, #1933 meta-class); `SetMemberAccessResolution`'s `TypeSymbol` owner (`SemanticInfo.cs:1204`, read as such by `ConstEligibility.cs:460`) is the stated reason not to widen it — confirmed. `MergeFrom` (`SemanticInfo.cs:2135`) is reflected by `MergeFrom_PreservesEveryEntry_Behaviorally`, so the new dictionary is round-tripped automatically and `MinimumSideTableFieldCount` 78 → 79 is the pin. No rung-4 rule anywhere.

### Scope & convergence
- **Group:** one (P53, charter §3.4, order R-EZ / R-GC: after P50, which is DONE @ 7f06491a0) · **Alone:** no — required? no (no printed-output or layout change; no generated artifact) · **Gates:** 1
- **Generator:** built first in Phase 1 (two pairs over the 177-fixture corpus on the existing ratchet); hand matrix only on the server-route axis (Phase 3, `E2E/LspTestClient` + `DrainHelpers`) — stated, and the route axis is genuinely out of the in-process harness's reach.
- **Trackers named:** #2242 (this class), #2127 (path-less), #1099 (cross-link only), #2296 (quick-fix drift); ledger rows for the lead; no implementer `gh issue create`. · **Fidelity rows:** n/a (no Python surface) — correct.
- **Budget:** added to the Scope block (two sweeps, 32-session E2E matrix, two unit matrices, one gate; prober = matrix + ≤2 axes).

### Falsifiability
- `LSP-project` pair (i) union: mutation named (revert the `GetFileResult` hunk → red, N fixtures); exemption not subject; positive control = fixtures whose non-entry-file type errors already attribute at HEAD, `attributedDiagnostics > 0` asserted; by-direction n/a (no refusal).
- `LSP-project` pair (ii) attribution: mutation was MISSING — added: drop the `FilePath` filter in the projection → `misattributed:<code>@<file>` on every multi-file error fixture.
- `LSP-project-nav` sweep: mutation named (remove `SetModuleMemberResolution` → every `m.name` cell red); positive control = `from` cells green under the same mutation; `.NET`/stdlib modules record-and-skip with a stated reason (not silently passed) — adequate.
- `ProjectAnalysisResultTests` phase × file matrix: mutations named (union disabled → import/declaration rows red, type-check row green = the positive control; dedupe disabled → type-check row count 2); D5 test (pre-cancelled token → SPY09xx on the entry file only).
- E2E route × state matrix: three mutations named (re-enable single-file publish for project files → `didOpen/unlisted` red; restore the `success &&` publish gate → every error-state index cell red; disable the partitioner → `dupclass/didOpen` red); add the `didClose` cell's mutation: restore unconditional `ClearDiagnostics` → `didClose/error` cells red. Positive control: `clean` → `[]` on every route.
- `DiagnosticPublisherTests` D4 unit test: absence assertion (no throw) paired with a positive control (same-file in-range span → real range) — adequate.
- `DefinitionModuleQualifiedTests`: mutation named (recording removed → `m.name` red, `from` green); `MergeFrom` copy deleted → `SemanticInfoMergeConformanceTests` red AND project-mode cells red while single-file cells stay green — a discriminating asymmetry.
- Doc paragraph (Phase 5): each sentence maps to a Task 8 cell — execution evidence named.
- No refusal is added by this plan; by-direction record = the whole-solution gate (every cell goes silent → published, never worked → refused). Checked against the §Current State table: every "nothing"/`∅`-where-the-build-says-error cell is a silent-wrong at HEAD.

### Warnings
- **`DidChangeConfigurationHandler` publishes with `sourceText: null`** (line/column ranges only). After D4 that path goes through the partitioner; the E2E `didChangeConfiguration` cells compare code multisets, not ranges, so a range regression on that route would not be seen. If the implementer wants ranges on that route, resolve text through `LanguageService.GetSourceText` there too (one line), else state the line/column-only shape in the doc paragraph.
- **`p53probe2.py` (the no-project control for cause 6) was not re-run** by the verifier; cause 6 is outside this plan's contract (ledger row 1) and the in-project `edit` column was reproduced (below). The lead's new class issue for row 1 should cite `src/Sharpy.Compiler/Semantic/ScopedTypeChecker.cs:36` — the seam is in the compiler, so its fix reaches `Sharpy.Compiler.Tests` and the three GapDiscovery sweeps, not only `Lsp.Tests`.
- **Phase 1 cost:** each `InitializeProjectAsync` is a second full `AnalyzeProject` per fixture (the `compile-project` arm already runs one) plus a temp-directory copy; `FRONTEND_PARITY_DOP` parallelism applies. Report the sweep's wall-clock delta in the Task 3 commit body so a later round can decide on a stride for the LSP pair alone.
- **`DispatchSiteInventoryTests` / `WrapperNodeUnwrapConformanceTests`** scan `src/Sharpy.Lsp` and a new `MemberAccessSymbolResolver.cs` with a `node switch` is exactly what they inventory — the plan already says to run them in the edit loop; run them BEFORE the pre-commit filter, not only in the gate (memory: LSP-only work reddened them twice).
- **Route race under D2 a:** `OnDocumentChangedAsync` is fire-and-forget from the sync handler and cancels on rapid edits ("Expected when document changes rapidly", `TextDocumentSyncHandler.cs:118-128`); the LAST edit's reanalysis must still publish. The E2E `didChange structural` cell should send two edits back-to-back and assert the last notification matches the second edit's build set — otherwise a cancelled reanalysis that publishes nothing leaves the previous squiggles standing with no cell to see it.

### Missing Steps Added
- Task 7: delete/reroute the `DidChangeConfigurationHandler.cs:65-77` republish loop; `Handle(DidClose)` clears only non-project files.
- Phase 1 Task 1: thread `main.features` into the `LSP-project` arm (`SetConfiguredFeatures` or `<Features>` in the generated `.spyproj`).
- Phase 1 acceptance: the attribution oracle's own mutation.
- Scope: the verify-cost / prober-budget line.
- Phase 3 matrix: `didClose` route (×4 states) and the double-edit cell above (warning 5) — the latter is a suggestion, not inserted into the matrix text.

### Unchecked Claims
- The `AnalysisLatencyLog` `project` vs `single-file` ms figures (D2's cost) — a Phase 3 measurement by design; the class `src/Sharpy.Lsp/AnalysisLatencyLog.cs` exists.
- "`TryResolveClrSubNamespaceOrType` … binds a sub-module or CLR type symbol" — the sub-namespace arm constructs a `ModuleSymbol` and registers it in `Exports` (`TypeChecker.Expressions.Access.cs:3787-3817`); the CLR-type arm (`:3821+`) was not read to the end. The implementer confirms which symbol it has to record before Task 9.
- `ImportOutsideSourceSetEditorTests.Definition_…`'s "not a row" comment is at line 56 (confirmed); that the `plain` row then passes after Phase 4 is Phase 4's acceptance, not verified here.
- `E2E/MultiFileTests.cs:229` comment confirmed (lines 229-231); whether `WaitForDiagnosticsMatchingAsync` can express "LAST notification after drain" or needs a `DrainHelpers` extension was not checked — read `DrainHelpers.cs` before Task 8.

### Measured by the verifier @ 8be4576fd
`python3 -I .claude/tmp/p53-probes/p53probe.py` against the 15:11 `Sharpy.Lsp` build (sandbox off) reproduced the plan's §Current State table cell for cell:

| Cell | index | open | edit | def | hover |
|---|---|---|---|---|---|
| from/missing | (none) | `main.spy=SPY0300` | `SPY0300,SPY0451` | None | `build: <?>` |
| from/unlisted | (none) | `main.spy=∅` | `SPY0300,SPY0451` | lib.spy:7:4 | signature |
| from/listed | `lib.spy=∅; main.spy=∅` | `∅` | `SPY0300,SPY0451` | lib.spy:7:4 | signature |
| plain/missing | (none) | `SPY0300` | `SPY0300,SPY0451` | None | `build: <?>` |
| plain/unlisted | (none) | `∅` | `SPY0300,SPY0451` | **None** | signature |
| plain/listed | `∅; ∅` | `∅` | `SPY0300,SPY0451` | **None** | signature |
| from/listed+dupclass | (none) | (none) | `SPY0300,SPY0451` | lib.spy:7:4 | signature |
| plain/listed+dupclass | (none) | (none) | `SPY0300,SPY0451` | None | signature |

Control (`sharpyc project`, same tree): unlisted `lib.spy` → `error[SPY0313] … (in main.spy)` at `main.spy:1:1`, `Build FAILED`; listed `lib.spy` with a syntax error → `SPY0104` stamped `lib.spy:1:29` (the parse phase writes both bags — the attribution positive control exists at HEAD).

## Implementation Checklist

**Filing (wave 1):** #2301 filed for ledger row 1 (BodyOnly re-check, re-measured @ c245ece62 with no project: body edit → SPY0300,SPY0451), cross-linked on #1099; generic-reference escape-identity cell → #2131 comment; non-public CLR lazy lookup cell → #2175 comment; #2127: 0 unstamped cells met.

**Lead decisions during implementation (2026-10-13):**
- [CORRECTED: corpus] The multi-file corpus is **150** fixtures, not 177: `find -name main.spy` counted crash-dump copies under `imports/from_import_alias_type/bin/` and seven single-file fixtures that sit in their own directory. The close criterion reads `multiFileFixtures = 150` @ sha.
- [CORRECTED: Phase 4 guard] The editor never reads through `MergeFrom`: `GetFileResult(path).SemanticInfo` is the unit's own per-file instance, and so is `CompilerApi.Analyze*`. Deleting the `MergeFrom` copy reddens `SemanticInfoMergeConformanceTests` and the merged-view row of `ModuleMemberResolutionFactTests`, not the LSP project-mode cells. Phase 4's asymmetry claim is replaced by that measured record (95d14ce2b).
- [ADDED: in-contract cells] Four `from` navigation cells fail at a generic-call, type-argument or type-alias-call position (`Slot[int, str](..)`, `Box[int](5)`, `widest[Shape]`, `Handle("42")`). The contract ranges over every spelling of an imported name, and no other LSP navigation tracker exists, so they are this plan's work in Phase 4, fixed at the binding seam, with a position axis added to the Task 11 matrix.
- [ADDED: oracle rule] Four `from` cells sit in programs that refuse the binding (three circular-import fixtures, one bare reference to an escape-declared export). If the checker binds the name there, the cell must navigate. If it does not, the nav oracle skips cells in fixtures whose expected error is a binding refusal, keyed on an enumerated code list and never on the navigation result, and reports the skip count.


Base sha: 8be4576fd (verify-plan stamp). Baseline gate: 79531/0/4 @ 8be4576fd (measured, `.claude/tmp/p53-impl/baseline-gate.log`). Lead coordinates; wave 1 agents run in isolated worktrees and the lead cherry-picks onto `dev`.

- [x] T1–T3 Phase 1 generator — f222a745d, fb38aff2a, 5f728ec19. First run @ 8be4576fd: `LSP-project` 8/150 fixtures red (SPY0302 ×3, SPY0300, SPY0301, SPY0204, SPY0484 ×2 — warnings count), 0 misattributed, 0 unstamped, `attributedDiagnostics` 70 (41 non-entry); `LSP-project-nav` 96 red (88 `m.name`, 8 `from`), 155/163 `from` green, 81 skipped with reason. Attribution mutation: 40 offenders (24 `misattributed`).
- [x] T4 + T5 matrix — 3b443677d, 957a495e5; D5 aligned with the compiler's default entry c245ece62 (lead). In-contract addition: the per-file view now applies the project's `<NoWarn>`/`<WarningsAsErrors>` policy (it showed suppressed warnings and unpromoted ones at HEAD). Mutations: union off 14 red; dedupe off 9 red; FilePath filter off 18 red (lead re-run, narrower variant: 16 red); policy off 2 red.
- [x] T6 publisher partitions by file D4 — lsp-expert (worktree) — 5dd5acb45 (mutations: partition off → 3 red; range guard off → 1 red). Hazard handed to T7: the single-file route now publishes foreign partitions on their own URIs, never clears them, and can overwrite an open sibling's own squiggles.
- [x] T9 module-member fact D6 — 95d14ce2b. 8 recording sites incl. `GenericReferenceResolver.cs:353/:383` (a second binding site the plan missed). Mutations: all calls removed → 5/5 fact tests red; MergeFrom copy removed → merge conformance + merged-view row red, per-file/single-file rows green.
- [x] T5 drain — 173345ef9 (8 stale rows, exactly the LSP-project block; pair at 0 rows).
- [x] T7 one publication seam D2 a / D3 (+ didClose, config loop, foreign-partition rule) — lsp-expert (worktree) — 53f47b03a. `LanguageService.ProjectFilesAnalyzed` raised after every completed analysis (success or not) + `RepublishProjectDiagnosticsAsync`; one subscriber `DiagnosticRoutes` (DI singleton resolved before indexing). Both old loops deleted (Program.cs onComplete, DidChangeConfigurationHandler → republish call). **Suspected notification-token bug CONFIRMED and fixed**: the didChange token cancelled every reanalysis, and the compiler reports cancellation as a SPY0901 result, so a cancelled result was cached as the project analysis; now a server-lifetime token + supersede scope, cancelled → publish nothing, escaped exception → SPY0909 on UnattributedTarget. In-contract addition: a watched `.spy` outside the source set under the project dir/module path reanalyzes (missing → created module turns SPY0300 into SPY0313, as `sharpyc project`). Latency (12 structural edits): project median 40.5 ms vs single-file 35.6 ms + its 300 ms debounce.
- [x] T8 route × state E2E matrix + route drain — lsp-expert — fce79bf92. 11 routes × 4 states = 44 cells green (the 8 briefed + open-during-indexing, config-hints-only, didOpen-non-project-importer). Mutations: single-file re-enabled for project files 6; `success &&` gate 15 (lead re-run, broader variant gating the raise in InitializeProjectAsync: 43/58 red = every error-state cell + spyproj-reload/clean whose post-state is unlisted; every clean cell green; restored 58/58); partitioner off 7 (plan's predicted dupclass/didOpen cell correctly stays green — no project route hands the publisher a foreign diagnostic now; caught by the non-project-importer cell); clear-on-close 3; post-analysis cancellation check 1 (first version vacuous, fixed); ownership check 2; clear-on-disappear 2; token passed back 4. p53probe @ 4ab122c3b: from/unlisted → main.spy=SPY0313; listed+dupclass → lib.spy=SPY0204, main.spy=∅.
- [x] T10 MemberAccessSymbolResolver D7 (+ generic-position cells) — lsp-expert (worktree) — 4c7854292 (resolver; Definition/Implementation/Hover routed; `ReResolveInTable` same-declaration check) + ebf7c9cd0 (Semantic: generic-call/type-arg/type-alias binding recorded; `TypeAliasSymbol` gets its span+file; SPY0308 circular-import check ignores generic-reference callee/type-arg names so no verdict moves — fixture `imports/circular_import_type_argument`; InlineProvider declines a type-argued callee so code-action pins stay exact). Single-file control: generic positions answered nothing in ONE file too → the seam is the binding, not imports. Mutations: (a) D6 step skipped → 32 matrix + parity red (lead re-run: 33 failed/81 — 28+4 matrix + the parity test with 50 nav cells listed; restored 81/81); (b) generic recording reverted → 15 red; (c) alias span/file reverted → 10 red; (d) cycle guard removed → new fixture red SPY0308; (f) name-only re-lookup → `b.Point` implementation red.
- [x] T11 navigation matrix + nav drain — lsp-expert — 8484f7687. `DefinitionModuleQualifiedTests` 80 cells (def/impl/hover × 8 `m.name` spellings × listed/unlisted; × 4 `from` positions × listed/unlisted; 4 single-file controls; 4 same-name-two-modules). Refused-binding oracle: skip only when `main.error` exists AND the build reports SPY0302 anywhere or SPY0200/SPY0203 on the cell's line — 5 skips (3 circular + 2 backtick-escape). Sweep @ 8484f7687: 151 multi-file fixtures (new cycle fixture), 248 nav cells (161 `from`, 87 member), 0 violations, nav allowlist rows 96 → 0. Mutation (e) skip removed → exactly the 5 refused cells red. Mirrored sites not routed: References/highlight/rename/call-hierarchy-prepare have NO member-access arm (ledger row; routing only Definition would make rename edit `def build` but not `lib.build`). Lsp.Tests 21863/0/0 @ f8ac0cff5 (worktree).
- [x] T12 doc paragraph — lead — 202a78e72 (`docs/tooling/lsp-server.md` §Diagnostics "In project mode"; each bullet mapped to a T8/T5 cell in the commit body).
- [x] Final verification — lead. Gate **79714/0/4 @ 202a78e72 (measured)** (Core 3989, Stdlib 4188, Cli 449+1, Lsp 21923, Compiler 49165+3; baseline 79531/0/4 @ 8be4576fd → +183). Parity report @ 202a78e72: LSP-project 151/151, LSP-project-nav 248/248 (161 from + 87 member), unstamped 0, nonAllowlisted 0, stale 0, `multiFileFixtures 151` [CORRECTED: close criterion — 150 + the new `imports/circular_import_type_argument` fixture from ebf7c9cd0]; remaining 42 violations are the pre-existing drain-exempt `compile::` rows. InteropConformance / MetamorphicCorpus (sweep + invariance) / DifferentialExecution passed inside the gate. format whitespace --verify-no-changes EXIT 0; check_spy_staleness 0; check_spy_tests_staleness 0; check_allowlist_issue_state 0. p53probe @ 202a78e72: from/unlisted index main.spy=SPY0313; listed+dupclass lib.spy=SPY0204, main.spy=∅; definition lib.spy:7:4 on every cell whose module exists. Standalone verification-expert (opus) vs base 8be4576fd: targets 1 (Semantic recording: 18 programs × run/emit diagnostics/emit csharp byte-identical to base; LSP refused→worked only), 2 (resolver: no base answer lost) and 5 (inline: f(3) withdrawn when f[int](4) exists — correct direction, the type-argued use now counts) NOT REFUTED. **REFUTED 3**: didClose after an unsaved edit kept the discarded buffer's diagnostics (base cleared) — regression of 53f47b03a, IN CONTRACT, fixed bd42bbfd0 (reanalyze from disk when the closed buffer ≠ disk; E2E didClose-after-unsaved-edit + edit-then-immediate-close × 4 states; mutation drop-reanalysis → 9 red, lead re-run 9/67 red exactly, restored). **REFUTED 4**: doc claimed parity with `sharpyc project`; the editor runs analysis only (no codegen) → narrowed 79a443851; close bullet + #2304 limit f33b528f9. Pre-existing cells filed: buffer-export overlay → #2304 (new class); implementation overrides/interface null + listed dup → #2302 comment; type-argued fn from method spellings → #2151 comment; fn-scope alias H(3) → #1925 comment. Gate re-run **79723/0/4 @ f33b528f9 (measured)** (Lsp 21932); format whitespace --verify-no-changes 0; parity @ f33b528f9 LSP-project 151/151, LSP-project-nav 248/248, stale 0.
- [x] T13 close-out on #2242 — lead — comment 6160551643; `/close-issues` next.
<!-- /close-issues 2026-10-13: #2242 VERIFIED in code @ f33b528f9 (both cells fixed, matrix covered) but NOT closed — the 19 commits are unpushed on dev and /verify-implementation has not run (P47/P50 precedent: close after verify-implementation + push). -->
