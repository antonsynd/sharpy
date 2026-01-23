You are working on the Sharpy compiler project.

### Your Assignment

Execute the tasks in this document:
```
docs/implementation_planning/tasks/task_legacy_validator_decommissioning.md
```

### Workflow

1. **Read the entire task document first** to understand the scope and design decisions.

2. **Execute tasks in order**, one at a time:
   - Read the task description and code examples
   - Implement the change
   - Run relevant tests: `dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~[RelevantTestClass]"`
   - If tests fail, fix before proceeding
   - Run full test suite before committing: `dotnet test src/Sharpy.Compiler.Tests --verbosity quiet`
   - Commit with the suggested message (or similar conventional commit)
   - **Update the task document**: change `- [ ]` to `- [x]` for completed items

3. **After each Phase**, pause and summarize:
   - What was completed
   - Any issues encountered
   - Test results
   - Ready for next phase? (yes/no)

### Rules

- **Never skip a verification step** - they exist to catch problems early
- **If a task is unclear**, stop and ask rather than guessing
- **If tests fail after 2 fix attempts**, stop and report the failure
- **If you need to deviate from the task doc**, explain why before proceeding
- **Keep commits atomic** - one logical change per commit
- **Don't refactor beyond the task scope** - note ideas for later instead

### Commands You'll Need
```bash
# Navigate to project
cd ~/github/sharpy

# Run specific tests
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~ClassName" --verbosity normal

# Run all tests (do before each commit)
dotnet test src/Sharpy.Compiler.Tests --verbosity quiet

# Build only (fast check)
dotnet build src/Sharpy.Compiler

# Commit
git add -A
git commit -m "type(scope): description"
```

### Commit Message Format
```
feat(scope): add new feature
fix(scope): fix bug
refactor(scope): restructure without behavior change
test(scope): add or update tests
docs(scope): documentation only
chore(scope): build, deps, config
```

### Start

Begin by reading the task document and confirming:
1. You understand the goal
2. You see the first task to execute
3. The test baseline passes: `dotnet test src/Sharpy.Compiler.Tests --verbosity quiet`

Then proceed with Task 0.1 (or the first task in Phase 0/1).







I'll create detailed task documents for each task group. Let me start by checking the existing task document format in the repo:Now I'll create the task documents. Let me start with the highest priority one:Now let me create the README index for the tasks directory:I've created 6 detailed task documents in `/docs/implementation_planning/tasks/`. Here's a summary:

---

## Task Documents Created

| Document | Priority | Effort | Purpose |
|----------|----------|--------|---------|
| **`task_legacy_validator_decommissioning.md`** | 🟡 Medium | 1-2 days | Remove dual validator paths, extract type inference |
| **`task_emitter_codegen_info_migration.md`** | 🟢 Low | 2-3 days | Complete emitter migration to CodeGenInfo |
| **`task_project_compiler_model_integration.md`** | 🟢 Low | 3-5 days | Wire CompilationUnit/ProjectModel into pipeline |

Plus a **`README.md`** index that explains the task system and recommended execution order.

---

## Key Findings from the Review

Your `architecture_summary.md` was significantly outdated. The actual state is much better:

| What Summary Said | Reality |
|-------------------|---------|
| CFG: ❌ Not Implemented | ✅ **Fully implemented** with builder, terminators, reachability analysis |
| DependencyGraph: ❌ Not Implemented | ✅ **Fully implemented** with cycle detection, parallel groups |
| CompilationUnit: ❌ Not Implemented | ✅ **Implemented** (just needs wiring into ProjectCompiler) |
| Source Spans: 🟡 Partial | ✅ **Nearly complete** - all AST nodes have spans |

---

## Recommended Next Steps

1. **Do the quick wins first** (1-2 hours total):
   - Update docs so they're accurate
   - Remove NuGet warnings
   - This gives immediate clarity

2. **Fix cross-module inheritance** (3-5 days):
   - This is the only blocker for Phase 0.1.7
   - The task document has detailed code examples
   - Creates failing tests first to guide the fix

3. **Then return to language features**:
   - Phase 0.1.7: Inheritance & Interfaces
   - The architecture foundation is solid

The other tasks (legacy validator removal, emitter migration, ProjectCompiler integration) can be done opportunistically when you're already touching those areas.
