using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for code generation of definitions (functions, classes, structs, interfaces, enums)
/// </summary>
public class RoslynEmitterDefinitionTests
{
    private readonly RoslynEmitter _emitter;
    private readonly CodeGenContext _context;

    public RoslynEmitterDefinitionTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
    }

    #region Function Definition Tests

    [Fact]
    public void GenerateFunctionDeclaration_SimpleFunction_GeneratesPublicStaticMethod()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "greet",
            Parameters = new List<Parameter>(),
            ReturnType = null,
            Body = new List<Statement>
            {
                new ReturnStatement { Value = null }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { func } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static void Greet()", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithParameters_GeneratesParameterList()
    {
        // Arrange
        var func = new FunctionDef
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
                new ReturnStatement { Value = new IntegerLiteral { Value = "0" } }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { func } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("int Add(int x, int y)", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithDefaultParameter_GeneratesDefaultValue()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "greet",
            Parameters = new List<Parameter>
            {
                new Parameter
                {
                    Name = "name",
                    Type = new TypeAnnotation { Name = "string" },
                    DefaultValue = new StringLiteral { Value = "World" }
                }
            },
            ReturnType = new TypeAnnotation { Name = "string" },
            Body = new List<Statement>
            {
                new ReturnStatement { Value = new StringLiteral { Value = "Hello" } }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { func } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("string Greet(string name = \"World\")", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithDocstring_GeneratesXmlDoc()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "greet",
            Parameters = new List<Parameter>(),
            ReturnType = new TypeAnnotation { Name = "string" },
            Body = new List<Statement>
            {
                new ReturnStatement { Value = new StringLiteral { Value = "Hello" } }
            },
            DocString = "Greets the world"
        };

        // Act
        var module = new Module { Body = new List<Statement> { func } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// Greets the world", code);
        Assert.Contains("/// </summary>", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_WithPrivateDecorator_GeneratesPrivateMethod()
    {
        // Arrange
        var func = new FunctionDef
        {
            Name = "helper",
            Parameters = new List<Parameter>(),
            ReturnType = null,
            Body = new List<Statement> { new PassStatement() },
            Decorators = new List<Decorator>
            {
                new Decorator { Name = "private" }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { func } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("private static void Helper()", code);
    }

    [Fact]
    public void GenerateFunctionDeclaration_ExampleFromSpec_GeneratesCorrectSignature()
    {
        // Example from spec: def add(a: int, b: int = 1) -> int: return a * b
        // Should generate: public static int Add(int a, int b = 1)
        var func = new FunctionDef
        {
            Name = "add",
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "a", Type = new TypeAnnotation { Name = "int" } },
                new Parameter
                {
                    Name = "b",
                    Type = new TypeAnnotation { Name = "int" },
                    DefaultValue = new IntegerLiteral { Value = "1" }
                }
            },
            ReturnType = new TypeAnnotation { Name = "int" },
            Body = new List<Statement>
            {
                new ReturnStatement
                {
                    Value = new BinaryOp
                    {
                        Left = new Identifier { Name = "a" },
                        Operator = BinaryOperator.Multiply,
                        Right = new Identifier { Name = "b" }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { func } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static int Add(int a, int b = 1)", code);
        Assert.Contains("return a * b;", code);
    }

    #endregion

    #region Class Definition Tests

    [Fact]
    public void GenerateClassDeclaration_SimpleClass_GeneratesPublicClass()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Person",
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public class Person", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithFields_GeneratesFieldDeclarations()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Person",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "name",
                    Type = new TypeAnnotation { Name = "string" },
                    InitialValue = null
                },
                new VariableDeclaration
                {
                    Name = "age",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = null
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Debug: Print the generated code

        // Assert
        Assert.Contains("public string Name;", code);
        Assert.Contains("public int Age;", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithMethod_GeneratesMethodDeclaration()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Person",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "greet",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null }
                    },
                    ReturnType = new TypeAnnotation { Name = "string" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new StringLiteral { Value = "Hello" } }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public string Greet()", code);
        Assert.DoesNotContain("self", code); // self parameter should be skipped
    }

    [Fact]
    public void GenerateClassDeclaration_InstanceMethod_WithMultipleParameters_GeneratesCorrectSignature()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Calculator",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "add_numbers",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "first_num", Type = new TypeAnnotation { Name = "int" } },
                        new Parameter { Name = "second_num", Type = new TypeAnnotation { Name = "int" } }
                    },
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement
                        {
                            Value = new BinaryOp
                            {
                                Left = new Identifier { Name = "first_num" },
                                Operator = BinaryOperator.Add,
                                Right = new Identifier { Name = "second_num" }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        // Method name should be PascalCase
        Assert.Contains("AddNumbers", code);

        // Parameters should be camelCase
        Assert.Contains("int firstNum", code);
        Assert.Contains("int secondNum", code);

        // self parameter should be completely excluded
        Assert.DoesNotContain("self", code);

        // Complete signature verification
        Assert.Contains("public int AddNumbers(int firstNum, int secondNum)", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithBaseClass_GeneratesInheritance()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Employee",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "Person" }
            },
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public class Employee : Person", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithGenericTypeParameter_GeneratesGenericClass()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Container",
            TypeParameters = new List<string> { "T" },
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public class Container<T>", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithDocstring_GeneratesXmlDoc()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Person",
            Body = new List<Statement> { new PassStatement() },
            DocString = "Represents a person"
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// Represents a person", code);
        Assert.Contains("/// </summary>", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithConstructor_GeneratesConstructorMethod()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Person",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "name",
                    Type = new TypeAnnotation { Name = "string" },
                    InitialValue = null
                },
                new VariableDeclaration
                {
                    Name = "age",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = null
                },
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "name", Type = new TypeAnnotation { Name = "string" } },
                        new Parameter { Name = "age", Type = new TypeAnnotation { Name = "int" } }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "name"
                            },
                            Value = new Identifier { Name = "name" }
                        },
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "age"
                            },
                            Value = new Identifier { Name = "age" }
                        }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public Person(string name, int age)", code);
        Assert.Contains("Name = name", code);
        Assert.Contains("Age = age", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithMultipleConstructors_GeneratesOverloads()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Person",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "name",
                    Type = new TypeAnnotation { Name = "string" },
                    InitialValue = null
                },
                new VariableDeclaration
                {
                    Name = "age",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = null
                },
                // Constructor 1: No params (default values)
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "name"
                            },
                            Value = new StringLiteral { Value = "" }
                        },
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "age"
                            },
                            Value = new IntegerLiteral { Value = "0" }
                        }
                    }
                },
                // Constructor 2: name only
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "name", Type = new TypeAnnotation { Name = "string" } }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "name"
                            },
                            Value = new Identifier { Name = "name" }
                        },
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "age"
                            },
                            Value = new IntegerLiteral { Value = "0" }
                        }
                    }
                },
                // Constructor 3: name and age
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "name", Type = new TypeAnnotation { Name = "string" } },
                        new Parameter { Name = "age", Type = new TypeAnnotation { Name = "int" } }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "name"
                            },
                            Value = new Identifier { Name = "name" }
                        },
                        new Assignment
                        {
                            Target = new MemberAccess
                            {
                                Object = new Identifier { Name = "self" },
                                Member = "age"
                            },
                            Value = new Identifier { Name = "age" }
                        }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert - all three constructors are generated
        Assert.Contains("public Person()", code);
        Assert.Contains("public Person(string name)", code);
        Assert.Contains("public Person(string name, int age)", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithParameterlessConstructor_Works()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "EmptyClass",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public EmptyClass()", code);
    }

    [Fact]
    public void GenerateClassDeclaration_ConstructorOverloads_DifferentParamTypes_Allowed()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Value",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "data",
                    Type = new TypeAnnotation { Name = "object" },
                    InitialValue = null
                },
                // Constructor 1: int param
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "value", Type = new TypeAnnotation { Name = "int" } }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "data" },
                            Value = new Identifier { Name = "value" }
                        }
                    }
                },
                // Constructor 2: string param
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "value", Type = new TypeAnnotation { Name = "string" } }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment
                        {
                            Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "data" },
                            Value = new Identifier { Name = "value" }
                        }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert - both constructors with different parameter types
        Assert.Contains("public Value(int value)", code);
        Assert.Contains("public Value(string value)", code);
    }

    [Fact]
    public void GenerateClassDeclaration_ConstructorOverloads_DifferentParamCounts_Allowed()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Box",
            Body = new List<Statement>
            {
                new VariableDeclaration { Name = "width", Type = new TypeAnnotation { Name = "int" } },
                new VariableDeclaration { Name = "height", Type = new TypeAnnotation { Name = "int" } },
                new VariableDeclaration { Name = "depth", Type = new TypeAnnotation { Name = "int" } },
                // Constructor 1: one param (cube)
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "size", Type = new TypeAnnotation { Name = "int" } }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "width" }, Value = new Identifier { Name = "size" } },
                        new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "height" }, Value = new Identifier { Name = "size" } },
                        new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "depth" }, Value = new Identifier { Name = "size" } }
                    }
                },
                // Constructor 2: three params
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null },
                        new Parameter { Name = "width", Type = new TypeAnnotation { Name = "int" } },
                        new Parameter { Name = "height", Type = new TypeAnnotation { Name = "int" } },
                        new Parameter { Name = "depth", Type = new TypeAnnotation { Name = "int" } }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "width" }, Value = new Identifier { Name = "width" } },
                        new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "height" }, Value = new Identifier { Name = "height" } },
                        new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "depth" }, Value = new Identifier { Name = "depth" } }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public Box(int size)", code);
        Assert.Contains("public Box(int width, int height, int depth)", code);
    }

    [Fact]
    public void GenerateClassDeclaration_WithFieldEdgeCases_GeneratesCorrectFields()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "DataClass",
            Body = new List<Statement>
            {
                // Field with type annotation and no initializer
                new VariableDeclaration
                {
                    Name = "user_name",
                    Type = new TypeAnnotation { Name = "string" },
                    InitialValue = null
                },
                // Field with type annotation and initializer
                new VariableDeclaration
                {
                    Name = "user_count",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "0" }
                },
                // Field without type annotation but with initializer (falls back to object)
                new VariableDeclaration
                {
                    Name = "default_data",
                    Type = null,
                    InitialValue = new IntegerLiteral { Value = "42" }
                },
                // Const field with type annotation and initializer
                new VariableDeclaration
                {
                    Name = "max_users",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "100" },
                    IsConst = true
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert - verify name mangling (snake_case -> PascalCase)
        Assert.Contains("public string UserName;", code);

        // Assert - verify field with initializer
        Assert.Contains("public int UserCount = 0;", code);

        // Assert - verify field without type annotation falls back to object
        Assert.Contains("public object DefaultData = 42;", code);

        // Assert - verify const field
        Assert.Contains("public const int MaxUsers = 100;", code);
    }

    #endregion

    #region Struct Definition Tests

    [Fact]
    public void GenerateStructDeclaration_SimpleStruct_GeneratesPublicStruct()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Point",
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public struct Point", code);
    }

    [Fact]
    public void GenerateStructDeclaration_WithFields_GeneratesFieldDeclarations()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Point",
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "x",
                    Type = new TypeAnnotation { Name = "double" },
                    InitialValue = null
                },
                new VariableDeclaration
                {
                    Name = "y",
                    Type = new TypeAnnotation { Name = "double" },
                    InitialValue = null
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public double X;", code);
        Assert.Contains("public double Y;", code);
    }

    [Fact]
    public void GenerateStructDeclaration_WithGenericTypeParameter_GeneratesGenericStruct()
    {
        // Arrange
        var structDef = new StructDef
        {
            Name = "Pair",
            TypeParameters = new List<string> { "T1", "T2" },
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { structDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public struct Pair<T1, T2>", code);
    }

    #endregion

    #region Interface Definition Tests

    [Fact]
    public void GenerateInterfaceDeclaration_SimpleInterface_GeneratesPublicInterface()
    {
        // Arrange
        var interfaceDef = new InterfaceDef
        {
            Name = "IDrawable",
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { interfaceDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Debug

        // Assert
        Assert.Contains("public interface IDrawable", code);
    }

    [Fact]
    public void GenerateInterfaceDeclaration_WithMethod_GeneratesMethodSignature()
    {
        // Arrange
        var interfaceDef = new InterfaceDef
        {
            Name = "IDrawable",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "draw",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self", Type = null }
                    },
                    ReturnType = null,
                    Body = new List<Statement>
                    {
                        new ExpressionStatement { Expression = new EllipsisLiteral() }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { interfaceDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("void Draw();", code);
        Assert.DoesNotContain("self", code); // self parameter should be skipped
    }

    [Fact]
    public void GenerateInterfaceDeclaration_WithBaseInterface_GeneratesInheritance()
    {
        // Arrange
        var interfaceDef = new InterfaceDef
        {
            Name = "IShape",
            BaseInterfaces = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "IDrawable" }
            },
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { interfaceDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public interface IShape : IDrawable", code);
    }

    [Fact]
    public void GenerateInterfaceDeclaration_WithGenericTypeParameter_GeneratesGenericInterface()
    {
        // Arrange
        var interfaceDef = new InterfaceDef
        {
            Name = "IRepository",
            TypeParameters = new List<string> { "T" },
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { interfaceDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public interface IRepository<T>", code);
    }

    #endregion

    #region Enum Definition Tests

    [Fact]
    public void GenerateEnumDeclaration_SimpleEnum_GeneratesPublicEnum()
    {
        // Arrange
        var enumDef = new EnumDef
        {
            Name = "Color",
            Members = new List<EnumMember>
            {
                new EnumMember { Name = "RED", Value = new IntegerLiteral { Value = "1" } },
                new EnumMember { Name = "GREEN", Value = new IntegerLiteral { Value = "2" } },
                new EnumMember { Name = "BLUE", Value = new IntegerLiteral { Value = "3" } }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { enumDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public enum Color", code);
        Assert.Contains("RED = 1", code);
        Assert.Contains("GREEN = 2", code);
        Assert.Contains("BLUE = 3", code);
    }

    [Fact]
    public void GenerateEnumDeclaration_WithoutExplicitValues_GeneratesEnumMembers()
    {
        // Arrange
        var enumDef = new EnumDef
        {
            Name = "Status",
            Members = new List<EnumMember>
            {
                new EnumMember { Name = "PENDING", Value = null },
                new EnumMember { Name = "ACTIVE", Value = null },
                new EnumMember { Name = "COMPLETE", Value = null }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { enumDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public enum Status", code);
        Assert.Contains("PENDING", code);
        Assert.Contains("ACTIVE", code);
        Assert.Contains("COMPLETE", code);
    }

    [Fact]
    public void GenerateEnumDeclaration_WithDocstring_GeneratesXmlDoc()
    {
        // Arrange
        var enumDef = new EnumDef
        {
            Name = "Color",
            Members = new List<EnumMember>
            {
                new EnumMember { Name = "RED", Value = new IntegerLiteral { Value = "1" } }
            },
            DocString = "RGB color values"
        };

        // Act
        var module = new Module { Body = new List<Statement> { enumDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// RGB color values", code);
        Assert.Contains("/// </summary>", code);
    }

    #endregion

    #region Decorator Tests

    [Fact]
    public void GenerateClassDeclaration_WithAbstractDecorator_GeneratesAbstractClass()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Shape",
            Decorators = new List<Decorator>
            {
                new Decorator { Name = "abstract" }
            },
            Body = new List<Statement> { new PassStatement() }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public abstract class Shape", code);
    }

    [Fact]
    public void GenerateMethod_WithAbstractMethodDecorator_GeneratesAbstractMethodWithoutBody()
    {
        // Arrange - Abstract method with ellipsis should have no body
        var classDef = new ClassDef
        {
            Name = "Shape",
            Decorators = new List<Decorator>
            {
                new Decorator { Name = "abstract" }
            },
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "area",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "abstractmethod" }
                    },
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    },
                    ReturnType = new TypeAnnotation { Name = "float" },
                    Body = new List<Statement>
                    {
                        new ExpressionStatement
                        {
                            Expression = new EllipsisLiteral()
                        }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public abstract class Shape", code);
        // Per spec: Sharpy 'float' -> C# 'double'
        Assert.Contains("public abstract double Area();", code);
        // Abstract method should NOT have a body (no braces)
        Assert.DoesNotContain("Area()\r\n{", code);
        Assert.DoesNotContain("Area()\n{", code);
        Assert.DoesNotContain("NotImplementedException", code);
    }

    [Fact]
    public void GenerateMethod_ConcreteMethodWithEllipsis_ThrowsNotImplementedException()
    {
        // Arrange - Concrete method with ellipsis should throw NotImplementedException
        var classDef = new ClassDef
        {
            Name = "Todo",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "not_yet_implemented",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    },
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = new List<Statement>
                    {
                        new ExpressionStatement
                        {
                            Expression = new EllipsisLiteral()
                        }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public int NotYetImplemented()", code);
        Assert.Contains("throw new System.NotImplementedException()", code);
    }

    [Fact]
    public void GenerateMethod_WithStaticDecorator_GeneratesStaticMethod()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Math",
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "add",
                    Decorators = new List<Decorator>
                    {
                        new Decorator { Name = "staticmethod" }
                    },
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "x", Type = new TypeAnnotation { Name = "int" } },
                        new Parameter { Name = "y", Type = new TypeAnnotation { Name = "int" } }
                    },
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = new List<Statement>
                    {
                        new ReturnStatement { Value = new IntegerLiteral { Value = "0" } }
                    }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public static int Add(int x, int y)", code);
    }

    #endregion
}
