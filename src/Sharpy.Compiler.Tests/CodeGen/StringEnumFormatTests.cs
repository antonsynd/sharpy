using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// A string-backed enum formats as its str on every route, with a literal or a dynamic spec. That
/// is python's <c>StrEnum</c>: <c>f"[{c:>5}]"</c> → <c>[  red]</c>. A string enum lowers to a sealed
/// class, not a <c>System.Enum</c>. After #1988 the format engine refused every non-empty spec on a
/// value with no <c>__format__</c>, so the class printed right at d17ddb956 but threw
/// <c>TypeError: unsupported format string passed to Color.__format__</c> at 829b39998, while the
/// static twin (which projects every Sharpy enum to the str kind) accepted the same literal spec.
/// The cure is at the CLR-identity rung: the lowering implements <c>System.IFormattable</c>, the CLR
/// spelling of <c>__format__</c>, delegating to the str rules on <c>Value</c>.
///
/// <para>Oracle: python3.14 <c>class Color(StrEnum): RED = "red"; GREEN = "green"</c>. One recorded
/// divergence: for the refused <c>d</c> code python names the type <c>'Color'</c> (StrEnum is a str
/// subclass), while Sharpy's two twins both say <c>'str'</c>. That is Decision 6's projection for a
/// Sharpy enum, and it is python's own message for a plain <c>Enum</c>.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class StringEnumFormatTests : IntegrationTestBase
{
    public StringEnumFormatTests(ITestOutputHelper output) : base(output) { }

    private const string ColorEnum = "enum Color:\n    RED = \"red\"\n    GREEN = \"green\"\n\n";

    [Fact]
    public void StringEnum_FormatsAsItsStr_OnEveryRoute_LiteralAndDynamic()
    {
        var source = ColorEnum + """
            def main() -> None:
                c = Color.RED
                for spec in [">5", "<6", "^7"]:
                    print(f"[{format(c, spec)}]")
                print(f"[{c:>5}]")
                print(f"[{c:<6}]")
                print(f"[{c:^7}]")
                print("[{:>5}]".format(c))
                print("[{:<6}]".format(c))
                print("[{:^7}]".format(c))
                for spec2 in [">5", "<6", "^7"]:
                    print(f"[{c:{spec2}}]")
                    print(("[{:" + spec2 + "}]").format(c))
                spec = "d"
                try:
                    print(f"{c:{spec}}")
                except ValueError as e:
                    print("ValueError:", e)
                try:
                    print(format(c, spec))
                except ValueError as e:
                    print("ValueError:", e)
                try:
                    print(("{:" + spec + "}").format(c))
                except ValueError as e:
                    print("ValueError:", e)
                for x in [Color.RED, Color.GREEN]:
                    print(f"[{x:>6}]")
                print(f"[{c}]")
            """;
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors) + "\n" + result.StandardError);
        // python3.14 StrEnum, line for line (the three ValueError lines say 'str', see the class remarks).
        result.StandardOutput.Should().Be(
            "[  red]\n[red   ]\n[  red  ]\n"
            + "[  red]\n[red   ]\n[  red  ]\n"
            + "[  red]\n[red   ]\n[  red  ]\n"
            + "[  red]\n[  red]\n[red   ]\n[red   ]\n[  red  ]\n[  red  ]\n"
            + "ValueError: Unknown format code 'd' for object of type 'str'\n"
            + "ValueError: Unknown format code 'd' for object of type 'str'\n"
            + "ValueError: Unknown format code 'd' for object of type 'str'\n"
            + "[   red]\n[ green]\n[red]\n");

        var color = CSharpSyntaxTree.ParseText(result.GeneratedCSharp!).GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "Color");
        color.BaseList.Should().NotBeNull("the string-enum class implements System.IFormattable");
        color.BaseList!.Types.Select(t => t.Type.ToString()).Should().Contain("global::System.IFormattable");
    }

    public static IEnumerable<object[]> LiteralRefusals() => new[]
    {
        new object[] { "f", "print(f\"{Color.RED:d}\")" },
        new object[] { "format", "print(format(Color.RED, \"d\"))" },
        new object[] { "str_format", "print(\"{:d}\".format(Color.RED))" },
    };

    [Theory]
    [MemberData(nameof(LiteralRefusals))]
    public void StringEnum_LiteralUnknownCode_IsTheStaticTwinsRefusal(string route, string statement)
    {
        // The static twin and the runtime (dynamic cells above) name the same rule and type.
        var source = ColorEnum + "def main() -> None:\n    " + statement + "\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{route}] {source}");
        result.RawDiagnostics.Should().Contain(d => d.Message.Contains("Unknown format code 'd' for object of type 'str'"),
            $"[{route}] {string.Join(" | ", result.CompilationErrors)}");
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    [Fact]
    public void IntEnum_FormatsAsItsPythonStr()
    {
        // An int enum is a C# enum (System.Enum), routed as a str: the spec applies to python's
        // str(member), `Level.LOW` (#2007). python3: f"[{Level.LOW:>12}]" -> '[   Level.LOW]',
        // format(Level.HIGH, "<12") + "|" -> 'Level.HIGH  |'. (Before #2007: '[         LOW]'.)
        var source = "enum Level:\n    LOW = 1\n    HIGH = 2\n\ndef main() -> None:\n    print(f\"[{Level.LOW:>12}]\")\n    print(format(Level.HIGH, \"<12\") + \"|\")\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("[   Level.LOW]\nLevel.HIGH  |\n");
    }
}
