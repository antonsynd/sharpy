using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Conformance tests enforcing the front-end options seam (#1144): the same source, presented with
/// equivalent options, must produce the same diagnostics through every entry point — which is only
/// achievable if there is exactly one place options are constructed.
///
/// <para>
/// The seam has three surfaces, and a new setting has to be decided on all three or an entry point
/// silently compiles with something else:
/// <list type="number">
///   <item><see cref="CompilerOptions"/> construction — guarded by
///     <see cref="FactoryCore_AssignsEverySettableCompilerOptionsMember"/> (a new member must be
///     assigned by the factory core) and
///     <see cref="CompilerOptions_IsConstructedOnlyBySeam"/> (nobody else may build one).</item>
///   <item>The <see cref="CompilerOptions"/>→<see cref="ProjectConfig"/> mapping — the synthetic
///     project-of-one-file conversion, exercised behaviorally by the front-end parity sweep.</item>
///   <item>The flag set <see cref="ProjectCompiler"/> runs with — guarded by
///     <see cref="ProjectCompiler_TakesItsFlagsAsOneOptionsValue"/> and
///     <see cref="ProjectCompilerOptions_AreProducedOnlyByTheMerge"/>.</item>
/// </list>
/// </para>
///
/// <para>
/// History: #1097 (<c>emit csharp</c> dropped <c>--enable-feature</c>) and #1109 (the LSP
/// <c>.spyproj</c> path dropped warnings/maxErrors/features) are the same defect one batch apart —
/// a flag threaded N times and forgotten once. #1061 is the duplicated-parsing tax of the same
/// shape. The parity sweep (<c>FrontEndParityTests</c>) detects the drift after the fact; these
/// tests make the drift unrepresentable, which is #1144's acceptance criterion.
/// </para>
/// </summary>
public class CompilerOptionsSeamConformanceTests
{
    private const string FactoryFile = "CompilerOptionsFactory.cs";
    private const string MergeFile = "ProjectOptionsMerge.cs";
    private const string FactoryCoreMethod = "Create";

    /// <summary>
    /// Projects whose <see cref="CompilerOptions"/> construction is the product's own. Test projects
    /// are deliberately out of scope: a test that pins an arbitrary options shape is exercising the
    /// compiler, not shipping an entry point.
    /// </summary>
    private static readonly string[] ScannedProjects =
    {
        "Sharpy.Compiler",
        "Sharpy.Cli",
        "Sharpy.Lsp",
        "Sharpy.Playground",
        "Sharpy.TestInfrastructure",
        "Sharpy.Compiler.Benchmarks",
    };

    /// <summary>
    /// Files allowed to construct <see cref="CompilerOptions"/> directly, with the reason.
    /// Justification-only: an entry point never belongs here — it calls the factory.
    /// </summary>
    private static readonly Dictionary<string, string> ConstructionAllowlist = new(StringComparer.Ordinal)
    {
        [FactoryFile] = "The seam itself — the one place CompilerOptions members are assigned.",

        ["IntegrationTestBase.cs"] =
            "Baseline constructor: the fixture harness must state an options shape independent of "
            + "what any product surface decides, or the suite would agree with the surfaces by "
            + "construction and stop detecting their drift.",

        ["LineDirectiveSeamBenchmarks.cs"] =
            "Baseline constructor: the benchmark pins an exact options shape so measurements stay "
            + "comparable across releases.",
    };

    /// <summary>
    /// Every settable <see cref="CompilerOptions"/> member must be assigned by the factory core, or
    /// be named in <c>CompilerOptionsFactory.ExemptMembers</c> with a written reason. Adding a
    /// member without touching the factory fails here, which forces its author to decide the value
    /// for every entry surface at one screen of code.
    /// </summary>
    [Fact]
    public void FactoryCore_AssignsEverySettableCompilerOptionsMember()
    {
        var assigned = AssignmentsInFactoryCore();

        // Sanity: a scan that stopped finding the initializer would otherwise "pass" by finding
        // nothing to check.
        assigned.Count.Should().BeGreaterThan(5,
            $"the scan must find {FactoryCoreMethod}'s object initializer in {FactoryFile}");

        var settable = typeof(CompilerOptions)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToList();

        settable.Count.Should().BeGreaterThan(5,
            "the reflection sweep must see the CompilerOptions surface");

        var missing = settable
            .Where(name => !assigned.Contains(name))
            .Where(name => !CompilerOptionsFactory.ExemptMembers.ContainsKey(name))
            .ToList();

        missing.Should().BeEmpty(
            $"every settable CompilerOptions member must be assigned by {FactoryFile}'s "
            + $"{FactoryCoreMethod} core, so a new member is decided for every entry surface in one "
            + "place. Assign it there (and give the per-surface methods a parameter for it), or add "
            + "it to CompilerOptionsFactory.ExemptMembers with the reason it has no per-surface "
            + $"decision.\nUnassigned: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Exemptions are a reviewed decision, not a dumping ground: each must name a real member and
    /// carry a written reason.
    /// </summary>
    [Fact]
    public void FactoryExemptions_NameRealMembersAndCarryReasons()
    {
        var settable = typeof(CompilerOptions)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (member, reason) in CompilerOptionsFactory.ExemptMembers)
        {
            settable.Should().Contain(member,
                $"exemption '{member}' names a CompilerOptions member that no longer exists — delete it");
            reason.Should().NotBeNullOrWhiteSpace($"exemption '{member}' must say why it is exempt");
        }
    }

    /// <summary>
    /// No entry point may build its own <see cref="CompilerOptions"/>. Per-entry-point construction
    /// is the root cause the umbrella names: it is what makes a new flag something to remember N
    /// times.
    /// </summary>
    [Fact]
    public void CompilerOptions_IsConstructedOnlyBySeam()
    {
        var sites = new List<string>();
        var allowlistedHits = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (fileName, root) in EnumerateSyntaxTrees(text => text.Contains("CompilerOptions", StringComparison.Ordinal)))
        {
            foreach (var node in root.DescendantNodes())
            {
                if (!ConstructsCompilerOptions(node))
                    continue;

                if (ConstructionAllowlist.ContainsKey(fileName))
                {
                    allowlistedHits.Add(fileName);
                    continue;
                }

                var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                sites.Add($"{fileName}:{line}");
            }
        }

        sites.Should().BeEmpty(
            "CompilerOptions must be built by CompilerOptionsFactory so every entry point threads "
            + "the same members. Call the per-surface method for this entry point (ForCli, "
            + "ForProject, ForLsp, ForRepl, ForLibraryAnalysis, Default) — adding a per-site "
            + "initializer is how #1097 and #1109 happened. If the site really is a baseline "
            + "constructor, add its file to ConstructionAllowlist with the reason.\nSites:\n"
            + string.Join("\n", sites));

        // Sanity: the allowlist must still describe reality, or the scan has quietly stopped
        // matching and this test is passing vacuously.
        allowlistedHits.Should().BeEquivalentTo(ConstructionAllowlist.Keys,
            "every allowlisted file must still contain the construction it was excused for — "
            + "drain entries that no longer apply, and check the scan still matches if none do");
    }

    /// <summary>
    /// <see cref="ProjectCompiler"/> must take its flags as one options value. They used to
    /// be five loose constructor parameters (<c>warningsAsErrors</c>, <c>suppressedWarnings</c>,
    /// <c>maxErrors</c>, <c>incremental</c>, <c>features</c>) that every call site re-threaded by
    /// hand — the exact shape that let the LSP <c>.spyproj</c> path lose three of them (#1109).
    /// </summary>
    [Fact]
    public void ProjectCompiler_TakesItsFlagsAsOneOptionsValue()
    {
        var constructors = typeof(ProjectCompiler)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        constructors.Should().NotBeEmpty("the reflection sweep must see the ProjectCompiler surface");

        var permitted = new[]
        {
            typeof(ICompilerLogger),
            typeof(ModuleRegistry),
            typeof(ICodeEmitterFactory),
            typeof(ProjectCompilerOptions),
        };

        var loose = constructors
            .SelectMany(c => c.GetParameters(), (c, p) => (Ctor: c, Param: p))
            .Where(x => !permitted.Contains(x.Param.ParameterType))
            .Select(x => $"{x.Param.ParameterType.Name} {x.Param.Name}")
            .ToList();

        loose.Should().BeEmpty(
            "a compilation setting must reach ProjectCompiler inside ProjectCompilerOptions, not as "
            + "its own constructor parameter — loose parameters are re-threaded at every call site "
            + "and forgotten at one of them (#1109). Add the setting to ProjectCompilerOptions and "
            + $"populate it in ProjectOptionsMerge.Merge.\nLoose parameters: {string.Join(", ", loose)}");
    }

    /// <summary>
    /// <see cref="ProjectCompilerOptions"/> values come only from <see cref="ProjectOptionsMerge"/>.
    /// A hand-built value elsewhere would be a second, unreviewed answer to "how do option-level and
    /// project-level settings combine" — which is what the merge exists to prevent.
    /// </summary>
    [Fact]
    public void ProjectCompilerOptions_AreProducedOnlyByTheMerge()
    {
        var sites = new List<string>();
        var sawTheProducer = false;

        foreach (var (fileName, root) in EnumerateSyntaxTrees(
            text => text.Contains(nameof(ProjectCompilerOptions), StringComparison.Ordinal)))
        {
            foreach (var node in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (!NamesType(node.Type, nameof(ProjectCompilerOptions)))
                    continue;

                if (fileName == MergeFile)
                {
                    sawTheProducer = true;
                    continue;
                }

                var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                sites.Add($"{fileName}:{line}");
            }
        }

        sawTheProducer.Should().BeTrue(
            $"{MergeFile} must still construct the value — otherwise this scan matches nothing");

        sites.Should().BeEmpty(
            "ProjectCompilerOptions must come from ProjectOptionsMerge.Merge (or its Default), the "
            + "one definition of how compiler-level and project-level settings combine.\nSites:\n"
            + string.Join("\n", sites));
    }

    // --- Detection helpers ----------------------------------------------------------------------

    /// <summary>
    /// The member names assigned in the object initializer of the factory core, read from source so
    /// the guard sees what the code actually does rather than what a list claims.
    /// </summary>
    private static HashSet<string> AssignmentsInFactoryCore()
    {
        var path = Path.Combine(FindSourceDir("Sharpy.Compiler"), FactoryFile);
        File.Exists(path).Should().BeTrue($"the options seam must live at {path}");

        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
        var core = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(m => m.Identifier.Text == FactoryCoreMethod
                                  && m.ReturnType.ToString() == nameof(CompilerOptions));

        core.Should().NotBeNull(
            $"{FactoryFile} must have exactly one {FactoryCoreMethod} method returning "
            + $"{nameof(CompilerOptions)} — the construction core the guard reads");

        return core!.DescendantNodes()
            .OfType<InitializerExpressionSyntax>()
            .SelectMany(init => init.Expressions.OfType<AssignmentExpressionSyntax>())
            .Select(a => a.Left)
            .OfType<IdentifierNameSyntax>()
            .Select(id => id.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// True if the node builds a <see cref="CompilerOptions"/> — either explicitly
    /// (<c>new CompilerOptions { … }</c>) or target-typed (<c>= new() { … }</c> on a field, local,
    /// or property declared as <see cref="CompilerOptions"/>; the LSP workspace hardcode was
    /// written that way, so a scan that only matched the explicit form would have missed it).
    /// </summary>
    private static bool ConstructsCompilerOptions(SyntaxNode node) => node switch
    {
        ObjectCreationExpressionSyntax explicitNew => NamesType(explicitNew.Type, nameof(CompilerOptions)),
        ImplicitObjectCreationExpressionSyntax implicitNew => DeclaredTypeOf(implicitNew) is { } type
            && NamesType(type, nameof(CompilerOptions)),
        _ => false,
    };

    /// <summary>The syntactic type a target-typed <c>new()</c> is being assigned into, if visible.</summary>
    private static TypeSyntax? DeclaredTypeOf(SyntaxNode implicitNew)
    {
        foreach (var ancestor in implicitNew.Ancestors())
        {
            switch (ancestor)
            {
                case VariableDeclarationSyntax variable:
                    return variable.Type;
                case PropertyDeclarationSyntax property:
                    return property.Type;
                case MethodDeclarationSyntax method:
                    return method.ReturnType;
                case BaseTypeDeclarationSyntax:
                    return null; // left the member without finding a declared type
            }
        }

        return null;
    }

    private static bool NamesType(TypeSyntax type, string name) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text == name,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text == name,
        _ => false,
    };

    // --- File discovery -------------------------------------------------------------------------

    private static IEnumerable<(string FileName, CompilationUnitSyntax Root)> EnumerateSyntaxTrees(
        Func<string, bool> preFilter)
    {
        foreach (var project in ScannedProjects)
        {
            var dir = FindSourceDir(project);
            if (!Directory.Exists(dir))
                continue;

            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                var text = File.ReadAllText(file);
                if (!preFilter(text))
                    continue;

                var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot();
                yield return (Path.GetFileName(file), root);
            }
        }
    }

    private static string FindSourceDir(string project)
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", project);
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", project));
    }
}
