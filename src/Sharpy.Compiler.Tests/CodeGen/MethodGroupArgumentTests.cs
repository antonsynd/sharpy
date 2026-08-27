using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Pins the "argument is a method group" decision the emitter makes when a function name is
/// passed where a delegate is expected. The decision reads the recorded identifier symbol
/// (<c>SemanticInfo.GetIdentifierSymbol</c>) — the only route by which a NESTED <c>def</c> is
/// visible in CodeGen once the emitter-side local-function table was deleted (#1560). The
/// observable is the null-forgiving <c>!</c> that <c>ApplyNullabilityDelegateAdaptation</c>
/// appends to a method group (never to a lambda): losing the nested-def arm dropped it from
/// the regenerated json spy tests and Sharpy.Stdlib.Tests failed to build with CS8622 (gate @
/// 3bc6bc2a7). The top-level def is the positive control that passes with or without the
/// nested-def arm; the lambda is the negative control.
/// </summary>
public class MethodGroupArgumentTests
{
    private const string Prelude = @"
def apply(f: (int) -> int, x: int) -> int:
    return f(x)

def double_it(n: int) -> int:
    return n * 2
";

    [Fact]
    public void NestedDef_PassedPositionally_IsAMethodGroup_GetsNullForgiving()
    {
        var code = EmitterTestPipeline.CompileToCSharp(Prelude + @"
def main() -> None:
    def inner(n: int) -> int:
        return n + 1
    print(apply(inner, 5))
", isEntryPoint: true, requireNoErrors: true);

        code.Should().MatchRegex(@"Apply\(\s*Inner!\s*,");
    }

    [Fact]
    public void NestedDef_PassedByKeyword_IsAMethodGroup_GetsNullForgiving()
    {
        var code = EmitterTestPipeline.CompileToCSharp(Prelude + @"
def main() -> None:
    def inner(n: int) -> int:
        return n + 1
    print(apply(f=inner, x=5))
", isEntryPoint: true, requireNoErrors: true);

        code.Should().MatchRegex(@"f:\s*Inner!");
    }

    [Fact]
    public void TopLevelDef_PositiveControl_GetsNullForgiving()
    {
        var code = EmitterTestPipeline.CompileToCSharp(Prelude + @"
def main() -> None:
    print(apply(double_it, 5))
", isEntryPoint: true, requireNoErrors: true);

        code.Should().MatchRegex(@"Apply\(\s*DoubleIt!\s*,");
    }

    [Fact]
    public void Lambda_NegativeControl_NoNullForgiving()
    {
        var code = EmitterTestPipeline.CompileToCSharp(Prelude + @"
def main() -> None:
    print(apply(lambda n: n + 1, 5))
", isEntryPoint: true, requireNoErrors: true);

        code.Should().NotContain("!,").And.NotContain("!)");
    }
}
