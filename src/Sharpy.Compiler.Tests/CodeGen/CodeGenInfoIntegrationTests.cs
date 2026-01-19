using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Validation;
using Xunit;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Integration tests that verify CodeGenInfo is correctly computed and attached to symbols
/// when UsePrecomputedCodeGenInfo is enabled.
/// </summary>
public class CodeGenInfoIntegrationTests
{
    private (Module module, SymbolTable symbolTable) CompileWithCodeGenInfo(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var pipeline = ValidationPipelineFactory.CreateDefault(NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance, pipeline);

        // Enable CodeGenInfo computation
        typeChecker.CheckModule(module, computeCodeGenInfo: true);

        return (module, symbolTable);
    }

    [Fact]
    public void ModuleLevelVariable_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
my_variable: int = 42
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var symbol = symbolTable.Lookup("my_variable") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull("CodeGenInfo should be computed when flag is enabled");
        symbol.CodeGenInfo!.CSharpName.Should().Be("MyVariable");
        symbol.CodeGenInfo.IsModuleLevel.Should().BeTrue();
    }

    [Fact]
    public void ModuleLevelConstant_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
const MAX_VALUE: int = 100
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var symbol = symbolTable.Lookup("MAX_VALUE") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("MAX_VALUE");
        symbol.CodeGenInfo.IsConstant.Should().BeTrue();
    }

    [Fact]
    public void ClassDefinition_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
class my_class:
    x: int

    def __init__(self, x: int):
        self.x = x
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var typeSymbol = symbolTable.Lookup("my_class") as TypeSymbol;
        typeSymbol.Should().NotBeNull();
        typeSymbol!.CodeGenInfo.Should().NotBeNull();
        typeSymbol.CodeGenInfo!.CSharpName.Should().Be("MyClass");
    }

    [Fact]
    public void FunctionDefinition_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
def add_numbers(a: int, b: int) -> int:
    return a + b
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var funcSymbol = symbolTable.Lookup("add_numbers") as FunctionSymbol;
        funcSymbol.Should().NotBeNull();
        funcSymbol!.CodeGenInfo.Should().NotBeNull();
        funcSymbol.CodeGenInfo!.CSharpName.Should().Be("AddNumbers");
    }

    [Fact]
    public void Enum_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
enum color:
    RED
    GREEN
    BLUE
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var typeSymbol = symbolTable.Lookup("color") as TypeSymbol;
        typeSymbol.Should().NotBeNull();
        typeSymbol!.CodeGenInfo.Should().NotBeNull();
        typeSymbol.CodeGenInfo!.CSharpName.Should().Be("Color");
    }

    [Fact]
    public void Interface_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None: ...
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var typeSymbol = symbolTable.Lookup("IDrawable") as TypeSymbol;
        typeSymbol.Should().NotBeNull();
        typeSymbol!.CodeGenInfo.Should().NotBeNull();
        typeSymbol.CodeGenInfo!.CSharpName.Should().Be("IDrawable");
    }

    [Fact]
    public void Struct_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
struct point:
    x: int
    y: int
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var typeSymbol = symbolTable.Lookup("point") as TypeSymbol;
        typeSymbol.Should().NotBeNull();
        typeSymbol!.CodeGenInfo.Should().NotBeNull();
        typeSymbol.CodeGenInfo!.CSharpName.Should().Be("Point");
    }

    [Fact]
    public void VariableWithFunctionCall_NoExecutionOrderIssues()
    {
        // A variable initialized with a function call that's defined earlier
        // does NOT have execution order issues - it can be a static field
        var source = @"
def get_value() -> int:
    return 42

result: int = get_value()
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var symbol = symbolTable.Lookup("result") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        // Function is defined before variable, so no execution order issues
        symbol.CodeGenInfo!.HasExecutionOrderIssues.Should().BeFalse(
            "Variable initialized with function call defined earlier should NOT have execution order issues");
        symbol.CodeGenInfo.IsModuleLevel.Should().BeTrue(
            "Variable should be emitted as a static field");
    }

    [Fact]
    public void VariableWithAssignmentBeforeDeclaration_HasExecutionOrderIssues()
    {
        // Assignment before declaration is a true execution order issue
        var source = @"
x = 5
x: int = 10
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var symbol = symbolTable.Lookup("x") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.HasExecutionOrderIssues.Should().BeTrue(
            "Assignment before declaration is an execution order issue");
        symbol.CodeGenInfo.IsModuleLevel.Should().BeFalse(
            "Variable should be emitted as local in Main()");
    }

    [Fact]
    public void VariableReferencingAssignmentVariable_HasExecutionOrderIssues()
    {
        // Referencing an assignment variable (no type annotation) is an execution order issue
        var source = @"
x = 5
y: int = x
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var symbol = symbolTable.Lookup("y") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.HasExecutionOrderIssues.Should().BeTrue(
            "Referencing an assignment variable is an execution order issue");
        symbol.CodeGenInfo.IsModuleLevel.Should().BeFalse(
            "Variable should be emitted as local in Main()");
    }

    [Fact]
    public void ClassField_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
class MyClass:
    my_field: int
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var typeSymbol = symbolTable.Lookup("MyClass") as TypeSymbol;
        typeSymbol.Should().NotBeNull();

        var fieldSymbol = typeSymbol!.Fields.FirstOrDefault(f => f.Name == "my_field");
        fieldSymbol.Should().NotBeNull();
        fieldSymbol!.CodeGenInfo.Should().NotBeNull();
        fieldSymbol.CodeGenInfo!.CSharpName.Should().Be("myField");
    }

    [Fact]
    public void ClassMethod_HasCodeGenInfo_WhenFlagEnabled()
    {
        var source = @"
class MyClass:
    def my_method(self) -> None:
        pass
";
        var (module, symbolTable) = CompileWithCodeGenInfo(source);

        var typeSymbol = symbolTable.Lookup("MyClass") as TypeSymbol;
        typeSymbol.Should().NotBeNull();

        var methodSymbol = typeSymbol!.Methods.FirstOrDefault(m => m.Name == "my_method");
        methodSymbol.Should().NotBeNull();
        methodSymbol!.CodeGenInfo.Should().NotBeNull();
        methodSymbol.CodeGenInfo!.CSharpName.Should().Be("MyMethod");
    }
}
