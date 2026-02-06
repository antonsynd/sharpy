using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Integration tests that verify generated C# code compiles successfully
/// </summary>
public class RoslynEmitterIntegrationTests
{
    private RoslynEmitter CreateEmitter(string? sourceFilePath = null, bool isEntryPoint = true)
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = sourceFilePath,
            IsEntryPoint = isEntryPoint
        };
        return new RoslynEmitter(context);
    }

    /// <summary>
    /// Creates an emitter with full semantic analysis including CodeGenInfo computation.
    /// Use this when testing code generation that depends on semantic analysis results.
    /// </summary>
    private RoslynEmitter CreateEmitterWithSemanticAnalysis(Module module, string? sourceFilePath = null, bool isEntryPoint = true)
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();
        var logger = NullLogger.Instance;

        // Run name resolution
        var nameResolver = new NameResolver(symbolTable, logger, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        // Run type checking with CodeGenInfo computation
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger)
        {
            SemanticBinding = semanticBinding
        };
        typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: isEntryPoint);

        // Materialize onto Symbol properties for code generation
        semanticBinding.MaterializeCodeGenInfo();
        semanticBinding.MaterializeVariableTypes();

        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = sourceFilePath,
            IsEntryPoint = isEntryPoint,
            SemanticBinding = semanticBinding
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

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(SharpyCoreReference.Location),
            MetadataReference.CreateFromFile(systemRuntimePath),
        };

        // Add netstandard reference (required for Sharpy.Core which targets netstandard)
        try
        {
            var netstandardAssembly = System.Reflection.Assembly.Load("netstandard");
            references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
        }
        catch
        {
            // Fallback: try to find in runtime directory
            var netstandardPath = Path.Combine(
                Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                "netstandard.dll");
            if (File.Exists(netstandardPath))
            {
                references.Add(MetadataReference.CreateFromFile(netstandardPath));
            }
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
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
            Body = ImmutableArray<Statement>.Empty
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
                    Parameters = ImmutableArray<Parameter>.Empty,
                    ReturnType = new TypeAnnotation { Name = "void" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
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
                    }.ToImmutableArray(),
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
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
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
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
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
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
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
                            Parameters = ImmutableArray<Parameter>.Empty,
                            ReturnType = new TypeAnnotation { Name = "void" },
                            Body = new List<Statement>
                            {
                                new PassStatement()
                            }.ToImmutableArray()
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
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
                    }.ToImmutableArray()
                },
                new FunctionDef
                {
                    Name = "test",
                    Parameters = ImmutableArray<Parameter>.Empty,
                    ReturnType = new TypeAnnotation { Name = "void" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}");
    }

    [Fact]
    public void GeneratedCode_ConstReferenceInSameScope_UsesConsistentNaming()
    {
        // Arrange - Const declared and referenced in the same scope (Main)
        // This tests that const name mangling is consistent between declaration and reference
        var emitter = CreateEmitter();
        var module = new Module
        {
            Body = new List<Statement>
            {
                // const BASE: int = 10
                new VariableDeclaration
                {
                    Name = "BASE",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "10" },
                    IsConst = true
                },
                // x = BASE * 2 (reference in same scope)
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new BinaryOp
                    {
                        Left = new Identifier { Name = "BASE" },
                        Operator = BinaryOperator.Multiply,
                        Right = new IntegerLiteral { Value = "2" }
                    }
                }
            }.ToImmutableArray()
        };

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}\n\nGenerated code:\n{code}");
        // Verify both declaration and reference use the same name (BASE with @ escape)
        Assert.Contains("const int @BASE = 10", code);
        Assert.Contains("@BASE * 2", code);
    }

    [Fact]
    public void GeneratedCode_ConstReferenceInFunctionScope_UsesConsistentNaming()
    {
        // Arrange - Const declared at module level, referenced in function
        // Note: Currently module-level consts become local in Main(), so this tests
        // that the naming is consistent even though scoping needs future work
        var module = new Module
        {
            Body = new List<Statement>
            {
                // const BASE: int = 10
                new VariableDeclaration
                {
                    Name = "BASE",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "10" },
                    IsConst = true
                },
                // def calculate() -> int:
                //     return BASE * 2
                new FunctionDef
                {
                    Name = "calculate",
                    Parameters = ImmutableArray<Parameter>.Empty,
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new BinaryOp
                            {
                                Left = new Identifier { Name = "BASE" },  // Reference to module-level const
                                Operator = BinaryOperator.Multiply,
                                Right = new IntegerLiteral { Value = "2" }
                            }
                        }
                    }.ToImmutableArray()
                }
            }.ToImmutableArray()
        };

        // Create emitter with semantic analysis (required for CodeGenInfo)
        var emitter = CreateEmitterWithSemanticAnalysis(module);

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();

        // Assert - verify naming is consistent (both use @BASE)
        // Note: The code won't compile yet because const is local to Main, but naming is correct
        Assert.Contains("const int @BASE = 10", code);
        Assert.Contains("@BASE * 2", code);
    }

    [Fact]
    public void GeneratedCode_ModuleLevelVariableRedefinition_BecomesLocalVariables()
    {
        // Arrange - Module-level variable redefined with different type
        // Sharpy allows: x: int = 1; x: auto = "hello"
        // When there are multiple declarations for the same variable,
        // they become local variables in Main() to preserve execution order
        var module = new Module
        {
            Body = new List<Statement>
            {
                // x: int = 1
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "1" }
                },
                // x: auto = "hello" (redefinition with different type)
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new StringLiteral { Value = "hello" }
                }
            }.ToImmutableArray()
        };

        // Create emitter with semantic analysis (required for CodeGenInfo and execution order detection)
        var emitter = CreateEmitterWithSemanticAnalysis(module);

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}\n\nGenerated code:\n{code}");

        // With execution order issues (multiple declarations), variables become locals in Main
        // Verify first declaration appears as local variable (uses explicit type since it's specified as int)
        Assert.True(code.Contains("int x = 1"), $"Expected 'int x = 1' in generated code but got:\n{code}");

        // Verify second declaration appears as versioned local (var x_1 since type is auto)
        Assert.Contains("var x_1 = ", code);

        // Verify no static fields for x
        var xFieldCount = System.Text.RegularExpressions.Regex.Matches(code, @"public static \w+ X").Count;
        Assert.Equal(0, xFieldCount);
    }

    [Fact]
    public void GeneratedCode_ModuleLevelConstRedefinition_SkipsDuplicateField()
    {
        // Arrange - Module-level const redefined with different type
        var module = new Module
        {
            Body = new List<Statement>
            {
                // const MAX_SIZE: int = 100
                new VariableDeclaration
                {
                    Name = "MAX_SIZE",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "100" },
                    IsConst = true
                },
                // const MAX_SIZE: auto = 200 (redefinition with same type but different value)
                new VariableDeclaration
                {
                    Name = "MAX_SIZE",
                    Type = new TypeAnnotation { Name = "auto" },
                    InitialValue = new IntegerLiteral { Value = "200" },
                    IsConst = true
                }
            }.ToImmutableArray()
        };

        // Create emitter with semantic analysis (required for CodeGenInfo)
        var emitter = CreateEmitterWithSemanticAnalysis(module);

        // Act
        var result = emitter.GenerateCompilationUnit(module);
        var code = result.ToFullString();
        var compiles = CompileCode(code, out var errors);

        // Assert
        Assert.True(compiles, $"Generated code should compile. Errors:\n{errors}\n\nGenerated code:\n{code}");

        // Verify only the first declaration appears (MAX_SIZE = 100)
        // Constants keep their CAPS_SNAKE_CASE naming
        Assert.Contains("MAX_SIZE = 100", code);

        // Verify the second declaration does NOT appear (no MAX_SIZE = 200)
        Assert.DoesNotContain("200", code);

        // Verify there's only one MAX_SIZE field
        var maxSizeFieldCount = System.Text.RegularExpressions.Regex.Matches(code, @"\bMAX_SIZE\b").Count;
        Assert.Equal(1, maxSizeFieldCount);
    }
}
