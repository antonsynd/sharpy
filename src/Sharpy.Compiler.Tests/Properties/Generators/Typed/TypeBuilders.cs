namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class TypeBuilders
{
    public static readonly string[] SupportedTypes =
    {
        "int", "str", "bool", "float"
    };

    public static readonly string[] SupportedTypesWithCollections =
    {
        "int", "str", "bool", "float", "list[int]", "list[str]"
    };

    public static readonly string[] SupportedTypesWithOptionals =
    {
        "int", "str", "bool", "float", "int?", "str?"
    };
}
