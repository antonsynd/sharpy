using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenIterators
{
    private static readonly string[] YieldTypes = { "int", "str", "bool" };

    public static Gen<Module> ModuleWithGenerator(bool valid) =>
        Gen.Select(
            Gen.OneOfConst(YieldTypes),
            Gen.Int[1, 3],
            (yieldType, yieldCount) => BuildGeneratorModule(yieldType, yieldCount, valid));

    public static Gen<Module> ModuleWithYieldFrom() =>
        Gen.OneOfConst(YieldTypes).Select(yieldType =>
        {
            var innerGen = BuildGeneratorFunctionDef("inner_gen", yieldType, 2);
            var outerGen = new FunctionDef
            {
                Name = "outer_gen",
                Parameters = ImmutableArray<Parameter>.Empty,
                ReturnType = new TypeAnnotation { Name = yieldType },
                Body = ImmutableArray.Create<Statement>(
                    new YieldStatement
                    {
                        Value = new FunctionCall
                        {
                            Function = new Identifier { Name = "inner_gen" },
                            Arguments = ImmutableArray<Expression>.Empty
                        },
                        IsFrom = true
                    })
            };

            var moduleBody = ImmutableArray.CreateBuilder<Statement>();
            moduleBody.Add(innerGen);
            moduleBody.Add(outerGen);
            moduleBody.Add(BuildIteratorMain("outer_gen"));
            return new Module { Body = moduleBody.ToImmutable() };
        });

    public static Gen<Module> ModuleWithIterProtocol(bool complete) =>
        Gen.OneOfConst(YieldTypes).Select(yieldType =>
            BuildIterProtocolModule(yieldType, complete));

    public static Gen<Module> ModuleWithGeneratorValidator() =>
        Gen.Select(
            Gen.OneOfConst(YieldTypes),
            Gen.Int[1, 3],
            (yieldType, yieldCount) => BuildGeneratorModule(yieldType, yieldCount, valid: true));

    private static Module BuildGeneratorModule(string yieldType, int yieldCount, bool valid)
    {
        var genFunc = valid
            ? BuildGeneratorFunctionDef("gen_values", yieldType, yieldCount)
            : BuildInvalidGeneratorDef(yieldType);

        var moduleBody = ImmutableArray.CreateBuilder<Statement>();
        moduleBody.Add(genFunc);
        moduleBody.Add(BuildIteratorMain("gen_values"));
        return new Module { Body = moduleBody.ToImmutable() };
    }

    private static FunctionDef BuildGeneratorFunctionDef(string name, string yieldType, int yieldCount)
    {
        var body = ImmutableArray.CreateBuilder<Statement>();
        for (int i = 0; i < yieldCount; i++)
        {
            Expression yieldExpr = yieldType switch
            {
                "int" => new IntegerLiteral { Value = (i + 1).ToString() },
                "str" => new StringLiteral { Value = $"item_{i}" },
                "bool" => new BooleanLiteral { Value = i % 2 == 0 },
                _ => new IntegerLiteral { Value = "0" }
            };
            body.Add(new YieldStatement { Value = yieldExpr });
        }

        return new FunctionDef
        {
            Name = name,
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = new TypeAnnotation { Name = yieldType },
            Body = body.ToImmutable()
        };
    }

    private static FunctionDef BuildInvalidGeneratorDef(string yieldType)
    {
        Expression yieldExpr = yieldType switch
        {
            "int" => new IntegerLiteral { Value = "1" },
            "str" => new StringLiteral { Value = "x" },
            _ => new BooleanLiteral { Value = true }
        };

        return new FunctionDef
        {
            Name = "gen_values",
            Parameters = ImmutableArray<Parameter>.Empty,
            ReturnType = new TypeAnnotation { Name = yieldType },
            Body = ImmutableArray.Create<Statement>(
                new YieldStatement { Value = yieldExpr },
                new ReturnStatement { Value = new IntegerLiteral { Value = "42" } })
        };
    }

    /// <summary>
    /// A hand-written iterator class (<c>__next__</c> bounded by <c>raise StopIteration()</c>) that
    /// <c>main</c> iterates with <c>for v in c</c>. <paramref name="complete"/> adds
    /// <c>def __iter__(self) -> Self: return self</c> — the pairing <c>dunder_methods.md</c> names —
    /// so the complete program analyses clean (and runs, printing the element twice); the incomplete
    /// one differs in that ONE method and draws SPY0320 (not iterable) at the loop. The cursor field
    /// is declared at class level (<c>classes.md</c>: every instance field is) so the class itself is
    /// never the refused fact (#2288 G3: the old program drew SPY0203 for an undeclared
    /// <c>self.index</c>, and its <c>__iter__</c> had no return annotation, SPY0260).
    /// </summary>
    private static Module BuildIterProtocolModule(string yieldType, bool complete)
    {
        static MemberAccess SelfIndex() => new()
        {
            Object = new Identifier { Name = "self" },
            Member = "index"
        };

        var methods = ImmutableArray.CreateBuilder<Statement>();
        methods.Add(new VariableDeclaration
        {
            Name = "index",
            Type = new TypeAnnotation { Name = "int" }
        });
        methods.Add(new FunctionDef
        {
            Name = "__init__",
            Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
            Body = ImmutableArray.Create<Statement>(
                new Assignment
                {
                    Target = SelfIndex(),
                    Value = new IntegerLiteral { Value = "0" }
                })
        });

        methods.Add(new FunctionDef
        {
            Name = "__next__",
            Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
            ReturnType = new TypeAnnotation { Name = yieldType },
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = new BinaryOp
                    {
                        Operator = BinaryOperator.GreaterThanOrEqual,
                        Left = SelfIndex(),
                        Right = new IntegerLiteral { Value = "2" }
                    },
                    ThenBody = ImmutableArray.Create<Statement>(
                        new RaiseStatement
                        {
                            Exception = new FunctionCall
                            {
                                Function = new Identifier { Name = "StopIteration" },
                                Arguments = ImmutableArray<Expression>.Empty
                            }
                        })
                },
                new Assignment
                {
                    Target = SelfIndex(),
                    Operator = AssignmentOperator.PlusAssign,
                    Value = new IntegerLiteral { Value = "1" }
                },
                new ReturnStatement
                {
                    Value = yieldType switch
                    {
                        "int" => new IntegerLiteral { Value = "1" },
                        "str" => (Expression)new StringLiteral { Value = "item" },
                        _ => new BooleanLiteral { Value = true }
                    }
                })
        });

        if (complete)
        {
            methods.Add(new FunctionDef
            {
                Name = "__iter__",
                Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                ReturnType = new TypeAnnotation { Name = "Self" },
                Body = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = new Identifier { Name = "self" } })
            });
        }

        var classDef = new ClassDef
        {
            Name = "Counter",
            Body = methods.ToImmutable()
        };

        var mainBody = ImmutableArray.Create<Statement>(
            new VariableDeclaration
            {
                Name = "c",
                InitialValue = new FunctionCall
                {
                    Function = new Identifier { Name = "Counter" },
                    Arguments = ImmutableArray<Expression>.Empty
                }
            },
            new ForStatement
            {
                Target = new Identifier { Name = "v" },
                Iterator = new Identifier { Name = "c" },
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement
                    {
                        Expression = new FunctionCall
                        {
                            Function = new Identifier { Name = "print" },
                            Arguments = ImmutableArray.Create<Expression>(new Identifier { Name = "v" })
                        }
                    })
            });

        var moduleBody = ImmutableArray.CreateBuilder<Statement>();
        moduleBody.Add(classDef);
        moduleBody.Add(new FunctionDef { Name = "main", Body = mainBody });
        return new Module { Body = moduleBody.ToImmutable() };
    }

    private static FunctionDef BuildIteratorMain(string genFuncName)
    {
        var mainBody = ImmutableArray.Create<Statement>(
            new ForStatement
            {
                Target = new Identifier { Name = "item" },
                Iterator = new FunctionCall
                {
                    Function = new Identifier { Name = genFuncName },
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

        return new FunctionDef { Name = "main", Body = mainBody };
    }
}
