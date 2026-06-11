# Custom Agents

Specialized agents for Sharpy development. Each agent has domain expertise and clear boundaries.

> **See also:** [copilot-instructions.md](copilot-instructions.md) for architecture, [instructions/](instructions/) for component guides.

## The Three Axioms

All agents follow this priority order when axioms conflict:

| Priority | Axiom | Principle |
|----------|-------|-----------|
| 1 (Highest) | .NET Runtime | Compiles to valid C# 9.0 for .NET CLR |
| 2 | Static Typing | Non-nullable by default, explicit types |
| 3 (Yields) | Python Syntax | Python 3 syntax and idioms |

## Agent Reference

### Implementation Agents

| Agent | Domain | Edits |
|-------|--------|-------|
| `implementer` | Full implementation + PRs | All |
| `code-reviewer` | PR review (security, performance, SOLID) | Read-only |
| `test-expert` | Tests: unit, integration, file-based | `*Tests/` |

### Compiler Component Experts

| Agent | Component | Key Files |
|-------|-----------|-----------|
| `parser-expert` | AST construction | `Compiler/Parser/`, `Ast/*.cs` |
| `semantic-expert` | Type checking, name resolution, symbols | `Compiler/Semantic/`, `TypeChecker*.cs` |
| `codegen-expert` | C# emission via Roslyn | `Compiler/CodeGen/`, `RoslynEmitter*.cs` |
| `core-library-expert` | Core runtime library | `Sharpy.Core/`, `Partial.*/` |
| `stdlib-expert` | Stdlib modules (json, os, re, ...) | `Sharpy.Stdlib/`, `spy/`, `modules/` |
| `lsp-expert` | LSP server, handlers, refactoring | `Sharpy.Lsp/`, `Handlers/`, `Refactoring/` |

### Axiom Guardians (Advisory, Read-Only)

| Agent | Guards | Catches |
|-------|--------|---------|
| `net-axiom-guardian` | Axiom 1: .NET compatibility | C# 10+ features, invalid interop |


### Verification Agents (Read-Only)

| Agent | Purpose | Output |
|-------|---------|--------|
| `verification-expert` | Runs tests, reports results | Test reports |
| `hallucination-defense` | Fact-checks .NET/Python/Roslyn claims | Verification results |

### Dogfood Agents

| Agent | Purpose | Output |
|-------|---------|--------|
| `dogfood-analyst` | Classifies dogfood failures (C1-C5), writes repro files, delegates to `verification-expert`/`test-expert` | Triage reports + test fixtures |

## Teammate Compatibility

Editing domain experts (`parser-expert`, `semantic-expert`, `codegen-expert`, `test-expert`, `core-library-expert`, `lsp-expert`, `stdlib-expert`), `implementer`, and `dogfood-analyst` include team-collaboration tools (`SendMessage`, `TaskUpdate`, `TaskList`, `TaskGet`) and can be spawned as teammates in `/implement-plan` teams. They can message the lead/peers and update the shared task board.

**Read-only agents are NOT teammate-compatible:** `code-reviewer`, `verification-expert`, `net-axiom-guardian`, and `hallucination-defense` lack `SendMessage` and task tools by design. When used in teams, the team lead must pull their results from transcript JSONL or idle notifications — they cannot update the task board or respond to shutdown requests. Use them as standalone subagents (via `Agent` tool), not as teammates.

## MCP Tools for All Agents

Two MCP servers are available for codebase navigation. Prefer them over Grep/Read for structural queries:

| Server | Strength | Use For |
|--------|----------|---------|
| **Serena** | Live LSP, symbol-level ops | `find_symbol`, `find_referencing_symbols`, `get_symbols_overview`, `replace_symbol_body`, `rename_symbol` |
| **CodeGraphContext** | Pre-indexed graph | `find_callers`, `find_dead_code`, `find_most_complex_functions`, `analyze_code_relationships` |

**Rule of thumb:** If you're searching for a *symbol* (function, class, method), use Serena. If you need *relationships* (who calls X, what depends on Y), use CodeGraphContext. Use Grep only for text/regex patterns (comments, strings, non-symbol content).

## Key Rules for All Agents

1. **Never modify test expectations to pass** — fix the implementation
2. **Axiom precedence**: .NET > Type Safety > Python Syntax
3. **Verify Python behavior** — `python3 -c "..."` before implementing
4. **Language spec is authoritative** — check `docs/language_specification/` before implementing
5. **Follow existing patterns** — search codebase for similar code
6. Run tests before/after changes
7. **C# targets**: `Sharpy.Core` uses C# 9.0 (`netstandard2.0;netstandard2.1`); other projects use `net10.0` with `LangVersion latest`
8. **TODO/BUG/FIXME → create GitHub issues** — when leaving a `TODO`, `BUG`, or `FIXME` comment, first create a GitHub issue (`gh issue create`) and reference it (e.g., `// TODO(#123): ...`)

## Commands

```bash
dotnet build sharpy.sln && dotnet test               # Build + test all
dotnet format whitespace                             # Format before commit
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Debug parser
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Debug lexer
python3 -c "..."                                     # Verify Python behavior
```

## Feature Implementation Flow

For language features, component experts work in this order:

```
parser-expert → semantic-expert → codegen-expert → lsp-expert → test-expert
```

1. **Lexer** — Add tokens if needed (`Token.cs`, `Lexer.cs`)
2. **Parser** — Add AST records (`Ast/*.cs`), parsing rules in `Parser*.cs` (6 partial files)
3. **Semantic** — Add type rules (`TypeChecker*.cs` — 10 partial files), validators if needed
4. **CodeGen** — Emit C# via SyntaxFactory (`RoslynEmitter*.cs` — 16 partial files)
5. **LSP** — Update handlers if new AST nodes/semantic types affect IDE features (hover, completion, semantic tokens, etc.)
6. **Tests** — Unit tests per component + `.spy`/`.expected` file-based tests + LSP handler tests

## Semantic Analysis Pipeline (Critical Knowledge)

The semantic phase runs **six ordered passes**. Understanding this is critical:

```
NameResolver.ResolveDeclarations()  → Pass 1: build symbol table
NameResolver.ResolveInheritance()   → Pass 1b: resolve base classes
ImportResolver                      → Pass 1.5: module imports
TypeResolver.ResolveTypes()         → Pass 2: resolve type annotations
TypeChecker.CheckModule()           → Pass 3: type checking + inference
ValidationPipeline.Validate()       → Pass 4: operators/protocols/access
```

**Key registries**: `OperatorRegistry`, `ProtocolRegistry`, `BuiltinRegistry`, `PrimitiveCatalog`

## Symbol Architecture

Symbols use **reference equality** (overridden from record default) because properties are set progressively across passes:

- `SemanticInfo` — Maps AST nodes → types/symbols (uses `ReferenceEqualityComparer`)
- `SemanticBinding` — Stores computed data separately, materialized at phase boundaries
- `Symbol.CodeGenInfo` — Precomputed during semantic analysis for emitter use


## Testing Quick Reference

| Test Type | Location | Format |
|-----------|----------|--------|
| Unit tests | `*Tests/` | xUnit `[Fact]`/`[Theory]` |
| Integration | `Integration/` | Inherit `IntegrationTestBase` |
| File-based | `Integration/TestFixtures/` | `.spy` + `.expected` or `.error` |
| Multi-file | `Integration/TestFixtures/{dir}/` | `main.spy` + siblings + `main.expected` |
| Warnings | `Integration/TestFixtures/` | `.spy` + `.warning` |
| Skip | Add `.skip` file | Contains skip reason |
