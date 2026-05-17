using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for @dataclass code generation (RoslynEmitter.ClassMembers.Dataclass.cs).
/// Covers auto-property generation, constructor synthesis, Equals/GetHashCode synthesis,
/// ToString synthesis, and dataclass inheritance.
/// </summary>
[Collection("HeavyCompilation")]
public class RoslynEmitterDataclassTests : IntegrationTestBase
{
    public RoslynEmitterDataclassTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Dataclass_FieldEmission_ProducesProperties()
    {
        var result = CompileAndExecute(@"
@dataclass
class Point:
    x: float
    y: float

def main():
    p = Point(1.0, 2.0)
    print(p.x)
    print(p.y)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1.0\n2.0\n", result.StandardOutput);
    }

    [Fact]
    public void Dataclass_InitSynthesis_ConstructorWorksCorrectly()
    {
        var result = CompileAndExecute(@"
@dataclass
class Person:
    name: str
    age: int

def main():
    p = Person(""Alice"", 30)
    print(p.name)
    print(p.age)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Alice\n30\n", result.StandardOutput);
    }

    [Fact]
    public void Dataclass_EqSynthesis_EqualityWorksCorrectly()
    {
        var result = CompileAndExecute(@"
@dataclass
class Point:
    x: float
    y: float

def main():
    p1 = Point(1.0, 2.0)
    p2 = Point(1.0, 2.0)
    p3 = Point(3.0, 4.0)
    print(p1 == p2)
    print(p1 == p3)
    print(p1 != p3)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("True\nFalse\nTrue\n", result.StandardOutput);
    }

    [Fact]
    public void Dataclass_ToStringSynthesis_ProducesReadableOutput()
    {
        var result = CompileAndExecute(@"
@dataclass
class Point:
    x: float
    y: float

def main():
    p = Point(1.0, 2.0)
    print(p)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Point(x=1, y=2)\n", result.StandardOutput);
    }

    [Fact]
    public void Dataclass_WithInheritance_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
@dataclass
class Base:
    x: int

@dataclass
class Child(Base):
    y: int

def main():
    c = Child(1, 2)
    print(c)
    print(c.x)
    print(c.y)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Child(x=1, y=2)\n1\n2\n", result.StandardOutput);
    }

    [Fact]
    public void Dataclass_WithDefaultValues_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
@dataclass
class Config:
    host: str = ""localhost""
    port: int = 8080

def main():
    c1 = Config()
    print(c1.host)
    print(c1.port)
    c2 = Config(""example.com"", 443)
    print(c2.host)
    print(c2.port)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("localhost\n8080\nexample.com\n443\n", result.StandardOutput);
    }
}
