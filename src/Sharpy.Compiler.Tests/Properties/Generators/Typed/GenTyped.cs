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
                ConditionalOfType(env, "int", fuel)),
            "str" => Gen.OneOf<Expression>(
                StringConcat(env, fuel),
                ConditionalOfType(env, "str", fuel)),
            "bool" => Gen.OneOf<Expression>(
                BooleanLogic(env, fuel),
                ConditionalOfType(env, "bool", fuel)),
            "float" => Gen.OneOf<Expression>(
                FloatArithmetic(env, fuel),
                ConditionalOfType(env, "float", fuel)),
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
        ExpressionOfType(env, resultType, fuel).Select(expr =>
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
                    Type = new TypeAnnotation { Name = binding.Value },
                    InitialValue = DefaultValueForType(binding.Value)
                });
            }
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

    private static Expression DefaultValueForType(string type) => type switch
    {
        "int" => new IntegerLiteral { Value = "0" },
        "str" => new StringLiteral { Value = "" },
        "bool" => new BooleanLiteral { Value = false },
        "float" => new FloatLiteral { Value = "0.0" },
        _ => new IntegerLiteral { Value = "0" }
    };
}
