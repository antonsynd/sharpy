using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using static Sharpy.Compiler.Tests.Conformance.LetEmissionInvarianceTests.Instrument;

// The instrument runs as ShardCount classes Shard0..Shard7 plus one Controls class, all in a NAMESPACE
// named LetEmissionInvarianceTests, so `~LetEmissionInvarianceTests` and the Conformance CI shard select
// every row unchanged (#2179). One class would serialise the corpus Theory: xUnit v2 never runs two
// rows of one class concurrently, and as one class it was the suite's longest pole (576 s at one thread).
namespace Sharpy.Compiler.Tests.Conformance.LetEmissionInvarianceTests;

/// <summary>
/// Differential instrument for #1974 Stage 1 (P21a Phase 6), and the acceptance oracle 21b's codemod
/// reuses: <b>prefixing <c>let</c> at a statement store that already DECLARES changes nothing in the
/// emitted C#.</b>
///
/// <para>For every single-file fixture under <c>TestFixtures/</c> that compiles, the instrument
/// derives a TWIN from the fixture's own <see cref="SemanticInfo"/>: <c>let </c> is inserted before
/// every function-scope <see cref="Assignment"/> with <c>Operator == Assign</c> whose EVERY
/// identifier target (the single name, each tuple element, the starred name) the checker recorded
/// as <see cref="TargetBindingKind.Declares"/> — a tuple with one <c>Rebinds</c> element is left
/// alone, since <c>let</c> would make that element fresh — and before every function-scope non-const
/// <see cref="VariableDeclaration"/> with an initializer. Both are compiled through the production
/// single-file path and the Roslyn-normalised C# must be byte-identical. A declaring store and its
/// <c>let</c> twin reach the same emitter arm by construction (Design Decision 1): any difference is
/// a finding — the emitter deciding something from a fact other than <c>TargetBinding</c>.</para>
///
/// <para><b>Why text insertion, not unparsing.</b> The twin is the fixture's source with
/// <c>let </c> inserted at each chosen statement's 1-based <c>LineStart/ColumnStart</c>. Unparsing
/// the AST would re-lay-out the whole file (comments, blank lines, wrapping), moving line numbers,
/// and the emitted C# carries <c>#line</c> directives — every fixture would differ for a reason that
/// has nothing to do with <c>let</c>. Insertion touches only the prefixed lines and keeps every line
/// number. Its one hazard, a wrong insertion point, is closed by re-parsing the twin: the number of
/// <c>IsLet</c> store nodes must grow by exactly the number of insertions, else the cell fails as
/// an instrument error, not a finding.</para>
///
/// <para><b>Positive control</b> (<see cref="PositiveControl_LetAtAWriteThroughSite_ChangesTheCSharpAndTheOutput"/>):
/// the same insertion at a site the checker records as <c>Rebinds</c> — the loop accumulator of
/// <c>type_shorthand/list_shorthand</c> — must change the C# AND the printed output (15 → 0), so
/// the comparison can see a real difference. <see cref="SiteFinder_OnTheControlFixture_PicksTheDeclaringStoresOnly"/>
/// pins the site finder on the same fixture.</para>
///
/// <para><b>Ratchet.</b> <c>Conformance/let-emission-invariance-allowlist.txt</c> lists fixture
/// stems whose twin is known to differ, each citing an issue; it ships EMPTY. An unlisted differing
/// fixture fails, and a listed fixture whose twin is now identical fails too (drain on fix).</para>
///
/// <para>Not in scope: multi-file fixtures (the twin rule is single-file, driven by one file's
/// <c>SemanticInfo</c>) and fixtures with an <c>.error</c> sidecar (they do not compile). Both are
/// excluded at discovery; see <see cref="Census_DiscoveryExclusionsAreCountedAndStated"/>.</para>
///
/// <para><b>Layout.</b> The corpus Theory <c>LetTwin_EmitsByteIdenticalCSharp</c> is sharded over
/// <see cref="LetTwinShard"/>'s subclasses by the fixture-shard function
/// (<see cref="FileBasedIntegrationTestsBase.ShardOf"/>); this class holds the controls and the census,
/// which run once. The per-row ratchet checks (an allowlisted stem with no site, or whose twin is
/// identical again) run in the one shard that owns the row; the stale-entry check is the census's,
/// here.</para>
/// </summary>
public class Controls : FileBasedIntegrationTestsBase
{
    protected override string FixturesPath => FixturesPathValue;

    public Controls(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// The comparison can see a difference: <c>let</c> forced at a WRITE-THROUGH site (the loop
    /// accumulator <c>total = total + item</c>, recorded <c>Rebinds</c>) makes the accumulator a
    /// fresh loop-body binding, so the C# differs and the function returns 0 instead of 15.
    /// </summary>
    [Fact]
    public void PositiveControl_LetAtAWriteThroughSite_ChangesTheCSharpAndTheOutput()
    {
        var fixture = FixtureByName(ControlFixture);
        var source = File.ReadAllText(fixture.SpyFilePath);
        var fileName = Path.GetFileName(fixture.SpyFilePath);

        var original = Compile(source, fileName, FeatureFlags.None);
        original.Success.Should().BeTrue(Errors(original));

        var writeThrough = FunctionScopeStores(original.Ast!)
            .OfType<Assignment>()
            .Where(a => a.Operator == AssignmentOperator.Assign && a.Target is Identifier { Name: "total" })
            .ToList();
        writeThrough.Should().ContainSingle("the control fixture has one accumulator store");
        original.SemanticInfo!.GetTargetBinding(writeThrough[0].Target)?.Kind
            .Should().Be(TargetBindingKind.Rebinds, "the control site must be a write-through store");

        var twin = InsertLet(source, new[] { new Site(writeThrough[0].LineStart, writeThrough[0].ColumnStart, "Assignment") });
        AssertTwinParsesAsIntended(ControlFixture, source, twin, 1);

        var twinResult = Compile(twin, fileName, FeatureFlags.None);
        twinResult.Success.Should().BeTrue(Errors(twinResult));
        Comparable(twinResult.GeneratedCSharp!).Should().NotBe(
            Comparable(original.GeneratedCSharp!), "let at a write-through site must change the emitted C# code, not only #line columns");

        var originalRun = CompileAndExecute(source, fileName);
        var twinRun = CompileAndExecute(twin, fileName);
        originalRun.Success.Should().BeTrue();
        twinRun.Success.Should().BeTrue();
        originalRun.StandardOutput.Trim().Should().Be("15");
        twinRun.StandardOutput.Trim().Should().Be("0", "the let accumulator is fresh in each loop iteration");
    }

    /// <summary>
    /// The site finder on the control fixture: the two function-scope annotated declarations are
    /// sites; the write-through accumulator (<c>Rebinds</c>) is not.
    /// </summary>
    [Fact]
    public void SiteFinder_OnTheControlFixture_PicksTheDeclaringStoresOnly()
    {
        var fixture = FixtureByName(ControlFixture);
        var original = Compile(File.ReadAllText(fixture.SpyFilePath), Path.GetFileName(fixture.SpyFilePath), FeatureFlags.None);
        original.Success.Should().BeTrue(Errors(original));

        TwinSites(original.Ast!, original.SemanticInfo!)
            .Select(s => $"L{s.Line}:C{s.Column} {s.Kind}")
            .Should().Equal("L3:C5 VariableDeclaration", "L9:C5 VariableDeclaration");
    }

    /// <summary>
    /// The comparison's one exemption is narrow: it forgets the source COLUMNS of a <c>#line</c>
    /// span directive and nothing else — a different line number or a different code line still
    /// differs.
    /// </summary>
    [Fact]
    public void Comparison_ForgetsOnlyLineDirectiveColumns()
    {
        static string Program(string line, string code)
            => "class C\n{\n    void M()\n    {\n#line " + line + " 12 \"a.spy\"\n        " + code + "\n#line default\n    }\n}\n";

        var original = Program("(3, 5) - (3, 19)", "int x = 1;");
        Comparable(Program("(3, 5) - (3, 23)", "int x = 1;")).Should().Be(Comparable(original), "columns only");
        Comparable(Program("(4, 5) - (4, 19)", "int x = 1;")).Should().NotBe(Comparable(original), "a line number");
        Comparable(Program("(3, 5) - (3, 19)", "x = 1;")).Should().NotBe(Comparable(original), "a code line");
    }

    /// <summary>
    /// Every fixture under the root is either compared or excluded for a stated reason, and the
    /// allowlist names only comparable fixtures. The exclusion counts are printed for the record.
    /// </summary>
    [Fact]
    public void Census_DiscoveryExclusionsAreCountedAndStated()
    {
        var all = AllFixtures().ToList();
        var multiFile = all.Count(f => f.IsMultiFile);
        var error = all.Count(f => !f.IsMultiFile && f.ErrorFile != null);
        var comparable = ComparableFixtures().Select(f => f.TestName).ToHashSet(StringComparer.Ordinal);
        Output.WriteLine($"LETINV-CENSUS discovered={all.Count} comparable={comparable.Count} "
            + $"excluded-multi-file={multiFile} excluded-error-sidecar={error}");

        comparable.Count.Should().Be(all.Count - multiFile - error);
        comparable.Should().Contain(ControlFixture);
        LoadAllowlist().Keys.Should().OnlyContain(k => comparable.Contains(k),
            "every allowlist entry must name a comparable fixture");
    }
}

/// <summary>
/// The corpus cell of the instrument (see <see cref="Controls"/>). Deliberately NOT a
/// <see cref="FileBasedIntegrationTestsBase"/>: that base declares a <c>[Fact]</c>, which every shard
/// class would replicate.
/// </summary>
public abstract class LetTwinShard
{
    protected readonly ITestOutputHelper Output;

    protected LetTwinShard(ITestOutputHelper output)
    {
        Output = output;
    }

    protected void RunCell(string testName)
    {
        var fixture = FixtureByName(testName);
        var source = File.ReadAllText(fixture.SpyFilePath);
        var fileName = Path.GetFileName(fixture.SpyFilePath);
        var features = FeaturesOf(fixture);

        var original = Compile(source, fileName, features);
        original.Success.Should().BeTrue(
            $"{testName} has no .error sidecar, so the instrument expects it to compile. A fixture that "
            + $"does not compile here is an instrument fault, not a skipped cell:\n{Errors(original)}");
        original.GeneratedCSharp.Should().NotBeNull(testName);
        original.Ast.Should().NotBeNull(testName);
        original.SemanticInfo.Should().NotBeNull(testName);

        var sites = TwinSites(original.Ast!, original.SemanticInfo!);
        var allowlisted = LoadAllowlist().TryGetValue(testName, out var cite);
        if (sites.Count == 0)
        {
            Output.WriteLine($"LETINV {testName} no-site");
            allowlisted.Should().BeFalse($"{testName} is allowlisted ({cite}) but has no let site: delete its entry");
            return;
        }

        var twin = InsertLet(source, sites);
        AssertTwinParsesAsIntended(testName, source, twin, sites.Count);

        var twinResult = Compile(twin, fileName, features);
        var verdict = twinResult.Success && twinResult.GeneratedCSharp != null
            && Comparable(twinResult.GeneratedCSharp) == Comparable(original.GeneratedCSharp!)
            ? "identical"
            : "differs";
        Output.WriteLine($"LETINV {testName} {verdict} sites={sites.Count}");

        if (verdict == "identical")
        {
            allowlisted.Should().BeFalse(
                $"{testName}'s let twin now emits identical C# — {cite} is fixed for it: delete its line from "
                + $"Conformance/{AllowlistFileName}");
            return;
        }

        if (allowlisted)
            return;

        var detail = twinResult.Success
            ? FirstDifference(Comparable(original.GeneratedCSharp!), Comparable(twinResult.GeneratedCSharp!))
            : "the twin does not compile:\n" + Errors(twinResult);
        Assert.Fail(
            $"{testName}: prefixing `let` at {sites.Count} declaring store(s) "
            + $"({string.Join(", ", sites.Select(s => $"L{s.Line}:C{s.Column} {s.Kind}"))}) changed the emitted C#. "
            + $"A declaring store and its let twin must reach the same emitter arm. {detail}");
    }
}

public class Shard0 : LetTwinShard
{
    public Shard0(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => CellsOfShard(0);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void LetTwin_EmitsByteIdenticalCSharp(string testName) => RunCell(testName);
}

public class Shard1 : LetTwinShard
{
    public Shard1(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => CellsOfShard(1);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void LetTwin_EmitsByteIdenticalCSharp(string testName) => RunCell(testName);
}

public class Shard2 : LetTwinShard
{
    public Shard2(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => CellsOfShard(2);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void LetTwin_EmitsByteIdenticalCSharp(string testName) => RunCell(testName);
}

public class Shard3 : LetTwinShard
{
    public Shard3(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => CellsOfShard(3);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void LetTwin_EmitsByteIdenticalCSharp(string testName) => RunCell(testName);
}

public class Shard4 : LetTwinShard
{
    public Shard4(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => CellsOfShard(4);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void LetTwin_EmitsByteIdenticalCSharp(string testName) => RunCell(testName);
}

public class Shard5 : LetTwinShard
{
    public Shard5(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => CellsOfShard(5);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void LetTwin_EmitsByteIdenticalCSharp(string testName) => RunCell(testName);
}

public class Shard6 : LetTwinShard
{
    public Shard6(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => CellsOfShard(6);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void LetTwin_EmitsByteIdenticalCSharp(string testName) => RunCell(testName);
}

public class Shard7 : LetTwinShard
{
    public Shard7(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => CellsOfShard(7);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void LetTwin_EmitsByteIdenticalCSharp(string testName) => RunCell(testName);
}

/// <summary>Corpus, site finding, twin construction, compilation and the allowlist, shared by <see cref="Controls"/> and the shards.</summary>
internal static class Instrument
{
    /// <summary>
    /// K. As one class the Theory took 576 s at one thread @ 116c6e29d, the suite's longest pole once
    /// the fixture corpus was sharded 8 ways (101 s at N=4); 8 shards match that corpus, so neither
    /// bounds an N=8 run. Adding a shard means adding a ShardN class; the totality test pins the count.
    /// </summary>
    internal const int ShardCount = 8;

    internal const string AllowlistFileName = "let-emission-invariance-allowlist.txt";
    internal const string ControlFixture = "type_shorthand/list_shorthand";

    internal static readonly string FixturesPathValue = FixtureRoots.CompilerTests.Path;

    // ================================================================
    // Corpus
    // ================================================================

    internal static IEnumerable<TestFixtureInfo> AllFixtures()
        => FixtureDiscoveryHelper.DiscoverFixtures(FixturesPathValue);

    /// <summary>Single-file fixtures without an <c>.error</c> sidecar: every one is expected to compile. A fresh walk.</summary>
    internal static IEnumerable<TestFixtureInfo> ComparableFixtures()
        => AllFixtures().Where(f => !f.IsMultiFile && f.ErrorFile == null);

    /// <summary>The unsharded corpus (a fresh walk): the totality test's independent side.</summary>
    internal static IEnumerable<object[]> ComparableFixtureNames()
        => ComparableFixtures().Select(f => new object[] { f.TestName });

    /// <summary>
    /// One walk shared by every shard's discovery and every cell's fixture lookup; a cell used to
    /// re-walk the whole corpus to find its own fixture.
    /// </summary>
    private static readonly Lazy<TestFixtureInfo[]> CachedComparable = new(() => ComparableFixtures().ToArray());

    internal static IEnumerable<object[]> CellsOfShard(int shard)
        => CachedComparable.Value
            .Where(f => FileBasedIntegrationTestsBase.ShardOf(
                Path.GetRelativePath(FixturesPathValue, f.SpyFilePath).Replace('\\', '/'), ShardCount) == shard)
            .Select(f => new object[] { f.TestName });

    internal static TestFixtureInfo FixtureByName(string testName)
        => CachedComparable.Value.Single(f => f.TestName == testName);

    // ================================================================
    // Site finding and twin construction
    // ================================================================

    internal sealed record Site(int Line, int Column, string Kind);

    /// <summary>
    /// The statement stores in a function scope: bodies of functions, property and event accessors,
    /// and every block nested inside them — not module level, not a type body (a class declared
    /// inside a function has a type body again; its methods are function scopes).
    /// </summary>
    internal static IEnumerable<Statement> FunctionScopeStores(Module module)
    {
        var stores = new List<Statement>();
        Walk(module, inFunction: false);
        return stores;

        void Walk(Node node, bool inFunction)
        {
            foreach (var child in node.GetChildNodes())
            {
                var childInFunction = child switch
                {
                    FunctionDef or PropertyDef or EventDef => true,
                    ClassDef or StructDef or InterfaceDef or UnionDef or EnumDef => false,
                    _ => inFunction,
                };

                if (inFunction && child is Assignment or VariableDeclaration)
                    stores.Add((Statement)child);

                Walk(child, childInFunction);
            }
        }
    }

    internal static List<Site> TwinSites(Module module, SemanticInfo info)
    {
        var sites = new List<Site>();
        foreach (var store in FunctionScopeStores(module))
        {
            switch (store)
            {
                case Assignment { Operator: AssignmentOperator.Assign, IsLet: false } assignment:
                    {
                        var names = new List<Identifier>();
                        if (TargetNames(assignment.Target, names)
                            && names.Count > 0
                            && names.All(id => info.GetTargetBinding(id)?.Kind == TargetBindingKind.Declares))
                        {
                            sites.Add(new Site(assignment.LineStart, assignment.ColumnStart, "Assignment"));
                        }

                        break;
                    }
                case VariableDeclaration { IsConst: false, IsLet: false, InitialValue: not null, Type: not null } declaration:
                    sites.Add(new Site(declaration.LineStart, declaration.ColumnStart, "VariableDeclaration"));
                    break;
            }
        }

        return sites;
    }

    /// <summary>The identifiers under a store target, or false when the target is not names-only (a member or index target cannot take <c>let</c>).</summary>
    internal static bool TargetNames(Expression target, List<Identifier> names)
    {
        switch (target)
        {
            case Identifier id:
                names.Add(id);
                return true;
            case TupleLiteral tuple:
                return tuple.Elements.All(e => TargetNames(e, names));
            case StarExpression star:
                return TargetNames(star.Operand, names);
            default:
                return false;
        }
    }

    internal static string InsertLet(string source, IEnumerable<Site> sites)
    {
        var lines = source.Split('\n');
        foreach (var site in sites.OrderByDescending(s => s.Line).ThenByDescending(s => s.Column))
        {
            var index = site.Line - 1;
            var line = lines[index];
            var at = site.Column - 1;
            if (at < 0 || at > line.Length)
                throw new InvalidOperationException($"site L{site.Line}:C{site.Column} is outside its line: '{line}'");

            // A parenthesised annotated name `(c): int = 5` reports its statement start at the NAME
            // (measured: C6, not the `(` at C5), so `let` goes before the opening parentheses.
            while (at > 0 && line[at - 1] == '(')
                at--;
            lines[index] = line.Substring(0, at) + "let " + line.Substring(at);
        }

        return string.Join("\n", lines);
    }

    /// <summary>The twin re-parses with exactly <paramref name="insertions"/> more <c>let</c> store nodes than the original.</summary>
    internal static void AssertTwinParsesAsIntended(string testName, string source, string twin, int insertions)
    {
        var before = LetStoreCount(source);
        var after = LetStoreCount(twin);
        after.Should().Be(before + insertions,
            $"{testName}: the twin must parse with one more let store per insertion — an instrument fault, "
            + "not a finding, if it does not");
    }

    internal static int LetStoreCount(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var parser = new global::Sharpy.Compiler.Parser.Parser(lexer.TokenizeAll(), NullLogger.Instance);
        var module = parser.ParseModule();
        return Descendants(module).Count(n => n is Assignment { IsLet: true } or VariableDeclaration { IsLet: true });
    }

    internal static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }

    // ================================================================
    // Compilation, allowlist, reporting
    // ================================================================

    internal static FeatureFlags FeaturesOf(TestFixtureInfo fixture)
        => fixture.Features.Count == 0 ? FeatureFlags.None : FeatureFlags.None.Enable(fixture.Features);

    /// <summary>
    /// The production single-file path (<c>CompilerApi.Compile</c>, a synthetic project of one file),
    /// with the options the integration harness uses, stopping after code generation.
    /// </summary>
    internal static CompileResult Compile(string source, string fileName, FeatureFlags features)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_letinv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, source);
            var api = new CompilerApi(NullLogger.Instance, new[] { SharpyCoreReference.Location });
            var options = new CompilerOptions
            {
                OutputType = "exe",
                TargetsTestHost = true,
                Features = features,
            };
            return api.Compile(source, options, path);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    internal static string Errors(CompileResult result)
        => string.Join("\n", result.Diagnostics
            .Where(d => d.Severity == global::Sharpy.Compiler.Diagnostics.CompilerDiagnosticSeverity.Error)
            .Select(d => $"{d.Code} L{d.Line}: {d.Message}"));

    internal static readonly System.Text.RegularExpressions.Regex LineSpanColumns = new(
        @"^(\s*#line\s*)\((\d+),\s*\d+\)\s*-\s*\((\d+),\s*\d+\)",
        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// The Roslyn-normalised C# with the SOURCE COLUMNS of every span-form <c>#line (l, c) - (l, c)</c>
    /// directive removed; line numbers, the generated-side offset, the file name and every code line
    /// are kept byte-for-byte. Inserting <c>let </c> keeps every line number but shifts the columns
    /// of the prefixed line by four — the twin's source differs there by construction, so those
    /// columns are position metadata of a different text, not an emission decision (measured: without
    /// this, every twin differed at the statement's end column alone).
    /// </summary>
    internal static string Comparable(string csharp)
        => LineSpanColumns.Replace(FileBasedIntegrationTestsBase.NormalizeCSharp(csharp), "$1($2) - ($3)");

    internal static string FirstDifference(string expected, string actual)
    {
        var a = expected.Split('\n');
        var b = actual.Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var x = i < a.Length ? a[i] : "<end>";
            var y = i < b.Length ? b[i] : "<end>";
            if (x != y)
                return $"First differing C# line {i + 1}:\n  original: {x.Trim()}\n  let twin: {y.Trim()}";
        }

        return "The normalised C# differs only in length.";
    }

    /// <summary>Stem → cited reason. <c>#</c> starts a comment only after whitespace, so the cite stays readable.</summary>
    internal static Dictionary<string, string> LoadAllowlist()
    {
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException(
                $"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var stem = split < 0 ? line : line.Substring(0, split).Trim();
            var cite = split < 0 ? "" : line.Substring(split + 2).Trim();
            if (!cite.Contains('#'))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must cite an issue (#N).");
            entries[stem] = cite;
        }

        return entries;
    }

    internal static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
            {
                var path = Path.Combine(dir, AllowlistFileName);
                return File.Exists(path) ? path : null;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }
}
