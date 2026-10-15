using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Every place the compiler resolves a CLR name against — or loads an assembly into — the PROCESS
/// rather than its own compilation is a listed row (#2250).
///
/// <para><b>Why.</b> <c>AppDomain.CurrentDomain.GetAssemblies()</c> answers "whatever this process has
/// loaded so far", the static <c>Type.GetType(string)</c> answers against the same set, and
/// <c>Assembly.LoadFrom</c> loads into the one default <c>AssemblyLoadContext</c>, where the first copy
/// of a simple name wins for every later compile. A compile that consults any of them is a function of
/// the process's history, not of its inputs — the in-process isolation class P54 cuts at ONE
/// visibility seam owned by the compilation's registry.</para>
///
/// <para><b>What it scans.</b> A Roslyn SEMANTIC walk over <c>src/Sharpy.Compiler/**/*.cs</c> (bin/obj
/// excluded), compiled with the compiler's own references (the trusted-platform list, Sharpy.Core under
/// its <c>SharpyRT</c> alias) and its SDK global usings — without them the receivers bind to error types
/// and a symbol match silently finds nothing. A <i>site</i> is a name whose BOUND symbol is one of:
/// <c>System.AppDomain.GetAssemblies</c>; the STATIC <c>System.Type.GetType</c> (any overload — the
/// instance <c>object.GetType()</c> is not a resolution site, which is why this is a symbol match and not
/// a token grep); a static <c>System.Reflection.Assembly.Load*</c> (<c>Load</c>, <c>LoadFrom</c>,
/// <c>LoadFile</c>, <c>UnsafeLoadFrom</c>, <c>LoadWithPartialName</c>), invoked or passed as a method
/// group; <c>AssemblyLoadContext.Default</c>; or the construction of an <c>AssemblyLoadContext</c>
/// (or a type derived from it). Every candidate the scan inspects must bind: an unresolved candidate is
/// a red, never a skip, so a degraded compilation cannot pass by finding nothing.</para>
///
/// <para><b>Two lists</b>, as <c>StaticStateConformanceTests</c> has. <see cref="Roster"/> is
/// permanent: each row is key-pure or self-owned and says why. <see cref="Allowlist"/> is drain-on-fix:
/// each row is a process-wide resolution site citing #2250, deleted when P54 Phase 2 routes it through
/// the registry's visibility (a site that seam itself keeps becomes a Roster row). Rows are keyed by
/// file + enclosing member + site kind with a site COUNT — never a line number, which drifts. An
/// unlisted key, a count mismatch, and a stale row (no site) are each red, and the totals are anchored
/// to the literal census measured at the guard's birth (re-anchored as rows drain).</para>
/// </summary>
public class ProcessWideResolutionScanTests
{
    private const string CompilerProjectDir = "src/Sharpy.Compiler";
    private const string CompilerCsproj = "src/Sharpy.Compiler/Sharpy.Compiler.csproj";
    private const string SynthesizedGlobalUsingsPath = "GlobalUsings.synthesized.cs";

    // Site kinds — the rows' third key component.
    private const string AppDomainScan = "AppDomain.GetAssemblies";
    private const string StaticTypeGetType = "Type.GetType";
    private const string NewLoadContext = "new AssemblyLoadContext";
    private const string DefaultLoadContext = "AssemblyLoadContext.Default";

    /// <summary>
    /// Names whose binding the scan inspects — the symbol match needs a candidate set, and every
    /// spelling of a listed member reaches the binder through one of these simple names.
    /// </summary>
    private static readonly ImmutableHashSet<string> CandidateNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetAssemblies", "GetType", "Load", "LoadFrom", "LoadFile", "UnsafeLoadFrom",
        "LoadWithPartialName", "Default");

    /// <summary>
    /// Permanent rows: <c>file::Type.Member::kind</c> → (site count, why the site is not a process-wide
    /// resolution). Key-pure or self-owned only.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (int Count, string Reason)> Roster =
        new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["AssemblyCompiler.cs::AssemblyCompiler.GetMetadataReferences::Assembly.Load"] = (1,
                "Assembly.Load(\"netstandard\") — a framework simple name binds to the trusted-platform copy " +
                "in the default context, a process constant; only its Location is read, to add a reference."),
            ["Services/ReplSession.cs::ReplSession.BuildMetadataReferences::Assembly.Load"] = (3,
                "Assembly.Load(\"System.Runtime\" / \"System.Collections\" / \"netstandard\") — framework " +
                "simple names bind to the trusted-platform copies, a process constant; only Location is read."),
            ["Services/ReplSession.cs::ReplSession.ExecuteAssembly::Assembly.Load"] = (1,
                "Assembly.Load(bytes) of the REPL's OWN emitted submission — nothing resolves it by name; " +
                "the session owns it. That a later process-wide scan can see it is the Allowlist sites' " +
                "defect, cured at the visibility seam; REPL isolation itself is #2310."),
            ["Project/ProjectCompiler.Generators.cs::ProjectCompiler.CompileGeneratorAssembly::new AssemblyLoadContext"] = (1,
                "the collectible SharplyGenerators context — self-owned, loads only the generator assembly " +
                "this project just compiled (LoadFromStream); the precedent for the per-registry context " +
                "of P54 Design Decision 2."),

            // ---- The ONE visibility seam (#2250, P54 Design Decision 1): every CLR-name question the
            //      compiler asks is answered here, from {framework} ∪ {the registry's own references}. ----
            ["Discovery/AssemblyVisibility.cs::AssemblyVisibility.TypeNameResolution::Type.GetType"] = (1,
                "the one seam: the runtime's type-name parser called with BOTH resolvers supplied — the " +
                "assembly qualifier binds against the visible set by simple name and an unqualified name is " +
                "searched in the visible set, so nothing binds against the process."),
            ["Discovery/AssemblyVisibility.cs::AssemblyVisibility.LoadFrameworkAssembly::Assembly.Load"] = (1,
                "the one seam: Assembly.Load(AssemblyName) of a trusted-platform System.*/Microsoft.*/netstandard/" +
                "mscorlib name (or a compiler dependency), once per process behind the framework Lazy — the " +
                "default context's TPA binding, a process constant."),
            ["Discovery/AssemblyVisibility.cs::AssemblyVisibility.IsProcessStable::AssemblyLoadContext.Default"] = (1,
                "the one seam: Design Decision 3's gate — a static Type-keyed cache holds a type only when it is " +
                "wholly in the default context, so no static cache pins a per-registry load context."),
        };

    /// <summary>
    /// Drain-on-fix rows: every process-wide type-resolution site @ 486c9a88a. Each drains in P54
    /// Phase 2 (#2250) when it answers from the compilation's registry visibility instead.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (int Count, string Reason)> Allowlist =
        new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["Discovery/Caching/AssemblyIdentity.cs::AssemblyIdentity.FromAssemblyPath::Assembly.LoadFrom"] = (1,
                "#2250 drains in Phase 2 task 2 — DEAD code (zero callers) that would load into the default context — deleted, not rewritten."),
            ["Semantic/Registry/ModuleRegistry.cs::ModuleRegistry.BindAlreadyLoadedCopy::Assembly.Load"] = (1,
                "#2250 drains in Phase 2 task 2 — TRANSITIONAL: binds the default-context copy a same-named load from another path collided with, so task 1 changes no program's answer."),
            ["Semantic/Registry/ModuleRegistry.cs::ModuleRegistry.LoadReference::Assembly.LoadFrom"] = (1,
                "#2250 drains in Phase 2 task 2 — a user reference loaded into the default context: same-named copy from another path is SPY0306, the first copy stays visible to later compiles."),
        };

    private readonly ITestOutputHelper _output;

    public ProcessWideResolutionScanTests(ITestOutputHelper output) => _output = output;

    internal sealed record Site(string File, string Enclosing, string Kind, int Line)
    {
        public string Key => $"{File}::{Enclosing}::{Kind}";
    }

    internal sealed record UnresolvedCandidate(string File, int Line, string Text, string Why);

    internal sealed record ScanResult(IReadOnlyList<Site> Sites, IReadOnlyList<UnresolvedCandidate> Unresolved);

    private static readonly Lazy<IReadOnlyList<MetadataReference>> CompilerReferences = new(() =>
    {
        var csproj = Path.GetFullPath(Path.Combine(DispatchSiteScan.FindRepoRoot(), CompilerCsproj));
        return DispatchSiteScan.BuildReferences(
            DispatchSiteScan.ReadAliasMap(csproj), DispatchSiteScan.GetAssemblyName(csproj));
    });

    private static readonly Lazy<ScanResult> CompilerScan = new(ScanCompilerSources);

    private static ScanResult ScanCompilerSources()
    {
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        var projectDir = Path.GetFullPath(Path.Combine(repoRoot, CompilerProjectDir));
        var csproj = Path.GetFullPath(Path.Combine(repoRoot, CompilerCsproj));
        var sep = Path.DirectorySeparatorChar;

        var trees = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}obj{sep}"))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => Parse(File.ReadAllText(f), f))
            .ToList();
        trees.Add(Parse(DispatchSiteScan.BuildGlobalUsingsSource(csproj), SynthesizedGlobalUsingsPath));

        return Scan(trees, CompilerReferences.Value, projectDir);
    }

    private static SyntaxTree Parse(string source, string path) =>
        CSharpSyntaxTree.ParseText(
            source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: path);

    /// <summary>
    /// The ONE classifier — the compiler census and every synthetic control run through it.
    /// </summary>
    internal static ScanResult Scan(IReadOnlyList<SyntaxTree> trees, IEnumerable<MetadataReference> references, string root)
    {
        var compilation = CSharpCompilation.Create(
            "ProcessWideResolutionScan",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var loadContextType = compilation.GetTypeByMetadataName("System.Runtime.Loader.AssemblyLoadContext");
        var sites = new List<Site>();
        var unresolved = new List<UnresolvedCandidate>();

        // Without the load-context type the construction and Default arms are blind: a degraded
        // reference set, reported rather than silently scanned around.
        if (loadContextType == null)
        {
            unresolved.Add(new UnresolvedCandidate(
                "<references>", 0, "System.Runtime.Loader.AssemblyLoadContext", "type not found"));
        }

        // Source types deriving (by spelling) from AssemblyLoadContext, so an explicit
        // `new DerivedContext(...)` is a construction candidate too; the semantic check decides.
        var contextTypeNames = new HashSet<string>(StringComparer.Ordinal) { "AssemblyLoadContext" };
        foreach (var tree in trees)
        {
            foreach (var decl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (decl.BaseList?.Types.Any(t => RightmostName(t.Type) == "AssemblyLoadContext") == true)
                    contextTypeNames.Add(decl.Identifier.Text);
            }
        }

        foreach (var tree in trees)
        {
            if (tree.FilePath == SynthesizedGlobalUsingsPath)
                continue;

            var model = compilation.GetSemanticModel(tree);
            var file = Path.GetRelativePath(root, tree.FilePath).Replace('\\', '/');

            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                switch (node)
                {
                    case SimpleNameSyntax name when CandidateNames.Contains(name.Identifier.Text):
                        ClassifyName(name, model, file, sites, unresolved);
                        break;
                    case ObjectCreationExpressionSyntax creation
                        when contextTypeNames.Contains(RightmostName(creation.Type)):
                        ClassifyCreation(creation, model, loadContextType, file, sites, unresolved);
                        break;
                    case ImplicitObjectCreationExpressionSyntax implicitCreation:
                        ClassifyCreation(implicitCreation, model, loadContextType, file, sites, unresolved);
                        break;
                }
            }
        }

        return new ScanResult(sites, unresolved);
    }

    private static void ClassifyName(
        SimpleNameSyntax name, SemanticModel model, string file, List<Site> sites, List<UnresolvedCandidate> unresolved)
    {
        // A named argument (`Default: x`) names a parameter, not a member.
        if (name.Parent is NameColonSyntax)
            return;

        var info = model.GetSymbolInfo(name);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        if (symbol == null)
        {
            unresolved.Add(new UnresolvedCandidate(
                file, LineOf(name), name.Parent?.ToString() ?? name.ToString(), info.CandidateReason.ToString()));
            return;
        }

        var kind = KindOf(symbol);
        if (kind != null)
            sites.Add(new Site(file, DispatchSiteScan.EnclosingContext(name), kind, LineOf(name)));
    }

    private static void ClassifyCreation(
        BaseObjectCreationExpressionSyntax creation, SemanticModel model, INamedTypeSymbol? loadContextType,
        string file, List<Site> sites, List<UnresolvedCandidate> unresolved)
    {
        var type = model.GetTypeInfo(creation).Type;
        if (type == null || type.TypeKind == TypeKind.Error)
        {
            unresolved.Add(new UnresolvedCandidate(file, LineOf(creation), creation.ToString(), "creation type unbound"));
            return;
        }

        if (loadContextType != null && DerivesFrom(type, loadContextType))
            sites.Add(new Site(file, DispatchSiteScan.EnclosingContext(creation), NewLoadContext, LineOf(creation)));
    }

    /// <summary>The site kind a bound symbol denotes, or null when it is not a listed member.</summary>
    internal static string? KindOf(ISymbol symbol)
    {
        var containing = symbol.ContainingType?.ToDisplayString();
        return symbol switch
        {
            IMethodSymbol { Name: "GetAssemblies" } when containing == "System.AppDomain" => AppDomainScan,
            IMethodSymbol { Name: "GetType", IsStatic: true } when containing == "System.Type" => StaticTypeGetType,
            IMethodSymbol { IsStatic: true } m when containing == "System.Reflection.Assembly"
                && (m.Name.StartsWith("Load", StringComparison.Ordinal) || m.Name == "UnsafeLoadFrom")
                => "Assembly." + m.Name,
            IPropertySymbol { Name: "Default", IsStatic: true }
                when containing == "System.Runtime.Loader.AssemblyLoadContext" => DefaultLoadContext,
            _ => null,
        };
    }

    private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }
        return false;
    }

    private static string RightmostName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        AliasQualifiedNameSyntax a => a.Name.Identifier.Text,
        SimpleNameSyntax s => s.Identifier.Text,
        _ => type.ToString(),
    };

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    // ---- The census ----

    [Fact]
    public void CompilerSources_EveryProcessWideResolutionSite_IsRosteredOrAllowlisted()
    {
        var result = CompilerScan.Value;

        foreach (var u in result.Unresolved)
            _output.WriteLine($"UNRESOLVED {u.File}:{u.Line} [{u.Why}] {u.Text}");
        foreach (var s in result.Sites.OrderBy(s => s.Key, StringComparer.Ordinal).ThenBy(s => s.Line))
            _output.WriteLine($"SITE {s.Key} @ line {s.Line}");

        Assert.True(result.Unresolved.Count == 0,
            "The scan's compilation is degraded: candidate names did not bind, so a site could hide behind " +
            "an error type. Supply the missing reference/global using:\n  " +
            string.Join("\n  ", result.Unresolved.Select(u => $"{u.File}:{u.Line} [{u.Why}] {u.Text}")));

        var overlap = Roster.Keys.Intersect(Allowlist.Keys, StringComparer.Ordinal).ToList();
        Assert.True(overlap.Count == 0, "A key is in both Roster and Allowlist:\n  " + string.Join("\n  ", overlap));

        var byKey = result.Sites.GroupBy(s => s.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var violations = new List<string>();
        foreach (var (key, found) in byKey.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var lines = string.Join(",", found.Select(s => s.Line));
            if (Roster.TryGetValue(key, out var row) || Allowlist.TryGetValue(key, out row))
            {
                if (row.Count != found.Count)
                    violations.Add($"[count {found.Count} != row {row.Count}] {key} (lines {lines})");
            }
            else
            {
                violations.Add($"[unlisted] {key} (lines {lines})");
            }
        }

        var stale = Roster.Keys.Concat(Allowlist.Keys).Where(k => !byKey.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(violations.Count == 0,
            "Process-wide type-resolution / assembly-load sites in the compiler that no row accounts for " +
            "(#2250). Route the lookup through the compilation's registry; a key-pure site is a Roster row " +
            "with its justification:\n  " + string.Join("\n  ", violations));
        Assert.True(stale.Count == 0,
            "Stale rows (no matching site — the site drained or moved; delete or re-key the row):\n  " +
            string.Join("\n  ", stale));

        // Literal census @ 486c9a88a (measured): 14 AppDomain scans + 7 static Type.GetType + 4
        // Assembly.LoadFrom = 25 resolution sites; 5 Assembly.Load + 1 load-context construction = 6
        // rostered sites. P54 Phase 2 task 1 (measured): the 14 scans and the 6 non-seam Type.GetType
        // sites drained into AssemblyVisibility, whose own three seam sites are Roster rows; 2 LoadFrom
        // sites remain for task 2, plus task 1's one transitional Assembly.Load. Re-anchor as rows drain.
        Assert.Equal(0, result.Sites.Count(s => s.Kind == AppDomainScan));
        Assert.Equal(1, result.Sites.Count(s => s.Kind == StaticTypeGetType));
        Assert.Equal(3, result.Sites.Count(s => Allowlist.ContainsKey(s.Key)));
        Assert.Equal(9, result.Sites.Count(s => Roster.ContainsKey(s.Key)));
        Assert.Equal(12, result.Sites.Count);
    }

    // ---- Synthetic controls: the same classifier on in-memory sources ----

    private static ScanResult ScanSynthetic(string source)
    {
        var root = Path.Combine(Path.GetTempPath(), "process-wide-scan-synthetic");
        return Scan(new[] { Parse(source, Path.Combine(root, "Probe.cs")) }, CompilerReferences.Value, root);
    }

    [Fact]
    public void Synthetic_AppDomainScan_IsFound()
    {
        var result = ScanSynthetic("""
            namespace Probe;
            internal static class ProbeSites
            {
                internal static int Scan() => System.AppDomain.CurrentDomain.GetAssemblies().Length;
            }
            """);

        Assert.Empty(result.Unresolved);
        var site = Assert.Single(result.Sites);
        Assert.Equal("Probe.cs::ProbeSites.Scan::AppDomain.GetAssemblies", site.Key);
    }

    /// <summary>
    /// The instance <c>object.GetType()</c> is not a resolution site; the STATIC <c>Type.GetType</c> in
    /// the same source is the positive control that the absence is the symbol match, not a blind scan.
    /// </summary>
    [Fact]
    public void Synthetic_InstanceGetType_IsNotFound_StaticTypeGetType_Is()
    {
        var result = ScanSynthetic("""
            namespace Probe;
            internal static class ProbeSites
            {
                internal static string Instance(object x) => x.GetType().Name;
                internal static string OnType() => typeof(ProbeSites).GetType().Name;
                internal static System.Type? Static() => System.Type.GetType("System.Int32");
            }
            """);

        Assert.Empty(result.Unresolved);
        var site = Assert.Single(result.Sites);
        Assert.Equal("Probe.cs::ProbeSites.Static::Type.GetType", site.Key);
    }

    /// <summary>One synthetic cell per remaining site kind and spelling — each arm is falsifiable.</summary>
    [Theory]
    [InlineData("internal static object M() => System.Reflection.Assembly.LoadFrom(\"x.dll\");", "Assembly.LoadFrom")]
    [InlineData("internal static object M() => System.Reflection.Assembly.Load(\"netstandard\");", "Assembly.Load")]
    [InlineData("internal static object M() => System.Reflection.Assembly.LoadFile(\"/x.dll\");", "Assembly.LoadFile")]
    [InlineData("internal static object M() => System.Linq.Enumerable.Select(new string[0], System.Reflection.Assembly.LoadFrom);", "Assembly.LoadFrom")]
    [InlineData("internal static object M() => System.Runtime.Loader.AssemblyLoadContext.Default;", "AssemblyLoadContext.Default")]
    [InlineData("internal static object M() => new System.Runtime.Loader.AssemblyLoadContext(\"p\", isCollectible: true);", "new AssemblyLoadContext")]
    [InlineData("internal static object M() { System.Runtime.Loader.AssemblyLoadContext c = new(\"p\"); return c; }", "new AssemblyLoadContext")]
    [InlineData("internal static object M() => new Derived(); private sealed class Derived : System.Runtime.Loader.AssemblyLoadContext { }", "new AssemblyLoadContext")]
    [InlineData("internal static object M() { var d = System.AppDomain.CurrentDomain; return d.GetAssemblies(); }", "AppDomain.GetAssemblies")]
    public void Synthetic_EachSiteKind_IsFound(string member, string kind)
    {
        var result = ScanSynthetic($$"""
            namespace Probe;
            internal static class ProbeSites
            {
                {{member}}
            }
            """);

        Assert.Empty(result.Unresolved);
        var site = Assert.Single(result.Sites);
        Assert.Equal($"Probe.cs::ProbeSites.M::{kind}", site.Key);
    }

    /// <summary>
    /// A candidate that does not bind is reported, not skipped — the census's degraded-compilation
    /// red. The bound <c>AppDomain</c> scan beside it is still found (positive control).
    /// </summary>
    [Fact]
    public void Synthetic_UnboundCandidate_IsReportedUnresolved()
    {
        var result = ScanSynthetic("""
            namespace Probe;
            internal static class ProbeSites
            {
                internal static object Unbound() => Missing.Domain.GetAssemblies();
                internal static int Bound() => System.AppDomain.CurrentDomain.GetAssemblies().Length;
            }
            """);

        var unresolved = Assert.Single(result.Unresolved);
        Assert.Equal(4, unresolved.Line);
        var site = Assert.Single(result.Sites);
        Assert.Equal("Probe.cs::ProbeSites.Bound::AppDomain.GetAssemblies", site.Key);
    }
}
