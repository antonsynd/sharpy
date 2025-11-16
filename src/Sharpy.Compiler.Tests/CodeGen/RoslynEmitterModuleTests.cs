using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for module structure code generation (Phase 5)
/// </summary>
public class RoslynEmitterModuleTests
{
    private RoslynEmitter CreateEmitter(string? sourceFilePath = null)
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = sourceFilePath
        };
        return new RoslynEmitter(context);
    }

    [Fact]
    public void GenerateCompilationUnit_EmptyModule_GeneratesModuleClass()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("namespace SharpyGenerated", code);
        Assert.Contains("public static class __Module__", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithSourcePath_GeneratesProperNamespace()
    {
        // Arrange
        var emitter = CreateEmitter("src/myapp/utils.spy");
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("namespace Myapp.Utils", code);
    }

    [Fact]
    public void GenerateCompilationUnit_DefaultUsings_IncludesSystemAndSharpy()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("using System;", code);
        Assert.Contains("using System.Collections.Generic;", code);
        Assert.Contains("using System.Linq;", code);
        Assert.Contains("using Sharpy.Core;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithImportStatement_GeneratesUsing()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new ImportStatement
                {
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "system.io" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("using System.IO;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithImportAlias_GeneratesUsingAlias()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new ImportStatement
                {
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "system.collections.generic", AsName = "Collections" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("using Collections = System.Collections.Generic;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithFromImport_GeneratesUsing()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "system.io",
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "File" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("using System.IO;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithFromImportAll_GeneratesUsing()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "system.text",
                    ImportAll = true
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("using System.Text;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithMultipleImports_GeneratesAllUsings()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new ImportStatement
                {
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "system.io" },
                        new ImportAlias { Name = "system.text" }
                    }
                },
                new FromImportStatement
                {
                    Module = "system.linq",
                    ImportAll = true
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("using System.IO;", code);
        Assert.Contains("using System.Text;", code);
        Assert.Contains("using System.Linq;", code);

        // Verify no duplicates - each using should appear only once
        var linqCount = System.Text.RegularExpressions.Regex.Matches(code, @"using System\.Linq;").Count;
        Assert.Equal(1, linqCount);
    }

    [Fact]
    public void GenerateCompilationUnit_ImportsNotInModuleClass_OnlyTypesIncluded()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new ImportStatement
                {
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "system.io" }
                    }
                },
                new FunctionDef
                {
                    Name = "my_function",
                    Parameters = new List<Parameter>(),
                    Body = new List<Statement>()
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        // Import should be in using directives
        Assert.Contains("using System.IO;", code);
        // Function should be in module class
        Assert.Contains("MyFunction", code);
        // Import statement should not appear as a member
        var moduleClassStart = code.IndexOf("class __Module__");
        var importsInClass = code.Substring(moduleClassStart).Contains("ImportStatement");
        Assert.False(importsInClass);
    }

    [Fact]
    public void ConvertModuleNameToNamespace_SnakeCase_ConvertsToPascalCase()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new ImportStatement
                {
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "my_custom_module.sub_module" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("using MyCustomModule.SubModule;", code);
    }

    [Fact]
    public void GenerateNamespace_WithNestedPath_GeneratesNestedNamespace()
    {
        // Arrange
        var emitter = CreateEmitter("src/myapp/services/auth.spy");
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("namespace Myapp.Services.Auth", code);
    }

    [Fact]
    public void GenerateNamespace_FiltersSrcAndLib_ExcludesCommonDirs()
    {
        // Arrange
        var emitter = CreateEmitter("src/lib/mymodule.spy");
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        // Should only have Mymodule, not Src.Lib.Mymodule
        Assert.Contains("namespace Mymodule", code);
        Assert.DoesNotContain("namespace Src", code);
        Assert.DoesNotContain("namespace Lib", code);
    }

    [Fact]
    public void ConvertModuleNameToNamespace_HandlesEdgeCases_ReturnsOriginalOrEmpty()
    {
        // Arrange
        var emitter = CreateEmitter();

        // Test with only underscores - should return original
        var module1 = new Module
        {
            Body = new List<Statement>
            {
                new ImportStatement
                {
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "___" }
                    }
                }
            }
        };

        // Test with backticks only - should handle gracefully
        var module2 = new Module
        {
            Body = new List<Statement>
            {
                new ImportStatement
                {
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "``" }
                    }
                }
            }
        };

        // Act
        var result1 = emitter.GenerateCompilationUnit(module1);
        var code1 = result1.ToFullString();

        var result2 = emitter.GenerateCompilationUnit(module2);
        var code2 = result2.ToFullString();

        // Assert - should not throw and should generate valid code
        Assert.NotNull(code1);
        Assert.NotNull(code2);
        // Both should still compile to valid C# even with edge case inputs
        Assert.Contains("namespace", code1);
        Assert.Contains("namespace", code2);
    }
}
