<!-- Verified by /verify-plan on 2026-10-14 @ 27e261684 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: built-first (Phase 1) -->

# P46 — CLI input totality: every path and option on every command is honoured, refused, or documented

## Context

`sharpyc` has eleven commands (`build`, `compile`, `run`, `project`, `emit {tokens, ast, csharp,
parse, diagnostics, hover}`, `cache {clear, info}`, `explain`, `lsp`, `repl`, `format`, `server`)
and nine recursive global options. Issue #2173 established the contract for `compile` only: a
parsed option is honoured, refused for that input kind with a message, or documented as ignored —
never silently dropped — and a missing input path is an error. `compile` carries it as ONE
option-to-effect table (`CompileCommand.OptionEffects`, two columns `{.spy, .spyproj}`), totality-
checked by `CompileOptionEffectTableTests`. Issue #2223 (class tracker, `lane:silent-wrong`
`reach:program`) is the continuation beyond `compile`'s table: the other commands have no table,
user references are not deployed beside the output, a `.spyproj` `<Reference>` naming nothing is
accepted, and every non-`compile` command parses a `.spyproj` as Sharpy source. Its `-m` cell was
split out and fixed as chore #2233 (R-EL, closed; the fix is ae367e782 `fix(project): -m / <ModulePath>
search paths reach import resolution` [CORRECTED: 9af04978b is a docs(deviations) commit for #2224/#2228]).

The round charter (`.claude/plans/remediation-round-2026-09-20-batching.md` §3.5) places this as
**P46 — CLI input totality** (Cli + project-file parser), generator FIRST, directly after P53.
Batching comment (authoritative placement and ruling):
https://github.com/antonsynd/sharpy/issues/2223#issuecomment-5981850634 (R-EK: `<HintPath>` is
honoured); the `emit diagnostics -m` cell:
https://github.com/antonsynd/sharpy/issues/2223#issuecomment-6008445947.

## Current State

Measured 2026-10-14 @ 27e261684 (tree clean; `src/Sharpy.Cli/bin/Debug/net10.0/sharpyc` built at
01:44 from that commit). Probe workspace: `<scratchpad>/p46/ws/` — the scratchpad of session
`1f79f3b7-7872-4f83-8419-b96e11304627`, i.e. `/private/tmp/claude-501/-Users-anton-Documents-github-sharpy/1f79f3b7-7872-4f83-8419-b96e11304627/scratchpad/p46/ws/`
[CORRECTED: `<scratchpad>` is per session; the verifier re-ran cells (a) `compile miss2.spyproj` exit 0 +
assembly, (b) `project plain.spy` "Unexpected error", (c) `emit tokens --metrics-format json` no JSON,
(d) `emit diagnostics -r` "'-r' was not matched" from it @ 27e261684 — all reproduce] (a `Greeter.dll` library compiled
from `lib/greeter.spy`, `src/main.spy` importing it, `plain.spy`, and the `.spyproj` files named
below). Every cell below was executed; "code-read" marks the two that were not.

### Axis 1 — option × command: tables exist only for `compile`

| Command | Globals it reads (`GlobalOptions`) | Globals parsed and silently dropped @ 27e261684 |
|---|---|---|
| `build`, `compile`, `run`, `project` | all nine | none |
| `emit csharp` | log-level, log-file, verbose, warn-as-error, nowarn, max-errors, enable-feature | `--metrics-format`, `--metrics-output` |
| `emit diagnostics` | log-level, log-file, verbose, warn-as-error, nowarn, max-errors, enable-feature | `--metrics-format`, `--metrics-output` (measured: `--metrics-format json` prints nothing) |
| `emit tokens`, `emit ast`, `emit parse`, `emit hover` | log-level, log-file, verbose, max-errors | `--metrics-*`, `--warn-as-error`, `--nowarn`, `--enable-feature` (measured: `emit tokens --metrics-format json`, `emit ast --nowarn SPY0451` run as if absent) |
| `repl` | log-level, log-file, verbose, enable-feature | `--metrics-*`, `--warn-as-error`, `--nowarn`, `--max-errors` |
| `server` | log-level (via `globals.ResolveLogLevel`, which folds `--verbose` into `Info`), log-file | `--metrics-*`, `--warn-as-error`, `--nowarn`, `--max-errors`, `--enable-feature` [CORRECTED: `--verbose` IS honoured on `server` as the log-level raise (`ServerCommand.cs:35` → `GlobalOptions.cs:65-74`); only `ShowDiagnosticProvenance` is not set — roster it `Honoured` with evidence `GlobalProbe:--verbose`, or `Honoured` with a reason naming the provenance half, not `Ignored`] |
| `lsp` | log-level (forwarded to the server) | the other eight |
| `format`, `explain`, `cache clear`, `cache info` | **none** | all nine (measured: `format --check plain.spy --log-level Info --log-file fmt.log` creates no file; `explain SPY0300 --warn-as-error` and `cache info --log-level Debug` run as if absent) |

`--enable-feature <unknown>` is refused at parse on every command (the global validator in
`GlobalOptions`), so that one cell is already a refusal everywhere. No command other than `compile`
documents any of its non-honoured cells in `--help`.

Own-option declaration gaps (undeclared options are parse errors — refusals by the contract — but
two leave an analysis command unable to analyse its input):

| Command | Measured |
|---|---|
| `emit diagnostics src/main.spy -m lib` / `-r Greeter.dll` | `'-m' was not matched` / `'-r' was not matched`, exit 1; without them, `emit diagnostics src/main.spy` is `error SPY0300: Cannot find module 'Greeter'` — an entry importing a referenced library cannot be analysed (false-refusal, the #2247-probe cell) |
| `emit hover` | declares neither; `EmitHover` calls `api.Analyze(source)` with no file path (`EmitCommand.cs:412-414`), so sibling-file imports are not resolved either — code-read; Phase 1 measures it |
| `project app.spyproj -m lib` / `-r …` | `'-m' was not matched`; `compile app.spyproj -m/-r` honours both (`CompileCommand.CompileProject` merges them into the project's items) — the two project commands diverge |
| `emit tokens|ast|parse -r …` | parse error; lexing and parsing need no references — a documented omission |

### Axis 2 — input kind × command: only `compile` dispatches on the extension

| Command × `.spyproj` input | Measured @ 27e261684 |
|---|---|
| `build app.spyproj -o …`, `run app.spyproj` | `Warning: Input file 'app.spyproj' does not have .spy extension.` then `error[SPY0013]: Indentation must be multiple of 4 spaces (found 2)` at `app.spyproj:2:1` — the XML is lexed as Sharpy; exit 1 |
| `emit csharp|diagnostics|tokens app.spyproj` | the same SPY0013 (no warning on `emit`); exit 1 |
| `format --check app.spyproj` | `Error: app.spyproj: syntax errors prevented formatting / SPY0013 …`; exit 2 |
| `project plain.spy` | `Unexpected error: Data at the root level is invalid. Line 1, position 1.` (the XML loader's exception through `catch (Exception)`); exit 1 |
| `compile plain.spy` / `compile app.spyproj` (control) | both honoured — `CompileCommand.Configure` classifies by extension (`InputKind`) and dispatches |

Seam: `CliHelpers.ValidateInputFile` (`CliHelpers.cs:328-347`) is the shared gate of `build`,
`run`, `compile <file>` and the `emit` subcommands; it refuses a missing file and only **warns**
on a non-`.spy` extension. `format` and `project` have their own input handling and no extension
check at all. Lane for this axis: message · program.

### Axis 3 — reference source × state × effect

| Cell | Measured @ 27e261684 |
|---|---|
| `-r does/not/exist.dll` on `compile` (both kinds), `build`, `run`, `emit csharp` | `error[SPY0305]: Assembly not found`, exit 1 — fixed by #2173 (`CompileOptionEffectTableTests.AReferenceThatResolvesNowhere_IsSpy0305`) |
| `.spyproj` `<Reference Include="does/not/exist.dll"/>` on `compile miss2.spyproj -o out/App.dll` and `project miss2.spyproj` | **exit 0, assembly written, no diagnostic** (control `ctrl.spyproj` without the item: exit 0; `-r does/not/exist.dll` on the same project: SPY0305 exit 1) — silent-wrong |
| `.spyproj` `<Reference Include="Greeter"><HintPath>/abs/lib/Greeter.dll</HintPath></Reference>` on `compile` and `project` | `error[SPY0300]: Cannot find module 'Greeter'` — the parser reads only `Include`, roots `Greeter` at the project directory, nothing reports the unresolved path, and the import fails (false-refusal; R-EK rules the cure) |
| `compile src/main.spy -r /abs/lib/Greeter.dll -o outA/main.dll`, then `dotnet outA/main.dll` | exit 0; `outA/` holds `Sharpy.Core.dll`, `main.deps.json`, `main.dll`, `main.runtimeconfig.json`, `libe_sqlite3.dylib` — **no `Greeter.dll`**; run: `Unhandled exception. System.IO.FileNotFoundException: Could not load file or assembly 'Greeter, …'` — runtime-throw |
| `compile app.spyproj -o outB/App.dll` (`<Reference Include="/abs/lib/Greeter.dll"/>`), then `dotnet outB/App.dll` | exit 0; `outB/` holds every default stdlib assembly (`Sharpy.Stdlib.dll`, `MathNet.Numerics.dll`, `Tomlyn.dll`, `YamlDotNet.dll`, the SQLite set) — **no `Greeter.dll`**; run: the same FileNotFoundException |
| `run src/main.spy -r /abs/lib/Greeter.dll` | compiles, `=== Running Program ===`, then the same FileNotFoundException; exit 134 |
| `project app.spyproj`, then `dotnet bin/Debug/net10.0/App.exe` | exit 0; `bin/Debug/net10.0/` holds `App.exe`, `App.pdb`, `App.deps.json`, `App.runtimeconfig.json` only — not even `Sharpy.Core.dll`: `FileNotFoundException: … 'Sharpy.Core, Version=0.22.1.0'`. `project` has never called `RuntimeDependencyHelper` (`git log -S CopyRuntimeDependencies -- src/Sharpy.Cli/Commands/ProjectCommand.cs` is empty) |
| `build src/main.spy -r … -o outC/main.dll` | exit 0; `outC/` holds the assembly, pdb, deps.json, runtimeconfig only (`build` never copies dependencies — `BuildCommand.Configure` returns straight after `CompileToBinary`) |
| `compile --self-contained` / `run --self-contained` with a user reference | code-read: `SelfContainedPublisher.Publish` (`SelfContainedPublisher.cs:67-80`) references only the used assemblies it finds **in the CLI's own directory** (`Path.Combine(cliDir, fileName)` + `File.Exists`), so a `-r` library elsewhere is silently dropped from the wrapper project; Phase 4 measures it once (the publish is slow) |

Seams (read @ 27e261684):

- **Registry errors on the project route.** `Compiler`'s constructor loads every
  `_options.References` through `ModuleRegistry.LoadReference`, which records SPY0305 on the
  registry's own bag and returns false (`ModuleRegistry.cs:70-80`); the constructor logs a warning.
  `Compiler.Compile` (`Compiler.cs:263-265`) and `Compiler.Analyze` (`Compiler.cs:177-178`) merge
  `_moduleRegistry.Diagnostics` into their result; **`Compiler.CompileProject`
  (`Compiler.cs:110-144`) does not** — the third arm of a mirrored structure. `CompilerApi`'s
  single-file door (`CompilerApi.cs:131-153`) refuses on SPY0305 only (SPY0306 "already loaded"
  fires in long-lived processes and the compile goes on to succeed — fa7e8aa63's finding; keep that
  filter). The Roslyn step drops a non-existent reference with a log warning
  (`AssemblyCompiler.cs:423-434`) and `BuildReferenceClosure` skips it (`ProjectCompiler.Phases.cs:800-806`),
  so nothing downstream reports it either.
- **`<HintPath>`.** `ProjectFileParser.Load` reads `Include` only
  (`src/Sharpy.Compiler/ProjectConfig.cs:390-402`), rooting a relative value at the project
  directory. `ProjectFileParser` is the ONE `.spyproj` parser (CLI `compile`/`project`/server,
  LSP `LanguageService.InitializeProjectAsync`, `ProjectCompilationHelper.Compile`). The test
  helper's `WithRuntimeReferences()` and the compiler's own `TestProjectScaffold.BuildRuntimeReferences`
  write `<Reference Include="Sharpy.Core"><HintPath>…</HintPath></Reference>` (used by
  `SourceGeneratorTests` ×10, `ClrBaseOverrideRouteMatrixTests` ×1 and `DiagnosticFileProvenanceTests` ×1
  [CORRECTED: `grep -rc WithRuntimeReferences src/Sharpy.Compiler.Tests` @ 27e261684; the provenance
  test was missing from the list and from the control run]), so today those projects carry
  references that resolve nowhere and are silently dropped — the blocker #2223 named, resolved by
  R-EK (a): honour `HintPath`.
- **Deployment.** `RuntimeDependencyHelper.CopyRuntimeDependencies(outputDir, usedAssemblyPaths)`
  seeds the closure from `Sharpy.Core` plus `usedAssemblyPaths`; callers: `CompileCommand.CompileSingleFile`
  (`compileResult.UsedAssemblyPaths`), `CompileCommand.CompileProject` (the default references,
  "conservatively"), `RunCommand.HandleRunCommand` (`UsedAssemblyPaths` from the in-process compile
  or the server). `UsedAssemblyPaths` is `ModuleRegistry.GetUsedAssemblyPaths()`, written only by
  `RecordModuleUsage`, which the three **module** arms call (`GetModuleFunctions`/`GetModuleTypes`/
  `GetModuleFields`, `ModuleRegistry.cs:128/152/177`). A `-r` Sharpy library's `from Greeter import
  Greeter` resolves through the **CLR-namespace** arms (`IsNetNamespace`/`GetNamespaceTypes`/
  `TryResolveNetType` via `ImportResolver.ModuleLoading.cs:942,1033`, searching
  `AppDomain.CurrentDomain.GetAssemblies()`), which never record usage — so a user reference is
  never "used" and never copied, on every route. (Parallel-site class: three arms record, two do
  not.)
- **Input kind.** `CliHelpers.ValidateInputFile` warns; `ProjectFileParser.Load` throws
  `XmlException` on a `.spy`, caught by `ProjectCommand.CompileProject`'s `catch (Exception)` as
  "Unexpected error".

### Standing harnesses that should have caught this

`CompileOptionEffectTableTests` (compile only — by construction it cannot see another command's
table); `OptionGrammarConformanceTests` walks every command of the real root but checks one
grammar property (`AllowMultipleArgumentsPerToken`); `PathOptionArityTests` lists the `-r`/`-p`/`-m`
sites by hand (eleven `(command, option)` pairs, so the missing `emit diagnostics`/`project` sites
are invisible to it); `StandaloneDeploymentTests` runs standalone programs that import **stdlib**
modules only — no cell has a user reference. None has a totality axis over commands. Phase 1 adds
one. [ADDED: missing step] `docs/design/gap-discovery-contracts.md:42` lists "deployment closure
(`StandaloneDeploymentTests`)" among the well-guarded classes needing no new mechanism — this plan
shows the guard has no user-reference cell; Phase 4 Task 2 amends that line to name
`ReferenceDeploymentMatrixTests` as the user-reference axis, and Phase 1 Task 2 adds a roster row
for `CliOptionEffectSweepTests` (contract, harness, allowlist file, umbrella #2223), since every
allowlist-ratchet harness is rostered there. The new allowlist needs no registration with
`check_allowlist_issue_state.sh`: it scans every tracked `*allowlist*.txt` (`allowlist_issue_state.py:185-194`),
so the file is covered the moment it is committed — and its rows must cite `#2223` while it is OPEN.

Outside this contract, found by the probes (findings ledger — the lead files by class, §10): an
exe `.spyproj` whose only `<SpyFile>` is `plain.spy` at the project root (`rootctrl.spyproj`) is
`error[SPY0908] … CS5001: Program does not contain a static 'Main' method` on `compile` and
`project` (SPY0908 as a net — the entry module is not detected and no Sharpy diagnostic names
it); lane ice · program; no open tracker names it (#1387 is closed). And fa7e8aa63's recorded cell:
in one process an assembly loaded by an earlier compile satisfies a later compile's import without
`-r` (SPY0306 at the registry, compile succeeds) — a #2250-adjacent process-state cell, already in
that tracker's family.

## Scope

- **Group:** P46 — CLI input totality (Cli + project-file parser); chores riding along: none
- **Alone:** no — no printed program output or emitted C# changes; files beside an output are
  added (never removed), `--help` text gains generated notes, and new refusals are listed by
  direction in §Adversarial Review
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`,
  one line per cell: cell · program · observed · expected @ sha · lane · reach · candidate class ·
  found-by); the lead files by class (contract §10); trackers named at the end of this plan
- **Override:** none

## Defect Class

- **Violated contract:** every CLI input that names a path or an option is honoured, refused for
  that input kind, or documented as ignored — uniform across the command axis (not only `compile`),
  the reference-source axis (`-r`, `<Reference Include>`, `<Reference>` + `<HintPath>`), and the
  input-kind axis (`.spy`, `.spyproj`).
- **Meta-class:** position/route missing a shared check (the option table, the input-kind gate, the
  registry-error merge and the deployment seed each exist on one route and are absent on its
  siblings) + silent wrong behaviour (an accepted missing reference, an undeployed reference) →
  **standard cure:** move each check to ONE shared seam and build a route-table × axis matrix so a
  new command or route cannot skip it; fix first, then the sweep over the sibling cells.
- **Known member cells:** #2223 (the tracker: user references not copied; `.spyproj` `<Reference>`
  naming nothing accepted; `build`/`run`/`emit` parse a `.spyproj` as Sharpy; option tables only
  for `compile`; `emit diagnostics -m` not matched). Cell 1 (`-m` import resolution) was #2233,
  closed.
- **Sibling cells this plan must also cover** (the issue's list is a symptom report):
  - option × command: every `(command, option)` pair of the real root grammar — 15 commands
    (`build`, `compile`, `run`, `project`, six `emit`, two `cache`, `explain`, `lsp`, `repl`,
    `format`, `server`) × (own options + 9 globals) ≈ 185 cells, each `Honoured | Refused |
    Ignored` with evidence (table §Current State axis 1 lists the measured drops);
  - input kind × command: `{.spy, .spyproj}` × `{build, run, emit tokens|ast|csharp|parse|
    diagnostics|hover, format, project, compile (control)}`;
  - reference source `{-r, <Reference Include>, <Reference>+<HintPath>}` × state `{resolves,
    missing}` × route `{compile .spy, compile .spyproj, run, run --server, project, build,
    compile --self-contained, run --self-contained}` × effect `{SPY0305 before anything is written;
    deployed beside the output unless --no-deps; the self-contained wrapper references it}`, with
    `HintPath` × `{absolute, relative to the .spyproj directory}`;
  - the option-declaration axis: `-r`/`-m` on `emit diagnostics`, `emit hover`, `project`
    (honoured through the same seams as `emit csharp` / `compile .spyproj`), and `emit hover`'s
    input path reaching import resolution;
  - every door that builds a `ModuleRegistry` from a `ProjectConfig` surfaces SPY0305. The doors
    @ 27e261684 are exactly three [CORRECTED: there is no `CompilerApi.CompileProject`; the grep
    `BuildModuleRegistry(\|new ModuleRegistry(` hits `CompilerApi.cs:131` (`CompileAsSyntheticProject`,
    already refuses), `CompilerApi.cs:635` (`GetOrBuildAnalysisContext`, the cached registry behind
    `CompilerApi.AnalyzeProject` — the LSP's project door, `LanguageService.cs:710`) and
    `Compiler.cs:68` (the constructor, behind `Compiler.CompileProject` ×2 overloads and the single-file
    `Compile`/`Analyze`)]: `Compiler.CompileProject` (does not consult the registry's errors — the
    silent arm), `CompilerApi.CompileAsSyntheticProject` (refuses SPY0305 before compiling — the model
    arm) and `CompilerApi.AnalyzeProject` (does not — the LSP project-mode arm; its file-less SPY0305
    is published on `ProjectAnalysisResult.UnattributedTarget`, the #2242 D3/D5 rule the publisher
    already reads, so project mode gains the diagnostic with no new placement rule).
- **Lane / reach:** silent-wrong · program (deployment, missing `<Reference>`); false-refusal ·
  program (`HintPath`, `emit diagnostics -r/-m`); message · program (input kind); runtime-throw ·
  program (FileNotFoundException at run). **Fidelity-scope row:** N/A — no Python surface.
- **Standing harness that should have caught it:** none with a command axis; Phase 1 adds
  `CliOptionEffectSweepTests` (reflective totality over the real root grammar, allowlist ratchet)
  and Phase 4 adds `ReferenceDeploymentMatrixTests` (E2E, reference source × state × route).
- **Generator:** REQUIRED FIRST — the option × command matrix is 15 × ≥9 (two axes, ≥3 values
  each): Phase 1 builds the reflective sweep and its allowlist, and Phases 2–5 drain it. The
  reference and input-kind axes are closed hand matrices (the charter says so): their values are
  finite and spelled by the CLI's own grammar, so a generator would enumerate what the hand matrix
  already lists; the axis a generator cannot enumerate is the **effect** of an honoured option
  (what observable changes), which only a per-option probe can judge.
- **Owner rulings:** R-EK (a) — `<Reference>` with a `<HintPath>` child takes the path from
  `HintPath` and the assembly name from `Include`; without it `Include` is the path, as today; an
  unresolved reference is SPY0305 on the project route; the spec gains a `<Reference>` section
  (https://github.com/antonsynd/sharpy/issues/2223#issuecomment-5981850634). R-EL — the `-m`
  import-resolution cell was chore #2233 (done). Do not re-ask either.

## Adversarial Review (pre-mortem)

- **Alternative root cause:** for the deployment cell, "the copier filters user assemblies" vs
  "the registry never records them as used". Discriminating probe: `sharpyc compile src/main.spy
  -r Greeter.dll --log-level Debug` and read `[DEBUG]` for `Loaded module reference: Greeter` (it
  IS loaded) while `GetUsedAssemblyPaths()` (dump via a one-line test on
  `CompilerApi.Compile(...).UsedAssemblyPaths`) lacks it — the registry never recorded it, because
  `from Greeter import Greeter` resolved through `GetNamespaceTypes` (`ImportResolver.ModuleLoading.cs:1033`),
  not a module arm. The plan does not depend on which arm resolves a reference: the deployment set
  is seeded from the explicit reference inputs (Design Decision 4), so a future resolution arm
  cannot reopen the cell.
- **Alternative layer:** the layer ladder is about Core/Stdlib behaviours; nothing here is a
  language behaviour. The lowest rung for the `.spyproj` reference fact is the ONE parser
  (`ProjectFileParser`, shared by CLI and LSP) — not per-command parsing; for the registry error it
  is ONE helper on the registry consumed by every door, not a per-door filter; for input kind it is
  ONE classifier in `CliHelpers`, which `compile` already has inline.
- **How the fix could be inert:**
  - SPY0305 surfaced on `Compiler.CompileProject` but the CLI's `compile .spyproj` still runs
    `CliHelpers.ResolveReferences`/the registry check for `-r` only (`CompileCommand.cs:385-411`)
    — the `.spyproj`'s own items never pass through it; the merge must be read by
    `result.Diagnostics` (it is — `result.Success` + `GetErrors()`), and the test uses a
    `.spyproj` item, not `-r`.
  - `HintPath` honoured by the parser but `SharpyWorkspace`'s project-identity comparison
    (`SharpyWorkspace.cs:610`, `SameSequence(a.References, b.References)`) or
    `DefaultReferences.ApplyTo`'s `Contains` dedupe sees a different spelling of the same file
    (`Path.GetFullPath` the `HintPath` the way `Include` is rooted today).
  - Deployment seeds extended in `RuntimeDependencyHelper` but `run --server` copies from the
    server's `UsedAssemblyPaths` (`RunCommand.cs:189-199`) — the seed must be formed in `run` from
    its own `references` on both branches; the matrix has a `run --server` row.
  - The option sweep green because every undocumented global cell is rostered `Ignored` with a
    help note — that IS the contract (documented), but a cell that should be honoured
    (`--metrics-format` on `emit csharp`, `-r`/`-m` on `emit diagnostics`) must not be rostered
    `Ignored` to pass: Design Decision 1's rule (a command that compiles or analyses honours every
    compiler-facing global) is asserted by the sweep from the command's `Performs` flag, so an
    `Ignored` cell on a compiling command is red.
- **Before/after by direction** (contract §3; every new refusal and its prior behaviour, `run`
  where the command runs):
  - `build|run|emit *|format <x>.spyproj`: before SPY0013 on XML / format exit 2 (wrong output, a
    lexer error on a file that is not Sharpy); after a refusal naming `compile`/`project` — ICE-
    class message → diagnostic.
  - `project <x>.spy`: before "Unexpected error: Data at the root level is invalid" (exit 1); after
    a refusal naming `build`/`run`/`compile` — message → message, same exit.
  - `compile|project <proj>` with a `<Reference>` naming nothing: before exit 0 + assembly; after
    SPY0305 exit 1, nothing written. Newly rejected programs: only projects whose reference does
    not exist on disk — a program that compiled and **ran correctly** cannot have one (the import
    would fail or the run would throw); the test scaffold's `HintPath` projects resolve once
    `HintPath` is honoured (same commit, Phase 3 Task 2 lands before Task 3). The verifier re-runs
    `SourceGeneratorTests` and `ClrBaseOverrideRouteMatrixTests` at the base commit as the control.
  - `-r`/`-m` on `emit diagnostics`/`emit hover`/`project`: before parse error; after honoured —
    no program newly rejected.
  - `emit hover` with a file path: before SPY0300 on a sibling import (to be measured, Phase 1);
    after resolved — restricts nothing.
- **Blast radius:** `Sharpy.Cli.Tests` (whole project: the sweep walks every command; the E2E
  deployment matrix spawns `sharpyc` and `dotnet`); `Sharpy.Compiler.Tests` — `ProjectFileParser`
  (`SourceGeneratorTests`, `ClrBaseOverrideRouteMatrixTests`, `SpyToDllPipelineTests`,
  `TestProjectScaffoldTests`, every `ProjectCompilationHelper` user) and `Compiler.CompileProject`
  (every project test — a fixture whose `<Reference>` resolves nowhere turns red, which is a
  finding, not noise; `DiagnosticFileProvenanceTests` is a third `WithRuntimeReferences` user
  [CORRECTED: added]); `Sharpy.Lsp.Tests` — project mode loads `.spyproj` through the same parser
  (`LanguageService.cs:134`, `SharpyWorkspace` reference identity) AND now receives SPY0305 from
  the `AnalyzeProject` door (`LanguageService.cs:710`) on `UnattributedTarget` — every LSP
  project-mode fixture with a dangling `<Reference>` turns red (a finding) [ADDED]; `Sharpy.Cli.Tests`
  help-text pins (`RunHelpTests`, `GlobalOptionsTests`) see the generated "Global options without
  effect here" sentence on the commands that gain one [ADDED]; `Sharpy.Stdlib.Tests` only if
  a stdlib fixture compiles a project with a bad reference (none known; the gate tells). The three
  GapDiscovery sweeps are not reached (no `Semantic/`/`CodeGen/` change). `build_tools/regenerate_spy_stdlib.sh`
  and `check_spec_blocks.sh` drive `sharpyc` on `.spy` files — unchanged routes; the staleness
  gates are run in Phase 3 anyway (§7: regenerate early).
- **Generated artifacts touched:** none expected (no `Semantic/`/`CodeGen/` edit). Phase 3 runs
  `build_tools/check_spy_staleness.sh` and the stdlib-docs staleness check after its first commit
  and records "regen: no diff @ sha"; a diff is a finding.

## Design Decisions

1. **One option-to-effect registry for the whole grammar, keyed by command path** (`Sharpy.Cli`
   `CommandOptionEffects`: `IReadOnlyDictionary<string commandPath, CommandEffectTable>`;
   `CompileCommand.OptionEffects` moves in as the `"compile"` table, keeping its two input-kind
   columns; every other command has one column). A row is `(Effect, Reason, Evidence)`; `Effect ∈
   {Honoured, Refused, Ignored}`; `Evidence` names the test (`Type.Method`) that observes an
   honoured cell, or a shared **global probe** by option name. Each table declares
   `Performs ∈ {Compiles, Analyses, Lexes, Parses, Formats, Tooling}`. **Rule for globals:** a
   command whose `Performs` is `Compiles` honours all nine (they are compiler-facing); `Analyses`
   (`emit diagnostics`, `emit hover`) honours the seven analysis-facing globals (logger pair,
   `--verbose`, `--max-errors`, `--warn-as-error`, `--nowarn`, `--enable-feature`) and
   `--metrics-format`/`--metrics-output` only where the command reports metrics (`emit
   diagnostics --include-codegen` compiles and does; `emit hover` has none to report — documented)
   [CORRECTED: as written, "Analyses honours all nine" contradicted Phase 5 Task 2, which rosters
   `--metrics-*` on `emit hover` as documented-ignored; the sweep must assert ONE rule, so the rule
   names the metrics pair as the `Analyses` exception with its reason];
   `Lexes`/`Parses` (`emit tokens|ast|parse`) honour the logger and `--max-errors`/`--verbose`
   and document the rest ignored; `Formats`/`Tooling` (`format`, `explain`, `cache`, `lsp`,
   `server`, `repl`) honour the logger where they create one and document the rest. `lsp` keeps
   forwarding only `--log-level` (#1225): the server owns its logging; the other eight are
   documented ignored. The rule is a table fact the sweep asserts (an `Ignored` global on a
   `Compiles`/`Analyses` command is red), so "documented" cannot be used to dodge "honoured".
   Rung: N/A (CLI). Why a registry and not per-command tables: the sweep's totality walk needs one
   lookup per `(commandPath, option)`; per-file tables would re-create the #2173 shape once per
   command, which is exactly what let the other fourteen go unchecked.
2. **`--help` documents every non-honoured cell from the table.** Own options get the
   `DescriptionSuffix` `compile` already generates; global options (shared help text) get a
   generated sentence appended to the **command's** description: "Global options without effect
   here: --metrics-format, …". Refused cells are refused by ONE `RefuseInapplicableOptions`
   (generalised from `CompileCommand`'s) called first in every command's action, before any file
   is read or written.
3. **ONE input classifier and ONE refusal.** `CliHelpers.ClassifyInput(FileInfo) → InputKind
   {SpyFile, SpyProject, Other}` (extension, ordinal-ignore-case, as `compile` does today) and
   `CliHelpers.RefuseInputKind(input, accepted: InputKind[], commandName)`: `build`, `run`,
   `emit *`, `format` accept `SpyFile` (and `Other` with today's warning — not widened);
   `project` accepts `SpyProject`; `compile` accepts both and dispatches. Message shape follows
   the existing refusals (`Error: … is not supported …`):
   `Error: 'app.spyproj' is a project file; use 'sharpyc compile app.spyproj' or 'sharpyc project app.spyproj'.`
   and `Error: 'main.spy' is not a .spyproj project file; use 'sharpyc build', 'run' or 'compile' for a .spy file.`
   Exit code is the command's usage-error code (`format`: 2, others: 1), pinned per command in the
   matrix. No SPY code: these are command-line usage errors like `--project-reference`'s refusal,
   not compiler diagnostics (no `DiagnosticExplanations` entry needed).
4. **The deployment set is the used closure ∪ every explicit user reference.** User references
   are explicit inputs (`-r`, `<Reference>`), so honouring them means deploying them, used or not
   — the issue's expected and the simplest rule to state. ONE seam: `RuntimeDependencyHelper.
   CopyRuntimeDependencies(outputDir, usedAssemblyPaths, userReferences)` seeds the closure from
   both; `SelfContainedPublisher.Publish` takes the same seed and references any existing path
   (today it only takes CLI-directory siblings). Callers form the seed from their own inputs:
   `compile <file>.spy` and `run` from the resolved `-r` list — **where it exists**: `CliHelpers.ResolveReferences`
   is called at ONE site, inside `BuildCommand.CompileToBinary` (`BuildCommand.cs:131`), and its
   result is a local there; `CompileCommand.CompileSingleFile` and `RunCommand` hold only the raw
   `-r` spellings [CORRECTED: the plan read as if the callers already held the resolved list]. So
   the seed is read off the compile result — `compileResult.ProjectConfig.References` (the synthetic
   config is built from the resolved options and already carries them) — on the in-process branch,
   and on `run --server` from a `CliHelpers.ResolveReferences(references, modulePaths)` call made
   in `run` before `TryServerCompileForRun` (the server's reply carries `UsedAssemblyPaths` only);
   `compile <proj>.spyproj` from `projectConfig.References` minus the defaults (the project's
   `<Reference>` items + merged `-r`).
   `project` and `build` **write the assembly only** (documented in `--help`, the README's
   "Output Structure", and the spec): `build` never deployed, `project` never deployed, and
   `compile` is the deployment command for both kinds — a two-way door; if the owner prefers
   `project` to deploy, it is one more caller of the same seam plus a `--no-deps` row. Rejected:
   recording usage in the CLR-namespace arms (`GetNamespaceTypes`/`TryResolveNetType`) — precise
   but fragile (any new resolution arm reopens the cell) and a `Semantic/` edit with Stdlib blast
   radius; the arm asymmetry is recorded on the ledger as a #2223 cell, not fixed here.
5. **SPY0305 on the project route through ONE registry helper.** `ModuleRegistry.
   MissingReferenceDiagnostics()` (one name — Phase 3 Task 1 uses this spelling; SPY0305 only —
   SPY0306 "already loaded" is a long-lived-process artefact that fa7e8aa63 measured, and refusing
   on it reddened nine run-route cells) is consulted by `Compiler.CompileProject` **before**
   `projectCompiler.Compile` and, when non-empty, returns a failed `ProjectCompilationResult`
   carrying only those diagnostics — the shape `CompileAsSyntheticProject` already has
   (`CompilerApi.cs:140-150`) [CORRECTED: the plan said "exactly where `Compile`/`Analyze` merge
   today", i.e. AFTER the pipeline; but on the project route `projectCompiler.Compile` itself writes
   the assembly (`ProjectCompiler.CodeGen.cs:311` `CompileToAssembly`), so a merge after it leaves
   `App.dll` on disk and the acceptance "no assembly written" cannot be met; the single-file
   `Compile` merges after because its pipeline emits C# only]. The check runs after the NuGet
   package loads at `Compiler.cs:118-132` (those are loads on the same registry). `CompilerApi.AnalyzeProject`
   merges the same helper's output into its `ProjectAnalysisResult` (analysis writes nothing, so
   "after" is fine there); `CompileAsSyntheticProject`'s inline filter becomes a call to the helper.
   The project's own items and `-r` then report through the same code, and
   `CompileCommand.CompileProject`'s pre-check for `-r` (`CompileCommand.cs:385-411`) stays as
   the before-anything-is-written refusal (its message spelling is reused).
6. **`<HintPath>` per R-EK, in the one parser.** `ProjectFileParser.Load`: a `<Reference>` with a
   `<HintPath>` child takes its path from `HintPath` (absolute, or relative to the `.spyproj`
   directory — the rule `<ModulePath>` already uses) and its assembly name from `Include`; without
   it, `Include` is the path, as today. `ProjectConfig.References` stays a `List<string>` of
   full paths (the LSP's identity comparison and `DefaultReferences.ApplyTo`'s dedupe read it);
   the assembly name is not stored — the registry takes it from the loaded assembly. An
   `Include` that does not match the `HintPath` file's assembly name is **not diagnosed**: the file
   is what loads, R-EK asks for no such check, `ProjectFileParser` has no warning channel (it throws
   on an invalid `<Features>` name and returns a config otherwise), and a compiler warning would
   need a new SPY045x code plus a `DiagnosticExplanations` entry (Rule 10) for a cell with no
   observable harm [CORRECTED: the plan promised "a warning" with no code, channel or test; if the
   owner wants it, it is a `docs/deviations.yaml`-class note on the spec section, not a diagnostic].
   The spec sentence says `Include` is the reference's name and `HintPath` its file.
7. **`-r`/`-m` on `emit diagnostics`, `emit hover`, `project`** are wired through the existing
   seams (`CliHelpers.ResolveReferences`, `CliHelpers.ValidateModulePaths`, and a
   `CliHelpers.MergeCommandLineIntoProject(config, references, modulePaths)` extracted from
   `CompileCommand.CompileProject` so `compile .spyproj` and `project` share one merge). `emit
   hover` passes `inputFile.FullName` to `Analyze` as `emit diagnostics` does (#1377's rule).
   `emit tokens|ast|parse` declare neither; the README's option table says why.
8. **Spec and docs are deliverables of equal weight** (§8): `docs/language_specification/
   module_system.md` gains a `<Reference>` section (R-EK) with an executed example; the CLI README
   gains `<Reference>`/`<HintPath>`/`<ModulePath>` in "Project File Format", a per-command
   option table (which commands take `-r`/`-m`, which kinds each accepts, which deploy), and a
   corrected "Output Structure".

## Implementation

### Phase 1: Generator — the option-to-effect registry and its reflective sweep

**Goal:** every `(command, option)` pair of the real root grammar has a row that says what the
option does, the sweep refuses a row-less option, an undocumented `Ignored` cell, a non-refusing
`Refused` cell, and an unevidenced `Honoured` cell; the cells that are wrong today are allowlisted
against #2223 for Phases 2–5 to drain.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `CliOptionEffectSweepTests` over the real root (`CliTestHarness.BuildRoot`),
  ≥ 180 cells enumerated (positive control pins the count ≥ the literal sum of declared options
  across the 15 command paths, anchored to literals, not to the same enumeration);
  `CompileOptionEffectTableTests` keeps passing against the moved table.
- Guard mutation: (M1) delete the `"format"` table's `--check` row → totality red; (M2) suppress
  the generated "Global options without effect here" sentence → every `Ignored` global cell red;
  (M3) make `RefuseInapplicableOptions` refuse nothing → every `Refused` cell red; (M4) rename
  the test an `Evidence` names → that cell red; (M5) mark `--warn-as-error` `Ignored` on `emit
  csharp` → the `Performs` rule red. Positive control for the absence assertion ("no row-less
  option"): a test-only root with a synthetic `probe` command declaring `--dropped` with no row
  must be reported — `TheSweep_ReportsASyntheticDroppedOption`.
- Allowlist entries that drain: `src/Sharpy.Cli.Tests/cli-option-effect-allowlist.txt` is
  populated here (`<command path> <option> <reason> # #2223`, one per cell that is wrong today:
  the §Current State axis-1 drops, the `emit diagnostics`/`emit hover`/`project` `-r`/`-m`
  omissions are not rows (undeclared) but the sweep's `Performs=Analyses` rule lists them as
  "expected option missing" cells, allowlisted) and drained to 0 by Phase 5.
- Execution evidence the close-out will cite: `CliOptionEffectSweepTests` N/0/0 @ sha with the
  allowlist row count; the mutation record M1–M5 (broken → red count, restored → green) in the
  commit body.

#### Tasks

1. **`CommandOptionEffects` registry** — `src/Sharpy.Cli/CommandOptionEffects.cs` (new),
   `src/Sharpy.Cli/Commands/CompileCommand.cs`
   - Define `OptionEffect`, `OptionEffectRow(Effect per input kind, Reason, Evidence)`,
     `CommandEffectTable(Performs, InputKinds, Rows)`, `CommandOptionEffects.Tables` keyed by
     command path (`"compile"`, `"emit csharp"`, `"cache clear"`, …) — one static class, one
     dictionary literal per command, rows written from §Current State (a global the command
     reads today is `Honoured` with evidence `GlobalProbe:<option>`; a dropped one is `Ignored`
     with the reason, or `Honoured` with evidence `null` when Decision 1's rule says it must be
     honoured — those are the allowlisted cells). Move `CompileCommand.OptionEffects` here
     verbatim (two columns); `CompileCommand` reads `CommandOptionEffects.Tables["compile"]`.
   - Generalise `DescriptionSuffix`/`RefuseInapplicableOptions` into `CommandOptionEffects`
     (static helpers taking a table); `Configure` of every command calls
     `CommandOptionEffects.Document(command)` (appends own-option suffixes and the global
     sentence) and its action calls `CommandOptionEffects.Refuse(parseResult, command, kind)`
     first.
   - Acceptance: totality bullet; `CompileOptionEffectTableTests` green unchanged.
   - Commit: `refactor(cli): one option-to-effect registry for every command, compile's table moved in; --help notes and refusals generated from it (refs #2223)`

2. **`CliOptionEffectSweepTests` + global probes + allowlist** —
   `src/Sharpy.Cli.Tests/CliOptionEffectSweepTests.cs` (new),
   `src/Sharpy.Cli.Tests/cli-option-effect-allowlist.txt` (new),
   `src/Sharpy.Cli.Tests/GlobalOptionProbes.cs` (new)
   - Walk the root as `OptionGrammarConformanceTests.AllOptions` does; for each
     `(commandPath, option)`: row exists both ways; `Ignored` → the option's description (own) or
     the command description (global) carries the generated note; `Refused` → `CliTestHarness.Invoke`
     with the row's `RefusedSpelling` exits with the command's usage code naming the option and
     writes nothing to the output dir; `Honoured` → `Evidence` resolves by reflection to a public
     test method in the test assembly, or to a `GlobalOptionProbes` entry which the sweep RUNS on
     that command (`--log-file` → file non-empty at `--log-level Info`; `--log-level Debug` →
     `[DEBUG]` lines; `--verbose` → the phase-timing lines; `--metrics-format json` → a JSON
     object on stdout; `--metrics-output f` → f written; `--warn-as-error` → a warning fixture
     exits non-zero; `--nowarn SPY0451` → the warning absent, with the positive control that it is
     present without; `--max-errors 1` → one error reported of three; `--enable-feature defer` →
     a `defer` program accepted, SPY0331 without). Each command declares an `Invocation` template
     (how its input is spelled: `.spy` file, `.spyproj`, a code, nothing) in its table.
   - `Performs` rule: a `Compiles`/`Analyses` command with an `Ignored` global is red; a
     `Compiles`/`Analyses` command without `--reference`/`--module-path` declared is an
     "expected option missing" cell (red unless allowlisted).
   - Allowlist ratchet: a failing cell not in the file fails; a listed cell that passes fails
     ("stale row"); every row cites `#2223`.
   - Positive controls: the synthetic `probe` command (row-less option reported; an `Ignored`
     own option without the suffix reported; a `Refused` cell whose action does not refuse
     reported; an `Honoured` cell whose evidence names `Nope.Nope` reported); the enumeration
     count ≥ a literal.
   - Mutation record M1–M5 in the commit body.
   - Commit: `test(cli): CliOptionEffectSweepTests — reflective totality of every command's option table, --help notes, refusals and honoured-cell evidence, allowlist ratchet (refs #2223)`

### Phase 2: Input kind × command — one classifier, one refusal

**Goal:** a `.spyproj` given to a command that compiles Sharpy source is refused with a steer to
`compile`/`project`, and a `.spy` given to `project` is refused with a steer to
`build`/`run`/`compile`, before any file is read as the wrong thing.

**Acceptance (class-level, measurable):**
- Matrix that goes green: `InputKindMatrixTests` (`Sharpy.Cli.Tests`): `{.spy, .spyproj}` ×
  `{build, run, emit tokens, emit ast, emit csharp, emit parse, emit diagnostics, emit hover,
  format, project, compile}` — 22 cells: the accepted kind performs the command (positive control
  per command: the `.spy` program prints/emits; the `.spyproj` builds), the other kind is refused
  with the Decision-3 message and the command's usage exit code, stderr free of `SPY0013` and of
  "Unexpected error", and nothing written.
- Guard mutation: make `ClassifyInput` return `SpyFile` for every extension → the 10 refusal
  cells red, the 11 positive cells green; make `RefuseInputKind` return true → the same 10 red.
- Allowlist entries that drain: none (this axis has no allowlist; the cells are all enforced in
  this phase).
- Execution evidence: `InputKindMatrixTests` 22/0/0 @ sha; the before/after table from
  §Adversarial Review re-measured with the built `sharpyc`.

#### Tasks

1. **`CliHelpers.ClassifyInput` + `RefuseInputKind`** — `src/Sharpy.Cli/CliHelpers.cs`,
   `src/Sharpy.Cli/Commands/{Build,Run,Emit,Format,Project,Compile}Command.cs`
   - Add the classifier and the refusal (Decision 3); `compile` replaces its inline extension
     test with `ClassifyInput`; `build`, `run`, every `emit` subcommand and `format` (file input
     only — a directory is untouched) call `RefuseInputKind(input, [SpyFile, Other], …)` before
     `ValidateInputFile`/reading; `project` calls `RefuseInputKind(project, [SpyProject], …)`
     before `ProjectFileParser.Load`. `ValidateInputFile`'s non-`.spy` **warning** stays for
     `Other` (outside the contract; not widened).
   - Acceptance: the refusal cells of the matrix.
   - Commit: `fix(cli): a .spyproj given to build/run/emit/format and a .spy given to project are refused with a steer, before the file is read as the wrong kind (refs #2223)`

2. **`InputKindMatrixTests`** — `src/Sharpy.Cli.Tests/Commands/InputKindMatrixTests.cs` (new)
   - The 22-cell theory (command × kind), in-process via `CliTestHarness.Invoke` (`format`
     and `project` included; `emit hover` with `--line 1 --col 1`); mutation record in the body.
   - Commit: `test(cli): InputKindMatrixTests — {.spy, .spyproj} × every file-taking command, accepted kind runs, other kind refused before anything is read (refs #2223)`

### Phase 3: `.spyproj` references — `<HintPath>` honoured, a missing reference is SPY0305 on every door

**Goal:** R-EK: the one parser reads `HintPath`; a `<Reference>` that resolves nowhere fails
`compile <proj>` and `project` with SPY0305 before the assembly is written, through the same
registry helper every door uses; the spec and README say so.

**Acceptance (class-level, measurable):**
- Matrix that goes green: `ProjectReferenceParserTests` (`Sharpy.Compiler.Tests`, the parser axis):
  `{Include=path absolute, Include=path relative, Include=name + HintPath absolute, Include=name +
  HintPath relative, HintPath only (no Include), Include + empty HintPath}` × `{file exists, file
  missing}` → `config.References` holds the full path the rule names (12 cells, pure parser);
  `ProjectReferenceRouteTests` (`Sharpy.Cli.Tests`, the route axis): `{<Reference Include>,
  <Reference>+<HintPath> absolute, +<HintPath> relative}` × `{resolves, missing}` × `{compile
  .spyproj, project}` → resolves: the program compiles and `from Greeter import Greeter` binds (positive control:
  without the item SPY0300); missing: `SPY0305` + `Assembly not found: <path>` on stderr, exit 1,
  no assembly written (12 cells). Door completeness: `RegistryErrorDoorTests`
  (`Sharpy.Compiler.Tests`): every `Compiler`/`CompilerApi` public entry point that takes a
  `ProjectConfig` — enumerated by reflection over public methods with a `ProjectConfig` parameter
  and pinned to the literal **3**: `Compiler.CompileProject(ProjectConfig)`,
  `Compiler.CompileProject(ProjectConfig, CancellationToken)`, `CompilerApi.AnalyzeProject`
  [CORRECTED: `CompilerApi.CompileProject` does not exist @ 27e261684; `CompileAsSyntheticProject`
  is private and takes a string — it is covered by `CompileOptionEffectTableTests.AReferenceThatResolvesNowhere_IsSpy0305`]
  — reports SPY0305 for a config whose reference is missing, and the two compile doors write no
  assembly (`OutputAssemblyPath` null, the path absent). One LSP cell [ADDED: missing step]:
  `Sharpy.Lsp.Tests` project mode over a `.spyproj` with a missing `<Reference>` publishes SPY0305
  on `ProjectAnalysisResult.UnattributedTarget` (the #2242 D5 target) and clears it when the
  reference appears — the `AnalyzeProject` door is the editor's; its cached registry
  (`GetOrBuildAnalysisContext`, keyed by path + mtime) re-keys when the file appears, so the
  clear is the positive control that the cache does not pin the error.
- Guard mutation: (M1) parser ignores `HintPath` again → the 6 HintPath parser cells + the 4
  HintPath route cells red, AND `SourceGeneratorTests` red (the scaffold's references now
  resolve nowhere and are reported) — that second red is the blocker #2223 named, now a guard;
  (M2) `Compiler.CompileProject` drops the pre-compile check → the 6 missing-route cells + the
  door test red, and the "no assembly written" assertion is the arm that distinguishes a
  pre-compile check from a post-compile merge (a merge-after mutant fails ONLY that assertion —
  record it); (M2b) `CompilerApi.AnalyzeProject` drops its merge → the `AnalyzeProject` door cell
  + the LSP cell red [ADDED: the door test needs a mutation per door, not per class];
  (M3) the helper returns all registry errors (not SPY0305 only) → `ModuleNamespaceShadowsClrTypeTests`
  red in-process (the fa7e8aa63 finding, re-confirmed as the reason for the filter).
- Allowlist entries that drain: none here (no allowlist on this axis); Phase 1 rows for
  `compile`/`project` reference cells, if any were rostered, are deleted in the same commit.
- Execution evidence: both matrices and the door test green @ sha; `SourceGeneratorTests` and
  `ClrBaseOverrideRouteMatrixTests` green at HEAD and (control) at the base commit;
  `build_tools/check_spy_staleness.sh` and the stdlib-docs staleness check exit 0 after Task 2
  ("regen: no diff @ sha"); the spec section's example executed.

#### Tasks

1. **Registry helper + every door** — `src/Sharpy.Compiler/Semantic/Registry/ModuleRegistry.cs`,
   `src/Sharpy.Compiler/Compiler.cs`, `src/Sharpy.Compiler/CompilerApi.cs`
   - `ModuleRegistry.MissingReferenceDiagnostics()` returns the SPY0305 entries (with the
     SPY0306 rationale comment from `CompilerApi.cs:134-139` moved here); `Compiler.CompileProject`
     consults it **after the NuGet loads (`Compiler.cs:118-132`) and before
     `new ProjectCompiler(...).Compile(...)` (`Compiler.cs:134-135`)**, returning a failed
     `ProjectCompilationResult { Success = false, Diagnostics = <those>, OutputAssemblyPath = null }`
     when non-empty [CORRECTED: "after `projectCompiler.Compile`, the position `Compile` uses" would
     write the assembly first — see Decision 5]; `CompileAsSyntheticProject` calls the helper
     instead of its inline `Where`; `CompilerApi.AnalyzeProject` (`CompilerApi.cs:507-520`, the
     registry from `GetOrBuildAnalysisContext`) merges the helper's output into the
     `ProjectAnalysisResult` it returns (file-less → `UnattributedTarget`). `Compile`/`Analyze`'s
     broader merge stays (legacy single-file behaviour; outside this contract).
   - Acceptance: `RegistryErrorDoorTests`; the `missing` route cells.
   - Commit: `fix(compiler): a project reference that resolves nowhere is SPY0305 on the project route too — one registry helper read by every door that builds a registry from a ProjectConfig (refs #2223)`
   - **Order note:** lands together with Task 2 in one push to the gate, Task 2 first in history,
     because the test scaffolds' `HintPath` references are unresolved until Task 2.

2. **`<HintPath>` in the parser** — `src/Sharpy.Compiler/ProjectConfig.cs`
   - Per Decision 6; XML doc on `References` states the rule; `ProjectReferenceParserTests`.
   - Run the staleness gates after this commit (§7) and record "regen: no diff @ sha".
   - Commit: `feat(project): a .spyproj <Reference> honours <HintPath> — the path comes from HintPath (relative to the project directory), the assembly name from Include (R-EK, refs #2223)`

3. **Route matrix** — `src/Sharpy.Cli.Tests/Commands/ProjectReferenceRouteTests.cs` (new)
   - The 12 route cells; reuses `CompileOptionEffectTableTests.ReferenceLibrary` (extract it to a
     shared fixture `ReferenceLibraryFixture` so both classes build `Tablegreeter.dll` once).
   - Commit: `test(cli): ProjectReferenceRouteTests — <Reference Include> and <HintPath> × {resolves, missing} × {compile .spyproj, project}: a resolving reference binds the import, a missing one is SPY0305 before the assembly is written (refs #2223)`

4. **Spec + README** — `docs/language_specification/module_system.md` (new section
   `## Project references` placed after the **Module search paths** paragraph — a bold paragraph
   lead at `module_system.md:77`, inside `## Package Structure`, not a heading; the new `##`
   closes that section there, which is acceptable because the SPY0313 example it follows is the
   last of its content [CORRECTED: "after 'Module search paths'" read as if a heading existed]),
   `src/Sharpy.Cli/README.md` ("Project File Format" at `README.md:103`: `<Reference>`,
   `<HintPath>`, `<ModulePath>`; the `-r` bullet at `README.md:36`)
   - Spec content: the two spellings, the relative-`HintPath` rule, SPY0305 for a missing file,
     the deployment rule from Decision 4 (one sentence, finalised in Phase 4), an executed
     example (`main.spy` importing from a `<Reference>`d library: the `.spy` fence carries
     `<!-- spec-sweep: fragment -->` because `check_spec_blocks.sh` compiles a bare block without
     the library, and the program's output is shown in a `text` block beside it, executed by hand
     with the built `sharpyc` and quoted in the commit body; the SPY0305 example is a `text`
     block of the command and its diagnostic, as the `<ModulePath>` section already does for
     SPY0313).
   - Commit: `docs(spec): module_system.md gains the <Reference> section — Include, HintPath, SPY0305 for a missing file (R-EK, refs #2223)`

### Phase 4: Deployment — every user reference is deployed beside the output

**Goal:** a program compiled with `-r`/`<Reference>` runs from its output directory with
`dotnet`, and a self-contained publish carries the reference; `build` and `project` document that
they write the assembly only.

**Acceptance (class-level, measurable):**
- Matrix that goes green: `ReferenceDeploymentMatrixTests` (`Sharpy.Cli.Tests/E2E`, spawns the
  real `sharpyc.dll` like `StandaloneDeploymentTests`): reference source `{-r absolute, -r bare
  name + -m, <Reference Include>, <Reference>+<HintPath>}` × route `{compile .spy, compile
  .spyproj, run, run --server (a `sharpyc server` started for the cell, the pattern of
  `CompileServerE2ETests`)}` where the source applies → the output directory contains
  `Tablegreeter.dll` and `dotnet <out>.dll` prints `hello from lib` (for `run`: the banner and
  the line); `--no-deps` on `compile` (both kinds) → not copied (documented opt-out; control);
  `build` and `project` → not copied, and their `--help`/README say so (pinned text). One
  self-contained cell per kind (`compile --self-contained <file>.spy -r …` and `run --self-contained`;
  slow; **untraited**, exactly as `SelfContainedDeploymentTests` is — that class carries no
  `[Trait]` @ 27e261684, its two `[Fact]`s run in the regular `Sharpy.Cli.Tests` suite
  [CORRECTED: the plan said to copy a `Category=SelfContained` trait that does not exist; if the
  implementer wants one, add it to BOTH classes in the same commit and note that the gate filter
  `Category!=Benchmark` still runs them]) → the published executable prints `hello from lib`.
- Guard mutation: `CopyRuntimeDependencies` ignores `userReferences` → every copy cell red
  (FileNotFoundException at `dotnet`), the `--no-deps` and `build`/`project` controls green;
  the publisher filters to CLI-directory files again → the two self-contained cells red.
- Allowlist entries that drain: none on this axis.
- Execution evidence: the matrix @ sha (counts), the two self-contained cells run once at
  close-out with their output quoted.

#### Tasks

1. **Deployment seed = used ∪ user references** — `src/Sharpy.Cli/RuntimeDependencyHelper.cs`,
   `src/Sharpy.Cli/SelfContainedPublisher.cs`, `src/Sharpy.Cli/Commands/CompileCommand.cs`,
   `src/Sharpy.Cli/Commands/RunCommand.cs`
   - Per Decision 4. `compile <proj>`: user references = `projectConfig.References` before
     `defaultReferences` are injected (capture the list before the injection loop at
     `CompileCommand.cs:447-451`). `run`: both branches (in-process and server) seed from the
     resolved `references`. The publisher references every seed path that exists, not only
     CLI-directory siblings; `Sharpy.Core` stays special-cased.
   - Acceptance: the copy cells and the self-contained cells.
   - Commit: `fix(cli): every -r / <Reference> assembly is deployed beside the output by compile and run, and referenced by a self-contained publish — the deployment set is the used closure plus the explicit references (refs #2223)`

2. **`ReferenceDeploymentMatrixTests`** — `src/Sharpy.Cli.Tests/E2E/ReferenceDeploymentMatrixTests.cs` (new)
   - As in the acceptance; shares `ReferenceLibraryFixture`; mutation record in the body.
   - Commit: `test(cli): ReferenceDeploymentMatrixTests — reference source × route, the program runs from its output directory; --no-deps, build and project are the documented controls (refs #2223)`

3. **`build`/`project` deployment rule in help and docs** — `src/Sharpy.Cli/Commands/BuildCommand.cs`,
   `src/Sharpy.Cli/Commands/ProjectCommand.cs`, `src/Sharpy.Cli/README.md`,
   `docs/language_specification/module_system.md` (the Phase 3 sentence finalised)
   - Command descriptions: "writes the assembly only; `compile` deploys its runtime dependencies
     beside the output"; README "Output Structure" corrected (today it lists only exe/dll/pdb and
     omits that nothing beside them is deployable); the option rows for `-r` on `build` carry the
     reason.
   - Commit: `docs(cli): build and project write the assembly only — compile and run deploy; README output structure and <Reference> rules (refs #2223)`

### Phase 5: Drain — `-r`/`-m` where analysis needs them, globals wired or documented, allowlist to 0

**Goal:** every Phase 1 allowlist row is drained by the cure its row names; `emit diagnostics`,
`emit hover` and `project` accept `-r`/`-m` through the shared seams; `--help` documents every
remaining `Ignored` cell.

**Acceptance (class-level, measurable):**
- Sweep that goes green: `CliOptionEffectSweepTests` with `cli-option-effect-allowlist.txt`
  **empty** (the file stays, zero rows; the stale-row check makes any leftover red).
- Guard mutation: re-drop `--metrics-format` in `emit csharp` → its cell red; remove `-m` from
  `emit diagnostics` → the "expected option missing" cell red.
- Allowlist entries that drain: all of Phase 1's rows (listed by command in the commit body).
- Execution evidence: the sweep N/0/0 @ sha, allowlist 0 rows; `emit diagnostics src/main.spy
  -r Greeter.dll` → `No diagnostics.` with the built binary (and `emit hover` on a sibling import
  resolving — the Phase 1 measurement closed).

#### Tasks

1. **`-r`/`-m` on `emit diagnostics`, `emit hover`, `project`** — `src/Sharpy.Cli/Commands/EmitCommand.cs`,
   `src/Sharpy.Cli/Commands/ProjectCommand.cs`, `src/Sharpy.Cli/CliHelpers.cs`
   - Declare the two options (one value per occurrence, `PathOptionArityTests.OptionSites` gains
     the three sites); `emit diagnostics`/`emit hover` run `ValidateModulePaths` +
     `ResolveReferences` and pass `references:`/`modulePaths:` to `ForCli` (both the compile and
     analyze branches of `EmitDiagnostics`); `emit hover` passes `inputFile.FullName` to
     `Analyze`; `project` merges through `CliHelpers.MergeCommandLineIntoProject` extracted from
     `CompileCommand.CompileProject` (which now calls it too). Table rows flip to `Honoured` with
     evidence (`ModuleSearchPathCliTests` gains `emit diagnostics`/`project` cells for `-m`; the
     Phase 3 route tests cover `-r` on `project`). [ADDED: missing step] The two places that
     enumerate which commands take `-m` are updated in the same commit: the spec's **Module search
     paths** paragraph (`module_system.md:77-78`: "`-m` (`--module-path`) on `run`, `compile`,
     `build` and `emit csharp`") gains `emit diagnostics`, `emit hover` and `project`, and the
     README's `-m` bullet (`README.md:38`, "on every command") is made true or narrowed to the
     list; the spec example is re-run (§8).
   - Commit: `fix(cli): emit diagnostics, emit hover and project take -r and -m through the shared seams; emit hover resolves the entry's imports from its path (refs #2223)`

2. **Globals: honour or document per the `Performs` rule** — `src/Sharpy.Cli/Commands/{Emit,Repl,Server,Format,Explain,Cache,Lsp}Command.cs`,
   `src/Sharpy.Cli/CommandOptionEffects.cs`
   - `emit csharp`/`emit diagnostics`: `--metrics-format`/`--metrics-output` honoured
     (`CliHelpers.OutputMetrics` on the single-file metrics, as `build` does on project metrics);
     `emit tokens|ast|parse|hover`: `--warn-as-error`/`--nowarn`/`--enable-feature`/`--metrics-*`
     documented ignored (hover: `--enable-feature` honoured — analysis — and `--warn-as-error`/
     `--nowarn` too, since `Analyze` takes them; measure, then roster); `repl`: `--warn-as-error`/
     `--nowarn`/`--max-errors` honoured if `ReplSession` takes options, else documented
     (measure); `server`: `--verbose` honoured (logger), the rest documented (the request carries
     its own); `format`/`explain`/`cache`: logger globals honoured where a logger is created
     (`format` creates one for the lexer/parser if `FormatterService` accepts a logger — measure;
     otherwise documented), the rest documented; `lsp`: documented except `--log-level`.
   - Every flip from `Ignored` to `Honoured` names its evidence (a `GlobalOptionProbes` run or
     a test); every remaining `Ignored` has its `--help` sentence.
   - Commit: `fix(cli): every global option is honoured by the commands that compile or analyse and documented as ignored by the rest; allowlist drained to zero (refs #2223)`

3. **README option table** — `src/Sharpy.Cli/README.md`
   - A table: command × {input kinds, `-r`/`-m`, deploys?, globals without effect} generated by
     hand from the registry; a test (`ReadmeOptionTableTests`) that asserts every command path
     in the registry appears in the README table and vice versa (totality both ways, positive
     control: a fake command name is reported) — a cheap guard against the README fossilising.
   - Commit: `docs(cli): per-command option table in the README, guarded against the registry (refs #2223)`

### Gate

`.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`
(whole solution, background, private log path), once at the end of Phase 5; counts reported
`passed/failed/skipped @ sha (measured)`. The edit loop per phase is `Sharpy.Cli.Tests` whole
project plus, for Phase 3, `Sharpy.Compiler.Tests --filter "FullyQualifiedName~Project|FullyQualifiedName~SourceGenerator|FullyQualifiedName~SpyToDll|FullyQualifiedName~ClrBaseOverride"`
and `Sharpy.Lsp.Tests --filter "FullyQualifiedName~Project"` (a narrow filter is not a gate).
Orphaned wrapper watchdogs are checked before the gate (`ps -o ppid` on `sleep 30` children).

## Testing Strategy

- **Generator first (§11):** `CliOptionEffectSweepTests` over the option × command matrix with
  `cli-option-effect-allowlist.txt`; the effect of an honoured option is the axis the generator
  cannot enumerate — it is judged by the per-option `GlobalOptionProbes` (globals) and by the
  named evidence tests (own options). Hand matrices cover only the two closed axes the charter
  names: input kind × command (`InputKindMatrixTests`, 22 cells) and reference source × state ×
  route (`ProjectReferenceParserTests` 12, `ProjectReferenceRouteTests` 12,
  `ReferenceDeploymentMatrixTests` 4 sources × 4 routes + 2 self-contained + 4 controls).
- **New fixtures:** none file-based (`TestFixtures/` is for language behaviour); every cell is a
  `TempWorkspace` program. Negative cells and their positive controls: each refusal cell has the
  same command on the accepted kind / with the resolving reference / without the refused option
  succeeding; each absence assertion (`Tablegreeter.dll` absent under `--no-deps`; no `[DEBUG]`
  without `--log-level Debug`; no warning under `--nowarn`) has the presence control in the same
  theory.
- **Edge cases outside the issue's repro list:** `HintPath` relative with `..`; `Include` naming a
  different assembly than the `HintPath` file (warning, loads the file); a `-r` bare name found
  on a `<ModulePath>` of the `.spyproj` (`compile .spyproj`, `project -m`); `run --server` with
  `-r`; `format` given a directory containing a `.spyproj` (untouched — only `.spy` files are
  formatted; a positive control); `emit hover` on a file whose sibling import defines the hovered
  name; `project` with no argument and a `.spy` in the directory (`FindProjectFile` finds no
  project → today's message, unchanged).
- **Outputs that discriminate:** every deployment cell RUNS the program and asserts the library's
  line (`hello from lib`), not the exit code; every SPY0305 cell asserts the path in the message
  and that the output directory does not exist afterwards; every input-kind cell asserts the
  steer text and the absence of `SPY0013`.
- **Spec:** `module_system.md` `## Project references` — its example executed before commit
  (§8); `check_spec_blocks.sh` run; `build_tools/spec_blocks_allowlist.txt` gains no row
  [CORRECTED: the allowlist is `spec_blocks_allowlist.txt`; no `check_spec_blocks_allowlist.txt`
  exists]. The marker forms are `spec_blocks.py:43-52`: `fragment` for the library-importing
  block, and the SPY0305 command transcript is a `text` block (never a `spec-sweep: error` block —
  that marker expects a compilable Sharpy program, not a shell session).
- **Blast radius and regen:** Phase 3 runs the Compiler/LSP project-test filters in the edit loop
  and the staleness gates after its first commit; the whole-solution gate once at the end
  (§Gate). `FrontEndParityTests` is run by the gate (the LSP loads projects through the changed
  parser).

## Issues to Close

- #2223 — cli: paths and options beyond compile's table — closed by Phase 5, Task 3 (the last
  drain); **close criterion:** `CliOptionEffectSweepTests` green with `cli-option-effect-allowlist.txt`
  at 0 rows @ sha; `InputKindMatrixTests` 22/0/0, `ProjectReferenceParserTests` 12/0/0,
  `ProjectReferenceRouteTests` 12/0/0, `RegistryErrorDoorTests` green, `ReferenceDeploymentMatrixTests`
  all cells (the two self-contained cells run once, output quoted) @ sha; the issue's five cells
  re-measured with the built `sharpyc` and quoted (user reference copied and the program prints
  `hello from lib`; `<Reference>` naming nothing → SPY0305 exit 1 on `compile` and `project`;
  `build app.spyproj` → the steer, no SPY0013; `emit diagnostics … -m lib` → honoured); the spec
  section executed; whole-solution gate `passed/failed/skipped @ sha (measured)`; the mutation
  records of Phases 1–5 in their commit bodies.

## Class trackers this plan may post cells to

- #2223 — CLI input totality (this class) — any option/path/kind cell found outside the plan's
  matrix (including the CLR-namespace usage-recording asymmetry of Decision 4) goes here as a
  comment.
- #2292 — self-contained archive / `dotnet` host prerequisites — a self-contained cell whose
  failure is the host, not the reference.
- #2114 — a changed referenced assembly does not invalidate the incremental cache — any
  `--incremental` × `<Reference>` cell.
- #2182 — a `-r` assembly with an unloadable type aborts the compile — any reference whose load
  throws rather than resolves nowhere (SPY0306-class cells, the in-process "already loaded"
  cell from fa7e8aa63).
- #2115 — a referenced Sharpy library's surface (unions, enums) — any cell where the reference
  deploys and binds but a member of it misbehaves.
- #2250 — process-wide state under concurrent in-process use — the in-process "earlier compile
  satisfies a later import" cell, if the lead judges it that class rather than #2182's.
- The root-level-source `SPY0908 CS5001` cell (an exe `.spyproj` with no detected entry module)
  has no open tracker: it is ONE ledger line; the lead files one class issue (lane ice · program,
  "SPY0908 as a net: an exe project without an entry module") after de-duplicating against the
  open list. No implementer runs `gh issue create`.

## Implementation Checklist

Base 27e261684 · board for `/implement-plan` (2026-10-14). Door (a) posted: https://github.com/antonsynd/sharpy/issues/2223#issuecomment-6171984191

- [x] P3.T2 `<HintPath>` in ProjectFileParser + ProjectReferenceParserTests — agent `p3-compiler` — 27528e3d1 (lead re-ran M1: 15 red / 23 green restored)
- [x] P3.T1 `ModuleRegistry.MissingReferenceDiagnostics` + 3 doors + RegistryErrorDoorTests + LSP cell — agent `p3-compiler` — 0afdad634
In-contract cells found during implementation (added to the Defect Class; allowlisted in Phase 1, drained in Phase 5), measured with the built sharpyc @ 27e261684:
- `--metrics-output f` given alone is dropped on every command that reports metrics (`CliHelpers.OutputMetrics`/`OutputProjectMetrics` return when `--metrics-format` is null).
- `emit hover --max-errors` is read into a local and never passed to `Analyze` (three errors printed under `--max-errors 1`).
- `server --log-level/--log-file/--verbose`: `ServerCommand` builds a logger that `CompileServer` stores and never calls; requests run with `NullLogger` (`--log-file` creates an empty file).
- Evidence survey: 20 honoured own options/globals had no effect test; `p1-evidence` writes `OptionEffectEvidenceTests`.
- Registry evidence lives test-side (`CliOptionEffectSweepTests.Evidence`), not on the production row: production code never names test methods.

- [x] P1.T1 CommandOptionEffects registry (compile table moved; --help notes generated) — lead — c687afafc
- [x] P1.T2 CliOptionEffectSweepTests + GlobalOptionProbes + allowlist + roster row — lead — f6f4fd2bd (evidence tests, agent `p1-evidence`), ad5af608d (sweep), 27e7edc95 (process-state exemptions for InvokeWithInput)
- [x] P2.T1 ClassifyInput + RefuseInputKind — lead — 699c15b39
- [x] P2.T2 InputKindMatrixTests — lead — d6388d42f
- [x] P3.T3 ProjectReferenceRouteTests (+ ReferenceLibraryFixture) — agent `p3-compiler` — 3968ff210, a8f3dd4dc
- [x] P3.T4 spec "Project references" (a bold paragraph after "Module search paths", keeping `## Package Structure` intact) + README project-file format — agent `p3-compiler` — 1f9223895 (example re-executed by the lead @ 739ae5416: `hello, world`; SPY0305 case, no bin/)
- [x] P4.T1 deployment seed = used ∪ user refs — agent `p4-deploy` — 710f86af4 (compile .spy seeds from ResolveReferences(-r,-m): ProjectConfig.References also holds the defaults)
- [x] P4.T2 ReferenceDeploymentMatrixTests + gap-discovery roster line 42 — agent `p4-deploy` — e452d64ca (lead re-ran mutation (a): 11 red / 17 green restored)
- [x] P4.T3 build/project "assembly only" help + docs — agent `p4-deploy` — 10d0b3d9a
- [x] P5.T1 -r/-m on emit diagnostics, emit hover, project — lead — 447b9c60a (fourth registry door: single-file Analyze, in-contract), 8a6ba340f (emit diagnostics/hover), a30995174 (project; allowlist 0 rows)
- [x] P5.T2 globals honoured or documented; allowlist → 0 — lead — 8a6ba340f (metrics-output alone, emit csharp metrics, hover analysis options, server logger; emit diagnostics metrics rostered Ignored — analysis records no metrics; narrows Decision 1's parenthetical)
- [x] P5.T3 README option table + ReadmeOptionTableTests — lead — 739ae5416
Wave 1 filing (contract §10): ledger 4 cells → 1 in-contract (server logger, Phase 5) · 1 class issue #2310 (REPL void echo, lane:ice reach:program) · 2 tracker comments (#2309 LSP reference-file watcher, #2223 two authorities for references).
Wave 2 filing: ledger 1 new cell → #2223 comment (usage recording × CLR-namespace arms). Round totals: 6 cells recorded → 1 in-contract (server logger, fixed 8a6ba340f) + 1 in-contract found by the lead (single-file Analyze door, fixed 447b9c60a) · 1 class issue filed (#2310) · 3 tracker comments (#2309 ×1, #2223 ×2).

Final-verifier wave (verification-expert, refute brief, built binary @ 739ae5416; ~225 probes): 2 refuted in-contract cells + 4 sweep weaknesses, fixed:
- R1 `build|run --server` dropped --log-level/--log-file/--metrics-* (pre-existing) → in-process fallback with a note, 8f05cb93a
- R2 missing <Reference> + --clean deleted bin/obj before SPY0305 (P46-introduced ordering) → CLI merge checks items first, 8f05cb93a
- `project <dir>` steer and the help sentence's full stop, 8f05cb93a
- V1–V4 (refusal-only evidence, kind-less evidence fallback, absence probes) → f097e6650 (agent `p1-evidence`), 10b43ea0b
- Commit-body count error: 8a6ba340f says "16 drained"; its diff deletes 20 allowlist lines (rows + their section comments). Not amended (shared tree); recorded here.
Filing: 2 class issues #2311 (parser --max-errors cascade) #2312 (REPL repeated warning); comments #2250 (server reference leak), #2132 (module-file case on macOS), #2223 (emit diagnostics metrics door (a/b/c), SPY0305 (0:0), lsp stdin).
Gate @ 739ae5416: 80132/0/4 (measured); baseline 79724/0/4 @ 27e261684 (measured).
- [x] Final: build ok, whitespace clean, spy/spy-test staleness clean, spec blocks clean (1206 blocks, 0 failing), allowlist issue-state clean, spec example re-executed, verification-expert (refute) run and its in-contract cells fixed; gate 80150/0/4 @ 10b43ea0b (measured). Not pushed; #2223 left OPEN for /verify-implementation.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-14 @ 27e261684
**Plan file:** `.claude/plans/plan-05dd79.md`

Method: `python3 build_tools/check_plan_refs.py --issues` (17 paths ok, 7 "missing" are the plan's new
files, 22 `file:line` ok, 8 SPY codes ok, 12 issues resolved — the 6 CLOSED ones are cited as history);
by hand: `Compiler.cs` 68–144/170–182/255–270, `CompilerApi.cs` 120–160/500–530/580–660,
`ModuleRegistry.cs` 60–105/240–270/618–660, `ProjectConfig.cs` 380–410, `CliHelpers.cs` 320–372,
`CompileCommand.cs` 20–62/215–240/296–345/370–475, `BuildCommand.cs` 93–131, `RunCommand.cs` 180–205/290–315,
`SelfContainedPublisher.cs` 55–120, `AssemblyCompiler.cs` 420–436, `ProjectCompiler.Phases.cs` 796–808,
`ProjectCompiler.CodeGen.cs` 311, `EmitCommand.cs` 33–200/300–365/405–420, every `*Command.cs` global
read, `GlobalOptions.cs` 21–74, `SharpyWorkspace.cs` 600–615, `LanguageService.cs` 122–134/700–710,
`ProjectCompilationHelper.cs` 175–195/455–485, `TestProjectScaffold.cs` 140–160, the five Cli test
harness files the plan builds on, `gap-discovery-contracts.md` 1–44, `spec_blocks.py` 1–52,
`allowlist_issue_state.py` 185–210, charter §3.5 (line 262) and the state line, `gh issue view 2223
--comments`, the open `class-tracker` list (77), the states of #2292 #2114 #2182 #2115 #2250 #2233 #2173.
Probes with the built `sharpyc` (01:44 @ 27e261684) from the plan's workspace: `compile miss2.spyproj`
→ exit 0 + `App.dll`; `project plain.spy` → "Unexpected error: Data at the root level is invalid";
`emit tokens plain.spy --metrics-format json` → no JSON; `emit diagnostics src/main.spy -r lib/Greeter.dll`
→ `'-r' was not matched`. All four reproduce the plan's measurements. No Python behaviour is claimed
(fidelity scope N/A — confirmed, no `python3` probe needed).

### Corrections Made
- **Phase 3 Task 1 / Decision 5 — merge position (correctness):** "merge after `projectCompiler.Compile`,
  the position `Compile` uses" → consult `MissingReferenceDiagnostics()` BEFORE `projectCompiler.Compile`
  and return a failed `ProjectCompilationResult`. Refuting probe: `ProjectCompiler.CodeGen.cs:311`
  calls `CompileToAssembly` inside `Compile`, so the post-pipeline merge would leave the assembly on
  disk and the acceptance "no assembly written" could never pass; the single-file `Compile` merges
  after because its pipeline emits C# only. M2's discriminating arm recorded.
- **Door enumeration (Phase 3 acceptance, Defect Class):** `CompilerApi.CompileProject` does not exist.
  The public `ProjectConfig`-taking doors are exactly 3 (`Compiler.CompileProject` ×2 overloads,
  `CompilerApi.AnalyzeProject`); the private `CompileAsSyntheticProject` is the already-refusing arm.
  `AnalyzeProject` is the LSP's door (`LanguageService.cs:710`); its file-less SPY0305 lands on
  `UnattributedTarget` (#2242 D5) — an LSP cell and mutation M2b added.
- **Decision 1 — globals rule contradiction:** "`Analyses` honours all nine" contradicted Phase 5
  Task 2 (`emit hover` `--metrics-*` documented-ignored). Rule now: `Analyses` honours the seven
  analysis-facing globals and `--metrics-*` only where the command reports metrics.
- **Decision 4 — seed source:** `CliHelpers.ResolveReferences` is called only inside
  `BuildCommand.CompileToBinary` (`BuildCommand.cs:131`) and its result is local; callers hold raw
  `-r` spellings. Seed from `compileResult.ProjectConfig.References` in-process and from an explicit
  `ResolveReferences` call in `run` before the server branch.
- **Decision 6 — the `Include`/`HintPath` name-mismatch "warning"** is dropped: no parser warning
  channel, no code, no explanation entry, no test, not asked by R-EK.
- **Axis-1 table, `server` row:** `--verbose` IS honoured (log-level raise via `ResolveLogLevel`);
  only the provenance flag is not set — rostered `Honoured`, not `Ignored`.
- **HintPath scaffold users:** `SourceGeneratorTests` ×10, `ClrBaseOverrideRouteMatrixTests` ×1,
  **`DiagnosticFileProvenanceTests` ×1** (was missing from the list and the control run).
- **Phase 4 acceptance:** `SelfContainedDeploymentTests` carries no `Category=SelfContained` trait;
  the new cells are untraited like it (or both classes gain the trait in one commit).
- **References:** #2233's fix is `ae367e782` (not `9af04978b`, a deviations-docs commit);
  the spec allowlist is `build_tools/spec_blocks_allowlist.txt`; "Module search paths" is a bold
  paragraph at `module_system.md:77`, not a heading; the probe workspace path is session-qualified.

### Adequacy
- **Contract:** named — every CLI path/option input is honoured, refused for its kind, or documented,
  uniform across command × reference source × input kind (charter §3.5 wording, #2223 body).
- **Matrix:** larger than the repro lists — the plan enumerates 15 commands × (own + 9 globals),
  {.spy, .spyproj} × 11 commands, 3 sources × 2 states × 8 routes × 3 effects (+ HintPath abs/rel),
  the option-declaration axis, and the registry-door axis. Verifier's arm scan of the touched
  dispatch sites: registry doors 3/3 covered (after the correction above); deployment callers 5/5
  (`CompileSingleFile`, `CompileProject`, `run` ×2 branches, `Publish` ×2 call sites); input-kind
  arms 11/11 (`format`-on-directory is an explicit control); option arms are reflective. One
  cell the plan names only in the edge-case list is now also in the issue body: a bare
  `<Reference Include="X.dll"/>` is rooted at the project directory and never searched on
  `<ModulePath>` — R-EK rules "Include is the path, as today", so this is a spec sentence
  (Phase 3 Task 4 should state it), not a cell to fix.
- **Cure:** seams + sweep — one registry (Decision 1), one classifier/refusal (3), one deployment
  seed (4), one registry helper on every door (5), one parser (6); meta-class row "position/route
  missing a shared check" + "silent wrong behaviour". Not N patches.
- **Harness:** `CliOptionEffectSweepTests` (Phase 1, allowlist `cli-option-effect-allowlist.txt`,
  populated → drained to 0 by Phase 5) + `ReferenceDeploymentMatrixTests` (Phase 4); roster rows
  added as a missing step. Allowlist delta: +N rows at Phase 1 (each `# #2223`), −N by Phase 5;
  no entry on any other axis.
- **Layer:** N/A (CLI/tooling; no Core/Stdlib behaviour) — the plan says so and names the single
  parser / helper / classifier as its "lowest rung" analogue. No rung-4 rule introduced.
- **Owner rulings:** R-EK (a) cited by comment link and not re-asked; R-EL cited; the `project`
  "writes the assembly only" choice is flagged as a two-way door, not a ruling.

### Scope & convergence
- **Group:** one (P46) · chores: none · **Alone:** no — required? no (no program stdout or emitted
  C# changes; help text and new refusals are listed by direction) · **Gates:** 1
- **Generator:** built first in Phase 1 (reflective option × command sweep, synthetic positive
  control, allowlist ratchet) — the charter's own requirement; the reference and input-kind axes
  are closed hand matrices per the charter and #2223's batching comment; the axis the generator
  cannot enumerate (the EFFECT of an honoured option) is named and covered by `GlobalOptionProbes`.
- **Trackers named:** #2223 #2292 #2114 #2182 #2115 #2250 (all OPEN @ 27e261684) · the CS5001
  root-level cell has no open tracker (#1919 #1387 #1214 CLOSED; verified by `gh` search) — ONE
  ledger line, lead files by class · no implementer `gh issue create` · **Fidelity rows:** N/A.
- **Budget:** stated (prober = matrix + ≤2 axis changes; one gate; the two self-contained cells run once).

### Falsifiability
- `CliOptionEffectSweepTests`: mutations M1–M5 named with expected red sets; exemption not the
  subject (the `Performs` rule makes `Ignored` on a compiling command red — the only arm that could
  dodge); positive controls yes (synthetic `probe` command, literal count); by-direction n/a.
- `GlobalOptionProbes`: each probe pairs presence with absence (`--nowarn` has its positive
  control; `[DEBUG]` absent without `--log-level Debug`) — specified.
- `InputKindMatrixTests`: mutations named (classifier → all `SpyFile`; refusal → true) with
  10-red/11-green counts; the absence assertion "stderr free of SPY0013" has its positive control
  in the mutation (the mutant prints it) — record that pairing in the commit body; by-direction
  table specified in §Adversarial Review (verifier re-measured cells a–d above).
- `ProjectReferenceParserTests` / `ProjectReferenceRouteTests` / `RegistryErrorDoorTests`:
  M1–M3 named, M2b added for the `AnalyzeProject` door; inert-fix check present (M2's "no assembly
  written" arm now discriminates pre-compile check from post-compile merge); positive controls
  (resolving reference binds; without the item SPY0300); by-direction specified (the scaffold
  projects + control run at base — now including `DiagnosticFileProvenanceTests`).
- `ReferenceDeploymentMatrixTests`: mutations named (seed ignores user refs → copy cells red,
  controls green; publisher filter restored → self-contained cells red); outputs discriminate
  (`hello from lib` at `dotnet`, not exit codes); `--no-deps` absence has its presence control.
- `ReadmeOptionTableTests`: positive control named (fake command reported); mutation = delete a
  README row → red — implied, record it.
- Phase 5 drain: mutations named (re-drop `--metrics-format`; remove `-m` from `emit diagnostics`).

### Warnings
- **LSP reach of the `AnalyzeProject` door** (now explicit): project-mode SPY0305 publication is a
  user-visible change in the editor; the `.spyproj`-with-dangling-reference state must be probed
  cold and warm (the analysis cache keys on path + mtime: a reference that appears later re-keys,
  but confirm a reference that DISAPPEARS also re-keys — `GetMtimeTicks` on a missing file).
- **`run --server` seed:** the server reply carries `UsedAssemblyPaths` only; the plan now resolves
  `-r` in `run` — make sure the resolution uses the same `modulePaths` the server is handed, or the
  two can disagree on a bare `-r name` found on `-m`.
- **Help-text pins:** the generated "Global options without effect here" sentence lands on the
  command description; `RunHelpTests` pins `run --help` content — `run` compiles, so it gains no
  sentence, but check `GlobalOptionsTests` and any `--help` snapshot before assuming zero churn.
- **Phase 1 allowlist rows for undeclared options** ("expected option missing") are not
  `(command, option)` pairs the walk produces; give them a distinct row grammar
  (`<command path> --reference missing # #2223`) so the stale-row check can match them.
- **`Compiler.CompileProject` returns early before `projectCompiler.Compile`** — `_discoveryTime`
  metrics and `--emit-cs-to` (ProjectCommand) read fields off the result; the early result must
  construct `Metrics` as null-safe (callers use `?.`) — read `ProjectCommand.cs:169-200` when
  implementing.
- **Decision 4's `project` "writes the assembly only"** is a design choice the issue's expected
  ("copied beside the output unless `--no-deps`") does not mention for `project`; it is recorded as
  a two-way door — post it on #2223 at implementation start so the owner can flip it before the
  README text is written.

### Missing Steps Added
- Phase 3: LSP cell for the `AnalyzeProject` door (+ M2b).
- Phase 5 Task 1: update the spec's `-m` command list (`module_system.md:77-78`) and the README's
  `-m` bullet (`README.md:38`).
- Phase 1 Task 2 / Phase 4 Task 2: `gap-discovery-contracts.md` roster row for the sweep; amend
  line 42's "deployment closure" claim.
- Phase 3 Task 4: one spec sentence that a bare `<Reference Include>` is rooted at the project
  directory and not searched on `<ModulePath>` (R-EK "as today").
- Blast radius: `DiagnosticFileProvenanceTests`; LSP project-mode fixtures; help-text pins.

### Unchecked Claims
- `emit hover` sibling-import resolution ("code-read; Phase 1 measures it") — consistent with
  `EmitCommand.cs:412-416` (`api.Analyze(source)` with no path); not executed by the verifier.
- `compile --self-contained` / `run --self-contained` dropping a non-CLI-directory `-r` — code-read
  at `SelfContainedPublisher.cs:67-80` (`Path.Combine(cliDir, fileName)` + `File.Exists`), consistent;
  not executed (slow publish), as the plan states.
- The "nine run-route cells reddened by refusing SPY0306" figure is fa7e8aa63's commit-body claim;
  not re-measured.

### /verify-implementation addendum (2026-10-14 @ 9110a32c6)

- **By-direction correction to §Adversarial Review:** "a program that compiled and **ran correctly** cannot have [a `<Reference>` naming nothing]" is false — a `.spyproj` whose dangling `<Reference>` (missing path, bare `Include="Sharpy.Core"` with no HintPath, missing/whitespace HintPath, a directory) is never imported compiled and ran at 27e261684 and is SPY0305 exit 1 at 10b43ea0b (regression auditor, five programs, clean BASE and HEAD builds). The direction is **worked → rejected**, and it is the direction R-EK ruled ("an unresolved reference is SPY0305 on the project route"); it is recorded here as an owner-ruled refusal, not reverted.
- In-contract cells found by the round and fixed at the seams in 9110a32c6: steer spells the path as given; non-.spy warning keys on `ClassifyInput`; a refused single-file compile creates no output directory; `run --server --verbose` prints the server summary; build/project deployment text pinned (Phase 4 acceptance "pinned text" was unmet at 10b43ea0b).
- Second slice 13af752a5 (class-cure audit W2/W5, prober class 4c): `project app.proj` (valid project file, other extension) worked at BASE and was refused at HEAD with no ruling → narrowed (`ProjectInputKinds = {SpyProject, Other}`); `project`'s refusal moved before the logger and `-m` validation; a directory is refused first by the ONE `RefuseInputKind` on every file-taking command (compile wired through it); `emit diagnostics --include-codegen` `-r`/`-m` cells added (were vacuous). Filed by class: #2223 (malformed `.spyproj` catch-all; `<Reference Include=""/>` dropped; extensionless `Include` + module path = SPY0908 CS0400, owner decision), #2182 (non-assembly `.dll` = SPY0908 CS0009 on every route).
- Owner ruling C (2026-10-14, #2223 comment 6178185165) implemented in 3f16f261e: rooted reference paths exact (no module-path search, no .dll completion), Include normalized, merge writes items back resolved; spec sentence extended. Gate 80185/0/4 @ 3f16f261e (measured, `.claude/tmp/p46-verify/gate-ruling-3f16f261e.log`; +6 tests over 13af752a5); format, spec blocks (1206/0), spy + spy-test staleness clean. Not pushed; #2223 OPEN for the owner.
