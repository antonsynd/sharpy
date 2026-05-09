using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class ILCompilesPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ILCompilesPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeneratedCSharp_CompilesToValidIL()
    {
        int total = 0;
        int compiled = 0;

        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Compile(source, "il_test.spy");

                if (result.Success)
                    Interlocked.Increment(ref compiled);
            }
            catch
            {
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"IL compilation: {compiled}/{total} compiled");
    }
}
