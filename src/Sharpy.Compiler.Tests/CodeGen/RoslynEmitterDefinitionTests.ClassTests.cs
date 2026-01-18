using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for class definition code generation
/// </summary>
public partial class RoslynEmitterDefinitionTests
{
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
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } },
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

    [Fact]
    public void GenerateClassDeclaration_ImplementsSingleInterface_GeneratesInterfaceInheritance()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Circle",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "IDrawable" }
            },
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "draw",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    },
                    ReturnType = new TypeAnnotation { Name = "None" },
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
        Assert.Contains("public class Circle : IDrawable", code);
        Assert.Contains("public void Draw()", code);
    }

    [Fact]
    public void GenerateClassDeclaration_ImplementsMultipleInterfaces_GeneratesMultipleInheritance()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Button",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "IDrawable" },
                new TypeAnnotation { Name = "IClickable" }
            },
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "draw",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    },
                    ReturnType = new TypeAnnotation { Name = "None" },
                    Body = new List<Statement>
                    {
                        new PassStatement()
                    }
                },
                new FunctionDef
                {
                    Name = "on_click",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" }
                    },
                    ReturnType = new TypeAnnotation { Name = "None" },
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
        Assert.Contains("public class Button : IDrawable, IClickable", code);
        Assert.Contains("public void Draw()", code);
        Assert.Contains("public void OnClick()", code);
    }

    [Fact]
    public void GenerateClassDeclaration_InheritsClassAndImplementsInterface_GeneratesCorrectOrder()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "ColoredShape",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "Shape" },
                new TypeAnnotation { Name = "IColorable" }
            },
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "set_color",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" },
                        new Parameter
                        {
                            Name = "color",
                            Type = new TypeAnnotation { Name = "str" }
                        }
                    },
                    ReturnType = new TypeAnnotation { Name = "None" },
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

        // Assert - C# requires base class first, then interfaces
        Assert.Contains("public class ColoredShape : Shape, IColorable", code);
        Assert.Contains("public void SetColor(string color)", code);
    }

    [Fact]
    public void GenerateClassDeclaration_ImplementsInterfaceWithMethodNameMangling_ManglesCorrectly()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "game_object",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "IUpdateable" }
            },
            Body = new List<Statement>
            {
                new FunctionDef
                {
                    Name = "on_update",
                    Parameters = new List<Parameter>
                    {
                        new Parameter { Name = "self" },
                        new Parameter
                        {
                            Name = "delta_time",
                            Type = new TypeAnnotation { Name = "float" }
                        }
                    },
                    ReturnType = new TypeAnnotation { Name = "None" },
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

        // Assert - Type names preserve exact casing, method names are PascalCased
        Assert.Contains("public class game_object : IUpdateable", code);
        Assert.Contains("public void OnUpdate(double deltaTime)", code);
    }

    [Fact]
    public void GenerateClassDeclaration_ImplementsGenericInterface_GeneratesGenericConstraint()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "StringList",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation
                {
                    Name = "IList",
                    TypeArguments = new List<TypeAnnotation>
                    {
                        new TypeAnnotation { Name = "str" }
                    }
                }
            },
            Body = new List<Statement>
            {
                new PassStatement()
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public class StringList : IList<string>", code);
    }

    [Fact]
    public void GenerateClassDeclaration_ImplementsInterfaceWithProperties_GeneratesPropertyImplementations()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "Entity",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "IEntity" }
            },
            Body = new List<Statement>
            {
                new VariableDeclaration
                {
                    Name = "id",
                    Type = new TypeAnnotation { Name = "int" },
                    InitialValue = new IntegerLiteral { Value = "0" }
                },
                new VariableDeclaration
                {
                    Name = "name",
                    Type = new TypeAnnotation { Name = "str" },
                    InitialValue = new StringLiteral { Value = "Unknown" }
                }
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public class Entity : IEntity", code);
        Assert.Contains("public int Id = 0;", code);
        Assert.Contains("public string Name = \"Unknown\";", code);
    }

    [Fact]
    public void GenerateClassDeclaration_EmptyClassImplementsInterface_GeneratesEmptyBody()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "EmptyDrawable",
            BaseClasses = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "IDrawable" }
            },
            Body = new List<Statement>
            {
                new PassStatement()
            }
        };

        // Act
        var module = new Module { Body = new List<Statement> { classDef } };
        var compilationUnit = _emitter.GenerateCompilationUnit(module);
        var code = compilationUnit.NormalizeWhitespace().ToFullString();

        // Assert
        Assert.Contains("public class EmptyDrawable : IDrawable", code);
        // Class should have empty body (just braces)
        Assert.Contains("{", code);
        Assert.Contains("}", code);
    }

    #endregion

}
