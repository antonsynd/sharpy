using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance matrix for truthiness (#1558): position (9) x type (15).
/// Each cell asserts either accepted (truth-testable) or refused (SPY0220/SPY0241).
/// Adding a new truth position or type without updating this matrix is a loud failure.
/// </summary>
[Collection("HeavyCompilation")]
public class TruthinessConformanceTests : IntegrationTestBase
{
    public TruthinessConformanceTests(ITestOutputHelper output) : base(output) { }

    private const string Preamble = @"
class HasBool:
    def __bool__(self) -> bool:
        return True

class HasLen:
    _items: list[int]
    def __init__(self) -> None:
        self._items = [1, 2]
    def __len__(self) -> int:
        return len(self._items)

class PlainObject:
    pass
";

    // Types that ARE truth-testable (have a falsy case)
    public static IEnumerable<object[]> TruthTestableTypes()
    {
        yield return new object[] { "bool", "x: bool = True" };
        yield return new object[] { "int", "x: int = 42" };
        yield return new object[] { "float", "x: float = 3.14" };
        yield return new object[] { "long", "x: long = 42L" };
        yield return new object[] { "str", "x: str = \"hello\"" };
        yield return new object[] { "bytes", "x: bytes = b\"data\"" };
        yield return new object[] { "list", "x: list[int] = [1, 2]" };
        yield return new object[] { "dict", "x: dict[str, int] = {\"a\": 1}" };
        yield return new object[] { "set", "x: set[int] = {1, 2}" };
        yield return new object[] { "Optional", "x: int? = Some(42)" };
        yield return new object[] { "None", "x: int? = None()" };
        yield return new object[] { "UDT __bool__", "x: HasBool = HasBool()" };
        yield return new object[] { "UDT __len__", "x: HasLen = HasLen()" };
    }

    // Types that are NOT truth-testable (no falsy case)
    public static IEnumerable<object[]> NonTruthTestableTypes()
    {
        yield return new object[] { "function", "def f() -> int:\n        return 1\n    x = f" };
        yield return new object[] { "plain object", "x: PlainObject = PlainObject()" };
    }

    // --- if position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void If_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    if x:
        print(""ok"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'if' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void If_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    if x:
        print(""fail"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'if' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'if' refusal of {typeName} should produce SPY0220");
    }

    // --- while position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void While_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    while x:
        break
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'while' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void While_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    while x:
        break
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'while' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'while' refusal of {typeName} should produce SPY0220");
    }

    // --- assert position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void Assert_AcceptsTruthTestableType(string typeName, string decl)
    {
        // assert of an always-falsy value (None) compiles but throws at runtime — check
        // compilation only (no SPY0220), don't assert on execution success.
        var source = Preamble + $@"
def main() -> None:
    {decl}
    assert x
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0220",
            $"'assert' should accept truth-testable type {typeName} (no SPY0220)");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void Assert_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    assert x
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'assert' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'assert' refusal of {typeName} should produce SPY0220");
    }

    // --- ternary position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void Ternary_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: int = 1 if x else 2
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"ternary should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void Ternary_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: int = 1 if x else 2
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"ternary should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"ternary refusal of {typeName} should produce SPY0220");
    }

    // --- and position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void And_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = x and True
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'and' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void And_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = x and True
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'and' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'and' refusal of {typeName} should produce SPY0220");
    }

    // --- or position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void Or_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = x or False
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'or' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void Or_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = x or False
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'or' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'or' refusal of {typeName} should produce SPY0220");
    }

    // --- not position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void Not_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = not x
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'not' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void Not_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = not x
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'not' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'not' refusal of {typeName} should produce SPY0220");
    }

    // --- match guard position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void MatchGuard_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    val: int = 1
    match val:
        case v if x:
            print(""guarded"")
        case _:
            print(""fallback"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"match guard should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void MatchGuard_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    val: int = 1
    match val:
        case v if x:
            print(""fail"")
        case _:
            print(""fallback"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"match guard should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220" || d.Code == "SPY0241",
            $"match guard refusal of {typeName} should produce SPY0220 or SPY0241");
    }

    // --- comprehension filter position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void ComprehensionFilter_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def check(val: bool) -> list[int]:
    return [1 for _ in range(1) if val]

def main() -> None:
    {decl}
    result: list[int] = [1 for _ in range(1) if x]
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"comprehension filter should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void ComprehensionFilter_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    result: list[int] = [1 for _ in range(1) if x]
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"comprehension filter should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0241",
            $"comprehension filter refusal of {typeName} should produce SPY0241");
    }
}
