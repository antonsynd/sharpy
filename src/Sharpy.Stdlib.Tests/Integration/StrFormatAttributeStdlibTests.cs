using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// #2040 (R-CG), the Stdlib host of <c>StrFormatAttributeMatrixTests</c>: <c>str.format</c>'s
/// <c>{0.attr}</c> reaches a Stdlib type's member by the forward rule
/// (<c>NameMangling.ToPascalCase</c>) — <c>{0.year}</c> is <c>DateTime.Year</c>, <c>{0.days}</c> is
/// the timedelta's <c>Days</c>. Expected stdout is python3 3.12's on the same program.
/// </summary>
public class StrFormatAttributeStdlibTests : StdlibIntegrationTestBase
{
    public StrFormatAttributeStdlibTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void AttributeField_ResolvesStdlibMembers()
    {
        const string program = "from datetime import datetime, timedelta\n\n"
            + "def main() -> None:\n"
            + "    print(\"{0.year}\".format(datetime(2020, 1, 2)))\n"
            + "    print(\"{0.days}\".format(timedelta(days=3)))\n"
            + "    print(\"[{0.month:>3}]\".format(datetime(2020, 1, 2)))\n";

        var result = CompileAndExecute(program);

        Assert.True(result.Success, result.StandardError);
        Assert.Equal("2020\n3\n[  1]", result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n'));
    }
}
