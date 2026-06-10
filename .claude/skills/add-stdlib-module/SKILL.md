---
name: add-stdlib-module
description: Scaffold a new Sharpy.Stdlib module (spy-sourced or handwritten C#) with all required files, conventions, docs, and tests
---

# Add Stdlib Module

Scaffold a new standard library module in `src/Sharpy.Stdlib/`. Reference example: **Zoneinfo** (spy-sourced).

## Step 0: Decide the implementation kind

- **spy-sourced** (preferred for pure logic expressible in Sharpy): write `spy/<name>_module.spy`, C# is generated.
- **handwritten C#** (needed for heavy .NET interop, NuGet deps, or unsafe/perf-critical code): write C# directly in `<Module>/`.

Check `python3 -c "import <name>; help(<name>)"` and **verify Python behavior first** before designing the API.

## Step 1: Files to create

### Both kinds

| File | Content |
|------|---------|
| `src/Sharpy.Stdlib/<Module>/__Init__.cs` | `[SharpyModule("<name>")] public static partial class <Module> { }` in `namespace Sharpy` |
| `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.<Module>.csproj` | `<AssemblyName>` + `<Compile Include="../<Module>/**/*.cs" />` + `ProjectReference`s to other module csprojs it depends on |

No compiler registration needed — `ModuleRegistry.LoadReference()` discovers modules at runtime via the `[SharpyModule]` attribute.

### spy-sourced only

1. Create `src/Sharpy.Stdlib/spy/<name>_module.spy` (auto-included by `spy/stdlib.spyproj` glob)
2. Add an entry to the `MODULES` array in `build_tools/regenerate_spy_stdlib.sh`:
   ```
   "<name>_module:<Module>/<Module>.cs"
   ```
3. Generate the C#: `bash build_tools/regenerate_spy_stdlib.sh`
4. Never hand-edit the generated C# — CI fails via `check_spy_staleness.sh` if it drifts from the `.spy` source

## Step 2: API conventions (mandatory)

| Scenario | Return |
|----------|--------|
| Absence | `T?` nullable — NOT `Optional<T>` |
| Expected failure | `Result<T, E>` with a typed error class |
| Bugs | throw Python-named exception |

- Public signatures use `Sharpy.List<T>` / `Dict<K,V>` / `Set<T>`, never raw `System.Collections.Generic`
- No dual throwing/Result APIs for the same function
- Optional params: `T? = null` (C#) / `T | None = None()` (`.spy` — use `T | None`, not `T?`, per #804)
- Handwritten C# must compile under **C# 9.0** for the `netstandard2.1` target (`#if NET10_0_OR_GREATER` for newer paths)

## Step 3: Docs (generated — never hand-edit)

```bash
python -m build_tools stdlib generate --force
```

## Step 4: Tests

- Unit tests in `src/Sharpy.Stdlib.Tests/`
- Integration fixtures via `/add-test-fixture`: `stdlib_<name>_*.spy` + `.expected` in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`

## Step 5: Verify

```bash
.claude/scripts/dotnet-serialized build sharpy.sln
.claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~<Module>" --no-build
bash build_tools/regenerate_spy_stdlib.sh --check   # spy-sourced only
```

Read output from `.claude/tmp/dotnet-serialized-latest.log`. Finally, update the module count/list in `CLAUDE.md` (Architecture table + Sharpy.Stdlib section).
