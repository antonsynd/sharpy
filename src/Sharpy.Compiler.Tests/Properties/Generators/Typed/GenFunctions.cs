using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenFunctions
{
    public static Gen<FunctionDef> FunctionDef(TypeEnv env, string returnType, int fuel) =>
        Gen.Select(
            GenIdentifier.FunctionName,
            Gen.Int[0, 3].SelectMany(paramCount =>
                Enumerable.Range(0, paramCount)
                    .Select(i => ParameterGen(i))
                    .Aggregate(
                        Gen.Const(ImmutableArray<(string Name, string Type)>.Empty),
                        (acc, gen) => Gen.Select(acc, gen, (arr, p) => arr.Add(p)))),
            (name, parameters) =>
            {
                var paramEnv = new TypeEnv();
                foreach (var (pName, pType) in parameters)
                    paramEnv = paramEnv.WithBinding(pName, pType);

                return (name, parameters, paramEnv);
            })
        .SelectMany(tuple =>
            BodyForReturnType(tuple.paramEnv, returnType, fuel).Select(body =>
                new FunctionDef
                {
                    Name = tuple.name,
                    Parameters = tuple.parameters.Select(p => new Parameter
                    {
                        Name = p.Name,
                        Type = TypeAnnotationFor(p.Type)
                    }).ToImmutableArray(),
                    ReturnType = TypeAnnotationFor(returnType),
                    Body = body
                }));

    public static Gen<Expression> FunctionCall(TypeEnv env, string targetReturnType, int fuel)
    {
        var candidates = env.FunctionsReturning(targetReturnType);
        if (candidates.Count == 0)
            return GenTyped.ExpressionOfType(env, targetReturnType, fuel);

        return Gen.OneOfConst(candidates.ToArray()).SelectMany(funcName =>
        {
            var sig = env.Functions[funcName];
            return sig.Parameters.Length == 0
                ? Gen.Const(MakeCall(funcName, ImmutableArray<Expression>.Empty))
                : sig.Parameters
                    .Select(p => GenTyped.ExpressionOfType(env, p.Type, fuel))
                    .Aggregate(
                        Gen.Const(ImmutableArray<Expression>.Empty),
                        (acc, gen) => Gen.Select(acc, gen, (arr, e) => arr.Add(e)))
                    .Select(args => MakeCall(funcName, args));
        });
    }

    public static Gen<Module> ModuleWithFunctions(TypeEnv env, string resultType, int fuel)
    {
        var funcCount = Gen.Int[1, 3];
        var types = new[] { "int", "str", "bool" };
        var funcNamePrefixes = new[] { "compute", "process", "transform" };

        return funcCount.SelectMany(count =>
            Gen.Int[0, types.Length - 1].Array[count, count].SelectMany(typeIndices =>
            {
                var funcDefs = new List<Gen<(string Name, FunctionDef Def, FunctionSignature Sig)>>();
                for (int i = 0; i < count; i++)
                {
                    var retType = types[typeIndices[i]];
                    var fixedName = funcNamePrefixes[i];
                    funcDefs.Add(FunctionDefWithName(env, retType, fixedName, fuel).Select(fd =>
                        (fd.Name, fd,
                         new FunctionSignature(
                             fd.Parameters.Select(p => (p.Name, p.Type!.Name)).ToImmutableArray(),
                             retType))));
                }

                return funcDefs.Aggregate(
                    Gen.Const(ImmutableArray<(string Name, FunctionDef Def, FunctionSignature Sig)>.Empty),
                    (acc, gen) => Gen.Select(acc, gen, (arr, f) => arr.Add(f)))
                    .SelectMany(funcs =>
                    {
                        var callEnv = env;
                        foreach (var (name, _, sig) in funcs)
                            callEnv = callEnv.WithFunction(name, sig);

                        var callableFuncsForResult = callEnv.FunctionsReturning(resultType);
                        Gen<Expression> resultExpr = callableFuncsForResult.Count > 0
                            ? FunctionCall(callEnv, resultType, fuel)
                            : GenTyped.ExpressionOfType(callEnv, resultType, fuel);

                        return resultExpr.Select(expr =>
                        {
                            var moduleBody = ImmutableArray.CreateBuilder<Statement>();
                            foreach (var (_, def, _) in funcs)
                                moduleBody.Add(def);

                            var mainBody = ImmutableArray.CreateBuilder<Statement>();
                            foreach (var binding in env.Bindings)
                            {
                                mainBody.Add(new VariableDeclaration
                                {
                                    Name = binding.Key,
                                    Type = TypeAnnotationFor(binding.Value),
                                    InitialValue = GenTyped.DefaultValue(binding.Value)
                                });
                            }
                            mainBody.Add(new ExpressionStatement
                            {
                                Expression = new FunctionCall
                                {
                                    Function = new Identifier { Name = "print" },
                                    Arguments = ImmutableArray.Create<Expression>(expr)
                                }
                            });

                            moduleBody.Add(new FunctionDef
                            {
                                Name = "main",
                                Body = mainBody.ToImmutable()
                            });

                            return new Module { Body = moduleBody.ToImmutable() };
                        });
                    });
            }));
    }

    private static Gen<FunctionDef> FunctionDefWithName(TypeEnv env, string returnType, string name, int fuel) =>
        Gen.Int[0, 2].SelectMany(paramCount =>
            Enumerable.Range(0, paramCount)
                .Select(i => ParameterGen(i))
                .Aggregate(
                    Gen.Const(ImmutableArray<(string Name, string Type)>.Empty),
                    (acc, gen) => Gen.Select(acc, gen, (arr, p) => arr.Add(p))))
        .SelectMany(parameters =>
        {
            var paramEnv = new TypeEnv();
            foreach (var (pName, pType) in parameters)
                paramEnv = paramEnv.WithBinding(pName, pType);

            return BodyForReturnType(paramEnv, returnType, fuel).Select(body =>
                new FunctionDef
                {
                    Name = name,
                    Parameters = parameters.Select(p => new Parameter
                    {
                        Name = p.Name,
                        Type = TypeAnnotationFor(p.Type)
                    }).ToImmutableArray(),
                    ReturnType = TypeAnnotationFor(returnType),
                    Body = body
                });
        });

    private static Gen<ImmutableArray<Statement>> BodyForReturnType(TypeEnv env, string returnType, int fuel) =>
        GenTyped.ExpressionOfType(env, returnType, fuel).Select(expr =>
            ImmutableArray.Create<Statement>(new ReturnStatement { Value = expr }));

    private static Gen<(string Name, string Type)> ParameterGen(int index)
    {
        var paramNames = new[] { "arg0", "arg1", "arg2" };
        var paramTypes = new[] { "int", "str", "bool" };
        var name = index < paramNames.Length ? paramNames[index] : $"arg{index}";
        return Gen.OneOfConst(paramTypes).Select(t => (name, t));
    }

    private static Expression MakeCall(string funcName, ImmutableArray<Expression> args) =>
        new FunctionCall
        {
            Function = new Identifier { Name = funcName },
            Arguments = args
        };

    private static TypeAnnotation TypeAnnotationFor(string type) => type switch
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
