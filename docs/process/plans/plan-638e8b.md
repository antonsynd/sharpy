<!-- DRAFT, not verified: no /verify-plan pass has run. Committed for review at the owner's request (2026-11-09). The working copy is the gitignored .claude/plans/plan-638e8b.md; /verify-plan replaces this file when it passes. -->

# P33 — exception model fidelity: socket exception bases, OSError subclass by path state

## Context

Charter `docs/process/rounds/remediation-round-2026-09-20-batching.md` §3.8, row 8 (the Core band, R-LU/R-LT). Two of its open issues, one class: **the exception a Sharpy program can catch is the one python3 raises** (COMPARED row 3 of `docs/design/python-fidelity-scope.md`: exception TYPE and hierarchy; control flow depends on it).

- #2087 — `socket.error` / `socket.timeout` are distinct classes that `except OSError` does not catch.
- #2476 — `os.remove(dir)`, `os.mkdir(existing_file)`, `zipfile.ZipFile(missing)` raise the wrong `OSError` subclass or leak a CLR `IOException` past `except OSError` (class tracker).

#2124 (the tree itself) is CLOSED by W1.5 + W1.7 (R-MD); `ExceptionHierarchyPythonMroTests` is its standing guard and is extended, not replaced. **#2130 (`except*`) left this plan on 2026-11-09 by the owner's ruling ("split"):** its cure is a `CodeGen/` seam, so it becomes its own full-tier group at the next `/batch-issues`; the research is kept in `docs/process/plans/notes-2130-except-star.md`.

## Current State

Measured @ 2c803eb32 (dev tip, `sharpyc` rebuilt at it) against python3 3.13.18, scratch programs under `run`. **Socket:** `except OSError` does not catch a raised `socket.error` (prints `NOT caught by OSError: <class 'socket.error'>`); `TimeoutError` is SPY0200 (no such builtin). **Filesystem:** a generated probe of 48 cells (entry point × path state over `os`, `shutil`, `pathlib`, `zipfile`, `tarfile`, `gzip`, `open`) diverges on 25: 10 leak a CLR type past `except OSError`, 14 raise the wrong subclass, 1 raises nothing; the issue names 3 of the 25. The raise sites spell the mapping themselves: 29 `new <OSError or a subclass>(` sites in 8 handwritten C# files, 24 such `raise` sites in 4 spy modules, 9 C# and 2 spy CLR-catch translation sites (grep @ 2c803eb32; plain `OSError` raises included).

## Scope

- **Group:** P33 — exception model fidelity, MINUS #2130 (owner, 2026-11-09: "split" — #2130 leaves for its own group, full (b, c), placed and lettered at the next `/batch-issues`; the charter's "#2130 FIRST" no longer orders this plan). One non-chore group; chores riding along: none.
- **Tier:** light — from charter §3.8 and the `--light` flag (contract §12). Research surfaced two §12 letters; both are ruled by the owner and neither promotes this plan:
  - **(c), #2130 — RULED 2026-11-09 ("split"):** the `except*` cure is a `CodeGen/` arm, so #2130 is out of this plan. What remains touches no file under `Semantic/`, `Lowering/` or `CodeGen/`.
  - **(b), by the letter — RULED 2026-11-09:** the filesystem matrix is ≥2 axes × ≥3 values, so it gets a generator first (G1). The owner confirmed it ("confirm the matrices needing a generator"): G1 is built first, RED with its allowlist, and the group stays light on this letter (P83's precedent: a light group with a table generator). Not a promotion.
- **Alone:** no — no emitter change; printed output changes only for programs that are wrong today.
- **Gates:** 1.
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class (contract §10); trackers named at the end.
- **Override:** `override: "confirm the matrices needing a generator"` (owner, 2026-11-09) — light is kept against §12(b); the generator is required first. `override: "split"` (owner, 2026-11-09) — the plan covers P33 minus #2130.

## Defect Class

- **Violated contract:** the exception TYPE a handler observes at a Stdlib boundary is python3's — uniform across Stdlib boundary (every entry point that touches the filesystem) and across the name a handler is written with (`OSError` vs a Stdlib module's own class).
- **Meta-class:** silent wrong behavior → **standard cure:** fix at one seam per contract, then a conformance sweep over the sibling cells (the differential generator and the base theory below).
- **Known member cells:** #2087, #2476.
- **Sibling cells this plan must also cover** — axes are rosters, the verdict is a rule:
  - **C2 OSError subclass by path state (G1).** Axes: *entry point* = the image of a Roslyn scan (the technique of `StdlibClrThrowScanTests`) over handwritten and generated C# in `src/Sharpy.Stdlib` plus Core's `Open.cs` and `TextFile.cs` for members that invoke a filesystem API of `System.IO` / `System.IO.Compression` / `System.Formats.Tar`, under an explicit map *member → probe statement* (or an exclusion with a reason); **totality assertion:** every scanned member has a map row or an exclusion (mutation: delete one map row → red naming the member). *Path state* = a closed enum {Missing, MissingParent, ParentIsFile, File, EmptyDir, NonEmptyDir} read with `Enum.GetValues`. **Rule:** the outcome class (no exception / exception type name) equals python3's on the same host, and a python `OSError`-family outcome is never a non-`OSError` in Sharpy. Excluded axes: permission bits (the suite runs as root in containers, where mode bits do not refuse; one unit theory on the translation of `UnauthorizedAccessException` covers the arm), symlinks, path length, Windows-only verdicts.
  - **C3 Stdlib exception base.** Roster: every exported Stdlib exception type whose python namesake has `OSError` in its `__mro__` (oracle-derived, the method of `ExceptionHierarchyPythonMroTests` extended to `[SharpyModuleType]` exceptions) derives from Core `OSError`. Today's members: `socket.error`, `socket.timeout`, `socket.gaierror`, `socket.herror`; the theory finds any other.
- **Measured at base (red floor, not matrix rows) @ 2c803eb32:**

  | Probe | Cells | Diverge | Breakdown |
  |---|---|---|---|
  | filesystem entry × state | 48 | 25 | 10 CLR leaks (`os.mkdir`/`os.makedirs`/`Path.mkdir` on a file, `Path.rmdir` non-empty, `Path.read_text`/`write_text`, `gzip.open`, `zipfile.ZipFile(dir)`), 14 wrong subclass (6 need `NotADirectoryError`, 5 `shutil` plain `OSError`, `os.remove`/`Path.unlink` on a dir, `tarfile.open(dir)`, `zipfile.ZipFile(missing)`), 1 no exception (`shutil.copytree` onto an existing dir) |
  | `raise socket.error` under `except OSError` | 1 | 1 | not caught |

- **Lane / reach:** silent-wrong · program (both). **Fidelity-scope row:** COMPARED row 3 (exception TYPE raised, and the hierarchy). Message text stays DEVIATION by default (R-CI): no acceptance bullet asserts a message.
- **Standing harness that should have caught it:** `DifferentialExecutionTests` compares stdout with python3 but its corpus has no filesystem-failure program; `ExceptionHierarchyPythonMroTests` measures Core types, not raise sites or Stdlib exceptions. G1 and the C3 theory close the two holes.
- **Generator:** REQUIRED FIRST: G1 (C2), landing RED with its allowlist before its fix (owner-confirmed 2026-11-09; the tier stays light by the override in Scope). C3 is a one-axis roster theory, no generator.
- **Owner rulings:** R-MD (OSError family re-rooted on Core); R-CI (message text is DEVIATION); #2087's batching comment <https://github.com/antonsynd/sharpy/issues/2087#issuecomment-5904575425> ("rung 1 first, else a `docs/deviations.yaml` row — the plan decides by trying rung 1"); #2476's placement <https://github.com/antonsynd/sharpy/issues/2476#issuecomment-6724250373> ("one rung-2 helper, modules never spell the mapping; spy-sourced `os` regenerated"); R-MO (a CodeGen cell does not ride P33) and the owner's "split" of 2026-11-09, which applies it to #2130.

## Adversarial Review (pre-mortem)

- **How the fix could be inert:** (1) C2 — a precheck left in a module answers before the helper is reached (`os_module.spy:39` tests `File.exists` first; that ordering IS cell 1), so G1 must run the real entry points, never the helper alone, and the "modules never spell the mapping" scan (A2) must fail on a surviving `raise FileNotFoundError`. (2) C3 — `class error(OSError)` lands in the spy source and the committed generated C# is not regenerated; the staleness gate and the Stdlib fixture both read the regenerated file.
- **Blast radius:** Core.Tests, Stdlib.Tests (fixtures, spy tests, `StdlibClrThrowScanTests`, doc generation), Cli.Tests E2E (loads `Sharpy.Stdlib`), and Compiler.Tests only through the new builtin name of D2. **Before/after by direction (§3):** adding the builtin `NotADirectoryError` turns a user `class NotADirectoryError` into SPY0212 (no fixture declares one; `TimeoutError` is deliberately NOT added — `try_expression_multi_exception_common_base.spy` declares a user `TimeoutError` and would be refused).
- **Generated artifacts touched:** spy-stdlib C# for `os`, `os.path`, `shutil`, `tempfile`, `socket` (`build_tools/regenerate_spy_stdlib.sh`, run at the first spy edit and after the Core helper lands); stdlib docs (`socket`, `os`); `docs/deviations.yaml` → oracle ledger regenerated by `/push`.

## Design Decisions

**D1 — one Core classifier for (path state × what the operation requires) → `OSError` subclass (rung 2).** A static Core helper (callable from spy sources by `from sharpy import …`, the spelling the spy modules already use for Core types) with two doors that share one table: *check(path, requirement)* for operations where .NET is silent or lenient and POSIX refuses (`File.Delete` on a missing path, `Directory.CreateDirectory` on an existing one or with a missing parent), and *translate(clrException, path, requirement)* for a caught `IOException` family member or `UnauthorizedAccessException`. The requirement is a closed enum (needs-file-to-read, needs-file-to-write, creates-exclusively, removes-file, creates-dir, removes-dir, needs-dir, needs-any). The table is python's errno rule spelled once: missing or missing parent → `FileNotFoundError`; a parent that is a file, or a directory required and a file found → `NotADirectoryError`; a file required and a directory found → `IsADirectoryError`; create onto anything existing → `FileExistsError`; remove of a non-empty directory → plain `OSError`; an access refusal with none of those states → `PermissionError`; any other CLR I/O failure → plain `OSError`, never a CLR type. `open()` in `src/Sharpy.Core/Open.cs` (the `catch` ladder at `Sharpy.Core/Open.cs:83`) becomes the first caller, so the W1.7 cells are the helper's positive control. Every Stdlib module site — handwritten C# and spy — calls the helper and stops spelling a subclass. Why not rung 1: 8 handwritten C# files and Core's `open()` need it too, and one table in two languages is two tables. Why not rung 3/4: no compiler fact is involved.

**D2 — Core gains `NotADirectoryError(OSError)` and nothing else (rung 2).** Six measured cells need it and python's tree has it; it gets a row in the "Exception Type Hierarchy" table of `docs/language_specification/exception_handling.md` (an addition the charter's contract rules — python's tree — not a spec edit to match a wrong implementation) and is measured automatically by `ExceptionHierarchyPythonMroTests` (reflection-generated rows). NOT added here: `TimeoutError` (only useful with D3's identity, and it refuses a user class a fixture declares) and the `ConnectionError` family (`src/Sharpy.Stdlib/Requests/Exceptions.cs:43` already declares `Sharpy.ConnectionError` for `requests`; a Core type of the same full name is a CLR collision). Both go to the ledger as cells for the lead.

**D3 — socket exceptions derive from `OSError` in the spy source; the identity is a deviation row (rung 1, as the owner ruled).** Rung 1 was tried @ 2c803eb32: `type error = OSError` in a module gives the full identity for a SOURCE import (`except lib.error` catches a plain `OSError`, `type()` prints `OSError`; two-file project, run), but a compiled library emits no member for an alias (`docs/language_specification/type_aliases.md`: "emits no runtime member"; the library C# has no `error`), and Stdlib is consumed compiled: discovery reads only `[SharpyModuleType]`-stamped CLR types (`OverloadIndexBuilder.cs:58`), and that attribute is `AllowMultiple = false` and assigns the type to the named module instead of its namespace (read at `OverloadIndexBuilder.cs:482`, not run), so stamping Core `OSError` is not available. The identity therefore needs a module-export alias in `Discovery/` + `Semantic/Registry/` (W1.7's shape on the module table) — a compiler seam, outside this tier. In tier: `src/Sharpy.Stdlib/spy/socket_module.spy:72` becomes `class error(OSError)` (probe: an `OSError`-derived class with an `errno` field compiles and is caught by `except OSError` and `except IOError`); `timeout`, `gaierror`, `herror` keep deriving from `error`, so `issubclass(gaierror, OSError)` holds and `except socket.error` still catches a timeout (#945's fixture). The residue is ONE `docs/deviations.yaml` row (`socket-error-timeout-distinct-classes`): `type(socket.error(..))` spells `socket.error`; `except socket.error` does not catch a non-socket `OSError`; `socket.timeout` is not `TimeoutError`. The row names the module-export alias as its removal path.

## Acceptance

Class-level; the implementer sequences the work. Order constraint only: G1 lands RED-with-allowlist before its fix (contract §11), and regeneration runs at the first spy edit (§7).

- **A1 — G1, filesystem differential (Stdlib.Tests `Conformance`).** Generates one Sharpy program and one python program per module from (map row × `PathState`), runs both (python through the shared `PythonInterpreter` locator: SKIPPED when absent, FAILED under `SHARPY_REQUIRE_PYTHON_ORACLE=1`), compares outcome classes. Allowlist file beside it, every row citing #2476. Lands with the base's diverging cells as rows (the 25 measured are its floor; the scan will find more entry points than the hand probe); **drains to 0 rows** with D1/D2. **Mutations:** (a) delete a map row → totality red naming the member; (b) revert one helper table arm (file-required-found-dir → `FileNotFoundError`) → `os.remove(dir)` and its siblings red; (c) restore a module's own precheck ahead of the helper → that module's cells red. **Positive control:** `open(missing)`, `open(dir)`, `os.mkdir(existing_dir)` are cells and are green at base and after.
- **A2 — modules never spell the mapping (scan, Stdlib.Tests `Conformance`).** Zero `new FileNotFoundError|FileExistsError|IsADirectoryError|NotADirectoryError|PermissionError(` and zero `raise` of those names in `src/Sharpy.Stdlib` (C# and spy) and in Core outside the helper's file; zero `catch`/`except` of a CLR `System.IO` exception or `UnauthorizedAccessException` outside the helper's callers' single translate call. No allowlist. **Mutation:** re-add one `raise FileNotFoundError` to `os_module.spy` → red. **Positive control:** the scan's hit count at base is non-zero and is recorded in the commit body, so a scan that finds nothing is not read as clean.
- **A3 — C3 theory (Core.Tests or Stdlib.Tests).** Every Stdlib exported exception whose python namesake has `OSError` in its MRO is assignable to Core `OSError`. **Mutation:** put `socket.error` back on `Exception` → red on 4 rows. Plus the Stdlib fixture `stdlib_socket_qualified_error_types` gains `except OSError` cells (its `.expected` is extended with new lines; no existing line changes).
- **A4 — hierarchy guard unchanged in kind.** `ExceptionHierarchyPythonMroTests` passes with `NotADirectoryError` as a new generated row and the spec table row present; `Bridge_set_is_ExceptionGroup_only` unchanged.
- **A5 — docs.** `docs/language_specification/exception_handling.md`: the hierarchy row for `NotADirectoryError`. `docs/deviations.yaml`: the D3 row, examples executed at HEAD.
- **Execution evidence the close-out cites:** G1's cell and allowlist row counts `@ sha`; both issues' repros under `run`; regen diff of the five spy modules read as findings; the whole-solution gate (`/ci-gate`) at the pushed tip, `passed/failed/skipped @ sha (measured)`.

## Issues to Close

- #2087 — `socket.error` / `socket.timeout` distinct classes — **close criterion:** A3 green, `raise socket.error` under `except OSError` prints caught under `run`, and the D3 deviation row committed; closed to the ledger row for the identity residue, as the owner's batching comment allows.
- #2476 — wrong `OSError` subclass / CLR leak (class tracker) — **close criterion:** A1 at 0 rows and A2 green `@ sha`; the three issue cells under `run`. Stays open while A1 has a row.

## Class trackers this plan may post cells to

- #2476 — OSError subclass and CLR-leak cells of `os`/`zipfile` and their siblings.
- #2467 — standing CLR bug-guard rows of the Stdlib throw scan.
- #2363 — except-handler ordering cells (P13b); not this plan's.
- Ledger cells found while planning, for the lead to file or place (no implementer runs `gh issue create`): `import os` alone then `os.path.join(a, b)` is refused SPY0224 "expects 4 arguments but got 2" @ 2c803eb32, while `import os.path` runs it (false-refusal · program; python accepts both); `from os import path` is SPY0301; `EnvironmentError` is SPY0200 (probe); the `ConnectionError` family and `TimeoutError` are absent from Core (D2); a compiled library exports no module-level `type` alias (D3's removal path).


## Revision log

- @ 2c803eb32 — revised before the first `/verify-plan` pass on two owner rulings of 2026-11-09: the generator is confirmed as required first with the tier kept light; #2130 is split out (its `CodeGen/` seam is §12 c), removing the `except*` contract, its design decision, its generator and its acceptance bullet. Decisions and acceptance bullets renumbered.
