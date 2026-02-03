# Compiler Hardening Implementation Phases

> **Source:** [remaining-hardening-concerns.md](../remaining-hardening-concerns.md)
> **Date:** 2026-02-03
> **Target:** Junior engineers and Claude Sonnet

---

## Overview

This directory contains implementation plans for hardening the Sharpy compiler. The concerns from the original assessment have been organized into 6 phases based on priority, dependencies, and logical groupings.

### Phase Summary

| Phase | Name | Priority | Effort | Concerns |
|-------|------|----------|--------|----------|
| [1](phase-1-incremental-build-correctness.md) | Incremental Build Correctness | P0 | 11-15h | #1, #2, #3 |
| [2](phase-2-path-and-name-consistency.md) | Path and Name Consistency | P1 | 4-6h | #4, #5, #12 |
| [3](phase-3-robustness-and-phase-integrity.md) | Robustness and Phase Integrity | P2 | 3-4h | #6, #7 |
| [4](phase-4-test-coverage-expansion.md) | Test Coverage Expansion | P1 | 2-4h | #9 |
| [5](phase-5-lsp-readiness-foundation.md) | LSP Readiness Foundation | P2 | 9-12h | #11, #14 |
| [6](phase-6-advanced-type-safety-and-quality.md) | Advanced Type Safety & Quality | P3 | 14-22h | #8, #13, #15 |

**Total effort:** 43-63 hours (~5-8 weeks at 50% allocation)

---

## Priority Definitions

- **P0 (Critical):** Must fix before v1.0 — production correctness at stake
- **P1 (Important):** Should fix for v1.0 — significant quality/UX impact
- **P2 (Recommended):** Nice for v1.0 — improves maintainability/debuggability
- **P3 (Nice-to-have):** Can defer to v1.1 — enhancements, not blockers

---

## Dependency Graph

```
Phase 1 (P0: Incremental Build)
    │
    ├──→ Phase 2 (P1: Path/Name) [can run in parallel]
    │
    └──→ Phase 3 (P2: Robustness) [can run in parallel]

Phase 2 ──→ Phase 4 (P1: Test Coverage) [uses PathNormalizer]

Phase 4 ──→ Phase 5 (P2: LSP Readiness) [stable foundation needed]

Phase 5 ──→ Phase 6 (P3: Advanced) [optional, can run independently]
```

### Recommended Order

1. **Week 1-2:** Phase 1 (critical correctness)
2. **Week 2-3:** Phase 2 + 3 (parallel, low effort)
3. **Week 3-4:** Phase 4 (test coverage)
4. **Week 4-6:** Phase 5 (if LSP on roadmap)
5. **Week 6+:** Phase 6 (opportunistic)

---

## Quick Reference: Concerns by Phase

### Phase 1: Incremental Build Correctness
- **#1:** Cache lacks compiler version → silent bugs after upgrade
- **#2:** SymbolSerializer format not versioned → schema evolution blocked
- **#3:** Restored symbols lack validation → stale type references

### Phase 2: Path and Name Consistency
- **#4:** Path normalization inconsistent (6 implementations) → cache key mismatches
- **#5:** Variable name collision → cryptic C# errors
- **#12:** DiagnosticBag lacks deduplication → duplicate errors shown to user

### Phase 3: Robustness and Phase Integrity
- **#6:** `null!` usage in ProjectCompiler → unhelpful NREs
- **#7:** Dual-write assertions DEBUG-only → silent release bugs

### Phase 4: Test Coverage Expansion
- **#9:** Missing incremental compilation tests and advanced import scenarios → cache bugs invisible

### Phase 5: LSP Readiness Foundation
- **#11:** No CancellationToken in semantic analysis → can't cancel
- **#14:** No Roslyn error mapping → users see C# errors

### Phase 6: Advanced Type Safety & Quality
- **#8:** No grammar-aware fuzzing → edge cases untested
- **#13:** Type narrowing not persisted → LSP hover shows wrong type
- **#15:** Missing match exhaustiveness → silent runtime errors

---

## Deferred Item

**Concern #10: TypeChecker Size (~4,600 lines)** is deferred and not included in any phase. It should be addressed when:
- Implementing LSP features
- Adding major type system features
- The file becomes difficult to navigate

The TypeChecker is currently well-organized across 5 partial files; refactoring is a "nice to have" rather than a necessity.

---

## How to Use These Documents

### For Junior Engineers

1. Start with Phase 1, Task 1.1 (smallest, well-contained)
2. Read the "Background" section to understand the problem
3. Follow the checklist items in order
4. Run verification commands after each task
5. Ask for review before moving to next task

### For Claude Sonnet/AI Assistants

1. Each phase document is self-contained
2. Implementation checklists use markdown checkboxes
3. Code examples are complete and copy-pasteable
4. Verification commands are provided
5. Decision points are clearly marked with Q/A format

### For Senior Engineers/Reviewers

1. Review the "Design Decisions" sections for trade-offs
2. Check "Future Considerations" for long-term implications
3. Verify completion criteria before signing off
4. Consider performance impact notes

---

## Testing Commands

```bash
# Run all tests
dotnet test

# Run tests for a specific phase
dotnet test --filter "FullyQualifiedName~IncrementalCompilation"  # Phase 1
dotnet test --filter "FullyQualifiedName~PathNormalizer"          # Phase 2
dotnet test --filter "FullyQualifiedName~SemanticBinding"         # Phase 3
dotnet test --filter "DisplayName~multi_file"                      # Phase 4
dotnet test --filter "FullyQualifiedName~Cancellation"             # Phase 5
dotnet test --filter "FullyQualifiedName~Fuzzing"                  # Phase 6

# Quick smoke test
dotnet run --project src/Sharpy.Cli -- run snippets/hello_world.spy

# Build in Release mode (important for Phase 3)
dotnet build -c Release
dotnet test -c Release
```

---

## Progress Tracking

Use this section to track implementation progress:

### Phase 1: Incremental Build Correctness ✅
- [x] Task 1.1: Compiler version in cache
- [x] Task 1.2: SymbolSerializer versioning
- [x] Task 1.3: Restored symbol validation (infrastructure only; dependency graph handles scenarios; error detection tests added)
- [x] Code review completed (2026-02-03)

### Phase 2: Path and Name Consistency
- [ ] Task 2.1: PathNormalizer utility (consolidate 6 implementations)
- [ ] Task 2.2: Variable name collision fix
- [ ] Task 2.3: Add diagnostic deduplication

### Phase 3: Robustness and Phase Integrity
- [ ] Task 3.1: Replace `null!` pattern
- [ ] Task 3.2: Always-active assertions

### Phase 4: Test Coverage Expansion
- [ ] Task 4.1: Additional file-based multi-file fixtures (5 scenarios)
- [ ] Task 4.2: Programmatic incremental compilation tests (8 tests)

### Phase 5: LSP Readiness Foundation
- [ ] Task 5.1: CancellationToken support
- [ ] Task 5.2: Source mapping for Roslyn errors

### Phase 6: Advanced Type Safety & Quality
- [ ] Task 6.1: Grammar-aware fuzzing
- [ ] Task 6.2: Type narrowing persistence
- [ ] Task 6.3: Match exhaustiveness warnings

---

## Related Documents

- [CLAUDE.md](/CLAUDE.md) — Main codebase guide
- [phases.md](../phases.md) — Feature implementation phases (v0.1.x)
- [remaining-hardening-concerns.md](../remaining-hardening-concerns.md) — Original assessment
- [.github/copilot-instructions.md](/.github/copilot-instructions.md) — Architecture patterns
