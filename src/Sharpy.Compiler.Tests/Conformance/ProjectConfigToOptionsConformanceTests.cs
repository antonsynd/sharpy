using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Census: every public <see cref="ProjectConfig"/> member is either read by
/// <see cref="CompilerOptionsFactory.ForProject"/> or carries a rationale exemption in
/// <see cref="CompilerOptionsFactory.ProjectConfigExemptMembers"/> (#1554). Without this guard
/// a new <see cref="ProjectConfig"/> property could be silently dropped when converting a
/// project config to compiler options.
/// </summary>
public class ProjectConfigToOptionsConformanceTests
{
    private const string FactoryFile = "CompilerOptionsFactory.cs";
    private const string ForProjectMethod = "ForProject";

    /// <summary>
    /// Every public <see cref="ProjectConfig"/> member must be read by
    /// <see cref="CompilerOptionsFactory.ForProject"/> or be named in
    /// <see cref="CompilerOptionsFactory.ProjectConfigExemptMembers"/> with a rationale.
    /// </summary>
    [Fact]
    public void ForProject_ReadsEveryPublicProjectConfigMemberOrExemptsIt()
    {
        var read = ConfigMembersReadByForProject();

        read.Count.Should().BeGreaterThan(3,
            $"the scan must find {ForProjectMethod}'s config reads in {FactoryFile}");

        var missing = PublicProjectConfigMembers()
            .Where(name => !read.Contains(name))
            .Where(name => !CompilerOptionsFactory.ProjectConfigExemptMembers.ContainsKey(name))
            .ToList();

        missing.Should().BeEmpty(
            $"every public ProjectConfig member must be read by {FactoryFile}'s "
            + $"{ForProjectMethod}, or be named in CompilerOptionsFactory.ProjectConfigExemptMembers "
            + "with a rationale. A member that is neither is silently dropped when building "
            + "CompilerOptions from a project config. Read it in ForProject, or add it to "
            + "ProjectConfigExemptMembers naming where the compiler gets it instead.\n"
            + $"Uncovered: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Exemptions must name real members and carry written reasons — they are a reviewed decision,
    /// not a dumping ground.
    /// </summary>
    [Fact]
    public void ProjectConfigExemptions_NameRealMembersAndCarryReasons()
    {
        var members = PublicProjectConfigMembers().ToHashSet(StringComparer.Ordinal);

        foreach (var (member, reason) in CompilerOptionsFactory.ProjectConfigExemptMembers)
        {
            members.Should().Contain(member,
                $"exemption '{member}' names a ProjectConfig member that no longer exists — delete it");
            reason.Should().NotBeNullOrWhiteSpace(
                $"exemption '{member}' must say why it is exempt");
        }
    }

    /// <summary>
    /// An exemption that ForProject now reads is stale and must be drained (drain on fix).
    /// </summary>
    [Fact]
    public void ProjectConfigExemptions_AreNotAlsoReadByForProject()
    {
        var read = ConfigMembersReadByForProject();

        var stale = CompilerOptionsFactory.ProjectConfigExemptMembers.Keys
            .Where(read.Contains)
            .ToList();

        stale.Should().BeEmpty(
            "an exemption that ForProject now reads is stale — delete it (drain on fix).\n"
            + $"Stale: {string.Join(", ", stale)}");
    }

    // --- Detection helpers -----------------------------------------------------------------------

    private static List<string> PublicProjectConfigMembers()
    {
        var members = typeof(ProjectConfig)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.Name)
            .ToList();

        members.Count.Should().BeGreaterThan(5,
            "the reflection sweep must see the ProjectConfig surface");

        return members;
    }

    /// <summary>
    /// The <see cref="ProjectConfig"/> members <see cref="CompilerOptionsFactory.ForProject"/>
    /// reads off its config parameter, detected via Roslyn syntax scan of <c>config.Member</c>
    /// accesses.
    /// </summary>
    private static HashSet<string> ConfigMembersReadByForProject()
    {
        var root = ParseSourceFile(FactoryFile);
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(m => m.Identifier.Text == ForProjectMethod);

        method.Should().NotBeNull(
            $"{FactoryFile} must have exactly one {ForProjectMethod} method");

        var receivers = method!.ParameterList.Parameters
            .Where(p => p.Type != null && NamesType(p.Type, nameof(ProjectConfig)))
            .Select(p => p.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);

        receivers.Should().NotBeEmpty(
            $"{ForProjectMethod} must take a {nameof(ProjectConfig)} parameter");

        return method.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression is IdentifierNameSyntax id
                             && receivers.Contains(id.Identifier.Text))
            .Select(access => access.Name.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool NamesType(TypeSyntax type, string name) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text == name,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text == name,
        _ => false,
    };

    private static CompilationUnitSyntax ParseSourceFile(string fileName)
    {
        var dir = FindSourceDir();
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

    private static string FindSourceDir()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", "Sharpy.Compiler");
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Sharpy.Compiler"));
    }
}
