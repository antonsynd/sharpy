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
///     project-of-one-file conversion, guarded by
///     <see cref="BuildConfig_ReadsEverySettableCompilerOptionsMember"/> (a new member must be
///     mapped onto the config or exempted because it reaches the compiler another way) and
///     exercised behaviorally by the front-end parity sweep.</item>
///   <item>The flag set <see cref="ProjectCompiler"/> runs with — guarded by
///     <see cref="ProjectCompiler_TakesItsFlagsAsOneOptionsValue"/> and
///     <see cref="ProjectCompilerOptions_AreProducedOnlyByTheMerge"/>.</item>
/// </list>
/// </para>
///
/// <para>
/// A fourth guard, <see cref="LspAnalysisInputs_CompareEverySettableCompilerOptionsMember"/>, covers
/// the seam's one option-shaped consumer decision: which members the LSP compares to decide that a
/// cached analysis is still valid.
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
    private const string SyntheticProjectFile = "SyntheticProject.cs";
    private const string WorkspaceFile = "SharpyWorkspace.cs";
    private const string FactoryCoreMethod = "Create";
    private const string LspFactoryMethod = "ForLsp";
    private const string BuildConfigMethod = "BuildConfig";
    private const string MergeMethod = "Merge";
    private const string AnalysisInputsMethod = "SameAnalysisInputs";

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
    /// Files allowed to produce a <see cref="ProjectCompilerOptions"/> value outside
    /// <see cref="ProjectOptionsMerge"/> — by construction or by <c>with</c>-shaping a copy — with
    /// the reason. Justification-only, on the same terms as <see cref="ConstructionAllowlist"/>: a
    /// product entry point never belongs here, because a hand-shaped value is a second answer to
    /// "how do option-level and project-level settings combine".
    /// </summary>
    private static readonly Dictionary<string, string> ShapingAllowlist = new(StringComparer.Ordinal)
    {
        ["IntegrationTestBase.cs"] =
            "Baseline constructor (same rationale as its CompilerOptions exemption): the fixture "
            + "harness shapes ProjectCompilerOptions.Default with the fixture's `.features` sidecar "
            + "so it states a flag set independent of what any product surface decides. If it went "
            + "through the merge it would agree with the surfaces by construction and stop detecting "
            + "their drift.",
    };

    /// <summary>
    /// <see cref="CompilerOptions"/> members that <c>SyntheticProject.BuildConfig</c> deliberately
    /// does not map onto the synthetic <see cref="ProjectConfig"/>, each naming where it reaches the
    /// compiler instead. Every one of these travels to <see cref="ProjectCompiler"/> inside the
    /// <see cref="ProjectCompilerOptions"/> value <see cref="ProjectOptionsMerge"/> derives straight
    /// from <see cref="CompilerOptions"/> — which
    /// <see cref="BuildConfigExemptions_AreThreadedThroughTheMerge"/> verifies rather than takes on
    /// trust, so an exemption cannot outlive the threading it claims.
    /// </summary>
    private static readonly Dictionary<string, string> BuildConfigExemptions = new(StringComparer.Ordinal)
    {
        ["MaxErrors"] =
            "ProjectConfig has no counterpart; the limit reaches the compiler as "
            + "ProjectCompilerOptions.MaxErrors via ProjectOptionsMerge.Merge.",

        ["Incremental"] =
            "The incremental cache is keyed on a real project's obj/ directory, so the "
            + "project-of-one-file never uses it: SyntheticProject.Analyze calls "
            + "ProjectOptionsMerge.Merge with incremental:false, and project mode gets the flag as "
            + "ProjectCompilerOptions.Incremental from the same merge.",

        ["Features"] =
            "ProjectConfig.Features is the .spyproj's own <Features> list, which the merge widens "
            + "the options with; the synthetic config has no independent list to contribute, so the "
            + "option set reaches the compiler as ProjectCompilerOptions.Features via "
            + "ProjectOptionsMerge.Merge. BuildConfig hands the whole options value to its "
            + "import-closure scan, which lexes/parses with the same flags — that is a use of the "
            + "features, not a mapping of them onto the config.",
    };

    /// <summary>
    /// <see cref="CompilerOptions"/> members the LSP's <c>SameAnalysisInputs</c> deliberately does
    /// not compare: <see cref="CompilerOptionsFactory.ForLsp"/> never decides them, so they hold the
    /// same default in every options value the workspace can build and comparing them could only
    /// ever be true. <see cref="LspAnalysisInputsExemptions_NameMembersForLspLeavesAtTheirDefault"/>
    /// re-derives that claim from <see cref="FactoryFile"/> instead of trusting these comments, so
    /// giving <c>ForLsp</c> a say over one of these members fails the guard until it is compared.
    /// </summary>
    private static readonly Dictionary<string, string> AnalysisInputsExemptions = new(StringComparer.Ordinal)
    {
        ["Incremental"] =
            "ForLsp never enables it — in-editor analysis emits no assembly and has no obj/ cache.",

        ["Namespace"] =
            "ForLsp never sets it; the namespace override exists for `emit csharp --namespace`.",

        ["Configuration"] =
            "ForLsp never sets it; analysis does not depend on the Debug/Release build configuration.",

        ["AssemblyName"] =
            "ForLsp never sets it; in-editor analysis names no output assembly.",

        ["OutputAssemblyPath"] =
            "ForLsp never sets it; in-editor analysis emits no assembly to place.",
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

        var missing = SettableCompilerOptionsMembers()
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
        var settable = SettableCompilerOptionsMembers().ToHashSet(StringComparer.Ordinal);

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
    /// Surface 2: every settable <see cref="CompilerOptions"/> member must be read by
    /// <c>SyntheticProject.BuildConfig</c> — the one <see cref="CompilerOptions"/>→
    /// <see cref="ProjectConfig"/> mapping — or be named in <see cref="BuildConfigExemptions"/> with
    /// the place it reaches the compiler instead. Surfaces 1 and 3 already fail on an undecided
    /// member; without this, a new member could pass both and still be dropped on the way into the
    /// project-of-one-file every single-file compile and analyze runs through.
    /// </summary>
    [Fact]
    public void BuildConfig_ReadsEverySettableCompilerOptionsMember()
    {
        var mapped = OptionMembersReadBy(
            "Sharpy.Compiler", SyntheticProjectFile, BuildConfigMethod);

        // Sanity: a scan that stopped finding the mapping would otherwise "pass" by finding nothing.
        mapped.Count.Should().BeGreaterThan(5,
            $"the scan must find {BuildConfigMethod}'s options reads in {SyntheticProjectFile}");

        var missing = SettableCompilerOptionsMembers()
            .Where(name => !mapped.Contains(name))
            .Where(name => !BuildConfigExemptions.ContainsKey(name))
            .ToList();

        missing.Should().BeEmpty(
            "every settable CompilerOptions member must be decided for surface 2 — either "
            + $"{SyntheticProjectFile}'s {BuildConfigMethod} maps it onto the synthetic "
            + "ProjectConfig, or it is exempt because it reaches ProjectCompiler through "
            + "ProjectOptionsMerge instead. A member that is decided on neither is silently absent "
            + "from every single-file compile and analyze, which is #1097's shape. Map it, or add it "
            + $"to BuildConfigExemptions naming where it is threaded.\nUnmapped: "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// A surface-2 exemption is a claim that the member reaches the compiler through
    /// <see cref="ProjectOptionsMerge"/> instead. The claim is checked, not trusted: the member must
    /// really be read by the merge, and must not also be mapped by <c>BuildConfig</c> (an exemption
    /// that has been fixed is drained rather than left to rot).
    /// </summary>
    [Fact]
    public void BuildConfigExemptions_AreThreadedThroughTheMerge()
    {
        var settable = SettableCompilerOptionsMembers().ToHashSet(StringComparer.Ordinal);
        var mapped = OptionMembersReadBy("Sharpy.Compiler", SyntheticProjectFile, BuildConfigMethod);
        var merged = OptionMembersReadBy("Sharpy.Compiler", MergeFile, MergeMethod);

        merged.Should().NotBeEmpty(
            $"the scan must find {MergeMethod}'s options reads in {MergeFile}");

        foreach (var (member, reason) in BuildConfigExemptions)
        {
            settable.Should().Contain(member,
                $"exemption '{member}' names a CompilerOptions member that no longer exists — delete it");
            reason.Should().NotBeNullOrWhiteSpace($"exemption '{member}' must say why it is exempt");

            merged.Should().Contain(member,
                $"exemption '{member}' claims the member reaches the compiler through "
                + $"{MergeFile}.{MergeMethod} instead of the config, but the merge never reads it — "
                + "so nothing threads it and the exemption is hiding a dropped setting");

            mapped.Should().NotContain(member,
                $"exemption '{member}' is stale: {BuildConfigMethod} now maps the member onto the "
                + "config, so delete the entry (drain on fix)");
        }
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
    /// <remarks>
    /// <see cref="ProjectCompilerOptions"/> is a record struct, so <c>with</c>-shaping produces a
    /// value just as surely as <c>new</c> does — <c>ProjectCompilerOptions.Default with { Features =
    /// … }</c> is a bypass a <c>new</c>-only scan cannot see. One such site already existed
    /// undetected in the fixture harness when this guard was written; the scan therefore matches
    /// both forms.
    /// </remarks>
    [Fact]
    public void ProjectCompilerOptions_AreProducedOnlyByTheMerge()
    {
        var sites = new List<string>();
        var allowlistedHits = new HashSet<string>(StringComparer.Ordinal);
        var sawTheProducer = false;

        foreach (var (fileName, root) in EnumerateSyntaxTrees(
            text => text.Contains(nameof(ProjectCompilerOptions), StringComparison.Ordinal)))
        {
            foreach (var node in root.DescendantNodes())
            {
                var constructs = node is ObjectCreationExpressionSyntax creation
                    && NamesType(creation.Type, nameof(ProjectCompilerOptions));
                var shapes = node is WithExpressionSyntax with && ShapesProjectCompilerOptions(with);

                if (!constructs && !shapes)
                    continue;

                if (fileName == MergeFile)
                {
                    sawTheProducer |= constructs;
                    continue;
                }

                if (ShapingAllowlist.ContainsKey(fileName))
                {
                    allowlistedHits.Add(fileName);
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
            + "one definition of how compiler-level and project-level settings combine. `Default "
            + "with { … }` counts: it produces a value the merge never saw, so a setting the merge "
            + "would have widened is simply absent. Call the merge — or, if the site really is a "
            + "baseline, add its file to ShapingAllowlist with the reason.\nSites:\n"
            + string.Join("\n", sites));

        // Sanity: the allowlist must still describe reality, or the scan has quietly stopped
        // matching and this test is passing vacuously.
        allowlistedHits.Should().BeEquivalentTo(ShapingAllowlist.Keys,
            "every allowlisted file must still contain the production it was excused for — "
            + "drain entries that no longer apply, and check the scan still matches if none do");
    }

    /// <summary>
    /// The LSP decides a cached analysis is still valid by comparing options with
    /// <c>SharpyWorkspace.SameAnalysisInputs</c>, which reads a hand-picked subset of
    /// <see cref="CompilerOptions"/>. Nothing tied that subset to the options surface, so a new
    /// member that <see cref="CompilerOptionsFactory.ForLsp"/> honors could change the analysis while
    /// comparing equal — leaving the editor showing diagnostics from the old settings until the
    /// buffer is edited. Every settable member must therefore be compared, or be named in
    /// <see cref="AnalysisInputsExemptions"/>.
    /// </summary>
    [Fact]
    public void LspAnalysisInputs_CompareEverySettableCompilerOptionsMember()
    {
        var compared = OptionMembersReadBy("Sharpy.Lsp", WorkspaceFile, AnalysisInputsMethod);

        // Sanity: a scan that stopped finding the comparison would otherwise "pass" by finding
        // nothing.
        compared.Count.Should().BeGreaterThan(3,
            $"the scan must find {AnalysisInputsMethod}'s option comparisons in {WorkspaceFile}");

        var missing = SettableCompilerOptionsMembers()
            .Where(name => !compared.Contains(name))
            .Where(name => !AnalysisInputsExemptions.ContainsKey(name))
            .ToList();

        missing.Should().BeEmpty(
            $"every settable CompilerOptions member must be compared by {WorkspaceFile}'s "
            + $"{AnalysisInputsMethod}, or the LSP can rebuild its options, see 'nothing changed', "
            + "and keep serving analyses produced under the previous settings. Compare it — or, if "
            + "ForLsp genuinely cannot vary it, add it to AnalysisInputsExemptions with that "
            + $"reason.\nUncompared: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Each analysis-inputs exemption claims <see cref="CompilerOptionsFactory.ForLsp"/> leaves the
    /// member at its default and so it can never differ between two workspace option sets. That
    /// claim is re-derived from the factory source: the moment <c>ForLsp</c> gains a say over the
    /// member, the exemption is false and the member must be compared.
    /// </summary>
    [Fact]
    public void LspAnalysisInputsExemptions_NameMembersForLspLeavesAtTheirDefault()
    {
        var settable = SettableCompilerOptionsMembers().ToHashSet(StringComparer.Ordinal);
        var decidedByForLsp = SpecAssignmentsIn(LspFactoryMethod);

        decidedByForLsp.Should().NotBeEmpty(
            $"the scan must find {LspFactoryMethod}'s CompilerOptionsSpec initializer in {FactoryFile}");

        foreach (var (member, reason) in AnalysisInputsExemptions)
        {
            settable.Should().Contain(member,
                $"exemption '{member}' names a CompilerOptions member that no longer exists — delete it");
            reason.Should().NotBeNullOrWhiteSpace($"exemption '{member}' must say why it is exempt");

            decidedByForLsp.Should().NotContain(member,
                $"exemption '{member}' claims ForLsp leaves the member at its default, but ForLsp "
                + "now assigns it — so two workspace option sets can differ in it. Compare it in "
                + $"{AnalysisInputsMethod} and delete the exemption");
        }
    }

    // --- Detection helpers ----------------------------------------------------------------------

    /// <summary>The settable <see cref="CompilerOptions"/> surface every completeness guard is measured against.</summary>
    private static List<string> SettableCompilerOptionsMembers()
    {
        var settable = typeof(CompilerOptions)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToList();

        settable.Count.Should().BeGreaterThan(5,
            "the reflection sweep must see the CompilerOptions surface");

        return settable;
    }

    /// <summary>
    /// The member names assigned in the object initializer of the factory core, read from source so
    /// the guard sees what the code actually does rather than what a list claims.
    /// </summary>
    private static HashSet<string> AssignmentsInFactoryCore() => SpecAssignmentsIn(FactoryCoreMethod);

    /// <summary>
    /// The member names assigned in the object initializers inside one
    /// <see cref="CompilerOptions"/>-returning factory method — the core's own
    /// <see cref="CompilerOptions"/> initializer, or a per-surface method's
    /// <c>CompilerOptionsSpec</c>, which is that surface's list of members it has a say over.
    /// </summary>
    private static HashSet<string> SpecAssignmentsIn(string methodName)
    {
        var root = ParseSourceFile("Sharpy.Compiler", FactoryFile);
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(m => m.Identifier.Text == methodName
                                  && m.ReturnType.ToString() == nameof(CompilerOptions));

        method.Should().NotBeNull(
            $"{FactoryFile} must have exactly one {methodName} method returning "
            + $"{nameof(CompilerOptions)} — the initializer the guard reads");

        return method!.DescendantNodes()
            .OfType<InitializerExpressionSyntax>()
            .SelectMany(init => init.Expressions.OfType<AssignmentExpressionSyntax>())
            .Select(a => a.Left)
            .OfType<IdentifierNameSyntax>()
            .Select(id => id.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The <see cref="CompilerOptions"/> members one method reads off its options parameter(s),
    /// read from source: every <c>&lt;optionsParam&gt;.Member</c> access inside the method body. The
    /// receiver names are taken from the parameter list rather than assumed, so renaming a parameter
    /// cannot quietly empty the scan.
    /// </summary>
    private static HashSet<string> OptionMembersReadBy(string project, string fileName, string methodName)
    {
        var root = ParseSourceFile(project, fileName);
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(m => m.Identifier.Text == methodName);

        method.Should().NotBeNull(
            $"{fileName} must have exactly one {methodName} method — the guard reads it");

        var receivers = method!.ParameterList.Parameters
            .Where(p => p.Type != null && NamesType(p.Type, nameof(CompilerOptions)))
            .Select(p => p.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);

        receivers.Should().NotBeEmpty(
            $"{methodName} must take its input as a {nameof(CompilerOptions)} parameter — the guard "
            + "reads the members off it");

        return method.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression is IdentifierNameSyntax id
                             && receivers.Contains(id.Identifier.Text))
            .Select(access => access.Name.Identifier.Text)
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

    /// <summary>
    /// True if a <c>with</c> expression produces a <see cref="ProjectCompilerOptions"/>: the
    /// receiver either names the type (<c>ProjectCompilerOptions.Default with { … }</c>) or is a
    /// local whose declaration or initializer does (<c>var o = ProjectCompilerOptions.Default; … o
    /// with { … }</c>). A receiver whose type is only knowable from a method's return type is not
    /// matched — this scan is syntactic, like the rest of the file, and would need a compilation to
    /// go further.
    /// </summary>
    private static bool ShapesProjectCompilerOptions(WithExpressionSyntax with)
    {
        if (with.Expression.ToString().Contains(nameof(ProjectCompilerOptions), StringComparison.Ordinal))
            return true;

        if (with.Expression is not IdentifierNameSyntax receiver)
            return false;

        foreach (var ancestor in with.Ancestors())
        {
            foreach (var declarator in ancestor.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (declarator.Identifier.Text != receiver.Identifier.Text)
                    continue;

                if (declarator.Parent is VariableDeclarationSyntax declaration
                    && NamesType(declaration.Type, nameof(ProjectCompilerOptions)))
                    return true;

                if (declarator.Initializer?.Value.ToString()
                        .Contains(nameof(ProjectCompilerOptions), StringComparison.Ordinal) == true)
                    return true;
            }

            if (ancestor is BaseTypeDeclarationSyntax)
                break; // left the type without finding a declaration for the receiver
        }

        return false;
    }

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

    /// <summary>
    /// Parses one named source file of a project, found wherever it sits in the tree — the guarded
    /// seam files are spread across the project root and subdirectories, and moving one must not
    /// silently empty a scan.
    /// </summary>
    private static CompilationUnitSyntax ParseSourceFile(string project, string fileName)
    {
        var dir = FindSourceDir(project);
        var matches = Directory.Exists(dir)
            ? Directory.GetFiles(dir, fileName, SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                            && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToList()
            : new List<string>();

        matches.Should().ContainSingle(
            $"the guard reads {fileName} from {dir} — it must exist there exactly once");

        return (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText(matches[0])).GetRoot();
    }

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
