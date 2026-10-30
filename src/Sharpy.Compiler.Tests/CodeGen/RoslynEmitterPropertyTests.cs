using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;

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

    /// <summary>
    /// A lone auto set half (<c>property set level: int</c>, properties.md's write-only auto
    /// property) has no C# auto shape — CS8051 — so it emits a private backing field named by the
    /// mixed and observer arms' authority and a set-only property whose set accessor writes it
    /// (#2395, R-JS (a)). Asserted on the emitted syntax nodes: nothing reads a write-only property,
    /// so the shape is the discriminating observation.
    /// </summary>
    [Theory]
    [InlineData("class")]
    [InlineData("struct")]
    public void Property_LoneAutoSetHalf_EmitsBackingFieldAndSetOnlyProperty(string host)
    {
        var type = EmittedType($"{host} Host:\n    property set level: int\n", "Host");

        var field = Assert.Single(type.Members.OfType<FieldDeclarationSyntax>());
        Assert.Equal(new[] { SyntaxKind.PrivateKeyword }, field.Modifiers.Select(m => m.Kind()));
        var fieldName = Assert.Single(field.Declaration.Variables).Identifier.Text;
        Assert.Equal("_Level", fieldName);
        Assert.Null(field.Declaration.Variables[0].Initializer);

        var property = Assert.Single(type.Members.OfType<PropertyDeclarationSyntax>());
        Assert.Equal("Level", property.Identifier.Text);
        Assert.Null(property.Initializer);
        var setter = Assert.Single(property.AccessorList!.Accessors);
        Assert.Equal(SyntaxKind.SetAccessorDeclaration, setter.Kind());
        var assignment = Assert.IsType<AssignmentExpressionSyntax>(setter.ExpressionBody?.Expression);
        Assert.Equal(SyntaxKind.SimpleAssignmentExpression, assignment.Kind());
        Assert.Equal(fieldName, Assert.IsType<IdentifierNameSyntax>(assignment.Left).Identifier.Text);
        Assert.Equal("value", Assert.IsType<IdentifierNameSyntax>(assignment.Right).Identifier.Text);
    }

    /// <summary>The lone set half's default initializes its backing field, and a @static half's field is
    /// static (#2395).</summary>
    [Fact]
    public void Property_LoneAutoSetHalf_DefaultAndStaticGoOnTheField()
    {
        var type = EmittedType(
            "class Host:\n    property set level: int = 6\n    @static\n    property set shared: int\n", "Host");

        var fields = type.Members.OfType<FieldDeclarationSyntax>()
            .ToDictionary(f => f.Declaration.Variables.Single().Identifier.Text);
        Assert.Equal(new[] { "_Level", "_Shared" }, fields.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("6", fields["_Level"].Declaration.Variables[0].Initializer?.Value.ToString());
        Assert.DoesNotContain(fields["_Level"].Modifiers, m => m.IsKind(SyntaxKind.StaticKeyword));
        Assert.Contains(fields["_Shared"].Modifiers, m => m.IsKind(SyntaxKind.StaticKeyword));
        Assert.All(type.Members.OfType<PropertyDeclarationSyntax>(), p => Assert.Null(p.Initializer));
    }

    /// <summary>Positive control for the #2395 shape test: the split auto pair
    /// (<c>property get</c> + <c>property set</c>) is still ONE auto property <c>{ get; set; }</c> with no
    /// synthesized field.</summary>
    [Fact]
    public void Property_SplitAutoPair_StaysOneAutoProperty()
    {
        var type = EmittedType("class Host:\n    property get level: int\n    property set level: int\n", "Host");

        Assert.Empty(type.Members.OfType<FieldDeclarationSyntax>());
        var property = Assert.Single(type.Members.OfType<PropertyDeclarationSyntax>());
        Assert.Equal(
            new[] { SyntaxKind.GetAccessorDeclaration, SyntaxKind.SetAccessorDeclaration },
            property.AccessorList!.Accessors.Select(a => a.Kind()));
        Assert.All(property.AccessorList.Accessors, a =>
        {
            Assert.Null(a.Body);
            Assert.Null(a.ExpressionBody);
        });
    }

    /// <summary>Interface control for #2395: an interface's lone set half stays the abstract
    /// <c>{ set; }</c>, legal C#, with no field.</summary>
    [Fact]
    public void Property_InterfaceLoneAutoSetHalf_StaysAbstract()
    {
        var type = EmittedType("interface IHost:\n    property set level: int\n", "IHost");

        Assert.Empty(type.Members.OfType<FieldDeclarationSyntax>());
        var property = Assert.Single(type.Members.OfType<PropertyDeclarationSyntax>());
        var setter = Assert.Single(property.AccessorList!.Accessors);
        Assert.Equal(SyntaxKind.SetAccessorDeclaration, setter.Kind());
        Assert.Null(setter.Body);
        Assert.Null(setter.ExpressionBody);
    }

    private static TypeDeclarationSyntax EmittedType(string source, string typeName)
        => EmitterTestPipeline.EmitCompilationUnit(source, requireNoErrors: true)
            .DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Single(t => t.Identifier.Text == typeName);
}
