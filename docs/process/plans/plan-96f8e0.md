<!-- Verified by /verify-plan on 2026-10-06 @ 7a034b81d -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: built-first -->

# P22e — LSP formatting route parity (#2168)

> Charter: `.claude/plans/remediation-round-2026-09-20-batching.md` §3.32 (order N23, first in the queue after chore #2263).
> Contract documents applied: `docs/design/verification-contract.md` (§1–§11), `docs/design/gap-discovery-contracts.md`.
> `python-fidelity-scope.md` is not in play: no input is a Python-parity cell.

## Context

`sharpyc format` and the LSP's *Format Document* apply exactly the text `FormatterService.Format` produced and the
SPY0912 net (`FormatterService.CheckMeaningPreserved`) checked. Two other LSP routes derive their edits from
something the net never saw:

- **Format Selection** (`textDocument/rangeFormatting`) formats the whole document, the net checks THAT text, and the
  handler then replaces source line *i* with formatted line *i* by index. Any formatting that adds or removes a line
  shifts every later line: statements are duplicated or deleted and the result is applied without a check.
- **Format on type** (`textDocument/onTypeFormatting`) re-indents one line from a lexer-based indent map and applies
  the result without any check: it rewrites a string's content, and it can move a statement into another block.

Both are silent-wrong on ordinary programs (lane `silent-wrong`, `reach:program`). The class tracker is
[#2168](https://github.com/antonsynd/sharpy/issues/2168); the cure shape was ruled by the owner (R-DK, option a).
`docs/tooling/formatter.md:121` currently tells users to avoid both routes.

## Current State

Measured @ `7a034b81d` (dev, tree clean) through the built Debug LSP server over stdio (`src/Sharpy.Lsp/bin/Debug/net10.0/Sharpy.Lsp`,
built 2026-10-06 10:54, after the last `src/Sharpy.Lsp` / `src/Sharpy.Compiler` commit `15d7a122d`). Driver, cases and
raw results: `.claude/tmp/p22e-probes/` (`lspfmt.py`, `cases{1,2}.py`, `out{1,2}.json`; gitignored). Re-run with
`python3 .claude/tmp/p22e-probes/lspfmt.py .claude/tmp/p22e-probes/cases1.json` (sandbox off). "Applied" is the text
after applying the returned edits.

Documents (`\n` shown as line breaks would be; each is one string):

| id | document |
|---|---|
| D1 | `def helper() -> int:\n    return 2\ndef main():\n    x   =   helper()\n    print(x)\n` |
| D2 | `def main():\n    xs = [1,\n          2]\n    # keep me\n    y = 3\n    print(xs, y)\n` |
| D3 | `def f(): ...\ndef main():\n    x   =  1\n    print(x)\n` |
| D4 | `def main():\n    x = 1 + \\\n        2\n    y   =  3\n    print(x, y)\n` |
| D5 | `s = """\n        key: value\n"""\nprint(s)\n` |
| D5u | `def main():\n    s = """\n        key: value\n` (string not closed yet) |
| D9 | `# top\ndef a():\n    return 1  # one\n# between\ndef b():\n    # inside\n    return 2\n# tail\n` |
| D11 | `def main():\n        if False:\n                print(1)\n                print(2)\n` (8 spaces per level; parses) |
| D12 | D11 + `        x = (\n` (does not parse) |
| D13 | `def main():\n    xs = [1,  # one\n          2]\n    print(xs)\n` (a `Format` fixed point) |
| D16 | `def main():\n        x = 1\n\n        y = 2\n        print(x, y)\n` |
| D17 | `import os\nx: int = 1\n\n\n\n\n\ndef main():\n    print(x)\n` |
| D19 | `def f() -> int:\n    """\n    return 1\ndef g() -> str:\n    s = """\n        key: value\n    """\n    return s\n` (a docstring opened in `f` ABOVE an existing multi-line string: the new `"""` pairs with `g`'s opener, `g`'s string content lexes as code, `g`'s closer is open to end of input) `[ADDED: adequacy]` |
| D20f | `def main():\n    s = f"""\n        }\n        key: value\n    """\n    print(s)\n` (the lexer aborts INSIDE a literal that is closed in the text: `Unmatched '}'`) `[ADDED: adequacy]` |
| D20b | D20f with `b"""` and the line `        é` (abort: non-ASCII in a byte string, `Lexer.Literals.cs:560`) `[ADDED: adequacy]` |
| D21 | `def main():\n        a = 1\n        if a == 1:\n` (S3, 8 spaces per level; the block opener is the last line) `[ADDED: adequacy]` |

| # | route · request | document | applied @ 7a034b81d | expected |
|---|---|---|---|---|
| 1 | range lines 3..4 (main's body) | D1 | `def main():` twice, `x = helper()` and `print(x)` gone | only line 3 re-spaced |
| 2 | range lines 1..4 | D2 | `print(xs, y)` twice — the program prints twice, no diagnostic | `Format(D2)` |
| 3 | range lines 2..3 | D2 | `xs = [1,` left open, `y = 3` twice — does not parse | the `[1, 2]` join applied whole |
| 4 | range = whole document | D1 | last line `print(x)` and the final newline are lost | equals Format Document |
| 5 | range = whole document | D9 | `# tail` and one blank line lost | equals Format Document |
| 6 | range line 2 | D3 | `x   =  1` deleted | line 2 re-spaced |
| 7 | range line 3 | D4 | `y   =  3` replaced by a second `print(x, y)` | line 3 re-spaced |
| 8 | range lines 7..8 | D17 | `def main():` deleted | lines 7..8 unchanged or the blank-line hunk |
| 9 | range line 1 | D16 | `x` at 4 spaces, `y` at 8 — does not parse | no edits (the selection cannot be formatted alone) |
| 10 | range line 3 | D11 | `print(2)` moves out of `if False:` | no edits, or the whole re-indent |
| 11 | on-type line 3 | D11 | `print(2)` moves out of `if False:` — **the program prints `2`; before it printed nothing** (both run with `sharpyc run`) | no edit |
| 12 | on-type `:` line 1 | D5 | the string line loses its 8 spaces — the string's value changes | no edit |
| 13 | on-type `:` line 2 | D5u | the string line goes from 8 to 4 spaces | no edit |
| 14 | Format Document (fallback) | D5u | same line re-indented 8 → 4 | untouched |
| 15 | range line 2 (fallback) | D5u | same line re-indented 8 → 4 | untouched |
| 16 | on-type line 3 / range line 3 (fallback) | D12 | `print(2)` moves out of the `if` block | no edit |
| 17 | on-type line 2 | D13 | `2]` snapped from column 10 to column 4 on an already formatted document | no edit |
| 18 | on-type on a multi-line f-string hole line; on a backslash continuation line; on a dict-literal entry line | (cases1: d8, d7, d6) | each snapped to the block's level | no edit |
| 19 | Format Document (fallback); range line 5 (fallback); on-type line 5 | D19 | all three: `        key: value` → `    key: value` — `g`'s string loses 4 spaces of content; when the user closes the docstring the program's string has changed | no edit |
| 20 | Format Document (fallback); on-type line 3 | D20f, D20b | every interior line of the literal re-indented 8 → 4 (`}` / `é`, `key: value`) | no edit |
| 21 | on-type line 2; range line 2 (fallback) | D21 | `if a == 1:` moved 8 → 4 while `a = 1` stays at 8. With the body typed, the unedited document runs; the edited one is `SPY0014 Indentation mismatch` (both through `sharpyc run`). The indent map gives the line level 1 before AND after (Format Document's fallback writes `    if a:` for both texts) | no edit |
| C1 | Format Document | D1, D2, D3, D4, D9, D11, D13, CRLF D1 | equals `sharpyc format` | control — holds |
| C2 | range = whole | D13 | no edits | control — holds |

Cells 1–5 and 12 restate the issue's table (its rows 1–3, 6 and 7; its rows 4 and 5 are agent probes of the same
mechanism). Cells 6–8 are the charter's document axis (a split `def f(): ...`, a joined backslash continuation,
removed blank lines). Cells 9–11 and 13–18 are siblings measured here: the issue's table has no over-indented
document, no unterminated literal, no fallback route and no formatted document.

`[ADDED: adequacy — /verify-plan @ 7a034b81d]` Cells 19–21 were measured by the verifier through the same driver
(cases and raw output: `.claude/tmp/p22e-probes/cases3.py`, `cases3.json`, `out3.json`; all 44 recorded cases of
`out1.json` / `out2.json` re-ran byte-identical first). They are members of this plan's contract that the plan as
written did not reach: cells 19–20 are the state "the lexer lost a literal's token" by a route other than end of
input (decision 7 as first written protected only `[last unmatched opener, end of source]`, and in D19 the damaged
line lies BEFORE that opener); cell 21 is a partial re-indent that decision 8's level clause accepts, because
`IndentationService.cs:95-98` gives a dedent to an unseen width a level of its own. D19 is the ordinary state while
typing a docstring in any file that has a later triple-quoted string; the cut-only S4 state is the special case
with none.

Mechanisms (read @ 7a034b81d):

- `src/Sharpy.Lsp/Handlers/RangeFormattingHandler.cs:62-93` — the per-index line mapping; the comment at `:70-73`
  ("Added/removed lines … are intentionally skipped") describes behaviour the loop does not have.
- `src/Sharpy.Lsp/Handlers/OnTypeFormattingHandler.cs:47-76` — `IndentationService.BuildIndentMap` gives every line
  a level; a line with no entry defaults to 0; a continuation line gets its block's level
  (`IndentationService.cs:103-104`); nothing checks the result. `request.Character` is ignored, so a request is
  fully described by (document, line).
- The indent-only fallbacks (`FormattingFallback.ReindentDocument`, `RangeFormattingHandler.ComputeIndentOnlyRangeEdits`)
  guard literal lines with `IndentationService.FindMultiLineStringLines` → `LiteralSpans.Of(tokens)`. An unterminated
  triple-quoted string yields **no token** (`Lexer.Literals.cs:114` throws at end of input), so the guard is blind in
  the state every user is in while typing a docstring — `editors/vscode/language-configuration.json` has no `"""`
  auto-closing pair.
- `FormatterService.CheckMeaningPreserved` is `internal`; `Sharpy.Lsp` and `Sharpy.Lsp.Tests` both have
  `InternalsVisibleTo` on `Sharpy.Compiler`.
- **Two existing tests pin applied texts that do not parse**: `RangeFormattingTests.MultipleLines_ReformattedAsync`
  (applied text → `SPY0100 Unexpected token: Indent`, source parses) and `RangeFormattingTests.LastLine_ReformattedAsync`
  (applied text → `SPY0014 Indentation mismatch`, source parses). They are cells of this defect, not expectations to
  preserve (see Phase 2 Task 3).

## Scope

- **Group:** P22e — LSP formatting route parity (one non-chore group; chores riding along: none).
- **Alone:** no — the CLI's printed output and every emitted C# are untouched (no snapshot, no `.expected` change).
  Nothing else shares the gate anyway.
- **Gates:** 1.
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`); the lead files
  by class (contract §10); trackers are named at the end of this plan.
- **Override:** none.
- **Matrix axes added by measurement (inside the charter's contract, not a widening):** *document state* (formatted /
  unformatted / does not parse / does not parse with an open literal) and the *wide-indent* document shape. The
  charter's matrix lists "on-type per typed line" × "a triple-quoted string with an indented line"; a typed line is
  by nature a mid-edit state, and `formatter.md:123` already promises that the fallback "leaves everything else
  alone, including the inside of strings".
- `[ADDED: missing step]` **Budgets:** verify cost = one generator (4 twins × 5 document states), one whole-solution
  gate, one control run at `7a034b81d` only if the gate is red. Sibling-cell prober = this matrix plus at most two
  axis changes (contract §10); `/verify-plan` spent one (document state gained S5).

## Defect Class

- **Violated contract:** every formatting route applies a text that was checked AS APPLIED — the SPY0912 net when the
  document parses, the indent-only invariant when it does not — or applies no edits. Uniform across route × document.
- **Meta-class:** *Position/route missing a shared check* (verification-contract §1) → **standard cure:** move the
  check to one seam every route calls, plus a route × document sweep so a new route cannot skip it. The effect is
  the *silent wrong behavior* meta-class, so the sweep lands FIRST.
- **Known member cells:** #2168 (cells 1–8, 12 above).
- **Sibling cells this plan must also cover** — the matrix:
  - **route** {CLI (`FormatterService.Format`, what `FormatRunner.FormatFile` calls), LSP full, LSP range, LSP on-type}
  - **range kind** (range route) {whole document; one top-level statement, selected as whole lines `(a,0)–(b+1,0)`;
    one block body, selected `(a,0)–(b,len)`; one line}
  - **document state** {S1 formatted `F = Format(P)`; S2 unformatted, parses; S3 does not parse, literals closed —
    `P` cut after a block-opener line; S4 does not parse, literal open — `P` cut after the first interior line of a
    multi-line literal; **S5 does not parse, a literal's token is lost with text after it** `[ADDED: adequacy]` —
    `P` with one unmatched triple-quote line inserted as its first line (every later triple-quoted literal of that
    quote character flips: content lexes as code — cell 19), plus the mid-literal lexer abort (cell 20), which the
    generator cannot produce from an arbitrary fixture and is pinned by hand}
  - **document shape (twin)** {identity; comment-injected (`FormatterTwins.CommentInjected` — comments before, inside
    and after every range, and verbatim constructs); wide-indent (every level written with 8 spaces — the shape in
    which a partial re-indent changes structure); CRLF}
  - ≥ 4 axes × ≥ 3 values. Cells 9–18 above are members found off the issue's repro list; cells 19–21 were added
    by `/verify-plan` `[ADDED: adequacy]`.
- **Lane / reach:** `silent-wrong` · `program`; **fidelity-scope row:** N/A (tooling, not a Python surface).
- **Standing harness that should have caught it:** `FormatterMeaningPreservationSweepTests` — it sweeps the CLI
  route only. None covers the LSP routes; Phase 1 adds one.
- **Generator:** **REQUIRED FIRST** — residue group (P22b → P22e) and a ≥2-axis × ≥3-value matrix. Phase 1 builds
  `FormattingRouteParitySweepTests` over the fixture corpus through the real handlers, with an allowlist ratchet;
  Phases 2–3 drain it to empty. Axis the generator cannot enumerate: the wire protocol (a stdio server per cell is
  too slow) — one E2E cell per route covers it (Phase 4 Task 3).
- **Owner rulings:** **R-DK (option a)** —
  [#2168 comment 2026-10-02](https://github.com/antonsynd/sharpy/issues/2168#issuecomment-5944918395) and charter §3.32 / ruling table:
  range edits are the minimal line-diff hunks of the checked whole-document output that intersect the range, applied
  to a copy, and `CheckMeaningPreserved` runs on THAT applied text at one `FormatterService` seam every LSP route
  calls; a hunk that lands inside a multi-line statement fails the net → no edits; on-type gains the #2062
  string-literal line guard. Rejected: (b) one whole-document edit regardless of range — **stays the in-group
  fallback if measurement shows (a) refusing most real selections; not a new decision**; (c) capabilities off.
  Do not re-ask.

## Adversarial Review (pre-mortem)

- **Alternative root cause:** "the formatter's output is wrong, the routes are fine". Discriminating probe: control
  C1 — Format Document equals `sharpyc format` on every probed document, and the range route's edits on the same
  documents differ. The defect is in the mapping from `Format(P)` to edits, not in `Format`.
- **Alternative layer:** the layer ladder (CLAUDE.md › Core & Stdlib Conventions) is not in play — no Python
  behaviour on a Core/Stdlib type, no `Semantic/` or `CodeGen/` change, no name-keyed rule. The layer question here
  is *client vs server*: an editor could diff a whole-document edit itself, but LSP gives the server the range and
  expects edits confined to it; (b) was rejected for that reason.
- **How the fix could be inert:**
  1. The seam exists and a route does not call it (the full-document fallback, or a handler added later). Guard: the
     sweep drives every handler found by reflection, and its own oracles re-check the applied text independently of
     whether the handler checked it.
  2. The net runs on the whole-document output again instead of the applied text (today's bug, restated). Guard:
     the mutation in Phase 2 Task 2.
  3. The on-type literal guard reads `LiteralSpans.Of(tokens)` only — still blind to an open literal. Guard: the S4
     cells, whose ground truth comes from the parent fixture's clean lex, not from the lexer run on the cut document.
     `[CORRECTED: the S4 cut has no text after the open literal, so it cannot show a guard that protects only the
     tail — cell 19]` The S5 cells carry that: the parent fixture's literal lines lie BEFORE the last unmatched
     opener of the damaged document.
  4. A `local` oracle computed with the same selection code as production would agree with any selection bug. The
     sweep restates the selection predicate itself (three lines) and holds `LineDiff` to an independent property
     (`Apply(P, Hunks(P, F)) == F`).
- **Before/after by direction:** every behaviour change is "edits → different edits" or "edits → no edits". For each
  request that newly returns NO edits, the commit body classifies the prior applied text: *did not parse* /
  *changed the program* / *changed a string* (a fix) versus *parsed to the same program* (a restriction — must be
  justified by the `fixedPoint` oracle or it is a regression). The sweep's `refusedWork` census is the list.
- **Blast radius:** `Sharpy.Compiler` (`Formatting/`, one recorded lexer fact, `Lexer/LiteralSpans.cs`),
  `Sharpy.Lsp` (three handlers, `IndentationService`, `FormattingFallback`), `Sharpy.TestInfrastructure`
  (`FormatterTwins` moves in), `Sharpy.Compiler.Tests` (usings; `FormatterServiceTests`), `Sharpy.Lsp.Tests`.
  The lexer change records a fact and must not change a token or a diagnostic — the whole suite is the check.
  No `Semantic/`/`CodeGen/` change, so Stdlib.Tests, Cli.Tests and the three CI GapDiscovery sweeps are reached only
  through the shared lexer; the gate is still the whole solution. `Sharpy.Lsp.Tests` runs under a 3 GB
  `HeapHardLimit` and CI runs it as one step (`dotnet10.yml:190`) — the sweep's budget is a task (Phase 1 Task 3).
- **Generated artifacts touched:** none (no spy-stdlib C#, spy-test C#, stdlib docs or oracle ledger). Record
  "regen: not reached — no Semantic/CodeGen change" in the first commit body; `/push` still runs the staleness gates.

## Design Decisions

1. **Range edits are hunks of the checked output, selected by the range, checked as applied (R-DK a).**
   `FormatterService.FormatRange(source, firstLine, lastLine, options)`:
   format the whole document (net-checked as today) → `LineDiff.Hunks(source, formatted)` → keep the hunks the
   selection touches → apply them to a copy → `CheckMeaningPreserved(source, sourceAst, applied)` → return the hunks,
   or none plus the SPY0912 diagnostic. No retry with a wider or narrower selection: a refused selection gets no
   edits (R-DK). `Format` and `FormatRange` share one private core so the source is lexed and parsed once.
2. **Selection rule.** Selected lines `[s, e]`: `s = range.Start.Line`; `e = range.End.Line`, minus one when
   `End.Character == 0` and `End.Line > Start.Line` (an editor's whole-line selection ends at column 0 of the next
   line); when `e` reaches the last non-empty line and the document ends with a line break, the empty final line is
   selected too. A replace/delete hunk over source lines `[a, b)` is selected iff `a ≤ e ∧ b − 1 ≥ s`. A pure
   insertion before line `a` is selected iff both neighbours that exist are selected: `(a == 0 ∨ a − 1 ≥ s) ∧
   (a == n ∨ a ≤ e)`. `[CORRECTED at /implement-plan by the sweep lane: the formula disagrees with its own prose at
   the edges — at `a == 0` it holds for every selection (`0 ≤ e`), and at `a == n` whenever `s ≤ n−1`. The rule is
   the prose: `(a == 0 ∨ s ≤ a−1 ≤ e) ∧ (a == n ∨ s ≤ a ≤ e)`; the sweep and `FormatRange` both use it. "e reaches
   the last non-empty line" reads as `e ≥ n−2` when the document ends with a line break (`LineDiff.Split`'s final
   `""` line is index `n−1`).]` So "format main's body" does not add the two blank lines above `def main():` (cell 1), and a
   whole-document selection selects every hunk.
   `[CORRECTED: the missing final line break was not modelled]` A document with no final line break whose
   `Format` adds one differs by an empty final line: an insertion at `a == n`, selected by the rule above whenever
   the last line is selected, and merged into a replacement that ends at `n` (hunks are non-adjacent). So a
   whole-document selection adds the final line break — the `whole` oracle and cell 4 require it — and three more
   existing tests observe it (Phase 2 Task 3).
3. **Lines are content; line endings are never edited by the range route.** `LineDiff` splits on `\r\n`, `\n` and a
   lone `\r` (the lexer's and `LiteralSpans`' definition) and compares line CONTENT. A replaced line keeps its own
   terminator; an inserted line break is the document's own (its first line break; `\n` when it has none). A CRLF
   document therefore stays CRLF under Format Selection, and only changed lines are touched. Format Document keeps
   its documented behaviour (the whole text, `\n`). Without this, every line of a CRLF document differs from
   `Format(P)`, the whole file is one hunk, and any selection formats the entire document — option (b) by accident.
4. **Edit shape.** One `TextEdit` per selected hunk, non-overlapping, in document order. A 1:1 line replacement keeps
   today's shape — `Range (i,0)–(i,len(content))`, `NewText` = the bare line — because existing assertions read it.
   Replace `[a,b)` with lines `N`: `(a,0)–(b−1,len)`, `join(N, eol)`. Delete: `(a,0)–(b,0)`, `""` (through the last
   line: from the end of line `a−1`). Insert before `a`: `(a,0)–(a,0)`, `join(N, eol) + eol` (at end of document:
   at the end of the last line, `eol + join(N, eol)`). The implementer may choose an equivalent shape; the judges are
   the driver's strict applier and the property `strictApply(ToTextEdits(hunks)) == LineDiff.Apply(hunks)`.
5. **`LineDiff` is a new internal type; `FormatRunner`'s private LCS stays.** `FormatRunner.ComputeUnifiedDiff`
   prints `--diff`; routing it through a diff that trims the common prefix and suffix can change which of two equal
   lines is shown as removed, which is printed output — and a group that changes printed output lands alone (§11).
   `LineDiff` trims the common prefix and suffix, runs an LCS on the middle, and when the middle exceeds a stated
   bound (propose 4,000,000 cells) returns the middle as ONE hunk — correct, coarser, still checked as applied.
   The duplication is a ledger note, not work for this plan.
6. **On-type edits only a line that starts a logical line, outside any literal, and only after the check.** In order:
   blank line → none (as today); the document's literal state is unknown (decision 7) → none; line starts inside a
   literal → none; line does not start a logical
   line (bracket or backslash continuation) → none; desired indent equals existing → none; otherwise apply the
   re-indent to a copy and check it (decision 8). The continuation rule is what makes on-type a no-op on a formatted
   document (cell 17, the `fixedPoint` oracle): the indent map's level means "block level", which is only a
   statement's first line's indentation. The rule is derived from the lexer's token stream
   (`IndentationService`'s `logicalStart`), not from a name or a keyword list.
7. **When the lexer loses a literal that can span lines, no indent-only route edits the document.**
   `[CORRECTED: was "An open literal runs to end of input" — the lexer records where an unterminated triple-quoted
   literal opened and `LiteralSpans` gains `[thatOffset, end of source]`. Measured @ 7a034b81d (cells 19–20): that
   span protects only what FOLLOWS the last unmatched opener. In D19 the damaged line precedes it, and in D20 the
   lexer never reports an unterminated literal at the damaged lines at all — it aborts inside one and resumes on
   the next line as code. Once one multi-line literal's token is missing, the lexer's literal map of the rest of
   the document is a guess: every later delimiter may be paired the other way round.]`
   The lexer records ONE fact, `Lexer.LiteralStateUnknown` (`bool`), set at its single abort funnel
   (`ReportError`, `src/Sharpy.Compiler/Lexer/Lexer.cs:26` — all 75 `throw ReportError` sites pass through it) when the abort happens while
   reading a literal that can span lines: any triple-quoted read (plain and `d"""` via `ReadTripleQuotedString`,
   `r"""`, `dr"""`, `b"""`), or an f-/t-string context that is triple-quoted or has already crossed a line break
   (a multi-line replacement field). A seam, not a site list: a new read path cannot skip it.
   `IndentationService.BuildIndentMap` returns the fact; the funnel (decision 8) returns NO edits for on-type and
   for both indent-only fallbacks when it is set. `LiteralSpans` does not change. A single-quoted plain, raw or
   byte literal ends at its line and sets nothing, so an unfinished `x = "abc` does not switch the fallback off.
   The fact is diagnostic-neutral: no token, message, code or position changes.
   `FormatterService.StripTrailingWhitespace` is unaffected (it strips nothing when the lex has errors).
   - **Options considered** (all two-way doors; none needs a new owner ruling — the contract already says "or
     applies no edits", and `formatter.md:123` promises the inside of strings is left alone):
     **(a, taken)** no indent-only edit anywhere in such a document. Sound and one clause.
     Cost, by direction: on a cut-only S4 document the lines above the open literal used to be re-indented
     correctly and now are not — a restriction, justified because the lexer cannot tell S4 from D19, where the same
     edit changes a string. **(b, the plan's first draft)** protect `[last unmatched opener, end]` only: keeps that
     tidy-up, leaves cells 19–20 silent-wrong. **(c)** freeze only from the first multi-line-capable literal opener
     of the document onward: also sound, keeps tidy-up of the lines above it, costs a second lexer fact; it stacks
     on (a) later without undoing it.
   - Rung: a recorded lexer fact read by the LSP; no name-keyed rule.
8. **One funnel in the LSP, two checks behind it.** `Sharpy.Lsp/Handlers/FormattingEdits.cs`:
   every handler returns only what the funnel returns. For hunks from `FormatRange` the check already ran inside
   `FormatterService`. For an indent-only candidate (on-type; the full and range fallbacks) the funnel builds the
   applied text and asks `FormatterService.CheckApplied(source, applied, out sourceParses)`: when the source
   parses, the SPY0912 net decides; when it does not, `FormattingFallback.IndentOnlyPreserved(source, applied)`
   decides — same number of lines; each line's content after its leading whitespace unchanged (a whitespace-only
   line may become empty); literal lines byte-identical; and the indent map's level of every logical-line-start line
   unchanged. A failed check → no edits. The last clause is what refuses cell 16: re-indenting ONE line of an
   8-space block changes that line's level relative to its neighbours.
   `[CORRECTED: the level clause alone accepts cell 21]` A dedent that lands BETWEEN two widths of the stack keeps
   its level in the indent map (`IndentationService.cs:95-98` opens a block at the unseen width — measured: the map
   gives D21's `if` line level 1 before and after the edit) while the lexer calls the result `SPY0014`. So
   `IndentOnlyPreserved` has a fifth clause: **the applied text has no more lexer indentation diagnostics
   (`SPY0013`, `SPY0014`) than the source.** Measured @ 7a034b81d with `sharpyc emit diagnostics` (before → after
   the partial re-indent): a 2-space body, the document the fallback exists to repair, 2 → 1
   (`def main():\n  x: int = 1\n  print(x)\n`, line 1 re-indented) and 1 → 0
   (`ParseError_TwoSpaceDocument_TabSize2_FallbackWritesFourSpacesAsync`'s source);
   `SingleMisIndentedLine_FixedAsync`'s source 1 → 0; D21 0 → 1. So the existing fallback tests and the E2E
   `RangeFormatting_NormalizesIndentationInRange` keep their edits and D21 is refused. And when decision 7's fact
   is set the funnel returns no edits before any of this runs.
   `[ADDED at /implement-plan after P3T2, measured @ aef9c0816 — 100 sweep rows still failed after the five clauses]`
   **A sixth clause: the lexer's block depth of every logical-line-start line whose source width is on its enclosing
   stack is unchanged in the applied text** (the lexer's width rule — wider pushes, narrower pops to an equal width; a
   line at a width on no enclosing level is the misindentation the fallback repairs and is exempt). Clause 4 compares
   the indent map with itself, and the map is wrong in two measured places, so it accepts both: (i) the map opens a
   block only after a line ending in `:`, so a property-observer block (`experimental/property_observers_*`,
   `Formatting/member_line_comments`) is re-indented one level too shallow — S3 `depth`; (ii) in
   `strings/d_string_trailing_newline` an inserted `"""` pairs with a `"""` inside a COMMENT, every later quote
   re-pairs with no lexer error (so decision 7's fact stays false — the parity premise fails when a triple quote sits
   in a comment or a short string), and the fallback moves `main`'s body to column 0 — S5 `depth`. The indent-only
   routes may change widths, never the block structure the lexer reads. And the decision-7 seam missed one f-string
   shape: `ReportUnclosedField` resets `_line` to the bracket line before `ReportError` asks whether the context
   crossed a line, so a single-quoted f-string whose hole crossed a line break with its innermost bracket on the
   start line sets nothing (`fstrings/multiline_hole_2022` S4 `literal`) — fixed at the seam.
   `[DECIDED by the lead at /implement-plan, two-way door]` The sixth clause cannot tell an unexpected indent from a
   colon-less block opener (both are "a deeper line after a line that does not end in `:`"), so it also refuses the
   fallback's repair of an over-indented line in an unparseable document. Options: **(a, taken)** accept — the
   indent-only routes never re-nest; such a document gets no edits (by direction: "worked when the over-indent was
   a typo → no edits; silent-wrong when it was a property block → no edits"); **(b)** a structure-preserving map for
   the fallback only (levels = lexer depth), keeping width normalization of over-indented lines without re-nesting
   — a second map beside on-type's colon-based one; stacks on (a) later (ledger note for #2168). Implementation
   detail accepted from the lane: a line the lexer drops (SPY0014 off-stack, SPY0013 width not a multiple of 4, tab)
   is exempt but SPY0013/tab lines still move the width stack, so a nested 2-space document's repair is kept. The
   lexer seam keeps the detection line when the innermost open bracket is inside the hole (implicit line joining),
   and moves to the field's `{` otherwise — lane C's single-line control stays green.
   `[DECIDED by the lead at /implement-plan (P3T3), two-way door]` On-type gets no exemption from the sixth clause
   for the requested line. Measured @ d97821ff2: with the exemption, 16 `ontype` S3 `depth` cells still failed — on
   the last line of a cut ending at a property-observer header, on-type moved `before_set(...)`/`after_set(...)` 8 → 4,
   out of the property block. All 11 `[CORRECTED by the P3T3 lane: was 12]` pre-existing `OnTypeFormattingTests` normalize a width at constant depth, and a
   body-width `else:` was never dedented at HEAD (no keyword rule), so the exemption's only beneficiaries were an
   over-indented `else:` and an unexpected indent — the same shapes ruling (a) above gives up for the fallbacks.
   So on-type, like the fallbacks, re-indents and never re-nests.
9. **The generator lives in `Sharpy.Lsp.Tests`; `FormatterTwins` moves to `Sharpy.TestInfrastructure`.** The handlers
   are `internal` to `Sharpy.Lsp` (IVT: `Sharpy.Lsp.Tests` only), P47 reuses the handler driver for code actions
   (charter §3.42), and `Sharpy.Compiler.Tests` is the gate's long pole. `Sharpy.TestInfrastructure` already has IVT
   on `Sharpy.Compiler` and is referenced by both test projects.
10. **The fallback's treatment of continuation lines on a document that does not parse is unchanged** (it sets them
    to their block's level; layout only, and only where no formatter can run). It passes every S3/S4 oracle. Ledger
    note for #2168, lane `tooling`.

No new diagnostic code (SPY0912 is reused, never surfaced to the client — the handlers return no edits, as today).
No `SemanticInfo` dictionary, no `CodeGenInfo` member.

## Implementation

Order: test infrastructure → generator (red first) → Compiler `Formatting/` → LSP range → Lexer fact → LSP on-type
and fallbacks → docs. Every task is one commit. Edit loop: `.claude/scripts/dotnet-serialized test
src/Sharpy.Lsp.Tests --filter "FullyQualifiedName~Formatting"` plus `src/Sharpy.Compiler.Tests --filter
"FullyQualifiedName~Formatter|FullyQualifiedName~LiteralSpans"`; a filtered run is never the gate.

### Phase 1: The route-parity generator, red first

**Goal:** a standing sweep that drives every formatting route through the real handlers over the corpus and fails
on every cell of the table above — with no production change.

**Acceptance (class-level, measurable):**
- Sweep that goes green: `FormattingRouteParitySweepTests` green @ sha WITH its populated allowlist; the commit body
  records the first run's failing-row count per route × state × bucket, the cells per state and twin, and the wall
  time.
- Guard mutation: (a) break a route that is green today — in `FormattingHandler.cs` drop the last line of
  `NewText` → `whole` red on non-allowlisted LSP-full cells; restore → green. `[CORRECTED: the plan's first choice,
  the edit's end at `lastCol - 1`, is `-1` on every document that ends with a line break (`lastCol` is 0 on the
  empty final line), so it reads as the instrument bucket `edits`, not as `whole`]` (b) delete one allowlist row → red; add a row for a passing cell → red (drain-on-fix armed). (c) each
  bucket's positive control (below) is red on its hand-damaged text and green on the clean one.
- Allowlist entries that drain: none yet — this phase populates `formatting-route-parity-allowlist.txt`.
- Execution evidence the close-out will cite: the census line and the per-bucket first-run table @ sha.

#### Tasks

1. **Move `FormatterTwins` to the shared test infrastructure; add the wide-indent and CRLF twins** —
   `src/Sharpy.Compiler.Tests/Conformance/FormatterTwins.cs` → `src/Sharpy.TestInfrastructure/Formatting/FormatterTwins.cs`
   (`git mv`; namespace `Sharpy.TestInfrastructure.Formatting`; add the `using` to its five consumers:
   `FormatterMeaningPreservationSweepTests`, `ContextualKeywordEscapeSweepTests`, `FormatterNameFidelityMatrixTests`,
   `FormatterCommentFidelityMatrixTests`, `FormatterTwinsTests`).
   - Pure move first: the existing formatter sweeps pass with identical counts before and after (record both).
   - `FormatterTwins.WideIndented(source)`: every line that starts a logical line or is a comment-only line gets its
     leading whitespace doubled; continuation lines get the same number of added spaces as their statement's first
     line; a line that starts inside a literal is untouched (`LiteralSpans`). `FormatterTwins.Crlf(source)`: every
     line break becomes `\r\n`.
   - Instrument checks in `FormatterTwinsTests`: both twins parse to the source's AST
     (`AstNormalizer` + `StructuralEqualityComparer`); `Format(Wide(P)) == Format(P)`; a positive control where the
     wide twin differs from the source; literal lines byte-identical.
   - Mutation: make `WideIndented` double a literal-interior line → the AST instrument check goes red on a fixture
     with an indented triple-quoted string.
   - Commit: `test(infra): FormatterTwins moves to Sharpy.TestInfrastructure and gains the wide-indent and CRLF twins (refs #2168)`

2. **The handler driver** — `src/Sharpy.Lsp.Tests/Conformance/LspFormattingDriver.cs` (+ `LspFormattingDriverTests.cs`)
   - One real `SharpyWorkspace` and the three real handlers (`SharpyFormattingHandler`, `SharpyRangeFormattingHandler`,
     `SharpyOnTypeFormattingHandler`), as `RangeFormattingTests` builds them. Per request: `OpenDocument` → `Handle`
     → `CloseDocument`, synchronously — `OpenDocument` schedules a debounced semantic analysis and `CloseDocument`
     disposes its timer; a sweep that leaves documents open analyses every one of them.
   - `Full(text)`, `Range(text, LspRange)`, `OnType(text, line)` return `(IReadOnlyList<TextEdit> Edits, string Applied)`.
   - **Strict applier** (LSP semantics): positions are (line, UTF-16 character) over lines ended by `\r\n`, `\n` or
     `\r`; it THROWS on an edit outside the document, on `start > end`, and on overlapping edits; edits apply
     against the original text. `[k:0-k:0]` on the empty final line is legal; a character past a line's content is not.
   - `Routes`: the roster the sweep iterates — and a totality fact: every non-abstract type in the `Sharpy.Lsp`
     assembly deriving from `DocumentFormattingHandlerBase`, `DocumentRangeFormattingHandlerBase` or
     `DocumentOnTypeFormattingHandlerBase` is in the roster, and the count is the literal `3`.
   - Driver tests: the applier on insert / delete / replace / end-of-document / CRLF; it throws on an overlap
     (positive control for the instrument).
   - Mutation: make the applier sort and apply sequentially against the MOVING text → the multi-edit unit test red.
   - Written for reuse: P47 drives code actions through the same class (charter §3.42) — keep the route methods and
     the applier independent of the formatting oracles.
   - Commit: `test(lsp): a handler driver applies formatting edits strictly, as an LSP client must (refs #2168)`

3. **The sweep, its oracles, census and allowlist** — `src/Sharpy.Lsp.Tests/Conformance/FormattingRouteParitySweepTests.cs`,
   `src/Sharpy.Lsp.Tests/Conformance/formatting-route-parity-allowlist.txt`
   - **Corpus:** every single-file fixture under `FixtureRoots.CompilerTests.Path` that lexes and parses the way
     `FormatterService.Format` reads it (the P22b sweep's rule; `FixtureDiscoveryHelper.DiscoverFixtures(…, includeSkipped: true)`).
     One `[Theory]` per twin {`identity`, `comment`, `wide`, `crlf`}, `MemberData` = corpus stems. The backtick and
     line-trailing twins are not swept: their subject is the unparser, which the CLI-route sweep owns; this sweep's
     subject is the mapping from `Format(P)` to edits. Say so in the class doc.
   - **Cells per (stem, twin)** with `Q` the twin, `F = Format(Q)` (skip the F-dependent cells and count them when
     `Format(Q)` is declined):
     - S2 on `Q`: `cli`, `full`, `range-whole`, `range-statement` (each `Module.Body` entry, whole-line selection),
       `range-block` (each lexer `Indent…Dedent` line span), `range-line` (each non-blank line), `ontype` (each
       non-blank line).
     - S1 on `F`: `full`, `range-whole`, `range-statement`, `range-block`, `ontype`.
     - S3 on each `Q` cut after a line whose last code token is `Colon` at bracket depth 0 (document = lines up to
       it + a line break): `full`, `range-whole`, `range-line` (the last line), `ontype` (the last line).
     - S4 on each `Q` cut after the first interior line of each literal that spans ≥ 2 lines: the same four.
     - `[ADDED: adequacy]` S5 on each `Q` that has a literal spanning ≥ 2 lines: `Q` with one line holding only the
       triple quote of its first such literal's quote character inserted as line 0. Cells: `full`, `range-whole`,
       and `range-line` and `ontype` on every line that starts inside a literal in `Q` (shifted by one). Ground
       truth is `Q`'s clean lex, as for S4.
     - The state is OBSERVED (does the cut document parse?), never assumed; a cut that parses is scored as S2.
     - `[ADDED: missing step]` S1 documents are de-duplicated by content before they are swept: `Format(Wide(P))`
       equals `Format(P)` by Task 1's instrument check, so the `wide` twin's S1 cells are the `identity` twin's.
   - **Oracles (buckets).** `T` is the applied text.
     - `edits` (instrument) — the strict applier threw.
     - Parseable document `D` (S1, S2):
       - `net` — `FormatterService.CheckMeaningPreserved(D, ast(D), T) != null`, called by the TEST on the applied
         text, whatever the handler did.
       - `whole` — `cli`, `full`, `range-whole`: `T != Format(D)` (for `range-whole` on the `crlf` twin: equal
         after normalizing line breaks, and `T` contains no bare `\n`).
       - `local` — range kinds: `T ∉ { D, LineDiff.Apply(D, selected) }` where `selected` applies decision 2's
         predicate — restated in the test, not called from production — to `LineDiff.Hunks(D, Format(D))`.
         Until Phase 2 Task 1 lands `LineDiff`, the sweep uses a test-local reference diff (LCS over lines); Task 1
         replaces it and keeps the reference as the property test's comparison.
       - `fixedPoint` — S1 only: `T != F` for any route.
       - `ontypeShape` — `ontype`: `T` differs from `D` anywhere other than the leading whitespace of the requested line.
     - Unparseable document `D` (S3, S4, S5), ground truth from the PARENT `Q`'s clean lex. A line is its content
       without its terminator (decision 3's definition) `[CORRECTED: unstated — the full fallback joins with `\n`,
       so on the `crlf` twin a comparison that included terminators would be red on every line]`:
       - `content` — line count differs, or a line's content after leading whitespace differs (a whitespace-only
         line may become empty).
       - `literal` — a line that starts inside a literal in `Q` is not byte-identical in `T`.
       - `depth` — for a line that starts a logical line in `Q`: its block depth in `T` (test-side width stack over
         `T`'s leading whitespace) differs from its depth in `Q` (count of `Indent` minus `Dedent` tokens before it).
         `[CORRECTED: the stack was unspecified exactly where it decides cell 21]` The test-side stack is the
         LEXER's rule, not `IndentationService`'s: a wider line pushes; a narrower line pops to an equal width; a
         width that is on no enclosing level is a `depth` failure, never a level of its own. A stack that copied
         `IndentationService.cs:95-98` would agree with production on cell 21 and pass it.
     - `refusedWork` — **census, not a failure**: a range cell with a non-empty `selected` and `T == D`.
   - **Positive controls** (one `[Fact]` per bucket, on hand-written texts — the rows of the Current State table):
     cell 2's applied text → `net` (the program differs) ; cell 1's → `net`; cell 4's → `whole`; a D1 range edit that
     also re-spaces a line outside the selection → `local`; cell 17's → `fixedPoint`; an on-type result that also
     strips a trailing space → `ontypeShape`; cell 13's → `literal`; cell 19's → `literal`; cell 16's → `depth`;
     cell 21's → `depth` (the dedent between widths); a fallback result that
     drops a character → `content`; and each bucket NOT firing on the clean text of the same cell.
   - **Census `[Fact]`:** prints cells per route × state × twin, `refusedWork` per range kind and twin, the count of
     F-declined stems, and the S4 and S5 cells per literal kind — one per lexer read path `[CORRECTED: the list
     was plain, raw, byte, f-, t-string; the lexer has three more triple-quoted paths and the corpus uses all
     three]`: plain, `d"""` (both `ReadTripleQuotedString`), `r"""` (`ReadRawString`), `dr"""`
     (`ReadDedentedRawString`), `b"""` (`ReadTripleQuotedByteString`), `f"""`, `t"""`, `df"""`. It also prints, per
     route, the S3 cells whose applied text DIFFERS from the document, with a floor of ≥ 1 per route
     `[ADDED: missing step]` — the positive control for decision 7: "no edits" passes every S4/S5 oracle, so a
     `LiteralStateUnknown` that is always set would be green everywhere else. Floors: every route ×
     state has ≥ 1 cell; S4 and S5 have ≥ 1 cell for each literal kind and the backslash-continuation and
     bracket-continuation shapes have ≥ 1 `ontype` cell — **where the corpus has none, add a fixture with its
     `.expected` under `TestFixtures/Formatting/` (via `/add-test-fixture`), never a hand cell**. Corpus grep
     @ 7a034b81d: `"""` in 24 fixtures, `f"""` 6, `t"""` 2, `r"""` 1, `b"""` 1, a trailing backslash in 3, CRLF 0
     (hence the twin). `[CORRECTED: with a prefix boundary (`(^|[^A-Za-z0-9_])r"""`) plain `r"""` is in 0 fixtures —
     the 1 is the `dr"""` fixture; `d"""` 3, `dr"""` 1, `df"""` 4, `b"""` 1, `t"""` 2. The plain-raw floor therefore
     needs a new fixture under `TestFixtures/Formatting/`]` Allowlist rows name corpus stems, known twins, routes and buckets only.
   - **Allowlist:** rows `stem twin route state bucket # #2168 reason` — one row per failing (stem, twin, route,
     state, bucket), covering every cell of that route on that stem; a row whose bucket no longer fails on any cell
     fails the test (drain on fix). Populate from the first run (`FMTROUTE-ROW` lines in the test output, the P22b
     sweep's mechanism). Expect thousands of rows (P22b's allowlist peaked above 11,000 and drained to 0); the file
     name contains `allowlist`, so `build_tools/check_allowlist_issue_state.sh` scans it. `cli` and `full` cells on
     S1/S2 must need NO rows — a row there is a finding outside this plan's cure; stop and report it to the lead.
   - **Budget:** measure the sweep's wall time in `Sharpy.Lsp.Tests` (its `xunit.runner.json` runs 8 threads) and
     record it. Target ≤ 90 s added. If over: `range-line` and `ontype` cells — the two kinds proportional to line
     count — are sampled per fixture by a stable hash (the `StableBucket` precedent in `FrontEndParityTests`), the
     stride is printed by the census, and `SHARPY_ROUTE_SWEEP_FULL=1` runs every cell; the lead runs the full mode
     once per phase and records its counts. `whole`, `statement`, `block`, S3, S4 and S5 cells are never sampled.
     Hold no applied text beyond its cell (3 GB heap limit).
     `[CORRECTED: sampling and the drain-on-fix rule contradicted each other]` A row covers "every cell of that
     route on that stem", and a row "whose bucket no longer fails on any cell" is stale. If lines are sampled
     within a stem, a row populated in full mode is stale in sampled mode whenever its failing line is not drawn,
     and a row populated in sampled mode is incomplete in full mode. So: sampling is by STEM (a stem's
     `range-line` and `ontype` cells run entirely or not at all); the allowlist is populated from a FULL-mode run;
     and a row is judged stale only when its (stem, twin, route, state) ran in this mode. Mutation for this rule:
     in sampled mode, a row for a stem outside the sample must NOT be reported stale, and the same row for a stem
     inside the sample whose cells pass must be.
   - Commit: `test(conformance): a route-parity sweep drives every LSP formatting route over the corpus — applied text is checked, not the edits (refs #2168)`
     (body: first-run table, census, wall time, the three mutations with red/green counts, "regen: not reached").

### Phase 2: The range route applies checked hunks

**Goal:** Format Selection applies the selected hunks of the checked output, checked again as applied — or nothing.

**Acceptance (class-level, measurable):**
- Sweep rows that go green: every allowlist row with route `range-*` and state S1 or S2 is deleted; the sweep is
  green with the remaining rows (on-type and S3/S4 only).
- Guard mutation: Task 2's and Task 3's (below), each recorded red → green.
- Allowlist entries that drain: all `range-whole` / `range-statement` / `range-block` / `range-line` rows in S1/S2.
- Execution evidence the close-out will cite: cells 1–10 of the Current State table re-run through the rebuilt
  server (`lspfmt.py`), applied text shown; the `refusedWork` table @ sha.

#### Tasks

1. **`LineDiff`: hunks over line content, and their application** — new `src/Sharpy.Compiler/Formatting/LineDiff.cs`;
   tests in `src/Sharpy.Compiler.Tests/Formatting/LineDiffTests.cs`
   - `internal readonly record struct LineHunk(int Start, int End, IReadOnlyList<string> NewLines)` (replace source
     lines `[Start, End)`), `LineDiff.Hunks(source, target)`, `LineDiff.Apply(source, hunks)` per decisions 3 and 5.
   - Properties: (i) for every corpus fixture and each of the four twins, `Apply(Q, Hunks(Q, Format(Q)))` equals
     `Format(Q)` (modulo line breaks for the CRLF twin, whose result has no bare `\n`); (ii) hunks are sorted,
     non-overlapping and non-adjacent; (iii) a CsCheck property over random line lists (the project already
     references CsCheck) — `Apply(a, Hunks(a, b)) == b`; (iv) no trailing newline, empty document, one line; (v) a
     middle over the size bound yields one hunk and still satisfies (i).
   - Mutation: off-by-one in the hunk's `End` → (i) red; restore → green.
   - `[ADDED: missing step]` (vi) **Minimality.** Properties (i)–(v) all hold for a `Hunks` that returns the whole
     middle as one hunk, and the sweep's `local` oracle is computed with the same `Hunks`, so it would agree:
     Format Selection would format the whole changed region — option (b) by accident — with every guard green.
     Pin it: on inputs under the size bound, the source lines covered by `Hunks(a, b)` number exactly
     `len(a) − LCS(a, b)`, with the LCS length taken from the test's reference diff (two minimal diffs may align
     differently; their LCS length cannot). Mutation: return the trimmed middle as one hunk → (vi) red, (i) green.
   - The sweep's `local` oracle switches from its reference diff to `LineDiff.Hunks`; the reference stays as (iii)'s
     cross-check on small inputs (agreement on `Apply`, not on the hunks themselves — two minimal diffs may differ).
   - Commit: `feat(formatting): LineDiff — the hunks between a source and its formatted text, over line content (refs #2168)`

2. **`FormatterService.FormatRange` and `CheckApplied` — the seam** — `src/Sharpy.Compiler/Formatting/FormatterService.cs`;
   tests in `src/Sharpy.Compiler.Tests/Formatting/FormatterServiceTests.cs`
   - Per decisions 1–2. `FormatRange` returns the selected hunks, the applied text and the diagnostics (lex/parse
     errors → the caller takes the fallback; SPY0912 → no hunks). When every hunk is selected and the source has no
     `\r`, the applied text IS the checked output — do not check twice.
   - `internal static CompilerDiagnostic? CheckApplied(string source, string applied, out bool sourceParses)`:
     `sourceParses == false` → returns null and the caller must use its own check (decision 8).
   - Tests (regression pins for the named cells, not the matrix — the sweep is the matrix): cells 1–10 with their
     expected applied text; cell 9 and cell 10 return no hunks and SPY0912; a selection ending at column 0; the
     insertion-neighbour rule on D1 (body selected → no blank lines added; whole document → added).
   - Mutation (inert-fix guard 2): make `FormatRange` check the whole-document output instead of the applied text →
     cell 9 returns hunks and its test goes red; restore → green.
   - `[CORRECTED at /implement-plan by the P2T2 lane, measured]` Cells 9 and 10 do NOT refuse under decisions 1–2:
     `Format` changes D16's lines 1–4 (and D11's 1–3) with no unchanged line between them, so `LineDiff` gives ONE
     hunk and selecting one line takes the whole re-indent — which parses and passes the net (an acceptable outcome:
     "a change may extend to the end of the changed lines it starts in"). Cell 7 (D4) likewise applies `Format(D4)`.
     The SPY0912 pins and the inert-fix mutation use two cells that do refuse: D17 range line 1 (the LCS tie-break
     makes "delete line 1" + "re-insert `x: int = 1` below" — line 1 alone deletes the declaration) and a
     `def main():` body at 8 spaces holding a column-0 triple-quoted string, range line 1 (the string lines are common,
     so two hunks; line 1 alone does not re-parse). Phase 4 Task 1's "applies nothing" example uses one of these, not
     cell 9. The D17 tie-break is a refusal-rate input for Phase 2 Task 4 (a tie-break that prefers matching non-blank
     lines would keep `x: int = 1` in place).
   - Commit: `fix(formatting): range formatting returns the hunks of the checked output that the selection touches, and the net checks the text they produce (refs #2168)`

3. **The range handler applies `FormatRange`'s hunks through the funnel** — `src/Sharpy.Lsp/Handlers/RangeFormattingHandler.cs`,
   new `src/Sharpy.Lsp/Handlers/FormattingEdits.cs`; `src/Sharpy.Lsp.Tests/RangeFormattingTests.cs`
   - `FormattingEdits.ToTextEdits(source, hunks)` per decision 4; the handler's primary path becomes
     `FormatRange` → `ToTextEdits`. Delete the per-index loop and its false comment; rewrite the class summary.
     The unparseable arm (`ComputeIndentOnlyRangeEdits`) is untouched until Phase 3.
   - Property test: `driver.strictApply(ToTextEdits(hunks)) == LineDiff.Apply(source, hunks)` over the corpus twins.
   - **Two existing expectations change, and the commit body must say why** (CLAUDE.md Rule 1 forbids changing an
     expected value to make a test pass; these two expected values ARE the defect): `MultipleLines_ReformattedAsync`
     and `LastLine_ReformattedAsync` pin edits whose applied text does not parse (`SPY0100` / `SPY0014` @ 7a034b81d,
     source parses). Rewrite both to assert the APPLIED text (it equals `Format(source)` — the three body lines are
     one hunk) and quote the old applied text and its diagnostic in the commit body. Every other test in the file
     must pass with its expected values untouched; one that does not is reported to the lead, not edited.
     `[CORRECTED: "only these two" is refuted — three more assertions cannot hold under decision 2 and the `whole`
     oracle. Measured @ 7a034b81d: each source has no final line break, Format Document adds one, and the range
     route returns what the test pins.]`
     - `RangeInsideMultiLineString_NoChangesAsync` and `EntireDocument_TabSize2_FourSpaceDocument_GetsNoEditsAsync`
       select the WHOLE document and assert `BeEmpty()`; Format Document on the same text returns one edit (the
       final line break). They pin `range-whole ≠ Format Document` — cell 4's defect. Rewrite each to assert the
       applied text equals `Format(source)` and, for the first, that the string's interior lines are byte-identical
       (its real subject); the commit body quotes the old value and names cell 4.
     - `TabsPreference_StillWritesFourSpacesInRangeAsync` selects the last line and asserts `ContainSingle()` with
       `NewText == "    x: int = 1"`. Today's applied text is correct. Under decision 2 the final line break is
       selected with the last line, so the edit list changes shape while the applied text becomes
       `Format(source)`. This one is NOT a defect pin: it is a by-direction row ("worked → works, plus the final
       line break"), justified by `whole`/`local`, and the commit body says so in those words. Rewrite it to assert
       the applied text.
     - These five are the complete list by the verifier's reading of all twenty tests in the file against
       decisions 2, 4, 7 and 8 (the other fifteen were traced and keep their assertions;
       `BracketComment…` and `ClauseComment…` read joined `NewText`, which a multi-line hunk still satisfies, and
       the six fallback tests keep their edits under decision 8's five clauses). A sixth is still reported to the
       lead, not edited.
   - `[ADDED: missing step]` Mutation for the property test: make `ToTextEdits` drop the line break it appends to
     an insertion → the property red; restore → green.
   - Delete the drained allowlist rows in this commit; run the sweep's full mode once and record the counts.
   - Mutation: in the handler, pass `request.Range.End.Line + 1` → `local` red on non-allowlisted cells; restore → green.
   - Commit: `fix(lsp): Format Selection applies the checked hunks its range touches — no line is duplicated, dropped or left half-formatted (refs #2168)`

4. **Measure the refusal rate and pin it** — `FormattingRouteParitySweepTests.cs` (census)
   - Record `refusedWork / cells-with-selected-hunks` per range kind for the `identity` and `comment` twins (the
     `wide` twin refuses by construction and is reported separately, not judged).
   - **Decision rule (R-DK, already ruled):** if either `range-statement` or `range-block` refuses more than half of
     its cells-with-work on the `identity` twin, apply fallback (b) inside this group — a refused selection gets the
     one whole-document edit — and record the measurement on #2168. Otherwise keep (a). Either way the census pins
     the measured rate as a literal ceiling (rounded up to the next whole percent) so a later change that makes
     selections refuse more often is red.
   - `[ADDED: missing step]` Mutation: make `FormatRange` return no hunks for every partial selection → the
     ceiling red; restore → green.
   - `[ADDED: missing step]` Record the opposite measurement beside it, as census only: per range kind, the
     selected hunks' source lines that lie OUTSIDE the selection, over the lines selected. (b) was rejected
     because it "formats outside the selection"; (a) does too whenever a hunk straddles the range's edge (cell 10
     re-indents three lines for a one-line selection), and the decision rule above reads only the refusal half.
   - Commit: `test(conformance): the route sweep pins how often Format Selection declines a selection (refs #2168)`

### Phase 3: On-type and the indent-only fallbacks

**Goal:** every remaining route edit is checked as applied, and a literal that is still open is a literal.

**Acceptance (class-level, measurable):**
- Sweep that goes green: `FormattingRouteParitySweepTests` green with an EMPTY allowlist; the census anchors
  `rows.Count == 0` with a literal (the P22b sweep's closing move).
- Guard mutation: Tasks 1–3's, each recorded red → green.
- Allowlist entries that drain: every remaining row (`ontype` in S1–S5; `full` and `range-*` in S3/S4/S5).
- Execution evidence the close-out will cite: cells 11–21 re-run through the rebuilt server; D11 before/after with
  `sharpyc run` (prints nothing both times); the census @ sha.

#### Tasks

1. **The lexer records that it lost a literal that can span lines** `[CORRECTED: was "records where an unterminated
   triple-quoted literal opened; `LiteralSpans` extends it to end of input", with a site list (`Lexer.Literals.cs`
   `:102`, `:114`; `Lexer.FStrings.cs` `:573`, `:890`, `:899`, `:929`, `:535-559`). The list named ONE of the five
   triple-quoted read paths — it omitted `ReadDedentedRawString` (`:249`), `ReadRawString` (`:428`) and
   `ReadTripleQuotedByteString` (`:553`, `:560`, `:568`) although decision 7 said "plain, raw, byte" — it included
   `:899`, which is reached only for a single-quoted f-string, and its span did not reach cells 19–20. See
   decision 7.]` —
   `src/Sharpy.Compiler/Lexer/Lexer.cs` (`ReportError`, `:26` — the one abort funnel; a scope marker entered by the
   triple-quoted arms of `ReadTripleQuotedString`, `ReadDedentedRawString`, `ReadRawString`,
   `ReadTripleQuotedByteString`; the f-string case reads `_fstringStack`), `src/Sharpy.Lsp/IndentationService.cs`;
   tests in `src/Sharpy.Compiler.Tests/Lexer/LiteralStateTests.cs` (new). `LiteralSpans.cs` is not edited.
   - `Lexer.LiteralStateUnknown` (`bool`), set inside `ReportError` when the abort happens within a triple-quoted
     read, or with `_fstringStack` holding a context that is triple-quoted or whose `FStringStart` is on an earlier
     line. `IndentationService.BuildIndentMap` returns it (it constructs the lexer itself).
   - MEASURE the f-string recovery before writing: `TokenizeAll` drops the tokens lexed past an unclosed field's
     bracket and resumes on the next line (`_resumeAfterUnclosedField`), so later lines of an open triple-quoted
     f-string are lexed as code today. The judges are the S4 and S5 `literal` oracles, whose ground truth is the
     parent fixture — not this code's own idea of the state.
   - Diagnostic-neutral: no token, message, code or position changes. Evidence: the whole Lexer and
     FileBasedIntegration suites unchanged, and a fact that `LiteralStateUnknown` is false for every corpus fixture.
   - Tests: each of the eight read paths (plain, `d`, `r`, `dr`, `b`, `f`, `t`, `df`) × {cut after its first interior
     line; D19's inserted opener above it}; the mid-literal aborts D20f and D20b; a single-quoted unterminated
     plain, raw, byte and f-string each set nothing (positive control: the triple-quoted twin of each does).
   - Mutation: set the fact only at `Lexer.Literals.cs:114` (the first draft's one site) → the `r`, `dr`, `b`, `f`,
     `t`, `df` rows of the test and the S4/S5 `literal` rows for those kinds stay red; set it on EVERY abort → the
     single-quoted controls red and the census's S3 "applied text differs" floor red (Phase 1 Task 3).
   - Commit: `fix(lexer): the lexer says when it lost a string that can span lines — the editor's indent-only formatting then leaves the document alone (refs #2168)`

2. **The indent-only check and the funnel's second arm** — `src/Sharpy.Lsp/IndentationService.cs` (return the
   logical-line-start set and the literal-line set with the map), `src/Sharpy.Lsp/Handlers/FormattingFallback.cs`
   (`IndentOnlyPreserved`), `src/Sharpy.Lsp/Handlers/FormattingEdits.cs` (`CheckedIndentOnly`),
   `FormattingHandler.cs` and `RangeFormattingHandler.cs` (their unparseable arms go through the funnel);
   tests in `src/Sharpy.Lsp.Tests/FormattingFallbackTests.cs` (new)
   - Per decision 8. The full fallback re-indents every line consistently and must pass its own check on every S3
     cell — a refusal there is a finding (report it; do not weaken the check). `[CORRECTED: was "every S3/S4 cell" —
     under decision 7 an S4 or S5 document gets no edits from any indent-only route, by design]`
   - Cells 14, 15, 16, 19, 20, 21 by direction: before → the applied text in the Current State table; after →
     untouched / no edits. `[ADDED: adequacy]` One restriction to record in the commit body: on an S4 document the
     lines above the open literal were re-indented correctly before and are not edited now (decision 7, option a).
   - Mutation: drop the level-invariance clause → cell 16's test and the `depth` rows red; drop the literal clause →
     `literal` rows red (with Task 1 reverted in the same worktree, to show the clause is not inert behind the guard).
     `[ADDED: missing step]` Drop the indentation-diagnostics clause → cell 21's test and its `depth` rows red, cell
     16 still green (the two clauses are not each other's fallback); drop the `LiteralStateUnknown` early return →
     cells 19 and 20 and the S5 `literal` rows red.
   - Commit: `fix(lsp): the indent-only fallback is checked as applied — a partial re-indent that changes a line's block, or touches an open string, applies nothing (refs #2168)`

3. **On-type edits a logical line's indentation only, and only when the check passes** —
   `src/Sharpy.Lsp/Handlers/OnTypeFormattingHandler.cs`; `src/Sharpy.Lsp.Tests/OnTypeFormattingTests.cs`
   - Per decision 6, through `FormattingEdits.CheckedIndentOnly`. The check runs only when there is a candidate
     edit, so a keystroke on a canonically indented line costs what it costs today (one lex).
   - New tests: cells 11, 12, 13, 17, 18, 19, 20, 21. Every existing test in the file must pass with its expected values
     untouched (each is a single-line body whose re-indent passes the net — verify, do not assume).
   - Delete the last allowlist rows; anchor the empty allowlist in the census; run the sweep's full mode and record it.
   - Mutation: remove the funnel call → cell 11 red and `net` rows red; remove the continuation rule → `fixedPoint`
     red; restore → green.
   - Commit: `fix(lsp): format on type never edits inside a string or a continuation line, and never moves a statement to another block (refs #2168)`

### Phase 4: Documentation, roster, wire cells, gate

**Goal:** the documented guarantees match the behaviour, the harness is on the roster, and the group closes on one gate.

**Acceptance (class-level, measurable):**
- Every example added to `docs/tooling/formatter.md` is executed @ sha (through the driver for editor routes;
  `sharpyc format` for the CLI ones) and its output is the text in the doc.
- Guard mutation: Task 3's E2E cells red when the server's range handler is reverted to the base commit's.
- Allowlist entries that drain: none left; `bash build_tools/check_allowlist_issue_state.sh` green.
- Execution evidence the close-out will cite: whole-solution gate `passed/failed/skipped @ sha (measured)`.

#### Tasks

1. **`docs/tooling/formatter.md` — the editor section states the guarantees** — lines 118–123 and line 133
   - Replace the "do not yet carry these guarantees (#2168) … use Format Document" paragraph with: what Format
     Selection applies (the changes the selection touches; a change may extend to the end of the changed lines it
     starts in), when it applies nothing (the selection cannot be formatted on its own — with cell 9's document as
     the example), that it keeps the file's line endings, what format on type edits (the indentation of a
     statement's first line) and what it never touches (a string; a continuation line), and the
     fallback's checked guarantee — including, in the user's words, decision 7: while a triple-quoted string is
     unfinished or broken, the editor cannot tell which lines are code, so format on type and the indentation-only
     pass change nothing until it is closed (D19 as the executed example) `[CORRECTED: was "a string, open or
     closed" — that promise is not keepable: in D19 the lexer sees the damaged line as code]`. Drop #2168 from the "open formatter issues" sentence.
   - No test reads `formatter.md` today (grep @ 7a034b81d). Add `FormatterDocEditorExamplesTests` in
     `Sharpy.Lsp.Tests`: it extracts the editor section's fenced blocks from the doc file (before / after pairs),
     drives the handlers through `LspFormattingDriver`, and asserts the applied text equals the doc's "after" block —
     so the doc cannot drift. Mutation: change one character of an "after" block → red. Examples must discriminate:
     cell 1 (before the fix: `def main():` twice) and cell 11.
   - Commit: `docs(formatter): Format Selection and format on type carry the formatter's guarantees — what each applies, and when it applies nothing (refs #2168)`

2. **Roster row** — `docs/design/gap-discovery-contracts.md` (after the "Formatter meaning preservation" row)
   - Class "Formatting route parity (#2168)"; contract (this plan's); guard `FormattingRouteParitySweepTests`
     (regular suite, `Sharpy.Lsp.Tests`) with its routes, states, twins, buckets, the roster-totality fact and
     `formatting-route-parity-allowlist.txt` (populated at Phase 1, EMPTY since Phase 3 @ sha).
   - Commit: `docs(design): the gap-discovery roster gains formatting route parity (refs #2168)`

3. **One wire cell per route** — `src/Sharpy.Lsp.Tests/E2E/WorkspaceAndFormattingTests.cs`
   - Through `LspTestClient` (a real server over stdio): `textDocument/formatting` on D1, `textDocument/rangeFormatting`
     for cell 1 and cell 2, `textDocument/onTypeFormatting` for cell 12 — assert the APPLIED text. This is the one
     axis the generator cannot enumerate.
   - Mutation: in a worktree with `RangeFormattingHandler.cs` at the base commit, the two range cells are red.
   - Commit: `test(lsp): the formatting routes over the wire — one end-to-end cell per route (refs #2168)`

4. **Gate and close-out** — no file
   - `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`
     across the solution, in the background, to a private log; report `passed/failed/skipped @ sha (measured)`.
     A red is attributed only after a control run at the base commit `7a034b81d` (worktree).
   - Re-run `lspfmt.py` on all three case files (`cases3.json` holds cells 19–21) against the rebuilt server and
     attach the applied texts for cells 1–21.
   - The lead files the ledger per wave, then closes #2168 with the evidence below.

## Testing Strategy

- **Generator first (§11):** `FormattingRouteParitySweepTests` enumerates route × range kind × document state ×
  twin over the corpus through the real handlers; its allowlist is the ratchet and drains to empty in Phase 3. The
  only hand cells are the wire-protocol ones (Phase 4 Task 3), the named-cell regression pins in
  `FormatterServiceTests` / `OnTypeFormattingTests`, and each bucket's positive control.
- **The sweep checks the applied text, never the edit list** — an edit list can look right line by line (today's
  per-line edits do) and produce a document that does not parse.
- **Independent judges:** `net` is computed by the test on the applied text; S3/S4/S5 ground truth comes from the
  parent fixture's clean lex; the `depth` stack is the lexer's rule, not the indent map's; the selection predicate is restated in the test; `LineDiff` is held by
  `Apply(P, Hunks(P, F)) == F`.
- **Absence assertions and their positive controls:** "no edit inside a string" ↔ the same request re-indents an
  over-indented code line in the same document; "no edits for cell 9" ↔ the whole-block selection of D16 is
  formatted; "`OpenLiteralStart` is null on the corpus" ↔ non-null on the four cut literals.
- **Outputs that discriminate:** D11 uses `if False:` so the moved statement changes what the program prints
  (nothing → `2`); D2's duplicate is a `print`. A document whose damaged form prints the same proves nothing.
- **Refusals by direction (§3):** the `refusedWork` census lists every selection that newly gets no edits, with the
  prior applied text's classification; `fixedPoint` is the justification for every on-type edit that disappears on
  a formatted document.
- **Existing tests:** `[CORRECTED: was "only `MultipleLines_ReformattedAsync` and `LastLine_ReformattedAsync`"]`
  five tests in `RangeFormattingTests` change expectations (Phase 2 Task 3): those two, with the evidence that
  their old expectations do not parse; two whole-document tests that pin cell 4; and one last-line test whose
  edit list changes shape while its applied text stays correct. Everything else in `RangeFormattingTests`,
  `OnTypeFormattingTests`, `FormattingTests` and the E2E formatting tests passes unchanged.
- **Spec/doc deliverable:** `docs/tooling/formatter.md` § "In the editor (LSP)", every example executed; no
  `docs/language_specification/` file is touched (formatting is tooling, not language).
- **Blast radius and regen:** see Adversarial Review. No generated artifact is reached; the gate is the whole
  solution; pre-commit filters for any agent include `Sharpy.Lsp.Tests` and the conformance/census classes.
- **Agents:** every agent prompt carries the shared-tree block of verification-contract §9 verbatim, and mutation
  tests run in a `git worktree` under `sharpy.worktrees/` or restore from a `cp` copy (never `copy2`, never git).

## Issues to Close

- **#2168** — lsp: range and on-type formatting apply edits that are not the checked `Format(P)` — closed by Phase 3
  Task 3 (code) and Phase 4 Task 4 (gate). **Close criterion:** `FormattingRouteParitySweepTests` green with an
  empty allowlist and its full mode run @ sha; cells 1–21 re-run through the rebuilt server with the expected
  applied texts; the three recorded mutation pairs per phase; `formatter.md`'s editor section rewritten with executed
  examples; whole-solution gate `passed/failed/skipped @ sha (measured)`. The issue is closed by the lead with
  `gh issue close`, not by a "closes" phrase in a commit body (that auto-closes on the mainline push and reddens the
  allowlist issue-state gate if a row still cites it).

## Class trackers this plan may post cells to

- **#2168** — formatting route parity — until it closes: ledger notes of lane `tooling` (decision 5's duplicate
  line diff; decision 10's continuation-line treatment in the fallback; decision 7's option (c), the narrower
  freeze, if the S4 restriction is reported as a nuisance).
- **#2169** — unparser spelling fidelity / constructs the formatter declines (P22d) — a `cli` or `full` cell that
  fails `whole`/`net` in S1/S2, or a stem whose `Format` is declined, is the unparser's, not a route's.
- **#2232** — LSP code-action source-shape preservation (P47) — anything the driver shows about code actions.

Cells found outside this plan's contract go to the findings ledger; the lead files a NEW tracker only for a class
none of these covers. No implementer runs `gh issue create`.

## Implementation Checklist

`/implement-plan` started 2026-10-06 @ base `7a034b81d`. Baseline gate: **57955/0/4 @ 7a034b81d (measured**, the #2263
session's whole-solution run, log kept at `.claude/tmp/p22e-impl/baseline-gate-7a034b81d.log`); build green @ 7a034b81d.
Decision 7 option (a) stands (the owner did not ask for (c) before `/implement-plan`). Lanes run in isolated worktrees;
the lead cherry-picks onto `dev`. Findings ledger: `<scratchpad>/findings-ledger.md`.

- [x] P1T1 FormatterTwins → TestInfrastructure + wide/CRLF twins — test-expert (wave 1, lane A1) — `4d656af20` (pure move, 13841/0/0 before and after) + `a6136442a` (twins; mutation 3/34 red); `Format(Wide(P)) ≠ Format(P)` on 2 pinned stems (`Formatting/inner_comments_verbatim`, `imports/from_import_parenthesized` — a comment line inside a verbatim bracketed statement); lead `31b58a840` adds comment+wide to LineDiff (i) — 53/0/0
- [x] P1T2 LspFormattingDriver + strict applier + Routes totality — test-expert (wave 1, lane A2) — `0a8162edc` (34 tests; mutations 3/34, 1/34, +2 extra red; worktrees were created at 90f7ff9ff, Stdlib-only delta to dev)
- [x] P2T1 LineDiff + properties (i)–(vi) — implementer (wave 1, lane B) — `5f2d26557` (17 tests; corpus 3287; mutations 9/17 and 4/17 red; (i) covers identity+crlf — comment/wide twins to add to `LineDiffTests.TwinTransforms` after P1T1 lands)
- [x] P3T1 Lexer.LiteralStateUnknown at ReportError + BuildIndentMap returns it — parser-expert (wave 1, lane C) — `5cf4e6400` (51 LiteralStateTests; Lexer|LiteralSpans 970→1021 /0/2; corpus: 0 of 3769 clean fixtures set it; mutation 1 (plain-EOF site only) 26/51 red — the 16 D19 rows stay green because every D19 ends in the PLAIN read, so the plan's "r…df rows red" holds for the cut rows only; mutation 2 (every abort) 10/51 red — **re-run by the lead in `sharpy.worktrees/p22e-lead` @ 5cf4e6400: 10 failed / 41 passed, restored 51/0**). FileBasedIntegration left to the gate.
- [x] P1T3 part A (S1/S2, parseable oracles, ratchet, sampling) — test-expert (wave 2) — `ce5b88f51`: 86,538 rows (9.5 MB raw, 440 KB as a git object; plan expected "thousands"; comment twin 34.6k); cli/full S1/S2 rows 0 (loader refuses them); sampled ≈63–83 s, full 213 s; corpus 3287 stems; mutation (a) **re-run by the lead @ ce5b88f51: 12235 failed / 13161, restored 13161/0**; `check_allowlist_issue_state.sh --paths` green. Follow-up `5aba67e5e`: reason text dropped (`stem twin route state bucket # #2168`), 6.2 MB; gate positive control (closed #2262 / uncited row → exit 1). Decision 2's insertion formula corrected to its prose (see decision 2).
- [x] P1T3 part B (S3/S4/S5 cuts, content/literal/depth oracles, literal-kind floors, r/b fixtures) — test-expert (wave 2b) — `482f14a67`: +3,862 rows (90,400 total; `content` 0; cli/full S1/S2 0); fixtures `raw_multiline_string`, `byte_multiline_string`, `backslash_continuation`; sampled 69 s / full 224 s; mutations: depth copies IndentationService → cell 21 control red; literal dropped → control red; S5 line empty → 12 census floors red (**re-run by the lead @ 482f14a67: 12 floors red, restored green**). Ledger cell (b""" re-spelled single-line, value kept) posted on #2169 (comment 6024437372).
- [x] P2T2 FormatterService.FormatRange + CheckApplied — implementer (wave 2b, parallel with sweep part B) — `85dad036b` (FormatSelection + FormatRangeResult; cells 1–10 pinned as measured — 7/9/10 apply one whole hunk (see the CORRECTED note in Phase 2 Task 2); SPY0912 pins D17 line 1 + 8-space body/column-0 string; FormatterService|LineDiff 84/0/0, Formatter|LineDiff 13488/0/0; mutations: check whole output → 2/84 red, keep all hunks → 8/84 red; D17 LCS tie-break in the ledger)
- [x] P2T3 Range handler through FormattingEdits; five RangeFormattingTests — lsp-expert (wave 3) — `ec673074f`: exactly the five named tests changed (old values quoted in the body), no sixth; 72,935 range S2 rows drained (0 range S1), 17,483 remain; Lsp.Tests 17621/0/0; mutations: ToTextEdits drops insertion break → 3288 red, End.Line+1 → 9893 red full mode (**re-run by the lead @ ec673074f, sampled: 3879 failed / 13176, all `local` on range-block/statement/line S2; restored 13176/0**). Wide twin refuses by construction (range-line 13184, block 3942, statement 541) → P2T4. Ledger: SingleMisIndentedLine comment names the wrong path → folded into P3T2.
- [x] P2T4 Refusal-rate ceiling + over-reach census — test-expert (wave 4) — `a3244f6cd`: full-mode refused/with-work on identity — whole 0/2389, statement 1/1443 (0.07%), block 6/2721 (0.22%), line 28/1070 (2.62%); comment line 46/402 (11.44%); wide (reported only) statement 7.93%, block 30.12%, line 37.57%. **R-DK decision rule: (a) stays** (statement/block ≪ half). Ceilings pinned (identity 1/1/1/3 %, comment 1/1/1/12 %, sampled range-line own ceiling). Over-reach on identity: statement 0.05%, block 0.14%, line 126.67% of selected lines (a one-line selection that applies changes ~1.27 lines outside itself). D17-shape upper bound: identity line 10/28 refusals. Mutation: no hunks for partial selections → census red (6 ceilings), restored green.
- [x] P3T2 IndentOnlyPreserved (5 clauses) + funnel's second arm — lsp-expert (wave 4) — `aef9c0816`: cells 14/15/16/19/20/21 by direction (candidate → no edits); S4 restriction has its own test; 1,958 rows drained; full-fallback S3 refusals 0 (33,679 accepted); Lsp.Tests 17636/0/0, full sweep 13176/0/0; mutations: level clause → 533 red (Cell16 + 531 depth), diagnostics clause → 1498 red (Cell21; Cell16 green) (**re-run by the lead @ aef9c0816: 1498 failed / 13191, Cell21 red, Cell16 not among them; restored 13191/0**), early return → 62 red; literal clause is a backstop behind the producers' own literal skip (with that skip disabled too: 58 → 63 red, +6 S3 literal rows) — recorded, not a vacuous guard. **100 non-ontype rows still failed** (3 classes, all in-contract) → P3T2b.
- [x] P3T2b sixth clause (lexer depth unchanged) + decision-7 seam fix for a multi-line f-string hole — lsp-expert (wave 5) — `f6fd8b4e1` (lexer; 3 LiteralState rows; 12 S4 rows) + `d97821ff2` (clause 6; 88 S3/S5 rows; ruling (a) applied to two tests authored in aef9c0816, new `UnexpectedIndent_FullFallback_GetsNoEdits`); 0 non-ontype rows left; newly refused S3 candidates: full 84, range-whole 84, range-line 16 — all before_set/after_set in the 8 observer stems + the 4 d-string S5 documents; Lsp.Tests 17642/0/0; Lexer|LiteralSpans|LiteralState 1024/0/2; mutations: clause dropped → 88 rows + 6 tests red (**re-run by the lead @ d97821ff2: 42 failed / 13197 tests, the failing rows are exactly the 88 non-ontype depth rows — full S3 32 + S5 4, range-whole S3 32 + S5 4, range-line S3 16; restored 13197/0**); lexer fix reverted → 12 rows + 3 rows red; exempt-all → 5 red; detection-line-only seam → lane C control red.
- [x] P3T3 On-type through the funnel; allowlist empty — lsp-expert (wave 6, main tree) — `ba29675c1`: ruling (a) — no requested-line exemption (with one, 16 ontype S3 depth rows in the 8 property-observer stems still re-nested); cells 11/12/13/17/18/19/20/21 + 2 pins by direction (applied → no edit); positive controls: parseable nested 16→8, unparseable width-at-constant-depth 8→4 (lane used `def foo():\n        x: int = 1\ny = (\n` — the lead's suggested text is refused by the depth clause); 11 pre-existing OnType tests untouched and green; **allowlist 15,407 → 0, census anchors `rows.Count == 0`**; full sweep 13176/0/0, Lsp.Tests 17657/0/0; mutations: funnel removed → 1798 red (**re-run by the lead in `sharpy.worktrees/p22e-lead` @ ba29675c1: 1798 failed / 13202, restored 13202/0**), continuation rule removed → 503 red, exemption re-added → 18 red (2 pins + the 16 rows).
- [x] P4T1 formatter.md editor section + FormatterDocEditorExamplesTests — test-expert (wave 7, main tree) — `8226d2c78` + lead/lane wording follow-up `fe6e1b13b` (on-type changes a width, never a block): 5 examples / 7 requests, every one measured through the driver @ ba29675c1 — selection D1 4–5 (only `x = helper()` re-spaced, no blank lines), selection line 2 of the 8-space body with a column-0 `"""` string (applies nothing — refused as predicted; D17 not needed), on-type positive 12→8, on-type D11 line 4 (nothing), D19 document/selection 6/on-type 6 (nothing); instrument literals 5/7/2 + positive control (Format Document changes both "unchanged" selection/on-type examples); mutations: one char of an "after" block → 1 red, on-type funnel removed → 1 red (the D11 example), both restored green; Lsp.Tests 17659/0/0. The intro's "Format Selection produces the same text" sentence also corrected.
- [x] P4T2 gap-discovery roster row — lead — `88345108f` (row after "Formatter meaning preservation"; allowlist EMPTY since Phase 3 Task 3 @ ba29675c1)
- [x] P4T3 E2E wire cell per route — test-expert (wave 7, worktree, cherry-picked) — `5cfa8a401` (from e5c0c015d): formatting D1, range cell 1, range cell 2, on-type cell 12 (no edit) + on-type positive control over stdio, each pinned literal AND equal to the in-process driver; WorkspaceAndFormattingTests 19/0/0, Lsp.Tests 17662/0/0; mutations: range handler @ 7a034b81d (two compile fixes) → 2 red — cell 1 `def main():` twice, cell 2 `print(xs, y)` twice (**re-run by the lead in `sharpy.worktrees/p22e-lead` @ 88345108f: 2 failed / 19, exactly the two range cells; restored 21/0 with the doc-example tests**); on-type handler @ 7a034b81d → cell 12 red; single-stop on-type mutations (funnel / literal rule / literal+logical-start) stay green — cell 12 has three independent stops, recorded, the whole-handler revert is the falsifying mutation.
- [x] P4T4 Whole-solution gate + lspfmt re-run cells 1–21 (+ close #2168 → deferred to /verify-implementation) — lead — blocked by all
  - lspfmt re-run @ ba29675c1's binaries (Sharpy.Lsp.dll 19:47, Compiler 18:40, no production diff after): cases1/2/3 → `<scratchpad>/final-out{1,2,3}.json`. Cells 1–6, 8 as expected; 7, 9, 10 apply the whole checked re-indent (per the Phase 2 Task 2 correction); 11–21 no edits; C1/C2 hold.
  - Ledger notes posted on #2168 (comment 6027713578: decision 5, D17 tie-break, decision 7 (c), re-nest (b), colon-only opener, column-0 comments, decision 10). #2168 is NOT closed here: the skill closes issues at /verify-implementation.
  - `check_allowlist_issue_state.sh` exit 0 @ 88345108f.
  - Whole-solution gate #1 @ 88345108f: **74634/2/4 (measured**, 36.9 min, `<scratchpad>/final-gate-88345108f.log`; Core 3989, Stdlib 4180, Cli 441+1 skip, Lsp 17656, Compiler 48368/2/3). Both reds are this plan's own files (absent at 7a034b81d → attributed by site): SourceGlobConformance (#1660) on LiteralStateTests' glob, and MetamorphicCorpusSweep ParensWrap::Formatting/backslash_continuation (the transform wrapped a `\`-continuation line). Fixed in `dbe585053` (MaskedSource.StartsStatement honours explicit line joins; glob filtered); mutation 1 red / restored 78/0; the four guards 136/0/0. A second gate runs after P4T5.
  - Whole-solution gate #2 @ 92af80222: **77937/0/4 (measured**, ~34 min, `.claude/tmp/dotnet-serialized-2.log`, summary `<scratchpad>/final-gate-92af80222.log`; Core 3989, Stdlib 4180, Cli 441+1 skip, Lsp 20954, Compiler 48373+3 skip). Lsp 20954 vs the lane's 20962 = the 8 `LspAnalysisLatencyBaselineHarness`/`LspEditSessionTraceHarness` benchmark tests the lane's unfiltered run included. Allowlist issue-state gate: exit 0.
- [x] P4T5 one line model across the LSP formatting routes (lone `\r`) + sweep `cr` twin — lsp-expert (wave 8, main tree) — `[ADDED at /implement-plan after the standalone verification-expert @ 88345108f, in contract]` The verifier (opus, lock-free, `<scratchpad>/verify-report.md`) refuted the contract on an axis the generator does not enumerate: the lexer, `LineDiff` and the LSP spec count a lone `\r` as a line break, but `FormattingHandler.WholeDocumentEdit`, `RangeFormattingHandler.ComputeIndentOnlyRangeEdits`, `OnTypeFormattingHandler` and the funnel's `Apply`/`IndentOnlyPreserved` split on `\n` only — the server checks one text and the client applies another. LC3: Format Document on a document that PARSES, `def main():\r    x   =  1\n    print(x)\n`, applies a duplicated `print(x)` (prints `1\n1`, no diagnostic); the fallbacks and on-type drop/duplicate/rewrite lines. Pre-existing at 7a034b81d (same `Split('\n')`). Also: the full fallback rewrites CRLF to LF (A6) — invisible because `whole` compares modulo line breaks. Cure: a sweep twin with lone-`\r` breaks (generator first, red measured), ONE line model (split keeping each line's own break) in every formatting handler and the funnel, and the fallback/on-type judged byte-exact on line breaks.
  - Landed `92af80222` (lane-p4t5-lines): every route + funnel + `IndentationService.BuildIndentMap` (lead-approved addition: its `\n` split indexed lexer lines) on `LineDiff.Split`; `cr` twin + instrument check; `content` judged byte-exact on breaks for unparseable oracles; per-twin S3 census floor; 6 pins in `LineBreakFormattingTests`. Pre-fix red FULL 6573/16468 (cr S2 edits 3286, cr S3 edits 2999, cr S2 whole 2, crlf S3 content 3283, 6 pins); post-fix FULL 16468/0/0, Lsp.Tests 20962/0/0. Mutations a 3294 · b1 3 · b2 6568 · c 2 · d (BuildIndentMap → `Split('\n')`, run after the commit, not in its body) 2336 · restored 16474/0. Lead re-run of (a) in sharpy.worktrees/p22e-lead @ 92af80222 (FULL): 3294 failed / 16474 → restored 16474/0 — reproduces. Allowlist EMPTY, `rows.Count` anchor at :1749. Defect: the commit trailer lost its `Co-Authored-By` key (reads `: Claude Opus…`); not amended (shared tree). Ledger notes 8–9 → #2168 comment 6030199291; Refactoring/ line-model cell → #2232 comment 6030199432.
- [x] P4T6 formatter.md wording — lead — `ad3944d0c` (the fallback's "changes nothing else" is made true by P4T5, not reworded) — the verifier's doc findings: the reach goes both ways ("to both ends of the run of changed lines it touches"); the fallback "changes nothing else" holds after P4T5; "stays in the block it was in" holds for every line the lexer places in a block — a tab-indented line or one at a width no enclosing block uses (SPY0012/SPY0013) is the misindentation the pass repairs, aligned where the editor's indent levels put it (M11b, T2: outside the contract as defects — the source has no block for that line — fallback-quality note for #2168).
- [x] Final: standalone verification-expert; `dotnet format whitespace` clean — lead — blocked by all
  - verification-expert (opus) @ 88345108f → P4T5 + P4T6. After P4T5 the lead re-ran its probe (`<scratchpad>/verify/post_v{1,2,3}.txt`) on the built server @ 92af80222: LC3 full applies the formatted text (no duplicated line; src/applied both print 1), every lone-`\r` cell edits the client's line or applies nothing; LC2 on-type 2 → note-8 cell on #2168 (comment 6030732819). No second verifier spawned. `check_spy_staleness.sh` 0, `check_spy_tests_staleness.sh` 0 (119 files), `dotnet format whitespace sharpy.sln --verify-no-changes` 0, allowlist issue-state 0. #2168 stays OPEN for /verify-implementation.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS (after the corrections below; as written the literal cure reached one arm — see Adequacy)
**Verified on:** 2026-10-06 @ 7a034b81d (dev, tree clean before and after)
**Plan file:** `.claude/plans/plan-96f8e0.md`

Method: `check_plan_refs.py --issues` first; every handler, `IndentationService`, `FormattingFallback`,
`FormatterService`, `LiteralSpans` and the lexer's literal readers read in full; the plan's 44 recorded driver cases
re-run through the built server (`Sharpy.Lsp` built 2026-10-06 10:54, after the last `src/Sharpy.Lsp` /
`src/Sharpy.Compiler` commit `15d7a122d`) — 44 of 44 byte-identical; 17 new cases through the same driver
(`cases3.json` → `out3.json`); 16 texts through the built `sharpyc` (6 under `run`, for the claims about what a
program prints or whether it compiles; 10 under `emit diagnostics`, for the indentation-diagnostic counts). No `dotnet` build or test was run: the
plan proposes code that does not exist yet, so everything about the plan's own design is read, not executed.

### Corrections Made
- **Decision 7 and Phase 3 Task 1 (changes a decision).** Before: the lexer records where an unterminated
  triple-quoted literal opened and `LiteralSpans` gains `[that offset, end of source]`. After: the lexer records one
  fact at its single abort funnel, `LiteralStateUnknown`, and no indent-only route edits such a document. Reason,
  measured: cell 19 (D19 — a docstring opened above an existing multi-line string: all three routes strip 4 spaces
  from the LATER string's content line, which lies before the last unmatched opener) and cell 20 (D20f/D20b — the
  lexer aborts inside a closed literal and re-indents its lines). The first draft cured neither. Options (a) taken,
  (b) first draft, (c) narrower freeze are recorded under decision 7; all two-way.
- **Phase 3 Task 1 site list.** Before: `Lexer.Literals.cs:102`, `:114` and five `Lexer.FStrings.cs` sites. After:
  the `ReportError` seam. The list omitted three of the five triple-quoted read paths (`:249`, `:428`,
  `:553`/`:560`/`:568`) that decision 7 itself named, and included `:899`, which only a single-quoted f-string reaches.
- **Decision 8 gains a fifth clause** (no more `SPY0013`/`SPY0014` in the applied text than in the source). Reason,
  measured: cell 21 — the indent map gives a dedent between two widths the same level before and after, so the
  level clause accepts an edit that turns a document which runs once its body is typed into `SPY0014`.
- **`depth` oracle** (Phase 1 Task 3): the test-side width stack is now specified as the lexer's rule; a stack that
  mirrored `IndentationService.cs:95-98` would have agreed with production on cell 21.
- **Document state S5** added to the matrix, the generator, the census floors and the positive controls.
- **Phase 2 Task 3 / Testing Strategy:** "only two existing tests change expectations" → five. Added
  `RangeInsideMultiLineString_NoChangesAsync`, `EntireDocument_TabSize2_FourSpaceDocument_GetsNoEditsAsync` (both
  pin cell 4: whole-document selection ≠ Format Document) and `TabsPreference_StillWritesFourSpacesInRangeAsync`
  (a by-direction row, not a defect pin). Decision 2 now states how a missing final line break is modelled.
- **Allowlist × sampling** (Phase 1 Task 3): line-level sampling contradicted drain-on-fix; now sampling is by
  stem, the allowlist is populated in full mode, and staleness is judged only for what ran.
- **Phase 1 mutation (a):** `lastCol - 1` → "drop the last line of `NewText`" (`lastCol - 1` is `-1` on every
  newline-terminated document and reads as the instrument bucket).
- **Corpus count:** plain `r"""` is in 0 fixtures, not 1 (the 1 is `dr"""`); the census's literal kinds are now the
  eight lexer read paths.
- **Phase 3 Task 2:** "the full fallback must pass on every S3/S4 cell" → S3 (S4/S5 get no edits by design).
- **Phase 4 Task 1:** the doc sentence "never touches a string, open or closed" replaced by the keepable promise.
- **Owner ruling link** now points at the comment (`#issuecomment-5944918395`), not the issue.

### Adequacy
- **Contract:** named — every formatting route applies a text that was checked as applied, or applies no edits;
  uniform across route × document.
- **Matrix:** larger than the repro lists — and, as written, still short of one document-state value. The plan's
  S4 (cut after an open literal) has no text after the literal, so it could not show a guard that protects only
  the tail. Added: S5 and cells 19–21. Arms checked by the verifier: the three handlers (all reached); the two
  indent-only producers `ReindentDocument` and `ComputeIndentOnlyRangeEdits` plus on-type (all go through decision
  8's funnel); the lexer's 75 `throw ReportError` sites (now one seam instead of a list of six).
- **Cure:** seam + harness — meta-class "position/route missing a shared check", effect "silent wrong behavior".
- **Harness:** Phase 1 adds `FormattingRouteParitySweepTests`; allowlist populated at Phase 1, empty at Phase 3.
- **Layer:** every decision rung-justified — no `Semantic/` rule, no name-keyed rule; decision 7 is a recorded
  lexer fact.

### Scope & convergence
- **Group:** one (P22e) · **Alone:** no — required? no (no printed output, no snapshot) · **Gates:** 1
- **Generator:** built first in Phase 1; hand cells only for the wire protocol, the mid-literal abort (cell 20) and
  each bucket's positive control.
- **Trackers named:** #2168, #2169, #2232 · **Fidelity rows:** N/A (tooling). No implementer files issues.
- **Budgets:** added to `## Scope` (the plan did not state the prober's).

### Falsifiability
- `FormatterTwins.WideIndented` / `Crlf`: mutation named (double a literal-interior line); exemption not subject;
  positive control yes.
- `LspFormattingDriver` strict applier: mutation named (apply against the moving text); positive control yes
  (throws on overlap). `Routes` totality: literal `3`; mutation MISSING — expected: remove one handler from the
  roster → the totality fact red.
- `FormattingRouteParitySweepTests`: mutations named (a, corrected), (b), (c); positive control per bucket; added
  the S3 "applied text differs" floor, without which an always-set `LiteralStateUnknown` is green; by-direction
  probe specified (`refusedWork` census).
- `LineDiffTests`: mutation named for (i); minimality ADDED as (vi) with its mutation — without it a one-hunk diff
  passes every guard, because `local` is computed with the same `Hunks`.
- `FormatterServiceTests` (cells 1–10): inert-fix mutation named (check the whole-document output instead).
- `ToTextEdits` property: mutation ADDED. Range handler: mutation named (`End.Line + 1`).
- Refusal-rate ceiling (Phase 2 Task 4): mutation ADDED.
- `LiteralStateTests` (was `LiteralSpansTests`): mutations rewritten for the corrected decision (one site only →
  six kinds red; every abort → single-quoted controls and the S3 floor red); absence assertion ("false on the
  corpus") has its positive control.
- `FormattingFallbackTests`: mutations named per clause; ADDED the two new clauses, with "cell 16 stays green when
  the diagnostics clause is dropped" so the clauses are shown not to be each other's fallback.
- `OnTypeFormattingTests`: mutations named (remove the funnel call; remove the continuation rule).
- `FormatterDocEditorExamplesTests`, E2E wire cells: mutations named; outputs discriminate (cell 1, cell 11).

### Warnings
- **Decision 7 (a) restricts one thing that worked.** On a cut-only S4 document, Format Document used to re-indent
  the lines above the open literal correctly and will now do nothing until the string is closed. It is recorded as
  a by-direction row and is the price of not guessing; option (c) removes it later without undoing (a). If the
  owner prefers (c) now, say so before `/implement-plan` — Phase 3 Task 1 then needs a second lexer fact.
- **Budget is unmeasured and probably over.** The corpus is 3,874 `.spy` files and 65,177 lines (3,511 at depth
  ≤ 2). `range-line` runs one whole-document `Format` plus one net check per non-blank line per twin; expect the
  90 s target to be exceeded and the stem-level sampling to be the normal mode. Measure in Phase 1 Task 3 before
  populating the allowlist.
- **(a) also formats outside the selection** whenever a hunk straddles the range's edge. The decision rule in
  Phase 2 Task 4 reads only the refusal rate; the over-reach census was added so the choice between (a) and the
  in-group fallback (b) is made on both numbers.
- **`Format(Wide(P)) == Format(P)`** is asserted as an instrument check but was not measured. If it fails on a
  fixture (a verbatim bracketed statement is the likely place), skip and count the stem; do not weaken the twin.
- **Lone `\r` documents:** every handler splits on `\n` only, the strict applier on `\r\n`, `\n` and `\r`. The
  corpus has none and the `crlf` twin does not produce one; a cell found there is a ledger note for #2168.

### Missing Steps Added
- S5 cells, its census floors and positive controls (Phase 1 Task 3).
- S1 de-duplication by content (the `wide` twin's S1 documents equal the `identity` twin's).
- `LineDiff` minimality property (Phase 2 Task 1); `ToTextEdits` mutation (Task 3); ceiling mutation and
  over-reach census (Task 4).
- Mutations for decision 8's two new behaviours (Phase 3 Task 2); cells 19–21 in the new tests and the close-out.
- Prober and verify budgets (`## Scope`).

### Unchecked Claims
- "P22b's allowlist peaked above 11,000" — not checked; `git log -p` on
  `formatter-meaning-preservation-allowlist.txt` would show it. Nothing rests on it.
- The fifteen `RangeFormattingTests` that "keep their assertions", and the three `OnTypeFormattingTests` with an
  edit, were traced by reading against decisions 2, 4, 6, 7 and 8 — the code does not exist to run. Phase 2 Task 3
  and Phase 3 Task 3 run them.
- "Cells 19–21 are refused after the plan" is derived from the corrected decisions, not measured. The S5 `literal`
  rows, the `depth` rows and the three named tests are what measure it.
- The parity argument behind decision 7 (one lost opener always ends in an unterminated literal or an abort inside
  one) was reasoned for one quote character; a file mixing `'''` and `"""` literals was not probed. The S5
  generator's ground truth does not depend on the argument.
- `check_plan_refs.py` reports `RESULT: FAIL` for 9 paths and 16 symbols, all of them files and members this plan
  creates, plus `FormattingHandler` / `RangeFormattingHandler` (file names; the classes are `Sharpy…Handler`), and
  two CLOSED issues cited as history (#2062, #2263). None is an error.
