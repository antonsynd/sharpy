# Follow-Up Tasks: Enhance CodeGenInfoComputer for Execution Order Detection

**Blocker:** Part A cannot fully remove legacy fallback code until `CodeGenInfoComputer` detects all execution order issues that `GenerateModuleClass()` currently detects.

**Root Cause:** The current `CodeGenInfoComputer.HasExecutionOrderIssues()` only checks if an initializer contains runtime expressions. The legacy code in `RoslynEmitter.ModuleClass.cs` does multi-pass analysis across all statements.

---

## Tasks

### F.1: Understand the Legacy Detection Logic

- [ ] Read `RoslynEmitter.ModuleClass.cs` lines 75-200 (the pre-scan in `GenerateModuleClass`)
- [ ] Document what it detects:
  1. **Assigned before declared**: Variable used in Assignment before VariableDeclaration
  2. **Multiple declarations**: Same variable name declared more than once
  3. **References assignment variables**: Initializer references a variable created by Assignment (no type annotation)
  4. **References other module variables**: Initializer references non-const module variable declared later
  5. **Transitive closure**: If A references B and B has issues, A has issues

---

### F.2: Refactor Detection Logic into Reusable Class

- [ ] Create `src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs`:

```csharp
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Analyzes module-level statements to detect variables with execution order issues.
/// A variable has execution order issues if it cannot safely be a static field initializer.
///
/// This logic was previously embedded in RoslynEmitter.GenerateModuleClass().
/// Moving it here allows CodeGenInfoComputer to use it during semantic analysis.
/// </summary>
public class ExecutionOrderAnalyzer
{
    private readonly SymbolTable _symbolTable;

    // Track statement positions
    private readonly Dictionary<string, int> _variableFirstSeen = new();      // First Assignment position
    private readonly Dictionary<string, int> _variableFirstDeclared = new();  // First VariableDeclaration position

    // Track variable categories
    private readonly HashSet<string> _constVariables = new();
    private readonly HashSet<string> _assignmentVariables = new();  // Created by Assignment, not VariableDeclaration
    private readonly HashSet<string> _typeAndFunctionNames = new();

    // Result
    private readonly HashSet<string> _variablesWithIssues = new();

    public ExecutionOrderAnalyzer(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
    }

    /// <summary>
    /// Analyze all module statements and return set of variable names with execution order issues.
    /// </summary>
    public HashSet<string> Analyze(IReadOnlyList<Statement> statements)
    {
        _variablesWithIssues.Clear();

        // Pass 1: Collect type/function names and const variables
        CollectDeclarationNames(statements);

        // Pass 2: Track variable positions and detect basic issues
        DetectBasicIssues(statements);

        // Pass 3: Detect initializer dependencies (transitive closure)
        DetectInitializerDependencies(statements);

        return new HashSet<string>(_variablesWithIssues);
    }

    private void CollectDeclarationNames(IReadOnlyList<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    _typeAndFunctionNames.Add(classDef.Name);
                    break;
                case StructDef structDef:
                    _typeAndFunctionNames.Add(structDef.Name);
                    break;
                case FunctionDef funcDef:
                    _typeAndFunctionNames.Add(funcDef.Name);
                    break;
                case EnumDef enumDef:
                    _typeAndFunctionNames.Add(enumDef.Name);
                    break;
                case InterfaceDef interfaceDef:
                    _typeAndFunctionNames.Add(interfaceDef.Name);
                    break;
                case VariableDeclaration varDecl when varDecl.IsConst || IsConstantCaseName(varDecl.Name):
                    _constVariables.Add(varDecl.Name);
                    break;
            }
        }
    }

    private void DetectBasicIssues(IReadOnlyList<Statement> statements)
    {
        for (int i = 0; i < statements.Count; i++)
        {
            var stmt = statements[i];

            if (stmt is VariableDeclaration varDecl && !_constVariables.Contains(varDecl.Name))
            {
                var varName = varDecl.Name;

                if (_variableFirstDeclared.ContainsKey(varName))
                {
                    // Multiple declarations
                    _variablesWithIssues.Add(varName);
                }
                else
                {
                    _variableFirstDeclared[varName] = i;

                    // Check if assigned before declared
                    if (_variableFirstSeen.TryGetValue(varName, out var firstSeen) && firstSeen < i)
                    {
                        _variablesWithIssues.Add(varName);
                    }
                }
            }
            else if (stmt is Assignment assign && assign.Target is Identifier targetId)
            {
                var varName = targetId.Name;

                if (!_variableFirstSeen.ContainsKey(varName))
                {
                    _variableFirstSeen[varName] = i;
                }

                // Track variables created by Assignment (no VariableDeclaration)
                // These will be local variables in Main()
                if (!_variableFirstDeclared.ContainsKey(varName) && !_constVariables.Contains(varName))
                {
                    _assignmentVariables.Add(varName);
                }
            }
        }
    }

    private void DetectInitializerDependencies(IReadOnlyList<Statement> statements)
    {
        // Build map of variable -> initializer
        var variableDeclarations = new Dictionary<string, VariableDeclaration>();
        foreach (var stmt in statements)
        {
            if (stmt is VariableDeclaration varDecl &&
                !_constVariables.Contains(varDecl.Name) &&
                varDecl.InitialValue != null)
            {
                variableDeclarations[varDecl.Name] = varDecl;
            }
        }

        // Iterate until no new issues found (transitive closure)
        bool changed = true;
        while (changed)
        {
            changed = false;

            foreach (var (varName, varDecl) in variableDeclarations)
            {
                if (_variablesWithIssues.Contains(varName))
                    continue;

                var referencedIds = new HashSet<string>();
                CollectReferencedIdentifiers(varDecl.InitialValue!, referencedIds);

                foreach (var refId in referencedIds)
                {
                    // Skip types, functions, consts
                    if (_typeAndFunctionNames.Contains(refId) || _constVariables.Contains(refId))
                        continue;

                    // Skip builtins
                    var symbol = _symbolTable.Lookup(refId);
                    if (symbol is FunctionSymbol or TypeSymbol)
                        continue;

                    // If references a variable with issues -> this has issues
                    if (_variablesWithIssues.Contains(refId))
                    {
                        _variablesWithIssues.Add(varName);
                        changed = true;
                        break;
                    }

                    // If references an assignment variable -> this has issues
                    if (_assignmentVariables.Contains(refId))
                    {
                        _variablesWithIssues.Add(varName);
                        changed = true;
                        break;
                    }

                    // If references another module variable (non-const) -> this has issues
                    // (static field initialization order is undefined)
                    if (variableDeclarations.ContainsKey(refId))
                    {
                        _variablesWithIssues.Add(varName);
                        changed = true;
                        break;
                    }
                }
            }
        }
    }

    private void CollectReferencedIdentifiers(Expression expr, HashSet<string> identifiers)
    {
        switch (expr)
        {
            case Identifier id:
                identifiers.Add(id.Name);
                break;
            case BinaryOp binOp:
                CollectReferencedIdentifiers(binOp.Left, identifiers);
                CollectReferencedIdentifiers(binOp.Right, identifiers);
                break;
            case UnaryOp unaryOp:
                CollectReferencedIdentifiers(unaryOp.Operand, identifiers);
                break;
            case FunctionCall call:
                if (call.Function is Identifier funcId)
                    identifiers.Add(funcId.Name);
                else
                    CollectReferencedIdentifiers(call.Function, identifiers);
                foreach (var arg in call.Arguments)
                    CollectReferencedIdentifiers(arg.Value, identifiers);
                break;
            case MemberAccess memberAccess:
                CollectReferencedIdentifiers(memberAccess.Object, identifiers);
                break;
            case IndexAccess indexAccess:
                CollectReferencedIdentifiers(indexAccess.Object, identifiers);
                CollectReferencedIdentifiers(indexAccess.Index, identifiers);
                break;
            case ConditionalExpression cond:
                CollectReferencedIdentifiers(cond.Test, identifiers);
                CollectReferencedIdentifiers(cond.ThenValue, identifiers);
                CollectReferencedIdentifiers(cond.ElseValue, identifiers);
                break;
            case Parenthesized paren:
                CollectReferencedIdentifiers(paren.Expression, identifiers);
                break;
            case ListLiteral list:
                foreach (var elem in list.Elements)
                    CollectReferencedIdentifiers(elem, identifiers);
                break;
            case DictLiteral dict:
                foreach (var (key, value) in dict.Entries)
                {
                    CollectReferencedIdentifiers(key, identifiers);
                    CollectReferencedIdentifiers(value, identifiers);
                }
                break;
            case TupleLiteral tuple:
                foreach (var elem in tuple.Elements)
                    CollectReferencedIdentifiers(elem, identifiers);
                break;
            // Literals don't reference identifiers
            case IntegerLiteral:
            case FloatLiteral:
            case StringLiteral:
            case BooleanLiteral:
            case NoneLiteral:
                break;
        }
    }

    private static bool IsConstantCaseName(string name)
    {
        return name.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c))
               && name.Any(char.IsUpper);
    }
}
```

- [ ] Run tests (new class, not yet integrated)

---

### F.3: Integrate ExecutionOrderAnalyzer into CodeGenInfoComputer

- [ ] Update `CodeGenInfoComputer.cs`:

```csharp
public class CodeGenInfoComputer
{
    private readonly SymbolTable _symbolTable;
    private readonly HashSet<string> _processedModuleLevelVars = new();
    private HashSet<string> _variablesWithExecutionOrderIssues = new();  // NEW

    // ... existing code ...

    public void ComputeForModule(Module module)
    {
        // NEW: Run execution order analysis first
        var analyzer = new ExecutionOrderAnalyzer(_symbolTable);
        _variablesWithExecutionOrderIssues = analyzer.Analyze(module.Body);

        // Rest of existing code...
        ProcessModuleLevelDeclarations(module);
        // ...
    }

    private void ProcessModuleLevelVariable(VariableDeclaration varDecl)
    {
        var symbol = _symbolTable.Lookup(varDecl.Name);
        if (symbol is VariableSymbol varSymbol)
        {
            // UPDATED: Use analyzer result instead of simple check
            var hasIssues = _variablesWithExecutionOrderIssues.Contains(varDecl.Name);

            varSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(varDecl.Name),
                OriginalName = varDecl.Name,
                Version = 0,
                IsModuleLevel = !hasIssues,  // Not module-level if has issues
                IsConstant = false,
                HasExecutionOrderIssues = hasIssues
            };
            _processedModuleLevelVars.Add(varDecl.Name);
        }
    }
}
```

- [ ] Run tests — all should pass

🏁 **Checkpoint F.3: Commit**
```bash
git add -A
git commit -m "feat(semantic): add ExecutionOrderAnalyzer for module-level variable detection"
```

---

### F.4: Add Tests for ExecutionOrderAnalyzer

- [ ] Create `src/Sharpy.Compiler.Tests/Semantic/ExecutionOrderAnalyzerTests.cs`:

```csharp
using Xunit;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

public class ExecutionOrderAnalyzerTests
{
    [Fact]
    public void DetectsAssignmentBeforeDeclaration()
    {
        // x = 1  (assignment)
        // x: int = 2  (declaration)
        var statements = new List<Statement>
        {
            new Assignment
            {
                Target = new Identifier { Name = "x" },
                Value = new IntegerLiteral { Value = 1 }
            },
            new VariableDeclaration
            {
                Name = "x",
                InitialValue = new IntegerLiteral { Value = 2 }
            }
        };

        var analyzer = new ExecutionOrderAnalyzer(new SymbolTable());
        var issues = analyzer.Analyze(statements);

        Assert.Contains("x", issues);
    }

    [Fact]
    public void DetectsMultipleDeclarations()
    {
        var statements = new List<Statement>
        {
            new VariableDeclaration { Name = "x", InitialValue = new IntegerLiteral { Value = 1 } },
            new VariableDeclaration { Name = "x", InitialValue = new IntegerLiteral { Value = 2 } }
        };

        var analyzer = new ExecutionOrderAnalyzer(new SymbolTable());
        var issues = analyzer.Analyze(statements);

        Assert.Contains("x", issues);
    }

    [Fact]
    public void DetectsReferenceToAssignmentVariable()
    {
        // y = 1  (assignment, no type annotation)
        // x: int = y + 1  (references y)
        var statements = new List<Statement>
        {
            new Assignment
            {
                Target = new Identifier { Name = "y" },
                Value = new IntegerLiteral { Value = 1 }
            },
            new VariableDeclaration
            {
                Name = "x",
                InitialValue = new BinaryOp
                {
                    Left = new Identifier { Name = "y" },
                    Operator = BinaryOperator.Add,
                    Right = new IntegerLiteral { Value = 1 }
                }
            }
        };

        var analyzer = new ExecutionOrderAnalyzer(new SymbolTable());
        var issues = analyzer.Analyze(statements);

        Assert.Contains("x", issues);
    }

    [Fact]
    public void DetectsTransitiveDependency()
    {
        // a: int = b + 1  (a depends on b)
        // b: int = c + 1  (b depends on c)
        // c = 1  (c is assignment variable)
        var statements = new List<Statement>
        {
            new VariableDeclaration
            {
                Name = "a",
                InitialValue = new BinaryOp
                {
                    Left = new Identifier { Name = "b" },
                    Operator = BinaryOperator.Add,
                    Right = new IntegerLiteral { Value = 1 }
                }
            },
            new VariableDeclaration
            {
                Name = "b",
                InitialValue = new BinaryOp
                {
                    Left = new Identifier { Name = "c" },
                    Operator = BinaryOperator.Add,
                    Right = new IntegerLiteral { Value = 1 }
                }
            },
            new Assignment
            {
                Target = new Identifier { Name = "c" },
                Value = new IntegerLiteral { Value = 1 }
            }
        };

        var analyzer = new ExecutionOrderAnalyzer(new SymbolTable());
        var issues = analyzer.Analyze(statements);

        Assert.Contains("a", issues);
        Assert.Contains("b", issues);
    }

    [Fact]
    public void ConstVariablesHaveNoIssues()
    {
        var statements = new List<Statement>
        {
            new VariableDeclaration
            {
                Name = "MAX_VALUE",
                IsConst = true,
                InitialValue = new IntegerLiteral { Value = 100 }
            }
        };

        var analyzer = new ExecutionOrderAnalyzer(new SymbolTable());
        var issues = analyzer.Analyze(statements);

        Assert.DoesNotContain("MAX_VALUE", issues);
    }

    [Fact]
    public void SimpleVariablesHaveNoIssues()
    {
        var statements = new List<Statement>
        {
            new VariableDeclaration
            {
                Name = "x",
                InitialValue = new IntegerLiteral { Value = 42 }
            }
        };

        var analyzer = new ExecutionOrderAnalyzer(new SymbolTable());
        var issues = analyzer.Analyze(statements);

        Assert.DoesNotContain("x", issues);
    }
}
```

- [ ] Run tests — all should pass

🏁 **Checkpoint F.4: Commit**
```bash
git add -A
git commit -m "test(semantic): add ExecutionOrderAnalyzer tests"
```

---

### F.5: Remove Legacy Detection from RoslynEmitter

Now that `CodeGenInfoComputer` has full detection, remove the legacy code from `GenerateModuleClass()`.

- [ ] In `RoslynEmitter.ModuleClass.cs`, find the pre-scan logic (approximately lines 75-200)
- [ ] Remove or comment out:
  - `variableFirstSeen` dictionary and its population
  - `variableFirstDeclaration` dictionary and its population
  - The `while (changed)` transitive closure loop
  - The `assignmentVariables` tracking
- [ ] Keep only:
  - `_moduleConstVariables` clearing (if still needed for local const tracking)
  - `_moduleFieldNames` clearing (still needed to prevent duplicate fields)
  - Type/function name collection (for class/struct name tracking)

- [ ] Run tests — all should pass

🏁 **Checkpoint F.5: Commit**
```bash
git add -A
git commit -m "refactor(codegen): remove legacy execution order detection from GenerateModuleClass"
```

---

### F.6: Resume Part A Tasks

- [ ] Return to Part A, task A.7 (Remove Legacy Fallback)
- [ ] Complete remaining Part A tasks

---

## Summary

The blocker was that `CodeGenInfoComputer` didn't replicate the sophisticated execution order analysis from `GenerateModuleClass()`. These follow-up tasks:

1. Extract the logic into a reusable `ExecutionOrderAnalyzer` class
2. Integrate it into `CodeGenInfoComputer`
3. Add comprehensive tests
4. Remove the legacy code from `RoslynEmitter`

This is a **two-way door**: The new analyzer can coexist with legacy code during development, and we only remove the legacy code after tests confirm the new analyzer works correctly.
