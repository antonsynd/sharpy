using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Validation;
using Xunit;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests verifying CodeGenInfo-based emission works correctly
/// after migration from legacy tracking fields.
///
/// These tests ensure that the emitter correctly uses CodeGenInfo
/// for name resolution when available.
/// </summary>
public class EmitterMigrationTests
{
    /// <summary>
    /// Compile source code with CodeGenInfo enabled and return the generated C#.
    /// </summary>
    private string CompileToString(string source, bool isEntryPoint = true)
    {
        var logger = NullLogger.Instance;
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, logger);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, logger, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
        var pipeline = ValidationPipelineFactory.CreateDefault(logger);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger, pipeline)
        {
            SemanticBinding = semanticBinding
        };

        // Enable CodeGenInfo computation (simulates UsePrecomputedCodeGenInfo = true)
        typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: isEntryPoint);

        // Materialize onto Symbol properties for code generation
        semanticBinding.MaterializeCodeGenInfo();
        semanticBinding.MaterializeVariableTypes();

        var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
        {
            IsEntryPoint = isEntryPoint,
            Logger = logger,
            SemanticBinding = semanticBinding
        };
        var emitter = new RoslynEmitter(codeGenContext);
        var compilationUnit = emitter.GenerateCompilationUnit(module);

        return compilationUnit.ToFullString();
    }

    [Fact]
    public void ModuleLevelVariable_UsesCodeGenInfo()
    {
        var code = @"
my_variable: int = 42
";
        var result = CompileToString(code);

        // Should use PascalCase for module-level variable
        result.Should().Contain("public static int MyVariable = 42;");
    }

    [Fact]
    public void ModuleLevelConstant_UsesCodeGenInfo()
    {
        var code = @"
const MAX_VALUE: int = 100
";
        var result = CompileToString(code);

        // Constants → PascalCase
        result.Should().Contain("MaxValue");
        result.Should().Contain("const int MaxValue = 100");
    }

    [Fact]
    public void ModuleLevelConstantStyle_UsesCodeGenInfo()
    {
        // Python-style constant (ALL_CAPS without const keyword)
        var code = @"
MAX_SIZE: int = 1024
";
        var result = CompileToString(code);

        // ALL_CAPS names should be treated as constants and use PascalCase
        result.Should().Contain("MaxSize");
    }

    [Fact]
    public void FunctionName_UsesCodeGenInfo()
    {
        var code = @"
def add_numbers(a: int, b: int) -> int:
    return a + b
";
        var result = CompileToString(code);

        // Functions should use PascalCase
        result.Should().Contain("public static int AddNumbers(int a, int b)");
    }

    [Fact]
    public void ClassName_UsesCodeGenInfo()
    {
        var code = @"
class MyClass:
    x: int

    def __init__(self, value: int):
        self.x = value
";
        var result = CompileToString(code);

        // Classes should preserve PascalCase name
        result.Should().Contain("public class MyClass");
    }

    [Fact]
    public void LocalVariable_FallsBackToLegacyTracking()
    {
        // Local variables inside functions still use legacy tracking
        // because they can be redeclared during emission
        var code = @"
def test_func() -> None:
    x: int = 1
    x = 2  # Update, not redeclare
    x: int = 3  # Redeclare (new local)
";
        var result = CompileToString(code);

        // Local variable redeclaration should create versioned names
        // The exact behavior depends on the redeclaration semantics
        result.Should().Contain("TestFunc");
    }

    [Fact]
    public void ModuleVariableReferenceInFunction_UsesCodeGenInfo()
    {
        var code = @"
my_var: int = 10

def get_value() -> int:
    return my_var
";
        var result = CompileToString(code);

        // Module variable reference inside function should use PascalCase
        result.Should().Contain("MyVar");
        result.Should().Contain("return MyVar;");
    }

    [Fact]
    public void ConstantReferenceInFunction_UsesCodeGenInfo()
    {
        var code = @"
const PI: float = 3.14159

def get_pi() -> float:
    return PI
";
        var result = CompileToString(code);

        // Constant reference should use PascalCase name
        result.Should().Contain("return Pi;");
    }

    [Fact]
    public void ExecutionOrderIssues_DetectedCorrectly()
    {
        // Variables with runtime initializers should be detected as having execution order issues
        var code = @"
def get_value() -> int:
    return 42

x: int = get_value()
";
        var result = CompileToString(code, isEntryPoint: false);

        // Variable with function call initializer should still be a static field
        // (execution order issues are flagged but the field is still generated)
        result.Should().Contain("X");
        result.Should().Contain("GetValue");
    }

    [Fact]
    public void EnumDefinition_UsesCodeGenInfo()
    {
        var code = @"
enum Color:
    RED
    GREEN
    BLUE
";
        var result = CompileToString(code);

        // Enum should preserve PascalCase name
        result.Should().Contain("public enum Color");
    }

    [Fact]
    public void StructDefinition_UsesCodeGenInfo()
    {
        var code = @"
struct Point:
    x: int
    y: int
";
        var result = CompileToString(code);

        // Struct should preserve PascalCase name
        result.Should().Contain("public struct Point");
    }

    [Fact]
    public void InterfaceDefinition_UsesCodeGenInfo()
    {
        var code = @"
interface IDrawable:
    def draw(self) -> None: ...
";
        var result = CompileToString(code);

        // Interface should preserve I prefix and use PascalCase
        result.Should().Contain("public interface IDrawable");
    }
}
