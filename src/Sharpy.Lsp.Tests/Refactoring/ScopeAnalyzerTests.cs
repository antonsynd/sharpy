using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Lsp.Refactoring;
using Xunit;

namespace Sharpy.Lsp.Tests.Refactoring;

public class ScopeAnalyzerTests
{
    private readonly CompilerApi _api = new();

    /// <summary>
    /// Parses source and returns the body of the first function named "target".
    /// </summary>
    private System.Collections.Generic.List<Statement> GetFunctionBody(string source, string functionName = "target")
    {
        var result = _api.Analyze(source, CancellationToken.None);
        result.Ast.Should().NotBeNull();

        foreach (var stmt in result.Ast!.Body)
        {
            if (stmt is FunctionDef fd && fd.Name == functionName)
            {
                return fd.Body.ToList();
            }
        }

        throw new InvalidOperationException($"Function '{functionName}' not found in source");
    }

    [Fact]
    public void AnalyzeScope_ReadFromOuter_DetectsRead()
    {
        // x is declared outside selected statements, but read inside
        var source = @"
def target():
    x: int = 10
    y: int = x + 1
    print(y)

def main():
    pass
";
        var body = GetFunctionBody(source);

        // Select the second statement: y = x + 1
        var selected = new System.Collections.Generic.List<Statement> { body[1] };

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.ReadsFromOuterScope.Should().Contain("x");
        scopeInfo.DeclaredInScope.Should().Contain("y");
    }

    [Fact]
    public void AnalyzeScope_WriteToOuter_DetectsWrite()
    {
        // y is declared in selection and used after it
        var source = @"
def target():
    y: int = 42
    print(y)

def main():
    pass
";
        var body = GetFunctionBody(source);

        // Select the first statement: y = 42
        var selected = new System.Collections.Generic.List<Statement> { body[0] };

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.DeclaredInScope.Should().Contain("y");
        scopeInfo.WritesToOuterScope.Should().Contain("y");
    }

    [Fact]
    public void AnalyzeScope_ContainsReturn_FlagsReturn()
    {
        var source = @"
def target() -> int:
    x: int = 42
    return x

def main():
    pass
";
        var body = GetFunctionBody(source);

        // Select the return statement
        var selected = new System.Collections.Generic.List<Statement> { body[1] };

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.ContainsReturn.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeScope_DeclaredInScope_DetectsDeclaration()
    {
        var source = @"
def target():
    x: int = 10
    y: int = 20
    print(x)
    print(y)

def main():
    pass
";
        var body = GetFunctionBody(source);

        // Select both variable declarations
        var selected = new System.Collections.Generic.List<Statement> { body[0], body[1] };

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.DeclaredInScope.Should().Contain("x");
        scopeInfo.DeclaredInScope.Should().Contain("y");
    }

    [Fact]
    public void AnalyzeScope_NoReturn_DoesNotFlagReturn()
    {
        var source = @"
def target():
    x: int = 10
    print(x)

def main():
    pass
";
        var body = GetFunctionBody(source);

        var selected = new System.Collections.Generic.List<Statement> { body[0] };

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.ContainsReturn.Should().BeFalse();
        scopeInfo.ContainsBreak.Should().BeFalse();
        scopeInfo.ContainsContinue.Should().BeFalse();
        scopeInfo.ContainsYield.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeScope_EmptySelection_ReturnsEmptyScopeInfo()
    {
        var source = @"
def target():
    x: int = 10

def main():
    pass
";
        var body = GetFunctionBody(source);
        var selected = new System.Collections.Generic.List<Statement>();

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.ReadsFromOuterScope.Should().BeEmpty();
        scopeInfo.WritesToOuterScope.Should().BeEmpty();
        scopeInfo.DeclaredInScope.Should().BeEmpty();
        scopeInfo.ContainsReturn.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeScope_ContainsBreak_FlagsBreak()
    {
        var source = @"
def target():
    for i in range(10):
        if i == 5:
            break
        print(i)

def main():
    pass
";
        var body = GetFunctionBody(source);

        // Select the for loop which contains a break
        var selected = new System.Collections.Generic.List<Statement> { body[0] };

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.ContainsBreak.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeScope_ContainsContinue_FlagsContinue()
    {
        var source = @"
def target():
    for i in range(10):
        if i == 5:
            continue
        print(i)

def main():
    pass
";
        var body = GetFunctionBody(source);

        // Select the for loop which contains a continue
        var selected = new System.Collections.Generic.List<Statement> { body[0] };

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.ContainsContinue.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeScope_ContainsYield_FlagsYield()
    {
        var source = @"
def target():
    yield 42

def main():
    pass
";
        var body = GetFunctionBody(source);

        // Select the yield statement
        var selected = new System.Collections.Generic.List<Statement> { body[0] };

        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selected, body, null);

        scopeInfo.ContainsYield.Should().BeTrue();
    }
}
