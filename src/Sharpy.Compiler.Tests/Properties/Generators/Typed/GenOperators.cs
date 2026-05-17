using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenOperators
{
    private static readonly (string Dunder, string Op)[] BinaryOps =
    {
        ("__add__", "+"),
        ("__sub__", "-"),
        ("__mul__", "*"),
        ("__mod__", "%"),
    };

    private static readonly (string Dunder, string Op)[] UnaryOps =
    {
        ("__neg__", "-"),
        ("__pos__", "+"),
        ("__invert__", "~"),
    };

    private static readonly (string Dunder, string Op)[] ComparisonOps =
    {
        ("__lt__", "<"),
        ("__le__", "<="),
        ("__gt__", ">"),
        ("__ge__", ">="),
        ("__eq__", "=="),
        ("__ne__", "!="),
    };

    public static Gen<string> BinaryOperatorProgram() =>
        Gen.Select(
            Gen.Int[1, 3],
            Gen.Int[0, BinaryOps.Length - 1],
            (opCount, startIdx) =>
            {
                var lines = new List<string>
                {
                    "class Vec:",
                    "    x: int",
                    "    y: int",
                    "",
                    "    def __init__(self, x: int, y: int):",
                    "        self.x = x",
                    "        self.y = y",
                    ""
                };

                var opsToUse = new List<(string Dunder, string Op)>();
                for (int i = 0; i < opCount; i++)
                {
                    var idx = (startIdx + i) % BinaryOps.Length;
                    opsToUse.Add(BinaryOps[idx]);
                }

                foreach (var (dunder, _) in opsToUse)
                {
                    lines.Add($"    def {dunder}(self, other: Vec) -> Vec:");
                    lines.Add($"        return Vec(self.x + other.x, self.y + other.y)");
                    lines.Add("");
                }

                lines.Add("def main():");
                lines.Add("    a = Vec(1, 2)");
                lines.Add("    b = Vec(3, 4)");

                foreach (var (_, op) in opsToUse)
                {
                    lines.Add($"    c = a {op} b");
                    lines.Add("    print(c.x)");
                }

                return string.Join("\n", lines) + "\n";
            });

    public static Gen<string> UnaryOperatorProgram() =>
        Gen.Int[0, UnaryOps.Length - 1].Select(idx =>
        {
            var (dunder, op) = UnaryOps[idx];
            var retExpr = dunder == "__invert__"
                ? "Vec(-self.x, -self.y)"
                : dunder == "__neg__"
                    ? "Vec(-self.x, -self.y)"
                    : "Vec(self.x, self.y)";

            var lines = new List<string>
            {
                "class Vec:",
                "    x: int",
                "    y: int",
                "",
                "    def __init__(self, x: int, y: int):",
                "        self.x = x",
                "        self.y = y",
                "",
                $"    def {dunder}(self) -> Vec:",
                $"        return {retExpr}",
                "",
                "def main():",
                "    v = Vec(1, 2)",
                $"    w = {op}v",
                "    print(w.x)"
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> ComparisonOperatorProgram() =>
        Gen.Int[0, ComparisonOps.Length - 1].Select(idx =>
        {
            var (dunder, op) = ComparisonOps[idx];

            var lines = new List<string>
            {
                "class Score:",
                "    value: int",
                "",
                "    def __init__(self, value: int):",
                "        self.value = value",
                "",
                $"    def {dunder}(self, other: Score) -> bool:",
                $"        return self.value {op} other.value",
                "",
                "def main():",
                "    a = Score(10)",
                "    b = Score(20)",
                $"    print(a {op} b)"
            };

            return string.Join("\n", lines) + "\n";
        });

    private static readonly (string Dunder, string Op)[] AdditiveOps =
    {
        ("__add__", "+"),
        ("__sub__", "-"),
    };

    private static readonly (string Dunder, string Op)[] MultiplicativeOps =
    {
        ("__mul__", "*"),
        ("__mod__", "%"),
    };

    public static Gen<string> PrecedenceProgram() =>
        Gen.Select(
            Gen.Int[0, AdditiveOps.Length - 1],
            Gen.Int[0, MultiplicativeOps.Length - 1],
            (opIdx1, opIdx2) =>
            {
                var op1 = AdditiveOps[opIdx1];
                var op2 = MultiplicativeOps[opIdx2];

                var lines = new List<string>
                {
                    "class Num:",
                    "    value: int",
                    "",
                    "    def __init__(self, value: int):",
                    "        self.value = value",
                    "",
                    $"    def {op1.Dunder}(self, other: Num) -> Num:",
                    "        return Num(self.value + other.value)",
                    "",
                    $"    def {op2.Dunder}(self, other: Num) -> Num:",
                    "        return Num(self.value * other.value)",
                    "",
                    "def main():",
                    "    a = Num(1)",
                    "    b = Num(2)",
                    "    c = Num(3)",
                    $"    d = a {op1.Op} b {op2.Op} c",
                    "    print(d.value)"
                };

                return string.Join("\n", lines) + "\n";
            });

    public static Gen<string> InvalidDunderProgram() =>
        Gen.Int[0, 2].Select(variant =>
        {
            var lines = variant switch
            {
                0 => new List<string>
                {
                    "class Bad:",
                    "    def __add__(self) -> Bad:",
                    "        return self",
                    "",
                    "def main():",
                    "    pass",
                },
                1 => new List<string>
                {
                    "class Bad:",
                    "    def __neg__(self, other: Bad) -> Bad:",
                    "        return self",
                    "",
                    "def main():",
                    "    pass",
                },
                _ => new List<string>
                {
                    "class Bad:",
                    "    def __xyzzy__(self) -> Bad:",
                    "        return self",
                    "",
                    "def main():",
                    "    pass",
                },
            };

            return string.Join("\n", lines) + "\n";
        });
}
