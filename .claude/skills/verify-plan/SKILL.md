---
name: verify-plan
description: Verify a plan for accuracy and architectural soundness
argument-hint: "<path/to/plan.md>"
---

Verify a plan file for accuracy against the actual Sharpy codebase. Reads the plan, extracts every verifiable claim, checks each against the codebase, and edits the plan directly to fix inaccuracies. Adds a verification stamp at the top.

## Argument Handling

If `$ARGUMENTS` is non-empty, use it as the path to the plan file.

If `$ARGUMENTS` is empty, find the most recently modified `.md` file in `~/.claude/plans/` using:
```bash
ls -t ~/.claude/plans/*.md | head -1
```

Read the plan file completely before proceeding.

## Verification Dimensions

Check each dimension in order. For each claim found, verify it against the actual codebase using Glob, Grep, and Read tools.

### 1. Structural Accuracy

Verify every concrete reference in the plan:
- **File paths**: Use Glob to confirm every referenced file/directory exists
- **Function/method/class/type names**: Use Grep to confirm they exist where claimed
- **Parameter signatures**: Read the actual code and compare signatures
- **Diagnostic codes**: Check against `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`
- **Line number references**: Read the file and verify content at those lines

Flag as error: any path, name, or code that doesn't exist. Fix inline if the correct reference can be determined.

### 2. Consistency with Project Conventions

Check the plan follows Sharpy project rules:
- **Feature implementation order**: Changes should follow Lexer -> Parser -> Semantic -> Validation -> CodeGen -> Tests
- **Immutable AST rule**: No plan should modify AST node records to add mutable state; annotations go in SemanticInfo
- **SyntaxFactory-only rule**: Any CodeGen changes must use Roslyn SyntaxFactory, never string templating
- **Axiom precedence**: .NET > Type Safety > Python Syntax — flag if plan violates this
- **C# 9.0 constraint**: Any Sharpy.Core or generated code changes must be C# 9.0 compatible
- **Language spec authority**: Check `docs/language_specification/` for relevant spec sections; flag if plan contradicts spec
- **TODO -> issue rule**: Plan should not introduce TODOs without mentioning issue creation
- **Test expectations rule**: Plan must never suggest modifying .expected files to make tests pass

Flag as warning: any convention violation. Add a note explaining the correct convention.

### 3. Architectural Soundness

Verify architectural claims and decisions:
- **Pipeline phase ordering**: Verify claimed phase dependencies match actual pipeline (NameResolver -> ImportResolver -> TypeResolver -> TypeChecker -> ValidationPipeline)
- **SemanticInfo vs AST**: Confirm plan doesn't propose putting computed data on AST nodes
- **Materialization boundaries**: Check that any new symbol data is materialized at the correct phase boundary
- **Validation pipeline placement**: Verify proposed validator Order values don't conflict with existing validators; check TypeChecker vs ValidationPipeline responsibility split
- **Symbol lifecycle**: Confirm plan respects progressive symbol population across passes
- **CompilerServices integration**: If plan adds new services, verify they follow the CompilerServicesBuilder adapter pattern

Flag as error: architectural violations that would break the pipeline. Flag as warning: suboptimal placement.

### 4. Correctness

Check that proposed changes will actually work:
- **Compilation**: Will the proposed C# code compile? Check for obvious type errors, missing usings, wrong method signatures
- **Edge cases**: Does the plan address empty collections, null values, error recovery, UnknownType handling?
- **Python behavior**: For any claimed Python behavior, verify with `python3 -c "..."` commands
- **Type narrowing**: If plan touches type narrowing, verify interactions with `_narrowingContext` (TypeNarrowingContext)
- **Variable versioning**: If plan touches local variables in CodeGen, verify interactions with `_variableVersions`

Flag as warning: unchecked edge cases. Flag as error: demonstrably incorrect claims.

### 5. Completeness

Check that nothing is missing:
- **All pipeline phases covered**: If adding a new language feature, verify plan covers all phases (Lexer through Tests)
- **Tests specified**: Every implementation change should have corresponding test additions
- **New tokens**: Must flow through to Parser, Semantic, and CodeGen
- **New AST nodes**: Must be handled in TypeChecker and RoslynEmitter
- **New validators**: Must be registered in `ValidationPipelineFactory` with correct Order
- **Imports/usings**: New dependencies properly imported

Flag as warning: missing steps. Add them as suggestions in the verification summary.

## Output

After verification, edit the plan file directly:

### 1. Add verification stamp at the very top of the file

```markdown
<!-- Verified by /project:verify-plan on YYYY-MM-DD -->
<!-- Verification result: [PASS / PASS WITH CORRECTIONS / NEEDS REVISION] -->
```

Use:
- **PASS** — No errors found, at most minor suggestions
- **PASS WITH CORRECTIONS** — Errors found and corrected inline; plan is now accurate
- **NEEDS REVISION** — Fundamental architectural or correctness issues that require the plan author's judgment

### 2. Add a Verification Summary section at the end of the plan

```markdown
## Verification Summary

**Result:** [PASS / PASS WITH CORRECTIONS / NEEDS REVISION]
**Verified on:** YYYY-MM-DD
**Plan file:** [path]

### Corrections Made
- (list each inline correction with before/after)

### Warnings
- (list each warning with explanation)

### Missing Steps Added
- (list suggestions for missing steps)

### Unchecked Claims
- (list any claims that couldn't be verified, with reason)
```

### 3. Fix errors inline

For each error found, edit the plan text directly to correct it. Mark corrections with `[CORRECTED: reason]` so the author can review changes.

Present a brief summary to the user after editing is complete.
