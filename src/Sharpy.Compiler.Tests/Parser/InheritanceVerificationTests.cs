using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Comprehensive verification tests for class inheritance parsing.
/// These tests demonstrate that the parser correctly handles:
/// 1. Single inheritance (class Dog(Animal):)
/// 2. Multiple inheritance (class Dog(Animal, ICanine):)
/// 3. Generic base classes with type arguments
/// </summary>
public class InheritanceVerificationTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = new List<LexerNs.Token>();
        while (true)
        {
            var token = lexer.NextToken();
            tokens.Add(token);
            if (token.Type == LexerNs.TokenType.Eof)
                break;
        }
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void ParseSingleInheritance_DogAnimal()
    {
        // Arrange: The exact example from the task specification
        var source = @"
class Dog(Animal):
    pass
";

        // Act: Parse the code
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        // Assert: Verify all aspects of the ClassDef node
        classDef.Name.Should().Be("Dog");
        classDef.BaseClasses.Should().HaveCount(1);
        classDef.BaseClasses[0].Name.Should().Be("Animal");
        classDef.BaseClasses[0].TypeArguments.Should().BeEmpty();
        classDef.BaseClasses[0].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleInheritance_EmployeePerson()
    {
        var source = @"
class Employee(Person):
    def __init__(self, name: str, id: int):
        pass
";

        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.Name.Should().Be("Employee");
        classDef.BaseClasses.Should().HaveCount(1);
        classDef.BaseClasses[0].Name.Should().Be("Person");
    }

    [Fact]
    public void ParseMultipleInheritance_DogWithInterface()
    {
        // Multiple base classes: one concrete base + interface(s)
        var source = @"
class Dog(Animal, ICanine, IPet):
    def bark(self) -> None:
        pass
";

        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.Name.Should().Be("Dog");
        classDef.BaseClasses.Should().HaveCount(3);
        classDef.BaseClasses[0].Name.Should().Be("Animal");
        classDef.BaseClasses[1].Name.Should().Be("ICanine");
        classDef.BaseClasses[2].Name.Should().Be("IPet");
    }

    [Fact]
    public void ParseInheritanceWithGenericBase()
    {
        // Base class with type arguments
        var source = @"
class StringList(List[str]):
    pass
";

        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.Name.Should().Be("StringList");
        classDef.BaseClasses.Should().HaveCount(1);
        classDef.BaseClasses[0].Name.Should().Be("List");
        classDef.BaseClasses[0].TypeArguments.Should().HaveCount(1);
        classDef.BaseClasses[0].TypeArguments[0].Name.Should().Be("str");
    }

    [Fact]
    public void ParseNoInheritance()
    {
        // Class with no base classes - BaseClasses should be empty
        var source = @"
class Animal:
    pass
";

        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.Name.Should().Be("Animal");
        classDef.BaseClasses.Should().BeEmpty();
    }

    [Fact]
    public void ParseComplexInheritanceScenario()
    {
        // Multiple classes with different inheritance patterns
        var source = @"
class Animal:
    pass

class Dog(Animal):
    pass

class Husky(Dog, IWorkingDog):
    pass
";

        var module = Parse(source);

        // Animal: no base classes
        var animalClass = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        animalClass.Name.Should().Be("Animal");
        animalClass.BaseClasses.Should().BeEmpty();

        // Dog: inherits from Animal
        var dogClass = module.Body[1].Should().BeOfType<ClassDef>().Subject;
        dogClass.Name.Should().Be("Dog");
        dogClass.BaseClasses.Should().HaveCount(1);
        dogClass.BaseClasses[0].Name.Should().Be("Animal");

        // Husky: inherits from Dog and implements IWorkingDog
        var huskyClass = module.Body[2].Should().BeOfType<ClassDef>().Subject;
        huskyClass.Name.Should().Be("Husky");
        huskyClass.BaseClasses.Should().HaveCount(2);
        huskyClass.BaseClasses[0].Name.Should().Be("Dog");
        huskyClass.BaseClasses[1].Name.Should().Be("IWorkingDog");
    }

    [Fact]
    public void ParseInheritanceWithTypeParameters()
    {
        // Generic class with inheritance
        var source = @"
class Repository[T](IRepository[T]):
    pass
";

        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.Name.Should().Be("Repository");
        classDef.TypeParameters.Should().HaveCount(1);
        classDef.TypeParameters[0].Name.Should().Be("T");
        classDef.BaseClasses.Should().HaveCount(1);
        classDef.BaseClasses[0].Name.Should().Be("IRepository");
        classDef.BaseClasses[0].TypeArguments.Should().HaveCount(1);
        classDef.BaseClasses[0].TypeArguments[0].Name.Should().Be("T");
    }
}
