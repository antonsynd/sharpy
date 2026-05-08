using CsCheck;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic;

[Trait("Category", "Property")]
[Trait("Speed", "Slow")]
public class MetamorphicPropertyTests : IntegrationTestBase
{
    private static readonly IAstTransform[] Transforms =
    {
        new CommentInsertionTransform(),
        new PassInsertionTransform()
    };

    public MetamorphicPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CommentInsertion_PreservesOutput()
    {
        var transform = new CommentInsertionTransform();
        Gen.OneOfConst(SamplePrograms).Sample(source =>
        {
            var r1 = CompileAndExecute(source);
            if (!r1.Success)
                return;

            var transformed = transform.Apply(source);
            var r2 = CompileAndExecute(transformed);
            if (!r2.Success)
                return;

            if (r1.StandardOutput != r2.StandardOutput)
                throw new Exception(
                    $"{transform.Name}: output changed.\n" +
                    $"Original: {r1.StandardOutput.TrimEnd()}\n" +
                    $"Transformed: {r2.StandardOutput.TrimEnd()}");
        }, iter: 10);
    }

    [Fact]
    public void PassInsertion_PreservesOutput()
    {
        var transform = new PassInsertionTransform();
        Gen.OneOfConst(SamplePrograms).Sample(source =>
        {
            var r1 = CompileAndExecute(source);
            if (!r1.Success)
                return;

            var transformed = transform.Apply(source);
            var r2 = CompileAndExecute(transformed);
            if (!r2.Success)
                return;

            if (r1.StandardOutput != r2.StandardOutput)
                throw new Exception(
                    $"{transform.Name}: output changed.\n" +
                    $"Original: {r1.StandardOutput.TrimEnd()}\n" +
                    $"Transformed: {r2.StandardOutput.TrimEnd()}");
        }, iter: 10);
    }

    private static readonly string[] SamplePrograms =
    {
        "def main():\n    print(1 + 2)",
        "def main():\n    x: int = 42\n    print(x)",
        "def main():\n    print(\"hello\" + \" \" + \"world\")",
        "def main():\n    x: int = 10\n    if x > 5:\n        print(\"big\")\n    else:\n        print(\"small\")",
        "def main():\n    for i in range(5):\n        print(i)",
        "def add(a: int, b: int) -> int:\n    return a + b\ndef main():\n    print(add(3, 4))",
    };
}
