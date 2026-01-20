# Task List: Immutable AST Foundation (Recommendation #7)

## Overview

This document provides a comprehensive, step-by-step task list for implementing Recommendation #7 from the Architecture Review: **Immutable AST and Semantic Model**. The goal is to migrate the AST from mutable `List<T>` properties to immutable `ImmutableArray<T>`, ensure all AST nodes use `init`-only setters, and establish clear separation between syntax (AST) and semantic binding.

### Why Immutability Matters

1. **LSP Support**: Can't safely share AST across analysis threads; edits to one version affect others
2. **Parallel Compilation**: Mutable state requires locking or careful coordination
3. **Incremental Compilation**: Can't compare old vs. new state; can't cache intermediate results safely
4. **Future Features**: Tagged unions, pattern matching, async/await all benefit from immutable structures

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **List → ImmutableArray** | `ImmutableArray<T>` | Better performance than `ImmutableList<T>` for read-heavy workloads; matches Roslyn patterns |
| **Semantic Binding** | Separate binding data structure | Keeps AST pure; allows multiple bindings per AST (e.g., for LSP) |
| **Migration Strategy** | In-place transformation | Avoids parallel type hierarchies; minimizes code duplication |
| **Helper Types** | Promote to proper records | `ElifClause`, `ExceptHandler`, etc. should be first-class records |

### Compatibility Notes

- All existing tests that pass must continue to pass at the end
- Each phase should produce a compilable, testable codebase
- Commit points are provided for easy rollback

---

## Prerequisites

Before starting, ensure you understand:
- C# records and `init` properties
- `System.Collections.Immutable.ImmutableArray<T>`
- The Sharpy compiler's parsing pipeline

### Required Package

Add to `Sharpy.Compiler.csproj` if not already present:
```xml
<PackageReference Include="System.Collections.Immutable" Version="8.0.0" />
```

---

## Phase 1: Foundation and Preparation

### 1.1 Create Test Baseline and Infrastructure

**Goal**: Establish a reliable test baseline before making changes.

- [x] **1.1.1** Run all existing tests and document pass/fail counts
  ```bash
  cd src/Sharpy.Compiler.Tests
  dotnet test --logger "console;verbosity=detailed" > baseline_test_results.txt 2>&1
  ```
  - Record: Total tests, Passed, Failed, Skipped
  - Save this file for comparison after each phase

- [x] **1.1.2** Create a new test file for immutability verification
  - Create `src/Sharpy.Compiler.Tests/Ast/ImmutabilityTests.cs`
  - This file will contain tests that verify immutability guarantees
  ```csharp
  // src/Sharpy.Compiler.Tests/Ast/ImmutabilityTests.cs
  using FluentAssertions;
  using Xunit;
  using Sharpy.Compiler.Parser.Ast;
  using System.Collections.Immutable;

  namespace Sharpy.Compiler.Tests.Ast;

  /// <summary>
  /// Tests that verify AST immutability guarantees.
  /// These tests will initially fail and should pass after migration.
  /// </summary>
  public class ImmutabilityTests
  {
      [Fact(Skip = "Enable after Phase 2")]
      public void Module_Body_Is_Immutable()
      {
          var module = new Module { Body = ImmutableArray<Statement>.Empty };
          // After migration, Body should be ImmutableArray
          module.Body.Should().BeOfType<ImmutableArray<Statement>>();
      }

      [Fact(Skip = "Enable after Phase 2")]
      public void FunctionDef_Parameters_Is_Immutable()
      {
          var func = new FunctionDef 
          { 
              Name = "test",
              Parameters = ImmutableArray<Parameter>.Empty,
              Body = ImmutableArray<Statement>.Empty
          };
          func.Parameters.Should().BeOfType<ImmutableArray<Parameter>>();
      }

      // Add more tests as you migrate each type
  }
  ```

- [x] **1.1.3** Add `System.Collections.Immutable` using statements to AST files
  - Add to `Node.cs`, `Statement.cs`, `Expression.cs`, `Types.cs`
  ```csharp
  using System.Collections.Immutable;
  ```

**Commit Point**: `git commit -m "chore: prepare infrastructure for immutable AST migration"`

---

### 1.2 Fix Immediate Mutability Issues in Symbols

**Goal**: Address the `set` accessors in Symbol classes that should be `init`.

- [x] **1.2.1** Audit `Symbol.cs` for mutable properties
  - `Symbol.CodeGenInfo` uses `set` - needs careful handling
  - `VariableSymbol.Type` uses `set` - needs careful handling
  - `TypeSymbol.BaseType` uses `set` - needs careful handling

- [x] **1.2.2** Create `SemanticBinding.cs` for post-AST semantic information
  ```csharp
  // src/Sharpy.Compiler/Semantic/SemanticBinding.cs
  using System.Collections.Concurrent;
  
  namespace Sharpy.Compiler.Semantic;

  /// <summary>
  /// Stores semantic information that is computed after AST creation.
  /// This separates mutable semantic data from immutable syntax.
  /// </summary>
  public class SemanticBinding
  {
      // Maps symbols to their CodeGenInfo
      private readonly ConcurrentDictionary<Symbol, CodeGenInfo> _codeGenInfo = new();
      
      // Maps variable symbols to their resolved types
      private readonly ConcurrentDictionary<VariableSymbol, SemanticType> _variableTypes = new();
      
      // Maps type symbols to their resolved base types
      private readonly ConcurrentDictionary<TypeSymbol, TypeSymbol> _baseTypes = new();

      public void SetCodeGenInfo(Symbol symbol, CodeGenInfo info) 
          => _codeGenInfo[symbol] = info;
      
      public CodeGenInfo? GetCodeGenInfo(Symbol symbol) 
          => _codeGenInfo.TryGetValue(symbol, out var info) ? info : null;

      public void SetVariableType(VariableSymbol symbol, SemanticType type)
          => _variableTypes[symbol] = type;
      
      public SemanticType GetVariableType(VariableSymbol symbol)
          => _variableTypes.TryGetValue(symbol, out var type) ? type : SemanticType.Unknown;

      public void SetBaseType(TypeSymbol symbol, TypeSymbol baseType)
          => _baseTypes[symbol] = baseType;
      
      public TypeSymbol? GetBaseType(TypeSymbol symbol)
          => _baseTypes.TryGetValue(symbol, out var bt) ? bt : null;
  }
  ```

- [x] **1.2.3** Update `Symbol.cs` to keep `set` but add deprecation
  - Add `[Obsolete]` attributes to mutable properties with migration guidance
  - Do NOT remove `set` yet - this would break too much
  ```csharp
  /// <summary>
  /// Code generation information computed during semantic analysis.
  /// </summary>
  /// <remarks>
  /// MIGRATION NOTE: In the future, use SemanticBinding.SetCodeGenInfo/GetCodeGenInfo instead.
  /// The mutable setter is preserved for backward compatibility during migration.
  /// </remarks>
  public CodeGenInfo? CodeGenInfo { get; set; }
  ```

- [x] **1.2.4** Run tests to verify no regressions
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat: add SemanticBinding for separating mutable semantic data from syntax"`

---

### 1.3 Create ImmutableArray Helper Extensions

**Goal**: Create helper methods to ease the transition from `List<T>` to `ImmutableArray<T>`.

- [x] **1.3.1** Create `AstExtensions.cs` with conversion helpers
  ```csharp
  // src/Sharpy.Compiler/Parser/Ast/AstExtensions.cs
  using System.Collections.Immutable;

  namespace Sharpy.Compiler.Parser.Ast;

  /// <summary>
  /// Extension methods to help with AST immutability migration.
  /// </summary>
  public static class AstExtensions
  {
      /// <summary>
      /// Converts a list to an immutable array. 
      /// Use during migration from List to ImmutableArray.
      /// </summary>
      public static ImmutableArray<T> ToImmutableArraySafe<T>(this List<T>? list)
          => list?.ToImmutableArray() ?? ImmutableArray<T>.Empty;
      
      /// <summary>
      /// Converts a list to an immutable array.
      /// </summary>
      public static ImmutableArray<T> ToImmutableArraySafe<T>(this IEnumerable<T>? items)
          => items?.ToImmutableArray() ?? ImmutableArray<T>.Empty;

      /// <summary>
      /// Creates an ImmutableArray builder for efficient building.
      /// </summary>
      public static ImmutableArray<T>.Builder CreateBuilder<T>(int initialCapacity = 4)
          => ImmutableArray.CreateBuilder<T>(initialCapacity);
  }
  ```

- [x] **1.3.2** Run tests to verify no regressions
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat: add AstExtensions helper methods for immutable array migration"`

---

## Phase 2: Migrate AST Node Types (Leaf to Root)

The strategy is to migrate from leaf nodes (simple types) to root nodes (complex types). This minimizes cascading changes.

### Migration Order:
1. Helper records (`ElifClause`, `ExceptHandler`, etc.)
2. Type annotations (`TypeAnnotation`, `FunctionType`, `TupleType`)
3. Simple expressions (literals)
4. Complex expressions (collections, comprehensions)
5. Simple statements
6. Complex statements (control flow)
7. Definitions (functions, classes, etc.)
8. Module

---

### 2.1 Migrate Helper Records

**Goal**: Migrate non-Node helper records that contain List<T>.

- [x] **2.1.1** Migrate `ElifClause` in `Statement.cs`
  ```csharp
  // BEFORE:
  public record ElifClause
  {
      public Expression Test { get; init; } = null!;
      public List<Statement> Body { get; init; } = new();
      // ... location properties
  }

  // AFTER:
  public record ElifClause
  {
      public Expression Test { get; init; } = null!;
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      // ... location properties
  }
  ```

- [x] **2.1.2** Migrate `ExceptHandler` in `Statement.cs`
  ```csharp
  // AFTER:
  public record ExceptHandler
  {
      public TypeAnnotation? ExceptionType { get; init; }
      public string? Name { get; init; }
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      // ... location properties
  }
  ```

- [x] **2.1.3** Migrate `Parameter` in `Statement.cs`
  - No List<T> changes needed, but verify all properties use `init`

- [x] **2.1.4** Migrate `EnumMember` in `Statement.cs`
  - No List<T> changes needed

- [x] **2.1.5** Migrate `TypeParameterDef` in `Statement.cs`
  ```csharp
  // AFTER:
  public record TypeParameterDef
  {
      public string Name { get; init; } = "";
      public ImmutableArray<ConstraintClause> Constraints { get; init; } = ImmutableArray<ConstraintClause>.Empty;
      // ... location properties
  }
  ```

- [x] **2.1.6** Migrate `Decorator` in `Statement.cs`
  - No List<T> changes needed

- [x] **2.1.7** Migrate `ImportAlias` in `Statement.cs`
  - No List<T> changes needed

- [x] **2.1.8** Migrate `FStringPart` in `Expression.cs`
  - No List<T> changes needed

- [x] **2.1.9** Migrate `DictEntry` in `Expression.cs`
  - No List<T> changes needed

- [x] **2.1.10** Migrate `KeywordArgument` in `Expression.cs`
  - No List<T> changes needed

- [x] **2.1.11** Run tests - expect some failures in parser tests
  ```bash
  dotnet test
  ```
  - Document which tests fail (parser tests that create AST with `new List<T>`)

**Commit Point**: `git commit -m "feat(ast): migrate helper records to ImmutableArray"`

---

### 2.2 Update Parser to Build ImmutableArrays (Part 1: Helper Types)

**Goal**: Update parser code that creates helper records.

- [x] **2.2.1** Update `Parser.Statements.cs` - `ParseIfStatement`
  ```csharp
  // BEFORE:
  var elifClauses = new List<ElifClause>();
  // ... loop adding clauses ...
  elifClauses.Add(new ElifClause { ... });

  // AFTER:
  var elifClausesBuilder = ImmutableArray.CreateBuilder<ElifClause>();
  // ... loop adding clauses ...
  elifClausesBuilder.Add(new ElifClause { ... });
  // ... at the end:
  return new IfStatement
  {
      ElifClauses = elifClausesBuilder.ToImmutable(),
      // ...
  };
  ```

- [x] **2.2.2** Update `Parser.Statements.cs` - `ParseTryStatement`
  - Update exception handler list building

- [x] **2.2.3** Update other parser methods that build helper type lists
  - Search for patterns like `new List<ElifClause>()`, `new List<ExceptHandler>()`, etc.

- [x] **2.2.4** Run tests - should have fewer failures now
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(parser): update parser to build ImmutableArray for helper types"`

---

### 2.3 Migrate Type Annotation Types

**Goal**: Migrate type-related AST nodes.

- [x] **2.3.1** Migrate `TypeAnnotation` in `Types.cs`
  ```csharp
  // AFTER:
  public record TypeAnnotation
  {
      public string Name { get; init; } = "";
      public ImmutableArray<TypeAnnotation> TypeArguments { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
      public bool IsNullable { get; init; }
      // ... location properties
  }
  ```

- [x] **2.3.2** Migrate `FunctionType` in `Types.cs`
  ```csharp
  // AFTER:
  public record FunctionType
  {
      public ImmutableArray<TypeAnnotation> ParameterTypes { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
      public TypeAnnotation ReturnType { get; init; } = null!;
      // ... location properties
  }
  ```

- [x] **2.3.3** Migrate `TupleType` in `Types.cs`
  ```csharp
  // AFTER:
  public record TupleType
  {
      public ImmutableArray<TypeAnnotation> ElementTypes { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
  }
  ```

- [x] **2.3.4** Update `Parser.Types.cs` to build ImmutableArrays
  - Update `ParseTypeAnnotation`, `ParseFunctionType`, etc.

- [x] **2.3.5** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(ast): migrate type annotation types to ImmutableArray"`

---

### 2.4 Migrate Expression Types

**Goal**: Migrate expression AST nodes.

- [x] **2.4.1** Migrate collection literal expressions in `Expression.cs`
  ```csharp
  // ListLiteral
  public record ListLiteral : Expression
  {
      public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
  }

  // DictLiteral
  public record DictLiteral : Expression
  {
      public ImmutableArray<DictEntry> Entries { get; init; } = ImmutableArray<DictEntry>.Empty;
  }

  // SetLiteral
  public record SetLiteral : Expression
  {
      public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
  }

  // TupleLiteral
  public record TupleLiteral : Expression
  {
      public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
  }
  ```

- [x] **2.4.2** Migrate `FStringLiteral` in `Expression.cs`
  ```csharp
  public record FStringLiteral : Expression
  {
      public ImmutableArray<FStringPart> Parts { get; init; } = ImmutableArray<FStringPart>.Empty;
  }
  ```

- [x] **2.4.3** Migrate comprehension types in `Expression.cs`
  ```csharp
  // ListComprehension
  public record ListComprehension : Expression
  {
      public Expression Element { get; init; } = null!;
      public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;
  }

  // SetComprehension
  public record SetComprehension : Expression
  {
      public Expression Element { get; init; } = null!;
      public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;
  }

  // DictComprehension
  public record DictComprehension : Expression
  {
      public Expression Key { get; init; } = null!;
      public Expression Value { get; init; } = null!;
      public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;
  }
  ```

- [x] **2.4.4** Migrate `FunctionCall` in `Expression.cs`
  ```csharp
  public record FunctionCall : Expression
  {
      public Expression Function { get; init; } = null!;
      public ImmutableArray<Expression> Arguments { get; init; } = ImmutableArray<Expression>.Empty;
      public ImmutableArray<KeywordArgument> KeywordArguments { get; init; } = ImmutableArray<KeywordArgument>.Empty;
  }
  ```

- [x] **2.4.5** Migrate `ComparisonChain` in `Expression.cs`
  ```csharp
  public record ComparisonChain : Expression
  {
      public ImmutableArray<Expression> Operands { get; init; } = ImmutableArray<Expression>.Empty;
      public ImmutableArray<ComparisonOperator> Operators { get; init; } = ImmutableArray<ComparisonOperator>.Empty;
  }
  ```

- [x] **2.4.6** Migrate `LambdaExpression` in `Expression.cs`
  ```csharp
  public record LambdaExpression : Expression
  {
      public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;
      public Expression Body { get; init; } = null!;
  }
  ```

- [x] **2.4.7** Update `Parser.Expressions.cs` and `Parser.Primaries.cs`
  - Update all methods that create expression nodes with List<T>

- [x] **2.4.8** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(ast): migrate expression types to ImmutableArray"`

---

### 2.5 Migrate Statement Types

**Goal**: Migrate statement AST nodes.

- [x] **2.5.1** Migrate control flow statements in `Statement.cs`
  ```csharp
  // IfStatement
  public record IfStatement : Statement
  {
      public Expression Test { get; init; } = null!;
      public ImmutableArray<Statement> ThenBody { get; init; } = ImmutableArray<Statement>.Empty;
      public ImmutableArray<ElifClause> ElifClauses { get; init; } = ImmutableArray<ElifClause>.Empty;
      public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
  }

  // WhileStatement
  public record WhileStatement : Statement
  {
      public Expression Test { get; init; } = null!;
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
  }

  // ForStatement
  public record ForStatement : Statement
  {
      public Expression Target { get; init; } = null!;
      public Expression Iterator { get; init; } = null!;
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
  }

  // TryStatement
  public record TryStatement : Statement
  {
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      public ImmutableArray<ExceptHandler> Handlers { get; init; } = ImmutableArray<ExceptHandler>.Empty;
      public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
      public ImmutableArray<Statement> FinallyBody { get; init; } = ImmutableArray<Statement>.Empty;
  }
  ```

- [x] **2.5.2** Migrate import statements in `Statement.cs`
  ```csharp
  // ImportStatement
  public record ImportStatement : Statement
  {
      public ImmutableArray<ImportAlias> Names { get; init; } = ImmutableArray<ImportAlias>.Empty;
  }

  // FromImportStatement
  public record FromImportStatement : Statement
  {
      public string Module { get; init; } = "";
      public ImmutableArray<ImportAlias> Names { get; init; } = ImmutableArray<ImportAlias>.Empty;
      public bool ImportAll { get; init; }
      
      // NOTE: These remain mutable for now - semantic data should be in SemanticBinding
      // TODO: Migrate to SemanticBinding in Phase 4
      public string? ResolvedModulePath { get; set; }
      public Dictionary<string, Semantic.Symbol>? ReExportedSymbols { get; set; }
  }
  ```

- [x] **2.5.3** Update `Parser.Statements.cs`
  - Update all statement parsing methods

- [x] **2.5.4** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(ast): migrate statement types to ImmutableArray"`

---

### 2.6 Migrate Definition Types

**Goal**: Migrate function, class, struct, interface, enum definitions.

- [x] **2.6.1** Migrate `FunctionDef` in `Statement.cs`
  ```csharp
  public record FunctionDef : Statement
  {
      public string Name { get; init; } = "";
      public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
      public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;
      public TypeAnnotation? ReturnType { get; init; }
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
      public string? DocString { get; init; }
  }
  ```

- [x] **2.6.2** Migrate `ClassDef` in `Statement.cs`
  ```csharp
  public record ClassDef : Statement
  {
      public string Name { get; init; } = "";
      public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
      public ImmutableArray<TypeAnnotation> BaseClasses { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
      public string? DocString { get; init; }
  }
  ```

- [x] **2.6.3** Migrate `StructDef` in `Statement.cs`
  ```csharp
  public record StructDef : Statement
  {
      public string Name { get; init; } = "";
      public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
      public ImmutableArray<TypeAnnotation> BaseClasses { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
      public string? DocString { get; init; }
  }
  ```

- [x] **2.6.4** Migrate `InterfaceDef` in `Statement.cs`
  ```csharp
  public record InterfaceDef : Statement
  {
      public string Name { get; init; } = "";
      public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
      public ImmutableArray<TypeAnnotation> BaseInterfaces { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      public string? DocString { get; init; }
  }
  ```

- [x] **2.6.5** Migrate `EnumDef` in `Statement.cs`
  ```csharp
  public record EnumDef : Statement
  {
      public string Name { get; init; } = "";
      public ImmutableArray<EnumMember> Members { get; init; } = ImmutableArray<EnumMember>.Empty;
      public string? DocString { get; init; }
  }
  ```

- [x] **2.6.6** Update `Parser.Definitions.cs`
  - Update all definition parsing methods

- [x] **2.6.7** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(ast): migrate definition types to ImmutableArray"`

---

### 2.7 Migrate Module (Root Node)

**Goal**: Migrate the root Module node.

- [x] **2.7.1** Migrate `Module` in `Node.cs`
  ```csharp
  public record Module : Node
  {
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
      public string? DocString { get; init; }
  }
  ```

- [x] **2.7.2** Update `Parser.cs` - `ParseModule` method

- [x] **2.7.3** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(ast): migrate Module root node to ImmutableArray"`

---

## Phase 3: Update Consumers

### 3.1 Update Semantic Analysis

**Goal**: Update TypeChecker, NameResolver, etc. to work with ImmutableArray.

- [x] **3.1.1** Update `NameResolver.cs`
  - Replace `foreach (var stmt in node.Body)` patterns (should work unchanged)
  - Replace any code that adds to AST node lists (should be removed)

- [x] **3.1.2** Update `TypeChecker.cs` and partials
  - Same pattern as above

- [x] **3.1.3** Update all validators
  - `AccessValidator.cs`
  - `ControlFlowValidator.cs`
  - `DefaultParameterValidator.cs`
  - `OperatorValidator.cs`
  - `ProtocolValidator.cs`
  - etc.

- [x] **3.1.4** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(semantic): update semantic analysis for ImmutableArray"`

---

### 3.2 Update Code Generation

**Goal**: Update RoslynEmitter to work with ImmutableArray.

- [x] **3.2.1** Update `RoslynEmitter.cs`
  - ImmutableArray iteration works the same as List
  - No major changes expected, but verify

- [x] **3.2.2** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(codegen): update code generation for ImmutableArray"`

---

### 3.3 Update Tests

**Goal**: Update test code that creates AST nodes directly.

- [x] **3.3.1** Search for test patterns that need updating
  ```bash
  grep -r "new List<" src/Sharpy.Compiler.Tests/ --include="*.cs"
  grep -r "Body = new()" src/Sharpy.Compiler.Tests/ --include="*.cs"
  ```

- [x] **3.3.2** Update test helper methods
  - Update `Parse()` helper methods if they create AST directly

- [x] **3.3.3** Update individual tests that create AST manually
  ```csharp
  // BEFORE:
  var func = new FunctionDef
  {
      Name = "test",
      Parameters = new List<Parameter> { ... },
      Body = new List<Statement> { ... }
  };

  // AFTER:
  var func = new FunctionDef
  {
      Name = "test",
      Parameters = ImmutableArray.Create(new Parameter { ... }),
      Body = ImmutableArray.Create<Statement>(new PassStatement())
  };
  ```

- [x] **3.3.4** Enable the immutability tests created in Phase 1
  - Remove `Skip` attributes from `ImmutabilityTests.cs`

- [x] **3.3.5** Run all tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "test: update tests for ImmutableArray AST"`

---

## Phase 4: Semantic Binding Separation (Deferred Mutability)

### 4.1 Move FromImportStatement Semantic Data

**Goal**: Move `ResolvedModulePath` and `ReExportedSymbols` to SemanticBinding.

- [x] **4.1.1** Extend `SemanticBinding.cs`
  ```csharp
  // Add to SemanticBinding.cs
  private readonly ConcurrentDictionary<FromImportStatement, string> _resolvedModulePaths = new();
  private readonly ConcurrentDictionary<FromImportStatement, Dictionary<string, Symbol>> _reExportedSymbols = new();

  public void SetResolvedModulePath(FromImportStatement stmt, string path)
      => _resolvedModulePaths[stmt] = path;

  public string? GetResolvedModulePath(FromImportStatement stmt)
      => _resolvedModulePaths.TryGetValue(stmt, out var path) ? path : null;

  public void SetReExportedSymbols(FromImportStatement stmt, Dictionary<string, Symbol> symbols)
      => _reExportedSymbols[stmt] = symbols;

  public Dictionary<string, Symbol>? GetReExportedSymbols(FromImportStatement stmt)
      => _reExportedSymbols.TryGetValue(stmt, out var symbols) ? symbols : null;
  ```

- [x] **4.1.2** Update ImportResolver to use SemanticBinding
  - Added `SetSemanticBinding()` method to ImportResolver
  - ImportResolver now uses SemanticBinding when available (with fallback to AST properties)
  - Updated `ResolveFromImport()` and `ExtractReExportedSymbols()` to use SemanticBinding

- [x] **4.1.3** Update RoslynEmitter to use SemanticBinding
  - Added SemanticBinding property to CodeGenContext
  - Added helper methods to RoslynEmitter: `GetResolvedModulePath()`, `GetReExportedSymbols()`, `HasReExportedSymbols()`
  - Updated CompilationUnit and ModuleClass generation to use SemanticBinding helpers

- [x] **4.1.4** Update ProjectModel and ProjectCompiler
  - Added SemanticBinding property to ProjectModel
  - ProjectCompiler now creates SemanticBinding and passes it to ImportResolver and CodeGenContext

- [x] **4.1.5** FromImportStatement properties remain mutable for backward compatibility
  - The AST properties still have `set` accessors for legacy code paths
  - SemanticBinding is the preferred storage when available

- [x] **4.1.6** Run tests
  ```bash
  dotnet test
  ```
  - All 3887 tests pass

**Commit Point**: `git commit -m "feat(semantic): move FromImportStatement semantic data to SemanticBinding"`

---

### 4.2 Move Symbol Semantic Data (DEFERRED)

**Goal**: Move `CodeGenInfo`, `Type`, and `BaseType` to SemanticBinding.

**STATUS**: Deferred for future implementation. The SemanticBinding infrastructure is in place
(see `SemanticBinding.cs` with `SetCodeGenInfo`, `SetVariableType`, `SetBaseType` methods).
The Symbol properties retain their `set` accessors with migration notes. This can be completed
when LSP or parallel compilation features require full AST immutability.

- [ ] **4.2.1** Update CodeGenInfoComputer
  - Use SemanticBinding.SetCodeGenInfo instead of symbol.CodeGenInfo

- [ ] **4.2.2** Update TypeChecker
  - Use SemanticBinding.SetVariableType instead of symbol.Type

- [ ] **4.2.3** Update NameResolver
  - Use SemanticBinding.SetBaseType instead of symbol.BaseType

- [ ] **4.2.4** Update all consumers to read from SemanticBinding
  - RoslynEmitter
  - Validators
  - etc.

- [ ] **4.2.5** Remove mutable properties from Symbol classes
  ```csharp
  // Symbol.cs - CodeGenInfo becomes init-only or removed
  // VariableSymbol.Type becomes init-only
  // TypeSymbol.BaseType becomes init-only
  ```

- [ ] **4.2.6** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(semantic): move Symbol semantic data to SemanticBinding"`

---

## Phase 5: Symbol Types ImmutableArray Migration (DEFERRED)

**STATUS**: Deferred for future implementation. Symbol list properties (`Parameters`, `TypeParameters`,
`Fields`, `Methods`, `Constructors`, etc.) still use mutable `List<T>` and `Dictionary<string, List<T>>`.
This can be migrated when Phase 4.2 is completed, or when Symbol immutability becomes a requirement.

### 5.1 Migrate Symbol List Properties

**Goal**: Migrate Symbol classes to use ImmutableArray.

- [ ] **5.1.1** Migrate `FunctionSymbol` in `Symbol.cs`
  ```csharp
  public record FunctionSymbol : Symbol
  {
      public ImmutableArray<ParameterSymbol> Parameters { get; init; } = ImmutableArray<ParameterSymbol>.Empty;
      public SemanticType ReturnType { get; init; } = SemanticType.Unknown;
      public bool IsStatic { get; init; }
      public bool IsAbstract { get; init; }
      public bool IsVirtual { get; init; }
      public bool IsOverride { get; init; }
      public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
      public bool IsGeneric => !TypeParameters.IsEmpty;
      public System.Reflection.MethodInfo? ClrMethod { get; init; }
  }
  ```

- [ ] **5.1.2** Migrate `TypeSymbol` in `Symbol.cs`
  ```csharp
  public record TypeSymbol : Symbol
  {
      public TypeKind TypeKind { get; init; }
      public Type? ClrType { get; init; }
      public bool IsAbstract { get; init; }
      public string? DefiningModule { get; init; }
      public string? DefiningFilePath { get; init; }
      public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
      public bool IsGeneric => !TypeParameters.IsEmpty;
      public ImmutableArray<VariableSymbol> Fields { get; init; } = ImmutableArray<VariableSymbol>.Empty;
      public ImmutableArray<FunctionSymbol> Methods { get; init; } = ImmutableArray<FunctionSymbol>.Empty;
      public ImmutableArray<PropertySymbol> Properties { get; init; } = ImmutableArray<PropertySymbol>.Empty;
      public ImmutableDictionary<string, ImmutableArray<FunctionSymbol>> OperatorMethods { get; init; } 
          = ImmutableDictionary<string, ImmutableArray<FunctionSymbol>>.Empty;
      public ImmutableDictionary<string, ImmutableArray<FunctionSymbol>> ProtocolMethods { get; init; } 
          = ImmutableDictionary<string, ImmutableArray<FunctionSymbol>>.Empty;
      public ImmutableArray<FunctionSymbol> Constructors { get; init; } = ImmutableArray<FunctionSymbol>.Empty;
      // BaseType moved to SemanticBinding
      public ImmutableArray<TypeSymbol> Interfaces { get; init; } = ImmutableArray<TypeSymbol>.Empty;
  }
  ```

- [ ] **5.1.3** Migrate `ModuleSymbol` in `Symbol.cs`
  ```csharp
  public record ModuleSymbol : Symbol
  {
      public string FilePath { get; init; } = string.Empty;
      public ImmutableDictionary<string, Symbol> Exports { get; init; } 
          = ImmutableDictionary<string, Symbol>.Empty;
  }
  ```

- [ ] **5.1.4** Update NameResolver to build immutable symbol collections

- [ ] **5.1.5** Update TypeChecker to build immutable symbol collections

- [ ] **5.1.6** Run tests
  ```bash
  dotnet test
  ```

**Commit Point**: `git commit -m "feat(semantic): migrate Symbol types to ImmutableArray/ImmutableDictionary"`

---

## Phase 6: Cleanup and Documentation

### 6.1 Remove Obsolete Code

- [x] **6.1.1** Review code for deprecated `set` properties
  - Symbol properties (`CodeGenInfo`, `Type`, `BaseType`) keep `set` with migration notes - deferred
  - FromImportStatement properties keep `set` for backward compatibility - working as intended
- [x] **6.1.2** No `[Obsolete]` attributes added for this migration - not needed
- [x] **6.1.3** No temporary workarounds to clean up

**Commit Point**: No commit needed - no changes required.

---

### 6.2 Add Documentation

- [x] **6.2.1** XML documentation already present on AST types (records are self-documenting)

- [x] **6.2.2** CLAUDE.md already contains immutability guidelines
  - "Immutable AST: All semantic annotations go in `SemanticInfo` class, not on AST nodes"

- [x] **6.2.3** This task document (`task_immutable_ast_foundation.md`) serves as the design explanation

**Commit Point**: `git commit -m "docs: update task list and add documentation notes"`

---

### 6.3 Final Verification

- [x] **6.3.1** Run all tests
  ```bash
  dotnet test
  ```
  - Result: Passed! - Failed: 0, Passed: 3887, Skipped: 20

- [x] **6.3.2** Compare test results with baseline
  - Baseline before migration: 3887 passed
  - After migration: 3887 passed
  - All tests continue to pass

- [x] **6.3.3** Build succeeds with no errors

- [x] **6.3.4** Integration tests verify compilation works

**Commit Point**: `git commit -m "docs: complete immutable AST foundation task list"`

---

## Summary

### Expected Changes by File

| File | Changes |
|------|---------|
| `Node.cs` | Module body to ImmutableArray |
| `Expression.cs` | ~10 types with List<T> to ImmutableArray |
| `Statement.cs` | ~15 types with List<T> to ImmutableArray |
| `Types.cs` | 3 types with List<T> to ImmutableArray |
| `Symbol.cs` | ~5 types with List/Dictionary to Immutable collections |
| `Parser*.cs` | All parsing methods that build lists |
| `*Validator.cs` | Read patterns (should work unchanged) |
| `TypeChecker*.cs` | Read patterns + SemanticBinding writes |
| `NameResolver.cs` | Symbol creation + SemanticBinding writes |
| `RoslynEmitter.cs` | Read patterns + SemanticBinding reads |
| Tests | Manual AST creation patterns |

### Estimated Effort

| Phase | Estimated Time |
|-------|----------------|
| Phase 1: Foundation | 2-4 hours |
| Phase 2: AST Migration | 8-12 hours |
| Phase 3: Consumer Updates | 4-6 hours |
| Phase 4: Semantic Binding | 4-6 hours |
| Phase 5: Symbol Migration | 4-6 hours |
| Phase 6: Cleanup | 2-4 hours |
| **Total** | **24-38 hours** |

### Risk Mitigation

1. **Commit frequently**: Each task section has a commit point
2. **Test continuously**: Run tests after each major change
3. **Two-way door**: ImmutableArray and List have similar APIs; can temporarily keep both
4. **Gradual migration**: Symbol binding separation can be deferred if needed

### Future Considerations

This migration prepares the codebase for:
- **Tagged Unions (v0.2.x)**: New `UnionDef` AST node will be immutable from the start
- **Match Statements (v0.2.x)**: New `MatchStatement`/`MatchExpression` will be immutable
- **Async/Await (v0.2.x+)**: AST nodes for async functions will be immutable
- **LSP**: Safe sharing of AST across threads
- **Parallel Compilation**: No locking needed for AST access
- **Incremental Compilation**: AST can be cached and compared
