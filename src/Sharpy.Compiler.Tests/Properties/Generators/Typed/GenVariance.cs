using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenVariance
{
    private static readonly string[] ConcreteTypes = { "int", "str", "bool" };

    public static Gen<string> CovariantInterfaceProgram() =>
        Gen.Select(
            Gen.OneOfConst(ConcreteTypes),
            Gen.Int[1, 2],
            (concreteType, methodCount) =>
            {
                var lines = new List<string>
                {
                    "interface IProducer[out T]:",
                    "    def produce(self) -> T",
                };

                if (methodCount > 1)
                {
                    lines.Add("    def peek(self) -> T");
                }

                lines.Add("");
                lines.Add($"class Factory(IProducer[{concreteType}]):");
                lines.Add($"    def produce(self) -> {concreteType}:");
                lines.Add($"        return {DefaultLiteral(concreteType)}");

                if (methodCount > 1)
                {
                    lines.Add("");
                    lines.Add($"    def peek(self) -> {concreteType}:");
                    lines.Add($"        return {DefaultLiteral(concreteType)}");
                }

                lines.Add("");
                lines.Add("def main():");
                lines.Add("    f = Factory()");
                lines.Add("    print(f.produce())");

                return string.Join("\n", lines) + "\n";
            });

    public static Gen<string> ContravariantInterfaceProgram() =>
        Gen.Select(
            Gen.OneOfConst(ConcreteTypes),
            Gen.Int[1, 2],
            (concreteType, methodCount) =>
            {
                var lines = new List<string>
                {
                    "interface IConsumer[in T]:",
                    "    def consume(self, item: T) -> None",
                };

                if (methodCount > 1)
                {
                    lines.Add("    def process(self, item: T) -> None");
                }

                lines.Add("");
                lines.Add($"class Sink(IConsumer[{concreteType}]):");
                lines.Add($"    def consume(self, item: {concreteType}) -> None:");
                lines.Add("        pass");

                if (methodCount > 1)
                {
                    lines.Add("");
                    lines.Add($"    def process(self, item: {concreteType}) -> None:");
                    lines.Add("        pass");
                }

                lines.Add("");
                lines.Add("def main():");
                lines.Add("    s = Sink()");
                lines.Add($"    s.consume({DefaultLiteral(concreteType)})");

                return string.Join("\n", lines) + "\n";
            });

    public static Gen<string> VarianceOnClassProgram() =>
        Gen.Int[0, 1].Select(variant =>
        {
            var annotation = variant == 0 ? "out" : "in";

            var lines = new List<string>
            {
                $"class BadBox[{annotation} T]:",
                "    value: int",
                "",
                "    def __init__(self, value: int):",
                "        self.value = value",
                "",
                "def main():",
                "    pass",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> CovariantInInputPositionProgram() =>
        Gen.OneOfConst(ConcreteTypes).Select(concreteType =>
        {
            var lines = new List<string>
            {
                "interface IBadProducer[out T]:",
                "    def accept(self, item: T) -> None",
                "",
                $"class Producer(IBadProducer[{concreteType}]):",
                $"    def accept(self, item: {concreteType}) -> None:",
                "        pass",
                "",
                "def main():",
                $"    p = Producer()",
                $"    p.accept({DefaultLiteral(concreteType)})",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> ContravariantInOutputPositionProgram() =>
        Gen.OneOfConst(ConcreteTypes).Select(concreteType =>
        {
            var lines = new List<string>
            {
                "interface IBadConsumer[in T]:",
                "    def produce(self) -> T",
                "",
                $"class Consumer(IBadConsumer[{concreteType}]):",
                $"    def produce(self) -> {concreteType}:",
                $"        return {DefaultLiteral(concreteType)}",
                "",
                "def main():",
                "    c = Consumer()",
                "    print(c.produce())",
            };

            return string.Join("\n", lines) + "\n";
        });

    private static string DefaultLiteral(string type) => type switch
    {
        "int" => "42",
        "str" => "\"hello\"",
        "bool" => "True",
        _ => "0",
    };
}
