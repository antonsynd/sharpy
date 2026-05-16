using CsCheck;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class ImportResolutionPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ImportResolutionPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ImportedFunction_IsCallable()
    {
        int tested = 0;
        int passed = 0;

        GenImports.ImportingModule(TypeEnv.Default, fuel: 1)
            .Sample(pair =>
            {
                Interlocked.Increment(ref tested);

                using var helper = new ProjectCompilationHelper(_output);
                helper.WithRootNamespace("Test")
                    .AddSourceFile("main.spy", pair.MainSource)
                    .AddSourceFile("lib.spy", pair.LibSource)
                    .CreateProjectFile();

                var result = helper.Compile();
                if (result.Success)
                    Interlocked.Increment(ref passed);
            }, iter: 20);

        _output.WriteLine($"Imported function callable: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Import resolution rate too low: {passed}/{tested}");
    }

    [Fact]
    public void MultiFileProgram_CompilesClean()
    {
        int tested = 0;
        int passed = 0;

        GenImports.MultiFileProgram(TypeEnv.Default, fuel: 1)
            .Sample(pair =>
            {
                Interlocked.Increment(ref tested);

                using var helper = new ProjectCompilationHelper(_output);
                helper.WithRootNamespace("Test")
                    .AddSourceFile("main.spy", pair.Main)
                    .AddSourceFile("lib.spy", pair.Lib)
                    .CreateProjectFile();

                var result = helper.Compile();
                if (result.Success)
                    Interlocked.Increment(ref passed);
            }, iter: 20);

        _output.WriteLine($"Multi-file compilation: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Multi-file pass rate too low: {passed}/{tested}");
    }

    [Fact]
    public void CircularImport_IsRejected()
    {
        int tested = 0;
        int rejected = 0;

        GenImports.CircularImportPair(TypeEnv.Default, fuel: 1)
            .Sample(pair =>
            {
                Interlocked.Increment(ref tested);

                using var helper = new ProjectCompilationHelper(_output);
                helper.WithRootNamespace("Test")
                    .AddSourceFile("file_a.spy", pair.FileA)
                    .AddSourceFile("file_b.spy", pair.FileB)
                    .AddSourceFile("main.spy", "from file_a import helper_a\n\ndef main():\n    print(helper_a())\n")
                    .CreateProjectFile();

                var result = helper.Compile();
                if (!result.Success)
                    Interlocked.Increment(ref rejected);
            }, iter: 20);

        _output.WriteLine($"Circular import rejection: {rejected}/{tested} rejected");
        Assert.True(rejected > tested / 2,
            $"Circular import rejection rate too low: {rejected}/{tested}");
    }

    [Fact]
    public void UnusedImport_ProducesWarning()
    {
        int tested = 0;
        int warned = 0;

        GenImports.UnusedImportProgram(TypeEnv.Default, fuel: 1)
            .Sample(pair =>
            {
                Interlocked.Increment(ref tested);

                using var helper = new ProjectCompilationHelper(_output);
                helper.WithRootNamespace("Test")
                    .AddSourceFile("main.spy", pair.Main)
                    .AddSourceFile("lib.spy", pair.Lib)
                    .CreateProjectFile();

                var result = helper.Compile();
                var warnings = result.Diagnostics.GetAll()
                    .Where(d => d.Code == "SPY0452")
                    .ToList();
                if (warnings.Count > 0)
                    Interlocked.Increment(ref warned);
            }, iter: 20);

        _output.WriteLine($"Unused import warning: {warned}/{tested} warned");
        Assert.True(warned > tested / 2,
            $"Unused import warning rate too low: {warned}/{tested}");
    }
}
