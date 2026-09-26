using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// #2039 (P14c, Decision 28 (e)): every module is a namespace, and the module layout — its namespace
/// segments, its members class <c>&lt;X&gt;</c>, the sibling types beside it, its test and fixture
/// classes — is ONE fact semantic analysis records (<c>ModuleLayout</c> on the module root and on each
/// from-import, <c>CodeGenInfo.NamespaceSegments</c>/<c>MembersClassName</c> on an imported module
/// and a sibling type). This Roslyn source scan pins that every layout consumer family reads it:
/// <list type="bullet">
/// <item><description><b>Roster.</b> Each consumer (F1–F14 and the two CLI entry-type callers) is a
/// named method that must reference the fact it reads. The roster's size is anchored to a literal,
/// so a consumer that stops existing, or a scan that finds no method, fails instead of passing
/// vacuously.</description></item>
/// <item><description><b>Retired derivations stay gone.</b> No CodeGen or CLI source names the
/// retired module-class derivations: <c>GetModuleClassName</c>, <c>ModuleIdentifiers.ModuleClassName</c>
/// (the stem-class name with its <c>Program</c> special case), <c>ModuleClassPath</c>, the merge
/// (<c>MergedClassName</c>, the last-segment string compare of <c>BuildQualifiedTypeName</c>) or the
/// library-mode extraction (<c>ExtractedTypeNames</c>).</description></item>
/// </list>
/// </summary>
public class LayoutFactConsumerScanTests
{
    /// <summary>(file, type, method or property) → the identifier it must reference.</summary>
    private static readonly (string File, string Member, string ReadsFact)[] Consumers =
    {
        // The own module: the recorded layout read once, and the namespace every member is assembled into.
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "ComputeModuleShape", "GetModuleLayout"),
        ("CodeGen/RoslynEmitter.CompilationUnit.cs", "GenerateCompilationUnit", "NamespaceParts"),
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "GenerateModuleMembers", "MembersClassName"),
        // F1: a cross-module type reference — the recorded namespace of its outermost type.
        ("CodeGen/TypeSyntaxMapper.cs", "TypeNamespaceSegments", "NamespaceSegments"),
        ("CodeGen/TypeSyntaxMapper.cs", "QualifyFromSymbol", "TypeNamespaceSegments"),
        // F4: the ONE same-file type qualifier, and the value-position site that routes to it.
        ("CodeGen/TypeSyntaxMapper.cs", "QualifySameFileTypeName", "SiblingTypePath"),
        ("CodeGen/RoslynEmitter.Expressions.Access.Calls.cs", "BuildQualifiedTypeAccess", "QualifySameFileTypeName"),
        // F5/F9: own module members, from anywhere, and from inside a type body.
        ("CodeGen/RoslynEmitter.Expressions.cs", "OwnModuleContainerSegments", "MembersClassPath"),
        ("CodeGen/RoslynEmitter.Expressions.cs", "ModuleQualified", "MembersClassPath"),
        // F6/F13: a from-imported member and a package re-export — the layout recorded on the import.
        ("CodeGen/RoslynEmitter.CompilationUnit.cs", "FromImportMembersClassPath", "GetModuleLayout"),
        ("CodeGen/RoslynEmitter.CompilationUnit.cs", "RegisterFromImportMembers", "FromImportMembersClassPath"),
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "GenerateReExportMembers", "FromImportMembersClassPath"),
        // F7: `import thing; thing.helper()` — the layout recorded on the imported ModuleSymbol.
        ("CodeGen/RoslynEmitter.Expressions.Access.cs", "UserModuleLayout", "MembersClassName"),
        ("CodeGen/RoslynEmitter.Expressions.Access.cs", "BuildModuleAccessExpression", "UserModuleLayout"),
        // F10: [MemberData] names the members class global::-rooted.
        ("CodeGen/RoslynEmitter.TypeDeclarations.cs", "GenerateMemberDataAttribute", "MembersClassPath"),
        // F14: the test class and the fixture classes.
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "GenerateModuleTestClass", "TestClassName"),
        ("CodeGen/RoslynEmitter.TestFixtures.cs", "RegisterFixture", "FixtureClassNames"),
        // The CLI's self-contained publish entry type.
        ("../Sharpy.Cli/Commands/RunCommand.cs", "HandleRunCommand", "EntryTypeName"),
        ("../Sharpy.Cli/Commands/CompileCommand.cs", "CompileSingleFile", "EntryTypeName"),
    };

    /// <summary>The consumer count @ this commit — anchored so the roster cannot silently shrink.</summary>
    private const int ConsumerCount = 19;

    private static readonly string[] RetiredDerivations =
    {
        "GetModuleClassName", "ModuleClassPath", "MergedClassName", "ExtractedTypeNames",
        "BuildQualifiedTypeName", "GetModuleNameFromFilePath", "DecorateExtractedType",
    };

    [Fact]
    public void Roster_IsTheAnchoredCount()
        => Consumers.Should().HaveCount(ConsumerCount);

    [Fact]
    public void EveryLayoutConsumer_ReadsTheRecordedFact()
    {
        var failures = new List<string>();
        foreach (var (file, member, fact) in Consumers)
        {
            var bodies = MemberBodies(file, member).ToList();
            if (bodies.Count == 0)
            {
                failures.Add($"{file}::{member} — no such method or property (the roster is stale)");
                continue;
            }

            if (!bodies.Any(b => ReferencesIdentifier(b, fact)))
                failures.Add($"{file}::{member} does not read '{fact}'");
        }

        failures.Should().BeEmpty(
            "every module-layout consumer reads the fact semantic analysis recorded (#2039, Rule 2)");
    }

    [Fact]
    public void NoSource_NamesARetiredDerivation()
    {
        var offenders = new List<string>();
        foreach (var (relative, tree) in ScannedSources())
        {
            foreach (var id in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (RetiredDerivations.Contains(id.Identifier.Text))
                    offenders.Add($"{relative}:{id.GetLocation().GetLineSpan().StartLinePosition.Line + 1} {id.Identifier.Text}");
                // ModuleIdentifiers.ModuleClassName is the stem-class name with its Program special
                // case; the layout reads LayoutMembersClassName.
                if (id.Identifier.Text == "ModuleClassName"
                    && id.Parent is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "ModuleIdentifiers" } })
                    offenders.Add($"{relative}:{id.GetLocation().GetLineSpan().StartLinePosition.Line + 1} ModuleIdentifiers.ModuleClassName");
            }
        }

        offenders.Should().BeEmpty("the module class derivations are retired onto the recorded layout (#2039)");
    }

    [Fact]
    public void RetiredDerivationScan_FlagsAPlantedName()
    {
        // Positive control: the same identifier walk over a synthetic source flags the retired names.
        var tree = CSharpSyntaxTree.ParseText(
            "class C { string F() => GetModuleClassName() + ModuleIdentifiers.ModuleClassName(p, true); }");
        var hits = tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>()
            .Count(id => RetiredDerivations.Contains(id.Identifier.Text)
                || (id.Identifier.Text == "ModuleClassName"
                    && id.Parent is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "ModuleIdentifiers" } }));
        hits.Should().Be(2);
        ScannedSources().Should().HaveCountGreaterThan(20, "the scan reads the CodeGen directory and the CLI commands");
    }

    private static bool ReferencesIdentifier(SyntaxNode body, string identifier)
        => body.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Any(n => n.Identifier.Text == identifier);

    private static IEnumerable<SyntaxNode> MemberBodies(string file, string member)
    {
        var path = Path.GetFullPath(Path.Combine(CompilerDirectory(), file));
        File.Exists(path).Should().BeTrue($"roster file {file} not found at {path}");
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case MethodDeclarationSyntax m when m.Identifier.Text == member:
                    yield return m;
                    break;
                case PropertyDeclarationSyntax p when p.Identifier.Text == member:
                    yield return p;
                    break;
            }
        }
    }

    private static IEnumerable<(string Relative, SyntaxTree Tree)> ScannedSources()
    {
        var compiler = CompilerDirectory();
        var files = Directory.GetFiles(Path.Combine(compiler, "CodeGen"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.GetFullPath(Path.Combine(compiler, "../Sharpy.Cli/Commands")), "*.cs"));
        foreach (var file in files)
            yield return (Path.GetRelativePath(compiler, file), CSharpSyntaxTree.ParseText(File.ReadAllText(file)));
    }

    private static string CompilerDirectory()
        => Path.GetDirectoryName(EmitterBannedTokenScanTests.FindCodeGenSourceDirectory())!;
}
