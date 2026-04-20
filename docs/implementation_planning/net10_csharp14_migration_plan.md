# .NET 10 / C# 14 Migration Plan

**Status:** Planning
**Target:** Post-Unity 6.8 CoreCLR migration (expected late 2026)
**Scope:** Lift Sharpy.Core from `netstandard2.1` / C# 9.0 to `net10.0` / C# 14

## Context

Unity 6.8 replaces Mono with CoreCLR and ships a .NET 10 toolchain. The only supported
TFM for precompiled assemblies will be .NET Standard 2.1 (backward compat) and .NET 10
(full access). This removes the rationale for Sharpy.Core's current floor of
`netstandard2.0` + C# 9.0.

The migration spans 5 C# language versions (10-14) and ~7 .NET runtime releases (5-10)
worth of APIs that Sharpy currently cannot use.

### Current State

| Component | TFM | LangVersion |
|-----------|-----|-------------|
| Sharpy.Core | `netstandard2.1;netstandard2.0` | 9.0 |
| Sharpy.Compiler | `net10.0` | latest |
| Sharpy.Cli | `net10.0` | latest |
| Sharpy.Lsp | `net10.0` | latest |
| Generated C# output | targets Sharpy.Core | constrained to C# 9.0 |

### Target State

| Component | TFM | LangVersion |
|-----------|-----|-------------|
| Sharpy.Core | `net10.0;netstandard2.1` | 14 (net10.0), 9.0 (netstandard2.1) |
| Sharpy.Compiler | `net10.0` | latest (no change) |
| Sharpy.Cli | `net10.0` | latest (no change) |
| Sharpy.Lsp | `net10.0` | latest (no change) |
| Generated C# output | targets Sharpy.Core | C# 14 when targeting net10.0 |

### Guiding Principles

1. **Multi-target with `netstandard2.1` fallback** — Sharpy.Core keeps backward compat
   via conditional compilation (`#if NET10_0_OR_GREATER`). Older Unity/embedded scenarios
   still work.
2. **Generated code adapts to target** — The RoslynEmitter detects the output target and
   emits modern C# when available, falling back to C# 9.0 patterns otherwise.
3. **Phases are independently shippable** — Each phase produces a releasable state. No
   phase depends on a later phase for correctness.
4. **Axiom precedence unchanged** — .NET > Type Safety > Python Syntax. New features
   must earn their complexity.

---

## Phase 0: Foundation — Multi-Target Sharpy.Core

**Goal:** Add `net10.0` target to Sharpy.Core alongside `netstandard2.1`. Drop
`netstandard2.0`. Establish conditional compilation infrastructure.

### Tasks

- [ ] Change `Sharpy.Core.csproj` TargetFrameworks to `net10.0;netstandard2.1`
- [ ] Remove `netstandard2.0` target (Unity 6.8 requires at minimum netstandard2.1)
- [ ] Remove `Microsoft.Bcl.Memory` polyfill (only needed for netstandard2.0)
- [ ] Add `LangVersion` conditional: 14 for net10.0, 9.0 for netstandard2.1
- [ ] Verify all 10,218+ tests pass on both targets
- [ ] Update CI to matrix-test both TFMs
- [ ] Update `CLAUDE.md` and `deferred_features.md` to reflect new constraints

### Breaking Changes

- Drop `netstandard2.0` support. Projects on .NET Core 2.0 or Unity 2018 can no longer
  use Sharpy.Core. This is acceptable — those runtimes are EOL.

### Estimated Effort

Small. Primarily project file and CI changes.

---

## Phase 1: Sharpy.Core Modernization — BCL Upgrades

**Goal:** Replace hand-rolled implementations with .NET BCL types where available on
`net10.0`, keeping `netstandard2.1` fallbacks.

### 1.1 FrozenSet — Use `System.Collections.Frozen`

Currently `FrozenSet<T>` wraps `ImmutableHashSet<T>` with this comment:
> "Does NOT use System.Collections.Frozen (requires .NET 8+) or IReadOnlySet (requires .NET 5+)."

**Change:**
```csharp
#if NET10_0_OR_GREATER
    // Delegate to System.Collections.Frozen.FrozenSet<T> — vectorized read path
    private readonly System.Collections.Frozen.FrozenSet<T> _set;
#else
    private readonly ImmutableHashSet<T> _set;
#endif
```

Also implement `IReadOnlySet<T>` on net10.0 (available since .NET 5).

### 1.2 datetime.date / datetime.time — Use `DateOnly` / `TimeOnly`

Currently `Sharpy.Date` wraps `System.DateTime` (semantically wrong — carries unused time
component). `Sharpy.Time` also wraps `System.DateTime`.

**Change:**
```csharp
#if NET10_0_OR_GREATER
    private readonly DateOnly _date;  // .NET 6+
#else
    private readonly System.DateTime _date;
#endif
```

Same for `Time` → `TimeOnly`. Expose conversion methods between Sharpy and BCL types.

### 1.3 random — Use `Random.Shared`

Currently manages `private static System.Random _random = new System.Random()` with a
manual lock.

**Change:** On net10.0, use `System.Random.Shared` (thread-safe, no lock needed) for the
unseeded case. Keep `Seed()` support via a separate instance.

### 1.4 heapq — Evaluate `PriorityQueue<T,P>`

Currently implements min-heap manually via `Heappush`/`Heappop` on `IList<T>`.
`PriorityQueue<T,P>` (.NET 6+) has a different API shape (element + priority pairs) that
doesn't map 1:1 to Python's heapq (single-key comparison). Evaluate whether it simplifies
internals or whether the manual heap remains simpler. **May be a no-op.**

### 1.5 Encoding fix — `iso-8859-1`

Four call sites use `Encoding.GetEncoding("iso-8859-1")` which will throw on .NET 10
without registering the CodePages provider:
- `Pathlib/Path.cs:580`
- `StringExtensions.Format.cs:552`
- `Open.cs:71`
- `Bytes.cs:143`

**Change:** Add a module initializer (or static constructor) that calls
`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` on net10.0. On
netstandard2.1 the encodings are available by default.

### 1.6 json — Source-generated serialization

Sharpy.Core's `json` module uses `System.Text.Json` via reflection-based serialization.
On net10.0, add `[JsonSerializable]` source generators for AOT/IL2CPP compatibility in
Unity. Also adopt new .NET 10 options: `JsonSerializerOptions.RespectNullableAnnotations`,
duplicate property detection.

### 1.7 Minor BCL upgrades

| Current | net10.0 replacement | Files |
|---------|---------------------|-------|
| Manual `IEquatable<T>` + `GetHashCode` | `HashCode.Combine()` (.NET Core 2.1+) | Multiple |
| `string.Contains(char)` polyfills | Native (available since .NET Core 2.1) | StringExtensions |
| Manual `Span<T>` workarounds | First-class span support | Slicing code |
| `CompositeFormat` for repeated formatting | Pre-parsed format strings (.NET 8+) | `str.format()` |

### Estimated Effort

Medium. Each subsection is independent and can be a separate PR.

---

## Phase 2: Codegen Modernization — Emitting Modern C#

**Goal:** When the output target is net10.0, emit modern C# constructs that produce
cleaner, smaller, faster code.

### 2.1 Collection expressions (`[1, 2, 3]`)

**C# 12.** Currently Sharpy list literals emit:
```csharp
new Sharpy.List<int>(new int[] { 1, 2, 3 })
```

On C# 14 target, emit:
```csharp
new Sharpy.List<int>([1, 2, 3])
```

Requires `Sharpy.List<T>` to support construction from `ReadOnlySpan<T>` (collection
expression target).

**Spread operator:** Sharpy's `[*a, *b]` unpacking could map to C# 12's `[..a, ..b]`
spread element.

### 2.2 Primary constructors

**C# 12.** Currently `__init__` emits a full constructor body:
```csharp
class Foo
{
    private int _x;
    public Foo(int x) { _x = x; }
}
```

For simple classes where `__init__` only assigns parameters to fields, emit:
```csharp
class Foo(int x)
{
    private int _x = x;
}
```

**Constraint:** Only apply when `__init__` body is pure assignment. Complex `__init__`
methods still emit full constructors.

### 2.3 Record structs for `@dataclass struct`

**C# 10.** Currently `@dataclass` on a struct emits manual `Equals`, `GetHashCode`,
`ToString`, `operator==`, `operator!=`, `Deconstruct`. This is ~50 lines of boilerplate.

On C# 14 target, emit `record struct Foo(int X, string Y)` — the compiler generates all
of these automatically, correctly, and efficiently.

### 2.4 File-scoped namespaces

**C# 10.** Emit `namespace Foo;` instead of `namespace Foo { ... }`. Reduces indentation
of all generated code by one level. Pure cosmetic but significantly improves readability
of emitted C#.

### 2.5 Global usings

**C# 10.** Emit a single `global using Sharpy;` and `global using static Sharpy.Builtins;`
in a generated `_GlobalUsings.cs` file instead of repeating these in every compilation
unit.

### 2.6 `field` keyword in properties

**C# 14.** Currently properties with custom setters emit explicit backing fields:
```csharp
private string _name;
public string Name { get => _name; set => _name = value ?? throw ...; }
```

Emit instead:
```csharp
public string Name { get; set => field = value ?? throw ...; }
```

### 2.7 Target-mode detection

Add a `CodeGenTarget` enum to the compiler:
```
CodeGenTarget.NetStandard21  →  C# 9.0 patterns (current behavior)
CodeGenTarget.Net10          →  C# 14 patterns (new behavior)
```

The CLI flag `--target net10.0` (or detection from `.spyproj`) selects the mode. Default
remains `NetStandard21` until Unity 6.8 is widely adopted, then flips to `Net10`.

### Estimated Effort

Large. Touches RoslynEmitter extensively. Each subsection is independently implementable
but 2.7 (target-mode) should be done first as infrastructure.

---

## Phase 3: Language Features — Formerly Deferred

**Goal:** Implement Sharpy language features that were explicitly deferred pending C# 11+
or .NET 7+ support.

### 3.1 User-defined compound assignment (`__iadd__`, `__isub__`, etc.)

**Unblocked by:** C# 14 user-defined compound assignment operators.

Currently documented as unsupported in `assignment_operators.md`:
> "user definitions of assignment operators like `+=` via dunder methods (e.g. `__iadd__`)
> are not supported."

**Sharpy syntax:**
```python
class Vector:
    x: float
    y: float

    def __iadd__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

v1 += v2  # calls __iadd__
```

**Emits:** C# 14 user-defined `operator +=`.

**Dunders to support:** `__iadd__`, `__isub__`, `__imul__`, `__itruediv__`, `__ifloordiv__`,
`__imod__`, `__ipow__`, `__iand__`, `__ior__`, `__ixor__`, `__ilshift__`, `__irshift__`.

### 3.2 Extension properties and operators

**Unblocked by:** C# 14 extension members.

Currently Sharpy can consume .NET extension methods but cannot define extension properties
or operators. C# 14's `extension` blocks enable:

**Sharpy syntax (proposed):**
```python
@extension(list[int])
def sum(self) -> int:
    total = 0
    for item in self:
        total += item
    return total

@extension(list[int])
@property
def is_empty(self) -> bool:
    return self.len() == 0
```

**Emits:** C# 14 `extension(IEnumerable<int>) { ... }` blocks.

**Design decision needed:** Whether to use a decorator (`@extension`) or a dedicated
syntax block. Decorator approach is more Pythonic; dedicated block is more explicit.

### 3.3 List patterns in `match`

**Unblocked by:** C# 11 list patterns.

**Sharpy syntax:**
```python
match items:
    case []:
        print("empty")
    case [first]:
        print(f"single: {first}")
    case [first, second, *rest]:
        print(f"first={first}, second={second}, rest has {rest.len()} items")
```

**Emits:** C# 11 list patterns: `case []: ...`, `case [var first, .., var rest]: ...`

**Scope:** Requires Parser changes (new pattern AST nodes), Semantic analysis (type
inference for destructured elements), Validation (exhaustiveness for list patterns),
CodeGen (Roslyn list pattern syntax), and LSP updates.

### 3.4 Generic math / Numeric protocols

**Unblocked by:** C# 11 static abstract interface members + .NET 7 `INumber<T>`.

**Sharpy syntax:**
```python
def sum[T: Numeric](items: list[T]) -> T:
    total: T = T(0)
    for item in items:
        total += item
    return total
```

**Emits:** `where T : INumber<T>` constraint.

**Mapping:**

| Sharpy protocol | .NET interface |
|-----------------|----------------|
| `Numeric` | `INumber<T>` |
| `Addable` | `IAdditionOperators<T,T,T>` |
| `Comparable` | `IComparisonOperators<T,T,bool>` |
| `FloatingPoint` | `IFloatingPoint<T>` |
| `Integer` | `IBinaryInteger<T>` |

**Design decision needed:** Whether to expose the full .NET generic math interface
hierarchy or define simplified Sharpy-side protocols that map to them.

### 3.5 `required` members

**Unblocked by:** C# 11 `required` keyword + .NET 7 runtime support.

**Sharpy syntax:**
```python
class Config:
    name: str          # required — no default, must be set
    retries: int = 3   # optional — has default

c = Config(name="prod")  # OK
c = Config()              # Error: 'name' is required
```

Fields without defaults are already enforced by `__init__`, but `required` enables
enforcement at the C# level for interop scenarios where .NET code constructs Sharpy types.

**Emits:** `required` modifier on properties/fields.

### 3.6 `@file` access modifier

**Unblocked by:** C# 11 file-local types.

For types that should not be visible outside their compilation unit (internal helpers,
generated adapters), Sharpy could support:

```python
@file
class _Helper:
    ...
```

**Emits:** `file class Helper { ... }`

Low priority — mainly useful for generated code hygiene.

### Estimated Effort

Very large. Each feature is a full pipeline implementation (Lexer → Parser → Semantic →
Validation → CodeGen → LSP → Tests). Can be done in parallel across features.

---

## Phase 4: Performance — Span-Based Internals

**Goal:** Use `Span<T>` and related types internally in Sharpy.Core for zero-copy
operations on net10.0.

### 4.1 `params ReadOnlySpan<T>` for variadic functions

**C# 13.** Sharpy's `*args` currently emits `params T[]`, allocating an array on every
call. On net10.0, emit `params ReadOnlySpan<T>` for stack-allocated variadics.

**Affected areas:** `print()`, `str.format()`, any user-defined `*args` function.

### 4.2 Span-based string slicing

Sharpy string slicing (`s[1:5]`) currently creates a new `string` via `Substring()`. On
net10.0, internal operations that don't escape the method could use
`ReadOnlySpan<char>` / `AsSpan()` with implicit span conversions (C# 14).

### 4.3 Span-based list slicing

Sharpy list slicing (`lst[1:5]`) currently copies elements into a new `Sharpy.List<T>`.
For read-only access patterns, provide a `Span<T>` view of the underlying storage.

### 4.4 `SearchValues<T>` for string operations

**(.NET 8+)** Sharpy.Core string methods like `str.strip()`, `str.split()` with character
sets could use `SearchValues<T>` for vectorized multi-character search.

### Estimated Effort

Medium. Primarily Sharpy.Core changes with `#if NET10_0_OR_GREATER` guards. No language
changes needed.

---

## Phase 5: Unity Integration

**Goal:** Ensure Sharpy works correctly in Unity 6.8 with CoreCLR.

### 5.1 Static field lifecycle

Sharpy module-level variables compile to static fields. Unity 6.8's code reload does not
reset static variables. Options:

**Option A (codegen):** Emit `[AutoStaticsCleanup]` on generated module classes.
**Option B (documentation):** Document that Sharpy module-level state persists across
play mode transitions and users must use Unity lifecycle callbacks.
**Option C (hybrid):** Emit `[AutoStaticsCleanup]` by default, with a compiler flag
to disable it for non-Unity targets.

**Recommendation:** Option C — automatic for Unity, no overhead elsewhere.

### 5.2 Assembly loading compatibility

Sharpy's `ProjectCompiler` loads assemblies dynamically. Verify compatibility with Unity
6.8's restricted `Assembly.Load` APIs. May need to use
`UnityEngine.Assemblies.CurrentAssemblies.LoadFromPath()` in Unity contexts.

### 5.3 IL2CPP / AOT compatibility

For Unity builds using IL2CPP (AOT), ensure Sharpy.Core avoids:
- `System.Reflection.Emit` (dynamic code generation)
- Unbounded generic instantiation
- `MakeGenericType` / `MakeGenericMethod` at runtime

Audit Sharpy.Core's `json` module (reflection-based serialization) and `copy` module
(deep copy via reflection) for IL2CPP compatibility. Source-generated alternatives
(Phase 1.6) address the JSON case.

### 5.4 IEEE 754 floating point

Document that floating-point results may differ slightly between Mono-based Unity
(pre-6.8) and CoreCLR-based Unity (6.8+). Sharpy maps `float` → `double`, so this
affects all Sharpy numeric computation in Unity.

### 5.5 Unity package

Create a `.unitypackage` or UPM package for Sharpy.Core targeting Unity 6.8+. Include:
- Sharpy.Core.dll (net10.0 build)
- Integration guide
- Sample scripts

### Estimated Effort

Medium. Mix of codegen changes, testing, and documentation.

---

## Phase 6: Cleanup and Finalization

### 6.1 Update `deferred_features.md`

Remove all features that have been implemented. Document any features that remain deferred
with updated rationale.

### 6.2 Update CLAUDE.md

Reflect new TFMs, C# version, and codegen capabilities.

### 6.3 Update language specification

Update spec documents for all new features (compound assignment operators, extension
members, list patterns, generic math protocols, required members).

### 6.4 Deprecation of netstandard2.1 path

Once Unity 6.8 is widely adopted (estimated mid-2027), evaluate dropping the
`netstandard2.1` target entirely to eliminate conditional compilation complexity.

---

## Dependency Graph

```
Phase 0 (Foundation)
  │
  ├──> Phase 1 (Core Modernization)    ──> Phase 4 (Span Performance)
  │                                              │
  ├──> Phase 2 (Codegen Modernization)           │
  │       │                                      │
  │       └──> Phase 3 (Language Features)       │
  │                    │                         │
  │                    └─────────┬───────────────┘
  │                              │
  │                              v
  └──────────────────────> Phase 5 (Unity Integration)
                                 │
                                 v
                           Phase 6 (Cleanup)
```

- Phase 0 is prerequisite for all others
- Phases 1, 2 can proceed in parallel after Phase 0
- Phase 3 depends on Phase 2 (needs target-mode infrastructure from 2.7)
- Phase 4 depends on Phase 1 (needs net10.0 Sharpy.Core target)
- Phase 5 depends on Phases 1-4 being substantially complete
- Phase 6 is finalization after all features land

---

## Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| Unity 6.8 ships later than expected | Plan timeline shifts | Phases 0-4 are Unity-independent; only Phase 5 requires Unity 6.8 |
| netstandard2.1 conditional compilation complexity | Maintenance burden, bugs | Strict `#if` patterns, CI matrix testing both TFMs |
| Codegen target detection unreliable | Wrong C# emitted | Default to conservative (C# 9.0); require explicit opt-in for C# 14 |
| Generic math API surface too large | Feature creep, incomplete impl | Start with `Numeric` protocol only, expand based on demand |
| IL2CPP strips Sharpy.Core types | Runtime failures in Unity builds | Add `[Preserve]` attributes, provide link.xml configuration |
| Roslyn version mismatch | Emitter can't produce C# 14 syntax | Sharpy.Compiler already uses Roslyn 5.3.0 which supports C# 14 |

---

## Feature Inventory

Complete list of features unlocked by the migration, organized by source.

### From C# 10 (.NET 6)

| Feature | Use in Sharpy | Phase |
|---------|---------------|-------|
| Record structs | `@dataclass struct` codegen | 2.3 |
| File-scoped namespaces | Cleaner emitted C# | 2.4 |
| Global usings | Single import file | 2.5 |
| Extended property patterns | Nested `match` patterns | 3.3 |
| Const interpolated strings | Sharpy.Core constants | 1.7 |
| Lambda natural types | Cleaner lambda codegen | 2.2 |

### From C# 11 (.NET 7)

| Feature | Use in Sharpy | Phase |
|---------|---------------|-------|
| List patterns | `match` with `[head, *tail]` | 3.3 |
| Generic math / static abstract interfaces | `Numeric` protocol | 3.4 |
| Required members | Non-optional field enforcement | 3.5 |
| Raw string literals | Codegen readability | 2 (minor) |
| UTF-8 string literals | `bytes` type optimization | 1.7 |
| File-local types | `@file` modifier | 3.6 |
| Generic attributes | Typed decorators | Future |
| `nint`/`nuint` aliases | Native integer interop | Future |
| Span pattern matching | String match optimization | 4 |

### From C# 12 (.NET 8)

| Feature | Use in Sharpy | Phase |
|---------|---------------|-------|
| Primary constructors | `__init__` codegen | 2.2 |
| Collection expressions | List/set/dict literal codegen | 2.1 |
| Default lambda parameters | Lambda default args | Future |
| Type aliases (`using X = ...`) | `type` alias codegen | Future |
| Inline arrays | Fixed-size buffer optimization | 4 |

### From C# 13 (.NET 9)

| Feature | Use in Sharpy | Phase |
|---------|---------------|-------|
| `params` collections (Span) | Zero-alloc `*args` | 4.1 |
| `\e` escape sequence | String literal support | 1.7 |
| Partial properties | Sharpy.Core internal split | 1 (minor) |
| `ref struct` interfaces | Span protocol impl | 4 |
| `Lock` type | Thread-safe module state | Future |

### From C# 14 (.NET 10)

| Feature | Use in Sharpy | Phase |
|---------|---------------|-------|
| Extension members | Extension properties/operators | 3.2 |
| `field` keyword | Property codegen | 2.6 |
| Null-conditional assignment | `obj?.prop = value` | 3 (minor) |
| User-defined compound assignment | `__iadd__` etc. | 3.1 |
| Implicit Span conversions | Internal perf | 4 |

### From .NET BCL (runtime APIs)

| API | Use in Sharpy | Phase |
|-----|---------------|-------|
| `DateOnly` / `TimeOnly` (.NET 6) | `datetime.date` / `datetime.time` | 1.2 |
| `PriorityQueue<T,P>` (.NET 6) | `heapq` module (evaluate) | 1.4 |
| `Random.Shared` (.NET 6) | `random` module | 1.3 |
| `IReadOnlySet<T>` (.NET 5) | `FrozenSet` interface | 1.1 |
| `System.Collections.Frozen` (.NET 8) | `FrozenSet` backing store | 1.1 |
| `SearchValues<T>` (.NET 8) | String search vectorization | 4.4 |
| `INumber<T>` (.NET 7) | Generic math protocols | 3.4 |
| `Half` (.NET 5) | `float16` type (future) | Future |
| `HashCode.Combine` (.NET Core 2.1) | Simpler hash implementations | 1.7 |
| `CompositeFormat` (.NET 8) | `str.format()` perf | 1.7 |
| `JsonSerializer` source gen (.NET 6) | AOT-safe JSON | 1.6 |
| `TimeProvider` (.NET 8) | Testable `time` module | Future |
| `System.Text.Json` .NET 10 options | Strict JSON parsing | 1.6 |
| `Encoding.CodePages` (.NET 10 default change) | Fix iso-8859-1 breakage | 1.5 |
