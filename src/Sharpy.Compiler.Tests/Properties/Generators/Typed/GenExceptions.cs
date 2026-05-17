using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenExceptions
{
    private static readonly string[] ExceptionTypes =
        { "Exception", "ValueError", "TypeError", "KeyError", "IndexError", "RuntimeError" };

    public static Gen<Module> ModuleWithTryExcept(int handlerCount) =>
        Gen.Int[0, ExceptionTypes.Length - 1].Array[handlerCount, handlerCount]
            .Select(typeIndices => BuildTryExceptModule(typeIndices));

    public static Gen<Module> ModuleWithMultipleHandlers() =>
        Gen.Int[2, 4].SelectMany(count =>
            Gen.Int[0, ExceptionTypes.Length - 1].Array[count, count]
                .Select(typeIndices => BuildTryExceptModule(typeIndices)));

    public static Gen<Module> ModuleWithBareRaise(bool insideExcept) =>
        Gen.Const(BuildBareRaiseModule(insideExcept));

    public static Gen<Module> ModuleWithRaiseExpression() =>
        Gen.Int[0, ExceptionTypes.Length - 1].Select(typeIndex =>
            BuildRaiseModule(ExceptionTypes[typeIndex]));

    public static Gen<Module> ModuleWithNestedTry() =>
        Gen.Int[0, ExceptionTypes.Length - 1].Select(typeIndex =>
            BuildNestedTryModule(ExceptionTypes[typeIndex]));

    public static Gen<Module> ModuleWithExceptionHierarchy() =>
        Gen.Const(BuildHierarchyModule());

    private static Module BuildTryExceptModule(int[] typeIndices)
    {
        var handlers = ImmutableArray.CreateBuilder<ExceptHandler>();
        for (int i = 0; i < typeIndices.Length; i++)
        {
            handlers.Add(new ExceptHandler
            {
                ExceptionType = new TypeAnnotation { Name = ExceptionTypes[typeIndices[i]] },
                Name = $"e{i}",
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement
                    {
                        Expression = new FunctionCall
                        {
                            Function = new Identifier { Name = "print" },
                            Arguments = ImmutableArray.Create<Expression>(
                                new StringLiteral { Value = $"caught {ExceptionTypes[typeIndices[i]]}" })
                        }
                    })
            });
        }

        var tryStmt = new TryStatement
        {
            Body = ImmutableArray.Create<Statement>(
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new StringLiteral { Value = "try body" })
                    }
                }),
            Handlers = handlers.ToImmutable()
        };

        var mainBody = ImmutableArray.Create<Statement>(tryStmt);
        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef { Name = "main", Body = mainBody })
        };
    }

    private static Module BuildBareRaiseModule(bool insideExcept)
    {
        Statement raiseStmt = new RaiseStatement();

        ImmutableArray<Statement> mainBody;
        if (insideExcept)
        {
            mainBody = ImmutableArray.Create<Statement>(
                new TryStatement
                {
                    Body = ImmutableArray.Create<Statement>(
                        new ExpressionStatement
                        {
                            Expression = new FunctionCall
                            {
                                Function = new Identifier { Name = "print" },
                                Arguments = ImmutableArray.Create<Expression>(
                                    new StringLiteral { Value = "try" })
                            }
                        }),
                    Handlers = ImmutableArray.Create(new ExceptHandler
                    {
                        ExceptionType = new TypeAnnotation { Name = "Exception" },
                        Name = "e",
                        Body = ImmutableArray.Create<Statement>(raiseStmt)
                    })
                });
        }
        else
        {
            mainBody = ImmutableArray.Create<Statement>(raiseStmt);
        }

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef { Name = "main", Body = mainBody })
        };
    }

    private static Module BuildRaiseModule(string exceptionType)
    {
        var raiseStmt = new RaiseStatement
        {
            Exception = new FunctionCall
            {
                Function = new Identifier { Name = exceptionType },
                Arguments = ImmutableArray.Create<Expression>(
                    new StringLiteral { Value = "error occurred" })
            }
        };

        var tryStmt = new TryStatement
        {
            Body = ImmutableArray.Create<Statement>(raiseStmt),
            Handlers = ImmutableArray.Create(new ExceptHandler
            {
                ExceptionType = new TypeAnnotation { Name = exceptionType },
                Name = "e",
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement
                    {
                        Expression = new FunctionCall
                        {
                            Function = new Identifier { Name = "print" },
                            Arguments = ImmutableArray.Create<Expression>(
                                new StringLiteral { Value = "handled" })
                        }
                    })
            })
        };

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "main",
                    Body = ImmutableArray.Create<Statement>(tryStmt)
                })
        };
    }

    private static Module BuildNestedTryModule(string exceptionType)
    {
        var innerTry = new TryStatement
        {
            Body = ImmutableArray.Create<Statement>(
                new RaiseStatement
                {
                    Exception = new FunctionCall
                    {
                        Function = new Identifier { Name = exceptionType },
                        Arguments = ImmutableArray.Create<Expression>(
                            new StringLiteral { Value = "inner" })
                    }
                }),
            Handlers = ImmutableArray.Create(new ExceptHandler
            {
                ExceptionType = new TypeAnnotation { Name = exceptionType },
                Name = "inner_e",
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement
                    {
                        Expression = new FunctionCall
                        {
                            Function = new Identifier { Name = "print" },
                            Arguments = ImmutableArray.Create<Expression>(
                                new StringLiteral { Value = "inner caught" })
                        }
                    })
            })
        };

        var outerTry = new TryStatement
        {
            Body = ImmutableArray.Create<Statement>(innerTry),
            Handlers = ImmutableArray.Create(new ExceptHandler
            {
                ExceptionType = new TypeAnnotation { Name = "Exception" },
                Name = "outer_e",
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement
                    {
                        Expression = new FunctionCall
                        {
                            Function = new Identifier { Name = "print" },
                            Arguments = ImmutableArray.Create<Expression>(
                                new StringLiteral { Value = "outer caught" })
                        }
                    })
            })
        };

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "main",
                    Body = ImmutableArray.Create<Statement>(outerTry)
                })
        };
    }

    private static Module BuildHierarchyModule()
    {
        var tryStmt = new TryStatement
        {
            Body = ImmutableArray.Create<Statement>(
                new RaiseStatement
                {
                    Exception = new FunctionCall
                    {
                        Function = new Identifier { Name = "ValueError" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new StringLiteral { Value = "test" })
                    }
                }),
            Handlers = ImmutableArray.Create(
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "ValueError" },
                    Name = "e",
                    Body = ImmutableArray.Create<Statement>(
                        new ExpressionStatement
                        {
                            Expression = new FunctionCall
                            {
                                Function = new Identifier { Name = "print" },
                                Arguments = ImmutableArray.Create<Expression>(
                                    new StringLiteral { Value = "value error" })
                            }
                        })
                },
                new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Name = "e",
                    Body = ImmutableArray.Create<Statement>(
                        new ExpressionStatement
                        {
                            Expression = new FunctionCall
                            {
                                Function = new Identifier { Name = "print" },
                                Arguments = ImmutableArray.Create<Expression>(
                                    new StringLiteral { Value = "base exception" })
                            }
                        })
                })
        };

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "main",
                    Body = ImmutableArray.Create<Statement>(tryStmt)
                })
        };
    }
}
