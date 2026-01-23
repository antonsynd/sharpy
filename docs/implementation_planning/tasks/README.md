# Implementation Task Documents

This directory contains detailed task lists for architecture improvements, technical debt cleanup, and follow-up work identified during architecture reviews.

**Last Updated:** 2026-01-23

---

## Task Documents Overview

### 🔴 High Priority (Blocking Language Features)

| Document | Description | Effort | Status |
|----------|-------------|--------|--------|
| [`task_cross_module_inheritance_fix.md`](task_cross_module_inheritance_fix.md) | Fix inheritance/interface resolution across module boundaries | 3-5 days | ✅ Complete |

### 🟡 Medium Priority (Technical Debt)

| Document | Description | Effort | Status |
|----------|-------------|--------|--------|
| [`task_legacy_validator_decommissioning.md`](task_legacy_validator_decommissioning.md) | Remove legacy validators from TypeChecker, migrate type inference | 1-2 days | ✅ Complete |
| [`task_emitter_codegen_info_migration.md`](task_emitter_codegen_info_migration.md) | Complete RoslynEmitter migration to use CodeGenInfo | 2-3 days | ✅ Complete |

### 🟢 Low Priority (Nice to Have)

| Document | Description | Effort | Status |
|----------|-------------|--------|--------|
| [`task_project_compiler_model_integration.md`](task_project_compiler_model_integration.md) | Wire CompilationUnit/ProjectModel into ProjectCompiler | 3-5 days | ✅ Complete |

### ⚡ Quick Wins

| Document | Description | Effort | Status |
|----------|-------------|--------|--------|
| [`task_documentation_updates.md`](task_documentation_updates.md) | Update architecture docs to reflect current state | 30-60 min | ✅ Complete |
| [`task_quick_wins_cleanup.md`](task_quick_wins_cleanup.md) | Remove warnings, add tests, clean up skipped tests | 1-2 hours | ✅ Complete |

---

## Recommended Execution Order

All tasks listed above have been completed. For future work, consider:

1. **Unified Type System** - Large refactor, defer to future
2. **Error Recovery Parser** - LSP work (v0.2.x)
3. **Symbol Index** - LSP work (v0.2.x)

---

## Completed Tasks

| Document | Completed | Notes |
|----------|-----------|-------|
| `task_compiler_services_layer.md` | ✅ Jan 2025 | CompilerServices implemented with builder pattern |
| `task_dependency_graph_implementation.md` | ✅ Jan 2025 | Full implementation with cycle detection |
| `task_immutable_ast_foundation.md` | ✅ Jan 2025 | Collections migrated to ImmutableArray |
| `task_cross_module_inheritance_fix.md` | ✅ Jan 2026 | Cross-module and .NET base class inheritance working |
| `task_legacy_validator_decommissioning.md` | ✅ Jan 2026 | Legacy validators removed from TypeChecker |
| `task_emitter_codegen_info_migration.md` | ✅ Jan 2026 | Tracking sets removed, using CodeGenInfo |
| `task_project_compiler_model_integration.md` | ✅ Jan 2026 | ProjectModel integrated |
| `task_documentation_updates.md` | ✅ Jan 2026 | Architecture docs updated |
| `task_quick_wins_cleanup.md` | ✅ Jan 2026 | Warnings removed, tests added |

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
