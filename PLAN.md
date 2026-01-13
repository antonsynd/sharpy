# Implementation Plan: R-0.1.1.1 - Verify Keyword-as-Member-Name Bug Resolution

## Summary

This task is a **verification task** - the bug fix is already implemented. The goal is to confirm the fix works correctly and ensure adequate test coverage.

## Current Status: ✅ Bug Already Fixed

### Evidence of Fix

The `ParsePostfix()` method in `src/Sharpy.Compiler/Parser/Parser.cs` (line 1811) now uses `ExpectIdentifierOrKeyword()` instead of `ExpectIdentifier()`:

```csharp
// Line 1811 in Parser.cs
var member = ExpectIdentifierOrKeyword();
```

The `ExpectIdentifierOrKeyword()` method (lines 2399-2408) accepts both identifiers and keywords:

```csharp
private string ExpectIdentifierOrKeyword()
{
    if (Current.Type == TokenType.Identifier || IsKeywordToken(Current.Type))
    {
        var value = Current.Value;
        Advance();
        return value;
    }
    throw new ParserError($"Expected identifier, got {Current.Type}", Current.Line, Current.Column);
}
```

The `IsKeywordToken()` method (lines 2413-2445) covers all keywords including `property`, `event`, `type`, `class`, `if`, `for`, etc.

## Implementation Steps

### Step 1: Verify Existing Tests Pass
Run the full parser test suite to confirm no regressions:
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test --filter "FullyQualifiedName~Parser" --no-build
```

### Step 2: Add Explicit Test Coverage for Keyword-as-Member-Name
Create a new test in `ParserEdgeCaseTests.cs` that explicitly tests keywords as member names:

**Test cases to add:**
1. `obj.property` - member keyword as member name
2. `obj.event` - member keyword as member name
3. `obj.type` - type keyword as member name
4. `obj.class` - control flow keyword as member name
5. `obj.if` - control flow keyword as member name
6. `obj.for` - control flow keyword as member name
7. `obj.in` - operator keyword as member name
8. `obj.is` - operator keyword as member name
9. `obj?.property` - null-conditional with keyword member name
10. Chained: `obj.class.type.property`

### Step 3: Run Tests and Verify
```bash
dotnet test --filter "FullyQualifiedName~ParserEdgeCaseTests" --verbosity normal
```

### Step 4: Document Verification Results
Update task notes with:
- Confirmation that fix is in place
- Test count before/after adding new tests
- Any edge cases discovered

## Key Files

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/Parser/Parser.cs` | Parser implementation - **already fixed** |
| `src/Sharpy.Compiler.Tests/Parser/ParserEdgeCaseTests.cs` | Add explicit test coverage |

## Test Code to Add

```csharp
#region Keyword as Member Name

[Theory]
[InlineData("obj.property")]
[InlineData("obj.event")]
[InlineData("obj.type")]
[InlineData("obj.class")]
[InlineData("obj.if")]
[InlineData("obj.for")]
[InlineData("obj.in")]
[InlineData("obj.is")]
[InlineData("obj.and")]
[InlineData("obj.or")]
[InlineData("obj.not")]
[InlineData("obj.True")]
[InlineData("obj.False")]
[InlineData("obj.None")]
public void ParsesKeywordAsMemberName(string source)
{
    var assignment = $"x = {source}";
    var module = Parse(assignment);

    module.Body.Should().HaveCount(1);
    var stmt = module.Body[0].Should().BeOfType<Assignment>().Subject;
    var memberAccess = stmt.Value.Should().BeOfType<MemberAccess>().Subject;
    memberAccess.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("obj");
}

[Fact]
public void ParsesNullConditionalWithKeywordMemberName()
{
    var source = "x = obj?.property";
    var module = Parse(source);

    module.Body.Should().HaveCount(1);
    var stmt = module.Body[0].Should().BeOfType<Assignment>().Subject;
    var memberAccess = stmt.Value.Should().BeOfType<MemberAccess>().Subject;
    memberAccess.IsNullConditional.Should().BeTrue();
    memberAccess.Member.Should().Be("property");
}

[Fact]
public void ParsesChainedKeywordMemberNames()
{
    var source = "x = obj.class.type.property";
    var module = Parse(source);

    module.Body.Should().HaveCount(1);
    var stmt = module.Body[0].Should().BeOfType<Assignment>().Subject;
    stmt.Value.Should().BeOfType<MemberAccess>();
}

#endregion
```

## Potential Risks

1. **Low Risk**: The fix is already in place and comprehensive, covering all keyword types.

2. **Test Brittleness**: Some keywords might have semantic restrictions in later compilation phases (e.g., `obj.class` parsing succeeds but semantic analysis might reject it). Tests should focus on parser behavior only.

3. **Missing Keywords**: If new keywords are added in the future, they need to be added to `IsKeywordToken()`.

## Questions for Stakeholder

1. Should we also verify that this works in the null-conditional case (`obj?.property`)?
   - **Answer**: Yes, the current implementation handles this (same code path at line 1811).

2. Are there any keywords that should NOT be allowed as member names?
   - **Current behavior**: All keywords are allowed as member names after `.`
   - This matches Python/C# behavior where keywords can be member names.

## Success Criteria

- [ ] All existing parser tests pass
- [ ] New explicit tests for keyword-as-member-name are added and pass
- [ ] Test covers at least 5 different keyword categories (control flow, type, member, boolean, operator)
- [ ] Null-conditional variant is tested
- [ ] Chained member access with keywords is tested
