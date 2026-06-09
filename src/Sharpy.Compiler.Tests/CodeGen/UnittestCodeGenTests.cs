using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for the unittest framework code generation transforms:
///
/// 1. Assert rewriting inside <c>@test</c> functions (Plan Task 20):
///    each Python-style <c>assert</c> is rewritten to a specialized
///    <c>Xunit.Assert.*</c> call (Equal, NotEqual, Null, NotNull, Contains,
///    DoesNotContain, IsAssignableFrom, False, True). Outside <c>@test</c>, asserts
///    continue to emit <c>System.Diagnostics.Debug.Assert(...)</c>.
///
/// 2. Unittest module transforms (Plan Task 22):
///    <c>with assert_raises(E):</c> compiles to <c>Xunit.Assert.Throws&lt;E&gt;</c>
///    around a lambda; <c>assert_almost_equal(a, b)</c> compiles to
///    <c>Xunit.Assert.Equal(b, a, 7)</c>; <c>TestCase</c> lifecycle methods
///    are wired up via a generated constructor/Dispose pair; and
///    module-level <c>@test</c> functions are lifted into a sibling
///    <c>{Module}Tests</c> partial class so xUnit can discover them.
/// </summary>
public class UnittestCodeGenTests
{
    private readonly ITestOutputHelper _output;

    public UnittestCodeGenTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private string CompileToCSharp(string source, string fileName = "test.spy", bool requiresSharpyCore = false)
    {
        var compiler = requiresSharpyCore
            ? new Compiler(new CompilerOptions { References = new[] { SharpyCoreReference.Location, SharpyStdlibReference.Location } })
            : new Compiler();
        var result = compiler.Compile(source, fileName);
        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        result.GeneratedCSharpCode.Should().NotBeNull();
        _output.WriteLine("=== Generated C# ===");
        _output.WriteLine(result.GeneratedCSharpCode);
        return result.GeneratedCSharpCode!;
    }

    #region Assert Rewriting (Plan Task 20)

    [Fact]
    public void AssertRewrite_Equality_GeneratesXunitAssertEqual()
    {
        var source = @"
@test
def test_eq():
    x: int = 1
    y: int = 2
    assert x == y

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        // assert a == b → Xunit.Assert.Equal(b, a)  (expected, actual order)
        code.Should().Contain("Xunit.Assert.Equal(y, x)");
    }

    [Fact]
    public void AssertRewrite_NotEqual_GeneratesXunitAssertNotEqual()
    {
        var source = @"
@test
def test_neq():
    x: int = 1
    y: int = 2
    assert x != y

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.NotEqual(y, x)");
    }

    [Fact]
    public void AssertRewrite_IsNone_GeneratesXunitAssertNull()
    {
        var source = @"
@test
def test_is_none():
    s: str? = None()
    assert s is None

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.Null(s)");
    }

    [Fact]
    public void AssertRewrite_IsNotNone_GeneratesXunitAssertNotNull()
    {
        var source = @"
@test
def test_not_none():
    s: str? = None()
    assert s is not None

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.NotNull(s)");
    }

    [Fact]
    public void AssertRewrite_In_GeneratesXunitAssertContains()
    {
        var source = @"
@test
def test_in():
    items: list[int] = [1, 2, 3]
    assert 1 in items

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.Contains(1, items)");
    }

    [Fact]
    public void AssertRewrite_NotIn_GeneratesXunitAssertDoesNotContain()
    {
        var source = @"
@test
def test_not_in():
    items: list[int] = [1, 2, 3]
    assert 4 not in items

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.DoesNotContain(4, items)");
    }

    [Fact]
    public void AssertRewrite_Isinstance_GeneratesXunitAssertIsAssignableFrom()
    {
        var source = @"
@test
def test_isinstance():
    x: object = 42
    assert isinstance(x, int)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.IsAssignableFrom<int>(x)");
    }

    [Fact]
    public void AssertRewrite_Not_GeneratesXunitAssertFalse()
    {
        var source = @"
@test
def test_not():
    flag: bool = True
    assert not flag

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.False(flag)");
    }

    [Fact]
    public void AssertRewrite_Comparison_GeneratesXunitAssertTrue()
    {
        var source = @"
@test
def test_cmp():
    x: int = 1
    y: int = 2
    assert x > y

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        // Comparisons fall back to Xunit.Assert.True wrapping the comparison
        code.Should().Contain("Xunit.Assert.True(x > y)");
    }

    [Fact]
    public void AssertRewrite_Fallback_GeneratesXunitAssertTrue()
    {
        var source = @"
@test
def test_fallback():
    flag: bool = True
    assert flag

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.True(flag)");
    }

    [Fact]
    public void AssertRewrite_WithMessage_PassesMessageThrough()
    {
        var source = @"
@test
def test_with_msg():
    flag: bool = True
    assert flag, ""should be true""

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        // The message should be forwarded to the xUnit assertion
        code.Should().Contain("Xunit.Assert.True(flag, \"should be true\")");
    }

    [Fact]
    public void AssertOutsideTest_StillEmitsDebugAssert()
    {
        // Regression guard: asserts in non-@test functions must continue to
        // use System.Diagnostics.Debug.Assert and must NOT be rewritten to
        // Xunit.Assert.*. This protects production code paths.
        var source = @"
def helper():
    x: int = 1
    y: int = 2
    assert x != y

def main():
    helper()
";
        var code = CompileToCSharp(source);
        code.Should().Contain("System.Diagnostics.Debug.Assert");
        code.Should().NotContain("Xunit.Assert.");
    }

    #endregion

    #region startswith / endswith assert rewriting (#837)

    [Fact]
    public void AssertRewrite_StrStartswith_GeneratesXunitAssertStartsWith()
    {
        var source = @"
@test
def test_prefix():
    name: str = ""hello world""
    assert name.startswith(""hello"")

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        // assert s.startswith(p) → Xunit.Assert.StartsWith(p, s)  (pattern, actual order)
        code.Should().Contain("Xunit.Assert.StartsWith(\"hello\", name)");
    }

    [Fact]
    public void AssertRewrite_StrEndswith_GeneratesXunitAssertEndsWith()
    {
        var source = @"
@test
def test_suffix():
    name: str = ""hello world""
    assert name.endswith(""world"")

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.EndsWith(\"world\", name)");
    }

    [Fact]
    public void AssertRewrite_NonStrStartswith_FallsBackToAssertTrue()
    {
        // Type-gated: a non-str receiver (here a user-defined class with its own
        // startswith method) must NOT be rewritten to Xunit.Assert.StartsWith — it
        // falls through to the Assert.True fallback so there is no surprising behavior.
        var source = @"
class Matcher:
    def startswith(self, prefix: str) -> bool:
        return True

@test
def test_user_type(m: Matcher):
    assert m.startswith(""x"")

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().NotContain("Xunit.Assert.StartsWith");
        code.Should().Contain("Xunit.Assert.True(");
    }

    [Fact]
    public void AssertRewrite_StartswithWithStartArg_FallsBackToAssertTrue()
    {
        // Multi-argument startswith (start offset) is not the single-arg shape we
        // rewrite, so it falls through to Assert.True.
        var source = @"
@test
def test_multi_arg():
    name: str = ""hello world""
    assert name.startswith(""hello"", 0)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().NotContain("Xunit.Assert.StartsWith");
        code.Should().Contain("Xunit.Assert.True(");
    }

    #endregion

    #region approx equality assert rewriting (#837)

    [Fact]
    public void AssertRewrite_ApproxDefault_GeneratesEqualWithPrecision7()
    {
        var source = @"
from unittest import approx

@test
def test_approx():
    assert 0.1 + 0.2 == approx(0.3)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // expected (from approx), actual, default precision 7
        code.Should().Contain("Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 7)");
    }

    [Fact]
    public void AssertRewrite_ApproxPlaces_GeneratesEqualWithPrecision()
    {
        var source = @"
from unittest import approx

@test
def test_approx():
    assert 0.1 + 0.2 == approx(0.3, places=10)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        code.Should().Contain("Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 10)");
    }

    [Fact]
    public void AssertRewrite_ApproxAbs_GeneratesEqualWithTolerance()
    {
        var source = @"
from unittest import approx

@test
def test_approx():
    assert 0.1 + 0.2 == approx(0.3, abs=1e-9)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // abs=d selects the tolerance overload (double argument)
        code.Should().Contain("Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 1e-9d)");
    }

    [Fact]
    public void AssertRewrite_ApproxOnLeft_UsesApproxArgAsExpected()
    {
        var source = @"
from unittest import approx

@test
def test_approx():
    assert approx(0.3) == 0.1 + 0.2

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // approx's argument is the expected value regardless of operand position
        code.Should().Contain("Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 7)");
    }

    [Fact]
    public void AssertRewrite_ApproxBothAbsAndPlaces_AbsWins()
    {
        var source = @"
from unittest import approx

@test
def test_approx():
    assert 0.1 + 0.2 == approx(0.3, places=3, abs=1e-9)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // abs wins over places — tolerance overload, not precision 3
        code.Should().Contain("Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 1e-9d)");
        code.Should().NotContain(", 3)");
    }

    [Fact]
    public void AssertRewrite_ApproxOutsideTest_NotRewritten()
    {
        // The approx rewrite is gated to @test functions. In an ordinary function the
        // assert lowers to System.Diagnostics.Debug.Assert and the approx() call is left
        // as the runtime marker (which throws NotSupportedException), matching the
        // behavior of every other unittest marker outside a test.
        var source = @"
from unittest import approx

def helper():
    value: float = 1.0
    assert value == approx(1.0)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        code.Should().Contain("System.Diagnostics.Debug.Assert(value == Approx(1.0d))");
        // No tolerance/precision Xunit.Assert.Equal rewrite outside a @test function.
        code.Should().NotContain("Xunit.Assert.Equal");
    }

    #endregion

    #region Unittest transforms (Plan Task 22)

    [Fact]
    public void AssertRaises_GeneratesXunitAssertThrows()
    {
        var source = @"
from unittest import assert_raises

@test
def test_raises():
    with assert_raises(ValueError):
        raise ValueError(""oops"")

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // `with assert_raises(E):` becomes a call to Xunit.Assert.Throws<E>
        // wrapping the suite as a lambda. We only assert on the call shape;
        // the lambda formatting may vary across normalizations.
        code.Should().Contain("Xunit.Assert.Throws<ValueError>");
    }

    [Fact]
    public void AssertRaises_WithCapture_GeneratesVarDeclarationFromAssertThrows()
    {
        var source = @"
from unittest import assert_raises

@test
def test_capture():
    with assert_raises(ValueError) as exc:
        raise ValueError(""bad input"")
    assert ""bad input"" in str(exc)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // `with assert_raises(E) as exc:` captures the thrown exception:
        //   var exc = Xunit.Assert.Throws<E>((Action)(() => { ... }));
        code.Should().Contain("var exc = Xunit.Assert.Throws<ValueError>");
        // The captured variable must be usable in subsequent assertions
        code.Should().Contain("Xunit.Assert.Contains(\"bad input\", global::Sharpy.Builtins.Str(exc))");
    }

    [Fact]
    public void AssertRaises_WithoutCapture_DoesNotDeclareVariable()
    {
        var source = @"
from unittest import assert_raises

@test
def test_raises():
    with assert_raises(ValueError):
        raise ValueError(""oops"")

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // No `as` name → plain expression statement, no local declaration
        code.Should().Contain("Xunit.Assert.Throws<ValueError>");
        code.Should().NotContain("= Xunit.Assert.Throws");
    }

    [Fact]
    public void AssertRaises_WithMatch_GeneratesAssertMatchesOnMessage()
    {
        // assert_raises(E, "pattern") (positional match) → captured Throws + Assert.Matches
        // on the exception Message (re.search semantics). The captured local is a temp.
        var source = @"
from unittest import assert_raises

@test
def test_match():
    with assert_raises(ValueError, ""bad.*input""):
        raise ValueError(""bad input"")

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        code.Should().Contain("= Xunit.Assert.Throws<ValueError>");
        code.Should().MatchRegex(@"Xunit\.Assert\.Matches\(""bad\.\*input"", __ex_\d+\.Message\)");
    }

    [Fact]
    public void AssertRaises_WithMatchAndCapture_ReusesCapturedName()
    {
        // assert_raises(E, "pattern") as exc → the captured name is reused for Assert.Matches
        // and remains visible to statements after the with (flat, no scoping block).
        var source = @"
from unittest import assert_raises

@test
def test_match_capture():
    with assert_raises(ValueError, ""bad"") as exc:
        raise ValueError(""bad input"")
    assert str(exc) == ""bad input""

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        code.Should().Contain("var exc = Xunit.Assert.Throws<ValueError>");
        code.Should().Contain("Xunit.Assert.Matches(\"bad\", exc.Message)");
        // exc is still visible afterward → assert str(exc) == ... lowers using exc
        code.Should().Contain("global::Sharpy.Builtins.Str(exc)");
    }

    [Fact]
    public void AssertAlmostEqual_GeneratesXunitAssertEqualWithPrecision()
    {
        var source = @"
from unittest import assert_almost_equal

@test
def test_almost():
    assert_almost_equal(3.14159, 3.14159265)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // Default precision is 7. Argument order is (expected, actual, places),
        // which inverts the (actual, expected) order of the source call.
        code.Should().Contain("Xunit.Assert.Equal(3.14159265d, 3.14159d, 7)");
    }

    [Fact]
    public void AssertAlmostEqual_WithPlaces_PassesPrecisionThrough()
    {
        var source = @"
from unittest import assert_almost_equal

@test
def test_almost():
    assert_almost_equal(0.1 + 0.2, 0.3, places=3)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        code.Should().Contain("Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 3)");
    }

    [Fact]
    public void AssertCountEqual_GeneratesSortedEqual()
    {
        // assert_count_equal(a, b) → Xunit.Assert.Equal(Sorted(b), Sorted(a))
        var source = @"
from unittest import assert_count_equal

@test
def test_count():
    assert_count_equal([3, 1, 2], [1, 2, 3])

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        code.Should().Contain("Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted(");
        // expected = Sorted(b), actual = Sorted(a) — both operands sorted
        code.Should().MatchRegex(
            @"Xunit\.Assert\.Equal\(global::Sharpy\.Builtins\.Sorted\(.*\), global::Sharpy\.Builtins\.Sorted\(.*\)\)");
    }

    [Fact]
    public void AssertRegex_GeneratesAssertMatchesWithSwappedArgs()
    {
        // assert_regex(text, pattern) → Xunit.Assert.Matches(pattern, text)
        var source = @"
from unittest import assert_regex

@test
def test_regex():
    assert_regex(""2026-06-09"", ""[0-9]+"")

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // pattern first, text second
        code.Should().Contain("Xunit.Assert.Matches(\"[0-9]+\", \"2026-06-09\")");
    }

    [Fact]
    public void TestCase_LifecycleSynthesized()
    {
        // TestCase subclasses must have:
        //   - a generated constructor that calls Setup()
        //   - IDisposable.Dispose() that calls Teardown()
        // so xUnit's per-test instance creation triggers both hooks.
        var source = @"
from unittest import TestCase

class TestCalc(TestCase):
    x: int

    def setup(self):
        self.x = 42

    def teardown(self):
        pass

    @test
    def test_value(self):
        assert self.x == 42

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, requiresSharpyCore: true);
        // IDisposable implementation
        code.Should().Contain("global::System.IDisposable");
        // Constructor calls Setup()
        code.Should().MatchRegex(@"public\s+TestCalc\s*\(\s*\)");
        code.Should().Contain("Setup()");
        // Dispose() calls Teardown()
        code.Should().Contain("public void Dispose()");
        code.Should().Contain("Teardown()");
        // The @test method must carry the FactAttribute
        code.Should().Contain("Xunit.FactAttribute");
    }

    [Fact]
    public void ModuleLevelTestFunction_LiftedToSeparateTestClass()
    {
        // Module-level @test functions are lifted into a `{Module}Tests`
        // partial class so xUnit can discover and instantiate them.
        var source = @"
@test
def test_addition():
    x: int = 1 + 1
    assert x == 2

def main():
    print(2)
";
        var code = CompileToCSharp(source, fileName: "basic_test.spy");
        // The companion test class lives next to the main module class
        code.Should().Contain("BasicTestTests");
        // The @test function is emitted with [Xunit.FactAttribute] inside it
        code.Should().Contain("Xunit.FactAttribute");
        code.Should().Contain("TestAddition");
    }

    [Fact]
    public void TestDescription_PreservedInGeneratedCode()
    {
        // A description argument to @test must compile without errors and
        // should still produce a [Xunit.FactAttribute]-decorated method.
        var source = @"
@test(""verifies that addition works"")
def test_addition():
    assert 1 + 1 == 2

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.FactAttribute");
        code.Should().Contain("TestAddition");
    }

    #endregion

    #region @test.parametrize with variable reference (#836)

    [Fact]
    public void Parametrize_VariableReference_GeneratesMemberData()
    {
        // @test.parametrize(VARIABLE) emits [Theory] + a single [MemberData]
        // pointing at a generated wrapper property on the module class instead
        // of one [InlineData] per row.
        var source = @"
const TEST_DATA: list[tuple[int, int, int]] = [(1, 2, 3), (4, 5, 9)]

@test.parametrize(TEST_DATA)
def test_add(a: int, b: int, expected: int):
    assert a + b == expected

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, fileName: "param_var.spy");
        code.Should().Contain("Xunit.TheoryAttribute");
        code.Should().Contain(
            "Xunit.MemberDataAttribute(nameof(ParamVar.TestDataMemberData), MemberType = typeof(ParamVar))");
        // Wrapper property adapts list[tuple[...]] to xUnit's IEnumerable<object[]>
        code.Should().Contain(
            "public static global::System.Collections.Generic.IEnumerable<object[]> TestDataMemberData");
        code.Should().Contain("new object[] { row.Item1, row.Item2, row.Item3 }");
        code.Should().NotContain("InlineData");
    }

    [Fact]
    public void Parametrize_VariableReference_SingleParameter_WrapsEachElement()
    {
        // For non-tuple element types (single-parameter tests), each element is
        // wrapped directly into a one-element object array.
        var source = @"
const FLAGS: list[bool] = [True, False]

@test.parametrize(FLAGS)
def test_flag(flag: bool):
    assert flag == True or flag == False

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source, fileName: "param_single.spy");
        code.Should().Contain(
            "Xunit.MemberDataAttribute(nameof(ParamSingle.FLAGSMemberData), MemberType = typeof(ParamSingle))");
        code.Should().Contain("new object[] { row }");
        code.Should().NotContain("InlineData");
    }

    [Fact]
    public void Parametrize_UndefinedVariableReference_ReportsError()
    {
        // Referencing a name that doesn't exist must fail validation.
        var source = @"
@test.parametrize(MISSING_DATA)
def test_add(a: int, b: int, expected: int):
    assert a + b == expected

def main():
    print(""ok"")
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "param_missing.spy");
        result.Success.Should().BeFalse();
        result.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("references undefined variable 'MISSING_DATA'"));
    }

    #endregion

    #region isinstance assert rewriting (#841)

    [Fact]
    public void AssertIsinstance_UsesIsAssignableFrom()
    {
        var source = @"
@test
def test_type():
    x: object = 42
    assert isinstance(x, int)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.IsAssignableFrom<int>(x)");
        code.Should().NotContain("IsType");
    }

    [Fact]
    public void AssertIsinstance_NegatedForm_GeneratesAssertFalse()
    {
        var source = @"
@test
def test_not_type():
    x: object = ""hello""
    assert not isinstance(x, int)

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.False(x is int)");
    }

    [Fact]
    public void AssertIsinstance_TupleForm_GeneratesMultiTypeCheck()
    {
        var source = @"
@test
def test_multi_type():
    x: object = 42
    assert isinstance(x, (int, str))

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.True(x is int || x is string)");
    }

    [Fact]
    public void AssertIsinstance_NegatedTupleForm_GeneratesAssertFalse()
    {
        var source = @"
@test
def test_not_multi_type():
    x: object = 3.14
    assert not isinstance(x, (int, str))

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.Assert.False(x is int || x is string)");
    }

    #endregion

    #region tmp_path built-in per-test fixture (#842)

    [Fact]
    public void TmpPath_BuiltinFixture_EmitsPerTestFieldAndIDisposable()
    {
        var source = @"
@test
def test_writes(tmp_path: str):
    assert len(tmp_path) > 0

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        // Per-test instance field (not IClassFixture<T>), System.IDisposable, Dispose().
        code.Should().Contain(": global::System.IDisposable");
        code.Should().Contain(
            "private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();");
        code.Should().Contain("string tmpPath = _tmpPathFixture.Value;");
        code.Should().Contain("public void Dispose()");
        code.Should().Contain("_tmpPathFixture.Dispose();");
        // tmp_path is NOT an IClassFixture (that would share one dir across tests).
        code.Should().NotContain("IClassFixture<global::Sharpy.TmpPathFixture>");
    }

    [Fact]
    public void TmpPath_UserFixtureOverridesBuiltin()
    {
        // A user-defined @test.fixture named tmp_path wins: the IClassFixture path is used
        // and the built-in per-test field / IDisposable is NOT emitted.
        var source = @"
@test.fixture
def tmp_path() -> str:
    return ""/custom""

@test
def test_override(tmp_path: str):
    assert tmp_path == ""/custom""

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.IClassFixture<TmpPathFixture>");
        code.Should().NotContain("new global::Sharpy.TmpPathFixture()");
    }

    [Fact]
    public void TmpPath_ComposesWithUserFixture()
    {
        // A test consuming both tmp_path (built-in) and a user fixture gets both mechanisms:
        // IClassFixture<T> + ctor injection for the user fixture, and the per-test
        // TmpPathFixture field + IDisposable for tmp_path.
        var source = @"
@test.fixture
def greeting() -> str:
    return ""hi""

@test
def test_both(tmp_path: str, greeting: str):
    assert greeting == ""hi""
    assert len(tmp_path) > 0

def main():
    print(""ok"")
";
        var code = CompileToCSharp(source);
        code.Should().Contain("Xunit.IClassFixture<GreetingFixture>");
        code.Should().Contain("global::System.IDisposable");
        code.Should().Contain("new global::Sharpy.TmpPathFixture()");
        code.Should().Contain("string greeting = _greetingFixture.Value;");
        code.Should().Contain("string tmpPath = _tmpPathFixture.Value;");
        code.Should().Contain("_tmpPathFixture.Dispose();");
    }

    #endregion
}
