# Component Instructions

Component-specific guides for the Sharpy codebase.

## Quick Reference

| Working on | Guide |
|------------|-------|
| New language feature | [Sharpy.Compiler](./Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md) |
| Builtin function | [Sharpy.Core](./Sharpy.Core/HOW_TO_CONTRIBUTE.instructions.md) |
| Compiler tests | [Sharpy.Compiler.Tests](./Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md) |
| Library tests | [Sharpy.Core.Tests](./Sharpy.Core.Tests/HOW_TO_CONTRIBUTE.instructions.md) |
| CLI options | [Sharpy.Cli](./Sharpy.Cli/HOW_TO_CONTRIBUTE.instructions.md) |
| Example programs | [samples](./samples/HOW_TO_CONTRIBUTE.instructions.md) |

## Core Principles

1. **Fix root causes** - Never artificially make tests pass
2. **Match Python semantics** - Test against `python3 -c "..."` for expected behavior
3. **Follow existing patterns** - Check similar code in the codebase

See `../.github/copilot-instructions.md` for repository-wide guidance.
