# Fix GitHub Issue

Analyze and fix a GitHub issue for the Sharpy compiler.

## Issue

$ARGUMENTS

## Workflow

### 1. Understand the Issue

- Read the issue description and any linked discussions
- Identify the expected vs actual behavior
- Determine which component is affected

### 2. Reproduce

```bash
# Create a minimal test case
echo 'your_code_here' > /tmp/test.spy
dotnet run --project src/Sharpy.Cli -- run /tmp/test.spy

# Or inspect generated C#
dotnet run --project src/Sharpy.Cli -- emit csharp /tmp/test.spy

# Inspect AST for parser issues
dotnet run --project src/Sharpy.Cli -- emit ast /tmp/test.spy
```

### 3. Diagnose

- Search codebase for related code
- Identify root cause in the relevant component:
  - **Lexer** issues: Token recognition, keywords
  - **Parser** issues: AST construction, syntax errors
  - **Semantic** issues: Type checking, name resolution
  - **Validation** issues: Operator/protocol validators
  - **CodeGen** issues: C# emission, Roslyn API usage

### 4. Fix

- Make minimal, targeted changes
- Follow existing code patterns
- Add regression test

### 5. Verify

```bash
dotnet build sharpy.sln
dotnet test
dotnet test --filter "FullyQualifiedName~RelatedComponent"
```

### 6. Test the Original Issue

Re-run the original failing case to confirm the fix.

## Branch Naming

Format: `claude/fix-<short-description>`

Examples:
- `claude/fix-type-narrowing-bug`
- `claude/fix-list-insert-codegen`
