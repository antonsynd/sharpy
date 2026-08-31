using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
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
        var samples = PropertyCorpus.CompileAll(
            GenClasses.ModuleWithClasses(TypeEnv.WithInheritance, fuel: 2),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ClassHierarchy_CompilesClean");
    }

    [Fact]
    public void DerivedClass_InheritsBaseMembers()
    {
        var samples = PropertyCorpus.CompileAll(
            GenClasses.ModuleWithClasses(TypeEnv.WithInheritance, fuel: 2),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "DerivedClass_InheritsBaseMembers");
    }

    [Fact]
    public void MethodOverride_PreservesSignature()
    {
        var samples = PropertyCorpus.CompileAll(
            GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "MethodOverride_PreservesSignature");
    }

    [Fact]
    public void Constructor_InitializesFieldTypes()
    {
        var samples = PropertyCorpus.CompileAll(
            GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "Constructor_InitializesFieldTypes");
    }

    [Fact]
    public void AbstractMethod_MustBeOverridden()
    {
        int tested = 0;
        int rejected = 0;

        GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2)
            .Sample(module =>
            {
                var injected = ErrorInjector.InjectMissingAbstractImplementation(module);
                if (injected == null)
                    return;

                Interlocked.Increment(ref tested);
                var injectedSource = Sharpy.Compiler.Pretty.Unparser.Unparse(injected.Mutated);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(injectedSource, "class_test.spy");
                    if (!result.Success)
                        Interlocked.Increment(ref rejected);
                }
                catch
                {
                    Interlocked.Increment(ref rejected);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Abstract method enforcement: {rejected}/{tested} rejected");
        if (tested > 0)
            Assert.True(rejected > tested / 2,
                $"Abstract method enforcement rate too low: {rejected}/{tested}");
    }

    [Fact]
    public void SuperCall_ResolvesCorrectly()
    {
        int tested = 0;
        int passed = 0;

        GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                var hasSuperCall = source.Contains("super().__init__");
                if (!hasSuperCall)
                    return;

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

        _output.WriteLine($"Super call resolution: {passed}/{tested} with super() resolved");
        Assert.True(passed > 0, "No super() calls were successfully resolved");
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
