# Stryker.NET mutation-score pilot: `Lexer/` — 2026-10-11

Issue #2289 (ruling R-GB). Pilot scope: `src/Sharpy.Compiler/Lexer/*.cs` scored against the Lexer's own tests.

## Result

**Mutation score 73.27 % @ 258a7b66a (measured)** — 2,146 of 2,929 tested mutants detected (1,856 killed + 290 timeout), 783 survived, 0 without coverage.

| File | Score | Killed | Survived | Timeout | Ignored | Compile errors | Total |
|------|-------|--------|----------|---------|---------|----------------|-------|
| `Lexer.cs` | 74.55 % | 403 | 157 | 57 | 77 | 114 | 808 |
| `Lexer.FStrings.cs` | 73.62 % | 472 | 206 | 103 | 92 | 25 | 898 |
| `Lexer.Indentation.cs` | 86.23 % | 89 | 19 | 30 | 23 | 6 | 167 |
| `Lexer.LiteralLoss.cs` | 83.65 % | 273 | 60 | 34 | 34 | 34 | 435 |
| `Lexer.Literals.cs` | 66.84 % | 570 | 314 | 63 | 98 | 46 | 1091 |
| `LiteralSpans.cs` | 77.42 % | 45 | 14 | 3 | 5 | 10 | 77 |
| `Token.cs` | 23.53 % | 4 | 13 | 0 | 1 | 0 | 18 |
| **total** | **73.27 %** | **1856** | **783** | **290** | **330** | **235** | **3494** |

"Ignored" = Stryker's block-already-covered filter. "Compile errors" are mutants Stryker could not compile; it does not score them. Safe mode removed every mutation in `Lexer.TokenizeAll` (CS0165 on `token`), so that method has no score.

## Command and cost

```bash
bash build_tools/stryker/run.sh build_tools/stryker/lexer.json <output-dir>
# = .claude/scripts/dotnet-serialized stryker -f build_tools/stryker/lexer.json -O <output-dir> --skip-version-check
```

- **Tool:** `dotnet-stryker` 5.0.0, the repo-root local-tool manifest `dotnet-tools.json`.
- **Config:** `build_tools/stryker/lexer.json`.
  - `mutate` is `**/Lexer/*.cs`.
  - The test filter is the `Sharpy.Compiler.Tests.Lexer.` and `Sharpy.Compiler.Tests.Properties.Lexer.` namespaces: 1,574 tests.
  - Coverage analysis `perTest`, mutation level `Standard`, concurrency 4, Debug.
- **Where it ran:** a detached worktree at 258a7b66a on the owner's macOS machine. The lock was held for the whole run.
- **Wall clock: 7 h 22 min 59 s (measured, Stryker's `Time Elapsed`).**
  - Setup took 12.5 min: analysis, build, the initial test run, mutant coverage, and compiling all 72,781 mutants of `Sharpy.Compiler`. Stryker compiles the whole project even when `mutate` names one directory.
  - Mutant testing took 7 h 10 min: 2,929 mutants, about 35 s per mutant per test session.
- **Peak RSS: 22.1 GB (measured).** This is the sum over every `dotnet`/`testhost` process on the machine, sampled every 30 s. It includes the build and all four test sessions.
- **First attempt killed by the wrapper's watchdog.** It was killed at 2,643 s, after 1,753 s of log silence. Stryker's default `progress` reporter prints nothing when stdout is not a TTY. The config must keep the `dots` reporter, and `run.sh` refuses a config without it.

## Survivor triage

**Scope caveat.** The test filter is the Lexer's own tests. A survivor means "the Lexer tests do not detect this", not "no test in the solution does". The two whole-suite checks below measure that difference for one cell.

### Buckets (all 783 survivors; script classification, by mutated line)

| Bucket | Survivors | Reading |
|--------|-----------|---------|
| Column bookkeeping (`_column++`/`+=` removed or inverted) | 146 | Gap. Every later token on the line gets a wrong column; no Lexer test checks the column of a token that follows the mutated construct. |
| End-of-source bound (`< Length` → `<=`/`>`) | 89 | Gap at end of file. The mutant throws `IndexOutOfRange` only when the construct ends the file with no newline, and no Lexer test lexes that. |
| Diagnostic or log message text | 57 | Equivalent for this purpose. Message text beyond the type name is DEVIATION-by-default (`python-fidelity-scope.md`). Ignore-list candidates. |
| Fixed-token `Value`/`Text` strings | 38 | Mostly unobserved. The parser reads an operator token's `Value` only inside error-message text (grep-only). Ignore-list candidates. |
| `Token` accessors (`ILocatable`, `Position` default) | 11 | Consumers live in `Diagnostics/`, outside the filter's reach. |
| Keyword table / `_keywordNames` | 6 | Gap. Deleting `union`, `delegate`, `super`, `Self` or `self` from the keyword table survives: no Lexer test lexes them as keywords. |
| Constructor (fragment start, logger) | 5 | Mixed. |
| Behavioural, everything else | 431 | See the classes below. |

### Random sample (30 survivors, `random.seed(2289)`, judged by reading the code)

- **23 test gaps:** the mutant changes a token, a value, a position, or turns a diagnostic into a crash on an input no Lexer test lexes.
- **3 plausible gaps:** observable only through a diagnostic span or an unclosed-brace diagnostic.
- **3 equivalent or unobserved:** message text; an empty trivia list attached instead of none; an operator token's `Value`.
- **1 outside the filter's reach.**

So equivalent mutants are about 10 % of the sample, not the majority: the issue's no-go condition ("survivors dominated by equivalent mutants") does not hold. These verdicts come from reading the code, not from running each mutant.

### Candidate classes (for the next `/batch-issues`; never one issue per mutant)

| Class | Survivors in its methods | Contract a guard would state | Representative cells |
|-------|--------------------------|------------------------------|----------------------|
| C1 numeric literals | 152 (`ReadNumber`, hex/octal/binary, prefixed-integer suffix) | every numeric-literal form × {underscore placement, exponent sign, suffix, end of file} lexes to CPython's verdict and value (Sharpy suffixes on their own rows) | leading-dot floats `.5e3` `.5e-3` `.1__2` `.12_` `.5e` `.5e1_`: no test in any test project lexes one (grep-only); `0xFFu` + any following character; trailing-dot `1.` |
| C2 token positions | 146 column + span/`SourceLength` mutants | every token's (line, column, position, length) maps to its source slice | `a >> b`, a token after `b"…"`, `dr"…"`, `"""…"""` |
| C3 line-ending axis | `NextToken`, `ConsumeLineBreak`, `IsPreCloseNewline`, hole trivia | {LF, CRLF, CR} at every line-break site gives the same tokens | backslash continuation over CRLF; newline inside brackets on CR; CRLF in a triple-quoted literal (the compiler handles all of these correctly: `crlf`/`cr` probes print `3 7` @ 258a7b66a; only the tests are missing) |
| C4 f/t-string and dedented-literal arms | 266 | every f-string arm (spec escape, `{{`/`}}`, `==` in a field, nested braces, a comment in hole trivia, dedented triple prescan) is exercised | `f"{a==b}"` (the mutant reads it as a self-documenting `=`), `f"{x:\">4}"`, a `df"""…"""` hole containing a triple-quoted string or a tab |
| C5 LiteralLoss hand-model scanner | 60 | the scanner agrees with the lexer on every escape and brace arm (#2289 named this file: "survivors there are actionable") | a backslash escape and nested `{ }` in a field on a dropped line; spec `\"` |
| C6 end of file | 89 (bucket above) | every literal and operator form lexes the same at end of file with and without a trailing newline | `dr"abc` at EOF (mutant: `IndexOutOfRange` instead of the unterminated-string diagnostic) |
| C7 keyword roster | 6 | every keyword in the spec's list lexes to its token type (anchored to literals, not to `Keywords.Keys`) | `union`, `delegate`, `super`, `Self`, `self` |

### Whole-suite check of one cell (measured)

Two C1 survivors were applied by hand in a scratch worktree at 258a7b66a, in the leading-dot float path of `ReadNumber`:

- **M1:** removes the consecutive-underscore refusal in the fraction (`Lexer.Literals.cs:737`).
- **M2:** removes the `sb.Append` of the exponent letter (`Lexer.Literals.cs:753`).

**Positive control (measured with the mutant build's `sharpyc run`):**
- `print(.5e-3)` dies with SPY0909, an internal error: `The input string '0.5-3' was not in a correct format`.
- `print(.1__2)` compiles and prints `0.12`.
- The unmutated compiler prints `0.0005` and refuses `.1__2`, as CPython does (`SyntaxError: invalid decimal literal`).

**Whole solution, `.claude/scripts/dotnet-serialized test sharpy.sln --no-build --filter "Category!=Benchmark" --logger "console;verbosity=normal"`:** 79207/0/4 at 258a7b66a+M1+M2 (measured; 1,965 s).
- That is the same count as the unmutated gate at e2446c46b.
- No test anywhere in the solution notices either mutant. The C1 leading-dot cell is a gap in the whole suite, not only in the Lexer filter.

## Go / no-go

- **"≥1 survivor is a real test gap": met.**
  - 23 of 30 sampled survivors are gaps by reading.
  - One cell (C1, leading-dot floats) is measured as a gap in the whole suite: a mutant that crashes the compiler with SPY0909 passes all 79,207 tests.
- **"Wall clock fits a nightly job": not met as configured.**
  - The Lexer alone takes 7 h 23 min at concurrency 4. That is above a GitHub-hosted job's 6 h limit.
  - Its 22.1 GB peak is above a standard hosted runner's memory.
  - The whole compiler would be many times larger. The Lexer is 2,929 of the roughly 61,000 mutants that compile; this is a derived ratio, not a run.
  - It fits only as an overnight run on the owner's machine, holding the dotnet lock.

**Verdict:** GO for the instrument; NO-GO for a CI nightly over the compiler.
- **The instrument:** owner-triggered, one component per run, plus `--since` (mutate only the files changed against a base) as the incremental form for Lexer changes. `--since` is not yet measured.
- **Ratchet baseline:** 73.27 % for `Lexer/` @ 258a7b66a.
- **Next pilot:** `Semantic/Validation/` (R-GB) is the next component.
- **Before a ratchet can hold:**
  - Ignore-list the message-text and logger-string mutants (the B and C buckets) with a reason.
  - Remove the `TokenizeAll` CS0165 safe-mode hole, so the score is not inflated by what Stryker could not compile.
- **Survivors:** classes C1–C7 go to the next `/batch-issues` as class issues, never one issue per mutant.

## Ignore list (P68)

P68 (#2300) answered two of the pilot's preconditions. It removed the `TokenizeAll` CS0165 hole in e621d9e94, and it turned the message-text buckets into data with a reason per entry.

- **The data:** `build_tools/stryker/lexer-ignore.json`, applied by `build_tools/stryker/triage.py`. The triage script prints `score`, `scoreIgnoringListed`, the survivor buckets and the C1–C7 class of each mutated method.
- **Not used:** a global `ignore-mutations: ["string"]`, because it would also hide the `Keywords` table keys (C7) and the numeric-suffix roster (C1). `// Stryker disable` comments in production files are not used either.

### What `ignore-methods` skips (measured)

The Stryker docs do not say whether `ignore-methods` also skips mutations in the *arguments* of a matched call. A probe measured it.

```bash
bash build_tools/stryker/run.sh build_tools/stryker/lexer-probe.json <output-dir>
```

- **Probe config:** `build_tools/stryker/lexer-probe.json` is `lexer.json` with these changes:
  - `mutate` is `Token.cs`, `LiteralSpans.cs` and `Lexer.Indentation.cs`. Neither of the first two files calls `ReportError`, so the probe was widened to `Lexer.Indentation.cs`, as the plan allowed.
  - `"ignore-methods": ["ReportError", "LogInfo", "AddWarning", "AddError"]`.
- **Where it ran:** a detached worktree at e621d9e94, through `run.sh`, holding the lock.
- **Result: argument mutations are skipped.**
  - `Lexer.Indentation.cs` has four `throw ReportError("…", _line, 1, code)` sites (lines 47, 51, 55, 72).
  - Each site's message-string mutant is reported `Ignored`, with the reason "Removed by method filter". That is 4 of 4. The json still records them, so the triage script can count them.
  - Log line: `2282 mutants got status Ignored. Reason: Removed by method filter`. That count covers all of `Sharpy.Compiler`, because Stryker applies the method filter before the `mutate` filter. It is not a Lexer figure.
- **Not skipped:** two `_logger.LogIndentChange(…)` statement-removal mutants (`Lexer.Indentation.cs:175`, `:191`) survived. `LogIndentChange` is not in the list. Whether `ignore-methods` also drops the statement-removal mutant of the matched call itself was not probed. That is the next entry to measure.
- **Score: 82.16 % @ e621d9e94 (measured)** over the three files.
  - 144 killed, 31 timeout, 38 survived, 0 without coverage.
  - 16 compile errors, 33 ignored: 29 block-already-covered and 4 method filter.
  - `triage.py` gives `scoreIgnoringListed` 82.16 %. The data file's `within ReportError|…` entry matched 0, because Stryker had already removed those mutants.

| File | Killed | Timeout | Survived | Ignored | Compile error |
|---|---|---|---|---|---|
| `Lexer.Indentation.cs` | 94 | 27 | 13 | 27 (4 method filter) | 6 |
| `LiteralSpans.cs` | 46 | 4 | 12 | 5 | 10 |
| `Token.cs` | 4 | 0 | 13 | 1 | 0 |

- **Cost (measured):**
  - **Wall clock:** 5,835 s (1 h 37 min), Stryker `Time Elapsed` 1:37:14.
  - **Setup:** 24.5 min, including 17 min of per-test coverage capture over 2,217 tests. The pilot's whole setup was 12.5 min over 1,574 tests.
  - **Testing:** 213 mutants in 72.8 min, about 20.5 s of wall clock per mutant at concurrency 4. The pilot's figure was about 8.8 s of wall clock per mutant (35 s per session ÷ 4).
  - **Peak RSS:** 5.55 GB, the maximum single-process RSS that `/usr/bin/time -l` reports for the run. This is not the pilot's whole-machine sum.
- **What the cost means for the full re-run (derived, not measured).**
  - The P68 harnesses cover most of the Lexer, so under `perTest` almost every mutant now runs the corpus twin, the injection differential and the CPython numeric twin.
  - At the probe's rate, the pilot's 2,929 tested mutants would take about 16.7 h, against the pilot's 7 h 10 min.
  - Timeouts are 15 % of the probe's tested mutants. Each one costs the full timeout. In `Lexer.Indentation.cs` they are mostly infinite loops from a deleted `tempPos++` or `_position++`.
  - The full re-run (P68 Phase 5 Task 3) needs an owner slot sized from this, not from the pilot's 7 h.

**Decision:** `lexer.json` carries `ignore-methods` for the message and logger sinks. It is measured to skip the argument mutations, and those mutants are then not tested at all. The data file stays authoritative for what `ignore-methods` cannot express: the `CreateToken` operator `Value` entry, and sinks the config does not list, such as `ReportUnclosedField` and `ReportFieldEndedByQuote`.

### Per-class mutation control (P68 Phase 5 Task 2)

This control applies one recorded survivor-class mutant at a time in a private worktree at e621d9e94, and runs the Lexer filter for each (`FullyQualifiedName~Sharpy.Compiler.Tests.Lexer.|FullyQualifiedName~Sharpy.Compiler.Tests.Properties.Lexer.`, 2217 tests). Each file is restored by a plain copy afterwards. Script and logs are in `.claude/tmp/p68/lead/ctl/` (measured).

| Class | Mutant | Lexer filter |
|---|---|---|
| C1 numeric literals | `ReadHexNumber` trailing-underscore check off (`Lexer.Literals.cs`) | red, 1 failed (`NumericLiteralTwinTests.SharedGrammar_AgreesWithCPython`) |
| C2 token positions | `>>` advances `_column` by 1, not 2 (`Lexer.cs`) | red, 7 failed |
| C3 line endings | `ConsumeLineBreak` takes CRLF as one char (`Lexer.cs`) | red, 9 failed |
| C4 f/t/df arms | `IsPreCloseNewline` call inverted (`Lexer.FStrings.cs`) | red, 46 failed |
| C5 LiteralLoss scanner | the `{{` escaped-brace skip removed (`Lexer.LiteralLoss.cs`) | red, 2 failed |
| C6 end of file | single-line `dr"…"` loop bound `<` → `<=` (`Lexer.Literals.cs`) | red, 2 failed |
| C7 keyword roster | `"self"` removed from `_keywordNames` (`Lexer.cs`) | red, 1 failed |
| — | all restored | green, 2214 passed / 0 failed / 3 skipped (the three #2337 df skips) |
