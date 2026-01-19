# Task List: Legacy Cleanup (F.7) and Source Span Foundation

**Document Version:** 1.0  
**Created:** January 2026  
**Target:** v0.1.x  
**Estimated Total Effort:** 3-5 days  
**Prerequisite:** All existing tests pass before starting

---

## Overview

This document contains two related task groups:

1. **Part A: Legacy Cleanup (F.7)** — Remove deprecated legacy tracking fields from `RoslynEmitter` now that CodeGenInfo is the primary path
2. **Part B: Source Span Foundation** — Add character-offset-based source tracking to enable future LSP, debugger, and error reporting improvements

Both tasks are designed to be incremental and reversible, with commit points after each major step.

---

## Design Decisions

### Two-Way Door Decisions (Reversible)

1. **TextSpan as a separate type** — Can be merged with Node properties later if needed
2. **Optional Span properties** — Existing code continues to work; spans are additive
3. **Gradual migration** — Old line/column properties remain; new spans added alongside
4. **SourceText as opt-in** — Only used when spans are needed; existing paths unchanged

### One-Way Door Decisions (Commit Now)

1. **TextSpan uses (Start, Length) not (Start, End)** — Matches Roslyn's design and is more efficient for substring operations. This is the standard in .NET compiler infrastructure.
2. **Character offsets are 0-based** — Standard in all compiler tooling
3. **Lexer tracks character position** — Required for all future tooling; no alternative

---

## Prerequisites Checklist

Before starting, verify:

```bash
cd /Users/anton/Documents/github/sharpy/src
dotnet build Sharpy.Compiler
dotnet test Sharpy.Compiler.Tests --no-build
```

- [ ] All tests pass
- [ ] Note the test count: _____ tests
- [ ] Create feature branch: `git checkout -b feature/f7-source-spans`

---

# Part A: Legacy Cleanup (F.7)

**Goal:** Remove deprecated legacy tracking fields from `RoslynEmitter` that are superseded by `CodeGenInfo`.

**Why now:** `UsePrecomputedCodeGenInfo` defaults to `true`, so the legacy fallback code is dead code. Removing it simplifies the emitter and prevents maintenance burden.

---

## A.1: Verify CodeGenInfo Is Always Used

**Files:** Multiple  
**Effort:** 30 minutes  
**Risk:** Low

### Task A.1.1: Search for Legacy Flag Usage

Verify that `UsePrecomputedCodeGenInfo` is always `true` in practice.

```bash
cd /Users/anton/Documents/github/sharpy
grep -rn "UsePrecomputedCodeGenInfo" --include="*.cs"
```

**Expected:** Should only find:
- Definition in `ProjectConfig.cs` (defaults to `true`)
- Usage in emitter (checking the flag)

**If you find:** Any code setting it to `false`, that code path needs updating first.

### Task A.1.2: Run Full Test Suite

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

**Record results:**
- Total tests: _____
- Passed: _____
- Failed: _____

All tests must pass before proceeding.

---

## A.2: Identify Legacy Code to Remove

**Files:** `RoslynEmitter.cs`, `RoslynEmitter.Statements.cs`, `RoslynEmitter.ModuleClass.cs`  
**Effort:** 30 minutes  
**Risk:** Low

### Task A.2.1: Document Legacy Fields

The following fields in `RoslynEmitter.cs` are marked as `[DEPRECATED]`:

```csharp
// These fields should be removed:
private readonly Dictionary<string, int> _variableVersions = new();
private readonly HashSet<string> _constVariables = new();
private readonly HashSet<string> _moduleConstVariables = new();
private readonly HashSet<string> _moduleVariables = new();
private HashSet<string> _variablesWithExecutionOrderIssues = new();
private readonly HashSet<string> _fromImportSymbols = new();
private readonly Dictionary<string, string> _importAliasToOriginal = new();
```

**Keep these fields** (still needed):
- `_declaredVariables` — Tracks variables declared in current scope
- `_moduleFieldNames` — Prevents duplicate field declarations
- `_classNames`, `_structNames`, `_stringEnumNames` — Track type definitions
- `_interfaceDefinitions` — For abstract class stub generation

### Task A.2.2: Search for Legacy Fallback Comments

```bash
grep -rn "LEGACY FALLBACK\|DEPRECATED" src/Sharpy.Compiler/CodeGen/ --include="*.cs"
```

Document all locations found:
- [ ] `RoslynEmitter.cs` line ~192 (module const fallback)
- [ ] `RoslynEmitter.cs` line ~199 (module variable fallback)
- [ ] `RoslynEmitter.Statements.cs` line ~440 (execution order fallback)
- [ ] Any others: _____

---

## A.3: Remove Legacy Tracking from GetMangledVariableName

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`  
**Effort:** 1 hour  
**Risk:** Medium (core naming logic)

### Task A.3.1: Understand Current Logic

Read the `GetMangledVariableName` method. It currently:
1. Checks `Symbol.CodeGenInfo` first (preferred path)
2. Falls back to `_constVariables`, `_moduleConstVariables`, `_moduleVariables`

The fallback should be removed.

### Task A.3.2: Remove Legacy Fallback Code

Edit `RoslynEmitter.cs`. Find the section with `// [LEGACY FALLBACK]` comments and remove it.

**Before (approximately):**
```csharp
// Check CodeGenInfo first (preferred)
if (symbol?.CodeGenInfo != null)
{
    return symbol.CodeGenInfo.CSharpName;
}

// Local const check
if (_constVariables.Contains(name))
{
    return NameMangler.ToConstantCase(name);
}

// [LEGACY FALLBACK]
// Check if this is a reference to a module-level const
if (_moduleConstVariables.Contains(name))
{
    return NameMangler.ToConstantCase(name);
}

// [LEGACY FALLBACK]
// Check if this is a reference to a module-level variable
if (_moduleVariables.Contains(name))
{
    return NameMangler.ToPascalCase(name);
}
```

**After:**
```csharp
// Check CodeGenInfo first (preferred path)
if (symbol?.CodeGenInfo != null)
{
    return symbol.CodeGenInfo.CSharpName;
}

// Local const check (still needed for local scope tracking)
if (_constVariables.Contains(name))
{
    return NameMangler.ToConstantCase(name);
}

// If no CodeGenInfo, fall back to simple naming
// This path should rarely be hit now that CodeGenInfo is always computed
```

**Important:** Keep the `_constVariables` check — it's used for LOCAL const variables tracked during emission.

### Task A.3.3: Run Tests

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

If tests fail:
- Check if the failure is in module-level variable naming
- The issue is likely that some symbol doesn't have CodeGenInfo populated
- Do NOT proceed until tests pass

**Commit Point (if tests pass):**
```bash
git add src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs
git commit -m "refactor(codegen): remove legacy module variable fallback from GetMangledVariableName

CodeGenInfo is now the primary path for all symbol naming.
Removed _moduleConstVariables and _moduleVariables fallback checks."
```

---

## A.4: Remove Legacy Execution Order Fallback

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`  
**Effort:** 30 minutes  
**Risk:** Medium

### Task A.4.1: Find and Remove Fallback

Search for the execution order fallback:
```bash
grep -n "LEGACY FALLBACK" src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs
```

**Before (approximately line 440):**
```csharp
var symbol = _context.LookupSymbol(varDecl.Name);
if (symbol != null && HasExecutionOrderIssues(symbol))
{
    return null;
}
// [LEGACY FALLBACK]
else if (symbol == null && _variablesWithExecutionOrderIssues.Contains(varDecl.Name))
{
    return null;
}
```

**After:**
```csharp
var symbol = _context.LookupSymbol(varDecl.Name);
if (symbol != null && HasExecutionOrderIssues(symbol))
{
    return null;
}
// Note: If symbol is null, we can't check execution order issues
// This shouldn't happen in well-typed code
```

### Task A.4.2: Run Tests

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

**Commit Point (if tests pass):**
```bash
git add src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs
git commit -m "refactor(codegen): remove legacy execution order fallback

Symbol.CodeGenInfo.HasExecutionOrderIssues is now the only path."
```

---

## A.5: Remove Deprecated Field Declarations

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`  
**Effort:** 30 minutes  
**Risk:** Medium

### Task A.5.1: Remove Field Declarations

Remove the following field declarations from `RoslynEmitter.cs`:

```csharp
// Remove these lines:
private readonly HashSet<string> _moduleConstVariables = new();
private readonly HashSet<string> _moduleVariables = new();
private HashSet<string> _variablesWithExecutionOrderIssues = new();
private readonly HashSet<string> _fromImportSymbols = new();
private readonly Dictionary<string, string> _importAliasToOriginal = new();
```

**Keep these:**
```csharp
// Keep - still used for local scope tracking:
private readonly HashSet<string> _declaredVariables = new();
private readonly Dictionary<string, int> _variableVersions = new();
private readonly HashSet<string> _constVariables = new();
private readonly HashSet<string> _moduleFieldNames = new();
```

### Task A.5.2: Fix Compilation Errors

After removing the fields, the compiler will show errors where they were used. For each error:

1. **If the code is populating the field** → Remove that code
2. **If the code is reading the field** → Replace with CodeGenInfo lookup or remove if it's a fallback

Common patterns to fix:
- `_moduleConstVariables.Add(...)` → Remove
- `_moduleVariables.Add(...)` → Remove  
- `_fromImportSymbols.Add(...)` → Remove
- `_importAliasToOriginal[...] = ...` → Remove

### Task A.5.3: Search for Any Remaining References

```bash
grep -rn "_moduleConstVariables\|_moduleVariables\|_variablesWithExecutionOrderIssues\|_fromImportSymbols\|_importAliasToOriginal" src/Sharpy.Compiler/CodeGen/
```

Fix any remaining references.

### Task A.5.4: Run Tests

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

**Troubleshooting:**
- If many tests fail, you may have removed code that was still being used
- Check `git diff` to see what was removed
- Focus on one field at a time if needed

**Commit Point (if tests pass):**
```bash
git add src/Sharpy.Compiler/CodeGen/
git commit -m "refactor(codegen): remove deprecated legacy tracking fields

Removed:
- _moduleConstVariables
- _moduleVariables  
- _variablesWithExecutionOrderIssues
- _fromImportSymbols
- _importAliasToOriginal

All module-level variable tracking now uses CodeGenInfo."
```

---

## A.6: Remove PopulateModuleVariableTrackingLegacy

**Files:** `RoslynEmitter.cs` or `RoslynEmitter.ModuleClass.cs`  
**Effort:** 30 minutes  
**Risk:** Low

### Task A.6.1: Find the Legacy Method

```bash
grep -rn "PopulateModuleVariableTrackingLegacy\|Legacy" src/Sharpy.Compiler/CodeGen/ --include="*.cs"
```

### Task A.6.2: Remove the Method

Delete the `PopulateModuleVariableTrackingLegacy` method entirely.

### Task A.6.3: Remove Calls to the Method

Search for any calls:
```bash
grep -rn "PopulateModuleVariableTrackingLegacy" src/Sharpy.Compiler/
```

Remove those calls. They should be inside a conditional like:
```csharp
if (!UsePrecomputedCodeGenInfo)
{
    PopulateModuleVariableTrackingLegacy();
}
```

Remove the entire conditional block.

### Task A.6.4: Run Tests

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

**Commit Point (if tests pass):**
```bash
git add src/Sharpy.Compiler/CodeGen/
git commit -m "refactor(codegen): remove PopulateModuleVariableTrackingLegacy method

CodeGenInfo is now the only path for module variable tracking."
```

---

## A.7: Clean Up Deprecation Comments

**Files:** `RoslynEmitter.cs`  
**Effort:** 15 minutes  
**Risk:** None

### Task A.7.1: Remove Stale Comments

Remove the large comment block at the top of `RoslynEmitter.cs` that says:

```csharp
// ============================================================
// LEGACY TRACKING FIELDS - Deprecated in favor of CodeGenInfo
// ...
// ============================================================
```

### Task A.7.2: Update Any Remaining Documentation

Search for remaining `[DEPRECATED]` or `[LEGACY` comments:
```bash
grep -rn "DEPRECATED\|LEGACY" src/Sharpy.Compiler/CodeGen/
```

Remove or update them.

### Task A.7.3: Final Test Run

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/CodeGen/
git commit -m "chore(codegen): remove legacy deprecation comments

Legacy cleanup complete. RoslynEmitter now uses CodeGenInfo exclusively."
```

---

## A.8: Part A Summary and Verification

### Task A.8.1: Verify All Tests Pass

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

**Record:**
- Tests before: _____
- Tests after: _____
- All passing: [ ] Yes / [ ] No

### Task A.8.2: Verify Code Compiles Cleanly

```bash
dotnet build Sharpy.Compiler --warnaserror
```

### Task A.8.3: Create Part A Summary Commit

```bash
git log --oneline HEAD~5..HEAD
```

If all looks good:
```bash
git tag part-a-complete
```

---

# Part B: Source Span Foundation

**Goal:** Add character-offset-based source tracking to enable future LSP, debugger, and error reporting improvements.

**Why now:** From the architecture addendum: *"Start #10 (Source Spans) during v0.1.x — retrofitting is extremely expensive."* The current line/column tracking is good but insufficient for:
- LSP position calculations
- Efficient range queries
- Source maps for debugging
- Error message improvements

**Design Philosophy:**
- **Additive, not breaking:** All existing code continues to work
- **Two-way door:** If this doesn't work out, we can remove TextSpan without breaking anything
- **Gradual adoption:** Start with infrastructure, migrate nodes incrementally

---

## B.1: Create Text Infrastructure

**Files:** New files in `src/Sharpy.Compiler/Text/`  
**Effort:** 1 hour  
**Risk:** None (new code only)

### Task B.1.1: Create Text Directory

```bash
mkdir -p /Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Text
```

### Task B.1.2: Create TextSpan Type

**File:** `src/Sharpy.Compiler/Text/TextSpan.cs`

```csharp
namespace Sharpy.Compiler.Text;

/// <summary>
/// A span of text in source code, represented as start position and length.
/// Uses character offsets (0-based).
/// 
/// Design note: Using (Start, Length) instead of (Start, End) matches
/// Roslyn's design and is more efficient for substring operations.
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>
    /// The exclusive end position (Start + Length).
    /// </summary>
    public int End => Start + Length;
    
    /// <summary>
    /// Represents no span / invalid span.
    /// </summary>
    public static TextSpan None => new(-1, 0);
    
    /// <summary>
    /// Whether this span is valid (has a non-negative start position).
    /// </summary>
    public bool IsValid => Start >= 0;
    
    /// <summary>
    /// Check if a position is within this span.
    /// </summary>
    public bool Contains(int position) => position >= Start && position < End;
    
    /// <summary>
    /// Check if this span contains another span entirely.
    /// </summary>
    public bool Contains(TextSpan other) => 
        other.IsValid && Start <= other.Start && other.End <= End;
    
    /// <summary>
    /// Check if this span overlaps with another span.
    /// </summary>
    public bool Overlaps(TextSpan other) =>
        other.IsValid && Start < other.End && other.Start < End;
    
    /// <summary>
    /// Create a span that covers both this span and another.
    /// </summary>
    public TextSpan Union(TextSpan other)
    {
        if (!IsValid) return other;
        if (!other.IsValid) return this;
        var start = Math.Min(Start, other.Start);
        var end = Math.Max(End, other.End);
        return new TextSpan(start, end - start);
    }
    
    /// <summary>
    /// Create a span from start and end positions.
    /// </summary>
    public static TextSpan FromBounds(int start, int end) =>
        new(start, end - start);
    
    public override string ToString() => 
        IsValid ? $"[{Start}..{End})" : "[invalid]";
}
```

### Task B.1.3: Create SourceText Type

**File:** `src/Sharpy.Compiler/Text/SourceText.cs`

```csharp
using System.Collections.Immutable;

namespace Sharpy.Compiler.Text;

/// <summary>
/// Represents source file content with efficient position ↔ line/column mapping.
/// 
/// Design note: This class is immutable and can be safely shared across threads,
/// which will be important for parallel compilation and LSP scenarios.
/// </summary>
public class SourceText
{
    public string FilePath { get; }
    public string Content { get; }
    public int Length => Content.Length;
    
    // Line start positions for efficient line/column lookup
    private readonly ImmutableArray<int> _lineStarts;
    
    public SourceText(string filePath, string content)
    {
        FilePath = filePath ?? string.Empty;
        Content = content ?? string.Empty;
        _lineStarts = ComputeLineStarts(content ?? string.Empty);
    }
    
    /// <summary>
    /// Number of lines in the source text.
    /// </summary>
    public int LineCount => _lineStarts.Length;
    
    /// <summary>
    /// Get line and column (1-based) for a character position (0-based).
    /// </summary>
    public (int Line, int Column) GetLineAndColumn(int position)
    {
        if (position < 0 || position > Content.Length)
            return (1, 1);
        
        // Binary search for the line containing this position
        var line = BinarySearchLineStarts(position);
        var column = position - _lineStarts[line] + 1; // 1-based column
        return (line + 1, column); // 1-based line
    }
    
    /// <summary>
    /// Get character position (0-based) for line and column (1-based).
    /// </summary>
    public int GetPosition(int line, int column)
    {
        if (line < 1 || line > _lineStarts.Length)
            return 0;
        
        var lineStart = _lineStarts[line - 1];
        return lineStart + column - 1;
    }
    
    /// <summary>
    /// Get the text content of a span.
    /// </summary>
    public string GetText(TextSpan span)
    {
        if (!span.IsValid || span.Start >= Content.Length)
            return string.Empty;
        
        var length = Math.Min(span.Length, Content.Length - span.Start);
        return Content.Substring(span.Start, length);
    }
    
    /// <summary>
    /// Get the entire line at the given 1-based line number.
    /// </summary>
    public string GetLine(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > _lineStarts.Length)
            return string.Empty;
        
        var start = _lineStarts[lineNumber - 1];
        var end = lineNumber < _lineStarts.Length 
            ? _lineStarts[lineNumber] 
            : Content.Length;
        
        // Trim trailing newline
        while (end > start && (Content[end - 1] == '\n' || Content[end - 1] == '\r'))
            end--;
        
        return Content.Substring(start, end - start);
    }
    
    /// <summary>
    /// Create a TextSpan from line/column ranges (1-based).
    /// </summary>
    public TextSpan GetSpan(int startLine, int startColumn, int endLine, int endColumn)
    {
        var start = GetPosition(startLine, startColumn);
        var end = GetPosition(endLine, endColumn);
        return TextSpan.FromBounds(start, end);
    }
    
    private static ImmutableArray<int> ComputeLineStarts(string content)
    {
        var lineStarts = ImmutableArray.CreateBuilder<int>();
        lineStarts.Add(0); // First line starts at position 0
        
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                lineStarts.Add(i + 1);
            }
            else if (content[i] == '\r')
            {
                // Handle \r\n as single newline
                if (i + 1 < content.Length && content[i + 1] == '\n')
                    i++;
                lineStarts.Add(i + 1);
            }
        }
        
        return lineStarts.ToImmutable();
    }
    
    private int BinarySearchLineStarts(int position)
    {
        var lo = 0;
        var hi = _lineStarts.Length - 1;
        
        while (lo < hi)
        {
            var mid = lo + (hi - lo + 1) / 2;
            if (_lineStarts[mid] <= position)
                lo = mid;
            else
                hi = mid - 1;
        }
        
        return lo;
    }
}
```

### Task B.1.4: Compile and Test

```bash
dotnet build Sharpy.Compiler
```

Should compile with no errors. No tests yet — we're just adding infrastructure.

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Text/
git commit -m "feat(text): add TextSpan and SourceText infrastructure

Introduces core types for source location tracking:
- TextSpan: Character offset-based span (Start, Length)
- SourceText: Source file with efficient position ↔ line/column mapping

This is foundation work for LSP and debugger support (Rec #10).
No breaking changes - purely additive."
```

---

## B.2: Add Unit Tests for Text Infrastructure

**File:** New file in tests  
**Effort:** 1 hour  
**Risk:** None

### Task B.2.1: Create Test File

**File:** `src/Sharpy.Compiler.Tests/Text/TextSpanTests.cs`

```csharp
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Text;

public class TextSpanTests
{
    [Fact]
    public void TextSpan_BasicProperties()
    {
        var span = new TextSpan(10, 5);
        
        Assert.Equal(10, span.Start);
        Assert.Equal(5, span.Length);
        Assert.Equal(15, span.End);
        Assert.True(span.IsValid);
    }
    
    [Fact]
    public void TextSpan_None_IsInvalid()
    {
        var span = TextSpan.None;
        
        Assert.False(span.IsValid);
        Assert.Equal(-1, span.Start);
    }
    
    [Fact]
    public void TextSpan_Contains_Position()
    {
        var span = new TextSpan(10, 5); // [10..15)
        
        Assert.False(span.Contains(9));
        Assert.True(span.Contains(10));
        Assert.True(span.Contains(14));
        Assert.False(span.Contains(15));
    }
    
    [Fact]
    public void TextSpan_Contains_OtherSpan()
    {
        var outer = new TextSpan(10, 10); // [10..20)
        var inner = new TextSpan(12, 3);  // [12..15)
        var partial = new TextSpan(15, 10); // [15..25)
        
        Assert.True(outer.Contains(inner));
        Assert.False(outer.Contains(partial));
        Assert.False(inner.Contains(outer));
    }
    
    [Fact]
    public void TextSpan_Overlaps()
    {
        var span1 = new TextSpan(10, 5); // [10..15)
        var span2 = new TextSpan(12, 5); // [12..17)
        var span3 = new TextSpan(15, 5); // [15..20) - no overlap
        
        Assert.True(span1.Overlaps(span2));
        Assert.True(span2.Overlaps(span1));
        Assert.False(span1.Overlaps(span3));
    }
    
    [Fact]
    public void TextSpan_Union()
    {
        var span1 = new TextSpan(10, 5);  // [10..15)
        var span2 = new TextSpan(20, 5);  // [20..25)
        
        var union = span1.Union(span2);
        
        Assert.Equal(10, union.Start);
        Assert.Equal(15, union.Length);
        Assert.Equal(25, union.End);
    }
    
    [Fact]
    public void TextSpan_FromBounds()
    {
        var span = TextSpan.FromBounds(10, 15);
        
        Assert.Equal(10, span.Start);
        Assert.Equal(5, span.Length);
        Assert.Equal(15, span.End);
    }
}

public class SourceTextTests
{
    private const string TestSource = @"line one
line two
line three";

    [Fact]
    public void SourceText_LineCount()
    {
        var source = new SourceText("test.spy", TestSource);
        
        Assert.Equal(3, source.LineCount);
    }
    
    [Fact]
    public void SourceText_GetLineAndColumn()
    {
        var source = new SourceText("test.spy", TestSource);
        
        // Position 0 = start of "line one"
        Assert.Equal((1, 1), source.GetLineAndColumn(0));
        
        // Position 5 = 'o' in "line one"  
        Assert.Equal((1, 6), source.GetLineAndColumn(5));
        
        // Position 9 = start of "line two" (after newline)
        Assert.Equal((2, 1), source.GetLineAndColumn(9));
    }
    
    [Fact]
    public void SourceText_GetPosition()
    {
        var source = new SourceText("test.spy", TestSource);
        
        // Line 1, Column 1 = position 0
        Assert.Equal(0, source.GetPosition(1, 1));
        
        // Line 2, Column 1 = position 9
        Assert.Equal(9, source.GetPosition(2, 1));
    }
    
    [Fact]
    public void SourceText_GetText()
    {
        var source = new SourceText("test.spy", TestSource);
        
        var span = new TextSpan(0, 8);
        Assert.Equal("line one", source.GetText(span));
    }
    
    [Fact]
    public void SourceText_GetLine()
    {
        var source = new SourceText("test.spy", TestSource);
        
        Assert.Equal("line one", source.GetLine(1));
        Assert.Equal("line two", source.GetLine(2));
        Assert.Equal("line three", source.GetLine(3));
    }
    
    [Fact]
    public void SourceText_RoundTrip_Position()
    {
        var source = new SourceText("test.spy", TestSource);
        
        // Test round-trip: position → line/col → position
        for (var pos = 0; pos < TestSource.Length; pos++)
        {
            var (line, col) = source.GetLineAndColumn(pos);
            var roundTrip = source.GetPosition(line, col);
            Assert.Equal(pos, roundTrip);
        }
    }
}
```

### Task B.2.2: Run Tests

```bash
dotnet test Sharpy.Compiler.Tests --filter "FullyQualifiedName~Text"
```

All tests should pass.

**Commit Point:**
```bash
git add src/Sharpy.Compiler.Tests/Text/
git commit -m "test(text): add unit tests for TextSpan and SourceText

Comprehensive tests for:
- TextSpan operations (contains, overlaps, union)
- SourceText line/column conversion
- Round-trip position verification"
```

---

## B.3: Add Position Tracking to Lexer

**File:** `src/Sharpy.Compiler/Lexer/Token.cs`, `src/Sharpy.Compiler/Lexer/Lexer.cs`  
**Effort:** 1-2 hours  
**Risk:** Low-Medium (changes Token but is additive)

### Task B.3.1: Extend Token with Position

**File:** `src/Sharpy.Compiler/Lexer/Token.cs`

Add character position tracking to the Token record. We keep the existing `Line` and `Column` for backward compatibility.

```csharp
// Add at the top of the file:
using Sharpy.Compiler.Text;

// Modify the Token record to add optional Span:
public record Token
{
    public TokenType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }
    
    // NEW: Character offset-based span
    public TextSpan Span { get; init; } = TextSpan.None;
    
    // Existing constructor (backward compatible)
    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
        Span = TextSpan.None; // Not tracked
    }
    
    // NEW: Constructor with span
    public Token(TokenType type, string value, int line, int column, TextSpan span)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
        Span = span;
    }
}
```

### Task B.3.2: Update Lexer to Track Position

**File:** `src/Sharpy.Compiler/Lexer/Lexer.cs`

The lexer needs to track the current character position in addition to line/column.

Add a field:
```csharp
private int _position = 0; // Current character offset in source
```

Update `Advance()` to track position:
```csharp
private void Advance()
{
    if (_current < _source.Length)
    {
        _position++;
        // ... existing line/column tracking ...
    }
    _current++;
}
```

Update token creation to include span. For example, in methods that create tokens:
```csharp
// Before:
return new Token(TokenType.Integer, numberStr, _line, startColumn);

// After:
var span = new TextSpan(startPosition, _position - startPosition);
return new Token(TokenType.Integer, numberStr, _line, startColumn, span);
```

**Important:** This is a larger change. Do it incrementally:
1. First, add the `_position` field and update `Advance()`
2. Then update one token type at a time
3. Test after each change

### Task B.3.3: Run Tests After Each Change

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

All existing tests should still pass because:
- The old constructor is still available
- Span defaults to `TextSpan.None` for backward compatibility

### Task B.3.4: Verify Span Tracking

Add a simple test to verify spans are being tracked:

**File:** `src/Sharpy.Compiler.Tests/Lexer/LexerSpanTests.cs`

```csharp
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

public class LexerSpanTests
{
    [Fact]
    public void Lexer_TracksTokenSpans()
    {
        var source = "x = 42";
        var lexer = new Lexer(source);
        var tokens = lexer.TokenizeAll();
        
        // x
        Assert.Equal("x", tokens[0].Value);
        Assert.True(tokens[0].Span.IsValid);
        Assert.Equal(0, tokens[0].Span.Start);
        Assert.Equal(1, tokens[0].Span.Length);
        
        // =
        Assert.Equal("=", tokens[1].Value);
        Assert.True(tokens[1].Span.IsValid);
        Assert.Equal(2, tokens[1].Span.Start);
        
        // 42
        Assert.Equal("42", tokens[2].Value);
        Assert.True(tokens[2].Span.IsValid);
        Assert.Equal(4, tokens[2].Span.Start);
        Assert.Equal(2, tokens[2].Span.Length);
    }
}
```

**Note:** If you haven't updated all token types yet, this test might fail. That's okay — update incrementally.

**Commit Point (after lexer changes compile and basic tests pass):**
```bash
git add src/Sharpy.Compiler/Lexer/ src/Sharpy.Compiler.Tests/Lexer/
git commit -m "feat(lexer): add character position tracking to tokens

- Add TextSpan property to Token
- Track character offset in Lexer
- Backward compatible: old constructor still works, span defaults to None

Foundation for source span tracking throughout compilation pipeline."
```

---

## B.4: Add ILocatable Interface

**File:** `src/Sharpy.Compiler/Text/ILocatable.cs`  
**Effort:** 30 minutes  
**Risk:** None

### Task B.4.1: Create Interface

This interface will be implemented by AST nodes that have source locations.

**File:** `src/Sharpy.Compiler/Text/ILocatable.cs`

```csharp
namespace Sharpy.Compiler.Text;

/// <summary>
/// Interface for elements that have a source location.
/// Implemented by AST nodes, tokens, and symbols.
/// </summary>
public interface ILocatable
{
    /// <summary>
    /// The span of this element in the source text.
    /// May be TextSpan.None if location is not tracked.
    /// </summary>
    TextSpan Span { get; }
}
```

### Task B.4.2: Make Token Implement ILocatable

Update `Token.cs`:

```csharp
public record Token : ILocatable
{
    // ... existing code ...
}
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Text/ src/Sharpy.Compiler/Lexer/Token.cs
git commit -m "feat(text): add ILocatable interface

Interface for elements with source locations.
Token now implements ILocatable."
```

---

## B.5: Add Optional Span to AST Node Base

**File:** `src/Sharpy.Compiler/Parser/Ast/Node.cs`  
**Effort:** 30 minutes  
**Risk:** Low

### Task B.5.1: Add Span Property to Node

The existing Node has `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`. We add an optional `Span` property that can be computed from a SourceText if needed, or set directly during parsing.

```csharp
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Base class for all AST nodes
/// </summary>
public abstract record Node : ILocatable
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    
    /// <summary>
    /// Character offset-based span. May be TextSpan.None if not tracked.
    /// This is optional for backward compatibility.
    /// </summary>
    public TextSpan Span { get; init; } = TextSpan.None;
}
```

### Task B.5.2: Run All Tests

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

All tests should pass — we only added an optional property with a default value.

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Ast/Node.cs
git commit -m "feat(ast): add optional Span property to Node base class

- Node now implements ILocatable
- Span defaults to TextSpan.None for backward compatibility
- Existing Line/Column properties unchanged

This enables gradual migration to span-based location tracking."
```

---

## B.6: Update Parser to Populate Spans (Incremental)

**Files:** `src/Sharpy.Compiler/Parser/Parser.cs` and partials  
**Effort:** 2-4 hours (can be done incrementally over time)  
**Risk:** Medium

### Task B.6.1: Add SourceText to Parser

The parser needs access to SourceText to compute spans from tokens.

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`

```csharp
// Add field:
private readonly SourceText? _sourceText;

// Add constructor overload:
public Parser(List<Token> tokens, SourceText? sourceText = null)
{
    _tokens = tokens;
    _sourceText = sourceText;
    // ... existing initialization ...
}
```

### Task B.6.2: Add Helper Method for Computing Node Spans

```csharp
/// <summary>
/// Compute a span that covers a range of tokens.
/// Returns TextSpan.None if source text is not available or tokens don't have spans.
/// </summary>
private TextSpan ComputeSpan(Token start, Token end)
{
    if (!start.Span.IsValid || !end.Span.IsValid)
        return TextSpan.None;
    
    return TextSpan.FromBounds(start.Span.Start, end.Span.End);
}

/// <summary>
/// Compute a span from a single token.
/// </summary>
private TextSpan ComputeSpan(Token token) => token.Span;
```

### Task B.6.3: Update One Node Type as Example

Pick a simple node type and update it to use spans. For example, `Identifier`:

```csharp
// In the parsing code for identifiers:
var token = Current();
Advance();
return new Identifier
{
    Name = token.Value,
    LineStart = token.Line,
    ColumnStart = token.Column,
    LineEnd = token.Line,
    ColumnEnd = token.Column + token.Value.Length,
    Span = token.Span  // NEW: use token's span
};
```

### Task B.6.4: Test and Iterate

Run tests after each node type is updated:
```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

**Note:** This is a large task. It's okay to do it incrementally over multiple sessions. The key is that:
1. No existing tests break
2. New code that creates AST nodes includes spans when possible

### Task B.6.5: Commit Incrementally

After updating a group of node types:
```bash
git add src/Sharpy.Compiler/Parser/
git commit -m "feat(parser): add span tracking to [NodeType] nodes

Spans are now populated for [list of node types] during parsing.
Backward compatible: spans default to None if not available."
```

---

## B.7: Part B Summary

### Task B.7.1: Run Full Test Suite

```bash
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

### Task B.7.2: Document Migration Status

Create or update a tracking file:

**File:** `docs/implementation_planning/source_span_migration_status.md`

```markdown
# Source Span Migration Status

## Completed
- [x] TextSpan type
- [x] SourceText type
- [x] ILocatable interface
- [x] Token span tracking
- [x] Node base class Span property

## In Progress
- [ ] Parser span population (partial)

## Node Types with Spans
- [x] Identifier
- [ ] IntegerLiteral
- [ ] FloatLiteral
- [ ] ... (list all node types)

## Node Types Without Spans (need migration)
- [ ] FunctionDef
- [ ] ClassDef
- [ ] ... (list remaining)
```

### Task B.7.3: Final Commit

```bash
git add docs/
git commit -m "docs: add source span migration tracking

Track progress of span propagation through AST nodes."
```

---

# Final Checklist

## Part A Completion
- [ ] All legacy fields removed from RoslynEmitter
- [ ] PopulateModuleVariableTrackingLegacy removed
- [ ] All [LEGACY FALLBACK] comments removed
- [ ] All tests pass

## Part B Foundation Complete
- [ ] TextSpan and SourceText types created
- [ ] ILocatable interface created
- [ ] Token tracks character offsets
- [ ] Node base class has optional Span
- [ ] Parser infrastructure for span computation
- [ ] At least one node type populates spans
- [ ] All tests pass

## Overall
- [ ] Feature branch ready for merge
- [ ] No regressions in test count
- [ ] Code compiles without warnings

---

# Rollback Procedures

## If Part A Breaks Something

```bash
# Revert to before Part A changes
git revert HEAD~N  # where N is number of Part A commits

# Or restore specific files
git checkout main -- src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs
```

## If Part B Breaks Something

Part B is designed to be fully reversible:
1. The `Span` property defaults to `TextSpan.None`
2. All existing code ignores spans
3. Simply stop populating spans to effectively disable the feature

```bash
# Remove span tracking (keeps infrastructure for future)
git checkout main -- src/Sharpy.Compiler/Parser/
git checkout main -- src/Sharpy.Compiler/Lexer/Lexer.cs
```

---

# Future Work (Not in This Task)

These items are enabled by this foundation but are separate tasks:

1. **Complete span migration** — Update all parser productions to populate spans
2. **Source maps for debugging** — Map C# locations back to Sharpy locations  
3. **Error message improvements** — Use spans for better error underlining
4. **LSP infrastructure** — Position-based symbol lookup
5. **SourceText caching** — Store SourceText in CompilationUnit

---

# References

- `docs/implementation_planning/architecture_review_addendum_future_features.md` (Rec #10)
- `docs/implementation_planning/codegen_follow_up_tasks.md` (F.7)
- Roslyn source: https://github.com/dotnet/roslyn (for TextSpan design inspiration)
