using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// The typed doors' missing-required-field message names the target by its python name, Core
/// <see cref="PyFormat.PyTypeName"/> (the #2035 authority): <c>app_cfg</c>, not the emitted CLR
/// <c>AppCfg</c>, and <c>Box</c>, not <c>Box`1</c> (#2099). Both doors share
/// <c>TypedLoadContract.MissingFieldMessage</c>.
/// </summary>
public class TypedLoadMissingFieldNameTests : StdlibIntegrationTestBase
{
    public TypedLoadMissingFieldNameTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void MissingField_NamesTheTargetByItsPythonName()
    {
        var result = CompileAndExecute(
            "import json\nimport yaml\n\n\n"
            + "@dataclass\nclass app_cfg:\n    name: str\n    max_conn: int\n\n\n"
            + "@dataclass\nclass Box[T]:\n    item: T\n\n\n"
            + "def main() -> None:\n"
            + "    match json.loads[app_cfg](\"{\\\"name\\\": \\\"web\\\"}\"):\n"
            + "        case Ok(v):\n            print(\"ok\")\n        case Err(e):\n            print(e)\n"
            + "    match yaml.safe_load_typed[app_cfg](\"name: web\\n\"):\n"
            + "        case Ok(v):\n            print(\"ok\")\n        case Err(e):\n            print(e)\n"
            + "    match json.loads[Box[int]](\"{}\"):\n"
            + "        case Ok(v):\n            print(\"ok\")\n        case Err(e):\n            print(e)\n");

        Assert.True(result.Success, $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal(
            new[]
            {
                "missing required field 'max_conn' for app_cfg: line 1 column 1 (char 0)",
                "missing required field 'max_conn' for app_cfg",
                "missing required field 'item' for Box: line 1 column 1 (char 0)",
            },
            result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
    }
}
