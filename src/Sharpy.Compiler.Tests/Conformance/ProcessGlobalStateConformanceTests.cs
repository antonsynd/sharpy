using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// A test that mutates process-global state runs alone (#2179). The current directory, the
/// console writers, the environment, the default culture and the time-zone cache are shared by
/// every test in a testhost, so a test that changes one must sit in an xUnit collection with
/// <c>DisableParallelization = true</c> — and that collection must be DEFINED in the test's own
/// project, because xUnit v2 reads <c>[CollectionDefinition]</c> only from the test assembly: a
/// definition anywhere else (as HeavyCompilation's was, in Sharpy.TestInfrastructure, from #679 until
/// #2179) leaves the collection an ordinary parallel one.
///
/// <para><b>What it scans.</b> A Roslyn walk over every <c>.cs</c> file of the five test projects
/// and Sharpy.TestInfrastructure (bin/obj and the uncompiled <c>Integration/TestFixtures</c>
/// excluded; <c>Spy/generated</c> included). A <i>mutation site</i> is a syntax match against a
/// LITERAL roster: an invocation of a BCL setter or of a product entry point that changes a global
/// when called in-process (<see cref="InvokedMutators"/>), a construction of such a product type
/// (<see cref="ConstructedMutators"/>), or an assignment to a global property
/// (<see cref="AssignedMutators"/>). Strings and comments never match. Each site is attributed to
/// its outermost enclosing type, whose <c>[Collection]</c> is read from every partial declaration
/// of that type in the same project (the spy-generated classes get theirs from a hand-written twin
/// in <c>Spy/SpyTestCollections.cs</c>).</para>
///
/// <para>A site is <b>isolated</b> when that collection is defined in the same project with
/// <c>DisableParallelization = true</c>; <b>exempt</b> when it is a row of <see cref="Exemptions"/>
/// (a row matching no site is stale and fails); otherwise a <b>violation</b>. Sites in
/// Sharpy.TestInfrastructure can only be exempt — no one collection isolates a shared library.
/// The positive control pins the known sites by name and requires each to be found and isolated;
/// the synthetic controls run the same classifier on in-memory sources, one per roster entry and
/// one per verdict.</para>
/// </summary>
public class ProcessGlobalStateConformanceTests
{
    private static readonly string[] ScannedProjects =
    {
        "Sharpy.Compiler.Tests",
        "Sharpy.Stdlib.Tests",
        "Sharpy.Core.Tests",
        "Sharpy.Cli.Tests",
        "Sharpy.Lsp.Tests",
        "Sharpy.TestInfrastructure",
    };

    /// <summary>Invoked members that mutate a process global, and the global.</summary>
    private static readonly IReadOnlyDictionary<string, string> InvokedMutators = new Dictionary<string, string>
    {
        ["SetCurrentDirectory"] = "current directory (Directory.SetCurrentDirectory)",
        ["Chdir"] = "current directory (os.chdir -> OsModuleModule.Chdir)",
        ["SetOut"] = "Console.Out (Console.SetOut)",
        ["SetError"] = "Console.Error (Console.SetError)",
        ["SetIn"] = "Console.In (Console.SetIn)",
        ["CapturedOutput"] = "Console.Out (unittest.CapturedOutput)",
        ["CapturedStderr"] = "Console.Error (unittest.CapturedStderr)",
        ["SetEnvironmentVariable"] = "environment (Environment.SetEnvironmentVariable)",
        ["Putenv"] = "environment (os.putenv -> OsModuleModule.Putenv)",
        ["ClearCachedData"] = "time-zone cache (TimeZoneInfo.ClearCachedData)",
    };

    /// <summary>Product types whose construction (and use) mutates a process global in-process.</summary>
    private static readonly IReadOnlyDictionary<string, string> ConstructedMutators = new Dictionary<string, string>
    {
        ["CapturedOutput"] = "Console.Out (Sharpy.Unittest.CapturedOutput)",
        ["CapturedStderr"] = "Console.Error (Sharpy.Unittest.CapturedStderr)",
        ["ReplSession"] = "Console.Out (ReplSession.ExecuteAssembly swaps it on evaluation)",
        ["CompileServer"] = "current directory + Console (CompileServer.ExecuteCompile)",
    };

    /// <summary>Global properties whose assignment is a mutation: (receiver type, property, global).</summary>
    private static readonly (string Receiver, string Property, string Global)[] AssignedMutators =
    {
        ("Environment", "CurrentDirectory", "current directory (Environment.CurrentDirectory =)"),
        ("CultureInfo", "DefaultThreadCurrentCulture", "default culture (CultureInfo.DefaultThreadCurrentCulture =)"),
        ("CultureInfo", "DefaultThreadCurrentUICulture", "default UI culture (CultureInfo.DefaultThreadCurrentUICulture =)"),
    };

    /// <summary>
    /// Sites that mutate a global outside a run-alone collection on purpose: (path, enclosing type,
    /// roster token, why it is safe). Each row must match at least one site.
    /// </summary>
    private static readonly (string Path, string Type, string Token, string Reason)[] Exemptions =
    {
        ("src/Sharpy.Cli.Tests/CliTestHarness.cs", "Sharpy.Cli.Tests.CliTestHarness", "SetOut",
            "every Cli.Tests console redirection happens inside CliTestHarness's private lock; any other Cli.Tests redirector is a violation here"),
        ("src/Sharpy.Cli.Tests/CliTestHarness.cs", "Sharpy.Cli.Tests.CliTestHarness", "SetError",
            "every Cli.Tests console redirection happens inside CliTestHarness's private lock; any other Cli.Tests redirector is a violation here"),
        ("src/Sharpy.Compiler.Tests/Helpers/ProjectCompilationHelper.cs", "Sharpy.Compiler.Tests.Helpers.ProjectCompilationHelper", "SetOut",
            "redirected inside TestHelpers.ConsoleLock; every other Compiler.Tests console redirector (ReplSessionTests) runs alone in ConsoleCapture"),
        ("src/Sharpy.Compiler.Tests/Helpers/ProjectCompilationHelper.cs", "Sharpy.Compiler.Tests.Helpers.ProjectCompilationHelper", "SetError",
            "redirected inside TestHelpers.ConsoleLock; every other Compiler.Tests console redirector (ReplSessionTests) runs alone in ConsoleCapture"),
        ("src/Sharpy.Lsp.Tests/Conformance/FrontEndParityTests.cs", "Sharpy.Lsp.Tests.Conformance.FrontEndParityTests", "ReplSession",
            "calls only ReplSession.ProbeFrontEndDiagnostics, which never reaches ExecuteAssembly's Console.SetOut"),
    };

    /// <summary>
    /// Positive control: sites the scan must find, each isolated. Literal — derived from the
    /// tree by grep at landing, not from the scan.
    /// </summary>
    private static readonly (string Path, string Type, string Token)[] KnownIsolatedSites =
    {
        ("src/Sharpy.Compiler.Tests/Semantic/ModuleResolverTests.cs", "Sharpy.Compiler.Tests.Semantic.ModuleResolverTests", "SetCurrentDirectory"),
        ("src/Sharpy.Compiler.Tests/Services/ReplSessionTests.cs", "Sharpy.Compiler.Tests.Services.ReplSessionTests", "ReplSession"),
        ("src/Sharpy.Stdlib.Tests/Spy/generated/os_module_tests.cs", "Sharpy.Stdlib.Tests.Spy.Os.OsModuleTests.OsModuleTestsModuleTests", "Chdir"),
        ("src/Sharpy.Stdlib.Tests/Spy/generated/os_module_tests.cs", "Sharpy.Stdlib.Tests.Spy.Os.OsModuleTests.OsModuleTestsModuleTests", "Putenv"),
        ("src/Sharpy.Stdlib.Tests/Spy/generated/logging_module_tests.cs", "Sharpy.Stdlib.Tests.Spy.Logging.LoggingModuleTests.LoggingModuleTestsModuleTests", "CapturedStderr"),
        ("src/Sharpy.Stdlib.Tests/Spy/generated/logging_complete_tests.cs", "Sharpy.Stdlib.Tests.Spy.Logging.LoggingCompleteTests.LoggingCompleteTestsModuleTests", "CapturedStderr"),
        ("src/Sharpy.Stdlib.Tests/StrftimeDirectiveTests.cs", "Sharpy.Stdlib.Tests.StrftimeHostTimeZoneTests", "SetEnvironmentVariable"),
        ("src/Sharpy.Stdlib.Tests/StrftimeDirectiveTests.cs", "Sharpy.Stdlib.Tests.StrftimeHostTimeZoneTests", "ClearCachedData"),
        ("src/Sharpy.Stdlib.Tests/UnittestCapturedOutputTests.cs", "Sharpy.Core.Tests.UnittestCapturedOutputTests", "CapturedOutput"),
        ("src/Sharpy.Core.Tests/PrintTests.cs", "Sharpy.Core.Tests.Print_Tests", "SetOut"),
    };

    private static readonly Lazy<Analysis> RepositoryAnalysis = new(() => Analyze(ReadScannedFiles()));

    [Fact]
    public void EveryProcessGlobalMutation_RunsAloneInACollectionDefinedInItsOwnProject()
    {
        var analysis = RepositoryAnalysis.Value;

        var violations = analysis.Sites
            .Where(site => !IsExempt(site))
            .Select(site => (Site: site, Reason: analysis.Violation(site)))
            .Where(v => v.Reason != null)
            .Select(v => $"  {v.Site.Path}:{v.Site.Line} {v.Site.Token} [{v.Site.Global}] in {v.Site.Type}: {v.Reason}")
            .ToList();

        Assert.True(violations.Count == 0,
            $"{violations.Count} process-global mutation(s) can run concurrently with other tests:\n" +
            string.Join("\n", violations) +
            "\nPut the class in a collection defined in ITS OWN test project with DisableParallelization = true " +
            "(spy-generated classes: a partial twin in Spy/SpyTestCollections.cs), or add an exemption with a reason.");
    }

    [Fact]
    public void PositiveControl_TheKnownSitesAreFoundAndIsolated()
    {
        var analysis = RepositoryAnalysis.Value;

        foreach (var (path, type, token) in KnownIsolatedSites)
        {
            var sites = analysis.Sites.Where(s => s.Path == path && s.Type == type && s.Token == token).ToList();
            Assert.True(sites.Count > 0, $"control: the scan must find {token} in {type} ({path})");
            foreach (var site in sites)
                Assert.Null(analysis.Violation(site));
        }
    }

    [Fact]
    public void EveryExemption_MatchesASite()
    {
        var analysis = RepositoryAnalysis.Value;

        var stale = Exemptions
            .Where(e => !analysis.Sites.Any(s => s.Path == e.Path && s.Type == e.Type && s.Token == e.Token))
            .Select(e => $"  {e.Path} {e.Type} {e.Token}")
            .ToList();

        Assert.True(stale.Count == 0, "stale exemption(s) match no mutation site:\n" + string.Join("\n", stale));
    }

    public static IEnumerable<object[]> RosterSnippets()
    {
        foreach (var token in InvokedMutators.Keys)
            yield return new object[] { token, $"Receiver.{token}(argument);" };
        foreach (var token in ConstructedMutators.Keys)
            yield return new object[] { token, $"var instance = new {token}();" };
        foreach (var (receiver, property, _) in AssignedMutators)
            yield return new object[] { property, $"System.{receiver}.{property} = value;" };
    }

    /// <summary>Every roster entry is matched by the walk — a family whose matcher broke would otherwise pass as "no sites".</summary>
    [Theory]
    [MemberData(nameof(RosterSnippets))]
    public void Synthetic_EveryRosterEntryIsASite(string token, string statement)
    {
        var analysis = Analyze(new[] { Source("Sharpy.Fake.Tests", "A.cs", $"namespace N; public class T {{ void M() {{ {statement} }} }}") });

        var site = Assert.Single(analysis.Sites);
        Assert.Equal(token, site.Token);
        Assert.Equal("N.T", site.Type);
        Assert.NotNull(analysis.Violation(site));
    }

    [Fact]
    public void Synthetic_StringsCommentsAndReadsAreNotSites()
    {
        var analysis = Analyze(new[]
        {
            Source("Sharpy.Fake.Tests", "A.cs", """
                namespace N;
                public class T
                {
                    // Directory.SetCurrentDirectory(x);
                    /// <see cref="ReplSession"/>
                    void M()
                    {
                        var s = "Console.SetOut(w); os.Chdir(p)";
                        var d = System.Environment.CurrentDirectory;
                        var c = System.Globalization.CultureInfo.DefaultThreadCurrentCulture;
                        System.Globalization.CultureInfo.CurrentCulture = null;
                        Options.CurrentDirectory = d;
                    }
                }
                """),
        });

        Assert.Empty(analysis.Sites);
    }

    [Fact]
    public void Synthetic_NoCollection_IsAViolation()
    {
        var analysis = Analyze(new[] { Source("Sharpy.Fake.Tests", "A.cs", MutatingClass(attribute: "")) });

        Assert.Contains("no [Collection]", analysis.Violation(Assert.Single(analysis.Sites)));
    }

    [Fact]
    public void Synthetic_CollectionDefinedInAnotherProject_IsAViolation()
    {
        var analysis = Analyze(new[]
        {
            Source("Sharpy.Fake.Tests", "A.cs", MutatingClass(attribute: "[Collection(\"Alone\")]")),
            Source("Sharpy.TestInfrastructure", "C.cs", Definition("Alone", disableParallelization: true)),
        });

        Assert.Contains("no [CollectionDefinition(\"Alone\")] in src/Sharpy.Fake.Tests", analysis.Violation(Assert.Single(analysis.Sites)));
    }

    [Fact]
    public void Synthetic_CollectionThatRunsInParallel_IsAViolation()
    {
        var analysis = Analyze(new[]
        {
            Source("Sharpy.Fake.Tests", "A.cs", MutatingClass(attribute: "[Collection(\"Shared\")]")),
            Source("Sharpy.Fake.Tests", "C.cs", Definition("Shared", disableParallelization: false)),
        });

        Assert.Contains("DisableParallelization", analysis.Violation(Assert.Single(analysis.Sites)));
    }

    [Fact]
    public void Synthetic_PartialTwinInTheSameProject_Isolates_AndOneInAnotherNamespaceDoesNot()
    {
        var twin = """
            namespace N { [Xunit.Collection("Alone")] public partial class T { } }
            """;
        var wrongNamespaceTwin = """
            namespace M { [Xunit.Collection("Alone")] public partial class T { } }
            """;
        var generated = """
            namespace N { public partial class T { void M() { os.Chdir(p); } } }
            """;

        var isolated = Analyze(new[]
        {
            Source("Sharpy.Fake.Tests", "Spy/generated/a.cs", generated),
            Source("Sharpy.Fake.Tests", "Spy/Twins.cs", twin),
            Source("Sharpy.Fake.Tests", "C.cs", Definition("Alone", disableParallelization: true)),
        });
        Assert.Null(isolated.Violation(Assert.Single(isolated.Sites)));

        var notIsolated = Analyze(new[]
        {
            Source("Sharpy.Fake.Tests", "Spy/generated/a.cs", generated),
            Source("Sharpy.Fake.Tests", "Spy/Twins.cs", wrongNamespaceTwin),
            Source("Sharpy.Fake.Tests", "C.cs", Definition("Alone", disableParallelization: true)),
        });
        Assert.NotNull(notIsolated.Violation(Assert.Single(notIsolated.Sites)));
    }

    [Fact]
    public void Synthetic_SiteInANestedType_IsAttributedToTheOutermostType()
    {
        var analysis = Analyze(new[]
        {
            Source("Sharpy.Fake.Tests", "A.cs", """
                namespace N;
                [Collection("Alone")]
                public class T
                {
                    private sealed class Helper { void M() { System.Console.SetError(w); } }
                }
                """),
            Source("Sharpy.Fake.Tests", "C.cs", Definition("Alone", disableParallelization: true)),
        });

        var site = Assert.Single(analysis.Sites);
        Assert.Equal("N.T", site.Type);
        Assert.Null(analysis.Violation(site));
    }

    [Fact]
    public void Synthetic_SiteInSharedInfrastructure_IsAlwaysAViolation()
    {
        var analysis = Analyze(new[]
        {
            Source("Sharpy.TestInfrastructure", "A.cs", MutatingClass(attribute: "[Collection(\"Alone\")]")),
            Source("Sharpy.TestInfrastructure", "C.cs", Definition("Alone", disableParallelization: true)),
        });

        Assert.Contains("shared test library", analysis.Violation(Assert.Single(analysis.Sites)));
    }

    // ── scan ─────────────────────────────────────────────────────────────────

    private sealed record SourceFile(string Project, string Path, string Text);

    private sealed record Site(string Project, string Path, int Line, string Type, string Token, string Global);

    private sealed class Analysis
    {
        public required List<Site> Sites { get; init; }

        /// <summary>project → type full name → collection names on any of its declarations.</summary>
        public required Dictionary<string, Dictionary<string, HashSet<string>>> Memberships { get; init; }

        /// <summary>project → collection name → DisableParallelization.</summary>
        public required Dictionary<string, Dictionary<string, bool>> Definitions { get; init; }

        /// <summary>Null when the site is isolated; otherwise why it is not.</summary>
        public string? Violation(Site site)
        {
            if (site.Project == "Sharpy.TestInfrastructure")
                return "a shared test library; no one collection can isolate it — move the mutation into a test project or exempt it";

            var collections = Memberships.GetValueOrDefault(site.Project)?.GetValueOrDefault(site.Type);
            if (collections == null || collections.Count == 0)
                return "its class carries no [Collection]";

            var name = collections.First();
            var definitions = Definitions.GetValueOrDefault(site.Project);
            if (definitions == null || !definitions.TryGetValue(name, out var disableParallelization))
                return $"no [CollectionDefinition(\"{name}\")] in src/{site.Project} — xUnit v2 ignores definitions in other assemblies, so the collection runs in parallel";
            if (!disableParallelization)
                return $"collection \"{name}\" is not defined with DisableParallelization = true";
            return null;
        }
    }

    private static bool IsExempt(Site site)
        => Exemptions.Any(e => e.Path == site.Path && e.Type == site.Type && e.Token == site.Token);

    private static SourceFile Source(string project, string relativePath, string text)
        => new(project, $"src/{project}/{relativePath}", text);

    private static string MutatingClass(string attribute)
        => $"namespace N;\n{attribute}\npublic class T {{ void M() {{ System.IO.Directory.SetCurrentDirectory(d); }} }}\n";

    private static string Definition(string name, bool disableParallelization)
        => disableParallelization
            ? $"namespace N;\n[Xunit.CollectionDefinition(\"{name}\", DisableParallelization = true)]\npublic class D {{ }}\n"
            : $"namespace N;\n[CollectionDefinition(\"{name}\")]\npublic class D {{ }}\n";

    private static List<SourceFile> ReadScannedFiles()
    {
        var files = new List<SourceFile>();
        foreach (var project in ScannedProjects)
        {
            var root = Path.Combine(FixtureRoots.RepositoryRoot, "src", project);
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(FixtureRoots.RepositoryRoot, file).Replace('\\', '/');
                if (rel.Contains("/bin/", StringComparison.Ordinal)
                    || rel.Contains("/obj/", StringComparison.Ordinal)
                    || rel.Contains("/Integration/TestFixtures/", StringComparison.Ordinal))
                    continue;
                files.Add(new SourceFile(project, rel, System.IO.File.ReadAllText(file)));
            }
        }
        Assert.True(files.Count > 0, "control: the scan must read source files");
        return files;
    }

    private static Analysis Analyze(IEnumerable<SourceFile> files)
    {
        var options = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithPreprocessorSymbols("NET", "NET10_0", "NET10_0_OR_GREATER");
        var sites = new List<Site>();
        var memberships = new Dictionary<string, Dictionary<string, HashSet<string>>>();
        var definitions = new Dictionary<string, Dictionary<string, bool>>();

        foreach (var file in files)
        {
            var tree = CSharpSyntaxTree.ParseText(file.Text, options, path: file.Path);
            var root = tree.GetRoot();

            foreach (var type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                foreach (var attribute in type.AttributeLists.SelectMany(l => l.Attributes))
                {
                    var name = AttributeName(attribute);
                    if (name == "Collection" && type.Parent is not BaseTypeDeclarationSyntax
                        && FirstStringArgument(attribute) is { } member)
                    {
                        var perType = Index(memberships, file.Project);
                        var fullName = FullName(type);
                        if (!perType.TryGetValue(fullName, out var collections))
                            perType[fullName] = collections = new HashSet<string>();
                        collections.Add(member);
                    }
                    else if (name == "CollectionDefinition" && FirstStringArgument(attribute) is { } defined)
                    {
                        Index(definitions, file.Project)[defined] = DisablesParallelization(attribute);
                    }
                }
            }

            foreach (var node in root.DescendantNodes())
            {
                var match = MatchMutation(node);
                if (match == null)
                    continue;
                var outermost = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().LastOrDefault();
                sites.Add(new Site(
                    file.Project,
                    file.Path,
                    tree.GetLineSpan(node.Span).StartLinePosition.Line + 1,
                    outermost == null ? "<no type>" : FullName(outermost),
                    match.Value.Token,
                    match.Value.Global));
            }
        }

        return new Analysis { Sites = sites, Memberships = memberships, Definitions = definitions };
    }

    private static (string Token, string Global)? MatchMutation(SyntaxNode node)
    {
        switch (node)
        {
            case InvocationExpressionSyntax invocation:
                {
                    var name = invocation.Expression switch
                    {
                        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
                        MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text,
                        SimpleNameSyntax simple => simple.Identifier.Text,
                        _ => null,
                    };
                    return name != null && InvokedMutators.TryGetValue(name, out var global) ? (name, global) : null;
                }
            case ObjectCreationExpressionSyntax creation:
                {
                    var name = creation.Type is NameSyntax typeName ? SimpleName(typeName) : null;
                    return name != null && ConstructedMutators.TryGetValue(name, out var global) ? (name, global) : null;
                }
            case AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax target }:
                {
                    var receiver = target.Expression switch
                    {
                        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
                        SimpleNameSyntax simple => simple.Identifier.Text,
                        _ => null,
                    };
                    foreach (var (type, property, global) in AssignedMutators)
                    {
                        if (receiver == type && target.Name.Identifier.Text == property)
                            return (property, global);
                    }
                    return null;
                }
            default:
                return null;
        }
    }

    private static string SimpleName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => name.ToString(),
    };

    private static string AttributeName(AttributeSyntax attribute)
    {
        var name = SimpleName(attribute.Name);
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;
    }

    private static string? FirstStringArgument(AttributeSyntax attribute)
        => attribute.ArgumentList?.Arguments.FirstOrDefault() is { NameEquals: null, NameColon: null } argument
           && argument.Expression is LiteralExpressionSyntax literal
           && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;

    private static bool DisablesParallelization(AttributeSyntax attribute)
        => attribute.ArgumentList?.Arguments.Any(a =>
               a.NameEquals?.Name.Identifier.Text == "DisableParallelization"
               && a.Expression.IsKind(SyntaxKind.TrueLiteralExpression)) == true;

    private static string FullName(BaseTypeDeclarationSyntax type)
    {
        var namespaces = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString());
        var enclosingTypes = type.Ancestors().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.Text);
        return string.Join(".", namespaces.Concat(enclosingTypes).Append(type.Identifier.Text));
    }

    private static Dictionary<string, TValue> Index<TValue>(Dictionary<string, Dictionary<string, TValue>> index, string project)
    {
        if (!index.TryGetValue(project, out var perProject))
            index[project] = perProject = new Dictionary<string, TValue>();
        return perProject;
    }
}

