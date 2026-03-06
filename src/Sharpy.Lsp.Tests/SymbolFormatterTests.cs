using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class SymbolFormatterTests
{
    [Fact]
    public void FormatFunction_NoParams_NoReturn()
    {
        var symbol = new FunctionSymbol { Name = "do_thing" };

        var result = SymbolFormatter.FormatSymbol(symbol);

        result.Should().Contain("def do_thing()");
        result.Should().StartWith("(function)");
    }

    [Fact]
    public void FormatType_Class()
    {
        var symbol = new TypeSymbol { Name = "MyClass", TypeKind = TypeKind.Class };

        var result = SymbolFormatter.FormatSymbol(symbol);

        result.Should().Contain("class MyClass");
        result.Should().StartWith("(class)");
    }

    [Fact]
    public void FormatType_Struct()
    {
        var symbol = new TypeSymbol { Name = "Point", TypeKind = TypeKind.Struct };

        var result = SymbolFormatter.FormatSymbol(symbol);

        result.Should().Contain("struct Point");
        result.Should().StartWith("(struct)");
    }

    [Fact]
    public void FormatType_Interface()
    {
        var symbol = new TypeSymbol { Name = "Drawable", TypeKind = TypeKind.Interface };

        var result = SymbolFormatter.FormatSymbol(symbol);

        result.Should().Contain("interface Drawable");
        result.Should().StartWith("(interface)");
    }

    [Fact]
    public void FormatModule_ShowsModuleName()
    {
        var symbol = new ModuleSymbol { Name = "math", FilePath = "/path/to/math.spy" };

        var result = SymbolFormatter.FormatSymbol(symbol);

        result.Should().Be("(module) math");
    }

    [Fact]
    public void FormatTypeInfo_ReturnsDisplayName()
    {
        var result = SymbolFormatter.FormatTypeInfo(BuiltinType.Str);

        result.Should().Be("str");
    }

    [Fact]
    public void FormatVariable_ViaAnalysis_ShowsType()
    {
        // Use the compiler to produce a real symbol with type set
        var api = new CompilerApi();
        var analysis = api.Analyze("x: int = 42\ndef main():\n    print(x)");
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable?.Lookup("x");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<VariableSymbol>();

        var result = SymbolFormatter.FormatSymbol(symbol!);

        result.Should().Contain("x");
        result.Should().Contain("int");
        result.Should().StartWith("(variable)");
    }

    [Fact]
    public void FormatFunction_ViaAnalysis_ShowsSignature()
    {
        var api = new CompilerApi();
        var analysis = api.Analyze("def add(a: int, b: int) -> int:\n    return a + b\ndef main():\n    pass");
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable?.Lookup("add");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<FunctionSymbol>();

        var result = SymbolFormatter.FormatSymbol(symbol!);

        result.Should().Contain("def add(a: int, b: int) -> int");
        result.Should().StartWith("(function)");
    }
}
