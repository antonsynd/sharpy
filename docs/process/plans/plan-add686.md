<!-- Verified by /verify-plan on 2026-10-07 @ 3c70ea492 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: built-first -->

# P22f — literal loss without a lexer abort: widen `Lexer.LiteralStateUnknown` (#2271)

## Context

P22e (plan-96f8e0, #2168, closed) made every LSP formatting route apply a text that was checked as applied. Its decision 7 says: **when the lexer has lost a literal that can span lines, no indent-only route (format on type, the full and selection fallbacks) edits the document — every line inside a string is unchanged.** The fact the routes read is `Lexer.LiteralStateUnknown`, set in `ReportError` (`src/Sharpy.Compiler/Lexer/Lexer.cs:34`) while a multi-line literal read is open — i.e. only when the lexer ABORTS inside such a read.

The verify round of P22e refuted the contract on three **abort-free** loss mechanisms (#2271, measured @ 92af80222 by two auditors, re-measured in this plan @ 3c70ea492):

- **N1 error budget** — `TokenizeAll` stops after `MaxErrors` (25) lexer errors with source left; a closed triple-quoted literal below the stop has no token, its interior lines read as token-less "dropped code lines" to the indent map, and the routes re-indent them.
- **N2 re-pair in a short string** — a stray `"""` above a multi-line string pairs with that string's opener; the string's content lexes as code; its closer opens a run that a `'"""'` short string lower down closes mid-line. The triple count is even, so no read aborts.
- **N4 dropped delimiter line** — the opener (and closer) line of a triple-quoted literal is dropped whole by error recovery at an indentation error (SPY0011/SPY0012/SPY0013/SPY0014 — `MeasureIndentation` throws before any token of the line is read, `RecoverFromError` skips to the next line); the `"""` pair is never read and the content lexes as code.

Owner ruling **R-FK (option a)** (charter §3.1, #2271 comment 6039921334): widen the recorded fact — a lex that dropped a line holding a quote character at SPY0012/13/14, re-paired a triple quote, or stopped at its error budget with source left marks the literal map unknown; the 2-space-document repair (`ParseError_TwoSpaceDocument_TabSize2_FallbackWritesFourSpacesAsync`) stays a positive control; the cure tells a dropped line holding a quote from a dropped line holding code; **the plan measures which lexer facts carry each mechanism**; generator (an S5 twin, S6) FIRST; direction record against the prior commit for every newly refused edit. Rejected (b) refuse any document whose lex has a diagnostic; (c) hand guards per cell.

Issues: #2271 (class tracker, `lane:silent-wrong` `reach:program`). Predecessor: #2168 (closed).

## Current State (measured @ 3c70ea492)

Driven through the built server over stdio (`.claude/tmp/p22e-probes/lspfmt.py`, binaries built 2026-10-07 11:18 = the HEAD commit's minute; cases and outputs saved in `.claude/tmp/p22f-probes/`: `cases.json`, `out2.json`, one `.spy` per document; diagnostics via `sharpyc emit diagnostics`):

| doc | shape | lexer diagnostics | route → applied @ 3c70ea492 |
|---|---|---|---|
| N1 | 25 × `x = "abc` then `main` with `s = """\n    key:\n      sub: 1\n    """` | 25 × SPY0001, SPY0905 | full, range-line 28, ontype 28 all rewrite `      sub: 1` → `        sub: 1` (string content; width 6 is exempt from clause 6) |
| N1c | `def main():` + 30 × `  x = 1` (no literal) | 25 × SPY0013, SPY0905 | full REPAIRS every line to 4 spaces — **must keep its edit** (direction control) |
| N1s | `def main():` + 30 × `  print("hi")` | same | full REPAIRS — **must keep its edit** |
| N1t | 30 × `  x = 1` then a 2-space `s = """ / key / """` past the budget | same | full: no edits (another clause); range-line 32 / ontype 32 rewrite `      key` → `    key` |
| N2 | issue's document (`"""` line 0; `t = '"""'`) | SPY0013 (4:1), SPY0001 (6:14) | full: `key: value`, `"""`, `print(s, t)` all to column 0; range-line 3: `key: value` to column 0 |
| N2c | N2 with `t = '"""'  # don't` | SPY0013 only — **no SPY0001**: the orphan `'` and the comment's apostrophe pair into a string | same leak as N2 |
| N2w | quote chars swapped: `'''` line 0 above a `"""` docstring, `x = "'''"` | SPY0001 (6:14) | full: `return x` to column 0 (the docstring's lines are inside the `'''` run, so range-line/ontype on them happen to refuse) |
| N4 | issue's document (2-space opener and closer) | SPY0013 ×3 | ontype 2, range-line 2, range (2,0)–(3,0): `        key: value` → `    key: value`; range (2,0)–(4,0) also `  """` → `    """`; full and range-whole refused (clause 3) |
| N4q | `def main():\n  x = "hi"\n  print(x)` | SPY0013 ×2 | full REPAIRS — **must keep its edit** (a dropped line holding a quote that opens nothing multi-line) |
| N4t | `def main():\n  t = '"""'\n  print(t)` | SPY0013 ×2 | full REPAIRS — **must keep its edit** (a triple inside a short string on a dropped line) |
| N4m | wide body, delimiter lines at width 4 under a width-8 block | SPY0014 ×2 | full / ontype 3 / range-line 3 all refused today (another clause) — the FACT is still false: a `factMissing` cell, not a route cell |
| N4tab | tab-indented twin | SPY0012 ×3 | ontype 2: `        key: value` → `    key: value` |
| N4cc | closer on the content line (`        key: value"""`) | SPY0013, SPY0001 (triple) | refused — the closer opens a run to EOF, the existing abort fact fires (control) |
| N4f | 2-space `x = f"{', '.join(\n        names\n      )}"` | SPY0013 ×3 | ontype 2 / range-line 2: `        names` → `    names` — a hole continuation line, inside the f-string's `LiteralSpans` span in the clean lex: a sibling the issue did not list |
| N2typ | `def"""  + "typing` (an unterminated short string typed on a closer line) | SPY0001 (3:21) | full re-indents the next line (an unexpected indent, 8 → 4); ontype refused |
| TWO | the 2-space positive control | SPY0013 ×2 | full REPAIRS (the owner's positive control) |
| X6 [ADDED: adequacy — verifier-measured @ 3c70ea492] | `def main():\n    x = $"""\n        key: value\n    """\n    print(x)\n` — a mid-line unexpected character BEFORE the opener on a valid-width line | SPY0015 (2:9), SPY0014 (4:1) | full and range-line 2 rewrite `        key: value` → `    key: value`; ontype 2 refused. Abort-free: the `$` abort drops the rest of line 1 (the opener), the closer line then drops at SPY0014 (width 4 under the content's 8), nothing is ever read as a literal. A ROUTE cell the issue did not list; carried by the dropped-span hook (Decision 4) |
| X4 [ADDED: verifier-measured] | `def main():\n  s = """abc"""\n  print(s)\n` | SPY0013 ×2 | full REPAIRS — **must keep its edit** (a dropped line holding a triple literal closed on its own line) |
| X9c / X9s / X1a / XSW [ADDED: adequacy — verifier-measured @ 3c70ea492] | the four re-pair shapes NO closer-context fact can see (Decision 3's known limits): `"""` at line 0 re-paired by a `# """` comment below (X9c); `"""` at line 0 + `t = '""" '  # don't` (X9s: spaced orphan, quote swallowed by the comment's apostrophe); `"""` at line 0 AND a bare `"""` as the last line (X1a); `x = 'abc + """` — the opener swallowed by an unterminated short string, count re-paired by `# see """ docs` below (XSW) | none / none / none / SPY0001 (2:19) only — `key: value` lexes as code (`emit tokens`), no abort | full, range-line, ontype on the content line all REFUSED today (another clause) — `factMissing`-only cells like N4m, not route cells; the sweep would list them only if S6 built them (it does not — see Decision 3) |
| D19 | S5 (abort) | SPY0001 (triple) | refused everywhere (control) |

Lexer facts measured: N1 leaves `_position < _source.Length` at the budget break (`Lexer.cs:291–301`, no `RecoverFromError` call); N4's abort happens with `_position` at the line start (`HandleLineStartIndentation` restores `_position = savedPos` before `MeasureIndentation` throws), so the text `RecoverFromError` skips is the whole line; N2's closer is immediately followed by a quote character in every variant (N2, N2c, N2w), and by SPY0001 on the closer's line in all but the comment-swallowed one. Implicit string concatenation is not Sharpy syntax (`literal_string.md:91`, `string_literals.md` "No Implicit Concatenation" — `"a" "b"` is SPY0103), so a triple-quoted closer immediately followed by a quote character never occurs in a program that parses.

The standing harness `FormattingRouteParitySweepTests` has states S1–S5; S5 inserts one unmatched opener as line 0, which always ends in an abort inside a literal read. It cannot build any of the three shapes. `formatting-route-parity-allowlist.txt` is EMPTY.

## Scope

- **Group:** P22f — literal loss without a lexer abort (charter §3.1; P22e residue; R-FK). Chores riding along: none.
- **Alone:** no — no printed output or emitted layout changes (LSP edits and a lexer fact that changes no token or diagnostic; charter: "By design NO snapshot moves … P22f (LSP edits only)").
- **Gates:** 1 (whole solution at the end; the edit loop is `Sharpy.Compiler.Tests` lexer classes + `Sharpy.Lsp.Tests`).
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`); the lead files by class (contract §10); trackers named at the end of this plan.
- **Override:** none.
- **Verify cost and prober budget [ADDED: missing step]:** verify re-drives `cases.json` + the verifier's `xcases.json` (22 cells) through the rebuilt server, runs the sweep in FULL mode once, and the whole-solution gate once (~30 min); the sibling-cell prober's budget is this matrix plus at most two axis changes, findings one ledger line per cell, never an issue per cell.

## Defect Class

- **Violated contract:** when the lexer has lost a literal that can span lines, `Lexer.LiteralStateUnknown` is true — for EVERY mechanism that loses one, not only an abort inside its read — so that no indent-only route edits a line that is string content in the document the user wrote.
- **Meta-class:** silent wrong behavior (string content rewritten) + a position/route missing a shared check's INPUT: the shared seam (`FormattingEdits.CheckedIndentOnly` reading one lexer fact) exists since P22e; the fact's producer covers one of four loss mechanisms → **standard cure:** widen the fact at its one producer (the lexer), then a generator over the loss-mechanism axis so a fifth mechanism cannot recur unseen.
- **Known member cells:** #2271's table (N1 budget; N2 re-pair; N4 dropped delimiter line + 13 siblings), plus this plan's N2c (comment-swallowed orphan), N2w (swapped quote chars), N4f (f-string with a multi-line hole), N4m (SPY0014, fact false while every route refuses for another reason).
- **Sibling cells this plan must also cover (the matrix):**
  - loss mechanism {budget stop with a literal left unread; re-pair: orphan quote adjacent to the closer / orphan unterminated on the closer line / orphan swallowed by a comment; dropped delimiter line × code {SPY0011 mixed, SPY0012 tab, SPY0013 width, SPY0014 mismatch, **SPY0015 mid-line unexpected character before the opener on a valid-width line (X6) [ADDED: adequacy]**} × closer line {bare delimiter, content line (control: aborts)}; S5 abort (control)}
  - **[ADDED: adequacy] known-limit cells, named so the matrix is honest (no lexer fact can carry them without content inspection — they share their lexer signature with `DStringRepaired`, which loses nothing):** re-pair by a comment `# """` (X9c), spaced orphan with its quote swallowed by a comment apostrophe (X9s), two stray bare delimiter lines (X1a), opener swallowed by an unterminated short string on its line with the count re-paired below (XSW). All four are abort-free and refused on every route today for another clause (verifier-measured @ 3c70ea492: `factMissing`-only). They are listed in Decision 3, posted as a cell comment on #2271 at close-out, and NOT built by S6 (rows that cannot drain). The author confirms this scoping or chooses to build them as an S6 sub-shape whose rows stay cited to #2271.
  - × literal kind {`"""`, `d"""`, `r"""`, `dr"""`, `b"""`, `f"""`, `t"""`, `df"""`} × quote char {`"`, `'`} × a single-quoted f-/t-string whose hole spans lines (N4f)
  - × route {ontype, range-line, range content+closer, full, range-whole}
  - × document {the clean parent (control: fact false, routes may edit), the N-document}
  - × direction controls that MUST keep their edits: a dropped line holding a short string (N4q), a short string holding a triple (N4t), a budget stop whose remainder holds no multi-line literal (N1c, N1s), the owner's 2-space document (TWO).
- **Lane / reach:** silent-wrong · program; **fidelity-scope row:** N/A — an editor surface, not a Python-parity cell.
- **Standing harness that should have caught it:** `FormattingRouteParitySweepTests` — its S5 state samples one failure shape (abort). Phase 1 adds S6 with the three abort-free shapes and two FACT buckets; `LiteralStateTests` (lexer level) gets the same shapes.
- **Generator:** REQUIRED FIRST (residue group; ≥ 2 axes × ≥ 3 values). Phase 1 builds S6 in the existing sweep (`formatting-route-parity-allowlist.txt` is the ratchet) and the cure (Phase 2) drains it. The hand matrix is reserved for the axis the sweep cannot see: **which lexer fact fired** (the sweep sees only the route verdict and the boolean) — that axis lives in `LiteralStateTests` as prefix × quote × mechanism theories asserting the `LiteralLoss` flag.
- **Owner rulings:** #2271 comment 6039921334 (R-FK; authoritative); #2168 comment 6038262582 (the verify evidence). Do not re-ask.

## Adversarial Review (pre-mortem)

- **Alternative root cause:** "the indent map, not the fact, is wrong — `BuildIndentMap` treats a token-less line as a dropped CODE line, so it should instead treat it as unknown." Discriminating probe: TWO (`def main():\n  x: int = 1\n  print(x)`) — its lines are token-less for the same reason and the repair is the fallback's purpose; a map-side change that refuses N4 refuses TWO (that is rejected option (b)). The fact is the right carrier because it is the one input every indent-only route already reads (`CheckedIndentOnly`, `OnTypeFormattingHandler.cs:68`).
- **Alternative layer:** the layer ladder (Core/Stdlib Python behaviours) does not apply — tooling. Within the compiler: the fact could be computed in `IndentationService` from tokens + diagnostics instead of the lexer, but the dropped-span text and the unread remainder are visible only to the lexer (`_position` at the abort; the budget break), and `FormatterService`/`TriviaCursor` share `LiteralSpans` with the LSP so a lexer-level fact stays the single definition. Rung: a lexer fact, as the charter records ("a lexer fact, not `SemanticInfo`").
- **How the fix could be inert:** (1) the fact is set but `IndentationService.BuildIndentMap`'s `catch (Exception)` path or a second lexer instance (`FormatterTwins.Lex`, `FileCompilationPipeline.Lex`) is what a route reads — verified: every route reads `BuildIndentMap(source).LiteralStateUnknown` which constructs its own `Lexer` and reads the property after `TokenizeAll` (`IndentationService.cs:128`); the sweep's `factMissing` bucket reads the same call. (2) N4's fact fires on the opener line but `RecoverFromError` runs BEFORE the hook (the span is gone) — the hook reads `_source[_position..eol]` before the call. (3) The budget break path never reaches a hook because `_position >= _source.Length` — the hook is inside the `ErrorCount >= MaxErrors` branch. Mutation per fact (Phase 2 Task 2.4) proves each one is load-bearing.
- **Before/after by direction:** every S6 cell and every hand cell is classified against 3c70ea492 as `edited string content` (fix) / `no edits` (unchanged) / `edited correctly → no edits` (a LOSS unless the document really lost a literal, which is R-FK's intended refusal). The sweep's `factSpurious` bucket (fact true while no line inside a literal of Q is lost in D's lex) is the mechanical direction guard over every corpus-derived document; the five direction controls (N1c, N1s, N4q, N4t, TWO) are hand cells that must keep their edits. Expected direction rows that are NOT losses: N2typ's full edit (an unexpected-indent re-nest that clause 6's own rationale says the routes should not make; `UnexpectedIndent_FullFallback_GetsNoEdits`) — recorded, not fixed.
- **Blast radius:** the lexer gains state and a static scanner; no token, diagnostic, span or trivia changes → `Sharpy.Compiler.Tests` (lexer classes, `LiteralStateTests`, `FormatterTwinsTests`), `Sharpy.Lsp.Tests` (all formatting classes, `FormattingRouteParitySweepTests`, `FormatterDocEditorExamplesTests`), `Sharpy.Cli.Tests` (format command — unchanged output expected), the three GapDiscovery sweeps (unaffected; the whole-solution gate covers them). No warm/cold or interop reach.
- **Generated artifacts touched:** none (no spy-stdlib, spy-test, stdlib docs or oracle ledger reach). `regen: no diff` is the expected record.

## Design Decisions

1. **One fact, four producers, recorded as flags.** `LiteralLoss` (a `[Flags]` enum: `None`, `AbortInsideLiteral`, `DroppedOpener`, `UnreadRemainder`, `RePairedCloser`) replaces the private setter of `LiteralStateUnknown`; **[CORRECTED: the enum is declared at namespace level in `Sharpy.Compiler.Lexer`, NOT nested in `Lexer` — a nested type `Lexer.LiteralLoss` beside the property `Lexer.LiteralLoss` is CS0102; a namespace-level `LiteralLoss` with a property of the same name is the ordinary Color-Color case]** `LiteralStateUnknown => LiteralLoss != LiteralLoss.None` keeps every consumer unchanged. The flags make the mechanism axis assertable (the hand matrix in `LiteralStateTests`) and let the sweep's census prove "abort-free" after the cure. Rung: lexer fact (tooling; not `SemanticInfo`).
2. **One scanner decides "holds a literal that can span lines" for both the dropped span (N4) and the unread remainder (N1).** `Lexer.HoldsALiteralSpanningLines(ReadOnlySpan<char> text)` (static, pure, new partial `Lexer/Lexer.LiteralLoss.cs`): walks the text; skips `#` comments to end of line; at a quote character, with the prefix letters immediately before it: a triple opener whose closer is absent from the text or lies on a later line → true; a single-quoted literal is skipped to its closing quote on the same line honoring backslash escapes — if the line ends first, an `f`/`t`-prefixed one → true (a replacement field may span lines, N4f), any other prefix → continue after the line break (a short string ends at its line, decision 7). Why not "any quote character" (the ruling's words): N4q and N4t are repaired today and the ruling's own rationale ("a dropped line holding a quote vs. holding code") is about what the dropped line OPENS; a `"hi"` or a `'"""'` opens nothing that spans lines. Why not unconditional at the budget stop: N1c/N1s (30-line 2-space bodies, the fallback's main use case) are repaired today and hold no literal; their loss is the regression the direction record forbids. The ruling's N1 clause is read as "stopped with a literal that can span lines left unread"; `/verify-plan` grades this reading. Known limits (allowlist residue if the generator ever builds them, cited #2271): a backslash-newline continuation inside a short string; a nested quote inside a replacement field (`f"{'"""'}"`).
3. **The re-pair fact is the closer's context, not the content.** `RePairedCloser` is set (a) when a triple-quoted literal's closer is immediately followed by `"` or `'` — never legal, since implicit concatenation does not parse (`string_literals.md`), so this costs no program; and (b) when an unterminated single-quoted literal (SPY0001/SPY0002/SPY0003/SPY0026) is reported on the line where the last triple-quoted literal closed (`_lastMultiLineCloseLine`). (a) carries N2, N2c and N2w (measured: the orphan quote is adjacent in all three); (b) carries the orphan separated by a space (`'""" '`). **[ADDED: adequacy] Known limits of any closer-context fact (verifier-measured @ 3c70ea492, all abort-free, routes refuse today for another clause):** a closer that lands inside what Q calls a comment (`# """`, X9c), a spaced orphan whose quote a comment apostrophe swallows (`t = '""" '  # don't`, X9s), two stray bare delimiter lines (X1a), and an opener swallowed by an unterminated short string on its line (`x = 'abc + """`, XSW — the dropped-span hook sees an empty span because `ReadString` aborts with `_position` on the line break). `DStringRepaired` has the same signature (a closer inside a comment) and loses nothing, so no fact short of content inspection separates them; they are a cell comment on #2271, not rows. **[RULED 2026-10-07, owner, at /verify-plan]** (b) is restricted to runs that spanned lines: `NoteMultiLineLiteralClosed()` records `_lastMultiLineCloseLine` only when the run's start line is below the close line (`startLine < _line`); N2, N2typ (`s = """abc\n    def""" + "typing`) and X8 all span lines, and a single-line `x = """a""" + 'b` keystroke costs no on-type edit. A single-line re-paired run cannot have lost multi-line content (the stray and the opener would share a line). **[RULED 2026-10-07, owner]** the four known-limit cells are a documented limit of a lexer-level fact: #2271 CLOSES with them posted as ONE cell comment (X9c, X9s, X1a, XSW: program, lexer diagnostics, route verdicts @ 3c70ea492), and S6 builds the comment re-pair as a route-only guard (Phase 1 Task 1, S6b-comment) so a future route edit on them is a sweep row, not a silent wrong. The ruling's other shape — "an opener whose line is a bare delimiter ends up inside a literal" — is NOT a fact of its own: a bare `"""` line inside a `'''` literal is a legal program (a fixture holding Sharpy source), so a fact on it would refuse on-type for a whole parseable file; the generator's `factSpurious` bucket over the corpus is what decides if it is ever added. The close sites: `ReadTripleQuotedString`, `ReadTripleQuotedByteString`, the triple arms of `ReadRawString` and `ReadDedentedRawString`, and the f-string `FStringEnd` triple arm (`Lexer.FStrings.cs:833`) — five arms, one helper `NoteMultiLineLiteralClosed()`; completeness is guarded by the prefix × quote theory (`Rows_CoverEveryLexerPrefix` already pins the prefix table to the literal list).
4. **The dropped-span hook reads the text before recovery discards it.** In `TokenizeAll`'s `catch`, before `RecoverFromError()`: the span `_source[_position .. end-of-line)`; `DroppedOpener` when the scanner says true. `_position` is the line start for the four indentation codes (measured) and the abort position for a mid-line abort (an unexpected character drops the rest of the line — `x = $"""` is a sibling the same hook covers). An unterminated short string's abort leaves `_position` at the line break: an empty span, nothing set (the `UnterminatedSingleQuotedLiteral_SetsNothing` theory keeps holding).
5. **The budget hook reads the remainder.** In the `ErrorCount >= MaxErrors` branch with `_position < _source.Length`: `UnreadRemainder` when the scanner says the remainder holds a literal spanning lines. Lines the map never sees are "dropped code lines" to it (`IndentationService.cs:38–47`), which is why a CLOSED literal past the stop is lost.
6. **The generator is S6 in the route-parity sweep, with two fact buckets.** Three sub-shapes derived from each twin's Q: S6a `budget` — `MaxErrors` lines of `x = "abc` prepended (for EVERY Q, so Qs without a multi-line literal are the direction controls), S6b `repair` — a bare triple of the first multi-line literal's quote char inserted as line 0 AND `_ = <other-quote>"""<other-quote>` appended as the last line (the count of that triple stays even → abort-free when Q's own triples are balanced), S6c `dropped` — the leading whitespace of a literal's opener AND closer line (closer line a bare delimiter line only) replaced by: +2 spaces (SPY0013), a tab (SPY0012), `\t ` (SPY0011), and on the `wide` twin width 4 inside a block (SPY0014). Routes as S5: `full`, `range-whole`, `range-line` and `ontype` on every line that starts inside a literal in Q. Oracles: the existing `content`/`literal`/`depth` plus, per document, `factMissing` (fact false while some Q-literal line does not start inside a literal in D's lex) and `factSpurious` (fact true while none is lost) — judged on every cell of the document so the allowlist rows keep their `stem twin route state bucket` shape. Ground truth: S6a shifts Q's lines by 25; S6b by 1; S6c leaves line numbers alone and removes the two delimiter lines from `LogicalStarts` (their depth may legitimately change — the repair). Census floors: S6 has ≥ 1 cell per route; ≥ 1 ABORT-FREE document per sub-shape; S6c ≥ 1 per `TripleQuotedKinds` kind and per indentation code. Abort-free at Phase 1 = `!LiteralStateUnknown`; from Phase 2 = `(LiteralLoss & AbortInsideLiteral) == 0`.
7. **No LSP code changes.** `CheckedIndentOnly`, `OnTypeFormattingHandler`, `IndentOnlyPreserved` and `BuildIndentMap` keep reading the boolean. The cure is the fact; the LSP side gets tests and the doc.
8. **Docs:** `docs/tooling/formatter.md` "In the editor" line 125 gains the three mechanisms; N2 and N4 are executed `<!-- editor-example: document, selection L-L, on-type L; unchanged -->` blocks (the extractor's literals in `FormatterDocEditorExamplesTests` move 5 → 7 examples, 7 → 13 requests, unchanged-without-document controls stay 2); N1 is a prose sentence (a 31-line block is not a reader's example) executed by direction in `FormattingFallbackTests`. The roster row in `docs/design/gap-discovery-contracts.md` names S6 and the two buckets.

## Implementation

### Phase 1: Generator FIRST — S6 and the fact buckets in `FormattingRouteParitySweepTests`

**Goal:** the standing harness builds the three abort-free shapes from every corpus document and is red on them, with every red row listed in the allowlist against #2271.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `FormattingRouteParitySweepTests` with S6 (sampled mode) and the census floors — green only with the allowlist rows present.
- Guard mutation: (i) S6b built WITHOUT the appended short-string line → the "≥ 1 abort-free S6b document" floor goes red (S6b degenerates to S5); (ii) `factMissing` computed with the lost-line set forced empty → the S6 `factMissing` rows become stale (the ratchet reports "holds now … drain") → red; (iii) positive controls: `factMissing` fires on the issue's N4 document built from its 4-space parent and `factSpurious` fires on a stub (fact true, lost lines empty) — both asserted by unit tests in the sweep class.
- Allowlist entries that drain: none in this phase; rows ADDED (every one cites `#2271`), from a FULL-mode run.
- Execution evidence the close-out will cite: `FMTROUTE-CENSUS cells … S6`, `FMTROUTE-CENSUS abort-free S6a/S6b/S6c = …`, the row count @ sha.

#### Tasks

1. **S6 state, builders and routes** — `src/Sharpy.Lsp.Tests/Conformance/FormattingRouteParitySweepTests.cs`
   - Add `S6 = "S6"` to `States` and `RoutesByState[S6] = { Full, RangeWhole, RangeLine, OnType }`; `IsSampledCell` unchanged (S6 always runs).
   - `GroundTruth` gains `Shape` (`budget` | `repair` | `dropped-SPY0011|0012|0013|0014`) beside `Kind`; `Tallies` emits `kind S6 <kind>` and `shape S6 <shape>`.
   - Builders beside `Cut`/`Inserted`: `Prepended(q, facts, lines[])` (shift by `lines.Length`), `Appended(q, facts, lineText)`, `Reindented(q, facts, lineIndexes[], leadingWhitespace)` — the last removes the re-indented lines from `LogicalStarts` and keeps `InsideLines` as they are.
   - `UnparseableDocuments` yields: S6a for every Q (`Enumerable.Repeat("x = \"abc", maxErrors)` where `maxErrors = new Lexer("").MaxErrors` — an instance property, never a literal 25 [CORRECTED: `Lexer.MaxErrors` is not static]); S6b when Q has a multi-line literal; **[RULED 2026-10-07] S6b-comment** (shape `repair-comment`) when Q has a multi-line literal: the same line-0 bare triple, with `# <triple>` appended as the last line instead of the short-string line — judged by the route buckets (`content`/`literal`/`depth`) ONLY; `factMissing`/`factSpurious` are NOT judged on this shape (a named exemption in `UnparseableOracles`, comment citing #2271: no lexer fact separates it from `DStringRepaired`); the census floor for it is "≥ 1 cell per route", no abort-free floor, and its rows at Phase 1 are expected to be zero (every route refuses it today for another clause — if a row appears it is a finding for the ledger, not an allowlist row); S6c for every multi-line literal whose closer line's first non-whitespace character is its quote character, × the four whitespace variants (SPY0014 only on `wide`, only when the opener's width ≥ 8); **[ADDED: adequacy] S6c also yields the `dropped-SPY0015` variant — `$` inserted immediately before the opener's prefix on an unchanged line (X6); abort-free whenever the literal is block-level, because the bare closer line then drops at SPY0014 under the content's width (a module-level literal's closer at width 0 is read as an opener and aborts — observed, not floored).** The state is observed as before (`ObserveParseable(text) ?? …`).
   - Commit: `test(lsp): the route-parity sweep gains S6 — the three abort-free literal-loss shapes derived from every corpus document (refs #2271)`

2. **The fact buckets** — same file + `formatting-route-parity-allowlist.txt` header
   - `SweepDocument` gains `Lazy<(bool FactSet, SCG.IReadOnlySet<int> LostLiteralLines)> LexFacts` for unparseable documents: `IndentationService.BuildIndentMap(Text)` → `LiteralStateUnknown`, and `Truth.LiteralLines` minus `LiteralSpans.LinesStartingInside(Text, LiteralSpans.Of(tokens))` (0-based).
   - `UnparseableOracles` appends `factMissing` (`!FactSet && Lost.Count > 0`, detail: the first lost line) and `factSpurious` (`FactSet && Lost.Count == 0`) to every cell's failures; `Buckets` gains both; the allowlist header documents them and S6.
   - Unit tests in the class: `PositiveControl_FactMissing_FiresOnTheIssuesN4Document` (build the issue's N4 from its 4-space parent through `Reindented`, assert the bucket and that the handler's `ontype` applied text changes the content line — the by-direction link); `PositiveControl_FactSpurious_FiresOnAStubbedFact` (a `SweepDocument` whose truth has no literal lines, fact forced through a test seam `SweepDocument.WithLexFacts(...)`).
   - Commit: `test(lsp): the route-parity sweep judges the lexer's literal-loss fact itself — factMissing and factSpurious on every unparseable document (refs #2271)`

3. **Census floors** — same file, `Census_EveryRouteAndStateHasCells_CountsAreStated`
   - Print `FMTROUTE-CENSUS literal-kind S6`, `FMTROUTE-CENSUS shape S6 <shape>=<cells>` and `FMTROUTE-CENSUS abort-free S6 <shape>=<documents>`; floors: every S6 route ≥ 1 cell; ≥ 1 abort-free document per shape; S6c ≥ 1 cell per `TripleQuotedKinds` kind and per indentation code. "Abort-free" = `!LiteralStateUnknown` at this phase (a named helper `IsAbortFree(SweepDocument)` so Phase 2 changes one line).
   - Mutation (recorded in the commit body): drop the appended line from S6b → the S6b abort-free floor red; restore → green.
   - Commit: `test(lsp): the route-parity census floors S6 — an abort-free document per shape, every literal kind and indentation code (refs #2271)`

4. **Populate the ratchet** — `src/Sharpy.Lsp.Tests/Conformance/formatting-route-parity-allowlist.txt`
   - `SHARPY_ROUTE_SWEEP_FULL=1 .claude/scripts/dotnet-serialized test src/Sharpy.Lsp.Tests --filter "FullyQualifiedName~FormattingRouteParitySweepTests" --logger "console;verbosity=normal"` → the `FMTROUTE-ROW` lines → rows `stem twin route S6 <bucket> # #2271 <shape>`; expected buckets `literal` and `factMissing` (and `depth`/`content` where a route moves a code line of a re-paired run). Any `factSpurious` row at this phase is a finding (the existing abort fact firing without a lost line) → ledger, not a row.
   - Sampled-mode run green with the rows; commit body records the row count and the FULL-run counts @ sha.
   - Commit: `test(lsp): list the S6 cells the indent-only routes edit string content on — N rows, every one #2271 (refs #2271)`

### Phase 2: The cure — widen the lexer's fact at its four producers

**Goal:** `Lexer.LiteralStateUnknown` is true for every abort-free loss mechanism, false for every direction control; the allowlist drains to empty.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `FormattingRouteParitySweepTests` with ZERO S6 rows (sampled and FULL mode); `LiteralStateTests` prefix × quote × mechanism theories; `FormattingFallbackTests`, `OnTypeFormattingTests`, `RangeFormattingTests`, `FormattingTests` unchanged and green (the five direction controls among them).
- Guard mutation: each flag's set site commented out → its `LiteralStateTests` theory red AND the matching S6 shape's `factMissing` rows reappear (the sweep red); restore → green. The scanner's unit table: invert the f/t-prefix arm → the N4f row red.
- Allowlist entries that drain: every Phase 1 row (deleted in Task 2.5's commit); the ratchet's "holds now" message is the proof.
- Execution evidence: the sweep's census @ sha (`abort-free` floors now flag-based), the probe cases of `.claude/tmp/p22f-probes/cases.json` re-driven through the built server @ sha — N1/N2/N2c/N2w/N4/N4tab/N4f/N1t → no edits; N1c/N1s/N4q/N4t/TWO → the same applied text as @ 3c70ea492.

#### Tasks

1. **`LiteralLoss` flags** — `src/Sharpy.Compiler/Lexer/Lexer.cs`
   - `[Flags] public enum LiteralLoss { None = 0, AbortInsideLiteral = 1, DroppedOpener = 2, UnreadRemainder = 4, RePairedCloser = 8 }`; `public LiteralLoss LiteralLoss { get; private set; }`; `public bool LiteralStateUnknown => LiteralLoss != LiteralLoss.None`; `ReportError` sets `AbortInsideLiteral` where it set the bool. Doc comment rewritten: four producers, one consumer contract.
   - `LiteralStateTests` existing assertions keep passing (they read the bool); add `.LiteralLoss.Should().HaveFlag(AbortInsideLiteral)` to one cut theory as the flag's own guard.
   - Commit: `refactor(lexer): LiteralStateUnknown is derived from LiteralLoss flags — the mechanism that lost the literal is recorded, not only that one was (refs #2271)`

2. **The scanner** — new `src/Sharpy.Compiler/Lexer/Lexer.LiteralLoss.cs` + `src/Sharpy.Compiler.Tests/Lexer/LiteralLossScannerTests.cs`
   - `internal static bool HoldsALiteralSpanningLines(ReadOnlySpan<char> text)` per Design Decision 2 (comments skipped; triple opener unclosed-on-its-line → true; `f`/`t`-prefixed single-quoted unclosed on its line → true; other single-quoted unclosed → skip to the line break; backslash escapes honored inside single-quoted literals).
   - Unit theory (each row a literal, not generated): `s = """` → true; `"""` → true; `s = """abc"""` → false; `x = "hi"` → false; `t = '"""'` → false; `t = '"""' + x` → false; `# s = """` → false; `x = f"{', '.join(` → true; `x = f"{a` → true; `x = r"abc` → false; `x = "a\"""` → false; multi-line remainder `def main():\n    s = """\n    k\n    """\n` → true; `def main():\n    x = 1\n` → false; `s = """a\nb"""` → true. **[ADDED: missing step] rows the verifier expects beside these:** `x = "#" + """` → true (a `#` inside a short string is not a comment — the scanner must enter the short string before it looks for `#`); `x = 1  # """` → false; `s = """abc""" + """` → true (a closed triple then an opener on the same line); `x = $"""` → true (the X6 span starts at the `$`); `x = 'abc """` → false (the swallowed opener — the known limit, pinned so the choice is recorded); `s = """\r` and `s = """\r\n` → true (the `cr`/`crlf` twins run S6; the line-end detection honours `\r`); `x = f"{a}" + "b` → false (a closed hole then an unterminated short string).
   - Commit: `feat(lexer): one scanner says whether a span of source holds a literal that can span lines — a triple opener or an f-/t-string hole left open on its line (refs #2271)`

3. **The three hooks** — `Lexer.cs` (`TokenizeAll`, `ReportError`), `Lexer.Literals.cs`, `Lexer.FStrings.cs`
   - `TokenizeAll` catch: before `RecoverFromError()`, `NoteDroppedSpan()` → `DroppedOpener |= HoldsALiteralSpanningLines(_source.AsSpan(_position, eol - _position))` where `eol` is the index of the next `\n`/`\r` or the end. In the `ErrorCount >= MaxErrors` branch: `if (_position < _source.Length && HoldsALiteralSpanningLines(_source.AsSpan(_position))) LiteralLoss |= UnreadRemainder`.
   - `private int _lastMultiLineCloseLine = -1; private void NoteMultiLineLiteralClosed(int startLine)` — sets `_lastMultiLineCloseLine = _line` ONLY when `startLine < _line` (the run spanned lines — RULED), and `RePairedCloser` when `_position < _source.Length && _source[_position] is '"' or '\''`; called at the five close sites (Design Decision 3) with the literal's start line (the f-string arm passes `context.StartLine`). `ReportError`: `if (line == _lastMultiLineCloseLine && code is UnterminatedString or UnterminatedFString or UnterminatedRawString or UnterminatedByteString) LiteralLoss |= RePairedCloser` — placed next to the existing abort check.
   - No token, span, trivia or diagnostic changes: `sharpyc emit tokens`/`emit diagnostics` on every probe document byte-identical before/after (recorded in the commit body).
   - Commit: `fix(lexer): the literal map is unknown when a dropped line or the unread remainder holds a literal that can span lines, or a triple-quoted closer re-paired into a short string — not only after an abort inside a read (refs #2271)`

4. **The lexer-level hand matrix** — `src/Sharpy.Compiler.Tests/Lexer/LiteralStateTests.cs`
   - Theories over `PrefixByTripleQuote` (the existing member data): `OpenerLineDroppedAtAnIndentationError_SetsDroppedOpener` × whitespace {`  ` (SPY0013), `\t` (SPY0012), `\t ` (SPY0011)} and a SPY0014 shape (wide body, delimiter lines at width 4); `BudgetStopWithALiteralLeft_SetsUnreadRemainder` (25 × `x = "abc` + the literal) ; `ClosedLiteralRePairedIntoAShortString_SetsRePairedCloser` × orphan placement {adjacent `'"""'`, spaced `'""" '`, comment-swallowed `'"""'  # don't`} × quote chars swapped; the f-string hole twin (`x = f"{', '.join(` at a dropped width) sets `DroppedOpener`; **[ADDED: adequacy]** `OpenerLineDroppedAtAMidLineUnexpectedCharacter_SetsDroppedOpener` × `PrefixByTripleQuote` (`x = $<prefix><quotes>` on a valid-width line with the bare closer under a wider content line — X6).
   - Positive controls (the fact stays `None`): TWO; `def main():\n  x = "hi"\n  print(x)`; `def main():\n  t = '"""'\n  print(t)`; `def main():\n  s = """abc"""\n  print(s)`; 30 × `  x = 1` (budget, no literal); 30 × `  print("hi")`; `x = 1__2` on a closer line's neighbour; **`x = """a""" + 'b` (a single-line triple run then an unterminated short string on the same line — the RULED (b) restriction's own control, fact `None`)**; `DStringRepaired` (its re-pairing is into a comment, no orphan — the `FormattingFallbackTests` assertion `LiteralStateUnknown.Should().BeFalse` keeps holding).
   - Corpus test extension: for every clean corpus document with a multi-line literal, the three abort-free shapes (built with the same recipe as S6 — a shared helper in `Sharpy.TestInfrastructure/Formatting/FormatterTwins.cs` so the sweep and the lexer test derive the SAME documents) set the matching flag; every clean document stays `None`.
   - Mutation record in the commit body: each of the four set sites disabled in turn → the named theories red (counts), restored → green.
   - Commit: `test(lexer): every abort-free loss mechanism × every triple-quoted prefix × both quote characters sets its LiteralLoss flag; a dropped line holding a short string sets nothing (refs #2271)`

5. **Drain the ratchet; switch the abort-free floor to the flag** — `formatting-route-parity-allowlist.txt`, `FormattingRouteParitySweepTests.cs`
   - Delete every S6 row (the ratchet reports each as "holds now"); `IsAbortFree` = `(lexer.LiteralLoss & AbortInsideLiteral) == 0`; FULL-mode and sampled runs green with 0 rows; census floors hold.
   - Commit: `test(lsp): the S6 rows drain — no indent-only route edits a literal lost without an abort; the abort-free floor reads the LiteralLoss flag (refs #2271)`

### Phase 3: LSP cells by direction and the direction record

**Goal:** the issue's documents and this plan's siblings are refused on every route through the real handlers, the direction controls keep their edits, and every newly refused edit is listed against 3c70ea492.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: the new facts in `FormattingFallbackTests` / `OnTypeFormattingTests` / `IndentationServiceLiteralStateTests`.
- Guard mutation: with Task 2.3's hooks reverted (a `cp` of the pre-fix file), every "no edits" cell red and every positive control still green (proves the controls are not vacuous); restore.
- Allowlist entries that drain: none.
- Execution evidence: the by-direction cells' recorded unchecked candidates (the damaged text each handler applied @ 3c70ea492, from `.claude/tmp/p22f-probes/out2.json`) and the direction table in the commit body.

#### Tasks

1. **By-direction hand cells** — `src/Sharpy.Lsp.Tests/FormattingFallbackTests.cs`, `OnTypeFormattingTests.cs`, `IndentationServiceLiteralStateTests.cs`
   - Constants N1, N2, N2c, N2w, N4, N4tab, N4f, N1t, **X6 [ADDED: adequacy]** (the exact texts of `.claude/tmp/p22f-probes/*.spy`; X6 = `def main():\n    x = $"""\n        key: value\n    """\n    print(x)\n`, full + range-line 2 edited string content @ 3c70ea492); for each, the unchecked candidate asserted (`ReindentDocument` / `RangeCandidate` / the on-type candidate equals the damaged text of @ 3c70ea492) and `_driver.Full/Range/OnType(...).Edits.Should().BeEmpty()` for the routes the Current State table lists.
   - Direction controls: `_driver.Full(N1c)`, `(N1s)`, `(N4q)`, `(N4t)`, **`(X4)` [ADDED: `def main():\n  s = """abc"""\n  print(s)\n`, repaired @ 3c70ea492]** applied texts equal their @ 3c70ea492 texts (4-space repairs); `ParseError_TwoSpaceDocument_TabSize2_FallbackWritesFourSpacesAsync` (both files) untouched and green.
   - `IndentationServiceLiteralStateTests`: N1/N2/N4 → `LiteralStateUnknown` true; N4q/N4t/N1c → false.
   - Commit: `test(lsp): the budget, re-pair and dropped-delimiter documents get no indent-only edits on every route, and a 2-space document holding short strings keeps its repair (refs #2271)`

2. **Direction record** — commit body of Task 3.1 (and the close-out)
   - Re-drive `.claude/tmp/p22f-probes/cases.json` through the rebuilt server (`python3 .claude/tmp/p22e-probes/lspfmt.py cases.json out-after.json`, sandbox off) and tabulate each case: @ 3c70ea492 → @ fix, classified `edited string content → no edits` (fix) / `no edits → no edits` / `edited correctly → edited correctly` (control kept) / `edited → no edits on a document that lost a literal` (R-FK's intended refusal; N2typ's unexpected-indent re-nest goes here) / `edited correctly → no edits on a document that lost NO literal` (a LOSS — must be zero; a non-zero entry is a Phase 2 defect, not a row).
   - The sweep's `factSpurious` count (0 rows) is cited as the corpus-wide version of the same record.

### Phase 4: Docs and the roster

**Goal:** `docs/tooling/formatter.md` states the three mechanisms with executed examples; the harness roster names S6 and the fact buckets.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `FormatterDocEditorExamplesTests` with its literals moved (examples 7, requests 13, unchanged-without-document controls 2) — each new example driven through the real handlers.
- Guard mutation: change one new example's `on-type` line to a line outside the literal (which the route DOES edit) → the test red; restore. The extractor literals are the instrument check against a silently skipped example.
- Allowlist entries that drain: none.
- Execution evidence: the test's pass @ sha; the roster row diff.

#### Tasks

1. **formatter.md "In the editor"** — `docs/tooling/formatter.md:125`
   - Extend the sentence: "…or cannot be read as a string — because the file's error budget stopped the lexer above it, a stray `"""` above it re-paired its quotes into a later `'"""'`, or its opening `"""` sits on a line the lexer could not read at all (a 2-space or tab indent) — the editor cannot tell which lines are code. Format on type and the indentation-only pass change nothing until the string is closed."
   - Two executed examples: N2 (`<!-- editor-example: document, selection 4-4, on-type 4; unchanged -->`, the issue's text) and N4 (`<!-- editor-example: document, selection 3-3, on-type 3; unchanged -->`); one prose sentence for N1 naming the budget (25 lexer errors) and pointing at `--max-errors` for the CLI.
   - `FormatterDocEditorExamplesTests`: `HaveCount(5)` → 7, requests 7 → 13, `controls.Should().HaveCount(2)` unchanged (both new examples request `document`). Run the test before committing (docs need execution evidence).
   - Commit: `docs(formatter): the three ways a triple-quoted string is lost without the lexer stopping inside it, each an executed editor example or a stated rule (refs #2271)`

2. **Roster and allowlist header** — `docs/design/gap-discovery-contracts.md` (the "Formatting route parity (#2168)" row), `formatting-route-parity-allowlist.txt` header
   - The row's contract gains "…and `Lexer.LiteralStateUnknown` is true for every loss mechanism (abort inside a read, dropped opener line, unread remainder past the error budget, re-paired closer), judged directly by `factMissing`/`factSpurious`"; the harness column gains "S6 the three abort-free shapes (budget, repair, dropped delimiter × SPY0011–0014)"; cites #2271.
   - Commit: `docs(design): the route-parity roster row names S6 and the literal-loss fact buckets (refs #2271)`

## Testing Strategy

- **Generator first (contract §11):** Phase 1's S6 over the corpus × five twins is the generative harness; `formatting-route-parity-allowlist.txt` is its ratchet (rows cite #2271, deleted in Phase 2 Task 5). The axis it cannot enumerate is WHICH fact fired — that is the `LiteralStateTests` hand matrix (prefix × quote × mechanism, with the flag asserted), plus the scanner's literal row table.
- **New fixtures:** none file-based (LSP behaviour is not a `.spy`+`.expected` cell). Every negative cell (no edits) has a positive control in the same class: TWO, N4q, N4t, N1c, N1s keep their repairs; the sweep's own S3 "applied-differs" floor stays the guard against a fact that is always true.
- **Edge cases outside the issue's list:** N2c (comment-swallowed orphan), N2w (swapped quote chars), N4f (f-string hole continuation line), N4m (fact false, routes refuse for another reason — `factMissing` only), SPY0011 mixed tabs/spaces, a mid-line unexpected character before a triple (`x = $"""`), `s = """abc"""` on a dropped line (must stay repairable), CRLF/CR twins of every shape (the sweep's `crlf`/`cr` twins run S6 too — the scanner's line-end detection must honour `\r`).
- **Outputs that discriminate:** every by-direction cell asserts the damaged unchecked candidate (what the handler applied @ 3c70ea492) beside the empty edit list; an applied-text equality on a document the route never edited proves nothing (the `FormatterDocEditorExamplesTests` positive control pattern).
- **Spec/doc deliverable of equal weight:** `formatter.md`'s two new examples are executed by `FormatterDocEditorExamplesTests` before the commit; the extractor's literals move with them.
- **Blast radius and regen:** `Sharpy.Compiler.Tests` (lexer, formatter twins) and `Sharpy.Lsp.Tests` are the edit loop; the commit gate is the whole solution once at the end of Phase 4 (`.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`, private log, counts `passed/failed/skipped @ sha (measured)`); `regen: no diff @ sha` is recorded after Task 2.3 (`build_tools/regenerate_spy_stdlib.sh`, spy-test regen, `check_spec_blocks.sh` — none reach a lexer fact, the record proves it). Every probe goes through the built `sharpyc`/`Sharpy.Lsp` binaries or the serialized wrapper; no raw `dotnet`.
- **Mutation records (contract §2):** Phase 1 Task 3 (S6b without the appended line → floor red); Phase 2 Task 4 (each flag's set site disabled → named theories red; the sweep's `factMissing` rows reappear); Phase 3 Task 1 (hooks reverted via `cp` → every no-edit cell red, controls green); Phase 4 Task 1 (an example's line moved outside the literal → red). Each commit body carries "broken → red (N), restored → green (N)".

## Issues to Close

- #2271 — lsp: the indent-only formatting routes edit string content when a triple-quoted literal is lost without a lexer abort — closed by Phase 2 Task 5 + Phase 3 Task 1 + Phase 4 Task 1; **close criterion:** (1) `FormattingRouteParitySweepTests` green in sampled AND FULL mode with ZERO rows, S6 census floors (abort-free per shape, every kind, every indentation code **including `dropped-SPY0015` [ADDED]**) holding @ sha; **the four known-limit cells (X9c, X9s, X1a, XSW) posted as ONE cell comment on #2271 before it closes [RULED 2026-10-07: #2271 closes; the S6b-comment shape is their standing route guard]**; (2) the issue's N1, N2, N4 documents and the 13 N4 siblings (tab, widths, `r"""`, `f"""`, docstring — the S6c kind × code floors plus the hand cells) get no edits on every route through the real handlers @ sha; (3) the direction record lists zero `edited correctly → no edits` entries on a document that lost no literal, with N1c/N1s/N4q/N4t/TWO's repairs byte-identical to @ 3c70ea492; (4) the two doc examples execute @ sha; (5) `emit tokens`/`emit diagnostics` byte-identical on every probe document before/after (the fact changes no token or diagnostic).

## Class trackers this plan may post cells to

- #2271 — literal loss without a lexer abort (THIS class) — a loss mechanism the scanner or the hooks miss (a backslash-newline continuation inside a short string, a nested quote inside a replacement field, a bare delimiter line inside a literal of the other quote char) is a comment here with its S6 row.
- #2232 — LSP code-action source-shape preservation (P47) — a handler-driver finding that is not a formatting route.
- #2239 — formatter blank-line preservation (P50) — a Pretty/formatter output cell.
- #2169 — constructs the formatter declines (SPY0912) — a `net`/`whole` bucket finding on S1/S2.
- A cell of no class above (for example `IndentOnlyPreserved` accepting a re-nest on a document whose lex is clean, or a `FormatterService` seam) goes to the findings ledger; the lead files ONE class issue with a cell table, `lane:*` + `reach:*` labels. No implementer runs `gh issue create`.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-07 @ 3c70ea492
**Plan file:** .claude/plans/plan-add686.md

Probes: `python3 build_tools/check_plan_refs.py --issues` (11/13 paths, 8/9 `file:line`, 11/11 SPY codes, 57/70 symbols — every miss is a symbol or file the plan proposes; `Lexer.cs:291-301` ambiguous only because two files are named `Lexer.cs`, the compiler's is the one meant and holds the budget branch); `gh issue view 2271` (OPEN, `class-tracker` + `lane:silent-wrong` + `reach:program`; the R-FK comment reads as the plan quotes it); the plan's 37 `cases.json` cells re-driven through the built server (`Sharpy.Lsp` 11:18 = the HEAD commit's minute) — applied texts byte-identical to `out2.json`, 0 differences; 22 verifier cells (`.claude/tmp/p22f-probes/xcases.json` → `xout.json`, `.spy` per document) + `sharpyc emit diagnostics`/`emit tokens` on them; the lexer read by hand at `ReportError`, `TokenizeAll`'s catch and budget branch, `RecoverFromError`, `HandleLineStartIndentation` (`_position = savedPos` before `MeasureIndentation` throws — the dropped span IS the whole line), `ReadString` (an unterminated short string aborts with `_position` on the line break — the span the hook reads is empty), the five triple-quoted close arms (`"""`/`d"""` share `ReadTripleQuotedString`; `r`/`dr` have their own arms; `b` its own; `f`/`t`/`df` the `FStringEnd` triple arm at `Lexer.FStrings.cs:833`), `IndentationService.BuildIndentMap` (one `Lexer` per call, the bool read after `TokenizeAll` at line 128; every route reads it through `CheckedIndentOnly`/`OnTypeFormattingHandler.cs:68`/`IndentOnlyPreserved`), the sweep's `States`/`RoutesByState`/`Buckets`/`GroundTruth`/`Cut`/`Inserted`/`UnparseableOracles`/`Census` and the EMPTY allowlist, `LiteralStateTests` (`PrefixByTripleQuote`, `Rows_CoverEveryLexerPrefix`, `UnterminatedSingleQuotedLiteral_SetsNothing`, the corpus test), `FormatterDocEditorExamplesTests` (5 examples / 7 requests / 2 controls; the marker grammar accepts `document`, `selection A-B`, `on-type N`, `; unchanged`; controls count only unchanged examples with no `document` request, so 2 stays), `formatter.md:125`, the roster row, `string_literals.md` "No Implicit Concatenation" (SPY0103), `DStringRepaired` (its comment re-pair loses nothing).

### Corrections Made
- Decision 1: `LiteralLoss` enum declared at namespace level, not nested in `Lexer` (a nested type and a property of the same name in one class is CS0102). Before: `Lexer.LiteralLoss (a [Flags] enum …)`; after: namespace-level enum, property `Lexer.LiteralLoss` of that type.
- Phase 1 Task 1: `Lexer.MaxErrors` is an instance property; the builder reads `new Lexer("").MaxErrors` (the plan said both; the static spelling is removed).
- Defect Class matrix: the dropped-delimiter code axis gains **SPY0015** (X6: `x = $"""` on a valid-width line — full and range-line edit string content today; the plan's own Decision 4 named the shape but the matrix, S6c and the hand matrix did not build it). S6c gains the `dropped-SPY0015` variant; `LiteralStateTests` gains its theory; Phase 3 gains the X6 constant; the census floor names it.
- Defect Class matrix + Decision 3: four **known-limit cells** named (X9c comment re-pair, X9s spaced orphan swallowed by a comment apostrophe, X1a two stray bare delimiters, XSW opener swallowed by an unterminated short string) — abort-free losses with the same lexer signature as `DStringRepaired`, refused on every route today for another clause (`factMissing`-only). The plan's "ZERO S6 rows" acceptance held only because S6b never builds them; they are now stated, with the author's choice recorded in Issues to Close.
- Phase 2 Task 2 scanner table: seven rows added (a `#` inside a short string; a trailing comment holding a triple; a closed triple then an opener on one line; `x = $"""`; the swallowed opener pinned as false; `\r` and `\r\n` line ends; a closed hole then an unterminated short string).
- Scope: verify cost and the prober's budget stated.
- Current State: X6, X4 and the four known-limit documents added as verifier-measured rows.

### Adequacy
- **Contract:** named — `Lexer.LiteralStateUnknown` is true for EVERY mechanism that loses a literal spanning lines, so no indent-only route edits string content of the document the user wrote.
- **Matrix:** larger than the repro lists — mechanism × literal kind × quote char × route × document × direction controls; the verifier added the SPY0015 code value (a route cell the plan's own decision named but never built) and four known-limit cells. Sites scanned: every `throw ReportError` reaches `TokenizeAll`'s catch (one hook), the budget branch is the only stop with source left, the five close arms are the only places a multi-line literal closes. The `_resumeAfterUnclosedField` path re-lexes from the line after the bracket, so it loses nothing (checked, not a cell).
- **Cure:** seam — one producer widened at the lexer (the fact every route already reads), then the generator; meta-class row "position/route missing a shared check" (the check exists, its INPUT was partial) + silent wrong behavior.
- **Harness:** named + allowlist delta — `FormattingRouteParitySweepTests` S6 (Phase 1 adds; rows cite #2271; Phase 2 Task 5 drains to empty), `LiteralStateTests` for the mechanism axis.
- **Layer:** every decision rung-justified — a lexer fact (tooling; the charter records "a lexer fact, not `SemanticInfo`"); the ladder's Core/Stdlib rungs do not apply. Decision 2's narrowing of the ruling's "a line holding a quote character" to "holding a literal that can span lines" is graded CORRECT: the ruling's own direction clause forbids losing N4q/N4t's repairs, and both are measured repaired today; the budget clause's reading ("a literal left unread") is likewise forced by N1c/N1s.

### Scope & convergence
- **Group:** one (P22f, charter §3.1, R-FK) · **Alone:** no — required? no (no printed output or emitted layout changes; `emit tokens`/`emit diagnostics` byte-identity is an acceptance bullet) · **Gates:** 1
- **Generator:** built first in Phase 1 (S6 over corpus × five twins; the hand matrix covers only the "which fact fired" axis, as the plan states)
- **Trackers named:** #2271 #2232 #2239 #2169 · **Fidelity rows:** N/A (editor surface, not a Python-parity cell)

### Falsifiability
- S6 builders + census floors (P1 T1/T3): mutation named (S6b without the appended line → the abort-free S6b floor red); exemption not subject; positive control n/a; by-direction probe specified (every S6 cell classified against 3c70ea492).
- `factMissing`/`factSpurious` (P1 T2): mutation named (lost-line set forced empty → rows stale → ratchet red); exemption not subject (breaking the producer makes the sweep redder, not greener); positive controls yes (N4 from its 4-space parent; a stubbed fact); by-direction: the `factSpurious` bucket is the corpus-wide direction guard.
- Allowlist population (P1 T4): the ratchet itself; rows cite #2271; "holds now" on drain.
- `LiteralLoss` flags (P2 T1): mutation named (`HaveFlag(AbortInsideLiteral)` on one cut theory); exemption not subject.
- Scanner table (P2 T2): mutation named (invert the f/t arm → the N4f row red); the verifier's added rows each pin one arm (comment vs string `#`, same-line close-then-open, `\r`).
- Hooks (P2 T3): mutation named per set site (P2 T4 counts recorded); inert-fix check: the plan traces every route to the one `BuildIndentMap` lexer; `emit tokens`/`diagnostics` byte-identity is an absence assertion whose positive control is the diagnostics the probe documents already carry.
- Hand matrix (P2 T4): mutation named (each set site disabled → named theories red); positive controls (TWO, N4q, N4t, X4, N1c, N1s, `s = """abc"""`, `DStringRepaired`) yes.
- Drain (P2 T5): mutation = the ratchet's stale-row red on a re-added row; positive control = Phase 1's populated state.
- By-direction cells (P3 T1): mutation named (hooks reverted via `cp` → every no-edit cell red, controls green); discriminating outputs yes (the damaged unchecked candidate asserted beside the empty edit list).
- Doc examples (P4 T1): mutation named (an example's on-type line moved outside the literal → red); the extractor literals 5→7 / 7→13 are the instrument check.

### Warnings
- ~~The four known-limit cells are a scoping decision~~ **RULED 2026-10-07 (owner): #2271 closes with the four cells as one comment; S6b-comment is built as a route-only guard (no fact bucket on that shape).** Recorded in Decision 3, Phase 1 Task 1, Issues to Close.
- ~~Decision 3(b) refuses single-line keystrokes it need not~~ **RULED 2026-10-07 (owner): (b) is restricted to runs that spanned lines (`startLine < _line`); `x = """a""" + 'b` is the control theory.** Recorded in Decision 3, Phase 2 Tasks 3 and 4.
- **S6a on a Q whose only multi-line span is a single-quoted f-string hole** sets `UnreadRemainder` (the scanner's f/t arm) — correct (those continuation lines are `InsideLines`), but the "abort-free S6a ≥ 1" floor should not rely on such a Q; the corpus has plain literals, so it will not.
- **S6c `dropped-SPY0015` at module level aborts** (the closer at width 0 is read as an opener); the census floor for that code must be satisfied by a block-level literal — the plan now says "observed, not floored" for the module-level case; keep the floor at ≥ 1 abort-free document, not ≥ 1 per literal position.
- **`emit tokens`/`emit diagnostics` byte-identity** (P2 T3, close criterion 5): the fact is read by `IndentationService` only, but `FormatterService.Format` and the CLI lex the same files — the Cli.Tests slice of the gate is the executing check; the byte-identity record should name the command and the probe set, not "every probe document".
- **Phase 1's FULL-mode run** is a `Sharpy.Lsp.Tests` filtered run (the edit loop); the plan says so. The whole-solution gate at the end of Phase 4 is the only gate — four phases on one gate is allowed (one group, no layout change) but the implementer should run the Lsp.Tests + Compiler.Tests lexer classes after every commit (the plan's edit loop) so a red is attributed to its commit.

### Missing Steps Added
- Scope: verify cost + prober budget line.
- Phase 1 Task 1: the `dropped-SPY0015` S6c variant.
- Phase 2 Task 2: seven scanner rows.
- Phase 2 Task 4: the SPY0015 theory.
- Phase 3 Task 1: X6 cells and the X4 direction control.
- Issues to Close: the known-limit cells' disposition.
- (Not added, already present and checked:) regen record after Task 2.3; whole-solution gate command; allowlist rows citing #2271; `DiagnosticExplanations` — no new codes; LSP handlers — no changes; `MergeFrom` — no `SemanticInfo` dictionary.

### Unchecked Claims
- "Implicit string concatenation never parses" is read from the spec and `SPY0103`; the lexer-level consequence (a triple closer immediately followed by a quote character never occurs in a parseable program) was reasoned over the lexer's arms, not executed over the corpus — the `factSpurious` bucket over every clean Q's S1/S2 documents is the executing check once Phase 1 lands (a clean Q with the fact set would be a row).
- The S6b builder's abort-free claim ("the count of that triple stays even when Q's own triples are balanced") was reasoned, not run; Phase 1 Task 3's floor measures it. A Q holding a triple inside a short string (`t = '"""'`) makes S6b abort — expected, the floor is ≥ 1 document, not all.
- Whether `_position` sits on or after the `$` at the SPY0015 abort was not read (the `_ =>` arm is reached after the character was consumed in `NextToken`); the scanner returns true from either offset (`$"""` or `"""`), so no decision rests on it.
- No `python3` claim is made by the plan (editor surface); none was probed.

## Implementation Checklist

Base sha: 3c70ea492 · baseline gate 77942/0/4 @ 3c70ea492 (measured 2026-10-07, chores round #2264/#2265 — same sha, reused) · board = this section.

- Wave 1 lead decision (2026-10-07): the sweep found two row families the cure cannot drain — F1 (S6a over a Q with no multi-line literal: a column-0 bracket continuation past the budget pops the indent map; not a literal loss) → **#2273**, sweep state `S6n`; F2 (S6b-comment rows exist on `b"""`/`r"""` with a column-0 closer — the RULED "zero rows" premise was false) → **#2274**, the known-limit tracker (also holds X9c/X9s/X1a/XSW), sweep state `S6k`. `S6` keeps budget-with-literal, repair, dropped-*; every S6 row drains in Phase 2. #2271's close-out comment points at #2274 for the known-limit cells (the ruling's "one comment" stays; the rows need an open issue).
- Wave 1 lead decision 2: F3 (no corpus literal gives `dropped-SPY0015` an abort-free document — every bare closer sits at the content's width or column 0) and F4 (no `"""`/`f"""`/`t"""` literal closes on a bare delimiter line) are corpus gaps, inside the contract → ONE new fixture `strings/triple_quoted_delimiter_lines.spy` (Python-layout plain/f/t/`'''` literals, run-verified `.expected`), own commit before the S6 commits; floors unchanged.
- [x] P1 done: 843abc9c1 fixture · 8e52c937e T1 · cb2f7268f T2 · 035e51506 T3 · 95e1b0574 T4 (808 rows: S6 #2271 434, S5 #2271 20, S6n #2273 334, S6k #2274 20) · a0ba80a9e range-line identity ceiling re-pinned 3% → 3.1% (33/1078 after the fixture; stricter than R-DK's whole-percent rounding). Mutation of S6b's '≥ 1 abort-free' floor was vacuous (10/75 without the line) → a majority floor added.
- (superseded line) P1 T1–T4 — S6 builders (text recipes shared via `FormatterTwins`), fact buckets, census floors, ratchet populated from FULL mode — test-expert (main tree) — blocked by nothing
- [x] P2 T1–T4 cherry-picked: 2c7760024 · 6c323417f · 428fd31db · 72715b042 (lead re-ran RePairedCloser both-arms mutation: 97 red / 1129, restored green)
- (superseded line) P2 T1–T3 + T4 (theories, minus the corpus extension) — `LiteralLoss` flags, scanner + table, hooks, hand matrix — parser-expert (isolated worktree; lead cherry-picks after P1 lands) — blocked by nothing (lands after P1)
- [x] 3b60a0183 ceiling re-pinned 3.1% → 4% (agent acted on the lead's superseded message; 4% = R-DK's own whole-percent rule, kept) · bd13a3ce8 P2 T4 corpus extension · 3ea365007 P2 T5 drain (454 #2271 rows; 354 remain: #2273 334, #2274 20). Lead re-ran UnreadRemainder-off: sweep 75 failed / 16475, restored 16475 green.
- (superseded line) P2 T4 corpus extension (shared S6 recipe) + P2 T5 drain + `IsAbortFree` → flag — blocked by P1, P2 T1–T3
- [ ] P3 T1 by-direction LSP cells + P3 T2 direction record (re-driven probes) — blocked by P2 T5
- [x] P4 e526ccf13 (formatter.md; doc test mutation: hooks reverted → 1/2 red, restored 2/2) · 520630c77 roster row
- (superseded line) P4 T1 formatter.md examples + P4 T2 roster/allowlist header — blocked by P2 T5
- [x] P3 4b7428567 (cherry-picked from 8d2e9d1f7): 19 route cells + 5 direction controls + fact cells; hooks reverted → 17/73 red, controls green; direction record 18 fix / 2 R-FK refusal / 6 kept / 33 unchanged / 0 LOSS
- Final verification @ 4b7428567 (verification-expert, opus) REFUTED class (V1 mid-line short-string abort span) and direction (V2 f/t arm too wide, V3 arm (a) single-line); all in contract → wave 4 fix (lead decision: arm (a) gets the same `startLine < _line` restriction the owner ruled for (b), by the ruling's own rationale).
- Gate @ 4b7428567: 78216/0/4 (measured, 5 assemblies, 36 min; baseline 77942/0/4 @ 3c70ea492).
- [x] Wave 4 cherry-picked: bcc54f3e0 (S6c dropped-SPY0004 — recipe inserts `"\q" + ` before opener AND closer; generator red 45 failed / 239 rows) · 498a66cc1 (V1 `_tokenStart` span, V2 open-field f/t arm, V3 arm (a) spanned-lines; + found Dq_closed `x = "\q" + """abc"""` spurious-opener sibling, fixed) · 37e5c1d02 (V4 docs). Lead re-ran M1 (V1 off): 82 / 1225 red, restored 1225 green.
- (superseded) Wave 4: V1–V4 fix + tests + S6c `dropped-SPY0004` — parser-expert (worktree)
- Second verification @ 37e5c1d02: wave-4 fixes hold on every first-pass cell; REFUTED on nested-string-in-hole siblings (V5) → wave 5.
- Gate @ 37e5c1d02: 78318/0/4 (measured, 5 assemblies). Staleness @ 37e5c1d02: check_spy_staleness 0, check_spy_tests_staleness 0 (regen: no diff — a lexer fact reaches no generated artifact), whole-sln format whitespace --verify-no-changes 0, check_allowlist_issue_state 0.
- [x] Wave 5 390d701d7 (cherry-picked from d37616c28): scanner reads nested literals in replacement fields; M0 (old scanner) → 22/1264 lexer + 4/45 fallback red; `_tokenStart` guard conditions now pinned (a: 2 rows, b: backtick row); FULL 16475/0; direction 898 cells, 0 LOSS (W13 `f"{'}'` = Decision-2 refusal by design); emit 609/0 differ.
- [x] Wave 6 5c8d81f28 (lead): top-level backtick name opaque (wave-5 lane's sibling `x = $`it's` + """`); mutation → 5 lexer + 1 fallback red, restored green; Lexer|Formatter 14757/0, Lsp Formatting|IndentationService 19974/0; original 59 cells @ 5c8d81f28: 18 fix + 2 R-FK + 6 kept + 33 unchanged, 0 LOSS.
- (superseded) Wave 5: scanner models nested literals inside replacement fields — parser-expert (worktree)
- **[RULED 2026-10-07, owner, after implementation]** (1) RePairedCloser arm (a) keeps the `startLine < _line` restriction the lead extended from the (b) ruling (498a66cc1) — a single-line run cannot have lost multi-line content; N2/N2c/N2w and the one-line controls pin both directions. (2) #2274 is the open home for the known-limit re-pair cells (X9c, X9s, X1a, XSW + the S6k `b"""`/`r"""` route rows); #2271 closes at /verify-implementation with a comment pointing at #2274 (option (a); rejected: keep #2271 open as home; stop judging routes on the comment re-pair shape).
- [x] Final @ 5c8d81f28: build 0 warnings; gate 78368/0/4 (measured, 5 assemblies); FULL route-parity sweep 16475/0/0; allowlist 354 rows (#2273 334, #2274 20), check_allowlist_issue_state 0; staleness (spy, spy-tests) 0 and whole-sln whitespace verify 0 @ 37e5c1d02 (waves 5–6 touch only the scanner and tests); two verification-expert passes (opus) — both refutations fixed in waves 4–6. Not pushed; #2271 not closed (that is /verify-implementation's).
- (superseded) Final: build, whole-solution gate, regen record, whitespace, verification-expert — blocked by all

## /verify-implementation — 2026-10-07 @ 729bf1e7d (range 3c70ea492..729bf1e7d)

**Result:** VERIFIED WITH REMEDIATION — the cure holds on the plan's matrix; two in-contract residues
(found by the prober's two axis changes and the class-cure auditor's differential) are posted on #2271,
which STAYS OPEN as their tracker (the owner's "closes at verify" ruling was premised on a covered matrix).
Four auditors: completeness 17 DONE / 2 PARTIAL (1e weak iff-link, 1f census folded — both load-bearing
under mutation, left) / 1 benign DIVERGED / 0 MISSING; regression NOT REFUTED (0 regressions; all
staleness gates 0; tokens byte-identical BASE vs HEAD over 3903 files + 600 fuzzed documents);
class-cure: 13 mutations red→green, 2 REFUTED cells + 1 by-direction LOSS; prober: 840 route cases,
86 FAIL in 3 classes, 0 LOSS, all pre-existing at base.

- Gate @ 5c8d81f28 (HEAD): 78368/0/4 (measured, 5 assemblies). Gate @ bc1ea697b: 78392/1/4 (measured —
  the red is SourceGlobConformanceTests catching the new differential's unfiltered glob; fixed in
  729bf1e7d). **Gate @ 729bf1e7d: 78393/0/4 (measured, 5 assemblies).**
- Remediation commits: fc626bcb2 (budget stop reads the remainder from the aborted literal's start — one
  UnreadTextStart for both hooks; B1 LOSS + the 18 BS route cells; M1 broken → 4 lexer + 2 LSP red,
  restored green), bc1ea697b (LiteralLossScannerDifferentialTests — corpus + clean hand rows; M2 broken →
  18 lexer + 2 LSP red incl. the differential, restored green; E1par2's inert full/range-whole entries
  dropped; #2274 cite; ceiling message), 729bf1e7d (non-source glob filter).
- FULL route-parity sweep @ bc1ea697b: 16475/0/0, 354 rows (#2273 334, #2274 20), factMissing 0,
  factSpurious 0, abort-free floors budget 75/75, repair 65/75, dropped-0011/12/13 69/69, 0014 14/14,
  0015 20/70, 0004 70/70. Staleness @ bc1ea697b (spy, spy-tests, spec blocks 1204/0, allowlist state) 0.
  Whitespace verify @ bc1ea697b 0 (re-run @ 729bf1e7d queued).
- Direction on the fixed binary: the 59 probe cells unchanged vs @ 5c8d81f28 (18 fix / 2 R-FK / 6 kept /
  33 unchanged / 0 LOSS); BS-* 18 cells → no edits; B1, B2 keep their repair.
- Residue (on #2271, for /batch-issues as P22g): (A) RePairedCloser arm (b) keyed on 4 unterminated
  codes — any other abort on the close line (SPY0015/0018/0007/0008/0022) leaves the fact None, 21
  documents, ARM verdict, cure = fire on ANY lexer error on the close line (owner rules its direction
  cost); (B) scanner format-spec arm: `f"{x:">}" + """` — lexer aborts SPY0022, model closes the
  f-string; SQ2/SQ3 edited. (C) #2274 gains M3-backtick-unterminated (an actual route edit) + the
  lexer-diagnostics column. Ledger-only (no tracker): diagnostics-order nondeterminism in
  errors/edge_cases/multifile_error_recovery (both shas); cosmetic quote normalization to an escaped
  triple (value-equal). Suggestions not taken: derive the `30` past-budget bodies from Budget; `IsAbortFree`
  spelled twice; X6's cells are double-carried by the SPY0014 closer line.

### Close-out — 2026-10-08 (owner ruling: "push to dev and mainline and close the issues")

- Pushed `3c70ea492..729bf1e7d` to `origin/dev` and fast-forwarded `origin/mainline` to the same sha (24 commits). No version bump (0.22.0 == v0.22.0; `Directory.Build.props` untouched, so no auto-tag/release).
- Allowlist issue-state gate EXIT=0 before and after the close (`.claude/tmp/p22f-verify/allowlist-state-{pre,post}-close.log`).
- #2271 CLOSED (completed). Residue family A → #2275, family B → #2276 (both `lane:silent-wrong reach:program class-tracker`), filed by class with the cell tables from the verify comment. #2273 and #2274 stay open (354 allowlist rows cite them).
