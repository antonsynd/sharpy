using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class CompilerServicesIntegrationTests
{
    [Fact]
    public void TypeChecker_WithCompilerServices_WorksCorrectly()
    {
        // Arrange
        var source = @"
x: int = 42
y: str = ""hello""
";
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var services = CompilerServicesBuilder.CreateForTesting();

        var nameResolver = new NameResolver(services.SymbolTable);
        nameResolver.ResolveDeclarations(module);

        // Act
        var typeChecker = new TypeChecker(services);
        typeChecker.CheckModule(module);

        // Assert
        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void TypeChecker_WithCompilerServices_CollectsErrors()
    {
        // Arrange - intentional type error
        var source = @"
x: int = ""not an int""
";
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var services = CompilerServicesBuilder.CreateForTesting();

        var nameResolver = new NameResolver(services.SymbolTable);
        nameResolver.ResolveDeclarations(module);

        // Act
        var typeChecker = new TypeChecker(services);
        typeChecker.CheckModule(module);

        // Assert
        Assert.NotEmpty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void SemanticContext_WithCompilerServices_SharesState()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        services.CurrentFilePath = "/test/file.spy";

        // Act
        var context = new SemanticContext(services);

        // Assert
        Assert.Same(services.SymbolTable, context.SymbolTable);
        Assert.Same(services.SemanticInfo, context.SemanticInfo);
        Assert.Same(services.DiagnosticReporter.Diagnostics, context.Diagnostics);
        Assert.Equal("/test/file.spy", context.CurrentFilePath);
    }

    [Fact]
    public void FullCompilation_WithCompilerServices_ProducesValidOutput()
    {
        // Arrange
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main():
    result: int = add(1, 2)
";

        // Act
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        // Assert
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.NotNull(result.GeneratedCSharpCode);
        Assert.Contains("public static int Add", result.GeneratedCSharpCode);
    }

    [Fact]
    public void SemanticContext_CreatedFromCompilerServices_HasCorrectConfiguration()
    {
        // Arrange
        var config = new CompilerServicesConfiguration
        {
            MaxErrors = 50,
            ContinueAfterErrors = false
        };
        var builtinRegistry = new BuiltinRegistry();
        var services = new CompilerServicesBuilder()
            .WithConfiguration(config)
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo())
            .Build();

        // Act
        var context = new SemanticContext(services);

        // Assert
        Assert.Equal(50, context.MaxErrors);
        Assert.False(context.ContinueAfterErrors);
    }

    [Fact]
    public void TypeChecker_CreateSemanticContext_UsesCompilerServicesWhenAvailable()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        services.CurrentFilePath = "/my/file.spy";
        var typeChecker = new TypeChecker(services);

        // Act
        var context = typeChecker.CreateSemanticContext();

        // Assert
        Assert.NotNull(context.Services);
        Assert.Same(services, context.Services);
        Assert.Equal("/my/file.spy", context.CurrentFilePath);
    }

    [Fact]
    public void TypeChecker_WithoutCompilerServices_CreateSemanticContextStillWorks()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);

        // Act
        var context = typeChecker.CreateSemanticContext();

        // Assert
        Assert.Null(context.Services);
        Assert.Same(symbolTable, context.SymbolTable);
    }

    [Fact]
    public void CompilerServices_UsedWithComplexCode_WorksCorrectly()
    {
        // Arrange - more complex code with classes and methods
        var source = @"
class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def greet(self) -> str:
        return ""Hello, "" + self.name

p: Person = Person(""Alice"", 30)
greeting: str = p.greet()
";
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var services = CompilerServicesBuilder.CreateForTesting();

        var nameResolver = new NameResolver(services.SymbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Act
        var typeChecker = new TypeChecker(services);
        typeChecker.CheckModule(module);

        // Assert
        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }
}
