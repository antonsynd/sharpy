using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Tests.Conformance;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// The per-result output ceiling and the compile-path mute (#2438, P77b D8). That xUnit really marks a test failed from
/// <see cref="IntegrationTestBase.Dispose"/> is the mutation in the commit that added this file: an un-muted sweep's own
/// test goes red. These facts pin what the wrapper counts, drops and fails on.
/// </summary>
public class TestResultOutputTests
{
    private const string Program = "def main() -> None:\n    print(1)\n";

    private static readonly int LineCost = Environment.NewLine.Length;

    private sealed class RecordingOutput : ITestOutputHelper
    {
        public List<string> Lines { get; } = new();

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }

    private sealed class Probe : IntegrationTestBase
    {
        public bool Released { get; private set; }

        public Probe(ITestOutputHelper output, bool mute) : base(output) => MuteCompilePath = mute;

        public bool Compile(string arm)
        {
            if (arm == "single")
                return CompileAndExecute(Program, "main.spy").Success;

            var dir = Directory.CreateTempSubdirectory("sharpy-result-output-").FullName;
            try
            {
                var entry = Path.Combine(dir, "main.spy");
                File.WriteAllText(entry, Program);
                return (arm == "project" ? CompileAndExecuteProject(dir, "main.spy") : CompileAndExecuteEntryFile(entry)).Success;
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        public void Write(string message) => Output.WriteLine(message);

        public string FailureOf(string source, int maxCharacters = 600) => CompileAndExecute(source, "main.spy").FailureSummary(maxCharacters);

        public static string SummaryOf(string stderr = "", bool timedOut = false, Exception? exception = null)
            => new ExecutionResult { StandardError = stderr, TimedOut = timedOut, Exception = exception }.FailureSummary();

        protected override void DisposeTestResources() => Released = true;
    }

    private static string Line(long charactersIncludingNewline) => new('x', (int)charactersIncludingNewline - LineCost);

    [Fact]
    public void AWriteAtTheCeiling_Passes_AndPrints()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded);

        output.WriteLine(Line(TestResultOutput.CharacterCeiling));

        Assert.Equal(TestResultOutput.CharacterCeiling, output.CharactersWritten);
        Assert.Single(recorded.Lines);
        output.FailIfOverCeiling();
    }

    [Fact]
    public void AWritePastTheCeiling_Fails_AndIsDropped()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded);
        output.WriteLine(Line(TestResultOutput.CharacterCeiling));

        output.WriteLine("x");
        output.WriteLine("x");

        Assert.Equal(TestResultOutput.CharacterCeiling + 2 * (1 + LineCost), output.CharactersWritten);
        // The line at the ceiling and one note: the writes past it reach neither the result nor the heap.
        Assert.Equal(2, recorded.Lines.Count);
        Assert.Contains("ceiling", recorded.Lines[1], StringComparison.Ordinal);
        var failure = Assert.ThrowsAny<XunitException>(output.FailIfOverCeiling);
        Assert.Contains("#2438", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFormatOverload_CountsWhatItFormats()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded);

        output.WriteLine("{0}-{1}", "a", "b");

        Assert.Equal("a-b", Assert.Single(recorded.Lines));
        Assert.Equal(3 + LineCost, output.CharactersWritten);
    }

    [Fact]
    public void MutedCompilePathWrites_NeitherCount_NorPrint()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded) { MuteCompilePath = true };

        output.CompilePath.WriteLine(Line(TestResultOutput.CharacterCeiling * 2));
        output.CompilePath.WriteLine("{0}", "formatted");

        Assert.Empty(recorded.Lines);
        Assert.Equal(0, output.CharactersWritten);
        output.FailIfOverCeiling();

        // The same two writes un-muted, on the same output, which shows the mute is what kept them out.
        output.MuteCompilePath = false;
        output.CompilePath.WriteLine("line");
        output.CompilePath.WriteLine("{0}", "formatted");
        Assert.Equal(new[] { "line", "formatted" }, recorded.Lines);
        Assert.Equal(4 + 9 + 2 * LineCost, output.CharactersWritten);
    }

    [Fact]
    public void TheMute_LeavesATestsOwnWritesAlone()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded) { MuteCompilePath = true };

        output.WriteLine("own");

        Assert.Equal("own", Assert.Single(recorded.Lines));
        Assert.Equal(3 + LineCost, output.CharactersWritten);
    }

    [Fact]
    public void TheBaseClass_FailsWhenTheTestEnds_AfterReleasingTheSubclassResources()
    {
        var overCeiling = new Probe(new RecordingOutput(), mute: false);
        overCeiling.Write(Line(TestResultOutput.CharacterCeiling + 1));

        Assert.ThrowsAny<XunitException>(overCeiling.Dispose);
        Assert.True(overCeiling.Released);

        var underCeiling = new Probe(new RecordingOutput(), mute: false);
        underCeiling.Write(Line(TestResultOutput.CharacterCeiling));
        underCeiling.Dispose();
        Assert.True(underCeiling.Released);
    }

    /// <summary>A run has reasons no compile reaches: a timeout with what the program wrote so far, and an exception out of
    /// the harness. Each is said once: a crash's stderr is already inside its compile error.</summary>
    [Fact]
    public void FailureSummary_SaysEachOtherReason_Once()
    {
        Assert.Equal("timed out; stderr: partial", Probe.SummaryOf(stderr: "partial\n", timedOut: true));
        Assert.Equal("harness threw", Probe.SummaryOf(exception: new InvalidOperationException("harness threw")));

        var crash = new Probe(new RecordingOutput(), mute: true).FailureOf("def main() -> None:\n    raise ValueError(\"x\")\n");
        Assert.DoesNotContain("stderr:", crash, StringComparison.Ordinal);
    }

    /// <summary>What a muted class has left to say why: a refusal's code, a throw's type, and a bound on either.</summary>
    [Fact]
    public void FailureSummary_NamesTheCodeOrTheThrow_WithinItsBound()
    {
        var probe = new Probe(new RecordingOutput(), mute: true);

        Assert.Contains("SPY0200", probe.FailureOf("def main() -> None:\n    print(no_such_name)\n"), StringComparison.Ordinal);
        Assert.Contains("ValueError", probe.FailureOf("def main() -> None:\n    raise ValueError(\"x\")\n"), StringComparison.Ordinal);
        Assert.Equal("", probe.FailureOf(Program));

        var bounded = probe.FailureOf("def main() -> None:\n    print(no_such_name)\n", maxCharacters: 10);
        Assert.Equal(11, bounded.Length);
        Assert.EndsWith("…", bounded, StringComparison.Ordinal);
    }

    /// <summary>Just below the 247 IntegrationTestBase subclasses measured when this fact was written. A literal, so that a
    /// scan which finds no subclass cannot pass.</summary>
    private const int SubclassFloor = 240;

    /// <summary>The ceiling binds what goes through <see cref="IntegrationTestBase.Output"/> and its verdict is in the base's
    /// <see cref="IntegrationTestBase.Dispose"/>. C# leaves a subclass two ways round: keep the constructor's raw helper in
    /// a field and write through it, or re-implement <see cref="IDisposable"/> so the runner calls another Dispose. A class
    /// off the base has no ceiling at all, which is a different seam and is not this scan's.</summary>
    [Fact]
    public void NoIntegrationTestBaseSubclass_WritesAroundTheCeiling_OrDropsItsVerdict()
    {
        var sln = PythonInterpreter.FindRepoFile("sharpy.sln")
            ?? throw new FileNotFoundException("sharpy.sln not found above the test binary.");
        var repoRoot = Path.GetDirectoryName(sln)!;

        var (routes, subclasses) = ScanCeilingRoutes(PythonOracleSkipCensusTests.TestSourceFiles(repoRoot)
            .Select(file => (Path.GetRelativePath(repoRoot, file), File.ReadAllText(file))));

        Assert.True(subclasses >= SubclassFloor, $"The scan found {subclasses} subclasses (floor {SubclassFloor}).");
        Assert.True(routes.Count == 0, $"{routes.Count} way(s) round the ceiling (#2438):\n" + string.Join("\n", routes));
    }

    /// <summary>The scan fires on each route, through a second level of inheritance and across a partial class's files,
    /// and leaves the base's own spelling alone. The non-subclass is the control: the same field there is the other seam's.</summary>
    [Fact]
    public void TheCeilingScan_FiresOnEachRoute_AndLeavesTheAllowedShapesAlone()
    {
        const string Broken = """
            namespace N;
            public abstract class Middle : IntegrationTestBase { protected Middle(ITestOutputHelper o) : base(o) { } }
            public class KeepsTheHelper : Middle
            {
                private readonly ITestOutputHelper _raw;
                public KeepsTheHelper(ITestOutputHelper output) : base(output) { _raw = output; }
            }
            public class KeepsItInAnExpression : IntegrationTestBase
            {
                private readonly ITestOutputHelper _raw;
                public KeepsItInAnExpression(ITestOutputHelper output) : base(output) { this._raw = (output ?? throw new Exception()); }
            }
            public class CapturesIt(ITestOutputHelper output) : IntegrationTestBase(output)
            {
                public void Write() => output.WriteLine("x");
            }
            public partial class Reimplements : IntegrationTestBase { public Reimplements(ITestOutputHelper o) : base(o) { } }
            public partial class Reimplements : System.IDisposable { }
            public class Hides : IntegrationTestBase
            {
                public Hides(ITestOutputHelper o) : base(o) { }
                public new void Dispose() { }
            }
            public class Explicit : Middle
            {
                public Explicit(ITestOutputHelper o) : base(o) { }
                void System.IDisposable.Dispose() { }
            }
            public class ThroughTheBase : IntegrationTestBase
            {
                private readonly ITestOutputHelper _output;
                private readonly Other _other;
                public ThroughTheBase(ITestOutputHelper output) : base(output) { _output = Output; _other = new Other(output); }
                protected override void DisposeTestResources() { }
                public void Dispose(bool disposing) { }
            }
            public class OffTheBase : System.IDisposable
            {
                private readonly ITestOutputHelper _raw;
                public OffTheBase(ITestOutputHelper output) { _raw = output; }
                public void Dispose() { }
            }
            """;

        var (routes, subclasses) = ScanCeilingRoutes(new[] { ("control.cs", Broken) });

        Assert.Equal(8, subclasses);
        foreach (var named in new[] { "KeepsTheHelper", "KeepsItInAnExpression", "CapturesIt", "Reimplements", "Hides", "Explicit" })
            Assert.Single(routes, r => r.Contains("N." + named + ":", StringComparison.Ordinal));
        Assert.Equal(6, routes.Count);
    }

    private static (List<string> Routes, int Subclasses) ScanCeilingRoutes(IEnumerable<(string Path, string Source)> sources)
    {
        static string Simple(TypeSyntax type) => type switch
        {
            QualifiedNameSyntax q => Simple(q.Right),
            AliasQualifiedNameSyntax a => Simple(a.Name),
            SimpleNameSyntax s => s.Identifier.ValueText,
            _ => type.ToString(),
        };

        static IEnumerable<string> BaseNames(ClassDeclarationSyntax c)
            => c.BaseList?.Types.Select(t => Simple(t.Type)) ?? Enumerable.Empty<string>();

        // A class's parts by its qualified name: `partial` can put the base list in another file than the member.
        var parts = sources
            .SelectMany(s => CSharpSyntaxTree.ParseText(s.Source, path: s.Path).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .GroupBy(QualifiedClassName, StringComparer.Ordinal)
            .ToList();

        var subclassNames = new HashSet<string>(StringComparer.Ordinal) { nameof(IntegrationTestBase) };
        var subclasses = new List<IGrouping<string, ClassDeclarationSyntax>>();
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var group in parts.Except(subclasses).ToList())
            {
                if (group.SelectMany(BaseNames).Any(subclassNames.Contains))
                {
                    subclasses.Add(group);
                    subclassNames.Add(group.First().Identifier.ValueText);
                    grew = true;
                }
            }
        }

        var routes = new List<string>();
        foreach (var group in subclasses)
        {
            foreach (var part in group)
            {
                var at = $"{group.Key}: {part.SyntaxTree.FilePath}:{part.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                if (BaseNames(part).Contains(nameof(IDisposable)))
                    routes.Add($"{at} re-implements IDisposable, so the runner never calls the base's Dispose and its verdict");
                if (part.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == nameof(IDisposable.Dispose) && m.ParameterList.Parameters.Count == 0))
                    routes.Add($"{at} declares its own Dispose: release resources in DisposeTestResources");
                if (KeepsTheRawHelper(part))
                    routes.Add($"{at} keeps the constructor's raw ITestOutputHelper, and what it writes there has no ceiling: use Output");
            }
        }

        return (routes, subclasses.Count);

        static bool IsHelper(ParameterSyntax p) => p.Type != null && Simple(p.Type) == nameof(ITestOutputHelper);

        // An identifier naming the helper outside any argument list: `_raw = output`, `_raw = (output ?? x)`, but not `new Other(output)`.
        static bool NamesIt(SyntaxNode value, HashSet<string> names)
            => value.DescendantNodesAndSelf(n => n is not ArgumentListSyntax)
                .OfType<IdentifierNameSyntax>()
                .Any(id => names.Contains(id.Identifier.ValueText));

        static bool KeepsTheRawHelper(ClassDeclarationSyntax part)
        {
            foreach (var ctor in part.Members.OfType<ConstructorDeclarationSyntax>())
            {
                var names = ctor.ParameterList.Parameters.Where(IsHelper).Select(p => p.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
                SyntaxNode? body = (SyntaxNode?)ctor.Body ?? ctor.ExpressionBody;
                if (names.Count > 0 && body != null
                    && body.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>().Any(a => NamesIt(a.Right, names)))
                    return true;
            }

            // A primary constructor's parameter is captured wherever a member names it; the base list's argument is not a capture.
            var primary = part.ParameterList?.Parameters.Where(IsHelper).Select(p => p.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
            return primary is { Count: > 0 }
                && part.Members.SelectMany(m => m.DescendantNodes()).OfType<IdentifierNameSyntax>().Any(id => primary.Contains(id.Identifier.ValueText));
        }
    }

    private static string QualifiedClassName(ClassDeclarationSyntax c)
    {
        var ns = c.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString());
        var nesting = c.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().Reverse().Select(n => n.Identifier.ValueText);
        return string.Join('.', ns.Concat(nesting));
    }

    [Theory]
    [InlineData("single")]
    [InlineData("project")]
    [InlineData("entry-file")]
    public void TheBaseClass_RoutesEachCompilesOutputThroughTheMute(string arm)
    {
        var muted = new RecordingOutput();
        Assert.True(new Probe(muted, mute: true).Compile(arm));
        Assert.Empty(muted.Lines);

        var unmuted = new RecordingOutput();
        Assert.True(new Probe(unmuted, mute: false).Compile(arm));
        Assert.Contains(unmuted.Lines, line => line.Contains("Generated C#", StringComparison.Ordinal));
    }
}
