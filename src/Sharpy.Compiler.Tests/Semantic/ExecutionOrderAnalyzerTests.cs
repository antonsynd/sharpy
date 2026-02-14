using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Xunit;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for ExecutionOrderAnalyzer which detects module-level variables
/// that have execution order issues and cannot be static fields.
/// </summary>
public class ExecutionOrderAnalyzerTests
{
    private (Module module, SymbolTable symbolTable, HashSet<string> issues) Analyze(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var analyzer = new ExecutionOrderAnalyzer(symbolTable);
        var issues = analyzer.Analyze(module.Body);

        return (module, symbolTable, issues);
    }

    // ==============================================
    // Basic Cases - No Execution Order Issues
    // ==============================================

    [Fact]
    public void SimpleVariable_WithLiteral_NoIssues()
    {
        var source = "x: int = 42";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void ConstVariable_NoIssues()
    {
        var source = "const X: int = 42";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void Variable_WithFunctionCallToDefinedFunction_NoIssues()
    {
        var source = @"
def get_value() -> int:
    return 42

result: int = get_value()
";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void Variable_ReferencingConst_NoIssues()
    {
        var source = @"
const X: int = 10
y: int = X
";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void Variable_WithBuiltinCall_NoIssues()
    {
        var source = "x: int = len([1, 2, 3])";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void Variable_ReferencingTypeName_NoIssues()
    {
        var source = @"
class MyClass:
    pass

instance: MyClass = MyClass()
";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    // ==============================================
    // Assignment Before Declaration
    // ==============================================

    [Fact]
    public void AssignmentBeforeDeclaration_HasIssues()
    {
        var source = @"
x = 5
x: int = 10
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("x");
    }

    [Fact]
    public void MultipleAssignmentsBeforeDeclaration_HasIssues()
    {
        var source = @"
x = 1
x = 2
x: int = 3
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("x");
    }

    // ==============================================
    // Multiple Declarations
    // ==============================================

    [Fact]
    public void MultipleDifferentDeclarations_AllHaveIssues()
    {
        // When the same name is declared twice, both have issues
        var source = @"
x: int = 1
x: int = 2
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("x");
    }

    // ==============================================
    // Reference to Assignment Variable
    // ==============================================

    [Fact]
    public void ReferenceToAssignmentVariable_HasIssues()
    {
        var source = @"
x = 5
y: int = x
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("y");
    }

    [Fact]
    public void ReferenceToAssignmentVariable_InExpression_HasIssues()
    {
        var source = @"
x = 5
y: int = x + 10
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("y");
    }

    [Fact]
    public void ReferenceToAssignmentVariable_InFunctionCall_HasIssues()
    {
        var source = @"
x = 5
y: int = abs(x)
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("y");
    }

    // ==============================================
    // Transitive Dependencies
    // ==============================================

    [Fact]
    public void TransitiveDependency_OnAssignmentVariable_HasIssues()
    {
        var source = @"
x = 5
y: int = x
z: int = y
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("y", "y references assignment variable x");
        issues.Should().Contain("z", "z references y which has issues");
    }

    [Fact]
    public void TransitiveDependency_ThroughMultipleLevels_HasIssues()
    {
        var source = @"
a = 1
b: int = a
c: int = b
d: int = c
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("b");
        issues.Should().Contain("c");
        issues.Should().Contain("d");
    }

    [Fact]
    public void TransitiveDependency_OnVariableWithIssues_HasIssues()
    {
        var source = @"
x = 1
x: int = 2
y: int = x
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("x", "x has assignment before declaration");
        issues.Should().Contain("y", "y references x which has issues");
    }

    // ==============================================
    // Assignment Variables (no type annotation)
    // ==============================================

    [Fact]
    public void AssignmentVariable_Itself_HasIssues()
    {
        var source = "x = 42";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("x", "assignment variables always have execution order issues");
    }

    [Fact]
    public void MultipleAssignmentVariables_AllHaveIssues()
    {
        var source = @"
x = 1
y = 2
z = 3
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("x");
        issues.Should().Contain("y");
        issues.Should().Contain("z");
    }

    // ==============================================
    // Mixed Scenarios
    // ==============================================

    [Fact]
    public void MixedScenario_SomeWithIssues_SomeWithout()
    {
        var source = @"
const A: int = 1
x = 5
y: int = x
z: int = A
";
        var (_, _, issues) = Analyze(source);

        issues.Should().NotContain("A", "const has no issues");
        issues.Should().Contain("x", "assignment variable has issues");
        issues.Should().Contain("y", "references assignment variable");
        issues.Should().NotContain("z", "references const, no issues");
    }

    [Fact]
    public void FunctionCallWithAssignmentArg_HasIssues()
    {
        var source = @"
def process(val: int) -> int:
    return val * 2

x = 5
y: int = process(x)
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("x");
        issues.Should().Contain("y", "function argument references assignment variable");
    }

    // ==============================================
    // Complex Expressions
    // ==============================================

    [Fact]
    public void ListLiteral_WithAssignmentReference_HasIssues()
    {
        var source = @"
x = 5
y: list[int] = [x, 10, 20]
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("y");
    }

    [Fact]
    public void DictLiteral_WithAssignmentReference_HasIssues()
    {
        var source = @"
k = ""key""
d: dict[str, int] = {k: 42}
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("d");
    }

    [Fact]
    public void TupleLiteral_WithAssignmentReference_HasIssues()
    {
        var source = @"
x = 5
t: tuple[int, int] = (x, 10)
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("t");
    }

    [Fact]
    public void FString_WithAssignmentReference_HasIssues()
    {
        var source = @"
name = ""World""
greeting: str = f""Hello, {name}!""
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("greeting");
    }

    // ==============================================
    // Edge Cases
    // ==============================================

    [Fact]
    public void EmptyModule_NoIssues()
    {
        var source = "";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void OnlyFunctionDefinitions_NoIssues()
    {
        var source = @"
def foo() -> int:
    return 1

def bar() -> int:
    return foo()
";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void OnlyClassDefinitions_NoIssues()
    {
        var source = @"
class Foo:
    x: int

class Bar(Foo):
    y: int
";
        var (_, _, issues) = Analyze(source);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void Variable_ReferencingOtherDeclaredVariable_HasIssues()
    {
        // Non-const module variables referencing each other
        var source = @"
x: int = 10
y: int = x
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("y", "y references non-const module variable x");
    }

    [Fact]
    public void Variable_WithSelfReference_HasIssues()
    {
        var source = @"
x = x + 1
";
        var (_, _, issues) = Analyze(source);

        issues.Should().Contain("x");
    }

    [Fact]
    public void ChainedDeclarations_AllHaveIssues()
    {
        // Each variable references the previous one
        var source = @"
a: int = 1
b: int = a
c: int = b
d: int = c
";
        var (_, _, issues) = Analyze(source);

        // a has no issues (literal initializer)
        issues.Should().NotContain("a");
        // b, c, d all reference non-const module variables
        issues.Should().Contain("b");
        issues.Should().Contain("c");
        issues.Should().Contain("d");
    }
}
