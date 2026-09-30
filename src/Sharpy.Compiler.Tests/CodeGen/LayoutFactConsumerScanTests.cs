using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// #2039 (P14c, Decision 28 (e)) and #2102: every module is a namespace, and the module layout — its
/// namespace segments, its members class <c>&lt;X&gt;</c>, the sibling types beside it, its test and
/// fixture classes — is ONE fact semantic analysis records (<c>ModuleLayout</c> on the module root and
/// on each from-import, <c>CodeGenInfo.NamespaceSegments</c>/<c>MembersClassName</c> on an imported
/// module and a sibling type). The emitter reads it and derives nothing (Rule 2). This Roslyn source
/// scan over <c>CodeGen/</c> and the CLI commands pins that:
/// <list type="bullet">
/// <item><description><b>The reader set is derived, not listed.</b> Every member whose body names a
/// layout-fact identifier (<see cref="FactIdentifiers"/>) is found by the scan; the derived set must
/// equal the literal <see cref="Readers"/> roster, whose size is anchored to a literal. A new consumer
/// goes red until it is added to the roster — so it is reviewed as a layout consumer — and a deleted one
/// goes red until it is removed. (The #2102 predecessor compared a literal list with a literal count,
/// which no source change could redden; it missed three readers.)</description></item>
/// <item><description><b>Routes.</b> The consumers that reach the fact through a reader (a type
/// reference through <c>TypeNamespaceSegments</c>, a module member through
/// <c>ModuleMemberContainerSegments</c>) must still call that reader.</description></item>
/// <item><description><b>No derivation.</b> No scanned source names the path authority the recorder
/// uses (<c>ModuleIdentifiers.LayoutNamespaceSegments</c>, <c>LayoutMembersClassName</c>,
/// <c>DottedModulePath</c>, …) or a retired module-class derivation. The emitter fallbacks that
/// recomputed the layout from those were deleted in #2102.</description></item>
/// <item><description><b>No fallback.</b> Emitting a module whose layout was never recorded is an
/// internal compiler error, not a silently re-derived layout.</description></item>
/// </list>
/// </summary>
public class LayoutFactConsumerScanTests
{
    /// <summary>
    /// The identifiers that name the recorded layout fact or its once-read shape: <c>ModuleLayout</c>
    /// and <c>ModuleShape</c> members, the layout members of <c>CodeGenInfo</c>, the shape field and
    /// property, and the CLI's recorded entry type.
    /// </summary>
    private static readonly ImmutableHashSet<string> FactIdentifiers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetModuleLayout", "NamespaceParts", "NamespaceSegments", "MembersClassName", "MembersClassPath",
        "SiblingTypePath", "TestClassName", "FixtureClassNames", "MembersClassFullName", "IsNamespaceSibling",
        "EntryTypeName", "_moduleShape", "CurrentModuleShape");

    /// <summary>
    /// The fact carriers themselves — their members DECLARE the fact rather than consume it.
    /// </summary>
    private static readonly ImmutableHashSet<string> CarrierTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal, "ModuleShape");

    /// <summary>(file, member) → one fact identifier it reads. The set of keys is compared with the scan.</summary>
    private static readonly (string File, string Member, string ReadsFact)[] Readers =
    {
        // The own module: the recorded layout read once, and the namespace every member is assembled into.
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "ComputeModuleShape", "GetModuleLayout"),
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "CurrentModuleShape", "_moduleShape"),
        ("CodeGen/RoslynEmitter.CompilationUnit.cs", "GenerateCompilationUnit", "NamespaceParts"),
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "GenerateModuleMembers", "MembersClassName"),
        // F1: a cross-module type reference — the recorded namespace of its outermost type.
        ("CodeGen/TypeSyntaxMapper.cs", "TypeNamespaceSegments", "NamespaceSegments"),
        // F4: the ONE same-file type qualifier.
        ("CodeGen/TypeSyntaxMapper.cs", "QualifySameFileTypeName", "SiblingTypePath"),
        // F5/F9: own module members, from anywhere, and from inside a type body.
        ("CodeGen/RoslynEmitter.Expressions.cs", "OwnModuleContainerSegments", "MembersClassPath"),
        ("CodeGen/RoslynEmitter.Expressions.cs", "ModuleQualified", "MembersClassPath"),
        // F6/F13: a from-imported member and a package re-export — the layout recorded on the import.
        ("CodeGen/RoslynEmitter.CompilationUnit.cs", "FromImportMembersClassPath", "GetModuleLayout"),
        // F7: `import thing; thing.helper()` — the layout recorded on the imported ModuleSymbol.
        ("CodeGen/RoslynEmitter.Expressions.Access.cs", "UserModuleLayout", "MembersClassName"),
        // F10: [MemberData] names the members class global::-rooted.
        ("CodeGen/RoslynEmitter.TypeDeclarations.cs", "GenerateMemberDataAttribute", "MembersClassPath"),
        // F14: the test class and the fixture classes.
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "GenerateModuleTestClass", "TestClassName"),
        ("CodeGen/RoslynEmitter.TestFixtures.cs", "RegisterFixture", "FixtureClassNames"),
        // The CLI's self-contained publish entry type.
        ("../Sharpy.Cli/Commands/RunCommand.cs", "HandleRunCommand", "EntryTypeName"),
        ("../Sharpy.Cli/Commands/CompileCommand.cs", "CompileSingleFile", "EntryTypeName"),
    };

    /// <summary>The reader count @ this commit — a literal, so neither the roster nor the scan can drift.</summary>
    private const int ReaderCount = 15;

    /// <summary>(file, member) → the reader it must route through.</summary>
    private static readonly (string File, string Member, string CallsReader)[] Routes =
    {
        ("CodeGen/TypeSyntaxMapper.cs", "QualifyFromSymbol", "TypeNamespaceSegments"),
        ("CodeGen/RoslynEmitter.Expressions.Access.Calls.cs", "BuildQualifiedTypeAccess", "QualifySameFileTypeName"),
        ("CodeGen/RoslynEmitter.Expressions.cs", "ModuleMemberContainerSegments", "OwnModuleContainerSegments"),
        ("CodeGen/RoslynEmitter.Expressions.cs", "QualifyModuleMember", "ModuleMemberContainerSegments"),
        ("CodeGen/RoslynEmitter.CompilationUnit.cs", "RegisterFromImportMembers", "FromImportMembersClassPath"),
        ("CodeGen/RoslynEmitter.ModuleClass.cs", "GenerateReExportMembers", "FromImportMembersClassPath"),
        ("CodeGen/RoslynEmitter.Expressions.Access.cs", "BuildModuleAccessExpression", "UserModuleLayout"),
    };

    /// <summary>
    /// Names no scanned source may use: the path authority the layout RECORDER derives from (a use in
    /// the emitter is a second layout derivation, the #2102 fallback shape), and the retired module-class
    /// derivations (#2039).
    /// </summary>
    private static readonly ImmutableHashSet<string> DerivationNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "LayoutNamespaceSegments", "LayoutMembersClassName", "ModuleNamespaceSegments", "DottedModulePath",
        "ModuleNamespaceFromFilePath", "ForModule",
        "GetModuleClassName", "ModuleClassPath", "MergedClassName", "ExtractedTypeNames",
        "BuildQualifiedTypeName", "GetModuleNameFromFilePath", "DecorateExtractedType");

    [Fact]
    public void Roster_IsTheAnchoredCount()
    {
        Readers.Should().HaveCount(ReaderCount);
        DerivedReaders(ScannedSources()).Should().HaveCount(ReaderCount,
            "the scan finds exactly the anchored number of layout-fact readers");
    }

    [Fact]
    public void DerivedReaderSet_IsTheRoster()
    {
        var derived = DerivedReaders(ScannedSources()).Keys.ToHashSet();
        var listed = Readers.Select(r => (r.File, r.Member)).ToHashSet();

        var unlisted = derived.Except(listed).Select(k => $"{k.File}::{k.Member} reads {string.Join(", ", DerivedReaders(ScannedSources())[k])}");
        var stale = listed.Except(derived).Select(k => $"{k.File}::{k.Member} reads no layout fact (or no longer exists)");

        unlisted.Concat(stale).Should().BeEmpty(
            "every member that reads the recorded module layout is on the reviewed roster, and every roster "
            + "entry still reads it (#2102)");
    }

    [Fact]
    public void EveryReader_ReadsItsNamedFact_AndEveryRoute_CallsItsReader()
    {
        var failures = new List<string>();
        foreach (var (file, member, name) in Readers.Concat(Routes.Select(r => (r.File, r.Member, ReadsFact: r.CallsReader))))
        {
            var bodies = MemberBodies(file, member).ToList();
            if (bodies.Count == 0)
                failures.Add($"{file}::{member} — no such member (the roster is stale)");
            else if (!bodies.Any(b => b.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Any(n => n.Identifier.Text == name)))
                failures.Add($"{file}::{member} does not reference '{name}'");
        }

        failures.Should().BeEmpty("every module-layout consumer reads the fact semantic analysis recorded (#2039, Rule 2)");
    }

    [Fact]
    public void NoSource_DerivesALayout()
    {
        var offenders = new List<string>();
        foreach (var (relative, tree) in ScannedSources())
        {
            foreach (var id in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (DerivationNames.Contains(id.Identifier.Text))
                    offenders.Add($"{relative}:{Line(id)} {id.Identifier.Text}");
                // ModuleIdentifiers.MembersClassName / ModuleClassName derive <X> from a stem.
                if (id.Identifier.Text is "ModuleClassName" or "MembersClassName"
                    && id.Parent is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "ModuleIdentifiers" } })
                    offenders.Add($"{relative}:{Line(id)} ModuleIdentifiers.{id.Identifier.Text}");
            }
        }

        offenders.Should().BeEmpty("the emitter reads the recorded layout and derives none of its own (#2039, #2102)");
    }

    [Fact]
    public void Scan_FlagsAPlantedReaderAndAPlantedDerivation()
    {
        // Positive control: the same walks over synthetic sources flag a new reader (in a plain method,
        // not in the carrier record) and a planted derivation.
        var tree = CSharpSyntaxTree.ParseText(
            "partial class RoslynEmitter {\n"
            + "  record ModuleShape(string MembersClassName) { string[] P => new[] { MembersClassName }; }\n"
            + "  string NewConsumer() => CurrentModuleShape.MembersClassName;\n"
            + "  string Derives(string p) => ModuleIdentifiers.LayoutMembersClassName(p) + ModuleIdentifiers.MembersClassName(p);\n"
            + "}\n");
        var readers = DerivedReaders(new[] { ("Synthetic.cs", tree) });
        readers.Keys.Should().BeEquivalentTo(new[] { ("Synthetic.cs", "NewConsumer"), ("Synthetic.cs", "Derives") },
            "a method naming a fact identifier is a reader; the carrier record's own members are not");

        var derivations = tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>()
            .Count(id => DerivationNames.Contains(id.Identifier.Text)
                || (id.Identifier.Text == "MembersClassName"
                    && id.Parent is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "ModuleIdentifiers" } }));
        derivations.Should().Be(2);
        ScannedSources().Should().HaveCountGreaterThan(20, "the scan reads the CodeGen directory and the CLI commands");
    }

    [Fact]
    public void Emission_WithoutARecordedLayout_IsAnInternalError()
    {
        // The fallback is gone: a module whose layout semantic analysis never recorded is not emitted
        // under a re-derived layout. Positive control: the same module with the layout recorded emits.
        var builtins = new BuiltinRegistry();
        var context = new CodeGenContext(new SymbolTable(builtins), builtins)
        {
            Ir = IrCompilation.Empty,
            SourceFilePath = "pkg/thing.spy",
            SemanticInfo = new SemanticInfo(),
        };
        var module = new Module { Body = ImmutableArray<Statement>.Empty };

        var unrecorded = () => new RoslynEmitter(context).GenerateCompilationUnit(module);
        unrecorded.Should().Throw<InternalCompilerErrorException>().WithMessage("*no module layout recorded*");

        context.SemanticInfo.SetModuleLayout(module, new ModuleLayout(new[] { "Pkg", "Thing" }, "ThingModule"));
        new RoslynEmitter(context).GenerateCompilationUnit(module).NormalizeWhitespace().ToFullString()
            .Should().Contain("namespace Pkg.Thing").And.Contain("class ThingModule");
    }

    /// <summary>(file, member) → the fact identifiers its body names, for every member outside a carrier type.</summary>
    private static Dictionary<(string File, string Member), SortedSet<string>> DerivedReaders(
        IEnumerable<(string Relative, SyntaxTree Tree)> sources)
    {
        var readers = new Dictionary<(string, string), SortedSet<string>>();
        foreach (var (relative, tree) in sources)
        {
            foreach (var id in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (!FactIdentifiers.Contains(id.Identifier.Text))
                    continue;
                var member = id.Ancestors().FirstOrDefault(a => a is BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax);
                if (member == null || member.Ancestors().OfType<TypeDeclarationSyntax>().Any(t => CarrierTypes.Contains(t.Identifier.Text)))
                    continue;
                var key = (relative.Replace('\\', '/'), MemberName(member));
                if (!readers.TryGetValue(key, out var facts))
                    readers[key] = facts = new SortedSet<string>(StringComparer.Ordinal);
                facts.Add(id.Identifier.Text);
            }
        }
        return readers;
    }

    private static string MemberName(SyntaxNode member) => member switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        PropertyDeclarationSyntax p => p.Identifier.Text,
        _ => member.Kind().ToString(),
    };

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static IEnumerable<SyntaxNode> MemberBodies(string file, string member)
    {
        var path = Path.GetFullPath(Path.Combine(CompilerDirectory(), file));
        File.Exists(path).Should().BeTrue($"roster file {file} not found at {path}");
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
        return root.DescendantNodes().Where(n => n is MethodDeclarationSyntax or PropertyDeclarationSyntax && MemberName(n) == member);
    }

    private static IEnumerable<(string Relative, SyntaxTree Tree)> ScannedSources()
    {
        var compiler = CompilerDirectory();
        var files = Directory.GetFiles(Path.Combine(compiler, "CodeGen"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.GetFullPath(Path.Combine(compiler, "../Sharpy.Cli/Commands")), "*.cs"));
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(compiler, file).Replace('\\', '/');
            yield return (relative, CSharpSyntaxTree.ParseText(File.ReadAllText(file)));
        }
    }

    private static string CompilerDirectory()
        => Path.GetDirectoryName(EmitterBannedTokenScanTests.FindCodeGenSourceDirectory())!;
}
