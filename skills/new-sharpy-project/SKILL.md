---
name: new-sharpy-project
description: Creates a new Sharpy project. Use when starting a new Sharpy (.spy) project from scratch, setting up the sharpyc toolchain, adding a .spyproj file to existing Sharpy sources, or when the user wants to begin a Sharpy program or library.
---

When the user wants to create a new project, first infer as many options as
possible from the request ("a script" implies single-file, "an app/library
with modules" implies a `.spyproj` project, "called foo" means name=foo). Then
use a structured multiple-choice prompt (not plain text) to gather only the
**remaining unspecified** options in a single interaction. The options:

- **Project name**: ask if unspecified.
- **Shape**: single `.spy` file (no project file needed — `sharpyc run`
  auto-discovers sibling imports) or a multi-file `.spyproj` project.
- **Output type** (only for `.spyproj`): executable (`exe`) or `library`.

Write the Sharpy source itself following the `sharpy-syntax` skill — Sharpy is
not Python; plain Python will not compile.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — any 10.x version
  (there is no `global.json` pin). `sharpyc run` executes programs via
  `dotnet`, so the SDK must be on `PATH`.

## Getting `sharpyc`

> [!NOTE]
> `dotnet tool install -g sharpyc` does **not** work — sharpyc is not
> published as a dotnet tool. Use one of these instead:

**GitHub release archive** (self-contained, no build):
download `sharpyc-<rid>.tar.gz`/`.zip` for your platform
(`osx-arm64`, `osx-x64`, `win-x64`, `linux-x64`, `linux-arm64`) from
<https://github.com/antonsynd/sharpy/releases>, extract, and put `sharpyc` on
`PATH`.

**From source** (needs the .NET 10 SDK):

```bash
git clone https://github.com/antonsynd/sharpy.git
cd sharpy
dotnet build sharpy.sln -c Release
build_tools/bin/build_sharpy install     # installs a sharpyc wrapper to ~/.local/bin
```

Or run it straight from the repo without installing:

```bash
dotnet run --project src/Sharpy.Cli -- run hello.spy
```

The commands below assume `sharpyc` is on `PATH`; otherwise substitute
`dotnet run --project src/Sharpy.Cli --`.

---

## Single-file program

```python
# hello.spy — main() is the entry point, invoked automatically.
# No statements are allowed at module level, and there is no
# `if __name__ == "__main__":` idiom.
def greet(name: str) -> str:
    return f"Hello, {name}!"

def main():
    print(greet("World"))
```

```bash
sharpyc run hello.spy               # compile + execute (temp output)
sharpyc run hello.spy -- arg1 arg2  # argv after a bare --
sharpyc compile hello.spy -o out/   # runnable artifact + runtime DLLs
sharpyc emit csharp hello.spy       # inspect the generated C#
```

Single-file `run` **auto-discovers sibling imports**: if `hello.spy` has
`from geometry import Point`, a `./geometry.spy` next to it is compiled in
with no project file.

---

## Multi-file project (`.spyproj`)

Layout:

```text
myapp/
├── myapp.spyproj
├── main.spy          # entry point: must contain def main():
└── geometry.spy      # library module: from geometry import ...
```

`myapp.spyproj` — minimal:

```xml
<Project>
  <PropertyGroup>
    <RootNamespace>MyApp</RootNamespace>
    <OutputType>exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <SpyFile Include="**/*.spy" />
  </ItemGroup>
</Project>
```

Every supported key (nothing else is read — no `<Configuration>`, no
`<ProjectReference>`):

```xml
<Project>
  <PropertyGroup>
    <RootNamespace>MyApp</RootNamespace>          <!-- REQUIRED -->
    <OutputType>exe</OutputType>                  <!-- default: library -->
    <TargetFramework>net10.0</TargetFramework>    <!-- default: net10.0 -->
    <AssemblyName>MyApp</AssemblyName>            <!-- default: RootNamespace -->
    <EntryPoint>main.spy</EntryPoint>             <!-- default: main.spy -->
    <WarningsAsErrors>false</WarningsAsErrors>
    <NoWarn>SPY0451;SPY0452</NoWarn>
    <Features>defer;matmul</Features>             <!-- experimental flags;
                                                       unknown names fail loudly -->
  </PropertyGroup>
  <ItemGroup>
    <SpyFile Include="src/**/*.spy" Exclude="src/scratch/**" />
    <Reference Include="lib/ThirdParty.dll" />    <!-- .NET assembly -->
    <ModulePath Include="vendor" />               <!-- assembly probe dir -->
    <PackageReference Include="xunit" Version="2.9.3" />
  </ItemGroup>
</Project>
```

Build and run:

```bash
sharpyc project myapp.spyproj        # build only → bin/Debug/net10.0/MyApp.exe
sharpyc compile myapp.spyproj        # build + copy runtime DLLs → runnable
dotnet bin/Release/net10.0/MyApp.exe
```

> [!IMPORTANT]
> `sharpyc project` does **not** copy runtime dependencies — its output
> cannot execute standalone (it will fail to find `Sharpy.Core.dll`). Use
> `sharpyc compile <file>.spyproj` (default configuration: Release) to get a
> runnable artifact.

- With no argument, `sharpyc project` uses the single `.spyproj` in the
  current directory (two or more is an error).
- `--incremental` skips unchanged files (cache under `obj/`); `--clean`
  deletes `bin/` and `obj/` first.
- `--emit-cs-to <dir>` dumps the generated C# alongside the build.

### Modules and packages

- `from geometry import Point` finds `geometry.spy`; dotted imports map to
  directories (`from mypackage.helpers import greet` →
  `mypackage/helpers.spy`).
- A package directory may have an `__init__.spy` (may be empty). Re-exports
  in it use the **full dotted path from the project root** — there are no
  relative imports:

  ```python
  # mypackage/__init__.spy
  from mypackage.helpers import greet
  ```

- A project file named like a stdlib module (`json.spy`) does NOT shadow the
  stdlib — registry modules win; pick a different name.
- Circular imports are rejected (SPY0302) — restructure via a shared module.
- The entry file must define `def main():` (SPY0403 otherwise). For
  `<OutputType>library</OutputType>` no entry point exists and `main` is not
  required.

---

## Everyday commands

| Command | Purpose |
|---|---|
| `sharpyc run file.spy [-- args]` | Compile and execute |
| `sharpyc compile <file.spy \| proj.spyproj>` | Runnable artifact (+ deps) |
| `sharpyc project [proj.spyproj]` | Build a project (no deps copied) |
| `sharpyc emit csharp file.spy` | Show generated C# (`ast`, `tokens`, `diagnostics` too) |
| `sharpyc explain SPY0340` | Explain any diagnostic code |
| `sharpyc format file.spy` | Format source (`-c` check, `-d` diff) |
| `sharpyc repl` | Interactive REPL |
| `sharpyc --enable-feature defer run f.spy` | Enable an experimental feature (repeat the flag per feature) |

Repeatable flags (`--reference`, `--module-path`, `--enable-feature`) take
one value each — repeat the flag rather than passing a space-separated list.

## Editor setup

`sharpyc lsp` starts the language server (stdio). VS Code: install the
"Sharpy" extension from the marketplace (language id `sharpy`, `.spy` files;
it launches `sharpyc lsp` and picks up a workspace `.spyproj`'s `<Features>`
automatically). Neovim/Emacs/Sublime/Helix/Zed configs live in the repo at
`docs/tooling/editor-integration.md` — all boil down to
`command: sharpyc, args: ["lsp"]`, with `*.spyproj` as the root marker.

## References

- [Sharpy repository](https://github.com/antonsynd/sharpy)
- [Documentation site](https://antonsynd.github.io/sharpy/)
- [Browser playground](https://antonsynd.github.io/sharpy/playground/)
