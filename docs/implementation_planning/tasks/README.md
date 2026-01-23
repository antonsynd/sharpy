# Implementation Task Documents

This directory contains detailed task lists for architecture improvements, technical debt cleanup, and follow-up work identified during architecture reviews.

**Last Updated:** 2026-01-23

---

## Task Documents Overview

### 🔴 High Priority (Blocking Language Features)

| Document | Description | Effort | Status |
|----------|-------------|--------|--------|
| [`task_cross_module_inheritance_fix.md`](task_cross_module_inheritance_fix.md) | Fix inheritance/interface resolution across module boundaries | 3-5 days | Not Started |

### 🟡 Medium Priority (Technical Debt)

| Document | Description | Effort | Status |
|----------|-------------|--------|--------|
| [`task_legacy_validator_decommissioning.md`](task_legacy_validator_decommissioning.md) | Remove legacy validators from TypeChecker, migrate type inference | 1-2 days | Not Started |
| [`task_emitter_codegen_info_migration.md`](task_emitter_codegen_info_migration.md) | Complete RoslynEmitter migration to use CodeGenInfo | 2-3 days | Not Started |

### 🟢 Low Priority (Nice to Have)

| Document | Description | Effort | Status |
|----------|-------------|--------|--------|
| [`task_project_compiler_model_integration.md`](task_project_compiler_model_integration.md) | Wire CompilationUnit/ProjectModel into ProjectCompiler | 3-5 days | Not Started |

### ⚡ Quick Wins

| Document | Description | Effort | Status |
|----------|-------------|--------|--------|
| [`task_documentation_updates.md`](task_documentation_updates.md) | Update architecture docs to reflect current state | 30-60 min | ✅ Complete |
| [`task_quick_wins_cleanup.md`](task_quick_wins_cleanup.md) | Remove warnings, add tests, clean up skipped tests | 1-2 hours | Not Started |

---

## Recommended Execution Order

1. **Cross-Module Inheritance Fix** - Unblocks Phase 0.1.7 (Inheritance & Interfaces)
2. **Documentation Updates** - Quick clarity win, prevents confusion
3. **Quick Wins / Cleanup** - Low effort, improves project hygiene
4. **Legacy Validator Decommissioning** - Do when working on TypeChecker
5. **Emitter Migration** - Do when working on CodeGen
6. **ProjectCompiler Integration** - Do when planning incremental compilation

---

## Completed Tasks

| Document | Completed | Notes |
|----------|-----------|-------|
| `task_compiler_services_layer.md` | ✅ Jan 2025 | CompilerServices implemented with builder pattern |
| `task_dependency_graph_implementation.md` | ✅ Jan 2025 | Full implementation with cycle detection |
| `task_immutable_ast_foundation.md` | ✅ Jan 2025 | Collections migrated to ImmutableArray |

---

## How to Use These Documents

Each task document follows a consistent structure:

1. **Goal** - What the task accomplishes
2. **Priority** - Why it matters
3. **Prerequisites** - What must be done first
4. **Design Decisions** - Two-way door (reversible) vs one-way door (commit now)
5. **Phases** - Incremental steps with verification
6. **Tasks** - Specific work items with code examples
7. **Summary** - Expected outcome

### For AI Agents

These documents are designed for execution by AI coding agents:
- Each task has explicit verification steps
- Code examples show expected changes
- Commit points are clearly marked
- Rollback is possible between phases

### For Human Developers

These documents provide:
- Clear scope and effort estimates
- Context for why changes are needed
- Specific file locations
- Test requirements

---

## Related Documents

- [`../architecture_review_and_recommendations.md`](../architecture_review_and_recommendations.md) - Original architecture review
- [`../architecture_review_addendum_future_features.md`](../architecture_review_addendum_future_features.md) - Future feature considerations
- [`../architecture_summary.md`](../architecture_summary.md) - Current implementation status
- [`../phases.md`](../phases.md) - Language implementation phases
