using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenGenerics
{
    private static readonly string[] ConcreteTypes = { "int", "str", "bool" };

    public static Gen<string> GenericClassProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.Int[1, 2],
            Gen.OneOfConst(ConcreteTypes),
            (fieldCount, instantiationType) =>
            {
                var lines = new List<string>
                {
                    "class Box[T]:",
                    "    value: T",
                    "",
                    "    def __init__(self, value: T):",
                    "        self.value = value",
                    "",
                    "    def get(self) -> T:",
                    "        return self.value",
                    ""
                };

                if (fieldCount > 1)
                {
                    lines.InsertRange(1, new[]
                    {
                        "    label: str",
                    });
                    lines[4] = "    def __init__(self, value: T, label: str):";
                    lines.Insert(5, "        self.label = label");
                }

                lines.Add("def main():");
                var ctorArgs = fieldCount > 1
                    ? $"{DefaultLiteral(instantiationType)}, \"item\""
                    : DefaultLiteral(instantiationType);
                lines.Add($"    b = Box[{instantiationType}]({ctorArgs})");
                lines.Add("    print(b.get())");

                return string.Join("\n", lines) + "\n";
            });

    public static Gen<string> GenericFunctionProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.OneOfConst(ConcreteTypes),
            Gen.OneOfConst(ConcreteTypes),
            (type1, type2) =>
            {
                var lines = new List<string>
                {
                    "def identity[T](x: T) -> T:",
                    "    return x",
                    "",
                    "def main():",
                    $"    a: {type1} = identity[{type1}]({DefaultLiteral(type1)})",
                    "    print(a)",
                    $"    b: {type2} = identity[{type2}]({DefaultLiteral(type2)})",
                    "    print(b)"
                };

                return string.Join("\n", lines) + "\n";
            });

    public static Gen<string> MultiTypeParamProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.OneOfConst(ConcreteTypes),
            Gen.OneOfConst(ConcreteTypes),
            (typeA, typeB) =>
            {
                var lines = new List<string>
                {
                    "class Pair[A, B]:",
                    "    first: A",
                    "    second: B",
                    "",
                    "    def __init__(self, first: A, second: B):",
                    "        self.first = first",
                    "        self.second = second",
                    "",
                    "    def get_first(self) -> A:",
                    "        return self.first",
                    "",
                    "    def get_second(self) -> B:",
                    "        return self.second",
                    "",
                    "def main():",
                    $"    p = Pair[{typeA}, {typeB}]({DefaultLiteral(typeA)}, {DefaultLiteral(typeB)})",
                    "    print(p.get_first())",
                    "    print(p.get_second())"
                };

                return string.Join("\n", lines) + "\n";
            });

    public static Gen<string> GenericWithInheritanceProgram(TypeEnv env, int fuel) =>
        Gen.OneOfConst(ConcreteTypes).Select(concreteType =>
        {
            var lines = new List<string>
            {
                "class Container[T]:",
                "    value: T",
                "",
                "    def __init__(self, value: T):",
                "        self.value = value",
                "",
                "    @virtual",
                "    def describe(self) -> str:",
                "        return \"container\"",
                "",
                "class NamedContainer[T](Container[T]):",
                "    name: str",
                "",
                "    def __init__(self, value: T, name: str):",
                "        super().__init__(value)",
                "        self.name = name",
                "",
                "    @override",
                "    def describe(self) -> str:",
                "        return self.name",
                "",
                "def main():",
                $"    nc = NamedContainer[{concreteType}]({DefaultLiteral(concreteType)}, \"test\")",
                "    print(nc.describe())",
                "    print(nc.value)"
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> WrongTypeArgCountProgram(TypeEnv env, int fuel) =>
        Gen.OneOfConst(ConcreteTypes).Select(concreteType =>
        {
            var lines = new List<string>
            {
                "class Box[T]:",
                "    value: T",
                "",
                "    def __init__(self, value: T):",
                "        self.value = value",
                "",
                "def main():",
                $"    b = Box[{concreteType}, str]({DefaultLiteral(concreteType)})"
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> TypeMismatchOnGenericFieldProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.OneOfConst(ConcreteTypes),
            Gen.OneOfConst(ConcreteTypes).Where(t => t != "int"),
            (instantType, wrongType) =>
            {
                var lines = new List<string>
                {
                    "class Box[T]:",
                    "    value: T",
                    "",
                    "    def __init__(self, value: T):",
                    "        self.value = value",
                    "",
                    "def main():",
                    $"    b = Box[int](42)",
                    $"    b.value = {DefaultLiteral(wrongType)}"
                };

                return string.Join("\n", lines) + "\n";
            });

    private static string DefaultLiteral(string type) => type switch
    {
        "int" => "42",
        "str" => "\"hello\"",
        "bool" => "True",
        "float" => "3.14",
        _ => "0"
    };
}
