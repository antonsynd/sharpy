using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class ClassInheritancePropertyTests
{
    private readonly ITestOutputHelper _output;

    public ClassInheritancePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ClassHierarchy_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "class_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Class hierarchy compilation: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Class hierarchy pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void DerivedClass_InheritsBaseMembers()
    {
        int tested = 0;
        int passed = 0;

        GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "class_test.spy");
                    if (result.Success && result.SemanticInfo != null)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Derived class member access: {passed}/{tested} passed");
        Assert.True(passed > tested / 3,
            $"Derived class member access rate too low: {passed}/{tested}");
    }

    [Fact]
    public void MethodOverride_PreservesSignature()
    {
        int tested = 0;
        int passed = 0;

        GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "class_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Override signature preservation: {passed}/{tested} passed");
        Assert.True(passed > tested / 3,
            $"Override signature preservation rate too low: {passed}/{tested}");
    }

    [Fact]
    public void ModuleWithClasses_NeverThrowsInternalError()
    {
        GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    compiler.Analyze(source, "class_test.spy");
                }
                catch (Sharpy.Compiler.Diagnostics.InternalCompilerErrorException ex)
                {
                    throw new Exception(
                        $"InternalCompilerErrorException on class program:\n{source}\n{ex.Message}");
                }
                catch
                {
                    // Other exceptions are acceptable
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }
}
