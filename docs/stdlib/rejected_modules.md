# Rejected Standard Library Modules

Python standard library modules that were evaluated and deliberately excluded from Sharpy's stdlib roadmap. Each entry explains why the module is not a good fit.

## Evaluation Criteria

A module is rejected when it meets one or more of these criteria:

1. **CPython-specific** — tightly coupled to CPython internals with no meaningful Sharpy equivalent
2. **Replaced by .NET** — .NET provides superior alternatives accessible via inline CLR or Sharpy's type system
3. **Wrong abstraction layer** — belongs at the framework/application level, not in a language stdlib
4. **Platform-specific** — limited to a single OS with no cross-platform story
5. **Deprecated/superseded** — already superseded in the Python ecosystem itself

---

## Serialization

### `pickle`, `shelve`, `marshal`

**Reason:** CPython-specific serialization formats.

These modules serialize Python objects into CPython-specific binary formats. They have no meaning outside the Python runtime — you cannot deserialize a pickle in .NET, and Sharpy objects are .NET objects. Use `json`, `yaml`, `toml`, or .NET's own serialization (`System.Text.Json`, `MessagePack`, `protobuf`) instead.

### `copyreg`

**Reason:** `pickle` support infrastructure — rejected along with `pickle`.

---

## Python Language Services

### `ast`, `symtable`, `token`, `keyword`, `tokenize`, `tabnanny`, `pyclbr`, `py_compile`, `compileall`, `dis`, `pickletools`

**Reason:** CPython-specific. These inspect or manipulate Python source code, bytecode, and parse trees. Sharpy has its own compiler pipeline — these modules are meaningless outside CPython.

---

## Debugging & Profiling (CPython)

### `bdb`, `pdb`, `faulthandler`, `tracemalloc`, `trace`

**Reason:** CPython-specific debugging infrastructure. Sharpy programs are .NET IL — use .NET debugging tools (Visual Studio, JetBrains Rider, `dotnet-trace`, `dotnet-dump`, `dotnet-counters`) instead.

### `timeit`

**Reason:** Replaced by .NET. Use `BenchmarkDotNet` for proper microbenchmarking with warmup, statistical analysis, and memory diagnostics. A `timeit`-style module would give misleading results on .NET due to JIT compilation warmup.

---

## GUI

### `tkinter` (and submodules), `turtle`, `IDLE`

**Reason:** Wrong abstraction layer. Tk is a specific GUI toolkit, not a language primitive. Sharpy targets .NET, which has its own GUI frameworks (WPF, MAUI, Avalonia, Blazor). Shipping a Tk binding would add a massive native dependency for minimal value. Use .NET GUI frameworks directly.

---

## Concurrency (Advanced)

### `asyncio`

**Reason:** Requires dedicated language design. Sharpy maps `async`/`await` to C# `Task<T>`. A full asyncio-equivalent module (event loop, coroutine scheduling, futures, transports, protocols) requires deep integration with the compiler's async model. This is a major feature, not a stdlib module — it will be designed separately if needed.

### `multiprocessing`, `multiprocessing.shared_memory`

**Reason:** Heavy OS-specific infrastructure. .NET provides `System.Diagnostics.Process` for process spawning and `System.IO.MemoryMappedFiles` for shared memory. The Python `multiprocessing` API is designed around forking (Unix) and pickling (serialization), neither of which applies to Sharpy.

### `concurrent.futures`, `concurrent.interpreters`

**Reason:** `concurrent.futures` is a thin wrapper over threading/multiprocessing — once we have `threading`, the value is marginal. `concurrent.interpreters` is CPython's sub-interpreter API, entirely CPython-specific.

---

## Low-Level / OS-Specific

### `ctypes`

**Reason:** Replaced by .NET. Sharpy has inline CLR access (backtick syntax) for calling .NET APIs, and .NET has `P/Invoke` (`[DllImport]`) for native interop. `ctypes` is Python's FFI — Sharpy doesn't need its own FFI layer on top of .NET's.

### `mmap`

**Reason:** Replaced by .NET. Use `System.IO.MemoryMappedFiles.MemoryMappedFile` directly via inline CLR.

### `signal`

**Reason:** Platform-specific and low-level. Signal handling differs fundamentally between Unix and Windows. .NET provides `Console.CancelKeyPress` for Ctrl+C and `AppDomain.ProcessExit` for shutdown hooks, which cover the common use cases.

### `select`, `selectors`

**Reason:** Replaced by .NET. .NET's `System.Net.Sockets.Socket.Select()`, `Poll()`, and async socket APIs (`SocketAsyncEventArgs`, `ValueTask`-based) are superior. These Python modules exist because Python lacked native async — .NET doesn't have that problem.

### `fcntl`, `termios`, `tty`, `pty`, `resource`

**Reason:** Unix-specific C APIs. No Windows equivalent, no .NET wrapper. Use P/Invoke if needed.

### `posix`, `pwd`, `grp`

**Reason:** Unix-specific system calls. Use .NET's `System.Runtime.InteropServices` or `System.IO` APIs which abstract platform differences.

### `syslog`

**Reason:** Unix-specific logging. Sharpy already has a `logging` module; use .NET's `ILogger` + Syslog providers for platform-specific log targets.

---

## Windows-Specific

### `msvcrt`, `winreg`, `winsound`

**Reason:** Windows-only. Use .NET's `Microsoft.Win32.Registry` for registry access, `System.Media.SoundPlayer` for audio. These are better accessed via inline CLR than wrapped in cross-platform-pretending modules.

---

## Networking (Advanced)

### `socketserver`, `http.server`, `xmlrpc`

**Reason:** Wrong abstraction layer. These are server frameworks, not language primitives. .NET has ASP.NET Core, Kestrel, and gRPC. Shipping toy HTTP/XML-RPC servers in the stdlib adds complexity without matching what .NET already offers.

### `ftplib`, `poplib`, `imaplib`, `smtplib`

**Reason:** Niche protocol clients. FTP, POP3, IMAP, SMTP are legacy protocols. If needed, use NuGet packages (MailKit for email, FluentFTP for FTP) which are better maintained than stdlib wrappers.

### `ssl`

**Reason:** Replaced by .NET. .NET's `SslStream` and `HttpClient` handle TLS natively with system certificate stores. A separate `ssl` module would duplicate what .NET already provides transparently.

---

## Data Persistence (Legacy)

### `dbm`

**Reason:** Unix-specific legacy database format. Sharpy already has `sqlite3`, which is cross-platform and more capable.

---

## Text Processing (Niche)

### `unicodedata`

**Reason:** Replaced by .NET. .NET's `System.Globalization.CharUnicodeInfo` and `System.Text.Unicode` provide Unicode character data. Low demand as a standalone module.

### `stringprep`

**Reason:** Niche internet protocol string preparation (RFC 3454). Extremely specialized — use .NET's `System.Globalization.IdnMapping` for internationalized domain names.

### `readline`, `rlcompleter`

**Reason:** CPython interactive shell infrastructure. Sharpy's REPL uses .NET-based line editing.

### `codecs`

**Reason:** Replaced by .NET. .NET's `System.Text.Encoding` provides comprehensive codec support (UTF-8, UTF-16, ASCII, ISO-8859-*, etc.). The Python `codecs` module exists because Python 2 had complex string/bytes handling — not relevant to Sharpy.

---

## Compression (Niche)

### `lzma`, `bz2`

**Reason:** Lower priority, not rejected outright. These could be added later if there's demand. `zlib`/`gzip`/`zipfile` cover the most common compression needs. If added, `lzma` would use `System.IO.Compression.BrotliStream` or a NuGet package, and `bz2` would need `SharpZipLib` or similar.

### `compression.zstd`

**Reason:** Too new (Python 3.14+). Could be reconsidered once the Python API stabilizes. Would use a NuGet Zstandard binding.

---

## Internationalization

### `gettext`, `locale`

**Reason:** Replaced by .NET. .NET has a comprehensive globalization and localization story (`System.Globalization.CultureInfo`, resource files, `IStringLocalizer`). Python's `gettext` is a wrapper around GNU gettext — .NET uses its own resource-based localization.

---

## Packaging & Distribution

### `ensurepip`, `venv`, `zipapp`

**Reason:** Python packaging infrastructure. Sharpy uses NuGet and .NET project tooling.

---

## Runtime Services (CPython)

### `gc`, `inspect`, `__future__`, `site`, `sysconfig`, `builtins` (module), `__main__`, `atexit`, `warnings`, `contextlib`, `abc`, `traceback`, `dataclasses`, `annotationlib`

**Reason:** Mix of CPython internals and features handled differently in Sharpy:

- `gc` — .NET has its own GC, not user-controllable in the same way
- `inspect` — .NET reflection (`System.Reflection`) replaces this
- `dataclasses` — Sharpy has `@dataclass` as a first-class language feature, not a runtime module
- `abc` — Sharpy uses interfaces (Axiom 1)
- `contextlib` — Sharpy maps `with` to `IDisposable` (Axiom 1)
- `warnings` — Sharpy uses compiler diagnostics (SPY codes), not runtime warnings
- `atexit` — Use `AppDomain.ProcessExit` via inline CLR
- `traceback` — Use .NET's `Exception.StackTrace`

---

## Other

### `array`

**Reason:** Replaced by .NET. .NET arrays (`T[]`) and `System.Buffers` provide efficient typed arrays. Python's `array` module exists to escape the overhead of Python lists — .NET doesn't have that overhead.

### `weakref`

**Reason:** Replaced by .NET. Use `System.WeakReference<T>` via inline CLR.

### `types`, `enum`

**Reason:** Sharpy has these as language features, not runtime modules. Sharpy classes, structs, and enums are first-class language constructs.

### `numbers`

**Reason:** Abstract numeric tower. Sharpy's type system with `int`, `long`, `float`, `double`, `complex`, and `decimal` doesn't need an abstract numeric hierarchy — .NET's numeric interfaces (`INumber<T>` in .NET 7+) provide this.

### `reprlib`

**Reason:** Niche. `pprint` covers the main use case (pretty-printing). `reprlib.Repr` for truncated representations is rarely needed.

### `graphlib`

**Reason:** Niche. Topological sorting. Could be added to `collections` or as a standalone if there's demand, but low priority.

### `plistlib`

**Reason:** Apple-specific format. Niche. Use a NuGet package if needed.

### `netrc`

**Reason:** Legacy authentication file format. Niche.

### `wave`

**Reason:** Niche audio format. Use NAudio or similar NuGet packages for audio processing.

### `filecmp`, `linecache`

**Reason:** Niche file utilities. `filecmp` can be done with hashlib + os. `linecache` is a CPython optimization for tracebacks.

### `cmd`

**Reason:** Interactive command interpreter framework. Use `System.CommandLine` (already used by Sharpy CLI) for command-line interfaces.

### `optparse`, `getopt`

**Reason:** Superseded by `argparse` in Python itself. Sharpy already has `argparse`.

### `curses`

**Reason:** Terminal UI library. Platform-specific (Unix). Use .NET TUI libraries (Spectre.Console, Terminal.Gui) instead.

### `webbrowser`

**Reason:** Tiny utility to open URLs in browser. Trivially done with `Process.Start()` via inline CLR. Not worth a module.

### `sched`, `queue`, `contextvars`

**Reason:** `sched` is a simple event scheduler (use `System.Timers.Timer`). `queue` is a thread-safe queue (use `System.Collections.Concurrent`). `contextvars` maps to `AsyncLocal<T>`. All better accessed via .NET directly.

---

## Summary

| Category | Count | Reason |
|----------|-------|--------|
| CPython-specific | ~20 | No equivalent outside CPython runtime |
| Replaced by .NET | ~15 | .NET provides superior alternatives |
| Wrong layer | ~8 | Framework/application concerns, not stdlib |
| Platform-specific | ~8 | Unix-only or Windows-only |
| Niche/superseded | ~10 | Low demand or already superseded |

For any rejected module, the .NET equivalent is typically accessible via Sharpy's inline CLR backtick syntax or by adding the appropriate NuGet package as a project reference.
