using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenAsync
{
    private static readonly string[] ReturnTypes = { "int", "str", "bool" };

    public static Gen<Module> ModuleWithAsyncFunction(bool valid) =>
        Gen.Select(
            Gen.OneOfConst(ReturnTypes),
            Gen.Int[0, 2],
            (retType, paramCount) => BuildAsyncModule(retType, paramCount, valid));

    public static Gen<Module> ModuleWithAwaitOutsideAsync() =>
        Gen.OneOfConst(ReturnTypes).Select(retType =>
        {
            var asyncFunc = BuildAsyncFunctionDef("fetch_data", retType, 0);
            var syncFunc = new FunctionDef
            {
                Name = "process",
                IsAsync = false,
                Parameters = ImmutableArray<Parameter>.Empty,
                ReturnType = new TypeAnnotation { Name = retType },
                Body = ImmutableArray.Create<Statement>(
                    new ReturnStatement
                    {
                        Value = new AwaitExpression
                        {
                            Operand = new FunctionCall
                            {
                                Function = new Identifier { Name = "fetch_data" },
                                Arguments = ImmutableArray<Expression>.Empty
                            }
                        }
                    })
            };

            var moduleBody = ImmutableArray.CreateBuilder<Statement>();
            moduleBody.Add(asyncFunc);
            moduleBody.Add(syncFunc);
            moduleBody.Add(BuildAsyncMain("process"));
            return new Module { Body = moduleBody.ToImmutable() };
        });

    public static Gen<Module> ModuleWithAsyncDeterminism() =>
        Gen.Select(
            Gen.OneOfConst(ReturnTypes),
            Gen.Int[0, 2],
            (retType, paramCount) => BuildAsyncModule(retType, paramCount, valid: true));

    private static Module BuildAsyncModule(string retType, int paramCount, bool valid)
    {
        var asyncFunc = BuildAsyncFunctionDef("fetch_data", retType, paramCount);

        var callerBody = ImmutableArray.CreateBuilder<Statement>();
        var callArgs = Enumerable.Range(0, paramCount)
            .Select(i => (Expression)DefaultValueFor(i))
            .ToImmutableArray();

        if (valid)
        {
            callerBody.Add(new VariableDeclaration
            {
                Name = "result",
                InitialValue = new AwaitExpression
                {
                    Operand = new FunctionCall
                    {
                        Function = new Identifier { Name = "fetch_data" },
                        Arguments = callArgs
                    }
                }
            });
            callerBody.Add(new ExpressionStatement
            {
                Expression = new FunctionCall
                {
                    Function = new Identifier { Name = "print" },
                    Arguments = ImmutableArray.Create<Expression>(new Identifier { Name = "result" })
                }
            });
        }
        else
        {
            callerBody.Add(new ReturnStatement
            {
                Value = new AwaitExpression
                {
                    Operand = new StringLiteral { Value = "not_awaitable" }
                }
            });
        }

        var caller = new FunctionDef
        {
            Name = "run",
            IsAsync = true,
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = callerBody.ToImmutable()
        };

        var moduleBody = ImmutableArray.CreateBuilder<Statement>();
        moduleBody.Add(asyncFunc);
        moduleBody.Add(caller);
        moduleBody.Add(BuildAsyncMain("run"));
        return new Module { Body = moduleBody.ToImmutable() };
    }

    private static FunctionDef BuildAsyncFunctionDef(string name, string retType, int paramCount)
    {
        var parameters = ImmutableArray.CreateBuilder<Parameter>();
        for (int i = 0; i < paramCount; i++)
        {
            parameters.Add(new Parameter
            {
                Name = $"arg{i}",
                Type = new TypeAnnotation { Name = ReturnTypes[i % ReturnTypes.Length] }
            });
        }

        Expression returnExpr = retType switch
        {
            "int" => new IntegerLiteral { Value = "42" },
            "str" => new StringLiteral { Value = "hello" },
            "bool" => new BooleanLiteral { Value = true },
            _ => new IntegerLiteral { Value = "0" }
        };

        return new FunctionDef
        {
            Name = name,
            IsAsync = true,
            Parameters = parameters.ToImmutable(),
            ReturnType = new TypeAnnotation { Name = retType },
            Body = ImmutableArray.Create<Statement>(new ReturnStatement { Value = returnExpr })
        };
    }

    private static FunctionDef BuildAsyncMain(string funcToCall)
    {
        var mainBody = ImmutableArray.Create<Statement>(
            new ExpressionStatement
            {
                Expression = new AwaitExpression
                {
                    Operand = new FunctionCall
                    {
                        Function = new Identifier { Name = funcToCall },
                        Arguments = ImmutableArray<Expression>.Empty
                    }
                }
            });

        return new FunctionDef
        {
            Name = "main",
            IsAsync = true,
            Body = mainBody
        };
    }

    private static Expression DefaultValueFor(int index) => (index % 3) switch
    {
        0 => new IntegerLiteral { Value = "1" },
        1 => new StringLiteral { Value = "test" },
        _ => new BooleanLiteral { Value = true }
    };
}
