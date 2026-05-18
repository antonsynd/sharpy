using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenGenerics
{
    private static readonly string[] ConcreteTypes = { "int", "str", "bool" };

    public static Gen<Module> GenericClassProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.Int[1, 2],
            Gen.OneOfConst(ConcreteTypes),
            (fieldCount, instantiationType) =>
            {
                var typeParams = ImmutableArray.Create(
                    new TypeParameterDef { Name = "T" });

                var fields = fieldCount > 1
                    ? ImmutableArray.Create(("value", "T"), ("label", "str"))
                    : ImmutableArray.Create(("value", "T"));

                var classDef = BuildGenericClassDef("Box", typeParams, fields);

                var ctorArgs = fieldCount > 1
                    ? ImmutableArray.Create(BuildLiteral(instantiationType), (Expression)new StringLiteral { Value = "item" })
                    : ImmutableArray.Create(BuildLiteral(instantiationType));

                var mainBody = ImmutableArray.CreateBuilder<Statement>();
                mainBody.Add(new VariableDeclaration
                {
                    Name = "b",
                    InitialValue = BuildGenericInstantiation("Box",
                        ImmutableArray.Create(instantiationType), ctorArgs)
                });
                mainBody.Add(new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new FunctionCall
                            {
                                Function = new MemberAccess
                                {
                                    Object = new Identifier { Name = "b" },
                                    Member = "get"
                                },
                                Arguments = ImmutableArray<Expression>.Empty
                            })
                    }
                });

                var main = new FunctionDef
                {
                    Name = "main",
                    Body = mainBody.ToImmutable()
                };

                return new Module
                {
                    Body = ImmutableArray.Create<Statement>(classDef, main)
                };
            });

    public static Gen<Module> GenericFunctionProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.OneOfConst(ConcreteTypes),
            Gen.OneOfConst(ConcreteTypes),
            (type1, type2) =>
            {
                var identityFn = new FunctionDef
                {
                    Name = "identity",
                    TypeParameters = ImmutableArray.Create(
                        new TypeParameterDef { Name = "T" }),
                    Parameters = ImmutableArray.Create(
                        new Parameter { Name = "x", Type = new TypeAnnotation { Name = "T" } }),
                    ReturnType = new TypeAnnotation { Name = "T" },
                    Body = ImmutableArray.Create<Statement>(
                        new ReturnStatement
                        {
                            Value = new Identifier { Name = "x" }
                        })
                };

                var mainBody = ImmutableArray.CreateBuilder<Statement>();
                mainBody.Add(new VariableDeclaration
                {
                    Name = "a",
                    Type = TypeAnnotationFor(type1),
                    InitialValue = new FunctionCall
                    {
                        Function = new IndexAccess
                        {
                            Object = new Identifier { Name = "identity" },
                            Index = new Identifier { Name = type1 }
                        },
                        Arguments = ImmutableArray.Create(BuildLiteral(type1))
                    }
                });
                mainBody.Add(new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new Identifier { Name = "a" })
                    }
                });
                mainBody.Add(new VariableDeclaration
                {
                    Name = "b",
                    Type = TypeAnnotationFor(type2),
                    InitialValue = new FunctionCall
                    {
                        Function = new IndexAccess
                        {
                            Object = new Identifier { Name = "identity" },
                            Index = new Identifier { Name = type2 }
                        },
                        Arguments = ImmutableArray.Create(BuildLiteral(type2))
                    }
                });
                mainBody.Add(new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new Identifier { Name = "b" })
                    }
                });

                var main = new FunctionDef
                {
                    Name = "main",
                    Body = mainBody.ToImmutable()
                };

                return new Module
                {
                    Body = ImmutableArray.Create<Statement>(identityFn, main)
                };
            });

    public static Gen<Module> MultiTypeParamProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.OneOfConst(ConcreteTypes),
            Gen.OneOfConst(ConcreteTypes),
            (typeA, typeB) =>
            {
                var typeParams = ImmutableArray.Create(
                    new TypeParameterDef { Name = "A" },
                    new TypeParameterDef { Name = "B" });

                var fields = ImmutableArray.Create(("first", "A"), ("second", "B"));

                var methods = ImmutableArray.Create(
                    BuildMethod("get_first", ImmutableArray<(string, string)>.Empty, "A"),
                    BuildMethod("get_second", ImmutableArray<(string, string)>.Empty, "B"));

                var classDef = BuildGenericClassDef("Pair", typeParams, fields,
                    extraMethods: methods);

                var mainBody = ImmutableArray.CreateBuilder<Statement>();
                mainBody.Add(new VariableDeclaration
                {
                    Name = "p",
                    InitialValue = BuildGenericInstantiation("Pair",
                        ImmutableArray.Create(typeA, typeB),
                        ImmutableArray.Create(BuildLiteral(typeA), BuildLiteral(typeB)))
                });
                mainBody.Add(PrintCall(new FunctionCall
                {
                    Function = new MemberAccess
                    {
                        Object = new Identifier { Name = "p" },
                        Member = "get_first"
                    },
                    Arguments = ImmutableArray<Expression>.Empty
                }));
                mainBody.Add(PrintCall(new FunctionCall
                {
                    Function = new MemberAccess
                    {
                        Object = new Identifier { Name = "p" },
                        Member = "get_second"
                    },
                    Arguments = ImmutableArray<Expression>.Empty
                }));

                var main = new FunctionDef
                {
                    Name = "main",
                    Body = mainBody.ToImmutable()
                };

                return new Module
                {
                    Body = ImmutableArray.Create<Statement>(classDef, main)
                };
            });

    public static Gen<Module> GenericWithInheritanceProgram(TypeEnv env, int fuel) =>
        Gen.OneOfConst(ConcreteTypes).Select(concreteType =>
        {
            var containerTypeParams = ImmutableArray.Create(
                new TypeParameterDef { Name = "T" });

            var containerFields = ImmutableArray.Create(("value", "T"));

            var describeMethod = new FunctionDef
            {
                Name = "describe",
                Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                ReturnType = new TypeAnnotation { Name = "str" },
                Body = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = new StringLiteral { Value = "container" } }),
                Decorators = ImmutableArray.Create(
                    new Decorator { QualifiedParts = ImmutableArray.Create("virtual") })
            };

            var containerDef = BuildGenericClassDef("Container", containerTypeParams,
                containerFields, extraMethods: ImmutableArray.Create(describeMethod));

            var namedContainerTypeParams = ImmutableArray.Create(
                new TypeParameterDef { Name = "T" });

            var namedContainerBody = ImmutableArray.CreateBuilder<Statement>();
            namedContainerBody.Add(new VariableDeclaration
            {
                Name = "name",
                Type = new TypeAnnotation { Name = "str" }
            });

            // __init__(self, value: T, name: str)
            var initParams = ImmutableArray.Create(
                new Parameter { Name = "self" },
                new Parameter { Name = "value", Type = new TypeAnnotation { Name = "T" } },
                new Parameter { Name = "name", Type = new TypeAnnotation { Name = "str" } });

            var initBody = ImmutableArray.CreateBuilder<Statement>();
            initBody.Add(new ExpressionStatement
            {
                Expression = new FunctionCall
                {
                    Function = new MemberAccess
                    {
                        Object = new FunctionCall
                        {
                            Function = new Identifier { Name = "super" },
                            Arguments = ImmutableArray<Expression>.Empty
                        },
                        Member = "__init__"
                    },
                    Arguments = ImmutableArray.Create<Expression>(
                        new Identifier { Name = "value" })
                }
            });
            initBody.Add(new Assignment
            {
                Target = new MemberAccess
                {
                    Object = new Identifier { Name = "self" },
                    Member = "name"
                },
                Value = new Identifier { Name = "name" }
            });

            namedContainerBody.Add(new FunctionDef
            {
                Name = "__init__",
                Parameters = initParams,
                Body = initBody.ToImmutable()
            });

            // @override describe(self) -> str
            namedContainerBody.Add(new FunctionDef
            {
                Name = "describe",
                Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                ReturnType = new TypeAnnotation { Name = "str" },
                Body = ImmutableArray.Create<Statement>(
                    new ReturnStatement
                    {
                        Value = new MemberAccess
                        {
                            Object = new Identifier { Name = "self" },
                            Member = "name"
                        }
                    }),
                Decorators = ImmutableArray.Create(
                    new Decorator { QualifiedParts = ImmutableArray.Create("override") })
            });

            var namedContainerDef = new ClassDef
            {
                Name = "NamedContainer",
                TypeParameters = namedContainerTypeParams,
                BaseClasses = ImmutableArray.Create(new TypeAnnotation
                {
                    Name = "Container",
                    TypeArguments = ImmutableArray.Create(
                        new TypeAnnotation { Name = "T" })
                }),
                Body = namedContainerBody.ToImmutable()
            };

            var mainBody = ImmutableArray.CreateBuilder<Statement>();
            mainBody.Add(new VariableDeclaration
            {
                Name = "nc",
                InitialValue = new FunctionCall
                {
                    Function = new IndexAccess
                    {
                        Object = new Identifier { Name = "NamedContainer" },
                        Index = new Identifier { Name = concreteType }
                    },
                    Arguments = ImmutableArray.Create(
                        BuildLiteral(concreteType),
                        (Expression)new StringLiteral { Value = "test" })
                }
            });
            mainBody.Add(PrintCall(new FunctionCall
            {
                Function = new MemberAccess
                {
                    Object = new Identifier { Name = "nc" },
                    Member = "describe"
                },
                Arguments = ImmutableArray<Expression>.Empty
            }));
            mainBody.Add(PrintCall(new MemberAccess
            {
                Object = new Identifier { Name = "nc" },
                Member = "value"
            }));

            var main = new FunctionDef
            {
                Name = "main",
                Body = mainBody.ToImmutable()
            };

            return new Module
            {
                Body = ImmutableArray.Create<Statement>(containerDef, namedContainerDef, main)
            };
        });

    public static Gen<Module> WrongTypeArgCountProgram(TypeEnv env, int fuel) =>
        Gen.OneOfConst(ConcreteTypes).Select(concreteType =>
        {
            var typeParams = ImmutableArray.Create(
                new TypeParameterDef { Name = "T" });
            var fields = ImmutableArray.Create(("value", "T"));
            var classDef = BuildGenericClassDef("Box", typeParams, fields);

            var mainBody = ImmutableArray.Create<Statement>(
                new VariableDeclaration
                {
                    Name = "b",
                    InitialValue = new FunctionCall
                    {
                        Function = new IndexAccess
                        {
                            Object = new Identifier { Name = "Box" },
                            Index = new TupleLiteral
                            {
                                Elements = ImmutableArray.Create<Expression>(
                                    new Identifier { Name = concreteType },
                                    new Identifier { Name = "str" })
                            }
                        },
                        Arguments = ImmutableArray.Create(BuildLiteral(concreteType))
                    }
                });

            var main = new FunctionDef
            {
                Name = "main",
                Body = mainBody
            };

            return new Module
            {
                Body = ImmutableArray.Create<Statement>(classDef, main)
            };
        });

    public static Gen<Module> TypeMismatchOnGenericFieldProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.OneOfConst(ConcreteTypes),
            Gen.OneOfConst(ConcreteTypes).Where(t => t != "int"),
            (instantType, wrongType) =>
            {
                var typeParams = ImmutableArray.Create(
                    new TypeParameterDef { Name = "T" });
                var fields = ImmutableArray.Create(("value", "T"));
                var classDef = BuildGenericClassDef("Box", typeParams, fields);

                var mainBody = ImmutableArray.CreateBuilder<Statement>();
                mainBody.Add(new VariableDeclaration
                {
                    Name = "b",
                    InitialValue = BuildGenericInstantiation("Box",
                        ImmutableArray.Create("int"),
                        ImmutableArray.Create<Expression>(new IntegerLiteral { Value = "42" }))
                });
                mainBody.Add(new Assignment
                {
                    Target = new MemberAccess
                    {
                        Object = new Identifier { Name = "b" },
                        Member = "value"
                    },
                    Value = BuildLiteral(wrongType)
                });

                var main = new FunctionDef
                {
                    Name = "main",
                    Body = mainBody.ToImmutable()
                };

                return new Module
                {
                    Body = ImmutableArray.Create<Statement>(classDef, main)
                };
            });

    public static Gen<Module> GenericConstraintProgram(TypeEnv env, int fuel) =>
        Gen.OneOfConst(ConcreteTypes).Select(concreteType =>
        {
            var importStmt = new FromImportStatement
            {
                Module = "collections",
                Names = ImmutableArray.Create(
                    new ImportAlias { Name = "Comparable" })
            };

            var typeParams = ImmutableArray.Create(
                new TypeParameterDef
                {
                    Name = "T",
                    Constraints = ImmutableArray.Create<ConstraintClause>(
                        new TypeConstraint
                        {
                            Type = new TypeAnnotation { Name = "Comparable" }
                        })
                });

            var fields = ImmutableArray.Create(("value", "T"));
            var classDef = BuildGenericClassDef("SortedBox", typeParams, fields);

            var mainBody = ImmutableArray.CreateBuilder<Statement>();
            mainBody.Add(new VariableDeclaration
            {
                Name = "sb",
                InitialValue = BuildGenericInstantiation("SortedBox",
                    ImmutableArray.Create(concreteType),
                    ImmutableArray.Create(BuildLiteral(concreteType)))
            });
            mainBody.Add(PrintCall(new MemberAccess
            {
                Object = new Identifier { Name = "sb" },
                Member = "value"
            }));

            var main = new FunctionDef
            {
                Name = "main",
                Body = mainBody.ToImmutable()
            };

            return new Module
            {
                Body = ImmutableArray.Create<Statement>(importStmt, classDef, main)
            };
        });

    private static Expression BuildLiteral(string type) => type switch
    {
        "int" => new IntegerLiteral { Value = "42" },
        "str" => new StringLiteral { Value = "hello" },
        "bool" => new BooleanLiteral { Value = true },
        "float" => new FloatLiteral { Value = "3.14" },
        _ => new IntegerLiteral { Value = "0" }
    };

    private static TypeAnnotation TypeAnnotationFor(string type) =>
        new TypeAnnotation { Name = type };

    private static ClassDef BuildGenericClassDef(
        string name,
        ImmutableArray<TypeParameterDef> typeParams,
        ImmutableArray<(string Name, string Type)> fields,
        ImmutableArray<TypeAnnotation>? baseClasses = null,
        ImmutableArray<FunctionDef>? extraMethods = null)
    {
        var body = ImmutableArray.CreateBuilder<Statement>();

        foreach (var (fieldName, fieldType) in fields)
        {
            body.Add(new VariableDeclaration
            {
                Name = fieldName,
                Type = TypeAnnotationFor(fieldType)
            });
        }

        body.Add(BuildConstructor(fields));

        if (extraMethods != null)
        {
            foreach (var method in extraMethods.Value)
                body.Add(method);
        }
        else
        {
            foreach (var (fieldName, fieldType) in fields)
            {
                body.Add(BuildMethod($"get", ImmutableArray<(string, string)>.Empty, fieldType));
                break;
            }
        }

        return new ClassDef
        {
            Name = name,
            TypeParameters = typeParams,
            BaseClasses = baseClasses ?? ImmutableArray<TypeAnnotation>.Empty,
            Body = body.ToImmutable()
        };
    }

    private static FunctionDef BuildConstructor(
        ImmutableArray<(string Name, string Type)> fields)
    {
        var parameters = ImmutableArray.CreateBuilder<Parameter>();
        parameters.Add(new Parameter { Name = "self" });
        foreach (var (fieldName, fieldType) in fields)
        {
            parameters.Add(new Parameter
            {
                Name = fieldName,
                Type = TypeAnnotationFor(fieldType)
            });
        }

        var bodyStmts = ImmutableArray.CreateBuilder<Statement>();
        foreach (var (fieldName, _) in fields)
        {
            bodyStmts.Add(new Assignment
            {
                Target = new MemberAccess
                {
                    Object = new Identifier { Name = "self" },
                    Member = fieldName
                },
                Value = new Identifier { Name = fieldName }
            });
        }

        return new FunctionDef
        {
            Name = "__init__",
            Parameters = parameters.ToImmutable(),
            Body = bodyStmts.ToImmutable()
        };
    }

    private static FunctionDef BuildMethod(string name,
        ImmutableArray<(string Name, string Type)> parameters, string returnType)
    {
        var paramList = ImmutableArray.CreateBuilder<Parameter>();
        paramList.Add(new Parameter { Name = "self" });
        foreach (var (paramName, paramType) in parameters)
        {
            paramList.Add(new Parameter
            {
                Name = paramName,
                Type = TypeAnnotationFor(paramType)
            });
        }

        Expression returnExpr = returnType switch
        {
            "int" => new IntegerLiteral { Value = "42" },
            "str" => new StringLiteral { Value = "result" },
            "bool" => new BooleanLiteral { Value = true },
            _ => new MemberAccess
            {
                Object = new Identifier { Name = "self" },
                Member = name.StartsWith("get_") ? name.Substring(4) : "value"
            }
        };

        return new FunctionDef
        {
            Name = name,
            Parameters = paramList.ToImmutable(),
            ReturnType = TypeAnnotationFor(returnType),
            Body = ImmutableArray.Create<Statement>(
                new ReturnStatement { Value = returnExpr })
        };
    }

    private static Expression BuildGenericInstantiation(string className,
        ImmutableArray<string> typeArgs, ImmutableArray<Expression> ctorArgs)
    {
        Expression index = typeArgs.Length == 1
            ? new Identifier { Name = typeArgs[0] }
            : new TupleLiteral
            {
                Elements = typeArgs.Select(t => (Expression)new Identifier { Name = t })
                    .ToImmutableArray()
            };

        return new FunctionCall
        {
            Function = new IndexAccess
            {
                Object = new Identifier { Name = className },
                Index = index
            },
            Arguments = ctorArgs
        };
    }

    private static ExpressionStatement PrintCall(Expression arg) =>
        new ExpressionStatement
        {
            Expression = new FunctionCall
            {
                Function = new Identifier { Name = "print" },
                Arguments = ImmutableArray.Create(arg)
            }
        };
}
