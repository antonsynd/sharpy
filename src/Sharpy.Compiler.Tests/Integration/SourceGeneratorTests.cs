using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Integration;

public class SourceGeneratorTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public SourceGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private ProjectCompilationHelper CreateHelper()
    {
        _helper = new ProjectCompilationHelper(_output);
        return _helper;
    }

    public void Dispose()
    {
        _helper?.Dispose();
    }

    [Fact]
    public void Compilation_WithNoGenerators_SucceedsNormally()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("main.spy", @"
def main():
    x: int = 42
    print(x)
");

        helper.WithRootNamespace("NoGenTest").WithEntryPoint("main.spy").CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact(Skip = "Requires ModuleRegistry with Sharpy.Core loaded — available in CLI, not unit test harness")]
    public void GeneratorClass_Compiles_WithoutErrors()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class MyGen(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput('')
");

        helper.AddSourceFile("main.spy", @"
def main():
    print('hello')
");

        helper.WithRootNamespace("GenClassTest").WithEntryPoint("main.spy").CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void DiagnosticCodes_GeneratorCodesExist()
    {
        Assert.Equal("SPY0550", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorExecutionError);
        Assert.Equal("SPY0551", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorTimeout);
        Assert.Equal("SPY0552", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorInvalidSource);
        Assert.Equal("SPY0553", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorCycleDetected);
        Assert.Equal("SPY0554", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorEmptyOutput);
    }
}
