<!-- Verified by /verify-plan on 2026-10-10 @ 53ee2d993 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: existing (extended first) -->

# P22i — a comment-only line is left verbatim by every indent-only route (#2290)

## Context

The indent-only routes of the LSP — format on type, and the Format Selection / Format Document **fallbacks** that run
only when the document does not parse — may edit only text the lexer read as the user wrote it (P22g's contract,
unchanged since). A comment-only line is **trivia** to the lexer: `Lexer.Indentation.cs:94-98` skips it entirely at
line start (no INDENT/DEDENT, no SPY0012/13/14, a `Comment` trivia). The indent map therefore records no level for it,
and both indent-only builders read a missing level as **level 0**: every indented comment-only line of an unparseable
document is written at column 0 (`FormattingFallback.cs:59`, `RangeFormattingHandler.cs:119`). The check every
indent-only candidate passes through (`FormattingFallback.IndentOnlyPreserved`) has no clause for comment lines, so it
passes the edit; the standing harness (`FormattingRouteParitySweepTests`) is blind to the axis by construction (its
`content` oracle trims leading whitespace before comparing, `depth`/`moved` judge token-bearing lines only).

Pre-existing since de15c8d61 (2026-05-14). P22h (plan-f92797, R-FU) widened the reach: the recovery line became a
repair target, so Format Document / range-whole now run on documents base refused (an unterminated string, `$`, a
backtick name, an unterminated f-string above a block) and there the ONLY edit is the comment dedent — the ordinary
editing state (a string being typed) now moves the user's comments.

- **#2290** (`lane:tooling` · `reach:program` · `class-tracker`) — the filer's four hand documents, the 24
  seed-2280 fuzz cells newly edited at 040c79ec2 (all comment-line-only), and 111 of base's 141 edited cells.
- **Owner rulings (authoritative, do not re-ask):** issue #2290 comments of 2026-10-10 —
  **R-GD (a)**: a comment-only line is left verbatim — byte-identical leading whitespace — by every indent-only route,
  in BOTH builders (`ReindentDocument`, `ComputeIndentOnlyRangeEdits`) and as a clause of `IndentOnlyPreserved`.
  Rejected: (b) column-claim like the unparser's trivia rule (stacks later under a direction record); (c) attach to the
  following token-bearing line's level (unknowable in an unparseable document). **R-GE (a)**: its own small group in
  P22h's slot directly before P47, generator extended FIRST, verify round. **R-GF**: the P22 family is CAPPED at P22i —
  a further residue is a tracker cell whose group queues behind P34 and P13b, unless it is a regression by direction
  against the last pushed sha (remediated in-round).
- Charter: `.claude/plans/remediation-round-2026-09-20-batching.md` §3.1c (P22i), §4 (no carrier; no snapshot moves;
  blast radius Lsp.Tests only; `docs/tooling/formatter.md` "In the editor" gains the rule).

## Current State (measured @ 53ee2d993 = dev HEAD = mainline; the built `Sharpy.Lsp` at `src/Sharpy.Lsp/bin/Debug/net10.0/` is dated 21:13, after the commit's 21:11:47 — the gate build)

- **The level rule has no arm for trivia lines.** `IndentationService.BuildIndentMap` (`src/Sharpy.Lsp/IndentationService.cs:171-176`):
  a line with no `logicalStart` entry whose trimmed text is empty, starts with `#`, or is a literal line `continue`s
  BEFORE `lineIndent[line] = widths.Count - 1` (`:225`) — continuation lines (inside brackets, after `\`) DO get the
  current block's level there. So a comment-only line is the ONLY non-blank, non-literal line without a `LineIndent`
  entry, and the builders' `TryGetValue(n, out l) ? l : 0` arm fires exactly for comment-only lines (and blank lines,
  which the builders blank anyway). Three consumers of the map read a level: `FormattingFallback.ReindentDocument`
  (`FormattingFallback.cs:44-61`), `SharpyRangeFormattingHandler.ComputeIndentOnlyRangeEdits`
  (`RangeFormattingHandler.cs:79-132`), and `OnTypeFormattingHandler` (`OnTypeFormattingHandler.cs:64-76`) — the last
  already refuses a line that is not a logical-line start, so on-type never edits a comment line (a control).
- **The check** (`FormattingFallback.IndentOnlyPreserved`, `:112-189`): clauses 1–7 (+6b, 6c) — line count and breaks,
  trimmed content (a whitespace-only line may become empty), literal lines byte-identical, logical-start levels,
  indentation diagnostics, block depth and landing (6, 6b), unplaced lines (6c), frozen lines (7). None judges a
  comment-only line; clause 2 compares content after `TrimStart(' ', '\t')`, so a dedented comment passes.
- **The funnel** (`FormattingEdits.CheckedIndentOnly`, `FormattingEdits.cs:79-93`): every indent-only candidate — on-type,
  full fallback, range fallback — is applied and judged by `IndentOnlyPreserved` when the source does not parse; a refusal
  returns NO edits for the whole candidate (so a check clause alone, without the builders, would turn every repair of a
  document holding an indented comment into "no edits" — a direction regression, see Adversarial Review).
- **The sweep** (`src/Sharpy.Lsp.Tests/Conformance/FormattingRouteParitySweepTests.cs`): corpus = every single-file
  fixture under `Integration/TestFixtures` that PARSES — **3,291 stems @ P22h's FULL census (`single-file=3445
  excluded-unparseable=154`, `.claude/tmp/p22h-impl/census-p1c-green.txt`); re-measure** [CORRECTED: the plan said
  "3,878 `.spy` minus 177 `main.spy` ≈ 3,700" — the 177 multi-file directories hold more `.spy` files than their
  `main.spy`, and `BuildCorpus` excludes the 154 unparseable single-file fixtures] × five twins
  `identity, comment, wide, crlf, cr` × states S1–S7; unparseable states run `full, range-whole, range-line, ontype`
  (`RoutesByState`, `:166-183`). **Mode:** the five per-twin theories (`:1339-1357`) take `CorpusNames` = every corpus
  stem in EVERY mode; only `range-line`/`ontype` on S1/S2 are sampled within a stem (`IsSampledCell`, `:193`, stride 8).
  So every S3–S7 cell runs at the gate, and the pin block (`:2578-2579`, `everyTwinRan.Count == 0`) ASSERTS
  `KnownLimitPin`/`KnownLimitCellsPin`/`S7ParityExemptPin` in the regular suite too, not only under
  `SHARPY_ROUTE_SWEEP_FULL=1` [CORRECTED: the plan read the pins as FULL-only; FULL changes only the S1/S2 line-route
  sample, which no bucket this plan touches judges]. `UnparseableOracles` (`:1061-1111`): `content` (`:1081` trims leading whitespace),
  `literal` (Q's literal lines byte-identical), `moved` (recovery-header bodies), else `depth` (Q's logical starts). The
  **`comment` twin** (`FormatterTwins.CommentInjected`, `src/Sharpy.TestInfrastructure/Formatting/FormatterTwins.cs:320`)
  injects `# cN` at ten anchor kinds — own-line kinds: `LineOwnLine` (at the statement's indent, module statements and
  the first statement included), `BlockEnd` (at a closed block's indent, right after its last line), `BeforeClauseKeyword`
  (at the clause keyword's indent), `CloseBracket` (column 0 before a closer), `EndOfFile` (column 0) — so the generator
  ALREADY enumerates the comment-position axis over every corpus stem and every unparseable state; no bucket judges it.
  Allowlist `formatting-route-parity-allowlist.txt`: 0 rows (`rows.Count.Should().Be(0)` at `:2617`); pins:
  `KnownLimitPin`/`KnownLimitCellsPin` (S6k, S6ok; #2274), `S7ParityExemptPin` (L7), refusal ceilings (S1/S2 only).
  `Buckets` (`:277`) is the loader's bucket universe (`:1762`) and the census's (`:2491`).
- **Hand probe @ 53ee2d993** — `.claude/tmp/p22i-plan/{cases.json,head-53ee2d993.json,probe-table.md}` (driver
  `lspfmt.py` over stdio, 15 documents × {full, range-whole, range-line on the comment, on-type on the comment} = 60
  cells). Widths before→after, 0-based lines:

  | document | full / range-whole / range-line(comment) | on-type(comment) |
  |---|---|---|
  | C1 `def f():\n    y = "abc\n\ndef main():\n    # c1\n    b = 1\n` | `# c1` 4→0, nothing else | no edit |
  | C2 same with `y = $`; C13 `` y = `ab ``; C14 `y = f"{x` | `# c1` 4→0, nothing else | no edit |
  | C3 misindent control `def f():\n      y = 1\n\ndef main():\n    # c1\n    b = 1\n` | `y` 6→4 (repair) AND `# c1` 4→0 | no edit |
  | C4 clean twin (`y = 1`; parses) | the REAL formatter: `# c1` kept at 4, a blank line added before `def main` (full, range-whole); range-line on the comment: no edit | no edit |
  | C5 `def main():\n        # deep\n    b = 1\n` (comment deeper than its block; `y = $` above) | `# deep` 8→0 | no edit |
  | C6 `# c0` at column 0 inside main's body (`y = $` above) | no edit (already level 0) | no edit |
  | C7 `    # end` as the last line of main's body before `def g()` | 4→0 | no edit |
  | C8 `# between` at module level between defs; C9 `# top` above the first statement | no edit | no edit |
  | C10 `    xs = [1,\n          # inner\n          2]` inside a CLOSED bracket (`y = $` above) | `# inner` 10→0 AND `2]` 10→4 (the continuation takes the block's level) | no edit |
  | C11 2-space body `def main():\n  # c\n  x = 1\n  print(x)\n` | `# c` 2→0; `x`, `print` 2→4 (repair) | no edit |
  | C12 8-space S3 cut `def main():\n        # c\n        x = 1\n        if x:\n` | `# c` 8→0; code 8→4 (repair) | no edit |
  | C15 `\t# tab` (a tab-indented comment; `y = $` above) | 1→0 | no edit |
  | **C16** `def main():\n    if foo($):\n        # c\n        y = 1\n` (a comment inside the PUTATIVE BODY below a dropped header, L8) [ADDED: adequacy — verifier probe @ 53ee2d993, `lspfmt.py` over the 21:13 binary] | full / range-whole: **no edit** (clause 6c refuses the candidate because `y` 8→4 moves an unplaced line); **range-line(comment): `# c` 8→0** — a wrong edit | no edit |
  | **C16b** same with `# c` and `y` at 10 (the body deeper than a level) [ADDED: adequacy] | as C16: full / range-whole no edit; range-line(comment) 10→0 | no edit |
  | **C17** `def main():\n    xs = [1,\n        # c\n        2\n` (a comment on the FROZEN tail below an open bracket) [ADDED: control, verifier probe] | no edit on every route (clause 7 and the builders' frozen check come before the level) | no edit |

  Every indented comment-only line of an unparseable document moves to column 0 on full, range-whole and range-line;
  code repairs happen beside it; on-type and column-0 comments are already unchanged. **C16 is why clause 6c is not a
  guard for comment lines:** the width loop's `#` arm `continue`s (`IndentationService.cs:174`) BEFORE
  `unplaced.Add(line)` (`:227`), so a comment line in a putative body is in neither `LineIndent` nor `UnplacedLines` —
  on full the body's own repair gets the whole candidate refused, on range-line nothing stands between the comment and
  column 0. After Design 1 it is in `CommentLines` and clause 8 is its guard.
- **Tests @ 53ee2d993 (measured, gate log `.claude/tmp/p22h-vi/gate-final-53ee2d993.log`):** whole gate 79124/0/4; the
  Lsp.Tests dll 442 total / 441 passed / 1 skipped (verifier re-read the log: `Total tests: 442 / Passed: 441 / Skipped: 1`
  at log line 10671). FULL-mode sweep cost ≤ 2 × 248 s per run (plan-f92797's measurement, not re-measured by the
  verifier; `SHARPY_ROUTE_SWEEP_FULL=1`); the regular-mode sweep class has a 90 s budget on the 8-thread runner (`:122`).
  **Every count this plan reads — the `comment` pin, `KnownLimitPin`, `S7ParityExemptPin`, the S3 `applied-differs`
  floor — is a tally over S3–S7 cells, which run on every stem in BOTH modes, so a regular-mode run of the sweep class
  measures them identically; FULL is needed only for the S1/S2 refusal ceilings, which this plan does not touch**
  [CORRECTED: the plan budgeted three FULL runs for measurements a regular run gives].
- **Docs:** `docs/tooling/formatter.md` guarantee 2 (`:35`) states the REAL formatter's rule ("a comment-only line …
  is written at the indentation of the code it belongs to"); the "In the editor" fallback bullet (`:124`) says the pass
  "re-indents lines to four-space levels and changes nothing else" — silent about comments.
  `src/Sharpy.Lsp.Tests/Conformance/FormatterDocEditorExamplesTests.cs` executes the editor examples (`document`,
  `selection A-B`, `on-type N`, `; unchanged`) and pins their counts (`examples 10, requests 20, changing 3` at
  `:49-51`) [CORRECTED: path under `Conformance/`, literals at `:49-51` not `:46-48`].
- **Direction-record tooling from P22h:** `.claude/tmp/p22h-impl/lspfmt.py` (the `SHARPY_LSP` env override at `:7`);
  the seed-2280 fuzz set `.claude/tmp/p22h-vi/regression/fuzz2/cases.json` (200 `full` cases, fields `name, text, kind,
  orig`); the four hand documents `.claude/tmp/p22h-vi/lead/f1.json`.

## Scope

- **Group:** P22i — comment-only lines in the indent-only fallback (charter §3.1c; P22h residue). Chores riding along: none.
- **Alone:** no — LSP edits only; by design NO snapshot moves (charter §4: "P22g, P22h and P22i (LSP edits and lexer
  facts only)"); no printed output or emitted layout changes.
- **Gates:** 1 (the whole solution at the last commit; filtered runs are the edit loop).
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`, one line per cell);
  the lead files by class (contract §10); trackers named at the end of this plan. **R-GF:** a residue the verify round
  finds is a cell on #2290 (or the permanent #2274) and its group queues behind P34 and P13b — only a regression by
  direction against 53ee2d993 is remediated in-round.
- **Override:** none.
- **Verify cost / prober budget:** regular-mode runs of the sweep class (≤ 90 s budget) measure the `comment` pin in
  Phase 1 and its drain in Phase 2 (every pinned tally is over S3–S7 cells, which run on every stem in every mode —
  Current State); ONE FULL-mode run (≤ 2 × 248 s) at the gate for the close criterion's "FULL sweep 0 rows"
  [CORRECTED: was three FULL runs]; the prober's budget is the matrix below plus two axis changes (the verifier
  consumed the plan's axis change (a) into the matrix as C16 — see Testing Strategy for the replacement).

## Defect Class

- **Violated contract (P22g's, unchanged):** the indent-only routes edit only text the lexer read as the user wrote it;
  a comment-only line is trivia to the lexer (`Lexer.Indentation.cs:94-98`), so its leading whitespace is never
  rewritten by an indent-only route — uniform across route × comment position × document state × twin.
- **Meta-class:** "position/route missing a shared check" — the map's level rule has no arm for a trivia line and
  three consumers each read the missing level their own way (two default to 0, one refuses); silent wrong behaviour
  (an edit nobody asked for, on a layout the lexer never read). **Standard cure:** ONE shared fact on the map read by
  both builders AND a clause of the check (so a builder that forgets is refused), plus the sweep bucket so a new
  consumer or a new comment-placement mechanism cannot skip it.
- **Known member cells:** #2290's four hand documents (C1, C2, C3, C4 above) × {full, range-whole}; the 24 seed-2280
  fuzz cells (fixture · error line · comment lines moved, `.claude/tmp/p22h-vi/regression-report.md` §F1); range-line on
  the comment "at BOTH builds".
- **Sibling cells this plan must also cover (the matrix; the issue's list is a symptom report):**
  **comment position** {own-line inside a body (`LineOwnLine`); last line of a body before a dedent (`BlockEnd`, C7);
  before a clause keyword (`BeforeClauseKeyword`); inside a CLOSED bracket at the continuation's width (C10) and at
  column 0 (`CloseBracket`); module level between definitions (C8, control); above the first statement (C9, control);
  end of file (`EndOfFile`, control); deeper than its block (C5, the misindent control of the comment itself); column 0
  inside a body (C6, control); tab-indented (C15); under a 2-space body (C11) and an 8-space body (C12 / the `wide` twin);
  **inside the putative body below a dropped header (C16, C16b — a fix on range-line, a 6c-refusal control on full)
  [ADDED: adequacy]; on the frozen tail below an open bracket (C17, control)**}
  × **route** {full, range-whole, range-line on the comment, on-type on the comment (control)} × **document state**
  {S3 cut after a block opener; S4 cut inside a literal; S5 stray triple quote; the S6 family; S7 injections — the
  issue's four typing-state errors (`"abc`, `$`, a backtick name, `f"{x`) are S7 `recovery`/`bracket-*`/`-string`
  shapes; the parseable control (C4: the real formatter keeps the comment at the code's indentation)} × **twin**
  {identity, comment, wide, crlf, cr}. The generator covers position × state × twin × route for every anchor kind it
  injects; the **hand axis** (rows in `FormattingFallbackTests`) is what `CommentInjected` cannot produce: a comment
  DEEPER than its block (C5), a column-0 comment INSIDE a body (C6), a tab-indented comment (C15), a comment inside a
  closed bracket at the continuation's width (C10), the 2-space body (C11), **a comment inside the putative body below a
  dropped header (C16, C16b: the generator's `recovery-header-*` shapes inject no comment INSIDE the body — the
  `LineOwnLine` anchor sits at a statement, and the body's statement is the recovery line itself)** — plus the issue's
  four documents and the parseable control. (The charter's route list names `range-body`; the sweep has no such route
  on S3–S7 — `RangeStatement`/`RangeBlock` run on S1/S2 only — so the route axis here follows `RoutesByState`.)
- **Lane / reach:** tooling · program (an unterminated string while typing is the ordinary editing state; no statement
  changes block). **Fidelity-scope row:** N/A — an editor surface, not a Python-comparison surface
  (`python-fidelity-scope.md` does not list it; nothing to promote).
- **Standing harness that should have caught it:** `FormattingRouteParitySweepTests` — blind by construction (`content`
  trims leading whitespace; `depth`/`moved` judge logical lines). **Phase 1 adds the `comment` bucket.**
- **Generator: EXISTS — extended FIRST.** The sweep's `comment` twin already enumerates the comment-position axis over
  3,700 stems × 5 twins × S3–S7 × four routes; Phase 1 adds the bucket that judges it (a comment-only line of D that
  is not string content in Q is byte-identical in T) with an in-file positive control and the misindent-control twin
  (C3: base and head both dedent it today). The ratchet while red is a **pinned count** (L1 precedent, see Design 5),
  deleted when Phase 2 drains it to zero; the bucket then rides the ordinary row ratchet with 0 rows.
- **Owner rulings:** R-GD (a), R-GE (a), R-GF — https://github.com/antonsynd/sharpy/issues/2290 (the two batching
  comments of 2026-10-10). Authoritative; the plan re-measures the seam only.

## Adversarial Review (pre-mortem)

- **Alternative root cause:** "the map should give comment lines a level" — deleting the `#` arm of the `continue` at
  `IndentationService.cs:174` would give a comment line the current block's level (`widths.Count - 1`), exactly as
  continuation lines get. That is option (c)'s cousin (the enclosing block at that point: a `BlockEnd` comment would take
  the INNER block's level before the dedent, a comment under a dropped header the header's guessed block) and R-GD
  rejected it; the discriminating probe is C5 (`        # deep` under a 4-space block): (a) keeps 8, the map-level
  alternative writes 4, (b) writes 4, today writes 0. The plan does not take it; `IndentMap.CommentLines` is derived at
  that very arm so the two readings cannot drift.
- **Alternative layer:** a lexer fact ("comment-only lines") instead of a map set — unnecessary: the lexer already
  classifies the line as trivia at `Lexer.Indentation.cs:94` (first non-space/tab character `#`), and the map's text
  predicate is the same test; a second lexer fact would be a mirror to keep in step. No language rung applies (no
  Core/Stdlib behaviour; LSP-internal, charter §4 "P22i adds no carrier").
- **How the fix could be inert:** (1) **three consumers** — a skip in `ReindentDocument` alone leaves `range-line`/
  `range-whole` moving comments (C1 range-line today); both builders change, and the clause refuses a builder that
  forgets. (2) **Clause-only "fix"** — adding clause 8 without the builder skips makes every candidate that touches an
  indented comment fail the check → NO edits for the whole document → C3's `y` repair, C11's and C12's code repairs are
  LOST (repair → no edit: a regression by direction). The `P22i` repair rows assert the applied text (`y` at 4 AND the
  comment kept), so a clause-only tree is red; the Phase 2 commit body records that reading. (3) **Predicate
  duplication** — a builder spelling its own `trimmed[0] == '#'` while the map spells another: both builders and the
  clause read `IndentMap.CommentLines`; `IsCommentOnlyLine` is the single predicate and the map's own `#` arm calls it.
  (4) **The bucket judging Q instead of D** — a `#` line that is string content in Q (S6k's comment re-pair) is
  `literal`'s cell, not `comment`'s; the bucket reads D's text for the predicate and Q's literal lines for the exclusion,
  and the in-file positive control includes a literal `#` line that must NOT be flagged `comment`.
- **Before/after by direction** (every cell classified against 53ee2d993 by `lspfmt.py` over base-lsp @ 53ee2d993 and
  HEAD; the table is the Phase 3 deliverable, expectations here):
  (1) C1, C2, C13, C14, C7 × {full, range-whole, range-line}: wrong edit (comment → column 0) → **no edit** (fix).
  (2) C3 full/range-whole: `y` 6→4 + comment 4→0 → `y` 6→4, comment kept (fix; the kept repair is the control against
  the clause-only regression); C3 range-line(comment): wrong edit → no edit.
  (3) C5: 8→0 → kept at 8 (fix by R-GD (a); (b) would write 4 — recorded so the verify round does not re-find it).
  (4) C10: `# inner` 10→0 + `2]` 10→4 → `# inner` kept at 10, `2]` still 10→4 (fix + control: continuation lines keep
  their level rule).
  (5) C11: `# c` 2→0 + code 2→4 → `# c` STAYS at 2 while the code goes to 4; C12: `# c` stays at 8 while the code goes to
  4 — the honest consequence of (a): a repaired body may leave its comments at the unrepaired width; (b) stacks on (a)
  later under its own direction record. Not a regression: today's output (column 0) is wrong by the contract and no
  code line moves differently.
  (6) C15: `\t# tab` → `# tab` today → kept (fix).
  (7) C4 (parseable), C6, C8, C9, C17, every on-type cell: unchanged (controls; on-type already refuses a non-logical-start
  line, `OnTypeFormattingHandler.cs:75`; C17's comment is frozen by clause 7 and the builders' frozen check).
  (8) Corpus half: Phase 1's sweep run measures the `comment` pin per (twin, route) (the "before"); Phase 2 drains it to
  0 (the "after"); the 200-case seed-2280 set re-run on both binaries — expected: the 24 cells newly edited at P22h
  become no-edit where their only edits were comment lines; blank-line whitespace stripping (clause 2's allowance)
  stays (control); code lines changed: 0 both ways.
  **(9) C16 / C16b range-line(comment): wrong edit (`# c` → column 0) → no edit (fix); C16 full / range-whole: no edit →
  no edit (control — clause 6c refuses the body's repair before and after; the applied text is the document both
  ways)** [ADDED: adequacy — the plan listed this cell as a 6c "no-edit control"; refuted by the verifier's probe @
  53ee2d993: 6c never holds a comment line, see Current State].
- **A pin that may MOVE (direction controls):** `KnownLimitPin[(S6k, RangeLine)] = 10` / `OnType = 10` and the
  `KnownLimitCellsPin` beside it — S6k's string-content edits on a re-paired closer may include comment-looking lines
  that are now kept verbatim: a SHRINK is a cell closed on #2274 (the pin's own instruction: change the literal in the
  same commit and say which). `S7ParityExemptPin` must not move (depth parity of the misindented line; comments are not
  judged by depth). The S3 **`applied-differs` floor** (`:2546-2556`: some cell of each route edits an S3 document on
  every twin) may drop on the `identity`/`crlf`/`cr` twins if their S3 edits were comment dedents and blank-line
  stripping only — Phase 2 measures `FMTROUTE-CENSUS S3 applied-differs`; if a twin reads 0, the floor keeps its purpose
  on the twins with a repairable misindent (`wide`) and the lead records the measured reason (every identity S3 document
  is already on 4-space levels) in the commit body — a lead ruling, not a silent edit.
- **Blast radius:** `Sharpy.Lsp.Tests` whole (the sweep, `FormattingFallbackTests`, `RangeFormattingTests`,
  `OnTypeFormattingTests`, `FormattingEditsTests`, `FormatterDocEditorExamplesTests`, `WorkspaceAndFormattingTests` E2E,
  `FrontEndParityTests` unreached but in the project). No compiler, Semantic or CodeGen change: Compiler.Tests,
  Stdlib.Tests, Cli.Tests and the three GapDiscovery sweeps are unreached (they still run in the gate).
- **Generated artifacts touched:** none; "regen: no diff @ sha" recorded at the gate.

## Design Decisions

1. **One predicate, one set, three readers (rung: LSP-internal; no carrier).** `IndentationService.IsCommentOnlyLine(string line)`
   — true iff the first character after leading spaces/tabs is `#` (the lexer's own test at `Lexer.Indentation.cs:94`;
   the width loop's own spelling is `Trim(' ', '\t')` then `trimmed[0] == '#'`, `IndentationService.cs:173-174` — same
   first character; a line indented with OTHER whitespace is L12's whole-document refusal at the funnel
   (`FormattingEdits.CheckedIndentOnly`, `HasOtherWhitespace`) — the builders DO run on it but their output is discarded
   [CORRECTED: "never reaches a builder"]) — and
   `IndentMap.CommentLines` (1-based; every line the width loop's `#` arm skips that is not a literal line). The width
   loop at `IndentationService.cs:174` calls the predicate (one spelling), and the three readers are the two builders and
   clause 8. Why not a lexer fact: see Alternative layer. Why a set rather than each builder re-testing the text: the
   exclusion of literal lines and frozen/unplaced lines is the map's knowledge, and the check must judge the SAME lines
   the builders skip.
2. **Builders return the line verbatim (R-GD (a)), not a guessed level.** In `ReindentDocument.Reindent` and
   `ComputeIndentOnlyRangeEdits`, after the literal and frozen checks and before the blank check:
   `if (map.CommentLines.Contains(n)) return line; / continue;`. A comment line inside a closed bracket (C10) and a
   tab-indented comment (C15) are covered by the same arm. Blank lines keep today's behaviour (whitespace-only → empty,
   clause 2) — not in R-GD's scope.
3. **Clause 8 of `IndentOnlyPreserved`:** every line in `before.CommentLines` byte-identical in the applied text. Placed
   beside clauses 7 and 6c (the other "byte-identical" loops). `after.CommentLines` need not be compared: clause 2
   fixes the trimmed content and clause 3 the literal lines, so the set cannot change without one of them refusing.
   The doc comment's list gains item 8 (P22i, R-GD, #2290).
4. **The sweep bucket `comment`** (string `"comment"` per the charter; C# constant `CommentLine`, since `Comment` is the
   twin constant — a row reads `stem comment range-line S7 comment # #2290`, twin then bucket, as the header documents).
   In `UnparseableOracles`, after the line-count check: for each line `i` with `IsCommentOnlyLine(x[i])` and
   `!truth.LiteralLines.Contains(i)`, `x[i] != t[i]` is a `comment` failure naming the line and both texts. D's text
   chooses the lines (what the routes read), Q's literal lines exclude string content (S6k's known limit stays
   `literal`'s pinned cell). Added to `Buckets` (loader + census), the allowlist header, and the roster row. Judged on
   unparseable documents only: on a parseable document the real formatter MAY move a comment (guarantee 2) and the
   `whole`/`net` buckets own that.
5. **Pinned while red, then rows (lead ruling L1's precedent, plan-f92797).** The bucket alone would fail `comment` on
   one row per (stem, twin, route, state) holding an indented comment — the comment twin alone is ~3,291 stems × 3
   routes × up to 11 unparseable states (S3, S4, S5, the seven S6 states, S7) [CORRECTED: was "~3,700 × … × up to 8"],
   the 6 MB allowlist blob P22h deleted whole — **and it fails in the REGULAR suite, not only under FULL: every S3–S7
   cell runs on every stem in every mode (Current State)**. So: `CommentPendingPin` per (twin, route), EXACT, cited
   #2290, asserted under the existing `everyTwinRan.Count == 0` guard (`:2578-2579`) — which the regular gate satisfies,
   exactly as `KnownLimitPin` is asserted today [CORRECTED: the plan said "FULL mode only"]; while the table exists a
   `comment` failure is counted (`comment-pending {twin} {route}` tally, one per (stem, twin, route) + a cells tally)
   instead of failing the stem, and the loader REFUSES a `comment` row. **The tally mechanism must land in the SAME
   commit as the bucket** (Phase 1 Task 1), or that commit reddens the gate on every comment-holding stem. Phase 2
   drains the counts to 0 and DELETES the pin, the tally and the loader refusal, so `comment` then rides the ordinary row
   ratchet with 0 rows (`rows.Count.Should().Be(0)` keeps its literal; the message names P22i). Weaker than per-row
   identity only while transient; equality keeps a swap visible. The pin's counts are the corpus "before" of the
   direction record.
6. **Hand axis as by-direction rows** in `FormattingFallbackTests` (the `NoEditCells` / `RepairCells` pattern, routes
   `full`, `range-whole`, `range-line N`, `ontype N`): P22i rows for C1–C3, C5–C7, C10–C12, C15, **C16, C16b, C17** with
   the @ 53ee2d993 candidate text named in each comment, plus a clause-8 unit test (refused and accepted twin) and an
   on-type control (`ontype` on a comment line: no edit before and after — the handler's own rule,
   `RefusedAsAContinuationLine` applies). **The rows go in a P22i-own theory, not `NoEditCells`** [CORRECTED: verifier]:
   `RefusedAsAContinuationLine` (`FormattingFallbackTests.cs:1207-1212`) returns true for EVERY `range-line N` / `ontype N`
   row whose line is not a logical start — a comment line never is — and the existing theory then SKIPS the
   `IndentOnlyPreserved(document, candidate) == false` assertion. For on-type that is right (the handler refuses the
   line itself); for range-line it is false — `ComputeIndentOnlyRangeEdits` has no logical-start rule, so after the fix
   the only things refusing C1 `range-line 4` are the builder's `CommentLines` skip and clause 8. A P22i range-line row
   riding `NoEditCells` would never exercise clause 8, and mutation (iii) would be caught by the single unit test alone.
   The P22i theory asserts the check's refusal for every `full`, `range-whole` and `range-line N` row and skips it only
   for `ontype N`.
7. **Docs are a deliverable of equal weight:** `docs/tooling/formatter.md` "In the editor" gains one bullet — the
   indentation-only pass and format on type leave a comment-only line exactly as written, even when they re-indent the
   code around it; unlike Format Document on a file that parses (guarantee 2), which places the comment with its code —
   and one executed `document` example (C3: `y` repaired, `# c1` kept; today's output moves the comment, so the example
   discriminates). `FormatterDocEditorExamplesTests`' literals move 10→11 examples, 20→21 requests, 3→4 changing.
8. **Roster:** `docs/design/gap-discovery-contracts.md` row "Formatting route parity (#2168)" names the `comment` bucket
   (the bucket list becomes ten + two fact buckets) and cites #2290; the allowlist header documents the bucket.

## Implementation

### Phase 1: Generator FIRST — the `comment` bucket, its positive control, the pin measured

**Goal:** the sweep can see a comment line's indentation; the red is measured and pinned; the regular suite stays green.

**Acceptance (class-level, measurable):**
- Matrix/sweep: `UnparseableOracles` flags `comment` on a dedented comment line of an S3/S7 document and on nothing
  else; a run of the sweep class (regular mode suffices — the tally is over S3–S7 cells, which run on every stem in
  every mode [CORRECTED: was "FULL run"]) reports `FMTROUTE-CENSUS comment-pending {twin} {route}=N` for all 20
  (twin, route) keys, pinned EXACT in `CommentPendingPin` and ASSERTED in the regular suite (the existing
  `everyTwinRan` guard); the regular suite green **after every commit of this phase** (Task 1's commit carries the tally).
- Guard mutation: drop the `comment` loop in `UnparseableOracles` → `PositiveControl_Comment_SeesADedentedCommentLine`
  red; restore → green. Positive control for the exclusion: a `#` line that is string content in Q is NOT flagged
  `comment` (and IS `literal`). Pin mutation: change one pin literal by ±1 → the FULL census red.
- Allowlist entries that drain: none (0 rows; `comment` rows refused by the loader while the pin exists).
- Execution evidence: the FULL run's census lines @ sha, saved to `.claude/tmp/p22i-impl/census-p1.txt`.

#### Tasks

1. **The bucket and its control** — `src/Sharpy.Lsp.Tests/Conformance/FormattingRouteParitySweepTests.cs`,
   `src/Sharpy.Lsp/IndentationService.cs` (the predicate only), `formatting-route-parity-allowlist.txt` (header)
   - Add `IndentationService.IsCommentOnlyLine(string)` (internal static; `TrimStart(' ', '\t')` then `[0] == '#'`) and
     call it from the width loop's `#` arm (`:174`) — no behaviour change yet (the map's set comes in Phase 2).
   - Add `internal const string CommentLine = "comment";` to the bucket constants and to `Buckets`; in
     `UnparseableOracles`, after the content check, the loop of Design 4 with a detail
     `"{request}: line {i} is a comment-only line: D '{x[i]}' vs T '{t[i]}'"`.
   - **In the same commit** [CORRECTED: moved here from Task 2]: the `comment-pending {twin} {route}` tally (a `comment`
     failure removed from `Failures` and counted while `CommentPendingPin` is non-empty, like `KnownLimit` at `:1418`),
     the loader refusal of `comment` rows, and the `CommentPendingPin` table with its EXACT assertion under the
     `everyTwinRan` guard — the literals filled in Task 2. Without the tally this commit fails the regular suite on
     every stem holding an indented comment (S3–S7 run on every stem in every mode).
   - `PositiveControl_Comment_SeesADedentedCommentLine`: parent `Q = "def main():\n    a = 1\n    # c1\n    if a == 1:\n        print(a)\n"`
     cut after `if a == 1:` (S3, via `Cut`); `T = D` with `# c1` → column 0 → buckets `Equal(CommentLine)`; `T = D` →
     empty; `T = D` with `a = 1` re-indented to 8 → does NOT contain `CommentLine` (depth's cell, the axis control). The
     literal exclusion: `Q19`'s S5 twin (D19, `"""` inserted above `g`) with a `#` line inside `g`'s string — the string
     line re-indented is `literal`, never `comment`. Also assert the misindent control document of #2290
     (`def f():\n      y = 1\n\ndef main():\n    # c1\n    b = 1\n`, S7-shaped by hand: unparseable, Truth from its
     clean twin) is flagged `comment` for today's applied text (`FormattingFallback.ReindentDocument` of it) — the
     "both builds dedent it" positive control the charter names.
   - Allowlist header: add `comment` to the bucket list ("a comment-only line of D — not string content in Q — is not
     byte-identical in T; R-GD, #2290").
   - Commit: `test(lsp): the route-parity sweep judges a comment-only line's indentation — the comment bucket (R-GD, refs #2290)`
2. **The pin, measured** — the sweep
   - Fill the 20 `CommentPendingPin` literals (the mechanism landed in Task 1 [CORRECTED]); the census line and a doc
     comment naming L1's precedent and the Phase 2 deletion.
   - Run the class: `.claude/scripts/dotnet-serialized test src/Sharpy.Lsp.Tests --filter "FullyQualifiedName~FormattingRouteParitySweepTests" --logger "console;verbosity=normal" > .claude/tmp/p22i-impl/census-p1.txt`
     (`dangerouslyDisableSandbox: true`; regular mode measures the same S3–S7 tallies as FULL — add
     `SHARPY_ROUTE_SWEEP_FULL=1` only if the S1/S2 ceilings are wanted in the same log); fill the 20 literals from
     `FMTROUTE-CENSUS comment-pending`; re-run to green. Record the S3 `applied-differs` and `known-limit` census lines
     beside it (the Phase 2 controls). Note `ITestOutputHelper` lines reach the console log only for failing tests —
     read them from the `.trx`/the failing-run log, as P22h did (`census-p1b.txt` → `census-p1b-green.txt`).
   - Commit: `test(lsp): pin the comment-bucket count per (twin, route) while red — the ratchet P22i Phase 2 drains and deletes (L1 precedent, refs #2290)`

### Phase 2: The seam — one set on the map, both builders, clause 8, the hand axis; the pin drained and deleted

**Goal:** no indent-only route edits a comment-only line; a builder that forgets is refused; the corpus count is 0.

**Acceptance (class-level, measurable):**
- Matrix/sweep: sweep-class run `comment-pending` 0 on all 20 keys (regular mode measures it; FULL optional) → the pin,
  tally and loader refusal DELETED; allowlist 0 rows;
  `KnownLimitPin`/`KnownLimitCellsPin` and `S7ParityExemptPin` re-measured (a shrink of S6k is recorded on #2274 in the
  commit body and the literal changed in the same commit; S7 parity unchanged); the S3 `applied-differs` floor measured
  per twin (see pre-mortem).
- Hand matrix: the P22i rows of `FormattingFallbackTests` green — repair rows (C3 full/range-whole: `y` at 4 AND `# c1`
  at 4; C10: `2]` at 4 AND `# inner` at 10; C11: code at 4, `# c` at 2; C12: code at 4, `# c` at 8) and no-edit rows
  (C1, C2, C13, C14, C7 × full/range-whole/range-line; C5 × three routes; C15; **C16/C16b `range-line 2` (wrong edit →
  no edit; the candidate `# c` at column 0 refused by clause 8 — 6c does not hold the line) and C16 full/range-whole
  (no edit both ways; the candidate moving `y` refused by 6c)**; C17 × four routes (frozen, control); the on-type
  controls; C6/C8/C9 unchanged) [ADDED: adequacy]. Every `range-line N` row asserts the check's refusal (Design 6).
- Guard mutations (each recorded broken → red / restored → green in the commit body): (i) remove the `CommentLines` skip
  in `ReindentDocument` → C3 `full` repair row red (the check refuses the candidate: applied == document ≠ repaired) and
  the Phase 3 doc example red; (ii) remove it in `ComputeIndentOnlyRangeEdits` → C3 `range-whole` and C1 `range-line 4`
  rows red; (iii) drop clause 8 → `Comment_ADedentedCommentLine_IsRefused_TheCodeRepairBesideItIsNot` red (the check
  accepts the @ 53ee2d993 text); (iv) with clause 8 present and BOTH builder skips removed (the clause-only tree), the
  C3/C11/C12 repair rows are red — the inertness reading of the pre-mortem, recorded.
- Allowlist entries that drain: the `CommentPendingPin` table (20 literals → deleted).
- Execution evidence: the FULL run @ sha (`census-p2.txt`), the hand rows @ sha, the direction probe of Phase 3.

#### Tasks

1. **The map's set** — `src/Sharpy.Lsp/IndentationService.cs`
   - `IndentMap` gains `IReadOnlySet<int> CommentLines`; the width loop's `#` arm adds the line when it is not a literal
     line; the record's doc comment states the rule and R-GD. The empty-map (`TokenizeAll` threw) arm passes an empty set.
   - Commit: `feat(lsp): the indent map names its comment-only lines — the lines the width loop reads as trivia, never a level (R-GD, refs #2290)`
2. **Both builders + clause 8** — `src/Sharpy.Lsp/Handlers/FormattingFallback.cs`, `src/Sharpy.Lsp/Handlers/RangeFormattingHandler.cs`
   - `Reindent`: `if (map.CommentLines.Contains(number)) return line;` after the frozen check; `ComputeIndentOnlyRangeEdits`:
     the same `continue` after its frozen check; summaries updated (a comment-only line is returned verbatim, R-GD).
   - `IndentOnlyPreserved`: clause 8 loop over `before.CommentLines`; doc list item 8.
   - `OnTypeFormattingHandler.cs:64` comment: name the control (a comment line is not a logical start; no change).
   - Commit: `fix(lsp): the indent-only routes leave a comment-only line verbatim — both builders skip it and the check refuses an applied text that moves one (R-GD, refs #2290)`
3. **The hand axis** — `src/Sharpy.Lsp.Tests/FormattingFallbackTests.cs`, `src/Sharpy.Lsp.Tests/OnTypeFormattingTests.cs`
   - Constants C1, C2, C3, C5, C6, C7, C10, C11, C12, C13, C14, C15, **C16, C16b, C17** (texts from the Current State
     table; name each by its position in the matrix, never by the class); `P22i_NoEditCells` rows (document, route, the
     @ 53ee2d993 candidate) and `P22i_RepairCells` rows (document, route, the repaired text) in **parallel `P22i_*`
     theories** [CORRECTED: not `NoEditCells` — its `RefusedAsAContinuationLine` skip would leave every range-line row's
     clause-8 refusal unasserted, Design 6]; `Comment_ADedentedCommentLine_IsRefused_TheCodeRepairBesideItIsNot`
     (clause 8: C3 with `# c1` at 0 → false; C3 with `y` at 4 and `# c1` at 4 → true; **C16 with `# c` at 0 and `y`
     unchanged → false — the cell no other clause refuses**; the literal-`#` twin from D19 → judged by clause 3, not 8);
     `CommentLines` asserted for C10 (`{6}`), C15 (`{5}`), **C16 (`{3}`, and `UnplacedLines` = `{4}` without 3 — the
     fact that makes clause 8 load-bearing there)**, D19 (the `#` string line NOT in the set).
   - `OnTypeFormattingTests`: `CommentOnlyLine_NoEditAsync` (C1 line 4; `# c1` indented 4 under a 4-block AND C5's
     `# deep` at 8: no edit — the handler's rule, before and after).
   - Commit: `test(lsp): comment-only lines across position × route × document state — by-direction rows against 53ee2d993 (refs #2290)`
4. **Drain the pin** — the sweep, the allowlist header
   - Sweep-class run (regular mode; FULL optional) → `comment-pending` 0 everywhere → delete `CommentPendingPin`, its tally/census line and the loader
     refusal; `comment` failures are rows again; update the `rows.Count.Should().Be(0)` message and the file-header
     history line; re-measure and, if moved, update `KnownLimitPin`/`KnownLimitCellsPin` (S6k) with the reason; assert
     `S7ParityExemptPin` unchanged; read the S3 `applied-differs` census per twin and act per the pre-mortem.
   - Commit: `test(lsp): the comment bucket rides the row ratchet with 0 rows — the P22i pin drained and deleted; pins re-measured @ <sha> (refs #2290)`

### Phase 3: Docs, roster, direction record, gate

**Goal:** the rule is stated and executed in the docs; the roster names the bucket; the direction record and the gate
are cited at their shas; #2290 closes on evidence.

**Acceptance:**
- `FormatterDocEditorExamplesTests` executes the new `document` example (C3 → `y` at 4, `# c1` kept) — mutation: edit
  the example's result to the @ 53ee2d993 text (`# c1` at column 0) → red; counts 11/21/4.
- Direction record `.claude/tmp/p22i-impl/direction-record.md`: the 60 hand cells (base-lsp @ 53ee2d993 vs HEAD) each
  classified per the pre-mortem's eight classes; the 200 seed-2280 cases (`fuzz2/cases.json`) on both binaries with
  "code lines changed: 0" asserted and the comment-only edit count before/after; the corpus half = Phase 1's pin
  counts → Phase 2's zero.
- Gate: `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`
  to a PRIVATE log path, green @ the last commit; FULL sweep 0 rows; `regen: no diff @ sha`;
  `build_tools/check_allowlist_issue_state.sh` green.

#### Tasks

1. **Docs + roster** — `docs/tooling/formatter.md`, `docs/design/gap-discovery-contracts.md`
   - The "In the editor" bullet (Design 7) after the `:124` fallback bullet; the executed example
     (`<!-- editor-example: document -->` with the C3 text and its result); the roster row's bucket list and cites.
   - Commit: `docs(formatter): the indentation-only pass and format on type leave a comment-only line exactly as written (R-GD, refs #2290)`
2. **Direction record + gate** — `.claude/tmp/p22i-impl/`
   - Base binary: `git worktree add ../sharpy.worktrees/p22i-base 53ee2d993` (`dangerouslyDisableSandbox: true`; use
     `git -C`, never `cd &&`), `.claude/scripts/dotnet-serialized build <worktree>/src/Sharpy.Lsp`, copy
     `bin/Debug/net10.0/` to `.claude/tmp/p22i-impl/base-lsp/`; driver `.claude/tmp/p22i-plan/lspfmt.py` with
     `SHARPY_LSP=<base>` vs the default (HEAD); cases `.claude/tmp/p22i-plan/cases.json` and
     `.claude/tmp/p22h-vi/regression/fuzz2/cases.json`.
   - Ledger lines for anything the prober finds outside the matrix; the lead posts the cells after the gate (#2290 for a
     comment-placement mechanism, #2274 for an S6k shrink).
   - Commit: none (evidence in the close-out and the issue comments).

## Testing Strategy

- **Generator first (contract §11):** the existing sweep's `comment` twin × every unparseable state × four routes × five
  twins, judged by the new `comment` bucket; pinned while red (L1), drained to 0 and the pin deleted; the allowlist stays
  at 0 rows. The axis the generator cannot enumerate — a comment at a width OTHER than its anchor's (deeper than its
  block, column 0 inside a body, a tab, a comment inside a closed bracket at the continuation's width, a 2-space body) —
  is the hand matrix in `FormattingFallbackTests`, and only that axis.
- **New fixtures:** none file-based (LSP-only routes). Every no-edit row carries the @ 53ee2d993 candidate text it
  refuses (an edit, asserted `NotBe(document)`), and every repair row the applied text — the positive controls that the
  routes still repair code beside a kept comment (C3, C10, C11, C12).
- **Edge cells outside the issue's list:** C5 (deeper than its block), C6 (column 0 in a body — unchanged), C7 (block
  end), C10 (inside a closed bracket), C11/C12 (the repaired-body widths), C15 (tab), C4 (the parseable control: the
  real formatter still places the comment with its code — guarantee 2 is untouched), C16/C16b (inside a putative body —
  the verifier's probe showed range-line dedents it today; 6c holds code lines only), C17 (the frozen tail — a control
  that held). When every spelling agrees, change axis: the prober's two axis changes are **(a') a comment line DIRECTLY
  after a dropped line, before its recovery line (`def main():\n    y = $\n    # c\n    z = 1\n`): the recovery-line
  judgment (`partiallyRead.Contains(previousStart)`) then runs at `z`, one line later than the dropped line — probe
  that the comment neither becomes the recovery line nor changes `z`'s verdict, on both builds; and (b') a
  comment-only LAST line without a trailing line break, indented, under an unterminated string above (the `EndOfFile`
  anchor injects at column 0 only)** [CORRECTED: the plan's (a) was a matrix cell, not a control — consumed as C16; (b)
  held as C17 and is now a hand row, not a prober axis].
- **Discriminating outputs:** C3's expectation prints `y` at 4 AND `# c1` at 4 — today's output has `# c1` at 0, a
  clause-only tree has `y` at 6 — so the row distinguishes both wrong trees from the fix.
- **Docs:** the one executed `document` example; the spec is untouched (no language change; `docs/language_specification/`
  has no formatter rule for comments in unparseable text).
- **Blast radius and regen:** `Sharpy.Lsp.Tests` only; no generated artifact (`regen: no diff @ sha` at the gate); the
  whole-solution gate once at the last commit, run in the background to a private log; S6k/S7 pins and the S3 floor
  re-measured as direction controls.

## Issues to Close

- #2290 — the indent-only fallback writes every comment-only line of an unparseable document at column 0 — closed by
  Phase 2 Task 4 (code) + Phase 3 Task 1 (docs); **close criterion:** FULL sweep `comment` 0 rows with the pin deleted
  @ sha; the P22i hand rows green @ sha (repair AND no-edit rows, mutations (i)–(iv) recorded); the executed editor
  example @ sha; the direction record over base-lsp @ 53ee2d993 vs HEAD with "code lines changed: 0" and every changed
  cell in classes (1)–(8); whole-solution gate `passed/failed/skipped @ sha (measured)`; `check_allowlist_issue_state.sh`
  green after the push. After the close, a new comment-placement mechanism is a cell on the permanent #2274 (or on a
  new tracker the lead files), and by R-GF its group queues behind P34 and P13b unless it is a regression by direction.

## Class trackers this plan may post cells to

- #2290 — comment-only lines in the indent-only routes (this plan's class; a new comment-placement mechanism found
  during the round is a comment here, never a new issue).
- #2274 — PERMANENT known-limit tracker (R-FQ): an S6k `KnownLimitPin` shrink (a comment-looking string line now kept
  verbatim) is recorded here.
- #2232 (P47) — only to note the `comment` bucket exists for its comment-token oracle to reuse; no cells.
- A NEW tracker only for a class none of these covers; the lead files it, never an implementer (no `gh issue create` in
  any lane).

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-10 @ 53ee2d993
**Plan file:** `.claude/plans/plan-ee5544.md`

Mechanical check: `python3 build_tools/check_plan_refs.py .claude/plans/plan-ee5544.md --issues` — paths 17 ok / 2 missing
(both Phase 1/3 deliverables: `census-p1.txt`, `direction-record.md`); `file:line` 13 ok; 5 symbols not found are the
proposed ones (`CommentLine`, `CommentLines`, `CommentPendingPin`, `IsCommentOnlyLine`) plus `RangeFormattingHandler`
(the FILE name; the class is `SharpyRangeFormattingHandler` — the plan uses it as a file reference, fine); #2168 CLOSED is
only the roster row's name. Issue #2290 open; R-GD (a), R-GE (a), R-GF read from its two owner comments of 2026-10-10 and
match the plan verbatim. Probes: `.claude/tmp/p22i-plan/verify-cases-axis*.json` run through `lspfmt.py` over the built
`Sharpy.Lsp` (dated 21:13, after the 21:11:47 commit) → `verify-head-53ee2d993-axis*.txt`; C1/C3/C5 re-probed and match
the plan's table.

### Corrections Made
- **Axis change (a) → matrix cell C16/C16b** (Current State table, Defect Class matrix, by-direction class (9), hand axis,
  Phase 2 acceptance and Task 3). Before: "a comment line inside the UNPLACED body below a dropped header (6c already
  freezes it — a no-edit control)". After: a FIX cell — `range-line(comment)` dedents it 8→0 today (and 10→0 at width
  10); full/range-whole are no-edit both ways only because 6c refuses the BODY's repair. Refuted by reading
  `IndentationService.cs:174` (`continue` before `unplaced.Add` at `:227`) and by probe. Clause 8 is the only guard for
  it, so the clause-8 unit test gains the C16 candidate and `UnplacedLines`/`CommentLines` are asserted for C16.
- **Pins are asserted in the regular suite, not FULL-only** (Current State, Design 5, Phase 1 acceptance, Task 1/2,
  Phase 2 acceptance and Task 4, Scope cost). The five per-twin theories take `CorpusNames` (every corpus stem) in every
  mode; `IsSampledCell` samples only S1/S2 `range-line`/`ontype`; the pin block's guard `everyTwinRan.Count == 0`
  (`:2578`) is therefore met at the gate. Consequences written in: Task 1's commit must carry the tally mechanism or it
  reddens the gate on every comment-holding stem; the `comment`, known-limit, S7-parity tallies and the S3 floor are
  mode-independent, so Phase 1/2 measure with regular-mode class runs (cost: three FULL runs → one, at the gate).
- **Corpus size** 3,878 − 177 ≈ 3,700 → **3,291 stems** @ P22h's FULL census (`single-file=3445 excluded-unparseable=154`);
  Design 5's "up to 8 states" → 11 unparseable states (S3, S4, S5, S6, S6n, S6k, S6r, S6x, S6o, S6ok, S7).
- **Hand rows in a P22i-own theory, not `NoEditCells`** (Design 6, Task 3): `RefusedAsAContinuationLine` skips the
  check-refusal assertion for every `range-line N` row on a non-logical-start line, so a P22i range-line row there would
  never exercise clause 8 and mutation (iii) would rest on one unit test.
- `FormatterDocEditorExamplesTests` path (`Conformance/`) and literal lines `:46-48` → `:49-51`.
- Design 1 "never reaches a builder" → the builders run on an other-whitespace document; the funnel discards the output.
- Prober axes replaced: (a') a comment directly after a dropped line, before its recovery line; (b') an indented
  comment-only last line without a trailing break. Pre-probed @ 53ee2d993 (`verify-head-53ee2d993-axis2.txt`): BOTH are
  class members today — the comment goes to column 0 on full/range-whole/range-line, `z = 1` unchanged (the recovery
  judgment is not disturbed), on-type no edit. Expected after the fix: no edit on every route. They are direction-record
  cells for Phase 3, not new axes for the verify round.

### Adequacy
- **Contract:** named — the indent-only routes edit only text the lexer read as the user wrote it; a comment-only line is
  trivia (`Lexer.Indentation.cs:94`, `preserveTrivia` defaults to false in both `Lexer` constructors, so a comment line
  has no token and no `logicalStart` entry), uniform across route × position × state × twin.
- **Matrix:** larger than the repro lists — and the verifier found one more cell the plan had mislabelled as a control
  (C16, inside a putative body) and two more members on the prober axes ((a'), (b')). All on the same seam; none changes
  the cure. The dispatch sites were enumerated: `LineIndent` has exactly three readers outside the map
  (`FormattingFallback.cs:29`, `RangeFormattingHandler.cs:84`, `OnTypeFormattingHandler.cs:78`) plus the check's own
  reads (`:153-154`); `BuildIndentMap` is called at those three, the check (`:125-126`) and the funnel (`FormattingEdits.cs:83`,
  for the L12/literal-state refusals only). `new IndentMap(` has two sites, both in `IndentationService.cs` (`:53`, `:240`) —
  the plan's empty-map arm covers the first.
- **Cure:** seam — ONE predicate (`IsCommentOnlyLine`, called from the width loop's own `#` arm), ONE set
  (`IndentMap.CommentLines`), read by both builders AND clause 8 (a builder that forgets is refused), plus the sweep
  bucket; meta-class row "position/route missing a shared check".
- **Harness:** named — `FormattingRouteParitySweepTests` + the `comment` bucket; allowlist delta = the `CommentPendingPin`
  table (20 literals) created in Phase 1 and deleted in Phase 2; `KnownLimitPin` (S6k `range-line=10 ontype=10` @
  census-p1c-green) may shrink — recorded on #2274.
- **Layer:** every decision rung-justified — LSP-internal, no carrier (charter §4); the lexer-fact alternative is
  rejected with a reason (a mirror of the same predicate). No rung-4 name-keyed rule.

### Scope & convergence
- **Group:** one (P22i, charter §3.1c) · **Alone:** no — required? no (LSP edits only; no printed output or emitted
  layout changes; no snapshot moves) · **Gates:** 1
- **Generator:** existing (`comment` twin × S3–S7 × four routes × five twins), extended FIRST with the bucket that judges
  the axis; the hand axis is stated and is what `CommentInjected` cannot produce (a comment at a width other than its
  anchor's, a tab, inside a closed bracket at the continuation's width, inside a putative body).
- **Trackers named:** #2290 (class), #2274 (permanent known-limit), #2232 (P47 note only) · **Fidelity rows:** N/A
  (editor surface; `python-fidelity-scope.md` lists no formatter surface) · **Filing:** ledger + lead files; no
  `gh issue create` in any lane · **R-GF** honoured (a residue queues; only a regression by direction is in-round).

### Falsifiability
- `comment` bucket (`UnparseableOracles` loop): mutation named — drop the loop → `PositiveControl_Comment_SeesADedentedCommentLine`
  red; exemption not subject (the literal exclusion is tested by a `#` string line of D19 that must be `literal`, never
  `comment`); positive control yes (S3 cut of a document with `# c1` dedented → `Equal(CommentLine)`; `T = D` → empty;
  `a = 1` at 8 → `depth` not `comment`); by-direction: the C3 misindent control flagged for today's `ReindentDocument` output.
- `CommentPendingPin`: mutation named (±1 on a literal → census red); EXACT, not a floor; asserted at the gate (corrected).
- `IndentMap.CommentLines` + both builder skips: mutations (i) and (ii) named with the rows that go red (C3 `full`; C3
  `range-whole` + C1 `range-line 4`); inert-fix check = mutation (iv), the clause-only tree, red on the C3/C11/C12 repair
  rows (applied == document ≠ repaired) — discriminating: today's output has `# c1` at 0, the clause-only tree has `y` at 6.
- Clause 8: mutation (iii) → `Comment_ADedentedCommentLine_IsRefused_TheCodeRepairBesideItIsNot` red; now also exercised
  by every P22i `range-line N` row (corrected: a P22i-own theory, no `RefusedAsAContinuationLine` skip for range-line);
  C16's candidate added as the one cell no other clause refuses.
- Hand rows: every no-edit row carries the @ 53ee2d993 candidate (asserted `NotBe(document)`), every repair row the
  applied text; by-direction classes (1)–(9) listed.
- On-type control `CommentOnlyLine_NoEditAsync`: a control, not a guard — no mutation needed; the handler's rule is
  `OnTypeFormattingHandler.cs:77` (`!map.LogicalLineStarts.Contains(line + 1)`).
- Doc example (C3): mutation named — edit the result to the @ 53ee2d993 text → red; discriminates (today moves the comment).
- Roster/allowlist header: documentation, no guard claimed.

### Warnings
- **Census lines are test output.** `ITestOutputHelper` lines (`FMTROUTE-CENSUS …`) reach the console logger only for a
  FAILING test at `verbosity=normal`; P22h read them from a red run before the green one (`census-p1b.txt` →
  `census-p1b-green.txt`). Plan the first Phase 1 run with placeholder literals (red) to collect the 20 counts, as Task 2
  now says. [added to Task 2]
- **Pin literals are the gate's.** Because the pins are asserted in the regular suite, any corpus fixture added or
  changed between Phase 1 and Phase 2 moves `comment-pending` and fails the gate — the plan's two commits of Phase 1
  should land close together, and the implementer re-measures before the Phase 2 drain commit.
- **C11/C12 consequence stays visible.** After (a), a repaired 2-space or 8-space body leaves its comments at the old
  width; the direction record and the doc bullet must state it plainly (the plan does — keep it when editing
  `formatter.md`), since a verify round will otherwise file it as a residue; by R-GF it is a tracker cell, not an issue.
- `KnownLimitPin` for S6k (`range-line=10 ontype=10`) may shrink (a comment-looking string line kept verbatim) — the plan
  handles it; `KnownLimitCellsPin` moves with it; S6ok is untouched (its bucket is `factSpurious`).
- FULL-mode cost (≤ 2 × 248 s) and the "24 seed-2280 cells all comment-line-only" claim are plan-f92797's and the
  filer's measurements, not re-measured by the verifier.

### Missing Steps Added
- Phase 1 Task 1: the tally / loader refusal / pin table in the same commit as the bucket (else a red commit).
- Phase 2 Task 3: C16, C16b, C17 constants and rows; C16 in the clause-8 unit test; `UnplacedLines`/`CommentLines`
  asserted for C16; the P22i-own theory asserting the check's refusal on range-line rows.
- Phase 3 direction record: the (a') and (b') cells (pre-probed @ 53ee2d993, `verify-head-53ee2d993-axis2.txt`).
- Blast radius / gate / regen: already present and correct (Lsp.Tests whole; whole-solution gate to a private log; "regen:
  no diff @ sha"; `check_allowlist_issue_state.sh` after the push). No change.

### Unchecked Claims
- "`LineOwnLine` injects at the statement's indent, module statements and the first statement included" — read from
  `FormatterTwins.cs:479` (`LeadingWhitespace(...)` of the statement's first token); the "first statement included"
  part was not executed. Verify by running the positive control's S3 document through `CommentInjected` in Phase 1.
- "111 of base's 141 edited cells" and "24 seed-2280 cells all comment-line-only" — the filer's numbers @ 040c79ec2
  (`.claude/tmp/p22h-vi/regression-report.md` §F1); the Phase 3 direction record re-derives them on both binaries.
- FULL-mode wall clock ≤ 2 × 248 s — plan-f92797's measurement; Phase 3's single FULL run re-measures it.

## Implementation Checklist

Lead-implemented (no teammates: one LSP seam and one test file, serial by dependency). Base 53ee2d993; baseline gate
79124/0/4 @ 53ee2d993 (measured, `.claude/tmp/p22h-vi/gate-final-53ee2d993.log`; HEAD = base at start, tree clean).

- [x] P1 T1+T2 — the `comment` bucket, CommentTargets, CommentPendingPin measured — lead — 51f0ca092
- [x] P2 T1 — `IndentMap.CommentLines` — lead — eb361c58c
- [x] P2 T2+T4 — both builders + clause 8; pin drained (0 on all 20 keys) and deleted in the same commit — lead — e75095fdc
- [x] P2 T3 — hand axis (47 no-edit rows — the commit body says 49 —, 8 repair rows, builders/clause-8/map facts, on-type control), mutations (i)–(iv) + by-direction run — lead — 998d309da
- [x] P3 T1 — docs + roster; doc example mutation-tested — lead — ae5eb0aae
- [x] P3 T2 — direction record `.claude/tmp/p22i-impl/direction-record.md` (hand 80 cells: 41 changed, all "comment kept, code
  identical", code lines changed 0; fuzz 200: 119 edit → no edit, comment-line edits 616 → 0, code 0); gate
  **79185/0/4 @ ae5eb0aae (measured, `.claude/tmp/p22i-impl/gate-ae5eb0aae.log`)** vs 79124/0/4 @ 53ee2d993 (+61 = 60
  P22i rows + PositiveControl_Comment); FULL sweep 16480/0/0 in 8.3 min (`sweep-full-ae5eb0aae.txt`, 0 rows);
  `check_allowlist_issue_state.sh` exit 0; no generated artifact's inputs touched (regen gates not run) — lead

**Lead rulings (implementation):**
- **L1 (generator gap).** The sweep requested `range-line`/`ontype` only on target lines — never a comment line — so the
  plan's "the generator covers position × state × twin × route" was false for the line routes (0 range-line cells
  measured on the first red run). `CommentTargets` adds the first and last indented comment-only line of every
  unparseable document (not `recovery-misindented`, whose D fails `depth` by construction). Measured: range-line pins
  1473 / 8911 (comment twin); the verifier's C16 class surfaced at scale (1,242 stems `range-line S7 moved`).
- **L2 (one defect, one bucket).** `moved` leaves a comment-only line of the putative body to `comment`.
- **L3.** Phase 1 Tasks 1 and 2 are one commit, and Phase 2 Tasks 2 and 4 are one commit: the pin is asserted in the
  regular suite, so a bucket without its measured pin, or a fix without the drain, is a red commit.
- **L4.** `KnownLimitCellsPin` S6ok range-line/ontype 162 → 198: L1's 36 new cells on the same 54 pairs (pair counts
  unchanged; nothing posted on #2274).
- **L5 (mutation (ii)'s named row).** The plan named C1 `range-line 4` red under mutation (ii); it stays green — clause 8
  refuses the forgetting builder's candidate, so the route still applies nothing. The range builder is carried by the
  four range-whole repair rows and `P22i_BothBuilders_ReturnACommentLineVerbatim` (the unchecked candidate).

## Verify-implementation (2026-10-10) — BASE 53ee2d993, HEAD ae5eb0aae, FINAL 2e0ef5601

- Auditors: completeness (items 1–11 DONE, 12–13 lead actions), regression (Lsp.Tests 21529/0/0 @ ae5eb0aae measured in a
  fresh worktree; FULL sweep 16480/0/0; no tampering; parseable formatter output identical base vs head), class cure
  (SEAM; m1–m10 + behavioural revert all red when broken, restored 21537/0 project-only), prober (1,250 cells, 0 regressions;
  2 axis changes: error kind with comment-after-error; didChange vs fresh open).
- Direction class (10), lead ruling: a candidate base refused only because of its own comment dedent now applies the
  repair its comment-free twin gets on both builds — benign (recorded in direction-record.md).
- Out of contract, pre-existing: backslash-continued unterminated short string tail re-indented; over-refusal below it →
  posted on #2274 (issuecomment-6100024807). Nothing filed new.
- Remediation: 2e0ef5601 (stray blank line from the pin deletion). #2290 closes after the push (P22h precedent: #2279 closed
  after 53ee2d993 reached dev + mainline).
