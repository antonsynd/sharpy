using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The escape-identity matrix over VALUE-POSITION BINDING FORMS (#1326, #1281). Every form that
/// binds a name usable bare as a value is a row; each row is run twice, once with the binding
/// spelled bare and once backtick-escaped.
/// </summary>
/// <remarks>
/// <para>Both columns call the binding through its own spelling and then call the BARE spelling of
/// the same name, so one program answers both halves of the rule at once:</para>
/// <list type="bullet">
///   <item>bare column — the binding shadows the builtin, so <c>int(1)</c> runs the bound function
///     and prints <c>2</c>; CPython does the same</item>
///   <item>escaped column — the binding is the user's own name, so <c>int(1)</c> is still the
///     builtin conversion and prints <c>1</c></item>
/// </list>
/// <para>The forms were not all plumbed. The parameter and comprehension rows printed <c>2</c> in
/// the ESCAPED column — the escaped binding had captured the bare spelling too, silently, because
/// both spellings mangle to the same C# name. The walrus row had no flag on the AST node at all:
/// it drew a spurious SPY0483 and destroyed the bare builtin outright. A per-row fix would have
/// left the next unplumbed form to be found the same way, so the matrix is the guard.</para>
/// </remarks>
[Collection("HeavyCompilation")]
public class BacktickEscapedBindingFormTests : IntegrationTestBase
{
    public BacktickEscapedBindingFormTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>The binding forms under test, keyed by the body each one wraps around its target.</summary>
    public static TheoryData<string, bool> Rows()
    {
        var data = new TheoryData<string, bool>();
        foreach (var form in new[]
                 {
                     "assignment", "declaration", "for_statement", "comprehension", "walrus",
                     "parameter",
                 })
        {
            data.Add(form, false);
            data.Add(form, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void BindingForm_BareSpellingReachesTheBuiltinOnlyWhenTheBindingIsEscaped(
        string form, bool escaped)
    {
        var name = escaped ? "`int`" : "int";

        // The bound function doubles, so a call through the binding prints 42 in both columns; the
        // bare `int(1)` prints 1 through the builtin and 2 through the binding.
        var source = "def twice(x: int) -> int:\n    return x * 2\n\n" + form switch
        {
            "assignment" =>
                "def main() -> None:\n"
                + $"    {name} = twice\n"
                + $"    print({name}(21))\n"
                + "    print(int(1))\n",

            "declaration" =>
                "def main() -> None:\n"
                + $"    {name}: (int) -> int = twice\n"
                + $"    print({name}(21))\n"
                + "    print(int(1))\n",

            "for_statement" =>
                "def main() -> None:\n"
                + $"    for {name} in [twice]:\n"
                + $"        print({name}(21))\n"
                + "        print(int(1))\n",

            "comprehension" =>
                "def main() -> None:\n"
                + $"    print([{name}(21) for {name} in [twice]][0])\n"
                + $"    print([int(1) for {name} in [twice]][0])\n",

            "walrus" =>
                "def main() -> None:\n"
                + $"    bound = ({name} := twice)\n"
                + $"    print({name}(21))\n"
                + "    print(int(1))\n"
                + "    print(bound(0))\n",

            "parameter" =>
                $"def call_it({name}: (int) -> int) -> None:\n"
                + $"    print({name}(21))\n"
                + "    print(int(1))\n\n"
                + "def main() -> None:\n"
                + "    call_it(twice)\n",

            _ => throw new ArgumentOutOfRangeException(nameof(form), form, "unknown binding form"),
        };

        var expected = "42\n" + (escaped ? "1\n" : "2\n") + (form == "walrus" ? "0\n" : "");

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"'{form}' ({(escaped ? "escaped" : "bare")}) failed to compile: "
            + string.Join(", ", result.CompilationErrors));
        Assert.Equal(expected, result.StandardOutput);
    }

    /// <summary>
    /// The same rule in VALUE position rather than callee position: with an escaped binding holding
    /// the name, a bare <c>int</c> read is still the builtin's constructor reference and still pins
    /// against the annotation (#1182's tier 1).
    /// </summary>
    /// <remarks>
    /// This is a separate theory because it has no meaningful bare column — in the bare column the
    /// binding IS <c>int</c>, so <c>h: (str) -&gt; int = int</c> is a plain (and correct) type
    /// mismatch. It is a separate SEAM as well: the constructor-reference classifier re-looks the
    /// name up, and with the escaped binding in scope it answered "not a type reference" and dropped
    /// the pinning, reporting <c>Cannot assign '(__synth_T0) -&gt; int'</c>. That failed for the
    /// assignment form too, which the callee-position rule had covered since #1281 — the tell that
    /// this was a second seam and not a missing binding-form flag (#1326).
    /// </remarks>
    [Theory]
    [InlineData("assignment")]
    [InlineData("declaration")]
    [InlineData("for_statement")]
    [InlineData("walrus")]
    [InlineData("parameter")]
    public void EscapedBinding_BareReadStillPinsTheBuiltinConstructorReference(string form)
    {
        const string body =
            "    h: (str) -> int = int\n"
            + "    print(h(\"7\"))\n";

        var source = "def twice(x: int) -> int:\n    return x * 2\n\n" + form switch
        {
            "assignment" => "def main() -> None:\n    `int` = twice\n" + body,
            "declaration" => "def main() -> None:\n    `int`: (int) -> int = twice\n" + body,
            "for_statement" =>
                "def main() -> None:\n    for `int` in [twice]:\n"
                + body.Replace("    ", "        ", StringComparison.Ordinal),
            "walrus" => "def main() -> None:\n    bound = (`int` := twice)\n" + body,
            "parameter" =>
                "def call_it(`int`: (int) -> int) -> None:\n" + body
                + "\ndef main() -> None:\n    call_it(twice)\n",
            _ => throw new ArgumentOutOfRangeException(nameof(form), form, "unknown binding form"),
        };

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"'{form}' failed to compile: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("7\n", result.StandardOutput);
    }
}
