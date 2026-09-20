using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// The Python property-decorator refusal matrix (SPY0148, #1854) — every HOST a decorator can be
/// written in, crossed with every SHAPE of the decorator itself.
///
/// <para><b>Contract.</b> A refused Python property decorator names a replacement that the Sharpy
/// grammar ACCEPTS. The refusal is a property of the decorator's shape, not of its host: the
/// parser sees the same token sequence in a class, a struct, an interface, a nested class and at
/// module level.</para>
///
/// <para><b>What was wrong.</b> The steer interpolated the decorator's suffix verbatim, so
/// <c>@p.setter</c> said "Declare 'property setter p(self, ...) -> ...:'" and <c>@p.getter</c>
/// said "property getter ..." — neither parses (SPY0104) — while <c>@p.deleter</c> was mapped onto
/// <c>property set</c>, which parses with semantics the user never asked for. Only four
/// class-hosted fixtures existed, and each asserted the message PREFIX only, so the steer itself
/// was unguarded.</para>
///
/// <para>Each cell asserts the diagnostic CODE, the steer's phrases, and the ABSENCE of the
/// non-parsing spellings (<see cref="NonParsingSpellings"/>) — a cell that only asserted the
/// prefix is what let the defect live.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class PropertyDecoratorMatrixTests : IntegrationTestBase
{
    public PropertyDecoratorMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Every host a decorated member can sit in. Read by <see cref="Totality"/>: a declared host
    /// with no cell fails the suite, so the roster is not a label.
    /// </summary>
    private static readonly string[] Hosts = { "class", "struct", "interface", "module", "nested" };

    /// <summary>
    /// Every decorator shape. The first five are SPY0148 (the parser refuses them); the last two
    /// are NOT in the property family and must stay SPY0444 — a one-part <c>@setter</c> and a
    /// three-part <c>@a.b.setter</c> are ordinary unknown decorators, and a cure that widened the
    /// refusal to swallow them would be a regression.
    /// </summary>
    private static readonly string[] Shapes =
    {
        "property", "getter", "setter", "deleter", "stacked_staticmethod",
        "onepart_setter", "threepart_setter",
    };

    /// <summary>
    /// Spellings that do NOT parse (or that mean something else), asserted absent from every
    /// SPY0148 message. <c>(self, ...) -&gt; ...:</c> is the placeholder the old steer emitted at
    /// all three two-part shapes; <c>property setter</c>/<c>getter</c>/<c>deleter</c> are the
    /// keyword spellings the grammar has no rule for.
    /// </summary>
    private static readonly string[] NonParsingSpellings =
    {
        "(self, ...) -> ...:", "property setter", "property getter", "property deleter",
    };

    private const string Spy0148 = "SPY0148";
    private const string Spy0444 = "SPY0444";

    [Theory]
    [MemberData(nameof(MatrixCells))]
    public void RefusalNamesAParsingSpelling(
        string host, string shape, string source, string expectedCode, string expectedPhrasesJoined)
    {
        var label = $"host={host} shape={shape}";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"{label} must be refused");

        var candidates = result.RawDiagnostics.Where(d => d.Code == expectedCode).ToList();
        candidates.Should().NotBeEmpty(
            $"expected {expectedCode} for {label}; got: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));

        foreach (var phrase in expectedPhrasesJoined.Split("||", StringSplitOptions.RemoveEmptyEntries))
        {
            candidates.Should().Contain(
                d => d.Message.Contains(phrase, StringComparison.Ordinal),
                $"{label} must carry '{phrase}'; got: "
                + string.Join(" | ", candidates.Select(d => d.Message)));
        }

        if (expectedCode != Spy0148)
            return;

        // The steer must not name a spelling the grammar rejects. Asserted on the SPY0148
        // diagnostics only, so an unrelated message elsewhere cannot trip it.
        foreach (var bad in NonParsingSpellings)
        {
            candidates.Should().NotContain(
                d => d.Message.Contains(bad, StringComparison.OrdinalIgnoreCase),
                $"{label} must not steer to '{bad}' — it does not parse; got: "
                + string.Join(" | ", candidates.Select(d => d.Message)));
        }
    }

    /// <summary>
    /// The replacement forms, RUN. Without these the matrix could be satisfied by a steer that
    /// names a spelling nothing accepts — which is exactly the defect. Printed values were taken
    /// from python3 3.12 first (Rule 6): the class/nested/module pairs print 3 then 7, the struct
    /// pair prints 6 then 10, the interface control prints 3.
    /// </summary>
    [Theory]
    [MemberData(nameof(PositiveControls))]
    public void SteerSpellingRuns(string host, string label, string source, string expectedOutput)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"the steer spelling for host={host} ('{label}') must compile and run; errors: "
            + string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd()
            .Should().Be(expectedOutput, $"host={host} control '{label}'");
    }

    public static TheoryData<string, string, string, string, string> MatrixCells
    {
        get
        {
            var data = new TheoryData<string, string, string, string, string>();

            foreach (var host in Hosts)
            {
                foreach (var shape in Shapes)
                {
                    var (code, phrases) = Expectation(shape);
                    data.Add(host, shape, Program(host, Member(shape, HasSelf(host))), code,
                        string.Join("||", phrases));
                }
            }

            return data;
        }
    }

    /// <summary>
    /// The diagnostic code and the phrases the message must carry, per shape. The property
    /// spellings are the spec's (<c>properties.md</c> §Property Forms); each was executed through
    /// <c>sharpyc run</c> before being asserted here.
    /// </summary>
    private static (string Code, string[] Phrases) Expectation(string shape) => shape switch
    {
        "property" or "stacked_staticmethod" => (Spy0148, new[]
        {
            "@property is not a decorator in Sharpy",
            "'property get p(self) -> T:'",
            "'property set p(self, value: T) -> None:'",
            "'property [get|set|init] p: T [= value]'",
        }),

        "getter" => (Spy0148, new[]
        {
            "@size.getter is not a decorator in Sharpy",
            "no '<name>.getter' decorator form",
            "'property get size(self) -> T:'",
            "'property [get|set|init] size: T [= value]'",
        }),

        "setter" => (Spy0148, new[]
        {
            "@size.setter is not a decorator in Sharpy",
            "no '<name>.setter' decorator form",
            "'property set size(self, value: T) -> None:'",
            "'property [get|set|init] size: T [= value]'",
        }),

        // There is no deleter form to steer to — 'del' is unsupported — so the message says so
        // instead of naming a replacement accessor.
        "deleter" => (Spy0148, new[]
        {
            "@size.deleter is not a decorator in Sharpy",
            "there is no deleter form at all",
            "'del' is not supported (Axiom 1)",
        }),

        "onepart_setter" => (Spy0444, new[] { "Unknown decorator '@setter'" }),
        "threepart_setter" => (Spy0444, new[] { "Unknown decorator '@a.b.setter'" }),

        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown decorator shape"),
    };

    /// <summary>Module-level and <c>@staticmethod</c>-stacked accessors take no <c>self</c>.</summary>
    private static bool HasSelf(string host) => host != "module";

    private static string Member(string shape, bool hasSelf)
    {
        var self = hasSelf ? "self" : "";
        var selfComma = hasSelf ? "self, " : "";

        return shape switch
        {
            "property" => $"@property\ndef size({self}) -> int:\n    return 3",
            "getter" => $"@size.getter\ndef size({self}) -> int:\n    return 3",
            "setter" => $"@size.setter\ndef size({selfComma}value: int) -> None:\n    pass",
            "deleter" => $"@size.deleter\ndef size({self}) -> None:\n    pass",
            "stacked_staticmethod" => "@staticmethod\n@property\ndef size() -> int:\n    return 3",
            "onepart_setter" => $"@setter\ndef size({selfComma}value: int) -> None:\n    pass",
            "threepart_setter" => $"@a.b.setter\ndef size({selfComma}value: int) -> None:\n    pass",
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown decorator shape"),
        };
    }

    /// <summary>Wraps a decorated member in each host, at that host's indentation.</summary>
    private static string Program(string host, string member) => host switch
    {
        "class" =>
            "class Box:\n"
            + "    _size: int\n\n"
            + "    def __init__(self, size: int):\n"
            + "        self._size = size\n\n"
            + Indent(member, 4) + "\n\n"
            + Main,

        // A struct backing field must not be underscore-prefixed (#1937).
        "struct" =>
            "struct Point:\n"
            + "    size: int\n\n"
            + "    def __init__(self, size: int):\n"
            + "        self.size = size\n\n"
            + Indent(member, 4) + "\n\n"
            + Main,

        "interface" =>
            "interface IBoxed:\n"
            + Indent(member, 4) + "\n\n"
            + Main,

        "module" => member + "\n\n" + Main,

        "nested" =>
            "class Outer:\n"
            + "    class Inner:\n"
            + Indent(member, 8) + "\n\n"
            + Main,

        _ => throw new ArgumentOutOfRangeException(nameof(host), host, "unknown host"),
    };

    private const string Main = "def main() -> None:\n    print(\"x\")\n";

    private static string Indent(string block, int spaces)
        => string.Join("\n", block.Split('\n').Select(l => l.Length == 0 ? l : new string(' ', spaces) + l));

    public static TheoryData<string, string, string, string> PositiveControls
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();

            // python3 3.12: a @property/@size.setter pair over self._size prints 3 then 7.
            data.Add("class", "function-style get + set",
                "class Box:\n"
                + "    _size: int\n\n"
                + "    def __init__(self, size: int):\n"
                + "        self._size = size\n\n"
                + "    property get size(self) -> int:\n"
                + "        return self._size\n\n"
                + "    property set size(self, value: int) -> None:\n"
                + "        self._size = value\n\n"
                + "def main() -> None:\n"
                + "    b: Box = Box(3)\n"
                + "    print(b.size)\n"
                + "    b.size = 7\n"
                + "    print(b.size)\n",
                "3\n7");

            // python3 3.12: doubled == x*2, and setting it to 10 leaves x == 5 -> prints 6 then 10.
            data.Add("struct", "function-style get + set on a struct",
                "struct Point:\n"
                + "    x: int\n\n"
                + "    def __init__(self, x: int):\n"
                + "        self.x = x\n\n"
                + "    property get doubled(self) -> int:\n"
                + "        return self.x * 2\n\n"
                + "    property set doubled(self, value: int) -> None:\n"
                + "        self.x = value // 2\n\n"
                + "def main() -> None:\n"
                + "    p: Point = Point(3)\n"
                + "    print(p.doubled)\n"
                + "    p.doubled = 10\n"
                + "    print(p.doubled)\n",
                "6\n10");

            // An interface declares the getter requirement; the implementing class satisfies it.
            data.Add("interface", "interface property requirement",
                "interface IBoxed:\n"
                + "    property get size(self) -> int\n\n"
                + "class Box(IBoxed):\n"
                + "    _size: int\n\n"
                + "    def __init__(self, size: int):\n"
                + "        self._size = size\n\n"
                + "    property get size(self) -> int:\n"
                + "        return self._size\n\n"
                + "def main() -> None:\n"
                + "    b: IBoxed = Box(3)\n"
                + "    print(b.size)\n",
                "3");

            // Module-level accessors take no 'self' — the clause the steer states.
            data.Add("module", "module-level get + set",
                "_count: int = 3\n\n"
                + "property get count() -> int:\n"
                + "    return _count\n\n"
                + "property set count(value: int) -> None:\n"
                + "    _count = value\n\n"
                + "def main() -> None:\n"
                + "    print(count)\n"
                + "    count = 7\n"
                + "    print(count)\n",
                "3\n7");

            data.Add("nested", "function-style get + set on a nested class",
                "class Outer:\n"
                + "    class Inner:\n"
                + "        _size: int\n\n"
                + "        def __init__(self, size: int):\n"
                + "            self._size = size\n\n"
                + "        property get size(self) -> int:\n"
                + "            return self._size\n\n"
                + "        property set size(self, value: int) -> None:\n"
                + "            self._size = value\n\n"
                + "def main() -> None:\n"
                + "    i: Outer.Inner = Outer.Inner(3)\n"
                + "    print(i.size)\n"
                + "    i.size = 7\n"
                + "    print(i.size)\n",
                "3\n7");

            return data;
        }
    }

    [Fact]
    public void Totality()
    {
        var cells = MatrixCells.Cast<object[]>().ToList();
        var controls = PositiveControls.Cast<object[]>().ToList();

        // Axis sizes anchored to literals, not to the arrays' own Length.
        Hosts.Should().HaveCount(5, "class, struct, interface, module level and nested class");
        Shapes.Should().HaveCount(7,
            "@property, @x.getter, @x.setter, @x.deleter, @property under @staticmethod, "
            + "one-part @setter and three-part @a.b.setter");
        Hosts.Should().OnlyHaveUniqueItems();
        Shapes.Should().OnlyHaveUniqueItems();
        NonParsingSpellings.Should().HaveCount(4);

        // 5 hosts x 7 shapes, fully crossed: dropping a cell is a decision, not an omission.
        cells.Should().HaveCount(35);

        var coveredHosts = cells.Select(c => (string)c[0]).Distinct().ToList();
        var coveredShapes = cells.Select(c => (string)c[1]).Distinct().ToList();
        Hosts.Should().BeSubsetOf(coveredHosts,
            "every host needs a cell; uncovered: " + string.Join(", ", Hosts.Except(coveredHosts)));
        Shapes.Should().BeSubsetOf(coveredShapes,
            "every shape needs a cell; uncovered: " + string.Join(", ", Shapes.Except(coveredShapes)));
        coveredHosts.Should().BeSubsetOf(Hosts);
        coveredShapes.Should().BeSubsetOf(Shapes);

        // Both codes are exercised — a cure that swallowed @setter into the property family would
        // collapse the matrix onto SPY0148 alone.
        cells.Select(c => (string)c[3]).Distinct().Should().BeEquivalentTo(new[] { Spy0148, Spy0444 });
        cells.Count(c => (string)c[3] == Spy0444).Should().Be(10, "2 out-of-family shapes x 5 hosts");

        // One RUNNING control per host: the steer's replacement form, executed.
        controls.Should().HaveCount(5);
        controls.Select(c => (string)c[0]).Should().BeEquivalentTo(Hosts);
    }

    // -------------------------------------------------------------------------------------
    // Source scan: every SPY0148 message comes from the ONE formatter.
    // -------------------------------------------------------------------------------------

    private const string Formatter = "FormatPropertyDecoratorRefusal";

    /// <summary>
    /// The class contract in source form: a shape cannot acquire a steer of its own. Every parser
    /// site that reports <see cref="DiagnosticCodes.Parser.PropertyDecoratorNotSupported"/> must
    /// pass <see cref="Formatter"/>'s result as the message — the defect was one site building its
    /// own string from the decorator's suffix.
    /// </summary>
    [Fact]
    public void EveryRefusalSiteBuildsItsMessageWithTheOneFormatter()
    {
        var sites = new List<(string File, int Line, string Text)>();
        var formatted = new List<(string File, int Line, string Text)>();

        foreach (var file in ParserSourceFiles())
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not IdentifierNameSyntax { Identifier.ValueText: "ReportError" })
                    continue;

                var arguments = invocation.ArgumentList.Arguments;
                if (!arguments.Any(a => a.Expression.ToString().EndsWith(
                        "PropertyDecoratorNotSupported", StringComparison.Ordinal)))
                    continue;

                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var site = (Path.GetFileName(file), line, Truncate(invocation.ToString()));
                sites.Add(site);

                if (arguments[0].Expression is InvocationExpressionSyntax
                    { Expression: IdentifierNameSyntax { Identifier.ValueText: Formatter } })
                {
                    formatted.Add(site);
                }
            }
        }

        // Positive control: a scan that found nothing would pass the assertion below vacuously.
        sites.Should().HaveCount(2,
            "the parser refuses @property and the two-part @x.<accessor> shapes at exactly two "
            + "sites; found: " + string.Join("; ", sites.Select(Describe)));

        formatted.Should().BeEquivalentTo(sites,
            $"every SPY0148 site must build its message with {Formatter}; offenders: "
            + string.Join("; ", sites.Except(formatted).Select(Describe)));
    }

    private static string Describe((string File, int Line, string Text) site)
        => $"{site.File}:{site.Line}: {site.Text}";

    private static string Truncate(string text)
        => text.Length <= 110 ? text : text[..110] + "…";

    private static IEnumerable<string> ParserSourceFiles()
        => Directory.GetFiles(
            Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler", "Parser"), "*.cs",
            SearchOption.TopDirectoryOnly);

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, "src", "Sharpy.Compiler", "Parser")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("repository root not found from " + AppContext.BaseDirectory);
    }
}
