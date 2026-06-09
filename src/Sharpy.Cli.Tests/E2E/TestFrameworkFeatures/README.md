# Test-framework E2E smoke (#837, #838, #839, #842)

`feature_tests.spy` exercises one feature from each test-framework issue in a single
module that the compiler lowers to one xUnit test class:

| Test | Issue | Generated lowering |
|------|-------|--------------------|
| `test_async_fixture` | #839 | `ResourceFixture : Xunit.IAsyncLifetime` (`InitializeAsync` setup + async-lambda teardown awaited from `DisposeAsync`), consumed via `IClassFixture<ResourceFixture>` |
| `test_approx` | #837 | `Xunit.Assert.Equal(0.3, 0.1 + 0.2, 7)` (tolerance/precision) |
| `test_match` | #837 | `Xunit.Assert.Throws<ValueError>(...)` + `Xunit.Assert.Matches("bad.*input", __ex.Message)` |
| `test_captured_output` | #838 | `using (var out = CapturedOutput()) { ...; Assert.Equal("hello\n", out.Getvalue()); }` |
| `test_tmp_path` | #842 | per-test `global::Sharpy.TmpPathFixture` field + `System.IDisposable` cleanup |

The single generated class composes all four mechanisms simultaneously
(`IClassFixture<ResourceFixture>, System.IDisposable` with a built-in `_tmpPathFixture`
field), proving they compose correctly.

## Verified procedure

This was verified on 2026-06-09 (net10.0, xUnit 2.9.3). Because file-based fixtures and
the unit suites don't run a real test assembly, the runtime path is proven by emitting
C# and running it under `dotnet test`:

```bash
# 1. Emit the generated C# (project/library mode — no main() required).
dotnet run --project src/Sharpy.Cli -- project tests.spyproj --emit-cs-to gen

# 2. Compile the emitted gen/feature_tests.cs in a standard xUnit project that
#    references Sharpy.Core.dll + Sharpy.Stdlib.dll (net10.0) and the xunit /
#    xunit.runner.visualstudio / Microsoft.NET.Test.Sdk packages, then:
dotnet test
```

Result: **5 passed, 0 failed.** tmp_path directories are created per test and
recursively cleaned up on dispose (0 leftover `sharpy-test-*` dirs). A deliberately
broken `match=` pattern produces a readable failure that maps back to the `.spy`
source via `#line` directives:

```
Assert.Matches() Failure: Pattern not found in value
Regex: "WILL-NOT-MATCH"
Value: "bad input"
   at FeatureTests.FeatureTestsTests.TestMatch() in .../feature_tests.spy:line 26
```

## Known gaps in the `sharpyc project` test-runner path (out of test scope)

Running `sharpyc project tests.spyproj` to produce a directly-`dotnet test`-able output
does **not** work end-to-end today, for two implementation reasons (reported to the
project/compiler owners — not test bugs):

1. **`NuGetResolver` does no transitive dependency resolution** (tracked: #874;
   `src/Sharpy.Compiler/Project/NuGetResolver.cs`).
   `xunit` is a meta-package with an empty `lib/`, so a `.spyproj` that references only
   `xunit` (as the spec's Project Setup example shows) fails to compile with
   `CS0103: The name 'Xunit' does not exist`. Workaround: reference the concrete
   assemblies (`xunit.assert`, `xunit.extensibility.core`, …) explicitly.
2. **`TestProjectScaffold`'s generated `.csproj` doesn't wire in the compiled assembly
   or the generated source** (tracked: #875;
   `src/Sharpy.Compiler/Project/TestProjectScaffold.cs`), so `dotnet test` on the
   scaffold output discovers no tests.

The emit-then-compile procedure above sidesteps both and confirms the generated test
code itself is runtime-correct.
