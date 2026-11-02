# Contributing to Sharpy

## How Contributions Work

Sharpy is run as a benevolent dictatorship (see [GOVERNANCE.md](GOVERNANCE.md)): the maintainer alone decides the direction of the language and its ecosystem. There is no defined roadmap beyond a complete, stable syntax and semantics and (mostly) bug-free compilation and runtime behavior.

- **Bug fixes are welcome.** Compiler, runtime, stdlib, tooling, docs, and test fixes don't need prior approval. For anything non-trivial, opening an issue first saves effort.
- **Direction is not up for a vote.** New syntax, new semantics, new stdlib surface, and new tooling scope are the maintainer's call. You may propose them in an issue, but a PR that changes the language's direction may be declined however well made, so please discuss before investing time.
- **Forking is fine.** If you want a different direction, you're free to fork under the [Apache-2.0 or MIT](#license) license.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Python 3 (for verifying language semantics)

## Build and Test

```bash
git clone https://github.com/antonsynd/sharpy.git
cd sharpy
dotnet build sharpy.sln
dotnet test
```

## Run and Inspect

```bash
dotnet run --project src/Sharpy.Cli -- run file.spy           # Compile and run
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy   # View generated C#
dotnet run --project src/Sharpy.Cli -- explain SPY0200        # Explain an error code
```

## Project Layout

The compiler pipeline:

```
.spy → Lexer → Parser (AST) → Semantic → ValidationPipeline → RoslynEmitter → C# → .NET IL
```

See [docs/architecture.md](docs/architecture.md) for architecture details.

> **AI contributors:** See [CLAUDE.md](CLAUDE.md) for AI-specific guidance.

## Tests

File-based integration tests live in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` and are auto-discovered:

- `.spy` + `.expected` — expected stdout (exact match)
- `.spy` + `.error` — expected error substring

```bash
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based tests
dotnet test --filter "FullyQualifiedName~Lexer"                      # By component
dotnet test --filter "DisplayName~my_feature"                        # By name
```

## Before Submitting

1. `dotnet build sharpy.sln` — builds cleanly
2. `dotnet test` — all tests pass
3. Tests cover the change
4. Never modify `.expected` files to make tests pass — fix the implementation

## License

Unless you explicitly state otherwise, any contribution intentionally submitted
for inclusion in this project shall be dual licensed under [Apache-2.0](LICENSE-APACHE) or [MIT](LICENSE-MIT), without any additional terms or conditions.
