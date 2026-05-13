using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenTyped
{
    public static Gen<Expression> ExpressionOfType(TypeEnv env, string targetType, int fuel)
    {
        if (fuel <= 0)
            return LeafOfType(env, targetType);

        return Gen.Frequency(
            (3, LeafOfType(env, targetType)),
            (2, CompositeOfType(env, targetType, fuel - 1)));
    }

    private static Gen<Expression> LeafOfType(TypeEnv env, string targetType)
    {
        var vars = env.VarsOfType(targetType);
        var varGen = vars.Count > 0
            ? Gen.OneOfConst(vars.ToArray()).Select(n => (Expression)new Identifier { Name = n })
            : null;

        return targetType switch
        {
            "int" => varGen != null
                ? Gen.OneOf(GenLiterals.Integer.Select(x => (Expression)x), varGen)
                : GenLiterals.Integer.Select(x => (Expression)x),
            "str" => varGen != null
                ? Gen.OneOf(GenLiterals.SimpleString.Select(x => (Expression)x), varGen)
                : GenLiterals.SimpleString.Select(x => (Expression)x),
            "bool" => varGen != null
                ? Gen.OneOf(GenLiterals.Boolean.Select(x => (Expression)x), varGen)
                : GenLiterals.Boolean.Select(x => (Expression)x),
            "float" => varGen != null
                ? Gen.OneOf(GenLiterals.Float.Select(x => (Expression)x), varGen)
                : GenLiterals.Float.Select(x => (Expression)x),
            "list[int]" => varGen != null
                ? Gen.OneOf(ListLiteralGen("int"), varGen)
                : ListLiteralGen("int"),
            "list[str]" => varGen != null
                ? Gen.OneOf(ListLiteralGen("str"), varGen)
                : ListLiteralGen("str"),
            "int?" or "str?" => OptionalLeaf(env, targetType, varGen),
            _ => GenLiterals.AnyLiteral
        };
    }

    private static Gen<Expression> CompositeOfType(TypeEnv env, string targetType, int fuel)
    {
        return targetType switch
        {
            "int" => Gen.OneOf<Expression>(
                IntArithmetic(env, fuel),
                LenExpression(env, fuel),
                AbsExpression(env, fuel),
                ConditionalOfType(env, "int", fuel)),
            "str" => Gen.OneOf<Expression>(
                StringConcat(env, fuel),
                StringMethodCall(env, fuel),
                ConditionalOfType(env, "str", fuel)),
            "bool" => HasOptionalVars(env)
                ? Gen.OneOf<Expression>(
                    BooleanLogic(env, fuel),
                    IsNoneCheck(env),
                    ConditionalOfType(env, "bool", fuel))
                : Gen.OneOf<Expression>(
                    BooleanLogic(env, fuel),
                    ConditionalOfType(env, "bool", fuel)),
            "float" => Gen.OneOf<Expression>(
                FloatArithmetic(env, fuel),
                ConditionalOfType(env, "float", fuel)),
            "list[int]" => Gen.OneOf<Expression>(
                ListLiteralGen("int", env, fuel),
                ConditionalOfType(env, "list[int]", fuel)),
            "list[str]" => Gen.OneOf<Expression>(
                ListLiteralGen("str", env, fuel),
                ConditionalOfType(env, "list[str]", fuel)),
            _ => ConditionalOfType(env, targetType, fuel)
        };
    }

    private static Gen<Expression> IntArithmetic(TypeEnv env, int fuel) =>
        Gen.Select(
            ExpressionOfType(env, "int", fuel),
            ExpressionOfType(env, "int", fuel),
            Gen.OneOfConst(BinaryOperator.Add, BinaryOperator.Subtract, BinaryOperator.Multiply),
            (left, right, op) => (Expression)new BinaryOp { Left = left, Right = right, Operator = op });

    private static Gen<Expression> FloatArithmetic(TypeEnv env, int fuel) =>
        Gen.Select(
            ExpressionOfType(env, "float", fuel),
            ExpressionOfType(env, "float", fuel),
            Gen.OneOfConst(BinaryOperator.Add, BinaryOperator.Subtract, BinaryOperator.Multiply),
            (left, right, op) => (Expression)new BinaryOp { Left = left, Right = right, Operator = op });

    private static Gen<Expression> StringConcat(TypeEnv env, int fuel) =>
        Gen.Select(
            ExpressionOfType(env, "str", fuel),
            ExpressionOfType(env, "str", fuel),
            (left, right) => (Expression)new BinaryOp
            {
                Left = left,
                Right = right,
                Operator = BinaryOperator.Add
            });

    private static Gen<Expression> BooleanLogic(TypeEnv env, int fuel) =>
        Gen.OneOf(
            Gen.Select(
                ExpressionOfType(env, "bool", fuel),
                ExpressionOfType(env, "bool", fuel),
                Gen.OneOfConst(BinaryOperator.And, BinaryOperator.Or),
                (left, right, op) => (Expression)new BinaryOp { Left = left, Right = right, Operator = op }),
            ComparisonExpression(env, fuel));

    private static Gen<Expression> ComparisonExpression(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.OneOfConst("int", "float").SelectMany(t => ExpressionOfType(env, t, fuel)),
            Gen.OneOfConst("int", "float").SelectMany(t => ExpressionOfType(env, t, fuel)),
            Gen.OneOfConst(
                BinaryOperator.Equal, BinaryOperator.NotEqual,
                BinaryOperator.LessThan, BinaryOperator.LessThanOrEqual,
                BinaryOperator.GreaterThan, BinaryOperator.GreaterThanOrEqual),
            (left, right, op) => (Expression)new BinaryOp { Left = left, Right = right, Operator = op });

    private static Gen<Expression> ConditionalOfType(TypeEnv env, string targetType, int fuel) =>
        Gen.Select(
            ExpressionOfType(env, "bool", fuel),
            ExpressionOfType(env, targetType, fuel),
            ExpressionOfType(env, targetType, fuel),
            (test, then, els) => (Expression)new ConditionalExpression
            {
                Test = test,
                ThenValue = then,
                ElseValue = els
            });

    public static Gen<Module> TypedProgram(TypeEnv env, string resultType, int fuel) =>
        TypedProgram(env, resultType, fuel, withStatements: false);

    public static Gen<Module> TypedProgram(TypeEnv env, string resultType, int fuel, bool withStatements) =>
        Gen.Select(
            ExpressionOfType(env, resultType, fuel),
            withStatements && fuel > 0
                ? Gen.Int[0, 2].SelectMany(n =>
                    Enumerable.Range(0, n)
                        .Select(_ => StatementOfType(env, fuel - 1))
                        .Aggregate(
                            Gen.Const(ImmutableArray<Statement>.Empty),
                            (acc, gen) => Gen.Select(acc, gen, (arr, s) => arr.Add(s))))
                : Gen.Const(ImmutableArray<Statement>.Empty),
            (expr, stmts) =>
            {
                var printCall = new FunctionCall
                {
                    Function = new Identifier { Name = "print" },
                    Arguments = ImmutableArray.Create<Expression>(expr)
                };

                var body = ImmutableArray.CreateBuilder<Statement>();
                foreach (var binding in env.Bindings)
                {
                    body.Add(new VariableDeclaration
                    {
                        Name = binding.Key,
                        Type = TypeAnnotationForType(binding.Value),
                        InitialValue = DefaultValueForType(binding.Value)
                    });
                }
                body.AddRange(stmts);
                body.Add(new ExpressionStatement { Expression = printCall });

                return new Module
                {
                    Body = ImmutableArray.Create<Statement>(
                        new FunctionDef
                        {
                            Name = "main",
                            Body = body.ToImmutable()
                        })
                };
            });

    public static Gen<Statement> StatementOfType(TypeEnv env, int fuel) =>
        Gen.OneOf(
            IfElseStatement(env, fuel),
            ForRangeStatement(env, fuel),
            AssignmentStatement(env, fuel));

    public static Gen<Statement> IfElseStatement(TypeEnv env, int fuel) =>
        Gen.Select(
            ExpressionOfType(env, "bool", fuel),
            AssignmentStatement(env, fuel),
            AssignmentStatement(env, fuel),
            (test, thenStmt, elseStmt) => (Statement)new IfStatement
            {
                Test = test,
                ThenBody = ImmutableArray.Create(thenStmt),
                ElseBody = ImmutableArray.Create(elseStmt)
            });

    public static Gen<Statement> ForRangeStatement(TypeEnv env, int fuel) =>
        Gen.Int[0, 5].SelectMany(rangeMax =>
            AssignmentStatement(env, fuel).Select(bodyStmt =>
                (Statement)new ForStatement
                {
                    Target = new Identifier { Name = "i" },
                    Iterator = new FunctionCall
                    {
                        Function = new Identifier { Name = "range" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new IntegerLiteral { Value = rangeMax.ToString() })
                    },
                    Body = ImmutableArray.Create(bodyStmt)
                }));

    private static Gen<Statement> AssignmentStatement(TypeEnv env, int fuel)
    {
        var mutableVars = env.Bindings
            .Where(kv => kv.Value is "int" or "str" or "bool" or "float")
            .ToArray();

        if (mutableVars.Length == 0)
        {
            return Gen.Const((Statement)new ExpressionStatement
            {
                Expression = new FunctionCall
                {
                    Function = new Identifier { Name = "print" },
                    Arguments = ImmutableArray.Create<Expression>(new IntegerLiteral { Value = "0" })
                }
            });
        }

        return Gen.OneOfConst(mutableVars).SelectMany(kv =>
            ExpressionOfType(env, kv.Value, fuel).Select(expr =>
                (Statement)new VariableDeclaration
                {
                    Name = kv.Key,
                    InitialValue = expr
                }));
    }

    private static Gen<Expression> LenExpression(TypeEnv env, int fuel)
    {
        var listVars = env.VarsOfType("list[int]")
            .Concat(env.VarsOfType("list[str]"))
            .ToList();
        if (listVars.Count > 0)
        {
            return Gen.OneOfConst(listVars.ToArray()).Select(n =>
                (Expression)new FunctionCall
                {
                    Function = new Identifier { Name = "len" },
                    Arguments = ImmutableArray.Create<Expression>(new Identifier { Name = n })
                });
        }
        return ExpressionOfType(env, "int", fuel).Select(inner =>
            (Expression)new FunctionCall
            {
                Function = new Identifier { Name = "len" },
                Arguments = ImmutableArray.Create<Expression>(
                    new ListLiteral
                    {
                        Elements = ImmutableArray.Create<Expression>(inner)
                    })
            });
    }

    public static Gen<Expression> ListOfType(TypeEnv env, string elementType, int fuel) =>
        ExpressionOfType(env, elementType, fuel).Array[0, 3].Select(elems =>
            (Expression)new ListLiteral { Elements = elems.ToImmutableArray() });

    private static Gen<Expression> OptionalLeaf(TypeEnv env, string targetType, Gen<Expression>? varGen)
    {
        var innerType = targetType.TrimEnd('?');
        var noneGen = Gen.Const((Expression)new FunctionCall
        {
            Function = new Identifier { Name = "None" },
            Arguments = ImmutableArray<Expression>.Empty
        });
        var valueGen = LeafOfType(env, innerType);
        var combined = Gen.OneOf(noneGen, valueGen);
        return varGen != null ? Gen.OneOf(combined, varGen) : combined;
    }

    private static bool HasOptionalVars(TypeEnv env) =>
        env.Bindings.Any(kv => kv.Value.EndsWith("?"));

    private static Gen<Expression> IsNoneCheck(TypeEnv env)
    {
        var optionalVars = env.Bindings
            .Where(kv => kv.Value.EndsWith("?"))
            .Select(kv => kv.Key)
            .ToArray();
        return Gen.Select(
            Gen.OneOfConst(optionalVars),
            Gen.OneOfConst(ComparisonOperator.Is, ComparisonOperator.IsNot),
            (name, op) => (Expression)new ComparisonChain
            {
                Operands = ImmutableArray.Create<Expression>(
                    new Identifier { Name = name },
                    new NoneLiteral()),
                Operators = ImmutableArray.Create(op)
            });
    }

    private static Gen<Expression> StringMethodCall(TypeEnv env, int fuel) =>
        Gen.Select(
            ExpressionOfType(env, "str", fuel),
            Gen.OneOfConst("upper", "lower", "strip"),
            (obj, method) => (Expression)new FunctionCall
            {
                Function = new MemberAccess { Object = obj, Member = method },
                Arguments = ImmutableArray<Expression>.Empty
            });

    private static Gen<Expression> AbsExpression(TypeEnv env, int fuel) =>
        ExpressionOfType(env, "int", fuel).Select(inner =>
            (Expression)new FunctionCall
            {
                Function = new Identifier { Name = "abs" },
                Arguments = ImmutableArray.Create<Expression>(inner)
            });

    private static Gen<Expression> ListLiteralGen(string elementType) =>
        Gen.Int[0, 3].Select(len =>
        {
            var elements = Enumerable.Range(0, len)
                .Select(_ => DefaultValueForType(elementType))
                .ToImmutableArray();
            return (Expression)new ListLiteral { Elements = elements };
        });

    private static Gen<Expression> ListLiteralGen(string elementType, TypeEnv env, int fuel) =>
        ExpressionOfType(env, elementType, fuel).Array[0, 3].Select(elems =>
            (Expression)new ListLiteral { Elements = elems.ToImmutableArray() });

    private static Expression DefaultValueForType(string type) => type switch
    {
        "int" => new IntegerLiteral { Value = "0" },
        "str" => new StringLiteral { Value = "" },
        "bool" => new BooleanLiteral { Value = false },
        "float" => new FloatLiteral { Value = "0.0" },
        "list[int]" => new ListLiteral { Elements = ImmutableArray<Expression>.Empty },
        "list[str]" => new ListLiteral { Elements = ImmutableArray<Expression>.Empty },
        "int?" or "str?" => new FunctionCall
        {
            Function = new Identifier { Name = "None" },
            Arguments = ImmutableArray<Expression>.Empty
        },
        _ => new IntegerLiteral { Value = "0" }
    };

    private static TypeAnnotation TypeAnnotationForType(string type) => type switch
    {
        "list[int]" => new TypeAnnotation
        {
            Name = "list",
            TypeArguments = ImmutableArray.Create(new TypeAnnotation { Name = "int" })
        },
        "list[str]" => new TypeAnnotation
        {
            Name = "list",
            TypeArguments = ImmutableArray.Create(new TypeAnnotation { Name = "str" })
        },
        "int?" => new TypeAnnotation { Name = "int", IsOptional = true },
        "str?" => new TypeAnnotation { Name = "str", IsOptional = true },
        _ => new TypeAnnotation { Name = type }
    };
}
