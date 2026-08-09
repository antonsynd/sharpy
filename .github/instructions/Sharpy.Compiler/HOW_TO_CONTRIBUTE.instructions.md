---
applyTo: "src/Sharpy.Compiler/**"
---
# Sharpy.Compiler

Core compiler: Lexer → Parser → Semantic → ValidationPipeline → CodeGen. Location: `src/Sharpy.Compiler/`

## Directory Structure

```
Sharpy.Compiler/
├── Lexer/           # Tokenization (Lexer*.cs — 4 partials, Token.cs)
├── Parser/          # Recursive descent → AST (Parser*.cs — 6 files, Ast/*.cs)
├── Semantic/        # NameResolver → ImportResolver → TypeResolver → TypeChecker (11 partial files)
│   └── Validation/  # Pluggable validators (OperatorValidator, etc.)
├── CodeGen/         # RoslynEmitter*.cs (22 partial files), TypeMapper.cs, NameMangler.cs
├── Discovery/       # CLR type discovery, module imports, caching
├── Analysis/        # Control flow analysis (ControlFlowGraph, BasicBlock)
├── Diagnostics/     # DiagnosticBag, DiagnosticCodes, DiagnosticRenderer
├── Model/           # CompilationUnit, ProjectModel
├── Project/         # ProjectCompiler, SpyProject, DependencyGraph
├── Services/        # CompilerServices, CompilerServicesBuilder
├── Text/            # SourceText, TextSpan, ILocatable
├── Logging/         # Compiler logging infrastructure
├── Utilities/       # Shared utility classes
├── Compiler.cs      # Single-file compilation
└── AssemblyCompiler.cs  # Multi-file projects
```

## Adding a Language Feature

Touch components **in order** (dependencies flow left→right):

1. **Lexer:** `Token.cs` (add `TokenType`), `Lexer.cs` (recognize it)
2. **Parser:** `Parser/Ast/*.cs` (add AST record), `Parser.cs` (parsing rules)
3. **Semantic:** `TypeChecker*.cs` (type rules), add validator if needed
4. **CodeGen:** `RoslynEmitter*.cs` (C# emission via SyntaxFactory)
5. **Tests:** Unit tests per component + `.spy`/`.expected` integration tests

**Before implementing:** Check `docs/language_specification/` for spec compliance.

## Key Design Patterns

**AST nodes are immutable records:**
```csharp
public record FunctionDef : Statement {
    public string Name { get; init; }
    public List<Parameter> Parameters { get; init; }
    // Source location tracked via Node base class
}
```

**Semantic info stored in `SemanticInfo`, never on AST:**
```csharp
// SemanticInfo is the single source of truth for resolved types/symbols
semanticInfo.SetType(expression, resolvedType);
semanticInfo.SetSymbol(name, symbol);
// AST nodes remain immutable throughout compilation
```

**Code generation uses Roslyn `SyntaxFactory` exclusively:**
```csharp
// ✅ Correct — use SyntaxFactory methods
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
return MethodDeclaration(returnType, Identifier("MyMethod"))
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithBody(Block(statements));

// ❌ NEVER use string templating
$"public {returnType} MyMethod() {{ }}"
```

**A lowering evaluates each operand expression exactly once.**

An operand arrives as an `ExpressionSyntax` the emitter did not build and cannot inspect for
side effects. Splicing it into two positions of the emitted tree — classically a guard and the
guarded value — makes `x % f()` call `f()` twice. Nothing in the fixture suite catches this by
accident, because fixtures overwhelmingly use side-effect-free operands and a doubly-evaluated
operand then produces identical output.

```csharp
// ❌ The divisor is spliced twice — `x % f()` calls f() twice (#1216)
return ParenthesizedExpression(ConditionalExpression(
    BinaryExpression(SyntaxKind.EqualsExpression, right, Literal(0m)),
    ThrowExpression(...),
    InvocationExpression(...).AddArgumentListArguments(Argument(left), Argument(right))));

// ✅ The guard lives in the Core helper; each operand is spliced once
return InvocationExpression(
    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
        MakeGlobalQualifiedName("Sharpy", "Builtins"), IdentifierName("DecimalMod")))
    .AddArgumentListArguments(Argument(left), Argument(right));
```

Three remedies, in order of preference:

1. **Move the guard into a `Sharpy.Core` helper.** Inside the helper the operands are runtime
   parameters, so the guard costs nothing at the emission site. This is what
   `Builtins.FloorMod`, `FloorDiv`, `DecimalMod` and `DecimalFloorDiv` are for. It also
   dissolves the CS0020 workaround — "division by *constant* zero" cannot arise from a
   parameter, so a helper may use plain `/` and `%` where the emitter had to use
   `Decimal.Divide`/`Decimal.Remainder`.
2. **Capture with `EnsureSingleEvaluation`** (`RoslynEmitter.Expressions.Access.Calls.cs`),
   which binds the value with an inline `is var __temp` pattern when the AST operand is not
   side-effect-free. Used by the binary `??` lowering and the optional-member-access chain.
3. **Hoist into a temp local** via `_hoistedStatements`, as `GenerateTupleElementArray` does
   when it needs `.Item1…ItemN` off one operand.

Two traps:

- **Gating on `IsSideEffectFree` is not a remedy.** It degrades the lowering (a different,
  weaker lowering is chosen for a side-effecting operand), so two spellings of one operation
  get different runtime behavior — and it is easy to gate the wrong operand. The `**`
  checked-power path at `RoslynEmitter.Expressions.Operators.cs:90` does both, and is a defect,
  not a sanctioned exception: it gates only `binOp.Right` while regenerating `binOp.Left`
  unguarded (#1228). Do not copy it.
- **Do not call `GenerateExpression` twice on the same AST node** to get a "fresh" node for a
  second position. `GenerateExpression` is not pure — it can push into `_hoistedStatements`,
  and hoisted statements run unconditionally, so the second call duplicates a side effect even
  when the two use sites are mutually exclusive ternary arms (#1228). Reuse or capture the
  already-generated syntax instead.

The historical violations are fixed: #1226 (integer `//`) and #1228 (`**`) each emit one
Core-helper invocation splicing every operand once, and #1227 hoists augmented-assignment
targets (calls AND property reads — a member read is repeatable only when it is a plain
field, `MemberReadIsPlainField`). The standing guards are the evaluation-count fixtures
(`single_evaluation_*.spy`) and the enum-driven
`AugmentedAssignmentSingleEvaluationTests`, which forces every new `AssignmentOperator` to
declare its single-evaluation story.

The general guard now exists (#1334): `GenerateExpressionReentryTests` counts, per Sharpy
statement, how often each AST node passes through `GenerateExpression` — the one wrapper
every expression goes through — and fails on any node reached twice. It sweeps the whole
executing single-file corpus (~1,720 fixtures, ~9s). So a new double-generation does not
need anyone to have guessed the shape: it fails on whichever fixture contains it. The
recorder is installed by a test-side `ICodeEmitterFactory` (`IExpressionGenerationRecorder`);
production emit holds a null field and pays one null check.

Note what the guard does NOT cover, measured rather than assumed (#1351): it sees
**re-generation**, not **re-splicing**. `HoistAugmentedTargetOperand` takes an
already-generated `ExpressionSyntax` and the caller splices the same syntax into the read
and the write, so `GenerateExpression` runs once — inverting `MemberReadIsPlainField` to
reintroduce `abc5bf4b0` leaves the sweep green. Both historical instances of this class
were re-splicing. `single_evaluation_*.spy` and `AugmentedAssignmentSingleEvaluationTests`
are what cover that half; neither mechanism subsumes the other. Reuse-or-capture is still
the rule.

## Semantic Analysis Pipeline

Six-stage architecture (order matters):

```
NameResolver.ResolveDeclarations()  → Pass 1: build symbol table
NameResolver.ResolveInheritance()   → Pass 1b: resolve base classes
ImportResolver                      → Pass 1.5: module imports
TypeResolver.ResolveTypes()         → Pass 2: resolve type annotations
TypeChecker.CheckModule()           → Pass 3: type checking + inference
ValidationPipeline.Validate()       → Pass 4: operators/protocols/access
```

**Materialization points:** After each phase, computed data is frozen from `SemanticBinding` onto `Symbol` properties.

## Validation Pipeline Architecture

After `TypeChecker`, pluggable validators run via `ValidationPipeline`. Validators implement `ISemanticValidator` with an `Order` property (lower runs first):

| Order | Validator | Purpose |
|-------|-----------|---------|
| 50 | `ModuleLevelValidator` | Entry point validation |
| 55 | `NamingConventionValidator` | Naming convention checks |
| 60 | `DecoratorValidator` | Decorator validation |
| 150 | `SignatureValidator` | Dunder method signatures |
| 160 | `EqualityContractValidator` | Equality contract checks |
| 170 | `InterfaceConflictValidator` | Interface conflict detection |
| 250 | `DefaultParameterValidator` | Default parameter validation |
| 400 | `ControlFlowValidator` | CFG-based unreachable code, missing returns |
| 410 | `PropertyValidator` | Property validation |
| 420 | `UnusedVariableValidator` | Unused variable warnings |
| 430 | `UnusedImportValidator` | Unused import warnings |
| 450 | `AccessValidator` | Private/protected member access |
| 460 | `DunderInvocationValidator` | Direct dunder call warnings |
| 500 | `ProtocolValidator`, `OperatorValidator` | Protocol/operator validation |

**Responsibility split:** TypeChecker handles type mismatches and in-progress inference. ValidationPipeline handles self-contained AST analyses. See `Semantic/Validation/README.md`.

## Type Narrowing

`TypeChecker._narrowedTypes` tracks flow-sensitive types:
- `if x is not None:` → narrows `T?` to `T` in branch
- `isinstance(x, SomeClass)` → narrows to `SomeClass`

## Testing

```bash
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

**CRITICAL:** Fix bugs, don't change test expectations. Use `[Fact(Skip = "reason")]` if blocked.

## Debugging

```bash
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect C# output
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Inspect AST
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Inspect tokens
```

## Key Files

| File | Purpose |
|------|---------|
| `TypeMapper.cs` | Sharpy→C# types: `list[T]` → `global::Sharpy.Core.List<T>` |
| `NameMangler.cs` | `snake_case` → `PascalCase`, `__str__` → `ToString()` |
| `SemanticInfo.cs` | Type/symbol annotations (separate from AST) |
| `SemanticBinding.cs` | Computed data, materialized at phase boundaries |
| `CodeGenInfo.cs` | Per-symbol codegen metadata (invocation style, etc.) |
| `RoslynEmitter*.cs` | 22 partial classes by AST category |
| `PrimitiveCatalog.cs` | Source of truth for primitive types and CLR mappings |
| `OperatorRegistry.cs` | Operator type rules |

## C# 9.0 Constraints (Sharpy.Core Only)

`Sharpy.Core` targets `netstandard2.1;netstandard2.0` with `LangVersion 9.0`. `Sharpy.Compiler` and `Sharpy.Cli` target `net10.0` with `LangVersion latest`.

| ✅ C# 9.0 Available | ❌ Not Available (C# 10+) |
|---------------------|-------------------------|
| Records | File-scoped namespaces |
| Init-only setters | Global usings |
| Target-typed new | Record structs |
| Pattern matching | Required members |
