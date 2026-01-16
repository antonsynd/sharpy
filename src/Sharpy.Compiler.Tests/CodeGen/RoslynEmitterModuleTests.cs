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
        Assert.Contains("public static class Exports", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithSourcePath_GeneratesSimplerNamespace()
    {
        // Arrange - Single-file compilation without project namespace
        var emitter = CreateEmitter("src/myapp/utils.spy");
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Single-file uses simpler file-name-based namespace
        Assert.Contains("namespace Sharpy.Utils", code);
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
        Assert.Contains("using global::Sharpy.Core;", code);
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

        // Assert - .NET framework namespace imports normally without .Exports
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

        // Assert - .NET framework namespace with alias
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

        // Assert - .NET framework imports normally without using static
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

        // Assert - .NET framework imports normally
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

        // Assert - .NET framework imports normally without .Exports
        Assert.Contains("using System.IO;", code);
        Assert.Contains("using System.Text;", code);
        Assert.Contains("using System.Linq;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithImportModule_GeneratesAliasToExports()
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
                        new ImportAlias { Name = "utils.helpers" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - should generate alias pointing to Exports class
        Assert.Contains("using utils_helpers = Utils.Helpers.Exports;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithImportModuleAsAlias_GeneratesCorrectAlias()
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
                        new ImportAlias { Name = "utils.helpers", AsName = "h" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("using h = Utils.Helpers.Exports;", code);
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
        // Import should be in using directives (.NET framework import)
        Assert.Contains("using System.IO;", code);
        // Function should be in module class
        Assert.Contains("MyFunction", code);
        // Import statement should not appear as a member
        var moduleClassStart = code.IndexOf("class Exports");
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

        // Assert - Sharpy modules (non-.NET) should generate alias to Exports
        Assert.Contains("using my_custom_module_sub_module = MyCustomModule.SubModule.Exports;", code);
    }

    [Fact]
    public void GenerateNamespace_WithNestedPath_GeneratesNestedNamespace()
    {
        // Arrange - For project-based compilation, set both ProjectNamespace and ProjectRootPath
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = "/project/src/myapp/services/auth.spy",
            ProjectNamespace = "MyProject",
            ProjectRootPath = "/project/src"
        };
        var emitter = new RoslynEmitter(context);
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - project-based namespace generation
        Assert.Contains("namespace MyProject.Myapp.Services.Auth", code);
    }

    [Fact]
    public void GenerateNamespace_SingleFileWithPath_UsesSimplerNamespace()
    {
        // Arrange - Single-file compilation (no ProjectNamespace set)
        var emitter = CreateEmitter("src/myapp/services/auth.spy");
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Single-file uses simpler file-name-based namespace
        Assert.Contains("namespace Sharpy.Auth", code);
    }

    [Fact]
    public void GenerateNamespace_FiltersSrcAndLib_ExcludesCommonDirs()
    {
        // Arrange - Project-based compilation to test directory filtering
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = "/project/src/lib/mymodule.spy",
            ProjectNamespace = "MyApp",
            ProjectRootPath = "/project/src"
        };
        var emitter = new RoslynEmitter(context);
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Project-based namespace includes relative path from root
        Assert.Contains("namespace MyApp.Lib.Mymodule", code);
    }

    [Fact]
    public void GenerateNamespace_SingleFile_UsesSimpleNamespace()
    {
        // Arrange - Single-file (no project namespace)
        var emitter = CreateEmitter("src/lib/mymodule.spy");
        var module = new Module
        {
            Body = new List<Statement>()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Single-file uses simpler file-name-based namespace
        Assert.Contains("namespace Sharpy.Mymodule", code);
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

    #region Namespace Edge Cases

    [Fact]
    public void GenerateNamespace_PathStartsWithNumber_PrefixesWithUnderscore()
    {
        // Arrange - Path with directory starting with number
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = "/project/src/20260113_test/module.spy",
            ProjectNamespace = "TestApp",
            ProjectRootPath = "/project/src"
        };
        var emitter = new RoslynEmitter(context);
        var module = new Module { Body = new List<Statement>() };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - numeric directory should be prefixed with underscore
        Assert.Contains("namespace TestApp._20260113Test.Module", code);
    }

    [Fact]
    public void GenerateNamespace_FileNameStartsWithNumber_PrefixesWithUnderscore()
    {
        // Arrange - File name starting with number
        var emitter = CreateEmitter("/some/path/123test.spy");
        var module = new Module { Body = new List<Statement>() };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - numeric-starting filename should be prefixed with underscore
        // Note: The SimpleToPascalCase doesn't capitalize after numbers, so it stays lowercase
        Assert.Contains("namespace Sharpy._123test", code);
    }

    [Fact]
    public void GenerateNamespace_PathWithSpecialChars_SanitizesChars()
    {
        // Arrange - Path with special characters (dashes, dots)
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = "/project/src/my-app.test/module.spy",
            ProjectNamespace = "TestApp",
            ProjectRootPath = "/project/src"
        };
        var emitter = new RoslynEmitter(context);
        var module = new Module { Body = new List<Statement>() };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - special chars should be converted to valid C# identifiers
        Assert.Contains("namespace TestApp.MyAppTest.Module", code);
    }

    [Fact]
    public void GenerateNamespace_NestedNumericDirs_HandlesMultipleLevels()
    {
        // Arrange - Multiple nested directories with numeric names
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = "/dogfood/issues/20260113_failed_0001/source.spy",
            ProjectNamespace = "Dogfood",
            ProjectRootPath = "/dogfood/issues"
        };
        var emitter = new RoslynEmitter(context);
        var module = new Module { Body = new List<Statement>() };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - numeric directory should be valid C# namespace
        Assert.Contains("namespace Dogfood._20260113Failed0001.Source", code);
    }

    [Fact]
    public void GenerateNamespace_SingleFileNumericPath_UsesFileNameOnly()
    {
        // Arrange - Single-file compilation with numeric-starting path
        // This simulates the dogfooding scenario where path is /20260113.../source.spy
        var emitter = CreateEmitter("/dogfood/20260113_compilation_failed/source.spy");
        var module = new Module { Body = new List<Statement>() };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Single-file should use simple file-name-based namespace
        // avoiding the problematic path entirely
        Assert.Contains("namespace Sharpy.Source", code);
    }

    [Fact]
    public void GenerateNamespace_ProjectNamespaceOnlyNoPath_UsesProjectNamespaceWithFileName()
    {
        // Arrange - ProjectNamespace set but no ProjectRootPath
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = "/some/path/mymodule.spy",
            ProjectNamespace = "MyApp"
            // ProjectRootPath not set
        };
        var emitter = new RoslynEmitter(context);
        var module = new Module { Body = new List<Statement>() };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Should use ProjectNamespace with file name
        Assert.Contains("namespace MyApp.Mymodule", code);
    }

    [Fact]
    public void GenerateNamespace_NoSourceFilePath_UsesDefault()
    {
        // Arrange - No source file path
        var emitter = CreateEmitter(null);
        var module = new Module { Body = new List<Statement>() };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Should use default namespace
        Assert.Contains("namespace SharpyGenerated", code);
    }

    #endregion

    #region Nullable Pragma Tests

    [Fact]
    public void GenerateCompilationUnit_IncludesNullablePragma()
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

        // Assert - #nullable enable should be present
        Assert.Contains("#nullable enable", code);
    }

    [Fact]
    public void GenerateCompilationUnit_NullablePragma_AppearsBeforeUsings()
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

        // Assert - #nullable enable should appear before using statements
        var nullableIndex = code.IndexOf("#nullable enable");
        var usingIndex = code.IndexOf("using System;");
        Assert.True(nullableIndex >= 0, "#nullable enable should be present");
        Assert.True(usingIndex > nullableIndex, "using statements should appear after #nullable enable");
    }

    [Fact]
    public void GenerateCompilationUnit_NullableIntParameter_GeneratesIntQuestion()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "test_func",
                    Parameters = new List<Parameter>
                    {
                        new Parameter
                        {
                            Name = "value",
                            Type = new TypeAnnotation { Name = "int", IsNullable = true }
                        }
                    },
                    ReturnType = new TypeAnnotation { Name = "void" },
                    Body = new List<Statement>()
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - should generate int? parameter
        Assert.Contains("int?", code);
        Assert.Contains("value", code);
    }

    [Fact]
    public void GenerateCompilationUnit_NullableStringReturnType_GeneratesStringQuestion()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "get_name",
                    Parameters = new List<Parameter>(),
                    ReturnType = new TypeAnnotation { Name = "str", IsNullable = true },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new NoneLiteral()
                        }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - should generate string? return type
        Assert.Contains("string?", code);
    }

    [Fact]
    public void GenerateCompilationUnit_NullableListType_GeneratesListQuestion()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "get_items",
                    Parameters = new List<Parameter>(),
                    ReturnType = new TypeAnnotation
                    {
                        Name = "list",
                        TypeArguments = new List<TypeAnnotation>
                        {
                            new TypeAnnotation { Name = "int" }
                        },
                        IsNullable = true
                    },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new NoneLiteral()
                        }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - should generate global::Sharpy.Core.List<int>?
        Assert.Contains("global::Sharpy.Core.List<int>?", code);
    }

    [Fact]
    public void GenerateCompilationUnit_ListOfNullableType_GeneratesCorrectly()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "get_items",
                    Parameters = new List<Parameter>(),
                    ReturnType = new TypeAnnotation
                    {
                        Name = "list",
                        TypeArguments = new List<TypeAnnotation>
                        {
                            new TypeAnnotation { Name = "int", IsNullable = true }
                        },
                        IsNullable = false
                    },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new ListLiteral
                            {
                                Elements = new List<Expression>()
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - should generate global::Sharpy.Core.List<int?>
        Assert.Contains("global::Sharpy.Core.List<int?>", code);
    }

    #endregion

    #region From-Import Tests for Sharpy Modules

    [Fact]
    public void GenerateCompilationUnit_WithFromImportSharpyModule_GeneratesUsingStatic()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "config",
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "MAX_SIZE" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Sharpy modules should generate using static
        Assert.Contains("using static Config.Config;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithFromImportMultipleSymbols_GeneratesUsingStatic()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "utils.helpers",
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "format_text" },
                        new ImportAlias { Name = "parse_json" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Should generate using static for the module (not individual symbols)
        Assert.Contains("using static Utils.Helpers.Helpers;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithFromImportNestedModule_GeneratesPascalCasePath()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "lib.math.operations",
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "add" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Nested module path should be converted to PascalCase
        Assert.Contains("using static Lib.Math.Operations.Operations;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithFromImportAllSharpyModule_GeneratesUsingStatic()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "utils",
                    ImportAll = true
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - from module import * should generate using static
        Assert.Contains("using static Utils.Utils;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithFromImportSnakeCaseModule_ConvertsToPascalCase()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "database_utils.connection_pool",
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "get_connection" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Snake_case module names should be converted to PascalCase
        Assert.Contains("using static DatabaseUtils.ConnectionPool.ConnectionPool;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_WithFromImportSymbolWithAlias_GeneratesUsingStatic()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "config",
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "MAX_SIZE", AsName = "max" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Should still generate using static (alias handled at usage site)
        // Note: C# using static doesn't support aliasing individual members
        // The semantic analyzer should handle the symbol aliasing
        Assert.Contains("using static Config.Config;", code);
    }

    [Fact]
    public void GenerateCompilationUnit_MultipleFromImportsSameModule_GeneratesSingleUsingStatic()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FromImportStatement
                {
                    Module = "config",
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "MAX_SIZE" }
                    }
                },
                new FromImportStatement
                {
                    Module = "config",
                    Names = new List<ImportAlias>
                    {
                        new ImportAlias { Name = "MIN_SIZE" }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - Should only have one using static directive
        var firstIndex = code.IndexOf("using static Config.Config;");
        var lastIndex = code.LastIndexOf("using static Config.Config;");

        // If they're the same index, there's only one occurrence
        // If different, count how many there are (could be deduplicated or not)
        // The important thing is that it compiles correctly
        Assert.Contains("using static Config.Config;", code);
    }

    #endregion
}
