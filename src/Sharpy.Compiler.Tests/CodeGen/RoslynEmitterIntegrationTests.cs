using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Integration tests that verify generated C# code compiles successfully
/// </summary>
public class RoslynEmitterIntegrationTests
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

    private bool CompileCode(string code, out string errors)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        // Get path to System.Runtime
        var systemRuntimePath = Path.Combine(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Runtime.dll");

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Sharpy.Core.Exports).Assembly.Location),
                MetadataReference.CreateFromFile(systemRuntimePath),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics();
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        errors = string.Join("\n", errorDiagnostics.Select(d => d.ToString()));
        return errorDiagnostics.Count == 0;
    }

    [Fact]
    public void GeneratedCode_EmptyModule_CompilesSuccessfully()
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
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}");
    }

    [Fact]
    public void GeneratedCode_SimpleFunctionDeclaration_CompilesSuccessfully()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "hello_world",
                    Parameters = new List<Parameter>(),
                    ReturnType = new TypeAnnotation { Name = "void" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}");
    }

    [Fact]
    public void GeneratedCode_FunctionWithParameters_CompilesSuccessfully()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "add",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "x", Type = new TypeAnnotation { Name = "int" } },
                        new Parameter { Name = "y", Type = new TypeAnnotation { Name = "int" } }
                    },
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new BinaryOp
                            {
                                Left = new Identifier { Name = "x" },
                                Operator = BinaryOperator.Add,
                                Right = new Identifier { Name = "y" }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}");
    }

    [Fact]
    public void GeneratedCode_SimpleClassDeclaration_CompilesSuccessfully()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new ClassDef
                {
                    Name = "Person",
                    Body = new List<Statement>
                    {
                        new VariableDeclaration
                        {
                            Name = "name",
                            Type = new TypeAnnotation { Name = "str" }
                        },
                        new VariableDeclaration
                        {
                            Name = "age",
                            Type = new TypeAnnotation { Name = "int" }
                        }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}");
    }

    [Fact]
    public void GeneratedCode_EnumDeclaration_CompilesSuccessfully()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new EnumDef
                {
                    Name = "Color",
                    Members = new List<EnumMember>
                    {
                        new EnumMember { Name = "RED", Value = new IntegerLiteral { Value = "1" } },
                        new EnumMember { Name = "GREEN", Value = new IntegerLiteral { Value = "2" } },
                        new EnumMember { Name = "BLUE", Value = new IntegerLiteral { Value = "3" } }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}");
    }

    [Fact]
    public void GeneratedCode_InterfaceDeclaration_CompilesSuccessfully()
    {
        // Arrange
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                new InterfaceDef
                {
                    Name = "IDrawable",
                    Body = new List<Statement>
                    {
                        new FunctionDef
                        {
                            Name = "draw",
                            Parameters = new List<Parameter>(),
                            ReturnType = new TypeAnnotation { Name = "void" },
                            Body = new List<Statement>
                            {
                                new PassStatement()
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}");
    }

    [Fact]
    public void GeneratedCode_WithImports_CompilesSuccessfully()
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
                        new ImportAlias { Name = "system.text" }
                    }
                },
                new FunctionDef
                {
                    Name = "test",
                    Parameters = new List<Parameter>(),
                    ReturnType = new TypeAnnotation { Name = "void" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                }
            }
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}");
    }
}
