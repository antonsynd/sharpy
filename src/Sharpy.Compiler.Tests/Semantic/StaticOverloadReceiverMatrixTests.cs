using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #2391 (R-JM): a call resolves over EVERY overload of the method it names, whatever the receiver
/// (<c>method_overloading.md</c>). A bare type-name receiver (<c>C.make(...)</c>) types its identifier
/// as the constructor reference (#432), so the call-site overload pass found no type, and the call was
/// validated against the member seam's FIRST <c>@static</c> overload: <c>C.make("abc")</c> beside
/// <c>C.make(1)</c> was SPY0220, and in the other declaration order the <c>int</c> call was refused
/// instead. The alias spelling (<c>type K = C</c>) already worked through its normalized receiver.
/// </summary>
/// <remarks>
/// Cells: receiver {type name, alias of the type, type name inside the class} × <c>@static</c> overload
/// count {1, 2, 3} × declaration order {forward, reversed} × argument shape {plain, lambda} → run, every
/// overload called once. Controls: instance overloads through an instance
/// {1, 2, 3} → run; instance-only overloads through the type name → SPY0290 alone; a mixed set
/// (<c>@static</c> + instance) through the type name binds its static half (run) and never its instance
/// half (refused, no SPY09xx). Not a cell here: a <c>@static</c> member through an INSTANCE
/// (<c>C().make(1)</c>) is CS0176 behind SPY0908 — #2147 (P30), its own tracker.
/// </remarks>
public class StaticOverloadReceiverMatrixTests : IntegrationTestBase
{
    public StaticOverloadReceiverMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>The overloads by parameter type, each returning a value that names it.</summary>
    private static readonly (string ParamType, string Body, string Argument, string Printed)[] Overloads =
    {
        ("int", "return x", "1", "1"),
        ("str", "return len(x) + 100", "\"abc\"", "103"),
        ("float", "return 7", "2.5", "7"),
    };

    private static string StaticOverloads(int count, bool reversed)
    {
        var picked = Overloads.Take(count).ToList();
        if (reversed)
            picked.Reverse();
        return string.Concat(picked.Select(o =>
            $"    @static\n    def make(x: {o.ParamType}) -> int:\n        {o.Body}\n"));
    }

    private static string Calls(string receiver, int count, string indent)
        => string.Concat(Overloads.Take(count).Select(o => $"{indent}print({receiver}.make({o.Argument}))\n"));

    private static string Expected(int count) => string.Join("\n", Overloads.Take(count).Select(o => o.Printed));

    public static IEnumerable<object[]> StaticCells()
    {
        foreach (var receiver in new[] { "type_name", "alias", "in_class" })
            foreach (var count in new[] { 1, 2, 3 })
                foreach (var reversed in new[] { false, true })
                {
                    if (count == 1 && reversed)
                        continue;
                    yield return new object[] { receiver, count, reversed };
                }
    }

    [Theory]
    [MemberData(nameof(StaticCells))]
    public void StaticOverloads_ResolveOverEveryOverload_ForEveryTypeReceiver(string receiver, int count, bool reversed)
    {
        var classBody = StaticOverloads(count, reversed);
        var source = receiver switch
        {
            "type_name" => $"class C:\n{classBody}\ndef main() -> None:\n{Calls("C", count, "    ")}",
            "alias" => $"class C:\n{classBody}\ntype K = C\n\ndef main() -> None:\n{Calls("K", count, "    ")}",
            "in_class" => $"class C:\n{classBody}    @static\n    def run_all() -> None:\n{Calls("C", count, "        ")}"
                + "\ndef main() -> None:\n    C.run_all()\n",
            _ => throw new ArgumentOutOfRangeException(nameof(receiver)),
        };

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{receiver} × {count} overload(s), reversed={reversed}:\n{source}\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be(Expected(count));
    }

    /// <summary>
    /// A lambda argument through the same call-site pass. The two overloads differ by ARITY, so the
    /// lambda's parameter type is the same under both. Overloads distinguished by the lambda's own
    /// parameter type, or a lambda in a slot only a later overload has, fail on EVERY receiver
    /// (alias and instance included) — a different class, not this receiver's.
    /// </summary>
    [Theory]
    [InlineData("type_name")]
    [InlineData("alias")]
    [InlineData("in_class")]
    public void StaticOverloadsTakingALambda_ResolveForEveryTypeReceiver(string receiver)
    {
        const string classBody =
            "    @static\n    def apply(f: (int) -> int) -> int:\n        return f(3)\n"
            + "    @static\n    def apply(f: (int) -> int, y: int) -> int:\n        return f(y)\n";
        static string LambdaCalls(string r, string indent)
            => $"{indent}print({r}.apply(lambda n: n + 1))\n{indent}print({r}.apply(lambda n: n * 2, 5))\n";
        var source = receiver switch
        {
            "type_name" => $"class C:\n{classBody}\ndef main() -> None:\n{LambdaCalls("C", "    ")}",
            "alias" => $"class C:\n{classBody}\ntype K = C\n\ndef main() -> None:\n{LambdaCalls("K", "    ")}",
            "in_class" => $"class C:\n{classBody}    @static\n    def run_all() -> None:\n{LambdaCalls("C", "        ")}"
                + "\ndef main() -> None:\n    C.run_all()\n",
            _ => throw new ArgumentOutOfRangeException(nameof(receiver)),
        };

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{receiver}:\n{source}\n" + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("4\n10");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InstanceOverloadsThroughAnInstance_Run(int count)
    {
        var methods = string.Concat(Overloads.Take(count).Select(o =>
            $"    def make(self, x: {o.ParamType}) -> int:\n        {o.Body}\n"));
        var source = $"class C:\n{methods}\ndef main() -> None:\n    c = C()\n{Calls("c", count, "    ")}";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be(Expected(count));
    }

    [Fact]
    public void InstanceOnlyOverloadsThroughTheTypeName_AreRefusedOnce()
    {
        const string source =
            "class C:\n    def make(self, x: int) -> int:\n        return x\n"
            + "    def make(self, x: str) -> int:\n        return len(x)\n\n"
            + "def main() -> None:\n    print(C.make(1))\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Where(d => d.IsError).Select(d => d.Code)
            .Should().Equal(DiagnosticCodes.Semantic.InstanceFieldViaTypeName);
    }

    /// <summary>
    /// A type receiver binds the STATIC half of a mixed set: the static overload runs, and the instance
    /// overload is never bound through the type name — refused in Sharpy, never reaching Roslyn (CS0120).
    /// </summary>
    [Fact]
    public void AMixedSetThroughTheTypeName_BindsItsStaticHalfOnly()
    {
        const string mixed =
            "class C:\n    @static\n    def make(x: int) -> int:\n        return x\n"
            + "    def make(self, x: str) -> int:\n        return len(x)\n\n";

        var runs = CompileAndExecute(mixed + "def main() -> None:\n    print(C.make(1))\n    print(C().make(\"abc\"))\n");
        runs.Success.Should().BeTrue(string.Join("\n", runs.CompilationErrors));
        runs.StandardOutput.Trim().Should().Be("1\n3");

        var refused = CompileAndExecute(mixed + "def main() -> None:\n    print(C.make(\"abc\"))\n");
        refused.Success.Should().BeFalse();
        refused.RawDiagnostics.Where(d => d.IsError).Should().NotBeEmpty()
            .And.NotContain(d => d.Code.StartsWith("SPY09", StringComparison.Ordinal));
    }
}
