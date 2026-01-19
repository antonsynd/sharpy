# Claude Code Commands for Sharpy

Custom slash commands for the Sharpy compiler development workflow. These commands are available in Claude Code via the `/project:` prefix.

## Quick Reference

| Command | Description | Use When |
|---------|-------------|----------|
| `/project:implement` | Full implementation workflow | Adding features, fixing bugs |
| `/project:review` | Code review (read-only) | Reviewing changes before merge |
| `/project:plan` | Task decomposition | Breaking down complex features |
| `/project:test` | Run component tests | Testing specific components |
| `/project:emit` | Inspect generated C# | Debugging code generation |
| `/project:verify-python` | Check Python behavior | Implementing stdlib functions |
| `/project:fix-issue` | Fix GitHub issue | Working on reported bugs |
| `/project:check-axioms` | Verify axiom compliance | Design decisions |
| `/project:add-test-fixture` | Create file-based test | Adding test coverage |

## Usage Examples

```
/project:implement Add list.insert() method to Sharpy.Core
/project:review the changes in src/Sharpy.Compiler/CodeGen/
/project:plan Add support for async/await syntax
/project:test Semantic
/project:emit samples/hello.spy
/project:verify-python [1,2,3].pop()
/project:fix-issue #42
/project:check-axioms integer division implementation
/project:add-test-fixture test for negative list indexing
```

## Command Structure

Each command is a Markdown file that:
1. Describes the task workflow
2. Uses `$ARGUMENTS` to capture user input
3. Provides relevant context and patterns
4. Lists related files and commands

## Creating New Commands

1. Create a new `.md` file in this directory
2. Use `$ARGUMENTS` where user input should go
3. Include relevant patterns and guidance
4. Test by invoking `/project:your-command`

## Related Resources

- **Specialized agents**: `.github/agents/` (domain-specific guidance)
- **Contribution guides**: `.github/instructions/` (per-directory guidance)
- **Language spec**: `docs/language_specification/` (feature specifications)
