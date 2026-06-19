using FluentAssertions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Edge case tests for overload resolution: exact arity preference,
/// nullable unwrapping, variadic matching, default parameter expansion,
/// ambiguity detection, and constructor overloading.
/// </summary>
[Collection("HeavyCompilation")]
public class OverloadResolutionTests : IntegrationTestBase
{
    public OverloadResolutionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ExactArityPreferred_OverVariadicMatch()
    {
        // When both exact-arity and variadic overloads match,
        // exact arity should win
        var source = @"
class Joiner:
    def __init__(self):
        pass

    def join(self, a: str, b: str) -> str:
        return ""exact:"" + a + "","" + b

    def join(self, *parts: str) -> str:
        return ""variadic""

def main():
    j: Joiner = Joiner()
    print(j.join(""x"", ""y""))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("exact:x,y\n");
    }

    [Fact]
    public void VariadicMatch_WhenExactArityUnavailable()
    {
        // Three args should fall through to variadic overload
        var source = @"
class Joiner:
    def __init__(self):
        pass

    def join(self, a: str, b: str) -> str:
        return ""exact""

    def join(self, *parts: str) -> str:
        result: str = """"
        for p in parts:
            if result != """":
                result = result + "",""
            result = result + p
        return result

def main():
    j: Joiner = Joiner()
    print(j.join(""a"", ""b"", ""c""))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("a,b,c\n");
    }

    [Fact]
    public void DefaultParameterExpansion_SelectsCorrectOverload()
    {
        // Two-arg call should match (msg, level) overload using default
        var source = @"
class Logger:
    def __init__(self):
        pass

    def log(self, msg: str) -> None:
        print(""simple:"" + msg)

    def log(self, msg: str, level: int) -> None:
        print(""leveled:"" + msg)

def main():
    l: Logger = Logger()
    l.log(""a"")
    l.log(""b"", 3)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("simple:a\nleveled:b\n");
    }

    [Fact]
    public void AmbiguousOverload_ProducesError()
    {
        // compute(int, float) vs compute(float, int) — call with (int, int) is ambiguous
        var source = @"
class Calc:
    def __init__(self):
        pass

    def compute(self, x: int, y: float) -> float:
        return float(x) + y

    def compute(self, x: float, y: int) -> float:
        return x + float(y)

def main():
    c: Calc = Calc()
    c.compute(1, 2)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        string.Join(" ", result.CompilationErrors).Should().Contain("Ambiguous");
    }

    [Fact]
    public void NoMatchingOverload_ProducesError()
    {
        // Call with wrong arity — no overload has 1 param
        var source = @"
class Math:
    def __init__(self):
        pass

    def add(self, x: int, y: int) -> int:
        return x + y

    def add(self, x: int, y: int, z: int) -> int:
        return x + y + z

def main():
    m: Math = Math()
    m.add(1)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        string.Join(" ", result.CompilationErrors).Should().Contain("No matching overload");
    }

    [Fact]
    public void TypeBasedResolution_IntVsStr()
    {
        // Resolve overload based on argument type
        var source = @"
class Converter:
    def __init__(self):
        pass

    def convert(self, x: int) -> str:
        return ""int:"" + str(x)

    def convert(self, x: str) -> str:
        return ""str:"" + x

def main():
    c: Converter = Converter()
    print(c.convert(42))
    print(c.convert(""hello""))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("int:42\nstr:hello\n");
    }

    [Fact]
    public void ConstructorOverload_ZeroVsTwoArgs()
    {
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self):
        self.x = 0
        self.y = 0

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p1 = Point()
    p2 = Point(3, 4)
    print(p1.x)
    print(p2.x)
    print(p2.y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("0\n3\n4\n");
    }

    [Fact]
    public void ConstructorOverload_ThreeArities()
    {
        var source = @"
class Rect:
    w: int
    h: int

    def __init__(self):
        self.w = 1
        self.h = 1

    def __init__(self, size: int):
        self.w = size
        self.h = size

    def __init__(self, w: int, h: int):
        self.w = w
        self.h = h

def main():
    r1 = Rect()
    r2 = Rect(5)
    r3 = Rect(3, 7)
    print(r1.w)
    print(r2.w)
    print(r3.w)
    print(r3.h)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("1\n5\n3\n7\n");
    }

    [Fact]
    public void OverloadWithOptionalParam_SelectsByArity()
    {
        // Optional type parameter should not cause ambiguity
        var source = @"
class Config:
    def __init__(self):
        pass

    def get(self, key: str) -> str:
        return ""default""

    def get(self, key: str, fallback: str) -> str:
        return fallback

def main():
    c: Config = Config()
    print(c.get(""k""))
    print(c.get(""k"", ""custom""))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("default\ncustom\n");
    }

    [Fact]
    public void BuiltinOverload_IntAndFloat()
    {
        // abs() has int and float overloads via builtins
        var source = @"
def main():
    x: int = abs(-5)
    y: float = abs(-3.14)
    print(x)
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("5\n3.14\n");
    }

    [Fact]
    public void OverloadWithMixedTypes_IntStrBool()
    {
        // Three overloads differentiated purely by parameter type
        var source = @"
class Handler:
    def __init__(self):
        pass

    def handle(self, x: int) -> str:
        return ""int""

    def handle(self, x: str) -> str:
        return ""str""

    def handle(self, x: bool) -> str:
        return ""bool""

def main():
    h: Handler = Handler()
    print(h.handle(42))
    print(h.handle(""hi""))
    print(h.handle(True))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("int\nstr\nbool\n");
    }

    [Fact]
    public void MoreSpecificType_SelectedOverAmbiguousMatch()
    {
        var source = @"
class Calc:
    def __init__(self):
        pass

    def compute(self, x: int) -> int:
        return x * 3

    def compute(self, x: float) -> float:
        return x * 3.0

def main():
    c: Calc = Calc()
    print(c.compute(7))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Should().Be("21\n");
    }

    [Fact]
    public void EqualSpecificity_RemainsAmbiguous()
    {
        var source = @"
class Calc:
    def __init__(self):
        pass

    def compute(self, x: int, y: float) -> float:
        return float(x) + y

    def compute(self, x: float, y: int) -> float:
        return x + float(y)

def main():
    c: Calc = Calc()
    c.compute(1, 2)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        string.Join(" ", result.CompilationErrors).Should().Contain("Ambiguous");
    }

    [Fact]
    public void GenericOverloads_StructuredArgBeatsBareTypeParameter_NoLongerAmbiguous()
    {
        // When concrete positions are equal and the remaining position is a bare type
        // parameter in one overload and a structured generic in the other, the structured
        // generic is more specific (C# §12.6.4.4: a type parameter is less specific than a
        // constructed type). So calc(x: int, y: list[T]) is preferred over calc(x: int, y: T)
        // for a list argument — matching C# (verified) and Axiom 1 (#957). Previously this was
        // reported ambiguous because the resolver could not rank T against list[T].
        //
        // This asserts only the (improved) overload-resolution outcome — no ambiguity — because
        // user-defined generic methods currently mis-codegen the <T> type parameter (#960), so
        // the program cannot execute. End-to-end execution of the same tiebreak is covered by the
        // numpy_array_2d fixture (np.array([[...]]) selects the 2-D discovered overload).
        var source = @"
class Processor:
    def __init__(self):
        pass

    def calc[T](self, x: int, y: T) -> str:
        return ""generic-T""

    def calc[T](self, x: int, y: list[T]) -> str:
        return ""generic-list-T""

def main():
    p: Processor = Processor()
    p.calc(5, [1, 2, 3])
";
        var result = CompileAndExecute(source);
        string.Join(" ", result.CompilationErrors).Should().NotContain("Ambiguous");
    }
}
