using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Edge case tests for overload resolution in the Sharpy compiler.
/// </summary>
[Collection("HeavyCompilation")]
public class OverloadResolutionTests : StdlibAwareIntegrationTestBase
{
    public OverloadResolutionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ExactArityPreference_SelectsTwoParamOverVariadic()
    {
        var source = @"
class Joiner:
    def __init__(self):
        pass

    def join(self, a: str, b: str) -> str:
        return a + ""-"" + b

    def join(self, *parts: str) -> str:
        result: str = """"
        for p in parts:
            if result != """":
                result = result + ""/""
            result = result + p
        return result

def main():
    j = Joiner()
    print(j.join(""x"", ""y""))
    print(j.join(""a"", ""b"", ""c""))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("x-y\na/b/c\n", result.StandardOutput);
    }

    [Fact]
    public void DefaultParameterExpansion_MatchesWithoutDefaults()
    {
        var source = @"
class Logger:
    def __init__(self):
        pass

    def log(self, msg: str) -> None:
        print(msg)

    def log(self, msg: str, level: int) -> None:
        print(f""[{level}] {msg}"")

def main():
    l = Logger()
    l.log(""hello"")
    l.log(""world"", 2)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("hello\n[2] world\n", result.StandardOutput);
    }

    [Fact]
    public void DuplicateSignature_ReportsAlreadyDefined()
    {
        var source = @"
class Converter:
    def __init__(self):
        pass

    def convert(self, x: int) -> str:
        return str(x)

    def convert(self, x: int) -> int:
        return x

def main():
    c = Converter()
    print(c.convert(42))
";
        var result = CompileAndExecute(source);
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("already defined"));
    }

    [Fact]
    public void VariadicOverload_CatchesExtraArgs()
    {
        // Exact 2-param overload is preferred; variadic catches 3+ args
        var source = @"
class Builder:
    def __init__(self):
        pass

    def build(self, a: str, b: str) -> str:
        return a + ""-"" + b

    def build(self, *args: str) -> str:
        result: str = """"
        for a in args:
            result = result + a
        return result

def main():
    b = Builder()
    print(b.build(""x"", ""y""))
    print(b.build(""a"", ""b"", ""c""))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("x-y\nabc\n", result.StandardOutput);
    }

    [Fact]
    public void OverloadByType_IntVsStr()
    {
        var source = @"
class Formatter:
    def __init__(self):
        pass

    def format(self, x: int) -> str:
        return f""int:{x}""

    def format(self, x: str) -> str:
        return f""str:{x}""

def main():
    f = Formatter()
    print(f.format(42))
    print(f.format(""hello""))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("int:42\nstr:hello\n", result.StandardOutput);
    }

    [Fact]
    public void IntVsFloat_PrefersExactIntOverload()
    {
        // Specificity-based disambiguation: int is more specific than float,
        // so the int overload is preferred when calling with an int argument.
        var source = @"
class MathOps:
    def __init__(self):
        pass

    def compute(self, x: int) -> int:
        return x * 2

    def compute(self, x: float) -> float:
        return x * 2.0

def main():
    m = MathOps()
    print(m.compute(5))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("10\n", result.StandardOutput);
    }

    [Fact]
    public void NoMatchingOverload_ReportsError()
    {
        var source = @"
class Calc:
    def __init__(self):
        pass

    def add(self, a: int, b: int) -> int:
        return a + b

def main():
    c = Calc()
    print(c.add(""a"", ""b""))
";
        var result = CompileAndExecute(source);
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e =>
            e.Contains("No matching overload") || e.Contains("type") || e.Contains("argument"));
    }

    [Fact]
    public void OverloadWithInheritance_SelectsCorrectOverload()
    {
        var source = @"
class Base:
    def __init__(self):
        pass

    def process(self, x: int) -> str:
        return f""base:{x}""

class Derived(Base):
    def __init__(self):
        super().__init__()

    def process(self, x: int) -> str:
        return f""derived:{x}""

    def process(self, x: int, y: int) -> str:
        return f""derived:{x},{y}""

def main():
    d = Derived()
    print(d.process(1))
    print(d.process(1, 2))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("derived:1\nderived:1,2\n", result.StandardOutput);
    }

    [Fact]
    public void OverloadThreeArity_SelectsByArgCount()
    {
        var source = @"
class Printer:
    def __init__(self):
        pass

    def show(self) -> None:
        print(""none"")

    def show(self, a: int) -> None:
        print(f""one:{a}"")

    def show(self, a: int, b: int) -> None:
        print(f""two:{a},{b}"")

def main():
    p = Printer()
    p.show()
    p.show(1)
    p.show(1, 2)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("none\none:1\ntwo:1,2\n", result.StandardOutput);
    }

    [Fact]
    public void OverloadWithBoolAndInt_DistinguishesTypes()
    {
        var source = @"
class Checker:
    def __init__(self):
        pass

    def check(self, x: bool) -> str:
        return f""bool:{x}""

    def check(self, x: int) -> str:
        return f""int:{x}""

def main():
    c = Checker()
    print(c.check(True))
    print(c.check(42))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("bool:True\nint:42\n", result.StandardOutput);
    }

    [Fact]
    public void KeywordArgDisambiguates_VariadicVsNamed()
    {
        // When a keyword argument (reverse=True) is passed, the overload with a
        // matching named parameter should be preferred over a params/variadic overload
        // that lacks that parameter name.
        var source = @"
class Sorter:
    def __init__(self):
        pass

    def merge(self, a: str, b: str) -> str:
        return a + b

    def merge(self, *parts: str) -> str:
        result: str = """"
        for p in parts:
            result = result + p
        return result

    def merge(self, a: str, b: str, reverse: bool) -> str:
        if reverse:
            return b + a
        return a + b

def main():
    s = Sorter()
    # Without keyword: exact arity picks 2-param overload
    print(s.merge(""x"", ""y""))
    # With keyword: keyword name 'reverse' disambiguates
    print(s.merge(""x"", ""y"", reverse=True))
    # Variadic: 3 positional args
    print(s.merge(""a"", ""b"", ""c""))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("xy\nyx\nabc\n", result.StandardOutput);
    }

    [Fact]
    public void StatisticsMean_ListInt_PrefersSpyOverload()
    {
        var source = @"
import statistics

def main() -> None:
    data: list[int] = [1, 2, 3, 4, 5]
    print(statistics.mean(data))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("3.0\n", result.StandardOutput);
    }

    private static string FormatErrors(ExecutionResult result)
    {
        return string.Join("\n", result.CompilationErrors);
    }
}
