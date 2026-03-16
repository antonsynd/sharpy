using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests.Refactoring;

public class ImplementInterfaceProviderTests
{
    private readonly CompilerApi _api = new();
    private static readonly DocumentUri TestUri = DocumentUri.From("file:///test.spy");

    private async Task<IReadOnlyList<CodeAction>> GetActionsAsync(
        ICodeActionProvider provider,
        string source,
        LspRange? range = null)
    {
        var analysis = _api.Analyze(source, CancellationToken.None);
        var context = new CodeActionProviderContext(
            TestUri,
            range ?? new LspRange(new Position(0, 0), new Position(0, 0)),
            new Container<Diagnostic>(),
            analysis,
            source);
        return await provider.GetCodeActionsAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task ImplementInterface_MissingMethod_ReturnsImplementAction()
    {
        // When a class doesn't implement a required interface method, the compiler
        // emits SPY0320 but still provides the SymbolTable with interface data,
        // so the provider can offer to implement the missing methods.
        var source = @"interface Drawable:
    def draw(self) -> None:
        ...

class Circle(Drawable):
    def __init__(self):
        pass

def main():
    c: Circle = Circle()
    print(c)";

        var provider = new ImplementInterfaceProvider();

        // Cursor inside the ClassDef body
        var range = new LspRange(new Position(5, 4), new Position(5, 4));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().ContainSingle();
        actions[0].Title.Should().Contain("Implement interface 'Drawable'");
    }

    [Fact]
    public async Task ImplementInterface_FullyImplemented_ReturnsNoAction()
    {
        var source = @"interface Drawable:
    def draw(self) -> None:
        ...

class Circle(Drawable):
    def draw(self) -> None:
        print('drawing')

def main():
    c: Circle = Circle()
    c.draw()";

        var provider = new ImplementInterfaceProvider();

        // Cursor on the ClassDef line
        var range = new LspRange(new Position(4, 0), new Position(4, 0));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImplementInterface_NotOnClass_ReturnsNoAction()
    {
        var source = @"interface Drawable:
    def draw(self) -> None:
        ...

class Circle(Drawable):
    def draw(self) -> None:
        print('drawing')

def main():
    c: Circle = Circle()
    c.draw()";

        var provider = new ImplementInterfaceProvider();

        // Cursor on the main function, not on a class
        var range = new LspRange(new Position(9, 0), new Position(9, 0));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImplementInterface_NullAnalysis_ReturnsNoAction()
    {
        var provider = new ImplementInterfaceProvider();
        var context = new CodeActionProviderContext(
            TestUri,
            new LspRange(new Position(0, 0), new Position(0, 0)),
            new Container<Diagnostic>(),
            null,
            null);
        var actions = await provider.GetCodeActionsAsync(context, CancellationToken.None);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImplementInterface_ClassWithNoInterfaces_ReturnsNoAction()
    {
        var source = @"class Circle:
    pass

def main():
    pass";

        var provider = new ImplementInterfaceProvider();

        var range = new LspRange(new Position(0, 0), new Position(0, 0));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ImplementInterface_MissingMethod_ReturnsStubWithNotImplementedError()
    {
        // The compiler emits SPY0320 for missing interface methods but still provides
        // the SymbolTable, allowing the provider to offer implementation stubs.
        var source = @"interface Greeter:
    def greet(self, name: str) -> str:
        ...

class EnglishGreeter(Greeter):
    def __init__(self):
        pass

def main():
    g: EnglishGreeter = EnglishGreeter()
    print(g)";

        var provider = new ImplementInterfaceProvider();

        // Cursor on the class name "EnglishGreeter"
        var range = new LspRange(new Position(4, 6), new Position(4, 6));
        var actions = await GetActionsAsync(provider, source, range);

        actions.Should().ContainSingle();
        actions[0].Title.Should().Contain("Implement interface 'Greeter'");
    }

    [Fact]
    public async Task ImplementInterface_MultipleInterfacesMissing_ReturnsActions()
    {
        var source = @"interface Readable:
    def read(self) -> str:
        ...

interface Writable:
    def write(self, data: str) -> None:
        ...

class FileStream(Readable, Writable):
    def __init__(self):
        pass

def main():
    f: FileStream = FileStream()
    print(f)";

        var provider = new ImplementInterfaceProvider();

        // Cursor inside the class body (inside __init__)
        var range = new LspRange(new Position(9, 8), new Position(9, 8));
        var actions = await GetActionsAsync(provider, source, range);

        // Should offer individual interface actions plus "implement all"
        actions.Should().HaveCountGreaterThanOrEqualTo(2);
        actions.Should().Contain(a => a.Title.Contains("Implement interface 'Readable'"));
        actions.Should().Contain(a => a.Title.Contains("Implement interface 'Writable'"));
    }

    [Fact]
    public async Task ImplementInterface_PartiallyImplemented_ReturnsActionForMissing()
    {
        var source = @"interface Serializable:
    def serialize(self) -> str:
        ...
    def deserialize(self, data: str) -> None:
        ...

class JsonData(Serializable):
    def serialize(self) -> str:
        return '{}'

def main():
    pass";

        var provider = new ImplementInterfaceProvider();

        // Cursor on the class name "JsonData"
        var range = new LspRange(new Position(6, 6), new Position(6, 6));
        var actions = await GetActionsAsync(provider, source, range);

        // Should offer to implement the missing 'deserialize' method
        actions.Should().ContainSingle();
        actions[0].Title.Should().Contain("Implement interface 'Serializable'");
    }

    [Fact]
    public async Task ImplementInterface_CursorInsideClassBody_ReturnsNoActionForFullyImplemented()
    {
        var source = @"interface Sizeable:
    def size(self) -> int:
        ...

class MyList(Sizeable):
    def size(self) -> int:
        return 0

def main():
    pass";

        var provider = new ImplementInterfaceProvider();

        // Cursor inside the method body (should still find the containing class)
        var range = new LspRange(new Position(5, 8), new Position(5, 8));
        var actions = await GetActionsAsync(provider, source, range);

        // Fully implemented, so no actions
        actions.Should().BeEmpty();
    }

    #region Positive-path stub generation tests

    [Fact]
    public void FormatMethodStub_SimpleNoParams_ProducesCorrectStub()
    {
        var method = new FunctionSymbol
        {
            Name = "do_work",
            Parameters = new SCG.List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown }
            },
            ReturnType = new VoidType()
        };

        var result = ImplementInterfaceProvider.FormatMethodStub(method, 1);

        result.Should().Contain("def do_work(self):");
        result.Should().Contain("raise NotImplementedError()");
        result.Should().NotContain("->");
    }

    [Fact]
    public void FormatMethodStub_WithParameters_IncludesTypedParams()
    {
        var method = new FunctionSymbol
        {
            Name = "greet",
            Parameters = new SCG.List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown },
                new() { Name = "name", Type = (BuiltinType)SemanticType.Str }
            },
            ReturnType = new VoidType()
        };

        var result = ImplementInterfaceProvider.FormatMethodStub(method, 1);

        result.Should().Contain("def greet(self, name: str):");
        result.Should().Contain("raise NotImplementedError()");
    }

    [Fact]
    public void FormatMethodStub_WithReturnType_IncludesArrow()
    {
        var method = new FunctionSymbol
        {
            Name = "get_count",
            Parameters = new SCG.List<ParameterSymbol>
            {
                new() { Name = "self", Type = SemanticType.Unknown }
            },
            ReturnType = (BuiltinType)SemanticType.Int
        };

        var result = ImplementInterfaceProvider.FormatMethodStub(method, 1);

        result.Should().Contain("def get_count(self) -> int:");
        result.Should().Contain("raise NotImplementedError()");
    }

    [Fact]
    public void FormatMethodStub_NoSelfParam_AddsImplicitSelf()
    {
        var method = new FunctionSymbol
        {
            Name = "compute",
            Parameters = new SCG.List<ParameterSymbol>(),
            ReturnType = (BuiltinType)SemanticType.Float
        };

        var result = ImplementInterfaceProvider.FormatMethodStub(method, 1);

        result.Should().Contain("def compute(self) -> float:");
    }

    [Fact]
    public void FormatPropertyDef_ReadOnly_ProducesGetterOnly()
    {
        var result = SharpySourceGenerator.FormatPropertyDef(
            "count", (BuiltinType)SemanticType.Int,
            hasGetter: true, hasSetter: false, indentLevel: 1);

        result.Should().Contain("@property");
        result.Should().Contain("def count(self) -> int:");
        result.Should().Contain("raise NotImplementedError()");
        result.Should().NotContain("@count.setter");
    }

    [Fact]
    public void FormatPropertyDef_ReadWrite_ProducesGetterAndSetter()
    {
        var result = SharpySourceGenerator.FormatPropertyDef(
            "name", (BuiltinType)SemanticType.Str,
            hasGetter: true, hasSetter: true, indentLevel: 1);

        result.Should().Contain("@property");
        result.Should().Contain("def name(self) -> str:");
        result.Should().Contain("@name.setter");
        result.Should().Contain("def name(self, value: str):");
    }

    [Fact]
    public void GenerateStubs_MethodsAndProperties_ProducesCorrectOutput()
    {
        var methods = new SCG.List<FunctionSymbol>
        {
            new()
            {
                Name = "process",
                Parameters = new SCG.List<ParameterSymbol>
                {
                    new() { Name = "self", Type = SemanticType.Unknown },
                    new() { Name = "data", Type = (BuiltinType)SemanticType.Str }
                },
                ReturnType = (BuiltinType)SemanticType.Bool
            }
        };

        var properties = new SCG.List<PropertySymbol>
        {
            new()
            {
                Name = "status",
                Type = (BuiltinType)SemanticType.Str,
                HasGetter = true,
                HasSetter = false
            }
        };

        var result = ImplementInterfaceProvider.GenerateStubs(methods, properties, 1);

        // Properties come first (convention)
        var propIdx = result.IndexOf("@property", StringComparison.Ordinal);
        var methodIdx = result.IndexOf("def process", StringComparison.Ordinal);
        propIdx.Should().BeLessThan(methodIdx);

        result.Should().Contain("def status(self) -> str:");
        result.Should().Contain("def process(self, data: str) -> bool:");
    }

    [Fact]
    public void GenerateStubs_EmptyLists_ReturnsEmptyString()
    {
        var result = ImplementInterfaceProvider.GenerateStubs(
            new SCG.List<FunctionSymbol>(),
            new SCG.List<PropertySymbol>(),
            1);

        result.Should().BeEmpty();
    }

    // Stub generation logic is also tested above via direct invocation of the
    // internal static methods (FormatMethodStub, FormatPropertyDef, GenerateStubs).

    #endregion
}
