# Architecture Review Addendum: Future Feature Considerations

**Date:** January 2026  
**Status:** Planning Reference  
**Relates To:** `architecture_review_and_recommendations.md`

---

## Overview

This document extends the original architecture review with additional recommendations specifically targeting future Sharpy features beyond v0.1.x. These recommendations are designed to be implemented incrementally—some should begin during v0.1.x to avoid costly retrofitting, while others can wait until the features they enable are actively being developed.

### Future Features Considered

| Feature | Version Target | Description |
|---------|---------------|-------------|
| **Tagged Unions (ADTs)** | v0.2.x | Sum types with pattern matching and exhaustiveness checking |
| **Async/Await** | v0.2.x+ | Coroutine-based asynchronous programming with Task mapping |
| **LSP (Language Server Protocol)** | v0.2.x+ | IDE integration for completions, hover, go-to-definition, diagnostics |
| **Source/Debug Tracking** | v0.2.x+ | PDB generation, source maps, debugger breakpoint support |
| **Parallel Compilation** | v0.2.x+ | Multi-threaded compilation for large projects |
| **Incremental/Resumptive Compilation** | v0.2.x+ | Partial recompilation for fast Unity iteration |

### Architectural Requirements by Feature

| Feature | Key Architectural Needs |
|---------|------------------------|
| **ADTs / Tagged Unions** | Type hierarchy extension, pattern exhaustiveness checking, discriminator codegen |
| **Async/Await** | Control flow graph (CFG), state machine generation, async context tracking |
| **LSP** | Incremental parsing, error recovery, symbol index, position-aware queries |
| **Source/Debug Tracking** | Span preservation through pipeline, PDB/source map generation |
| **Parallel Compilation** | Thread-safe structures, clear ownership, minimal shared state |
| **Incremental/Resumptive** | Dependency graph, content hashing, cache invalidation, partial analysis |

---

## Additional Recommendations (#7-12)

These recommendations extend the original six from `architecture_review_and_recommendations.md`.

---

### Recommendation #7: Immutable AST and Semantic Model

**Priority:** HIGH for LSP, Parallel, Incremental  
**When to Start:** v0.1.x (incremental migration)  
**Effort:** Large (but can be done incrementally)  
**Impact:** Critical for future tooling

#### Problem

The current architecture has mutable AST nodes and symbols that get modified during analysis:

```csharp
// Current (mutable):
public class FunctionDef : Statement
{
    public string Name { get; set; }  // Mutable!
    public List<Parameter> Parameters { get; set; }  // Mutable list!
    public TypeAnnotation? ReturnType { get; set; }
}
```

This creates problems for:
- **LSP**: Can't safely share AST across analysis threads; edits to one version affect others
- **Parallel**: Mutable state requires locking or careful coordination
- **Incremental**: Can't compare old vs. new state; can't cache intermediate results safely

#### Proposed Solution

Migrate to immutable record types with `ImmutableArray` for collections:

```csharp
// New: src/Sharpy.Compiler/Syntax/FunctionDefSyntax.cs
public sealed record FunctionDefSyntax(
    string Name,
    ImmutableArray<ParameterSyntax> Parameters,
    TypeAnnotationSyntax? ReturnType,
    ImmutableArray<StatementSyntax> Body,
    ImmutableArray<DecoratorSyntax> Decorators,
    TextSpan Span  // Position tracking built-in (see Rec #10)
) : StatementSyntax
{
    // "With" methods for creating modified copies
    public FunctionDefSyntax WithBody(ImmutableArray<StatementSyntax> newBody) =>
        this with { Body = newBody };
}

// Semantic binding stored separately (not mutating syntax):
public sealed record BoundFunctionDef(
    FunctionDefSyntax Syntax,
    FunctionSymbol Symbol,
    ImmutableArray<BoundStatement> BoundBody,
    ImmutableArray<Diagnostic> Diagnostics
) : BoundNode;
```

#### Migration Strategy

1. **Phase 1 (v0.1.x):** New AST nodes use immutable patterns; existing nodes unchanged
2. **Phase 2 (v0.1.x-v0.2.x):** Gradually migrate existing nodes, starting with leaf nodes
3. **Phase 3 (v0.2.x):** Complete migration; remove all mutable setters

#### Benefits

- Thread-safe sharing between parallel compilation units
- Cache-friendly (identical inputs → structurally equal objects)
- Easy diff detection for incremental compilation
- LSP can hold multiple AST versions simultaneously without interference
- Enables structural sharing for memory efficiency

---

### Recommendation #8: Explicit Dependency Graph

**Priority:** HIGH for Incremental, Parallel  
**When to Start:** Before 0.1.12 (extends CompilationUnit model from Rec #4)  
**Effort:** Medium  
**Impact:** Critical for build performance

#### Problem

Currently, dependencies are implicit—discovered during import resolution and scattered across multiple data structures. This makes it impossible to:
- Determine which files can be compiled in parallel
- Know which files need recompilation when one file changes
- Build an efficient incremental compilation cache

#### Proposed Solution

```csharp
// New: src/Sharpy.Compiler/Model/DependencyGraph.cs
public class DependencyGraph
{
    // File-level dependencies (file path → set of file paths it depends on)
    public IReadOnlyDictionary<string, ImmutableHashSet<string>> FileDependencies { get; }
    
    // Reverse dependencies (file path → set of file paths that depend on it)
    public IReadOnlyDictionary<string, ImmutableHashSet<string>> ReverseDependencies { get; }
    
    // Type-level dependencies for finer granularity (optional, for advanced incremental)
    public IReadOnlyDictionary<TypeId, ImmutableHashSet<TypeId>> TypeDependencies { get; }
    
    /// <summary>
    /// Compute topologically sorted build order.
    /// Files with no dependencies come first.
    /// </summary>
    public IReadOnlyList<string> GetBuildOrder();
    
    /// <summary>
    /// Find all files that need recompilation when the given file changes.
    /// Includes transitive dependents.
    /// </summary>
    public ImmutableHashSet<string> GetAffectedFiles(string changedFile);
    
    /// <summary>
    /// Find all files affected by changes to any of the given files.
    /// </summary>
    public ImmutableHashSet<string> GetAffectedFiles(IEnumerable<string> changedFiles);
    
    /// <summary>
    /// Identify groups of files that can be compiled in parallel.
    /// Each group contains files with no dependencies on each other.
    /// Groups are returned in dependency order.
    /// </summary>
    public IReadOnlyList<ImmutableHashSet<string>> GetParallelizableGroups();
    
    /// <summary>
    /// Check for circular dependencies.
    /// </summary>
    public IReadOnlyList<ImmutableArray<string>> DetectCycles();
}

// Builder for constructing the graph during compilation
public class DependencyGraphBuilder
{
    public void AddFileDependency(string fromFile, string toFile);
    public void AddTypeDependency(TypeId fromType, TypeId toType);
    public DependencyGraph Build();
}
```

#### Integration with CompilationUnit

```csharp
// Extended CompilationUnit from Rec #4
public class CompilationUnit
{
    // ... existing fields from Rec #4 ...
    
    /// <summary>
    /// SHA-256 hash of source content for change detection.
    /// </summary>
    public string ContentHash { get; }
    
    /// <summary>
    /// Direct dependencies: files this unit imports or references types from.
    /// Computed during import resolution.
    /// </summary>
    public ImmutableHashSet<string> DirectDependencies { get; }
    
    /// <summary>
    /// Whether this unit's content has changed since last compilation.
    /// </summary>
    public bool IsStale(CompilationCache cache) =>
        cache.GetHash(FilePath) != ContentHash;
}

// Project model includes the graph
public class ProjectModel
{
    // ... existing fields ...
    
    public DependencyGraph DependencyGraph { get; }
    
    /// <summary>
    /// Rebuild dependency graph after file changes.
    /// </summary>
    public ProjectModel RebuildDependencyGraph();
}
```

#### Use Cases

**Parallel Compilation:**
```csharp
var groups = project.DependencyGraph.GetParallelizableGroups();
foreach (var group in groups)
{
    // All files in this group can be compiled simultaneously
    await Task.WhenAll(group.Select(file => CompileFileAsync(file)));
}
```

**Incremental Compilation:**
```csharp
var changedFiles = DetectChangedFiles(project);
var affectedFiles = project.DependencyGraph.GetAffectedFiles(changedFiles);
// Only recompile affected files, in correct order
foreach (var file in project.DependencyGraph.GetBuildOrder().Where(affectedFiles.Contains))
{
    await RecompileFileAsync(file);
}
```

---

### Recommendation #9: Control Flow Graph (CFG) Infrastructure

**Priority:** HIGH for Async, ADTs, Advanced Analysis  
**When to Start:** v0.2.x (before ADTs and async)  
**Effort:** Large  
**Impact:** Enables multiple advanced features

#### Problem

Several future features require explicit control flow analysis:
- **Async/await:** State machine generation requires splitting code at await boundaries
- **Pattern matching:** Exhaustiveness checking requires analyzing all possible paths
- **Dead code detection:** Identifying unreachable code requires CFG analysis
- **Definite assignment:** Ensuring variables are assigned before use

#### Proposed Solution

```csharp
// New: src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraph.cs
public class ControlFlowGraph
{
    public BasicBlock Entry { get; }
    public BasicBlock Exit { get; }
    public IReadOnlyList<BasicBlock> Blocks { get; }
    public IReadOnlyList<ControlFlowEdge> Edges { get; }
    
    /// <summary>
    /// Find all paths from entry to exit.
    /// </summary>
    public IEnumerable<ImmutableArray<BasicBlock>> EnumeratePaths();
    
    /// <summary>
    /// Compute dominator tree for the CFG.
    /// </summary>
    public DominatorTree ComputeDominators();
}

public class BasicBlock
{
    public int Id { get; }
    public ImmutableArray<BoundStatement> Statements { get; }
    public IReadOnlyList<BasicBlock> Predecessors { get; }
    public IReadOnlyList<BasicBlock> Successors { get; }
    
    /// <summary>
    /// For async: true if this block contains an await expression.
    /// </summary>
    public bool ContainsAwait { get; }
    
    /// <summary>
    /// For pattern matching: information about match arm this block represents.
    /// </summary>
    public PatternMatchInfo? PatternInfo { get; }
    
    /// <summary>
    /// The terminator instruction (branch, return, throw, etc.)
    /// </summary>
    public BlockTerminator Terminator { get; }
}

public abstract record BlockTerminator;
public sealed record ReturnTerminator(BoundExpression? Value) : BlockTerminator;
public sealed record BranchTerminator(BasicBlock Target) : BlockTerminator;
public sealed record ConditionalBranchTerminator(
    BoundExpression Condition,
    BasicBlock TrueTarget,
    BasicBlock FalseTarget
) : BlockTerminator;
public sealed record SwitchTerminator(
    BoundExpression Value,
    ImmutableArray<(object? Pattern, BasicBlock Target)> Cases,
    BasicBlock? DefaultTarget
) : BlockTerminator;
public sealed record ThrowTerminator(BoundExpression Exception) : BlockTerminator;

public record ControlFlowEdge(BasicBlock From, BasicBlock To, EdgeKind Kind);
public enum EdgeKind { Unconditional, ConditionalTrue, ConditionalFalse, SwitchCase, Exception }
```

#### CFG Builder

```csharp
// New: src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs
public class ControlFlowGraphBuilder
{
    /// <summary>
    /// Build CFG from a bound function body.
    /// </summary>
    public ControlFlowGraph Build(BoundBlockStatement body);
    
    /// <summary>
    /// Build CFG from a bound expression (for expression-bodied members).
    /// </summary>
    public ControlFlowGraph Build(BoundExpression body);
}
```

#### Analysis Extensions

```csharp
// New: src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowAnalysis.cs
public static class ControlFlowAnalysis
{
    /// <summary>
    /// For async state machine generation: identify regions between await points.
    /// </summary>
    public static IReadOnlyList<AsyncStateRegion> IdentifyAsyncRegions(ControlFlowGraph cfg);
    
    /// <summary>
    /// For pattern matching: check if all cases are covered.
    /// </summary>
    public static ExhaustivenessResult CheckExhaustiveness(
        ControlFlowGraph cfg, 
        BoundMatchExpression match,
        TypeInfo scrutineeType);
    
    /// <summary>
    /// Find unreachable blocks.
    /// </summary>
    public static ImmutableHashSet<BasicBlock> FindUnreachableBlocks(ControlFlowGraph cfg);
    
    /// <summary>
    /// Check definite assignment for a variable.
    /// </summary>
    public static DefiniteAssignmentResult CheckDefiniteAssignment(
        ControlFlowGraph cfg,
        VariableSymbol variable);
}

public record AsyncStateRegion(
    int StateId,
    ImmutableArray<BasicBlock> Blocks,
    BoundAwaitExpression? AwaitAtEnd
);

public record ExhaustivenessResult(
    bool IsExhaustive,
    ImmutableArray<Pattern> MissingPatterns
);
```

#### Use Case: Async State Machine Generation

```csharp
// Conceptual flow for async compilation
public class AsyncLowering
{
    public BoundStatement Lower(BoundFunctionDef asyncFunc)
    {
        var cfg = new ControlFlowGraphBuilder().Build(asyncFunc.Body);
        var regions = ControlFlowAnalysis.IdentifyAsyncRegions(cfg);
        
        // Generate state machine with one state per region
        return GenerateStateMachine(asyncFunc, regions);
    }
}
```

#### Use Case: Pattern Exhaustiveness

```csharp
// During semantic analysis of match expressions
public void CheckMatch(BoundMatchExpression match)
{
    var cfg = new ControlFlowGraphBuilder().Build(match);
    var result = ControlFlowAnalysis.CheckExhaustiveness(cfg, match, match.Scrutinee.Type);
    
    if (!result.IsExhaustive)
    {
        foreach (var missing in result.MissingPatterns)
        {
            AddError($"Match is not exhaustive. Missing pattern: {missing}");
        }
    }
}
```

---

### Recommendation #10: Source Span Preservation Throughout Pipeline

**Priority:** HIGH for LSP, Debuggers  
**When to Start:** Immediately (v0.1.x) — retrofitting is very expensive  
**Effort:** Medium (if started now), Very Large (if retrofitted later)  
**Impact:** Critical for developer experience

#### Problem

For useful tooling, every element in the compilation pipeline must know its source location:
- **LSP hover:** Need to know what symbol is at cursor position
- **LSP go-to-definition:** Need to know where symbols are defined
- **Error messages:** Need accurate line/column information
- **Debugger breakpoints:** Need to map C# locations back to Sharpy locations
- **Debugger stepping:** Need source-level stepping through Sharpy code

#### Proposed Solution

##### Core Types

```csharp
// New: src/Sharpy.Compiler/Text/TextSpan.cs
/// <summary>
/// A span of text in a source file, represented as start position and length.
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
    
    public static TextSpan None => new(-1, 0);
    
    public bool IsValid => Start >= 0;
    
    public bool Contains(int position) => position >= Start && position < End;
    
    public bool Overlaps(TextSpan other) => 
        Start < other.End && other.Start < End;
    
    public TextSpan Union(TextSpan other) =>
        new(Math.Min(Start, other.Start), Math.Max(End, other.End) - Math.Min(Start, other.Start));
}

// New: src/Sharpy.Compiler/Text/FileLocation.cs
/// <summary>
/// A location in a specific source file, with both offset and line/column.
/// </summary>
public readonly record struct FileLocation(
    string FilePath,
    TextSpan Span,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn)
{
    public static FileLocation None => new("", TextSpan.None, 0, 0, 0, 0);
    
    public bool IsValid => Span.IsValid && !string.IsNullOrEmpty(FilePath);
    
    public override string ToString() => 
        $"{FilePath}({StartLine},{StartColumn})-({EndLine},{EndColumn})";
}

// New: src/Sharpy.Compiler/Text/SourceText.cs
/// <summary>
/// Represents source file content with efficient position ↔ line/column mapping.
/// </summary>
public class SourceText
{
    public string FilePath { get; }
    public string Content { get; }
    
    private readonly ImmutableArray<int> _lineStarts;
    
    public SourceText(string filePath, string content)
    {
        FilePath = filePath;
        Content = content;
        _lineStarts = ComputeLineStarts(content);
    }
    
    public (int Line, int Column) GetLineAndColumn(int position);
    public int GetPosition(int line, int column);
    public FileLocation GetLocation(TextSpan span);
    public string GetText(TextSpan span);
}
```

##### Integration with Syntax Nodes

```csharp
// Base interface for all locatable elements
public interface ILocatable
{
    TextSpan Span { get; }
}

// All syntax nodes include span
public abstract record SyntaxNode : ILocatable
{
    public abstract TextSpan Span { get; }
}

// Example: expression with span
public sealed record BinaryOpSyntax(
    ExpressionSyntax Left,
    BinaryOperator Operator,
    ExpressionSyntax Right,
    TextSpan Span  // Spans from Left.Span.Start to Right.Span.End
) : ExpressionSyntax;

// Tokens also have spans
public readonly record struct Token(
    TokenType Type,
    string Text,
    object? Value,
    TextSpan Span
);
```

##### Integration with Bound Nodes

```csharp
// Bound nodes preserve syntax location
public abstract record BoundNode : ILocatable
{
    public abstract TextSpan Span { get; }
    
    /// <summary>
    /// The syntax node this was bound from (null for synthesized nodes).
    /// </summary>
    public abstract SyntaxNode? Syntax { get; }
}

public sealed record BoundBinaryExpression(
    BoundExpression Left,
    BinaryOperator Operator,
    BoundExpression Right,
    TypeInfo Type,
    BinaryOpSyntax Syntax
) : BoundExpression
{
    public override TextSpan Span => Syntax.Span;
    SyntaxNode? BoundNode.Syntax => Syntax;
}
```

##### Source Map for Code Generation

```csharp
// New: src/Sharpy.Compiler/CodeGen/SourceMap.cs
/// <summary>
/// Maps locations in generated C# code back to Sharpy source locations.
/// </summary>
public class SourceMap
{
    public record Mapping(
        FileLocation SharpyLocation,
        FileLocation CSharpLocation
    );
    
    public IReadOnlyList<Mapping> Mappings { get; }
    
    /// <summary>
    /// Find Sharpy location for a C# location.
    /// </summary>
    public FileLocation? MapToCSharp(FileLocation sharpyLocation);
    
    /// <summary>
    /// Find C# location for a Sharpy location.
    /// </summary>
    public FileLocation? MapToSharpy(FileLocation csharpLocation);
}

// New: src/Sharpy.Compiler/CodeGen/SourceMapBuilder.cs
public class SourceMapBuilder
{
    /// <summary>
    /// Record a mapping from Sharpy location to C# location.
    /// </summary>
    public void AddMapping(FileLocation sharpyLoc, FileLocation csharpLoc);
    
    /// <summary>
    /// Build the final source map.
    /// </summary>
    public SourceMap Build();
    
    /// <summary>
    /// Generate PDB-compatible sequence points.
    /// </summary>
    public ImmutableArray<SequencePoint> GenerateSequencePoints();
}
```

##### Integration with RoslynEmitter

```csharp
// Extended RoslynEmitter
public class RoslynEmitter
{
    private readonly SourceMapBuilder _sourceMap = new();
    
    private StatementSyntax EmitStatement(BoundStatement stmt)
    {
        var csharpStmt = stmt switch
        {
            BoundExpressionStatement es => EmitExpressionStatement(es),
            BoundIfStatement ifs => EmitIfStatement(ifs),
            // ... etc
        };
        
        // Record mapping if we have location info
        if (stmt.Span.IsValid)
        {
            _sourceMap.AddMapping(
                GetSharpyLocation(stmt),
                GetCSharpLocation(csharpStmt));
        }
        
        return csharpStmt;
    }
    
    public CompilationResult Emit()
    {
        // ... generate C# ...
        
        return new CompilationResult
        {
            GeneratedCode = code,
            SourceMap = _sourceMap.Build(),
            // ...
        };
    }
}
```

#### Why Start Now

Adding spans to AST nodes is straightforward when creating new nodes but very tedious when retrofitting. Every AST node constructor, every parser production rule, and every tree transformation must be updated. Starting early means:
- New code automatically includes spans
- Migrations can happen gradually
- No "big bang" refactoring needed later

---

### Recommendation #11: Error-Tolerant Parsing with Partial AST

**Priority:** HIGH for LSP  
**When to Start:** v0.2.x (before LSP development)  
**Effort:** Medium-Large  
**Impact:** Critical for LSP usability

#### Problem

The current parser aborts on errors, producing no AST. For LSP to be useful, it must work with incomplete and broken code:
- User is mid-keystroke
- Code has syntax errors
- Imports are unresolved

LSP features like completions, hover, and go-to-definition should work on the valid portions of the code.

#### Proposed Solution

##### Parse Result with Partial AST

```csharp
// New: src/Sharpy.Compiler/Parsing/ParseResult.cs
public class ParseResult
{
    /// <summary>
    /// The parsed module. Always non-null, but may contain error nodes.
    /// </summary>
    public ModuleSyntax Module { get; }
    
    /// <summary>
    /// Diagnostics produced during parsing.
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; }
    
    /// <summary>
    /// True if any error diagnostics were produced.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    
    /// <summary>
    /// True if the parse completed without any issues.
    /// </summary>
    public bool IsComplete => !HasErrors && Module.ErrorNodes.IsEmpty;
}
```

##### Error Node Types

```csharp
// New: src/Sharpy.Compiler/Syntax/ErrorNodes.cs

/// <summary>
/// Represents a statement that couldn't be parsed.
/// </summary>
public sealed record ErrorStatementSyntax(
    ImmutableArray<Token> SkippedTokens,
    string ErrorMessage,
    TextSpan Span
) : StatementSyntax;

/// <summary>
/// Represents an expression that couldn't be parsed.
/// </summary>
public sealed record ErrorExpressionSyntax(
    ImmutableArray<Token> SkippedTokens,
    TextSpan Span
) : ExpressionSyntax;

/// <summary>
/// Represents a type annotation that couldn't be parsed.
/// </summary>
public sealed record ErrorTypeSyntax(
    ImmutableArray<Token> SkippedTokens,
    TextSpan Span
) : TypeAnnotationSyntax;

/// <summary>
/// Represents a missing token that was expected but not found.
/// </summary>
public sealed record MissingTokenSyntax(
    TokenType ExpectedType,
    TextSpan Span
) : SyntaxNode;
```

##### Recovery Strategies

```csharp
// New: src/Sharpy.Compiler/Parsing/RecoveringParser.cs
public class RecoveringParser : Parser
{
    /// <summary>
    /// Synchronization tokens that indicate statement boundaries.
    /// </summary>
    private static readonly ImmutableHashSet<TokenType> StatementSync = ImmutableHashSet.Create(
        TokenType.Def,
        TokenType.Class,
        TokenType.If,
        TokenType.While,
        TokenType.For,
        TokenType.Return,
        TokenType.Dedent,
        TokenType.Eof
    );
    
    /// <summary>
    /// Called when statement parsing fails. Skips to next statement boundary.
    /// </summary>
    protected override StatementSyntax RecoverFromStatementError(ParserError error)
    {
        var skipped = new List<Token>();
        
        // Skip tokens until we find a synchronization point
        while (!StatementSync.Contains(Current.Type))
        {
            skipped.Add(Current);
            Advance();
        }
        
        AddDiagnostic(error.Message, error.Span);
        
        return new ErrorStatementSyntax(
            skipped.ToImmutableArray(),
            error.Message,
            ComputeSpan(skipped)
        );
    }
    
    /// <summary>
    /// Called when expression parsing fails. Creates error expression and continues.
    /// </summary>
    protected override ExpressionSyntax RecoverFromExpressionError(ParserError error)
    {
        AddDiagnostic(error.Message, error.Span);
        
        // Don't skip tokens for expressions - let statement recovery handle it
        return new ErrorExpressionSyntax(
            ImmutableArray<Token>.Empty,
            error.Span
        );
    }
    
    /// <summary>
    /// Called when a specific token is expected but not found.
    /// Inserts a "missing" token and continues.
    /// </summary>
    protected override Token ExpectToken(TokenType expected)
    {
        if (Current.Type == expected)
        {
            return Advance();
        }
        
        AddDiagnostic($"Expected {expected}, found {Current.Type}", Current.Span);
        
        // Return a synthetic missing token at current position
        return new Token(expected, "", null, new TextSpan(Current.Span.Start, 0));
    }
}
```

##### Example: Partial Parse

```python
# Source with errors:
def foo(x: int)    # Missing colon and return type
    if x > 0
        return x   # Missing colon after if
    return 0

class Bar:
    x: int
    
    def broken(     # Unclosed parenthesis
```

```csharp
// Resulting partial AST:
Module {
    Body: [
        FunctionDef {
            Name: "foo",
            Parameters: [Parameter { Name: "x", Type: int }],
            ReturnType: ErrorTypeSyntax { },  // Missing
            Body: [
                ErrorStatementSyntax { ... },  // Malformed if
                ReturnStatement { Value: 0 }
            ]
        },
        ClassDef {
            Name: "Bar",
            Body: [
                FieldDecl { Name: "x", Type: int },
                FunctionDef {
                    Name: "broken",
                    Parameters: ErrorParameterListSyntax { },  // Unclosed
                    Body: []
                }
            ]
        }
    ]
}
```

LSP can still provide:
- Completions inside `foo` body (knows we're in a function)
- Hover on `x` parameter (fully parsed)
- Go-to-definition for `Bar.x` (fully parsed)

---

### Recommendation #12: Symbol Index for Fast Lookups

**Priority:** HIGH for LSP, Incremental  
**When to Start:** v0.2.x (before LSP development)  
**Effort:** Medium  
**Impact:** Critical for LSP performance

#### Problem

LSP operations like "find all references" and "rename symbol" need to quickly answer:
- Where is this symbol defined?
- Where is this symbol used?
- What symbols are at this position?

Currently, answering these requires walking the entire AST. For large projects, this is too slow for interactive use.

Similarly, incremental compilation needs to answer:
- What files use this symbol?
- If this type changes, what needs recompilation?

#### Proposed Solution

```csharp
// New: src/Sharpy.Compiler/Index/SymbolIndex.cs
public class SymbolIndex
{
    // ==================== Core Lookups ====================
    
    /// <summary>
    /// Symbol → all locations where it's defined.
    /// (Usually one, but could be multiple for partial classes)
    /// </summary>
    public ILookup<SymbolId, FileLocation> Definitions { get; }
    
    /// <summary>
    /// Symbol → all locations where it's referenced.
    /// </summary>
    public ILookup<SymbolId, FileLocation> References { get; }
    
    /// <summary>
    /// File → all symbols defined in that file.
    /// </summary>
    public ILookup<string, SymbolId> SymbolsByFile { get; }
    
    /// <summary>
    /// File → all symbols referenced in that file (but defined elsewhere).
    /// </summary>
    public ILookup<string, SymbolId> ExternalReferencesByFile { get; }
    
    // ==================== Query Methods ====================
    
    /// <summary>
    /// Find all locations where the symbol is used.
    /// </summary>
    public IEnumerable<FileLocation> FindAllReferences(SymbolId symbol);
    
    /// <summary>
    /// Find where the symbol is defined.
    /// </summary>
    public FileLocation? FindDefinition(SymbolId symbol);
    
    /// <summary>
    /// Find all symbols whose definition or references overlap the given position.
    /// </summary>
    public IEnumerable<SymbolId> GetSymbolsAtPosition(string file, int position);
    
    /// <summary>
    /// Find all symbols in the given text range.
    /// </summary>
    public IEnumerable<SymbolId> GetSymbolsInRange(string file, TextSpan range);
    
    /// <summary>
    /// Find all files that reference the given symbol.
    /// </summary>
    public IEnumerable<string> GetFilesReferencingSymbol(SymbolId symbol);
    
    // ==================== Incremental Update ====================
    
    /// <summary>
    /// Update index for a single file (after recompilation).
    /// Returns a new index with updates applied.
    /// </summary>
    public SymbolIndex UpdateFile(string file, CompilationUnit newUnit);
    
    /// <summary>
    /// Remove a file from the index.
    /// </summary>
    public SymbolIndex RemoveFile(string file);
}

// Unique identifier for a symbol
public readonly record struct SymbolId(string FullyQualifiedName, SymbolKind Kind)
{
    public static SymbolId ForType(string fqn) => new(fqn, SymbolKind.Type);
    public static SymbolId ForFunction(string fqn) => new(fqn, SymbolKind.Function);
    public static SymbolId ForVariable(string fqn) => new(fqn, SymbolKind.Variable);
    public static SymbolId ForField(string fqn) => new(fqn, SymbolKind.Field);
    public static SymbolId ForParameter(string fqn) => new(fqn, SymbolKind.Parameter);
}
```

#### Index Builder

```csharp
// New: src/Sharpy.Compiler/Index/SymbolIndexBuilder.cs
public class SymbolIndexBuilder
{
    private readonly Dictionary<SymbolId, List<FileLocation>> _definitions = new();
    private readonly Dictionary<SymbolId, List<FileLocation>> _references = new();
    private readonly Dictionary<string, List<SymbolId>> _symbolsByFile = new();
    
    /// <summary>
    /// Index a compilation unit.
    /// </summary>
    public void IndexUnit(CompilationUnit unit)
    {
        var visitor = new IndexingVisitor(this, unit.FilePath);
        visitor.Visit(unit.BoundTree);
    }
    
    /// <summary>
    /// Record a symbol definition.
    /// </summary>
    public void AddDefinition(SymbolId symbol, FileLocation location);
    
    /// <summary>
    /// Record a symbol reference.
    /// </summary>
    public void AddReference(SymbolId symbol, FileLocation location);
    
    /// <summary>
    /// Build the final index.
    /// </summary>
    public SymbolIndex Build();
    
    private class IndexingVisitor : BoundTreeVisitor
    {
        // Visits bound tree, recording definitions and references
    }
}
```

#### Integration with Project Model

```csharp
// Extended ProjectModel
public class ProjectModel
{
    // ... existing fields ...
    
    /// <summary>
    /// Symbol index for fast lookups.
    /// </summary>
    public SymbolIndex Index { get; }
    
    /// <summary>
    /// Update project with new content for a file.
    /// Returns new project model with updated compilation and index.
    /// </summary>
    public ProjectModel UpdateFile(string file, string newContent)
    {
        var newUnit = RecompileFile(file, newContent);
        var newIndex = Index.UpdateFile(file, newUnit);
        return this with 
        { 
            Units = Units.SetItem(file, newUnit),
            Index = newIndex 
        };
    }
}
```

#### LSP Usage Example

```csharp
// LSP "Find All References" handler
public class FindReferencesHandler
{
    public Location[] Handle(FindReferencesParams request, ProjectModel project)
    {
        // Get symbol at position
        var symbols = project.Index.GetSymbolsAtPosition(
            request.TextDocument.Uri,
            request.Position.ToOffset());
        
        if (!symbols.Any())
            return Array.Empty<Location>();
        
        var symbol = symbols.First();
        
        // Get all references
        var refs = project.Index.FindAllReferences(symbol);
        
        // Include definition if requested
        if (request.Context.IncludeDeclaration)
        {
            var def = project.Index.FindDefinition(symbol);
            if (def.HasValue)
                refs = refs.Prepend(def.Value);
        }
        
        return refs.Select(ToLspLocation).ToArray();
    }
}
```

---

## Integration with Original Recommendations

### Complete Recommendation Set

| # | Recommendation | Timeline | Primary Enabler For |
|---|---------------|----------|---------------------|
| 1 | Validation Pipeline | Before 0.1.11 | Cleaner semantic analysis |
| 2 | CompilerServices | Before 0.1.11 | Centralized services, thread safety |
| 3 | Pre-compute CodeGenInfo | Before 0.1.15 | Stateless emitter |
| 4 | CompilationUnit Model | Before 0.1.12 | Cross-module, incremental |
| 5 | Unified TypeInfo | Before 0.1.14 | ADTs, lambdas |
| 6 | Directory Reorganization | Anytime | Code clarity |
| **7** | **Immutable AST** | **Start v0.1.x** | **Parallel, incremental, LSP** |
| **8** | **Dependency Graph** | **Before 0.1.12** | **Parallel, incremental** |
| **9** | **Control Flow Graph** | **v0.2.x** | **ADTs, async** |
| **10** | **Source Span Preservation** | **Start immediately** | **LSP, debugger** |
| **11** | **Error-Tolerant Parsing** | **v0.2.x** | **LSP** |
| **12** | **Symbol Index** | **v0.2.x** | **LSP, incremental** |

### Dependency Graph Between Recommendations

```
                    ┌─────────────────────────────────────────────────────┐
                    │              FOUNDATION (v0.1.x)                     │
                    ├─────────────────────────────────────────────────────┤
                    │                                                     │
                    │   #2 CompilerServices ◄──────────────────────┐      │
                    │          │                                   │      │
   ┌────────────────┼──────────┼───────────────────────────────────┼──────┤
   │                │          │                                   │      │
   │   #10 Source Spans ◄──────┴────────────┐                      │      │
   │   (start now)                          │                      │      │
   │         │                              │                      │      │
   │         ▼                              │                      │      │
   │   #7 Immutable AST ◄───────────────────┤                      │      │
   │   (start now)                          │                      │      │
   │                                        │                      │      │
   └────────────────────────────────────────┼──────────────────────┼──────┘
                    │                       │                      │
                    │   #4 CompilationUnit ◄┘                      │
                    │          │                                   │
                    │          ▼                                   │
                    │   #8 Dependency Graph                        │
                    │          │                                   │
                    ├──────────┼───────────────────────────────────┤
                    │          │      DATA MODEL COMPLETE          │
                    └──────────┼───────────────────────────────────┘
                               │
                    ┌──────────┼───────────────────────────────────┐
                    │          │       v0.2.x FEATURES             │
                    ├──────────┼───────────────────────────────────┤
                    │          ▼                                   │
                    │   #5 Unified TypeInfo ────────────┐          │
                    │          │                        │          │
                    │          ▼                        ▼          │
                    │   #9 Control Flow Graph    ADTs/Pattern      │
                    │          │                  Matching         │
                    │          │                        │          │
                    │          ├────────────────────────┤          │
                    │          │                        │          │
                    │          ▼                        ▼          │
                    │      Async/Await            Exhaustiveness   │
                    │                                              │
                    ├──────────────────────────────────────────────┤
                    │              TOOLING (v0.2.x+)               │
                    ├──────────────────────────────────────────────┤
                    │                                              │
                    │   #11 Error-Tolerant ──────┐                 │
                    │        Parsing             │                 │
                    │          │                 │                 │
                    │          ▼                 ▼                 │
                    │   #12 Symbol Index ──────► LSP               │
                    │          │                                   │
                    │          ▼                                   │
                    │   Incremental/Parallel Compilation           │
                    │                                              │
                    └──────────────────────────────────────────────┘
```

### What to Start Now (During v0.1.x)

Even though their full benefits come in v0.2.x, these should begin immediately:

1. **#10 Source Span Preservation**
   - Add `TextSpan Span` to all new AST nodes
   - Update lexer to track positions
   - Parser passes spans to node constructors
   - Retrofitting spans later requires touching every node and parser rule

2. **#7 Immutable AST (Partial)**
   - Use `record` for new AST node types
   - Use `ImmutableArray<T>` for new collections
   - Don't need to migrate existing nodes yet

3. **#8 Dependency Graph (Foundation)**
   - Track dependencies during import resolution
   - Store in CompilationUnit (when Rec #4 is implemented)
   - Full parallel/incremental comes later, but data collection starts now

---

## Feature-Specific Implementation Notes

### Tagged Unions (ADTs)

**Depends On:** #5 (TypeInfo), #9 (CFG for exhaustiveness)

**Type System Extension:**
```csharp
// Extend TypeInfo hierarchy from Rec #5
public sealed class UnionTypeInfo : TypeInfo
{
    public string Name { get; }
    public ImmutableArray<TypeParameterInfo> TypeParameters { get; }
    public ImmutableArray<UnionCaseInfo> Cases { get; }
}

public sealed class UnionCaseInfo
{
    public string Name { get; }
    public ImmutableArray<UnionCaseFieldInfo> Fields { get; }
    public TypeInfo ParentUnion { get; }
}
```

**Code Generation:**
- Union → abstract sealed class
- Cases → nested sealed classes with Deconstruct

**Exhaustiveness (uses CFG from #9):**
```csharp
// In ControlFlowAnalysis
public static ExhaustivenessResult CheckExhaustiveness(
    BoundMatchExpression match,
    UnionTypeInfo unionType)
{
    var coveredCases = new HashSet<string>();
    foreach (var arm in match.Arms)
    {
        if (arm.Pattern is UnionCasePattern ucp)
            coveredCases.Add(ucp.CaseName);
        else if (arm.Pattern is WildcardPattern)
            return ExhaustivenessResult.Exhaustive;
    }
    
    var missingCases = unionType.Cases
        .Where(c => !coveredCases.Contains(c.Name))
        .ToList();
    
    if (missingCases.Any())
        return ExhaustivenessResult.NotExhaustive(missingCases);
    
    return ExhaustivenessResult.Exhaustive;
}
```

### Async/Await

**Depends On:** #9 (CFG for state machine generation)

**State Machine Generation Flow:**
1. Build CFG for async function body
2. Identify await points (basic blocks containing await)
3. Split CFG into state regions
4. Generate state machine class with MoveNext method
5. Each region becomes a state case

**Conceptual Code Generation:**
```python
# Sharpy source
async def fetch_data(url: str) -> str:
    response = await http_get(url)
    data = await response.read()
    return data
```

```csharp
// Generated C# (simplified)
private class FetchDataStateMachine : IAsyncStateMachine
{
    public int _state;
    public AsyncTaskMethodBuilder<string> _builder;
    public string url;
    
    // Locals hoisted to fields
    private HttpResponse _response;
    private string _data;
    private TaskAwaiter<HttpResponse> _awaiter1;
    private TaskAwaiter<string> _awaiter2;
    
    public void MoveNext()
    {
        switch (_state)
        {
            case 0:
                _awaiter1 = HttpGet(url).GetAwaiter();
                if (!_awaiter1.IsCompleted)
                {
                    _state = 1;
                    _builder.AwaitUnsafeOnCompleted(ref _awaiter1, ref this);
                    return;
                }
                goto case 1;
                
            case 1:
                _response = _awaiter1.GetResult();
                _awaiter2 = _response.Read().GetAwaiter();
                if (!_awaiter2.IsCompleted)
                {
                    _state = 2;
                    _builder.AwaitUnsafeOnCompleted(ref _awaiter2, ref this);
                    return;
                }
                goto case 2;
                
            case 2:
                _data = _awaiter2.GetResult();
                _builder.SetResult(_data);
                return;
        }
    }
}
```

### LSP Implementation

**Depends On:** #10 (spans), #11 (error recovery), #12 (symbol index)

**Architecture:**
```
┌─────────────────────────────────────────────────────────────┐
│                    LSP Server                                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐     │
│  │  Document   │    │   Project   │    │   Symbol    │     │
│  │  Manager    │    │    Model    │    │    Index    │     │
│  └──────┬──────┘    └──────┬──────┘    └──────┬──────┘     │
│         │                  │                  │             │
│         │    ┌─────────────┴─────────────┐    │             │
│         └───►│    Incremental Compiler   │◄───┘             │
│              │  (Error-Tolerant Parser)  │                  │
│              └───────────────────────────┘                  │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                    Request Handlers                          │
├─────────────────────────────────────────────────────────────┤
│  textDocument/completion  │  Completions at cursor          │
│  textDocument/hover       │  Type info at cursor            │
│  textDocument/definition  │  Go to definition               │
│  textDocument/references  │  Find all references            │
│  textDocument/rename      │  Rename symbol                  │
│  textDocument/diagnostic  │  Report errors                  │
└─────────────────────────────────────────────────────────────┘
```

**Document Manager:**
- Tracks open documents and their versions
- Applies incremental text changes
- Triggers recompilation on change

**Incremental Workflow:**
1. User edits file
2. Document manager applies edit
3. Error-tolerant parser produces partial AST
4. Semantic analysis runs (with partial results)
5. Symbol index updated incrementally
6. Diagnostics pushed to client
7. Completions/hover use partial semantic model

### Parallel Compilation

**Depends On:** #7 (immutable), #8 (dependency graph)

```csharp
public class ParallelProjectCompiler
{
    public async Task<CompilationResult> CompileAsync(
        ProjectModel project,
        int maxDegreeOfParallelism = -1)
    {
        var graph = project.DependencyGraph;
        var groups = graph.GetParallelizableGroups();
        
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism > 0 
                ? maxDegreeOfParallelism 
                : Environment.ProcessorCount
        };
        
        var compiledUnits = new ConcurrentDictionary<string, CompilationUnit>();
        
        foreach (var group in groups)
        {
            // Compile all files in this group in parallel
            await Parallel.ForEachAsync(group, options, async (file, ct) =>
            {
                // Thread-safe: using immutable AST and shared CompilerServices
                var unit = await CompileFileAsync(file, project.Services, ct);
                compiledUnits[file] = unit;
            });
        }
        
        return BuildResult(compiledUnits.Values);
    }
}
```

### Incremental Compilation (Unity-style)

**Depends On:** #4 (CompilationUnit), #8 (dependency graph), #12 (symbol index)

```csharp
public class IncrementalCompiler
{
    private readonly CompilationCache _cache;
    
    public async Task<CompilationResult> RecompileChangedAsync(
        ProjectModel project,
        IReadOnlySet<string> changedFiles)
    {
        // 1. Compute content hashes
        var newHashes = changedFiles.ToDictionary(
            f => f, 
            f => ComputeHash(File.ReadAllText(f)));
        
        // 2. Filter to actually changed (content hash differs)
        var actuallyChanged = changedFiles
            .Where(f => _cache.GetHash(f) != newHashes[f])
            .ToHashSet();
        
        if (actuallyChanged.Count == 0)
            return _cache.GetLastResult();
        
        // 3. Find all affected files
        var affected = project.DependencyGraph.GetAffectedFiles(actuallyChanged);
        
        // 4. Get build order for just affected files
        var buildOrder = project.DependencyGraph
            .GetBuildOrder()
            .Where(affected.Contains)
            .ToList();
        
        // 5. Recompile in order
        var updatedProject = project;
        foreach (var file in buildOrder)
        {
            var newContent = File.ReadAllText(file);
            updatedProject = updatedProject.UpdateFile(file, newContent);
            _cache.Store(file, updatedProject.Units[file], newHashes.GetValueOrDefault(file));
        }
        
        // 6. Only regenerate code for changed files
        var result = await GenerateCodeAsync(updatedProject, affected);
        _cache.StoreResult(result);
        
        return result;
    }
}
```

---

## Summary

This addendum provides six additional architectural recommendations (#7-12) that complement the original six, specifically targeting future features like ADTs, async/await, LSP, debugger support, and parallel/incremental compilation.

**Key Takeaways:**

1. **Start #10 (Source Spans) and #7 (Immutable AST) during v0.1.x** — retrofitting these is extremely expensive

2. **#8 (Dependency Graph) naturally extends #4 (CompilationUnit)** — implement together

3. **#9 (CFG) is the critical enabler for v0.2.x** — blocks both ADTs and async

4. **#11 and #12 are prerequisites for a useful LSP** — plan for these before starting LSP work

5. **All recommendations work together** — parallel compilation needs #7 + #8; incremental needs #4 + #8 + #12; LSP needs #10 + #11 + #12

The goal is to build architectural foundations during v0.1.x that enable rapid development of advanced features in v0.2.x and beyond, rather than accumulating technical debt that must be paid down later.
