using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// One module-path authority (#1948, Decision 28 (d)): <see cref="ModuleIdentifiers"/> is the only
/// place a source file's position becomes a C# path. The type-naming seam's own derivation (F2,
/// <c>GetModuleNameFromFilePath</c>) and the two dotted-name copies (F3,
/// <c>ConvertModuleToNamespace</c> / <c>ConvertModuleNameToNamespace</c>) are retired onto it.
///
/// <para>Pinned against LITERALS (a table computed from the helper itself would pass vacuously), plus
/// a source scan that the retired derivations stay gone, plus a pin of
/// <see cref="ModuleIdentifiers.TopLevelMemberIdentifiers"/> — the pre-analysis spelling refusal 2 of
/// SPY0526 reads — against the members the emitter actually declares.</para>
/// </summary>
public class ModulePathAuthorityTests
{
    private readonly ITestOutputHelper _output;

    public ModulePathAuthorityTests(ITestOutputHelper output) => _output = output;

    private const string Root = "/proj/src";

    /// <summary>
    /// The module-as-namespace layout (#2039): a file's namespace (its directories, then its stem — a
    /// package's <c>__init__</c> is the package itself) and its members class <c>&lt;X&gt;</c>.
    /// </summary>
    [Theory]
    [InlineData("lib.spy", "Lib|LibModule")]
    [InlineData("main.spy", "Main|MainModule")]
    [InlineData("pkg/lib.spy", "Pkg.Lib|LibModule")]
    [InlineData("pkg/__init__.spy", "Pkg|PkgModule")]
    [InlineData("pkg/sub/__init__.spy", "Pkg.Sub|SubModule")]
    [InlineData("pkg/sub/leaf.spy", "Pkg.Sub.Leaf|LeafModule")]
    [InlineData("my_pkg/sub_mod.spy", "MyPkg.SubMod|SubModModule")]
    [InlineData("db/models.spy", "DB.Models|ModelsModule")]
    [InlineData("a/b/c/d.spy", "A.B.C.D|DModule")]
    [InlineData("lib/lib.spy", "Lib.Lib|LibModule")]
    [InlineData("a/a/x.spy", "A.A.X|XModule")]
    [InlineData("__init__.spy", "|SrcModule")]
    [InlineData("api/v2/__init__.spy", "API.V2|V2Module")]
    // A stem is not an identifier: a leading digit gets a `_` in the namespace segment AND <X>.
    [InlineData("20260118_x.spy", "_20260118X|_20260118XModule")]
    public void Layout_IsTheLiteralNamespaceAndMembersClass(string relativePath, string expected)
    {
        var file = Path.Combine(Root, relativePath);
        (string.Join(".", ModuleIdentifiers.LayoutNamespaceSegments(Root, file))
                + "|" + ModuleIdentifiers.LayoutMembersClassName(file))
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("lib.spy", "")]
    [InlineData("pkg/lib.spy", "Pkg")]
    [InlineData("pkg/__init__.spy", "Pkg")]
    [InlineData("pkg/sub/__init__.spy", "Pkg.Sub")]
    [InlineData("db/api/x.spy", "DB.API")]
    [InlineData("lib/lib.spy", "Lib")]
    public void ModuleNamespaceSegments_AreEveryDirectory(string relativePath, string expected)
        => string.Join(".", ModuleIdentifiers.ModuleNamespaceSegments(Root, Path.Combine(Root, relativePath)))
            .Should().Be(expected);

    [Theory]
    [InlineData("pkg", "PkgModule")]
    [InlineData("my_pkg", "MyPkgModule")]
    [InlineData("thing", "ThingModule")]
    public void MembersClassName_IsTheStemTypeNamePlusModule(string stem, string expected)
        => ModuleIdentifiers.MembersClassName(stem).Should().Be(expected);

    [Theory]
    [InlineData("lib", "Lib")]
    [InlineData("my_pkg.sub_mod", "MyPkg.SubMod")]
    [InlineData("system.io", "System.IO")]
    [InlineData("db.models", "DB.Models")]
    public void DottedModulePath_IsTheLiteralSpelling(string dotted, string expected)
        => ModuleIdentifiers.DottedModulePath(dotted).Should().Be(expected);

    [Theory]
    [InlineData("lib.spy", "lib")]
    [InlineData("pkg/lib.spy", "pkg.lib")]
    [InlineData("pkg/__init__.spy", "pkg")]
    [InlineData("my_pkg/sub_mod.spy", "my_pkg.sub_mod")]
    [InlineData("__init__.spy", "module")]
    public void SharpyModuleName_IsTheDottedPythonName(string relativePath, string expected)
        => ModuleIdentifiers.SharpyModuleName(Root, Path.Combine(Root, relativePath)).Should().Be(expected);

    /// <summary>
    /// The retired derivations stay retired: no CodeGen file re-derives a module path from a relative
    /// file path or re-implements the dotted-name conversion.
    /// </summary>
    [Fact]
    public void NoCodeGenFile_ReDerivesAModulePath()
    {
        var codeGen = Path.Combine(FindCompilerDirectory(), "CodeGen");
        var offenders = Directory.GetFiles(codeGen, "*.cs", SearchOption.AllDirectories)
            .Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .Where(f => f.Text.Contains("Path.GetRelativePath(", StringComparison.Ordinal)
                        || f.Text.Contains("ConvertModuleToNamespace", StringComparison.Ordinal)
                        || f.Text.Contains("ConvertModuleNameToNamespace", StringComparison.Ordinal))
            .Select(f => f.File)
            .ToList();

        offenders.Should().BeEmpty("a module's C# path is derived only by ModuleIdentifiers (#1948)");

        // Positive control: the scan reads the directory the authority's consumers live in.
        Directory.GetFiles(codeGen, "TypeSyntaxMapper.cs", SearchOption.AllDirectories).Should().ContainSingle();
    }

    /// <summary>
    /// <see cref="ModuleIdentifiers.TopLevelMemberIdentifiers"/> spells exactly the member names the
    /// emitter declares on the module class — the spelling refusal 2 of SPY0526 compares, before
    /// analysis can materialize it.
    /// </summary>
    [Fact]
    public void TopLevelMemberIdentifiers_AreTheEmittedMemberNames()
    {
        const string lib = "LIMIT: int = 3\ncount_v: int = 1\nconst K: int = 2\n\n"
            + "def do_thing() -> int:\n    return 1\n\n\nclass the_box:\n    pass\n\n\n"
            + "struct Pt:\n    x: int = 0\n\n\nenum Color:\n    RED = 1\n\n\ninterface IShape:\n    def area(self) -> int: ...\n";
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Pin").WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", lib);
        helper.AddSourceFile("main.spy", "def main() -> None:\n    print(1)\n");
        helper.CreateProjectFile();
        var result = helper.Compile();
        result.Success.Should().BeTrue(string.Join("\n", result.Diagnostics.GetErrors().Select(d => d.Message)));

        // Functions, variables and constants are members of <X>; types are its siblings in the
        // module namespace (#2039).
        var cs = result.GeneratedCSharpFiles.Single(kv => Path.GetFileName(kv.Key) == "lib.cs").Value;
        var moduleNamespace = CSharpSyntaxTree.ParseText(cs).GetRoot().DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>().Single();
        var membersClass = moduleNamespace.Members.OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.Text == "LibModule");
        var declared = membersClass.Members.Concat(moduleNamespace.Members).SelectMany(m => m switch
        {
            FieldDeclarationSyntax f => f.Declaration.Variables.Select(v => v.Identifier.Text),
            MethodDeclarationSyntax meth => new[] { meth.Identifier.Text },
            BaseTypeDeclarationSyntax t => new[] { t.Identifier.Text },
            PropertyDeclarationSyntax p => new[] { p.Identifier.Text },
            _ => Array.Empty<string>(),
        }).ToHashSet();

        var body = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(lib).TokenizeAll()).ParseModule().Body;
        var spelled = ModuleIdentifiers.TopLevelMemberIdentifiers(body).Select(t => t.Identifier).ToList();

        spelled.Should().HaveCount(8, "every top-level declaration is spelled");
        spelled.Should().OnlyContain(id => declared.Contains(id),
            $"the pre-analysis spelling must be what the emitter declares; declared: {string.Join(", ", declared)}");
    }

    private static string FindCompilerDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Sharpy.Compiler");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("src/Sharpy.Compiler not found from " + AppContext.BaseDirectory);
    }
}
