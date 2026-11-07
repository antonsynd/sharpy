<!-- Verified by /verify-plan on 2026-10-12 @ f58a75158 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: built-first -->
# P50 — Formatter blank-line preservation (R-ET, #2239)

## Context

`sharpyc format`, Format Document, Format Selection and (indirectly) every LSP formatting route write a file in one canonical vertical layout: two blank lines around top-level definitions, one between class members, one after the import block — and **every other blank line the author wrote is deleted**. A function body loses its paragraph breaks; module-level statements are packed. The owner ruled this wrong on 2026-10-04 (R-ET, option b): **the formatter preserves up to one user blank line between statements; runs collapse to one**. Rejected: (a) fully canonical layout (today), (c) preserve as written.

- Issue: [#2239](https://github.com/antonsynd/sharpy/issues/2239) — `lane:tooling` · `reach:program` · `class-tracker`. Ruling R-ET in the round charter (`.claude/plans/remediation-round-2026-09-20-batching.md` §3.3 and the rulings table) and in the issue body; no comments. Authoritative — do not re-ask.
- Group **P50**, charter §3.3: "Pretty + LSP routes; lands alone (formatted output changes); generator FIRST; before P53 (both touch the LSP handlers' test driver)".
- Reference, not oracle: `python3 -m black` 24.10.0 is installed. On the matrix below black agrees with R-ET on every placement except three the ruling or Decision 4 decides otherwise (black keeps a blank line at the start of a body, allows two at module level, and writes TWO blank lines between a module-level comment and the `def` that follows it; R-ET/this plan: removed, one, one) [CORRECTED: black 24.10.0 run @ f58a75158 on `y = 2` / blank / `# c` / blank / `def g()` writes `# c` / 2 blank / `def g()` — the comment is separated from the def and canonical 2 applies; the plan writes 1]. The formatter is not a Python-parity surface (`python-fidelity-scope.md` lists runtime surfaces); no `deviations.yaml` row.

## Current State

Measured @ f58a75158 with the built `src/Sharpy.Cli/bin/Debug/net10.0/sharpyc` (built 18:40; no `src/Sharpy.Compiler` or `src/Sharpy.Cli` file changed between that build and HEAD — `git log --since … --name-only` empty), `sharpyc format <in> -o <out>`:

The issue's program (`def main():` with `a = 1` / blank / `b = 2` / `# section two` / blank / `print(a + b)`, then `x = 1` / blank / `y = 2`) formats to

```
def main():
    a = 1
    b = 2
    # section two
    print(a + b)


x = 1
y = 2
```

A 46-line placement matrix (`<scratchpad>/probe/matrix.spy`: leading blank lines, blank runs between imports, between module statements, after a comment before a `def`, at a body start, inside a body (1 and 2), after a comment, before `else:`, after a block, between a decorator and its `def`, after a docstring, at a class-body start, between members (3), after a trailing comment, at EOF) comes back with **every** user run removed and only canonical spacing written — the same output black 24.10 gives only at the decorator, module-start and EOF cells.

Mechanism (read, not inferred):

- **Lexer** (`src/Sharpy.Compiler/Lexer/Lexer.Indentation.cs:136`, `Lexer.cs:946`): with `preserveTrivia`, every blank or whitespace-only line at bracket depth 0 increments `_pendingBlankLineCount`; `FlushPendingBlankLines()` records ONE `Trivia { Kind = BlankLines, BlankLineCount = n, Line = _line }` — `Line` is the line that ENDS the run (the next comment line, `Lexer.Indentation.cs:101`; the next code line, `:162`; or EOF, `Lexer.cs:666`). It becomes `LeadingTrivia` of the next non-`Indent`/`Dedent` token (`Lexer.cs:936`). A blank line inside brackets is never trivia (`Lexer.cs:768` — newlines inside brackets `continue` before the line-start handler).
- **Cursor** (`src/Sharpy.Compiler/Pretty/TriviaCursor.cs`): `FromTokens` collects every comment and blank-run item in source order; `TakeBefore(line)` hands a run to the anchor whose line ends it (`:143`); `TakeBodyEnd` includes runs between body-end comments (`:194`).
- **Unparser** — the ONLY two consumers of a blank run: `UnparseVisitor.WriteOwnLine` (`UnparseVisitor.cs:343`: `if (_options.Formatting != null) continue;` else writes `BlankLineCount` lines) and `CloseBody` (`:299`, same rule). Canonical spacing is written BEFORE the anchor by five loops: `UnparseModule` top-level defs (`:61`) and the import gap (`:67`), `WriteEndOfModule` (`:107`), `WriteBody` members (`:576`), `VisitUnionDef` members (`UnparseVisitor.Statements.cs:707`). `UnparseWriter.WriteLine()` absorbs one terminator after a block-bodied expression (`_lineEndedByBlockExpression`, #2227).
- **Routes**: `FormatterService.Format` (CLI, Format Document) and `FormatRange` (Format Selection: `LineDiff.Hunks(source, Format(source))` filtered by `FormatSelection.Selects`); format-on-type is indent-only (`OnTypeFormattingHandler` → `FormattingEdits.CheckedIndentOnly`) and never touches a blank line. There is no second formatter.

Pinned today (all flip under R-ET): `Sharpy.Lsp.Tests/FormattingTests.cs:72` `BlankLines_InsideFunctionAreNormalizedAsync`, `:99` `MatchExpressionStatement_BlankLineAfter_IsNormalizedLikeAnyStatementAsync`; `Sharpy.Lsp.Tests/Conformance/LspFormattingDriverTests.cs:191` `Full_FormatsTheDocument` and `:232` `Requests_FromManyThreads_…` (both format `x: int = 1\n\n    return x`); `Sharpy.Compiler.Tests/Conformance/FormatterMatchExpressionLayoutMatrixTests.cs:192` (`expected.RemoveAt(userBlankLine)`); fixtures `Integration/TestFixtures/Formatting/comments_and_blank_lines.formatted` and `match_expression_statements.formatted` (`then_blank`). Fixtures whose runs are all canonical-sized stay byte-identical: `backtick_escapes`, `comments`, `let_declarations`, Stdlib's `already_formatted`.

## Scope

- **Group:** P50 — formatter blank-line preservation (one non-chore group; chores riding along: none)
- **Alone:** yes — changes formatted output (`.formatted` fixtures, LSP applied text); charter §3.3 "lands alone"
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`); the lead files by class (contract §10); trackers named at the end of this plan
- **Override:** none
- **Verify cost / prober budget:** [ADDED: missing step] one whole-solution gate per phase (3), the sweep's FULL run once (Phase 2), the FULL route sweep once (Phase 2), the base-binary direction probes once; the sibling-cell prober's budget is the Defect Class matrix plus at most two axis changes (contract §10) — the two it may spend: a header kind not in the matrix (`with`/`while`/`async`) and an inline-comment-bearing carrier (`x = 1  # c` / blank / `y`).

## Defect Class

- **Violated contract:** *A blank-line run the lexer records between two anchors is written by every formatting route as exactly ONE blank line, except at a body start, the module start, after a decorator line and at end of file, where it is written as none; canonical spacing — as `UnparseModule`/`WriteBody` write it TODAY: 2 before a top-level definition and after one when a non-import statement follows, 1 between body items when either is a member def (in EVERY body, not only a class), 1 after the import block — is a minimum the written count never exceeds.* Uniform across placement × run length × route × line break. [CORRECTED: the canonical rule is restated from the code — `UnparseVisitor.cs:59` writes 2 after a def only when `!isImport`, so a top-level def followed by an `import` gets NO blank line today (measured @ f58a75158: `def f():` / `    pass` / `import json`); R-ET says canonical spacing is unchanged, so this plan does not touch that arm — it is a ledger cell (below), and the doc sentence in Phase 3 states the real rule.]
- **Meta-class:** "Position/route missing a shared check" (contract §1 table) → **standard cure:** the placement fact is computed at ONE seam (`TriviaCursor`, from the token stream) and consumed at the only two blank-run writers; a placement × run-length × route sweep so a new anchor kind cannot skip it. (The defect is a ruled-wrong rule, not an accident — the cure is the same.)
- **Known member cells:** #2239 (the issue's two programs).
- **Sibling cells this plan must also cover (the issue's repro list is a symptom report):**

  | axis | values |
  |---|---|
  | placement (kept → 1) | inside a function body · inside a nested block (`if`/`for`/`with`/`try` body) · class body between methods (canonical 1 ⇒ 1) · class body between FIELDS (canonical 0 ⇒ 1 — `WriteBody` writes the member gap only when either neighbour is a member def) · a nested `def` inside a function body (canonical 1 ⇒ 1, same `WriteBody` arm) · between match-EXPRESSION arms (anchors via `WriteAnchored`, `UnparseVisitor.Expressions.cs:448`; dropped today, measured @ f58a75158) · between enum members (`Statements.cs:426`) · between union cases (`:697`) · between the last union case and its first method (canonical 1 ⇒ 1) [CORRECTED: cells added — every `WriteAnchored` caller is a carrier the fix reaches, so each anchor kind is a cell] · module level between statements · before a leading comment · after a comment (comment → statement) · after a block-bodied statement (carrier follows `Dedent`) · after a match-EXPRESSION statement (#2227's absorbed terminator) · before a clause header (`elif`/`else`/`except`/`finally`) · between `match`-statement arms · after a docstring (function and class) · between imports · after the import block (canonical 1 ⇒ 1) · before a top-level `def`/`class` (canonical 2 ⇒ 2) · before an end-of-file comment · between body-end comments (`CloseBody`) |
  | placement (dropped → 0) | body start before the first statement · before the first match-EXPRESSION arm (the arms follow an `Indent`) [ADDED: adequacy] · body start before a docstring · body start before a leading comment · class-body start before a decorated member · module start (before code; before a leading comment) · between two decorators · between a decorator and its header · end of file (after code; after a trailing comment) |
  | not a run (no contract) | inside brackets (canonical rewrite drops; a construct with an inner comment is verbatim and keeps it) · inside a triple-quoted string (guarantee 1, untouched) |
  | run length | 0 · 1 · 2 · 5 (and whitespace-only lines, which the lexer counts as blank) |
  | route | `cli` · `full` · `range-whole` · `range-statement` · `range-line` · `ontype` |
  | line break | LF · CRLF · lone CR (the route sweep's twins) |

- **Lane / reach:** tooling · program; **fidelity-scope row:** none — not a Python-runtime surface (black is a reference only).
- **Standing harness that should have caught it:** none — `FormatterMeaningPreservationSweepTests` judges meaning (AST, comments, escapes, idempotence), not layout, and the layout was the rule until R-ET. **Phase 1 adds `FormatterBlankLineSweepTests`.**
- **Generator:** **REQUIRED FIRST** — the matrix is ≥2 axes × ≥3 values. Phase 1 builds `FormatterBlankLineSweepTests` over the fixture corpus (identity + comment twins) with a differential, model-free oracle (below) and its allowlist ratchet (`formatter-blank-line-allowlist.txt`), red-pinned at Phase 1 and drained by Phase 2. The route axis is already generated by `FormattingRouteParitySweepTests` (`cli`/`full`/`range-whole` are judged byte-equal to `Format(D)`, `range-*` by the restated `Selects`, `ontype` by `ontypeShape`): once `Format` changes, every route follows by construction and the sweep's FULL run is the route evidence. The axis the generator cannot judge: the **exact bytes per placement class** — the sweep's expectation is derived from `Format(Q₀)` plus a RESTATED placement predicate, so a predicate wrong the same way in cursor and test would pass; the hand fixture `Formatting/blank_line_placements.spy/.formatted` pins the bytes of every placement class, and that is the only hand matrix.
- **Owner rulings:** R-ET (charter rulings table; issue body, 2026-10-04): "preserve up to one user blank line between statements; runs collapse to one; canonical spacing around `def`/`class` is unchanged; first/last line of a body and end of file still removed". R-GO/R-GC: P50 keeps its slot directly after chore R-GP, before P53.

## Adversarial Review (pre-mortem)

- **Alternative root cause:** "the lexer never records the runs" — refuted: `UnparserTriviaAndFormattingTests.Roundtrip_ThreeBlankLinesBetweenFunctions_Preserved` (`Formatting == null`) round-trips `\n\n\n\n` through the same cursor; the drop is the explicit `Formatting != null → continue` arm at `UnparseVisitor.cs:343` and `:301`. Discriminating probe: `Unparser.Unparse(module, new UnparseOptions { PreserveTrivia = true })` on the issue program keeps the runs; `FormatterService.Format` removes them.
- **Alternative layer:** (i) the lexer could classify a run's placement at flush time — rejected: it flushes BEFORE `MeasureIndentation` emits the `Indent`, so it would need a post-fix on the pending trivia, and P68 (#2300) is about to instrument the lexer; the cursor already derives every other line fact (body end, docstring, verbatim slice, `EndLineOf`) from the token stream at the same rung. (ii) the writer could "arm suppression" after each block header's `:` + `WriteLine()` — rejected: ~20 header arms across `UnparseVisitor.Statements.cs`/`Expressions.cs` (a mirrored-site class); one token-stream predicate in the cursor replaces them all. (iii) A `.spy`/Core/Stdlib rung does not apply (compiler tooling, no language semantics).
- **How the fix could be inert:** (a) `EnsureBlankLines` counting the writer's trailing line endings while `_lineEndedByBlockExpression` is set — the next `WriteLine()` would be absorbed, writing nothing: the #2227 cell (`then_blank` in `match_expression_statements`) is in the fixture and the sweep corpus; `EnsureBlankLines` clears the flag and appends directly. (b) A run handed to an anchor that writes its trivia through a path other than `WriteOwnLine`/`CloseBody` — there is none (grep `TriviaKind.BlankLines` in `Pretty/`: `UnparseVisitor.cs:299,347`, `TriviaCursor.cs` only); the sweep catches a third if one appears. (c) The `FromAst` cursor (no tokens) — test-only with `Formatting != null` (`UnparserTriviaAndFormattingTests.FormatSource`); production always passes `SourceTokens` (`Formatting/FormatterService.cs:175` [CORRECTED: path and line]). Its runs get `Placement.Unknown` and keep today's drop — documented, and the sweep runs through `FormatterService`, so it cannot be fooled by that path.
- **Before/after by direction:** two probes, both `run`-style (format and compare bytes), against the base binary built from the pre-plan sha in `sharpy.worktrees/`: (1) **fixed points stay fixed** — for every corpus fixture P, `Format_new(Format_old(P)) == Format_old(P)` (today's output has no user run, so nothing is preserved and canonical spacing is unchanged; a difference is a regression); (2) **preservation only adds** — `Format_new(P)` differs from `Format_old(P)` only by inserted blank lines, each at a `Between` run of P; `LineDiff.Hunks(Format_old(P), Format_new(P))` are pure insertions of empty lines. No refusal direction: SPY0912 is untouched; the meaning sweep's `refused` bucket must not change.
- **Blast radius:** `Sharpy.Compiler.Tests` (Formatting/, Conformance/ formatter sweeps and matrices, Pretty/, Integration `FormatterIntegrationTests`), `Sharpy.Lsp.Tests` (FormattingTests, LspFormattingDriverTests, `FormattingRouteParitySweepTests` incl. FULL mode and its `RefusalCeilingPercent`, `FormatterDocEditorExamplesTests`, E2E), `Sharpy.Cli.Tests` (`FormatCommandTests` — canonical-only expectations, no flip expected), `Sharpy.Stdlib.Tests` (`FormatterIntegrationTests` — `already_formatted` is canonical, no flip expected). No `Semantic/`/`CodeGen/` change: no `.expected.cs`, no interop/metamorphic/differential sweeps, no warm/cold.
- **Generated artifacts touched:** none (no spy-stdlib C#, spy-test C#, stdlib docs or oracle ledger). The `.formatted` fixtures are hand expectations regenerated by running the formatter and reading the diff as findings (§7) in Phase 2.

## Design Decisions

1. **The placement of a blank run is a token-stream fact computed once, in `TriviaCursor.FromTokens`** (rung: compiler tooling; the lower rungs do not apply). `enum BlankRunPlacement { Between, BodyStart, ModuleStart, AfterDecorator, EndOfFile, Unknown }`, looked up by `TriviaCursor.PlacementOf(Trivia)`. Definitions, with the carrier = the token whose `LeadingTrivia` holds the run (its item list = the run, the comments before it and after it on the carrier's leading trivia):
   - `EndOfFile` — the carrier is `Eof`.
   - `ModuleStart` — the carrier is the first non-`Indent`/`Dedent`/`Newline` token of the stream AND the run is the first item of its leading trivia (a run after a leading comment is `Between`).
   - `BodyStart` — the token before the carrier in the stream is `Indent` AND the run is the first item of the carrier's leading trivia (`def f():` / blank / `# c` / `x` → the run precedes the comment → `BodyStart`; `def f():` / `# c` / blank / `x` → `Between`). A docstring's string token is a carrier like any other, so a run between a header and its docstring is `BodyStart` and a run after the docstring is `Between`.
   - `AfterDecorator` — the previous ITEM (code token or own-line comment) before the run is on a logical line whose first code token is `TokenType.At` (between two decorators, or between the last decorator and the header). A comment between the decorator and the run makes the run `Between`.
   - `Between` — everything else, including a carrier after a `Dedent` (after a block; before `else`/`elif`/`except`/`finally`/`case`), between imports, before a comment, after a comment, between body-end comments, between match-expression arms, between enum members and union cases [CORRECTED: anchor kinds added — all reach `WriteOwnLine` through `WriteAnchored`].
   - `Unknown` — every run of a `FromAst` cursor (no tokens).
2. **The writer gets `EnsureBlankLines(int n)`: append line endings until the output ends with ≥ n blank lines** (counts trailing line endings after the last non-empty line; clears `_lineEndedByBlockExpression` and appends directly, never through the absorbing `WriteLine()`). Every canonical loop (`for (b < fmt.BlankLinesAroundTopLevelDefs) _w.WriteLine()` × 5 sites) becomes `_w.EnsureBlankLines(fmt.N)`, and a `Between` run in format mode is `_w.EnsureBlankLines(1)`. Because canonical spacing is written BEFORE the anchor's trivia (`UnparseModule` → `VisitStatementWithTrivia` → `WriteAnchored` → `WriteOwnLine`), the composition is `Ensure(canonical)` then `Ensure(1)`: canonical is a minimum the run never exceeds, and a run before a top-level `def` yields 2, between members 1, after the import block 1, elsewhere 1 — idempotent by construction (re-lexing the output yields runs of 1 or 2 that `Ensure` reproduces).
3. **Non-format mode is unchanged**: `Formatting == null` keeps writing `BlankLineCount` lines verbatim (the `Roundtrip_*` tests); `Unknown` placement in format mode keeps today's drop (test-only path).
4. **A blank line between a leading comment and its statement is preserved** (`x = 1` / blank / `# c` / blank / `def f()` → `x = 1` / 2 blank / `# c` / 1 blank / `def f()`): the comment is an anchor item, so the run after it is "between statements" in the ruling's sense. [CORRECTED: black does NOT do the same — black 24.10.0 writes `# c` / 2 blank / `def f()` here (the comment is separated from the def, so black applies its canonical 2 before the def). R-ET does not rule this cell; the plan's choice (1, the capped user line) stands as a Design Decision — if the owner prefers black's shape, the alternative is `Ensure(fmt.BlankLinesAroundTopLevelDefs)` for a `Between` run whose carrier is a top-level def header, one arm in `WriteOwnLine`; record the choice in the close-out.]
5. **Blank lines inside brackets are outside the contract** (never trivia; canonical rewrite joins the lines, a verbatim construct keeps them) — stated in the doc, not changed.
6. **The sweep's oracle is differential and model-free.** For a document Q that `Format` accepts: Q₀ = Q with every run's lines deleted (instrument: Q₀ has zero `BlankLines` trivia, the same normalized AST and comment sequence as Q); Q⁺ = Q with two extra empty lines in every run (instrument: same AST and comments). F = Format(Q), F₀ = Format(Q₀), F⁺ = Format(Q⁺). Items = the merged source-ordered list of LOGICAL LINES (the token groups the lexer separates with `Newline` tokens at bracket depth 0 — the first code token of each) and own-line comments [CORRECTED: not code tokens — the formatter respells tokens (`except KeyError, IndexError:` → `except (KeyError, IndexError):`, 52 → 54 tokens, measured @ f58a75158), so a token-indexed alignment of Q to F mis-attributes every run after the first respelling; logical-line count IS stable — semicolons are not Sharpy syntax (SPY0103), a backslash continuation lexes as one logical line in both (2 `Newline` in, 2 out), and the meaning net guarantees the own-line comment sequence]. The oracle asserts `|Items(Q)| == |Items(F)| == |Items(F₀)|` as an `instrument` check before comparing gaps (a mismatch is listed by stem, never silently miscounted); `gapBefore(i)` = `max(0, startLine(i) − endLine(i−1) − 1)` (end line via the token's source extent, so a triple-quoted string spans). Then: at an item that carries a `Between` run in Q, `gapBefore_F(i) == max(1, gapBefore_F₀(i))`; at every other item (no run, or a dropped placement), `gapBefore_F(i) == gapBefore_F₀(i)`; leading and trailing blank lines of F₀ are 0; and `F⁺ == F` byte-for-byte. The expectation needs no model of canonical spacing (F₀ supplies it) — only the restated placement predicate, whose bytes the hand fixture pins.
7. **The ratchet is a pin while red, rows after** (the P22h/P22i pattern): Phase 1 lands the sweep with ONE pin row (`pin identity blankLost <count> <sha256 of the sorted failing stems>` citing #2239, and one for the comment twin); Phase 2 deletes the pins in the fix commit; the census anchors the row count at the literal 0 thereafter. Buckets: `instrument` (a twin does not parse / changes the AST / Q₀ still has runs / the item counts of Q, F, F₀ differ [CORRECTED: added]), `refused` (census: `Format` refused Q, Q₀ or Q⁺ [CORRECTED: all three — a refusal of a derived twin alone leaves the cell unjudgeable and is counted, not failed] — the meaning sweep's business), `blankLost` (a `Between` run written as 0 lines — today's defect), `blankShape` (any other gap mismatch: invented, not collapsed, a dropped placement kept, leading/trailing not 0), `blankLength` (`F⁺ != F`), `notIdempotent` (`Format(F) != F`, the sweep's own check so a blank-line fix cannot break idempotence unseen).

## Implementation

### Phase 1: The generator — `FormatterBlankLineSweepTests` (red-pinned) and the placement fixture

**Goal:** the class contract is executable over the whole corpus before any production line changes; today's defect is a measured count under a pin.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: the sweep runs over `FormatterMeaningPreservationSweepTests.CorpusCensus` (identity + comment twins), prints `FMTBLANK <stem> <twin> <buckets>` per cell and a `FMTBLANK-CENSUS` line (corpus size, cells with ≥1 run, runs per placement class, pin counts), and is green with the two pins @ sha (measured).
- Guard mutation: today IS the broken state, so the pin's count is the "broken → red" record by construction (a `blankShape` mutation cannot fire yet — every run is dropped). Record two falsifications in the commit body: (M-a) the oracle's `max(1, g₀)` → `g₀` makes `blankLost` count 0, the pin goes stale → red; (M-b) pin count +1 → red. Restored → green.
- Positive controls for the census: every placement class in Decision 1 occurs at least once in the corpus (anchored by the fixture added here — a literal per class, not a count of the same source).
- Allowlist entries that drain: none yet (two pins created).
- Execution evidence the close-out will cite: `FMTBLANK-CENSUS` line @ sha; the pin counts.

#### Tasks

1. **Placement fixture (today's bytes)** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/Formatting/blank_line_placements.spy` + `.formatted` + `.skip`
   - One program containing every placement cell of the Defect Class table (kept and dropped), with run lengths 1, 2 and 5 and one whitespace-only line, the #2227 match-expression statement followed by a blank line, a bracketed construct with a blank line inside (and a twin with an inner comment, verbatim), and a triple-quoted string containing a blank line. Line-numbered comments name each cell (`# cell: body-start-before-docstring`).
   - `.formatted` = the output of `FormatterService.Format` TODAY (write it by running the formatter, then read the diff against the `.spy` as the record of what is deleted today). It is replaced in Phase 2 — the Phase 1 → Phase 2 diff of this file is the direction record.
   - Acceptance: `FormatterIntegrationTests` green on it (byte-exact + idempotent); the sweep corpus contains it (`.skip` fixtures are in the parse-level corpus).
   - Commit: `test(format): the blank-line placement fixture pins today's layout — every run deleted (P50 Phase 1, refs #2239)`

2. **`FormatterBlankLineSweepTests`** — `src/Sharpy.Compiler.Tests/Conformance/FormatterBlankLineSweepTests.cs`, `src/Sharpy.Compiler.Tests/Conformance/formatter-blank-line-allowlist.txt`
   - Corpus: `FormatterMeaningPreservationSweepTests.CorpusCensus.Value.Corpus` (shared, read once). Twins: `identity` (Q = P) and `comment` (`FormatterTwins.CommentInjected(P)` — the before/after-comment axis at every anchor kind). One theory per twin, `[MemberData(CorpusNames)]`.
   - Observation helpers (test-side, restated on purpose): `Runs(tokens)` = every `BlankLines` trivia with its carrier token and its index among the carrier's leading trivia; `PlacementOf(run)` per Decision 1 (restated — the cursor's predicate is production, this one is the oracle's); `Items(tokens)` and `Gaps(text)` per Decision 6 (end line of an item from `TriviaCursor.EndLineOf`-equivalent source arithmetic — do not call the cursor); `WithoutRuns(text)` (Q₀: delete lines `[Line − Count, Line − 1]` of each run, 1-based, splitting on the lexer's line-break rule via `LineDiff.Split`) and `WithLongerRuns(text)` (Q⁺: two extra empty lines before each run's `Line`).
   - Oracles per Decision 6 and buckets per Decision 7; `refused` is census (skip the cell, count it); the comment twin's instrument check reuses the meaning sweep's (`counts[EndOfFile] == 1`, same AST, comment count).
   - Ratchet: rows `stem twin bucket # #N reason` and pins `pin twin bucket count sha256 # #N reason`; a non-listed failure fails; a listed row that holds fails (drain on fix); a pin whose count or digest differs fails (exact in both directions). Loader refuses unknown twins/buckets and a cite that is not `#\d+`. Census asserts the row count literal (0 rows + 2 pins at Phase 1; changed in the same commit as the file, with the reason).
   - Census positive controls: each `BlankRunPlacement` value (except `Unknown`) is observed in the identity corpus ≥ 1 time, each anchored to `Formatting/blank_line_placements` by name (the fixture must contain it — a `Should().Contain(stem)` per class); `Formatting/blank_lines` remains the format-runs control.
   - Budget: three `Format` calls per cell; report wall time in the census line. If the comment twin exceeds 120 s on the lead's machine, sample it stride-8 like the route sweep and say so in the census (`SHARPY_BLANK_SWEEP_FULL=1` runs all).
   - Mutation record (commit body): (M-a) oracle `max(1, g₀)` → `g₀`: pin stale (count 0 ≠ pinned) → red; (M-b) pin count +1 → red; (M-c) `WithoutRuns` made a no-op → `instrument` red on every cell with a run (Q₀ still has runs).
   - Commit: `test(format): FormatterBlankLineSweepTests — a user blank-line run between statements survives formatting as one line, differential against the run-free twin over the corpus; red-pinned to #2239 (P50 Phase 1)`

3. **Roster entry** — `docs/design/gap-discovery-contracts.md`
   - New row after "Formatter meaning preservation": contract sentence (Defect Class), harness, twins, buckets, pin/ratchet file, "populated at P50 Phase 1 with 2 pins; drained at Phase 2".
   - Commit: `docs(design): roster row for the formatter blank-line sweep (P50 Phase 1, refs #2239)`

### Phase 2: The fix — placement in the cursor, `EnsureBlankLines` in the writer, one rule at the two writers

**Goal:** every route writes `Between` runs as one blank line and dropped placements as none; the sweep's pins drain; the direction probes hold.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `FormatterBlankLineSweepTests` with 0 rows and 0 pins @ sha; `FormatterMeaningPreservationSweepTests` + `FormatterEmitInvarianceSweepTests` unchanged (0 rows; the `refused` census count identical to the base sha); `FormattingRouteParitySweepTests` in FULL mode (`SHARPY_ROUTE_SWEEP_FULL=1`) 0 rows, `RefusalCeilingPercent` unchanged; `FormatterIntegrationTests` (Compiler + Stdlib) green with the regenerated `.formatted`; `FormatterMatchExpressionLayoutMatrixTests` green.
- Guard mutation (commit body): (M-1) `EnsureBlankLines(1)` → nothing for `Between` → sweep `blankLost` red on N cells, `blank_line_placements` byte-red; (M-2) write `BlankLineCount` lines instead → `blankLength` red (F⁺ ≠ F) and `blankShape` red on runs ≥ 2; (M-3) cursor `BodyStart` → `Between` → `blankShape` red on the fixture's body-start cells and `blank_line_placements` byte-red; (M-4) `EnsureBlankLines` appending through the absorbing `WriteLine()` → `then_blank` cell red (the match-expression blank is swallowed). Each: broken → red (counts), restored → green.
- Allowlist entries that drain: both pins in `formatter-blank-line-allowlist.txt` (deleted in the fix commit; census literal → 0).
- Execution evidence: the issue's two programs @ sha (`sharpyc format`), the fixture diff Phase 1 → Phase 2, the direction probes (fixed points stay fixed: 0 differing corpus fixtures; preservation only adds: every hunk a pure empty-line insertion) against the base binary from a `sharpy.worktrees/` checkout.

#### Tasks

1. **Placement classification in the cursor** — `src/Sharpy.Compiler/Pretty/TriviaCursor.cs`
   - Add `internal enum BlankRunPlacement` (Decision 1) and compute a `Dictionary<Trivia, BlankRunPlacement>` (reference keys) in `FromTokens` from the token stream: carrier, previous stream token (`Indent`?), first-item test, previous item's logical line starting with `At`, `Eof`. `FromAst` → `Unknown` for every run. Expose `PlacementOf(Trivia)`.
   - Unit tests in a new `src/Sharpy.Compiler.Tests/Pretty/TriviaCursorPlacementTests.cs`: one row per placement class × {first item, after a comment} × {LF, CRLF}; mutation: flip the `Indent` test → the BodyStart rows red.
   - Commit: `feat(format): the trivia cursor classifies each blank-line run's placement — body start, module start, after a decorator, end of file, or between (P50, refs #2239)`

2. **`EnsureBlankLines` in the writer** — `src/Sharpy.Compiler/Pretty/UnparseWriter.cs`
   - `public void EnsureBlankLines(int count)`: trailing-blank-line count of `_sb` (line endings after the last non-line-ending char, minus one when the buffer is not at a line start… specify: if the buffer is empty or not `AtLineStart`, do nothing — a blank run is only ever written at a line start); append `_lineEnding` until the count is reached; clear `_lineEndedByBlockExpression`. Unit tests in `src/Sharpy.Compiler.Tests/Pretty/UnparseWriterTests.cs` (exists [CORRECTED: it does]): tail with 0/1/2 blank lines × ensure 1/2; after `EndLineByBlockExpression()` the ensured line is real; CRLF line ending.
   - Commit: `feat(format): UnparseWriter.EnsureBlankLines — a minimum of trailing blank lines, never absorbed by a block expression's terminator (P50, refs #2239)`

3. **The rule at the two blank-run writers and the five canonical sites** — `src/Sharpy.Compiler/Pretty/UnparseVisitor.cs`, `UnparseVisitor.Statements.cs`
   - `WriteOwnLine` and `CloseBody`: in format mode, `Between` → `_w.EnsureBlankLines(1)`; `BodyStart`/`ModuleStart`/`AfterDecorator`/`EndOfFile`/`Unknown` → nothing; non-format mode unchanged (writes `BlankLineCount`). Enumerate in the commit body that these are the ONLY consumers (`grep -n "TriviaKind.BlankLines" src/Sharpy.Compiler/Pretty/`).
   - Canonical sites → `EnsureBlankLines(n)`: `UnparseModule` defs (`:61`) and import gap (`:67`), `WriteEndOfModule` (`:107`), `WriteBody` (`:576`), `VisitUnionDef` (`Statements.cs:707`). Grep `BlankLinesAroundTopLevelDefs|BlankLinesBetweenClassMembers` to confirm five and no sixth.
   - Update the pinned tests to the ruled layout (names say what they guard now): `FormattingTests.BlankLines_InsideFunctionAreNormalizedAsync` → `OneUserBlankLine_InsideFunctionIsKeptAsync` (and a sibling `ThreeUserBlankLines_CollapseToOneAsync`); `MatchExpressionStatement_BlankLineAfter_IsNormalizedLikeAnyStatementAsync` → kept, never doubled (expected `case _: "other"\n\n    return s`); `LspFormattingDriverTests` `:191`/`:232` keep the blank (their subject is the driver, not the layout — say so in a comment); `FormatterMatchExpressionLayoutMatrixTests` exact-layout branch keeps the user's line (drop the `RemoveAt` at `:193`; the `followed_by_user_blank_line` position is the flipping cell; the differential oracle still compares against the plain twin). Add a `blank_between_arms` position to its matrix (a user blank between two arms of the expression → one kept line; before the first arm → none) [ADDED: missing step — the arms are anchors the fix reaches and no existing cell varies a blank INSIDE the expression].
   - Regenerate `.formatted` for `comments_and_blank_lines`, `match_expression_statements` and `blank_line_placements` by running the formatter; read the three diffs as findings (every changed line is a `Between` run becoming one blank line, or a dropped placement staying dropped — anything else is a finding for the ledger). Confirm `backtick_escapes`, `comments`, `let_declarations` and Stdlib `already_formatted` are byte-identical.
   - Delete both pins from `formatter-blank-line-allowlist.txt`; census literal → 0.
   - Commit: `fix(format): a user blank line between statements survives formatting as one line; runs collapse; body start, module start, after a decorator and end of file stay blank-free (R-ET, P50, closes-criterion #2239)`

4. **Route cells by hand (the LSP driver), and the direction probes** — `src/Sharpy.Lsp.Tests/FormattingTests.cs`, `RangeFormattingTests.cs`, `OnTypeFormattingTests.cs`
   - Format Document: the issue's program → kept lines (one assertion of the whole text). Format Selection on a body with a 3-line run: selection touching the run → the deletion hunk `[a, b)` applies (run → 1); selection of the statement before the run only (`range-statement`) → the hunk is not selected (`Selects`: a deletion touches iff it overlaps), text unchanged; selection covering a `Between` run of 1 → no hunk at all. On type on the line after a blank line → only that line's indentation may change (the blank stays); on type on a blank line → no edit (exists: `BlankLine_NoEditAsync`). CRLF twin of the Format Document cell (output `\n`, runs preserved).
   - Direction probes (recorded in the commit body, not as tests): build the base sha in `sharpy.worktrees/`; over every corpus fixture, `Format_new(Format_old(P)) == Format_old(P)` (count of differing stems = 0) and `LineDiff.Hunks(Format_old(P), Format_new(P))` all pure empty-line insertions (count of other hunks = 0). A script under `<scratchpad>/p50-direction/` — never a committed tool.
   - Run `FormattingRouteParitySweepTests` FULL; drain any row by fixing the class, not the cell; report the `RefusalCeilingPercent` census beside the base run.
   - Commit: `test(lsp): Format Document, Format Selection and on type on user blank lines — one kept, runs collapse where the selection touches them (P50, refs #2239)`

### Phase 3: The doc states the vertical-spacing rule with an executed example

**Goal:** `docs/tooling/formatter.md` says what the formatter does with blank lines — today it never states the canonical spacing at all — and the example runs.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `FormatterDocEditorExamplesTests` with the instrument literals bumped (11 → 12 examples, 21 → 22 requests, 4 → 5 changing) — the test reads from `## In the editor (LSP)` and `ReadExamples` DOES stop at the next `## ` heading (`FormatterDocEditorExamplesTests.cs:113–115`) [CORRECTED: resolved by reading the test] — so the executed `<!-- editor-example: document -->` block goes INSIDE "In the editor (LSP)" (a short paragraph pointing at the new "Vertical spacing" section), and "Vertical spacing" holds the prose rule; the reader is unchanged. The existing example at `formatter.md:277` has a blank line between `f`'s body and `def main()` but is the unparseable (6-space body) indent-only case, so it does not flip.
- Guard mutation: edit the doc's "after" block by one blank line → red; restore → green.
- Allowlist entries that drain: none.
- Execution evidence: the doc example's before/after produced by the real handler @ sha (the test), and by `sharpyc format` (pasted in the close-out).

#### Tasks

1. **`## Vertical spacing` section** — `docs/tooling/formatter.md` (after "Canonical spellings")
   - States: two blank lines before a top-level definition and after one when a statement that is not an import follows (today's rule as `UnparseModule` writes it — a definition followed by an `import` gets none; never written down), one after the import block, one between body items when either is a method, property or nested definition (in every body) [CORRECTED: the doc must state the rule the code has, not "around"]; **a blank line you wrote between two statements is kept — one, however many you wrote**; none at the start of a body, before a docstring, between a decorator and its definition, at the start or end of the file; a blank line between a comment and the statement it precedes is kept; blank lines inside brackets are not kept unless the construct is written verbatim; blank lines inside a triple-quoted string are string content (guarantee 1). One `<!-- editor-example: document -->` example (the issue's program widened with a body-start blank and a decorator blank), before/after.
   - Update the "In the editor" Format Selection sentence at `formatter.md:154` only if its claim changed (it did not: blank lines above a `def` are canonical insertions outside the selection).
   - Commit: `docs(format): vertical spacing — canonical def/member/import spacing stated, one user blank line between statements kept, with an executed Format Document example (P50, refs #2239)`

2. **Close-out on #2239** — comment with: the contract, the sweep census line @ sha, pins drained (0 rows), the fixture diff, the direction-probe counts, the FULL route sweep count, the doc example; the issue stays `class-tracker` for later cells (do not remove the label).

## Testing Strategy

- **Generator first (contract §11):** `FormatterBlankLineSweepTests` (Phase 1) over the whole single-file corpus × {identity, comment} twins with the differential oracle of Decision 6 and the pin-then-rows ratchet of Decision 7; the route axis is generated by the existing `FormattingRouteParitySweepTests` (FULL run in Phase 2); the hand matrix covers only the axis the generator cannot judge — the exact bytes per placement class (`Formatting/blank_line_placements`).
- **New fixtures:** `Formatting/blank_line_placements.spy/.formatted/.skip` (Phase 1 today's bytes → Phase 2 ruled bytes; the diff is the direction record). Negative/positive pairs: for every "dropped" placement the fixture also holds the "kept" neighbour (body start vs. after the first statement; after a decorator vs. after a comment that follows the decorator; EOF vs. before an EOF comment), so an absence (`0 blank lines`) is asserted next to a presence on the same input.
- **Edge cells outside the issue's repro list:** whitespace-only lines in a run; a run of 5; a run before `else:`; between `match` arms; after a docstring; after a block-bodied expression statement (#2227); between body-end comments at two depths; CRLF and lone-CR documents (route sweep twins); a bracketed construct with/without an inner comment; a triple-quoted string with an interior blank line. Change axis when all placement spellings agree: the run-length axis (Q⁺) and the route axis are the other two.
- **Outputs that discriminate:** every expected text has at least one line that today's formatter does not produce (a kept blank line) — an expectation that today's output also satisfies proves nothing; the direction probes make this explicit (0 differing fixed points, ≥ 1 insertion on every fixture with a `Between` run).
- **Spec/doc deliverable of equal weight:** `docs/tooling/formatter.md` "Vertical spacing" with the executed editor example (Phase 3) and `docs/design/gap-discovery-contracts.md` roster row (Phase 1).
- **Blast radius and when it runs:** the edit loop is `Sharpy.Compiler.Tests` filtered to `Formatter|Formatting|Pretty|Unparse` plus `Sharpy.Lsp.Tests` filtered to `Formatting|Formatter|LspFormattingDriver` (the LSP half is part of every pre-commit filter — the 2026-10-04 and 2026-10-12 lesson); the gate is the whole solution once per phase (`.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`, private log path), counts reported `passed/failed/skipped @ sha (measured)`, a red attributed only after a control run at the base commit. No generated artifacts to regenerate.

## Issues to Close

- #2239 — formatter: preserve one user blank line between statements (R-ET) — closed by Phase 2 Task 3 (fix + pins drained) with Phase 2 Task 4 and Phase 3 Task 1 landed; **close criterion:** `FormatterBlankLineSweepTests` 0 rows / 0 pins @ sha with the census showing every placement class observed; `FormattingRouteParitySweepTests` FULL 0 rows @ sha; `Formatting/blank_line_placements.formatted` showing every kept cell as one blank line and every dropped cell as none; the direction probes 0/0 against the base binary; the doc example executed by `FormatterDocEditorExamplesTests` @ sha. The issue keeps its `class-tracker` label after closing only if the owner says so — default: close, later cells go to #2169/#2274 or a new tracker filed by the lead.

## Class trackers this plan may post cells to

- #2239 — formatter blank-line preservation — cells INSIDE the contract are this plan's work (fix at the seam, add to the fixture and the sweep); no issue.
- #2169 — formatter: unparser spellings that change the program (SPY0912 refusals) — any cell where preserving a blank line makes the net refuse, or a spelling the unparser cannot write back.
- #2274 — permanent: indent-only routes and re-pair shapes no lexer fact can see — any on-type / indent-only cell on an unparseable document met while running the route sweep.
- #2296 / #2294 — LSP code-action meaning preservation / synthesized-declaration inference — any code-action cell the LSP driver work surfaces (P47's residues, queued behind P13b per R-GO).
- Ledger cell found at verification (outside this contract — canonical spacing, R-ET says unchanged): a top-level definition followed by an `import` statement gets NO canonical blank line (`UnparseVisitor.cs:59` `!isImport`; `def f():` / `    pass` / `import json` formats unchanged @ f58a75158). Lane tooling · reach program; the lead files or folds it onto the nearest formatter layout tracker. [ADDED: adequacy]
- A cell in no class above goes to the findings ledger; the lead files ONE class issue with a cell table, labelled `lane:*` + `reach:*`. No implementer runs `gh issue create`.

## Owner rulings after implementation (2026-10-13, in session)

Asked after the close-out with repros, lettered options and door/stacking analysis; answered by the owner:
1. **Decision 4 → black's shape (option C).** A comment separated from the top-level definition below it by a blank line is module-level content: the definition gets its canonical two blank lines directly above it (above its ATTACHED comments, if any), and the gap above the detached comment is the user's run (one) or none. Measured black 24.10 shapes, `|` = line break: `x = 1||# c||def` → `x = 1||# c|||def`; `x = 1|# c||def` → `x = 1|# c|||def`; `# header||def` → `# header|||def`; `def g ... ||# c||def f` → `... |||# c|||def f`; `x = 1||# c||@dec|def` → `x = 1||# c|||@dec|def`; class bodies unchanged (`x||# c||def m` keeps 1/1).
2. **Canonical gaps → two blank lines in both (option B).** A top-level definition followed by an `import` gets two (black: `def g ... |||import json`); a module docstring followed by a top-level definition gets two (black: `"""Doc."""|||def f`). Not ruled, NOT changed: docstring → non-definition statement (black writes one). [CORRECTED at /verify-implementation 2026-10-13: the former second item — "black's blank after a comment that follows a definition (`def g ... |||# c||x = 1`)" — is refuted: black 24.10.0 writes `def g ... |||# c|x = 1` for a source with no blank between `# c` and `x`, and so does `sharpyc` @ 4aca8ebfe; there is no divergence on that cell.]
3. **Format Selection refusals → accept (A) + a judged ceiling on the over-indented `wide` twin (D)**; widening the selection across unchanged blank lines (B) is a possible follow-up plan, not this one.

## Implementation Checklist

Lead-implemented (one tight seam in `Pretty/` + formatter tests; parallel agents would share every file). Base f58a75158; baseline gate 79405/0/4 @ f58a75158 (measured).

- [x] P1.1 placement fixture `Formatting/blank_line_placements` (today's bytes) — 88ad0a480
- [x] P1.2 `FormatterBlankLineSweepTests` + allowlist (2 pins, blankLost 1057 per twin) — 421275071
- [x] P1.3 roster row in `gap-discovery-contracts.md` — 0d5038ef5
- [x] P1 gate 79422/0/4 @ 0d5038ef5 (measured)
- [x] P2.1 cursor placement + `TriviaCursorPlacementTests` — 7b4f6c628
- [x] P2.2 `UnparseWriter.EnsureBlankLines` + tests — bee252226
- [x] P2.3 rule at the two writers + five canonical sites; pinned tests flipped; 3 `.formatted` regenerated; pins drained — aecd35f96
- [x] P2.4 LSP route cells + direction probes (0 moved / 0 non-insertion hunks over 3879 files) + route sweep FULL 16485/0 — 656230889
- [x] P3.1 `formatter.md` Vertical spacing + executed example — cd8490c29
- [x] Final gate 79471/0/4 @ cd8490c29 (measured; P2+P3 share it: P3 is docs + one test literal); standalone verification-expert NOT REFUTED (~85 probes, corpus re-run, independent oracle 48510 pairs / 0 mismatches); `dotnet format whitespace` clean
- [x] P3.2 close-out on #2239 (issuecomment-6138142514; issue stays OPEN for /verify-implementation; 3 out-of-contract cells posted there)
- [x] Owner rulings 1–2 (detached comment → black; def → import and docstring → def get two) — 73a31308c (code, sweep oracle restates the detached gaps, fixture `canonical_definition_spacing` byte-identical to black), 17aba6fb6 (doc)
- [x] Owner ruling 3 (accept + judged `wide` ceilings) — 4aca8ebfe
- [x] Final gate 79487/0/4 @ 4aca8ebfe (measured); rulings comment issuecomment-6147054967 on #2239

Implementation notes (deviations from the plan text, decided at implementation, each measured):
- Decision 1 `EndOfFile` refined: the carrier is `Eof` AND no comment follows the run in its leading trivia — a run before an end-of-file comment is also carried by `Eof` and the plan lists it as kept (`Between`).
- Decision 6 alignment: Q's items map onto F's by a longest common subsequence of item keys (first token / comment text), not equal counts — the formatter writes a `...` body for a bodyless definition (94 `instrument` cells under equal counts, 0 under the alignment). F and F₀ must still have identical keys.
- Decision 7: one `[Fact]` per twin aggregating failing stems (a pin is an aggregate), not a theory per stem.
- Decision 2: `EnsureBlankLines` does NOT clear `_lineEndedByBlockExpression` — clearing it would let the pending host terminator write a second line (M4b red). M-4 (append through `WriteLine()`) is an equivalent mutant (green 159/159): the flag is only set when a host terminator follows.
- `TriviaCursor.FromTokens` sorts trivia stably (a run and the comment that ends it share a position).
- Pinned test outside the plan's list: `FormatterServiceTests` cell 9 relied on Format deleting D16's interior blank; moved to D16b (body-start blank); D16 line-1 Format Selection is now REFUSED (worked → refused): findings-ledger cell 2.
- FULL route sweep at 0d5038ef5 (Phase 1 head) is red: range-line identity 44/1089 (4.04%) > 4% ceiling; control @ f58a75158 33/1078 (3.06%) green, so the +11 are the new fixture under the old formatter; resolved at aecd35f96 (34/1081, 3.15%). FULL mode is not in the gate.
- Findings ledger (`<scratchpad>/findings-ledger.md`): (1) def → import canonical 0 (needs a ruling); (2) Format Selection on a partial re-indent split by a kept blank line refused (wide twin range-line refusedWork 37.72% → 53.23%). Both posted as cells on #2239.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-12 @ f58a75158
**Plan file:** .claude/plans/plan-be3b1f.md

Mechanical check (`build_tools/check_plan_refs.py --issues`): 9 paths ok, 3 "missing" are the files Phase 1–2 create; 15 `file:line` ok, 5 ambiguous resolve under `Lexer/` and `Pretty/`; 7 issues, #2227 CLOSED (cited as history only); #2239 OPEN with `lane:tooling` · `reach:program` · `class-tracker`, body carries R-ET, no comments; charter §3.3 and the rulings table (R-ET, R-GC, R-GF, R-GO) say what the plan says.

### Corrections Made
- **Decision 6 (oracle alignment)** — before: items = code tokens, "the meaning net guarantees the same sequence in Q, F, F₀"; after: items = logical lines (Newline-delimited) + own-line comments, with an `instrument` check that the three item counts agree. Refuted by probe: `sharpyc format` on `except KeyError, IndexError:` writes `except (KeyError, IndexError):` — 52 → 54 tokens @ f58a75158; token-indexed alignment would mis-attribute every run after a respelling. Logical-line count is stable (semicolons refused SPY0103; backslash continuation 2 Newline → 2).
- **Decision 4 / Context (black claim)** — before: "black does the same"; after: black 24.10.0 writes `# c` / 2 blank / `def g()` (measured). The plan's 1 is recorded as an un-ruled Design Decision with the one-arm alternative named.
- **Defect Class contract** — before: "2 around top-level definitions"; after: the rule `UnparseModule` has — 2 before a def, 2 after a def only when a NON-IMPORT follows; `def` → `import` gets none today (measured). Phase 3's doc sentence corrected to the same.
- **Matrix** — added: class body between FIELDS (canonical 0 ⇒ 1), nested def in a function body (canonical 1), between match-EXPRESSION arms (kept) and before the first arm (dropped), between enum members, between union cases, last case → first method. All are `WriteAnchored` callers (`Statements.cs:426`, `:697`, `Expressions.cs:448`) and all drop runs today (measured @ f58a75158).
- **Decision 7 buckets** — `instrument` gains the item-count check; `refused` covers Q₀/Q⁺ refusals.
- **Phase 2 Task 3** — a `blank_between_arms` position added to `FormatterMatchExpressionLayoutMatrixTests`; the `RemoveAt` is at `:193`.
- **Phase 3** — `ReadExamples` DOES stop at the next `## ` heading (`FormatterDocEditorExamplesTests.cs:113–115`); the conditional is resolved: executed example inside "In the editor (LSP)", prose in "Vertical spacing".
- Path fixes: `Formatting/FormatterService.cs:175` (SourceTokens); `UnparseWriterTests.cs` exists.
- Scope gained a verify-cost / prober-budget line; the def → import canonical cell is recorded as a ledger cell.

### Adequacy
- **Contract:** named — one sentence, placement × run length × route × line break; canonical rule corrected to the code's.
- **Matrix:** larger than repro lists — the issue names 9 positions; the plan now names 27 kept/dropped placement cells plus run length, route and line-break axes. Cells the verifier added: see Corrections. Checked against every `WriteAnchored` caller (`UnparseVisitor.cs:181` is the single `TakeBefore → WriteOwnLine` funnel; callers in `Statements.cs` 423–426, 697, `Expressions.cs` 448, plus statements/decorators/clause headers) and the two `BlankLineCount` consumers (`UnparseVisitor.cs:299`, `:347` — grep confirms no third).
- **Cure:** seam — placement computed once in `TriviaCursor`, consumed at two writers, five canonical loops routed through `EnsureBlankLines`; meta-class "position/route missing a shared check".
- **Harness:** `FormatterBlankLineSweepTests` built in Phase 1 (red-pinned, 2 pins citing #2239), drained in Phase 2; route axis by the existing `FormattingRouteParitySweepTests` (`cli`/`full`/`range-whole` judged against `Format(D).FormattedText` at `:1002`/`:378`). Pin-with-digest precedent: `code-action-shape-allowlist.txt`.
- **Layer:** every decision rung-justified (compiler tooling; no Core/Stdlib or name-keyed rule). Lexer-side and writer-side alternatives rejected with reasons that match the code (flush precedes `MeasureIndentation` at `Lexer.Indentation.cs:160–166`).

### Scope & convergence
- **Group:** one (P50) · **Alone:** yes — required yes (formatted output changes; `.formatted` fixtures and LSP applied text flip) · **Gates:** 1 per phase
- **Generator:** built first in Phase 1; hand matrix limited to the per-placement bytes fixture — the axis a restated predicate cannot judge, stated.
- **Trackers named:** #2239, #2169, #2274, #2296/#2294 · **Fidelity rows:** none — `python-fidelity-scope.md` lists no formatter surface (grep: only `format()`/format specs); black is reference only. No implementer runs `gh issue create`.

### Falsifiability
- `FormatterBlankLineSweepTests` (Phase 1): mutation named (M-a oracle `max(1,g₀)` → `g₀` stales the pin; M-b pin +1; M-c `WithoutRuns` no-op → instrument); exemption not subject (the pin is a count+digest, not a predicate over the failing cells); positive control yes (`Formatting/blank_lines` format-runs control, every placement class anchored to the fixture by name); by-direction probe specified (fixed points stay fixed; preservation only adds; no refusal direction).
- `Formatting/blank_line_placements` fixture: mutation = any of M-1/M-3 (byte-red); discriminating — Phase 2 bytes contain kept blank lines today's formatter never writes; dropped cells sit beside kept neighbours (absence next to presence).
- `TriviaCursorPlacementTests`: mutation named (flip the `Indent` test → BodyStart rows red); positive control = the Between rows on the same inputs.
- `UnparseWriterTests` `EnsureBlankLines`: mutation M-4 (append through the absorbing `WriteLine()` → `then_blank` red). Verified by reading: `EndLineByBlockExpression` (`Expressions.cs:473`) runs before the host's terminator, and every `Ensure` site runs after it, so the flag-clear is defensive, not load-bearing — the `then_blank` cell still discriminates M-4.
- LSP route cells (Phase 2 Task 4): `Selects` confirmed — a deletion hunk `[a,b)` is selected iff it overlaps the selection (`FormatSelection.cs:38–44`), so the `range-statement` "no edit" cell is real; mutation = revert Phase 2 Task 3 → Format Document cell red.
- Doc example (Phase 3): mutation named (one blank line in the after block → red); the example must contain a kept user blank line (discriminating), which the plan's widened issue program does.

### Warnings
- **Decision 4 is un-ruled and differs from black.** A blank between a module-level comment and the def it precedes: plan writes 1, black writes 2. Not blocking (R-ET does not reach it; the plan records it as a decision), but say so in the close-out so the owner can rule.
- **Canonical def → import spacing is 0 today** and the contract/doc now say so. If the owner wants 2 there, it is a one-arm change at `UnparseVisitor.cs:59` on this plan's seam — but R-ET says canonical spacing is unchanged, so it stays a ledger cell unless ruled.
- **Rule 1 (never modify expected values to make tests pass):** the `.formatted` regeneration and the renamed LSP pins are expectation changes forced by the ruling, not by a failing test; the plan's "read the three diffs as findings" discipline is what keeps this on the right side — every changed line must be a `Between` run → 1 or a dropped placement → 0.
- **`FormatRunsControl` (`Formatting/blank_lines`) has no user blank lines** (4 lines, canonical only) — it controls canonical runs, not preservation; the census positive controls for preservation come from `blank_line_placements`, as the plan says. Keep both.
- **Whitespace-only trailing line without a newline** is not counted by the lexer (`_position < _source.Length` at `Lexer.Indentation.cs:135`): the fixture's EOF cells should include the `\n`-terminated form, which is the one that records a run.

### Missing Steps Added
- Scope: verify-cost and prober-budget line.
- Phase 2 Task 3: `blank_between_arms` position in the match-expression layout matrix.
- Class trackers: the def → import canonical cell for the ledger.

### Unchecked Claims
- `FormatterTwins.CommentInjected` wall-time on the comment twin (plan's 120 s sampling threshold) — measured only at implementation.
- The exact pin counts at Phase 1 — measured when the sweep first runs.
- CRLF / lone-CR behaviour of `EnsureBlankLines` — specified by `_lineEnding`; the route sweep's twins will measure it.

## Verification (/verify-implementation, 2026-10-13)

**Result:** VERIFIED WITH FIXES · scope f58a75158..4aca8ebfe (11 commits) + remediation 4aca8ebfe..b4e6e1633 (4 commits, `dev`, not pushed) · gate 79487/0/4 @ 4aca8ebfe → 79498/0/4 @ b4e6e1633 (measured) · format clean · staleness gates clean · #2239 CLOSEABLE, left open as class tracker (one un-ruled cell posted: docstring → detached comment → def, 0 above the comment vs black 1).

- **Regression (fixed bcee5d151):** import block → detached comment → top-level def lost the import gap (`import os|# c|||def`; base `|||# c|def`; black `||# c|||def`) — ruling-1 arm wrote the detached comment before the `lastWasImport` gap; the sweep's restated `DetachedCommentGaps` had the same omission. Found by Agents 2, 3, 4 independently; no corpus fixture had the shape. Cured at the seam; oracle corrected; cells pinned against black in `canonical_definition_spacing`; mutations broken → red 3 / 2 / 2, restored → green 32.
- **Deliverables fixed:** `blank_line_placements_edges` (3 cells one file cannot hold — d56b12476); roster row + decorator/comment doc sentence (f0d82493e); `refused` census anchored at 0 (bcee5d151); writer test renamed, M-4 is an equivalent mutant (b4e6e1633).
- **Not refuted:** seam verdicts (2 writers, 6 Ensure sites, 12 WriteAnchored callers, routes by construction); 13/14 implementer mutations red (M-4 equivalent); direction probe 4129 files (4 moved + 10 hunks, all rulings 1–2); remediation range 0 corpus outputs changed; FULL route sweep 16490/0/0, `wide` ceilings hold (range-block 3 cells of headroom); 200 CLI + 40 LSP prober cells, 8 FAIL all C1.
- **Residue:** three remediation commits' Claude-Session trailer dropped one char (not amended); the "19 shapes" black claim in the rulings comment is unverifiable (no list); the lead's `_edges` fixture is a second hand matrix (the axis the generator cannot judge, same as the first).

## Owner ruling 4 (2026-10-13, at /verify-implementation)

Shown the docstring → detached comment cell with its siblings (docstring → comment / import / statement all write 0; black writes 1 on each), lettered options A (black's shape for the whole class) / B (comment cell only) / C (leave): **A, landed now as a follow-up slice.** Rule: one blank line after the module docstring before anything that is not a definition; definitions keep their two. One arm at the `UnparseModule` seam (like the after-import gap), the sweep oracle restates it for the detached-comment case, bytes pinned against black in `Formatting/module_docstring_then_{comment,import,statement}`, doc + roster row updated. Supersedes ruling 2's "docstring → non-definition statement NOT changed".
- Landed 03c8063f8 (`dev`, not pushed): arm + oracle + 3 fixtures (byte-identical to black, idempotent) + doc + roster; mutations writer arm → red 5 / restored 35, oracle arm → red 2 / restored 35; corpus direction probe 0 outputs changed (the corpus has 4 docstring-opening files, all → def); edit loop Compiler 17458/0, Lsp 20540/0; whole-solution gate recorded below when measured.
- Gate @ 03c8063f8: 79540 run, 1 red — `CodeActionShapeSweepTests.Identity` extract-method diagnostics pin 829 → 830 (#2294, open): `module_docstring_then_statement` joined the sampled class; green at b4e6e1633, so the delta is the new fixture. FULL runs (all four twins) re-measured the pins: this round's six fixtures moved four FULL keys on every twin (+2/+2/+1/+2, #2294/#2296) — stale since b4e6e1633 because only a FULL run measures them, and CI does not run FULL. Pins updated from the sweep's own `*-actual-pins.txt` → 7f06491a0; whole-solution gate at 7f06491a0 recorded below.
- **FINAL 7f06491a0:** gate 79531/0/4 (measured); FULL code-action sweep 48/0; format + staleness clean; #2239 CLOSEABLE (issuecomment posted). Range f58a75158..7f06491a0 = 17 commits, 39 files, +2029/−97.
