using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for ImportResolver with .NET module support
/// </summary>
public class ImportResolverNetModuleTests
{
    [Fact]
    public void ImportResolver_WithModuleRegistry_ResolvesNetModules()
    {
        var logger = NullLogger.Instance;
        var registry = new ModuleRegistry(logger);
        
        // Load Sharpy.Core
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        registry.LoadReference(sharpyCoreAssembly);

        var resolver = new ImportResolver(logger, registry);

        // Create an import statement for builtins
        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "builtins", AsName = null }
            }
        };

        var result = resolver.ResolveImport(importStmt);

        Assert.Single(result);
        Assert.NotNull(result[0]);
        Assert.True(result[0].IsNetModule);
        Assert.NotEmpty(result[0].ExportedSymbols);
        Assert.Contains("print", result[0].ExportedSymbols.Keys);
        Assert.Contains("range", result[0].ExportedSymbols.Keys);
        Assert.Contains("len", result[0].ExportedSymbols.Keys);
    }

    [Fact]
    public void ImportResolver_WithSampleModule_ResolvesSuccessfully()
    {
        var sampleModulePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "samples", "SampleModule", "bin", "Debug", "net9.0", "SampleModule.dll");

        // Only run test if SampleModule exists
        if (!File.Exists(sampleModulePath))
        {
            return; // Skip test
        }

        var logger = NullLogger.Instance;
        var registry = new ModuleRegistry(logger);
        registry.LoadReference(sampleModulePath);

        var resolver = new ImportResolver(logger, registry);

        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "samplemodule", AsName = null }
            }
        };

        var result = resolver.ResolveImport(importStmt);

        Assert.Single(result);
        Assert.NotNull(result[0]);
        Assert.True(result[0].IsNetModule);
        Assert.NotEmpty(result[0].ExportedSymbols);
        Assert.Contains("square", result[0].ExportedSymbols.Keys);
        Assert.Contains("cube", result[0].ExportedSymbols.Keys);
        Assert.Contains("average", result[0].ExportedSymbols.Keys);
        Assert.Contains("is_prime", result[0].ExportedSymbols.Keys);
        Assert.Contains("factorial", result[0].ExportedSymbols.Keys);
    }

    [Fact]
    public void ImportResolver_WithNonExistentNetModule_ReturnsEmpty()
    {
        var logger = NullLogger.Instance;
        var registry = new ModuleRegistry(logger);
        var resolver = new ImportResolver(logger, registry);

        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "nonexistent", AsName = null }
            }
        };

        var result = resolver.ResolveImport(importStmt);

        Assert.Empty(result);
        Assert.NotEmpty(resolver.Errors);
        Assert.Contains(resolver.Errors, e => e.Message.Contains("nonexistent"));
    }

    [Fact]
    public void ImportResolver_FromImport_WithNetModule_ResolvesSuccessfully()
    {
        var logger = NullLogger.Instance;
        var registry = new ModuleRegistry(logger);
        
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        registry.LoadReference(sharpyCoreAssembly);

        var resolver = new ImportResolver(logger, registry);

        var fromImport = new FromImportStatement
        {
            Module = "builtins",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "print", AsName = null },
                new ImportAlias { Name = "range", AsName = null }
            },
            ImportAll = false
        };

        var result = resolver.ResolveFromImport(fromImport);

        Assert.NotNull(result);
        Assert.True(result.IsNetModule);
        Assert.Contains("print", result.ExportedSymbols.Keys);
        Assert.Contains("range", result.ExportedSymbols.Keys);
        Assert.Empty(resolver.Errors);
    }

    [Fact]
    public void ImportResolver_FromImport_WithInvalidSymbol_ReportsError()
    {
        var logger = NullLogger.Instance;
        var registry = new ModuleRegistry(logger);
        
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        registry.LoadReference(sharpyCoreAssembly);

        var resolver = new ImportResolver(logger, registry);

        var fromImport = new FromImportStatement
        {
            Module = "builtins",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "nonexistent_function", AsName = null }
            },
            ImportAll = false
        };

        var result = resolver.ResolveFromImport(fromImport);

        Assert.NotNull(result);
        Assert.NotEmpty(resolver.Errors);
        Assert.Contains(resolver.Errors, e => e.Message.Contains("nonexistent_function"));
    }

    [Fact]
    public void ImportResolver_FromImportAll_WithNetModule_ResolvesAllSymbols()
    {
        var logger = NullLogger.Instance;
        var registry = new ModuleRegistry(logger);
        
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        registry.LoadReference(sharpyCoreAssembly);

        var resolver = new ImportResolver(logger, registry);

        var fromImport = new FromImportStatement
        {
            Module = "builtins",
            Names = new List<ImportAlias>(),
            ImportAll = true
        };

        var result = resolver.ResolveFromImport(fromImport);

        Assert.NotNull(result);
        Assert.True(result.IsNetModule);
        Assert.NotEmpty(result.ExportedSymbols);
        // Should not validate specific symbols when importing all
        Assert.Empty(resolver.Errors);
    }

    [Fact]
    public void ImportResolver_CachesNetModules_OnMultipleImports()
    {
        var logger = NullLogger.Instance;
        var registry = new ModuleRegistry(logger);
        
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        registry.LoadReference(sharpyCoreAssembly);

        var resolver = new ImportResolver(logger, registry);

        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "builtins", AsName = null }
            }
        };

        // First import
        var result1 = resolver.ResolveImport(importStmt);
        Assert.Single(result1);

        // Second import (should use cache)
        var result2 = resolver.ResolveImport(importStmt);
        Assert.Single(result2);

        // Both should reference the same ModuleInfo (from cache)
        Assert.Same(result1[0], result2[0]);
    }

    [Fact]
    public void ImportResolver_WithoutModuleRegistry_SkipsNetModules()
    {
        var logger = NullLogger.Instance;
        var resolver = new ImportResolver(logger, null); // No ModuleRegistry

        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "builtins", AsName = null }
            }
        };

        var result = resolver.ResolveImport(importStmt);

        // Without ModuleRegistry, it should try .spy files and fail
        Assert.Empty(result);
        Assert.NotEmpty(resolver.Errors);
    }
}
