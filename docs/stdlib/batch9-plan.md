<!-- Verified by /verify-plan on 2026-05-29 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Stdlib Batch 9: threading, socket, difflib, fractions

## Context

Implement the four "advanced" stdlib modules from the [Tier 2 roadmap](docs/stdlib/roadmap.md) Batch 9. These are more complex modules with lower immediate demand but important for specific use cases: `threading` provides thread-based concurrency, `socket` provides low-level networking, `difflib` provides text comparison/diffing, and `fractions` provides exact rational number arithmetic.

**GitHub issues:**
- [#753](https://github.com/antonsynd/sharpy/issues/753) — threading module (thread-based concurrency)
- [#754](https://github.com/antonsynd/sharpy/issues/754) — socket module (low-level networking)
- [#746](https://github.com/antonsynd/sharpy/issues/746) — difflib module (text comparison and diffing)
- [#757](https://github.com/antonsynd/sharpy/issues/757) — fractions module (rational number arithmetic)

## Current State

- **33+ stdlib modules** exist in `src/Sharpy.Stdlib/` (31 original + Toml + Yaml; earlier batches may add more by the time this plan executes)
- None of the four modules exist yet
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files in `modules/`
- `Bytes` type exists in `Sharpy.Core/Bytes.cs` — socket uses bytes for binary I/O
- No NuGet dependencies needed — all four modules use only BCL types
- `System.Threading`, `System.Net.Sockets`, and `System.Numerics.BigInteger` are available on both `net10.0` and `netstandard2.1`

## Design Decisions

### General

1. **All four modules are hand-written C#** (not `.spy`-generated). Rationale: threading wraps complex `System.Threading` primitives with context manager semantics; socket wraps `System.Net.Sockets.Socket` with low-level byte operations; difflib is algorithmic (SequenceMatcher); fractions needs operator overloading and BigInteger. All are cleaner in C#. Follow the pattern of Hashlib. [CORRECTED: Subprocess module does not exist in the codebase — removed reference]

2. **Implementation order: fractions → difflib → threading → socket.** Rationale:
   - `fractions` is self-contained, smallest API surface, no dependencies — quick win to build momentum
   - `difflib` is self-contained with clear algorithmic scope — complex but no external concerns
   - `threading` requires careful concurrency design — medium complexity
   - `socket` is largest (many constants, methods, error types) and hardest to test — last

3. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` where needed.

4. **No new NuGet dependencies.** All four modules use only BCL types.

### fractions Module

5. **`Fraction` is a sealed class, not a struct** (C# 9.0 compatibility). Backed by `System.Numerics.BigInteger` for arbitrary-precision numerator/denominator (matching Python). Immutable — all arithmetic returns new instances.

6. **Automatic GCD reduction on construction.** `Fraction(6, 4)` normalizes to `Fraction(3, 2)`. Negative fractions normalize sign to numerator: `Fraction(-1, -3)` → `Fraction(1, 3)`, `Fraction(1, -3)` → `Fraction(-1, 3)`. `Fraction(0, N)` normalizes to `Fraction(0, 1)`.

7. **Construction from multiple types** (matching Python):
   - `Fraction(int numerator, int denominator)` — basic form
   - `Fraction(int value)` — integer as fraction (denominator = 1)
   - `Fraction(double value)` — exact float conversion via `BigInteger` scaling (produces large denominators like Python's `Fraction(0.1)` → `3602879701896397/36028797018963968`)
   - `Fraction(string value)` — parse `"3/4"`, `"3.14"`, `"-1/3"`, `"0"`, `".5"`, `"1e-3"`. Throws `ValueError` on invalid format.
   - `Fraction(Fraction other)` — copy constructor

8. **Full operator support via C# operators** (Axiom 1 — .NET native operators):
   - Arithmetic: `+`, `-`, `*`, `/`, `%` (all return `Fraction`)
   - Floor division: expose as `FloorDiv()` method (C# has no `//` operator). Returns `long` (matching Python's `int` return type). [CORRECTED: Python's `Fraction.__floordiv__` returns `int`, not `Fraction`]
   - Power: `Pow(int exponent)` method. Negative exponents invert the fraction.
   - Unary: `-`, `+`, `abs()` (via `Abs()` method)
   - Comparison: `<`, `<=`, `>`, `>=`, `==`, `!=` (cross-multiply to avoid floating-point)
   - Mixed arithmetic with `int`: `Fraction(1,3) + 1` → `Fraction(4,3)`

9. **`LimitDenominator(long maxDenominator = 1000000)`** — rational approximation using the Stern-Brocot tree / mediant algorithm (matching Python's implementation). `Fraction(0.1).LimitDenominator(10)` → `Fraction(1, 10)`.

10. **`float()` and `int()` conversions**: `ToDouble()` returns `(double)numerator / (double)denominator`. `ToInt()` truncates toward zero (matching Python's `int(Fraction(7,3))` → `2`).

11. **Hash compatibility**: `GetHashCode()` for `Fraction(1, 2)` should ideally equal `0.5.GetHashCode()` for consistency, but this is not required by Python (Python guarantees `hash(Fraction(1,2)) == hash(0.5)` but .NET doesn't have a cross-type hash contract). We follow .NET conventions for `GetHashCode` (hash based on numerator/denominator) and document the difference.

### difflib Module

12. **`SequenceMatcher` is the core class** — implements the Ratcliff/Obershelp pattern matching algorithm (matching Python). Works on `List<string>` (for line-level diffs) and `string` (for character-level diffs). Uses a generic `IList<T>` internally.

13. **`SequenceMatcher` constructor takes `Func<T, bool>? isJunk`** (matching Python's `isjunk` parameter). `null` means no junk function. The autojunk heuristic (for sequences over 200 items, elements appearing more than 1% of the time are treated as popular/junk) is enabled by default — controlled by `autojunk` constructor parameter (default `true`, matching Python).

14. **Module-level diff functions** produce `IEnumerable<string>` (lazy, matching Python's generator behavior):
    - `UnifiedDiff(lines_a, lines_b, fromfile, tofile, fromfiledate, tofiledate, n)` — unified diff format
    - `ContextDiff(lines_a, lines_b, ...)` — context diff format
    - `Ndiff(lines_a, lines_b, linejunk, charjunk)` — informative diff with intra-line changes
    - `GetCloseMatches(word, possibilities, n, cutoff)` — fuzzy matching

15. **`Differ` class for detailed comparison** — uses SequenceMatcher internally, produces output with `'  '` (common), `'- '` (only in a), `'+ '` (only in b), `'? '` (guide) prefixes. Exposed as `Differ` class with `Compare(a, b)` method.

16. **`HtmlDiff` class deferred to v2.** It's a large class (~400 lines in CPython) that generates HTML table diffs. Low priority for a v1 of the difflib module — the core diffing APIs (SequenceMatcher, unified_diff, context_diff, ndiff, get_close_matches) cover the primary use cases. Document as "not yet implemented" if referenced.

17. **Helper functions** `IS_LINE_JUNK` and `IS_CHARACTER_JUNK` are module-level functions matching Python — `IS_LINE_JUNK` returns true for blank lines (whitespace-only) or lines containing only a `#` comment (regex: `\s*(?:#\s*)?$`), `IS_CHARACTER_JUNK` returns true for space or tab characters (not newline). [CORRECTED: IS_LINE_JUNK also matches `#` comment-only lines per CPython implementation]

### threading Module

18. **Core types map to .NET primitives:**
    - `Thread` → wraps `System.Threading.Thread`
    - `Lock` → wraps `System.Threading.Lock` on .NET 10+ / `object` with `Monitor.Enter`/`Monitor.Exit` on netstandard2.1
    - `RLock` → wraps a reentrant lock via `Monitor` (already reentrant in .NET)
    - `Event` → wraps `ManualResetEventSlim`
    - `Semaphore` → wraps `SemaphoreSlim`
    - `BoundedSemaphore` → wraps `SemaphoreSlim` with over-release detection
    - `Barrier` → wraps `System.Threading.Barrier`
    - `Timer` → wraps `System.Threading.Timer` (NOT a Thread subclass — Sharpy deviation)

19. **No GIL — document as key difference from Python.** .NET has true parallelism. This is the most important behavioral difference and should be clearly documented. Race conditions that are "safe" in CPython due to the GIL may cause real bugs in Sharpy's threading.

20. **`Thread` API:**
    - Constructor: `Thread(Action target, string? name = null, bool daemon = false, object[]? args = null)`
      - In Python, `target` is a callable and `args` is a tuple. In Sharpy, `target` is `Action` (no args — use closures for argument capture, which is the idiomatic .NET pattern). If `args` support is needed, provide overloads with `Action<object[]>`.
    - Properties: `Name`, `Daemon` (maps to `IsBackground`), `Ident` (thread ID, null before start), `IsAlive`
    - Methods: `Start()`, `Join(double? timeout = null)`, `Run()` (virtual, for subclassing), `IsAlive` (property, not method — matching Python's `is_alive()`)
    - Deprecated Python methods (`getName`, `setName`, `isDaemon`, `setDaemon`) are NOT implemented — Sharpy uses property access directly.

21. **Lock context manager support via `IDisposable`:**
    - `Lock.Acquire(bool blocking = true, double timeout = -1)` → `bool`
    - `Lock.Release()` → void
    - `Lock.Locked()` → `bool`
    - `using (lock.Acquire()) { ... }` pattern for context manager behavior — `Acquire()` returns a disposable guard that calls `Release()` on dispose.
    - Alternative: implement as explicit `IDisposable` on the Lock class itself.

22. **Module-level functions:**
    - `CurrentThread()` → returns Thread wrapper for `Thread.CurrentThread`
    - `ActiveCount()` → returns `Process.Threads.Count` or tracks spawned threads
    - `Enumerate()` → list of active Thread objects (tracked internally)
    - `MainThread()` → returns Thread wrapper for the main thread

23. **Timer is NOT a Thread subclass** (Sharpy deviation from Python). Python's `Timer` extends `Thread`, which is a CPython-specific design. In .NET, `System.Threading.Timer` is a separate concept. Sharpy's `Timer` wraps `System.Threading.Timer` directly:
    - Constructor: `Timer(double interval, Action function, object[]? args = null)`
    - Methods: `Start()`, `Cancel()`

### socket Module

24. **`Socket` class wraps `System.Net.Sockets.Socket`:**
    - Constructor: `Socket(int family = AF_INET, int type = SOCK_STREAM, int proto = 0)`
    - Maps Python constants to .NET enums internally (e.g., `AF_INET` (2) → `AddressFamily.InterNetwork`)
    - All methods that return/accept addresses use `(string, int)` tuples for IPv4 (matching Python's `(host, port)` convention)

25. **Address constants as module-level integers** (matching Python):
    - `AF_INET = 2`, `AF_INET6 = 30` (platform-dependent — use .NET enum values), `AF_UNIX = 1`
    - `SOCK_STREAM = 1`, `SOCK_DGRAM = 2`
    - `SOL_SOCKET`, `SO_REUSEADDR`, `SO_KEEPALIVE`, etc. — expose only the most commonly used options
    - `SHUT_RD = 0`, `SHUT_WR = 1`, `SHUT_RDWR = 2`
    - `SOMAXCONN = 128` (use .NET value if available)

26. **Socket methods (core subset for v1):**
    - Connection: `Bind(addr)`, `Listen(backlog)`, `Accept()` → `(Socket, addr)`, `Connect(addr)`, `ConnectEx(addr)` → int
    - Data: `Send(data)` → int, `Sendall(data)`, `Recv(bufsize)` → `Bytes`, `Recvfrom(bufsize)` → `(Bytes, addr)`
    - Control: `Close()`, `Shutdown(how)`, `Settimeout(timeout)`, `Gettimeout()`, `Setblocking(flag)`, `Getblocking()`
    - Info: `Getsockname()` → addr, `Getpeername()` → addr, `Fileno()` → int
    - Options: `Setsockopt(level, optname, value)`, `Getsockopt(level, optname)` → int
    - Context manager: `IDisposable` for `using` blocks
    - `Makefile(mode)` — NOT implemented in v1 (complex stream wrapping, niche use case)

27. **Module-level convenience functions (v1 subset):**
    - `Gethostname()` → `Dns.GetHostName()`
    - `Gethostbyname(hostname)` → `Dns.GetHostAddresses(hostname)` (first IPv4)
    - `Getaddrinfo(host, port, family, type, proto, flags)` → list of `(family, type, proto, canonname, sockaddr)`
    - `Getnameinfo(sockaddr, flags)` → `(host, service)`
    - `CreateConnection(address, timeout, sourceAddress)` → `Socket` (convenience TCP connect)
    - `InetAton(ipString)` → `Bytes`, `InetNtoa(packedIp)` → string
    - `InetPton(af, ipString)` → `Bytes`, `InetNtop(af, packedIp)` → string
    - `Htons(x)`, `Htonl(x)`, `Ntohs(x)`, `Ntohl(x)` — byte order conversion

28. **Error types match Python's hierarchy:**
    - `SharpySocketError : Exception` with `[SharpyModuleType("socket", "error")]` (Python's `socket.error` is `OSError`, but we use a custom base since Sharpy doesn't have `OSError`). CLR name is `SharpySocketError` to avoid conflict with `System.Net.Sockets.SocketError` enum. [CORRECTED: `SocketError` conflicts with the .NET enum `System.Net.Sockets.SocketError`]
    - `SharpySocketTimeout : SharpySocketError` with `[SharpyModuleType("socket", "timeout")]` (Python's `socket.timeout` is `TimeoutError`)
    - `SharpySocketGaiError : SharpySocketError` with `[SharpyModuleType("socket", "gaierror")]` (DNS resolution errors)
    - `SharpySocketHError : SharpySocketError` with `[SharpyModuleType("socket", "herror")]` (address-related errors)

29. **Platform constant values differ between macOS/Linux/Windows.** Use .NET's `AddressFamily`, `SocketType`, `ProtocolType` enum values cast to int rather than hardcoding platform-specific values. `AF_INET` is `(int)AddressFamily.InterNetwork` (always 2), `AF_INET6` is `(int)AddressFamily.InterNetworkV6` (10 on Linux, 30 on macOS, 23 on Windows). Document that numeric constants match the runtime platform.

## Implementation

Module implementation order: fractions (smallest) → difflib (self-contained algorithmic) → threading (concurrency primitives) → socket (largest, most constants).

### Phase 1: fractions Module

**Goal:** Implement `fractions` — exact rational number arithmetic. Medium module (~400 lines).

#### Tasks

1. **Create fractions module directory and registration** — `src/Sharpy.Stdlib/Fractions/__Init__.cs`
   - Create `Fractions/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("fractions")]` on `public static partial class FractionsModule`
   - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
   - Acceptance: `FractionsModule` class compiles with `[SharpyModule]` attribute
   - Commit: `feat(stdlib): scaffold fractions module registration`

2. **Implement Fraction class** — `src/Sharpy.Stdlib/Fractions/Fraction.cs`
   - Create `[SharpyModuleType("fractions")]` sealed class `Fraction : IComparable<Fraction>, IEquatable<Fraction>`:
     - Internal storage: `BigInteger _numerator`, `BigInteger _denominator` (always positive denominator, GCD-reduced)
     - Properties: `BigInteger Numerator { get; }`, `BigInteger Denominator { get; }`
     - Constructors:
       - `Fraction(long numerator = 0, long denominator = 1)` — normalize: GCD reduce, move sign to numerator, `denominator == 0` throws `ZeroDivisionError` (matching Python's error message `"Fraction(1, 0)"`)
       - `Fraction(BigInteger numerator, BigInteger denominator)` — same normalization
       - `Fraction(double value)` — exact float-to-fraction via `BitConverter.DoubleToInt64Bits` decomposition (extract sign, exponent, mantissa; construct exact BigInteger ratio). Matches Python's behavior: `Fraction(0.1)` → `3602879701896397/36028797018963968`
       - `Fraction(string value)` — parse formats: `"3/4"`, `"3.14"`, `"-1/3"`, `"0"`, `".5"`, `"1e-3"`, `" 3/4 "` (strip whitespace). Throws `ValueError` for invalid format.
       - `Fraction(Fraction other)` — copy
     - Private: `static (BigInteger, BigInteger) Normalize(BigInteger num, BigInteger den)` — GCD reduce, normalize sign, handle zero
     - Arithmetic operators (all return `Fraction`):
       - `operator +(Fraction, Fraction)` — `(a.num * b.den + b.num * a.den) / (a.den * b.den)`, then normalize
       - `operator -(Fraction, Fraction)` — same pattern
       - `operator *(Fraction, Fraction)` — `(a.num * b.num) / (a.den * b.den)`
       - `operator /(Fraction, Fraction)` — `(a.num * b.den) / (a.den * b.num)`, throws `ZeroDivisionError` if b is zero
       - `operator %(Fraction, Fraction)` — `a - (a // b) * b` where `//` is floor division
       - `operator -(Fraction)` — unary negate
       - `operator +(Fraction)` — unary plus (returns copy)
     - Mixed operators with `long`:
       - `operator +(Fraction, long)` and `operator +(long, Fraction)` — convert int to `Fraction(n, 1)`
       - Same for `-`, `*`, `/`, `%`
     - Methods:
       - `long FloorDiv(Fraction other)` — floor division: `BigInteger.DivRem` then adjust for negative, return as `long` [CORRECTED: Python's `//` on Fraction returns `int`, not `Fraction`]
       - `Fraction Pow(int exponent)` — positive exp: `(num^exp, den^exp)`. Negative exp: `(den^|exp|, num^|exp|)`. Zero exp: `Fraction(1, 1)`.
       - `Fraction Abs()` — `Fraction(BigInteger.Abs(num), den)`
       - `Fraction LimitDenominator(long maxDenominator = 1000000)` — Stern-Brocot / mediant algorithm matching Python's implementation. Returns the closest rational with denominator <= maxDenominator.
       - `double ToDouble()` — `(double)_numerator / (double)_denominator`
       - `long ToLong()` — truncate toward zero: `(long)BigInteger.Divide(_numerator, _denominator)`
     - Comparison operators: `==`, `!=`, `<`, `<=`, `>`, `>=`
       - Cross-multiply to compare: `a.num * b.den` vs `b.num * a.den`
     - Equality and hashing:
       - `Equals(Fraction)`, `Equals(object)` — compare numerator and denominator
       - `GetHashCode()` — `HashCode.Combine(_numerator, _denominator)` (on netstandard2.1, use manual hash combination)
       - `CompareTo(Fraction)` — cross-multiply comparison
     - String representation:
       - `ToString()` — `"1/3"` for non-integer, `"2"` for integer (denominator == 1). Matches Python's `str(Fraction(1,3))` → `"1/3"`.
   - Acceptance: Fraction compiles with all operators and methods, matches Python behavior for all construction and arithmetic
   - Commit: `feat(stdlib): implement fractions Fraction class`

3. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Fractions.csproj`
   - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
   - Set `<AssemblyName>Sharpy.Stdlib.Fractions</AssemblyName>`
   - Set `<Compile Include="../Fractions/**/*.cs" />`
   - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Fractions.csproj` succeeds
   - Commit: `build(stdlib): add Sharpy.Stdlib.Fractions project file`

4. **Create spy stub file** — `src/Sharpy.Stdlib/spy/fractions_module.spy`
   - Write Sharpy source defining the module-level type export
   - Types: `Fraction`
   - Acceptance: file defines the Fraction type with correct constructor signatures
   - Commit: `feat(stdlib): add fractions module spy source`

5. **Add fractions module tests** — `src/Sharpy.Stdlib.Tests/FractionsModuleTests.cs`
   - Test construction:
     - `Fraction(1, 3)` — numerator=1, denominator=3
     - `Fraction(6, 4)` — auto-reduces to 3/2
     - `Fraction(-1, -3)` — normalizes to 1/3
     - `Fraction(1, -3)` — normalizes to -1/3
     - `Fraction(0, 5)` — normalizes to 0/1
     - `Fraction(1, 0)` — throws ZeroDivisionError
     - `Fraction(5)` — 5/1
     - `Fraction(0.1)` — `3602879701896397/36028797018963968`
     - `Fraction("3/4")` — 3/4
     - `Fraction("3.14")` — 157/50
     - `Fraction("-1/3")` — -1/3
     - `Fraction(".5")` — 1/2
     - `Fraction("invalid")` — throws ValueError
   - Test arithmetic:
     - `Fraction(1,3) + Fraction(1,6)` → `1/2`
     - `Fraction(1,2) - Fraction(1,3)` → `1/6`
     - `Fraction(2,3) * Fraction(3,4)` → `1/2`
     - `Fraction(1,2) / Fraction(1,3)` → `3/2`
     - `Fraction(1,3) % Fraction(1,5)` → `2/15`
     - `Fraction(1,3).FloorDiv(Fraction(1,5))` → `1` (as `long`, not `Fraction`) [CORRECTED: matches Python's `int` return type]
     - `Fraction(1,3).Pow(2)` → `1/9`
     - `Fraction(1,3).Pow(-1)` → `3/1`
     - `-Fraction(1,3)` → `-1/3`
     - `Fraction(1,3).Abs()` vs `Fraction(-1,3).Abs()` → both `1/3`
   - Test mixed arithmetic:
     - `Fraction(1,3) + 1` → `4/3`
     - `2 * Fraction(1,3)` → `2/3`
   - Test comparison:
     - `Fraction(1,3) < Fraction(1,2)` → true
     - `Fraction(1,3) == Fraction(2,6)` → true
     - `Fraction(1,3) > Fraction(1,4)` → true
   - Test conversions:
     - `Fraction(1,3).ToDouble()` ≈ 0.333...
     - `Fraction(7,3).ToLong()` → 2
     - `Fraction(-7,3).ToLong()` → -2 (truncate toward zero)
   - Test LimitDenominator:
     - `Fraction(0.1).LimitDenominator(10)` → `1/10`
     - `Fraction(355, 113).LimitDenominator(100)` → `311/99` [CORRECTED: Python returns `311/99` for maxDenominator=100; `22/7` requires maxDenominator=10]
   - Test ToString:
     - `Fraction(1,3).ToString()` → `"1/3"`
     - `Fraction(2,1).ToString()` → `"2"`
     - `Fraction(-3,4).ToString()` → `"-3/4"`
     - `Fraction(0).ToString()` → `"0"`
   - Acceptance: all tests pass
   - Commit: `test(stdlib): add fractions module tests`

### Phase 2: difflib Module — Core

**Goal:** Implement `SequenceMatcher` and module-level diff functions. Large module (~800 lines).

#### Tasks

6. **Create difflib module directory and registration** — `src/Sharpy.Stdlib/Difflib/__Init__.cs`
   - Create `Difflib/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("difflib")]` on `public static partial class DifflibModule`
   - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
   - Acceptance: `DifflibModule` class compiles with `[SharpyModule]` attribute
   - Commit: `feat(stdlib): scaffold difflib module registration`

7. **Implement SequenceMatcher class** — `src/Sharpy.Stdlib/Difflib/SequenceMatcher.cs`
   - Create `[SharpyModuleType("difflib")]` sealed class `SequenceMatcher<T>` where T : IEquatable<T>:
     - Constructor: `SequenceMatcher(Func<T, bool>? isJunk, IList<T> a, IList<T> b, bool autoJunk = true)`
     - Internal state:
       - `_a`, `_b` — the two sequences
       - `_isJunk` — junk element predicate
       - `_autoJunk` — enable heuristic for popular elements
       - `_b2j` — `Dictionary<T, List<int>>` mapping elements in b to their indices
       - `_fullBCount` — full element counts for b (used by `QuickRatio`)
       - Lazy-computed matching blocks and opcodes
     - Methods:
       - `void SetSeqs(IList<T> a, IList<T> b)` — update both sequences
       - `void SetSeq1(IList<T> a)` — update sequence a only (reuses b's index)
       - `void SetSeq2(IList<T> b)` — update sequence b (rebuilds index)
       - `(int a, int b, int size) FindLongestMatch(int aLo, int aHi, int bLo, int bHi)` — find longest common subsequence in the given ranges using the Ratcliff/Obershelp algorithm with junk filtering
       - `List<(int a, int b, int size)> GetMatchingBlocks()` — return list of matching blocks (sorted, non-overlapping). Final sentinel `(len(a), len(b), 0)` appended.
       - `List<(string tag, int i1, int i2, int j1, int j2)> GetOpcodes()` — return list of opcodes: `"equal"`, `"replace"`, `"insert"`, `"delete"`. Derived from matching blocks.
       - `List<(string tag, int i1, int i2, int j1, int j2)[]> GetGroupedOpcodes(int n = 3)` — group opcodes with n lines of context. Returns groups separated by large "equal" blocks.
       - `double Ratio()` — `2.0 * M / T` where M = total matching characters, T = total elements
       - `double QuickRatio()` — upper bound approximation (faster)
       - `double RealQuickRatio()` — cheapest upper bound: `2.0 * min(len(a), len(b)) / (len(a) + len(b))`
     - Implementation: use `_ChainB()` (matching Python's internal) to build the b-to-indices map:
       - Build `_b2j` from b's elements
       - If `autoJunk && len(b) >= 200`: mark elements appearing > 1% of len(b) as popular, remove from `_b2j`
       - Apply `isJunk` to filter additional elements
   - Also provide a convenience non-generic `SequenceMatcher` class for `string` sequences (lines), since that's the most common use case:
     - `SequenceMatcher(Func<string, bool>? isJunk, List<string> a, List<string> b, bool autoJunk = true)` — delegates to `SequenceMatcher<string>`
   - Acceptance: SequenceMatcher compiles, matching blocks and opcodes match Python output for test cases
   - Commit: `feat(stdlib): implement difflib SequenceMatcher`

8. **Implement Differ class** — `src/Sharpy.Stdlib/Difflib/Differ.cs`
   - Create `[SharpyModuleType("difflib")]` sealed class `Differ`:
     - Constructor: `Differ(Func<string, bool>? lineJunk = null, Func<string, bool>? charJunk = null)`
     - `IEnumerable<string> Compare(List<string> a, List<string> b)` — produce a delta using SequenceMatcher:
       - `"  "` prefix — common line
       - `"- "` prefix — line only in a
       - `"+ "` prefix — line only in b
       - `"? "` prefix — guide line showing intra-line changes (carets/tildes)
     - Internal: uses SequenceMatcher for line-level diff, then SequenceMatcher<char> for intra-line `"? "` guides
   - Acceptance: Differ produces correct output with guide lines
   - Commit: `feat(stdlib): implement difflib Differ class`

9. **Implement difflib module-level functions** — `src/Sharpy.Stdlib/Difflib/DifflibFunctions.cs`
   - Implement as `public static partial class DifflibModule`:
     - `IEnumerable<string> UnifiedDiff(List<string> a, List<string> b, string fromFile = "", string toFile = "", string fromFileDate = "", string toFileDate = "", int n = 3, string lineterm = "\n")`:
       - Produce unified diff format: `--- fromFile`, `+++ toFile`, `@@ -i,j +k,l @@` hunks
       - Uses `SequenceMatcher.GetGroupedOpcodes(n)` for context
       - `lineterm` controls line ending (default newline, `""` to omit)
     - `IEnumerable<string> ContextDiff(List<string> a, List<string> b, string fromFile = "", string toFile = "", string fromFileDate = "", string toFileDate = "", int n = 3, string lineterm = "\n")`:
       - Produce context diff format: `*** fromFile`, `--- toFile`, `***` separator, `*** i,j ****` and `--- i,j ----` sections
     - `IEnumerable<string> Ndiff(List<string> a, List<string> b, Func<string, bool>? lineJunk = null, Func<string, bool>? charJunk = null)`:
       - Delegates to `Differ(lineJunk, charJunk).Compare(a, b)`
     - `List<string> GetCloseMatches(string word, List<string> possibilities, int n = 3, double cutoff = 0.6)`:
       - Use SequenceMatcher to score each possibility against word
       - Return top n matches with ratio >= cutoff, sorted by score descending
       - Matches Python: `get_close_matches("appel", ["ape", "apple", "peach"])` → `["apple", "ape"]`
     - `bool IsLineJunk(string line)` — returns true if line is blank (only whitespace + optional newline)
     - `bool IsCharacterJunk(string ch)` — returns true if ch is space or tab (matching Python's `IS_CHARACTER_JUNK`)
     - `IEnumerable<string> Restore(IEnumerable<string> delta, int which)` — restore one side from ndiff output. `which=1` → lines from a, `which=2` → lines from b.
   - Acceptance: all functions compile and match Python output
   - Commit: `feat(stdlib): implement difflib module-level functions`

10. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Difflib.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Difflib</AssemblyName>`
    - Set `<Compile Include="../Difflib/**/*.cs" />`
    - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Difflib.csproj` succeeds
    - Commit: `build(stdlib): add Sharpy.Stdlib.Difflib project file`

11. **Create spy stub file** — `src/Sharpy.Stdlib/spy/difflib_module.spy`
    - Write Sharpy source defining the module-level function signatures and type exports
    - Types: `SequenceMatcher`, `Differ`
    - Functions: `unified_diff`, `context_diff`, `ndiff`, `get_close_matches`, `IS_LINE_JUNK`, `IS_CHARACTER_JUNK`, `restore`
    - Acceptance: file defines all signatures with correct types
    - Commit: `feat(stdlib): add difflib module spy source`

12. **Add difflib module tests** — `src/Sharpy.Stdlib.Tests/DifflibModuleTests.cs`
    - Test SequenceMatcher:
      - `Ratio()` for `"abcde"` vs `"abdce"` → `0.8`
      - `GetMatchingBlocks()` returns correct blocks with sentinel
      - `GetOpcodes()` returns correct operation tags
      - `GetGroupedOpcodes()` groups with context
      - `QuickRatio()` >= `Ratio()` (upper bound property)
      - `RealQuickRatio()` >= `QuickRatio()`
      - Junk function: custom `isJunk` excludes elements from matching
      - Autojunk: elements appearing > 1% in long sequences are deprioritized
      - Empty sequences: ratio is 1.0 for two empty, 0.0 for one empty
    - Test GetCloseMatches:
      - `GetCloseMatches("appel", ["ape", "apple", "peach"])` → `["apple", "ape"]`
      - `cutoff=0.9` filters out worse matches
      - `n=1` returns only the best match
      - No matches above cutoff → empty list
    - Test UnifiedDiff:
      - Basic diff output matches Python format exactly (header, hunks, context)
      - `n=0` shows no context lines
      - Identical inputs → no output
      - `fromFile`/`toFile` appear in header
    - Test ContextDiff:
      - Output format matches Python (*** / --- sections)
    - Test Ndiff:
      - Common lines prefixed with `"  "`
      - Removed lines prefixed with `"- "`
      - Added lines prefixed with `"+ "`
    - Test Differ:
      - `Compare()` produces correct delta
      - Guide lines (`"? "`) mark intra-line changes
    - Test IsLineJunk / IsCharacterJunk:
      - `IsLineJunk("  \n")` → true
      - `IsLineJunk("  #  \n")` → true (comment-only lines are junk) [CORRECTED: added `#`-comment case per Python behavior]
      - `IsLineJunk("code\n")` → false
      - `IsCharacterJunk(" ")` → true
      - `IsCharacterJunk("\t")` → true
      - `IsCharacterJunk("\n")` → false (newline is NOT junk) [CORRECTED: added newline case — IS_CHARACTER_JUNK only matches space/tab]
      - `IsCharacterJunk("a")` → false
    - Test Restore:
      - Roundtrip: `Restore(Ndiff(a, b), 1)` recovers a
      - `Restore(Ndiff(a, b), 2)` recovers b
    - Edge cases:
      - Very long sequences (autojunk kicks in at 200+)
      - Unicode content
      - Lines without trailing newline
      - Empty input lists
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add difflib module tests`

### Phase 3: threading Module

**Goal:** Implement `threading` — thread-based concurrency primitives. Medium module (~500 lines).

#### Tasks

13. **Create threading module directory and registration** — `src/Sharpy.Stdlib/Threading/__Init__.cs`
    - Create `Threading/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("threading")]` on `public static partial class ThreadingModule`
    - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
    - Acceptance: `ThreadingModule` class compiles with `[SharpyModule]` attribute
    - Commit: `feat(stdlib): scaffold threading module registration`

14. **Implement Lock and RLock** — `src/Sharpy.Stdlib/Threading/Lock.cs`, `src/Sharpy.Stdlib/Threading/RLock.cs`
    - `[SharpyModuleType("threading")]` sealed class `Lock`:
      - Internal: `object _lockObj = new object()` (netstandard2.1). On net10.0 use `System.Threading.Lock`.
      - `bool Acquire(bool blocking = true, double timeout = -1)`:
        - `blocking=true, timeout=-1`: `Monitor.Enter(_lockObj)`, return true
        - `blocking=true, timeout>=0`: `Monitor.TryEnter(_lockObj, TimeSpan)`, return result
        - `blocking=false`: `Monitor.TryEnter(_lockObj)`, return result
      - `void Release()`: `Monitor.Exit(_lockObj)`. Throws `RuntimeError` if not locked by current thread.
      - `bool Locked()`: `Monitor.IsEntered(_lockObj)` (available on both net10.0 and netstandard2.1) [CORRECTED: `Monitor.IsEntered` is available since .NET Standard 2.0 — no need for internal bool tracking]
      - `LockGuard AcquireGuard()` — returns `IDisposable` for `using` pattern
      - Nested sealed class `LockGuard : IDisposable` — calls `Release()` on Dispose
    - `[SharpyModuleType("threading")]` sealed class `RLock`:
      - Internal: uses same `Monitor` (which is already reentrant in .NET)
      - Same API as `Lock` but explicitly reentrant — `Acquire()` can be called multiple times from same thread
      - `Release()` must be called matching number of times
      - Track recursion count for `Locked()` reporting
    - Acceptance: Lock and RLock compile with acquire/release semantics
    - Commit: `feat(stdlib): implement threading Lock and RLock`

15. **Implement Event** — `src/Sharpy.Stdlib/Threading/Event.cs`
    - `[SharpyModuleType("threading")]` sealed class `Event`:
      - Internal: `ManualResetEventSlim _event = new ManualResetEventSlim(false)`
      - `bool IsSet()` — `_event.IsSet`
      - `void Set()` — `_event.Set()`
      - `void Clear()` — `_event.Reset()`
      - `bool Wait(double? timeout = null)` — `timeout == null`: `_event.Wait()`, return true. Otherwise: `_event.Wait(TimeSpan)`, return whether signaled.
    - Acceptance: Event compiles with set/wait/clear semantics
    - Commit: `feat(stdlib): implement threading Event`

16. **Implement Semaphore and BoundedSemaphore** — `src/Sharpy.Stdlib/Threading/Semaphore.cs`
    - `[SharpyModuleType("threading")]` sealed class `Semaphore`:
      - Internal: `SemaphoreSlim _semaphore`
      - Constructor: `Semaphore(int value = 1)` — `_semaphore = new SemaphoreSlim(value)`
      - `bool Acquire(bool blocking = true, double? timeout = null)`:
        - `blocking=false`: `_semaphore.Wait(0)`, return result
        - `timeout != null`: `_semaphore.Wait(TimeSpan)`, return result
        - else: `_semaphore.Wait()`, return true
      - `void Release(int n = 1)` — `_semaphore.Release(n)`
    - `[SharpyModuleType("threading")]` sealed class `BoundedSemaphore`:
      - Same API as `Semaphore` but tracks release count
      - `Release()` throws `ValueError("Semaphore released too many times")` if count would exceed initial value
      - Internal: `_maxValue` and `_currentValue` (with lock for thread safety)
    - Acceptance: Semaphore and BoundedSemaphore compile with correct semantics
    - Commit: `feat(stdlib): implement threading Semaphore and BoundedSemaphore`

17. **Implement Barrier** — `src/Sharpy.Stdlib/Threading/Barrier.cs`
    - `[SharpyModuleType("threading")]` sealed class `Barrier`:
      - Internal: `System.Threading.Barrier _barrier`
      - Constructor: `Barrier(int parties, Action? action = null)` — `_barrier = new Barrier(parties, action != null ? _ => action() : null)`
      - `int Wait(double? timeout = null)` — `_barrier.SignalAndWait(timeout != null ? TimeSpan.FromSeconds(timeout.Value) : Timeout.InfiniteTimeSpan)`. Returns the arrival phase number. Throws `BrokenBarrierError` on timeout.
      - `void Reset()` — dispose and recreate barrier (no direct Reset in .NET)
      - `void Abort()` — `_barrier.Dispose()` (matching Python's barrier abort semantics — subsequent waits throw)
      - Properties: `int Parties`, `int NWaiting` (not directly available in .NET — track manually), `bool Broken`
    - `[SharpyModuleType("threading")]` class `BrokenBarrierError : Exception` — thrown when barrier is broken/aborted
    - Acceptance: Barrier compiles with wait/reset/abort semantics
    - Commit: `feat(stdlib): implement threading Barrier`

18. **Implement Thread class** — `src/Sharpy.Stdlib/Threading/Thread.cs`
    - `[SharpyModuleType("threading")]` class `Thread`:
      - Internal: `System.Threading.Thread _thread` (null until `Start()`)
      - Constructor: `Thread(Action? target = null, string? name = null, bool daemon = false)`
      - Properties:
        - `string Name { get; set; }` — thread name
        - `bool Daemon { get; set; }` — maps to `_thread.IsBackground`. Can only be set before `Start()`.
        - `long? Ident { get; }` — `_thread.ManagedThreadId` (null before start)
        - `bool IsAlive { get; }` — `_thread.IsAlive`
      - Methods:
        - `void Start()` — create `new Thread(Run)`, configure name/daemon, call `_thread.Start()`. Register in global thread list.
        - `void Join(double? timeout = null)` — `_thread.Join(TimeSpan)` or `_thread.Join()`. No return value (match Python).
        - `virtual void Run()` — calls `target()` if set. Can be overridden in subclasses.
      - Thread is NOT subclassable in v1 (sealed) — Python allows subclassing Thread and overriding `run()`, but Sharpy's sealed class with `Action target` covers the primary use case. Document that subclassing will be added in v2 if needed.
      - Actually: make it non-sealed (class, not sealed class) so subclassing works — `Run()` is `virtual`. This matches Python's pattern.
    - Internal thread tracking:
      - Static `List<Thread> _activeThreads` (with lock for thread safety)
      - Register on `Start()`, unregister on thread exit
    - Acceptance: Thread compiles with start/join/daemon semantics
    - Commit: `feat(stdlib): implement threading Thread class`

19. **Implement Timer and module-level functions** — `src/Sharpy.Stdlib/Threading/Timer.cs`, `src/Sharpy.Stdlib/Threading/ThreadingFunctions.cs`
    - `[SharpyModuleType("threading")]` sealed class `Timer`:
      - Internal: `System.Threading.Timer _timer`
      - Constructor: `Timer(double interval, Action function)`
      - `void Start()` — `_timer = new Timer(_ => function(), null, TimeSpan.FromSeconds(interval), Timeout.InfiniteTimeSpan)` (one-shot)
      - `void Cancel()` — `_timer?.Dispose()`
      - `bool IsAlive { get; }` — whether timer is pending
    - Module-level functions in `ThreadingFunctions.cs` (as `public static partial class ThreadingModule`):
      - `Thread CurrentThread()` — wrap `System.Threading.Thread.CurrentThread`. Return a Thread instance backed by the current thread.
      - `int ActiveCount()` — return count of active tracked threads + 1 (main thread)
      - `List<Thread> Enumerate()` — return copy of active thread list
      - `Thread MainThread()` — return Thread wrapper for main thread (cached)
    - Acceptance: Timer and module functions compile
    - Commit: `feat(stdlib): implement threading Timer and module functions`

20. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Threading.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Threading</AssemblyName>`
    - Set `<Compile Include="../Threading/**/*.cs" />`
    - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Threading.csproj` succeeds
    - Commit: `build(stdlib): add Sharpy.Stdlib.Threading project file`

21. **Create spy stub file** — `src/Sharpy.Stdlib/spy/threading_module.spy`
    - Write Sharpy source defining the module-level function signatures and type exports
    - Types: `Thread`, `Lock`, `RLock`, `Event`, `Semaphore`, `BoundedSemaphore`, `Barrier`, `Timer`, `BrokenBarrierError`
    - Functions: `current_thread`, `active_count`, `enumerate`, `main_thread`
    - Acceptance: file defines all signatures with correct types
    - Commit: `feat(stdlib): add threading module spy source`

22. **Add threading module tests** — `src/Sharpy.Stdlib.Tests/ThreadingModuleTests.cs`
    - Test Thread:
      - Create and start thread, join, verify it ran (set a shared bool)
      - Thread.Name is set correctly
      - Thread.Daemon sets IsBackground
      - Thread.Ident is null before start, non-null after
      - Thread.IsAlive is false before start, true during, false after join
      - Thread with target=Action runs the action
      - Thread.Join with timeout returns even if thread is still running
    - Test Lock:
      - Acquire/Release basic flow
      - Acquire(blocking=false) returns false when already locked
      - Acquire(timeout=0.01) returns false when already locked
      - Locked() reflects state
      - LockGuard (using pattern) releases on dispose
      - Release when not locked throws RuntimeError
    - Test RLock:
      - Reentrant: acquire twice from same thread succeeds
      - Must release matching number of times
    - Test Event:
      - Initial state is not set
      - Set/IsSet/Clear cycle
      - Wait with timeout returns false when not set
      - Wait returns true when set
    - Test Semaphore:
      - Acquire up to count succeeds
      - Acquire beyond count with blocking=false returns false
      - Release increments count
    - Test BoundedSemaphore:
      - Same as Semaphore
      - Release beyond initial value throws ValueError
    - Test Barrier:
      - N threads waiting, all released when last arrives
      - Barrier with action runs action once per phase
      - Abort makes subsequent waits throw BrokenBarrierError
    - Test Timer:
      - Timer fires after interval
      - Cancel prevents firing
    - Test module functions:
      - CurrentThread returns a Thread for the current thread
      - ActiveCount >= 1
      - Enumerate returns list including main thread
    - Test thread safety (basic):
      - Multiple threads incrementing a shared counter with Lock → correct final value
    - Note: threading tests inherently involve timing. Use generous timeouts (1-5 seconds) and avoid assertions that depend on exact timing.
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add threading module tests`

### Phase 4: socket Module

**Goal:** Implement `socket` — low-level networking. Large module (~800 lines).

#### Tasks

23. **Create socket module directory and registration** — `src/Sharpy.Stdlib/Socket/__Init__.cs`
    - Create `Socket/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("socket")]` on `public static partial class SocketModule`
    - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
    - Acceptance: `SocketModule` class compiles with `[SharpyModule]` attribute
    - Commit: `feat(stdlib): scaffold socket module registration`

24. **Implement socket error types** — `src/Sharpy.Stdlib/Socket/Errors.cs`
    - Create exception classes (all with `[SharpyModuleType("socket", "pythonName")]`):
      - `SharpySocketError : Exception` `[SharpyModuleType("socket", "error")]` — base. Wraps `System.Net.Sockets.SocketException` messages. [CORRECTED: CLR name avoids conflict with `System.Net.Sockets.SocketError` enum]
        - Properties: `int Errno { get; }` — socket error code
        - Constructor: `SharpySocketError(string message, int errno = 0)`
        - Factory: `static SharpySocketError FromSocketException(SocketException ex)` — extract message and error code
      - `SharpySocketTimeout : SharpySocketError` `[SharpyModuleType("socket", "timeout")]` — timeout errors
      - `SharpySocketGaiError : SharpySocketError` `[SharpyModuleType("socket", "gaierror")]` — DNS resolution errors (getaddrinfo failures)
      - `SharpySocketHError : SharpySocketError` `[SharpyModuleType("socket", "herror")]` — address-related errors
    - Acceptance: error hierarchy compiles matching Python's structure
    - Commit: `feat(stdlib): implement socket error types`

25. **Implement socket constants** — `src/Sharpy.Stdlib/Socket/SocketConstants.cs`
    - Add to `public static partial class SocketModule`:
      - Address families: `AF_INET = (int)AddressFamily.InterNetwork`, `AF_INET6 = (int)AddressFamily.InterNetworkV6`, `AF_UNIX = (int)AddressFamily.Unix`
      - Socket types: `SOCK_STREAM = (int)SocketType.Stream`, `SOCK_DGRAM = (int)SocketType.Dgram`, `SOCK_RAW = (int)SocketType.Raw`
      - Protocol: `IPPROTO_TCP = (int)ProtocolType.Tcp`, `IPPROTO_UDP = (int)ProtocolType.Udp`
      - Socket options: `SOL_SOCKET = (int)SocketOptionLevel.Socket`, `SO_REUSEADDR = (int)SocketOptionName.ReuseAddress`, `SO_KEEPALIVE = (int)SocketOptionName.KeepAlive`, `SO_BROADCAST = (int)SocketOptionName.Broadcast`, `SO_RCVBUF = (int)SocketOptionName.ReceiveBuffer`, `SO_SNDBUF = (int)SocketOptionName.SendBuffer`
      - TCP options: `TCP_NODELAY = (int)SocketOptionName.NoDelay`
      - Shutdown: `SHUT_RD = (int)SocketShutdown.Receive`, `SHUT_WR = (int)SocketShutdown.Send`, `SHUT_RDWR = (int)SocketShutdown.Both`
      - `SOMAXCONN = (int)SocketOptionName.MaxConnections` or hardcode 128
    - Implementation note: use `(int)EnumValue` casts from `System.Net.Sockets` enums to get platform-correct values. These are `static readonly int` (not const) since enum values may vary by platform.
    - Acceptance: constants accessible and have correct platform-appropriate values
    - Commit: `feat(stdlib): implement socket module constants`

26. **Implement Socket class** — `src/Sharpy.Stdlib/Socket/SocketClass.cs`
    - Create `[SharpyModuleType("socket", "socket")]` sealed class `SocketClass : IDisposable`: [CORRECTED: need PythonName="socket" so Sharpy code sees `socket.socket`, not `socket.SocketClass`]
      - Internal: `System.Net.Sockets.Socket _socket`
      - Constructor: `SocketClass(int family = AF_INET, int type = SOCK_STREAM, int proto = 0)`:
        - `_socket = new Socket((AddressFamily)family, (SocketType)type, (ProtocolType)proto)`
      - Internal constructor: `SocketClass(System.Net.Sockets.Socket socket)` — wrap existing socket (for Accept)
      - Properties:
        - `int Family { get; }` — `(int)_socket.AddressFamily`
        - `int Type { get; }` — `(int)_socket.SocketType`
        - `int Proto { get; }` — `(int)_socket.ProtocolType`
      - Connection methods:
        - `void Bind((string host, int port) address)` — parse to IPEndPoint, `_socket.Bind(endpoint)`
        - `void Listen(int backlog = 128)` — `_socket.Listen(backlog)`
        - `(SocketClass socket, (string host, int port) address) Accept()` — `_socket.Accept()`, wrap result, extract address
        - `void Connect((string host, int port) address)` — resolve host via `Dns.GetHostAddresses`, `_socket.Connect(endpoint)`
        - `int ConnectEx((string host, int port) address)` — non-blocking connect, return 0 on success, errno on failure
      - Data methods:
        - `int Send(Bytes data, int flags = 0)` — `_socket.Send(data.ToArray(), (SocketFlags)flags)`
        - `void Sendall(Bytes data)` — send all bytes, looping if needed
        - `int Sendto(Bytes data, (string host, int port) address, int flags = 0)` — `_socket.SendTo`
        - `Bytes Recv(int bufsize, int flags = 0)` — `_socket.Receive(buffer, (SocketFlags)flags)`, return as Bytes
        - `(Bytes data, (string host, int port) address) Recvfrom(int bufsize, int flags = 0)` — `_socket.ReceiveFrom`
      - Control methods:
        - `void Close()` — `_socket.Close()`
        - `void Shutdown(int how)` — `_socket.Shutdown((SocketShutdown)how)`
        - `void Settimeout(double? seconds)` — `null` → blocking, `0` → non-blocking, `>0` → timeout. Sets `_socket.ReceiveTimeout` and `_socket.SendTimeout`.
        - `double? Gettimeout()` — return current timeout setting
        - `void Setblocking(bool flag)` — `Settimeout(flag ? null : 0)`
        - `bool Getblocking()` — `_socket.Blocking`
      - Info methods:
        - `(string host, int port) Getsockname()` — extract from `_socket.LocalEndPoint`
        - `(string host, int port) Getpeername()` — extract from `_socket.RemoteEndPoint`
        - `int Fileno()` — `(int)_socket.Handle` (platform-dependent, documented as unsafe)
      - Options:
        - `void Setsockopt(int level, int optname, int value)` — `_socket.SetSocketOption((SocketOptionLevel)level, (SocketOptionName)optname, value)`
        - `int Getsockopt(int level, int optname)` — `(int)_socket.GetSocketOption((SocketOptionLevel)level, (SocketOptionName)optname)`
      - `void Dispose()` — `_socket.Dispose()`
      - Helper: `static IPEndPoint ResolveEndpoint(string host, int port)` — resolve hostname to IP, create endpoint. Handles numeric IPs directly.
    - Error handling: wrap `SocketException` in `SharpySocketError`, wrap timeout exceptions in `SharpySocketTimeout`.
    - Acceptance: Socket compiles with all methods
    - Commit: `feat(stdlib): implement socket Socket class`

27. **Implement socket module-level functions** — `src/Sharpy.Stdlib/Socket/SocketFunctions.cs`
    - Implement as `public static partial class SocketModule`:
      - `string Gethostname()` — `Dns.GetHostName()`
      - `string Gethostbyname(string hostname)` — `Dns.GetHostAddresses(hostname)`, return first IPv4 as string
      - `List<(int family, int type, int proto, string canonname, (string host, int port) sockaddr)> Getaddrinfo(string? host, int? port, int family = 0, int type = 0, int proto = 0, int flags = 0)`:
        - Wraps `Dns.GetHostAddresses` and constructs result tuples matching Python format
        - `family=0` means any, filter by family if specified
      - `(string host, string service) Getnameinfo((string host, int port) sockaddr, int flags = 0)`:
        - Reverse DNS lookup via `Dns.GetHostEntry`
      - `SocketClass CreateConnection((string host, int port) address, double? timeout = null, (string host, int port)? sourceAddress = null)`:
        - Create TCP socket, optionally bind to sourceAddress, connect with timeout
      - Byte order functions:
        - `int Htons(int x)` — `IPAddress.HostToNetworkOrder((short)x)` cast back to int
        - `int Htonl(int x)` — `IPAddress.HostToNetworkOrder(x)`
        - `int Ntohs(int x)` — `IPAddress.NetworkToHostOrder((short)x)` cast back to int
        - `int Ntohl(int x)` — `IPAddress.NetworkToHostOrder(x)`
      - IP conversion functions:
        - `Bytes InetAton(string ipString)` — `IPAddress.Parse(ipString).GetAddressBytes()` as Bytes
        - `string InetNtoa(Bytes packedIp)` — `new IPAddress(packedIp.ToArray()).ToString()`
        - `Bytes InetPton(int af, string ipString)` — parse IPv4/IPv6, return packed bytes
        - `string InetNtop(int af, Bytes packedIp)` — `new IPAddress(packedIp.ToArray()).ToString()`
      - `double? Getdefaulttimeout()` — return module-level default timeout (null = no timeout)
      - `void Setdefaulttimeout(double? timeout)` — set module-level default timeout
    - Acceptance: all functions compile
    - Commit: `feat(stdlib): implement socket module-level functions`

28. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Socket.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Socket</AssemblyName>`
    - Set `<Compile Include="../Socket/**/*.cs" />`
    - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Socket.csproj` succeeds
    - Commit: `build(stdlib): add Sharpy.Stdlib.Socket project file`

29. **Create spy stub file** — `src/Sharpy.Stdlib/spy/socket_module.spy`
    - Write Sharpy source defining the module-level function signatures, constants, and type exports
    - Types: `socket` (SocketClass), `error` (SharpySocketError), `timeout` (SharpySocketTimeout), `gaierror` (SharpySocketGaiError), `herror` (SharpySocketHError)
    - Functions: `gethostname`, `gethostbyname`, `getaddrinfo`, `getnameinfo`, `create_connection`, `htons`, `htonl`, `ntohs`, `ntohl`, `inet_aton`, `inet_ntoa`, `inet_pton`, `inet_ntop`, `getdefaulttimeout`, `setdefaulttimeout`
    - Constants: `AF_INET`, `AF_INET6`, `AF_UNIX`, `SOCK_STREAM`, `SOCK_DGRAM`, `IPPROTO_TCP`, `IPPROTO_UDP`, `SOL_SOCKET`, `SO_REUSEADDR`, etc.
    - Acceptance: file defines all signatures with correct types
    - Commit: `feat(stdlib): add socket module spy source`

30. **Add socket module tests** — `src/Sharpy.Stdlib.Tests/SocketModuleTests.cs`
    - Test Socket creation:
      - `SocketClass(AF_INET, SOCK_STREAM)` creates TCP socket
      - `SocketClass(AF_INET, SOCK_DGRAM)` creates UDP socket
      - `Family`, `Type`, `Proto` properties correct
    - Test TCP loopback:
      - Server: bind to localhost:0, listen, accept
      - Client: connect to server's port
      - Send/recv data roundtrip
      - Close both ends
    - Test UDP loopback:
      - Bind to localhost:0
      - Sendto/recvfrom data roundtrip
    - Test socket options:
      - Setsockopt/Getsockopt SO_REUSEADDR
      - Settimeout/Gettimeout
      - Setblocking/Getblocking
    - Test DNS functions:
      - Gethostname returns non-empty string
      - Gethostbyname("localhost") returns "127.0.0.1"
      - Getaddrinfo("localhost", 80) returns valid entries
    - Test byte order:
      - Htons/Ntohs roundtrip
      - Htonl/Ntohl roundtrip
    - Test IP conversion:
      - InetAton("127.0.0.1") → 4 bytes
      - InetNtoa(InetAton("127.0.0.1")) → "127.0.0.1"
      - InetPton(AF_INET, "127.0.0.1") → 4 bytes
      - InetNtop(AF_INET, bytes) → "127.0.0.1"
    - Test error types:
      - Connect to refused port → SharpySocketError
      - Recv with timeout → SharpySocketTimeout
      - Gethostbyname("nonexistent.invalid") → SharpySocketGaiError
    - Test CreateConnection:
      - Successful TCP connection to loopback server
      - Timeout on unreachable host
    - Test cleanup:
      - Dispose/Close releases resources
      - Using pattern works correctly
    - Note: socket tests must use loopback (127.0.0.1) and ephemeral ports (bind to port 0, read assigned port). No external network access needed. Add `[Trait("Category", "Integration")]` for tests that create real sockets.
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add socket module tests`

### Phase 5: Documentation

**Goal:** Add batch plan doc for reference.

#### Tasks

31. **Add Batch 9 plan to docs** — `docs/stdlib/batch9-plan.md`
    - Save this plan (cleaned up) as the batch plan document in the docs directory
    - Follow the same format as `docs/stdlib/batch5-plan.md` and `docs/stdlib/batch7-plan.md`
    - Acceptance: document exists with correct content
    - Commit: `docs(stdlib): add Batch 9 implementation plan for threading, socket, difflib, fractions`

## Testing Strategy

### New test fixtures needed

- `src/Sharpy.Stdlib.Tests/FractionsModuleTests.cs` — ~30 tests covering construction, arithmetic, comparison, conversions, LimitDenominator
- `src/Sharpy.Stdlib.Tests/DifflibModuleTests.cs` — ~35 tests covering SequenceMatcher, UnifiedDiff, ContextDiff, Ndiff, Differ, GetCloseMatches, Restore
- `src/Sharpy.Stdlib.Tests/ThreadingModuleTests.cs` — ~30 tests covering Thread, Lock, RLock, Event, Semaphore, BoundedSemaphore, Barrier, Timer, module functions
- `src/Sharpy.Stdlib.Tests/SocketModuleTests.cs` — ~30 tests covering Socket, TCP/UDP loopback, DNS, byte order, IP conversion, errors

### Edge cases to cover

**fractions:**
- Zero denominator (ZeroDivisionError)
- Very large numerator/denominator (BigInteger overflow from double)
- Negative fractions with various sign combinations
- String parsing edge cases: whitespace, leading/trailing signs, decimal notation, scientific notation
- Division by zero fraction
- LimitDenominator with maxDenominator=1

**difflib:**
- Empty sequences (both, one, neither)
- Identical sequences (no diff)
- Completely different sequences
- Very long sequences (autojunk threshold at 200)
- Unicode content in lines
- Lines with/without trailing newlines
- Single-character sequences for character-level SequenceMatcher

**threading:**
- Thread that throws exception (should not crash other threads)
- Lock contention between multiple threads
- Event wait with timeout=0 (immediate check)
- Barrier with 1 party (trivial case)
- Timer.Cancel before it fires vs. after
- Daemon thread doesn't prevent program exit (difficult to test)

**socket:**
- Connect to refused port
- Send/recv on closed socket
- Timeout on blocking recv
- Large data transfer (exceeding buffer size)
- IPv6 loopback (::1)
- Bind to already-in-use port (address already in use error)

### Negative test cases

- `Fraction(1, 0)` → `ZeroDivisionError`
- `Fraction("invalid")` → `ValueError`
- `Fraction(1, 3) / Fraction(0)` → `ZeroDivisionError`
- `Lock.Release()` when not locked → `RuntimeError`
- `BoundedSemaphore.Release()` beyond initial count → `ValueError`
- Socket connect to unreachable host → `SharpySocketError`
- Socket recv with expired timeout → `SharpySocketTimeout`
- `Gethostbyname("nonexistent.invalid")` → `SharpySocketGaiError`

## Issues to Close

- #757 — fractions module (closed by Phase 1, Task 2 — full module implementation)
- #746 — difflib module (closed by Phase 2, Task 9 — full module implementation)
- #753 — threading module (closed by Phase 3, Task 19 — full module implementation)
- #754 — socket module (closed by Phase 4, Task 27 — full module implementation)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-29
**Plan file:** ~/.claude/plans/plan-694ba2.md

### Corrections Made

1. **Design Decision #1** (line 26): Removed reference to "Subprocess" module — it does not exist in the codebase. Changed to "Follow the pattern of Hashlib."

2. **Design Decision #8** (line 53): Changed `FloorDiv()` return type from `Fraction` to `long`. Python's `Fraction.__floordiv__` returns `int`, not `Fraction`. Verified: `python3 -c "from fractions import Fraction; print(type(Fraction(1,3) // Fraction(1,5)).__name__)"` → `int`.

3. **Task 2, FloorDiv method** (line 205): Changed return type from `Fraction FloorDiv(...)` to `long FloorDiv(...)` and removed "return as Fraction(result, 1)".

4. **Task 5, LimitDenominator test** (line 271): Changed expected result of `Fraction(355, 113).LimitDenominator(100)` from `22/7` to `311/99`. Verified: `python3 -c "from fractions import Fraction; print(Fraction(355,113).limit_denominator(100))"` → `311/99`. (`22/7` is the result for `limit_denominator(10)`.)

5. **Design Decision #17, IS_LINE_JUNK** (line 81): Corrected description — also returns true for lines containing only `#` comments (regex `\s*(?:#\s*)?$`), not just whitespace-only lines.

6. **Task 14, Lock.Locked()** (line 434): Removed netstandard2.1 workaround — `Monitor.IsEntered` is available since .NET Standard 2.0, no need for internal bool tracking.

7. **Design Decision #28, socket error types** (line 155-158): Renamed all socket error classes (`SocketError` → `SharpySocketError`, etc.) to avoid naming conflict with `System.Net.Sockets.SocketError` enum. Added `[SharpyModuleType("socket", "pythonName")]` attributes for Python name mapping.

8. **Task 26, SocketClass** (line 622): Added `[SharpyModuleType("socket", "socket")]` — needs PythonName so Sharpy code sees `socket.socket`, not `socket.SocketClass`.

9. **Task 12, IS_LINE_JUNK/IS_CHARACTER_JUNK tests** (line 400-403): Added `#`-comment test case for IS_LINE_JUNK, added `\t` and `\n` cases for IS_CHARACTER_JUNK to match Python behavior.

10. **Task 5, FloorDiv test** (line 253): Clarified return type is `long`, not `Fraction`.

### Warnings

1. **Thread class sealed/non-sealed contradiction** (Task 18, lines 498-499): The paragraph first says "Thread is NOT subclassable in v1 (sealed)" then immediately reverses to "make it non-sealed". The final decision (non-sealed with virtual `Run()`) is correct, but the contradictory text could confuse an implementer. Consider removing the first "sealed" paragraph.

2. **HashCode.Combine on netstandard2.1** (Task 2, line 212): The plan acknowledges the need for a manual hash combination but doesn't specify the pattern. Consider using `unchecked { int hash = 17; hash = hash * 31 + _numerator.GetHashCode(); hash = hash * 31 + _denominator.GetHashCode(); return hash; }` or similar.

3. **`System.Threading.Lock` (.NET 9+)** (Task 14): Claim is correct — `System.Threading.Lock` was added in .NET 9 and is available on .NET 10. Just noting this is a relatively new type.

4. **Barrier.NWaiting** (Task 17, line 480): "not directly available in .NET — track manually" — this is correct but the implementation will need careful thread-safety. `Interlocked` operations or a separate lock will be needed.

### Missing Steps Added

None — the plan covers all necessary phases (implementation, project files, spy stubs, tests, docs) for each module.

### Unchecked Claims

1. **GitHub issues #753, #754, #746, #757** — Not verified (would require `gh` CLI). Assumed correct based on roadmap references.
2. **`Fraction(0.1)` exact float conversion via `BitConverter.DoubleToInt64Bits`** — Algorithm description is plausible but not verified against a reference implementation. The expected output `3602879701896397/36028797018963968` was verified correct against Python.
3. **Stern-Brocot/mediant algorithm for LimitDenominator** — Not verified against CPython source, but the described approach is standard.
