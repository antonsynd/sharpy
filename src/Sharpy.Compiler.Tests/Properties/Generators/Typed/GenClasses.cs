using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenClasses
{
    private static readonly string[] FieldTypes = { "int", "str", "bool" };

    public static Gen<ClassDef> ClassDef(TypeEnv env, string className, ClassInfo info, int fuel) =>
        Gen.Const(BuildClassDef(className, info));

    public static Gen<(ClassDef Base, ClassDef Derived)> ClassHierarchy(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.Int[1, 2],
            Gen.Int[1, 2],
            Gen.OneOfConst(FieldTypes),
            (baseFieldCount, derivedFieldCount, methodRetType) =>
            {
                var baseName = "BaseEntity";
                var derivedName = "DerivedEntity";

                var baseFields = Enumerable.Range(0, baseFieldCount)
                    .Select(i => ($"field_{i}", FieldTypes[i % FieldTypes.Length]))
                    .ToImmutableArray();

                var derivedExtraFields = Enumerable.Range(0, derivedFieldCount)
                    .Select(i => ($"extra_{i}", FieldTypes[(i + 1) % FieldTypes.Length]))
                    .ToImmutableArray();

                var methodSig = new FunctionSignature(
                    ImmutableArray<(string, string)>.Empty, methodRetType);

                var baseInfo = new ClassInfo(null, baseFields,
                    ImmutableArray.Create(("get_info", methodSig)));
                var derivedInfo = new ClassInfo(baseName,
                    baseFields.AddRange(derivedExtraFields),
                    ImmutableArray.Create(("get_info", methodSig)));

                var baseDef = BuildClassDef(baseName, baseInfo, isVirtual: true);
                var derivedDef = BuildClassDef(derivedName, derivedInfo,
                    isOverride: true, baseFieldCount: baseFields.Length);

                return (baseDef, derivedDef);
            });

    public static Gen<Expression> ConstructorCall(TypeEnv env, string className, ClassInfo info) =>
        Gen.Const(MakeConstructorCall(className, info));

    public static Gen<Module> ModuleWithClasses(TypeEnv env, int fuel) =>
        ClassHierarchy(env, fuel).SelectMany(hierarchy =>
        {
            var (baseDef, derivedDef) = hierarchy;
            var derivedName = derivedDef.Name;
            var derivedInfo = env.Classes.ContainsKey(derivedName)
                ? env.Classes[derivedName]
                : ExtractClassInfo(derivedDef);

            return Gen.Const(BuildModule(env, baseDef, derivedDef, derivedName, derivedInfo));
        });

    private static ClassDef BuildClassDef(string name, ClassInfo info,
        bool isVirtual = false, bool isOverride = false, int baseFieldCount = 0)
    {
        var body = ImmutableArray.CreateBuilder<Statement>();

        foreach (var (fieldName, fieldType) in info.Fields)
        {
            body.Add(new VariableDeclaration
            {
                Name = fieldName,
                Type = TypeAnnotationFor(fieldType)
            });
        }

        body.Add(BuildConstructor(info, baseFieldCount));

        foreach (var (methodName, sig) in info.Methods)
        {
            body.Add(BuildMethod(methodName, sig, isVirtual, isOverride));
        }

        var baseClasses = info.BaseClass != null
            ? ImmutableArray.Create(new TypeAnnotation { Name = info.BaseClass })
            : ImmutableArray<TypeAnnotation>.Empty;

        return new ClassDef
        {
            Name = name,
            BaseClasses = baseClasses,
            Body = body.ToImmutable()
        };
    }

    private static FunctionDef BuildConstructor(ClassInfo info, int baseFieldCount = 0)
    {
        var parameters = ImmutableArray.CreateBuilder<Parameter>();
        parameters.Add(new Parameter { Name = "self" });
        foreach (var (fieldName, fieldType) in info.Fields)
        {
            parameters.Add(new Parameter
            {
                Name = fieldName,
                Type = TypeAnnotationFor(fieldType)
            });
        }

        var bodyStmts = ImmutableArray.CreateBuilder<Statement>();

        if (info.BaseClass != null && baseFieldCount > 0)
        {
            var baseArgs = info.Fields.Take(baseFieldCount)
                .Select(f => (Expression)new Identifier { Name = f.Name })
                .ToImmutableArray();
            bodyStmts.Add(new ExpressionStatement
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
                    Arguments = baseArgs
                }
            });
        }

        foreach (var (fieldName, _) in info.Fields.Skip(baseFieldCount))
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

        if (bodyStmts.Count == 0)
            bodyStmts.Add(new PassStatement());

        return new FunctionDef
        {
            Name = "__init__",
            Parameters = parameters.ToImmutable(),
            Body = bodyStmts.ToImmutable()
        };
    }

    private static FunctionDef BuildMethod(string name, FunctionSignature sig,
        bool isVirtual, bool isOverride)
    {
        var parameters = ImmutableArray.CreateBuilder<Parameter>();
        parameters.Add(new Parameter { Name = "self" });
        foreach (var (paramName, paramType) in sig.Parameters)
        {
            parameters.Add(new Parameter
            {
                Name = paramName,
                Type = TypeAnnotationFor(paramType)
            });
        }

        Expression returnExpr = sig.ReturnType switch
        {
            "int" => new IntegerLiteral { Value = "42" },
            "str" => new StringLiteral { Value = "result" },
            "bool" => new BooleanLiteral { Value = true },
            _ => new IntegerLiteral { Value = "0" }
        };

        var decorators = ImmutableArray.CreateBuilder<Decorator>();
        if (isVirtual)
            decorators.Add(new Decorator { QualifiedParts = ImmutableArray.Create("virtual") });
        if (isOverride)
            decorators.Add(new Decorator { QualifiedParts = ImmutableArray.Create("override") });

        return new FunctionDef
        {
            Name = name,
            Parameters = parameters.ToImmutable(),
            ReturnType = TypeAnnotationFor(sig.ReturnType),
            Body = ImmutableArray.Create<Statement>(new ReturnStatement { Value = returnExpr }),
            Decorators = decorators.ToImmutable()
        };
    }

    private static Expression MakeConstructorCall(string className, ClassInfo info)
    {
        var args = info.Fields.Select(f => (Expression)(f.Type switch
        {
            "int" => new IntegerLiteral { Value = "1" },
            "str" => new StringLiteral { Value = "test" },
            "bool" => new BooleanLiteral { Value = true },
            _ => new IntegerLiteral { Value = "0" }
        })).ToImmutableArray();

        return new FunctionCall
        {
            Function = new Identifier { Name = className },
            Arguments = args
        };
    }

    private static Module BuildModule(TypeEnv env, ClassDef baseDef, ClassDef derivedDef,
        string derivedName, ClassInfo derivedInfo)
    {
        var moduleBody = ImmutableArray.CreateBuilder<Statement>();
        moduleBody.Add(baseDef);
        moduleBody.Add(derivedDef);

        var mainBody = ImmutableArray.CreateBuilder<Statement>();

        mainBody.Add(new VariableDeclaration
        {
            Name = "obj",
            Type = TypeAnnotationFor(derivedName),
            InitialValue = MakeConstructorCall(derivedName, derivedInfo)
        });

        if (derivedInfo.Methods.Length > 0)
        {
            var (methodName, methodSig) = derivedInfo.Methods[0];
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
                                Object = new Identifier { Name = "obj" },
                                Member = methodName
                            },
                            Arguments = ImmutableArray<Expression>.Empty
                        })
                }
            });
        }

        moduleBody.Add(new FunctionDef
        {
            Name = "main",
            Body = mainBody.ToImmutable()
        });

        return new Module { Body = moduleBody.ToImmutable() };
    }

    private static ClassInfo ExtractClassInfo(ClassDef def) =>
        new(def.BaseClasses.Length > 0 ? def.BaseClasses[0].Name : null,
            def.Body.OfType<VariableDeclaration>()
                .Where(v => v.Type != null)
                .Select(v => (v.Name, v.Type!.Name))
                .ToImmutableArray(),
            def.Body.OfType<FunctionDef>()
                .Where(f => f.Name != "__init__")
                .Select(f => (f.Name, new FunctionSignature(
                    f.Parameters.Skip(1)
                        .Where(p => p.Type != null)
                        .Select(p => (p.Name, p.Type!.Name))
                        .ToImmutableArray(),
                    f.ReturnType?.Name ?? "int")))
                .ToImmutableArray());

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
        _ => new TypeAnnotation { Name = type }
    };
}
