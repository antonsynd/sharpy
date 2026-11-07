<!-- Verified by /verify-implementation on 2026-10-03 @ 6b208066e (BASE 7c4ba48cc, HEAD 77d71e05c + 2 remediation commits 076033523 regen 6b208066e): class D regression fixed at the NameCasing seam (`_` local → `__spy_underscore`, never `@_`); filed #2212 #2213; W1 inert EscapedIdentifier pattern hunks (EmitLineDirectives=false route untested), W2 checker SPY0132 arm spells "_" by hand — recorded, not fixed -->
<!-- Verified by /verify-plan on 2026-10-02 @ 7c4ba48cc -->
<!-- Verification result: PASS WITH CORRECTIONS (re-graded 2026-10-02 after the owner ruled D1 = (a); first pass was NEEDS REVISION) -->
<!-- Adequacy: CLASS (P40 parser contract + the dependent `_`-local designation class, both with a seam cure and a matrix; D1 ruled (a) — https://github.com/antonsynd/sharpy/issues/2166#issuecomment-5953997153) -->
<!-- Scope: ONE-GROUP · Generator: built-first -->
# P40 — Contextual-keyword escape totality (#2166)

## Context

`docs/language_specification/identifiers.md` § Literal Names: a backtick-escaped name tells the compiler not to transform it and is "equivalent to C#'s `@` prefix" — a keyword written `` `class` `` is an identifier. The parser honours that for hard keywords because the lexer always emits an escaped token as `TokenType.Identifier` (`src/Sharpy.Compiler/Lexer/Lexer.cs:820–830` [CORRECTED: path qualified — two files named Lexer.cs]). At the **contextual (soft) keyword** sites the parser recognises the keyword by `Token.Value` alone and never reads `Token.IsBacktickEscaped`, so `` `_` ``, `` `get` ``, `` `out` ``, `` `when` `` … are read as the keyword and the escape is silently lost.

Round charter: `.claude/plans/remediation-round-2026-09-20-batching.md` §3.31 (P40, first after chores). Class tracker: [#2166](https://github.com/antonsynd/sharpy/issues/2166) (`lane:silent-wrong`, `reach:probe`, `class-tracker`). Unblocks [#2169](https://github.com/antonsynd/sharpy/issues/2169) row 6 (P22d), which is "SPY0912 expected until #2166 lands".

## Current State (measured @ 7c4ba48cc, Debug `sharpyc` built 2026-10-02 00:36, probes in the session scratchpad `probes/`)

Every probe was run with `sharpyc run` (never `emit`). The bare spelling is the positive control in each row.

| Cell | Program (escaped) | Bare control prints | Escaped prints today | Spec says |
|---|---|---|---|---|
| `_` call positional arg (issue repro) | `_ = 3; g = f(1, `_`); print(g)` | `System.Func`2[…]` | `System.Func`2[System.Int32,System.Int32]` | `13` |
| `_` call keyword arg | `g = f(1, b=`_`)` | lambda | SPY0343 "cannot infer the type of parameter 'b' of this lambda" | `13` |
| `_` operator section | `h = (`_` + 1)` | lambda | `System.Func`2[…]` | `4` |
| `_` case pattern | `case `_`:` then `y = `_`` | `wild` (wildcard) | SPY0132 at the read (`_` bound nothing) | binds, prints `5` |
| `_` star capture | `case [1, *`_`]:` then `y = `_`` | wildcard | SPY0132 | binds, prints `[2, 3]` |
| `_` bare reference | `_ = 3; x = `_`; print(x)` | `3` | `3` | `3` (already right — no contextual site) |
| `_` f-string hole, lambda parameter | `print(f"{`_`}")`, `lambda `_`: `_` + 1` | `3` | `3` | right already |
| `_` for-target / tuple-target then `print(`_`)` | | | SPY0603 (the READ is the call-arg cell) | prints the bound value |
| **[CORRECTED: measured by the verifier @ 7c4ba48cc]** `_` bound by a NON-designation target, escaped read `x = `_``: `_ = 3` / `for _ in range(2)` / `` for `_` in range(2) `` / `def f(_: int)` | | `3` / `0 1` / `0 1` / `3` | same — right already | right already |
| **[CORRECTED: measured]** `_` bound by a DECONSTRUCTION target, escaped read: `a, _ = 1, 2` then `x = `_``; `print(x)` | | — (a bare read of `_` is SPY0132/SPY0603 by design) | **SPY0908 CS0103 "The name '_' does not exist"** — emitted `var (a, _) = (1, 2); var x = _;`: C# reads `_` in a designation as a discard | `2` — a pre-existing ICE at the base commit, reachable only by an escaped read (`lane:ice`, `reach:probe`); the same C# rule hits every designation the plan's Task 4 will start producing (pattern `case var _`, list-pattern `.. var _`) — see Decision D1 |
| `get`/`set`/`init` accessor | `property `get` name(self) -> int` | `7` | `7` (read as the accessor) | the property is named `get`; `name` is unexpected → parse error |
| `add`/`remove` accessor | `event `add` on_click(self, handler)` | compiles | compiles (read as the accessor) | parse error |
| `when` filter (with and without `as`) | `except ValueError as e `when` True:` | `filtered` | `filtered` | parse error |
| `out`/`ref` parameter modifier | `def f(a: `out` int)` | `True 5` | `True 5` | parse error (`out` is the type, `int` unexpected) |
| `out`/`ref` call-site modifier | `f(`out` v)` | `True 5` | `True 5` | parse error |
| `out` variance | `delegate D[`out` T]() -> T` | ok | ok | parse error |
| `notnull` constraint | `def f[T: `notnull`](x: T)` | `3` | `3` | type constraint naming `notnull` → SPY0202 "Type 'notnull' not found" (measured today for `[T: Nope]`) |
| `new()` constraint | `def f[T: `new`()](x: T)` | `3` | `3` | parse error |
| `before_set`/`after_set` observer (`--enable-feature property_observers`) | `` `before_set`(new_value): `` | `1` | `1` | the observer arm's own refusal ("Expected 'before_set' or 'after_set'") |
| every keyword, ordinary expression (`k = 1; print(`k`)`) | | `1` | `1` | right already |
| every keyword, escaped binding read bare (`` `k` = 2; print(k) ``) | | — | SPY0200 (escape-declared name, #1328) | by design; out of this contract |

**Proxy for the post-fix outcome of the eight "parse error" rows:** the escaped keyword at its site will be exactly an identifier, so the bare site with a fresh identifier `zz` in the keyword's place predicts it. Measured @ 7c4ba48cc: `property zz name(…)`, `event zz on_click(…)`, `except E as e zz True:`, `def f(a: zz int)`, `f(zz v)`, `delegate D[zz T]()`, `def f[T: zz()]` are all SPY0104 at the token after `zz`; `def f[T: zz]` parses (semantic SPY0202 later).

**Escape-blind sites (every one, enumerated by grep over `Parser/*.cs` for `Value == "`, `Value != "`, `case "…":` on Identifier tokens and `Name: "_"` on AST nodes):**

| # | File:line | Keyword | Mechanism |
|---|---|---|---|
| 1 | `Parser.Expressions.cs:1446, 1453, 1466, 1524, 1563` | `_` | `arg is Identifier { Name: "_" }` (call placeholder, positional + keyword) |
| 2 | `Parser.Primaries.cs:1109, 1147` | `_` | `Identifier { Name: "_" }` (operator section) |
| 3 | `Parser.Statements.cs:1855` | `_` | `TokenType.Identifier when Current.Value == "_"` → `ParseWildcardPattern` |
| 4 | `Parser.Statements.cs:2297` | `_` | `nameToken.Value == "_"` → `WildcardPattern` (star capture) |
| 5 | `Parser.Definitions.cs:1532–1540` | `get`/`set`/`init` | `switch (Current.Value)` |
| 6 | `Parser.Definitions.cs:1811–1815` | `add`/`remove` | `switch (Current.Value)` |
| 7 | `Parser.Definitions.cs:1763, 1770` | `before_set`/`after_set` | `Current.Value != "before_set" …` |
| 8 | `Parser.Statements.cs:771` | `when` | `Current.Value == "when"` |
| 9 | `Parser.Statements.cs:1460–1463` | `ref`/`out` | parameter modifier (the ternary at `:1463` is a second `Value == "ref"` read) |
| 10 | `Parser/Parser.cs:1412–1424` [CORRECTED: path qualified — nine files named Parser.cs] | `ref`/`out` | call-site modifier (the ternary at `:1423` is a second `Value == "ref"` read) |
| 11 | `Parser.Definitions.cs:965` | `out` | type-parameter variance |
| 12 | `Parser.Definitions.cs:1060` | `notnull` | constraint |
| 13 | `Parser.Definitions.cs:1067` | `new` | constraint |
| 14 | `Semantic/TypeChecker.Expressions.cs:573` | `_` | `id.Name == "_"` → SPY0132 for an unbound `_` (fires for an unbound `` `_` `` too — message lane) |

`match`/`case`/`type` are `TokenType`s (`Parser.Types.cs:658`), so their escapes are honoured by construction; `in` is `TokenType.In`. The charter called the group "Parser only"; code reading adds site 14 (one predicate arm in the checker) and a measured risk in CodeGen (below). `Pretty` already writes escaped names at every name position (`UnparserBacktickEscapeRoundTripTests`), including `BindingPattern.Name` and type names.

**What the standing harness does today:** `FormatterMeaningPreservationSweepTests` T2 (`FormatterTwins.BacktickInjected`) skips the ten values of `FormatterTwins.ContextualKeywordsReadAsKeywords` at their contextual position and the census guard at `FormatterMeaningPreservationSweepTests.cs:458` demands each still occurs in the corpus. `docs/tooling/formatter.md:118` documents the SPY0912 refusal of `` case `_`: `` as a known case.

**Correction to the charter's acceptance (code reading revised the recommendation).** §3.31 says "the skip set drains to EMPTY and the guard inverts to 'the set is empty'". That cannot happen: T2's instrument requires the escaped twin to **parse**, and once the escape is honoured, eight of the ten values at their site turn the twin into a different, unparseable program (the proxy rows above — `property `get` name` is a property named `get` followed by a stray token). Only `_` (becomes a binding/identifier) and `notnull` (becomes a type constraint) yield parseable twins. So after this plan: `_` and `notnull` **leave** the set and are escaped by T2 like any identifier; the other eight **stay**, with the rationale inverted from "the parser ignores the escape" to "the escape is honoured and changes the program, so the twin would not parse" — the set is renamed to say so and the census guard keeps its "corpus must contain one" shape. The class harness is therefore **not** T2; Phase 1 builds it.

No spy-sourced stdlib or spy-test source uses an escaped soft keyword (grep @ 7c4ba48cc: comment hits only), so no generated artifact moves.

## Scope

- **Group:** P40 — contextual-keyword escape totality (one non-chore group; chores riding along: none)
- **Alone:** yes, trivially (one group). [CORRECTED] Under Decision D1 (a) the emitted C# name of every local named `_` changes — an emitted-layout change — but the measured reach is 0 snapshot fixtures, 2 spy-sourced sources, 2 spec pages; under (a′) nothing that is correct today changes. Either way the group is the only one on the gate.
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger (`<scratchpad>/findings-ledger.md`); the lead files by class (contract §10); trackers named at the end of this plan
- **Override:** none

## Defect Class

- **Violated contract:** a backtick-escaped name is an identifier at EVERY site — the predicate "this token is the contextual keyword *k*" is `Type == Identifier ∧ ¬IsBacktickEscaped ∧ Value == k` at every contextual site, and "this node is the placeholder" is `Identifier { Name: "_", IsNameBacktickEscaped: false }`.
- **Meta-class:** "Position/route missing a shared check" (verification-contract.md §1) → **standard cure:** ONE shared predicate at every site + a keyword × site matrix so a new soft keyword cannot skip it. The symptom lane is silent-wrong (the issue's repro), so the fix lands first and the sweep follows in the same plan.
- **Known member cells:** #2166 (call placeholder, accessor, modifier, event accessor, `when`, `notnull`); #2166 comment 2026-10-01 (star capture).
- **Sibling cells this plan must also cover (the matrix):** keyword ∈ {`_`, `get`, `set`, `init`, `add`, `remove`, `before_set`, `after_set`, `when`, `out`, `ref`, `notnull`, `new`} (13) × site ∈ {each keyword's contextual site(s) — for `_`: call positional, call keyword, operator section, case pattern, star capture, class-pattern positional, class-pattern keyword, tuple-pattern element, guarded pattern; for `out`: variance, parameter modifier, call modifier; `ref`: parameter, call; `when`: with/without `as`; the rest one site each} × spelling ∈ {bare, escaped, renamed-to-fresh-identifier} plus position ∈ {ordinary expression, binding target} for every keyword. Site 14 (checker SPY0132 arm) is a cell of the `_` row.
- **Lane / reach:** silent-wrong · probe; **fidelity-scope row:** not a Python surface (backticks are Sharpy-only); python3 control for the record: `_ = 3; f(1, _)` → `13`, `_ + 1` → `4`.
- **Standing harness that should have caught it:** none at the parser level — T2 skipped exactly these cells by design. Phase 1 adds `ContextualKeywordEscapeSweepTests` (corpus differential, allowlist ratchet) and `ContextualKeywordEscapeMatrixTests` (templated site matrix). T2's skip set shrinks by `_` and `notnull` in Phase 2 so the formatter sweep covers those two from then on.
- **Generator:** REQUIRED FIRST — the matrix is ≥2 axes × ≥3 values. Phase 1 builds the corpus differential: for every identifier token at a contextual position in every corpus fixture, the **escaped twin** and the **renamed twin** (the keyword replaced by a fresh identifier of the same length) must parse to the same outcome — same parse-diagnostic (code, line, column) multiset, or both parse to the same node-type shape with the keyword construct absent. The axis a parse-level generator cannot enumerate is **execution** (the value an escaped `_` now binds to / passes), covered by `.spy` + `.expected` fixtures in Phase 3.
- **[ADDED: adequacy] Dependent contract the direction cells lean on — "a Sharpy local named `_` is a real local in the emitted C#":** today it holds for declaration positions and fails for designation positions. Matrix: binding position ∈ {assignment `_ = e` (works, m1), `for _ in` (works, m3/m4), parameter (works, m12), lambda parameter (plan: works), deconstruction `a, _ = …` (**ICE CS0103 measured** @ 7c4ba48cc), pattern binding `case `_`:` (unreachable today; reachable after Task 4a — predicted ICE by the same rule), star capture `*`_`` (same), comprehension target `[… for a, _ in …]` (designation at `RoslynEmitter.Expressions.Comprehensions.cs:763` — unmeasured, probe it), `out let _` / `with … as (a, _)` (probe)} × read ∈ {escaped read `x = `_`` (the only read spelling the language allows), no read (a discard is correct — must stay `_`-shaped or equivalent)}. Meta-class: **silent-wrong/ICE from a C# spelling rule applied at one arm** — the cure is ONE symbol-keyed spelling decision (`CodeGenInfoComputer.cs:295`) that every designation-emitting arm already reads, plus the position matrix above as the test; never a per-arm patch. Lane / reach: ice · probe (a bare read of `_` is refused by design, so only `` `_` `` reaches it). Churn measured @ 7c4ba48cc: 0 `.spy` fixtures with a `.expected.cs` snapshot bind `_` by deconstruction/star; 2 spy-sourced stdlib/spy-test sources and 2 spec pages do (`a, _ = …` / `*_`) — their emitted C# changes only if the chosen spelling renames unread `_` locals too (option (a) vs (a′) in D1). Owner ruling D1 = (a) (link in Design Decision 7): this class is INSIDE P40 as Task 4b, so the group is Parser + one Semantic materialization seam, not "Parser only".
- **Owner rulings:** charter §3.31 ("a backtick-escaped name is an identifier at EVERY site"; order N22; P40 before P22d), the batching comment on #2166 (2026-10-02, https://github.com/antonsynd/sharpy/issues/2166#issuecomment-5944918113), and D1 = (a) (2026-10-02, https://github.com/antonsynd/sharpy/issues/2166#issuecomment-5953997153). The T2 acceptance correction above is a measured revision of that comment's "drains to EMPTY" clause, not a new decision: the contract is unchanged, only the instrument that proves it.

## Adversarial Review (pre-mortem)

- **Alternative root cause:** the lexer could be dropping the flag (it is not — `Lexer/Lexer.cs:830` sets it, `LexerTests:1491–1525` guard it) or the AST could be losing it (it is not — every carrier copies `IsBacktickEscaped`, `ParserBacktickEscapeCarrierTests`). The discriminating probe is `_ = 3; x = `_`; print(x)` → `3` (no contextual site, works) against `g = f(1, `_`)` → `Func` (contextual site, lost): the token and node carry the flag; only the contextual predicates ignore it.
- **Alternative layer:** rung 1–3 do not apply — this is pure syntax; there is no Core type, dunder or CLR identity involved. The predicate stays in the parser (syntax facts on syntax nodes, as R-DL rules for P22d). Site 14 is an existing rung-4 name-keyed arm (`id.Name == "_"`) and only gains the escape conjunct; what the CLR surface cannot express is the Sharpy placeholder token itself.
- **How the fix could be inert:** (a) the parser stops lowering `` f(1, `_`) `` but the checker's SPY0132 arm refuses the escaped reference when `_` is unbound — the direction cell `case `_`:` + `y = `_`` proves the arm is reached only for unbound names (`x = `_`` after `_ = 3` prints `3` today), so the arm is a message cell, not an inertness risk; still, Phase 2 Task 6 adds the conjunct and the matrix asserts the generic undefined-name code for an unbound `` `_` ``. (b) One site patched, its twin left: sites 1 and 2 are two copies of the placeholder test, sites 9 and 10 two copies of the modifier test, sites 5/6/7 three copies of the accessor switch — Phase 2 routes every site through the two shared predicates and a scan guard (Phase 2 Task 7) fails on any raw spelling left in `Parser/*.cs`.
- **Before/after by direction (contract §3) — programs newly rejected, with the prior commit's behaviour:**

| Program | @ 7c4ba48cc | After | Classification |
|---|---|---|---|
| `property `get` name(self) -> int:` | worked as `property get name` (escape ignored) | SPY0104 at `name` | spec-mandated: the escape names the property `get`; charter §3.31 "EVERY site" |
| `event `add` e(self, h)` | worked as the accessor | SPY0104 | same |
| `except E as e `when` c:` / `except E `when` c:` | worked as the filter | SPY0104 | same |
| `def f(a: `out` int)`, `f(`out` v)`, `def f(a: `ref` int)`, `f(`ref` v)` | worked as the modifier | SPY0104 | same |
| `delegate D[`out` T]`, `interface I[`out` T]` | worked as variance | SPY0104 | same |
| `def f[T: `notnull`]` | worked as the constraint | SPY0202 "Type 'notnull' not found" | same |
| `def f[T: `new`()]` | worked as the constraint | SPY0104 | same |
| `` `before_set`(v): `` | worked as the observer | parser "Expected 'before_set' or 'after_set'" (`DiagnosticCodes.Parser.UnexpectedToken`) | same |

  Every row is "the escape was ignored" → "the escape is honoured"; none of these programs is one a user can have written meaning the keyword, because the bare spelling is the only spelling the spec gives the keyword. The direction cells that go from wrong/refused to working: `f(1, `_`)` → `13`; `f(1, b=`_`)` → `13`; `(`_` + 1)` → `4`; `for `_` in range(2): print(`_`)` → `0`,`1` (the binding already works today — only the read changes). **[CORRECTED: verifier measurement]** Three of the seven cells do NOT reach their "after" value through the parser fix alone: `case `_`:` → `5`, `case [1, *`_`]:` → `[2, 3]` and `a, `_` = 1, 2; print(`_`)` → `2` all bind `_` through a C# **designation** (`case var _`, `[.., .. var _]`, `var (a, _)`), and C# reads a designation named `_` as a discard — measured today on the deconstruction cell (`a, _ = 1, 2; x = `_`` → SPY0908 CS0103 @ 7c4ba48cc). With Task 4a alone, `case `_`:` and `case [1, *`_`]:` move from SPY0132 (a refusal) to SPY0908 (an ICE) — a regression in kind under contract §3 — so the `_`-local spelling (Task 4b, Decision D1) must land in the same commit or before.
- **Recorded ≠ applied in CodeGen — [CORRECTED: no longer a risk, a measured defect].** A `BindingPattern` named `_` emits `VarPattern(SingleVariableDesignation(Identifier("_")))` (`RoslynEmitter.Patterns.cs:332–333`); a deconstruction target `_` emits `SingleVariableDesignation(EscapedIdentifier("_"))` (`RoslynEmitter.Statements.Assignments.cs:460–471`); a star capture emits a slice designation (`Patterns.cs` list-pattern arm); comprehension targets `RoslynEmitter.Expressions.Comprehensions.cs:763`. The plan's analogy to `var _ = 3;` does not transfer: a *declaration* named `_` is a local, a *designation* named `_` is a discard — `SyntaxFactory.Identifier("_")` carries the `UnderscoreToken` contextual kind, and the binder reads the designation as a discard. Measured @ 7c4ba48cc: `a, _ = 1, 2` / `x = `_`` → `var (a, _) = (1, 2); var x = _;` → SPY0908 CS0103. The same C# rule reaches the pattern and star-capture designations Task 4a will start producing, so the cure is NOT "that one emitter arm": it is the C# spelling of a Sharpy local named `_` wherever a designation declares it — a symbol-keyed fact (`CodeGenInfoComputer.cs:295` is where a local's `CSharpName` is materialized from `NameCasing.ResolveVariable`; every emitter arm already reads it through `GetMangledVariableName`). Decision D1 below.
- **Blast radius:** `Sharpy.Compiler.Tests` (Parser, Conformance incl. the formatter sweeps, FileBased), `Sharpy.Lsp` `FrontEndParityTests` (shared parser), `Sharpy.Cli.Tests`, `Sharpy.Stdlib.Tests` (spy-test compilation — no escaped soft keyword in those sources, so no behaviour change expected, but the project runs in the gate), the three GapDiscovery sweeps (none has a backtick relation — `IsBacktickEscaped` is read only by the formatter twins and lexer/parser tests). Warm/cold: not touched.
- **Generated artifacts touched:** none (grep above). `check_spy_staleness.sh` runs at the end of Phase 2 anyway (CLAUDE.md: the staleness CHECK is the gate).

## Design Decisions

1. **One token predicate, one node predicate, one roster — in the parser (rung: syntax; no ladder rung applies).** New file `src/Sharpy.Compiler/Parser/Parser.ContextualKeywords.cs`:
   - `internal static class ContextualKeywords` with `public const string Placeholder = "_"`, `Get`, `Set`, `Init`, `Add`, `Remove`, `BeforeSet`, `AfterSet`, `When`, `Out`, `Ref`, `NotNull`, `New` and `public static readonly IReadOnlySet<string> All` built from those constants (13 members; the matrix test anchors the count to the literal `13`, never to `All.Count`).
   - `private static bool IsContextualKeyword(Token token, string keyword) => token.Type == TokenType.Identifier && !token.IsBacktickEscaped && token.Value == keyword;`
   - `private static bool IsPlaceholder(Expression? expr) => expr is Identifier { Name: ContextualKeywords.Placeholder, IsNameBacktickEscaped: false };`
   - Every site in the table above calls one of the two; no `Value == "…"` / `case "…":` / `Name: "_"` on an Identifier value survives outside this file (Phase 2 Task 7 guard).
   Why not an AST change (a `PlaceholderExpression` node, a `SoftKeyword` token type): the lexer cannot classify `_` without the parser's context, and `partial_application.md:3` records the parser-level desugaring as the design; the escape flag is already on both token and node, so the fix is a predicate, not a representation.
2. **The checker's SPY0132 arm gains the escape conjunct (rung 4, existing).** `TypeChecker.Expressions.cs:573` becomes `id.Name == "_" && !id.IsNameBacktickEscaped`; an unbound `` `_` `` falls through to the ordinary undefined-name diagnostic. Message lane; no new code; `DiagnosticExplanations` unchanged.
3. **T2's skip set is re-rationalised, not emptied.** `FormatterTwins.ContextualKeywordsReadAsKeywords` → `ContextualKeywordsAtTheirSite` = {`get`,`set`,`init`,`add`,`remove`,`when`,`out`,`ref`} with the doc comment "an escape here is honoured and changes the program (the twin would not parse), so T2 leaves it bare; `_` and `notnull` are escaped like any identifier since #2166"; `IsReadAsContextualKeyword` → `IsAtContextualSite` loses its `"_"` and `"notnull"` arms; the census guard keeps "the corpus must contain one". `before_set`/`after_set` stay excluded by VALUE (`ContextualBeforeSet`/`ContextualAfterSet`) as today — out of this plan's instrument scope, noted in the ledger as a T2 coverage gap (`lane:tooling`, `reach:probe`; a comment on #2166, no issue).
4. **The class harness is a corpus differential with an allowlist ratchet (contract §11), plus a templated site matrix for the shapes the corpus may lack.** Oracle: `Outcome(escapedTwin) == Outcome(renamedTwin)` where `Outcome` = the parse-diagnostic multiset `(code, line, column)` when either fails, else the preorder node-type sequence of the AST (names and flags excluded) — the renamed twin is the ground truth for "this token is an identifier here". The matrix test additionally names the node kinds (bare → `WildcardPattern`/`LambdaExpression`/`PropertyAccessor.Get`/…; escaped → `BindingPattern`/`Identifier{escaped}`/parse error) so a wrong-but-consistent pair cannot pass.
5. **Spec deliverables carry equal weight.** `keywords.md` § Soft Keywords gains the nine missing rows and the sentence "Backtick-escaping a soft keyword makes it an ordinary identifier at every site"; `identifiers.md` § Literal Names gains an executed example; `partial_application.md`'s disambiguation table gains the escaped row; `generics.md`'s constraint table gains `T: notnull` and `T: new()` (implemented, unspecified today — documented as existing behaviour, flagged in the report); `docs/tooling/formatter.md:118` loses its #2166 bullet. Every new fenced block is executed (`build_tools/check_spec_blocks.sh`).
6. **Fixture expectations that change are the twin tests' census, not language semantics.** `FormatterTwinsTests.BacktickInjected_LeavesTheContextualKeywordsOf2166Bare_OnlyAtTheirPosition` (expects `case _:` bare, count 9, skips `{_: 1}`) and the `_`/`notnull` rows of `BacktickInjected_LeavesAContextualKeywordBare_AtItsPosition` encode the set's membership; they move to the "escaped like any other identifier" test in the same commit as the set change, with the rationale in the commit body. No `.expected`/`.error` fixture changes (`errors/placeholder_outside_call` is a bare-`_` cell and stays).
7. **[ADDED: verifier — DECISION D1 — RULED (a) by the owner 2026-10-02, https://github.com/antonsynd/sharpy/issues/2166#issuecomment-5953997153; owner's note: (b) was also reasonable, since a *readable* `_` binding is primarily a Python REPL idiom — recorded, not re-asked] The C# spelling of a Sharpy local named `_` bound by a designation.** Repro @ 7c4ba48cc: `def main(): a, _ = 1, 2; x = `_`; print(x)` → SPY0908 CS0103 (emitted `var (a, _) = (1, 2); var x = _;`); after Task 4a alone, `case `_`: y = `_`` and `case [1, *`_`]: y = `_`` take the same route (SPY0132 → SPY0908, a refusal becoming an ICE). Options:
   - **(a) — RECOMMENDED — one symbol-keyed spelling for every `_` local, in this plan (Task 4b, before 4a):** `CodeGenInfoComputer.cs:295` materializes a local whose Sharpy name is `_` under a C# name that is not a discard in any position (the implementer measures the candidates by `run` on the deconstruction cell: a verbatim-kind token `@_`, else a reserved mangle such as `__`), and every designation arm keeps reading `GetMangledVariableName`. Door: two-way (a spelling is internal; the snapshot shows it). Stacking: churn is 0 snapshot fixtures, 2 stdlib/spy-test sources, 2 spec pages (measured) — regen right after 4b (§7) and read the diff. Cost: Semantic+CodeGen enter the blast radius (`Sharpy.Stdlib.Tests`, snapshots, the three GapDiscovery sweeps) — they are in the gate already. Delivers all seven direction cells and keeps the charter's spec outcome (`case `_`:` binds).
   - **(a′) — same seam, narrower:** the non-discard spelling only for a `_` local that is **read** somewhere (an unread `_` keeps the discard spelling, so no existing output changes). Needs a "referenced" fact at materialization time (`UnusedVariableValidator` computes it later in the pipeline — check ordering before choosing). Door: two-way. Cost: a usage-dependent spelling is a second rule to document; zero regen churn.
   - **(b) — carve out:** keep P40 parser-only; Task 4a adds a semantic refusal "a binding named `_` in pattern/deconstruction position is not supported (#NEW)" so the cells stay refusals, not ICEs; the lead files ONE class issue (`lane:ice`, `reach:probe`, the matrix above) for a later group. Door: one-way for this round (the spec example `case `_`:` → `5` cannot ship; `keywords.md`'s new sentence must say "except in designation positions until #NEW"); the direction table shrinks to four printing cells and the #2166 close criterion changes. Rejected unless the owner wants P40 strictly parser-only.
   Ruled (a). Task 4a never lands without 4b: the by-direction rule (§3) forbids SPY0132 → SPY0908. Spec consequence for Task 11: `keywords.md`'s new sentence and `partial_application.md`'s table row state that an escaped `_` is an ordinary readable local in every position, including pattern and deconstruction bindings; no carve-out text.

## Implementation

### Phase 1: Generator first — the roster and the two harnesses (red)

**Goal:** the matrix exists as executable code before any site changes; its red count is the measured population of the class.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes red now: `ContextualKeywordEscapeSweepTests` fails on every (fixture, token) cell where the escaped twin's outcome differs from the renamed twin's — expected ≈ the issue's census (250/124/36/14/44/12/26/24/7/1 fixtures per token @ 011fa4068; re-counted @ this phase) — and `ContextualKeywordEscapeMatrixTests` fails on every escaped cell of the 13 rows.
- Guard mutation: (a) with the allowlist emptied the sweep is red (count recorded); (b) replacing the renamed twin with the escaped twin itself (comparing a twin to itself) turns the sweep green — recorded as the vacuity control, then restored; (c) deleting one row of the matrix table for a roster member turns the totality check red.
- Allowlist entries: `src/Sharpy.Compiler.Tests/Conformance/contextual-keyword-escape-allowlist.txt` populated with one row per red cell (`stem token # #2166 escape read as the keyword`), every row citing #2166.
- Execution evidence: the harness output line `CKE-CENSUS tokens=<n> cells=<n> red=<n> allowlisted=<n>` @ sha.

#### Tasks

1. **Roster and predicates (unused yet)** — `src/Sharpy.Compiler/Parser/Parser.ContextualKeywords.cs`
   - Add `ContextualKeywords` (13 constants + `All`), `IsContextualKeyword(Token, string)`, `IsPlaceholder(Expression?)` as in Design Decision 1. No site changes in this task.
   - Acceptance: builds; `ContextualKeywords.All.Count == 13` asserted by the matrix test against the literal.
   - Commit: `feat(parser): ContextualKeywords roster and the escape-aware IsContextualKeyword/IsPlaceholder predicates (refs #2166)`

2. **Site matrix harness** — `src/Sharpy.Compiler.Tests/Parser/ContextualKeywordEscapeMatrixTests.cs`
   - A static table `Rows` of `(Id, Keyword, Template, BareConstruct, EscapedOutcome)` with `{K}` as the hole; rows: U1–U9 for `_` (call positional, call keyword, operator section, case pattern, star capture, class-pattern positional `case P({K}, y)`, class-pattern keyword `case P(x={K})`, tuple element `case ({K}, z)`, guarded `case {K} if c`), G1–G3 (`property {K} p(self) -> int`), A1–A2 (`event {K} e(self, h: H)`), O1–O2 (`property health: int = 0` + `{K}(v):` observer; parse-level only — the gate is semantic, the parser always parses the clause per `Parser.Definitions.cs:1757`), W1–W2 (`except E as e {K} c:` / `except E {K} c:`), M1–M4 (`def f(x: {K} int)`, `f({K} y)` for `out` and `ref`), V1–V2 (`interface I[{K} T]:`, `delegate D[{K} T]() -> T`), C1 (`def f[T: {K}](x: T) -> T`), C2 (`def f[T: {K}()](x: T) -> T`); plus X1/X2 for every keyword (`{K} = 1` then `` print(`{K}`) ``; `` `{K}` = 2 `` then `print({K})`) asserting both spellings parse to `Identifier` nodes with the right flag (X2's semantic SPY0200 is not this test's concern — parse level).
   - Per row, three parses: bare (positive control: the `BareConstruct` node/enum value is present — `WildcardPattern`, a `LambdaExpression` whose first parameter starts with `__placeholder_`, `PropertyDef.Accessor == Get`, `EventDef.Accessor`, `PropertyObserver.Kind`, `ExceptHandler.Filter != null`, `Parameter.Modifier`, `ModifiedArgument`, `TypeParameter.Variance`, `NotnullConstraint`, `NewConstraint`), escaped (`EscapedOutcome`: either `IdentifierBound` — the construct is absent and a name-bearing node `{Name: K, IsNameBacktickEscaped: true}` exists — or `ParseError`), renamed (`zz` in the hole; its outcome must equal the escaped twin's by the Design Decision 4 oracle — shared with the sweep through a `ContextualKeywordEscapeOracle` helper in `Conformance/`).
   - Totality: `ContextualKeywords.All` equals the literal set of 13 names, and every name has ≥ 1 row with a contextual site (X rows do not count).
   - Acceptance: red on every escaped cell @ Phase 1; the three mutations in the phase acceptance recorded in the commit body.
   - Commit: `test(parser): ContextualKeywordEscapeMatrixTests — keyword × site × {bare, escaped, renamed} with node-kind oracles and a literal-anchored roster totality (refs #2166)`

3. **Corpus differential sweep with allowlist** — `src/Sharpy.Compiler.Tests/Conformance/ContextualKeywordEscapeSweepTests.cs`, `ContextualKeywordEscapeOracle.cs`, `contextual-keyword-escape-allowlist.txt`
   - Corpus: the same single-file corpus `FormatterMeaningPreservationSweepTests.CorpusCensus` enumerates (reuse it). For each fixture, for each identifier token at a contextual site — `FormatterTwins.IsReadAsContextualKeyword` today (its generous position arms are exactly the enumeration we want; after Phase 2 renames it, the sweep calls the renamed predicate for the eight staying values and adds `_`/`notnull` positions back locally so the sweep keeps enumerating all 13) — build the escaped twin (insert backticks at that one token) and the renamed twin (replace the token text with `z` × length, same offsets) and compare `Outcome`s. One cell per (stem, token value); a fixture with three `_` tokens is three twins and one cell (first failing token reported).
   - Allowlist format `stem token # #2166 <reason>`; a non-allowlisted red fails; a green allowlisted row fails ("drain on fix"); rows citing closed issues are caught by `build_tools/check_allowlist_issue_state.sh` as for every other allowlist.
   - Register the harness in `docs/design/gap-discovery-contracts.md` § "Classes, contracts, harnesses" [CORRECTED: section name] (contract: "an escaped soft keyword parses as a fresh identifier would"; harness: this sweep + the matrix; ratchet: the allowlist). The file name `contextual-keyword-escape-allowlist.txt` matches the `*allowlist*.txt` glob `build_tools/allowlist_issue_state.py:194` scans over tracked files, so the issue-state gate picks it up once it is `git add`ed — no registration step.
   - Acceptance: red count ≈ 538 cells (re-measured @ sha, printed as `CKE-CENSUS`), allowlist populated, the self-comparison vacuity control recorded.
   - Commit: `test(conformance): ContextualKeywordEscapeSweepTests — escaped twin ≡ renamed twin over the fixture corpus, allowlist ratchet populated (refs #2166)`

### Phase 2: The fix — every site through the predicates, the allowlist drains

**Goal:** all 14 sites honour the escape; the sweep's allowlist is empty; T2 escapes `_` and `notnull`.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `ContextualKeywordEscapeMatrixTests` all rows; `ContextualKeywordEscapeSweepTests` with `contextual-keyword-escape-allowlist.txt` at 0 rows; `FormatterMeaningPreservationSweepTests` with its allowlist still at 0 rows and the census line showing eight skipped values, each > 0.
- Guard mutation: reverting Task 4's hunk alone (the `_` sites) reddens U1–U9 and the sweep's `_` rows; reverting Task 5's accessor hunk reddens G1–G3/A1–A2/O1–O2; dropping the `!IsBacktickEscaped` conjunct from `IsContextualKeyword` reddens every non-`_` escaped cell; re-adding `_` to the T2 set reddens `FormatterTwinsTests.BacktickInjected_EscapesTheSameSpellingsAsPlainIdentifiers`.
- Allowlist entries that drain: every row of `contextual-keyword-escape-allowlist.txt` (file ends empty, kept with its header).
- Execution evidence: the seven direction cells run @ sha with the outputs in the Adversarial Review table; the eight refusal rows measured by `run` with their codes; `check_spy_staleness.sh` green.

#### Tasks

4b. **[CORRECTED: split out of Task 4 — lands FIRST, per Decision D1 (a)/(a′)] A Sharpy local named `_` binds in C# at every designation position** — `Semantic/CodeGenInfoComputer.cs:295` (the symbol-keyed `CSharpName`); no emitter arm changes unless one bypasses `GetMangledVariableName` (grep `Identifier("_")`/`EscapedIdentifier(` in `CodeGen/` — `RoslynEmitter.Expressions.Comprehensions.cs:763`, `Statements.Assignments.cs:460–471`, `RoslynEmitter.Patterns.cs:333`).
   - Test: a `[Theory]` over the designation-position matrix of the Defect Class (deconstruction, pattern binding, star capture, comprehension target, `out let`, `with … as (a, _)`) × {escaped read prints the value, no read still compiles}; the deconstruction row is red today (`CompileAndExecute` → SPY0908) and is the inert-fix control; `for _`/`_ = e`/parameter rows are the positive controls that already pass.
   - Regen immediately after (contract §7): `build_tools/regenerate_spy_stdlib.sh` + spy-test regen + `check_spy_staleness.sh`; the 2 matching stdlib/spy-test sources either show no diff (a′) or a spelling-only diff (a) that is read, not waved through. Snapshot fixtures: 0 expected to move (measured) — any that does is a finding.
   - Mutation: revert the materialization hunk → the deconstruction row red (CS0103), restore → green; recorded in the commit body.
   - Commit: `fix(semantic): a local named `_` is materialized under a C# name that binds in designation positions — deconstruction, pattern and star-capture reads no longer CS0103 (refs #2166)`

4. **`_` sites (1–4) through `IsPlaceholder`/`IsContextualKeyword`** — `Parser.Expressions.cs`, `Parser.Primaries.cs`, `Parser.Statements.cs:1855, 2297` [CORRECTED: depends on 4b — never lands alone]
   - Replace every `Identifier { Name: "_" }` pattern with `IsPlaceholder(arg)` (keep the `placeholder` binding via `arg is Identifier placeholder && IsPlaceholder(placeholder)`); `case TokenType.Identifier when IsContextualKeyword(Current, ContextualKeywords.Placeholder)`; star capture `if (IsContextualKeyword(nameToken, ContextualKeywords.Placeholder))`.
   - Run the direction cells `p01`, `p02`, `p04`, `p05`, `p17a`, `p17b`, `p17f` (re-create them from the Current State table inside `def main():` — module-level statements are SPY0340) with `sharpyc run` — `13`, `4`, `5`, `[2, 3]`, `0`/`1`, `2`, `13`. [CORRECTED] `p04`/`p05`/`p17b` WILL be SPY0908 CS0103 without 4b (measured mechanism: a designation named `_` is a C# discard); the cure is Task 4b's symbol-keyed spelling, not an edit at `RoslynEmitter.Patterns.cs:332`.
   - Acceptance: U1–U9 green; sweep `_` rows drain; direction outputs recorded in the commit body; the Phase 1 tree + 4b alone leaves `case `_`:` at SPY0132 (4b is inert on parser behaviour — say so).
   - Commit: `fix(parser): an escaped `_` is an identifier at every placeholder and pattern site — call args, operator sections, case and star captures (refs #2166)`

5. **Token-level sites (5–13) through `IsContextualKeyword`** — `Parser.Definitions.cs` (accessors, observers, variance, `notnull`, `new`), `Parser.Statements.cs:771, 1460`, `Parser/Parser.cs:1412`
   - Rewrite each `switch (Current.Value)` as `if (IsContextualKeyword(Current, ContextualKeywords.Get)) … else if (…Set) …`; the modifier sites as `IsContextualKeyword(Current, ContextualKeywords.Ref) || IsContextualKeyword(Current, ContextualKeywords.Out)` with the existing peek guards unchanged; the observer check as `!IsContextualKeyword(Current, BeforeSet) && !IsContextualKeyword(Current, AfterSet)`.
   - Measure the eight refusal rows by `run` (codes and positions) and the `notnull` row (SPY0202).
   - Acceptance: G/A/O/W/M/V/C rows green; sweep rows for those tokens drain; the refusal table recorded in the commit body with the prior-commit column.
   - Commit: `fix(parser): escaped get/set/init, add/remove, before_set/after_set, when, out/ref, notnull and new are identifiers, never the contextual keyword (refs #2166)`

6. **Checker arm (site 14)** — `Semantic/TypeChecker.Expressions.cs:573`
   - `if (id.Name == "_" && !id.IsNameBacktickEscaped)`; an unbound `` `_` `` reports the ordinary undefined-name diagnostic.
   - Add the cell to the matrix test as a semantic row (`CompileAndExecute`-level, in `Sharpy.Compiler.Tests/Semantic/` — [CORRECTED] there are no existing SPY0132 unit tests in `.cs` (grep `PlaceholderOutsideCallOrOperator|SPY0132` over the test project hits nothing); the only control is the fixture, so put the row in a new `PlaceholderDiagnosticTests` class): `y = `_`` with `_` unbound → the generic code (SPY0200), not SPY0132; positive control: bare `y = _` → SPY0132 (the existing `errors/placeholder_outside_call` fixture is that control). Mutation: drop the `!id.IsNameBacktickEscaped` conjunct → the escaped row reports SPY0132 → red.
   - Commit: `fix(semantic): the SPY0132 bare-placeholder refusal reads only an unescaped `_`; an unbound escaped `_` is an ordinary undefined name (refs #2166)`

7. **Raw-spelling scan guard** — `src/Sharpy.Compiler.Tests/Parser/ContextualKeywordPredicateConformanceTests.cs`
   - For each roster word, no `"<word>"` string literal appears in `src/Sharpy.Compiler/Parser/*.cs` outside `Parser.ContextualKeywords.cs` (the diagnostic messages use single quotes). Positive control: the scan run over a synthetic source containing `Current.Value == "when"` reports it. Mutation: re-introduce `Current.Value == "when"` at site 8 → red; restore → green.
   - [CORRECTED: the scan as written is red on day one for reasons that are not predicates] (i) `Parser/AstDumper.cs:526` spells `"before_set"`/`"after_set"` to print an observer — rewrite it to `ContextualKeywords.BeforeSet/AfterSet` (an honest green, not an exemption); (ii) the XML doc comments at `Parser.Primaries.cs:1104, 1117, 1140` and `Parser.Expressions.cs:1436` contain `Identifier("_")` in prose — the scan strips `//`/`///` comment text before matching (and the positive control proves it still sees code); (iii) the ternaries `Current.Value == "ref" ? … : …` at `Parser/Parser.cs:1423` and `Parser.Statements.cs:1463` are second reads of the same token and must also go through the predicate — the Task 5 rewrite covers them. The scan must not exempt `AstDumper.cs` (the exemption would be the subject, §2).
   - Commit: `test(parser): no contextual-keyword spelling outside ContextualKeywords — scan guard with a positive control (refs #2166)`

8. **T2 skip set re-rationalised; twin tests follow the set** — `Conformance/FormatterTwins.cs`, `FormatterTwinsTests.cs`, `FormatterMeaningPreservationSweepTests.cs` (census label only), `docs/tooling/formatter.md`
   - Rename per Design Decision 3; remove the `_` and `notnull` arms; update the three twin tests (the `case _:` fixture now escapes to `` case `_`: `` with count 10 and no skips; the `_` and `notnull` `InlineData` rows move to the "escaped like any other identifier" theory; the eight remaining rows keep "stays bare at its site" and gain the assertion that the escaped twin would NOT parse — the positive reason for the skip). Census label `skipped-contextual-keywords(#2166)` → `skipped-contextual-keywords(at-site)`.
   - Run the formatter sweep: T2 now escapes `_` in ~250 fixtures and `notnull` in 1; any `EscapeDropped`/`AstChanged` row is inside this plan's contract (the unparser writes `BindingPattern.Name` through `WriteName` and type names through the type writer — a drop here is a one-line fix in `Pretty/`, not an allowlist row).
   - Delete the `#2166` bullet at `docs/tooling/formatter.md:118`.
   - Acceptance: both formatter allowlists stay at 0; the census line shows exactly eight skipped values each > 0; mutation "re-add `_`" → red as above.
   - Commit: `test(conformance): T2 escapes `_` and `notnull` like any identifier; the eight soft keywords that stay bare at their site do so because the honoured escape would not parse (refs #2166)`

9. **Staleness check** — run `build_tools/check_spy_staleness.sh` (expected green; no escaped soft keyword in spy sources). No commit unless it is red, in which case the regenerated C# is a finding, not a silent regen (contract §7).

### Phase 3: Spec, docs and executing fixtures (the axis the generator cannot enumerate)

**Goal:** the spec states the rule with executed examples; the direction cells are file-based fixtures.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: `FileBasedIntegrationTests` over the new fixtures; `build_tools/check_spec_blocks.sh` over the new blocks.
- Guard mutation: each `.expected` fixture is red against the Phase 1 tree (run once via the worktree, recorded); each `.error` fixture's substring is absent at the base commit (the escaped program compiled there).
- Allowlist entries that drain: none.
- Execution evidence: fixture outputs @ sha; spec-block gate output @ sha.

#### Tasks

10. **Fixtures** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
    - `escaped_placeholder_is_identifier_2166.spy/.expected` (call positional `13`, call keyword `13`, operator section `4`, f-string hole `3`), `escaped_wildcard_binds_2166.spy/.expected` (`case `_`:` → `5`, star capture → `[2, 3]`, tuple element, class-pattern positional), `escaped_underscore_targets_2166.spy/.expected` (for-target, tuple-target, lambda parameter), `errors/escaped_accessor_is_a_name_2166.spy/.error` (`property `get` name` → SPY0104 `@line:col`), `errors/escaped_when_is_a_name_2166.spy/.error`, `errors/escaped_notnull_is_a_type_2166.spy/.error` (SPY0202 'notnull'). Scaffold with `/add-test-fixture`.
    - Commit: `test(fixtures): escaped soft keywords bind and refuse by direction — `_` placeholder/wildcard/targets print, accessor/when/notnull escapes are names (refs #2166)`

11. **Spec and docs** — `docs/language_specification/keywords.md`, `identifiers.md`, `partial_application.md`, `generics.md`
    - `keywords.md` § Soft Keywords: add rows `add`/`remove` (events), `before_set`/`after_set` (property observers, experimental), `when` (exception filter), `out`/`ref` (parameter and argument modifiers), `out` (variance), `notnull`/`new()` (constraints); add "Backtick-escaping a soft keyword makes it an ordinary identifier at every one of these sites: `` f(1, `_`) `` passes the variable `_`; `` case `_`: `` binds `_`" with an executed block.
    - `identifiers.md` § Literal Names: one executed block — `_ = 3` / `` g = f(1, `_`) `` / `print(g)` → `13`.
    - `partial_application.md` table: row "Backtick-escaped `` `_` `` | Regular identifier | `` f(1, `_`) `` passes the variable `_`".
    - `generics.md` constraint table: `T: notnull` → `where T : notnull`, `T: new()` → `where T : new()` (implemented today; the report flags that they were unspecified).
    - Every new block runs under `check_spec_blocks.sh` (error blocks carry the marker).
    - Commit: `docs(spec): soft-keyword roster completed; a backtick-escaped soft keyword is an identifier at every site, with executed examples (refs #2166)`

### Phase 4: Gate and close-out

12. **Commit gate** — `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` over the whole solution, run in the background, output to a private log (not `-latest.log`); `Sharpy.Compiler.Tests` as its own background call (> 1 h; `--logger "console;verbosity=normal"`). Counts reported `passed/failed/skipped @ sha (measured)`; any red gets a control run at 7c4ba48cc before attribution.
13. **Close-out evidence** (lead): the P40 census line, the two formatter census lines, the direction-cell outputs, the refusal table, the spec-block gate; post on #2169 that row 6 now formats (`` f(1, `_`) `` round-trips with the escape) with the sha; the ledger's cells filed by class.

## Testing Strategy

- **Generator first (contract §11):** `ContextualKeywordEscapeSweepTests` (Phase 1 Task 3) over corpus × contextual positions with the escaped-vs-renamed oracle and `contextual-keyword-escape-allowlist.txt` as the ratchet; `ContextualKeywordEscapeMatrixTests` (Task 2) names node kinds per site. The axis the generator cannot enumerate is execution — the value an escaped `_` binds/passes — covered by the Phase 3 `.expected` fixtures; the renamed-twin oracle is itself the positive control for every parse-error outcome.
- **New fixtures:** listed in Task 10; for each `.error` fixture the positive control is the bare spelling in the existing corpus (`property get`, `except … when`, `[T: notnull]` fixtures already execute).
- **Edge cases / sibling axes probed in planning:** `_` in f-string holes, lambda parameters, for/tuple targets (identifiers already — no site); `new` and `before_set`/`after_set` (not in the issue; found by the `Value ==` grep); class-pattern and tuple-pattern `_` (same `ParsePattern` arm — one row each so the arm cannot grow a second entry); `in` and `match`/`case`/`type` are token types (control that the hard-keyword path is right). If every escaped spelling in a family agrees, the implementer changes axis to position before adding a spelling.
- **Outputs that discriminate:** `13` vs `System.Func…`, `4`, `5`, `[2, 3]` — each prints differently with the bug present; `print(x)` is never used as a type probe.
- **Spec:** Task 11's executed blocks are a deliverable of equal weight; `check_spec_blocks.sh` at FINAL (#2140).
- **Blast radius and regen timing:** Phase 2 ends with `check_spy_staleness.sh` (expected no-op); the gate covers `Sharpy.Stdlib.Tests`, `Sharpy.Cli.Tests`, `FrontEndParityTests` and the three GapDiscovery sweeps.
- **Direction verification:** every refusal added is listed in the Adversarial Review table with the prior commit's behaviour and verified with `run` against 7c4ba48cc (a worktree, never `git checkout` in the shared tree).

## Issues to Close

- #2166 — parser: a backtick-escaped soft/contextual keyword is still read as the keyword — closed by Phase 2 Tasks 4–8 (verified at `/verify-implementation`); **close criterion:** `contextual-keyword-escape-allowlist.txt` at 0 rows with the sweep green @ sha; all 13 roster members have a green escaped cell in the matrix; the seven direction cells print the spec values under `run` @ sha; the eight refusal rows measured with codes; the formatter census shows eight skipped values and both formatter allowlists at 0.
- #2169 — NOT closed here; row 6 drains: a comment with the sha at which `sharpyc format` accepts `_ = 3; f(1, `_`)` and preserves the escape (P22d keeps rows 1–5).

## Class trackers this plan may post cells to

- #2166 — contextual-keyword escape totality — cells of this matrix are the plan's work (no issue); the T2 value-level exclusion of `before_set`/`after_set` is posted here as a coverage note (`lane:tooling`, `reach:probe`).
- #2169 — unparser spellings that change the program — any `EscapeDropped`/`AstChanged` the widened T2 finds that is NOT an escape on a name position this plan touches (e.g. a placeholder lambda written back as `lambda __placeholder_0`) goes here; an escape drop on `BindingPattern.Name` or a constraint type name is inside this plan and is fixed in Phase 2 Task 8.
- #2168 — LSP formatting route parity — if the LSP range/on-type routes behave differently on an escaped soft keyword (P22e).
- [CORRECTED: measured, no longer hypothetical] The `_`-local designation spelling class (deconstruction ICE measured @ 7c4ba48cc; pattern/star predicted by the same rule) is INSIDE this plan under Decision D1 (a)/(a′) — Task 4b, no issue. Under (b) only, the lead files ONE class issue (`lane:ice`, `reach:probe`, the Defect Class matrix as its cell table) and the comprehension/`out let`/`with … as` cells the implementer probes join it as rows. No implementer runs `gh issue create`.

## Verification Summary

**Result:** PASS WITH CORRECTIONS (first pass NEEDS REVISION; re-graded the same day after the owner ruled D1 = (a))
**Adequacy:** CLASS — the P40 parser contract (14-site matrix, shared-predicate cure, generator-first harness, all re-derived by the verifier) plus the dependent `_`-local designation class, now inside the plan as Task 4b with its own seam and binding-position matrix. D1 ruled (a): https://github.com/antonsynd/sharpy/issues/2166#issuecomment-5953997153 (owner's note: (b) was also reasonable — a readable `_` is mostly a REPL idiom).
**Verified on:** 2026-10-02 @ 7c4ba48cc
**Plan file:** .claude/plans/plan-be89c1.md

### Corrections Made
- **Direction table / Adversarial Review:** "a plain `var _ = 3;` compiles, so the binding is expected to declare a real local" → refuted by `run`: `def main(): a, _ = 1, 2; x = `_`; print(x)` → SPY0908 CS0103 @ 7c4ba48cc, emitted `var (a, _) = (1, 2); var x = _;` (`emit csharp`). A designation named `_` is a C# discard whatever the emitter's token spelling. `case `_`:` and `case [1, *`_`]:` take the same route after Task 4a, turning SPY0132 into SPY0908 — a refusal → ICE regression under §3. Rewritten as a measured defect; Task 4 split into 4b (seam, first) + 4a (parser).
- **Defect Class:** added the dependent contract and its binding-position × read matrix `[ADDED: adequacy]`, with the measured churn (0 snapshot fixtures, 2 stdlib/spy-test sources, 2 spec pages).
- **Design Decisions:** added D1 with options (a) one symbol-keyed spelling at `CodeGenInfoComputer.cs:295` (recommended), (a′) the same only for read `_` locals, (b) carve out with a refusal; each with repro, door and stacking.
- **Current State row "for-target / tuple-target":** split into the measured non-designation rows (`_ = 3`, `for _`, `` for `_` ``, parameter — all print) and the deconstruction row (ICE).
- **Task 6:** "next to the existing SPY0132 tests" → there are none in `.cs`; new class, positive control is the fixture; mutation named.
- **Task 7 scan guard:** would be red on `AstDumper.cs:526` and on four doc-comment lines; corrected to rewrite the dumper through the roster and strip comments, never exempt the file; the two `Value == "ref"` ternaries named as sites the rewrite must cover.
- **Task 3:** gap-discovery section is "Classes, contracts, harnesses"; allowlist auto-discovered by the `*allowlist*.txt` glob.
- **Scope › Alone:** "no" → "yes, trivially"; the emitted-layout change under D1 (a) is quantified.
- **Class trackers:** the `_`-spelling bullet is now measured and placed (inside under (a)/(a′), one class issue under (b)).
- **References:** the lexer escape site → `Lexer/Lexer.cs:820–830`; the call-site modifier → `Parser/Parser.cs:1412–1424` (both ambiguous across files); direction probes must sit inside `def main():` (SPY0340 at module level).

### Adequacy
- **Contract:** named — "the predicate *this token is the contextual keyword k* is `Identifier ∧ ¬escaped ∧ Value == k` at every contextual site". Verifier re-derived the site list by grep (`Value ==|!=|is "`, `case "…":`, `Name: "_"`) over `Parser/*.cs`, `Semantic/`, `CodeGen/`, `Pretty/`, `Lsp/`: exactly the plan's 14 sites plus the two ternary re-reads (`Parser/Parser.cs:1423`, `Parser.Statements.cs:1463`) and the dumper spelling (`AstDumper.cs:526`), now named. No LSP handler keys on a soft-keyword string.
- **Matrix:** larger than the repro lists — 13 keywords × sites × {bare, escaped, renamed} × {expression, binding target}; the verifier found no parser cell outside it. The cell the plan missed is on the dependent class (designation positions), now drafted.
- **Cure:** seam — one token predicate + one node predicate + roster (meta-class "position/route missing a shared check"). The dependent class's cure is also a seam (symbol-keyed `CSharpName`), not the per-arm patch the plan had.
- **Harness:** `ContextualKeywordEscapeSweepTests` (corpus differential, escaped twin ≡ renamed twin) + `ContextualKeywordEscapeMatrixTests`, Phase 1; allowlist drains to 0 in Phase 2. T2's skip set correction (8 stay, `_`/`notnull` leave) is sound: the proxy rows (`property zz name(…)` etc. → SPY0104) were re-read against the parser arms and hold.
- **Layer:** every decision rung-justified (pure syntax; site 14 is an existing rung-4 arm gaining a conjunct — the dunder table/CLR surface cannot express the placeholder token). D1 (a) is symbol-keyed at the `MaterializeCodeGenInfo` boundary per Rule 2.

### Scope & convergence
- **Group:** one (P40) · **Alone:** yes — required? under D1 (a) yes (emitted layout), satisfied · **Gates:** 1
- **Generator:** built first in Phase 1 (corpus differential + allowlist ratchet); hand matrix covers execution (the generator is parse-level) — stated.
- **Trackers named:** #2166, #2169, #2168 · **Fidelity rows:** not a Python surface (backticks are Sharpy-only); no DEVIATION surface planned as a bug.

### Falsifiability
- `ContextualKeywordEscapeMatrixTests`: mutation [named: delete a roster row → totality red; revert Task 4/5 hunks → named rows red]; exemption [not subject]; positive control [bare spelling asserts the construct node — yes]; by-direction [the Adversarial Review table, verified by `run` at 7c4ba48cc — three rows corrected].
- `ContextualKeywordEscapeSweepTests`: mutation [named: allowlist emptied → red; self-comparison → green vacuity control]; exemption [the allowlist is the ratchet, not the predicate]; positive control [renamed twin is the ground truth — yes].
- Task 4b designation `[Theory]`: mutation [named by verifier: revert the materialization hunk → deconstruction row CS0103]; positive control [`for _`/`_ = e`/parameter rows pass today]; inert-fix [the deconstruction row is red at 7c4ba48cc, so a green proves the hunk fires].
- Task 6 semantic row: mutation [named by verifier: drop the conjunct → SPY0132 on the escaped row]; positive control [fixture `errors/placeholder_outside_call`].
- Task 7 scan guard: mutation [named: re-add `Current.Value == "when"`]; exemption [corrected — the dumper is rewritten, not exempted]; positive control [synthetic source — yes].
- Task 8 twin tests: mutation [named: re-add `_` to the set → `BacktickInjected_EscapesTheSameSpellingsAsPlainIdentifiers` red]; the "escaped twin would NOT parse" assertion for the eight staying values is the positive reason for the skip (no longer an exemption without a subject).
- Phase 3 fixtures: `.expected` red against the Phase 1 tree via worktree (named); `.error` substrings absent at base (the programs compiled) — discriminating.

### Warnings
- **D1 ruled (a)** — Task 4b lands before 4a; the implementer measures the candidate spellings (`@_` verbatim-kind token vs a reserved mangle) by `run` on the deconstruction cell and regenerates the 2 stdlib/spy-test sources right after (§7), reading the diff as findings.
- **Comprehension / `out let` / `with … as (a, _)` designation cells are unmeasured by the verifier** (in ~5 minutes each); Task 4b's matrix measures them before choosing the spelling.
- **`notnull` after the fix is SPY0202 "Type 'notnull' not found"** — a semantic message naming a soft keyword as a missing type; acceptable (the escaped name IS a type reference) but the `.error` fixture should pin the code, not the prose.
- **Design Decision 3 leaves `before_set`/`after_set` excluded from T2 by VALUE** — consistent with the plan's own rationale (an honoured escape would not parse there either), so the coverage note on #2166 should say that, not "gap".

### Missing Steps Added
- Task 4b (seam + regen early + mutation) — see Implementation.
- Direction probes inside `def main():` (module-level statements are SPY0340 at this sha).
- The two `Value == "ref"` ternaries and `AstDumper.cs:526` in the Task 5/7 scope.

### Unchecked Claims
- The issue's per-token census (250/124/36/14/44/12/26/24/7/1 @ 011fa4068) — re-counted by Phase 1's `CKE-CENSUS` line; not re-measured here.
- `Pretty` writing an escaped `BindingPattern.Name` unchanged — `UnparseVisitor.Patterns.cs:12` visits the `Identifier` (escape-aware writer), consistent with the plan; Task 8's widened T2 is the measurement.
- Whether `@_` (verbatim-kind token) binds in a C# designation — the implementer measures it in Task 4b before picking the spelling.

## Implementation Checklist

Base: 7c4ba48cc. Baseline: src/ byte-identical to 6caca4aca, whose gate was measured — Core 3988/0/0, Cli 378/0/0, Lsp 1094/0/0, Stdlib 4145/0/0, Compiler non-sweep 46093/0/3, GapDiscovery 18/0/0 @ 6caca4aca (measured; `git diff --stat 6caca4aca 7c4ba48cc -- src/` empty). Build green @ 7c4ba48cc.

- [x] W1-A Task 1 roster + predicates — test-expert (lane A) — blocked by none — 7431705a5
- [x] W1-A Task 2 ContextualKeywordEscapeMatrixTests (red) — lane A — blocked by Task 1 — 6ffe3fec1 (KnownRed 26; lead re-ran: n/a)
- [x] W1-A Task 3 ContextualKeywordEscapeSweepTests + allowlist (red, populated) — lane A — blocked by Task 1 — 6fb0a6a7f (CKE-CENSUS tokens=1197 cells=632 red=366; lead re-ran allowlist-emptied mutation: 366 failed / 268 passed @ 3561b6440)
- [x] W1-B Task 4b `_` local designation spelling at CodeGenInfoComputer + Theory + regen — semantic-expert (lane B) — blocked by none — 7b396248c + regen 3561b6440 (spelling `@_` at NameCasing.ResolveVariable; 8 spy-test files spelling-only; lead re-ran arm-disabled mutation: 7 failed of 34 in ~UnderscoreLocal)
- [x] W2 Task 4 `_` parser sites — parser-expert (lane C) — blocked by 1, 2, 3, 4b — d604ebb16
- [x] W2 Task 5 token-level parser sites — lane C — blocked by 4 — a7bbdfa25 (lead re-ran conjunct-dropped mutation: 377 failed / 363 passed in ~ContextualKeywordEscape @ b02015466)
- [x] W2 Task 6 checker SPY0132 arm — lane C — blocked by 4 — b4dc1788b
- [x] W2 Task 7 raw-spelling scan guard — lane C — blocked by 5 — b02015466 (allowlist 0, KnownRed empty, CKE-CENSUS red=0)
- [x] W3 Task 8 T2 skip set re-rationalised — test-expert — blocked by 4, 5 — 499bc22b3 (census at-site add=26 get=127 init=14 out=48 ref=12 remove=24 set=38 when=7; mutation 2 independently corroborated by lane E's 6 except_when reds)
- [x] W3 Task 9 staleness check — lead — blocked by 4b, 4, 5 — no diff: check_spy_staleness + check_spy_tests_staleness exit 0 @ 8e37c2f7b (built sharpyc)
- [x] W3 Task 10 fixtures — test-expert — blocked by 4, 5, 6 — f90af3106 (7 fixtures; base 7c4ba48cc red/absent per fixture, HEAD 7/0/0)
- [x] W3 Task 11 spec + docs — lead/agent — blocked by 4, 5 — 8e37c2f7b (blocks executed; check_spec_blocks failing 0)
- [x] Final verification (gate, sweeps, staleness, spec blocks, verification-expert) — lead — gate @ 499bc22b3: Core 3988/0/0, Stdlib 4145/0/0, Cli 377/1/0, Lsp 1083/3/0, Compiler (incl. GapDiscovery) 46953/1/3; all 5 reds came from the fix itself (4 declined-format tests used `case `_`:`; 2 new parse-error fixtures need the #1360 parity block) → c09971660 + bc2ce6d25; verifier gaps 1–2 → 77d71e05c (scan mutation: 1 red of 32). Re-run @ 77d71e05c (test-only delta from 499bc22b3): Cli 379/0/0, Lsp 1086/0/0, Compiler 46974/0/3. The Lsp "1094" baseline was carried over from the chores round; control @ 7c4ba48cc measured 1086/0/0. Staleness exit 0 @ 8e37c2f7b, check_spec_blocks failing 0, format verify-no-changes exit 0 @ 77d71e05c.
- [x] dotnet format whitespace — lead — verify-no-changes exit 0 @ 499bc22b3
