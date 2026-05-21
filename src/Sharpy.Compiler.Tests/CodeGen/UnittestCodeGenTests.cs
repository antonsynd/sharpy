using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for the unittest framework code generation transforms:
///
/// 1. Assert rewriting inside <c>@test</c> functions (Plan Task 20):
///    each Python-style <c>assert</c> is rewritten to a specialized
///    <c>Xunit.Assert.*</c> call (Equal, NotEqual, Null, NotNull, Contains,
///    DoesNotContain, IsType, False, True). Outside <c>@test</c>, asserts
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
    public void AssertRewrite_Isinstance_GeneratesXunitAssertIsType()
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
        code.Should().Contain("Xunit.Assert.IsType<int>(x)");
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
        code.Should().Contain("System.IDisposable");
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
}
