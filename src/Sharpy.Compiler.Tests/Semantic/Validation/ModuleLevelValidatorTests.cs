using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ModuleLevelValidatorTests
{
    private (Module module, SemanticContext context) Parse(string code, bool isEntryPoint = true)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        // Run name resolution
        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Run type checking to populate semantic info
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver)
        {
            IsEntryPoint = isEntryPoint
        };
        return (module, context);
    }

    #region Entry Point Rules

    [Fact]
    public void EntryPointWithMain_NoErrors()
    {
        var code = @"
def main():
    print(""Hello"")
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void EntryPointWithoutMain_ReportsError()
    {
        // Entry point files MUST have a main() function per the language specification
        var code = @"
def helper() -> int:
    return 42
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Entry point file requires a 'main()' function"));
    }

    [Fact]
    public void EntryPointWithOnlyTypedVariables_ReportsError()
    {
        // Entry point files MUST have a main() function even if they only have declarations
        var code = @"
counter: int = 0

def helper() -> int:
    return 42
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Entry point file requires a 'main()' function"));
    }

    [Fact]
    public void NonEntryPointWithoutMain_NoErrors()
    {
        var code = @"
def helper() -> int:
    return 42

class Utility:
    x: int = 0
";
        var (module, context) = Parse(code, isEntryPoint: false);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Type Annotation Rules

    [Fact]
    public void ModuleLevelWithTypeAnnotation_NoErrors()
    {
        var code = @"
counter: int = 0
name: str = ""test""

def main():
    print(counter)
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ModuleLevelAssignment_Error()
    {
        // Python-style "x = 42" is parsed as an Assignment statement, not a VariableDeclaration
        // Assignments are executable statements and not allowed at module level
        // Users should use "x: int = 42" for module-level variables
        var code = @"
x = 42

def main():
    pass
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Executable statements are not allowed at module level"));
    }

    [Fact]
    public void ModuleLevelConstWithoutTypeAnnotation_NoErrors()
    {
        // Const declarations can infer type from value
        var code = @"
const MAX_SIZE = 100
const NAME = ""app""

def main():
    print(MAX_SIZE)
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Executable Statement Rules

    [Fact]
    public void ModuleLevelPrint_Error()
    {
        var code = @"
print(""hello"")

def main():
    pass
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Executable statements are not allowed at module level"));
    }

    [Fact]
    public void ModuleLevelFunctionCall_Error()
    {
        var code = @"
def helper() -> None:
    print(""hi"")

helper()

def main():
    pass
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Executable statements are not allowed at module level"));
    }

    [Fact]
    public void ModuleLevelForLoop_Error()
    {
        var code = @"
for i in range(10):
    print(i)

def main():
    pass
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Executable statements are not allowed at module level"));
    }

    [Fact]
    public void ModuleLevelIfStatement_Error()
    {
        var code = @"
flag: bool = True

if flag:
    print(""yes"")

def main():
    pass
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Executable statements are not allowed at module level"));
    }

    #endregion

    #region Valid Declarations

    [Fact]
    public void ClassDefinition_NoErrors()
    {
        var code = @"
class Point:
    x: int
    y: int

def main():
    p: Point = Point()
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void StructDefinition_NoErrors()
    {
        var code = @"
struct Vector:
    x: float
    y: float

def main():
    v: Vector = Vector(1.0, 2.0)
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void InterfaceDefinition_NoErrors()
    {
        var code = @"
interface IDrawable:
    def draw(self) -> None:
        pass

def main():
    pass
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void EnumDefinition_NoErrors()
    {
        var code = @"
enum Color:
    Red
    Green
    Blue

def main():
    c: Color = Color.Red
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void FunctionDefinition_NoErrors()
    {
        var code = @"
def helper(x: int) -> int:
    return x * 2

def main():
    result: int = helper(21)
    print(result)
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void TypedVariableWithFunctionCallInitializer_NoErrors()
    {
        // Function calls in initializers are OK - they're part of the declaration
        var code = @"
def get_config() -> str:
    return ""config""

config: str = get_config()

def main():
    print(config)
";
        var (module, context) = Parse(code, isEntryPoint: true);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Library Module (Non-Entry Point)

    [Fact]
    public void LibraryModuleWithDefinitionsOnly_NoErrors()
    {
        var code = @"
def utility() -> int:
    return 42

class Helper:
    value: int = 0

const VERSION = ""1.0.0""
";
        var (module, context) = Parse(code, isEntryPoint: false);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void LibraryModuleWithExecutableStatements_Error()
    {
        // Even library modules can't have bare executable statements
        var code = @"
def utility() -> None:
    print(""hi"")

utility()
";
        var (module, context) = Parse(code, isEntryPoint: false);

        var validator = new ModuleLevelValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Executable statements are not allowed at module level"));
    }

    #endregion
}
