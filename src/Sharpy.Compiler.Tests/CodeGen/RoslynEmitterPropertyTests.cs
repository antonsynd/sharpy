using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for property and event code generation (RoslynEmitter.ClassMembers.Properties.cs).
/// Covers property getter/setter emission, read-only properties, auto-properties,
/// static properties, and event declarations.
/// </summary>
public class RoslynEmitterPropertyTests : IntegrationTestBase
{
    public RoslynEmitterPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Property_GetterAndSetter_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
class Person:
    _age: int

    def __init__(self, age: int):
        self._age = age

    property get age(self) -> int:
        return self._age

    property set age(self, value: int):
        if value < 0:
            self._age = 0
        else:
            self._age = value

def main():
    p = Person(25)
    print(p.age)
    p.age = -5
    print(p.age)
    p.age = 50
    print(p.age)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("25\n0\n50\n", result.StandardOutput);
    }

    [Fact]
    public void Property_ReadOnly_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
class Circle:
    _radius: float

    def __init__(self, radius: float):
        self._radius = radius

    property get radius(self) -> float:
        return self._radius

    property get diameter(self) -> float:
        return self._radius * 2.0

def main():
    c = Circle(5.0)
    print(c.radius)
    print(c.diameter)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("5.0\n10.0\n", result.StandardOutput);
    }

    [Fact]
    public void Property_AutoProperty_WithDefaultValue()
    {
        var result = CompileAndExecute(@"
class AppConfig:
    @static
    property version: str = ""1.0.0""

    @static
    property debug: bool = False

def main():
    print(AppConfig.version)
    print(AppConfig.debug)
    AppConfig.debug = True
    print(AppConfig.debug)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1.0.0\nFalse\nTrue\n", result.StandardOutput);
    }

    [Fact]
    public void Property_WithTypeAnnotation_GeneratesTypedProperty()
    {
        var result = CompileAndExecute(@"
class Counter:
    _count: int

    def __init__(self):
        self._count = 0

    property get count(self) -> int:
        return self._count

    def increment(self) -> None:
        self._count = self._count + 1

def main():
    c = Counter()
    print(c.count)
    c.increment()
    c.increment()
    print(c.count)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("0\n2\n", result.StandardOutput);
    }

    [Fact]
    public void Event_AutoEvent_WithDelegate_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
delegate Callback(message: str) -> None

class Button:
    label: str
    event on_click: Callback

    def __init__(self, label: str):
        self.label = label

    def click(self) -> None:
        self.on_click?.invoke(self.label)

def handle_click(message: str) -> None:
    print(""Clicked: "" + message)

def main():
    btn = Button(""Submit"")
    btn.on_click += handle_click
    btn.click()
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Clicked: Submit\n", result.StandardOutput);
    }

    [Fact]
    public void Event_SubscribeAndUnsubscribe_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
delegate Notify(value: int) -> None

class Sensor:
    event on_change: Notify

    def update(self, value: int) -> None:
        self.on_change?.invoke(value)

def logger(value: int) -> None:
    print(""Logged: "" + str(value))

def main():
    s = Sensor()
    s.on_change += logger
    s.update(42)
    s.on_change -= logger
    s.update(99)
    print(""done"")
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Logged: 42\ndone\n", result.StandardOutput);
    }
}
