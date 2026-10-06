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
/// when called in-process (<see cref="InvokedMutators"/>; <see cref="ReceiverInvokedMutators"/> when
/// the member name alone is too common — a forced <c>GC.Collect</c> stalls every concurrently
/// running test, so it is held to the same rule, #2254), a construction of such a product type
/// (<see cref="ConstructedMutators"/>), or an assignment to a global property
/// (<see cref="AssignedMutators"/>). Strings and comments never match. Each site is attributed to
/// its outermost enclosing type, whose <c>[Collection]</c> is read from every partial declaration
/// of that type in the same project (the spy-generated classes get theirs from a hand-written twin
/// in <c>Spy/SpyTestCollections.cs</c>).</para>
///
/// <para>A site is <b>isolated</b> when that collection is defined in the same project with
/// <c>DisableParallelization = true</c>; <b>exempt</b> when it is a row of <see cref="Exemptions"/>
/// — keyed on the enclosing member and, for a lock-based reason, on the lock the token must sit in
/// lexically, so one justified site never exempts a later use of the same token elsewhere in the
/// type (#2258); a row matching no site is stale and fails — otherwise a <b>violation</b>. Sites in
/// Sharpy.TestInfrastructure can only be exempt — no one collection isolates a shared library.
/// The positive control pins the known sites by name and requires each to be found and isolated;
/// the synthetic controls run the same classifier on in-memory sources, one per roster entry and
/// one per verdict.</para>
///
/// <para><b>The serial-collection roster.</b> A collection with <c>DisableParallelization = true</c>
/// runs last and alone, so every such definition and every class in it costs wall clock for the
/// whole suite (#2179: one 173-class collection made the suite serial). <see cref="SerialCollectionRoster"/>
/// lists each one literally — project, name, why it must run alone, and its member classes — and
/// the scan must find exactly that: an unlisted definition or member fails, and so does a roster
/// row or member the scan no longer finds. Separately, every collection a class names must be
/// defined in that class's own project, since a definition anywhere else is ignored.</para>
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
        ["CompileAndExecuteWithGC"] = "every test thread (IntegrationTestBase.CompileAndExecuteWithGC forces a blocking Gen2 GC per call, #2254)",
    };

    /// <summary>
    /// Invoked members that are a process-wide effect only on a specific receiver type — the member
    /// name alone is too common to match: (receiver type, member, global).
    /// </summary>
    private static readonly (string Receiver, string Member, string Global)[] ReceiverInvokedMutators =
    {
        ("GC", "Collect", "every test thread (GC.Collect: a forced collection stalls the concurrently running tests, #2254)"),
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
    /// A site that mutates a global outside a run-alone collection on purpose, scoped to ONE site
    /// (#2258): the file, the outermost type, the enclosing MEMBER (method, constructor, property —
    /// by name; a lambda or local function belongs to the member around it), the roster token,
    /// and — when the reason is a lock — the lock expression, spelled as in source, that must
    /// enclose the token lexically (a lambda or local function body is a boundary: code there runs
    /// whenever it is invoked, not under the lock it is written in). A new use of the token in
    /// another member, or outside the lock in the same member, is not exempt.
    /// </summary>
    private sealed record Exemption(string Path, string Type, string Member, string Token, string? Lock, string Reason);

    /// <summary>The exempt sites, each with why it is safe. Each row must match at least one site.</summary>
    private static readonly Exemption[] Exemptions =
    {
        new("src/Sharpy.Cli.Tests/CliTestHarness.cs", "Sharpy.Cli.Tests.CliTestHarness", "Invoke", "SetOut", "ConsoleLock",
            "every Cli.Tests console redirection happens inside CliTestHarness's private lock; any other Cli.Tests redirector is a violation here"),
        new("src/Sharpy.Cli.Tests/CliTestHarness.cs", "Sharpy.Cli.Tests.CliTestHarness", "Invoke", "SetError", "ConsoleLock",
            "every Cli.Tests console redirection happens inside CliTestHarness's private lock; any other Cli.Tests redirector is a violation here"),
        new("src/Sharpy.Cli.Tests/CliTestHarness.cs", "Sharpy.Cli.Tests.CliTestHarness", "CaptureConsole", "SetOut", "ConsoleLock",
            "every Cli.Tests console redirection happens inside CliTestHarness's private lock; any other Cli.Tests redirector is a violation here"),
        new("src/Sharpy.Cli.Tests/CliTestHarness.cs", "Sharpy.Cli.Tests.CliTestHarness", "CaptureConsole", "SetError", "ConsoleLock",
            "every Cli.Tests console redirection happens inside CliTestHarness's private lock; any other Cli.Tests redirector is a violation here"),
        new("src/Sharpy.Compiler.Tests/Helpers/ProjectCompilationHelper.cs", "Sharpy.Compiler.Tests.Helpers.ProjectCompilationHelper", "ExecuteAssembly", "SetOut", "TestHelpers.ConsoleLock",
            "redirected inside TestHelpers.ConsoleLock; every other Compiler.Tests console redirector (ReplSessionTests) runs alone in ConsoleCapture"),
        new("src/Sharpy.Compiler.Tests/Helpers/ProjectCompilationHelper.cs", "Sharpy.Compiler.Tests.Helpers.ProjectCompilationHelper", "ExecuteAssembly", "SetError", "TestHelpers.ConsoleLock",
            "redirected inside TestHelpers.ConsoleLock; every other Compiler.Tests console redirector (ReplSessionTests) runs alone in ConsoleCapture"),
        new("src/Sharpy.Lsp.Tests/Conformance/FrontEndParityTests.cs", "Sharpy.Lsp.Tests.Conformance.FrontEndParityTests", "SweepSingleFileAsync", "ReplSession", null,
            "calls only ReplSession.ProbeFrontEndDiagnostics, which never reaches ExecuteAssembly's Console.SetOut"),
        new("src/Sharpy.TestInfrastructure/Integration/IntegrationTestBase.cs", "Sharpy.TestInfrastructure.Integration.IntegrationTestBase", "CompileAndExecuteWithGC", "Collect", null,
            "the opt-in helper itself: each of its callers is a CompileAndExecuteWithGC site, which this scan holds to a run-alone collection"),
        new("src/Sharpy.Compiler.Tests/Properties/Algebraic/AlgebraicTestBase.cs", "Sharpy.Compiler.Tests.Properties.Algebraic.AlgebraicTestBase", "RunAndCapture", "Collect", null,
            "abstract base of the nine Properties.Algebraic.* classes, each a PropertySerial member in SerialCollectionRoster"),
        new("src/Sharpy.Compiler.Tests/Properties/Generators/Typed/SemanticFilter.cs", "Sharpy.Compiler.Tests.Properties.Generators.Typed.SemanticFilter", "WellTypedProgram", "Collect", null,
            "a CsCheck Gen filter called only from Properties.* classes, each a PropertySerial member in SerialCollectionRoster"),
        new("src/Sharpy.Compiler.Tests/Properties/Generators/Typed/SemanticFilter.cs", "Sharpy.Compiler.Tests.Properties.Generators.Typed.SemanticFilter", "CompilableProgram", "Collect", null,
            "a CsCheck Gen filter called only from Properties.* classes, each a PropertySerial member in SerialCollectionRoster"),
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
        ("src/Sharpy.Compiler.Tests/Properties/Metamorphic/MetamorphicPropertyTests.cs", "Sharpy.Compiler.Tests.Properties.Metamorphic.MetamorphicPropertyTests", "CompileAndExecuteWithGC"),
        ("src/Sharpy.Compiler.Tests/Properties/Semantic/TypeSoundnessPropertyTests.cs", "Sharpy.Compiler.Tests.Properties.Semantic.TypeSoundnessPropertyTests", "CompileAndExecuteWithGC"),
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
            .Where(e => !analysis.Sites.Any(s => Exempts(e, s)))
            .Select(e => $"  {e.Path} {e.Type}.{e.Member} {e.Token}" + (e.Lock == null ? "" : $" inside lock ({e.Lock})"))
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
        foreach (var (receiver, member, _) in ReceiverInvokedMutators)
            yield return new object[] { member, $"System.{receiver}.{member}(argument);" };
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
                        items.Collect(d);
                        // GC.Collect(); CompileAndExecuteWithGC(source);
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

    /// <summary>
    /// #2258: an exemption covers its own member, and — when it names a lock — only the uses
    /// lexically inside that lock. The same token in another member, after the lock in the same
    /// member, under a different lock, or in a lambda written inside the lock is not exempt; a
    /// lock-free row covers its whole member.
    /// </summary>
    [Fact]
    public void Synthetic_ExemptionIsScopedToItsMemberAndItsLock()
    {
        var analysis = Analyze(new[]
        {
            Source("Sharpy.Fake.Tests", "A.cs", """
                namespace N;
                public class T
                {
                    void Guarded()
                    {
                        lock (Gate.ConsoleLock) { System.Console.SetOut(inside); }
                        System.Console.SetOut(after);
                        lock (OtherLock) { System.Console.SetOut(wrongLock); }
                        lock (Gate.ConsoleLock) { System.Action a = () => System.Console.SetOut(deferred); }
                    }
                    void Elsewhere() { lock (Gate.ConsoleLock) { System.Console.SetOut(otherMember); } }
                    void Unlocked() { System.Console.SetError(anyway); }
                }
                """),
        });
        var exemptions = new[]
        {
            new Exemption("src/Sharpy.Fake.Tests/A.cs", "N.T", "Guarded", "SetOut", "Gate.ConsoleLock", "test"),
            new Exemption("src/Sharpy.Fake.Tests/A.cs", "N.T", "Unlocked", "SetError", null, "test"),
        };

        var verdicts = analysis.Sites
            .OrderBy(s => s.Line)
            .Select(s => (s.Member, Exempt: IsExempt(s, exemptions)))
            .ToList();

        Assert.Equal(new[]
        {
            ("Guarded", true),    // inside the named lock
            ("Guarded", false),   // after it
            ("Guarded", false),   // under another lock
            ("Guarded", false),   // in a lambda written inside the lock
            ("Elsewhere", false), // same token and lock, another member
            ("Unlocked", true),   // lock-free row: the member is the scope
        }, verdicts);
    }

    // ── serial-collection roster ─────────────────────────────────────────────

    private const string PropertiesNs = "Sharpy.Compiler.Tests.Properties.";

    /// <summary>
    /// Every collection defined with <c>DisableParallelization = true</c> in the scanned projects, with
    /// why it must run alone and its member classes (full names). Change it only with the audit that
    /// justifies the run-alone cost.
    /// </summary>
    private static readonly (string Project, string Collection, string Reason, string[] Members)[] SerialCollectionRoster =
    {
        ("Sharpy.Compiler.Tests", "ProcessCwd",
            "changes the process's current directory, which every compile in the assembly reads",
            new[] { "Sharpy.Compiler.Tests.Semantic.ModuleResolverTests" }),
        ("Sharpy.Compiler.Tests", "ConsoleCapture",
            "ReplSession swaps Console.Out on a thread-pool thread, outside TestHelpers.ConsoleLock",
            new[] { "Sharpy.Compiler.Tests.Services.ReplSessionTests" }),
        ("Sharpy.Compiler.Tests", "PropertySerial",
            "CsCheck Sample already uses one thread per core; compile-heavy property classes run alone to bound memory (#649)",
            new[]
            {
                PropertiesNs + "Algebraic.ArithmeticPropertyTests", PropertiesNs + "Algebraic.BooleanPropertyTests",
                PropertiesNs + "Algebraic.DictPropertyTests", PropertiesNs + "Algebraic.ListPropertyTests",
                PropertiesNs + "Algebraic.MapZipPropertyTests", PropertiesNs + "Algebraic.SetPropertyTests",
                PropertiesNs + "Algebraic.SortedPropertyTests", PropertiesNs + "Algebraic.StringPropertyTests",
                PropertiesNs + "Algebraic.TuplePropertyTests",
                PropertiesNs + "CodeGen.CsCleanPropertyTests", PropertiesNs + "CodeGen.ILCompilesPropertyTests",
                PropertiesNs + "CodeGen.IncrementalCompilationPropertyTests",
                PropertiesNs + "CodeGen.NormalizationIdempotencePropertyTests",
                PropertiesNs + "CodeGen.RoslynParseablePropertyTests",
                PropertiesNs + "Metamorphic.MetamorphicCorpusInvarianceTests", PropertiesNs + "Metamorphic.MetamorphicPropertyTests",
                PropertiesNs + "Parser.ErrorRecoveryPropertyTests",
                PropertiesNs + "Semantic.AsyncPropertyTests", PropertiesNs + "Semantic.BuiltinShadowingPropertyTests",
                PropertiesNs + "Semantic.CfgWellFormednessPropertyTests", PropertiesNs + "Semantic.ClassInheritancePropertyTests",
                PropertiesNs + "Semantic.CombinedFeaturePropertyTests", PropertiesNs + "Semantic.ContextManagerPropertyTests",
                PropertiesNs + "Semantic.DecoratorPropertyTests", PropertiesNs + "Semantic.DeterminismPropertyTests",
                PropertiesNs + "Semantic.DiagnosticSpanPropertyTests", PropertiesNs + "Semantic.ErrorCompletenessPropertyTests",
                PropertiesNs + "Semantic.ExceptionPropertyTests", PropertiesNs + "Semantic.FStringPropertyTests",
                PropertiesNs + "Semantic.FunctionSemanticsPropertyTests", PropertiesNs + "Semantic.GenericTypePropertyTests",
                PropertiesNs + "Semantic.ImportResolutionPropertyTests", PropertiesNs + "Semantic.InterfacePropertyTests",
                PropertiesNs + "Semantic.IteratorPropertyTests", PropertiesNs + "Semantic.OperatorOverloadPropertyTests",
                PropertiesNs + "Semantic.OverloadOrderIndependencePropertyTests", PropertiesNs + "Semantic.PatternMatchPropertyTests",
                PropertiesNs + "Semantic.SemanticInfoConsistencyTests", PropertiesNs + "Semantic.SymbolPositionPropertyTests",
                PropertiesNs + "Semantic.TypeSoundnessPropertyTests", PropertiesNs + "Semantic.TypedAnalyzePropertyTests",
                PropertiesNs + "Semantic.ValidatorOrderingPropertyTests", PropertiesNs + "Semantic.VariancePropertyTests",
                PropertiesNs + "Stress.LargeProgramPropertyTests",
            }),
        ("Sharpy.Stdlib.Tests", "ProcessCwd",
            "os.chdir changes the current directory and os.putenv the environment; getcwd, Path.cwd and relative paths read them",
            new[] { "Sharpy.Stdlib.Tests.Spy.Os.OsModuleTests.OsModuleTestsModuleTests" }),
        ("Sharpy.Stdlib.Tests", "ConsoleCapture",
            "CapturedOutput/CapturedStderr swap Console.Out/Error; argparse --help and logging write the console unprotected",
            new[]
            {
                "Sharpy.Core.Tests.UnittestCapturedOutputTests",
                "Sharpy.Stdlib.Tests.Spy.Logging.LoggingModuleTests.LoggingModuleTestsModuleTests",
                "Sharpy.Stdlib.Tests.Spy.Logging.LoggingCompleteTests.LoggingCompleteTestsModuleTests",
            }),
        ("Sharpy.Stdlib.Tests", "HostTimeZone",
            "sets TZ and clears the TimeZoneInfo cache, which time/datetime tests and child processes read",
            new[] { "Sharpy.Stdlib.Tests.StrftimeHostTimeZoneTests" }),
        ("Sharpy.Core.Tests", "ConsoleCapture",
            "Console.SetOut swaps a process-global writer while Core.Tests runs collections in parallel",
            new[] { "Sharpy.Core.Tests.Print_Tests" }),
    };

    /// <summary>Positive control: roster size counted by hand from the P2/P4/P5 audit (not from the array).</summary>
    private const int ExpectedSerialCollections = 7;

    /// <summary>1 + 1 + 44 (Compiler) + 1 + 3 + 1 (Stdlib) + 1 (Core), counted by hand; the WallClockWindow row (#2179 P5.1) drained with #2251.</summary>
    private const int ExpectedSerialMembers = 52;

    [Fact]
    public void EveryRunAloneCollection_IsRostered_AndEveryRosterRowIsDefined()
    {
        var analysis = RepositoryAnalysis.Value;
        var found = RunAloneDefinitions(analysis);
        var rostered = SerialCollectionRoster.Select(r => (r.Project, r.Collection)).ToHashSet();

        var unrostered = found.Except(rostered).Select(d => $"  + {d.Project}: \"{d.Collection}\"").ToList();
        var stale = rostered.Except(found).Select(d => $"  - {d.Project}: \"{d.Collection}\"").ToList();

        Assert.True(unrostered.Count == 0 && stale.Count == 0,
            "the run-alone collections differ from SerialCollectionRoster:\n" + string.Join("\n", unrostered.Concat(stale)) +
            "\n(+ defined with DisableParallelization = true but not rostered; - rostered but no longer defined so)." +
            " A run-alone collection costs suite wall clock: roster it with its reason, or drop the row.");
    }

    [Fact]
    public void EveryMemberOfARunAloneCollection_IsRostered()
    {
        var analysis = RepositoryAnalysis.Value;
        var found = RunAloneMembers(analysis);
        var rostered = SerialCollectionRoster
            .SelectMany(r => r.Members.Select(m => (r.Project, r.Collection, Member: m)))
            .ToHashSet();

        var unrostered = found.Except(rostered).Select(m => $"  + {m.Project}: {m.Member} in \"{m.Collection}\"").ToList();
        var stale = rostered.Except(found).Select(m => $"  - {m.Project}: {m.Member} in \"{m.Collection}\"").ToList();

        Assert.True(unrostered.Count == 0 && stale.Count == 0,
            "the members of run-alone collections differ from SerialCollectionRoster:\n" + string.Join("\n", unrostered.Concat(stale)) +
            "\n(+ a class joined a run-alone collection without a roster entry; - a rostered member no longer names it).");
    }

    [Fact]
    public void PositiveControl_TheRosterIsFoundInFull()
    {
        var analysis = RepositoryAnalysis.Value;

        Assert.Equal(ExpectedSerialCollections, SerialCollectionRoster.Length);
        Assert.Equal(ExpectedSerialMembers, SerialCollectionRoster.Sum(r => r.Members.Length));
        Assert.Equal(ExpectedSerialCollections, RunAloneDefinitions(analysis).Count);
        Assert.Equal(ExpectedSerialMembers, RunAloneMembers(analysis).Count);
    }

    [Fact]
    public void EveryCollectionAClassNames_IsDefinedInItsOwnProject()
    {
        var analysis = RepositoryAnalysis.Value;

        var inert = analysis.Memberships
            .SelectMany(p => p.Value.SelectMany(t => t.Value.Select(c => (Project: p.Key, Type: t.Key, Collection: c))))
            .Where(m => analysis.Definitions.GetValueOrDefault(m.Project)?.ContainsKey(m.Collection) != true)
            .Select(m => $"  {m.Project}: {m.Type} names \"{m.Collection}\"")
            .ToList();

        Assert.True(inert.Count == 0,
            "class(es) name a collection with no [CollectionDefinition] in their own project — xUnit v2 ignores " +
            "definitions in other assemblies, so the attribute is inert:\n" + string.Join("\n", inert));
    }

    private static HashSet<(string Project, string Collection)> RunAloneDefinitions(Analysis analysis)
        => analysis.Definitions
            .SelectMany(p => p.Value.Where(d => d.Value).Select(d => (p.Key, d.Key)))
            .ToHashSet();

    private static HashSet<(string Project, string Collection, string Member)> RunAloneMembers(Analysis analysis)
    {
        var runAlone = RunAloneDefinitions(analysis);
        return analysis.Memberships
            .SelectMany(p => p.Value.SelectMany(t => t.Value.Select(c => (Project: p.Key, Collection: c, Member: t.Key))))
            .Where(m => runAlone.Contains((m.Project, m.Collection)))
            .ToHashSet();
    }

    // ── scan ─────────────────────────────────────────────────────────────────

    private sealed record SourceFile(string Project, string Path, string Text);

    /// <param name="Member">The enclosing member's name (see <see cref="EnclosingMember"/>).</param>
    /// <param name="Locks">The expressions of the <c>lock</c> statements lexically enclosing the site,
    /// innermost first, up to the nearest lambda or local function.</param>
    private sealed record Site(string Project, string Path, int Line, string Type, string Member,
        IReadOnlyList<string> Locks, string Token, string Global);

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

    private static bool IsExempt(Site site, IEnumerable<Exemption>? exemptions = null)
        => (exemptions ?? Exemptions).Any(e => Exempts(e, site));

    private static bool Exempts(Exemption exemption, Site site)
        => exemption.Path == site.Path
           && exemption.Type == site.Type
           && exemption.Member == site.Member
           && exemption.Token == site.Token
           && (exemption.Lock == null || site.Locks.Contains(exemption.Lock));

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
                    EnclosingMember(node),
                    EnclosingLocks(node),
                    match.Value.Token,
                    match.Value.Global));
            }
        }

        return new Analysis { Sites = sites, Memberships = memberships, Definitions = definitions };
    }

    /// <summary>
    /// The name of the member declaring the site: a method, constructor, operator, property,
    /// indexer, event or field initializer. A lambda or local function belongs to the member that
    /// contains it.
    /// </summary>
    private static string EnclosingMember(SyntaxNode node)
    {
        var member = node.Ancestors().OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(m => m is not BaseTypeDeclarationSyntax and not BaseNamespaceDeclarationSyntax);
        return member switch
        {
            MethodDeclarationSyntax method => method.Identifier.Text,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.Text,
            DestructorDeclarationSyntax destructor => "~" + destructor.Identifier.Text,
            PropertyDeclarationSyntax property => property.Identifier.Text,
            EventDeclarationSyntax @event => @event.Identifier.Text,
            BaseFieldDeclarationSyntax field => string.Join(",", field.Declaration.Variables.Select(v => v.Identifier.Text)),
            null => "<no member>",
            _ => member.Kind().ToString(),
        };
    }

    /// <summary>
    /// The <c>lock</c> expressions enclosing <paramref name="node"/>, innermost first. A lambda or
    /// local function stops the walk: its body runs when it is invoked, not under the lock it is
    /// written in.
    /// </summary>
    private static IReadOnlyList<string> EnclosingLocks(SyntaxNode node)
    {
        var locks = new List<string>();
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax)
                break;
            if (ancestor is LockStatementSyntax lockStatement)
                locks.Add(lockStatement.Expression.ToString());
        }
        return locks;
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
                    if (name != null && InvokedMutators.TryGetValue(name, out var global))
                        return (name, global);
                    if (invocation.Expression is MemberAccessExpressionSyntax receiverAccess)
                    {
                        var receiver = ReceiverName(receiverAccess.Expression);
                        foreach (var (type, member, receiverGlobal) in ReceiverInvokedMutators)
                        {
                            if (receiver == type && name == member)
                                return (member, receiverGlobal);
                        }
                    }
                    return null;
                }
            case ObjectCreationExpressionSyntax creation:
                {
                    var name = creation.Type is NameSyntax typeName ? SimpleName(typeName) : null;
                    return name != null && ConstructedMutators.TryGetValue(name, out var global) ? (name, global) : null;
                }
            case AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax target }:
                {
                    var receiver = ReceiverName(target.Expression);
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

    /// <summary>The last identifier of a receiver expression (<c>GC</c> and <c>System.GC</c> both read <c>GC</c>).</summary>
    private static string? ReceiverName(ExpressionSyntax receiver) => receiver switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => null,
    };

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

