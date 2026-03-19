---
name: compiler-audit
description: Run a comprehensive compiler health audit
argument-hint: "[focus-area]"
---

Run a comprehensive health audit of the Sharpy compiler. Spawns a team of parallel read-only agents, each auditing a different dimension of compiler health. Synthesizes findings into a prioritized report.

If `$ARGUMENTS` is non-empty, narrow the audit to that focus area (e.g., "semantic", "codegen", "testing"). Otherwise audit all dimensions.

## Steps

### 1. Create a team

Use `TeamCreate` with team name `compiler-audit`.

### 2. Create tasks for each audit dimension

Create one task per dimension using `TaskCreate`. If `$ARGUMENTS` specifies a focus area, only create tasks relevant to that area. Otherwise create all seven:

| Task | Agent Type | Focus |
|------|-----------|-------|
| Architecture & Modularity | `code-reviewer` | Large file hotspots, coupling, duplication, separation of concerns |
| Type Safety & Correctness | `verification-expert` | Test suite results, skipped tests, error recovery paths, diagnostic gaps, SemanticInfo integrity |
| .NET Compliance | `net-axiom-guardian` | C# 9.0 compliance, type mapping completeness, netstandard compatibility, forward compatibility |
| Testing Health | `verification-expert` | Coverage gaps by category, test-to-code ratio, negative/warning/snapshot test coverage |
| Codebase Metrics | `Explore` | File sizes, TODO/FIXME audit (issue compliance), git churn hotspots, unused diagnostics, magic values |
| LSP Health | `verification-expert` | Handler coverage, test coverage, thread safety, position conversion correctness, refactoring providers |
| Future Readiness | `Explore` | Parallel build readiness (thread safety), REPL/formatter/debugger readiness |

### 3. Spawn agents in parallel

Spawn all agents simultaneously using the `Task` tool with `team_name: "compiler-audit"`. Each agent is strictly **read-only** (run tests, read files, search code - no edits). Provide each agent with detailed, specific prompts:

**Architecture & Modularity** (`code-reviewer`):
```
Audit the Sharpy compiler for architecture and modularity health. Focus on:
- Large file hotspots: identify files over 500 lines in src/Sharpy.Compiler/ and src/Sharpy.Core/
- Coupling: check for circular dependencies between Semantic/, CodeGen/, Parser/
- Duplication: look for repeated patterns that should be abstracted
- Separation of concerns: verify TypeChecker vs ValidationPipeline responsibility split, RoslynEmitter partial file boundaries

Use CodeGraphContext `find_dead_code` and `find_most_complex_functions` to identify hotspots.
Use CodeGraphContext `analyze_code_relationships` to check for circular dependencies between Semantic/, CodeGen/, Parser/.

Key directories: src/Sharpy.Compiler/ (Lexer/, Parser/, Semantic/, CodeGen/, Diagnostics/, Analysis/), src/Sharpy.Lsp/
Key files: RoslynEmitter*.cs (16 partials), TypeChecker*.cs (10 partials), Parser*.cs (6 partials)

Output your findings as structured markdown with: Critical (must fix), Warning (should fix), Opportunity (nice to have).
```

**Type Safety & Correctness** (`verification-expert`):
```
Audit compiler correctness and type safety. Focus on:
- Run `dotnet test` and report results (pass/fail/skip counts)
- List all skipped tests (.skip files in src/Sharpy.Compiler.Tests/Integration/TestFixtures/)
- Check error recovery paths in Parser and TypeChecker (look for UnknownType usage, error node handling)
- Verify diagnostic coverage: are all SPY codes in DiagnosticCodes.cs actually emitted somewhere?
- Check SemanticInfo integrity: look for places where types might not be recorded

Key files: src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs, src/Sharpy.Compiler/Semantic/TypeChecker*.cs
Key directories: src/Sharpy.Compiler.Tests/

Output your findings as structured markdown with: Critical, Warning, Opportunity.
```

**Testing Health** (`verification-expert`):
```
Audit the test suite health. Focus on:
- Count tests by category: unit vs integration, file-based vs programmatic
- Count file-based test fixtures: .spy+.expected pairs, .spy+.error pairs, .spy+.warning pairs, .spy+.expected.cs snapshot pairs
- Identify coverage gaps: which compiler components have few or no tests?
- Check test-to-code ratio per component (Parser, Semantic, CodeGen, Sharpy.Core)
- List negative test coverage (.error files) and warning test coverage (.warning files)
- Check for snapshot test coverage (.expected.cs files)

Key directories: src/Sharpy.Compiler.Tests/, src/Sharpy.Core.Tests/, src/Sharpy.Compiler.Tests/Integration/TestFixtures/

Output your findings as structured markdown with metrics tables and gap analysis.
```

**.NET Compliance** (`net-axiom-guardian`):
```
Audit .NET runtime compatibility. Focus on:
- C# 9.0 compliance in Sharpy.Core (no global usings, file-scoped namespaces, record structs, init-only setters)
- C# 9.0 compliance in generated code (check RoslynEmitter output patterns)
- Type mapping completeness in CodeGen/TypeMapper.cs and Discovery/ClrTypeMapper.cs
- netstandard2.0/2.1 compatibility of Sharpy.Core APIs
- Check for any .NET 6+ APIs used in Sharpy.Core that aren't available in netstandard2.1

Key files: src/Sharpy.Core/Sharpy.Core.csproj, src/Sharpy.Compiler/CodeGen/TypeMapper.cs, src/Sharpy.Compiler/Discovery/ClrTypeMapper.cs

Output your findings as structured markdown with: Critical, Warning, Opportunity.
```

**Codebase Metrics** (`Explore`, thoroughness: very thorough):
```
Gather codebase metrics for the Sharpy compiler. Focus on:
- File sizes: list the 15 largest .cs files with line counts
- TODO/FIXME/BUG audit: find all such comments and check if they reference GitHub issues (format: TODO(#123))
- Count total .cs files in src/Sharpy.Compiler/ and src/Sharpy.Core/
- Count total .spy test fixture files
- Look for magic numbers/strings in CodeGen/ and Semantic/ that should be constants
- Check for unused diagnostic codes (defined in DiagnosticCodes.cs but never referenced)

Use CodeGraphContext `get_repository_stats` for structural metrics.
Use CodeGraphContext `find_most_complex_functions` for complexity data.

Output your findings as structured markdown with a metrics dashboard table and detailed findings.
```

**LSP Health** (`verification-expert`):
```
Audit the Sharpy LSP server health. Focus on:
- Handler coverage: List all implemented LSP handlers in src/Sharpy.Lsp/Handlers/ and note which standard LSP features are missing
- Test coverage: Count tests per handler in src/Sharpy.Lsp.Tests/, identify handlers with no tests
- E2E tests: Check src/Sharpy.Lsp.Tests/E2E/ for protocol-level integration tests
- Thread safety: Review LanguageService.cs and SharpyWorkspace.cs for proper locking, ConcurrentDictionary usage, cancellation token propagation
- Position conversion: Check PositionConverter.cs for 0-based (LSP) vs 1-based (compiler) coordinate correctness
- Diagnostic publishing: Verify DiagnosticPublisher.cs correctly maps compiler diagnostics to LSP diagnostics
- Refactoring providers: List all ICodeActionProvider implementations in Refactoring/ and their test coverage
- Incremental analysis: Check DocumentState for correct partial re-analysis (AstFingerprint, ScopedTypeChecker)
- Project awareness: Verify LanguageService handles project-level analysis, dependency tracking, and file watching

Key directories: src/Sharpy.Lsp/, src/Sharpy.Lsp/Handlers/, src/Sharpy.Lsp/Refactoring/, src/Sharpy.Lsp.Tests/
Key files: LanguageService.cs, SharpyWorkspace.cs, PositionConverter.cs, DiagnosticPublisher.cs

Output your findings as structured markdown with: Critical, Warning, Opportunity. Include a handler coverage table.
```

**Future Readiness** (`Explore`, thoroughness: very thorough):
```
Assess the Sharpy compiler's readiness for future tooling. Focus on:
- Parallel build readiness: Are there mutable statics or shared state that would prevent parallel compilation? Check CompilerServices, SymbolTable, SemanticInfo for thread safety
- REPL readiness: Can the pipeline process partial/incomplete inputs? Is there statement-level compilation?
- Formatter readiness: Is whitespace/trivia preserved through the pipeline? Can the parser roundtrip?
- Debugger readiness: Is source mapping information available? Are TextSpan locations preserved through codegen?

Key files: src/Sharpy.Compiler/Text/, src/Sharpy.Compiler/Semantic/SemanticInfo.cs, src/Sharpy.Compiler/Semantic/SymbolTable.cs, src/Sharpy.Compiler/Services/

Output your findings as structured markdown with readiness ratings (Ready/Partial/Not Ready) per dimension.
```

### 4. Collect results and synthesize report

Wait for all agents to complete. Synthesize their findings into a single report with this structure:

```markdown
# Sharpy Compiler Health Audit — YYYY-MM-DD

## Executive Summary
(2-3 paragraph overview of overall health, key risks, and top priorities)

## Critical Findings
(Issues requiring immediate attention)

## Warnings
(Issues that should be addressed soon)

## Opportunities
(Nice-to-have improvements)

## Metrics Dashboard

| Metric | Value |
|--------|-------|
| Total source files | |
| Largest file (lines) | |
| Total tests | |
| Tests passing | |
| Tests skipped | |
| TODO/FIXME count | |
| TODOs with issues | |
| TODOs without issues | |
| File-based test fixtures | |
| Snapshot tests | |

## Detailed Findings

### Architecture & Modularity
(Full report from agent)

### Type Safety & Correctness
(Full report from agent)

### .NET Compliance
(Full report from agent)

### Testing Health
(Full report from agent)

### Codebase Metrics
(Full report from agent)

### LSP Health
(Full report from agent)

### Future Readiness
(Full report from agent)

## Recommended Action Items

| Priority | Item | Effort | Impact |
|----------|------|--------|--------|
| P0 | ... | S/M/L | High/Med/Low |
| P1 | ... | S/M/L | High/Med/Low |
| P2 | ... | S/M/L | High/Med/Low |
```

### 5. Save and present

1. Create the `docs/audits/` directory if it doesn't exist
2. Write the report to `docs/audits/audit-YYYY-MM-DD.md` (using today's date)
3. Present the full report inline to the user
4. Shut down all teammates via `SendMessage` with `type: "shutdown_request"`
5. Delete the team via `TeamDelete`
