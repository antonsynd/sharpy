using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenFStrings
{
    private static readonly string[] FormatSpecs = { "d", ".2f", ".1f", ">10", "<10" };
    private static readonly string[] BuiltinFunctions = { "len", "str", "int" };

    public static Gen<string> FStringWithMethodCalls() =>
        Gen.Int[1, 3].Select(callCount =>
        {
            var lines = new List<string>
            {
                "class Item:",
                "    name: str",
                "    value: int",
                "",
                "    def __init__(self, name: str, value: int):",
                "        self.name = name",
                "        self.value = value",
                "",
                "    def display(self) -> str:",
                "        return self.name",
                "",
                "    def score(self) -> int:",
                "        return self.value",
                "",
                "def main():",
                "    item = Item(\"widget\", 42)",
            };

            if (callCount >= 1)
                lines.Add("    print(f\"{item.display()}\")");
            if (callCount >= 2)
                lines.Add("    print(f\"score: {item.score()}\")");
            if (callCount >= 3)
                lines.Add("    print(f\"{item.display()} = {item.score()}\")");

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> FStringWithNestedCalls() =>
        Gen.Int[0, BuiltinFunctions.Length - 1].Select(funcIdx =>
        {
            var func = BuiltinFunctions[funcIdx];
            var (setup, expr) = func switch
            {
                "len" => ("    items: list[int] = [1, 2, 3]", "len(items)"),
                "str" => ("    n: int = 42", "str(n)"),
                _ => ("    s: str = \"123\"", "int(s)"),
            };

            var lines = new List<string>
            {
                "def main():",
                setup,
                $"    print(f\"result: {{{expr}}}\")",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> FStringWithFormatSpecs() =>
        Gen.Int[0, FormatSpecs.Length - 1].Select(specIdx =>
        {
            var spec = FormatSpecs[specIdx];
            var (varDecl, varName) = spec.Contains("f")
                ? ("    x: float = 3.14159", "x")
                : ("    x: int = 42", "x");

            var lines = new List<string>
            {
                "def main():",
                varDecl,
                $"    print(f\"{{{varName}:{spec}}}\")",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> FStringWithArithmetic() =>
        Gen.Int[0, 3].Select(variant =>
        {
            var expr = variant switch
            {
                0 => "x + y",
                1 => "x * y",
                2 => "x - y",
                _ => "x + y * 2",
            };

            var lines = new List<string>
            {
                "def main():",
                "    x: int = 10",
                "    y: int = 3",
                $"    print(f\"result: {{{expr}}}\")",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> FStringComplexCombined() =>
        Gen.Int[0, 3].Select(variant =>
        {
            var lines = new List<string>
            {
                "class Point:",
                "    x: int",
                "    y: int",
                "",
                "    def __init__(self, x: int, y: int):",
                "        self.x = x",
                "        self.y = y",
                "",
                "    def magnitude(self) -> int:",
                "        return self.x + self.y",
                "",
                "def main():",
                "    p = Point(3, 4)",
                "    items: list[int] = [1, 2, 3]",
            };

            lines.Add(variant switch
            {
                0 => "    print(f\"({p.x}, {p.y})\")",
                1 => "    print(f\"mag={p.magnitude()} len={len(items)}\")",
                2 => "    print(f\"sum={p.x + p.y}\")",
                _ => "    print(f\"point ({p.x}, {p.y}) has {len(items)} items\")",
            });

            return string.Join("\n", lines) + "\n";
        });
}
