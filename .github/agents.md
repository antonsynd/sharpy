# Custom Agents

Specialized agents for Sharpy development. Each agent has domain expertise and clear boundaries.

## Quick Reference

| Agent | Domain | Key Command |
|-------|--------|-------------|
| `compiler_expert` | Lexer, Parser, Semantic, CodeGen | `dotnet test --filter "FullyQualifiedName~Compiler"` |
| `core_library_expert` | Standard library (`Sharpy.Core`) | `python3 -c "..."` to verify behavior |
| `test_expert` | xUnit tests, coverage | `dotnet test` |
| `cli_expert` | CLI (`sharpyc`) | `dotnet run --project src/Sharpy.Cli -- build` |
| `docs_writer` | Documentation (read-only) | N/A |
| `verification_expert` | Read-only verification of compiler, standard library, CLI, documentation | `dotnet test` |

## Agent Boundaries

**All agents must:**
- Never artificially make tests pass (fix bugs instead)
- Run tests before/after changes
- Follow existing code patterns

**Domain separation:**
- `compiler_expert` → `src/Sharpy.Compiler/` only
- `core_library_expert` → `src/Sharpy.Core/` only
- `cli_expert` → `src/Sharpy.Cli/` only
- Cross-domain changes require coordination

## Compiler Expert

Handles: Lexer → Parser → Semantic → CodeGen pipeline

```bash
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
```

Key patterns: Immutable AST nodes, visitor pattern, Roslyn `SyntaxFactory`

## Core Library Expert

Handles: Pythonic APIs wrapping .NET types

```bash
python3 -c "print([1,2,3].pop())"  # Verify expected behavior first
dotnet test --filter "FullyQualifiedName~ListTests"
```

Key pattern: `partial class Exports` for builtins, negative indexing, slicing

## Test Expert

Handles: xUnit tests for all components

```csharp
[Fact]
public void TestFeature_Scenario()
{
    // Arrange, Act, Assert
}

[Fact(Skip = "TODO: Reason. See issue #N")]  // Only if blocked
```

## CLI Expert

Handles: System.CommandLine integration in `src/Sharpy.Cli/`

```bash
dotnet run --project src/Sharpy.Cli -- build file.spy
```

## Docs Writer

Read-only access. Verify examples compile before documenting.

## Verification Expert

Read-only access. Verifies the given documentation/specification of a feature
or function, by reading the code, and/or running tests to verify the behavior
or implementation.
