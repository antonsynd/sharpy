# Contributing to Sharpy.Stdlib via `.spy` Source

This guide describes the workflow for authoring stdlib modules in Sharpy (`.spy`)
source files instead of writing C# directly.

## Overview

We are incrementally rewriting Sharpy's standard library modules from hand-written
C# into Sharpy source files. This serves two purposes:

1. **Dogfooding** — exercising the compiler on real, non-trivial code.
2. **Maintainability** — stdlib authors work in the same language users do.

The `.spy` file is the **authoring artifact**: it lives under source control and is
where edits should be made. The generated C# is the **deployment artifact**: it
ships in `Sharpy.Stdlib.dll`. Treat the generated C# as build output that happens
to be checked in (so consumers of `Sharpy.Stdlib` don't need the compiler), not as
something to edit by hand.

## Writing a `.spy` stdlib module

1. Place the source file under `src/Sharpy.Stdlib/spy/`, named after the module
   (e.g. `textwrap.spy`, `bisect.spy`).
2. Functions live at module scope — **not** inside a class. The compiler wraps
   them into a partial static class when emitting with `-t library`.
3. Use snake_case for function and parameter names. The compiler mangles them:
   - Functions: `snake_case` → `PascalCase` (e.g. `dedent` → `Dedent`,
     `fill_lines` → `FillLines`).
   - Single-word parameter names are preserved (`text` stays `text`).
   - Multi-word parameter names become camelCase (`max_width` → `maxWidth`).
4. The compiler automatically attaches `[SharpyModule("<filename>")]` to the
   generated class when `-t library` is passed. The module name is the lowercase
   filename stem (e.g. `textwrap.spy` → `[SharpyModule("textwrap")]`).
5. Module discovery maps PascalCase methods back to snake_case at import time, so
   user code keeps writing `textwrap.fill_lines(...)` regardless of the C# casing.

### Language features supported in stdlib `.spy`

- Generics with constraints, e.g. `def find[T: IComparable[T]](items: list[T], target: T) -> int`
- Default parameter values
- f-strings
- Generator functions (`yield`)
- Pattern matching (`match` / `case`)
- Optional and nullable types

## Generating C# from `.spy`

Use the regeneration script to regenerate all `.spy` → C# mappings at once:

```bash
bash build_tools/regenerate_spy_stdlib.sh           # Regenerate all in-place
bash build_tools/regenerate_spy_stdlib.sh --check    # Diff against committed (CI mode)
bash build_tools/regenerate_spy_stdlib.sh --dry-run  # Show what would be regenerated
```

The script uses **project compilation** (`sharpyc project stdlib.spyproj --emit-cs-to`)
to emit all modules in a single pass, then post-processes each output file:
- Prepends the `// Generated from ...` header comment
- Strips the auto-generated `[SharpyModule]` attribute (since `__Init__.cs` owns it)
- Strips `#line` directives (emitted by project compilation for source mapping)
- Normalizes trailing whitespace

CI runs `bash build_tools/check_spy_staleness.sh` (which delegates to
`regenerate_spy_stdlib.sh --check`) to catch drift between `.spy` sources and
committed C#.

### Adding a new module to the mapping

1. Add an entry to the `MODULES` array in `build_tools/regenerate_spy_stdlib.sh`:
   ```bash
   "mymodule:MyModule/MyModule.cs"
   ```
2. Run `bash build_tools/regenerate_spy_stdlib.sh` to generate the C#.
3. The CI staleness check picks it up automatically.

### Manual single-file emit (for debugging)

For one-off debugging, you can still emit a single file directly:

```bash
sharpyc emit csharp \
    src/Sharpy.Stdlib/spy/textwrap.spy \
    -t library -n Sharpy \
    -o src/Sharpy.Stdlib/Textwrap/Textwrap.cs
```

- `-t library` is **required**. Without it the emitter produces a `Main`-style
  program, not a library class with `[SharpyModule]`.
- `-n Sharpy` wraps the output in `namespace Sharpy { }` to match `__Init__.cs`.
- `-o` writes the generated C# directly to the deployment location.

> **Note:** Manual emit skips the header and `[SharpyModule]` stripping that the
> regeneration script handles. Always use the script for final regeneration.

## Native compilation (`.spy` → `.dll` directly)

The native pipeline compiles all `.spy` modules into a single assembly without
going through checked-in C# files:

```bash
sharpyc project src/Sharpy.Stdlib/spy/stdlib.spyproj
```

This produces `spy/bin/Debug/net10.0/Sharpy.Stdlib.Spy.dll` — a validation
artifact that proves the `.spy` sources can compile end-to-end through the
ProjectCompiler's 7-phase pipeline (Parse → Init → Declarations → Imports →
Semantic → CodeGen → Assembly).

### When to use native compilation vs C# emission

| Use Case | Command |
|----------|---------|
| Deploy to `Sharpy.Stdlib.dll` | `bash build_tools/regenerate_spy_stdlib.sh` |
| Validate all modules compile together | `sharpyc project stdlib.spyproj` |
| Debug a single module's codegen | `sharpyc emit csharp <file>.spy -t library -n Sharpy` |
| Compare native vs MSBuild API surface | Run `NativeStdlibCompilationTests` and `ApiSurfaceComparisonTests` |

### Why checked-in C# is still the deployment path

The generated C# files checked into the repo are what ships in `Sharpy.Stdlib.dll`.
This ensures consumers of the NuGet package or source reference don't need the
Sharpy compiler installed. The native pipeline validates correctness but doesn't
replace the deployment path.

Decision: keep checked-in C# for v1.0, plan removal when NuGet distribution of
the compiler is ready.

## Running tests

After regenerating a module, validate both layers:

```bash
# Unit tests (C#-level tests of the stdlib module)
dotnet test --filter "FullyQualifiedName~Textwrap"

# Integration tests (.spy programs that import the module)
dotnet test --filter "DisplayName~stdlib_textwrap"
```

All existing tests must pass — the generated C# is required to be a drop-in
replacement for the hand-written code it replaces.

> Prefer the `/run-tests` skill (or `.claude/scripts/dotnet-serialized test ...`)
> over raw `dotnet test`. See [`CLAUDE.md`](../../CLAUDE.md).

## Naming conventions summary

| Layer            | Convention                                                       |
| ---------------- | ---------------------------------------------------------------- |
| `.spy` source    | snake_case functions, snake_case parameters                      |
| Generated C#     | PascalCase methods, camelCase parameters                         |
| Module name      | lowercase, derived from filename (`textwrap.spy` → `textwrap`)   |
| Sharpy import    | snake_case (discovery maps PascalCase methods back to snake_case)|

## Modules that stay C#

The following modules cannot be rewritten in `.spy` due to compiler limitations
or architectural constraints. They remain hand-written C# permanently.

### Class-based modules (collections, argparse)

These modules export **classes** annotated with `[SharpyModuleType]`, not just
module-level functions. The `.spy` → library pipeline only emits static methods
into a partial class — it cannot produce standalone generic classes.

| Module | Classes | Blockers |
|--------|---------|----------|
| `collections` | `Deque<T>`, `Counter<T>`, `DefaultDict<TKey,TValue>`, `ChainMap<TKey,TValue>`, `OrderedDict<TKey,TValue>` | Generic constraints (`where T : notnull`), operator overloads, interface implementations, indexers |
| `argparse` | `ArgumentParser`, `Namespace`, `ArgumentGroup`, `MutuallyExclusiveGroup`, `SubparsersAction` | Complex class hierarchies, `params` arrays, builder-pattern methods |

### Multi-class modules with internal visibility (datetime, pathlib)

| Module | Classes | Blockers |
|--------|---------|----------|
| `datetime` | `Date`, `Time`, `DateTime`, `Timedelta`, `Timezone` | 5 interrelated classes with `internal` constructors and `InternalTimeSpan`/`InternalDateTime` properties for cross-class access (Sharpy has no `internal` visibility modifier). Complex operator overloading between different types (`DateTime + Timedelta → DateTime`). `internal static class DatetimeFormatHelper` with ~100-line strftime/strptime state machine. Multiple `[SharpyModuleType]` registrations. Per Axiom 1, keeping this in C# is correct. |
| `pathlib` | `Path` | `sealed class Path : IEquatable<Path>` with `/` operator overloading for path joining, private helper methods (`GlobMatch`, `GetEncoding`), `IEnumerable<Path>` returns via `yield return` (generators), `[SharpyModuleType("pathlib")]` registration. While technically rewritable, the class wraps `System.IO.Path` 1:1 — the `.spy` version would be identical logic with more verbose .NET import syntax. Diminishing returns. |

### Modules with complex CLR interop (glob, csv)

| Module | Blockers |
|--------|----------|
| `glob` | `out` parameters (not supported in Sharpy), `Array.Copy`, try/catch inside generators (`yield break` in catch), extensive char-level pattern matching, `SearchOption` enum member access |
| `csv` | Factory functions return types from the same assembly (circular dependency during regeneration), SCREAMING_SNAKE_CASE constants don't preserve casing through the name mangler (emit as PascalCase) |

### Class-heavy modules with deep CLR interop (json, re, requests, sqlite3, numpy)

Tier 4 modules evaluated in #694. These are class-heavy and deeply intertwined
with specific .NET APIs — rewriting in `.spy` would produce identical delegation
with no logic gain.

| Module | Classes | Blockers |
|--------|---------|----------|
| `json` | `JSONEncoder`, `JSONDecoder`, `JSONDecodeError`, internal `JsonParser`, `JsonSerializer` | Custom recursive descent parser, `#if NET10_0_OR_GREATER`, generic `Loads<T>`, `object?` boxing |
| `re` | `RePattern`, `ReMatch`, `ReError`, internal `RePatternTranslator` | Char-by-char FSM regex translator, `sealed`/`internal` constructors, lazy properties |
| `requests` | `Response`, `Session`, 4 exception classes | Async-to-sync bridge (`.GetAwaiter().GetResult()`), `HttpClient` singleton, `CancellationTokenSource` |
| `sqlite3` | `Sqlite3Connection`, `Sqlite3Cursor`, `Sqlite3Row`, 6 exception classes | `IDisposable`, NuGet assembly interop (`Microsoft.Data.Sqlite`), `internal` constructor coupling |
| `numpy` | `NdArray<T>` (10 partial files), `Numpy` (7 partial files) | 4,631 LOC generic numerics, `MathNet.Numerics` NuGet dep, `Span<T>`, broadcasting algorithms |

### Partially rewritten modules (hashlib)

`hashlib` factory functions (`md5()`, `sha1()`, `sha256()`, `sha384()`, `sha512()`)
are written in `.spy`. The `HashObject` class stays in C# because it requires
`Func<HashAlgorithm>` lambdas and `System.Security.Cryptography` imports not
available through the spy compilation pipeline.

### When can these be revisited?

- **collections/argparse**: When the compiler supports class declarations with
  `[SharpyModuleType]` in library mode (tracked as a future capability, not
  currently planned).
- **glob**: When the compiler adds `out` parameter support, or when the module
  is restructured to avoid them.
- **csv**: When the regeneration pipeline supports cross-type references within
  the same project, or when backtick escaping correctly preserves
  SCREAMING_SNAKE_CASE for module-level variable declarations.
- **hashlib (full)**: When the `.spy` pipeline supports class definitions and
  `system.security.cryptography` is added to the namespace map.
- **json/re/requests/sqlite3/numpy**: Unlikely regardless of pipeline
  improvements — their value proposition doesn't improve with class support.

## Known limitations (as of 2026-05)

These are tracked rough edges to be aware of when porting:

- **`IList[T]` indexing is not supported** in `.spy`. Use `list[T]` for
  parameters and locals; convert at the boundary if you need to interoperate
  with a `.NET` `IList`.
- **Generic constraints with `IComparable[T]`** work under both single-file
  emit and project compilation. The project-compilation namespace resolution
  issues documented previously have been resolved.
- **Cross-namespace module imports** require matching root namespaces. Stdlib
  `.spy` modules must target the `Sharpy` root namespace so they coexist with
  the rest of `Sharpy.Stdlib`.

## Module migration checklist

When porting a module from C# to `.spy`:

- [ ] Write the `.spy` equivalent under `src/Sharpy.Stdlib/spy/<module>.spy`.
- [ ] Verify Python reference behavior before implementing semantics:
      `python3 -c "import <module>; ..."`.
- [ ] Add the module to the `MODULES` array in `build_tools/regenerate_spy_stdlib.sh`.
- [ ] Run `bash build_tools/regenerate_spy_stdlib.sh` to generate C#.
- [ ] Compare public signatures of the generated C# against the original
      implementation — they must match.
- [ ] Replace the original hand-written `.cs` file with the generated code.
- [ ] Keep `__Init__.cs` with the `[SharpyModule]` partial class declaration.
- [ ] Run `bash build_tools/regenerate_spy_stdlib.sh --check` to verify staleness check passes.
- [ ] Run unit tests: `dotnet test --filter "FullyQualifiedName~<Module>"`.
- [ ] Run integration tests: `dotnet test --filter "DisplayName~stdlib_<module>"`.
- [ ] Commit with a message referencing issue #676.
