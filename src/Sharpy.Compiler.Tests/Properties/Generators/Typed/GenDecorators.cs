using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenDecorators
{
    // Valid method-decorator corpus. Spellings must match DecoratorNames
    // (Sharpy.Compiler.Shared): Virtual = "virtual", Override = "override",
    // Final = "final". @final is only legal on an @override (DecoratorValidator
    // .ValidateFinalRequiresOverride emits SPY0412 otherwise), so the "final"
    // variant is generated as a real Base.@virtual -> Derived.@override @final
    // chain rather than a bare @final on a base-less method (#1031).
    private static readonly string[] ValidMethodDecorators =
        { "virtual", "final" };

    // DecoratorNames.Dataclass = "dataclass".
    private static readonly string[] ValidClassDecorators =
        { "dataclass" };

    // Invalid decorator *combinations*: conflicting access modifiers
    // (DecoratorNames.Public/Protected/Private/Internal). Each pair is rejected as
    // a combination by DecoratorValidator.ValidateAccessModifierDecorators with
    // ConflictingAccessModifiers (SPY0430) — a genuine two-decorator conflict.
    // Earlier this array used @staticmethod/@abstractmethod (an unsupported and an
    // unknown spelling) on a base-less method, so samples were diagnosed for the
    // wrong reason (unsupported-decorator / override-without-base), never as
    // combinations (#1031).
    private static readonly string[][] InvalidCombinations =
    {
        new[] { "public", "private" },
        new[] { "public", "protected" },
        new[] { "protected", "internal" }
    };

    public static Gen<Module> ModuleWithDecoratedFunction(bool validDecorator) =>
        validDecorator
            ? Gen.OneOfConst(ValidMethodDecorators).Select(BuildValidDecoratedModule)
            : Gen.Int[0, InvalidCombinations.Length - 1].Select(i =>
                BuildInvalidDecoratedModule(InvalidCombinations[i]));

    public static Gen<Module> ModuleWithDecoratorStack(bool valid) =>
        valid
            ? Gen.Const(BuildValidStackModule())
            : Gen.Int[0, InvalidCombinations.Length - 1].Select(i =>
                BuildInvalidStackModule(InvalidCombinations[i]));

    public static Gen<Module> ModuleWithDataclass() =>
        Gen.Int[1, 4].Select(BuildDataclassModule);

    public static Gen<Module> ModuleWithDecoratorDeterminism() =>
        Gen.OneOfConst(ValidMethodDecorators).Select(BuildValidDecoratedModule);

    private static Module BuildValidDecoratedModule(string decorator) =>
        // @final requires an @override chain; every other valid decorator sits on a
        // single base-less method. See ValidMethodDecorators / SPY0412 (#1031).
        decorator == "final"
            ? BuildFinalOverrideModule()
            : BuildSingleDecoratedModule(decorator);

    private static Module BuildSingleDecoratedModule(string decorator)
    {
        var moduleBody = ImmutableArray.CreateBuilder<Statement>();

        var baseDef = new ClassDef
        {
            Name = "Base",
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    Body = ImmutableArray.Create<Statement>(new PassStatement())
                },
                new FunctionDef
                {
                    Name = "action",
                    Decorators = ImmutableArray.Create(
                        new Decorator { QualifiedParts = ImmutableArray.Create(decorator) }),
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = new StringLiteral { Value = "base" } })
                })
        };
        moduleBody.Add(baseDef);
        moduleBody.Add(BuildMainWithClassCall("Base", "action"));

        return new Module { Body = moduleBody.ToImmutable() };
    }

    // Base.@virtual action  ->  Derived(Base).@override @final action.
    // This is the only legal home for @final: it requires an @override target
    // (DecoratorValidator.ValidateFinalRequiresOverride, SPY0412).
    private static Module BuildFinalOverrideModule()
    {
        var baseDef = new ClassDef
        {
            Name = "Base",
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    Body = ImmutableArray.Create<Statement>(new PassStatement())
                },
                new FunctionDef
                {
                    Name = "action",
                    Decorators = ImmutableArray.Create(
                        new Decorator { QualifiedParts = ImmutableArray.Create("virtual") }),
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = new StringLiteral { Value = "base" } })
                })
        };

        var derivedDef = new ClassDef
        {
            Name = "Derived",
            BaseClasses = ImmutableArray.Create(new TypeAnnotation { Name = "Base" }),
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "action",
                    Decorators = ImmutableArray.Create(
                        new Decorator { QualifiedParts = ImmutableArray.Create("override") },
                        new Decorator { QualifiedParts = ImmutableArray.Create("final") }),
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = new StringLiteral { Value = "derived" } })
                })
        };

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                baseDef,
                derivedDef,
                BuildMainWithClassCall("Derived", "action"))
        };
    }

    private static Module BuildInvalidDecoratedModule(string[] decorators)
    {
        var decoratorNodes = decorators.Select(d =>
            new Decorator { QualifiedParts = ImmutableArray.Create(d) })
            .ToImmutableArray();

        var classDef = new ClassDef
        {
            Name = "Broken",
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "__init__",
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    Body = ImmutableArray.Create<Statement>(new PassStatement())
                },
                new FunctionDef
                {
                    Name = "bad_method",
                    Decorators = decoratorNodes,
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    ReturnType = new TypeAnnotation { Name = "str" },
                    Body = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = new StringLiteral { Value = "oops" } })
                })
        };

        var moduleBody = ImmutableArray.Create<Statement>(
            classDef,
            new FunctionDef
            {
                Name = "main",
                Body = ImmutableArray.Create<Statement>(new PassStatement())
            });

        return new Module { Body = moduleBody };
    }

    private static Module BuildValidStackModule()
    {
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
                    Name = "compute",
                    Decorators = ImmutableArray.Create(
                        new Decorator { QualifiedParts = ImmutableArray.Create("virtual") }),
                    Parameters = ImmutableArray.Create(new Parameter { Name = "self" }),
                    ReturnType = new TypeAnnotation { Name = "int" },
                    Body = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = new IntegerLiteral { Value = "42" } })
                })
        };

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                classDef,
                BuildMainWithClassCall("Service", "compute"))
        };
    }

    private static Module BuildInvalidStackModule(string[] decorators)
    {
        return BuildInvalidDecoratedModule(decorators);
    }

    private static Module BuildDataclassModule(int fieldCount)
    {
        var fieldTypes = new[] { "int", "str", "bool", "int" };
        var fields = ImmutableArray.CreateBuilder<Statement>();
        for (int i = 0; i < fieldCount; i++)
        {
            fields.Add(new VariableDeclaration
            {
                Name = $"field_{i}",
                Type = new TypeAnnotation { Name = fieldTypes[i % fieldTypes.Length] }
            });
        }

        var classDef = new ClassDef
        {
            Name = "Config",
            Decorators = ImmutableArray.Create(
                new Decorator { QualifiedParts = ImmutableArray.Create("dataclass") }),
            Body = fields.ToImmutable()
        };

        var ctorArgs = Enumerable.Range(0, fieldCount)
            .Select(i => (Expression)(fieldTypes[i % fieldTypes.Length] switch
            {
                "int" => new IntegerLiteral { Value = (i + 1).ToString() },
                "str" => new StringLiteral { Value = $"val_{i}" },
                "bool" => new BooleanLiteral { Value = i % 2 == 0 },
                _ => new IntegerLiteral { Value = "0" }
            }))
            .ToImmutableArray();

        var mainBody = ImmutableArray.Create<Statement>(
            new VariableDeclaration
            {
                Name = "cfg",
                InitialValue = new FunctionCall
                {
                    Function = new Identifier { Name = "Config" },
                    Arguments = ctorArgs
                }
            },
            new ExpressionStatement
            {
                Expression = new FunctionCall
                {
                    Function = new Identifier { Name = "print" },
                    Arguments = ImmutableArray.Create<Expression>(new Identifier { Name = "cfg" })
                }
            });

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                classDef,
                new FunctionDef { Name = "main", Body = mainBody })
        };
    }

    private static FunctionDef BuildMainWithClassCall(string className, string methodName)
    {
        var mainBody = ImmutableArray.Create<Statement>(
            new VariableDeclaration
            {
                Name = "obj",
                InitialValue = new FunctionCall
                {
                    Function = new Identifier { Name = className },
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
                                Object = new Identifier { Name = "obj" },
                                Member = methodName
                            },
                            Arguments = ImmutableArray<Expression>.Empty
                        })
                }
            });

        return new FunctionDef { Name = "main", Body = mainBody };
    }
}
