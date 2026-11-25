using System.Text;
using System.Text.Json;
using FluentAssertions;
using Sharpy.BlastRadius;
using Sharpy.BlastRadius.Probes;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.BlastRadius;

/// <summary>
/// The R-RO probe (<see cref="SuperCallOrderProbe"/>) against generated programs. The matrix is
/// generated, not written by hand (contract §11): one class per cell, all in one module, analysed
/// once. The expected verdict and fix come from <see cref="Expected"/>, which states the rule as a
/// table and shares no code with the probe.
/// </summary>
public class SuperCallOrderProbeTests
{
    private static readonly ProbeRunner Runner = new(new SuperCallOrderProbe());

    // ------------------------------------------------------------------ the generated matrix

    private sealed record SlotType(string Name, string Annotation, bool HasDefault,
        string FromParameter, string FromInherited, string FromEarlier);

    private static readonly SlotType[] Types =
    {
        new("ref", "str", false, "p", "str(self.n)", "self.first"),
        new("list", "list[int]", false, "p", "[self.n]", "[len(self.first)]"),
        new("value", "int", true, "p", "self.n", "len(self.first)"),
        new("nullable", "str | None", true, "p", "str(self.n)", "self.first"),
        new("optional", "int?", true, "p", "Some(self.n)", "Some(len(self.first))"),
        // Value types that SemanticType.IsValueType does not know as such.
        new("enum", "Color", true, "p", "color_of(self.n)", "color_of(len(self.first))"),
        new("tuple", "tuple[int, int]", true, "p", "(self.n, 1)", "(len(self.first), 1)"),
        new("generic_struct", "Pair[int]", true, "p", "Pair[int](self.n)", "Pair[int](len(self.first))"),
    };

    /// <summary>What the matrix's classes refer to.</summary>
    private const string MatrixPrelude = """
        enum Color:
            RED = 1
            GREEN = 2

        struct Pair[T]:
            item: T

            def __init__(self, item: T):
                self.item = item

        def color_of(n: int) -> Color:
            return Color.RED if n > 0 else Color.GREEN


        """;

    private static readonly string[] Kinds = { "field", "property", "getonly" };
    private static readonly string[] Positions = { "top", "nested" };
    private static readonly string[] Values = { "parameter", "local", "inherited", "earlier" };
    private static readonly string[] Writes = { "once", "method", "twice" };

    private sealed record Cell(SlotType Type, string Kind, string Position, string Value, string Write)
    {
        public string ClassName => $"C_{Type.Name}_{Kind}_{Position}_{Value}_{Write}";

        public string Source()
        {
            var declaration = Kind switch
            {
                "field" => $"f: {Type.Annotation}",
                "property" => $"property f: {Type.Annotation}",
                _ => $"property get f: {Type.Annotation}",
            };
            var value = Value switch
            {
                "parameter" => Type.FromParameter,
                "local" => "t",
                "inherited" => Type.FromInherited,
                _ => Type.FromEarlier,
            };

            var code = new StringBuilder();
            code.Append($"class {ClassName}(Base):\n");
            if (Value == "earlier")
                code.Append("    first: str\n");
            code.Append($"    {declaration}\n\n");
            code.Append($"    def __init__(self, p: {Type.Annotation}, q: str, c: bool):\n");
            code.Append("        super().__init__()\n");
            if (Value == "earlier")
                code.Append("        self.first = q\n");
            if (Value == "local")
                code.Append("        t = p\n");
            if (Position == "nested")
                code.Append($"        if c:\n            self.f = {value}\n        else:\n            self.f = {value}\n");
            else
                code.Append($"        self.f = {value}\n");
            if (Write == "twice")
                code.Append("        self.f = p\n");
            if (Write == "method")
                code.Append($"\n    def touch(self, p: {Type.Annotation}) -> None:\n        self.f = p\n");
            return code.Append('\n').ToString();
        }
    }

    private static IEnumerable<Cell> Cells()
        => from type in Types
           from kind in Kinds
           from position in Positions
           from value in Values
           from write in Writes
               // A get-only auto-property cannot be written outside __init__ or twice by design.
           where kind != "getonly" || write == "once"
           select new Cell(type, kind, position, value, write);

    /// <summary>The rule as a table (R-RO, R-RQ, R-RP, R-RU), written without reading the probe.</summary>
    private static (string Verdict, string? Fix, string? Why) Expected(Cell cell)
    {
        // Does the slot rest on a default? A get-only property never does; a field or a plain
        // property does when its type has one.
        var hasDefault = cell.Kind != "getonly" && cell.Type.HasDefault;
        // A plain auto-property with a default is not a slot the rule knows at all.
        var isSlot = cell.Kind != "property" || !hasDefault;
        var writtenAgain = cell.Write != "once";
        // `first: str` (the "earlier" value) is itself a reference-type field assigned after the call.
        var firstMoves = cell.Value == "earlier";

        var fMoves = isSlot && !(hasDefault && writtenAgain);
        var fMustMove = isSlot && !hasDefault;
        var fFix = cell.Position == "nested" ? "restructure"
            : cell.Value switch
            {
                "local" => "restructure",
                "inherited" => "uses-self",
                // Reads `self.first`, an own field assigned ahead of it: allowed, so it moves with it.
                _ => "move",
            };

        if (firstMoves)
        {
            // Refused for `first`; f's own fix counts only when f must move as well.
            return ("refused", fMustMove ? fFix : "move", null);
        }

        if (fMustMove)
            return ("refused", fFix, null);
        if (fMoves)
            return ("refused-if-let", fFix, null);
        return ("conforms", null, isSlot ? "var-only" : "no-own-slots");
    }

    [Fact]
    public void EveryCellOfTheMatrixGetsTheVerdictAndTheFixTheRuleGives()
    {
        var cells = Cells().ToList();
        cells.Should().HaveCount(8 * 2 * 4 * 2 * 3 + 8 * 1 * 4 * 2 * 1, "the matrix is the product of its axes");

        var source = new StringBuilder(MatrixPrelude + BaseClass);
        foreach (var cell in cells)
            source.Append(cell.Source());
        source.Append("def main() -> None:\n    pass\n");

        var sites = Analyse(source.ToString()).ToDictionary(s => s.Subject);

        var wrong = new List<string>();
        foreach (var cell in cells)
        {
            var site = sites[$"{cell.ClassName}.__init__"];
            var (verdict, fix, why) = Expected(cell);
            var actual = (site.Verdict, site.Coords.GetValueOrDefault("fix"), site.Coords.GetValueOrDefault("why"));
            if (actual != (verdict, fix, why))
                wrong.Add($"{cell.ClassName}: expected {(verdict, fix, why)}, got {actual} [{site.Detail}]");
        }

        wrong.Should().BeEmpty();
        // Positive control for the absences above: the matrix holds every verdict and every fix.
        sites.Values.Select(s => s.Verdict).Distinct().Should().BeEquivalentTo("refused", "refused-if-let", "conforms");
        sites.Values.Select(s => s.Coords.GetValueOrDefault("fix")).Where(f => f != null).Distinct()
            .Should().BeEquivalentTo("move", "restructure", "uses-self");
    }

    // ------------------------------------------------------------------ cells outside the matrix

    private const string BaseClass = "class Base:\n    n: int\n\n    def __init__(self):\n        self.n = 1\n\n";

    [Fact]
    public void AClassWithNoBaseClassIsNotASite()
    {
        Analyse("""
            class Lone:
                name: str

                def __init__(self):
                    self.name = "a"

            def main() -> None:
                pass
            """).Should().BeEmpty();
    }

    [Fact]
    public void AnInterfaceIsNotABaseClass()
    {
        Analyse("""
            interface INamed:
                def name(self) -> str:
                    ...

            class Thing(INamed):
                label: str

                def __init__(self):
                    self.label = "a"

                def name(self) -> str:
                    return self.label

            def main() -> None:
                pass
            """).Should().BeEmpty();
    }

    [Fact]
    public void AConstructorWithNoSuperCallIsOutOfScopeAndSaysWhetherABaseDeclaresOne()
    {
        var sites = Analyse(BaseClass + """
            class Plain:
                def hello(self) -> str:
                    return "hi"

            class NoCallNoBaseInit(Plain):
                name: str

                def __init__(self):
                    self.name = "a"

            class Delegating(Base):
                name: str

                def __init__(self, name: str):
                    super().__init__()
                    self.name = name

                def __init__(self):
                    self.__init__("d")

            def main() -> None:
                pass
            """);

        var noCall = sites.Single(s => s.Subject == "NoCallNoBaseInit.__init__");
        noCall.Verdict.Should().Be("out-of-scope");
        noCall.Coords.Should().Contain("call", "omitted").And.Contain("base-has-init", "none");

        var delegating = sites.Where(s => s.Subject == "Delegating.__init__").OrderBy(s => s.Line).ToList();
        delegating.Select(s => s.Verdict).Should().Equal("refused", "out-of-scope");
        delegating[1].Coords.Should().Contain("call", "chained-to-own-init").And.Contain("base-has-init", "declared");
    }

    [Fact]
    public void AParenthesizedSuperCallIsStillTheCall()
    {
        // The compiler's own predicate finds the call (AstHelper.TryGetConstructorInitializerCall);
        // a text search for "super().__init__(" does not.
        var site = Analyse(BaseClass + """
            class Wrapped(Base):
                tag: str

                def __init__(self, tag: str):
                    (super().__init__)()
                    self.tag = tag

            def main() -> None:
                pass
            """).Single();
        site.Verdict.Should().Be("refused");
        site.Coords.Should().Contain("fix", "move");
    }

    [Fact]
    public void ABuiltInExceptionBaseIsADotnetBase()
    {
        var site = Analyse("""
            class MyError(Exception):
                code: int
                detail: str

                def __init__(self, code: int, detail: str):
                    super().__init__(detail)
                    self.code = code
                    self.detail = detail

            def main() -> None:
                pass
            """).Single();
        site.Verdict.Should().Be("refused");
        site.Coords.Should().Contain("base", "dotnet").And.Contain("fix", "move");
        site.Detail.Should().Contain("code:").And.Contain("detail: str");
    }

    [Fact]
    public void ATupleTargetAssignsEachOfItsFields()
    {
        var site = Analyse(BaseClass + """
            class Pair(Base):
                a: str
                b: str

                def __init__(self, a: str, b: str):
                    super().__init__()
                    self.a, self.b = a, b

            def main() -> None:
                pass
            """).Single();
        site.Verdict.Should().Be("refused");
        site.Detail.Should().Contain("a: str").And.Contain("b: str");
    }

    [Fact]
    public void AStoreFromOutsideTheClassOrFromASubclassMakesTheFieldVar()
    {
        var sites = Analyse(BaseClass + """
            class ThroughAnother(Base):
                count: int

                def __init__(self):
                    super().__init__()
                    self.count = 0

            def bump(c: ThroughAnother) -> None:
                c.count = c.count + 1

            class InASubclassMethod(Base):
                count: int

                def __init__(self):
                    super().__init__()
                    self.count = 0

            class Writer(InASubclassMethod):
                def reset(self) -> None:
                    self.count = 5

            class InASubclassInit(Base):
                count: int

                def __init__(self):
                    super().__init__()
                    self.count = 0

            class Late(InASubclassInit):
                def __init__(self):
                    super().__init__()
                    self.count = 5

            class NeverAgain(Base):
                count: int

                def __init__(self):
                    super().__init__()
                    self.count = 0

            def main() -> None:
                pass
            """).ToDictionary(s => s.Subject);

        foreach (var written in new[] { "ThroughAnother", "InASubclassMethod", "InASubclassInit" })
        {
            sites[$"{written}.__init__"].Verdict.Should().Be("conforms", written);
            sites[$"{written}.__init__"].Coords.Should().Contain("why", "var-only");
        }

        // Positive control: the same class with no second write could be `let`.
        sites["NeverAgain.__init__"].Verdict.Should().Be("refused-if-let");
    }

    [Fact]
    public void AValueThatReadsAnOwnFieldMovesOnlyAsEasilyAsThatFieldDoes()
    {
        var site = Analyse(BaseClass + """
            class Chain(Base):
                a: int
                b: str

                def __init__(self):
                    super().__init__()
                    self.a = self.n + 1
                    self.b = str(self.a)

            def main() -> None:
                pass
            """).Single();
        // `b` must move, and its value reads `a`, whose own value reads an inherited field.
        site.Verdict.Should().Be("refused");
        site.Coords.Should().Contain("fix", "uses-self");
    }

    [Fact]
    public void AValueThatReadsAnOwnFieldWithAnInitializerIsAPlainMove()
    {
        var sites = Analyse(BaseClass + """
            class Initialized(Base):
                label: str = "x"
                name: str

                def __init__(self):
                    super().__init__()
                    self.name = self.label

            class Inherited(Base):
                name: str

                def __init__(self):
                    super().__init__()
                    self.name = str(self.n)

            def main() -> None:
                pass
            """).ToDictionary(s => s.Subject);

        // `label` is assigned by its initializer, which runs before the base constructor.
        sites["Initialized.__init__"].Coords.Should().Contain("fix", "move");
        // Positive control: a field the base constructor assigns is not there yet.
        sites["Inherited.__init__"].Coords.Should().Contain("fix", "uses-self");
    }

    [Fact]
    public void ABranchThatLeavesTheConstructorDoesNotCountTowardsASecondWrite()
    {
        var sites = Analyse(BaseClass + """
            class EarlyReturn(Base):
                k: int

                def __init__(self, c: bool):
                    super().__init__()
                    if c:
                        self.k = 1
                        return
                    self.k = 2

            class FallsThrough(Base):
                k: int

                def __init__(self, c: bool):
                    super().__init__()
                    if c:
                        self.k = 1
                    self.k = 2

            def main() -> None:
                pass
            """).ToDictionary(s => s.Subject);

        // `k` is written once on each path, so it could be `let`.
        sites["EarlyReturn.__init__"].Verdict.Should().Be("refused-if-let");
        // Positive control: without the return, the second store is a second write.
        sites["FallsThrough.__init__"].Verdict.Should().Be("conforms");
        sites["FallsThrough.__init__"].Coords.Should().Contain("why", "var-only");
    }

    [Fact]
    public void AClassThatNamesObjectAsItsBaseHasNoBaseClass()
    {
        Analyse("""
            class Spelled(object):
                name: str

                def __init__(self):
                    super().__init__()
                    self.name = "a"

            def main() -> None:
                pass
            """).Should().BeEmpty();
    }

    [Fact]
    public void AValueThatCallsAFunctionDeclaredInTheConstructorIsNotAPlainMove()
    {
        var site = Analyse(BaseClass + """
            class Helped(Base):
                name: str

                def __init__(self, p: str):
                    super().__init__()
                    def helper() -> str:
                        return p + "!"
                    self.name = helper()

            def main() -> None:
                pass
            """).Single();
        site.Verdict.Should().Be("refused");
        site.Coords.Should().Contain("fix", "restructure");
    }

    [Fact]
    public void AStoreInANestedFunctionIsNotAConstructorAssignment()
    {
        var site = Analyse(BaseClass + """
            class Late(Base):
                name: str

                def __init__(self):
                    super().__init__()
                    def set_later() -> None:
                        self.name = "later"
                    set_later()

            def main() -> None:
                pass
            """).Single();
        // The store runs when set_later() does, not as a statement of the constructor.
        site.Verdict.Should().Be("conforms");
        site.Coords.Should().Contain("why", "none-after-call");
    }

    [Fact]
    public void AFieldWithAnInitializerAndAStaticFieldAreNotSlots()
    {
        var site = Analyse(BaseClass + """
            class Settled(Base):
                label: str = "x"
                @static
                count: int = 0

                def __init__(self):
                    super().__init__()
                    self.label = "y"

            def main() -> None:
                pass
            """).Single();
        site.Verdict.Should().Be("conforms");
        site.Coords.Should().Contain("why", "no-own-slots");
    }

    [Fact]
    public void ADataclassWithABaseIsNotRuledAndSaysWhatTheTextWouldGive()
    {
        var site = Analyse("""
            @dataclass
            class Point:
                x: int

            @dataclass
            class Named(Point):
                name: str

            def main() -> None:
                pass
            """).Single();
        site.Subject.Should().Be("Named");
        site.Verdict.Should().Be("not-ruled");
        site.Coords.Should().Contain("as-if", "refused");
    }

    [Fact]
    public void AGenericBaseClassIsABaseClass()
    {
        var site = Analyse("""
            class Box[T]:
                item: T

                def __init__(self, item: T):
                    self.item = item

            class Labelled(Box[int]):
                label: str

                def __init__(self, item: int, label: str):
                    super().__init__(item)
                    self.label = label

            def main() -> None:
                pass
            """).Single();
        site.Subject.Should().Be("Labelled.__init__");
        site.Verdict.Should().Be("refused");
        site.Coords.Should().Contain("base", "sharpy");
    }

    [Fact]
    public void ANestedClassIsProbed()
    {
        var site = Analyse(BaseClass + """
            class Outer:
                class Inner(Base):
                    name: str

                    def __init__(self, name: str):
                        super().__init__()
                        self.name = name

            def main() -> None:
                pass
            """).Single();
        site.Subject.Should().Be("Inner.__init__");
        site.Verdict.Should().Be("refused");
    }

    // ------------------------------------------------------------------ the corpus and the report

    [Fact]
    public void AProgramThatDoesNotCompileIsSkippedWithItsFirstErrorCode()
    {
        var outcome = Runner.Run(new CorpusItem("t", "t", "def main() -> None:\n    print(nope)\n", null, 0, null));
        outcome.Skipped.Should().Be(ItemOutcome.DoesNotCompile);
        outcome.ErrorCode.Should().StartWith("SPY");
        outcome.Sites.Should().BeEmpty();
    }

    [Fact]
    public void TheCorpusReadsSpyFilesAndSpecBlocksAndSetsAsideWhatIsExpectedToFail()
    {
        var root = Directory.CreateTempSubdirectory("blast-corpus").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "ok.spy"), BaseClass + "def main() -> None:\n    pass\n");
            File.WriteAllText(Path.Combine(root, "bad.spy"), "def main() -> None:\n    pass\n");
            File.WriteAllText(Path.Combine(root, "bad.error"), "SPY0000");
            Directory.CreateDirectory(Path.Combine(root, "multi"));
            File.WriteAllText(Path.Combine(root, "multi", "main.spy"), "def main() -> None:\n    pass\n");
            File.WriteAllText(Path.Combine(root, "multi", "lib.spy"), "x: int = 1\n");
            File.WriteAllText(Path.Combine(root, "multi", "main.error"), "SPY0000");
            // A file below a multi-file fixture's directory is part of that fixture.
            Directory.CreateDirectory(Path.Combine(root, "multi", "pkg"));
            File.WriteAllText(Path.Combine(root, "multi", "pkg", "mod.spy"), "x: int = 1\n");
            // Build output and crash-bundle copies are not programs of the corpus.
            Directory.CreateDirectory(Path.Combine(root, "obj"));
            File.WriteAllText(Path.Combine(root, "obj", "generated.spy"), "x: int = 1\n");
            Directory.CreateDirectory(Path.Combine(root, ".sharpy-crash", "1"));
            File.WriteAllText(Path.Combine(root, ".sharpy-crash", "1", "copy.spy"), "x: int = 1\n");
            File.WriteAllText(Path.Combine(root, "page.md"), string.Join("\n",
                "# Page", "",
                "```python",
                "class Base:", "    n: int", "", "    def __init__(self):", "        self.n = 1", "",
                "class Child(Base):", "    name: str", "", "    def __init__(self):",
                "        super().__init__()", "        self.name = \"a\"", "",
                "c = Child()", "print(c.name)",
                "```", "",
                "<!-- spec-sweep: error SPY0200 -->",
                "```python", "print(nope)", "```", "",
                "```text", "not a program", "```", ""));

            var items = Corpus.Enumerate("t", root, root).ToList();
            items.Select(i => i.Label).Should().Equal(
                "bad.spy", "multi/lib.spy", "multi/main.spy", "multi/pkg/mod.spy", "ok.spy", "page.md:3", "page.md:22");
            items.Where(i => i.SkipReason == Corpus.SkipExpectedError).Select(i => i.Label)
                .Should().Equal("bad.spy", "multi/lib.spy", "multi/main.spy", "multi/pkg/mod.spy");
            items.Single(i => i.Label == "page.md:22").SkipReason.Should().Be(Corpus.SkipFragment);

            // The block ends in loose statements, which a module cannot hold: it is still read, and
            // the site keeps its line in the page (fence line 3, `def __init__` on block line 10).
            var block = Runner.Run(items.Single(i => i.Label == "page.md:3"));
            block.Skipped.Should().BeNull();
            var site = block.Sites.Single();
            site.Subject.Should().Be("Child.__init__");
            site.Line.Should().Be(13);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ABlockOfASpecPageSeesThePreludesAboveItAndReportsOnlyItsOwnSites()
    {
        var root = Directory.CreateTempSubdirectory("blast-prelude").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "page.md"), string.Join("\n",
                "<!-- spec-sweep: prelude -->",
                "```python",
                "class Base:", "    n: int", "", "    def __init__(self):", "        self.n = 1", "",
                "class First(Base):", "    name: str", "", "    def __init__(self):",
                "        super().__init__()", "        self.name = \"a\"",
                "```", "",
                "```python",
                "class Second(Base):", "    name: str", "", "    def __init__(self):",
                "        super().__init__()", "        self.name = \"b\"",
                "```", ""));

            var outcomes = Corpus.Enumerate("t", root, root).Select(Runner.Run).ToList();

            outcomes.Select(o => o.Skipped).Should().Equal(null, null);
            // `Base` reaches the second block through the prelude; `First` is reported once, by
            // the block that declares it, and each site keeps its line in the page.
            outcomes[0].Sites.Select(s => (s.Subject, s.Line)).Should().Equal(("First.__init__", 12));
            outcomes[1].Sites.Select(s => (s.Subject, s.Line)).Should().Equal(("Second.__init__", 21));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AVariableOfAPreludeStaysAtModuleLevelWhenALaterBlockIsHoisted()
    {
        var root = Directory.CreateTempSubdirectory("blast-prelude-variable").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "page.md"), string.Join("\n",
                "<!-- spec-sweep: prelude -->",
                "```python",
                "limit: int = 3",
                "```", "",
                "```python",
                "def capped(n: int) -> int:", "    return n if n < limit else limit", "",
                "print(capped(5))",
                "```", ""));

            Corpus.Enumerate("t", root, root).Select(Runner.Run).Select(o => (o.Skipped, o.ErrorCode))
                .Should().Equal((null, null), (null, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LooseStatementsOfASpecBlockAreMovedWholeAndAConstantStaysWhereItIs()
    {
        var root = Directory.CreateTempSubdirectory("blast-hoist").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "page.md"), string.Join("\n",
                "```python",
                "text = \"\"\"", "several", "lines", "\"\"\"",
                "print(text)",
                "```", "",
                "```python",
                "const LIMIT: int = 3", "",
                "def capped(n: int) -> int:", "    return n if n < LIMIT else LIMIT", "",
                "print(capped(5))",
                "```", ""));

            Corpus.Enumerate("t", root, root).Select(Runner.Run).Select(o => (o.Skipped, o.ErrorCode))
                .Should().Equal((null, null), (null, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The tool reads a specification block with its own reader, not the sweep's
    /// (<c>build_tools/spec_blocks.py</c>). This holds the two together: the sweep's allowlist is
    /// every unmarked block that does not build, and analysis, which builds nothing, may refuse no
    /// more than those.
    /// </summary>
    [Fact]
    public void TheSpecificationBlocksTheToolCannotCompileAreNoMoreThanTheSweepAllows()
    {
        var spec = Path.Combine(FixtureRoots.RepositoryRoot, "docs", "language_specification");
        var allowed = File.ReadLines(Path.Combine(FixtureRoots.RepositoryRoot, "build_tools", "spec_blocks_allowlist.txt"))
            .Count(line => line.Length > 0 && !line.StartsWith('#'));

        var outcomes = Corpus.Enumerate("spec", spec, spec).Select(Runner.Run).ToList();

        outcomes.Count(o => o.Skipped == null).Should().BeGreaterThan(500, "positive control: the pages are read");
        outcomes.Count(o => o.ErrorCode != null).Should().BeLessThanOrEqualTo(allowed);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("99999999999", 2)]
    [InlineData("-3", 2)]
    [InlineData("many", 2)]
    public void AnExamplesCountThatIsNotACountIsAUsageError(string count, int exitCode)
    {
        var root = Directory.CreateTempSubdirectory("blast-cli").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "ok.spy"), "def main() -> None:\n    pass\n");
            // Everything else about the call is valid, so only the count decides the exit code.
            Program.Main(new[]
            {
                "run", "R-RO", "--repo", root, "--area", "t=.", "--md", Path.Combine(root, "out.md"), "--examples", count,
            }).Should().Be(exitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OnlyTheSweepsOwnMarkersSetASpecBlockAside()
    {
        var root = Directory.CreateTempSubdirectory("blast-markers").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "page.md"), string.Join("\n",
                "<!-- spec-sweep: fragment -->", "```python", "x", "```", "",
                "<!-- spec-sweep: error SPY0200 -->", "```python", "x", "```", "",
                // Not a marker of the sweep (no code): the sweep compiles the block, and so does the tool.
                "<!-- spec-sweep: error -->", "```python", "x", "```", "",
                "<!-- spec-sweep: output -->", "```python", "x", "```", ""));

            Corpus.Enumerate("t", root, root).Select(i => i.SkipReason)
                .Should().Equal(Corpus.SkipFragment, Corpus.SkipFragment, null, null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The corpus reader decides which fixtures are expected to fail from their sidecars, as the
    /// test suite's own discovery does. The two share no code, so this holds them together.
    /// </summary>
    [Fact]
    public void TheCorpusSetsAsideExactlyTheFixturesTheTestSuiteExpectsToFailOrSkips()
    {
        foreach (var root in FixtureRoots.All)
        {
            var setAside = Corpus.Enumerate("t", root.Path, root.Path)
                .Where(i => i.SkipReason == Corpus.SkipExpectedError).Select(i => i.Path!).ToHashSet();

            var expected = new HashSet<string>();
            foreach (var fixture in FixtureDiscoveryHelper.DiscoverFixtures(root.Path, includeSkipped: true)
                         .Where(f => f.ErrorFile != null || f.IsSkipped))
            {
                if (!fixture.IsMultiFile)
                    expected.Add(Path.GetFullPath(fixture.SpyFilePath));
                else
                    expected.UnionWith(Directory.EnumerateFiles(fixture.SpyFilePath, "*.spy", SearchOption.AllDirectories)
                        .Select(Path.GetFullPath));
            }

            expected.Should().NotBeEmpty("positive control: both roots hold fixtures that expect an error");
            setAside.Except(expected).Should().BeEmpty("the corpus sets aside only what the suite expects to fail");
            expected.Except(setAside).Should().BeEmpty("the corpus sets aside all the suite expects to fail");
        }
    }

    [Fact]
    public void TheReportCountsSitesByAreaAndVerdictAndNamesEveryOne()
    {
        var refusedSource = BaseClass + "class A(Base):\n    name: str\n\n    def __init__(self):\n"
            + "        super().__init__()\n        self.name = \"a\"\n\ndef main() -> None:\n    pass\n";
        var outcomes = new[]
        {
            Runner.Run(new CorpusItem("one", "one/a.spy", refusedSource, null, 0, null)),
            Runner.Run(new CorpusItem("one", "one/b.spy", refusedSource, null, 0, Corpus.SkipExpectedError)),
            Runner.Run(new CorpusItem("two", "two/c.spy", BaseClass + "def main() -> None:\n    pass\n", null, 0, null)),
        };
        var report = new BlastReport
        {
            Probe = new SuperCallOrderProbe(),
            Commit = "abc1234",
            AreaOrder = new[] { "one", "two" },
            Outcomes = outcomes,
        };

        using var json = JsonDocument.Parse(report.ToJson());
        var areas = json.RootElement.GetProperty("areas").EnumerateArray().ToList();
        areas[0].GetProperty("programs").GetInt32().Should().Be(2);
        areas[0].GetProperty("analysed").GetInt32().Should().Be(1);
        areas[0].GetProperty("verdicts").GetProperty("refused").GetInt32().Should().Be(1);
        areas[1].GetProperty("verdicts").GetProperty("refused").GetInt32().Should().Be(0);
        var site = json.RootElement.GetProperty("sites").EnumerateArray().Single();
        site.GetProperty("program").GetString().Should().Be("one/a.spy");
        site.GetProperty("coords").GetProperty("fix").GetString().Should().Be("move");

        var markdown = report.ToMarkdown(examples: 3);
        markdown.Should().Contain("# Blast radius of R-RO")
            .And.Contain("| one | 2 | 1 | 1 | 1 | 0 | 0 | 0 | 0 |")
            .And.Contain("| **all** | 3 | 2 | 1 | 1 | 0 | 0 | 0 | 0 |")
            .And.Contain("| refused | base: sharpy, fix: move, slots: fields | 1 | 1 |")
            .And.Contain("A.__init__: name: str (move)")
            .And.Contain("| one | a fixture that is expected to fail | 1 |");
    }

    /// <summary>A probe whose one verdict and one axis value are set per test.</summary>
    private sealed class FixedProbe(string verdict, string axis, string value) : IRuleProbe
    {
        public string Rule => "R-TEST";
        public string Title => "test";
        public string SiteNoun => "module";
        public IReadOnlyList<VerdictInfo> Verdicts { get; } = new VerdictInfo[] { new("hit", "") };
        public IReadOnlyList<AxisInfo> Axes { get; } = new AxisInfo[] { new("size", "", new[] { ("small", "") }) };
        public IReadOnlyList<string> Limits { get; } = Array.Empty<string>();

        public IEnumerable<ProbeSite> Probe(ProbeInput input)
        {
            yield return new ProbeSite
            {
                Subject = "m",
                Line = 1,
                Verdict = verdict,
                Coords = new Dictionary<string, string> { [axis] = value },
            };
        }
    }

    [Theory]
    [InlineData("hit", "size", "small", true)]
    [InlineData("miss", "size", "small", false)]
    [InlineData("hit", "weight", "small", false)]
    [InlineData("hit", "size", "large", false)]
    public void ARunRefusesASiteWhoseVerdictOrCoordinateTheProbeDidNotDeclare(
        string verdict, string axis, string value, bool declared)
    {
        // A report prints only what a probe declares, so an undeclared site would be counted and
        // never explained. The runner holds every probe to this, over every program it reads.
        var run = () => new ProbeRunner(new FixedProbe(verdict, axis, value))
            .Run(new CorpusItem("t", "t", "def main() -> None:\n    pass\n", null, 0, null));

        if (declared)
            run().Sites.Should().ContainSingle();
        else
            run.Should().Throw<InvalidOperationException>().WithMessage("*does not declare*");
    }

    private static IReadOnlyList<ProbeSite> Analyse(string source)
    {
        var outcome = Runner.Run(new CorpusItem("test", "test", source, null, 0, null));
        outcome.Skipped.Should().BeNull("the test program must compile today");
        return outcome.Sites;
    }
}
