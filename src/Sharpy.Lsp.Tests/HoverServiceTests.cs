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

        // "    def method(self) -> int:" — 'self' starts at col 16
        // Cols: "    def method(" = 15 chars, 'self' at col 16
        var hover = _hoverService.GetHoverMarkdown(result, 2, 16);

        hover.Should().NotBeNull();
        hover.Should().Contain("self");
        hover.Should().Contain("MyClass");
    }

    [Fact]
    public void GetHoverMarkdown_OverSelfInMethodBody_ShowsClassName()
    {
        var source = "class Foo:\n    x: int = 0\n    def get_x(self) -> int:\n        return self.x\ndef main():\n    pass";
        var result = _api.Analyze(source);

        // Line 4: "        return self.x"
        // Cols:    123456789012345678
        // 'self' starts at col 16
        var hover = _hoverService.GetHoverMarkdown(result, 4, 16);

        hover.Should().NotBeNull();
        hover.Should().Contain("self");
        hover.Should().Contain("Foo");
    }

    // --- #472: With-statement as-variable hover ---

    [Fact]
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

    [Fact]
    public void GetHoverMarkdown_OverWithWithoutAs_ReturnsContextExpressionType()
    {
        var source = "class Conn:\n    def __enter__(self) -> Conn:\n        return self\n    def __exit__(self):\n        pass\ndef main():\n    with Conn():\n        pass";
        var result = _api.Analyze(source);

        // Line 7: "    with Conn():"
        // Hover over 'Conn()' at col 10
        var hover = _hoverService.GetHoverMarkdown(result, 7, 10);

        hover.Should().NotBeNull();
        hover.Should().Contain("Conn");
    }

    // --- #471: Async with TaskType unwrapping hover ---

    [Fact]
    public void GetHoverMarkdown_OverAsyncWithAsVariable_ReturnsUnwrappedType()
    {
        var source = "class AsyncConn:\n    async def __aenter__(self) -> AsyncConn:\n        return self\n    async def __aexit__(self):\n        pass\nasync def main():\n    async with AsyncConn() as conn:\n        pass";
        var result = _api.Analyze(source);

        // Line 7: "    async with AsyncConn() as conn:"
        // Cols:    1234567890123456789012345678901234
        // 'conn' starts at col 31
        var hover = _hoverService.GetHoverMarkdown(result, 7, 31);

        hover.Should().NotBeNull();
        // Should show AsyncConn (unwrapped), not Task[AsyncConn]
        hover.Should().Contain("AsyncConn");
        hover.Should().NotContain("Task");
    }

    // --- #511: Type alias hover ---

    [Fact]
    public void GetHoverMarkdown_OverModuleLevelTypeAlias_ReturnsAliasInfo()
    {
        var source = "type MyInt = int\ndef main():\n    pass";
        var result = _api.Analyze(source);

        // Line 1: "type MyInt = int"
        // 'MyInt' starts at col 6
        var hover = _hoverService.GetHoverMarkdown(result, 1, 6);

        hover.Should().NotBeNull();
        hover.Should().Contain("type alias");
        hover.Should().Contain("MyInt");
    }

    [Fact]
    public void GetHoverMarkdown_OverClassScopedTypeAlias_ReturnsAliasInfo()
    {
        var source = "class Foo:\n    type Alias = int\n    x: Alias = 0\ndef main():\n    pass";
        var result = _api.Analyze(source);

        // Line 2: "    type Alias = int"
        // 'Alias' starts at col 10
        var hover = _hoverService.GetHoverMarkdown(result, 2, 10);

        hover.Should().NotBeNull();
        hover.Should().Contain("type alias");
        hover.Should().Contain("Alias");
        hover.Should().Contain("int");
    }

    [Fact]
    public void GetHoverMarkdown_OverFunctionScopedTypeAlias_ReturnsAliasInfo()
    {
        var source = "def main():\n    type LocalAlias = str\n    x: LocalAlias = \"hi\"";
        var result = _api.Analyze(source);

        // Line 2: "    type LocalAlias = str"
        // 'LocalAlias' starts at col 10
        var hover = _hoverService.GetHoverMarkdown(result, 2, 10);

        hover.Should().NotBeNull();
        hover.Should().Contain("type alias");
        hover.Should().Contain("LocalAlias");
        hover.Should().Contain("str");
    }
}
