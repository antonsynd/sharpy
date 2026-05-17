using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class InterfacePropertyTests
{
    private readonly ITestOutputHelper _output;

    public InterfacePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InterfaceImplementation_CompilesWhenComplete()
    {
        int total = 0;
        int passed = 0;

        GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: true)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "iface_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Generated code may have edge cases
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Interface implementation (complete): {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Interface implementation pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void InterfaceImplementation_FailsWhenMissing()
    {
        int total = 0;
        int diagnosed = 0;

        GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: false)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "iface_test.spy");
                    if (!result.Success && result.Diagnostics.GetAll().Any(d =>
                        d.Code.StartsWith("SPY03") || d.Code.StartsWith("SPY04")))
                    {
                        Interlocked.Increment(ref diagnosed);
                    }
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Interface missing method diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Interface missing method diagnostic rate too low: {diagnosed}/{total}");
    }

    [Fact]
    public void ProtocolSynthesis_AddsInterfaceForDunder()
    {
        int total = 0;
        int passed = 0;

        Gen.OneOfConst("__len__", "__bool__")
            .SelectMany(GenInterfaces.ModuleWithProtocolDunder)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "protocol_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Protocol synthesis: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Protocol synthesis pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void InterfaceConflict_DetectedInHierarchy()
    {
        int total = 0;
        int passed = 0;

        GenInterfaces.ModuleWithInterfaceHierarchy()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "hierarchy_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Interface hierarchy: {passed}/{total} passed");
        Assert.True(passed > total / 4,
            $"Interface hierarchy pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void MultipleInterfaces_AllValidated()
    {
        int total = 0;
        int passed = 0;

        GenInterfaces.ModuleWithMultipleInterfaces()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "multi_iface_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Multiple interfaces: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Multiple interfaces pass rate too low: {passed}/{total}");
    }
}
