using FluentAssertions;
using Sharpy.Compiler;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for <see cref="HoverService"/> — the extracted hover resolution logic
/// used by both the LSP server and the CLI.
/// </summary>
public class HoverServiceTests
{
    private readonly CompilerApi _api = new();
    private readonly HoverService _hoverService;

    public HoverServiceTests()
    {
        _hoverService = new HoverService(_api);
    }

    [Fact]
    public void GetHoverMarkdown_OverVariable_ReturnsTypeInfo()
    {
        var source = "x: int = 42\ndef main():\n    print(x)";
        var result = _api.Analyze(source);

        var hover = _hoverService.GetHoverMarkdown(result, 1, 1);

        hover.Should().NotBeNull();
        hover.Should().Contain("x");
        hover.Should().Contain("int");
    }

    [Fact]
    public void GetHoverMarkdown_OverFunction_ReturnsSignature()
    {
        var source = "def greet(name: str) -> str:\n    return \"hi \" + name\ndef main():\n    greet(\"world\")";
        var result = _api.Analyze(source);

        // Hover over 'greet' at line 1, col 5
        var hover = _hoverService.GetHoverMarkdown(result, 1, 5);

        hover.Should().NotBeNull();
        hover.Should().Contain("def greet");
        hover.Should().Contain("name: str");
        hover.Should().Contain("-> str");
    }

    [Fact]
    public void GetHoverMarkdown_OverMethod_ReturnsMethodSignature()
    {
        var source = "class Foo:\n    def bar(self) -> int:\n        return 42\ndef main():\n    f = Foo()\n    f.bar()";
        var result = _api.Analyze(source);

        // Hover over 'bar' in the definition (line 2, col 9)
        var hover = _hoverService.GetHoverMarkdown(result, 2, 9);

        hover.Should().NotBeNull();
        hover.Should().Contain("bar");
        hover.Should().Contain("int");
    }

    [Fact]
    public void GetHoverMarkdown_OutOfRange_ReturnsNull()
    {
        var source = "x: int = 42\ndef main():\n    pass";
        var result = _api.Analyze(source);

        var hover = _hoverService.GetHoverMarkdown(result, 999, 1);

        hover.Should().BeNull();
    }

    [Fact]
    public void GetHoverMarkdown_NullAst_ReturnsNull()
    {
        // Create a result with no AST (simulates parse failure)
        var result = new SemanticResult { Success = false, Ast = null };

        var hover = _hoverService.GetHoverMarkdown(result, 1, 1);

        hover.Should().BeNull();
    }

    [Fact]
    public void GetHoverMarkdown_OverClass_ReturnsClassInfo()
    {
        var source = "class MyClass:\n    x: int = 0\ndef main():\n    c = MyClass()";
        var result = _api.Analyze(source);

        // Hover over 'MyClass' at line 1, col 7
        var hover = _hoverService.GetHoverMarkdown(result, 1, 7);

        hover.Should().NotBeNull();
        hover.Should().Contain("MyClass");
    }

    [Fact]
    public void GetHoverMarkdown_EmptyPosition_ReturnsNull()
    {
        var source = "x: int = 42\n\ndef main():\n    pass";
        var result = _api.Analyze(source);

        // Hover over empty line 2
        var hover = _hoverService.GetHoverMarkdown(result, 2, 1);

        hover.Should().BeNull();
    }

    // --- #474: Variable declaration name hover ---

    [Fact]
    public void GetHoverMarkdown_OverVariableNameAtDeclaration_ReturnsTypeInfo()
    {
        var source = "x: int = 42\ndef main():\n    pass";
        var result = _api.Analyze(source);

        // Hover over 'x' at line 1, col 1 (the declaration name)
        var hover = _hoverService.GetHoverMarkdown(result, 1, 1);

        hover.Should().NotBeNull();
        hover.Should().Contain("x");
        hover.Should().Contain("int");
    }

    [Fact]
    public void GetHoverMarkdown_OverVariableNameInferredType_ReturnsTypeInfo()
    {
        var source = "x = 42\ndef main():\n    pass";
        var result = _api.Analyze(source);

        // Hover over 'x' at line 1, col 1
        var hover = _hoverService.GetHoverMarkdown(result, 1, 1);

        hover.Should().NotBeNull();
        hover.Should().Contain("x");
        hover.Should().Contain("int");
    }

    // --- #473: Self parameter type display ---

    [Fact]
    public void GetHoverMarkdown_OverSelfParameter_ShowsClassName()
    {
        var source = "class MyClass:\n    def method(self) -> int:\n        return 42\ndef main():\n    pass";
        var result = _api.Analyze(source);

        // "    def method(self) -> int:" — 'self' starts at col 19
        // Cols: "    def method(" = 15 chars, 'self' starts at col 16
        var hover = _hoverService.GetHoverMarkdown(result, 2, 16);

        hover.Should().NotBeNull();
        hover.Should().Contain("self");
        hover.Should().Contain("MyClass");
    }

    // --- #472: With-statement as-variable hover ---

    [Fact(Skip = "TODO(#475): WithStatement hover uses SymbolTable.LookupVariable which can't find local-scope 'as' variables after analysis.")]
    public void GetHoverMarkdown_OverWithAsVariable_ReturnsTypeInfo()
    {
        var source = "class Conn:\n    def __enter__(self) -> Conn:\n        return self\n    def __exit__(self):\n        pass\ndef main():\n    with Conn() as c:\n        pass";
        var result = _api.Analyze(source);

        // Line 7: "    with Conn() as c:"
        // Cols:    12345678901234567890
        // 'c' starts at col 20
        var hover = _hoverService.GetHoverMarkdown(result, 7, 20);

        hover.Should().NotBeNull();
        hover.Should().Contain("Conn");
    }

    [Fact]
    public void GetHoverMarkdown_OverWithContextExpression_ReturnsTypeInfo()
    {
        var source = "class Conn:\n    def __enter__(self) -> Conn:\n        return self\n    def __exit__(self):\n        pass\ndef main():\n    with Conn() as c:\n        pass";
        var result = _api.Analyze(source);

        // Line 7: "    with Conn() as c:"
        // Hover over 'Conn()' call at col 10
        var hover = _hoverService.GetHoverMarkdown(result, 7, 10);

        hover.Should().NotBeNull();
        hover.Should().Contain("Conn");
    }
}
