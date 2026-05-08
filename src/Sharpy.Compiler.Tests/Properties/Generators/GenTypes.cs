using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenTypes
{
    private static readonly string[] SimpleTypeNames =
    {
        "int", "str", "bool", "float", "long", "double", "object"
    };

    private static readonly string[] GenericTypeNames =
    {
        "list", "set", "dict", "tuple"
    };

    public static Gen<TypeAnnotation> SimpleType { get; } =
        Gen.OneOfConst(SimpleTypeNames).Select(name =>
            new TypeAnnotation { Name = name });

    public static Gen<TypeAnnotation> Annotation(int fuel) =>
        fuel <= 0
            ? SimpleType
            : Gen.Frequency(
                (5, SimpleType),
                (2, GenericSingleArg(fuel - 1)),
                (1, OptionalType(fuel - 1)),
                (1, DictType(fuel - 1)));

    private static Gen<TypeAnnotation> GenericSingleArg(int fuel) =>
        Gen.Select(Gen.OneOfConst("list", "set"), Annotation(fuel),
            (name, arg) => new TypeAnnotation
            {
                Name = name,
                TypeArguments = ImmutableArray.Create(arg)
            });

    private static Gen<TypeAnnotation> DictType(int fuel) =>
        Gen.Select(Annotation(fuel), Annotation(fuel),
            (k, v) => new TypeAnnotation
            {
                Name = "dict",
                TypeArguments = ImmutableArray.Create(k, v)
            });

    private static Gen<TypeAnnotation> OptionalType(int fuel) =>
        Annotation(fuel).Select(inner => new TypeAnnotation
        {
            Name = inner.Name,
            TypeArguments = inner.TypeArguments,
            IsOptional = true
        });
}
