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

Single-file emit is the workflow for stdlib rewrites:

```bash
sharpyc emit csharp \
    src/Sharpy.Stdlib/spy/textwrap.spy \
    -t library \
    -o src/Sharpy.Stdlib/Textwrap/Textwrap.cs
```

- `-t library` is **required**. Without it the emitter produces a `Main`-style
  program, not a library class with `[SharpyModule]`.
- `-o` writes the generated C# directly to the deployment location.
- After regeneration, diff the new C# against the previous version and against
  the original hand-written implementation. Public signatures should match so
  the file is a drop-in replacement.

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

## Known limitations (as of 2026-05)

These are tracked rough edges to be aware of when porting:

- **`IList[T]` indexing is not supported** in `.spy`. Use `list[T]` for
  parameters and locals; convert at the boundary if you need to interoperate
  with a `.NET` `IList`.
- **Generic constraints with `IComparable[T]`** work cleanly under single-file
  emit (`sharpyc emit csharp ... -t library`) but may surface namespace
  resolution issues under full project compilation. Single-file emit is the
  recommended path for stdlib rewrites until the project-compilation case is
  fixed.
- **Cross-namespace module imports** require matching root namespaces. Stdlib
  `.spy` modules must target the `Sharpy` root namespace so they coexist with
  the rest of `Sharpy.Stdlib`.

## Module migration checklist

When porting a module from C# to `.spy`:

- [ ] Write the `.spy` equivalent under `src/Sharpy.Stdlib/spy/<module>.spy`.
- [ ] Verify Python reference behavior before implementing semantics:
      `python3 -c "import <module>; ..."`.
- [ ] Generate C#:
      `sharpyc emit csharp src/Sharpy.Stdlib/spy/<module>.spy -t library -o src/Sharpy.Stdlib/<Module>/<Module>.cs`.
- [ ] Compare public signatures of the generated C# against the original
      implementation — they must match.
- [ ] Replace the original hand-written `.cs` file with the generated code.
- [ ] Keep `__Init__.cs` if it contains the `[SharpyModule]` partial class
      declaration or other state that the generator does not emit.
- [ ] Run unit tests: `dotnet test --filter "FullyQualifiedName~<Module>"`.
- [ ] Run integration tests: `dotnet test --filter "DisplayName~stdlib_<module>"`.
- [ ] Commit with a message referencing issue #676.
