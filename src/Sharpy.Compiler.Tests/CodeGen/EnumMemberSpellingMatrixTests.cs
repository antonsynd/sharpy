using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// One materialized spelling per enum member (#2037, plan-0ca7b7 Decision 4, ruling 3): the member
/// symbol's <c>CodeGenInfo.CSharpName</c>, computed from the DECLARATION's escape flag, is read by
/// every declaration and reference arm. Six spellers used to answer independently — the declaration
/// arms ignored the escape, the <c>C.x</c> arm read the USE-site flag, the pattern arm had none and
/// the qualified <c>H.C.x</c> arm read the declaration's through a different mangler — so an escaped
/// string member and every nested camelCase int member were CS0117 behind SPY0908.
///
/// <para><b>Cells.</b> kind {int, string} × escape {EP, PE, EE, PP} (declaration escaped/plain ×
/// use escaped/plain) × route {top-level <c>Color.red</c>, nested <c>H.C.red</c>, match pattern
/// <c>case Color.red:</c>} — 24 programs that must run and print python's values; plus the
/// <c>.name</c> route, the SPY0522 collision route (an escaped member no longer collides; the plain
/// pair still does — the positive control), the nested camelCase int member with no escape, and a
/// WARM cell (a cache-served escaped member consumed by a recompiled file: the fact rides the
/// cache). The #2036 cells: the SPY0522 walk over a nested enum, and a string-enum member named
/// like a member the lowering synthesizes.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class EnumMemberSpellingMatrixTests : IntegrationTestBase
{
    public EnumMemberSpellingMatrixTests(ITestOutputHelper output) : base(output) { }

    private static string Decl(bool escaped) => escaped ? "`red`" : "red";

    private static string Use(bool escaped) => escaped ? "`red`" : "red";

    private static string Members(string kind, bool declEscaped)
        => kind == "int"
            ? $"{Decl(declEscaped)} = 1\n    BLUE = 2\n"
            : $"{Decl(declEscaped)} = \"r\"\n    BLUE = \"b\"\n";

    private static string ValueOut(string kind) => kind == "int" ? "1" : "r";

    public static IEnumerable<object[]> Cells()
        => from kind in new[] { "int", "string" }
           from escape in new[] { "EP", "PE", "EE", "PP" }
           from route in new[] { "top", "nest", "match" }
           select new object[] { kind, escape, route };

    /// <summary>
    /// The program for a cell and python's output for it (python3: <c>Color.red == Color.red</c> is
    /// <c>True</c>, <c>.value</c> is the backing value, the match arm names the member).
    /// </summary>
    private static (string Source, string Expected) Program(string kind, string escape, string route)
    {
        var declEscaped = escape[0] == 'E';
        var use = Use(escape[1] == 'E');
        switch (route)
        {
            case "top":
                return ("enum Color:\n    " + Members(kind, declEscaped)
                        + $"\n\ndef main() -> None:\n    c = Color.{use}\n    print(c == Color.{use})\n    print(c.value)\n",
                    $"True\n{ValueOut(kind)}\n");
            case "nest":
                // Compared, not `.value`: a nested enum's `.value` through a class qualifier is untyped (#2038).
                return ("class H:\n    enum C:\n        " + Members(kind, declEscaped).Replace("\n    ", "\n        ")
                        + $"\n\ndef main() -> None:\n    c = H.C.{use}\n    print(c == H.C.{use})\n    print(c == H.C.BLUE)\n",
                    "True\nFalse\n");
            default:
                return ("enum Color:\n    " + Members(kind, declEscaped)
                        + $"\n\ndef main() -> None:\n    c = Color.red\n    match c:\n        case Color.{use}:\n"
                        + "            print(\"red\")\n        case _:\n            print(\"other\")\n",
                    "red\n");
        }
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void EveryRoute_SpellsTheDeclaredMember(string kind, string escape, string route)
    {
        var (source, expected) = Program(kind, escape, route);

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"[{kind}/{escape}/{route}] {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected, $"[{kind}/{escape}/{route}]\n{source}");
    }

    /// <summary>
    /// The <c>.name</c> route reads the declared (python) name. For a string enum it is the singleton's
    /// constructor argument in every escape cell; for an int enum it is the C# member's name, which is
    /// the declared name exactly when the member is escaped — the unescaped int cell (<c>red</c> →
    /// <c>Red</c>) is #2069, not this contract.
    /// </summary>
    [Theory]
    [InlineData("string", "EP")]
    [InlineData("string", "PE")]
    [InlineData("string", "EE")]
    [InlineData("string", "PP")]
    [InlineData("int", "EP")]
    [InlineData("int", "EE")]
    public void TheNameRoute_IsTheDeclaredName(string kind, string escape)
    {
        var source = "enum Color:\n    " + Members(kind, escape[0] == 'E')
            + $"\n\ndef main() -> None:\n    print(Color.{Use(escape[1] == 'E')}.name)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"[{kind}/{escape}] {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("red\n", $"[{kind}/{escape}] python prints the declared name");
    }

    /// <summary>
    /// The collision route (SPY0522) reads the same speller: an escaped <c>`red`</c> compiles to
    /// <c>red</c> and no longer collides with its mangled twin — the program runs. The plain pair is
    /// the positive control: still refused, on the same input shape.
    /// </summary>
    [Theory]
    [InlineData("int", "Red")]
    [InlineData("string", "Red")]
    public void AnEscapedMember_NoLongerCollides_ThePlainPairStillDoes(string kind, string twin)
    {
        var value = kind == "int" ? ("1", "2") : ("\"a\"", "\"b\"");
        string Source(string first)
            => $"enum Color:\n    {first} = {value.Item1}\n    {twin} = {value.Item2}\n\n\n"
               + $"def main() -> None:\n    print(Color.red == Color.{twin})\n";

        var escaped = CompileAndExecute(Source("`red`"));
        escaped.Success.Should().BeTrue($"[{kind}] {string.Join(" | ", escaped.CompilationErrors)}");
        escaped.StandardOutput.Should().Be("False\n");

        var plain = CompileAndExecute(Source("red"));
        plain.Success.Should().BeFalse("the unescaped pair compiles to one identifier");
        plain.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.CodeGen.MemberNameCollision,
            "the positive control: the collision check still fires on the plain pair");
    }

    /// <summary>
    /// A nested int enum's camelCase member with no escape at all: the declaration spelled
    /// <c>Mygreen</c> (ToEnumMemberName) while the qualified reference spelled <c>MyGreen</c> (the
    /// field mangler) — CS0117. Both now read the member's fact.
    /// </summary>
    [Fact]
    public void ANestedCamelCaseIntMember_Runs()
    {
        var source = "class H:\n    enum C:\n        myGreen = 1\n        BLUE = 2\n\n\n"
            + "def main() -> None:\n    c = H.C.myGreen\n    print(c == H.C.myGreen)\n    print(c == H.C.BLUE)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("True\nFalse\n");
    }

    /// <summary>
    /// The fact rides the incremental cache: <c>lib.spy</c> declares an escaped string member and is
    /// served from the cache while <c>main.spy</c> recompiles (a content edit) and references it plain
    /// and escaped. Warm must build and print what cold prints.
    /// </summary>
    [Fact]
    public void AWarmConsumer_OfACachedEscapedMember_SpellsTheDeclaredMember()
    {
        const string lib = "enum Color:\n    `red` = \"r\"\n    BLUE = \"b\"\n";
        const string initial = "from lib import Color\n\n\ndef main() -> None:\n    print(Color.BLUE.value)\n";
        const string edited = "from lib import Color\n\n\ndef main() -> None:\n    print(Color.red.value)\n"
            + "    print(Color.`red` == Color.red)\n    match Color.red:\n        case Color.red:\n            print(\"red\")\n"
            + "        case _:\n            print(\"other\")\n";

        using var warm = new ProjectCompilationHelper(Output).WithIncremental();
        warm.AddSourceFile("lib.spy", lib).AddSourceFile("main.spy", initial);
        warm.Compile().Success.Should().BeTrue("the first build writes the cache");

        warm.UpdateSourceFile("main.spy", edited);
        var warmRun = warm.CompileAndExecute();
        warm.AssertWarmBuildSkipped(warm.LastCompilationResult!, "lib.spy");

        using var cold = new ProjectCompilationHelper(Output).WithIncremental();
        cold.AddSourceFile("lib.spy", lib).AddSourceFile("main.spy", edited);
        var coldRun = cold.CompileAndExecute();

        coldRun.Success.Should().BeTrue(string.Join(" | ", coldRun.CompilationErrors));
        coldRun.StandardOutput.Should().Be("r\nTrue\nred\n");
        warmRun.Success.Should().BeTrue(string.Join(" | ", warmRun.CompilationErrors));
        warmRun.StandardOutput.Should().Be(coldRun.StandardOutput);
    }

    /// <summary>
    /// #2036, hole 1: the SPY0522 member walk runs for a NESTED enum too — it only ever ran from the
    /// module-level declaration, so a nested pair compiling to one identifier was CS0102 behind
    /// SPY0908 while its top-level twin was SPY0522. The escaped twin is the positive control that
    /// the walk keys on the emitted name.
    /// </summary>
    [Theory]
    // int: ToEnumMemberName spells both `darkBlue` and `Darkblue` as `Darkblue`; string: the
    // constant speller spells both `dark_blue` and `DarkBlue` as `DarkBlue`.
    [InlineData("int", "darkBlue", "Darkblue", "1", "2")]
    [InlineData("string", "dark_blue", "DarkBlue", "\"a\"", "\"b\"")]
    public void ANestedEnumsMembers_AreWalkedForCollisions(
        string kind, string mangled, string twin, string first, string second)
    {
        string Source(string decl)
            => $"class O:\n    enum C:\n        {decl} = {first}\n        {twin} = {second}\n\n\n"
               + $"def main() -> None:\n    print(O.C.{twin} == O.C.{twin})\n";

        var plain = CompileAndExecute(Source(mangled));
        plain.Success.Should().BeFalse($"[{kind}] '{mangled}' and '{twin}' compile to one identifier");
        plain.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{kind}] the refusal is SPY0522, not CS0102 behind SPY0908");
        plain.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.CodeGen.MemberNameCollision
            && d.Message.Contains($"'{twin}' and '{mangled}'"));

        var escaped = CompileAndExecute(Source($"`{mangled}`"));
        escaped.Success.Should().BeTrue($"[{kind}] {string.Join(" | ", escaped.CompilationErrors)}");
        escaped.StandardOutput.Should().Be("True\n");
    }

    /// <summary>
    /// #2036, hole 2: a string enum lowers to a class that synthesizes Name, Value, Values and
    /// ToString, so a member compiling to one of those names (spelled so, or mangled into it) is a
    /// member collision — SPY0522, not CS0102 behind SPY0908. An escaped lowercase spelling compiles
    /// verbatim and runs (the positive control, with the escape steer's premise).
    /// </summary>
    [Theory]
    [InlineData("Name", false)]
    [InlineData("Value", false)]
    [InlineData("Values", false)]
    [InlineData("ToString", false)]
    [InlineData("name", true)]
    [InlineData("value", true)]
    [InlineData("to_string", true)]
    public void AStringEnumMember_NamedLikeASynthesizedMember_IsSpy0522(string member, bool escapeHelps)
    {
        var source = $"enum Level:\n    {member} = \"n\"\n    B = \"b\"\n\n\n"
            + "def main() -> None:\n    print(Level.B.value)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "the refusal is SPY0522, not CS0102 behind SPY0908");
        var refusal = result.RawDiagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.CodeGen.MemberNameCollision).Subject;
        refusal.Message.Should().Contain($"enum member '{member}' compiles to");
        refusal.Line.Should().Be(2);
        if (escapeHelps)
        {
            refusal.Message.Should().EndWith($"backtick-escape its declaration (`{member}`) to keep the Python spelling.");
            var escaped = CompileAndExecute(source.Replace($"    {member} = ", $"    `{member}` = "));
            escaped.Success.Should().BeTrue(string.Join(" | ", escaped.CompilationErrors));
            escaped.StandardOutput.Should().Be("b\n");
        }
        else
        {
            refusal.Message.Should().EndWith("Rename the member.");
        }
    }

    [Fact]
    public void Matrix_IsTotal()
        => Cells().Should().HaveCount(24, "2 kinds × 4 escape cells × 3 routes");
}
