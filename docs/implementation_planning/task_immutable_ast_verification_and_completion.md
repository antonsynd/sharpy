# Task List: Immutable AST Verification & Completion

## Overview

This document provides tasks to complete the Immutable AST Foundation (Recommendation #7) after verification found minor gaps. The core AST migration is complete; these tasks address cleanup and future-proofing.

**Prerequisite**: Read `task_immutable_ast_foundation.md` for context.

---

## Task 1: Enable Immutability Tests

**Effort**: 30 minutes  
**Priority**: HIGH (verifies completed work)

### 1.1 Update ImmutabilityTests.cs

The tests in this file have `Skip` attributes but the migration they were waiting for is complete. Update each test to actually verify the types.

**File**: `src/Sharpy.Compiler.Tests/Ast/ImmutabilityTests.cs`

Replace the entire file with:

```csharp
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Ast;

/// <summary>
/// Tests that verify AST immutability guarantees.
/// These tests confirm that AST properties use ImmutableArray after migration.
/// </summary>
public class ImmutabilityTests
{
    [Fact]
    public void Module_Body_Is_Immutable()
    {
        var module = new Module { Body = ImmutableArray<Statement>.Empty };
        module.Body.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void FunctionDef_Parameters_Is_Immutable()
    {
        var func = new FunctionDef 
        { 
            Name = "test",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = ImmutableArray<Statement>.Empty
        };
        func.Parameters.Should().BeOfType<ImmutableArray<Parameter>>();
        func.Body.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void ClassDef_Body_Is_Immutable()
    {
        var classDef = new ClassDef 
        { 
            Name = "TestClass",
            Body = ImmutableArray<Statement>.Empty
        };
        classDef.Body.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void IfStatement_Collections_Are_Immutable()
    {
        var ifStmt = new IfStatement
        {
            Test = new BooleanLiteral { Value = true },
            ThenBody = ImmutableArray<Statement>.Empty,
            ElifClauses = ImmutableArray<ElifClause>.Empty,
            ElseBody = ImmutableArray<Statement>.Empty
        };
        ifStmt.ThenBody.Should().BeOfType<ImmutableArray<Statement>>();
        ifStmt.ElifClauses.Should().BeOfType<ImmutableArray<ElifClause>>();
        ifStmt.ElseBody.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void ListLiteral_Elements_Is_Immutable()
    {
        var list = new ListLiteral { Elements = ImmutableArray<Expression>.Empty };
        list.Elements.Should().BeOfType<ImmutableArray<Expression>>();
    }

    [Fact]
    public void FunctionCall_Arguments_Are_Immutable()
    {
        var call = new FunctionCall
        {
            Function = new Identifier { Name = "test" },
            Arguments = ImmutableArray<Expression>.Empty,
            KeywordArguments = ImmutableArray<KeywordArgument>.Empty
        };
        call.Arguments.Should().BeOfType<ImmutableArray<Expression>>();
        call.KeywordArguments.Should().BeOfType<ImmutableArray<KeywordArgument>>();
    }

    [Fact]
    public void TypeAnnotation_TypeArguments_Is_Immutable()
    {
        var typeAnnotation = new TypeAnnotation 
        { 
            Name = "list",
            TypeArguments = ImmutableArray<TypeAnnotation>.Empty
        };
        typeAnnotation.TypeArguments.Should().BeOfType<ImmutableArray<TypeAnnotation>>();
    }

    [Fact]
    public void TryStatement_Collections_Are_Immutable()
    {
        var tryStmt = new TryStatement
        {
            Body = ImmutableArray<Statement>.Empty,
            Handlers = ImmutableArray<ExceptHandler>.Empty,
            ElseBody = ImmutableArray<Statement>.Empty,
            FinallyBody = ImmutableArray<Statement>.Empty
        };
        tryStmt.Body.Should().BeOfType<ImmutableArray<Statement>>();
        tryStmt.Handlers.Should().BeOfType<ImmutableArray<ExceptHandler>>();
    }

    [Fact]
    public void ForStatement_Collections_Are_Immutable()
    {
        var forStmt = new ForStatement
        {
            Target = new Identifier { Name = "i" },
            Iterator = new FunctionCall { Function = new Identifier { Name = "range" } },
            Body = ImmutableArray<Statement>.Empty,
            ElseBody = ImmutableArray<Statement>.Empty
        };
        forStmt.Body.Should().BeOfType<ImmutableArray<Statement>>();
        forStmt.ElseBody.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void ComparisonChain_Collections_Are_Immutable()
    {
        var chain = new ComparisonChain
        {
            Operands = ImmutableArray<Expression>.Empty,
            Operators = ImmutableArray<ComparisonOperator>.Empty
        };
        chain.Operands.Should().BeOfType<ImmutableArray<Expression>>();
        chain.Operators.Should().BeOfType<ImmutableArray<ComparisonOperator>>();
    }

    [Fact]
    public void EnumDef_Members_Is_Immutable()
    {
        var enumDef = new EnumDef
        {
            Name = "TestEnum",
            Members = ImmutableArray<EnumMember>.Empty
        };
        enumDef.Members.Should().BeOfType<ImmutableArray<EnumMember>>();
    }

    [Fact]
    public void InterfaceDef_Collections_Are_Immutable()
    {
        var interfaceDef = new InterfaceDef
        {
            Name = "ITest",
            TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
            BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
            Body = ImmutableArray<Statement>.Empty
        };
        interfaceDef.TypeParameters.Should().BeOfType<ImmutableArray<TypeParameterDef>>();
        interfaceDef.BaseInterfaces.Should().BeOfType<ImmutableArray<TypeAnnotation>>();
        interfaceDef.Body.Should().BeOfType<ImmutableArray<Statement>>();
    }
}
```

### 1.2 Run Tests

```bash
cd src/Sharpy.Compiler.Tests
dotnet test --filter "FullyQualifiedName~ImmutabilityTests"
```

**Expected**: All 12 tests pass.

### 1.3 Commit

```bash
git add src/Sharpy.Compiler.Tests/Ast/ImmutabilityTests.cs
git commit -m "test: enable immutability tests after AST migration completion"
```

---

## Task 2: Add Placeholder AST Nodes for v0.2.x Features (MEDIUM EFFORT - 2 hours)

**Effort**: 2 hours  
**Priority**: MEDIUM (prepares for future without blocking current work)

### Why Placeholders?

Adding placeholder (stub) AST nodes now ensures:
1. They follow the immutable pattern from the start
2. Parser stubs can be added incrementally
3. Integration points are defined early
4. Less refactoring when features are implemented

### 2.1 Create `Expression.Future.cs` Partial Class

**File**: `src/Sharpy.Compiler/Parser/Ast/Expression.Future.cs`

```csharp
using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// FUTURE EXPRESSION NODES (v0.2.x+)
// These are placeholder definitions that follow the immutable pattern.
// Implementation will be completed when these features are developed.
// =============================================================================

#region Async/Await (v0.2.x+)

/// <summary>
/// Await expression (await expr).
/// Suspends execution until the awaited task completes.
/// </summary>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x+
/// </remarks>
public record AwaitExpression : Expression
{
    /// <summary>
    /// The expression being awaited (must return a Task or ValueTask).
    /// </summary>
    public Expression Operand { get; init; } = null!;
}

#endregion

#region Pattern Matching (v0.2.x)

/// <summary>
/// Match expression (match expr { case1 => result1, case2 => result2 }).
/// Returns a value based on pattern matching.
/// </summary>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
public record MatchExpression : Expression
{
    /// <summary>
    /// The expression being matched against patterns.
    /// </summary>
    public Expression Scrutinee { get; init; } = null!;
    
    /// <summary>
    /// The match arms (pattern => result pairs).
    /// </summary>
    public ImmutableArray<MatchArm> Arms { get; init; } = ImmutableArray<MatchArm>.Empty;
}

/// <summary>
/// A single arm in a match expression (pattern => result).
/// </summary>
public record MatchArm
{
    /// <summary>
    /// The pattern to match against.
    /// </summary>
    public Pattern Pattern { get; init; } = null!;
    
    /// <summary>
    /// Optional guard condition (when clause).
    /// </summary>
    public Expression? Guard { get; init; }
    
    /// <summary>
    /// The result expression if the pattern matches.
    /// </summary>
    public Expression Result { get; init; } = null!;
    
    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

#endregion
```

### 2.2 Create `Statement.Future.cs` Partial Class

**File**: `src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs`

```csharp
using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// FUTURE STATEMENT NODES (v0.2.x+)
// These are placeholder definitions that follow the immutable pattern.
// Implementation will be completed when these features are developed.
// =============================================================================

#region Pattern Matching (v0.2.x)

/// <summary>
/// Match statement (match expr: case1: ..., case2: ...).
/// Executes code based on pattern matching (statement form, unlike MatchExpression).
/// </summary>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
public record MatchStatement : Statement
{
    /// <summary>
    /// The expression being matched against patterns.
    /// </summary>
    public Expression Scrutinee { get; init; } = null!;
    
    /// <summary>
    /// The match cases (pattern: body pairs).
    /// </summary>
    public ImmutableArray<MatchCase> Cases { get; init; } = ImmutableArray<MatchCase>.Empty;
}

/// <summary>
/// A single case in a match statement (pattern: body).
/// </summary>
public record MatchCase
{
    /// <summary>
    /// The pattern to match against.
    /// </summary>
    public Pattern Pattern { get; init; } = null!;
    
    /// <summary>
    /// Optional guard condition (when clause).
    /// </summary>
    public Expression? Guard { get; init; }
    
    /// <summary>
    /// The body statements to execute if the pattern matches.
    /// </summary>
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    
    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

#endregion

#region Tagged Unions / ADTs (v0.2.x)

/// <summary>
/// Union type definition (tagged union / algebraic data type).
/// </summary>
/// <example>
/// union Result[T, E]:
///     case Ok(value: T)
///     case Err(error: E)
/// </example>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
public record UnionDef : Statement
{
    /// <summary>
    /// The name of the union type.
    /// </summary>
    public string Name { get; init; } = "";
    
    /// <summary>
    /// Type parameters for generic unions (e.g., T, E in Result[T, E]).
    /// </summary>
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
    
    /// <summary>
    /// The union cases (variants).
    /// </summary>
    public ImmutableArray<UnionCaseDef> Cases { get; init; } = ImmutableArray<UnionCaseDef>.Empty;
    
    /// <summary>
    /// Decorators applied to the union.
    /// </summary>
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
    
    /// <summary>
    /// Documentation string.
    /// </summary>
    public string? DocString { get; init; }
}

/// <summary>
/// A single case (variant) in a union type definition.
/// </summary>
/// <example>
/// case Ok(value: T)       // Case with named field
/// case None               // Case with no fields
/// case Tuple(int, str)    // Case with positional fields
/// </example>
public record UnionCaseDef
{
    /// <summary>
    /// The name of this case (e.g., Ok, Err, None).
    /// </summary>
    public string Name { get; init; } = "";
    
    /// <summary>
    /// Fields for this case. Empty for singleton cases (e.g., None).
    /// </summary>
    public ImmutableArray<UnionCaseField> Fields { get; init; } = ImmutableArray<UnionCaseField>.Empty;
    
    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// A field in a union case.
/// </summary>
public record UnionCaseField
{
    /// <summary>
    /// The field name (null for positional fields).
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// The field type.
    /// </summary>
    public TypeAnnotation Type { get; init; } = null!;
    
    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

#endregion
```

### 2.3 Create `Pattern.cs` for Pattern AST Nodes

**File**: `src/Sharpy.Compiler/Parser/Ast/Pattern.cs`

```csharp
using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// PATTERN AST NODES (v0.2.x)
// Pattern matching for match expressions/statements and other future uses.
// =============================================================================

/// <summary>
/// Base class for all pattern nodes in pattern matching.
/// </summary>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
public abstract record Pattern : Node;

#region Basic Patterns

/// <summary>
/// Wildcard pattern (_) - matches anything and discards.
/// </summary>
public record WildcardPattern : Pattern;

/// <summary>
/// Binding pattern (name or name: type) - matches anything and binds to variable.
/// </summary>
public record BindingPattern : Pattern
{
    /// <summary>
    /// The variable name to bind the matched value to.
    /// </summary>
    public string Name { get; init; } = "";
    
    /// <summary>
    /// Optional type constraint for the binding.
    /// </summary>
    public TypeAnnotation? Type { get; init; }
}

/// <summary>
/// Literal pattern - matches a specific constant value.
/// </summary>
public record LiteralPattern : Pattern
{
    /// <summary>
    /// The literal value to match (IntegerLiteral, StringLiteral, etc.).
    /// </summary>
    public Expression Literal { get; init; } = null!;
}

/// <summary>
/// Type pattern (is Type) - matches if value is of specified type.
/// </summary>
public record TypePattern : Pattern
{
    /// <summary>
    /// The type to check against.
    /// </summary>
    public TypeAnnotation Type { get; init; } = null!;
    
    /// <summary>
    /// Optional variable to bind the casted value to.
    /// </summary>
    public string? BindingName { get; init; }
}

#endregion

#region Compound Patterns

/// <summary>
/// Union case pattern - matches a specific case of a union type.
/// </summary>
/// <example>
/// case Ok(value)      // Destructuring case
/// case None           // Singleton case
/// </example>
public record UnionCasePattern : Pattern
{
    /// <summary>
    /// The union type (if qualified, e.g., Result.Ok).
    /// </summary>
    public TypeAnnotation? UnionType { get; init; }
    
    /// <summary>
    /// The case name to match.
    /// </summary>
    public string CaseName { get; init; } = "";
    
    /// <summary>
    /// Patterns to match against the case fields.
    /// </summary>
    public ImmutableArray<Pattern> FieldPatterns { get; init; } = ImmutableArray<Pattern>.Empty;
}

/// <summary>
/// Tuple pattern - matches tuple structure.
/// </summary>
/// <example>
/// (a, b, _)           // Match 3-tuple, bind first two
/// (x, y)              // Match 2-tuple
/// </example>
public record TuplePattern : Pattern
{
    /// <summary>
    /// Patterns for each element.
    /// </summary>
    public ImmutableArray<Pattern> Elements { get; init; } = ImmutableArray<Pattern>.Empty;
}

/// <summary>
/// List pattern - matches list structure.
/// </summary>
/// <example>
/// []                  // Empty list
/// [x]                 // Single element
/// [head, ...tail]     // Head and rest pattern
/// [a, b, c]           // Exact match
/// </example>
public record ListPattern : Pattern
{
    /// <summary>
    /// Patterns for list elements.
    /// </summary>
    public ImmutableArray<Pattern> Elements { get; init; } = ImmutableArray<Pattern>.Empty;
    
    /// <summary>
    /// Optional rest pattern (the "...tail" part).
    /// </summary>
    public Pattern? RestPattern { get; init; }
}

/// <summary>
/// Or pattern (pattern1 | pattern2) - matches if either pattern matches.
/// </summary>
public record OrPattern : Pattern
{
    /// <summary>
    /// The alternative patterns (at least 2).
    /// </summary>
    public ImmutableArray<Pattern> Alternatives { get; init; } = ImmutableArray<Pattern>.Empty;
}

/// <summary>
/// And pattern (pattern1 and pattern2) - matches if both patterns match.
/// Also known as "as pattern" in some languages.
/// </summary>
public record AndPattern : Pattern
{
    /// <summary>
    /// The left pattern.
    /// </summary>
    public Pattern Left { get; init; } = null!;
    
    /// <summary>
    /// The right pattern.
    /// </summary>
    public Pattern Right { get; init; } = null!;
}

/// <summary>
/// Guard pattern (pattern when condition) - adds a condition to a pattern.
/// </summary>
public record GuardPattern : Pattern
{
    /// <summary>
    /// The inner pattern.
    /// </summary>
    public Pattern Inner { get; init; } = null!;
    
    /// <summary>
    /// The guard condition.
    /// </summary>
    public Expression Guard { get; init; } = null!;
}

#endregion
```

### 2.4 Run Build to Verify

```bash
cd src/Sharpy.Compiler
dotnet build
```

**Expected**: Build succeeds with no errors.

### 2.5 Run All Tests

```bash
cd src/Sharpy.Compiler.Tests
dotnet test
```

**Expected**: All tests pass (existing tests should not be affected by new placeholder files).

### 2.6 Commit

```bash
git add src/Sharpy.Compiler/Parser/Ast/Expression.Future.cs
git add src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs
git add src/Sharpy.Compiler/Parser/Ast/Pattern.cs
git commit -m "feat(ast): add placeholder AST nodes for v0.2.x features (async, unions, patterns)"
```

---

## Task 3: Update Documentation (LOW EFFORT - 30 min)

**Effort**: 30 minutes  
**Priority**: LOW (but good practice)

### 3.1 Update task_immutable_ast_foundation.md

Add a summary note at the top indicating completion status:

```markdown
## Status: Phase 1-4.1 Complete ✅

**Completed**: January 2026  
**Phases Complete**: 1, 2, 3, 4.1, 6  
**Phases Deferred**: 4.2 (Symbol semantic data), 5 (Symbol ImmutableArray)

The core AST migration is complete. All AST nodes use record types with ImmutableArray.
Deferred phases can be completed when LSP or parallel compilation features require full immutability.
```

### 3.2 Commit

```bash
git add docs/implementation_planning/task_immutable_ast_foundation.md
git commit -m "docs: update immutable AST task list with completion status"
```

---

## Task 4 (OPTIONAL): Symbol Class Migration Preparation

**Effort**: 4-6 hours  
**Priority**: LOW (only needed for LSP/parallel compilation)  
**Dependency**: Should be done when starting LSP work

This task is documented in `task_immutable_ast_foundation.md` as Phases 4.2 and 5. It involves:

1. Migrating `Symbol.CodeGenInfo`, `VariableSymbol.Type`, `TypeSymbol.BaseType` to use `SemanticBinding`
2. Migrating `FunctionSymbol.Parameters`, `TypeSymbol.Fields`, `TypeSymbol.Methods`, etc. to `ImmutableArray<T>`
3. Migrating `TypeSymbol.OperatorMethods`, `ModuleSymbol.Exports` to `ImmutableDictionary<K,V>`

**Recommendation**: Defer until v0.2.x LSP work begins. The current symbol mutability doesn't affect v0.1.x functionality.

---

## Verification Checklist

After completing Tasks 1-3, verify:

- [ ] `dotnet build` succeeds for Sharpy.Compiler
- [ ] `dotnet test` passes all tests in Sharpy.Compiler.Tests
- [ ] ImmutabilityTests (12 tests) all pass
- [ ] New placeholder files compile without errors
- [ ] No regressions in existing integration tests

---

## Summary

| Task | Effort | Priority | Status |
|------|--------|----------|--------|
| Task 1: Enable Immutability Tests | 30 min | HIGH | TODO |
| Task 2: Add Placeholder AST Nodes | 2 hours | MEDIUM | TODO |
| Task 3: Update Documentation | 30 min | LOW | TODO |
| Task 4: Symbol Migration | 4-6 hours | LOW | DEFERRED |

**Total Estimated Effort**: ~3 hours (excluding Task 4)

After completing these tasks, the Immutable AST Foundation will be:
1. ✅ Verified by tests
2. ✅ Prepared for v0.2.x feature development
3. ✅ Well-documented with clear completion status
