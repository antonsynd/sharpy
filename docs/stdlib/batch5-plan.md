<!-- Verified by /verify-plan on 2026-05-29 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Stdlib Batch 5: subprocess, shlex

## Context

Implement the two "scripting" stdlib modules from the [Tier 2 roadmap](docs/stdlib/roadmap.md) Batch 5. These are frequently needed for scripting and automation — `subprocess` wraps `System.Diagnostics.Process` for spawning child processes, and `shlex` provides shell-like string splitting/quoting (custom implementation, no .NET equivalent).

**GitHub issues:**
- [#752](https://github.com/antonsynd/sharpy/issues/752) — subprocess module (process spawning and management)
- [#756](https://github.com/antonsynd/sharpy/issues/756) — shlex module (shell-like lexical analysis)

## Current State

- **33+ stdlib modules** exist in `src/Sharpy.Stdlib/` (31 original + Toml + Yaml; earlier batches may add more by the time this plan executes)
- Neither subprocess nor shlex exists yet
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files
- No NuGet dependencies needed — both modules use only BCL types
- `Bytes` type exists in `Sharpy.Core/Bytes.cs` — subprocess uses bytes for non-text mode I/O

## Design Decisions

1. **Both modules are hand-written C#** (not `.spy`-generated). Rationale: subprocess wraps `System.Diagnostics.Process`/`ProcessStartInfo` with streaming I/O and pipe management that the Sharpy compiler can't express. shlex could theoretically be `.spy` but the character-level parsing with state machines is cleaner in C#. Follow the pattern of Hashlib/Toml.

2. **shlex is implemented first** (no dependencies, simpler, ~200 lines). subprocess is implemented second (more complex, but independent of shlex — unlike Python where `subprocess` can optionally use `shlex` for `shell=True` on POSIX).

3. **subprocess exception hierarchy matches Python's**:
   - `SubprocessError : Exception` — base exception for subprocess module
   - `CalledProcessError : SubprocessError` — raised on non-zero exit when `check=True`
   - `TimeoutExpired : SubprocessError` — raised when timeout elapses
   All annotated with `[SharpyModuleType("subprocess", "...")]`.

4. **subprocess.run() is the primary API** (matching Python 3.5+ recommendation). `check_output()` and `check_call()` are convenience wrappers around `run()`.

5. **subprocess.Popen maps to a class wrapping `System.Diagnostics.Process`**. Key differences from Python:
   - `PIPE` (-1), `DEVNULL` (-3), `STDOUT` (-2) are integer constants for API compatibility, but internally translated to `ProcessStartInfo` configuration
   - `communicate()` returns `(string, string)` in text mode or `(Bytes, Bytes)` in binary mode — since Sharpy is statically typed, we provide two methods: `Communicate()` returns `(string, string)` (text mode) and `CommunicateBytes()` returns `(Bytes, Bytes)` (binary mode), determined by whether `text=True` was set at construction
   - `shell=True` uses `/bin/sh -c` on Unix and `cmd.exe /c` on Windows (via `UseShellExecute` + command wrapping)

6. **Text mode is the default in Sharpy** (Axiom 2 — Python convenience, but type-safe). Python defaults to binary mode; we default to `text=True` since string I/O is far more common and avoids the bytes/string duality confusion. Binary mode is opt-in via `text=False`. This is a deliberate deviation documented in the module docstring.

7. **CompletedProcess is a sealed class, not a record** (C# 9.0 compatibility for netstandard2.1). Properties: `Args` (list[str]), `Returncode` (int), `Stdout` (str?), `Stderr` (str?). For binary mode, `StdoutBytes`/`StderrBytes` (Bytes?) are separate properties.

8. **shlex.split() is POSIX-mode only** (matching Python 3 default). No non-POSIX mode — it's rarely used and adds complexity. `comments=False` by default (matching Python). `shlex.Shlex` class (the full tokenizer) is NOT implemented in v1 — only the three module-level functions (`split`, `quote`, `join`).

9. **shlex.quote() uses single-quote wrapping** on all platforms (matching Python's POSIX behavior). Does NOT provide Windows `cmd.exe`-style quoting — document this as a known limitation.

10. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` where needed.

11. **No new NuGet dependencies.** subprocess uses `System.Diagnostics.Process` (BCL). shlex is pure string manipulation.

## Implementation

Module implementation order: shlex (simpler, no dependencies, ~200 lines) → subprocess (more complex, ~600 lines). Each module follows the standard stdlib pattern.

### Phase 1: shlex Module

**Goal:** Implement `shlex` — shell-like string splitting and quoting. Small module (~200 lines).

#### Tasks

1. **Create shlex module directory and registration** — `src/Sharpy.Stdlib/Shlex/__Init__.cs`
   - Create `Shlex/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("shlex")]` on `public static partial class ShlexModule`
   - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
   - Acceptance: `ShlexModule` class compiles with `[SharpyModule]` attribute
   - Commit: `feat(stdlib): scaffold shlex module registration`

2. **Implement shlex module functions** — `src/Sharpy.Stdlib/Shlex/Shlex.cs`
   - Implement as `public static partial class ShlexModule`:
     - `Split(string s, bool comments = false, bool posix = true)` → returns `List<string>`
       - POSIX shell tokenization: handles single quotes (preserve literal), double quotes (allow `\` escapes for `"`, `\`, `$`, `` ` ``, newline), backslash escaping, whitespace splitting
       - `comments=true` treats `#` as comment start (to end of string) when not inside quotes
       - `posix` parameter accepted but only `true` is supported (throw `ValueError` if `false` with message "non-POSIX mode is not supported")
       - Throws `ValueError("No closing quotation")` on unclosed quotes (matching Python exactly)
     - `Quote(string s)` → returns `string`
       - Empty string → `"''"` (two single quotes)
       - If string contains only safe characters (`[a-zA-Z0-9@%+=:,./-]`), return as-is
       - Otherwise wrap in single quotes, replacing internal `'` with `'\''` (end quote, escaped quote, start quote)
     - `Join(List<string> splitCommand)` → returns `string`
       - Apply `Quote()` to each element, join with space
   - Implementation notes:
     - Use a simple state machine with states: `Normal`, `InSingleQuote`, `InDoubleQuote`, `Escape`, `EscapeInDoubleQuote`
     - Track current token with `StringBuilder`
     - Python reference behavior (verified):
       - `shlex.split("echo 'hello world' foo")` → `["echo", "hello world", "foo"]`
       - `shlex.split("echo \"hello \\\"world\\\"\"")` → `["echo", "hello \"world\""]` (escaped inner quotes are preserved in output, not consumed) [CORRECTED: verified with Python 3 — actual result includes the inner quotes]
       - `shlex.split("echo foo#bar")` → `["echo", "foo#bar"]` (# not a comment by default)
       - `shlex.quote("hello world")` → `"'hello world'"`
       - `shlex.join(["echo", "hello world"])` → `"echo 'hello world'"`
   - Acceptance: all three functions compile and match Python behavior
   - Commit: `feat(stdlib): implement shlex split, quote, and join`

3. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Shlex.csproj`
   - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
   - Set `<AssemblyName>Sharpy.Stdlib.Shlex</AssemblyName>`
   - Set `<Compile Include="../Shlex/**/*.cs" />`
   - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Shlex.csproj` succeeds
   - Commit: `build(stdlib): add Sharpy.Stdlib.Shlex project file`

4. **Create spy stub file** — `src/Sharpy.Stdlib/spy/shlex_module.spy`
   - Write Sharpy source defining the module-level function signatures
   - Functions: `split(s: str, comments: bool = False, posix: bool = True) -> list[str]`, `quote(s: str) -> str`, `join(split_command: list[str]) -> str`
   - Acceptance: file defines all three function signatures with correct types
   - Commit: `feat(stdlib): add shlex module spy source`

5. **Add shlex tests** — `src/Sharpy.Stdlib.Tests/ShlexTests.cs`
   - Test `Split()`:
     - Basic splitting: `"echo hello world"` → `["echo", "hello", "world"]`
     - Single-quoted strings: `"echo 'hello world'"` → `["echo", "hello world"]`
     - Double-quoted strings: `"echo \"hello world\""` → `["echo", "hello world"]`
     - Backslash escaping outside quotes: `"echo hello\\ world"` → `["echo", "hello world"]`
     - Escaped quotes in double quotes: `"echo \"hello \\\"world\\\"\""` → `["echo", "hello \"world\""]`
     - Mixed quoting: `"echo 'single' \"double\""` → `["echo", "single", "double"]`
     - Empty string: `""` → `[]`
     - Only whitespace: `"   "` → `[]`
     - Hash not treated as comment by default: `"echo foo#bar"` → `["echo", "foo#bar"]`
     - Hash treated as comment when `comments=true`: `"echo foo #comment"` → `["echo", "foo"]`
     - Unclosed single quote: `"echo 'unclosed"` → throws `ValueError`
     - Unclosed double quote: `"echo \"unclosed"` → throws `ValueError`
     - Trailing backslash: `"echo test\\"` → throws `ValueError`
     - Unicode in quoted strings: `"echo '日本語'"` → `["echo", "日本語"]`
     - Adjacent quotes: `"'hello''world'"` → `["helloworld"]`
     - Pipes and redirects (treated as regular tokens): `"echo foo | grep bar"` → `["echo", "foo", "|", "grep", "bar"]`
   - Test `Quote()`:
     - Safe string (no quoting needed): `"hello"` → `"hello"`
     - String with spaces: `"hello world"` → `"'hello world'"`
     - String with single quote: `"it's"` → `"'it'\\''s'"`
     - Empty string: `""` → `"''"`
     - String with special chars: `"hello;world"` → `"'hello;world'"`
   - Test `Join()`:
     - Basic join: `["echo", "hello world"]` → `"echo 'hello world'"`
     - Roundtrip with split: `Split(Join(tokens))` equals `tokens`
     - Empty list: `[]` → `""`
   - Acceptance: all tests pass
   - Commit: `test(stdlib): add shlex module tests`

### Phase 2: subprocess Module

**Goal:** Implement `subprocess` — process spawning and management. Medium module (~600 lines).

#### Tasks

6. **Create subprocess module directory and registration** — `src/Sharpy.Stdlib/Subprocess/__Init__.cs`
   - Create `Subprocess/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("subprocess")]` on `public static partial class SubprocessModule`
   - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
   - Acceptance: `SubprocessModule` class compiles with `[SharpyModule]` attribute
   - Commit: `feat(stdlib): scaffold subprocess module registration`

7. **Implement subprocess exception hierarchy** — `src/Sharpy.Stdlib/Subprocess/Errors.cs`
   - Create exception classes (all `[SharpyModuleType("subprocess", "...")]`):
     - `SubprocessError : Exception` — base; `[SharpyModuleType("subprocess", "SubprocessError")]`
       - Constructor: `SubprocessError(string message) : base(message)`
     - `CalledProcessError : SubprocessError` — `[SharpyModuleType("subprocess", "CalledProcessError")]`
       - Properties: `int Returncode`, `List<string> Cmd`, `string? Output`, `string? Stderr`
       - Constructor: `CalledProcessError(int returncode, List<string> cmd, string? output = null, string? stderr = null)`
       - Message format: `"Command '{string.Join(" ", cmd)}' returned non-zero exit status {returncode}."`
     - `TimeoutExpired : SubprocessError` — `[SharpyModuleType("subprocess", "TimeoutExpired")]`
       - Properties: `List<string> Cmd`, `double Timeout`, `string? Output`, `string? Stderr`
       - Constructor: `TimeoutExpired(List<string> cmd, double timeout, string? output = null, string? stderr = null)`
       - Message format: `"Command '{string.Join(" ", cmd)}' timed out after {timeout} seconds."`
   - Acceptance: exception hierarchy compiles, matches Python's `Exception → SubprocessError → CalledProcessError/TimeoutExpired`
   - Commit: `feat(stdlib): implement subprocess exception hierarchy`

8. **Implement subprocess constants** — `src/Sharpy.Stdlib/Subprocess/SubprocessConstants.cs`
   - Add to `public static partial class SubprocessModule`:
     - `public const int Pipe = -1;` (Python: `subprocess.PIPE`)
     - `public const int Devnull = -3;` (Python: `subprocess.DEVNULL`)
     - `public const int Stdout = -2;` (Python: `subprocess.STDOUT`)
   - Acceptance: constants accessible as `SubprocessModule.Pipe` etc.
   - Commit: `feat(stdlib): add subprocess module constants`

9. **Implement CompletedProcess** — `src/Sharpy.Stdlib/Subprocess/CompletedProcess.cs`
   - Create `[SharpyModuleType("subprocess", "CompletedProcess")]` sealed class:
     - Properties: `List<string> Args { get; }`, `int Returncode { get; }`, `string? Stdout { get; }`, `string? Stderr { get; }`, `Bytes? StdoutBytes { get; }`, `Bytes? StderrBytes { get; }`
     - Constructor: `CompletedProcess(List<string> args, int returncode, string? stdout = null, string? stderr = null, Bytes? stdoutBytes = null, Bytes? stderrBytes = null)`
     - `void CheckReturncode()` — throws `CalledProcessError` if `Returncode != 0`
     - `override string ToString()` — `"CompletedProcess(args={Args}, returncode={Returncode})"`
   - Acceptance: class compiles with all properties and methods
   - Commit: `feat(stdlib): implement subprocess CompletedProcess`

10. **Implement Popen class** — `src/Sharpy.Stdlib/Subprocess/Popen.cs`
    - Create `[SharpyModuleType("subprocess")]` sealed class `Popen : IDisposable`:
      - Constructor parameters (matching Python's most-used subset):
        - `List<string> args` — command and arguments
        - `int stdin = 0` — 0 = inherit, PIPE = create pipe, DEVNULL = /dev/null
        - `int stdout = 0` — same constants
        - `int stderr = 0` — same constants, plus STDOUT = redirect to stdout
        - `bool text = true` — text mode (Sharpy default; Python default is False)
        - `string? cwd = null` — working directory
        - `Dict<string, string>? env = null` — environment variables (null = inherit)
        - `bool shell = false` — run via system shell
        - `string? encoding = null` — text encoding (default UTF-8)
      - Internal: creates `ProcessStartInfo`, configures redirects based on constants, starts `Process`
      - Properties:
        - `int Pid` → `_process.Id`
        - `int? Returncode` → `_process.HasExited ? _process.ExitCode : null`
        - `System.IO.StreamWriter? Stdin` → write end of stdin pipe (when `stdin=PIPE`)
        - `System.IO.StreamReader? StdoutStream` → read end of stdout pipe (when `stdout=PIPE`)
        - `System.IO.StreamReader? StderrStream` → read end of stderr pipe (when `stderr=PIPE`)
      - Methods:
        - `(string, string) Communicate(string? input = null, double? timeout = null)` — text mode: write input to stdin, read stdout+stderr, wait for exit. Uses `Task.WhenAll` for concurrent reads to avoid deadlocks. Throws `TimeoutExpired` on timeout.
        - `(Bytes, Bytes) CommunicateBytes(Bytes? input = null, double? timeout = null)` — binary mode variant
        - `int Wait(double? timeout = null)` — wait for process exit; throws `TimeoutExpired` on timeout
        - `int? Poll()` → check if process has exited, return exit code or null
        - `void Kill()` → `_process.Kill()`
        - `void Terminate()` → `_process.Kill()` on Windows, `_process.Kill(false)` on Unix (SIGTERM not available via .NET `Process` — documented limitation)
        - `void SendSignal(int signal)` → on Unix: `_process.Kill()` (only SIGKILL supported via .NET; documented limitation). On Windows: throws `NotImplementedError`.
        - `void Dispose()` → kill process if still running, dispose `_process`
      - Implementation notes:
        - Deadlock prevention: when both stdout and stderr are PIPE, read them concurrently using `Task.Run` + `ReadToEndAsync()` (standard .NET pattern). Sequential reads can deadlock if the child fills one pipe buffer while we're reading the other.
        - `shell=True`: construct command as `["/bin/sh", "-c", string.Join(" ", args)]` on Unix or `["cmd.exe", "/c", string.Join(" ", args)]` on Windows. Use `RuntimeInformation.IsOSPlatform`.
        - Timeout: use `Process.WaitForExit(TimeSpan)` (.NET 10) or `Process.WaitForExit(int)` on netstandard2.1 (milliseconds).
    - Acceptance: Popen compiles with all methods and handles pipe redirection
    - Commit: `feat(stdlib): implement subprocess Popen class`

11. **Implement subprocess module-level functions** — `src/Sharpy.Stdlib/Subprocess/SubprocessFunctions.cs`
    - Implement as `public static partial class SubprocessModule`:
      - `CompletedProcess Run(List<string> args, bool captureOutput = false, bool text = true, bool check = false, string? input = null, double? timeout = null, string? cwd = null, Dict<string, string>? env = null, bool shell = false, int stdin = 0, int stdout = 0, int stderr = 0)`
        - If `captureOutput` is true, set `stdout=PIPE` and `stderr=PIPE` (override explicit values)
        - If `input` is not null, set `stdin=PIPE`
        - Create `Popen` with the args
        - Call `Communicate(input, timeout)`
        - Build `CompletedProcess` from results
        - If `check` is true, call `CheckReturncode()`
        - Return the `CompletedProcess`
      - `string CheckOutput(List<string> args, bool text = true, double? timeout = null, string? input = null, string? cwd = null, Dict<string, string>? env = null, bool shell = false, int stderr = 0)`
        - Call `Run(args, captureOutput: true, check: true, text: text, timeout: timeout, ...)`
        - Return `result.Stdout` (or throw if non-zero exit)
      - `int CheckCall(List<string> args, double? timeout = null, string? cwd = null, Dict<string, string>? env = null, bool shell = false)`
        - Call `Run(args, check: true, timeout: timeout, ...)`
        - Return `result.Returncode` (always 0, since check=true throws on non-zero)
    - Acceptance: all three functions compile and delegate to Popen correctly
    - Commit: `feat(stdlib): implement subprocess run, check_output, check_call`

12. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Subprocess.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Subprocess</AssemblyName>`
    - Set `<Compile Include="../Subprocess/**/*.cs" />`
    - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Subprocess.csproj` succeeds
    - Commit: `build(stdlib): add Sharpy.Stdlib.Subprocess project file`

13. **Create spy stub file** — `src/Sharpy.Stdlib/spy/subprocess_module.spy`
    - Write Sharpy source defining the module-level function signatures and type exports
    - Types: `CompletedProcess`, `Popen`, `SubprocessError`, `CalledProcessError`, `TimeoutExpired`
    - Functions: `run`, `check_output`, `check_call`
    - Constants: `PIPE`, `DEVNULL`, `STDOUT`
    - Acceptance: file defines all signatures with correct types
    - Commit: `feat(stdlib): add subprocess module spy source`

14. **Add subprocess tests** — `src/Sharpy.Stdlib.Tests/SubprocessTests.cs`
    - Test `Run()`:
      - Basic command: `Run(["echo", "hello"], captureOutput: true)` → stdout is "hello\n", returncode is 0
      - Capture stderr: `Run(["sh", "-c", "echo err >&2"], captureOutput: true)` → stderr is "err\n"
      - Check mode success: `Run(["true"], check: true)` → no exception
      - Check mode failure: `Run(["false"], check: true)` → throws `CalledProcessError` with returncode 1
      - Input piping: `Run(["cat"], input: "hello", captureOutput: true)` → stdout is "hello"
      - Timeout: `Run(["sleep", "10"], timeout: 0.1)` → throws `TimeoutExpired`
      - Working directory: `Run(["pwd"], captureOutput: true, cwd: "/tmp")` → stdout contains "/tmp"
      - Environment variables: `Run(["sh", "-c", "echo $MY_VAR"], captureOutput: true, env: {"MY_VAR": "test"})` → stdout is "test\n"
      - Non-zero exit without check: `Run(["false"])` → returncode is 1, no exception
    - Test `CheckOutput()`:
      - Basic: `CheckOutput(["echo", "hello"])` → "hello\n"
      - Failure: `CheckOutput(["false"])` → throws `CalledProcessError`
    - Test `CheckCall()`:
      - Success: `CheckCall(["true"])` → returns 0
      - Failure: `CheckCall(["false"])` → throws `CalledProcessError`
    - Test `Popen`:
      - Basic: create Popen, wait, check returncode
      - Communicate: Popen with PIPE, communicate("input"), read stdout
      - Poll: Popen with sleep, poll returns null, then returns exit code
      - Kill: Popen with sleep, kill, wait returns non-zero
      - Pid: Popen.Pid is positive integer
      - Dispose: Popen disposes cleanly
    - Test `CompletedProcess`:
      - Properties: args, returncode, stdout, stderr
      - CheckReturncode: throws on non-zero, no-op on zero
      - ToString format
    - Test exceptions:
      - `CalledProcessError` message format and properties
      - `TimeoutExpired` message format and properties
      - Inheritance chain: `CalledProcessError` is `SubprocessError` is `Exception`
    - Skip note: tests that spawn real processes should use simple, portable Unix commands (`echo`, `cat`, `true`, `false`, `sh -c`). Add `[Trait("Category", "Integration")]` to tests that actually spawn processes, so they can be filtered on CI if needed.
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add subprocess module tests`

### Phase 3: Documentation

**Goal:** Add batch plan doc for reference.

#### Tasks

15. **Add Batch 5 plan to docs** — `docs/stdlib/batch5-plan.md`
    - Save this plan (cleaned up) as the batch plan document in the docs directory
    - Follow the same format as `docs/stdlib/batch1-plan.md` and `docs/stdlib/batch4-plan.md`
    - Acceptance: document exists with correct content
    - Commit: `docs(stdlib): add Batch 5 implementation plan for subprocess, shlex`

## Testing Strategy

### New test fixtures needed

- `src/Sharpy.Stdlib.Tests/ShlexTests.cs` — ~20 tests covering split, quote, join
- `src/Sharpy.Stdlib.Tests/SubprocessTests.cs` — ~25 tests covering run, check_output, check_call, Popen, CompletedProcess, exceptions

### Edge cases to cover

**shlex:**
- Empty input, whitespace-only input
- Adjacent quotes (`'hello''world'` → `helloworld`)
- Backslash at end of string
- Unicode characters in quoted strings
- Mixed single/double quoting
- Hash as comment vs. inside token
- Tab and newline as delimiters

**subprocess:**
- Zero-length stdout/stderr
- Very large output (buffer handling)
- Process that exits before communicate()
- Timeout of 0 (immediate timeout)
- Environment with empty values
- Command not found (should raise exception from Process.Start)

### Negative test cases

- `shlex.split` with unclosed quotes → `ValueError`
- `shlex.split` with trailing backslash → `ValueError`
- `subprocess.run` with `check=True` on failing command → `CalledProcessError`
- `subprocess.run` with expired timeout → `TimeoutExpired`
- `subprocess.check_output` on failing command → `CalledProcessError`
- `subprocess.check_call` on failing command → `CalledProcessError`

## Issues to Close

- #752 — subprocess module (closed by Phase 2, Task 11 — full module implementation)
- #756 — shlex module (closed by Phase 1, Task 2 — full module implementation)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-29
**Plan file:** `~/.claude/plans/plan-f17541.md`

### Corrections Made

1. **Line 85 — Python reference behavior for escaped inner quotes**
   - **Before:** `shlex.split("echo \"hello \\\"world\\\"\"")` → `["echo", "hello world"]` (escaped inner quotes consumed)
   - **After:** `shlex.split("echo \"hello \\\"world\\\"\"")` → `["echo", "hello \"world\""]` (escaped inner quotes are preserved in output, not consumed)
   - **Reason:** Verified with Python 3 — the inner escaped quotes are preserved in the output string, not stripped.

### Warnings

1. **List/Dict type ambiguity** — The plan uses `List<string>` and `Dict<string, string>` throughout without qualifying the namespace. Since the stdlib modules live in `namespace Sharpy` and the main `Sharpy.Stdlib.csproj` has `ImplicitUsings disable`, unqualified `List<T>` resolves to `Sharpy.List<T>` (not `System.Collections.Generic.List<T>`) — but only if `using System.Collections.Generic;` is NOT added. Implementation should avoid adding that using directive for public API types. The existing codebase is inconsistent (Argparse uses `System.Collections.Generic.List`, Textwrap uses `Sharpy.List`) — prefer `Sharpy.List` for public APIs since compiled Sharpy code produces `Sharpy.List<T>`.

2. **Terminate() implementation on line 206** — `_process.Kill(false)` does NOT send SIGTERM; the `false` parameter only controls whether child processes are also killed. The signal sent is still SIGKILL. The plan's parenthetical "(SIGTERM not available via .NET Process — documented limitation)" is correct, but the code `_process.Kill(false)` is misleading as a `Terminate()` implementation since it behaves identically to `Kill()` for the signal type. Consider documenting this more clearly or just mapping both `Kill()` and `Terminate()` to `_process.Kill()`.

3. **Trailing backslash error message** — The test on line 119 says trailing backslash throws `ValueError` (correct), but the implementation (line 73) should note that Python's error message for trailing backslash is `"No escaped character"`, which is different from the unclosed-quote message `"No closing quotation"`. Both are `ValueError` but with different messages.

### Missing Steps Added

None — the plan covers all necessary steps for stdlib module implementation (no parser/codegen/semantic changes required).

### Unchecked Claims

1. **Process.WaitForExit(TimeSpan) availability** — Plan references this as a .NET 10 API (line 212). This overload was added in .NET 5, so it's available on both `net10.0` and can be adapted for `netstandard2.1` using the `int` milliseconds overload as the plan suggests. Not verified at the API level, but the claim is consistent with .NET documentation.
