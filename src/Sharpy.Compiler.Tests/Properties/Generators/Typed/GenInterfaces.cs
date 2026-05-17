using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenInterfaces
{
    private static readonly string[] ReturnTypes = { "int", "str", "bool" };
    private static readonly string[] ParamTypes = { "int", "str", "bool" };

    public static Gen<InterfaceDef> InterfaceDef(string name, int methodCount) =>
        Gen.Int[0, ReturnTypes.Length - 1].Array[methodCount, methodCount]
            .SelectMany(retIndices =>
                Gen.Int[0, 2].Array[methodCount, methodCount].Select(paramCounts =>
                {
                    var methods = ImmutableArray.CreateBuilder<Statement>();
                    for (int i = 0; i < methodCount; i++)
                    {
                        var paramList = ImmutableArray.CreateBuilder<Parameter>();
                        paramList.Add(new Parameter { Name = "self" });
                        for (int p = 0; p < paramCounts[i]; p++)
                        {
                            paramList.Add(new Parameter
                            {
                                Name = $"arg{p}",
                                Type = new TypeAnnotation { Name = ParamTypes[p % ParamTypes.Length] }
                            });
                        }

                        methods.Add(new FunctionDef
                        {
                            Name = $"method_{i}",
                            Parameters = paramList.ToImmutable(),
                            ReturnType = new TypeAnnotation { Name = ReturnTypes[retIndices[i]] },
                            Body = ImmutableArray.Create<Statement>(
                                new ExpressionStatement { Expression = new EllipsisLiteral() })
                        });
                    }

                    return new InterfaceDef
                    {
                        Name = name,
                        Body = methods.ToImmutable()
                    };
                }));

    public static Gen<ClassDef> ImplementingClass(string className, InterfaceDef iface, bool complete) =>
        Gen.Const(BuildImplementingClass(className, iface, complete));

    public static Gen<Module> ModuleWithInterface(int methodCount, bool completeImpl) =>
        Gen.Int[1, 3].SelectMany(mc =>
            InterfaceDef("IShape", mc).Select(iface =>
            {
                var classDef = BuildImplementingClass("Circle", iface, completeImpl);
                var moduleBody = ImmutableArray.CreateBuilder<Statement>();
                moduleBody.Add(iface);
                moduleBody.Add(classDef);
                moduleBody.Add(BuildMainFunction(classDef));
                return new Module { Body = moduleBody.ToImmutable() };
            }));

    public static Gen<Module> ModuleWithMultipleInterfaces() =>
        Gen.Select(
            Gen.Int[1, 2],
            Gen.Int[1, 2],
            (mc1, mc2) =>
            {
                var iface1 = BuildInterfaceDef("IDrawable", mc1, "draw");
                var iface2 = BuildInterfaceDef("ISerializable", mc2, "serialize");
                var classDef = BuildClassImplementingMultiple("Widget", iface1, iface2);
                var moduleBody = ImmutableArray.CreateBuilder<Statement>();
                moduleBody.Add(iface1);
                moduleBody.Add(iface2);
                moduleBody.Add(classDef);
                moduleBody.Add(BuildMainFunction(classDef));
                return new Module { Body = moduleBody.ToImmutable() };
            });

    public static Gen<Module> ModuleWithInterfaceHierarchy() =>
        Gen.Int[1, 2].Select(methodCount =>
        {
            var baseIface = BuildInterfaceDef("IBase", methodCount, "base_op");
            var derivedIface = new InterfaceDef
            {
                Name = "IDerived",
                BaseInterfaces = ImmutableArray.Create(new TypeAnnotation { Name = "IBase" }),
                Body = ImmutableArray.Create<Statement>(new FunctionDef
                {
                    Name = "derived_op",
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = ImmutableArray.Create<Statement>(
                        new ExpressionStatement { Expression = new EllipsisLiteral() })
                })
            };

            var implMethods = ImmutableArray.CreateBuilder<Statement>();
            AddConstructor(implMethods);
            foreach (var stmt in baseIface.Body)
            {
                if (stmt is FunctionDef fd)
                    implMethods.Add(BuildImplementation(fd));
            }
            implMethods.Add(BuildImplementation((FunctionDef)derivedIface.Body[0]));

            var classDef = new ClassDef
            {
                Name = "Impl",
                BaseClasses = ImmutableArray.Create(new TypeAnnotation { Name = "IDerived" }),
                Body = implMethods.ToImmutable()
            };

            var moduleBody = ImmutableArray.CreateBuilder<Statement>();
            moduleBody.Add(baseIface);
            moduleBody.Add(derivedIface);
            moduleBody.Add(classDef);
            moduleBody.Add(BuildMainFunction(classDef));
            return new Module { Body = moduleBody.ToImmutable() };
        });

    public static Gen<Module> ModuleWithProtocolDunder(string dunderName) =>
        Gen.Const(BuildProtocolModule(dunderName));

    private static Module BuildProtocolModule(string dunderName)
    {
        var (returnType, body) = dunderName switch
        {
            "__len__" => ("int", (Expression)new IntegerLiteral { Value = "5" }),
            "__bool__" => ("bool", (Expression)new BooleanLiteral { Value = true }),
            "__reversed__" => ("list[int]", (Expression)new ListLiteral
            {
                Elements = ImmutableArray.Create<Expression>(new IntegerLiteral { Value = "1" })
            }),
            _ => ("int", (Expression)new IntegerLiteral { Value = "0" })
        };

        var methods = ImmutableArray.CreateBuilder<Statement>();
        AddConstructor(methods);
        methods.Add(new FunctionDef
        {
            Name = dunderName,
            Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
            ReturnType = new TypeAnnotation
            {
                Name = returnType.Contains('[') ? returnType.Split('[')[0] : returnType,
                TypeArguments = returnType.Contains('[')
                    ? ImmutableArray.Create(new TypeAnnotation { Name = returnType.Split('[', ']')[1] })
                    : ImmutableArray<TypeAnnotation>.Empty
            },
            Body = ImmutableArray.Create<Statement>(new ReturnStatement { Value = body })
        });

        var classDef = new ClassDef
        {
            Name = "Container",
            Body = methods.ToImmutable()
        };

        var mainBody = ImmutableArray.Create<Statement>(
            new VariableDeclaration
            {
                Name = "c",
                InitialValue = new FunctionCall
                {
                    Function = new Identifier { Name = "Container" },
                    Arguments = ImmutableArray<Expression>.Empty
                }
            },
            new ExpressionStatement
            {
                Expression = new FunctionCall
                {
                    Function = new Identifier { Name = "print" },
                    Arguments = ImmutableArray.Create<Expression>(
                        new FunctionCall
                        {
                            Function = new Identifier { Name = dunderName == "__bool__" ? "bool" : "len" },
                            Arguments = ImmutableArray.Create<Expression>(new Identifier { Name = "c" })
                        })
                }
            });

        var moduleBody = ImmutableArray.CreateBuilder<Statement>();
        moduleBody.Add(classDef);
        moduleBody.Add(new FunctionDef { Name = "main", Body = mainBody });
        return new Module { Body = moduleBody.ToImmutable() };
    }

    private static InterfaceDef BuildInterfaceDef(string name, int methodCount, string methodPrefix)
    {
        var methods = ImmutableArray.CreateBuilder<Statement>();
        for (int i = 0; i < methodCount; i++)
        {
            var paramList = ImmutableArray.CreateBuilder<Parameter>();
            paramList.Add(new Parameter { Name = "self" });
            if (i > 0)
            {
                paramList.Add(new Parameter
                {
                    Name = "arg0",
                    Type = new TypeAnnotation { Name = "int" }
                });
            }

            methods.Add(new FunctionDef
            {
                Name = $"{methodPrefix}_{i}",
                Parameters = paramList.ToImmutable(),
                ReturnType = new TypeAnnotation { Name = ReturnTypes[i % ReturnTypes.Length] },
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement { Expression = new EllipsisLiteral() })
            });
        }

        return new InterfaceDef
        {
            Name = name,
            Body = methods.ToImmutable()
        };
    }

    private static ClassDef BuildImplementingClass(string className, InterfaceDef iface, bool complete)
    {
        var body = ImmutableArray.CreateBuilder<Statement>();
        AddConstructor(body);

        var methods = iface.Body.OfType<FunctionDef>().ToList();
        var methodsToImplement = complete ? methods : methods.Skip(1).ToList();

        foreach (var method in methodsToImplement)
        {
            body.Add(BuildImplementation(method));
        }

        return new ClassDef
        {
            Name = className,
            BaseClasses = ImmutableArray.Create(new TypeAnnotation { Name = iface.Name }),
            Body = body.ToImmutable()
        };
    }

    private static ClassDef BuildClassImplementingMultiple(string className, InterfaceDef iface1, InterfaceDef iface2)
    {
        var body = ImmutableArray.CreateBuilder<Statement>();
        AddConstructor(body);

        foreach (var stmt in iface1.Body)
        {
            if (stmt is FunctionDef fd)
                body.Add(BuildImplementation(fd));
        }
        foreach (var stmt in iface2.Body)
        {
            if (stmt is FunctionDef fd)
                body.Add(BuildImplementation(fd));
        }

        return new ClassDef
        {
            Name = className,
            BaseClasses = ImmutableArray.Create(
                new TypeAnnotation { Name = iface1.Name },
                new TypeAnnotation { Name = iface2.Name }),
            Body = body.ToImmutable()
        };
    }

    private static FunctionDef BuildImplementation(FunctionDef interfaceMethod)
    {
        Expression returnExpr = interfaceMethod.ReturnType?.Name switch
        {
            "int" => new IntegerLiteral { Value = "42" },
            "str" => new StringLiteral { Value = "result" },
            "bool" => new BooleanLiteral { Value = true },
            "list" => new ListLiteral
            {
                Elements = ImmutableArray.Create<Expression>(new IntegerLiteral { Value = "1" })
            },
            _ => new IntegerLiteral { Value = "0" }
        };

        return new FunctionDef
        {
            Name = interfaceMethod.Name,
            Parameters = interfaceMethod.Parameters,
            ReturnType = interfaceMethod.ReturnType,
            Body = ImmutableArray.Create<Statement>(new ReturnStatement { Value = returnExpr })
        };
    }

    private static void AddConstructor(ImmutableArray<Statement>.Builder body)
    {
        body.Add(new FunctionDef
        {
            Name = "__init__",
            Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
            Body = ImmutableArray.Create<Statement>(new PassStatement())
        });
    }

    private static FunctionDef BuildMainFunction(ClassDef classDef)
    {
        var mainBody = ImmutableArray.CreateBuilder<Statement>();
        mainBody.Add(new VariableDeclaration
        {
            Name = "obj",
            InitialValue = new FunctionCall
            {
                Function = new Identifier { Name = classDef.Name },
                Arguments = ImmutableArray<Expression>.Empty
            }
        });
        mainBody.Add(new ExpressionStatement
        {
            Expression = new FunctionCall
            {
                Function = new Identifier { Name = "print" },
                Arguments = ImmutableArray.Create<Expression>(new Identifier { Name = "obj" })
            }
        });

        return new FunctionDef
        {
            Name = "main",
            Body = mainBody.ToImmutable()
        };
    }
}
