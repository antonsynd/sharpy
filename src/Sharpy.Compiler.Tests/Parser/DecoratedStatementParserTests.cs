using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests for statement-scoped <c>@suppress</c> via the additive DecoratedStatement wrapper
/// (#1024): an all-<c>@suppress</c> decorator set may prefix an expression statement or assignment
/// (call-site suppression); every other decorator on a non-definition target keeps its SPY0105.
/// </summary>
public partial class ParserTests
{
    #region Statement-scoped @suppress (#1024)

    [Fact]
    public void Suppress_OnExpressionStatement_WrapsInDecoratedStatement()
    {
        var module = Parse("@suppress(\"SPY0480\")\nfoo()");
        var decorated = module.Body[0].Should().BeOfType<DecoratedStatement>().Subject;
        decorated.Decorators.Should().ContainSingle().Which.Name.Should().Be("suppress");
        decorated.Statement.Should().BeOfType<ExpressionStatement>();
    }

    [Fact]
    public void Suppress_OnAssignment_WrapsInDecoratedStatement()
    {
        var module = Parse("@suppress(\"SPY0451\")\nx = compute()");
        var decorated = module.Body[0].Should().BeOfType<DecoratedStatement>().Subject;
        decorated.Statement.Should().BeOfType<Assignment>();
    }

    [Fact]
    public void NonSuppressDecorator_OnAssignment_StillErrors()
    {
        // @dataclass (not @suppress) on an assignment keeps today's SPY0105 parse error.
        var error = ParseExpectingError("@dataclass\nx = 5");
        error.Should().Contain("Decorators cannot be applied to assignments");
    }

    [Fact]
    public void Suppress_OnImport_WrapsInDecoratedStatement()
    {
        // Statement-scoped @suppress now covers plain imports (#1124): the import parses normally
        // and is wrapped in a DecoratedStatement so every module.Body scan can classify it via
        // Statement.UnwrapDecorated.
        var module = Parse("@suppress(\"SPY0452\")\nimport os");
        var decorated = module.Body[0].Should().BeOfType<DecoratedStatement>().Subject;
        decorated.Decorators.Should().ContainSingle().Which.Name.Should().Be("suppress");
        decorated.Statement.Should().BeOfType<ImportStatement>();
    }

    [Fact]
    public void Suppress_OnFromImport_WrapsInDecoratedStatement()
    {
        var module = Parse("@suppress(\"SPY0452\")\nfrom os import path");
        var decorated = module.Body[0].Should().BeOfType<DecoratedStatement>().Subject;
        decorated.Decorators.Should().ContainSingle().Which.Name.Should().Be("suppress");
        decorated.Statement.Should().BeOfType<FromImportStatement>();
    }

    [Fact]
    public void NonSuppressDecorator_OnImport_StillErrors()
    {
        // A non-@suppress decorator on an import keeps today's SPY0105 — imports have no CLR
        // attribute target, so anything but @suppress is meaningless.
        var error = ParseExpectingError("@lru_cache\nimport os");
        error.Should().Contain("Decorators can only be applied to");
    }

    [Fact]
    public void MixedDecorators_OnImport_StillErrors()
    {
        // If any decorator is not @suppress, the import keeps its SPY0105.
        var error = ParseExpectingError("@suppress(\"SPY0452\")\n@lru_cache\nimport os");
        error.Should().Contain("Decorators can only be applied to");
    }

    [Fact]
    public void MixedDecorators_OnStatement_StillError()
    {
        // If any decorator is not @suppress, the non-definition target keeps its SPY0105.
        var error = ParseExpectingError("@suppress(\"SPY0480\")\n@dataclass\nfoo()");
        error.Should().Contain("Decorators can only be applied to");
    }

    #endregion
}
