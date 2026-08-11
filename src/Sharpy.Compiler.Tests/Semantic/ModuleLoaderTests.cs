using Xunit;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Semantic;

public class ModuleLoaderTests : IDisposable
{
    private readonly string _testDir;
    private readonly ModuleLoader _loader;

    public ModuleLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _loader = new ModuleLoader();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private string CreateModule(string name, string content)
    {
        var path = Path.Combine(_testDir, name);
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void LoadModule_CacheHit_ReturnsCachedResult()
    {
        var path = CreateModule("cached.spy", "x: int = 42");

        var first = _loader.LoadModule(path, 1, 1);
        var second = _loader.LoadModule(path, 1, 1);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void LoadModule_CircularImport_CreatesStub()
    {
        // Create two modules that import each other
        var pathA = CreateModule("a.spy", "from b import foo\nclass MyClass:\n    pass");
        var pathB = CreateModule("b.spy", "from a import bar");

        ModuleInfo? circularResult = null;

        // Load module A, which will try to load B via the callback
        var result = _loader.LoadModule(pathA, 1, 1, (module, moduleInfo, searchPath) =>
        {
            // Simulate resolving imports within module A: it tries to load B
            _loader.LoadModule(pathB, 1, 1, (innerModule, innerModuleInfo, innerSearchPath) =>
            {
                // B tries to load A again - this returns a stub (not an error)
                circularResult = _loader.LoadModule(pathA, 1, 1);
            });
        });

        // Circular import creates a stub instead of reporting an error
        Assert.NotNull(circularResult);
        Assert.True(circularResult!.IsStub);
        Assert.Contains(pathA, _loader.DeferredCycleModules);
        Assert.False(_loader.Diagnostics.HasErrors);
    }

    [Fact]
    public void LoadModule_ExtractsFunction()
    {
        var path = CreateModule("funcs.spy", @"
def greet(name: str) -> str:
    return f""Hello, {name}""
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        Assert.NotNull(moduleInfo);
        Assert.True(moduleInfo!.ExportedSymbols.ContainsKey("greet"));
        var symbol = moduleInfo.ExportedSymbols["greet"];
        Assert.IsType<FunctionSymbol>(symbol);
        var func = (FunctionSymbol)symbol;
        Assert.Equal("greet", func.Name);
        Assert.Single(func.Parameters);
        Assert.Equal("name", func.Parameters[0].Name);
    }

    [Fact]
    public void LoadModule_ExtractsClass()
    {
        var path = CreateModule("classes.spy", @"
class Animal:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def speak(self) -> str:
        return self.name
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        Assert.NotNull(moduleInfo);
        Assert.True(moduleInfo!.ExportedSymbols.ContainsKey("Animal"));
        var symbol = moduleInfo.ExportedSymbols["Animal"];
        Assert.IsType<TypeSymbol>(symbol);
        var typeSymbol = (TypeSymbol)symbol;
        Assert.Equal(TypeKind.Class, typeSymbol.TypeKind);
        Assert.Equal(2, typeSymbol.Fields.Count);
        Assert.True(typeSymbol.Methods.Count >= 2); // __init__ + speak
        Assert.Single(typeSymbol.Constructors);
    }

    [Fact]
    public void LoadModule_ExtractsStruct()
    {
        var path = CreateModule("structs.spy", @"
struct Point:
    x: int
    y: int

    def magnitude(self) -> float:
        return 0.0
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        Assert.NotNull(moduleInfo);
        Assert.True(moduleInfo!.ExportedSymbols.ContainsKey("Point"));
        var symbol = moduleInfo.ExportedSymbols["Point"];
        Assert.IsType<TypeSymbol>(symbol);
        var typeSymbol = (TypeSymbol)symbol;
        Assert.Equal(TypeKind.Struct, typeSymbol.TypeKind);
        Assert.Equal(2, typeSymbol.Fields.Count);
    }

    [Fact]
    public void LoadModule_ExtractsEnum()
    {
        var path = CreateModule("enums.spy", @"
enum Color:
    RED
    GREEN
    BLUE
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        Assert.NotNull(moduleInfo);
        Assert.True(moduleInfo!.ExportedSymbols.ContainsKey("Color"));
        var symbol = moduleInfo.ExportedSymbols["Color"];
        Assert.IsType<TypeSymbol>(symbol);
        var typeSymbol = (TypeSymbol)symbol;
        Assert.Equal(TypeKind.Enum, typeSymbol.TypeKind);
    }

    [Fact]
    public void LoadModule_ExtractsInterface()
    {
        var path = CreateModule("interfaces.spy", @"
interface Drawable:
    def draw(self) -> None:
        ...
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        Assert.NotNull(moduleInfo);
        Assert.True(moduleInfo!.ExportedSymbols.ContainsKey("Drawable"));
        var symbol = moduleInfo.ExportedSymbols["Drawable"];
        Assert.IsType<TypeSymbol>(symbol);
        var typeSymbol = (TypeSymbol)symbol;
        Assert.Equal(TypeKind.Interface, typeSymbol.TypeKind);
        Assert.Single(typeSymbol.Methods);
    }

    [Fact]
    public void LoadModule_FileNotFound_ReportsError()
    {
        var result = _loader.LoadModule("/nonexistent/module.spy", 1, 1);

        Assert.Null(result);
        Assert.True(_loader.Diagnostics.HasErrors);
        Assert.Contains(_loader.Diagnostics.GetErrors(),
            d => d.Message.Contains("Module file not found"));
    }

    [Fact]
    public void ComputeCanonicalModuleName_SimpleFile()
    {
        var path = CreateModule("helpers.spy", "x: int = 1");
        var name = _loader.ComputeCanonicalModuleName(path);
        Assert.Equal("helpers", name);
    }

    [Fact]
    public void ComputeCanonicalModuleName_PackageModule()
    {
        // Create a package structure with __init__.spy
        var pkgDir = Path.Combine(_testDir, "mypkg");
        Directory.CreateDirectory(pkgDir);
        File.WriteAllText(Path.Combine(pkgDir, "__init__.spy"), "");
        var modulePath = Path.Combine(pkgDir, "utils.spy");
        File.WriteAllText(modulePath, "x: int = 1");

        var name = _loader.ComputeCanonicalModuleName(modulePath);
        Assert.Equal("mypkg.utils", name);
    }

    [Fact]
    public void GetAccessLevel_PublicName()
    {
        Assert.Equal(AccessLevel.Public, _loader.GetAccessLevel("foo"));
    }

    [Fact]
    public void GetAccessLevel_ProtectedName()
    {
        Assert.Equal(AccessLevel.Protected, _loader.GetAccessLevel("_bar"));
    }

    [Fact]
    public void GetAccessLevel_PrivateName()
    {
        Assert.Equal(AccessLevel.Private, _loader.GetAccessLevel("__baz"));
    }

    [Fact]
    public void ConvertTypeAnnotation_PrimitiveTypes()
    {
        Assert.Equal(SemanticType.Int, _loader.ConvertTypeAnnotationToSemanticType(new TypeAnnotation { Name = "int" }));
        Assert.Equal(SemanticType.Str, _loader.ConvertTypeAnnotationToSemanticType(new TypeAnnotation { Name = "str" }));
        Assert.Equal(SemanticType.Bool, _loader.ConvertTypeAnnotationToSemanticType(new TypeAnnotation { Name = "bool" }));
        Assert.Equal(SemanticType.Float, _loader.ConvertTypeAnnotationToSemanticType(new TypeAnnotation { Name = "float" }));
        Assert.Equal(SemanticType.Void, _loader.ConvertTypeAnnotationToSemanticType(new TypeAnnotation { Name = "None" }));
    }

    [Fact]
    public void ConvertTypeAnnotation_NullReturnsUnknown()
    {
        Assert.Equal(SemanticType.Unknown, _loader.ConvertTypeAnnotationToSemanticType(null));
    }

    [Fact]
    public void ConvertTypeAnnotation_UserDefinedType()
    {
        var result = _loader.ConvertTypeAnnotationToSemanticType(new TypeAnnotation { Name = "MyClass" });
        Assert.IsType<UserDefinedType>(result);
        Assert.Equal("MyClass", ((UserDefinedType)result).Name);
    }

    [Fact]
    public void FindTypeInLoadedModules_FindsType()
    {
        var path = CreateModule("types.spy", @"
class Widget:
    name: str
");

        _loader.LoadModule(path, 1, 1);

        var found = _loader.FindTypeInLoadedModules("Widget");
        Assert.NotNull(found);
        Assert.Equal("Widget", found!.Name);
    }

    [Fact]
    public void FindTypeInLoadedModules_ReturnsNullForUnknown()
    {
        var found = _loader.FindTypeInLoadedModules("NonExistent");
        Assert.Null(found);
    }

    [Fact]
    public void GetCachedModule_ReturnsNullForUncached()
    {
        var result = _loader.GetCachedModule("/nonexistent/path.spy");
        Assert.Null(result);
    }

    [Fact]
    public void CacheModule_StoresAndRetrieves()
    {
        var moduleInfo = new ModuleInfo
        {
            Path = "test_cache",
            ExportedSymbols = new ModuleExports(),
            IsNetModule = true
        };

        _loader.CacheModule("test_cache", moduleInfo);
        var retrieved = _loader.GetCachedModule("test_cache");

        Assert.Same(moduleInfo, retrieved);
    }

    [Fact]
    public void LoadedSpyModules_ExcludesNetModules()
    {
        var spyPath = CreateModule("real.spy", "x: int = 1");
        _loader.LoadModule(spyPath, 1, 1);

        var netModule = new ModuleInfo
        {
            Path = ".net:system",
            ExportedSymbols = new ModuleExports(),
            IsNetModule = true
        };
        _loader.CacheModule(".net:system", netModule);

        var spyModules = _loader.LoadedSpyModules;
        Assert.Single(spyModules);
        Assert.DoesNotContain(spyModules.Values, m => m.IsNetModule);
    }

    /// <summary>
    /// #1363: a class declared inside an imported class used to be invisible to the importer —
    /// the body loop matched only fields, methods, properties and events, so
    /// <c>TypeSymbol.NestedTypes</c> arrived empty and <c>Outer.Inner</c>, which
    /// <c>TypeResolver.LookupNestedType</c> answers by walking exactly that list, reported SPY0202.
    /// All four nested kinds are cells because the extraction dispatches on the statement type.
    /// </summary>
    [Fact]
    public void LoadModule_ExtractsNestedTypes()
    {
        var path = CreateModule("nested.spy", @"
class Registry:
    class Entry:
        key: str

    struct Slot:
        index: int

    interface Visitor:
        def visit(self) -> None:
            ...

    enum Kind:
        FIRST
        SECOND
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        Assert.NotNull(moduleInfo);
        var registry = Assert.IsType<TypeSymbol>(moduleInfo!.ExportedSymbols["Registry"]);

        Assert.Equal(
            new[] { "Entry", "Kind", "Slot", "Visitor" },
            registry.NestedTypes.Select(n => n.Name).OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(TypeKind.Class, NestedNamed(registry, "Entry").TypeKind);
        Assert.Equal(TypeKind.Struct, NestedNamed(registry, "Slot").TypeKind);
        Assert.Equal(TypeKind.Interface, NestedNamed(registry, "Visitor").TypeKind);
        Assert.Equal(TypeKind.Enum, NestedNamed(registry, "Kind").TypeKind);

        // The members of a nested type come along — a name-only nested symbol would resolve
        // Outer.Inner and then refuse every access through it.
        Assert.Single(NestedNamed(registry, "Entry").Fields);
        Assert.Single(NestedNamed(registry, "Visitor").Methods);
        Assert.Equal(2, NestedNamed(registry, "Kind").Fields.Count);

        // DeclaringType is what codegen and the access validator read to reconstruct the chain;
        // NameResolver.ResolveNestedTypeDeclaration sets it for the same-file declaration.
        Assert.All(registry.NestedTypes, n => Assert.Same(registry, n.DeclaringType));
    }

    /// <summary>
    /// #1363: nesting is recursive, and so is the extraction — the class extractor calls back into
    /// the nested-type walk, so an inner type's own inner types are not a second special case.
    /// </summary>
    [Fact]
    public void LoadModule_ExtractsNestedTypes_Recursively()
    {
        var path = CreateModule("deep.spy", @"
class Outer:
    class Middle:
        class Inner:
            value: int
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        var outer = Assert.IsType<TypeSymbol>(moduleInfo!.ExportedSymbols["Outer"]);
        var middle = Assert.Single(outer.NestedTypes);
        Assert.Equal("Middle", middle.Name);
        var inner = Assert.Single(middle.NestedTypes);
        Assert.Equal("Inner", inner.Name);
        Assert.Same(middle, inner.DeclaringType);
        Assert.Single(inner.Fields);
    }

    /// <summary>
    /// #1363: struct and interface bodies drop nested types identically to the class body — they
    /// simply carried no TODO marker, which is why the issue names only the class extractor.
    /// </summary>
    [Fact]
    public void LoadModule_ExtractsNestedTypes_FromStructAndInterfaceBodies()
    {
        var path = CreateModule("hosts.spy", @"
struct Grid:
    class Cell:
        value: int

interface Shape:
    enum Kind:
        ROUND
        FLAT
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        var grid = Assert.IsType<TypeSymbol>(moduleInfo!.ExportedSymbols["Grid"]);
        var cell = Assert.Single(grid.NestedTypes);
        Assert.Equal("Cell", cell.Name);
        Assert.Same(grid, cell.DeclaringType);

        var shape = Assert.IsType<TypeSymbol>(moduleInfo.ExportedSymbols["Shape"]);
        var kind = Assert.Single(shape.NestedTypes);
        Assert.Equal("Kind", kind.Name);
        Assert.Equal(TypeKind.Enum, kind.TypeKind);
        Assert.Same(shape, kind.DeclaringType);
    }

    /// <summary>
    /// #1363: a module-level <c>type X = ...</c> is an export like any other declaration. The
    /// top-level switch had no <c>TypeAlias</c> arm at all, so <c>from lib import Handle</c>
    /// reported SPY0202 for a name the module plainly declares.
    /// </summary>
    [Fact]
    public void LoadModule_ExtractsTypeAlias()
    {
        var path = CreateModule("aliases.spy", @"
type Handle = int
type Pair[T] = tuple[T, T]
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        Assert.NotNull(moduleInfo);
        var handle = Assert.IsType<TypeAliasSymbol>(moduleInfo!.ExportedSymbols["Handle"]);
        Assert.Equal(SymbolKind.TypeAlias, handle.Kind);
        Assert.Equal("int", handle.TypeAnnotation?.Name);

        var pair = Assert.IsType<TypeAliasSymbol>(moduleInfo.ExportedSymbols["Pair"]);
        Assert.Single(pair.TypeParameters);
        Assert.Equal("T", pair.TypeParameters[0].Name);

        // An alias is not a TypeSymbol, so it does NOT join the types-only view — which is exactly
        // why the qualified spelling `lib.Handle` still fails (#1436): ResolveQualifiedType needs a
        // TypeSymbol for the final segment. Pinned so the follow-up fix has a visible starting state.
        Assert.False(moduleInfo.ExportedSymbols.ContainsType("Handle"));
    }

    /// <summary>
    /// #1365: the facts NameResolver stamps on a declaration must survive re-extraction for
    /// importers. SignatureKey is what overload dedup compares, Documentation is what hover shows,
    /// DeprecationMessage is SPY0466's text and IsMustUse is SPY0480's trigger — an imported symbol
    /// missing them is not the same symbol.
    /// </summary>
    [Fact]
    public void LoadModule_ThreadsFunctionFacts()
    {
        var path = CreateModule("facts.spy", @"
@must_use
@deprecated(""use parse_v2"")
def parse(text: str, limit: int) -> int:
    """"""Parse text.""""""
    return limit
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        var parse = Assert.IsType<FunctionSymbol>(moduleInfo!.ExportedSymbols["parse"]);
        Assert.Equal("str,int", parse.SignatureKey);
        Assert.Equal("Parse text.", parse.Documentation);
        Assert.Equal("use parse_v2", parse.DeprecationMessage);
        Assert.True(parse.IsMustUse);
    }

    /// <summary>
    /// #1365: the facts ride on the RESOLVED overload, not on the name. ImportResolver registers
    /// this overload list (extraction symbols) through SymbolTable.DefineFunctionOverloads, and
    /// overload resolution answers from it — which is why an imported overloaded @must_use call is
    /// the shape that actually reads the extraction rather than the project's own symbol.
    /// </summary>
    [Fact]
    public void LoadModule_ThreadsPerOverloadFacts()
    {
        var path = CreateModule("overloads.spy", @"
@must_use
def pick(x: int) -> int:
    return x


def pick(x: str) -> str:
    return x
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        var overloads = moduleInfo!.FunctionOverloads["pick"];
        Assert.Equal(2, overloads.Count);

        var intOverload = overloads.Single(o => o.SignatureKey == "int");
        var strOverload = overloads.Single(o => o.SignatureKey == "str");

        Assert.True(intOverload.IsMustUse);
        Assert.False(strOverload.IsMustUse);
    }

    /// <summary>
    /// #1365: type declarations carry the same three decorator/doc facts, and methods carry the
    /// function set. A @must_use TYPE is SPY0480's other trigger (UserDefinedType.Symbol.IsMustUse).
    /// </summary>
    [Fact]
    public void LoadModule_ThreadsTypeAndMethodFacts()
    {
        var path = CreateModule("typefacts.spy", @"
@must_use
@deprecated(""use Receipt2"")
class Receipt:
    """"""A receipt.""""""

    @must_use
    def total(self, tax: float) -> float:
        """"""Total with tax.""""""
        return tax


@must_use
struct Token:
    """"""A token.""""""
    value: int


@must_use
interface Closer:
    """"""Closes things.""""""
    def close(self) -> None:
        ...


@must_use
enum Status:
    """"""Status codes.""""""
    OK
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        var receipt = Assert.IsType<TypeSymbol>(moduleInfo!.ExportedSymbols["Receipt"]);
        Assert.True(receipt.IsMustUse);
        Assert.Equal("A receipt.", receipt.Documentation);
        Assert.Equal("use Receipt2", receipt.DeprecationMessage);

        var total = receipt.Methods.Single(m => m.Name == "total");
        Assert.True(total.IsMustUse);
        Assert.Equal("Total with tax.", total.Documentation);
        Assert.Equal("float", total.SignatureKey);

        // Struct, interface and enum are separate extractors and each dropped the facts
        // independently — one cell per extractor, not one cell for "types".
        Assert.True(Assert.IsType<TypeSymbol>(moduleInfo.ExportedSymbols["Token"]).IsMustUse);
        Assert.True(Assert.IsType<TypeSymbol>(moduleInfo.ExportedSymbols["Closer"]).IsMustUse);
        Assert.True(Assert.IsType<TypeSymbol>(moduleInfo.ExportedSymbols["Status"]).IsMustUse);
        Assert.Equal("A token.", moduleInfo.ExportedSymbols["Token"].Documentation);
        Assert.Equal("Closes things.", moduleInfo.ExportedSymbols["Closer"].Documentation);
        Assert.Equal("Status codes.", moduleInfo.ExportedSymbols["Status"].Documentation);
    }

    /// <summary>
    /// #1365: a field's <c>IsFinal</c> decides whether assignment outside a constructor is legal
    /// and whether the emitted field is <c>readonly</c>; <c>HasDefaultValue</c> decides whether a
    /// constructor must initialize it. Both dropped at the import boundary, for module-level
    /// variables and for class/struct fields alike.
    /// </summary>
    [Fact]
    public void LoadModule_ThreadsFieldFacts()
    {
        var path = CreateModule("fields.spy", @"
MODULE_LEVEL: int = 5

class Holder:
    @final
    locked: int = 1
    loose: int

struct Packed:
    @final
    tag: str = ""t""
    other: str
");

        var moduleInfo = _loader.LoadModule(path, 1, 1);

        var moduleVar = Assert.IsType<VariableSymbol>(moduleInfo!.ExportedSymbols["MODULE_LEVEL"]);
        Assert.True(moduleVar.HasDefaultValue);
        Assert.False(moduleVar.IsFinal);

        var holder = Assert.IsType<TypeSymbol>(moduleInfo.ExportedSymbols["Holder"]);
        var locked = holder.Fields.Single(f => f.Name == "locked");
        Assert.True(locked.IsFinal);
        Assert.True(locked.HasDefaultValue);
        var loose = holder.Fields.Single(f => f.Name == "loose");
        Assert.False(loose.IsFinal);
        Assert.False(loose.HasDefaultValue);

        var packed = Assert.IsType<TypeSymbol>(moduleInfo.ExportedSymbols["Packed"]);
        var tag = packed.Fields.Single(f => f.Name == "tag");
        Assert.True(tag.IsFinal);
        Assert.True(tag.HasDefaultValue);
        Assert.False(packed.Fields.Single(f => f.Name == "other").IsFinal);
    }

    private static TypeSymbol NestedNamed(TypeSymbol owner, string name)
        => owner.NestedTypes.Single(n => n.Name == name);
}
