using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// <c>toml.loads[T]</c> binds a key to the member the compiler emitted for the field of that name:
/// the one forward rule, Core <see cref="NameMangling.ToPascalCase"/> (R-CG, #2040). Its own copy
/// dropped every underscore, so a key <c>x_</c> (field <c>X_</c>) bound the field <c>x</c> (<c>X</c>),
/// and <c>_x</c> (<c>_X</c>) bound <c>X</c>.
/// </summary>
public class TomlKeyMemberRuleTests : StdlibIntegrationTestBase
{
    public TomlKeyMemberRuleTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Key_BindsTheFieldOfTheSameSharpyName()
    {
        var result = CompileAndExecute(
            "import toml\n\n\nclass Cfg:\n    x: long = 0\n    x_: long = 0\n    max_conn: long = 0\n    port: long = 0\n\n\n"
            + "def main() -> None:\n"
            + "    c = toml.loads[Cfg](\"x_ = 1\\nmax_conn = 5\\nPORT = 7\\n\").Unwrap()\n"
            + "    print(c.x, c.x_, c.max_conn, c.port)\n");

        Assert.True(result.Success, $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal("0 1 5 7", result.StandardOutput.Trim());
    }

    public sealed class Underscored
    {
        public long X;
        public long _X;
        public long X_;
    }

    [Theory]
    [InlineData("_x = 3", 0, 3, 0)]
    [InlineData("x_ = 4", 0, 0, 4)]
    [InlineData("x = 5", 5, 0, 0)]
    public void UnderscoreAffixes_AreKept(string document, long x, long prefixed, long suffixed)
    {
        var value = Toml.Loads<Underscored>(document).Unwrap();
        Assert.Equal((x, prefixed, suffixed), (value.X, value._X, value.X_));
    }
}
