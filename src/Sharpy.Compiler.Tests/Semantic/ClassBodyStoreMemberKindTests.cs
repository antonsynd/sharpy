using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The bare-store refusal keeps its own member KINDS when the one membership predicate
/// (<c>TypeBodyMembership</c>) grows (P21h D8; lead ruling (a), 2026-10-28): the read steer now
/// describes methods and events too, but a bare store in a method body is refused only for a field or
/// a property (#1786, R-Y; #1803, R-AA). A local named like a method or an event is a local, as in C#
/// and Python. The field cell is the positive control: the refusal still fires for storage.
/// </summary>
public class ClassBodyStoreMemberKindTests : IntegrationTestBase
{
    public ClassBodyStoreMemberKindTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void BareStore_OfAMethodsName_DeclaresALocal()
    {
        var result = CompileAndExecute("""
            class C:
                def items(self) -> int:
                    return 1

                def m(self) -> int:
                    items = 5
                    return items


            def main() -> None:
                print(C().m())
            """);

        Assert.True(result.Success, string.Join(" | ", result.CompilationErrors));
        Assert.Equal("5\n", result.StandardOutput);
    }

    [Fact]
    public void BareStore_OfAnEventsName_DeclaresALocal()
    {
        var result = CompileAndExecute("""
            class C:
                event changed: Action[int]

                def m(self) -> int:
                    changed = 7
                    return changed


            def main() -> None:
                print(C().m())
            """);

        Assert.True(result.Success, string.Join(" | ", result.CompilationErrors));
        Assert.Equal("7\n", result.StandardOutput);
    }

    [Fact]
    public void BareStore_OfAFieldsName_IsStillRefused()
    {
        var result = CompileAndExecute("""
            class C:
                f: int = 1

                def m(self) -> int:
                    f = 5
                    return f


            def main() -> None:
                print(C().m())
            """);

        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors,
            e => e.Contains("Cannot assign to class attribute 'f' by bare name", StringComparison.Ordinal));
    }
}
