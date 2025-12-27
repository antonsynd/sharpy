# Custom Agents

Specialized agents for Sharpy development. Each agent has domain expertise and clear boundaries.

## Quick Reference

### Core Agents

| Agent | Domain | Key Command |
|-------|--------|-------------|
| `implementer` | Full implementation + PRs | `dotnet build && dotnet test` |
| `code_reviewer` | PR review (read-only) | N/A |
| `task_planner` | Task decomposition, coordination | N/A |
| `verification_expert` | Read-only verification | `dotnet test` |

### Compiler Pipeline Specialists

| Agent | Domain | Key Command |
|-------|--------|-------------|
| `lexer_expert` | Tokenization, lexical analysis | `dotnet test --filter "FullyQualifiedName~Lexer"` |
| `parser_expert` | AST construction, grammar | `dotnet test --filter "FullyQualifiedName~Parser"` |
| `semantic_expert` | Type checking, name resolution | `dotnet test --filter "FullyQualifiedName~Semantic"` |
| `codegen_expert` | Roslyn C# emission | `dotnet test --filter "FullyQualifiedName~CodeGen"` |
| `compiler_expert` | Full pipeline (cross-cutting) | `dotnet test --filter "FullyQualifiedName~Compiler"` |

### Library & CLI

| Agent | Domain | Key Command |
|-------|--------|-------------|
| `core_library_expert` | Standard library (`Sharpy.Core`) | `python3 -c "..."` to verify behavior |
| `cli_expert` | CLI (`sharpyc`) | `dotnet run --project src/Sharpy.Cli -- build` |
| `test_expert` | xUnit tests, coverage | `dotnet test` |

### Quality & Compliance

| Agent | Domain | Key Command |
|-------|--------|-------------|
| `spec_adherence` | Spec compliance verification | N/A (read-only) |
| `hallucination_defense` | Fact-checking, accuracy validation | N/A (read-only) |
| `doc_sync` | Documentation freshness | N/A |
| `docs_writer` | Documentation (read-only) | N/A |

### Steering Agents (Axiom Guardians)

| Agent | Domain | Axiom |
|-------|--------|-------|
| `net_axiom_guardian` | .NET/C# 9.0 compatibility | Axiom 1 |
| `python_axiom_guardian` | Python syntax fidelity | Axiom 2 |
| `type_safety_axiom_guardian` | Static typing, null safety | Axiom 3 |
| `axiom_arbiter` | Conflict resolution | Meta |
| `design_philosophy_guardian` | Overall design principles | Philosophy |
| `unity_compatibility_guardian` | Unity-specific constraints | Platform |

## Agent Boundaries

**All agents must:**
- Never artificially make tests pass (fix bugs instead)
- Run tests before/after changes
- Follow existing code patterns
- Reference the language specification when implementing features

**Domain separation:**
- `lexer_expert` → `src/Sharpy.Compiler/Lexer/` only
- `parser_expert` → `src/Sharpy.Compiler/Parser/` only
- `semantic_expert` → `src/Sharpy.Compiler/Semantic/` only
- `codegen_expert` → `src/Sharpy.Compiler/CodeGen/` only
- `core_library_expert` → `src/Sharpy.Core/` only
- `cli_expert` → `src/Sharpy.Cli/` only
- Cross-domain changes require coordination via `implementer`, `compiler_expert`, or `task_planner`

---

## Task Planner

Decomposes complex features into subtasks and coordinates specialist agents.

**Use for:**
- Multi-component features spanning lexer→parser→semantic→codegen
- Planning implementation milestones
- Identifying dependencies between tasks

**Output:** Implementation plans with phases, agent assignments, and deliverables

---

## Compiler Expert

Handles: Full Lexer → Parser → Semantic → CodeGen pipeline

```bash
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
```

Key patterns: Immutable AST nodes, visitor pattern, Roslyn `SyntaxFactory`

---

## Pipeline Specialists

### Lexer Expert
Handles: Tokenization, keyword recognition, literal parsing, indentation tracking

Key patterns: Token types, source locations, lexer state machine

### Parser Expert
Handles: EBNF grammar implementation, AST node construction, precedence climbing

Key patterns: Recursive descent, Pratt parsing for expressions, error recovery

### Semantic Expert
Handles: Symbol tables, type inference, name resolution, scope analysis

Key patterns: Multi-pass analysis, type unification, semantic errors

### CodeGen Expert
Handles: Roslyn SyntaxFactory, C# AST emission, lowering transformations

Key patterns: `SyntaxFactory.*`, trivia handling, C# 9.0 targeting

---

## Core Library Expert

Handles: Pythonic APIs wrapping .NET types

```bash
python3 -c "print([1,2,3].pop())"  # Verify expected behavior first
dotnet test --filter "FullyQualifiedName~ListTests"
```

Key pattern: `partial class Exports` for builtins, negative indexing, slicing

---

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

**Critical:** Never alter expected values to pass tests. Fix the implementation.

---

## CLI Expert

Handles: System.CommandLine integration in `src/Sharpy.Cli/`

```bash
dotnet run --project src/Sharpy.Cli -- build file.spy
```

---

## Documentation Sync Agent

Handles: Keeping `docs/` synchronized with implementation

- Detects drift between spec and implementation
- Updates examples when APIs change
- Generates changelog entries
- Creates PRs for doc updates

---

## Spec Adherence Agent

Handles: Verifying implementation matches `docs/language_specification/`

- Cross-references code against spec documents
- Flags deviations with spec citations
- **Read-only:** reports findings, doesn't modify code
- Produces compliance reports

---

## Hallucination Defense Agent

Handles: Fact-checking and accuracy validation

- Verifies claims against codebase reality
- Catches incorrect assumptions about .NET/Roslyn APIs
- Validates test assertions match actual behavior
- **Read-only:** flags issues for human review

Common checks:
- .NET API behavior verification
- Roslyn SyntaxFactory method existence
- Python semantic parity
- C# 9.0 feature availability

---

## Steering Agents (Axiom Guardians)

These agents ensure implementation stays aligned with Sharpy's core principles.

### Axiom Precedence

```
When axioms conflict: Axiom 1 > Axiom 3 > Axiom 2
                      .NET    > Types   > Python

Unless the conflict can be resolved at zero cost.
```

### .NET Axiom Guardian

Guards **Axiom 1: .NET Runtime Compatibility**

- Ensures all output is valid C# 9.0
- Verifies .NET interop works seamlessly
- Catches C# 10+ features that would break compatibility
- Validates type mappings to .NET types

**Key checks:**
- No file-scoped namespaces, global usings, record structs
- No dynamic/DLR usage
- Extension methods over wrapper types
- Roslyn optimization pipeline preserved

### Python Axiom Guardian

Guards **Axiom 2: Python Surface Syntax**

- Ensures syntax feels natural to Python developers
- Flags unnecessary C#-isms
- Documents intentional deviations
- Validates Python idiom support

**Key checks:**
- `snake_case` naming, `and`/`or`/`not` operators
- List/dict/set literals, comprehensions, slicing
- Built-in functions (`print`, `len`, `range`)
- No access modifiers on functions

### Type Safety Axiom Guardian

Guards **Axiom 3: Static & Null-Safe Typing**

- Ensures all types known at compile time
- Enforces non-nullable by default
- Prevents dynamic dispatch and duck typing
- Validates null narrowing works correctly

**Key checks:**
- All variables have type annotations
- No `Any` type or untyped containers
- Nullable uses `T?` explicitly
- No runtime type discovery

### Axiom Arbiter

Resolves conflicts between axioms when they arise.

- Applies precedence rules (.NET > Types > Python)
- Attempts zero-cost resolution first
- Documents tradeoffs and rationale
- Maintains decision registry
- Escalates novel conflicts to human maintainers

### Design Philosophy Guardian

Guards overall design principles beyond the axioms:

- **Developer happiness:** Does this bring joy?
- **Zero-overhead:** No runtime cost for abstractions
- **Simplicity:** Each feature must earn its complexity
- **Principled constraints:** Explicit > implicit

### Unity Compatibility Guardian

Ensures generated C# works in Unity:

- C# 9.0 language features only
- .NET Standard 2.1 API surface
- IL2CPP AOT compilation safe
- No runtime code generation or problematic reflection
- Platform considerations (WebGL, mobile, console)

---

## Verification Expert

Handles: Read-only verification of all components

- Runs comprehensive test suites
- Produces verification reports
- Validates behavior against specs
- Identifies regressions

---

## Docs Writer

Read-only access. Verify examples compile before documenting.

---

## Implementer

Full implementation agent with PR creation capabilities. Use for:
- New feature implementation
- Bug fixes
- Updating existing PRs based on feedback

See: `Github Sharpy Implementer.agent.md`

---

## Code Reviewer

Reviews PRs for security, performance, SOLID principles, and Sharpy design alignment.

See: `Github Sharpy Code Reviewer.agent.md`

---

## Agent Collaboration Patterns

### Feature Implementation Flow

```
task_planner → creates implementation plan
    ↓
lexer_expert → implements tokens
    ↓
parser_expert → implements AST nodes
    ↓
semantic_expert → implements type checking
    ↓
codegen_expert → implements C# emission
    ↓
test_expert → implements tests
    ↓
spec_adherence → verifies compliance
    ↓
doc_sync → updates documentation
    ↓
implementer → creates PR
    ↓
code_reviewer → reviews PR
    ↓
verification_expert → final verification
```

### Axiom Guardian Flow

```
Any implementation decision
    ↓
net_axiom_guardian → checks .NET/C# 9.0 compatibility
python_axiom_guardian → checks Python syntax fidelity
type_safety_axiom_guardian → checks static typing
unity_compatibility_guardian → checks Unity support
    ↓
[If conflict detected]
    ↓
axiom_arbiter → resolves using precedence rules
    ↓
design_philosophy_guardian → validates overall approach
    ↓
[If novel/significant]
    ↓
Human maintainer → final decision
```

### Steering Review Flow

For any significant design decision:

```
1. Implementation proposed
    ↓
2. Axiom guardians review (parallel)
   ├─ net_axiom_guardian: ".NET OK?"
   ├─ python_axiom_guardian: "Pythonic?"
   ├─ type_safety_axiom_guardian: "Type-safe?"
   └─ unity_compatibility_guardian: "Unity OK?"
    ↓
3. If conflicts → axiom_arbiter
    ↓
4. design_philosophy_guardian: "Simple? Joyful? Zero-cost?"
    ↓
5. Proceed or revise
```

### Fact-Checking Flow

```
Any agent makes a claim
    ↓
hallucination_defense → verifies claim
    ↓
Report: ✅ VERIFIED / ❌ INCORRECT
```

### Documentation Flow

```
implementer → merges code changes
    ↓
doc_sync → detects doc drift
    ↓
doc_sync → creates doc update PR
    ↓
spec_adherence → verifies accuracy
```

### Complete Agent Hierarchy

```
                    Human Maintainers
                          ↑
                    [escalation]
                          │
    ┌─────────────────────┼─────────────────────┐
    │                     │                     │
axiom_arbiter    design_philosophy      task_planner
    ↑                guardian                  │
    │                                          │
    ├──── net_axiom_guardian            ┌──────┴──────┐
    ├──── python_axiom_guardian         │  Specialists │
    ├──── type_safety_axiom_guardian    │             │
    └──── unity_compatibility_guardian  ├─ lexer      │
                                        ├─ parser     │
    spec_adherence ←───────────────────→├─ semantic   │
    hallucination_defense ←────────────→├─ codegen    │
    verification_expert ←──────────────→├─ test       │
                                        ├─ core_lib   │
                                        └─ cli        │
                                                ↓
                                          implementer
                                                ↓
                                          code_reviewer
                                                ↓
                                            doc_sync
```
