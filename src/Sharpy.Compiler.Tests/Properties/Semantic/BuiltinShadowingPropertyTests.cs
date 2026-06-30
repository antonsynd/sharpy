using CsCheck;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// A user-defined generic function whose name shadows a builtin must, when called with
/// explicit type arguments, resolve to the user function — both in semantic binding
/// (#1002) and codegen (#1003, which previously mis-qualified the call as
/// <c>Sharpy.Builtins.X&lt;...&gt;</c> and failed with CS0305). The identity body means a
/// correctly-resolved call echoes its argument; a leak to the builtin overload would
/// either fail to compile or print something else.
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class BuiltinShadowingPropertyTests : IntegrationTestBase
{
    public BuiltinShadowingPropertyTests(ITestOutputHelper output) : base(output) { }

    // Builtin function names that compile to a PascalCase identifier distinct from the
    // default `test.spy` module class (`Test`), so shadowing is exercised without a
    // module-class collision (SPY0523).
    private static readonly string[] BuiltinNames =
    {
        "map", "zip", "filter", "len", "sum", "sorted", "min", "max",
        "abs", "enumerate", "reversed", "any", "all", "range", "iter", "next",
    };

    private static readonly (string Type, string Value, string Expected)[] Cases =
    {
        ("int", "5", "5"),
        ("int", "42", "42"),
        ("str", "\"hi\"", "hi"),
        ("bool", "True", "True"),
        ("float", "3.5", "3.5"),
    };

    [Fact]
    public void UserGenericShadowingBuiltin_ExplicitCall_ResolvesToUserFunction()
    {
        Gen.Select(Gen.OneOfConst(BuiltinNames), Gen.OneOfConst(Cases))
            .Sample((name, c) =>
            {
                var source =
                    $"def {name}[T](x: T) -> T:\n" +
                    "    return x\n\n" +
                    "def main() -> None:\n" +
                    $"    print({name}[{c.Type}]({c.Value}))";

                ExecutionResult result = CompileAndExecute(source);

                if (!result.Success)
                    throw new Exception(
                        $"User generic '{name}[{c.Type}]' shadowing a builtin failed to compile " +
                        $"(builtin-shadow regression): {result.StandardError}");

                var actual = result.StandardOutput.TrimEnd();
                if (actual != c.Expected)
                    throw new Exception(
                        $"{name}[{c.Type}]({c.Value}) printed '{actual}', expected '{c.Expected}' " +
                        "(resolved to the builtin overload instead of the user function)");
            }, iter: 25);
    }
}
