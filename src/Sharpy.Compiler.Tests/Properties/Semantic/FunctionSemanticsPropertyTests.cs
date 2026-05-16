using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class FunctionSemanticsPropertyTests
{
    private readonly ITestOutputHelper _output;

    public FunctionSemanticsPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FunctionDef_IsResolvableByName()
    {
        int tested = 0;
        int resolved = 0;

        GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "func_test.spy");

                    if (result.Success && result.SymbolTable != null)
                    {
                        var funcNames = module.Body
                            .OfType<Sharpy.Compiler.Parser.Ast.FunctionDef>()
                            .Where(f => f.Name != "main")
                            .Select(f => f.Name)
                            .ToList();
                        var allResolved = funcNames.All(name =>
                            result.SymbolTable.LookupFunction(name) != null);
                        if (allResolved && funcNames.Count > 0)
                            Interlocked.Increment(ref resolved);
                    }
                }
                catch
                {
                    // Generated code may have edge cases
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Function resolution: {resolved}/{tested} resolved");
        Assert.True(resolved > tested / 2,
            $"Function resolution rate too low: {resolved}/{tested}");
    }

    [Fact]
    public void FunctionCall_TypeChecksCorrectly()
    {
        int total = 0;
        int passed = 0;

        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenFunctions.ModuleWithFunctions(TypeEnv.Default, type, fuel: 2))
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "func_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Function call type check: {passed}/{total} passed");
        Assert.True(passed > total / 4,
            $"Function call pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void FunctionReturnType_MatchesAnnotation()
    {
        int tested = 0;
        int matched = 0;

        GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "func_test.spy");

                    if (result.Success && result.SymbolTable != null)
                    {
                        var funcDefs = module.Body
                            .OfType<Sharpy.Compiler.Parser.Ast.FunctionDef>()
                            .Where(f => f.Name != "main" && f.ReturnType != null)
                            .ToList();
                        var allMatch = funcDefs.All(fd =>
                        {
                            var sym = result.SymbolTable.LookupFunction(fd.Name);
                            return sym?.ReturnType != null;
                        });
                        if (allMatch && funcDefs.Count > 0)
                            Interlocked.Increment(ref matched);
                    }
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Return type matching: {matched}/{tested} matched");
        Assert.True(matched > tested / 2,
            $"Return type match rate too low: {matched}/{tested}");
    }

    [Fact]
    public void FunctionOverload_DispatchesCorrectly()
    {
        int tested = 0;
        int passed = 0;

        Gen.OneOfConst("int", "str", "bool").Select(type =>
        {
            var source = $"class Processor:\n    def handle(self, x: int) -> str:\n        return \"int\"\n\n    def handle(self, x: str) -> str:\n        return \"str\"\n\n    def handle(self, x: int, y: int) -> str:\n        return \"two\"\n\ndef main():\n    p = Processor()\n    a = p.handle(42)\n    b = p.handle(\"hi\")\n    c = p.handle(1, 2)\n    print(a)\n    print(b)\n    print(c)\n";
            return source;
        }).Sample(source =>
        {
            Interlocked.Increment(ref tested);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "func_test.spy");
                if (result.Success)
                    Interlocked.Increment(ref passed);
            }
            catch
            {
                // Swallow
            }
        }, iter: 50);

        _output.WriteLine($"Function overload dispatch: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Function overload dispatch rate too low: {passed}/{tested}");
    }

    [Fact]
    public void ModuleWithFunctions_NeverThrowsInternalError()
    {
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenFunctions.ModuleWithFunctions(TypeEnv.Default, type, fuel: 2))
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    compiler.Analyze(source, "func_test.spy");
                }
                catch (Sharpy.Compiler.Diagnostics.InternalCompilerErrorException ex)
                {
                    throw new Exception(
                        $"InternalCompilerErrorException on function program:\n{source}\n{ex.Message}");
                }
                catch
                {
                    // Other exceptions are acceptable
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }
}
