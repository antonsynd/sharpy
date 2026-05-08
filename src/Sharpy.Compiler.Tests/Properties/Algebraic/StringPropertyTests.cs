using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

[Trait("Category", "Property")]
[Trait("Speed", "Slow")]
public class StringPropertyTests : AlgebraicTestBase
{
    public StringPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void StringConcat_HasIdentity()
    {
        Gen.OneOfConst("hello", "world", "test", "abc").Sample(s =>
        {
            var r1 = RunAndCapture($"def main():\n    print(\"{s}\" + \"\")");
            var r2 = RunAndCapture($"def main():\n    print(\"{s}\")");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"\"{s}\" + \"\" = {r1} but \"{s}\" = {r2}");
        }, iter: 25);
    }

    [Fact]
    public void StringConcat_LengthIsAdditive()
    {
        Gen.Select(
            Gen.OneOfConst("hello", "world", "x", ""),
            Gen.OneOfConst("foo", "bar", "y", "")
        ).Sample((a, b) =>
        {
            var r = RunAndCapture(
                $"def main():\n    print(len(\"{a}\" + \"{b}\") == len(\"{a}\") + len(\"{b}\"))");
            if (r != null && r != "True")
                throw new Exception($"len(\"{a}\" + \"{b}\") != len(\"{a}\") + len(\"{b}\")");
        }, iter: 25);
    }
}
