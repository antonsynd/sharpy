using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #2039 (P14c, Decision 28 (e)/(h)): the module-as-namespace layout is ONE recorded fact —
/// namespace segments, the members class <c>&lt;X&gt;</c> (<c>&lt;Stem&gt;Module</c>, ruling X3) and the
/// sibling bit of every top-level type — materialized by <see cref="CodeGenInfoComputer"/> for the
/// file's own module (node-keyed on its <see cref="Module"/> root: it has no ModuleSymbol), for an
/// <c>import</c>ed module (its ModuleSymbol's CodeGenInfo) and for a <c>from … import</c>'s source
/// module (node-keyed on the import). The module namespace is seeded with <c>&lt;X&gt;</c> (a
/// declaration spelled like it is SPY0523, ruling 15), the test class and the fixture classes (SPY0522).
/// Values are literals, not re-derived from <c>ModuleIdentifiers</c>.
/// </summary>
public class ModuleLayoutFactTests
{
    private sealed record Analysis(
        Module Module, SymbolTable SymbolTable, SemanticBinding Binding, SemanticInfo Info, DiagnosticBag Diagnostics);

    private static Analysis Analyze(
        string source, string filePath, string? sourceRoot = null, Action<Module, SymbolTable, SemanticBinding>? arrange = null)
    {
        var tokens = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance).TokenizeAll();
        var module = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance).ParseModule();

        var symbolTable = new SymbolTable(new BuiltinRegistry());
        var semanticInfo = new SemanticInfo();
        var binding = new SemanticBinding();
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, binding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        binding.MaterializeInheritance();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance) { SemanticBinding = binding }
            .CheckModule(module, computeCodeGenInfo: false, isEntryPoint: false);
        binding.MaterializeVariableTypes();

        arrange?.Invoke(module, symbolTable, binding);

        var diagnostics = new DiagnosticBag();
        new CodeGenInfoComputer(symbolTable, binding, diagnostics, semanticInfo)
            .ComputeForModule(module, filePath, isEntryPoint: false, sourceRootPath: sourceRoot);
        return new Analysis(module, symbolTable, binding, semanticInfo, diagnostics);
    }

    private static string Root => Path.Combine(Path.GetTempPath(), "layout-root");

    private static string InRoot(params string[] parts) => Path.Combine(new[] { Root }.Concat(parts).ToArray());

    // ---- the own module's layout -------------------------------------------------------------------

    [Theory]
    // (file relative to the root, or a bare file name for no root) → (segments, <X>)
    [InlineData("thing.spy", false, "Thing", "ThingModule")]
    [InlineData("thing.spy", true, "Thing", "ThingModule")]
    [InlineData("pkg/thing.spy", true, "Pkg.Thing", "ThingModule")]
    [InlineData("pkg/__init__.spy", true, "Pkg", "PkgModule")]
    [InlineData("my_pkg/sub_mod.spy", true, "MyPkg.SubMod", "SubModModule")]
    [InlineData("pkg/sub/__init__.spy", true, "Pkg.Sub", "SubModule")]
    [InlineData("math_module.spy", true, "MathModule", "MathModuleModule")]
    [InlineData("main.spy", true, "Main", "MainModule")]
    public void OwnModule_LayoutIsRecordedOnItsRoot(string relative, bool withRoot, string segments, string membersClass)
    {
        var path = withRoot ? InRoot(relative.Split('/')) : relative;
        var a = Analyze("def helper() -> int:\n    return 1\n", path, withRoot ? Root : null);

        var layout = a.Info.GetModuleLayout(a.Module);
        layout.Should().NotBeNull();
        string.Join(".", layout!.NamespaceSegments).Should().Be(segments);
        layout.MembersClassName.Should().Be(membersClass);
        layout.TestClassName.Should().BeNull("the module declares no test");
        layout.FixtureClassNames.Should().BeNull();
    }

    [Fact]
    public void OwnModule_WithoutAName_RecordsNoLayout()
    {
        var a = Analyze("def helper() -> int:\n    return 1\n", filePath: null!);
        a.Info.GetModuleLayout(a.Module).Should().BeNull();
    }

    [Fact]
    public void OwnModule_TestAndFixtureClasses_AreRecorded()
    {
        var a = Analyze(
            "@test.fixture\ndef greeting() -> str:\n    return \"hi\"\n\n@test\ndef test_hi(greeting: str) -> None:\n    assert greeting == \"hi\"\n",
            "thing.spy");
        var layout = a.Info.GetModuleLayout(a.Module)!;
        layout.TestClassName.Should().Be("ThingModuleTests");
        layout.FixtureClassNames.Should().BeEquivalentTo(new Dictionary<string, string> { ["greeting"] = "GreetingFixture" });
    }

    // ---- the sibling bit ---------------------------------------------------------------------------

    [Fact]
    public void TopLevelTypes_AreNamespaceSiblings_NestedAndFunctionsAreNot()
    {
        var a = Analyze(
            "class Foo:\n    class Inner:\n        x: int = 0\n\nstruct Pt:\n    x: int\n\ninterface IShape:\n    def area(self) -> float: ...\n\nenum Color:\n    RED = 1\n\ndef helper() -> int:\n    return 1\n",
            "thing.spy");
        foreach (var name in new[] { "Foo", "Pt", "IShape", "Color" })
        {
            var info = a.Binding.GetCodeGenInfo(a.SymbolTable.Lookup(name)!)!;
            info.IsNamespaceSibling.Should().BeTrue(name);
            string.Join(".", info.NamespaceSegments!).Should().Be("Thing", "a type records the namespace it is declared in");
        }

        var foo = (TypeSymbol)a.SymbolTable.Lookup("Foo")!;
        var inner = foo.NestedTypes.Single(t => t.Name == "Inner");
        (a.Binding.GetCodeGenInfo(inner)?.IsNamespaceSibling ?? false).Should().BeFalse("a nested type stays inside its outer type");
        a.Binding.GetCodeGenInfo(a.SymbolTable.Lookup("helper")!)!.IsNamespaceSibling.Should().BeFalse();
    }

    // ---- imported modules --------------------------------------------------------------------------

    [Fact]
    public void ImportedModule_CarriesItsLayout_OnTheModuleSymbolsCodeGenInfo()
    {
        var a = Analyze("import thing\n", InRoot("main.spy"), Root, (_, table, _) =>
            table.TryDefine(new ModuleSymbol { Name = "thing", Kind = SymbolKind.Module, FilePath = InRoot("pkg", "thing.spy") }));

        var info = a.Binding.GetCodeGenInfo(a.SymbolTable.Lookup("thing")!)!;
        string.Join(".", info.NamespaceSegments!).Should().Be("Pkg.Thing");
        info.MembersClassName.Should().Be("ThingModule");
    }

    [Fact]
    public void FromImport_RecordsItsSourceLayout_AndTheImportedTypeIsASibling()
    {
        var a = Analyze("from pkg.thing import Foo\n", InRoot("main.spy"), Root, (module, table, binding) =>
        {
            table.TryDefine(new TypeSymbol { Name = "Foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class });
            binding.SetResolvedModuleFilePath((FromImportStatement)module.Body[0], InRoot("pkg", "thing.spy"));
        });

        var layout = a.Info.GetModuleLayout(a.Module.Body[0])!;
        string.Join(".", layout.NamespaceSegments).Should().Be("Pkg.Thing");
        layout.MembersClassName.Should().Be("ThingModule");
        var info = a.Binding.GetCodeGenInfo(a.SymbolTable.Lookup("Foo")!)!;
        info.IsNamespaceSibling.Should().BeTrue();
        string.Join(".", info.NamespaceSegments!).Should().Be("Pkg.Thing");
    }

    [Fact]
    public void FromImport_OfAReExportedType_RecordsTheDeclaringFilesNamespace()
    {
        // `from pkg import Foo` where pkg/__init__.spy re-exports Foo from pkg/lib.spy: Foo lives in
        // the namespace of the file that DECLARES it, not the package's.
        var a = Analyze("from pkg import Foo\n", InRoot("main.spy"), Root, (module, table, binding) =>
        {
            table.TryDefine(new TypeSymbol
            {
                Name = "Foo", Kind = SymbolKind.Type, TypeKind = TypeKind.Class,
                DefiningFilePath = InRoot("pkg", "lib.spy"),
            });
            binding.SetResolvedModuleFilePath((FromImportStatement)module.Body[0], InRoot("pkg", "__init__.spy"));
        });

        string.Join(".", a.Info.GetModuleLayout(a.Module.Body[0])!.NamespaceSegments).Should().Be("Pkg");
        string.Join(".", a.Binding.GetCodeGenInfo(a.SymbolTable.Lookup("Foo")!)!.NamespaceSegments!)
            .Should().Be("Pkg.Lib");
    }

    [Fact]
    public void FromImport_OfANetModule_RecordsTheReflectedLayout_AndNoSibling()
    {
        var a = Analyze("from netmod import Bar\n", InRoot("main.spy"), Root, (_, table, binding) =>
        {
            table.TryDefine(new TypeSymbol { Name = "Bar", Kind = SymbolKind.Type, TypeKind = TypeKind.Class });
            binding.MarkAsNetModule("netmod", "Sharpy", "NetmodModule");
        });

        var layout = a.Info.GetModuleLayout(a.Module.Body[0])!;
        string.Join(".", layout.NamespaceSegments).Should().Be("Sharpy");
        layout.MembersClassName.Should().Be("NetmodModule");
        a.Binding.GetCodeGenInfo(a.SymbolTable.Lookup("Bar")!)!.IsNamespaceSibling.Should().BeFalse();
    }

    // ---- collision seeding (Decision 28 (h)) ------------------------------------------------------

    public static IEnumerable<object[]> SeedCells()
    {
        // source, file, expected codes (in order); every refused cell compiles and runs at the base
        // (the legacy layout nests it inside class Thing) — worked → refused, the direction record.
        yield return new object[] { "def thing_module() -> int:\n    return 1\n", "thing.spy", new[] { "SPY0523" } };
        yield return new object[] { "class ThingModule:\n    x: int = 0\n", "thing.spy", new[] { "SPY0523" } };
        yield return new object[] { "thing_module: int = 1\n", "thing.spy", new[] { "SPY0523" } };
        yield return new object[] { "class ThingModuleTests:\n    x: int = 0\n\n@test\ndef test_a() -> None:\n    assert True\n", "thing.spy", new[] { "SPY0522" } };
        yield return new object[] { "class GreetingFixture:\n    x: int = 0\n\n@test.fixture\ndef greeting() -> str:\n    return \"hi\"\n", "thing.spy", new[] { "SPY0522" } };
        // A package's __init__, whose members class is <Dir>Module, is reported once.
        yield return new object[] { "def pkg_module() -> int:\n    return 1\n", "pkg/__init__.spy", new[] { "SPY0523" } };
        // Collisions within one scope stay: two members of <X>, two sibling types (SPY0522).
        yield return new object[] { "def foo_bar() -> int:\n    return 1\n\nFooBar: int = 2\n", "thing.spy", new[] { "SPY0522" } };
        yield return new object[] { "class foo_bar:\n    x: int = 0\n\nclass FooBar:\n    y: int = 0\n", "thing.spy", new[] { "SPY0522" } };
        // Controls: the seed is only there when the module emits it; ordinary names are free.
        yield return new object[] { "class ThingModuleTests:\n    x: int = 0\n", "thing.spy", Array.Empty<string>() };
        yield return new object[] { "class Thing:\n    x: int = 0\n\ndef other() -> int:\n    return 1\n", "thing.spy", Array.Empty<string>() };
        yield return new object[] { "def other_module() -> int:\n    return 1\n", "thing.spy", Array.Empty<string>() };
        // Lifted (refused -> runs, #2039): a function named like its file no longer meets a module
        // class (was SPY0523), and a function and a type of one emitted name live in two scopes (was
        // SPY0522 collision_func_type).
        yield return new object[] { "def thing() -> int:\n    return 1\n", "thing.spy", Array.Empty<string>() };
        yield return new object[] { "class FooBar:\n    x: int = 0\n\ndef foo_bar() -> int:\n    return 1\n", "thing.spy", Array.Empty<string>() };
    }

    [Theory]
    [MemberData(nameof(SeedCells))]
    public void ModuleNamespace_IsSeededWithItsClasses(string source, string file, string[] codes)
    {
        var path = file.Contains('/') ? InRoot(file.Split('/')) : file;
        var a = Analyze(source, path, file.Contains('/') ? Root : null);
        a.Diagnostics.GetErrors().Select(d => d.Code).Should().Equal(codes,
            string.Join("; ", a.Diagnostics.GetErrors().Select(d => d.Code + ": " + d.Message)));
    }

    [Fact]
    public void MembersClassSeed_NamesTheMembersClass()
    {
        var a = Analyze("def thing_module() -> int:\n    return 1\n", "thing.spy");
        a.Diagnostics.GetErrors().Single().Message.Should().Contain("'ThingModule'").And.Contain("members class");
    }
}
