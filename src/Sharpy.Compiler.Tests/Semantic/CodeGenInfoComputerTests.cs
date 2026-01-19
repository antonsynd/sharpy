using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Xunit;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.Semantic;

public class CodeGenInfoComputerTests
{
    private (Module module, SymbolTable symbolTable) ParseAndResolve(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Run type checker to fully populate symbols
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module);

        return (module, symbolTable);
    }

    [Fact]
    public void ComputeForModule_ModuleLevelVariable_SetsPascalCaseName()
    {
        var source = @"
my_variable: int = 42
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("my_variable") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("MyVariable");
        symbol.CodeGenInfo.OriginalName.Should().Be("my_variable");
        symbol.CodeGenInfo.IsModuleLevel.Should().BeTrue();
        symbol.CodeGenInfo.IsConstant.Should().BeFalse();
    }

    [Fact]
    public void ComputeForModule_ModuleLevelVariable_WithUppercaseName_UsesPascalCase()
    {
        // Without const keyword, even ALL_CAPS names are treated as regular variables
        // and get PascalCase conversion
        var source = @"
MAX_VALUE: int = 100
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("MAX_VALUE") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        // Without const keyword, this becomes PascalCase like other module-level variables
        symbol.CodeGenInfo!.CSharpName.Should().Be("MaxValue");
        symbol.CodeGenInfo.IsModuleLevel.Should().BeTrue();
        symbol.CodeGenInfo.IsConstant.Should().BeFalse();
    }

    [Fact]
    public void ComputeForModule_ConstDeclaration_SetsConstantFlags()
    {
        // Using const keyword (if supported)
        var source = @"
const MY_CONST: int = 100
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("MY_CONST") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("MY_CONST");
        symbol.CodeGenInfo.IsModuleLevel.Should().BeTrue();
        symbol.CodeGenInfo.IsConstant.Should().BeTrue();
    }

    [Fact]
    public void ComputeForModule_ClassDefinition_SetsPascalCaseName()
    {
        var source = @"
class my_class:
    pass
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("my_class") as TypeSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("MyClass");
    }

    [Fact]
    public void ComputeForModule_ClassWithPascalCaseName_PreservesName()
    {
        var source = @"
class MyClass:
    pass
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("MyClass") as TypeSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("MyClass");
    }

    [Fact]
    public void ComputeForModule_FunctionDefinition_SetsPascalCaseName()
    {
        var source = @"
def my_function() -> None:
    pass
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("my_function") as FunctionSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("MyFunction");
    }

    [Fact]
    public void ComputeForModule_Interface_PreservesExactName()
    {
        var source = @"
interface IMyInterface:
    def do_something(self) -> None: ...
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("IMyInterface") as TypeSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("IMyInterface");
    }

    [Fact]
    public void ComputeForModule_Struct_SetsPascalCaseName()
    {
        var source = @"
struct my_struct:
    x: int
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("my_struct") as TypeSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("MyStruct");
    }

    [Fact]
    public void ComputeForModule_Enum_SetsPascalCaseName()
    {
        var source = @"
enum color:
    RED
    GREEN
    BLUE
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("color") as TypeSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.CSharpName.Should().Be("Color");
    }

    [Fact]
    public void ComputeForModule_ClassField_SetsCamelCaseName()
    {
        var source = @"
class MyClass:
    my_field: int
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var typeSymbol = symbolTable.Lookup("MyClass") as TypeSymbol;
        typeSymbol.Should().NotBeNull();

        var fieldSymbol = typeSymbol!.Fields.FirstOrDefault(f => f.Name == "my_field");
        fieldSymbol.Should().NotBeNull();
        fieldSymbol!.CodeGenInfo.Should().NotBeNull();
        fieldSymbol.CodeGenInfo!.CSharpName.Should().Be("myField");
    }

    [Fact]
    public void ComputeForModule_ClassMethod_SetsPascalCaseName()
    {
        var source = @"
class MyClass:
    def my_method(self) -> None:
        pass
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var typeSymbol = symbolTable.Lookup("MyClass") as TypeSymbol;
        typeSymbol.Should().NotBeNull();

        var methodSymbol = typeSymbol!.Methods.FirstOrDefault(m => m.Name == "my_method");
        methodSymbol.Should().NotBeNull();
        methodSymbol!.CodeGenInfo.Should().NotBeNull();
        methodSymbol.CodeGenInfo!.CSharpName.Should().Be("MyMethod");
    }

    [Fact]
    public void ComputeForModule_ModuleLevelVariable_WithFunctionCall_HasExecutionOrderIssues()
    {
        var source = @"
def get_value() -> int:
    return 42

result: int = get_value()
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("result") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.HasExecutionOrderIssues.Should().BeTrue();
    }

    [Fact]
    public void ComputeForModule_ModuleLevelVariable_WithLiteral_NoExecutionOrderIssues()
    {
        var source = @"
value: int = 42
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("value") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.HasExecutionOrderIssues.Should().BeFalse();
    }

    [Fact]
    public void ComputeForModule_ModuleLevelConstant_NeverHasExecutionOrderIssues()
    {
        var source = @"
const CONST_VALUE: int = 100
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);

        computer.ComputeForModule(module);

        var symbol = symbolTable.Lookup("CONST_VALUE") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.CodeGenInfo.Should().NotBeNull();
        symbol.CodeGenInfo!.HasExecutionOrderIssues.Should().BeFalse();
    }
}
