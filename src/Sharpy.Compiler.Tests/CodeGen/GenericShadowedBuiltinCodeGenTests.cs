using System.Linq;
using FluentAssertions;
using Sharpy.Compiler;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// #1003: codegen sibling of #1002. When a user-defined generic function shadows a builtin
/// name (e.g. <c>def map[T]</c>), an explicit-generic call <c>map[int](5)</c> must emit the
/// user's unqualified method <c>Map&lt;int&gt;(5)</c>, NOT <c>global::Sharpy.Builtins.Map&lt;int&gt;</c>
/// (which produces a C# CS0305 compile error). #1002 fixed the semantic binding; this asserts the
/// emitted C# string — the end-to-end gap #1002's symbol-level test could not cover.
/// </summary>
public class GenericShadowedBuiltinCodeGenTests
{
    private static string CompileToCSharp(string source)
    {
        var compiler = new global::Sharpy.Compiler.Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        result.GeneratedCSharpCode.Should().NotBeNull();
        return result.GeneratedCSharpCode!;
    }

    [Fact]
    public void ExplicitGenericCall_ShadowingBuiltin_EmitsUserMethodUnqualified()
    {
        // def map[T] shadows the builtin map; map[int](5) must bind to the user's Map<int>.
        var source = @"
def map[T](x: T) -> T:
    return x

def main() -> None:
    print(map[int](5))
";
        var csharp = CompileToCSharp(source);

        csharp.Should().Contain("Map<int>(5)",
            "the explicit-generic call must emit the user's unqualified generic method (#1003)");
        csharp.Should().NotContain("Builtins.Map<int>",
            "a user-shadowed name must not be qualified with the builtin registry (#1003)");
    }

    [Fact]
    public void ExplicitGenericCall_GenuineBuiltin_StillQualifiesWithBuiltins()
    {
        // No user shadow: a real explicit-generic builtin map[...] call must still emit
        // global::Sharpy.Builtins.Map<...> — the path the #1003 fix must leave intact.
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main() -> None:
    a: list[int] = [1, 2, 3]
    b: list[int] = [10, 20, 30]
    print(list(map[int, int, int](add, a, b)))
";
        var csharp = CompileToCSharp(source);

        csharp.Should().Contain("Sharpy.Builtins.Map<",
            "a genuine builtin explicit-generic call must remain qualified (#1003 no-regression)");
    }
}
