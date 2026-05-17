using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenMatchPatterns
{
    private static readonly string[] GuardExprs =
    {
        "x > 0",
        "x < 100",
        "x != 0",
        "x >= 0 and x <= 50",
    };

    public static Gen<string> MatchWithGuards() =>
        Gen.Int[0, GuardExprs.Length - 1].Select(guardIdx =>
        {
            var guard = GuardExprs[guardIdx];
            var lines = new List<string>
            {
                "def classify(x: int) -> str:",
                "    match x:",
                $"        case int() if {guard}:",
                "            return \"matched\"",
                "        case _:",
                "            return \"default\"",
                "",
                "def main():",
                "    print(classify(5))",
                "    print(classify(-1))",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> MatchWithOrPatterns() =>
        Gen.Int[0, 3].Select(variant =>
        {
            var pattern = variant switch
            {
                0 => "case 1 | 2 | 3:",
                1 => "case 10 | 20:",
                2 => "case 0 | -1:",
                _ => "case 100 | 200 | 300:",
            };

            var lines = new List<string>
            {
                "def check(x: int) -> str:",
                "    match x:",
                $"        {pattern}",
                "            return \"found\"",
                "        case _:",
                "            return \"not found\"",
                "",
                "def main():",
                "    print(check(1))",
                "    print(check(99))",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> MatchWithTypePatterns() =>
        Gen.Int[0, 2].Select(variant =>
        {
            var lines = new List<string>
            {
                "class Animal:",
                "    name: str",
                "",
                "    def __init__(self, name: str):",
                "        self.name = name",
                "",
                "class Dog(Animal):",
                "    breed: str",
                "",
                "    def __init__(self, name: str, breed: str):",
                "        super().__init__(name)",
                "        self.breed = breed",
                "",
                "class Cat(Animal):",
                "    def __init__(self, name: str):",
                "        super().__init__(name)",
                "",
            };

            lines.Add(variant switch
            {
                0 => string.Join("\n", new[]
                {
                    "def describe(a: Animal) -> str:",
                    "    match a:",
                    "        case Dog():",
                    "            return \"dog\"",
                    "        case Cat():",
                    "            return \"cat\"",
                    "        case _:",
                    "            return \"animal\"",
                }),
                1 => string.Join("\n", new[]
                {
                    "def describe(a: Animal) -> str:",
                    "    match a:",
                    "        case Dog() if a.name == \"Rex\":",
                    "            return \"good boy\"",
                    "        case Dog():",
                    "            return \"dog\"",
                    "        case _:",
                    "            return \"other\"",
                }),
                _ => string.Join("\n", new[]
                {
                    "def describe(a: Animal) -> str:",
                    "    match a:",
                    "        case Cat():",
                    "            return \"cat\"",
                    "        case _:",
                    "            return \"not cat\"",
                }),
            });

            lines.Add("");
            lines.Add("def main():");
            lines.Add("    d = Dog(\"Rex\", \"Lab\")");
            lines.Add("    print(describe(d))");

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> MatchWithNestedPatterns() =>
        Gen.Int[0, 2].Select(variant =>
        {
            var lines = variant switch
            {
                0 => new List<string>
                {
                    "def check_pair(pair: tuple[int, int]) -> str:",
                    "    match pair:",
                    "        case (0, 0):",
                    "            return \"origin\"",
                    "        case (0, _):",
                    "            return \"y-axis\"",
                    "        case (_, 0):",
                    "            return \"x-axis\"",
                    "        case _:",
                    "            return \"other\"",
                    "",
                    "def main():",
                    "    print(check_pair((0, 0)))",
                    "    print(check_pair((1, 0)))",
                },
                1 => new List<string>
                {
                    "def check_triple(t: tuple[int, int, int]) -> str:",
                    "    match t:",
                    "        case (0, 0, 0):",
                    "            return \"zero\"",
                    "        case (_, _, 0):",
                    "            return \"flat\"",
                    "        case _:",
                    "            return \"3d\"",
                    "",
                    "def main():",
                    "    print(check_triple((0, 0, 0)))",
                    "    print(check_triple((1, 2, 0)))",
                },
                _ => new List<string>
                {
                    "def check_value(x: int) -> str:",
                    "    match x:",
                    "        case 1 | 2 | 3 if x > 1:",
                    "            return \"small positive\"",
                    "        case int() if x > 100:",
                    "            return \"large\"",
                    "        case _:",
                    "            return \"other\"",
                    "",
                    "def main():",
                    "    print(check_value(2))",
                    "    print(check_value(200))",
                },
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> MatchNonExhaustive() =>
        Gen.Int[0, 2].Select(variant =>
        {
            var lines = variant switch
            {
                0 => new List<string>
                {
                    "def check(x: int) -> str:",
                    "    match x:",
                    "        case 1:",
                    "            return \"one\"",
                    "        case 2:",
                    "            return \"two\"",
                },
                1 => new List<string>
                {
                    "def check(x: bool) -> str:",
                    "    match x:",
                    "        case True:",
                    "            return \"yes\"",
                },
                _ => new List<string>
                {
                    "class Shape:",
                    "    pass",
                    "",
                    "class Circle(Shape):",
                    "    pass",
                    "",
                    "def check(s: Shape) -> str:",
                    "    match s:",
                    "        case Circle():",
                    "            return \"circle\"",
                },
            };

            lines.Add("");
            lines.Add("def main():");
            lines.Add("    pass");

            return string.Join("\n", lines) + "\n";
        });
}
