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

    [Fact(Skip = "Module resolution now works via WithRuntimeReferences (#1090), but a .spy class overriding the CLR-abstract SourceGenerator.generate emits no `override` — CS0534, tracked in #1122")]
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

        helper.WithRootNamespace("GenClassTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact(Skip = "Blocked by #1122 (CS0534: .spy override of CLR-abstract SourceGenerator.generate). The #1090 reflection-type resolution works; this exercises it once #1122 lands.")]
    public void GeneratorClass_ReadingReflectionTypes_Compiles()
    {
        // Exercises the Sharpy.Generators reflection types (ClassInfo/MethodInfo/ParameterInfo)
        // through codegen — the Mode-A types that #1090 fixed — from a .spy-authored generator.
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput, ClassInfo, MethodInfo

class DescribeGen(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        target: ClassInfo = context.target_class
        first: MethodInfo = target.methods[0]
        return GeneratorOutput('// ' + first.name)
");

        helper.AddSourceFile("main.spy", @"
def main():
    print('hello')
");

        helper.WithRootNamespace("GenReflectionTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
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
