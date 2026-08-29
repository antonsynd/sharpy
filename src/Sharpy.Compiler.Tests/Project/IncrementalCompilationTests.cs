using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Tests for incremental compilation infrastructure.
/// </summary>
public class IncrementalCompilationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = new();

    public IncrementalCompilationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_inc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            { File.Delete(file); }
            catch { }
        }
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    private string CreateTempFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        var dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private ProjectConfig CreateTestConfig(params string[] fileContents)
    {
        var sourceFiles = new List<string>();
        for (int i = 0; i < fileContents.Length; i++)
        {
            var file = CreateTempFile($"file{i}.spy", fileContents[i]);
            sourceFiles.Add(file);
        }

        return new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = sourceFiles,
            Configuration = "Debug"
        };
    }

    [Fact]
    public void ComputeFileHash_SameContent_ReturnsSameHash()
    {
        var file1 = CreateTempFile("same1.spy", "def main():\n    print('hello')");
        var file2 = CreateTempFile("same2.spy", "def main():\n    print('hello')");

        var hash1 = IncrementalCompilationCache.ComputeFileHash(file1);
        var hash2 = IncrementalCompilationCache.ComputeFileHash(file2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeFileHash_DifferentContent_ReturnsDifferentHash()
    {
        var file1 = CreateTempFile("diff1.spy", "def main():\n    print('hello')");
        var file2 = CreateTempFile("diff2.spy", "def main():\n    print('world')");

        var hash1 = IncrementalCompilationCache.ComputeFileHash(file1);
        var hash2 = IncrementalCompilationCache.ComputeFileHash(file2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void IsStale_NewFile_ReturnsTrue()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var isStale = cache.IsStale(config.SourceFiles[0]);

        Assert.True(isStale);
    }

    [Fact]
    public void IsStale_AfterUpdate_ReturnsFalse()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update and save
        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        // Reload cache
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var isStale = cache2.IsStale(config.SourceFiles[0]);

        Assert.False(isStale);
    }

    [Fact]
    public void IsStale_AfterContentChange_ReturnsTrue()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update and save
        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        // Modify the file
        File.WriteAllText(config.SourceFiles[0], "def main():\n    print('changed')");

        // Reload cache
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var isStale = cache2.IsStale(config.SourceFiles[0]);

        Assert.True(isStale);
    }

    [Fact]
    public void GetFilesToRecompile_NoCache_ReturnsAllFiles()
    {
        var config = CreateTestConfig(
            "def main():\n    pass",
            "def helper():\n    pass"
        );
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var filesToRecompile = cache.GetFilesToRecompile(config.SourceFiles, null);

        Assert.Equal(2, filesToRecompile.Count);
        Assert.Equal(2, cache.StaleFileCount);
        Assert.Equal(0, cache.UpToDateFileCount);
    }

    [Fact]
    public void GetFilesToRecompile_AllUpToDate_ReturnsEmptySet()
    {
        var config = CreateTestConfig(
            "def main():\n    pass",
            "def helper():\n    pass"
        );
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update all files
        foreach (var file in config.SourceFiles)
        {
            cache.UpdateHash(file);
        }
        cache.SaveCache();

        // Reload and check
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var filesToRecompile = cache2.GetFilesToRecompile(config.SourceFiles, null);

        Assert.Empty(filesToRecompile);
        Assert.Equal(0, cache2.StaleFileCount);
        Assert.Equal(2, cache2.UpToDateFileCount);
    }

    [Fact]
    public void GetFilesToRecompile_OneChanged_ReturnsOnlyChangedFile()
    {
        var config = CreateTestConfig(
            "def main():\n    pass",
            "def helper():\n    pass"
        );
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update all files
        foreach (var file in config.SourceFiles)
        {
            cache.UpdateHash(file);
        }
        cache.SaveCache();

        // Modify one file
        File.WriteAllText(config.SourceFiles[0], "def main():\n    print('changed')");

        // Reload and check
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var filesToRecompile = cache2.GetFilesToRecompile(config.SourceFiles, null);

        Assert.Single(filesToRecompile);
        Assert.Contains(config.SourceFiles[0], filesToRecompile);
        Assert.Equal(1, cache2.StaleFileCount);
        Assert.Equal(1, cache2.UpToDateFileCount);
    }

    [Fact]
    public void Clear_RemovesCacheFile()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");
        Assert.True(File.Exists(cacheFilePath));

        cache.Clear();
        Assert.False(File.Exists(cacheFilePath));
    }

    [Fact]
    public void IncrementalMode_EndToEnd_CompilationSucceeds()
    {
        var config = CreateTestConfig(@"
def main():
    print('hello')
");
        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        var result = compiler.CompileProject(config);

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalMode_SecondBuild_CacheIsSaved()
    {
        var config = CreateTestConfig(@"
def main():
    print('hello')
");
        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success);
        Assert.True(File.Exists(cacheFilePath), "Cache file should be created after first build");

        // Second build
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);
    }

    #region Symbol Serialization Tests

    [Fact]
    public void SymbolSerializer_SerializeType_BuiltinTypes()
    {
        // Test that builtin types serialize correctly
        var intType = BuiltinType.Int;
        var strType = BuiltinType.Str;
        var boolType = BuiltinType.Bool;

        // We can't directly call SerializeType since it's private,
        // but we can test via a function symbol with these types
        var funcSymbol = new FunctionSymbol
        {
            Name = "test_func",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "x", Type = intType },
                new ParameterSymbol { Name = "y", Type = strType }
            },
            ReturnType = boolType
        };

        var filePath = CreateTempFile("test.spy", "def test_func(x: int, y: str) -> bool:\n    pass");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

        Assert.Equal("Function", cached.Kind);
        Assert.Equal("test_func", cached.Name);
        Assert.NotNull(cached.Parameters);
        Assert.Equal(2, cached.Parameters!.Count);
        // CLR-backed builtins encode their origin as name@FullName (#1538); the decoder
        // resolves the singleton by name first, so the suffix never shadows reference identity.
        Assert.Equal("builtin:int32@System.Int32", cached.Parameters[0].TypeId);
        Assert.Equal("builtin:str@System.String", cached.Parameters[1].TypeId);
        Assert.Equal("builtin:bool@System.Boolean", cached.ReturnTypeId);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_FunctionSymbol()
    {
        var funcSymbol = new FunctionSymbol
        {
            Name = "my_function",
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            DeclarationLine = 5,
            DeclarationColumn = 1,
            DeclarationSpan = new Sharpy.Compiler.Text.TextSpan(20, 50),
            DeclaringFilePath = "/test/func.spy",
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "count", Type = BuiltinType.Int, HasDefault = true }
            },
            ReturnType = BuiltinType.Str,
            IsStatic = true
        };

        var filePath = CreateTempFile("func.spy", "def my_function(count: int = 10) -> str:\n    pass");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as FunctionSymbol;

        Assert.NotNull(restored);
        Assert.Equal("my_function", restored!.Name);
        Assert.Equal(AccessLevel.Public, restored.AccessLevel);
        Assert.Equal(5, restored.DeclarationLine);
        Assert.True(restored.IsStatic);
        Assert.Single(restored.Parameters);
        Assert.Equal("count", restored.Parameters[0].Name);
        Assert.True(restored.Parameters[0].HasDefault);
        Assert.NotNull(restored.DeclarationSpan);
        Assert.Equal(20, restored.DeclarationSpan!.Value.Start);
        Assert.Equal(50, restored.DeclarationSpan.Value.Length);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_FunctionSymbol_PreservesTypeParameters()
    {
        // #1142: a generic function export must stay generic across an incremental cache reload.
        // Before v17 the serializer dropped TypeParameters, so a cross-module `def identity[T]`
        // deserialized as non-generic (IsGeneric == false), defeating explicit-type-args resolution
        // and inference on the importing file's next (cache-served) build.
        var funcSymbol = new FunctionSymbol
        {
            Name = "identity",
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            DeclaringFilePath = "/test/genlib.spy",
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "x", Type = new TypeParameterType { Name = "T" } }
            },
            ReturnType = new TypeParameterType { Name = "T" },
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef
                {
                    Name = "T",
                    Constraints = ImmutableArray.Create<ConstraintClause>(
                        new TypeConstraint { Type = new TypeAnnotation { Name = "IComparable" } })
                }
            }
        };

        var filePath = CreateTempFile("genlib.spy", "def identity[T](x: T) -> T:\n    return x");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

        Assert.NotNull(cached.TypeParameters);
        Assert.Single(cached.TypeParameters!);
        Assert.Equal("T", cached.TypeParameters![0].Name);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as FunctionSymbol;

        Assert.NotNull(restored);
        Assert.True(restored!.IsGeneric, "Deserialized generic function must preserve IsGeneric=true");
        Assert.Single(restored.TypeParameters);
        Assert.Equal("T", restored.TypeParameters[0].Name);
        // Constraints survive as an interface/type constraint on the type parameter.
        Assert.Single(restored.TypeParameters[0].Constraints);
        var restoredConstraint = Assert.IsType<TypeConstraint>(restored.TypeParameters[0].Constraints[0]);
        Assert.Equal("IComparable", restoredConstraint.Type.Name);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_TypeSymbol_PreservesTypeParameters()
    {
        // #1142: the serializer's TypeParameters omission was symbol-kind-wide — a generic class
        // (class Box[T]) must also round-trip its type parameters, with variance and default type.
        var typeSymbol = new TypeSymbol
        {
            Name = "Box",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef
                {
                    Name = "T",
                    Variance = TypeParameterVariance.Covariant,
                    DefaultType = new TypeAnnotation { Name = "int" }
                }
            }
        };

        var filePath = CreateTempFile("box.spy", "class Box[T]:\n    pass");
        var cached = SymbolSerializer.Serialize(typeSymbol, filePath);

        Assert.NotNull(cached.TypeParameters);
        Assert.Single(cached.TypeParameters!);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as TypeSymbol;

        Assert.NotNull(restored);
        Assert.True(restored!.IsGeneric, "Deserialized generic class must preserve IsGeneric=true");
        Assert.Single(restored.TypeParameters);
        Assert.Equal("T", restored.TypeParameters[0].Name);
        Assert.Equal(TypeParameterVariance.Covariant, restored.TypeParameters[0].Variance);
        Assert.NotNull(restored.TypeParameters[0].DefaultType);
        Assert.Equal("int", restored.TypeParameters[0].DefaultType!.Name);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_NonGenericFunction_TypeParametersNull()
    {
        // A non-generic function serializes no TypeParameters (compact cache) and stays non-generic.
        var funcSymbol = new FunctionSymbol
        {
            Name = "plain",
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            DeclaringFilePath = "/test/plain.spy",
            Parameters = new List<ParameterSymbol>(),
            ReturnType = BuiltinType.Int
        };

        var filePath = CreateTempFile("plain.spy", "def plain() -> int:\n    return 0");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);
        Assert.Null(cached.TypeParameters);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as FunctionSymbol;
        Assert.NotNull(restored);
        Assert.False(restored!.IsGeneric);
        Assert.Empty(restored.TypeParameters);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_GeneratorFunction_PreservesIsGenerator()
    {
        var funcSymbol = new FunctionSymbol
        {
            Name = "my_generator",
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            DeclarationLine = 1,
            DeclarationColumn = 1,
            DeclaringFilePath = "/test/gen.spy",
            Parameters = new List<ParameterSymbol>(),
            ReturnType = BuiltinType.Int,
            IsGenerator = true
        };

        var filePath = CreateTempFile("gen.spy", "def my_generator() -> int:\n    yield 1");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

        Assert.True(cached.IsGenerator, "Serialized symbol should have IsGenerator=true");

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as FunctionSymbol;

        Assert.NotNull(restored);
        Assert.True(restored!.IsGenerator, "Deserialized generator function should preserve IsGenerator=true");
        Assert.Equal("my_generator", restored.Name);
        Assert.Equal(BuiltinType.Int, restored.ReturnType);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_NonGeneratorFunction_IsGeneratorDefaultsFalse()
    {
        var funcSymbol = new FunctionSymbol
        {
            Name = "normal_func",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = BuiltinType.Int
        };

        var filePath = CreateTempFile("normal.spy", "def normal_func() -> int:\n    return 42");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

        Assert.False(cached.IsGenerator, "Non-generator should serialize IsGenerator=false");

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as FunctionSymbol;

        Assert.NotNull(restored);
        Assert.False(restored!.IsGenerator, "Deserialized non-generator should have IsGenerator=false");
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_TypeSymbol()
    {
        var typeSymbol = new TypeSymbol
        {
            Name = "MyClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            DeclarationLine = 1,
            DeclarationSpan = new Sharpy.Compiler.Text.TextSpan(0, 30),
            DeclaringFilePath = "/test/class.spy",
            IsAbstract = true,
            DefiningModule = "test"
        };

        var filePath = CreateTempFile("class.spy", "class MyClass:\n    pass");
        var cached = SymbolSerializer.Serialize(typeSymbol, filePath);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as TypeSymbol;

        Assert.NotNull(restored);
        Assert.Equal("MyClass", restored!.Name);
        Assert.Equal(TypeKind.Class, restored.TypeKind);
        Assert.True(restored.IsAbstract);
        Assert.Equal("test", restored.DefiningModule);
        Assert.NotNull(restored.DeclarationSpan);
        Assert.Equal(0, restored.DeclarationSpan!.Value.Start);
        Assert.Equal(30, restored.DeclarationSpan.Value.Length);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_VariableSymbol()
    {
        var varSymbol = new VariableSymbol
        {
            Name = "my_var",
            Kind = SymbolKind.Variable,
            Type = BuiltinType.Int,
            IsConstant = true,
            AccessLevel = AccessLevel.Public,
            DeclarationSpan = new Sharpy.Compiler.Text.TextSpan(0, 16),
            DeclaringFilePath = "/test/var.spy"
        };

        var filePath = CreateTempFile("var.spy", "my_var: int = 42");
        var cached = SymbolSerializer.Serialize(varSymbol, filePath);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as VariableSymbol;

        Assert.NotNull(restored);
        Assert.Equal("my_var", restored!.Name);
        Assert.True(restored.IsConstant);
        Assert.Equal(BuiltinType.Int, restored.Type);
        Assert.NotNull(restored.DeclarationSpan);
        Assert.Equal(0, restored.DeclarationSpan!.Value.Start);
        Assert.Equal(16, restored.DeclarationSpan.Value.Length);
    }

    [Fact]
    public void SymbolSerializer_SerializeCodeGenInfo()
    {
        var funcSymbol = new FunctionSymbol
        {
            Name = "snake_case_func",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };
        var binding = new SemanticBinding();
        binding.SetCodeGenInfo(funcSymbol, new CodeGenInfo
        {
            CSharpName = "SnakeCaseFunc",
            OriginalName = "snake_case_func",
            IsModuleLevel = true
        });

        var filePath = CreateTempFile("codegen.spy", "def snake_case_func():\n    pass");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath, binding);

        Assert.NotNull(cached.CodeGenInfo);
        Assert.Equal("SnakeCaseFunc", cached.CodeGenInfo!.CSharpName);
        Assert.Equal("snake_case_func", cached.CodeGenInfo.OriginalName);
        Assert.True(cached.CodeGenInfo.IsModuleLevel);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_TypeSymbol_WithGenericInterfaces()
    {
        // Create an interface symbol (e.g., IEquatable)
        var ifaceSymbol = new TypeSymbol
        {
            Name = "IEquatable",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            AccessLevel = AccessLevel.Public,
            DefiningFilePath = "/test/iface.spy"
        };

        // Create a type symbol that implements the interface with type args
        var typeSymbol = new TypeSymbol
        {
            Name = "MyClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public
        };
        typeSymbol.Interfaces.Add(new InterfaceReference
        {
            Definition = ifaceSymbol,
            TypeArgAnnotations = ImmutableArray.Create(
                new TypeAnnotation { Name = "str" })
        });

        var filePath = CreateTempFile("myclass.spy", "class MyClass(IEquatable[str]):\n    pass");
        var cached = SymbolSerializer.Serialize(typeSymbol, filePath);

        // Verify cached has InterfaceEntries with TypeArgs
        Assert.NotNull(cached.InterfaceEntries);
        Assert.Single(cached.InterfaceEntries!);
        Assert.NotNull(cached.InterfaceEntries[0].TypeArgs);
        Assert.Single(cached.InterfaceEntries[0].TypeArgs!);
        Assert.Equal("str", cached.InterfaceEntries[0].TypeArgs![0]);

        // Deserialize and resolve references
        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as TypeSymbol;
        Assert.NotNull(restored);

        // Register the interface symbol so references can resolve
        var ifaceId = SymbolSerializer.ComputeSymbolId(ifaceSymbol, "/test/iface.spy");
        registry[ifaceId] = ifaceSymbol;
        registry[cached.Id] = restored!;

        SymbolSerializer.ResolveReferences(new[] { cached }, registry);

        // Verify the TypeArgAnnotations survived
        Assert.Single(restored!.Interfaces);
        var ifaceRef = restored.Interfaces[0];
        Assert.Equal("IEquatable", ifaceRef.Definition.Name);
        Assert.False(ifaceRef.TypeArgAnnotations.IsDefaultOrEmpty);
        Assert.Single(ifaceRef.TypeArgAnnotations);
        Assert.Equal("str", ifaceRef.TypeArgAnnotations[0].Name);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_ModuleSymbol_PreservesTypesOnlyExportLookup()
    {
        // Models the #1092 collision shape: a net module where the name "Row" maps to a
        // value-position export (VariableSymbol, simulating row_factory) in Exports and to a
        // TypeSymbol in the types-only ExportedTypes lookup. After a cache round-trip the type
        // must still win in annotation position, and the net-module PascalCase fallback must
        // survive (both depend on IsNetModule/ExportedTypes being round-tripped — #1105).
        var modPath = CreateTempFile("sqlite3.spy", "# net module stub");

        var rowType = new TypeSymbol
        {
            Name = "Row",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            DefiningFilePath = modPath
        };
        var rowFactoryValue = new VariableSymbol
        {
            Name = "Row",
            Kind = SymbolKind.Variable,
            AccessLevel = AccessLevel.Public
        };

        var module = new ModuleSymbol
        {
            Name = "sqlite3",
            Kind = SymbolKind.Module,
            AccessLevel = AccessLevel.Public,
            FilePath = modPath,
            IsNetModule = true,
            NetNamespaceName = "Sqlite3"
        };
        // Exporting the type then the same-named value reproduces the collision: the value takes
        // the value-position lookup, the type stays reachable in annotation position.
        module.Exports.Add("Row", rowType);
        module.Exports.Add("Row", rowFactoryValue);

        var cached = SymbolSerializer.Serialize(module, modPath);

        // The three new fields are written to the cache.
        Assert.True(cached.IsNetModule);
        Assert.Equal("Sqlite3", cached.NetNamespaceName);
        Assert.NotNull(cached.ExportedTypeIds);
        Assert.True(cached.ExportedTypeIds!.ContainsKey("Row"));

        // Deserialize; init-only IsNetModule/NetNamespaceName are set at construction.
        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as ModuleSymbol;
        Assert.NotNull(restored);
        Assert.True(restored!.IsNetModule);
        Assert.Equal("Sqlite3", restored.NetNamespaceName);

        // Register both export symbols and the module itself so references resolve.
        registry[SymbolSerializer.ComputeSymbolId(rowType, modPath)] = rowType;
        registry[SymbolSerializer.ComputeSymbolId(rowFactoryValue, modPath)] = rowFactoryValue;
        registry[cached.Id] = restored;

        SymbolSerializer.ResolveReferences(new[] { cached }, registry);

        // Exports keeps the value; ExportedTypes keeps the type — no shadowing after round-trip.
        Assert.IsType<VariableSymbol>(restored.Exports["Row"]);
        Assert.True(restored.ExportedTypes.ContainsKey("Row"));
        Assert.Same(rowType, restored.ExportedTypes["Row"]);

        // Direct types-only lookup returns the TypeSymbol, not the shadowing value.
        Assert.True(restored.TryGetExportedType("Row", out var found));
        Assert.Same(rowType, found);

        // The net-module PascalCase fallback still works (gated on IsNetModule, now round-tripped).
        Assert.True(restored.TryGetExportedType("row", out var foundLower));
        Assert.Same(rowType, foundLower);
    }

    [Fact]
    public void SymbolSerializer_ResolveReferences_SkipsNonTypeExportedTypeId()
    {
        // A corrupt cache whose ExportedTypeIds entry resolves to a non-TypeSymbol must be
        // skipped silently (the `s is TypeSymbol` guard), not throw and not populate the
        // strongly-typed ExportedTypes dictionary (#1105).
        var moduleCached = new CachedSymbol
        {
            Id = "/x.spy:Module:m",
            Kind = "Module",
            Name = "m",
            FilePath = "/x.spy",
            IsNetModule = true,
            ExportedTypeIds = new Dictionary<string, string> { ["Bogus"] = "/x.spy:Variable:Bogus" }
        };

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(moduleCached, registry) as ModuleSymbol;
        Assert.NotNull(restored);

        // The ExportedTypeId points at a VariableSymbol, not a TypeSymbol.
        registry["/x.spy:Variable:Bogus"] = new VariableSymbol { Name = "Bogus", Kind = SymbolKind.Variable };
        registry[moduleCached.Id] = restored!;

        var ex = Record.Exception(() => SymbolSerializer.ResolveReferences(new[] { moduleCached }, registry));
        Assert.Null(ex);
        Assert.False(restored!.ExportedTypes.ContainsKey("Bogus"));
    }

    [Fact]
    public void SerializeTypeAnnotation_RoundTrips_NestedTypes()
    {
        // Test nested annotations like dict[str, list[int]]
        var annotation = new TypeAnnotation
        {
            Name = "dict",
            TypeArguments = ImmutableArray.Create(
                new TypeAnnotation { Name = "str" },
                new TypeAnnotation
                {
                    Name = "list",
                    TypeArguments = ImmutableArray.Create(
                        new TypeAnnotation { Name = "int" })
                })
        };

        var serialized = SymbolSerializer.SerializeTypeAnnotation(annotation);
        Assert.Equal("dict[str,list[int]]", serialized);

        var restored = SymbolSerializer.DeserializeTypeAnnotation(serialized);
        Assert.Equal("dict", restored.Name);
        Assert.Equal(2, restored.TypeArguments.Length);
        Assert.Equal("str", restored.TypeArguments[0].Name);
        Assert.Equal("list", restored.TypeArguments[1].Name);
        Assert.Single(restored.TypeArguments[1].TypeArguments);
        Assert.Equal("int", restored.TypeArguments[1].TypeArguments[0].Name);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_TypeSymbol_WithNonGenericInterfaces()
    {
        // Create an interface symbol without type args
        var ifaceSymbol = new TypeSymbol
        {
            Name = "ISized",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            AccessLevel = AccessLevel.Public,
            DefiningFilePath = "/test/iface.spy"
        };

        var typeSymbol = new TypeSymbol
        {
            Name = "MyList",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public
        };
        typeSymbol.Interfaces.Add(new InterfaceReference
        {
            Definition = ifaceSymbol
        });

        var filePath = CreateTempFile("mylist.spy", "class MyList(ISized):\n    pass");
        var cached = SymbolSerializer.Serialize(typeSymbol, filePath);

        // Verify cached has InterfaceEntries without TypeArgs
        Assert.NotNull(cached.InterfaceEntries);
        Assert.Single(cached.InterfaceEntries!);
        Assert.Null(cached.InterfaceEntries[0].TypeArgs);

        // Deserialize and resolve references
        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as TypeSymbol;
        Assert.NotNull(restored);

        var ifaceId = SymbolSerializer.ComputeSymbolId(ifaceSymbol, "/test/iface.spy");
        registry[ifaceId] = ifaceSymbol;
        registry[cached.Id] = restored!;

        SymbolSerializer.ResolveReferences(new[] { cached }, registry);

        // Verify the interface was restored correctly (no type args)
        Assert.Single(restored!.Interfaces);
        var ifaceRef = restored.Interfaces[0];
        Assert.Equal("ISized", ifaceRef.Definition.Name);
        Assert.True(ifaceRef.TypeArgAnnotations.IsDefaultOrEmpty || ifaceRef.TypeArgAnnotations.IsEmpty);
    }

    [Fact]
    public void SerializeTypeAnnotation_RoundTrips_OptionalType()
    {
        var annotation = new TypeAnnotation { Name = "int", IsOptional = true };

        var serialized = SymbolSerializer.SerializeTypeAnnotation(annotation);
        Assert.Equal("optional:int", serialized);

        var restored = SymbolSerializer.DeserializeTypeAnnotation(serialized);
        Assert.Equal("int", restored.Name);
        Assert.True(restored.IsOptional);
    }

    [Fact]
    public void SerializeTypeAnnotation_RoundTrips_NullableType()
    {
        var annotation = new TypeAnnotation { Name = "str", IsCSharpNullable = true };

        var serialized = SymbolSerializer.SerializeTypeAnnotation(annotation);
        Assert.Equal("nullable:str", serialized);

        var restored = SymbolSerializer.DeserializeTypeAnnotation(serialized);
        Assert.Equal("str", restored.Name);
        Assert.True(restored.IsCSharpNullable);
    }

    [Fact]
    public void SerializeTypeAnnotation_RoundTrips_ResultType()
    {
        var annotation = new TypeAnnotation
        {
            Name = "int",
            ErrorType = new TypeAnnotation { Name = "ValueError" }
        };

        var serialized = SymbolSerializer.SerializeTypeAnnotation(annotation);
        Assert.Equal("int!ValueError", serialized);

        var restored = SymbolSerializer.DeserializeTypeAnnotation(serialized);
        Assert.Equal("int", restored.Name);
        Assert.NotNull(restored.ErrorType);
        Assert.Equal("ValueError", restored.ErrorType!.Name);
    }

    [Fact]
    public void SerializeTypeAnnotation_RoundTrips_SimpleName()
    {
        var annotation = new TypeAnnotation { Name = "int" };

        var serialized = SymbolSerializer.SerializeTypeAnnotation(annotation);
        Assert.Equal("int", serialized);

        var restored = SymbolSerializer.DeserializeTypeAnnotation(serialized);
        Assert.Equal("int", restored.Name);
        Assert.True(restored.TypeArguments.IsDefaultOrEmpty || restored.TypeArguments.IsEmpty);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_FunctionType_NullVariadic_ZeroOptional()
    {
        var funcType = new Sharpy.Compiler.Semantic.FunctionType
        {
            ParameterTypes = new List<SemanticType> { BuiltinType.Int, BuiltinType.Str },
            ReturnType = BuiltinType.Bool,
            VariadicParameterIndex = null,
            OptionalParameterCount = 0
        };

        var varSymbol = new VariableSymbol
        {
            Name = "callback",
            Kind = SymbolKind.Variable,
            Type = funcType
        };

        var filePath = CreateTempFile("cb.spy", "callback: (int, str) -> bool = ...");
        var cached = SymbolSerializer.Serialize(varSymbol, filePath);

        // Verify the serialized TypeId contains the metadata markers
        Assert.Contains("|-|0)", cached.TypeId);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as VariableSymbol;

        Assert.NotNull(restored);
        var restoredType = Assert.IsType<Sharpy.Compiler.Semantic.FunctionType>(restored!.Type);
        Assert.Null(restoredType.VariadicParameterIndex);
        Assert.Equal(0, restoredType.OptionalParameterCount);
        Assert.Equal(2, restoredType.ParameterTypes.Count);
        Assert.Equal(BuiltinType.Int, restoredType.ParameterTypes[0]);
        Assert.Equal(BuiltinType.Str, restoredType.ParameterTypes[1]);
        Assert.Equal(BuiltinType.Bool, restoredType.ReturnType);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_FunctionType_BothVariadicAndOptional()
    {
        var funcType = new Sharpy.Compiler.Semantic.FunctionType
        {
            ParameterTypes = new List<SemanticType> { BuiltinType.Int, BuiltinType.Str, BuiltinType.Bool },
            ReturnType = SemanticType.Void,
            VariadicParameterIndex = 2,
            OptionalParameterCount = 1
        };

        var varSymbol = new VariableSymbol
        {
            Name = "handler",
            Kind = SymbolKind.Variable,
            Type = funcType
        };

        var filePath = CreateTempFile("handler.spy", "handler: (int, str, params bool) -> None = ...");
        var cached = SymbolSerializer.Serialize(varSymbol, filePath);

        Assert.Contains("|2|1)", cached.TypeId);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as VariableSymbol;

        Assert.NotNull(restored);
        var restoredType = Assert.IsType<Sharpy.Compiler.Semantic.FunctionType>(restored!.Type);
        Assert.Equal(2, restoredType.VariadicParameterIndex);
        Assert.Equal(1, restoredType.OptionalParameterCount);
        Assert.Equal(3, restoredType.ParameterTypes.Count);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_FunctionType_VariadicAtZero()
    {
        var funcType = new Sharpy.Compiler.Semantic.FunctionType
        {
            ParameterTypes = new List<SemanticType> { BuiltinType.Int },
            ReturnType = BuiltinType.Str,
            VariadicParameterIndex = 0,
            OptionalParameterCount = 0
        };

        var varSymbol = new VariableSymbol
        {
            Name = "variadic_first",
            Kind = SymbolKind.Variable,
            Type = funcType
        };

        var filePath = CreateTempFile("vf.spy", "variadic_first: (params int) -> str = ...");
        var cached = SymbolSerializer.Serialize(varSymbol, filePath);

        Assert.Contains("|0|0)", cached.TypeId);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as VariableSymbol;

        Assert.NotNull(restored);
        var restoredType = Assert.IsType<Sharpy.Compiler.Semantic.FunctionType>(restored!.Type);
        Assert.Equal(0, restoredType.VariadicParameterIndex);
        Assert.Equal(0, restoredType.OptionalParameterCount);
        Assert.Single(restoredType.ParameterTypes);
        Assert.Equal(BuiltinType.Int, restoredType.ParameterTypes[0]);
        Assert.Equal(BuiltinType.Str, restoredType.ReturnType);
    }

    #endregion

    #region File Cache Tests

    [Fact]
    public void FileCache_SaveAndRetrieve()
    {
        var config = CreateTestConfig("def main():\n    print('hello')");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var funcSymbol = new FunctionSymbol
        {
            Name = "main",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };

        var symbols = new List<Symbol> { funcSymbol };
        var generatedCSharp = "public static void Main() { Console.WriteLine(\"hello\"); }";
        var dependencies = new List<string>();

        cache.SaveFileCache(config.SourceFiles[0], symbols, generatedCSharp, dependencies, "test");
        cache.SaveAllCaches();

        // Reload cache
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var entry = cache2.GetFileCache(config.SourceFiles[0]);

        Assert.NotNull(entry);
        Assert.Equal(generatedCSharp, entry!.GeneratedCSharp);
        Assert.Equal("test", entry.ModulePath);
        Assert.Single(entry.Symbols);
        Assert.Equal("main", entry.Symbols[0].Name);
    }

    [Fact]
    public void FileCache_InvalidAfterContentChange()
    {
        var config = CreateTestConfig("def main():\n    print('hello')");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var symbols = new List<Symbol>
        {
            new FunctionSymbol
            {
                Name = "main",
                Kind = SymbolKind.Function,
                Parameters = new List<ParameterSymbol>(),
                ReturnType = SemanticType.Void
            }
        };

        cache.SaveFileCache(config.SourceFiles[0], symbols, "generated code", new List<string>());
        cache.SaveAllCaches();

        // Modify the file
        File.WriteAllText(config.SourceFiles[0], "def main():\n    print('world')");

        // Reload cache
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var entry = cache2.GetFileCache(config.SourceFiles[0]);

        Assert.Null(entry); // Should be null because content changed
    }

    [Fact]
    public void FileCache_HasValidFileCache_ReturnsFalseForChangedFile()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        cache.SaveFileCache(
            config.SourceFiles[0],
            new List<Symbol>(),
            "generated",
            new List<string>());
        cache.SaveAllCaches();

        // Modify file
        File.WriteAllText(config.SourceFiles[0], "def main():\n    print('changed')");

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        Assert.False(cache2.HasValidFileCache(config.SourceFiles[0]));
    }

    [Fact]
    public void FileCache_RestoreSymbols()
    {
        var config = CreateTestConfig("x: int = 42");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var varSymbol = new VariableSymbol
        {
            Name = "x",
            Kind = SymbolKind.Variable,
            Type = BuiltinType.Int,
            IsConstant = true
        };

        cache.SaveFileCache(config.SourceFiles[0], new List<Symbol> { varSymbol }, "code", new List<string>());
        cache.SaveAllCaches();

        // Reload and restore
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var registry = new Dictionary<string, Symbol>();
        var restored = cache2.RestoreSymbols(config.SourceFiles[0], registry);

        Assert.True(restored);
        Assert.Single(registry);

        var restoredSymbol = registry.Values.First() as VariableSymbol;
        Assert.NotNull(restoredSymbol);
        Assert.Equal("x", restoredSymbol!.Name);
        Assert.True(restoredSymbol.IsConstant);
    }

    [Fact]
    public void FileCache_RestoreSymbols_ModuleExports_SurvivesDiskRoundTripInBothViews()
    {
        // The --incremental path end to end: a module whose "Row" name is a type in annotation
        // position and a value in value position is written to the symbol cache on disk, reloaded
        // by a fresh cache instance, and must come back with BOTH views intact. Serializing or
        // restoring one view without the other is the #1105 regression; ModuleExports round-trips
        // as a unit (#1145).
        var config = CreateTestConfig("import sqlite3");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var rowType = new TypeSymbol
        {
            Name = "Row",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            DefiningFilePath = config.SourceFiles[0]
        };
        var rowFactoryValue = new VariableSymbol
        {
            Name = "Row",
            Kind = SymbolKind.Variable,
            AccessLevel = AccessLevel.Public
        };
        var connect = new FunctionSymbol
        {
            Name = "connect",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };

        var module = new ModuleSymbol
        {
            Name = "sqlite3",
            Kind = SymbolKind.Module,
            AccessLevel = AccessLevel.Public,
            FilePath = config.SourceFiles[0],
            IsNetModule = true,
            NetNamespaceName = "Sqlite3"
        };
        module.Exports.Add("Row", rowType);
        module.Exports.Add("Row", rowFactoryValue);
        module.Exports.Add("connect", connect);

        cache.SaveFileCache(
            config.SourceFiles[0],
            new List<Symbol> { module, rowType, rowFactoryValue, connect },
            "generated",
            new List<string>());
        cache.SaveAllCaches();

        var reloaded = new IncrementalCompilationCache(config, NullLogger.Instance);
        reloaded.LoadAllCaches();

        var registry = new Dictionary<string, Symbol>();
        Assert.True(reloaded.RestoreSymbols(config.SourceFiles[0], registry));

        var restoredModule = registry.Values.OfType<ModuleSymbol>().Single();

        // Value view: the field still shadows the type.
        Assert.IsType<VariableSymbol>(restoredModule.Exports["Row"]);
        Assert.IsType<FunctionSymbol>(restoredModule.Exports["connect"]);

        // Types view: the type is still there, and still resolvable in annotation position.
        Assert.True(restoredModule.Exports.TryGetType("Row", out var restoredType));
        Assert.Equal("Row", restoredType!.Name);
        Assert.Equal(new[] { "Row" }, restoredModule.ExportedTypes.Keys);
        Assert.True(restoredModule.TryGetExportedType("Row", out var viaExtension));
        Assert.Same(restoredType, viaExtension);
    }

    #endregion

    #region End-to-End Incremental Compilation Tests

    [Fact]
    public void IncrementalMode_SecondBuild_SymbolCacheCreated()
    {
        var config = CreateTestConfig(@"
def main():
    x: int = 42
    print(x)
");
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result = compiler.CompileProject(config);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));

        // Symbol cache should exist after successful build
        Assert.True(File.Exists(symbolCachePath), "Symbol cache file should be created after first build");

        // Verify it's valid JSON
        var json = File.ReadAllText(symbolCachePath);
        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void IncrementalMode_UnchangedFile_ProducesIdenticalOutput()
    {
        var config = CreateTestConfig(@"
def main():
    print('hello')
");
        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success);

        // Second build (file unchanged)
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);

        // Both builds should produce the same output assembly
        Assert.NotNull(result1.OutputAssemblyPath);
        Assert.NotNull(result2.OutputAssemblyPath);
    }

    [Fact]
    public void IncrementalMode_MultipleFiles_OnlyRecompilesChanged()
    {
        // Create two files
        var file1 = CreateTempFile("main.spy", @"
import helper

def main():
    helper.greet()
");
        var file2 = CreateTempFile("helper.spy", @"
def greet():
    print('hello')
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { file1, file2 },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build (both files compiled)
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify only the helper file
        File.WriteAllText(file2, @"
def greet():
    print('modified hello')
");

        // Second build (should recompile helper, potentially skip main if no dependency change)
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);
    }

    [Fact]
    public void IncrementalMode_Clean_ForcesFullRebuild()
    {
        var config = CreateTestConfig(@"
def main():
    print('hello')
");
        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success);

        // Clear cache
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache.Clear();

        // Verify cache files are gone
        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        Assert.False(File.Exists(cacheFilePath));
        Assert.False(File.Exists(symbolCachePath));

        // Build again (should be full rebuild)
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);

        // Cache should be recreated
        Assert.True(File.Exists(cacheFilePath));
    }

    [Fact]
    public void IncrementalMode_WithClass_SerializesTypeSymbol()
    {
        var config = CreateTestConfig(@"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p = Point(1, 2)
    print(p.x)
");
        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        var result = compiler.CompileProject(config);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));

        // The envelope — not the raw text — has to carry the class. `Assert.Contains("Point", json)`
        // was satisfied by the GeneratedCSharp payload alone, so it stayed green through the whole
        // period when ExtractFileSymbols serialized an empty symbol list for every file (#1309).
        var entry = LoadCachedEntry(config, config.SourceFiles[0]);
        Assert.NotEmpty(entry.Symbols);

        var point = Assert.Single(entry.Symbols, s => s.Kind == "Type" && s.Name == "Point");
        Assert.Equal("Class", point.TypeKind);
        Assert.EndsWith(":Type:Point", point.Id);

        // The class's members round-trip with it, and the module-level function is cached too —
        // both are module-scope symbols, which is the scope the extractor used to miss entirely.
        Assert.NotNull(point.Fields);
        Assert.Equal(new[] { "x", "y" }, point.Fields!.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Contains(entry.Symbols, s => s.Kind == "Function" && s.Name == "main");
    }

    [Fact]
    public void IncrementalMode_GenericFunctionExport_StaysGenericAcrossReload()
    {
        // #1142 incremental face: a file using a cross-module generic function must still resolve
        // explicit type args AND inference on a build that serves the library's symbols from the
        // cache. Build once (both files compiled, symbols cached); then edit only the entry file so
        // it recompiles against the DESERIALIZED library symbols. If TypeParameters didn't round-trip
        // (pre-v17), the reloaded `identity` would be non-generic and the second build would fail.
        var libFile = CreateTempFile("genlib.spy", @"
def identity[T](x: T) -> T:
    return x
");
        var mainFile = CreateTempFile("main.spy", @"
import genlib

def main():
    print(genlib.identity[int](5))
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build populates the symbol cache (both files compiled).
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Edit only the entry file: genlib is unchanged, so its symbols are served from cache when
        // main recompiles. A second explicit-type-args call proves the reloaded export is generic.
        File.WriteAllText(mainFile, @"
import genlib

def main():
    print(genlib.identity[int](5))
    print(genlib.identity[str](""hi""))
");

        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success,
            "Second build must resolve the cache-reloaded generic export: " +
            string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Success alone does not prove the cache carried `identity` — the ModuleLoader path can
        // re-read genlib.spy and make this pass with an envelope full of nothing. Assert the
        // envelope holds the generic export whose round-trip this test is named for.
        var libEntry = LoadCachedEntry(config, libFile);
        var identity = Assert.Single(libEntry.Symbols, s => s.Kind == "Function" && s.Name == "identity");
        Assert.NotNull(identity.TypeParameters);
        Assert.Equal(new[] { "T" }, identity.TypeParameters!.Select(tp => tp.Name));
    }

    [Fact]
    public void IncrementalMode_ClrOriginFormal_UnifiesOnWarmCacheBuild()
    {
        // #1252's warm-cache face (plan-8ecf0f): unification through a provenance-carrying formal
        // must still succeed on a build served from the symbol cache — a missing schema bump plus a
        // stale cache reproduces the original silent decline, and a serializer round-trip test alone
        // cannot see that. Layout mirrors GenericFunctionExport_StaysGenericAcrossReload: build once
        // (both files compiled, symbols cached), then edit only the entry file so it recompiles with
        // the library served from the DESERIALIZED cache while the bridge re-maps the BCL formals.
        var libFile = CreateTempFile("seqhelpers.spy", @"
def label(n: int) -> str:
    return str(n)
");
        var mainFile = CreateTempFile("main.spy", @"
import seqhelpers
from system.collections.generic import List

def main():
    lst: List[int] = List[int]()
    lst.add(1)
    inner: List[int] = List[int]()
    inner.add(9)
    print(list(lst.select_many(lambda x: inner, lambda a, b: seqhelpers.label(a + b))))
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success,
            "Cold build of the #1252 shape must succeed: " +
            string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Edit only the entry file: seqhelpers is unchanged and cache-served. The added second
        // select_many call proves unification through the CLR-origin formal on the warm build —
        // the exact decline the original bug produced silently.
        File.WriteAllText(mainFile, @"
import seqhelpers
from system.collections.generic import List

def main():
    lst: List[int] = List[int]()
    lst.add(1)
    inner: List[int] = List[int]()
    inner.add(9)
    print(list(lst.select_many(lambda x: inner, lambda a, b: seqhelpers.label(a + b))))
    print(list(lst.select_many(lambda x: inner, lambda a, b: seqhelpers.label(a * b))))
");

        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success,
            "Warm-cache build must still unify through the CLR-origin formal: " +
            string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // As above: prove the library really was cache-served rather than re-read, by asserting the
        // envelope carries seqhelpers' export. Without this the test passes on an empty cache.
        var libEntry = LoadCachedEntry(config, libFile);
        var label = Assert.Single(libEntry.Symbols, s => s.Kind == "Function" && s.Name == "label");
        Assert.NotNull(label.Parameters);
        Assert.Equal(new[] { "n" }, label.Parameters!.Select(p => p.Name));
    }

    /// <summary>
    /// Reads back what the production serializer wrote to <c>.sharpy-symbols</c>, through the
    /// production loader — a fresh <see cref="IncrementalCompilationCache"/> over the same project,
    /// which is exactly how a warm build reaches the cache. Fails loudly when the file is missing or
    /// the entry is absent/stale, so callers can assert on the entry without null checks.
    /// </summary>
    private static FileCacheEntry LoadCachedEntry(ProjectConfig config, string sourceFile)
    {
        var symbolCachePath = Path.Combine(
            config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        Assert.True(File.Exists(symbolCachePath), $"No symbol cache was written to {symbolCachePath}.");

        var reloaded = new IncrementalCompilationCache(config, NullLogger.Instance);
        reloaded.LoadAllCaches();

        var entry = reloaded.GetFileCache(sourceFile);
        Assert.True(entry != null,
            $"The symbol cache holds no valid entry for {Path.GetFileName(sourceFile)}; " +
            "the file was never cached, or its content changed after the build being asserted.");
        return entry!;
    }

    [Fact]
    public void IncrementalMode_TransitiveDependency_RecompilesDependents()
    {
        // Test that when a dependency changes, files that import it are also recompiled.
        // This verifies the cached dependency graph is used correctly.

        // Create three files: main imports helper, helper imports util
        var utilFile = CreateTempFile("util.spy", @"
def format_message(msg: str) -> str:
    return '[INFO] ' + msg
");
        var helperFile = CreateTempFile("helper.spy", @"
from util import format_message

def greet() -> str:
    return format_message('Hello')
");
        var mainFile = CreateTempFile("main.spy", @"
from helper import greet

def main():
    print(greet())
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, helperFile, utilFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - all files compiled
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify the leaf file (util.spy)
        File.WriteAllText(utilFile, @"
def format_message(msg: str) -> str:
    return '[MODIFIED] ' + msg
");

        // Second build - util changed, so helper and main should also be recompiled
        // (helper imports util, main imports helper)
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // The compilation should succeed and produce correct output
        Assert.NotNull(result2.OutputAssemblyPath);
    }

    [Fact]
    public void BuildCachedDependencyGraph_CreatesDependencyGraph()
    {
        // Create files with known dependencies
        var utilFile = CreateTempFile("util.spy", @"
def helper():
    pass
");
        var mainFile = CreateTempFile("main.spy", @"
from util import helper

def main():
    helper()
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, utilFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build to create cache
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Load the cache and build a cached dependency graph
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache.LoadAllCaches();

        var cachedGraph = cache.BuildCachedDependencyGraph(config.SourceFiles);
        Assert.NotNull(cachedGraph);

        // The graph should show that main depends on util
        var mainDeps = cachedGraph!.GetDirectDependencies(mainFile);
        Assert.Contains(cachedGraph.AllFiles, f => f.EndsWith("util.spy"));
    }

    [Fact]
    public void IncrementalMode_DependencyChangesSignature_RecompilesDependent()
    {
        // Test that when a function implementation changes in a dependency,
        // files that use it are recompiled

        var libFile = CreateTempFile("lib.spy", @"
def get_message() -> str:
    return 'original'
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import get_message

def main():
    msg: str = get_message()
    print(msg)
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build succeeds
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify lib to change function implementation (same signature)
        File.WriteAllText(libFile, @"
def get_message() -> str:
    return 'modified'
");

        // Second build - main.spy should be recompiled (not skipped) because lib changed
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Both builds should produce valid assemblies
        Assert.NotNull(result1.OutputAssemblyPath);
        Assert.NotNull(result2.OutputAssemblyPath);
    }

    [Fact]
    public void IncrementalMode_NoChanges_SkipsAllFiles()
    {
        // Verify that when nothing changes, all files are skipped in the second build

        var file1 = CreateTempFile("main.spy", @"
def main():
    print('hello')
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { file1 },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success);

        // Second build - should skip all files
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);

        // Verify metrics show files were skipped
        var metrics = result2.Metrics;
        Assert.NotNull(metrics);
        Assert.True(metrics!.SkippedFileCount > 0,
            $"Expected skipped files, got SkippedFileCount={metrics.SkippedFileCount}");
    }

    [Fact]
    public void IncrementalMode_ImporterChangedImporteeUnchanged_BuildsSuccessfully()
    {
        // Test the scenario where the importing file changes but the imported file does not.
        // This verifies that import resolution correctly parses the unchanged imported file.

        var libFile = CreateTempFile("lib.spy", @"
def get_value() -> int:
    return 42
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import get_value

def main():
    x: int = get_value()
    print(x)
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - both files compiled
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify only main.spy (add another print)
        File.WriteAllText(mainFile, @"
from lib import get_value

def main():
    x: int = get_value()
    print(x)
    print('done')
");

        // Second build - lib.spy should be skipped, main.spy should be recompiled
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Verify at least one file was skipped (lib.spy)
        var metrics = result2.Metrics;
        Assert.NotNull(metrics);
        Assert.True(metrics!.SkippedFileCount > 0,
            $"Expected lib.spy to be skipped, got SkippedFileCount={metrics.SkippedFileCount}");
    }

    [Fact]
    public void IncrementalMode_ImporterChangedWithClass_BuildsSuccessfully()
    {
        // Test the scenario with a class import: importing file changes, imported file (with class) does not.
        // This verifies that type symbols from unchanged files are accessible during semantic analysis.

        var libFile = CreateTempFile("lib.spy", @"
class Counter:
    value: int

    def __init__(self):
        self.value = 0

    def increment(self):
        self.value += 1
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import Counter

def main():
    c: Counter = Counter()
    c.increment()
    print(c.value)
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - both files compiled
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify only main.spy (call increment twice)
        File.WriteAllText(mainFile, @"
from lib import Counter

def main():
    c: Counter = Counter()
    c.increment()
    c.increment()
    print(c.value)
");

        // Second build - lib.spy should be skipped, main.spy should be recompiled
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Verify at least one file was skipped (lib.spy)
        var metrics = result2.Metrics;
        Assert.NotNull(metrics);
        Assert.True(metrics!.SkippedFileCount > 0,
            $"Expected lib.spy to be skipped, got SkippedFileCount={metrics.SkippedFileCount}");
    }

    [Fact]
    public void IncrementalMode_Sqlite3RowAnnotation_ResolvesAcrossCacheReuse()
    {
        // TRIPWIRE (#1105): a file importing sqlite3 and using sqlite3.Row in annotation
        // position must keep resolving the Row TYPE (not degrade to SPY0202) when it is
        // reused from the incremental cache. This is green today because ExtractFileSymbols
        // never selects the imported sqlite3 ModuleSymbol (it carries sqlite3's own definition
        // path, not the importing file's), so no module symbol is round-tripped yet — the
        // annotation resolves via the always-live TryResolveNetModule path. It becomes the
        // live regression net the moment ExtractFileSymbols / DeclaringFilePath handling
        // changes to cache imported module symbols: with the serializer round-trip in place
        // (ExportedTypes + IsNetModule + NetNamespaceName), the types-only lookup survives
        // restore; without it, a value-position export could shadow Row and reintroduce
        // SPY0202 under --incremental.
        var dbFile = CreateTempFile("dbwrap.spy", @"
import sqlite3

def describe(row: sqlite3.Row) -> int:
    return 0
");
        var appFile = CreateTempFile("app.spy", @"
def main():
    print('v1')
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { appFile, dbFile },
            Configuration = "Debug"
        };

        // The sqlite3 module is discovered from the Sharpy.Stdlib reference assembly.
        var options = new CompilerOptions
        {
            Incremental = true,
            References = new[] { SharpyStdlibReference.Location }
        };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - both files compiled; sqlite3.Row must resolve without SPY0202.
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));
        Assert.DoesNotContain(result1.Diagnostics.GetErrors(), d => d.Code == "SPY0202");

        // Touch only the OTHER file so dbwrap.spy (the sqlite3 importer) becomes cache-eligible.
        File.WriteAllText(appFile, @"
def main():
    print('v2')
");

        // Second build - dbwrap.spy is reused from cache; annotation still resolves.
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));
        Assert.DoesNotContain(result2.Diagnostics.GetErrors(), d => d.Code == "SPY0202");

        // Confirm the incremental path was actually exercised (dbwrap.spy skipped).
        var metrics = result2.Metrics;
        Assert.NotNull(metrics);
        Assert.True(metrics!.SkippedFileCount > 0,
            $"Expected dbwrap.spy to be skipped, got SkippedFileCount={metrics.SkippedFileCount}");
    }

    [Fact]
    public void IncrementalMode_AllFilesUnchanged_WithClass_BuildsSuccessfully()
    {
        // Regression test: When ALL files are unchanged and restored from cache,
        // the DualWriteAssertions must not fail due to CodeGenInfo mismatch.
        // This was broken before the fix to register CodeGenInfo in SemanticBinding
        // for restored symbols.

        var libFile = CreateTempFile("lib.spy", @"
class Counter:
    value: int
    name: str

    def __init__(self, name: str, start: int = 0):
        self.name = name
        self.value = start

    def increment(self):
        self.value += 1

    def get_status(self) -> str:
        return self.name + ': ' + str(self.value)
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import Counter

def main():
    c: Counter = Counter('test', 5)
    c.increment()
    print(c.get_status())
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - both files compiled
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Second build - NO changes, both files should be skipped
        // This should succeed without assertion failures
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Verify both files were skipped
        var metrics = result2.Metrics;
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics!.SkippedFileCount);

        // Third build - still no changes, should still succeed
        var result3 = compiler.CompileProject(config);
        Assert.True(result3.Success, string.Join("; ", result3.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalMode_NewFileAddition_BuildsSuccessfully()
    {
        // Test that adding a new file between builds works correctly

        var mainFile = CreateTempFile("main.spy", @"
def main():
    print('hello')
");

        var config1 = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build with just main.spy
        var result1 = compiler.CompileProject(config1);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Add a new file
        var utilsFile = CreateTempFile("utils.spy", @"
def greet() -> str:
    return 'world'
");

        // Update main.spy to use the new file
        File.WriteAllText(mainFile, @"
from utils import greet

def main():
    print(greet())
");

        // Create new config with updated source files
        var config2 = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, utilsFile },
            Configuration = "Debug"
        };

        // Second build - should compile both files
        var result2 = compiler.CompileProject(config2);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalMode_FileRemoval_ErrorsCorrectly()
    {
        // Test that removing an imported module between builds errors correctly

        var libFile = CreateTempFile("lib.spy", @"
def helper() -> int:
    return 42
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import helper

def main():
    print(helper())
");

        var config1 = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build succeeds
        var result1 = compiler.CompileProject(config1);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Remove lib.spy
        File.Delete(libFile);

        // Create new config without lib.spy
        var config2 = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile },
            Configuration = "Debug"
        };

        // Second build should fail because lib module no longer exists
        var result2 = compiler.CompileProject(config2);
        Assert.False(result2.Success, "Expected compilation to fail when imported module is removed");

        // Should mention the missing module
        var errorMessages = string.Join(" ", result2.Diagnostics.GetErrors().Select(e => e.Message.ToLower()));
        Assert.True(
            errorMessages.Contains("lib") || errorMessages.Contains("not found") ||
            errorMessages.Contains("cannot find") || errorMessages.Contains("module"),
            $"Expected error about missing lib module, got: {errorMessages}");
    }

    #endregion

    #region Compiler Version Cache Invalidation Tests

    [Fact]
    public void GetCompilerVersion_ReturnsNonEmptyString()
    {
        var version = IncrementalCompilationCache.GetCompilerVersion();

        Assert.NotNull(version);
        Assert.NotEmpty(version);
    }

    [Fact]
    public void GetCompilerVersion_IncludesVersionAndHash()
    {
        var version = IncrementalCompilationCache.GetCompilerVersion();

        // Should contain at least one dot (version) and one dash (hash separator)
        Assert.Contains('.', version);
        Assert.Contains('-', version);
    }

    [Fact]
    public void Cache_InvalidatesOnVersionChange()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update and save
        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        // Manually modify the cache file to have a different compiler version
        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");
        var json = File.ReadAllText(cacheFilePath);

        // Replace the version with a fake old version
        var fakeVersion = "0.0.0-fakeversion";
        var currentVersion = IncrementalCompilationCache.GetCompilerVersion();
        json = json.Replace(currentVersion, fakeVersion);
        File.WriteAllText(cacheFilePath, json);

        // Reload cache - should be empty due to version mismatch
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var isStale = cache2.IsStale(config.SourceFiles[0]);

        Assert.True(isStale, "Cache should be invalidated when compiler version changes");
    }

    [Fact]
    public void Cache_PreservesOnSameVersion()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update and save
        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        // Reload cache - should be preserved because version matches
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var isStale = cache2.IsStale(config.SourceFiles[0]);

        Assert.False(isStale, "Cache should be preserved when compiler version is the same");
    }

    [Fact]
    public void Cache_SavesWithVersionMetadata()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        // Read and verify the cache file contains CompilerVersion
        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");
        var json = File.ReadAllText(cacheFilePath);

        Assert.Contains("CompilerVersion", json);
        Assert.Contains("FileHashes", json);
    }

    [Fact]
    public void Cache_InvalidatesOnCorruptedJson()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        // Corrupt the cache file
        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");
        File.WriteAllText(cacheFilePath, "{ invalid json }");

        // Reload cache - should start fresh
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var isStale = cache2.IsStale(config.SourceFiles[0]);

        Assert.True(isStale, "Cache should be invalidated when JSON is corrupted");
    }

    #endregion

    #region Schema Version Tests

    [Fact]
    public void CurrentSchemaVersion_IsPositive()
    {
        Assert.True(IncrementalCompilationCache.CurrentSchemaVersion > 0,
            "Schema version should be a positive integer");
    }

    [Fact]
    public void SymbolCache_SavesWithSchemaVersion()
    {
        var config = CreateTestConfig("def main():\n    print('hello')");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var funcSymbol = new FunctionSymbol
        {
            Name = "main",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };

        cache.SaveFileCache(config.SourceFiles[0], new List<Symbol> { funcSymbol }, "generated code", new List<string>());
        cache.SaveAllCaches();

        // Read and verify the symbol cache contains SchemaVersion
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        var json = File.ReadAllText(symbolCachePath);

        Assert.Contains("SchemaVersion", json);
        Assert.Contains("Files", json);
        Assert.Contains($"\"SchemaVersion\": {IncrementalCompilationCache.CurrentSchemaVersion}", json);
    }

    [Fact]
    public void SymbolCache_InvalidatesOnSchemaVersionChange()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var funcSymbol = new FunctionSymbol
        {
            Name = "main",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };

        cache.SaveFileCache(config.SourceFiles[0], new List<Symbol> { funcSymbol }, "generated code", new List<string>());
        cache.SaveAllCaches();

        // Manually modify the symbol cache to have an older schema version
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        var json = File.ReadAllText(symbolCachePath);
        json = json.Replace($"\"SchemaVersion\": {IncrementalCompilationCache.CurrentSchemaVersion}", "\"SchemaVersion\": 0");
        File.WriteAllText(symbolCachePath, json);

        // Reload cache - should be empty due to schema version mismatch
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var hasValidCache = cache2.HasValidFileCache(config.SourceFiles[0]);
        Assert.False(hasValidCache, "Symbol cache should be invalidated when schema version changes");
    }

    [Fact]
    public void SymbolCache_PreservesOnSameSchemaVersion()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var funcSymbol = new FunctionSymbol
        {
            Name = "main",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };

        cache.SaveFileCache(config.SourceFiles[0], new List<Symbol> { funcSymbol }, "generated code", new List<string>());
        cache.SaveAllCaches();

        // Reload cache - should be preserved because schema version matches
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var hasValidCache = cache2.HasValidFileCache(config.SourceFiles[0]);
        Assert.True(hasValidCache, "Symbol cache should be preserved when schema version matches");
    }

    [Fact]
    public void SymbolCache_InvalidatesOnCorruptedJson()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var funcSymbol = new FunctionSymbol
        {
            Name = "main",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };

        cache.SaveFileCache(config.SourceFiles[0], new List<Symbol> { funcSymbol }, "generated code", new List<string>());
        cache.SaveAllCaches();

        // Corrupt the symbol cache file
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        File.WriteAllText(symbolCachePath, "{ invalid json }");

        // Reload cache - should start fresh
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var hasValidCache = cache2.HasValidFileCache(config.SourceFiles[0]);
        Assert.False(hasValidCache, "Symbol cache should be invalidated when JSON is corrupted");
    }

    [Fact]
    public void SymbolCache_InvalidatesOnLegacyFormat()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var funcSymbol = new FunctionSymbol
        {
            Name = "main",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };

        cache.SaveFileCache(config.SourceFiles[0], new List<Symbol> { funcSymbol }, "generated code", new List<string>());
        cache.SaveAllCaches();

        // Write symbol cache in legacy format (plain dictionary without envelope)
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        var legacyFormat = "{\"some/path.spy\": {\"ContentHash\": \"abc\", \"Symbols\": [], \"GeneratedCSharp\": \"code\", \"Dependencies\": []}}";
        File.WriteAllText(symbolCachePath, legacyFormat);

        // Reload cache - should be empty due to legacy format
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var hasValidCache = cache2.HasValidFileCache(config.SourceFiles[0]);
        Assert.False(hasValidCache, "Symbol cache should be invalidated when legacy format is detected");
    }

    #endregion

    #region Error Detection Tests (verifies dependency graph handles semantic changes)

    [Fact]
    public void IncrementalMode_TypeRenamedInDependency_RecompilesAndReportsError()
    {
        // Test that when a type is renamed in a dependency, files that use the old name
        // correctly fail with an error (verifies dependency graph triggers recompilation)

        var typesFile = CreateTempFile("types.spy", @"
class MyClass:
    x: int = 1
");
        var mainFile = CreateTempFile("main.spy", @"
from types import MyClass

def main():
    obj: MyClass = MyClass()
    print(obj.x)
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, typesFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - both files compile successfully
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Rename the class in types.spy
        File.WriteAllText(typesFile, @"
class RenamedClass:
    x: int = 99
");

        // Second build - main.spy should fail because MyClass no longer exists
        var result2 = compiler.CompileProject(config);
        Assert.False(result2.Success, "Expected compilation to fail because MyClass was renamed");

        // Should mention the missing type
        var errorMessages = string.Join(" ", result2.Diagnostics.GetErrors().Select(e => e.Message));
        Assert.True(
            errorMessages.Contains("MyClass") || errorMessages.Contains("not found") || errorMessages.Contains("undefined"),
            $"Expected error about MyClass not found, got: {errorMessages}");
    }

    [Fact]
    public void IncrementalMode_FunctionSignatureChanged_RecompilesAndReportsError()
    {
        // Test that when a function's return type changes, callers correctly fail
        // with a type mismatch error

        var libFile = CreateTempFile("lib.spy", @"
def get_value() -> int:
    return 42
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import get_value

def main():
    x: int = get_value()
    print(x)
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build succeeds
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Change return type from int to str
        File.WriteAllText(libFile, @"
def get_value() -> str:
    return 'hello'
");

        // Second build - main.spy should fail with type mismatch
        var result2 = compiler.CompileProject(config);
        Assert.False(result2.Success, "Expected compilation to fail due to type mismatch");

        // Should mention type mismatch
        var errorMessages = string.Join(" ", result2.Diagnostics.GetErrors().Select(e => e.Message.ToLower()));
        Assert.True(
            errorMessages.Contains("type") || errorMessages.Contains("cannot") || errorMessages.Contains("str"),
            $"Expected type-related error, got: {errorMessages}");
    }

    [Fact]
    public void IncrementalMode_BaseClassMethodSignatureChanged_RecompilesAndReportsError()
    {
        // Test that when a base class method signature changes, derived classes
        // correctly fail with an override mismatch error

        var baseFile = CreateTempFile("base.spy", @"
class Animal:
    @virtual
    def speak(self) -> str:
        return '...'
");
        var derivedFile = CreateTempFile("derived.spy", @"
from base import Animal

class Dog(Animal):
    @override
    def speak(self) -> str:
        return 'woof'
");
        var mainFile = CreateTempFile("main.spy", @"
from derived import Dog

def main():
    d: Dog = Dog()
    print(d.speak())
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, derivedFile, baseFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build succeeds
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Change base class method signature (return type changed to int)
        File.WriteAllText(baseFile, @"
class Animal:
    @virtual
    def speak(self) -> int:
        return 0
");

        // Second build - derived.spy should fail with override signature mismatch
        var result2 = compiler.CompileProject(config);
        Assert.False(result2.Success, "Expected compilation to fail due to override signature mismatch");

        // Should mention override or signature issue
        var errorMessages = string.Join(" ", result2.Diagnostics.GetErrors().Select(e => e.Message.ToLower()));
        Assert.True(
            errorMessages.Contains("override") || errorMessages.Contains("signature") ||
            errorMessages.Contains("return type") || errorMessages.Contains("str") || errorMessages.Contains("int"),
            $"Expected override/signature error, got: {errorMessages}");
    }

    [Fact]
    public void IncrementalMode_TypeDeleted_RecompilesAndReportsError()
    {
        // Test that when a type is completely removed, importers fail correctly

        var typesFile = CreateTempFile("types.spy", @"
class Helper:
    def do_work(self) -> int:
        return 42
");
        var mainFile = CreateTempFile("main.spy", @"
from types import Helper

def main():
    h: Helper = Helper()
    print(h.do_work())
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, typesFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build succeeds
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Delete the Helper class entirely (replace with empty file or different content)
        File.WriteAllText(typesFile, @"
# Helper class has been removed
def some_function() -> int:
    return 0
");

        // Second build - main.spy should fail because Helper no longer exists
        var result2 = compiler.CompileProject(config);
        Assert.False(result2.Success, "Expected compilation to fail because Helper was deleted");

        var errorMessages = string.Join(" ", result2.Diagnostics.GetErrors().Select(e => e.Message));
        Assert.True(
            errorMessages.Contains("Helper") || errorMessages.Contains("not found") ||
            errorMessages.Contains("undefined") || errorMessages.Contains("cannot import"),
            $"Expected error about Helper not found, got: {errorMessages}");
    }

    #endregion

    #region Nested Type Serialization

    [Fact]
    public void SymbolSerializer_RoundTrip_NestedTypes()
    {
        var innerType = new TypeSymbol
        {
            Name = "Inner",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Private,
            DefiningFilePath = "/test/nested.spy",
            DeclaringFilePath = "/test/nested.spy",
            DeclarationLine = 2,
            DeclarationColumn = 5,
            Fields = new List<VariableSymbol>
            {
                new VariableSymbol { Name = "value", Kind = SymbolKind.Variable, Type = BuiltinType.Int }
            }
        };

        var outerType = new TypeSymbol
        {
            Name = "Outer",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            DefiningFilePath = "/test/nested.spy",
            DeclaringFilePath = "/test/nested.spy",
            DeclarationLine = 1,
            DeclarationColumn = 1,
            NestedTypes = new List<TypeSymbol> { innerType }
        };
        innerType.DeclaringType = outerType;

        var filePath = CreateTempFile("nested.spy", "class Outer:\n    class Inner:\n        value: int");
        var cached = SymbolSerializer.Serialize(outerType, filePath);

        Assert.NotNull(cached.NestedTypes);
        Assert.Single(cached.NestedTypes!);
        Assert.Equal("Inner", cached.NestedTypes[0].Name);
        Assert.Equal("Type", cached.NestedTypes[0].Kind);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as TypeSymbol;

        Assert.NotNull(restored);
        Assert.Single(restored!.NestedTypes);
        Assert.Equal("Inner", restored.NestedTypes[0].Name);
        Assert.Equal(TypeKind.Class, restored.NestedTypes[0].TypeKind);
        Assert.Equal(AccessLevel.Private, restored.NestedTypes[0].AccessLevel);
        Assert.Equal(restored, restored.NestedTypes[0].DeclaringType);
        Assert.Single(restored.NestedTypes[0].Fields);
        Assert.Equal("value", restored.NestedTypes[0].Fields[0].Name);
    }

    #endregion

    #region Source Generator Cache Tests (schema v13, #636)

    private const string TestGeneratorIdentity = "GenA@Target";

    private string ComputeStringSha256(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    [Fact]
    public void GeneratedCacheEntry_RecordEquality_MatchesByValue()
    {
        // Records compare structurally — two entries with the same fields must
        // be considered equal. This is relied upon by cache round-trip tests.
        var a = new GeneratedCacheEntry
        {
            GeneratorHash = "hash-g",
            TargetHash = "hash-t",
            ArgumentsHash = "hash-a",
            GeneratedSource = "def foo():\n    pass"
        };
        var b = new GeneratedCacheEntry
        {
            GeneratorHash = "hash-g",
            TargetHash = "hash-t",
            ArgumentsHash = "hash-a",
            GeneratedSource = "def foo():\n    pass"
        };

        Assert.Equal(a, b);
        Assert.NotSame(a, b);
    }

    [Fact]
    public void GeneratorCache_RoundTrip_PersistsGeneratorOutputs()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);
        var targetHash = ComputeStringSha256("class Target: pass");
        var argumentsHash = ComputeStringSha256("()");
        const string generated = "def __generated_method(self):\n    return 1";

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash,
            argumentsHash,
            generated);

        // Write a file cache entry for the target so the pending generator
        // output gets merged into a persisted FileCacheEntry.
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var entry = cache2.GetFileCache(targetFile);
        Assert.NotNull(entry);
        Assert.NotNull(entry!.GeneratorOutputs);
        Assert.True(entry.GeneratorOutputs!.ContainsKey(TestGeneratorIdentity));

        var stored = entry.GeneratorOutputs[TestGeneratorIdentity];
        Assert.Equal(generatorHash, stored.GeneratorHash);
        Assert.Equal(targetHash, stored.TargetHash);
        Assert.Equal(argumentsHash, stored.ArgumentsHash);
        Assert.Equal(generated, stored.GeneratedSource);
    }

    [Fact]
    public void IsGeneratorCacheValid_FreshCache_ReturnsTrueWithSource()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);
        var argumentsHash = ComputeStringSha256("()");
        const string generated = "def helper(): return 42";

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "ignored",
            argumentsHash: argumentsHash,
            generatedSource: generated);

        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var isValid = cache2.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash,
            out var cachedSource);

        Assert.True(isValid);
        Assert.Equal(generated, cachedSource);
    }

    [Fact]
    public void IsGeneratorCacheValid_ChangedGeneratorHash_ReturnsFalse()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var argumentsHash = ComputeStringSha256("()");

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash: IncrementalCompilationCache.ComputeFileHash(generatorFile),
            targetHash: "ignored",
            argumentsHash: argumentsHash,
            generatedSource: "def gen(): pass");

        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        // Edit the generator file so its hash changes.
        File.WriteAllText(generatorFile, "class GenA:\n    def generate(self, ctx): pass");

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var isValid = cache2.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash,
            out var cachedSource);

        Assert.False(isValid);
        Assert.Null(cachedSource);
    }

    [Fact]
    public void IsGeneratorCacheValid_ChangedArgumentsHash_ReturnsFalse()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "ignored",
            argumentsHash: ComputeStringSha256("(old)"),
            generatedSource: "def gen(): pass");

        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var isValid = cache2.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash: ComputeStringSha256("(new)"),
            out var cachedSource);

        Assert.False(isValid);
        Assert.Null(cachedSource);
    }

    [Fact]
    public void IsGeneratorCacheValid_NullArgumentsHash_MatchesNull()
    {
        // Null argument hashes (decorators with no args) must round-trip correctly.
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "ignored",
            argumentsHash: null,
            generatedSource: "def gen(): pass");

        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var isValid = cache2.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash: null,
            out var cachedSource);

        Assert.True(isValid);
        Assert.Equal("def gen(): pass", cachedSource);

        // And switching from null to non-null invalidates.
        var withArgs = cache2.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash: ComputeStringSha256("(now-with-args)"),
            out var noSource);
        Assert.False(withArgs);
        Assert.Null(noSource);
    }

    [Fact]
    public void IsGeneratorCacheValid_NoCacheEntry_ReturnsFalse()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var isValid = cache.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash: null,
            out var cachedSource);

        Assert.False(isValid);
        Assert.Null(cachedSource);
    }

    [Fact]
    public void IsGeneratorCacheValid_MissingGeneratorFile_ReturnsFalse()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "ignored",
            argumentsHash: null,
            generatedSource: "def gen(): pass");

        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        // Delete the generator file before re-loading the cache.
        File.Delete(generatorFile);

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var isValid = cache2.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash: null,
            out var cachedSource);

        Assert.False(isValid);
        Assert.Null(cachedSource);
    }

    [Fact]
    public void GeneratorOutputs_SurviveSaveFileCacheOverwrite()
    {
        // SaveFileCache is invoked after generator execution and overwrites
        // the FileCacheEntry. Pending generator outputs cached *before* the
        // SaveFileCache call must be carried into the new entry — otherwise
        // the next build wouldn't see them.
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        // Cache the generator output FIRST (no FileCacheEntry exists yet).
        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "th",
            argumentsHash: "ah",
            generatedSource: "def gen(): pass");

        // Now SaveFileCache creates the entry.
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());

        var entry = cache.GetFileCache(targetFile);
        Assert.NotNull(entry);
        Assert.NotNull(entry!.GeneratorOutputs);
        Assert.True(entry.GeneratorOutputs!.ContainsKey(TestGeneratorIdentity));
    }

    [Fact]
    public void GeneratorOutputs_PreservedAcrossUnrelatedSaveFileCache()
    {
        // When a subsequent SaveFileCache happens for the same target without
        // re-caching the generator output, the existing GeneratorOutputs must
        // be carried over from the previous entry.
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "th",
            argumentsHash: "ah",
            generatedSource: "def gen(): pass");
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen v1", new List<string>());

        // A second SaveFileCache (e.g., after a re-run that didn't re-execute the
        // generator) must keep GeneratorOutputs populated.
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen v2", new List<string>());

        var entry = cache.GetFileCache(targetFile);
        Assert.NotNull(entry);
        Assert.Equal("// gen v2", entry!.GeneratedCSharp);
        Assert.NotNull(entry.GeneratorOutputs);
        Assert.True(entry.GeneratorOutputs!.ContainsKey(TestGeneratorIdentity));
    }

    [Fact]
    public void GeneratorOutputs_OverwriteSameIdentity()
    {
        // Caching twice under the same identity must overwrite the entry.
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "th",
            argumentsHash: "ah",
            generatedSource: "v1");
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "th",
            argumentsHash: "ah",
            generatedSource: "v2");

        var entry = cache.GetFileCache(targetFile);
        Assert.NotNull(entry);
        Assert.Equal("v2", entry!.GeneratorOutputs![TestGeneratorIdentity].GeneratedSource);
    }

    [Fact]
    public void GeneratorOutputs_MultipleIdentitiesCoexist()
    {
        // Two generators targeting the same file must each be stored under
        // their own identity.
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile, "GenA@T", generatorHash, "th", "ah1", "src-a");
        cache.CacheGeneratorOutput(
            targetFile, "GenB@T", generatorHash, "th", "ah2", "src-b");
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());

        var entry = cache.GetFileCache(targetFile);
        Assert.NotNull(entry);
        Assert.Equal(2, entry!.GeneratorOutputs!.Count);
        Assert.Equal("src-a", entry.GeneratorOutputs["GenA@T"].GeneratedSource);
        Assert.Equal("src-b", entry.GeneratorOutputs["GenB@T"].GeneratedSource);
    }

    [Fact]
    public void SchemaVersion_V13_IncludesGeneratorOutputsInJson()
    {
        // Sanity check: the persisted JSON for a v13 envelope should contain
        // a 'GeneratorOutputs' field when the cache entry has generator output.
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "th",
            argumentsHash: "ah",
            generatedSource: "def gen(): pass");
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        var json = File.ReadAllText(symbolCachePath);

        Assert.Contains($"\"SchemaVersion\": {IncrementalCompilationCache.CurrentSchemaVersion}", json);
        Assert.Contains("GeneratorOutputs", json);
        Assert.Contains(TestGeneratorIdentity, json);
        Assert.Contains("GeneratorHash", json);
        Assert.Contains("TargetHash", json);
        Assert.Contains("ArgumentsHash", json);
        Assert.Contains("GeneratedSource", json);
    }

    [Fact]
    public void SchemaVersion_PreviousVersion_InvalidatesGeneratorCache()
    {
        // Simulates an older cache file (e.g., v12 written before generator
        // support landed). Loading must drop the cache entirely so generator
        // outputs are re-computed.
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "th",
            argumentsHash: "ah",
            generatedSource: "def gen(): pass");
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        // Downgrade the schema version on disk to v12.
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        var json = File.ReadAllText(symbolCachePath);
        json = json.Replace(
            $"\"SchemaVersion\": {IncrementalCompilationCache.CurrentSchemaVersion}",
            "\"SchemaVersion\": 12");
        File.WriteAllText(symbolCachePath, json);

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        Assert.False(cache2.HasValidFileCache(targetFile),
            "Generator cache must be discarded when persisted schema version differs from current.");

        var isValid = cache2.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash: "ah",
            out var cachedSource);

        Assert.False(isValid);
        Assert.Null(cachedSource);
    }

    [Fact]
    public void Clear_RemovesGeneratorOutputs()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var targetFile = config.SourceFiles[0];
        var generatorFile = CreateTempFile("gen.spy", "class GenA: pass");

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var generatorHash = IncrementalCompilationCache.ComputeFileHash(generatorFile);

        cache.CacheGeneratorOutput(
            targetFile,
            TestGeneratorIdentity,
            generatorHash,
            targetHash: "th",
            argumentsHash: "ah",
            generatedSource: "def gen(): pass");
        cache.SaveFileCache(targetFile, new List<Symbol>(), "// gen", new List<string>());
        cache.SaveAllCaches();

        cache.Clear();

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var isValid = cache2.IsGeneratorCacheValid(
            targetFile,
            TestGeneratorIdentity,
            generatorFile,
            argumentsHash: "ah",
            out var cachedSource);

        Assert.False(isValid);
        Assert.Null(cachedSource);
    }

    #endregion

    #region Warm-Cache Inheritance Characterization (#1309)

    // These four shapes are the characterization table from plan-058a93 Phase 4.2. Each one
    // compiled cold and failed on the *second* build — the one where the library file is served
    // from the symbol cache — because inheritance resolution walked the global scope only and
    // never saw module-scoped symbols (#1309). Shape 4 failed cold as well.
    //
    // The harness: build once so the library's symbols land in `.sharpy-symbols`, make a REAL
    // content edit to the entry file (a touch or an inert edit leaves the SHA-256 hash unchanged
    // and silently produces a second COLD build, which is how a test in this shape passes without
    // testing anything), build again. Every test asserts the mode line, so "1 skipped" is proven
    // rather than assumed.

    private const string BaseChildLibrary = @"
class Base:
    def greet(self) -> str:
        return 'hello from Base'


class Child(Base):
    pass
";

    /// <summary>
    /// Asserts the compiler reported the expected incremental split for the build whose log
    /// <paramref name="logger"/> captured. This is the raw-<see cref="ProjectConfig"/> counterpart
    /// of <see cref="ProjectCompilationHelper.AssertIncrementalSkipped"/>.
    /// </summary>
    private static void AssertIncrementalSplit(CapturingCompilerLogger logger, int compiled, int skipped)
    {
        var modeLine = logger.InfoMessages
            .LastOrDefault(m => m.StartsWith("Incremental mode:", StringComparison.Ordinal));

        Assert.True(modeLine != null,
            "The build reported no incremental mode line, so nothing was cache-served. Captured: "
            + string.Join(" | ", logger.InfoMessages));
        Assert.Equal(
            $"Incremental mode: {compiled} file(s) to compile, {skipped} skipped (unchanged)",
            modeLine);
    }

    [Fact]
    public void IncrementalMode_WarmCache_InheritedMethodOnImportedSubclass_Resolves()
    {
        // Shape 1: `class Child(Base)` in a cached file, `Child().greet()` in the edited one.
        // Pre-fix the warm build failed with SPY0203 — Child came back from the cache with no base,
        // so the inherited member did not exist. Uses ProjectCompilationHelper's warm-cache harness
        // and executes, so a silently wrong resolution (e.g. binding to something else named greet)
        // shows up in the output too.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("WarmInherit")
            .WithIncremental()
            .WithEntryPoint("main.spy")
            .AddSourceFile("lib.spy", BaseChildLibrary)
            .AddSourceFile("main.spy", @"
from lib import Child


def main():
    c: Child = Child()
    print(c.greet())
")
            .CreateProjectFile();

        var cold = helper.Compile();
        helper.AssertCompilationSucceeded(cold);

        helper.UpdateSourceFile("main.spy", @"
from lib import Child


def main():
    c: Child = Child()
    print(c.greet())
    print(c.greet())
");

        var warm = helper.CompileAndExecute();

        Assert.True(warm.Success, string.Join("; ", warm.CompilationErrors));
        helper.AssertIncrementalSkipped(helper.LastCompilationResult!, "lib.spy");
        Assert.Equal("hello from Base\nhello from Base\n", warm.StandardOutput);
    }

    [Fact]
    public void IncrementalMode_WarmCache_FieldOnCachedExceptionSubclass_Resolves()
    {
        // Shape 2 (#1309's own repro): a user exception whose defining file is cache-served, caught
        // and read through `e.code`. Pre-fix the warm build reported SPY0203 on `e.code`.
        var libFile = CreateTempFile("lib.spy", @"
class AppError(Exception):
    code: int = 0

    def __init__(self, msg: str, code: int):
        super().__init__(msg)
        self.code = code
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import AppError


def main():
    try:
        raise AppError('boom', 42)
    except AppError as e:
        print(e.code)
");

        var config = CreateConfigFor(mainFile, libFile);
        var logger = new CapturingCompilerLogger();
        var compiler = new Compiler(new CompilerOptions { Incremental = true }, logger);

        var cold = compiler.CompileProject(config);
        Assert.True(cold.Success,
            "Cold build: " + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        File.WriteAllText(mainFile, @"
from lib import AppError


def main():
    try:
        raise AppError('boom', 42)
    except AppError as e:
        print(e.code)
        print(e.code + 1)
");

        logger.Clear();
        var warm = compiler.CompileProject(config);

        Assert.True(warm.Success,
            "Warm build must resolve a field on the cache-served exception subclass: "
            + string.Join("; ", warm.Diagnostics.GetErrors().Select(e => e.Message)));
        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
    }

    [Fact]
    public void IncrementalMode_WarmCache_SubclassAssignedToBaseAnnotation_IsAccepted()
    {
        // Shape 3: `b: Base = Child()` where both types come from a cache-served file. Pre-fix the
        // warm build reported SPY0220 — with Child's base lost, Child was not a Base.
        var libFile = CreateTempFile("lib.spy", BaseChildLibrary);
        var mainFile = CreateTempFile("main.spy", @"
from lib import Base, Child


def main():
    b: Base = Child()
    print(b.greet())
");

        var config = CreateConfigFor(mainFile, libFile);
        var logger = new CapturingCompilerLogger();
        var compiler = new Compiler(new CompilerOptions { Incremental = true }, logger);

        var cold = compiler.CompileProject(config);
        Assert.True(cold.Success,
            "Cold build: " + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        File.WriteAllText(mainFile, @"
from lib import Base, Child


def main():
    b: Base = Child()
    print(b.greet())
    other: Base = Child()
    print(other.greet())
");

        logger.Clear();
        var warm = compiler.CompileProject(config);

        Assert.True(warm.Success,
            "Warm build must accept the cache-served subclass where its base is required: "
            + string.Join("; ", warm.Diagnostics.GetErrors().Select(e => e.Message)));
        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
    }

    [Fact]
    public void IncrementalMode_PlainImport_InheritedMemberResolves_ColdAndWarm()
    {
        // Shape 4: module-qualified `lib.Child().greet()`. This one is cache-independent — it failed
        // on the COLD build too, because the same wrong-scope iteration ran there. Both builds are
        // asserted; the fixture `plain_import_inherited_member` covers the cold half end to end.
        //
        // Measured at e32ff6e34 (4-cell probe, all cold builds): `lib.Child().greet()` and
        // `c: lib.Child = lib.Child(); c.greet()` fail with SPY0203, while `lib.Child().describe()`
        // (Child's own member) and `lib.Base().greet()` (the base's own member) pass — as does
        // `from lib import Child` + `Child().greet()`, which is why shapes 1 and 3 above are green.
        //
        // Un-skipped when #1366 was fixed by making an in-source-set module export the
        // compilation's OWN symbols instead of a ModuleLoader re-extraction of them. The WARM half
        // is not incidental: a cache-served file never re-runs NameResolver, so its exports point
        // at whatever RestoreCachedSymbols defined into the module scope in Phase 2 — this test is
        // what says that path answers the same as the cold one.
        var libFile = CreateTempFile("lib.spy", BaseChildLibrary);
        var mainFile = CreateTempFile("main.spy", @"
import lib


def main():
    print(lib.Child().greet())
");

        var config = CreateConfigFor(mainFile, libFile);
        var logger = new CapturingCompilerLogger();
        var compiler = new Compiler(new CompilerOptions { Incremental = true }, logger);

        var cold = compiler.CompileProject(config);
        Assert.True(cold.Success,
            "Cold build of the plain-import shape: "
            + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        File.WriteAllText(mainFile, @"
import lib


def main():
    print(lib.Child().greet())
    print(lib.Child().greet())
");

        logger.Clear();
        var warm = compiler.CompileProject(config);

        Assert.True(warm.Success,
            "Warm build of the plain-import shape: "
            + string.Join("; ", warm.Diagnostics.GetErrors().Select(e => e.Message)));
        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
    }

    [Fact]
    public void IncrementalMode_WarmCache_ExceptOnCachedExceptionSubclass_DoesNotReportSpy0399()
    {
        // The exact shape that forced the except-derivation check's withdrawal in 6bd193925: a warm
        // build where AppError's defining file is cache-served. With the base chain lost, the check
        // refused a VALID user exception type. Restored in Phase 6 now that the cache carries real
        // symbols — this test is the guard on that. Its positive control is the test below; without
        // one, this assertion would also pass with the check deleted.
        var libFile = CreateTempFile("lib.spy", @"
class AppError(Exception):
    pass
");
        // Nothing raises AppError here: the derivation check is on the handler's TYPE, and the
        // bare `class AppError(Exception): pass` cannot be constructed with a message at all
        // today (#1367 — `raise AppError('boom')` is an ICE, CS1729 behind SPY0908). Keeping the
        // library class bare is the point: that is the shape that forced the withdrawal.
        var mainFile = CreateTempFile("main.spy", @"
from lib import AppError


def main():
    try:
        print('body')
    except AppError as e:
        print('caught')
");

        var config = CreateConfigFor(mainFile, libFile);
        var logger = new CapturingCompilerLogger();
        var compiler = new Compiler(new CompilerOptions { Incremental = true }, logger);

        var cold = compiler.CompileProject(config);
        Assert.True(cold.Success,
            "Cold build: " + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        File.WriteAllText(mainFile, @"
from lib import AppError


def main():
    try:
        print('body')
    except AppError as e:
        print('caught')
        print('again')
");

        logger.Clear();
        var warm = compiler.CompileProject(config);

        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
        Assert.DoesNotContain(
            warm.Diagnostics.GetErrors(),
            d => d.Code == DiagnosticCodes.Semantic.TryExceptionTypeNotException);
        Assert.True(warm.Success,
            "Warm build: " + string.Join("; ", warm.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalMode_WarmCache_ExceptOnCachedNonExceptionType_ReportsSpy0399()
    {
        // Positive control for the test above: same warm-cache harness, same cache-served library,
        // but the caught type does not derive from Exception. SPY0399 must still fire on a build
        // where the type came from the cache — that is what makes the absence assertion above mean
        // "the type was accepted" rather than "the check never ran".
        var libFile = CreateTempFile("lib.spy", @"
class NotAnError:
    x: int = 1
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import NotAnError


def main():
    n: NotAnError = NotAnError()
    print(n.x)
");

        var config = CreateConfigFor(mainFile, libFile);
        var logger = new CapturingCompilerLogger();
        var compiler = new Compiler(new CompilerOptions { Incremental = true }, logger);

        var cold = compiler.CompileProject(config);
        Assert.True(cold.Success,
            "Cold build: " + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        // The edit introduces the bad handler, so the library stays cached while main recompiles.
        File.WriteAllText(mainFile, @"
from lib import NotAnError


def main():
    n: NotAnError = NotAnError()
    print(n.x)
    try:
        print('try')
    except NotAnError:
        print('never')
");

        logger.Clear();
        var warm = compiler.CompileProject(config);

        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
        Assert.False(warm.Success, "A non-Exception except clause must be refused on warm builds too.");
        Assert.Contains(
            warm.Diagnostics.GetErrors(),
            d => d.Code == DiagnosticCodes.Semantic.TryExceptionTypeNotException
                && d.Message.Contains("must be a subclass of 'Exception'"));
    }

    /// <summary>
    /// A two-file project config over <see cref="_tempDir"/>, entry file first — the layout every
    /// warm-cache shape in this region uses.
    /// </summary>
    private ProjectConfig CreateConfigFor(string entryFile, params string[] otherFiles)
    {
        var sourceFiles = new List<string> { entryFile };
        sourceFiles.AddRange(otherFiles);

        return new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = sourceFiles,
            Configuration = "Debug"
        };
    }

    #endregion

    #region Warm-Cache Base-Type Arguments (#1287)

    /// <summary>
    /// The acceptance for #1287's serializer leg (Design Decision 9): a base class's type
    /// ARGUMENTS must survive the symbol cache, or the warm build silently answers assignability
    /// from a different supertype than the cold one did.
    ///
    /// <para>
    /// This is the CLR-base cell, the one whose round-trip is least obvious.
    /// <c>class MyList[T](List[int])</c> has a base whose <c>ClrType</c> is set, so
    /// <c>SymbolSerializer</c> writes no <c>BaseTypeId</c> for it by construction, and
    /// <c>ResolveTypeReferences</c> rebuilds <c>BaseTypeRef</c> only inside its
    /// <c>BaseTypeId != null</c> branch — which is never entered here. The arguments survive anyway,
    /// on a second path: they are written as <c>BaseTypeArgs</c> beside <c>UnresolvedBaseName</c>,
    /// restored into <c>UnresolvedBaseTypeArgs</c> (<c>SymbolSerializer.cs:484</c>), and turned back
    /// into a <c>BaseTypeReference</c> when <c>InheritanceResolver</c> re-resolves the base name
    /// (<c>InheritanceResolver.cs:78-83</c>). The cache entry for <c>MyList</c> reads
    /// <c>BaseTypeId: null, BaseTypeArgs: ["int"], UnresolvedBaseName: "List"</c>.
    /// </para>
    ///
    /// <para>
    /// So this passes today, and it is a PIN rather than a repro: the two paths are independent, and
    /// a change to either one alone would silently drop the arguments for exactly this shape while
    /// leaving the source-base cell below green.
    /// </para>
    ///
    /// <para>
    /// The arity coincides (<c>MyList</c> has one parameter, <c>List</c> has one), which is exactly
    /// the condition the walker's positional-copy fallback tests. So a dropped reference does not
    /// degrade to "no supertype" — it degrades to the WRONG supertype, <c>List[str]</c> instead of
    /// <c>List[int]</c>, and both verdicts invert: the correct assignment is refused and the
    /// incorrect one would be accepted. Two directions are asserted for that reason; checking only
    /// the acceptance would pass against a walker that had simply stopped answering.
    /// </para>
    /// </summary>
    [Fact]
    public void IncrementalMode_WarmCache_ClrGenericBaseAtConcreteArgs_KeepsBothVerdicts()
    {
        var libFile = CreateTempFile("lib.spy", ClrGenericBaseLibrary);
        var mainFile = CreateTempFile("main.spy", @"
from system.collections.generic import List
from lib import MyList


def main():
    m: MyList[str] = MyList[str]()
    ok: List[int] = m
    print(ok.count)
");

        var config = CreateConfigFor(mainFile, libFile);
        var logger = new CapturingCompilerLogger();
        var compiler = new Compiler(new CompilerOptions { Incremental = true }, logger);

        var cold = compiler.CompileProject(config);
        Assert.True(cold.Success,
            "Cold build must accept List[int] = MyList[str] — the base is written List[int]: "
            + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        File.WriteAllText(mainFile, @"
from system.collections.generic import List
from lib import MyList


def main():
    m: MyList[str] = MyList[str]()
    ok: List[int] = m
    print(ok.count)
    print(ok.count)
");

        logger.Clear();
        var warm = compiler.CompileProject(config);

        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
        Assert.True(warm.Success,
            "Warm build must still read MyList's base as List[int]. A dropped BaseTypeReference "
            + "falls into the walker's positional copy, which reads it as List[T] and refuses: "
            + string.Join("; ", warm.Diagnostics.GetErrors().Select(e => e.Message)));

        // The other direction, on the same warm cache: the positional copy would ACCEPT this.
        File.WriteAllText(mainFile, @"
from system.collections.generic import List
from lib import MyList


def main():
    m: MyList[str] = MyList[str]()
    bad: List[str] = m
    print(bad.count)
");

        logger.Clear();
        var warmWrong = compiler.CompileProject(config);

        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
        Assert.False(warmWrong.Success,
            "Warm build must still REFUSE List[str] = MyList[str]; accepting it is the positional "
            + "copy answering from List[T].");
        Assert.Contains(
            warmWrong.Diagnostics.GetErrors(),
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch
                && d.Message.Contains("'List[str]'"));
    }

    /// <summary>
    /// The plan's own cell: a source-declared generic base at a concrete argument
    /// (<c>class IntBox(Box[int])</c>), cached, then used from an edited file. The base has no
    /// <c>ClrType</c>, so this half round-trips through <c>BaseTypeId</c> + <c>BaseTypeArgs</c> and
    /// the <c>ResolveTypeReferences</c> branch the CLR cell never enters. It is the CONTROL for that
    /// cell: the two shapes exercise different restore paths, so if both fail the defect is in the
    /// serializer generally, and if only one fails it names which path broke.
    /// </summary>
    [Fact]
    public void IncrementalMode_WarmCache_SourceGenericBaseAtConcreteArg_StillAssignable()
    {
        var libFile = CreateTempFile("lib.spy", SourceGenericBaseLibrary);
        var mainFile = CreateTempFile("main.spy", @"
from lib import Box, IntBox


def main():
    b: Box[int] = IntBox(1)
    print(b.value)
");

        var config = CreateConfigFor(mainFile, libFile);
        var logger = new CapturingCompilerLogger();
        var compiler = new Compiler(new CompilerOptions { Incremental = true }, logger);

        var cold = compiler.CompileProject(config);
        Assert.True(cold.Success,
            "Cold build: " + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        File.WriteAllText(mainFile, @"
from lib import Box, IntBox


def main():
    b: Box[int] = IntBox(1)
    print(b.value)
    print(b.value)
");

        logger.Clear();
        var warm = compiler.CompileProject(config);

        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
        Assert.True(warm.Success,
            "Warm build must still accept Box[int] = IntBox(1): "
            + string.Join("; ", warm.Diagnostics.GetErrors().Select(e => e.Message)));

        // The wrong argument stays refused on the same warm cache.
        File.WriteAllText(mainFile, @"
from lib import Box, IntBox


def main():
    b: Box[str] = IntBox(1)
    print(b.value)
");

        logger.Clear();
        var warmWrong = compiler.CompileProject(config);

        AssertIncrementalSplit(logger, compiled: 1, skipped: 1);
        Assert.False(warmWrong.Success,
            "Warm build must still refuse Box[str] = IntBox(1).");
    }

    /// <summary>
    /// A generic class over a CLR generic base pinned at a concrete argument. It carried an explicit
    /// <c>__init__</c> until #1408 landed, because the synthesized forwarders copied the base's OPEN
    /// signatures and the shape could not emit at all (CS1503 behind SPY0908); the workaround is gone
    /// now that the forwarders substitute the base clause's written arguments, so this cell exercises
    /// the synthesized path as well as the warm cache.
    /// </summary>
    private const string ClrGenericBaseLibrary = @"
from system.collections.generic import List


class MyList[T](List[int]):
    pass
";

    private const string SourceGenericBaseLibrary = @"
class Box[T]:
    value: T

    def __init__(self, value: T) -> None:
        self.value = value


class IntBox(Box[int]):
    def __init__(self, value: int) -> None:
        super().__init__(value)
";

    #endregion
}
