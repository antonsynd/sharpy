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
