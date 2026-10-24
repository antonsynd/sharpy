using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The valid base programs the mutation lanes break (#2324, P63b): each is a typed generator member called
/// with the arguments its acceptance property samples it with, so the injected lane and the generated lane
/// read the SAME programs. A source-text member is parsed (<see cref="AstRewriter.Parse"/>); every lane
/// normalizes its sample before it enumerates sites. Shared by <c>ErrorCompletenessPropertyTests</c>' lanes
/// and the census, so the census measures the programs the lanes judge.
/// </summary>
internal static class MutationBases
{
    /// <summary><c>GenTyped.TypedProgram</c> with statements over the default env (the roster's <c>TypedStatements</c>).</summary>
    public static Gen<Module> TypedStatements() =>
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2, withStatements: true));

    /// <summary><c>GenTyped.TypedProgram</c> with statements over the collections env.</summary>
    public static Gen<Module> TypedCollections() =>
        Gen.OneOfConst("int", "str", "list[int]").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithCollections, type, fuel: 2, withStatements: true));

    /// <summary><c>GenTyped.TypedProgram</c> over the Optionals env (no statements).</summary>
    public static Gen<Module> TypedOptionals() =>
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel: 2));

    /// <summary><c>GenFunctions.ModuleWithFunctions</c> at fuel 2 (the roster's <c>Functions</c>).</summary>
    public static Gen<Module> Functions() => GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 2);

    /// <summary><c>GenClasses.ModuleWithClasses</c> at fuel 2 (the roster's <c>Classes</c>).</summary>
    public static Gen<Module> Classes() => GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2);

    /// <summary><c>GenTyped.ListSubscript</c> printed from a <c>main</c> declaring the collections env.</summary>
    public static Gen<Module> ListSubscripts() =>
        Gen.OneOfConst("int", "str").SelectMany(element =>
            GenTyped.ListSubscript(TypeEnv.WithCollections, element).Select(expr => InMain(TypeEnv.WithCollections, expr)));

    public static Gen<Module> GenericFunctions() => GenGenerics.GenericFunctionProgram(TypeEnv.WithGenerics, fuel: 2);

    public static Gen<Module> GenericInheritance() => GenGenerics.GenericWithInheritanceProgram(TypeEnv.WithGenerics, fuel: 2);

    /// <summary>A source-text generator's samples, parsed.</summary>
    public static Gen<Module> Parsed(Gen<string> source) => source.Select(AstRewriter.Parse);

    /// <summary>A <c>main</c> declaring every binding of <paramref name="env"/> at its default value, then
    /// <c>print(fragment)</c>.</summary>
    private static Module InMain(TypeEnv env, Expression fragment)
    {
        var body = ImmutableArray.CreateBuilder<Statement>();
        foreach (var (name, type) in env.Bindings)
        {
            body.Add(new VariableDeclaration
            {
                Name = name,
                Type = Annotation(type),
                InitialValue = GenTyped.DefaultValue(type),
            });
        }
        body.Add(new ExpressionStatement
        {
            Expression = new FunctionCall
            {
                Function = new Identifier { Name = "print" },
                Arguments = ImmutableArray.Create(fragment),
            },
        });
        return new Module
        {
            Body = ImmutableArray.Create<Statement>(new FunctionDef { Name = "main", Body = body.ToImmutable() })
        };
    }

    private static TypeAnnotation Annotation(string type) => type switch
    {
        "list[int]" or "list[str]" => new TypeAnnotation
        {
            Name = "list",
            TypeArguments = ImmutableArray.Create(new TypeAnnotation { Name = type[5..^1] }),
        },
        _ when type.EndsWith('?') => new TypeAnnotation { Name = type[..^1], IsOptional = true },
        _ => new TypeAnnotation { Name = type },
    };
}
