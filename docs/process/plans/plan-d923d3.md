<!-- Verified by /verify-plan on 2026-10-08 @ 926670989 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: existing (S6 route-parity sweep, extended FIRST in Phase 1) -->

# P22g — indent-only routes past a lexer stop or a re-pair (#2273 #2274 #2275 #2276)

## Context

P22f (plan-add686, #2271, closed 2026-10-08) gave the lexer a literal-loss fact (`Lexer.LiteralLoss`, four
producers) that every indent-only LSP route reads through `IndentationService.BuildIndentMap` → `LiteralStateUnknown`
→ `FormattingEdits.CheckedIndentOnly` / `OnTypeFormattingHandler`: while the lexer has lost a literal that can span
lines, format on type and the Format Document / Format Selection fallbacks apply nothing. Its sweep (S6 in
`FormattingRouteParitySweepTests`) and its verify round left four residues, batched as **P22g** (charter
`.claude/plans/remediation-round-2026-09-20-batching.md` §3.1, rulings R-FP R-FQ R-FR R-FT, 2026-10-08):

- **#2273** (R-FP, FIRST) — the error budget. `BuildIndentMap` lexes with the compiler's `MaxErrors` (25); every line
  past the stop has no token, the map reads each as a dropped CODE line that starts a logical line, and a bracket
  continuation at column 0 pops every block: Format Document moves a method body out of its method. Not a literal
  loss — `UnreadRemainder` fires only when the unread text holds a literal. 334 `S6n depth` allowlist rows.
- **#2274** (R-FQ) — re-pair shapes with the lexer signature of a document that loses nothing (`DStringRepaired`):
  a stray triple re-paired through a comment, two stray bare delimiters, an opener inside an unterminated short string
  or backtick name. PERMANENT known-limit tracker (never closes). 20 `S6k literal` rows (on-type / range-line rewrite
  content of `b"""`/`r"""` literals whose closer sits at column 0).
- **#2275** (R-FR) — `RePairedCloser` arm (b) keys on the four unterminated-string codes on the close line of a
  re-paired run; an orphan `t = '"""$'` closes the run, SPY0015 drops the rest of the line, the trailing `'` is never
  read, the fact stays `None`, and full / range-line / on-type rewrite the string's content. 21 documents over
  SPY0015 / SPY0018 / SPY0007 / SPY0008 / SPY0022.
- **#2276** — the scanner `HoldsALiteralSpanningLines` is a hand model of the lexer; at the literal's own quote inside
  a format spec the lexer aborts SPY0022 and drops the line (opener included), the scanner closes the f-string there and
  pairs the rest of the line the other way (`" + "` then `""` swallow the triple), returns false, `DroppedOpener` is not
  set, and range-line / full rewrite `key: value`.

Links: https://github.com/antonsynd/sharpy/issues/2273 · /2274 · /2275 · /2276. Owner rulings (authoritative, do not
re-ask): #2273 comment 6061576581 (R-FP a), #2274 comments 6051094247 + 6061577042 (R-FQ a), #2275 comment
6061577397 (R-FR b), #2276 comment 6061577768 (no ruling needed; the plan measures the seam), charter §4 R-FT (order).

## Current State (measured @ 926670989 = dev HEAD; the built `Sharpy.Lsp`/`sharpyc` binaries are @ 729bf1e7d, and 926670989 touched no compiler or LSP source)

Reproduced over stdio with `.claude/tmp/p22e-probes/lspfmt.py` (cases in this session's scratchpad `cases*.json`):

| cell | route | observed @ 729bf1e7d |
|---|---|---|
| #2273's program (25 × `x = "abc`, then `class Foo:` / `    def method(  # c1` / `self) -> None:` / `        ...`) | full, range-whole | `        ...` → `    ...` (the body leaves `method`) |
| N1c (25 errors then a 2-space body) | full | 30 lines repaired to 4 spaces (direction control, keeps) |
| SQ2 `def main():\n    x = f"{x:">}" + """\n        key: value\n        f"{x:">}" + """\n    print(x)\n` | range-line 2 | `        key: value` → `    key: value` |
| SQ3 (module-level twin) `x = f"{x:">}" + """\n    key: value\nf"{x:">}" + """\nprint(x)\n` — the closer line at COLUMN 0 [CORRECTED: spelling pinned from the issue; with the closer line at 4 spaces full applies nothing (refused for another reason), so the route cells must use this text] | full, range-line 1 | `    key: value` → `key: value` (re-driven by the verifier @ 729bf1e7d) |
| #2273's program with 24 error lines (one under the budget) [ADDED: measured by the verifier @ 729bf1e7d] | full | ONE edit: `self) -> None:` → `    self) -> None:` (the column-0 continuation line is re-indented to 4 spaces); `        ...` keeps its 8 spaces — NOT "no edits". This applied text is the discriminating expectation for the 25-error program after R-FP |
| #2275's N2 shape, orphan tails `$'`, `0x'`, `` `' ``, `1__2'`, `f' + "{x:" + str(y)`, ` $ '` (content at 6 spaces) | full, range-line 3, on-type 3 | every tail: `      key: value` → `key: value` / `    key: value` / `key: value` (18 of the 21 cells re-driven; the fact is `None`) |
| control: no stray, closer line `    """ + $` (also `+ 1__2`, `+ 0x`; `+ 'b` is NOT a nothing-lost control — an unterminated short string on the close line sets `RePairedCloser` through the existing arm (b) today, so it is that arm's positive control [CORRECTED]), then `      print(s)` | on-type 4, full | **no edits today** — refused by the map's continuation clause (see below), not by the fact (re-driven by the verifier: `+ $` on-type 4 → no edits; with `    q = 1` between → repaired to 4 spaces) |
| control: the same, with a clean `    q = 1` between the close line and `      print(s)` | on-type 5, full | repaired to 4 spaces (keeps) |
| M3 `def main():\n    x = \`abc + """\n        key: value\n    """  # """\n    print(x)\n` (#2274) | full | `        key: value` → `    key: value` (known limit; stays) |

Lexer facts read with the built `sharpyc emit diagnostics`: the N2-dollar document reports SPY0013 (4:1) then SPY0015
(6:13) — the abort on the close line is an unexpected character, outside arm (b)'s code list; the control reports
SPY0007/SPY0015 at 4:11 then SPY0013 (5:1).

**A pre-existing cell outside this contract, found while measuring the direction controls:** the indent map reads the
line IMMEDIATELY after any error-dropped line as a continuation of that logical line (`RecoverFromError` consumes the
line break without emitting a `Newline` token, so `BuildIndentMap`'s token walk never resets `atLogicalLineStart`):
`def main():\n    x = $\n      y = 1\n` — on-type 2 and full apply nothing; with one clean line between, the repair
returns. Lane `tooling` · reach `program` (a lost repair, never a wrong edit). It goes to the findings ledger and, at
the wave, as a comment on #2273 (the indent-map tracker of this group) — not an issue (contract §10). It changes the
spelling of R-FR's direction control (the on-type line must not be the line right after the erroring close line).

Instruments @ 729bf1e7d (P22f's verify record): FULL route-parity sweep 16475/0/0, 354 allowlist rows (334 `S6n
depth` citing #2273 = 167 stems × {full, range-whole}; 20 `S6k literal` citing #2274 = 2 stems × 5 twins × {ontype,
range-line}), `factMissing` 0, `factSpurious` 0, census shape `S6 budget` 75/75 abort-free documents, `S6n budget`
16373 documents, sweep-time 218–248 s in FULL mode. `rows.Count.Should().Be(354)` anchors the allowlist.
`LiteralLossScannerDifferentialTests` green (clean documents only — an erroring line is outside its oracle).

Code facts the design rests on (read this round):

- `IndentationService.BuildIndentMap` (`src/Sharpy.Lsp/IndentationService.cs:25`) constructs `new Lexer(source)` with
  the default `MaxErrors = 25` and reads `lexer.LiteralStateUnknown`, the tokens, `LiteralSpans`, and the count of
  SPY0013/SPY0014. Its five callers are the whole indent-only surface: `OnTypeFormattingHandler:50`,
  `FormattingFallback.ReindentDocument:27`, `FormattingFallback.IndentOnlyPreserved:96-97` (before/after),
  `FormattingEdits.CheckedIndentOnly:80`, `RangeFormattingHandler:81`. The sweep reads the fact through the same call
  (`SweepDocument.LiteralState`) and the MECHANISM through its own `new Lexer(Text)` "constructed as BuildIndentMap
  constructs its own" (`FormattingRouteParitySweepTests.cs:322`, `LexerLiteralLoss`, feeds `IsAbortFree`).
- `Lexer.TokenizeAll` (`Lexer.cs:313-370`): on every abort, if `ErrorCount >= MaxErrors` the rest of the source gets no
  token (the `UnreadRemainder` hook + SPY0905), else `NoteDroppedSpan` then `RecoverFromError` (skips to the next line,
  resets the indent stack, brackets, pending tokens and the f-string stack). The budget check is the ONLY use of
  `MaxErrors` in the lexer. `DiagnosticBag` has no cap of its own.
- Every lexer diagnostic but two aborts through `ReportError` (`Lexer.cs:49`) and so drops the rest of its line; the two
  that report without dropping are the dedented-string (`d"""`) indentation errors (`Lexer.Literals.cs:299,338`) —
  "a code that reports without dropping loses nothing and needs no fact" (R-FR) names exactly those.
  **[CORRECTED: the dropped SPAN can be EMPTY even though the line was dropped.]** The backtick-name reader
  (`Lexer.cs:917-936`) advances `_position` character by character and reports SPY0018 only when it reaches the line
  break, and `UnreadTextStart` (`Lexer.LiteralLoss.cs:43-45`) returns `_tokenStart` only for a STRING literal start —
  so for every SPY0018 abort the span `[UnreadTextStart, end of line)` is empty. Measured with the built `sharpyc emit
  diagnostics` @ 729bf1e7d on the N2 shape: SPY0018 at (6:15) on the 14-character line `    t = '"""`'`, at (6:21) on
  `    t = '""" see `x'`, at (6:24) on `    t = '"""x' + `it's`` — the line end each time (SPY0007/SPY0008 at (6:13):
  `_position` is inside the number, before the trailing `'`; SPY0015 at the `$`; SPY0022 at the bracket via
  `ReportUnclosedField`, which restores `_position` and `_line` to it). Six of #2275's nine orphan rows are SPY0018
  (R-bt, R3-bt, R2, R-btname, R2-bt-mid, IB-bt): Design 2's quote rule cannot fire on an empty span, so the span start
  must be extended to a backtick name's own start (Design 2 ii′), and R-btname stays un-cured by any span rule (Design 2).
- Arm (b) of `RePairedCloser` lives in `ReportError` (`Lexer.cs:63-66`): `line == _lastMultiLineCloseLine && code is
  SPY0001/0002/0003/0026`. Arm (a) is in `NoteMultiLineLiteralClosed` (`Lexer.LiteralLoss.cs:66-72`): the closer is
  immediately followed by a quote character. `NoteDroppedSpan` (`Lexer.LiteralLoss.cs:22-30`) computes the dropped span
  `[UnreadTextStart, end of line)` and sets `DroppedOpener` when the scanner says it holds a literal spanning lines;
  the budget hook reads from the same `UnreadTextStart` (fc626bcb2) but over the rest of the source.
- `NextToken` dispatches to `NextFStringToken` (`Lexer.cs:511`) BEFORE `_tokenStart = startPosition` (`:556`), so during
  an f-string read `_tokenStart` is the `FStringStart`'s start; `UnreadTextStart(fromTheAbortedLiteral: true)` therefore
  returns the f-string's prefix for a mid-f-string abort and the scanner re-reads the whole literal — the mechanism
  #2276 describes. The spec abort is `ReportFieldEndedByQuote` (`Lexer.FStrings.cs:569-576` [CORRECTED: line refs]): on the field's own line it
  is `ReportError(SPY0022)` with `_position` AT the quote and `_resumeAfterUnclosedField` unset; `NextFStringSpecToken`
  (`:1021-1022` [CORRECTED]) raises it when `fsc == context.QuoteChar && (!IsTriple || IsTripleQuoteAt)`; the OTHER quote character
  is spec text in the lexer and in the scanner (hand row `y = f"{x:'}" + 'z'` → false).
- The scanner's spec branch (`Lexer.LiteralLoss.cs:147-185`; the own-quote closer arm at `:161-166` [CORRECTED: line refs]): `c == literal.Quote && (!Triple || IsTripleAt)` closes the
  literal; it is the one arm #2276 refutes. Its expression branch already models the lexer's "expecting '}'" rule
  (`y = f"{x" + 'z'` → false) with a lookahead (`ClosesOnItsLine`).
- `FormatterTwins.LiteralLossShapes` (`src/Sharpy.TestInfrastructure/Formatting/FormatterTwins.LiteralLoss.cs`) is the ONE
  recipe both `LiteralStateTests.Corpus_EveryAbortFreeLossShape_SetsItsMechanismsFlag` (`:623`) and the sweep's S6 (`:604`) derive their
  documents from (its only two callers today; `IndentationServiceLiteralStateTests` reads hand documents from
  `FormattingFallbackTests` [CORRECTED: Design 6's "three consumers" is two today — the third is Phase 1's addition]): `budget` (25 × `x = "abc` above Q), `repair` / `repair-comment` (a stray triple as line 0, last line
  `_ = '"""'` or `# """`), `dropped-SPY00NN`. `FormattingRouteParitySweepTests.LossState` maps shape → state (S6 / S6n /
  S6k), `StateIssue` maps state → the one issue its rows may cite (S6 → #2271 — CLOSED, so no S6 row can be added
  today; S6n → #2273; S6k → #2274); the loader refuses a row citing another state's issue.
- `FactOracles` exempts S6k from the FACT buckets only; its route buckets (`literal`) are judged and allowlisted.
- `docs/tooling/formatter.md` "In the editor" bullet (line 125) lists the loss mechanisms including "when the lexer
  stopped at its limit of 25 errors before reaching it", and line 229 says a file with ≥ 25 lexer errors above a string
  "is treated the same way" — both become false under R-FP. `FormatterDocEditorExamplesTests` executes the
  `<!-- editor-example -->` blocks (none is a budget example).

## Scope

- **Group:** P22g — indent-only routes past a lexer stop or a re-pair (charter §3.1; P22f residue). Chores riding
  along: none.
- **Alone:** no — LSP edits and lexer facts only; by design NO snapshot moves (charter §4 "snapshot/regen-sensitive":
  P22g).
- **Gates:** 1 (the whole solution at the last commit; a filtered run is the edit loop).
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`); the lead files by
  class (contract §10); trackers named at the end of this plan. No implementer runs `gh issue create`.
- **Override:** none.
- **Verify cost / prober budget [ADDED: missing step]:** FULL-mode sweep ≤ 1.5 × 248 s per run, run 4× (Phase 1
  populate, Phases 2–3 drains, Phase 5 record); the direction driver over ~130 documents × 4 routes on two trees; one
  whole-solution gate (~30 min). Prober's budget at verify: this matrix plus at most two axis changes (the charter's
  §10), findings to the ledger.

## Defect Class

- **Violated contract:** the indent-only routes (format on type, the Format Selection and Format Document fallbacks)
  edit only text the lexer read as the user wrote it — no line that is string content in the document the user wrote,
  and no statement moved to another block, including in the part of the document the lexer did not read. Two seams
  carry it: the indent map's token source (R-FP) and the lexer's literal-loss fact, which must be true for EVERY
  mechanism that loses a literal spanning lines (R-FR, #2276).
- **Meta-class:** silent wrong behavior (string content rewritten; a body moved out of its method) + two **arms of a
  shared seam** keyed on an enumeration (arm (b) on four codes; the scanner's spec branch on "own quote = closer") →
  **standard cure:** fix at the seam (the map lexes the whole document; the arm reads the loss MECHANISM — a dropped
  span holding a quote on the close line — not a code list; the scanner models the lexer's abort), then the existing
  generator (the S6 sweep) gains the two axes the prober used so a sixth code or a second spec-abort shape cannot recur
  unseen.
- **Known member cells:** #2273's table (the issue program × {full, range-whole}; 167 corpus stems' comment twins);
  #2275's 21 documents × {full, range-line, on-type} over 5 codes (table in the issue; 18 re-driven above); #2276's
  SQ2 / SQ3 × {full, range-line} (SQ1 the control); #2274's 20 S6k rows, X9c, X9s, X1a, XSW, M3 (known limits, not
  cured).
- **Sibling cells this plan must also cover (the matrix):**
  - mechanism {budget stop (R-FP) × **where the 25th error lands within its line** {line end `x = "abc` (today's
    shape), inside a short string aborting mid-line `x = "\q" + 1` (SPY0004), inside a replacement field `x = f"{a $ b}"`
    (SPY0015), an indentation error dropping the line whole `  x = 1` (SPY0013)} × what follows {a bracket continuation
    at column 0 (#2273), a 2-space body (N1c/N1s), a multi-line literal (S6 budget, 75 documents), a 2-space body then a
    multi-line literal (N1t), nothing lost};
  - re-pair with **an abort on the close line** (R-FR) × code {SPY0015, SPY0018, SPY0007, SPY0008, SPY0022; the four
    unterminated codes SPY0001/0002/0003/0026 as the existing 33 theories; the two non-dropping dedented-string reports
    as the "reports without dropping" control} × the dropped span {holds a quote (the orphan's trailing `'`), holds no
    quote (`""" + 1__2`, `""" + $`: the direction control, nothing lost), **EMPTY (every SPY0018: the backtick reader
    reports at the line end — the span must start at the backtick name's own start, Design 2 ii′) [CORRECTED]**} ×
    **where the orphan's quote went {in the dropped span (R-bt `t = '"""`'`, R2 `t = '""" see `x'`, R2-bt-mid
    `t = '"""`x'`, IB-bt — cured once the span starts at the backtick); READ as a short string's opener and closed by a
    later apostrophe (R-btname `t = '"""x' + `it's``: the lexer reads `' + `it'` as a String token, the aborting backtick
    is the LAST one, its span `` ` `` holds no quote — the same lexer signature as the no-stray control `""" + `x``, so by
    R-FQ's rule it is a #2274 known-limit cell under R-FR (b), not a #2275 cell) [ADDED: adequacy]}** × stray
    {present at line 0 (the N2 shape), present INSIDE the block at line 1 (IB-bt, #2275's table), absent (the direction
    control's document)} — #2275's table has NINE orphan spellings (incl. `t = '""" costs $5'`, `t = '""" $ '`) plus
    the IB-bt placement; the recipe's six tails are the generator's axis, the issue's rows are the hand route cells;
  - a quote inside a format spec (#2276) × spec text before the quote {none `f"{x:"`, some `f"{x:">}"`} × what follows
    on the line {nothing (being typed), a closed f-string, `+ """` (SQ2/SQ3), `+ 'z'`, a comment holding `"""`} ×
    enclosing kind {single-quoted f-string, triple f-string (already `AbortInsideLiteral`), t- and df-strings} × **the
    other mid-literal abort codes** the scanner reads as structure {SPY0020 `x = f"{x" + '''` — the OTHER triple
    [CORRECTED: `x = f"{x" + """` is NOT SPY0020 — the lexer's prescan (`Lexer.FStrings.cs:764-771`) finds the closing
    `"` later on the line and reads `" + "` as a nested string; measured @ 729bf1e7d it reports SPY0022 `'{' was never
    closed` at the bracket via `ReportUnclosedField`, and full applies no edits — a scanner-agrees control. SPY0020
    `expecting '}'` fires only when no `"` follows on the line, so the dropped text can hold only the other triple
    `'''`; the scanner's `ClosesOnItsLine` arm already returns true there — a control], SPY0021 (an unmatched `}`:
    `x = f"}" + """`), SPY0030 `f"{x!q}" + …`} × {`+ """` after, clean close after};
  - × route {on-type, range-line, range-whole, full} × document {every corpus stem under the S6 injections ×
    5 twins; the hand documents in each issue}.
- **[ADDED: implementation, in-contract cells found by Phase 1's generator — fixed at the seam in e16e323eb]** error
  recovery emits no `Newline`, and two consumers reset per-line state only at one: (i) `LiteralSpans.Of` pairs an
  f-string abandoned by recovery with the NEXT f-string's end, so a triple f-string on the next line, whose field
  spans lines, loses its span and its field line is rewritten (S6x factMissing/literal on
  `fstrings/multiline_hole_2022` and its t-string twin, closeline-SPY0022); (ii) the line after an error is read as a
  continuation, so `IndentOnlyPreserved` never judges its depth and an on-type/range-line re-indent of the line
  above moves it a block deeper (S6x depth on the wide twin of `strings/d_string_raw` and
  `strings/d_string_trailing_newline`, every close-line code). Cure: `Lexer.RecoveryResumes` (no token change),
  `LiteralSpans.Of(tokens, recoveryResumes)` resets there, `BuildIndentMap.HiddenAfterRecovery` lines are
  depth-judged. The lost-repair facet of the same clause (Current State) stays outside the contract (#2273 comment).
- **Lane / reach:** silent-wrong · program; **fidelity-scope row:** N/A — an editor surface, not a Python-parity cell.
- **Standing harness that should have caught it:** `FormattingRouteParitySweepTests` S6 (states S6n/S6k) and
  `LiteralLossScannerDifferentialTests`. The sweep's S6 had ONE budget shape and ONE re-pair closer line, so the prober's
  two axis changes (the close-line abort code; where the budget stop lands) found the cells. The differential's oracle is
  the clean lex, so a divergence on an erroring line (#2276) is outside it by construction — the scanner's hand rows are
  the only guard there.
- **Generator: EXISTS — extended FIRST.** Phase 1 adds the two axes to `FormatterTwins.LiteralLossShapes` (one recipe,
  three consumers: the sweep, `LiteralStateTests`' corpus theory, and `IndentationServiceLiteralStateTests`) and new
  states/cites so the rows can be allowlisted before the cures land; the cures (Phases 2–4) drain them to zero; the S6k
  rows become a census pin (R-FQ). The hand matrices are reserved for the axes the generator cannot enumerate: **which
  lexer flag fired** (`LiteralStateTests`, lexer level) and **the scanner's verdict on an erroring line**
  (`LiteralLossScannerTests` — no clean-lex oracle exists for it).
- **Owner rulings:** listed under Context. R-FP (a): the map gets real tokens for the unread tail — "the budget caps
  diagnostics, not structure"; two-way, can tighten to (b) "refuse below the stop line" for cells the tail cannot
  repair. R-FQ (a): the S6k rows leave the allowlist and become a census floor; `formatter.md` names the shape; rejected
  (b) a content-level heuristic (every candidate also matches a module docstring). R-FR (b): arm (b) keys on "a dropped
  span holding a quote character on the close line"; the plan measures which of the five codes drop a span; direction
  record `""" + 1__2` keeps on-type; two-way, widens to (a) by dropping the quote condition. R-FT: P22g directly before
  P47 in P22f's slot.

## Adversarial Review (pre-mortem)

- **Alternative root cause (#2273):** not the budget but the map's "dropped code line starts a logical line" rule. The
  discriminating probe: the issue's program with 24 error lines above it (one under the budget) — full applies ONE edit
  that moves no statement: `self) -> None:` → `    self) -> None:` and `        ...` keeps its 8 spaces [CORRECTED:
  measured by the verifier @ 729bf1e7d; the plan said "apply nothing wrong" — true in the contract's sense, but the
  expected output is this applied text, and "no edits" would not discriminate]. Lexing the tail is therefore
  sufficient; the rule stays. A second alternative — the continuation line is misread because the `Newline` of the
  erroring logical line is never emitted — is the pre-existing cell named under Current State and is NOT what moves the
  body (the body's lines are past the stop with no tokens at all).
- **Alternative layer:** the lexer itself could "continue past the budget without reporting" for every consumer (a lexer
  mode). Rejected: the compiler's budget bounds the parser's cascade on a garbage file and the CLI's printed
  diagnostics; changing it moves CLI output (a layout-changing phase that would have to land alone, contract §11). The
  map is the ONE consumer that wants structure, so the seam is the map's lexer construction — LSP-internal, no carrier
  (charter §4). Rung: 3 (a construction rule keyed on the consumer), no lower rung applies (no `.spy`, no Core type).
- **How the fix could be inert (R-FP):** `BuildIndentMap` is called five times per route (the map, the applied-text
  map, the preserved check's before/after); if only one construction site changes, the before/after maps disagree
  and `IndentOnlyPreserved` refuses every edit (the S6n rows would "drain" by refusal while N1c/N1s LOSE their
  repairs). Cure: one factory (`IndentationService.StructureLexer(source)`), every site and the sweep's mechanism read
  through it; N1c/N1s are the mutation's positive control.
- **How the fix could be inert (R-FR):** arm (b) moved to `NoteDroppedSpan` but the budget hook in `TokenizeAll` still
  reads the four-code list (or nothing) — a stop that lands on the close line misses the fact. Cure: ONE
  `NoteUnreadText` both hooks call (P22f's "one UnreadTextStart for both hooks" extended to the close-line rule); a
  lexer hand row where the 25th error is the orphan on the close line.
- **How the fix could be inert (#2276):** the scanner arm lands but the span fed to it starts at `_position` (the quote)
  on some path, where the OLD scanner already returned true — the hand row on the scanner is green either way and the
  route cell is what discriminates. Cure: SQ2/SQ3 are asserted at THREE levels: the scanner on the exact span
  `f"{x:">}" + """` (false today → true), the lexer's `LiteralLoss` on the document (`None` → `DroppedOpener`), and the
  route (edit → none).
- **Before/after by direction:** every route cell this plan changes is classified against 729bf1e7d by running the
  in-process driver (`LspFormattingDriver`) on both trees (a `git worktree` at 729bf1e7d): (1) the S6 `budget`
  documents — 75 documents whose literal is now READ: they go from "refused by the fact" to "judged by the `literal` /
  `content` / `depth` buckets" (a gain when green; any row is a finding IN the contract, fixed at the seam — R-FP's
  "tighten to (b)" escape is taken only for a cell the measured tail cannot repair, with the owner's words); (2) S6n
  (edits that move a block → no block moves; N1c/N1s/N1t's code lines keep their repairs); (3) #2275's 21 documents and
  SQ2/SQ3 (edits → none); (4) the direction controls: `closeline-<code>-then-clean` (keeps), `f"{x:"` being typed
  (nothing set, keeps), `f"{x:">}"` closed (keeps), `DStringRepaired` (keeps its edits); (5) N1 / BSesc / N1t: today
  "no edits" (the fact); after R-FP the literal is read, the literal lines are byte-identical, and misindented CODE
  lines are repaired — a gain, recorded as such (their tests are re-spelled, see Phase 2 Task 3; this is a design
  change under an owner ruling, not an expectation edited to pass).
- **Blast radius:** `Sharpy.Lsp.Tests` whole (the sweep, `FormattingFallbackTests`, `IndentationServiceLiteralStateTests`,
  `FormattingEditsTests`, `OnTypeFormattingTests`, `FormatterDocEditorExamplesTests`, `WorkspaceAndFormattingTests` E2E,
  `FrontEndParityTests`) and the lexer half of `Sharpy.Compiler.Tests` (`LiteralStateTests`, `LiteralLossScannerTests`,
  `LiteralLossScannerDifferentialTests`, `LexerTests`, the spec-block gate). The lexer change records a flag only — "it
  changes no token, span, trivia or diagnostic" — and the regression control is a corpus differential of `sharpyc emit
  tokens` + `emit diagnostics` BASE vs HEAD (Phase 5). No Semantic/CodeGen change: Stdlib.Tests, Cli.Tests and the three
  GapDiscovery sweeps are unreached (they still run in the gate).
- **Generated artifacts touched:** none (no spy-stdlib, spy-test, stdlib-docs or oracle-ledger input changes); "regen: no
  diff @ sha" is still recorded at the gate.

## Design Decisions

1. **The map lexes the whole document (R-FP, rung 3 — an LSP construction rule).** `IndentationService` gains one
   internal factory `StructureLexer(string source)` returning `new Lexer(source) { MaxErrors = int.MaxValue }` (no trivia,
   as today) with a doc comment citing #2273/R-FP: the lexer's budget caps the compiler's diagnostics; the indent map
   needs structure, so it reads every line. All five route sites already go through `BuildIndentMap`; the factory is
   the one place the lexer is built, and the sweep's mechanism read (`LexerLiteralLoss`) calls the same factory. The
   lexer's `UnreadRemainder` producer stays (the compiler's lexer still stops; the fact's contract is lexer-level and
   `LiteralStateTests` keep their theories); `Lexer.LiteralStateUnknown`'s doc gains "(the editor's indent map lexes
   without a budget, R-FP)". Alternatives rejected: (b) refuse every edit below the stop line (N1c/N1s lose their
   repairs — the fallback's main use case); (c) a lexer-wide mode (CLI output moves). Cost: a pasted document of N error
   lines costs N diagnostics in a bag nobody reads — linear, same order as lexing a clean document of N lines.
2. **Arm (b) reads the loss mechanism at the dropped-span seam (R-FR, lexer-internal).** The four-code test in
   `ReportError` is deleted. `NoteDroppedSpan` and the budget hook both call one `NoteUnreadText(int start)` that (i)
   computes the rest of the current line `[start, end of line)`; (ii) when `_line == _lastMultiLineCloseLine` and that
   text holds `"` or `'`, records `RePairedCloser` (a closer that re-paired with an orphan quote the lexer never read:
   R-FK's dropped-line rule applied at the close line); (iii) records `DroppedOpener` / `UnreadRemainder` from the
   scanner over its hook's extent as today. Every abort through `ReportError` drops the rest of its line, but the SPAN
   `[UnreadTextStart, end of line)` is EMPTY for SPY0018 [CORRECTED — see Current State: the backtick reader reports at
   the line end]; so **(ii′) `UnreadTextStart(fromTheAbortedLiteral: true)` also returns `_tokenStart` when the token
   being read is a backtick-delimited name that began on this line** (`IsBacktickEscaped` start at `_tokenStart`; the
   lexer read it as opaque, exactly the string-literal rule P22f wrote for `"\q" + """`), so the span of R-bt is `` `' ``,
   of R2 `` `x' ``, of R2-bt-mid `` `x' `` — each holds a quote. Measured by the hand theories, not assumed; the two
   dedented-string reports do not abort and record nothing (their control theory: a `d"""` closer line with a
   less-indented content line records `None`). **R-btname (`t = '"""x' + `it's``) is NOT cured by (ii)+(ii′):** the orphan
   `'` was READ as the opener of the String token `' + `it'`, the aborting backtick is the last one and its span `` ` ``
   holds no quote — the lexer signature of the no-stray control `""" + `x`` (closeline-SPY0018, nothing lost). Under
   R-FR (b) it is a shape no span rule can see → by R-FQ's standing rule a comment on #2274 at close-out, with its
   direction twin named; #2275's route cells are the other 20 documents. (The owner's two-way door — widen to (a) "any
   abort on the close line" — would cure it at the cost of the closeline-* controls' on-type repairs; the plan does
   not take it. Record `override:` under Scope if the owner rules otherwise.) [CORRECTED: changes #2275's close
   criterion, see Issues to Close.] The
   subsumption of the old list is proven by the 33 existing theories staying green with the list gone (mutation: restore
   the list and remove the quote rule → the 21 new theories red; drop the quote condition → the S6x direction rows and
   the `""" + 1__2` theory red).
3. **The scanner models the spec-quote abort as a quote of unknown role (#2276, lexer-internal).** At the literal's own
   quote inside a format spec (non-triple, or a triple at a triple), the lexer aborts and reads nothing more on the line.
   The scanner abandons every open literal there and returns true iff a FRESH walk from the quote (the quote opens a
   literal: `">}" + """` → `">}"` closed, then an opener) OR a fresh walk from just past it (the quote closed the
   literal: `>}" + """` → `" + "`, `""`) holds a literal spanning lines. Rationale: after the abort the user's text past
   the quote has no lexer reading; the two walks are the two roles the quote can play; the fact is a conservative
   detector (a missing fact is a silent wrong edit, a spurious one a lost repair), so the OR is the honest model. It
   gives SQ2/SQ3 true, `x = f"{x:"` being typed false (both walks: an unterminated short string ends at its line —
   no on-type refusal while typing a spec), `f"{x:">}"` false, `f"{x:">}"  # """` false, `f"{x:" + """` TRUE (the sibling
   found by one axis change: no spec text before the quote — covered, not a known limit). The fresh walk from a position
   is a pure function of the position; implement it as the existing loop started at `p` with the result memoized per
   start position so a line with k such aborts is bounded by O(k·n) — at most two fresh walks per abort position, each
   at most the rest of the text [CORRECTED: "stays linear" overstated it; bounded, not linear] (a hand row with 40
   spec-quote aborts on one line is the complexity control). Where the enclosing read is triple-quoted or a field crossed a line, `AbortInsideLiteral` already
   fires at `ReportError`; the scanner's verdict is moot there and the hand rows say so. Alternatives rejected: "the quote
   is spec text" (the escape the user omitted) — `f"{x:"` being typed sets the fact (an open field at the line end);
   "restart after the quote only" — SQ2 false; "re-read the quote only" — `f"{x:" + """` false (a known-limit row the
   OR removes).
4. **The S6k rows become a census pin, not an allowlist (R-FQ).** The sweep stops reporting the `literal` bucket of an
   S6k cell as a failure and counts it instead (`known-limit S6k literal <route>`); `Census_EveryRouteAndStateHasCells`
   pins the counts per route EXACTLY as measured in FULL mode (ontype 10, range-line 10, full 0, range-whole 0 @ the
   commit that lands it) and guards the pin on "the S6k theories ran over every stem in this process" (the refusal
   ceilings' guard). Exact, not ≥: a shrink is a fix that must be recorded (a cell closed on #2274), a growth a new
   known-limit cell (a comment on #2274) — "counted; may not shrink unmeasured" in the charter's words, and never
   silently grows either. S6k's other buckets (`edits`, `net`, `content`, `depth`) stay judged; its FACT exemption stays.
   The allowlist anchor becomes `rows.Count.Should().Be(0)` with the drain history in its reason.
5. **Two new sweep states, one issue each (the loader's rule).** `S6r` — the repair shape with an abort on the close
   line (`repair-SPY00NN`; cites #2275); `S6x` — the close-line abort with NO stray, nothing lost (`closeline-SPY00NN`;
   cites #2275; its only legitimate row is `factSpurious`, which an over-wide arm (a) would produce — the direction
   control as a sweep state). The budget-position shapes keep the budget states (`budget-<position>` → S6 when Q has a
   literal spanning lines, S6n otherwise; `LossState` keys on the `budget` prefix). `StateIssue` gains the two states;
   `S6 → #2271` stays (closed; no S6 row may be added — any S6 failure after R-FP is a finding fixed in this plan, never
   a row).
6. **One recipe, three consumers (two today — the sweep `:604` and `LiteralStateTests` `:623`; `IndentationServiceLiteralStateTests` joins here [CORRECTED]).** The new shapes are built in `FormatterTwins.LiteralLossShapes` so the sweep,
   `LiteralStateTests.Corpus_EveryAbortFreeLossShape_SetsItsMechanismsFlag` (expected flag per shape: `budget-*` →
   `UnreadRemainder` under the compiler's lexer; `repair-SPY00NN` → `RePairedCloser`; `closeline-*` → a direction
   control: `None`) and `IndentationServiceLiteralStateTests` read the same documents.
7. **Docs are deliverables.** `docs/tooling/formatter.md` "In the editor": the budget clause leaves the loss list; a new
   sentence says the editor reads past the compiler's 25-error limit; the R-FQ sentence names the shapes no lexer fact can
   see (a stray `"""` re-paired through a comment, two stray bare delimiters, an opener inside an unterminated short
   string or backtick name) and that the indentation-only pass may re-indent string lines there — a limit, stated, with
   no executable example (an example would pin the wrong edit). `gap-discovery-contracts.md`'s route-parity row gains the
   two axes, the states, the pin, and "allowlist 0 rows".

No `SemanticInfo`/`CodeGenInfo` carrier is added (charter §4); no new SPY code; no spec file changes (the lexer fact is
not language).

## Implementation

### Phase 1: Generator FIRST — the two axes, the new states, the S6k pin

**Goal:** the sweep enumerates the prober's two axes over the corpus and the S6k rows become a measured pin, so Phases
2–4 drain rows instead of hand cells; the allowlist is re-populated from a FULL-mode run with every row citing its
tracker.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: FULL-mode `FormattingRouteParitySweepTests` green with the allowlist re-populated:
  the 334 S6n rows + the new `budget-<position>` rows (S6n `depth`, citing #2273) + the new `S6r` rows (citing #2275);
  `S6x` has 0 rows; the census prints `shape S6r repair-SPY00NN=…` with ≥ 1 abort-free document per code (floor) and
  `known-limit S6k literal ontype=10 range-line=10 full=0 range-whole=0`.
- Guard mutation: (a) delete the `known-limit` exemption → the 20 S6k cells fail as unlisted rows (the pin counts real
  cells); (b) change a pinned count → red; (c) remove one `repair-SPY00NN` shape → the per-code floor red; (d) the
  existing `PositiveControl_FactSpurious_FiresOnAStubbedFact` stays the fact buckets' positive control.
- Allowlist entries that drain: none in this phase (rows are ADDED, each citing #2273 or #2275; the 20 S6k rows are
  DELETED — replaced by the pin, not drained).
- Execution evidence: `FMTROUTE-CENSUS` lines of the FULL run @ sha; the row counts per state; sweep-time (bounded:
  ≤ 1.5 × 248 s in FULL mode — the new shapes multiply S6n's 16k documents by 4; if over, sample the budget-position
  shapes by stem stride like S1/S2 and say so).

#### Tasks

1. **The close-line axis in the recipe** — `src/Sharpy.TestInfrastructure/Formatting/FormatterTwins.LiteralLoss.cs`
   - Add `CloseLineCodes` = {SPY0015 `$'`, SPY0018 `` `' ``, SPY0007 `1__2'`, SPY0008 `0x'`, SPY0022 `f' + "{x:" +
     str(y)`} as (code, tail) pairs — the tail is appended to the orphan `_ = '<triple>` in place of the closing `'`
     (the issue's table; the quote character follows the literal's as the `repair` shape does). Shape names
     `repair-SPY00NN` (`RepairCloseLinePrefix`). Built when Q has a literal spanning lines, like `repair`.
   - Add `closeline-SPY00NN`: NO stray; the FIRST multi-line literal's closer line gets ` + <tail-without-quote>`
     appended (`$`, `` `x ``, `1__2`, `0x`, `f"{x:`) — nothing lost; `ReindentedLines` empty; `Kind` = the literal's.
   - Extend `LiteralLossShapeNames`; keep every existing shape byte-identical (the existing rows must not move).
   - Commit: `test(formatting): the literal-loss recipe gains the close-line abort axis — a re-pair closed by an orphan whose line aborts at SPY0015/0018/0007/0008/0022, and the no-stray control (refs #2275)`

2. **The budget-position axis in the recipe** — same file
   - `BudgetStopLines` = {`x = "abc` (line end; today's), `x = "\q" + 1` (SPY0004 inside a short string, no opener),
     `x = f"{a $ b}"` (SPY0015 inside a field), `  x = 1` (SPY0013, the line dropped whole)}; shapes `budget` (unchanged
     name for the first) and `budget-midstring` / `budget-field` / `budget-indent`: 24 × `x = "abc` then the 25th line.
     Built for EVERY Q (the S6n direction control needs them too).
   - `LiteralStateTests.Corpus_EveryAbortFreeLossShape_SetsItsMechanismsFlag` (no ratchet, so only shapes that are
     green today join here): expected flag `UnreadRemainder` for every `budget*` shape (the compiler's lexer still
     stops); `closeline-*` documents are direction controls (`None`), counted like the budget-no-literal ones. The
     `repair-SPY00NN` shapes are SKIPPED by this theory until Phase 3 Task 2 adds them with `RePairedCloser` (their
     rows live in the sweep's allowlist meanwhile — the ratchet is the sweep's, not this theory's).
   - Commit: `test(formatting): the literal-loss recipe gains the budget-position axis — the 25th error inside a short string, inside a replacement field, or an indentation error (refs #2273)`

3. **States S6r / S6x, the budget prefix, the cites** — `src/Sharpy.Lsp.Tests/Conformance/FormattingRouteParitySweepTests.cs`
   - `States` += S6r, S6x (`RoutesByState` like S6); `LossState`: `repair-SPY*` → S6r; `closeline-*` → S6x; `budget*`
     prefix → S6n when `facts.MultiLineLiterals.Count == 0` else S6; `StateIssue[S6r] = StateIssue[S6x] = "#2275"`;
     `LossStateShapes` enumerates the new (state, shape) pairs; `DocumentTallies`/`IsAbortFree` unchanged; the census
     floor "≥ 1 abort-free document" applies to S6r and S6x per shape; the loader tests gain rows for the new states
     (a `S6r` row citing #2273 is refused).
   - `Shaped`: an S6x document's line routes are requested on Q's first logical line inside a block (the S6n rule), since
     it has lost nothing; an S6r document's on every literal line (the S6 rule).
   - Update the allowlist file header (states, cites) and the roster doc comment at the top of the class.
   - Commit: `test(lsp): the route-parity sweep gains states S6r (a re-pair with an abort on the close line) and S6x (its no-stray control) and the budget-position shapes (refs #2273, #2275)`

4. **The S6k known-limit pin (R-FQ)** — same file + `formatting-route-parity-allowlist.txt`
   - In `Judge`/`CellVerdict` for an S6k cell, a `literal` failure is not a failure: it is tallied as
     `known-limit S6k literal <route>` (other buckets unchanged). The census prints `FMTROUTE-CENSUS known-limit S6k
     literal ontype=… range-line=… full=… range-whole=…` and, when the S6k theories ran over every stem on ALL FIVE
     twins (`s_stemsRun` per twin as `AssertRefusalCeilings` does for identity/comment — the 20 rows are 2 stems × 5
     twins, so the guard must cover wide, CRLF and lone-CR too [CORRECTED]), asserts each count EQUALS the pinned literal (doc comment: measured @ sha in
     FULL mode; a shrink closes a cell on #2274, a growth posts one — change the literal in the same commit and say
     which). Delete the 20 S6k rows; `rows.Count.Should().Be(…)` becomes the new total with the reason updated.
   - Mutation record in the commit body: exemption removed → 20 unlisted rows; pin changed → red.
   - Commit: `test(lsp): the comment re-pair's string-content edits are a pinned census count, not allowlist rows — the known limit stays measured while the allowlist trends to empty (R-FQ, refs #2274)`

5. **Populate from a FULL run** — `formatting-route-parity-allowlist.txt`
   - `SHARPY_ROUTE_SWEEP_FULL=1 .claude/scripts/dotnet-serialized test src/Sharpy.Lsp.Tests --filter
     "FullyQualifiedName~FormattingRouteParitySweepTests" --logger "console;verbosity=normal"` → the `FMTROUTE-ROW` lines
     become rows: S6n (`budget-*`, citing #2273), S6r (citing #2275). An S6x row, an S6 row, or a `factSpurious` row on
     any state is a FINDING (ledger), not a row. Update the count anchor.
   - Commit: `test(lsp): allowlist the S6r and budget-position rows from a FULL-mode run — N rows (#2273 …, #2275 …) (refs #2273, #2275)`

### Phase 2: R-FP — the indent map lexes the whole document

**Goal:** a budget stop no longer leaves the map's tail token-less; the S6n rows drain; N1c/N1s/N1t keep (or gain)
their repairs; the S6 budget documents are read and judged.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: every S6n row (334 + the budget-position rows) deleted, FULL sweep green on S6/S6n with
  0 rows and `factMissing`/`factSpurious` 0; `IndentationServiceLiteralStateTests` + `FormattingFallbackTests` green
  with the re-spelled N1-family assertions; #2273's program: full and range-whole apply nothing (the `...` keeps depth
  2) — as a `FormattingFallbackTests` cell and in the direction record.
- Guard mutation: factory left at `MaxErrors = 25` → the #2273 cell red, the S6n rows return (sweep red), the N1-family
  tests red; the sweep's mechanism read NOT routed through the factory → `IsAbortFree`'s budget floor diverges from the
  map (the census asserts the factory is the one construction site: a test that `StructureLexer("…25 errors…")`
  reports > 25 errors and no SPY0905 — the positive control).
- Allowlist entries that drain: all `S6n` rows (file + count anchor).
- Execution evidence: FULL sweep counts @ sha; the direction driver's before/after table for S6 budget (75 documents),
  S6n, N1/N1t/BSesc/N1c/N1s/B1, #2273's program.

#### Tasks

1. **`IndentationService.StructureLexer`** — `src/Sharpy.Lsp/IndentationService.cs`
   - The factory (Design 1); `BuildIndentMap` uses it; doc comment on the map's "lexer's lines" paragraph: a dropped
     code line is now only a line the lexer's ERROR RECOVERY dropped — never a line past a stop.
   - `Lexer.LiteralStateUnknown` / `LiteralLoss.UnreadRemainder` doc comments: the compiler's lexer still stops; the
     editor's map does not (R-FP).
   - Commit: `fix(lsp): the indent map lexes the whole document — the lexer's error budget caps the compiler's diagnostics, not the editor's structure; a bracket continuation past 25 errors no longer pops every block (R-FP, refs #2273)`

2. **The sweep reads the mechanism through the factory** — `FormattingRouteParitySweepTests.cs:322`
   - `_lexerLiteralLoss` constructs via `IndentationService.StructureLexer` (internal; `InternalsVisibleTo` already
     covers the test project — verified: `Sharpy.Lsp.csproj:12` and `Sharpy.Compiler.csproj:30`). Add `StructureLexer_ReadsPastTheBudget` (Lsp.Tests): 30 error lines → 30 errors,
     no SPY0905, a token on the last line. [ADDED: missing step] SPY0905 is a WARNING (`_diagnostics.AddWarning`,
     `Lexer.cs:352`), so the absence assertion reads the warning list, and its positive control is the default
     `new Lexer(source)` on the same 30 lines: 25 errors, SPY0905 present, no token on the last line.
   - Commit: `test(lsp): the sweep's mechanism read and the map build the same lexer (refs #2273)`

3. **Re-spell the N1 family and add #2273's cells** — `src/Sharpy.Lsp.Tests/FormattingFallbackTests.cs`,
   `IndentationServiceLiteralStateTests.cs`
   - `N1_…GetNoEdits` → `N1_BudgetAboveAString_TheStringIsRead_ItsLinesAreUntouched`: `ReindentDocument(N1)` keeps
     `      sub: 1` byte-identical and the fact is false; `N1t`: the 2-space code lines repaired, `    key` kept; `BSesc`:
     same shape (its 25th line is an aborted short string with an opener after it — now read as the dropped span of
     ONE line: `DroppedOpener` set, routes refuse — keep as a refusal cell, say why); `B1` keeps its repair;
     `IndentationServiceLiteralStateTests.LiteralLostWithoutAnAbort` drops the N1 row (the map no longer loses it) and
     gains `N1` under `NoLiteralLost` with the LEXER-level assertion moved to `LiteralStateTests` (the compiler's lexer
     still records `UnreadRemainder` on N1 — keep that theory there). Each re-spelling cites R-FP in its doc comment.
   - Add `P22g_BracketContinuationPastTheBudget_FullAndRangeWhole_KeepTheBody` with #2273's program and the 24-error
     control (one under the budget), asserting the APPLIED TEXT of both is identical and equal to the 24-error twin's
     measured result @ 729bf1e7d: `self) -> None:` → `    self) -> None:`, `        ...` unchanged (8 spaces)
     [CORRECTED: "no edits, or `...` at depth 2" does not discriminate — the 24-error twin edits the continuation line].
   - Commit: `test(lsp): past the error budget the string is read — N1/N1t/BSesc re-spelled under R-FP, the #2273 bracket continuation keeps its block (refs #2273)`

4. **Drain the S6n rows** — `formatting-route-parity-allowlist.txt`
   - FULL run; delete every S6n row; count anchor; any surviving S6 `literal`/`content`/`depth` row on a budget document
     is a finding IN the contract: fix at the seam before draining (R-FP's "tighten to (b)" only with the owner's
     words, recorded under Scope › Override).
   - Commit: `test(lsp): drain the 334 S6n rows and the budget-position rows — the indent map reads past the budget (refs #2273)`

### Phase 3: R-FR — arm (b) reads the dropped span at the close line

**Goal:** `RePairedCloser` fires for any abort on the close line whose dropped span holds a quote; the code list is gone;
the S6r rows drain; the no-stray controls keep their repairs.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: every `S6r` row deleted; S6x 0 rows (no `factSpurious`); `LiteralStateTests` gains the
  close-line theories: 5 codes × 2 quote characters × {orphan tail with a quote → `RePairedCloser`; tail without a quote
  → `None`} + the budget-stop-on-the-close-line row + the dedented-string "reports without dropping" control (`None`);
  the 33 existing re-pair theories unchanged and green with the code list deleted.
- Guard mutation (recorded in the commit body): restore the code list and delete the quote rule → the 10 orphan
  theories + the S6r cells red, the 33 green (the list is subsumed, the rule is the guard); drop the quote condition
  (`any error on the close line`) → the 5 no-quote theories and the S6x `factSpurious` rows red (the direction control
  is falsifiable).
- Allowlist entries that drain: all `S6r` rows.
- Execution evidence: FULL sweep @ sha; the 21 documents' routes before/after (in-process driver table); the
  `closeline-<code>-then-clean` on-type cell keeps its repair (the on-type line one clean line below the close line —
  Current State names why).

#### Tasks

1. **`NoteUnreadText` — one seam for both hooks** — `src/Sharpy.Compiler/Lexer/Lexer.LiteralLoss.cs`, `Lexer.cs`
   - `UnreadTextStart(fromTheAbortedLiteral: true)` also returns `_tokenStart` for a backtick-delimited name that began
     on this line (Design 2 ii′) [CORRECTED: without it every SPY0018 span is empty — Current State]; update its doc.
   - `NoteDroppedSpan(fromTheAbortedLiteral)` → computes `start = UnreadTextStart(...)`, calls
     `NoteUnreadText(start, lineEnd)`: the close-line quote rule (Design 2 ii) + `DroppedOpener` over `[start, lineEnd)`.
     The budget branch in `TokenizeAll` calls `NoteUnreadText(start, lineEnd)` for the close-line rule and keeps its
     `UnreadRemainder` scan over `[start, source end)`. Delete the four-code arm in `ReportError`; update the
     `LiteralLoss`/`RePairedCloser` and `NoteMultiLineLiteralClosed` doc comments (arm (b) = "a dropped span on the close
     line holding a quote character").
   - Commit: `fix(lexer): a re-paired closer is recorded for ANY abort on its line whose dropped span holds a quote — arm (b) reads the loss mechanism at the dropped-span seam, not a list of four codes (R-FR, refs #2275)`

2. **Lexer hand theories** — `src/Sharpy.Compiler.Tests/Lexer/LiteralStateTests.cs`
   - `AbortOnTheCloseLineWithAQuoteInTheDroppedSpan_SetsRePairedCloser(code, tail, quotes)` over the five codes × both
     quote characters (the N2 shape with the orphan from `FormatterTwins.CloseLineCodes` — the one recipe), asserting
     the reported code first (a row that reports another code is a test defect); `AbortOnTheCloseLineWithNoQuote_SetsNothing`
     over the five no-quote tails (no stray; `""" + 1__2`, `+ $`, `+ `x` …); `BudgetStopOnTheCloseLine_SetsRePairedCloser` (24
     errors then the N2 document so the orphan is the 25th); `DedentedStringReport_DropsNothing_SetsNothing`;
     [CORRECTED] `BacktickNameOnTheCloseLine_SpanStartsAtTheBacktick` — the SPY0018 rows R-bt `` `' ``, R2 `` see `x' ``,
     R2-bt-mid `` `x' `` set `RePairedCloser` (red without ii′: the span is empty), and
     `OrphanReadAsAShortString_RBtname_SetsNothing` — `t = '"""x' + `it's`` records `None` (the documented #2274 limit;
     the theory pins the signature so a later cure is a visible change).
     `Corpus_EveryAbortFreeLossShape_SetsItsMechanismsFlag`: `repair-SPY00NN` → `RePairedCloser`; `closeline-*` →
     direction controls.
   - Commit: `test(lexer): the close-line abort axis — five codes × two quote characters set RePairedCloser through the dropped span; the no-quote twins and a dedented-string report set nothing (refs #2275)`

3. **LSP cells and the drain** — `FormattingFallbackTests.cs`, allowlist
   - `R_CloseLineAbort_FullRangeLineAndOnType_GetNoEdits(orphanLine)` over #2275's NINE orphan lines plus the IB-bt
     placement (stray at line 1 inside the block) — the issue's rows are the data, not the recipe's six tails
     [CORRECTED: `t = '""" costs $5'`, `t = '""" $ '`, `t = '""" see `x'`, IB-bt are not recipe tails]; R-btname is
     EXCLUDED from this theory and asserted in `KnownLimit_RBtname_StillEdits_SeeIssue2274` (full/range-line/on-type
     still rewrite `key: value`; the test pins the known limit the way `DStringRepaired` is pinned);
     `DirectionControl_CloseLineAbortThenACleanLine_OnTypeKeepsItsRepair`. FULL run; delete the S6r rows; count anchor.
   - Commit: `test(lsp): a re-pair closed by an orphan that aborts at SPY0015/0018/0007/0008/0022 gets no edits; the no-stray close-line error keeps its repair — S6r rows drained (refs #2275)`

### Phase 4: #2276 — the scanner's spec-quote arm

**Goal:** `HoldsALiteralSpanningLines` agrees with the lexer's SPY0022 abort: SQ2/SQ3 set `DroppedOpener`; the typing
and closed-f-string controls stay false; the other mid-literal abort codes are walked.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `LiteralLossScannerTests` gains the spec-quote rows (Design 3's table: SQ2/SQ3 spans
  true; `x = f"{x:"` false; `x = f"{x:">}"` false; `x = f"{x:">}"  # """` false; `x = f"{x:">}" + 'z'` false;
  `x = f"{x:" + """` true; `x = t"{x:">}" + """` / `df"…"` true; the triple twin `f"""{x:"""` true; the 40-abort line
  completes) and the sibling-axis rows: SPY0020 `x = f"{x" + '''` (the existing `ClosesOnItsLine` rule — control; the
  `"""` spelling is SPY0022 via `ReportUnclosedField` and a second scanner-agrees control [CORRECTED]), SPY0021 `x = f"}" + """`
  / `f"{x}}" + """`, SPY0030 `x = f"{x!q}" + """`, each with its clean-close twin (false); `LiteralStateTests` gains the
  document-level twins (the flag through the real lexer, `DroppedOpener`, asserting SPY0022/0021/0030 reported first);
  `FormattingFallbackTests` gains SQ2 (range-line 2, full) and SQ3 (full, range-line 1) → no edits, SQ1 the control;
  `LiteralLossScannerDifferentialTests` stays green (unchanged oracle; the hand rows assert a clean lex is NOT
  required — they are erroring lines by construction, so they live in `LiteralLossScannerTests`, not the differential's
  clean-row theory).
- Guard mutation: remove the arm → the SQ2/SQ3 scanner rows, the lexer twins and the route cells red; keep the arm but
  drop the "walk from the quote" half → `f"{x:">}" + """` red; drop the "walk from past it" half → `f"{x:" + """` red
  (each half is load-bearing).
- Allowlist entries that drain: none (the sweep has no spec-quote shape; its corpus has no such erroring line — the
  hand rows are the axis the generator cannot enumerate, stated in Testing Strategy).
- Execution evidence: the theories @ sha; the SQ2/SQ3 route cells before/after.

#### Tasks

1. **The arm** — `src/Sharpy.Compiler/Lexer/Lexer.LiteralLoss.cs`
   - Refactor the loop into `WalkFrom(text, start, memo)`; the spec branch: at the literal's own quote in a spec, return
     `WalkFrom(i) || WalkFrom(i + quoteLength)` (memoized). Doc comment: the lexer's SPY0022 abort; the two roles; the
     known limit sentence for `x = 'abc """` stays (#2274).
   - Commit: `fix(lexer): the literal-loss scanner reads a quote inside a format spec as the lexer's abort — the rest of the line is walked with the quote in either role, so f"{x:">}" + """ holds an opener (refs #2276)`

2. **Hand rows, lexer twins, route cells** — `LiteralLossScannerTests.cs`, `LiteralStateTests.cs`, `FormattingFallbackTests.cs`
   - As listed under Acceptance; the sibling axis (SPY0020/0021/0030) is the matrix's two allowed axis changes for this
     mechanism (the second: enclosing kind t/df) — a divergence found there is fixed in this phase (in-contract), not
     filed.
   - Commit: `test(lexer,lsp): the spec-quote abort and the other mid-literal aborts (SPY0020/0021/0030) — scanner rows, lexer flags and SQ2/SQ3 route cells (refs #2276)`

### Phase 5: Docs, the direction record, the regression control, the gate

**Goal:** the documented behaviour matches the shipped behaviour; every changed route cell is classified by direction
against 729bf1e7d; the lexer change is shown to move no token or diagnostic; one whole-solution gate.

**Acceptance (class-level, measurable):**
- `docs/tooling/formatter.md`: the "In the editor" loss bullet without the budget clause; a sentence that the editor
  reads past the compiler's 25-error limit (`sharpyc emit diagnostics --max-errors N` stays); the R-FQ known-limit
  sentence; line 229's paragraph rewritten. `FormatterDocEditorExamplesTests` green (no new executable example: Design
  7). `docs/design/gap-discovery-contracts.md` route-parity row updated (axes, S6r/S6x, the S6k pin, allowlist 0).
- Direction record file `<scratchpad>/p22g-direction.md` (also pasted into the close-out comment): every route cell
  the plan changed, BASE (worktree @ 729bf1e7d) vs HEAD, labelled fix / gain / keep / LOSS — LOSS count 0.
- Regression control: over every corpus `.spy` (the sweep's corpus + `src/Sharpy.Compiler.Tests/Integration/TestFixtures`
  recursively, the non-source filter applied), `sharpyc emit tokens` and `emit diagnostics` byte-identical BASE vs HEAD
  (the built binaries of both worktrees); counts reported `N files, 0 diffs @ sha`.
- The gate: `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`
  → `passed/failed/skipped @ sha (measured)`; staleness gates 0 (`/push` runs them); "regen: no diff @ sha".
- Allowlist: 0 rows; `rows.Count.Should().Be(0)`; `build_tools/check_allowlist_issue_state.sh` green.

#### Tasks

1. **Docs** — `docs/tooling/formatter.md`, `docs/design/gap-discovery-contracts.md`
   - Commit: `docs(formatter): the editor reads past the compiler's error limit; the re-pair shapes no lexer fact can see are a stated limit (R-FP, R-FQ, refs #2273, #2274)`
2. **Direction record + token/diagnostic differential** — scratchpad scripts; results in the commit body of the
   close-out commit (or a `docs`-free `chore` commit touching nothing: paste into the plan's verify section instead).
3. **Gate** — the whole solution in the background; read the log, never re-run on a red without the control.

## Testing Strategy

- **Generator first (contract §11):** the existing S6 sweep extended with the two axes (Phase 1), its allowlist the
  ratchet (populated, then drained to 0 by Phases 2–3). The axes a generator cannot enumerate, covered by hand: (i)
  WHICH lexer flag fired (`LiteralStateTests`, lexer level — the sweep sees the boolean); (ii) the scanner's verdict on
  an ERRORING line (`LiteralLossScannerTests`) — the corpus has no erroring lines and the differential's oracle is the
  clean lex; (iii) the sibling abort codes SPY0020/0021/0030 (hand rows + lexer twins).
- **Fixtures:** none (`.spy`/`.expected` fixtures are compile tests; every cell here is an LSP route or a lexer flag).
  Every negative cell (no edits / flag set) has its positive control in the same theory or the sibling theory:
  N1c/N1s/N1t/B1 keep their repairs (R-FP); `closeline-<code>-then-clean` keeps on-type (R-FR); `f"{x:"` being typed and
  `f"{x:">}"` closed stay false and keep their routes (#2276); `DStringRepaired` keeps its edits (R-FQ).
- **Edge cells outside the issues' repro lists:** the 24-error twin of #2273's program (identical result — the budget
  is not the rule); the budget stop ON the close line (both hooks share the rule); both quote characters for every
  close-line code; the t-/df- enclosing kinds and the triple twin for the spec quote; a 40-abort line (complexity).
- **Outputs that discriminate:** every route cell asserts the applied text (the literal line byte-identical AND the
  misindented code line repaired where a repair exists) — "no edits" alone passes every oracle, so each no-edit cell
  is paired with a cell on the same document that DOES edit when the fact is false (the sweep's S3 "applied-differs"
  floor is the standing version).
- **Spec:** no `docs/language_specification/` change (the lexer fact and the map are tooling); the formatter doc is the
  deliverable of equal weight and its examples run under `FormatterDocEditorExamplesTests`.
- **Blast radius and when:** Lsp.Tests whole + Compiler.Tests lexer half in the edit loop after each phase; the
  token/diagnostic differential after Phase 4; the whole solution once at the end. Regen: nothing generated is reached;
  "regen: no diff @ sha" recorded.
- **Mutation records:** each commit that adds a guard records "broken → red (N), restored → green (N)" in its body
  (contract §2); restores from a `cp` copy, never from git (shared tree, §9).

## Issues to Close

- #2273 — the indentation-only fallback moves a statement to another block past the error budget — closed by Phase 2
  (Tasks 1–4); **close criterion:** every S6n row drained (allowlist 0 S6n rows @ sha), N1c/N1s/N1t keep their repairs
  (test names @ sha), #2273's program applies nothing on full/range-whole (cell @ sha), the direction record shows 0 LOSS
  over the S6 budget documents (75) and S6n (FULL sweep counts @ sha).
- #2275 — arm (b) keyed on four codes — closed by Phase 3 (Tasks 1–3); **close criterion:** the 10 orphan theories, the
  SPY0018 span-start theories and 20 of the 21 documents' route cells green @ sha; R-btname pinned as a known limit and
  posted on #2274 with its direction twin `""" + `x`` [CORRECTED: R-btname's orphan quote is READ, not dropped — no span
  rule under R-FR (b) sees it]; S6r rows drained, S6x 0 rows, the `""" + 1__2`-then-clean cell keeps on-type,
  mutation record (list restored → red; ii′ removed → the SPY0018 theories red) in the commit body.
- #2276 — the scanner closes an f-string at a spec quote — closed by Phase 4 (Tasks 1–2); **close criterion:** SQ2/SQ3
  true at the scanner, `DroppedOpener` at the lexer, no edits at the routes @ sha; the typing/closed controls false and
  editing; SPY0020/0021/0030 rows green; both halves of the arm load-bearing under mutation.
- #2274 — PERMANENT known-limit tracker (R-FQ): **not closed.** The plan posts one comment at close-out: the census pin
  (counts @ sha), the formatter.md sentence, that the 20 rows left the allowlist, and the R-btname cell (orphan quote
  read as a short string closed by an apostrophe inside a later backtick name; lexer signature = `""" + `x`` on a close
  line) [CORRECTED].

## Class trackers this plan may post cells to

- #2273 — the indent map's token source (budget, dropped lines, the post-error continuation clause found under
  Current State) — cells found outside this plan's contract go here as comments (contract §10).
- #2274 — re-pair shapes no lexer fact can see (permanent) — a new such shape is a comment here, never an issue.
- #2275 — the re-pair fact's close-line arm — a sixth code or a second arm shape.
- #2276 — the scanner vs the lexer on erroring lines — a further mid-literal abort the scanner misreads.
- #2232 (P47, LSP code actions) and #2239 (P50, formatter blank lines) are the neighbouring LSP trackers for cells on
  other routes. The lead files a NEW tracker only for a class none of these covers. No implementer runs `gh issue create`.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-08 @ 926670989 (built `sharpyc`/`Sharpy.Lsp` binaries @ 729bf1e7d — 926670989 touched no compiler or LSP source; probes re-driven over stdio with `.claude/tmp/p22e-probes/lspfmt.py`, lexer codes with `sharpyc emit diagnostics`)
**Plan file:** .claude/plans/plan-d923d3.md

### Corrections Made
- **Design 2 / Phase 3 (decision-changing):** "all five codes drop a span" → the SPAN is EMPTY for every SPY0018 abort: the backtick reader (`Lexer.cs:917-936`) reports at the line end and `UnreadTextStart` returns `_tokenStart` only for string literals. Measured: SPY0018 at (6:15) on the 14-char line `    t = '"""`'`, (6:21) on `t = '""" see `x'`, (6:24) on R-btname. Added (ii′): the span starts at a backtick name's own start (cures R-bt, R2, R2-bt-mid, IB-bt). **R-btname** (`t = '"""x' + `it's``: the orphan quote was READ as a short string closed by the apostrophe; the aborting backtick's span holds no quote; signature = the no-stray control `""" + `x``) is not curable under R-FR (b) → #2274 known-limit cell by R-FQ's rule; #2275's close criterion is 20 of 21 route cells + R-btname pinned and posted. The owner's two-way door to (a) is noted, not taken.
- **Phase 3 Task 3 data:** the route theory iterates #2275's nine orphan lines + the IB-bt placement (stray inside the block), not the recipe's six tails (`costs $5`, ` $ `, `see `x'`, IB-bt are not tails).
- **Adversarial review / Phase 2 Task 3:** the 24-error twin of #2273's program does NOT apply nothing — full applies one edit (`self) -> None:` → 4 spaces; `...` keeps 8). The cell now asserts that applied text for both the 24- and 25-error programs (discriminating).
- **Matrix / Phase 4:** `x = f"{x" + """` is not SPY0020 — the prescan reads `" + "` as a nested string; measured SPY0022 `'{' was never closed` at the bracket and no edits (scanner agrees). The SPY0020 spelling is `x = f"{x" + '''`.
- **Current State:** SQ3's exact text (closer line at column 0; the 4-space closer variant refuses for another reason); `+ 'b` removed from the nothing-lost controls (an unterminated string on the close line already sets the fact via the existing arm — it is that arm's positive control).
- **Design 3:** "stays linear" → bounded O(k·n) (two memoized fresh walks per abort position).
- **Design 6 / code facts:** "three consumers" → two today (`FormattingRouteParitySweepTests.cs:604`, `LiteralStateTests.cs:623`); `IndentationServiceLiteralStateTests` reads hand documents.
- **Phase 1 Task 4:** the S6k pin's "all stems ran" guard must cover all five twins (20 rows = 2 stems × 5 twins), not identity/comment only.
- **Line refs:** `Lexer.LiteralLoss.cs:150-160` → `:147-185` (own-quote arm `:161-166`); `Lexer.FStrings.cs:565-572` → `:569-576`; `:1024-1025` → `:1021-1022`; `Lexer.cs:48` → `:49`.

### Adequacy
- **Contract:** named — the indent-only routes edit only text the lexer read as the user wrote it (no string-content line rewritten, no statement moved), including past a lexer stop; two seams (the map's token source; a literal-loss fact true for every loss mechanism).
- **Matrix:** larger than the repro lists — budget-stop position × what follows; close-line code × dropped span × stray; spec-quote × spec text × what follows × enclosing kind × sibling abort codes; × route × document. Verifier added one axis value the plan missed: the dropped span being EMPTY (SPY0018) and the orphan quote being READ rather than dropped (R-btname), plus the IB-bt stray placement. The touched dispatch sites were listed by hand: `ReportError`'s four-code arm (deleted), `NoteDroppedSpan`/the budget hook (one `NoteUnreadText`), the scanner's spec-branch own-quote arm (`:161-166`) and its expression-branch `ClosesOnItsLine` arm (left as a control, verified to agree with the lexer's prescan `Lexer.FStrings.cs:764-771`).
- **Cure:** seam — the map's lexer construction (one factory), the dropped-span seam for arm (b), the scanner modelling the lexer's abort; then the generator gains the two axes. Meta-class row: silent wrong behavior + position/route missing a shared check.
- **Harness:** named — `FormattingRouteParitySweepTests` S6 family (S6n 334 → 0, S6k 20 → pin, new S6r/S6x) and `LiteralLossScannerDifferentialTests` (unchanged oracle; the erroring-line axis is hand rows, stated); allowlist drains to 0 with `rows.Count.Should().Be(0)`.
- **Layer:** every decision rung-justified — Design 1 rung 3 (an LSP construction rule; no `.spy`/Core rung exists); Designs 2–3 lexer-internal facts; no `BuiltinNames`/name-keyed rule.

### Scope & convergence
- **Group:** one (P22g, charter §3.1) · **Alone:** no — required? no (LSP edits + lexer flag only; no printed output or emitted layout moves; the token/diagnostic differential in Phase 5 is the proof) · **Gates:** 1
- **Generator:** existing `FormattingRouteParitySweepTests` S6 — extended first in Phase 1 (close-line axis, budget-position axis, S6r/S6x, S6k pin); hand matrices reserved for the axes it cannot enumerate (which lexer flag fired; the scanner's verdict on an erroring line; the sibling abort codes) — stated in Testing Strategy.
- **Trackers named:** #2273 #2274 #2275 #2276 (+ #2232, #2239 neighbours) · **Fidelity rows:** N/A (editor surface, no Python-parity cell)

### Falsifiability
- Phase 1 S6r/S6x states + budget-position shapes: mutation [named: delete a `repair-SPY00NN` shape → per-code floor red; pinned count changed → red; exemption removed → 20 unlisted rows]; exemption [the S6k `literal` exemption is NOT the subject — its pin is the falsifiable arm]; positive control [`PositiveControl_FactSpurious_FiresOnAStubbedFact` for the fact buckets; S3 "applied-differs" floor for routes]; by-direction probe [n/a — no new refusal]
- Phase 2 `StructureLexer` + `P22g_BracketContinuationPastTheBudget…`: mutation [named: factory at `MaxErrors = 25` → the cell, the S6n rows and N1-family red]; exemption [not subject]; positive control [N1c/N1s/N1t/B1 keep their repairs; default lexer on 30 lines → SPY0905 present (added)]; by-direction probe [specified: S6 budget 75 documents refused-by-fact → judged; S6n; N1 family; recorded in the direction file]
- Phase 3 close-line theories + `R_CloseLineAbort…`: mutation [named: four-code list restored + quote rule deleted → 10 orphan theories + S6r red, 33 green; quote condition dropped → 5 no-quote theories + S6x `factSpurious` red; ii′ removed → SPY0018 theories red (added)]; exemption [not subject]; positive control [`DirectionControl_CloseLineAbortThenACleanLine_OnTypeKeepsItsRepair` — on-type one clean line below the close line (the post-error continuation clause)]; by-direction probe [specified: 21 documents + controls on both trees; R-btname pinned as a still-editing known limit]
- Phase 4 scanner rows + lexer twins + SQ2/SQ3 route cells: mutation [named: arm removed → rows/twins/cells red; "walk from the quote" half dropped → `f"{x:">}" + """` red; "walk from past it" half dropped → `f"{x:" + """` red]; exemption [not subject]; positive control [`f"{x:"` being typed and `f"{x:">}"` closed stay false and keep their routes; SPY0020 `'''` and SPY0021/SPY0030 controls agree with the lexer]; by-direction probe [specified: SQ2/SQ3 edit → none; controls keep]
- Phase 5 token/diagnostic differential: mutation [implicit — any token/diagnostic change is a diff]; positive control [MISSING — expected: one deliberate lexer change (e.g. a message tweak in a scratch worktree) shown to produce a diff before trusting "0 diffs"]

### Warnings
- **Design 2's line comparison:** the rule compares `_line` at recovery time; the old arm compared the `line` argument of `ReportError`. They agree for every unterminated-string report (all pass `_line`: `Lexer.Literals.cs:53-189`, `Lexer.FStrings.cs:589-946`) and for `ReportUnclosedField` with no bracket open (`_line` restored to the bracket line, `Lexer.FStrings.cs:548-558`); with a bracket open inside the hole `_line` stays where the abort was detected — a later line than the close line, where today's arm also records nothing. The 33 theories are the guard; name this in the `NoteUnreadText` doc comment.
- **Phase 5 differential needs a positive control** (see Falsifiability): show the instrument detects a change before reporting "N files, 0 diffs".
- **Route axis naming:** the charter's matrix says `range-body`; the sweep's route kinds are `range-whole/-statement/-block/-line` — S6r/S6x take S6's `RoutesByState` set; say so in Phase 1 Task 3.
- **S6x tails must hold no quote** (`$`, `` `x ``, `1__2`, `0x`, `f"{x:`): the SPY0022 tail's dropped span starts at the bracket (`{x:`, no quote) — fine; a tail like `` `x' `` would be a legitimate `factSpurious` row under (ii′) and is the intended refusal R-FK accepts, not a control.
- **Phase 1 sweep time:** S6n's ~16k documents × 4 budget-position shapes in FULL mode; the plan's 1.5 × 248 s bound and stride fallback are stated — measure on the first FULL run and record.

### Missing Steps Added
- Scope: verify cost and prober budget line.
- Phase 2 Task 2: SPY0905 is a warning — assert on the warning channel with the default-lexer positive control.
- Phase 3 Task 1: `UnreadTextStart` backtick-name start (ii′); Task 2: the SPY0018 span-start theories and the R-btname `None` pin; Task 3: the issue's nine orphan lines + IB-bt as the route theory's data; R-btname pinned as a known limit.
- Issues to Close: #2275 criterion re-spelled (20 of 21 + R-btname → #2274); #2274's comment gains the R-btname cell.

### Unchecked Claims
- "33 existing re-pair theories" — not re-counted (the two `PrefixByTripleQuoteByOrphan` theories + one Fact exist at `LiteralStateTests.cs:418-477`; the count is the issue's and the charter's).
- BSesc's exact behaviour after R-FP ("DroppedOpener set, routes refuse") — not probed; its text was not read. Phase 2 Task 3 measures it.
- Sweep-time bound and the S6k pin's exact per-route counts (ontype 10, range-line 10, full 0, range-whole 0) — taken from P22f's verify record; Phase 1 Task 5's FULL run measures them.
- The `_tokenStart` claim for the f-string path (set in the main loop at `Lexer.cs:556` before `NextFStringToken` dispatch at `:511`) — verified by reading; not exercised by a probe (the SQ2 route cell is the end-to-end observation).

## Implementation Checklist

Base sha 926670989. Lead-implemented in phase order (every phase shares the recipe, the sweep, `Lexer.LiteralLoss.cs`
or `FormattingFallbackTests.cs`, and the dotnet lock serializes test runs); standalone `verification-expert` at the end.

- [x] Baseline gate @ 926670989 — lead — 78397/0/4 @ 926670989 (measured; Core 3989, Stdlib 4188, Cli 441+1 skip, Lsp 21002 [corrected: labels were swapped, per the verifier], Compiler 48777+3 skip; `.claude/tmp/p22g/baseline-gate.log`)
- [x] P1T1 close-line axis in the recipe — lead — 52cac78b5
- [x] P1T2 budget-position axis in the recipe (+ LiteralStateTests expectations) — lead — 52cac78b5
- [x] P1T3 sweep states S6r/S6x, budget prefix, cites, loader rows — lead — 52cac78b5
- [x] P1T4 S6k known-limit pin — lead — 52cac78b5 (pin 0/0/10/10; 3 mutations red)
- [x] P1b (ADDED) recovery-point seam — the generator's S6x findings, fixed in contract — lead — e16e323eb
- [x] P1T5 populate allowlist from FULL run — lead — 52cac78b5 (574 rows: S6n 334, S6r 240; FULL 16475/0/0)
- [x] P2T1 IndentationService.StructureLexer — lead — a8209a71c
- [x] P2T2 sweep mechanism read via factory + StructureLexer_ReadsPastTheBudget — lead — a8209a71c
- [x] P2T3 re-spell N1 family + #2273 cells — lead — a8209a71c (N1t/BSesc stay refused via DroppedOpener, measured)
- [x] P2T4 drain S6n rows — lead — a8209a71c (334 drained; anchor 240; mutation 172 red)
- [x] P3T1 NoteUnreadLine (the plan's NoteUnreadText) + backtick span start (ii′) — lead — d5e6d2d5d
- [x] P3T2 lexer hand theories — lead — d5e6d2d5d (+ end-of-source stop rows, found by mutation)
- [x] P3T3 LSP cells + drain S6r — lead — d5e6d2d5d (240 drained; allowlist 0; 4 mutations red)
- [x] P4T1 scanner spec-quote arm — lead — 4771474d7
- [x] P4T2 scanner rows, lexer twins, SQ2/SQ3 route cells — lead — 4771474d7 (+ SQ2b/SQ3b: SQ2/SQ3 cells inert at route level under mutation)
- [x] P5T1 docs — lead — 9202e3b83
- [x] P5T2 direction record + token/diagnostic differential (with positive control) — lead — LOSS 0 of 41,540 cells; 3800 files 0 deterministic diffs (record below)
- [x] P5T3 whole-solution gate + format + staleness — lead — 78480/0/4 @ 9202e3b83 (measured); format clean; spy + spy-tests staleness OK; allowlist issue-state OK
- [x] Final refutation — verification-expert (standalone, opus) — REFUTED "LOSS 0": e16e323eb refused whole-document repairs when the line error recovery drops continues (open bracket / trailing backslash) — 14 cells; cured in c08cf1f03 (RecoveryResumesAfterAContinuedLine); items 1 (inert fixes, 242+42 probes), 3 (StructureLexer mutation 172 red reproduced) and 4 (docs) NOT REFUTED
- [x] Final gate @ c08cf1f03 — lead — 78490/0/4 @ c08cf1f03 (measured; Core 3989, Cli 441+1, Stdlib 4188, Lsp 21029, Compiler 48843+3); staleness + allowlist issue-state re-run

## Implementation Record (lead, 2026-10-08)

### Direction record (plan-d923d3) — BASE 926670989 vs HEAD 9202e3b83

Instrument: the built Sharpy.Lsp of each tree driven over stdio (one server per tree, open → request → close),
applied text compared byte for byte. BASE = a snapshot of the binaries built @ 926670989 before any P22g edit;
HEAD = a snapshot @ 9202e3b83. Label: fix (a wrong edit at BASE refused or corrected), gain (no edit at BASE, a
correct edit at HEAD), keep (identical), LOSS (a correct edit at BASE, none or a different one at HEAD).

## Hand cells (80 requests)

| group | cells | BASE | HEAD | label |
|---|---|---|---|---|
| #2273's program, 25 errors (full, range-whole) | 2 | `        ...` moved to 4 spaces | the 24-error twin's text: continuation to 4 spaces, `...` kept | fix |
| #2273's program, 24 errors (full, range-whole) | 2 | continuation to 4 | same | keep |
| N1 (full) | 1 | no edits (fact) | no edits (nothing to repair; string read) | keep |
| N1r — N1 with a 2-space `print(s)` (full) | 1 | no edits (fact) | `print(s)` repaired, string kept | gain |
| N1t (full, range-line 32), BSesc (full) | 3 | no edits (budget fact) | no edits (DroppedOpener) | keep |
| #2275's 9 orphan spellings + IB-bt (full, range-line 3, on-type 3) | 30 | `key: value` rewritten | no edits | fix |
| R-btname (full, range-line 3, on-type 3) | 3 | rewritten | rewritten (#2274 known limit, pinned) | keep |
| `""" + 1__2` then a clean line (on-type 5) | 1 | repaired | repaired | keep |
| abandoned `f"{x:` above a triple f-string's field (H: full, range-whole, range-line 3) | 3 | field line `x * 2` rewritten | no edits | fix |
| the same + `  c = 2` (Hb: full, range-whole) | 2 | field line rewritten + c repaired | field line kept + c repaired | fix |
| Hb range-line 3 | 1 | field line rewritten | no edits | fix |
| Hb range-line 6, on-type 6; H on-type 3; H2; H3; typed-field 2-space doc | 6 | — | same | keep |
| wide `dr"""` closer `""" + $` (on-type 1, range-line 1) ×2 docs | 4 | line 1 alone re-indented, `print(s)` one block deeper | no edits | fix |
| the same, full | 1 | whole-document repair | same | keep |
| SQ2b / SQ3b shapes (A range-line 3; B full, range-line 3; C full, range-line 2) | 5 | string line rewritten | no edits | fix |
| the other SQ candidate cells (A full/range2, B range2, C range3, D ×3) | 7 | no edits | no edits | keep |
| post-error 2-space / nested / wide docs (pe) | 5 | — | same | keep |

LOSS: 0 of 80.

## Corpus, identity twin: every single-file fixture (3455) × the four budget-stop shapes × full = 13820 cells

| label | budget | budget-midstring | budget-field | budget-indent |
|---|---|---|---|---|
| keep (no edits) | 2762 | 2762 | 2762 | 3455 |
| keep (same edit) | 679 | 679 | 679 | 0 |
| edits now (BASE: refused by UnreadRemainder) | 14 | 14 | 14 | 0 |
| refused now / different edit | 0 | 0 | 0 | 0 |

The 42 "edits now" cells: HEAD's applied text (first line dropped) equals BASE's AND HEAD's applied text on the
24-error twin, 42/42 — exactly R-FP's contract ("the budget caps diagnostics, not structure"). Their edits
(continuation lines and comment lines re-aligned to block levels, and on `decorators/bracket_attr_missing_close`
both `pass` lines moved to column 0) are what the fallback applies with no budget stop at BASE too: pre-existing,
the second recorded in the findings ledger as the unclosed-bracket class.

## Token / diagnostic differential (regression control for the lexer changes)

`sharpyc emit tokens` and `sharpyc emit diagnostics`, BASE vs HEAD binaries, over every `.spy` under
`src/Sharpy.Compiler.Tests/Integration/TestFixtures` (bin/obj excluded): 3800 files, 0 deterministic diffs @
9202e3b83. One diff on the first pass (`errors/edge_cases/multifile_error_recovery/main.spy`, diagnostics) is
BASE's own run-to-run variance (its 2nd and 3rd runs equal HEAD's three). Positive control: the HEAD side reading
a perturbed copy of a fixture (one line appended) → a diff in tokens AND diagnostics; no-difference control: the
same file both sides → none.

## Corpus, comment and wide twins (FormatterTwins.CommentInjected / WideIndented, the sweep's own twins): 3455 fixtures × 2 twins × 4 budget-stop shapes × full = 27640 cells

| label | comment (per shape: budget, -midstring, -field / -indent) | wide (per shape / -indent) |
|---|---|---|
| keep (no edits) | 39 / 3455 | 38 / 3455 |
| keep (same edit) | 155 / 0 | 3386 / 0 |
| edits now (BASE: none) | 2626 / 0 | 31 / 0 |
| different edit | 635 / 0 | 0 / 0 |
| refused now | 0 | 0 |

R-FP oracle on every changed cell (9876): HEAD's applied text past the budget (first line dropped) equals HEAD's on
the 24-error twin in 9876/9876, and BASE's on the 24-error twin in 9868/9876 — the 334 S6n depth rows (comment twin)
were among the "different edit" cells: BASE moved `        ...` out of `method`, HEAD keeps it. The 8 cells where
HEAD differs from BASE even under the budget are the e16e323eb fix: an f-string abandoned by recovery
(`x = f"{a $ b}"`, the budget-field line; `errors/fstring_comment_swallows_close`; `errors/invalid_hex_escape`)
left the NEXT literal without a span at BASE, so BASE refused the whole document; HEAD reads the literal and applies
the fallback's ordinary re-indent (`def foo():` / `pass` 8 → 4 spaces on the wide twin; every `return` 8 → 4 on the
wide `fstring_comment_swallows_close`, whose lines after the unclosed field the lexer itself rules code; comment and
bracket-continuation lines re-aligned on the comment twin, as the fallback does everywhere). LOSS: 0 of 27640.

## Totals

LOSS: 0 across 80 hand cells + 13820 identity + 27640 twin corpus cells. Fixes: #2273's program and the 334 S6n
rows; #2275's 30 route cells; #2276's SQ2b/SQ3b cells; the recovery-point cells (field line, 8-space closer line).
Gains: N1r, the 75 S6 budget documents, and every past-the-budget document now handled as under the budget.
Known limit kept: R-btname (#2274).

### Findings ledger


- [FILED #2279] unclosed-bracket-swallow · `def main():\n    xs = [1,\n    y = 2\n    if y:\n        print(y)\n` · Format Document and range-whole move `print(y)` to 4 spaces, out of the `if` body (also `decorators/bracket_attr_missing_close.spy`: both `pass` lines to column 0, out of `Bar` and `main`) · no edits, or the body kept at its depth @ 926670989 AND 9202e3b83 (pre-existing) · silent-wrong · program · "lines the indent map reads as bracket continuations escape the indent-only check's block-depth judgment" (the map follows the lexer's open bracket to EOF; IndentOnlyPreserved judges logical-line starts only) · found-by: P22g Phase 5 direction record (corpus budget shapes, R-FP oracle)
- [FILED #2279] post-error-continuation-lost-repair · `def main():\n    x = $\n      y = 1\n` · on-type 2 and full apply nothing · y repaired to 4 spaces (with one clean line between, the repair returns) @ 729bf1e7d … 9202e3b83 · tooling · program · same class as above (the line after an error recovery is read as a continuation; e16e323eb made its DEPTH judged — wrong edits refused — but it is still not a re-indent target) · found-by: plan-d923d3 Current State (the plan routed it to #2273 as a comment; #2273 closes with this plan)

### Correction after the refuting verifier (2026-10-08)

The "LOSS: 0" above was measured @ 9202e3b83 and is REFUTED: e16e323eb's hidden-line depth judgment refused the
full and range-whole repairs of any document in which the line error recovery drops continues (an open bracket, a
trailing backslash) — 14 cells of the verifier (`x = foo($,` / `        a)`, an unterminated string in a call, a list,
a backslash, CRLF, 8-space, a module-level dict) plus the bare `errors/backslash_trailing_whitespace.spy`. Not
seen by the corpus runs above: no budget-shape document has a continued erroring line. Cured in c08cf1f03: the lexer
records which recovery points followed a continued line, and the map leaves the next line out of the judgment.
Re-drive on c08cf1f03's build: the verifier's 14 cells byte-identical to BASE again; its 242 inert-fix probes, 42
spec-quote probes and the wrong-edit cells unchanged; the 41,460 corpus cells unchanged except 9 on
`errors/backslash_trailing_whitespace` (BASE refused only because the fixture's own error became the 25th): the
new build applies BASE's bare-fixture repair, and the same text past the budget (R-FP). LOSS @ c08cf1f03: 0 known.
Also from the verifier (ledger, not cured): the spec-quote arm's either-role rule refuses on-type/full while typing
`f"{x:"""}"` and `f'{x:'''}'` (low reach; Design 3's stated direction cost); M3 (#2274) now refuses at HEAD — an
incidental gain of the hidden-line check, so the plan's "M3 stays" no longer holds (say so on #2274 at close-out).

## Verification Record (/verify-implementation, 2026-10-08)

**Scope:** 926670989..be1c16fcf (8 implementation commits incl. the chore cde8e36cd riding in the range; 2 remediation commits be698d250, be1c16fcf). **Stamp:** PASS WITH CORRECTIONS / Adequacy CLASS. Four refuting auditors (completeness, regression, class-cure, sibling-cell prober), reports in this session's scratchpad (`report-*.md`, `findings-ledger-*.md`).

- **Gate @ c08cf1f03 (HEAD before remediation):** 78487/3/4 (measured; Core 3989, Cli 441+1, Stdlib 4188, Lsp 21029, Compiler 48840/3/3). The 3 reds = `SemanticPropertyTests.TrailingNewline_DoesNotChangeDiagnostics` seeds 42/2025/9999: `FuzzIterationTimeoutMs` (2 s) hit under load 14–25, SPY0901 recorded not thrown. Control: 5/0/0 @ c08cf1f03 and 5/0/0 @ 926670989, same filter (measured) → environmental; cell on #2278.
- **Gate @ be1c16fcf (FINAL):** 78517/0/4 (measured; Core 3989, Stdlib 4188, Cli 441+1, Lsp 21042, Compiler 48857+3; +27 tests = the remediation rows; `.claude/tmp/p22g-verify/gate-final-be1c16fcf.log`). FrontEndParity, InteropConformance (5), MetamorphicCorpus (2), DifferentialExecution (3) green inside it.
- **Blast radius:** FrontEndParity 1/0/0, InteropConformance 5/0/0, MetamorphicCorpus 2/0/0, DifferentialExecution 3/0/0 (in the gate @ c08cf1f03); Semantic/CodeGen/Core/Stdlib diff EMPTY. FULL route-parity sweep 16475/0/0 @ c08cf1f03 (319.8 s) and 16475/0/0 @ be698d250 (fixed tree); allowlist 0 rows; S6k pin 0/0/10/10 held. Regen: no diff @ c08cf1f03 (spy-stdlib, spy-tests, stdlib docs, oracle ledger, in a worktree); staleness checks + allowlist issue-state green @ be1c16fcf; format clean @ be1c16fcf.
- **Token/diagnostic differential (BASE 926670989 sharpyc vs be1c16fcf):** 860 files (all 440 errors/ + every 8th other), 1720 outputs, 0 deterministic diffs; positive control (perturbed copy) differs on both channels. (Agent 2: 440 errors/ fixtures @ c08cf1f03, 0 diffs.) NOTE: `emit tokens` prints no token dump for an erroring file, so the token half is real only on clean-lexing files (ledger, tooling).
- **REFUTED and fixed in be698d250 (in contract, P1b's seam):** c08cf1f03's continued-line flag read `_bracketDepth > 0` at the abort + the last skipped char: a bracket the given-up text closes counted as open (`if foo($):` ×7 header spellings + `def method(self, $) -> None:` + its budget twin: 21 wrong edits = BASE's, 2 NEW vs BASE; refused @ 9202e3b83), a bracket opened after the abort was missed (`x = $ foo(1,`: 4 LOSS vs BASE), a backslash in a comment continued. Now `Lexer.DroppedLineContinues` judges the line's END state. Mutations: depth-at-abort-only → lexer 8/18 red + FormattingFallbackTests 11/87 red; comment-as-code → 2/18 + 1/4 red; restored green. Prober re-drive on the fixed build: 27/401 cells changed (21 refused, 4 repaired = BASE), 374 unchanged.
- **REFUTED, filed #2280 (lane:tooling, reach:program, class-tracker):** R-FR (b)'s close-line quote rule counts the aborted literal's own delimiter (`""" + "\q"`, `f"{x!q}"`, `f"}"`, `f"{x:">}"`, `` `it's ``: 24 LOSS cells vs BASE, 0 wrong edits). An owner-ruled arm; narrowing needs a pairing model (a plan, not a remediation).
- **Guards (be1c16fcf):** `ManySpecQuoteAborts` asserts a fresh-walk count (40..80) instead of a 1 s clock (mutation: 3 walks/abort → 120, red); rows `x = f"{x}}"` false, `x = f"{x" + """` true (scanner-level; the lexer's dropped span starts at the bracket → no flag); M3 refusal pinned (`KnownLimit2274_M3_…`); SQ2/SQ3 doc names SQ2b/SQ3b as the discriminating cell (Agent 3 M8: arm reverted → SQ2/SQ3 green, SQ2b/SQ3b red).
- **Cells outside the contract (posted):** #2279 — `if foo("abc):`, ``if foo(`x):`` (bracket closed inside an unterminated string/backtick: open to any lexer fact), `x = foo($,\ndef g():` (open to EOF); #2278 — the 3 timeout reds + the removed 1 s clock; #2274 — close-out comment posted (pin, formatter.md sentence, 20 rows gone, R-btname, M3 now refuses). Ledger: 3 (completeness) + 4 (classcure) + prober classes + 1 (regression) → issues filed 1 (#2280), comments 3.
- **Issues to Close:** #2273 CLOSEABLE (close comment must say N1t stays refused via DroppedOpener, not repaired); #2275 CLOSEABLE (20/21 + R-btname posted on #2274; the direction cost of its arm → #2280); #2276 CLOSEABLE (close comment should record the typing cost: `f"{x:"""}"` / `f'{x:'''}'` refuse on-type/full while being typed — Design 3's stated cost, ledgered, no tracker); #2274 stays open.
