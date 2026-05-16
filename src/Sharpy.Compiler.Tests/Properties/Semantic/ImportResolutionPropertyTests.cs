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
    public void ImportedClass_IsInstantiable()
    {
        int tested = 0;
        int passed = 0;

        Gen.OneOfConst("int", "str", "bool").Select(fieldType =>
        {
            var lib = $"class Widget:\n    value: {fieldType}\n\n    def __init__(self, value: {fieldType}):\n        self.value = value\n";
            var main = $"from lib import Widget\n\ndef main():\n    w = Widget({GenImports_DefaultLiteral(fieldType)})\n    print(w.value)\n";
            return (Main: main, Lib: lib);
        }).Sample(pair =>
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

        _output.WriteLine($"Imported class instantiation: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Imported class instantiation rate too low: {passed}/{tested}");
    }

    [Fact]
    public void NameCollision_IsDetected()
    {
        int tested = 0;
        int detected = 0;

        GenImports.NameCollisionPair(TypeEnv.Default, fuel: 1)
            .Sample(quad =>
            {
                Interlocked.Increment(ref tested);

                using var helper = new ProjectCompilationHelper(_output);
                helper.WithRootNamespace("Test")
                    .AddSourceFile("main.spy", quad.Main)
                    .AddSourceFile("lib1.spy", quad.Lib1)
                    .AddSourceFile("lib2.spy", quad.Lib2)
                    .CreateProjectFile();

                var result = helper.Compile();
                if (!result.Success || result.Diagnostics.GetAll().Any(
                    d => d.Code.StartsWith("SPY0")))
                    Interlocked.Increment(ref detected);
            }, iter: 20);

        _output.WriteLine($"Name collision detection: {detected}/{tested} detected");
        Assert.True(detected > tested / 2,
            $"Name collision detection rate too low: {detected}/{tested}");
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
        Assert.Equal(tested, rejected);
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

    private static string GenImports_DefaultLiteral(string type) => type switch
    {
        "int" => "42",
        "str" => "\"hello\"",
        "bool" => "True",
        _ => "0"
    };
}
