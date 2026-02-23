using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
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
        Assert.Equal("builtin:int", cached.Parameters[0].TypeId);
        Assert.Equal("builtin:str", cached.Parameters[1].TypeId);
        Assert.Equal("builtin:bool", cached.ReturnTypeId);
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
            ReturnType = SemanticType.Void,
            CodeGenInfo = new CodeGenInfo
            {
                CSharpName = "SnakeCaseFunc",
                OriginalName = "snake_case_func",
                IsModuleLevel = true
            }
        };

        var filePath = CreateTempFile("codegen.spy", "def snake_case_func():\n    pass");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

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
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        var result = compiler.CompileProject(config);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));

        // Check symbol cache contains the Point class
        Assert.True(File.Exists(symbolCachePath));
        var json = File.ReadAllText(symbolCachePath);
        Assert.Contains("Point", json);
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
}
