using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class GenImports
{
    private static readonly string[] SimpleTypes = { "int", "str", "bool" };

    public static Gen<(string LibSource, (string Name, string ParamType)[] Exports)> LibraryModule(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.Int[1, 3],
            Gen.OneOfConst(SimpleTypes),
            (count, retType) =>
            {
                var lines = new List<string>();
                var exports = new List<(string Name, string ParamType)>();

                for (int i = 0; i < count; i++)
                {
                    var funcName = $"lib_func_{i}";
                    var paramType = SimpleTypes[i % SimpleTypes.Length];
                    lines.Add($"def {funcName}(val: {paramType}) -> {retType}:");
                    lines.Add(retType switch
                    {
                        "int" => "    return 42",
                        "str" => "    return \"hello\"",
                        "bool" => "    return True",
                        _ => "    return 0"
                    });
                    lines.Add("");
                    exports.Add((funcName, paramType));
                }

                lines.Add($"LIB_CONST: {retType} = {DefaultLiteral(retType)}");
                exports.Add(("LIB_CONST", retType));

                return (string.Join("\n", lines) + "\n", exports.ToArray());
            });

    public static Gen<(string MainSource, string LibSource)> ImportingModule(TypeEnv env, int fuel) =>
        LibraryModule(env, fuel).SelectMany(lib =>
            Gen.Int[0, lib.Exports.Length - 1].Select(importIdx =>
            {
                var (importName, paramType) = lib.Exports[importIdx];
                var mainLines = new List<string>
                {
                    $"from lib import {importName}",
                    "",
                    "def main():"
                };

                if (importName.StartsWith("lib_func_"))
                {
                    mainLines.Add($"    print({importName}({DefaultLiteral(paramType)}))");
                }
                else
                {
                    mainLines.Add($"    print({importName})");
                }

                return (string.Join("\n", mainLines) + "\n", lib.LibSource);
            }));

    public static Gen<(string FileA, string FileB)> CircularImportPair(TypeEnv env, int fuel) =>
        Gen.OneOfConst(SimpleTypes).Select(retType =>
        {
            var fileA = $"from file_b import helper_b\n\ndef helper_a() -> {retType}:\n    return helper_b()\n";
            var fileB = $"from file_a import helper_a\n\ndef helper_b() -> {retType}:\n    return helper_a()\n";
            return (fileA, fileB);
        });

    public static Gen<(string Main, string Lib1, string Lib2, string ConflictingName)> NameCollisionPair(
        TypeEnv env, int fuel) =>
        Gen.OneOfConst(SimpleTypes).Select(retType =>
        {
            var conflictName = "shared_func";
            var lib1 = $"def {conflictName}() -> {retType}:\n    return {DefaultLiteral(retType)}\n";
            var lib2 = $"def {conflictName}() -> {retType}:\n    return {DefaultLiteral(retType)}\n";
            var main = $"from lib1 import {conflictName}\nfrom lib2 import {conflictName}\n\ndef main():\n    print({conflictName}())\n";
            return (main, lib1, lib2, conflictName);
        });

    public static Gen<(string Main, string Lib)> MultiFileProgram(TypeEnv env, int fuel) =>
        Gen.Select(
            Gen.Int[1, 2],
            Gen.OneOfConst(SimpleTypes),
            (funcCount, retType) =>
            {
                var libLines = new List<string>();
                var importNames = new List<string>();

                for (int i = 0; i < funcCount; i++)
                {
                    var name = $"util_{i}";
                    libLines.Add($"def {name}(x: int) -> {retType}:");
                    libLines.Add(retType switch
                    {
                        "int" => "    return x + 1",
                        "str" => "    return \"done\"",
                        "bool" => "    return True",
                        _ => "    return 0"
                    });
                    libLines.Add("");
                    importNames.Add(name);
                }

                var imports = string.Join(", ", importNames);
                var mainLines = new List<string>
                {
                    $"from lib import {imports}",
                    "",
                    "def main():",
                    $"    result = {importNames[0]}(10)",
                    "    print(result)"
                };

                return (string.Join("\n", mainLines) + "\n",
                        string.Join("\n", libLines) + "\n");
            });

    public static Gen<(string Main, string Lib)> UnusedImportProgram(TypeEnv env, int fuel) =>
        Gen.OneOfConst(SimpleTypes).Select(retType =>
        {
            var lib = $"def unused_func() -> {retType}:\n    return {DefaultLiteral(retType)}\n";
            var main = "from lib import unused_func\n\ndef main():\n    print(42)\n";
            return (main, lib);
        });

    private static string DefaultLiteral(string type) => type switch
    {
        "int" => "0",
        "str" => "\"\"",
        "bool" => "True",
        _ => "0"
    };
}
