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
    public void LoadModule_CircularImport_ReportsError()
    {
        // Create two modules that import each other
        var pathA = CreateModule("a.spy", "from b import foo");
        var pathB = CreateModule("b.spy", "from a import bar");

        // Load module A, which will try to load B via the callback
        var result = _loader.LoadModule(pathA, 1, 1, (module, moduleInfo, searchPath) =>
        {
            // Simulate resolving imports within module A: it tries to load B
            _loader.LoadModule(pathB, 1, 1, (innerModule, innerModuleInfo, innerSearchPath) =>
            {
                // B tries to load A again - this should be detected as circular
                _loader.LoadModule(pathA, 1, 1);
            });
        });

        Assert.True(_loader.Diagnostics.HasErrors);
        Assert.Contains(_loader.Diagnostics.GetErrors(),
            d => d.Message.Contains("Circular import"));
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
            ExportedSymbols = new Dictionary<string, Symbol>(),
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
            ExportedSymbols = new Dictionary<string, Symbol>(),
            IsNetModule = true
        };
        _loader.CacheModule(".net:system", netModule);

        var spyModules = _loader.LoadedSpyModules;
        Assert.Single(spyModules);
        Assert.DoesNotContain(spyModules.Values, m => m.IsNetModule);
    }
}
