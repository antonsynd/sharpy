# Sharpy Standard Library Plan

## Design Principles

### Axiom application to stdlib

- **Axiom 1 (.NET first)** — The *implementation* is .NET. Every module is backed by .NET APIs internally. The *user-facing API* is Pythonic. `import system.*` exists as an advanced escape hatch, not the default.
- **Axiom 2 (Python syntax)** — A Python developer should recognize and reach for familiar module names and function signatures.
- **Axiom 3 (Static typing)** — Every API is fully typed. Modules that are inherently dynamic (inspect, ctypes, eval) are excluded.

### Error handling convention

Sharpy's stdlib uses `Optional[T]` and `Result[T, E]` natively. No dual APIs — `try expr` and `maybe expr` bridge .NET's exceptions/nullables into Result/Optional when calling .NET APIs directly.

| Programmer intent | Convention | Example |
|---|---|---|
| "It might not be there" | `Optional[T]` | `dict.get(key)`, `re.search(pattern, s)` |
| "This can fail for real reasons" | `Result[T, E]` | `int.parse(s)`, `open(path)`, `json.loads(s)` |
| "I know this should work" | Exception (bug) | `list[i]`, `dict[key]` |

No dual APIs. `try expr` / `maybe expr` bridge .NET's exceptions/nullables into Result/Optional when calling .NET APIs directly. The stdlib lives in Sharpy's safety world; `try`/`maybe` handle the C#/.NET world.

---

## Phase 0: Documentation Pipeline

Every stdlib addition is wasted if the user can't discover it. This is the highest-ROI work and a prerequisite for everything else.

### 0a. Symbol Documentation Property

- Add `Documentation: string?` to base `Symbol` record
- Add `ParameterDocumentation` to `FunctionSymbol` / `ParameterSymbol`

### 0b. XML Doc Extraction

- Enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in Sharpy.Core csproj
- During `CachedModuleDiscovery`, extract `/// <summary>`, `<param>`, `<returns>`, `<example>` from the generated XML doc file
- Store on discovered symbols

### 0c. LSP Surfacing

- **Hover**: Doc summary + examples below the signature
- **Completion**: `CompletionItem.Documentation` field populated
- **Signature Help**: `ParameterInformation.Documentation` for each param

### 0d. XML Doc Quality Pass

Existing Sharpy.Core docs have `<summary>` and `<param>` but few have `<example>`. Add `<example>` blocks with Sharpy code snippets to all public builtins and collection methods — these show directly in hover tooltips.

```csharp
/// <summary>
/// Return the number of items in a container.
/// </summary>
/// <param name="obj">A list, dict, set, str, or any object with __len__.</param>
/// <returns>The number of items as a non-negative integer.</returns>
/// <example>
/// <code>
/// len([1, 2, 3])    # 3
/// len("hello")      # 5
/// len({})           # 0
/// </code>
/// </example>
```

**Result**: Every current and future Sharpy.Core function gets hover docs, completion docs, and param docs in the editor for free.

---

## Phase 1: Builtin Gaps

Functions every Python developer expects to "just work."

### 1a. Missing Builtins

| Function | Signature | .NET Backing |
|---|---|---|
| `hex(x)` | `int -> str` | `Convert.ToString(x, 16)` with `"0x"` prefix |
| `oct(x)` | `int -> str` | `Convert.ToString(x, 8)` with `"0o"` prefix |
| `bin(x)` | `int -> str` | `Convert.ToString(x, 2)` with `"0b"` prefix |
| `ascii(obj)` | `object -> str` | Escape non-ASCII chars to `\x`, `\u`, `\U` |
| `breakpoint()` | `-> None` | `System.Diagnostics.Debugger.Break()` |

### 1b. Builtin Parameter Completeness

These builtins exist but lack key parameters that are central to idiomatic Python usage:

| Function | Missing Parameters | Impact |
|---|---|---|
| `sorted(iterable)` | `key`, `reverse` | Very common: `sorted(items, key=lambda x: x.name)` |
| `min(iterable)` | `key`, `default` | `min([], default=0)` avoids exception on empty |
| `max(iterable)` | `key`, `default` | Same |
| `enumerate(iterable)` | `start` | `enumerate(items, start=1)` for 1-based indexing |
| `zip(*iterables)` | `strict` | Length-mismatch detection |
| `next(iterator)` | `default` | 2-arg form avoids StopIteration |
| `sum(iterable)` | `start` | Accumulator seed |
| `print(*args)` | `file` | Output redirection |
| `round(number)` | `ndigits=None` returning `int` | `round(3.7)` → `4` (int) |

---

## Phase 2: Standard Library Modules

All modules provide Pythonic APIs backed by .NET internally.

### Tier A — Low effort, high value

| Module | Key APIs | .NET Backing | Error Convention |
|---|---|---|---|
| **`string`** | `ascii_letters`, `digits`, `punctuation`, `whitespace` | Constants only | N/A (pure data) |
| **`time`** | `time()`, `sleep(secs)`, `perf_counter()`, `monotonic()`, `strftime()` | `Stopwatch`, `Thread.Sleep`, `DateTimeOffset` | `sleep` is void; `time()` is pure |
| **`copy`** | `copy(x)`, `deepcopy(x)` | `ICloneable`, serialization | `Result` if object isn't copyable |
| **`tempfile`** | `gettempdir()`, `mkdtemp()`, `NamedTemporaryFile()` | `Path.GetTempPath()`, `Path.GetTempFileName()` | `Result` for file creation |
| **`glob`** | `glob(pattern)`, `iglob(pattern)` | `Directory.EnumerateFiles` | Returns iterator (empty = no match) |
| **`shutil`** | `copy()`, `copytree()`, `rmtree()`, `move()`, `which()` | `System.IO` operations | `Result` for I/O ops; `which()` → `Optional` |
| **`textwrap`** | `wrap()`, `fill()`, `dedent()`, `indent()` | String manipulation | Pure functions, no errors |
| **`fnmatch`** | `fnmatch(name, pat)`, `filter(names, pat)` | Regex translation | Pure functions |

### Tier B — Medium effort, high value

| Module | Key APIs | .NET Backing | Error Convention |
|---|---|---|---|
| **`functools`** | `reduce()`, `partial()`, `lru_cache()`, `cache` | Manual impl; lambda support | `reduce` on empty → `Result` |
| **`hashlib`** | `md5()`, `sha256()`, `sha512()`, `.hexdigest()`, `.digest()` | `System.Security.Cryptography` | Pure (hash of bytes) |
| **`csv`** | `reader(file)`, `writer(file)`, `DictReader`, `DictWriter` | Manual or thin wrapper | `Result` for parse errors |
| **`io`** | `StringIO()`, `BytesIO()` | `StringReader/Writer`, `MemoryStream` | I/O ops → `Result` |
| **`logging`** | `getLogger()`, `basicConfig()`, `.info()`, `.warning()`, `.error()` | `Microsoft.Extensions.Logging` or custom | Logging never fails to caller |
| **`statistics`** | `mean()`, `median()`, `stdev()`, `variance()`, `mode()` | Math operations | Empty input → `Result` |
| **`bisect`** | `bisect_left()`, `bisect_right()`, `insort()` | `Array.BinarySearch` | Pure functions |
| **`heapq`** | `heappush()`, `heappop()`, `heapify()`, `nlargest()` | `PriorityQueue` or manual | `heappop` on empty → exception (bug) |

### Tier C — Pythonic wrappers over .NET subsystems

These are larger modules where .NET has excellent internal implementations. Sharpy wraps them with Python-familiar APIs.

| Module | Key APIs | .NET Backing | Error Convention |
|---|---|---|---|
| **`http`** | `http.get(url)`, `http.post(url, data)`, `Response` object | `System.Net.Http.HttpClient` | `Result[Response, HttpError]` |
| **`subprocess`** | `run(cmd)`, `Popen(cmd)`, `PIPE`, `STDOUT` | `System.Diagnostics.Process` | `Result[CompletedProcess, SubprocessError]` |
| **`threading`** | `Thread(target=func)`, `Lock()`, `Event()` | `System.Threading` | Thread creation → `Result` |
| **`socket`** | `socket()`, `.connect()`, `.send()`, `.recv()` | `System.Net.Sockets` | All I/O → `Result` |
| **`xml`** | `xml.etree.ElementTree` style API | `System.Xml.Linq` | Parsing → `Result` |
| **`urllib`** | `urllib.parse.urlparse()`, `urlencode()`, `quote()` | `System.Uri`, `WebUtility` | `urlparse` → `Result`, quote/encode pure |
| **`email`** | `email.message`, `email.mime` | `System.Net.Mail` | Construction pure, sending → `Result` |

### Intentionally Excluded

| Module | Reason |
|---|---|
| `inspect`, `ctypes`, `importlib` | Inherently dynamic — violates Axiom 3 |
| `pickle`, `shelve` | Security hazard; use `json` or protobuf |
| `tkinter`, `curses` | GUI/TUI — use .NET frameworks directly |
| `unittest` | Use .NET test frameworks (xUnit, NUnit) |
| `compile`, `exec`, `eval` | Dynamic execution — violates Axiom 3 |
| `vars`, `dir`, `getattr`, `setattr`, `delattr` | Reflection — not appropriate for static language |
| `abc` | Sharpy has `interface` |
| `dataclasses` | Sharpy has `struct` / `class` |
| `typing` | Sharpy has native static types |
| `enum` (module) | Sharpy has native `enum` keyword |
| `asyncio` | Sharpy uses `async`/`await` with .NET Task model directly |

---

## Phase 3: Collection & Type Completeness

### 3a. str Methods Audit

`str` maps to `System.String`, so methods are discoverable via CLR reflection — but the names differ. Decision needed: Python-name extension methods vs compiler aliasing.

| Python | .NET | Resolution |
|---|---|---|
| `.upper()` | `.ToUpper()` | Extension method or compiler alias |
| `.lower()` | `.ToLower()` | Same |
| `.strip()` | `.Trim()` | Same |
| `.split()` | `.Split()` | Needs Python semantics (no-arg splits on whitespace) |
| `.join(iterable)` | `String.Join()` | Different calling convention (`", ".join(list)`) |
| `.startswith()` | `.StartsWith()` | Direct map |
| `.find()` | `.IndexOf()` | Direct map |
| `.replace()` | `.Replace()` | Direct map |
| `.isdigit()`, `.isalpha()`, etc. | `char.IsDigit()` etc. | Extension methods |
| `.format()` | String interpolation | May already be handled by f-strings |
| `.encode()` | `Encoding.UTF8.GetBytes()` | Needs `bytes` type story |
| `.zfill()`, `.ljust()`, `.rjust()`, `.center()` | `PadLeft()`, `PadRight()` | Extension methods |

### 3b. Collection Method Gaps

Audit against Python 3.12:

- `dict.fromkeys()` — class method, needs static method support
- `list.copy()` — shallow copy
- `set.symmetric_difference()`, `.issubset()`, `.issuperset()` — verify completeness
- `dict | other` merge operator — verify

### 3c. bytes Type

`bytes` as a full type with `.decode()`, `.hex()`, slicing, indexing. Backed by `byte[]` or `ReadOnlyMemory<byte>`. This unlocks `hashlib`, `io.BytesIO`, binary file I/O.

---

## Phase 4: Module Completion (existing modules)

### datetime

- `timedelta` with arithmetic (`date + timedelta`)
- Timezone support via `DateTimeOffset` / `TimeZoneInfo`
- `strftime()` / `strptime()` formatting
- Return convention: `strptime()` → `Result[DateTime, ValueError]`

### pathlib

- Full `Path` API: `/` operator for joining, `.exists()`, `.is_file()`, `.is_dir()`, `.read_text()`, `.write_text()`, `.glob()`, `.iterdir()`, `.mkdir()`, `.unlink()`
- I/O operations → `Result`; queries like `.exists()` → `bool`

### collections

- `Counter` — very common (`Counter("mississippi")`)
- `defaultdict` — extremely common
- `deque` — double-ended queue, backed by `LinkedList<T>` or ring buffer
- `namedtuple` — evaluate overlap with Sharpy's `struct`

### argparse

- Audit current state, fill gaps to handle common CLI patterns

---

## Phase 5: Advanced Modules (Tier C)

The larger Pythonic wrappers over .NET subsystems. Each needs:

1. API design spec (which Python APIs to support, mapped to .NET internals)
2. Error convention decisions per operation
3. Full XML docs with examples

Priority order: `http` → `subprocess` → `urllib.parse` → `threading` → `socket` → `xml`

---

## Execution Order

| Order | Work | Rationale |
|---|---|---|
| **0** | Documentation pipeline (Symbol docs → LSP) | Multiplier for all future work |
| **1a** | `hex()`, `oct()`, `bin()` | Trivial, fills obvious gaps |
| **1b** | `sorted/min/max/enumerate/zip` param completeness | Most impactful for idiomatic usage |
| **2A** | `string`, `time`, `copy`, `tempfile`, `glob`, `shutil`, `textwrap`, `fnmatch` | Low-effort, daily-use |
| **3a** | str method audit + Python-name resolution | str is the most-used type |
| **3c** | `bytes` type | Unlocks hashlib, binary I/O, encode/decode |
| **4** | `Counter`, `defaultdict`, `deque`, datetime tz, pathlib completion | Common patterns + existing module gaps |
| **2B** | `functools`, `hashlib`, `csv`, `io`, `logging`, `statistics`, `bisect`, `heapq` | Higher effort, important |
| **5** | `http`, `subprocess`, `urllib.parse`, `threading` | Largest scope, Pythonic wrappers over .NET |
