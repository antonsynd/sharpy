using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenContextManagers
{
    private static readonly string[] ReturnTypes = { "int", "str", "bool" };

    public static Gen<string> ValidContextManagerProgram() =>
        Gen.Select(
            Gen.OneOfConst(ReturnTypes),
            Gen.Bool,
            (returnType, hasBody) =>
            {
                var lines = new List<string>
                {
                    "class Resource:",
                    $"    value: {returnType}",
                    "",
                    $"    def __init__(self, value: {returnType}):",
                    "        self.value = value",
                    "",
                    $"    def __enter__(self) -> {returnType}:",
                    "        return self.value",
                    "",
                    "    def __exit__(self):",
                    "        pass",
                };

                if (hasBody)
                {
                    lines.Add("");
                    lines.Add($"    def get(self) -> {returnType}:");
                    lines.Add("        return self.value");
                }

                lines.Add("");
                lines.Add("def main():");
                lines.Add($"    r = Resource({DefaultLiteral(returnType)})");
                lines.Add("    with r as val:");
                lines.Add("        print(val)");

                return string.Join("\n", lines) + "\n";
            });

    public static Gen<string> ContextManagerWithAsBinding() =>
        Gen.OneOfConst(ReturnTypes).Select(returnType =>
        {
            var lines = new List<string>
            {
                "class Conn:",
                $"    data: {returnType}",
                "",
                $"    def __init__(self, data: {returnType}):",
                "        self.data = data",
                "",
                "    def __enter__(self) -> Conn:",
                "        return self",
                "",
                "    def __exit__(self):",
                "        pass",
                "",
                $"    def read(self) -> {returnType}:",
                "        return self.data",
                "",
                "def main():",
                $"    with Conn({DefaultLiteral(returnType)}) as c:",
                "        print(c.read())",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> AsyncContextManagerProgram() =>
        Gen.OneOfConst(ReturnTypes).Select(returnType =>
        {
            var lines = new List<string>
            {
                "class AsyncResource:",
                $"    value: {returnType}",
                "",
                $"    def __init__(self, value: {returnType}):",
                "        self.value = value",
                "",
                $"    async def __aenter__(self) -> {returnType}:",
                "        return self.value",
                "",
                "    async def __aexit__(self):",
                "        pass",
                "",
                "async def main():",
                $"    r = AsyncResource({DefaultLiteral(returnType)})",
                "    async with r as val:",
                "        print(val)",
            };

            return string.Join("\n", lines) + "\n";
        });

    public static Gen<string> MissingEnterOrExitProgram() =>
        Gen.Int[0, 2].Select(variant =>
        {
            var lines = variant switch
            {
                0 => new List<string>
                {
                    "class NoEnter:",
                    "    def __exit__(self):",
                    "        pass",
                    "",
                    "def main():",
                    "    with NoEnter() as x:",
                    "        pass",
                },
                1 => new List<string>
                {
                    "class NoExit:",
                    "    def __enter__(self) -> int:",
                    "        return 42",
                    "",
                    "def main():",
                    "    with NoExit() as x:",
                    "        pass",
                },
                _ => new List<string>
                {
                    "class Empty:",
                    "    pass",
                    "",
                    "def main():",
                    "    with Empty() as x:",
                    "        pass",
                },
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
