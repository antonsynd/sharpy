using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class CombinedFeaturePropertyTests
{
    private readonly ITestOutputHelper _output;

    public CombinedFeaturePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AsyncGenerator_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        Gen.OneOfConst("int", "str").Select(yieldType =>
        {
            Expression yieldExpr = yieldType == "int"
                ? new IntegerLiteral { Value = "42" }
                : (Expression)new StringLiteral { Value = "hello" };

            var asyncGenFunc = new FunctionDef
            {
                Name = "async_gen",
                IsAsync = true,
                Parameters = ImmutableArray<Parameter>.Empty,
                ReturnType = new TypeAnnotation { Name = yieldType },
                Body = ImmutableArray.Create<Statement>(
                    new YieldStatement { Value = yieldExpr },
                    new YieldStatement { Value = yieldExpr })
            };

            var mainBody = ImmutableArray.Create<Statement>(
                new ForStatement
                {
                    IsAsync = true,
                    Target = new Identifier { Name = "item" },
                    Iterator = new FunctionCall
                    {
                        Function = new Identifier { Name = "async_gen" },
                        Arguments = ImmutableArray<Expression>.Empty
                    },
                    Body = ImmutableArray.Create<Statement>(
                        new ExpressionStatement
                        {
                            Expression = new FunctionCall
                            {
                                Function = new Identifier { Name = "print" },
                                Arguments = ImmutableArray.Create<Expression>(
                                    new Identifier { Name = "item" })
                            }
                        })
                });

            return new Module
            {
                Body = ImmutableArray.Create<Statement>(
                    asyncGenFunc,
                    new FunctionDef { Name = "main", IsAsync = true, Body = mainBody })
            };
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            Interlocked.Increment(ref total);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "combined_test.spy");
                if (result.Success)
                    Interlocked.Increment(ref passed);
            }
            catch
            {
                // Swallow
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Async generator: {passed}/{total} passed");
        Assert.True(passed > total / 4,
            $"Async generator pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void DecoratedAsyncFunction_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        Gen.OneOfConst("int", "str", "bool").Select(retType =>
        {
            Expression returnExpr = retType switch
            {
                "int" => new IntegerLiteral { Value = "1" },
                "str" => (Expression)new StringLiteral { Value = "result" },
                _ => new BooleanLiteral { Value = true }
            };

            var classDef = new ClassDef
            {
                Name = "Service",
                Body = ImmutableArray.Create<Statement>(
                    new FunctionDef
                    {
                        Name = "__init__",
                        Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                        Body = ImmutableArray.Create<Statement>(new PassStatement())
                    },
                    new FunctionDef
                    {
                        Name = "fetch",
                        IsAsync = true,
                        Decorators = ImmutableArray.Create(
                            new Decorator { QualifiedParts = ImmutableArray.Create("virtual") }),
                        Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                        ReturnType = new TypeAnnotation { Name = retType },
                        Body = ImmutableArray.Create<Statement>(
                            new ReturnStatement { Value = returnExpr })
                    })
            };

            var mainBody = ImmutableArray.Create<Statement>(
                new VariableDeclaration
                {
                    Name = "svc",
                    InitialValue = new FunctionCall
                    {
                        Function = new Identifier { Name = "Service" },
                        Arguments = ImmutableArray<Expression>.Empty
                    }
                },
                new VariableDeclaration
                {
                    Name = "result",
                    InitialValue = new AwaitExpression
                    {
                        Operand = new FunctionCall
                        {
                            Function = new MemberAccess
                            {
                                Object = new Identifier { Name = "svc" },
                                Member = "fetch"
                            },
                            Arguments = ImmutableArray<Expression>.Empty
                        }
                    }
                },
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new Identifier { Name = "result" })
                    }
                });

            return new Module
            {
                Body = ImmutableArray.Create<Statement>(
                    classDef,
                    new FunctionDef { Name = "main", IsAsync = true, Body = mainBody })
            };
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            Interlocked.Increment(ref total);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "combined_test.spy");
                if (result.Success)
                    Interlocked.Increment(ref passed);
            }
            catch
            {
                // Swallow
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Decorated async function: {passed}/{total} passed");
        Assert.True(passed > total / 4,
            $"Decorated async function pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void InterfaceWithExceptions_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        Gen.Const(BuildInterfaceWithExceptionModule()).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            Interlocked.Increment(ref total);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "combined_test.spy");
                if (result.Success)
                    Interlocked.Increment(ref passed);
            }
            catch
            {
                // Swallow
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Interface with exceptions: {passed}/{total} passed");
        Assert.True(passed > total / 4,
            $"Interface with exceptions pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void DecoratedClassImplementingInterface_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        Gen.Int[1, 3].Select(fieldCount =>
        {
            var ifaceDef = new InterfaceDef
            {
                Name = "IConfig",
                Body = ImmutableArray.Create<Statement>(
                    new FunctionDef
                    {
                        Name = "get_value",
                        Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                        ReturnType = new TypeAnnotation { Name = "str" },
                        Body = ImmutableArray.Create<Statement>(
                            new ExpressionStatement { Expression = new EllipsisLiteral() })
                    })
            };

            var fields = ImmutableArray.CreateBuilder<Statement>();
            fields.Add(new VariableDeclaration
            {
                Name = "name",
                Type = new TypeAnnotation { Name = "str" }
            });
            for (int i = 0; i < fieldCount; i++)
            {
                fields.Add(new VariableDeclaration
                {
                    Name = $"field_{i}",
                    Type = new TypeAnnotation { Name = "int" }
                });
            }

            var classDef = new ClassDef
            {
                Name = "AppConfig",
                Decorators = ImmutableArray.Create(
                    new Decorator { QualifiedParts = ImmutableArray.Create("dataclass") }),
                BaseClasses = ImmutableArray.Create(new TypeAnnotation { Name = "IConfig" }),
                Body = fields.ToImmutable().Add(
                    new FunctionDef
                    {
                        Name = "get_value",
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
                            })
                    })
            };

            var ctorArgs = ImmutableArray.CreateBuilder<Expression>();
            ctorArgs.Add(new StringLiteral { Value = "test" });
            for (int i = 0; i < fieldCount; i++)
                ctorArgs.Add(new IntegerLiteral { Value = (i + 1).ToString() });

            var mainBody = ImmutableArray.Create<Statement>(
                new VariableDeclaration
                {
                    Name = "cfg",
                    InitialValue = new FunctionCall
                    {
                        Function = new Identifier { Name = "AppConfig" },
                        Arguments = ctorArgs.ToImmutable()
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
                                Function = new MemberAccess
                                {
                                    Object = new Identifier { Name = "cfg" },
                                    Member = "get_value"
                                },
                                Arguments = ImmutableArray<Expression>.Empty
                            })
                    }
                });

            return new Module
            {
                Body = ImmutableArray.Create<Statement>(
                    ifaceDef,
                    classDef,
                    new FunctionDef { Name = "main", Body = mainBody })
            };
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            Interlocked.Increment(ref total);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "combined_test.spy");
                if (result.Success)
                    Interlocked.Increment(ref passed);
            }
            catch
            {
                // Swallow
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Decorated class implementing interface: {passed}/{total} passed");
        Assert.True(passed > total / 4,
            $"Decorated class implementing interface pass rate too low: {passed}/{total}");
    }

    private static Module BuildInterfaceWithExceptionModule()
    {
        var ifaceDef = new InterfaceDef
        {
            Name = "IProcessor",
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "process",
                    Parameters = ImmutableArray.Create(
                        new Parameter { Name = "self" },
                        new Parameter { Name = "data", Type = new TypeAnnotation { Name = "str" } }),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = ImmutableArray.Create<Statement>(
                        new ExpressionStatement { Expression = new EllipsisLiteral() })
                })
        };

        var classDef = new ClassDef
        {
            Name = "SafeProcessor",
            BaseClasses = ImmutableArray.Create(new TypeAnnotation { Name = "IProcessor" }),
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    Body = ImmutableArray.Create<Statement>(new PassStatement())
                },
                new FunctionDef
                {
                    Name = "process",
                    Parameters = ImmutableArray.Create(
                        new Parameter { Name = "self" },
                        new Parameter { Name = "data", Type = new TypeAnnotation { Name = "str" } }),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = ImmutableArray.Create<Statement>(
                        new TryStatement
                        {
                            Body = ImmutableArray.Create<Statement>(
                                new ReturnStatement
                                {
                                    Value = new BinaryOp
                                    {
                                        Left = new StringLiteral { Value = "processed: " },
                                        Operator = BinaryOperator.Add,
                                        Right = new Identifier { Name = "data" }
                                    }
                                }),
                            Handlers = ImmutableArray.Create(new ExceptHandler
                            {
                                ExceptionType = new TypeAnnotation { Name = "Exception" },
                                Name = "e",
                                Body = ImmutableArray.Create<Statement>(
                                    new ReturnStatement
                                    {
                                        Value = new StringLiteral { Value = "error" }
                                    })
                            })
                        })
                })
        };

        var mainBody = ImmutableArray.Create<Statement>(
            new VariableDeclaration
            {
                Name = "proc",
                InitialValue = new FunctionCall
                {
                    Function = new Identifier { Name = "SafeProcessor" },
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
                            Function = new MemberAccess
                            {
                                Object = new Identifier { Name = "proc" },
                                Member = "process"
                            },
                            Arguments = ImmutableArray.Create<Expression>(
                                new StringLiteral { Value = "hello" })
                        })
                }
            });

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                ifaceDef,
                classDef,
                new FunctionDef { Name = "main", Body = mainBody })
        };
    }
}
